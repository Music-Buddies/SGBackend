using Refit;

namespace SGBackend.Connector;

public static class ExternalApiClientExtension
{
    public static void AddExternalApiClients(this IServiceCollection serviceCollection)
    {
        AddSpotifyApi(serviceCollection);
    }

    private static void AddSpotifyApi(IServiceCollection serviceCollection)
    {
        serviceCollection.AddRefitClient<ISpotifyApi>().ConfigureHttpClient(httpClient =>
        {
            httpClient.BaseAddress = new Uri("https://api.spotify.com/");
        });
    }
}