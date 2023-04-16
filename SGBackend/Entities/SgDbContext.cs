using Microsoft.EntityFrameworkCore;
using SecretsProvider;
using SGBackend.Models;
using SGBackend.Provider;

namespace SGBackend.Entities;

public class SgDbContext : DbContext
{    
    
    private readonly ISecretsProvider _secretsProvider;
    public SgDbContext(ISecretsProvider secretsProvider)
    {
        _secretsProvider = secretsProvider;
    }

    public DbSet<User> User { get; set; }

    public DbSet<Medium> Media { get; set; }

    public DbSet<PlaybackRecord> PlaybackRecords { get; set; }

    public DbSet<PlaybackSummary> PlaybackSummaries { get; set; }

    public DbSet<MediumImage> Images { get; set; }

    public DbSet<Artist> Artists { get; set; }
    
    public DbSet<State> States { get; set; }

    public DbSet<MutualPlaybackOverview> MutualPlaybackOverviews { get; set; }

    public DbSet<MutualPlaybackEntry> MutualPlaybackEntries { get; set; }



    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySQL(_secretsProvider.GetSecret<Secrets>().DBConnectionString);
    }

    protected override void OnModelCreating(ModelBuilder modelbuilder)
    {
        base.OnModelCreating(modelbuilder);
    }
}