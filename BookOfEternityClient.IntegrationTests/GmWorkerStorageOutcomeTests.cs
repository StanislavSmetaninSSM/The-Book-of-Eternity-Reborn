using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerStorageOutcomeTests(ITestOutputHelper output)
{
    internal const string WeatherPath = "game_state/world/weather.json";
    private const string ContentPath = "worker_proposals/worker_proposal_20260620_0001/game_state/world/weather.json";
    private const string ReadyPath = GmWorkerValidationRepairDelegator.ValidationRepairReadyPath;
    private static readonly byte[] AcceptedWeather = FileSystemManager.EncodeUtf8WithPreamble("{\"after\":true}");

    [Theory]
    [InlineData("router_audit")]
    [InlineData("latest_task")]
    [InlineData("build_diagnostic")]
    [InlineData("initial_generation")]
    [InlineData("apply_audit")]
    [InlineData("ready_publication")]
    [InlineData("ready_audit")]
    [InlineData("ready_diagnostic")]
    public Task OriginalWorkerPublicationUnknownStopsWithoutLosingAcceptedFacts(string mode) => RunAsync(mode, uncertain: true);

    [Theory]
    [InlineData("audit_known")]
    [InlineData("ready_success")]
    [InlineData("ready_known")]
    public Task OriginalWorkerKnownPublicationPolicyRemains(string mode) => RunAsync(mode, uncertain: false);

    private async Task RunAsync(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-worker-storage-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var probe = new WorkerStorageProbe();
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        fs.EnsureDirectoryStructure();
        probe.Attach(fs);
        probe.Cut.Armed = true;
        var audit = new GmWorkerAuditLog(fs);
        var poolAttempts = new List<string>();
        Task RefusePool(string stage)
        {
            poolAttempts.Add(stage);
            throw new InvalidOperationException("fixture refuses worker launch: " + stage);
        }
        var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit,
            new GmWorkerBridgePoolHooks
            {
                BeforeWorkerSlotWaitAsync = () => RefusePool("slot"),
                BeforeWorkspaceFileCreateAsync = _ => RefusePool("workspace"),
                BeforeProcessTreeAttachAsync = () => RefusePool("attach"),
                BeforeWorkerReleaseAsync = () => RefusePool("release")
            });
        var validationCalls = 0;
        // Real apply gate and publication; this callback qualifies storage semantics only.
        var gate = new GmWorkerApplyGate(fs, () =>
        {
            validationCalls++;
            return Task.FromResult<IReadOnlyList<ValidationIssue>>([]);
        }, audit);
        var delegator = new GmWorkerValidationRepairDelegator(fs, pool, gate, audit);
        var profile = GmWorkerBridgeTestFixtures.ValidationRepairCodexProfile();
        var turn = GmWorkerBridgeTestFixtures.ValidationRepairTask().SourceTurn;
        var known = new InvalidDataException("known worker storage preparation refusal");
        var knownHits = 0;
        WorkerTaskPacket? task = null;
        WorkerProposal? proposal = null;
        ApplyGateDecision? acceptedBefore = null;
        ApplyGateDecision? returnedDecision = null;
        GmWorkerValidationRepairDispatchResult? dispatchResult = null;
        (bool Created, string Diagnostic, bool SessionReplaced)? readyResult = null;

        var readyMode = mode.StartsWith("ready_", StringComparison.Ordinal);
        if (readyMode || mode == "apply_audit")
        {
            (task, proposal) = await PrepareReservedRepairAsync(fs);
            if (readyMode)
            {
                acceptedBefore = await gate.ApplyReservedAsync(proposal, profile, task.SessionGeneration);
                AssertAccepted(acceptedBefore);
                Assert.Equal(AcceptedWeather, File.ReadAllBytes(fs.ResolvePath(WeatherPath)));
                Assert.Equal(AcceptedWeather, probe.Committed[fs.ResolvePath(WeatherPath)]);
                Assert.Equal(1, validationCalls);
            }
        }
        else if (mode != "initial_generation")
            await fs.WriteFileAtomicAsync(WeatherPath, "{\"before\":true}");
        else
            Assert.False(File.Exists(fs.SessionGenerationPath));

        var weatherBefore = CleanupPublicationCut.ReadOptional(fs.ResolvePath(WeatherPath));
        var auditBefore = CleanupPublicationCut.ReadOptional(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath));
        if (mode == "build_diagnostic")
            probe.BeforeRead = path =>
            {
                if (path == WeatherPath) { knownHits++; throw known; }
                return Task.CompletedTask;
            };
        if (mode is "ready_diagnostic" or "ready_known" or "audit_known")
            probe.BeforeMutation = path =>
            {
                if (path == (mode == "audit_known" ? GmWorkerAuditLog.AuditLogPath : ReadyPath))
                { knownHits++; throw known; }
                return Task.CompletedTask;
            };
        probe.Target = !uncertain ? null : mode switch
        {
            "latest_task" => fs.ResolvePath(GmWorkerValidationRepairDelegator.LatestValidationRepairTaskPath),
            "initial_generation" => fs.SessionGenerationPath,
            "ready_publication" => fs.ResolvePath(ReadyPath),
            _ => fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)
        };
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (readyMode)
                readyResult = await InvokeReadyAsync(delegator, task!, proposal!);
            else if (mode == "apply_audit")
                returnedDecision = await gate.ApplyReservedAsync(proposal!, profile, task!.SessionGeneration);
            else if (mode == "audit_known")
                await audit.AppendEventAsync(Event());
            else
                dispatchResult = await delegator.TryRunAsync([profile],
                    mode == "router_audit" ? [] : [Issue()], turn, "2026-10-09T00:00:00Z", 1);
        });
        var weatherAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(WeatherPath));
        var readyAfter = CleanupPublicationCut.ReadOptional(fs.ResolvePath(ReadyPath));
        var retainedDecision = failure?.Data["GmWorkerApplyDecision"] as ApplyGateDecision;
        var readyCommitted = probe.Committed.GetValueOrDefault(fs.ResolvePath(ReadyPath));
        var afterImages = probe.PriorAtCut?.ToDictionary(pair => pair.Key,
            pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            mode, uncertain, Failure = failure?.ToString(), knownHits, poolAttempts, validationCalls,
            acceptedBefore, returnedDecision, retainedDecision, dispatchResult,
            ReadyResult = readyResult is { } ready ? new { ready.Created, ready.Diagnostic, ready.SessionReplaced } : null,
            weatherBefore, weatherAfter, readyCommitted, readyAfter,
            ReadyFactRetained = failure?.Data["GmWorkerReadySignalCreated"] is true,
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(known),
            probe.PriorAtCut, afterImages, Cut = probe.Cut.Evidence()
        }));
        Assert.Empty(poolAttempts);
        if (uncertain)
        {
            probe.Cut.AssertReachedAndStopped();
            Assert.Same(probe.Cut.OriginalUncertainty, failure);
            Assert.Null(dispatchResult); Assert.Null(returnedDecision); Assert.Null(readyResult);
            Assert.NotNull(probe.PriorAtCut);
            foreach (var pair in probe.PriorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
            if (mode is "build_diagnostic" or "ready_diagnostic")
            {
                Assert.Equal(1, knownHits);
                Assert.Same(known, failure!.Data["GmWorkerOriginalFailure"]);
            }
            if (readyMode)
            {
                Assert.Same(task, failure!.Data["GmWorkerTask"]);
                Assert.Same(proposal, failure.Data["GmWorkerProposal"]);
                Assert.Equal(AcceptedWeather, weatherAfter);
            }
            if (mode == "ready_audit")
            {
                Assert.NotNull(readyCommitted); Assert.Equal(readyCommitted, readyAfter);
                Assert.True(failure!.Data["GmWorkerReadySignalCreated"] is true);
            }
            if (mode == "apply_audit")
            {
                Assert.NotNull(retainedDecision); AssertAccepted(retainedDecision!);
                Assert.Same(proposal, failure!.Data["GmWorkerProposal"]);
                Assert.Equal(AcceptedWeather, weatherAfter);
                Assert.Equal(AcceptedWeather, probe.Committed[fs.ResolvePath(WeatherPath)]);
            }
            if (mode == "latest_task")
            {
                var retainedTask = Assert.IsType<WorkerTaskPacket>(failure!.Data["GmWorkerTask"]);
                var published = GmWorkerJson.Deserialize<WorkerTaskPacket>(Encoding.UTF8.GetString(probe.Cut.PublishedBytes!).TrimStart('\uFEFF'));
                Assert.Equal(GmWorkerJson.Serialize(published), GmWorkerJson.Serialize(retainedTask));
            }
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, probe.Cut.Cuts);
            Assert.False(File.Exists(probe.Cut.JournalPath));
            Assert.Equal(weatherBefore, weatherAfter);
            if (mode == "audit_known")
            {
                Assert.Equal(1, knownHits);
                Assert.Equal(auditBefore, CleanupPublicationCut.ReadOptional(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)));
            }
            else
            {
                Assert.NotNull(readyResult); Assert.False(readyResult.Value.SessionReplaced);
                Assert.Equal(mode == "ready_success", readyResult.Value.Created);
                if (mode == "ready_success")
                {
                    Assert.NotNull(readyCommitted); Assert.Equal(readyCommitted, readyAfter);
                    using var ready = JsonDocument.Parse(Encoding.UTF8.GetString(readyAfter!).TrimStart('\uFEFF'));
                    Assert.Equal(task!.SourceTurn.RequestId, ready.RootElement.GetProperty("requestId").GetString());
                }
                else
                {
                    Assert.Equal(1, knownHits); Assert.Null(readyAfter);
                    Assert.Contains(known.Message, readyResult.Value.Diagnostic, StringComparison.Ordinal);
                }
            }
        }
    }

    private static async Task<(WorkerTaskPacket Task, WorkerProposal Proposal)> PrepareReservedRepairAsync(FileSystemManager fs)
    {
        await fs.WriteFileAtomicAsync(WeatherPath, "{\"before\":true}");
        await fs.WriteFileAtomicAsync(ContentPath, "{\"after\":true}");
        string generation;
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
            generation = fs.ReadExistingSessionGeneration(lease)!;
        Assert.False(string.IsNullOrWhiteSpace(generation));
        var beforeHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fs.ResolvePath(WeatherPath)))).ToLowerInvariant();
        var afterHash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fs.ResolvePath(ContentPath)))).ToLowerInvariant();
        var task = GmWorkerBridgeTestFixtures.ValidationRepairTask() with
        {
            SessionGeneration = generation,
            ContextFiles = [new WorkerFileReference { Path = WeatherPath, Sha256 = beforeHash }]
        };
        var proposal = GmWorkerBridgeTestFixtures.ValidationRepairProposal();
        proposal = proposal with
        {
            ChangedFiles = [proposal.ChangedFiles[0] with { BeforeSha256 = beforeHash, AfterSha256 = afterHash }]
        };
        await fs.WriteFileAtomicAsync(GmWorkerBridgePool.GetTaskPacketPath(task.TaskId), GmWorkerJson.Serialize(task));
        return (task, proposal);
    }

    private static void AssertAccepted(ApplyGateDecision decision)
    {
        Assert.Equal(ApplyGateResult.Accepted, decision.Result);
        Assert.True(decision.ScopeCheck.Passed); Assert.True(decision.ValidationCheck.Passed);
        Assert.Contains(WeatherPath, decision.AppliedFiles);
    }

    private static Task<(bool Created, string Diagnostic, bool SessionReplaced)> InvokeReadyAsync(
        GmWorkerValidationRepairDelegator delegator, WorkerTaskPacket task, WorkerProposal proposal) =>
        (Task<(bool, string, bool)>)typeof(GmWorkerValidationRepairDelegator)
            .GetMethod("TryWriteReadySignalAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(delegator, [task, proposal, null])!;

    private static ValidationIssue Issue() => new(WeatherPath, IssueSeverity.Error,
        "normalizedWeatherState.description is required.", code: "normalized_weather_missing_description");

    private static WorkerAuditEvent Event() => new()
    {
        EventId = GmWorkerAuditEventIdGenerator.Create(), EventType = "storage-known-control",
        WorkerId = "validation_repair_codex", TimestampUtc = "2026-10-09T00:00:00Z", Summary = "Known audit refusal."
    };
}

internal sealed class WorkerStorageProbe : IDisposable
{
    internal CleanupPublicationCut Cut { get; } = new();
    internal Dictionary<string, byte[]?> Committed { get; } = new(StringComparer.Ordinal);
    internal Dictionary<string, byte[]?>? PriorAtCut { get; private set; }
    internal string? Target { get; set; }
    internal Func<string, Task>? BeforeRead { get; set; }
    internal Func<string, Task>? BeforeMutation { get; set; }
    internal FileSystemManagerHooks Hooks { get; }

    internal WorkerStorageProbe()
    {
        Hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = Cut.Hooks.LocalPublicationObserver,
            LocalPublicationRecoveryObserver = Cut.Hooks.LocalPublicationRecoveryObserver,
            SessionOperationClosingAsync = Cut.Hooks.SessionOperationClosingAsync,
            BeforeCanonicalMutationBoundaryAsync = async path =>
            {
                await Cut.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
                if (BeforeMutation != null) await BeforeMutation(path);
            },
            AfterCanonicalReadInitialValidationAsync = async path =>
            {
                await Cut.Hooks.AfterCanonicalReadInitialValidationAsync!(path);
                if (BeforeRead != null) await BeforeRead(path);
            },
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                var before = Cut.LaterLeases;
                await Cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                if (Cut.LaterLeases > before)
                    throw new InvalidOperationException("fixture refuses post-Unknown worker canonical admission");
            }
        };
        Cut.Select = (path, _) => Target != null && path == Target;
        Cut.ObserveBeforeCut = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.Committed) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
            foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
            {
                var path = member.GetProperty("Path").GetString()!;
                Committed[path] = CleanupPublicationCut.ReadOptional(path);
            }
        };
        Cut.BeforeCut = () =>
        {
            PriorAtCut = Committed.Where(pair => pair.Key != Target).ToDictionary(
                pair => pair.Key, pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
            foreach (var pair in PriorAtCut) Assert.Equal(Committed[pair.Key], pair.Value);
        };
    }

    internal void Attach(FileSystemManager files) => Cut.Attach(files);
    public void Dispose() => Cut.Dispose();
}
