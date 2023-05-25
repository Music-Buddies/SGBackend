using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using SGBackend.Connector.Spotify.Model;
using SGBackend.Entities;
using SGBackend.Provider;
using SGBackend.Service;

namespace SGBackend.Connector.Spotify;

public class SpotifyConnector
{
    private readonly SgDbContext _dbContext;

    private readonly ILogger<SpotifyConnector> _logger;

    private readonly ISpotifyApi _spotifyApi;

    private readonly ISpotifyAuthApi _spotifyAuthApi;

    private readonly AccessTokenProvider _tokenProvider;

    private readonly UserService _userService;

    public SpotifyConnector(ISpotifyApi spotifyApi, ISpotifyAuthApi spotifyAuthApi, SgDbContext dbContext,
        ILogger<SpotifyConnector> logger, UserService userService, AccessTokenProvider tokenProvider)
    {
        _spotifyApi = spotifyApi;
        _spotifyAuthApi = spotifyAuthApi;
        _dbContext = dbContext;
        _logger = logger;
        _userService = userService;
        _tokenProvider = tokenProvider;
    }

    public async Task<TokenResponse?> GetAccessTokenUsingRefreshToken(string spotifyRefreshToken)
    {
        var token = await _spotifyAuthApi.GetTokenFromRefreshToken(new Dictionary<string, object>
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", spotifyRefreshToken }
        });

        if (token.IsSuccessStatusCode) return token.Content;
        _logger.LogError(token.Error.Content);

        return null;
    }

    public async Task<UserLoggedInResult> HandleUserLoggedIn(OAuthCreatingTicketContext context)
    {
        var claimsIdentity = context.Identity;
        _logger.LogInformation("User logged in {claims}", string.Join(", ", claimsIdentity.Claims.Select(claim => claim.ToString())));

        var spotifyUserUrl = claimsIdentity.FindFirst("urn:spotify:url");
        // user registered freshly
        var nameClaim = claimsIdentity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
        var profileUrl = claimsIdentity.FindFirst("urn:spotify:profilepicture");

        if (spotifyUserUrl != null)
        {
            var dbUser = await _dbContext.User
                .Include(u => u.PlaybackRecords)
                .FirstOrDefaultAsync(user => user.SpotifyId == spotifyUserUrl.Value);
            var userExistedPreviously = dbUser != null;

            if (dbUser != null)
            {
                // user already exists
                dbUser.SpotifyProfileUrl = profileUrl?.Value;
                dbUser.Name = nameClaim != null ? nameClaim.Value : string.Empty;
                if (dbUser.SpotifyRefreshToken == null)
                {
                    // user disconnected spotify and logged back in again
                    // set refresh token again
                    dbUser.SpotifyRefreshToken = context.RefreshToken;
                    await _dbContext.SaveChangesAsync();
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
                dbUser = await _userService.AddUser(new User
                {
                    SpotifyId = spotifyUserUrl.Value,
                    Name = nameClaim != null ? nameClaim.Value : string.Empty,
                    SpotifyRefreshToken = context.RefreshToken,
                    SpotifyProfileUrl = profileUrl?.Value
                });
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

        if (accessToken == null) return null;

        var history =
            await _spotifyApi.GetAvailableHistory("Bearer " + accessToken);

        return history;
    }
}