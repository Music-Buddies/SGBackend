using Microsoft.EntityFrameworkCore;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;

namespace SGBackend.Service;

/// <summary>
/// needs to be registered as singleton
/// </summary>
public class ParalellAlgoService
{
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly ILogger<ParalellAlgoService> _logger;

    public ParalellAlgoService(IServiceScopeFactory scopeFactory, ILogger<ParalellAlgoService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private readonly SemaphoreSlim _mediaGlobalLock = new SemaphoreSlim(1, 1);
    
    private async Task UpdateMedia(SpotifyListenHistory history)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            var dbExistingMedia = await dbContext.Media.ToArrayAsync();

            var mediaToInsert = history.GetMedia().DistinctBy(m => m.LinkToMedium).Where(m => !dbExistingMedia.Any(existingMedia =>
                existingMedia.LinkToMedium == m.LinkToMedium
                && existingMedia.MediumSource == m.MediumSource)).ToArray();
            await dbContext.Media.AddRangeAsync(mediaToInsert);
            await dbContext.SaveChangesAsync();
        }
    }
    
    private readonly Dictionary<Guid, SemaphoreSlim> _userSlims = new();

    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="history"></param>
    /// <returns>Ids of updated/inserted playbacksummaries</returns>
    private async Task<List<Guid>> ProcessRecordsUpdateSummaries(Guid userId, SpotifyListenHistory history)
    {
        
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            var existingSpotifyMedia =
                await dbContext.Media.Where(media => media.MediumSource == MediumSource.Spotify).ToArrayAsync();

            var user = await dbContext.User
                .Include(u => u.PlaybackSummaries).Include(u => u.PlaybackRecords)
                .FirstAsync(u => u.Id == userId);
            
            var recordsToAdd = history.GetPlaybackRecords(existingSpotifyMedia, user);
                
            var latestPlaybackRecord = user.PlaybackRecords.OrderByDescending(record => record.PlayedAt)
                .FirstOrDefault();
            if (latestPlaybackRecord != null)
                recordsToAdd = recordsToAdd
                    .Where(record => record.PlayedAt > latestPlaybackRecord.PlayedAt).ToList();

            if (recordsToAdd.Any())
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                
                // add them to db
                await dbContext.AddRangeAsync(recordsToAdd);
                await dbContext.SaveChangesAsync();
                
                // calculate summaries

                var upsertedSummaries = new List<PlaybackSummary>();

                foreach (var newInsertedRecordGrouping in recordsToAdd.GroupBy(record => record.Medium))
                {
                    var existingSummary = user.PlaybackSummaries.FirstOrDefault(s => s.Medium == newInsertedRecordGrouping.Key);
                    if (existingSummary != null)
                    {
                        // add sum of records on top and update last record timestamp
                        existingSummary.TotalSeconds += newInsertedRecordGrouping.Sum(r => r.PlayedSeconds);
                        existingSummary.LastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt;
                        existingSummary.NeedsCalculation = true;
                        upsertedSummaries.Add(existingSummary);
                    }
                    else
                    {
                        var newSummary = new PlaybackSummary
                        {
                            User = user,
                            Medium = newInsertedRecordGrouping.Key,
                            LastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt,
                            TotalSeconds = newInsertedRecordGrouping.Sum(r => r.PlayedSeconds),
                            NeedsCalculation = true
                        };
                        // just dump new entry
                        await dbContext.PlaybackSummaries.AddAsync(newSummary);
                        upsertedSummaries.Add(newSummary);
                    }
                }

                await dbContext.SaveChangesAsync();
                
                watch.Stop();
                var elapsedMs = watch.ElapsedMilliseconds;
                
                _logger.LogInformation("Update Summaries for {guid} took {ms} ms", userId, elapsedMs);
                
                return upsertedSummaries.Select(ps => ps.Id).ToList();
            }
            
            // no new records, return empty list
            return new List<Guid>();
        }
    }

    private readonly SemaphoreSlim _mutualCalcSlim = new SemaphoreSlim(1, 1);

    private async Task UpdateMutualPlaybackOverviews(Guid userId, List<Guid> summaryIds)
    {
        var watch = System.Diagnostics.Stopwatch.StartNew();
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            var user = await dbContext.User.FirstAsync(u => u.Id == userId);
            var upsertedSummaries =
                await dbContext.PlaybackSummaries.Include(ps => ps.Medium)
                    .Where(ps => summaryIds.Contains(ps.Id))
                    .ToArrayAsync();

            var affectedMedia = upsertedSummaries.Select(s => s.Medium).Distinct().ToArray();

            var otherPlaybackSummaries =
                await dbContext.PlaybackSummaries
                    .Include(ps => ps.Medium)
                    .Include(ps => ps.User)
                    .Where(ps => affectedMedia.Contains(ps.Medium) && ps.User != user).ToListAsync();

            var otherSummariesByMedia = otherPlaybackSummaries.Except(upsertedSummaries).GroupBy(ps => ps.Medium)
                .ToDictionary(g => g.Key, g => g.ToList());

            var playbackOverviews = await dbContext.MutualPlaybackOverviews
                .Include(lts => lts.MutualPlaybackEntries)
                .ThenInclude(lte => lte.Medium)
                .Include(lts => lts.User1)
                .Include(lts => lts.User2)
                .Where(lts => lts.User1 == user || lts.User2 == user).ToArrayAsync();

            var overviewsByOtherUser =
                playbackOverviews.ToDictionary(lts => lts.GetOtherUser(user), summary => summary);

            foreach (var upsertedSummary in upsertedSummaries)
            {
                otherSummariesByMedia.TryGetValue(upsertedSummary.Medium, out var otherSummaries);
                // there might just be no other summaries for this medium yet
                if (otherSummaries == null) continue;

                foreach (var otherSummary in otherSummaries)
                {
                    var playbackOverview = overviewsByOtherUser[otherSummary.User];

                    var mutualPlaybackEntry = playbackOverview.MutualPlaybackEntries
                        .FirstOrDefault(e => e.Medium == otherSummary.Medium);

                    if (mutualPlaybackEntry != null)
                        // update seconds
                        mutualPlaybackEntry.PlaybackSeconds =
                            Math.Min(otherSummary.TotalSeconds, upsertedSummary.TotalSeconds);
                    else
                        // create
                        playbackOverview.MutualPlaybackEntries.Add(new MutualPlaybackEntry
                        {
                            Medium = upsertedSummary.Medium,
                            PlaybackSeconds = Math.Min(otherSummary.TotalSeconds, upsertedSummary.TotalSeconds),
                            MutualPlaybackOverview = playbackOverview
                        });
                }
            }

            await dbContext.SaveChangesAsync();
            
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
                
            _logger.LogInformation("Update Overviews for {guid} took {ms} ms", userId, elapsedMs);
        }
    }
    
    public async Task Process(Guid userId, SpotifyListenHistory history)
    {
        // insert missing media globally locked
        await _mediaGlobalLock.WaitAsync();
        try
        {
            await UpdateMedia(history);
        }
        finally
        {
            _mediaGlobalLock.Release();
        }
        // insert records and update summaries, locked by user
        SemaphoreSlim userSlim;
        // get / create lock for user
        lock (_userSlims)
        {
            if (!_userSlims.ContainsKey(userId))
            {
                _userSlims.Add(userId, new SemaphoreSlim(1,1));
            }

            userSlim = _userSlims[userId];
        }

        List<Guid> summaries;
        await userSlim.WaitAsync();
        try
        {
            summaries = await ProcessRecordsUpdateSummaries(userId, history);
        }
        finally
        {
            userSlim.Release();
        }
        
        // calculate mutual playback entries, global lock
        await _mutualCalcSlim.WaitAsync();
        try
        {
            await UpdateMutualPlaybackOverviews(userId, summaries);
        }
        finally
        {
            _mutualCalcSlim.Release();
        }
    }
}