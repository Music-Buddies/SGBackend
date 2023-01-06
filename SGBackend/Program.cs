using SGBackend;

var builder = WebApplication.CreateBuilder(args);
var startup = new Startup();
startup.ConfigureServices(builder);

var app = builder.Build();
await startup.Configure(app);