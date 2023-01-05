using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

public class SgDbContext : DbContext
{
    public DbSet<User> User { get; set; }

    public DbSet<Medium> Media { get; set; }

    public DbSet<PlaybackRecord> PlaybackRecords { get; set; }

    public DbSet<PlaybackSummary> PlaybackSummaries { get; set; }

    public DbSet<MediumImage> Images { get; set; }

    public DbSet<Artist> Artists { get; set; }

    public DbSet<MutualPlaybackOverview> MutualPlaybackOverviews { get; set; }

    public DbSet<MutualPlaybackEntry> MutualPlaybackEntries { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySQL("server=localhost;database=sg;user=root;password=root");
    }

    protected override void OnModelCreating(ModelBuilder modelbuilder)
    {
        base.OnModelCreating(modelbuilder);
    }
}