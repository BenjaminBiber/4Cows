using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.EntityFrameworkCore;

namespace XLinkScraper;

public class WebScraperJob
{
    private readonly XLinkService _xLinkService;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;

    public WebScraperJob(XLinkService apiService, IDbContextFactory<DatabaseContext> contextFactory)
    {
        _xLinkService = apiService;
        _contextFactory = contextFactory;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            await using var context = await _contextFactory.CreateDbContextAsync();
            var canConnect = await context.Database.CanConnectAsync();
            if (canConnect)
            {
                _xLinkService.ExecuteScraper();
            }
            else
            {
                LoggerService.LogWarning(typeof(WebScraperJob), "Database connection unavailable. Skipping scraper execution.");
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}
