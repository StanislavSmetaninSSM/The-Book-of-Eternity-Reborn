using System.Text;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortablePreparedSettingsTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-prepared-settings-" + Guid.NewGuid().ToString("N"));
    private readonly FileSystemManager _files;
    private readonly StateManager _state;
    private readonly BrowserLocalWriteCoordinator _coordinator;
    private Action<TrustedLocalPublicationPhase, int>? _observe;
    private Action<string>? _before;
    private bool _settingsPublication;
    private const string Projection = "game_state/core/game_settings.json";
    private static readonly BrowserLocalWriteRequest Request = new("test-settings", "Settings test", "Settings update");
    private static readonly byte[] Desired = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes("{\"language\":\"en\"}")).ToArray();

    public PortablePreparedSettingsTests()
    {
        Directory.CreateDirectory(_root);
        _files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    var logicalPath = path.Replace('\\', '/');
                    _before?.Invoke(logicalPath);
                    if (logicalPath == "config.json") _settingsPublication = true;
                    return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, index) =>
                {
                    if (_settingsPublication) _observe?.Invoke(phase, index);
                }
            });
        _state = new StateManager(_files, new GameSettings(), NullLogger<StateManager>.Instance);
        _coordinator = new BrowserLocalWriteCoordinator(_files, new LocalUiSessionLockService(_files));
    }

    private async Task<byte[]> Initialize()
    {
        await _state.BootstrapLocalStorageAsync();
        File.WriteAllBytes(_files.ResolvePath(Projection), [0xFF, 0]);
        _settingsPublication = false;
        return File.ReadAllBytes(_files.ResolvePath("config.json"));
    }

    private PreparedBrowserLocalWrite Prepared(byte[] before, Func<Task> apply) => new(
        [new("config.json", before, Desired), new(Projection, [0xFF, 0], [123, 125])], apply);

    [Fact]
    public async Task PreparedSetCommitsBeforeRuntimeApplicationAndCreatesNoLegacyBackup()
    {
        var before = await Initialize(); var applied = false;
        var result = await _coordinator.ExecutePreparedAsync(Request, _ => Task.FromResult(Prepared(before, () =>
        {
            Assert.Equal(Desired, File.ReadAllBytes(_files.ResolvePath("config.json")));
            Assert.Equal(new byte[] { 123, 125 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
            applied = true; return Task.CompletedTask;
        })));
        Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.False(result.NeedsFollowUp); Assert.True(applied);
        Assert.False(Directory.Exists(_files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
        Assert.False(File.Exists(_files.ResolvePath(LocalUiSessionLockService.LockPath)));
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("owner")]
    public async Task PreparedAdmissionBlocksBeforePreparationForPendingTurnOrActiveOwner(string blocker)
    {
        var before = await Initialize(); var prepared = false;
        if (blocker == "pending") await _files.WriteFileAtomicAsync("input/turn_request.json", "{}");
        else await new LocalUiSessionLockService(_files).AcquireOrRefreshAsync(
            new("other", "console", "Other UI", TimeSpan.FromSeconds(120)), "Other operation");
        var result = await _coordinator.ExecutePreparedAsync(Request, _ =>
        {
            prepared = true; return Task.FromResult(Prepared(before, () => Task.CompletedTask));
        });
        Assert.Equal(BrowserPreparedWriteDisposition.Blocked, result.Disposition);
        Assert.False(prepared); Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
    }

    [Theory]
    [InlineData("rollback")]
    [InlineData("committed-cleanup")]
    [InlineData("unknown")]
    [InlineData("unknown-close")]
    public async Task PreparedOutcomeSeparatesRollbackCommitDebtAndUnknownEvidence(string cut)
    {
        var before = await Initialize(); var applied = false; var injected = false;
        _observe = (phase, _) =>
        {
            var wanted = cut == "committed-cleanup" ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished;
            if (injected || phase != wanted) return;
            injected = true;
            if (cut.StartsWith("unknown", StringComparison.Ordinal)) File.WriteAllBytes(_files.ResolvePath(Projection), [99]);
            if (cut == "unknown-close")
                SessionOperationContext.MarkReplaced(_root, null, "Revoked after partial publication with unknown bytes.");
            throw new InvalidOperationException("Injected settings interruption.");
        };
        var result = await _coordinator.ExecutePreparedAsync(Request, _ => Task.FromResult(Prepared(before, () =>
        {
            applied = true; return Task.CompletedTask;
        })));
        Assert.True(injected);
        if (cut == "rollback")
        {
            Assert.Equal(BrowserPreparedWriteDisposition.RolledBack, result.Disposition); Assert.False(applied);
            Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
            Assert.Equal(new byte[] { 0xFF, 0 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
        }
        else if (cut == "committed-cleanup")
        {
            Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
            Assert.True(result.NeedsFollowUp); Assert.True(applied);
            Assert.Equal(Desired, File.ReadAllBytes(_files.ResolvePath("config.json")));
        }
        else
        {
            Assert.Equal(BrowserPreparedWriteDisposition.Uncertain, result.Disposition); Assert.False(applied);
            Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
            Assert.Equal(Desired, File.ReadAllBytes(_files.ResolvePath("config.json")));
            Assert.True(File.Exists(Path.Combine(_files.RuntimeRootPath, "trusted-local-publication-v1", "active.json")));
        }
    }

    [Theory]
    [InlineData("runtime")]
    [InlineData("owner-cleanup")]
    public async Task PostCommitFailureReportsFollowUpWithoutRollingBackAcceptedFiles(string failure)
    {
        var before = await Initialize(); var runtimeReached = false;
        _before = path =>
        {
            if (runtimeReached && failure == "owner-cleanup" && path == LocalUiSessionLockService.LockPath)
                throw new IOException("Injected owner cleanup failure.");
        };
        var result = await _coordinator.ExecutePreparedAsync(Request, _ => Task.FromResult(Prepared(before, () =>
        {
            runtimeReached = true;
            if (failure == "runtime") throw new InvalidOperationException("Injected runtime application failure.");
            return Task.CompletedTask;
        })));
        Assert.True(runtimeReached);
        Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
        Assert.True(result.NeedsFollowUp);
        Assert.Equal(Desired, File.ReadAllBytes(_files.ResolvePath("config.json")));
        Assert.Equal(new byte[] { 123, 125 }, File.ReadAllBytes(_files.ResolvePath(Projection)));
    }

    [Fact]
    public async Task PreparedGenerationRevocationAtMutationBoundaryCannotPublishSettings()
    {
        var before = await Initialize(); var applied = false;
        _before = path =>
        {
            if (path == "config.json") _ = SessionOperationContext.MarkReplaced(_root, null, "Revoked before settings publication.");
        };
        var result = await _coordinator.ExecutePreparedAsync(Request, _ => Task.FromResult(Prepared(before, () =>
        {
            applied = true; return Task.CompletedTask;
        })));
        Assert.Equal(BrowserPreparedWriteDisposition.Blocked, result.Disposition); Assert.False(applied);
        Assert.Equal(before, File.ReadAllBytes(_files.ResolvePath("config.json")));
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
