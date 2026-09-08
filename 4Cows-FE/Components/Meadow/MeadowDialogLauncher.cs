using _4Cows_FE.Components._4CowsComponent.Dialogs;
using _4Cows_FE.Components.Services;
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
    private readonly MeadowDataChanges _changes;

    public MeadowDialogLauncher(IDialogService dialogs, MeadowDataChanges changes)
    {
        _dialogs = dialogs;
        _changes = changes;
    }

    /// <summary>
    /// Oeffnet einen Hinzufuegen-Dialog, wartet bis er geschlossen ist und
    /// meldet die Aenderung, sofern gespeichert wurde.
    ///
    /// Die Open*-Methoden darunter geben nur Task zurueck: sie haengen an
    /// EventCallbacks in FAB, Add-Menue und Dashboard, die kein Ergebnis
    /// brauchen. Gewartet werden MUSS trotzdem - ShowAsync allein kehrt
    /// schon nach dem OEFFNEN zurueck, und dann gibt es keinen Zeitpunkt,
    /// an dem die Meldung stimmt.
    /// </summary>
    private async Task ShowAndNotifyAsync<TDialog>(MeadowDataKind kind)
        where TDialog : ComponentBase
    {
        var dialog = await _dialogs.ShowAsync<TDialog>(string.Empty, AddOptions);
        var result = await dialog.Result;

        if (result is null || result.Canceled)
        {
            return;
        }

        _changes.Notify(kind);
    }

    public Task OpenCowTreatmentAsync()
        => ShowAndNotifyAsync<Add_Cow_Treatment_Dialog>(MeadowDataKind.CowTreatment);

    public Task OpenPlannedCowTreatmentAsync()
        => ShowAndNotifyAsync<Add_Planned_Cow_Treatment_Dialog>(MeadowDataKind.PlannedCowTreatment);

    public Task OpenClawTreatmentAsync()
        => ShowAndNotifyAsync<Add_Claw_Treatment_Dialog>(MeadowDataKind.ClawTreatment);

    public Task OpenPlannedClawTreatmentAsync()
        => ShowAndNotifyAsync<Add_Planned_Claw_Treatment_Dialog>(MeadowDataKind.PlannedClawTreatment);

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
