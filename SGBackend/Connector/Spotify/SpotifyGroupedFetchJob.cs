using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SGBackend.Entities;
using SGBackend.Service;

namespace SGBackend.Connector.Spotify;

public class SpotifyGroupedFetchJob : IJob
{
    private readonly ParalellAlgoService _algoService;
    
    public SpotifyGroupedFetchJob(ParalellAlgoService algoService)
    {
        _algoService = algoService;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await _algoService.FetchAndCalcUsers();
    }
}