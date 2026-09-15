using Meadow.Shared.Models;

namespace Meadow.Shared.Lookups;

/// <summary>
/// Die reinen Nachschlage- und Anzeigeregeln rund um den Wie/Wo-Cache. Rumpf
/// hier, delegierende Zeile in WhereHowService.
/// </summary>
public static class WhereHowLookups
{
    public static List<string> WhereHowNames(IEnumerable<WhereHow> whereHows)
    {
        return whereHows.Select(x => x.WhereHowName).Distinct().ToList();
    }

    /// <summary>
    /// Die Id zum Namen, <see cref="int.MinValue"/> wenn es ihn nicht gibt.
    ///
    /// Der Rumpf liegt hier, weil beide Fassungen von GetWhereHowIDByName ihn
    /// brauchen - und weil sie sich genau darueber schon einmal uneinig waren:
    ///
    /// Die EF-Fassung schrieb das Nachschlagen als
    /// <c>(... ?? new WhereHow()).WhereHowId</c>. Der parameterlose
    /// Konstruktor von WhereHow ruft <c>this(0, "", true)</c> - bei einem
    /// Fehltreffer kam also eine 0 heraus statt int.MinValue. Die HTTP-Fassung
    /// lieferte int.MinValue. Die Aufrufstellen pruefen auf int.MinValue; die
    /// 0 rutschte durch und landete als Wie/Wo-Fremdschluessel an einer
    /// Behandlung, obwohl es keinen WhereHow 0 gibt.
    ///
    /// Udder macht es richtig (<c>new Udder()</c> setzt int.MinValue) - die
    /// Asymmetrie zwischen den beiden Modellen war der ganze Fehler. Mit einer
    /// Kopie dieser Regel kann sie nicht wiederkommen.
    ///
    /// Verglichen wird getrimmt und kleingeschrieben, wie an jeder anderen
    /// Namenssuche auch.
    /// </summary>
    public static int FindIdByName(IReadOnlyDictionary<int, WhereHow> whereHows, string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return int.MinValue;
        }

        var gesucht = name.Trim().ToLower();

        var treffer = whereHows.Values.FirstOrDefault(
            x => x.WhereHowName != null && x.WhereHowName.Trim().ToLower() == gesucht);

        return treffer?.WhereHowId ?? int.MinValue;
    }

    /// <summary>
    /// Bei Fehltreffer ein frisches, leeres WhereHow - kein null. Die
    /// Aufrufstellen lesen den Namen direkt.
    /// </summary>
    public static WhereHow GetById(IReadOnlyDictionary<int, WhereHow> whereHows, int whereHowID)
    {
        return whereHows.ContainsKey(whereHowID) ? whereHows[whereHowID] : new WhereHow();
    }

    /// <summary>
    /// Anzeigename, bei Fehltreffer LEER. Kein Platzhalterzeichen: der
    /// Leerstring ist, was GetWhereHowNamesByIds unten herausfiltert.
    /// </summary>
    public static string GetWhereHowNameById(IReadOnlyDictionary<int, WhereHow> whereHows, int id)
    {
        return whereHows.ContainsKey(id) ? whereHows[id].WhereHowName : String.Empty;
    }

    public static List<string> GetWhereHowNamesByIds(IReadOnlyDictionary<int, WhereHow> whereHows, List<int> Ids)
    {
        var returnList = new List<string>();

        foreach (var id in Ids)
        {
            var returnId = GetWhereHowNameById(whereHows, id);
            if (!string.IsNullOrEmpty(returnId) && !returnList.Contains(returnId))
            {
                returnList.Add(returnId);
            }
        }

        return returnList;
    }

    /// <summary>
    /// Wie/Wo mit angehaengten Vierteln.
    ///
    /// Braucht als einzige Regel ZWEI Caches. Beide kommen als Dictionary
    /// herein; die Instanzmethode behaelt ihren IUdderService-Parameter und
    /// holt den zweiten Cache daraus. Der Viertel-Zugriff laeuft ueber
    /// <see cref="UdderLookups.GetById"/>, damit auch der Fehltreffer-Fall nur
    /// einmal beschrieben ist.
    /// </summary>
    public static string GetFullWhereHowName(
        IReadOnlyDictionary<int, WhereHow> whereHows,
        IReadOnlyDictionary<int, Udder> udders,
        int whereHow_id,
        int? udder_id = null)
    {
        var whereHow = GetById(whereHows, whereHow_id);
        if (!udder_id.HasValue)
        {
            return whereHow.WhereHowName;
        }
        else
        {
            var udder = UdderLookups.GetById(udders, udder_id.Value);
            return $"{whereHow.WhereHowName} {GetUdderString(udder)}";
        }
    }

    /// <summary>
    /// Reine Funktion ihres Arguments - ohne Cache, deshalb ohne Cache-
    /// Parameter. Steht trotzdem hier: GetFullWhereHowName oben braucht sie,
    /// und zwei Kopien derselben Klammerschreibweise waeren genau die Drift,
    /// die dieses Projekt verhindern soll.
    /// </summary>
    public static string GetUdderString(Udder udder)
    {
        if (udder.QuarterLH && udder.QuarterLV && udder.QuarterRV && udder.QuarterRH)
        {
            return "(Alle 4)";
        }else if (!udder.QuarterLH && !udder.QuarterLV && !udder.QuarterRV && !udder.QuarterRH)
        {
            return "";
        }

        List<string> results = new List<string>();
        results.Add(udder.QuarterLV ? "LV" : "");
        results.Add(udder.QuarterLH ? "LH" : "");
        results.Add(udder.QuarterRV ? "RV" : "");
        results.Add(udder.QuarterRH ? "RH" : "");
        return $"({String.Join("/ ", results.Where(x => !string.IsNullOrEmpty(x)))})";

    }
}
