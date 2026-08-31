using System.Collections.ObjectModel;

namespace BookOfEternityClient.Services;

internal sealed class MortalWoundProcedureClaimRecoveryResult
{
    private readonly ReadOnlyCollection<ValidationIssue> _issues;
    private readonly ReadOnlyCollection<MortalWoundTreatmentAttemptRequest> _requests;

    internal MortalWoundProcedureClaimRecoveryResult(
        bool isValid,
        IEnumerable<ValidationIssue> issues,
        IEnumerable<MortalWoundTreatmentAttemptRequest> requests)
    {
        IsValid = isValid;
        _issues = MortalWoundTreatmentShellDetachment.Freeze(issues);
        _requests = MortalWoundTreatmentShellDetachment.Freeze(requests);
    }

    internal bool IsValid { get; }
    internal IReadOnlyList<ValidationIssue> Issues => _issues;
    internal IReadOnlyList<MortalWoundTreatmentAttemptRequest> Requests => _requests;
}
