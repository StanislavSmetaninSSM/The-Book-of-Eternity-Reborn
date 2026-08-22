using System.Collections.Frozen;

namespace BookOfEternityClient.Services;

internal enum ResourceMutationSourceState
{
    Active,
    Inactive,
    Historical
}

internal sealed record ResourceMutationSourceExport(
    string SourceKind,
    string SourceId,
    string AuthorityFingerprint,
    ResourceMutationSourceState State,
    bool SameTurn,
    ResourceOwnerKey? BoundOwner = null);

internal sealed record ResourceMutationSourceRequest(
    string SourceKind,
    string SourceId,
    ResourceOperation Operation);

internal sealed record ResourceAuthorizedSourceRoute(
    ResourceMutationPhase Phase,
    int Priority,
    ResourcePolicyBinding PolicyBinding,
    ResourceSourceEvidence SourceEvidence,
    bool SameTurn);

internal sealed record ResourceMutationSourceResolution(
    ResourceAuthorizedSourceRoute? Route,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Route != null && Issues.Count == 0;
}

internal sealed record ResourceMutationSourceCatalogResult(
    ResourceMutationSourceCatalog? Catalog,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Catalog != null && Issues.Count == 0;
}

internal sealed class ResourceMutationSourceCatalog
{
    private static readonly FrozenDictionary<string, SourceRouteDefinition> Routes =
        new Dictionary<string, SourceRouteDefinition>(StringComparer.Ordinal)
        {
            ["action_cost"] = BoundCost(
                ResourceMutationPhase.DirectCost,
                priority: 100,
                ResourceOperation.Spend),
            ["combat_outcome"] = BoundOutcome(
                ResourceMutationPhase.DirectOutcome,
                priority: 100,
                ResourceOperation.Damage,
                ResourceOperation.Restore),
            ["narrative_outcome"] = BoundOutcome(
                ResourceMutationPhase.DirectOutcome,
                priority: 110,
                ResourceOperation.Damage,
                ResourceOperation.Restore,
                ResourceOperation.Spend,
                ResourceOperation.Gain),
            ["local_item_cost"] = ItemCost(
                ResourceMutationPhase.DirectCost,
                priority: 120,
                ResourceOperation.Spend),
            ["local_item_outcome"] = ItemOutcome(
                ResourceMutationPhase.DirectOutcome,
                priority: 120,
                ResourceOperation.Damage,
                ResourceOperation.Restore,
                ResourceOperation.Gain),
            ["afterlife_cost"] = Cost(
                ResourceMutationPhase.DirectCost,
                priority: 130,
                ResourceOperation.Spend),
            ["afterlife_outcome"] = Outcome(
                ResourceMutationPhase.DirectOutcome,
                priority: 130,
                ResourceOperation.Damage,
                ResourceOperation.Restore,
                ResourceOperation.Gain),
            ["registered_system_outcome"] = Outcome(
                ResourceMutationPhase.RegisteredSystemOutcome,
                priority: 100,
                ResourceOperation.Damage,
                ResourceOperation.Restore,
                ResourceOperation.Spend,
                ResourceOperation.Gain),
            ["effect_component"] = new SourceRouteDefinition(
                ResourceMutationPhase.EffectTrigger,
                Priority: 200,
                new[]
                {
                    ResourceOperation.Damage,
                    ResourceOperation.Restore,
                    ResourceOperation.Spend,
                    ResourceOperation.Gain
                }.ToFrozenSet(),
                RejectBounds: false,
                BoundOwnerKind: null,
                RequiresBoundOwner: true),
            ["bounded_receipt"] = Outcome(
                ResourceMutationPhase.EffectTrigger,
                priority: 210,
                ResourceOperation.Damage,
                ResourceOperation.Restore,
                ResourceOperation.Spend,
                ResourceOperation.Gain)
        }.ToFrozenDictionary(StringComparer.Ordinal);

    private readonly FrozenDictionary<SourceKey, ResourceMutationSourceExport> _sources;

    private ResourceMutationSourceCatalog(
        FrozenDictionary<SourceKey, ResourceMutationSourceExport> sources)
    {
        _sources = sources;
        Fingerprint = CreateFingerprint(sources.Values);
    }

    internal string Fingerprint { get; }

    internal IReadOnlyList<ResourceMutationSourceExport> Exports =>
        Array.AsReadOnly(_sources.Values
            .OrderBy(static source => source.SourceKind, StringComparer.Ordinal)
            .ThenBy(static source => source.SourceId, StringComparer.Ordinal)
            .Select(static source => source with { })
            .ToArray());

    internal static ResourceMutationSourceCatalogResult Create(
        IEnumerable<ResourceMutationSourceExport> exports)
    {
        ArgumentNullException.ThrowIfNull(exports);
        var candidates = exports.ToArray();
        var issues = new List<ValidationIssue>();
        if (candidates.Length > ResourceMaterializationContract.MaxLiveEntries)
        {
            Add(
                issues,
                "resource_source_limit_exceeded",
                $"at most {ResourceMaterializationContract.MaxLiveEntries} source exports",
                candidates.Length.ToString());
            return new ResourceMutationSourceCatalogResult(null, issues.ToArray());
        }

        var exact = new Dictionary<SourceKey, ResourceMutationSourceExport>();
        var aliases = new HashSet<string>(StringComparer.Ordinal);
        foreach (var export in candidates)
        {
            if (!Routes.TryGetValue(export.SourceKind, out var routeDefinition))
            {
                Add(
                    issues,
                    "resource_source_route_unknown",
                    "registered closed resource source route",
                    export.SourceKind);
                continue;
            }
            if (routeDefinition.BoundOwnerKind is { } boundOwnerKind)
            {
                if (export.BoundOwner == null ||
                    export.BoundOwner.OwnerKind != boundOwnerKind ||
                    !string.Equals(
                        export.BoundOwner.ResourceOwnerId,
                        export.SourceId,
                        StringComparison.Ordinal))
                {
                    Add(
                        issues,
                        "resource_source_owner_binding_invalid",
                        $"exact {boundOwnerKind} owner bound to the same sourceId",
                        export.BoundOwner == null
                            ? "missing"
                            : $"{export.BoundOwner.Realm}/{export.BoundOwner.OwnerKind}/{export.BoundOwner.ResourceOwnerId}");
                    continue;
                }
            }
            else if (routeDefinition.RequiresBoundOwner)
            {
                if (export.BoundOwner == null)
                {
                    Add(
                        issues,
                        "resource_source_owner_binding_required",
                        "one exact target owner binding for this source route",
                        export.SourceKind);
                    continue;
                }
            }
            else if (export.BoundOwner != null)
            {
                Add(
                    issues,
                    "resource_source_owner_binding_forbidden",
                    "no owner binding for this source route",
                    export.SourceKind);
                continue;
            }
            if (!ResourceMaterializationContract.IsExactIdentifier(export.SourceId))
            {
                Add(
                    issues,
                    "resource_source_id_invalid",
                    "exact source identity",
                    export.SourceId);
                continue;
            }
            if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                    export.AuthorityFingerprint))
            {
                Add(
                    issues,
                    "resource_source_fingerprint_invalid",
                    "exact source authority fingerprint",
                    export.AuthorityFingerprint);
                continue;
            }
            if (export.SameTurn && export.State != ResourceMutationSourceState.Active)
            {
                Add(
                    issues,
                    "resource_source_same_turn_state_invalid",
                    "active same-turn source authority",
                    export.State.ToString());
                continue;
            }

            var key = new SourceKey(export.SourceKind, export.SourceId);
            if (!exact.TryAdd(key, export))
            {
                Add(
                    issues,
                    "resource_source_duplicate_exact",
                    "one exact source authority per kind and ID",
                    export.SourceKind + "/" + export.SourceId);
            }
            var alias = export.SourceKind + "\0" +
                        ResourceMaterializationContract.BuildConfusableKey(export.SourceId);
            if (!aliases.Add(alias))
            {
                Add(
                    issues,
                    "resource_source_duplicate_confusable",
                    "case/confusable-unique source authority per kind",
                    export.SourceKind + "/" + export.SourceId);
            }
        }

        return issues.Count == 0
            ? new ResourceMutationSourceCatalogResult(
                new ResourceMutationSourceCatalog(
                    exact.ToFrozenDictionary(SourceKeyComparer.Instance)),
                Array.Empty<ValidationIssue>())
            : new ResourceMutationSourceCatalogResult(null, issues.ToArray());
    }

    internal ResourceMutationSourceResolution Resolve(
        ResourceMutationSourceRequest request,
        ResourceDefinition definition,
        ResourceCoordinate? target = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(definition);
        var issues = new List<ValidationIssue>();
        if (!Routes.TryGetValue(request.SourceKind, out var routeDefinition))
        {
            Add(
                issues,
                "resource_source_route_unknown",
                "registered closed resource source route",
                request.SourceKind);
            return Failure(issues);
        }
        if (!ResourceMaterializationContract.IsExactIdentifier(request.SourceId) ||
            !_sources.TryGetValue(
                new SourceKey(request.SourceKind, request.SourceId),
                out var source))
        {
            Add(
                issues,
                "resource_source_unknown",
                "exact active source authority",
                request.SourceKind + "/" + request.SourceId);
            return Failure(issues);
        }
        if (source.State != ResourceMutationSourceState.Active)
        {
            Add(
                issues,
                "resource_source_inactive",
                "active source authority",
                source.State.ToString());
            return Failure(issues);
        }
        if (source.BoundOwner != null &&
            (target == null ||
             !string.Equals(source.BoundOwner.Realm, target.Realm, StringComparison.Ordinal) ||
             source.BoundOwner.OwnerKind != target.OwnerKind ||
             !string.Equals(
                 source.BoundOwner.ResourceOwnerId,
                 target.ResourceOwnerId,
                 StringComparison.Ordinal)))
        {
            Add(
                issues,
                "resource_source_target_mismatch",
                $"{source.BoundOwner.Realm}/{source.BoundOwner.OwnerKind}/{source.BoundOwner.ResourceOwnerId}",
                target == null
                    ? "missing target"
                    : $"{target.Realm}/{target.OwnerKind}/{target.ResourceOwnerId}");
            return Failure(issues);
        }
        if (!routeDefinition.AllowedOperations.Contains(request.Operation))
        {
            Add(
                issues,
                "resource_source_operation_forbidden",
                "operation allowed by the registered source route",
                request.Operation.ToString());
            return Failure(issues);
        }
        if (!definition.AllowedOperations.Contains(request.Operation))
        {
            Add(
                issues,
                "resource_source_definition_operation_forbidden",
                "operation allowed by the sealed resource definition",
                request.Operation.ToString());
            return Failure(issues);
        }

        var floor = routeDefinition.RejectBounds
            ? ResourceBoundPolicy.RejectBelowMinimum
            : definition.FloorPolicy;
        var cap = routeDefinition.RejectBounds
            ? ResourceBoundPolicy.RejectAboveMaximum
            : definition.CapPolicy;
        var evidence = new ResourceSourceEvidence(
            source.SourceKind,
            source.SourceId,
            source.AuthorityFingerprint);
        var policy = new ResourcePolicyBinding(
            floor,
            cap,
            CreatePolicyFingerprint(
                source,
                routeDefinition,
                request.Operation,
                definition,
                floor,
                cap));
        return new ResourceMutationSourceResolution(
            new ResourceAuthorizedSourceRoute(
                routeDefinition.Phase,
                routeDefinition.Priority,
                policy,
                evidence,
                source.SameTurn),
            Array.Empty<ValidationIssue>());
    }

    private static string CreatePolicyFingerprint(
        ResourceMutationSourceExport source,
        SourceRouteDefinition route,
        ResourceOperation operation,
        ResourceDefinition definition,
        ResourceBoundPolicy floor,
        ResourceBoundPolicy cap)
    {
        using var builder = new ResourceFingerprintBuilder("resource-source-policy-v1");
        builder.Append(source.SourceKind);
        builder.Append(source.SourceId);
        builder.Append(source.AuthorityFingerprint);
        builder.Append(source.BoundOwner?.Realm ?? "<unbound>");
        builder.Append(source.BoundOwner == null ? -1 : (int)source.BoundOwner.OwnerKind);
        builder.Append(source.BoundOwner?.ResourceOwnerId ?? "<unbound>");
        builder.Append((int)route.Phase);
        builder.Append(route.Priority);
        builder.Append((int)operation);
        builder.Append(definition.ResourceKey);
        builder.Append(definition.DefinitionVersion);
        builder.Append(definition.Materialization.DefinitionId);
        builder.Append(definition.Materialization.Seal);
        builder.Append((int)floor);
        builder.Append((int)cap);
        return builder.Build();
    }

    private static ResourceMutationSourceResolution Failure(
        IEnumerable<ValidationIssue> issues) =>
        new(null, issues.ToArray());

    private static string CreateFingerprint(
        IEnumerable<ResourceMutationSourceExport> sources)
    {
        using var builder = new ResourceFingerprintBuilder("resource-source-catalog-v2");
        foreach (var source in sources
                     .OrderBy(static value => value.SourceKind, StringComparer.Ordinal)
                     .ThenBy(static value => value.SourceId, StringComparer.Ordinal))
        {
            builder.Append(source.SourceKind);
            builder.Append(source.SourceId);
            builder.Append(source.AuthorityFingerprint);
            builder.Append((int)source.State);
            builder.Append(source.SameTurn);
            builder.Append(source.BoundOwner?.Realm ?? "<unbound>");
            builder.Append(source.BoundOwner == null ? -1 : (int)source.BoundOwner.OwnerKind);
            builder.Append(source.BoundOwner?.ResourceOwnerId ?? "<unbound>");
        }
        return builder.Build();
    }

    private static SourceRouteDefinition Cost(
        ResourceMutationPhase phase,
        int priority,
        params ResourceOperation[] operations) =>
        new(
            phase,
            priority,
            operations.ToFrozenSet(),
            RejectBounds: true,
            BoundOwnerKind: null,
            RequiresBoundOwner: false);

    private static SourceRouteDefinition Outcome(
        ResourceMutationPhase phase,
        int priority,
        params ResourceOperation[] operations) =>
        new(
            phase,
            priority,
            operations.ToFrozenSet(),
            RejectBounds: false,
            BoundOwnerKind: null,
            RequiresBoundOwner: false);

    private static SourceRouteDefinition BoundCost(
        ResourceMutationPhase phase,
        int priority,
        params ResourceOperation[] operations) =>
        new(
            phase,
            priority,
            operations.ToFrozenSet(),
            RejectBounds: true,
            BoundOwnerKind: null,
            RequiresBoundOwner: true);

    private static SourceRouteDefinition BoundOutcome(
        ResourceMutationPhase phase,
        int priority,
        params ResourceOperation[] operations) =>
        new(
            phase,
            priority,
            operations.ToFrozenSet(),
            RejectBounds: false,
            BoundOwnerKind: null,
            RequiresBoundOwner: true);

    private static SourceRouteDefinition ItemCost(
        ResourceMutationPhase phase,
        int priority,
        params ResourceOperation[] operations) =>
        new(
            phase,
            priority,
            operations.ToFrozenSet(),
            RejectBounds: true,
            BoundOwnerKind: ResourceOwnerKind.Item,
            RequiresBoundOwner: true);

    private static SourceRouteDefinition ItemOutcome(
        ResourceMutationPhase phase,
        int priority,
        params ResourceOperation[] operations) =>
        new(
            phase,
            priority,
            operations.ToFrozenSet(),
            RejectBounds: false,
            BoundOwnerKind: ResourceOwnerKind.Item,
            RequiresBoundOwner: true);

    private static void Add(
        List<ValidationIssue> issues,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            ResourceMaterializationContract.StatePath,
            code,
            expected,
            actual);

    private sealed record SourceRouteDefinition(
        ResourceMutationPhase Phase,
        int Priority,
        IReadOnlySet<ResourceOperation> AllowedOperations,
        bool RejectBounds,
        ResourceOwnerKind? BoundOwnerKind,
        bool RequiresBoundOwner);

    private sealed record SourceKey(string SourceKind, string SourceId);

    private sealed class SourceKeyComparer : IEqualityComparer<SourceKey>
    {
        internal static SourceKeyComparer Instance { get; } = new();

        public bool Equals(SourceKey? left, SourceKey? right) =>
            left != null && right != null &&
            string.Equals(left.SourceKind, right.SourceKind, StringComparison.Ordinal) &&
            string.Equals(left.SourceId, right.SourceId, StringComparison.Ordinal);

        public int GetHashCode(SourceKey value) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.SourceKind),
                StringComparer.Ordinal.GetHashCode(value.SourceId));
    }
}
