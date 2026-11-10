using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Requires the real isolated ordinary evaluator to preserve another applicable burden and never cache its semantic failure.
    /// The pressure roll burden is authored in the genuine birth decision, never patched into an existing signed parent.
    /// </summary>
    [Fact]
    public async Task SpiritualExistingPressure_OrdinaryIsolationStillRejectsOtherApplicableBurden()
        => await WithSignedPressureParentBelowCapAsync([4, 12], "request_pressure_other_burden_44", async context =>
        {
            var parent = await ReadCombatHealingWoundAsync(context);
            var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
            Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
            var pressureStrain = Assert.Single(mechanics.Components, row => row.Profile == "spiritual_strain_burden" &&
                row.Payload.GetProperty("operation").GetString() == "pressure");
            var pressureRoll = Assert.Single(mechanics.Components, row => row.Profile == "spiritual_roll_hindrance" &&
                row.Payload.GetProperty("operation").GetString() == "pressure");
            Assert.Equal("disadvantage", pressureRoll.Payload.GetProperty("magnitude").GetString());
            Assert.Equal(("chaos_sea", "player", "player_soul", 1),
                (pressureRoll.Realm, pressureRoll.TargetKind, pressureRoll.TargetId, pressureRoll.CurrentStacks));
            Assert.Equal(parent.WoundId, pressureRoll.Source!.SourceId);
            Assert.Equal(pressureStrain.EffectId, pressureRoll.EffectId);
            Assert.NotEqual(pressureStrain.ComponentId, pressureRoll.ComponentId);

            var conflict = (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!.AsObject();
            var ordinary = await AppendExistingPressureOrdinaryDraftAsync(context, conflict);
            Assert.Null(ordinary["diceAudit"]?["rollMode"]?["player"]);
            var definitions = (await context.ReadJsonAsync(ResourceMaterializationContract.DefinitionsPath))!.ToJsonString();
            await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, PressureOrdinarySignedPaths);
            Assert.True(signed.Success, string.Join("\n", signed.Issues));
            Assert.Equal((44, "request_pressure_other_burden_44"),
                (signed.Snapshot!.TurnNumber, signed.Snapshot.RequestId));
            Assert.Equal(new[] { 4, 12 }, signed.Snapshot.AcceptedD20EventValues);
            var copy = new PressureOrdinaryOwnerCopy(context,
                Assert.IsType<string>(context.FileSystem.ReadExistingSessionGeneration(lease)), ordinary, conflict, definitions);
            using var capture = await BeginPressureOrdinaryOwnerAsync(copy, lease, "other_burden");
            var files = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
            var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
            var before = await ReadPressureOwnerSnapshotAsync(capture, lease, "other_burden_before");
            AssertPressureOrdinaryFrontier(before, copy, capture, lease);
            var beforeCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(0, beforeCounts.OrdinaryEvaluations);
            Assert.Equal(0, beforeCounts.ActualEvaluations);

            var first = await MeasurePressureOwnerPhaseAsync("ordinary_other_burden_failure", "first",
                () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
            Assert.Null(first.Comparison);
            Assert.False(first.PrefixBlocked);
            var firstIssue = Assert.Single(first.Issues);
            Assert.Equal("spiritual_wound_roll_hindrance_missing", firstIssue.Code);
            Assert.Equal("activeConflict.exchangeLog[2].diceAudit.rollMode.player", firstIssue.FilePath);
            Assert.Equal(IssueSeverity.Error, firstIssue.Severity);
            Assert.True(capture.IsCurrentOwner);
            var firstCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(beforeCounts.Queries + 1, firstCounts.Queries);
            Assert.Equal(beforeCounts.OrdinaryEvaluations + 1, firstCounts.OrdinaryEvaluations);
            Assert.Equal(beforeCounts.ActualEvaluations, firstCounts.ActualEvaluations);
            Assert.True(firstCounts.FreshInputChecks > beforeCounts.FreshInputChecks);
            Assert.True(firstCounts.FreshDependencyChecks > beforeCounts.FreshDependencyChecks);
            AssertPressureOrdinaryFrontiersEqual(before,
                await ReadPressureOwnerSnapshotAsync(capture, lease, "other_burden_after_first_failure"));
            AssertExistingPressureInputInert(context, lease, files, generation);

            var repeated = await MeasurePressureOwnerPhaseAsync("ordinary_other_burden_failure", "second",
                () => capture.ReadExistingPressureOrdinaryComparisonAsync(lease));
            Assert.Null(repeated.Comparison);
            Assert.False(repeated.PrefixBlocked);
            var repeatedIssue = Assert.Single(repeated.Issues);
            Assert.Equal((firstIssue.Code, firstIssue.FilePath, firstIssue.Severity, firstIssue.Expected, firstIssue.Actual),
                (repeatedIssue.Code, repeatedIssue.FilePath, repeatedIssue.Severity, repeatedIssue.Expected, repeatedIssue.Actual));
            Assert.True(capture.IsCurrentOwner);
            var repeatedCounts = capture.ReadExistingStrainReuseCounts();
            Assert.Equal(firstCounts.Queries + 1, repeatedCounts.Queries);
            Assert.Equal(firstCounts.OrdinaryEvaluations + 1, repeatedCounts.OrdinaryEvaluations);
            Assert.Equal(beforeCounts.OrdinaryEvaluations + 2, repeatedCounts.OrdinaryEvaluations);
            Assert.Equal(beforeCounts.ActualEvaluations, repeatedCounts.ActualEvaluations);
            Assert.True(repeatedCounts.FreshInputChecks > firstCounts.FreshInputChecks);
            Assert.True(repeatedCounts.FreshDependencyChecks > firstCounts.FreshDependencyChecks);
            // Signed-parent DATA may be recomputed after failure; only real ordinary evaluator entries prove failure nonreuse.
            AssertPressureOrdinaryFrontiersEqual(before,
                await ReadPressureOwnerSnapshotAsync(capture, lease, "other_burden_after_second_failure"));
            AssertPressureOrdinaryFrontier(before, copy, capture, lease);
            AssertExistingPressureInputInert(context, lease, files, generation);
            foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                         SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
                Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, path));
            capture.Dispose();
            Assert.False(capture.IsCurrentOwner);
            AssertExistingPressureInputInert(context, lease, files, generation);
        }, rollBurdenOperation: PressureParentRollBurdenOperation.Pressure);
}
