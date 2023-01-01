using Refit;

namespace SGBackend.Connector;

public interface ISpotifyApi
{
    [Get("/v1/me/player/recently-played")]
    public Task<string> GetHistory([Header("Authorization")] string bearerToken);
}