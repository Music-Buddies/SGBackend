using System.ComponentModel.DataAnnotations;
using SGBackend.Models;

namespace SGBackend.Entities;

public class MutualPlaybackOverview : BaseEntity
{
    public User User1 { get; set; }
    
    public User User2 { get; set; }

    public List<MutualPlaybackEntry> MutualPlaybackEntries { get; set; } = new();
    
    public long TotalSeconds { get; set; }
    
    public User GetOtherUser(User user)
    {
        return User1 == user ? User2 : User1;
    }
}