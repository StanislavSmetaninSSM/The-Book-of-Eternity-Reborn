using System.Collections.Immutable;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Services;

internal static partial class WoundAcceptedTurnPlanner
{
    internal const string TreatmentPublicationSummary =
        "The accepted treatment result is retained for replay.";

    private sealed class TreatmentContinuationAuthority
    {
        internal TreatmentContinuationAuthority(
            WoundMaterializationEnvelope before,
            WoundMaterializationEnvelope after,
            MortalWoundTreatmentAcceptedStateAuthority acceptedStateAuthority,
            MortalWoundTreatmentAttemptRequest requestAuthority,
            MortalWoundTreatmentResolution resolution,
            MortalWoundTreatmentResourceFinalization resourceFinalization,
            string transitionId,
            MortalWoundTreatmentPersistedResult persistedResult,
            string acceptedStateFingerprint,
            string baseSemanticFingerprint,
            string semanticFingerprint,
            object reservationAuthority,
            MortalTreatmentItemCommandEnvelope itemCommandEnvelope,
            object skillProjectionAuthority,
            string fingerprint)
        {
            Before = before;
            After = after;
            AcceptedStateAuthority = acceptedStateAuthority;
            RequestAuthority = requestAuthority;
            Resolution = resolution;
            ResourceFinalization = resourceFinalization;
            TransitionId = transitionId;
            PersistedResult = persistedResult;
            AcceptedStateFingerprint = acceptedStateFingerprint;
            BaseSemanticFingerprint = baseSemanticFingerprint;
            SemanticFingerprint = semanticFingerprint;
            ReservationAuthority = reservationAuthority;
            ItemCommandEnvelope = itemCommandEnvelope;
            SkillProjectionAuthority = skillProjectionAuthority;
            Fingerprint = fingerprint;
        }

        internal WoundMaterializationEnvelope Before { get; }
        internal WoundMaterializationEnvelope After { get; }
        internal MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority { get; }
        internal MortalWoundTreatmentAttemptRequest RequestAuthority { get; }
        internal MortalWoundTreatmentResolution Resolution { get; }
        internal MortalWoundTreatmentResourceFinalization ResourceFinalization { get; }
        internal string TransitionId { get; }
        internal MortalWoundTreatmentPersistedResult PersistedResult { get; }
        internal string AcceptedStateFingerprint { get; }
        internal string BaseSemanticFingerprint { get; }
        internal string SemanticFingerprint { get; }
        internal object ReservationAuthority { get; }
        internal MortalTreatmentItemCommandEnvelope ItemCommandEnvelope { get; }
        internal object SkillProjectionAuthority { get; }
        internal string Fingerprint { get; }
    }

    internal sealed record TreatmentContinuationView(
        WoundMaterializationEnvelope Before,
        WoundMaterializationEnvelope After,
        MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest RequestAuthority,
        MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentResourceFinalization ResourceFinalization,
        string TransitionId,
        MortalWoundTreatmentPersistedResult PersistedResult,
        string AcceptedStateFingerprint,
        string BaseSemanticFingerprint,
        string SemanticFingerprint,
        object ReservationAuthority,
        MortalTreatmentItemCommandEnvelope ItemCommandEnvelope,
        object SkillProjectionAuthority,
        string Fingerprint);

    internal sealed class MortalWoundTreatmentPublicationResult
    {
        private MortalWoundTreatmentPublicationResult(
            bool isValid,
            IReadOnlyList<ValidationIssue> issues,
            AcceptedMechanicsPlan? plan)
        {
            IsValid = isValid;
            Issues = Array.AsReadOnly(issues.ToArray());
            Plan = plan;
        }

        public bool IsValid { get; }
        public IReadOnlyList<ValidationIssue> Issues { get; }
        public AcceptedMechanicsPlan? Plan { get; }

        internal static MortalWoundTreatmentPublicationResult Valid(
            AcceptedMechanicsPlan plan) => new(
            true,
            Array.Empty<ValidationIssue>(),
            plan);

        internal static MortalWoundTreatmentPublicationResult Invalid(
            IEnumerable<ValidationIssue> issues) => new(
            false,
            issues.ToArray(),
            null);
    }

    internal static MortalWoundTreatmentPublicationResult
        ComposeMortalWoundTreatmentPublication(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            GameResponse response,
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution)
    {
        if (fileSystem is null || writeLease is null || response is null ||
            acceptedState is null || request is null || resolution is null)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_input_missing",
                "six complete non-null publication arguments",
                "missing argument");
        }
        try
        {
            fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException or ObjectDisposedException or
                ArgumentException)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_lease_invalid",
                "one active canonical write lease",
                exception.GetType().Name);
        }

        var frozenCommands = FreezeTreatmentSkillCommands(response);
        if (frozenCommands.Envelope is null)
            return MortalWoundTreatmentPublicationResult.Invalid(frozenCommands.Issues);
        var commandEnvelope = frozenCommands.Envelope;
        var frozenItemCommands = FreezeTreatmentItemCommands(response);
        if (frozenItemCommands.Envelope is null)
        {
            return MortalWoundTreatmentPublicationResult.Invalid(
                frozenItemCommands.Issues);
        }
        var itemCommandEnvelope = frozenItemCommands.Envelope;
        var baseSemanticFingerprint = ComputeTreatmentPublicationFingerprint(
            acceptedState,
            request,
            resolution);
        var semanticFingerprint = ComputeTreatmentPublicationEnvelopeFingerprint(
            baseSemanticFingerprint,
            commandEnvelope,
            itemCommandEnvelope);
        var skillSemanticFingerprint = ComputeTreatmentSkillSemanticFingerprint(
            commandEnvelope);
        var reservation = AcceptedTurnAuthorityRegistry
            .ReserveMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                acceptedState,
                semanticFingerprint);
        if (reservation.Status ==
            MortalWoundTreatmentPublicationReservationStatus.RestartRequired)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_compensation_restart_required",
                "a fresh session generation without an unresolved confirmed hold",
                "the current generation is blocked by an unpublishable held command");
        }
        if (reservation.Status ==
            MortalWoundTreatmentPublicationReservationStatus.Conflict)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_conflict",
                "the exact first sealed guaranteed stabilization semantics",
                "different accepted treatment semantics are already cached");
        }
        if (reservation.Status ==
            MortalWoundTreatmentPublicationReservationStatus.Exact)
        {
            if (AcceptedMechanicsPlanAuthority.TryPeekValidated(
                    fileSystem,
                    writeLease,
                    out _,
                    out var cached) &&
                cached.Success && cached.Plan is not null)
            {
                return MortalWoundTreatmentPublicationResult.Valid(cached.Plan);
            }
            return PublicationFailure(
                "mortal_wound_treatment_publication_cache_missing",
                "the original exact validated common plan",
                "publication fingerprint exists without its plan");
        }

        try
        {
            var shell = ValidateGuaranteedStabilizationShell(
                acceptedState,
                request,
                resolution);
            if (shell.Issues.Count != 0 || shell.Finalization is null)
                return MortalWoundTreatmentPublicationResult.Invalid(shell.Issues);

            if (!MortalItemAcceptedTurnAuthority.HasValidatedItems(
                    fileSystem,
                    writeLease))
            {
                var itemValidationIssues = new ValidationService(
                        fileSystem,
                        NullLogger<ValidationService>.Instance)
                    .ValidateAcceptedTurnRawMortalItemMaterializationAsync(
                        writeLease)
                    .GetAwaiter()
                    .GetResult();
                if (itemValidationIssues.Count != 0)
                {
                    return MortalWoundTreatmentPublicationResult.Invalid(
                        itemValidationIssues);
                }
            }

            var skillProjection = CreateTreatmentSkillProjection(
                fileSystem,
                writeLease,
                commandEnvelope,
                skillSemanticFingerprint,
                reservation.Authority!,
                suppliedNpcSemanticBaseline: null);
            if (skillProjection.Authority is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    skillProjection.Issues);
            }

            var baselines = ReadPublicationBaselines(fileSystem, writeLease);
            if (baselines.Issues.Count != 0)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    baselines.Issues);
            }

            var transitionId = CreateTreatmentTransitionId(resolution);
            var after = CreateGuaranteedStabilizationAfter(
                request.RouteSourceWound,
                resolution,
                transitionId,
                acceptedState.CurrentGameMinute);
            var parsedAfter = WoundMaterializationContract.Parse(
                WoundMaterializationContract.SerializeCanonical(after),
                "treatmentPublication.afterWound");
            if (!parsedAfter.IsValid || parsedAfter.Wound is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    parsedAfter.Issues);
            }
            after = parsedAfter.Wound;
            var continuationAuthority = CreateTreatmentContinuationAuthority(
                request.RouteSourceWound,
                after,
                request,
                resolution,
                shell.Finalization,
                transitionId,
                acceptedState,
                baseSemanticFingerprint,
                semanticFingerprint,
                reservation.Authority!,
                itemCommandEnvelope,
                skillProjection.Authority);
            var input = new WoundAcceptedTurnInput(
                acceptedState.Binding,
                Array.Empty<WoundOpportunityAuthority>(),
                Array.Empty<WoundAcceptedTransitionDraft>(),
                baselines.WoundCarriers!,
                baselines.WoundIdentity!,
                baselines.WoundHistory!,
                baselines.EffectCarriers,
                baselines.EffectIdentity);
            var preparedResult = WoundAcceptedTurnPlanAuthority
                .GetOrBuildTreatmentContinuationPreparedValidated(
                    fileSystem,
                    writeLease,
                    input,
                    continuationAuthority,
                    reservation.Authority!);
            if (!preparedResult.Success || preparedResult.Plan is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    preparedResult.Issues);
            }
            var prepared = preparedResult.Plan;
            var effectInput = EffectAcceptedTurnInputComposer.Compose(
                acceptedState.Binding.SessionId,
                acceptedState.Binding.SnapshotToken,
                acceptedState.Binding.Turn,
                EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
                baselines.EffectCarriers!,
                baselines.EffectCarriers!,
                baselines.EffectIdentity,
                baselines.SourceRoots!,
                currentWorldTime: acceptedState.CurrentGameMinute,
                publicationCarrierBaselines: baselines.EffectCarriers,
                realm: acceptedState.Binding.Realm,
                preparedWoundPlan: prepared);
            var effectResult = WoundAcceptedTurnPlanAuthority
                .GetOrBuildTreatmentContinuationEffectValidated(
                    fileSystem,
                    writeLease,
                    prepared,
                    effectInput,
                    continuationAuthority,
                    reservation.Authority!);
            if (!effectResult.Success || effectResult.Plan is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    effectResult.Issues);
            }
            var finalResult = WoundAcceptedTurnPlanAuthority
                .GetOrBuildTreatmentContinuationFinalValidated(
                    fileSystem,
                    writeLease,
                    prepared,
                    effectResult,
                    continuationAuthority,
                    reservation.Authority!);
            if (!finalResult.Success || finalResult.Plan is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    finalResult.Issues);
            }
            var bundle = new AcceptedMechanicsWoundStageBundle(
                input,
                prepared,
                effectResult.Plan,
                finalResult.Plan);
            var commonInput = AcceptedMechanicsWoundCommonInputComposer
                .ComposeTreatmentContinuation(
                    fileSystem,
                    writeLease,
                    bundle,
                    continuationAuthority,
                    reservation.Authority!,
                    acceptedState.CurrentGameMinute);
            if (!commonInput.Success || commonInput.Input is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    commonInput.Issues);
            }
            var commonResult = AcceptedMechanicsPlanAuthority
                .GetOrBuildMortalWoundTreatmentValidated(
                    fileSystem,
                    writeLease,
                    commonInput.Input,
                    bundle,
                    acceptedState,
                    request,
                    resolution,
                    semanticFingerprint,
                    continuationAuthority,
                    reservation.Authority!);
            return commonResult.Success && commonResult.Plan is not null
                ? MortalWoundTreatmentPublicationResult.Valid(commonResult.Plan)
                : MortalWoundTreatmentPublicationResult.Invalid(
                    commonResult.Issues);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                InvalidDataException or IOException or ObjectDisposedException or
                UnauthorizedAccessException or System.Text.Json.JsonException or
                NullReferenceException or OverflowException)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_invalid",
                "one complete sealed guaranteed stabilization publication",
                exception.GetType().Name);
        }
        finally
        {
            AcceptedTurnAuthorityRegistry.AbortMortalWoundTreatmentPublication(
                fileSystem,
                writeLease,
                reservation.Authority);
        }
    }

    private static object CreateTreatmentContinuationAuthority(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentResourceFinalization resourceFinalization,
        string transitionId,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        string baseSemanticFingerprint,
        string semanticFingerprint,
        object reservationAuthority,
        MortalTreatmentItemCommandEnvelope itemCommandEnvelope,
        object skillProjectionAuthority)
    {
        if (!TryReadTreatmentSkillProjectionForEnvelopeSemantics(
                skillProjectionAuthority,
                reservationAuthority,
                out var skillProjection))
        {
            throw new InvalidOperationException(
                "Treatment continuation requires one exact sealed skill projection.");
        }
        var persisted = MortalWoundTreatmentPersistedResult.Create(resolution);
        var fingerprint = ComputeTreatmentContinuationFingerprint(
            before,
            after,
            resolution,
            resourceFinalization.FinalizationFingerprint,
            transitionId,
            acceptedState.AcceptedStateFingerprint,
            baseSemanticFingerprint,
            semanticFingerprint,
            itemCommandEnvelope.Fingerprint,
            skillProjection.Fingerprint);
        return new TreatmentContinuationAuthority(
            before,
            after,
            acceptedState,
            request,
            resolution,
            resourceFinalization,
            transitionId,
            persisted,
            acceptedState.AcceptedStateFingerprint,
            baseSemanticFingerprint,
            semanticFingerprint,
            reservationAuthority,
            itemCommandEnvelope,
            skillProjectionAuthority,
            fingerprint);
    }

    internal static string GetTreatmentContinuationFingerprint(object authority)
    {
        if (!TryReadTreatmentContinuation(authority, out var continuation))
        {
            throw new InvalidOperationException(
                "The treatment continuation authority is not private-minted or sealed.");
        }
        return continuation.Fingerprint;
    }

    internal static bool TryReadTreatmentContinuation(
        object? authority,
        out TreatmentContinuationView continuation)
    {
        continuation = null!;
        if (authority is not TreatmentContinuationAuthority candidate ||
            candidate.AcceptedStateAuthority is null ||
            candidate.RequestAuthority is null ||
            candidate.Resolution is null ||
            candidate.ResourceFinalization is null ||
            candidate.ReservationAuthority is null ||
            candidate.ItemCommandEnvelope is null ||
            !ReferenceEquals(
                candidate.Resolution.RequestAuthority,
                candidate.RequestAuthority) ||
            !string.Equals(
                candidate.AcceptedStateAuthority.AcceptedStateFingerprint,
                candidate.AcceptedStateFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                candidate.ResourceFinalization.RequestFingerprint,
                candidate.RequestAuthority.RequestFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                candidate.ResourceFinalization.ResultFingerprint,
                candidate.Resolution.ResultFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                candidate.ResourceFinalization.ResourceAuthorityFingerprint,
                candidate.RequestAuthority.ResourceAuthority.AuthorityFingerprint,
                StringComparison.Ordinal) ||
            !TryReadTreatmentSkillProjectionForEnvelopeSemantics(
                candidate.SkillProjectionAuthority,
                candidate.ReservationAuthority,
                out var skillProjection))
            return false;
        var fingerprint = ComputeTreatmentContinuationFingerprint(
            candidate.Before,
            candidate.After,
            candidate.Resolution,
            candidate.ResourceFinalization.FinalizationFingerprint,
            candidate.TransitionId,
            candidate.AcceptedStateFingerprint,
            candidate.BaseSemanticFingerprint,
            candidate.SemanticFingerprint,
            candidate.ItemCommandEnvelope.Fingerprint,
            skillProjection.Fingerprint);
        if (!string.Equals(
                fingerprint,
                candidate.Fingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }
        continuation = new TreatmentContinuationView(
            candidate.Before,
            candidate.After,
            candidate.AcceptedStateAuthority,
            candidate.RequestAuthority,
            candidate.Resolution,
            candidate.ResourceFinalization,
            candidate.TransitionId,
            candidate.PersistedResult,
            candidate.AcceptedStateFingerprint,
            candidate.BaseSemanticFingerprint,
            candidate.SemanticFingerprint,
            candidate.ReservationAuthority,
            candidate.ItemCommandEnvelope,
            candidate.SkillProjectionAuthority,
            candidate.Fingerprint);
        return true;
    }

    internal static bool TreatmentContinuationPreparedAgrees(
        WoundPreparedAcceptedTurnPlan prepared)
    {
        if (!TryReadTreatmentContinuation(
                prepared.TreatmentContinuationAuthority,
                out var continuation))
        {
            return false;
        }
        var coordinates = continuation.Resolution.Coordinates;
        var binding = prepared.Binding;
        return prepared.AllocatedWoundIds.Count == 1 &&
               prepared.AllocatedTransitionIds.Count == 1 &&
               prepared.PreparedWounds.Count == 1 &&
               prepared.EffectOperationBatches.Count == 0 &&
               string.Equals(
                   prepared.AllocatedWoundIds[0],
                   continuation.Before.WoundId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   prepared.AllocatedTransitionIds[0],
                   continuation.TransitionId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   WoundMaterializationContract.SerializeCanonical(
                       prepared.PreparedWounds[0]),
                   WoundMaterializationContract.SerializeCanonical(
                       continuation.After),
                   StringComparison.Ordinal) &&
               string.Equals(binding.SessionId, coordinates.SessionId, StringComparison.Ordinal) &&
               string.Equals(binding.RequestId, coordinates.RequestId, StringComparison.Ordinal) &&
               string.Equals(binding.SnapshotToken, coordinates.SnapshotToken, StringComparison.Ordinal) &&
               string.Equals(binding.Realm, coordinates.Realm, StringComparison.Ordinal) &&
               binding.Turn == coordinates.Turn;
    }

    internal static bool TreatmentContinuationReservationAgrees(
        object continuationAuthority,
        object reservationAuthority) =>
        TryReadTreatmentContinuation(
            continuationAuthority,
            out var continuation) &&
        ReferenceEquals(
            continuation.ReservationAuthority,
            reservationAuthority);

    internal static bool TreatmentContinuationReservationAgrees(
        object continuationAuthority,
        object reservationAuthority,
        string semanticFingerprint) =>
        TreatmentContinuationReservationAgrees(
            continuationAuthority,
            reservationAuthority) &&
        TryReadTreatmentContinuation(
            continuationAuthority,
            out var continuation) &&
        string.Equals(
            continuation.SemanticFingerprint,
            semanticFingerprint,
            StringComparison.Ordinal);

    internal static bool TreatmentContinuationPublicationAgrees(
        object authority,
        AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        string semanticFingerprint)
    {
        if (!ReferenceEquals(
                bundle.PreparedPlan.TreatmentContinuationAuthority,
                authority) ||
            !TryReadTreatmentContinuation(authority, out var continuation) ||
            !TreatmentContinuationPreparedAgrees(bundle.PreparedPlan))
        {
            return false;
        }
        var recomputedBaseSemantic = ComputeTreatmentPublicationFingerprint(
            acceptedState,
            request,
            resolution);
        return ReferenceEquals(continuation.AcceptedStateAuthority, acceptedState) &&
               ReferenceEquals(continuation.RequestAuthority, request) &&
               ReferenceEquals(continuation.Resolution, resolution) &&
               string.Equals(
                   continuation.AcceptedStateFingerprint,
                   acceptedState.AcceptedStateFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   continuation.Resolution.RequestFingerprint,
                   request.RequestFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   continuation.BaseSemanticFingerprint,
                   recomputedBaseSemantic,
                   StringComparison.Ordinal) &&
               string.Equals(
                   continuation.SemanticFingerprint,
                   semanticFingerprint,
                   StringComparison.Ordinal);
    }

    internal static WoundAcceptedTurnPreparationResult
        PrepareTreatmentContinuationCandidate(
            WoundAcceptedTurnInput input,
            object authority)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (!TryReadTreatmentContinuation(authority, out var continuation))
        {
            return new WoundAcceptedTurnPreparationResult(
                null,
                new[]
                {
                    WoundAcceptedTurnPlannerCore.NewIssue(
                        "wound_plan_treatment_continuation_invalid",
                        "Treatment continuation requires one private-minted sealed authority.",
                        "private treatment continuation authority",
                        "foreign or changed authority")
                });
        }
        var validation = WoundAcceptedTurnPlannerCore.ValidateInput(input);
        if (validation.Issues.Count != 0)
            return new WoundAcceptedTurnPreparationResult(null, validation.Issues);
        if (validation.Transitions.Count != 0 ||
            input.Opportunities.Count != 0 ||
            input.Transitions.Count != 0)
        {
            return new WoundAcceptedTurnPreparationResult(
                null,
                new[]
                {
                    WoundAcceptedTurnPlannerCore.NewIssue(
                        "wound_plan_treatment_continuation_invalid",
                        "Treatment continuation cannot impersonate an accepted wound opportunity.",
                        "empty opportunity and transition drafts",
                        $"opportunities={input.Opportunities.Count};transitions={input.Transitions.Count}")
                });
        }

        var beforeCatalog = WoundCarrierCatalog.Build(input.PreTurnCarriers);
        var matches = beforeCatalog.Occurrences.Where(value => string.Equals(
                value.WoundId,
                continuation.Before.WoundId,
                StringComparison.Ordinal))
            .ToArray();
        if (beforeCatalog.Issues.Count != 0 ||
            matches.Length != 1 ||
            !string.Equals(
                WoundIdentityState.ComputeSemanticFingerprint(matches[0].Wound),
                WoundIdentityState.ComputeSemanticFingerprint(continuation.Before),
                StringComparison.Ordinal))
        {
            return new WoundAcceptedTurnPreparationResult(
                null,
                new[]
                {
                    WoundAcceptedTurnPlannerCore.NewIssue(
                        "wound_plan_treatment_continuation_invalid",
                        "Treatment continuation must bind one exact existing baseline wound.",
                        continuation.Before.WoundId,
                        $"matches={matches.Length}")
                });
        }

        var bindingFingerprint = WoundAcceptedTurnFingerprints.ComputeBinding(input.Binding);
        var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
        var baseline = WoundAcceptedTurnPlannerCore.CreateBaselineAuthority(
            inputFingerprint,
            input);
        var provisional = new WoundPreparedAcceptedTurnPlan(
            input.Binding,
            bindingFingerprint,
            inputFingerprint,
            string.Empty,
            new[] { continuation.Before.WoundId },
            new[] { continuation.TransitionId },
            new[] { continuation.After },
            Array.Empty<WoundEffectOperationBatch>(),
            baseline,
            authority);
        var preparationFingerprint =
            WoundAcceptedTurnFingerprints.ComputePreparation(provisional);
        return new WoundAcceptedTurnPreparationResult(
            new WoundPreparedAcceptedTurnPlan(
                input.Binding,
                bindingFingerprint,
                inputFingerprint,
                preparationFingerprint,
                new[] { continuation.Before.WoundId },
                new[] { continuation.TransitionId },
                new[] { continuation.After },
                Array.Empty<WoundEffectOperationBatch>(),
                baseline,
                authority),
            Array.Empty<ValidationIssue>());
    }

    private static string ComputeTreatmentContinuationFingerprint(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        MortalWoundTreatmentResolution resolution,
        string resourceFinalizationFingerprint,
        string transitionId,
        string acceptedStateFingerprint,
        string baseSemanticFingerprint,
        string semanticFingerprint,
        string itemCommandEnvelopeFingerprint,
        string skillProjectionFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.wound.treatment_continuation_authority",
            "2",
            WoundMaterializationContract.SerializeCanonical(before),
            WoundMaterializationContract.SerializeCanonical(after),
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resourceFinalizationFingerprint,
            resolution.Coordinates.CoordinatesFingerprint,
            transitionId,
            acceptedStateFingerprint,
            baseSemanticFingerprint,
            semanticFingerprint,
            itemCommandEnvelopeFingerprint,
            skillProjectionFingerprint,
            TreatmentPublicationSummary
        });

    private static string CreateTreatmentTransitionId(
        MortalWoundTreatmentResolution resolution) =>
        "wound_transition_" + WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.wound.treatment_transition_identity",
                "1",
                resolution.Coordinates.SessionGeneration,
                resolution.Coordinates.SessionId,
                resolution.Coordinates.RequestId,
                resolution.Coordinates.SnapshotToken,
                resolution.Coordinates.OperationKey,
                resolution.Coordinates.AttemptId,
                resolution.RequestFingerprint,
                resolution.ResultFingerprint
            })["sha256:".Length..];

    private static WoundMaterializationEnvelope CreateGuaranteedStabilizationAfter(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentResolution resolution,
        string transitionId,
        long currentGameMinute)
    {
        var coordinates = resolution.Coordinates;
        var blockers = before.Recovery.Blockers
            .Where(static blocker => !string.Equals(
                blocker,
                "not_stabilized",
                StringComparison.Ordinal))
            .ToImmutableArray();
        var completedRoutes = before.Treatment.CompletedRouteIds
            .Append(coordinates.RouteId)
            .Distinct(StringComparer.Ordinal)
            .ToImmutableArray();
        var deteriorationAnchor = before.Recovery.DeteriorationAnchor is
            { ConditionKey: "not_stabilized" }
                ? null
                : before.Recovery.DeteriorationAnchor;
        var candidate = before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = coordinates.Turn,
                LastAttemptId = coordinates.AttemptId
            },
            Treatment = before.Treatment with
            {
                CompletedRouteIds = completedRoutes
            },
            Recovery = before.Recovery with
            {
                Blockers = blockers,
                RecoveryAnchor = new WoundRecoveryAnchor(
                    "stabilization",
                    currentGameMinute,
                    transitionId),
                DeteriorationAnchor = deteriorationAnchor
            },
            LastTransition = new WoundLastTransition(
                transitionId,
                checked(before.LastTransition.Ordinal + 1),
                coordinates.Turn,
                "treat")
        };
        return candidate;
    }

    private sealed record PublicationBaselines(
        WoundCarrierCatalogInput? WoundCarriers,
        JsonObject? WoundIdentity,
        JsonObject? WoundHistory,
        EffectCarrierCatalogInput? EffectCarriers,
        JsonObject? EffectIdentity,
        IReadOnlyDictionary<string, JsonNode?>? SourceRoots,
        IReadOnlyList<ValidationIssue> Issues);

    private static PublicationBaselines ReadPublicationBaselines(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        var issues = new List<ValidationIssue>();
        JsonObject? Read(string path, bool required)
        {
            var bytes = fileSystem.ReadFileBytesAsync(writeLease, path)
                .GetAwaiter()
                .GetResult();
            if (bytes is null)
            {
                if (required)
                {
                    issues.Add(PublicationIssue(
                        path,
                        "mortal_wound_treatment_publication_baseline_missing",
                        "one complete canonical baseline root",
                        "missing"));
                }
                return null;
            }
            try
            {
                var preamble = Encoding.UTF8.GetPreamble();
                var offset = bytes.AsSpan().StartsWith(preamble)
                    ? preamble.Length
                    : 0;
                var text = new UTF8Encoding(false, true)
                    .GetString(bytes, offset, bytes.Length - offset);
                return JsonNode.Parse(text)?.AsObject() ??
                    throw new InvalidDataException("Expected a JSON object.");
            }
            catch (Exception exception) when (
                exception is DecoderFallbackException or
                    System.Text.Json.JsonException or InvalidOperationException)
            {
                issues.Add(PublicationIssue(
                    path,
                    "mortal_wound_treatment_publication_baseline_invalid",
                    "one valid UTF-8 canonical object root",
                    exception.GetType().Name));
                return null;
            }
        }

        var woundCarriers = new WoundCarrierCatalogInput(
            Read(WoundCarrierCatalog.PlayerPath, required: false),
            Read(WoundCarrierCatalog.NpcPath, required: false),
            Read(WoundCarrierCatalog.EnemiesPath, required: false),
            Read(WoundCarrierCatalog.AlliesPath, required: false),
            Read(WoundCarrierCatalog.AfterlifeProfilesPath, required: false));
        var woundIdentity = Read(WoundIdentityState.StatePath, required: true);
        var woundHistory = Read(WoundHistoryState.HistoryPath, required: true);
        var effectCarriers = new EffectCarrierCatalogInput(
            Read(EffectCarrierCatalog.PlayerPath, required: false),
            Read(EffectCarrierCatalog.NpcPath, required: false),
            Read(EffectCarrierCatalog.EnemiesPath, required: false),
            Read(EffectCarrierCatalog.AlliesPath, required: false),
            Read(EffectCarrierCatalog.AfterlifeProfilesPath, required: false),
            Read(EffectCarrierCatalog.SpiritualConflictPath, required: false));
        var effectIdentity = Read(
            EffectIdentityState.StatePath,
            required: false) ?? new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray()
        };
        var sourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .ToDictionary(
                static path => path,
                path => (JsonNode?)Read(path, required: false),
                StringComparer.Ordinal);
        return new PublicationBaselines(
            woundCarriers,
            woundIdentity,
            woundHistory,
            effectCarriers,
            effectIdentity,
            sourceRoots,
            issues);
    }

    private sealed record GuaranteedStabilizationShellValidation(
        MortalWoundTreatmentResourceFinalization? Finalization,
        IReadOnlyList<ValidationIssue> Issues);

    private static GuaranteedStabilizationShellValidation
        ValidateGuaranteedStabilizationShell(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution)
    {
        var issues = new List<ValidationIssue>();
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        if (!finalization.IsValid || finalization.Finalization is null)
            issues.AddRange(finalization.Issues);
        var finalized = finalization.Finalization;
        var coordinates = request.Coordinates;
        var exactRequest = ReferenceEquals(resolution.RequestAuthority, request) ||
            string.Equals(
                resolution.RequestAuthority?.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal);
        var exactFinalization = finalized is not null &&
            exactRequest &&
            string.Equals(
                finalized.RequestFingerprint,
                request.RequestFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                finalized.ResultFingerprint,
                resolution.ResultFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                finalized.ResourceAuthorityFingerprint,
                request.ResourceAuthority.AuthorityFingerprint,
                StringComparison.Ordinal);
        if (finalized is not null && !exactFinalization)
        {
            return new GuaranteedStabilizationShellValidation(
                null,
                new[]
                {
                    PublicationIssue(
                        "treatmentPublication.resources.finalization",
                        "mortal_wound_treatment_publication_resource_finalization_mismatch",
                        "the exact finalization recomposed from the supplied sealed request and result",
                        finalized.FinalizationFingerprint)
                });
        }
        var finalizationShape = finalized switch
        {
            { Disposition: "not_required", ReservationId: null } value =>
                value.Consumptions.Count == 0 &&
                value.ReleasedClaimFingerprints.Count == 0,
            { Disposition: "release_only", ReservationId: not null } value =>
                value.Consumptions.Count == 0,
            { Disposition: "consume", ReservationId: not null } value =>
                value.Consumptions.Count != 0 &&
                value.Consumptions.All(static consumption =>
                    string.Equals(
                        consumption.Kind,
                        "resource_quantity",
                        StringComparison.Ordinal) ||
                    string.Equals(
                        consumption.Kind,
                        "item_quantity",
                        StringComparison.Ordinal)),
            _ => false
        };
        var exactCoordinates = coordinates.MatchesAcceptedState(acceptedState) &&
            string.Equals(
                resolution.Coordinates.CoordinatesFingerprint,
                coordinates.CoordinatesFingerprint,
                StringComparison.Ordinal);
        var recomposed = MortalWoundTreatmentOutcomeIntentComposer.TryCompose(
            request,
            resolution.DeclaredResult,
            acceptedState,
            out var recomposedIntents,
            out _);
        var exactOutcome = resolution.DeclaredResult.Count == 1 &&
            resolution.DeclaredResult[0] is MortalWoundStabilizeOperation &&
            resolution.OutcomeIntents.Count == 1 &&
            resolution.OutcomeIntents[0] is MortalWoundStabilizeOutcomeIntent actualIntent &&
            recomposed &&
            recomposedIntents.Count == 1 &&
            recomposedIntents[0] is MortalWoundStabilizeOutcomeIntent expectedIntent &&
            actualIntent.OperationOrdinal == 0 &&
            string.Equals(actualIntent.Kind, "stabilize", StringComparison.Ordinal) &&
            string.Equals(
                actualIntent.DeclaredOperationFingerprint,
                expectedIntent.DeclaredOperationFingerprint,
                StringComparison.Ordinal) &&
            string.Equals(
                actualIntent.IntentFingerprint,
                expectedIntent.IntentFingerprint,
                StringComparison.Ordinal);
        var failedAxes = new List<string>();
        if (!acceptedState.HasCurrentAdmissionAuthority())
            failedAxes.Add("accepted_state");
        if (!exactRequest)
            failedAxes.Add("request");
        if (!exactCoordinates)
            failedAxes.Add("coordinates");
        if (request.Mode is not "guaranteed" || resolution.Mode is not "guaranteed")
            failedAxes.Add("mode");
        if (resolution.AttemptDisposition is not "AcceptedTerminal")
            failedAxes.Add("attempt_disposition");
        if (resolution.ResultCategory is not "success" ||
            resolution.SelectedOutcomeIndex != 0 || resolution.Interruption)
        {
            failedAxes.Add("result_selection");
        }
        if (!exactOutcome)
            failedAxes.Add("outcome");
        if (resolution.CriticalReactionIntent is not null)
            failedAxes.Add("critical_reaction");
        if (resolution.CourseId is not null ||
            resolution.CourseMilestoneOrdinal is not null ||
            resolution.CourseDisposition is not null)
        {
            failedAxes.Add("course");
        }
        if (resolution.RouteCompletion is not "AppendOnce")
            failedAxes.Add("route_completion");
        if (!string.Equals(
                request.RouteSourceWoundFingerprint,
                coordinates.ExpectedBeforeFingerprint,
                StringComparison.Ordinal))
        {
            failedAxes.Add("before_wound");
        }
        if (finalized is null || !exactFinalization || !finalizationShape)
            failedAxes.Add("resource_finalization");
        if (failedAxes.Count != 0)
        {
            issues.Add(PublicationIssue(
                "treatmentPublication.guaranteedStabilization",
                "mortal_wound_treatment_publication_slice_unsupported",
                "one fresh accepted-terminal guaranteed singleton stabilization that preserves its sealed source graph, has no course or reaction, and has one valid resource finalization",
                string.Join(",", failedAxes)));
        }
        return new GuaranteedStabilizationShellValidation(
            issues.Count == 0 ? finalized : null,
            issues);
    }

    private static string ComputeTreatmentPublicationFingerprint(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.guaranteed_stabilization_publication",
            "1",
            acceptedState.AcceptedStateFingerprint,
            acceptedState.BindingFingerprint,
            request.RequestFingerprint,
            request.Coordinates.OperationKey,
            request.Coordinates.AttemptId,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint
        });

    private static MortalWoundTreatmentPublicationResult PublicationFailure(
        string code,
        string expected,
        string actual) => MortalWoundTreatmentPublicationResult.Invalid(
        new[]
        {
            PublicationIssue(
                "treatmentPublication",
                code,
                expected,
                actual)
        });

    private static ValidationIssue PublicationIssue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "The guaranteed Mortal wound stabilization could not be published.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
            "Reuse the exact sealed accepted terminal result while its canonical authority remains current.");
}
