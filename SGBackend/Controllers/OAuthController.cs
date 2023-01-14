using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;

namespace SGBackend.Controllers;

[ApiController]
public class OAuthController : ControllerBase
{
    [HttpPost("~/signin")]
    public async Task<IActionResult> SignIn()
    {
        // TODO: on multiple content providers, change back to input type field
        // normally [FromForm] provider
        // with  <input type="hidden" name="Provider" value="@scheme.Name" />
        var provider = "Spotify";

        // Note: the "provider" parameter corresponds to the external
        // authentication provider choosen by the user agent.
        if (string.IsNullOrWhiteSpace(provider)) return BadRequest();

        if (!await HttpContext.IsProviderSupportedAsync(provider)) return BadRequest();

        // Instruct the middleware corresponding to the requested external identity
        // provider to redirect the user agent to its own authorization endpoint.
        // Note: the authenticationScheme parameter must match the value configured in Startup.cs
        return Challenge(new AuthenticationProperties { RedirectUri = "/" }, provider);
    }
}

public static class HttpContextExtensions
{
    public static async Task<AuthenticationScheme[]> GetExternalProvidersAsync(this HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var schemes = context.RequestServices.GetRequiredService<IAuthenticationSchemeProvider>();

        return (from scheme in await schemes.GetAllSchemesAsync()
            where !string.IsNullOrEmpty(scheme.DisplayName)
            select scheme).ToArray();
    }

    public static async Task<bool> IsProviderSupportedAsync(this HttpContext context, string provider)
    {
        ArgumentNullException.ThrowIfNull(context);

        return (from scheme in await context.GetExternalProvidersAsync()
            where string.Equals(scheme.Name, provider, StringComparison.OrdinalIgnoreCase)
            select scheme).Any();
    }
}