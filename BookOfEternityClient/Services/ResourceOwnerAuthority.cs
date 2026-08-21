using System.Collections;
using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum ResourceOwnerLifecycle
{
    Active,
    Suspended,
    Terminal
}

internal sealed record ResourceOwnerKey(
    string Realm,
    ResourceOwnerKind OwnerKind,
    string ResourceOwnerId);

internal sealed record ResourceOwnerExport(
    ResourceOwnerKey Key,
    ResourceOwnerLifecycle Lifecycle,
    bool SameTurn,
    string? OwnerRef,
    string? BoundNpcId,
    IReadOnlySet<string> ResourceCapabilities,
    string AuthorityFingerprint)
{
    internal IReadOnlySet<string> RealmIndependentResourceCapabilities { get; init; } =
        new ResourceReadOnlySet<string>(Array.Empty<string>(), StringComparer.Ordinal);
}

internal sealed record ResourceOwnerAuthorityInput(
    IReadOnlyList<ResourceOwnerExport> PreTurnOwners,
    IReadOnlyList<ResourceOwnerExport> SameTurnOwners,
    IReadOnlyList<ResourceOwnerKey> HistoricalOwners);

internal sealed record ResourceOwnerRequest(
    string Realm,
    ResourceOwnerKind OwnerKind,
    string ResourceKey,
    string? ResourceOwnerId,
    string? OwnerRef);

internal sealed record ResourceOwnerAuthorityEntry(
    ResourceOwnerKey Key,
    ResourceOwnerLifecycle Lifecycle,
    bool SameTurn,
    string? SameTurnRef,
    string? BoundNpcId,
    IReadOnlySet<string> ResourceCapabilities,
    string AuthorityFingerprint)
{
    internal IReadOnlySet<string> RealmIndependentResourceCapabilities { get; init; } =
        new ResourceReadOnlySet<string>(Array.Empty<string>(), StringComparer.Ordinal);

    internal bool IsResourceCapabilityActive(string resourceKey) =>
        Lifecycle != ResourceOwnerLifecycle.Terminal &&
        (Lifecycle == ResourceOwnerLifecycle.Active ||
         RealmIndependentResourceCapabilities.Contains(resourceKey));
}

internal sealed record ResourceOwnerAuthorityResolution(
    ResourceOwnerAuthorityEntry? Entry,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Entry != null && Issues.Count == 0;
}

internal sealed class ResourceOwnerAuthority
{
    private static readonly HashSet<string> Realms = new(StringComparer.Ordinal)
    {
        "mortal_world", "chaos_sea", "shining_abode"
    };

    private readonly Dictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry> _entries;
    private readonly Dictionary<string, ResourceOwnerAuthorityEntry> _sameTurnRefs;
    private readonly Dictionary<string, List<ResourceOwnerKey>> _ownerAliases;
    private readonly Dictionary<string, List<string>> _refAliases;
    private readonly HashSet<ResourceOwnerKey> _historicalOwners;
    private readonly HashSet<string> _historicalAliases;
    private readonly ReadOnlyDictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry>
        _readOnlyEntries;

    private ResourceOwnerAuthority(Builder builder)
    {
        _entries = builder.Entries.ToDictionary(
            static pair => pair.Key,
            static pair => CopyEntry(pair.Value));
        _sameTurnRefs = builder.SameTurnRefs.ToDictionary(
            static pair => pair.Key,
            static pair => CopyEntry(pair.Value),
            StringComparer.Ordinal);
        _ownerAliases = builder.OwnerAliases.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _refAliases = builder.RefAliases.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _historicalOwners = new HashSet<ResourceOwnerKey>(
            builder.HistoricalOwners,
            ResourceOwnerKeyComparer.Instance);
        _historicalAliases = new HashSet<string>(builder.HistoricalAliases, StringComparer.Ordinal);
        _readOnlyEntries = new ReadOnlyDictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry>(
            _entries);
        Issues = builder.Issues.ToArray();
        Fingerprint = CreateFingerprint(
            _entries.Values,
            _sameTurnRefs,
            builder.HistoricalOwners,
            Issues);
    }

    internal IReadOnlyDictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry> Entries =>
        _readOnlyEntries;

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal string Fingerprint { get; }

    internal ResourceOwnerAuthorityInput ExportInput()
    {
        var preTurn = new List<ResourceOwnerExport>();
        var sameTurn = new List<ResourceOwnerExport>();
        foreach (var entry in _entries.Values.OrderBy(
                     static value => value.Key.Realm,
                     StringComparer.Ordinal).ThenBy(
                     static value => value.Key.OwnerKind).ThenBy(
                     static value => value.Key.ResourceOwnerId,
                     StringComparer.Ordinal))
        {
            var export = new ResourceOwnerExport(
                entry.Key,
                entry.Lifecycle,
                entry.SameTurn,
                entry.SameTurnRef,
                entry.BoundNpcId,
                new HashSet<string>(entry.ResourceCapabilities, StringComparer.Ordinal),
                entry.AuthorityFingerprint)
            {
                RealmIndependentResourceCapabilities = new HashSet<string>(
                    entry.RealmIndependentResourceCapabilities,
                    StringComparer.Ordinal)
            };
            (entry.SameTurn ? sameTurn : preTurn).Add(export);
        }

        return new ResourceOwnerAuthorityInput(
            preTurn,
            sameTurn,
            _historicalOwners.OrderBy(
                    static value => value.Realm,
                    StringComparer.Ordinal)
                .ThenBy(static value => value.OwnerKind)
                .ThenBy(
                    static value => value.ResourceOwnerId,
                    StringComparer.Ordinal)
                .ToArray());
    }

    internal static ResourceOwnerAuthority Combine(
        params ResourceOwnerAuthority[] authorities)
    {
        ArgumentNullException.ThrowIfNull(authorities);
        var preTurn = new List<ResourceOwnerExport>();
        var sameTurn = new List<ResourceOwnerExport>();
        var historical = new List<ResourceOwnerKey>();
        foreach (var authority in authorities)
        {
            ArgumentNullException.ThrowIfNull(authority);
            var input = authority.ExportInput();
            preTurn.AddRange(input.PreTurnOwners);
            sameTurn.AddRange(input.SameTurnOwners);
            historical.AddRange(input.HistoricalOwners);
        }

        return Build(new ResourceOwnerAuthorityInput(
            preTurn,
            sameTurn,
            historical));
    }

    internal static ResourceOwnerAuthority Build(ResourceOwnerAuthorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new Builder(input.HistoricalOwners);
        builder.AddRange(input.PreTurnOwners, sameTurn: false, "preTurnOwners");
        builder.AddRange(input.SameTurnOwners, sameTurn: true, "sameTurnOwners");
        return new ResourceOwnerAuthority(builder);
    }

    internal static ResourceOwnerAuthority CreateCurrentPlayerAuthority(
        ResourceDefinitionCatalog definitions,
        IEnumerable<string?>? sameTurnCapabilities = null)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        var capabilities = definitions.Definitions
            .Where(static definition =>
                definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.Player))
            .Select(static definition => definition.ResourceKey)
            .Concat(sameTurnCapabilities ?? Array.Empty<string?>())
            .Where(static key => ResourceMaterializationContract.IsExactIdentifier(key))
            .Select(static key => key!)
            .ToHashSet(StringComparer.Ordinal);
        using var fingerprintBuilder = new ResourceFingerprintBuilder(
            "resource-owner-player-current-v1");
        foreach (var capability in capabilities.OrderBy(
                     static value => value,
                     StringComparer.Ordinal))
        {
            fingerprintBuilder.Append(capability);
        }
        var fingerprint = fingerprintBuilder.Build();
        return Build(new ResourceOwnerAuthorityInput(
            new[]
            {
                new ResourceOwnerExport(
                    new ResourceOwnerKey(
                        "mortal_world",
                        ResourceOwnerKind.Player,
                        "player_current"),
                    ResourceOwnerLifecycle.Active,
                    SameTurn: false,
                    OwnerRef: null,
                    BoundNpcId: null,
                    capabilities,
                    fingerprint)
            },
            Array.Empty<ResourceOwnerExport>(),
            Array.Empty<ResourceOwnerKey>()));
    }

    internal ResourceOwnerAuthorityResolution Resolve(ResourceOwnerRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<ValidationIssue>();
        if (Issues.Count > 0)
        {
            Add(
                issues,
                "owner",
                "resource_owner_authority_invalid",
                "issue-free composed owner authority",
                Issues.Count.ToString());
            return new ResourceOwnerAuthorityResolution(null, issues);
        }

        if (!IsRealmAllowed(request.Realm, request.OwnerKind) ||
            !ResourceMaterializationContract.IsExactIdentifier(request.ResourceKey))
        {
            Add(
                issues,
                "owner",
                "resource_owner_selector_invalid",
                "closed owner realm/kind and exact resource key",
                DescribeRequest(request));
            return new ResourceOwnerAuthorityResolution(null, issues);
        }

        var hasId = request.ResourceOwnerId != null;
        var hasRef = request.OwnerRef != null;
        if (hasId == hasRef)
        {
            Add(
                issues,
                "owner",
                "resource_owner_selector_invalid",
                "exactly one resourceOwnerId or ownerRef",
                DescribeRequest(request));
            return new ResourceOwnerAuthorityResolution(null, issues);
        }

        ResourceOwnerAuthorityEntry? entry;
        if (hasRef)
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(request.OwnerRef))
            {
                Add(issues, "owner.ownerRef", "resource_owner_ref_invalid", "exact same-turn ref", request.OwnerRef!);
                return new ResourceOwnerAuthorityResolution(null, issues);
            }

            if (!_sameTurnRefs.TryGetValue(request.OwnerRef!, out entry))
            {
                var alias = ResourceMaterializationContract.BuildConfusableKey(request.OwnerRef!);
                Add(
                    issues,
                    "owner.ownerRef",
                    _refAliases.ContainsKey(alias)
                        ? "resource_owner_ref_confusable"
                        : "resource_owner_unresolved",
                    "one exact accepted same-turn owner ref",
                    request.OwnerRef!);
                return new ResourceOwnerAuthorityResolution(null, issues);
            }

            if (!string.Equals(entry.Key.Realm, request.Realm, StringComparison.Ordinal) ||
                entry.Key.OwnerKind != request.OwnerKind)
            {
                Add(
                    issues,
                    "owner.ownerRef",
                    "resource_owner_ref_kind_mismatch",
                    "same realm and owner kind as the accepted ref",
                    DescribeRequest(request));
                return new ResourceOwnerAuthorityResolution(null, issues);
            }
        }
        else
        {
            if (!ResourceMaterializationContract.IsExactIdentifier(request.ResourceOwnerId))
            {
                Add(issues, "owner.resourceOwnerId", "resource_owner_id_invalid", "exact owner ID", request.ResourceOwnerId!);
                return new ResourceOwnerAuthorityResolution(null, issues);
            }

            var key = new ResourceOwnerKey(
                request.Realm,
                request.OwnerKind,
                request.ResourceOwnerId!);
            if (_entries.TryGetValue(key, out entry))
            {
                if (entry.SameTurn)
                {
                    Add(
                        issues,
                        "owner.resourceOwnerId",
                        "resource_owner_same_turn_id_forbidden",
                        "same-turn ownerRef; allocated permanent ID is client-owned",
                        request.ResourceOwnerId!);
                    return new ResourceOwnerAuthorityResolution(null, issues);
                }
            }
            else
            {
                var alias = OwnerAlias(key);
                var historicalAlias = HistoricalAlias(key);
                var code = _ownerAliases.ContainsKey(alias)
                    ? "resource_owner_identity_confusable"
                    : _historicalAliases.Contains(historicalAlias)
                        ? "resource_owner_historical"
                        : _entries.Keys.Any(candidate =>
                            candidate.OwnerKind == request.OwnerKind &&
                            string.Equals(
                                candidate.ResourceOwnerId,
                                request.ResourceOwnerId,
                                StringComparison.Ordinal) &&
                            !string.Equals(candidate.Realm, request.Realm, StringComparison.Ordinal))
                            ? "resource_owner_realm_mismatch"
                            : "resource_owner_unresolved";
                Add(
                    issues,
                    "owner.resourceOwnerId",
                    code,
                    "one exact current owner ID",
                    request.ResourceOwnerId!);
                return new ResourceOwnerAuthorityResolution(null, issues);
            }
        }

        if (!entry.ResourceCapabilities.Contains(request.ResourceKey))
        {
            Add(
                issues,
                "owner.resourceKey",
                "resource_owner_capability_missing",
                "resource key explicitly exported by the owner",
                request.ResourceKey);
        }
        else if (!entry.IsResourceCapabilityActive(request.ResourceKey))
        {
            Add(
                issues,
                "owner",
                "resource_owner_inactive",
                "active accepted owner",
                LifecycleToken(entry.Lifecycle));
        }

        return issues.Count == 0
            ? new ResourceOwnerAuthorityResolution(CopyEntry(entry), Array.Empty<ValidationIssue>())
            : new ResourceOwnerAuthorityResolution(null, issues);
    }

    internal IReadOnlyList<ValidationIssue> ValidateCanonicalAgreement(
        ResourceStateLedger state,
        ResourceHistoryState history,
        IReadOnlyCollection<ResourceOwnerKey>? pendingTerminalOwners = null)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(history);
        var issues = new List<ValidationIssue>();
        if (Issues.Count > 0)
        {
            Add(
                issues,
                "resourceOwners",
                "resource_owner_authority_invalid",
                "issue-free composed owner authority",
                Issues.Count.ToString());
            return issues;
        }

        var allowedTerminalOwners = new HashSet<ResourceOwnerKey>(
            pendingTerminalOwners ?? Array.Empty<ResourceOwnerKey>(),
            ResourceOwnerKeyComparer.Instance);
        foreach (var terminal in allowedTerminalOwners)
        {
            if (!_historicalOwners.Contains(terminal))
            {
                Add(
                    issues,
                    "resourceOwners.pendingTerminalOwners",
                    "resource_owner_terminal_authority_invalid",
                    "exact owner made historical by this accepted owner lifecycle",
                    $"{terminal.Realm}/{OwnerKindToken(terminal.OwnerKind)}/{terminal.ResourceOwnerId}");
            }
        }
        if (issues.Count != 0)
            return issues;

        var liveCoordinates = new HashSet<ResourceCoordinate>(
            state.Entries.Select(static entry => entry.Coordinate),
            ResourceCoordinateComparer.Instance);
        var index = 0;
        foreach (var entry in state.Entries)
        {
            ValidateCanonicalCoordinate(
                entry.Coordinate,
                true,
                entry.State,
                $"resourceState.entries[{index++}]",
                allowedTerminalOwners,
                issues);
        }

        index = 0;
        foreach (var coordinate in history.Transitions
                     .Select(static transition => transition.Coordinate)
                     .Distinct(ResourceCoordinateComparer.Instance))
        {
            if (!liveCoordinates.Contains(coordinate))
            {
                ValidateCanonicalCoordinate(
                    coordinate,
                    false,
                    null,
                    $"resourceHistory.coordinates[{index}]",
                    allowedTerminalOwners,
                    issues);
            }
            index++;
        }
        return issues;
    }

    private void ValidateCanonicalCoordinate(
        ResourceCoordinate coordinate,
        bool requireLiveOwner,
        ResourceLifecycleState? liveState,
        string path,
        IReadOnlySet<ResourceOwnerKey> pendingTerminalOwners,
        List<ValidationIssue> issues)
    {
        var key = new ResourceOwnerKey(
            coordinate.Realm,
            coordinate.OwnerKind,
            coordinate.ResourceOwnerId);
        if (_entries.TryGetValue(key, out var entry))
        {
            if (requireLiveOwner && entry.Lifecycle == ResourceOwnerLifecycle.Terminal)
            {
                Add(
                    issues,
                    path,
                    "resource_owner_terminal_state_forbidden",
                    "active or suspended exact owner for every live resource coordinate",
                    DescribeCoordinate(coordinate));
            }
            else if (requireLiveOwner &&
                     liveState != (entry.IsResourceCapabilityActive(coordinate.ResourceKey)
                         ? ResourceLifecycleState.Active
                         : ResourceLifecycleState.Suspended))
            {
                var expectsActive = entry.IsResourceCapabilityActive(
                    coordinate.ResourceKey);
                Add(
                    issues,
                    path + ".state",
                    "resource_owner_lifecycle_state_mismatch",
                    expectsActive
                        ? "active resource state for active owner or sealed realm-independent capability"
                        : "suspended resource state for suspended realm-bound capability",
                    liveState?.ToString() ?? "missing");
            }
            if (!entry.ResourceCapabilities.Contains(coordinate.ResourceKey))
            {
                Add(
                    issues,
                    path + ".resourceKey",
                    "resource_owner_capability_missing",
                    "resource key explicitly exported by the exact owner",
                    coordinate.ResourceKey);
            }
            return;
        }

        if (requireLiveOwner &&
            _historicalOwners.Contains(key) &&
            pendingTerminalOwners.Contains(key))
        {
            return;
        }

        if (!requireLiveOwner && _historicalOwners.Contains(key))
            return;

        var alias = OwnerAlias(key);
        var historicalAlias = HistoricalAlias(key);
        var code = _ownerAliases.ContainsKey(alias)
            ? "resource_owner_identity_confusable"
            : _historicalAliases.Contains(historicalAlias)
                ? "resource_owner_historical"
                : _entries.Keys.Any(candidate =>
                    candidate.OwnerKind == key.OwnerKind &&
                    string.Equals(
                        candidate.ResourceOwnerId,
                        key.ResourceOwnerId,
                        StringComparison.Ordinal) &&
                    !string.Equals(candidate.Realm, key.Realm, StringComparison.Ordinal))
                    ? "resource_owner_realm_mismatch"
                    : "resource_owner_unresolved";
        Add(
            issues,
            path,
            code,
            requireLiveOwner
                ? "one exact composed current owner"
                : "one exact composed current or historical owner",
            DescribeCoordinate(coordinate));
    }

    private static ResourceOwnerAuthorityEntry CopyEntry(ResourceOwnerAuthorityEntry entry) =>
        entry with
        {
            ResourceCapabilities = new ResourceReadOnlySet<string>(
                entry.ResourceCapabilities,
                StringComparer.Ordinal),
            RealmIndependentResourceCapabilities = new ResourceReadOnlySet<string>(
                entry.RealmIndependentResourceCapabilities,
                StringComparer.Ordinal)
        };

    private static string CreateFingerprint(
        IEnumerable<ResourceOwnerAuthorityEntry> entries,
        IReadOnlyDictionary<string, ResourceOwnerAuthorityEntry> refs,
        IEnumerable<ResourceOwnerKey> historical,
        IReadOnlyList<ValidationIssue> issues)
    {
        var root = new JsonObject
        {
            ["owners"] = new JsonArray(entries
                .OrderBy(static entry => entry.Key.Realm, StringComparer.Ordinal)
                .ThenBy(static entry => OwnerKindToken(entry.Key.OwnerKind), StringComparer.Ordinal)
                .ThenBy(static entry => entry.Key.ResourceOwnerId, StringComparer.Ordinal)
                .Select(entry => (JsonNode)new JsonObject
                {
                    ["realm"] = entry.Key.Realm,
                    ["ownerKind"] = OwnerKindToken(entry.Key.OwnerKind),
                    ["resourceOwnerId"] = entry.Key.ResourceOwnerId,
                    ["lifecycle"] = LifecycleToken(entry.Lifecycle),
                    ["sameTurn"] = entry.SameTurn,
                    ["ownerRef"] = entry.SameTurnRef,
                    ["boundNpcId"] = entry.BoundNpcId,
                    ["capabilities"] = new JsonArray(entry.ResourceCapabilities
                        .OrderBy(static capability => capability, StringComparer.Ordinal)
                        .Select(static capability => (JsonNode)capability)
                        .ToArray()),
                    ["realmIndependentCapabilities"] = new JsonArray(
                        entry.RealmIndependentResourceCapabilities
                            .OrderBy(static capability => capability, StringComparer.Ordinal)
                            .Select(static capability => (JsonNode)capability)
                            .ToArray()),
                    ["authorityFingerprint"] = entry.AuthorityFingerprint
                }).ToArray()),
            ["refs"] = new JsonArray(refs
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (JsonNode)new JsonObject
                {
                    ["ownerRef"] = pair.Key,
                    ["realm"] = pair.Value.Key.Realm,
                    ["ownerKind"] = OwnerKindToken(pair.Value.Key.OwnerKind),
                    ["resourceOwnerId"] = pair.Value.Key.ResourceOwnerId
                }).ToArray()),
            ["historical"] = new JsonArray(historical
                .OrderBy(static key => key.Realm, StringComparer.Ordinal)
                .ThenBy(static key => OwnerKindToken(key.OwnerKind), StringComparer.Ordinal)
                .ThenBy(static key => key.ResourceOwnerId, StringComparer.Ordinal)
                .Select(key => (JsonNode)new JsonObject
                {
                    ["realm"] = key.Realm,
                    ["ownerKind"] = OwnerKindToken(key.OwnerKind),
                    ["resourceOwnerId"] = key.ResourceOwnerId
                }).ToArray()),
            ["issues"] = new JsonArray(issues
                .OrderBy(static issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(static issue => issue.Code, StringComparer.Ordinal)
                .Select(issue => (JsonNode)new JsonObject
                {
                    ["path"] = issue.FilePath,
                    ["code"] = issue.Code
                }).ToArray())
        };
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())))
            .ToLowerInvariant();
    }

    private static string DescribeRequest(ResourceOwnerRequest request) =>
        $"{request.Realm}/{OwnerKindToken(request.OwnerKind)}/" +
        $"{request.ResourceOwnerId ?? request.OwnerRef ?? "<missing>"}/{request.ResourceKey}";

    private static string DescribeCoordinate(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/{OwnerKindToken(coordinate.OwnerKind)}/" +
        $"{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static bool IsRealmAllowed(string realm, ResourceOwnerKind kind)
    {
        if (!Realms.Contains(realm))
            return false;
        return kind is
            ResourceOwnerKind.Player or
            ResourceOwnerKind.Npc or
            ResourceOwnerKind.Combatant or
            ResourceOwnerKind.CombatGroupMember or
            ResourceOwnerKind.Vehicle or
            ResourceOwnerKind.Item
            ? string.Equals(realm, "mortal_world", StringComparison.Ordinal)
            : realm is "chaos_sea" or "shining_abode";
    }

    private static string OwnerAlias(ResourceOwnerKey key) =>
        key.Realm + "\u001f" + OwnerKindToken(key.OwnerKind) + "\u001f" +
        ResourceMaterializationContract.BuildConfusableKey(key.ResourceOwnerId);

    private static string HistoricalAlias(ResourceOwnerKey key) => OwnerAlias(key);

    private static string OwnerKindToken(ResourceOwnerKind kind) => kind switch
    {
        ResourceOwnerKind.Player => "player",
        ResourceOwnerKind.Npc => "npc",
        ResourceOwnerKind.Combatant => "combatant",
        ResourceOwnerKind.CombatGroupMember => "combat_group_member",
        ResourceOwnerKind.Vehicle => "vehicle",
        ResourceOwnerKind.Item => "item",
        ResourceOwnerKind.AfterlifeActor => "afterlife_actor",
        ResourceOwnerKind.AfterlifeConflictSide => "afterlife_conflict_side",
        ResourceOwnerKind.AfterlifeScope => "afterlife_scope",
        _ => throw new ArgumentOutOfRangeException(nameof(kind))
    };

    private static string LifecycleToken(ResourceOwnerLifecycle lifecycle) => lifecycle switch
    {
        ResourceOwnerLifecycle.Active => "active",
        ResourceOwnerLifecycle.Suspended => "suspended",
        ResourceOwnerLifecycle.Terminal => "terminal",
        _ => throw new ArgumentOutOfRangeException(nameof(lifecycle))
    };

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Resource owner violates composed exact authority.",
            code: code,
            section: "resource_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Use one exact accepted owner ID or same-turn ref with an active lifecycle and an explicitly exported resource capability; never infer identity from a name, alias, or index."));

    private sealed class Builder
    {
        internal Dictionary<ResourceOwnerKey, ResourceOwnerAuthorityEntry> Entries { get; } = new();
        internal Dictionary<string, ResourceOwnerAuthorityEntry> SameTurnRefs { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, List<ResourceOwnerKey>> OwnerAliases { get; } =
            new(StringComparer.Ordinal);
        internal Dictionary<string, List<string>> RefAliases { get; } =
            new(StringComparer.Ordinal);
        internal HashSet<string> HistoricalAliases { get; } = new(StringComparer.Ordinal);
        internal List<ResourceOwnerKey> HistoricalOwners { get; } = new();
        internal List<ValidationIssue> Issues { get; } = new();

        internal Builder(IEnumerable<ResourceOwnerKey> historicalOwners)
        {
            var index = 0;
            foreach (var key in historicalOwners)
            {
                var path = $"historicalOwners[{index++}]";
                if (!ValidateKey(key, path, Issues))
                    continue;
                HistoricalOwners.Add(key);
                HistoricalAliases.Add(HistoricalAlias(key));
            }
        }

        internal void AddRange(
            IEnumerable<ResourceOwnerExport> exports,
            bool sameTurn,
            string path)
        {
            var index = 0;
            foreach (var export in exports)
                AddExport(export, sameTurn, $"{path}[{index++}]");
        }

        private void AddExport(ResourceOwnerExport export, bool sameTurn, string path)
        {
            var valid = ValidateKey(export.Key, path + ".key", Issues);
            if (export.SameTurn != sameTurn)
            {
                Add(Issues, path + ".sameTurn", "resource_owner_same_turn_invalid", sameTurn.ToString(), export.SameTurn.ToString());
                valid = false;
            }
            if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                    export.AuthorityFingerprint))
            {
                Add(Issues, path + ".authorityFingerprint", "resource_owner_fingerprint_invalid", "lowercase SHA-256 authority fingerprint", export.AuthorityFingerprint);
                valid = false;
            }

            if (sameTurn)
            {
                if (!ResourceMaterializationContract.IsExactIdentifier(export.OwnerRef))
                {
                    Add(Issues, path + ".ownerRef", "resource_owner_ref_invalid", "exact same-turn owner ref", export.OwnerRef ?? "missing");
                    valid = false;
                }
            }
            else if (export.OwnerRef != null)
            {
                Add(Issues, path + ".ownerRef", "resource_owner_ref_forbidden", "null for pre-turn owner", export.OwnerRef);
                valid = false;
            }

            if (export.BoundNpcId != null &&
                (!ResourceMaterializationContract.IsExactIdentifier(export.BoundNpcId) ||
                 export.Key.OwnerKind != ResourceOwnerKind.Npc ||
                 !string.Equals(
                     export.BoundNpcId,
                     export.Key.ResourceOwnerId,
                     StringComparison.Ordinal)))
            {
                Add(Issues, path + ".boundNpcId", "resource_owner_npc_binding_invalid", "null or exact NPC owner ID on the same NPC key", export.BoundNpcId);
                valid = false;
            }

            var capabilities = new HashSet<string>(StringComparer.Ordinal);
            var capabilityAliases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capability in export.ResourceCapabilities)
            {
                if (!ResourceMaterializationContract.IsExactIdentifier(capability))
                {
                    Add(Issues, path + ".capabilities", "resource_owner_capability_invalid", "exact resource key", capability ?? "null");
                    valid = false;
                    continue;
                }
                var alias = ResourceMaterializationContract.BuildConfusableKey(capability);
                if (!capabilities.Add(capability) || !capabilityAliases.Add(alias))
                {
                    Add(Issues, path + ".capabilities", "resource_owner_capability_confusable", "exact/confusable-unique capability", capability);
                    valid = false;
                }
            }

            var realmIndependentCapabilities = new HashSet<string>(StringComparer.Ordinal);
            var realmIndependentAliases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var capability in export.RealmIndependentResourceCapabilities)
            {
                if (!ResourceMaterializationContract.IsExactIdentifier(capability))
                {
                    Add(
                        Issues,
                        path + ".realmIndependentCapabilities",
                        "resource_owner_realm_independent_capability_invalid",
                        "exact resource key",
                        capability ?? "null");
                    valid = false;
                    continue;
                }
                var alias = ResourceMaterializationContract.BuildConfusableKey(capability);
                if (!realmIndependentCapabilities.Add(capability) ||
                    !realmIndependentAliases.Add(alias))
                {
                    Add(
                        Issues,
                        path + ".realmIndependentCapabilities",
                        "resource_owner_realm_independent_capability_confusable",
                        "exact/confusable-unique resource capability",
                        capability);
                    valid = false;
                }
                if (!capabilities.Contains(capability))
                {
                    Add(
                        Issues,
                        path + ".realmIndependentCapabilities",
                        "resource_owner_realm_independent_capability_unbound",
                        "realm-independent policy only for an explicitly exported capability",
                        capability);
                    valid = false;
                }
            }

            if (!valid)
                return;

            var entry = new ResourceOwnerAuthorityEntry(
                export.Key,
                export.Lifecycle,
                sameTurn,
                export.OwnerRef,
                export.BoundNpcId,
                new ResourceReadOnlySet<string>(capabilities, StringComparer.Ordinal),
                export.AuthorityFingerprint)
            {
                RealmIndependentResourceCapabilities = new ResourceReadOnlySet<string>(
                    realmIndependentCapabilities,
                    StringComparer.Ordinal)
            };
            var ownerAlias = OwnerAlias(export.Key);
            if (!OwnerAliases.TryGetValue(ownerAlias, out var aliases))
            {
                aliases = new List<ResourceOwnerKey>();
                OwnerAliases.Add(ownerAlias, aliases);
            }
            aliases.Add(export.Key);
            if (Entries.ContainsKey(export.Key))
            {
                Add(Issues, path + ".key", "resource_owner_identity_duplicate", "one exact owner key", export.Key.ToString());
            }
            else if (aliases.Count > 1)
            {
                Add(Issues, path + ".key", "resource_owner_identity_confusable", "one exact/confusable owner key", export.Key.ToString());
            }
            else if (HistoricalAliases.Contains(HistoricalAlias(export.Key)))
            {
                Add(Issues, path + ".key", "resource_owner_historical_conflict", "non-historical owner identity", export.Key.ToString());
            }
            else
            {
                Entries.Add(export.Key, entry);
            }

            if (!sameTurn || export.OwnerRef == null)
                return;
            var refAlias = ResourceMaterializationContract.BuildConfusableKey(export.OwnerRef);
            if (!RefAliases.TryGetValue(refAlias, out var refs))
            {
                refs = new List<string>();
                RefAliases.Add(refAlias, refs);
            }
            refs.Add(export.OwnerRef);
            if (SameTurnRefs.ContainsKey(export.OwnerRef))
            {
                Add(Issues, path + ".ownerRef", "resource_owner_ref_duplicate", "one exact same-turn ref", export.OwnerRef);
            }
            else if (refs.Count > 1)
            {
                Add(Issues, path + ".ownerRef", "resource_owner_ref_confusable", "one global exact/confusable same-turn ref", export.OwnerRef);
            }
            else
            {
                SameTurnRefs.Add(export.OwnerRef, entry);
            }
        }

        private static bool ValidateKey(
            ResourceOwnerKey key,
            string path,
            List<ValidationIssue> issues)
        {
            var valid = true;
            if (!IsRealmAllowed(key.Realm, key.OwnerKind))
            {
                Add(issues, path + ".realm", "resource_owner_realm_invalid", "closed realm compatible with owner kind", key.Realm);
                valid = false;
            }
            if (!ResourceMaterializationContract.IsExactIdentifier(key.ResourceOwnerId))
            {
                Add(issues, path + ".resourceOwnerId", "resource_owner_id_invalid", "exact owner ID", key.ResourceOwnerId);
                valid = false;
            }
            if (key.OwnerKind == ResourceOwnerKind.Player &&
                !string.Equals(key.ResourceOwnerId, "player_current", StringComparison.Ordinal))
            {
                Add(issues, path + ".resourceOwnerId", "resource_owner_player_identity_invalid", "player_current", key.ResourceOwnerId);
                valid = false;
            }
            return valid;
        }
    }

    private sealed class ResourceOwnerKeyComparer : IEqualityComparer<ResourceOwnerKey>
    {
        internal static ResourceOwnerKeyComparer Instance { get; } = new();

        public bool Equals(ResourceOwnerKey? x, ResourceOwnerKey? y) =>
            ReferenceEquals(x, y) ||
            (x != null && y != null &&
             x.OwnerKind == y.OwnerKind &&
             string.Equals(x.Realm, y.Realm, StringComparison.Ordinal) &&
             string.Equals(x.ResourceOwnerId, y.ResourceOwnerId, StringComparison.Ordinal));

        public int GetHashCode(ResourceOwnerKey obj) => HashCode.Combine(
            StringComparer.Ordinal.GetHashCode(obj.Realm),
            obj.OwnerKind,
            StringComparer.Ordinal.GetHashCode(obj.ResourceOwnerId));
    }
}

internal sealed class ResourceReadOnlySet<T> : ISet<T>, IReadOnlySet<T>
    where T : notnull
{
    private readonly HashSet<T> _items;

    internal ResourceReadOnlySet(IEnumerable<T> values, IEqualityComparer<T> comparer) =>
        _items = new HashSet<T>(values, comparer);

    public int Count => _items.Count;

    public bool IsReadOnly => true;

    public bool Contains(T item) => _items.Contains(item);

    public bool IsProperSubsetOf(IEnumerable<T> other) => _items.IsProperSubsetOf(other);

    public bool IsProperSupersetOf(IEnumerable<T> other) => _items.IsProperSupersetOf(other);

    public bool IsSubsetOf(IEnumerable<T> other) => _items.IsSubsetOf(other);

    public bool IsSupersetOf(IEnumerable<T> other) => _items.IsSupersetOf(other);

    public bool Overlaps(IEnumerable<T> other) => _items.Overlaps(other);

    public bool SetEquals(IEnumerable<T> other) => _items.SetEquals(other);

    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);

    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    bool ISet<T>.Add(T item) => throw ReadOnly();

    void ICollection<T>.Add(T item) => throw ReadOnly();

    public void ExceptWith(IEnumerable<T> other) => throw ReadOnly();

    public void IntersectWith(IEnumerable<T> other) => throw ReadOnly();

    public void SymmetricExceptWith(IEnumerable<T> other) => throw ReadOnly();

    public void UnionWith(IEnumerable<T> other) => throw ReadOnly();

    public void Clear() => throw ReadOnly();

    public bool Remove(T item) => throw ReadOnly();

    private static NotSupportedException ReadOnly() =>
        new("The resource authority set is immutable.");
}
