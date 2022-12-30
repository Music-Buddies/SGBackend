using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authentication.OAuth;

using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

using SGBackend;
using SGBackend.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("SpotifyApi", httpClient =>
{
    httpClient.BaseAddress = new Uri("https://api.spotify.com/");
});

builder.Services.AddDbContext<SgDbContext>();

builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddControllers();

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultSignInScheme =  CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.LoginPath = "/signin";
    options.LogoutPath = "/signout";
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateAudience = false,
        ValidateIssuer = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = "http://localhost:5173",
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY")))
    };
}).AddSpotify(options =>
{ 
    options.ClientId = "de22eb2cc8c9478aa6f81f401bcaa23a";
    options.ClientSecret = "03e25493374146c987ee581f6f64ad1f";
    options.Scope.Add("user-read-recently-played");
    
    options.Events = new OAuthEvents()
    {
        OnCreatingTicket = async context =>
        {
            // this means the user logged in
            
            // todo: find user via db/create
            var key = Encoding.UTF8.GetBytes(Environment.GetEnvironmentVariable("JWT_KEY"));
            
            var handler = new JsonWebTokenHandler();
            var token = handler.CreateToken(new SecurityTokenDescriptor()
            {
                Issuer = "http://localhost:5173",
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim("sub", Guid.NewGuid().ToString()),
                    new Claim("name", "nogg"),
                    new Claim("spotify-token", context.AccessToken!)
                }),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key),
                    SecurityAlgorithms.HmacSha512Signature),
                Expires = DateTime.Now.AddHours(3)
            });
            
           
            context.Response.Cookies.Append("jwt", token);
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // overwrite host for oauth redirect

    app.Use(async (context, next) =>
    {
        context.Request.Host = new HostString("localhost:5173");
        await next();
    });
}

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    var context = services.GetRequiredService<SgDbContext>();
    context.Database.EnsureCreated();

    var user = new User
    {
        ID = "1",
        Name = "TestName",
        SpotifyURL = "TestURL"
    };

    context.Add(user);
    context.SaveChanges();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();