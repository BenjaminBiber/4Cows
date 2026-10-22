using Meadow.Shared.Models.Horizon;

namespace Meadow.HorizonAgent.Reading;

/// <summary>
/// Ersatz fuer die Entwicklung: auf Entwicklerrechnern gibt es keine Horizon-Datenbank.
/// Wird nur in der Umgebung "Development" eingehaengt, nie im Dienst.
/// </summary>
public sealed class FakeHorizonReader : IHorizonReader
{
    public Task<List<HorizonAnimal>> ReadAnimalsAsync(CancellationToken cancellationToken)
    {
        var animals = Enumerable.Range(1, 25).Select(i => new HorizonAnimal
        {
            AnimalId = i.ToString(),
            CollarNumber = 1000 + i,
            EarTag = $"DE 0909{i:D4}",
            IsGone = i % 7 == 0,
            Location = i % 2 == 0 ? "Stall 1" : "Stall 2",
            Group = i % 3 == 0 ? "Frischmelker" : "Hochleistung",
            Description = i % 5 == 0 ? "Beispielbeschreibung fuer die Entwicklung" : null,
        }).ToList();

        return Task.FromResult(animals);
    }
}
