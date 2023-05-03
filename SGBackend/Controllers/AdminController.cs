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

    private readonly TransferService _transferService;

    public AdminController(ParalellAlgoService algoService, SgDbContext dbContext, UserService userService,
        ISchedulerFactory schedulerFactory, ISecretsProvider secretsProvider, TransferService transferService)
    {
        _algoService = algoService;
        _dbContext = dbContext;
        _userService = userService;
        _schedulerFactory = schedulerFactory;
        _secretsProvider = secretsProvider;
        _transferService = transferService;
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

        await _transferService.ImportUsers(exportContainer);
        
        return Ok();
    }

    [HttpPost("exportUsers")]
    public async Task<ActionResult<ExportContainer>> ExportUsers(string adminToken)
    {
        if (!AdminTokenValid(adminToken)) return Unauthorized();

        return await _transferService.ExportUsers();
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