using BB_Cow.Kpi;

namespace BBCowDataLibrary.Tests.Kpi;

public class KpiScriptGuardTests
{
    /// <summary>Every script DataSeeder has ever shipped must still be accepted.</summary>
    [Theory]
    [InlineData("SELECT CAST(COUNT(*) AS CHAR) AS value FROM Cow_Treatment;")]
    [InlineData("SELECT CAST(COUNT(*) AS CHAR) AS value FROM Planned_Cow_Treatment;")]
    [InlineData(@"SELECT CAST(c.Collar_Number AS CHAR) AS value
                    FROM Cow_Treatment ct
                    LEFT JOIN Cow c ON ct.Ear_Tag_Number = c.Cow_ID
                    GROUP BY ct.Ear_Tag_Number
                    ORDER BY COUNT(*) DESC
                    LIMIT 1;")]
    [InlineData(@"SELECT TRIM(BOTH '/' FROM CONCAT_WS('/ ',
                    CASE WHEN c.Quarter_LV = 1 THEN 'LV' END,
                    CASE WHEN c.Quarter_RH = 1 THEN 'RH' END )) AS value
                  FROM Cow_Treatment ct
                  LEFT JOIN Udder c ON ct.COW_QUARTER_ID = c.UDDER_ID
                  WHERE c.UDDER_ID != 16
                  GROUP BY ct.COW_QUARTER_ID
                  ORDER BY COUNT(*) DESC
                  LIMIT 1;")]
    public void Accepts_the_shipped_seed_scripts(string script)
        => Assert.Null(KpiScriptGuard.Reject(script));

    [Fact]
    public void Accepts_a_common_table_expression()
        => Assert.Null(KpiScriptGuard.Reject("WITH t AS (SELECT 1 AS v) SELECT CAST(v AS CHAR) AS value FROM t"));

    [Theory]
    [InlineData("DELETE FROM Cow_Treatment")]
    [InlineData("DROP TABLE Cow")]
    [InlineData("UPDATE KPI SET Script = 'x'")]
    [InlineData("TRUNCATE TABLE Medicine")]
    [InlineData("INSERT INTO Medicine (Medicine_Name) VALUES ('x')")]
    [InlineData("CALL some_procedure()")]
    [InlineData("GRANT ALL ON *.* TO 'x'")]
    public void Refuses_anything_that_is_not_a_query(string script)
        => Assert.NotNull(KpiScriptGuard.Reject(script));

    [Theory]
    [InlineData("SELECT 1 AS value; DROP TABLE Cow")]
    [InlineData("SELECT 1 AS value;DELETE FROM Cow;")]
    public void Refuses_a_second_statement(string script)
    {
        // The actual attack surface. A single statement beginning with SELECT cannot contain a
        // DELETE or a DROP at all, so this check is what carries the guarantee.
        Assert.NotNull(KpiScriptGuard.Reject(script));
    }

    [Fact]
    public void Allows_a_single_trailing_semicolon()
        => Assert.Null(KpiScriptGuard.Reject("SELECT 1 AS value;   "));

    [Fact]
    public void Refuses_writing_to_the_file_system()
    {
        // The one genuinely dangerous thing a lone SELECT can still do.
        Assert.NotNull(KpiScriptGuard.Reject("SELECT * INTO OUTFILE '/tmp/x' FROM Cow"));
        Assert.NotNull(KpiScriptGuard.Reject("select 1 into dumpfile '/tmp/x'"));
    }

    [Fact]
    public void Sees_through_a_comment_that_hides_the_real_verb()
    {
        Assert.NotNull(KpiScriptGuard.Reject("/* SELECT */ DROP TABLE Cow"));
        Assert.NotNull(KpiScriptGuard.Reject("-- SELECT 1\nDELETE FROM Cow"));
    }

    [Fact]
    public void Accepts_a_query_that_merely_mentions_a_dangerous_word()
    {
        // Exactly the false positive a keyword blacklist would produce, and the reason there is no
        // blacklist: both of these only read.
        Assert.Null(KpiScriptGuard.Reject("SELECT CAST(COUNT(*) AS CHAR) AS `value` FROM Cow /* update check */"));
        Assert.Null(KpiScriptGuard.Reject("SELECT 'delete' AS value"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   \r\n ")]
    [InlineData("-- nur ein Kommentar")]
    public void Refuses_an_empty_script(string? script)
        => Assert.NotNull(KpiScriptGuard.Reject(script));
}
