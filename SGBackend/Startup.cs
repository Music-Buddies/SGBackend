using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi.Models;
using Quartz;
using SGBackend.Connector;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Provider;
using SGBackend.Service;

namespace SGBackend;

public class Startup
{
    public void ConfigureServices(WebApplicationBuilder builder)
    {
        ISecretsProvider secretsProvider = null;
        if (builder.Environment.IsDevelopment())
        {
            secretsProvider = new DevSecretsProvider(builder.Configuration);
            builder.Services.AddSingleton<ISecretsProvider, DevSecretsProvider>();
        }else if (builder.Environment.IsProduction())
        {
            builder.Services.AddSingleton<ISecretsProvider, EnvSecretsProvider>();
            secretsProvider = new EnvSecretsProvider();
        }

        builder.Services.AddExternalApiClients();

        builder.Services.AddDbContext<SgDbContext>();
        builder.Services.AddScoped<SpotifyConnector>();
        builder.Services.AddSingleton<JwtProvider>();
        builder.Services.AddScoped<RandomizedUserService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddSingleton<AccessTokenProvider>();
        builder.Services.AddScoped<DevSecretsProvider>();
        builder.Services.AddSingleton<ParalellAlgoService>();

        builder.Services.AddDatabaseDeveloperPageExceptionFilter();
        builder.Services.AddControllers();

        builder.Services.AddQuartz(q =>
        {
            q.UseMicrosoftDependencyInjectionJobFactory();
            q.UsePersistentStore(o =>
            {
                o.UseMySql(secretsProvider.GetSecret<Secrets>().DBConnectionString);
                o.UseJsonSerializer();
            });
        });

        builder.Services.AddQuartzHostedService(o => { o.WaitForJobsToComplete = true; });

        // configure jwt validation using tokenprovider
        builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<JwtProvider>((options, provider) =>
                options.TokenValidationParameters = provider.GetJwtValidationParameters(secretsProvider.GetSecret<Secrets>().JwtKey));

        builder.Services.AddAuthentication(options =>
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
            options.ClientId = secretsProvider.GetSecret<Secrets>().SpotifyClientId;
            options.ClientSecret = secretsProvider.GetSecret<Secrets>().SpotifyClientSecret;
            options.Scope.Add("user-read-recently-played");

            options.Events = new OAuthEvents
            {
                OnTicketReceived = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                    logger.LogTrace("executing onticketrecieved spotify");
                },
                OnCreatingTicket = async context =>
                {
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Startup>>();
                    logger.LogTrace("executing oncreatingticket spotify");
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
                        var paralellAlgo =
                            context.HttpContext.RequestServices.GetRequiredService<ParalellAlgoService>();

                        await paralellAlgo.Process(dbUser.Id,
                            await spotifyConnector.FetchAvailableContentHistory(dbUser));
                    }
                }
            };
        });

        builder.Services.AddSwaggerGen(option =>
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
        app.Use(async (context, next) =>
        {
            var logger = context.RequestServices.GetRequiredService<ILogger<Startup>>();
            logger.LogTrace(context.Request.Path.ToString()); 
            await next();
        });
      
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

        if (app.Environment.IsProduction())
        {
            app.Use(async (context, next) =>
            {
                context.Request.Host = new HostString("suggest-app.com");
                context.Request.Scheme = "https";
                await next();
            });
            
            // create some dummy users for reference aswell
            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                var context = services.GetRequiredService<RandomizedUserService>();
                context.GenerateXRandomUsersAndCalc(2).Wait();
            }
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();


        app.Run();
    }
}