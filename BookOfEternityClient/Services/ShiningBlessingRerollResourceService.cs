using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class ShiningBlessingRerollResourceFilePlan
{
    private readonly ValidationIssue[] _issues;
    private readonly CoordinatedStateWriteHelper.PlannedWrite[] _writes;

    internal ShiningBlessingRerollResourceFilePlan(
        JsonObject? effectStateAfterImage,
        IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> writes,
        IReadOnlyList<ValidationIssue> issues,
        bool isReplay = false)
    {
        EffectStateAfterImage = effectStateAfterImage?.DeepClone().AsObject();
        _writes = writes?.ToArray() ?? throw new ArgumentNullException(nameof(writes));
        _issues = issues?.ToArray() ?? throw new ArgumentNullException(nameof(issues));
        IsReplay = isReplay;
    }

    internal JsonObject? EffectStateAfterImage { get; }
    internal IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> Writes =>
        Array.AsReadOnly(_writes.ToArray());
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal bool IsReplay { get; }
    internal bool IsValid => EffectStateAfterImage != null && _issues.Length == 0;
}

internal sealed record ShiningBlessingRerollAllocationProjection(
    ResourceCoordinate? Coordinate,
    string? AllocationId,
    int Remaining,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid =>
        Coordinate != null && AllocationId != null && Issues.Count == 0;
}

internal sealed class ShiningBlessingRerollSpendFilePlan
{
    private readonly ValidationIssue[] _issues;
    private readonly CoordinatedStateWriteHelper.PlannedWrite[] _writes;

    internal ShiningBlessingRerollSpendFilePlan(
        int remainingAfter,
        IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> writes,
        IReadOnlyList<ValidationIssue> issues)
    {
        RemainingAfter = remainingAfter;
        _writes = writes.ToArray();
        _issues = issues.ToArray();
    }

    internal int RemainingAfter { get; }
    internal IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> Writes =>
        Array.AsReadOnly(_writes.ToArray());
    internal IReadOnlyList<ValidationIssue> Issues =>
        Array.AsReadOnly(_issues.ToArray());
    internal bool IsValid => _issues.Length == 0;
}

internal static class ShiningBlessingRerollResourceService
{
    private const string ResourceKey = "blessing_rerolls";
    private const string Realm = "shining_abode";
    private const string PlayerSoulId = "player_soul";
    private const string BindingProperty = "rerollResourceBinding";
    private const string SoulStatePath = "game_state/meta/soul_state.json";

    internal static async Task<ShiningBlessingRerollResourceFilePlan> BuildBootstrapAsync(
        FileSystemManager fs,
        JsonObject effectState,
        int currentIncarnation,
        JsonObject? existingEffectState = null)
    {
        ArgumentNullException.ThrowIfNull(fs);
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return await BuildBootstrapAsync(
            fs,
            writeLease,
            effectState,
            currentIncarnation,
            existingEffectState);
    }

    internal static async Task<ShiningBlessingRerollResourceFilePlan> BuildBootstrapAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject effectState,
        int currentIncarnation,
        JsonObject? existingEffectState = null)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(effectState);
        var afterImage = effectState.DeepClone().AsObject();
        var memory = afterImage["memorySelection"] as JsonObject;
        var relic = afterImage["relicRefinementEntitlements"] as JsonObject;
        if (!ShiningBlessingRerollAllocationContract.TryConsume(memory, out var memoryAmount) ||
            !ShiningBlessingRerollAllocationContract.TryConsume(relic, out var relicAmount))
        {
            return Failure(
                "shining_blessing_reroll_allocation_invalid",
                "absent or closed {resourceKey:'blessing_rerolls',amount:non-negative integer} allocation",
                afterImage.ToJsonString());
        }

        var total = checked(memoryAmount + relicAmount);
        var preparedAtTurn = ReadNonNegativeInt(afterImage["sourcePackagePreparedAtTurn"]);
        if (preparedAtTurn <= 0 || currentIncarnation <= 0)
        {
            return Failure(
                "shining_blessing_reroll_bootstrap_identity_invalid",
                "positive prepared turn and incarnation",
                $"turn={preparedAtTurn};incarnation={currentIncarnation}");
        }

        var paths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            AfterlifeEntityProfileState.StatePath,
            SoulStatePath
        };
        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths)
            beforeImages[path] = await fs.ReadFileAsync(writeLease, path);

        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            beforeImages[ResourceMaterializationContract.DefinitionsPath],
            allowMissingPristine: false);
        if (!definitionsResult.IsValid || definitionsResult.Catalog == null)
            return Failure(definitionsResult.Issues);
        var stateResult = ResourceStateContract.ParseCanonical(
            beforeImages[ResourceMaterializationContract.StatePath],
            definitionsResult.Catalog,
            allowMissingPristine: false);
        if (!stateResult.IsValid || stateResult.Ledger == null)
            return Failure(stateResult.Issues);
        var historyResult = ResourceHistoryState.ParseCanonical(
            beforeImages[ResourceMaterializationContract.HistoryPath],
            definitionsResult.Catalog,
            allowMissingPristine: false);
        if (!historyResult.IsValid || historyResult.History == null)
            return Failure(historyResult.Issues);
        var existingAgreement = historyResult.History.ValidateStateAgreement(
            stateResult.Ledger);
        if (existingAgreement.Count != 0)
            return Failure(existingAgreement);

        var profileIssues = new List<ValidationIssue>();
        var profiles = ParseObject(
            beforeImages[AfterlifeEntityProfileState.StatePath],
            AfterlifeEntityProfileState.StatePath,
            profileIssues);
        if (profiles == null)
            return Failure(profileIssues);
        var soulState = ParseObject(
            beforeImages[SoulStatePath],
            SoulStatePath,
            profileIssues);
        if (soulState == null)
            return Failure(profileIssues);
        var owners = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitionsResult.Catalog,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    spiritualConflict: null,
                    soulState: soulState),
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    spiritualConflict: null,
                    soulState: soulState)));
        if (!owners.IsValid || owners.Authority == null)
            return Failure(owners.Issues);
        var owner = owners.Authority.Resolve(new ResourceOwnerRequest(
            Realm,
            ResourceOwnerKind.AfterlifeActor,
            ResourceKey,
            PlayerSoulId,
            OwnerRef: null));
        if (!owner.Success || owner.Entry == null)
            return Failure(owner.Issues);
        if (!definitionsResult.Catalog.TryResolveExact(ResourceKey, out var definition) ||
            definition == null)
        {
            return Failure(
                "shining_blessing_reroll_definition_missing",
                "sealed blessing_rerolls definition",
                ResourceKey);
        }

        var coordinate = new ResourceCoordinate(
            Realm,
            ResourceOwnerKind.AfterlifeActor,
            PlayerSoulId,
            ResourceKey);
        var capacityId = $"shining_blessing_capacity_{preparedAtTurn}_{currentIncarnation}";
        var memoryId = $"shining_blessing_memory_{preparedAtTurn}_{currentIncarnation}";
        var relicId = $"shining_blessing_relic_{preparedAtTurn}_{currentIncarnation}";
        var expireId = $"shining_blessing_expire_{preparedAtTurn}_{currentIncarnation}";
        var capacityFingerprint = CapacityFingerprint(
            owner.Entry,
            coordinate,
            preparedAtTurn,
            currentIncarnation,
            memoryAmount,
            relicAmount,
            memory,
            relic);
        var memoryFingerprint = memoryAmount > 0
            ? SourceFingerprint(
                "memory",
                owner.Entry,
                coordinate,
                preparedAtTurn,
                currentIncarnation,
                memoryAmount,
                ReadStrings(memory?["sourceCardIds"]))
            : null;
        var relicFingerprint = relicAmount > 0
            ? SourceFingerprint(
                "relic",
                owner.Entry,
                coordinate,
                preparedAtTurn,
                currentIncarnation,
                relicAmount,
                ReadStrings(relic?["sourceCardIds"]))
            : null;
        var existing = stateResult.Ledger.TryResolveExact(coordinate, out var found)
            ? found
            : null;
        var samePackageIdentity = ExistingEffectStateMatchesPackage(
            existingEffectState,
            afterImage,
            preparedAtTurn,
            currentIncarnation);
        if (total == 0)
        {
            if (existing == null || existing.Current == 0m)
            {
                return samePackageIdentity
                    ? new ShiningBlessingRerollResourceFilePlan(
                        existingEffectState,
                        Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                        Array.Empty<ValidationIssue>(),
                        isReplay: true)
                    : Success(
                        afterImage,
                        Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>());
            }

            var expireFingerprint = SourceFingerprint(
                "expire",
                owner.Entry,
                coordinate,
                preparedAtTurn,
                currentIncarnation,
                existing.Current,
                Array.Empty<string>());
            var expireSources = ResourceMutationSourceCatalog.Create(new[]
            {
                Source(expireId, expireFingerprint)
            });
            if (!expireSources.IsValid || expireSources.Catalog == null)
                return Failure(expireSources.Issues);
            var expirePlan = AcceptedMechanicsPlanner.BuildResources(
                new AcceptedMechanicsResourceInput(
                    preparedAtTurn,
                    definitionsResult.Catalog,
                    stateResult.Ledger,
                    historyResult.History,
                    expireSources.Catalog,
                    new[]
                    {
                        Mutation(
                            preparedAtTurn,
                            "expire",
                            expireId,
                            expireFingerprint,
                            coordinate,
                            ResourceOperation.Spend,
                            existing.Current,
                            Array.Empty<ResourceOperationKey>())
                    },
                    Array.Empty<ResourceCapacityIntent>()),
                new AcceptedMechanicsIdentityFactory());
            if (!expirePlan.IsValid || expirePlan.StateAfterImage == null ||
                expirePlan.HistoryAfterImage == null)
            {
                return Failure(expirePlan.Issues);
            }
            var expireAgreement = expirePlan.HistoryAfterImage.ValidateStateAgreement(
                expirePlan.StateAfterImage);
            if (expireAgreement.Count != 0)
                return Failure(expireAgreement);

            return Success(
                afterImage,
                new[]
                {
                    CoordinatedStateWriteHelper.CreateGuardWrite(
                        ResourceMaterializationContract.DefinitionsPath,
                        beforeImages[ResourceMaterializationContract.DefinitionsPath]),
                    CoordinatedStateWriteHelper.CreateGuardWrite(
                        AfterlifeEntityProfileState.StatePath,
                        beforeImages[AfterlifeEntityProfileState.StatePath]),
                    Write(
                        ResourceMaterializationContract.StatePath,
                        beforeImages[ResourceMaterializationContract.StatePath],
                        expirePlan.StateAfterImage.ToCanonicalJson()),
                    Write(
                        ResourceMaterializationContract.HistoryPath,
                        beforeImages[ResourceMaterializationContract.HistoryPath],
                        expirePlan.HistoryAfterImage.ToCanonicalJson())
                });
        }
        var resolved = ResolvedResourceCapacity.Resolve(
            definition,
            coordinate,
            new InstanceFixedCapacityInput(
                new ResourceFormulaOwner(
                    coordinate.Realm,
                    coordinate.OwnerKind,
                    coordinate.ResourceOwnerId),
                total,
                capacityFingerprint),
            capacityId,
            includeInitialization: existing == null);
        if (!resolved.IsValid || resolved.Capacity == null)
            return Failure(resolved.Issues);
        var allocationMatches = ExistingAllocationMatches(
            existing,
            historyResult.History,
            coordinate,
            total,
            resolved.Capacity.Binding.AuthorityFingerprint,
            memoryId,
            memoryAmount,
            memoryFingerprint,
            relicId,
            relicAmount,
            relicFingerprint);
        var memoryBindingMatches = ExistingBindingMatches(
            existingEffectState?["memorySelection"] as JsonObject,
            coordinate,
            memoryId,
            memoryAmount);
        var relicBindingMatches = ExistingBindingMatches(
            existingEffectState?["relicRefinementEntitlements"] as JsonObject,
            coordinate,
            relicId,
            relicAmount);
        var exactReplay = samePackageIdentity &&
                          allocationMatches &&
                          memoryBindingMatches &&
                          relicBindingMatches;
        if (exactReplay)
        {
            return new ShiningBlessingRerollResourceFilePlan(
                existingEffectState,
                Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                Array.Empty<ValidationIssue>(),
                isReplay: true);
        }

        if (samePackageIdentity ||
            historyResult.History.Transitions.Any(transition =>
                string.Equals(transition.OriginId, memoryId, StringComparison.Ordinal) ||
                string.Equals(transition.OriginId, relicId, StringComparison.Ordinal)))
        {
            return Failure(
                "shining_blessing_reroll_replay_authority_mismatch",
                "exact existing package identity, allocation bindings, capacity and immutable grants",
                $"turn={preparedAtTurn};incarnation={currentIncarnation};" +
                $"identity={samePackageIdentity};allocation={allocationMatches};" +
                $"memoryBinding={memoryBindingMatches};relicBinding={relicBindingMatches}");
        }
        var capacity = new ResourceCapacityIntent(
            $"turn_{preparedAtTurn}:shining_blessing:capacity",
            "bootstrap_materialization",
            capacityId,
            coordinate,
            existing == null
                ? ResourceCapacityOperation.Initialize
                : ResourceCapacityOperation.Reconfigure,
            existing == null
                ? resolved.Capacity
                : resolved.Capacity.AsReconfiguration(),
            existing == null
                ? ResourceCurrentDisposition.InitializeFromDefinition
                : ResourceCurrentDisposition.ClampToNewMaximum,
            ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 70,
            new ResourceSourceEvidence(
                "bootstrap_materialization",
                capacityId,
                capacityFingerprint),
            existing == null
                ? resolved.Capacity.Initialization!.AuthorityFingerprint
                : resolved.Capacity.Binding.AuthorityFingerprint,
            ReceiptId: null);

        var exports = new List<ResourceMutationSourceExport>();
        var mutations = new List<ResourceMutationIntent>();
        var projectedOldCurrent = existing == null
            ? 0m
            : Math.Min(existing.Current, total);
        ResourceOperationKey? expireKey = null;
        if (projectedOldCurrent > 0m)
        {
            var fingerprint = SourceFingerprint(
                "expire",
                owner.Entry,
                coordinate,
                preparedAtTurn,
                currentIncarnation,
                projectedOldCurrent,
                Array.Empty<string>());
            exports.Add(Source(expireId, fingerprint));
            var mutation = Mutation(
                preparedAtTurn,
                "expire",
                expireId,
                fingerprint,
                coordinate,
                ResourceOperation.Spend,
                projectedOldCurrent,
                Array.Empty<ResourceOperationKey>());
            mutations.Add(mutation);
            expireKey = mutation.Key;
        }
        if (memoryAmount > 0)
        {
            var fingerprint = memoryFingerprint!;
            exports.Add(Source(memoryId, fingerprint));
            mutations.Add(Mutation(
                preparedAtTurn,
                "memory",
                memoryId,
                fingerprint,
                coordinate,
                ResourceOperation.Gain,
                memoryAmount,
                expireKey == null ? Array.Empty<ResourceOperationKey>() : new[] { expireKey }));
            memory![BindingProperty] = Binding(coordinate, memoryId);
        }
        if (relicAmount > 0)
        {
            var fingerprint = relicFingerprint!;
            exports.Add(Source(relicId, fingerprint));
            var dependencies = new List<ResourceOperationKey>();
            if (expireKey != null)
                dependencies.Add(expireKey);
            if (mutations.LastOrDefault(candidate =>
                    string.Equals(candidate.Source.SourceId, memoryId, StringComparison.Ordinal))
                is { } memoryMutation)
            {
                dependencies.Add(memoryMutation.Key);
            }
            mutations.Add(Mutation(
                preparedAtTurn,
                "relic",
                relicId,
                fingerprint,
                coordinate,
                ResourceOperation.Gain,
                relicAmount,
                dependencies));
            relic![BindingProperty] = Binding(coordinate, relicId);
        }

        var sources = ResourceMutationSourceCatalog.Create(exports);
        if (!sources.IsValid || sources.Catalog == null)
            return Failure(sources.Issues);
        var planned = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                preparedAtTurn,
                definitionsResult.Catalog,
                stateResult.Ledger,
                historyResult.History,
                sources.Catalog,
                mutations,
                new[] { capacity }),
            new AcceptedMechanicsIdentityFactory());
        if (!planned.IsValid || planned.StateAfterImage == null ||
            planned.HistoryAfterImage == null)
        {
            return Failure(planned.Issues);
        }
        var agreement = planned.HistoryAfterImage.ValidateStateAgreement(
            planned.StateAfterImage);
        if (agreement.Count != 0)
            return Failure(agreement);

        var writes = new[]
        {
            CoordinatedStateWriteHelper.CreateGuardWrite(
                ResourceMaterializationContract.DefinitionsPath,
                beforeImages[ResourceMaterializationContract.DefinitionsPath]),
            CoordinatedStateWriteHelper.CreateGuardWrite(
                AfterlifeEntityProfileState.StatePath,
                beforeImages[AfterlifeEntityProfileState.StatePath]),
            Write(
                ResourceMaterializationContract.StatePath,
                beforeImages[ResourceMaterializationContract.StatePath],
                planned.StateAfterImage.ToCanonicalJson()),
            Write(
                ResourceMaterializationContract.HistoryPath,
                beforeImages[ResourceMaterializationContract.HistoryPath],
                planned.HistoryAfterImage.ToCanonicalJson())
        };
        return Success(afterImage, writes);
    }

    internal static async Task<ShiningBlessingRerollAllocationProjection>
        ReadAllocationAsync(
            FileSystemManager fs,
            JsonObject entitlement)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(entitlement);
        var loaded = await LoadAllocationAuthorityAsync(
            fs,
            writeLease: null,
            entitlement);
        return loaded.IsValid
            ? new ShiningBlessingRerollAllocationProjection(
                loaded.Coordinate,
                loaded.AllocationId,
                loaded.Remaining,
                Array.Empty<ValidationIssue>())
            : new ShiningBlessingRerollAllocationProjection(
                null,
                null,
                0,
                loaded.Issues);
    }

    internal static async Task<ShiningBlessingRerollAllocationProjection>
        ReadAllocationAsync(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease readLease,
            JsonObject entitlement)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(readLease);
        ArgumentNullException.ThrowIfNull(entitlement);
        var loaded = await LoadAllocationAuthorityAsync(
            fs,
            readLease,
            entitlement);
        return loaded.IsValid
            ? new ShiningBlessingRerollAllocationProjection(
                loaded.Coordinate,
                loaded.AllocationId,
                loaded.Remaining,
                Array.Empty<ValidationIssue>())
            : new ShiningBlessingRerollAllocationProjection(
                null,
                null,
                0,
                loaded.Issues);
    }

    internal static Task<ShiningBlessingRerollSpendFilePlan> BuildSpendAsync(
        FileSystemManager fs,
        JsonObject entitlement,
        int turn,
        int amount) =>
        BuildSpendCoreAsync(fs, writeLease: null, entitlement, turn, amount);

    internal static Task<ShiningBlessingRerollSpendFilePlan> BuildSpendAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject entitlement,
        int turn,
        int amount) =>
        BuildSpendCoreAsync(fs, writeLease, entitlement, turn, amount);

    private static async Task<ShiningBlessingRerollSpendFilePlan>
        BuildSpendCoreAsync(
            FileSystemManager fs,
            FileSystemManager.CanonicalWriteLease? writeLease,
            JsonObject entitlement,
            int turn,
            int amount)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(entitlement);
        if (turn < 0 || amount <= 0)
        {
            return SpendFailure(
                "shining_blessing_reroll_spend_input_invalid",
                "non-negative turn and positive integral amount",
                $"turn={turn};amount={amount}");
        }

        var loaded = await LoadAllocationAuthorityAsync(fs, writeLease, entitlement);
        if (!loaded.IsValid || loaded.Coordinate == null ||
            loaded.AllocationId == null || loaded.Definitions == null ||
            loaded.State == null || loaded.History == null ||
            loaded.SourceFingerprint == null)
        {
            return new ShiningBlessingRerollSpendFilePlan(
                0,
                Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                loaded.Issues);
        }
        if (amount > loaded.Remaining)
        {
            return SpendFailure(
                "shining_blessing_reroll_allocation_exhausted",
                $"amount at most allocation remainder {loaded.Remaining}",
                amount.ToString());
        }
        if (!loaded.State.TryResolveExact(loaded.Coordinate, out var entry) ||
            entry == null || entry.State != ResourceLifecycleState.Active ||
            entry.Current < amount)
        {
            return SpendFailure(
                "shining_blessing_reroll_state_exhausted",
                "active actor ledger with sufficient current",
                entry == null ? "missing" : $"state={entry.State};current={entry.Current}");
        }

        var source = ResourceMutationSourceCatalog.Create(new[]
        {
            new ResourceMutationSourceExport(
                "registered_system_outcome",
                loaded.AllocationId,
                loaded.SourceFingerprint,
                ResourceMutationSourceState.Active,
                SameTurn: false)
        });
        if (!source.IsValid || source.Catalog == null)
            return new ShiningBlessingRerollSpendFilePlan(
                0,
                Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                source.Issues);
        var ordinal = loaded.History.Transitions.Count(transition =>
            transition.Coordinate == loaded.Coordinate &&
            transition.Operation == ResourceTransitionOperation.Spend &&
            string.Equals(
                transition.OriginId,
                loaded.AllocationId,
                StringComparison.Ordinal)) + 1;
        var latestResourceTurn = loaded.History.Transitions
            .Where(transition => transition.Coordinate == loaded.Coordinate)
            .Select(static transition => transition.Turn)
            .DefaultIfEmpty(0)
            .Max();
        if (latestResourceTurn == int.MaxValue && turn <= latestResourceTurn)
        {
            return SpendFailure(
                "shining_blessing_reroll_turn_exhausted",
                "a resource transition turn after the latest allocation transition",
                latestResourceTurn.ToString());
        }
        var effectiveTurn = Math.Max(
            Math.Max(1, turn),
            latestResourceTurn + 1);
        var mutation = new ResourceMutationIntent(
            $"turn_{effectiveTurn}:shining_blessing:{loaded.AllocationId}:spend_{ordinal}",
            loaded.Coordinate,
            amount,
            new ResourceMutationSourceRequest(
                "registered_system_outcome",
                loaded.AllocationId,
                ResourceOperation.Spend),
            Array.Empty<ResourceOperationKey>(),
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);
        var planned = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                effectiveTurn,
                loaded.Definitions,
                loaded.State,
                loaded.History,
                source.Catalog,
                new[] { mutation }),
            new AcceptedMechanicsIdentityFactory());
        if (!planned.IsValid || planned.StateAfterImage == null ||
            planned.HistoryAfterImage == null)
        {
            return new ShiningBlessingRerollSpendFilePlan(
                0,
                Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
                planned.Issues);
        }

        var writes = new[]
        {
            CoordinatedStateWriteHelper.CreateGuardWrite(
                ResourceMaterializationContract.DefinitionsPath,
                loaded.BeforeImages[ResourceMaterializationContract.DefinitionsPath]),
            Write(
                ResourceMaterializationContract.StatePath,
                loaded.BeforeImages[ResourceMaterializationContract.StatePath],
                planned.StateAfterImage.ToCanonicalJson()),
            Write(
                ResourceMaterializationContract.HistoryPath,
                loaded.BeforeImages[ResourceMaterializationContract.HistoryPath],
                planned.HistoryAfterImage.ToCanonicalJson())
        };
        return new ShiningBlessingRerollSpendFilePlan(
            loaded.Remaining - amount,
            writes,
            Array.Empty<ValidationIssue>());
    }

    private static ResourceMutationSourceExport Source(
        string sourceId,
        string fingerprint) =>
        new(
            "registered_system_outcome",
            sourceId,
            fingerprint,
            ResourceMutationSourceState.Active,
            SameTurn: true);

    private static async Task<AllocationAuthority> LoadAllocationAuthorityAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        JsonObject entitlement)
    {
        if (!TryReadBinding(
                entitlement[BindingProperty],
                out var coordinate,
                out var allocationId,
                out var bindingIssue))
        {
            return AllocationAuthority.Failure(bindingIssue!);
        }
        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ResourceMaterializationContract.DefinitionsPath] = await ReadAsync(
                fs,
                writeLease,
                ResourceMaterializationContract.DefinitionsPath),
            [ResourceMaterializationContract.StatePath] = await ReadAsync(
                fs,
                writeLease,
                ResourceMaterializationContract.StatePath),
            [ResourceMaterializationContract.HistoryPath] = await ReadAsync(
                fs,
                writeLease,
                ResourceMaterializationContract.HistoryPath)
        };
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            beforeImages[ResourceMaterializationContract.DefinitionsPath],
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
            return AllocationAuthority.Failure(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            beforeImages[ResourceMaterializationContract.StatePath],
            definitions.Catalog,
            allowMissingPristine: false);
        if (!state.IsValid || state.Ledger == null)
            return AllocationAuthority.Failure(state.Issues);
        var history = ResourceHistoryState.ParseCanonical(
            beforeImages[ResourceMaterializationContract.HistoryPath],
            definitions.Catalog,
            allowMissingPristine: false);
        if (!history.IsValid || history.History == null)
            return AllocationAuthority.Failure(history.Issues);
        var agreement = history.History.ValidateStateAgreement(state.Ledger);
        if (agreement.Count != 0)
            return AllocationAuthority.Failure(agreement);
        if (!state.Ledger.TryResolveExact(coordinate!, out var entry) || entry == null)
        {
            return AllocationAuthority.Failure(Issue(
                ResourceMaterializationContract.StatePath,
                "shining_blessing_reroll_coordinate_missing",
                "one exact bound actor resource coordinate",
                DescribeCoordinate(coordinate!)));
        }

        var gains = history.History.Transitions.Where(transition =>
                transition.Coordinate == coordinate &&
                transition.Operation == ResourceTransitionOperation.Gain &&
                string.Equals(transition.OriginId, allocationId, StringComparison.Ordinal))
            .ToArray();
        if (gains.Length != 1 || gains[0].AppliedAmount <= 0m ||
            gains[0].AppliedAmount != decimal.Truncate(gains[0].AppliedAmount))
        {
            return AllocationAuthority.Failure(Issue(
                ResourceMaterializationContract.HistoryPath,
                "shining_blessing_reroll_grant_authority_invalid",
                "one positive integral gain for the exact allocationId",
                $"allocationId={allocationId};matches={gains.Length}"));
        }
        var spent = history.History.Transitions
            .Where(transition =>
                transition.Coordinate == coordinate &&
                transition.Operation == ResourceTransitionOperation.Spend &&
                string.Equals(transition.OriginId, allocationId, StringComparison.Ordinal))
            .Sum(static transition => transition.AppliedAmount);
        var remaining = gains[0].AppliedAmount - spent;
        if (remaining < 0m || remaining != decimal.Truncate(remaining) ||
            remaining > int.MaxValue || remaining > entry.Current)
        {
            return AllocationAuthority.Failure(Issue(
                ResourceMaterializationContract.HistoryPath,
                "shining_blessing_reroll_allocation_agreement_invalid",
                "non-negative integral allocation remainder within ledger current",
                $"granted={gains[0].AppliedAmount};spent={spent};ledger={entry.Current}"));
        }
        return AllocationAuthority.Success(
            coordinate!,
            allocationId!,
            (int)remaining,
            gains[0].SourceEvidence.AuthorityFingerprint,
            definitions.Catalog,
            state.Ledger,
            history.History,
            beforeImages);
    }

    private static bool TryReadBinding(
        JsonNode? node,
        out ResourceCoordinate? coordinate,
        out string? allocationId,
        out ValidationIssue? issue)
    {
        coordinate = null;
        allocationId = null;
        issue = null;
        if (node is not JsonObject binding ||
            binding.Count != 5 ||
            !TryReadExact(binding["realm"], out var realm) ||
            !TryReadExact(binding["ownerKind"], out var ownerKind) ||
            !TryReadExact(binding["resourceOwnerId"], out var ownerId) ||
            !TryReadExact(binding["resourceKey"], out var resourceKey) ||
            !TryReadExact(binding["allocationId"], out allocationId) ||
            !string.Equals(realm, Realm, StringComparison.Ordinal) ||
            !string.Equals(ownerKind, "afterlife_actor", StringComparison.Ordinal) ||
            !string.Equals(ownerId, PlayerSoulId, StringComparison.Ordinal) ||
            !string.Equals(resourceKey, ResourceKey, StringComparison.Ordinal))
        {
            issue = Issue(
                "game_state/meta/soul_state.json",
                "shining_blessing_reroll_binding_invalid",
                "closed exact blessing_rerolls allocation binding",
                node?.ToJsonString() ?? "missing/null");
            return false;
        }
        coordinate = new ResourceCoordinate(
            realm!,
            ResourceOwnerKind.AfterlifeActor,
            ownerId!,
            resourceKey!);
        return true;
    }

    private static bool TryReadExact(JsonNode? node, out string? value)
    {
        value = null;
        return node is JsonValue scalar &&
               scalar.TryGetValue<string>(out value) &&
               ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private static string DescribeCoordinate(ResourceCoordinate coordinate) =>
        $"{coordinate.Realm}/{coordinate.OwnerKind}/{coordinate.ResourceOwnerId}/{coordinate.ResourceKey}";

    private static ResourceMutationIntent Mutation(
        int turn,
        string purpose,
        string sourceId,
        string fingerprint,
        ResourceCoordinate coordinate,
        ResourceOperation operation,
        decimal amount,
        IReadOnlyList<ResourceOperationKey> dependencies) =>
        new(
            $"turn_{turn}:shining_blessing:{purpose}",
            coordinate,
            amount,
            new ResourceMutationSourceRequest(
                "registered_system_outcome",
                sourceId,
                operation),
            dependencies,
            Array.Empty<ResourceMutationEventRequirement>(),
            ReceiptId: null);

    private static JsonObject Binding(
        ResourceCoordinate coordinate,
        string allocationId) =>
        new()
        {
            ["realm"] = coordinate.Realm,
            ["ownerKind"] = "afterlife_actor",
            ["resourceOwnerId"] = coordinate.ResourceOwnerId,
            ["resourceKey"] = coordinate.ResourceKey,
            ["allocationId"] = allocationId
        };

    private static string CapacityFingerprint(
        ResourceOwnerAuthorityEntry owner,
        ResourceCoordinate coordinate,
        int turn,
        int incarnation,
        int memoryAmount,
        int relicAmount,
        JsonObject? memory,
        JsonObject? relic)
    {
        using var builder = new ResourceFingerprintBuilder(
            "shining-blessing-reroll-capacity-v1");
        builder.Append(owner.AuthorityFingerprint);
        ResourceStateContract.AppendCoordinate(builder, coordinate);
        builder.Append(turn);
        builder.Append(incarnation);
        builder.Append(memoryAmount);
        builder.Append(relicAmount);
        foreach (var id in ReadStrings(memory?["sourceCardIds"]))
            builder.Append(id);
        foreach (var id in ReadStrings(relic?["sourceCardIds"]))
            builder.Append(id);
        return builder.Build();
    }

    private static string SourceFingerprint(
        string purpose,
        ResourceOwnerAuthorityEntry owner,
        ResourceCoordinate coordinate,
        int turn,
        int incarnation,
        decimal amount,
        IReadOnlyList<string> sourceCardIds)
    {
        using var builder = new ResourceFingerprintBuilder(
            "shining-blessing-reroll-allocation-v1");
        builder.Append(purpose);
        builder.Append(owner.AuthorityFingerprint);
        ResourceStateContract.AppendCoordinate(builder, coordinate);
        builder.Append(turn);
        builder.Append(incarnation);
        builder.Append(amount);
        foreach (var sourceCardId in sourceCardIds)
            builder.Append(sourceCardId);
        return builder.Build();
    }

    private static bool ExistingEffectStateMatchesPackage(
        JsonObject? existing,
        JsonObject candidate,
        int preparedAtTurn,
        int currentIncarnation) =>
        existing != null &&
        ReadNonNegativeInt(existing["sourcePackagePreparedAtTurn"]) == preparedAtTurn &&
        ReadNonNegativeInt(existing["currentIncarnation"]) == currentIncarnation &&
        ReadNonNegativeInt(existing["sourceCardCount"]) ==
        ReadNonNegativeInt(candidate["sourceCardCount"]) &&
        JsonNode.DeepEquals(existing["sourceCardIds"], candidate["sourceCardIds"]);

    private static bool ExistingAllocationMatches(
        ResourceStateEntry? entry,
        ResourceHistoryState history,
        ResourceCoordinate coordinate,
        int total,
        string capacityFingerprint,
        string memoryId,
        int memoryAmount,
        string? memoryFingerprint,
        string relicId,
        int relicAmount,
        string? relicFingerprint) =>
        entry != null &&
        entry.Coordinate == coordinate &&
        entry.State == ResourceLifecycleState.Active &&
        entry.Maximum == total &&
        entry.Current >= 0m &&
        entry.Current <= total &&
        string.Equals(
            entry.CapacityBinding.AuthorityFingerprint,
            capacityFingerprint,
            StringComparison.Ordinal) &&
        ExactGrantMatches(
            history,
            coordinate,
            memoryId,
            memoryAmount,
            memoryFingerprint) &&
        ExactGrantMatches(
            history,
            coordinate,
            relicId,
            relicAmount,
            relicFingerprint);

    private static bool ExactGrantMatches(
        ResourceHistoryState history,
        ResourceCoordinate coordinate,
        string allocationId,
        int expectedAmount,
        string? expectedFingerprint)
    {
        var matches = history.Transitions.Where(transition =>
                transition.Coordinate == coordinate &&
                transition.Operation == ResourceTransitionOperation.Gain &&
                string.Equals(
                    transition.OriginId,
                    allocationId,
                    StringComparison.Ordinal))
            .ToArray();
        if (expectedAmount == 0)
            return matches.Length == 0;

        return matches.Length == 1 &&
               matches[0].RequestedAmount == expectedAmount &&
               matches[0].AppliedAmount == expectedAmount &&
               matches[0].Outcome == ResourceTransitionOutcome.Applied &&
               string.Equals(
                   matches[0].SourceEvidence.AuthorityFingerprint,
                   expectedFingerprint,
                   StringComparison.Ordinal);
    }

    private static bool ExistingBindingMatches(
        JsonObject? entitlement,
        ResourceCoordinate coordinate,
        string allocationId,
        int expectedAmount)
    {
        if (expectedAmount == 0)
            return entitlement?[BindingProperty] == null;
        if (entitlement == null ||
            !TryReadBinding(
                entitlement[BindingProperty],
                out var existingCoordinate,
                out var existingAllocationId,
                out _))
        {
            return false;
        }

        return existingCoordinate == coordinate &&
               string.Equals(
                   existingAllocationId,
                   allocationId,
                   StringComparison.Ordinal);
    }

    private static IReadOnlyList<string> ReadStrings(JsonNode? node) =>
        node is JsonArray values
            ? values.OfType<JsonValue>()
                .Select(value => value.TryGetValue<string>(out var text) ? text : null)
                .Where(static value => !string.IsNullOrWhiteSpace(value))
                .Select(static value => value!)
                .ToArray()
            : Array.Empty<string>();

    private static int ReadNonNegativeInt(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<int>(out var result)
            ? Math.Max(0, result)
            : 0;

    private static Task<string?> ReadAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? fs.ReadFileAsync(path)
            : fs.ReadFileAsync(writeLease, path);

    private static JsonObject? ParseObject(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (json == null || string.IsNullOrWhiteSpace(json))
        {
            issues.Add(Issue(
                path,
                "shining_blessing_reroll_owner_root_missing",
                "present non-empty profile object root",
                json == null ? "missing" : "empty/whitespace"));
            return null;
        }
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                issues.Add(Issue(
                    path,
                    "shining_blessing_reroll_owner_root_invalid",
                    "strict JSON object root",
                    document.RootElement.ValueKind.ToString()));
                return null;
            }
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                path,
                issues,
                "shining_blessing_reroll_owner_duplicate_property");
            return issues.Count == 0
                ? JsonNode.Parse(document.RootElement.GetRawText())!.AsObject()
                : null;
        }
        catch (JsonException exception)
        {
            issues.Add(Issue(
                path,
                "shining_blessing_reroll_owner_root_invalid",
                "well-formed strict JSON object root",
                exception.GetType().Name));
            return null;
        }
    }

    private static CoordinatedStateWriteHelper.PlannedWrite Write(
        string path,
        string? previousJson,
        string nextJson) =>
        new(path, previousJson, nextJson, RequireCurrentBaseline: true);

    private static ShiningBlessingRerollResourceFilePlan Success(
        JsonObject afterImage,
        IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> writes) =>
        new(afterImage, writes, Array.Empty<ValidationIssue>());

    private static ShiningBlessingRerollResourceFilePlan Failure(
        IReadOnlyList<ValidationIssue> issues) =>
        new(null, Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(), issues);

    private static ShiningBlessingRerollResourceFilePlan Failure(
        string code,
        string expected,
        string actual) =>
        Failure(new[]
        {
            Issue(
                ResourceMaterializationContract.StatePath,
                code,
                expected,
                actual)
        });

    private static ValidationIssue Issue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "Shining blessing reroll resource materialization is invalid.",
            code: code,
            section: "ResourceMaterialization",
            expected: expected,
            actual: actual);

    private static ShiningBlessingRerollSpendFilePlan SpendFailure(
        string code,
        string expected,
        string actual) =>
        new(
            0,
            Array.Empty<CoordinatedStateWriteHelper.PlannedWrite>(),
            new[]
            {
                Issue(
                    ResourceMaterializationContract.StatePath,
                    code,
                    expected,
                    actual)
            });

    private sealed record AllocationAuthority(
        ResourceCoordinate? Coordinate,
        string? AllocationId,
        int Remaining,
        string? SourceFingerprint,
        ResourceDefinitionCatalog? Definitions,
        ResourceStateLedger? State,
        ResourceHistoryState? History,
        IReadOnlyDictionary<string, string?> BeforeImages,
        IReadOnlyList<ValidationIssue> Issues)
    {
        internal bool IsValid =>
            Coordinate != null && AllocationId != null &&
            SourceFingerprint != null && Definitions != null && State != null &&
            History != null && Issues.Count == 0;

        internal static AllocationAuthority Success(
            ResourceCoordinate coordinate,
            string allocationId,
            int remaining,
            string sourceFingerprint,
            ResourceDefinitionCatalog definitions,
            ResourceStateLedger state,
            ResourceHistoryState history,
            IReadOnlyDictionary<string, string?> beforeImages) =>
            new(
                coordinate,
                allocationId,
                remaining,
                sourceFingerprint,
                definitions,
                state,
                history,
                new ReadOnlyDictionary<string, string?>(
                    new Dictionary<string, string?>(beforeImages, StringComparer.Ordinal)),
                Array.Empty<ValidationIssue>());

        internal static AllocationAuthority Failure(ValidationIssue issue) =>
            Failure(new[] { issue });

        internal static AllocationAuthority Failure(
            IReadOnlyList<ValidationIssue> issues) =>
            new(
                null,
                null,
                0,
                null,
                null,
                null,
                null,
                new ReadOnlyDictionary<string, string?>(
                    new Dictionary<string, string?>(StringComparer.Ordinal)),
                issues);
    }
}
