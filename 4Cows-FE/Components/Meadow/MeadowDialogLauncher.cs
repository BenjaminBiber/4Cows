using _4Cows_FE.Components._4CowsComponent.Dialogs;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace _4Cows_FE.Components.Meadow;

/// <summary>
/// Die vier Hinzufuegen-Dialoge an einer Stelle, weil Header, Tabbar-FAB,
/// Add-Menue und das Dashboard sie alle oeffnen.
///
/// Die DialogOptions entsprechen dem bisherigen Verhalten der Aufrufstellen.
/// In Etappe 4 wandern sie in die gemeinsamen Meadow-Optionen.
/// </summary>
public sealed class MeadowDialogLauncher
{
    private static readonly DialogOptions AddOptions = new()
    {
        CloseOnEscapeKey = true,
        CloseButton = true,
        MaxWidth = MaxWidth.Medium,
        FullWidth = true
    };

    private readonly IDialogService _dialogs;

    public MeadowDialogLauncher(IDialogService dialogs) => _dialogs = dialogs;

    /// <summary>
    /// Oeffnet einen Dialog und wartet, bis er geschlossen ist. Liefert
    /// false, wenn abgebrochen wurde.
    ///
    /// Die Open*-Methoden darunter geben absichtlich nur Task zurueck: sie
    /// haengen an EventCallbacks in Header, FAB und Add-Menue, die kein
    /// Ergebnis brauchen. ShowAsync allein wuerde ausserdem schon nach dem
    /// OEFFNEN fortsetzen - wer danach neu laden will, braucht diese Methode.
    /// </summary>
    public async Task<bool> ShowAndAwaitAsync<TDialog>() where TDialog : ComponentBase
    {
        var dialog = await _dialogs.ShowAsync<TDialog>(string.Empty, AddOptions);
        var result = await dialog.Result;
        return result is not null && !result.Canceled;
    }

    public Task OpenCowTreatmentAsync()
        => _dialogs.ShowAsync<Add_Cow_Treatment_Dialog>(string.Empty, AddOptions);

    public Task OpenPlannedCowTreatmentAsync()
        => _dialogs.ShowAsync<Add_Planned_Cow_Treatment_Dialog>(string.Empty, AddOptions);

    public Task OpenClawTreatmentAsync()
        => _dialogs.ShowAsync<Add_Claw_Treatment_Dialog>(string.Empty, AddOptions);

    public Task OpenPlannedClawTreatmentAsync()
        => _dialogs.ShowAsync<Add_Planned_Claw_Treatment_Dialog>(string.Empty, AddOptions);

    public Task OpenDatabaseInfoAsync()
        => _dialogs.ShowAsync<DatabaseInfoDialog>(string.Empty, new DialogOptions
        {
            MaxWidth = MaxWidth.Small,
            FullWidth = true,
            CloseOnEscapeKey = true
        });

    /// <summary>
    /// Die Aktion, die der FAB auf der aktuellen Route ausloest.
    /// Listen-Routen fuehren direkt zum passenden Dialog, alles andere
    /// oeffnet das Vier-Punkte-Menue (Prototyp: fabAction).
    /// </summary>
    public Func<Task>? FabActionFor(string route) => route switch
    {
        MeadowRoutes.CowTreatments => OpenCowTreatmentAsync,
        MeadowRoutes.PlannedCowTreatments => OpenPlannedCowTreatmentAsync,
        MeadowRoutes.ClawTreatments => OpenClawTreatmentAsync,
        MeadowRoutes.Bandages => OpenClawTreatmentAsync,
        MeadowRoutes.PlannedClawTreatments => OpenPlannedClawTreatmentAsync,
        _ => null
    };

    public static string FabTitleFor(string route) => route switch
    {
        MeadowRoutes.CowTreatments => "Kuh Behandlung hinzufügen",
        MeadowRoutes.PlannedCowTreatments => "Kuh Behandlung planen",
        MeadowRoutes.ClawTreatments => "Klauen Behandlung hinzufügen",
        MeadowRoutes.Bandages => "Klauen Behandlung hinzufügen",
        MeadowRoutes.PlannedClawTreatments => "Klauen Behandlung planen",
        _ => "Hinzufügen"
    };
}
