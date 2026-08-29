using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace BookOfEternityClient.Services;

/// <summary>
/// Pure, append-only state for accepted Mortal-wound opportunity decisions.  T070 owns
/// signed before-images and publication; this type only parses and plans after-images.
/// </summary>
internal sealed class MortalWoundOpportunityReceiptState
{
    internal const string StatePath =
        "game_state/wounds/wound_opportunity_receipts.json";

    private const int SchemaVersion = 1;
    private const int MaximumReceipts = 20_000;
    private const int MaximumCandidateCount = 32;
    private const string ReceiptIdPrefix = "mortal_wound_receipt_";
    private const string OccurrenceIdPrefix = "mortal_wound_occurrence_";
    private const string OpportunityRefPrefix = "mortal_wound_";
    private const string OperationKeyPrefix = "wound_operation_";

    private static readonly IReadOnlySet<string> RootFields = Set(
        "schemaVersion", "nextOrdinal", "receipts");

    private static readonly IReadOnlySet<string> ReceiptFields = Set(
        "receiptId", "ordinal", "opportunityId", "opportunityAuthorityFingerprint",
        "sessionId", "requestId", "snapshotToken", "turn", "eventRef",
        "eventSemanticFingerprint", "sourceSessionId", "sourceRequestId",
        "sourceTurn", "producerOperationKey", "producerCandidateOrdinal",
        "producerCandidateCount", "sourceResultFingerprint", "candidateFingerprint",
        "occurrenceFingerprint", "decision", "decisionFingerprint", "operationKey",
        "woundId", "transitionId", "receiptFingerprint");

    private readonly ReadOnlyCollection<MortalWoundOpportunityReceipt> _receipts;

    private MortalWoundOpportunityReceiptState(
        int nextOrdinal,
        IEnumerable<MortalWoundOpportunityReceipt> receipts)
    {
        NextOrdinal = nextOrdinal;
        _receipts = Array.AsReadOnly(receipts
            .Select(CloneReceipt)
            .ToArray());
    }

    public int NextOrdinal { get; }

    public IReadOnlyList<MortalWoundOpportunityReceipt> Receipts => _receipts;

    public static MortalWoundOpportunityReceiptParseResult Parse(
        string? json,
        string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var issues = new List<ValidationIssue>();

        if (string.IsNullOrWhiteSpace(json))
        {
            Add(issues, path, "mortal_wound_opportunity_receipt_invalid_root");
            return ParseFailure(issues);
        }

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            Add(issues, path, "mortal_wound_opportunity_receipt_invalid_root");
            return ParseFailure(issues);
        }

        using (document)
        {
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
            {
                Add(issues, path, "mortal_wound_opportunity_receipt_invalid_root");
                return ParseFailure(issues);
            }

            ResourceMaterializationContract.FindDuplicateProperties(
                root,
                path,
                issues,
                "mortal_wound_opportunity_receipt_duplicate_property");
            ResourceMaterializationContract.ValidateClosedObject(
                root,
                path,
                RootFields,
                issues,
                "mortal_wound_opportunity_receipt_unknown_field");

            var schemaVersion = ReadInteger(
                root,
                "schemaVersion",
                path,
                1,
                1,
                issues);
            var nextOrdinal = ReadInteger(
                root,
                "nextOrdinal",
                path,
                1,
                MaximumReceipts + 1,
                issues);

            if (schemaVersion is not SchemaVersion)
            {
                Add(
                    issues,
                    path + ".schemaVersion",
                    "mortal_wound_opportunity_receipt_invalid_field");
            }

            var receipts = new List<MortalWoundOpportunityReceipt>();
            if (!root.TryGetProperty("receipts", out var rows))
            {
                Add(
                    issues,
                    path + ".receipts",
                    "mortal_wound_opportunity_receipt_missing_field");
            }
            else if (rows.ValueKind != JsonValueKind.Array)
            {
                Add(
                    issues,
                    path + ".receipts",
                    "mortal_wound_opportunity_receipt_invalid_field");
            }
            else if (rows.GetArrayLength() > MaximumReceipts)
            {
                Add(
                    issues,
                    path + ".receipts",
                    "mortal_wound_opportunity_receipt_limit_exceeded");
            }
            else
            {
                var index = 0;
                foreach (var row in rows.EnumerateArray())
                {
                    var receipt = ParseReceipt(
                        row,
                        $"{path}.receipts[{index++}]",
                        issues);
                    if (receipt is not null)
                        receipts.Add(receipt);
                }
            }

            if (nextOrdinal is not null)
                ValidatePersistedSet(receipts, nextOrdinal.Value, path, issues);

            return issues.Count == 0 && nextOrdinal is not null
                ? new MortalWoundOpportunityReceiptParseResult(
                    true,
                    new MortalWoundOpportunityReceiptState(nextOrdinal.Value, receipts),
                    Array.Empty<ValidationIssue>())
                : ParseFailure(issues);
        }
    }

    public static string SerializeCanonical(
        MortalWoundOpportunityReceiptState state)
    {
        ArgumentNullException.ThrowIfNull(state);

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(
                   stream,
                   new JsonWriterOptions
                   {
                       Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                       Indented = false
                   }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SchemaVersion);
            writer.WriteNumber("nextOrdinal", state.NextOrdinal);
            writer.WritePropertyName("receipts");
            writer.WriteStartArray();
            foreach (var receipt in state._receipts)
                WriteReceipt(writer, receipt);
            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    public static string ComputeReceiptFingerprint(
        MortalWoundOpportunityReceipt receipt)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return Hash(
            "book_of_eternity.mortal_wound.opportunity_receipt",
            "1",
            receipt.ReceiptId,
            Number(receipt.Ordinal),
            receipt.OpportunityId,
            receipt.OpportunityAuthorityFingerprint,
            receipt.SessionId,
            receipt.RequestId,
            receipt.SnapshotToken,
            Number(receipt.Turn),
            receipt.EventRef,
            receipt.EventSemanticFingerprint,
            receipt.SourceSessionId,
            receipt.SourceRequestId,
            Number(receipt.SourceTurn),
            receipt.ProducerOperationKey,
            Number(receipt.ProducerCandidateOrdinal),
            Number(receipt.ProducerCandidateCount),
            receipt.SourceResultFingerprint,
            receipt.CandidateFingerprint,
            receipt.OccurrenceFingerprint,
            receipt.Decision,
            receipt.DecisionFingerprint,
            receipt.OperationKey,
            receipt.WoundId,
            receipt.TransitionId);
    }

    public static MortalWoundOpportunityReceiptConsumePlanResult PlanConsumeAndAppend(
        MortalWoundOccurrenceState pendingBefore,
        MortalWoundOpportunityReceiptState receiptBefore,
        MortalWoundOpportunityReceiptDraft draft,
        WoundHistoryState plannedHistory)
    {
        ArgumentNullException.ThrowIfNull(pendingBefore);
        ArgumentNullException.ThrowIfNull(receiptBefore);
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentNullException.ThrowIfNull(plannedHistory);

        var issues = new List<ValidationIssue>();
        ValidateDraft(draft, issues);
        if (issues.Count != 0)
            return Conflict(issues);

        // Durable receipt authority is deliberately consulted before pending membership
        // or operation-key reuse.  A consumed opportunity cannot be reopened by a new
        // operation key after a cold restart.
        var priorReceipts = receiptBefore._receipts
            .Where(receipt => string.Equals(
                receipt.OpportunityId,
                draft.OpportunityId,
                StringComparison.Ordinal))
            .ToArray();

        var agreementIssues = ValidateConsumedOccurrenceAgreement(
            pendingBefore,
            receiptBefore);
        if (agreementIssues.Count != 0)
            return Conflict(agreementIssues);

        if (priorReceipts.Length != 0)
        {
            if (priorReceipts.Length != 1 ||
                !ReceiptMatchesDraft(priorReceipts[0], draft) ||
                !TryValidateHistoryAgreement(
                    priorReceipts[0],
                    expectedTransitionKind: null,
                    plannedHistory,
                    issues,
                    out var replayHistoryAgreement))
            {
                if (issues.Count == 0)
                    AddConflict(issues, StatePath + ".receipts");
                return Conflict(issues);
            }

            return new MortalWoundOpportunityReceiptConsumePlanResult(
                "exact_replay",
                Array.Empty<ValidationIssue>(),
                pendingBefore.DetachForReceiptPlan(),
                CloneState(receiptBefore),
                CloneReceipt(priorReceipts[0]),
                replayHistoryAgreement);
        }

        var pendingMatches = pendingBefore.Occurrences
            .Where(occurrence => string.Equals(
                occurrence.OccurrenceId,
                draft.OpportunityId,
                StringComparison.Ordinal))
            .ToArray();
        if (pendingMatches.Length != 1)
        {
            AddConflict(issues, StatePath + ".receipts");
            return Conflict(issues);
        }

        var occurrence = pendingMatches[0];
        if (occurrence.GuaranteedTrigger is not null && draft.Decision == "none")
        {
            AddConflict(issues, StatePath + ".draft.decision");
            return Conflict(issues);
        }

        var selectedEvent = occurrence.AcceptedEvents[occurrence.AcceptedEventOrdinal];
        var expectedTransitionKind = occurrence.WorseningTarget is null
            ? "create"
            : "worsen";
        if (occurrence.WorseningTarget is not null &&
            !string.Equals(
                occurrence.WorseningTarget.WoundId,
                draft.WoundId,
                StringComparison.Ordinal))
        {
            AddConflict(issues, StatePath + ".draft.woundId");
            return Conflict(issues);
        }
        if (!TryValidateHistoryAgreement(
                draft,
                selectedEvent.EventRef,
                expectedTransitionKind,
                plannedHistory,
                issues,
                out var historyAgreement))
        {
            return Conflict(issues);
        }

        if (receiptBefore._receipts.Any(receipt =>
                ConfusableEquals(receipt.OperationKey, draft.OperationKey)))
        {
            AddConflict(issues, StatePath + ".draft.operationKey");
            return Conflict(issues);
        }

        if (receiptBefore._receipts.Count >= MaximumReceipts ||
            receiptBefore.NextOrdinal != receiptBefore._receipts.Count + 1)
        {
            AddConflict(issues, StatePath);
            return Conflict(issues);
        }

        var receipt = CreateReceipt(
            receiptBefore.NextOrdinal,
            occurrence,
            selectedEvent,
            draft);
        var occurrenceAfter = pendingBefore.RemoveForReceiptPlan(
            occurrence.OccurrenceId);
        var receiptAfter = new MortalWoundOpportunityReceiptState(
            receiptBefore.NextOrdinal + 1,
            receiptBefore._receipts.Append(receipt));

        var receiptStateIssues = new List<ValidationIssue>();
        ValidatePersistedSet(
            receiptAfter._receipts,
            receiptAfter.NextOrdinal,
            StatePath,
            receiptStateIssues);
        if (receiptStateIssues.Count != 0)
            return Conflict(receiptStateIssues);

        var afterAgreementIssues = ValidateConsumedOccurrenceAgreement(
            occurrenceAfter,
            receiptAfter);
        if (afterAgreementIssues.Count != 0)
            return Conflict(afterAgreementIssues);

        return new MortalWoundOpportunityReceiptConsumePlanResult(
            "appended",
            Array.Empty<ValidationIssue>(),
            occurrenceAfter,
            receiptAfter,
            CloneReceipt(receipt),
            historyAgreement);
    }

    public static IReadOnlyList<ValidationIssue> ValidateConsumedOccurrenceAgreement(
        MortalWoundOccurrenceState pending,
        MortalWoundOpportunityReceiptState receipts)
    {
        ArgumentNullException.ThrowIfNull(pending);
        ArgumentNullException.ThrowIfNull(receipts);

        var issues = new List<ValidationIssue>();
        var rows = new List<ConsumedOccurrenceProjection>();
        rows.AddRange(pending.Occurrences.Select((occurrence, index) =>
            new ConsumedOccurrenceProjection(
                occurrence.OccurrenceId,
                occurrence.SourceSessionId,
                occurrence.SourceRequestId,
                occurrence.SourceTurn,
                occurrence.ProducerOperationKey,
                occurrence.ProducerCandidateOrdinal,
                occurrence.ProducerCandidateCount,
                occurrence.SourceResultFingerprint,
                occurrence.CandidateFingerprint,
                occurrence.OccurrenceFingerprint,
                $"{MortalWoundOccurrenceState.StatePath}.occurrences[{index}]")));
        rows.AddRange(receipts._receipts.Select((receipt, index) =>
            new ConsumedOccurrenceProjection(
                receipt.OpportunityId,
                receipt.SourceSessionId,
                receipt.SourceRequestId,
                receipt.SourceTurn,
                receipt.ProducerOperationKey,
                receipt.ProducerCandidateOrdinal,
                receipt.ProducerCandidateCount,
                receipt.SourceResultFingerprint,
                receipt.CandidateFingerprint,
                receipt.OccurrenceFingerprint,
                $"{StatePath}.receipts[{index}]")));

        var identityExact = new HashSet<string>(StringComparer.Ordinal);
        var identityAliases = new HashSet<string>(StringComparer.Ordinal);
        var tupleExact = new HashSet<string>(StringComparer.Ordinal);
        var tupleAliases = new HashSet<string>(StringComparer.Ordinal);
        var producerAliases = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var row in rows)
        {
            RegisterCrossRootIdentity(
                row.OpportunityId,
                row.Path + ".opportunityId",
                identityExact,
                identityAliases,
                issues);

            var tuple = TupleKey(
                row.ProducerOperationKey,
                row.ProducerCandidateOrdinal);
            if (!tupleExact.Add(tuple) ||
                !tupleAliases.Add(MortalLocationIdentityState.BuildConfusableKey(tuple)))
            {
                Add(
                    issues,
                    row.Path,
                    "mortal_wound_opportunity_receipt_duplicate_batch_tuple");
            }

            var producerAlias = MortalLocationIdentityState.BuildConfusableKey(
                row.ProducerOperationKey);
            if (producerAliases.TryGetValue(producerAlias, out var previousKey) &&
                !string.Equals(previousKey, row.ProducerOperationKey, StringComparison.Ordinal))
            {
                Add(
                    issues,
                    row.Path + ".producerOperationKey",
                    "mortal_wound_opportunity_receipt_batch_agreement_mismatch");
            }
            else
            {
                producerAliases[producerAlias] = row.ProducerOperationKey;
            }
        }

        foreach (var group in rows.GroupBy(
                     row => row.ProducerOperationKey,
                     StringComparer.Ordinal))
        {
            var ordered = group
                .OrderBy(row => row.ProducerCandidateOrdinal)
                .ToArray();
            var first = ordered[0];
            var completeOrdinals = ordered
                .Select(row => row.ProducerCandidateOrdinal)
                .SequenceEqual(Enumerable.Range(0, first.ProducerCandidateCount));
            if (ordered.Length != first.ProducerCandidateCount ||
                !completeOrdinals ||
                ordered.Any(row => !SameBatchAuthority(first, row)))
            {
                Add(
                    issues,
                    first.Path,
                    "mortal_wound_opportunity_receipt_batch_agreement_mismatch");
            }
        }

        return Array.AsReadOnly(issues.ToArray());
    }

    private static MortalWoundOpportunityReceipt? ParseReceipt(
        JsonElement row,
        string path,
        List<ValidationIssue> issues)
    {
        if (row.ValueKind != JsonValueKind.Object)
        {
            Add(
                issues,
                path,
                "mortal_wound_opportunity_receipt_invalid_field");
            return null;
        }

        ResourceMaterializationContract.ValidateClosedObject(
            row,
            path,
            ReceiptFields,
            issues,
            "mortal_wound_opportunity_receipt_unknown_field");

        var receiptId = ReadIdentifier(row, "receiptId", path, issues);
        var ordinal = ReadInteger(row, "ordinal", path, 1, MaximumReceipts, issues);
        var opportunityId = ReadIdentifier(row, "opportunityId", path, issues);
        var opportunityAuthorityFingerprint = ReadFingerprint(
            row,
            "opportunityAuthorityFingerprint",
            path,
            issues);
        var sessionId = ReadIdentifier(row, "sessionId", path, issues);
        var requestId = ReadIdentifier(row, "requestId", path, issues);
        var snapshotToken = ReadIdentifier(row, "snapshotToken", path, issues);
        var turn = ReadInteger(row, "turn", path, 0, int.MaxValue, issues);
        var eventRef = ReadIdentifier(row, "eventRef", path, issues);
        var eventSemanticFingerprint = ReadFingerprint(
            row,
            "eventSemanticFingerprint",
            path,
            issues);
        var sourceSessionId = ReadIdentifier(row, "sourceSessionId", path, issues);
        var sourceRequestId = ReadIdentifier(row, "sourceRequestId", path, issues);
        var sourceTurn = ReadInteger(
            row,
            "sourceTurn",
            path,
            1,
            int.MaxValue,
            issues);
        var producerOperationKey = ReadIdentifier(
            row,
            "producerOperationKey",
            path,
            issues);
        var producerCandidateOrdinal = ReadInteger(
            row,
            "producerCandidateOrdinal",
            path,
            0,
            MaximumCandidateCount - 1,
            issues);
        var producerCandidateCount = ReadInteger(
            row,
            "producerCandidateCount",
            path,
            1,
            MaximumCandidateCount,
            issues);
        var sourceResultFingerprint = ReadFingerprint(
            row,
            "sourceResultFingerprint",
            path,
            issues);
        var candidateFingerprint = ReadFingerprint(
            row,
            "candidateFingerprint",
            path,
            issues);
        var occurrenceFingerprint = ReadFingerprint(
            row,
            "occurrenceFingerprint",
            path,
            issues);
        var decision = ReadIdentifier(row, "decision", path, issues);
        var decisionFingerprint = ReadFingerprint(
            row,
            "decisionFingerprint",
            path,
            issues);
        var operationKey = ReadIdentifier(row, "operationKey", path, issues);
        var woundId = ReadNullableIdentifier(row, "woundId", path, issues);
        var transitionId = ReadNullableIdentifier(row, "transitionId", path, issues);
        var receiptFingerprint = ReadFingerprint(
            row,
            "receiptFingerprint",
            path,
            issues);

        if (receiptId is null || ordinal is null || opportunityId is null ||
            opportunityAuthorityFingerprint is null || sessionId is null ||
            requestId is null || snapshotToken is null || turn is null ||
            eventRef is null || eventSemanticFingerprint is null ||
            sourceSessionId is null || sourceRequestId is null || sourceTurn is null ||
            producerOperationKey is null || producerCandidateOrdinal is null ||
            producerCandidateCount is null || sourceResultFingerprint is null ||
            candidateFingerprint is null || occurrenceFingerprint is null ||
            decision is null || decisionFingerprint is null || operationKey is null ||
            receiptFingerprint is null)
        {
            return null;
        }

        var receipt = new MortalWoundOpportunityReceipt(
            receiptId,
            ordinal.Value,
            opportunityId,
            opportunityAuthorityFingerprint,
            sessionId,
            requestId,
            snapshotToken,
            turn.Value,
            eventRef,
            eventSemanticFingerprint,
            sourceSessionId,
            sourceRequestId,
            sourceTurn.Value,
            producerOperationKey,
            producerCandidateOrdinal.Value,
            producerCandidateCount.Value,
            sourceResultFingerprint,
            candidateFingerprint,
            occurrenceFingerprint,
            decision,
            decisionFingerprint,
            operationKey,
            woundId,
            transitionId,
            receiptFingerprint);

        ValidateReceipt(receipt, path, issues);
        return receipt;
    }

    private static void ValidateReceipt(
        MortalWoundOpportunityReceipt receipt,
        string path,
        List<ValidationIssue> issues)
    {
        if (receipt.ProducerCandidateOrdinal >= receipt.ProducerCandidateCount)
        {
            Add(
                issues,
                path + ".producerCandidateOrdinal",
                "mortal_wound_opportunity_receipt_invalid_field");
        }

        if (!DecisionCoordinatesAgree(
                receipt.Decision,
                receipt.WoundId,
                receipt.TransitionId))
        {
            Add(
                issues,
                path,
                "mortal_wound_opportunity_receipt_decision_coordinate_mismatch");
        }

        var candidateHex = receipt.CandidateFingerprint["sha256:".Length..];
        var expectedOpportunityId = OccurrenceIdPrefix + candidateHex;
        if (!string.Equals(
                receipt.OpportunityId,
                expectedOpportunityId,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".opportunityId",
                "mortal_wound_opportunity_receipt_invalid_field");
        }

        var expectedOccurrenceFingerprint = Hash(
            "book_of_eternity.mortal_wound.occurrence",
            "1",
            expectedOpportunityId,
            OpportunityRefPrefix + candidateHex,
            receipt.CandidateFingerprint);
        if (!string.Equals(
                receipt.OccurrenceFingerprint,
                expectedOccurrenceFingerprint,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".occurrenceFingerprint",
                "mortal_wound_opportunity_receipt_occurrence_fingerprint_mismatch");
        }

        var expectedOperationKey = OperationKeyPrefix +
            receipt.DecisionFingerprint["sha256:".Length..];
        if (!string.Equals(
                receipt.OperationKey,
                expectedOperationKey,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".operationKey",
                "mortal_wound_opportunity_receipt_invalid_field");
        }

        var expectedReceiptId = ComputeReceiptId(
            receipt.OpportunityId,
            receipt.OperationKey,
            receipt.DecisionFingerprint);
        if (!string.Equals(
                receipt.ReceiptId,
                expectedReceiptId,
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".receiptId",
                "mortal_wound_opportunity_receipt_invalid_field");
        }

        if (!string.Equals(
                receipt.ReceiptFingerprint,
                ComputeReceiptFingerprint(receipt),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + ".receiptFingerprint",
                "mortal_wound_opportunity_receipt_fingerprint_mismatch");
        }
    }

    private static void ValidatePersistedSet(
        IReadOnlyList<MortalWoundOpportunityReceipt> receipts,
        int nextOrdinal,
        string path,
        List<ValidationIssue> issues)
    {
        if (nextOrdinal != receipts.Count + 1 ||
            receipts.Where((receipt, index) => receipt.Ordinal != index + 1).Any())
        {
            Add(
                issues,
                path,
                "mortal_wound_opportunity_receipt_ordinal_discontinuity");
        }

        var receiptIds = new HashSet<string>(StringComparer.Ordinal);
        var receiptAliases = new HashSet<string>(StringComparer.Ordinal);
        var opportunityIds = new HashSet<string>(StringComparer.Ordinal);
        var opportunityAliases = new HashSet<string>(StringComparer.Ordinal);
        var operationKeys = new HashSet<string>(StringComparer.Ordinal);
        var operationAliases = new HashSet<string>(StringComparer.Ordinal);
        var tuples = new HashSet<string>(StringComparer.Ordinal);
        var tupleAliases = new HashSet<string>(StringComparer.Ordinal);
        var producerAliases = new Dictionary<string, string>(StringComparer.Ordinal);

        for (var index = 0; index < receipts.Count; index++)
        {
            var receipt = receipts[index];
            var rowPath = $"{path}.receipts[{index}]";
            RegisterIdentity(
                receipt.ReceiptId,
                rowPath + ".receiptId",
                receiptIds,
                receiptAliases,
                "mortal_wound_opportunity_receipt_duplicate_id",
                "mortal_wound_opportunity_receipt_confusable_id",
                issues);
            RegisterIdentity(
                receipt.OpportunityId,
                rowPath + ".opportunityId",
                opportunityIds,
                opportunityAliases,
                "mortal_wound_opportunity_receipt_duplicate_opportunity",
                "mortal_wound_opportunity_receipt_confusable_opportunity",
                issues);
            RegisterIdentity(
                receipt.OperationKey,
                rowPath + ".operationKey",
                operationKeys,
                operationAliases,
                "mortal_wound_opportunity_receipt_duplicate_operation",
                "mortal_wound_opportunity_receipt_confusable_operation",
                issues);

            var tuple = TupleKey(
                receipt.ProducerOperationKey,
                receipt.ProducerCandidateOrdinal);
            if (!tuples.Add(tuple) ||
                !tupleAliases.Add(MortalLocationIdentityState.BuildConfusableKey(tuple)))
            {
                Add(
                    issues,
                    rowPath,
                    "mortal_wound_opportunity_receipt_duplicate_batch_tuple");
            }

            var producerAlias = MortalLocationIdentityState.BuildConfusableKey(
                receipt.ProducerOperationKey);
            if (producerAliases.TryGetValue(producerAlias, out var priorProducerKey) &&
                !string.Equals(
                    priorProducerKey,
                    receipt.ProducerOperationKey,
                    StringComparison.Ordinal))
            {
                Add(
                    issues,
                    rowPath + ".producerOperationKey",
                    "mortal_wound_opportunity_receipt_batch_agreement_mismatch");
            }
            else
            {
                producerAliases[producerAlias] = receipt.ProducerOperationKey;
            }
        }

        foreach (var group in receipts.GroupBy(
                     receipt => receipt.ProducerOperationKey,
                     StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Count() > first.ProducerCandidateCount ||
                group.Any(receipt => !SameStoredBatchAuthority(first, receipt)))
            {
                Add(
                    issues,
                    path,
                    "mortal_wound_opportunity_receipt_batch_agreement_mismatch");
            }
        }
    }

    private static void ValidateDraft(
        MortalWoundOpportunityReceiptDraft draft,
        List<ValidationIssue> issues)
    {
        const string path = StatePath + ".draft";
        if (draft.Binding is null)
        {
            AddConflict(issues, path + ".binding");
            return;
        }

        if (!ExactIdentifier(draft.Binding.SessionId) ||
            !ExactIdentifier(draft.Binding.RequestId) ||
            !ExactIdentifier(draft.Binding.SnapshotToken) ||
            draft.Binding.Turn < 0)
        {
            AddConflict(issues, path + ".binding");
        }

        if (!ExactIdentifier(draft.OpportunityId))
            AddConflict(issues, path + ".opportunityId");
        if (!Fingerprint(draft.OpportunityAuthorityFingerprint))
            AddConflict(issues, path + ".opportunityAuthorityFingerprint");
        if (!Fingerprint(draft.DecisionFingerprint))
            AddConflict(issues, path + ".decisionFingerprint");
        if (!ExactIdentifier(draft.OperationKey) ||
            !Fingerprint(draft.DecisionFingerprint) ||
            !string.Equals(
                draft.OperationKey,
                OperationKeyPrefix + draft.DecisionFingerprint["sha256:".Length..],
                StringComparison.Ordinal))
        {
            AddConflict(issues, path + ".operationKey");
        }

        if (!DecisionCoordinatesAgree(
                draft.Decision,
                draft.WoundId,
                draft.TransitionId))
        {
            AddConflict(issues, path);
        }
    }

    private static bool TryValidateHistoryAgreement(
        MortalWoundOpportunityReceiptDraft draft,
        string eventRef,
        string expectedTransitionKind,
        WoundHistoryState history,
        List<ValidationIssue> issues,
        out string? agreement)
    {
        if (draft.Decision == "none")
        {
            agreement = "none";
            if (history.Transitions.Any(transition => string.Equals(
                    transition.OperationKey,
                    draft.OperationKey,
                    StringComparison.Ordinal)))
            {
                AddConflict(issues, WoundHistoryState.HistoryPath);
                agreement = null;
                return false;
            }

            return true;
        }

        agreement = "materialize";
        var matches = history.Transitions
            .Where(transition => string.Equals(
                transition.OperationKey,
                draft.OperationKey,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 ||
            !TransitionMatches(
                matches[0],
                draft.TransitionId,
                draft.WoundId,
                draft.Binding.Turn,
                eventRef,
                draft.OperationKey,
                draft.OpportunityAuthorityFingerprint,
                expectedTransitionKind))
        {
            AddConflict(issues, WoundHistoryState.HistoryPath);
            agreement = null;
            return false;
        }

        return true;
    }

    private static bool TryValidateHistoryAgreement(
        MortalWoundOpportunityReceipt receipt,
        string? expectedTransitionKind,
        WoundHistoryState history,
        List<ValidationIssue> issues,
        out string? agreement)
    {
        if (receipt.Decision == "none")
        {
            agreement = "none";
            if (history.Transitions.Any(transition => string.Equals(
                    transition.OperationKey,
                    receipt.OperationKey,
                    StringComparison.Ordinal)))
            {
                AddConflict(issues, WoundHistoryState.HistoryPath);
                agreement = null;
                return false;
            }

            return true;
        }

        agreement = "materialize";
        var matches = history.Transitions
            .Where(transition => string.Equals(
                transition.OperationKey,
                receipt.OperationKey,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 ||
            !TransitionMatches(
                matches[0],
                receipt.TransitionId,
                receipt.WoundId,
                receipt.Turn,
                receipt.EventRef,
                receipt.OperationKey,
                receipt.OpportunityAuthorityFingerprint,
                expectedTransitionKind))
        {
            AddConflict(issues, WoundHistoryState.HistoryPath);
            agreement = null;
            return false;
        }

        return true;
    }

    private static bool TransitionMatches(
        WoundHistoryTransition transition,
        string? transitionId,
        string? woundId,
        int turn,
        string eventRef,
        string operationKey,
        string sourceFingerprint,
        string? expectedKind) =>
        transition.Kind is "create" or "worsen" &&
        (expectedKind is null || transition.Kind == expectedKind) &&
        string.Equals(transition.TransitionId, transitionId, StringComparison.Ordinal) &&
        string.Equals(transition.WoundId, woundId, StringComparison.Ordinal) &&
        transition.Turn == turn &&
        string.Equals(transition.EventRef, eventRef, StringComparison.Ordinal) &&
        string.Equals(transition.OperationKey, operationKey, StringComparison.Ordinal) &&
        string.Equals(
            transition.SourceFingerprint,
            sourceFingerprint,
            StringComparison.Ordinal) &&
        !transition.Terminal;

    private static MortalWoundOpportunityReceipt CreateReceipt(
        int ordinal,
        MortalWoundOccurrence occurrence,
        WoundAcceptedEventAuthority selectedEvent,
        MortalWoundOpportunityReceiptDraft draft)
    {
        var receiptId = ComputeReceiptId(
            occurrence.OccurrenceId,
            draft.OperationKey,
            draft.DecisionFingerprint);
        var receipt = new MortalWoundOpportunityReceipt(
            receiptId,
            ordinal,
            occurrence.OccurrenceId,
            draft.OpportunityAuthorityFingerprint,
            draft.Binding.SessionId,
            draft.Binding.RequestId,
            draft.Binding.SnapshotToken,
            draft.Binding.Turn,
            selectedEvent.EventRef,
            selectedEvent.SemanticFingerprint,
            occurrence.SourceSessionId,
            occurrence.SourceRequestId,
            occurrence.SourceTurn,
            occurrence.ProducerOperationKey,
            occurrence.ProducerCandidateOrdinal,
            occurrence.ProducerCandidateCount,
            occurrence.SourceResultFingerprint,
            occurrence.CandidateFingerprint,
            occurrence.OccurrenceFingerprint,
            draft.Decision,
            draft.DecisionFingerprint,
            draft.OperationKey,
            draft.WoundId,
            draft.TransitionId,
            string.Empty);
        return receipt with
        {
            ReceiptFingerprint = ComputeReceiptFingerprint(receipt)
        };
    }

    private static bool ReceiptMatchesDraft(
        MortalWoundOpportunityReceipt receipt,
        MortalWoundOpportunityReceiptDraft draft) =>
        string.Equals(receipt.OpportunityId, draft.OpportunityId, StringComparison.Ordinal) &&
        string.Equals(
            receipt.OpportunityAuthorityFingerprint,
            draft.OpportunityAuthorityFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(receipt.SessionId, draft.Binding.SessionId, StringComparison.Ordinal) &&
        string.Equals(receipt.RequestId, draft.Binding.RequestId, StringComparison.Ordinal) &&
        string.Equals(
            receipt.SnapshotToken,
            draft.Binding.SnapshotToken,
            StringComparison.Ordinal) &&
        receipt.Turn == draft.Binding.Turn &&
        string.Equals(receipt.Decision, draft.Decision, StringComparison.Ordinal) &&
        string.Equals(
            receipt.DecisionFingerprint,
            draft.DecisionFingerprint,
            StringComparison.Ordinal) &&
        string.Equals(receipt.OperationKey, draft.OperationKey, StringComparison.Ordinal) &&
        string.Equals(receipt.WoundId, draft.WoundId, StringComparison.Ordinal) &&
        string.Equals(receipt.TransitionId, draft.TransitionId, StringComparison.Ordinal);

    private static MortalWoundOpportunityReceiptState CloneState(
        MortalWoundOpportunityReceiptState state) =>
        new(state.NextOrdinal, state._receipts);

    private static MortalWoundOpportunityReceipt CloneReceipt(
        MortalWoundOpportunityReceipt receipt) => receipt with { };

    private static bool SameStoredBatchAuthority(
        MortalWoundOpportunityReceipt left,
        MortalWoundOpportunityReceipt right) =>
        string.Equals(left.SourceSessionId, right.SourceSessionId, StringComparison.Ordinal) &&
        string.Equals(left.SourceRequestId, right.SourceRequestId, StringComparison.Ordinal) &&
        left.SourceTurn == right.SourceTurn &&
        left.ProducerCandidateCount == right.ProducerCandidateCount &&
        string.Equals(
            left.SourceResultFingerprint,
            right.SourceResultFingerprint,
            StringComparison.Ordinal);

    private static bool SameBatchAuthority(
        ConsumedOccurrenceProjection left,
        ConsumedOccurrenceProjection right) =>
        string.Equals(left.SourceSessionId, right.SourceSessionId, StringComparison.Ordinal) &&
        string.Equals(left.SourceRequestId, right.SourceRequestId, StringComparison.Ordinal) &&
        left.SourceTurn == right.SourceTurn &&
        left.ProducerCandidateCount == right.ProducerCandidateCount &&
        string.Equals(
            left.SourceResultFingerprint,
            right.SourceResultFingerprint,
            StringComparison.Ordinal);

    private static void RegisterIdentity(
        string value,
        string path,
        HashSet<string> exact,
        HashSet<string> aliases,
        string duplicateCode,
        string confusableCode,
        List<ValidationIssue> issues)
    {
        if (!exact.Add(value))
        {
            Add(issues, path, duplicateCode);
            return;
        }

        if (!aliases.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
            Add(issues, path, confusableCode);
    }

    private static void RegisterCrossRootIdentity(
        string value,
        string path,
        HashSet<string> exact,
        HashSet<string> aliases,
        List<ValidationIssue> issues)
    {
        if (!exact.Add(value) ||
            !aliases.Add(MortalLocationIdentityState.BuildConfusableKey(value)))
        {
            Add(
                issues,
                path,
                "mortal_wound_opportunity_receipt_duplicate_opportunity");
        }
    }

    private static int? ReadInteger(
        JsonElement value,
        string name,
        string path,
        int minimum,
        int maximum,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(name, out var property))
        {
            Add(
                issues,
                path + "." + name,
                "mortal_wound_opportunity_receipt_missing_field");
            return null;
        }

        if (property.ValueKind != JsonValueKind.Number ||
            !property.TryGetInt32(out var result) ||
            result < minimum ||
            result > maximum ||
            !string.Equals(
                property.GetRawText(),
                Number(result),
                StringComparison.Ordinal))
        {
            Add(
                issues,
                path + "." + name,
                "mortal_wound_opportunity_receipt_invalid_field");
            return null;
        }

        return result;
    }

    private static string? ReadIdentifier(
        JsonElement value,
        string name,
        string path,
        List<ValidationIssue> issues)
    {
        var text = ReadString(value, name, path, issues);
        if (text is null)
            return null;
        if (ExactIdentifier(text))
            return text;

        Add(
            issues,
            path + "." + name,
            "mortal_wound_opportunity_receipt_invalid_field");
        return null;
    }

    private static string? ReadNullableIdentifier(
        JsonElement value,
        string name,
        string path,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(name, out var property))
        {
            Add(
                issues,
                path + "." + name,
                "mortal_wound_opportunity_receipt_missing_field");
            return null;
        }

        if (property.ValueKind == JsonValueKind.Null)
            return null;
        if (property.ValueKind == JsonValueKind.String &&
            ExactIdentifier(property.GetString()))
        {
            return property.GetString();
        }

        Add(
            issues,
            path + "." + name,
            "mortal_wound_opportunity_receipt_invalid_field");
        return null;
    }

    private static string? ReadFingerprint(
        JsonElement value,
        string name,
        string path,
        List<ValidationIssue> issues)
    {
        var text = ReadString(value, name, path, issues);
        if (text is null)
            return null;
        if (Fingerprint(text))
            return text;

        Add(
            issues,
            path + "." + name,
            "mortal_wound_opportunity_receipt_invalid_field");
        return null;
    }

    private static string? ReadString(
        JsonElement value,
        string name,
        string path,
        List<ValidationIssue> issues)
    {
        if (!value.TryGetProperty(name, out var property))
        {
            Add(
                issues,
                path + "." + name,
                "mortal_wound_opportunity_receipt_missing_field");
            return null;
        }

        if (property.ValueKind == JsonValueKind.String &&
            property.GetString() is { Length: > 0 } text)
        {
            return text;
        }

        Add(
            issues,
            path + "." + name,
            "mortal_wound_opportunity_receipt_invalid_field");
        return null;
    }

    private static bool DecisionCoordinatesAgree(
        string? decision,
        string? woundId,
        string? transitionId) => decision switch
    {
        "none" => woundId is null && transitionId is null,
        "materialize" => ExactIdentifier(woundId) && ExactIdentifier(transitionId),
        _ => false
    };

    private static string ComputeReceiptId(
        string opportunityId,
        string operationKey,
        string decisionFingerprint) => ReceiptIdPrefix + Hash(
        "book_of_eternity.mortal_wound.opportunity_receipt_id",
        "1",
        opportunityId,
        operationKey,
        decisionFingerprint)["sha256:".Length..];

    private static bool ExactIdentifier(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool Fingerprint(string? value) =>
        ResourceMaterializationContract.IsAuthorityFingerprint(value);

    private static bool ConfusableEquals(string left, string right) =>
        string.Equals(
            MortalLocationIdentityState.BuildConfusableKey(left),
            MortalLocationIdentityState.BuildConfusableKey(right),
            StringComparison.Ordinal);

    private static string TupleKey(string operationKey, int ordinal) =>
        operationKey + "\u001f" + Number(ordinal);

    private static string Number(int value) =>
        value.ToString(CultureInfo.InvariantCulture);

    private static string Hash(params string?[] fields) =>
        WoundAcceptedTurnFingerprintWriter.Compute(fields);

    private static IReadOnlySet<string> Set(params string[] values) =>
        new HashSet<string>(values, StringComparer.Ordinal);

    private static MortalWoundOpportunityReceiptParseResult ParseFailure(
        IEnumerable<ValidationIssue> issues) =>
        new(false, null, Array.AsReadOnly(issues.ToArray()));

    private static MortalWoundOpportunityReceiptConsumePlanResult Conflict(
        IEnumerable<ValidationIssue> issues) =>
        new(
            "conflict",
            Array.AsReadOnly(issues.ToArray()),
            null,
            null,
            null,
            null);

    private static void AddConflict(
        ICollection<ValidationIssue> issues,
        string path) =>
        issues.Add(NewIssue(
            path,
            "mortal_wound_opportunity_receipt_conflict"));

    private static void Add(
        ICollection<ValidationIssue> issues,
        string path,
        string code) => issues.Add(NewIssue(path, code));

    private static ValidationIssue NewIssue(string path, string code) => new(
        path,
        IssueSeverity.Error,
        "Mortal wound opportunity receipt authority is invalid.",
        code: code,
        actor: "Client",
        section: "wound_materialization",
        expected: "one strict immutable canonical opportunity receipt state",
        actual: code,
        repairHint:
        "Restore the signed pending occurrence and append-only receipt roots; never infer, rewrite, truncate, duplicate, or reuse opportunity authority.");

    private static void WriteReceipt(
        Utf8JsonWriter writer,
        MortalWoundOpportunityReceipt receipt)
    {
        writer.WriteStartObject();
        writer.WriteString("receiptId", receipt.ReceiptId);
        writer.WriteNumber("ordinal", receipt.Ordinal);
        writer.WriteString("opportunityId", receipt.OpportunityId);
        writer.WriteString(
            "opportunityAuthorityFingerprint",
            receipt.OpportunityAuthorityFingerprint);
        writer.WriteString("sessionId", receipt.SessionId);
        writer.WriteString("requestId", receipt.RequestId);
        writer.WriteString("snapshotToken", receipt.SnapshotToken);
        writer.WriteNumber("turn", receipt.Turn);
        writer.WriteString("eventRef", receipt.EventRef);
        writer.WriteString(
            "eventSemanticFingerprint",
            receipt.EventSemanticFingerprint);
        writer.WriteString("sourceSessionId", receipt.SourceSessionId);
        writer.WriteString("sourceRequestId", receipt.SourceRequestId);
        writer.WriteNumber("sourceTurn", receipt.SourceTurn);
        writer.WriteString("producerOperationKey", receipt.ProducerOperationKey);
        writer.WriteNumber(
            "producerCandidateOrdinal",
            receipt.ProducerCandidateOrdinal);
        writer.WriteNumber("producerCandidateCount", receipt.ProducerCandidateCount);
        writer.WriteString(
            "sourceResultFingerprint",
            receipt.SourceResultFingerprint);
        writer.WriteString("candidateFingerprint", receipt.CandidateFingerprint);
        writer.WriteString("occurrenceFingerprint", receipt.OccurrenceFingerprint);
        writer.WriteString("decision", receipt.Decision);
        writer.WriteString("decisionFingerprint", receipt.DecisionFingerprint);
        writer.WriteString("operationKey", receipt.OperationKey);
        if (receipt.WoundId is null)
            writer.WriteNull("woundId");
        else
            writer.WriteString("woundId", receipt.WoundId);
        if (receipt.TransitionId is null)
            writer.WriteNull("transitionId");
        else
            writer.WriteString("transitionId", receipt.TransitionId);
        writer.WriteString("receiptFingerprint", receipt.ReceiptFingerprint);
        writer.WriteEndObject();
    }

    private sealed record ConsumedOccurrenceProjection(
        string OpportunityId,
        string SourceSessionId,
        string SourceRequestId,
        int SourceTurn,
        string ProducerOperationKey,
        int ProducerCandidateOrdinal,
        int ProducerCandidateCount,
        string SourceResultFingerprint,
        string CandidateFingerprint,
        string OccurrenceFingerprint,
        string Path);
}

internal sealed record MortalWoundOpportunityDecisionBinding(
    string SessionId,
    string RequestId,
    string SnapshotToken,
    int Turn);

internal sealed record MortalWoundOpportunityReceiptDraft(
    MortalWoundOpportunityDecisionBinding Binding,
    string OpportunityId,
    string OpportunityAuthorityFingerprint,
    string Decision,
    string DecisionFingerprint,
    string OperationKey,
    string? WoundId,
    string? TransitionId);

internal sealed record MortalWoundOpportunityReceipt(
    string ReceiptId,
    int Ordinal,
    string OpportunityId,
    string OpportunityAuthorityFingerprint,
    string SessionId,
    string RequestId,
    string SnapshotToken,
    int Turn,
    string EventRef,
    string EventSemanticFingerprint,
    string SourceSessionId,
    string SourceRequestId,
    int SourceTurn,
    string ProducerOperationKey,
    int ProducerCandidateOrdinal,
    int ProducerCandidateCount,
    string SourceResultFingerprint,
    string CandidateFingerprint,
    string OccurrenceFingerprint,
    string Decision,
    string DecisionFingerprint,
    string OperationKey,
    string? WoundId,
    string? TransitionId,
    string ReceiptFingerprint);

internal sealed record MortalWoundOpportunityReceiptParseResult(
    bool IsValid,
    MortalWoundOpportunityReceiptState? State,
    IReadOnlyList<ValidationIssue> Issues);

internal sealed record MortalWoundOpportunityReceiptConsumePlanResult(
    string Disposition,
    IReadOnlyList<ValidationIssue> Issues,
    MortalWoundOccurrenceState? OccurrenceState,
    MortalWoundOpportunityReceiptState? ReceiptState,
    MortalWoundOpportunityReceipt? Receipt,
    string? HistoryAgreement);
