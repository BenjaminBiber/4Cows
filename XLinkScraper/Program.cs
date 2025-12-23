using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Hangfire;
using Hangfire.Redis.StackExchange;
using Microsoft.EntityFrameworkCore;
using XLinkScraper;

var builder = WebApplication.CreateBuilder(args);
builder.Configuration.AddEnvironmentVariables();
LoggerService.InitializeLogger();
var databaseSettings = new DatabaseConnectionSettings
{
    Server = builder.Configuration["DB_SERVER"] ?? "127.0.0.1",
    User = builder.Configuration["DB_User"] ?? "root",
    Password = builder.Configuration["DB_Password"] ?? "admin",
    Database = builder.Configuration["DB_DB"] ?? "4cows_v2",
    Port = uint.TryParse(builder.Configuration["DB_PORT"], out var port) ? port : 3306
};
var connectionString = ConnectionStringFactory.Create(databaseSettings);
await DatabaseInitializer.EnsureDatabaseAsync(connectionString);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSingleton<DatabaseStatusService>();
builder.Services.AddDbContextFactory<DatabaseContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));
builder.Services.AddSingleton<CowService>();
builder.Services.AddSingleton<XLinkService>();
builder.Services.AddTransient<WebScraperJob>();
builder.Services.AddSwaggerGen();
Console.WriteLine(Environment.GetEnvironmentVariable("REDIS_URL"));
//builder.Services.AddHangfire(config =>
    //config.UseRedisStorage(Environment.GetEnvironmentVariable("REDIS_URL") ?? "192.168.50.225:6379")); 
//builder.Services.AddHangfireServer();
var app = builder.Build();
using (var scope = app.Services.CreateScope())
{
    var contextFactory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<DatabaseContext>>();
    await using var context = await contextFactory.CreateDbContextAsync();
    await MigrationHelper.EnsureInitialMigrationRecordedAsync(context, "20251223183944_InitialCreate", "8.0.6");
    await context.Database.MigrateAsync();
    await DataSeeder.SeedAsync(context);
}

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
