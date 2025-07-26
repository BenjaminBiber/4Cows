using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Hangfire;
using Hangfire.Redis.StackExchange;
using XLinkScraper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
LoggerService.InitializeLogger();
builder.Services.AddSingleton<CowService>();
builder.Services.AddSingleton<XLinkService>();
builder.Services.AddTransient<WebScraperJob>();
builder.Services.AddSwaggerGen();
builder.Configuration.AddEnvironmentVariables();
DatabaseService.GetDBStringFromEnvironment();
Console.WriteLine(Environment.GetEnvironmentVariable("REDIS_URL"));
//builder.Services.AddHangfire(config =>
    //config.UseRedisStorage(Environment.GetEnvironmentVariable("REDIS_URL") ?? "192.168.50.225:6379")); 
//builder.Services.AddHangfireServer();
var app = builder.Build();

var dashboardOptions = new DashboardOptions
{
    Authorization = new[] { new CustomAuthorizationFilter() }
};

//app.UseHangfireDashboard("", dashboardOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseRouting(); // Falls noch nicht vorhanden
app.UseAuthorization(); // schon vorhanden
app.UseEndpoints(endpoints =>
{
    endpoints.MapControllers();
});

app.MapControllers();
//BackgroundJob.Enqueue(() => Console.WriteLine("Testjob läuft!"));
//RecurringJob.AddOrUpdate<WebScraperJob>("WebScraperJob", x => x.ExecuteAsync(), Cron.Daily);
app.Run();