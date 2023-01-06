namespace SGBackend.Entities
{
    public class Secrets
    {
        public string JwtKey { get; set; }
        public string SpotifyClientId { get; set;}
        public string SpotifyClientSecret { get; set;}
        public string DBConnectionString { get; set;}
    }
}
