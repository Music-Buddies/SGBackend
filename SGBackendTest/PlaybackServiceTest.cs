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
    public async Task TestPerformance()
    {
        var rndUserService = _serviceProvider.GetService<RandomizedUserService>();

        var rndUsers = await rndUserService.GenerateXRandomUsersAndCalc(10);
    }

    [Fact]
    public async Task Test()
    {
        var db = _serviceProvider.GetService<SgDbContext>();
        var rndUserService = _serviceProvider.GetService<RandomizedUserService>();

        var rndUsers = await rndUserService.GenerateXRandomUsersAndCalc(5);
        
        // find matches between rndUsers 
        var matchesBetweenRndUsers = db.MutualPlaybackOverviews.Include(m => m.MutualPlaybackEntries)
            .ThenInclude(e => e.Medium).Where(m => rndUsers.Contains(m.User1) && rndUsers.Contains(m.User2)).ToArray();
        
        // validate them
        foreach (var matchBetweenRndUsers in matchesBetweenRndUsers)
        {
            foreach (var mutualPlaybackEntry in matchBetweenRndUsers.MutualPlaybackEntries)
            {
                var media = mutualPlaybackEntry.Medium;
                var recordsUser1 =
                    db.PlaybackRecords.Where(pb => pb.User == matchBetweenRndUsers.User1 && pb.Medium == media).ToArray();
                var sumUser1 = recordsUser1.Sum(r => r.PlayedSeconds);
            
                var recordsUser2 = 
                    db.PlaybackRecords.Where(pb => pb.User == matchBetweenRndUsers.User2 && pb.Medium == media).ToArray();
                var sumUser2 = recordsUser2.Sum(r => r.PlayedSeconds);

                var listenedTogether = Math.Min(sumUser1, sumUser2);
                Assert.Equal(listenedTogether, mutualPlaybackEntry.PlaybackSeconds);
            }
           
        }
    }
}