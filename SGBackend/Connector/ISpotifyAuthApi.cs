using Refit;

namespace SGBackend.Connector;

public interface ISpotifyAuthApi
{
    [Post("/api/token")]
    public Task<TokenResponse> GetTokenFromRefreshToken([Body(BodySerializationMethod.UrlEncoded)] Dictionary<string, object> data);

}