namespace SGBackend.Models;

public class Match
{
    public string userId { get; set; }

    public string username { get; set; }

    public string profileUrl { get; set; }

    public long listenedTogetherSeconds { get; set; }
}