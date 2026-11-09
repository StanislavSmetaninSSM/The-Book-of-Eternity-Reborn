using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Two independent III/IV normal owners retain exactly one comparison, publish through common C4 and validate durable signed47.</summary>
    /// <param name="rank">Complete singleton guardian parent III or IV, fixed before signing.</param>
    [Theory]
    [InlineData(3)] [InlineData(4)]
    public Task SpiritualGuardianZero_ColdSourceOnlyPublishesAndValidatesSignedNext(int rank) =>
        WithGuardianZeroPressureSetupAsync("guardian_zero_normal_" + rank, SpiritualGuardianZeroPressureTestData.Create(rank), async setup =>
        {
            var beforeProfiles = (await setup.Context.FileSystem.ReadFileBytesAsync(AfterlifeEntityProfileState.StatePath))!;
            var beforeIdentity = (await setup.Context.FileSystem.ReadFileBytesAsync(EffectIdentityState.StatePath))!;
            var beforeWoundIdentity = (await setup.Context.FileSystem.ReadFileBytesAsync(WoundIdentityState.StatePath))!;
            var completed = await CompleteGuardianZeroSourceOnlyC3Async(setup, (authority, lease) =>
            {
                Assert.True(AcceptedMechanicsPlanAuthority.RegisterSpiritualPublication(setup.Context.FileSystem, lease, authority));
                return Task.CompletedTask;
            });
            await PublishGuardianZeroAndReadNextAsync(setup, completed, beforeProfiles, beforeIdentity, beforeWoundIdentity);
        });

    /// <summary>Uses only the registered immutable completion after warm/cold execution captures and leases have closed.</summary>
    /// <param name="setup">Independent signed guardian originals and unchanged authored ordinary bytes.</param>
    /// <param name="completed">Detached exact cold C3 result, never an execution owner.</param>
    /// <param name="beforeProfiles">Complete original profile bytes retained before any publication.</param>
    /// <param name="beforeIdentity">Complete original effect identity bytes.</param>
    /// <param name="beforeWoundIdentity">Original wound identity bytes which must remain unchanged.</param>
    private async Task PublishGuardianZeroAndReadNextAsync(GuardianZeroPressureSetup setup, PressureGroupCompletedData completed,
        byte[] beforeProfiles, byte[] beforeIdentity, byte[] beforeWoundIdentity)
    {
        var context = setup.Context; var validator = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var started = Stopwatch.GetTimestamp();
        RequireGuardianJourneyIssues(setup.Name, "guardian_zero_normal_raw_resources", await validator.ValidateAcceptedTurnRawResourceMaterializationAsync());
        var plan = await PeekPlanAsync(context);
        var actualRoot = JsonNode.Parse(completed.ActualRootJson)!.AsObject();
        AssertPressureExistingResourcePlan(plan, actualRoot);
        Assert.True(JsonNode.DeepEquals(actualRoot, plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), plan.OwnerCompanionAfterImages[SpiritualWoundOpportunityReceiptState.StatePath]));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.Resources.StateAfterImage!.ToCanonicalJson()), plan.StateAfterImage));
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.Resources.HistoryAfterImage!.ToCanonicalJson()), plan.HistoryAfterImage));
        Assert.Equal(completed.Resources.Events.OrderBy(row => row.Turn).ThenBy(row => row.ExecutionSequence)
            .ThenBy(row => row.EventKind, StringComparer.Ordinal).ThenBy(row => row.OperationId, StringComparer.Ordinal), plan.ResourceEvents);
        Assert.Equal(completed.EffectPayloadFingerprint,
            WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan)));
        Assert.False(plan.EffectPlan!.HasPressureConflictCarrierCollision());
        Assert.Equal(2, completed.Resources.AppliedTransitions.Count); Assert.Empty(completed.Resources.ReplayTransitions);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(context.FileSystem, context.Normalizer,
            validator, await ReadSpiritualC4BackupsAsync(context));
        RequireGuardianJourneyIssues(setup.Name, "guardian_zero_normal_common_c4", published.Issues); Assert.Same(plan, published.MechanicsPlan);
        foreach (var image in plan.WoundCarrierAfterImages.Concat(plan.EffectCarrierAfterImages).Concat(plan.OwnerCompanionAfterImages))
            Assert.True(JsonNode.DeepEquals(image.Value, await context.ReadJsonAsync(image.Key)), image.Key);
        Assert.True(JsonNode.DeepEquals(plan.StateAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.StatePath)));
        Assert.True(JsonNode.DeepEquals(plan.HistoryAfterImage, await context.ReadJsonAsync(ResourceMaterializationContract.HistoryPath)));
        Assert.True(JsonNode.DeepEquals(plan.EffectIdentityAfterImage, await context.ReadJsonAsync(EffectIdentityState.StatePath)));
        Assert.Equal(beforeWoundIdentity, await context.FileSystem.ReadFileBytesAsync(WoundIdentityState.StatePath));
        Assert.Equal(setup.OriginalHistoryBytes, await context.FileSystem.ReadFileBytesAsync(WoundHistoryState.HistoryPath));
        var profiles = (await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath))!.AsObject();
        var identity = (await context.ReadJsonAsync(EffectIdentityState.StatePath))!.AsObject();
        AssertGuardianZeroPublishedGuardian(setup, completed, plan, ReadGuardianAttributionJson(beforeProfiles), profiles,
            ReadGuardianAttributionJson(beforeIdentity), identity);
        var receipt = (await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath))!.AsObject();
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.ReceiptJson), receipt));
        AssertGuardianZeroSourceOnlyReceipt(setup, receipt, actualRoot);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(completed.WitnessJson), receipt["strainComparisons"]![0]!["witness"]));
        var mechanics = await EffectMechanicsSnapshot.LoadAsync(context.FileSystem);
        RequireGuardianJourneyIssues(setup.Name, "guardian_zero_normal_published_mechanics", mechanics.Issues); Assert.True(mechanics.IsAccepted);
        var rootId = setup.Data.Retained.Effects.Single()["effectId"]!.GetValue<string>();
        Assert.Equal(setup.Data.Rank, mechanics.Components.Count(row => row.EffectId == rootId));
        await WithGuardianZeroControlLeaseAsync(context, setup.Name, "normal_published_private_absence", lease =>
        {
            foreach (var path in GuardianZeroPublicationPrivatePaths)
            { Assert.Contains(path, plan.ConsumedPaths); Assert.False(context.FileSystem.FileExists(lease, path)); }
            AssertPressureNoPrivatePair(context, lease);
            Assert.False(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out _));
            return Task.CompletedTask;
        });
        WritePressureOwnerTiming("guardian_zero_normal_common_c4", setup.Name, started);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_normal_published", setup.Name, setup.Data.Rank,
            actualRoot, receipt, effectIdentity = identity, profiles,
            resourceState = plan.StateAfterImage, resourceHistory = plan.HistoryAfterImage,
            resourceSpentEvents = plan.ResourceEvents.Count(e => e.EventKind == "resource_spent"),
            resourceDepletedEvents = plan.ResourceEvents.Count(e => e.EventKind == "resource_depleted"),
            replayTransitions = completed.Resources.ReplayTransitions.Count }));
        var durablePaths = plan.WoundCarrierAfterImages.Keys.Concat(plan.EffectCarrierAfterImages.Keys).Concat(plan.OwnerCompanionAfterImages.Keys)
            .Concat(new[] { ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                EffectIdentityState.StatePath, SpiritualWoundOpportunityReceiptState.StatePath, AfterlifeSpiritualConflictState.StatePath })
            .Distinct(StringComparer.Ordinal).ToArray();
        var durable = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in durablePaths) durable[path] = await context.FileSystem.ReadFileBytesAsync(path);
        await ReadGuardianZeroSignedNextAsync(setup, validator, receipt, actualRoot);
        foreach (var image in durable) Assert.Equal(image.Value, await context.FileSystem.ReadFileBytesAsync(image.Key));
        foreach (var path in GuardianZeroPublicationPrivatePaths) Assert.Null(await context.FileSystem.ReadFileBytesAsync(path));
    }

    /// <summary>Joins one exact guardian root/actor/realm to its full unchanged sibling graph and one complete owner-turn-end identity append.</summary>
    /// <param name="setup">Complete original guardian ancestry and rank.</param>
    /// <param name="completed">Exact completed resource execution DATA.</param>
    /// <param name="plan">The already published common plan.</param>
    /// <param name="beforeProfiles">Independent complete original profiles.</param>
    /// <param name="profiles">Complete current published profiles.</param>
    /// <param name="beforeIdentity">Original effect identity root.</param>
    /// <param name="identity">Published effect identity root.</param>
    private static void AssertGuardianZeroPublishedGuardian(GuardianZeroPressureSetup setup, PressureGroupCompletedData completed,
        AcceptedMechanicsPlan plan, JsonObject beforeProfiles, JsonObject profiles, JsonObject beforeIdentity, JsonObject identity)
    {
        var guardian = ReadGuardianAttributionProfile(profiles); var originalEffect = setup.Data.Retained.Effects.Single();
        Assert.True(JsonNode.DeepEquals(setup.Data.Retained.Parents.Single(), Assert.Single(guardian["activeWounds"]!.AsArray())));
        var effectId = originalEffect["effectId"]!.GetValue<string>();
        var effect = Assert.Single(guardian["activeEffects"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var originalEntry = Assert.Single(beforeIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var currentEntry = Assert.Single(identity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!;
        var parsed = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(identity), EffectIdentityState.StatePath);
        Assert.Empty(parsed.Issues);
        var transitions = currentEntry["transitions"]!.AsArray(); var oldTransitions = originalEntry["transitions"]!.AsArray();
        Assert.Equal(oldTransitions.Count + 1, transitions.Count);
        for (var index = 0; index < oldTransitions.Count; index++) Assert.True(JsonNode.DeepEquals(oldTransitions[index], transitions[index]));
        var appended = transitions[^1]!;
        Assert.Equal(("trigger", 46), (appended["kind"]!.GetValue<string>(), appended["turn"]!.GetValue<int>()));
        var execution = Assert.Single(completed.Resources.ResourceTriggerExecutions, row => row.EffectId == effectId);
        Assert.Equal(("on_owner_turn_end", "owner_turn_end"), (execution.TriggerId, execution.EventKind));
        Assert.Empty(execution.MutationKeys); Assert.NotNull(execution.ComponentIds); Assert.Empty(execution.ComponentIds); Assert.Null(execution.RemainingUseBudget);
        var target = new JsonObject { ["kind"] = "guardian", ["targetId"] = SpiritualGuardianAttributionTestData.GuardianId };
        Assert.Equal("chaos_sea", originalEntry["realm"]!.GetValue<string>()); Assert.True(JsonNode.DeepEquals(target, originalEntry["target"]));
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan);
        var typedEntry = Assert.Single(parsed.State!.Entries, row => row.EffectId == effectId);
        var typedAppend = typedEntry.Transitions[^1];
        Assert.Equal(new[] { effectId }, typedAppend.SourceEffectIds); Assert.Equal(new[] { effectId }, typedAppend.ResultEffectIds);
        Assert.Null(typedAppend.ReceiptId); Assert.Contains(typedAppend.TransitionId, effects.AllocatedTransitionIds);
        Assert.DoesNotContain(typedAppend.TransitionId, oldTransitions.Select(row => row!["transitionId"]!.GetValue<string>()));
        var ownerEvent = Assert.Single(effects.EventInput["lifecycleEvents"]!.AsArray().OfType<JsonObject>(), row =>
            row["phase"]!.GetValue<string>() == "owner_turn_end" && row["realm"]!.GetValue<string>() == "chaos_sea" && JsonNode.DeepEquals(row["target"], target));
        Assert.Equal(46, ownerEvent["turn"]!.GetValue<int>());
        var ownerEventRef = "turn_46:lifecycle:owner_turn_end:chaos_sea:guardian:" + SpiritualGuardianAttributionTestData.GuardianId;
        Assert.Equal(ownerEventRef, ownerEvent["eventRef"]!.GetValue<string>()); Assert.Equal(ownerEventRef, execution.TriggerEventRef);
        Assert.Equal(execution.EventRef, appended["eventRef"]!.GetValue<string>());
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(completed.Resources.EffectBoundaryTranscript);
        var activation = Assert.Single(transcript.AcceptedActivations, row => row.Activation.Stamp.Identity.EffectId == effectId);
        Assert.Equal(new EffectActivationCandidateIdentity(effectId, execution.TriggerId, execution.EventKind, execution.EventRef, execution.TriggerEventRef!), activation.Activation.Stamp.Identity);
        Assert.Equal(new ResourcePendingAuthorityBinding("permanent", effectId), activation.Activation.Stamp.EffectAuthority);
        Assert.Equal(EffectAcceptedTurnPlanner.CreateLifecycleResourceTriggerEventRef(ownerEventRef, effectId, activation.Activation.Stamp.EffectAuthority, execution.TriggerId), execution.EventRef);
        Assert.False(activation.Activation.Stamp.ConsumesUse); Assert.Null(activation.Activation.Stamp.UsesBefore); Assert.Null(activation.Activation.UsesAfter);
        var chronology = originalEffect["chronology"]!.DeepClone(); chronology["lastTransitionTurn"] = 46; chronology["lastTransitionId"] = appended["transitionId"]!.DeepClone();
        Assert.True(JsonNode.DeepEquals(chronology, effect["chronology"]));
        var restoredIdentity = identity.DeepClone().AsObject();
        Assert.Single(restoredIdentity["entries"]!.AsArray(), row => row!["effectId"]!.GetValue<string>() == effectId)!["transitions"] = oldTransitions.DeepClone();
        Assert.True(JsonNode.DeepEquals(beforeIdentity, restoredIdentity));
        var restoredProfiles = profiles.DeepClone().AsObject();
        Assert.Single(ReadGuardianAttributionProfile(restoredProfiles)["activeEffects"]!.AsArray())!["chronology"] = originalEffect["chronology"]!.DeepClone();
        var expectedProfiles = beforeProfiles.DeepClone().AsObject();
        if (!expectedProfiles.ContainsKey("schemaVersion")) expectedProfiles["schemaVersion"] = 1;
        Assert.Equal(1, expectedProfiles["schemaVersion"]!.GetValue<int>());
        foreach (var profile in expectedProfiles[AfterlifeEntityProfileState.ProfilesProperty]!.AsArray().OfType<JsonObject>())
            foreach (var field in new[] { "activeWounds", "activeEffects" })
                if (!profile.ContainsKey(field)) profile[field] = new JsonArray();
        Assert.True(JsonNode.DeepEquals(expectedProfiles, restoredProfiles));
    }

    /// <summary>Signs published turn47, reads each exact signed input and validates the real frame without executing another action.</summary>
    /// <param name="setup">Same independently owned normal fixture.</param>
    /// <param name="validator">Fresh validator used for actual common publication.</param>
    /// <param name="receipt">Complete published comparison-only receipt.</param>
    /// <param name="actualRoot">Complete published actual conflict.</param>
    private async Task ReadGuardianZeroSignedNextAsync(GuardianZeroPressureSetup setup, ValidationService validator, JsonObject receipt, JsonObject actualRoot)
    {
        var context = setup.Context; var started = Stopwatch.GetTimestamp();
        await context.CaptureValidatedPendingSnapshotAsync(turn: 47, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5],
            requestId: "request_" + setup.Name + "_published_read_47");
        var manifestBytes = (await context.FileSystem.ReadFileBytesAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!;
        var manifest = ReadGuardianAttributionJson(manifestBytes); Assert.Equal(47, manifest["turnNumber"]!.GetValue<int>());
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_signed47_manifest", setup.Name, manifest,
            manifestBytesBase64 = Convert.ToBase64String(manifestBytes) }));
        foreach (var path in PressureOrdinarySignedPaths)
        {
            var current = await context.FileSystem.ReadFileBytesAsync(path);
            var signed = await context.FileSystem.ReadFileBytesAsync(manifest["files"]![path]!.GetValue<string>());
            Assert.Equal(current, signed);
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_signed47_input", setup.Name, path,
                currentBytesBase64 = current is null ? null : Convert.ToBase64String(current), signedBytesBase64 = signed is null ? null : Convert.ToBase64String(signed) }));
        }
        var signedReceipt = ReadGuardianAttributionJson((await context.FileSystem.ReadFileBytesAsync(
            manifest["files"]![SpiritualWoundOpportunityReceiptState.StatePath]!.GetValue<string>()))!);
        Assert.True(JsonNode.DeepEquals(receipt, signedReceipt)); AssertGuardianZeroSourceOnlyReceipt(setup, signedReceipt, actualRoot);
        RequireGuardianJourneyIssues(setup.Name, "guardian_zero_signed47_frame", await validator.ValidateGameStateAsync(
            new GameStateValidationSelection(GameStateValidationPhase.AfterlifeSpiritualConflictState)));
        WritePressureOwnerTiming("guardian_zero_normal_signed47", setup.Name, started);
    }

    /// <summary>Retains complete immutable prepared operands for later independent analysis, without serializing live capabilities or implementing a retry map.</summary>
    /// <param name="setup">Independent signed/authored DATA.</param>
    /// <param name="plan">Complete prepared common plan, read only through detached DATA properties.</param>
    /// <param name="reduction">Completed resource/effect DATA.</param>
    /// <param name="interval">The already checked actual interval, never a fabricated selector.</param>
    /// <param name="journal">Exact existing allocation journal JSON.</param>
    /// <param name="cursor">Existing allocation cursor.</param>
    /// <param name="source">Detached admitted source facts.</param>
    /// <param name="witness">Complete comparison witness.</param>
    /// <param name="receipt">Complete source-only receipt.</param>
    /// <returns>The already serialized immutable complete envelope; no owner or authority is retained.</returns>
    private string WriteGuardianZeroPreparedEnvelope(GuardianZeroPressureSetup setup, AcceptedMechanicsPlan plan,
        AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction reduction, AcceptedMechanicsPlanner.SpiritualExchangeInterval interval,
        string journal, int cursor, ValidationService.PreparedSpiritualSource source, JsonObject witness, JsonObject receipt)
    {
        var started = Stopwatch.GetTimestamp();
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(reduction.Effects);
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(reduction.Resources.EffectBoundaryTranscript);
        var definitionAfter = plan.DefinitionAfterImage;
        var stateAfter = plan.StateAfterImage;
        var historyAfter = plan.HistoryAfterImage;
        var effectCarriers = plan.EffectCarrierAfterImages;
        var effectIdentity = plan.EffectIdentityAfterImage;
        var pendingAfter = plan.PendingAfterImages;
        var pendingPacket = plan.PendingGmPacket;
        var pendingAuthority = plan.PendingPublicationAuthority;
        var ownerImages = plan.OwnerCompanionAfterImages;
        var ownerTransitions = plan.OwnerTransitions;
        var beforeImages = plan.BeforeImages;
        var beforeData = beforeImages.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row =>
        {
            var bytes = row.Value.Bytes;
            return new { path = row.Key, row.Value.Existed, bytesBase64 = bytes is null ? null : Convert.ToBase64String(bytes), row.Value.Fingerprint };
        }).ToArray();
        var authorityFingerprints = plan.AuthorityFingerprints.Enumerate().OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        var resourceEvents = plan.ResourceEvents;
        var projection = plan.ProjectionInput;
        var projectionDefinitions = projection.Definitions;
        var projectionState = projection.State;
        var projectionHistory = projection.History;
        var projectionOwnerFingerprint = projection.OwnerAuthorityFingerprint;
        var ownerFingerprint = plan.OwnerAuthority.Fingerprint;
        var woundStages = plan.WoundStageBundle;
        var liveProof = plan.LiveWoundProofFingerprint;
        var woundAnchor = plan.WoundAnchorPlanFingerprint;
        var directAuthority = plan.DirectWoundPublicationAuthority;
        var treatmentAuthority = plan.TreatmentResourcePublicationAuthority;
        var jointAuthority = plan.JointRepairCommonAuthority;
        var woundCarriers = plan.WoundCarrierAfterImages;
        var woundIdentity = plan.WoundIdentityAfterImage;
        var woundHistory = plan.WoundHistoryAfterImage;
        Assert.Empty(pendingAfter); Assert.Null(pendingPacket); Assert.False(plan.AwaitsPendingResolution); Assert.Null(pendingAuthority);
        Assert.Empty(ownerTransitions); Assert.Null(woundStages); Assert.Null(liveProof); Assert.Null(woundAnchor);
        Assert.Null(directAuthority); Assert.Null(treatmentAuthority); Assert.Null(jointAuthority);
        Assert.Empty(woundCarriers); Assert.Null(woundIdentity); Assert.Null(woundHistory);
        Assert.True(JsonNode.DeepEquals(definitionAfter, projectionDefinitions));
        Assert.True(JsonNode.DeepEquals(stateAfter, projectionState));
        Assert.True(JsonNode.DeepEquals(historyAfter, projectionHistory));
        var effectData = JsonSerializer.SerializeToNode(new
        {
            effects.InputFingerprint, effects.CarrierAuthorityFingerprint, effects.SourceAuthorityFingerprint, effects.TargetAuthorityFingerprint,
            SkillScopeFingerprint = effects.SkillScopeAuthority?.Fingerprint, effects.AllocatedCombatantIds, effects.AllocatedEffectIds, effects.AllocatedTransitionIds,
            effects.WoundApplicationRootEffectBindings, effects.Sources, effects.Targets, effects.SourceBindings,
            DeferredReactions = effects.DeferredReactions.Select(ReadPressureZeroReactionDiagnosticData).ToArray(), effects.ReactionExpansionCount,
            ReactionExpansionUsage = effects.ReactionExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray(),
            effects.ActiveEffects, effects.ResourceTriggerCarriers, effects.AcceptedCarrierBaselines, effects.EventInput,
            effects.CarrierBeforeImages, effects.CarrierAfterImages, effects.IdentityIndexBeforeImage, effects.IdentityIndexAfterImage,
            effects.TouchedPaths, effects.DeletedPaths, effects.IsAcceptedBoundaryComplete, effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint,
            MortalRecoveryExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalRecoveryExecutionFingerprint(effects),
            SpiritualHealingExecutionFingerprint = EffectAcceptedTurnPlanner.GetSpiritualHealingExecutionFingerprint(effects),
            MortalDiagnosisExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalDiagnosisExecutionFingerprint(effects),
            MortalTreatmentExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalTreatmentExecutionFingerprint(effects),
            RetainedSourceAuthorityFingerprint = effects.SourceAuthority.Fingerprint,
            RetainedTargetAuthorityFingerprint = effects.TargetAuthority.Fingerprint
        })!.AsObject();
        var envelope = JsonSerializer.SerializeToNode(new
        {
            phase = "guardian_zero_complete_prepared_data_envelope", setup.Name, setup.Data.Rank, AllocationJournalJson = journal, AllocationCursor = cursor,
            ordinaryBytesBase64 = Convert.ToBase64String(setup.RawOrdinaryBytes), originalConflictBytesBase64 = Convert.ToBase64String(setup.OriginalConflictBytes),
            originalReceiptBytesBase64 = Convert.ToBase64String(setup.OriginalReceiptBytes), originalHistoryBytesBase64 = Convert.ToBase64String(setup.OriginalHistoryBytes),
            actualRoot = ownerImages[AfterlifeSpiritualConflictState.StatePath], witness, receipt,
            source.Coordinate, source.ConflictId, source.ExchangeId, source.AffectedSide, source.ActingActor, source.AffectedActor, source.Operation,
            source.OriginalSpecialArtJson, source.ExchangeJson, source.Calculation, source.GuaranteedSeverityRank, source.RetraumaWoundId, source.RetraumaWoundJson, source.StrainPreimageJson,
            Interval = new { interval.ConflictId, interval.ExchangeId, interval.Ordinal, interval.AppliedRange, interval.ReplayRange, interval.EventRange,
                interval.AppliedTransitions, interval.ReplayTransitions, interval.Events,
                EffectBefore = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectBefore), EffectAfter = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectAfter) },
            ResourceStateAfterImage = reduction.Resources.StateAfterImage!.ToCanonicalJson(), ResourceHistoryAfterImage = reduction.Resources.HistoryAfterImage!.ToCanonicalJson(),
            reduction.Resources.AppliedTransitions, reduction.Resources.ReplayTransitions, reduction.Resources.Events, reduction.Resources.ResourceTriggerExecutions,
            FullEffectBoundary = ReadGuardianZeroTerminalEffectBoundaryDiagnosticData(transcript), EffectPlan = effectData,
            plan.InputFingerprint, plan.PreparedPlanFingerprint, plan.TouchedPaths, plan.ConsumedPaths,
            BeforeImages = beforeData,
            WoundCarrierAfterImages = woundCarriers, EffectCarrierAfterImages = effectCarriers, OwnerCompanionAfterImages = ownerImages,
            EffectIdentityAfterImage = effectIdentity, StateAfterImage = stateAfter, HistoryAfterImage = historyAfter, ResourceEvents = resourceEvents,
            PreparedHashOperands = new
            {
                DefinitionAfterImage = definitionAfter, AuthorityFingerprints = authorityFingerprints,
                PendingAfterImages = pendingAfter, PendingGmPacket = pendingPacket, PendingPublicationAuthority = (object?)null,
                OwnerTransitions = ownerTransitions.Select(row => row.ToFingerprintNode()).ToArray(),
                ProjectionInput = new { Definitions = projectionDefinitions, State = projectionState, History = projectionHistory,
                    OwnerAuthorityFingerprint = projectionOwnerFingerprint },
                OwnerAuthorityFingerprint = ownerFingerprint, WoundStageBundle = (object?)null,
                LiveWoundProofFingerprint = liveProof, WoundAnchorPlanFingerprint = woundAnchor,
                DirectWoundPublicationAuthority = (object?)null, TreatmentResourcePublicationAuthority = (object?)null,
                JointRepairCommonAuthority = (object?)null, WoundIdentityAfterImage = woundIdentity, WoundHistoryAfterImage = woundHistory
            }
        })!.AsObject();
        // Retain complete operands even if an independent hash comparison fails before registration.
        var envelopeJson = envelope.ToJsonString();
        _storageOutput.WriteLine(envelopeJson);
        var counts = AssertGuardianZeroPreparedHashClosure(envelope, plan, effects);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { phase = "guardian_zero_prepared_hash_closure_verified", setup.Name, setup.Data.Rank,
            effectInputFieldCount = counts.EffectFields, preparedInputFieldCount = counts.PreparedFields }));
        // Timing is separate diagnostic output, never a field in the semantic comparison envelope.
        WritePressureOwnerTiming("guardian_zero_prepared_hash_diagnostics", setup.Name, started);
        return envelopeJson;
    }

    /// <summary>Checks two complete detached hash-input projections against the genuine typed producers and their stored results.</summary>
    /// <param name="envelope">Complete detached operands, without an execution owner or publication handle.</param>
    /// <param name="plan">The existing genuine common plan, used only for the independent production hash comparison.</param>
    /// <param name="effects">The existing completed effect plan, used only for the independent production payload hash comparison.</param>
    /// <returns>Measured input-field counts; no count is guessed or inserted into the semantic envelope.</returns>
    private static (int EffectFields, int PreparedFields) AssertGuardianZeroPreparedHashClosure(JsonObject envelope,
        AcceptedMechanicsPlan plan, EffectAcceptedTurnPlan effects)
    {
        var effectFields = ReadGuardianZeroEffectPayloadHashFields(envelope["EffectPlan"]!.AsObject());
        var effectHash = WoundAcceptedTurnFingerprintWriter.Compute(effectFields);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects), effectHash);
        Assert.Equal(envelope["EffectPlan"]!["AcceptedBoundaryFinalPlanFingerprint"]!.GetValue<string>(), effectHash);
        var preparedFields = ReadGuardianZeroPreparedPlanHashFields(envelope, effectHash);
        var preparedHash = WoundAcceptedTurnFingerprintWriter.Compute(preparedFields);
        Assert.Equal(AcceptedMechanicsPlanFingerprints.ComputePrepared(plan), preparedHash);
        Assert.Equal(envelope["PreparedPlanFingerprint"]!.GetValue<string>(), preparedHash);
        return (effectFields.Count, preparedFields.Count);
    }

    /// <summary>Projects the entire bounded completed effect payload in the production field order, including both retained authority fingerprints.</summary>
    /// <param name="data">Detached full effect plan DATA whose closed absent branches are asserted before their empty framing is appended.</param>
    /// <returns>Every ordered nullable string consumed by the effect payload writer for this exact no-injury lane.</returns>
    private static IReadOnlyList<string?> ReadGuardianZeroEffectPayloadHashFields(JsonObject data)
    {
        var fields = new List<string?>
        {
            "book_of_eternity.effect.accepted_plan_payload", "1",
            data["InputFingerprint"]!.GetValue<string>(), data["CarrierAuthorityFingerprint"]!.GetValue<string>(),
            data["SourceAuthorityFingerprint"]!.GetValue<string>(), data["TargetAuthorityFingerprint"]!.GetValue<string>(),
            data["SkillScopeFingerprint"]?.GetValue<string>() ?? "none"
        };
        foreach (var name in new[] { "MortalRecoveryExecutionFingerprint", "SpiritualHealingExecutionFingerprint",
            "MortalDiagnosisExecutionFingerprint", "MortalTreatmentExecutionFingerprint" }) Assert.Null(data[name]);
        Assert.Empty(data["AllocatedCombatantIds"]!.AsArray()); Assert.Empty(data["AllocatedEffectIds"]!.AsArray());
        Assert.Single(data["AllocatedTransitionIds"]!.AsArray());
        foreach (var name in new[] { "AllocatedCombatantIds", "AllocatedEffectIds", "AllocatedTransitionIds" })
            AppendGuardianZeroHashEffectOrderedStrings(fields, data[name]!.AsArray());
        foreach (var name in new[] { "WoundApplicationRootEffectBindings", "Sources", "Targets", "SourceBindings", "DeferredReactions" })
        {
            var rows = data[name]!.AsArray(); Assert.Empty(rows); fields.Add(GuardianZeroHashNumber(rows.Count));
        }
        var expansionCount = data["ReactionExpansionCount"]!.GetValue<int>(); Assert.Equal(0, expansionCount);
        fields.Add(GuardianZeroHashNumber(expansionCount));
        var expansion = data["ReactionExpansionUsage"]!.AsArray(); Assert.Empty(expansion); fields.Add(GuardianZeroHashNumber(expansion.Count));
        var active = data["ActiveEffects"]!.AsArray(); Assert.Single(active); fields.Add(GuardianZeroHashNumber(active.Count));
        for (var index = 0; index < active.Count; index++)
        { fields.Add(GuardianZeroHashNumber(index)); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(active[index])); }
        AppendGuardianZeroHashEffectCarriers(fields, data["ResourceTriggerCarriers"]);
        fields.Add(data["RetainedSourceAuthorityFingerprint"]!.GetValue<string>());
        fields.Add(data["RetainedTargetAuthorityFingerprint"]!.GetValue<string>());
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["EventInput"]));
        AppendGuardianZeroHashEffectCarrierMap(fields, data["CarrierBeforeImages"]!.AsObject());
        AppendGuardianZeroHashEffectCarrierMap(fields, data["CarrierAfterImages"]!.AsObject());
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["IdentityIndexBeforeImage"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["IdentityIndexAfterImage"]));
        AppendGuardianZeroHashEffectOrderedStrings(fields, data["TouchedPaths"]!.AsArray());
        AppendGuardianZeroHashEffectOrderedStrings(fields, data["DeletedPaths"]!.AsArray());
        AppendGuardianZeroHashEffectCarriers(fields, data["AcceptedCarrierBaselines"]);
        return fields;
    }

    /// <summary>Projects every common prepared-plan operand in production order after proving all absent stage/capability branches.</summary>
    /// <param name="envelope">Full existing images plus complete detached missing operands; none are substituted or omitted.</param>
    /// <param name="effectHash">Independently verified full completed effect payload, not an arbitrary digest binding.</param>
    /// <returns>The complete ordered nullable strings for this bounded common plan, including exact null and empty-map framing.</returns>
    private static IReadOnlyList<string?> ReadGuardianZeroPreparedPlanHashFields(JsonObject envelope, string effectHash)
    {
        var data = envelope["PreparedHashOperands"]!.AsObject();
        var fields = new List<string?>
        {
            "book_of_eternity.accepted_mechanics.prepared_plan", "1", envelope["InputFingerprint"]!.GetValue<string>(),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(data["DefinitionAfterImage"]),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(envelope["StateAfterImage"]),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(envelope["HistoryAfterImage"])
        };
        var authorities = data["AuthorityFingerprints"]!.AsArray().Select(row => row!.AsObject())
            .OrderBy(row => row["Key"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
        Assert.Equal(new[] { "acceptedEvents", "commands", "definitions", "effectCarriers", "effectIdentityIndex", "effectSources", "effectTargets",
            "internalInputs", "owners", "pending", "resourceHistory", "resourceState", "woundCarriers", "woundHistory", "woundIdentityIndex" },
            authorities.Select(row => row["Key"]!.GetValue<string>()));
        fields.Add(GuardianZeroHashNumber(authorities.Length));
        foreach (var row in authorities) { fields.Add(row["Key"]!.GetValue<string>()); fields.Add(row["Value"]!.GetValue<string>()); }
        AppendGuardianZeroHashPreparedObjectMap(fields, envelope["EffectCarrierAfterImages"]!.AsObject());
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(envelope["EffectIdentityAfterImage"]));
        var pending = data["PendingAfterImages"]!.AsObject(); Assert.Empty(pending); fields.Add(GuardianZeroHashNumber(pending.Count));
        Assert.Null(data["PendingGmPacket"]); fields.Add(null);
        Assert.Null(data["PendingPublicationAuthority"]); fields.Add(null);
        AppendGuardianZeroHashPreparedObjectMap(fields, envelope["OwnerCompanionAfterImages"]!.AsObject());
        var owners = data["OwnerTransitions"]!.AsArray(); Assert.Empty(owners); fields.Add(GuardianZeroHashNumber(owners.Count));
        var events = envelope["ResourceEvents"]!.AsArray(); fields.Add(GuardianZeroHashNumber(events.Count));
        foreach (var node in events)
        {
            var row = node!.AsObject(); var coordinate = row["Coordinate"]!.AsObject();
            fields.Add(row["EventKind"]!.GetValue<string>()); fields.Add(row["OperationId"]!.GetValue<string>());
            fields.Add(row["EventRef"]!.GetValue<string>()); fields.Add(coordinate["Realm"]!.GetValue<string>());
            fields.Add(((ResourceOwnerKind)coordinate["OwnerKind"]!.GetValue<int>()).ToString());
            fields.Add(coordinate["ResourceOwnerId"]!.GetValue<string>()); fields.Add(coordinate["ResourceKey"]!.GetValue<string>());
            fields.Add(row["Before"]!.GetValue<decimal>().ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(row["After"]!.GetValue<decimal>().ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(row["AppliedAmount"]!.GetValue<decimal>().ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(GuardianZeroHashNumber(row["Turn"]!.GetValue<int>())); fields.Add(GuardianZeroHashNumber(row["ExecutionSequence"]!.GetValue<int>()));
            fields.Add(row["SourceFingerprint"]!.GetValue<string>());
        }
        var projection = data["ProjectionInput"]!.AsObject();
        Assert.True(JsonNode.DeepEquals(data["DefinitionAfterImage"], projection["Definitions"]));
        Assert.True(JsonNode.DeepEquals(envelope["StateAfterImage"], projection["State"]));
        Assert.True(JsonNode.DeepEquals(envelope["HistoryAfterImage"], projection["History"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(projection["Definitions"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(projection["State"]));
        fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(projection["History"]));
        fields.Add(projection["OwnerAuthorityFingerprint"]!.GetValue<string>()); fields.Add(data["OwnerAuthorityFingerprint"]!.GetValue<string>());
        fields.Add("present"); fields.Add(effectHash);
        Assert.Null(data["WoundStageBundle"]); fields.Add(null);
        foreach (var name in new[] { "LiveWoundProofFingerprint", "WoundAnchorPlanFingerprint", "DirectWoundPublicationAuthority", "TreatmentResourcePublicationAuthority" })
        { Assert.Null(data[name]); fields.Add(null); }
        // ComputePrepared adds no field at all for the absent joint-repair branch.
        Assert.Null(data["JointRepairCommonAuthority"]);
        Assert.Empty(envelope["WoundCarrierAfterImages"]!.AsObject());
        AppendGuardianZeroHashPreparedObjectMap(fields, envelope["WoundCarrierAfterImages"]!.AsObject());
        Assert.Null(data["WoundIdentityAfterImage"]); fields.Add(null);
        Assert.Null(data["WoundHistoryAfterImage"]); fields.Add(null);
        var before = envelope["BeforeImages"]!.AsArray().Select(row => row!.AsObject()).OrderBy(row => row["path"]!.GetValue<string>(), StringComparer.Ordinal).ToArray();
        fields.Add(GuardianZeroHashNumber(before.Length));
        foreach (var row in before)
        {
            fields.Add(row["path"]!.GetValue<string>()); fields.Add(row["Existed"]!.GetValue<bool>() ? "present" : "missing");
            fields.Add(row["bytesBase64"]?.GetValue<string>());
        }
        AppendGuardianZeroHashOrderedStrings(fields, envelope["TouchedPaths"]!.AsArray());
        AppendGuardianZeroHashOrderedStrings(fields, envelope["ConsumedPaths"]!.AsArray());
        return fields;
    }

    /// <summary>Appends every retained effect carrier in its declared production order, preserving explicit null carriers.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="carriers">Full detached carrier catalog, or an explicitly absent catalog.</param>
    private static void AppendGuardianZeroHashEffectCarriers(ICollection<string?> fields, JsonNode? carriers)
    {
        if (carriers is null) { fields.Add(null); return; }
        fields.Add("present");
        foreach (var name in new[] { "PlayerEffects", "NpcEffects", "EnemyCombatants", "AllyCombatants", "AfterlifeProfiles", "SpiritualConflict" })
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(carriers[name]));
    }

    /// <summary>Appends a complete effect before/after carrier map, with the count and sorted per-entry index required by its payload hash.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="images">Every detached carrier image, including explicitly absent before-images.</param>
    private static void AppendGuardianZeroHashEffectCarrierMap(ICollection<string?> fields, JsonObject images)
    {
        var rows = images.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(); fields.Add(GuardianZeroHashNumber(rows.Length));
        for (var index = 0; index < rows.Length; index++)
        { fields.Add(GuardianZeroHashNumber(index)); fields.Add(rows[index].Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(rows[index].Value)); }
    }

    /// <summary>Appends a complete common-plan object map in its production count/key/value order, without effect-map indexes.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="images">All detached images, never a subset selected to make fingerprints agree.</param>
    private static void AppendGuardianZeroHashPreparedObjectMap(ICollection<string?> fields, JsonObject images)
    {
        var rows = images.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(); fields.Add(GuardianZeroHashNumber(rows.Length));
        foreach (var row in rows) { fields.Add(row.Key); fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(row.Value)); }
    }

    /// <summary>Appends the effect payload's actual count followed by each index and ordered value, including empty arrays.</summary>
    /// <param name="fields">Independent effect payload hash fields being constructed.</param>
    /// <param name="values">Exact detached effect list whose indices are part of its producer's hash framing.</param>
    private static void AppendGuardianZeroHashEffectOrderedStrings(ICollection<string?> fields, JsonArray values)
    {
        fields.Add(GuardianZeroHashNumber(values.Count));
        for (var index = 0; index < values.Count; index++)
        { fields.Add(GuardianZeroHashNumber(index)); fields.Add(values[index]!.GetValue<string>()); }
    }

    /// <summary>Appends the actual count and every ordered string, preserving array order and all empty arrays.</summary>
    /// <param name="fields">Independent ordered hash fields being constructed.</param>
    /// <param name="values">Exact detached ordered values.</param>
    private static void AppendGuardianZeroHashOrderedStrings(ICollection<string?> fields, JsonArray values)
    {
        fields.Add(GuardianZeroHashNumber(values.Count)); foreach (var value in values) fields.Add(value!.GetValue<string>());
    }

    /// <summary>Formats one actual count or index using the production invariant decimal representation.</summary>
    /// <param name="value">Actual retained count, ordinal or index.</param>
    /// <returns>Invariant decimal string, without a guessed diagnostic count.</returns>
    private static string GuardianZeroHashNumber(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>Projects complete retained terminal effect DATA without constructing a prefix or changing any seal.</summary>
    /// <param name="transcript">The completed reduction's immutable effect boundary, including its terminal or pending seal values.</param>
    /// <returns>Every readable evidence vector, stored fingerprint and completion/seal field, without new authority or computation.</returns>
    private static object ReadGuardianZeroTerminalEffectBoundaryDiagnosticData(AcceptedEffectBoundaryTranscript transcript) => new
    {
        transcript.Fingerprint, transcript.PlanAuthority, transcript.Boundaries, transcript.BoundaryCloses, transcript.CausalClosures,
        AcceptedActivations = transcript.AcceptedActivations.Select(row => new
        {
            row.Boundary, row.Activation, Candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(row.Candidate),
            row.CandidateFingerprint, row.MechanicsOrdinal
        }).ToArray(),
        RejectedActivations = transcript.RejectedActivations.Select(row => new
        {
            row.Boundary, Candidate = AcceptedEffectBoundaryTranscript.ReadCandidateDiagnosticData(row.Candidate),
            row.CandidateFingerprint, row.Reason, row.BlockedAvailabilityEffectId
        }).ToArray(),
        transcript.AppliedComponentEvidence, transcript.ResourceMutations,
        ReleasedReactions = transcript.ReleasedReactions.Select(row => new
        {
            row.Boundary, row.Activation, Reaction = ReadPressureZeroReactionDiagnosticData(row.Reaction),
            row.Stage, row.MechanicsOrdinal
        }).ToArray(),
        TerminalAvailabilityReservations = transcript.TerminalAvailabilityReservations.Select(row => new
        {
            row.Boundary, row.Activation, Subject = new { row.Subject.EffectId, row.Subject.Authority },
            row.Kind, row.ReactionFingerprint, row.ReplayStableOrderKey, row.MechanicsOrdinal
        }).ToArray(),
        ExpansionUsage = transcript.ExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray(),
        transcript.ExpansionCount, transcript.UseProjectionOrdinal, transcript.PendingFrontierBoundaryOrdinal, transcript.IsComplete
    };

    /// <summary>The exact three private surfaces consumed by source-only C4; no decision packet is invented.</summary>
    private static IReadOnlyList<string> GuardianZeroPublicationPrivatePaths =>
        [SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath];
}
