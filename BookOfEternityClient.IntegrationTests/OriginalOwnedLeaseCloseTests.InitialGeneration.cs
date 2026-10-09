using System.Runtime.ExceptionServices;
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
    public async Task OriginalParticipatingInitialGenerationPreservesItsActualDecisionOnClose(bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-initial-generation-close-" + Guid.NewGuid().ToString("N"));
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
        Task<string>? operation = null; string? result = null; Exception? failure = null;
        try
        {
            armed = true;
            operation = SessionOperationContext.RunParticipatingCurrentSessionAsync(files, () =>
            {
                callbacks++;
                Assert.True(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out boundGeneration));
                Assert.False(original!.IsActive); // Initial-generation owner closes before the actual bound callback.
                return Task.FromResult("actual bound operation");
            });
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, ownerState) = InspectOriginalNestedOwningLease(operation, "SessionOperationContext+<RunParticipatingSessionAsync>");
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe;
            cut.Armed = uncertain; allow.TrySetResult();
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
        var afterGeneration = File.ReadAllBytes(files.SessionGenerationPath);
        output.WriteLine(JsonSerializer.Serialize(new { uncertain, root, ownerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, callbacks, boundGeneration, result, failure = failure?.ToString(), afterGeneration,
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], releaseFailure), lockAvailable,
            activeAfter = original!.IsActive, mainClosed = original.MainAdmission == null,
            ambientClosed = original.AmbientRegistration == null, contextClosed = original.ExternalPublicationContext == null,
            Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.MainAdmission); Assert.Null(original.AmbientRegistration);
        Assert.Null(original.ExternalPublicationContext);
        Assert.False(SessionOperationContext.TryGetExpectedGeneration(files.BasePath, out _));
        if (uncertain)
        {
            Assert.Null(result); Assert.Equal(0, callbacks); Assert.Null(boundGeneration);
            Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
            Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(releaseFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
            cut.AssertReachedAndStopped();
        }
        else
        {
            Assert.Null(failure); Assert.Equal(1, callbacks); Assert.Equal("actual bound operation", result);
            Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls);
            using var document = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(afterGeneration));
            Assert.Equal(boundGeneration, document.RootElement.GetProperty("GenerationId").GetString());
            Assert.False(File.Exists(cut.JournalPath));
        }
    }
}
