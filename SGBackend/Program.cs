using SGBackend;

var builder = WebApplication.CreateBuilder(args);
var startup = new Startup();
startup.ConfigureServices(builder.Services);

var app = builder.Build();
await startup.Configure(app);


