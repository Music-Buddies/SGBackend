using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using SGBackend.Models;

namespace SGBackend.Connector;

public class SpotifyConnector : IContentConnector
{
    private readonly ISpotifyApi _spotifyApi;

    private readonly ISpotifyAuthApi _spotifyAuthApi;

    private readonly SgDbContext _dbContext;

    private readonly ILogger<SpotifyConnector> _logger;

    public SpotifyConnector(ISpotifyApi spotifyApi, ISpotifyAuthApi spotifyAuthApi, SgDbContext dbContext, ILogger<SpotifyConnector> logger)
    {
        _spotifyApi = spotifyApi;
        _spotifyAuthApi = spotifyAuthApi;
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<string> GetAccessTokenUsingRefreshToken(User dbUser)
    {
        var token = await _spotifyAuthApi.GetTokenFromRefreshToken(new Dictionary<string, object>()
        {
            { "grant_type", "refresh_token" },
            { "refresh_token", dbUser.SpotifyRefreshToken }
        });

        return token.access_token;
    }
    
    public async Task<User> HandleUserLoggedIn(OAuthCreatingTicketContext context)
    {
        var claimsIdentity = context.Identity;
        _logger.LogInformation(string.Join(", ", claimsIdentity.Claims.Select(claim => claim.ToString())));
        
        var spotifyUserUrl = claimsIdentity.FindFirst("urn:spotify:url");
        if (spotifyUserUrl != null)
        {
            var dbUser = await _dbContext.User
                .Include(u => u.PlaybackRecords)
                .FirstOrDefaultAsync(user => user.SpotifyId == spotifyUserUrl.Value);
            if (dbUser == null)
            {
                var nameClaim = claimsIdentity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
                var profileUrl = claimsIdentity.FindFirst("urn:spotify:profilepicture");
                // create in db
                dbUser = new User
                {
                    SpotifyId = spotifyUserUrl.Value,
                    Name = nameClaim != null ? nameClaim.Value : string.Empty,
                    SpotifyRefreshToken = context.RefreshToken,
                    SpotifyProfileUrl = profileUrl != null ? profileUrl.Value : "https://miro.medium.com/max/659/1*8xraf6eyaXh-myNXOXkqLA.jpeg"
                };
                _dbContext.User.Add(dbUser);
            }
            else
            {
                // user exists only update refresh token
                dbUser.SpotifyRefreshToken = context.RefreshToken;
            }
            
            await _dbContext.SaveChangesAsync();
            return dbUser;
        }

        throw new Exception("could not find user url in claims from spotify");
    }

    public async Task<SpotifyListenHistory> FetchAvailableContentHistory(string accessToken)
    {
        var history = await _spotifyApi.GetEntireAvailableHistory("Bearer " + accessToken);
        return history;
    }
    
}