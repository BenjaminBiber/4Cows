using Meadow.Shared.Lookups;
using Meadow.Shared.Models;
using Meadow.Shared.Models.Horizon;

namespace Meadow.Data.Tests.Lookups;

/// <summary>
/// Der Abgleich zwischen Horizon und Meadow.
///
/// Er zaehlt, weil jede falsche Zuordnung eine Behandlungshistorie an das
/// falsche Tier haengt und niemand im Stall das sehen kann: ein Kalb mit der
/// Lely-ID seines Nachbarn sieht aus wie ein Kalb. Die Schutzregel verhindert
/// zusaetzlich, dass ein halb gelesener Snapshot die halbe Herde abgehen laesst.
/// </summary>
public class HorizonSyncPlannerTests
{
    // Eine identifizierte Kuh: die Ohrmarke ist zugleich ihre Cow_ID.
    private static Cow Tagged(string earTag, int collar, bool isGone = false)
        => new(earTag, collar, isGone);

    // Ein Kalb: GUID als Cow_ID, noch keine Ohrmarke.
    private static Cow Calf(string cowId, int collar, bool isGone = false)
        => new(cowId, null, collar, isCalv: true, isGone: isGone);

    private static Cow WithLely(Cow cow, string lelyId)
    {
        cow.LelyAnimalId = lelyId;
        return cow;
    }

    private static HorizonAnimal Animal(
        string animalId,
        int collar = 0,
        string? earTag = null,
        bool isGone = false,
        string? location = null,
        string? group = null,
        string? description = null)
        => new()
        {
            AnimalId = animalId,
            CollarNumber = collar,
            EarTag = earTag,
            IsGone = isGone,
            Location = location,
            Group = group,
            Description = description,
        };

    private static HorizonSnapshot Snapshot(params HorizonAnimal[] animals)
        => new() { AgentVersion = "1.0", MachineName = "TEST", Animals = animals.ToList() };

    private static HorizonSyncPlan Plan(Cow[] cows, params HorizonAnimal[] animals)
        => HorizonSyncPlanner.Plan(cows, Snapshot(animals), () => "guid-1");

    [Fact]
    public void Empty_snapshot_is_rejected()
    {
        var plan = Plan([Tagged("1001", 11)]);

        Assert.Equal("Der Snapshot enthält keine Tiere.", plan.RejectedReason);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Duplicate_animal_id_is_rejected()
    {
        var plan = Plan([], Animal("A1", 11), Animal("A1", 12));

        Assert.Equal("AnimalId A1 kommt mehrfach vor.", plan.RejectedReason);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Blank_animal_id_is_rejected()
    {
        var plan = Plan([], Animal("  ", 11));

        Assert.Equal("Ein Tier ohne AnimalId.", plan.RejectedReason);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Known_lely_id_updates_changed_fields_only()
    {
        var cow = WithLely(Tagged("1001", 11), "A1");
        cow.Location = "Stall 1";

        var changed = Plan([cow], Animal("A1", 12, earTag: "1001", location: "Stall 2"));

        var action = Assert.Single(changed.Actions);
        Assert.Equal(HorizonActionKind.Update, action.Kind);
        Assert.Equal("1001", action.CowId);
        Assert.Equal(0, changed.Unchanged);

        var same = Plan([cow], Animal("A1", 11, earTag: "1001", location: "Stall 1"));

        Assert.Empty(same.Actions);
        Assert.Equal(1, same.Unchanged);
    }

    [Fact]
    public void Known_lely_id_wins_even_after_ear_tag_and_collar_changed()
    {
        var byLely = WithLely(Tagged("1001", 11), "A1");
        var byEarTag = Tagged("2002", 22);

        var plan = Plan([byLely, byEarTag], Animal("A1", 22, earTag: "2002"));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Update, action.Kind);
        Assert.Equal("1001", action.CowId);
    }

    [Fact]
    public void Cow_without_lely_id_is_linked_by_ear_tag_once()
    {
        var cow = Tagged("1001", 11);

        var plan = Plan([cow], Animal("A1", 11, earTag: "1001"));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Link, action.Kind);
        Assert.Equal("1001", action.CowId);
    }

    [Fact]
    public void Link_by_ear_tag_ignores_cows_that_already_have_a_lely_id()
    {
        var cow = WithLely(Tagged("1001", 11), "A9");

        var plan = Plan([cow], Animal("A1", 11, earTag: "1001"));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Create, action.Kind);
        Assert.NotNull(action.NewCow);
    }

    [Fact]
    public void Active_animal_promotes_calf_by_collar_number()
    {
        var calf = Calf("guid-alt", 11);

        var plan = Plan([calf], Animal("A1", 11, earTag: "1001"));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Promote, action.Kind);
        Assert.Equal("guid-alt", action.CowId);
    }

    [Fact]
    public void Calf_is_linked_but_not_promoted_when_horizon_has_no_ear_tag()
    {
        var calf = Calf("guid-alt", 11);

        var plan = Plan([calf], Animal("A1", 11));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Link, action.Kind);
        Assert.Equal("guid-alt", action.CowId);
    }

    [Fact]
    public void Gone_animal_never_promotes_a_calf()
    {
        var calf = Calf("guid-alt", 11);

        var plan = Plan([calf], Animal("A1", 11, earTag: "1001", isGone: true));

        Assert.Empty(plan.Actions);
        Assert.Equal(1, plan.SkippedGone);
    }

    [Fact]
    public void New_active_animal_with_ear_tag_is_created_with_ear_tag_as_cow_id()
    {
        var plan = Plan([], Animal("A1", 11, earTag: "1001", location: "Stall 1", group: "Gruppe 2"));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Create, action.Kind);
        Assert.Equal("1001", action.CowId);
        Assert.NotNull(action.NewCow);
        Assert.Equal("1001", action.NewCow!.CowId);
        Assert.Equal("1001", action.NewCow.EarTagNumber);
        Assert.False(action.NewCow.IsCalv);
        Assert.Equal(11, action.NewCow.CollarNumber);
        Assert.Equal("A1", action.NewCow.LelyAnimalId);
        Assert.Equal("Stall 1", action.NewCow.Location);
        Assert.Equal("Gruppe 2", action.NewCow.Group);
    }

    [Fact]
    public void New_active_animal_without_ear_tag_is_created_as_calf_with_guid()
    {
        var plan = Plan([], Animal("A1", 11));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Create, action.Kind);
        Assert.Equal("guid-1", action.CowId);
        Assert.NotNull(action.NewCow);
        Assert.Null(action.NewCow!.EarTagNumber);
        Assert.True(action.NewCow.IsCalv);
    }

    [Fact]
    public void Gone_animal_without_counterpart_is_skipped()
    {
        var plan = Plan([], Animal("A1", 11, earTag: "1001", isGone: true));

        Assert.Empty(plan.Actions);
        Assert.Equal(1, plan.SkippedGone);
        Assert.Null(plan.RejectedReason);
    }

    [Fact]
    public void IsGone_is_taken_over_in_both_directions()
    {
        var active = WithLely(Tagged("1001", 11), "A1");
        var gone = WithLely(Tagged("2002", 22, isGone: true), "A2");

        var plan = HorizonSyncPlanner.Plan(
            [active, gone, Tagged("3003", 33), Tagged("4004", 44), Tagged("5005", 55)],
            Snapshot(Animal("A1", 11, earTag: "1001", isGone: true), Animal("A2", 22, earTag: "2002")),
            () => "guid-1");

        Assert.Null(plan.RejectedReason);
        Assert.Equal(2, plan.Actions.Count);
        Assert.All(plan.Actions, a => Assert.Equal(HorizonActionKind.Update, a.Kind));
    }

    [Fact]
    public void Meadow_cow_missing_from_snapshot_gets_no_action()
    {
        var known = WithLely(Tagged("1001", 11), "A1");
        var unknown = Tagged("2002", 22);

        var plan = Plan([known, unknown], Animal("A1", 11, earTag: "1001"));

        Assert.Empty(plan.Actions);
        Assert.Equal(1, plan.Unchanged);
    }

    [Fact]
    public void Location_group_description_are_taken_over_and_null_clears_them()
    {
        var cow = WithLely(Tagged("1001", 11), "A1");
        cow.Location = "Stall 1";
        cow.Group = "Gruppe 1";
        cow.Description = "alt";

        var plan = Plan([cow], Animal("A1", 11, earTag: "1001", location: "  "));

        var action = Assert.Single(plan.Actions);
        Assert.Equal(HorizonActionKind.Update, action.Kind);
        Assert.Null(action.Animal.Location);
        Assert.Null(action.Animal.Group);
        Assert.Null(action.Animal.Description);
    }

    [Fact]
    public void Missing_ear_tag_in_horizon_does_not_clear_meadow_ear_tag()
    {
        var cow = WithLely(Tagged("1001", 11), "A1");

        var plan = Plan([cow], Animal("A1", 11));

        Assert.Empty(plan.Actions);
        Assert.Equal(1, plan.Unchanged);
    }

    [Fact]
    public void Mass_gone_above_half_of_at_least_ten_active_cows_is_rejected()
    {
        var cows = new List<Cow>();
        var animals = new List<HorizonAnimal>();
        for (var i = 0; i < 10; i++)
        {
            var tag = $"10{i:00}";
            cows.Add(WithLely(Tagged(tag, 10 + i), $"A{i}"));
            animals.Add(Animal($"A{i}", 10 + i, earTag: tag, isGone: i < 6));
        }

        var plan = HorizonSyncPlanner.Plan(cows, Snapshot(animals.ToArray()), () => "guid-1");

        Assert.Equal("6 von 10 aktiven Kühen würden abgehen – abgelehnt.", plan.RejectedReason);
        Assert.Empty(plan.Actions);
    }

    [Fact]
    public void Mass_gone_below_ten_active_cows_is_allowed()
    {
        var cows = new List<Cow>();
        var animals = new List<HorizonAnimal>();
        for (var i = 0; i < 9; i++)
        {
            var tag = $"10{i:00}";
            cows.Add(WithLely(Tagged(tag, 10 + i), $"A{i}"));
            animals.Add(Animal($"A{i}", 10 + i, earTag: tag, isGone: true));
        }

        var plan = HorizonSyncPlanner.Plan(cows, Snapshot(animals.ToArray()), () => "guid-1");

        Assert.Null(plan.RejectedReason);
        Assert.Equal(9, plan.Actions.Count);
    }

    [Fact]
    public void Ear_tag_already_used_by_other_cow_is_not_taken_over_and_warned()
    {
        var calf = WithLely(Calf("guid-alt", 11), "A1");
        var other = Tagged("1001", 22);

        var plan = Plan([calf, other], Animal("A1", 11, earTag: "1001"));

        Assert.Empty(plan.Actions);
        Assert.Equal(1, plan.Unchanged);
        var warning = Assert.Single(plan.Warnings);
        Assert.Equal("Ohrmarke 1001 von Tier A1 ist bereits vergeben – nicht übernommen.", warning);
    }

    [Fact]
    public void Two_animals_never_match_the_same_meadow_cow()
    {
        var calf = Calf("guid-alt", 11);

        var plan = Plan([calf], Animal("A1", 11), Animal("A2", 11));

        Assert.Equal(2, plan.Actions.Count);
        Assert.Equal(HorizonActionKind.Link, plan.Actions[0].Kind);
        Assert.Equal("guid-alt", plan.Actions[0].CowId);
        Assert.Equal(HorizonActionKind.Create, plan.Actions[1].Kind);
        Assert.Equal("guid-1", plan.Actions[1].CowId);
    }
}
