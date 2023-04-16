using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

// defines a singular record of a medium being played
[Index(nameof(UserId), nameof(MediumId), nameof(PlayedAt), IsUnique = true)]
public class PlaybackRecord : BaseUserEntity
{
    public Medium Medium { get; set; }
    
    public Guid MediumId { get; set; }
    
    public DateTime PlayedAt { get; set; }

    public int PlayedSeconds { get; set; }

    public ExportPlaybackRecord ToExportPlaybackRecord()
    {
        return new ExportPlaybackRecord
        {
            PlayedAt = PlayedAt,
            PlayedSeconds = PlayedSeconds,
            LinkToMedium = Medium.LinkToMedium
        };
    }
}

public class ExportPlaybackRecord {
    
    /// <summary>
    /// unique key
    /// </summary>
    public string LinkToMedium { get; set; }
    
    public DateTime PlayedAt { get; set; }

    public int PlayedSeconds { get; set; }

    public PlaybackRecord ToPlaybackRecord(Dictionary<string, Guid> mediumLinkMap)
    {
        return new PlaybackRecord
        {
            PlayedSeconds = PlayedSeconds,
            MediumId = mediumLinkMap[LinkToMedium],
            PlayedAt = PlayedAt
        };
    }
    
}