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
    [HttpPatch("settings")]
    public async Task<IActionResult> PatchSettings(UserSettings settings)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);

        if (settings.language != null)
        {
            Enum.TryParse(settings.language, out Language lang);
            dbUser.Language = lang;
        }

        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    [Authorize]
    [HttpGet("spotify-token")]
    public async Task<Token?> GetSpotifyToken()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);
        var spotifyToken = await _spotifyConnector.GetAccessTokenUsingRefreshToken(dbUser);

        if (spotifyToken == null) return null;

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
        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords)
            .FirstAsync(u => u.Id == userId);

        var earliestRecord = dbUser.PlaybackRecords.MinBy(r => r.PlayedAt);
        return new ProfileInformation
        {
            username = dbUser.Name,
            trackingSince = earliestRecord?.PlayedAt,
            profileImage = dbUser.SpotifyProfileUrl,
            totalListenedSeconds = dbUser.PlaybackRecords.Sum(pr => pr.PlayedSeconds),
            latestFetch = dbUser.Stats.LatestFetch,
            language = dbUser.Language
        };
    }

    [Authorize]
    [HttpGet("spotify/personal-summary/{guid}")]
    public async Task<RecommendedMedia[]> GetPersonalSummaryOfOtherUser(string guid, int? limit)
    {
        return await GetSummaryForGuid(Guid.Parse(guid), limit);
    }

    [Authorize]
    [HttpGet("spotify/personal-summary")]
    public async Task<RecommendedMedia[]> GetPersonalSummary(int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return await GetSummaryForGuid(userId, limit);
    }

    private async Task<RecommendedMedia[]> GetSummaryForGuid(Guid userId, int? limit)
    {
        var summariesQuery = _dbContext.PlaybackSummaries
            .Include(s => s.Medium).ThenInclude(m => m.Artists)
            .Include(ps => ps.Medium).ThenInclude(m => m.Images)
            .OrderByDescending(ps => ps.TotalSeconds)
            .Where(s => s.UserId == userId);

        PlaybackSummary[] summaries;
        if (limit.HasValue)
        {
            summaries = await summariesQuery.Take(limit.Value).ToArrayAsync();
        }
        else
        {
            summaries = await summariesQuery.ToArrayAsync();
        }
        
        return summaries.Select(ps => ps.Medium.ToRecommendedMedia(ps.TotalSeconds))
            .OrderByDescending(ms => ms.listenedSeconds)
            .ToArray();
    }

    [Authorize]
    [HttpGet("matches")]
    public async Task<Match[]> GetMatches(int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.FirstAsync(u => u.Id == userId);

        var query = _dbContext.MutualPlaybackOverviews
            .Include(m => m.User1)
            .Include(m => m.User2)
            .Include(m => m.MutualPlaybackEntries)
            .OrderByDescending(m =>
                m.MutualPlaybackEntries.Sum(e => Math.Min(e.PlaybackSecondsUser1, e.PlaybackSecondsUser2)))
            .Where(m => m.User1 == dbUser || m.User2 == dbUser);
        
        MutualPlaybackOverview[] matches;

        if (limit.HasValue)
        {
            matches = await query.Take(limit.Value).ToArrayAsync();
        }
        else
        {
            matches = await query.ToArrayAsync();
        }

        return MatchesHelper.CreateMatchesArray(matches.GroupBy(m => m.GetOtherUser(dbUser)));
    }

    [Authorize]
    [HttpGet("matches/{guid}/together-consumed/tracks")]
    public async Task<TogetherConsumedTrack[]> GetListenedTogetherTracks(string guid)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var guidRequested = Guid.Parse(guid);

        var loggedInUser = await _dbContext.User.Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium)
            .FirstAsync(u => u.Id == userId);

        var requestedUser =
            await _dbContext.User.Include(u => u.PlaybackSummaries).FirstAsync(u => u.Id == guidRequested);

        var match = await _dbContext.MutualPlaybackOverviews
            .Include(m => m.User1)
            .Include(m => m.User2)
            .Include(m => m.MutualPlaybackEntries)
            .ThenInclude(entry => entry.Medium)
            .ThenInclude(m => m.Artists)
            .Include(m => m.MutualPlaybackEntries)
            .ThenInclude(m => m.Medium)
            .ThenInclude(m => m.Images)
            .FirstAsync(m =>
                (m.User1 == loggedInUser && m.User2 == requestedUser) ||
                (m.User2 == loggedInUser && m.User1 == requestedUser));

        // all mutual playback results, 
        return match.MutualPlaybackEntries.Select(m =>
        {
            long listenedSecondsMatch;
            // determine listened seconds of the other user, useful in case the other user listened more to the song than yourself
            if (match.User1 == loggedInUser)
                listenedSecondsMatch = m.PlaybackSecondsUser2;
            else
                listenedSecondsMatch = m.PlaybackSecondsUser1;
            return m.Medium.ToTogetherConsumedTrack(listenedSecondsMatch,
                Math.Min(m.PlaybackSecondsUser1, m.PlaybackSecondsUser2));
        }).OrderByDescending(ms => ms.listenedSecondsTogether).ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/recommended-media")]
    public async Task<RecommendedMedia[]> GetRecommendedMedia(string guid)
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
            .Select(ps => ps.Medium.ToRecommendedMedia(ps.TotalSeconds)).OrderByDescending(ms => ms.listenedSeconds)
            .ToArray();
    }
}