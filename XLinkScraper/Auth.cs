using Hangfire.Dashboard;

namespace XLinkScraper;

public class CustomAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        return true;
    }
}