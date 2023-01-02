namespace SGBackend.Models;

public class PlaybackRecord
{
    public Guid Id { get; set; }
    
    public Media Media { get; set; }
    
    public User User { get; set; }
    
    public DateTime PlayedAt { get; set; }
    
    public long PlayedSeconds { get; set; }
}