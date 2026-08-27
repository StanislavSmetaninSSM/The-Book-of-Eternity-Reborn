using System.Collections.Frozen;
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
    bool SameTurn,
    string? SourceRef = null,
    IReadOnlySet<string>? SatisfiedPredicates = null,
    string? RequiredApplicationAuthority = null);

internal sealed class WoundSourceGroupAuthority
{
    private const string FingerprintDomain =
        "book_of_eternity.wound.effect_source_group";
    private const string FingerprintVersion = "1";
    private readonly WoundEffectSourceDefinition[] _definitions;
    private readonly WoundRootLineageAuthorityRow[] _applicationRootLineage;
    private readonly WoundRootLineageAuthorityRow[] _existingRootLineage;

    internal WoundSourceGroupAuthority(
        EffectIdentitySourceGroup key,
        WoundOwnerCoordinate owner,
        EffectTargetKey target,
        bool sameTurn,
        string? sourceRef,
        string? preparedSourceExportFingerprint,
        IReadOnlyList<WoundEffectSourceDefinition> definitions,
        IReadOnlyList<WoundRootLineageAuthorityRow> applicationRootLineage,
        IReadOnlyList<WoundRootLineageAuthorityRow> existingRootLineage,
        string? graphAuthorityFingerprint = null)
    {
        Key = key;
        Owner = owner;
        Target = target;
        SameTurn = sameTurn;
        SourceRef = sourceRef;
        PreparedSourceExportFingerprint = preparedSourceExportFingerprint;
        _definitions = definitions.Select(CloneDefinition).ToArray();
        _applicationRootLineage = applicationRootLineage
            .Select(WoundAcceptedTurnData.CloneLineageRow)
            .ToArray();
        _existingRootLineage = existingRootLineage
            .Select(WoundAcceptedTurnData.CloneLineageRow)
            .ToArray();
        GraphAuthorityFingerprint = graphAuthorityFingerprint ??
            ComputeGraphAuthorityFingerprint(
                Key,
                Owner,
                Target,
                _definitions,
                _applicationRootLineage,
                _existingRootLineage);
    }

    internal EffectIdentitySourceGroup Key { get; }
    internal WoundOwnerCoordinate Owner { get; }
    internal EffectTargetKey Target { get; }
    internal bool SameTurn { get; }
    internal string? SourceRef { get; }
    internal string? PreparedSourceExportFingerprint { get; }
    internal string GraphAuthorityFingerprint { get; }
    internal IReadOnlyList<WoundEffectSourceDefinition> Definitions =>
        _definitions.Select(CloneDefinition).ToArray();
    internal IReadOnlyList<WoundRootLineageAuthorityRow> ApplicationRootLineage =>
        _applicationRootLineage
            .Select(WoundAcceptedTurnData.CloneLineageRow)
            .ToArray();
    internal IReadOnlyList<WoundRootLineageAuthorityRow> ExistingRootLineage =>
        _existingRootLineage
            .Select(WoundAcceptedTurnData.CloneLineageRow)
            .ToArray();

    internal WoundSourceGroupAuthority DetachedCopy() => new(
        Key,
        Owner,
        Target,
        SameTurn,
        SourceRef,
        PreparedSourceExportFingerprint,
        Definitions,
        ApplicationRootLineage,
        ExistingRootLineage,
        GraphAuthorityFingerprint);

    internal string RecomputeGraphAuthorityFingerprint() =>
        ComputeGraphAuthorityFingerprint(
            Key,
            Owner,
            Target,
            _definitions,
            _applicationRootLineage,
            _existingRootLineage);

    private static WoundEffectSourceDefinition CloneDefinition(
        WoundEffectSourceDefinition value) =>
        new(value.DefinitionKey, value.Definition);

    private static string ComputeGraphAuthorityFingerprint(
        EffectIdentitySourceGroup key,
        WoundOwnerCoordinate owner,
        EffectTargetKey target,
        IReadOnlyList<WoundEffectSourceDefinition> definitions,
        IReadOnlyList<WoundRootLineageAuthorityRow> applicationRoots,
        IReadOnlyList<WoundRootLineageAuthorityRow> existingRoots)
    {
        var fields = new List<string?>
        {
            FingerprintDomain,
            FingerprintVersion,
            key.Realm,
            key.Kind,
            key.SourceId,
            owner.Realm,
            owner.OwnerKind,
            owner.OwnerId,
            owner.CarrierPath,
            target.Realm,
            target.Kind,
            target.TargetId,
            definitions.Count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var definition in definitions)
        {
            fields.Add(definition.DefinitionKey);
            fields.Add(WoundAcceptedTurnFingerprintWriter.CanonicalJson(
                definition.Definition));
        }
        AppendRoots(fields, applicationRoots);
        AppendRoots(fields, existingRoots);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static void AppendRoots(
        ICollection<string?> fields,
        IReadOnlyList<WoundRootLineageAuthorityRow> roots)
    {
        fields.Add(roots.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));
        foreach (var root in roots)
        {
            fields.Add(root.ApplicationRef);
            fields.Add(root.EffectId);
            fields.Add(root.DefinitionKey);
            fields.Add(root.OwnershipDomain.Kind);
            fields.Add(root.OwnershipDomain.ComplicationId);
        }
    }
}

internal sealed record EffectSourceAuthorityInput(
    IReadOnlyList<EffectSourceExport> PreTurnSources,
    IReadOnlyList<EffectSourceExport> SameTurnSources,
    IReadOnlySet<string> HistoricalSourceIds,
    IReadOnlySet<string>? GrantedApplicationAuthorities = null,
    IReadOnlyList<WoundSourceGroupAuthority>? WoundGroups = null,
    IReadOnlyList<ValidationIssue>? CompositionIssues = null);

internal sealed record EffectSourceAuthorityEntry(
    EffectSourceKey Key,
    JsonObject Definition,
    bool Materializable,
    bool Active,
    bool SameTurn,
    string? SourceRef,
    IReadOnlySet<string> SatisfiedPredicates,
    string? RequiredApplicationAuthority = null);

internal sealed record EffectSourceReferenceKey(
    string Realm,
    string Kind,
    string SourceRef,
    string DefinitionKey);

internal sealed record EffectSourceResolution(
    EffectSourceAuthorityEntry? Source,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Source != null && Issues.Count == 0;
}

internal enum WoundTypedRootSourceIdentityKind
{
    NewSourceRef,
    ExistingSourceId
}

internal sealed record WoundTypedRootBindingRequest(
    EffectSourceKey SourceKey,
    WoundEffectSourceSelector SourceSelector,
    WoundTypedRootSourceIdentityKind SourceIdentityKind,
    EffectTargetKey Target,
    string SourceExportFingerprint,
    string ApplicationRef,
    WoundRootOwnershipDomain OwnershipDomain);

internal sealed record WoundTypedRootBindingResolution(
    EffectSourceAuthorityEntry? Source,
    WoundSourceGroupAuthority? Group,
    WoundRootLineageAuthorityRow? Root,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success =>
        Source is not null &&
        Group is not null &&
        Root is not null &&
        Issues.Count == 0;
}

internal readonly record struct EffectSourceRoutingBinding(
    EffectSourceKey Key,
    bool Materializable,
    bool Active,
    bool SameTurn,
    string? SourceRef,
    string? RequiredApplicationAuthority,
    string? StackKey,
    string? StackPolicy)
{
    internal static EffectSourceRoutingBinding FromEntry(
        EffectSourceAuthorityEntry entry) =>
        new(
            entry.Key,
            entry.Materializable,
            entry.Active,
            entry.SameTurn,
            entry.SourceRef,
            entry.RequiredApplicationAuthority,
            entry.Definition["stacking"]?["stackKey"]?.GetValue<string>(),
            entry.Definition["stacking"]?["policy"]?.GetValue<string>());
}

internal sealed record EffectSourceRoutingResolution(
    EffectSourceRoutingBinding? Source,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Source.HasValue && Issues.Count == 0;
}

internal sealed class EffectSourceAuthority
{
    internal const int MaximumSameTurnWoundDefinitions = 160;
    internal const int MaximumPreTurnWoundDefinitions = 10_000;

    private static readonly HashSet<string> SourceKinds = new(StringComparer.Ordinal)
    {
        "skill", "spiritual_art", "item", "wound", "quest", "location", "hazard", "faction",
        "world_event", "fate_card", "combat_action"
    };

    private readonly Dictionary<EffectSourceKey, EffectSourceAuthorityEntry> _entries;
    private readonly Dictionary<string, List<EffectSourceAuthorityEntry>> _byAlias;
    private readonly Dictionary<EffectSourceReferenceKey, EffectSourceAuthorityEntry> _byRef;
    private readonly Dictionary<string, List<EffectSourceReferenceKey>> _refsByAlias;
    private readonly HashSet<EffectSourceReferenceKey> _invalidRefs;
    private readonly HashSet<string> _historicalAliases;
    private readonly HashSet<EffectSourceKey> _invalidKeys;
    private readonly HashSet<string> _grantedApplicationAuthorities;
    private readonly EffectSourceExport[] _ownerExports;
    private readonly Dictionary<EffectIdentitySourceGroup, WoundSourceGroupAuthority>
        _woundGroups;

    private EffectSourceAuthority(Builder builder)
    {
        _entries = new Dictionary<EffectSourceKey, EffectSourceAuthorityEntry>(builder.Entries);
        _byAlias = builder.ByAlias.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _byRef = new Dictionary<EffectSourceReferenceKey, EffectSourceAuthorityEntry>(builder.ByRef);
        _refsByAlias = builder.RefsByAlias.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToList(),
            StringComparer.Ordinal);
        _invalidRefs = new HashSet<EffectSourceReferenceKey>(builder.InvalidRefs);
        _historicalAliases = new HashSet<string>(builder.HistoricalAliases, StringComparer.Ordinal);
        _invalidKeys = new HashSet<EffectSourceKey>(builder.InvalidKeys);
        _grantedApplicationAuthorities = new HashSet<string>(
            builder.GrantedApplicationAuthorities,
            StringComparer.Ordinal);
        _ownerExports = builder.OwnerExports
            .Select(DetachExport)
            .ToArray();
        _woundGroups = builder.WoundGroups.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.DetachedCopy());
        Issues = builder.Issues.ToArray();
        Fingerprint = CreateFingerprint(
            _entries.Values,
            _ownerExports,
            _woundGroups.Values,
            Issues,
            _grantedApplicationAuthorities);
        CanonicalFingerprint = CreateCanonicalFingerprint(
            _entries.Values,
            _ownerExports,
            _woundGroups.Values,
            Issues);
    }

    internal IReadOnlyList<ValidationIssue> Issues { get; }

    internal static string CanonicalLinkKind(string sourceKind) =>
        string.Equals(sourceKind, "combat_action", StringComparison.Ordinal)
            ? "combat"
            : sourceKind;

    internal string Fingerprint { get; }

    internal string CanonicalFingerprint { get; }

    internal IReadOnlyList<EffectSourceAuthorityEntry> SnapshotSameTurnWoundEntries() =>
        _entries.Values
            .Where(static entry =>
                entry.SameTurn &&
                string.Equals(entry.Key.Kind, "wound", StringComparison.Ordinal))
            .OrderBy(static entry => entry.Key.Realm, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.SourceId, StringComparer.Ordinal)
            .ThenBy(static entry => entry.Key.DefinitionKey, StringComparer.Ordinal)
            .Select(static entry => new EffectSourceAuthorityEntry(
                new EffectSourceKey(
                    entry.Key.Realm,
                    entry.Key.Kind,
                    entry.Key.SourceId,
                    entry.Key.DefinitionKey),
                entry.Definition.DeepClone().AsObject(),
                entry.Materializable,
                entry.Active,
                entry.SameTurn,
                entry.SourceRef,
                entry.SatisfiedPredicates.ToFrozenSet(StringComparer.Ordinal),
                entry.RequiredApplicationAuthority))
            .ToArray();

    internal IReadOnlyList<EffectSourceExport> SnapshotSameTurnWoundGroups() =>
        _ownerExports
            .Where(static export =>
                export.SameTurn &&
                string.Equals(export.Kind, "wound", StringComparison.Ordinal))
            .OrderBy(static export => export.Realm, StringComparer.Ordinal)
            .ThenBy(static export => export.SourceId, StringComparer.Ordinal)
            .ThenBy(static export => export.SourceRef, StringComparer.Ordinal)
            .Select(DetachExport)
            .ToArray();

    internal IReadOnlyList<WoundSourceGroupAuthority> SnapshotWoundGroupAuthorities() =>
        _woundGroups.Values
            .OrderBy(static group => group.Key.Realm, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.SourceId, StringComparer.Ordinal)
            .Select(static group => group.DetachedCopy())
            .ToArray();

    internal bool TryResolveWoundGroup(
        EffectIdentitySourceGroup key,
        out WoundSourceGroupAuthority group)
    {
        if (_woundGroups.TryGetValue(key, out var resolved))
        {
            group = resolved.DetachedCopy();
            return true;
        }
        group = null!;
        return false;
    }

    internal static EffectSourceAuthority Build(EffectSourceAuthorityInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var builder = new Builder(
            input.HistoricalSourceIds,
            input.GrantedApplicationAuthorities ?? new HashSet<string>(StringComparer.Ordinal));
        var compositionIssues = input.CompositionIssues ??
            Array.Empty<ValidationIssue>();
        builder.AddCompositionIssues(compositionIssues);
        var woundGroups = input.WoundGroups ?? Array.Empty<WoundSourceGroupAuthority>();
        if (builder.ValidateWoundAggregateBounds(
                input.PreTurnSources,
                input.SameTurnSources,
                woundGroups) &&
            compositionIssues.Count == 0)
        {
            builder.AddRange(input.PreTurnSources, sameTurn: false);
            builder.AddRange(input.SameTurnSources, sameTurn: true);
            builder.AddWoundGroups(woundGroups);
        }
        builder.ValidateDefinitionLinks();
        builder.ValidateWoundBindings();
        return new EffectSourceAuthority(builder);
    }

    internal EffectSourceResolution Resolve(
        EffectSourceKey key,
        string targetKind,
        JsonObject? parameters) =>
        ResolveByKey(
            key,
            targetKind,
            parameters,
            allowResolvedSameTurnIdentity: false,
            validateParameters: true,
            validateApplicationAuthority: true);

    internal EffectSourceResolution ResolveCanonicalBinding(
        EffectSourceKey key,
        string targetKind) =>
        ResolveByKey(
            key,
            targetKind,
            parameters: null,
            allowResolvedSameTurnIdentity: true,
            validateParameters: false,
            validateApplicationAuthority: false);

    internal WoundTypedRootBindingResolution ResolveTypedWoundRootBinding(
        WoundTypedRootBindingRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var issues = new List<ValidationIssue>();
        var key = request.SourceKey;
        var selector = request.SourceSelector;
        var target = request.Target;
        var ownership = request.OwnershipDomain;
        if (key is null ||
            selector is null ||
            target is null ||
            ownership is null ||
            !string.Equals(key.Kind, "wound", StringComparison.Ordinal) ||
            !ResourceMaterializationContract.IsExactIdentifier(key.Realm) ||
            !ResourceMaterializationContract.IsExactIdentifier(key.SourceId) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                key.DefinitionKey) ||
            !ResourceMaterializationContract.IsExactIdentifier(target.Realm) ||
            !ResourceMaterializationContract.IsExactIdentifier(target.Kind) ||
            !ResourceMaterializationContract.IsExactIdentifier(target.TargetId) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                request.ApplicationRef) ||
            !ResourceMaterializationContract.IsAuthorityFingerprint(
                request.SourceExportFingerprint) ||
            !Enum.IsDefined(request.SourceIdentityKind))
        {
            Add(
                issues,
                "source.woundTypedRoot",
                "effect_source_wound_typed_root_request_invalid",
                "one complete exact typed wound-root request",
                request.ToString());
            return new WoundTypedRootBindingResolution(
                null,
                null,
                null,
                issues);
        }

        var groupKey = new EffectIdentitySourceGroup(
            key.Realm,
            key.Kind,
            key.SourceId);
        if (!_woundGroups.TryGetValue(groupKey, out var group))
        {
            Add(
                issues,
                "source.woundTypedRoot.group",
                "effect_source_wound_typed_root_group_unresolved",
                "one exact sealed wound source group",
                groupKey.ToString());
            return new WoundTypedRootBindingResolution(
                null,
                null,
                null,
                issues);
        }

        if (!group.SameTurn ||
            group.Target != target ||
            !string.Equals(
                group.PreparedSourceExportFingerprint,
                request.SourceExportFingerprint,
                StringComparison.Ordinal) ||
            !string.Equals(
                group.GraphAuthorityFingerprint,
                group.RecomputeGraphAuthorityFingerprint(),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                "source.woundTypedRoot.group",
                "effect_source_wound_typed_root_group_mismatch",
                "the exact same-turn owner/target/source-export/graph authority",
                groupKey.ToString());
        }

        var selectorMatches =
            string.Equals(selector.Realm, key.Realm, StringComparison.Ordinal) &&
            string.Equals(selector.Kind, key.Kind, StringComparison.Ordinal) &&
            string.Equals(
                selector.DefinitionKey,
                key.DefinitionKey,
                StringComparison.Ordinal) &&
            request.SourceIdentityKind switch
            {
                WoundTypedRootSourceIdentityKind.NewSourceRef =>
                    selector.SourceId is null &&
                    selector.SourceRef is not null &&
                    string.Equals(
                        selector.SourceRef,
                        group.SourceRef,
                        StringComparison.Ordinal),
                WoundTypedRootSourceIdentityKind.ExistingSourceId =>
                    selector.SourceRef is null &&
                    selector.SourceId is not null &&
                    string.Equals(
                        selector.SourceId,
                        key.SourceId,
                        StringComparison.Ordinal),
                _ => false
            };
        if (!selectorMatches)
        {
            Add(
                issues,
                "source.woundTypedRoot.selector",
                "effect_source_wound_typed_root_selector_mismatch",
                request.SourceIdentityKind ==
                    WoundTypedRootSourceIdentityKind.NewSourceRef
                    ? "the exact sealed new-wound sourceRef selector"
                    : "the exact sealed existing-wound sourceId selector",
                $"{selector.SourceId}/{selector.SourceRef}");
        }

        var matchingRoots = group.ApplicationRootLineage
            .Where(root => string.Equals(
                    root.ApplicationRef,
                    request.ApplicationRef,
                    StringComparison.Ordinal) &&
                string.Equals(
                    root.DefinitionKey,
                    key.DefinitionKey,
                    StringComparison.Ordinal) &&
                root.OwnershipDomain == ownership)
            .ToArray();
        var rootBinding = matchingRoots.Length == 1
            ? matchingRoots[0]
            : null;
        if (rootBinding is null)
        {
            Add(
                issues,
                "source.woundTypedRoot.applicationRef",
                "effect_source_wound_typed_root_unresolved",
                "one exact sealed application root and ownership domain",
                request.ApplicationRef);
        }

        _entries.TryGetValue(key, out var entry);
        var groupedDefinitions = group.Definitions
            .Where(definition => string.Equals(
                definition.DefinitionKey,
                key.DefinitionKey,
                StringComparison.Ordinal))
            .ToArray();
        if (entry is null ||
            _invalidKeys.Contains(key) ||
            groupedDefinitions.Length != 1 ||
            entry.Materializable ||
            !entry.Active ||
            !entry.SameTurn ||
            !string.Equals(
                entry.SourceRef,
                group.SourceRef,
                StringComparison.Ordinal) ||
            !JsonNode.DeepEquals(
                entry.Definition,
                groupedDefinitions.ElementAtOrDefault(0)?.Definition))
        {
            Add(
                issues,
                "source.woundTypedRoot.definition",
                "effect_source_wound_typed_root_definition_mismatch",
                "one exact active non-materializable definition in the sealed wound source group",
                key.ToString());
        }
        else
        {
            ValidateTargetKind(entry.Definition, target.Kind, issues);
        }

        return issues.Count == 0 && entry is not null && rootBinding is not null
            ? new WoundTypedRootBindingResolution(
                DetachEntry(entry),
                group.DetachedCopy(),
                WoundAcceptedTurnData.CloneLineageRow(rootBinding),
                Array.Empty<ValidationIssue>())
            : new WoundTypedRootBindingResolution(
                null,
                null,
                null,
                issues);
    }

    internal EffectSourceRoutingResolution ResolveCanonicalRoutingBinding(
        EffectSourceKey key,
        string targetKind,
        EffectAcceptedTurnPlanner.EffectResourceRoutingWorkMeter workMeter)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetKind);
        ArgumentNullException.ThrowIfNull(workMeter);
        var issues = new List<ValidationIssue>();
        if (_entries.TryGetValue(key, out var entry) &&
            !_invalidKeys.Contains(key))
        {
            ValidateTargetKind(entry.Definition, targetKind, issues);
            if (issues.Count == 0)
            {
                workMeter.RecordSourceBindingBorrow();
                return new EffectSourceRoutingResolution(
                    EffectSourceRoutingBinding.FromEntry(entry),
                    Array.Empty<ValidationIssue>());
            }
            return new EffectSourceRoutingResolution(null, issues);
        }

        AddUnresolvedKeyIssues(key, issues);
        return new EffectSourceRoutingResolution(null, issues);
    }

    internal IReadOnlyList<ValidationIssue> ValidateCanonicalParameters(
        EffectSourceAuthorityEntry source,
        JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(parameters);
        var issues = new List<ValidationIssue>();
        ValidateParameters(source.Definition, parameters, issues);
        return issues;
    }

    internal IReadOnlyList<ValidationIssue> ValidateCanonicalRoutingParameters(
        EffectSourceRoutingBinding source,
        JsonObject parameters)
    {
        ArgumentNullException.ThrowIfNull(parameters);
        var issues = new List<ValidationIssue>();
        if (!_entries.TryGetValue(source.Key, out var entry) ||
            _invalidKeys.Contains(source.Key))
        {
            AddUnresolvedKeyIssues(source.Key, issues);
            return issues;
        }
        ValidateParameters(entry.Definition, parameters, issues);
        return issues;
    }

    internal static IReadOnlyList<ValidationIssue> ValidateDefinitionParameters(
        JsonObject definition,
        JsonObject? parameters)
    {
        ArgumentNullException.ThrowIfNull(definition);
        var issues = new List<ValidationIssue>();
        ValidateParameters(definition, parameters, issues);
        return issues;
    }

    private EffectSourceResolution ResolveByKey(
        EffectSourceKey key,
        string targetKind,
        JsonObject? parameters,
        bool allowResolvedSameTurnIdentity,
        bool validateParameters,
        bool validateApplicationAuthority)
    {
        var issues = new List<ValidationIssue>();
        if (_entries.TryGetValue(key, out var entry) && !_invalidKeys.Contains(key))
        {
            if (!allowResolvedSameTurnIdentity &&
                entry.SameTurn &&
                entry.SourceRef != null)
            {
                Add(issues, "source.sourceId", "effect_source_same_turn_id_forbidden", "same-turn sourceRef for client-assigned source identity", key.SourceId);
                return new EffectSourceResolution(null, issues);
            }
            if (validateApplicationAuthority)
            {
                if (!entry.Materializable)
                    Add(issues, "source", "effect_source_not_materializable", "source definition explicitly materializable as an active instance", key.ToString());
                if (!entry.Active)
                    Add(issues, "source", "effect_source_inactive", "source current state authorizes application", key.ToString());
                if (entry.RequiredApplicationAuthority is { } requiredAuthority &&
                    !_grantedApplicationAuthorities.Contains(requiredAuthority))
                {
                    Add(
                        issues,
                        "source",
                        "effect_source_application_authority_missing",
                        "exact accepted-turn application authority for this built-in source",
                        requiredAuthority);
                }
                ValidateActivePredicate(entry, issues);
            }
            ValidateTargetKind(entry.Definition, targetKind, issues);
            if (validateParameters)
                ValidateParameters(entry.Definition, parameters, issues);
            return issues.Count == 0
                ? new EffectSourceResolution(
                    DetachEntry(entry),
                    Array.Empty<ValidationIssue>())
                : new EffectSourceResolution(null, issues);
        }

        AddUnresolvedKeyIssues(key, issues);
        return new EffectSourceResolution(null, issues);
    }

    private void AddUnresolvedKeyIssues(
        EffectSourceKey key,
        List<ValidationIssue> issues)
    {
        var selectorAlias = Alias(
            key.Realm,
            key.Kind,
            key.SourceId,
            key.DefinitionKey);
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
    }

    internal EffectSourceResolution Resolve(
        JsonObject selector,
        string realm,
        string targetKind,
        JsonObject? parameters)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var issues = new List<ValidationIssue>();
        if (!TryReadExactString(selector["kind"], out var kind) ||
            !SourceKinds.Contains(kind) ||
            !TryReadExactString(selector["definitionKey"], out var definitionKey))
        {
            Add(issues, "source", "effect_source_selector_invalid", "closed source kind and exact definitionKey", selector.ToJsonString());
            return new EffectSourceResolution(null, issues);
        }

        var hasId = selector.ContainsKey("sourceId") && selector["sourceId"] != null;
        var hasRef = selector.ContainsKey("sourceRef") && selector["sourceRef"] != null;
        if (hasId == hasRef || selector.Any(pair => pair.Key is not ("kind" or "sourceId" or "sourceRef" or "definitionKey")))
        {
            Add(issues, "source", "effect_source_selector_invalid", "kind and definitionKey plus exactly one sourceId or sourceRef", selector.ToJsonString());
            return new EffectSourceResolution(null, issues);
        }

        if (hasId)
        {
            if (!TryReadExactString(selector["sourceId"], out var sourceId))
            {
                Add(issues, "source.sourceId", "effect_source_selector_invalid", "exact non-empty sourceId", selector["sourceId"]?.ToJsonString() ?? "missing");
                return new EffectSourceResolution(null, issues);
            }
            return Resolve(
                new EffectSourceKey(realm, kind, sourceId, definitionKey),
                targetKind,
                parameters);
        }

        if (!TryReadExactString(selector["sourceRef"], out var sourceRef))
        {
            Add(issues, "source.sourceRef", "effect_source_selector_invalid", "exact same-turn sourceRef", selector["sourceRef"]?.ToJsonString() ?? "missing");
            return new EffectSourceResolution(null, issues);
        }
        var reference = new EffectSourceReferenceKey(realm, kind, sourceRef, definitionKey);
        if (_byRef.TryGetValue(reference, out var entry) && !_invalidRefs.Contains(reference))
        {
            ValidateResolvedEntry(entry, targetKind, parameters, issues);
            return issues.Count == 0
                ? new EffectSourceResolution(
                    DetachEntry(entry),
                    Array.Empty<ValidationIssue>())
                : new EffectSourceResolution(null, issues);
        }

        var alias = RefAlias(realm, kind, sourceRef, definitionKey);
        if (_refsByAlias.TryGetValue(alias, out var aliases) && aliases.Count > 0)
            Add(issues, "source.sourceRef", "effect_source_selector_confusable", "one exact accepted same-turn sourceRef", sourceRef);
        else
            Add(issues, "source.sourceRef", "effect_source_selector_unresolved", "one accepted same-turn sourceRef export", sourceRef);
        return new EffectSourceResolution(null, issues);
    }

    internal EffectSourceResolution ResolveRepairCandidate(
        JsonObject selector,
        string realm,
        string targetKind)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var issues = new List<ValidationIssue>();
        if (!TryReadExactString(selector["kind"], out var kind) ||
            !SourceKinds.Contains(kind) ||
            !TryReadExactString(selector["definitionKey"], out var definitionKey))
        {
            Add(
                issues,
                "source",
                "effect_source_selector_invalid",
                "closed source kind and exact definitionKey",
                selector.ToJsonString());
            return new EffectSourceResolution(null, issues);
        }

        var hasId = selector.ContainsKey("sourceId") && selector["sourceId"] != null;
        var hasRef = selector.ContainsKey("sourceRef") && selector["sourceRef"] != null;
        if (hasId == hasRef ||
            selector.Any(pair => pair.Key is not
                ("kind" or "sourceId" or "sourceRef" or "definitionKey")))
        {
            Add(
                issues,
                "source",
                "effect_source_selector_invalid",
                "kind and definitionKey plus exactly one sourceId or sourceRef",
                selector.ToJsonString());
            return new EffectSourceResolution(null, issues);
        }

        if (hasId)
        {
            if (!TryReadExactString(selector["sourceId"], out var sourceId))
            {
                Add(
                    issues,
                    "source.sourceId",
                    "effect_source_selector_invalid",
                    "exact non-empty sourceId",
                    selector["sourceId"]?.ToJsonString() ?? "missing");
                return new EffectSourceResolution(null, issues);
            }
            return ResolveByKey(
                new EffectSourceKey(realm, kind, sourceId, definitionKey),
                targetKind,
                parameters: null,
                allowResolvedSameTurnIdentity: false,
                validateParameters: false,
                validateApplicationAuthority: true);
        }

        if (!TryReadExactString(selector["sourceRef"], out var sourceRef))
        {
            Add(
                issues,
                "source.sourceRef",
                "effect_source_selector_invalid",
                "exact same-turn sourceRef",
                selector["sourceRef"]?.ToJsonString() ?? "missing");
            return new EffectSourceResolution(null, issues);
        }
        var reference = new EffectSourceReferenceKey(
            realm,
            kind,
            sourceRef,
            definitionKey);
        if (_byRef.TryGetValue(reference, out var entry) &&
            !_invalidRefs.Contains(reference))
        {
            if (!entry.Materializable)
            {
                Add(
                    issues,
                    "source",
                    "effect_source_not_materializable",
                    "source definition explicitly materializable as an active instance",
                    entry.Key.ToString());
            }
            if (!entry.Active)
            {
                Add(
                    issues,
                    "source",
                    "effect_source_inactive",
                    "source current state authorizes application",
                    entry.Key.ToString());
            }
            ValidateActivePredicate(entry, issues);
            ValidateTargetKind(entry.Definition, targetKind, issues);
            return issues.Count == 0
                ? new EffectSourceResolution(
                    DetachEntry(entry),
                    Array.Empty<ValidationIssue>())
                : new EffectSourceResolution(null, issues);
        }

        Add(
            issues,
            "source.sourceRef",
            _refsByAlias.ContainsKey(RefAlias(realm, kind, sourceRef, definitionKey))
                ? "effect_source_selector_confusable"
                : "effect_source_selector_unresolved",
            "one exact accepted same-turn sourceRef export",
            sourceRef);
        return new EffectSourceResolution(null, issues);
    }

    private static void ValidateResolvedEntry(
        EffectSourceAuthorityEntry entry,
        string targetKind,
        JsonObject? parameters,
        List<ValidationIssue> issues)
    {
        if (!entry.Materializable)
            Add(issues, "source", "effect_source_not_materializable", "source definition explicitly materializable as an active instance", entry.Key.ToString());
        if (!entry.Active)
            Add(issues, "source", "effect_source_inactive", "source current state authorizes application", entry.Key.ToString());
        ValidateActivePredicate(entry, issues);
        ValidateTargetKind(entry.Definition, targetKind, issues);
        ValidateParameters(entry.Definition, parameters, issues);
    }

    private static void ValidateActivePredicate(
        EffectSourceAuthorityEntry entry,
        List<ValidationIssue> issues)
    {
        var predicate = EffectSourcePredicateCatalog.RequiredPredicate(entry.Definition);
        if (predicate == null)
            return;
        if (!EffectSourcePredicateCatalog.IsAllowedForSource(entry.Key.Kind, predicate))
        {
            Add(
                issues,
                "source.activePredicate",
                "effect_source_predicate_incompatible",
                "registered predicate compatible with the exact source kind",
                $"{entry.Key.Kind}:{predicate}");
            return;
        }
        if (!entry.SatisfiedPredicates.Contains(predicate))
        {
            Add(
                issues,
                "source.activePredicate",
                "effect_source_predicate_unsatisfied",
                "source current state satisfies its registered active predicate",
                $"{entry.Key.Kind}:{entry.Key.SourceId}:{predicate}");
        }
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
        IEnumerable<EffectSourceExport> ownerExports,
        IEnumerable<WoundSourceGroupAuthority> woundGroups,
        IReadOnlyList<ValidationIssue> issues,
        IReadOnlySet<string> grantedApplicationAuthorities)
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
                    ["sourceRef"] = entry.SourceRef,
                    ["requiredApplicationAuthority"] = entry.RequiredApplicationAuthority,
                    ["satisfiedPredicates"] = new JsonArray(entry.SatisfiedPredicates
                        .OrderBy(static predicate => predicate, StringComparer.Ordinal)
                        .Select(static predicate => (JsonNode)predicate)
                        .ToArray()),
                    ["definition"] = entry.Definition.DeepClone()
                }).ToArray()),
            ["owners"] = CreateOwnerExportArray(ownerExports, canonical: false),
            ["woundGroups"] = CreateWoundGroupArray(woundGroups, canonical: false),
            ["grantedApplicationAuthorities"] = new JsonArray(
                grantedApplicationAuthorities
                    .OrderBy(static authority => authority, StringComparer.Ordinal)
                    .Select(static authority => (JsonNode)authority)
                    .ToArray()),
            ["issues"] = new JsonArray(issues.Select(issue => (JsonNode)new JsonObject
            {
                ["code"] = issue.Code,
                ["path"] = issue.FilePath
            }).ToArray())
        };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(root.ToJsonString())));
    }

    private static string CreateCanonicalFingerprint(
        IEnumerable<EffectSourceAuthorityEntry> entries,
        IEnumerable<EffectSourceExport> ownerExports,
        IEnumerable<WoundSourceGroupAuthority> woundGroups,
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
                    ["requiredApplicationAuthority"] = entry.RequiredApplicationAuthority,
                    ["satisfiedPredicates"] = new JsonArray(entry.SatisfiedPredicates
                        .OrderBy(static predicate => predicate, StringComparer.Ordinal)
                        .Select(static predicate => (JsonNode)predicate)
                        .ToArray()),
                    ["definition"] = entry.Definition.DeepClone()
                }).ToArray()),
            ["owners"] = CreateOwnerExportArray(ownerExports, canonical: true),
            ["woundGroups"] = CreateWoundGroupArray(woundGroups, canonical: true),
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

    private static JsonArray CreateOwnerExportArray(
        IEnumerable<EffectSourceExport> ownerExports,
        bool canonical) =>
        new(ownerExports
            .OrderBy(static export => export.Realm, StringComparer.Ordinal)
            .ThenBy(static export => export.Kind, StringComparer.Ordinal)
            .ThenBy(static export => export.SourceId, StringComparer.Ordinal)
            .ThenBy(static export => export.SourceRef, StringComparer.Ordinal)
            .Select(export => (JsonNode)new JsonObject
            {
                ["realm"] = export.Realm,
                ["kind"] = export.Kind,
                ["sourceId"] = export.SourceId,
                ["materializable"] = export.Materializable,
                ["active"] = export.Active,
                ["sameTurn"] = canonical ? null : export.SameTurn,
                ["sourceRef"] = canonical ? null : export.SourceRef,
                ["requiredApplicationAuthority"] = export.RequiredApplicationAuthority,
                ["satisfiedPredicates"] = new JsonArray(
                    (export.SatisfiedPredicates ?? new HashSet<string>(StringComparer.Ordinal))
                    .OrderBy(static predicate => predicate, StringComparer.Ordinal)
                    .Select(static predicate => (JsonNode)predicate)
                    .ToArray()),
                ["definitions"] = export.Definitions.DeepClone()
            })
            .ToArray());

    private static JsonArray CreateWoundGroupArray(
        IEnumerable<WoundSourceGroupAuthority> woundGroups,
        bool canonical) =>
        new(woundGroups
            .OrderBy(static group => group.Key.Realm, StringComparer.Ordinal)
            .ThenBy(static group => group.Key.SourceId, StringComparer.Ordinal)
            .Select(group => (JsonNode)new JsonObject
            {
                ["realm"] = group.Key.Realm,
                ["kind"] = group.Key.Kind,
                ["sourceId"] = group.Key.SourceId,
                ["owner"] = new JsonObject
                {
                    ["realm"] = group.Owner.Realm,
                    ["ownerKind"] = group.Owner.OwnerKind,
                    ["ownerId"] = group.Owner.OwnerId,
                    ["carrierPath"] = group.Owner.CarrierPath
                },
                ["target"] = new JsonObject
                {
                    ["realm"] = group.Target.Realm,
                    ["kind"] = group.Target.Kind,
                    ["targetId"] = group.Target.TargetId
                },
                ["sameTurn"] = canonical ? null : group.SameTurn,
                ["sourceRef"] = canonical ? null : group.SourceRef,
                ["preparedSourceExportFingerprint"] = canonical
                    ? null
                    : group.PreparedSourceExportFingerprint,
                ["graphAuthorityFingerprint"] = group.GraphAuthorityFingerprint,
                ["definitions"] = new JsonArray(group.Definitions
                    .Select(definition => (JsonNode)new JsonObject
                    {
                        ["definitionKey"] = definition.DefinitionKey,
                        ["definition"] = definition.Definition
                    })
                    .ToArray()),
                ["applicationRoots"] = CreateRootLineageArray(
                    group.ApplicationRootLineage),
                ["existingRoots"] = CreateRootLineageArray(
                    group.ExistingRootLineage)
            })
            .ToArray());

    private static JsonArray CreateRootLineageArray(
        IReadOnlyList<WoundRootLineageAuthorityRow> roots) =>
        new(roots.Select(root => (JsonNode)new JsonObject
        {
            ["applicationRef"] = root.ApplicationRef,
            ["effectId"] = root.EffectId,
            ["definitionKey"] = root.DefinitionKey,
            ["ownershipKind"] = root.OwnershipDomain.Kind,
            ["complicationId"] = root.OwnershipDomain.ComplicationId
        }).ToArray());

    private static string Alias(string realm, string kind, string sourceId, string definitionKey) =>
        realm + "\u001f" + kind + "\u001f" +
        MortalLocationIdentityState.BuildConfusableKey(sourceId) + "\u001f" +
        MortalLocationIdentityState.BuildConfusableKey(definitionKey);

    private static EffectSourceAuthorityEntry DetachEntry(
        EffectSourceAuthorityEntry entry) =>
        entry with
        {
            Definition = entry.Definition.DeepClone().AsObject(),
            SatisfiedPredicates = entry.SatisfiedPredicates.ToFrozenSet(
                StringComparer.Ordinal)
        };

    private static EffectSourceExport DetachExport(EffectSourceExport export) =>
        export with
        {
            Definitions = export.Definitions.DeepClone().AsArray(),
            SatisfiedPredicates = (export.SatisfiedPredicates ??
                    new HashSet<string>(StringComparer.Ordinal))
                .ToFrozenSet(StringComparer.Ordinal)
        };

    private static string RefAlias(string realm, string kind, string sourceRef, string definitionKey) =>
        realm + "\u001f" + kind + "\u001f" +
        MortalLocationIdentityState.BuildConfusableKey(sourceRef) + "\u001f" +
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
        internal Dictionary<EffectSourceOwnerKey, EffectSourceExport> Owners { get; } = new();
        internal List<EffectSourceExport> OwnerExports { get; } = new();
        internal Dictionary<EffectIdentitySourceGroup, WoundSourceGroupAuthority>
            WoundGroups { get; } = new();
        internal Dictionary<string, List<EffectSourceOwnerKey>> OwnerAliases { get; } = new(StringComparer.Ordinal);
        internal HashSet<EffectSourceOwnerKey> InvalidOwners { get; } = new();
        internal Dictionary<string, List<EffectSourceAuthorityEntry>> ByAlias { get; } = new(StringComparer.Ordinal);
        internal Dictionary<EffectSourceReferenceKey, EffectSourceAuthorityEntry> ByRef { get; } = new();
        internal Dictionary<string, List<EffectSourceReferenceKey>> RefsByAlias { get; } = new(StringComparer.Ordinal);
        internal HashSet<EffectSourceReferenceKey> InvalidRefs { get; } = new();
        internal HashSet<string> HistoricalAliases { get; }
        internal HashSet<string> GrantedApplicationAuthorities { get; }
        internal HashSet<EffectSourceKey> InvalidKeys { get; } = new();
        internal List<ValidationIssue> Issues { get; } = new();

        internal Builder(
            IEnumerable<string> historicalSourceIds,
            IEnumerable<string> grantedApplicationAuthorities)
        {
            HistoricalAliases = historicalSourceIds
                .Select(MortalLocationIdentityState.BuildConfusableKey)
                .ToHashSet(StringComparer.Ordinal);
            GrantedApplicationAuthorities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var authority in grantedApplicationAuthorities)
            {
                if (!TryExact(authority) ||
                    !EffectBuiltInSourceCatalog.IsRegisteredApplicationAuthority(authority))
                {
                    Issues.Add(NewIssue(
                        "source.applicationAuthority",
                        "effect_source_application_authority_unknown",
                        "one registered exact built-in application authority",
                        authority));
                    continue;
                }
                GrantedApplicationAuthorities.Add(authority);
            }
        }

        internal void AddRange(IEnumerable<EffectSourceExport> exports, bool sameTurn)
        {
            foreach (var export in exports)
                AddExport(export, sameTurn);
        }

        internal void AddCompositionIssues(
            IReadOnlyList<ValidationIssue>? compositionIssues)
        {
            if (compositionIssues is null)
                return;
            Issues.AddRange(compositionIssues);
        }

        internal void AddWoundGroups(
            IReadOnlyList<WoundSourceGroupAuthority> groups)
        {
            foreach (var group in groups)
                AddWoundGroup(group);
        }

        internal bool ValidateWoundAggregateBounds(
            IReadOnlyList<EffectSourceExport> preTurn,
            IReadOnlyList<EffectSourceExport> sameTurn,
            IReadOnlyList<WoundSourceGroupAuthority> groups)
        {
            static long CountDefinitions(IEnumerable<EffectSourceExport> exports) =>
                exports.Where(static export => export is not null && string.Equals(
                        export.Kind,
                        "wound",
                        StringComparison.Ordinal))
                    .Sum(static export => (long)(export.Definitions?.Count ?? 0));

            var preTurnCount = CountDefinitions(preTurn);
            var sameTurnCount = CountDefinitions(sameTurn);
            var preTurnRootCount = groups
                .Where(static group => group is not null && !group.SameTurn)
                .Sum(static group => (long)group.ExistingRootLineage.Count);
            var sameTurnRootCount = groups
                .Where(static group => group is not null && group.SameTurn)
                .Sum(static group => (long)group.ApplicationRootLineage.Count);
            if (preTurnCount > MaximumPreTurnWoundDefinitions)
            {
                Issues.Add(NewIssue(
                    "sources.wound.preTurn",
                    "effect_source_wound_pre_turn_aggregate_limit_exceeded",
                    $"at most {MaximumPreTurnWoundDefinitions} pre-turn wound definitions before source composition",
                    preTurnCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }
            if (sameTurnCount > MaximumSameTurnWoundDefinitions)
            {
                Issues.Add(NewIssue(
                    "sources.wound.sameTurn",
                    "effect_source_wound_same_turn_aggregate_limit_exceeded",
                    $"at most {MaximumSameTurnWoundDefinitions} same-turn wound definitions before source composition",
                    sameTurnCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }
            if (preTurnRootCount > MaximumPreTurnWoundDefinitions)
            {
                Issues.Add(NewIssue(
                    "sources.wound.preTurn.rootBindings",
                    "effect_source_wound_pre_turn_root_aggregate_limit_exceeded",
                    $"at most {MaximumPreTurnWoundDefinitions} pre-turn wound root bindings before source composition",
                    preTurnRootCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }
            if (sameTurnRootCount > MaximumSameTurnWoundDefinitions)
            {
                Issues.Add(NewIssue(
                    "sources.wound.sameTurn.rootApplications",
                    "effect_source_wound_same_turn_root_aggregate_limit_exceeded",
                    $"at most {MaximumSameTurnWoundDefinitions} same-turn wound root applications before source composition",
                    sameTurnRootCount.ToString(
                        System.Globalization.CultureInfo.InvariantCulture)));
            }
            return preTurnCount <= MaximumPreTurnWoundDefinitions &&
                   sameTurnCount <= MaximumSameTurnWoundDefinitions &&
                   preTurnRootCount <= MaximumPreTurnWoundDefinitions &&
                   sameTurnRootCount <= MaximumSameTurnWoundDefinitions;
        }

        private void AddWoundGroup(WoundSourceGroupAuthority group)
        {
            if (group is null ||
                !string.Equals(group.Key.Kind, "wound", StringComparison.Ordinal) ||
                !TryExact(group.Key.Realm) ||
                !TryExact(group.Key.SourceId) ||
                !WoundEffectCarrierAdapter.TryCreateTargetKey(
                    group.Owner,
                    out var expectedTarget) ||
                expectedTarget != group.Target ||
                !string.Equals(group.Owner.Realm, group.Key.Realm, StringComparison.Ordinal) ||
                group.SameTurn != (group.SourceRef is not null) ||
                (group.SourceRef is not null && !TryExact(group.SourceRef)) ||
                !string.Equals(
                    group.GraphAuthorityFingerprint,
                    group.RecomputeGraphAuthorityFingerprint(),
                    StringComparison.Ordinal))
            {
                Issues.Add(NewIssue(
                    "sources.wound.groups",
                    "effect_source_wound_group_authority_invalid",
                    "one exact owner-bound sealed wound source group",
                    group?.Key.ToString() ?? "null"));
                return;
            }

            var ownerKey = new EffectSourceOwnerKey(
                group.Key.Realm,
                group.Key.Kind,
                group.Key.SourceId);
            string? rootIssue = null;
            var rootsValid = ValidateRootRows(group, out rootIssue);
            if (!Owners.TryGetValue(ownerKey, out var export) ||
                export.SameTurn != group.SameTurn ||
                !string.Equals(export.SourceRef, group.SourceRef, StringComparison.Ordinal) ||
                !DefinitionsAgree(export.Definitions, group.Definitions) ||
                !rootsValid)
            {
                Issues.Add(NewIssue(
                    "sources.wound.groups",
                    rootIssue ?? "effect_source_wound_group_export_mismatch",
                    "exact source export, complete definition graph, and disjoint root selectors",
                    group.Key.ToString()));
                return;
            }

            if (!WoundGroups.TryAdd(group.Key, group.DetachedCopy()))
            {
                Issues.Add(NewIssue(
                    "sources.wound.groups",
                    "effect_source_wound_group_duplicate",
                    "one exact wound source group authority",
                    group.Key.ToString()));
            }
        }

        private static bool DefinitionsAgree(
            JsonArray exported,
            IReadOnlyList<WoundEffectSourceDefinition> grouped)
        {
            if (exported.Count != grouped.Count)
                return false;
            for (var index = 0; index < exported.Count; index++)
            {
                if (exported[index] is not JsonObject definition ||
                    !string.Equals(
                        definition["definitionKey"]?.GetValue<string>(),
                        grouped[index].DefinitionKey,
                        StringComparison.Ordinal) ||
                    !JsonNode.DeepEquals(definition, grouped[index].Definition))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool ValidateRootRows(
            WoundSourceGroupAuthority group,
            out string? issueCode)
        {
            issueCode = null;
            var definitionKeys = group.Definitions
                .Select(static definition => definition.DefinitionKey)
                .ToHashSet(StringComparer.Ordinal);
            var applicationRefs = new HashSet<string>(StringComparer.Ordinal);
            var effectIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var row in group.ApplicationRootLineage.Concat(
                         group.ExistingRootLineage))
            {
                var hasApplication = TryExact(row.ApplicationRef ?? string.Empty);
                var hasEffect = TryExact(row.EffectId ?? string.Empty);
                var ownershipDomainValid = row.OwnershipDomain is not null &&
                    (string.Equals(
                         row.OwnershipDomain.Kind,
                         "base_wound",
                         StringComparison.Ordinal) &&
                     row.OwnershipDomain.ComplicationId is null ||
                     string.Equals(
                         row.OwnershipDomain.Kind,
                         "complication",
                         StringComparison.Ordinal) &&
                     TryExact(row.OwnershipDomain.ComplicationId ?? string.Empty));
                if (hasApplication == hasEffect ||
                    !definitionKeys.Contains(row.DefinitionKey) ||
                    !ownershipDomainValid ||
                    (hasApplication && !applicationRefs.Add(row.ApplicationRef!)) ||
                    (hasEffect && !effectIds.Add(row.EffectId!)))
                {
                    issueCode = "effect_source_wound_group_root_invalid";
                    return false;
                }
            }
            if (group.ApplicationRootLineage.Any(static row => row.EffectId is not null) ||
                group.ExistingRootLineage.Any(static row => row.ApplicationRef is not null))
            {
                issueCode = "effect_source_wound_group_root_partition_invalid";
                return false;
            }
            return true;
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
            if (export.RequiredApplicationAuthority is { } requiredAuthority &&
                (!TryExact(requiredAuthority) ||
                 !EffectBuiltInSourceCatalog.IsRegisteredApplicationAuthority(requiredAuthority)))
            {
                Issues.Add(NewIssue(
                    sourcePath + ".requiredApplicationAuthority",
                    "effect_source_application_authority_unknown",
                    "one registered exact built-in application authority",
                    requiredAuthority));
                return;
            }

            var normalizedExport = export with
            {
                SameTurn = sameTurn,
                Definitions = export.Definitions.DeepClone().AsArray(),
                SatisfiedPredicates = EffectSourcePredicateCatalog.NormalizeSatisfied(
                        export.Active,
                        export.SatisfiedPredicates)
                    .ToFrozenSet(StringComparer.Ordinal)
            };
            OwnerExports.Add(normalizedExport);
            RegisterOwner(normalizedExport, sourcePath);

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
                var requiredPredicate = EffectSourcePredicateCatalog.RequiredPredicate(definition);
                if (requiredPredicate != null &&
                    !EffectSourcePredicateCatalog.IsAllowedForSource(
                        export.Kind,
                        requiredPredicate))
                {
                    Issues.Add(NewIssue(
                        sourcePath + ".activeEffectDefinitions.lifetime.activePredicate",
                        "effect_source_predicate_incompatible",
                        "registered predicate compatible with the exact source kind",
                        $"{export.Kind}:{requiredPredicate}"));
                    continue;
                }
                var key = new EffectSourceKey(export.Realm, export.Kind, export.SourceId, definitionKey);
                var satisfiedPredicates = EffectSourcePredicateCatalog.NormalizeSatisfied(
                        export.Active,
                        export.SatisfiedPredicates)
                    .ToFrozenSet(StringComparer.Ordinal);
                var entry = new EffectSourceAuthorityEntry(
                    key,
                    definition.DeepClone().AsObject(),
                    export.Materializable,
                    export.Active,
                    sameTurn,
                    export.SourceRef,
                    satisfiedPredicates,
                    export.RequiredApplicationAuthority);
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

                if (export.SourceRef == null)
                    continue;
                if (!sameTurn || !TryExact(export.SourceRef))
                {
                    Issues.Add(NewIssue(sourcePath + ".sourceRef", "effect_source_authority_invalid_ref", "exact same-turn sourceRef", export.SourceRef));
                    continue;
                }
                var reference = new EffectSourceReferenceKey(
                    export.Realm,
                    export.Kind,
                    export.SourceRef,
                    definitionKey);
                if (!ByRef.TryAdd(reference, entry))
                {
                    InvalidRefs.Add(reference);
                    Issues.Add(NewIssue(sourcePath + ".sourceRef", "effect_source_authority_duplicate_ref", "one exact same-turn sourceRef", export.SourceRef));
                }
                var refAlias = EffectSourceAuthority.RefAlias(
                    export.Realm,
                    export.Kind,
                    export.SourceRef,
                    definitionKey);
                if (!RefsByAlias.TryGetValue(refAlias, out var refs))
                {
                    refs = new List<EffectSourceReferenceKey>();
                    RefsByAlias.Add(refAlias, refs);
                }
                refs.Add(reference);
                if (refs.Distinct().Count() > 1)
                {
                    foreach (var candidate in refs)
                        InvalidRefs.Add(candidate);
                    Issues.Add(NewIssue(sourcePath + ".sourceRef", "effect_source_authority_confusable_ref", "one exact/confusable same-turn sourceRef", export.SourceRef));
                }
            }
        }

        internal void ValidateDefinitionLinks()
        {
            foreach (var entry in Entries.Values.OrderBy(
                         static value => value.Key.ToString(),
                         StringComparer.Ordinal))
            {
                if (entry.Definition["links"] is not JsonArray links)
                    continue;
                for (var index = 0; index < links.Count; index++)
                {
                    if (links[index] is not JsonObject link ||
                        !TryReadExactString(link["kind"], out var linkKind) ||
                        !TryReadExactString(link["targetId"], out var targetId))
                    {
                        continue;
                    }

                    var ownerKind = string.Equals(
                        linkKind,
                        "combat",
                        StringComparison.Ordinal)
                        ? "combat_action"
                        : linkKind;
                    var ownerKey = new EffectSourceOwnerKey(
                        entry.Key.Realm,
                        ownerKind,
                        targetId);
                    if (Owners.ContainsKey(ownerKey) && !InvalidOwners.Contains(ownerKey))
                        continue;

                    var path = $"sources[{entry.Key.Kind}:{entry.Key.SourceId}]." +
                               $"activeEffectDefinitions[{entry.Key.DefinitionKey}]." +
                               $"links[{index}].targetId";
                    var alias = OwnerAlias(
                        ownerKey.Realm,
                        ownerKey.Kind,
                        ownerKey.SourceId);
                    string code;
                    string expected;
                    if (OwnerAliases.TryGetValue(alias, out var aliases) && aliases.Count > 0)
                    {
                        code = "effect_source_link_target_confusable";
                        expected = "one exact non-confusable linked owner identity";
                    }
                    else if (Owners.Keys.Any(candidate =>
                                 string.Equals(candidate.Kind, ownerKey.Kind, StringComparison.Ordinal) &&
                                 string.Equals(candidate.SourceId, ownerKey.SourceId, StringComparison.Ordinal) &&
                                 !string.Equals(candidate.Realm, ownerKey.Realm, StringComparison.Ordinal)))
                    {
                        code = "effect_source_link_target_realm_mismatch";
                        expected = "linked owner in the effect realm";
                    }
                    else
                    {
                        code = "effect_source_link_target_unresolved";
                        expected = "one exact linked owner in composed source authority";
                    }
                    Issues.Add(NewIssue(path, code, expected, ownerKey.ToString()));
                }
            }
        }

        internal void ValidateWoundBindings()
        {
            foreach (var entry in Entries.Values.OrderBy(
                         static value => value.Key.ToString(),
                         StringComparer.Ordinal))
            {
                var consequenceComponents = entry.Definition["components"] is JsonArray components
                    ? components
                        .Select(static (component, index) => (component, index))
                        .Where(static candidate =>
                            candidate.component is JsonObject component &&
                            string.Equals(
                                ReadExact(component["profile"]),
                                "wound_consequence",
                                StringComparison.Ordinal))
                        .Select(static candidate =>
                            ((JsonObject)candidate.component!, candidate.index))
                        .ToArray()
                    : Array.Empty<(JsonObject Component, int Index)>();
                if (consequenceComponents.Length == 0)
                    continue;

                var definitionPath =
                    $"sources[{entry.Key.Kind}:{entry.Key.SourceId}]." +
                    $"activeEffectDefinitions[{entry.Key.DefinitionKey}]";
                var woundSourceLinks = entry.Definition["links"] is JsonArray links
                    ? links
                        .OfType<JsonObject>()
                        .Where(static link =>
                            string.Equals(
                                ReadExact(link["kind"]),
                                "wound",
                                StringComparison.Ordinal) &&
                            string.Equals(
                                ReadExact(link["role"]),
                                "source",
                                StringComparison.Ordinal))
                        .ToArray()
                    : Array.Empty<JsonObject>();

                if (woundSourceLinks.Length == 0)
                {
                    RejectWoundBinding(
                        entry.Key,
                        definitionPath + ".links",
                        "effect_source_wound_link_missing",
                        "one exact wound source link for every wound_consequence",
                        "missing");
                    continue;
                }
                if (woundSourceLinks.Length != 1 ||
                    !TryReadExactString(
                        woundSourceLinks[0]["targetId"],
                        out var linkedWoundId))
                {
                    RejectWoundBinding(
                        entry.Key,
                        definitionPath + ".links",
                        "effect_source_wound_link_ambiguous",
                        "one exact/confusable-unique wound source link",
                        woundSourceLinks.Length.ToString());
                    continue;
                }

                foreach (var (component, componentIndex) in consequenceComponents)
                {
                    var payloadWoundId = component["payload"] is JsonObject payload &&
                                         TryReadExactString(
                                             payload["woundId"],
                                             out var candidate)
                        ? candidate
                        : string.Empty;
                    if (!string.Equals(
                            payloadWoundId,
                            linkedWoundId,
                            StringComparison.Ordinal))
                    {
                        RejectWoundBinding(
                            entry.Key,
                            $"{definitionPath}.components[{componentIndex}].payload.woundId",
                            "effect_source_wound_link_mismatch",
                            "payload woundId equal to the exact wound source link",
                            payloadWoundId);
                    }
                    if (string.Equals(
                            entry.Key.Kind,
                            "wound",
                            StringComparison.Ordinal) &&
                        !string.Equals(
                            payloadWoundId,
                            entry.Key.SourceId,
                            StringComparison.Ordinal))
                    {
                        RejectWoundBinding(
                            entry.Key,
                            $"{definitionPath}.components[{componentIndex}].payload.woundId",
                            "effect_source_wound_payload_mismatch",
                            "payload woundId equal to the exact wound source identity",
                            payloadWoundId);
                    }
                }

                if (entry.Definition["lifetime"] is JsonObject lifetime &&
                    string.Equals(
                        ReadExact(lifetime["mode"]),
                        "source_bound",
                        StringComparison.Ordinal) &&
                    (!string.Equals(
                         entry.Key.Kind,
                         "wound",
                         StringComparison.Ordinal) ||
                     !string.Equals(
                         linkedWoundId,
                         entry.Key.SourceId,
                         StringComparison.Ordinal)))
                {
                    RejectWoundBinding(
                        entry.Key,
                        definitionPath + ".lifetime",
                        "effect_source_wound_source_bound_mismatch",
                        "source-bound wound consequence owned by its exact linked wound",
                        entry.Key.ToString());
                }
            }
        }

        private void RejectWoundBinding(
            EffectSourceKey key,
            string path,
            string code,
            string expected,
            string actual)
        {
            InvalidKeys.Add(key);
            Issues.Add(NewIssue(path, code, expected, actual));
        }

        private static string? ReadExact(JsonNode? node) =>
            TryReadExactString(node, out var value) ? value : null;

        private void RegisterOwner(EffectSourceExport export, string sourcePath)
        {
            var key = new EffectSourceOwnerKey(
                export.Realm,
                export.Kind,
                export.SourceId);
            if (!Owners.TryAdd(key, export))
            {
                InvalidOwners.Add(key);
                Issues.Add(NewIssue(
                    sourcePath,
                    "effect_source_authority_duplicate_owner",
                    "one exact source owner export",
                    key.ToString()));
            }

            var alias = OwnerAlias(key.Realm, key.Kind, key.SourceId);
            if (!OwnerAliases.TryGetValue(alias, out var candidates))
            {
                candidates = new List<EffectSourceOwnerKey>();
                OwnerAliases.Add(alias, candidates);
            }
            candidates.Add(key);
            if (candidates.Any(candidate => !Equals(candidate, key)))
            {
                foreach (var candidate in candidates)
                    InvalidOwners.Add(candidate);
                Issues.Add(NewIssue(
                    sourcePath,
                    "effect_source_authority_confusable_owner",
                    "one exact/confusable source owner export",
                    key.ToString()));
            }
        }

        private static string OwnerAlias(string realm, string kind, string sourceId) =>
            realm + "\u001f" + kind + "\u001f" +
            MortalLocationIdentityState.BuildConfusableKey(sourceId);

        private static bool TryExact(string value) =>
            value.Length > 0 && string.Equals(value, value.Trim(), StringComparison.Ordinal);
    }
}
