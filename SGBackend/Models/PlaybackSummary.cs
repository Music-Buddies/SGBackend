namespace SGBackend.Models;

public class PlaybackSummary
{
    public Guid Id { get; set; }
    
    public User User { get; set; }
    
    public Media Media { get; set; }
    
    public long TotalSeconds { get; set; }
    
    public DateTime lastListened { get; set; }
}