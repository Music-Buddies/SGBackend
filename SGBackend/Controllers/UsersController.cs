using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Controllers;

[ApiController]
[Route("users")]
public class UsersController : ControllerBase
{
    private SgDbContext _dbContext;

    public UsersController(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    [Authorize]
    [HttpPost("search")]
    public async Task<ModelUser[]> PostUserSearch(SearchBody searchBody)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        return (await _dbContext.User.Where(u =>
            u.Id != userId && u.Name.ToLower().Contains(searchBody.searchString.ToLower())).ToArrayAsync()).Select(u => u.ToModelUser()).ToArray();
    }
    
    [Authorize]
    [HttpGet("{guid}/profile-information")]
    public async Task<ProfileInformation> GetProfileInformationForUser(string guid)
    {
        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords)
            .FirstAsync(u => u.Id == Guid.Parse(guid));
        return await GetProfileInformationGuid(dbUser, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value));
    }
    
    [Authorize]
    [HttpGet("{guid}/profile-media")]
    public async Task<ProfileMediaModel[]> GetProfileMediaForOtherUser(string guid, int? limit,
        [FromQuery(Name = "limit-key")] string? limitKey)
    {
        if (limitKey != null)
        {
            return await FetchProfileMediaUntil(Guid.Parse(guid), LimitKeyToDate(limitKey), limit);
        }

        return await FetchProfileMedia(Guid.Parse(guid), limit);
    }
    
    private async Task<ProfileMediaModel[]> FetchProfileMediaUntil(Guid userId, DateTime untilTime, int? limit)
    {
        var hiddenMedia = await _dbContext.HiddenMedia.Where(hm => hm.UserId == userId).ToArrayAsync();
        var hiddenMediaHashSet = hiddenMedia.Select(hm => hm.MediumId).ToHashSet();

        var records = await _dbContext.PlaybackRecords.Where(s => s.UserId == userId && s.PlayedAt > untilTime)
            .ToArrayAsync();
        var uniqueMediaIds = records.Select(r => r.MediumId).ToHashSet();

        var medias = await _dbContext.Media.Include(m => m.Artists).Include(m => m.Images)
            .Where(m => uniqueMediaIds.Contains(m.Id)).ToArrayAsync();
        var mediaMap = medias.ToDictionary(m => m.Id, m => m);

        // sum the records by medium id
        var pseudoSummaries = records.GroupBy(r => r.MediumId).Select(g => new PlaybackSummary
        {
            TotalSeconds = g.Sum(r => r.PlayedSeconds),
            Medium = mediaMap[g.Key],
            MediumId = g.Key
        }).OrderByDescending(ms => ms.TotalSeconds).ToArray();

        PlaybackSummary[] summaries;
        if (limit.HasValue)
            summaries = pseudoSummaries.Take(limit.Value).ToArray();
        else
            summaries = pseudoSummaries;

        return summaries.Where(s => !hiddenMediaHashSet.Contains(s.MediumId)).Select(ps =>
            {
                return ps.Medium.ToProfileMediaModel(ps.TotalSeconds);
            })
            .OrderByDescending(ms => ms.listenedSeconds)
            .ToArray();
    }
    
     private async Task<ProfileMediaModel[]> FetchProfileMedia(Guid userId, int? limit)
    {
        var hiddenMedia = await _dbContext.HiddenMedia.Where(hm => hm.UserId == userId).ToArrayAsync();
        var hiddenMediaHashSet = hiddenMedia.Select(hm => hm.MediumId).ToHashSet();

        var summariesQuery = _dbContext.PlaybackSummaries
            .Include(s => s.Medium).ThenInclude(m => m.Artists)
            .Include(ps => ps.Medium).ThenInclude(m => m.Images)
            .OrderByDescending(ps => ps.TotalSeconds)
            .Where(s => s.UserId == userId);

        PlaybackSummary[] summaries;
        if (limit.HasValue)
            summaries = await summariesQuery.Take(limit.Value).ToArrayAsync();
        else
            summaries = await summariesQuery.ToArrayAsync();

        return summaries.Where(s => !hiddenMediaHashSet.Contains(s.MediumId)).Select(ps =>
            {
                return ps.Medium.ToProfileMediaModel(ps.TotalSeconds);
            })
            .OrderByDescending(ms => ms.listenedSeconds)
            .ToArray();
    }

    private async Task<ProfileInformation> GetProfileInformationGuid(User dbUser, Guid? mainUser = null)
    {
        var userId = dbUser.Id;
        var earliestRecord = dbUser.PlaybackRecords.MinBy(r => r.PlayedAt);
        var profileInformation = new ProfileInformation
        {
            username = dbUser.Name,
            trackingSince = earliestRecord?.PlayedAt,
            profileImage = dbUser.SpotifyProfileUrl,
            totalListenedSeconds = dbUser.PlaybackRecords.Sum(pr => pr.PlayedSeconds),
            latestFetch = dbUser.Stats.LatestFetch,
            language = dbUser.Language
        };

        if (mainUser.HasValue)
        {
            var match = await _dbContext.MutualPlaybackOverviews
                .Include(m => m.User1)
                .Include(m => m.User2)
                .Include(m => m.MutualPlaybackEntries)
                .FirstAsync(m =>
                    (m.User1Id == userId && m.User2Id == mainUser) ||
                    (m.User2Id == userId && m.User1Id == mainUser));

            var totalListenedSecondsTogether =
                match.MutualPlaybackEntries.Sum(e => Math.Min(e.PlaybackSecondsUser1, e.PlaybackSecondsUser2));

            profileInformation.totalTogetherListenedSeconds = totalListenedSecondsTogether;
        }

        return profileInformation;
    }
    
    private DateTime LimitKeyToDate(string limitKey)
    {
        var now = DateTime.Now;
        switch (limitKey)
        {
            case "1W":
                return now.AddDays(-7);
            case "1M":
                return now.AddMonths(-1);
            case "1Y":
                return now.AddYears(-1);
        }

        throw new Exception("Limit key not parsable");
    }
}