using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using SGBackend.Connector;
using SGBackend.Controllers;
using SGBackend.Provider;

namespace SGBackend;

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        services.AddExternalApiClients();

        services.AddDbContext<SgDbContext>();
        services.AddSingleton<ISecretsProvider, LocalSecretsProvider>();
        services.AddScoped<SpotifyConnector>();
        services.AddSingleton<TokenProvider>();

        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddControllers();

        // configure jwt validation using tokenprovider
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<TokenProvider>((options, provider) => options.TokenValidationParameters = provider.GetJwtValidationParameters());

        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            
        }).AddCookie(options =>
        {
            options.LoginPath = "/signin";
            options.LogoutPath = "/signout";
        }).AddJwtBearer().AddSpotify(options =>
        {
            options.ClientId = "de22eb2cc8c9478aa6f81f401bcaa23a";
            options.ClientSecret = "03e25493374146c987ee581f6f64ad1f";
            options.Scope.Add("user-read-recently-played");

            options.Events = new OAuthEvents
            {
                OnCreatingTicket = async context =>
                {
                    // this means the user logged in successfully at spotify
                    var spotifyConnector = context.HttpContext.RequestServices.GetRequiredService<SpotifyConnector>();
                    var tokenProvider = context.HttpContext.RequestServices.GetRequiredService<TokenProvider>();

                    var dbUser = await spotifyConnector.HandleUserLoggedIn(context);

                    // write spotify access token to jwt
                    context.Response.Cookies.Append("jwt", tokenProvider.GetJwt(dbUser, new[]
                    {
                        new Claim("spotify-token", context.AccessToken!)
                    }));
                    
                    // cookie is still signed in but its irrelevant since we are using
                    // jwt scheme for auth
                }
            };
        });
    }

    public void Configure(WebApplication app)
    {
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
            // overwrite host for oauth redirect
            // dev fe is running on different port, vite.config.js proxies
            // the relevant oauth requests to the dev running backend
            app.Use(async (context, next) =>
            {
                context.Request.Host = new HostString("localhost:5173");
                await next();
            });

        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<SgDbContext>();
            context.Database.EnsureCreated();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}