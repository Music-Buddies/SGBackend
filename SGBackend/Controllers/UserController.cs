using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGBackend.Connector;
using SGBackend.Models;

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
            .Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Media).ThenInclude(m => m.Artists)
            .Include(u => u.PlaybackSummaries).ThenInclude(ps => ps.Media).ThenInclude(m => m.Images)
            .FirstAsync(u => u.Id == userId);

        return dbUser.PlaybackSummaries.Select(ps => new MediaSummary()
        {
            albumImages = ps.Media.Images.ToArray(),
            allArtists = ps.Media.Artists.Select(a => a.Name).ToArray(),
            explicitFlag = ps.Media.ExplicitContent,
            listenedSeconds = ps.TotalSeconds,
            songTitle = ps.Media.Title,
            linkToMedia = ps.Media.LinkToMedia
        }).ToArray();
    }
}