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

    private readonly ILogger<SpotifyGroupedFetchJob> _logger;

    public SpotifyGroupedFetchJob(SgDbContext dbContext, SpotifyConnector spotifyConnector, ParalellAlgoService algoService, ILogger<SpotifyGroupedFetchJob> logger)
    {
        _dbContext = dbContext;
        _spotifyConnector = spotifyConnector;
        _algoService = algoService;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var users = await _dbContext.User.Include(u => u.Stats).ToArrayAsync();

        foreach (var user in users)
        {
            var watch = System.Diagnostics.Stopwatch.StartNew();
            _logger.LogInformation("Fetching for User {userId}", user.Id);
            var availableHistory = await _spotifyConnector.FetchAvailableContentHistory(user);
            if (availableHistory == null)
            {
                // no access token
                continue;
            }
            
            await _algoService.Process(user.Id, availableHistory);
            
            user.Stats.LatestFetch = DateTime.Now;
            
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
                
            _logger.LogInformation("Fetched for User {userId} took {ms} ms", user.Id, elapsedMs);
        }

        await _dbContext.SaveChangesAsync();
    }
}