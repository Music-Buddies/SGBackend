using Microsoft.EntityFrameworkCore;

namespace SGBackend.Entities;

[Index(nameof(UserBeingFollowedId), nameof(UserFollowingId), IsUnique = true)]
public class Follower : BaseEntity
{
    public Guid UserBeingFollowedId { get; set; }
    
    public User UserBeingFollowed { get; set; }
    
    public Guid UserFollowingId { get; set; }
    
    public User UserFollowing { get; set; }
}