using Refit;

namespace SGBackend.Connector;

public interface ISpotifyApi
{
    [Get("/v1/me/player/recently-played?limit=50")]
    public Task<SpotifyListenHistory> GetEntireAvailableHistory([Header("Authorization")] string bearerToken);
    
}