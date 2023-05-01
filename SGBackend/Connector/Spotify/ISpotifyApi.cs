using Refit;

namespace SGBackend.Connector.Spotify;

public interface ISpotifyApi
{
    [Get("/v1/me/player/recently-played?limit=50")]
    public Task<SpotifyListenHistory> GetEntireAvailableHistory([Header("Authorization")] string bearerToken);

    [Get("/v1/me/player/recently-played?limit=50")]
    public Task<string> GetEntireAvailableHistoryStr([Header("Authorization")] string bearerToken);


    [Get("/v1/me")]
    public Task<SpotifyProfileResponse> GetProfile([Header("Authorization")] string bearerToken);
}