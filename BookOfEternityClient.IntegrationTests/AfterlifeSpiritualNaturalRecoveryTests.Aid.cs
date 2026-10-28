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
    internal const string AidGuardianId = "guardian_spiritual_aid";
    internal const string AidAbodeId = "abode_spiritual_aid";

    [Fact]
    public async Task AcceptedNaturalTurnCommitsGuardianParticipationWithItsFullContentWitness()
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 3, 20, stageRequest: false,
            beforeResourceBootstrap: SeedAidGuardianAsync);
        var catalog = await fixture.ReadWoundsAsync();
        var original = Assert.Single(catalog.Occurrences).Wound;
        var identities = WoundIdentityState.Parse(await fixture.Files.ReadFileAsync(WoundIdentityState.StatePath), WoundIdentityState.StatePath);
        Assert.True(identities.IsValid, string.Join(";", identities.Issues.Select(i => i.Actual)));
        Assert.Empty((await fixture.ReadHistoryAsync()).ValidateAgreement(identities.State!, catalog));
        Assert.Equal("guardian", original.Owner.OwnerKind);
        Assert.Equal(AidGuardianId, original.Owner.OwnerId);
        Assert.Equal(0, original.Recovery.CurrentStepProgress);
        var (control, projection) = await StageAsync(fixture);
        Assert.Equal(0, Assert.Single(projection["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());

        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var scope = await new LocalInteractionScopeService(fixture.Files).ResolveAsync(lease);
            Assert.True(scope.IsResolved, scope.UnavailableReason);
            Assert.Equal(AidGuardianId, scope.CurrentGuardianId);
            Assert.Equal(AidAbodeId, scope.LocationId);
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            var guardian = profiles["profiles"]!.AsArray().OfType<JsonObject>()
                .Single(p => p["actorId"]!.GetValue<string>() == AidGuardianId);
            Assert.Null(guardian["spiritualTreatmentParticipation"]);
            guardian["spiritualTreatmentParticipationChange"] = new JsonObject
            {
                ["schemaVersion"] = 1, ["operation"] = "grant",
                ["healerId"] = "player_soul", ["woundId"] = original.WoundId,
                ["conscious"] = true, ["capable"] = true,
                ["readableEvidence"] = "Хранитель понимает предложение души и явно разрешает ей лечить эту рану."
            };
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        }

        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            var raw = (await fixture.Files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            var forged = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(raw))!.AsObject();
            forged["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == AidGuardianId)
                [SpiritualTreatmentParticipation.RecordProperty] = new JsonObject { ["status"] = "granted" };
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, forged.ToJsonString());
            var before = fixture.CaptureMutationRoots();
            var refusal = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
                .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.Contains(refusal, i => i.Code == "spiritual_participation_invalid" &&
                i.Actual!.Contains("cannot mint or change", StringComparison.Ordinal));
            fixture.AssertMutationRootsUnchanged(before);
            await fixture.Files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, raw);
        }
        var published = await PublishAsync(fixture);
        Assert.NotNull(published); // A refusal cannot masquerade as missing-grant causal RED.
        var after = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(original.WoundId, after.WoundId);
        Assert.Equal(3, after.Severity.Rank);
        Assert.Equal(1, after.Recovery.CurrentStepProgress);
        var schedule = JsonNode.Parse((await fixture.Files.ReadFileAsync(ProgressionScheduleService.SchedulePath))!)!;
        Assert.Equal(control.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
        Assert.Null(await fixture.Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
        fixture.AssertResourcesUnchanged();
        var history = await fixture.ReadHistoryAsync();
        Assert.DoesNotContain(history.Transitions, t => t.Kind == "treat" || t.AttemptId is not null);
        var row = Assert.Single(history.Transitions, t => t.TransitionResult is SpiritualHealingPersistedResult);
        var result = Assert.IsType<SpiritualHealingPersistedResult>(row.TransitionResult).ToCanonicalJson();
        Assert.Equal("natural", result["mode"]!.GetValue<string>());
        var current = JsonNode.Parse((await fixture.Files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
        var acceptedGuardian = current["profiles"]!.AsArray().OfType<JsonObject>()
            .Single(p => p["actorId"]!.GetValue<string>() == AidGuardianId);
        var participation = acceptedGuardian["spiritualTreatmentParticipation"];
        Assert.NotNull(participation); // First aid-specific RED: actual natural publication already succeeded.
        Assert.Null(acceptedGuardian["spiritualTreatmentParticipationChange"]);
        Assert.Equal("granted", participation!["status"]!.GetValue<string>());
        Assert.Equal("player_soul", participation["healerId"]!.GetValue<string>());
        Assert.Equal(AidGuardianId, participation["targetId"]!.GetValue<string>());
        Assert.Equal(original.WoundId, participation["woundId"]!.GetValue<string>());
        Assert.Equal(control.NextChaosSeaTurnOrdinal + 1, participation["eligibleCycleOrdinal"]!.GetValue<int>());
        Assert.Equal(row.TransitionId, participation["primaryTransitionId"]!.GetValue<string>());
        Assert.Equal(3, result["schemaVersion"]!.GetValue<int>());
        var outcome = Assert.Single(result["participationOutcomes"]!.AsArray());
        Assert.True(JsonNode.DeepEquals(participation, outcome!["record"]));
        Assert.Equal(SpiritualTreatmentParticipation.Fingerprint(participation.AsObject()), outcome["contentFingerprint"]!.GetValue<string>());
        var parserIssues = new List<ValidationIssue>();
        Assert.NotNull(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(result), "valid.participation", parserIssues));
        Assert.Empty(parserIssues);
        foreach (var invalid in new[] { "v2_payload", "v3_missing", "v3_empty", "v3_foreign_content" })
        {
            var changed = result.DeepClone().AsObject();
            switch (invalid)
            {
                case "v2_payload": changed["schemaVersion"] = 2; break;
                case "v3_missing": changed.Remove("participationOutcomes"); break;
                case "v3_empty": changed["participationOutcomes"] = new JsonArray(); break;
                default: changed["participationOutcomes"]![0]!["record"]!["readableEvidence"] = "Иное согласие с тем же принятым tuple."; break;
            }
            changed["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(changed);
            parserIssues.Clear();
            Assert.Null(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(changed), invalid, parserIssues));
            Assert.Contains(parserIssues, i => i.Code == "spiritual_healing_history_invalid");
        }
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var manifest = await fixture.ReadManifestAsync(lease);
            Task<SpiritualHealingPersistedResult?> Read() => ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(
                control, manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
                p => fixture.Files.ReadFileBytesAsync(lease, p), manifest.ManifestPayloadHash, expectedNaturalProjection: projection);
            Assert.NotNull(await Read());
            var bytes = (await fixture.Files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            acceptedGuardian[SpiritualTreatmentParticipation.RecordProperty]!["readableEvidence"] = "Подмена содержимого с чужим прежним accepted tuple.";
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, current.ToJsonString());
            var before = fixture.CaptureMutationRoots();
            var refusal = await Assert.ThrowsAsync<InvalidDataException>(() => Read());
            Assert.Contains("exact accepted profile content", refusal.Message);
            fixture.AssertMutationRootsUnchanged(before);
            await fixture.Files.WriteFileAtomicBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, bytes);
            Assert.NotNull(await Read());
        }
        fixture.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task RolledBackNaturalGrantCannotLeaveAParticipationRecordOrAcceptedWitness()
    {
        var armed = false;
        var reached = 0;
        FileSystemManager? owned = null;
        var fault = new ParticipationPublicationFault();
        await using var fixture = await HealingFixture.CreateAsync(3, 3, 20, stageRequest: false,
            beforeResourceBootstrap: SeedAidGuardianAsync, hooks: new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, _) =>
                {
                    if (!armed || phase != TrustedLocalPublicationPhase.MemberPublished) return;
                    var root = JsonNode.Parse(File.ReadAllText(owned!.ResolvePath(WoundCarrierCatalog.AfterlifeProfilesPath)))!;
                    if (!root["profiles"]!.AsArray().OfType<JsonObject>().Any(p =>
                            p[SpiritualTreatmentParticipation.RecordProperty] is not null)) return;
                    reached++; armed = false;
                    throw fault; // After actual profile publication, before transaction completion.
                }
            });
        owned = fixture.Files;
        await StageAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorId"]!.GetValue<string>() == AidGuardianId)
            [SpiritualTreatmentParticipation.ChangeProperty] = new JsonObject
            {
                ["schemaVersion"] = 1, ["operation"] = "grant", ["healerId"] = "player_soul",
                ["woundId"] = "wound_spiritual_session", ["conscious"] = true, ["capable"] = true,
                ["readableEvidence"] = "Хранитель разрешает душе лечить эту рану."
            };
        await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await fixture.WriteReportAsync(lease);
        var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
        var before = fixture.CaptureMutationRoots();
        Exception? failure;
        armed = true;
        try
        {
            failure = await Record.ExceptionAsync(() => new CanonicalStateNormalizer(fixture.Files,
                NullLogger<CanonicalStateNormalizer>.Instance).BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        }
        finally { armed = false; }
        Assert.Equal(1, reached);
        Assert.NotNull(failure);
        Assert.IsNotType<CoordinatedStatePublicationUncertainException>(failure);
        fixture.AssertMutationRootsUnchanged(before);
        Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.TransitionResult is SpiritualHealingPersistedResult);
        var after = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
        Assert.All(after["profiles"]!.AsArray().OfType<JsonObject>(), p => Assert.Null(p[SpiritualTreatmentParticipation.RecordProperty]));
    }

    private sealed class ParticipationPublicationFault : InvalidOperationException { }

    internal static async Task SeedAidGuardianAsync(FileSystemManager files, JsonObject profiles)
    {
        // This is canonical initial create DATA, not an invented accepted grant.
        // It runs before the fixture's one real resource bootstrap.
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var player = profiles["profiles"]![0]!.AsObject();
        var wound = player["activeWounds"]![0]!.DeepClone().AsObject();
        wound["owner"]!["ownerKind"] = "guardian";
        wound["owner"]!["ownerId"] = AidGuardianId;
        wound["display"]!["visibility"] = "public";
        foreach (var definition in wound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().OfType<JsonObject>())
            definition["allowedTargetKinds"] = new JsonArray("guardian");
        var effects = player["activeEffects"]!.AsArray().OfType<JsonObject>().Select(e => e.DeepClone().AsObject()).ToArray();
        foreach (var effect in effects)
        {
            effect["target"]!["kind"] = "guardian";
            effect["target"]!["targetId"] = AidGuardianId;
        }
        var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", AidGuardianId, "Chaos Sea", 42);
        guardian["displayName"] = "Хранитель целостности";
        guardian["locationId"] = AidAbodeId;
        guardian["standardArts"]!["spiritual_healing"] = 0;
        guardian["activeWounds"] = new JsonArray(wound);
        guardian["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
        player["activeWounds"] = new JsonArray();
        player["activeEffects"] = new JsonArray();
        profiles["profiles"]!.AsArray().Add(guardian);
        var parsed = WoundMaterializationContract.Parse(wound.ToJsonString(), "aid.seed");
        Assert.True(parsed.IsValid, string.Join(";", parsed.Issues.Select(i => i.Actual)));
        var w = parsed.Wound!;
        var fp = WoundIdentityState.ComputeSemanticFingerprint(w);
        var identity = WoundContractTestData.CreateIdentityEntry(w.WoundId, "chaos_sea", "guardian", AidGuardianId,
            WoundCarrierCatalog.AfterlifeProfilesPath, "spiritual", semanticFingerprint: fp);
        var row = WoundContractTestData.CreateTransition();
        row["woundId"] = w.WoundId; row["transitionId"] = w.LastTransition.TransitionId;
        row["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(w.WoundId);
        row["afterFingerprint"] = fp; row["sourceFingerprint"] = "sha256:" + new string('e', 64);
        row["attemptId"] = null;
        row["outputFingerprint"] = WoundHistoryState.ComputeOutputFingerprint(row["operationKey"]!.GetValue<string>(),
            row["eventRef"]!.GetValue<string>(), row["readableSummary"]!.GetValue<string>());
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects);
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            var effect = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        var localGuardian = new JsonObject
        {
            ["guardianId"] = AidGuardianId, ["canonicalName"] = "Хранитель целостности",
            ["abode"] = new JsonObject { ["abodeId"] = AidAbodeId, ["name"] = "Обитель целостности" }
        };
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundContractTestData.CreateIdentityIndex(identity).ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundContractTestData.CreateHistory(row).ToJsonString());
        await files.WriteFileAtomicAsync(lease, EffectIdentityState.StatePath, index.ToJsonString());
        await files.WriteFileAtomicAsync(lease, "game_state/meta/guardians.json", new JsonObject
        {
            ["guardians"] = new JsonArray(localGuardian.DeepClone()), ["activeGuardian"] = localGuardian.DeepClone(),
            ["chaosSeaNavigation"] = new JsonObject { ["currentGuardianId"] = AidGuardianId, ["currentAbodeId"] = AidAbodeId }
        }.ToJsonString());
    }
}
