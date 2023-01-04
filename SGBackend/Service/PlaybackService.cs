using Microsoft.EntityFrameworkCore;
using SGBackend.Connector;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Service;

public class PlaybackService
{
    private readonly SgDbContext _dbContext;

    public PlaybackService(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task InsertMissingMedia(SpotifyListenHistory spotifyListenHistory)
    {
        var media = spotifyListenHistory.GetMedia();
        var dbExistingMedia = await _dbContext.Media.ToArrayAsync();

        var mediaToInsert = media.Where(media => !dbExistingMedia.Any(existingMedia =>
            existingMedia.LinkToMedia == media.LinkToMedia
            && existingMedia.MediaSource == media.MediaSource)).ToArray();

        await _dbContext.Media.AddRangeAsync(mediaToInsert);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<PlaybackRecord>> InsertNewRecords(User user, SpotifyListenHistory spotifyListenHistory)
    {
        await InsertMissingMedia(spotifyListenHistory);
        var existingSpotifyMedia =
            await _dbContext.Media.Where(media => media.MediaSource == MediaSource.Spotify).ToArrayAsync();

        var records = spotifyListenHistory.GetPlaybackRecords(existingSpotifyMedia, user);
        
        // filter out new records to insert
        var latestPlaybackRecord = user.PlaybackRecords.OrderByDescending(record => record.PlayedAt).FirstOrDefault();
        if (latestPlaybackRecord != null)
        {
            records = records
                .Where(record => record.PlayedAt > latestPlaybackRecord.PlayedAt).ToList();
        }

        if (records.Any())
        {
            await _dbContext.AddRangeAsync(records);
            await _dbContext.SaveChangesAsync();
        }

        return records;
    }

    private async Task<List<PlaybackSummary>> UpsertPlaybackSummaryNoSave(List<PlaybackRecord> records, User user)
    {
        if (records.Any(r => r.User != user))
        {
            throw new Exception("method should only be called with records of the same user");
        }
        
        var upsertedSummaries = new List<PlaybackSummary>();
        
        foreach (var newInsertedRecordGrouping in records.GroupBy(record => record.Media))
        {
            var existingSummary = user.PlaybackSummaries.FirstOrDefault(s => s.Media == newInsertedRecordGrouping.Key);
            if (existingSummary != null)
            {
                // add sum of records on top and update last record timestamp
                existingSummary.TotalSeconds += newInsertedRecordGrouping.Sum(r => r.PlayedSeconds);
                existingSummary.lastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt;
                upsertedSummaries.Add(existingSummary);
            }
            else
            {
                var newSummary = new PlaybackSummary()
                {
                    User = user,
                    Media = newInsertedRecordGrouping.Key,
                    lastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt,
                    TotalSeconds = newInsertedRecordGrouping.Sum(r => r.PlayedSeconds)
                };
                // just dump new entry
                _dbContext.PlaybackSummaries.Add(newSummary);
                upsertedSummaries.Add(newSummary);
            }
        }

        return upsertedSummaries;

    }
    
    public async Task<List<PlaybackSummary>> UpsertPlaybackSummary(List<PlaybackRecord> newInsertedRecords)
    {
        var insertedOrUpdatedSummaries = new List<PlaybackSummary>();

        foreach (var newRecordsForUser in newInsertedRecords.GroupBy(record => record.User))
        {
            var user = newRecordsForUser.Key;
            insertedOrUpdatedSummaries.AddRange(await UpsertPlaybackSummaryNoSave(newRecordsForUser.ToList(), user));
        }
        
        await _dbContext.SaveChangesAsync();
        return insertedOrUpdatedSummaries;
    }

    public async Task<List<PlaybackSummary>> UpsertPlaybackSummary(List<PlaybackRecord> records, User user)
    {
        var summaries = await UpsertPlaybackSummaryNoSave(records, user);
        await _dbContext.SaveChangesAsync();
        return summaries;
    }

    private async Task<List<ListenedTogetherRecord>> ProcessSummariesForMediaNoSave(List<PlaybackSummary> summaries, Media media)
    {
        if (summaries.Any(s => s.Media != media))
        {
            throw new Exception("summaries need to be of same media");
        }

        var listenedTogetherRecords = new List<ListenedTogetherRecord>();
        
        // compare each summary to every other summary - once per pair is enough (m/s indexes are offset so we dont compare double)
        for (var m = 0; m < summaries.Count; m++)
        {
            var mSummary = summaries[m];
            for (var s = m +1; s < summaries.Count; s++)
            {
                var sSummary = summaries[s];
                // update record / create new
                var togetherRecord = mSummary.ListenedTogetherRecords.FirstOrDefault(ltr => ltr.OfUser(sSummary.User));

                if (togetherRecord != null)
                {
                    togetherRecord.listenedTogether = Math.Min(mSummary.TotalSeconds, sSummary.TotalSeconds);
                    listenedTogetherRecords.Add(togetherRecord);
                }
                else
                {
                    var newRecord = new ListenedTogetherRecord()
                    {
                        media = media,
                        user1 = mSummary.User,
                        user2 = sSummary.User,
                        listenedTogether = Math.Min(mSummary.TotalSeconds, sSummary.TotalSeconds)
                    };
                    listenedTogetherRecords.Add(newRecord);
                    _dbContext.ListenedTogetherRecords.Add(newRecord);
                    
                    
                    sSummary.ListenedTogetherRecords.Add(newRecord);
                    mSummary.ListenedTogetherRecords.Add(newRecord);
                }
            }
        }

        return listenedTogetherRecords;
    }

    public async Task<List<ListenedTogetherRecord>> ProcessUpsertedSummaries(List<PlaybackSummary> upsertedSummaries)
    {
        var mediaToRecalc = upsertedSummaries.Select(s => s.Media).Distinct().ToArray();

        var ltrs = new List<ListenedTogetherRecord>();
        
        var affectedSummaries = await _dbContext.PlaybackSummaries.Include(ps => ps.User)
            .Where(ps => mediaToRecalc.Contains(ps.Media)).ToArrayAsync();
        
        // compare each summary to every other summary of this media
        foreach (var playbackSummaries in affectedSummaries.GroupBy(s => s.Media))
        {
            ltrs.AddRange(await ProcessSummariesForMediaNoSave(playbackSummaries.ToList(), playbackSummaries.Key));
        }

        await _dbContext.SaveChangesAsync();

        return ltrs;
    }

    public async Task UpdatePlaybackMatches(List<PlaybackSummary> newInsertedSummaries)
    {
        var affectedMedia = newInsertedSummaries.Select(ps => ps.Media).ToArray();
        
        // get other summaries of same media type
        var affectedExistingSummaries = await _dbContext.PlaybackSummaries.Include(ps => ps.User)
            .Include(ps => ps.Media)
            .Where(ps => affectedMedia.Contains(ps.Media)).ToArrayAsync();
      
        var allMatches = await _dbContext.PlaybackMatches.ToArrayAsync();
        var matchesToDelete = new List<PlaybackMatch>();
        var newPlayBackMatches = new List<PlaybackMatch>();
        
        foreach (var newSummariesUser in newInsertedSummaries.GroupBy(s => s.User))
        {
            var user = newSummariesUser.Key;
            var affectedMediaUser = newSummariesUser.Select(s => s.Media).ToArray();

            var otherAffectedSummaries = affectedExistingSummaries.Except(newSummariesUser).ToArray();
            var relevantOtherSummaries = otherAffectedSummaries.Where(os => affectedMediaUser.Contains(os.Media)).ToArray();
            
            var otherUsers = relevantOtherSummaries.Select(s => s.User).Distinct().ToArray();
            
            foreach (var playbackSummary in newSummariesUser)
            {
                var relevantOtherSummariesForMedia = relevantOtherSummaries.Where(s => s.Media == playbackSummary.Media).ToArray();
                foreach (var otherSummary in relevantOtherSummariesForMedia)
                {
                    // create playbackmatches
                    newPlayBackMatches.Add(new PlaybackMatch()
                    {
                        User1 = user,
                        User2 = otherSummary.User,
                        Media = playbackSummary.Media,
                        listenedTogetherSeconds = Math.Min(playbackSummary.TotalSeconds, otherSummary.TotalSeconds),
                    });
                }
            }
            // just delete existing playback matches
            matchesToDelete.AddRange(allMatches.Where(pm => (pm.User1 == user && otherUsers.Contains(pm.User2))
                                                         || (pm.User2 == user && otherUsers.Contains(pm.User1))).ToArray());
        }
        
        // delete old and add new
        _dbContext.PlaybackMatches.RemoveRange(matchesToDelete);
        _dbContext.PlaybackMatches.AddRange(newPlayBackMatches);
        await _dbContext.SaveChangesAsync();

    }
}