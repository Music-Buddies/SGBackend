using Microsoft.EntityFrameworkCore;
using SGBackend.Models;

namespace SGBackend;

public class SgDbContext : DbContext
{

    public DbSet<User> User { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySQL("server=localhost;database=sg;user=root;password=root");
    }

    protected override void OnModelCreating(ModelBuilder modelbuilder)
    {
        base.OnModelCreating(modelbuilder);

        modelbuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.ID);
            entity.Property(e => e.Name);
            entity.Property(e => e.SpotifyURL);
        });
    }
}