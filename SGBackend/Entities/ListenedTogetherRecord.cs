using SGBackend.Models;

namespace SGBackend.Entities;

public class ListenedTogetherRecord
{
    public Guid Id { get; set; }
    
    public Media media { get; set; }
    
    public User user1 { get; set; }
    
    public User user2 { get; set; }
    
    public long listenedTogether { get; set; }

    public bool OfUser(User user)
    {
        return user1 == user || user2 == user;
    }
}