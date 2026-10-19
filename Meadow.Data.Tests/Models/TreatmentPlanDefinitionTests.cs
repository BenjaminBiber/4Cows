using Meadow.Shared.Models;

namespace Meadow.Data.Tests.Models;

/// <summary>
/// Plan-Definition am Medikament: wann gilt ein Plan als definiert, wie wird er
/// beschrieben, und wann setzt der Server den Stichtag PlanValidFrom.
/// </summary>
public class TreatmentPlanDefinitionTests
{
    private static readonly DateTime Today = new(2026, 6, 15);

    private static Medicine Med(int? doses, TreatmentPlanMode? mode, int? days, DateTime? validFrom = null)
        => new(1, "Synulox") { PlanDoseCount = doses, PlanMode = mode, PlanDays = days, PlanValidFrom = validFrom };

    [Fact]
    public void Validate_accepts_no_plan_at_all()
        => Assert.Null(TreatmentPlanDefinition.Validate(null, null, null));

    [Theory]
    [InlineData(3, null, 5)]
    [InlineData(null, TreatmentPlanMode.Window, 5)]
    [InlineData(3, TreatmentPlanMode.Interval, null)]
    public void Validate_rejects_partial_plan(int? doses, TreatmentPlanMode? mode, int? days)
        => Assert.NotNull(TreatmentPlanDefinition.Validate(doses, mode, days));

    [Theory]
    [InlineData(1, 5)]
    [InlineData(21, 5)]
    [InlineData(3, 0)]
    [InlineData(3, 91)]
    public void Validate_rejects_values_out_of_range(int doses, int days)
        => Assert.NotNull(TreatmentPlanDefinition.Validate(doses, TreatmentPlanMode.Window, days));

    [Fact]
    public void Validate_rejects_unknown_mode()
        => Assert.NotNull(TreatmentPlanDefinition.Validate(3, (TreatmentPlanMode)7, 5));

    [Fact]
    public void IsDefined_requires_complete_valid_plan()
    {
        Assert.True(TreatmentPlanDefinition.IsDefined(Med(3, TreatmentPlanMode.Window, 5)));
        Assert.False(TreatmentPlanDefinition.IsDefined(Med(null, null, null)));
        Assert.False(TreatmentPlanDefinition.IsDefined(Med(1, TreatmentPlanMode.Window, 5)));
    }

    [Theory]
    [InlineData(TreatmentPlanMode.Window, 5, "3× innerhalb von 5 Tagen")]
    [InlineData(TreatmentPlanMode.Window, 1, "3× innerhalb von 1 Tag")]
    [InlineData(TreatmentPlanMode.Interval, 5, "3× alle 5 Tage")]
    [InlineData(TreatmentPlanMode.Interval, 1, "3× täglich")]
    public void Describe_formats_both_modes(TreatmentPlanMode mode, int days, string expected)
        => Assert.Equal(expected, TreatmentPlanDefinition.Describe(Med(3, mode, days)));

    [Fact]
    public void Describe_is_null_without_plan()
        => Assert.Null(TreatmentPlanDefinition.Describe(Med(null, null, null)));

    [Fact]
    public void ResolveValidFrom_sets_today_when_plan_is_new()
        => Assert.Equal(Today.Date,
            TreatmentPlanDefinition.ResolveValidFrom(Med(null, null, null), Med(3, TreatmentPlanMode.Window, 5), Today.AddHours(14)));

    [Fact]
    public void ResolveValidFrom_sets_today_on_insert()
        => Assert.Equal(Today.Date,
            TreatmentPlanDefinition.ResolveValidFrom(null, Med(3, TreatmentPlanMode.Window, 5), Today));

    [Fact]
    public void ResolveValidFrom_keeps_existing_date_when_parameters_change()
    {
        var before = Med(3, TreatmentPlanMode.Window, 5, validFrom: new DateTime(2026, 1, 1));
        var after = Med(4, TreatmentPlanMode.Interval, 7);
        Assert.Equal(new DateTime(2026, 1, 1), TreatmentPlanDefinition.ResolveValidFrom(before, after, Today));
    }

    [Fact]
    public void ResolveValidFrom_clears_date_when_plan_is_removed()
    {
        var before = Med(3, TreatmentPlanMode.Window, 5, validFrom: new DateTime(2026, 1, 1));
        Assert.Null(TreatmentPlanDefinition.ResolveValidFrom(before, Med(null, null, null), Today));
    }

    [Fact]
    public void ResolveValidFrom_ignores_client_supplied_date()
    {
        // Der Client darf den Stichtag nicht setzen - sonst koennte ein Bearbeiten
        // die gesamte Historie nachtraeglich in Plaene verwandeln.
        var after = Med(3, TreatmentPlanMode.Window, 5, validFrom: new DateTime(2020, 1, 1));
        Assert.Equal(Today.Date, TreatmentPlanDefinition.ResolveValidFrom(Med(null, null, null), after, Today));
    }
}
