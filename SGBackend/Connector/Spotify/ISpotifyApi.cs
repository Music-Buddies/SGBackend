using Refit;
using SGBackend.Connector.Spotify.Model;

namespace SGBackend.Connector.Spotify;

public interface ISpotifyApi
{
    [Get("/v1/me/player/recently-played?limit=50")]
    public Task<SpotifyListenHistory> GetAvailableHistory([Header("Authorization")] string bearerToken);
    
    [Get("/v1/audio-features/{id}")]
    public Task<ApiResponse<FeatureResponse>> GetFeatures([Header("Authorization")] string bearerToken, string id);
}