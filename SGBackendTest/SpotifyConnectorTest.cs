using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SGBackend;
using SGBackend.Connector;

namespace SGBackendTest;

public class SpotifyConnectorFixture
{
    public SpotifyConnectorFixture()
    {
        var services = new ServiceCollection();
        services.AddExternalApiClients();
        services.AddDbContext<SgDbContext>();
        services.AddScoped<SpotifyConnector>();
        ServiceProvider = services.BuildServiceProvider();
    }
    
    public ServiceProvider ServiceProvider { get; set; }
}

public class SpotifyConnectorTest : IClassFixture<SpotifyConnectorFixture>
{
    private ServiceProvider _serviceProvider;

    public SpotifyConnectorTest(SpotifyConnectorFixture fixture)
    {
        _serviceProvider = fixture.ServiceProvider;
    }

    [Fact]
    public async void Test()
    {
        var db = _serviceProvider.GetService<SgDbContext>();
        var connector = _serviceProvider.GetService<SpotifyConnector>();
        var user = await db.User.FirstAsync();

        var token = await connector.GetAccessTokenUsingRefreshToken(user);

        await connector.FetchAvailableContentHistory(token);
    }

}