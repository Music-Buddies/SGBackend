namespace SGBackend.Models;

public class PlaybackMatch
{
    public Guid Id { get; set; }
    
    public User User1 { get; set; }
    
    public User User2 { get; set; }
    
    public Media Media { get; set; }
    
    public long listenedTogetherSeconds { get; set; }

    public User GetOtherUser(User user)
    {
        return User1 == user ? User2 : User1;
    }
}