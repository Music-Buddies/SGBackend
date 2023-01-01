namespace SGBackend.Models;

public class User
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public string SpotifyId { get; set; }
    public string SpotifyRefreshToken { get; set; }
}