using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace SGBackend.Controllers;

[ApiController]
public class TestController : ControllerBase
{
    [Authorize]
    [HttpGet("/get")]
    public async Task<string> testGet()
    {
        var client = HttpContext.Request.HttpContext.RequestServices.GetService<IHttpClientFactory>()?.CreateClient("SpotifyApi");
        var token = HttpContext.User.FindFirst("spotify-token").Value;
        var httpRequestMessage = new HttpRequestMessage(
            HttpMethod.Get,
            "/v1/me/player/recently-played")
        {
            Headers =
            {
                {"Authorization", "Bearer " + token}
            }
        };
        var resp = await client.SendAsync(httpRequestMessage);
        var body = await resp.Content.ReadAsStringAsync();

        return body;
    }
    
}