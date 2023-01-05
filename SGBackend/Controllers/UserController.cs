using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace SGBackend.Controllers;

[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    private readonly SgDbContext _dbContext;
    
    public UserController(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Authorize]
    [HttpGet("profile-information")]
    public async Task<ProfileInformation> GetProfileInformation()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);

        var earliestRecord = dbUser.PlaybackRecords.MinBy(r => r.PlayedAt);
        return new ProfileInformation()
        {
            username = dbUser.Name,
            trackingSince =  earliestRecord?.PlayedAt,
            profileImage = dbUser.SpotifyProfileUrl
        };
    }

    [Authorize]
    [HttpGet("spotify/personal-summary")]
    public async Task<MediaSummary[]> GetPersonalSummary()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        
        var dbUser = await _dbContext.User
            .Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).ThenInclude(m => m.Artists)
            .Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).ThenInclude(m => m.Images)
            .FirstAsync(u => u.Id == userId);

        return dbUser.PlaybackSummaries.Select(ps => ps.ToMediaSummary()).OrderByDescending(ms => ms.listenedSeconds).ToArray();
    }

    [Authorize]
    [HttpGet("matches")]
    public async Task<Match[]> GetMatches()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.FirstAsync(u => u.Id == userId);

        var matches = await _dbContext.MutualPlaybackOverviews
            .Include(m => m.User1)
            .Include(m => m.User2)
            .Include(m => m.MutualPlaybackEntries)
            .Where(m => m.User1 == dbUser || m.User2 == dbUser).ToArrayAsync();
        
        return matches.GroupBy(m => m.GetOtherUser(dbUser)).Select(m => new Match()
        {
            username = m.Key.Name,
            userId = m.Key.Id.ToString(),
            profileUrl = m.Key.SpotifyProfileUrl,
            listenedTogetherSeconds = m.Sum(o => o.MutualPlaybackEntries.Sum(e => e.PlaybackSeconds))
        }).OrderByDescending(m => m.listenedTogetherSeconds).ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/recommended-media")]
    public async Task<MediaSummary[]> GetRecommendedMedia(string guid)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var guidRequested = Guid.Parse(guid);
        
        var loggedInUser = await _dbContext.User.Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).FirstAsync(u => u.Id == userId);
        var knownMedia = loggedInUser.PlaybackSummaries.Select(ps => ps.Medium).ToHashSet();
        
        var requestedUser =  await _dbContext.User.Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).FirstAsync(u => u.Id == guidRequested);
        
        return requestedUser.PlaybackSummaries.Where(ps => !knownMedia.Contains(ps.Medium))
            .Select(ps => ps.ToMediaSummary()).OrderByDescending(ms => ms.listenedSeconds).ToArray();
    }
}