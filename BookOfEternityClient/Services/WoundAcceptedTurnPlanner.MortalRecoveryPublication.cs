using System.Collections.ObjectModel;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    /// <summary>
    /// Supplies the stable client-authored summary used to seal recovery history rows.
    /// </summary>
    internal const string RecoveryPublicationSummary =
        "The accepted Mortal recovery evaluation is retained for replay.";

    /// <summary>
    /// Reads complete detached publication baselines under the caller's live lease.
    /// </summary>
    /// <param name="fileSystem">
    /// The owning canonical filesystem.
    /// </param>
    /// <param name="writeLease">
    /// Its active canonical write lease.
    /// </param>
    /// <param name="binding">
    /// The exact accepted event binding for this evaluation.
    /// </param>
    /// <returns>
    /// Empty-draft wound input and source roots, or the canonical read diagnostics.
    /// </returns>
    internal static (WoundAcceptedTurnInput? Input,
        IReadOnlyDictionary<string, JsonNode?>? SourceRoots,
        IReadOnlyList<ValidationIssue> Issues) ReadRecoveryPublicationBaselines(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        WoundAcceptedTurnBinding binding)
    {
        var baselines = ReadPublicationBaselines(fileSystem, writeLease);
        if (baselines.Issues.Count != 0)
            return (null, null, baselines.Issues);
        return (new WoundAcceptedTurnInput(binding,
            Array.Empty<WoundOpportunityAuthority>(), Array.Empty<WoundAcceptedTransitionDraft>(),
            baselines.WoundCarriers!, baselines.WoundIdentity!, baselines.WoundHistory!,
            baselines.EffectCarriers, baselines.EffectIdentity),
            baselines.SourceRoots!, Array.Empty<ValidationIssue>());
    }

    /// <summary>
    /// Retains the registry-admitted source, complete baselines and bounded program seal.
    /// </summary>
    private sealed class RecoveryContinuationAuthority
    {
        private readonly WoundAcceptedTurnInput _input;
        private readonly MortalWoundRecoveryResolution _resolution;
        private readonly string[] _transitionIds;
        private readonly string[] _operationKeys;
        private readonly IReadOnlyDictionary<string, JsonNode?> _sourceRoots;

        /// <summary>
        /// Freezes one independently admitted recovery program and allocates its coordinates.
        /// </summary>
        /// <param name="acceptedState">
        /// The exact live registered source authority.
        /// </param>
        /// <param name="resolution">
        /// The registry's complete recomputed result.
        /// </param>
        /// <param name="input">
        /// The complete detached pre-turn wound and effect baselines.
        /// </param>
        /// <param name="sourceRoots">
        /// Canonical effect-source roots read under the same live lease.
        /// </param>
        internal RecoveryContinuationAuthority(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundRecoveryResolution resolution,
            WoundAcceptedTurnInput input,
            IReadOnlyDictionary<string, JsonNode?> sourceRoots)
        {
            AcceptedStateAuthority = acceptedState;
            _resolution = CloneRecoveryResolution(resolution);
            _input = WoundAcceptedTurnData.CloneInput(input)!;
            _sourceRoots = CloneRecoverySourceRoots(sourceRoots);
            EventRef = input.Binding.AcceptedEvents.First().EventRef;
            var count = RecoveryStageCount(acceptedState.CurrentWound, resolution);
            _transitionIds = Enumerable.Range(1, count).Select(ordinal =>
                RecoveryCoordinate("wound_recovery_transition", resolution.TickKey, ordinal)).ToArray();
            _operationKeys = Enumerable.Range(1, count).Select(ordinal =>
                RecoveryCoordinate("wound_recovery_operation", resolution.TickKey, ordinal)).ToArray();
            Fingerprint = ComputeFingerprint();
        }

        /// <summary>
        /// Gets the exact registry-owned source admitted before this continuation.
        /// </summary>
        internal MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority { get; }

        /// <summary>
        /// Gets the accepted event reference shared by the recovery boundaries.
        /// </summary>
        internal string EventRef { get; }

        /// <summary>
        /// Gets the seal over the original inputs and deterministic stage coordinates.
        /// </summary>
        internal string Fingerprint { get; }

        /// <summary>
        /// Exports detached comparison data while retaining the actual accepted source capability.
        /// </summary>
        /// <returns>
        /// A fresh view whose mutable JSON and collections do not expose retained state.
        /// </returns>
        internal RecoveryContinuationView Read() => new(
            WoundAcceptedTurnData.CloneWound(AcceptedStateAuthority.CurrentWound)!,
            AcceptedStateAuthority,
            CloneRecoveryResolution(_resolution),
            WoundAcceptedTurnData.CloneInput(_input)!,
            Array.AsReadOnly(_transitionIds.ToArray()),
            EventRef,
            Array.AsReadOnly(_operationKeys.ToArray()),
            CloneRecoverySourceRoots(_sourceRoots),
            Fingerprint);

        /// <summary>
        /// Gets whether the retained inputs still agree with their original seal.
        /// </summary>
        internal bool IsSealed => string.Equals(Fingerprint, ComputeFingerprint(), StringComparison.Ordinal);

        /// <summary>
        /// Seals the complete source, ordered program, coordinates and canonical baselines.
        /// </summary>
        /// <returns>
        /// The deterministic private preparation fingerprint.
        /// </returns>
        private string ComputeFingerprint()
        {
            var fields = new List<string?>
            {
                "book_of_eternity.mortal_wound.recovery_continuation", "1",
                AcceptedStateAuthority.AcceptedStateFingerprint,
                WoundMaterializationContract.SerializeCanonical(AcceptedStateAuthority.CurrentWound),
                WoundAcceptedTurnFingerprints.ComputeInput(_input),
                RecoveryResolutionProjection(_resolution).ToJsonString(),
                EventRef,
                _transitionIds.Length.ToString(CultureInfo.InvariantCulture)
            };
            for (var index = 0; index < _transitionIds.Length; index++)
            {
                fields.Add(_transitionIds[index]);
                fields.Add(_operationKeys[index]);
            }
            foreach (var pair in _sourceRoots.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                fields.Add(pair.Key);
                fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(pair.Value));
            }
            return WoundAcceptedTurnFingerprintWriter.Compute(fields);
        }
    }

    /// <summary>
    /// Exposes detached recovery inputs and coordinates from the private continuation.
    /// </summary>
    /// <param name="Before">
    /// The original selected wound before any recovery stage.
    /// </param>
    /// <param name="AcceptedStateAuthority">
    /// The registered authority that admitted the signed source.
    /// </param>
    /// <param name="Resolution">
    /// The complete independently recomputed recovery result.
    /// </param>
    /// <param name="Input">
    /// The original wound and effect baselines with no GM transition drafts.
    /// </param>
    /// <param name="AllocatedTransitionIds">
    /// The ordered transition identifiers allocated for this publication.
    /// </param>
    /// <param name="EventRef">
    /// The accepted event reference used by all recovery stages.
    /// </param>
    /// <param name="OperationKeys">
    /// The deterministic operation keys in stage order.
    /// </param>
    /// <param name="SourceRoots">
    /// Detached canonical roots used to authenticate effect sources.
    /// </param>
    /// <param name="Fingerprint">
    /// The seal over the complete continuation inputs and stage coordinates.
    /// </param>
    internal sealed record RecoveryContinuationView(
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority,
        MortalWoundRecoveryResolution Resolution,
        WoundAcceptedTurnInput Input,
        IReadOnlyList<string> AllocatedTransitionIds,
        string EventRef,
        IReadOnlyList<string> OperationKeys,
        IReadOnlyDictionary<string, JsonNode?> SourceRoots,
        string Fingerprint);

    /// <summary>
    /// Mints a private continuation after the registry has independently admitted
    /// the current source and recomputed the complete recovery resolution.
    /// </summary>
    /// <param name="capability">
    /// The private recovery-composition capability retained by the registry.
    /// </param>
    /// <param name="acceptedState">
    /// The exact registered accepted source authority.
    /// </param>
    /// <param name="resolution">
    /// The independently recomputed ordered recovery result.
    /// </param>
    /// <param name="input">
    /// Complete current wound and effect baselines with empty GM transition drafts.
    /// </param>
    /// <returns>
    /// A private continuation companion; foreign capabilities are rejected.
    /// </returns>
    /// <param name="sourceRoots">
    /// Complete canonical effect-source roots read under the same live lease.
    /// </param>
    internal static object MintRecoveryContinuation(
        object capability,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundRecoveryResolution resolution,
        WoundAcceptedTurnInput input,
        IReadOnlyDictionary<string, JsonNode?> sourceRoots)
    {
        if (!AcceptedTurnAuthorityRegistry.IsMortalWoundRecoveryCompositionCapability(capability))
            throw new ArgumentException("Recovery composition requires its private registry capability.", nameof(capability));
        return new RecoveryContinuationAuthority(acceptedState, resolution, input, sourceRoots);
    }

    /// <summary>
    /// Recognizes a sealed private continuation and exports detached comparison data.
    /// </summary>
    /// <param name="authority">
    /// The actual private companion; null and detached records are rejected.
    /// </param>
    /// <param name="continuation">
    /// Receives a detached view only when the private seal remains valid.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for the actual sealed companion; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool TryReadRecoveryContinuation(
        object? authority,
        out RecoveryContinuationView continuation)
    {
        if (authority is RecoveryContinuationAuthority actual && actual.IsSealed)
        {
            continuation = actual.Read();
            return true;
        }
        continuation = null!;
        return false;
    }

    /// <summary>
    /// Reads the fingerprint of an actual sealed continuation.
    /// </summary>
    /// <param name="authority">
    /// The private continuation; foreign or changed values throw.
    /// </param>
    /// <returns>
    /// Its independently recomputed complete preparation fingerprint.
    /// </returns>
    internal static string GetRecoveryContinuationFingerprint(object authority) =>
        TryReadRecoveryContinuation(authority, out var continuation)
            ? continuation.Fingerprint
            : throw new ArgumentException("A recovery continuation must retain its private source seal.", nameof(authority));

    /// <summary>
    /// Checks the exact selected source, baselines and allocated coordinates of a recovery preparation.
    /// </summary>
    /// <param name="prepared">
    /// The prepared stage with its private continuation and no ordinary GM drafts.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when every retained preparation seal agrees; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RecoveryContinuationPreparedAgrees(WoundPreparedAcceptedTurnPlan prepared)
    {
        if (prepared is null || prepared.TreatmentContinuationAuthority is not null ||
            prepared.DraftBefore is not null ||
            !TryReadRecoveryContinuation(prepared.RecoveryContinuationAuthority, out var continuation))
            return false;
        return prepared.AllocatedWoundIds.SequenceEqual(new[] { continuation.Before.WoundId }, StringComparer.Ordinal) &&
               prepared.AllocatedTransitionIds.SequenceEqual(continuation.AllocatedTransitionIds, StringComparer.Ordinal) &&
               prepared.PreparedWounds.Count == 1 &&
               WoundMaterializationContract.SerializeCanonical(prepared.PreparedWounds[0]) ==
               WoundMaterializationContract.SerializeCanonical(continuation.Before) &&
               prepared.EffectOperationBatches.Count == 0 &&
               prepared.BindingFingerprint == WoundAcceptedTurnFingerprints.ComputeBinding(continuation.Input.Binding) &&
               prepared.InputFingerprint == WoundAcceptedTurnFingerprints.ComputeInput(continuation.Input) &&
               prepared.WoundPreparationFingerprint == WoundAcceptedTurnFingerprints.ComputePreparation(prepared);
    }

    /// <summary>
    /// Prepares the privately admitted program from complete validated baselines.
    /// </summary>
    /// <param name="input">
    /// The exact original input, with no opportunities or authored transition drafts.
    /// </param>
    /// <param name="authority">
    /// The registry-minted recovery continuation.
    /// </param>
    /// <returns>
    /// A sealed preparation for actual ordered execution, or diagnostics without a plan.
    /// </returns>
    internal static WoundAcceptedTurnPreparationResult PrepareRecoveryContinuationCandidate(
        WoundAcceptedTurnInput input,
        object authority)
    {
        if (!TryReadRecoveryContinuation(authority, out var continuation) ||
            input.Opportunities.Count != 0 || input.Transitions.Count != 0 ||
            WoundAcceptedTurnFingerprints.ComputeInput(input) !=
            WoundAcceptedTurnFingerprints.ComputeInput(continuation.Input))
            return RecoveryPreparationFailure("foreign continuation or changed baseline input");
        var validation = WoundAcceptedTurnPlannerCore.ValidateInput(input);
        if (validation.Issues.Count != 0)
            return new WoundAcceptedTurnPreparationResult(null, validation.Issues);
        var catalog = WoundCarrierCatalog.Build(input.PreTurnCarriers);
        var matches = catalog.Occurrences.Where(row => row.WoundId == continuation.Before.WoundId).ToArray();
        if (catalog.Issues.Count != 0 || matches.Length != 1 ||
            WoundMaterializationContract.SerializeCanonical(matches[0].Wound) !=
            WoundMaterializationContract.SerializeCanonical(continuation.Before))
            return RecoveryPreparationFailure("missing or changed selected source wound");
        var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
        var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(inputFingerprint, input);
        WoundPreparedAcceptedTurnPlan Build(string fingerprint) => new(
            input.Binding,
            WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding),
            inputFingerprint,
            fingerprint,
            new[] { continuation.Before.WoundId },
            continuation.AllocatedTransitionIds,
            new[] { continuation.Before },
            Array.Empty<WoundEffectOperationBatch>(),
            baseline,
            recoveryContinuationAuthority: authority);
        var provisional = Build(string.Empty);
        return new WoundAcceptedTurnPreparationResult(
            Build(WoundAcceptedTurnFingerprints.ComputePreparation(provisional)),
            Array.Empty<ValidationIssue>());
    }

    /// <summary>
    /// Derives the complete recovery effect input from its sealed source and signed minute.
    /// </summary>
    /// <param name="prepared">
    /// The exact private preparation; invalid or incomplete preparations throw.
    /// </param>
    /// <returns>
    /// Detached ordinary effect baselines and accepted events, with no additional commands.
    /// </returns>
    internal static EffectAcceptedTurnInput ComposeRecoveryEffectInput(WoundPreparedAcceptedTurnPlan prepared)
    {
        if (!RecoveryContinuationPreparedAgrees(prepared) ||
            !TryReadRecoveryContinuation(prepared.RecoveryContinuationAuthority, out var continuation))
            throw new ArgumentException("Recovery effect input requires an exact private prepared stage.", nameof(prepared));
        var binding = continuation.Input.Binding;
        var carriers = continuation.Input.PreTurnEffectCarriers ??
            throw new ArgumentException("Recovery has no complete effect carrier baseline.", nameof(prepared));
        return EffectAcceptedTurnInputComposer.Compose(binding.SessionId,
            binding.SnapshotToken, binding.Turn, new JsonObject(), carriers, carriers,
            continuation.Input.PreTurnEffectIdentityIndex, continuation.SourceRoots,
            currentWorldTime: continuation.Resolution.CurrentTimeInMinutes,
            publicationCarrierBaselines: carriers, realm: binding.Realm,
            preparedWoundPlan: prepared);
    }

    /// <summary>
    /// Compares an effect handoff with a fresh independent derivation from the private source.
    /// </summary>
    /// <param name="prepared">
    /// The exact recovery preparation.
    /// </param>
    /// <param name="input">
    /// The supplied complete effect handoff.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when all input semantics agree; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RecoveryContinuationEffectInputAgrees(
        WoundPreparedAcceptedTurnPlan prepared,
        EffectAcceptedTurnInput input) =>
        WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, input) ==
        WoundAcceptedTurnFingerprints.ComputeEffectInput(prepared, ComposeRecoveryEffectInput(prepared));

    /// <summary>
    /// Revalidates the complete prepared, executed and finalized recovery bundle.
    /// </summary>
    /// <param name="authority">
    /// Its actual private continuation companion.
    /// </param>
    /// <param name="bundle">
    /// The complete ordinary handoff to common publication.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the private companion and recomputed stages agree;
    /// otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RecoveryContinuationFinalPlanAgrees(
        object? authority,
        AcceptedMechanicsWoundStageBundle bundle) =>
        authority is not null && ReferenceEquals(authority, bundle.PreparedPlan.RecoveryContinuationAuthority) &&
        RecoveryContinuationPreparedAgrees(bundle.PreparedPlan) &&
        RecoveryContinuationEffectInputAgrees(bundle.PreparedPlan, bundle.EffectBatchPlan.EffectInput) &&
        WoundAcceptedTurnPlanCache.ValidateFinalResult(bundle.PreparedPlan, bundle.EffectBatchPlan,
            new WoundAcceptedTurnPlanningResult(bundle.FinalPlan, Array.Empty<ValidationIssue>())).Success;

    /// <summary>
    /// Compares every closed scalar field and the complete ordered typed intent vector.
    /// </summary>
    /// <param name="first">
    /// The independently recomputed result.
    /// </param>
    /// <param name="second">
    /// The submitted result to authenticate.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for equal complete semantics; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RecoveryResolutionsEqual(
        MortalWoundRecoveryResolution first,
        MortalWoundRecoveryResolution second) =>
        (first with { TransitionIntents = Array.Empty<IMortalWoundRecoveryTransitionIntent>() }) ==
        (second with { TransitionIntents = Array.Empty<IMortalWoundRecoveryTransitionIntent>() }) &&
        first.TransitionIntents.SequenceEqual(second.TransitionIntents);

    /// <summary>
    /// Recognizes the privately admitted natural program's final rank-I threshold.
    /// Ordinary recovery evidence does not authorize a progress reset.
    /// </summary>
    /// <param name="authority">
    /// The exact private recovery continuation.
    /// </param>
    /// <param name="before">
    /// The unpublished rank-I wound immediately before the full-heal staging boundary.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the original sealed natural program reaches full
    /// healing for this Mortal wound; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RecoveryContinuationProvesFullHeal(object? authority,
        WoundMaterializationEnvelope before)
    {
        if (!TryReadRecoveryContinuation(authority, out var continuation) ||
            before.WoundId != continuation.Before.WoundId || before.Owner != continuation.Before.Owner ||
            before.Owner.Realm != "mortal_world" || before.Classification.Domain != "physical" ||
            before.Lifecycle != "active" || before.Severity.Rank != 1 ||
            before.Recovery.CurrentStepThreshold != continuation.Before.Recovery.CurrentStepThreshold ||
            before.Recovery.CarryOverflow != continuation.Before.Recovery.CarryOverflow)
            return false;
        var progress = continuation.Resolution.TransitionIntents
            .OfType<MortalWoundRecoveryProgressIntent>().SingleOrDefault();
        if (progress is null)
            return false;
        var program = MortalWoundRecoveryProgressProgram.Create(
            continuation.Before.Severity.Rank, continuation.Before.Recovery.CurrentStepProgress,
            continuation.Before.Recovery.CurrentStepThreshold, progress.ElapsedCadences,
            continuation.Before.Recovery.CarryOverflow);
        if (program.Length == 0 || !program[^1].FullHeal)
            return false;
        var stagingIndex = program.Length - 2;
        if (stagingIndex == 0)
            return WoundMaterializationContract.SerializeCanonical(before) ==
                   WoundMaterializationContract.SerializeCanonical(continuation.Before);
        return before.LastTransition.Kind == "recover" &&
               before.LastTransition.TransitionId == continuation.AllocatedTransitionIds[stagingIndex - 1] &&
               before.LastTransition.Turn == continuation.Input.Binding.Turn &&
               before.LastTransition.Ordinal == checked(continuation.Before.LastTransition.Ordinal + stagingIndex) &&
               before.Recovery.CurrentStepProgress == program[stagingIndex - 1].Progress;
    }

    /// <summary>
    /// Recognizes the registered add-complication recovery policy while retaining
    /// the complete source complication facts through preceding severity stages.
    /// </summary>
    /// <param name="authority">
    /// The privately minted recovery continuation; detached records do not qualify.
    /// </param>
    /// <param name="before">
    /// The current unpublished wound before the adverse stage.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for the exact admitted add-complication policy and
    /// retained source facts; otherwise, <see langword="false"/>.
    /// </returns>
    internal static bool RecoveryContinuationProvesAddComplication(
        object? authority,
        WoundMaterializationEnvelope before)
    {
        if (!TryReadRecoveryContinuation(authority, out var continuation) ||
            before.Owner != continuation.Before.Owner || before.WoundId != continuation.Before.WoundId ||
            before.Lifecycle != "active" || before.Classification.Domain != "physical" ||
            before.Owner.Realm != "mortal_world" ||
            before.Recovery.DeteriorationPolicy is not { } policy ||
            continuation.Before.Recovery.DeteriorationPolicy is not { } sourcePolicy ||
            !JsonNode.DeepEquals(JsonNode.Parse(policy.GetRawText()), JsonNode.Parse(sourcePolicy.GetRawText())) ||
            !before.Complications.Select(row => row with { OwnedEffectIds = Array.Empty<string>() })
                .SequenceEqual(continuation.Before.Complications.Select(row =>
                    row with { OwnedEffectIds = Array.Empty<string>() })))
            return false;
        var parsed = MortalWoundDeteriorationPolicyContract.Parse(policy,
            "recovery.deteriorationPolicy", before.Owner.Realm,
            WoundMaterializationContract.ResolveEffectTargetKind(before.Owner.OwnerKind), before.Severity.Rank);
        return parsed.IsValid && parsed.Policy is { ResultKind: MortalWoundDeteriorationResultKind.AddComplication } declared &&
               continuation.Resolution.TransitionIntents.OfType<MortalWoundRecoveryDeteriorationIntent>()
                   .Count(intent => intent.PolicyRef == declared.PolicyRef) == 1;
    }

    /// <summary>
    /// Detaches the complete canonical source-root map.
    /// </summary>
    /// <param name="source">
    /// Root projections whose JSON must not expose retained mutable state.
    /// </param>
    /// <returns>
    /// A read-only map containing fresh JSON copies and preserved missing roots.
    /// </returns>
    private static IReadOnlyDictionary<string, JsonNode?> CloneRecoverySourceRoots(
        IReadOnlyDictionary<string, JsonNode?> source) =>
        new ReadOnlyDictionary<string, JsonNode?>(source.ToDictionary(
            pair => pair.Key, pair => pair.Value?.DeepClone(), StringComparer.Ordinal));

    /// <summary>
    /// Freezes all supported typed intents and the optional lifecycle handoff.
    /// </summary>
    /// <param name="source">
    /// The complete resolution to detach; unknown intent types are rejected.
    /// </param>
    /// <returns>
    /// A detached closed resolution with a read-only ordered intent vector.
    /// </returns>
    private static MortalWoundRecoveryResolution CloneRecoveryResolution(MortalWoundRecoveryResolution source) =>
        source with
        {
            DeathHandoff = source.DeathHandoff is null ? null : source.DeathHandoff with { },
            TransitionIntents = new ReadOnlyCollection<IMortalWoundRecoveryTransitionIntent>(
                source.TransitionIntents.Select(intent => intent switch
                {
                    MortalWoundRecoveryProgressIntent progress => (IMortalWoundRecoveryTransitionIntent)(progress with { }),
                    MortalWoundRecoveryDeteriorationIntent adverse => adverse with { },
                    MortalWoundDeathHandoffIntent death => death with { },
                    _ => throw new ArgumentException("Recovery has an unsupported intent.", nameof(source))
                }).ToArray())
        };

    /// <summary>
    /// Projects every scalar and concrete typed intent into fingerprint comparison data.
    /// </summary>
    /// <param name="source">
    /// The complete recovery resolution to seal.
    /// </param>
    /// <returns>
    /// A detached full projection retaining concrete intent fields.
    /// </returns>
    private static JsonObject RecoveryResolutionProjection(MortalWoundRecoveryResolution source)
    {
        var root = JsonSerializer.SerializeToNode(source with
            { TransitionIntents = Array.Empty<IMortalWoundRecoveryTransitionIntent>() })!.AsObject();
        root[nameof(source.TransitionIntents)] = new JsonArray(source.TransitionIntents.Select(intent =>
            JsonSerializer.SerializeToNode(intent, intent.GetType())).ToArray());
        return root;
    }

    /// <summary>
    /// Bounds the ordered natural and adverse program before allocating stage coordinates.
    /// </summary>
    /// <param name="before">
    /// The exact active source wound and declared threshold policy.
    /// </param>
    /// <param name="resolution">
    /// The complete admitted intent vector.
    /// </param>
    /// <returns>
    /// The bounded stage count, including one evaluation stage for a no-op result.
    /// A healing program followed by an adverse result is rejected.
    /// </returns>
    private static int RecoveryStageCount(WoundMaterializationEnvelope before, MortalWoundRecoveryResolution resolution)
    {
        var progress = resolution.TransitionIntents.OfType<MortalWoundRecoveryProgressIntent>().SingleOrDefault();
        var stages = progress is null ? Array.Empty<MortalWoundRecoveryProgressStage>() :
            MortalWoundRecoveryProgressProgram.Create(before.Severity.Rank,
                before.Recovery.CurrentStepProgress, before.Recovery.CurrentStepThreshold,
                progress.ElapsedCadences, before.Recovery.CarryOverflow).ToArray();
        var adverse = resolution.TransitionIntents.Count(intent => intent is MortalWoundRecoveryDeteriorationIntent);
        if (stages.Any(stage => stage.FullHeal) && adverse != 0)
            throw new InvalidOperationException("A completed natural healing cannot receive an ordered adverse wound result.");
        return Math.Max(1, checked(stages.Length + adverse));
    }

    /// <summary>
    /// Allocates a deterministic coordinate within one logical recovery evaluation.
    /// </summary>
    /// <param name="kind">
    /// The distinct transition or operation coordinate domain.
    /// </param>
    /// <param name="tickKey">
    /// The original logical evaluation tick.
    /// </param>
    /// <param name="ordinal">
    /// The one-based ordered stage ordinal.
    /// </param>
    /// <returns>
    /// An exact identifier derived from this domain, tick and ordinal.
    /// </returns>
    private static string RecoveryCoordinate(string kind, string tickKey, int ordinal) => kind + "_" +
        WoundAcceptedTurnFingerprintWriter.Compute(new[]
        {
            "book_of_eternity.mortal_wound.recovery_stage_coordinate", "1", kind, tickKey,
            ordinal.ToString(CultureInfo.InvariantCulture)
        })[..24];

    /// <summary>
    /// Reports invalid private preparation without retaining a partial plan.
    /// </summary>
    /// <param name="actual">
    /// The rejected authority or baseline condition.
    /// </param>
    /// <returns>
    /// A failed preparation containing one structured diagnostic.
    /// </returns>
    private static WoundAcceptedTurnPreparationResult RecoveryPreparationFailure(string actual) => new(
        null,
        new[] { WoundAcceptedTurnPlannerCore.NewIssue("wound_plan_recovery_continuation_invalid",
            "Recovery requires one private continuation and exact original baselines.",
            "one registry-admitted recovery continuation", actual) });
}
