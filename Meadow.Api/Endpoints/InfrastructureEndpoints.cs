using Meadow.Api.Infrastructure;
using Meadow.Data.Sql;
using Microsoft.EntityFrameworkCore;

namespace Meadow.Api.Endpoints;

public static class InfrastructureEndpoints
{
    public static RouteGroupBuilder MapInfrastructureEndpoints(this RouteGroupBuilder api)
    {
        // Antwortet IMMER mit 200, auch wenn die Datenbank weg ist - der
        // Zustand steht im Rumpf.
        //
        // Eine 503 waere hier verlockend und falsch: ein Docker-Healthcheck
        // wuerde den Container dann waehrend einer Datenbankstoerung neu
        // starten, die die laufende Anwendung heute aussitzt. Der Dialog in der
        // Oberflaeche will ausserdem "erreichbar, aber ohne Datenbank" von
        // "gar nicht erreichbar" unterscheiden koennen, und das geht nur, wenn
        // der erste Fall eine Antwort ist.
        api.MapGet("/health", async (IDbContextFactory<DatabaseContext> factory, IDataVersion version) =>
        {
            bool database;
            try
            {
                await using var context = await factory.CreateDbContextAsync();
                database = await context.Database.CanConnectAsync();
            }
            catch
            {
                database = false;
            }

            return Results.Ok(new
            {
                status = database ? "ok" : "degraded",
                database,
                version = version.Token
            });
        }).WithName("Health");

        // Der Client fragt das nur, wenn ihm der Header an einer Antwort
        // aufgefallen ist. Dann will er wissen, WELCHE Tabelle sich bewegt hat,
        // damit er nicht alle zwoelf neu laedt.
        api.MapGet("/version", (IDataVersion version) => Results.Ok(new
        {
            bootId = version.BootId,
            token = version.Token,
            scopes = version.Scopes
        })).WithName("DataVersion");

        return api;
    }
}
