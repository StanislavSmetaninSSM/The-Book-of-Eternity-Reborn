using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectSourceKey(
    string Realm,
    string Kind,
    string SourceId,
    string DefinitionKey);

internal sealed record EffectSourceExport(
    string Realm,
    string Kind,
    string SourceId,
    JsonArray Definitions,
    bool Materializable,
    bool Active,
    bool SameTurn);

internal sealed record EffectSourceAuthorityInput(
    IReadOnlyList<EffectSourceExport> PreTurnSources,
    IReadOnlyList<EffectSourceExport> SameTurnSources,
    IReadOnlySet<string> HistoricalSourceIds);

internal sealed record EffectSourceAuthorityEntry(
    EffectSourceKey Key,
    JsonObject Definition,
    bool Materializable,
    bool Active,
    bool SameTurn);

internal sealed record EffectSourceResolution(
    EffectSourceAuthorityEntry? Source,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Source != null && Issues.Count == 0;
}

internal sealed class EffectSourceAuthority
{
    private static readonly HashSet<string> SourceKinds = new(StringComparer.Ordinal)
    {
        "skill", "spiritual_art", "item", "wound", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat_action"
    };

    private readonly Dictionary<EffectSourceKey, EffectSourceAuthorityEntry> _entries;
    private readonly Dictionary<string, List<EffectSourceAuthorityEntry>> _byAlias;
    private readonly HashSet<string> _historicalAliases;
    private readonly HashSet<EffectSourceKey> _invalidKeys;

    private EffectSourceAuthority(Builder builder)
    {
        _entries = new Dictionary<EffectSourceKey, EffectSourceAuthorityEntry>(builder.Entries);
        _byAlias = builder.ByAlias.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _historicalAliases = new HashSet<string>(builder.HistoricalAliases, StringComparer.Ordinal);
        _invalidKeys = new HashSet<EffectSourceKey>(builder.InvalidKeys);
        Issues = builder.Issues.ToArray();
        Fingerprint = CreateFingerprint(_entries.Values, Issues);
    }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal string Fingerprint { get; }

    internal static EffectSourceAuthority Build(EffectSourceAuthorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new Builder(input.HistoricalSourceIds);
        builder.AddRange(input.PreTurnSources, sameTurn: false);
        builder.AddRange(input.SameTurnSources, sameTurn: true);
        return new EffectSourceAuthority(builder);
    }

    internal EffectSourceResolution Resolve(
        EffectSourceKey key,
        string targetKind,
        JsonObject? parameters)
    {
        var issues = new List<ValidationIssue>();
        if (_entries.TryGetValue(key, out var entry) && !_invalidKeys.Contains(key))
        {
            if (!entry.Materializable)
                Add(issues, "source", "effect_source_not_materializable", "source definition explicitly materializable as an active instance", key.ToString());
            if (!entry.Active)
                Add(issues, "source", "effect_source_inactive", "source current state authorizes application", key.ToString());
            ValidateTargetKind(entry.Definition, targetKind, issues);
            ValidateParameters(entry.Definition, parameters, issues);
            return issues.Count == 0
                ? new EffectSourceResolution(
                    entry with { Definition = entry.Definition.DeepClone().AsObject() },
                    Array.Empty<ValidationIssue>())
                : new EffectSourceResolution(null, issues);
        }

        var selectorAlias = Alias(key.Realm, key.Kind, key.SourceId, key.DefinitionKey);
        if (_byAlias.TryGetValue(selectorAlias, out var aliases) && aliases.Count > 0)
        {
            Add(issues, "source", "effect_source_selector_confusable", "one exact ordinal source selector", key.ToString());
        }
        else if (_historicalAliases.Contains(MortalLocationIdentityState.BuildConfusableKey(key.SourceId)))
        {
            Add(issues, "source.sourceId", "effect_source_selector_historical", "current non-historical source identity", key.SourceId);
        }
        else if (_entries.Keys.Any(candidate =>
                     string.Equals(candidate.Kind, key.Kind, StringComparison.Ordinal) &&
                     string.Equals(candidate.SourceId, key.SourceId, StringComparison.Ordinal) &&
                     string.Equals(candidate.DefinitionKey, key.DefinitionKey, StringComparison.Ordinal) &&
                     !string.Equals(candidate.Realm, key.Realm, StringComparison.Ordinal)))
        {
            Add(issues, "source.realm", "effect_source_realm_mismatch", "source realm equal to target realm", key.Realm);
        }
        else
        {
            Add(issues, "source", "effect_source_selector_unresolved", "one exact current source definition", key.ToString());
        }
        return new EffectSourceResolution(null, issues);
    }

    private static void ValidateTargetKind(
        JsonObject definition,
        string targetKind,
        List<ValidationIssue> issues)
    {
        if (definition["allowedTargetKinds"] is not JsonArray targets ||
            !targets.Any(item =>
                item is JsonValue value &&
                value.TryGetValue<string>(out var text) &&
                string.Equals(text, targetKind, StringComparison.Ordinal)))
        {
            Add(issues, "source.allowedTargetKinds", "effect_source_target_kind_forbidden", "definition allows exact target kind", targetKind);
        }
    }

    private static void ValidateParameters(
        JsonObject definition,
        JsonObject? parameters,
        List<ValidationIssue> issues)
    {
        if (definition["parameterBounds"] is not JsonObject bounds)
        {
            if (parameters != null && parameters.Count > 0)
                Add(issues, "source.parameterBounds", "effect_source_parameter_forbidden", "closed source parameter bounds", "missing");
            return;
        }
        foreach (var bound in bounds)
        {
            if (bound.Value is JsonObject boundObject &&
                boundObject["required"] is JsonValue requiredValue &&
                requiredValue.TryGetValue<bool>(out var required) && required &&
                (parameters == null || !parameters.ContainsKey(bound.Key) || parameters[bound.Key] == null))
            {
                Add(issues, "parameters." + bound.Key, "effect_source_parameter_required", "required source-owned parameter", "missing");
            }
        }
        if (parameters == null)
            return;
        foreach (var parameter in parameters)
        {
            var path = "parameters." + parameter.Key;
            if (bounds[parameter.Key] is not JsonObject bound)
            {
                Add(issues, path, "effect_source_parameter_forbidden", "parameter declared by exact source definition", parameter.Value?.ToJsonString() ?? "null");
                continue;
            }
            if (!TryReadString(bound["kind"], out var kind))
            {
                Add(issues, path, "effect_source_parameter_out_of_bounds", "valid source parameter bound", bound.ToJsonString());
                continue;
            }
            switch (kind)
            {
                case "number":
                case "integer":
                    if (!TryReadFinite(parameter.Value, out var number) ||
                        !TryReadFinite(bound["minimum"], out var minimum) ||
                        !TryReadFinite(bound["maximum"], out var maximum) ||
                        number < minimum || number > maximum ||
                        kind == "integer" && number != Math.Truncate(number))
                    {
                        Add(issues, path, "effect_source_parameter_out_of_bounds", $"{kind} inside source-owned bound", parameter.Value?.ToJsonString() ?? "null");
                    }
                    break;
                case "enum":
                    if (!TryReadString(parameter.Value, out var enumValue) ||
                        bound["allowedValues"] is not JsonArray allowed ||
                        !allowed.Any(item => TryReadString(item, out var candidate) && string.Equals(candidate, enumValue, StringComparison.Ordinal)))
                    {
                        Add(issues, path, "effect_source_parameter_out_of_bounds", "exact source-owned enum value", parameter.Value?.ToJsonString() ?? "null");
                    }
                    break;
                case "identity":
                    if (!TryReadExactString(parameter.Value, out _))
                        Add(issues, path, "effect_source_parameter_out_of_bounds", "exact identity", parameter.Value?.ToJsonString() ?? "null");
                    break;
                case "boolean":
                    if (parameter.Value is not JsonValue booleanValue ||
                        !booleanValue.TryGetValue<bool>(out _))
                    {
                        Add(issues, path, "effect_source_parameter_out_of_bounds", "boolean", parameter.Value?.ToJsonString() ?? "null");
                    }
                    break;
            }
        }
    }

    private static string CreateFingerprint(
        IEnumerable<EffectSourceAuthorityEntry> entries,
        IReadOnlyList<ValidationIssue> issues)
    {
        var root = new JsonObject
        {
            ["entries"] = new JsonArray(entries
                .OrderBy(static entry => entry.Key.Realm, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Key.Kind, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Key.SourceId, StringComparer.Ordinal)
                .ThenBy(static entry => entry.Key.DefinitionKey, StringComparer.Ordinal)
                .Select(entry => (JsonNode)new JsonObject
                {
                    ["realm"] = entry.Key.Realm,
                    ["kind"] = entry.Key.Kind,
                    ["sourceId"] = entry.Key.SourceId,
                    ["definitionKey"] = entry.Key.DefinitionKey,
                    ["materializable"] = entry.Materializable,
                    ["active"] = entry.Active,
                    ["sameTurn"] = entry.SameTurn,
                    ["definition"] = entry.Definition.DeepClone()
                }).ToArray()),
            ["issues"] = new JsonArray(issues.Select(issue => (JsonNode)new JsonObject
            {
                ["code"] = issue.Code,
                ["path"] = issue.FilePath
            }).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static string Alias(string realm, string kind, string sourceId, string definitionKey) =>
        realm + "\u001f" + kind + "\u001f" +
        MortalLocationIdentityState.BuildConfusableKey(sourceId) + "\u001f" +
        MortalLocationIdentityState.BuildConfusableKey(definitionKey);

    private static bool TryReadString(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
        return value.Length > 0;
    }

    private static bool TryReadExactString(JsonNode? node, out string value) =>
        TryReadString(node, out value) && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool TryReadFinite(JsonNode? node, out double value)
    {
        value = 0;
        if (node is not JsonValue jsonValue)
            return false;
        if (jsonValue.TryGetValue<double>(out value) && double.IsFinite(value))
            return true;
        if (jsonValue.TryGetValue<int>(out var integer))
        {
            value = integer;
            return true;
        }
        return false;
    }

    private static void Add(List<ValidationIssue> issues, string path, string code, string expected, string actual) =>
        issues.Add(NewIssue(path, code, expected, actual));

    private static ValidationIssue NewIssue(string path, string code, string expected, string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Effect source selector violates exact materialization authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact active source export and its closed materializable definition; do not infer authority from names, prose, aliases, or raw siblings.");

    private sealed class Builder
    {
        internal Dictionary<EffectSourceKey, EffectSourceAuthorityEntry> Entries { get; } = new();
        internal Dictionary<string, List<EffectSourceAuthorityEntry>> ByAlias { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> HistoricalAliases { get; }
        internal HashSet<EffectSourceKey> InvalidKeys { get; } = new();
        internal List<ValidationIssue> Issues { get; } = new();

        internal Builder(IEnumerable<string> historicalSourceIds)
        {
            HistoricalAliases = historicalSourceIds
                .Select(MortalLocationIdentityState.BuildConfusableKey)
                .ToHashSet(StringComparer.Ordinal);
        }

        internal void AddRange(IEnumerable<EffectSourceExport> exports, bool sameTurn)
        {
            foreach (var export in exports)
                AddExport(export, sameTurn);
        }

        private void AddExport(EffectSourceExport export, bool sameTurn)
        {
            var sourcePath = $"sources[{export.Kind}:{export.SourceId}]";
            if (!SourceKinds.Contains(export.Kind) ||
                !TryExact(export.Realm) ||
                !TryExact(export.SourceId))
            {
                Issues.Add(NewIssue(sourcePath, "effect_source_authority_invalid_export", "exact supported source export", export.ToString()));
                return;
            }

            using var document = JsonDocument.Parse(export.Definitions.ToJsonString());
            var definitionIssues = EffectSourceDefinitionContract.ValidateArray(
                document.RootElement,
                sourcePath + ".activeEffectDefinitions",
                export.Realm);
            Issues.AddRange(definitionIssues);
            if (definitionIssues.Count > 0)
                return;

            foreach (var node in export.Definitions)
            {
                if (node is not JsonObject definition || !TryReadExactString(definition["definitionKey"], out var definitionKey))
                    continue;
                var key = new EffectSourceKey(export.Realm, export.Kind, export.SourceId, definitionKey);
                var entry = new EffectSourceAuthorityEntry(
                    key,
                    definition.DeepClone().AsObject(),
                    export.Materializable,
                    export.Active,
                    sameTurn);
                if (!Entries.TryAdd(key, entry))
                {
                    InvalidKeys.Add(key);
                    Issues.Add(NewIssue(sourcePath, "effect_source_authority_duplicate_source", "one exact source definition export", key.ToString()));
                }
                var alias = Alias(key.Realm, key.Kind, key.SourceId, key.DefinitionKey);
                if (!ByAlias.TryGetValue(alias, out var values))
                {
                    values = new List<EffectSourceAuthorityEntry>();
                    ByAlias.Add(alias, values);
                }
                values.Add(entry);
                if (values.Any(candidate => !Equals(candidate.Key, key)))
                {
                    InvalidKeys.Add(key);
                    foreach (var candidate in values)
                        InvalidKeys.Add(candidate.Key);
                    Issues.Add(NewIssue(sourcePath, "effect_source_authority_confusable_source", "one exact/confusable source definition export", key.ToString()));
                }
            }
        }

        private static bool TryExact(string value) =>
            value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }
}
