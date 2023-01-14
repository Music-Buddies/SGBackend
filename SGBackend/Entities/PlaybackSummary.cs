using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using SGBackend.Models;

namespace SGBackend.Entities;

[Index(nameof(MediumId), nameof(UserId), IsUnique = true)]
public class PlaybackSummary : BaseUserEntity
{
    public Guid UserId { get; set; }
    
    public Medium Medium { get; set; }
    
    public Guid MediumId { get; set; }

    public int TotalSeconds { get; set; }

    public DateTime LastListened { get; set; }

    // TODO: replace with queue service
    public bool NeedsCalculation { get; set; }

    public MediaSummary ToMediaSummary()
    {
        return new MediaSummary
        {
            albumImages = SortBySize(Medium.Images),
            allArtists = Medium.Artists.Select(a => a.Name).ToArray(),
            explicitFlag = Medium.ExplicitContent,
            listenedSeconds = TotalSeconds,
            songTitle = Medium.Title,
            linkToMedia = Medium.LinkToMedium,
            albumName = Medium.AlbumName,
            releaseDate = Medium.ReleaseDate
        };
    }

    private static MediumImage[] SortBySize(List<MediumImage> mediumImages)
    {
        return mediumImages.OrderBy(i => i.height).ThenBy(i => i.width).ToArray();
    }
}