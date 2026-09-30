using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Rejects a reserved worker's scene update when another response appears before apply begins.
    /// </summary>
    /// <returns>
    /// A task completing after the competing response, narrative and private state remain unchanged.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationWorker_ReadyPublishedDuringExecutionPreventsNarrativeApply()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
        var response = CreateSpiritualContinuationDraftNoneResponse(boundary.Request) with
        {
            WoundDecisions = [OriginalSpiritualWoundDecision(boundary.Request.Offer!.OpportunityRef)]
        };
        var narrative = Encoding.UTF8.GetBytes(new JsonObject
        {
            ["response"] = "Чужое давление надломило волю хранителя.",
            ["timestamp"] = "2026-08-15T00:01:00Z"
        }.ToJsonString());
        var packet = await ReserveSpiritualContinuationWorkerAsync(context, boundary.Request, response,
            new Dictionary<string, byte[]> { [ProjectionNarrativePath] = narrative });
        await context.FileSystem.WriteFileAtomicAsync(GmWorkerValidationRepairDelegator.ValidationRepairReadyPath,
            GmWorkerJson.Serialize(new
            {
                sessionId = boundary.Turn.SessionId,
                requestId = boundary.Turn.RequestId,
                turnNumber = boundary.Turn.TurnNumber,
                status = "success",
                timestamp = "2026-08-15T00:02:00Z",
                spiritualWoundContinuation = CreateSpiritualContinuationDraftNoneResponse(boundary.Request)
            }));
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var gate = new GmWorkerApplyGate(context.FileSystem,
            () => throw new InvalidOperationException("Continuation reached ordinary validation."));

        var result = await gate.ApplyReservedAsync(packet.Proposal, packet.Profile, packet.Task.SessionGeneration);

        Assert.Equal(ApplyGateResult.Rejected, result.Result);
        Assert.Empty(result.AppliedFiles);
        Assert.Contains(result.RejectionReasons, reason => reason.Contains("Ready", StringComparison.Ordinal));
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }

    /// <summary>
    /// Accepts an explicit current decision with no file changes without validating an unfinished full turn.
    /// </summary>
    /// <returns>
    /// A task completing after reserved-task admission preserves every physical session image.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationWorker_DecisionWithoutChangedFilesUsesCurrentOwner()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        SpiritualWoundContinuationRequest request;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal("decision", read.Disposition);
            Assert.Empty(read.Issues);
            request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        }
        var packet = await ReserveSpiritualContinuationWorkerAsync(context, request,
            CreateSpiritualContinuationDraftNoneResponse(request), new Dictionary<string, byte[]>());
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);
        var ordinaryCalls = 0;
        var gate = new GmWorkerApplyGate(context.FileSystem, () =>
        {
            ordinaryCalls++;
            return Task.FromResult<IReadOnlyList<ValidationIssue>>(
                [new ValidationIssue(ProjectionNarrativePath, IssueSeverity.Error, "An unfinished original turn reached ordinary validation.")]);
        });

        var decision = await gate.ApplyReservedAsync(packet.Proposal, packet.Profile, packet.Task.SessionGeneration);

        Assert.True(decision.Result == ApplyGateResult.Accepted, string.Join(Environment.NewLine, decision.RejectionReasons));
        Assert.Equal(0, ordinaryCalls);
        Assert.Empty(decision.AppliedFiles);
        Assert.True(decision.ValidationCheck.Passed);
        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
    }

    /// <summary>
    /// Rejects invalid materialization semantics before applying narrative and accepts a valid unconsumed decision.
    /// </summary>
    /// <returns>
    /// A task completing after genuine offer bounds, proposal shape and acquisition narration are checked without C2 advancement.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationWorker_MaterializeIsComposedBeforeNarrativeApply()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        var boundary = await ReadSpiritualDispatchBoundaryAsync(context);
        var original = OriginalSpiritualWoundDecision(boundary.Request.Offer!.OpportunityRef);
        Assert.InRange(boundary.Request.Offer.MaximumSeverityRank, 1, 3);
        foreach (var scenario in new[] { "above offered severity", "invalid severity", "missing classification", "missing acquisition narration", "valid" })
        {
            var decision = JsonNode.Parse(original.GetRawText())!.AsObject();
            if (scenario == "above offered severity")
                decision["proposal"]!["severity"] = new[] { "I", "II", "III", "IV" }[boundary.Request.Offer.MaximumSeverityRank];
            if (scenario == "invalid severity") decision["proposal"]!["severity"] = "V";
            if (scenario == "missing classification") decision["proposal"]!["classification"] = new JsonObject();
            var scene = scenario == "missing acquisition narration"
                ? "Обмен завершён без описания новой раны."
                : "Чужое давление надломило волю хранителя.";
            var narrative = Encoding.UTF8.GetBytes(new JsonObject
                { ["response"] = scene, ["timestamp"] = "2026-08-15T00:01:00Z" }.ToJsonString());
            var response = CreateSpiritualContinuationDraftNoneResponse(boundary.Request) with
                { WoundDecisions = [JsonSerializer.SerializeToElement(decision)] };
            var packet = await ReserveSpiritualContinuationWorkerAsync(context, boundary.Request, response,
                new Dictionary<string, byte[]> { [ProjectionNarrativePath] = narrative });
            var before = await ReadSpiritualContinuationTransportFilesAsync(context);
            var gate = new GmWorkerApplyGate(context.FileSystem,
                () => throw new InvalidOperationException("Continuation reached ordinary full-turn validation."));

            var result = await gate.ApplyReservedAsync(packet.Proposal, packet.Profile, packet.Task.SessionGeneration);

            var expected = scenario == "valid" ? ApplyGateResult.Accepted : ApplyGateResult.Rejected;
            Assert.True(result.Result == expected,
                scenario + ": " + result.Result + "; " + string.Join("; ", result.RejectionReasons));
            if (scenario == "valid")
            {
                var narrativePath = Path.GetRelativePath(context.FileSystem.GameSessionPath,
                    Path.Combine(context.FileSystem.GameSessionPath, ProjectionNarrativePath));
                Assert.Contains(narrativePath, before.Keys);
                before[narrativePath] = narrative;
            }
            else Assert.Empty(result.AppliedFiles);
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
            var current = await ReadSpiritualDispatchBoundaryAsync(context);
            Assert.Equal(boundary.Request.ContinuationId, current.Request.ContinuationId);
        }
    }

    /// <summary>
    /// Rejects malicious broad task permissions and forged readable offer data despite valid transport correlation.
    /// </summary>
    /// <returns>
    /// A task completing after each normally reserved malicious proposal is rejected without session mutations.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationWorker_PublicTaskCannotAuthorizePrivateOrUnrelatedChanges()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true);
        SpiritualWoundContinuationRequest request;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal("decision", read.Disposition);
            request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
        }
        var response = CreateSpiritualContinuationDraftNoneResponse(request);
        var forged = request with { Offer = request.Offer! with { Target = "Другой хранитель" } };
        var scenarios = new (string Name, SpiritualWoundContinuationRequest Request, Dictionary<string, byte[]> Images)[]
        {
            ("forged safe target", forged, new()),
            ("foreign request metadata", request, new()),
            ("conflict edit", request, new() { [AfterlifeSpiritualConflictState.StatePath] = Encoding.UTF8.GetBytes("{}") }),
            ("private checkpoint edit", request, new() { [SpiritualWoundCaptureCheckpointState.StatePath] = Encoding.UTF8.GetBytes("{}") }),
            ("canonical resource edit", request, new() { [ResourceMaterializationContract.StatePath] = Encoding.UTF8.GetBytes("{}") })
        };
        foreach (var scenario in scenarios)
        {
            var packet = await ReserveSpiritualContinuationWorkerAsync(context, scenario.Request, response, scenario.Images);
            if (scenario.Name == "foreign request metadata")
            {
                packet.Task = packet.Task with { SourceTurn = packet.Task.SourceTurn with
                    { RequestId = packet.Task.SourceTurn.RequestId + "_foreign" } };
                await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
                await context.FileSystem.WriteFileAtomicAsync(lease,
                    GmWorkerBridgePool.GetTaskPacketPath(packet.Task.TaskId), GmWorkerJson.Serialize(packet.Task));
            }
            var before = await ReadSpiritualContinuationTransportFilesAsync(context);
            var ordinaryCalls = 0;
            var gate = new GmWorkerApplyGate(context.FileSystem, () =>
            {
                ordinaryCalls++;
                return Task.FromResult<IReadOnlyList<ValidationIssue>>([]);
            });

            var decision = await gate.ApplyReservedAsync(packet.Proposal, packet.Profile, packet.Task.SessionGeneration);

            Assert.True(decision.Result == ApplyGateResult.Rejected,
                $"{scenario.Name}: {decision.Result}; {string.Join(Environment.NewLine, decision.RejectionReasons)}");
            Assert.Equal(0, ordinaryCalls);
            Assert.Empty(decision.AppliedFiles);
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        }
    }

    /// <summary>
    /// Applies a valid dependent cost draft and rolls back invalid permitted numbers without advancing the saved choice.
    /// </summary>
    /// <returns>
    /// A task completing after both worker outcomes preserve the private pair, command and canonical resources.
    /// </returns>
    [Fact]
    public async Task OriginalSpiritualContinuationWorker_DependentCostsUseCurrentDiagnosticsAndRollback()
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true, dependentCost: true);
        SpiritualWoundContinuationRequest request;
        byte[] originalConflict;
        await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
            Assert.Equal("offer", opened.Disposition);
            using (var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
            {
                var selected = await session.SubmitDecisionAsync(lease,
                    OriginalSpiritualWoundDecision(session.Offer!.OpportunityRef, "spiritual_action_cost_burden", "guard"),
                    "Чужое давление надломило волю хранителя.");
                Assert.Equal("dependent_continuation", selected.Disposition);
                selected.Session?.Dispose();
            }
            var read = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
            Assert.Equal("dependent_draft", read.Disposition);
            Assert.NotEmpty(read.Issues);
            request = Assert.IsType<SpiritualWoundContinuationRequest>(read.Request);
            originalConflict = (await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath))!;
        }
        var response = new SpiritualWoundContinuationResponse { ContinuationId = request.ContinuationId, WoundDecisions = [] };
        foreach (var validCost in new[] { false, true })
        {
            var candidate = System.Text.Json.Nodes.JsonNode.Parse(originalConflict)!;
            candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = validCost ? 3 : 2;
            candidate["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
            var proposed = Encoding.UTF8.GetBytes(candidate.ToJsonString());
            var images = new Dictionary<string, byte[]> { [AfterlifeSpiritualConflictState.StatePath] = proposed };
            await using (var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
                Assert.Empty(await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, request, response,
                    images.ToDictionary(pair => pair.Key, pair => (byte[]?)pair.Value)));
            var packet = await ReserveSpiritualContinuationWorkerAsync(context, request, response, images);
            var before = await ReadSpiritualContinuationTransportFilesAsync(context);
            var ordinaryCalls = 0;
            var gate = new GmWorkerApplyGate(context.FileSystem, () =>
            {
                ordinaryCalls++;
                return Task.FromResult<IReadOnlyList<ValidationIssue>>([]);
            });

            var decision = await gate.ApplyReservedAsync(packet.Proposal, packet.Profile, packet.Task.SessionGeneration);

            Assert.True(decision.Result == (validCost ? ApplyGateResult.Accepted : ApplyGateResult.ValidationFailed),
                $"validCost={validCost}: {decision.Result}; {string.Join(Environment.NewLine, decision.RejectionReasons)}");
            Assert.Equal(0, ordinaryCalls);
            Assert.False(File.Exists(context.FileSystem.ActiveWorkerApplyTransactionJournalPath));
            if (validCost)
            {
                Assert.Equal(new[] { AfterlifeSpiritualConflictState.StatePath }, decision.AppliedFiles);
                before[Path.GetRelativePath(context.FileSystem.GameSessionPath,
                    context.FileSystem.ResolvePath(AfterlifeSpiritualConflictState.StatePath))] = proposed;
            }
            else
            {
                Assert.Empty(decision.AppliedFiles);
                Assert.True(decision.ScopeCheck.Passed);
                Assert.False(decision.ValidationCheck.Passed);
                Assert.True(decision.ValidationCheck.IssueCount > 0);
            }
            await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
            await using var postLease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
            var current = await context.Validator.ReadSpiritualWoundContinuationAsync(postLease);
            Assert.Equal("dependent_draft", current.Disposition);
            Assert.Equal(request.ContinuationId, current.Request!.ContinuationId);
            if (validCost) Assert.Empty(current.Issues);
            else Assert.NotEmpty(current.Issues);
        }
    }

    /// <summary>
    /// Stores a normal hash-pinned worker reservation and proposal content for a genuine continuation envelope.
    /// </summary>
    /// <param name="context">
    /// Signed C2 fixture providing the actual original turn and current session generation.
    /// </param>
    /// <param name="request">
    /// Current safe request, or deliberately forged readable data in the malicious-task control.
    /// </param>
    /// <param name="response">
    /// Structurally valid correlated response without private execution authority.
    /// </param>
    /// <param name="images">
    /// Exact proposed replacements; empty permits a decision-only proposal. Private paths occur only in malicious controls.
    /// </param>
    /// <returns>
    /// Profile, physically reserved task and proposal for the existing reserved apply entry point.
    /// </returns>
    private static async Task<(WorkerBridgeProfile Profile, WorkerTaskPacket Task, WorkerProposal Proposal)>
        ReserveSpiritualContinuationWorkerAsync(ResourceMaterializationTestContext context,
            SpiritualWoundContinuationRequest request, SpiritualWoundContinuationResponse response,
            IReadOnlyDictionary<string, byte[]> images)
    {
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var generation = context.FileSystem.GetOrCreateSessionGeneration(lease);
        using var originalRequest = JsonDocument.Parse((await context.FileSystem.ReadFileBytesAsync(lease, "input/turn_request.json"))!);
        var original = originalRequest.RootElement;
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        profile = profile with { Permissions = profile.Permissions with { ProposalWritePaths = ["game_state/**", "output/**"] } };
        var allowed = images.Keys.Append(ProjectionNarrativePath).Distinct(StringComparer.Ordinal).ToArray();
        var afterlife = allowed.Any(AfterlifeRealmAuthorityContract.IsAfterlifeStatePath);
        var paths = afterlife ? allowed.Append(AfterlifeRealmAuthorityContract.StatePath) : allowed.AsEnumerable();
        var files = new List<WorkerFileReference>();
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            var bytes = await context.FileSystem.ReadFileBytesAsync(lease, path);
            files.Add(new() { Path = path, Sha256 = bytes is null ? "missing" : Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() });
        }
        var suffix = Guid.NewGuid().ToString("N");
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            TaskId = "worker_task_spiritual_" + suffix,
            SessionGeneration = generation,
            SourceTurn = new()
            {
                SessionId = original.GetProperty("sessionId").GetString()!,
                RequestId = original.GetProperty("requestId").GetString()!,
                TurnNumber = original.GetProperty("turnNumber").GetInt32()
            },
            ValidationIssues = [],
            SpiritualWoundContinuation = request,
            ContextFiles = files,
            AllowedProposalPaths = allowed,
            AfterlifeContract = afterlife ? new WorkerAfterlifeTaskContract
            {
                RealmGate = WorkerAfterlifeRealmGate.ChaosSea,
                CurrentRealm = "Chaos Sea",
                AllowedAfterlifeSurfaces = allowed.Where(AfterlifeRealmAuthorityContract.IsAfterlifeStatePath).ToArray(),
                RequiredReceipts = ["No receipt is authored by the worker."],
                RequiredReports = ["The apply decision records the result."],
                ForbiddenMortalSubstitutes = ["Mortal combat state"]
            } : null
        };
        var proposalId = "worker_proposal_spiritual_" + suffix;
        var changes = new List<WorkerChangedFile>();
        foreach (var pair in images)
        {
            var contentRef = $"worker_proposals/{proposalId}/{pair.Key}";
            await context.FileSystem.WriteFileAtomicBytesAsync(lease, contentRef, pair.Value);
            var beforeHash = files.Single(file => file.Path == pair.Key).Sha256;
            changes.Add(new()
            {
                Path = pair.Key,
                ChangeKind = beforeHash == "missing" ? WorkerFileChangeKind.Add : WorkerFileChangeKind.Replace,
                BeforeSha256 = beforeHash,
                AfterSha256 = Convert.ToHexString(SHA256.HashData(pair.Value)).ToLowerInvariant(),
                ContentRef = contentRef
            });
        }
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal() with
        {
            TaskId = task.TaskId,
            ProposalId = proposalId,
            ChangedFiles = changes,
            SpiritualWoundContinuation = response
        };
        Assert.Empty(GmWorkerContractValidator.ValidateTaskPacket(task, profile).Errors);
        Assert.Empty(GmWorkerContractValidator.ValidateProposal(proposal, task, profile).Errors);
        await context.FileSystem.WriteFileAtomicAsync(lease, GmWorkerBridgePool.GetTaskPacketPath(task.TaskId), GmWorkerJson.Serialize(task));
        return (profile, task, proposal);
    }
}
