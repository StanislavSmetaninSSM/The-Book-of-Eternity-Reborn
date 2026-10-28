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
    public async Task PersistentOffscreenOwnersPublishOwnTierRecoveryAndTerminalOwnerLossRefusesReplay()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false,
            beforeResourceBootstrap: SeedPersistentOwnersAsync);
        var (control, projection) = await StageAsync(fixture);
        Assert.Equal(new[] { 2, 0, 0, 5 }, projection["cohort"]!.AsArray()
            .Select(m => m!["healingTier"]!.GetValue<int>()));
        var before = fixture.CaptureMutationRoots();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error),
                string.Join("\n", issues.Select(i => i.Code + "@" + i.FilePath + ": " + i.Message + ": " + i.Actual)));
            Assert.NotNull(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        }
        var active = (await fixture.ReadWoundsAsync()).Occurrences;
        Assert.Equal(new[] { "guardian", "player_soul" }, active.Select(o => o.Wound.Owner.OwnerKind).Order(StringComparer.Ordinal));
        Assert.All(active, w => Assert.Equal(1, w.Wound.Recovery.CurrentStepProgress));
        var history = await fixture.ReadHistoryAsync();
        Assert.Equal(2, history.Transitions.Count(t => t.Terminal));
        Assert.DoesNotContain(history.Transitions, t => t.AttemptId is not null || t.Kind == "treat");
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        Assert.Equal(4, result.Stages.Select(s => s.Before.WoundId).Distinct().Count());
        foreach (var path in new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath })
            Assert.Equal(before[path], await fixture.Files.ReadFileBytesAsync(path));
        var accepted = fixture.CaptureMutationRoots();
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var frame = await fixture.ReadManifestAsync(lease);
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control,
                frame.SessionId, frame.RequestId, frame.TurnNumber, p => fixture.Files.ReadFileBytesAsync(lease, p),
                frame.ManifestPayloadHash, expectedNaturalProjection: projection));
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            foreach (var realmChange in new[] { false, true })
            {
                var changed = profiles.DeepClone().AsObject();
                var resident = changed["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == "resident_natural");
                Assert.Empty(resident["activeWounds"]!.AsArray()); // This owner's wound healed in this exact cycle.
                if (realmChange) resident["realm"] = "Shining Abode";
                else changed["profiles"]!.AsArray().Remove(resident);
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                    ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control, frame.SessionId, frame.RequestId,
                        frame.TurnNumber, p => p == WoundCarrierCatalog.AfterlifeProfilesPath
                            ? Task.FromResult<byte[]?>(System.Text.Encoding.UTF8.GetBytes(changed.ToJsonString()))
                            : fixture.Files.ReadFileBytesAsync(lease, p)));
                Assert.Contains("lost a persistent original owner", failure.Message);
            }
        }
        fixture.AssertMutationRootsUnchanged(accepted);

        // A later real signed cycle contains only the two still-active H0 wounds.
        // The two legitimately healed in43 remain in identity/History, but must
        // not be required again by the independent completeness witness in44.
        var (secondControl, secondProjection) = await PrepareLaterNaturalCycleAsync(fixture, 44, "request_natural_second");
        Assert.Equal(2, secondProjection!["cohort"]!.AsArray().Count);
        await PublishPreparedCycleAsync(fixture);
        Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
        Assert.Equal(4, (await fixture.ReadHistoryAsync()).Transitions.Count(t => t.Terminal));
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var frame = await fixture.ReadManifestAsync(lease);
            var read = (string path) => fixture.Files.ReadFileBytesAsync(lease, path);
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(secondControl,
                frame.SessionId, frame.RequestId, 44, read, frame.ManifestPayloadHash, expectedNaturalProjection: secondProjection));
            var oldCohort = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(secondControl, frame.SessionId, frame.RequestId,
                    44, read, frame.ManifestPayloadHash, expectedNaturalProjection: projection));
            Assert.Contains("exact signed original family, cohort and snapshot", oldCohort.Message);
            var oldCycle = await Assert.ThrowsAsync<InvalidDataException>(() =>
                ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control, frame.SessionId, frame.RequestId,
                    44, read, frame.ManifestPayloadHash, expectedNaturalProjection: secondProjection));
            Assert.Contains("changed control", oldCycle.Message);
        }
        var secondStable = fixture.CaptureMutationRoots();
        var secondScheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
        await secondScheduler.ApplyAcceptedTurnOutcomeAsync(secondControl);
        await secondScheduler.ApplyAcceptedTurnOutcomeAsync(secondControl);
        fixture.AssertMutationRootsUnchanged(secondStable);

        var historyBeforeEmpty = (await fixture.Files.ReadFileBytesAsync(WoundHistoryState.HistoryPath))!;
        var (emptyControl, emptyProjection) = await PrepareLaterNaturalCycleAsync(fixture, 45, "request_natural_empty");
        Assert.Null(emptyProjection);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var noWork = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.DoesNotContain(noWork, i => i.Severity == IssueSeverity.Error);
            Assert.Null(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        }
        await secondScheduler.ApplyAcceptedTurnOutcomeAsync(emptyControl);
        Assert.Equal(historyBeforeEmpty, await fixture.Files.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
        Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
        Assert.Equal(3, JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
    }

    [Fact]
    public async Task RankFourCarriesOverflowThroughAnAuthoredRankThreeGraph()
    {
        await using var fixture = await HealingFixture.CreateAsync(4, 5, 20, stageRequest: false, initialProgress: 7);
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var oldRoots = original.Consequences.OwnedEffectSources.RootBindings.Select(r => r.EffectId).ToArray();
        var (control, projection) = await StageAsync(fixture);
        Assert.Equal(3, Assert.Single(projection["destinationGraphs"]!.AsArray())!["severityRank"]!.GetValue<int>());
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            var unchanged = fixture.CaptureMutationRoots();
            var absent = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.Contains(absent, i => i.Code == "spiritual_natural_recovery_admission_invalid" &&
                i.Actual!.Contains("GM-authored spiritual destination graphs are required", StringComparison.Ordinal));
            fixture.AssertMutationRootsUnchanged(unchanged);
            await fixture.Files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
            {
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(original, projection["destinationGraphs"]!.AsArray()),
                ["timestamp"] = DateTime.UtcNow.ToString("o")
            }.ToJsonString());
        }
        var plan = await PublishAsync(fixture);
        var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(3, after.Severity.Rank);
        Assert.Equal(5, after.Recovery.CurrentStepProgress); //7+6−8; overflow is not capped/discarded.
        Assert.Equal(6, after.Recovery.CurrentStepThreshold);
        var history = await fixture.ReadHistoryAsync();
        Assert.DoesNotContain(history.Transitions, t => t.AttemptId is not null || t.Kind == "treat" || t.Terminal);
        var published = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        Assert.True(published.IsNatural);
        Assert.Equal(3, Assert.Single(published.Stages).After.Severity.Rank);
        using var doc = JsonDocument.Parse((await fixture.Files.ReadFileAsync(EffectIdentityState.StatePath))!);
        var identity = EffectIdentityState.Parse(doc.RootElement, EffectIdentityState.StatePath);
        Assert.Empty(identity.Issues);
        foreach (var id in oldRoots) { Assert.True(identity.State!.TryGetEntry(id, out var retired)); Assert.Equal("expired", retired.State); }
        foreach (var root in after.Consequences.OwnedEffectSources.RootBindings)
        { Assert.True(identity.State!.TryGetEntry(root.EffectId, out var current)); Assert.Equal("active", current.State); }
        Assert.DoesNotContain(await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        var stable = fixture.CaptureMutationRoots(plan.TouchedPaths.Concat(plan.ConsumedPaths));
        var scheduler = new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance);
        for (var replay = 0; replay < 2; replay++)
        {
            Assert.DoesNotContain(await scheduler.ValidateAcceptedTurnOutcomeAsync(control), i => i.Severity == IssueSeverity.Error);
            await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
        }
        fixture.AssertMutationRootsUnchanged(stable);
        fixture.AssertResourcesUnchanged();

        await using var terminal = await HealingFixture.CreateAsync(4, 5, 20, stageRequest: false, initialProgress: 14);
        var terminalOriginal = Assert.Single((await terminal.ReadWoundsAsync()).Occurrences).Wound;
        var (_, terminalProjection) = await StageAsync(terminal);
        Assert.Equal(new[] { 3, 2, 1 }, terminalProjection["destinationGraphs"]!.AsArray()
            .Select(g => g!["severityRank"]!.GetValue<int>()));
        await terminal.Files.WriteFileAtomicAsync("output/interface_updates.json", new JsonObject
        {
            ["spiritualHealingGraphs"] = AuthorNaturalGraphs(terminalOriginal, terminalProjection["destinationGraphs"]!.AsArray()),
            ["timestamp"] = DateTime.UtcNow.ToString("o")
        }.ToJsonString());
        await PublishAsync(terminal);
        Assert.Empty((await terminal.ReadWoundsAsync()).Occurrences);
        var terminalHistory = await terminal.ReadHistoryAsync();
        Assert.Single(terminalHistory.Transitions, t => t.Terminal);
        var terminalResult = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(terminalHistory.Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        Assert.Equal(new[] { 3, 2, 1, 1, 1 }, terminalResult.Stages.Select(s => s.After.Severity.Rank));
        Assert.Equal(new[] { "recover", "recover", "recover", "recover", "heal" }, terminalResult.Stages.Select(s => s.Kind));
        var trimmed = terminalResult.ToCanonicalJson();
        trimmed["stages"]!.AsArray().RemoveAt(1);
        trimmed["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(trimmed);
        var issues = new List<ValidationIssue>();
        Assert.Null(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(trimmed), "trimmed.intermediate", issues));
        Assert.Contains(issues, i => i.Actual!.Contains("Missing/extra spiritual execution stage", StringComparison.Ordinal));
        terminal.AssertResourcesUnchanged();
    }

    // Explicit authored choices for this GM fixture; never a production survivor-selection rule.
    internal static JsonArray AuthorNaturalGraphs(WoundMaterializationEnvelope original, JsonArray requested)
    {
        var graphs = new JsonArray();
        foreach (var item in requested.OfType<JsonObject>())
        {
            var rank = item["severityRank"]!.GetValue<int>();
            var profiles = rank switch
            {
                1 => new[] { "spiritual_roll_hindrance" },
                2 => new[] { "spiritual_roll_hindrance", "spiritual_action_cost_burden" },
                3 => new[] { "spiritual_roll_hindrance", "spiritual_action_cost_burden", "spiritual_strain_burden" },
                _ => throw new InvalidDataException("This GM fixture authors only requested I–III destinations.")
            };
            var definitions = new JsonArray();
            foreach (var profile in profiles)
            {
                var effect = original.Consequences.Entries.Single(e => e.ProfileKey == profile).EffectId;
                var binding = original.Consequences.OwnedEffectSources.RootBindings.Single(r => r.EffectId == effect);
                var definition = JsonNode.Parse(original.Consequences.OwnedEffectSources.Definitions.Single(d =>
                    d.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
                definition["links"] = new JsonArray();
                if (profile == "spiritual_action_cost_burden") definition["components"]![0]!["payload"]!["magnitude"] = rank - 1;
                definitions.Add(new JsonObject
                {
                    ["definitionRef"] = binding.DefinitionKey, ["definition"] = definition,
                    ["root"] = new JsonObject
                    {
                        ["ownership"] = new JsonObject { ["kind"] = "base_wound", ["complicationRef"] = null },
                        ["slots"] = new JsonArray(new JsonObject { ["profileKey"] = profile,
                            ["readableSummary"] = "Явно описанное Мастером ослабленное последствие." })
                    }
                });
            }
            graphs.Add(new JsonObject { ["stageRef"] = item["stageRef"]!.DeepClone(), ["consequenceDefinitions"] = definitions });
        }
        return graphs;
    }

    private static async Task SeedPersistentOwnersAsync(FileSystemManager files, JsonObject profiles)
    {
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var player = profiles["profiles"]![0]!.AsObject();
            var woundText = player["activeWounds"]![0]!.ToJsonString();
            var effectText = player["activeEffects"]![0]!.ToJsonString();
            foreach (var (kind, actorType, id, tier) in new[]
            {
                ("guardian", "guardian", "guardian_natural", 0), ("resident", "resident", "resident_natural", 5),
                ("afterlife_actor", "custom_afterlife_actor", "other_natural", 2)
            })
            {
                string Rebind(string text) => text.Replace("wound_spiritual_session", "wound_" + id, StringComparison.Ordinal)
                    .Replace("effect_", "effect_" + id + "_", StringComparison.Ordinal)
                    .Replace("definition_", "definition_" + id + "_", StringComparison.Ordinal);
                var wound = JsonNode.Parse(Rebind(woundText))!.AsObject();
                wound["owner"]!["ownerKind"] = kind; wound["owner"]!["ownerId"] = id;
                wound["display"]!["visibility"] = "hidden";
                foreach (var definition in wound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().OfType<JsonObject>())
                    definition["allowedTargetKinds"] = new JsonArray(kind);
                var effect = JsonNode.Parse(Rebind(effectText))!.AsObject();
                effect["target"]!["kind"] = kind; effect["target"]!["targetId"] = id;
                var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile(actorType, id, "Chaos Sea", 42);
                profile["standardArts"]!["spiritual_healing"] = tier;
                profile["locationId"] = "location_offscreen_natural";
                profile["activeWounds"] = new JsonArray(wound); profile["activeEffects"] = new JsonArray(effect);
                profiles["profiles"]!.AsArray().Add(profile);
            }
            var identities = new List<JsonObject>(); var transitions = new List<JsonObject>();
            foreach (var node in profiles["profiles"]!.AsArray().OfType<JsonObject>()
                         .SelectMany(p => p["activeWounds"]!.AsArray().OfType<JsonObject>()))
            {
                node["lastTransition"]!["transitionId"] = "create_" + node["woundId"]!.GetValue<string>();
                var wound = WoundMaterializationContract.Parse(node.ToJsonString(), "persistent.seed");
                Assert.True(wound.IsValid, string.Join(";", wound.Issues.Select(i => i.Actual)));
                var w = wound.Wound!; var fp = WoundIdentityState.ComputeSemanticFingerprint(w);
                identities.Add(WoundContractTestData.CreateIdentityEntry(w.WoundId, w.Owner.Realm, w.Owner.OwnerKind,
                    w.Owner.OwnerId, w.Owner.CarrierPath, "spiritual", semanticFingerprint: fp));
                var row = WoundContractTestData.CreateTransition();
                row["woundId"] = w.WoundId; row["transitionId"] = w.LastTransition.TransitionId;
                row["ordinal"] = transitions.Count + 1; row["woundTransitionOrdinal"] = 1; row["operationKey"] = "create_" + w.WoundId;
                row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(w.WoundId);
                row["afterFingerprint"] = fp; row["sourceFingerprint"] = "sha256:" + new string('e', 64); row["attemptId"] = null;
                row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(row["operationKey"]!.GetValue<string>(),
                    row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
                transitions.Add(row);
            }
            var effects = profiles["profiles"]!.AsArray().OfType<JsonObject>()
                .SelectMany(p => p["activeEffects"]!.AsArray().OfType<JsonObject>()).ToArray();
            var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects);
            foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
            {
                var effect = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
                entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
                entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
                entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
            }
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundContractTestData.CreateIdentityIndex(identities.ToArray()).ToJsonString());
            await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundContractTestData.CreateHistory(transitions.ToArray()).ToJsonString());
            await files.WriteFileAtomicAsync(lease, EffectIdentityState.StatePath, index.ToJsonString());
        }
    }

    private static async Task<(BookOfEternityClient.Models.ProgressionControl Control, JsonObject? Projection)>
        PrepareLaterNaturalCycleAsync(HealingFixture fixture, int turn, string requestId)
    {
        var control = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync();
        JsonObject? projection;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            projection = await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(fixture.Files, lease,
                new("session_spiritual_healing", requestId, turn, control));
        await new LiveTurnPreparationService(fixture.Files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_spiritual_healing", RequestId = requestId, TurnNumber = turn,
            CurrentRealm = "Chaos Sea", PlayerAction = "Продолжить безопасное наблюдение.", ProgressionControl = control
        });
        await fixture.Files.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, JsonSerializer.Serialize(new
        {
            progressionProcessingReport = new
            {
                sessionId = "session_spiritual_healing", requestId, turnNumber = turn,
                worldCyclesProcessed = 0, factionCyclesProcessed = 0,
                chaosSeaCyclesProcessed = control.ChaosSeaCyclesExpectedThisTurn,
                guardianProjectCyclesProcessed = control.GuardianProjectCyclesExpectedThisTurn,
                residentAgencyCyclesProcessed = control.ResidentAgencyCyclesExpectedThisTurn,
                shiningAbodeCyclesProcessed = control.ShiningAbodeCyclesExpectedThisTurn,
                shiningFactionCyclesProcessed = control.ShiningFactionCyclesExpectedThisTurn,
                shiningTradeCyclesProcessed = control.ShiningTradeCyclesExpectedThisTurn,
                newLastChaosSeaSimulationOrdinal = control.NextChaosSeaTurnOrdinal,
                newLastGuardianProjectCycleOrdinal = control.NextGuardianProjectCycleOrdinal,
                newLastResidentAgencyCycleOrdinal = control.NextResidentAgencyCycleOrdinal,
                newLastShiningAbodeCycleOrdinal = control.NextShiningAbodeCycleOrdinal,
                newLastShiningFactionCycleOrdinal = control.NextShiningFactionCycleOrdinal,
                newLastShiningTradeCycleOrdinal = control.NextShiningTradeCycleOrdinal,
                afterlifeCatchupProcessed = false, afterlifeCatchupSummaryEventsProcessed = 0
            }
        }));
        return (control, projection);
    }

    private static async Task PublishPreparedCycleAsync(HealingFixture fixture)
    {
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
        Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error),
            string.Join("\n", issues.Select(i => i.Code + "@" + i.FilePath + ": " + i.Message + ": " + i.Actual)));
        Assert.NotNull(await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
    }
}
