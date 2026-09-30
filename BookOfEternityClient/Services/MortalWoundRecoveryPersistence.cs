using System.Collections.Immutable;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace BookOfEternityClient.Services;

/// <summary>
/// Records the complete before and after evidence for one actually published recovery stage.
/// </summary>
/// <param name="TransitionId">
/// The exact allocated history transition identity.
/// </param>
/// <param name="Kind">
/// The accepted stage kind: recover or heal.
/// </param>
/// <param name="BeforeFingerprint">
/// The semantic fingerprint recomputed from <paramref name="BeforeWound"/>.
/// </param>
/// <param name="AfterFingerprint">
/// The semantic fingerprint recomputed from <paramref name="AfterWound"/>.
/// </param>
/// <param name="Terminal">
/// Whether this is the final, terminal healing stage.
/// </param>
/// <param name="Turn">
/// The original accepted turn shared by the evaluation's stages.
/// </param>
/// <param name="EventRef">
/// The exact accepted event reference used by the history row.
/// </param>
/// <param name="OperationKey">
/// The exact operation identity used by the history row.
/// </param>
/// <param name="BeforeWound">
/// The complete canonical wound immediately before this stage.
/// </param>
/// <param name="AfterWound">
/// The complete canonical wound immediately after this stage.
/// </param>
internal sealed record MortalWoundRecoveryPublishedStage(
    string TransitionId, string Kind, string BeforeFingerprint, string AfterFingerprint,
    bool Terminal, int Turn, string EventRef, string OperationKey,
    WoundMaterializationEnvelope BeforeWound, WoundMaterializationEnvelope AfterWound);

/// <summary>
/// Stores detached recovery comparison and replay evidence without granting live publication authority.
/// </summary>
[JsonConverter(typeof(WoundTransitionResultJsonConverter))]
internal sealed class MortalWoundRecoveryPersistedResult : WoundTransitionResult
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() }
    };
    private readonly JsonObject _canonical;

    /// <summary>
    /// Freezes evidence already reconstructed by the strict parser.
    /// </summary>
    /// <param name="canonical">
    /// The complete verified result projection, cloned before retention.
    /// </param>
    /// <param name="source">
    /// The verified original scalar source evidence; its event values are frozen.
    /// </param>
    /// <param name="binding">
    /// The verified original binding, cloned with its immutable event vector.
    /// </param>
    /// <param name="sourceWound">
    /// The immutable wound parsed from the complete canonical source snapshot.
    /// </param>
    /// <param name="resolution">
    /// The reconstructed original resolution whose intent vector is frozen.
    /// </param>
    /// <param name="stages">
    /// Complete canonical stage snapshots retained in an immutable vector.
    /// </param>
    /// <param name="recoveryConsumption">
    /// The verified exact recovery epoch consumption.
    /// </param>
    /// <param name="deteriorationConsumption">
    /// The verified condition consumption, or null without a condition epoch.
    /// </param>
    /// <param name="receipt">
    /// The reconstructed original four-field receipt.
    /// </param>
    private MortalWoundRecoveryPersistedResult(JsonObject canonical,
        MortalWoundRecoverySourceEvidence source, WoundAcceptedTurnBinding binding,
        WoundMaterializationEnvelope sourceWound, MortalWoundRecoveryResolution resolution,
        IReadOnlyList<MortalWoundRecoveryPublishedStage> stages,
        MortalWoundRecoveryEpochConsumption recoveryConsumption,
        MortalWoundRecoveryEpochConsumption? deteriorationConsumption,
        MortalWoundRecoveryReceipt receipt)
    {
        _canonical = canonical.DeepClone().AsObject();
        Source = source with { AcceptedD20EventValues = source.AcceptedD20EventValues.ToImmutableArray() };
        Binding = WoundAcceptedTurnData.CloneBinding(binding)!;
        SourceWound = sourceWound;
        Resolution = resolution with { TransitionIntents = resolution.TransitionIntents.ToImmutableArray() };
        Stages = stages.ToImmutableArray();
        RecoveryConsumption = recoveryConsumption;
        DeteriorationConsumption = deteriorationConsumption;
        Receipt = receipt;
    }

    /// <summary>
    /// Gets the registered recover result discriminator.
    /// </summary>
    public override string Kind => "recover";
    /// <summary>
    /// Gets immutable detached original scalar comparison evidence.
    /// </summary>
    internal MortalWoundRecoverySourceEvidence Source { get; }
    /// <summary>
    /// Gets the detached original accepted binding whose event vector is immutable.
    /// </summary>
    internal WoundAcceptedTurnBinding Binding { get; }
    /// <summary>
    /// Gets the complete immutable canonical wound before the evaluation.
    /// </summary>
    internal WoundMaterializationEnvelope SourceWound { get; }
    /// <summary>
    /// Gets the reconstructed original resolution and immutable ordered intents.
    /// </summary>
    internal MortalWoundRecoveryResolution Resolution { get; }
    /// <summary>
    /// Gets the immutable ordered complete executed stage snapshots.
    /// </summary>
    internal IReadOnlyList<MortalWoundRecoveryPublishedStage> Stages { get; }
    /// <summary>
    /// Gets consumption belonging to the exact original recovery epoch.
    /// </summary>
    internal MortalWoundRecoveryEpochConsumption RecoveryConsumption { get; }
    /// <summary>
    /// Gets original condition-epoch consumption, or null when the source has no condition epoch.
    /// </summary>
    internal MortalWoundRecoveryEpochConsumption? DeteriorationConsumption { get; }
    /// <summary>
    /// Gets the original four-field receipt shared by the entire logical evaluation.
    /// </summary>
    internal MortalWoundRecoveryReceipt Receipt { get; }
    /// <summary>
    /// Returns a detached clone of every canonical recovery result field.
    /// </summary>
    /// <returns>
    /// A complete mutable JSON copy whose changes cannot alter the retained evidence.
    /// </returns>
    internal override JsonObject ToCanonicalJson() => _canonical.DeepClone().AsObject();

    /// <summary>
    /// Builds durable evidence from a genuine accepted source and the exact executed stage chain.
    /// </summary>
    /// <param name="acceptedState">
    /// The registry-owned original accepted state, including its complete history baseline.
    /// </param>
    /// <param name="binding">
    /// The exact binding that authorized the original recovery evaluation.
    /// </param>
    /// <param name="resolution">
    /// The immutable resolution issued for this accepted source.
    /// </param>
    /// <param name="stages">
    /// The complete ordered executed stages, including an initial recover stage and any final heal stage.
    /// </param>
    /// <returns>
    /// Strictly reconstructed, detached history evidence; inconsistent sources or stages are rejected.
    /// </returns>
    internal static MortalWoundRecoveryPersistedResult Create(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        WoundAcceptedTurnBinding binding, MortalWoundRecoveryResolution resolution,
        IReadOnlyList<MortalWoundRecoveryPublishedStage> stages)
    {
        ArgumentNullException.ThrowIfNull(acceptedState);
        if (!acceptedState.AgreesWithBindingAndWound(binding, acceptedState.CurrentWound.WoundId))
            throw new InvalidDataException("Recovery evidence does not match the original accepted binding.");
        var consumed = acceptedState.History.GetRecoveryConsumption(acceptedState.CurrentWound);
        return CreateComparisonEvidence(MortalWoundRecoverySourceEvidence.FromAcceptedState(acceptedState),
            binding, acceptedState.CurrentWound, resolution, stages, consumed.Recovery, consumed.Deterioration);
    }

    /// <summary>
    /// Encodes comparison evidence and verifies it through the same closed parser used by history.
    /// </summary>
    /// <param name="source">
    /// Original scalar evidence whose fingerprints must reconstruct from the retained coordinates.
    /// </param>
    /// <param name="binding">
    /// The original accepted binding and complete ordered event set.
    /// </param>
    /// <param name="wound">
    /// The complete wound before the evaluation.
    /// </param>
    /// <param name="resolution">
    /// The original typed evaluation to be reconstructed.
    /// </param>
    /// <param name="stages">
    /// The complete executed stage evidence; this method does not execute or authorize stages.
    /// </param>
    /// <param name="consumedRecovery">
    /// The previous consumed ordinal in this exact recovery epoch.
    /// </param>
    /// <param name="consumedDeterioration">
    /// The previous consumed ordinal in this exact deterioration epoch.
    /// </param>
    /// <returns>
    /// Detached durable evidence that cannot substitute for a registry-owned continuation.
    /// </returns>
    internal static MortalWoundRecoveryPersistedResult CreateComparisonEvidence(
        MortalWoundRecoverySourceEvidence source, WoundAcceptedTurnBinding binding,
        WoundMaterializationEnvelope wound, MortalWoundRecoveryResolution resolution,
        IReadOnlyList<MortalWoundRecoveryPublishedStage> stages,
        long consumedRecovery, long consumedDeterioration)
    {
        var anchor = wound.Recovery.RecoveryAnchor
            ?? throw new InvalidDataException("Recovery evidence has no epoch.");
        var consumesRecovery = resolution.TransitionIntents.OfType<MortalWoundRecoveryProgressIntent>().Any();
        var recoveryConsumption = new MortalWoundRecoveryEpochConsumption(anchor.AnchorKind, null,
            anchor.AnchorMinute, anchor.AnchorTransitionId, consumedRecovery,
            checked(consumedRecovery + (consumesRecovery ? resolution.ElapsedCadences : 0)));
        var condition = wound.Recovery.DeteriorationAnchor;
        var deteriorationConsumption = condition is null ? null : new MortalWoundRecoveryEpochConsumption(
            "condition", condition.ConditionKey, condition.AnchorMinute, condition.AnchorTransitionId,
            consumedDeterioration, checked(consumedDeterioration + resolution.ElapsedDeteriorationCadences));
        var receipt = CreateReceipt(resolution, wound.WoundId);
        var canonical = new JsonObject
        {
            ["kind"] = "recover", ["schemaVersion"] = 1,
            ["source"] = JsonSerializer.SerializeToNode(source, Options),
            ["binding"] = BindingJson(binding), ["sourceWound"] = WoundJson(wound),
            ["resolution"] = ResolutionJson(resolution),
            ["recoveryConsumption"] = JsonSerializer.SerializeToNode(recoveryConsumption, Options),
            ["deteriorationConsumption"] = JsonSerializer.SerializeToNode(deteriorationConsumption, Options),
            ["stages"] = new JsonArray(stages.Select(StageJson).Cast<JsonNode?>().ToArray()),
            ["receipt"] = JsonSerializer.SerializeToNode(receipt, Options)
        };
        canonical["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(canonical);
        var issues = new List<ValidationIssue>();
        var parsed = Parse(JsonSerializer.SerializeToElement(canonical), "recovery.transitionResult", issues);
        return parsed ?? throw new InvalidDataException(string.Join("; ", issues.Select(issue =>
            $"{issue.Code}@{issue.FilePath}: {issue.Actual}")));
    }

    /// <summary>
    /// Parses closed recovery evidence, reconstructing its source, math, intents, receipt and stage seals.
    /// </summary>
    /// <param name="element">
    /// The complete recover result object; missing, null or open values are invalid.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of this result in canonical history.
    /// </param>
    /// <param name="issues">
    /// The collection receiving precise structural or semantic failures.
    /// </param>
    /// <returns>
    /// A detached result when all checks succeed; otherwise null with validation issues.
    /// </returns>
    internal static MortalWoundRecoveryPersistedResult? Parse(
        JsonElement element, string path, ICollection<ValidationIssue> issues)
    {
        var before = issues.Count;
        ValidateDuplicates(element, path, issues);
        Closed(element, path, issues, "kind", "schemaVersion", "source", "binding", "sourceWound",
            "resolution", "recoveryConsumption", "deteriorationConsumption", "stages", "receipt", "resultFingerprint");
        if (issues.Count != before)
            return null;
        try
        {
            if (element.GetProperty("kind").GetString() != "recover" ||
                element.GetProperty("schemaVersion").GetInt32() != 1)
                throw new InvalidDataException("Expected recover schema version 1.");
            var sourceElement = element.GetProperty("source");
            Closed(sourceElement, path + ".source", issues,
                "sessionGeneration", "bindingFingerprint", "context", "contextFingerprint", "acceptedD20EventValues",
                "woundSourcePath", "woundFingerprint", "historyFingerprint", "historyNextOrdinal", "identityFingerprint",
                "clockFingerprint", "currentGameMinute", "effectFingerprint", "itemResourceFingerprint",
                "actorLocationFingerprint", "playerCapabilityCatalogFingerprint", "npcCapabilityCatalogFingerprint",
                "skillSourceFingerprint", "requirementSnapshotFingerprint", "acceptedStateFingerprint");
            Closed(sourceElement.GetProperty("context"), path + ".source.context", issues,
                "schemaVersion", "realm", "targetKind", "targetId", "providerKind", "providerId", "currentLocationId");
            var source = sourceElement.Deserialize<MortalWoundRecoverySourceEvidence>(Options)!;
            var binding = ParseBinding(element.GetProperty("binding"), path + ".binding", issues);
            var wound = ParseWound(element.GetProperty("sourceWound"), path + ".sourceWound", issues);
            var resolution = ParseResolution(element.GetProperty("resolution"), path + ".resolution", issues);
            var recoveryConsumption = ParseConsumption(element.GetProperty("recoveryConsumption"), path + ".recoveryConsumption", issues);
            var deteriorationElement = element.GetProperty("deteriorationConsumption");
            var deteriorationConsumption = deteriorationElement.ValueKind == JsonValueKind.Null ? null :
                ParseConsumption(deteriorationElement, path + ".deteriorationConsumption", issues);
            Closed(element.GetProperty("receipt"), path + ".receipt", issues,
                "authorityFingerprint", "receiptFingerprint", "tickKey", "woundId");
            var receipt = element.GetProperty("receipt").Deserialize<MortalWoundRecoveryReceipt>(Options)!;
            var stages = ParseStages(element.GetProperty("stages"), path + ".stages", issues);
            if (issues.Count != before || wound is null || binding is null || resolution is null || recoveryConsumption is null)
                return null;
            ValidateSource(source, binding, wound);
            ValidateConsumption(wound, recoveryConsumption, deteriorationConsumption, resolution);
            var reconstructed = MortalWoundRecoveryPlanner.ReconstructResolution(source, binding, wound,
                recoveryConsumption.ConsumedBefore, deteriorationConsumption?.ConsumedBefore ?? 0);
            if (reconstructed.Resolution is null || !SameJson(ResolutionJson(resolution), ResolutionJson(reconstructed.Resolution)))
                throw new InvalidDataException("Resolution does not reconstruct from its original source and consumption.");
            if (receipt != CreateReceipt(resolution, wound.WoundId))
                throw new InvalidDataException("Receipt does not reconstruct from the original resolution.");
            ValidateStages(source, binding, wound, resolution, stages);
            var canonical = JsonNode.Parse(element.GetRawText())!.AsObject();
            if (element.GetProperty("resultFingerprint").GetString() != WoundHistoryState.ComputeTransitionResultFingerprint(canonical))
                throw new InvalidDataException("Recovery result fingerprint mismatch.");
            return new(canonical, source, binding, wound, reconstructed.Resolution, stages,
                recoveryConsumption, deteriorationConsumption, receipt);
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or
            InvalidDataException or ArgumentException or OverflowException or KeyNotFoundException or NullReferenceException or FormatException)
        {
            AddIssue(issues, path, "mortal_wound_recovery_result_invalid", exception.Message);
            return null;
        }
    }

    /// <summary>
    /// Recomputes accepted source seals from detached original coordinates.
    /// </summary>
    /// <param name="source">
    /// Original scalar source evidence with retained component seals.
    /// </param>
    /// <param name="binding">
    /// The full original accepted event binding.
    /// </param>
    /// <param name="wound">
    /// The complete canonical source wound.
    /// </param>
    /// <returns>
    /// Comparison evidence with reconstructed binding, context, clock, skill-source and accepted-state seals.
    /// </returns>
    internal static MortalWoundRecoverySourceEvidence SealSourceEvidence(
        MortalWoundRecoverySourceEvidence source, WoundAcceptedTurnBinding binding, WoundMaterializationEnvelope wound)
    {
        var context = source.Context;
        var bindingFields = new List<string?>
        {
            "mortal_wound_treatment_binding", "1", binding.SessionId, binding.RequestId, binding.SnapshotToken,
            binding.Realm, Number(binding.Turn), binding.AcceptedEventsFingerprint, Number(binding.AcceptedEvents.Count)
        };
        foreach (var value in binding.AcceptedEvents)
            bindingFields.AddRange(new[] { value.EventRef, value.Kind, value.AuthorityId, value.SemanticFingerprint });
        source = source with
        {
            BindingFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(bindingFields),
            WoundFingerprint = WoundIdentityState.ComputeSemanticFingerprint(wound),
            ContextFingerprint = Hash("mortal_wound_treatment_context", source.SessionGeneration,
                binding.SessionId, binding.RequestId, binding.SnapshotToken, Number(binding.Turn),
                Number(context.SchemaVersion), context.Realm, context.TargetKind, context.TargetId,
                context.ProviderKind, context.ProviderId, context.CurrentLocationId),
            ClockFingerprint = Hash("mortal_wound_treatment_clock", EffectAcceptedTurnInputComposer.WorldTimePath,
                Number(source.CurrentGameMinute)),
            SkillSourceFingerprint = Hash("mortal_wound_treatment_skill_sources",
                source.PlayerCapabilityCatalogFingerprint, source.NpcCapabilityCatalogFingerprint)
        };
        var request = Hash("mortal_wound_treatment_request_event_source", binding.SessionId, binding.RequestId,
            Number(binding.Turn), string.Join(",", source.AcceptedD20EventValues.Select(static value => Number(value))));
        return source with { AcceptedStateFingerprint = Hash("mortal_wound_treatment_accepted_state", "1",
            source.SessionGeneration, source.BindingFingerprint, source.ContextFingerprint, request,
            source.WoundFingerprint, source.WoundSourcePath, source.IdentityFingerprint, source.HistoryFingerprint,
            source.ClockFingerprint, source.EffectFingerprint, source.ItemResourceFingerprint, source.ActorLocationFingerprint,
            source.PlayerCapabilityCatalogFingerprint, source.NpcCapabilityCatalogFingerprint,
            source.SkillSourceFingerprint, source.RequirementSnapshotFingerprint) };
    }

    /// <summary>
    /// Checks original source coordinates and recomputes accepted binding and source seals.
    /// </summary>
    /// <param name="source">
    /// The detached original accepted source evidence.
    /// </param>
    /// <param name="binding">
    /// The original binding and ordered accepted events.
    /// </param>
    /// <param name="wound">
    /// The complete canonical wound before evaluation.
    /// </param>
    private static void ValidateSource(MortalWoundRecoverySourceEvidence source,
        WoundAcceptedTurnBinding binding, WoundMaterializationEnvelope wound)
    {
        if (source.Context.SchemaVersion != 1 || source.Context.Realm != "mortal_world" ||
            binding.Realm != "mortal_world" || binding.Turn < 0 || source.CurrentGameMinute < 0 ||
            source.HistoryNextOrdinal is < 1 or > WoundHistoryState.MaxTransitions + 1 ||
            string.IsNullOrWhiteSpace(source.SessionGeneration) || string.IsNullOrWhiteSpace(source.WoundSourcePath) ||
            source.AcceptedD20EventValues.Count == 0 || source.AcceptedD20EventValues.Any(value => value is < 1 or > 20) ||
            wound.Lifecycle != "active" || wound.Owner.Realm != "mortal_world" || wound.Classification.Domain != "physical" ||
            wound.Recovery.ClockKind != "world_time.currentTimeInMinutes" ||
            binding.AcceptedEvents.Count == 0 || binding.AcceptedEventsFingerprint != WoundAcceptedEventSetFingerprint.Compute(binding.AcceptedEvents))
            throw new InvalidDataException("Invalid original accepted source coordinates.");
        var sealedSource = SealSourceEvidence(source, binding, wound);
        if (!SameJson(JsonSerializer.SerializeToNode(source, Options)!, JsonSerializer.SerializeToNode(sealedSource, Options)!))
            throw new InvalidDataException("Accepted source fingerprints do not reconstruct.");
        var originalEvents = WoundAcceptedEventAuthorityComposer.ComposeDefaultAcceptedTurn(
            binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Turn);
        if (!originalEvents.Success || binding.AcceptedEventsFingerprint != originalEvents.EventsFingerprint ||
            !binding.AcceptedEvents.SequenceEqual(originalEvents.Events))
            throw new InvalidDataException("Original accepted events do not reconstruct from the binding.");
        foreach (var field in new[] { source.IdentityFingerprint, source.HistoryFingerprint, source.EffectFingerprint,
            source.ItemResourceFingerprint, source.ActorLocationFingerprint, source.PlayerCapabilityCatalogFingerprint,
            source.NpcCapabilityCatalogFingerprint, source.RequirementSnapshotFingerprint })
        {
            if (!IsFingerprint(field))
                throw new InvalidDataException("Malformed retained original accepted component seal.");
        }
    }

    /// <summary>
    /// Checks epoch identity and consumption against the original ordered resolution.
    /// </summary>
    /// <param name="wound">
    /// The complete source wound, including both canonical epochs.
    /// </param>
    /// <param name="recovery">
    /// The required recovery epoch consumption.
    /// </param>
    /// <param name="deterioration">
    /// The condition epoch consumption, or null when the source has no condition epoch.
    /// </param>
    /// <param name="resolution">
    /// The original resolution whose applied intervals must match consumption.
    /// </param>
    private static void ValidateConsumption(WoundMaterializationEnvelope wound,
        MortalWoundRecoveryEpochConsumption recovery, MortalWoundRecoveryEpochConsumption? deterioration,
        MortalWoundRecoveryResolution resolution)
    {
        var anchor = wound.Recovery.RecoveryAnchor;
        if (anchor is null || recovery.EpochKind != anchor.AnchorKind || recovery.ConditionKey is not null ||
            recovery.AnchorMinute != anchor.AnchorMinute || recovery.AnchorTransitionId != anchor.AnchorTransitionId ||
            recovery.ConsumedBefore < 0 || recovery.ConsumedAfter < recovery.ConsumedBefore)
            throw new InvalidDataException("Recovery consumption does not match the exact source epoch.");
        var progressApplied = resolution.TransitionIntents.OfType<MortalWoundRecoveryProgressIntent>().Any();
        if (recovery.ConsumedAfter != checked(recovery.ConsumedBefore + (progressApplied ? resolution.ElapsedCadences : 0)))
            throw new InvalidDataException("Recovery consumption does not match newly applied intervals.");
        var condition = wound.Recovery.DeteriorationAnchor;
        if ((condition is null) != (deterioration is null))
            throw new InvalidDataException("Condition consumption must match the presence of the canonical condition epoch.");
        if (condition is not null && deterioration is not null &&
            (deterioration.EpochKind != "condition" || deterioration.ConditionKey != condition.ConditionKey ||
             deterioration.AnchorMinute != condition.AnchorMinute || deterioration.AnchorTransitionId != condition.AnchorTransitionId ||
             deterioration.ConsumedBefore < 0 || deterioration.ConsumedAfter !=
                checked(deterioration.ConsumedBefore + resolution.ElapsedDeteriorationCadences)))
            throw new InvalidDataException("Deterioration consumption does not match the exact source epoch and elapsed intervals.");
    }

    /// <summary>
    /// Checks complete stage snapshots, exact coordinates and the uninterrupted original epoch chain.
    /// </summary>
    /// <param name="source">
    /// The original accepted source evidence.
    /// </param>
    /// <param name="binding">
    /// The original binding used by every stage.
    /// </param>
    /// <param name="wound">
    /// The full source wound before the primary stage.
    /// </param>
    /// <param name="resolution">
    /// The reconstructed original resolution.
    /// </param>
    /// <param name="stages">
    /// The complete ordered executed stage evidence.
    /// </param>
    private static void ValidateStages(MortalWoundRecoverySourceEvidence source, WoundAcceptedTurnBinding binding,
        WoundMaterializationEnvelope wound, MortalWoundRecoveryResolution resolution,
        IReadOnlyList<MortalWoundRecoveryPublishedStage> stages)
    {
        if (stages.Count is < 1 or > 6 || stages[0].Kind != "recover")
            throw new InvalidDataException("Expected a bounded recovery stage chain beginning with recover.");
        var previous = source.WoundFingerprint;
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var operations = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < stages.Count; index++)
        {
            var stage = stages[index];
            var before = stage.BeforeWound;
            var after = stage.AfterWound;
            var finalRecoverIndex = stages[^1].Terminal ? stages.Count - 2 : stages.Count - 1;
            var expectedTick = EffectAcceptedTurnPlanner.GetMortalRecoveryStageTick(
                resolution.TickKey, index, finalRecoverIndex, stage.Terminal);
            if (stage.Kind is not ("recover" or "heal") ||
                stage.Turn != binding.Turn || !ResourceMaterializationContract.IsExactIdentifier(stage.TransitionId) ||
                !ResourceMaterializationContract.IsExactIdentifier(stage.OperationKey) ||
                !binding.AcceptedEvents.Any(value => value.EventRef == stage.EventRef) ||
                !identities.Add(stage.TransitionId) || !operations.Add(stage.OperationKey) ||
                before.WoundId != wound.WoundId || after.WoundId != wound.WoundId ||
                before.Owner != wound.Owner || after.Owner != wound.Owner ||
                stage.BeforeFingerprint != previous || stage.BeforeFingerprint != WoundIdentityState.ComputeSemanticFingerprint(before) ||
                stage.AfterFingerprint != WoundIdentityState.ComputeSemanticFingerprint(after) ||
                after.LastTransition.TransitionId != stage.TransitionId || after.LastTransition.Kind != stage.Kind ||
                after.LastTransition.Turn != stage.Turn || after.LastTransition.Ordinal != checked(wound.LastTransition.Ordinal + index + 1) ||
                after.Recovery.LastTickKey != expectedTick ||
                before.Recovery.RecoveryAnchor != wound.Recovery.RecoveryAnchor ||
                after.Recovery.RecoveryAnchor != wound.Recovery.RecoveryAnchor ||
                before.Recovery.DeteriorationAnchor != wound.Recovery.DeteriorationAnchor ||
                after.Recovery.DeteriorationAnchor != wound.Recovery.DeteriorationAnchor ||
                stage.Terminal != (stage.Kind == "heal") ||
                (stage.Terminal && (index != stages.Count - 1 || after.Lifecycle != "healed")) ||
                (!stage.Terminal && after.Lifecycle != "active"))
                throw new InvalidDataException("Recovery stage evidence disagrees with its source, chain, epoch or allocated coordinates.");
            previous = stage.AfterFingerprint;
        }
        ValidateStageMechanics(wound, resolution, stages);
    }

    /// <summary>
    /// Reconstructs exact natural and adverse scalar boundaries and verifies their preserved source facts.
    /// </summary>
    /// <param name="wound">
    /// The complete original source wound.
    /// </param>
    /// <param name="resolution">
    /// The reconstructed ordered original evaluation.
    /// </param>
    /// <param name="stages">
    /// The complete actually executed recover and optional heal stages.
    /// </param>
    private static void ValidateStageMechanics(WoundMaterializationEnvelope wound,
        MortalWoundRecoveryResolution resolution, IReadOnlyList<MortalWoundRecoveryPublishedStage> stages)
    {
        var expected = new List<MortalWoundRecoveryProgressStage>();
        var progress = resolution.TransitionIntents.OfType<MortalWoundRecoveryProgressIntent>().SingleOrDefault();
        if (progress is not null)
            expected.AddRange(MortalWoundRecoveryProgressProgram.Create(wound.Severity.Rank,
                wound.Recovery.CurrentStepProgress, wound.Recovery.CurrentStepThreshold,
                progress.ElapsedCadences, wound.Recovery.CarryOverflow));
        var adverse = resolution.TransitionIntents.OfType<MortalWoundRecoveryDeteriorationIntent>().SingleOrDefault();
        MortalWoundDeteriorationPolicyDefinition? policy = null;
        MortalWoundComplicationProposalDraft? complication = null;
        if (adverse is not null)
        {
            if (expected.Any(stage => stage.FullHeal) || wound.Recovery.DeteriorationPolicy is not { } policyElement)
                throw new InvalidDataException("An ordered adverse result cannot follow terminal natural recovery.");
            policy = MortalWoundDeteriorationPolicyContract.Parse(policyElement, "recovery.deteriorationPolicy",
                wound.Owner.Realm, WoundMaterializationContract.ResolveEffectTargetKind(wound.Owner.OwnerKind),
                wound.Severity.Rank).Policy ?? throw new InvalidDataException("Missing applicable adverse policy.");
            var rank = expected.Count == 0 ? wound.Severity.Rank : expected[^1].SeverityRank;
            var currentProgress = expected.Count == 0 ? wound.Recovery.CurrentStepProgress : expected[^1].Progress;
            if (policy.ResultKind == MortalWoundDeteriorationResultKind.IncreaseSeverity)
            {
                rank = checked(rank + 1);
                currentProgress = 0;
                if (rank > 4)
                    throw new InvalidDataException("Adverse severity increase exceeds the applicable wound boundary.");
            }
            else if (policy.ResultKind == MortalWoundDeteriorationResultKind.AddComplication)
                complication = MortalWoundTreatmentContract.BuildValidatedComplicationDraft(policy.Result.GetProperty("complicationDraft"));
            else
                throw new InvalidDataException("Non-adverse or death policy cannot produce an adverse mechanical stage.");
            expected.Add(new(rank, currentProgress, false));
        }
        if (expected.Count == 0)
            expected.Add(new(wound.Severity.Rank, wound.Recovery.CurrentStepProgress, false));
        if (stages.Count != expected.Count)
            throw new InvalidDataException("Executed stage count differs from the reconstructed original program.");
        for (var index = 0; index < expected.Count; index++)
        {
            var stage = stages[index];
            var scalar = expected[index];
            var before = stage.BeforeWound;
            var after = stage.AfterWound;
            if (stage.Kind != (scalar.FullHeal ? "heal" : "recover") ||
                after.Severity.Rank != scalar.SeverityRank || after.Recovery.CurrentStepProgress != scalar.Progress ||
                after.Severity.MaximumAtCreation != before.Severity.MaximumAtCreation ||
                after.Severity.LastChangeEventRef != (before.Severity.Rank == after.Severity.Rank
                    ? before.Severity.LastChangeEventRef : stage.EventRef))
                throw new InvalidDataException("Executed rank, progress or terminal boundary differs from the reconstructed original program.");
            var expectedCare = scalar.FullHeal ? before.Care with { State = "healed", ActiveCourseId = null } : before.Care;
            if (after.Care != expectedCare)
                throw new InvalidDataException("Recovery changed care outside the explicit full-heal boundary.");
            var normalized = after with
            {
                Lifecycle = before.Lifecycle, Severity = before.Severity, Care = before.Care,
                Complications = before.Complications, Consequences = before.Consequences,
                Recovery = after.Recovery with
                {
                    CurrentStepProgress = before.Recovery.CurrentStepProgress,
                    LastTickKey = before.Recovery.LastTickKey
                },
                LastTransition = before.LastTransition
            };
            if (WoundMaterializationContract.SerializeCanonical(before) != WoundMaterializationContract.SerializeCanonical(normalized))
                throw new InvalidDataException("Recovery changed unrelated source data, policy, blockers or epoch coordinates.");
            var addition = adverse is not null && index == expected.Count - 1 ? complication : null;
            ValidateOwnedGraph(before, after, addition);
        }
    }

    /// <summary>
    /// Verifies unchanged or rematerialized graph facts and the exact declared complication append.
    /// </summary>
    /// <param name="before">
    /// The complete canonical stage source.
    /// </param>
    /// <param name="after">
    /// The complete canonical stage result.
    /// </param>
    /// <param name="addition">
    /// The declared adverse complication draft, or null when no graph append is allowed.
    /// </param>
    private static void ValidateOwnedGraph(WoundMaterializationEnvelope before, WoundMaterializationEnvelope after,
        MortalWoundComplicationProposalDraft? addition)
    {
        var prior = before.Consequences.OwnedEffectSources;
        var current = after.Consequences.OwnedEffectSources;
        var fragment = addition is null ? null : WoundResponseInputComposer.ConvertTreatmentComplicationGraph(
            addition, before.WoundId, WoundWorkingReferenceOrigin.PolicyAddition, new(0, 0));
        var expectedDefinitions = prior.Definitions.Concat(fragment?.Definitions.Select(value => value.Definition) ??
            Enumerable.Empty<JsonElement>()).OrderBy(value => value.GetProperty("definitionKey").GetString(), StringComparer.Ordinal).ToArray();
        var expectedBudget = after.Severity.Rank < before.Severity.Rank ? after.Severity.Rank : before.Consequences.SlotBudget;
        if (expectedBudget != after.Consequences.SlotBudget ||
            !SameJson(JsonSerializer.SerializeToNode(expectedDefinitions)!, JsonSerializer.SerializeToNode(current.Definitions)!))
            throw new InvalidDataException("Recovery changed owned definition semantics or slot budget.");
        var roots = prior.RootBindings.Select(value => value.DefinitionKey).Concat(
            fragment?.Roots.Select(value => value.DefinitionKey) ?? Enumerable.Empty<string>()).ToArray();
        if (!roots.OrderBy(value => value, StringComparer.Ordinal).SequenceEqual(
                current.RootBindings.Select(value => value.DefinitionKey).OrderBy(value => value, StringComparer.Ordinal)))
            throw new InvalidDataException("Recovery root topology differs from the retained or declared graph.");
        var rootMap = current.RootBindings.ToDictionary(value => value.DefinitionKey, value => value.EffectId, StringComparer.Ordinal);
        var oldMap = prior.RootBindings.ToDictionary(value => value.EffectId, value => value.DefinitionKey, StringComparer.Ordinal);
        var rankChanged = before.Severity.Rank != after.Severity.Rank;
        if (rankChanged)
        {
            var oldRoots = prior.RootBindings.Select(value => MortalLocationIdentityState.BuildConfusableKey(value.EffectId))
                .ToHashSet(StringComparer.Ordinal);
            if (current.RootBindings.Any(value => oldRoots.Contains(MortalLocationIdentityState.BuildConfusableKey(value.EffectId))))
                throw new InvalidDataException("Severity rematerialization reused a prior exact or confusable root identity.");
        }
        else if (prior.RootBindings.Any(value => !current.RootBindings.Contains(value)))
            throw new InvalidDataException("Same-rank recovery changed existing root identities.");
        var expectedComplications = before.Complications.Select(value => value with
        {
            OwnedEffectIds = value.OwnedEffectIds.Select(id => rootMap[oldMap[id]]).ToImmutableArray()
        }).ToList();
        var entries = before.Consequences.Entries.Select(value => value with { EffectId = rootMap[oldMap[value.EffectId]] }).ToList();
        if (fragment is not null)
        {
            if (after.Complications.Count != before.Complications.Count + 1)
                throw new InvalidDataException("Adverse recovery must append exactly one declared complication.");
            var declared = fragment.Complications[0];
            var addedId = after.Complications[^1].ComplicationId;
            var definitionsByRef = fragment.Roots.ToDictionary(value => value.Reference, value => value.DefinitionKey);
            expectedComplications.Add(new(addedId, declared.Kind, declared.State, declared.DisplayName,
                declared.TreatmentDifficultyModifier, declared.OwnedRoots.Select(value => rootMap[definitionsByRef[value]]).ToImmutableArray(),
                declared.Visibility));
            entries.AddRange(fragment.Entries.Select(value => new WoundConsequenceEntry(
                checked(value.Slot + before.Consequences.Entries.Count), value.ProfileKey,
                rootMap[definitionsByRef[value.Root]], value.ReadableSummary)));
        }
        if (after.Lifecycle != "healed")
            entries = entries.OrderBy(value => value.EffectId, StringComparer.Ordinal).ThenBy(value => value.Slot)
                .Select((value, index) => value with { Slot = index + 1 }).ToList();
        if (!SameJson(JsonSerializer.SerializeToNode(expectedComplications, Options)!, JsonSerializer.SerializeToNode(after.Complications, Options)!) ||
            !entries.SequenceEqual(after.Consequences.Entries) || after.Consequences.SlotsUsed != entries.Count)
            throw new InvalidDataException("Recovery changed retained complication facts or declared consequence slots.");
    }

    /// <summary>
    /// Reconstructs the original four-field recovery receipt.
    /// </summary>
    /// <param name="resolution">
    /// The reconstructed original resolution.
    /// </param>
    /// <param name="woundId">
    /// The exact wound identity selected by the original evaluation.
    /// </param>
    /// <returns>
    /// The deterministic original receipt with its recomputed receipt fingerprint.
    /// </returns>
    private static MortalWoundRecoveryReceipt CreateReceipt(MortalWoundRecoveryResolution resolution, string woundId) => new(
        resolution.AuthorityFingerprint, Hash("book_of_eternity.mortal_wound.recovery_receipt",
            resolution.AuthorityFingerprint, resolution.TickKey, woundId), resolution.TickKey, woundId);

    /// <summary>
    /// Serializes the closed resolution with explicit ordered typed intent discriminators.
    /// </summary>
    /// <param name="resolution">
    /// The original detached resolution, including zero to two ordered intents.
    /// </param>
    /// <returns>
    /// A detached canonical projection retaining every resolution and derived intent field.
    /// </returns>
    internal static JsonObject ResolutionJson(MortalWoundRecoveryResolution resolution)
    {
        var value = JsonSerializer.SerializeToNode(resolution, Options)!.AsObject();
        value["transitionIntents"] = new JsonArray(resolution.TransitionIntents.Select(intent =>
        {
            var node = JsonSerializer.SerializeToNode(intent, intent.GetType(), Options)!.AsObject();
            node["kind"] = intent switch
            {
                MortalWoundRecoveryProgressIntent => "progress",
                MortalWoundRecoveryDeteriorationIntent => "deterioration",
                MortalWoundDeathHandoffIntent => "death_handoff",
                _ => throw new InvalidDataException("Unknown recovery intent.")
            };
            return (JsonNode?)node;
        }).ToArray());
        return value;
    }

    /// <summary>
    /// Parses every closed scalar resolution field and its explicitly discriminated ordered intents.
    /// </summary>
    /// <param name="value">
    /// The complete resolution object.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the resolution.
    /// </param>
    /// <param name="issues">
    /// The collection receiving closed-shape failures.
    /// </param>
    /// <returns>
    /// The detached resolution; structural failures are reported or rejected by the enclosing result parser.
    /// </returns>
    private static MortalWoundRecoveryResolution? ParseResolution(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        Closed(value, path, issues, "authorityFingerprint", "cadenceDueMinute", "clockKind", "clockSourcePath",
            "currentTimeInMinutes", "deathHandoff", "deteriorationGraceDeadlineMinute", "deteriorationAnchorMinute",
            "deteriorationPolicyRef", "elapsedCadences", "elapsedDeteriorationCadences", "mode", "nextRecoveryAnchorMinute",
            "nextDeteriorationAnchorMinute", "recoveryAnchorMinute", "recoveryDisposition", "tickKey", "transitionIntents");
        var handoff = value.GetProperty("deathHandoff");
        if (handoff.ValueKind != JsonValueKind.Null)
            Closed(handoff, path + ".deathHandoff", issues, "authorityFingerprint", "policyRef", "tickKey", "woundId");
        var intents = new List<IMortalWoundRecoveryTransitionIntent>();
        var index = 0;
        foreach (var intent in value.GetProperty("transitionIntents").EnumerateArray())
        {
            if (index >= 2)
                throw new InvalidDataException("Recovery has more than two ordered intents.");
            var intentPath = path + $".transitionIntents[{index++}]";
            var kind = intent.GetProperty("kind").GetString();
            var node = JsonNode.Parse(intent.GetRawText())!.AsObject();
            node.Remove("kind");
            if (kind == "progress")
            {
                Closed(intent, intentPath, issues, "kind", "authorityFingerprint", "elapsedCadences", "nextRecoveryAnchorMinute",
                    "recoveryAnchorMinute", "tickKey", "woundId");
                intents.Add(node.Deserialize<MortalWoundRecoveryProgressIntent>(Options)!);
            }
            else if (kind == "deterioration")
            {
                Closed(intent, intentPath, issues, "kind", "authorityFingerprint", "elapsedCadences", "nextDeteriorationAnchorMinute",
                    "policyRef", "tickKey", "woundId");
                intents.Add(node.Deserialize<MortalWoundRecoveryDeteriorationIntent>(Options)!);
            }
            else if (kind == "death_handoff")
            {
                Closed(intent, intentPath, issues, "kind", "authorityFingerprint", "policyRef", "tickKey", "woundId");
                intents.Add(node.Deserialize<MortalWoundDeathHandoffIntent>(Options)!);
            }
            else throw new InvalidDataException("Unknown recovery intent kind.");
        }
        var scalar = JsonNode.Parse(value.GetRawText())!.AsObject();
        scalar["transitionIntents"] = new JsonArray();
        var parsed = scalar.Deserialize<MortalWoundRecoveryResolution>(Options)!;
        var disposition = value.GetProperty("recoveryDisposition");
        if (disposition.ValueKind != JsonValueKind.String || disposition.GetString() != parsed.RecoveryDisposition.ToString() ||
            !Enum.IsDefined(parsed.RecoveryDisposition))
            throw new InvalidDataException("Recovery disposition must be one exact registered enum-name string.");
        return parsed with { TransitionIntents = intents.ToImmutableArray() };
    }

    /// <summary>
    /// Parses the closed original accepted binding and its complete event vector.
    /// </summary>
    /// <param name="value">
    /// The complete binding object.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the binding.
    /// </param>
    /// <param name="issues">
    /// The collection receiving closed-shape failures.
    /// </param>
    /// <returns>
    /// The detached binding whose event and binding seals are checked by source validation.
    /// </returns>
    private static WoundAcceptedTurnBinding ParseBinding(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        Closed(value, path, issues, "sessionId", "requestId", "snapshotToken", "realm", "turn", "acceptedEvents", "acceptedEventsFingerprint");
        var events = new List<WoundAcceptedEventAuthority>();
        var index = 0;
        foreach (var item in value.GetProperty("acceptedEvents").EnumerateArray())
        {
            if (index >= 160)
                throw new InvalidDataException("Too many accepted recovery events.");
            Closed(item, path + $".acceptedEvents[{index++}]", issues, "eventRef", "kind", "authorityId", "semanticFingerprint");
            events.Add(item.Deserialize<WoundAcceptedEventAuthority>(Options)!);
        }
        return new(value.GetProperty("sessionId").GetString()!, value.GetProperty("requestId").GetString()!,
            value.GetProperty("snapshotToken").GetString()!, value.GetProperty("realm").GetString()!,
            value.GetProperty("turn").GetInt32(), events, value.GetProperty("acceptedEventsFingerprint").GetString()!);
    }

    /// <summary>
    /// Serializes every original binding coordinate and accepted event.
    /// </summary>
    /// <param name="binding">
    /// The original accepted binding to serialize.
    /// </param>
    /// <returns>
    /// A detached closed binding projection.
    /// </returns>
    private static JsonObject BindingJson(WoundAcceptedTurnBinding binding) => new()
    {
        ["sessionId"] = binding.SessionId, ["requestId"] = binding.RequestId, ["snapshotToken"] = binding.SnapshotToken,
        ["realm"] = binding.Realm, ["turn"] = binding.Turn,
        ["acceptedEvents"] = JsonSerializer.SerializeToNode(binding.AcceptedEvents, Options),
        ["acceptedEventsFingerprint"] = binding.AcceptedEventsFingerprint
    };

    /// <summary>
    /// Parses one closed exact-epoch consumption record.
    /// </summary>
    /// <param name="value">
    /// The required consumption object.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of this epoch.
    /// </param>
    /// <param name="issues">
    /// The collection receiving closed-shape failures.
    /// </param>
    /// <returns>
    /// The parsed consumption record, or null when deserialization produces null.
    /// </returns>
    private static MortalWoundRecoveryEpochConsumption? ParseConsumption(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        Closed(value, path, issues, "epochKind", "conditionKey", "anchorMinute", "anchorTransitionId", "consumedBefore", "consumedAfter");
        return value.Deserialize<MortalWoundRecoveryEpochConsumption>(Options);
    }

    /// <summary>
    /// Parses the bounded complete stage chain using strict canonical wound parsers.
    /// </summary>
    /// <param name="value">
    /// The ordered stage array, bounded to six executed stages.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the stage array.
    /// </param>
    /// <param name="issues">
    /// The collection receiving closed-shape and canonical wound failures.
    /// </param>
    /// <returns>
    /// An immutable list of complete stage evidence; failures remain in the enclosing parser issue collection.
    /// </returns>
    private static IReadOnlyList<MortalWoundRecoveryPublishedStage> ParseStages(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind != JsonValueKind.Array || value.GetArrayLength() is < 1 or > 6)
            throw new InvalidDataException("Expected one to six bounded executed stages.");
        var stages = new List<MortalWoundRecoveryPublishedStage>();
        var index = 0;
        foreach (var item in value.EnumerateArray())
        {
            var stagePath = path + $"[{index++}]";
            Closed(item, stagePath, issues, "transitionId", "kind", "beforeFingerprint", "afterFingerprint", "terminal",
                "turn", "eventRef", "operationKey", "beforeWound", "afterWound");
            var before = ParseWound(item.GetProperty("beforeWound"), stagePath + ".beforeWound", issues);
            var after = ParseWound(item.GetProperty("afterWound"), stagePath + ".afterWound", issues);
            if (before is not null && after is not null)
                stages.Add(new(item.GetProperty("transitionId").GetString()!, item.GetProperty("kind").GetString()!,
                    item.GetProperty("beforeFingerprint").GetString()!, item.GetProperty("afterFingerprint").GetString()!,
                    item.GetProperty("terminal").GetBoolean(), item.GetProperty("turn").GetInt32(),
                    item.GetProperty("eventRef").GetString()!, item.GetProperty("operationKey").GetString()!, before, after));
        }
        return stages.ToImmutableArray();
    }

    /// <summary>
    /// Reads a complete canonical wound using the existing strict materialization contract.
    /// </summary>
    /// <param name="value">
    /// The complete canonical wound JSON value.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of this wound snapshot.
    /// </param>
    /// <param name="issues">
    /// The collection receiving every canonical wound parsing issue.
    /// </param>
    /// <returns>
    /// The parsed wound, or null when the canonical contract rejects it.
    /// </returns>
    private static WoundMaterializationEnvelope? ParseWound(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        var parsed = WoundMaterializationContract.Parse(value.GetRawText(), path);
        foreach (var issue in parsed.Issues) issues.Add(issue);
        return parsed.Wound;
    }

    /// <summary>
    /// Serializes exact stage coordinates and complete canonical before and after wounds.
    /// </summary>
    /// <param name="stage">
    /// The actually executed stage evidence to serialize.
    /// </param>
    /// <returns>
    /// A detached closed stage projection.
    /// </returns>
    private static JsonObject StageJson(MortalWoundRecoveryPublishedStage stage) => new()
    {
        ["transitionId"] = stage.TransitionId, ["kind"] = stage.Kind,
        ["beforeFingerprint"] = stage.BeforeFingerprint, ["afterFingerprint"] = stage.AfterFingerprint,
        ["terminal"] = stage.Terminal, ["turn"] = stage.Turn, ["eventRef"] = stage.EventRef,
        ["operationKey"] = stage.OperationKey, ["beforeWound"] = WoundJson(stage.BeforeWound),
        ["afterWound"] = WoundJson(stage.AfterWound)
    };

    /// <summary>
    /// Serializes a wound with the canonical wound contract.
    /// </summary>
    /// <param name="wound">
    /// The complete canonical wound snapshot.
    /// </param>
    /// <returns>
    /// A detached canonical wound object.
    /// </returns>
    private static JsonObject WoundJson(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!.AsObject();
    /// <summary>
    /// Compares complete JSON values using canonical property ordering.
    /// </summary>
    /// <param name="first">
    /// The first complete JSON value.
    /// </param>
    /// <param name="second">
    /// The second complete JSON value.
    /// </param>
    /// <returns>
    /// True when both canonical projections agree; otherwise false.
    /// </returns>
    private static bool SameJson(JsonNode first, JsonNode second) =>
        WoundAcceptedTurnFingerprintWriter.CanonicalJson(first) == WoundAcceptedTurnFingerprintWriter.CanonicalJson(second);
    /// <summary>
    /// Checks the exact lowercase SHA-256 representation of a retained component seal.
    /// </summary>
    /// <param name="value">
    /// The component seal to inspect.
    /// </param>
    /// <returns>
    /// True for the exact sha256 prefix and sixty-four lowercase hexadecimal digits; otherwise false.
    /// </returns>
    private static bool IsFingerprint(string value) => value is { Length: 71 } && value.StartsWith("sha256:", StringComparison.Ordinal) &&
        value.AsSpan(7).IndexOfAnyExcept("0123456789abcdef") < 0;
    /// <summary>
    /// Formats an exact ordinal or minute without culture-dependent formatting.
    /// </summary>
    /// <param name="value">
    /// The integer coordinate to format.
    /// </param>
    /// <returns>
    /// The invariant decimal representation.
    /// </returns>
    private static string Number(long value) => value.ToString(CultureInfo.InvariantCulture);
    /// <summary>
    /// Computes a version-one domain-separated evidence fingerprint.
    /// </summary>
    /// <param name="domain">
    /// The exact fingerprint domain.
    /// </param>
    /// <param name="values">
    /// The ordered nullable fingerprint fields; null remains distinct from an empty value.
    /// </param>
    /// <returns>
    /// The lowercase SHA-256 fingerprint produced by the shared canonical field writer.
    /// </returns>
    private static string Hash(string domain, params string?[] values) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new[] { domain, "1" }.Concat(values));

    /// <summary>
    /// Checks a required object against its complete exact field set.
    /// </summary>
    /// <param name="value">
    /// The JSON value that must be an object.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the object.
    /// </param>
    /// <param name="issues">
    /// The collection receiving missing or unknown field failures.
    /// </param>
    /// <param name="fields">
    /// The complete set of required exact property names.
    /// </param>
    private static void Closed(JsonElement value, string path, ICollection<ValidationIssue> issues, params string[] fields)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            AddIssue(issues, path, "mortal_wound_recovery_result_invalid", "Expected a closed object.");
            return;
        }
        var expected = fields.ToHashSet(StringComparer.Ordinal);
        foreach (var property in value.EnumerateObject())
            if (!expected.Contains(property.Name))
                AddIssue(issues, path + "." + property.Name, "mortal_wound_recovery_unknown_field", "Unknown closed recovery field.");
        foreach (var field in fields)
            if (!value.TryGetProperty(field, out _))
                AddIssue(issues, path + "." + field, "mortal_wound_recovery_missing_field", "Missing required closed recovery field.");
    }

    /// <summary>
    /// Rejects duplicate JSON properties recursively before any object materialization.
    /// </summary>
    /// <param name="value">
    /// The original JSON value whose property occurrences are preserved.
    /// </param>
    /// <param name="path">
    /// The diagnostic path of the value.
    /// </param>
    /// <param name="issues">
    /// The collection receiving every duplicate-property failure.
    /// </param>
    private static void ValidateDuplicates(JsonElement value, string path, ICollection<ValidationIssue> issues)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                var childPath = path + "." + property.Name;
                if (!names.Add(property.Name))
                    AddIssue(issues, childPath, "mortal_wound_recovery_duplicate_property", "Duplicate JSON property.");
                ValidateDuplicates(property.Value, childPath, issues);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var item in value.EnumerateArray()) ValidateDuplicates(item, path + $"[{index++}]", issues);
        }
    }

    /// <summary>
    /// Adds a recovery evidence failure without granting any replay or publication authority.
    /// </summary>
    /// <param name="issues">
    /// The collection receiving the issue.
    /// </param>
    /// <param name="path">
    /// The exact diagnostic path.
    /// </param>
    /// <param name="code">
    /// The stable failure classification.
    /// </param>
    /// <param name="actual">
    /// The rejected value or reason.
    /// </param>
    private static void AddIssue(ICollection<ValidationIssue> issues, string path, string code, string actual) =>
        issues.Add(new ValidationIssue(path, IssueSeverity.Error,
            "The persisted Mortal recovery result cannot authorize replay.", code: code, actor: "Client",
            section: "wound_materialization", expected: "complete closed original recovery evidence", actual: actual,
            repairHint: "Restore accepted history; never hand-write a receipt, tick, anchor or result fingerprint."));
}
