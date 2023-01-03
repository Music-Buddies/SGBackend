using Microsoft.EntityFrameworkCore;
using SGBackend.Connector;
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

    public async Task<List<PlaybackSummary>> UpsertPlaybackSummary(User user, List<PlaybackRecord> newInsertedRecords)
    {
        var existingSummaries = user.PlaybackSummaries;
        var insertedOrUpdatedSummaries = new List<PlaybackSummary>();

        foreach (var newInsertedRecordGrouping in newInsertedRecords.GroupBy(record => record.Media))
        {
            var existingSummary = existingSummaries.FirstOrDefault(s => s.Media == newInsertedRecordGrouping.Key);
            if (existingSummary != null)
            {
                // add sum of records on top and update last record timestamp
                existingSummary.TotalSeconds += newInsertedRecordGrouping.Sum(r => r.PlayedSeconds);
                existingSummary.lastListened = newInsertedRecords.MaxBy(r => r.PlayedAt).PlayedAt;
                insertedOrUpdatedSummaries.Add(existingSummary);
            }
            else
            {
                var newSummary = new PlaybackSummary()
                {
                    User = user,
                    Media = newInsertedRecordGrouping.Key,
                    lastListened = newInsertedRecords.MaxBy(r => r.PlayedAt).PlayedAt,
                    TotalSeconds = newInsertedRecordGrouping.Sum(r => r.PlayedSeconds)
                };
                // just dump new entry
                _dbContext.PlaybackSummaries.Add(newSummary);
                insertedOrUpdatedSummaries.Add(newSummary);
            }
        }
        await _dbContext.SaveChangesAsync();
        return insertedOrUpdatedSummaries;
    }

    public async Task<long> UpdatePlaybackMatches(List<PlaybackSummary> newInsertedSummaries, User user)
    {
        var affectedMedia = newInsertedSummaries.Select(ps => ps.Media).ToArray();
        
        // get other summaries of same media type
        var existingSummariesOfSameMedia = await _dbContext.PlaybackSummaries.Include(ps => ps.User)
            .Include(ps => ps.Media)
            .Where(ps => affectedMedia.Contains(ps.Media)).ToArrayAsync();
        existingSummariesOfSameMedia = existingSummariesOfSameMedia.Except(newInsertedSummaries).ToArray();

        var otherUsers = existingSummariesOfSameMedia.Select(s => s.User).Distinct().ToArray();

        var playBackMatches = new List<PlaybackMatch>();
        
        foreach (var newInsertedSummary in newInsertedSummaries)
        {
            var otherSummaries = existingSummariesOfSameMedia.Where(s => s.Media == newInsertedSummary.Media).ToArray();

            foreach (var otherSummary in otherSummaries)
            {
                // create playbackmatches
                playBackMatches.Add(new PlaybackMatch()
                {
                    User1 = user,
                    User2 = otherSummary.User,
                    Media = newInsertedSummary.Media,
                    listenedTogetherSeconds = Math.Min(newInsertedSummary.TotalSeconds, otherSummary.TotalSeconds),
                });
            }
        }
        
        // just delete existing playback matches
        var matchesToDelete = await _dbContext.PlaybackMatches.Where(pm => (pm.User1 == user && otherUsers.Contains(pm.User2))
                                               || (pm.User2 == user && otherUsers.Contains(pm.User1))).ToArrayAsync();

        _dbContext.PlaybackMatches.RemoveRange(matchesToDelete);
        
        // add new
        _dbContext.PlaybackMatches.AddRange(playBackMatches);
        await _dbContext.SaveChangesAsync();

        return matchesToDelete.Length;

    }
}