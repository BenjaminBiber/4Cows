using BB_Cow.Services;
using BBCowDataLibrary.SQL;

namespace XLinkScraper;

public class WebScraperJob
{
    private readonly XLinkService _xLinkService;

    public WebScraperJob(XLinkService apiService)
    {
        _xLinkService = apiService;
    }

    public async Task ExecuteAsync()
    {
        try
        {
            var isConfigured = await DatabaseService.IsConfigured();
            if(isConfigured)
            {
                _xLinkService.ExecuteScraper();
            }
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}