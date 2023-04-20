namespace SGBackend.Models;

public class ProfileInformation
{
    public string username { get; set; }

    public string profileImage { get; set; }

    public DateTime? trackingSince { get; set; }
    
    public long totalListenedSeconds { get; set; }
    
    public DateTime? latestFetch { get; set; }
}