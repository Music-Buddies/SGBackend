using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Service;

public class UserService
{
    private readonly SgDbContext _dbContext;

    public UserService(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<User> AddUser(User user)
    {
        var allExistingUsers = await _dbContext.User.ToArrayAsync();

        _dbContext.User.Add(user);
                
        // also fetch all other users and precreate listenedTogetherSummaries
        foreach (var existingUser in allExistingUsers)
        {
            _dbContext.MutualPlaybackOverviews.Add(new MutualPlaybackOverview()
            {
                User1 = user,
                User2 = existingUser,
            });
        }

        await _dbContext.SaveChangesAsync();

        return user;
    }
}