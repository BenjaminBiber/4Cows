namespace Meadow.Client.Components.Ui;

/// <summary>
/// Bestimmt den zu speichernden Zeitstempel einer erfassten Behandlung aus dem
/// im Dialog gewaehlten Datum.
///
/// Die Behandlungsdialoge haben nur einen Datums-Picker; ein per Picker
/// gewaehltes Datum steht auf Mitternacht. Damit mehrere Behandlungen
/// desselben Tages in der Tabelle nach ihrer Reihenfolge sortiert werden
/// koennen - und nicht als gleichwertige 00:00-Eintraege danebenstehen -
/// bekommt eine Behandlung, die HEUTE stattfindet, zusaetzlich die aktuelle
/// Uhrzeit. Die Tabellen sortieren ueber den vollen DateTime, die Uhrzeit
/// wirkt also von selbst als Feinsortierung innerhalb des Tages.
///
/// Nur fuer den heutigen Tag: fuer ein rueckdatiertes Datum gibt es keine
/// Uhrzeit, die wir kennen wuerden - die aktuelle waere schlicht falsch.
/// Solche Eintraege bleiben auf Mitternacht und sortieren innerhalb ihres
/// Tages wie bisher.
/// </summary>
public static class TreatmentClock
{
    /// <param name="chosen">Das im Dialog gewaehlte Behandlungsdatum.</param>
    /// <param name="now">Die Gegenwart. Parameter statt DateTime.Now im Rumpf,
    /// damit Tests eine feste Gegenwart vorgeben koennen.</param>
    public static DateTime StampFor(DateTime chosen, DateTime now)
        => chosen.Date == now.Date ? chosen.Date + now.TimeOfDay : chosen.Date;

    /// <summary>Ueberladung mit der echten Gegenwart fuer den Aufruf aus den Dialogen.</summary>
    public static DateTime StampFor(DateTime chosen) => StampFor(chosen, DateTime.Now);
}
