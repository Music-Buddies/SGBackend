namespace SGBackend.Entities;

public class MutualPlaybackOverview : BaseEntity
{
    public User User1 { get; set; }

    public User User2 { get; set; }

    public List<MutualPlaybackEntry> MutualPlaybackEntries { get; set; } = new();

    public User GetOtherUser(User user)
    {
        var returnUser = User1 == user ? User2 : User1;
        return returnUser;
    }
}