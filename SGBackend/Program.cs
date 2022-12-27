using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<IdentityUser>(
        options => options.SignIn.RequireConfirmedAccount = true)
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<ApplicationDbContext>();

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
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
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