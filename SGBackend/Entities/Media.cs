using SGBackend.Controllers;

namespace SGBackend.Models;

public enum MediaSource
{
    Spotify
}
public class Media
{
    public Guid Id { get; set; }
    
    public string Title { get; set; }
    
    public MediaSource MediaSource { get; set; }
    
    public string LinkToMedia { get; set; }
    
    public bool ExplicitContent { get; set; }
    
    public List<Artist> Artists { get; set; }

    public List<MediaImage> Images { get; set; }
}

