using Refit;
using SGBackend.Connector.Spotify.Model;

namespace SGBackend.Connector.Spotify;

public interface ISpotifyAuthApi
{
    [Post("/api/token")]
    public Task<ApiResponse<TokenResponse>> GetTokenFromRefreshToken(
        [Body(BodySerializationMethod.UrlEncoded)]
        Dictionary<string, object> data);
}