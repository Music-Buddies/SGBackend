using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SGBackend.Entities;
using SGBackend.Models;

namespace SGBackend.Controllers;


[ApiController]
[Route("follower")]
public class FollowerController : ControllerBase
{
    private readonly SgDbContext _dbContext;

    public FollowerController(SgDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [Authorize]
    [HttpGet("users-that-follow/{guid}")]
    public async Task<ModelUser[]> UsersThatFollowGuid(string guid)
    {
        var userId = Guid.Parse(guid);
        var usersBeingFollowed = await _dbContext.Follower.Include(f => f.UserFollowing)
            .Where(u => u.UserBeingFollowedId == userId).ToArrayAsync();

        return usersBeingFollowed.Select(u => u.UserFollowing.ToModelUser()).ToArray();
    }

    [Authorize]
    [HttpGet("users-followed-by/{guid}")]
    public async Task<ModelUser[]> UsersFollowedByGuid(string guid)
    {
        var userId = Guid.Parse(guid);
        var usersBeingFollowed = await _dbContext.Follower.Include(f => f.UserBeingFollowed)
            .Where(u => u.UserFollowingId == userId).ToArrayAsync();

        return usersBeingFollowed.Select(u => u.UserBeingFollowed.ToModelUser()).ToArray();
    }

    [Authorize]
    [HttpGet("users-that-follow-you")]
    public async Task<ModelUser[]> GetFollowYou()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var usersBeingFollowed = await _dbContext.Follower.Include(f => f.UserFollowing)
            .Where(u => u.UserBeingFollowedId == userId).ToArrayAsync();

        return usersBeingFollowed.Select(u => u.UserFollowing.ToModelUser()).ToArray();
    }
    
    [Authorize]
    [HttpGet("users-that-you-follow")]
    public async Task<ModelUser[]> GetYouFollow()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var usersBeingFollowed = await _dbContext.Follower.Include(f => f.UserBeingFollowed)
            .Where(u => u.UserFollowingId == userId).ToArrayAsync();

        return usersBeingFollowed.Select(u => u.UserBeingFollowed.ToModelUser()).ToArray();
    }

    [Authorize]
    [HttpDelete("follow/{guid}")]
    public async Task<IActionResult> UnfollowUser(string guid)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var userFollowToDelete = Guid.Parse(guid);

        if (!(await _dbContext.User.AnyAsync(u => u.Id == userFollowToDelete))) return NotFound("Specified user id not found");

        var follower = await _dbContext.Follower.FirstOrDefaultAsync(f =>
            f.UserFollowingId == userId && f.UserBeingFollowedId == userFollowToDelete);

        if (follower != null)
        {
            _dbContext.Follower.Remove(follower);
            await _dbContext.SaveChangesAsync();
        }
        
        return Ok();
    }
    
    [Authorize]
    [HttpPost("follow/{guid}")]
    public async Task<IActionResult> FollowUser(string guid)
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var userToFollow = Guid.Parse(guid);

        if (!(await _dbContext.User.AnyAsync(u => u.Id == userToFollow))) return NotFound("Specified user id not found");
        
        await _dbContext.Follower.AddAsync(new Follower
        {
            UserFollowingId = userId,
            UserBeingFollowedId = userToFollow
        });
        await _dbContext.SaveChangesAsync();

        return Ok();
    }
}