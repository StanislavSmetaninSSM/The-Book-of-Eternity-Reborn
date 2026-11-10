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
    /// <summary>
    /// Measures one real signed publication over retained late-History DATA and a newly bootstrapped resource lineage.
    /// This independent fixture is neither restoration nor continuation of the interrupted twenty-cycle scenario.
    /// </summary>
    [Fact]
    public async Task LateHistoryWithNewResourcesPublishesOneFreshSignedCycle()
    {
        const string lineage = "independent-new-resource-lineage";
        var evidence = Path.Combine(RequireLateHistoryDiagnosticRoot(), "late-history-actual",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var timings = new OperationTimingAccumulator();
        using var window = timings.StartWindow();
        HealingFixture? fixture = null;
        string? ownedRoot = null;
        var passed = false;
        var publishedCycles = 0;
        Exception? scenarioFailure = null;
        try
        {
            // The shared loader verifies the immutable external capture before it becomes initial DATA.
            var seed = await ReadLateHistoryDiagnosticCaptureAsync();
            Assert.Equal(17, seed["cycle"]!.GetValue<int>());
            var capturedPlayer = Assert.Single(seed["profiles"]!["profiles"]!.AsArray().OfType<JsonObject>());
            Assert.Equal("player_soul", capturedPlayer["actorId"]!.GetValue<string>());
            var previousRows = seed["history"]!["transitions"]!.AsArray();
            Assert.Equal(18, previousRows.Count);
            var priorSnapshot = seed["snapshotToken"]!.GetValue<string>();
            var scheduleJson = seed["result"]!["scheduleAfterJson"]!.GetValue<string>();
            await File.WriteAllTextAsync(Path.Combine(evidence, "seed-data.json"), seed.ToJsonString());

            using (timings.Measure("LateHistory.SetupFreshResources"))
                fixture = await HealingFixture.CreateAsync(4, 0, 20, stageRequest: false,
                    hooks: new FileSystemManagerHooks { PassiveOperationTimings = timings },
                    allocatedRootForDiagnostics: root => ownedRoot = root,
                    beforeResourceBootstrap: async (files, profiles) =>
                    {
                        using var import = timings.Measure("LateHistory.ImportSeed");
                        var player = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>());
                        Assert.Equal("player_soul", player["actorId"]!.GetValue<string>());
                        Assert.False(player.ContainsKey(AfterlifeEntityProfileState.ResourceOwnerBindingsProperty));
                        var comparedOwnerFields = AssertLateHistoryInitialOwnerData(capturedPlayer, player);
                        await File.WriteAllTextAsync(Path.Combine(evidence, "owner-data-comparison.json"), JsonSerializer.Serialize(new
                        {
                            lineage, comparedOwnerFields, capturedResourceBindingImported = false,
                            deliberateDifference = "Resource operation/transition identities and authority are created by one new ordinary bootstrap."
                        }));
                        // Copy only wound/effect DATA. The captured client-owned resource binding is never imported,
                        // removed or resealed. All other fresh owner fields retain their ordinary fixture values.
                        player["activeWounds"] = capturedPlayer["activeWounds"]!.DeepClone();
                        player["activeEffects"] = capturedPlayer["activeEffects"]!.DeepClone();
                        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
                        await files.WriteFileAtomicAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
                        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, seed["identities"]!.ToJsonString());
                        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, seed["history"]!.ToJsonString());
                        await files.WriteFileAtomicAsync(lease, EffectIdentityState.StatePath, seed["effects"]!.ToJsonString());
                        // Preserve the exact decoded scheduler representation, including its original timestamp.
                        // The ordinary writer supplies the UTF-8 BOM used by CanonicalBeforeImage framing.
                        await files.WriteFileAtomicAsync(lease, ProgressionScheduleService.SchedulePath, scheduleJson);
                    });

            WoundMaterializationEnvelope before;
            HashSet<string> expectedEffectIds;
            Dictionary<string, byte[]?> resources;
            using (timings.Measure("LateHistory.ImportSeed"))
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            {
                var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease,
                    WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                var player = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>());
                Assert.True(JsonNode.DeepEquals(capturedPlayer["activeWounds"], player["activeWounds"]));
                Assert.True(JsonNode.DeepEquals(capturedPlayer["activeEffects"], player["activeEffects"]));
                Assert.NotNull(player[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]);
                AssertLateHistoryInitialOwnerData(capturedPlayer, player);
                var identities = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundIdentityState.StatePath))!)!.AsObject();
                var history = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundHistoryState.HistoryPath))!)!.AsObject();
                var effects = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, EffectIdentityState.StatePath))!)!.AsObject();
                Assert.True(JsonNode.DeepEquals(seed["identities"], identities));
                Assert.True(JsonNode.DeepEquals(seed["history"], history));
                Assert.True(JsonNode.DeepEquals(seed["effects"], effects));
                using (fixture.Files.EnterPassiveOperationTimingContext())
                {
                    before = Assert.Single(RequireCurrentAgreement(profiles, identities, history).Occurrences).Wound;
                    Assert.Equal((2, 3L), (before.Severity.Rank, before.Recovery.CurrentStepProgress));
                    Assert.Equal("active", before.Lifecycle);
                    Assert.Equal(AfterlifeWoundTreatmentAuthority.CycleKey("chaos_sea", 17), before.Recovery.LastTickKey);
                    expectedEffectIds = effects["entries"]!.AsArray().OfType<JsonObject>()
                        .Select(entry => entry["effectId"]!.GetValue<string>()).ToHashSet(StringComparer.Ordinal);
                    AssertLateHistoryEffects(profiles, effects, before, expectedEffectIds);
                }
                var scheduleBytes = Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync(lease,
                    ProgressionScheduleService.SchedulePath));
                Assert.Equal(scheduleJson, CanonicalJsonUtf8.DecodeOneOptionalBom(scheduleBytes));
                Assert.Equal(seed["result"]!["scheduleAfterFingerprint"]!.GetValue<string>(),
                    new CanonicalBeforeImage(true, scheduleBytes).Fingerprint);
                Assert.True(JsonNode.DeepEquals(seed["schedule"], JsonNode.Parse(scheduleJson)));
                resources = await AssertLateHistoryResourceAuthorityAsync(fixture.Files, lease);
                fixture.AssertResourcesUnchanged();
            }
            await File.WriteAllTextAsync(Path.Combine(evidence, "fresh-resource-quartet.json"),
                JsonSerializer.Serialize(resources));

            const int turn = 60;
            var requestId = "request_late_history_new_resources_" + Guid.NewGuid().ToString("N");
            BookOfEternityClient.Models.ProgressionControl control;
            JsonObject? projection;
            string graphFingerprint;
            using (timings.Measure("LateHistory.PrepareTurn"))
            {
                (control, projection) = await PrepareLaterNaturalCycleAsync(fixture, turn, requestId);
                Assert.NotNull(projection);
                Assert.Equal(18, control.NextChaosSeaTurnOrdinal);
                Assert.Equal(1, control.ChaosSeaCyclesExpectedThisTurn);
                Assert.Equal(0, Assert.Single(projection!["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
                await fixture.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
                {
                    ["spiritualHealingGraphs"] = AuthorNaturalGraphs(before, projection["destinationGraphs"]!.AsArray()),
                    ["timestamp"] = DateTime.UtcNow.ToString("o")
                }.ToJsonString());
                graphFingerprint = new CanonicalBeforeImage(true,
                    Assert.IsType<byte[]>(await fixture.Files.ReadFileBytesAsync("output/interface_updates.json"))).Fingerprint;
                fixture.AssertMutationRootsUnchanged(resources);
            }

            string snapshot;
            await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            {
                var manifest = await fixture.ReadManifestAsync(lease);
                Assert.Equal(turn, manifest.TurnNumber);
                Assert.Equal(requestId, manifest.RequestId);
                Assert.NotEqual(seed["requestId"]!.GetValue<string>(), manifest.RequestId);
                snapshot = manifest.ManifestPayloadHash;
                Assert.NotEqual(priorSnapshot, snapshot);
                IReadOnlyList<ValidationIssue> issues;
                using (timings.Measure("LateHistory.CommonPreparation"))
                    issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                        .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
                AssertNoCleanStartErrors(issues);
                fixture.AssertMutationRootsUnchanged(resources);
                using (timings.Measure("LateHistory.Publication"))
                    Assert.NotNull(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                        .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
                publishedCycles = 1;
            }

            using (timings.Measure("LateHistory.AssertAndReplay"))
            {
                ProgressionScheduleService.PublishedSpiritualCycleData publishedData;
                await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                {
                    var manifest = await fixture.ReadManifestAsync(lease);
                    Assert.Equal(snapshot, manifest.ManifestPayloadHash);
                    publishedData = Assert.IsType<ProgressionScheduleService.PublishedSpiritualCycleData>(
                        await ProgressionScheduleService.ReadPublishedSpiritualCycleDataAsync(control,
                            manifest.SessionId, requestId, turn, path => fixture.Files.ReadFileBytesAsync(lease, path),
                            snapshot, expectedNaturalProjection: projection));
                }
                var published = publishedData.Result;
                Assert.True(published.IsNatural);
                Assert.True(published.MatchesBinding("session_spiritual_healing", requestId, turn));
                Assert.Equal(graphFingerprint, published.GraphResponseFingerprint);
                Assert.Null(publishedData.Root["request"]);
                Assert.Equal(snapshot, publishedData.Root["binding"]!["snapshotToken"]!.GetValue<string>());
                var stage = Assert.Single(published.Stages);
                Assert.Equal(WoundMaterializationContract.SerializeCanonical(before),
                    WoundMaterializationContract.SerializeCanonical(stage.Before));
                Assert.Equal(before.Owner, stage.After.Owner);
                Assert.Equal(before.WoundId, stage.After.WoundId);
                Assert.Null(stage.AttemptId);
                Assert.Equal("recover", stage.Kind);
                Assert.False(stage.Terminal);
                var final = stage.After;
                Assert.Equal((1, 0L), (final.Severity.Rank, final.Recovery.CurrentStepProgress));
                Assert.Equal("active", final.Lifecycle);
                Assert.Equal(AfterlifeWoundTreatmentAuthority.CycleKey("chaos_sea", 18), final.Recovery.LastTickKey);
                var history = publishedData.History;
                var historyJson = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!.AsObject();
                var rows = historyJson["transitions"]!.AsArray();
                Assert.Equal(19, rows.Count);
                for (var i = 0; i < previousRows.Count; i++)
                    Assert.True(JsonNode.DeepEquals(previousRows[i], rows[i]), "Independent publication rewrote imported History DATA.");
                var appended = Assert.Single(history.Transitions.Skip(previousRows.Count));
                Assert.Equal(published.ResultFingerprint,
                    Assert.IsType<SpiritualHealingPersistedResult>(appended.TransitionResult).ResultFingerprint);
                Assert.Equal(turn, appended.Turn);
                Assert.Equal(before.WoundId, appended.WoundId);
                Assert.Equal(stage.TransitionId, appended.TransitionId);
                Assert.Equal(stage.After.LastTransition.Ordinal, appended.WoundTransitionOrdinal);
                Assert.Equal(stage.OperationKey, appended.OperationKey);
                Assert.Equal(stage.EventRef, appended.EventRef);
                Assert.Equal(stage.CycleKey, appended.CycleKey);
                Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.Before), appended.BeforeFingerprint);
                Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.After), appended.AfterFingerprint);
                Assert.Equal(stage.Terminal, appended.Terminal);
                Assert.Equal(18, history.Transitions.Count(row => row.TransitionResult is SpiritualHealingPersistedResult));
                Assert.DoesNotContain(history.Transitions, row => row.Terminal || row.AttemptId is not null || row.Kind == "treat");
                var identity = Assert.Single(publishedData.Identities.Entries);
                Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(final), identity.SemanticFingerprint);
                Assert.Equal(final.Lifecycle, identity.Status);
                Assert.Equal(final.LastTransition.Ordinal, identity.LastTransitionOrdinal);
                Assert.Null(identity.TerminalTransitionId);
                Assert.Equal(WoundMaterializationContract.SerializeCanonical(final),
                    WoundMaterializationContract.SerializeCanonical(Assert.Single(publishedData.Catalog.Occurrences).Wound));
                var profiles = Assert.IsType<JsonObject>(publishedData.CarrierRoots[WoundCarrierCatalog.AfterlifeProfilesPath]);
                var effects = JsonNode.Parse((await fixture.Files.ReadFileAsync(EffectIdentityState.StatePath))!)!.AsObject();
                expectedEffectIds.UnionWith(final.Consequences.OwnedEffectSources.RootBindings.Select(binding => binding.EffectId));
                using (fixture.Files.EnterPassiveOperationTimingContext())
                    AssertLateHistoryEffects(profiles, effects, final, expectedEffectIds);
                var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
                Assert.Equal(18, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                Assert.Equal(18, schedule["lastChaosSeaSimulationOrdinal"]!.GetValue<int>());
                Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
                Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
                Assert.Null(await fixture.Files.ReadFileBytesAsync(ResourceMaterializationContract.CommandPath));
                Assert.Null(await fixture.Files.ReadFileBytesAsync(EffectAcceptedTurnPlan.CommandPath));
                Dictionary<string, byte[]?> publishedResources;
                await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
                {
                    publishedResources = await AssertLateHistoryResourceAuthorityAsync(fixture.Files, lease);
                    // Ordinary publication changes compact bootstrap formatting to pretty JSON. Compare complete
                    // roots, including every resource identity and chronology field, not selected balances.
                    foreach (var pair in resources)
                        Assert.True(JsonNode.DeepEquals(
                            JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(pair.Value!)),
                            JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(publishedResources[pair.Key]!))),
                            "Independent publication changed canonical resource DATA: " + pair.Key);
                }
                fixture.AssertResourcesUnchanged();
                await File.WriteAllTextAsync(Path.Combine(evidence, "published-resource-quartet.json"),
                    JsonSerializer.Serialize(publishedResources));
                await File.WriteAllTextAsync(Path.Combine(evidence, "published-independent-cycle.json"), new JsonObject
                {
                    ["lineage"] = lineage, ["cycle"] = 18, ["requestId"] = requestId, ["snapshotToken"] = snapshot,
                    ["projection"] = projection!.DeepClone(), ["result"] = publishedData.Root.DeepClone(),
                    ["history"] = historyJson, ["profiles"] = profiles.DeepClone(),
                    ["identities"] = JsonNode.Parse(WoundIdentityState.SerializeCanonical(publishedData.Identities)),
                    ["effects"] = effects, ["schedule"] = schedule
                }.ToJsonString());

                var stable = fixture.CaptureMutationRoots(["output/interface_updates.json"]);
                var reopened = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance,
                    PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks { PassiveOperationTimings = timings });
                var scheduler = new ProgressionScheduleService(reopened, NullLogger<ProgressionScheduleService>.Instance);
                Assert.DoesNotContain(await scheduler.ValidateAcceptedTurnOutcomeAsync(control), issue => issue.Severity == IssueSeverity.Error);
                await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
                await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
                fixture.AssertMutationRootsUnchanged(stable);
                fixture.AssertMutationRootsUnchanged(publishedResources);
                fixture.AssertResourcesUnchanged();
            }
            passed = true;
        }
        catch (Exception failure) { scenarioFailure = failure; throw; }
        finally
        {
            Exception? captureFailure = null;
            Exception? cleanupFailure = null;
            try
            {
                if (scenarioFailure is not null && ownedRoot is not null && Directory.Exists(ownedRoot))
                    using (timings.Measure("LateHistory.FailureCapture"))
                        CaptureLateHistoryFailedFixture(ownedRoot, Path.Combine(evidence, "failed-fixture"));
            }
            catch (Exception failure) { captureFailure = failure; }
            try
            {
                using (timings.Measure("LateHistory.Cleanup"))
                    if (fixture is not null) await fixture.DisposeAsync();
            }
            catch (Exception failure) { cleanupFailure = failure; }
            finally { window.Dispose(); }
            var capture = window.Capture();
            var removed = ownedRoot is not null && !Directory.Exists(ownedRoot);
            passed = passed && captureFailure is null && cleanupFailure is null && capture.Complete && removed;
            await File.WriteAllTextAsync(Path.Combine(evidence, "outcome.json"), JsonSerializer.Serialize(new
            {
                lineage, importedCyclesAsData = 17, actualSignedCyclesRequested = 1, publishedCycles,
                passed, ownedRoot, removed, scenarioFailure = scenarioFailure?.ToString(),
                captureFailure = captureFailure?.ToString(), cleanupFailure = cleanupFailure?.ToString(), timings = capture
            }));
            if (captureFailure is not null || cleanupFailure is not null)
                throw new AggregateException("Independent late-History capture or cleanup failed.",
                    new[] { scenarioFailure, captureFailure, cleanupFailure }.OfType<Exception>());
            Assert.True(capture.Complete);
            if (ownedRoot is not null) Assert.True(removed);
        }
    }

    /// <summary>Checks every owner field before and after bootstrap without importing captured resource authority.</summary>
    /// <param name="captured">The immutable captured owner used solely as a DATA comparison oracle.</param>
    /// <param name="fresh">The ordinary fresh profile, before import or after its one real resource bootstrap.</param>
    /// <returns>The exact compared field names retained in external diagnostic evidence.</returns>
    private static string[] AssertLateHistoryInitialOwnerData(JsonObject captured, JsonObject fresh)
    {
        var fields = captured.Select(pair => pair.Key).Union(fresh.Select(pair => pair.Key), StringComparer.Ordinal)
            .Where(key => key is not "activeWounds" and not "activeEffects" &&
                key != AfterlifeEntityProfileState.ResourceOwnerBindingsProperty)
            .OrderBy(key => key, StringComparer.Ordinal).ToArray();
        foreach (var field in fields)
        {
            Assert.True(captured.TryGetPropertyValue(field, out var expected) && fresh.TryGetPropertyValue(field, out _),
                "Independent fixture owner shape differs at " + field);
            Assert.True(JsonNode.DeepEquals(expected, fresh[field]), "Independent fixture owner DATA differs at " + field);
        }
        return fields;
    }

    /// <summary>Validates exact current effect/identity agreement and the complete retained generation set.</summary>
    /// <param name="profiles">Current physical or published profile DATA.</param>
    /// <param name="effects">Current complete effect identity DATA.</param>
    /// <param name="wound">The exact surviving wound generation.</param>
    /// <param name="expectedEffectIds">All imported and newly published effect identities.</param>
    private static void AssertLateHistoryEffects(JsonObject profiles, JsonObject effects,
        WoundMaterializationEnvelope wound, HashSet<string> expectedEffectIds)
    {
        var mechanics = EffectMechanicsSnapshot.Build(
            EffectCarrierCatalog.Build(new(null, null, null, null, profiles, null)), effects, null, out var index);
        Assert.True(mechanics.IsAccepted, string.Join(";", mechanics.Issues.Select(issue => issue.Actual)));
        Assert.NotNull(index);
        var active = wound.Consequences.OwnedEffectSources.RootBindings.Select(binding => binding.EffectId)
            .ToHashSet(StringComparer.Ordinal);
        Assert.True(active.SetEquals(mechanics.Effects.Select(effect => effect.EffectId)));
        Assert.True(expectedEffectIds.SetEquals(index!.Entries.Select(entry => entry.EffectId)));
        foreach (var effectId in expectedEffectIds)
        {
            Assert.True(index.TryGetEntry(effectId, out var entry));
            Assert.Equal(active.Contains(effectId) ? "active" : "expired", entry.State);
        }
    }

    /// <summary>Reads and validates the new quartet through ordinary complete current-owner APIs.</summary>
    /// <param name="files">The actual freshly bootstrapped fixture filesystem.</param>
    /// <param name="lease">The live canonical lease protecting current source reads.</param>
    /// <returns>Exact resource bytes retained only as an unchanged-source assertion baseline.</returns>
    private static async Task<Dictionary<string, byte[]?>> AssertLateHistoryResourceAuthorityAsync(
        FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var roots = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath })
            roots.Add(path, Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, path)));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            CanonicalJsonUtf8.DecodeOneOptionalBom(roots[ResourceMaterializationContract.DefinitionsPath]!), false);
        Assert.True(definitions.IsValid);
        Assert.NotNull(definitions.Catalog);
        var state = ResourceStateContract.ParseCanonical(
            CanonicalJsonUtf8.DecodeOneOptionalBom(roots[ResourceMaterializationContract.StatePath]!), definitions.Catalog!, false);
        var history = ResourceHistoryState.ParseCanonical(
            CanonicalJsonUtf8.DecodeOneOptionalBom(roots[ResourceMaterializationContract.HistoryPath]!), definitions.Catalog!, false);
        Assert.True(state.IsValid);
        Assert.True(history.IsValid);
        Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions.Catalog!,
            path => files.ReadFileAsync(lease, path), state.Ledger, history.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(authority.IsValid, string.Join(";", authority.Issues.Select(issue => issue.Actual)));
        Assert.Empty(authority.Authority!.ValidateCanonicalAgreement(state.Ledger!, history.History));
        return roots;
    }

    /// <summary>Preserves every owned fixture file after a failure, without limiting diagnostic count or bytes.</summary>
    /// <param name="ownedRoot">The root reported by this scenario's actual fixture allocation.</param>
    /// <param name="destination">The external diagnostic destination, never a source of restored authority.</param>
    private static void CaptureLateHistoryFailedFixture(string ownedRoot, string destination)
    {
        var failures = new List<Exception>();
        foreach (var source in Directory.EnumerateFiles(ownedRoot, "*", SearchOption.AllDirectories))
        {
            try
            {
                var target = Path.Combine(destination, Path.GetRelativePath(ownedRoot, source));
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, overwrite: false);
            }
            catch (Exception failure) { failures.Add(failure); }
        }
        if (failures.Count != 0) throw new AggregateException("Late-History failure capture is incomplete.", failures);
    }
}
