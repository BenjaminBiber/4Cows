using System.Security.Cryptography;
using System.Text;

namespace Meadow.Data.Services;

/// <summary>
/// Der Vergleich des Schluessels, den der Agent im Header X-Agent-Key
/// mitschickt.
///
/// Konstante Laufzeit, weil ein gewoehnlicher Zeichenkettenvergleich beim
/// ersten Unterschied abbricht: wer die Antwortzeit misst, kann den Schluessel
/// damit Zeichen fuer Zeichen erraten. FixedTimeEquals vergleicht immer die
/// ganze Laenge.
///
/// Eigene Klasse und nicht ein paar Zeilen im Endpunkt, damit die Randfaelle
/// (fehlender oder leerer Schluessel) in Meadow.Data.Tests pruefbar sind.
/// </summary>
public static class HorizonAgentKey
{
    /// <summary>
    /// Trifft der vorgelegte Schluessel den konfigurierten? Fehlt eine der
    /// beiden Seiten oder ist sie leer, ist die Antwort immer false - eine
    /// Instanz ohne konfigurierten Schluessel darf niemanden hereinlassen.
    /// </summary>
    public static bool Matches(string? configured, string? presented)
    {
        if (string.IsNullOrWhiteSpace(configured) || string.IsNullOrWhiteSpace(presented))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(configured);
        var b = Encoding.UTF8.GetBytes(presented);

        // FixedTimeEquals verlangt gleiche Laenge; die Laenge selbst ist kein
        // Geheimnis, deshalb ist der fruehe Ausstieg hier unbedenklich.
        return a.Length == b.Length && CryptographicOperations.FixedTimeEquals(a, b);
    }
}
