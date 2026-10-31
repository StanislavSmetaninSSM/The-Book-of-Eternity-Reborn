using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task DiagnosisSelectedSecondKeepsCompletePrivateCohortRecoveryAndOrderedHistory()
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(20, signedInput: true, duration: 30, twoMembers: true);
        var owner = fixture.Authority;
        var context = await WoundPlayerReadService.LoadContextAsync(fixture.Files, fixture.Lease);
        Assert.NotNull(context); Assert.True(context.Projection.IsAvailable);
        Assert.DoesNotContain("ПРИВАТНАЯ_РАНА", JsonSerializer.Serialize(context.Projection));
        var request = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner,
            new(owner.History, Array.Empty<ValidationIssue>()), owner.CurrentWound,
            "diagnosis_complete_two_member_cohort", "inspect_explicit_method", fixture.EventRef);
        Assert.True(request.IsValid, JsonSerializer.Serialize(request.Issues));
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, new(owner.History, Array.Empty<ValidationIssue>()), request.Request!);
        var working = MortalWoundDiagnosisWorkingProjection.Build(owner, outcome);
        Assert.Equal(2, working.Members.Length);
        Assert.NotEqual(owner.CurrentWound.WoundId, working.Members[0].Original.WoundId);
        Assert.Equal(owner.CurrentWound.WoundId, working.Members[1].Original.WoundId);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(working.Members[0].Original),
            WoundMaterializationContract.SerializeCanonical(working.Members[0].Working));
        Assert.Contains("diagnostic_hidden_route", working.Members[1].Working.Treatment.KnownRouteIds);
        Assert.DoesNotContain("diagnostic_hidden_route", working.Members[0].Working.Treatment.KnownRouteIds);
        Assert.All(working.Members, member => Assert.Equal(3, member.Resolution.ElapsedCadences));
        var progression = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .PrepareDiagnosisOutcomeAsync(fixture.Lease, working);
        var originalFiles = fixture.CaptureJson();
        var prepared = WoundAcceptedTurnPlanner.PrepareMortalDiagnosisContinuation(owner, outcome, working, progression);
        Assert.True(prepared.Success, JsonSerializer.Serialize(prepared.Issues));
        var effects = WoundEffectBatchPlanner.Build(prepared.Plan!, WoundAcceptedTurnPlanner.ComposeMortalDiagnosisEffectInput(prepared.Plan!), new EffectIdentityFactory());
        Assert.True(effects.Success, JsonSerializer.Serialize(effects.Issues));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalDiagnosisExecution(prepared.Plan!, effects.Plan!.EffectPlan, out var stages));
        Assert.Equal("diagnose", stages[0].Kind); Assert.Equal(owner.CurrentWound.WoundId, stages[0].After.WoundId);
        Assert.Equal(working.Members.Select(member => member.Original.WoundId), stages.Skip(1).Select(stage => stage.After.WoundId));
        Assert.All(stages.Skip(1), stage => Assert.Equal("recover", stage.Kind));
        var finalized = WoundAcceptedTurnPlanner.Finalize(prepared.Plan!, effects);
        Assert.True(finalized.Success, JsonSerializer.Serialize(finalized.Issues));
        var final = finalized.Plan!;
        var fullHistory = WoundHistoryState.Parse(final.HistoryAfterImage.ToJsonString(), WoundHistoryState.HistoryPath);
        var identities = WoundIdentityState.Parse(final.IdentityIndexAfterImage.ToJsonString(), WoundIdentityState.StatePath);
        Assert.True(fullHistory.IsValid, JsonSerializer.Serialize(fullHistory.Issues)); Assert.True(identities.IsValid);
        var primary = Assert.Single(fullHistory.State!.Transitions, row => row.TransitionResult is MortalWoundDiagnosisPersistedResult);
        var result = Assert.IsType<MortalWoundDiagnosisPersistedResult>(primary.TransitionResult);
        Assert.Equal(2, result.Recoveries.Length);
        var retainedOrder = result.Recoveries.Select(member => member.Original.WoundId).ToArray();
        Assert.Equal(working.Members.Select(member => member.Original.WoundId), retainedOrder);
        Assert.All(result.Recoveries, member => Assert.Single(member.Stages));
        var carriers = context.Input.Carriers with { PlayerWounds = context.Input.Carriers.PlayerWounds!.DeepClone().AsObject() };
        foreach (var contribution in final.CarrierContributions)
        {
            var collection = WoundCarrierCollectionAuthority.Resolve(carriers, contribution.Owner);
            foreach (var mutation in contribution.Mutations) Assert.Null(AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, contribution.Owner, mutation));
        }
        Assert.Empty(fullHistory.State.ValidateAgreement(identities.State!, WoundCarrierCatalog.Build(carriers)));
        var projected = WoundPlayerProjection.Build(context.Input with { Carriers = carriers, Identities = identities.State!, History = fullHistory.State });
        Assert.True(projected.IsAvailable); Assert.DoesNotContain("ПРИВАТНАЯ_РАНА", JsonSerializer.Serialize(projected));
        foreach (var fault in new[] { "omit_private", "reorder" })
        {
            var changed = final.HistoryAfterImage;
            var row = changed["transitions"]!.AsArray().OfType<JsonObject>().Single(value => value["kind"]!.GetValue<string>() == "diagnose");
            var root = row["transitionResult"]!.AsObject();
            var cohort = root["cohort"]!.AsArray();
            var members = working.Members.ToArray();
            if (fault == "omit_private") { cohort.RemoveAt(0); members = members.Skip(1).ToArray(); }
            else
            {
                var first = cohort[0]!.DeepClone(); var second = cohort[1]!.DeepClone(); cohort[0] = second; cohort[1] = first;
                members = members.Reverse().ToArray();
            }
            root["workingFingerprint"] = MortalWoundDiagnosisWorkingProjection.ComputeComparisonFingerprint(request.Request!, outcome.Resolution,
                owner.AcceptedStateFingerprint, working.OriginalMinute, working.ResultingMinute, working.OriginalControl, working.OriginalSchedule,
                working.ClockAfter, members);
            root["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(root);
            row["sourceFingerprint"] = root["workingFingerprint"]!.DeepClone();
            var refused = WoundHistoryState.Parse(changed.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.False(refused.IsValid);
            Assert.Contains(refused.Issues, issue => issue.Code == "mortal_wound_diagnosis_result_invalid" &&
                issue.Actual?.Contains(fault == "omit_private" ? "omitted or replaced" : "order/identity/source", StringComparison.Ordinal) == true);
        }
        fixture.AssertJson(originalFiles);
    }

    private static async Task SeedDiagnosisPrivateMemberAsync(FileSystemManager files, JsonObject selected)
    {
        var copy = selected.DeepClone().AsObject();
        var parsed = WoundMaterializationContract.Parse(selected.ToJsonString(), "selected.original").Wound!;
        var replacements = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [parsed.WoundId] = "wound_aa_private_diagnosis_cohort",
            [parsed.LastTransition.TransitionId] = parsed.LastTransition.TransitionId + "_private"
        };
        foreach (var binding in parsed.Consequences.OwnedEffectSources.RootBindings)
        { replacements[binding.EffectId] = binding.EffectId + "_private"; replacements[binding.DefinitionKey] = binding.DefinitionKey + "_private"; }
        void Rebind(JsonNode node)
        {
            if (node is JsonObject obj)
                foreach (var pair in obj.ToArray())
                {
                    if (pair.Value is JsonValue value && value.TryGetValue<string>(out var text) && replacements.TryGetValue(text, out var replaced)) obj[pair.Key] = replaced;
                    else if (pair.Value is not null) Rebind(pair.Value);
                }
            else if (node is JsonArray array)
                for (var index = 0; index < array.Count; index++)
                {
                    if (array[index] is JsonValue value && value.TryGetValue<string>(out var text) && replacements.TryGetValue(text, out var replaced)) array[index] = replaced;
                    else if (array[index] is { } child) Rebind(child);
                }
        }
        Rebind(copy); copy["display"]!["visibility"] = "gm_only"; copy["display"]!["name"] = "ПРИВАТНАЯ_РАНА";
        var wound = WoundMaterializationContract.Parse(copy.ToJsonString(), "private.original");
        Assert.True(wound.IsValid, JsonSerializer.Serialize(wound.Issues));
        var carrier = JsonNode.Parse(await files.ReadFileAsync(WoundCarrierCatalog.PlayerPath))!.AsObject();
        carrier["activeWounds"]!.AsArray().Insert(0, copy);
        await files.WriteFileAtomicAsync(WoundCarrierCatalog.PlayerPath, carrier.ToJsonString());
        var identities = JsonNode.Parse(await files.ReadFileAsync(WoundIdentityState.StatePath))!.AsObject();
        identities["entries"]!.AsArray().Add(WoundContractTestData.CreateIdentityEntry(woundId: wound.Wound!.WoundId,
            createdAtTurn: wound.Wound.Origin.CreatedAtTurn, createdEventRef: wound.Wound.Origin.EventRef,
            lastTransitionOrdinal: wound.Wound.LastTransition.Ordinal, semanticFingerprint: WoundIdentityState.ComputeSemanticFingerprint(wound.Wound)));
        await files.WriteFileAtomicAsync(WoundIdentityState.StatePath, identities.ToJsonString());
        var history = JsonNode.Parse(await files.ReadFileAsync(WoundHistoryState.HistoryPath))!.AsObject();
        var privateRow = Assert.IsType<JsonObject>(Assert.Single(CreateCurrentWoundHistory(copy)["transitions"]!.AsArray())).DeepClone().AsObject();
        privateRow["ordinal"] = history["nextOrdinal"]!.GetValue<int>(); privateRow["operationKey"] = "operation_private_diagnosis_initial";
        history["transitions"]!.AsArray().Add(privateRow); history["nextOrdinal"] = history["nextOrdinal"]!.GetValue<int>() + 1;
        await files.WriteFileAtomicAsync(WoundHistoryState.HistoryPath, history.ToJsonString());
        var effects = CreateCanonicalWoundEffects(copy).ToArray();
        var effectCarrier = JsonNode.Parse(await files.ReadFileAsync(EffectCarrierCatalog.PlayerPath))!.AsObject();
        foreach (var effect in effects) effectCarrier["activeEffects"]!.AsArray().Add(effect.DeepClone());
        await files.WriteFileAtomicAsync(EffectCarrierCatalog.PlayerPath, effectCarrier.ToJsonString());
        var indexRoot = JsonNode.Parse(await files.ReadFileAsync(EffectIdentityState.StatePath))!.AsObject();
        var added = EffectMaterializationTestFixture.CreateIdentityIndex(effects)["entries"]!.AsArray();
        for (var index = 0; index < added.Count; index++)
        {
            var entry = added[index]!.DeepClone().AsObject(); var chronology = effects[index]["chronology"]!.AsObject();
            entry["createdAtTurn"] = chronology["createdAtTurn"]!.DeepClone();
            entry["transitions"]![0]!["transitionId"] = chronology["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = chronology["createdEventRef"]!.DeepClone();
            entry["transitions"]![0]!["turn"] = chronology["createdAtTurn"]!.DeepClone();
            indexRoot["entries"]!.AsArray().Add(entry);
        }
        await files.WriteFileAtomicAsync(EffectIdentityState.StatePath, indexRoot.ToJsonString());
        foreach (var pair in new Dictionary<string, JsonObject>
        {
            [WoundCarrierCatalog.NpcPath] = new() { ["schemaVersion"] = 1, ["entries"] = new JsonArray() },
            [WoundCarrierCatalog.EnemiesPath] = new() { ["enemiesData"] = new JsonArray() },
            [WoundCarrierCatalog.AlliesPath] = new() { ["alliesData"] = new JsonArray() },
            [WoundCarrierCatalog.AfterlifeProfilesPath] = AfterlifeEntityProfileState.CreateDefaultRoot()
        })
            if (await files.ReadFileBytesAsync(pair.Key) is null) await files.WriteFileAtomicAsync(pair.Key, pair.Value.ToJsonString());
    }
}
