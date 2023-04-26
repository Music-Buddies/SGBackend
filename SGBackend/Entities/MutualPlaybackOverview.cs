using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

[Index(nameof(User1Id), nameof(User2Id), IsUnique = true)]
public class MutualPlaybackOverview : BaseEntity
{
    public User User1 { get; set; }

    public Guid User1Id { get; set; }

    public User User2 { get; set; }

    public Guid User2Id { get; set; }

    public List<MutualPlaybackEntry> MutualPlaybackEntries { get; set; } = new();

    public User GetOtherUser(User user)
    {
        var returnUser = User1 == user ? User2 : User1;
        return returnUser;
    }
}