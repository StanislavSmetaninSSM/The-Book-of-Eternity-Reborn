using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.Services;

/// <summary>
/// Compares raw dependent cost and exact position arithmetic corrections with a frozen baseline; it grants no execution authority.
/// </summary>
internal sealed class SpiritualWoundDependentDraftPolicy
{
    private static readonly Regex CostPath = new(
        @"^activeConflict\.exchangeLog\[(\d+)\]\.actionCostAudit\.(player|opposition)(?:\.(effectiveCost|before|after))?$",
        RegexOptions.CultureInvariant);
    private static readonly string[] ForceFields =
        ["operationType", "baseCost", "minCost", "artTier", "effectiveCost", "before", "after"];
    private readonly JsonObject _baseline;
    private readonly IReadOnlyList<ValidationService.SpiritualPositionDraftCorrection> _positions;
    private readonly HashSet<string> _pointers;
    private readonly IReadOnlyList<(string Pointer, bool Adding)> _forceAudits;

    /// <summary>
    /// Freezes a derived comparison policy and its exact sorted public field list.
    /// </summary>
    /// <param name="baseline">
    /// Strict committed raw conflict image, copied before retention.
    /// </param>
    /// <param name="pointers">
    /// Exact leaf pointers and guarded arithmetic containers derived from genuine baseline diagnostics and owners.
    /// </param>
    /// <param name="forceAudits">
    /// Prescribed audit additions or removals requiring extra structural checks.
    /// </param>
    /// <param name="positions">
    /// Detached exact arithmetic groups derived at genuine current position frontiers.
    /// </param>
    private SpiritualWoundDependentDraftPolicy(JsonObject baseline, HashSet<string> pointers,
        IReadOnlyList<(string Pointer, bool Adding)> forceAudits,
        IReadOnlyList<ValidationService.SpiritualPositionDraftCorrection> positions)
    {
        _baseline = baseline.DeepClone().AsObject();
        _pointers = pointers;
        _forceAudits = forceAudits;
        _positions = positions;
        Fields = Array.AsReadOnly(pointers.OrderBy(value => value, StringComparer.Ordinal)
            .Select(pointer => new SpiritualWoundContinuationField
            {
                Path = AfterlifeSpiritualConflictState.StatePath, JsonPointer = pointer
            }).ToArray());
    }

    /// <summary>
    /// Gets the exact raw correction fields; guarded containers do not relax the internal whole-group matcher.
    /// </summary>
    internal IReadOnlyList<SpiritualWoundContinuationField> Fields { get; }

    /// <summary>
    /// Unites independently proved frontier permissions over the same immutable raw baseline.
    /// </summary>
    /// <param name="next">
    /// Policy derived by the same sequential owner walk for a later frontier.
    /// </param>
    /// <returns>
    /// A detached combined comparison policy; different baselines are rejected.
    /// </returns>
    internal SpiritualWoundDependentDraftPolicy Merge(SpiritualWoundDependentDraftPolicy next)
    {
        if (!JsonNode.DeepEquals(_baseline, next._baseline))
            throw new InvalidOperationException("Dependent policies require the same committed raw baseline.");
        var positions = _positions.Concat(next._positions).ToArray();
        if (positions.Select(position => position.Pointer).Distinct(StringComparer.Ordinal).Count() != positions.Length)
            throw new InvalidOperationException("A position frontier cannot be diagnosed twice.");
        return new(_baseline, _pointers.Union(next._pointers, StringComparer.Ordinal)
            .ToHashSet(StringComparer.Ordinal), _forceAudits.Concat(next._forceAudits).Distinct().ToArray(), positions);
    }

    /// <summary>
    /// Rejects duplicate keys before parsing a raw root for comparison or provenance mapping.
    /// </summary>
    /// <param name="json">
    /// Complete raw JSON object; malformed or duplicate-key input throws.
    /// </param>
    /// <returns>
    /// A detached root retaining the original carrier and field spellings.
    /// </returns>
    internal static JsonObject ReadStrictRoot(string json)
    {
        using var document = JsonDocument.Parse(json);
        Unique(document.RootElement);
        return JsonNode.Parse(json)!.AsObject();

        static void Unique(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                var names = new HashSet<string>(StringComparer.Ordinal);
                foreach (var property in element.EnumerateObject())
                {
                    if (!names.Add(property.Name)) throw new JsonException("Duplicate dependent draft key.");
                    Unique(property.Value);
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
                foreach (var item in element.EnumerateArray()) Unique(item);
        }
    }

    /// <summary>
    /// Combines independently proved cost fields and exact causal position arithmetic without granting consequence edits.
    /// </summary>
    /// <param name="original">
    /// Signed original conflict used solely to resolve actual raw carrier precedence.
    /// </param>
    /// <param name="baseline">
    /// Strict committed raw conflict root.
    /// </param>
    /// <param name="issues">
    /// Correctable diagnostics from the selected decision's committed baseline probe.
    /// </param>
    /// <param name="frontier">
    /// Actual next-exchange action-point image, or null only when there are no issues.
    /// </param>
    /// <param name="positions">
    /// Owner-derived position groups, or <see langword="null"/> for the existing cost-only route.
    /// </param>
    /// <returns>
    /// A comparison policy, or <see langword="null"/> when a diagnostic lacks a proved bounded correction.
    /// </returns>
    internal static SpiritualWoundDependentDraftPolicy? Create(JsonObject original, JsonObject baseline,
        IReadOnlyList<ValidationIssue> issues, AfterlifeConflictActionPointProjection? frontier,
        IReadOnlyList<ValidationService.SpiritualPositionDraftCorrection>? positions = null)
    {
        var pointers = new HashSet<string>(StringComparer.Ordinal);
        var forceAudits = new List<(string Pointer, bool Adding)>();
        positions ??= [];
        foreach (var correction in positions)
            foreach (var pointer in correction.Fields) pointers.Add(pointer);
        foreach (var issue in issues)
        {
            if (ValidationService.PositionDependencyIndex(issue) is { } positionIndex)
            {
                var rawPosition = AfterlifeSpiritualConflictState.ResolveRawExchange(original, baseline, positionIndex);
                if (rawPosition is not { } resolved || !positions.Any(correction => correction.Pointer == resolved.Pointer + "/diceAudit"))
                    return null;
                continue;
            }
            var match = CostPath.Match(issue.FilePath);
            if (!match.Success || frontier is null ||
                !int.TryParse(match.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out var index) ||
                AfterlifeSpiritualConflictState.ResolveRawExchange(original, baseline, index) is not { } raw)
                return null;
            var side = match.Groups[2].Value;
            var pointer = raw.Pointer + "/actionCostAudit/" + side;
            var force = issue.Code is "afterlife_conflict_wound_force_cost_audit_missing" or
                "afterlife_conflict_wound_force_cost_audit_obsolete";
            if (force)
            {
                foreach (var field in ForceFields) pointers.Add(pointer + "/" + field);
                forceAudits.Add((pointer, issue.Code == "afterlife_conflict_wound_force_cost_audit_missing"));
                continue;
            }
            var fieldName = match.Groups[3].Value;
            if (fieldName is not ("effectiveCost" or "before" or "after") ||
                raw.Exchange["actionCostAudit"]?[side] is not JsonObject audit ||
                audit["before"] is not JsonValue beforeValue || !beforeValue.TryGetValue<decimal>(out var before))
                return null;
            pointers.Add(pointer + "/" + fieldName);
            pointers.Add(pointer + "/after");
            var actualBefore = side == "player" ? frontier.Player.Current : frontier.Opposition.Current;
            if (before != actualBefore) pointers.Add(pointer + "/before");
        }
        return new(baseline, pointers, forceAudits, positions.ToArray());
    }

    /// <summary>
    /// Checks permitted raw leaves and exact position groups while preserving every independent modifier and sibling.
    /// A position group must remain unchanged or be completely corrected; partial arithmetic edits are rejected.
    /// </summary>
    /// <param name="candidate">
    /// Strict current conflict root; full source validation is required separately.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for unchanged input or a bounded correction; otherwise <see langword="false"/>.
    /// </returns>
    internal bool Allows(JsonObject candidate)
    {
        if (!SameExcept(_baseline, candidate, "") ||
            _positions.Any(position => !position.Allows(ReadNode(candidate, position.Pointer)))) return false;
        foreach (var (pointer, adding) in _forceAudits)
        {
            var prior = ReadNode(_baseline, pointer);
            var current = ReadNode(candidate, pointer);
            if (JsonNode.DeepEquals(prior, current)) continue;
            if (!adding)
            {
                var separator = pointer.LastIndexOf('/');
                if (ReadNode(candidate, pointer[..separator]) is JsonObject parent &&
                    parent.ContainsKey(pointer[(separator + 1)..])) return false;
                continue;
            }
            if (!ValidationService.IsPrescribedForceCostAudit(current))
                return false;
        }
        return true;
    }

    /// <summary>
    /// Applies only uniquely prescribed position arithmetic to a detached diagnostic draft.
    /// </summary>
    /// <param name="candidate">
    /// Mutable diagnostic copy; no physical files are changed.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when all position groups project without invented narration; otherwise <see langword="false"/>.
    /// </returns>
    internal bool TryApplyPositionCorrections(JsonObject candidate)
    {
        foreach (var correction in _positions)
            if (ReadNode(candidate, correction.Pointer) is not JsonObject audit || !correction.TryApply(audit)) return false;
        return true;
    }

    /// <summary>
    /// Compares all unpermitted raw siblings, retaining array order and exact object keys.
    /// </summary>
    /// <param name="before">
    /// Baseline value, including null or absence represented by null.
    /// </param>
    /// <param name="after">
    /// Candidate value at the same raw coordinate.
    /// </param>
    /// <param name="pointer">
    /// Escaped absolute JSON pointer, or empty for the root.
    /// </param>
    /// <returns>
    /// True when differences are limited to permitted leaves and their object scaffolding.
    /// </returns>
    private bool SameExcept(JsonNode? before, JsonNode? after, string pointer)
    {
        if (_pointers.Contains(pointer) || JsonNode.DeepEquals(before, after)) return true;
        if ((before is JsonObject || before is null) && (after is JsonObject || after is null))
        {
            var left = before as JsonObject;
            var right = after as JsonObject;
            var keys = (left?.Select(pair => pair.Key) ?? []).Union(
                right?.Select(pair => pair.Key) ?? [], StringComparer.Ordinal).ToArray();
            if (keys.Length == 0) return false;
            foreach (var key in keys)
            {
                var child = pointer + "/" + key.Replace("~", "~0", StringComparison.Ordinal)
                    .Replace("/", "~1", StringComparison.Ordinal);
                var leftHas = left?.ContainsKey(key) == true;
                var rightHas = right?.ContainsKey(key) == true;
                if (leftHas != rightHas && !_pointers.Contains(child) &&
                    (!_pointers.Any(value => value.StartsWith(child + "/", StringComparison.Ordinal)) ||
                     (leftHas ? left![key] : right![key]) is not JsonObject)) return false;
                if (!SameExcept(left?[key], right?[key], child)) return false;
            }
            return true;
        }
        if (before is JsonArray first && after is JsonArray second && first.Count == second.Count)
            return first.Select((value, index) => SameExcept(value, second[index], pointer + "/" +
                index.ToString(CultureInfo.InvariantCulture))).All(value => value);
        return false;
    }

    /// <summary>
    /// Reads an internal generated pointer without accepting caller-authored path syntax.
    /// </summary>
    /// <param name="root">
    /// Strict raw root from which to read.
    /// </param>
    /// <param name="pointer">
    /// Generated pointer containing known object fields and array indices.
    /// </param>
    /// <returns>
    /// The referenced node, or null when its optional container or leaf is absent.
    /// </returns>
    private static JsonNode? ReadNode(JsonObject root, string pointer)
    {
        JsonNode? node = root;
        foreach (var part in pointer.Split('/').Skip(1))
            node = node is JsonArray array && int.TryParse(part, out var index)
                ? index >= 0 && index < array.Count ? array[index] : null
                : node is JsonObject value ? value[part] : null;
        return node;
    }
}
