namespace SGBackend.Connector.Spotify;

public class SpotifyProfileResponse
{
    public string country { get; set; }
    public string display_name { get; set; }
    public string email { get; set; }
    public ExplicitContent explicit_content { get; set; }
    public ExternalUrls external_urls { get; set; }
    public Followers followers { get; set; }
    public string href { get; set; }
    public string id { get; set; }
    public List<Image> images { get; set; }
    public string product { get; set; }
    public string type { get; set; }
    public string uri { get; set; }
}

// Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
public class ExplicitContent
{
    public bool filter_enabled { get; set; }
    public bool filter_locked { get; set; }
}

public class Followers
{
    public string href { get; set; }
    public int total { get; set; }
}


