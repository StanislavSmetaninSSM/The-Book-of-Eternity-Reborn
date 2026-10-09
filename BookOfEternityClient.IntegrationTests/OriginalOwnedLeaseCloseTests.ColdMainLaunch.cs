using System.Runtime.ExceptionServices;
using System.Reflection;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmRuntime;
using System.Text.Json;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalColdMainLaunchPreservesActualGenerationUncertaintyBeforePreparation(bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-cold-main-launch-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (armed && acquisitionPauses == 0)
                    { acquisitionPauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
                },
                LocalPublicationObserver = (phase, index) =>
                {
                    if (armed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                    { publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult(); }
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        Assert.False(File.Exists(files.SessionGenerationPath));
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));

        cut.Select = (path, _) => path == files.SessionGenerationPath;
        var releaseFailure = new IOException("Actual original initial-generation owner close failure.");
        var closer = new ThrowingClose(releaseFailure);
        FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0; var callbacks = 0; string? boundGeneration = null; string? ownerState = null;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null &&
                Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var coordinator = await GmSessionRunCoordinator.OpenNeutralAsync(files);
        Assert.Null(coordinator.Record); Assert.True(coordinator.RetainsAuthority);
        var beforePreparedFailure = new InvalidOperationException("Original pre-Prepared callback stops before native preparation.");
        var prepareCalls = 0;
        Func<string, Task<OwnedTerminalSessionFactory.PreparedTerminal>> prepare = _ =>
        { prepareCalls++; throw new InvalidOperationException("No native preparation is allowed in this cold fixture."); };
        Func<FileSystemManager.CanonicalWriteLease, Task> beforePrepared = lease =>
        { callbacks++; boundGeneration = files.ReadExistingSessionGeneration(lease); throw beforePreparedFailure; };
        var launch = typeof(GmSessionRunCoordinator).GetMethod("LaunchAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        Task<IOwnedTerminalSession>? operation = null; Exception? failure = null;
        try
        {
            armed = true;
            operation = (Task<IOwnedTerminalSession>)launch.Invoke(coordinator, [prepare, CancellationToken.None, null, beforePrepared])!;
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, ownerState) = InspectOriginalNestedOwningLease(operation, "GmSessionRunCoordinator+<LaunchAsync>");
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe;
            cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(async () => await operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(async () => await operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var afterGeneration = File.ReadAllBytes(files.SessionGenerationPath);
        output.WriteLine(JsonSerializer.Serialize(new { uncertain, root, ownerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, callbacks, boundGeneration, prepareCalls, Retired = !coordinator.RetainsAuthority, RecordPresent = coordinator.Record != null, failure = failure?.ToString(), afterGeneration,
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], releaseFailure), lockAvailable,
            activeAfter = original!.IsActive, mainClosed = original.MainAdmission == null,
            ambientClosed = original.AmbientRegistration == null, contextClosed = original.ExternalPublicationContext == null,
            Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.MainAdmission); Assert.Null(original.AmbientRegistration);
        Assert.Null(original.ExternalPublicationContext);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
        Assert.Equal(0, prepareCalls); Assert.Null(coordinator.Record); Assert.False(coordinator.RetainsAuthority);
        using (var guardProbe = await GmMainOwnerGuard.AcquireAsync(files.BasePath)) { }

        if (uncertain)
        {
            Assert.Equal(0, callbacks); Assert.Null(boundGeneration);
            Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
            Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(releaseFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
            cut.AssertReachedAndStopped();
        }
        else
        {
            Assert.Same(beforePreparedFailure, failure); Assert.Equal(1, callbacks);
            Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls);
            using var document = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(afterGeneration));
            Assert.Equal(boundGeneration, document.RootElement.GetProperty("GenerationId").GetString());
            Assert.False(File.Exists(cut.JournalPath));
        }
    }
}
