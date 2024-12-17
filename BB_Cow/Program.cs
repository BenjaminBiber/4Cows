using _4Cows_FE.Components;

var builder = WebApplication.CreateBuilder(args);

// Load the Startup class.
var startup = new Startup(builder.Configuration);

// Call ConfigureServices on the Startup class.
startup.ConfigureServices(builder.Services);

var app = builder.Build();

// Call Configure on the Startup class.
startup.Configure(app, app.Environment);