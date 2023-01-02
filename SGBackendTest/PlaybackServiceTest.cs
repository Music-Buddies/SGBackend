using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGBackend;
using SGBackend.Connector;
using SGBackend.Provider;

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
    public async void Test()
    {
        var db = _serviceProvider.GetService<SgDbContext>();
        var connector = _serviceProvider.GetService<SpotifyConnector>();
        var pbService = _serviceProvider.GetService<PlaybackService>();
        var user = await db.User.Include(u => u.PlaybackRecords).FirstAsync();

        var token = await connector.GetAccessTokenUsingRefreshToken(user);

        var history = await connector.FetchAvailableContentHistory(token);

        await pbService.UpdateSpotifyRecords(history, user);

        await pbService.UpdatePlaybackSummary(await db.User.Include(u => u.PlaybackSummaries)
            .FirstAsync(u => u.Id == user.Id));
    }
}