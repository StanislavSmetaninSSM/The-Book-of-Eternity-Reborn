using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public async Task NaturalFinalReductionReusesAdmittedDataWithoutChangingPublication()
    {
        var evidence = Path.Combine(TestDiagnosticPaths.Root, "natural-final-reduction", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var timings = new OperationTimingAccumulator();
        using var window = timings.StartWindow();
        string? ownedRoot = null;
        var passed = false;
        HealingFixture? fixture = null;
        Exception? scenarioFailure = null;
        try
        {
            using (timings.Measure("NaturalFinalControl.Setup"))
            fixture = await HealingFixture.CreateAsync(4, 0, 20, stageRequest: false,
                hooks: new FileSystemManagerHooks { PassiveOperationTimings = timings },
                allocatedRootForDiagnostics: root => ownedRoot = root,
                beforeResourceBootstrap: (files, _) => files.WriteFileAtomicAsync(
                    AfterlifeSpiritualConflictState.StatePath, AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString()));
            var before = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            var historyBefore = WoundHistoryState.SerializeCanonical(await fixture.ReadHistoryAsync());
            var (control, projection) = await PrepareLaterNaturalCycleAsync(fixture, 43, "request_natural_final_reduction");
            Assert.NotNull(projection);
            await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
            {
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(before, projection!["destinationGraphs"]!.AsArray()),
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }.ToJsonString());
            await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
            IReadOnlyList<ValidationIssue> issues;
            using (timings.Measure("NaturalFinalControl.CommonPreparation"))
                issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                    .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            AssertNoCleanStartErrors(issues);
            var requests = window.CaptureNamedCounts("SpiritualFinal.Request");
            var reductions = window.CaptureNamedCounts("SpiritualFinal.Reduce");
            Assert.True(requests.Completed > 1, "The real common plan must request more than one detached final replay.");
            Assert.Equal(1, reductions.Completed);
            using (timings.Measure("NaturalFinalControl.Publication"))
                Assert.NotNull(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                    .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
            using var assertionsTiming = timings.Measure("NaturalFinalControl.AssertAndReplay");
            var current = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal((4, 1L), (current.Severity.Rank, current.Recovery.CurrentStepProgress));
            Assert.Equal(before.WoundId, current.WoundId);
            var history = await fixture.ReadHistoryAsync();
            Assert.Equal(2, history.Transitions.Count);
            Assert.True(JsonNode.DeepEquals(JsonNode.Parse(historyBefore)!["transitions"]![0],
                JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!["transitions"]![0]));
            Assert.True(Assert.IsType<SpiritualHealingPersistedResult>(history.Transitions[^1].TransitionResult).IsNatural);
            var manifest = await fixture.ReadManifestAsync(lease);
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control, manifest.SessionId,
                manifest.RequestId, manifest.TurnNumber, path => fixture.Files.ReadFileBytesAsync(lease, path),
                manifest.ManifestPayloadHash, expectedNaturalProjection: projection));
            fixture.AssertResourcesUnchanged();
            passed = true;
        }
        catch (Exception failure) { scenarioFailure = failure; throw; }
        finally
        {
            Exception? cleanupFailure = null;
            try { if (fixture is not null) await fixture.DisposeAsync(); }
            catch (Exception failure) { cleanupFailure = failure; }
            finally { window.Dispose(); }
            var capture = window.Capture();
            var removed = ownedRoot is not null && !Directory.Exists(ownedRoot);
            await File.WriteAllTextAsync(Path.Combine(evidence, "outcome.json"), JsonSerializer.Serialize(new
            { passed, ownedRoot, removed, scenarioFailure = scenarioFailure?.ToString(),
                cleanupFailure = cleanupFailure?.ToString(), timings = capture }));
            if (cleanupFailure is not null)
                throw new AggregateException("Natural reduction fixture cleanup failed.",
                    scenarioFailure is null ? [cleanupFailure] : [scenarioFailure, cleanupFailure]);
            Assert.True(capture.Complete);
            if (ownedRoot is not null) Assert.True(removed);
        }
    }
}
