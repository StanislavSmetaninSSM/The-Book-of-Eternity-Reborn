using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundProcedureClaimRecoveryResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;
    private readonly ReadOnlyCollection<MortalWoundTreatmentAttemptRequest> _requests;
    private readonly ReadOnlyCollection<MortalWoundTreatmentAttemptRequest>
        _heldRequests;
    private readonly ReadOnlyCollection<MortalWoundTreatmentAttemptRequest>
        _finalizedRequests;

    internal MortalWoundProcedureClaimRecoveryResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        IEnumerable<MortalWoundTreatmentAttemptRequest> requests,
        IEnumerable<MortalWoundTreatmentAttemptRequest> heldRequests,
        IEnumerable<MortalWoundTreatmentAttemptRequest> finalizedRequests)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        _requests = MortalWoundTreatmentShellDetachment.Freeze(requests);
        _heldRequests = MortalWoundTreatmentShellDetachment.Freeze(heldRequests);
        _finalizedRequests =
            MortalWoundTreatmentShellDetachment.Freeze(finalizedRequests);
    }

    internal bool IsValid { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal IReadOnlyList<MortalWoundTreatmentAttemptRequest> Requests => _requests;
    internal IReadOnlyList<MortalWoundTreatmentAttemptRequest> HeldRequests =>
        _heldRequests;
    internal IReadOnlyList<MortalWoundTreatmentAttemptRequest> FinalizedRequests =>
        _finalizedRequests;
}
