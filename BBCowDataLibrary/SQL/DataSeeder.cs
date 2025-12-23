using System.Collections.Generic;
using System.Threading.Tasks;
using BB_Cow.Class;
using Microsoft.EntityFrameworkCore;

namespace BBCowDataLibrary.SQL;

public static class DataSeeder
{
    public static async Task SeedAsync(DatabaseContext context)
    {
        await SeedKpisAsync(context);
    }

    private static async Task SeedKpisAsync(DatabaseContext context)
    {
        if (await context.KPIs.AnyAsync())
        {
            return;
        }

        var defaultKpis = new List<KPI>
        {
            new()
            {
                Title = "Geplante Kuh Behandlungen",
                Url = "geplante_Kuh_Daten",
                Script = "SELECT CAST(COUNT(*) AS CHAR) AS value FROM Planned_Cow_Treatment;",
                SortOrder = 0
            },
            new()
            {
                Title = "Kuh Behandlungen",
                Url = "Kuh_Daten",
                Script = "SELECT CAST(COUNT(*) AS CHAR) AS value FROM Cow_Treatment;",
                SortOrder = 1
            },
            new()
            {
                Title = "Klauen Behandlungen",
                Url = "Klauen_Daten",
                Script = "SELECT CAST(COUNT(*) AS CHAR) AS value FROM Claw_Treatment;",
                SortOrder = 2
            },
            new()
            {
                Title = "Geplante Klauen Behandlungen",
                Url = "geplante_Klauen_Daten",
                Script = "SELECT CAST(COUNT(*) AS CHAR) AS value FROM Planned_Claw_Treatment;",
                SortOrder = 3
            },
            new()
            {
                Title = "Kuh mit den meisten Behandlungen",
                Url = "Kuh_Daten",
                Script = @"SELECT CAST(c.Collar_Number AS CHAR) AS value 
                            FROM Cow_Treatment ct 
                            LEFT JOIN Cow c ON ct.Ear_Tag_Number = c.Ear_Tag_Number 
                            GROUP BY ct.Ear_Tag_Number 
                            ORDER BY COUNT(*) DESC 
                            LIMIT 1;",
                SortOrder = 4
            },
            new()
            {
                Title = "Meist behandeltes Viertel",
                Url = "Kuh_Daten",
                Script = @"SELECT TRIM(BOTH '/' FROM CONCAT_WS('/ ', 
                            CASE WHEN c.Quarter_LV = 1 THEN 'LV' END, 
                            CASE WHEN c.Quarter_LH = 1 THEN 'LH' END, 
                            CASE WHEN c.Quarter_RV = 1 THEN 'RV' END, 
                            CASE WHEN c.Quarter_RH = 1 THEN 'RH' END )) AS value 
                        FROM Cow_Treatment ct 
                        LEFT JOIN Udder c ON ct.COW_QUARTER_ID = c.UDDER_ID 
                        WHERE c.UDDER_ID != 16 
                        GROUP BY ct.COW_QUARTER_ID 
                        ORDER BY COUNT(*) DESC 
                        LIMIT 1;",
                SortOrder = 5
            },
            new()
            {
                Title = "Kuh mit den meisten Klauen Behandlungen",
                Url = "Klauen_Daten",
                Script = @"SELECT CAST(c.Collar_Number AS CHAR) AS value 
                            FROM Claw_Treatment ct 
                            LEFT JOIN Cow c ON ct.Ear_Tag_Number = c.Ear_Tag_Number 
                            GROUP BY ct.Ear_Tag_Number 
                            ORDER BY COUNT(*) DESC 
                            LIMIT 1;",
                SortOrder = 6
            }
        };

        await context.KPIs.AddRangeAsync(defaultKpis);
        await context.SaveChangesAsync();
    }
}
