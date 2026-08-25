using System.Globalization;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ResourceHistoryStateTests
{
    private const string FingerprintA =
        "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string FingerprintB =
        "sha256:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [Fact]
    public void ParseCanonical_BuildsImmutableChronologicalAndReplayIndexes()
    {
        var initialize = CreateInitialize();
        var spend = CreateOrdinary(
            transitionId: "transition_spend",
            operationId: "operation_spend",
            eventRef: "turn_2",
            operation: "spend",
            before: 5,
            after: 3,
            requested: 2,
            applied: 2,
            turn: 2);

        var result = Parse(CreateRoot(spend, initialize));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.NotNull(result.History);
        Assert.Equal(
            new[] { "transition_initialize", "transition_spend" },
            result.History.Transitions.Select(static transition => transition.TransitionId));
        Assert.True(result.History.TryResolveExactTransition(
            "transition_spend",
            out var resolved));
        Assert.Equal(3m, resolved!.AfterState!.Current);
        Assert.True(ResourceMaterializationContract.IsAuthorityFingerprint(
            result.History.Fingerprint));
    }

    [Fact]
    public void ParseCanonical_FingerprintIsOrderIndependentAndSemanticSensitive()
    {
        var initialize = CreateInitialize();
        var spend = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            5,
            3,
            2,
            2,
            2);
        var changed = (JsonObject)spend.DeepClone();
        changed["sourceEvidence"]!["authorityFingerprint"] = FingerprintB;

        var forward = Parse(CreateRoot(initialize, spend));
        var reversed = Parse(CreateRoot(spend, initialize));
        var mutated = Parse(CreateRoot(initialize, changed));

        Assert.Equal(forward.History!.Fingerprint, reversed.History!.Fingerprint);
        Assert.Equal(forward.History.ToCanonicalJson(), reversed.History.ToCanonicalJson());
        Assert.NotEqual(forward.History.Fingerprint, mutated.History!.Fingerprint);
    }

    [Fact]
    public void ParseCanonical_OrdersSameTurnContinuityByClientPhaseBeforeEventOrdinal()
    {
        var cost = CreateTransition(
            "transition_cost",
            "operation_cost",
            "turn_2_effect_2",
            "ordinary_action",
            "cost_alpha",
            "direct_cost",
            100,
            "spend",
            requested: 1,
            applied: 1,
            outcome: "applied",
            beforeState: CreateSnapshot(5, 5),
            afterState: CreateSnapshot(4, 5),
            turn: 2,
            executionSequence: 0);
        var outcome = CreateTransition(
            "transition_outcome",
            "operation_outcome",
            "turn_2",
            "ordinary_action",
            "outcome_alpha",
            "direct_outcome",
            100,
            "gain",
            requested: 1,
            applied: 1,
            outcome: "applied",
            beforeState: CreateSnapshot(4, 5),
            afterState: CreateSnapshot(5, 5),
            turn: 2,
            executionSequence: 1);

        var result = Parse(CreateRoot(outcome, CreateInitialize(), cost));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.Equal(
            new[] { "transition_initialize", "transition_cost", "transition_outcome" },
            result.History!.Transitions.Select(static transition => transition.TransitionId));
    }

    [Fact]
    public void ParseCanonical_UsesExecutionSequenceForDependencyConstrainedSamePhaseContinuity()
    {
        var prerequisite = CreateTransition(
            "transition_prerequisite",
            "operation_prerequisite",
            "turn_2_effect_2",
            "effect_component",
            "z_prerequisite",
            "effect_trigger",
            200,
            "spend",
            requested: 1,
            applied: 1,
            outcome: "applied",
            beforeState: CreateSnapshot(5, 5),
            afterState: CreateSnapshot(4, 5),
            turn: 2,
            executionSequence: 0);
        var dependent = CreateTransition(
            "transition_dependent",
            "operation_dependent",
            "turn_2_effect_3",
            "effect_component",
            "a_dependent",
            "effect_trigger",
            200,
            "gain",
            requested: 1,
            applied: 1,
            outcome: "applied",
            beforeState: CreateSnapshot(4, 5),
            afterState: CreateSnapshot(5, 5),
            turn: 2,
            executionSequence: 1);

        var result = Parse(CreateRoot(dependent, prerequisite, CreateInitialize()));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.Equal(
            new[]
            {
                "transition_initialize",
                "transition_prerequisite",
                "transition_dependent"
            },
            result.History!.Transitions.Select(static transition => transition.TransitionId));
    }

    [Fact]
    public void ParseCanonical_RejectsMissingNegativeAndDuplicateExecutionSequence()
    {
        var missing = CreateInitialize();
        missing.Remove("executionSequence");
        var negative = CreateInitialize();
        negative["executionSequence"] = -1;
        var duplicateCost = CreateTransition(
            "transition_duplicate_sequence_cost",
            "operation_duplicate_sequence_cost",
            "turn_2_effect_2",
            "ordinary_action",
            "cost_alpha",
            "direct_cost",
            100,
            "spend",
            1,
            1,
            "applied",
            CreateSnapshot(5, 5),
            CreateSnapshot(4, 5),
            turn: 2,
            executionSequence: 0);
        var duplicateOutcome = CreateTransition(
            "transition_duplicate_sequence_outcome",
            "operation_duplicate_sequence_outcome",
            "turn_2",
            "ordinary_action",
            "outcome_alpha",
            "direct_outcome",
            100,
            "gain",
            1,
            1,
            "applied",
            CreateSnapshot(4, 5),
            CreateSnapshot(5, 5),
            turn: 2,
            executionSequence: 0);

        var missingResult = Parse(CreateRoot(missing));
        var negativeResult = Parse(CreateRoot(negative));
        var duplicateResult = Parse(CreateRoot(
            CreateInitialize(),
            duplicateCost,
            duplicateOutcome));

        Assert.Contains(missingResult.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
        Assert.Contains(negativeResult.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
        Assert.Contains(duplicateResult.Issues, issue =>
            issue.Code == "resource_history_duplicate_execution_sequence");
    }

    [Fact]
    public void ParseCanonical_NormalizesEquivalentDecimalScaleInHistorySerialization()
    {
        var compactJson = CreateRoot(
            CreateInitialize(),
            CreateOrdinary(
                "transition_spend",
                "operation_spend",
                "turn_2",
                "spend",
                5,
                3,
                2,
                2,
                2)).ToJsonString();
        var scaledJson = compactJson
            .Replace("\"requestedAmount\":2", "\"requestedAmount\":2.00", StringComparison.Ordinal)
            .Replace("\"appliedAmount\":2", "\"appliedAmount\":2.0", StringComparison.Ordinal)
            .Replace("\"current\":3", "\"current\":3.00", StringComparison.Ordinal);

        var compact = ResourceHistoryState.ParseCanonical(
            compactJson,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        var scaled = ResourceHistoryState.ParseCanonical(
            scaledJson,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.True(compact.IsValid);
        Assert.True(scaled.IsValid);
        Assert.Equal(compact.History!.Fingerprint, scaled.History!.Fingerprint);
        Assert.Equal(compact.History.ToCanonicalJson(), scaled.History.ToCanonicalJson());
    }

    [Fact]
    public void ParseCanonical_DistinguishesMissingPristineFromPresentInvalidRoots()
    {
        var definitions = ResourceDefinitionCatalog.CreateBuiltIn();

        var missingAllowed = ResourceHistoryState.ParseCanonical(
            null,
            definitions,
            allowMissingPristine: true);
        var missingRequired = ResourceHistoryState.ParseCanonical(
            null,
            definitions,
            allowMissingPristine: false);

        Assert.True(missingAllowed.IsValid);
        Assert.True(missingAllowed.IsMissing);
        Assert.Empty(missingAllowed.History!.Transitions);
        Assert.Contains(missingRequired.Issues, issue =>
            issue.Code == "resource_history_root_missing");
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \r\n ")]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{broken")]
    [InlineData("{\"schemaVersion\":1,\"entries\":[],\"extra\":true}")]
    [InlineData("{\"schemaVersion\":1,\"entries\":[],\"entries\":[]}")]
    public void ParseCanonical_RejectsMalformedWrongRootUnknownAndDuplicateData(string json)
    {
        var result = ResourceHistoryState.ParseCanonical(
            json,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: true);

        Assert.False(result.IsValid);
        Assert.Null(result.History);
    }

    [Fact]
    public void ParseCanonical_RejectsUnknownAndDuplicateNestedTransitionFields()
    {
        var unknown = CreateInitialize();
        unknown["rawPath"] = "game_state/resources/resource_state.json";
        var duplicate = CreateInitialize().ToJsonString()
            .Replace(
                "\"operationId\":\"operation_initialize\"",
                "\"operationId\":\"operation_initialize\",\"operationId\":\"forged\"",
                StringComparison.Ordinal);
        var duplicateRoot = "{\"schemaVersion\":1,\"entries\":[" + duplicate + "]}";

        var unknownResult = Parse(CreateRoot(unknown));
        var duplicateResult = ResourceHistoryState.ParseCanonical(
            duplicateRoot,
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

        Assert.Contains(unknownResult.Issues, issue =>
            issue.Code == "resource_history_unknown_field");
        Assert.Contains(duplicateResult.Issues, issue =>
            issue.Code == "resource_history_duplicate_property");
    }

    [Theory]
    [InlineData("transition_initialize", "operation_other", "resource_history_duplicate_transition_id")]
    [InlineData("TRANSITION_INITIALIZE", "operation_other", "resource_history_confusable_transition_id")]
    [InlineData("transition_other", "operation_initialize", "resource_history_duplicate_operation_id")]
    [InlineData("transition_other", "OPERATION_INITIALIZE", "resource_history_confusable_operation_id")]
    public void ParseCanonical_RejectsDuplicateAndConfusableClientIdentities(
        string transitionId,
        string operationId,
        string expectedCode)
    {
        var initialize = CreateInitialize();
        var second = CreateOrdinary(
            transitionId,
            operationId,
            "turn_2",
            "spend",
            5,
            4,
            1,
            1,
            2);

        var result = Parse(CreateRoot(initialize, second));

        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ParseCanonical_RejectsDuplicateAndConfusableReceiptIdentity()
    {
        var initialize = CreateInitialize();
        var first = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            5,
            4,
            1,
            1,
            2,
            receiptId: "receipt_alpha");
        var second = CreateOrdinary(
            "transition_gain",
            "operation_gain",
            "turn_3",
            "gain",
            4,
            5,
            1,
            1,
            3,
            receiptId: "RECEIPT_ALPHA");

        var result = Parse(CreateRoot(initialize, first, second));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_confusable_receipt_id");
    }

    [Fact]
    public void ParseCanonical_RejectsConfusableCoordinatesEvenWhenBothChainsAreTerminal()
    {
        var initializeAlpha = CreateInitialize();
        var retireAlpha = CreateLifecycle(
            "transition_retire_alpha",
            "operation_retire_alpha",
            "turn_2_alpha",
            "retire",
            "active",
            null,
            2,
            sourceKind: "owner_lifecycle");
        var initializeConfusable = (JsonObject)CreateInitialize().DeepClone();
        initializeConfusable["transitionId"] = "transition_initialize_confusable";
        initializeConfusable["operationId"] = "operation_initialize_confusable";
        initializeConfusable["eventRef"] = "bootstrap_1_confusable";
        initializeConfusable["originId"] = "resource_bootstrap_confusable";
        initializeConfusable["coordinate"]!["resourceOwnerId"] = "ITEM_ALPHA";
        initializeConfusable["sourceEvidence"]!["sourceId"] = "resource_bootstrap_confusable";
        var retireConfusable = (JsonObject)retireAlpha.DeepClone();
        retireConfusable["transitionId"] = "transition_retire_confusable";
        retireConfusable["operationId"] = "operation_retire_confusable";
        retireConfusable["eventRef"] = "turn_2_confusable";
        retireConfusable["originId"] = "owner_confusable";
        retireConfusable["coordinate"]!["resourceOwnerId"] = "ITEM_ALPHA";
        retireConfusable["sourceEvidence"]!["sourceId"] = "owner_confusable";

        var result = Parse(CreateRoot(
            initializeAlpha,
            retireAlpha,
            initializeConfusable,
            retireConfusable));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_confusable_coordinate");
    }

    [Fact]
    public void ParseCanonical_RejectsBrokenBeforeAfterContinuity()
    {
        var initialize = CreateInitialize();
        var spend = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            before: 4,
            after: 3,
            requested: 1,
            applied: 1,
            turn: 2);

        var result = Parse(CreateRoot(initialize, spend));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_continuity_mismatch");
    }

    [Theory]
    [InlineData("spend", 5, 4, 2, 1, "applied")]
    [InlineData("gain", 5, 4, 1, 1, "applied")]
    [InlineData("spend", 5, 3, 1, 2, "applied")]
    [InlineData("spend", 5, 3, 2, 2, "clamped_maximum")]
    public void ParseCanonical_RejectsOperationArithmeticOrOutcomeMismatch(
        string operation,
        double before,
        double after,
        double requested,
        double applied,
        string outcome)
    {
        var transition = CreateOrdinary(
            "transition_invalid",
            "operation_invalid",
            "turn_2",
            operation,
            Convert.ToDecimal(before),
            Convert.ToDecimal(after),
            Convert.ToDecimal(requested),
            Convert.ToDecimal(applied),
            2);
        transition["outcome"] = outcome;

        var result = Parse(CreateRoot(CreateInitialize(), transition));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Fact]
    public void ParseCanonical_RejectsClampedRequestThatViolatesDefinitionQuantum()
    {
        var transition = CreateOrdinary(
            "transition_fractional_spend",
            "operation_fractional_spend",
            "turn_2",
            "spend",
            before: 1,
            after: 0,
            requested: 1.5m,
            applied: 1,
            turn: 2,
            maximum: 1);
        transition["outcome"] = "clamped_minimum";

        var result = Parse(CreateRoot(CreateInitialize(current: 1, maximum: 1), transition));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Fact]
    public void ParseCanonical_RejectsMalformedSourcePolicyAndReceiptBinding()
    {
        var transition = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            5,
            4,
            1,
            1,
            2,
            receiptId: " receipt_alpha ");
        transition["sourceEvidence"]!["authorityFingerprint"] = FingerprintA.ToUpperInvariant();
        transition["policyFingerprint"] = "not-a-fingerprint";

        var result = Parse(CreateRoot(CreateInitialize(), transition));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_source_invalid");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_policy_invalid");
        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_receipt_invalid");
    }

    [Fact]
    public void ParseCanonical_ValidatesFullSnapshotsAgainstDefinitionAndCapacityBinding()
    {
        var transition = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            5,
            4,
            1,
            1,
            2);
        transition["afterState"]!["maximum"] = 0;

        var result = Parse(CreateRoot(CreateInitialize(), transition));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_state_capacity_invalid");
    }

    [Theory]
    [InlineData("minimum", null, 5)]
    [InlineData("maximum", null, 4)]
    [InlineData("fixed", "3", 2)]
    public void ParseCanonical_RejectsInitializeCurrentThatContradictsStaticDefinitionPolicy(
        string initializationKind,
        string? fixedValue,
        int submittedCurrent)
    {
        var definitions = CreateStaticInitializationCatalog(
            initializationKind,
            fixedValue == null
                ? null
                : decimal.Parse(fixedValue, CultureInfo.InvariantCulture));

        var result = ResourceHistoryState.ParseCanonical(
            CreateRoot(CreateInitialize(submittedCurrent, maximum: 5)).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Fact]
    public void ParseCanonical_AcceptsSuspendResumeAndTerminalRetireEvidence()
    {
        var initialize = CreateInitialize();
        var suspend = CreateLifecycle(
            "transition_suspend",
            "operation_suspend",
            "turn_2",
            "suspend",
            "active",
            "suspended",
            turn: 2);
        var resume = CreateLifecycle(
            "transition_resume",
            "operation_resume",
            "turn_3",
            "resume",
            "suspended",
            "active",
            turn: 3);
        var retire = CreateLifecycle(
            "transition_retire",
            "operation_retire",
            "turn_4",
            "retire",
            "active",
            afterLifecycle: null,
            turn: 4,
            sourceKind: "owner_lifecycle");

        var result = Parse(CreateRoot(retire, resume, initialize, suspend));

        Assert.True(result.IsValid);
        Assert.True(result.History!.IsTerminal(Coordinate));
        Assert.Null(result.History.Transitions[^1].AfterState);
    }

    [Fact]
    public void ParseCanonical_AcceptsFullReconfigureSnapshotWithNewMaximumAndBinding()
    {
        var reconfigure = CreateTransition(
            "transition_reconfigure",
            "operation_reconfigure",
            "turn_2",
            "system_rule",
            "capacity_change_alpha",
            "registered_system_outcome",
            100,
            "reconfigure",
            0,
            0,
            "clamped_maximum",
            CreateSnapshot(5, 5),
            new JsonObject
            {
                ["current"] = 4,
                ["maximum"] = 4,
                ["capacityBinding"] = new JsonObject
                {
                    ["kind"] = "instance_fixed",
                    ["authorityKey"] = "capacity_reconfigured",
                    ["authorityFingerprint"] = FingerprintB
                },
                ["state"] = "active"
            },
            2,
            capacityDisposition: "clamp_to_new_maximum");

        var result = Parse(CreateRoot(CreateInitialize(), reconfigure));

        Assert.True(result.IsValid);
        var accepted = result.History!.Transitions[^1];
        Assert.Equal(4m, accepted.AfterState!.Maximum);
        Assert.Equal("capacity_reconfigured", accepted.AfterState.CapacityBinding.AuthorityKey);
    }

    [Fact]
    public void ParseCanonical_RejectsReconfigureWithoutExactDispositionOrWithArbitraryCurrent()
    {
        var missingDisposition = CreateReconfigure(
            beforeCurrent: 5,
            beforeMaximum: 5,
            afterCurrent: 4,
            afterMaximum: 4,
            disposition: "clamp_to_new_maximum");
        missingDisposition.Remove("capacityDisposition");
        var arbitraryCurrent = CreateReconfigure(
            beforeCurrent: 5,
            beforeMaximum: 5,
            afterCurrent: 3,
            afterMaximum: 6,
            disposition: "preserve");
        var nonCapacityDisposition = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            5,
            4,
            1,
            1,
            2);
        nonCapacityDisposition["capacityDisposition"] = "preserve";

        var missingResult = Parse(CreateRoot(CreateInitialize(), missingDisposition));
        var arbitraryResult = Parse(CreateRoot(CreateInitialize(), arbitraryCurrent));
        var ordinaryResult = Parse(CreateRoot(CreateInitialize(), nonCapacityDisposition));

        Assert.Contains(missingResult.Issues, issue =>
            issue.Code == "resource_history_capacity_disposition_invalid");
        Assert.Contains(arbitraryResult.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
        Assert.Contains(ordinaryResult.Issues, issue =>
            issue.Code == "resource_history_capacity_disposition_invalid");
    }

    [Fact]
    public void ParseCanonical_ValidatesExactRatioReconfigureEvidence()
    {
        var definitions = CreateStaticInitializationCatalog(
            "fixed",
            fixedValue: 3m);
        var exact = CreateReconfigure(
            beforeCurrent: 3,
            beforeMaximum: 6,
            afterCurrent: 2,
            afterMaximum: 4,
            disposition: "scale_ratio_exact");
        var inexact = CreateReconfigure(
            beforeCurrent: 3,
            beforeMaximum: 6,
            afterCurrent: 2,
            afterMaximum: 5,
            disposition: "scale_ratio_exact");

        var exactResult = ResourceHistoryState.ParseCanonical(
            CreateRoot(CreateInitialize(current: 3, maximum: 6), exact).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        var inexactResult = ResourceHistoryState.ParseCanonical(
            CreateRoot(CreateInitialize(current: 3, maximum: 6), inexact).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.True(exactResult.IsValid);
        Assert.Contains(inexactResult.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Fact]
    public void ParseCanonical_UsesUnroundedRationalEqualityForMaxScaleRatio()
    {
        var culture = CultureInfo.InvariantCulture;
        var quantum = decimal.Parse("0.0000000000000000000000000001", culture);
        var beforeCurrent = decimal.Parse("0.05", culture);
        var beforeMaximum = decimal.Parse("0.1", culture);
        var roundedFalsePositive = decimal.Parse(
            "0.0500000000000000000000000001",
            culture);
        var definitions = CreateStaticInitializationCatalog(
            "fixed",
            beforeCurrent,
            numericKind: "decimal",
            quantum: quantum);
        var invalid = CreateReconfigure(
            beforeCurrent,
            beforeMaximum,
            roundedFalsePositive,
            beforeMaximum,
            "scale_ratio_exact");
        var exact = CreateReconfigure(
            beforeCurrent,
            beforeMaximum,
            afterCurrent: decimal.Parse("0.1", culture),
            afterMaximum: decimal.Parse("0.2", culture),
            "scale_ratio_exact");

        var invalidResult = ResourceHistoryState.ParseCanonical(
            CreateRoot(
                CreateInitialize(beforeCurrent, beforeMaximum),
                invalid).ToJsonString(),
            definitions,
            allowMissingPristine: false);
        var exactResult = ResourceHistoryState.ParseCanonical(
            CreateRoot(
                CreateInitialize(beforeCurrent, beforeMaximum),
                exact).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.Contains(invalidResult.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
        Assert.True(
            exactResult.IsValid,
            string.Join(Environment.NewLine, exactResult.Issues.Select(issue => issue.Code)));
    }

    [Fact]
    public void ParseCanonical_RejectsOrdinaryMutationWhoseExactCandidateCannotFitDecimal()
    {
        var quantum = decimal.Parse(
            "0.0000000000000000000000000001",
            CultureInfo.InvariantCulture);
        var definitions = CreateStaticInitializationCatalog(
            "fixed",
            fixedValue: 10m,
            numericKind: "decimal",
            quantum: quantum);
        var gain = CreateOrdinary(
            "transition_gain_precision",
            "operation_gain_precision",
            "turn_2",
            "gain",
            before: 10m,
            after: 10m,
            requested: quantum,
            applied: quantum,
            turn: 2,
            maximum: 20m);

        var result = ResourceHistoryState.ParseCanonical(
            CreateRoot(CreateInitialize(current: 10m, maximum: 20m), gain).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Theory]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("79228162514264337593543950335")]
    public void ParseCanonical_RejectsClampWhoseRequestedCandidateCannotFitDecimal(
        string requestedToken)
    {
        var culture = CultureInfo.InvariantCulture;
        var quantum = decimal.Parse("0.0000000000000000000000000001", culture);
        var definitions = CreateStaticInitializationCatalog(
            "fixed",
            fixedValue: 10m,
            numericKind: "decimal",
            quantum: quantum);
        var gain = CreateOrdinary(
            "transition_gain_precision_clamp",
            "operation_gain_precision_clamp",
            "turn_2",
            "gain",
            before: 10m,
            after: 10m,
            requested: decimal.Parse(requestedToken, culture),
            applied: 0m,
            turn: 2,
            maximum: 10m);
        gain["outcome"] = "clamped_maximum";

        var result = ResourceHistoryState.ParseCanonical(
            CreateRoot(CreateInitialize(current: 10m, maximum: 10m), gain).ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Theory]
    [InlineData("spend", 1, 0, "clamped_minimum")]
    [InlineData("gain", 4, 5, "clamped_maximum")]
    public void ParseCanonical_AcceptsExactRequestedBoundaryCrossingAndAppliedClamp(
        string operation,
        int before,
        int after,
        string outcome)
    {
        var definitions = CreateStaticInitializationCatalog(
            "fixed",
            fixedValue: before);
        var transition = CreateOrdinary(
            $"transition_{operation}_exact_clamp",
            $"operation_{operation}_exact_clamp",
            "turn_2",
            operation,
            before,
            after,
            requested: 2,
            applied: 1,
            turn: 2,
            maximum: 5);
        transition["outcome"] = outcome;

        var result = ResourceHistoryState.ParseCanonical(
            CreateRoot(CreateInitialize(current: before, maximum: 5), transition)
                .ToJsonString(),
            definitions,
            allowMissingPristine: false);

        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Issues.Select(issue => issue.Code)));
    }

    [Fact]
    public void ParseCanonical_RejectsInitializationDirectlyIntoSuspendedState()
    {
        var initialize = CreateInitialize();
        initialize["afterState"]!["state"] = "suspended";

        var result = Parse(CreateRoot(initialize));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_transition_invalid");
    }

    [Fact]
    public void ParseCanonical_RejectsIncompleteRetireOrTransitionAfterTerminal()
    {
        var invalidRetire = CreateLifecycle(
            "transition_retire",
            "operation_retire",
            "turn_2",
            "retire",
            "active",
            "active",
            2,
            sourceKind: "owner_lifecycle");
        var incomplete = Parse(CreateRoot(CreateInitialize(), invalidRetire));

        var retire = CreateLifecycle(
            "transition_retire",
            "operation_retire",
            "turn_2",
            "retire",
            "active",
            null,
            2,
            sourceKind: "owner_lifecycle");
        var afterTerminal = CreateOrdinary(
            "transition_after_retire",
            "operation_after_retire",
            "turn_3",
            "gain",
            5,
            5,
            1,
            0,
            3);
        var continued = Parse(CreateRoot(CreateInitialize(), retire, afterTerminal));

        Assert.Contains(incomplete.Issues, issue =>
            issue.Code == "resource_history_terminal_invalid");
        Assert.Contains(continued.Issues, issue =>
            issue.Code == "resource_history_transition_after_terminal");
    }

    [Fact]
    public void ResolveReplay_ReturnsPriorEntryForExactSemanticsAndRejectsConflict()
    {
        var result = Parse(CreateRoot(
            CreateInitialize(),
            CreateOrdinary(
                "transition_spend",
                "operation_spend",
                "turn_2",
                "spend",
                5,
                3,
                2,
                2,
                2)));
        var exact = CreateReplayProbe(requestedAmount: 2, FingerprintA, FingerprintB);
        var changedAmount = CreateReplayProbe(requestedAmount: 1, FingerprintA, FingerprintB);
        var changedSource = CreateReplayProbe(requestedAmount: 2, FingerprintB, FingerprintB);
        var changedPolicy = CreateReplayProbe(requestedAmount: 2, FingerprintA, FingerprintA);
        var unknown = exact with { EventRef = "turn_99" };
        var changedSequence = exact with { ExecutionSequence = 1 };

        var exactResult = result.History!.ResolveReplay(exact);
        var amountResult = result.History.ResolveReplay(changedAmount);
        var sourceResult = result.History.ResolveReplay(changedSource);
        var policyResult = result.History.ResolveReplay(changedPolicy);
        var unknownResult = result.History.ResolveReplay(unknown);
        var sequenceResult = result.History.ResolveReplay(changedSequence);

        Assert.Equal(ResourceReplayDisposition.Exact, exactResult.Disposition);
        Assert.Equal("transition_spend", exactResult.Transition!.TransitionId);
        Assert.Equal(ResourceReplayDisposition.Conflict, amountResult.Disposition);
        Assert.Equal(ResourceReplayDisposition.Conflict, sourceResult.Disposition);
        Assert.Equal(ResourceReplayDisposition.Conflict, policyResult.Disposition);
        Assert.Equal(ResourceReplayDisposition.Conflict, sequenceResult.Disposition);
        Assert.All(
            new[] { amountResult, sourceResult, policyResult, sequenceResult },
            replay => Assert.Contains(replay.Issues, issue =>
                issue.Code == "resource_transition_conflicting_replay"));
        Assert.Equal(ResourceReplayDisposition.None, unknownResult.Disposition);
    }

    [Fact]
    public void ParseCanonical_RejectsPersistedExactReplayInsteadOfAppendingDuplicateHistory()
    {
        var first = CreateOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            "spend",
            5,
            3,
            2,
            2,
            2);
        var duplicate = (JsonObject)first.DeepClone();
        duplicate["transitionId"] = "transition_duplicate_replay";
        duplicate["operationId"] = "operation_duplicate_replay";

        var result = Parse(CreateRoot(CreateInitialize(), first, duplicate));

        Assert.Contains(result.Issues, issue =>
            issue.Code == "resource_history_duplicate_replay_entry");
    }

    [Fact]
    public void Append_ReturnsSameHistoryForReplayRejectsConflictAndAddsOneValidTransition()
    {
        var parsed = Parse(CreateRoot(CreateInitialize()));
        var existing = Assert.Single(parsed.History!.Transitions);
        var exactReplay = parsed.History.Append(existing, ResourceDefinitionCatalog.CreateBuiltIn());
        var conflictCandidate = existing with
        {
            TransitionId = "transition_conflict",
            OperationId = "operation_conflict",
            RequestedAmount = 1m
        };
        var conflict = parsed.History.Append(
            conflictCandidate,
            ResourceDefinitionCatalog.CreateBuiltIn());
        var spend = CreateTypedOrdinary(
            "transition_spend",
            "operation_spend",
            "turn_2",
            ResourceTransitionOperation.Spend,
            before: 5,
            after: 4,
            requested: 1,
            applied: 1,
            turn: 2);
        var appended = parsed.History.Append(spend, ResourceDefinitionCatalog.CreateBuiltIn());

        Assert.True(exactReplay.IsValid);
        Assert.True(exactReplay.IsReplay);
        Assert.Same(parsed.History, exactReplay.History);
        Assert.False(conflict.IsValid);
        Assert.Null(conflict.History);
        Assert.True(appended.IsValid);
        Assert.False(appended.IsReplay);
        Assert.Equal(2, appended.History!.Transitions.Count);
        Assert.NotEqual(parsed.History.Fingerprint, appended.History.Fingerprint);
    }

    [Fact]
    public void ParseCanonical_DoesNotTruncateLongValidHistory()
    {
        const int transitionCount = 1_025;
        var transitions = new List<JsonObject>(transitionCount)
        {
            CreateInitialize(current: 0, maximum: transitionCount)
        };
        for (var index = 1; index < transitionCount; index++)
        {
            transitions.Add(CreateOrdinary(
                "transition_" + index.ToString("D4", CultureInfo.InvariantCulture),
                "operation_" + index.ToString("D4", CultureInfo.InvariantCulture),
                "turn_" + (index + 1).ToString("D4", CultureInfo.InvariantCulture),
                "gain",
                index - 1,
                index,
                1,
                1,
                index + 1,
                maximum: transitionCount));
        }

        var result = ResourceHistoryState.ParseCanonical(
            CreateRoot(transitions.ToArray()).ToJsonString(),
            CreateStaticInitializationCatalog("minimum", fixedValue: null),
            allowMissingPristine: false);

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(static issue =>
                    $"{issue.Code}: {issue.FilePath} expected={issue.Expected} actual={issue.Actual}")));
        Assert.Equal(transitionCount, result.History!.Transitions.Count);
        Assert.Equal(transitionCount - 1, result.History.Transitions[^1].AfterState!.Current);
    }

    [Fact]
    public void HistoryTransitions_AreDefensiveAndCannotMutateIndexesOrFingerprint()
    {
        var result = Parse(CreateRoot(CreateInitialize()));
        var transitions = result.History!.Transitions;
        var fingerprint = result.History.Fingerprint;

        Assert.False(transitions is ResourceTransition[]);
        Assert.True(result.History.TryResolveExactTransition(
            transitions[0].TransitionId,
            out var resolved));
        Assert.Equal(transitions[0], resolved);
        Assert.Equal(fingerprint, result.History.Fingerprint);
    }

    [Fact]
    public void ValidateStateAgreement_AcceptsExactLatestSnapshotAndChronology()
    {
        var history = Parse(CreateRoot(
            CreateInitialize(),
            CreateOrdinary(
                "transition_spend",
                "operation_spend",
                "turn_2",
                "spend",
                5,
                3,
                2,
                2,
                2))).History!;
        var state = ParseState(CreateStateRoot(
            current: 3,
            lastTransitionId: "transition_spend",
            lastEventRef: "turn_2",
            lastTurn: 2));

        var issues = history.ValidateStateAgreement(state);

        Assert.Empty(issues);
    }

    [Fact]
    public void ValidateStateAgreement_RejectsLatestSnapshotAndChronologyMismatch()
    {
        var history = Parse(CreateRoot(
            CreateInitialize(),
            CreateOrdinary(
                "transition_spend",
                "operation_spend",
                "turn_2",
                "spend",
                5,
                3,
                2,
                2,
                2))).History!;
        var wrongSnapshot = ParseState(CreateStateRoot(
            current: 4,
            lastTransitionId: "transition_spend",
            lastEventRef: "turn_2",
            lastTurn: 2));
        var wrongChronology = ParseState(CreateStateRoot(
            current: 3,
            lastTransitionId: "transition_other",
            lastEventRef: "turn_2",
            lastTurn: 2));

        var snapshotIssues = history.ValidateStateAgreement(wrongSnapshot);
        var chronologyIssues = history.ValidateStateAgreement(wrongChronology);

        Assert.Contains(snapshotIssues, issue =>
            issue.Code == "resource_state_history_snapshot_mismatch");
        Assert.Contains(chronologyIssues, issue =>
            issue.Code == "resource_state_history_chronology_mismatch");
    }

    [Fact]
    public void ValidateStateAgreement_RejectsLiveStateAfterRetireAndMissingNonterminalState()
    {
        var liveState = ParseState(CreateStateRoot(
            current: 5,
            lastTransitionId: "transition_initialize",
            lastEventRef: "bootstrap_1",
            lastTurn: 1));
        var retiredHistory = Parse(CreateRoot(
            CreateInitialize(),
            CreateLifecycle(
                "transition_retire",
                "operation_retire",
                "turn_2",
                "retire",
                "active",
                null,
                2,
                sourceKind: "owner_lifecycle"))).History!;
        var nonterminalHistory = Parse(CreateRoot(CreateInitialize())).History!;
        var emptyState = ParseState(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray()
        });

        var terminalIssues = retiredHistory.ValidateStateAgreement(liveState);
        var missingIssues = nonterminalHistory.ValidateStateAgreement(emptyState);

        Assert.Contains(terminalIssues, issue =>
            issue.Code == "resource_state_history_terminal_mismatch");
        Assert.Contains(missingIssues, issue =>
            issue.Code == "resource_state_history_live_state_missing");
    }

    private static ResourceCoordinate Coordinate { get; } = new(
        "mortal_world",
        ResourceOwnerKind.Item,
        "item_alpha",
        "charges");

    private static ResourceHistoryStateResult Parse(JsonObject root) =>
        ResourceHistoryState.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);

    private static ResourceStateLedger ParseState(JsonObject root)
    {
        var result = ResourceStateContract.ParseCanonical(
            root.ToJsonString(),
            ResourceDefinitionCatalog.CreateBuiltIn(),
            allowMissingPristine: false);
        Assert.True(result.IsValid);
        return result.Ledger!;
    }

    private static JsonObject CreateRoot(params JsonObject[] entries) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(entries.Select(static entry => entry.DeepClone()).ToArray())
    };

    private static JsonObject CreateStateRoot(
        decimal current,
        string lastTransitionId,
        string lastEventRef,
        int lastTurn) => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray(new JsonObject
        {
            ["realm"] = "mortal_world",
            ["ownerKind"] = "item",
            ["resourceOwnerId"] = "item_alpha",
            ["resourceKey"] = "charges",
            ["current"] = current,
            ["maximum"] = 5,
            ["capacityBinding"] = new JsonObject
            {
                ["kind"] = "instance_fixed",
                ["authorityKey"] = "capacity_item_alpha",
                ["authorityFingerprint"] = FingerprintA
            },
            ["state"] = "active",
            ["chronology"] = new JsonObject
            {
                ["createdAtTurn"] = 1,
                ["createdEventRef"] = "bootstrap_1",
                ["lastTransitionId"] = lastTransitionId,
                ["lastEventRef"] = lastEventRef,
                ["lastTransitionTurn"] = lastTurn
            }
        })
    };

    private static JsonObject CreateInitialize(decimal current = 5, decimal maximum = 5) =>
        CreateTransition(
            "transition_initialize",
            "operation_initialize",
            "bootstrap_1",
            "system_rule",
            "resource_bootstrap",
            "registered_system_outcome",
            100,
            "initialize",
            requested: 0,
            applied: 0,
            outcome: "applied",
            beforeState: null,
            afterState: CreateSnapshot(current, maximum, "active"),
            turn: 1,
            capacityDisposition: "initialize_from_definition");

    private static ResourceDefinitionCatalog CreateStaticInitializationCatalog(
        string initializationKind,
        decimal? fixedValue,
        string numericKind = "integer",
        decimal quantum = 1m)
    {
        var initialization = new JsonObject
        {
            ["kind"] = initializationKind
        };
        if (fixedValue.HasValue)
            initialization["value"] = fixedValue.Value;

        var definition = new JsonObject
        {
            ["resourceKey"] = "charges",
            ["definitionVersion"] = 1,
            ["displayName"] = "Заряды",
            ["numericKind"] = numericKind,
            ["unit"] = "charge",
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
            ["allowedOwnerKinds"] = new JsonArray("item"),
            ["allowedOperations"] = new JsonArray("spend", "gain"),
            ["defaultFloorPolicy"] = "reject_below_minimum",
            ["defaultCapPolicy"] = "clamp_to_maximum",
            ["visibility"] = "owner_visible",
            ["materialization"] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["definitionId"] = "definition_charges_test",
                ["seal"] = "seal_charges_test",
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
        Assert.True(result.IsValid);
        return result.Catalog!;
    }

    private static JsonObject CreateOrdinary(
        string transitionId,
        string operationId,
        string eventRef,
        string operation,
        decimal before,
        decimal after,
        decimal requested,
        decimal applied,
        int turn,
        string? receiptId = null,
        decimal? maximum = null) =>
        CreateTransition(
            transitionId,
            operationId,
            eventRef,
            "ordinary_action",
            "action_alpha",
            "direct_outcome",
            100,
            operation,
            requested,
            applied,
            "applied",
            CreateSnapshot(before, maximum ?? Math.Max(before, after)),
            CreateSnapshot(after, maximum ?? Math.Max(before, after)),
            turn,
            receiptId);

    private static JsonObject CreateLifecycle(
        string transitionId,
        string operationId,
        string eventRef,
        string operation,
        string beforeLifecycle,
        string? afterLifecycle,
        int turn,
        string sourceKind = "system_rule") =>
        CreateTransition(
            transitionId,
            operationId,
            eventRef,
            sourceKind,
            "owner_alpha",
            "registered_system_outcome",
            100,
            operation,
            0,
            0,
            "applied",
            CreateSnapshot(5, 5, beforeLifecycle),
            afterLifecycle == null ? null : CreateSnapshot(5, 5, afterLifecycle),
            turn);

    private static JsonObject CreateTransition(
        string transitionId,
        string operationId,
        string eventRef,
        string originKind,
        string originId,
        string phase,
        int priority,
        string operation,
        decimal requested,
        decimal applied,
        string outcome,
        JsonObject? beforeState,
        JsonObject? afterState,
        int turn,
        string? receiptId = null,
        int executionSequence = 0,
        string? capacityDisposition = null) => new()
    {
        ["transitionId"] = transitionId,
        ["operationId"] = operationId,
        ["eventRef"] = eventRef,
        ["originKind"] = originKind,
        ["originId"] = originId,
        ["phase"] = phase,
        ["priority"] = priority,
        ["executionSequence"] = executionSequence,
        ["coordinate"] = new JsonObject
        {
            ["realm"] = "mortal_world",
            ["ownerKind"] = "item",
            ["resourceOwnerId"] = "item_alpha",
            ["resourceKey"] = "charges"
        },
        ["operation"] = operation,
        ["requestedAmount"] = requested,
        ["appliedAmount"] = applied,
        ["outcome"] = outcome,
        ["capacityDisposition"] = capacityDisposition,
        ["beforeState"] = beforeState,
        ["afterState"] = afterState,
        ["sourceEvidence"] = new JsonObject
        {
            ["sourceKind"] = originKind,
            ["sourceId"] = originId,
            ["authorityFingerprint"] = FingerprintA
        },
        ["policyFingerprint"] = FingerprintB,
        ["receiptId"] = receiptId,
        ["turn"] = turn
    };

    private static JsonObject CreateReconfigure(
        decimal beforeCurrent,
        decimal beforeMaximum,
        decimal afterCurrent,
        decimal afterMaximum,
        string disposition) =>
        CreateTransition(
            "transition_reconfigure",
            "operation_reconfigure",
            "turn_2",
            "system_rule",
            "capacity_change_alpha",
            "registered_system_outcome",
            100,
            "reconfigure",
            0,
            0,
            disposition == "clamp_to_new_maximum" && afterCurrent < beforeCurrent
                ? "clamped_maximum"
                : "applied",
            CreateSnapshot(beforeCurrent, beforeMaximum),
            new JsonObject
            {
                ["current"] = afterCurrent,
                ["maximum"] = afterMaximum,
                ["capacityBinding"] = new JsonObject
                {
                    ["kind"] = "instance_fixed",
                    ["authorityKey"] = "capacity_reconfigured",
                    ["authorityFingerprint"] = FingerprintB
                },
                ["state"] = "active"
            },
            turn: 2,
            capacityDisposition: disposition);

    private static JsonObject CreateSnapshot(
        decimal current,
        decimal maximum,
        string lifecycle = "active") => new()
    {
        ["current"] = current,
        ["maximum"] = maximum,
        ["capacityBinding"] = new JsonObject
        {
            ["kind"] = "instance_fixed",
            ["authorityKey"] = "capacity_item_alpha",
            ["authorityFingerprint"] = FingerprintA
        },
        ["state"] = lifecycle
    };

    private static ResourceReplayProbe CreateReplayProbe(
        decimal requestedAmount,
        string sourceFingerprint,
        string policyFingerprint) => new(
            EventRef: "turn_2",
            OriginKind: "ordinary_action",
            OriginId: "action_alpha",
            Coordinate,
            ResourceTransitionOperation.Spend,
            RequestedAmount: requestedAmount,
            ResourceMutationPhase.DirectOutcome,
            Priority: 100,
            ExecutionSequence: 0,
            CapacityDisposition: null,
            new ResourceSourceEvidence(
                "ordinary_action",
                "action_alpha",
                sourceFingerprint),
            PolicyFingerprint: policyFingerprint,
            ReceiptId: null);

    private static ResourceTransition CreateTypedOrdinary(
        string transitionId,
        string operationId,
        string eventRef,
        ResourceTransitionOperation operation,
        decimal before,
        decimal after,
        decimal requested,
        decimal applied,
        int turn) => new(
            transitionId,
            operationId,
            eventRef,
            "ordinary_action",
            "action_alpha",
            ResourceMutationPhase.DirectOutcome,
            100,
            0,
            Coordinate,
            operation,
            requested,
            applied,
            ResourceTransitionOutcome.Applied,
            CapacityDisposition: null,
            CreateTypedSnapshot(before, Math.Max(before, after)),
            CreateTypedSnapshot(after, Math.Max(before, after)),
            new ResourceSourceEvidence(
                "ordinary_action",
                "action_alpha",
                FingerprintA),
            FingerprintB,
            ReceiptId: null,
            turn);

    private static ResourceStateSnapshot CreateTypedSnapshot(
        decimal current,
        decimal maximum) => new(
            current,
            maximum,
            new ResourceCapacityBinding(
                ResourceCapacityKind.InstanceFixed,
                "capacity_item_alpha",
                FingerprintA),
            ResourceLifecycleState.Active);
}
