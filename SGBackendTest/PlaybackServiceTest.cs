using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SecretsProvider;
using SGBackend.Connector;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Service;

namespace SGBackendTest;

public class PlaybackServiceFixture
{
    public PlaybackServiceFixture()
    {
        var services = new ServiceCollection();
        var builder = new ConfigurationBuilder();
        builder.AddUserSecrets<Secrets>();
        IConfiguration configuration = builder.Build();
        services.AddScoped<IConfiguration>(_ => configuration);

        services.AddDevSecretsProvider("SG");

        services.AddExternalApiClients();
        services.AddDbContext<SgDbContext>();
        services.AddScoped<SpotifyConnector>();
        services.AddScoped<RandomUserService>();
        services.AddScoped<UserService>();
        services.AddSingleton<MatchingService>();

        ServiceProvider = services.BuildServiceProvider();
    }

    public ServiceProvider ServiceProvider { get; set; }
}

public class PlaybackServiceTest : IClassFixture<PlaybackServiceFixture>
{
    private readonly ServiceProvider _serviceProvider;

    public PlaybackServiceTest(PlaybackServiceFixture fixture)
    {
        _serviceProvider = fixture.ServiceProvider;
    }

    [Fact]
    public async Task TestPerformance()
    {
        var rndUserService = _serviceProvider.GetService<RandomUserService>();

        var rndUsers = await rndUserService.GenerateXRandomUsersAndCalc(1);
    }

    [Fact]
    public async Task TestContinuity()
    {
        var rndUserService = _serviceProvider.GetService<RandomUserService>();
        var userService = _serviceProvider.GetService<UserService>();
        var algoService = _serviceProvider.GetService<MatchingService>();
        var db = _serviceProvider.GetService<SgDbContext>();

        var user = await userService.AddUser(rndUserService.GetRandomizedDummyUser());

        await algoService.Process(user.Id, rndUserService.GetRandomizedHistory());
        var insertedRecords = (await db.User
            .Include(u => u.PlaybackRecords)
            .FirstAsync(u => u.Id == user.Id)).PlaybackRecords.ToArray();
        var summaries = (await db.User
            .Include(u => u.PlaybackSummaries)
            .FirstAsync(u => u.Id == user.Id)).PlaybackSummaries.ToArray();

        Assert.NotEmpty(insertedRecords);
        Assert.NotEmpty(summaries);

        // move history forward and recalc, should have added more records
        var secondHistory = rndUserService.GetRandomizedHistory();
        foreach (var historyItem in secondHistory.items) historyItem.played_at = historyItem.played_at.AddYears(1);

        await algoService.Process(user.Id, secondHistory);
        var insertedRecords2 = (await db.User
            .Include(u => u.PlaybackRecords)
            .FirstAsync(u => u.Id == user.Id)).PlaybackRecords.ToArray();
        var summaries2 = (await db.User
            .Include(u => u.PlaybackSummaries)
            .FirstAsync(u => u.Id == user.Id)).PlaybackSummaries.ToArray();

        Assert.True(insertedRecords2.Length > insertedRecords.Length);
        Assert.True(summaries2.Length > summaries.Length);
    }

    [Fact]
    public async Task TestParalellism()
    {
        var userService = _serviceProvider.GetService<UserService>();
        var rndUserService = _serviceProvider.GetService<RandomUserService>();
        var db = _serviceProvider.GetService<SgDbContext>();

        // create dummy users
        var users = new List<User>();
        foreach (var i in Enumerable.Range(0, 5))
        {
            var dummyUser = await userService.AddUser(rndUserService.GetRandomizedDummyUser());
            users.Add(dummyUser);
        }

        await db.SaveChangesAsync();


        var tasks = new List<Task>();
        // insert histories in paralell
        foreach (var user in users)
        {
            var history = rndUserService.GetRandomizedHistory();

            var insertFunc = async () =>
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var algo = scope.ServiceProvider.GetService<MatchingService>();
                    await algo.Process(user.Id, history);
                }
            };

            foreach (var i in Enumerable.Range(0, 5)) tasks.Add(Task.Run(insertFunc));
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task Test()
    {
        var db = _serviceProvider.GetService<SgDbContext>();
        var rndUserService = _serviceProvider.GetService<RandomUserService>();
        var rndUsers = await rndUserService.GenerateXRandomUsersAndCalc(5);

        // find matches between rndUsers 
        var matchesBetweenRndUsers = db.MutualPlaybackOverviews.Include(po => po.User1)
            .Include(po => po.User2)
            .Include(m => m.MutualPlaybackEntries)
            .ThenInclude(e => e.Medium).Where(m => rndUsers.Contains(m.User1) && rndUsers.Contains(m.User2)).ToArray();

        // validate them
        foreach (var matchBetweenRndUsers in matchesBetweenRndUsers)
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
            Assert.Equal(listenedTogether,
                Math.Min(mutualPlaybackEntry.PlaybackSecondsUser2, mutualPlaybackEntry.PlaybackSecondsUser1));
        }
    }
}