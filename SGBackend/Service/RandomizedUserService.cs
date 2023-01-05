using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SGBackend.Connector;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Service;

/// <summary>
/// For development purposes only!
/// Generates random users based on the same dummy template data
/// </summary>
public class RandomizedUserService
{
    private readonly PlaybackService _playbackService;

    private readonly SgDbContext _dbContext;
    
    private static Random rnd = new Random();

    public RandomizedUserService(PlaybackService playbackService, SgDbContext dbContext)
    {
        _playbackService = playbackService;
        _dbContext = dbContext;
    }

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
    }
    
    public SpotifyListenHistory GetRandomizedHistory()
    {
        var listenHistory = JsonSerializer.Deserialize<SpotifyListenHistory>(File.ReadAllText("fullListenHistory.json"));
        
        // get random tracks
        var randomItems = listenHistory.items.OrderBy(x => rnd.Next()).Take(10).ToList();
        // randomize listen times and listen duration
        
        foreach (var randomItem in randomItems)
        {
            // randomize listen duration and played at timestamp
            var randomFactor = rnd.Next(0, 200) / 100.0D;
            randomItem.track.duration_ms = (int) (randomItem.track.duration_ms * randomFactor);
            randomItem.played_at = randomItem.played_at.AddDays(rnd.Next(-100, 100));
        }

        return new SpotifyListenHistory()
        {
            items = randomItems
        };
    }

    public User GetRandomizedDummyUser()
    {
        return new User()
        {
            Name = RandomString(10),
            SpotifyId = RandomString(10),
            SpotifyProfileUrl = "https://miro.medium.com/max/659/1*8xraf6eyaXh-myNXOXkqLA.jpeg",
            SpotifyRefreshToken = RandomString(10)
        };
    }

    public async Task<List<User>> GenerateXRandomUsersAndCalc(int usersToGenerate)
    {
        var existingUsers = await _dbContext.User.ToListAsync();
        var users = new List<User>();
        foreach (var i in Enumerable.Range(0,usersToGenerate))
        {
            
            var dummyUser = GetRandomizedDummyUser();
            users.Add(dummyUser);
            _dbContext.Add(dummyUser);
            
            // also fetch all other users and precreate listenedTogetherSummaries
            foreach (var existingUser in existingUsers)
            {
                _dbContext.MutualPlaybackOverviews.Add(new MutualPlaybackOverview()
                {
                    User1 = dummyUser,
                    User2 = existingUser,
                });
            }
            existingUsers.Add(dummyUser);
        }
        await _dbContext.SaveChangesAsync();

        var records = await _playbackService.InsertNewRecords(users.Select(u => new SpotifyHistoryWithUser()
        {
            user = u,
            SpotifyListenHistory = GetRandomizedHistory()
        }).ToList());

        var summaries = await _playbackService.UpsertPlaybackSummary(records);
        
        
        foreach (var playbackSummary in summaries.GroupBy(s => s.User))
        {
            await _playbackService.UpdateMutualPlaybackOverviews(playbackSummary.ToList());
        }

        return users;
    }
}