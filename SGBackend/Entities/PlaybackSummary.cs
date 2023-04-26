using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

[Index(nameof(MediumId), nameof(UserId), IsUnique = true)]
public class PlaybackSummary : BaseUserEntity
{
    public Guid UserId { get; set; }

    public Medium Medium { get; set; }

    public Guid MediumId { get; set; }

    public int TotalSeconds { get; set; }

    public DateTime LastListened { get; set; }
}