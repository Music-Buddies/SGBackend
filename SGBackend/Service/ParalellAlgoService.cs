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

    
    public async Task UpdateAll()
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            var connector = scope.ServiceProvider.GetRequiredService<SpotifyConnector>();

            var summaryGuids = new List<Guid>();
            
            var users = await dbContext.User.Include(u => u.PlaybackRecords).ThenInclude(pbr => pbr.Medium).ToArrayAsync();
            
            foreach (var user in users)
            {
                await ProcessImport(user.Id, user.PlaybackRecords.ToList());
            }

            foreach (var user in users)
            {
                await UpdateMutualPlaybackOverviews(user.Id, summaryGuids);
            }
        }
    }
    
    private async Task UpdateMedia(SpotifyListenHistory history)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            var dbExistingMedia = await dbContext.Media.ToArrayAsync();

            var mediaToInsert = history.GetMedia().DistinctBy(m => m.LinkToMedium).Where(m => !dbExistingMedia.Any(existingMedia =>
                existingMedia.LinkToMedium == m.LinkToMedium
                && existingMedia.MediumSource == m.MediumSource)).ToArray();
           
            if (mediaToInsert.Any())
            {
                await dbContext.Media.AddRangeAsync(mediaToInsert);
                await dbContext.SaveChangesAsync();
            }
        }
    }
    
    private readonly Dictionary<Guid, SemaphoreSlim> _userSlims = new();
    
    /// <summary>
    /// 
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="history"></param>
    /// <returns>Ids of updated/inserted playbacksummaries</returns>
    private async Task<List<Guid>> ProcessRecordsUpdateSummaries(Guid userId, List<PlaybackRecord> addedRecords, List<PlaybackSummary> existingSummaries)
    {
        
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            
            if (addedRecords.Any())
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();

                // calculate summaries

                var upsertedSummaries = new List<PlaybackSummary>();

                foreach (var newInsertedRecordGrouping in addedRecords.GroupBy(record => record.MediumId))
                {
                    var existingSummary = existingSummaries.FirstOrDefault(s => s.MediumId == newInsertedRecordGrouping.Key);
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
                            UserId = userId,
                            MediumId = newInsertedRecordGrouping.Key,
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
                    
                    /*
                    long playbackSecondsUser1;
                    long playbackSecondsUser2;
                    if (otherSummary.User == mutualPlaybackEntry.MutualPlaybackOverview.User1 && upsertedSummary.User == mutualPlaybackEntry.MutualPlaybackOverview.User2)
                    {
                        playbackSecondsUser1 = otherSummary.TotalSeconds;
                        playbackSecondsUser2 = upsertedSummary.TotalSeconds;
                    }
                    else
                    {
                        playbackSecondsUser1 = upsertedSummary.TotalSeconds;
                        playbackSecondsUser2 = otherSummary.TotalSeconds;
                    }
                    */
                    
                    if (mutualPlaybackEntry != null)
                    {
                        // update seconds
                        mutualPlaybackEntry.PlaybackSeconds =
                            Math.Min(otherSummary.TotalSeconds, upsertedSummary.TotalSeconds);
                        //mutualPlaybackEntry.PlaybackSecondsUser1 = playbackSecondsUser1;
                        //mutualPlaybackEntry.PlaybackSecondsUser2 = playbackSecondsUser2;
                    }
                    else
                    {
                        // create
                        playbackOverview.MutualPlaybackEntries.Add(new MutualPlaybackEntry
                        {
                            Medium = upsertedSummary.Medium,
                            PlaybackSeconds = Math.Min(otherSummary.TotalSeconds, upsertedSummary.TotalSeconds),
                            //PlaybackSecondsUser1 = playbackSecondsUser1,
                            //PlaybackSecondsUser2 = playbackSecondsUser2,
                            MutualPlaybackOverview = playbackOverview
                        });
                    }
                        
                        
                      
                }
            }

            await dbContext.SaveChangesAsync();
            
            watch.Stop();
            var elapsedMs = watch.ElapsedMilliseconds;
                
            _logger.LogInformation("Update Overviews for {guid} took {ms} ms", userId, elapsedMs);
        }
    }

    public async Task ProcessImport(Guid userId, List<PlaybackRecord> records)
    {
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
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
                
                var user = await dbContext.User.Include(u => u.PlaybackSummaries).Include(u => u.PlaybackRecords)
                    .FirstAsync(u => u.Id == userId);
                
                summaries = await ProcessRecordsUpdateSummaries(userId, records, user.PlaybackSummaries);
            }
            
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
            using (var scope = _scopeFactory.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
                
                var existingSpotifyMedia =
                    await dbContext.Media.Where(media => media.MediumSource == MediumSource.Spotify).ToArrayAsync();
                
                var user = await dbContext.User.Include(u => u.PlaybackSummaries).Include(u => u.PlaybackRecords)
                    .FirstAsync(u => u.Id == userId);

                // 2 in one
                var records = history.GetPlaybackRecords(existingSpotifyMedia, user);

                await dbContext.PlaybackRecords.AddRangeAsync(records);
                await dbContext.SaveChangesAsync();
                
                summaries = await ProcessRecordsUpdateSummaries(userId, records, user.PlaybackSummaries);
            }
            
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