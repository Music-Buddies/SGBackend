using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Controllers;

[ApiController]
[Route("you")]
public class YouController : ControllerBase
{
    private readonly SgDbContext _dbContext;

    public YouController(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }
    
    /// <summary>
    /// Returns the users that the caller follows
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet("following")]
    public async Task<ModelUser[]> GetFollowedUsers()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var usersBeingFollowed = await _dbContext.Follower.Include(f => f.UserBeingFollowed)
            .Where(u => u.UserFollowingId == userId).ToArrayAsync();

        return usersBeingFollowed.Select(u => u.UserBeingFollowed.ToModelUser()).ToArray();
    }
    
    /// <summary>
    /// Returns the users that follow the caller
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet("followers")]
    public async Task<ModelUser[]> GetFollowingUsers()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var usersBeingFollowed = await _dbContext.Follower.Include(f => f.UserFollowing)
            .Where(u => u.UserBeingFollowedId == userId).ToArrayAsync();

        return usersBeingFollowed.Select(u => u.UserFollowing.ToModelUser()).ToArray();
    }

    /// <summary>
    /// This endpoint enables users to hide certain media from being displayed in their profile view and for other matches.
    /// </summary>
    /// <param name="hideMedia"></param>
    /// <returns></returns>
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
                MediumId = Guid.Parse(hideMedia.mediumId),
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
                m.MediumId == Guid.Parse(hideMedia.mediumId) && m.HiddenOrigin == origin);

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
    [HttpDelete("spotify-disconnect")]
    public async Task<IActionResult> DisconnectSpotify()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.FirstAsync(u => u.Id == userId);
        dbUser.SpotifyRefreshToken = null;
        await _dbContext.SaveChangesAsync();
        return Ok();
    }

    /// <summary>
    /// When migrating the database there sometimes are valid JWTs in circulation that contain user ids that no longer exists.
    /// To combat this the frontend calls this endpoint once initially, redirecting to login if invalid.
    /// </summary>
    /// <returns></returns>
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
    [HttpGet("profile-information")]
    public async Task<IActionResult> GetProfileInformation()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var dbUser = await _dbContext.User.Include(u => u.Stats).Include(u => u.PlaybackRecords)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (dbUser == null) return Unauthorized();

        return Ok(await _dbContext.GetProfileInformationGuid(dbUser));
    }
    
    [Authorize]
    [HttpGet("spotify/profile-media")]
    public async Task<ProfileMediaModel[]> GetProfileMedia(int? limit,
        [FromQuery(Name = "limit-key")] string? limitKey)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        if (limitKey != null)
        {
            return await _dbContext.FetchProfileMediaUntil(userId, HelperExtensions.LimitKeyToDate(limitKey), limit);
        }

        return (await _dbContext.FetchProfileMedia(userId, limit)).ToArray();
    }

    /// <summary>
    /// Returns all the media the user chose to hide (in different views).
    /// </summary>
    /// <returns></returns>
    [Authorize]
    [HttpGet("spotify/hidden-media")]
    public async Task<HiddenMediaModel[]> GetHiddenMedia()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var hiddenMedia = await _dbContext.HiddenMedia
            .Include(hm => hm.Medium).ThenInclude(m => m.Images)
            .Include(hm => hm.Medium).ThenInclude(m => m.Artists)
            .Where(hm => hm.UserId == userId).ToArrayAsync();

        return hiddenMedia.Select(hm =>
        {
            var mediaModel = new HiddenMediaModel
            {
                hiddenOrigin = hm.HiddenOrigin.ToString()
            };
            hm.Medium.SetMediaModel(mediaModel);
            return mediaModel;
        }).ToArray();
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

        return CreateMatchesArray(matches.GroupBy(m => m.GetOtherUser(dbUser)));
    }

    [Authorize]
    [HttpGet("matches/discover-media")]
    public async Task<DiscoverMediaModel[]> GetDiscoverMedia(int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);

        var loggedInUser = await _dbContext.User.Include(u => u.HiddenMedia).Include(u => u.PlaybackSummaries)
            .FirstAsync(u => u.Id == userId);

        var hiddenMediaSet = loggedInUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.Discover)
            .Select(hm => hm.MediumId).ToHashSet();

        var knownMedia = loggedInUser.PlaybackSummaries.Select(ps => ps.MediumId).ToHashSet();

        var overviews = await _dbContext.MutualPlaybackOverviews
            .Include(o => o.MutualPlaybackEntries)
            .Include(o => o.User1)
            .Include(o => o.User2)
            .Where(o => (o.User1Id == userId || o.User2Id == userId) && o.MutualPlaybackEntries.Any()).ToArrayAsync();

        var users = overviews.Select(o => o.GetOtherUser(loggedInUser).Id).ToHashSet();

        var userSummaries = await _dbContext.PlaybackSummaries.Where(ps => users.Contains(ps.UserId))
            .Include(ps => ps.Medium).ThenInclude(m => m.Artists)
            .Include(ps => ps.Medium).ThenInclude(m => m.Images).ToArrayAsync();

        var userSummariesGrouping = userSummaries.GroupBy(us => us.UserId)
            .ToDictionary(g => g.Key, g => g.ToArray());

        var recommendations = new List<DiscoverMediaModel>();


        foreach (var overview in overviews)
        {
            var otherUser = overview.GetOtherUser(loggedInUser);
            var listenedTogetherSeconds =
                overview.MutualPlaybackEntries.Sum(e => Math.Min(e.PlaybackSecondsUser1, e.PlaybackSecondsUser2));

            foreach (var unknownSummary in userSummariesGrouping[otherUser.Id]
                         .Where(ps => !knownMedia.Contains(ps.MediumId)))
            {
                var dmm = new DiscoverMediaModel
                {
                    username = otherUser.Name,
                    profileUrl = otherUser.SpotifyProfileUrl,
                    orderValue = listenedTogetherSeconds * unknownSummary.TotalSeconds,
                    listenedSeconds = unknownSummary.TotalSeconds,
                    hidden = hiddenMediaSet.Contains(unknownSummary.MediumId),
                };
                unknownSummary.Medium.SetMediaModel(dmm);
                recommendations.Add(dmm);
            }
        }

        if (limit.HasValue)
        {
            // get number of hidden in limit range 
            var numberHiddenInRange = recommendations.OrderByDescending(r => r.orderValue).Take(limit.Value)
                .Count(rec => rec.hidden);

            // always return limit + amount of hidden tracks
            return recommendations.Where(r => !r.hidden).OrderByDescending(r => r.orderValue)
                .Take(limit.Value + numberHiddenInRange).ToArray();
        }

        return recommendations.Where(r => !r.hidden).OrderByDescending(r => r.orderValue).ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/together-media")]
    public async Task<TogetherMediaModel[]> GetTogetherMedia(string guid, int? limit)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var guidRequested = Guid.Parse(guid);

        var loggedInUser = await _dbContext.User.Include(u => u.HiddenMedia).Include(u => u.PlaybackSummaries)
            .ThenInclude(ps => ps.Medium)
            .FirstAsync(u => u.Id == userId);

        var requestedUser =
            await _dbContext.User.Include(u => u.PlaybackSummaries).Include(u => u.HiddenMedia)
                .FirstAsync(u => u.Id == guidRequested);

        // collect hidden media, whether user or match hid it, it should not be displayed
        var hiddenMediaSet = new HashSet<Guid>();
        foreach (var media in loggedInUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.PersonalHistory))
        {
            hiddenMediaSet.Add(media.MediumId);
        }

        foreach (var media in requestedUser.HiddenMedia.Where(hm => hm.HiddenOrigin == HiddenOrigin.PersonalHistory))
        {
            hiddenMediaSet.Add(media.MediumId);
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

            return m.Medium.ToTogetherMedia(listenedSecondsMatch, listenedSecondsYou,
                hiddenMediaSet.Contains(m.MediumId));
        }).Where(s => !s.hidden); // don't return hidden

        if (limit.HasValue)
            // all mutual playback results, 
            return tracks.OrderByDescending(ms => Math.Min(ms.listenedSeconds, ms.listenedSecondsMatch))
                .Take(limit.Value).ToArray();

        return tracks.OrderByDescending(ms => Math.Min(ms.listenedSeconds, ms.listenedSecondsMatch))
            .ToArray();
    }

    [Authorize]
    [HttpGet("matches/{guid}/recommended-media")]
    public async Task<ProfileMediaModel[]> GetRecommendedMedia(string guid, int? limit)
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

        var hiddenMediaSet = requestedUser.HiddenMedia.Select(hm => hm.MediumId).ToHashSet();

        var summaries = requestedUser.PlaybackSummaries
            .Where(ps => !knownMedia.Contains(ps.Medium) && !hiddenMediaSet.Contains(ps.MediumId))
            .Select(ps => { return ps.Medium.ToProfileMediaModel(ps.TotalSeconds); });

        if (limit.HasValue)
            return summaries.OrderByDescending(ms => ms.listenedSeconds)
                .Take(limit.Value).ToArray();

        return summaries.OrderByDescending(ms => ms.listenedSeconds).ToArray();
    }
    
    private static Match[] CreateMatchesArray(IEnumerable<IGrouping<User, MutualPlaybackOverview>> matches)
    {
        if (!matches.Any()) return Array.Empty<Match>();

        var matchesArray = matches.Select(m => new Match
        {
            username = m.Key.Name,
            userId = m.Key.Id.ToString(),
            profileImage = m.Key.SpotifyProfileUrl,
            listenedTogetherSeconds = m.Sum(o =>
                o.MutualPlaybackEntries.Sum(e => Math.Min(e.PlaybackSecondsUser1, e.PlaybackSecondsUser2)))
        }).OrderByDescending(m => m.listenedTogetherSeconds).Where(m => m.listenedTogetherSeconds != 0).ToArray();

        if (matchesArray.Length == 0) return matchesArray;

        for (var i = 0; i < matchesArray.Length; i++) matchesArray[i].rank = i + 1;
        return matchesArray;
    }
}