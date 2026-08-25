using System.Globalization;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceMutationReducerTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void Reduce_AppliesSequentialClampWithoutPreSumming()
    {
        var harness = CreateHarness("durability", maximum: 10m);

        harness.Apply(Mutation(
            ResourceOperation.Damage,
            1m,
            sequence: 1,
            originId: "scratch",
            resourceKey: "durability"));
        var clamped = harness.Apply(
            Mutation(
                ResourceOperation.Restore,
                5m,
                sequence: 2,
                originId: "repair",
                resourceKey: "durability"));
        var final = harness.Apply(
            Mutation(
                ResourceOperation.Damage,
                4m,
                sequence: 3,
                originId: "impact",
                resourceKey: "durability"));

        Assert.Equal((9m, 10m),
            (clamped.Transition!.BeforeState!.Current, clamped.Transition.AfterState!.Current));
        Assert.Equal(ResourceTransitionOutcome.ClampedMaximum, clamped.Transition.Outcome);
        Assert.Equal((10m, 6m),
            (final.Transition!.BeforeState!.Current, final.Transition.AfterState!.Current));
        Assert.Equal(6m, harness.Current);
    }

    [Fact]
    public void Reduce_RejectsBelowMinimumWithoutMutatingLedgerOrWorkingHistory()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        var beforeFingerprint = harness.Ledger.Fingerprint;
        var beforePending = harness.History.PendingCount;

        var result = ResourceMutationReducer.Reduce(
            harness.Ledger,
            harness.History,
            Mutation(ResourceOperation.Spend, 6m, sequence: 1),
            harness.Definitions);

        Assert.Null(result.WorkingLedger);
        Assert.Null(result.Transition);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_mutation_below_minimum");
        Assert.Equal(beforeFingerprint, harness.Ledger.Fingerprint);
        Assert.Equal(beforePending, harness.History.PendingCount);
        Assert.Equal(5m, harness.Current);
    }

    [Fact]
    public void Reduce_ReturnsNewLedgerWithoutMutatingInputView()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        var before = harness.Ledger;

        var result = ResourceMutationReducer.Reduce(
            before,
            harness.History,
            Mutation(ResourceOperation.Spend, 1m, sequence: 1),
            harness.Definitions);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.NotSame(before, result.WorkingLedger);
        Assert.Equal(5m, before.Resolve(Coordinate("charges")).Current);
        Assert.Equal(4m, result.WorkingLedger!.Resolve(Coordinate("charges")).Current);
    }

    [Fact]
    public void Reduce_ClampsMinimumAndEmitsBoundaryOnlyOnCrossing()
    {
        var harness = CreateHarness("durability", maximum: 10m);

        var first = harness.Apply(
            Mutation(
                ResourceOperation.Damage,
                12m,
                sequence: 1,
                originId: "impact",
                resourceKey: "durability"));
        var second = harness.Apply(
            Mutation(
                ResourceOperation.Damage,
                1m,
                sequence: 2,
                originId: "aftermath",
                resourceKey: "durability"));

        Assert.Equal(0m, harness.Current);
        Assert.Equal(10m, first.Transition!.AppliedAmount);
        Assert.Equal(ResourceTransitionOutcome.ClampedMinimum, first.Transition.Outcome);
        Assert.Equal(
            new[] { "resource_damaged", "resource_depleted" },
            first.Events.Select(static value => value.EventKind));
        Assert.Equal(
            new[] { "resource_damaged" },
            second.Events.Select(static value => value.EventKind));
    }

    [Fact]
    public void Reduce_ClampsMaximumAndEmitsFilledOnlyWhenReachedFromBelow()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        harness.Apply(Mutation(ResourceOperation.Spend, 2m, sequence: 1, originId: "use"));

        var result = harness.Apply(
            Mutation(ResourceOperation.Gain, 4m, sequence: 2, originId: "reload"));

        Assert.Equal(5m, harness.Current);
        Assert.Equal(2m, result.Transition!.AppliedAmount);
        Assert.Equal(ResourceTransitionOutcome.ClampedMaximum, result.Transition.Outcome);
        Assert.Equal(
            new[] { "resource_gained", "resource_filled" },
            result.Events.Select(static value => value.EventKind));
    }

    [Fact]
    public void Reduce_RejectsQuantumMismatchAndExactDecimalScaleLoss()
    {
        var quarter = CreateHarness(
            CreateDefinitionCatalog("focus", "decimal", 0.25m, 1m, "maximum"),
            "focus",
            maximum: 1m);
        var maxScaleQuantum = decimal.Parse(
            "0.0000000000000000000000000001",
            CultureInfo.InvariantCulture);
        var maxScale = CreateHarness(
            CreateDefinitionCatalog("focus", "decimal", maxScaleQuantum, 10m, "maximum"),
            "focus",
            maximum: 10m);

        var mismatch = ResourceMutationReducer.Reduce(
            quarter.Ledger,
            quarter.History,
            Mutation(ResourceOperation.Spend, 0.1m, sequence: 1, resourceKey: "focus"),
            quarter.Definitions);
        var scaleLoss = ResourceMutationReducer.Reduce(
            maxScale.Ledger,
            maxScale.History,
            Mutation(ResourceOperation.Spend, maxScaleQuantum, sequence: 1, resourceKey: "focus"),
            maxScale.Definitions);

        Assert.Contains(mismatch.Issues, issue =>
            issue.Code == "resource_mutation_amount_invalid");
        Assert.Contains(scaleLoss.Issues, issue =>
            issue.Code == "resource_mutation_arithmetic_inexact");
        Assert.Equal(1m, quarter.Current);
        Assert.Equal(10m, maxScale.Current);
    }

    [Fact]
    public void Reduce_RejectsOverflowBeforeConsideringClamp()
    {
        var harness = CreateHarness(
            CreateDefinitionCatalog(
                "focus",
                "decimal",
                1m,
                decimal.MaxValue,
                "maximum"),
            "focus",
            decimal.MaxValue);

        var result = ResourceMutationReducer.Reduce(
            harness.Ledger,
            harness.History,
            Mutation(ResourceOperation.Gain, 1m, sequence: 1, resourceKey: "focus"),
            harness.Definitions);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_mutation_arithmetic_inexact");
        Assert.Equal(decimal.MaxValue, harness.Current);
    }

    [Fact]
    public void Reduce_ExactReplayReturnsPriorTransitionWithoutEventsOrAppend()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        var mutation = Mutation(ResourceOperation.Spend, 2m, sequence: 1);
        var first = harness.Apply(mutation);
        var pending = harness.History.PendingCount;

        var replay = ResourceMutationReducer.Reduce(
            harness.Ledger,
            harness.History,
            mutation,
            harness.Definitions);

        Assert.Null(replay.Transition);
        Assert.Same(first.Transition, replay.ReplayTransition);
        Assert.Empty(replay.Events);
        Assert.Equal(pending, harness.History.PendingCount);
        Assert.Equal(3m, replay.WorkingLedger!.Resolve(Coordinate("charges")).Current);
    }

    [Fact]
    public void Reduce_ConflictingReplayFailsWithoutAppend()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        harness.Apply(Mutation(ResourceOperation.Spend, 2m, sequence: 1));
        var pending = harness.History.PendingCount;

        var conflict = ResourceMutationReducer.Reduce(
            harness.Ledger,
            harness.History,
            Mutation(ResourceOperation.Spend, 1m, sequence: 1),
            harness.Definitions);

        Assert.Null(conflict.WorkingLedger);
        Assert.Contains(conflict.Issues, issue =>
            issue.Code == "resource_transition_conflicting_replay");
        Assert.Equal(pending, harness.History.PendingCount);
        Assert.Equal(3m, harness.Current);
    }

    [Fact]
    public void Reduce_RejectsOperationNotAllowedByDefinitionAndSuspendedState()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        var unsupported = ResourceMutationReducer.Reduce(
            harness.Ledger,
            harness.History,
            Mutation(ResourceOperation.Damage, 1m, sequence: 1),
            harness.Definitions);
        harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Suspend,
            sequence: 1,
            sourceKind: "owner_lifecycle"));
        var suspended = ResourceMutationReducer.Reduce(
            harness.Ledger,
            harness.History,
            Mutation(ResourceOperation.Spend, 1m, sequence: 2),
            harness.Definitions);

        Assert.Contains(unsupported.Issues, issue =>
            issue.Code == "resource_mutation_operation_forbidden");
        Assert.Contains(suspended.Issues, issue =>
            issue.Code == "resource_mutation_state_inactive");
    }

    [Theory]
    [InlineData("minimum", "0")]
    [InlineData("maximum", "10")]
    [InlineData("fixed", "3")]
    public void ApplyCapacityTransition_InitializesFromSealedStaticPolicy(
        string initializationKind,
        string expectedToken)
    {
        var expected = decimal.Parse(expectedToken, CultureInfo.InvariantCulture);
        var definitions = CreateDefinitionCatalog(
            "focus", "integer", 1m, 10m, initializationKind,
            initializationKind == "fixed" ? expected : null);
        var harness = CreateEmptyHarness(definitions, "focus");

        var result = harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Initialize,
            sequence: 0,
            resourceKey: "focus",
            maximum: 10m,
            disposition: ResourceCurrentDisposition.InitializeFromDefinition,
            definitions: definitions));

        Assert.Equal(expected, result.WorkingLedger!.Resolve(Coordinate("focus")).Current);
        Assert.Equal(ResourceCapacityDisposition.InitializeFromDefinition,
            result.Transition!.CapacityDisposition);
    }

    [Fact]
    public void ApplyCapacityTransition_RegisteredInitializationRequiresExactResolvedValue()
    {
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            "shining_return_scope_alpha",
            "focus");
        var definitions = CreateDefinitionCatalog(
            "focus",
            "decimal",
            0.25m,
            10m,
            "registered_formula",
            allowedOwnerKind: "afterlife_scope");
        var formulaInput = new ShiningReturnGachaFormulaInput(
            new ResourceFormulaOwner(
                coordinate.Realm,
                coordinate.OwnerKind,
                coordinate.ResourceOwnerId),
            FingerprintA,
            RadianceTier: 3,
            ReturnCycleId: "return_cycle_2");
        var expected = ResourceCapacityFormulaCatalog.Evaluate(
            ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1,
            formulaInput);
        Assert.True(expected.IsValid, string.Join(Environment.NewLine, expected.Issues));
        var missing = CreateEmptyHarness(definitions, coordinate);
        var accepted = CreateEmptyHarness(definitions, coordinate);

        var failure = ResourceMutationReducer.ApplyCapacityTransition(
            missing.Ledger,
            missing.History,
            Capacity(
                ResourceCapacityOperation.Initialize,
                sequence: 0,
                resourceKey: "focus",
                maximum: 10m,
                disposition: ResourceCurrentDisposition.InitializeFromDefinition,
                coordinate: coordinate,
                definitions: definitions,
                includeInitialization: false),
            definitions);
        var success = accepted.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Initialize,
            sequence: 0,
            resourceKey: "focus",
            maximum: 10m,
            disposition: ResourceCurrentDisposition.InitializeFromDefinition,
            coordinate: coordinate,
            definitions: definitions,
            initializationFormulaInput: formulaInput));

        Assert.Contains(failure.Issues, issue =>
            issue.Code == "resource_capacity_initialization_invalid");
        Assert.Equal(expected.Value, success.WorkingLedger!.Resolve(coordinate).Current);
    }

    [Fact]
    public void ApplyCapacityTransition_RegisteredCapacityMayEqualDefinitionMinimum()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        var coordinate = new ResourceCoordinate(
            "chaos_sea",
            ResourceOwnerKind.AfterlifeActor,
            "guardian_zero_attempts",
            "gacha_attempts");
        var formulaInput = new GuardianReturnGachaFormulaInput(
            new ResourceFormulaOwner(
                coordinate.Realm,
                coordinate.OwnerKind,
                coordinate.ResourceOwnerId),
            FingerprintA,
            Reputation: -51,
            AbodePower: 0,
            FounderExtraCharges: 0,
            ReturnCycleId: "return_cycle_zero_attempts");
        var harness = CreateEmptyHarness(definitions, coordinate);

        var result = harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Initialize,
            sequence: 0,
            resourceKey: "gacha_attempts",
            maximum: 0m,
            disposition: ResourceCurrentDisposition.InitializeFromDefinition,
            coordinate: coordinate,
            definitions: definitions,
            capacityFormulaInput: formulaInput));

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        var entry = result.WorkingLedger!.Resolve(coordinate);
        Assert.Equal(0m, entry.Current);
        Assert.Equal(0m, entry.Maximum);
    }

    [Fact]
    public void ResolveCapacity_RejectsStaleRegisteredCapacityAndInitializationBindings()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        Assert.True(definitions.TryResolveExact("gacha_attempts", out var definition));
        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            "shining_return_scope_alpha",
            "gacha_attempts");
        var formulaInput = new ShiningReturnGachaFormulaInput(
            new ResourceFormulaOwner(
                coordinate.Realm,
                coordinate.OwnerKind,
                coordinate.ResourceOwnerId),
            FingerprintA,
            RadianceTier: 3,
            ReturnCycleId: "return_cycle_2");

        var staleCapacity = ResolvedResourceCapacity.Resolve(
            definition!,
            coordinate,
            new RegisteredFormulaCapacityInput(formulaInput),
            instanceAuthorityKey: null,
            includeInitialization: true,
            initializationFormulaInput: formulaInput,
            expectedCapacityFingerprint: FingerprintB);
        var initializationDefinitions = CreateDefinitionCatalog(
            "focus",
            "integer",
            1m,
            10m,
            "registered_formula",
            allowedOwnerKind: "afterlife_scope");
        Assert.True(initializationDefinitions.TryResolveExact(
            "focus",
            out var initializationDefinition));
        var initializationCoordinate = coordinate with { ResourceKey = "focus" };
        var staleInitialization = ResolvedResourceCapacity.Resolve(
            initializationDefinition!,
            initializationCoordinate,
            new InstanceFixedCapacityInput(
                formulaInput.Owner,
                Maximum: 10m,
                CapacityAuthorityFingerprint: FingerprintA),
            instanceAuthorityKey: "capacity_alpha",
            includeInitialization: true,
            initializationFormulaInput: formulaInput,
            expectedInitializationFingerprint: FingerprintB);

        Assert.Contains(staleCapacity.Issues, issue =>
            issue.Code == "resource_capacity_stale_input");
        Assert.Contains(staleInitialization.Issues, issue =>
            issue.Code == "resource_initialization_stale_input");
        Assert.Null(staleCapacity.Capacity);
        Assert.Null(staleInitialization.Capacity);
    }

    [Fact]
    public void ApplyCapacityTransition_PreserveClampAndScaleAreExactAndSequential()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        harness.Apply(Mutation(ResourceOperation.Spend, 2m, sequence: 1));

        var preserve = harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Reconfigure,
            sequence: 2,
            maximum: 10m,
            disposition: ResourceCurrentDisposition.Preserve,
            fingerprint: FingerprintB));
        var clamp = harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Reconfigure,
            sequence: 3,
            maximum: 2m,
            disposition: ResourceCurrentDisposition.ClampToNewMaximum,
            fingerprint: FingerprintA));
        var scale = harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Reconfigure,
            sequence: 4,
            maximum: 4m,
            disposition: ResourceCurrentDisposition.ScaleRatioExact,
            fingerprint: FingerprintB));

        Assert.Equal(3m, preserve.Transition!.AfterState!.Current);
        Assert.Equal(2m, clamp.Transition!.AfterState!.Current);
        Assert.Equal(ResourceTransitionOutcome.ClampedMaximum, clamp.Transition.Outcome);
        Assert.Equal(4m, scale.Transition!.AfterState!.Current);
        Assert.Equal(4m, harness.Current);
    }

    [Fact]
    public void ApplyCapacityTransition_ReturnsNewLedgerWithoutMutatingInputView()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        var before = harness.Ledger;

        var result = ResourceMutationReducer.ApplyCapacityTransition(
            before,
            harness.History,
            Capacity(
                ResourceCapacityOperation.Reconfigure,
                sequence: 1,
                maximum: 10m,
                disposition: ResourceCurrentDisposition.Preserve,
                fingerprint: FingerprintB),
            harness.Definitions);

        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        Assert.NotSame(before, result.WorkingLedger);
        Assert.Equal(5m, before.Resolve(Coordinate("charges")).Maximum);
        Assert.Equal(10m, result.WorkingLedger!.Resolve(Coordinate("charges")).Maximum);
    }

    [Fact]
    public void ApplyCapacityTransition_RejectsInexactRatioAndNoChangeReconfigure()
    {
        var definitions = CreateDefinitionCatalog("focus", "decimal", 0.25m, 1m, "fixed", 0.25m);
        var inexact = CreateHarness(definitions, "focus", maximum: 1m);
        var noChange = CreateHarness("charges", maximum: 5m);

        var ratio = ResourceMutationReducer.ApplyCapacityTransition(
            inexact.Ledger,
            inexact.History,
            Capacity(
                ResourceCapacityOperation.Reconfigure,
                sequence: 1,
                resourceKey: "focus",
                maximum: 0.75m,
                disposition: ResourceCurrentDisposition.ScaleRatioExact,
                fingerprint: FingerprintB,
                definitions: definitions),
            definitions);
        var unchanged = ResourceMutationReducer.ApplyCapacityTransition(
            noChange.Ledger,
            noChange.History,
            Capacity(
                ResourceCapacityOperation.Reconfigure,
                sequence: 1,
                maximum: 5m,
                disposition: ResourceCurrentDisposition.Preserve,
                bindingAuthorityKey: "capacity_alpha_0"),
            noChange.Definitions);

        Assert.Contains(ratio.Issues, issue =>
            issue.Code == "resource_capacity_ratio_inexact");
        Assert.Contains(unchanged.Issues, issue =>
            issue.Code == "resource_capacity_unchanged");
    }

    [Fact]
    public void ApplyCapacityTransition_SuspendResumeRetireIsTerminalAndImmutable()
    {
        var harness = CreateHarness("charges", maximum: 5m);

        harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Suspend,
            sequence: 1,
            sourceKind: "owner_lifecycle"));
        Assert.Equal(ResourceLifecycleState.Suspended,
            harness.Ledger.Resolve(Coordinate("charges")).State);
        harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Resume,
            sequence: 2,
            sourceKind: "owner_lifecycle"));
        harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Retire,
            sequence: 3,
            sourceKind: "owner_lifecycle"));

        Assert.False(harness.Ledger.TryResolve(Coordinate("charges"), out _));
        var afterTerminal = ResourceMutationReducer.ApplyCapacityTransition(
            harness.Ledger,
            harness.History,
            Capacity(
                ResourceCapacityOperation.Initialize,
                sequence: 4,
                maximum: 5m,
                disposition: ResourceCurrentDisposition.InitializeFromDefinition),
            harness.Definitions);
        Assert.Contains(afterTerminal.Issues, issue =>
            issue.Code == "resource_capacity_terminal_coordinate");
    }

    [Fact]
    public void HistoryWorkingSet_SeedsOnceAppendsIncrementallyAndFreezesOnce()
    {
        var harness = CreateHarness("charges", maximum: 5m);
        harness.Apply(Mutation(ResourceOperation.Spend, 1m, sequence: 1, originId: "use_a"));
        harness.Apply(Mutation(ResourceOperation.Gain, 1m, sequence: 2, originId: "gain_a"));

        var frozen = harness.History.Freeze(harness.Definitions);

        Assert.True(frozen.IsValid, string.Join(Environment.NewLine, frozen.Issues));
        Assert.Equal(1, harness.History.BaselineSeedCount);
        Assert.Equal(3, harness.History.PendingCount);
        Assert.Equal(3, harness.History.IncrementalAppendCount);
        Assert.Equal(1, harness.History.FreezeCount);
        Assert.Equal(3, frozen.History!.Transitions.Count);
        Assert.Empty(frozen.History.ValidateStateAgreement(harness.Ledger.Freeze()));
        Assert.Throws<InvalidOperationException>(() => harness.History.Freeze(harness.Definitions));
    }

    private static Harness CreateHarness(string resourceKey, decimal maximum)
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();
        return CreateHarness(definitions, resourceKey, maximum);
    }

    private static Harness CreateHarness(
        ResourceDefinitionCatalog definitions,
        string resourceKey,
        decimal maximum)
    {
        var harness = CreateEmptyHarness(definitions, resourceKey);
        harness.ApplyCapacity(Capacity(
            ResourceCapacityOperation.Initialize,
            sequence: 0,
            resourceKey: resourceKey,
            maximum: maximum,
            disposition: ResourceCurrentDisposition.InitializeFromDefinition,
            definitions: definitions));
        return harness;
    }

    private static Harness CreateEmptyHarness(
        ResourceDefinitionCatalog definitions,
        string resourceKey)
        => CreateEmptyHarness(definitions, Coordinate(resourceKey));

    private static Harness CreateEmptyHarness(
        ResourceDefinitionCatalog definitions,
        ResourceCoordinate coordinate)
    {
        var emptyHistory = ResourceHistoryState.ParseCanonical(
            "{\"schemaVersion\":1,\"entries\":[]}",
            definitions,
            allowMissingPristine: false).History!;
        return new Harness(
            definitions,
            coordinate,
            ResourceWorkingLedger.Empty,
            new ResourceHistoryWorkingSet(emptyHistory));
    }

    private static AuthorizedResourceMutation Mutation(
        ResourceOperation operation,
        decimal amount,
        int sequence,
        string originId = "action_alpha",
        string resourceKey = "charges") =>
        new(
            TransitionId: "transition_" + originId,
            OperationId: "operation_" + originId,
            EventRef: "turn_2:" + originId,
            OriginKind: "ordinary_action",
            OriginId: originId,
            Coordinate: Coordinate(resourceKey),
            Operation: operation,
            Amount: amount,
            Phase: ResourceMutationPhase.DirectOutcome,
            Priority: 100,
            ExecutionSequence: sequence,
            PolicyBinding: new ResourcePolicyBinding(
                FloorPolicy: (operation is ResourceOperation.Damage or ResourceOperation.Spend) &&
                    resourceKey == "charges"
                        ? ResourceBoundPolicy.RejectBelowMinimum
                        : ResourceBoundPolicy.ClampToMinimum,
                CapPolicy: ResourceBoundPolicy.ClampToMaximum,
                FingerprintA),
            Dependencies: Array.Empty<ResourceOperationKey>(),
            SourceEvidence: new ResourceSourceEvidence(
                "narrative_outcome",
                originId,
                FingerprintA),
            ReceiptId: null,
            Turn: 2);

    private static AuthorizedResourceCapacityTransition Capacity(
        ResourceCapacityOperation operation,
        int sequence,
        string resourceKey = "charges",
        decimal? maximum = null,
        ResourceCurrentDisposition? disposition = null,
        string sourceKind = "setting_materialization",
        string fingerprint = FingerprintA,
        ResourceCoordinate? coordinate = null,
        string? bindingAuthorityKey = null,
        ResourceDefinitionCatalog? definitions = null,
        ResourceFormulaInput? capacityFormulaInput = null,
        ResourceFormulaInput? initializationFormulaInput = null,
        bool includeInitialization = true)
    {
        var exactCoordinate = coordinate ?? Coordinate(resourceKey);
        ResolvedResourceCapacity? resolvedCapacity = null;
        var policyFingerprint = fingerprint;
        if (maximum.HasValue)
        {
            var catalog = definitions ?? ResourceDefinitionCatalog.CreateBuiltIn();
            Assert.True(catalog.TryResolveExact(resourceKey, out var definition));
            var owner = new ResourceFormulaOwner(
                exactCoordinate.Realm,
                exactCoordinate.OwnerKind,
                exactCoordinate.ResourceOwnerId);
            ResourceCapacityInput capacityInput = definition!.CapacityPolicy.Kind switch
            {
                ResourceCapacityKind.DefinitionFixed =>
                    new DefinitionFixedCapacityInput(owner),
                ResourceCapacityKind.InstanceFixed =>
                    new InstanceFixedCapacityInput(owner, maximum.Value, fingerprint),
                ResourceCapacityKind.RegisteredFormula when capacityFormulaInput != null =>
                    new RegisteredFormulaCapacityInput(capacityFormulaInput),
                _ => throw new InvalidOperationException(
                    "A registered capacity formula requires one typed formula input.")
            };
            var resolution = ResolvedResourceCapacity.Resolve(
                definition,
                exactCoordinate,
                capacityInput,
                bindingAuthorityKey ??
                    (definition.CapacityPolicy.Kind == ResourceCapacityKind.InstanceFixed
                        ? "capacity_alpha_" + sequence
                        : null),
                includeInitialization:
                    operation == ResourceCapacityOperation.Initialize && includeInitialization,
                initializationFormulaInput: initializationFormulaInput);
            Assert.True(resolution.IsValid, string.Join(Environment.NewLine, resolution.Issues));
            resolvedCapacity = resolution.Capacity;
            policyFingerprint = operation == ResourceCapacityOperation.Initialize
                ? resolvedCapacity!.Initialization?.AuthorityFingerprint ??
                    resolvedCapacity.Binding.AuthorityFingerprint
                : resolvedCapacity!.Binding.AuthorityFingerprint;
        }

        return new AuthorizedResourceCapacityTransition(
            TransitionId: $"transition_capacity_{sequence}_{operation.ToString().ToLowerInvariant()}",
            OperationId: $"operation_capacity_{sequence}_{operation.ToString().ToLowerInvariant()}",
            EventRef: $"turn_2:capacity:{sequence}",
            OriginKind: sourceKind,
            OriginId: "capacity_alpha_" + sequence,
            Coordinate: exactCoordinate,
            Operation: operation,
            ResolvedCapacity: resolvedCapacity,
            CurrentDisposition: disposition,
            Phase: ResourceMutationPhase.RegisteredSystemOutcome,
            Priority: 50,
            ExecutionSequence: sequence,
            SourceEvidence: new ResourceSourceEvidence(
                sourceKind,
                "capacity_alpha_" + sequence,
                fingerprint),
            PolicyFingerprint: policyFingerprint,
            ReceiptId: null,
            Turn: sequence == 0 ? 1 : 2);
    }

    private static ResourceCoordinate Coordinate(string resourceKey) => new(
        "mortal_world",
        ResourceOwnerKind.Item,
        "item_alpha",
        resourceKey);

    private static ResourceDefinitionCatalog CreateDefinitionCatalog(
        string resourceKey,
        string numericKind,
        decimal quantum,
        decimal maximum,
        string initializationKind,
        decimal? initialValue = null,
        string allowedOwnerKind = "item")
    {
        var initialization = new JsonObject { ["kind"] = initializationKind };
        if (initialValue.HasValue)
            initialization["value"] = initialValue.Value;
        if (initializationKind == "registered_formula")
            initialization["formulaKey"] = ResourceCapacityFormulaCatalog.AfterlifeReturnGachaAttemptsV1;
        var definition = new JsonObject
        {
            ["resourceKey"] = resourceKey,
            ["definitionVersion"] = 1,
            ["displayName"] = "Focus",
            ["numericKind"] = numericKind,
            ["unit"] = "point",
            ["quantum"] = quantum,
            ["minimumPolicy"] = new JsonObject
            {
                ["kind"] = "definition_fixed",
                ["value"] = 0
            },
            ["capacityPolicy"] = new JsonObject
            {
                ["kind"] = "instance_fixed"
            },
            ["initializationPolicy"] = initialization,
            ["allowedOwnerKinds"] = new JsonArray(allowedOwnerKind),
            ["allowedOperations"] = new JsonArray("damage", "restore", "spend", "gain"),
            ["defaultFloorPolicy"] = "clamp_to_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible",
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitionId"] = "definition_" + resourceKey,
                ["seal"] = "seal_" + resourceKey,
                ["createdAtTurn"] = 1,
                ["createdEventRef"] = "bootstrap_1"
            }
        };
        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["definitions"] = new JsonArray(definition)
        };
        var result = ResourceDefinitionCatalog.ParseCanonical(
            root.ToJsonString(),
            allowMissingPristine: false);
        Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
        return result.Catalog!;
    }

    private sealed class Harness
    {
        internal Harness(
            ResourceDefinitionCatalog definitions,
            ResourceCoordinate coordinate,
            ResourceWorkingLedger ledger,
            ResourceHistoryWorkingSet history)
        {
            Definitions = definitions;
            Coordinate = coordinate;
            Ledger = ledger;
            History = history;
        }

        internal ResourceDefinitionCatalog Definitions { get; }
        internal ResourceCoordinate Coordinate { get; }
        internal ResourceWorkingLedger Ledger { get; private set; }
        internal ResourceHistoryWorkingSet History { get; }
        internal decimal Current => Ledger.Resolve(Coordinate).Current;

        internal ResourceMutationResult Apply(AuthorizedResourceMutation mutation)
        {
            var result = ResourceMutationReducer.Reduce(
                Ledger,
                History,
                mutation,
                Definitions);
            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
            Ledger = result.WorkingLedger!;
            return result;
        }

        internal ResourceCapacityResult ApplyCapacity(
            AuthorizedResourceCapacityTransition transition)
        {
            var result = ResourceMutationReducer.ApplyCapacityTransition(
                Ledger,
                History,
                transition,
                Definitions);
            Assert.True(result.IsValid, string.Join(Environment.NewLine, result.Issues));
            Ledger = result.WorkingLedger!;
            return result;
        }
    }
}
