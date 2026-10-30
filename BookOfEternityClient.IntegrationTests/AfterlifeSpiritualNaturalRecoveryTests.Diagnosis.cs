using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Theory]
    [InlineData("Chaos Sea")][InlineData("Shining Abode")]
    public async Task ActualDiagnosisPublishesKnowledgeAndOneNaturalResultThenReplaysWithoutInput(string realm)
        => await RunDiagnosisNativeAsync(realm, 0, 0);

    [Fact]
    public Task HealingTierVDiagnosisRetainsOriginalAfterTerminalRecovery() => RunDiagnosisNativeAsync("Chaos Sea", 5, 3);

    private static async Task RunDiagnosisNativeAsync(string realm, int tier, int initialProgress)
    {
        await using var fixture = await HealingFixture.CreateAsync(2, tier, 20, stageRequest: false, realm: realm,
            initialProgress: initialProgress, beforeResourceBootstrap: SeedDiagnosisBeforeBootstrapAsync);
        var files = fixture.Files;
        var before = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(2, before.Severity.Rank);
        Assert.Equal("hidden", Assert.Single(before.Complications).Visibility);
        var selection = new SpiritualSelfTreatmentSelection(before.WoundId, WoundIdentityState.ComputeSemanticFingerprint(before))
        { Diagnosis = new(before.Owner, "native_diagnosis", WoundDiagnosisEvidence.ComputePathFingerprint(before, "native_diagnosis")) };
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        var control = await scheduler.BuildControlForNextTurnAsync();
        JsonObject retained;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var staged = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(files, lease,
                new("session_spiritual_healing", "request_spiritual_healing", 43, control), selection);
            Assert.True(staged.IsStaged, staged.IssueCode);
            retained = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
            Assert.Equal(tier, retained["diagnosis"]!["healingTier"]!.GetValue<int>());
        }
        await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_spiritual_healing", RequestId = "request_spiritual_healing", TurnNumber = 43,
            CurrentRealm = realm, ProgressionControl = control, PlayerAction = WoundApplicationService.DiagnosisActionText
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var turn = JsonSerializer.Deserialize<TurnRequest>((await files.ReadFileAsync(lease,
                LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            await ExplorerWoundSelfTreatmentTests.WriteNativeCompletionAsync(files, lease, turn,
                narrative: "Я внимательно осматриваю духовную рану. Раскрылись предусмотренные способом сведения; мир продолжил безопасный цикл.");
            var preparation = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.DoesNotContain(preparation, i => i.Severity == IssueSeverity.Error);
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var validated));
            var plan = Assert.IsType<AcceptedMechanicsPlan>(validated.Plan);
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(plan.SpiritualHealingContinuation);
            var writes = plan.OwnerCompanionAfterImages.ToDictionary(p => p.Key, p => p.Value.DeepClone().AsObject(), StringComparer.Ordinal);
            foreach (var pair in plan.EffectCarrierAfterImages) writes[pair.Key] = pair.Value.DeepClone().AsObject();
            foreach (var pair in plan.WoundCarrierAfterImages) writes[pair.Key] = pair.Value.DeepClone().AsObject();
            owner.RequireDiagnosisComposedImages(writes);
            var invalid = writes.ToDictionary(p => p.Key, p => p.Value.DeepClone().AsObject(), StringComparer.Ordinal);
            var player = invalid[WoundCarrierCatalog.AfterlifeProfilesPath]["profiles"]!.AsArray().OfType<JsonObject>()
                .Single(p => p["actorType"]?.GetValue<string>() == "player_soul");
            var changedTier = tier == 5 ? 0 : 5;
            player["standardArts"]!["spiritual_healing"] = changedTier;
            invalid[AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath]["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = changedTier;
            Assert.Throws<InvalidDataException>(() => owner.RequireDiagnosisComposedImages(invalid));
            var missing = new Dictionary<string, JsonObject>(writes, StringComparer.Ordinal); missing.Remove(WoundCarrierCatalog.AfterlifeProfilesPath);
            Assert.Throws<InvalidDataException>(() => owner.RequireDiagnosisComposedImages(missing));
            owner.RequireDiagnosisComposedImages(writes);
            await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync((await fixture.ReadManifestAsync(lease)).Files);
        }
        var historyText = (await files.ReadFileAsync(WoundHistoryState.HistoryPath))!;
        var history = WoundHistoryState.Parse(historyText, WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, string.Join(";", history.Issues));
        var diagnosis = Assert.Single(history.State!.Transitions.Where(t => t.TransitionResult is SpiritualDiagnosisPersistedResult));
        var result = Assert.IsType<SpiritualDiagnosisPersistedResult>(diagnosis.TransitionResult);
        var natural = Assert.Single(history.State.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>());
        Assert.True(natural.IsNatural);
        Assert.Equal("diagnose", diagnosis.Kind); Assert.NotNull(diagnosis.AttemptId); Assert.Null(diagnosis.CycleKey);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(before), WoundMaterializationContract.SerializeCanonical(result.Original));
        Assert.Equal("known_to_player", Assert.Single(result.KnowledgeAfter.Complications).Visibility);
        Assert.Equal(before.Recovery, result.KnowledgeAfter.Recovery);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(result.KnowledgeAfter),
            WoundMaterializationContract.SerializeCanonical(natural.Stages.First(s => s.Before.WoundId == before.WoundId).Before));
        if (tier == 0)
        {
            var current = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
            Assert.Equal(2, current.Severity.Rank); Assert.Equal(1, current.Recovery.CurrentStepProgress);
            Assert.Equal("known_to_player", Assert.Single(current.Complications).Visibility);
        }
        else
        {
            Assert.Equal(5, result.Program.NaturalMembers.Single().HealingTier);
            Assert.Empty((await fixture.ReadWoundsAsync()).Occurrences);
            Assert.Equal("heal", natural.Stages[^1].Kind);
            Assert.Equal(3, result.Original.Recovery.CurrentStepProgress);
        }
        Assert.Null(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Null(await files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
        var settled = fixture.CaptureMutationRoots();
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            Assert.True(await scheduler.IsPublishedSpiritualCycleAsync(control, lease,
                (await fixture.ReadManifestAsync(lease)).ManifestPayloadHash, retained));
        fixture.AssertMutationRootsUnchanged(settled);
        Assert.DoesNotContain(await validator.ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(await validator.ValidateAcceptedTurnReasoningAsync(), i => i.Severity == IssueSeverity.Error);

        Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control,
            "session_spiritual_healing", "request_spiritual_healing", 43, path => files.ReadFileBytesAsync(path),
            result.Binding["snapshotToken"]!.GetValue<string>(), retained));
        fixture.AssertMutationRootsUnchanged(settled);
        var staleControl = JsonSerializer.Deserialize<ProgressionControl>(JsonSerializer.Serialize(control))!;
        staleControl.ChaosSeaCyclesExpectedThisTurn = staleControl.ChaosSeaCyclesExpectedThisTurn == 0 ? 1 : 0;
        await Assert.ThrowsAsync<InvalidDataException>(() => ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(staleControl,
            "session_spiritual_healing", "request_spiritual_healing", 43, path => files.ReadFileBytesAsync(path),
            result.Binding["snapshotToken"]!.GetValue<string>(), retained));
        fixture.AssertMutationRootsUnchanged(settled);

        foreach (var fault in new[] { "original", "knowledge", "empty_suffix", "extra_suffix", "binding", "source", "reordered_suffix", "missing_natural", "path_claim", "requirement_claim" })
        {
            if (fault == "reordered_suffix" && natural.Stages.Count < 2) continue;
            var root = JsonNode.Parse(historyText)!.AsObject();
            var transition = root["transitions"]!.AsArray().OfType<JsonObject>().Single(t => t["kind"]?.GetValue<string>() == "diagnose");
            var value = transition["transitionResult"]!.AsObject();
            switch (fault)
            {
                case "original": value["original"]!["recovery"]!["currentStepProgress"] = 2; break;
                case "knowledge": value["knowledgeAfter"]!["complications"]![0]!["visibility"] = "hidden"; break;
                case "empty_suffix": value["naturalSuffix"]!["transitionIds"]!.AsArray().Clear(); break;
                case "extra_suffix": value["naturalSuffix"]!["transitionIds"]!.AsArray().Add("foreign_suffix_transition"); break;
                case "binding": value["binding"]!["requestId"] = "foreign_request"; break;
                case "source": value["originalSources"]![0]!["fingerprint"] = "sha256:" + new string('a', 64); break;
                case "reordered_suffix":
                    value["naturalSuffix"]!["transitionIds"] = new JsonArray(natural.Stages.Reverse().Select(t => (JsonNode)t.TransitionId).ToArray()); break;
                case "path_claim":
                case "requirement_claim":
                    var field = fault == "path_claim" ? "pathFingerprint" : "requirementFingerprint";
                    value["admission"]![field] = "sha256:" + new string('c', 64);
                    var changedRequest = value["request"]!.AsObject();
                    changedRequest["diagnosis"]![field] = value["admission"]![field]!.DeepClone();
                    var changedSelection = SpiritualDiagnosisRequestCodec.ReadSelection(retained);
                    if (fault == "path_claim") changedSelection = changedSelection with { Diagnosis = changedSelection.Diagnosis! with
                        { ExpectedDiagnosisPathFingerprint = value["admission"]![field]!.GetValue<string>() } };
                    changedRequest["diagnosis"]!["attemptId"] = "spiritual_diagnosis_attempt_" + WoundAcceptedTurnFingerprintWriter.Compute(new[]
                    { "spiritual_diagnosis_request", "1", "session_spiritual_healing", "request_spiritual_healing", "43",
                        control.NextChaosSeaTurnOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        WoundAcceptedTurnFingerprintWriter.CanonicalJson(changedSelection.ToJson()) })[7..];
                    changedRequest.Remove("requestFingerprint");
                    changedRequest["requestFingerprint"] = WoundAcceptedTurnFingerprintWriter.Compute(new[] { "spiritual_diagnosis_request", "1",
                        WoundAcceptedTurnFingerprintWriter.CanonicalJson(changedRequest) });
                    break;
                case "missing_natural":
                    root["transitions"]!.AsArray().OfType<JsonObject>().Single(t => t["transitionId"]?.GetValue<string>() == natural.Stages[0].TransitionId).Remove("transitionResult"); break;
            }
            value["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(value);
            var rejected = WoundHistoryState.Parse(root.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.False(rejected.IsValid);
            Assert.Contains(rejected.Issues, i => i.Code == "spiritual_diagnosis_history_invalid");
        }
    }

    internal static async Task SeedDiagnosisBeforeBootstrapAsync(FileSystemManager files, JsonObject profiles)
    {
        var profile = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == "player_soul");
        var node = Assert.IsType<JsonObject>(Assert.Single(profile["activeWounds"]!.AsArray())).DeepClone().AsObject();
        node["complications"] = new JsonArray(new JsonObject { ["complicationId"] = "native_hidden", ["kind"] = "spiritual_instability",
            ["state"] = "active", ["displayName"] = "Скрытая неустойчивость", ["treatmentDifficultyModifier"] = 0,
            ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden" });
        node["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
        {
            ["diagnosisPathId"] = "native_diagnosis", ["displayName"] = "Осмотреть духовную рану", ["visibility"] = "public",
            ["requiresKnownFacts"] = new JsonArray(), ["requirements"] = new JsonArray(new JsonObject
            { ["kind"] = "source_capability", ["capabilityRef"] = "spiritual_healing", ["actorRole"] = "provider" }),
            ["check"] = new JsonObject(), ["reveals"] = new JsonArray("complication:native_hidden"), ["failurePolicy"] = "no_reveal"
        });
        Assert.True(AfterlifeEntityProfileState.TryNormalizeEffectRealm(profile["realm"]!.GetValue<string>(), out var realm));
        var seed = WoundPlayerProjectionTestData.InputInRealm(realm, node);
        profile["activeWounds"] = seed.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(seed.Identities));
        await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(seed.History));
    }
}
