using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;

namespace SGBackend.Service;

/// <summary>
/// needs to be registered as singleton
/// </summary>
public class UserService
{
    private readonly SemaphoreSlim _addUserSlim = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;

    public UserService(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task<User> AddUser(User user)
    {
        await _addUserSlim.WaitAsync();
        try
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
                var allExistingUsers = await dbContext.User.ToArrayAsync();

                dbContext.User.Add(user);

                // also fetch all other users and pre-create overviews
                foreach (var existingUser in allExistingUsers)
                    dbContext.MutualPlaybackOverviews.Add(new MutualPlaybackOverview
                    {
                        User1 = user,
                        User2 = existingUser
                    });

                await dbContext.SaveChangesAsync();
                return user;
            }
        }
        finally
        {
            _addUserSlim.Release();
        }
    }
}