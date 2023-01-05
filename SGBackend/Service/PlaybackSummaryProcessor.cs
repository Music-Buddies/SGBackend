using Microsoft.EntityFrameworkCore;
using SGBackend.Models;

namespace SGBackend.Service;

/// <summary>
/// should be registered as singleton
/// </summary>
public class PlaybackSummaryProcessor : IHostedService
{
    private readonly PriorityQueue<Guid[], int> SummaryQueue = new();

    private readonly SemaphoreSlim _slim = new(1, 1);

    private readonly IServiceScopeFactory _scopeFactory;

    public PlaybackSummaryProcessor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public async Task Enqueue(List<PlaybackSummary> summaries)
    {
        await _slim.WaitAsync();
        try
        {
            SummaryQueue.Enqueue(summaries.Select(s => s.Id).ToArray(), summaries.Sum(s => s.TotalSeconds));
        }
        finally
        {
            _slim.Release();
        }
    }

    public async Task ProcessSummary()
    {
        await _slim.WaitAsync();
        try
        {
            var didDequeue = SummaryQueue.TryDequeue(out var summaries, out var priority);
            if (!didDequeue || summaries == null)
            {
                return;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var pbService = scope.ServiceProvider.GetService<PlaybackService>();
                var db = scope.ServiceProvider.GetService<SgDbContext>();
                

                await pbService.UpdateMutualPlaybackOverviews(await db.PlaybackSummaries.Include(ps => ps.User).Include(ps => ps.Medium).Where(ps => summaries.Contains(ps.Id)).ToListAsync());
            }
        }finally
        {
            _slim.Release();
        }
       
    }
    
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        new Thread(async () =>
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                await ProcessSummary();
                Thread.Sleep(1);
            }
        }).Start();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
    }
}