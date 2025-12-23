using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace XLinkScraper.Controllers;

public class XLinkController : Controller
{
    private readonly XLinkService _xLinkService;
    private readonly IDbContextFactory<DatabaseContext> _contextFactory;
    
    public XLinkController(XLinkService xLinkService, IDbContextFactory<DatabaseContext> contextFactory)
    {
        _xLinkService = xLinkService;
        _contextFactory = contextFactory;
    }
    
    [HttpGet]
    [Route("api/SaveCowsToDB")]
    public async Task<IActionResult> GetXLinks()
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
                return BadRequest("Keine Verbindung zur Datenbank");
            }
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
        
        return Ok("Kühe wurden erfolgreich aktualiisiert");
    }
}
