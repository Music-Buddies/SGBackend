using Microsoft.EntityFrameworkCore;
using SGBackend.Models;
using Image = SGBackend.Connector.Image;

namespace SGBackend;

public class SgDbContext : DbContext
{
    public DbSet<User> User { get; set; }
    
    public DbSet<Media> Media { get; set; }
    
    public DbSet<PlaybackRecord> PlaybackRecords { get; set; }
    
    public DbSet<PlaybackSummary> PlaybackSummaries { get; set; }

    public DbSet<MediaImage> Images { get; set; }
    
    public DbSet<Artist> Artists { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySQL("server=localhost;database=sg;user=root;password=root");
    }

    protected override void OnModelCreating(ModelBuilder modelbuilder)
    {
        base.OnModelCreating(modelbuilder);
    }
}