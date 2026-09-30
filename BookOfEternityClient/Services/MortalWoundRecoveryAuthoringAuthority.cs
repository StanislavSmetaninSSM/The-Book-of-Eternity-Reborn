using System.Collections.Immutable;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundRecoveryAuthoringValidationResult(
    bool IsValid,
    ImmutableArray<ValidationIssue> Issues);

/// <summary>
/// Separates the GM-authored Mortal wound proposal dialect from canonical recovery
/// state. Recovery anchors are allocated by the accepted client lifecycle and may be
/// represented by the proposal only as absent or explicit null placeholders.
/// </summary>
internal static class MortalWoundRecoveryAuthoringAuthority
{
    private static readonly string[] ClientOwnedAnchorFields =
    {
        "recoveryAnchor",
        "deteriorationAnchor"
    };

    internal static MortalWoundRecoveryAuthoringValidationResult ValidateProposal(
        JsonObject proposal,
        string path)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var issues = ImmutableArray.CreateBuilder<ValidationIssue>();
        if (proposal["recovery"] is not JsonObject recovery)
        {
            issues.Add(CreateIssue(
                path + ".recovery",
                "wound_materialization_invalid_field",
                "complete GM-authored recovery object",
                proposal["recovery"]?.ToJsonString() ?? "missing"));
            return new MortalWoundRecoveryAuthoringValidationResult(
                false,
                issues.ToImmutable());
        }

        foreach (var field in ClientOwnedAnchorFields)
        {
            if (!recovery.TryGetPropertyValue(field, out var value) || value is null)
                continue;
            issues.Add(CreateIssue(
                path + ".recovery." + field,
                "wound_materialization_client_owned_field",
                "field absent or null in a GM-authored wound proposal",
                value.ToJsonString()));
        }

        return new MortalWoundRecoveryAuthoringValidationResult(
            issues.Count == 0,
            issues.ToImmutable());
    }

    private static ValidationIssue CreateIssue(
        string path,
        string code,
        string expected,
        string actual) => new(
        path,
        IssueSeverity.Error,
        "Mortal wound proposal contains client-owned recovery authority.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: expected,
        actual: actual,
        repairHint: "Remove recovery anchors from the GM proposal; the client allocates them after acceptance.");
}
