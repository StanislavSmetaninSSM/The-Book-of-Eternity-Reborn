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
    public async Task ResidentRevocationPublishesInMixedHistoryAndRefusesItsOtherwiseEligibleAidWithoutDraw()
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 3, 14, stageRequest: false,
            beforeResourceBootstrap: SeedMixedAidOwnersAsync);
        var files = fixture.Files;
        var immutable = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var turn = 43; turn <= 46; turn++)
        {
            var requestId = "request_mixed_aid_" + turn;
            var originalWounds = (await fixture.ReadWoundsAsync()).Occurrences.Select(o => o.Wound).ToArray();
            var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync();
            JsonObject projection;
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                projection = (await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(files, lease,
                    new("session_spiritual_healing", requestId, turn, control)))!;
            Assert.NotNull(projection);
            await new LiveTurnPreparationService(files).PrepareAsync(new()
            {
                SessionId = "session_spiritual_healing", RequestId = requestId, TurnNumber = turn,
                CurrentRealm = "Chaos Sea", PlayerAction = "Обсудить добровольную помощь и спокойно провести цикл Обители.", ProgressionControl = control
            });
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            {
                var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
                var proposal = turn == 43 ? AidGrantProposal() : turn == 44 ? GuardianOfferProposal() : ResidentGrantProposal();
                proposal["woundId"] = turn == 43 ? "wound_guardian_history" : turn == 44 ? "wound_player_history" : "wound_spiritual_session";
                if (turn == 46) { proposal["operation"] = "revoke"; proposal["conscious"] = false; proposal["capable"] = false; }
                var ownerId = turn <= 44 ? AidGuardianId : AidResidentId;
                profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == ownerId)
                    [SpiritualTreatmentParticipation.ChangeProperty] = proposal;
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                var graphs = new JsonArray();
                foreach (var graph in projection["destinationGraphs"]!.AsArray().OfType<JsonObject>())
                {
                    var wound = originalWounds.Single(w => SpiritualHealingRequestProjection.GraphRef(
                        projection["sourceFingerprint"]!.GetValue<string>(), w.WoundId, graph["severityRank"]!.GetValue<int>()) == graph["stageRef"]!.GetValue<string>());
                    foreach (var authored in AuthorNaturalGraphs(wound, new JsonArray(graph.DeepClone()))) graphs.Add(authored!.DeepClone());
                }
                await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", new JsonObject
                { ["timestamp"] = DateTime.UtcNow.ToString("o"), ["dialogueOptions"] = new JsonArray(), ["spiritualHealingGraphs"] = graphs }.ToJsonString());
                if (turn >= 45) await WriteResidentAidMemoryAsync(files, lease, requestId, turn);
                await WriteAidReportAsync(files, lease, control, requestId, turn);
                var issues = await new ValidationService(files, NullLogger<ValidationService>.Instance).PrepareSpiritualSelfTreatmentPublicationAsync(lease);
                Assert.True(!issues.Any(i => i.Severity == IssueSeverity.Error), string.Join(";", issues.Select(i => i.Code + ":" + i.Actual)));
                var manifest = await fixture.ReadManifestAsync(lease);
                Assert.NotNull(await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                    .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
                // Direct backend fixture models protocol absence; the two real
                // guided journeys separately require genuine Engine cleanup.
                files.DeleteFile(lease, LiveTurnPreparationService.TurnRequestPath);
            }
            var history = await fixture.ReadHistoryAsync();
            var results = history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>().ToArray();
            Assert.Equal(turn - 42, results.Length);
            foreach (var old in immutable)
                Assert.Equal(old.Value, results.Single(r => r.ResultFingerprint == old.Key).ToCanonicalJson().ToJsonString());
            var last = results.Single(r => r.MatchesTurn("session_spiritual_healing", turn));
            Assert.Equal(turn == 43 ? 3 : turn == 44 ? 4 : 5, last.SchemaVersion);
            immutable.Add(last.ResultFingerprint, last.ToCanonicalJson().ToJsonString());
        }
        var root = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        var record = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidResidentId)
            [SpiritualTreatmentParticipation.RecordProperty]!.AsObject();
        Assert.Equal("revoked", record["status"]!.GetValue<string>());
        Assert.False(record["conscious"]!.GetValue<bool>()); Assert.False(record["capable"]!.GetValue<bool>());
        Assert.Equal(46, record["binding"]!["turn"]!.GetValue<int>());
        var selected = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(AidResidentId, selected.Owner.OwnerId);
        var selection = new SpiritualSelfTreatmentSelection(selected.WoundId, WoundIdentityState.ComputeSemanticFingerprint(selected))
            { Aid = SpiritualTreatmentParticipation.Selection(record) };
        var schedule = JsonNode.Parse((await files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
        Assert.Equal(schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>() + 1, record["eligibleCycleOrdinal"]!.GetValue<int>());
        var draws = 0;
        await using var refusalLease = await files.AcquireCanonicalWriteLeaseAsync();
        var before = fixture.CaptureMutationRoots(AfterlifeWoundTreatmentAuthority.AidLocalityPaths);
        var refused = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, refusalLease,
            new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance),
            "session_spiritual_healing", "request_after_revocation", 47, selection, count => { draws++; return [14]; });
        Assert.False(refused.Stage.IsStaged); Assert.Null(refused.Control); Assert.Equal(0, draws);
        fixture.AssertMutationRootsUnchanged(before); fixture.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task OrdinaryNaturalOwnerCannotPublishUnboundProtectedResidentMemory()
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 3, 14, stageRequest: false);
        await StageAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        await WriteResidentAidMemoryAsync(fixture.Files, lease, "request_spiritual_healing", 43);
        var before = fixture.CaptureMutationRoots(new[] { GuardianAbodeResidentState.StatePath });
        var admission = await AfterlifeWoundHealingPlanner.Continuation.AdmitNaturalAsync(fixture.Files, lease, await fixture.ReadManifestAsync(lease));
        var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(admission.Owner);
        try
        {
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            Assert.Contains("actual resident grant or aid owner", Assert.Throws<InvalidDataException>(() => owner.ApplyParticipationAfterImage(profiles)).Message);
            fixture.AssertMutationRootsUnchanged(before);
        }
        finally { owner.Close(); }
    }

    private static async Task SeedMixedAidOwnersAsync(FileSystemManager files, JsonObject profiles)
    {
        await ExplorerWoundSelfTreatmentTests.SeedEngineAidResidentAsync(files, profiles);
        var resident = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => SpiritualTreatmentParticipation.ProfileId(p) == AidResidentId);
        var originalWound = resident["activeWounds"]![0]!.ToJsonString();
        var originalEffects = resident["activeEffects"]!.ToJsonString();
        foreach (var (kind, id, woundId, effectKind) in new[]
        { ("guardian", AidGuardianId, "wound_guardian_history", "guardian"), ("player_soul", "player_soul", "wound_player_history", "player") })
        {
            string Rebind(string text) => text.Replace("wound_spiritual_session", woundId, StringComparison.Ordinal)
                .Replace("effect_", "effect_" + id + "_", StringComparison.Ordinal)
                .Replace("definition_", "definition_" + id + "_", StringComparison.Ordinal);
            var wound = JsonNode.Parse(Rebind(originalWound))!.AsObject();
            wound["owner"]!["ownerKind"] = kind; wound["owner"]!["ownerId"] = id;
            foreach (var definition in wound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().OfType<JsonObject>())
                definition["allowedTargetKinds"] = new JsonArray(effectKind);
            var effects = JsonNode.Parse(Rebind(originalEffects))!.AsArray();
            foreach (var effect in effects.OfType<JsonObject>()) { effect["target"]!["kind"] = effectKind; effect["target"]!["targetId"] = id; }
            var profile = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]!.GetValue<string>() == kind);
            profile["standardArts"]!["spiritual_healing"] = 3;
            profile["activeWounds"] = new JsonArray(wound); profile["activeEffects"] = effects;
        }
        var identities = new List<JsonObject>(); var transitions = new List<JsonObject>();
        foreach (var node in profiles["profiles"]!.AsArray().OfType<JsonObject>().SelectMany(p => p["activeWounds"]!.AsArray().OfType<JsonObject>()))
        {
            node["lastTransition"]!["transitionId"] = "create_" + node["woundId"]!.GetValue<string>();
            var parsed = WoundMaterializationContract.Parse(node.ToJsonString(), "mixed.aid.seed");
            Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Actual)));
            var wound = parsed.Wound!; var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound);
            identities.Add(WoundContractTestData.CreateIdentityEntry(wound.WoundId, wound.Owner.Realm, wound.Owner.OwnerKind,
                wound.Owner.OwnerId, wound.Owner.CarrierPath, "spiritual", semanticFingerprint: fingerprint));
            var row = WoundContractTestData.CreateTransition(); row["woundId"] = wound.WoundId;
            row["transitionId"] = wound.LastTransition.TransitionId; row["ordinal"] = transitions.Count + 1;
            row["woundTransitionOrdinal"] = 1; row["operationKey"] = "create_" + wound.WoundId;
            row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(wound.WoundId); row["afterFingerprint"] = fingerprint;
            row["sourceFingerprint"] = "sha256:" + new string('e', 64); row["attemptId"] = null;
            row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(row["operationKey"]!.GetValue<string>(), row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
            transitions.Add(row);
        }
        var allEffects = profiles["profiles"]!.AsArray().OfType<JsonObject>().SelectMany(p => p["activeEffects"]!.AsArray().OfType<JsonObject>()).ToArray();
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(allEffects);
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            var effect = allEffects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundContractTestData.CreateIdentityIndex(identities.ToArray()).ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundContractTestData.CreateHistory(transitions.ToArray()).ToJsonString());
        await files.WriteFileAtomicAsync(lease, EffectIdentityState.StatePath, index.ToJsonString());
    }
}
