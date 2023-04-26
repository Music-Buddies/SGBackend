namespace SGBackend.Models;

public class Match
{
    public string userId { get; set; }

    public string username { get; set; }

    public string? profileImage { get; set; }

    public long listenedTogetherSeconds { get; set; }
    
    public int rank { get; set; }
}