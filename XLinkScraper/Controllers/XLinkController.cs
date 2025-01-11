using BB_Cow.Services;
using BBCowDataLibrary.SQL;
using Microsoft.AspNetCore.Mvc;
namespace XLinkScraper.Controllers;

public class XLinkController : Controller
{
    private readonly XLinkService _xLinkService;
    
    public XLinkController(XLinkService xLinkService)
    {
        _xLinkService = xLinkService;
    }
    
    [HttpGet]
    [Route("api/SaveCowsToDB")]
    public async Task<IActionResult> GetXLinks()
    {
        try
        {
            var isConfigured = await DatabaseService.IsConfigured();
            if(isConfigured)
            {
                _xLinkService.ExecuteScraper();
            }
            else
            {
                return BadRequest("Keine Verbindung zur Datenbank");
            }
            _xLinkService.ExecuteScraper();
        }
        catch (Exception e)
        {
            return BadRequest(e.Message);
        }
        
        return Ok("Kühe wurden erfolgreich aktualiisiert");
    }
}