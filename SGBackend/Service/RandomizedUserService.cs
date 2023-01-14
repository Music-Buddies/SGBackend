using System.Text.Json;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;

namespace SGBackend.Service;

/// <summary>
///     For development purposes only!
///     Generates random users based on the same dummy template data
/// </summary>
public class RandomizedUserService
{
    private static readonly Random rnd = new();

    private readonly SgDbContext _dbContext;
    
    private readonly UserService _userService;

    private readonly ParalellAlgoService _paralellAlgoService;


    public RandomizedUserService(SgDbContext dbContext, UserService userService, ParalellAlgoService paralellAlgoService)
    {
        _dbContext = dbContext;
        _userService = userService;
        _paralellAlgoService = paralellAlgoService;
    }

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[rnd.Next(s.Length)]).ToArray());
    }

    public SpotifyListenHistory GetRandomizedHistory()
    {
        var listenHistory =
            JsonSerializer.Deserialize<SpotifyListenHistory>(File.ReadAllText("fullListenHistory.json"));

        // get random tracks
        var randomItems = listenHistory.items.OrderBy(x => rnd.Next()).Take(10).ToList();
        // randomize listen times and listen duration

        foreach (var randomItem in randomItems)
        {
            // randomize listen duration and played at timestamp
            var randomFactor = rnd.Next(0, 200) / 100.0D;
            randomItem.track.duration_ms = (int)(randomItem.track.duration_ms * randomFactor);
            randomItem.played_at = randomItem.played_at.AddDays(rnd.Next(-100, 100));
        }

        return new SpotifyListenHistory
        {
            items = randomItems
        };
    }

    public User GetRandomizedDummyUser()
    {
        return new User
        {
            Name = RandomString(10),
            SpotifyId = RandomString(10),
            SpotifyProfileUrl = "https://miro.medium.com/max/659/1*8xraf6eyaXh-myNXOXkqLA.jpeg",
            SpotifyRefreshToken = RandomString(10)
        };
    }

    public async Task<List<User>> GenerateXRandomUsersAndCalc(int usersToGenerate)
    {
        var users = new List<User>();
        foreach (var i in Enumerable.Range(0, usersToGenerate))
        {
            var dummyUser = await _userService.AddUser(GetRandomizedDummyUser());
            users.Add(dummyUser);
        }

        await _dbContext.SaveChangesAsync();
        
        foreach (var user in users)
        {
            await _paralellAlgoService.Process(user.Id, GetRandomizedHistory());
        }

        return users;
    }
}