using System.Text.RegularExpressions;
using BB_Cow.Class;

namespace BB_Cow.Services;

public class XLinkService
{
    // Matches a single data row of the XLink ReportTable: two adjacent
    // <td ...><nobr>VALUE</nobr></td> cells (col 1 = collar number, col 2 = life number).
    // The header row uses <th>/<a> without <nobr>, so it is skipped automatically.
    private static readonly Regex RowRegex = new(
        @"<td[^>]*>\s*<nobr>([^<]*)</nobr>\s*</td>\s*<td[^>]*>\s*<nobr>([^<]*)</nobr>\s*</td>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Matches the pager label, e.g. <span id="PageCurrentLabel">1&nbsp;/&nbsp;17</span>
    // Group 1 = current page, group 2 = total page count.
    private static readonly Regex PageCountRegex = new(
        "PageCurrentLabel\">\\s*(\\d+)\\D+(\\d+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private const int MaxPages = 1000;

    // A single long-lived client is the recommended pattern for a fixed endpoint
    // and avoids socket exhaustion; the ReportTable is fetched at most daily/on demand.
    private static readonly HttpClient HttpClient = new();

    private readonly CowService _cowService;

    public XLinkService(CowService cowService)
    {
        _cowService = cowService;
    }

    public async Task RefreshCowsAsync(CancellationToken cancellationToken = default)
    {
        var cows = await FetchCowsAsync(cancellationToken);
        await SaveCowData(cows);
    }

    private async Task<List<XLinkCow>> FetchCowsAsync(CancellationToken cancellationToken)
    {
        var baseUrl = Environment.GetEnvironmentVariable("XLinkUrl") ?? "http://192.168.50.9/Xlink/";
        var id = Environment.GetEnvironmentVariable("XLinkID") ?? "10672";
        LoggerService.LogInformation(typeof(XLinkService), "Fetching XLink data from {@XLinkUrl} (id {@XLinkID})", baseUrl, id);

        var cows = new List<XLinkCow>();

        var firstHtml = await HttpClient.GetStringAsync(BuildUrl(baseUrl, id, 0), cancellationToken);
        var totalPages = ParseTotalPages(firstHtml);
        ParseRows(firstHtml, cows);

        for (var page = 1; page < totalPages; page++)
        {
            var html = await HttpClient.GetStringAsync(BuildUrl(baseUrl, id, page), cancellationToken);
            ParseRows(html, cows);
        }

        LoggerService.LogInformation(typeof(XLinkService), "Fetched {@Count} cows from XLink across {@Pages} page(s).", cows.Count, totalPages);
        return cows;
    }

    private static string BuildUrl(string baseUrl, string id, int page)
        => $"{baseUrl}ReportTable.aspx?id={id}&sort=1&dir=True&page={page}&ALAN=&LDN=";

    private static int ParseTotalPages(string html)
    {
        var match = PageCountRegex.Match(html);
        if (match.Success && int.TryParse(match.Groups[2].Value, out var total) && total > 0)
        {
            return Math.Min(total, MaxPages);
        }

        return 1;
    }

    private static void ParseRows(string html, List<XLinkCow> cows)
    {
        foreach (Match match in RowRegex.Matches(html))
        {
            var collarText = match.Groups[1].Value.Trim();
            var lifeNumb = match.Groups[2].Value.Trim();

            // Life number is the primary key; skip empty rows. No "DE" restriction:
            // ear tags without a "DE" prefix are stored as-is.
            if (string.IsNullOrWhiteSpace(lifeNumb))
            {
                continue;
            }

            cows.Add(new XLinkCow
            {
                CowNumb = int.TryParse(collarText, out var cowNumb) ? cowNumb : 0,
                LifeNumb = lifeNumb
            });
        }
    }

    private async Task SaveCowData(List<XLinkCow> cows)
    {
        await _cowService.GetAllDataAsync();

        var scraperLifeNums = cows
            .Select(c => c.LifeNumb)
            .Where(ln => !string.IsNullOrWhiteSpace(ln))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Mark IsGone only for IDENTIFIED cows (with an ear tag) that are no longer in XLink.
        // Calves have no ear tag and are never part of the scraper set, so they must be excluded
        // here — otherwise every calf would be wrongly flagged as gone on each sync.
        var goneCandidates = _cowService.Cows.Values
            .Where(c => !c.IsGone
                && !string.IsNullOrWhiteSpace(c.EarTagNumber)
                && !scraperLifeNums.Contains(c.EarTagNumber))
            .ToList();

        foreach (var cow in goneCandidates)
        {
            await _cowService.UpdateIsGoneAsync(cow.CowId, true);
        }

        foreach (var cow in cows)
        {
            if (string.IsNullOrWhiteSpace(cow.LifeNumb))
            {
                continue;
            }

            // (a) A cow already carries this ear tag -> keep collar in sync. Matching on the ear tag
            // FIELD (not the Cow_ID key) also covers promoted calves, whose Cow_ID stays a GUID.
            var identified = _cowService.GetByEarTagNumber(cow.LifeNumb);
            if (identified != null)
            {
                if (identified.CollarNumber != cow.CowNumb)
                {
                    await _cowService.UpdateCollarNumberAsync(identified.CowId, cow.CowNumb);
                }
                continue;
            }

            // (b) A local calf carries this collar number but has no ear tag yet -> promote it.
            // The Cow_ID (PK) stays unchanged, so its treatment history remains linked.
            var calf = _cowService.GetCalfByCollarNumber(cow.CowNumb);
            if (calf != null)
            {
                await _cowService.PromoteCalfAsync(calf.CowId, cow.LifeNumb);
                continue;
            }

            // (c) Brand-new identified cow: Cow_ID = ear tag.
            await _cowService.InsertDataAsync(new Cow(cow.LifeNumb, cow.CowNumb, false));
        }
    }
}
