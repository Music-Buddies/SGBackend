using System.Text.Json;
using SGBackend.Connector;
using SGBackend.Models;

namespace SGBackend.Service;

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
        var newRecords = new List<PlaybackRecord>();
        
        var users = new List<User>();
        foreach (var i in Enumerable.Range(0,usersToGenerate))
        {
            var dummyUser = GetRandomizedDummyUser();
            users.Add(dummyUser);
            _dbContext.Add(dummyUser);
            await _dbContext.SaveChangesAsync();
        }
        
        foreach (var user in users)
        {
            var history = GetRandomizedHistory();
            var records =  await _playbackService.InsertNewRecords(user, history);
            newRecords.AddRange(records);
        }
        
        var summaries = await _playbackService.UpsertPlaybackSummary(newRecords);

        var ltrs = await _playbackService.ProcessUpsertedSummaries(summaries);
        
        //await _playbackService.UpdatePlaybackMatches(summaries);

        return users;
    }
}