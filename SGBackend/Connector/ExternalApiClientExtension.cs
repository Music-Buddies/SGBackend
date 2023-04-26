using System.Text;
using Microsoft.Net.Http.Headers;
using Refit;
using SecretsProvider;
using SGBackend.Connector.Spotify;
using SGBackend.Models;

namespace SGBackend.Connector;

public static class ExternalApiClientExtension
{
    /// <summary>
    ///     Requires ISecretsProvider to be initialized
    /// </summary>
    /// <param name="serviceCollection"></param>
    public static void AddExternalApiClients(this IServiceCollection serviceCollection)
    {
        AddSpotifyApi(serviceCollection);
    }

    private static void AddSpotifyApi(IServiceCollection serviceCollection)
    {
        // fetch secrets
        var secretsProvider = serviceCollection.BuildServiceProvider().GetRequiredService<ISecretsProvider>();
        var secrets = secretsProvider.GetSecret<Secrets>();

        serviceCollection.AddRefitClient<ISpotifyApi>().ConfigureHttpClient(httpClient =>
        {
            httpClient.BaseAddress = new Uri("https://api.spotify.com/");
        });
        serviceCollection.AddRefitClient<ISpotifyAuthApi>().ConfigureHttpClient(httpClient =>
        {
            httpClient.BaseAddress = new Uri("https://accounts.spotify.com/");
            httpClient.DefaultRequestHeaders.Add(HeaderNames.Authorization,
                "Basic " + Base64Encode($"{secrets.SpotifyClientId}:{secrets.SpotifyClientSecret}"));
        });
    }

    public static string Base64Encode(string plainText)
    {
        var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
        return Convert.ToBase64String(plainTextBytes);
    }
}