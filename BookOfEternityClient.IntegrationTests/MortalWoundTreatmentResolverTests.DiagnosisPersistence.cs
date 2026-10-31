using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(20, false)]
    [InlineData(1, true)]
    public async Task DiagnosisBareHoldRoundTripsExactPreparedRequestWithoutRepairOrClaimMutation(int natural, bool fate)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(natural, signedInput: true, duration: 0, seedFate: fate);
        var owner = fixture.Authority;
        var history = new WoundHistoryParseResult(owner.History, Array.Empty<ValidationIssue>());
        const string operation = "diagnosis_pending_roundtrip";
        var prepared = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            operation, "inspect_explicit_method", fixture.EventRef);
        Assert.True(prepared.IsValid, JsonSerializer.Serialize(prepared.Issues));
        var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Equal(natural, check.NaturalRoll);
        Assert.Equal(fate, check.PreparedCriticalReaction is not null);
        Assert.True(owner.HasLiveProcedureReservationAgreement(check));
        var input = owner.ReadDiagnosisOriginalInput();
        MortalWoundDiagnosisInputCodec.RequireRequestMatches(input, request);
        var unchanged = fixture.CaptureJson();
        var serialized = Assert.IsType<JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request));
        var originalSerialized = serialized.ToJsonString();

        // The complete real request must cross the existing strict parser before
        // any envelope assertion. These detached values issue no permission.
        var issues = new List<ValidationIssue>();
        Assert.True(MortalWoundTreatmentCommandCodec.TryParseRequest(serialized, "diagnosis.request", issues, out var detached),
            JsonSerializer.Serialize(issues));
        Assert.Empty(issues);
        var parsed = Assert.IsType<MortalWoundTreatmentAttemptRequest>(detached);
        var roundTrip = Assert.IsType<JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(parsed));
        Assert.True(MortalWoundTreatmentCommandCodec.SemanticJsonEquals(serialized, roundTrip));
        Assert.Equal(originalSerialized, serialized.ToJsonString());
        Assert.Equal(request.RequestFingerprint, parsed.RequestFingerprint);
        Assert.Equal("diagnosis", parsed.Coordinates.Purpose);
        Assert.True(MortalWoundTreatmentDetachedSealValidator.IsValid(parsed));

        var pending = BareDiagnosisPending(request, serialized);
        var pendingBefore = pending.ToJsonString();
        var inputBefore = input.ToJsonString();
        var accepted = Catalog(input, pending, history);
        Assert.True(accepted.IsValid, JsonSerializer.Serialize(accepted.Issues));
        Assert.Empty(accepted.FinalizedRequests);
        Assert.Equal(request.RequestFingerprint, Assert.Single(accepted.Requests).RequestFingerprint);
        var held = Assert.Single(accepted.HeldRequests);
        Assert.True(MortalWoundTreatmentCommandCodec.SemanticJsonEquals(serialized,
            Assert.IsType<JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(held))));

        // A complete comparison History is detached DATA, not a fabricated
        // accepted receipt or canonical publication. It exposes coalescing bypasses.
        var matchingHistory = await CreateDiagnosisCatalogComparisonHistoryAsync(fixture, request);
        var finalized = Catalog(null, null, matchingHistory);
        Assert.True(finalized.IsValid, JsonSerializer.Serialize(finalized.Issues));
        Assert.Empty(finalized.HeldRequests);
        Assert.Equal(request.RequestFingerprint, Assert.Single(finalized.FinalizedRequests).RequestFingerprint);

        var staged = MortalWoundDiagnosisInputCodec.Read(input);
        var mismatchedInput = MortalWoundDiagnosisInputCodec.Create(staged.Frame with { RequestId = "different_diagnosis_request" },
            staged.Selection, staged.Context);
        foreach (var comparisonHistory in new[] { history, matchingHistory })
        {
            Refused(null, pending, comparisonHistory, "mortal_wound_treatment_persisted_pending_invalid");
            Refused(mismatchedInput, pending, comparisonHistory, "mortal_wound_diagnosis_hold_input_mismatch");
        }
        var malformedInput = input.DeepClone().AsObject();
        malformedInput["inputFingerprint"] = request.RequestFingerprint;
        Refused(malformedInput, pending, history, "mortal_wound_diagnosis_input_invalid");

        foreach (var fault in new[] { "nonempty_packet", "nonempty_receipt", "malformed_packet", "malformed_receipt",
                     "duplicate", "empty", "root_session", "root_request", "root_snapshot", "operation", "attempt", "fingerprint",
                     "jointRepairCheckpoint", "jointInterfacePublication" })
        {
            var changed = pending.DeepClone().AsObject();
            var row = Assert.IsType<JsonObject>(changed["submittedTreatmentRequests"]![0]);
            switch (fault)
            {
                case "nonempty_packet": changed["repairPackets"]!.AsArray().Add(new JsonObject()); break;
                case "nonempty_receipt": changed["repairReceipts"]!.AsArray().Add(new JsonObject()); break;
                case "malformed_packet": changed["repairPackets"] = new JsonObject(); break;
                case "malformed_receipt": changed["repairReceipts"] = null; break;
                case "duplicate": changed["submittedTreatmentRequests"]!.AsArray().Add(row.DeepClone()); break;
                case "empty": changed["submittedTreatmentRequests"]!.AsArray().Clear(); break;
                case "root_session": changed["sessionId"] = "different_session"; break;
                case "root_request": changed["requestId"] = "different_request"; break;
                case "root_snapshot": changed["snapshotToken"] = "different_snapshot"; break;
                case "operation": row["operationKey"] = "different_operation"; break;
                case "attempt": row["attemptId"] = "different_attempt"; break;
                case "fingerprint": row["requestFingerprint"] = request.RouteSourceWoundFingerprint; break;
                default: changed[fault] = null; break;
            }
            var expected = fault.StartsWith("root_", StringComparison.Ordinal)
                ? "mortal_wound_treatment_persisted_root_binding_mismatch"
                : fault is "operation" or "attempt" or "fingerprint"
                    ? "mortal_wound_treatment_persisted_pending_coordinate_mismatch"
                    : "mortal_wound_treatment_persisted_pending_invalid";
            Refused(input, changed, history, expected);
        }

        // This is a separately valid ordinary request, rather than a mode edit
        // under invalid seals. It cannot use the diagnosis-only empty-wave form.
        var ordinaryPrepared = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, owner.CurrentWound,
            "ordinary_pending_control", "inspect_explicit_method", fixture.EventRef);
        Assert.True(ordinaryPrepared.IsValid, JsonSerializer.Serialize(ordinaryPrepared.Issues));
        var ordinary = Assert.IsType<MortalWoundTreatmentAttemptRequest>(ordinaryPrepared.Request);
        var ordinaryJson = Assert.IsType<JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(ordinary));
        var ordinaryPending = BareDiagnosisPending(ordinary, ordinaryJson);
        Refused(input, ordinaryPending, history, "mortal_wound_diagnosis_hold_input_mismatch");
        Refused(null, ordinaryPending, history, "mortal_wound_treatment_persisted_pending_invalid");
        var mixed = pending.DeepClone().AsObject();
        mixed["submittedTreatmentRequests"]!.AsArray().Add(ordinaryPending["submittedTreatmentRequests"]![0]!.DeepClone());
        Refused(input, mixed, history, "mortal_wound_treatment_persisted_pending_invalid");
        Assert.True(ordinary.RollbackNewProvisionalClaims(owner));

        Assert.Equal(inputBefore, input.ToJsonString());
        Assert.Equal(pendingBefore, pending.ToJsonString());
        Assert.Equal(originalSerialized, serialized.ToJsonString());
        Assert.True(owner.HasLiveProcedureReservationAgreement(check));
        var retry = MortalWoundTreatmentPlanner.PrepareDiagnosisRequest(owner, history, owner.CurrentWound,
            operation, "inspect_explicit_method", fixture.EventRef);
        Assert.True(retry.IsValid, JsonSerializer.Serialize(retry.Issues));
        Assert.True(MortalWoundTreatmentCommandCodec.SemanticJsonEquals(serialized,
            Assert.IsType<JsonObject>(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(retry.Request))));
        fixture.AssertJson(unchanged);

        static MortalWoundTreatmentPersistedRequestCatalogResult Catalog(JsonObject? command, JsonObject? root, WoundHistoryParseResult rows) =>
            MortalWoundTreatmentPersistedRequestCatalog.Parse(command is null ? null : JsonSerializer.SerializeToElement(command),
                root is null ? null : JsonSerializer.SerializeToElement(root), rows);

        static void Refused(JsonObject? command, JsonObject root, WoundHistoryParseResult rows, string code)
        {
            var result = Catalog(command, root, rows);
            Assert.False(result.IsValid);
            Assert.Contains(result.Issues, issue => issue.Code == code);
            Assert.Empty(result.Requests); Assert.Empty(result.HeldRequests); Assert.Empty(result.FinalizedRequests);
        }
    }

    private static JsonObject BareDiagnosisPending(MortalWoundTreatmentAttemptRequest request, JsonObject serialized) => new()
    {
        ["schemaVersion"] = 1, ["sessionId"] = request.Coordinates.SessionId,
        ["requestId"] = request.Coordinates.RequestId, ["snapshotToken"] = request.Coordinates.SnapshotToken,
        ["repairPackets"] = new JsonArray(), ["repairReceipts"] = new JsonArray(),
        ["submittedTreatmentRequests"] = new JsonArray(new JsonObject
        {
            ["operationKey"] = request.Coordinates.OperationKey, ["attemptId"] = request.Coordinates.AttemptId,
            ["requestFingerprint"] = request.RequestFingerprint, ["request"] = serialized.DeepClone()
        })
    };

    private static async Task<WoundHistoryParseResult> CreateDiagnosisCatalogComparisonHistoryAsync(DiagnosisOwnershipFixture fixture,
        MortalWoundTreatmentAttemptRequest request)
    {
        var owner = fixture.Authority;
        var outcome = MortalWoundDiagnosisOutcome.Resolve(owner, new(owner.History, Array.Empty<ValidationIssue>()), request);
        var working = MortalWoundDiagnosisWorkingProjection.Build(owner, outcome);
        var calculation = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .PrepareDiagnosisOutcomeAsync(fixture.Lease, working);
        var result = MortalWoundDiagnosisPersistedResult.CreateComparisonEvidence(owner, outcome, working, calculation,
            working.Members.ToDictionary(member => member.Original.WoundId,
                _ => (IReadOnlyList<MortalWoundRecoveryPublishedStage>)Array.Empty<MortalWoundRecoveryPublishedStage>(), StringComparer.Ordinal));
        var coordinates = request.Coordinates;
        var knowledge = result.KnowledgeAfter;
        const string summary = "The exact declared diagnosis is retained as comparison DATA.";
        var row = new WoundHistoryTransition(knowledge.LastTransition.TransitionId, knowledge.WoundId,
            owner.History.NextOrdinal, knowledge.LastTransition.Ordinal, "diagnose", coordinates.Turn,
            coordinates.EventRef, coordinates.OperationKey, WoundIdentityState.ComputeSemanticFingerprint(result.Original),
            WoundIdentityState.ComputeSemanticFingerprint(knowledge), result.WorkingFingerprint, coordinates.AttemptId,
            null, null, null, null, WoundHistoryState.ComputeOutputFingerprint(coordinates.OperationKey, coordinates.EventRef, summary),
            summary, false, result);
        var complete = WoundHistoryState.CreateValidated(owner.History.NextOrdinal + 1, owner.History.Transitions.Append(row));
        Assert.True(complete.IsValid, JsonSerializer.Serialize(complete.Issues));
        var parsed = WoundHistoryState.Parse(WoundHistoryState.SerializeCanonical(complete.State!), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
        return parsed;
    }
}
