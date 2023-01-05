using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;

namespace SGBackend.Service;

/// <summary>
///     should be registered as singleton, this class will only be needed when the available processing power
///     cant turn over the amount of incoming data (we will have made it by then)
/// </summary>
public class PlaybackSummaryProcessor : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly SemaphoreSlim _slim = new(1, 1);
    private readonly PriorityQueue<Guid[], int> _summaryQueue = new();

    public PlaybackSummaryProcessor(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
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

    public async Task Enqueue(List<PlaybackSummary> summaries)
    {
        await _slim.WaitAsync();
        try
        {
            _summaryQueue.Enqueue(summaries.Select(s => s.Id).ToArray(), summaries.Sum(s => s.TotalSeconds));
        }
        finally
        {
            _slim.Release();
        }
    }

    /// <summary>
    ///     is public only for testing purposes
    /// </summary>
    public async Task ProcessSummary()
    {
        await _slim.WaitAsync();
        try
        {
            var didDequeue = _summaryQueue.TryDequeue(out var summaries, out var priority);
            if (!didDequeue || summaries == null) return;

            using (var scope = _scopeFactory.CreateScope())
            {
                var pbService = scope.ServiceProvider.GetService<PlaybackService>();
                var db = scope.ServiceProvider.GetService<SgDbContext>();


                await pbService.UpdateMutualPlaybackOverviews(await db.PlaybackSummaries.Include(ps => ps.User)
                    .Include(ps => ps.Medium).Where(ps => summaries.Contains(ps.Id)).ToListAsync());
            }
        }
        finally
        {
            _slim.Release();
        }
    }
}