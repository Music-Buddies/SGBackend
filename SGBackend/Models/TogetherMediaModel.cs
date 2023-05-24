namespace SGBackend.Models;

public class TogetherMediaModel : MediaModel
{
    public long listenedSeconds { get; set; }

    public long listenedSecondsMatch { get; set; }
    
    public bool hidden { get; set; }
}