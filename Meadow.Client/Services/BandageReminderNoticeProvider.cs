using Meadow.Client.Components.Services;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Client.Services;

/// <summary>
/// Die erste Hinweisquelle: ueberfaellige, noch liegende Verbaende.
///
/// Uebersetzt die gecachten Klauenbehandlungen ueber die gemeinsame Shared-Regel
/// (<see cref="ClawTreatmentExtensions.OverdueBandages"/>) in
/// <see cref="MeadowNotice"/>s und speist sie in den Kern (Task 1) ein. KEINE
/// eigene ueberfaellig-Rechnung hier - dieselbe Regel bedient auch den
/// Push-Scheduler (Task 6), und zwei Kopien driften auseinander.
///
/// Singleton wie die Dienste, aus deren Cache er liest: der Kern
/// (<see cref="MeadowNoticeState"/>) ist Singleton und darf keine
/// Scoped-Abhaengigkeit annehmen. Angemeldet wird der Provider im Program.cs
/// ueber <c>AddProvider</c>, nicht ueber Konstruktor-Injektion in den Kern.
/// </summary>
public sealed class BandageReminderNoticeProvider : INoticeProvider, IDisposable
{
    private readonly IClawTreatmentService _treatments;
    private readonly ISettingsService _settings;
    private readonly MeadowSyncState _sync;

    public BandageReminderNoticeProvider(
        IClawTreatmentService treatments,
        ISettingsService settings,
        MeadowSyncState sync)
    {
        _treatments = treatments;
        _settings = settings;
        _sync = sync;

        // An DATENAENDERUNGEN haengen, nicht an einer eigenen Uhr: kommen neue
        // oder geaenderte Klauenbehandlungen an (Dialog, Outbox, Neuabruf),
        // meldet MeadowSyncState.DataArrived. Der Kern liest daraufhin
        // GetNotices neu - und nach "Verband entfernt" faellt der betroffene
        // Hinweis aus der Liste heraus.
        //
        // Bewusst DataArrived am Singleton MeadowSyncState und nicht die
        // Scoped-Bruecke MeadowDataChanges: ein Singleton darf keine
        // Scoped-Abhaengigkeit annehmen (sonst ScopedInSingletonException). Es
        // ist dasselbe Signal - MeadowDataChanges haengt sich selbst nur daran.
        _sync.DataArrived += OnDataArrived;
    }

    /// <summary>Herkunft der Hinweise; landet in <see cref="MeadowNotice.Source"/>.</summary>
    public string Source => "Verband-Erinnerung";

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>
    /// Ein Hinweis je ueberfaelliger Behandlung, synchron aus dem Cache - kein
    /// Netz, wie es <see cref="INoticeProvider"/> verlangt. "Heute" ist hier
    /// bewusst DateTime.Now: der Client rechnet gegen die Uhr des Geraets, die
    /// Regel selbst bleibt durch den today-Parameter pruefbar.
    /// </summary>
    public IEnumerable<MeadowNotice> GetNotices()
    {
        var reminderDays = _settings.BandageRemovalReminderDays;
        var today = DateTime.Now;

        return ClawTreatmentExtensions
            .OverdueBandages(_treatments.Treatments.Values, reminderDays, today)
            .Select(t => ToNotice(t, today, Source))
            .ToList();
    }

    private static MeadowNotice ToNotice(ClawTreatment t, DateTime today, string source)
    {
        // Ueberfaellig SEIT: Tage ueber den Behandlungstag hinaus. Rein zur
        // Anzeige - die Faelligkeit selbst entscheidet die Shared-Regel.
        var overdueDays = (today.Date - t.TreatmentDate.Date).Days;
        var seit = overdueDays == 1 ? "seit 1 Tag" : $"seit {overdueDays} Tagen";

        return new MeadowNotice
        {
            // Stabile Id je Behandlung: bei jeder Neuberechnung dieselbe, damit
            // der Kern dedupliziert und der Hinweis nicht flackert.
            Id = $"bandage-{t.ClawTreatmentId}",
            Text = $"Verband an Ohrmarke {t.EarTagNumber} liegt {seit} - bitte abnehmen.",
            Severity = MeadowNoticeSeverity.Warning,
            // LINK auf den vorhandenen Abnahme-Weg (Verbaende-Tabelle), KEIN
            // Callback: das Modell liegt in Shared und muss serialisierbar
            // bleiben (Task 6). Dort nimmt der Nutzer den Verband ab, die Daten
            // aendern sich, und der Hinweis verschwindet von selbst.
            ActionHref = "/Verband_Daten",
            ActionLabel = "Verband abnehmen",
            Source = source
        };
    }

    /// <summary>
    /// Nur auf Klauenbehandlungen reagieren - eine geaenderte Kuh oder
    /// Eutertabelle beruehrt keinen Verband-Hinweis und braucht keine
    /// Neu-Aggregation.
    /// </summary>
    private void OnDataArrived(MeadowDataKind kind)
    {
        if (kind == MeadowDataKind.ClawTreatment)
        {
            Changed?.Invoke();
        }
    }

    public void Dispose() => _sync.DataArrived -= OnDataArrived;
}
