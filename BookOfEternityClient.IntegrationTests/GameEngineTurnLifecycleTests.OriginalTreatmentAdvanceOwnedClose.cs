using System.Runtime.ExceptionServices;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("known")] [InlineData("known_close")] [InlineData("uncertain")]
    public async Task OriginalTreatmentProgressionAdvanceRetainsItsActualDecisionOnClose(string mode)
    {
        var uncertain = mode == "uncertain";
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        string? target = null;
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (launchArmed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                {
                    using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                    target = actual.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString();
                    publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult();
                }
                cut.Hooks.LocalPublicationObserver!(phase, index);
            },
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                if (launchArmed && acquisitionPauses == 0)
                { acquisitionPauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
            },
            BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
            AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
            SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
        };
        using var classOwned = new CleanupOwnedFixture(_rootPath, _directGachaOutput!.WriteLine);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(fault: null, hooks: hooks);
        await context.ReleaseLeaseAsync();
        var files = context.FileSystem; var root = context.Root;
        using var owned = new CleanupOwnedFixture(root, _directGachaOutput!.WriteLine);
        var (engine, snapshotContext) = await CreateHeldTreatmentValidationEngineAsync(context);
        await using var disposedAudio = GetPrivateField<AudioService>(engine, "_audioService");
        var refresh = await InvokePrivateTaskResultAsync(engine, "RefreshAcceptedTurnCanonicalStateForValidationAsync", HeldTreatmentPipelineContext.Turn, snapshotContext);
        var returnedTransaction = refresh.GetType().GetProperty("TreatmentResourcePublicationTransaction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(refresh);
        await using var disposedTransaction = returnedTransaction as IAsyncDisposable;
        Assert.True((bool)refresh.GetType().GetProperty("BaselineUsable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(refresh)!);
        var issues = (IEnumerable<ValidationIssue>)refresh.GetType().GetProperty("PostSealIssues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(refresh)!;
        Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(returnedTransaction);
        cut.Attach(files);
        var initialProbe = await transaction.ProbeAsync(files); Assert.True(initialProbe.IsValid); Assert.Empty(initialProbe.Issues);
        var progression = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        var control = new ProgressionControl { CurrentRealm = "Mortal World" };
        cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual treatment progression original owner late close.");
        var closer = new TreatmentAdvanceThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null; var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        const string boundary = "MortalWoundTreatmentResourcePublicationTransaction+<AdvancePublishedAgreementWithProgressionAsync>";
        Task<MortalWoundTreatmentPublicationOperationResult>? operation = null;
        Exception? failure = null; MortalWoundTreatmentPublicationOperationResult? result = null;
        Dictionary<string, byte[]>? atIntentFiles = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = transaction.AdvancePublishedAgreementWithProgressionAsync(files, progression, control);
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            var publicationOrCompletion = await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12));
            if (ReferenceEquals(publicationOrCompletion, operation))
                _directGachaOutput!.WriteLine(JsonSerializer.Serialize(new { mode, PreparationFailure = (await Record.ExceptionAsync(() => operation))?.ToString(), EarlyOutcome = operation.IsCompletedSuccessfully ? operation.Result.Outcome.ToString() : null }));
            Assert.Same(entered.Task, publicationOrCompletion);
            atIntentFiles = ReadCanonicalFiles();
            var inspector = typeof(OriginalOwnedLeaseCloseTests).GetMethod("InspectOriginalNestedOwningLease", BindingFlags.Static | BindingFlags.NonPublic)!;
            (original, actualOwnerState) = ((FileSystemManager.CanonicalWriteLease, string))inspector.Invoke(null, [operation, boundary])!;
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe;
            if (mode == "known_close") { original.ExternalPublicationContext = closer; attachments++; }
            cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null) result = await operation;
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath); var afterFiles = ReadCanonicalFiles();
        _directGachaOutput!.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, target, boundary, actualOwnerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, Outcome = result?.Outcome.ToString(), IsValid = result?.IsValid, ChangedCount = result?.ChangedCount, failure = failure?.ToString(),
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["TreatmentPublicationLeaseCloseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, generationBefore, generationAfter, beforeFiles, atIntentFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        if (uncertain)
        {
            Assert.Null(result); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["TreatmentPublicationLeaseCloseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.Equal(atIntentFiles!.Keys.Where(path => path != target).Order(StringComparer.Ordinal), afterFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal));
            foreach (var pair in atIntentFiles.Where(pair => pair.Key != target)) Assert.Equal(pair.Value, afterFiles[pair.Key]);
        }
        else
        {
            Assert.Null(failure); Assert.NotNull(result); Assert.True(result.IsValid); Assert.Empty(result.Issues); Assert.Equal(1, result.ChangedCount);
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.PublishedAgreementAdvanced, result.Outcome);
            Assert.Equal(mode == "known_close" ? 1 : 0, attachments); Assert.Equal(attachments, closer.Calls);
            Assert.False(File.Exists(cut.JournalPath));
            var schedulePath = files.ResolvePath(ProgressionScheduleService.SchedulePath);
            Assert.False(beforeFiles[schedulePath].AsSpan().SequenceEqual(afterFiles[schedulePath]));
            var reportPath = files.ResolvePath(ProgressionScheduleService.ReportPath);
            Assert.Equal(beforeFiles.Keys.Where(path => path != schedulePath && path != reportPath).Order(StringComparer.Ordinal), afterFiles.Keys.Where(path => path != schedulePath && path != reportPath).Order(StringComparer.Ordinal));
            foreach (var pair in beforeFiles.Where(pair => pair.Key != schedulePath && pair.Key != reportPath)) Assert.Equal(pair.Value, afterFiles[pair.Key]);
            var stillOwned = await transaction.ProbeAsync(files); Assert.True(stillOwned.IsValid); Assert.Empty(stillOwned.Issues);
            Assert.Equal(initialProbe.OperationKey, stillOwned.OperationKey); Assert.Equal(initialProbe.AttemptId, stillOwned.AttemptId); Assert.Equal(initialProbe.RequestFingerprint, stillOwned.RequestFingerprint);
            Assert.Equal(initialProbe.SessionGeneration, stillOwned.SessionGeneration);
            var finalized = await transaction.CompleteAsync(files);
            _directGachaOutput!.WriteLine(JsonSerializer.Serialize(new { mode, FinalOutcome = finalized.Outcome.ToString(), finalized.IsValid, finalized.ChangedCount }));
            Assert.True(finalized.IsValid); Assert.Empty(finalized.Issues); Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, finalized.Outcome);
        }
        if (uncertain)
        {
            var retained = await Record.ExceptionAsync(() => transaction.ProbeAsync(files));
            Assert.Same(cut.OriginalUncertainty, retained); cut.AssertReachedAndStopped();
        }
    }

    private sealed class TreatmentAdvanceThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; throw failure; }
    }
}
