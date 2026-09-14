using System.Globalization;
using Meadow.Api.Components.Services;
using Meadow.Api.Infrastructure;
using Meadow.Data.Sql;
using Meadow.Shared.Services;
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

        // Die Konfiguration so, wie ein Client sie in seine EIGENE
        // IConfiguration schieben kann: die Schluessel sind die
        // KONFIGURATIONSSCHLUESSEL, nicht huebsche Feldnamen.
        //
        // Damit bleiben die Lesestellen im Client woertlich stehen. In
        // DatabaseInfoDialog.razor steht heute
        //     Configuration["DB_SERVER"] ?? "127.0.0.1"
        // und genau das funktioniert weiter, sobald der Dialog seine Werte
        // von hier bekommt statt aus der Umgebung des Servers.
        //
        // Ein Woerterbuch und kein anonymes Objekt: MeadowJson setzt
        // PropertyNamingPolicy auf camelCase, aus DB_SERVER wuerde also
        // "dbSERVER" - aus Demo:Enabled etwas noch Unbrauchbareres. Fuer
        // Woerterbuch-SCHLUESSEL gilt DictionaryKeyPolicy, und die ist nicht
        // gesetzt: sie gehen unveraendert raus.
        //
        // ABSICHTLICH NICHT DABEI: DB_User und DB_Password.
        //
        // Sie stehen in derselben Konfiguration und werden in Program.cs drei
        // Zeilen neben DB_SERVER gelesen - die Versuchung, sie "der
        // Vollstaendigkeit halber" zu ergaenzen, ist deshalb echt. Dieser
        // Endpunkt ist aber nicht authentifiziert, und wer die Adresse kennt,
        // bekaeme damit die Zugangsdaten der Datenbank im Klartext. Der
        // Dialog, fuer den die Antwort gedacht ist, zeigt ohnehin nur Server
        // und Datenbank an; ein Client, der die Datenbank direkt anspricht,
        // ist kein Fall, den es hier gibt.
        api.MapGet("/system/info", (IConfiguration configuration, DemoSettings demo) =>
            Results.Ok(new Dictionary<string, string>
            {
                // Dieselben Rueckfallwerte wie in Program.cs.
                ["DB_SERVER"] = configuration["DB_SERVER"] ?? "127.0.0.1",
                ["DB_DB"] = configuration["DB_DB"] ?? "4cows_v2",

                // XLink liest der Dienst ueber Environment und nicht ueber
                // IConfiguration - siehe XLinkService.FetchCowsAsync und
                // CowSyncBackgroundService. Hier wird deshalb dieselbe Quelle
                // gelesen, damit die Antwort nicht etwas anderes behauptet als
                // das, was der Abgleich tatsaechlich benutzt.
                ["XLinkUrl"] = XLinkUrl(),
                ["XLinkSyncIntervalHours"] = XLinkIntervalRaw(),

                // Aus DemoSettings und nicht aus der Rohkonfiguration:
                // Program.cs faellt bei einem unlesbaren Wert still auf false
                // bzw. 3 zurueck, und der Client soll erfahren, was GILT.
                ["Demo:Enabled"] = demo.Enabled ? "true" : "false",
                ["Demo:ResetHour"] = demo.ResetHour.ToString(CultureInfo.InvariantCulture)
            })).WithName("SystemInfo");

        // Dasselbe noch einmal, aber getippt und zum Anzeigen: camelCase-Namen
        // und echte Typen statt Zeichenketten. /system/info fuettert die
        // Konfiguration eines Clients, das hier fuellt eine Oberflaeche.
        //
        // Der Unterschied, der beide Endpunkte rechtfertigt, ist
        // databaseConnected: das ist kein Konfigurationswert, sondern eine
        // Messung, und sie kostet eine Anfrage an die Datenbank. Sie gehoert
        // deshalb nicht in eine Antwort, die ein Client beim Start einmal
        // abholt und danach als Einstellungen behaelt.
        api.MapGet("/config", async (
            IConfiguration configuration,
            DemoSettings demo,
            IDbContextFactory<DatabaseContext> factory) =>
        {
            bool connected;
            try
            {
                await using var context = await factory.CreateDbContextAsync();
                connected = await context.Database.CanConnectAsync();
            }
            catch
            {
                // Wie bei /health: eine unerreichbare Datenbank ist eine
                // Aussage dieses Endpunkts und kein Fehler von ihm.
                connected = false;
            }

            return Results.Ok(new
            {
                dbServer = configuration["DB_SERVER"] ?? "127.0.0.1",
                dbDatabase = configuration["DB_DB"] ?? "4cows_v2",
                xlinkUrl = XLinkUrl(),
                xlinkIntervalHours = XLinkIntervalHours(),
                demoEnabled = demo.Enabled,
                demoResetHour = demo.ResetHour,
                databaseConnected = connected
            });
        }).WithName("Config");

        // Die sieben Nachschlagetabellen in EINEM Aufruf.
        //
        // Genau die Menge, die MeadowDataLoader.EnsureLookupsAsync laedt - die
        // Liste dort ist die massgebliche, weil jede Razor-Seite ueber sie
        // geht. Kommt dort eine Tabelle dazu, gehoert sie auch hierher.
        //
        // Ohne diesen Endpunkt kostet jeder Seitenwechsel im Client sieben
        // Anfragen, von denen keine ohne die anderen brauchbar ist: eine
        // Behandlungszeile traegt Medicine_ID, WhereHow_ID, UDDER_ID,
        // Treatment_Reason_ID und Cow_ID, und ohne alle fuenf Tabellen zeigt
        // die Tabelle Zahlen statt Namen.
        api.MapGet("/bootstrap", async (
            ISettingsService settings,
            ICowService cows,
            IMedicineService medicines,
            IWhereHowService whereHows,
            ITreatmentReasonService reasons,
            IClawFindingService findings,
            IUdderService udders) =>
        {
            // Reihenfolge wie in EnsureLookupsAsync. Nacheinander und nicht
            // parallel: die Dienste teilen sich eine DbContextFactory, und ein
            // DbContext vertraegt keine zwei gleichzeitigen Abfragen.
            await settings.GetAllDataAsync();
            await cows.GetAllDataAsync();
            await medicines.GetAllDataAsync();
            await whereHows.GetAllDataAsync();
            await reasons.GetAllDataAsync();
            await findings.GetAllDataAsync();
            await udders.GetAllDataAsync();

            return Results.Ok(new
            {
                // settings als Objekt, die uebrigen als Listen - genau so, wie
                // die Einzelendpunkte sie liefern. Ein Client, der /bootstrap
                // gegen /settings tauscht, muss nichts umbauen.
                settings = settings.Settings,
                cows = cows.Cows.Values.ToList(),
                medicines = medicines.Medicines.Values.ToList(),
                whereHows = whereHows.WhereHows.Values.ToList(),
                reasons = reasons.Reasons.Values.ToList(),
                findings = findings.Findings.Values.ToList(),
                udders = udders.Udder.Values.ToList()
            });
        }).WithName("Bootstrap");

        return api;
    }

    /// <summary>
    /// Die XLink-Adresse mit demselben Rueckfallwert wie in
    /// XLinkService.FetchCowsAsync und DatabaseInfoDialog.razor.
    /// </summary>
    internal static string XLinkUrl()
        => Environment.GetEnvironmentVariable("XLinkUrl") ?? "http://192.168.50.9/Xlink/";

    /// <summary>
    /// Das Abgleichsintervall als ZEICHENKETTE, und zwar moeglichst die
    /// unveraenderte aus der Umgebung.
    ///
    /// Der Grund ist die Kultur: DatabaseInfoDialog.razor liest den Wert mit
    /// double.TryParse OHNE CultureInfo, also in der Kultur des Prozesses. Ein
    /// hier aus einer Zahl formatiertes "0.5" waere auf einem deutschen System
    /// unlesbar, "0,5" auf einem englischen. Die Zeichenkette
    /// zurueckzureichen, die der Dialog heute ohnehin liest, hat dieses
    /// Problem nicht.
    ///
    /// Der Rueckfallwert ist "24", wie in CowSyncBackgroundService.
    /// </summary>
    internal static string XLinkIntervalRaw()
    {
        var raw = Environment.GetEnvironmentVariable("XLinkSyncIntervalHours");
        return double.TryParse(raw, out var hours) && hours > 0 ? raw! : "24";
    }

    /// <summary>Dasselbe Intervall als Zahl, fuer /config und /xlink/status.</summary>
    internal static double XLinkIntervalHours()
        => double.TryParse(Environment.GetEnvironmentVariable("XLinkSyncIntervalHours"), out var hours) && hours > 0
            ? hours
            : 24;
}
