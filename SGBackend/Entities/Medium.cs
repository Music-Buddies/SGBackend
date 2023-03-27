using System.Runtime.InteropServices;
using Microsoft.EntityFrameworkCore;
using SGBackend.Models;

namespace SGBackend.Entities;

public enum MediumSource
{
    Spotify
}

[Index(nameof(LinkToMedium), IsUnique = true)]
public class Medium : BaseEntity
{
    public string Title { get; set; }

    public MediumSource MediumSource { get; set; }
    
    public string LinkToMedium { get; set; }

    public bool ExplicitContent { get; set; }

    public List<Artist> Artists { get; set; }

    public List<MediumImage> Images { get; set; }

    public string AlbumName { get; set; }

    public string ReleaseDate { get; set; }
    
    public MediaSummary ToMediaSummary(long totalSeconds)
    {
        return new MediaSummary
        {
            albumImages = SortBySize(Images),
            allArtists = Artists.Select(a => a.Name).ToArray(),
            explicitFlag = ExplicitContent,
            listenedSeconds = totalSeconds,
            songTitle = Title,
            // https://open.spotify.com/track/4EWCNWgDS8707fNSZ1oaA5
            linkToMedia = $"spotify:track:{LinkToMedium.Split("/").Last()}",
            albumName = AlbumName,
            releaseDate = ReleaseDate
        };
    }
    
    private static MediumImage[] SortBySize(List<MediumImage> mediumImages)
    {
        return mediumImages.OrderBy(i => i.height).ThenBy(i => i.width).ToArray();
    }
}