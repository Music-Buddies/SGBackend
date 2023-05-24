namespace SGBackend.Models;

public class DiscoverMediaModel : MediaModel
{
    public string username { get; set; }

    public string profileUrl { get; set; }
    
    public long orderValue { get; set; }
    
    public long listenedSeconds { get; set; }
    
    public bool hidden { get; set; }
}