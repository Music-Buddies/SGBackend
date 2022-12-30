using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.EntityFrameworkCore;
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
    options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
}).AddCookie(options =>
{
    options.LoginPath = "/signin";
    options.LogoutPath = "/signout";
}).AddSpotify(options =>
{
    options.ClientId = "de22eb2cc8c9478aa6f81f401bcaa23a";
    options.ClientSecret = "03e25493374146c987ee581f6f64ad1f";
    options.Scope.Add("user-read-recently-played");
    options.Events = new OAuthEvents()
    {
        
        OnCreatingTicket = async context =>
        {
            var accessToken = context.AccessToken;
            var refreshToken = context.RefreshToken;
            context.Response.Cookies.Append("token", "GENERATEDJWT");
            
            var client = context.HttpContext.RequestServices.GetService<IHttpClientFactory>()?.CreateClient("SpotifyApi");
            var httpRequestMessage = new HttpRequestMessage(
                HttpMethod.Get,
                "/v1/me/player/recently-played")
            {
                Headers =
                {
                    {"Authorization", "Bearer " +accessToken}
                }
            };
            var resp = await client.SendAsync(httpRequestMessage);
            var body = await resp.Content.ReadAsStringAsync();
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
    app.UseMigrationsEndPoint();
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