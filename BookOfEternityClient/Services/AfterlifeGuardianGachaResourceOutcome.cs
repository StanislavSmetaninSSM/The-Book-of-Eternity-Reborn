using System.Collections.ObjectModel;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal static class AfterlifeGuardianGachaResourceOutcome
{
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string ResourceKey = "gacha_attempts";

    internal sealed record BuildResult(
        IResourceRegisteredSystemOutcomeDraft? Draft,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid => Issues.Count == 0;
    }

    private sealed record ExpectedGain(
        string EventRef,
        string SourceId,
        ResourceCoordinate Coordinate,
        decimal Before,
        decimal After,
        decimal Amount);

    private sealed record ExpectedSpend(
        int CommandOrdinal,
        string EventRef,
        string SourceId,
        string GuardianId,
        string ReturnCycleId,
        ResourceCoordinate Coordinate,
        decimal Before,
        decimal After,
        int InkFeathersSpent,
        string RelicId,
        string FinalRarity,
        JsonNode? GachaBonusAudit,
        JsonArray ExpectedHistoryPrefix);

    private sealed class Draft : IOriginalSpiritualPrefixOutcomeDraft
    {
        private readonly ResourceMutationSourceExport[] _sources;
        private readonly ResourceMutationIntent[] _mutations;
        private readonly ExpectedGain[] _expectedGains;
        private readonly ExpectedSpend[] _expectedSpends;
        private readonly CanonicalBeforeImage _beforeImage;
        private readonly string _requestTimestamp;

        internal Draft(
            string fingerprint,
            IReadOnlyList<ResourceMutationSourceExport> sources,
            IReadOnlyList<ResourceMutationIntent> mutations,
            IReadOnlyList<ExpectedGain> expectedGains,
            IReadOnlyList<ExpectedSpend> expectedSpends,
            string requestTimestamp,
            CanonicalBeforeImage beforeImage)
        {
            Fingerprint = fingerprint;
            _sources = sources.ToArray();
            _mutations = mutations.ToArray();
            _expectedGains = expectedGains.ToArray();
            _expectedSpends = expectedSpends.Select(static expected => expected with
            {
                GachaBonusAudit = expected.GachaBonusAudit?.DeepClone(),
                ExpectedHistoryPrefix = expected.ExpectedHistoryPrefix.DeepClone().AsArray()
            }).ToArray();
            _requestTimestamp = requestTimestamp;
            _beforeImage = new CanonicalBeforeImage(beforeImage.Existed, beforeImage.Bytes);
        }

        public string Fingerprint { get; }

        public IReadOnlyList<ResourceMutationSourceExport> SourceExports =>
            Array.AsReadOnly(_sources.ToArray());

        public IReadOnlyList<ResourceMutationIntent> Mutations =>
            Array.AsReadOnly(_mutations.ToArray());

        public IReadOnlyDictionary<string, CanonicalBeforeImage> ExpectedBeforeImages =>
            new ReadOnlyDictionary<string, CanonicalBeforeImage>(
                new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal)
                {
                    [GuardiansPath] = new CanonicalBeforeImage(
                        _beforeImage.Existed,
                        _beforeImage.Bytes)
                });

        public ResourceRegisteredSystemOutcomeProjectionResult Project(
            AcceptedMechanicsResourcePlanningResult resourceResult)
        {
            ArgumentNullException.ThrowIfNull(resourceResult);
            var issues = new List<ValidationIssue>();
            var transitions = resourceResult.AppliedTransitions
                .Concat(resourceResult.ReplayTransitions)
                .ToArray();
            foreach (var expected in _expectedGains)
            {
                var matches = transitions.Where(transition =>
                    string.Equals(transition.EventRef, expected.EventRef, StringComparison.Ordinal) &&
                    string.Equals(
                        transition.OriginKind,
                        "registered_system_outcome",
                        StringComparison.Ordinal) &&
                    string.Equals(transition.OriginId, expected.SourceId, StringComparison.Ordinal) &&
                    ResourceCoordinateComparer.Instance.Equals(
                        transition.Coordinate,
                        expected.Coordinate) &&
                    transition.Operation == ResourceTransitionOperation.Gain).ToArray();
                if (matches.Length == 1 &&
                    matches[0].BeforeState?.Current == expected.Before &&
                    matches[0].AfterState?.Current == expected.After &&
                    matches[0].RequestedAmount == expected.Amount &&
                    matches[0].AppliedAmount == expected.Amount)
                {
                    continue;
                }

                Add(
                    issues,
                    GuardiansPath,
                    "afterlife_guardian_gacha_cycle_gain_mismatch",
                    $"one exact gain {expected.Before}->{expected.After} amount {expected.Amount}",
                    matches.Length == 1
                        ? $"{matches[0].BeforeState?.Current}->{matches[0].AfterState?.Current};requested={matches[0].RequestedAmount};applied={matches[0].AppliedAmount}"
                        : $"matches={matches.Length}");
            }

            var projectedSpends = new List<(ExpectedSpend Expected, ResourceTransition Transition)>();
            foreach (var expected in _expectedSpends)
            {
                var matches = transitions.Where(transition =>
                    string.Equals(transition.EventRef, expected.EventRef, StringComparison.Ordinal) &&
                    string.Equals(
                        transition.OriginKind,
                        "registered_system_outcome",
                        StringComparison.Ordinal) &&
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
                    projectedSpends.Add((expected, matches[0]));
                    continue;
                }

                Add(
                    issues,
                    GuardiansPath + ".UpdateGuardians",
                    "afterlife_guardian_gacha_spend_mismatch",
                    $"one exact spend {expected.Before}->{expected.After} amount 1",
                    matches.Length == 1
                        ? $"{matches[0].BeforeState?.Current}->{matches[0].AfterState?.Current};requested={matches[0].RequestedAmount};applied={matches[0].AppliedAmount}"
                        : $"matches={matches.Length}");
            }

            var ownerTransitions = new List<AcceptedMechanicsOwnerTransition>();
            if (issues.Count == 0)
            {
                foreach (var group in projectedSpends
                             .OrderBy(static value => value.Expected.CommandOrdinal)
                             .GroupBy(static value => value.Expected.GuardianId, StringComparer.Ordinal))
                {
                    var ordered = group.OrderBy(static value => value.Expected.CommandOrdinal)
                        .ToArray();
                    var entries = new JsonArray();
                    foreach (var item in ordered)
                    {
                        var entry = new JsonObject
                        {
                            ["eventId"] = item.Transition.OperationId,
                            ["resourceTransitionId"] = item.Transition.TransitionId,
                            ["returnCycleId"] = item.Expected.ReturnCycleId,
                            ["turnNumber"] = item.Transition.Turn,
                            ["relicId"] = item.Expected.RelicId,
                            ["costInFeathers"] = item.Expected.InkFeathersSpent,
                            ["finalRarity"] = item.Expected.FinalRarity,
                            ["timestamp"] = _requestTimestamp
                        };
                        if (item.Expected.GachaBonusAudit != null)
                        {
                            entry["gachaBonusAudit"] =
                                item.Expected.GachaBonusAudit.DeepClone();
                        }
                        entries.Add(entry);
                    }
                    ownerTransitions.Add(
                        AcceptedMechanicsOwnerTransition.CreateAfterlifeGuardianGacha(
                            group.Key,
                            ordered[0].Expected.ReturnCycleId,
                            ordered[0].Expected.ExpectedHistoryPrefix,
                            entries));
                }
            }

            return new ResourceRegisteredSystemOutcomeProjectionResult(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal),
                ownerTransitions,
                issues);
        }
    }

    internal static BuildResult TryCreate(
        int turn,
        string requestTimestamp,
        JsonObject preTurnRoot,
        JsonObject acceptedRoot,
        ResourceOwnerAuthority owners,
        ResourceStateLedger state,
        IReadOnlyList<ResourceOwnerCapacityDraft> capacityDrafts,
        CanonicalBeforeImage guardiansBeforeImage)
    {
        ArgumentNullException.ThrowIfNull(preTurnRoot);
        ArgumentNullException.ThrowIfNull(acceptedRoot);
        ArgumentNullException.ThrowIfNull(owners);
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(capacityDrafts);
        ArgumentNullException.ThrowIfNull(guardiansBeforeImage);
        var issues = new List<ValidationIssue>();
        if (turn <= 0 ||
            string.IsNullOrWhiteSpace(requestTimestamp) ||
            !DateTimeOffset.TryParse(requestTimestamp, out _))
        {
            Add(
                issues,
                GuardiansPath,
                "afterlife_guardian_gacha_cycle_turn_invalid",
                "positive accepted turn and exact ISO-8601 request timestamp",
                $"turn={turn};timestamp={requestTimestamp}");
            return new BuildResult(null, issues);
        }

        if (acceptedRoot["guardians"] is not JsonArray acceptedGuardians)
        {
            return new BuildResult(null, Array.Empty<ValidationIssue>());
        }

        var preTurnGuardians = preTurnRoot["guardians"] as JsonArray ?? new JsonArray();
        var previous = IndexGuardians(preTurnGuardians);
        var accepted = IndexGuardians(acceptedGuardians);
        var sources = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var expectedGains = new List<ExpectedGain>();
        var expectedSpends = new List<ExpectedSpend>();
        var projectedCurrent = new Dictionary<ResourceCoordinate, decimal>(
            ResourceCoordinateComparer.Instance);
        var lastOperations = new Dictionary<ResourceCoordinate, ResourceOperationKey>(
            ResourceCoordinateComparer.Instance);
        for (var index = 0; index < acceptedGuardians.Count; index++)
        {
            if (acceptedGuardians[index] is not JsonObject guardian ||
                ReadExact(guardian["guardianId"]) is not { } guardianId ||
                guardian["gachaSystem"] is not JsonObject gacha ||
                ReadExact(gacha["currentReturnCycleId"]) is not { } returnCycleId ||
                !previous.TryGetValue(guardianId, out var priorGuardian) ||
                priorGuardian["gachaSystem"] is not JsonObject priorGacha ||
                ReadExact(priorGacha["currentReturnCycleId"]) is not { } priorCycleId ||
                string.Equals(priorCycleId, returnCycleId, StringComparison.Ordinal))
            {
                continue;
            }

            var ownerKey = new ResourceOwnerKey(
                "chaos_sea",
                ResourceOwnerKind.AfterlifeActor,
                guardianId);
            if (!owners.Entries.TryGetValue(ownerKey, out var owner) ||
                owner.Lifecycle != ResourceOwnerLifecycle.Active ||
                !owner.ResourceCapabilities.Contains(ResourceKey))
            {
                Add(
                    issues,
                    $"{GuardiansPath}.guardians[{index}].guardianId",
                    "afterlife_guardian_gacha_cycle_owner_unresolved",
                    $"one active exact Guardian owner with {ResourceKey}",
                    guardianId);
                continue;
            }

            var coordinate = new ResourceCoordinate(
                ownerKey.Realm,
                ownerKey.OwnerKind,
                ownerKey.ResourceOwnerId,
                ResourceKey);
            var drafts = capacityDrafts.Where(draft =>
                ResourceCoordinateComparer.Instance.Equals(draft.Coordinate, coordinate) &&
                string.Equals(
                    draft.SourceEvidence.SourceKind,
                    "owner_capacity_cycle",
                    StringComparison.Ordinal) &&
                string.Equals(
                    draft.SourceEvidence.SourceId,
                    returnCycleId,
                    StringComparison.Ordinal)).ToArray();
            var capacity = drafts.Length == 1
                ? drafts[0].ResolvedCapacity.Capacity
                : null;
            if (drafts.Length != 1 ||
                capacity == null ||
                !state.TryResolveExact(coordinate, out var current) ||
                current == null ||
                current.State != ResourceLifecycleState.Active)
            {
                Add(
                    issues,
                    $"{GuardiansPath}.guardians[{index}].gachaSystem.currentReturnCycleId",
                    "afterlife_guardian_gacha_cycle_capacity_unresolved",
                    "one exact live resource and registered capacity for the accepted return cycle",
                    $"guardianId={guardianId};cycle={returnCycleId};drafts={drafts.Length}");
                continue;
            }

            var maximum = drafts[0].AcceptedMaximum;
            var reconfiguredCurrent = Math.Min(current.Current, maximum);
            var amount = maximum - reconfiguredCurrent;
            projectedCurrent[coordinate] = reconfiguredCurrent;
            if (amount <= 0m)
                continue;

            var sourceId = $"guardian_return_reset_{turn}_{index + 1}";
            using var sourceFingerprintBuilder = new ResourceFingerprintBuilder(
                "afterlife-guardian-gacha-cycle-gain-v1");
            sourceFingerprintBuilder.Append(turn);
            sourceFingerprintBuilder.Append(guardianId);
            sourceFingerprintBuilder.Append(priorCycleId);
            sourceFingerprintBuilder.Append(returnCycleId);
            sourceFingerprintBuilder.Append(owner.AuthorityFingerprint);
            sourceFingerprintBuilder.Append(current.Current);
            sourceFingerprintBuilder.Append(current.CapacityBinding.AuthorityFingerprint);
            sourceFingerprintBuilder.Append(maximum);
            sourceFingerprintBuilder.Append(capacity.Binding.AuthorityFingerprint);
            var sourceFingerprint = sourceFingerprintBuilder.Build();
            sources.Add(new ResourceMutationSourceExport(
                "registered_system_outcome",
                sourceId,
                sourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: true));
            var eventRef =
                $"turn_{turn}:guardian_return_reset:{guardianId}:{returnCycleId}";
            var mutation = new ResourceMutationIntent(
                eventRef,
                coordinate,
                amount,
                new ResourceMutationSourceRequest(
                    "registered_system_outcome",
                    sourceId,
                    ResourceOperation.Gain),
                Array.Empty<ResourceOperationKey>(),
                Array.Empty<ResourceMutationEventRequirement>(),
                ReceiptId: null);
            mutations.Add(mutation);
            expectedGains.Add(new ExpectedGain(
                eventRef,
                sourceId,
                coordinate,
                reconfiguredCurrent,
                maximum,
                amount));
            projectedCurrent[coordinate] = maximum;
            lastOperations[coordinate] = mutation.Key;
        }

        if (acceptedRoot["UpdateGuardians"] is JsonArray updates)
        {
            for (var commandOrdinal = 0; commandOrdinal < updates.Count; commandOrdinal++)
            {
                if (updates[commandOrdinal] is not JsonObject command ||
                    !string.Equals(
                        ReadString(command["command"]),
                        "processGacha",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                var path = $"{GuardiansPath}.UpdateGuardians[{commandOrdinal}]";
                var guardianId = ReadExact(command["guardianId"]);
                if (guardianId == null ||
                    !accepted.TryGetValue(guardianId, out var guardian) ||
                    guardian["gachaSystem"] is not JsonObject gacha ||
                    ReadExact(gacha["currentReturnCycleId"]) is not { } returnCycleId ||
                    gacha["gachaHistory"] is not JsonArray acceptedHistory ||
                    command["result"] is not JsonObject result ||
                    ReadExact(result["relicId"]) is not { } relicId ||
                    ReadExact(result["rarity"] ?? result["quality"]) is not { } finalRarity ||
                    !TryReadPositiveInt(command["inkFeathersSpent"], out var spentFeathers))
                {
                    Add(
                        issues,
                        path,
                        "afterlife_guardian_gacha_command_invalid",
                        "exact guardian/cycle/relic/rarity and positive inkFeathersSpent",
                        command.ToJsonString());
                    continue;
                }

                var expectedPrefix = previous.TryGetValue(guardianId, out var priorGuardian) &&
                                     priorGuardian["gachaSystem"] is JsonObject priorGacha &&
                                     priorGacha["gachaHistory"] is JsonArray priorHistory
                    ? priorHistory
                    : new JsonArray();
                if (!ArraysEqual(acceptedHistory, expectedPrefix))
                {
                    Add(
                        issues,
                        path,
                        "afterlife_guardian_gacha_history_direct_mutation",
                        "accepted Guardian history equal to the validated pre-turn prefix; processGacha is the only append authority",
                        acceptedHistory.ToJsonString());
                    continue;
                }

                var ownerKey = new ResourceOwnerKey(
                    "chaos_sea",
                    ResourceOwnerKind.AfterlifeActor,
                    guardianId);
                if (!owners.Entries.TryGetValue(ownerKey, out var owner) ||
                    owner.Lifecycle != ResourceOwnerLifecycle.Active ||
                    !owner.ResourceCapabilities.Contains(ResourceKey))
                {
                    Add(
                        issues,
                        path + ".guardianId",
                        "afterlife_guardian_gacha_owner_unresolved",
                        $"one active exact Guardian owner with {ResourceKey}",
                        guardianId);
                    continue;
                }

                var coordinate = new ResourceCoordinate(
                    ownerKey.Realm,
                    ownerKey.OwnerKind,
                    ownerKey.ResourceOwnerId,
                    ResourceKey);
                if (!projectedCurrent.TryGetValue(coordinate, out var current) &&
                    !TryResolveStartingCurrent(
                        coordinate,
                        owner,
                        state,
                        capacityDrafts,
                        out current,
                        issues,
                        path))
                {
                    continue;
                }
                if (current < 1m)
                {
                    Add(
                        issues,
                        path,
                        "afterlife_guardian_gacha_resource_exhausted",
                        "one remaining canonical gacha_attempts unit",
                        $"current={current}");
                    continue;
                }

                var sourceId = $"guardian_gacha_{turn}_{commandOrdinal + 1}";
                using var sourceFingerprintBuilder = new ResourceFingerprintBuilder(
                    "afterlife-guardian-gacha-spend-v1");
                sourceFingerprintBuilder.Append(turn);
                sourceFingerprintBuilder.Append(commandOrdinal);
                sourceFingerprintBuilder.Append(guardianId);
                sourceFingerprintBuilder.Append(returnCycleId);
                sourceFingerprintBuilder.Append(owner.AuthorityFingerprint);
                sourceFingerprintBuilder.Append(current);
                sourceFingerprintBuilder.Append(command.ToJsonString());
                var sourceFingerprint = sourceFingerprintBuilder.Build();
                sources.Add(new ResourceMutationSourceExport(
                    "registered_system_outcome",
                    sourceId,
                    sourceFingerprint,
                    ResourceMutationSourceState.Active,
                    SameTurn: true));
                var eventRef =
                    $"turn_{turn}:guardian_gacha:{commandOrdinal + 1}:{guardianId}";
                var dependencies = lastOperations.TryGetValue(coordinate, out var priorOperation)
                    ? new[] { priorOperation }
                    : Array.Empty<ResourceOperationKey>();
                var mutation = new ResourceMutationIntent(
                    eventRef,
                    coordinate,
                    1m,
                    new ResourceMutationSourceRequest(
                        "registered_system_outcome",
                        sourceId,
                        ResourceOperation.Spend),
                    dependencies,
                    Array.Empty<ResourceMutationEventRequirement>(),
                    ReceiptId: null);
                mutations.Add(mutation);
                expectedSpends.Add(new ExpectedSpend(
                    commandOrdinal,
                    eventRef,
                    sourceId,
                    guardianId,
                    returnCycleId,
                    coordinate,
                    current,
                    current - 1m,
                    spentFeathers,
                    relicId,
                    finalRarity,
                    command["gachaBonusAudit"]?.DeepClone(),
                    expectedPrefix.DeepClone().AsArray()));
                projectedCurrent[coordinate] = current - 1m;
                lastOperations[coordinate] = mutation.Key;
            }
        }

        if (issues.Count != 0)
            return new BuildResult(null, issues);
        if (mutations.Count == 0)
            return new BuildResult(null, Array.Empty<ValidationIssue>());

        using var fingerprint = new ResourceFingerprintBuilder(
            "afterlife-guardian-gacha-draft-v2");
        fingerprint.Append(turn);
        fingerprint.Append(requestTimestamp);
        fingerprint.Append(state.Fingerprint);
        fingerprint.Append(guardiansBeforeImage.Fingerprint);
        foreach (var source in sources)
        {
            fingerprint.Append(source.SourceId);
            fingerprint.Append(source.AuthorityFingerprint);
        }
        return new BuildResult(
            new Draft(
                fingerprint.Build(),
                sources,
                mutations,
                expectedGains,
                expectedSpends,
                requestTimestamp,
                guardiansBeforeImage),
            Array.Empty<ValidationIssue>());
    }

    private static Dictionary<string, JsonObject> IndexGuardians(JsonArray guardians)
    {
        var result = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var guardianId = ReadExact(guardian["guardianId"]);
            if (guardianId != null)
                result.TryAdd(guardianId, guardian);
        }
        return result;
    }

    private static bool TryResolveStartingCurrent(
        ResourceCoordinate coordinate,
        ResourceOwnerAuthorityEntry owner,
        ResourceStateLedger state,
        IReadOnlyList<ResourceOwnerCapacityDraft> capacityDrafts,
        out decimal current,
        List<ValidationIssue> issues,
        string path)
    {
        current = 0m;
        var drafts = capacityDrafts.Where(draft =>
            ResourceCoordinateComparer.Instance.Equals(
                draft.Coordinate,
                coordinate)).ToArray();
        if (drafts.Length != 1 ||
            drafts[0].ResolvedCapacity.Capacity is not { } capacity)
        {
            Add(
                issues,
                path,
                "afterlife_guardian_gacha_capacity_unresolved",
                "one exact registered Guardian return capacity",
                $"drafts={drafts.Length}");
            return false;
        }

        if (state.TryResolveExact(coordinate, out var entry) && entry != null)
        {
            if (entry.State != ResourceLifecycleState.Active)
            {
                Add(
                    issues,
                    path,
                    "afterlife_guardian_gacha_resource_inactive",
                    "active canonical gacha_attempts coordinate",
                    entry.State.ToString());
                return false;
            }
            current = entry.Maximum == capacity.Maximum &&
                      entry.CapacityBinding == capacity.Binding
                ? entry.Current
                : Math.Min(entry.Current, capacity.Maximum);
            return true;
        }

        if (!owner.SameTurn || capacity.Initialization == null)
        {
            Add(
                issues,
                path,
                "afterlife_guardian_gacha_resource_unresolved",
                "one live coordinate or exact same-turn initialized Guardian capacity",
                $"sameTurn={owner.SameTurn}");
            return false;
        }
        current = capacity.Initialization.Current;
        return true;
    }

    private static bool ArraysEqual(JsonArray left, JsonArray right)
    {
        if (left.Count != right.Count)
            return false;
        for (var index = 0; index < left.Count; index++)
        {
            if (!JsonNode.DeepEquals(left[index], right[index]))
                return false;
        }
        return true;
    }

    private static string? ReadExact(JsonNode? node) =>
        node is JsonValue value &&
        value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static string? ReadString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text)
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
