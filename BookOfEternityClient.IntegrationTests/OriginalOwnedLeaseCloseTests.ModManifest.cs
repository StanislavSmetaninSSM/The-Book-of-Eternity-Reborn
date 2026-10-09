using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("normalize")]
    [InlineData("unchanged")]
    [InlineData("unchanged_clean")]
    [InlineData("committed_close")]
    [InlineData("read_failure")]
    [InlineData("write_refusal")]
    [InlineData("uncertainty")]
    [InlineData("uncertainty_close")]
    public async Task OriginalSystemModManifestCommitsCacheOnlyAfterKnownPublication(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-mod-manifest-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        Action? observeCommitted = null; byte[]? committedManifest = null;
        var armed = false; var acquisitions = 0; var readCuts = 0; var writeCuts = 0; var pauses = 0;
        var mutations = new List<string>();
        var reads = new List<string>();
        var readFailure = new IOException("Actual original mod manifest read failure.");
        var writeFailure = new IOException("Actual original mod manifest mutation refusal.");
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (!armed) return;
                    acquisitions++;
                    if ((mode is "uncertainty_close" or "committed_close") && acquisitions == 1)
                    { acquireEntered.TrySetResult(); await allowAcquire.Task; }
                },
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (armed)
                    {
                        reads.Add(path);
                        if (mode == "read_failure" && path == SystemModService.ManifestPath)
                        { readCuts++; throw readFailure; }
                    }
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationBoundaryAsync = async path =>
                {
                    await cut.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
                    if (armed)
                    {
                        mutations.Add(path);
                        if (mode == "write_refusal" && path == SystemModService.ManifestPath)
                        { writeCuts++; throw writeFailure; }
                    }
                },
                LocalPublicationObserver = (phase, index) =>
                {
                    if (armed && (mode is "uncertainty_close" or "committed_close") && phase == TrustedLocalPublicationPhase.IntentPublished && pauses == 0)
                    {
                        pauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult();
                    }
                    if (armed && mode == "committed_close" && phase == TrustedLocalPublicationPhase.Committed) observeCommitted!();
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        await files.WriteFileAtomicAsync("mods/active.md", "# Exact active mod\nOriginal content.\n");
        await files.WriteFileAtomicAsync(SystemModService.ManifestPath, "{\"prior\":\"manifest\"}");
        var settings = new GameSettings { EnabledSystemMods = ["active.md", "missing.md", "ACTIVE.MD"] };
        var service = new SystemModService(files, settings, NullLogger<SystemModService>.Instance);
        if (mode is "unchanged" or "unchanged_clean")
        {
            Assert.True(await service.WriteManifestForGmAsync());
            if (mode == "unchanged") settings.EnabledSystemMods = ["active.md", "missing.md", "ACTIVE.MD"];
        }
        var beforeCache = settings.EnabledSystemMods;
        var beforeManifest = File.ReadAllBytes(files.ResolvePath(SystemModService.ManifestPath));
        var beforeMod = File.ReadAllBytes(files.ResolvePath("mods/active.md"));
        var beforeGeneration = File.ReadAllBytes(files.SessionGenerationPath);
        var beforeFiles = CaptureModManifestPhysicalTree(root);
        cut.Select = (path, _) => path == files.ResolvePath(SystemModService.ManifestPath);
        var releaseFailure = new IOException("Actual original mod manifest owner close failure.");
        var closer = new ThrowingClose(releaseFailure);
        FileSystemManager.CanonicalWriteLease? original = null;
        string? originalState = null; var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null &&
                Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        observeCommitted = () =>
        {
            using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
            Assert.True(actual.RootElement.GetProperty("Committed").GetBoolean());
            Assert.Contains(actual.RootElement.GetProperty("Members").EnumerateArray(),
                m => m.GetProperty("Path").GetString() == files.ResolvePath(SystemModService.ManifestPath));
            Assert.NotNull(original); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            committedManifest = File.ReadAllBytes(files.ResolvePath(SystemModService.ManifestPath));
            attachments++; original.ExternalPublicationContext = closer;
        };
        Task<bool>? operation = null; bool? result = null; Exception? failure = null;
        try
        {
            armed = true; cut.Armed = mode is "uncertainty" or "uncertainty_close";
            operation = service.WriteManifestForGmAsync();
            if (mode is "uncertainty_close" or "committed_close")
            {
                Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
                allowAcquire.TrySetResult();
                Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
                (original, originalState) = InspectOriginalNestedOwningLease(operation, "+<");
                Assert.True(originalState.Contains("SystemModService+<WriteManifestForGmAsync>", StringComparison.Ordinal) ||
                    originalState.Contains("FileSystemManager+<WriteFileAtomicBytesAsync>", StringComparison.Ordinal));
                Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
                AppDomain.CurrentDomain.FirstChanceException += observe; allow.TrySetResult();
            }
            failure = await Record.ExceptionAsync(async () => result = await operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(async () => await operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var afterManifest = File.ReadAllBytes(files.ResolvePath(SystemModService.ManifestPath));
        var afterFiles = CaptureModManifestPhysicalTree(root);
        output.WriteLine(JsonSerializer.Serialize(new { mode, root, result, failure = failure?.ToString(),
            acquisitions, reads, mutations, readCuts, writeCuts, pauses, originalState, attachments, closer.Calls,
            beforeCache, afterCache = settings.EnabledSystemMods, cacheOwnerSame = ReferenceEquals(beforeCache, settings.EnabledSystemMods),
            beforeManifest, afterManifest, committedManifest, beforeGeneration, afterGeneration = File.ReadAllBytes(files.SessionGenerationPath),
            beforeFiles, afterFiles, lockAvailable, originalActive = original?.IsActive,
            originalMainClosed = original?.MainAdmission == null, originalAmbientClosed = original?.AmbientRegistration == null,
            samePrimary = ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], releaseFailure), Cut = cut.Evidence() }));
        Assert.True(lockAvailable); Assert.Equal(beforeGeneration, File.ReadAllBytes(files.SessionGenerationPath));
        Assert.Equal(beforeMod, File.ReadAllBytes(files.ResolvePath("mods/active.md")));
        if (mode is "normalize" or "unchanged" or "unchanged_clean" or "committed_close")
        {
            Assert.Null(failure); Assert.Equal(mode != "unchanged_clean", result); Assert.Equal(new[] { "active.md" }, settings.EnabledSystemMods);
            Assert.False(File.Exists(cut.JournalPath));
            using var manifest = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(afterManifest));
            Assert.Equal("active.md", Assert.Single(manifest.RootElement.GetProperty("enabledSystemMods").EnumerateArray()).GetString());
            Assert.Equal("Original content.", Assert.Single(manifest.RootElement.GetProperty("activeMods").EnumerateArray()).GetProperty("content").GetString()!.Split('\n')[1]);
            if (mode is "unchanged" or "unchanged_clean") { Assert.Equal(beforeManifest, afterManifest); Assert.Empty(mutations); }
            else Assert.Single(mutations);
            if (mode == "committed_close")
            {
                Assert.Equal(committedManifest, afterManifest); Assert.Equal(1, pauses); Assert.Equal(1, attachments);
                Assert.Equal(1, closer.Calls); Assert.False(original!.IsActive); Assert.Null(original.MainAdmission);
                Assert.Null(original.AmbientRegistration); Assert.Null(original.ExternalPublicationContext);
            }
        }
        else
        {
            Assert.Null(result); Assert.Same(beforeCache, settings.EnabledSystemMods);
            Assert.Equal(new[] { "active.md", "missing.md", "ACTIVE.MD" }, settings.EnabledSystemMods);
            if (mode is "read_failure" or "write_refusal")
            {
                Assert.Same(mode == "read_failure" ? readFailure : writeFailure, failure);
                Assert.Equal(1, mode == "read_failure" ? readCuts : writeCuts);
                Assert.Equal(beforeManifest, afterManifest); Assert.Equal(beforeFiles, afterFiles);
                Assert.False(File.Exists(cut.JournalPath));
                if (mode == "read_failure") Assert.Empty(mutations); else Assert.Single(mutations);
            }
            else
            {
                Assert.Same(cut.OriginalUncertainty, failure); cut.AssertReachedAndStopped();
                if (original != null)
                {
                    Assert.Equal(1, pauses); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
                    Assert.Same(releaseFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
                    Assert.False(original.IsActive); Assert.Null(original.MainAdmission); Assert.Null(original.AmbientRegistration);
                    Assert.Null(original.ExternalPublicationContext);
                }
            }
        }
        Assert.Equal(1, acquisitions); // A complete manifest decision uses one original owner.
    }

    private static SortedDictionary<string, string> CaptureModManifestPhysicalTree(string root) =>
        new(Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path), path => Convert.ToBase64String(File.ReadAllBytes(path))), StringComparer.Ordinal);
}
