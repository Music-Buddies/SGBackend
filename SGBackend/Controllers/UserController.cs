using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Helpers;
using SGBackend.Models;

namespace SGBackend.Controllers;

[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    private readonly SgDbContext _dbContext;

    private readonly SpotifyConnector _spotifyConnector;

    public UserController(SgDbContext dbContext, SpotifyConnector spotifyConnector)
    {
        _dbContext = dbContext;
        _spotifyConnector = spotifyConnector;
    }

    [Authorize]
    [HttpGet("spotify-token")]
    public async Task<Token?> GetSpotifyToken()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);
        var spotifyToken = await _spotifyConnector.GetAccessTokenUsingRefreshToken(dbUser);

        if (spotifyToken == null)
        {
            return null;
        }
        
        return new Token
        {
            spotifyToken = spotifyToken.access_token
        };
    }

    [Authorize]
    [HttpDelete("spotify-disconnect")]
    public async Task<IActionResult> DisconnectSpotify()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.FirstAsync(u => u.Id == userId);
        dbUser.SpotifyRefreshToken = null;
        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [Authorize]
    [HttpGet("profile-information/{guid}")]
    public async Task<ProfileInformation> GetProfileInformationForUser(string guid)
    {
        return await GetProfileInformationGuid(Guid.Parse(guid));
    }
    
    [Authorize]
    [HttpGet("profile-information")]
    public async Task<ProfileInformation> GetProfileInformation()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        return await GetProfileInformationGuid(userId);
    }

    private async Task<ProfileInformation> GetProfileInformationGuid(Guid userId)
    {
        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);

        var earliestRecord = dbUser.PlaybackRecords.MinBy(r => r.PlayedAt);
        return new ProfileInformation
        {
            username = dbUser.Name,
            trackingSince = earliestRecord?.PlayedAt,
            profileImage = dbUser.SpotifyProfileUrl,
            totalListenedSeconds = dbUser.PlaybackRecords.Sum(pr => pr.PlayedSeconds),
            latestFetch = dbUser.Stats.LatestFetch
        };
    }

    [Authorize]
    [HttpGet("spotify/personal-summary/{guid}")]
    public async Task<MediaSummary[]> GetPersonalSummaryOfOtherUser(string guid)
    {
        return await GetSummaryForGuid(Guid.Parse(guid));
    }
    
    [Authorize]
    [HttpGet("spotify/personal-summary")]
    public async Task<MediaSummary[]> GetPersonalSummary()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return await GetSummaryForGuid(userId);
    }

    private async Task<MediaSummary[]> GetSummaryForGuid(Guid userId)
    {
        var dbUser = await _dbContext.User
            .Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).ThenInclude(m => m.Artists)
            .Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).ThenInclude(m => m.Images)
            .FirstAsync(u => u.Id == userId);

        return dbUser.PlaybackSummaries.Select(ps => ps.ToMediaSummary()).OrderByDescending(ms => ms.listenedSeconds)
            .ToArray();
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

        return MatchesHelper.CreateMatchesArray(matches.GroupBy(m => m.GetOtherUser(dbUser)));
    }

    [Authorize]
    [HttpGet("matches/{guid}/together-consumed/tracks")]
    public async Task<MediaSummary[]> GetListenedTogetherTracks(string guid)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var guidRequested = Guid.Parse(guid);

        var loggedInUser = await _dbContext.User.Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium)
            .FirstAsync(u => u.Id == userId);
        
        var requestedUser = await _dbContext.User.Include(u => u.PlaybackSummaries).FirstAsync(u => u.Id == guidRequested);
        
        var match = await _dbContext.MutualPlaybackOverviews
            .Include(m => m.User1)
            .Include(m => m.User2)
            .Include(m => m.MutualPlaybackEntries)
            .ThenInclude(entry => entry.Medium)
            .ThenInclude(m => m.Artists)
            .Include(m => m.MutualPlaybackEntries)
            .ThenInclude(m => m.Medium)
            .ThenInclude(m => m.Images)
            .FirstAsync(m => (m.User1 == loggedInUser && m.User2 == requestedUser) || (m.User2 == loggedInUser && m.User1 == requestedUser));

        return match.MutualPlaybackEntries.Select(m => m.Medium.ToMediaSummary(m.PlaybackSeconds)).OrderByDescending(ms => ms.listenedSeconds).ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/recommended-media")]
    public async Task<MediaSummary[]> GetRecommendedMedia(string guid)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var guidRequested = Guid.Parse(guid);

        var loggedInUser = await _dbContext.User.Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium)
            .FirstAsync(u => u.Id == userId);
        var knownMedia = loggedInUser.PlaybackSummaries.Select(ps => ps.Medium).ToHashSet();

        var requestedUser = await _dbContext.User.Include(u => u.PlaybackSummaries)
            .ThenInclude(ps => ps.Medium)
            .ThenInclude(m => m.Artists)
            .Include(u => u.PlaybackSummaries)
            .ThenInclude(ps => ps.Medium)
            .ThenInclude(m => m.Images)
            .FirstAsync(u => u.Id == guidRequested);

        return requestedUser.PlaybackSummaries.Where(ps => !knownMedia.Contains(ps.Medium))
            .Select(ps => ps.ToMediaSummary()).OrderByDescending(ms => ms.listenedSeconds).ToArray();
    }
}