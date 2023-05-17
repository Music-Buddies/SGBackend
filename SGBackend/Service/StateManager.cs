using SGBackend.Entities;
using EntityFrameworkQueryableExtensions = Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions;

namespace SGBackend.Service;

public class StateManager
{
    private readonly SgDbContext _dbContext;

    public StateManager(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<State> GetState()
    {
        var state = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync(_dbContext.States);
        if (state == null)
        {
            state = new State();
            _dbContext.Add(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }
}