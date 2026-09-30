using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum EffectRollSkillScopeState
{
    Usable,
    Unavailable,
    Missing,
    InvalidAuthority
}

internal sealed record EffectRollSkillScopeRow(
    EffectTargetKey Target,
    string SkillId,
    string DisplayName,
    string Lifecycle,
    bool Active,
    string SourcePath);

internal sealed record EffectRollSkillScopeResolution(
    EffectRollSkillScopeState State,
    string? DisplayName,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => State != EffectRollSkillScopeState.InvalidAuthority;
    internal bool IsUsable => State == EffectRollSkillScopeState.Usable;
}

internal sealed record EffectRollSkillScopeAuthorityInput(
    IReadOnlyDictionary<string, JsonNode?> OfferedRoots,
    IReadOnlyDictionary<string, JsonNode?> CurrentRoots);

/// <summary>Detached exact skill identity, separate from broad roll-scope mechanics.</summary>
internal sealed class EffectRollSkillScopeAuthority
{
    private const string ActivePath = "game_state/player/skills_active.json";
    private const string PassivePath = "game_state/player/skills_passive.json";
    private const string NpcPath = "game_state/npcs/npc_core.json";
    private const int MaxTargets = 128;
    private const int MaxSkillsPerTarget = 128;
    private const int MaxSelectableRows = 2048;
    private static readonly FrozenSet<string> TerminalStates = new[]
    {
        "completed", "failed", "abandoned", "cancelled", "resolved", "removed",
        "inactive", "disabled", "locked", "healed", "closed", "consumed", "destroyed"
    }.ToFrozenSet(StringComparer.OrdinalIgnoreCase);

    private readonly Catalog _offered;
    private readonly Catalog _current;

    private EffectRollSkillScopeAuthority(Catalog offered, Catalog current)
    {
        _offered = offered;
        _current = current;
        var seal = new JsonObject
        {
            ["offered"] = offered.CreateFingerprintData(),
            ["current"] = current.CreateFingerprintData()
        };
        Fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seal.ToJsonString())));
    }

    internal string Fingerprint { get; }

    internal static EffectRollSkillScopeAuthority Build(EffectRollSkillScopeAuthorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new EffectRollSkillScopeAuthority(BuildCatalog(input.OfferedRoots), BuildCatalog(input.CurrentRoots));
    }

    internal EffectRollSkillScopeResolution ResolveForNewBinding(
        EffectTargetKey target,
        string skillId,
        string path,
        string section = "effect_materialization")
    {
        var offered = _offered.Resolve(target, skillId, path, section);
        var current = _current.Resolve(target, skillId, path, section);
        if (!offered.IsValid) return offered;
        if (!current.IsValid) return current;
        if (offered.IsUsable && current.IsUsable) return current;

        var failure = !offered.IsUsable ? offered : current;
        return failure with
        {
            Issues = Issue(path, section, "effect_roll_skill_scope_unavailable",
                "An exact skill must be usable in both the offered and current target catalogs.", skillId)
        };
    }

    internal EffectRollSkillScopeResolution ResolveCurrent(EffectTargetKey target, string skillId, string path) =>
        _current.Resolve(target, skillId, path, "effect_materialization");

    internal IReadOnlyList<ValidationIssue> ValidateNewComponents(
        EffectTargetKey target,
        JsonArray components,
        string path,
        string section = "effect_materialization")
    {
        var issues = new List<ValidationIssue>();
        for (var index = 0; index < components.Count; index++)
        {
            // The closed payload/profile contract is validated by EffectComponentProfiles.
            if (components[index] is not JsonObject component || Read(component["profile"]) != "roll_modifier" ||
                component["payload"] is not JsonObject payload || payload["scope"] is not JsonObject scope ||
                Read(scope["kind"]) != "skill" || Read(scope["skillId"]) is not { } skillId)
                continue;
            issues.AddRange(ResolveForNewBinding(target, skillId,
                $"{path}[{index}].payload.scope.skillId", section).Issues);
        }
        return issues.AsReadOnly();
    }

    internal JsonObject CreateGmCatalog()
    {
        var targets = new JsonArray();
        foreach (var group in _offered.SelectableRows().GroupBy(row => row.Target))
        {
            targets.Add(new JsonObject
            {
                ["realm"] = group.Key.Realm,
                ["kind"] = group.Key.Kind,
                ["targetId"] = group.Key.TargetId,
                ["skills"] = new JsonArray(group.Select(row => (JsonNode?)new JsonObject
                {
                    ["skillId"] = row.SkillId, ["displayName"] = row.DisplayName
                }).ToArray())
            });
        }
        return new JsonObject { ["schemaVersion"] = 1, ["targets"] = targets };
    }

    private static Catalog BuildCatalog(IReadOnlyDictionary<string, JsonNode?> roots)
    {
        ArgumentNullException.ThrowIfNull(roots);
        // Only canonical roots participate. Clone before enumeration; no input JSON is retained.
        var snapshot = new[] { ActivePath, PassivePath, NpcPath }.ToDictionary(
            path => path, path => roots.TryGetValue(path, out var root) ? root?.DeepClone() : null,
            StringComparer.Ordinal);
        var rows = new List<EffectRollSkillScopeRow>();
        var player = new EffectTargetKey("mortal_world", "player", "player_current");
        AddPlayerRows(ActivePath, "activeSkillChanges");
        AddPlayerRows(PassivePath, "passiveSkillChanges");
        foreach (var actor in EffectAcceptedTurnInputComposer.EnumerateCanonicalNpcActors(snapshot[NpcPath]))
        {
            if (!GuardianPolicyContracts.TryResolveStrictPermanentNpcId(actor, out var npcId)) continue;
            var target = new EffectTargetKey("mortal_world", "npc", npcId);
            AddRows(actor["activeSkills"], target, NpcPath + ".activeSkills");
            AddRows(actor["passiveSkills"], target, NpcPath + ".passiveSkills");
        }
        return new Catalog(rows);

        void AddPlayerRows(string path, string changeProperty)
        {
            if (snapshot[path] is JsonArray array)
                AddRows(array, player, path);
            else if (snapshot[path] is JsonObject root)
            {
                AddRows(root[changeProperty], player, path + "." + changeProperty);
                AddRows(root["skills"], player, path + ".skills");
            }
        }

        void AddRows(JsonNode? node, EffectTargetKey target, string path)
        {
            if (node is not JsonArray skills) return;
            foreach (var skill in skills.OfType<JsonObject>())
            {
                if (Read(skill["skillId"]) is not { } id) continue;
                var lifecycle = new[] { "lifecycle", "status", "state", "availability" }
                    .Select(field => Read(skill[field])).Where(value => value != null).ToArray();
                var active = !IsBoolean(skill["active"], false) && !IsBoolean(skill["isActive"], false) &&
                    !IsBoolean(skill["isInactive"], true) && !lifecycle.Any(value => TerminalStates.Contains(value!));
                rows.Add(new EffectRollSkillScopeRow(target, id,
                    Read(skill["displayName"]) ?? Read(skill["skillName"]) ?? Read(skill["name"]) ?? "Навык",
                    lifecycle.FirstOrDefault() ?? "active", active, path));
            }
        }
    }

    private static string? Read(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text)
            ? text : null;

    private static bool IsBoolean(JsonNode? node, bool expected) =>
        node is JsonValue value && value.TryGetValue<bool>(out var actual) && actual == expected;

    private static IReadOnlyList<ValidationIssue> Issue(string path, string section, string code, string message, string id) =>
        Array.AsReadOnly(new[] { new ValidationIssue(path, IssueSeverity.Error, message, code: code, section: section,
            expected: "one exact usable target skill without exact/confusable competitors in each catalog",
            actual: id, repairHint: "Select an offered usable permanent skillId belonging to the exact effect target.") });

    private static IOrderedEnumerable<EffectRollSkillScopeRow> Ordered(IEnumerable<EffectRollSkillScopeRow> rows) =>
        rows.OrderBy(row => row.Target.Realm, StringComparer.Ordinal)
            .ThenBy(row => row.Target.Kind, StringComparer.Ordinal)
            .ThenBy(row => row.Target.TargetId, StringComparer.Ordinal)
            .ThenBy(row => row.SkillId, StringComparer.Ordinal)
            .ThenBy(row => row.DisplayName, StringComparer.Ordinal)
            .ThenBy(row => row.Lifecycle, StringComparer.Ordinal)
            .ThenBy(row => row.Active)
            .ThenBy(row => row.SourcePath, StringComparer.Ordinal);

    private sealed class Catalog
    {
        private readonly IReadOnlyList<EffectRollSkillScopeRow> _rows;
        private readonly FrozenDictionary<EffectTargetKey, FrozenDictionary<string, EffectRollSkillScopeRow[]>> _exact;
        private readonly FrozenDictionary<EffectTargetKey, FrozenDictionary<string, int>> _competitorCounts;
        private readonly FrozenSet<EffectTargetKey> _oversizedTargets;
        private readonly bool _overCatalogBound;

        internal Catalog(IEnumerable<EffectRollSkillScopeRow> rows)
        {
            _rows = Array.AsReadOnly(Ordered(rows).ToArray());
            _exact = _rows.GroupBy(row => row.Target).ToFrozenDictionary(group => group.Key,
                group => group.GroupBy(row => row.SkillId, StringComparer.Ordinal)
                    .ToFrozenDictionary(skills => skills.Key, skills => skills.ToArray(), StringComparer.Ordinal));
            _competitorCounts = _rows.GroupBy(row => row.Target).ToFrozenDictionary(group => group.Key,
                group => group.GroupBy(row => MortalLocationIdentityState.BuildConfusableKey(row.SkillId), StringComparer.Ordinal)
                    .ToFrozenDictionary(skills => skills.Key, skills => skills.Count(), StringComparer.Ordinal));
            var selectable = _rows.Where(IsUniqueUsable).ToArray();
            var counts = selectable.GroupBy(row => row.Target).ToDictionary(group => group.Key, group => group.Count());
            _oversizedTargets = counts.Where(pair => pair.Value > MaxSkillsPerTarget).Select(pair => pair.Key).ToFrozenSet();
            _overCatalogBound = counts.Count > MaxTargets || selectable.Length > MaxSelectableRows;
        }

        internal EffectRollSkillScopeResolution Resolve(EffectTargetKey target, string id, string path, string section)
        {
            if (!_exact.TryGetValue(target, out var skills) || !skills.TryGetValue(id, out var matches))
                return new(EffectRollSkillScopeState.Missing, null, Array.Empty<ValidationIssue>());
            if (_overCatalogBound || _oversizedTargets.Contains(target) || matches.Length != 1 ||
                _competitorCounts[target][MortalLocationIdentityState.BuildConfusableKey(id)] != 1)
                return new(EffectRollSkillScopeState.InvalidAuthority, null,
                    Issue(path, section, "effect_roll_skill_scope_invalid_authority",
                        "The target skill catalog is ambiguous or exceeds its selectable bounds.", id));
            return new(matches[0].Active ? EffectRollSkillScopeState.Usable : EffectRollSkillScopeState.Unavailable,
                matches[0].DisplayName, Array.Empty<ValidationIssue>());
        }

        internal IEnumerable<EffectRollSkillScopeRow> SelectableRows() =>
            _overCatalogBound ? Enumerable.Empty<EffectRollSkillScopeRow>() :
                _rows.Where(row => !_oversizedTargets.Contains(row.Target) && IsUniqueUsable(row));

        private bool IsUniqueUsable(EffectRollSkillScopeRow row) =>
            row.Active && _exact[row.Target][row.SkillId].Length == 1 &&
            _competitorCounts[row.Target][MortalLocationIdentityState.BuildConfusableKey(row.SkillId)] == 1;

        internal JsonObject CreateFingerprintData() => new()
        {
            ["overCatalogBound"] = _overCatalogBound,
            ["rows"] = new JsonArray(_rows.Select(row => (JsonNode?)new JsonObject
            {
                ["realm"] = row.Target.Realm, ["kind"] = row.Target.Kind, ["targetId"] = row.Target.TargetId,
                ["skillId"] = row.SkillId, ["displayName"] = row.DisplayName, ["lifecycle"] = row.Lifecycle,
                ["active"] = row.Active, ["sourcePath"] = row.SourcePath,
                ["overTargetBound"] = _oversizedTargets.Contains(row.Target)
            }).ToArray())
        };
    }
}
