using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>Invocation-local immutable serialized DATA only; never retains an execution owner, lease or permission.</summary>
    private sealed class GuardianCappedAttemptCapture
    {
        /// <summary>Gets or sets the single warm first-C2 snapshot before its capture and lease close.</summary>
        internal string? FirstJson { get; set; }
        /// <summary>Gets or sets the single cold none/C3 snapshot before its private session closes.</summary>
        internal string? CompletedJson { get; set; }
        /// <summary>Gets or sets the single prepared snapshot before common C4 takes its exact current authority.</summary>
        internal string? PreparedJson { get; set; }
    }

    /// <summary>Detaches all signed/authored inputs, positive packet, source, witness, interval and initial allocations exactly once.</summary>
    private async Task CaptureGuardianCappedFirstAsync(GuardianAttributionJourney setup, GuardianCappedAttemptCapture attempt,
        ValidationService.SpiritualOriginalTurnCapture capture, FileSystemManager.CanonicalWriteLease lease,
        AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, ValidationService.PreparedSpiritualSource source,
        JsonObject witness, string firstPendingJson, ValidationService.SpiritualC2InitialTransportResult committed)
    {
        Assert.Null(attempt.FirstJson);
        var physical = await ReadGuardianCappedTransportAsync(setup.Copy.Context, lease);
        var journal = capture.ReadAllocationJournal(lease).ToJsonString(); var cursor = capture.ReadAllocationCursor(lease);
        var checkpoint = Assert.IsType<SpiritualWoundCaptureCheckpointState>(committed.Checkpoint);
        var json = JsonSerializer.Serialize(new
        {
            phase = "guardian_capped_first_c2_data", setup.Name, Rank = setup.ExpectedRank, setup.Role, setup.Version,
            signedOriginalFiles = ReadGuardianCappedRawImages(setup.Origin.Files.Select(row => new KeyValuePair<string, byte[]?>(row.Key, row.Value))),
            signedOriginalTracked = ReadGuardianCappedRawImages(setup.Origin.Tracked),
            signedOriginalGenerationBase64 = Convert.ToBase64String(setup.Origin.Generation), setup.Origin.ReceiptJson, setup.Origin.IdentityJson,
            authoredInputs = ReadGuardianCappedRawImages(setup.Authored), setup.Retained,
            ordinaryRoot = setup.Copy.Conflict, actualRoot = setup.ActualRoot, setup.Ordinary, setup.Actual,
            setup.OrdinaryCalculation, setup.ActualCalculation,
            Source = JsonNode.Parse(SerializeSpiritualColdSourceData(source)), Witness = witness, FirstPendingJson = firstPendingJson,
            CommittedPendingJson = SpiritualWoundDecisionPendingState.SerializeCanonical(committed.Pending!),
            CommittedCheckpointJson = SpiritualWoundCaptureCheckpointState.SerializeCanonical(checkpoint),
            Transport = physical, AllocationJournalJson = journal, AllocationCursor = cursor,
            Interval = new { interval.ConflictId, interval.ExchangeId, interval.Ordinal, interval.AppliedRange, interval.ReplayRange,
                interval.EventRange, interval.AppliedTransitions, interval.ReplayTransitions, interval.Events,
                EffectBefore = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectBefore),
                EffectAfter = ReadPressureZeroEffectPrefixDiagnosticData(interval.EffectAfter) },
            counts = ReadGuardianCappedCounts(capture), captureCurrent = capture.IsCurrentOwner, leaseActive = lease.IsActive
        });
        attempt.FirstJson = json; _storageOutput.WriteLine(json);
    }

    /// <summary>Detaches actual completed positive transport and every C3/resource/effect result before its private owner closes.</summary>
    private async Task CaptureGuardianCappedCompletedAsync(GuardianAttributionJourney setup, GuardianCappedAttemptCapture attempt,
        FileSystemManager.CanonicalWriteLease lease, ValidationService.SpiritualC2OfferReplayEvidence observed,
        ValidationService.SpiritualC3DecisionReductionResult reduced, AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction reduction,
        object coldScope)
    {
        Assert.NotNull(attempt.FirstJson); Assert.Null(attempt.CompletedJson);
        var resources = reduction.Resources; var effects = Assert.IsType<EffectAcceptedTurnPlan>(reduction.Effects);
        var transcript = Assert.IsType<AcceptedEffectBoundaryTranscript>(resources.EffectBoundaryTranscript);
        var physical = await ReadGuardianCappedTransportAsync(setup.Copy.Context, lease);
        var checkpoint = ReadGuardianCappedTransportRoot(physical, SpiritualWoundCaptureCheckpointState.StatePath)["checkpoint"]!.AsObject();
        var pending = ReadGuardianCappedTransportRoot(physical, SpiritualWoundDecisionPendingState.StatePath)["pending"]!.AsObject();
        var decision = Assert.Single(pending["stagedDecisions"]!.AsArray()); Assert.Equal("none", decision!["decision"]!.GetValue<string>());
        var effectOperands = ReadGuardianCappedEffectHashOperands(effects);
        var json = JsonSerializer.Serialize(new
        {
            phase = "guardian_capped_completed_c3_data", setup.Name, Rank = setup.ExpectedRank, ColdScope = coldScope,
            ColdOfferReplay = new { observed.ReconstructedPendingJson, observed.ActualSourceDataJson, observed.AllocationCursor,
                observed.AllocationJournalJson, observed.OriginalConflictBytesBase64, observed.OriginalConflictFingerprint },
            Transport = physical, CompletedPending = pending, CompletedCheckpoint = checkpoint, NoneDecision = decision,
            AllocationJournal = checkpoint["allocations"], AllocationCursor = checkpoint["allocations"]!.AsArray().Count,
            Reduction = new
            {
                Input = ReadGuardianCappedBinding(reduction.Input.CreateBinding()), reduction.InputFingerprint,
                DefinitionAfterImageJson = reduction.Definitions.ToCanonicalJson(), reduction.PendingAfterImage, reduction.OwnerCompanionAfterImages,
                OwnerTransitions = reduction.OwnerTransitions.Select(row => row.ToFingerprintNode()).ToArray(),
                reduction.HasMortalProducerCompletion, reduction.RequiresSatisfactionReceipt, reduction.HasLiveDecisionReceipt,
                reduction.HasLiveWoundWork, LiveWoundCompletion = (object?)null, reduced.ReceiptAfterImage,
                ReceiptProofPresent = reduced.ReceiptProof is not null, reduced.Success, Issues = ReadGuardianCappedIssues(reduced.Issues),
                Resources = new
                {
                    StateAfterImageJson = resources.StateAfterImage?.ToCanonicalJson(), HistoryAfterImageJson = resources.HistoryAfterImage?.ToCanonicalJson(),
                    resources.Events, resources.AppliedTransitions, resources.ReplayTransitions,
                    ResourceTriggerExecutions = resources.ResourceTriggerExecutions.Select(row => new { row.EffectId, row.TriggerId, row.EventKind,
                        row.EventRef, row.MutationKeys, row.RemainingUseBudget, row.ComponentIds, row.TriggerEventRef,
                        ComponentIdsByMutation = row.ComponentIdsByMutation?.Select(pair => new { pair.Key, pair.Value }).ToArray() }).ToArray(),
                    resources.AcceptedPendingResolutions, AcceptedReactionExecutions = resources.AcceptedReactionExecutions.Select(ReadPressureZeroReactionDiagnosticData).ToArray(),
                    resources.AcceptedResolvedPendingRequestIds, resources.Statistics, resources.IsValid, Issues = ReadGuardianCappedIssues(resources.Issues),
                    FullEffectBoundary = ReadGuardianZeroTerminalEffectBoundaryDiagnosticData(transcript)
                },
                EffectPlan = ReadGuardianCappedEffectData(effects), EffectPayloadHashOperands = effectOperands,
                EffectPayloadFingerprint = WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects)
            },
            CurrentTerminalSeals = new { transcript.Fingerprint, transcript.IsComplete, transcript.UseProjectionOrdinal,
                transcript.PendingFrontierBoundaryOrdinal, effects.IsAcceptedBoundaryComplete, effects.AcceptedBoundaryBasePlanFingerprint,
                effects.AcceptedBoundaryFinalPlanFingerprint, leaseActive = lease.IsActive,
                publicationRegistered = AcceptedMechanicsPlanAuthority.HasValidated(setup.Copy.Context.FileSystem, lease) }
        });
        attempt.CompletedJson = json; _storageOutput.WriteLine(json);
        Assert.Null(reduction.LiveWoundCompletion);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects), WoundAcceptedTurnFingerprintWriter.Compute(effectOperands));
    }

    /// <summary>Captures full positive prepared DATA and hash operands once while its exact registered terminal seals are current.</summary>
    private async Task CaptureGuardianCappedPreparedAsync(GuardianAttributionJourney setup, GuardianCappedAttemptCapture attempt,
        AcceptedMechanicsPlan plan, FileSystemManager.CanonicalWriteLease lease)
    {
        Assert.NotNull(attempt.FirstJson); Assert.NotNull(attempt.CompletedJson); Assert.Null(attempt.PreparedJson);
        var context = setup.Copy.Context;
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekSpiritualPublication(context.FileSystem, lease, out var authority)); Assert.Same(plan, authority.Plan);
        var currentIssues = await authority.ValidateCurrentInputsAsync(context.FileSystem, lease);
        var effects = Assert.IsType<EffectAcceptedTurnPlan>(plan.EffectPlan); var effectOperands = ReadGuardianCappedEffectHashOperands(effects);
        var preparedOperands = ReadGuardianCappedPreparedHashOperands(plan); var pendingAuthority = plan.PendingPublicationAuthority; var projection = plan.ProjectionInput;
        var json = JsonSerializer.Serialize(new
        {
            phase = "guardian_capped_complete_prepared_data_envelope", setup.Name, Rank = setup.ExpectedRank,
            First = JsonNode.Parse(attempt.FirstJson), Completed = JsonNode.Parse(attempt.CompletedJson),
            Plan = new
            {
                plan.InputFingerprint, plan.PreparedPlanFingerprint, plan.DefinitionAfterImage, plan.StateAfterImage, plan.HistoryAfterImage,
                plan.AuthorityFingerprints, AuthorityFingerprintEntries = plan.AuthorityFingerprints.Enumerate().ToArray(),
                plan.EffectCarrierAfterImages, plan.EffectIdentityAfterImage, plan.PendingAfterImages, plan.PendingGmPacket, plan.AwaitsPendingResolution,
                PendingPublicationAuthority = pendingAuthority is null ? null : new { pendingAuthority.SessionId, pendingAuthority.AcceptedRequestId,
                    pendingAuthority.Turn, pendingAuthority.FullTurnFingerprint, pendingAuthority.SemanticTurnFingerprint, pendingAuthority.PendingStateFingerprint },
                plan.OwnerCompanionAfterImages, OwnerTransitions = plan.OwnerTransitions.Select(row => row.ToFingerprintNode()).ToArray(),
                plan.ResourceEvents, ProjectionInput = new { projection.Definitions, projection.State, projection.History, projection.OwnerAuthorityFingerprint },
                OwnerAuthorityFingerprint = plan.OwnerAuthority.Fingerprint, BeforeImages = ReadGuardianCappedBeforeImages(plan.BeforeImages), plan.TouchedPaths, plan.ConsumedPaths,
                EffectPlan = ReadGuardianCappedEffectData(effects), WoundStageBundle = (object?)null, plan.LiveWoundProofFingerprint, plan.WoundAnchorPlanFingerprint,
                DirectWoundPublicationAuthority = plan.DirectWoundPublicationAuthority?.Fingerprint,
                TreatmentResourcePublicationAuthority = plan.TreatmentResourcePublicationAuthority?.AuthorityFingerprint,
                JointRepairCommonAuthority = plan.JointRepairCommonAuthority?.Fingerprint,
                plan.WoundCarrierAfterImages, plan.WoundIdentityAfterImage, plan.WoundHistoryAfterImage, plan.IsMortalReceiptOnlyWoundPublication
            },
            EffectPayloadHashOperands = effectOperands, PreparedPlanHashOperands = preparedOperands,
            PublicationBinding = ReadGuardianCappedBinding(authority.Binding), PublicationInputs = ReadGuardianCappedBeforeImages(authority.PublicationInputs),
            SignedRollbackImages = ReadGuardianCappedBeforeImages(authority.SignedRollbackImages),
            CurrentTerminalSeals = new { validSeal = authority.HasValidSeal(), currentInputsAgree = authority.CurrentInputsAgree(context.FileSystem, lease),
                CurrentInputIssues = ReadGuardianCappedIssues(currentIssues), publicationRegistered = AcceptedMechanicsPlanAuthority.HasValidated(context.FileSystem, lease),
                registeredExactPlan = ReferenceEquals(plan, authority.Plan), leaseActive = lease.IsActive, effects.IsAcceptedBoundaryComplete,
                effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint, LiveWoundCompletionPresent = authority.LiveWoundCompletion is not null }
        });
        attempt.PreparedJson = json; _storageOutput.WriteLine(json);
        Assert.Empty(currentIssues); Assert.True(authority.HasValidSeal()); Assert.True(authority.CurrentInputsAgree(context.FileSystem, lease));
        Assert.Null(authority.LiveWoundCompletion); Assert.Null(plan.WoundStageBundle);
        Assert.Equal(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effects), WoundAcceptedTurnFingerprintWriter.Compute(effectOperands));
        Assert.Equal(plan.PreparedPlanFingerprint, WoundAcceptedTurnFingerprintWriter.Compute(preparedOperands));
        Assert.Equal(AcceptedMechanicsPlanFingerprints.ComputePrepared(plan), WoundAcceptedTurnFingerprintWriter.Compute(preparedOperands));
    }

    /// <summary>Reads the physical positive checkpoint/pending/command surfaces with explicit absence, exact bytes and fingerprints.</summary>
    private static async Task<JsonArray> ReadGuardianCappedTransportAsync(ResourceMaterializationTestContext context, FileSystemManager.CanonicalWriteLease lease)
    {
        var result = new JsonArray();
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath })
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            result.Add(JsonSerializer.SerializeToNode(new { path, Existed = bytes is not null, bytesBase64 = bytes is null ? null : Convert.ToBase64String(bytes),
                Fingerprint = new CanonicalBeforeImage(bytes is not null, bytes).Fingerprint }));
        }
        return result;
    }

    /// <summary>Reads an already detached present transport root, retaining its raw bytes separately.</summary>
    private static JsonObject ReadGuardianCappedTransportRoot(JsonArray rows, string path) => ReadGuardianAttributionJson(Convert.FromBase64String(
        Assert.Single(rows, row => row!["path"]!.GetValue<string>() == path)!["bytesBase64"]!.GetValue<string>()));

    /// <summary>Projects every raw image with explicit absence and exact bytes, without normalizing embedded JSON.</summary>
    private static object[] ReadGuardianCappedRawImages(IEnumerable<KeyValuePair<string, byte[]?>> images) => images.OrderBy(row => row.Key, StringComparer.Ordinal)
        .Select(row => (object)new { path = row.Key, Existed = row.Value is not null, bytesBase64 = row.Value is null ? null : Convert.ToBase64String(row.Value),
            Fingerprint = new CanonicalBeforeImage(row.Value is not null, row.Value).Fingerprint }).ToArray();

    /// <summary>Projects every retained before-image including explicit absence and its independently retained fingerprint.</summary>
    private static object[] ReadGuardianCappedBeforeImages(IReadOnlyDictionary<string, CanonicalBeforeImage> images) => images.OrderBy(row => row.Key, StringComparer.Ordinal)
        .Select(row => { var bytes = row.Value.Bytes; return (object)new { path = row.Key, row.Value.Existed,
            bytesBase64 = bytes is null ? null : Convert.ToBase64String(bytes), row.Value.Fingerprint }; }).ToArray();

    /// <summary>Projects the complete ordinary accepted binding; wound input absence is checked for this explicit-none lane.</summary>
    private static object ReadGuardianCappedBinding(AcceptedMechanicsPlanBinding binding)
    {
        Assert.Null(binding.WoundInput);
        return new { binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Realm, binding.Turn,
            binding.AcceptedEvents, binding.ResourceCommands, binding.EffectCommands, binding.PendingInput, binding.InternalInputs,
            binding.WoundCommands, WoundInput = (object?)null, binding.AuthorityFingerprints, BeforeImages = ReadGuardianCappedBeforeImages(binding.BeforeImages) };
    }

    /// <summary>Projects every issue field without count or byte limits.</summary>
    private static object[] ReadGuardianCappedIssues(IEnumerable<ValidationIssue> issues) => issues.Select(row => (object)new
    { row.FilePath, row.Severity, row.Message, row.Category, row.Code, row.Actor, row.Section, row.Expected, row.Actual, row.RepairHint, row.RepairTargetFiles }).ToArray();

    /// <summary>Projects named counters rather than ValueTuple's empty default JSON shape.</summary>
    private static object ReadGuardianCappedCounts(ValidationService.SpiritualOriginalTurnCapture capture)
    {
        var strain = capture.ReadExistingStrainReuseCounts(); var source = capture.ReadExistingPressureGroupComputationCounts();
        return new { strain.Queries, strain.SignedParentReads, strain.OrdinaryEvaluations, strain.ActualEvaluations, strain.FreshInputChecks, strain.FreshDependencyChecks, source };
    }

    /// <summary>Projects all effect payload and terminal fields, with declared and retained authority fingerprints kept distinct.</summary>
    private static object ReadGuardianCappedEffectData(EffectAcceptedTurnPlan effects) => new
    {
        effects.InputFingerprint, effects.CarrierAuthorityFingerprint, effects.SourceAuthorityFingerprint, effects.TargetAuthorityFingerprint,
        SkillScopeFingerprint = effects.SkillScopeAuthority?.Fingerprint, effects.AllocatedCombatantIds, effects.AllocatedEffectIds, effects.AllocatedTransitionIds,
        effects.WoundApplicationRootEffectBindings, effects.Sources, effects.Targets, effects.SourceBindings,
        DeferredReactions = effects.DeferredReactions.Select(ReadPressureZeroReactionDiagnosticData).ToArray(),
        DeferredRoutingReactions = effects.DeferredRoutingReactions.Select(ReadPressureZeroReactionDiagnosticData).ToArray(),
        effects.ReactionExpansionCount, ReactionExpansionUsage = effects.ReactionExpansionUsage.Select(row => new { row.Key, row.Value }).ToArray(),
        effects.ActiveEffects, effects.ResourceTriggerCarriers, effects.AcceptedCarrierBaselines, effects.EventInput,
        effects.CarrierBeforeImages, effects.CarrierAfterImages, effects.IdentityIndexBeforeImage, effects.IdentityIndexAfterImage,
        effects.TouchedPaths, effects.DeletedPaths, effects.HasDeferredReactions, effects.IsAcceptedBoundaryComplete,
        effects.AcceptedBoundaryBasePlanFingerprint, effects.AcceptedBoundaryFinalPlanFingerprint,
        LifecycleExecutionLedger = effects.LifecycleExecutionLedger?.ToJsonObject(), LifecycleExecutionLedgerFingerprint = effects.LifecycleExecutionLedger?.Fingerprint,
        MortalRecoveryExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalRecoveryExecutionFingerprint(effects),
        SpiritualHealingExecutionFingerprint = EffectAcceptedTurnPlanner.GetSpiritualHealingExecutionFingerprint(effects),
        MortalDiagnosisExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalDiagnosisExecutionFingerprint(effects),
        MortalTreatmentExecutionFingerprint = EffectAcceptedTurnPlanner.GetMortalTreatmentExecutionFingerprint(effects),
        RetainedSourceAuthorityFingerprint = effects.SourceAuthority.Fingerprint, RetainedTargetAuthorityFingerprint = effects.TargetAuthority.Fingerprint
    };

    /// <summary>Reads every ordered nullable effect payload operand directly from the typed positive producer, without fixed counts or allocation assumptions.</summary>
    private static IReadOnlyList<string?> ReadGuardianCappedEffectHashOperands(EffectAcceptedTurnPlan plan)
    {
        var fields = new List<string?> { "book_of_eternity.effect.accepted_plan_payload", "1", plan.InputFingerprint,
            plan.CarrierAuthorityFingerprint, plan.SourceAuthorityFingerprint, plan.TargetAuthorityFingerprint, plan.SkillScopeAuthority?.Fingerprint ?? "none" };
        foreach (var (tag, value) in new[]
        {
            ("mortal_recovery_execution", EffectAcceptedTurnPlanner.GetMortalRecoveryExecutionFingerprint(plan)),
            ("spiritual_healing_execution", EffectAcceptedTurnPlanner.GetSpiritualHealingExecutionFingerprint(plan)),
            ("mortal_diagnosis_execution", EffectAcceptedTurnPlanner.GetMortalDiagnosisExecutionFingerprint(plan)),
            ("mortal_treatment_execution", EffectAcceptedTurnPlanner.GetMortalTreatmentExecutionFingerprint(plan))
        }) if (value is not null) { fields.Add(tag); fields.Add(value); }
        GuardianCappedAppendStrings(fields, plan.AllocatedCombatantIds, indexed: true);
        GuardianCappedAppendStrings(fields, plan.AllocatedEffectIds, indexed: true);
        GuardianCappedAppendStrings(fields, plan.AllocatedTransitionIds, indexed: true);
        GuardianCappedAppendIndexed(fields, plan.WoundApplicationRootEffectBindings, row => { fields.Add(row?.ApplicationRef); fields.Add(row?.EffectId); });
        GuardianCappedAppendIndexed(fields, plan.Sources, row => GuardianCappedAppendSource(fields, row));
        GuardianCappedAppendIndexed(fields, plan.Targets, row => GuardianCappedAppendTarget(fields, row));
        GuardianCappedAppendIndexed(fields, plan.SourceBindings, row => GuardianCappedAppendSourceBinding(fields, row));
        GuardianCappedAppendIndexed(fields, plan.DeferredReactions, row => GuardianCappedAppendReaction(fields, row));
        fields.Add(GuardianCappedNumber(plan.ReactionExpansionCount));
        GuardianCappedAppendIndexed(fields, plan.ReactionExpansionUsage.OrderBy(row => row.Key.EffectId, StringComparer.Ordinal)
            .ThenBy(row => row.Key.ComponentId, StringComparer.Ordinal).ToArray(), row =>
            { fields.Add(row.Key.EffectId); fields.Add(row.Key.ComponentId); fields.Add(GuardianCappedNumber(row.Value.Count)); fields.Add(GuardianCappedNumber(row.Value.Maximum)); });
        GuardianCappedAppendIndexed(fields, plan.ActiveEffects, row => fields.Add(GuardianCappedCanonical(row)));
        GuardianCappedAppendCarriers(fields, plan.ResourceTriggerCarriers);
        fields.Add(plan.SourceAuthority?.Fingerprint); fields.Add(plan.TargetAuthority?.Fingerprint); fields.Add(GuardianCappedCanonical(plan.EventInput));
        GuardianCappedAppendMap(fields, plan.CarrierBeforeImages.Select(row => (row.Key, (JsonObject?)row.Value)), indexed: true);
        GuardianCappedAppendMap(fields, plan.CarrierAfterImages.Select(row => (row.Key, (JsonObject?)row.Value)), indexed: true);
        fields.Add(GuardianCappedCanonical(plan.IdentityIndexBeforeImage)); fields.Add(GuardianCappedCanonical(plan.IdentityIndexAfterImage));
        GuardianCappedAppendStrings(fields, plan.TouchedPaths, indexed: true); GuardianCappedAppendStrings(fields, plan.DeletedPaths, indexed: true);
        GuardianCappedAppendCarriers(fields, plan.AcceptedCarrierBaselines);
        return fields.ToArray();
    }

    /// <summary>Reads every prepared-plan operand in production order, including actual positive pending branches and every exact before-image.</summary>
    private static IReadOnlyList<string?> ReadGuardianCappedPreparedHashOperands(AcceptedMechanicsPlan plan)
    {
        var fields = new List<string?> { "book_of_eternity.accepted_mechanics.prepared_plan", "1", plan.InputFingerprint,
            GuardianCappedCanonical(plan.DefinitionAfterImage), GuardianCappedCanonical(plan.StateAfterImage), GuardianCappedCanonical(plan.HistoryAfterImage) };
        var authorities = plan.AuthorityFingerprints.Enumerate().OrderBy(row => row.Key, StringComparer.Ordinal).ToArray();
        fields.Add(GuardianCappedNumber(authorities.Length)); foreach (var row in authorities) { fields.Add(row.Key); fields.Add(row.Value); }
        GuardianCappedAppendMap(fields, plan.EffectCarrierAfterImages.Select(row => (row.Key, (JsonObject?)row.Value)));
        fields.Add(GuardianCappedCanonical(plan.EffectIdentityAfterImage));
        GuardianCappedAppendMap(fields, plan.PendingAfterImages.Select(row => (row.Key, row.Value)), optionalValues: true);
        GuardianCappedAppendOptional(fields, plan.PendingGmPacket);
        if (plan.PendingPublicationAuthority is { } pending)
        {
            fields.Add("present"); fields.Add(pending.SessionId); fields.Add(pending.AcceptedRequestId); fields.Add(GuardianCappedNumber(pending.Turn));
            fields.Add(pending.FullTurnFingerprint); fields.Add(pending.SemanticTurnFingerprint); fields.Add(pending.PendingStateFingerprint);
        }
        else fields.Add(null);
        GuardianCappedAppendMap(fields, plan.OwnerCompanionAfterImages.Select(row => (row.Key, (JsonObject?)row.Value)));
        var owners = plan.OwnerTransitions; fields.Add(GuardianCappedNumber(owners.Count)); foreach (var row in owners) fields.Add(GuardianCappedCanonical(row.ToFingerprintNode()));
        var events = plan.ResourceEvents; fields.Add(GuardianCappedNumber(events.Count));
        foreach (var row in events)
        {
            fields.Add(row.EventKind); fields.Add(row.OperationId); fields.Add(row.EventRef); fields.Add(row.Coordinate.Realm);
            fields.Add(row.Coordinate.OwnerKind.ToString()); fields.Add(row.Coordinate.ResourceOwnerId); fields.Add(row.Coordinate.ResourceKey);
            fields.Add(row.Before.ToString("G29", CultureInfo.InvariantCulture)); fields.Add(row.After.ToString("G29", CultureInfo.InvariantCulture));
            fields.Add(row.AppliedAmount.ToString("G29", CultureInfo.InvariantCulture)); fields.Add(GuardianCappedNumber(row.Turn));
            fields.Add(GuardianCappedNumber(row.ExecutionSequence)); fields.Add(row.SourceFingerprint);
        }
        fields.Add(GuardianCappedCanonical(plan.ProjectionInput.Definitions)); fields.Add(GuardianCappedCanonical(plan.ProjectionInput.State));
        fields.Add(GuardianCappedCanonical(plan.ProjectionInput.History)); fields.Add(plan.ProjectionInput.OwnerAuthorityFingerprint); fields.Add(plan.OwnerAuthority.Fingerprint);
        if (plan.EffectPlan is { } effect) { fields.Add("present"); fields.Add(WoundAcceptedTurnFingerprints.ComputeAcceptedEffectPlanPayload(effect)); } else fields.Add(null);
        // This is explicit none, not wound insertion/treatment. Keep the absent stage framing rather than borrowing a zero-lane envelope shape.
        Assert.Null(plan.WoundStageBundle); fields.Add(null);
        fields.Add(plan.LiveWoundProofFingerprint); fields.Add(plan.WoundAnchorPlanFingerprint);
        fields.Add(plan.DirectWoundPublicationAuthority?.Fingerprint); fields.Add(plan.TreatmentResourcePublicationAuthority?.AuthorityFingerprint);
        if (plan.JointRepairCommonAuthority is { } joint) { fields.Add("mortal_joint_repair_common"); fields.Add(joint.Fingerprint); }
        GuardianCappedAppendMap(fields, plan.WoundCarrierAfterImages.Select(row => (row.Key, (JsonObject?)row.Value)));
        GuardianCappedAppendOptional(fields, plan.WoundIdentityAfterImage); GuardianCappedAppendOptional(fields, plan.WoundHistoryAfterImage);
        var before = plan.BeforeImages.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(); fields.Add(GuardianCappedNumber(before.Length));
        foreach (var row in before) { var bytes = row.Value.Bytes; fields.Add(row.Key); fields.Add(row.Value.Existed ? "present" : "missing"); fields.Add(bytes is null ? null : Convert.ToBase64String(bytes)); }
        GuardianCappedAppendStrings(fields, plan.TouchedPaths); GuardianCappedAppendStrings(fields, plan.ConsumedPaths);
        return fields.ToArray();
    }

    /// <summary>Appends the complete actual ordered list, preserving production count/index framing without fixed dimensions.</summary>
    private static void GuardianCappedAppendIndexed<T>(ICollection<string?> fields, IReadOnlyList<T> rows, Action<T> append)
    { fields.Add(GuardianCappedNumber(rows.Count)); for (var index = 0; index < rows.Count; index++) { fields.Add(GuardianCappedNumber(index)); append(rows[index]); } }

    /// <summary>Appends ordered strings using the producer's optional index framing.</summary>
    private static void GuardianCappedAppendStrings(ICollection<string?> fields, IReadOnlyList<string> rows, bool indexed = false)
    { fields.Add(GuardianCappedNumber(rows.Count)); for (var index = 0; index < rows.Count; index++) { if (indexed) fields.Add(GuardianCappedNumber(index)); fields.Add(rows[index]); } }

    /// <summary>Appends every map entry in the producer's ordinal path order, retaining optional-value and effect-index framing.</summary>
    private static void GuardianCappedAppendMap(ICollection<string?> fields, IEnumerable<(string Key, JsonObject? Value)> values, bool indexed = false, bool optionalValues = false)
    {
        var rows = values.OrderBy(row => row.Key, StringComparer.Ordinal).ToArray(); fields.Add(GuardianCappedNumber(rows.Length));
        for (var index = 0; index < rows.Length; index++)
        { if (indexed) fields.Add(GuardianCappedNumber(index)); fields.Add(rows[index].Key); if (optionalValues) GuardianCappedAppendOptional(fields, rows[index].Value); else fields.Add(GuardianCappedCanonical(rows[index].Value)); }
    }

    /// <summary>Appends present or null JSON exactly as the common prepared-plan producer.</summary>
    private static void GuardianCappedAppendOptional(ICollection<string?> fields, JsonObject? value)
    { fields.Add(value is null ? null : "present"); if (value is not null) fields.Add(GuardianCappedCanonical(value)); }

    /// <summary>Appends every retained carrier root including explicit nulls in the effect producer's declared order.</summary>
    private static void GuardianCappedAppendCarriers(ICollection<string?> fields, EffectCarrierCatalogInput? carriers)
    {
        if (carriers is null) { fields.Add(null); return; } fields.Add("present");
        foreach (var value in new[] { carriers.PlayerEffects, carriers.NpcEffects, carriers.EnemyCombatants, carriers.AllyCombatants, carriers.AfterlifeProfiles, carriers.SpiritualConflict }) fields.Add(GuardianCappedCanonical(value));
    }

    /// <summary>Appends a nullable complete source key without converting absence to an empty source.</summary>
    private static void GuardianCappedAppendSource(ICollection<string?> fields, EffectSourceKey? row)
    { fields.Add(row?.Realm); fields.Add(row?.Kind); fields.Add(row?.SourceId); fields.Add(row?.DefinitionKey); }

    /// <summary>Appends a nullable complete target key.</summary>
    private static void GuardianCappedAppendTarget(ICollection<string?> fields, EffectTargetKey? row)
    { fields.Add(row?.Realm); fields.Add(row?.Kind); fields.Add(row?.TargetId); }

    /// <summary>Appends the complete retained source binding, including sorted predicates with no invented count.</summary>
    private static void GuardianCappedAppendSourceBinding(ICollection<string?> fields, EffectSourceAuthorityEntry? row)
    {
        GuardianCappedAppendSource(fields, row?.Key); fields.Add(GuardianCappedCanonical(row?.Definition));
        fields.Add(row is null ? null : row.Materializable ? "true" : "false"); fields.Add(row is null ? null : row.Active ? "true" : "false"); fields.Add(row is null ? null : row.SameTurn ? "true" : "false");
        fields.Add(row?.SourceRef); var predicates = row?.SatisfiedPredicates?.OrderBy(value => value, StringComparer.Ordinal).ToArray();
        fields.Add(predicates is null ? null : GuardianCappedNumber(predicates.Length));
        if (predicates is not null) for (var index = 0; index < predicates.Length; index++) { fields.Add(GuardianCappedNumber(index)); fields.Add(predicates[index]); }
        fields.Add(row?.RequiredApplicationAuthority);
    }

    /// <summary>Appends all reaction operands, including nullable downstream and replacement authority branches.</summary>
    private static void GuardianCappedAppendReaction(ICollection<string?> fields, EffectReactionExecution? row)
    {
        fields.Add(row?.EventRef); fields.Add(row?.TriggerEventRef); fields.Add(row?.CausalEventRef); fields.Add(row is null ? null : GuardianCappedNumber(row.Turn));
        fields.Add(row?.EventKind); GuardianCappedAppendTarget(fields, row?.Target); fields.Add(row?.EffectId); fields.Add(row?.TriggerId); fields.Add(row?.ComponentId);
        fields.Add(row?.ResultKind); fields.Add(row?.Dependency); fields.Add(row?.AfterComponentId); fields.Add(row is null ? null : GuardianCappedNumber(row.MaxExpansion));
        GuardianCappedAppendSourceBinding(fields, row?.DownstreamSource); fields.Add(GuardianCappedCanonical(row?.Parameters)); GuardianCappedAppendSource(fields, row?.DownstreamSourceKey);
        fields.Add(row is null ? null : GuardianCappedNumber(row.ComponentPriority)); fields.Add(row?.ReplacementTarget?.EffectId);
        fields.Add(row?.ReplacementTarget?.Authority.BindingKind); fields.Add(row?.ReplacementTarget?.Authority.AuthorityId);
    }

    /// <summary>Uses the existing canonical hash-input writer, never normalizing stored embedded raw JSON strings.</summary>
    private static string? GuardianCappedCanonical(JsonNode? value) => WoundAcceptedTurnFingerprintWriter.CanonicalJson(value);
    /// <summary>Formats an actual retained count or ordinal with the producer's invariant number format.</summary>
    private static string GuardianCappedNumber(int value) => value.ToString(CultureInfo.InvariantCulture);
}
