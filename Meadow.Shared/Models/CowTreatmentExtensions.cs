namespace Meadow.Shared.Models;

/// <summary>
/// Das Gegenstueck zu <see cref="ClawTreatmentExtensions"/> fuer die
/// Kuhbehandlung. Vorerst nur die Kopie - die zwoelf positionsbasierten
/// Zugriffe der Klaue haben hier keine Entsprechung.
/// </summary>
public static class CowTreatmentExtensions
{
    /// <summary>
    /// Eine flache Kopie fuer einen Bearbeiten-Dialog.
    ///
    /// Gereicht wird einem Dialog nie die Instanz aus dem Service-Cache: er
    /// schreibt sein Parameter-Objekt beim Speichern voll, und ein Abbrechen
    /// liesse sonst die halbe Aenderung in der Tabelle stehen, bis irgendwer
    /// neu laedt.
    ///
    /// Die ClientId wird MITGENOMMEN und nicht neu vergeben: sie ist der
    /// Idempotenzschluessel derselben Behandlung, nicht die Kennung dieser
    /// Kopie. Eine neue wuerde aus einer Aenderung serverseitig einen zweiten
    /// Datensatz machen.
    /// </summary>
    public static CowTreatment Copy(this CowTreatment t) => new(
        t.CowTreatmentId, t.EarTagNumber, t.MedicineId, t.AdministrationDate,
        t.MedicineDosage, t.WhereHowId, t.UdderId, t.TreatmentReasonId)
    {
        ClientId = t.ClientId
    };
}
