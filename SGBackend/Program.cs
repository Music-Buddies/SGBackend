using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;

using SGBackend;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<SgDbContext>();

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
    options.Events = new OAuthEvents()
    {
        OnTicketReceived = async context =>
        {
            // create db user if not exists
            var code = context.Request.Query["code"];
            
            // issue jwt 
            context.Response.Cookies.Append("token", "GENERATEDJWT");
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // overwrite host for oauth redirect
    app.Use(async (context,next) =>
    {
        context.Request.Host =new HostString("localhost:5173");
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();