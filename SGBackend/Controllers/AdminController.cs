using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SecretsProvider;
using SGBackend.Connector.Spotify;
using SGBackend.Controllers.Model;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Provider;
using SGBackend.Service;
using Stats = SGBackend.Controllers.Model.Stats;

namespace SGBackend.Controllers;

[ApiController]
[Route("admin")]
public class AdminController : ControllerBase
{
    private readonly MatchingService _algoService;

    private readonly SgDbContext _dbContext;

    private readonly JwtProvider _jwtProvider;

    private readonly ISecretsProvider _secretsProvider;

    private readonly TransferService _transferService;

    private readonly SpotifyConnector _spotifyConnector;

    private readonly ISpotifyApi _spotifyApi;

    private readonly ILogger<AdminController> _logger;

    public AdminController(MatchingService algoService, SgDbContext dbContext, ISecretsProvider secretsProvider, TransferService transferService,
        JwtProvider jwtProvider, SpotifyConnector spotifyConnector, ISpotifyApi spotifyApi, ILogger<AdminController> logger)
    {
        _algoService = algoService;
        _dbContext = dbContext;
        _secretsProvider = secretsProvider;
        _transferService = transferService;
        _jwtProvider = jwtProvider;
        _spotifyConnector = spotifyConnector;
        _spotifyApi = spotifyApi;
        _logger = logger;
    }
    
    /// <summary>
    /// Need for providing external partners, like apple, google access to an account which they can test the app with.
    /// </summary>
    /// <param name="loginToken"></param>
    /// <returns></returns>
    [HttpGet("login-with-token/{loginToken}")]
    public async Task<ActionResult<AdminTokenResponse>> LoginWithToken(string loginToken)
    {
        if (string.IsNullOrEmpty(loginToken))
        {
            return Unauthorized();
        }
        
        var user = await _dbContext.User.FirstOrDefaultAsync(u => u.LoginToken == loginToken);
        if (user != null)
        {
            var jwt = _jwtProvider.GetJwt(user);

            return new AdminTokenResponse
            {
                jwt = jwt
            };
        }

        return Unauthorized();
    }
    
    [HttpGet("audio-feature-migration/{adminPassword}")]
    public async Task<IActionResult> AudioFeatureMigration(string adminPassword)
    {
        if (!AdminTokenValid(adminPassword)) return Unauthorized();

        var userWithRefreshToken = await _dbContext.User.FirstAsync(u => u.SpotifyRefreshToken != null);
        var spotifyToken =
            await _spotifyConnector.GetAccessTokenUsingRefreshToken(userWithRefreshToken.SpotifyRefreshToken);

        var media = await _dbContext.Media.ToArrayAsync();
        
        foreach (var medium in media)
        {
            _logger.LogInformation("Fetching bpm for medium {mediumUrl}", medium.LinkToMedium);
            var bearer = "Bearer " + spotifyToken.access_token;
            var id = medium.LinkToMedium.Split("/").Last();
            var features = await _spotifyApi.GetFeatures(bearer, id);
            if (features.IsSuccessStatusCode)
            {
                medium.BeatsPerMinute = features.Content.tempo;
            }
        }

        await _dbContext.SaveChangesAsync();
        
        return Ok();
    }
    
    [HttpGet("fetch-and-calc-all-users/{adminPassword}")]
    public async Task<IActionResult> FetchAndCalcUsers(string adminPassword)
    {
        if (!AdminTokenValid(adminPassword)) return Unauthorized();
        await _algoService.FetchAndCalcUsers();
        return Ok();
    }

    /// <summary>
    /// Used in the dev stage for easy user impersonation
    /// </summary>
    /// <param name="adminPassword"></param>
    /// <returns></returns>
    [HttpGet("list-users/{adminPassword}")]
    public async Task<ActionResult<AdminUser[]>> GetUsers(string adminPassword)
    {
        if (!AdminTokenValid(adminPassword)) return Unauthorized();
        var users = await _dbContext.User.ToArrayAsync();
        return users.Select(u => new AdminUser
        {
            name = u.Name,
            userId = u.Id.ToString()
        }).ToArray();
    }

    /// <summary>
    /// Used in the dev stage for easy user impersonation
    /// </summary>
    /// <param name="userId"></param>
    /// <param name="adminPassword"></param>
    /// <returns></returns>
    [HttpGet("get-token/{userId}/{adminPassword}")]
    public async Task<ActionResult<AdminTokenResponse>> GetToken(string userId, string adminPassword)
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

    /// <summary>
    /// Used in several apis that flex the stats of our suggest app
    /// </summary>
    /// <returns></returns>
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
    
    /// <summary>
    /// For manually migrating the database
    /// </summary>
    /// <param name="exportContainer"></param>
    /// <returns></returns>
    [HttpPost("importUsers")]
    public async Task<IActionResult> ImportUsers(ExportContainer exportContainer)
    {
        if (!AdminTokenValid(exportContainer.adminToken)) return Unauthorized();

        await _transferService.ImportUsers(exportContainer);

        return Ok();
    }

    /// <summary>
    /// Creates an export of all users and their media. The dev stage uses this to initialize itself to the most recent prod state.
    /// </summary>
    /// <param name="adminToken"></param>
    /// <returns></returns>
    [HttpPost("exportUsers")]
    public async Task<ActionResult<ExportContainer>> ExportUsers(string adminToken)
    {
        if (!AdminTokenValid(adminToken)) return Unauthorized();

        return await _transferService.ExportUsers();
    }
    
    private bool AdminTokenValid(string adminToken)
    {
        var secrets = _secretsProvider.GetSecret<Secrets>();
        return secrets.AdminToken == adminToken;
    }
}