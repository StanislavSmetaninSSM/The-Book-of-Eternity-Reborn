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
    internal string SemanticFingerprint { get; }
    internal string GenerationFingerprint { get; }
    internal bool IsTerminalReleaseOnly => MortalItemCacheSnapshot.TerminalReleaseOnly;
    internal bool IsConsumed => Volatile.Read(ref _consumed) != 0;

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
        var generationFingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[]
            {
                "book_of_eternity.mortal_wound_treatment.publication_take_receipt",
                "2",
                sessionGeneration,
                sessionGenerationRevision.ToString(
                    System.Globalization.CultureInfo.InvariantCulture),
                cacheSnapshot.BindingFingerprint,
                cacheSnapshot.PreparedPlanFingerprint,
                mortalItemCacheSnapshot.PublicationFingerprint,
                publicationAuthority.AuthorityFingerprint,
                request.RequestFingerprint,
                finalization.FinalizationFingerprint,
                semanticFingerprint
            });
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
            semanticFingerprint,
            generationFingerprint);
    }

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
