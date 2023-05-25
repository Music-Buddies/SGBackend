namespace SGBackend.Connector.Spotify.Model;

public class TokenResponse
{
    public string access_token { get; set; }

    public int expires_in { get; set; }
}