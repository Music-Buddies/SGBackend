using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGBackend;
using SGBackend.Connector;
using SGBackend.Models;
using SGBackend.Provider;
using SGBackend.Service;

namespace SGBackendTest;

public class PlaybackServiceFixture
{
    public PlaybackServiceFixture()
    {
        var services = new ServiceCollection();
        services.AddExternalApiClients();
        services.AddDbContext<SgDbContext>();
        services.AddScoped<SpotifyConnector>();
        services.AddScoped<PlaybackService>();
        services.AddScoped<RandomizedUserService>();
        ServiceProvider = services.BuildServiceProvider();
    }
    
    public ServiceProvider ServiceProvider { get; set; }
}

public class PlaybackServiceTest : IClassFixture<PlaybackServiceFixture>
{
    private ServiceProvider _serviceProvider;

    public PlaybackServiceTest (PlaybackServiceFixture fixture)
    {
        _serviceProvider = fixture.ServiceProvider;
    }
    
    


    [Fact]
    public async Task UpdateMatches()
    {
        var pbService = _serviceProvider.GetService<PlaybackService>();
        
        var user1 = await InsertRecordsAndUpdateSummaries();
        var user2 = await InsertRecordsAndUpdateSummaries();

        var deletedMatches = await pbService.UpdatePlaybackMatches(user1.PlaybackSummaries, user1);
        Assert.Equal(0, deletedMatches);
        var deletedMatches2 = await pbService.UpdatePlaybackMatches(user1.PlaybackSummaries, user1);
        Assert.True(deletedMatches2 != 0);
    }

    [Fact]
    public async Task Test()
    {
        var db = _serviceProvider.GetService<SgDbContext>();
        var rndUserService = _serviceProvider.GetService<RandomizedUserService>();

        var rndUsers = await rndUserService.GenerateXRandomUsersAndCalc(5);
        
        // validate calculation
    }

    [Fact]
    public async Task<User> InsertRecordsAndUpdateSummaries()
    {
        // prepare sample data, same set of records just updated timestamps
        var historyJson = System.IO.File.ReadAllText("spotifyListenHistory0.json");
        var history1 = JsonSerializer.Deserialize<SpotifyListenHistory>(historyJson);
        var history2 = JsonSerializer.Deserialize<SpotifyListenHistory>(historyJson);
        foreach (var historyItem in history2.items)
        {
            historyItem.played_at = historyItem.played_at.AddDays(1);
        }

        // load services
        var db = _serviceProvider.GetService<SgDbContext>();
        var pbService = _serviceProvider.GetService<PlaybackService>();
        
        // setup dummy user
        var dummyUser = new User()
        {
            Name = "dummyname",
            SpotifyId = "dummyid",
            SpotifyProfileUrl = "dummyurl",
            SpotifyRefreshToken = "dummyrefreshtoken"
        };
        db.Add(dummyUser);
        await db.SaveChangesAsync();
        
        // insert first batch of records
        var insertNewRecords1 = await pbService.InsertNewRecords(dummyUser, history1);
        Assert.Equal(2, insertNewRecords1.Count);
        var insertNewRecords12 = await pbService.InsertNewRecords(dummyUser, history1);
        Assert.Empty(insertNewRecords12);
        
        // create first playback summaries
        var insertedSummaries1 = await pbService.UpsertPlaybackSummary(dummyUser, insertNewRecords1);
        Assert.Equal(2, insertedSummaries1.Count);
        var insertedSummaries1Checksum = insertedSummaries1.Sum(s => s.TotalSeconds);
        
        // insert second batch of records
        var insertNewRecords2 = await pbService.InsertNewRecords(dummyUser, history2);
        Assert.Equal(2, insertNewRecords2.Count);
        
        // update summaries with second batch
        var insertedSummaries2 = await pbService.UpsertPlaybackSummary(dummyUser, insertNewRecords2);
        Assert.Equal(2, insertedSummaries2.Count);
        Assert.Equal(insertedSummaries1Checksum * 2, insertedSummaries2.Sum(s => s.TotalSeconds));

        return dummyUser;
    }
}