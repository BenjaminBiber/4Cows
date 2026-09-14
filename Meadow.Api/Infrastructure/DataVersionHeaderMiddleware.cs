using Microsoft.AspNetCore.Http.HttpResults;

namespace Meadow.Api.Infrastructure;

/// <summary>
/// Setzt X-Data-Version auf JEDE Antwort, auch auf GETs und Fehler. Damit
/// erfaehrt ein Client den aktuellen Stand ohne zusaetzlichen Aufruf - er liest
/// ihn von der Antwort, die er ohnehin gerade bekommt.
///
/// OnStarting statt direkt: der Header muss stehen, bevor die ersten Bytes
/// rausgehen, und zu diesem Zeitpunkt hat der Endpunkt seine Bumps gemacht.
/// </summary>
public sealed class DataVersionHeaderMiddleware(RequestDelegate next)
{
    public const string HeaderName = "X-Data-Version";

    public Task InvokeAsync(HttpContext context, IDataVersion version)
    {
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = version.Token;
            return Task.CompletedTask;
        });
        return next(context);
    }
}

public static class DataVersionEndpointFilter
{
    /// <summary>
    /// Erhoeht den Zaehler nach jedem schreibenden Aufruf, der 2xx zurueckgab.
    ///
    /// Als Filter auf dem Endpunkt und nicht als 40 handgeschriebene
    /// Bump-Aufrufe: den Aufruf kann man beim 41. Endpunkt vergessen, den
    /// Filter nicht.
    ///
    /// alsoBumps gibt es fuer die vier Merge-Endpunkte. Ein Medikament zu
    /// verschmelzen schreibt BEHANDLUNGSZEILEN um, also eine fremde Tabelle.
    /// Eine reine Ableitung aus der Route bewertet genau diese vier Faelle
    /// falsch - und genau sie sind es, bei denen ein Client stillschweigend
    /// veraltete Zeilen behaelt.
    /// </summary>
    public static RouteHandlerBuilder BumpsOnWrite(
        this RouteHandlerBuilder builder, DataScope scope, params DataScope[] alsoBumps)
        => builder.AddEndpointFilter(async (context, next) =>
        {
            var result = await next(context);

            var http = context.HttpContext;
            if (HttpMethods.IsGet(http.Request.Method) || HttpMethods.IsHead(http.Request.Method))
            {
                return result;
            }

            // Nicht Response.StatusCode lesen. Ein IResult wird erst
            // AUSGEFUEHRT, nachdem alle Filter zurueckgekehrt sind - an dieser
            // Stelle steht dort noch die 200 aus der Voreinstellung, egal was
            // der Endpunkt entschieden hat. Der Zaehler stiege dann auch bei
            // 404 und 409, und das faellt niemandem auf: ein zu hoher Zaehler
            // laesst Clients nur unnoetig nachladen.
            //
            // Der Status steht im Ergebnisobjekt. Wer keinen traegt, ist ein
            // nackter Rueckgabewert und wird als 200 serialisiert.
            var status = result switch
            {
                IStatusCodeHttpResult s => s.StatusCode ?? StatusCodes.Status200OK,
                null => StatusCodes.Status500InternalServerError,
                _ => StatusCodes.Status200OK
            };

            if (status is >= 200 and < 300)
            {
                var version = http.RequestServices.GetRequiredService<IDataVersion>();
                version.Bump(scope);
                foreach (var also in alsoBumps)
                {
                    version.Bump(also);
                }
            }

            return result;
        });
}
