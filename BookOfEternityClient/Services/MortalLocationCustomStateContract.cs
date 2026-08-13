using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class MortalLocationCustomStateContract
{
    private static readonly HashSet<string> ForbiddenFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "materialization",
        "materializationReceipt",
        "materializationId",
        "receipt",
        "receiptId",
        "receiptSeal",
        "seal",
        "sourceAuthority",
        "sourceAuthorityKind",
        "sourceAuthorityId",
        "sourceTurn",
        "initialId",
        "locationIdentityIndex",
        "linkIdentityIndex",
        "identityIndex",
        "requestId",
        "sessionId",
        "reservationId",
        "transitionId",
        "transitions",
        "repairPacket",
        "repairRequest",
        "repairTargets",
        "expectedAuthority",
        "actualEvidence",
        "targetFiles",
        "exactFieldCorrections",
        "requiredCompanionTargets",
        "templateRefs",
        "expectedShape",
        "safeCorrectionRules",
        "repairHint",
        "validationCode",
        "validationCodes",
        "validationIssue",
        "validationIssues",
        "filePath",
        "sourcePath",
        "targetPath",
        "gmInstructions",
        "summaryGroups",
        "harnessRepairPackets",
        "metadataDiagnosticOnly",
        "revalidationAttempt",
        "fullTurnResubmissionRequired",
        "resubmissionObligations",
        "requiredResubmissionPaths",
        "rollbackAvailable",
        "detectedAtUtc",
        "worldMapUpdates",
        "currentLocationData",
        "newLocations",
        "newLinks",
        "locationUpdates",
        "locationDiscoveryTransitions",
        "storageUpdates",
        "storagesToRemove",
        "linkUpdates",
        "linkRemovals",
        "threatsToAdd",
        "threatsToUpdate",
        "threatsToRemove",
        "completeThreatActivities"
    };

    private static readonly HashSet<string> ForbiddenKinds = new(StringComparer.Ordinal)
    {
        "mortal_location_materialization_repair",
        "mortal_item_materialization_repair",
        "mortal_item_identity_authority_repair"
    };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement customStates,
        string context)
    {
        var issues = new List<ValidationIssue>();
        if (customStates.ValueKind != JsonValueKind.Array)
            return issues;

        ValidateRecursive(customStates, context, issues);
        return issues;
    }

    private static void ValidateRecursive(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                if (value.TryGetProperty("kind", out var kind) &&
                    kind.ValueKind == JsonValueKind.String &&
                    ForbiddenKinds.Contains(kind.GetString()!))
                {
                    Add(issues, path + ".kind", kind.GetString()!);
                }

                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (ForbiddenFields.Contains(property.Name))
                    {
                        Add(issues, propertyPath, property.Value.GetRawText());
                        continue;
                    }
                    ValidateRecursive(property.Value, propertyPath, issues);
                }
                break;
            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    ValidateRecursive(item, $"{path}[{index++}]", issues);
                break;
        }
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal location custom state cannot contain client-owned authority, protocol, validation, or repair fields.",
            code: "mortal_location_custom_state_authority_forbidden",
            section: "mortal_location_materialization",
            expected: "setting-specific semantic data without client/protocol/repair authority",
            actual: actual,
            repairHint: "Remove the internal field or DTO and resubmit only setting-specific semantic custom state."));
}
