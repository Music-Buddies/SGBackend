using Microsoft.EntityFrameworkCore;
using Quartz;
using SGBackend.Entities;
using SGBackend.Service;

namespace SGBackend.Connector.Spotify;

public class SpotifyGroupedFetchJob : IJob
{
    private readonly SgDbContext _dbContext;
    
    private readonly SpotifyConnector _spotifyConnector;

    private readonly ParalellAlgoService _algoService;

    public SpotifyGroupedFetchJob(SgDbContext dbContext, SpotifyConnector spotifyConnector, ParalellAlgoService algoService)
    {
        _dbContext = dbContext;
        _spotifyConnector = spotifyConnector;
        _algoService = algoService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var users = await _dbContext.User.Include(u => u.Stats).ToArrayAsync();

        foreach (var user in users)
        {
            var availableHistory = await _spotifyConnector.FetchAvailableContentHistory(user);
            if (availableHistory == null)
            {
                // no access token
                continue;
            }
            
            await _algoService.Process(user.Id, availableHistory);
            
            user.Stats.LatestFetch = DateTime.Now;
        }

        await _dbContext.SaveChangesAsync();
    }
}