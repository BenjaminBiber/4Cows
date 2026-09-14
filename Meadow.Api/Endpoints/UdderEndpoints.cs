using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>
/// Rumpf von POST /udders/by-quarters. Die vier Viertel sind die GANZE
/// Eingabe - eine Id kommt nicht vor, weil genau sie hier gesucht wird.
/// </summary>
public sealed record UdderByQuartersRequest(bool QuarterLV, bool QuarterLH, bool QuarterRV, bool QuarterRH);

public static class UdderEndpoints
{
    public static RouteGroupBuilder MapUdderEndpoints(this RouteGroupBuilder api)
    {
        api.MapGet("/udders", async (IUdderService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Udder.Values.ToList());
        }).WithName("UdderList");

        // Der Cache und nicht GetById: GetById gibt bei unbekannter Id nach der
        // Regel aus UdderLookups eine Kombination ohne Viertel zurueck statt
        // null. Fuer die Oberflaeche ist das richtig, als Antwort auf ein GET
        // waere es eine erfundene Zeile.
        api.MapGet("/udders/{udderId:int}", async (int udderId, IUdderService svc) =>
        {
            var udder = await EndpointCommon.FindAsync(() => svc.Udder, udderId, svc.GetAllDataAsync);
            return udder is null ? EndpointCommon.NotFound("Euterviertel", udderId) : Results.Ok(udder);
        }).WithName("UdderById");

        // Verlaesst sich auf die Normalisierung in UdderService.InsertDataAsync:
        // eine UdderId von int.MinValue wird dort auf 0 gesetzt, damit EF die
        // Identity vergibt.
        //
        // Ueber HTTP ist dieser Fall keine Theorie, sondern der Normalfall. Der
        // parameterlose Konstruktor von Udder setzt den Sentinel, und
        // MeadowJson stellt DefaultIgnoreCondition auf Never - ein Client, der
        // eine frische Instanz serialisiert, schickt die -2147483648 also
        // wirklich mit; laesst er udderId weg, entsteht sie hier beim
        // Deserialisieren. Beide Wege enden im selben Guard.
        //
        // Der Endpunkt normalisiert bewusst NICHT selbst nach: eine zweite
        // Stelle mit derselben Regel waere die naechste, die jemand vergisst,
        // und ohne sie steht wieder eine Zeile mit UDDER_ID = -2147483648 in
        // der Tabelle, an der jede weitere Viertelkombination scheitert.
        api.MapPost("/udders", async (Udder udder, IUdderService svc) =>
        {
            var ok = await svc.InsertDataAsync(udder);
            return ok
                ? Results.Created($"/api/udders/{udder.UdderId}", udder)
                : EndpointCommon.WriteFailed("Die Viertelkombination konnte nicht angelegt werden.");
        }).BumpsOnWrite(DataScope.Udders).WithName("UdderCreate");

        // Suchen, sonst anlegen - hier ueber die vier Viertel statt ueber
        // einen Namen. Die Begruendung fuer den eigenen Endpunkt steht bei
        // /medicines/by-name.
        //
        // Nur GetIDByBools, NICHT zusaetzlich GetIdForNoQuarters. Der
        // Verdacht, dass "alle vier false" ein Sonderfall waere, liegt nahe -
        // IUdderService hat dafuer eine eigene Methode -, er stimmt hier aber
        // nicht: GetIDByBools vergleicht schlicht alle vier Flaggen gegen den
        // Cache, und ohne Viertel ist eine der sechzehn Kombinationen wie jede
        // andere. Es findet die Zeile also selbst und legt sie sonst selbst
        // an. Beide Methoden hintereinander zu rufen waere ein ZWEITER
        // Anlagepfad fuer dieselbe Zeile - genau das, wogegen der Semaphor in
        // UdderService steht.
        //
        // GetIdForNoQuarters bleibt fuer die Oberflaeche, die kein
        // Udder-Objekt in der Hand hat.
        api.MapPost("/udders/by-quarters", async (UdderByQuartersRequest body, IUdderService svc) =>
        {
            // Id 0 und nicht der Sentinel aus dem parameterlosen Konstruktor:
            // die Identity vergibt die Datenbank. Der Guard in
            // UdderService.InsertDataAsync faengt den Sentinel zwar ab, aber
            // hier gibt es keinen Grund, ihn ueberhaupt erst zu erzeugen.
            var id = await svc.GetIDByBools(
                new Udder(0, body.QuarterLV, body.QuarterLH, body.QuarterRV, body.QuarterRH));

            return id == int.MinValue
                ? EndpointCommon.UpsertFailed("Die Viertelkombination", Quarters(body))
                : Results.Ok(new { id });
        }).BumpsOnWrite(DataScope.Udders).WithName("UdderByQuarters");

        // Kein PUT und kein DELETE: IUdderService hat weder Update noch Remove.
        // Das ist kein Versehen der Naht - die Tabelle ist ein Vorrat aus
        // sechzehn moeglichen Viertelkombinationen, auf den Behandlungszeilen
        // zeigen. Eine Kombination zu aendern hiesse, die Bedeutung bereits
        // geschriebener Behandlungen zu aendern.
        return api;
    }

    /// <summary>
    /// Die gewaehlten Viertel als Text fuer die Fehlermeldung. Eine
    /// Kombination hat keinen Namen, und "Die Viertelkombination liess sich
    /// nicht anlegen" allein sagt nicht, welche.
    /// </summary>
    private static string Quarters(UdderByQuartersRequest body)
    {
        var chosen = new List<string>(4);
        if (body.QuarterLV) chosen.Add("LV");
        if (body.QuarterLH) chosen.Add("LH");
        if (body.QuarterRV) chosen.Add("RV");
        if (body.QuarterRH) chosen.Add("RH");

        return chosen.Count == 0 ? "ohne Viertel" : string.Join("+", chosen);
    }
}
