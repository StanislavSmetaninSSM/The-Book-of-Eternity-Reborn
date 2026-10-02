using System.IO.Compression;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Detects original browser recorder use, guessed save identity or outcome loss after the save decision.
/// </summary>
public sealed class PortableBrowserSaveCreationTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-save-create-" + Guid.NewGuid().ToString("N"));

    /// <summary>
    /// Proves the actual browser caller creates one image decision and retains exact outcome and identity.
    /// </summary>
    /// <param name="scenario">
    /// Selects ordinary commit, confirmed rollback, uncertain evidence, committed cleanup or bound-close failure.
    /// </param>
    [Theory]
    [InlineData("committed")]
    [InlineData("rollback")]
    [InlineData("uncertain")]
    [InlineData("committed-cleanup")]
    [InlineData("committed-close")]
    public async Task BrowserSaveUsesPreparedImageDecisionAndRetainsExactOutcome(string scenario)
    {
        FileSystemManager? files = null;
        string? target = null;
        var preparingSave = false; var reached = false; var committed = false; var saveCommits = 0; var recorder = false;
        files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path =>
                {
                    if (path.Replace('\\', '/').StartsWith("saves/manual_saves/", StringComparison.Ordinal) && path.EndsWith(".zip", StringComparison.Ordinal))
                    { target = path.Replace('\\', '/'); preparingSave = true; }
                    return Task.CompletedTask;
                },
                LocalPublicationObserver = (phase, _) =>
                {
                    if (!preparingSave) return;
                    if (phase == TrustedLocalPublicationPhase.IntentPublished)
                    {
                        reached = true;
                        using var journal = File.OpenRead(Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1/active.json"));
                        var magic = new byte[8]; journal.ReadExactly(magic); Assert.Equal("BOELP2\r\n"u8.ToArray(), magic);
                    }
                    if (phase == TrustedLocalPublicationPhase.Committed)
                    {
                        saveCommits++; committed = true; preparingSave = false;
                        if (scenario == "committed-cleanup") throw new InvalidOperationException("Deliberate committed save cleanup debt.");
                    }
                    if (phase == TrustedLocalPublicationPhase.MemberPublished && scenario is "rollback" or "uncertain")
                    {
                        preparingSave = false;
                        if (scenario == "uncertain") File.WriteAllBytes(files!.ResolvePath(target!), [99]);
                        throw new InvalidOperationException("Deliberate browser save interruption.");
                    }
                },
                SessionOperationClosingAsync = () =>
                {
                    if (committed && scenario == "committed-close") throw new IOException("Deliberate save bound-close failure.");
                    return Task.CompletedTask;
                }
            });
        var state = PortableSaveFixture.Seed(files);
        var generation = File.ReadAllBytes(files.SessionGenerationPath);
        var future = files.ResolvePath("saves/manual_saves/future-existing.zip");
        using (var archive = ZipFile.Open(future, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("save_metadata.json");
            using var writer = new StreamWriter(entry.Open());
            writer.Write(JsonSerializer.Serialize(new { saveName = "same-name", description = "old untouched archive", timestamp = "2099-01-01T00:00:00Z" }));
        }
        var futureBytes = File.ReadAllBytes(future);
        var save = new SaveLoadService(files, state, NullLogger<SaveLoadService>.Instance, new SaveLoadServiceHooks
        {
            BeforeSaveCommitAsync = () =>
            {
                recorder = Directory.Exists(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root));
                return Task.CompletedTask;
            }
        });
        var coordinator = new BrowserLocalWriteCoordinator(files, new LocalUiSessionLockService(files));
        var session = new LocalWebUiSessionStatusService(files, coordinator);
        var lifecycle = new BrowserLifecycleDashboardService(files, session, new ValidationService(files, NullLogger<ValidationService>.Instance));
        var menu = new LocalWebUiMainMenuService(files, lifecycle, save, state, coordinator);
        var result = await menu.CreateManualSaveAsync(new BrowserCreateSaveRequest("same-name"));
        Assert.True(reached, "The actual browser save did not reach its prepared v2 image intent.");
        Assert.False(recorder);
        Assert.NotNull(target);
        Assert.Equal(generation, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(futureBytes, File.ReadAllBytes(future));
        if (scenario is "committed" or "committed-cleanup" or "committed-close")
        {
            Assert.True(result.Success, result.Error);
            Assert.Equal(BrowserPreparedWriteDisposition.Committed, result.Disposition);
            Assert.Equal(1, saveCommits);
            Assert.Equal("manual:" + Path.GetFileName(target), result.CreatedSaveId);
            Assert.NotEqual("manual:future-existing.zip", result.CreatedSaveId);
            Assert.True(File.Exists(files.ResolvePath(target!)));
            if (scenario != "committed") Assert.True(result.NeedsFollowUp);
        }
        else
        {
            Assert.False(result.Success); Assert.Empty(result.CreatedSaveId); Assert.Equal(0, saveCommits);
            Assert.Equal(scenario == "rollback" ? BrowserPreparedWriteDisposition.RolledBack : BrowserPreparedWriteDisposition.Uncertain, result.Disposition);
            if (scenario == "rollback") Assert.False(File.Exists(files.ResolvePath(target!)));
            else
            {
                Assert.True(result.NeedsFollowUp); Assert.True(result.ContinuationBlocked); Assert.Null(result.Menu);
                Assert.Equal(new byte[] { 99 }, File.ReadAllBytes(files.ResolvePath(target!)));
                Assert.True(File.Exists(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1/active.json")));
            }
        }
        Assert.False(Directory.Exists(files.ResolvePath(ExplorerLocalTurnRollbackArtifacts.Root)));
        Assert.Empty(Directory.EnumerateFileSystemEntries(Path.Combine(files.RuntimeRootPath, "save-staging")));
    }

    /// <summary>
    /// Removes only this case's independently owned root after browser-owned lifetimes end.
    /// </summary>
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true); }
}
