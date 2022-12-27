using Microsoft.EntityFrameworkCore;

namespace SGBackend;


public class User
{
    public string code { get; set; }
}
public class SgDbContext : DbContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseMySQL("server=localhost;database=library;user=user;password=password");
    }
    
    public DbSet<User> Book { get; set; }
}