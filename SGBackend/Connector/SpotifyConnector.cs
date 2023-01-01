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

    public SpotifyConnector(ISpotifyApi spotifyApi, ISpotifyAuthApi spotifyAuthApi, SgDbContext dbContext)
    {
        _spotifyApi = spotifyApi;
        _spotifyAuthApi = spotifyAuthApi;
        this._dbContext = dbContext;
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
        var spotifyUserUrl = claimsIdentity.FindFirst("urn:spotify:url");
        if (spotifyUserUrl != null)
        {
            var dbUser = await _dbContext.User.FirstOrDefaultAsync(user => user.SpotifyId == spotifyUserUrl.Value);
            if (dbUser == null)
            {
                var nameClaim = claimsIdentity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
                // create in db
                dbUser = new User
                {
                    SpotifyId = spotifyUserUrl.Value,
                    Name = nameClaim != null ? nameClaim.Value : string.Empty,
                    SpotifyRefreshToken = context.RefreshToken
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

    public async Task FetchAvailableContentHistory(string accessToken)
    {
        var history = await _spotifyApi.GetEntireAvailableHistory("Bearer " + accessToken);
    }
    
}