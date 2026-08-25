using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeSpiritualConflictResourceOutcome
{
    private const string ResourceKey = "spiritual_action_points";
    private const string ConflictPath = AfterlifeSpiritualConflictState.StatePath;

    internal sealed record BuildResult(
        IResourceRegisteredSystemOutcomeDraft? Draft,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;
    }

    private sealed record ExpectedTransition(
        string EventRef,
        string SourceKind,
        string SourceId,
        ResourceCoordinate Coordinate,
        ResourceTransitionOperation Operation,
        decimal Before,
        decimal After,
        decimal Amount);

    private sealed class Draft : IResourceRegisteredSystemOutcomeDraft
    {
        private readonly ResourceMutationSourceExport[] _sources;
        private readonly ResourceMutationIntent[] _mutations;
        private readonly ExpectedTransition[] _expectedTransitions;

        internal Draft(
            string fingerprint,
            IReadOnlyList<ResourceMutationSourceExport> sources,
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyList<ExpectedTransition> expectedTransitions)
        {
            Fingerprint = fingerprint;
            _sources = sources.ToArray();
            _mutations = mutations.ToArray();
            _expectedTransitions = expectedTransitions.ToArray();
        }

        public string Fingerprint { get; }

        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.AsReadOnly(_sources.ToArray());

        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.ToArray());

        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new ReadOnlyDictionary<string, CanonicalBeforeImage>(
                new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal));

        public ResourceRegisteredSystemOutcomeProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resourceResult)
        {
            ArgumentNullException.ThrowIfNull(resourceResult);
            var issues = new List<ValidationIssue>();
            var transitions = resourceResult.AppliedTransitions
                .Concat(resourceResult.ReplayTransitions)
                .ToArray();
            foreach (var expected in _expectedTransitions)
            {
                var matches = transitions.Where(transition =>
                    string.Equals(transition.EventRef, expected.EventRef, StringComparison.Ordinal) &&
                    string.Equals(transition.OriginKind, expected.SourceKind, StringComparison.Ordinal) &&
                    string.Equals(transition.OriginId, expected.SourceId, StringComparison.Ordinal) &&
                    ResourceCoordinateComparer.Instance.Equals(
                        transition.Coordinate,
                        expected.Coordinate) &&
                    transition.Operation == expected.Operation).ToArray();
                if (matches.Length != 1 ||
                    matches[0].BeforeState?.Current != expected.Before ||
                    matches[0].AfterState?.Current != expected.After ||
                    matches[0].RequestedAmount != expected.Amount ||
                    matches[0].AppliedAmount != expected.Amount)
                {
                    Add(
                        issues,
                        ConflictPath + ".activeConflict.exchangeLog",
                        "afterlife_conflict_resource_transition_mismatch",
                        $"one exact {expected.Operation} transition {expected.Before}->{expected.After} amount {expected.Amount}",
                        matches.Length == 1
                            ? $"{matches[0].Operation} {matches[0].BeforeState?.Current}->{matches[0].AfterState?.Current} requested={matches[0].RequestedAmount} applied={matches[0].AppliedAmount}"
                            : $"matches={matches.Length}");
                }
            }

            return new ResourceRegisteredSystemOutcomeProjectionResult(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                Array.Empty<AcceptedMechanicsOwnerTransition>(),
                issues);
        }
    }

    internal static BuildResult TryCreate(
        int turn,
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        ResourceOwnerAuthority owners,
        ResourceStateLedger state)
    {
        ArgumentNullException.ThrowIfNull(preTurnRoot);
        ArgumentNullException.ThrowIfNull(acceptedRoot);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(state);
        var issues = new List<ValidationIssue>();
        if (turn <= 0)
        {
            Add(
                issues,
                ConflictPath,
                "afterlife_conflict_resource_turn_invalid",
                "positive accepted turn",
                turn.ToString());
            return new BuildResult(null, issues);
        }

        if (preTurnRoot["activeConflict"] is not JsonObject preTurnConflict ||
            acceptedRoot["activeConflict"] is not JsonObject acceptedConflict)
        {
            return new BuildResult(null, Array.Empty<ValidationIssue>());
        }

        var conflictId = ReadExact(acceptedConflict["conflictId"]);
        var preTurnConflictId = ReadExact(preTurnConflict["conflictId"]);
        var realm = ReadRealm(acceptedConflict["realm"]);
        if (conflictId == null || preTurnConflictId == null || realm == null ||
            !string.Equals(conflictId, preTurnConflictId, StringComparison.Ordinal))
        {
            Add(
                issues,
                ConflictPath + ".activeConflict",
                "afterlife_conflict_resource_identity_mismatch",
                "one exact continuing conflict identity and realm",
                $"before={preTurnConflictId ?? "missing"};after={conflictId ?? "missing"};realm={realm ?? "missing"}");
            return new BuildResult(null, issues);
        }

        var beforeLog = preTurnConflict["exchangeLog"] as JsonArray ?? new JsonArray();
        var afterLog = acceptedConflict["exchangeLog"] as JsonArray ?? new JsonArray();
        if (afterLog.Count < beforeLog.Count)
        {
            Add(
                issues,
                ConflictPath + ".activeConflict.exchangeLog",
                "afterlife_conflict_resource_log_truncated",
                "accepted exchange log preserving the validated pre-turn prefix",
                $"before={beforeLog.Count};after={afterLog.Count}");
            return new BuildResult(null, issues);
        }
        for (var index = 0; index < beforeLog.Count; index++)
        {
            if (JsonNode.DeepEquals(beforeLog[index], afterLog[index]))
                continue;
            Add(
                issues,
                $"{ConflictPath}.activeConflict.exchangeLog[{index}]",
                "afterlife_conflict_resource_log_prefix_mutated",
                "byte-semantic validated pre-turn exchange entry",
                afterLog[index]?.ToJsonString() ?? "null");
        }
        if (issues.Count != 0)
            return new BuildResult(null, issues);

        if (!TryResolveCoordinate(
                owners,
                state,
                new ResourceOwnerKey(realm, ResourceOwnerKind.AfterlifeActor, "player_soul"),
                out var playerCoordinate,
                out var playerCurrent,
                out var playerMaximum,
                issues))
        {
            return new BuildResult(null, issues);
        }
        var oppositionOwnerId = ReadExact(
            acceptedConflict[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]?
                ["opposition"]?["resourceOwnerId"]);
        if (oppositionOwnerId == null ||
            !TryResolveCoordinate(
                owners,
                state,
                new ResourceOwnerKey(
                    realm,
                    ResourceOwnerKind.AfterlifeConflictSide,
                    oppositionOwnerId ?? string.Empty),
                out var oppositionCoordinate,
                out var oppositionCurrent,
                out var oppositionMaximum,
                issues))
        {
            if (oppositionOwnerId == null)
            {
                Add(
                    issues,
                    ConflictPath + ".activeConflict.resourceOwnerBindings.opposition",
                    "afterlife_conflict_resource_opposition_owner_missing",
                    "exact client-owned opposition resource owner binding",
                    "missing");
            }
            return new BuildResult(null, issues);
        }

        var sourceExports = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var expected = new List<ExpectedTransition>();
        var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
        var exchangeIds = new HashSet<string>(StringComparer.Ordinal);
        var exchangeAliases = new HashSet<string>(StringComparer.Ordinal);
        ResourceOperationKey? priorPlayer = null;
        ResourceOperationKey? priorOpposition = null;

        for (var index = beforeLog.Count; index < afterLog.Count; index++)
        {
            var path = $"{ConflictPath}.activeConflict.exchangeLog[{index}]";
            if (afterLog[index] is not JsonObject exchange ||
                ReadExact(exchange["exchangeId"]) is not { } exchangeId)
            {
                Add(
                    issues,
                    path + ".exchangeId",
                    "afterlife_conflict_resource_exchange_identity_invalid",
                    "exact exchange identity",
                    afterLog[index]?.ToJsonString() ?? "null");
                continue;
            }
            if (!exchangeIds.Add(exchangeId) ||
                !exchangeAliases.Add(ResourceMaterializationContract.BuildConfusableKey(exchangeId)))
            {
                Add(
                    issues,
                    path + ".exchangeId",
                    "afterlife_conflict_resource_exchange_identity_ambiguous",
                    "exact/confusable-unique accepted exchange identity",
                    exchangeId);
                continue;
            }
            if (exchange["actionCostAudit"] is not JsonObject audit)
                continue;

            using var sourceFingerprintBuilder = new ResourceFingerprintBuilder(
                "afterlife-conflict-resource-outcome-v1");
            sourceFingerprintBuilder.Append(turn);
            sourceFingerprintBuilder.Append(conflictId);
            sourceFingerprintBuilder.Append(realm);
            sourceFingerprintBuilder.Append(exchangeId);
            sourceFingerprintBuilder.Append(exchange.ToJsonString());
            sourceFingerprintBuilder.Append(owners.Fingerprint);
            var sourceFingerprint = sourceFingerprintBuilder.Build();

            ComposeSide(
                "player",
                audit["player"],
                exchangeId,
                path,
                playerCoordinate!,
                playerMaximum,
                ref playerCurrent,
                ref priorPlayer,
                sourceFingerprint,
                sourceKeys,
                sourceExports,
                mutations,
                expected,
                issues,
                turn);
            ComposeSide(
                "opposition",
                audit["opposition"],
                exchangeId,
                path,
                oppositionCoordinate!,
                oppositionMaximum,
                ref oppositionCurrent,
                ref priorOpposition,
                sourceFingerprint,
                sourceKeys,
                sourceExports,
                mutations,
                expected,
                issues,
                turn);
        }

        if (issues.Count != 0 || mutations.Count == 0)
        {
            return new BuildResult(
                null,
                issues.Count == 0 ? Array.Empty<ValidationIssue>() : issues);
        }

        using var draftFingerprint = new ResourceFingerprintBuilder(
            "afterlife-conflict-resource-draft-v1");
        draftFingerprint.Append(turn);
        draftFingerprint.Append(conflictId);
        draftFingerprint.Append(realm);
        draftFingerprint.Append(state.Fingerprint);
        foreach (var source in sourceExports
                     .OrderBy(static value => value.SourceKind, StringComparer.Ordinal)
                     .ThenBy(static value => value.SourceId, StringComparer.Ordinal))
        {
            draftFingerprint.Append(source.SourceKind);
            draftFingerprint.Append(source.SourceId);
            draftFingerprint.Append(source.AuthorityFingerprint);
        }
        return new BuildResult(
            new Draft(
                draftFingerprint.Build(),
                sourceExports,
                mutations,
                expected),
            Array.Empty<ValidationIssue>());
    }

    private static void ComposeSide(
        string side,
        JsonNode? auditNode,
        string exchangeId,
        string exchangePath,
        ResourceCoordinate coordinate,
        decimal maximum,
        ref decimal current,
        ref ResourceOperationKey? prior,
        string sourceFingerprint,
        HashSet<string> sourceKeys,
        List<ResourceMutationSourceExport> sourceExports,
        List<ResourceMutationIntent> mutations,
        List<ExpectedTransition> expected,
        List<ValidationIssue> issues,
        int turn)
    {
        if (auditNode == null)
            return;
        var path = exchangePath + ".actionCostAudit." + side;
        if (auditNode is not JsonObject audit ||
            ReadExact(audit["operationType"]) is not { } operationType ||
            !TryReadDecimal(audit["before"], out var before) ||
            !TryReadDecimal(audit["after"], out var after) ||
            !TryReadDecimal(audit["effectiveCost"], out var effectiveCost))
        {
            Add(
                issues,
                path,
                "afterlife_conflict_resource_audit_invalid",
                "exact operationType, effectiveCost, before, and after",
                auditNode.ToJsonString());
            return;
        }
        if (before != current || after < 0m || after > maximum)
        {
            Add(
                issues,
                path,
                "afterlife_conflict_resource_audit_state_mismatch",
                $"ledger sequence before={current} and after within 0..{maximum}",
                $"before={before};after={after}");
            return;
        }

        var recovery = string.Equals(
            operationType,
            "recover_spiritual_power",
            StringComparison.OrdinalIgnoreCase);
        var operation = recovery ? ResourceOperation.Gain : ResourceOperation.Spend;
        var transitionOperation = recovery
            ? ResourceTransitionOperation.Gain
            : ResourceTransitionOperation.Spend;
        var amount = recovery ? after - before : effectiveCost;
        var expectedAfter = recovery ? before + amount : before - amount;
        if (effectiveCost < 0m || amount < 0m || after != expectedAfter)
        {
            Add(
                issues,
                path,
                "afterlife_conflict_resource_audit_delta_mismatch",
                recovery
                    ? "non-negative recovery with after = before + recovered amount"
                    : "positive action cost with after = before - effectiveCost",
                $"effectiveCost={effectiveCost};before={before};after={after}");
            return;
        }
        current = after;
        if (amount == 0m)
            return;

        var sourceKind = recovery ? "afterlife_outcome" : "afterlife_cost";
        var sourceKey = sourceKind + "\0" + exchangeId;
        if (sourceKeys.Add(sourceKey))
        {
            sourceExports.Add(new ResourceMutationSourceExport(
                sourceKind,
                exchangeId,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true));
        }
        var eventRef = $"turn_{turn}:afterlife_conflict:{exchangeId}:{side}";
        var intent = new ResourceMutationIntent(
            eventRef,
            coordinate,
            amount,
            new ResourceMutationSourceRequest(sourceKind, exchangeId, operation),
            prior == null
                ? Array.Empty<ResourceOperationKey>()
                : new[] { prior },
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        mutations.Add(intent);
        expected.Add(new ExpectedTransition(
            eventRef,
            sourceKind,
            exchangeId,
            coordinate,
            transitionOperation,
            before,
            after,
            amount));
        prior = intent.Key;
    }

    private static bool TryResolveCoordinate(
        ResourceOwnerAuthority owners,
        ResourceStateLedger state,
        ResourceOwnerKey key,
        out ResourceCoordinate? coordinate,
        out decimal current,
        out decimal maximum,
        List<ValidationIssue> issues)
    {
        coordinate = null;
        current = 0m;
        maximum = 0m;
        if (!owners.Entries.TryGetValue(key, out var owner) ||
            owner.Lifecycle != ResourceOwnerLifecycle.Active ||
            !owner.ResourceCapabilities.Contains(ResourceKey))
        {
            Add(
                issues,
                ConflictPath + ".activeConflict",
                "afterlife_conflict_resource_owner_unresolved",
                $"active {key.OwnerKind} owner {key.ResourceOwnerId} with {ResourceKey}",
                "missing or unauthorized");
            return false;
        }
        coordinate = new ResourceCoordinate(
            key.Realm,
            key.OwnerKind,
            key.ResourceOwnerId,
            ResourceKey);
        if (!state.TryResolveExact(coordinate, out var entry) ||
            entry == null ||
            entry.State != ResourceLifecycleState.Active)
        {
            Add(
                issues,
                ResourceMaterializationContract.StatePath,
                "afterlife_conflict_resource_state_unresolved",
                $"active canonical resource coordinate {key.Realm}/{key.OwnerKind}/{key.ResourceOwnerId}/{ResourceKey}",
                "missing or inactive");
            return false;
        }
        current = entry.Current;
        maximum = entry.Maximum;
        return true;
    }

    private static string? ReadExact(JsonNode? node)
    {
        if (node is not JsonValue value ||
            !value.TryGetValue<string>(out var text) ||
            string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        var exact = text.Trim();
        return ResourceMaterializationContract.IsExactIdentifier(exact) ? exact : null;
    }

    private static string? ReadRealm(JsonNode? node)
    {
        var raw = node is JsonValue value && value.TryGetValue<string>(out var text)
            ? text
            : null;
        return AfterlifeEntityProfileState.TryNormalizeEffectRealm(raw, out var realm)
            ? realm
            : null;
    }

    private static bool TryReadDecimal(JsonNode? node, out decimal result)
    {
        result = 0m;
        return node is JsonValue value &&
               (value.TryGetValue<decimal>(out result) ||
                value.TryGetValue<int>(out var integer) && (result = integer) == integer);
    }

    private static void Add(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        ResourceMaterializationContract.AddIssue(
            issues,
            path,
            code,
            expected,
            actual);
}
