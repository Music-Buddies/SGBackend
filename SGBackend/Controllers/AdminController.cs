using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SecretsProvider;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Provider;
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

    private readonly JwtProvider _jwtProvider;
    
    public AdminController(ParalellAlgoService algoService, SgDbContext dbContext, UserService userService,
        ISchedulerFactory schedulerFactory, ISecretsProvider secretsProvider, TransferService transferService, JwtProvider jwtProvider)
    {
        _algoService = algoService;
        _dbContext = dbContext;
        _userService = userService;
        _schedulerFactory = schedulerFactory;
        _secretsProvider = secretsProvider;
        _transferService = transferService;
        _jwtProvider = jwtProvider;
    }

    private bool AdminTokenValid(string adminToken)
    {
        var secrets = _secretsProvider.GetSecret<Secrets>();
        return secrets.AdminToken == adminToken;
    }

    [HttpGet("fetch-and-calc-all-users/{adminPassword}")]
    public async Task<IActionResult> FetchAndCalcUsers()
    {
        await _algoService.FetchAndCalcUsers();
        return Ok();
    }

    [HttpGet("list-users/{adminPassword}")]
    public async Task<ActionResult<AdminUser[]>> GetAdminUsers(string adminPassword)
    {
        if (!AdminTokenValid(adminPassword)) return Unauthorized();
        var users = await _dbContext.User.ToArrayAsync();
        return users.Select(u => new AdminUser
        {
            name = u.Name,
            userId = u.Id.ToString()
        }).ToArray();
    }
    
    [HttpGet("get-token/{userId}/{adminPassword}")]
    public async Task<ActionResult<AdminTokenResponse>> GetAdminToken(string userId, string adminPassword)
    {
        if (!AdminTokenValid(adminPassword)) return Unauthorized();
        var guid = Guid.Parse(userId);
        var user = await _dbContext.User.FirstAsync(u => u.Id == guid);
        var jwt = _jwtProvider.GetJwt(user);
        
        return new AdminTokenResponse
        {
            jwt = jwt
        };
    }
    
    [HttpGet("stats")]
    public async Task<Stats> GetStats()
    {
        var summaries = await _dbContext.PlaybackSummaries.SumAsync(ps => ps.TotalSeconds);
        var users = await _dbContext.User.CountAsync();

        return new Stats
        {
            Users = users,
            UserMinutes = summaries / 60
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