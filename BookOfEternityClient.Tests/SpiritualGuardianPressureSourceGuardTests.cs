using System.Text.Json;
using BookOfEternityClient.Services;
using Xunit;
using GuardianPressureGroup = BookOfEternityClient.Services.AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext.ExistingPressureComparisonGroup;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the shared source-set and fixed-model DATA guards without a live owner.
/// Contribution records do not establish canonical fixture lawfulness; neither guard issues signed roster authority, permission or an actual basis.
/// </summary>
public sealed class SpiritualGuardianPressureSourceGuardTests
{
    private const string GuardianId = "guardian_source_guard";
    private const string ParentId = "wound_guardian_selected_parent";

    /// <summary>Same-parent non-strain pressure siblings remain allowed; only another matching wound-source realm/ID triggers this guard.</summary>
    /// <param name="change">One finite source identity or matching-coordinate distinction.</param>
    /// <param name="expected">Whether the DATA predicate must identify another matching pressure wound source.</param>
    [Theory]
    [InlineData("same-parent-siblings", false)]
    [InlineData("second-non-strain-pressure-source", true)]
    [InlineData("source-realm-difference", true)]
    [InlineData("unrelated-side", false)]
    [InlineData("unrelated-actor-kind", false)]
    [InlineData("unrelated-actor-id", false)]
    [InlineData("unrelated-operation", false)]
    [InlineData("non-wound-source", false)]
    public void GuardianPressureSourceGuard_DistinguishesParentAndMatchingCoordinates(string change, bool expected)
    {
        var selected = Contribution("selected", ParentId, "spiritual_strain_burden");
        AssertGuard(false, [selected], selected);
        var other = Contribution("other", "wound_guardian_other_parent", "spiritual_roll_hindrance");
        // The independent non-strain pressure row really triggers the guard before any unrelated-coordinate control.
        AssertGuard(true, [selected, other], selected);
        SpiritualWoundConflictContribution[] additions;
        switch (change)
        {
            case "same-parent-siblings":
                other = other with { WoundSource = other.WoundSource with { SourceId = ParentId } };
                additions = [other, Contribution("cost_sibling", ParentId, "spiritual_action_cost_burden")];
                Assert.NotEqual(selected.WoundSource.DefinitionKey, other.WoundSource.DefinitionKey);
                Assert.All(additions, row => Assert.NotEqual("spiritual_strain_burden", row.Profile));
                break;
            case "second-non-strain-pressure-source": additions = [other]; break;
            case "source-realm-difference":
                other = other with { WoundSource = other.WoundSource with { SourceId = ParentId } };
                AssertGuard(false, [selected, other], selected);
                additions = [other with { WoundSource = other.WoundSource with { Realm = "shining_abode" } }];
                break;
            case "unrelated-side": additions = [other with { ResolvedSide = "player" }]; break;
            case "unrelated-actor-kind": additions = [other with { Actor = other.Actor with { Kind = "resident" } }]; break;
            case "unrelated-actor-id": additions = [other with { Actor = other.Actor with { TargetId = "guardian_unrelated" } }]; break;
            case "unrelated-operation":
                additions = [Contribution("other", "wound_guardian_other_parent", "spiritual_roll_hindrance", "guard")];
                break;
            case "non-wound-source": additions = [other with { WoundSource = other.WoundSource with { Kind = "spiritual_art" } }]; break;
            default: throw new ArgumentOutOfRangeException(nameof(change));
        }
        AssertGuard(expected, [selected, .. additions], selected);
    }

    /// <summary>The fixed participant reader accepts only direct_duel model DATA after the complete declaration-bearing baseline passes.</summary>
    /// <param name="model">The exact direct-duel model, another authored model, or absence of the model field.</param>
    [Theory]
    [InlineData("direct_duel")]
    [InlineData("assisted_duel")]
    [InlineData("champion_duel")]
    [InlineData(null)]
    public void GuardianPressureModelGuard_RequiresExactDirectDuel(string? model)
    {
        var fixture = SpiritualGuardianAttributionTestData.Create();
        var fixtureIssues = SpiritualGuardianAttributionDataCheck.Check(fixture.Retained, fixture.Profiles, fixture.Conflict);
        Assert.True(fixtureIssues.Count == 0, JsonSerializer.Serialize(fixtureIssues.Select(issue => new
        {
            issue.FilePath, issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor,
            issue.Section, issue.Expected, issue.Actual, issue.RepairHint, issue.RepairTargetFiles
        })));
        var sourceBefore = fixture.Source.ToJsonString();
        // The full v5 witness validates both actual and retained ordinary exchanges before this DATA reader is called.
        var calculation = SpiritualWoundSourceWitness.ValidateAndReadStrainData(fixture.Source, out var comparison);
        Assert.Equal((15L, 2), (calculation.TraumaPressure, calculation.MaximumSeverityRank));
        Assert.Equal(5, Assert.IsType<SpiritualStrainPreimageData>(comparison).Version);
        var conflictBefore = fixture.Conflict.ToJsonString();
        var ordinaryBefore = fixture.Ordinary.ToJsonString();
        var active = fixture.Conflict["activeConflict"]!.AsObject();
        Assert.True(SpiritualWoundFailedAction.HasDirectDuelModel(active));
        var baseline = SpiritualWoundFailedAction.ReadValidated(active, fixture.Ordinary, 0);
        Assert.Equal(fixture.Source["conflictId"]!.GetValue<string>(), baseline.ConflictId);
        Assert.Equal(fixture.Ordinary["exchangeId"]!.GetValue<string>(), baseline.ExchangeId);
        Assert.Equal((0, SpiritualGuardianAttributionTestData.GuardianId), (baseline.ExchangeOrdinal, baseline.ActorId));
        Assert.Equal(conflictBefore, fixture.Conflict.ToJsonString());

        if (model is null) Assert.True(active.Remove("sideModel"));
        else active["sideModel"] = model;
        var before = fixture.Conflict.ToJsonString();
        Assert.Equal(model == "direct_duel", SpiritualWoundFailedAction.HasDirectDuelModel(active));
        if (model == "direct_duel")
            Assert.Equal(baseline, SpiritualWoundFailedAction.ReadValidated(active, fixture.Ordinary, 0));
        else
        {
            var error = Assert.Throws<FormatException>(() => SpiritualWoundFailedAction.ReadValidated(active, fixture.Ordinary, 0));
            Assert.Equal("Guardian attribution requires the exact direct_duel conflict model.", error.Message);
        }
        Assert.Equal(before, fixture.Conflict.ToJsonString());
        Assert.Equal(ordinaryBefore, fixture.Ordinary.ToJsonString());
        Assert.Equal(sourceBefore, fixture.Source.ToJsonString());
    }

    private static SpiritualWoundConflictContribution Contribution(string label, string parentId, string profile,
        string operation = "pressure")
    {
        var (kind, axis, resolvedAxis, magnitude) = profile switch
        {
            "spiritual_strain_burden" => (SpiritualWoundProjectionKind.StrainBurden, "sideStrain", "oppositionSideStrain",
                JsonSerializer.SerializeToElement(1)),
            "spiritual_roll_hindrance" => (SpiritualWoundProjectionKind.RollHindrance, "rollMode", "rollMode",
                JsonSerializer.SerializeToElement("disadvantage")),
            "spiritual_action_cost_burden" => (SpiritualWoundProjectionKind.ActionCostBurden, "actionCostAudit", "actionCostAudit.opposition",
                JsonSerializer.SerializeToElement(2)),
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };
        return new("effect_guardian_" + label, "component_001",
            new("chaos_sea", "wound", parentId, "definition_guardian_" + label),
            new("chaos_sea", "guardian", GuardianId), "opposition", kind, profile, operation, axis, resolvedAxis,
            magnitude, 0, 1, JsonSerializer.SerializeToElement(new { operation, axis, magnitude }));
    }

    private static void AssertGuard(bool expected, SpiritualWoundConflictContribution[] contributions,
        SpiritualWoundConflictContribution selected)
    {
        var before = Image(contributions);
        var references = contributions.ToArray();
        Assert.Equal(expected, GuardianPressureGroup.HasAnotherGuardianPressureSource(contributions, GuardianId, selected));
        Assert.Equal(before, Image(contributions));
        Assert.Equal(references.Length, contributions.Length);
        for (var index = 0; index < references.Length; index++) Assert.Same(references[index], contributions[index]);
    }

    private static string Image(IReadOnlyList<SpiritualWoundConflictContribution> contributions) =>
        JsonSerializer.Serialize(contributions.Select(row => new
        {
            row.EffectId, row.ComponentId, row.WoundSource, row.Actor, row.ResolvedSide, row.ProjectionKind,
            row.Profile, row.Operation, row.SourceAxis, row.ResolvedAxis, row.Priority, row.CurrentStacks,
            magnitude = row.Magnitude.GetRawText(), payload = row.ProfilePayload.GetRawText()
        }));
}
