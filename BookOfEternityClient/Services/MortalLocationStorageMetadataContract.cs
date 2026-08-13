using System.Text.Json;

namespace BookOfEternityClient.Services;

internal static class MortalLocationStorageMetadataContract
{
    private static readonly HashSet<string> AmbiguousStorageAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "ownerActorId",
        "ownerFactionId",
        "ownerId",
        "ownerType",
        "ownerName",
        "access",
        "accessState",
        "accessLevel"
    };

    private static readonly HashSet<string> OwnerFields = new(StringComparer.Ordinal)
    {
        "ownerType", "ownerId", "ownerName"
    };

    private static readonly HashSet<string> AuthorizedUserFields = new(StringComparer.Ordinal)
    {
        "playerId", "playerName"
    };

    private static readonly HashSet<string> AllowedOwnerTypes = new(StringComparer.Ordinal)
    {
        "Player", "Faction", "Shared"
    };

    internal static IReadOnlyList<ValidationIssue> Validate(
        JsonElement storage,
        string context)
    {
        var issues = new List<ValidationIssue>();
        if (storage.ValueKind != JsonValueKind.Object)
        {
            Add(issues, context, "complete location storage object", storage.ValueKind.ToString());
            return issues;
        }

        RequireExactIdentifier(storage, context, "storageId", issues);
        RequireString(storage, context, "name", issues);
        RequireString(storage, context, "description", issues);
        RequireString(storage, context, "image_prompt", issues);
        RequireNonNegativeInt(storage, context, "capacity", issues);
        RequireNonNegativeFiniteNumber(storage, context, "volume", issues);
        ValidateOwner(storage, context, issues);
        ValidateAuthorizedUsers(storage, context, issues);
        RequireBoolean(storage, context, "hasFullAccess", issues);

        foreach (var alias in storage.EnumerateObject()
                     .Where(property => AmbiguousStorageAliases.Contains(property.Name)))
        {
            Add(
                issues,
                context + "." + alias.Name,
                "field absent; use owner/authorizedUsers/hasFullAccess runtime fields",
                alias.Value.GetRawText());
        }

        if (storage.TryGetProperty("contents", out var contents) &&
            contents.ValueKind != JsonValueKind.Array)
        {
            Add(
                issues,
                context + ".contents",
                "array when present",
                contents.ValueKind.ToString());
        }

        return issues;
    }

    private static void ValidateOwner(
        JsonElement storage,
        string context,
        List<ValidationIssue> issues)
    {
        if (!storage.TryGetProperty("owner", out var owner))
        {
            Add(issues, context + ".owner", "complete owner object or null", "missing");
            return;
        }
        if (owner.ValueKind == JsonValueKind.Null)
            return;
        if (owner.ValueKind != JsonValueKind.Object)
        {
            Add(issues, context + ".owner", "complete owner object or null", owner.ValueKind.ToString());
            return;
        }

        var ownerContext = context + ".owner";
        ValidateClosedObject(owner, ownerContext, OwnerFields, issues);
        var ownerType = RequireString(owner, ownerContext, "ownerType", issues);
        RequireExactIdentifier(owner, ownerContext, "ownerId", issues);
        RequireString(owner, ownerContext, "ownerName", issues);
        if (ownerType != null && !AllowedOwnerTypes.Contains(ownerType))
        {
            Add(
                issues,
                ownerContext + ".ownerType",
                string.Join(" | ", AllowedOwnerTypes),
                ownerType);
        }
    }

    private static void ValidateAuthorizedUsers(
        JsonElement storage,
        string context,
        List<ValidationIssue> issues)
    {
        if (!storage.TryGetProperty("authorizedUsers", out var users))
        {
            Add(issues, context + ".authorizedUsers", "array or null", "missing");
            return;
        }
        if (users.ValueKind == JsonValueKind.Null)
            return;
        if (users.ValueKind != JsonValueKind.Array)
        {
            Add(issues, context + ".authorizedUsers", "array or null", users.ValueKind.ToString());
            return;
        }

        var playerIds = new HashSet<string>(StringComparer.Ordinal);
        var aliasKeys = new HashSet<string>(StringComparer.Ordinal);
        var index = 0;
        foreach (var user in users.EnumerateArray())
        {
            var userContext = $"{context}.authorizedUsers[{index++}]";
            if (user.ValueKind != JsonValueKind.Object)
            {
                Add(issues, userContext, "playerId/playerName object", user.ValueKind.ToString());
                continue;
            }

            ValidateClosedObject(user, userContext, AuthorizedUserFields, issues);
            var playerId = RequireExactIdentifier(user, userContext, "playerId", issues);
            RequireString(user, userContext, "playerName", issues);
            if (playerId == null)
                continue;
            var exactUnique = playerIds.Add(playerId);
            var aliasUnique = aliasKeys.Add(MortalLocationIdentityState.BuildConfusableKey(playerId));
            if (!exactUnique || !aliasUnique)
            {
                Add(
                    issues,
                    userContext + ".playerId",
                    "one unique exact/confusable playerId",
                    playerId);
            }
        }
    }

    private static string? RequireString(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            !string.IsNullOrWhiteSpace(value.GetString()))
        {
            return value.GetString();
        }

        Add(issues, context + "." + field, "non-empty string", Describe(root, field));
        return null;
    }

    private static string? RequireExactIdentifier(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            value.GetString() is string text &&
            text.Length > 0 &&
            string.Equals(text, text.Trim(), StringComparison.Ordinal))
        {
            return text;
        }

        Add(
            issues,
            context + "." + field,
            "exact non-empty identifier without surrounding whitespace",
            Describe(root, field));
        return null;
    }

    private static void ValidateClosedObject(
        JsonElement value,
        string context,
        IReadOnlySet<string> allowedFields,
        List<ValidationIssue> issues)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (allowedFields.Contains(property.Name))
                continue;
            Add(
                issues,
                context + "." + property.Name,
                string.Join(" | ", allowedFields.OrderBy(static field => field, StringComparer.Ordinal)),
                property.Name);
        }
    }

    private static void RequireNonNegativeInt(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetInt32(out var number) ||
            number < 0)
        {
            Add(issues, context + "." + field, "non-negative integer", Describe(root, field));
        }
    }

    private static void RequireNonNegativeFiniteNumber(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind != JsonValueKind.Number ||
            !value.TryGetDouble(out var number) ||
            !double.IsFinite(number) ||
            number < 0)
        {
            Add(issues, context + "." + field, "non-negative finite number", Describe(root, field));
        }
    }

    private static void RequireBoolean(
        JsonElement root,
        string context,
        string field,
        List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(field, out var value) ||
            value.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
        {
            Add(issues, context + "." + field, "boolean", Describe(root, field));
        }
    }

    private static string Describe(JsonElement root, string field) =>
        !root.TryGetProperty(field, out var value) ? "missing" : value.GetRawText();

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Mortal location storage violates the complete canonical storage metadata contract.",
            code: "mortal_location_storage_semantic_invalid",
            section: "mortal_location_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Resubmit one complete, type-correct location storage metadata object through its governed location route."));
}
