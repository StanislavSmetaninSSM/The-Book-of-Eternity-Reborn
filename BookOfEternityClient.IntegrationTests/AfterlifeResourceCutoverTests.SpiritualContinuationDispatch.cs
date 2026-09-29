using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Refuses worker dispatch when another response already awaits consumption at the current continuation.
    /// </summary>
    /// <returns>
    /// A task completing after no worker runs and every session file except the refusal audit remains exact.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task OriginalSpiritualContinuationDispatch_PreexistingReadyCannotBecomeOverwriteBaseline()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
        var profile = CreateSpiritualDispatchWorkerProfile(context);
        var response = CreateSpiritualContinuationDraftNoneResponse(boundary.Request);
        await context.FileSystem.WriteFileAtomicAsync(GmWorkerValidationRepairDelegator.ValidationRepairReadyPath,
            GmWorkerJson.Serialize(new
            {
                sessionId = boundary.Turn.SessionId,
                requestId = boundary.Turn.RequestId,
                turnNumber = boundary.Turn.TurnNumber,
                status = "success",
                timestamp = "2026-08-15T00:03:00Z",
                spiritualWoundContinuation = response
            }));
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var delegator = CreateSpiritualDispatchDelegator(context);

        var result = await delegator.TryRunAsync([profile], [], boundary.Turn, "2026-08-15T00:02:00Z", 1,
            expectedSessionGeneration: boundary.Generation, spiritualWoundContinuation: boundary.Request);

        Assert.Equal(GmWorkerValidationRepairOutcome.TaskBuildFailed, result.Outcome);
        Assert.Null(result.RunResult);
        Assert.False(result.ReadySignalCreated);
        Assert.Contains("Ready", result.FallbackReason, StringComparison.Ordinal);
        var audit = Assert.Single(await new GmWorkerAuditLog(context.FileSystem).ReadEventsAsync());
        Assert.Equal("validation-repair-task-build-failed", audit.EventType);
        var auditPath = Path.GetRelativePath(context.FileSystem.GameSessionPath,
            Path.Combine(context.FileSystem.GameSessionPath, GmWorkerAuditLog.AuditLogPath));
        before[auditPath] = (await context.FileSystem.ReadFileBytesAsync(GmWorkerAuditLog.AuditLogPath))!;
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }

    /// <summary>
    /// Dispatches an error-free current offer through a real worker reservation and copies its response into Ready.
    /// </summary>
    /// <returns>
    /// A task completing after the worker response is delivered without consuming its C2 decision.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task OriginalSpiritualContinuationDispatch_DecisionWithoutErrorsCreatesReadyWithoutAdvance()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
        Assert.Equal("decision", boundary.Request.Phase);
        Assert.Empty(boundary.Issues);
        var retained = await ReadSpiritualDispatchRetainedFilesAsync(context);
        var profile = CreateSpiritualDispatchWorkerProfile(context);
        var delegator = CreateSpiritualDispatchDelegator(context);

        var result = await delegator.TryRunAsync([profile], [], boundary.Turn, "2026-08-15T00:02:00Z", 1,
            expectedSessionGeneration: boundary.Generation, spiritualWoundContinuation: boundary.Request);

        Assert.True(result.Outcome == GmWorkerValidationRepairOutcome.Applied, result.FallbackReason + Environment.NewLine + result.RunResult?.StandardError);
        Assert.Equal(ApplyGateResult.Accepted, result.ApplyDecision?.Result);
        Assert.True(result.ReadySignalCreated);
        Assert.Empty(result.RunResult!.Proposal!.ChangedFiles);
        AssertSpiritualDispatchTask(result.Task!, boundary.Request, dependent: false);
        await AssertSpiritualDispatchReadyAsync(context, result, boundary.Request, boundary.Turn);
        await AssertSpiritualDispatchRetainedFilesAsync(context, retained);
    }

    /// <summary>
    /// Gives a dependent worker only narrative, exact conflict fields and required read-only realm context.
    /// </summary>
    /// <returns>
    /// A task completing after a real cost correction reaches Ready while the saved C2 choice remains unadvanced.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task OriginalSpiritualContinuationDispatch_DependentCorrectionUsesSafeContextAndEmptyDecisionResponse()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true, dependentCost: true);
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
            var selected = await session.SubmitDecisionAsync(lease,
                OriginalSpiritualWoundDecision(session.Offer!.OpportunityRef, "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            Assert.Equal("dependent_continuation", selected.Disposition);
            selected.Session?.Dispose();
        }
        var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
        Assert.Equal("dependent_draft", boundary.Request.Phase);
        Assert.NotEmpty(boundary.Issues);
        var retained = await ReadSpiritualDispatchRetainedFilesAsync(context);
        var profile = CreateSpiritualDispatchWorkerProfile(context);

        var result = await CreateSpiritualDispatchDelegator(context).TryRunAsync([profile], boundary.Issues,
            boundary.Turn, "2026-08-15T00:02:00Z", 2, expectedSessionGeneration: boundary.Generation,
            spiritualWoundContinuation: boundary.Request);

        Assert.True(result.Outcome == GmWorkerValidationRepairOutcome.Applied, result.FallbackReason + Environment.NewLine + result.RunResult?.StandardError);
        Assert.True(result.ReadySignalCreated);
        AssertSpiritualDispatchTask(result.Task!, boundary.Request, dependent: true);
        Assert.Equal(AfterlifeSpiritualConflictState.StatePath, Assert.Single(result.RunResult!.Proposal!.ChangedFiles).Path);
        Assert.Empty(result.RunResult.Proposal.SpiritualWoundContinuation!.WoundDecisions);
        await AssertSpiritualDispatchReadyAsync(context, result, boundary.Request, boundary.Turn);
        var conflict = await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath);
        Assert.Equal(3, conflict!["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"]!.GetValue<int>());
        Assert.Equal(0, conflict["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"]!.GetValue<int>());
        retained.Remove(AfterlifeSpiritualConflictState.StatePath);
        await AssertSpiritualDispatchRetainedFilesAsync(context, retained);
        var corrected = await ReadSpiritualDispatchBoundaryAsync(context);
        Assert.Equal(boundary.Request.ContinuationId, corrected.Request.ContinuationId);
        Assert.Empty(corrected.Issues);
    }

    /// <summary>
    /// Reauthenticates C2 before Ready even when the session generation stays fixed and a newer Ready exists.
    /// </summary>
    /// <returns>
    /// A task completing after stale publication is suppressed both with and without a newer correlated response.
    /// </returns>
    [Fact]
    [Trait("Category", "ProcessIntegration")]
    public async Task OriginalSpiritualContinuationDispatch_AdvanceBeforeReadyPreservesNewerResponse()
    {
        foreach (var (advanceBoundary, publishNewerReady) in new[] { (true, false), (true, true), (false, true) })
        {
            await using var context = await CreateTwoSourceC2ContextAsync();
            // The lower-level classifier fixture omits the required output timestamp.
            // Supply the permitted scene timestamp before exercising complete worker draft preservation.
            await context.WriteExactJsonAsync(ProjectionNarrativePath,
                "{\"response\":\"Духовный обмен завершён.\",\"timestamp\":\"2026-08-15T00:00:00Z\"}");
            var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
            byte[]? newerReady = null;
            Dictionary<string, byte[]?>? afterAdvance = null;
            var hooks = new GmWorkerValidationRepairDelegatorHooks
            {
                BeforeReadyPublicationAsync = async () =>
                {
                    await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                    {
                        Assert.Equal(boundary.Generation, context.FileSystem.GetOrCreateSessionGeneration(lease));
                        if (advanceBoundary)
                        {
                            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
                            Assert.Equal("offer", opened.Disposition);
                            using var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session);
                            var advanced = await session.SubmitDecisionAsync(lease,
                                Assert.Single(CreateSpiritualContinuationDraftNoneResponse(boundary.Request).WoundDecisions), null);
                            Assert.Equal("offer", advanced.Disposition);
                            Assert.Empty(advanced.Issues);
                            advanced.Session?.Dispose();
                        }
                        var current = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
                        var request = Assert.IsType<SpiritualWoundContinuationRequest>(current.Request);
                        Assert.Equal(!advanceBoundary, boundary.Request.ContinuationId == request.ContinuationId);
                        var response = CreateSpiritualContinuationDraftNoneResponse(request);
                        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(request, response));
                        if (publishNewerReady)
                        {
                            newerReady = Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(new
                            {
                                sessionId = boundary.Turn.SessionId,
                                requestId = boundary.Turn.RequestId,
                                turnNumber = boundary.Turn.TurnNumber,
                                updatedAtUtc = "2026-08-15T00:03:00Z",
                                note = "Current second opportunity response.",
                                spiritualWoundContinuation = response
                            }));
                            await context.FileSystem.WriteFileAtomicBytesAsync(lease,
                                GmWorkerValidationRepairDelegator.ValidationRepairReadyPath, newerReady);
                        }
                    }
                    afterAdvance = await ReadSpiritualDispatchRetainedFilesAsync(context);
                }
            };
            var profile = CreateSpiritualDispatchWorkerProfile(context);

            var result = await CreateSpiritualDispatchDelegator(context, hooks).TryRunAsync([profile], [],
                boundary.Turn, "2026-08-15T00:02:00Z", 3, expectedSessionGeneration: boundary.Generation,
                spiritualWoundContinuation: boundary.Request);

            Assert.True(result.ApplyDecision?.Result == ApplyGateResult.Accepted,
                result.FallbackReason + Environment.NewLine + result.RunResult?.StandardError);
            Assert.NotNull(afterAdvance);
            Assert.False(result.ReadySignalCreated);
            Assert.NotEmpty(result.FallbackReason);
            Assert.Equal(newerReady, await context.FileSystem.ReadFileBytesAsync(
                GmWorkerValidationRepairDelegator.ValidationRepairReadyPath));
            await AssertSpiritualDispatchRetainedFilesAsync(context, afterAdvance!);
            var currentBoundary = await ReadSpiritualDispatchBoundaryAsync(context);
            Assert.Equal(boundary.Generation, currentBoundary.Generation);
            Assert.Equal(!advanceBoundary, boundary.Request.ContinuationId == currentBoundary.Request.ContinuationId);
        }
    }

    /// <summary>
    /// Reads the actual continuation, signed request identity and current session generation under one lease.
    /// </summary>
    /// <param name="context">
    /// Genuine C2 fixture awaiting a decision or dependent correction.
    /// </param>
    /// <returns>
    /// Detached public request, real diagnostics, original turn identity and session generation.
    /// </returns>
    private static async Task<(SpiritualWoundContinuationRequest Request, IReadOnlyList<ValidationIssue> Issues,
        WorkerTurnReference Turn, string Generation)> ReadSpiritualDispatchBoundaryAsync(ResourceMaterializationTestContext context)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        var request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        using var input = JsonDocument.Parse((await context.FileSystem.ReadFileBytesAsync(lease, "input/turn_request.json"))!);
        return (request, read.Issues, new WorkerTurnReference
        {
            SessionId = input.RootElement.GetProperty("sessionId").GetString()!,
            RequestId = input.RootElement.GetProperty("requestId").GetString()!,
            TurnNumber = input.RootElement.GetProperty("turnNumber").GetInt32()
        }, context.FileSystem.GetOrCreateSessionGeneration(lease));
    }

    /// <summary>
    /// Creates the production bridge, reservation store, apply gate and audit path with an optional Ready race hook.
    /// </summary>
    /// <param name="context">
    /// Canonical fixture used by every production component.
    /// </param>
    /// <param name="hooks">
    /// Optional hook executed after accepted apply and before Ready reauthentication.
    /// </param>
    /// <returns>
    /// The real worker delegator; no reservation or C2 admission is bypassed.
    /// </returns>
    private static GmWorkerValidationRepairDelegator CreateSpiritualDispatchDelegator(
        ResourceMaterializationTestContext context, GmWorkerValidationRepairDelegatorHooks? hooks = null)
    {
        var audit = new GmWorkerAuditLog(context.FileSystem);
        return new(context.FileSystem,
            new GmWorkerBridgePool(context.FileSystem, new GmWorkerProposalStore(context.FileSystem), audit),
            new GmWorkerApplyGate(context.Validator, audit), audit, hooks);
    }

    /// <summary>
    /// Checks that a dispatched task exposes only current safe continuation inputs and exact permitted output paths.
    /// </summary>
    /// <param name="task">
    /// Actual task produced and reserved by the delegator.
    /// </param>
    /// <param name="request">
    /// Genuine request supplied to dispatch.
    /// </param>
    /// <param name="dependent">
    /// Whether conflict correction and read-only realm authority are required alongside narrative.
    /// </param>
    private static void AssertSpiritualDispatchTask(WorkerTaskPacket task, SpiritualWoundContinuationRequest request, bool dependent)
    {
        Assert.Equal(GmWorkerJson.Serialize(request), GmWorkerJson.Serialize(task.SpiritualWoundContinuation));
        var allowed = dependent ? new[] { ProjectionNarrativePath, AfterlifeSpiritualConflictState.StatePath } : new[] { ProjectionNarrativePath };
        Assert.Equal(allowed.OrderBy(path => path), task.AllowedProposalPaths.OrderBy(path => path));
        Assert.Contains(task.ContextFiles, file => file.Path == ProjectionNarrativePath);
        Assert.All(task.ContextFiles, file => Assert.Contains(file.Path,
            new[] { ProjectionNarrativePath, AfterlifeSpiritualConflictState.StatePath, AfterlifeRealmAuthorityContract.StatePath }));
        if (dependent)
        {
            Assert.Contains(task.ContextFiles, file => file.Path == AfterlifeSpiritualConflictState.StatePath);
            Assert.Contains(task.ContextFiles, file => file.Path == AfterlifeRealmAuthorityContract.StatePath);
            Assert.Equal(new[] { AfterlifeSpiritualConflictState.StatePath }, task.AfterlifeContract!.AllowedAfterlifeSurfaces);
        }
    }

    /// <summary>
    /// Checks Ready metadata and exact response forwarding from the actual received worker proposal.
    /// </summary>
    /// <param name="context">
    /// Canonical fixture containing the published Ready signal.
    /// </param>
    /// <param name="result">
    /// Completed dispatch with its received proposal.
    /// </param>
    /// <param name="request">
    /// Current safe request used to validate the copied response.
    /// </param>
    /// <param name="turn">
    /// Original signed turn identity retained by dispatch.
    /// </param>
    /// <returns>
    /// A task completing after strict response parsing and exact correlation checks.
    /// </returns>
    private static async Task AssertSpiritualDispatchReadyAsync(ResourceMaterializationTestContext context,
        GmWorkerValidationRepairDispatchResult result, SpiritualWoundContinuationRequest request, WorkerTurnReference turn)
    {
        using var ready = JsonDocument.Parse((await context.FileSystem.ReadFileBytesAsync(
            GmWorkerValidationRepairDelegator.ValidationRepairReadyPath))!);
        Assert.Equal(turn.SessionId, ready.RootElement.GetProperty("sessionId").GetString());
        Assert.Equal(turn.RequestId, ready.RootElement.GetProperty("requestId").GetString());
        Assert.Equal(turn.TurnNumber, ready.RootElement.GetProperty("turnNumber").GetInt32());
        var response = SpiritualWoundContinuationProtocol.ReadResponse(ready.RootElement.GetProperty("spiritualWoundContinuation"));
        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateResponse(request, response));
        Assert.Equal(GmWorkerJson.Serialize(result.RunResult!.Proposal!.SpiritualWoundContinuation), GmWorkerJson.Serialize(response));
    }

    /// <summary>
    /// Captures gameplay and private authority files while excluding expected worker task, audit and Ready outputs.
    /// </summary>
    /// <param name="context">
    /// Fixture whose current protected files are read under one canonical lease.
    /// </param>
    /// <returns>
    /// Exact protected bytes, including <see langword="null"/> for absent command or receipt files.
    /// </returns>
    private static async Task<Dictionary<string, byte[]?>> ReadSpiritualDispatchRetainedFilesAsync(ResourceMaterializationTestContext context)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var images = new Dictionary<string, byte[]?>();
        foreach (var path in ResourceMaterializationTestContext.AllResourcePaths.Concat(new[]
                 {
                     SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath, SpiritualWoundOpportunityReceiptState.StatePath,
                     AfterlifeSpiritualConflictState.StatePath, AfterlifeEntityProfileState.StatePath,
                     "game_state/meta/soul_state.json", "input/turn_request.json",
                     "game_state/control/pending_turn_snapshot.json", ProjectionNarrativePath
                 }))
            images[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);
        return images;
    }

    /// <summary>
    /// Verifies the exact protected images after dispatch without constraining expected worker bookkeeping files.
    /// </summary>
    /// <param name="context">
    /// Canonical fixture to inspect under one lease.
    /// </param>
    /// <param name="expected">
    /// Protected images captured before dispatch or immediately after the deliberate C2 race advance.
    /// </param>
    /// <returns>
    /// A task completing after every retained image is compared.
    /// </returns>
    private static async Task AssertSpiritualDispatchRetainedFilesAsync(ResourceMaterializationTestContext context,
        IReadOnlyDictionary<string, byte[]?> expected)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        foreach (var pair in expected)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Creates a bounded fake GM process that reads only the actual task and writes a proposal in its worker workspace.
    /// </summary>
    /// <param name="context">
    /// Fixture root holding the temporary script outside canonical game_session.
    /// </param>
    /// <returns>
    /// Enabled existing worker profile running a deterministic decision or dependent cost response.
    /// </returns>
    private static WorkerBridgeProfile CreateSpiritualDispatchWorkerProfile(ResourceMaterializationTestContext context)
    {
        var scriptPath = Path.Combine(context.RootPath, "spiritual-continuation-worker.ps1");
        File.WriteAllText(scriptPath, """
            $ErrorActionPreference = 'Stop'
            $task = Get-Content -Raw -LiteralPath $env:BOE_WORKER_TASK_PATH | ConvertFrom-Json
            $continuation = $task.spiritualWoundContinuation
            if ($null -eq $continuation) { throw 'Missing actual continuation request.' }
            $proposalId = 'worker_proposal_spiritual_dispatch'
            $changes = @()
            $decisions = @()
            if ($continuation.phase -eq 'decision') {
                $decisions = @([ordered]@{ opportunityRef = $continuation.offer.opportunityRef; decision = 'none' })
            } elseif ($continuation.phase -eq 'dependent_draft') {
                $path = $continuation.dependentDraftFields[0].path
                $conflict = Get-Content -Raw -LiteralPath (Join-Path $env:BOE_WORKER_SESSION_PATH $path) | ConvertFrom-Json
                $conflict.activeConflict.exchangeLog[1].actionCostAudit.opposition.effectiveCost = 3
                $conflict.activeConflict.exchangeLog[1].actionCostAudit.opposition.after = 0
                $contentRef = 'worker_proposals/' + $proposalId + '/' + $path
                $contentPath = Join-Path $env:BOE_WORKER_SESSION_PATH $contentRef
                New-Item -ItemType Directory -Path (Split-Path $contentPath) -Force | Out-Null
                $conflict | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $contentPath -Encoding UTF8
                $sha = [System.Security.Cryptography.SHA256]::Create()
                try { $afterHash = ([BitConverter]::ToString($sha.ComputeHash([IO.File]::ReadAllBytes($contentPath)))).Replace('-', '').ToLowerInvariant() }
                finally { $sha.Dispose() }
                $before = @($task.contextFiles | Where-Object { $_.path -eq $path })[0]
                $changes = @([ordered]@{ path = $path; changeKind = 'replace'; beforeSha256 = $before.sha256; afterSha256 = $afterHash; contentRef = $contentRef })
            } else { throw 'Unsupported actual continuation phase.' }
            $proposal = [ordered]@{
                schemaVersion = 1; proposalId = $proposalId; taskId = $task.taskId; workerId = $task.workerId
                status = 'completed'; summary = 'Responded to the current spiritual continuation.'
                changedFiles = $changes; findings = @()
                spiritualWoundContinuation = [ordered]@{ schemaVersion = 1; continuationId = $continuation.continuationId; woundDecisions = $decisions }
                selfCheck = [ordered]@{ scopeReviewed = $true; validationExpectedToPass = $true; notes = @() }
                createdAtUtc = '2026-08-15T00:02:05Z'
            }
            $proposal | ConvertTo-Json -Depth 100 | Set-Content -LiteralPath $env:BOE_WORKER_PROPOSAL_PATH -Encoding UTF8
            """);
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        return profile with
        {
            LaunchCommand = $"pwsh.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
            TimeoutSeconds = 10
        };
    }
}
