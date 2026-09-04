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
        private readonly MortalWoundTreatmentOutcomePreparation _outcomePreparation;
        private readonly JsonObject _expectedEffectCommandRoot;
        private readonly JsonObject[] _expectedEffectLifecycleEvents;
        private readonly JsonObject[] _criticalReactionLifecycleEvents;
        private readonly MortalWoundTreatmentRematerializationAuthority?
            _rematerializationAuthority;

        internal TreatmentContinuationAuthority(
            WoundMaterializationEnvelope before,
            MortalWoundTreatmentOutcomePreparation outcomePreparation,
            MortalWoundTreatmentAcceptedStateAuthority acceptedStateAuthority,
            MortalWoundTreatmentAttemptRequest requestAuthority,
            MortalWoundTreatmentResolution resolution,
            MortalWoundTreatmentResourceFinalization resourceFinalization,
            JsonObject expectedEffectCommandRoot,
            IReadOnlyList<JsonObject> expectedEffectLifecycleEvents,
            IReadOnlyList<JsonObject> criticalReactionLifecycleEvents,
            string criticalReactionPublicationFingerprint,
            string transitionId,
            string outcomePreparationFingerprint,
            MortalWoundTreatmentPersistedResult persistedResult,
            string acceptedStateFingerprint,
            string baseSemanticFingerprint,
            string semanticFingerprint,
            object reservationAuthority,
            MortalTreatmentItemCommandEnvelope itemCommandEnvelope,
            object skillProjectionAuthority,
            MortalWoundTreatmentRematerializationAuthority?
                rematerializationAuthority,
            string fingerprint)
        {
            Before = before;
            _outcomePreparation = outcomePreparation.DetachedCopy();
            AcceptedStateAuthority = acceptedStateAuthority;
            RequestAuthority = requestAuthority;
            Resolution = resolution;
            ResourceFinalization = resourceFinalization;
            _expectedEffectCommandRoot = expectedEffectCommandRoot
                .DeepClone()
                .AsObject();
            _expectedEffectLifecycleEvents = expectedEffectLifecycleEvents
                .Select(static value => value.DeepClone().AsObject())
                .ToArray();
            _criticalReactionLifecycleEvents = criticalReactionLifecycleEvents
                .Select(static value => value.DeepClone().AsObject())
                .ToArray();
            CriticalReactionPublicationFingerprint =
                criticalReactionPublicationFingerprint;
            TransitionId = transitionId;
            OutcomePreparationFingerprint = outcomePreparationFingerprint;
            PersistedResult = persistedResult;
            AcceptedStateFingerprint = acceptedStateFingerprint;
            BaseSemanticFingerprint = baseSemanticFingerprint;
            SemanticFingerprint = semanticFingerprint;
            ReservationAuthority = reservationAuthority;
            ItemCommandEnvelope = itemCommandEnvelope;
            SkillProjectionAuthority = skillProjectionAuthority;
            _rematerializationAuthority = rematerializationAuthority is null
                ? null
                : rematerializationAuthority with { };
            Fingerprint = fingerprint;
        }

        internal WoundMaterializationEnvelope Before { get; }
        internal MortalWoundTreatmentOutcomePreparation OutcomePreparation =>
            _outcomePreparation.DetachedCopy();
        internal MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority { get; }
        internal MortalWoundTreatmentAttemptRequest RequestAuthority { get; }
        internal MortalWoundTreatmentResolution Resolution { get; }
        internal MortalWoundTreatmentResourceFinalization ResourceFinalization { get; }
        internal JsonObject ExpectedEffectCommandRoot =>
            _expectedEffectCommandRoot.DeepClone().AsObject();
        internal IReadOnlyList<JsonObject> ExpectedEffectLifecycleEvents =>
            Array.AsReadOnly(_expectedEffectLifecycleEvents
                .Select(static value => value.DeepClone().AsObject())
                .ToArray());
        internal IReadOnlyList<JsonObject> CriticalReactionLifecycleEvents =>
            Array.AsReadOnly(_criticalReactionLifecycleEvents
                .Select(static value => value.DeepClone().AsObject())
                .ToArray());
        internal string CriticalReactionPublicationFingerprint { get; }
        internal string TransitionId { get; }
        internal string OutcomePreparationFingerprint { get; }
        internal MortalWoundTreatmentPersistedResult PersistedResult { get; }
        internal string AcceptedStateFingerprint { get; }
        internal string BaseSemanticFingerprint { get; }
        internal string SemanticFingerprint { get; }
        internal object ReservationAuthority { get; }
        internal MortalTreatmentItemCommandEnvelope ItemCommandEnvelope { get; }
        internal object SkillProjectionAuthority { get; }
        internal MortalWoundTreatmentRematerializationAuthority?
            RematerializationAuthority => _rematerializationAuthority is null
                ? null
                : _rematerializationAuthority with { };
        internal string Fingerprint { get; }
    }

    internal sealed record TreatmentContinuationView(
        WoundMaterializationEnvelope Before,
        MortalWoundTreatmentOutcomePreparation OutcomePreparation,
        MortalWoundTreatmentAcceptedStateAuthority AcceptedStateAuthority,
        MortalWoundTreatmentAttemptRequest RequestAuthority,
        MortalWoundTreatmentResolution Resolution,
        MortalWoundTreatmentResourceFinalization ResourceFinalization,
        JsonObject ExpectedEffectCommandRoot,
        IReadOnlyList<JsonObject> ExpectedEffectLifecycleEvents,
        IReadOnlyList<JsonObject> CriticalReactionLifecycleEvents,
        string CriticalReactionPublicationFingerprint,
        string TransitionId,
        string OutcomePreparationFingerprint,
        MortalWoundTreatmentPersistedResult PersistedResult,
        string AcceptedStateFingerprint,
        string BaseSemanticFingerprint,
        string SemanticFingerprint,
        object ReservationAuthority,
        MortalTreatmentItemCommandEnvelope ItemCommandEnvelope,
        object SkillProjectionAuthority,
        MortalWoundTreatmentRematerializationAuthority?
            RematerializationAuthority,
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

        var hasCrossSurfaceFateDuplicate =
            resolution.CriticalReactionIntent is not null &&
            response.EffectEventReports is not null;
        if (hasCrossSurfaceFateDuplicate &&
            request.HasNewProvisionalClaimCleanup)
        {
            request.RollbackNewProvisionalClaims(acceptedState);
            return CrossSurfaceFateDuplicateFailure();
        }

        var frozenCommands = FreezeTreatmentSkillCommands(
            response,
            ignoreLegacyFateDuplicate: hasCrossSurfaceFateDuplicate);
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
        var outcomePreparationResult =
            MortalWoundTreatmentOutcomePublicationPlanner.Prepare(
                acceptedState,
                request,
                resolution,
                acceptedState.CurrentGameMinute);
        if (!outcomePreparationResult.IsValid)
        {
            return MortalWoundTreatmentPublicationResult.Invalid(
                outcomePreparationResult.Issues);
        }
        var preparation = outcomePreparationResult.Preparation!;
        var baseSemanticFingerprint = ComputeTreatmentPublicationFingerprint(
            acceptedState,
            request,
            resolution,
            preparation.Fingerprint);
        var semanticFingerprint = ComputeTreatmentPublicationEnvelopeFingerprint(
            baseSemanticFingerprint,
            commandEnvelope,
            itemCommandEnvelope);
        var skillSemanticFingerprint = ComputeTreatmentSkillSemanticFingerprint(
            commandEnvelope);
        PublicationBaselines baselines;
        try
        {
            baselines = ReadPublicationBaselines(fileSystem, writeLease);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                InvalidDataException or IOException or ObjectDisposedException or
                UnauthorizedAccessException or System.Text.Json.JsonException or
                NullReferenceException or OverflowException)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_invalid",
                "one complete sealed treatment publication",
                exception.GetType().Name);
        }
        if (baselines.Issues.Count != 0)
        {
            return MortalWoundTreatmentPublicationResult.Invalid(
                baselines.Issues);
        }
        var input = new WoundAcceptedTurnInput(
            acceptedState.Binding,
            Array.Empty<WoundOpportunityAuthority>(),
            Array.Empty<WoundAcceptedTransitionDraft>(),
            baselines.WoundCarriers!,
            baselines.WoundIdentity!,
            baselines.WoundHistory!,
            baselines.EffectCarriers,
            baselines.EffectIdentity);
        var rematerialization =
            MortalWoundTreatmentSeverityRematerializationPlanner.Prepare(
                input,
                preparation,
                request.RequestFingerprint,
                resolution.ResolutionAuthorityFingerprint,
                resolution.ResultFingerprint,
                request.Coordinates.AttemptId,
                request.Coordinates.OperationKey,
                WoundIdentityState.ComputeSemanticFingerprint(
                    request.RouteSourceWound));
        if (!rematerialization.IsValid)
        {
            return MortalWoundTreatmentPublicationResult.Invalid(
                rematerialization.Issues);
        }
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
                "the exact first sealed treatment semantics",
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
                return hasCrossSurfaceFateDuplicate
                    ? CrossSurfaceFateDuplicateFailure()
                    : MortalWoundTreatmentPublicationResult.Valid(cached.Plan);
            }
            return PublicationFailure(
                "mortal_wound_treatment_publication_cache_missing",
                "the original exact validated common plan",
                "publication fingerprint exists without its plan");
        }

        try
        {
            var shell = ValidateTreatmentPublicationShell(
                acceptedState,
                request,
                resolution);
            if (shell.Issues.Count != 0 || shell.Finalization is null)
                return MortalWoundTreatmentPublicationResult.Invalid(shell.Issues);

            var criticalReactionPublication =
                MortalWoundCriticalReactionPublicationPlanner.Compose(
                    acceptedState,
                    request,
                    resolution);
            if (!criticalReactionPublication.IsValid)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    criticalReactionPublication.Issues);
            }

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

            var transitionId = preparation.TransitionId;
            var expectedEffectCommandRoot =
                EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot();
            var expectedEffectLifecycleEvents =
                CreateExpectedTreatmentEffectLifecycleEvents(
                    acceptedState,
                    expectedEffectCommandRoot,
                    baselines.EffectCarriers!,
                    criticalReactionPublication.LifecycleEvents);
            var continuationAuthority = CreateTreatmentContinuationAuthority(
                request.RouteSourceWound,
                preparation,
                request,
                resolution,
                shell.Finalization,
                expectedEffectCommandRoot,
                expectedEffectLifecycleEvents,
                criticalReactionPublication.LifecycleEvents,
                criticalReactionPublication.Fingerprint!,
                transitionId,
                preparation.Fingerprint,
                acceptedState,
                baseSemanticFingerprint,
                semanticFingerprint,
                reservation.Authority!,
                itemCommandEnvelope,
                skillProjection.Authority,
                rematerialization.Authority);
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
                expectedEffectCommandRoot,
                baselines.EffectCarriers!,
                baselines.EffectCarriers!,
                baselines.EffectIdentity,
                baselines.SourceRoots!,
                currentWorldTime: acceptedState.CurrentGameMinute,
                publicationCarrierBaselines: baselines.EffectCarriers,
                realm: acceptedState.Binding.Realm,
                acceptedReportedLifecycleEvents:
                    criticalReactionPublication.LifecycleEvents,
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
            if (!commonResult.Success || commonResult.Plan is null)
            {
                return MortalWoundTreatmentPublicationResult.Invalid(
                    commonResult.Issues);
            }
            return hasCrossSurfaceFateDuplicate
                ? CrossSurfaceFateDuplicateFailure()
                : MortalWoundTreatmentPublicationResult.Valid(commonResult.Plan);
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                InvalidDataException or IOException or ObjectDisposedException or
                UnauthorizedAccessException or System.Text.Json.JsonException or
                NullReferenceException or OverflowException)
        {
            return PublicationFailure(
                "mortal_wound_treatment_publication_invalid",
                "one complete sealed treatment publication",
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

    private static MortalWoundTreatmentPublicationResult
        CrossSurfaceFateDuplicateFailure() => PublicationFailure(
            "wound_treatment_fate_reaction_cross_surface_duplicate",
            "the sealed typed Fate reaction as the only reaction authority",
            "effectEventReports was also supplied");

    private static object CreateTreatmentContinuationAuthority(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentOutcomePreparation outcomePreparation,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentResourceFinalization resourceFinalization,
        JsonObject expectedEffectCommandRoot,
        IReadOnlyList<JsonObject> expectedEffectLifecycleEvents,
        IReadOnlyList<JsonObject> criticalReactionLifecycleEvents,
        string criticalReactionPublicationFingerprint,
        string transitionId,
        string outcomePreparationFingerprint,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        string baseSemanticFingerprint,
        string semanticFingerprint,
        object reservationAuthority,
        MortalTreatmentItemCommandEnvelope itemCommandEnvelope,
        object skillProjectionAuthority,
        MortalWoundTreatmentRematerializationAuthority?
            rematerializationAuthority)
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
            outcomePreparation,
            resolution,
            resourceFinalization.FinalizationFingerprint,
            expectedEffectCommandRoot,
            expectedEffectLifecycleEvents,
            criticalReactionPublicationFingerprint,
            transitionId,
            outcomePreparationFingerprint,
            acceptedState.AcceptedStateFingerprint,
            baseSemanticFingerprint,
            semanticFingerprint,
            itemCommandEnvelope.Fingerprint,
            skillProjection.Fingerprint,
            rematerializationAuthority?.AuthoritySeal);
        return new TreatmentContinuationAuthority(
            before,
            outcomePreparation,
            acceptedState,
            request,
            resolution,
            resourceFinalization,
            expectedEffectCommandRoot,
            expectedEffectLifecycleEvents,
            criticalReactionLifecycleEvents,
            criticalReactionPublicationFingerprint,
            transitionId,
            outcomePreparationFingerprint,
            persisted,
            acceptedState.AcceptedStateFingerprint,
            baseSemanticFingerprint,
            semanticFingerprint,
            reservationAuthority,
            itemCommandEnvelope,
            skillProjectionAuthority,
            rematerializationAuthority,
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
            string.IsNullOrWhiteSpace(
                candidate.CriticalReactionPublicationFingerprint) ||
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
            !string.Equals(
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                    candidate.ExpectedEffectCommandRoot),
                WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                    EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot()),
                StringComparison.Ordinal) ||
            !TryReadTreatmentSkillProjectionForEnvelopeSemantics(
                candidate.SkillProjectionAuthority,
                candidate.ReservationAuthority,
                out var skillProjection))
            return false;
        var outcomePreparation = candidate.OutcomePreparation;
        if (!outcomePreparation.AgreesWith(candidate.Resolution) ||
            !string.Equals(
                WoundMaterializationContract.SerializeCanonical(candidate.Before),
                WoundMaterializationContract.SerializeCanonical(
                    outcomePreparation.Before),
                StringComparison.Ordinal) ||
            !string.Equals(
                outcomePreparation.TransitionId,
                candidate.TransitionId,
                StringComparison.Ordinal) ||
            !string.Equals(
                outcomePreparation.Fingerprint,
                candidate.OutcomePreparationFingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }
        if (!MortalWoundCriticalReactionPublicationPlanner.HasMatchingFingerprint(
                candidate.RequestAuthority,
                candidate.Resolution,
                candidate.CriticalReactionLifecycleEvents,
                candidate.CriticalReactionPublicationFingerprint))
        {
            return false;
        }
        var fingerprint = ComputeTreatmentContinuationFingerprint(
            candidate.Before,
            outcomePreparation,
            candidate.Resolution,
            candidate.ResourceFinalization.FinalizationFingerprint,
            candidate.ExpectedEffectCommandRoot,
            candidate.ExpectedEffectLifecycleEvents,
            candidate.CriticalReactionPublicationFingerprint,
            candidate.TransitionId,
            candidate.OutcomePreparationFingerprint,
            candidate.AcceptedStateFingerprint,
            candidate.BaseSemanticFingerprint,
            candidate.SemanticFingerprint,
            candidate.ItemCommandEnvelope.Fingerprint,
            skillProjection.Fingerprint,
            candidate.RematerializationAuthority?.AuthoritySeal);
        if (!string.Equals(
                fingerprint,
                candidate.Fingerprint,
                StringComparison.Ordinal))
        {
            return false;
        }
        continuation = new TreatmentContinuationView(
            candidate.Before,
            outcomePreparation,
            candidate.AcceptedStateAuthority,
            candidate.RequestAuthority,
            candidate.Resolution,
            candidate.ResourceFinalization,
            candidate.ExpectedEffectCommandRoot,
            candidate.ExpectedEffectLifecycleEvents,
            candidate.CriticalReactionLifecycleEvents,
            candidate.CriticalReactionPublicationFingerprint,
            candidate.TransitionId,
            candidate.OutcomePreparationFingerprint,
            candidate.PersistedResult,
            candidate.AcceptedStateFingerprint,
            candidate.BaseSemanticFingerprint,
            candidate.SemanticFingerprint,
            candidate.ReservationAuthority,
            candidate.ItemCommandEnvelope,
            candidate.SkillProjectionAuthority,
            candidate.RematerializationAuthority,
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
        var hasReduction = continuation.OutcomePreparation.SeverityReduction is not null;
        var rematerialization = continuation.RematerializationAuthority;
        var batches = prepared.EffectOperationBatches;
        var batchAgrees = !hasReduction
            ? rematerialization is null && batches.Count == 0
            : rematerialization is not null &&
              batches.Count == 1 &&
              string.Equals(
                  rematerialization.PreparedInputFingerprint,
                  prepared.InputFingerprint,
                  StringComparison.Ordinal) &&
              string.Equals(
                  WoundAcceptedTurnFingerprints.ComputeSourceExport(batches[0]),
                  rematerialization.SourceExportFingerprint,
                  StringComparison.Ordinal) &&
              string.Equals(
                  batches[0].SourceExportFingerprint,
                  rematerialization.SourceExportFingerprint,
                  StringComparison.Ordinal) &&
              string.Equals(
                  MortalWoundTreatmentSeverityRematerializationPlanner
                      .ComputeBatchTopologyFingerprint(batches[0]),
                  rematerialization.BatchTopologyFingerprint,
                  StringComparison.Ordinal);
        return prepared.AllocatedWoundIds.Count == 1 &&
               prepared.AllocatedTransitionIds.Count == 1 &&
               prepared.PreparedWounds.Count == 1 &&
               batchAgrees &&
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
                       continuation.OutcomePreparation.ProvisionalAfter),
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

    internal static bool TreatmentContinuationEffectInputAgrees(
        object authority,
        EffectAcceptedTurnInput input)
    {
        ArgumentNullException.ThrowIfNull(authority);
        ArgumentNullException.ThrowIfNull(input);
        if (!TryReadTreatmentContinuation(authority, out var continuation) ||
            !string.Equals(
                input.SessionId,
                continuation.Resolution.Coordinates.SessionId,
                StringComparison.Ordinal) ||
            !string.Equals(
                input.SnapshotToken,
                continuation.Resolution.Coordinates.SnapshotToken,
                StringComparison.Ordinal) ||
            !string.Equals(
                input.Realm,
                continuation.Resolution.Coordinates.Realm,
                StringComparison.Ordinal))
        {
            return false;
        }

        var expectedLifecycle = CreateLifecycleArray(
            continuation.ExpectedEffectLifecycleEvents);
        return input.EventInput["lifecycleEvents"] is JsonArray actual &&
               string.Equals(
                   WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                       continuation.ExpectedEffectCommandRoot),
                   WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                       input.RawCommands),
                   StringComparison.Ordinal) &&
               string.Equals(
                   WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                       expectedLifecycle),
                   WoundAcceptedTurnFingerprintWriter.CanonicalJson(actual),
                   StringComparison.Ordinal);
    }

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
            !TreatmentContinuationPreparedAgrees(bundle.PreparedPlan) ||
            !TreatmentContinuationEffectInputAgrees(
                authority,
                bundle.EffectBatchPlan.EffectInput))
        {
            return false;
        }
        var recomputedBaseSemantic = ComputeTreatmentPublicationFingerprint(
            acceptedState,
            request,
            resolution,
            continuation.OutcomePreparationFingerprint);
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
        var rematerialization =
            MortalWoundTreatmentSeverityRematerializationPlanner.Prepare(
                input,
                continuation.OutcomePreparation,
                continuation.Resolution.RequestFingerprint,
                continuation.Resolution.ResolutionAuthorityFingerprint,
                continuation.Resolution.ResultFingerprint,
                continuation.Resolution.Coordinates.AttemptId,
                continuation.Resolution.Coordinates.OperationKey,
                WoundIdentityState.ComputeSemanticFingerprint(
                    continuation.Before));
        if (!rematerialization.IsValid ||
            rematerialization.Authority !=
                continuation.RematerializationAuthority)
        {
            return new WoundAcceptedTurnPreparationResult(
                null,
                rematerialization.Issues.Count == 0
                    ? new[]
                    {
                        WoundAcceptedTurnPlannerCore.NewIssue(
                            "wound_plan_treatment_continuation_invalid",
                            "Treatment continuation must own the independently recomputed rematerialization seal.",
                            "matching private rematerialization authority",
                            "changed authority")
                    }
                    : rematerialization.Issues);
        }
        var operationBatches = rematerialization.Batch is null
            ? Array.Empty<WoundEffectOperationBatch>()
            : new[] { rematerialization.Batch };
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
            new[] { continuation.OutcomePreparation.ProvisionalAfter },
            operationBatches,
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
                new[] { continuation.OutcomePreparation.ProvisionalAfter },
                operationBatches,
                baseline,
                authority),
            Array.Empty<ValidationIssue>());
    }

    private static string ComputeTreatmentContinuationFingerprint(
        WoundMaterializationEnvelope before,
        MortalWoundTreatmentOutcomePreparation outcomePreparation,
        MortalWoundTreatmentResolution resolution,
        string resourceFinalizationFingerprint,
        JsonObject expectedEffectCommandRoot,
        IReadOnlyList<JsonObject> expectedEffectLifecycleEvents,
        string criticalReactionPublicationFingerprint,
        string transitionId,
        string outcomePreparationFingerprint,
        string acceptedStateFingerprint,
        string baseSemanticFingerprint,
        string semanticFingerprint,
        string itemCommandEnvelopeFingerprint,
        string skillProjectionFingerprint,
        string? rematerializationAuthoritySeal) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.wound.treatment_continuation_authority",
            "6",
            WoundMaterializationContract.SerializeCanonical(before),
            WoundMaterializationContract.SerializeCanonical(
                outcomePreparation.ProvisionalAfter),
            resolution.RequestFingerprint,
            resolution.ResultFingerprint,
            resolution.ResolutionAuthorityFingerprint,
            resourceFinalizationFingerprint,
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                expectedEffectCommandRoot),
            WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                CreateLifecycleArray(expectedEffectLifecycleEvents)),
            criticalReactionPublicationFingerprint,
            resolution.Coordinates.CoordinatesFingerprint,
            transitionId,
            outcomePreparationFingerprint,
            acceptedStateFingerprint,
            baseSemanticFingerprint,
            semanticFingerprint,
            itemCommandEnvelopeFingerprint,
            skillProjectionFingerprint,
            rematerializationAuthoritySeal,
            TreatmentPublicationSummary
        });

    private static IReadOnlyList<JsonObject>
        CreateExpectedTreatmentEffectLifecycleEvents(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            JsonObject expectedEffectCommandRoot,
            EffectCarrierCatalogInput effectCarriers,
            IReadOnlyList<JsonObject> criticalReactionLifecycleEvents)
    {
        var eventInput = EffectAcceptedTurnInputComposer.BuildAcceptedEventInput(
            acceptedState.Binding.Turn,
            expectedEffectCommandRoot,
            acceptedState.CurrentGameMinute,
            effectCarriers,
            effectCarriers,
            acceptedState.Binding.Realm,
            criticalReactionLifecycleEvents);
        if (eventInput["lifecycleEvents"] is not JsonArray lifecycleEvents ||
            lifecycleEvents.Any(static value => value is not JsonObject))
        {
            throw new InvalidOperationException(
                "Treatment continuation requires one complete canonical effect lifecycle array.");
        }
        return Array.AsReadOnly(lifecycleEvents
            .Select(static value => value!.DeepClone().AsObject())
            .ToArray());
    }

    private static JsonArray CreateLifecycleArray(
        IReadOnlyList<JsonObject> lifecycleEvents) =>
        new(lifecycleEvents
            .Select(static value => (JsonNode)value.DeepClone())
            .ToArray());

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

    private sealed record TreatmentPublicationShellValidation(
        MortalWoundTreatmentResourceFinalization? Finalization,
        IReadOnlyList<ValidationIssue> Issues);

    private static TreatmentPublicationShellValidation
        ValidateTreatmentPublicationShell(
            MortalWoundTreatmentAcceptedStateAuthority acceptedState,
            MortalWoundTreatmentAttemptRequest request,
            MortalWoundTreatmentResolution resolution)
    {
        var issues = new List<ValidationIssue>();
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
        if (!finalization.IsValid || finalization.Finalization is null)
            issues.AddRange(finalization.Issues);
        var finalized = finalization.Finalization;
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
            return new TreatmentPublicationShellValidation(
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
        var failedAxes = new List<string>();
        if (!acceptedState.HasCurrentAdmissionAuthority())
            failedAxes.Add("accepted_state");
        if (!exactRequest)
            failedAxes.Add("request");
        if (finalized is null || !exactFinalization || !finalizationShape)
            failedAxes.Add("resource_finalization");
        if (failedAxes.Count != 0)
        {
            issues.Add(PublicationIssue(
                "treatmentPublication.resources",
                "mortal_wound_treatment_publication_resource_finalization_mismatch",
                "one exact resource finalization recomposed from the current sealed treatment result",
                string.Join(",", failedAxes)));
        }
        return new TreatmentPublicationShellValidation(
            issues.Count == 0 ? finalized : null,
            issues);
    }

    private static string ComputeTreatmentPublicationFingerprint(
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        string outcomePreparationFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.publication",
            "3",
            acceptedState.AcceptedStateFingerprint,
            acceptedState.BindingFingerprint,
            request.RequestFingerprint,
            request.Coordinates.OperationKey,
            request.Coordinates.AttemptId,
            resolution.ResolutionAuthorityFingerprint,
            resolution.ResultFingerprint,
            outcomePreparationFingerprint
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
        "The accepted Mortal wound treatment could not be published.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint:
            "Reuse the exact sealed accepted terminal result while its canonical authority remains current.");
}
