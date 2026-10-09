using System.Reflection;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("accepted_trajectory_unknown")]
    [InlineData("accepted_trajectory_known")]
    [InlineData("cleared_trajectory_unknown")]
    [InlineData("cleared_trajectory_known")]
    [InlineData("refresh_report_unknown")]
    [InlineData("refresh_cleanup_unknown")]
    [InlineData("refresh_report_known")]
    [InlineData("terminal_write_unknown")]
    [InlineData("terminal_known")]
    [InlineData("terminal_success")]
    public async Task OriginalRepairDiagnosticsPreservePublicationUncertaintyAndKnownPolicy(string mode)
    {
        const string ledgerPath = "game_state/control/gm_trajectory_ledger.jsonl";
        const string reportPath = "game_state/control/validation_diagnostic_failure_report.json";
        const string repairReadyPath = "game_state/control/validation_repair_ready.json";
        const string repairRequestPath = "game_state/control/validation_repair_request.json";
        const string terminalPath = "ready/turn_error.json";
        Assert.True(OperatingSystem.IsLinux());
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        using var probe = new WorkerStorageProbe();
        var fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(fs); probe.Cut.Armed = true;
        var engine = CreateGameEngine(new LoreRealmInput([], 0), InertRealmSettings, fileSystem: fs);
        var turn = GmWorkerBridgeTestFixtures.ValidationRepairTask().SourceTurn;
        var refreshCause = new IOException("known accepted-turn canonical refresh failure");
        var writeRefusal = new InvalidDataException("known repair diagnostic publication refusal");
        var knownHits = 0;
        var validationCalls = 0;
        WorkerTaskPacket? task = null;
        WorkerProposal? proposal = null;
        ApplyGateDecision? acceptedDecision = null;
        object? dispatch = null;
        if (mode.StartsWith("accepted_", StringComparison.Ordinal))
        {
            (task, proposal) = await GmWorkerStorageOutcomeTests.PrepareReservedRepairAsync(fs);
            var gate = new GmWorkerApplyGate(fs, () =>
            {
                validationCalls++;
                return Task.FromResult<IReadOnlyList<ValidationIssue>>([]);
            });
            acceptedDecision = await gate.ApplyReservedAsync(proposal,
                GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile(), task.SessionGeneration);
            Assert.Equal(ApplyGateResult.Accepted, acceptedDecision.Result);
            Assert.True(acceptedDecision.ScopeCheck.Passed); Assert.True(acceptedDecision.ValidationCheck.Passed);
            Assert.Equal(1, validationCalls);
            Assert.Equal(probe.Committed[fs.ResolvePath(GmWorkerStorageOutcomeTests.WeatherPath)],
                File.ReadAllBytes(fs.ResolvePath(GmWorkerStorageOutcomeTests.WeatherPath)));
            // Populate the original private caller DTO from an actual accepted gate result.
            // No live worker execution or full-game validation is claimed.
            var dispatchType = typeof(GameEngine).GetNestedType("ValidationRepairDispatchState", BindingFlags.NonPublic)!;
            dispatch = Activator.CreateInstance(dispatchType, nonPublic: true)!;
            dispatchType.GetProperty("WorkerApplyAccepted")!.SetValue(dispatch, true);
            dispatchType.GetProperty("WorkerResult")!.SetValue(dispatch, new GmWorkerValidationRepairDispatchResult
            {
                Outcome = GmWorkerValidationRepairOutcome.Applied,
                Task = task,
                ApplyDecision = acceptedDecision,
                ReadySignalCreated = false,
                FallbackReason = "fixture invokes actual accepted gate trajectory without Ready"
            });
        }
        else if (mode.StartsWith("refresh_", StringComparison.Ordinal))
        {
            await fs.WriteFileAtomicAsync(repairReadyPath, "{\"fixture\":\"ready-before-refresh\"}");
            await fs.WriteFileAtomicAsync(repairRequestPath, "{\"fixture\":\"request-before-refresh\"}");
        }
        else if (mode.StartsWith("terminal_", StringComparison.Ordinal))
        {
            await fs.WriteFileAtomicAsync("input/turn_request.json", JsonSerializer.Serialize(new
            {
                sessionId = turn.SessionId, requestId = turn.RequestId, turnNumber = turn.TurnNumber
            }));
        }
        string generation;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
            generation = fs.ReadExistingSessionGeneration(lease)!;
        Assert.False(string.IsNullOrWhiteSpace(generation));
        var weatherBefore = CleanupPublicationCut.ReadOptional(fs.ResolvePath(GmWorkerStorageOutcomeTests.WeatherPath));
        var uncertain = mode.EndsWith("_unknown", StringComparison.Ordinal);
        var targetPath = mode switch
        {
            "refresh_cleanup_unknown" => repairReadyPath,
            _ when mode.StartsWith("refresh_", StringComparison.Ordinal) => reportPath,
            _ when mode.StartsWith("terminal_", StringComparison.Ordinal) => terminalPath,
            _ => ledgerPath
        };
        probe.Target = uncertain ? fs.ResolvePath(targetPath) : null;
        if (mode.EndsWith("_known", StringComparison.Ordinal))
            probe.BeforeMutation = path =>
            {
                if (path == targetPath) { knownHits++; throw writeRefusal; }
                return Task.CompletedTask;
            };
        var returned = false;
        bool? terminalResult = null;
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (mode.StartsWith("accepted_", StringComparison.Ordinal))
                await InvokePrivateTaskAsync(engine, "AppendWorkerAcceptedValidationRepairTrajectoryAsync",
                    "storage fixture", new List<ValidationIssue>(), 1, dispatch);
            else if (mode.StartsWith("cleared_", StringComparison.Ordinal))
                await InvokePrivateTaskAsync(engine, "AppendClearedValidationRepairTrajectoryAsync",
                    "storage fixture", Array.Empty<ValidationIssue>(), 1, generation);
            else if (mode.StartsWith("refresh_", StringComparison.Ordinal))
                await InvokePrivateTaskAsync(engine, "FailClosedAcceptedTurnCanonicalRefreshAsync",
                    "storage fixture", refreshCause, null);
            else
                terminalResult = await InvokePrivateAsync<bool>(engine, "TryWriteHarnessTerminalErrorAsync",
                    "storage_fixture", "controlled terminal error");
            returned = true;
        });
        var weatherAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(GmWorkerStorageOutcomeTests.WeatherPath));
        var reportAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(reportPath));
        var reportCommitted = probe.Committed.GetValueOrDefault(fs.ResolvePath(reportPath));
        var terminalAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(terminalPath));
        var afterImages = probe.PriorAtCut?.ToDictionary(pair => pair.Key,
            pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new
        {
            mode, Failure = failure?.ToString(), returned, terminalResult, knownHits,
            acceptedDecision, RetainedDecision = failure?.Data["GmWorkerApplyDecision"], validationCalls, weatherBefore, weatherAfter, reportCommitted, reportAfter, terminalAfter,
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(refreshCause),
            probe.PriorAtCut, afterImages, Cut = probe.Cut.Evidence()
        }));
        Assert.Equal(weatherBefore, weatherAfter);
        if (uncertain)
        {
            probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
            Assert.False(returned); Assert.Null(terminalResult); Assert.NotNull(probe.PriorAtCut);
            foreach (var pair in probe.PriorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
            if (mode.StartsWith("accepted_", StringComparison.Ordinal))
            {
                Assert.Same(acceptedDecision, failure!.Data["GmWorkerApplyDecision"]);
                Assert.Same(task, failure.Data["GmWorkerTask"]);
            }
            if (mode.StartsWith("refresh_", StringComparison.Ordinal))
                Assert.Same(refreshCause, failure!.Data["CanonicalRefreshOriginalFailure"]);
            if (mode == "refresh_cleanup_unknown")
            {
                Assert.NotNull(reportCommitted); Assert.Equal(reportCommitted, reportAfter);
                Assert.Contains(refreshCause.Message, File.ReadAllText(fs.ResolvePath(reportPath)));
            }
        }
        else
        {
            Assert.Null(failure); Assert.True(returned); Assert.Equal(0, probe.Cut.Cuts);
            Assert.False(File.Exists(probe.Cut.JournalPath));
            Assert.Equal(mode == "terminal_success" ? 0 : 1, knownHits);
            if (mode == "refresh_report_known")
            {
                Assert.Null(reportAfter);
                Assert.False(File.Exists(fs.ResolvePath(repairReadyPath)));
                Assert.False(File.Exists(fs.ResolvePath(repairRequestPath)));
            }
            else if (mode.StartsWith("terminal_", StringComparison.Ordinal))
            {
                Assert.Equal(mode == "terminal_success", terminalResult);
                if (mode == "terminal_success")
                {
                    Assert.NotNull(terminalAfter);
                    Assert.Equal(probe.Committed[fs.ResolvePath(terminalPath)], terminalAfter);
                    using var terminal = JsonDocument.Parse(File.ReadAllText(fs.ResolvePath(terminalPath)));
                    Assert.Equal(turn.RequestId, terminal.RootElement.GetProperty("requestId").GetString());
                    Assert.Equal("error", terminal.RootElement.GetProperty("status").GetString());
                }
                else Assert.Null(terminalAfter);
            }
            else Assert.False(File.Exists(fs.ResolvePath(ledgerPath)));
        }
    }
}
