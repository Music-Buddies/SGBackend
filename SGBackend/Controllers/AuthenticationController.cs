using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Mvc;

namespace SGBackend.Controllers;

[ApiController]
public class AuthenticationController  : ControllerBase
{

    [HttpGet("~/signin-spotify")]
    public async Task<IActionResult> GetCode([FromQuery] string code)
    {
        return Ok();
    }
    [HttpPost("~/signin")]
    public async Task<IActionResult> SignIn()
    {
        var provider = "Spotify";
        // Note: the "provider" parameter corresponds to the external
        // authentication provider choosen by the user agent.
        if (string.IsNullOrWhiteSpace(provider))
        {
            return BadRequest();
        }

        if (!await HttpContext.IsProviderSupportedAsync(provider))
        {
            return BadRequest();
        }
        var c =Challenge(new AuthenticationProperties { RedirectUri = "/" }, provider);

        // Instruct the middleware corresponding to the requested external identity
        // provider to redirect the user agent to its own authorization endpoint.
        // Note: the authenticationScheme parameter must match the value configured in Startup.cs
        return c;
    }

    [HttpGet("~/signout")]
    [HttpPost("~/signout")]
    public IActionResult SignOutCurrentUser()
    {
        // Instruct the cookies middleware to delete the local cookie created
        // when the user agent is redirected from the external identity provider
        // after a successful authentication flow (e.g Google or Facebook).
        return SignOut(new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme);
    }
}