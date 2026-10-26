namespace Meadow.Shared.Profile;

/// <summary>
/// Eine Zeile der Kuh-Uebersicht: ein Tier mit seinen Behandlungszaehlern.
///
/// Bewusst nicht das volle <see cref="CowProfile"/> je Kuh - das waere ein
/// Durchlauf ueber alle Behandlungen pro Tier, also quadratisch. Hier reicht,
/// was in der Tabelle steht.
/// </summary>
/// <param name="CollarNumber">Kann von zwei Tieren nacheinander getragen werden.</param>
/// <param name="EarTagNumber">Null bei einem Kalb.</param>
/// <param name="LastTreatment">Die juengere der beiden Arten, null ohne Behandlung.</param>
/// <param name="Group">Lely-Gruppe aus Horizon, null wenn unbekannt.</param>
/// <param name="Location">Lely-Standort aus Horizon, null wenn unbekannt.</param>
public sealed record CowOverviewRow(
    string CowId,
    int CollarNumber,
    string? EarTagNumber,
    bool IsCalf,
    bool IsGone,
    int CowTreatments,
    int ClawTreatments,
    DateTime? LastTreatment,
    string? Group = null,
    string? Location = null)
{
    public int TotalTreatments => CowTreatments + ClawTreatments;
}
