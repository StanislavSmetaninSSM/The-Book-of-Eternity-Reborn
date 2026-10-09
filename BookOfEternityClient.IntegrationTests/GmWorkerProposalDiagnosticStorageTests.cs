using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerProposalDiagnosticStorageTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("initial_generation_unknown")]
    [InlineData("context_diagnostic_unknown")]
    [InlineData("context_known")]
    [InlineData("no_worker")]
    [InlineData("invalid")]
    public async Task OriginalProposalDispatchPreservesPublicationUncertaintyAndKnownPolicy(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-proposal-diagnostic-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var probe = new WorkerStorageProbe();
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        fs.EnsureDirectoryStructure();
        probe.Attach(fs); probe.Cut.Armed = true;
        var audit = new GmWorkerAuditLog(fs);
        var poolAttempts = new List<string>();
        Task RefusePool(string stage)
        {
            poolAttempts.Add(stage);
            throw new InvalidOperationException("fixture forbids proposal worker launch: " + stage);
        }
        var pool = new GmWorkerBridgePool(fs, new GmWorkerProposalStore(fs), audit,
            new GmWorkerBridgePoolHooks
            {
                BeforeWorkerSlotWaitAsync = () => RefusePool("slot"),
                BeforeTaskReservationAsync = () => RefusePool("reservation"),
                BeforeWorkspaceFileCreateAsync = _ => RefusePool("workspace"),
                BeforeProcessTreeAttachAsync = () => RefusePool("attach"),
                BeforeWorkerReleaseAsync = () => RefusePool("release")
            });
        var service = new GmWorkerProposalOnlyDispatchService(fs, pool, audit);
        var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile();
        var request = GmWorkerProposalOnlyDispatchRequest.Analysis(
            GmWorkerBridgeTestFixtures.ValidationRepairTask().SourceTurn,
            mode == "invalid" ? "" : "Inspect the stored weather without changing it.",
            ["Is the context readable?"], [GmWorkerStorageOutcomeTests.WeatherPath]);
        var known = new InvalidDataException("known proposal context read refusal");
        var knownHits = 0;
        if (mode.StartsWith("context_", StringComparison.Ordinal))
        {
            await fs.WriteFileAtomicAsync(GmWorkerStorageOutcomeTests.WeatherPath, "{\"before\":true}");
            probe.BeforeRead = path =>
            {
                if (path == GmWorkerStorageOutcomeTests.WeatherPath) { knownHits++; throw known; }
                return Task.CompletedTask;
            };
        }
        else
            Assert.False(File.Exists(fs.SessionGenerationPath));
        var uncertain = mode.EndsWith("_unknown", StringComparison.Ordinal);
        probe.Target = mode switch
        {
            "initial_generation_unknown" => fs.SessionGenerationPath,
            "context_diagnostic_unknown" => fs.ResolvePath(GmWorkerAuditLog.AuditLogPath),
            _ => null
        };
        var before = CleanupPublicationCut.ReadOptional(fs.ResolvePath(GmWorkerStorageOutcomeTests.WeatherPath));
        GmWorkerProposalOnlyDispatchResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await service.DispatchAsync(
            mode == "no_worker" ? [] : [profile], request));
        var after = CleanupPublicationCut.ReadOptional(fs.ResolvePath(GmWorkerStorageOutcomeTests.WeatherPath));
        var afterImages = probe.PriorAtCut?.ToDictionary(pair => pair.Key,
            pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
        output.WriteLine(JsonSerializer.Serialize(new
        {
            mode, Failure = failure?.ToString(), result, knownHits, poolAttempts, before, after,
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(known),
            probe.PriorAtCut, afterImages, Cut = probe.Cut.Evidence()
        }));
        Assert.Empty(poolAttempts); Assert.Equal(before, after);
        if (uncertain)
        {
            probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
            Assert.Null(result); Assert.NotNull(probe.PriorAtCut);
            foreach (var pair in probe.PriorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
            if (mode == "context_diagnostic_unknown")
            {
                Assert.Equal(1, knownHits);
                Assert.Same(known, failure!.Data["GmWorkerOriginalFailure"]);
            }
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, probe.Cut.Cuts); Assert.NotNull(result);
            Assert.Equal(mode == "no_worker" ? GmWorkerProposalOnlyDispatchOutcome.SkippedNoWorker
                : GmWorkerProposalOnlyDispatchOutcome.InvalidRequest, result!.Outcome);
            if (mode == "context_known")
            {
                Assert.Equal(1, knownHits);
                Assert.Contains(known.Message, result.FallbackReason, StringComparison.Ordinal);
                Assert.Contains("proposal-only-dispatch-failed", File.ReadAllText(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)));
            }
            else
            {
                Assert.False(File.Exists(fs.SessionGenerationPath));
                Assert.False(File.Exists(fs.ResolvePath(GmWorkerAuditLog.AuditLogPath)));
            }
            Assert.False(File.Exists(probe.Cut.JournalPath));
        }
    }
}
