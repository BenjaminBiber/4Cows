using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Hangfire;
using Hangfire.Redis.StackExchange;
using XLinkScraper;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<CowService>();
builder.Services.AddSingleton<XLinkService>();
builder.Services.AddSwaggerGen();
builder.Configuration.AddEnvironmentVariables();
DatabaseService.GetDBStringFromEnvironment();
Console.WriteLine(Environment.GetEnvironmentVariable("REDIS_URL"));
builder.Services.AddHangfire(config =>
    config.UseRedisStorage(Environment.GetEnvironmentVariable("REDIS_URL") ?? "192.168.50.225:6379")); 
builder.Services.AddHangfireServer();
var app = builder.Build();

var dashboardOptions = new DashboardOptions
{
    Authorization = new[] { new CustomAuthorizationFilter() }
};

app.UseHangfireDashboard("", dashboardOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
RecurringJob.AddOrUpdate<WebScraperJob>("WebScraperJob", x => x.ExecuteAsync(), Cron.Daily);
app.Run();