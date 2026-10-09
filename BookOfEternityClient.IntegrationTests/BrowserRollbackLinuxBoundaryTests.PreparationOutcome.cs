using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class BrowserRollbackLinuxBoundaryTests
{
    [Fact]
    public async Task PreparationUnknown_OriginalAtomicStageStopsBeforeCallback()
    {
        using var owned = new CleanupOwnedFixture(_root, line => output.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new PreparationPublicationProbe();
        var files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        probe.Attach(files);
        await new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance).BootstrapLocalStorageAsync();
        const string second = "game_state/meta/browser-second.bin";
        await files.WriteFileAtomicBytesAsync(Member, Before);
        await files.WriteFileAtomicBytesAsync(second, [71, 83]);
        var callback = false;
        byte[]? lockAtCut = null;
        probe.Select = (path, member) => path.EndsWith(".rollback", StringComparison.Ordinal) &&
            member.GetProperty("After").GetProperty("Exists").GetBoolean() &&
            probe.CommittedBackups(".rollback").Length == 1 && path != probe.CommittedBackups(".rollback")[0];
        probe.BeforeCut = () =>
        {
            Assert.Single(probe.CommittedBackups(".rollback"));
            lockAtCut = File.ReadAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath));
            Assert.False(callback);
        };
        probe.Arm();
        BrowserLocalWriteResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await new BrowserLocalWriteCoordinator(files,
            new LocalUiSessionLockService(files)).ExecuteAtomicAsync(Request, [Member, second], _ =>
            {
                callback = true;
                return Task.CompletedTask;
            }));
        output.WriteLine(JsonSerializer.Serialize(new { callback, result, Failure = failure?.ToString(), lockAtCut,
            RetainedLock = CleanupPublicationCut.ReadOptional(files.ResolvePath(LocalUiSessionLockService.LockPath)), Evidence = probe.Evidence() }));
        probe.AssertStopped(failure, explicitOutcome: true);
        Assert.Null(failure); Assert.False(callback); Assert.NotNull(result);
        Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
        Assert.True(result.NeedsFollowUp); Assert.True(result.ContinuationBlocked);
        Assert.Equal(lockAtCut, File.ReadAllBytes(files.ResolvePath(LocalUiSessionLockService.LockPath)));
        Assert.Equal(Before, File.ReadAllBytes(files.ResolvePath(Member)));
        Assert.Equal(new byte[] { 71, 83 }, File.ReadAllBytes(files.ResolvePath(second)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PreparationUnknown_OriginalDirectGachaStopsQueueAndOwnerCleanup(bool cleanup)
    {
        using var probe = new PreparationPublicationProbe();
        using var fixture = new BrowserDirectGachaLinuxFixture(output) { StorageObservationHooks = probe.Hooks };
        await fixture.InitializeAsync();
        probe.Attach(fixture.Files);
        fixture.Publication = probe.Hooks.LocalPublicationObserver;
        fixture.Closing = probe.Hooks.SessionOperationClosingAsync;
        var requestFailure = new InvalidOperationException("known final queue request refusal");
        var firstCleanupFailure = new InvalidOperationException("known first manifest cleanup refusal");
        var requestRefusals = 0;
        var cleanupRefusals = 0;
        var manifestDeleteAttempts = 0;
        string? stagedBackup = null;
        byte[]? stagedBytes = null;
        byte[]? manifestBeforeCleanup = null;
        byte[]? authorityBeforeCleanup = null;
        fixture.Mutation = async path =>
        {
            await probe.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
            if (cleanup && probe.Cut.Armed && probe.Cut.Cuts == 0)
            {
                if (path == BrowserPendingTurnInspector.TurnRequestPath && requestRefusals == 0 &&
                    probe.CommittedImages.ContainsKey(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)))
                {
                    probe.ValidateManifest(requireAuthority: true);
                    manifestBeforeCleanup = File.ReadAllBytes(fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath));
                    authorityBeforeCleanup = File.ReadAllBytes(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath));
                    requestRefusals++;
                    throw requestFailure;
                }
                if (requestRefusals == 1 && path == BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath)
                {
                    manifestDeleteAttempts++;
                    if (cleanupRefusals == 0)
                    {
                        cleanupRefusals++;
                        throw firstCleanupFailure;
                    }
                }
            }
        };
        probe.Select = (path, member) => cleanup
            ? requestRefusals == 1 && cleanupRefusals == 1 && manifestDeleteAttempts == 2 &&
              path == fixture.Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath) &&
              !member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : path == fixture.Files.ResolvePath(BrowserPendingTurnInspector.TurnRequestPath);
        probe.BeforeCut = () =>
        {
            stagedBackup = fixture.BackupPath();
            stagedBytes = File.ReadAllBytes(stagedBackup);
            Assert.Equal(fixture.BeforeSoul, stagedBytes);
            Assert.Equal(11, fixture.Feathers());
            Assert.True(probe.CommittedImages.ContainsKey(fixture.Files.ResolvePath(BrowserDirectGachaLinuxFixture.Soul)));
            if (!cleanup)
            {
                var manifest = probe.ValidateManifest(requireAuthority: true);
                var request = JsonNode.Parse(File.ReadAllText(probe.SelectedPath!))!.AsObject();
                Assert.Equal(manifest["requestId"]!.GetValue<string>(), request["requestId"]!.GetValue<string>());
            }
            else
            {
                Assert.NotNull(manifestBeforeCleanup); Assert.NotNull(authorityBeforeCleanup);
                Assert.Equal(authorityBeforeCleanup, File.ReadAllBytes(fixture.Files.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
                Assert.Equal(manifestBeforeCleanup, journal.RootElement.GetProperty("Members")[0].GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
            }
        };
        probe.Arm();
        BrowserPromptWriteResult? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await fixture.PullAsync());
        output.WriteLine(JsonSerializer.Serialize(new { cleanup, requestRefusals, cleanupRefusals, manifestDeleteAttempts,
            stagedBackup, stagedBytes, manifestBeforeCleanup, authorityBeforeCleanup, result, Failure = failure?.ToString(),
            KnownOwnerCauseRetained = probe.Cut.RetainsDiagnostic(firstCleanupFailure), Evidence = probe.Evidence() }));
        probe.AssertStopped(failure, explicitOutcome: true);
        Assert.Null(failure); Assert.NotNull(result); Assert.False(result.Success);
        Assert.Equal(CommandExecutionState.Failed, result.State);
        Assert.Contains("не подтвержд", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(stagedBackup); Assert.Equal(stagedBytes, File.ReadAllBytes(stagedBackup));
        if (cleanup)
        {
            Assert.Equal(1, requestRefusals); Assert.Equal(1, cleanupRefusals); Assert.Equal(2, manifestDeleteAttempts);
            Assert.True(probe.Cut.RetainsDiagnostic(firstCleanupFailure));
        }
    }
}
