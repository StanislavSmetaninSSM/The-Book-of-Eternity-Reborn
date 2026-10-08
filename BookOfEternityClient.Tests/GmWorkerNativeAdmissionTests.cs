using System.Diagnostics;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class GmWorkerNativeAdmissionTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    [InlineData(0, 2)]
    [InlineData(2, 2)]
    public async Task SharedPreparation_RejectsUnavailableCapabilityWithoutCallingLauncher(int mode, int capability)
    {
        Assert.True(OperatingSystem.IsLinux());
        var directory = Path.Combine(Path.GetTempPath(), "boe-native-admission-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var host = GmWorkerProcessHostLaunch.Create(new ProcessStartInfo("unused-worker") { WorkingDirectory = directory }, directory);
            var spy = new NeverStart();
            await Assert.ThrowsAsync<PlatformNotSupportedException>(() => host.PrepareOwnedAsync(spy,
                (GmWorkerBackendRequest)mode, (GmWorkerRequiredCapability)capability, CancellationToken.None));
            Assert.Equal(0, spy.Calls);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Fact]
    public async Task ActualPool_RejectsLinuxReleaseBeforeReservationAttachOrRelease()
    {
        Assert.True(OperatingSystem.IsLinux());
        var directory = Path.Combine(Path.GetTempPath(), "boe-native-pool-admission-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var fs = new FileSystemManager(directory, NullLogger<FileSystemManager>.Instance);
            var reservations = 0; var attachments = 0; var releases = 0;
            var hooks = new GmWorkerBridgePoolHooks
            {
                BeforeTaskReservationAsync = () =>
                {
                    reservations++;
                    // Baseline safety guard: no task files, detached workspace,
                    // helper or host may start even when the new gate is absent.
                    throw new InvalidOperationException("fixture stopped before any reservation or launch");
                },
                BeforeProcessTreeAttachAsync = () => { attachments++; throw new InvalidOperationException("unexpected attach"); },
                BeforeWorkerReleaseAsync = () => { releases++; throw new InvalidOperationException("unexpected Release"); }
            };
            var pool = new GmWorkerBridgePool(fs, null, null, hooks);
            var task = GmWorkerBridgeTestFixtures.AnalysisTask() with
            {
                ContextFiles = [new WorkerFileReference { Path = "game_state/world/weather.json", Sha256 = new string('a', 64) }]
            };
            var profile = GmWorkerBridgeTestFixtures.AnalysisCodexProfile();
            Assert.True(GmWorkerContractValidator.ValidateTaskPacket(task, profile).IsValid);
            var before = Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order().ToArray();
            var result = await pool.RunTaskAsync(profile, task);
            Assert.Equal(0, reservations);
            Assert.Equal(0, attachments); Assert.Equal(0, releases);
            Assert.Equal(WorkerBridgeState.Failed, result.Status.State);
            Assert.Null(result.Status.ProcessId); Assert.Null(result.Proposal);
            Assert.Contains("Linux worker Release", result.Status.LastError);
            Assert.Equal(before, Directory.EnumerateFiles(directory, "*", SearchOption.AllDirectories).Order().ToArray());
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private sealed class NeverStart : IGmWorkerOwnedLauncher
    {
        internal int Calls;
        public Task<GmWorkerOwnedLaunch> StartAsync(GmWorkerProcessHostLaunch host, GmWorkerBackendSelection selection, CancellationToken token)
        { Calls++; throw new InvalidOperationException("An unavailable capability called the launcher."); }
    }
}
