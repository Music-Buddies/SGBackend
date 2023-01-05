using System.Diagnostics.CodeAnalysis;
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

    /// <summary>
    /// Determine and insert new PlaybackRecords 
    /// </summary>
    /// <param name="user">Needs to include <see cref="User.PlaybackRecords"/></param>
    /// <param name="spotifyListenHistory"></param>
    /// <returns></returns>
    public async Task<List<PlaybackRecord>> InsertNewRecords(User user, SpotifyListenHistory spotifyListenHistory)
    {
        return await InsertNewRecords(new List<SpotifyHistoryWithUser>()
        {
            new()
            {
                user = user,
                SpotifyListenHistory = spotifyListenHistory
            }
        });
    }

    /// <summary>
    /// Determine and insert new PlaybackRecords for multiple users
    /// </summary>
    /// <param name="histories">Fetched histories + user, needs to include <see cref="User.PlaybackRecords"/></param>
    /// <returns></returns>
    public async Task<List<PlaybackRecord>> InsertNewRecords(List<SpotifyHistoryWithUser> histories)
    {
        await InsertMissingMedia(histories);
        var existingSpotifyMedia =
            await _dbContext.Media.Where(media => media.MediumSource == MediumSource.Spotify).ToArrayAsync();

        var records = new List<PlaybackRecord>();
        
        foreach (var history in histories)
        {
            var recordsToAdd = history.SpotifyListenHistory.GetPlaybackRecords(existingSpotifyMedia, history.user);
        
            // filter out new records to insert
            var latestPlaybackRecord = history.user.PlaybackRecords.OrderByDescending(record => record.PlayedAt).FirstOrDefault();
            if (latestPlaybackRecord != null)
            {
                recordsToAdd = recordsToAdd
                    .Where(record => record.PlayedAt > latestPlaybackRecord.PlayedAt).ToList();
            }
            records.AddRange(recordsToAdd);
        }

        if (records.Any())
        {
            await _dbContext.AddRangeAsync(records);
            await _dbContext.SaveChangesAsync();
        }

        return records;
    }

    private async Task InsertMissingMedia(List<SpotifyHistoryWithUser> histories)
    {
        var media = new List<Medium>();
        foreach (var spotifyHistoryWithUser in histories)
        {
            media.AddRange(spotifyHistoryWithUser.SpotifyListenHistory.GetMedia());
        }
        
        var dbExistingMedia = await _dbContext.Media.ToArrayAsync();

        var mediaToInsert = media.DistinctBy(m => m.LinkToMedium).Where(m => !dbExistingMedia.Any(existingMedia =>
            existingMedia.LinkToMedium == m.LinkToMedium
            && existingMedia.MediumSource == m.MediumSource)).ToArray();

        await _dbContext.Media.AddRangeAsync(mediaToInsert);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<PlaybackSummary>> UpsertPlaybackSummary(List<PlaybackRecord> newInsertedRecords)
    {
        var insertedOrUpdatedSummaries = new List<PlaybackSummary>();

        foreach (var newRecordsForUser in newInsertedRecords.GroupBy(record => record.User))
        {
            insertedOrUpdatedSummaries.AddRange(await UpsertPlaybackSummaryNoSave(newRecordsForUser.ToList()));
        }
        
        await _dbContext.SaveChangesAsync();
        return insertedOrUpdatedSummaries;
    }
    
    private async Task<List<PlaybackSummary>> UpsertPlaybackSummaryNoSave(List<PlaybackRecord> records)
    {
        var user = records.First().User;

        var upsertedSummaries = new List<PlaybackSummary>();
        
        foreach (var newInsertedRecordGrouping in records.GroupBy(record => record.Medium))
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
                var newSummary = new PlaybackSummary()
                {
                    User = user,
                    Medium = newInsertedRecordGrouping.Key,
                    LastListened = newInsertedRecordGrouping.MaxBy(r => r.PlayedAt).PlayedAt,
                    TotalSeconds = newInsertedRecordGrouping.Sum(r => r.PlayedSeconds),
                    NeedsCalculation = true
                };
                // just dump new entry
                _dbContext.PlaybackSummaries.Add(newSummary);
                upsertedSummaries.Add(newSummary);
            }
        }

        return upsertedSummaries;
    }
    
    public async Task UpdateMutualPlaybackOverviews(PlaybackSummary upsertedSummary)
    {
        await UpdateMutualPlaybackOverviews(new List<PlaybackSummary>() { upsertedSummary });
    }
    
    public async Task UpdateMutualPlaybackOverviews(List<PlaybackSummary> upsertedSummaries)
    {
        var user = upsertedSummaries.First().User;
        if (upsertedSummaries.Any(s => s.User != user))
        {
            throw new Exception("summaries of multiple users provided");
        }

        if (user == null)
        {
            throw new Exception("user must be included");
        }

        var affectedMedia = upsertedSummaries.Select(s => s.Medium).Distinct().ToArray();

        var otherPlaybackSummaries =
            await _dbContext.PlaybackSummaries
                .Include(ps => ps.Medium)
                .Include(ps => ps.User)
                .Where(ps => affectedMedia.Contains(ps.Medium) && ps.User != user).ToListAsync();
        
        var otherSummariesByMedia = otherPlaybackSummaries.Except(upsertedSummaries).GroupBy(ps => ps.Medium)
            .ToDictionary(g => g.Key, g => g.ToList());
        
        var playbackOverviews = await _dbContext.MutualPlaybackOverviews
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
            if(otherSummaries == null) continue;
            
            foreach (var otherSummary in otherSummaries)
            {
                var playbackOverview = overviewsByOtherUser[otherSummary.User];
                
                var mutualPlaybackEntry = playbackOverview.MutualPlaybackEntries
                    .FirstOrDefault(e => e.Medium == otherSummary.Medium);

                if (mutualPlaybackEntry != null)
                {
                    // update seconds
                    mutualPlaybackEntry.PlaybackSeconds = Math.Min(otherSummary.TotalSeconds, upsertedSummary.TotalSeconds);
                }
                else
                {
                    // create
                    playbackOverview.MutualPlaybackEntries.Add(new MutualPlaybackEntry()
                    {
                        Medium = upsertedSummary.Medium,
                        PlaybackSeconds = Math.Min(otherSummary.TotalSeconds, upsertedSummary.TotalSeconds),
                        MutualPlaybackOverview = playbackOverview
                    });
                }
            }
        }

        await _dbContext.SaveChangesAsync();
    }
}