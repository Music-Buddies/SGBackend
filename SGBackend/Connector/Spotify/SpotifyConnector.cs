using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Quartz;
using SGBackend.Entities;
using SGBackend.Provider;
using SGBackend.Service;

namespace SGBackend.Connector.Spotify;

public class SpotifyConnector : IContentConnector
{
    private readonly SgDbContext _dbContext;

    private readonly ILogger<SpotifyConnector> _logger;

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly ISpotifyApi _spotifyApi;

    private readonly ISpotifyAuthApi _spotifyAuthApi;

    private readonly AccessTokenProvider _tokenProvider;

    private readonly UserService _userService;

    public SpotifyConnector(ISpotifyApi spotifyApi, ISpotifyAuthApi spotifyAuthApi, SgDbContext dbContext,
        ILogger<SpotifyConnector> logger, UserService userService, AccessTokenProvider tokenProvider,
        ISchedulerFactory schedulerFactory)
    {
        _spotifyApi = spotifyApi;
        _spotifyAuthApi = spotifyAuthApi;
        _dbContext = dbContext;
        _logger = logger;
        _userService = userService;
        _tokenProvider = tokenProvider;
        _schedulerFactory = schedulerFactory;
    }

    public async Task<TokenResponse?> GetAccessTokenUsingRefreshToken(User dbUser)
    {
        var token = await _spotifyAuthApi.GetTokenFromRefreshToken(new Dictionary<string, object>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", dbUser.SpotifyRefreshToken }
        });

        if (token.IsSuccessStatusCode)
        {
            return token.Content;
        }
        _logger.LogError(token.Error.Content);

        return null;
    }

    public async Task<UserLoggedInResult> HandleUserLoggedIn(OAuthCreatingTicketContext context)
    {
        var claimsIdentity = context.Identity;
        _logger.LogInformation(string.Join(", ", claimsIdentity.Claims.Select(claim => claim.ToString())));

        var spotifyUserUrl = claimsIdentity.FindFirst("urn:spotify:url");
        if (spotifyUserUrl != null)
        {
            var dbUser = await _dbContext.User
                .Include(u => u.PlaybackRecords)
                .FirstOrDefaultAsync(user => user.SpotifyId == spotifyUserUrl.Value);
            var userExistedPreviously = dbUser != null;
            
            if (dbUser != null)
            {
                // user already exists
                
                if (dbUser.SpotifyRefreshToken == null)
                {
                    // user disconnected spotify and logged back in again
                
                    // set refresh token again
                    dbUser.SpotifyRefreshToken = context.RefreshToken;
                    await _dbContext.SaveChangesAsync();

                    // reschedule continuous spotify fetch job
                    var job = JobBuilder.Create<SpotifyContinuousFetchJob>()
                        .UsingJobData("userId", dbUser.Id)
                        .UsingJobData("isInitialJob", false)
                        .Build();
                
                    var trigger = TriggerBuilder.Create()
                        .WithIdentity(dbUser.Id.ToString(), "fetchInitial")
                        .StartNow()
                        .Build();

                    var scheduler = await _schedulerFactory.GetScheduler();
                    await scheduler.ScheduleJob(job, trigger);
                }
                else
                {
                    // user simply logged in again - only update refresh token
                    dbUser.SpotifyRefreshToken = context.RefreshToken;
                    await _dbContext.SaveChangesAsync();
                }
            }
            else
            {
                // user registered freshly
                var nameClaim = claimsIdentity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
                var profileUrl = claimsIdentity.FindFirst("urn:spotify:profilepicture");

                dbUser = await _userService.AddUser(new User
                {
                    SpotifyId = spotifyUserUrl.Value,
                    Name = nameClaim != null ? nameClaim.Value : string.Empty,
                    SpotifyRefreshToken = context.RefreshToken,
                    SpotifyProfileUrl = profileUrl != null
                        ? profileUrl.Value
                        : "https://miro.medium.com/max/659/1*8xraf6eyaXh-myNXOXkqLA.jpeg"
                });

                // schedule continuous spotify fetch job
                var job = JobBuilder.Create<SpotifyContinuousFetchJob>()
                    .UsingJobData("userId", dbUser.Id)
                    .UsingJobData("isInitialJob", true)
                    .Build();

                var trigger = TriggerBuilder.Create()
                    .WithIdentity(dbUser.Id.ToString(), "fetchInitial")
                    .StartNow()
                    .Build();

                var scheduler = await _schedulerFactory.GetScheduler();
                await scheduler.ScheduleJob(job, trigger);
            }

            return new UserLoggedInResult
            {
                User = dbUser,
                ExistedPreviously = userExistedPreviously
            };
        }

        throw new Exception("could not find user url in claims from spotify");
    }

    public async Task<SpotifyListenHistory?> FetchAvailableContentHistory(User user)
    {
        var accessToken = await _tokenProvider.GetAccessToken(user);

        if (accessToken == null)
        {
            return null;
        }
        
        var history =
            await _spotifyApi.GetEntireAvailableHistory("Bearer " + accessToken);
        
        return history;
    }
}