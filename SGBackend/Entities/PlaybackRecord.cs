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
}