using SGBackend.Models;

namespace SGBackend.Connector;

public class UserLoggedInResult
{
    public User User { get; set; }
    
    public bool ExistedPreviously { get; set; }
    
}