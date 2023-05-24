namespace SGBackend.Models;

public class TogetherMediaModel : MediaModel
{
    public long listenedSecondsYou { get; set; }

    public long listenedSecondsMatch { get; set; }
    
    public bool hidden { get; set; }
}