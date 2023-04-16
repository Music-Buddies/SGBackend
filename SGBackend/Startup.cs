using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.OpenApi.Models;
using MySql.EntityFrameworkCore.Extensions;
using Quartz;
using SecretsProvider;
using SGBackend.Connector;
using SGBackend.Connector.Spotify;
using SGBackend.Entities;
using SGBackend.Models;
using SGBackend.Provider;
using SGBackend.Service;


namespace SGBackend;

// mysql fix
public class MysqlEntityFrameworkDesignTimeServices : IDesignTimeServices
{
    public void ConfigureDesignTimeServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddEntityFrameworkMySQL();
        new EntityFrameworkRelationalDesignServicesBuilder(serviceCollection)
            .TryAddCoreServices();
    }
}

public class Startup
{
    public void ConfigureServices(WebApplicationBuilder builder)
    {
        
        builder.AddSecretsProvider("SG");
        var tempProvider = builder.Services.BuildServiceProvider();
        ISecretsProvider secretsProvider = tempProvider.GetRequiredService<ISecretsProvider>();

        builder.Services.AddExternalApiClients();

        builder.Services.AddDbContext<SgDbContext>();
        builder.Services.AddScoped<SpotifyConnector>();
        builder.Services.AddSingleton<JwtProvider>();
        builder.Services.AddScoped<RandomizedUserService>();
        builder.Services.AddScoped<UserService>();
        builder.Services.AddSingleton<AccessTokenProvider>();
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
                        var paralellAlgo =
                            context.HttpContext.RequestServices.GetRequiredService<ParalellAlgoService>();

                        var history = await spotifyConnector.FetchAvailableContentHistory(dbUser);

                        if (history != null)
                        {
                            // only with valid token
                            await paralellAlgo.Process(dbUser.Id, history);
                        }
                    }
                }
            };
        });
        
        builder.Services.AddSwaggerGen(option =>
        {
            option.SwaggerDoc("v1", new OpenApiInfo { Title = "SG Api", Version = "v1" });
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

            await context.Database.MigrateAsync();
        }
        
        app.UseSwagger();
        app.UseSwaggerUI();
        
        // Configure the HTTP request pipeline.
        if (app.Environment.IsDevelopment())
        {
            // overwrite host for oauth redirect
            // dev fe is running on different port, vite.config.js proxies
            // the relevant oauth requests to the dev running backend
            app.Use(async (context, next) =>
            {
                // localhost:5173 is the default port for serving the frontend with 'npm run dev'
                context.Request.Host = new HostString("localhost:5173");
                await next();
            });

            using (var scope = app.Services.CreateScope())
            {
                var services = scope.ServiceProvider;

                var context = services.GetRequiredService<RandomizedUserService>();
                //context.GenerateXRandomUsersAndCalc(5).Wait();

                var dbContext = services.GetRequiredService<SgDbContext>();
                var state = await dbContext.States.FirstOrDefaultAsync();
                if (state == null)
                {
                    dbContext.States.Add(new State
                    {
                        QuartzApplied = true
                    });

                    // first initialisations
                    var quartzTables = File.ReadAllText("generateQuartzTables.sql");
                    await dbContext.Database.ExecuteSqlRawAsync(quartzTables);
                }
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
        }
        
        // test loggin
        using (var scope = app.Services.CreateScope())
        {
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Startup>>();
            logger.LogInformation("this should be visible in prod aswell");
        }

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
        
        app.Run();
    }
}