using Meadow.Api.Infrastructure;
using Meadow.Shared.Services;

namespace Meadow.Api.Endpoints;

/// <summary>Rumpf von PUT /settings/{key}.</summary>
public sealed record SettingValueUpdate(string? Value);

public static class SettingsEndpoints
{
    public static RouteGroupBuilder MapSettingsEndpoints(this RouteGroupBuilder api)
    {
        // Als Objekt und nicht als Liste: der Cache IST ein Woerterbuch, und ein
        // Client, der einen Standardwert nachschlaegt, will dafuer keinen
        // Sucher ueber ein Array schreiben muessen.
        api.MapGet("/settings", async (ISettingsService svc) =>
        {
            await svc.GetAllDataAsync();
            return Results.Ok(svc.Settings);
        }).WithName("SettingList");

        api.MapGet("/settings/{key}", async (string key, ISettingsService svc) =>
        {
            var value = await EndpointCommon.FindAsync(() => svc.Settings, key, svc.GetAllDataAsync);
            return value is null
                ? EndpointCommon.NotFound("Standardwert", key)
                : Results.Ok(new { key, value });
        }).WithName("SettingByKey");

        // Kein POST und keine 404 beim PUT: ISettingsService hat nur SetAsync,
        // und die legt einen unbekannten Schluessel an, statt ihn abzulehnen.
        // Das passt zur Tabelle - sie hat keine feste Schluesselliste, und die
        // beiden heute gepflegten Werte stehen erst drin, seit jemand sie
        // gesetzt hat.
        api.MapPut("/settings/{key}", async (string key, SettingValueUpdate body, ISettingsService svc) =>
        {
            // null wuerde in eine [Required]-Spalte laufen und dort als
            // Ausnahme enden, aus der der Dienst nur ein false macht. Der
            // Aufrufer erfuehre dann 500 statt "Feld fehlt".
            if (body.Value is null)
            {
                return EndpointCommon.Invalid("value", "Ein Standardwert ohne Wert ist keiner.");
            }

            var ok = await svc.SetAsync(key, body.Value);
            return ok
                ? Results.NoContent()
                : EndpointCommon.WriteFailed($"Der Standardwert {key} konnte nicht gespeichert werden.");
        }).BumpsOnWrite(DataScope.Settings).WithName("SettingUpdate");

        return api;
    }
}
