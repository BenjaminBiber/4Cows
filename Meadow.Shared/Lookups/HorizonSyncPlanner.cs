using Meadow.Shared.Models;
using Meadow.Shared.Models.Horizon;

namespace Meadow.Shared.Lookups;

public enum HorizonActionKind { Update, Link, Promote, Create }

/// <param name="CowId">Bestehende Cow_ID (Update/Link/Promote) bzw. neue Cow_ID (Create).</param>
/// <param name="NewCow">Nur bei Create: die anzulegende Kuh, alle Felder gesetzt.</param>
public sealed record HorizonCowAction(HorizonActionKind Kind, string CowId, HorizonAnimal Animal, Cow? NewCow);

public sealed record HorizonSyncPlan(
    string? RejectedReason,
    IReadOnlyList<HorizonCowAction> Actions,
    int Unchanged,
    int SkippedGone,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Der Abgleich zwischen einem Horizon-Snapshot und der Meadow-Herde.
/// Reine Funktion: kein I/O, keine Zeit, keine Seiteneffekte auf den uebergebenen Kuehen.
/// Geschrieben wird erst spaeter anhand des Plans.
/// </summary>
public static class HorizonSyncPlanner
{
    public const int MassGoneMinActiveCows = 10;

    /// <param name="newCowId">Erzeugt die Cow_ID fuer neue Tiere ohne Ohrmarke (Standard: neue GUID).</param>
    public static HorizonSyncPlan Plan(IReadOnlyCollection<Cow> cows, HorizonSnapshot snapshot, Func<string>? newCowId = null)
    {
        newCowId ??= () => Guid.NewGuid().ToString();

        var animals = (snapshot.Animals ?? new List<HorizonAnimal>()).Select(Normalize).ToList();

        if (animals.Count == 0)
            return Rejected("Der Snapshot enthält keine Tiere.");

        if (animals.Any(a => a.AnimalId.Length == 0))
            return Rejected("Ein Tier ohne AnimalId.");

        var firstDuplicate = animals
            .GroupBy(a => a.AnimalId, StringComparer.Ordinal)
            .FirstOrDefault(g => g.Count() > 1);
        if (firstDuplicate is not null)
            return Rejected($"AnimalId {firstDuplicate.Key} kommt mehrfach vor.");

        // Die Herde vorab indizieren: jede Kuh wird hoechstens einmal getroffen.
        var byLely = new Dictionary<string, Cow>(StringComparer.Ordinal);
        var byEarTag = new Dictionary<string, Cow>(StringComparer.OrdinalIgnoreCase);
        var calvesByCollar = new Dictionary<int, List<Cow>>();
        var cowIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var cow in cows)
        {
            cowIds.Add(cow.CowId);
            if (!string.IsNullOrWhiteSpace(cow.LelyAnimalId))
                byLely.TryAdd(cow.LelyAnimalId!, cow);
            if (!string.IsNullOrWhiteSpace(cow.EarTagNumber))
                byEarTag.TryAdd(cow.EarTagNumber!, cow);
            if (cow.IsCalv && !cow.IsGone && string.IsNullOrWhiteSpace(cow.EarTagNumber) && string.IsNullOrWhiteSpace(cow.LelyAnimalId))
            {
                if (!calvesByCollar.TryGetValue(cow.CollarNumber, out var list))
                    calvesByCollar[cow.CollarNumber] = list = new List<Cow>();
                list.Add(cow);
            }
        }

        var used = new HashSet<string>(StringComparer.Ordinal);
        var actions = new List<HorizonCowAction>();
        var warnings = new List<string>();
        var unchanged = 0;
        var skippedGone = 0;
        var goingGone = 0;

        // Zaehlt eine Aktion, die eine heute aktive Kuh abgehen laesst (fuer die Schutzregel).
        void CountGone(Cow cow, HorizonAnimal animal)
        {
            if (!cow.IsGone && animal.IsGone)
                goingGone++;
        }

        // Eine Ohrmarke darf nur uebernommen werden, wenn keine andere Kuh sie schon traegt.
        bool EarTagFree(string earTag, Cow owner)
            => !byEarTag.TryGetValue(earTag, out var holder) || ReferenceEquals(holder, owner);

        void WarnEarTagTaken(HorizonAnimal animal)
            => warnings.Add($"Ohrmarke {animal.EarTag} von Tier {animal.AnimalId} ist bereits vergeben – nicht übernommen.");

        foreach (var animal in animals)
        {
            // Regel 1: bekannte Lely-ID gewinnt vor Ohrmarke und Halsband.
            if (byLely.TryGetValue(animal.AnimalId, out var known) && used.Add(known.CowId))
            {
                var takesEarTag = false;
                if (animal.EarTag is not null && !string.Equals(animal.EarTag, known.EarTagNumber, StringComparison.OrdinalIgnoreCase))
                {
                    if (EarTagFree(animal.EarTag, known))
                    {
                        takesEarTag = true;
                        byEarTag[animal.EarTag] = known;
                    }
                    else
                    {
                        WarnEarTagTaken(animal);
                    }
                }

                var changed = takesEarTag
                    || known.CollarNumber != animal.CollarNumber
                    || known.IsGone != animal.IsGone
                    || !string.Equals(known.Location, animal.Location, StringComparison.Ordinal)
                    || !string.Equals(known.Group, animal.Group, StringComparison.Ordinal)
                    || !string.Equals(known.Description, animal.Description, StringComparison.Ordinal);

                if (changed)
                {
                    CountGone(known, animal);
                    actions.Add(new HorizonCowAction(HorizonActionKind.Update, known.CowId, animal, null));
                }
                else
                {
                    unchanged++;
                }

                continue;
            }

            // Regel 2: Kuh ohne Lely-ID mit genau dieser Ohrmarke.
            if (animal.EarTag is not null
                && byEarTag.TryGetValue(animal.EarTag, out var byTag)
                && string.IsNullOrWhiteSpace(byTag.LelyAnimalId)
                && !used.Contains(byTag.CowId))
            {
                used.Add(byTag.CowId);
                CountGone(byTag, animal);
                actions.Add(new HorizonCowAction(HorizonActionKind.Link, byTag.CowId, animal, null));
                continue;
            }

            // Regel 3: aktives Tier trifft ein Kalb ueber die Halsbandnummer.
            if (!animal.IsGone && animal.CollarNumber > 0
                && calvesByCollar.TryGetValue(animal.CollarNumber, out var calves))
            {
                var calf = calves.FirstOrDefault(c => !used.Contains(c.CowId));
                if (calf is not null)
                {
                    used.Add(calf.CowId);
                    if (animal.EarTag is null)
                    {
                        // Das Kalb bleibt Kalb und bekommt nur die Lely-ID.
                        actions.Add(new HorizonCowAction(HorizonActionKind.Link, calf.CowId, animal, null));
                    }
                    else if (EarTagFree(animal.EarTag, calf))
                    {
                        byEarTag[animal.EarTag] = calf;
                        actions.Add(new HorizonCowAction(HorizonActionKind.Promote, calf.CowId, animal, null));
                    }
                    else
                    {
                        WarnEarTagTaken(animal);
                        actions.Add(new HorizonCowAction(HorizonActionKind.Link, calf.CowId, animal, null));
                    }

                    continue;
                }
            }

            // Regel 4: neues aktives Tier anlegen.
            if (!animal.IsGone)
            {
                Cow created;
                if (animal.EarTag is not null && !byEarTag.ContainsKey(animal.EarTag) && !cowIds.Contains(animal.EarTag))
                {
                    created = new Cow(animal.EarTag, animal.CollarNumber, false);
                    byEarTag[animal.EarTag] = created;
                }
                else
                {
                    // Ohne Ohrmarke oder mit bereits vergebener Ohrmarke: als Kalb mit eigener Cow_ID.
                    if (animal.EarTag is not null)
                        WarnEarTagTaken(animal);
                    created = new Cow(newCowId(), null, animal.CollarNumber, isCalv: true, isGone: false);
                }

                created.LelyAnimalId = animal.AnimalId;
                created.Location = animal.Location;
                created.Group = animal.Group;
                created.Description = animal.Description;
                cowIds.Add(created.CowId);
                actions.Add(new HorizonCowAction(HorizonActionKind.Create, created.CowId, animal, created));
                continue;
            }

            // Regel 5: abgegangenes Tier ohne Gegenstueck.
            skippedGone++;
        }

        // Schutzregel Massenabgang: erst nach dem Planen, weil sie die Aktionen zaehlt.
        var active = cows.Count(c => !c.IsGone);
        if (active >= MassGoneMinActiveCows && goingGone * 2 > active)
            return Rejected($"{goingGone} von {active} aktiven Kühen würden abgehen – abgelehnt.");

        return new HorizonSyncPlan(null, actions, unchanged, skippedGone, warnings);
    }

    private static HorizonSyncPlan Rejected(string reason)
        => new(reason, Array.Empty<HorizonCowAction>(), 0, 0, Array.Empty<string>());

    // Leerzeichen weg, leer wird null - sonst zaehlt " " als Aenderung gegenueber null.
    private static HorizonAnimal Normalize(HorizonAnimal animal) => new()
    {
        AnimalId = animal.AnimalId?.Trim() ?? "",
        CollarNumber = animal.CollarNumber,
        EarTag = Clean(animal.EarTag),
        IsGone = animal.IsGone,
        Location = Clean(animal.Location),
        Group = Clean(animal.Group),
        Description = Clean(animal.Description),
    };

    private static string? Clean(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}
