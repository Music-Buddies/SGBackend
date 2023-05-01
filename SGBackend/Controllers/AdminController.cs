using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SecretsProvider;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Service;

namespace SGBackend.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly ParalellAlgoService _algoService;

    private readonly SgDbContext _dbContext;

    private readonly ISchedulerFactory _schedulerFactory;

    private readonly ISecretsProvider _secretsProvider;

    private readonly UserService _userService;

    public AdminController(ParalellAlgoService algoService, SgDbContext dbContext, UserService userService,
        ISchedulerFactory schedulerFactory, ISecretsProvider secretsProvider)
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
    
    [HttpGet("stats")]
    public async Task<Stats> GetStats()
    {
        var summaries = await _dbContext.PlaybackSummaries.ToArrayAsync();
        var users = await _dbContext.User.ToArrayAsync();

        return new Stats
        {
            Users = users.Length,
            UserMinutes = summaries.Sum(s => s.TotalSeconds) / 60
        };
    }

    [HttpPost("importUsers")]
    public async Task<IActionResult> ImportUsers(ExportContainer exportContainer)
    {
        if (!AdminTokenValid(exportContainer.adminToken)) return Unauthorized();

        // import missing media, requirement of the most basic record entity type
        var existingMediaUrls = await _dbContext.Media.Select(m => m.LinkToMedium).ToArrayAsync();

        var exportMediaToImport =
            exportContainer.media.Where(m => !existingMediaUrls.Contains(m.LinkToMedium)).Select(m => m.ToMedium())
                .ToList();

        await _dbContext.Media.AddRangeAsync(exportMediaToImport);
        await _dbContext.SaveChangesAsync();

        // get link map for other exports
        var mediaLinkMap = (await _dbContext.Media.ToArrayAsync()).ToDictionary(m => m.LinkToMedium, m => m.Id);

        // filter out users that already exist (no need to import them again
        var existingUserSpotifyIds = await _dbContext.User.Select(u => u.SpotifyId).ToArrayAsync();
        var exportUsersToImport =
            exportContainer.users.Where(u => !existingUserSpotifyIds.Contains(u.SpotifyId)).ToList();


        // import the users with all their records
        var dbUsers = exportUsersToImport.Select(u => u.ToUser(mediaLinkMap)).ToArray();
        foreach (var user in dbUsers) await _userService.AddUser(user);

        await _dbContext.User.AddRangeAsync();
        await _dbContext.SaveChangesAsync();

        // calculate everything for the imported users
        foreach (var dbUser in dbUsers) await _algoService.ProcessImport(dbUser.Id);
        
        return Ok();
    }

    [HttpPost("exportUsers")]
    public async Task<ActionResult<ExportContainer>> ExportUsers(string adminToken)
    {
        if (!AdminTokenValid(adminToken)) return Unauthorized();

        // export all media (needed for import later)
        var media = await _dbContext.Media.Include(m => m.Artists).Include(m => m.Images).ToArrayAsync();

        // TODO: remove startswith dummy and and isDummy flag to user table, after migrations are implemented
        // export all users and their records (the rest will be recalculated)
        var users = await _dbContext.User.Where(u => !u.Name.StartsWith("Dummy")).Include(u => u.PlaybackRecords)
            .ThenInclude(pr => pr.Medium).ToArrayAsync();

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

public class Stats
{
    public long UserMinutes { get; set; }
    
    public long Users { get; set; }
}