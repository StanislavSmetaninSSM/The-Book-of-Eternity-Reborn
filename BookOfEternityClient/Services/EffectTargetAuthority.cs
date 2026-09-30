using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record EffectTargetKey(
    string Realm,
    string Kind,
    string TargetId);

internal sealed record EffectTargetExport(
    string Realm,
    string Kind,
    string TargetId,
    bool SameTurn,
    string? TargetRef = null,
    string? BoundNpcId = null,
    ResourceOwnerKind? BoundResourceOwnerKind = null);

internal sealed record EffectTargetAuthorityInput(
    IReadOnlyList<EffectTargetExport> PreTurnTargets,
    IReadOnlyList<EffectTargetExport> SameTurnTargets,
    IReadOnlySet<string> HistoricalTargetIds,
    CombatantIdentityState? CombatantIdentities);

internal sealed record EffectTargetResolution(
    EffectTargetKey? Target,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Target != null && Issues.Count == 0;
}

internal sealed class EffectTargetAuthority
{
    private static readonly HashSet<string> Realms = new(StringComparer.Ordinal)
    {
        "mortal_world", "chaos_sea", "shining_abode"
    };
    private static readonly HashSet<string> TargetKinds = new(StringComparer.Ordinal)
    {
        "player", "npc", "combatant", "guardian", "resident", "radiant_actor",
        "afterlife_actor", "spiritual_conflict_side"
    };

    private readonly Dictionary<EffectTargetKey, EffectTargetExport> _targets;
    private readonly Dictionary<string, List<EffectTargetKey>> _aliases;
    private readonly Dictionary<string, EffectTargetKey> _targetsByRef;
    private readonly Dictionary<string, List<string>> _refsByAlias;
    private readonly HashSet<string> _invalidRefAliases;
    private readonly HashSet<string> _historicalAliases;
    private readonly HashSet<EffectTargetKey> _invalidTargets;

    private EffectTargetAuthority(Builder builder)
    {
        _targets = new Dictionary<EffectTargetKey, EffectTargetExport>(builder.Targets);
        _aliases = builder.Aliases.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _targetsByRef = new Dictionary<string, EffectTargetKey>(builder.TargetsByRef, StringComparer.Ordinal);
        _refsByAlias = builder.RefsByAlias.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _invalidRefAliases = new HashSet<string>(builder.InvalidRefAliases, StringComparer.Ordinal);
        _historicalAliases = new HashSet<string>(builder.HistoricalAliases, StringComparer.Ordinal);
        _invalidTargets = new HashSet<EffectTargetKey>(builder.InvalidTargets);
        Issues = builder.Issues.ToArray();
        Fingerprint = CreateFingerprint(_targets.Values, _targetsByRef, Issues);
        CanonicalFingerprint = CreateCanonicalFingerprint(_targets.Values, Issues);
    }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal string Fingerprint { get; }

    internal string CanonicalFingerprint { get; }

    internal bool IsCanonicalPublicationSubsetOf(
        EffectTargetAuthority canonicalAuthority)
    {
        ArgumentNullException.ThrowIfNull(canonicalAuthority);
        if (Issues.Count != 0 || canonicalAuthority.Issues.Count != 0)
            return false;

        var targetKeys = _targets.Keys.ToHashSet();
        var canonicalTargets = canonicalAuthority._targets
            .Where(pair => targetKeys.Contains(pair.Key))
            .Select(static pair => pair.Value)
            .ToArray();
        if (canonicalTargets.Length != _targets.Count)
            return false;

        var canonicalSubsetFingerprint = CreateCanonicalFingerprint(
            canonicalTargets,
            Array.Empty<ValidationIssue>());
        return string.Equals(
            CanonicalFingerprint,
            canonicalSubsetFingerprint,
            StringComparison.Ordinal);
    }

    internal static EffectTargetAuthority Build(EffectTargetAuthorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new Builder(input.HistoricalTargetIds);
        builder.AddRange(input.PreTurnTargets, sameTurn: false);
        builder.AddRange(input.SameTurnTargets, sameTurn: true);
        if (input.CombatantIdentities != null)
        {
            foreach (var pair in input.CombatantIdentities.CombatantIdsByRef)
            {
                input.CombatantIdentities.TryGetBoundNpcId(
                    pair.Key,
                    out var boundNpcId);
                builder.Add(new EffectTargetExport(
                    "mortal_world",
                    "combatant",
                    pair.Value,
                    SameTurn: true,
                    TargetRef: pair.Key,
                    BoundNpcId: boundNpcId), sameTurn: true);
            }
        }
        return new EffectTargetAuthority(builder);
    }

    internal IReadOnlyList<ValidationIssue> ValidateNamedCombatantBindings(
        EffectCarrierCatalogInput carriers)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        var issues = new List<ValidationIssue>();
        ValidateNamedCombatantBindings(
            carriers.EnemyCombatants,
            EffectCarrierCatalog.EnemiesPath,
            "enemiesData",
            issues);
        ValidateNamedCombatantBindings(
            carriers.AllyCombatants,
            EffectCarrierCatalog.AlliesPath,
            "alliesData",
            issues);
        return issues;
    }

    private void ValidateNamedCombatantBindings(
        JsonObject? root,
        string path,
        string collection,
        List<ValidationIssue> issues)
    {
        if (root?[collection] is not JsonArray combatants)
            return;
        for (var index = 0; index < combatants.Count; index++)
        {
            if (combatants[index] is not JsonObject combatant ||
                !combatant.TryGetPropertyValue("NPCId", out var npcNode) ||
                npcNode == null)
            {
                continue;
            }

            var issuePath = $"{path}.{collection}[{index}].NPCId";
            if (!TryReadExact(npcNode, out var npcId))
            {
                issues.Add(NewCombatantBindingIssue(
                    issuePath,
                    "effect_target_combatant_npc_binding_invalid",
                    "null or one exact non-empty NPCId",
                    npcNode.ToJsonString()));
                continue;
            }

            var key = new EffectTargetKey("mortal_world", "npc", npcId);
            if (_targets.ContainsKey(key) && !_invalidTargets.Contains(key))
                continue;

            var alias = Alias(key.Realm, key.Kind, key.TargetId);
            var code = _aliases.TryGetValue(alias, out var candidates) && candidates.Count > 0
                ? "effect_target_combatant_npc_binding_confusable"
                : "effect_target_combatant_npc_binding_unresolved";
            issues.Add(NewCombatantBindingIssue(
                issuePath,
                code,
                "one exact accepted named-NPC identity in the composed target authority",
                npcId));
        }
    }

    internal EffectTargetResolution Resolve(JsonObject selector, string realm)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var issues = new List<ValidationIssue>();
        if (!Realms.Contains(realm) || !TryReadExact(selector["kind"], out var kind) || !TargetKinds.Contains(kind))
        {
            Add(issues, "target", "effect_target_selector_invalid", "closed target kind and current realm", selector.ToJsonString());
            return new EffectTargetResolution(null, issues);
        }
        var hasId = selector.ContainsKey("targetId") && selector["targetId"] != null;
        var hasRef = selector.ContainsKey("targetRef") && selector["targetRef"] != null;
        if (hasId == hasRef || selector.Any(pair => pair.Key is not ("kind" or "targetId" or "targetRef")))
        {
            Add(issues, "target", "effect_target_selector_invalid", "kind plus exactly one targetId or targetRef", selector.ToJsonString());
            return new EffectTargetResolution(null, issues);
        }

        if (hasRef)
            return ResolveRef(selector["targetRef"], realm, kind);
        if (!TryReadExact(selector["targetId"], out var targetId))
        {
            Add(issues, "target.targetId", "effect_target_selector_invalid", "exact non-empty targetId", selector["targetId"]?.ToJsonString() ?? "missing");
            return new EffectTargetResolution(null, issues);
        }

        var key = new EffectTargetKey(realm, kind, targetId);
        if (_targets.TryGetValue(key, out var target) && !_invalidTargets.Contains(key))
        {
            if (!target.SameTurn)
                return new EffectTargetResolution(key, Array.Empty<ValidationIssue>());
            Add(issues, "target.targetId", "effect_target_same_turn_id_forbidden", "same-turn targetRef; client-owned targetId is not GM-selectable", targetId);
            return new EffectTargetResolution(null, issues);
        }

        var alias = Alias(realm, kind, targetId);
        if (_aliases.TryGetValue(alias, out var candidates) && candidates.Count > 0)
        {
            Add(issues, "target.targetId", "effect_target_selector_confusable", "one exact ordinal targetId", targetId);
        }
        else if (_historicalAliases.Contains(MortalLocationIdentityState.BuildConfusableKey(targetId)))
        {
            Add(issues, "target.targetId", "effect_target_selector_historical", "current non-historical targetId", targetId);
        }
        else if (_targets.Keys.Any(candidate =>
                     string.Equals(candidate.Kind, kind, StringComparison.Ordinal) &&
                     string.Equals(candidate.TargetId, targetId, StringComparison.Ordinal) &&
                     !string.Equals(candidate.Realm, realm, StringComparison.Ordinal)))
        {
            Add(issues, "target", "effect_target_realm_mismatch", "target realm equal to requested effect realm", realm);
        }
        else
        {
            Add(issues, "target", "effect_target_selector_unresolved", "one exact current target authority", key.ToString());
        }
        return new EffectTargetResolution(null, issues);
    }

    internal bool TryResolveAcceptedTarget(
        EffectTargetKey key,
        out EffectTargetExport? target)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (_targets.TryGetValue(key, out var candidate) &&
            !_invalidTargets.Contains(key))
        {
            target = candidate with { };
            return true;
        }

        target = null;
        return false;
    }

    private EffectTargetResolution ResolveRef(JsonNode? node, string realm, string kind)
    {
        var issues = new List<ValidationIssue>();
        if (!TryReadExact(node, out var targetRef))
        {
            Add(issues, "target.targetRef", "effect_target_selector_invalid", "exact same-turn targetRef", node?.ToJsonString() ?? "missing");
            return new EffectTargetResolution(null, issues);
        }
        var alias = MortalLocationIdentityState.BuildConfusableKey(targetRef);
        if (_invalidRefAliases.Contains(alias))
        {
            Add(issues, "target.targetRef", "effect_target_selector_ambiguous", "one exact and non-confusable same-turn targetRef", targetRef);
            return new EffectTargetResolution(null, issues);
        }
        if (_targetsByRef.TryGetValue(targetRef, out var key) &&
            string.Equals(key.Realm, realm, StringComparison.Ordinal) &&
            string.Equals(key.Kind, kind, StringComparison.Ordinal) &&
            !_invalidTargets.Contains(key))
        {
            return new EffectTargetResolution(key, Array.Empty<ValidationIssue>());
        }
        if (_refsByAlias.TryGetValue(alias, out var aliases) && aliases.Count > 0)
            Add(issues, "target.targetRef", "effect_target_selector_confusable", "one exact same-turn targetRef", targetRef);
        else
            Add(issues, "target.targetRef", "effect_target_selector_unresolved", "one accepted same-turn targetRef export", targetRef);
        return new EffectTargetResolution(null, issues);
    }

    private static string CreateFingerprint(
        IEnumerable<EffectTargetExport> targets,
        IReadOnlyDictionary<string, EffectTargetKey> refs,
        IReadOnlyList<ValidationIssue> issues)
    {
        var root = new JsonObject
        {
            ["targets"] = new JsonArray(targets
                .OrderBy(static target => target.Realm, StringComparer.Ordinal)
                .ThenBy(static target => target.Kind, StringComparer.Ordinal)
                .ThenBy(static target => target.TargetId, StringComparer.Ordinal)
                .Select(target => (JsonNode)new JsonObject
                {
                    ["realm"] = target.Realm,
                    ["kind"] = target.Kind,
                    ["targetId"] = target.TargetId,
                    ["sameTurn"] = target.SameTurn,
                    ["targetRef"] = target.TargetRef,
                    ["boundNpcId"] = target.BoundNpcId,
                    ["boundResourceOwnerKind"] =
                        target.BoundResourceOwnerKind?.ToString()
                }).ToArray()),
            ["refs"] = new JsonArray(refs
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (JsonNode)new JsonObject
                {
                    ["targetRef"] = pair.Key,
                    ["realm"] = pair.Value.Realm,
                    ["kind"] = pair.Value.Kind,
                    ["targetId"] = pair.Value.TargetId
                }).ToArray()),
            ["issues"] = new JsonArray(issues.Select(issue => (JsonNode)new JsonObject
            {
                ["code"] = issue.Code,
                ["path"] = issue.FilePath
            }).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static string CreateCanonicalFingerprint(
        IEnumerable<EffectTargetExport> targets,
        IReadOnlyList<ValidationIssue> issues)
    {
        var root = new JsonObject
        {
            ["targets"] = new JsonArray(targets
                .OrderBy(static target => target.Realm, StringComparer.Ordinal)
                .ThenBy(static target => target.Kind, StringComparer.Ordinal)
                .ThenBy(static target => target.TargetId, StringComparer.Ordinal)
                .Select(target => (JsonNode)new JsonObject
                {
                    ["realm"] = target.Realm,
                    ["kind"] = target.Kind,
                    ["targetId"] = target.TargetId,
                    ["boundNpcId"] = target.BoundNpcId,
                    ["boundResourceOwnerKind"] =
                        target.BoundResourceOwnerKind?.ToString()
                }).ToArray()),
            ["issues"] = new JsonArray(issues
                .OrderBy(static issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
                .Select(issue => (JsonNode)new JsonObject
                {
                    ["code"] = issue.Code,
                    ["path"] = issue.FilePath
                }).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static string Alias(string realm, string kind, string targetId) =>
        realm + "\u001f" + kind + "\u001f" + MortalLocationIdentityState.BuildConfusableKey(targetId);

    private static bool TryReadExact(JsonNode? node, out string value)
    {
        value = node is JsonValue jsonValue && jsonValue.TryGetValue<string>(out var text) ? text : string.Empty;
        return value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }

    private static void Add(List<ValidationIssue> issues, string path, string code, string expected, string actual) =>
        issues.Add(NewIssue(path, code, expected, actual));

    private static ValidationIssue NewIssue(string path, string code, string expected, string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Effect target selector violates exact owner authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact current targetId or one accepted same-turn targetRef; do not use names, indices, aliases, historical IDs, or cross-realm targets.");

    private static ValidationIssue NewCombatantBindingIssue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Named combatant binding does not match exact accepted NPC authority.",
            code: code,
            section: "effect_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use null for an anonymous combatant or the exact NPCId of one accepted named NPC; do not infer an NPC from a name or alias.");

    private sealed class Builder
    {
        internal Dictionary<EffectTargetKey, EffectTargetExport> Targets { get; } = new();
        internal Dictionary<string, List<EffectTargetKey>> Aliases { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, EffectTargetKey> TargetsByRef { get; } = new(StringComparer.Ordinal);
        internal Dictionary<string, List<string>> RefsByAlias { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> InvalidRefAliases { get; } = new(StringComparer.Ordinal);
        internal HashSet<string> HistoricalAliases { get; }
        internal HashSet<EffectTargetKey> InvalidTargets { get; } = new();
        internal List<ValidationIssue> Issues { get; } = new();

        internal Builder(IEnumerable<string> historicalTargetIds)
        {
            HistoricalAliases = historicalTargetIds
                .Select(MortalLocationIdentityState.BuildConfusableKey)
                .ToHashSet(StringComparer.Ordinal);
        }

        internal void AddRange(IEnumerable<EffectTargetExport> exports, bool sameTurn)
        {
            foreach (var export in exports)
                Add(export, sameTurn);
        }

        internal void Add(EffectTargetExport export, bool sameTurn)
        {
            var path = $"targets[{export.Kind}:{export.TargetId}]";
            if (!Realms.Contains(export.Realm) || !TargetKinds.Contains(export.Kind) ||
                !TryExact(export.TargetId) || export.TargetRef != null && !TryExact(export.TargetRef) ||
                export.BoundNpcId != null && !TryExact(export.BoundNpcId) ||
                export.BoundResourceOwnerKind != null &&
                (export.Kind != "combatant" ||
                 export.BoundResourceOwnerKind is not (
                     ResourceOwnerKind.Combatant or
                     ResourceOwnerKind.CombatGroupMember or
                     ResourceOwnerKind.Npc)))
            {
                Issues.Add(NewIssue(path, "effect_target_authority_invalid_export", "exact supported target export", export.ToString()));
                return;
            }
            if (sameTurn && export.TargetRef == null)
            {
                Issues.Add(NewIssue(path + ".targetRef", "effect_target_authority_ref_required", "exact same-turn targetRef", "missing"));
                return;
            }
            var normalized = export with { SameTurn = sameTurn };
            var key = new EffectTargetKey(export.Realm, export.Kind, export.TargetId);
            if (!Targets.TryAdd(key, normalized))
            {
                InvalidTargets.Add(key);
                Issues.Add(NewIssue(path, "effect_target_authority_duplicate_target", "one exact target export", key.ToString()));
            }
            var alias = Alias(key.Realm, key.Kind, key.TargetId);
            if (!Aliases.TryGetValue(alias, out var aliasTargets))
            {
                aliasTargets = new List<EffectTargetKey>();
                Aliases.Add(alias, aliasTargets);
            }
            aliasTargets.Add(key);
            if (aliasTargets.Any(candidate => !Equals(candidate, key)))
            {
                InvalidTargets.Add(key);
                foreach (var candidate in aliasTargets)
                    InvalidTargets.Add(candidate);
                Issues.Add(NewIssue(path, "effect_target_authority_confusable_target", "one exact/confusable target export", key.ToString()));
            }

            if (normalized.TargetRef != null)
            {
                var refAlias = MortalLocationIdentityState.BuildConfusableKey(normalized.TargetRef);
                if (!sameTurn || !TargetsByRef.TryAdd(normalized.TargetRef, key))
                {
                    InvalidTargets.Add(key);
                    InvalidRefAliases.Add(refAlias);
                    Issues.Add(NewIssue(path + ".targetRef", "effect_target_authority_duplicate_ref", "one exact same-turn targetRef", normalized.TargetRef));
                }
                if (!RefsByAlias.TryGetValue(refAlias, out var refs))
                {
                    refs = new List<string>();
                    RefsByAlias.Add(refAlias, refs);
                }
                refs.Add(normalized.TargetRef);
                if (refs.Distinct(StringComparer.Ordinal).Count() > 1)
                {
                    InvalidTargets.Add(key);
                    InvalidRefAliases.Add(refAlias);
                    Issues.Add(NewIssue(path + ".targetRef", "effect_target_authority_confusable_ref", "one exact/confusable same-turn targetRef", normalized.TargetRef));
                }
            }
        }

        private static bool TryExact(string value) =>
            value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }
}
