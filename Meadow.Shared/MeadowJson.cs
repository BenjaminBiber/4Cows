using System.Text.Json;
using System.Text.Json.Serialization;

namespace Meadow.Shared;

/// <summary>
/// Der eine JSON-Vertrag. Server und Client benutzen dieselben Einstellungen,
/// weil sie dieselben Typen serialisieren - haetten beide Seiten eigene, waere
/// die erste Abweichung ein Feld, das unterwegs verschwindet, und kein Fehler.
/// </summary>
public static class MeadowJson
{
    public static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions();
        Apply(options);
        return options;
    }

    public static void Apply(JsonSerializerOptions options)
    {
        // Die tragende Zeile dieser Datei.
        //
        // Mit WhenWritingDefault - was viele APIs einstellen, um Bytes zu sparen -
        // faellt jede 0, jedes false und jedes null aus der Antwort. Beim
        // Deserialisieren laeuft dann der parameterlose Konstruktor, und mehrere
        // Modelle setzen dort SENTINELS statt Nullen:
        //
        //   Udder()         -> UdderId = int.MinValue
        //   CowTreatment()  -> WhereHowId = UdderId = int.MinValue,
        //                      AdministrationDate = DateTime.MinValue
        //
        // Eine Behandlung mit UdderId 0 kaeme also als UdderId int.MinValue an.
        // Beides sind Zustaende, gegen die dieser Code schon einmal gekaempft
        // hat - der 15-zeilige Kommentar in UdderService.InsertDataAsync
        // beschreibt, was eine Zeile mit UDDER_ID = -2147483648 anrichtet.
        //
        // Never kostet ein paar Bytes und nimmt dem Vertrag jede Zweideutigkeit:
        // was der Server schreibt, liest der Client zurueck.
        options.DefaultIgnoreCondition = JsonIgnoreCondition.Never;

        // Der ASP.NET-Standard. Ausdruecklich gesetzt, damit ein spaeteres
        // AddJsonOptions ihn nicht unbemerkt umstellt.
        options.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;

        // Beim Lesen grosszuegig, damit ein handgeschriebener Aufruf aus einer
        // .http-Datei nicht an der Gross-/Kleinschreibung scheitert.
        options.PropertyNameCaseInsensitive = true;

        // "0" ist eine Zeichenkette und soll keine Zahl werden. Strict ist der
        // Standard; er steht hier, weil AllowReadingFromString bei Dosierungen
        // (float) eine stille Umdeutung waere.
        options.NumberHandling = JsonNumberHandling.Strict;
    }
}
