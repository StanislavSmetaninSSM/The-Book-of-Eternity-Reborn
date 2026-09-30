using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static partial class WoundRepairPacketBuilder
{
    private static readonly IReadOnlySet<string> PersistedPacketFields =
        PersistedFieldSet(
            "kind",
            "candidateKind",
            "sessionId",
            "requestId",
            "snapshotToken",
            "candidateRef",
            "semanticFingerprint",
            "issues",
            "safeContext",
            "preservedProposal",
            "requiredResponseShape");

    private static readonly IReadOnlySet<string> PersistedReceiptFields =
        PersistedFieldSet(
            "sessionId",
            "requestId",
            "snapshotToken",
            "candidateRef",
            "semanticFingerprint");

    private static readonly IReadOnlySet<string> PersistedIssueFields =
        PersistedFieldSet("path", "code", "expected", "actual");

    private static readonly IReadOnlySet<string> PersistedSafeContextFields =
        PersistedFieldSet("event", "target", "realm");

    private static readonly IReadOnlySet<string> PersistedResponseShapeFields =
        PersistedFieldSet("woundDecisions", "response");

    private static readonly IReadOnlySet<string> PersistedDecisionFields =
        PersistedFieldSet("opportunityRef", "decision", "woundRef", "proposal");

    private static readonly IReadOnlySet<string> PersistedProposalRequirementFields =
        PersistedFieldSet("base", "correctOnly");

    /// <summary>
    /// Validates the complete public projection of one persisted wound-repair wave.
    /// The semantic fingerprint is opaque because its hidden source authority is not
    /// present in persisted public packet bytes; this boundary validates its shape and
    /// exact packet/receipt/root agreement without claiming to rederive it.
    /// </summary>
    internal static bool IsValidPersistedRepairWave(
        JsonElement packets,
        JsonElement receipts,
        string sessionId,
        string requestId,
        string snapshotToken)
    {
        if (!Exact(sessionId) ||
            !Exact(requestId) ||
            !Exact(snapshotToken) ||
            packets.ValueKind != JsonValueKind.Array ||
            receipts.ValueKind != JsonValueKind.Array ||
            packets.GetArrayLength() is < 1 or > CandidateLimit ||
            packets.GetArrayLength() != receipts.GetArrayLength())
        {
            return false;
        }

        var packetRows = packets.EnumerateArray().ToArray();
        var receiptRows = receipts.EnumerateArray().ToArray();
        var candidateRefs = new List<string>(packetRows.Length);
        for (var index = 0; index < packetRows.Length; index++)
        {
            if (!TryValidatePersistedPacket(
                    packetRows[index],
                    sessionId,
                    requestId,
                    snapshotToken,
                    out var packetIdentity) ||
                !TryValidatePersistedReceipt(
                    receiptRows[index],
                    sessionId,
                    requestId,
                    snapshotToken,
                    packetIdentity))
            {
                return false;
            }
            candidateRefs.Add(packetIdentity.CandidateRef);
        }

        return ExactAndConfusableUnique(candidateRefs);
    }

    private static bool TryValidatePersistedPacket(
        JsonElement packet,
        string sessionId,
        string requestId,
        string snapshotToken,
        out PersistedRepairIdentity identity)
    {
        identity = default;
        if (!HasUniquePersistedProperties(packet) ||
            !TryReadPersistedClosedObject(
                packet,
                PersistedPacketFields,
                out var fields) ||
            !TryReadPersistedString(fields, "kind", out var kind) ||
            !string.Equals(
                kind,
                "wound_materialization_repair",
                StringComparison.Ordinal) ||
            !TryReadPersistedString(fields, "candidateKind", out var candidateKind) ||
            !CandidateKinds.Contains(candidateKind) ||
            !TryReadPersistedString(fields, "sessionId", out var packetSessionId) ||
            !TryReadPersistedString(fields, "requestId", out var packetRequestId) ||
            !TryReadPersistedString(
                fields,
                "snapshotToken",
                out var packetSnapshotToken) ||
            !TryReadPersistedString(fields, "candidateRef", out var candidateRef) ||
            !TryReadPersistedString(
                fields,
                "semanticFingerprint",
                out var semanticFingerprint) ||
            !string.Equals(packetSessionId, sessionId, StringComparison.Ordinal) ||
            !string.Equals(packetRequestId, requestId, StringComparison.Ordinal) ||
            !string.Equals(packetSnapshotToken, snapshotToken, StringComparison.Ordinal) ||
            !Exact(candidateRef) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                semanticFingerprint) ||
            !IsValidPersistedSafeContext(fields["safeContext"]))
        {
            return false;
        }

        if (candidateKind == "author_alternative_treatment")
        {
            if (!IsValidPersistedAlternativePacket(fields))
                return false;
        }
        else if (!TryValidatePersistedIssues(fields["issues"], out var issuePaths) ||
                 !IsValidPersistedProposal(fields["preservedProposal"]) ||
                 !IsValidPersistedResponseShape(fields["requiredResponseShape"], issuePaths))
            return false;

        identity = new PersistedRepairIdentity(
            packetSessionId,
            packetRequestId,
            packetSnapshotToken,
            candidateRef,
            semanticFingerprint);
        return true;
    }

    private static bool TryValidatePersistedReceipt(
        JsonElement receipt,
        string sessionId,
        string requestId,
        string snapshotToken,
        PersistedRepairIdentity packetIdentity)
    {
        if (!HasUniquePersistedProperties(receipt) ||
            !TryReadPersistedClosedObject(
                receipt,
                PersistedReceiptFields,
                out var fields) ||
            !TryReadPersistedString(fields, "sessionId", out var receiptSessionId) ||
            !TryReadPersistedString(fields, "requestId", out var receiptRequestId) ||
            !TryReadPersistedString(
                fields,
                "snapshotToken",
                out var receiptSnapshotToken) ||
            !TryReadPersistedString(fields, "candidateRef", out var candidateRef) ||
            !TryReadPersistedString(
                fields,
                "semanticFingerprint",
                out var semanticFingerprint))
        {
            return false;
        }

        return Exact(receiptSessionId) &&
               Exact(receiptRequestId) &&
               Exact(receiptSnapshotToken) &&
               Exact(candidateRef) &&
               ResourceMaterializationContract.IsAuthorityFingerprint(
                   semanticFingerprint) &&
               string.Equals(receiptSessionId, sessionId, StringComparison.Ordinal) &&
               string.Equals(receiptRequestId, requestId, StringComparison.Ordinal) &&
               string.Equals(
                   receiptSnapshotToken,
                   snapshotToken,
                   StringComparison.Ordinal) &&
               string.Equals(
                   receiptSessionId,
                   packetIdentity.SessionId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   receiptRequestId,
                   packetIdentity.RequestId,
                   StringComparison.Ordinal) &&
               string.Equals(
                   receiptSnapshotToken,
                   packetIdentity.SnapshotToken,
                   StringComparison.Ordinal) &&
               string.Equals(
                   candidateRef,
                   packetIdentity.CandidateRef,
                   StringComparison.Ordinal) &&
               string.Equals(
                   semanticFingerprint,
                   packetIdentity.SemanticFingerprint,
                   StringComparison.Ordinal);
    }

    private static bool TryValidatePersistedIssues(
        JsonElement issues,
        out IReadOnlyList<string> paths)
    {
        paths = Array.Empty<string>();
        if (issues.ValueKind != JsonValueKind.Array ||
            issues.GetArrayLength() == 0)
        {
            return false;
        }

        var result = new List<string>(issues.GetArrayLength());
        foreach (var issue in issues.EnumerateArray())
        {
            if (!TryReadPersistedClosedObject(
                    issue,
                    PersistedIssueFields,
                    out var fields) ||
                !TryReadPersistedString(fields, "path", out var path) ||
                !TryReadPersistedString(fields, "code", out var code) ||
                !TryReadPersistedString(fields, "expected", out var expected) ||
                !TryReadPersistedString(fields, "actual", out var actual) ||
                actual.Length > EvidenceLimit ||
                !IsValidPersistedIssueProjection(path, code, expected))
            {
                return false;
            }
            result.Add(path);
        }

        paths = result;
        return true;
    }

    private static bool IsValidPersistedIssueProjection(
        string path,
        string code,
        string expected)
    {
        if (!IsValidPersistedIssuePath(path) ||
            !RepairableIssueCodes.Contains(code))
        {
            return false;
        }

        return code switch
        {
            "wound_response_unknown_field" when path == "proposal.owner" =>
                expected == "owner omitted; the client keeps the sealed target",
            "wound_response_unknown_field" when path.StartsWith(
                "proposal.",
                StringComparison.Ordinal) =>
                expected ==
                "remove only this unknown GM-authored field and preserve every valid sibling",
            "wound_severity_above_opportunity" =>
                path == "proposal.severity" &&
                IsValidPersistedSeverityRange(expected),
            "wound_consequence_slot_budget_exceeded" =>
                path.EndsWith(".root.slots", StringComparison.Ordinal) &&
                expected ==
                "at most 2 independently understandable consequence slots",
            "wound_materialization_effect_binding_invalid" =>
                path.EndsWith(".definition.links", StringComparison.Ordinal) &&
                expected ==
                "one complete wound-owned effect definition with response-local links",
            "wound_materialization_missing_field" =>
                path.StartsWith(
                    "proposal.treatment.routes[",
                    StringComparison.Ordinal) &&
                expected == "one complete treatment route in the closed wound schema",
            "wound_consequence_resource_bound_missing" =>
                path.EndsWith(".payload.resource", StringComparison.Ordinal) &&
                expected ==
                "one accepted resource and a bounded quantum-aligned amount",
            "wound_acquisition_narration_missing" or
                "wound_acquisition_narration_contradiction" =>
                path == "response" &&
                expected == "the exact acquisition narration inside the final scene",
            "wound_materialization_invalid_field" =>
                path.StartsWith("proposal.", StringComparison.Ordinal) &&
                expected ==
                "remove only this invalid field and preserve every valid sibling",
            _ => false
        };
    }

    private static bool IsValidPersistedIssuePath(string path)
    {
        if (string.Equals(path, "response", StringComparison.Ordinal))
            return true;
        const string prefix = "proposal.";
        if (!path.StartsWith(prefix, StringComparison.Ordinal) ||
            !TryParsePath(path[prefix.Length..], out var segments))
        {
            return false;
        }
        return segments.OfType<string>().All(segment =>
            !SensitiveKeys.Contains(segment));
    }

    private static bool IsValidPersistedSeverityRange(string value)
    {
        if (TrySeverityRank(value, out _))
            return true;
        var parts = value.Split('-', StringSplitOptions.None);
        return parts.Length == 2 &&
               TrySeverityRank(parts[0], out var minimum) &&
               TrySeverityRank(parts[1], out var maximum) &&
               minimum < maximum;
    }

    private static bool IsValidPersistedSafeContext(JsonElement context)
    {
        if (!TryReadPersistedClosedObject(
                context,
                PersistedSafeContextFields,
                out var fields))
        {
            return false;
        }
        return TryReadPersistedString(fields, "event", out var eventValue) &&
               Readable(eventValue) &&
               TryReadPersistedString(fields, "target", out var target) &&
               Readable(target) &&
               TryReadPersistedString(fields, "realm", out var realm) &&
               Readable(realm);
    }

    private static bool IsValidPersistedProposal(JsonElement proposal)
    {
        if (proposal.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in proposal.EnumerateObject())
        {
            if (!ProposalFields.Contains(property.Name, StringComparer.Ordinal))
                return false;
        }
        return !ContainsPersistedSensitiveKey(proposal);
    }

    private static bool IsValidPersistedResponseShape(
        JsonElement shape,
        IReadOnlyList<string> issuePaths)
    {
        if (!TryReadPersistedClosedObject(
                shape,
                PersistedResponseShapeFields,
                out var fields) ||
            !TryReadPersistedString(fields, "response", out var response) ||
            !string.Equals(
                response,
                "complete final scene containing display.acquisitionNarration verbatim",
                StringComparison.Ordinal) ||
            fields["woundDecisions"].ValueKind != JsonValueKind.Array ||
            fields["woundDecisions"].GetArrayLength() != 1)
        {
            return false;
        }

        var decision = fields["woundDecisions"].EnumerateArray().Single();
        if (!TryReadPersistedClosedObject(
                decision,
                PersistedDecisionFields,
                out var decisionFields) ||
            !TryReadPersistedString(
                decisionFields,
                "opportunityRef",
                out var opportunityRef) ||
            !Exact(opportunityRef) ||
            !TryReadPersistedString(decisionFields, "decision", out var decisionValue) ||
            decisionValue is not ("none" or "materialize") ||
            !TryReadPersistedString(decisionFields, "woundRef", out var woundRef) ||
            !Exact(woundRef) ||
            !TryReadPersistedClosedObject(
                decisionFields["proposal"],
                PersistedProposalRequirementFields,
                out var proposalFields) ||
            !TryReadPersistedString(proposalFields, "base", out var baseValue) ||
            !string.Equals(baseValue, "preservedProposal", StringComparison.Ordinal) ||
            proposalFields["correctOnly"].ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var expectedPaths = new List<string>();
        var seenPaths = new HashSet<string>(StringComparer.Ordinal);
        foreach (var path in issuePaths)
        {
            if (seenPaths.Add(path))
                expectedPaths.Add(path);
        }
        var actualPaths = new List<string>();
        foreach (var pathElement in proposalFields["correctOnly"].EnumerateArray())
        {
            if (pathElement.ValueKind != JsonValueKind.String ||
                pathElement.GetString() is not { } path)
            {
                return false;
            }
            actualPaths.Add(path);
        }
        return expectedPaths.SequenceEqual(actualPaths, StringComparer.Ordinal);
    }

    private static bool ContainsPersistedSensitiveKey(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in value.EnumerateObject())
            {
                if (SensitiveKeys.Contains(property.Name) ||
                    ContainsPersistedSensitiveKey(property.Value))
                {
                    return true;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (ContainsPersistedSensitiveKey(item))
                    return true;
            }
        }
        return false;
    }

    private static bool HasUniquePersistedProperties(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name) ||
                    !HasUniquePersistedProperties(property.Value))
                {
                    return false;
                }
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in value.EnumerateArray())
            {
                if (!HasUniquePersistedProperties(item))
                    return false;
            }
        }
        return true;
    }

    private static bool TryReadPersistedClosedObject(
        JsonElement value,
        IReadOnlySet<string> expectedFields,
        out IReadOnlyDictionary<string, JsonElement> fields)
    {
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        fields = result;
        if (value.ValueKind != JsonValueKind.Object)
            return false;
        foreach (var property in value.EnumerateObject())
        {
            if (!expectedFields.Contains(property.Name) ||
                !result.TryAdd(property.Name, property.Value))
            {
                return false;
            }
        }
        return result.Count == expectedFields.Count;
    }

    private static bool TryReadPersistedString(
        IReadOnlyDictionary<string, JsonElement> fields,
        string name,
        out string value)
    {
        value = string.Empty;
        if (!fields.TryGetValue(name, out var element) ||
            element.ValueKind != JsonValueKind.String ||
            element.GetString() is not { } text)
        {
            return false;
        }
        value = text;
        return true;
    }

    private static IReadOnlySet<string> PersistedFieldSet(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private readonly record struct PersistedRepairIdentity(
        string SessionId,
        string RequestId,
        string SnapshotToken,
        string CandidateRef,
        string SemanticFingerprint);
}
