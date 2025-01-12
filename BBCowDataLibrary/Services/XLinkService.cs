using BB_Cow.Class;
using Microsoft.Extensions.Hosting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using OpenQA.Selenium.Remote;

namespace BB_Cow.Services;

public class XLinkService
{
    public CowService _CowService;

    public XLinkService(CowService cowService)
    {
        _CowService = cowService;
    }

    public void ExecuteScraper()
    {
        try
        {
            var driver = InitzialieDriver();
            OpenWebsite(driver);
            GetCowData(driver);
            driver.Close();
            driver.Quit();
        }
        catch (Exception e)
        {
            LoggerService.LogError(typeof(XLinkService), "Error at Execution of Webscraper: {@Message}",e, e.Message);
        }
       
    }

    private RemoteWebDriver InitzialieDriver()
    {
        var options = new ChromeOptions();
        LoggerService.LogInformation(typeof(XLinkService), "Found Selenium URL: {@Selenium_URL}", Environment.GetEnvironmentVariable("Selenium_URL"));
        var url = new Uri(Environment.GetEnvironmentVariable("Selenium_URL") ?? "http://192.168.50.225:4444/wd/hub");
        return new RemoteWebDriver(url, options);

    }

    private void OpenWebsite(RemoteWebDriver driver, int page = 0)
    {
        LoggerService.LogInformation(typeof(XLinkService), "Found XLink URL: {@XLinkUrl}", Environment.GetEnvironmentVariable("XLinkUrl"));
        var url = Environment.GetEnvironmentVariable("XLinkUrl") ?? "http://192.168.50.9/Xlink/";
        var completeUrl = url + $"ReportTable.aspx?id=10672&sort=1&dir=True&page={page}&ALAN=&LDN=";
        driver.Navigate().GoToUrl(completeUrl);
    }

    private void GetCowData(RemoteWebDriver driver)
    {
        var cows = new List<XLinkCow>();
        var flag = false;
        var lineCount = 3;
        var pageCounter = 1;

        while (!flag)
        {
            if (driver.FindElements(By.XPath($"/html/body/form/div[3]/table/tbody/tr[{lineCount}]")).Count > 0)
            {
                var newCow = new XLinkCow();
                newCow.CowNumb = int.TryParse(driver.FindElement(By.XPath($"/html/body/form/div[3]/table/tbody/tr[{lineCount}]/td[1]")).Text, out var cowNumb) ? cowNumb : 0;
                newCow.LifeNumb = driver.FindElement(By.XPath($"/html/body/form/div[3]/table/tbody/tr[{lineCount}]/td[2]")).Text;

                if (cows.Any(x => x.LifeNumb == newCow.LifeNumb && x.CowNumb == newCow.CowNumb && !string.IsNullOrWhiteSpace(newCow.LifeNumb)))
                {
                    flag = true;
                }
                else
                {
                    cows.Add(newCow);
                }

                lineCount++;
            }
            else
            {
                OpenWebsite(driver, pageCounter++);
                lineCount = 3;
            }
        }

        SaveCowData(cows).Wait();
    }
    
    private async Task SaveCowData(List<XLinkCow> cows)
    {
        await _CowService.GetAllDataAsync();

        var scraperLifeNums = cows.Select(c => c.LifeNumb).Where(ln => !string.IsNullOrWhiteSpace(ln)).ToList();

        var databaseOnlyLifeNums = _CowService.Cows.Keys.ToList().Except(scraperLifeNums).ToList();

        foreach (var lifeNumb in databaseOnlyLifeNums)
        {
            await _CowService.UpdateIsGoneAsync(lifeNumb, true);
        }

        foreach (var cow in cows)
        {
            var newCow = new Cow(cow.LifeNumb, cow.CowNumb, false);

            if (_CowService.Cows.ContainsKey(cow.LifeNumb) && _CowService.Cows[cow.LifeNumb].CollarNumber != cow.CowNumb)
            {
                await _CowService.UpdateCollarNumberAsync(newCow.EarTagNumber, newCow.CollarNumber);
            }
            else if (!_CowService.Cows.ContainsKey(cow.LifeNumb) && !string.IsNullOrWhiteSpace(cow.LifeNumb))
            {
                await _CowService.InsertDataAsync(newCow);
            }
        }
    }

}