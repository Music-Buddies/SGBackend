namespace SGBackend.Entities;

public class MutualPlaybackEntry : BaseEntity
{
    public Medium Medium { get; set; }

    public long PlaybackSeconds { get; set; }

    public MutualPlaybackOverview MutualPlaybackOverview { get; set; }
}