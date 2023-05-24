using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Newtonsoft.Json;
using SecretsProvider;
using SGBackend;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Provider;

namespace SGBackendTest.Controllers;

public class DevTests : IClassFixture<WebApplicationFactory<Startup>>
{
    private readonly WebApplicationFactory<Startup> _factory;

    public DevTests(WebApplicationFactory<Startup> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async void TestFeatureMigration()
    {
        using var scope = _factory.Services.CreateScope();
        var secrets = scope.ServiceProvider.GetRequiredService<ISecretsProvider>().GetSecret<Secrets>();

        var client = _factory.CreateClient();
        await client.GetAsync($"admin/audio-feature-migration/{secrets.AdminToken}");
    }

    [Fact]
    public async void UserWithoutMatch()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SgDbContext>();
        
        
        var users = await context.User.ToArrayAsync();
        
        foreach (var user in users)
        {
            var overviews = await context.MutualPlaybackOverviews.Include(o => o.MutualPlaybackEntries)
                .Where(o => o.User1Id == user.Id || o.User2Id == user.Id).ToArrayAsync();
            var noMatches = overviews.All(o => !o.MutualPlaybackEntries.Any());
            
        }
    }

    [Fact]
    public async void FetchJoey()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SgDbContext>();
        var connector = scope.ServiceProvider.GetRequiredService<SpotifyConnector>();
        var joey = await context.User.FirstAsync(u => u.Name == "Joey");

        var history = await connector.FetchAvailableContentHistory(joey);
    }

    [Fact]
    public async void ValidateCalculation()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SgDbContext>();

        // validate summaries
        var usersWithSummaries = await context.User.Include(u => u.PlaybackSummaries).Include(u => u.PlaybackRecords)
            .ToArrayAsync();

        foreach (var userWithSummary in usersWithSummaries)
        {
            var groupedRecords = userWithSummary.PlaybackRecords.GroupBy(pr => pr.MediumId);

            foreach (var playbackRecords in groupedRecords)
            {
                var summary = userWithSummary.PlaybackSummaries.First(ps => ps.MediumId == playbackRecords.Key);
                Assert.Equal(summary.TotalSeconds, playbackRecords.Sum(pr => pr.PlayedSeconds));
            }
        }
    }

    [Fact]
    public async void GetClaesHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SgDbContext>();
        var users = await context.User.ToArrayAsync();
        var sebi = users.First(u => u.Name == "Sebastian Claes");

        var connector = scope.ServiceProvider.GetRequiredService<SpotifyConnector>();

        var history = await connector.FetchAvailableContentHistory(sebi);
        var historyJson = JsonConvert.SerializeObject(history);
    }

    [Fact]
    public async void TestConsumedTogetherTracks()
    {
        using var scope = _factory.Services.CreateScope();

        var scopedJwtProvider = scope.ServiceProvider.GetService<JwtProvider>();
        var context = scope.ServiceProvider.GetService<SgDbContext>();
        var users = await context.User.ToArrayAsync();
        var tobe = users.First(u => u.Name == "Tobe");
        var marc = users.First(u => u.Name == "Marc");
        var token = scopedJwtProvider.GetJwt(marc);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await client.GetAsync($"/user/matches/{tobe.Id.ToString()}/together-consumed/tracks");
        var body = await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async void TestMatchesNotShowing()
    {
        using var scope = _factory.Services.CreateScope();

        var scopedJwtProvider = scope.ServiceProvider.GetService<JwtProvider>();
        var context = scope.ServiceProvider.GetService<SgDbContext>();
        var users = await context.User.ToArrayAsync();
        var sebi = users.First(u => u.Name == "s.claes");
        var token = scopedJwtProvider.GetJwt(sebi);
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("Authorization", "Bearer " + token);
        var response = await client.GetAsync("/user/matches");
    }
    
}