using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

/// <summary>
/// Binds a proposed first offer to an instance in the exact signed original receipt history.
/// It returns an unpublished instance row only when the signed history has no open match.
/// </summary>
internal static class SpiritualWoundFirstOfferInstanceResolver
{
    /// <summary>
    /// Carries a bound instance identity or issues that prevent first-offer composition.
    /// </summary>
    /// <param name="InstanceId">
    /// Existing or newly derived instance identity; <see langword="null"/> on failure.
    /// </param>
    /// <param name="ProposedRow">
    /// Detached unpublished first-admission row, or <see langword="null"/> when reusing an accepted instance.
    /// </param>
    /// <param name="Issues">
    /// Strict signed-history or conflict-identity validation failures.
    /// </param>
    internal sealed record Result(string? InstanceId, JsonObject? ProposedRow,
        IReadOnlyList<ValidationIssue> Issues);

    /// <summary>
    /// Resolves the current display conflict against accepted signed history without writing it.
    /// </summary>
    /// <param name="receipt">
    /// Signed original receipt bytes or proved signed absence.
    /// </param>
    /// <param name="conflict">
    /// Exact signed original conflict image.
    /// </param>
    /// <param name="realm">
    /// Source-owned canonical spiritual realm.
    /// </param>
    /// <param name="sessionId">
    /// Signed original pending-session identity.
    /// </param>
    /// <param name="requestId">
    /// Signed original request identity.
    /// </param>
    /// <param name="snapshotToken">
    /// Validated pending-snapshot token.
    /// </param>
    /// <param name="turn">
    /// Positive signed original turn number.
    /// </param>
    /// <returns>
    /// An existing open instance or a detached proposed start row; no accepted receipt is created.
    /// </returns>
    internal static Result Resolve(CanonicalBeforeImage receipt, CanonicalBeforeImage conflict,
        string realm, string sessionId, string requestId, string snapshotToken, int turn)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        ArgumentNullException.ThrowIfNull(conflict);
        if (realm is not ("chaos_sea" or "shining_abode") ||
            string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(requestId) ||
            string.IsNullOrWhiteSpace(snapshotToken) || turn < 1 || !conflict.Existed ||
            conflict.Bytes is null || receipt.Existed != (receipt.Bytes is not null))
            return Fail("spiritual_first_offer_signed_origin_invalid");
        try
        {
            var original = SpiritualWoundStateJson.Parse(Encoding.UTF8.GetString(conflict.Bytes));
            var active = original["activeConflict"] as JsonObject;
            var display = active?["conflictId"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(display) || display != display.Trim())
                return Fail("spiritual_first_offer_active_conflict_required");
            var rootJson = receipt.Existed
                ? Encoding.UTF8.GetString(receipt.Bytes!)
                : """
                  {"schemaVersion":1,"nextInstanceOrdinal":1,"nextClosureOrdinal":1,
                   "nextSourceOrdinal":1,"nextDecisionOrdinal":1,"instances":[],
                   "closures":[],"sources":[],"decisions":[]}
                  """;
            var parsed = SpiritualWoundOpportunityReceiptState.Parse(rootJson,
                SpiritualWoundOpportunityReceiptState.StatePath);
            if (!parsed.IsValid || parsed.State is null)
                return new(null, null, parsed.Issues);
            var history = SpiritualWoundStateJson.Parse(
                SpiritualWoundOpportunityReceiptState.SerializeCanonical(parsed.State));
            if (history["sources"]!.AsArray().Any(row =>
                    (string?)row?["witness"]?["turnEvidence"]?["sessionId"] == sessionId &&
                    (string?)row?["witness"]?["turnEvidence"]?["requestId"] == requestId))
                return Fail("spiritual_first_offer_already_accepted");
            var instances = history["instances"]!.AsArray();
            var displayKey = MortalLocationIdentityState.BuildConfusableKey(display);
            if (instances.OfType<JsonObject>().Any(row =>
                    (string?)row["realm"] == realm &&
                    (string?)row["displayConflictId"] != display &&
                    MortalLocationIdentityState.BuildConfusableKey(
                        row["displayConflictId"]!.GetValue<string>()) == displayKey))
                return Fail("spiritual_first_offer_confusable_display");
            var closed = history["closures"]!.AsArray()
                .Select(row => row!["instanceId"]!.GetValue<string>())
                .ToHashSet(StringComparer.Ordinal);
            var open = instances.OfType<JsonObject>()
                .Where(row => (string?)row["realm"] == realm &&
                              (string?)row["displayConflictId"] == display &&
                              !closed.Contains(row["instanceId"]!.GetValue<string>()))
                .ToArray();
            if (open.Length > 1)
                return Fail("spiritual_first_offer_instance_ambiguous");
            if (open.Length == 1)
                return new(open[0]["instanceId"]!.GetValue<string>(), null, []);
            var row = new JsonObject
            {
                ["instanceId"] = "",
                ["ordinal"] = history["nextInstanceOrdinal"]!.GetValue<int>(),
                ["displayConflictId"] = display,
                ["realm"] = realm,
                ["startSessionId"] = sessionId,
                ["startRequestId"] = requestId,
                ["startSnapshotToken"] = snapshotToken,
                ["startTurn"] = turn,
                ["baselineConflictFingerprint"] = conflict.Fingerprint,
                ["instanceFingerprint"] = ""
            };
            var fingerprint = SpiritualWoundConflictInstanceState.ComputeRowFingerprint(row, "instance");
            row["instanceFingerprint"] = fingerprint;
            row["instanceId"] = "spiritual_instance_" + fingerprint[7..];
            history["instances"]!.AsArray().Add(row.DeepClone());
            history["nextInstanceOrdinal"] = checked(row["ordinal"]!.GetValue<int>() + 1);
            if (!SpiritualWoundOpportunityReceiptState.Parse(history.ToJsonString(),
                    SpiritualWoundOpportunityReceiptState.StatePath).IsValid)
                return Fail("spiritual_first_offer_instance_history_conflict");
            return new(row["instanceId"]!.GetValue<string>(), row, []);
        }
        catch (Exception error) when (error is JsonException or FormatException or
            InvalidOperationException or OverflowException)
        {
            return Fail("spiritual_first_offer_signed_origin_invalid");
        }
    }

    private static Result Fail(string code) => new(null, null,
    [
        new ValidationIssue(SpiritualWoundOpportunityReceiptState.StatePath,
            IssueSeverity.Error, "The signed original spiritual history cannot bind this conflict.",
            code: code, section: "AcceptedTurnWoundMaterialization")
    ]);
}
