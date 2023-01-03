using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SGBackend.Connector;
using SGBackend.Controllers;
using SGBackend.Provider;
using SGBackend.Service;

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
        services.AddScoped<PlaybackService>();
        services.AddScoped<RandomizedUserService>();

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
                    
                    // TODO: move in quartz logic, only for dev now
                    var playbackService = context.HttpContext.RequestServices.GetRequiredService<PlaybackService>();

                    var newInsertedRecords = await playbackService.InsertNewRecords(dbUser,
                        await spotifyConnector.FetchAvailableContentHistory(context.AccessToken));

                    var upsertedSummaries = await playbackService.UpsertPlaybackSummary(newInsertedRecords);

                    await playbackService.UpdatePlaybackMatches(upsertedSummaries);
                }
            };
        });
        
        services.AddSwaggerGen(option =>
        {
            option.SwaggerDoc("v1", new OpenApiInfo { Title = "Demo API", Version = "v1" });
            option.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                In = ParameterLocation.Header,
                Description = "Please enter a valid token",
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                BearerFormat = "JWT",
                Scheme = "Bearer"
            });
            option.AddSecurityRequirement(new OpenApiSecurityRequirement
            {
                {
                    new OpenApiSecurityScheme
                    {
                        Reference = new OpenApiReference
                        {
                            Type=ReferenceType.SecurityScheme,
                            Id="Bearer"
                        }
                    },
                    new string[]{}
                }
            });
        });
    }

    public void Configure(WebApplication app)
    {
        // create db if not already
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<SgDbContext>();
            context.Database.EnsureCreated();
        }

        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
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

                var context = services.GetRequiredService<RandomizedUserService>();
                context.GenerateXRandomUsersAndCalc(5).Wait();
            }
        }
        
        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();

        app.Run();
    }
}