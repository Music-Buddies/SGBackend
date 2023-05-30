using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Controllers;

[ApiController]
[Route("user")]
public class UserController : ControllerBase
{
    private SgDbContext _dbContext;

    public UserController(SgDbContext dbContext)
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
        return await _dbContext.GetProfileInformationGuid(dbUser, Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value));
    }
    
    [Authorize]
    [HttpGet("{guid}/profile-media")]
    public async Task<ProfileMediaModel[]> GetProfileMediaForOtherUser(string guid, int? limit,
        [FromQuery(Name = "limit-key")] string? limitKey)
    {
        if (limitKey != null)
        {
            return await _dbContext.FetchProfileMediaUntil(Guid.Parse(guid), HelperExtensions.LimitKeyToDate(limitKey), limit);
        }

        return await _dbContext.FetchProfileMedia(Guid.Parse(guid), limit);
    }
}