using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeShiningGachaResourceOutcome
{
    private const string ResourceKey = "gacha_attempts";
    private const string ShiningPath = ShiningAbodeState.StatePath;

    internal sealed record BuildResult(
        IResourceRegisteredSystemOutcomeDraft? Draft,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;
    }

    private sealed record ExpectedTransition(
        string EventRef,
        string SourceId,
        ResourceCoordinate Coordinate,
        decimal Before,
        decimal After);

    private sealed class Draft : IResourceRegisteredSystemOutcomeDraft
    {
        private readonly ResourceMutationSourceExport[] _sources;
        private readonly ResourceMutationIntent[] _mutations;
        private readonly ExpectedTransition[] _expectedTransitions;
        private readonly Dictionary<string, CanonicalBeforeImage> _beforeImages;

        internal Draft(
            string fingerprint,
            IReadOnlyList<ResourceMutationSourceExport> sources,
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyList<ExpectedTransition> expectedTransitions,
            CanonicalBeforeImage shiningBeforeImage)
        {
            Fingerprint = fingerprint;
            _sources = sources.ToArray();
            _mutations = mutations.ToArray();
            _expectedTransitions = expectedTransitions.ToArray();
            _beforeImages = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
            {
                [ShiningPath] = new CanonicalBeforeImage(
                    shiningBeforeImage.Existed,
                    shiningBeforeImage.Bytes)
            };
        }

        public string Fingerprint { get; }

        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.AsReadOnly(_sources.ToArray());

        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.ToArray());

        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new ReadOnlyDictionary<string, CanonicalBeforeImage>(
                _beforeImages.ToDictionary(
                    static pair => pair.Key,
                    static pair => new CanonicalBeforeImage(
                        pair.Value.Existed,
                        pair.Value.Bytes),
                    StringComparer.Ordinal));

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
                    string.Equals(transition.OriginKind, "afterlife_cost", StringComparison.Ordinal) &&
                    string.Equals(transition.OriginId, expected.SourceId, StringComparison.Ordinal) &&
                    ResourceCoordinateComparer.Instance.Equals(
                        transition.Coordinate,
                        expected.Coordinate) &&
                    transition.Operation == ResourceTransitionOperation.Spend).ToArray();
                if (matches.Length == 1 &&
                    matches[0].BeforeState?.Current == expected.Before &&
                    matches[0].AfterState?.Current == expected.After &&
                    matches[0].RequestedAmount == 1m &&
                    matches[0].AppliedAmount == 1m)
                {
                    continue;
                }

                Add(
                    issues,
                    ShiningPath + ".gachaSystem.gachaHistory",
                    "afterlife_shining_gacha_resource_transition_mismatch",
                    $"one exact spend transition {expected.Before}->{expected.After} amount 1",
                    matches.Length == 1
                        ? $"{matches[0].Operation} {matches[0].BeforeState?.Current}->{matches[0].AfterState?.Current} requested={matches[0].RequestedAmount} applied={matches[0].AppliedAmount}"
                        : $"matches={matches.Length}");
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
        ResourceStateLedger state,
        IReadOnlyList<ResourceOwnerCapacityDraft> capacityDrafts,
        CanonicalBeforeImage shiningBeforeImage)
    {
        ArgumentNullException.ThrowIfNull(preTurnRoot);
        ArgumentNullException.ThrowIfNull(acceptedRoot);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(capacityDrafts);
        ArgumentNullException.ThrowIfNull(shiningBeforeImage);
        var issues = new List<ValidationIssue>();
        if (turn <= 0)
        {
            Add(
                issues,
                ShiningPath,
                "afterlife_shining_gacha_resource_turn_invalid",
                "positive accepted turn",
                turn.ToString());
            return new BuildResult(null, issues);
        }

        if (preTurnRoot["gachaSystem"] is not JsonObject preTurnGacha ||
            acceptedRoot["gachaSystem"] is not JsonObject acceptedGacha)
        {
            return new BuildResult(null, Array.Empty<ValidationIssue>());
        }

        var beforeHistory = preTurnGacha["gachaHistory"] as JsonArray ?? new JsonArray();
        if (acceptedGacha["gachaHistory"] is not JsonArray afterHistory)
        {
            Add(
                issues,
                ShiningPath + ".gachaSystem.gachaHistory",
                "afterlife_shining_gacha_resource_history_invalid",
                "canonical gacha history array",
                acceptedGacha["gachaHistory"]?.ToJsonString() ?? "missing/null");
            return new BuildResult(null, issues);
        }
        if (afterHistory.Count < beforeHistory.Count)
        {
            Add(
                issues,
                ShiningPath + ".gachaSystem.gachaHistory",
                "afterlife_shining_gacha_resource_history_truncated",
                "accepted history preserving the validated pre-turn prefix",
                $"before={beforeHistory.Count};after={afterHistory.Count}");
            return new BuildResult(null, issues);
        }
        for (var index = 0; index < beforeHistory.Count; index++)
        {
            if (JsonNode.DeepEquals(beforeHistory[index], afterHistory[index]))
                continue;
            Add(
                issues,
                $"{ShiningPath}.gachaSystem.gachaHistory[{index}]",
                "afterlife_shining_gacha_resource_history_prefix_mutated",
                "byte-semantic validated pre-turn history entry",
                afterHistory[index]?.ToJsonString() ?? "null");
        }
        if (issues.Count != 0 || afterHistory.Count == beforeHistory.Count)
            return new BuildResult(null, issues);

        var returnCycleId = ReadExact(acceptedGacha["currentReturnCycleId"]);
        var binding = acceptedRoot[AfterlifeEntityProfileState.ResourceOwnerBindingsProperty]?
            ["gachaReturn"] as JsonObject;
        var boundCycleId = ReadExact(binding?["returnCycleId"]);
        var resourceOwnerId = ReadExact(binding?["resourceOwnerId"]);
        if (returnCycleId == null ||
            boundCycleId == null ||
            resourceOwnerId == null ||
            !string.Equals(returnCycleId, boundCycleId, StringComparison.Ordinal))
        {
            Add(
                issues,
                ShiningPath + ".resourceOwnerBindings.gachaReturn",
                "afterlife_shining_gacha_resource_scope_binding_invalid",
                "exact client-owned scope binding for the current return cycle",
                binding?.ToJsonString() ?? "missing");
            return new BuildResult(null, issues);
        }

        var ownerKey = new ResourceOwnerKey(
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            resourceOwnerId);
        if (!owners.Entries.TryGetValue(ownerKey, out var owner) ||
            owner.Lifecycle != ResourceOwnerLifecycle.Active ||
            !owner.ResourceCapabilities.Contains(ResourceKey))
        {
            Add(
                issues,
                ShiningPath + ".resourceOwnerBindings.gachaReturn.resourceOwnerId",
                "afterlife_shining_gacha_resource_owner_unresolved",
                $"active exact AfterlifeScope owner with {ResourceKey}",
                resourceOwnerId);
            return new BuildResult(null, issues);
        }

        var coordinate = new ResourceCoordinate(
            ownerKey.Realm,
            ownerKey.OwnerKind,
            ownerKey.ResourceOwnerId,
            ResourceKey);
        if (!TryResolveStartingState(
                coordinate,
                owner,
                state,
                capacityDrafts,
                out var current,
                out var maximum,
                issues))
        {
            return new BuildResult(null, issues);
        }

        var requestIds = new HashSet<string>(StringComparer.Ordinal);
        var requestAliases = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < afterHistory.Count; index++)
        {
            var requestId = afterHistory[index] is JsonObject entry
                ? ReadExact(entry["requestId"])
                : null;
            if (requestId == null ||
                !requestIds.Add(requestId) ||
                !requestAliases.Add(
                    ResourceMaterializationContract.BuildConfusableKey(requestId)))
            {
                Add(
                    issues,
                    $"{ShiningPath}.gachaSystem.gachaHistory[{index}].requestId",
                    "afterlife_shining_gacha_resource_request_identity_invalid",
                    "exact/confusable-unique gacha request identity",
                    requestId ?? afterHistory[index]?.ToJsonString() ?? "null");
            }
        }
        if (issues.Count != 0)
            return new BuildResult(null, issues);

        var sources = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var expected = new List<ExpectedTransition>();
        ResourceOperationKey? prior = null;
        for (var index = beforeHistory.Count; index < afterHistory.Count; index++)
        {
            var path = $"{ShiningPath}.gachaSystem.gachaHistory[{index}]";
            var entry = (JsonObject)afterHistory[index]!;
            var requestId = ReadExact(entry["requestId"])!;
            var entryCycleId = ReadExact(entry["returnCycleId"]);
            var relicId = ReadExact(entry["relicId"]);
            if (!TryReadPositiveInt(entry["turnNumber"], out var entryTurn) ||
                entryTurn != turn ||
                entryCycleId == null ||
                !string.Equals(entryCycleId, returnCycleId, StringComparison.Ordinal) ||
                relicId == null)
            {
                Add(
                    issues,
                    path,
                    "afterlife_shining_gacha_resource_entry_invalid",
                    $"exact request/relic, returnCycleId={returnCycleId}, and turnNumber={turn}",
                    entry.ToJsonString());
                continue;
            }
            if (current < 1m)
            {
                Add(
                    issues,
                    path,
                    "afterlife_shining_gacha_resource_exhausted",
                    "one remaining canonical gacha_attempts unit",
                    $"current={current};maximum={maximum}");
                continue;
            }

            using var sourceFingerprintBuilder = new ResourceFingerprintBuilder(
                "afterlife-shining-gacha-cost-v1");
            sourceFingerprintBuilder.Append(turn);
            sourceFingerprintBuilder.Append(returnCycleId);
            sourceFingerprintBuilder.Append(resourceOwnerId);
            sourceFingerprintBuilder.Append(requestId);
            sourceFingerprintBuilder.Append(relicId);
            sourceFingerprintBuilder.Append(entry.ToJsonString());
            sourceFingerprintBuilder.Append(owner.AuthorityFingerprint);
            var sourceFingerprint = sourceFingerprintBuilder.Build();
            sources.Add(new ResourceMutationSourceExport(
                "afterlife_cost",
                requestId,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true));
            var eventRef = $"turn_{turn}:shining_gacha:{requestId}";
            var mutation = new ResourceMutationIntent(
                eventRef,
                coordinate,
                1m,
                new ResourceMutationSourceRequest(
                    "afterlife_cost",
                    requestId,
                    ResourceOperation.Spend),
                prior == null
                    ? Array.Empty<ResourceOperationKey>()
                    : new[] { prior },
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null);
            mutations.Add(mutation);
            expected.Add(new ExpectedTransition(
                eventRef,
                requestId,
                coordinate,
                current,
                current - 1m));
            current -= 1m;
            prior = mutation.Key;
        }
        if (issues.Count != 0)
            return new BuildResult(null, issues);

        using var draftFingerprint = new ResourceFingerprintBuilder(
            "afterlife-shining-gacha-draft-v1");
        draftFingerprint.Append(turn);
        draftFingerprint.Append(returnCycleId);
        draftFingerprint.Append(resourceOwnerId);
        draftFingerprint.Append(state.Fingerprint);
        draftFingerprint.Append(owner.AuthorityFingerprint);
        draftFingerprint.Append(shiningBeforeImage.Fingerprint);
        foreach (var source in sources)
        {
            draftFingerprint.Append(source.SourceId);
            draftFingerprint.Append(source.AuthorityFingerprint);
        }
        return new BuildResult(
            new Draft(
                draftFingerprint.Build(),
                sources,
                mutations,
                expected,
                shiningBeforeImage),
            Array.Empty<ValidationIssue>());
    }

    private static bool TryResolveStartingState(
        ResourceCoordinate coordinate,
        ResourceOwnerAuthorityEntry owner,
        ResourceStateLedger state,
        IReadOnlyList<ResourceOwnerCapacityDraft> capacityDrafts,
        out decimal current,
        out decimal maximum,
        List<ValidationIssue> issues)
    {
        current = 0m;
        maximum = 0m;
        if (state.TryResolveExact(coordinate, out var entry) && entry != null)
        {
            if (entry.State != ResourceLifecycleState.Active)
            {
                Add(
                    issues,
                    ResourceMaterializationContract.StatePath,
                    "afterlife_shining_gacha_resource_state_inactive",
                    "active canonical gacha_attempts coordinate",
                    entry.State.ToString());
                return false;
            }
            current = entry.Current;
            maximum = entry.Maximum;
            return true;
        }

        var matchingDrafts = capacityDrafts
            .Where(draft => ResourceCoordinateComparer.Instance.Equals(
                draft.Coordinate,
                coordinate))
            .ToArray();
        if (!owner.SameTurn ||
            matchingDrafts.Length != 1 ||
            matchingDrafts[0].ResolvedCapacity.Capacity?.Initialization == null)
        {
            Add(
                issues,
                ResourceMaterializationContract.StatePath,
                "afterlife_shining_gacha_resource_state_unresolved",
                "one active canonical coordinate or exact same-turn initialized scope capacity",
                $"sameTurn={owner.SameTurn};capacityDrafts={matchingDrafts.Length}");
            return false;
        }

        var capacity = matchingDrafts[0].ResolvedCapacity.Capacity!;
        current = capacity.Initialization!.Current;
        maximum = capacity.Maximum;
        return true;
    }

    private static string? ReadExact(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static bool TryReadPositiveInt(JsonNode? node, out int result)
    {
        result = 0;
        return node is JsonValue value &&
               value.TryGetValue<int>(out result) &&
               result > 0;
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
