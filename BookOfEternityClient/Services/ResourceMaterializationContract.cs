using System.Collections.Frozen;
using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Services;

internal sealed record ResourceRootParseResult(
    bool IsMissing,
    JsonElement? Root,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Issues.Count == 0;
}

internal static class ResourceMaterializationContract
{
    internal const int SchemaVersion = 1;

    internal const string DefinitionsPath = "game_state/resources/resource_definitions.json";
    internal const string StatePath = "game_state/resources/resource_state.json";
    internal const string HistoryPath = "game_state/resources/resource_history.json";
    internal const string CommandPath = "game_state/resources/resource_commands.json";

    internal const int MaxDefinitions = 256;
    internal const int MaxLiveEntries = 20_000;
    internal const int MaxCapacityTransitionsPerTurn = 256;
    internal const int MaxMutationsBeforeTriggers = 512;
    internal const int MaxTriggerNodes = 1_024;
    internal const int MaxTriggerDepth = 32;
    internal const int MaxPendingRequestsPerTurn = 64;

    private static readonly FrozenSet<string> DefinitionRootFields =
        new[] { "schemaVersion", "definitions" }
            .ToFrozenSet(StringComparer.Ordinal);

    private static readonly FrozenSet<string> ClientOwnedDefinitionFields =
        new[]
        {
            "materialization",
            "definitionId",
            "seal",
            "createdAtTurn",
            "createdEventRef"
        }.ToFrozenSet(StringComparer.Ordinal);

    internal static ResourceRootParseResult ParseDefinitions(
        string? json,
        bool allowMissingPristine)
    {
        if (json == null)
        {
            if (allowMissingPristine)
            {
                return new ResourceRootParseResult(
                    IsMissing: true,
                    Root: null,
                    Issues: Array.Empty<ValidationIssue>());
            }

            return InvalidRoot(
                isMissing: true,
                DefinitionsPath,
                "resource_materialization_root_missing",
                "present canonical resource definition root",
                "missing");
        }

        if (string.IsNullOrWhiteSpace(json))
        {
            return InvalidRoot(
                isMissing: false,
                DefinitionsPath,
                "resource_materialization_invalid_root",
                "non-empty strict JSON object",
                "empty or whitespace-only file");
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            return InvalidRoot(
                isMissing: false,
                DefinitionsPath,
                "resource_materialization_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                return InvalidRoot(
                    isMissing: false,
                    DefinitionsPath,
                    "resource_materialization_invalid_root",
                    "strict JSON object",
                    root.ValueKind.ToString());
            }

            var issues = new List<ValidationIssue>();
            FindDuplicateProperties(
                root,
                DefinitionsPath,
                issues,
                "resource_materialization_duplicate_property");
            ValidateClosedObject(
                root,
                DefinitionsPath,
                DefinitionRootFields,
                issues,
                "resource_materialization_unknown_field");

            if (!root.TryGetProperty("schemaVersion", out var schemaVersion) ||
                schemaVersion.ValueKind != JsonValueKind.Number ||
                !schemaVersion.TryGetInt32(out var parsedVersion) ||
                parsedVersion != SchemaVersion)
            {
                AddIssue(
                    issues,
                    DefinitionsPath + ".schemaVersion",
                    "resource_materialization_invalid_field",
                    SchemaVersion.ToString(CultureInfo.InvariantCulture),
                    Describe(root, "schemaVersion"));
            }

            if (!root.TryGetProperty("definitions", out var definitions) ||
                definitions.ValueKind != JsonValueKind.Array)
            {
                AddIssue(
                    issues,
                    DefinitionsPath + ".definitions",
                    "resource_materialization_invalid_field",
                    "array",
                    Describe(root, "definitions"));
            }
            else if (definitions.GetArrayLength() > MaxDefinitions)
            {
                AddIssue(
                    issues,
                    DefinitionsPath + ".definitions",
                    "resource_materialization_limit_exceeded",
                    $"at most {MaxDefinitions} definitions",
                    definitions.GetArrayLength().ToString(CultureInfo.InvariantCulture));
            }

            return new ResourceRootParseResult(
                IsMissing: false,
                Root: issues.Count == 0 ? root.Clone() : null,
                Issues: issues.ToArray());
        }
    }

    internal static bool TryReadExactDecimal(JsonElement value, out decimal result)
    {
        result = 0m;
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDecimal(out var candidate))
            return false;

        if (!TryParseNumberRational(value.GetRawText(), out var rawCoefficient, out var rawScale))
            return false;

        var bits = decimal.GetBits(candidate);
        var decimalCoefficient =
            new BigInteger((uint)bits[0]) |
            (new BigInteger((uint)bits[1]) << 32) |
            (new BigInteger((uint)bits[2]) << 64);
        if ((bits[3] & int.MinValue) != 0)
            decimalCoefficient = -decimalCoefficient;
        var decimalScale = (bits[3] >> 16) & 0xFF;

        NormalizeDecimalRational(ref rawCoefficient, ref rawScale);
        NormalizeDecimalRational(ref decimalCoefficient, ref decimalScale);
        if (rawCoefficient != decimalCoefficient || rawScale != decimalScale)
            return false;

        result = candidate;
        return true;
    }

    internal static bool IsExactIdentifier(string? value)
    {
        if (string.IsNullOrEmpty(value) ||
            !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        try
        {
            if (!value.IsNormalized(NormalizationForm.FormKC))
                return false;

            foreach (var rune in value.EnumerateRunes())
            {
                if (Rune.GetUnicodeCategory(rune) is
                    UnicodeCategory.Control or
                    UnicodeCategory.Format or
                    UnicodeCategory.LineSeparator or
                    UnicodeCategory.ParagraphSeparator)
                {
                    return false;
                }
            }

            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    internal static bool IsIntegral(decimal value) => decimal.Truncate(value) == value;

    internal static bool IsAuthorityFingerprint(string? value)
    {
        const string prefix = "sha256:";
        if (value == null ||
            value.Length != prefix.Length + 64 ||
            !value.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var character in value.AsSpan(prefix.Length))
        {
            if (character is not (>= '0' and <= '9') and not (>= 'a' and <= 'f'))
                return false;
        }

        return true;
    }

    internal static bool IsQuantumAligned(decimal value, decimal minimum, decimal quantum)
    {
        if (quantum <= 0m)
            return false;

        try
        {
            return (value - minimum) % quantum == 0m;
        }
        catch (OverflowException)
        {
            return false;
        }
    }

    internal static string BuildConfusableKey(string value) =>
        MortalLocationIdentityState.BuildConfusableKey(value);

    internal static IReadOnlyList<ValidationIssue> ValidateRawDefinitionFields(
        JsonElement definition,
        string path)
    {
        var issues = new List<ValidationIssue>();
        FindClientOwnedDefinitionFields(definition, path, issues);
        return issues;
    }

    internal static void FindDuplicateProperties(
        JsonElement value,
        string path,
        List<ValidationIssue> issues,
        string code)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (!seen.Add(property.Name))
                    {
                        AddIssue(
                            issues,
                            propertyPath,
                            code,
                            "one occurrence of each exact property",
                            property.Name);
                    }

                    FindDuplicateProperties(property.Value, propertyPath, issues, code);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    FindDuplicateProperties(item, $"{path}[{index++}]", issues, code);
                break;
        }
    }

    internal static void ValidateClosedObject(
        JsonElement value,
        string path,
        IReadOnlySet<string> allowedFields,
        List<ValidationIssue> issues,
        string code)
    {
        if (value.ValueKind != JsonValueKind.Object)
            return;

        foreach (var property in value.EnumerateObject())
        {
            if (!allowedFields.Contains(property.Name))
            {
                AddIssue(
                    issues,
                    path + "." + property.Name,
                    code,
                    "registered current-schema field",
                    property.Name);
            }
        }
    }

    internal static void AddIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual,
        IssueCategory category = IssueCategory.StateConsistency)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            $"Unified resource contract rejected '{path}'.",
            code: code,
            actor: "Client",
            section: "UnifiedResourceAuthority",
            expected: expected,
            actual: actual,
            category: category));
    }

    internal static string Describe(JsonElement root, string field) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(field, out var value)
            ? value.GetRawText()
            : "missing";

    private static ResourceRootParseResult InvalidRoot(
        bool isMissing,
        string path,
        string code,
        string expected,
        string actual)
    {
        var issues = new List<ValidationIssue>();
        AddIssue(issues, path, code, expected, actual);
        return new ResourceRootParseResult(isMissing, null, issues);
    }

    private static void FindClientOwnedDefinitionFields(
        JsonElement value,
        string path,
        List<ValidationIssue> issues)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                foreach (var property in value.EnumerateObject())
                {
                    var propertyPath = path + "." + property.Name;
                    if (ClientOwnedDefinitionFields.Contains(property.Name))
                    {
                        AddIssue(
                            issues,
                            propertyPath,
                            "resource_definition_client_field_forbidden",
                            "field absent from GM-authored definition proposal",
                            property.Value.GetRawText(),
                            IssueCategory.ClientOwnedSurface);
                    }

                    FindClientOwnedDefinitionFields(property.Value, propertyPath, issues);
                }
                break;

            case JsonValueKind.Array:
                var index = 0;
                foreach (var item in value.EnumerateArray())
                    FindClientOwnedDefinitionFields(item, $"{path}[{index++}]", issues);
                break;
        }
    }

    private static bool TryParseNumberRational(
        string raw,
        out BigInteger coefficient,
        out int scale)
    {
        coefficient = BigInteger.Zero;
        scale = 0;
        var span = raw.AsSpan();
        var negative = false;
        if (span.Length > 0 && span[0] == '-')
        {
            negative = true;
            span = span[1..];
        }

        var exponentIndex = span.IndexOfAny('e', 'E');
        var mantissa = exponentIndex >= 0 ? span[..exponentIndex] : span;
        var exponent = 0;
        if (exponentIndex >= 0 &&
            !int.TryParse(
                span[(exponentIndex + 1)..],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out exponent))
        {
            return false;
        }

        var decimalIndex = mantissa.IndexOf('.');
        var fractionalDigits = decimalIndex >= 0
            ? mantissa.Length - decimalIndex - 1
            : 0;
        var digits = decimalIndex >= 0
            ? string.Concat(mantissa[..decimalIndex], mantissa[(decimalIndex + 1)..])
            : mantissa.ToString();
        if (!BigInteger.TryParse(
                digits,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out coefficient))
        {
            return false;
        }

        if (negative)
            coefficient = -coefficient;
        if (coefficient.IsZero)
        {
            scale = 0;
            return true;
        }

        try
        {
            scale = checked(fractionalDigits - exponent);
        }
        catch (OverflowException)
        {
            return false;
        }

        if (scale < 0)
        {
            if (scale < -100)
                return false;
            coefficient *= BigInteger.Pow(10, -scale);
            scale = 0;
        }

        return true;
    }

    private static void NormalizeDecimalRational(ref BigInteger coefficient, ref int scale)
    {
        if (coefficient.IsZero)
        {
            scale = 0;
            return;
        }

        while (scale > 0 && coefficient % 10 == 0)
        {
            coefficient /= 10;
            scale--;
        }
    }
}
