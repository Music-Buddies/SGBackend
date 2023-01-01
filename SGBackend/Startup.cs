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
        services.AddSingleton<ISecretsProvider, SecretsProvider>();
        services.AddScoped<SpotifyConnector>();

        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddControllers();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<ISecretsProvider>(
                (options, provider) =>
                {
                    options.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateAudience = false,
                        ValidateIssuer = true,
                        ValidateLifetime = true,
                        ValidateIssuerSigningKey = true,
                        ValidIssuer = "http://localhost:5173",
                        IssuerSigningKey =
                            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(provider.GetSecret("jwt-key")))
                    };
                });

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
                    var dbContext = context.HttpContext.RequestServices.GetRequiredService<SgDbContext>();
                    var spotifyConnector = context.HttpContext.RequestServices.GetRequiredService<SpotifyConnector>();
                    var secretsProvider = context.HttpContext.RequestServices.GetRequiredService<ISecretsProvider>();

                    var dbUser = await spotifyConnector.GetOrCreateUser(context.Identity, dbContext);

                    // issue token with user id
                    var key = Encoding.UTF8.GetBytes(secretsProvider.GetSecret("jwt-key"));

                    var handler = new JsonWebTokenHandler();
                    var token = handler.CreateToken(new SecurityTokenDescriptor
                    {
                        Issuer = "http://localhost:5173",
                        Subject = new ClaimsIdentity(new[]
                        {
                            new Claim("sub", dbUser.Id.ToString()),
                            new Claim("name", dbUser.Name),
                            new Claim("spotify-token", context.AccessToken!)
                        }),
                        SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                            SecurityAlgorithms.HmacSha512Signature),
                        Expires = DateTime.Now.AddHours(3)
                    });

                    context.Response.Cookies.Append("jwt", token);
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