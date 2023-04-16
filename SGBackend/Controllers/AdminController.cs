using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Utilities;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Service;

namespace SGBackend.Controllers;

[ApiController]
[Route("admin")]
public class AdminController  : ControllerBase
{
    private readonly ParalellAlgoService _algoService;

    private readonly SgDbContext _dbContext;

    private readonly UserService _userService;

    public AdminController(ParalellAlgoService algoService, SgDbContext dbContext, UserService userService)
    {
        _algoService = algoService;
        _dbContext = dbContext;
        _userService = userService;
    }

    [Authorize]
    [HttpPost("updateAll")]
    public async Task<IActionResult> UpdateAll()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);

        var allowedUsers = new List<string>() { "https://open.spotify.com/user/31ahh7pd3xdhtwipis3fprv3vp24", "https://open.spotify.com/user/4wfpnlvgdiwp1jfde3a80n9ml"};
        if (!allowedUsers.Contains(dbUser.SpotifyId))
        {
            return Unauthorized();
        }
        await _algoService.UpdateAll();
        return Ok();
    }

    //[Authorize]
    [HttpPost("importUsers")]
    public async Task<IActionResult> ImportUsers(ExportContainer exportContainer)
    {
        /*
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);

        var allowedUsers = new List<string>() { "https://open.spotify.com/user/31ahh7pd3xdhtwipis3fprv3vp24", "https://open.spotify.com/user/4wfpnlvgdiwp1jfde3a80n9ml"};
        if (!allowedUsers.Contains(dbUser.SpotifyId))
        {
            return Unauthorized();
        }
        */
        
        // import missing media
        var existingMediaUrls = await _dbContext.Media.Select(m => m.LinkToMedium).ToArrayAsync();

        var exportMediaToImport =
            exportContainer.media.Where(m => !existingMediaUrls.Contains(m.LinkToMedium)).Select(m => m.ToMedium()).ToList();
        
        await _dbContext.Media.AddRangeAsync(exportMediaToImport);
        await _dbContext.SaveChangesAsync();
        
        // get link map for other exports
        var mediaLinkMap = (await _dbContext.Media.ToArrayAsync()).ToDictionary(m => m.LinkToMedium, m => m.Id);

        var existingUserSpotifyIds = await _dbContext.User.Select(u => u.SpotifyId).ToArrayAsync();

        var exportUsersToImport = exportContainer.users.Where(u => !existingUserSpotifyIds.Contains(u.SpotifyId)).ToList();

        foreach (var user in exportUsersToImport.Select(u => u.ToUser(mediaLinkMap)))
        {
            await _userService.AddUser(user);
        }
        
        await _dbContext.User.AddRangeAsync();
        await _dbContext.SaveChangesAsync();

        // recalc 
        await _algoService.UpdateAll();
        
        return Ok();
    }

    [Authorize]
    [HttpGet("exportUsers")]
    public async Task<ActionResult<ExportContainer>> ExportUsers()
    {
        var userId = Guid.Parse(User.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        var dbUser = await _dbContext.User.Include(u => u.PlaybackRecords).FirstAsync(u => u.Id == userId);

        var allowedUsers = new List<string>() { "https://open.spotify.com/user/31ahh7pd3xdhtwipis3fprv3vp24", "https://open.spotify.com/user/4wfpnlvgdiwp1jfde3a80n9ml"};
        if (!allowedUsers.Contains(dbUser.SpotifyId))
        {
            return Unauthorized();
        }
        
        // export all media (needed for import later)
        var media = await _dbContext.Media.Include(m => m.Artists).Include(m => m.Images).ToArrayAsync();
        
        // TODO: remove startswith dummy and and isDummy flag to user table, after migrations are implemented
        // export all users and their records (the rest will be recalculated)
        var users = await _dbContext.User.Where(u => !u.Name.StartsWith("Dummy")).Include(u => u.PlaybackRecords).ThenInclude(pr => pr.Medium).ToArrayAsync();

        return new ExportContainer
        {
            media = media.Select(m => m.ToExportMedium()).ToList(),
            users = users.Select(u => u.ToExportUser()).ToList()
        };
    }
}

public class ExportContainer
{
    public List<ExportUser> users { get; set; }
    
    public List<ExportMedium> media { get; set; }
}