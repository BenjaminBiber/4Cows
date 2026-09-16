using Meadow.Shared.Models;

namespace Meadow.Shared.Kpi;

/// <summary>
/// The dashboard loop, once.
///
/// KPIService and HttpKPIService carried near-identical copies of it: same ordering, same
/// row-reuse dictionary, same builder evaluation, same legacy-script fallback, same "+" tile rule.
/// The ONLY real difference is where a hand-written script's value comes from - a shared
/// DbContext on the server, an HTTP call in the browser - and that is now a delegate.
///
/// Two copies of a loop are not a duplication problem until someone changes one of them. Nothing
/// tested that both produced the same tiles, and every feature added to a tile from here on has to
/// pass through both. So: one function, two callers, and the difference named explicitly.
/// </summary>
public static class KpiDashboard
{
    /// <summary>
    /// Builds every tile in one pass.
    /// </summary>
    /// <param name="kpis">All KPIs; ordered here, so callers cannot disagree about the order.</param>
    /// <param name="rows">
    /// Row projection for one source. Called at most ONCE per distinct source, not once per KPI -
    /// five KPIs over cow treatments share one projection. Pass KpiRowProvider.Rows.
    /// </param>
    /// <param name="sqlValue">
    /// The value of a hand-written script. Only called for KPIs that are not builder-defined, so on
    /// a fresh database it is never called at all and the dashboard touches no database and no
    /// network.
    /// </param>
    /// <param name="now">Injected so the tiles are testable against a fixed clock.</param>
    /// <param name="onError">
    /// Reported per failing KPI. Server and client log through different machinery, and neither
    /// belongs in Meadow.Shared.
    /// </param>
    /// <param name="addButtonKpi">Append the synthetic "add KPI" tile.</param>
    public static async Task<IReadOnlyList<KpiTileModel>> BuildAsync(
        IEnumerable<KPI> kpis,
        Func<KpiSourceId, IReadOnlyList<KpiRow>> rows,
        Func<KPI, Task<string>> sqlValue,
        DateTime now,
        Action<KPI, string>? onError = null,
        bool addButtonKpi = true)
    {
        var tiles = new List<KpiTileModel>();
        var rowsBySource = new Dictionary<KpiSourceId, IReadOnlyList<KpiRow>>();

        foreach (var kpi in kpis.OrderBy(x => x.SortOrder))
        {
            if (kpi.IsBuilder)
            {
                tiles.Add(new KpiTileModel
                {
                    Kpi = kpi,
                    Result = Evaluate(kpi, rows, rowsBySource, now, onError)
                });
                continue;
            }

            var value = await sqlValue(kpi);

            tiles.Add(new KpiTileModel
            {
                Kpi = kpi,
                // A legacy script yields a bare string, so Ok and Error cannot be told apart here -
                // that distinction is exactly what the declarative path buys.
                Result = new KpiResult
                {
                    State = value == LegacyEmpty ? KpiResultState.Empty : KpiResultState.Ok,
                    Display = value
                }
            });
        }

        if (addButtonKpi)
        {
            tiles.Add(KpiTileModel.AddTile());
        }

        return tiles;
    }

    /// <summary>What a legacy script's path returns when it yields nothing or fails.</summary>
    private const string LegacyEmpty = "--";

    /// <summary>
    /// Evaluates one declarative KPI, reusing an already-built row set for its source.
    ///
    /// There is deliberately NO fallback to <see cref="KPI.Script"/> when the definition is
    /// unusable: that would run a query the author believed to be switched off. A visible error is
    /// the honest outcome.
    /// </summary>
    private static KpiResult Evaluate(
        KPI kpi,
        Func<KpiSourceId, IReadOnlyList<KpiRow>> rows,
        Dictionary<KpiSourceId, IReadOnlyList<KpiRow>> rowsBySource,
        DateTime now,
        Action<KPI, string>? onError)
    {
        var definition = KpiDefinition.Deserialize(kpi.Definition);
        if (definition is null)
        {
            return Failed(kpi, "Die Definition dieser KPI ist unlesbar.", onError);
        }

        var source = KpiSourceRegistry.Find(definition.Source);
        if (source is null)
        {
            return Failed(kpi, $"Unbekannte Datenquelle: {definition.Source}.", onError);
        }

        if (!rowsBySource.TryGetValue(definition.Source, out var sourceRows))
        {
            sourceRows = rows(definition.Source);
            rowsBySource[definition.Source] = sourceRows;
        }

        var result = KpiEvaluator.Evaluate(definition, source, sourceRows, now);

        if (result.State == KpiResultState.Error)
        {
            onError?.Invoke(kpi, result.Message ?? "Unbekannter Fehler.");
        }

        return result;
    }

    private static KpiResult Failed(KPI kpi, string message, Action<KPI, string>? onError)
    {
        onError?.Invoke(kpi, message);
        return KpiResult.Failed(message);
    }
}
