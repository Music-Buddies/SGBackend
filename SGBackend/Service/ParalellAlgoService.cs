using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;

namespace SGBackend.Service;

/// <summary>
///     needs to be registered as singleton
/// </summary>
public class ParalellAlgoService
{
    private readonly ILogger<ParalellAlgoService> _logger;

    private readonly SemaphoreSlim _mediaGlobalLock = new(1, 1);

    private readonly SemaphoreSlim _mutualCalcSlim = new(1, 1);
    private readonly IServiceScopeFactory _scopeFactory;

    private readonly Dictionary<Guid, SemaphoreSlim> _userSlims = new();

    public ParalellAlgoService(IServiceScopeFactory scopeFactory, ILogger<ParalellAlgoService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    private async Task UpdateMedia(SgDbContext dbContext, SpotifyListenHistory history)
    {
        var dbExistingMedia = await dbContext.Media.ToArrayAsync();

        var mediaToInsert = history.GetMedia().DistinctBy(m => m.LinkToMedium).Where(m =>
            !dbExistingMedia.Any(existingMedia =>
                existingMedia.LinkToMedium == m.LinkToMedium
                && existingMedia.MediumSource == m.MediumSource)).ToArray();

        if (mediaToInsert.Any())
        {
            await dbContext.Media.AddRangeAsync(mediaToInsert);
            await dbContext.SaveChangesAsync();
        }
    }

    public async Task FetchAndCalcUsers()
    {
        using var scope = _scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
        var sp = scope.ServiceProvider.GetRequiredService<SpotifyConnector>();
        
        var users = await dbContext.User.Include(u => u.Stats).ToArrayAsync();
        
        var watch = Stopwatch.StartNew();
        _logger.LogInformation("Fetching for Users");

        await Parallel.ForEachAsync(users, async (user, token) =>
        {
            var availableHistory = await sp.FetchAvailableContentHistory(user);
            if (availableHistory == null)// no access token
                return ;

            await Process(user.Id, availableHistory);

            user.Stats.LatestFetch = DateTime.Now;
        });
        
        watch.Stop();
        var elapsedMs = watch.ElapsedMilliseconds;

        _logger.LogInformation("Fetched for Users took {ms} ms", elapsedMs);
        
        await dbContext.SaveChangesAsync();
    }

    /// <summary>
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="history"></param>
    /// <returns>Ids of updated/inserted playbacksummaries</returns>
    private async Task<List<PlaybackSummary>> ProcessRecordsUpdateSummaries(SgDbContext dbContext, Guid userId,
        List<PlaybackRecord> addedRecords,
        List<PlaybackSummary> existingSummaries)
    {
        if (addedRecords.Any())
        {
            var watch = Stopwatch.StartNew();

            // calculate summaries
            var upsertedSummaries = new List<PlaybackSummary>();

            foreach (var newInsertedRecordGrouping in addedRecords.GroupBy(record => record.MediumId))
            {
                var existingSummary =
                    existingSummaries.FirstOrDefault(s => s.MediumId == newInsertedRecordGrouping.Key);
                if (existingSummary != null)
                {
                    // add sum of records on top and update last record timestamp
                    existingSummary.TotalSeconds += newInsertedRecordGrouping.Sum(r => r.PlayedSeconds);
                    existingSummary.LastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt;
                    upsertedSummaries.Add(existingSummary);
                }
                else
                {
                    var newSummary = new PlaybackSummary
                    {
                        UserId = userId,
                        MediumId = newInsertedRecordGrouping.Key,
                        LastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt,
                        TotalSeconds = newInsertedRecordGrouping.Sum(r => r.PlayedSeconds)
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

            return upsertedSummaries;
        }

        // no new records, return empty list
        return new List<PlaybackSummary>();
    }

    private async Task UpdateMutualPlaybackOverviews(SgDbContext dbContext, Guid userId,
        List<PlaybackSummary> upsertedSummariesOfUser)
    {
        var watch = Stopwatch.StartNew();

        var user = await dbContext.User.FirstAsync(u => u.Id == userId);

        var affectedMedia = upsertedSummariesOfUser.Select(s => s.MediumId).Distinct().ToArray();

        var otherPlaybackSummaries =
            await dbContext.PlaybackSummaries
                .Include(ps => ps.User)
                .Where(ps => affectedMedia.Contains(ps.MediumId) && ps.User != user).ToListAsync();

        var otherSummariesByMedia = otherPlaybackSummaries.Except(upsertedSummariesOfUser).GroupBy(ps => ps.MediumId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var playbackOverviews = await dbContext.MutualPlaybackOverviews
            .Include(lts => lts.MutualPlaybackEntries)
            .Include(lts => lts.User1)
            .Include(lts => lts.User2)
            .Where(lts => lts.User1 == user || lts.User2 == user).ToArrayAsync();

        var overviewsByOtherUser =
            playbackOverviews.ToDictionary(lts => lts.GetOtherUser(user), summary => summary);

        foreach (var upsertedSummary in upsertedSummariesOfUser)
        {
            otherSummariesByMedia.TryGetValue(upsertedSummary.MediumId, out var otherSummaries);
            // there might just be no other summaries for this medium yet
            if (otherSummaries == null) continue;

            foreach (var otherSummary in otherSummaries)
            {
                var playbackOverview = overviewsByOtherUser[otherSummary.User];

                var mutualPlaybackEntry = playbackOverview.MutualPlaybackEntries
                    .FirstOrDefault(e => e.MediumId == otherSummary.MediumId);


                long playbackSecondsUser1;
                long playbackSecondsUser2;

                if (playbackOverview.User1 == user)
                {
                    // the current user, of which all upserted summaries are from, is user1 of the overview (since they are shared objects)
                    playbackSecondsUser1 = upsertedSummary.TotalSeconds;
                    playbackSecondsUser2 = otherSummary.TotalSeconds;
                }
                else
                {
                    // the other way around
                    playbackSecondsUser2 = upsertedSummary.TotalSeconds;
                    playbackSecondsUser1 = otherSummary.TotalSeconds;
                }

                if (mutualPlaybackEntry != null)
                {
                    // update seconds
                    mutualPlaybackEntry.PlaybackSecondsUser1 = playbackSecondsUser1;
                    mutualPlaybackEntry.PlaybackSecondsUser2 = playbackSecondsUser2;
                }
                else
                {
                    // create
                    playbackOverview.MutualPlaybackEntries.Add(new MutualPlaybackEntry
                    {
                        MediumId = upsertedSummary.MediumId,
                        PlaybackSecondsUser1 = playbackSecondsUser1,
                        PlaybackSecondsUser2 = playbackSecondsUser2,
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

    public async Task ProcessImport(Guid userId)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();

            // insert records and update summaries, locked by user
            SemaphoreSlim userSlim;
            // get / create lock for user
            lock (_userSlims)
            {
                if (!_userSlims.ContainsKey(userId)) _userSlims.Add(userId, new SemaphoreSlim(1, 1));

                userSlim = _userSlims[userId];
            }

            List<PlaybackSummary> summaries;
            await userSlim.WaitAsync();
            try
            {
                var user = await dbContext.User.Include(u => u.PlaybackSummaries).Include(u => u.PlaybackRecords)
                    .FirstAsync(u => u.Id == userId);

                summaries = await ProcessRecordsUpdateSummaries(dbContext, userId, user.PlaybackRecords,
                    user.PlaybackSummaries);
            }
            finally
            {
                userSlim.Release();
            }

            // calculate mutual playback entries, global lock
            await _mutualCalcSlim.WaitAsync();
            try
            {
                await UpdateMutualPlaybackOverviews(dbContext, userId, summaries);
            }
            finally
            {
                _mutualCalcSlim.Release();
            }
        }
    }

    public async Task Process(Guid userId, SpotifyListenHistory history)
    {
        using (var scope = _scopeFactory.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<SgDbContext>();
            // insert missing media globally locked
            await _mediaGlobalLock.WaitAsync();
            try
            {
                await UpdateMedia(dbContext, history);
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
                if (!_userSlims.ContainsKey(userId)) _userSlims.Add(userId, new SemaphoreSlim(1, 1));

                userSlim = _userSlims[userId];
            }

            List<PlaybackSummary> summaries;
            await userSlim.WaitAsync();
            try
            {
                var existingSpotifyMedia =
                    await dbContext.Media.Where(media => media.MediumSource == MediumSource.Spotify).ToArrayAsync();

                var user = await dbContext.User.Include(u => u.PlaybackSummaries).Include(u => u.PlaybackRecords)
                    .FirstAsync(u => u.Id == userId);


                // 2 in one
                var records = history.GetPlaybackRecords(existingSpotifyMedia, user);

                if (user.PlaybackRecords.Any())
                {
                    var latestRecordedPlayedAt = user.PlaybackRecords.Select(pr => pr.PlayedAt).Max();
                    records = records.Where(r => r.PlayedAt > latestRecordedPlayedAt).ToList();
                }

                await dbContext.PlaybackRecords.AddRangeAsync(records);
                await dbContext.SaveChangesAsync();

                summaries = await ProcessRecordsUpdateSummaries(dbContext, userId, records, user.PlaybackSummaries);
            }
            finally
            {
                userSlim.Release();
            }

            // calculate mutual playback entries, global lock
            await _mutualCalcSlim.WaitAsync();
            try
            {
                await UpdateMutualPlaybackOverviews(dbContext, userId, summaries);
            }
            finally
            {
                _mutualCalcSlim.Release();
            }
        }
    }
}