using _4Cows_FE.Components._4CowsComponent.Dialogs;
using _4Cows_FE.Components.Services;
using BB_Cow.Class;
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
    private async Task ShowAndNotifyAsync<TDialog>(
        MeadowDataKind kind, DialogParameters? parameters = null)
        where TDialog : ComponentBase
    {
        var dialog = parameters is null
            ? await _dialogs.ShowAsync<TDialog>(string.Empty, AddOptions)
            : await _dialogs.ShowAsync<TDialog>(string.Empty, parameters, AddOptions);

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

    // ---- Mit vorbelegtem Tier (Kuh-Seite) -------------------------------
    //
    // Bewusst Ueberladungen und keine optionalen Parameter an den vier
    // Methoden darueber: FabActionFor gibt sie als Methodengruppe an ein
    // Func<Task> weiter, und eine Methodengruppen-Konvertierung darf einen
    // optionalen Parameter nicht weglassen - das haette FAB, Add-Menue und die
    // vier Dashboard-Knoepfe gleichzeitig gebrochen.
    //
    // Der Parameter ist ueberall die Cow_ID, nicht die Ohrmarke und nicht die
    // Halsbandnummer.
    //
    // "new CowTreatment { ... }" laeuft ueber den parameterlosen Konstruktor:
    // WhereHowId und UdderId behalten ihre int.MinValue-Sentinels und
    // AdministrationDate bleibt default, woraus der Dialog DateTime.Now macht.
    // Dasselbe Muster nutzt Planned_Cow_Table.Complete bereits.

    public Task OpenCowTreatmentAsync(string cowId)
        => ShowAndNotifyAsync<Add_Cow_Treatment_Dialog>(
            MeadowDataKind.CowTreatment,
            new DialogParameters<Add_Cow_Treatment_Dialog>
            {
                { x => x.Cow_Treatment, new CowTreatment { EarTagNumber = cowId } }
            });

    public Task OpenPlannedCowTreatmentAsync(string cowId)
        => ShowAndNotifyAsync<Add_Planned_Cow_Treatment_Dialog>(
            MeadowDataKind.PlannedCowTreatment,
            new DialogParameters<Add_Planned_Cow_Treatment_Dialog>
            {
                { x => x.CowId, cowId }
            });

    public Task OpenClawTreatmentAsync(string cowId)
        => ShowAndNotifyAsync<Add_Claw_Treatment_Dialog>(
            MeadowDataKind.ClawTreatment,
            new DialogParameters<Add_Claw_Treatment_Dialog>
            {
                { x => x.Claw_Treatment, new ClawTreatment { EarTagNumber = cowId } }
            });

    public Task OpenPlannedClawTreatmentAsync(string cowId)
        => ShowAndNotifyAsync<Add_Planned_Claw_Treatment_Dialog>(
            MeadowDataKind.PlannedClawTreatment,
            new DialogParameters<Add_Planned_Claw_Treatment_Dialog>
            {
                { x => x.Planned_Claw_Treatment, new PlannedClawTreatment { EarTagNumber = cowId } }
            });

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
    ///
    /// Kuh-Uebersicht und Kuh-Seite bekommen bewusst KEINE eigene FAB-Aktion:
    /// die Uebersicht legt keine Tiere an (das passiert in den Basisdaten),
    /// und auf der Kuh-Seite stehen die vier vorbelegten Knoepfe im Kopf. Ein
    /// FAB dorthin wuerde einen LEEREN Dialog oeffnen und damit genau die
    /// Vorbelegung unterlaufen, wegen der man auf der Seite ist. Beide fallen
    /// deshalb ueber den Standardzweig in das Add-Menue.
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
