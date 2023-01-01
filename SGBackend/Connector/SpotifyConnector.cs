using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using SGBackend.Models;

namespace SGBackend.Connector;

public class SpotifyConnector : IContentConnector
{
    private readonly ISpotifyApi _spotifyApi;

    public SpotifyConnector(ISpotifyApi spotifyApi)
    {
        _spotifyApi = spotifyApi;
    }

    public async Task<User> GetOrCreateUser(ClaimsIdentity claimsIdentity, SgDbContext dbContext)
    {
        var spotifyUserUrl = claimsIdentity.FindFirst("urn:spotify:url");
        if (spotifyUserUrl != null)
        {
            var dbUser = await dbContext.User.FirstOrDefaultAsync(user => user.SpotifyId == spotifyUserUrl.Value);
            if (dbUser == null)
            {
                var nameClaim = claimsIdentity.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");
                // create in db
                dbUser = new User
                {
                    SpotifyId = spotifyUserUrl.Value,
                    Name = nameClaim != null ? nameClaim.Value : string.Empty
                };
                dbContext.User.Add(dbUser);
                await dbContext.SaveChangesAsync();
            }

            return dbUser;
        }

        throw new Exception("could not find user url in claims from spotify");
    }

    public async Task FetchEntireContentHistory(string accessToken)
    {
        var history = await _spotifyApi.GetHistory(accessToken);
    }

    public void FetchNewerThanRecord()
    {
    }
}