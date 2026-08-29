using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// T064-A RED contract for append-only opportunity receipts.  It is deliberately a
/// pure state contract: T070 supplies signed before-images and is the sole publisher.
/// </summary>
public sealed class MortalWoundOpportunityReceiptStateTests
{
    private const string RootPath = "game_state/wounds/wound_opportunity_receipts.json";
    private const string RowPath = RootPath + ".receipts[0]";

    [Fact]
    public void PureApi_ExposesOnlyStrictParserSerializerFingerprintAndPureAppendHistoryAgreement()
    {
        var type = RequiredType();
        AssertStatic(type, "Parse", 2, "MortalWoundOpportunityReceiptParseResult");
        AssertStatic(type, "SerializeCanonical", 1, nameof(String));
        AssertStatic(type, "ComputeReceiptFingerprint", 1, nameof(String));
        var consume = ExactStatic(type, "PlanConsumeAndAppend", 4, "MortalWoundOpportunityReceiptConsumePlanResult");
        Assert.Equal("MortalWoundOccurrenceState", consume.GetParameters()[0].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptState", consume.GetParameters()[1].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptDraft", consume.GetParameters()[2].ParameterType.Name);
        Assert.Equal(nameof(WoundHistoryState), consume.GetParameters()[3].ParameterType.Name);
        AssertStatic(type, "ValidateConsumedOccurrenceAgreement", 2, "IReadOnlyList`1");

        Assert.DoesNotContain(type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance), method =>
            method.Name.Contains("File", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Lease", StringComparison.OrdinalIgnoreCase) ||
            method.Name.Contains("Write", StringComparison.OrdinalIgnoreCase) ||
            method.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("FileSystemManager", StringComparison.Ordinal) ||
                parameter.ParameterType.Name.Contains("CanonicalWriteLease", StringComparison.Ordinal)));
    }

    [Fact]
    public void Parse_RootRowsAndNestedCoordinatesAreClosedCompleteAndRejectRawDuplicateProperties()
    {
        foreach (var json in new string?[] { null, string.Empty, " ", "[]", "null", "{" })
            AssertInvalid(Parse(json), RootPath, "mortal_wound_opportunity_receipt_invalid_root");

        foreach (var field in new[] { "schemaVersion", "nextOrdinal", "receipts" })
        {
            var root = Root();
            root.Remove(field);
            AssertInvalid(Parse(root), RootPath + "." + field, "mortal_wound_opportunity_receipt_missing_field");
        }

        var unknownRoot = Root();
        unknownRoot["legacyReceipts"] = new JsonArray();
        AssertInvalid(Parse(unknownRoot), RootPath + ".legacyReceipts", "mortal_wound_opportunity_receipt_unknown_field");

        foreach (var field in RequiredRowFields)
        {
            var row = Row();
            row.Remove(field);
            AssertInvalid(Parse(Root(row)), RowPath + "." + field, "mortal_wound_opportunity_receipt_missing_field");
        }

        var unknown = Row();
        unknown["history"] = new JsonObject();
        AssertInvalid(Parse(Root(unknown)), RowPath + ".history", "mortal_wound_opportunity_receipt_unknown_field");

        var baseline = Root(Row());
        var receiptId = baseline["receipts"]![0]!["receiptId"]!.GetValue<string>();
        var duplicate = baseline.ToJsonString().Replace(
            $"\"receiptId\":\"{receiptId}\"",
            $"\"receiptId\":\"{receiptId}\",\"receiptId\":\"forged\"",
            StringComparison.Ordinal);
        AssertInvalid(Parse(duplicate), RowPath + ".receiptId", "mortal_wound_opportunity_receipt_duplicate_property");
    }

    [Fact]
    public void Parse_RejectsWrongSchemaCollectionAndCoordinateScalarTypesBeforeReplayLookup()
    {
        foreach (var schema in new JsonNode?[] { 0, 2, "1", null })
        {
            var root = Root();
            root["schemaVersion"] = schema?.DeepClone();
            AssertInvalid(Parse(root), RootPath + ".schemaVersion", "mortal_wound_opportunity_receipt_invalid_field");
        }

        var decimalSchema = Root().ToJsonString().Replace(
            "\"schemaVersion\":1",
            "\"schemaVersion\":1.0",
            StringComparison.Ordinal);
        AssertInvalid(Parse(decimalSchema), RootPath + ".schemaVersion", "mortal_wound_opportunity_receipt_invalid_field");

        var nonArray = Root();
        nonArray["receipts"] = new JsonObject();
        AssertInvalid(Parse(nonArray), RootPath + ".receipts", "mortal_wound_opportunity_receipt_invalid_field");

        var nonObject = Root();
        nonObject["receipts"] = new JsonArray("forged");
        AssertInvalid(Parse(nonObject), RowPath, "mortal_wound_opportunity_receipt_invalid_field");

        foreach (var field in new[] { "ordinal", "turn", "sourceTurn", "producerCandidateOrdinal", "producerCandidateCount" })
        {
            var row = Row();
            row[field] = "1";
            AssertInvalid(Parse(Root(row)), RowPath + "." + field, "mortal_wound_opportunity_receipt_invalid_field");
        }
    }

    [Fact]
    public void Parse_RequiresContiguousCanonicalOrdinalsAndTwentyThousandRowLimit()
    {
        var second = Row(ordinal: 2, decisionSeed: "decision_2", producerOperationKey: "batch_2");
        AssertValid(Parse(Root(3, Row(), second)));

        foreach (var invalid in new[]
                 {
                     Root(1, Row()),
                     Root(3, Row(), Row(ordinal: 3,
                         decisionSeed: "decision_2", producerOperationKey: "batch_2")),
                     Root(3, second, Row())
                 })
            AssertInvalid(Parse(invalid), RootPath, "mortal_wound_opportunity_receipt_ordinal_discontinuity");

        var oversized = Enumerable.Range(1, 20_001).Select(index => Row(
            ordinal: index,
            decisionSeed: $"decision_{index}", producerOperationKey: $"batch_{index}")).ToArray();
        AssertInvalid(Parse(Root(20_002, oversized)), RootPath + ".receipts", "mortal_wound_opportunity_receipt_limit_exceeded");
    }

    [Fact]
    public void Parse_NoneAndMaterializeHaveClosedConditionalWoundAndTransitionCoordinates()
    {
        AssertValid(Parse(Root(Row(decision: "none", woundId: null, transitionId: null))));
        AssertValid(Parse(Root(Row(decision: "materialize", woundId: "wound_created", transitionId: "transition_create"))));

        foreach (var row in new[]
                 {
                     Row(decision: "none", woundId: "wound_created", transitionId: null),
                     Row(decision: "none", woundId: null, transitionId: "transition_create"),
                     Row(decision: "materialize", woundId: null, transitionId: "transition_create"),
                     Row(decision: "materialize", woundId: "wound_created", transitionId: null),
                     Row(decision: "decline", woundId: null, transitionId: null)
                 })
            AssertInvalid(Parse(Root(row)), RowPath, "mortal_wound_opportunity_receipt_decision_coordinate_mismatch");
    }

    [Fact]
    public void Parse_RetainsEveryConsumedSourceCoordinateBatchFieldAndFingerprint()
    {
        foreach (var field in new[]
                 {
                     "sourceSessionId", "sourceRequestId", "sourceTurn", "producerOperationKey",
                     "producerCandidateOrdinal", "producerCandidateCount", "sourceResultFingerprint",
                     "candidateFingerprint", "occurrenceFingerprint", "opportunityAuthorityFingerprint",
                     "eventSemanticFingerprint", "decisionFingerprint", "receiptFingerprint"
                 })
        {
            var row = Row();
            row.Remove(field);
            AssertInvalid(Parse(Root(row)), RowPath + "." + field, "mortal_wound_opportunity_receipt_missing_field");
        }

        foreach (var invalid in new JsonNode?[] { -1, "0", 1.5, null })
        {
            var row = Row();
            row["producerCandidateOrdinal"] = invalid?.DeepClone();
            AssertInvalid(Parse(Root(row)), RowPath + ".producerCandidateOrdinal", "mortal_wound_opportunity_receipt_invalid_field");
        }
    }

    [Fact]
    public void Parse_RejectsAnyTamperedRecomputedReceiptFingerprint()
    {
        var row = Row();
        row["receiptFingerprint"] = ExternalFingerprint("forged-receipt");

        AssertInvalid(Parse(Root(row)), RowPath + ".receiptFingerprint",
            "mortal_wound_opportunity_receipt_fingerprint_mismatch");
    }

    [Fact]
    public void Parse_RecomputesTheConsumedOccurrencePublicLinksBeforeAcceptingAReceipt()
    {
        AssertValid(Parse(Root(Row())));

        var forgedOpportunity = Row();
        forgedOpportunity["opportunityId"] = "mortal_wound_occurrence_forged";
        RecomputeReceiptFingerprint(forgedOpportunity);
        AssertInvalidAny(Parse(Root(forgedOpportunity)), RowPath + ".opportunityId");

        var forgedOccurrence = Row();
        forgedOccurrence["occurrenceFingerprint"] = ExternalFingerprint("forged-occurrence");
        RecomputeReceiptFingerprint(forgedOccurrence);
        AssertInvalidAny(Parse(Root(forgedOccurrence)), RowPath + ".occurrenceFingerprint");

        var forgedReceiptId = Row();
        forgedReceiptId["receiptId"] = "mortal_wound_receipt_forged";
        RecomputeReceiptFingerprint(forgedReceiptId);
        AssertInvalidAny(Parse(Root(forgedReceiptId)), RowPath + ".receiptId");

        var forgedOperation = Row();
        forgedOperation["operationKey"] = "wound_operation_forged";
        RecomputeReceiptIdentity(forgedOperation);
        AssertInvalidAny(Parse(Root(forgedOperation)), RowPath + ".operationKey");
    }

    [Fact]
    public void ValidateConsumedOccurrenceAgreement_RejectsDuplicateTupleAcrossRootsAndAnyBatchAuthorityMismatch()
    {
        var method = ExactStatic(RequiredType(), "ValidateConsumedOccurrenceAgreement", 2);
        Assert.Equal("MortalWoundOccurrenceState", method.GetParameters()[0].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptState", method.GetParameters()[1].ParameterType.Name);

        Assert.Equal("IReadOnlyList`1", method.ReturnType.Name);

        var pending = ParsedOccurrenceState(guaranteed: false);
        var emptyReceipts = EmptyReceiptState();
        Assert.Empty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(pending, emptyReceipts));

        var consumed = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            pending,
            emptyReceipts,
            Draft(Assert.Single(pending.Occurrences), "none"),
            EmptyHistory());
        Assert.Equal("appended", consumed.Disposition);
        Assert.Empty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
            consumed.OccurrenceState!, consumed.ReceiptState!));
        Assert.NotEmpty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
            pending, consumed.ReceiptState!));
    }

    [Fact]
    public void ValidateConsumedOccurrenceAgreement_AcceptsMixedPartitionAndRejectsEverySharedBatchAuthorityMismatch()
    {
        var pending = ParsedOccurrenceBatchState();
        var before = EmptyReceiptState();
        var first = Assert.Single(pending.Occurrences, value => value.ProducerCandidateOrdinal == 0);
        var consumed = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            pending,
            before,
            Draft(first, "none"),
            EmptyHistory());
        Assert.Equal("appended", consumed.Disposition);
        Assert.Single(consumed.OccurrenceState!.Occurrences);
        Assert.Single(consumed.ReceiptState!.Receipts);
        Assert.Empty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
            consumed.OccurrenceState,
            consumed.ReceiptState));

        var canonical = JsonNode.Parse(MortalWoundOpportunityReceiptState.SerializeCanonical(consumed.ReceiptState))!.AsObject();
        foreach (var mutation in new Action<JsonObject>[]
                 {
                     row => row["sourceSessionId"] = "source_session_changed",
                     row => row["sourceRequestId"] = "source_request_changed",
                     row => row["sourceTurn"] = 99,
                     row => row["producerCandidateCount"] = 3,
                     row => row["sourceResultFingerprint"] = ExternalFingerprint("changed-source-result")
                 })
        {
            var changedRoot = canonical.DeepClone().AsObject();
            var changedRow = changedRoot["receipts"]![0]!.AsObject();
            mutation(changedRow);
            RecomputeReceiptFingerprint(changedRow);
            var changed = MortalWoundOpportunityReceiptState.Parse(changedRoot.ToJsonString(), RootPath);
            Assert.True(changed.IsValid, string.Join(Environment.NewLine, changed.Issues));
            Assert.NotEmpty(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(
                consumed.OccurrenceState,
                changed.State!));
        }
    }

    [Fact]
    public void Parse_RejectsExactAndConfusableReceiptOpportunityDecisionAndBatchTupleCollisions()
    {
        var original = Row();
        var duplicateReceipt = original.DeepClone().AsObject();
        duplicateReceipt["ordinal"] = 2;
        RecomputeReceiptFingerprint(duplicateReceipt);
        AssertInvalid(Parse(Root(3, original, duplicateReceipt)), RootPath + ".receipts[1].receiptId", "mortal_wound_opportunity_receipt_duplicate_id");

        var duplicateOpportunity = original.DeepClone().AsObject();
        duplicateOpportunity["ordinal"] = 2;
        duplicateOpportunity["decisionFingerprint"] = ExternalFingerprint("decision:none:decision_2");
        duplicateOpportunity["operationKey"] = "wound_operation_" +
            duplicateOpportunity["decisionFingerprint"]!.GetValue<string>()["sha256:".Length..];
        RecomputeReceiptIdentity(duplicateOpportunity);
        AssertInvalid(Parse(Root(3, Row(), duplicateOpportunity)), RootPath + ".receipts[1].opportunityId", "mortal_wound_opportunity_receipt_duplicate_opportunity");

        var latin = Row(ordinal: 1, decisionSeed: "decision_o", producerOperationKey: "batch_o");
        var cyrillicReceipt = Row(ordinal: 2, decisionSeed: "decision_other", producerOperationKey: "batch_other");
        cyrillicReceipt["receiptId"] = latin["receiptId"]!.GetValue<string>()
            .Replace("mortal", "mоrtal", StringComparison.Ordinal);
        RecomputeReceiptFingerprint(cyrillicReceipt);
        AssertInvalid(Parse(Root(3, latin, cyrillicReceipt)), RootPath + ".receipts[1].receiptId", "mortal_wound_opportunity_receipt_confusable_id");

        var cyrillicOpportunity = Row(ordinal: 2,
            decisionSeed: "decision_2", producerOperationKey: "batch_2");
        cyrillicOpportunity["opportunityId"] = latin["opportunityId"]!.GetValue<string>().Replace("o", "о", StringComparison.Ordinal);
        RecomputeReceiptFingerprint(cyrillicOpportunity);
        AssertInvalid(Parse(Root(3, latin, cyrillicOpportunity)), RootPath + ".receipts[1].opportunityId", "mortal_wound_opportunity_receipt_confusable_opportunity");

        var cyrillicOperation = Row(ordinal: 2,
            decisionSeed: "decision_о", producerOperationKey: "batch_2");
        cyrillicOperation["operationKey"] = latin["operationKey"]!.GetValue<string>()
            .Replace("wound_operation", "wоund_operation", StringComparison.Ordinal);
        RecomputeReceiptIdentity(cyrillicOperation);
        AssertInvalid(Parse(Root(3, latin, cyrillicOperation)), RootPath + ".receipts[1].operationKey", "mortal_wound_opportunity_receipt_confusable_operation");

        var sameTuple = Row(ordinal: 2,
            decisionSeed: "decision_2", producerOperationKey: "batch_1");
        sameTuple["candidateFingerprint"] = ExternalFingerprint("distinct-candidate:same-producer-tuple");
        ResealConsumedOccurrenceIdentity(sameTuple);
        AssertInvalid(Parse(Root(3, Row(), sameTuple)), RootPath + ".receipts[1]", "mortal_wound_opportunity_receipt_duplicate_batch_tuple");
    }

    [Fact]
    public void PlanConsumeAndAppend_IndexesOpportunityBeforeDecisionOperationAndReturnsTheDetachedOriginalForExactReplay()
    {
        var method = ExactStatic(RequiredType(), "PlanConsumeAndAppend", 4);
        Assert.Equal("MortalWoundOpportunityReceiptConsumePlanResult", method.ReturnType.Name);
        Assert.DoesNotContain(method.GetParameters(), parameter =>
            parameter.ParameterType == typeof(JsonNode) || parameter.ParameterType == typeof(JsonObject) ||
            parameter.ParameterType.Name.Contains("FileSystemManager", StringComparison.Ordinal));
        AssertClosedResult(method.ReturnType, "Disposition", "Issues", "OccurrenceState", "ReceiptState", "Receipt", "HistoryAgreement");

        // Semantics frozen for the typed draft: exact opportunity/binding/event/source/
        // batch/decision/transition equality is ExactReplay and returns the original
        // detached receipt with no append.  Any changed coordinate is Conflict even if
        // the decision operation key is new; opportunity lookup happens first.
    }

    [Fact]
    public void ReceiptDraft_IsClosedAndCarriesOnlyPublicOpportunityCorrelationInsteadOfDetachedOccurrenceCoordinates()
    {
        var assembly = typeof(WoundMaterializationContract).Assembly;
        var draft = assembly.GetType("BookOfEternityClient.Services.MortalWoundOpportunityReceiptDraft", false, false)
            ?? throw new Xunit.Sdk.XunitException("T064-A requires a typed opportunity receipt draft.");
        Assert.Equal(
            new[]
            {
                "Binding", "Decision", "DecisionFingerprint", "OperationKey", "OpportunityAuthorityFingerprint",
                "OpportunityId", "TransitionId", "WoundId"
            },
            draft.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Select(property => property.Name).OrderBy(name => name));
        Assert.Equal("MortalWoundOpportunityDecisionBinding", draft.GetProperty("Binding")!.PropertyType.Name);
        Assert.Equal(typeof(string), draft.GetProperty("OpportunityAuthorityFingerprint")!.PropertyType);
        Assert.DoesNotContain(draft.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "Occurrence" or "EventRef" or "EventSemanticFingerprint" or
            "SourceSessionId" or "SourceRequestId" or "SourceTurn" or "ProducerOperationKey" or
            "ProducerCandidateOrdinal" or "ProducerCandidateCount" or "SourceResultFingerprint" or
            "CandidateFingerprint" or "OccurrenceFingerprint" or "ReceiptId" or "ReceiptFingerprint");

        var binding = assembly.GetType("BookOfEternityClient.Services.MortalWoundOpportunityDecisionBinding", false, false)
            ?? throw new Xunit.Sdk.XunitException("T064-A requires the four-coordinate decision binding.");
        Assert.Equal(new[] { "RequestId", "SessionId", "SnapshotToken", "Turn" },
            binding.GetProperties(BindingFlags.Public | BindingFlags.Instance).Select(property => property.Name).OrderBy(name => name));
    }

    [Fact]
    public void PlanConsumeAndAppend_RequiresNoneToHaveNoHistoryAndMaterializeToAgreeWithExactlyOneTypedHistoryTransition()
    {
        var method = ExactStatic(RequiredType(), "PlanConsumeAndAppend", 4);
        var parameters = method.GetParameters();
        Assert.Equal("MortalWoundOccurrenceState", parameters[0].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptState", parameters[1].ParameterType.Name);
        Assert.Equal("MortalWoundOpportunityReceiptDraft", parameters[2].ParameterType.Name);
        Assert.Equal(nameof(WoundHistoryState), parameters[3].ParameterType.Name);

        // The complete parsed history, not a caller projection, is the fourth argument:
        // none requires no matching transition; materialize requires exactly one
        // create|worsen transition agreeing on transition/wound/turn/event/operation/
        // source authority.
    }

    [Fact]
    public void PlanConsumeAndAppend_ReceivesOnlyPublicOpportunityCorrelationAndCannotAcceptDetachedOccurrenceAuthority()
    {
        var method = ExactStatic(RequiredType(), "PlanConsumeAndAppend", 4);
        var draft = method.GetParameters()[2].ParameterType;
        Assert.Equal("MortalWoundOpportunityReceiptDraft", draft.Name);

        // The planner resolves the current immutable occurrence under the signed pending
        // before-state.  A draft may correlate only by public opportunity ID: it cannot
        // smuggle a detached stale occurrence or a guarantee authority.
        Assert.DoesNotContain(draft.GetProperties(BindingFlags.Public | BindingFlags.Instance), property =>
            property.Name is "Occurrence" or "IsGuaranteed" or "AllowNone" or "GuaranteedTriggerAuthorityFingerprint");
    }

    [Fact]
    public void PlanConsumeAndAppend_AppendsRemovesThePendingRowAndReplaysByOpportunityBeforeDecisionOperation()
    {
        var pending = ParsedOccurrenceState(guaranteed: false);
        var before = EmptyReceiptState();
        var none = Draft(Assert.Single(pending.Occurrences), decision: "none");
        var history = EmptyHistory();

        var appended = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(pending, before, none, history);
        Assert.Equal("appended", appended.Disposition);
        Assert.Empty(appended.Issues);
        Assert.NotNull(appended.Receipt);
        Assert.Empty(appended.OccurrenceState!.Occurrences);
        Assert.Single(appended.ReceiptState!.Receipts);
        Assert.Equal("none", appended.HistoryAgreement);

        var replay = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            appended.OccurrenceState, appended.ReceiptState, none, history);
        Assert.Equal("exact_replay", replay.Disposition);
        Assert.NotSame(appended.Receipt, replay.Receipt);
        Assert.Equal(appended.Receipt, replay.Receipt);
        Assert.Empty(replay.OccurrenceState!.Occurrences);
        Assert.Single(replay.ReceiptState!.Receipts);

        var changedBinding = none with
        {
            Binding = new MortalWoundOpportunityDecisionBinding("session_42", "request_changed", "snapshot_42", 42)
        };
        var conflict = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            appended.OccurrenceState, appended.ReceiptState, changedBinding, history);
        Assert.Equal("conflict", conflict.Disposition);
        Assert.Null(conflict.OccurrenceState);
        Assert.Null(conflict.ReceiptState);
        Assert.Null(conflict.Receipt);

        var guaranteedPending = ParsedOccurrenceState(guaranteed: true);
        var guaranteedNone = Draft(Assert.Single(guaranteedPending.Occurrences), decision: "none");
        var guaranteed = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(guaranteedPending, before, guaranteedNone, history);
        Assert.Equal("conflict", guaranteed.Disposition);
        Assert.Null(guaranteed.OccurrenceState);
        Assert.Null(guaranteed.ReceiptState);
    }

    [Fact]
    public void PlanConsumeAndAppend_ReceiptFirstReplayRejectsEveryChangedDecisionCoordinateAndHistoryTampering()
    {
        var pending = ParsedOccurrenceState(guaranteed: false);
        var before = EmptyReceiptState();
        var none = Draft(Assert.Single(pending.Occurrences), decision: "none");
        var emptyHistory = EmptyHistory();
        var appended = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            pending,
            before,
            none,
            emptyHistory);
        Assert.Equal("appended", appended.Disposition);

        var changedDecisionFingerprint = ExternalFingerprint("decision:none:changed-replay");
        foreach (var changed in new[]
                 {
                     none with { OpportunityId = "mortal_wound_occurrence_" + new string('a', 64) },
                     none with { OpportunityAuthorityFingerprint = ExternalFingerprint("changed-opportunity-authority") },
                     none with { Decision = "materialize", WoundId = "wound_changed", TransitionId = "transition_changed" },
                     none with
                     {
                         DecisionFingerprint = changedDecisionFingerprint,
                         OperationKey = "wound_operation_" + changedDecisionFingerprint["sha256:".Length..]
                     },
                     none with { OperationKey = "wound_operation_" + new string('b', 64) }
                 })
        {
            var conflict = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
                appended.OccurrenceState!,
                appended.ReceiptState!,
                changed,
                emptyHistory);
            Assert.Equal("conflict", conflict.Disposition);
            Assert.Null(conflict.OccurrenceState);
            Assert.Null(conflict.ReceiptState);
        }

        var historyConflict = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            appended.OccurrenceState!,
            appended.ReceiptState!,
            none,
            HistoryWithOperation(none.OperationKey));
        Assert.Equal("conflict", historyConflict.Disposition);
    }

    [Fact]
    public void PlanConsumeAndAppend_RequiresExactlyOneMatchingParsedCreateOrWorsenHistoryTransitionForMaterialize()
    {
        var pending = ParsedOccurrenceState(guaranteed: false);
        var before = EmptyReceiptState();
        var materialize = Draft(Assert.Single(pending.Occurrences), decision: "materialize",
            woundId: "wound_test_torn_side", transitionId: "wound_transition_test_001");

        var zero = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(pending, before, materialize, EmptyHistory());
        Assert.Equal("conflict", zero.Disposition);

        var occurrence = Assert.Single(pending.Occurrences);
        var matching = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(pending, before, materialize, HistoryFor(materialize, occurrence));
        Assert.Equal("appended", matching.Disposition);
        Assert.Equal("materialize", matching.HistoryAgreement);
        Assert.Equal("wound_test_torn_side", matching.Receipt!.WoundId);
        Assert.Equal("wound_transition_test_001", matching.Receipt.TransitionId);

        var worseningPending = ParsedOccurrenceState(guaranteed: false, worsening: true);
        var worseningOccurrence = Assert.Single(worseningPending.Occurrences);
        var worsen = Draft(
            worseningOccurrence,
            decision: "materialize",
            woundId: "wound_active",
            transitionId: "wound_transition_worsen_002");
        var worsening = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            worseningPending,
            before,
            worsen,
            WorsenHistoryFor(worsen, worseningOccurrence));
        Assert.Equal("appended", worsening.Disposition);
        Assert.Equal("materialize", worsening.HistoryAgreement);
        Assert.Equal("wound_active", worsening.Receipt!.WoundId);
        Assert.Equal("wound_transition_worsen_002", worsening.Receipt.TransitionId);

        foreach (var mutation in new Action<JsonObject>[]
                 {
                     transition => transition["transitionId"] = "wound_transition_mismatch",
                     transition =>
                     {
                         transition["woundId"] = "wound_mismatch";
                         transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint("wound_mismatch");
                     },
                     transition => transition["turn"] = 43,
                     transition => transition["eventRef"] = "event_mismatch",
                     transition => transition["operationKey"] = "wound_operation_mismatch",
                     transition => transition["sourceFingerprint"] = ExternalFingerprint("source-authority-mismatch")
                 })
        {
            var mismatch = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
                pending,
                before,
                materialize,
                HistoryFor(materialize, occurrence, mutation));
            Assert.Equal("conflict", mismatch.Disposition);
        }

        var replay = MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
            matching.OccurrenceState!,
            matching.ReceiptState!,
            materialize,
            HistoryFor(materialize, occurrence));
        Assert.Equal("exact_replay", replay.Disposition);
        foreach (var changed in new[]
                 {
                     materialize with { WoundId = "wound_changed" },
                     materialize with { TransitionId = "wound_transition_changed" }
                 })
        {
            Assert.Equal(
                "conflict",
                MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
                    matching.OccurrenceState!,
                    matching.ReceiptState!,
                    changed,
                    HistoryFor(materialize, occurrence)).Disposition);
        }
        Assert.Equal(
            "conflict",
            MortalWoundOpportunityReceiptState.PlanConsumeAndAppend(
                matching.OccurrenceState!,
                matching.ReceiptState!,
                materialize,
                HistoryFor(materialize, occurrence, transition => transition["eventRef"] = "event_replay_mismatch")).Disposition);

        var first = HistoryTransitionFor(materialize, occurrence);
        var second = HistoryTransitionFor(materialize, occurrence);
        second["transitionId"] = "wound_transition_test_002";
        second["ordinal"] = 2;
        second["woundTransitionOrdinal"] = 2;
        second["kind"] = "worsen";
        var duplicateHistory = WoundHistoryState.Parse(WoundContractTestData.CreateHistory(first, second).ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.False(duplicateHistory.IsValid);
        Assert.Contains(duplicateHistory.Issues, issue => issue.Code == "wound_history_duplicate_operation_key");
    }

    [Fact]
    public void SerializeCanonical_RoundTripsDetachedReceiptStateWithoutLeakingPublicationAuthority()
    {
        var result = Parse(Root(Row()));
        AssertValid(result);
        var state = Required(result, "State");
        var serialized = (string)ExactStatic(RequiredType(), "SerializeCanonical", 1).Invoke(null, new[] { state })!;
        var reparsed = Parse(serialized);
        AssertValid(reparsed);
        Assert.Equal(serialized, (string)ExactStatic(RequiredType(), "SerializeCanonical", 1).Invoke(null, new[] { Required(reparsed, "State") })!);
        Assert.NotSame(state, Required(reparsed, "State"));
    }

    private static readonly string[] RequiredRowFields =
    {
        "receiptId", "ordinal", "opportunityId", "opportunityAuthorityFingerprint", "sessionId",
        "requestId", "snapshotToken", "turn", "eventRef", "eventSemanticFingerprint",
        "sourceSessionId", "sourceRequestId", "sourceTurn", "producerOperationKey",
        "producerCandidateOrdinal", "producerCandidateCount", "sourceResultFingerprint",
        "candidateFingerprint", "occurrenceFingerprint", "decision", "decisionFingerprint",
        "operationKey", "woundId", "transitionId", "receiptFingerprint"
    };

    private static object Parse(JsonObject root) => Parse(root.ToJsonString());
    private static object Parse(string? json) => Invoke(ExactStatic(RequiredType(), "Parse", 2), json!, RootPath);

    private static JsonObject Root(params JsonObject[] rows) => Root(rows.Length + 1, rows);

    private static JsonObject Root(int nextOrdinal, params JsonObject[] rows) => new()
    {
        ["schemaVersion"] = 1, ["nextOrdinal"] = nextOrdinal,
        ["receipts"] = new JsonArray(rows.Select(row => (JsonNode)row.DeepClone()).ToArray())
    };

    private static MortalWoundOccurrenceState ParsedOccurrenceState(bool guaranteed, bool worsening = false)
    {
        var parsed = MortalWoundOccurrenceState.Parse(
            MortalWoundOccurrenceStateTests.CreateCanonicalOccurrenceRoot(guaranteed, worsening).ToJsonString(),
            "game_state/control/pending_mortal_wound_occurrences.json");
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static MortalWoundOccurrenceState ParsedOccurrenceBatchState()
    {
        var parsed = MortalWoundOccurrenceState.Parse(
            MortalWoundOccurrenceStateTests.CreateCanonicalOccurrenceBatchRoot().ToJsonString(),
            "game_state/control/pending_mortal_wound_occurrences.json");
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static MortalWoundOpportunityReceiptState EmptyReceiptState()
    {
        var parsed = MortalWoundOpportunityReceiptState.Parse("{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}", RootPath);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static MortalWoundOpportunityReceiptDraft Draft(
        MortalWoundOccurrence occurrence,
        string decision,
        string? woundId = null,
        string? transitionId = null)
    {
        var decisionFingerprint = ExternalFingerprint("decision:" + decision + ":decision_42");
        return new MortalWoundOpportunityReceiptDraft(
            new MortalWoundOpportunityDecisionBinding("session_42", "request_42", "snapshot_42", 42),
            occurrence.OccurrenceId,
            ExternalFingerprint("opportunity:" + occurrence.OccurrenceId),
            decision,
            decisionFingerprint,
            "wound_operation_" + decisionFingerprint["sha256:".Length..],
            woundId,
            transitionId);
    }

    private static WoundHistoryState EmptyHistory()
    {
        var parsed = WoundHistoryState.Parse(WoundContractTestData.CreateHistory().ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static WoundHistoryState HistoryWithOperation(string operationKey)
    {
        const string woundId = "wound_history_intrusion";
        var transition = WoundContractTestData.CreateTransition(kind: "create");
        transition["transitionId"] = "wound_transition_history_intrusion";
        transition["woundId"] = woundId;
        transition["operationKey"] = operationKey;
        transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(woundId);
        transition["afterFingerprint"] = ExternalFingerprint("history-intrusion-after");
        transition["sourceFingerprint"] = ExternalFingerprint("history-intrusion-source");
        transition["attemptId"] = null;
        var parsed = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory(transition).ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static WoundHistoryState HistoryFor(
        MortalWoundOpportunityReceiptDraft draft,
        MortalWoundOccurrence occurrence,
        Action<JsonObject>? mutation = null)
    {
        var transition = HistoryTransitionFor(draft, occurrence);
        mutation?.Invoke(transition);
        var parsed = WoundHistoryState.Parse(WoundContractTestData.CreateHistory(transition).ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static JsonObject HistoryTransitionFor(MortalWoundOpportunityReceiptDraft draft, MortalWoundOccurrence occurrence)
    {
        var eventAuthority = occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal];
        var transition = WoundContractTestData.CreateTransition(kind: "create");
        transition["transitionId"] = draft.TransitionId;
        transition["woundId"] = draft.WoundId;
        transition["turn"] = draft.Binding.Turn;
        transition["eventRef"] = eventAuthority.EventRef;
        transition["operationKey"] = draft.OperationKey;
        transition["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(draft.WoundId!);
        transition["afterFingerprint"] = ExternalFingerprint("history-after:" + draft.WoundId);
        transition["sourceFingerprint"] = draft.OpportunityAuthorityFingerprint;
        transition["outputFingerprint"] = "sha256:" + new string('f', 64);
        return transition;
    }

    private static WoundHistoryState WorsenHistoryFor(
        MortalWoundOpportunityReceiptDraft draft,
        MortalWoundOccurrence occurrence)
    {
        var priorAfter = ExternalFingerprint("history-before-worsen:" + draft.WoundId);
        var create = WoundContractTestData.CreateTransition(kind: "create", ordinal: 1, woundTransitionOrdinal: 1);
        create["transitionId"] = "wound_transition_prior_create_001";
        create["woundId"] = draft.WoundId;
        create["turn"] = 40;
        create["eventRef"] = "event_prior_create_40";
        create["operationKey"] = "wound_operation_prior_create_001";
        create["beforeFingerprint"] = WoundHistoryState.ComputeNonexistentBeforeFingerprint(draft.WoundId!);
        create["afterFingerprint"] = priorAfter;
        create["sourceFingerprint"] = ExternalFingerprint("prior-create-source:" + draft.WoundId);
        create["attemptId"] = null;

        var selectedEvent = occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal];
        var worsen = WoundContractTestData.CreateTransition(kind: "worsen", ordinal: 2, woundTransitionOrdinal: 2);
        worsen["transitionId"] = draft.TransitionId;
        worsen["woundId"] = draft.WoundId;
        worsen["turn"] = draft.Binding.Turn;
        worsen["eventRef"] = selectedEvent.EventRef;
        worsen["operationKey"] = draft.OperationKey;
        worsen["beforeFingerprint"] = priorAfter;
        worsen["afterFingerprint"] = ExternalFingerprint("history-after-worsen:" + draft.WoundId);
        worsen["sourceFingerprint"] = draft.OpportunityAuthorityFingerprint;
        worsen["attemptId"] = null;

        var parsed = WoundHistoryState.Parse(
            WoundContractTestData.CreateHistory(create, worsen).ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static JsonObject Row(
        int ordinal = 1,
        string decisionSeed = "decision_1", string producerOperationKey = "batch_1",
        int producerCandidateOrdinal = 0, int producerCandidateCount = 1,
        string decision = "none", string? woundId = null, string? transitionId = null)
    {
        var candidateFingerprint = ExternalFingerprint("candidate:" + producerOperationKey + ":" + producerCandidateOrdinal);
        var opportunityId = "mortal_wound_occurrence_" + candidateFingerprint["sha256:".Length..];
        var occurrenceFingerprint = Hash(
            "book_of_eternity.mortal_wound.occurrence", "1",
            opportunityId, "mortal_wound_" + candidateFingerprint["sha256:".Length..], candidateFingerprint);
        var decisionFingerprint = ExternalFingerprint("decision:" + decision + ":" + decisionSeed);
        var operationKey = "wound_operation_" + decisionFingerprint["sha256:".Length..];
        var computedReceiptId = "mortal_wound_receipt_" + Hash(
            "book_of_eternity.mortal_wound.opportunity_receipt_id", "1",
            opportunityId, operationKey, decisionFingerprint)["sha256:".Length..];
        var row = new JsonObject
        {
            ["receiptId"] = computedReceiptId, ["ordinal"] = ordinal, ["opportunityId"] = opportunityId,
            ["opportunityAuthorityFingerprint"] = ExternalFingerprint("opportunity:" + opportunityId), ["sessionId"] = "session_42",
            ["requestId"] = "request_42", ["snapshotToken"] = "snapshot_42", ["turn"] = 42,
            ["eventRef"] = "event_42_0", ["eventSemanticFingerprint"] = ExternalFingerprint("event:42:0"),
            ["sourceSessionId"] = "session_41", ["sourceRequestId"] = "request_41", ["sourceTurn"] = 41,
            ["producerOperationKey"] = producerOperationKey, ["producerCandidateOrdinal"] = producerCandidateOrdinal,
            ["producerCandidateCount"] = producerCandidateCount, ["sourceResultFingerprint"] = ExternalFingerprint("source:" + producerOperationKey),
            ["candidateFingerprint"] = candidateFingerprint,
            ["occurrenceFingerprint"] = occurrenceFingerprint,
            ["decision"] = decision, ["decisionFingerprint"] = decisionFingerprint, ["operationKey"] = operationKey,
            ["woundId"] = woundId, ["transitionId"] = transitionId, ["receiptFingerprint"] = string.Empty
        };
        row["receiptFingerprint"] = ComputeReceiptFingerprint(row);
        return row;
    }

    private static void AssertValid(object result)
    {
        Assert.True(Assert.IsType<bool>(Required(result, "IsValid")), Describe(result));
        Assert.NotNull(Required(result, "State"));
        Assert.Empty(Values(result, "Issues"));
    }

    private static void AssertInvalid(object result, string path, string code)
    {
        Assert.False(Assert.IsType<bool>(Required(result, "IsValid")));
        Assert.Null(PropertyValue(result, "State"));
        Assert.Contains(Values(result, "Issues"), issue =>
            Equals(Required(issue!, "Code"), code) && Equals(Required(issue!, "FilePath"), path));
    }

    private static void AssertInvalidAny(object result, string path)
    {
        Assert.False(Assert.IsType<bool>(Required(result, "IsValid")));
        Assert.Null(PropertyValue(result, "State"));
        Assert.Contains(Values(result, "Issues"), issue => Equals(Required(issue!, "FilePath"), path));
    }

    private static Type RequiredType() => typeof(WoundMaterializationContract).Assembly.GetType(
        "BookOfEternityClient.Services.MortalWoundOpportunityReceiptState", false, false)
        ?? throw new Xunit.Sdk.XunitException("T064-A requires pure MortalWoundOpportunityReceiptState.");

    private static MethodInfo ExactStatic(Type type, string name, int arity, string? resultName = null)
    {
        var method = Assert.Single(
            type.GetMethods(BindingFlags.Public | BindingFlags.Static),
            candidate => candidate.Name == name && candidate.GetParameters().Length == arity);
        if (resultName is not null) Assert.Equal(resultName, method.ReturnType.Name);
        return method;
    }

    private static object Invoke(MethodInfo method, params object?[] arguments) =>
        method.Invoke(null, arguments) ?? throw new Xunit.Sdk.XunitException($"{method.Name} returned null.");

    private static object Required(object instance, string property) =>
        instance.GetType().GetProperty(property)?.GetValue(instance)
        ?? throw new Xunit.Sdk.XunitException($"{instance.GetType().Name}.{property} is required.");

    private static object? PropertyValue(object instance, string property)
    {
        var info = instance.GetType().GetProperty(property)
            ?? throw new Xunit.Sdk.XunitException($"{instance.GetType().Name}.{property} is required.");
        return info.GetValue(instance);
    }

    private static IReadOnlyList<object?> Values(object instance, string property) =>
        ((IEnumerable)Required(instance, property)).Cast<object?>().ToArray();

    private static string Describe(object result) => string.Join(Environment.NewLine, Values(result, "Issues").Select(issue =>
        $"{Required(issue!, "Code")} {Required(issue!, "FilePath")}"));

    private static void AssertStatic(Type type, string name, int arity, string returnTypeName) =>
        Assert.Equal(returnTypeName, ExactStatic(type, name, arity).ReturnType.Name);

    private static void AssertClosedResult(Type type, params string[] properties) =>
        Assert.Equal(properties.OrderBy(value => value), type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(property => property.Name).OrderBy(value => value));

    private static string ComputeReceiptFingerprint(JsonObject row) => Hash(
        "book_of_eternity.mortal_wound.opportunity_receipt", "1",
        Value(row, "receiptId"), Number(row, "ordinal"), Value(row, "opportunityId"),
        Value(row, "opportunityAuthorityFingerprint"), Value(row, "sessionId"), Value(row, "requestId"),
        Value(row, "snapshotToken"), Number(row, "turn"), Value(row, "eventRef"), Value(row, "eventSemanticFingerprint"),
        Value(row, "sourceSessionId"), Value(row, "sourceRequestId"), Number(row, "sourceTurn"),
        Value(row, "producerOperationKey"), Number(row, "producerCandidateOrdinal"), Number(row, "producerCandidateCount"),
        Value(row, "sourceResultFingerprint"), Value(row, "candidateFingerprint"), Value(row, "occurrenceFingerprint"),
        Value(row, "decision"), Value(row, "decisionFingerprint"), Value(row, "operationKey"), NullableValue(row, "woundId"),
        NullableValue(row, "transitionId"));

    private static void RecomputeReceiptIdentity(JsonObject row)
    {
        row["receiptId"] = "mortal_wound_receipt_" + Hash(
            "book_of_eternity.mortal_wound.opportunity_receipt_id", "1",
            Value(row, "opportunityId"), Value(row, "operationKey"), Value(row, "decisionFingerprint"))["sha256:".Length..];
        RecomputeReceiptFingerprint(row);
    }

    private static void ResealConsumedOccurrenceIdentity(JsonObject row)
    {
        var candidateFingerprint = Value(row, "candidateFingerprint");
        var candidateHex = candidateFingerprint["sha256:".Length..];
        row["opportunityId"] = "mortal_wound_occurrence_" + candidateHex;
        row["occurrenceFingerprint"] = Hash(
            "book_of_eternity.mortal_wound.occurrence",
            "1",
            Value(row, "opportunityId"),
            "mortal_wound_" + candidateHex,
            candidateFingerprint);
        RecomputeReceiptIdentity(row);
    }

    internal static MortalWoundOpportunityReceiptState MutateCanonicalReceiptStateForTest(
        MortalWoundOpportunityReceiptState state,
        Action<JsonObject> mutation,
        bool resealConsumedOccurrenceIdentity = false)
    {
        var root = JsonNode.Parse(MortalWoundOpportunityReceiptState.SerializeCanonical(state))!.AsObject();
        var row = root["receipts"]![0]!.AsObject();
        mutation(row);
        if (resealConsumedOccurrenceIdentity)
            ResealConsumedOccurrenceIdentity(row);
        else
            RecomputeReceiptFingerprint(row);
        var parsed = MortalWoundOpportunityReceiptState.Parse(root.ToJsonString(), RootPath);
        Assert.True(parsed.IsValid, string.Join(Environment.NewLine, parsed.Issues));
        return parsed.State!;
    }

    private static void RecomputeReceiptFingerprint(JsonObject row) =>
        row["receiptFingerprint"] = ComputeReceiptFingerprint(row);

    private static string ExternalFingerprint(string label) => Hash("test.mortal_wound.external", "1", label);

    private static string Hash(params string?[] fields)
    {
        var builder = new StringBuilder();
        foreach (var field in fields)
        {
            if (field is null)
            {
                builder.Append("-1:");
                continue;
            }
            builder.Append(Encoding.UTF8.GetByteCount(field).ToString(CultureInfo.InvariantCulture));
            builder.Append(':');
            builder.Append(field);
        }
        return "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()))).ToLowerInvariant();
    }

    private static string Value(JsonObject node, string field) => node[field]!.GetValue<string>();
    private static string? NullableValue(JsonObject node, string field) => node[field]?.GetValue<string>();
    private static string Number(JsonObject node, string field) => node[field]!.GetValue<int>().ToString(CultureInfo.InvariantCulture);
}
