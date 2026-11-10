using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Rejects a B-like envelope whose token matches the current private pair but whose fields require unaccepted A history.
    /// </summary>
    /// <returns>
    /// A task completing after a forged public B and complete A-plus-B draft fail without mutating private authority.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualStagedHistory_ForgedBTransportWithoutAcceptedAIsRejected()
    {
        var context = await CreateSpiritualStagedContinuationContextAsync();
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var history = await CommitSpiritualStagedHistoryAAsync(context);
        await context.FileSystem.WriteFileAtomicBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath, history.BeforeCheckpoint);
        var actualPending = (await context.FileSystem.ReadFileBytesAsync(SpiritualWoundDecisionPendingState.StatePath))!;
        var forgedB = history.B with
        {
            ContinuationId = ValidationService.SpiritualOriginalTurnCapture.DependentContextFingerprint(
                history.BeforeCheckpoint, actualPending, history.B.DependentDraftFields)
        };
        Assert.NotEqual(history.B.ContinuationId, forgedB.ContinuationId);
        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(forgedB));
        await context.FileSystem.WriteFileAtomicBytesAsync(AfterlifeSpiritualConflictState.StatePath,
            Encoding.UTF8.GetBytes(CreateSpiritualStagedCorrectionB(history.AcceptedA).ToJsonString()));
        var turn = (await context.ReadJsonAsync("input/turn_request.json"))!;
        await context.WriteExactJsonAsync("game_state/control/validation_repair_request.json", new JsonObject
        {
            ["sessionId"] = turn["sessionId"]!.DeepClone(), ["requestId"] = turn["requestId"]!.DeepClone(),
            ["turnNumber"] = turn["turnNumber"]!.DeepClone(),
            ["spiritualWoundContinuation"] = JsonNode.Parse(GmWorkerJson.Serialize(forgedB))
        }.ToJsonString());
        var files = await ReadSpiritualContinuationTransportFilesAsync(context);
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var cold = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);
        await using var lease = await coldFs.AcquireCanonicalWriteLeaseAsync();
        var evaluated = await cold.EvaluateSpiritualWoundContinuationDraftAsync(lease, forgedB,
            CreateSpiritualStagedEmptyResponse(forgedB));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, scenario = "forged_b_without_a",
            genuineA = history.A.ContinuationId, genuineB = history.B.ContinuationId, forgedB = forgedB.ContinuationId,
            disposition = evaluated.Disposition.ToString(), evaluated.Issues }));
        Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Rejected, evaluated.Disposition);
        Assert.Null(evaluated.NextRequest);
        var resolvedIssues = await cold.ValidateSpiritualWoundContinuationDraftAsync(lease, forgedB,
            CreateSpiritualStagedEmptyResponse(forgedB), new Dictionary<string, byte[]?>(), requireResolvedDraft: true);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { scenario = "forged_b_strict_resolution", issues = resolvedIssues }));
        Assert.NotEmpty(resolvedIssues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, files);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { scenario = "forged_b_without_a", TransportBytesPreserved = true }));
    }

    /// <summary>
    /// Freezes the actual accepted critical narration when a fresh owner recovers B.
    /// </summary>
    /// <param name="field">
    /// Accepted GM text replaced with another nonempty, arithmetically lawful sentence.
    /// </param>
    /// <returns>
    /// A task completing after the altered A text cannot be admitted as part of an otherwise valid B correction.
    /// </returns>
    [Theory]
    [InlineData("scaleLimit")]
    [InlineData("narrativeConstraint")]
    public async Task OriginalSpiritualStagedHistory_ColdBRejectsChangedAcceptedCriticalText(string field)
    {
        var history = await CreatePreparedSpiritualStagedContextAsync(acceptedA: true);
        var context = history.Context;
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(history.B);
        var candidate = CreateSpiritualStagedCorrectionB(history.AcceptedA);
        candidate["activeConflict"]!["exchangeLog"]![1]!["diceAudit"]!["criticalResult"]![field] =
            "Иное допустимое описание, которое не было принято ответом A.";
        await context.FileSystem.WriteFileAtomicBytesAsync(AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(candidate.ToJsonString()));
        var files = await ReadSpiritualContinuationTransportFilesAsync(context);
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var cold = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);
        await using var lease = await coldFs.AcquireCanonicalWriteLeaseAsync();
        var result = await cold.EvaluateSpiritualWoundContinuationDraftAsync(lease, b,
            CreateSpiritualStagedEmptyResponse(b));
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, scenario = "accepted_critical_text",
            field, a = history.A.ContinuationId, b = b.ContinuationId,
            disposition = result.Disposition.ToString(), result.Issues }));
        Assert.Equal(ValidationService.SpiritualWoundContinuationDisposition.Rejected, result.Disposition);
        Assert.NotEmpty(result.Issues);
        Assert.Null(result.NextRequest);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, files);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { scenario = "accepted_critical_text", field, TransportBytesPreserved = true }));
    }

    /// <summary>
    /// Replays real accepted A history and rejects malformed, rehashed or broadened replacements independently of public transport.
    /// </summary>
    /// <param name="mutation">
    /// One closed-shape, ordering, ownership, policy or stored-candidate violation applied to a genuinely committed row.
    /// </param>
    /// <returns>
    /// A task completing after cold private replay blocks and leaves every physical session file unchanged.
    /// </returns>
    [Theory]
    [InlineData("unknown_field")]
    [InlineData("empty_progress")]
    [InlineData("ordinal_gap")]
    [InlineData("reordered_rows")]
    [InlineData("wrong_owner")]
    [InlineData("broadened_fields")]
    [InlineData("foreign_path")]
    [InlineData("content_fingerprint")]
    [InlineData("future_b_image")]
    public async Task OriginalSpiritualStagedHistory_ColdReplayRejectsInvalidJournal(string mutation)
    {
        var history = await CreatePreparedSpiritualStagedContextAsync(acceptedA: true);
        var context = history.Context;
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(history.B);
        var root = JsonNode.Parse(history.AfterCheckpoint!)!.AsObject();
        var body = root["checkpoint"]!;
        var rows = body["pendingSubmission"]!["dependentDraftProgress"]!.AsArray();
        var row = Assert.Single(rows)!.AsObject();
        switch (mutation)
        {
            case "unknown_field": row["gmApproved"] = true; break;
            case "empty_progress": rows.Clear(); break;
            case "ordinal_gap": row["ordinal"] = 2; break;
            case "reordered_rows":
                var duplicate = row.DeepClone();
                duplicate["ordinal"] = 2;
                rows.Insert(0, duplicate);
                break;
            case "wrong_owner":
            {
                var foreign = await CreateSpiritualStagedContinuationContextAsync();
                using var foreignOwned = new OriginalFixtureCompletion(foreign.RootPath,
                    () => foreign.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
                await using (var foreignLease = await foreign.FileSystem.AcquireCanonicalWriteLeaseAsync())
                    row["acceptedContinuationId"] = (await foreign.Validator.ReadSpiritualWoundContinuationAsync(foreignLease)).Request!.ContinuationId;
                Assert.NotEqual(history.A.ContinuationId, row["acceptedContinuationId"]!.GetValue<string>());
                break;
            }
            case "broadened_fields":
                row["dependentDraftFields"]!.AsArray().Add(new JsonObject
                {
                    ["path"] = b.DependentDraftFields[0].Path,
                    ["jsonPointer"] = b.DependentDraftFields[0].JsonPointer
                });
                break;
            case "foreign_path": row["inputChanges"]![0]!["path"] = ResourceMaterializationContract.StatePath; break;
            case "content_fingerprint": row["inputChanges"]![0]!["contentFingerprint"] = "sha256:" + new string('0', 64); break;
            case "future_b_image":
                var image = Assert.Single(row["inputChanges"]!.AsArray(), change => change!["path"]!.GetValue<string>() == AfterlifeSpiritualConflictState.StatePath)!;
                var bytes = Encoding.UTF8.GetBytes(CreateSpiritualStagedCorrectionB(history.AcceptedA).ToJsonString());
                image["contentBase64"] = Convert.ToBase64String(bytes);
                image["contentFingerprint"] = new CanonicalBeforeImage(true, bytes).Fingerprint;
                break;
            default: throw new InvalidOperationException(mutation);
        }
        body["checkpointFingerprint"] = SpiritualWoundStateJson.Hash(body.AsObject(), "spiritual_capture_checkpoint_v1", "checkpointFingerprint");
        await context.FileSystem.WriteFileAtomicBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath,
            Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(root)));
        var files = await ReadSpiritualContinuationTransportFilesAsync(context);
        var coldFs = new FileSystemManager(context.RootPath, NullLogger<FileSystemManager>.Instance);
        var cold = new ValidationService(coldFs, NullLogger<ValidationService>.Instance);
        await using var lease = await coldFs.AcquireCanonicalWriteLeaseAsync();
        var opened = await cold.OpenC2PrivateSessionAsync(lease);
        using var owner = opened.Session;
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, scenario = "invalid_journal",
            mutation, a = history.A.ContinuationId, b = b.ContinuationId, opened.Disposition, opened.Issues }));
        Assert.Equal("blocked", opened.Disposition);
        Assert.NotEmpty(opened.Issues);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, files);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { scenario = "invalid_journal", mutation, TransportBytesPreserved = true }));
    }

    /// <summary>
    /// Refuses proposed writes to the client-owned journal even with a valid B candidate and broad worker reservation.
    /// </summary>
    /// <returns>
    /// A task completing after both public proposal validation and the real reserved worker apply gate reject without writes.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualStagedHistory_GmAndWorkerCannotWriteAcceptedJournal()
    {
        var prepared = await CreatePreparedSpiritualStagedContextAsync(acceptedA: true);
        var context = prepared.Context;
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(prepared.B);
        var afterCheckpoint = Assert.IsType<byte[]>(prepared.AfterCheckpoint);
        var candidate = Encoding.UTF8.GetBytes(CreateSpiritualStagedCorrectionB(prepared.AcceptedA).ToJsonString());
        var response = CreateSpiritualStagedEmptyResponse(b);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            Assert.Empty(await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, b, response,
                new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = candidate }));
            var files = await ReadSpiritualContinuationTransportFilesAsync(context);
            var privateWriteIssues = await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, b, response,
                new Dictionary<string, byte[]?> { [AfterlifeSpiritualConflictState.StatePath] = candidate,
                    [SpiritualWoundCaptureCheckpointState.StatePath] = afterCheckpoint });
            _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, scenario = "gm_private_journal_write",
                a = prepared.A.ContinuationId, b = b.ContinuationId, issues = privateWriteIssues }));
            Assert.NotEmpty(privateWriteIssues);
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, files);
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, candidate);
        }
        // Keep the legal B candidate on disk and reserve only the forbidden private proposal.
        // Otherwise the afterlife task-shape guard rejects the mixed paths before the continuation gate is exercised.
        var packet = await ReserveSpiritualContinuationWorkerAsync(context, b, response,
            new Dictionary<string, byte[]> { [SpiritualWoundCaptureCheckpointState.StatePath] = afterCheckpoint });
        var beforeApply = await ReadSpiritualContinuationTransportFilesAsync(context);
        var gate = new GmWorkerApplyGate(context.FileSystem,
            () => throw new InvalidOperationException("Private journal proposal reached ordinary validation."));
        var applied = await gate.ApplyReservedAsync(packet.Proposal, packet.Profile, packet.Task.SessionGeneration);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, scenario = "worker_private_journal_write",
            result = applied.Result.ToString(), applied.AppliedFiles, applied.RejectionReasons }));
        Assert.Equal(ApplyGateResult.Rejected, applied.Result);
        Assert.Empty(applied.AppliedFiles);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, beforeApply);
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { scenario = "gm_and_worker_private_journal_write",
            TransportBytesPreserved = true, AppliedFileCount = applied.AppliedFiles.Count }));
    }

    /// <summary>
    /// Commits a real A response before returning its old and new checkpoints for negative history controls.
    /// </summary>
    /// <param name="context">
    /// Signed three-exchange session with its original saved player choice and no accepted dependent progress yet.
    /// </param>
    /// <param name="correctedA">
    /// Detached lawful A already prepared for the session, or null to derive it from the original physical draft.
    /// </param>
    /// <returns>
    /// Actual issued A and B, exact before/after checkpoint bytes, and the detached accepted A draft.
    /// </returns>
    private static async Task<(SpiritualWoundContinuationRequest A, SpiritualWoundContinuationRequest B,
        byte[] BeforeCheckpoint, byte[] AfterCheckpoint, JsonObject AcceptedA)> CommitSpiritualStagedHistoryAAsync(
        ResourceMaterializationTestContext context, JsonObject? correctedA = null)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var a = Assert.IsType<SpiritualWoundContinuationRequest>((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request);
        var before = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!;
        var accepted = correctedA?.DeepClone().AsObject() ?? CreateSpiritualStagedCorrectionA(
            Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, Encoding.UTF8.GetBytes(accepted.ToJsonString()));
        var result = await context.Validator.CommitSpiritualWoundDependentProgressAsync(lease, a, CreateSpiritualStagedEmptyResponse(a));
        Assert.True(result.Disposition == "committed", string.Join("\n", result.Issues));
        Interlocked.Increment(ref _spiritualStagedAcceptedPreparationCount);
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(result.NextRequest);
        Assert.NotEqual(a.ContinuationId, b.ContinuationId);
        Assert.Equal(JsonSerializer.Serialize(b), JsonSerializer.Serialize((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request));
        var after = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!;
        var pre = JsonNode.Parse(before)!["checkpoint"]!;
        var post = JsonNode.Parse(after)!["checkpoint"]!;
        foreach (var name in new[] { "allocations", "advances", "committedAdvance", "expectedPendingPacketFingerprint" })
            Assert.True(JsonNode.DeepEquals(pre[name], post[name]), name);
        foreach (var name in new[] { "stagedDecision", "command", "allocations", "priorCommittedAdvance", "priorPendingPacketFingerprint" })
            Assert.True(JsonNode.DeepEquals(pre["pendingSubmission"]![name], post["pendingSubmission"]![name]), name);
        var progress = Assert.Single(post["pendingSubmission"]!["dependentDraftProgress"]!.AsArray())!;
        Assert.Equal(a.ContinuationId, progress["acceptedContinuationId"]!.GetValue<string>());
        var fields = progress["dependentDraftFields"]!.AsArray();
        Assert.Equal(a.DependentDraftFields.Count, fields.Count);
        for (var index = 0; index < fields.Count; index++)
        {
            Assert.Equal(a.DependentDraftFields[index].Path, fields[index]!["path"]!.GetValue<string>());
            Assert.Equal(a.DependentDraftFields[index].JsonPointer, fields[index]!["jsonPointer"]!.GetValue<string>());
        }
        return (a, b, before, after, accepted);
    }
}
