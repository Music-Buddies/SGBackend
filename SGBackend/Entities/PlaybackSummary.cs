using SGBackend.Controllers;
using SGBackend.Entities;

namespace SGBackend.Models;

public class PlaybackSummary
{
    public Guid Id { get; set; }
    
    public User User { get; set; }
    
    public Media Media { get; set; }
    
    public long TotalSeconds { get; set; }
    
    public DateTime lastListened { get; set; }

    public List<ListenedTogetherRecord> ListenedTogetherRecords { get; set; } = new List<ListenedTogetherRecord>();

    public MediaSummary ToMediaSummary()
    {
        return new MediaSummary()
        {
            albumImages = Media.Images.ToArray(),
            allArtists = Media.Artists.Select(a => a.Name).ToArray(),
            explicitFlag = Media.ExplicitContent,
            listenedSeconds = TotalSeconds,
            songTitle = Media.Title,
            linkToMedia = Media.LinkToMedia
        };
    }
}