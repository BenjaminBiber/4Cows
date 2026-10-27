using Meadow.Client.Components.Services;
using Meadow.Client.Components.Ui;
using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Services;

namespace Meadow.Client.Services;

/// <summary>
/// Die zweite Hinweisquelle: heute faellige und ueberfaellige Gaben laufender
/// Behandlungsplaene.
///
/// Rechnet NICHT selbst, sondern ueber die gemeinsame Shared-Regel
/// (<see cref="TreatmentPlanRuns"/>) - dieselbe Funktion bedient Dashboard,
/// Kuh-Seite und die Push vom Server, und zwei Kopien driften auseinander.
/// Abgegangene Kuehe fallen schon in <c>TreatmentPlanRuns.Due</c> heraus.
///
/// Singleton wie der Kern (<see cref="MeadowNoticeState"/>) und wie die
/// Dienste, aus deren Cache er liest; angemeldet wird er im Program.cs ueber
/// <c>AddProvider</c>, nicht ueber Konstruktor-Injektion in den Kern.
/// </summary>
public sealed class TreatmentPlanNoticeProvider : INoticeProvider, IDisposable
{
    private readonly ICowTreatmentService _treatments;
    private readonly IMedicineService _medicines;
    private readonly ICowService _cows;
    private readonly HttpTreatmentPlanCancellationService _cancellations;
    private readonly MeadowSyncState _sync;

    public TreatmentPlanNoticeProvider(
        ICowTreatmentService treatments,
        IMedicineService medicines,
        ICowService cows,
        HttpTreatmentPlanCancellationService cancellations,
        MeadowSyncState sync)
    {
        _treatments = treatments;
        _medicines = medicines;
        // Fuer "abgegangen" (kein Hinweis) und die Halsbandnummer im Text.
        _cows = cows;
        // Bewusst die konkrete Klasse: nur sie traegt das Changed-Ereignis, die
        // Naht ITreatmentPlanCancellationService kennt es nicht.
        _cancellations = cancellations;
        _sync = sync;

        // An DATENAENDERUNGEN haengen, nicht an einer eigenen Uhr: eine neu
        // erfasste Gabe meldet MeadowSyncState.DataArrived, ein Abbruch meldet
        // sich ueber den Abbruch-Dienst. Der Kern liest daraufhin GetNotices
        // neu - und der erledigte Hinweis faellt ohne Reload heraus.
        _sync.DataArrived += OnDataArrived;
        _cancellations.Changed += OnCancellationsChanged;
    }

    /// <summary>Herkunft der Hinweise; landet in <see cref="MeadowNotice.Source"/>.</summary>
    public string Source => "TreatmentPlanReminder";

    /// <inheritdoc />
    public event Action? Changed;

    /// <summary>
    /// Ein Hinweis je faelligem Durchlauf, synchron aus den Caches - kein Netz,
    /// wie es <see cref="INoticeProvider"/> verlangt. "Heute" ist hier
    /// DateTime.Now: der Client rechnet gegen die Uhr des Geraets, die Regel
    /// selbst bleibt durch den today-Parameter pruefbar.
    /// </summary>
    public IEnumerable<MeadowNotice> GetNotices()
    {
        var today = DateTime.Now;
        var runs = TreatmentPlanRuns.Build(_treatments.Treatments.Values, _medicines.Medicines,
            _cancellations.Cancellations.Values, today);

        return TreatmentPlanRuns.Due(runs, _cows.Cows)
            .Select(run => new MeadowNotice
            {
                // Stabil je Durchlauf - flackert nicht bei Neuberechnung.
                Id = $"treatment-plan-{run.StartTreatmentClientId}",
                Text = TreatmentPlanReminderText.For(run,
                    _medicines.Medicines.GetValueOrDefault(run.MedicineId)?.MedicineName ?? "Medikament",
                    _cows.Cows, today),
                Severity = run.Status == TreatmentPlanRunStatus.Overdue
                    ? MeadowNoticeSeverity.Critical
                    : MeadowNoticeSeverity.Warning,
                // LINK statt Callback: das Modell liegt in Shared und muss
                // serialisierbar bleiben. Auf der Lasche erfasst der Nutzer die
                // Gabe, die Daten aendern sich, der Hinweis verschwindet.
                ActionHref = $"/{MeadowRoutes.CowDetailFor(run.CowId)}?tab=plaene",
                ActionLabel = "Ansehen",
                Source = Source
            })
            .ToList();
    }

    /// <summary>
    /// Nur Kuhbehandlungen beruehren einen Behandlungsplan - eine geaenderte
    /// Klauentabelle braucht keine Neu-Aggregation.
    /// </summary>
    private void OnDataArrived(MeadowDataKind kind)
    {
        if (kind == MeadowDataKind.CowTreatment)
        {
            Changed?.Invoke();
        }
    }

    private void OnCancellationsChanged() => Changed?.Invoke();

    public void Dispose()
    {
        _sync.DataArrived -= OnDataArrived;
        _cancellations.Changed -= OnCancellationsChanged;
    }
}
