using Meadow.Api.Infrastructure;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

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

        // Kein PUT und kein DELETE: IUdderService hat weder Update noch Remove.
        // Das ist kein Versehen der Naht - die Tabelle ist ein Vorrat aus
        // sechzehn moeglichen Viertelkombinationen, auf den Behandlungszeilen
        // zeigen. Eine Kombination zu aendern hiesse, die Bedeutung bereits
        // geschriebener Behandlungen zu aendern.
        return api;
    }
}
