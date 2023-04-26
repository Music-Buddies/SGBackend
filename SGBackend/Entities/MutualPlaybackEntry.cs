using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

[Index(nameof(MediumId), nameof(MutualPlaybackOverviewId), IsUnique = true)]
public class MutualPlaybackEntry : BaseEntity
{
    public Medium Medium { get; set; }
    
    public Guid MediumId { get; set; }
    
    public long PlaybackSecondsUser1 { get; set; }
    
    public long PlaybackSecondsUser2 { get; set; }

    public MutualPlaybackOverview MutualPlaybackOverview { get; set; }
    
    public Guid MutualPlaybackOverviewId { get; set; }
}