using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Classifies failed checkpoint publication by exact readback and never activates B from an old or ambiguous image.
    /// </summary>
    /// <param name="readback">
    /// Whether the injected write failure leaves the old checkpoint, the complete new checkpoint, or unrelated bytes.
    /// </param>
    /// <returns>
    /// A task completing after durable classification, unchanged non-checkpoint files and safe retry behavior are checked.
    /// </returns>
    [Theory]
    [InlineData("old")]
    [InlineData("new")]
    [InlineData("ambiguous")]
    public async Task OriginalSpiritualStagedHistory_ProgressCommitClassifiesWriteFailure(string readback)
    {
        var prepared = await CreatePreparedSpiritualStagedContextAsync(acceptedA: false);
        await using var context = prepared.Context;
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var a = Assert.IsType<SpiritualWoundContinuationRequest>((await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request);
        var response = CreateSpiritualStagedEmptyResponse(a);
        Assert.Equal(JsonSerializer.Serialize(prepared.A), JsonSerializer.Serialize(a));
        Assert.True(JsonNode.DeepEquals(prepared.AcceptedA, await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath)));
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var oldCheckpoint = (await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!;
        byte[]? attempted = null;
        byte[] ambiguous = Encoding.UTF8.GetBytes("{\"schemaVersion\":1,\"checkpoint\":{\"unexpected\":true}}");
        var writes = 0;
        var result = await context.Validator.CommitSpiritualWoundDependentProgressAsync(lease, a, response,
            writeOverrideAsync: async (writeLease, path, bytes) =>
            {
                Assert.Equal(SpiritualWoundCaptureCheckpointState.StatePath, path);
                Assert.Same(lease, writeLease);
                writes++;
                attempted = bytes.ToArray();
                if (readback == "new") await context.FileSystem.WriteFileAtomicBytesAsync(writeLease, path, bytes);
                if (readback == "ambiguous") await context.FileSystem.WriteFileAtomicBytesAsync(writeLease, path, ambiguous);
                throw new IOException("Injected checkpoint publication failure with " + readback + " readback.");
            });
        Assert.Equal(1, writes);
        Assert.NotNull(attempted);
        Assert.Equal(readback == "old" ? "not_committed" : readback == "new" ? "committed" : "blocked", result.Disposition);
        var after = await ReadSpiritualContinuationTransportFilesAsync(context);
        Assert.Equal(before.Keys.Order(StringComparer.Ordinal), after.Keys.Order(StringComparer.Ordinal));
        var checkpointKey = Path.GetRelativePath(context.FileSystem.GameSessionPath, context.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath));
        foreach (var pair in before.Where(pair => pair.Key != checkpointKey)) Assert.Equal(pair.Value, after[pair.Key]);
        Assert.Equal(readback == "old" ? oldCheckpoint : readback == "new" ? attempted : ambiguous,
            await context.FileSystem.ReadFileBytesAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath));
        if (readback == "ambiguous")
        {
            Assert.Null(result.NextRequest);
            var blocked = await context.Validator.OpenC2PrivateSessionAsync(lease);
            using var blockedOwner = blocked.Session;
            Assert.Equal("blocked", blocked.Disposition);
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, after);
            return;
        }
        if (readback == "old")
        {
            Assert.Null(result.NextRequest);
            Assert.Equal(a.ContinuationId, (await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request!.ContinuationId);
            result = await context.Validator.CommitSpiritualWoundDependentProgressAsync(lease, a, response);
            Assert.Equal("committed", result.Disposition);
        }
        var b = Assert.IsType<SpiritualWoundContinuationRequest>(result.NextRequest);
        Assert.NotEqual(a.ContinuationId, b.ContinuationId);
        var committed = await ReadSpiritualContinuationTransportFilesAsync(context);
        var replayed = await context.Validator.CommitSpiritualWoundDependentProgressAsync(lease, a, response);
        Assert.NotEqual("committed", replayed.Disposition);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, committed);
        var body = (await context.ReadJsonAsync(SpiritualWoundCaptureCheckpointState.StatePath))!["checkpoint"]!;
        Assert.Single(body["pendingSubmission"]!["dependentDraftProgress"]!.AsArray());
        Assert.Equal(b.ContinuationId, (await context.Validator.ReadSpiritualWoundContinuationAsync(lease)).Request!.ContinuationId);
    }

    /// <summary>
    /// Reauthenticates the actual checkpoint after a worker applies valid A and before its Ready can be published.
    /// </summary>
    /// <returns>
    /// A task completing after a genuine foreign checkpoint suppresses Ready while its replacement and applied draft remain intact.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task OriginalSpiritualStagedHistory_CheckpointReplacementBeforeWorkerReadyIsRejected()
    {
        await using var context = await CreateSpiritualStagedContinuationContextAsync();
        await using var foreign = await CreateSpiritualStagedContinuationContextAsync();
        var replacement = (await foreign.FileSystem.ReadFileBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath))!;
        var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
        var original = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
        var candidate = Encoding.UTF8.GetBytes(CreateSpiritualStagedCorrectionA(original).ToJsonString());
        var profile = CreateSpiritualStagedSingleCorrectionWorkerProfile(context, candidate);
        Dictionary<string, byte[]?>? afterReplacement = null;
        var readyHooks = 0;
        var hooks = new GmWorkerValidationRepairDelegatorHooks
        {
            BeforeReadyPublicationAsync = async () =>
            {
                readyHooks++;
                Assert.Equal(candidate, await context.FileSystem.ReadFileBytesAsync(AfterlifeSpiritualConflictState.StatePath));
                await context.FileSystem.WriteFileAtomicBytesAsync(SpiritualWoundCaptureCheckpointState.StatePath, replacement);
                afterReplacement = await ReadSpiritualDispatchRetainedFilesAsync(context);
            }
        };
        var result = await CreateSpiritualDispatchDelegator(context, hooks).TryRunAsync([profile], boundary.Issues,
            boundary.Turn, "2026-08-15T00:02:00Z", 3, expectedSessionGeneration: boundary.Generation,
            spiritualWoundContinuation: boundary.Request);
        Assert.True(result.ApplyDecision?.Result == ApplyGateResult.Accepted,
            result.FallbackReason + Environment.NewLine + result.RunResult?.StandardError);
        Assert.Equal(1, readyHooks);
        Assert.NotNull(afterReplacement);
        Assert.False(result.ReadySignalCreated);
        Assert.NotEmpty(result.FallbackReason);
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(GmWorkerValidationRepairDelegator.ValidationRepairReadyPath));
        await AssertSpiritualDispatchRetainedFilesAsync(context, afterReplacement!);
    }

    /// <summary>
    /// Creates a bounded actual worker process that proposes one supplied candidate against its real task hashes and correlation.
    /// </summary>
    /// <param name="context">
    /// Fixture root receiving a temporary process script outside canonical session files.
    /// </param>
    /// <param name="candidate">
    /// Exact A-only conflict bytes produced by the shared lawful fixture helper.
    /// </param>
    /// <returns>
    /// The established repair worker profile with a ten-second process budget and no direct canonical writes.
    /// </returns>
    private static WorkerBridgeProfile CreateSpiritualStagedSingleCorrectionWorkerProfile(ResourceMaterializationTestContext context, byte[] candidate)
    {
        var script = Path.Combine(context.RootPath, "spiritual-staged-single-correction.ps1");
        File.WriteAllText(script, $$"""
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $request = $task.spiritualWoundContinuation
            if ($request.phase -ne 'dependent_draft' -or $null -ne $request.offer) { throw 'Expected genuine saved-choice dependent request.' }
            $path = '{{AfterlifeSpiritualConflictState.StatePath}}'
            $proposalId = 'proposal_' + $task.taskId
            $contentRef = 'worker_proposals/' + $proposalId + '/' + $path
            $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
            New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
            [IO.File]::WriteAllBytes($contentPath, [Convert]::FromBase64String('{{Convert.ToBase64String(candidate)}}'))
            $before = @($task.contextFiles | Where-Object { $_.path -eq $path })[0]
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = $proposalId; taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Corrected the actual current dependent group.'
                changedFiles = @([ordered]@{ path = $path; changeKind = 'replace'; beforeSha256 = $before.sha256; afterSha256 = (Get-FileHash -LiteralPath $contentPath -Algorithm SHA256).Hash.ToLowerInvariant(); contentRef = $contentRef })
                findings = @()
                spiritualWoundContinuation = [ordered]@{ schemaVersion = 1; continuationId = $request.continuationId; woundDecisions = @() }
                selfCheck = [ordered]@{ scopeReviewed = $true; validationExpectedToPass = $true; notes = @() }
                createdAtUtc = [DateTime]::UtcNow.ToString('O')
            }
            $proposal | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            """);
        return GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile() with
        {
            LaunchCommand = $"pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{script}\"",
            TimeoutSeconds = 10
        };
    }
}
