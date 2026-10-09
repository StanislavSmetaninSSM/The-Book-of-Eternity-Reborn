using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("initial_checkpoint_unknown")]
    [InlineData("initial_pending_unknown")]
    [InlineData("request_unknown")]
    [InlineData("cancel_request_unknown")]
    [InlineData("dependent_checkpoint_unknown")]
    [InlineData("dependent_ready_delete_unknown")]
    [InlineData("dependent_request_delete_unknown")]
    [InlineData("reconcile_ready_delete_unknown")]
    [InlineData("reconcile_request_delete_unknown")]
    [InlineData("initial_cancel_success")]
    [InlineData("dependent_next_cancel_success")]
    [InlineData("reconcile_next_cancel_success")]
    public async Task OriginalSpiritualEnginePreservesCanonicalPublicationOutcome(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        using var originalRoot = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        using var probe = new WorkerStorageProbe();
        const string requestPath = "game_state/control/validation_repair_request.json";
        const string readyPath = "game_state/control/validation_repair_ready.json";
        var dependent = mode.StartsWith("dependent_", StringComparison.Ordinal);
        var reconcile = mode.StartsWith("reconcile_", StringComparison.Ordinal);
        var unknown = mode.EndsWith("_unknown", StringComparison.Ordinal);
        var input = new QueuedConsoleInputSource(dependent || reconcile ? [] : [Key(ConsoleKey.Escape)]);
        GameEngine? engine = null;
        ResourceMaterializationTestContext? context = null;
        var request = new TurnRequest
        {
            SessionId = "session_storage_spiritual", RequestId = "request_storage_spiritual_42", TurnNumber = 42,
            PlayerAction = "Удержать встречное духовное давление.", Timestamp = DateTime.UtcNow.ToString("O"),
            PreGeneratedDices1d20 = [5, 15, 20, 18, 9, 8]
        };
        try
        {
            context = await AfterlifeResourceCutoverTests.CreateSpiritualGameEngineOriginalAsync(async original =>
            {
                await AfterlifeResourceCutoverTests.SeedSpiritualWorseningTierAuthorityAsync(original);
                engine = CreateGameEngine(input, settings =>
                {
                    settings.GmBridgeAutoStart = false;
                    settings.MusicEnabled = false;
                    settings.SoundEnabled = false;
                }, fileSystem: original.FileSystem);
                await InvokePrivateTaskAsync(engine, "RefreshRuntimeStateAsync");
                await InvokePrivateTaskAsync(engine, "EnsureClientOwnedSystemFilesHealthyAsync");
                request.ProgressionControl = await new ProgressionScheduleService(original.FileSystem,
                    NullLogger<ProgressionScheduleService>.Instance).BuildControlForNextTurnAsync("Chaos Sea");
                var backup = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "storage-spiritual-original");
                await original.WriteExactJsonAsync("input/turn_request.json", JsonSerializer.Serialize(request, SnapshotHashJsonOpts));
                await InvokePrivateTaskResultAsync(engine, "CreateCanonicalBaselineSnapshotAsync",
                    request, backup, "storage-spiritual-original");
            }, hooks: probe.Hooks);
            var fs = context.FileSystem;
            probe.Attach(fs);
            await AfterlifeResourceCutoverTests.WriteSpiritualStagedOriginalExchangesAsync(context);
            await WriteSpiritualStagedLifecycleOutputsAsync(context, request);
            const string scene = "Чужое давление надломило волю души.";
            await context.WriteExactJsonAsync("output/narrative_response.json", JsonSerializer.Serialize(new
            {
                response = scene, timestamp = DateTime.UtcNow.ToString("O")
            }));
            var resolution = await InvokePrivateTaskResultAsync(engine!, "ResolveActivePendingTurnSnapshotContextAsync");
            Assert.Equal("Usable", resolution.GetType().GetProperty("Status")!.GetValue(resolution)!.ToString());
            var snapshot = resolution.GetType().GetProperty("Context")!.GetValue(resolution)!;
            SpiritualWoundContinuationRequest? a = null;
            string? b = null;
            string generation;
            await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = fs.GetOrCreateSessionGeneration(lease);
                if (dependent || reconcile)
                {
                    Assert.True(Assert.IsType<bool>(await InvokePrivateTaskResultAsync(engine!, "BeginAcceptedSpiritualCaptureAsync", lease)));
                    var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
                    Assert.Equal("offer", opened.Disposition);
                    using (var owner = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
                    {
                        var submitted = await owner.SubmitDecisionAsync(lease,
                            AfterlifeResourceCutoverTests.CreateSpiritualStagedPlayerDecision(owner.Offer!.OpportunityRef), scene);
                        using var submittedOwner = submitted.Session;
                        Assert.Equal("dependent_continuation", submitted.Disposition);
                    }
                    var current = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
                    Assert.Equal("dependent_draft", current.Disposition);
                    a = Assert.IsType<SpiritualWoundContinuationRequest>(current.Request);
                    var draft = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeSpiritualConflictState.StatePath));
                    await fs.WriteFileAtomicAsync(lease, AfterlifeSpiritualConflictState.StatePath,
                        AfterlifeResourceCutoverTests.CreateSpiritualStagedCorrectionA(draft).ToJsonString());
                    var response = new SpiritualWoundContinuationResponse { ContinuationId = a.ContinuationId, WoundDecisions = [] };
                    await fs.WriteFileAtomicAsync(lease, requestPath, JsonSerializer.Serialize(new ValidationRepairRequest
                    {
                        SessionId = request.SessionId, RequestId = request.RequestId, TurnNumber = request.TurnNumber,
                        Source = "storage-original", DetectedAtUtc = DateTime.UtcNow.ToString("O"),
                        RevalidationAttempt = 1, SpiritualWoundContinuation = a
                    }, SnapshotHashJsonOpts));
                    await fs.WriteFileAtomicAsync(lease, readyPath, JsonSerializer.Serialize(new
                    {
                        sessionId = request.SessionId, requestId = request.RequestId, turnNumber = request.TurnNumber,
                        timestamp = DateTime.UtcNow.ToString("O"), status = "success", spiritualWoundContinuation = response
                    }, SnapshotHashJsonOpts));
                    if (reconcile)
                    {
                        var committed = await context.Validator.EvaluateAndCommitSpiritualWoundDependentResponseAsync(lease, a, response);
                        Assert.Equal("committed", committed.Progress.Disposition);
                        b = Assert.IsType<SpiritualWoundContinuationRequest>(committed.Progress.NextRequest).ContinuationId;
                        Assert.NotEqual(a.ContinuationId, b);
                    }
                }
            }
            foreach (var path in Directory.EnumerateFiles(fs.GameSessionPath, "*", SearchOption.AllDirectories))
                if (!path.EndsWith(".lock", StringComparison.Ordinal)) probe.Committed[path] = File.ReadAllBytes(path);
            var checkpointPath = fs.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath);
            var target = fs.ResolvePath(mode == "initial_pending_unknown" ? SpiritualWoundDecisionPendingState.StatePath :
                mode.Contains("checkpoint", StringComparison.Ordinal) ? SpiritualWoundCaptureCheckpointState.StatePath :
                mode.Contains("ready_delete", StringComparison.Ordinal) ? readyPath : requestPath);
            probe.Target = unknown ? target : null;
            var deletion = mode.Contains("delete", StringComparison.Ordinal) || mode == "cancel_request_unknown";
            probe.Cut.Select = (path, _) => unknown && path == target && (deletion ? !File.Exists(path) : File.Exists(path));
            var ordinaryCommit = probe.Cut.ObserveBeforeCut;
            var committedRequests = new List<string>();
            probe.Cut.ObserveBeforeCut = (phase, index) =>
            {
                ordinaryCommit!(phase, index);
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
                {
                    var path = member.GetProperty("Path").GetString();
                    if (path == fs.ResolvePath(requestPath) && File.Exists(path))
                    {
                        var id = JsonNode.Parse(File.ReadAllBytes(path))!["spiritualWoundContinuation"]!["continuationId"]!.GetValue<string>();
                        committedRequests.Add(id);
                        // A genuinely committed successor request ends this finite control through ordinary Escape.
                        if (a != null && id != a.ContinuationId) input.Enqueue(Key(ConsoleKey.Escape));
                    }
                    // Malformed Ready must not leave this fixture waiting for a nonexistent GM.
                    if (path == fs.ResolvePath(readyPath) && !File.Exists(path)) input.Enqueue(Key(ConsoleKey.Escape));
                }
            };
            probe.Cut.Armed = true;
            object? result = null;
            var failure = await Record.ExceptionAsync(async () => result = await InvokePrivateTaskResultAsync(engine!,
                "ContinueAcceptedSpiritualTurnAsync", "storage-original", 42, snapshot));
            var afterImages = probe.PriorAtCut?.ToDictionary(pair => pair.Key,
                pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
            var checkpointAfter = CleanupPublicationCut.ReadOptional(checkpointPath);
            var readyAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(readyPath));
            var requestAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(requestPath));
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
            {
                mode, unknown, generation, a = a?.ContinuationId, b, committedRequests,
                result = result?.ToString(), failure = failure?.ToString(),
                SameOriginalUncertainty = probe.Cut.OriginalUncertainty != null && ReferenceEquals(probe.Cut.OriginalUncertainty, failure),
                checkpointAfter, readyAfter, requestAfter, probe.PriorAtCut, afterImages, Cut = probe.Cut.Evidence()
            }));
            if (unknown)
            {
                probe.Cut.AssertReachedAndStopped();
                Assert.Same(probe.Cut.OriginalUncertainty, failure);
                Assert.Null(result);
                Assert.NotNull(probe.PriorAtCut);
                foreach (var pair in probe.PriorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
            }
            else
            {
                Assert.Null(failure); Assert.Equal(0, probe.Cut.Cuts);
                Assert.Equal("Rejected", result?.ToString()); // Ordinary, explicit player Escape.
                Assert.NotNull(checkpointAfter); Assert.Null(requestAfter); Assert.Null(readyAfter);
                Assert.False(File.Exists(probe.Cut.JournalPath));
                if (a != null)
                {
                    Assert.Contains(committedRequests, id => id != a.ContinuationId);
                    Assert.Single(JsonNode.Parse(checkpointAfter!)!["checkpoint"]!["pendingSubmission"]!["dependentDraftProgress"]!.AsArray());
                    if (b != null) Assert.Contains(b, committedRequests);
                }
                else Assert.Single(committedRequests);
            }
        }
        finally
        {
            if (context != null)
            {
                await context.DisposeAsync();
                _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
                {
                    CleanupOwnedRoot = context.RootPath, OwnedFixtureRemoved = !Directory.Exists(context.RootPath), CleanupFailure = (string?)null
                }));
                Assert.False(Directory.Exists(context.RootPath));
            }
        }
    }
}
