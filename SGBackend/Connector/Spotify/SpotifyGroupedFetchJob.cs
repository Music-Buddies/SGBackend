using Quartz;
using SGBackend.Service;

namespace SGBackend.Connector.Spotify;

public class SpotifyGroupedFetchJob : IJob
{
    private readonly MatchingService _algoService;

    public SpotifyGroupedFetchJob(MatchingService algoService)
    {
        _algoService = algoService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _algoService.FetchAndCalcUsers();
    }
}