using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;

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
        var state = await _dbContext.States.FirstOrDefaultAsync();
        if (state == null)
        {
            state = new State();
            _dbContext.Add(state);
            await _dbContext.SaveChangesAsync();
        }

        return state;
    }
    
}