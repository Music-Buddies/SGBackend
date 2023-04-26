using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Utilities;
using Quartz;
using SecretsProvider;
using SGBackend.Connector.Spotify;
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
    
    private readonly ISchedulerFactory _schedulerFactory;

    private readonly ISecretsProvider _secretsProvider;

    public AdminController(ParalellAlgoService algoService, SgDbContext dbContext, UserService userService, ISchedulerFactory schedulerFactory, ISecretsProvider secretsProvider)
    {
        _algoService = algoService;
        _dbContext = dbContext;
        _userService = userService;
        _schedulerFactory = schedulerFactory;
        _secretsProvider = secretsProvider;
    }

    private bool AdminTokenValid(string adminToken)
    {
        return _secretsProvider.GetSecret<Secrets>().AdminToken == adminToken;
    }
    
    [HttpPost("importUsers")]
    public async Task<IActionResult> ImportUsers(ExportContainer exportContainer)
    {
        if (!AdminTokenValid(exportContainer.adminToken))
        {
            return Unauthorized();
        }
        
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

        var dbUsers = exportUsersToImport.Select(u => u.ToUser(mediaLinkMap)).ToArray();
        
        foreach (var user in dbUsers)
        {
            await _userService.AddUser(user);
        }
        
        await _dbContext.User.AddRangeAsync();
        await _dbContext.SaveChangesAsync();

        foreach (var dbUser in dbUsers)
        {
            await _algoService.ProcessImport(dbUser.Id);
        }
        
        // trigger fetch job once, to set last fetched timestamp for users
        var job = JobBuilder.Create<SpotifyGroupedFetchJob>()
            .Build();
                
        var trigger = TriggerBuilder.Create()
            .StartNow()
            .Build();

        var scheduler = await _schedulerFactory.GetScheduler();
        await scheduler.ScheduleJob(job, trigger);
        
        return Ok();
    }
    
    [HttpPost("exportUsers")]
    public async Task<ActionResult<ExportContainer>> ExportUsers(string adminToken)
    {
        if (!AdminTokenValid(adminToken))
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
    public string adminToken { get; set; }
    public List<ExportUser> users { get; set; }
    
    public List<ExportMedium> media { get; set; }
}