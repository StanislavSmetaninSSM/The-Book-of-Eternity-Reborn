using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Finds the exact independently selected singleton owner; this DATA lookup never grants execution authority.</summary>
    /// <param name="setup">The closed pre-sign role, not an inferred current owner.</param>
    /// <param name="profiles">Detached complete profile root to inspect.</param>
    /// <returns>The one exact player or guardian profile for this fixture.</returns>
    private static JsonObject ReadPressureSingletonJourneyProfile(GuardianAttributionJourney setup, JsonObject profiles) =>
        Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>(), row =>
            row["actorType"]!.GetValue<string>() == setup.AffectedKind && row["actorId"]!.GetValue<string>() == setup.AffectedId);

    /// <summary>Requires the existing eight v3/v5 fields, explicit expected rank, full parent and original raw byte identity.</summary>
    private void AssertGuardianAttributionProof(GuardianAttributionJourney setup, JsonObject proof)
    {
        Assert.Equal(new[] { "componentId", "effectId", "inputRoleRevision", "ordinaryCalculation", "ordinaryExchangeJsonBase64",
                "originalInput", "parentWoundJsonBase64", "version" },
            proof.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(setup.Version, proof["version"]!.GetValue<int>());
        Assert.Equal(setup.IsGuardian ? SpiritualGuardianAttributionTestData.Role : "existing_pressure_ordinary_v1", proof["inputRoleRevision"]!.GetValue<string>());
        var input = proof["originalInput"]!.AsObject();
        Assert.Equal(new[] { "contentFingerprint", "exchangePointer", "existed", "path" },
            input.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, input["path"]!.GetValue<string>());
        Assert.True(input["existed"]!.GetValue<bool>());
        Assert.Equal("/activeConflict/exchangeLog/0", input["exchangePointer"]!.GetValue<string>());
        Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
            input["contentFingerprint"]!.GetValue<string>());
        var ordinaryJson = SpiritualWoundStateJson.DecodeUtf8JsonText(Convert.FromBase64String(proof["ordinaryExchangeJsonBase64"]!.GetValue<string>()));
        Assert.Equal(SpiritualWoundStateJson.Canonical(setup.Ordinary), ordinaryJson);
        Assert.True(JsonNode.DeepEquals(setup.Ordinary, JsonNode.Parse(ordinaryJson)));
        var parentJson = SpiritualWoundStateJson.DecodeUtf8JsonText(Convert.FromBase64String(proof["parentWoundJsonBase64"]!.GetValue<string>()));
        Assert.True(JsonNode.DeepEquals(Assert.Single(setup.Retained.Parents), JsonNode.Parse(parentJson)));
        var parsed = WoundMaterializationContract.Parse(parentJson, "guardian.issuedParent");
        RequireGuardianJourneyIssues(setup.Name, "issued_parent", parsed.Issues);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        var parent = parsed.Wound!;
        Assert.Equal(parentJson, WoundMaterializationContract.SerializeCanonical(parent));
        Assert.Equal(("active", "spiritual", setup.ExpectedRank), (parent.Lifecycle, parent.Classification.Domain, parent.Severity.Rank));
        var root = Assert.Single(parent.Consequences.OwnedEffectSources.RootBindings);
        Assert.Equal(root.EffectId, proof["effectId"]!.GetValue<string>());
        Assert.Equal("component_001", proof["componentId"]!.GetValue<string>());
        var calculation = proof["ordinaryCalculation"]!.AsObject();
        Assert.Equal(new[] { "maximumSeverityRank", "traumaPressure" }, calculation.Select(row => row.Key).OrderBy(key => key, StringComparer.Ordinal));
        Assert.Equal((setup.OrdinaryCalculation.TraumaPressure, setup.OrdinaryCalculation.MaximumSeverityRank), (calculation["traumaPressure"]!.GetValue<long>(), calculation["maximumSeverityRank"]!.GetValue<int>()));
    }

    /// <summary>Checks literal role-specific v3/v5 source direction, calculation and declaration presence, independently of issued DATA.</summary>
    private void AssertGuardianAttributionWitness(GuardianAttributionJourney setup, JsonObject witness)
    {
        Assert.Equal(setup.ActualCalculation, SpiritualWoundSourceWitness.ValidateAndReadStrainData(witness, out var strain));
        Assert.NotNull(strain); Assert.Equal(setup.Version, strain.Version); Assert.Null(strain.ParentSourceId);
        Assert.Equal(setup.OrdinaryCalculation, strain.OrdinaryCalculation);
        Assert.Equal(setup.ActualCalculation, strain.ActualCalculation);
        Assert.Equal("/activeConflict/exchangeLog/0", strain.OriginalExchangePointer);
        Assert.Equal(new CanonicalBeforeImage(true, setup.Authored[AfterlifeSpiritualConflictState.StatePath]).Fingerprint,
            strain.OriginalInputFingerprint);
        Assert.True(JsonNode.DeepEquals(setup.Actual, strain.ReadActualExchange()));
        Assert.True(JsonNode.DeepEquals(setup.Ordinary["after"], strain.ReadOrdinaryAfter(setup.Actual)));
        Assert.True(JsonNode.DeepEquals(Assert.Single(setup.Retained.Parents),
            JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(Assert.Single(strain.Parents).ParentWound))));
        AssertGuardianAttributionProof(setup, witness["strainPreimage"]!.AsObject());
        Assert.True(JsonNode.DeepEquals(setup.Actual,
            ReadGuardianAttributionJson(Convert.FromBase64String(witness["exchangeJsonBase64"]!.GetValue<string>()))));
        Assert.Equal(("activeConflict.exchangeLog[0]", setup.AffectedSide, "pressure", 0, 0),
            (witness["coordinate"]!.GetValue<string>(), witness["affectedSide"]!.GetValue<string>(), witness["operation"]!.GetValue<string>(),
                witness["exchangeOrdinal"]!.GetValue<int>(), witness["sourceOrdinal"]!.GetValue<int>()));
        Assert.Equal(setup.ExchangeId, witness["exchangeId"]!.GetValue<string>());
        Assert.Equal(setup.Copy.Conflict["activeConflict"]!["conflictId"]!.GetValue<string>(), witness["conflictId"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["actorKind"] = setup.ActingKind, ["actorId"] = setup.ActingId }, witness["actingActor"]));
        Assert.True(JsonNode.DeepEquals(new JsonObject { ["actorKind"] = setup.AffectedKind, ["actorId"] = setup.AffectedId }, witness["affectedActor"]));
        if (setup.IsGuardian)
            Assert.True(JsonNode.DeepEquals(SpiritualGuardianAttributionTestData.FailedAction(), strain.ReadActualExchange()["spiritualWoundFailedAction"]));
        else
            Assert.False(strain.ReadActualExchange().ContainsKey("spiritualWoundFailedAction"));
        Assert.Null(witness["guaranteedSeverityRank"]); Assert.Null(witness["retraumaWoundId"]);
        var evidence = witness["turnEvidence"]!;
        Assert.Equal(("session_" + setup.Name, "request_" + setup.Name, 46, "chaos_sea"),
            (evidence["sessionId"]!.GetValue<string>(), evidence["requestId"]!.GetValue<string>(),
                evidence["turn"]!.GetValue<int>(), evidence["realm"]!.GetValue<string>()));
        Assert.Equal(setup.Dice, evidence["acceptedD20Values"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, witness["selectedDiceIndices"]!.AsArray().Select(row => row!.GetValue<int>()));
        Assert.Equal(new[] { 0, 1 }, evidence["diceClaims"]!.AsArray().Select(row => row!["sourceIndex"]!.GetValue<int>()));
        Assert.Equal(setup.Dice, evidence["diceClaims"]!.AsArray().Select(row => row!["value"]!.GetValue<int>()));
        Assert.Equal((1, 2, 2), (evidence["bounds"]!["exchangeCount"]!.GetValue<int>(),
            evidence["bounds"]!["sourceSlotCount"]!.GetValue<int>(), evidence["bounds"]!["diceCount"]!.GetValue<int>()));
    }

    /// <summary>The complete ordinary root is recovered by restoring exactly the selected exchange and final affected-side strain scalars.</summary>
    private static void AssertGuardianAttributionOnlyProjections(GuardianAttributionJourney setup, JsonNode actualRoot)
    {
        Assert.True(JsonNode.DeepEquals(setup.ActualRoot, actualRoot));
        var restored = actualRoot.DeepClone().AsObject();
        Assert.Equal("overwhelmed", restored["activeConflict"]!["exchangeLog"]![0]!["after"]![setup.StrainField]!.GetValue<string>());
        Assert.Equal("overwhelmed", restored["activeConflict"]![setup.StrainField]!.GetValue<string>());
        restored["activeConflict"]!["exchangeLog"]![0]!["after"]![setup.StrainField] = "fractured";
        restored["activeConflict"]![setup.StrainField] = "fractured";
        Assert.True(JsonNode.DeepEquals(setup.Copy.Conflict, restored));
    }

    /// <summary>Receipt schema1 retains every historical record and appends precisely one actual v3/v5 source and none decision, joined to full History.</summary>
    private void AssertGuardianAttributionReceiptAppend(GuardianAttributionJourney setup, JsonObject receipt, JsonObject witness)
    {
        var before = JsonNode.Parse(setup.Origin.ReceiptJson)!;
        Assert.Equal(1, receipt["schemaVersion"]!.GetValue<int>());
        foreach (var field in new[] { "sources", "decisions" })
        {
            Assert.Single(before[field]!.AsArray()); Assert.Equal(2, receipt[field]!.AsArray().Count);
            Assert.True(JsonNode.DeepEquals(before[field]![0], receipt[field]![0]));
        }
        Assert.Single(before["instances"]!.AsArray()); Assert.Equal(2, receipt["instances"]!.AsArray().Count);
        Assert.Single(before["closures"]!.AsArray()); Assert.Single(receipt["closures"]!.AsArray());
        foreach (var field in new[] { "instances", "closures" })
            Assert.True(JsonNode.DeepEquals(before[field]![0], receipt[field]![0]));
        foreach (var field in new[] { "nextInstanceOrdinal", "nextSourceOrdinal", "nextDecisionOrdinal" })
            Assert.Equal(before[field]!.GetValue<int>() + 1, receipt[field]!.GetValue<int>());
        Assert.Equal(before["nextClosureOrdinal"]!.GetValue<int>(), receipt["nextClosureOrdinal"]!.GetValue<int>());
        Assert.True(JsonNode.DeepEquals(witness, receipt["sources"]![1]!["witness"]));
        var decision = receipt["decisions"]![1]!;
        Assert.Equal("none", decision["decision"]!.GetValue<string>());
        Assert.Equal(witness["sourceId"]!.GetValue<string>(), decision["sourceId"]!.GetValue<string>());
        Assert.Null(decision["woundId"]); Assert.Null(decision["selectedSeverityRank"]);
        Assert.True(JsonNode.DeepEquals(witness, decision["sourceWitness"]));
        var history = WoundHistoryState.Parse(SpiritualWoundStateJson.DecodeUtf8JsonText(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
        var ledger = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        RequireGuardianJourneyIssues(setup.Name, "receipt_original_history", history.Issues);
        RequireGuardianJourneyIssues(setup.Name, "receipt_append_ledger", ledger.Issues);
        Assert.True(history.IsValid, string.Join("\n", history.Issues));
        Assert.True(ledger.IsValid, string.Join("\n", ledger.Issues));
        Assert.True(ledger.State!.HasExactStrainHistory(history.State!));
    }

    /// <summary>Registers and publishes through common C4 only after every warm/cold owner and its phase lease has closed.</summary>
    private async Task PublishGuardianAttributionAndReadNextAsync(GuardianAttributionJourney setup, ValidationService validator,
        GuardianAttributionCompleted completed)
    {
        var context = setup.Copy.Context;
        var started = Stopwatch.GetTimestamp();
        RequireGuardianJourneyIssues(setup.Name, "common_plan", await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        AssertGuardianAttributionRegisteredPlan(setup, completed, plan);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
            context.FileSystem, context.Normalizer, validator, await ReadSpiritualC4BackupsAsync(context));
        RequireGuardianJourneyIssues(setup.Name, "common_c4", published.Issues);
        Assert.Same(plan, published.MechanicsPlan);
        await AssertGuardianAttributionPublishedAsync(setup, completed, plan);
        WritePressureOwnerTiming("guardian_journey_common_c4", setup.Name, started);
        await ReadGuardianAttributionSignedNextAsync(setup, validator);
    }

    /// <summary>Complete C3 DATA equals the common plan, including every event, resource, effect and owner companion.</summary>
    private static void AssertGuardianAttributionRegisteredPlan(GuardianAttributionJourney setup,
        GuardianAttributionCompleted completed, AcceptedMechanicsPlan plan)
    {
        var actualRoot = JsonNode.Parse(completed.ActualRootJson)!.AsObject();
        AssertGuardianAttributionOnlyProjections(setup, actualRoot);
        AssertPressureExistingResourcePlan(plan, actualRoot); // Exactly one AP3 spend and one depletion per side.
        Assert.True(JsonNode.DeepEquals(actualRoot, plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.Resources.StateAfterImage!.ToCanonicalJson()), plan.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.Resources.HistoryAfterImage!.ToCanonicalJson()), plan.HistoryAfterImage));
        Assert.Equal(completed.Resources.Events.OrderBy(row => row.Turn).ThenBy(row => row.ExecutionSequence)
            .ThenBy(row => row.EventKind, StringComparer.Ordinal).ThenBy(row => row.OperationId, StringComparer.Ordinal), plan.ResourceEvents);
        Assert.Equal(completed.EffectPayloadFingerprint,
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan)));
        Assert.False(plan.EffectPlan!.HasPressureConflictCarrierCollision());
        Assert.Equal(2, completed.Resources.AppliedTransitions.Count);
        Assert.Empty(completed.Resources.ReplayTransitions);
    }

    /// <summary>Reads every published image and immutable parent/History, one receipt append and the one lawful affected-owner identity append.</summary>
    private async Task AssertGuardianAttributionPublishedAsync(GuardianAttributionJourney setup,
        GuardianAttributionCompleted completed, AcceptedMechanicsPlan plan)
    {
        var context = setup.Copy.Context;
        foreach (var row in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(row.Value, await context.ReadJsonAsync(row.Key)), row.Key);
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
        AssertGuardianAttributionOnlyProjections(setup, (await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath))!);
        foreach (var path in new[] { WoundIdentityState.StatePath, WoundHistoryState.HistoryPath })
            Assert.Equal(setup.Origin.Tracked[path], await context.FileSystem.ReadFileBytesAsync(path));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var affected = ReadPressureSingletonJourneyProfile(setup, profiles);
        Assert.True(JsonNode.DeepEquals(Assert.Single(setup.Retained.Parents), Assert.Single(affected["activeWounds"]!.AsArray())));
        var effect = Assert.Single(affected["activeEffects"]!.AsArray())!;
        var baselineEffect = Assert.Single(setup.Retained.Effects);
        var restoredProfiles = profiles.DeepClone().AsObject();
        Assert.Single(ReadPressureSingletonJourneyProfile(setup, restoredProfiles)["activeEffects"]!.AsArray())!["chronology"] = baselineEffect["chronology"]!.DeepClone();
        var diagnosticStarted = Stopwatch.GetTimestamp();
        var diagnosticOriginBytes = setup.Origin.Tracked[AfterlifeEntityProfileState.StatePath]!;
        var diagnosticCurrentBytes = await context.FileSystem.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath);
        var diagnosticOrigin = ReadGuardianAttributionJson(diagnosticOriginBytes);
        var identity = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject();
        var beforeIdentity = JsonNode.Parse(setup.Origin.IdentityJson)!.AsObject();
        var diagnosticProfileChanges = new JsonArray();
        var diagnosticRestoredChanges = new JsonArray();
        var diagnosticEffectChanges = new JsonArray();
        var diagnosticIdentityChanges = new JsonArray();
        AppendPressurePairJsonDifferences(diagnosticOrigin, true, profiles, true, "", diagnosticProfileChanges);
        AppendPressurePairJsonDifferences(diagnosticOrigin, true, restoredProfiles, true, "", diagnosticRestoredChanges);
        AppendPressurePairJsonDifferences(baselineEffect, true, effect, true, "", diagnosticEffectChanges);
        AppendPressurePairJsonDifferences(beforeIdentity, true, identity, true, "", diagnosticIdentityChanges);
        var diagnosticTranscript = completed.Resources.EffectBoundaryTranscript as AcceptedEffectBoundaryTranscript;
        // Complete native test output retains every operand without quotas; no DATA is projected into an expected root.
        _storageOutput.WriteLine(JsonSerializer.Serialize(new
        {
            phase = "guardian_c4_profile_identity_operands", setup.Name,
            originProfiles = diagnosticOrigin, currentProfiles = profiles, restoredProfiles, baselineEffect, currentEffect = effect,
            originProfileBytesBase64 = Convert.ToBase64String(diagnosticOriginBytes),
            originProfileFingerprint = new CanonicalBeforeImage(true, diagnosticOriginBytes).Fingerprint,
            currentProfileBytesBase64 = diagnosticCurrentBytes is null ? null : Convert.ToBase64String(diagnosticCurrentBytes),
            currentProfileFingerprint = new CanonicalBeforeImage(diagnosticCurrentBytes is not null, diagnosticCurrentBytes).Fingerprint,
            beforeIdentity, currentIdentity = identity,
            planProfileImages = new
            {
                woundCarrierAfterImages = plan.WoundCarrierAfterImages.Where(row => row.Key == AfterlifeEntityProfileState.StatePath),
                effectCarrierAfterImages = plan.EffectCarrierAfterImages.Where(row => row.Key == AfterlifeEntityProfileState.StatePath),
                ownerCompanionAfterImages = plan.OwnerCompanionAfterImages.Where(row => row.Key == AfterlifeEntityProfileState.StatePath)
            },
            planEffectIdentity = plan.EffectIdentityAfterImage,
            resourceTriggerExecutions = completed.Resources.ResourceTriggerExecutions.Select(row => new
            {
                row.EffectId, row.TriggerId, row.EventKind, row.EventRef, row.MutationKeys,
                row.RemainingUseBudget, row.ComponentIds, row.TriggerEventRef,
                componentIdsByMutation = row.ComponentIdsByMutation?.Select(pair => new { pair.Key, pair.Value })
            }),
            acceptedActivations = diagnosticTranscript?.AcceptedActivations.Select(row => new
                { row.Boundary, row.Activation, row.CandidateFingerprint, row.MechanicsOrdinal }),
            completedReceiptJson = completed.ReceiptJson, completedWitnessJson = completed.WitnessJson,
            originWoundHistoryBytesBase64 = Convert.ToBase64String(setup.Origin.Tracked[WoundHistoryState.HistoryPath]!),
            originToPublishedProfiles = diagnosticProfileChanges, originToRestoredProfiles = diagnosticRestoredChanges,
            factoryToPublishedEffect = diagnosticEffectChanges, originToPublishedIdentity = diagnosticIdentityChanges
        }));
        WritePressureOwnerTiming("guardian_c4_profile_identity_diagnostic_capture", setup.Name, diagnosticStarted);
        // ProjectCanonicalRoot adds schema1 and the uninjured profile's absent arrays. The retained owner already has both arrays.
        // This expected root uses only signed-origin DATA plus those explicit defaults, never a production projection or actual after-image.
        var expectedCanonicalProfiles = diagnosticOrigin.DeepClone().AsObject();
        Assert.False(expectedCanonicalProfiles.ContainsKey("schemaVersion"));
        expectedCanonicalProfiles["schemaVersion"] = 1;
        var otherKind = setup.IsGuardian ? "player_soul" : "guardian";
        var otherId = setup.IsGuardian ? "player_soul" : SpiritualGuardianAttributionTestData.GuardianId;
        var expectedOther = Assert.Single(expectedCanonicalProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>(),
            row => row["actorType"]?.GetValue<string>() == otherKind && row["actorId"]?.GetValue<string>() == otherId);
        var publishedOther = Assert.Single(profiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>(),
            row => row["actorType"]?.GetValue<string>() == otherKind && row["actorId"]?.GetValue<string>() == otherId);
        Assert.Equal(1, profiles["schemaVersion"]!.GetValue<int>());
        foreach (var field in new[] { "activeEffects", "activeWounds" })
        {
            Assert.False(expectedOther.ContainsKey(field));
            expectedOther[field] = new JsonArray();
            Assert.Empty(Assert.IsType<JsonArray>(publishedOther[field]));
        }
        var expectedCanonicalChanges = new JsonArray();
        AppendPressurePairJsonDifferences(expectedCanonicalProfiles, true, restoredProfiles, true, "", expectedCanonicalChanges);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_c4_expected_canonical_profiles", setup.Name,
            expectedCanonicalProfiles, expectedToRestoredProfiles = expectedCanonicalChanges }));
        Assert.True(JsonNode.DeepEquals(expectedCanonicalProfiles, restoredProfiles));
        var effectId = baselineEffect["effectId"]!.GetValue<string>();
        var beforeEntry = Assert.Single(beforeIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var entry = Assert.Single(identity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var transitions = entry["transitions"]!.AsArray();
        Assert.Equal(beforeEntry["transitions"]!.AsArray().Count + 1, transitions.Count);
        for (var index = 0; index < transitions.Count - 1; index++)
            Assert.True(JsonNode.DeepEquals(beforeEntry["transitions"]![index], transitions[index]));
        var last = transitions[^1]!;
        Assert.Equal(("trigger", 46), (last["kind"]!.GetValue<string>(), last["turn"]!.GetValue<int>()));
        Assert.Equal((46, last["transitionId"]!.GetValue<string>()),
            (effect["chronology"]!["lastTransitionTurn"]!.GetValue<int>(), effect["chronology"]!["lastTransitionId"]!.GetValue<string>()));
        var expectedChronology = baselineEffect["chronology"]!.DeepClone();
        expectedChronology["lastTransitionTurn"] = 46;
        expectedChronology["lastTransitionId"] = last["transitionId"]!.GetValue<string>();
        Assert.True(JsonNode.DeepEquals(expectedChronology, effect["chronology"]));
        var execution = Assert.Single(completed.Resources.ResourceTriggerExecutions, row => row.EffectId == effectId);
        Assert.Equal(("on_owner_turn_end", "owner_turn_end"), (execution.TriggerId, execution.EventKind));
        // Passive strain/roll/cost profiles keep the full trigger graph but execute no resource mutation components here.
        Assert.Empty(execution.MutationKeys);
        Assert.NotNull(execution.ComponentIds); Assert.Empty(execution.ComponentIds);
        // Effect targets use player, while the same player's wound/profile/source actor kind remains player_soul.
        var expectedEffectTargetKind = setup.IsGuardian ? "guardian" : "player";
        var expectedEffectTarget = new JsonObject { ["kind"] = expectedEffectTargetKind, ["targetId"] = setup.AffectedId };
        Assert.Equal("chaos_sea", beforeEntry["realm"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(expectedEffectTarget, beforeEntry["target"]));
        var effectPlan = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        var ownerEvent = Assert.Single(effectPlan.EventInput["lifecycleEvents"]!.AsArray().OfType<JsonObject>(), row =>
            row["phase"]!.GetValue<string>() == "owner_turn_end" && row["realm"]!.GetValue<string>() == "chaos_sea" &&
            JsonNode.DeepEquals(row["target"], expectedEffectTarget));
        Assert.Equal(46, ownerEvent["turn"]!.GetValue<int>());
        var ownerEventRef = ownerEvent["eventRef"]!.GetValue<string>();
        Assert.Equal("turn_46:lifecycle:owner_turn_end:chaos_sea:" + expectedEffectTargetKind + ":" + setup.AffectedId, ownerEventRef);
        Assert.Equal(ownerEventRef, execution.TriggerEventRef);
        Assert.Equal(execution.EventRef, last["eventRef"]!.GetValue<string>());
        Assert.Null(execution.RemainingUseBudget);
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(completed.Resources.EffectBoundaryTranscript);
        var accepted = Assert.Single(transcript.AcceptedActivations, row => row.Activation.Stamp.Identity.EffectId == effectId);
        Assert.Equal(new EffectActivationCandidateIdentity(effectId, execution.TriggerId, execution.EventKind, execution.EventRef, execution.TriggerEventRef!),
            accepted.Activation.Stamp.Identity);
        Assert.Equal(new ResourcePendingAuthorityBinding("permanent", effectId), accepted.Activation.Stamp.EffectAuthority);
        Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(ownerEventRef, effectId,
            accepted.Activation.Stamp.EffectAuthority, execution.TriggerId), execution.EventRef);
        Assert.False(accepted.Activation.Stamp.ConsumesUse); Assert.Null(accepted.Activation.Stamp.UsesBefore); Assert.Null(accepted.Activation.UsesAfter);
        var restoredIdentity = identity.DeepClone().AsObject();
        Assert.Single(restoredIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!["transitions"] = beforeEntry["transitions"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(beforeIdentity, restoredIdentity));
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), receipt));
        var witness = JsonNode.Parse(completed.WitnessJson)!.AsObject();
        AssertGuardianAttributionReceiptAppend(setup, receipt, witness);
        AssertGuardianAttributionWitness(setup, receipt["sources"]![1]!["witness"]!.AsObject());
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        RequireGuardianJourneyIssues(setup.Name, "published_effect_mechanics", mechanics.Issues);
        Assert.True(mechanics.IsAccepted, string.Join("\n", mechanics.Issues));
        Assert.Equal(setup.ExpectedRank, mechanics.Components.Count(row => row.EffectId == effectId));
        await ExecuteGuardianLeaseStagesAsync(context, lease =>
        {
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease));
            return Task.CompletedTask;
        });
    }

    /// <summary>Signs only genuinely published state for turn47 and reads every snapshot input BOM-aware before real signed Frame validation.</summary>
    private async Task ReadGuardianAttributionSignedNextAsync(GuardianAttributionJourney setup, ValidationService validator)
    {
        var context = setup.Copy.Context;
        var started = Stopwatch.GetTimestamp();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 47, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5],
            requestId: "request_" + setup.Name + "_published_read_47");
        var manifestBytes = (await context.FileSystem.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
        var manifest = ReadGuardianAttributionJson(manifestBytes);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_signed47_manifest", setup.Name,
            manifest, manifestBytesBase64 = Convert.ToBase64String(manifestBytes),
            manifestFingerprint = new CanonicalBeforeImage(true, manifestBytes).Fingerprint }));
        Assert.Equal(47, manifest["turnNumber"]!.GetValue<int>());
        foreach (var path in PressureOrdinarySignedPaths)
        {
            var current = await context.FileSystem.ReadFileBytesAsync(path);
            var signed = await context.FileSystem.ReadFileBytesAsync(manifest["files"]![path]!.GetValue<string>());
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_signed47_input", setup.Name, path,
                currentBytesBase64 = current is null ? null : Convert.ToBase64String(current),
                currentFingerprint = new CanonicalBeforeImage(current is not null, current).Fingerprint,
                signedBytesBase64 = signed is null ? null : Convert.ToBase64String(signed),
                signedFingerprint = new CanonicalBeforeImage(signed is not null, signed).Fingerprint }));
            Assert.Equal(current, signed);
            Assert.True(JsonNode.DeepEquals(ReadGuardianAttributionJson(current!), ReadGuardianAttributionJson(signed!)), path);
        }
        var signedReceiptBytes = (await context.FileSystem.ReadFileBytesAsync(
            manifest["files"]![SpiritualWoundOpportunityReceiptState.StatePath]!.GetValue<string>()))!;
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_signed47_receipt", setup.Name,
            signedReceiptBytesBase64 = Convert.ToBase64String(signedReceiptBytes),
            signedReceiptFingerprint = new CanonicalBeforeImage(true, signedReceiptBytes).Fingerprint,
            originReceiptJson = setup.Origin.ReceiptJson }));
        var signedReceipt = ReadGuardianAttributionJson(signedReceiptBytes);
        AssertGuardianAttributionReceiptAppend(setup, signedReceipt, signedReceipt["sources"]![1]!["witness"]!.AsObject());
        AssertGuardianAttributionWitness(setup, signedReceipt["sources"]![1]!["witness"]!.AsObject());
        RequireGuardianJourneyIssues(setup.Name, "signed47_frame", await validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
        WritePressureOwnerTiming("guardian_journey_signed47_frame", setup.Name, started);
    }
}
