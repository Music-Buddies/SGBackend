using SGBackend.Models;

namespace SGBackend.Entities;

public class PlaybackSummary : BaseUserEntity
{
    public Medium Medium { get; set; }

    public int TotalSeconds { get; set; }

    public DateTime LastListened { get; set; }

    // TODO: replace with queue service
    public bool NeedsCalculation { get; set; }

    public MediaSummary ToMediaSummary()
    {
        return new MediaSummary
        {
            albumImages = Medium.Images.ToArray(),
            allArtists = Medium.Artists.Select(a => a.Name).ToArray(),
            explicitFlag = Medium.ExplicitContent,
            listenedSeconds = TotalSeconds,
            songTitle = Medium.Title,
            linkToMedia = Medium.LinkToMedium,
            albumName = Medium.AlbumName,
            releaseDate = Medium.ReleaseDate
        };
    }
}