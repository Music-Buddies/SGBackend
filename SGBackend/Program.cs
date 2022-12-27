using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using Microsoft.Net.Http.Headers;
using SGBackend;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient("Spotify", httpClient =>
{
    httpClient.BaseAddress = new Uri("https://accounts.spotify.com/");

    // using Microsoft.Net.Http.Headers;
    // The GitHub API requires two headers.
    httpClient.DefaultRequestHeaders.Add(
        HeaderNames.Authorization,
        "Basic " + Convert.ToBase64String(
            "de22eb2cc8c9478aa6f81f401bcaa23a:03e25493374146c987ee581f6f64ad1f"u8.ToArray()));
  
});
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
            var code = context.Request.Query["code"].First();
            var client = context.HttpContext.RequestServices.GetService<IHttpClientFactory>()?.CreateClient("Spotify");

            var httpRequestMessage = new HttpRequestMessage(
                HttpMethod.Post,
                "/api/token")
            {
                Content = new FormUrlEncodedContent(new[]
                    {
                        new KeyValuePair<string, string>("grant_type", "authorization_code"),
                        new KeyValuePair<string, string>("code", code),
                        new KeyValuePair<string, string>("redirect_url", "http://localhost:5173/signin-spotify"),
                    }
                )
            };
            httpRequestMessage.Content.Headers.ContentType =  new MediaTypeWithQualityHeaderValue("application/x-www-form-urlencoded");
            var httpResponseMessage = await client.SendAsync(httpRequestMessage);
            var resp = await httpResponseMessage.Content.ReadAsStringAsync();
            

            // issue jwt 
            context.Response.Cookies.Append("token", "GENERATEDJWT");
        }
    };
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    Console.WriteLine("is development");
    // overwrite host for oauth redirect
    app.Use(async (context, next) =>
    {
        Console.WriteLine("rewriting");
        context.Request.Host = new HostString("localhost:5173");
        await next();
    });
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();