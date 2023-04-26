using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGBackend;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
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
    public async void GetClaesHistory()
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<SgDbContext>();
        var users = await context.User.ToArrayAsync();
        var sebi = users.First(u => u.Name == "s.claes");

        var connector = scope.ServiceProvider.GetRequiredService<SpotifyConnector>();

        var history = await connector.FetchAvailableContentHistory(sebi);
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