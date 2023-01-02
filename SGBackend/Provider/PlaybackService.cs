using Microsoft.EntityFrameworkCore;
using SGBackend.Connector;
using SGBackend.Models;

namespace SGBackend.Provider;

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

    public async Task UpdateSpotifyRecords(SpotifyListenHistory spotifyListenHistory, User user)
    {
        await InsertMissingMedia(spotifyListenHistory);
        var existingSpotifyMedia =
            await _dbContext.Media.Where(media => media.MediaSource == MediaSource.Spotify).ToArrayAsync();

        var records = spotifyListenHistory.GetPlaybackRecords(existingSpotifyMedia, user);
        
        // only insert new (after played at last in db
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
    }

    public async Task UpdatePlaybackSummary(User user)
    {
        var groupedByMedia = user.PlaybackRecords.GroupBy(record => record.Media).ToArray();

        var summaries = groupedByMedia.Select(group => new PlaybackSummary()
        {
            User = user,
            Media = group.Key,
            lastListened = group.OrderByDescending(record => record.PlayedAt).First().PlayedAt,
            TotalSeconds = group.Select(record => record.PlayedSeconds).Sum()
        });
        
        // TODO: implement more efficient logic
        
        _dbContext.PlaybackSummaries.RemoveRange(_dbContext.PlaybackSummaries.Where(r => r.User == user));
        _dbContext.PlaybackSummaries.AddRange(summaries);
        await _dbContext.SaveChangesAsync();
    }
}