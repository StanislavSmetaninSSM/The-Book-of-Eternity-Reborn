using System.Collections.ObjectModel;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum MortalWoundTreatmentPublicationTransactionOutcome
{
    PublishedAgreementAdvanced,
    Finalized,
    Rearmed,
    HeldBlocked,
    CommandQuarantined,
    Released,
    ReleaseFailed,
    TokenMismatch,
    TokenReplayed,
    Stale,
    ReservationChanged,
    PublishedAgreementChanged
}

internal sealed class MortalWoundTreatmentPublicationOperationResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    internal MortalWoundTreatmentPublicationOperationResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        int changedCount,
        MortalWoundTreatmentPublicationTransactionOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(issues);
        IsValid = isValid;
        _issues = Array.AsReadOnly(issues.ToArray());
        ChangedCount = changedCount;
        Outcome = outcome;
    }

    internal bool IsValid { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal int ChangedCount { get; }
    internal MortalWoundTreatmentPublicationTransactionOutcome Outcome { get; }
}

internal sealed class MortalWoundTreatmentPublicationProbeResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;

    internal MortalWoundTreatmentPublicationProbeResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        MortalWoundTreatmentResourceReservationState? state,
        string? operationKey,
        string? attemptId,
        string? requestFingerprint,
        string? resourceAuthorityFingerprint,
        string? finalizationFingerprint,
        string? agreementFingerprint,
        string? sessionGeneration,
        long? sessionGenerationRevision,
        string? generationFingerprint)
    {
        ArgumentNullException.ThrowIfNull(issues);
        IsValid = isValid;
        _issues = Array.AsReadOnly(issues.ToArray());
        State = state;
        OperationKey = operationKey;
        AttemptId = attemptId;
        RequestFingerprint = requestFingerprint;
        ResourceAuthorityFingerprint = resourceAuthorityFingerprint;
        FinalizationFingerprint = finalizationFingerprint;
        AgreementFingerprint = agreementFingerprint;
        SessionGeneration = sessionGeneration;
        SessionGenerationRevision = sessionGenerationRevision;
        GenerationFingerprint = generationFingerprint;
    }

    internal bool IsValid { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal int ChangedCount => 0;
    internal MortalWoundTreatmentResourceReservationState? State { get; }
    internal string? OperationKey { get; }
    internal string? AttemptId { get; }
    internal string? RequestFingerprint { get; }
    internal string? ResourceAuthorityFingerprint { get; }
    internal string? FinalizationFingerprint { get; }
    internal string? AgreementFingerprint { get; }
    internal string? SessionGeneration { get; }
    internal long? SessionGenerationRevision { get; }
    internal string? GenerationFingerprint { get; }
}

internal sealed class MortalWoundProcedurePublicationClaimProof
{
    private readonly MortalWoundProcedureCheckAuthority _procedureAuthority;

    private MortalWoundProcedurePublicationClaimProof(
        object mintCapability,
        MortalWoundProcedureCheckAuthority procedureAuthority,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement)
    {
        if (!AcceptedTurnAuthorityRegistry
                .IsTreatmentPublicationTransactionCapability(mintCapability))
        {
            throw new InvalidOperationException(
                "Only the accepted-turn registry may seal procedure publication claims.");
        }
        ArgumentNullException.ThrowIfNull(procedureAuthority);
        ArgumentNullException.ThrowIfNull(diceReservation);
        if (!ReferenceEquals(
                criticalReactionAgreement?.Reservation,
                criticalReactionReservation) ||
            criticalReactionAgreement is not null &&
            !criticalReactionAgreement.Agrees(diceReservation))
        {
            throw new InvalidOperationException(
                "The procedure Fate reservation must match its exact dice agreement.");
        }

        _procedureAuthority = procedureAuthority;
        DiceReservation = diceReservation;
        CriticalReactionReservation = criticalReactionReservation;
        CriticalReactionAgreement = criticalReactionAgreement;
        Fingerprint = ComputeFingerprint(
            procedureAuthority,
            diceReservation,
            criticalReactionReservation,
            criticalReactionAgreement);
    }

    internal MortalWoundProcedureDiceReservation DiceReservation { get; }
    internal MortalWoundCriticalReactionReservation? CriticalReactionReservation { get; }
    internal MortalWoundCriticalReactionReservationAgreement?
        CriticalReactionAgreement { get; }
    internal string Fingerprint { get; }

    internal static MortalWoundProcedurePublicationClaimProof Mint(
        object mintCapability,
        MortalWoundProcedureCheckAuthority procedureAuthority,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement) => new(
        mintCapability,
        procedureAuthority,
        diceReservation,
        criticalReactionReservation,
        criticalReactionAgreement);

    internal bool MatchesAuthority(
        MortalWoundProcedureCheckAuthority procedureAuthority) =>
        ReferenceEquals(_procedureAuthority, procedureAuthority) &&
        string.Equals(
            Fingerprint,
            ComputeFingerprint(
                _procedureAuthority,
                DiceReservation,
                CriticalReactionReservation,
                CriticalReactionAgreement),
            StringComparison.Ordinal);

    private static string ComputeFingerprint(
        MortalWoundProcedureCheckAuthority procedureAuthority,
        MortalWoundProcedureDiceReservation diceReservation,
        MortalWoundCriticalReactionReservation? criticalReactionReservation,
        MortalWoundCriticalReactionReservationAgreement? criticalReactionAgreement) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.procedure_publication_claim",
            "1",
            procedureAuthority.AuthorityFingerprint,
            diceReservation.ClaimFingerprint,
            diceReservation.OperationKey,
            diceReservation.AttemptId,
            diceReservation.CoordinatesFingerprint,
            diceReservation.AcceptedStateFingerprint,
            criticalReactionAgreement is null
                ? "absent"
                : criticalReactionReservation is null
                    ? "empty"
                    : "claimed",
            criticalReactionReservation?.ClaimFingerprint,
            criticalReactionAgreement?.OperationKey,
            criticalReactionAgreement?.AttemptId,
            criticalReactionAgreement?.CoordinatesFingerprint,
            criticalReactionAgreement?.AcceptedStateFingerprint
        });
}

internal sealed class MortalWoundTreatmentPublicationTakeReceipt
{
    private int _consumed;

    private MortalWoundTreatmentPublicationTakeReceipt(
        FileSystemManager fileSystem,
        object authorityStateToken,
        string sessionGeneration,
        long sessionGenerationRevision,
        AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot cacheSnapshot,
        MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot
            mortalItemCacheSnapshot,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceFinalization finalization,
        MortalWoundTreatmentResourcePublicationAuthority publicationAuthority,
        MortalWoundProcedurePublicationClaimProof? procedureClaimProof,
        string semanticFingerprint,
        string generationFingerprint)
    {
        FileSystem = fileSystem;
        AuthorityStateToken = authorityStateToken;
        SessionGeneration = sessionGeneration;
        SessionGenerationRevision = sessionGenerationRevision;
        CacheSnapshot = cacheSnapshot;
        MortalItemCacheSnapshot = mortalItemCacheSnapshot;
        AcceptedState = acceptedState;
        Request = request;
        Finalization = finalization;
        PublicationAuthority = publicationAuthority;
        ProcedureClaimProof = procedureClaimProof;
        SemanticFingerprint = semanticFingerprint;
        GenerationFingerprint = generationFingerprint;
    }

    internal FileSystemManager FileSystem { get; }
    internal object AuthorityStateToken { get; }
    internal string SessionGeneration { get; }
    internal long SessionGenerationRevision { get; }
    internal AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot CacheSnapshot { get; }
    internal MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot
        MortalItemCacheSnapshot { get; }
    internal AcceptedMechanicsPlanningResult Result => CacheSnapshot.Result;
    internal AcceptedMechanicsPlan Plan => CacheSnapshot.Plan;
    internal AcceptedMechanicsPlanBinding Binding => CacheSnapshot.Binding;
    internal MortalWoundTreatmentAcceptedStateAuthority AcceptedState { get; }
    internal MortalWoundTreatmentAttemptRequest Request { get; }
    internal MortalWoundTreatmentResourceFinalization Finalization { get; }
    internal MortalWoundTreatmentResourcePublicationAuthority PublicationAuthority { get; }
    internal MortalWoundProcedurePublicationClaimProof? ProcedureClaimProof { get; }
    internal string SemanticFingerprint { get; }
    internal string GenerationFingerprint { get; }
    internal bool IsTerminalReleaseOnly => MortalItemCacheSnapshot.TerminalReleaseOnly;
    internal bool IsConsumed => Volatile.Read(ref _consumed) != 0;
    internal bool HasValidSeal() =>
        PublicationAuthority.RequiresProcedureSettlement ==
            (ProcedureClaimProof is not null) &&
        (ProcedureClaimProof is null ||
         Request.ModeAuthority is MortalWoundProcedureCheckAuthority procedure &&
         ProcedureClaimProof.MatchesAuthority(procedure)) &&
        string.Equals(
            GenerationFingerprint,
            ComputeGenerationFingerprint(
                SessionGeneration,
                SessionGenerationRevision,
                CacheSnapshot,
                MortalItemCacheSnapshot,
                PublicationAuthority,
                ProcedureClaimProof,
                Request,
                Finalization,
                SemanticFingerprint),
            StringComparison.Ordinal);

    internal static MortalWoundTreatmentPublicationTakeReceipt Mint(
        object mintCapability,
        FileSystemManager fileSystem,
        object authorityStateToken,
        string sessionGeneration,
        long sessionGenerationRevision,
        AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot cacheSnapshot,
        MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot
            mortalItemCacheSnapshot,
        MortalWoundTreatmentAcceptedStateAuthority acceptedState,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceFinalization finalization,
        MortalWoundTreatmentResourcePublicationAuthority publicationAuthority,
        MortalWoundProcedurePublicationClaimProof? procedureClaimProof,
        string semanticFingerprint)
    {
        if (!AcceptedTurnAuthorityRegistry
                .IsTreatmentPublicationTransactionCapability(mintCapability))
        {
            throw new InvalidOperationException(
                "Only the accepted-turn authority registry may mint treatment publication receipts.");
        }
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(authorityStateToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionGeneration);
        ArgumentNullException.ThrowIfNull(cacheSnapshot);
        ArgumentNullException.ThrowIfNull(mortalItemCacheSnapshot);
        ArgumentNullException.ThrowIfNull(acceptedState);
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(finalization);
        ArgumentNullException.ThrowIfNull(publicationAuthority);
        ArgumentException.ThrowIfNullOrWhiteSpace(semanticFingerprint);
        if (publicationAuthority.RequiresProcedureSettlement !=
            (procedureClaimProof is not null))
        {
            throw new InvalidOperationException(
                "A procedure settlement receipt requires its exact private claim proof.");
        }
        var generationFingerprint = ComputeGenerationFingerprint(
            sessionGeneration,
            sessionGenerationRevision,
            cacheSnapshot,
            mortalItemCacheSnapshot,
            publicationAuthority,
            procedureClaimProof,
            request,
            finalization,
            semanticFingerprint);
        return new MortalWoundTreatmentPublicationTakeReceipt(
            fileSystem,
            authorityStateToken,
            sessionGeneration,
            sessionGenerationRevision,
            cacheSnapshot,
            mortalItemCacheSnapshot,
            acceptedState,
            request,
            finalization,
            publicationAuthority,
            procedureClaimProof,
            semanticFingerprint,
            generationFingerprint);
    }

    private static string ComputeGenerationFingerprint(
        string sessionGeneration,
        long sessionGenerationRevision,
        AcceptedMechanicsPlanCache.ValidatedPublicationTakeSnapshot cacheSnapshot,
        MortalItemAcceptedTurnAuthority.Cache.ValidatedPublicationTakeSnapshot
            mortalItemCacheSnapshot,
        MortalWoundTreatmentResourcePublicationAuthority publicationAuthority,
        MortalWoundProcedurePublicationClaimProof? procedureClaimProof,
        MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceFinalization finalization,
        string semanticFingerprint) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            "book_of_eternity.mortal_wound_treatment.publication_take_receipt",
            "3",
            sessionGeneration,
            sessionGenerationRevision.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            cacheSnapshot.BindingFingerprint,
            cacheSnapshot.PreparedPlanFingerprint,
            mortalItemCacheSnapshot.PublicationFingerprint,
            publicationAuthority.AuthorityFingerprint,
            procedureClaimProof?.Fingerprint,
            request.RequestFingerprint,
            finalization.FinalizationFingerprint,
            semanticFingerprint
        });

    internal bool TryConsume(object capability)
    {
        if (!AcceptedTurnAuthorityRegistry
                .IsTreatmentPublicationTransactionCapability(capability))
        {
            return false;
        }
        return Interlocked.Exchange(ref _consumed, 1) == 0;
    }
}
