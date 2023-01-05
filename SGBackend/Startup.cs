using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Quartz;
using SGBackend.Connector;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
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
        services.AddSingleton<JwtProvider>();
        services.AddScoped<PlaybackService>();
        services.AddScoped<RandomizedUserService>();
        services.AddScoped<UserService>();
        services.AddSingleton<AccessTokenProvider>();

        // register playbacksummaryprocessor and make it gettable
        services.AddSingleton<PlaybackSummaryProcessor>();
        services.AddHostedService(p => p.GetRequiredService<PlaybackSummaryProcessor>());

        services.AddDatabaseDeveloperPageExceptionFilter();

        services.AddControllers();

        services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.UsePersistentStore(o =>
            {
                o.UseMySql("server=localhost;database=sg;user=root;password=root");
                o.UseJsonSerializer();
            });
        });

        services.AddQuartzHostedService(o => { o.WaitForJobsToComplete = true; });

        // configure jwt validation using tokenprovider
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtProvider>((options, provider) =>
                options.TokenValidationParameters = provider.GetJwtValidationParameters());

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
                    var tokenProvider = context.HttpContext.RequestServices.GetRequiredService<JwtProvider>();
                    var accessTokenProvider =
                        context.HttpContext.RequestServices.GetRequiredService<AccessTokenProvider>();

                    var handleResult = await spotifyConnector.HandleUserLoggedIn(context);
                    var dbUser = handleResult.User;

                    if (context.AccessToken != null && context.ExpiresIn.HasValue)
                        accessTokenProvider.InsertAccessToken(dbUser, new AccessToken
                        {
                            Fetched = DateTime.Now,
                            Token = context.AccessToken,
                            ExpiresIn = context.ExpiresIn.Value
                        });

                    // write spotify access token to jwt
                    context.Response.Cookies.Append("jwt", tokenProvider.GetJwt(dbUser));

                    // cookie is still signed in but its irrelevant since we are using
                    // jwt scheme for auth

                    if (!handleResult.ExistedPreviously)
                    {
                        var playbackService = context.HttpContext.RequestServices.GetRequiredService<PlaybackService>();

                        var newInsertedRecords = await playbackService.InsertNewRecords(dbUser,
                            await spotifyConnector.FetchAvailableContentHistory(dbUser));

                        var upsertedSummaries = await playbackService.UpsertPlaybackSummary(newInsertedRecords);

                        if (upsertedSummaries.Any())
                            await playbackService.UpdateMutualPlaybackOverviews(upsertedSummaries);
                    }
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
                            Type = ReferenceType.SecurityScheme,
                            Id = "Bearer"
                        }
                    },
                    new string[] { }
                }
            });
        });
    }

    public async Task Configure(WebApplication app)
    {
        // create db if not already
        using (var scope = app.Services.CreateScope())
        {
            var services = scope.ServiceProvider;

            var context = services.GetRequiredService<SgDbContext>();
            var created = context.Database.EnsureCreated();

            if (created)
            {
                var quartzTables = File.ReadAllText("generateQuartzTables.sql");
                await context.Database.ExecuteSqlRawAsync(quartzTables);
            }
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