using BB_Cow.Class;
using Microsoft.Extensions.Hosting;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;

namespace BB_Cow.Services;

public class XLinkService : BackgroundService
{
    public CowService _CowService;

    public XLinkService(CowService cowService)
    {
        _CowService = cowService;
    }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            // var now = DateTime.Now;
            // var nextRun = now.Date.AddHours(2); 
            // if (now > nextRun)
            // {
            //     nextRun = now.Date.AddDays(1).AddHours(2);
            // }
            //
            // var delay = nextRun - now;
            // await Task.Delay(delay, stoppingToken);

            try
            {
                await PerformDailyTask();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }

    private async Task PerformDailyTask()
    {
        var driver = InitzialieDriver();
        OpenWebsite(driver);
        GetCowData(driver);
    }

    private ChromeDriver InitzialieDriver()
    {
        var options = new ChromeOptions();
        options.AddArgument("--headless");
        return new ChromeDriver(options);
    }

    private void OpenWebsite(ChromeDriver driver, int page = 0)
    {
        var url = Environment.GetEnvironmentVariable("XLinkUrl") ?? "http://192.168.50.9/Xlink/";
        var completeUrl = url + $"ReportTable.aspx?id=10672&sort=1&dir=True&page={page}&ALAN=&LDN=";
        driver.Navigate().GoToUrl(completeUrl);
    }

    private void GetCowData(ChromeDriver driver)
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
        foreach (var cow in cows)
        {
            var newCow = new Cow(cow.LifeNumb, cow.CowNumb, false);
            if (_CowService.Cows.ContainsKey(cow.LifeNumb) && _CowService.Cows[cow.LifeNumb].CollarNumber != cow.CowNumb)
            {
                await _CowService.UpdateCollarNumberAsync(newCow.EarTagNumber, newCow.CollarNumber);
            }else if (!_CowService.Cows.ContainsKey(cow.LifeNumb) && !string.IsNullOrWhiteSpace(cow.LifeNumb))
            {
                await _CowService.InsertDataAsync(newCow);
            }
        }
    }

}