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
    [HttpPost("hide-media")]
    public async Task<IActionResult> PostHideMedia(HideMedia hideMedia)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.HiddenMedia).FirstAsync(u => u.Id == userId);
        
        if (Enum.TryParse(hideMedia.origin, true, out HiddenOrigin origin))
        {
            dbUser.HiddenMedia.Add(new HiddenMedia
            {
                HiddenMediumId = Guid.Parse(hideMedia.mediumId),
                HiddenOrigin = origin
            });
        }
        else
        {
            return BadRequest("could not parse origin");
        }

        await _dbContext.SaveChangesAsync();
        return Ok();
    }
    
    [Authorize]
    [HttpDelete("hide-media")]
    public async Task<IActionResult> DeleteHideMedia(HideMedia hideMedia)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.HiddenMedia).FirstAsync(u => u.Id == userId);
        
        if (Enum.TryParse(hideMedia.origin, true, out HiddenOrigin origin))
        {
            HiddenMedia? hideMediaDb = dbUser.HiddenMedia.FirstOrDefault(m =>
                m.HiddenMediumId == Guid.Parse(hideMedia.mediumId) && m.HiddenOrigin == origin);

            if (hideMediaDb != null)
            {
                dbUser.HiddenMedia.Remove(hideMediaDb);
            }
        }
        else
        {
            return BadRequest("could not parse origin");
        }

        await _dbContext.SaveChangesAsync();
        return Ok();
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
        if (dbUser.SpotifyRefreshToken == null) return null;
        var spotifyToken = await _spotifyConnector.GetAccessTokenUsingRefreshToken(dbUser.SpotifyRefreshToken);

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
    [HttpGet("valid")]
    public async Task<bool> TestTokenValid()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (dbUser == null) return false;

        return true;
    }

    [Authorize]
    [HttpGet("profile-information/{guid}")]
    public async Task<ProfileInformation> GetProfileInformationForUser(string guid)
    {
        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords)
            .FirstAsync(u => u.Id == Guid.Parse(guid));
        return await GetProfileInformationGuid(dbUser, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value));
    }

    [Authorize]
    [HttpGet("profile-information")]
    public async Task<IActionResult> GetProfileInformation()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (dbUser == null) return Unauthorized();

        return Ok(await GetProfileInformationGuid(dbUser));
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

    [Authorize]
    [HttpGet("spotify/personal-summary/{guid}")]
    public async Task<MediaSummary[]> GetPersonalSummaryOfOtherUser(string guid, int? limit)
    {
        return (await GetSummaryForGuid(Guid.Parse(guid), limit)).Where(s => !s.hidden).ToArray();
    }

    [Authorize]
    [HttpGet("spotify/personal-summary")]
    public async Task<MediaSummary[]> GetPersonalSummary(int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return (await GetSummaryForGuid(userId, limit)).Where(s => !s.hidden).ToArray();
    }
    
    [Authorize]
    [HttpGet("spotify/personal-summary/hidden")]
    public async Task<MediaSummary[]> GetPersonalSummaryHidden(int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        return (await GetSummaryForGuid(userId, limit)).Where(s => s.hidden).ToArray();
    }

    private async Task<MediaSummary[]> GetSummaryForGuid(Guid userId, int? limit)
    {
        var hiddenMedia = await _dbContext.HiddenMedia.Where(hm => hm.UserId == userId && hm.HiddenOrigin == HiddenOrigin.PersonalHistory).ToArrayAsync();
        var hiddenMediaHashSet = hiddenMedia.Select(hm => hm.HiddenMediumId).ToHashSet();
        
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

        return summaries.Select(ps => ps.Medium.ToRecommendedMedia(ps.TotalSeconds, hiddenMediaHashSet.Contains(ps.MediumId)))
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
            matches = await query.Take(limit.Value).ToArrayAsync();
        else
            matches = await query.ToArrayAsync();

        return MatchesHelper.CreateMatchesArray(matches.GroupBy(m => m.GetOtherUser(dbUser)));
    }

    [Authorize]
    [HttpGet("matches/recommended-media")]
    public async Task<IndependentRecommendation[]> GetIndependentRecommendedMedia(int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var loggedInUser = await _dbContext.User.Include(u => u.HiddenMedia).Include(u => u.PlaybackSummaries)
            .FirstAsync(u => u.Id == userId);

        var hiddenMediaSet = loggedInUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.Discover)
            .Select(hm => hm.HiddenMediumId).ToHashSet();
        
        var knownMedia = loggedInUser.PlaybackSummaries.Select(ps => ps.MediumId).ToHashSet();

        var overviews = await _dbContext.MutualPlaybackOverviews
            .Include(o => o.MutualPlaybackEntries)
            .Include(o => o.User1)
            .Include(o => o.User2)
            //.Include(o => o.User1).ThenInclude(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).ThenInclude(m => m.Images)
            //.Include(o => o.User2).ThenInclude(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium).ThenInclude(m => m.Images)
            .Where(o => (o.User1Id == userId || o.User2Id == userId) && o.MutualPlaybackEntries.Any()).ToArrayAsync();

        var users = overviews.Select(o => o.GetOtherUser(loggedInUser).Id).ToHashSet();

        var userSummaries = await _dbContext.PlaybackSummaries.Where(ps => users.Contains(ps.UserId))
            .Include(ps => ps.Medium).ThenInclude(m => m.Artists)
            .Include(ps => ps.Medium).ThenInclude(m => m.Images).ToArrayAsync();

        var userSummariesGrouping = userSummaries.GroupBy(us => us.UserId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var recommendations = new List<IndependentRecommendation>();

        foreach (var overview in overviews)
        {
            var otherUser = overview.GetOtherUser(loggedInUser);
            var listenedTogetherSeconds =
                overview.MutualPlaybackEntries.Sum(e => Math.Min(e.PlaybackSecondsUser1, e.PlaybackSecondsUser2));

            foreach (var unknownSummary in userSummariesGrouping[otherUser.Id]
                         .Where(ps => !knownMedia.Contains(ps.MediumId)))

                // only return non hidden
                if (!hiddenMediaSet.Contains(unknownSummary.MediumId))
                {
                    recommendations.Add(new IndependentRecommendation
                    {
                        orderValue = listenedTogetherSeconds * unknownSummary.TotalSeconds,
                        listenedSecondsMatch = unknownSummary.TotalSeconds,
                        albumImages = unknownSummary.Medium.GetMediumImages(),
                        albumName = unknownSummary.Medium.AlbumName,
                        explicitFlag = unknownSummary.Medium.ExplicitContent,
                        profileUrl = otherUser.SpotifyProfileUrl,
                        songTitle = unknownSummary.Medium.Title,
                        username = otherUser.Name,
                        linkToMedia =  $"spotify:track:{unknownSummary.Medium.LinkToMedium.Split("/").Last()}",
                        allArtists = unknownSummary.Medium.Artists.Select(a => a.Name).ToArray(),
                        hidden = hiddenMediaSet.Contains(unknownSummary.MediumId),
                        mediumId = unknownSummary.MediumId.ToString()
                    });
                }
               
        }

        if (limit.HasValue)
        {
            // get number of hidden in limit range 
            var numberHiddenInRange = recommendations.OrderByDescending(r => r.orderValue).Take(limit.Value)
                .Count(rec => rec.hidden);
            
            // always return limit + amount of hidden tracks
            return recommendations.OrderByDescending(r => r.orderValue).Take(limit.Value + numberHiddenInRange).ToArray();
        }

        return recommendations.OrderByDescending(r => r.orderValue).ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/together-consumed/tracks")]
    public async Task<MediaSummary[]> GetListenedTogetherTracks(string guid, int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var guidRequested = Guid.Parse(guid);
        
        var loggedInUser = await _dbContext.User.Include(u => u.HiddenMedia).Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Medium)
            .FirstAsync(u => u.Id == userId);
        
        var requestedUser =
            await _dbContext.User.Include(u => u.PlaybackSummaries).Include(u => u.HiddenMedia).FirstAsync(u => u.Id == guidRequested);

        // collect hidden media, whether user or match hid it, it should not be displayed
        var hiddenMediaSet = new HashSet<Guid>();
        foreach (var media in loggedInUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.PersonalHistory))
        {
            hiddenMediaSet.Add(media.HiddenMediumId);
        }
        foreach (var media in requestedUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.PersonalHistory))
        {
            hiddenMediaSet.Add(media.HiddenMediumId);
        }

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

        var tracks = match.MutualPlaybackEntries.Select(m =>
        {
            long listenedSecondsMatch;
            long listenedSecondsYou;
            // determine listened seconds of the other user, useful in case the other user listened more to the song than yourself
            if (match.User1 == loggedInUser)
            {
                listenedSecondsMatch = m.PlaybackSecondsUser2;
                listenedSecondsYou = m.PlaybackSecondsUser1;
            }
            else
            {
                listenedSecondsMatch = m.PlaybackSecondsUser1;
                listenedSecondsYou = m.PlaybackSecondsUser2;
            }

            return m.Medium.ToTogetherConsumedTrack(listenedSecondsMatch, listenedSecondsYou, hiddenMediaSet.Contains(m.MediumId));
        }).Where(s => !s.hidden); // don't return hidden

        if (limit.HasValue)
            // all mutual playback results, 
            return tracks.OrderByDescending(ms => Math.Min(ms.listenedSecondsYou.Value, ms.listenedSecondsMatch.Value))
                .Take(limit.Value).ToArray();

        return tracks.OrderByDescending(ms => Math.Min(ms.listenedSecondsYou.Value, ms.listenedSecondsMatch.Value))
            .ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/recommended-media")]
    public async Task<MediaSummary[]> GetRecommendedMedia(string guid, int? limit)
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
            .Include(u => u.HiddenMedia)
            .FirstAsync(u => u.Id == guidRequested);
        
        // add hide flag from personal hidden media
        var hiddenMediaHashSet = requestedUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.PersonalHistory).Select(hm => hm.HiddenMediumId).ToHashSet();
        
        var summaries = requestedUser.PlaybackSummaries.Where(ps => !knownMedia.Contains(ps.Medium))
            .Select(ps => ps.Medium.ToRecommendedMedia(ps.TotalSeconds, hiddenMediaHashSet.Contains(ps.MediumId)));

        if (limit.HasValue)
            return summaries.OrderByDescending(ms => ms.listenedSeconds)
                .Take(limit.Value).ToArray();

        return summaries.OrderByDescending(ms => ms.listenedSeconds).ToArray();
    }
}