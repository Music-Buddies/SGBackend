using System.Text;
using Microsoft.Net.Http.Headers;
using Refit;
using SGBackend.Connector.Spotify;

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
        serviceCollection.AddRefitClient<ISpotifyAuthApi>().ConfigureHttpClient(httpClient =>
        {
            httpClient.BaseAddress = new Uri("https://accounts.spotify.com/");
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization,
                "Basic " + Base64Encode("de22eb2cc8c9478aa6f81f401bcaa23a:03e25493374146c987ee581f6f64ad1f"));
        });
    }

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
}