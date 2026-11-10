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
    public Task InitialCriticalTierFivePublishesFourContinuousSafeCycles() =>
        RunInitialCriticalChronologyAsync(5, [(4, 6), (3, 4), (1, 0), (1, 0)]);

    [Fact]
    public Task InitialCriticalTierZeroPublishesTwentyContinuousSafeCycles() =>
        RunInitialCriticalChronologyAsync(0,
        [
            (4, 1), (4, 2), (4, 3), (4, 4), (4, 5), (4, 6), (4, 7),
            (3, 0), (3, 1), (3, 2), (3, 3), (3, 4), (3, 5),
            (2, 0), (2, 1), (2, 2), (2, 3), (1, 0), (1, 1), (1, 0)
        ]);

    /// <summary>
    /// Exercises persisted chronology, not the already qualified recovery calculator.
    /// Only initial DATA is seeded; every later wound, graph generation and receipt is published.
    /// </summary>
    private static async Task RunInitialCriticalChronologyAsync(int tier, (int Rank, long Progress)[] expected)
    {
        var evidence = Path.Combine(TestDiagnosticPaths.Root, "initial-critical-chronology",
            "tier-" + tier + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var timings = new OperationTimingAccumulator();
        using var window = timings.StartWindow(); // Aggregate-only passive costs; no event-capture claim.
        HealingFixture? fixture = null;
        string? ownedRoot = null;
        var completed = 0;
        var passed = false;
        Exception? scenarioFailure = null;
        try
        {
            using (timings.Measure("Chronology.Setup"))
                fixture = await HealingFixture.CreateAsync(4, tier, 20, stageRequest: false,
                    hooks: new FileSystemManagerHooks { PassiveOperationTimings = timings },
                    allocatedRootForDiagnostics: root => ownedRoot = root,
                    beforeResourceBootstrap: (files, _) => files.WriteFileAtomicAsync(
                        AfterlifeSpiritualConflictState.StatePath, AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString()));
            var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal(4, original.Severity.Rank);
            Assert.Equal(0, original.Recovery.CurrentStepProgress);
            var previousEnvelope = original;
            var previousWound = WoundMaterializationContract.SerializeCanonical(original);
            var previousRows = JsonNode.Parse(WoundHistoryState.SerializeCanonical(await fixture.ReadHistoryAsync()))!
                ["transitions"]!.AsArray();
            Assert.Single(previousRows);
            var expectedEffectIds = original.Consequences.OwnedEffectSources.RootBindings
                .Select(r => r.EffectId).ToHashSet(StringComparer.Ordinal);
            await File.WriteAllTextAsync(Path.Combine(evidence, "initial-wound.json"), previousWound);

            for (var cycle = 1; cycle <= expected.Length; cycle++)
            {
                using var cycleTimer = timings.Measure($"Chronology.Cycle.{cycle:00}");
                var turn = 42 + cycle;
                var requestId = "request_initial_critical_" + tier + "_" + cycle;
                BookOfEternityClient.Models.ProgressionControl control;
                JsonObject? projection;
                using (timings.Measure("Chronology.Prepare"))
                    (control, projection) = await PrepareLaterNaturalCycleAsync(fixture, turn, requestId);
                Assert.NotNull(projection);
                Assert.Equal(cycle, control.NextChaosSeaTurnOrdinal);
                Assert.Equal(1, control.ChaosSeaCyclesExpectedThisTurn);
                Assert.Equal(tier, Assert.Single(projection!["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
                // Retained immutable prior result supplies graph-authoring DATA only;
                // actual preparation/publication still verifies the fresh signed original.
                var before = previousEnvelope;
                using (timings.Measure("Chronology.AuthorRequestedGraphs"))
                    await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
                    {
                        // Fresh response even on a progress-only cycle: the prior response is not new GM input.
                        ["spiritualHealingGraphs"] = AuthorNaturalGraphs(before, projection["destinationGraphs"]!.AsArray()),
                        ["timestamp"] = DateTime.UtcNow.ToString("o")
                    }.ToJsonString());
                var graphFingerprint = new CanonicalBeforeImage(true,
                    (await fixture.Files.ReadFileBytesAsync("output/interface_updates.json"))!).Fingerprint;
                using (timings.Measure("Chronology.Publish"))
                    await PublishPreparedCycleAsync(fixture);

                ProgressionScheduleService.PublishedSpiritualCycleData publishedData;
                string snapshot;
                using (timings.Measure("Chronology.CurrentWitness"))
                await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                {
                    var frame = await fixture.ReadManifestAsync(lease);
                    Assert.Equal(turn, frame.TurnNumber);
                    Assert.Equal(requestId, frame.RequestId);
                    snapshot = frame.ManifestPayloadHash;
                    publishedData = Assert.IsType<ProgressionScheduleService.PublishedSpiritualCycleData>(
                        await ProgressionScheduleService.ReadPublishedSpiritualCycleDataAsync(control,
                            frame.SessionId, requestId, turn, path => fixture.Files.ReadFileBytesAsync(lease, path),
                            snapshot, expectedNaturalProjection: projection));
                }

                using (timings.Measure("Chronology.AssertAndCapture"))
                {
                    var published = publishedData.Result;
                    Assert.True(published.IsNatural);
                    Assert.Equal(graphFingerprint, published.GraphResponseFingerprint);
                    Assert.True(published.MatchesBinding("session_spiritual_healing", requestId, turn));
                    var result = publishedData.Root;
                    Assert.Null(result["request"]);
                    Assert.Equal(snapshot, result["binding"]!["snapshotToken"]!.GetValue<string>());
                    var stages = published.Stages;
                    Assert.Equal(previousWound, WoundMaterializationContract.SerializeCanonical(stages[0].Before));
                    Assert.All(stages, stage =>
                    {
                        Assert.Equal(original.Owner, stage.Before.Owner);
                        Assert.Equal(original.WoundId, stage.Before.WoundId);
                        Assert.Null(stage.AttemptId);
                        Assert.Contains(stage.Kind, new[] { "recover", "heal" });
                    });
                    for (var i = 1; i < stages.Count; i++)
                        Assert.Equal(WoundMaterializationContract.SerializeCanonical(stages[i - 1].After),
                            WoundMaterializationContract.SerializeCanonical(stages[i].Before));
                    var final = stages[^1].After;
                    Assert.Equal(expected[cycle - 1].Rank, final.Severity.Rank);
                    Assert.Equal(expected[cycle - 1].Progress, final.Recovery.CurrentStepProgress);
                    Assert.Equal(cycle == expected.Length, stages[^1].Terminal);
                    Assert.Equal(AfterlifeWoundTreatmentAuthority.CycleKey("chaos_sea", cycle), final.Recovery.LastTickKey);
                    Assert.Equal(cycle == expected.Length ? "healed" : "active", final.Lifecycle);

                    var history = publishedData.History;
                    var historyJson = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!.AsObject();
                    var rows = historyJson["transitions"]!.AsArray();
                    Assert.Equal(previousRows.Count + stages.Count, rows.Count);
                    for (var i = 0; i < previousRows.Count; i++)
                        Assert.True(JsonNode.DeepEquals(previousRows[i], rows[i]), "Published cycle rewrote an earlier History row.");
                    var appended = history.Transitions.Skip(previousRows.Count).ToArray();
                    Assert.Equal(stages.Count, appended.Length);
                    Assert.Equal(published.ResultFingerprint,
                        Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(appended,
                            row => row.TransitionResult is not null).TransitionResult).ResultFingerprint);
                    for (var i = 0; i < stages.Count; i++)
                    {
                        var stage = stages[i]; var row = appended[i];
                        Assert.Equal(turn, row.Turn);
                        Assert.Equal(original.WoundId, row.WoundId);
                        Assert.Equal(stage.TransitionId, row.TransitionId);
                        Assert.Equal(stage.After.LastTransition.Ordinal, row.WoundTransitionOrdinal);
                        Assert.Equal(stage.OperationKey, row.OperationKey);
                        Assert.Equal(stage.EventRef, row.EventRef);
                        Assert.Equal(stage.CycleKey, row.CycleKey);
                        Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.Before), row.BeforeFingerprint);
                        Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.After), row.AfterFingerprint);
                        Assert.Equal(stage.Terminal, row.Terminal);
                    }
                    Assert.Equal(cycle, history.Transitions.Count(row => row.TransitionResult is SpiritualHealingPersistedResult));
                    Assert.Equal(cycle == expected.Length ? 1 : 0, history.Transitions.Count(row => row.Terminal));
                    Assert.DoesNotContain(history.Transitions, row => row.AttemptId is not null || row.Kind == "treat");

                    var profiles = Assert.IsType<JsonObject>(publishedData.CarrierRoots[WoundCarrierCatalog.AfterlifeProfilesPath]);
                    var identities = JsonNode.Parse(WoundIdentityState.SerializeCanonical(publishedData.Identities))!.AsObject();
                    var effects = JsonNode.Parse((await fixture.Files.ReadFileAsync(EffectIdentityState.StatePath))!)!.AsObject();
                    var catalog = publishedData.Catalog;
                    var mechanics = EffectMechanicsSnapshot.Build(
                        EffectCarrierCatalog.Build(new(null, null, null, null, profiles, null)), effects, null, out var effectIndex);
                    Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues.Select(issue => issue.Actual)));
                    var identity = Assert.Single(publishedData.Identities.Entries);
                    Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(final), identity.SemanticFingerprint);
                    Assert.Equal(final.Lifecycle, identity.Status);
                    Assert.Equal(final.LastTransition.Ordinal, identity.LastTransitionOrdinal);
                    Assert.Equal(cycle == expected.Length ? final.LastTransition.TransitionId : null, identity.TerminalTransitionId);
                    if (cycle == expected.Length) Assert.Empty(catalog.Occurrences);
                    else Assert.Equal(WoundMaterializationContract.SerializeCanonical(final),
                        WoundMaterializationContract.SerializeCanonical(Assert.Single(catalog.Occurrences).Wound));

                    var activeEffects = cycle == expected.Length ? new HashSet<string>(StringComparer.Ordinal)
                        : final.Consequences.OwnedEffectSources.RootBindings.Select(root => root.EffectId).ToHashSet(StringComparer.Ordinal);
                    foreach (var stage in stages)
                        expectedEffectIds.UnionWith(stage.After.Consequences.OwnedEffectSources.RootBindings.Select(root => root.EffectId));
                    Assert.NotNull(effectIndex);
                    Assert.True(activeEffects.SetEquals(mechanics.Effects.Select(effect => effect.EffectId)),
                        "Current effect carrier differs from the exact surviving wound generation.");
                    foreach (var effectId in expectedEffectIds)
                    {
                        Assert.True(effectIndex!.TryGetEntry(effectId, out var entry));
                        Assert.Equal(activeEffects.Contains(effectId) ? "active" : "expired", entry.State);
                    }
                    var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
                    Assert.Equal(cycle, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                    Assert.Equal(cycle, schedule["lastChaosSeaSimulationOrdinal"]!.GetValue<int>());
                    Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
                    Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                    fixture.AssertResourcesUnchanged();
                    await File.WriteAllTextAsync(Path.Combine(evidence, $"cycle-{cycle:00}.json"), new JsonObject
                    {
                        ["cycle"] = cycle, ["requestId"] = requestId, ["snapshotToken"] = snapshot,
                        ["projection"] = projection.DeepClone(), ["result"] = result,
                        ["history"] = historyJson.DeepClone(), ["profiles"] = profiles,
                        ["identities"] = identities, ["effects"] = effects, ["schedule"] = schedule
                    }.ToJsonString());
                    previousRows = rows.DeepClone().AsArray();
                    previousWound = WoundMaterializationContract.SerializeCanonical(final);
                    previousEnvelope = final;
                    completed = cycle;
                }

                if (cycle == 1 || cycle == expected.Length)
                {
                    using var replayTimer = timings.Measure("Chronology.CurrentReplay");
                    var stable = fixture.CaptureMutationRoots(["output/interface_updates.json"]);
                    var scheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
                    Assert.DoesNotContain(await scheduler.ValidateAcceptedTurnOutcomeAsync(control), issue => issue.Severity == IssueSeverity.Error);
                    await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
                    await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
                    fixture.AssertMutationRootsUnchanged(stable);
                }
            }

            using (timings.Measure("Chronology.EmptyFollowingCycle"))
            {
                Assert.Equal(tier == 0 ? 22 : 7, previousRows.Count);
                var stablePaths = new[] { WoundHistoryState.HistoryPath, WoundIdentityState.StatePath,
                    EffectIdentityState.StatePath, WoundCarrierCatalog.AfterlifeProfilesPath };
                var terminalState = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var path in stablePaths) terminalState.Add(path, (await fixture.Files.ReadFileBytesAsync(path))!);
                var (control, projection) = await PrepareLaterNaturalCycleAsync(fixture, 43 + expected.Length,
                    "request_initial_critical_" + tier + "_empty");
                Assert.Null(projection);
                await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
                { ["spiritualHealingGraphs"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o") }.ToJsonString());
                await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                {
                    Assert.DoesNotContain(await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                        .PrepareSpiritualSelfTreatmentPublicationAsync(lease), issue => issue.Severity == IssueSeverity.Error);
                    Assert.Null(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                        .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
                }
                await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
                    .ApplyAcceptedTurnOutcomeAsync(control);
                foreach (var pair in terminalState)
                    Assert.Equal(pair.Value, await fixture.Files.ReadFileBytesAsync(pair.Key));
                Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
                Assert.Equal(expected.Length + 1, JsonNode.Parse((await fixture.Files.ReadFileAsync(
                    ProgressionScheduleService.SchedulePath))!)!["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                fixture.AssertResourcesUnchanged();
            }
            passed = true;
        }
        catch (Exception error)
        {
            scenarioFailure = error;
            throw;
        }
        finally
        {
            Exception? cleanupFailure = null;
            try
            {
                using (timings.Measure("Chronology.Cleanup"))
                    if (fixture is not null) await fixture.DisposeAsync();
            }
            catch (Exception error) { cleanupFailure = error; }
            finally { window.Dispose(); }
            var capture = window.Capture();
            var removed = ownedRoot is not null && !Directory.Exists(ownedRoot);
            await File.WriteAllTextAsync(Path.Combine(evidence, "outcome.json"), JsonSerializer.Serialize(new
            { tier, expectedCycles = expected.Length, completedCycles = completed, passed, ownedRoot, removed,
                scenarioFailure = scenarioFailure?.ToString(), cleanupFailure = cleanupFailure?.ToString(), timings = capture }));
            if (cleanupFailure is not null)
                throw new AggregateException("Chronology cleanup failed; all observed failures are retained.",
                    scenarioFailure is null ? [cleanupFailure] : [scenarioFailure, cleanupFailure]);
            Assert.True(capture.Complete);
            if (ownedRoot is not null) Assert.True(removed);
        }
    }
}
