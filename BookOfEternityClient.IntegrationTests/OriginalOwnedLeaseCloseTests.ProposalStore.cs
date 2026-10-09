using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("known")] [InlineData("known_close")] [InlineData("uncertain")] [InlineData("task_refused")]
    public async Task OriginalProposalStoreRetainsActualPublishedWarningAndStopsOnUncertainty(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-proposal-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var intentEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowIntent = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowCallback = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false; var auditStarted = false; var acquisitions = 0; var pauses = 0; var callbacks = 0;
        string? target = null;
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (armed && auditStarted && mode == "uncertain" && phase == TrustedLocalPublicationPhase.IntentPublished && pauses == 0)
                {
                    using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                    target = actual.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString();
                    pauses++; intentEntered.TrySetResult(); allowIntent.Task.GetAwaiter().GetResult();
                }
                cut.Hooks.LocalPublicationObserver!(phase, index);
            },
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                if (armed && acquisitions == 0) { acquisitions++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
            },
            BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
            AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
            SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
        };
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
        files.EnsureDirectoryStructure(); cut.Attach(files);
        using var admission = new GmWorkerNativePoolAdmission(root, root);
        // This factory selects only the existing isolated local rename adapter. No Enter/host/worker/ledger/ACK is used.
        var store = admission.CreateProposalStore(files);
        var proposal = GmWorkerBridgeTestFixtures.NarrativeDraftProposal();
        var taskPath = GmWorkerBridgePool.GetTaskPacketPath(proposal.TaskId);
        var inboxPath = GmWorkerBridgePool.GetProposalInboxPath(proposal.TaskId);
        var proposalPath = GmWorkerProposalStore.GetProposalPath(proposal.ProposalId);
        const string auditPath = "game_state/control/original_proposal_derived_audit.json";
        var auditBytes = Encoding.UTF8.GetBytes("{\"actualAudit\":\"published before ordinary warning\"}");
        var proposalBytes = Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(proposal));
        string generation;
        await using (var setup = await files.AcquireCanonicalWriteLeaseAsync()) generation = files.GetOrCreateSessionGeneration(setup);
        var task = GmWorkerBridgeTestFixtures.NarrativeDraftTask() with { SessionGeneration = generation };
        var taskBytes = Encoding.UTF8.GetBytes(GmWorkerJson.Serialize(task));
        await files.WriteFileAtomicBytesAsync(taskPath, taskBytes);
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles(); Dictionary<string, byte[]>? atWitnessFiles = null;
        var callbackWarning = new InvalidOperationException("actual audit completed; original callback diagnostic failed");
        var closeFailure = new IOException("actual original proposal owner late close");
        var closer = new ProposalStoreThrowingClose(closeFailure);
        FileSystemManager.CanonicalWriteLease? original = null; FileSystemManager.CanonicalWriteLease? callbackLease = null;
        var attachments = 0; string? actualOwnerState = null;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        cut.Select = (path, _) => path == target;
        async Task PublishActualAuditAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            callbacks++; callbackLease = lease; auditStarted = true;
            await files.WriteFileAtomicBytesAsync(lease, auditPath, auditBytes);
            callbackEntered.TrySetResult(); await allowCallback.Task;
            throw callbackWarning;
        }
        Task<WorkerProposalPublicationResult>? operation = null; WorkerProposalPublicationResult? result = null; Exception? failure = null;
        try
        {
            armed = true;
            operation = store.PublishBundleAsync(proposal, proposalBytes, new Dictionary<string, byte[]>(), taskPath,
                mode == "task_refused" ? Encoding.UTF8.GetBytes("other task bytes") : taskBytes, generation, inboxPath, PublishActualAuditAsync, durableExecution: null);
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            if (mode != "task_refused")
            {
                var gate = mode == "uncertain" ? intentEntered.Task : callbackEntered.Task;
                var ready = await Task.WhenAny(gate, operation).WaitAsync(TimeSpan.FromSeconds(12));
                if (ReferenceEquals(ready, operation)) output.WriteLine(JsonSerializer.Serialize(new { mode, PreparationFailure = (await Record.ExceptionAsync(() => operation))?.ToString(), EarlyResult = operation.IsCompletedSuccessfully ? operation.Result : null }));
                Assert.Same(gate, ready);
                (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, "GmWorkerProposalStore+<PublishBundleAsync>");
                Assert.Same(files, original.Owner); Assert.Same(original, callbackLease); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
                atWitnessFiles = ReadCanonicalFiles();
                Assert.Equal(proposalBytes, atWitnessFiles[files.ResolvePath(proposalPath)]);
                Assert.Equal(proposalBytes, atWitnessFiles[files.ResolvePath(inboxPath)]);
                if (mode != "uncertain") Assert.Equal(auditBytes, atWitnessFiles[files.ResolvePath(auditPath)]);
                AppDomain.CurrentDomain.FirstChanceException += observe;
                if (mode == "known_close") { original.ExternalPublicationContext = closer; attachments++; }
                cut.Armed = mode == "uncertain"; allowIntent.TrySetResult(); allowCallback.TrySetResult();
            }
            failure = await Record.ExceptionAsync(() => operation); if (failure == null) result = await operation;
        }
        finally
        {
            allowAcquire.TrySetResult(); allowIntent.TrySetResult(); allowCallback.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable; using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var afterFiles = ReadCanonicalFiles();
        var stagingArea = Path.Combine(files.RuntimeRootPath, "proposal-staging");
        var retainedStagingFiles = Directory.Exists(stagingArea) ? Directory.GetFileSystemEntries(stagingArea) : [];
        output.WriteLine(JsonSerializer.Serialize(new { mode, root, target, acquisitions, pauses, callbacks, actualOwnerState, attachments, closer.Calls,
            result, failure = failure?.ToString(), samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original?.IsActive, ambientClosed = original == null || original.AmbientRegistration == null,
            mainClosed = original == null || original.MainAdmission == null, contextClosed = original == null || original.ExternalPublicationContext == null,
            generationBefore, generationAfter = File.ReadAllBytes(files.SessionGenerationPath), retainedStagingFiles, beforeFiles, atWitnessFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitions); Assert.True(lockAvailable); Assert.Empty(retainedStagingFiles); Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
        if (mode == "task_refused")
        {
            Assert.Null(failure); Assert.NotNull(result); Assert.False(result.Published); Assert.False(result.SessionReplaced); Assert.Contains("no longer belongs", result.Error, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(0, callbacks); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls);
            Assert.Equal(beforeFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            foreach (var pair in beforeFiles) Assert.Equal(pair.Value, afterFiles[pair.Key]);
        }
        else
        {
            Assert.False(original!.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext); Assert.Equal(1, callbacks);
            if (mode == "uncertain")
            {
                Assert.Null(result); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
                Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
                Assert.Equal(atWitnessFiles!.Keys.Where(path => path != target).Order(StringComparer.Ordinal), afterFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal));
                foreach (var pair in atWitnessFiles.Where(pair => pair.Key != target)) Assert.Equal(pair.Value, afterFiles[pair.Key]);
            }
            else
            {
                Assert.Null(failure); Assert.NotNull(result); Assert.True(result.Published); Assert.False(result.SessionReplaced); Assert.Null(result.Error);
                Assert.Contains(callbackWarning.Message, result.Warning, StringComparison.Ordinal);
                Assert.Equal(mode == "known_close" ? 1 : 0, attachments); Assert.Equal(attachments, closer.Calls);
                Assert.Equal(atWitnessFiles!.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
                foreach (var pair in atWitnessFiles) Assert.Equal(pair.Value, afterFiles[pair.Key]); Assert.False(File.Exists(cut.JournalPath));
            }
        }
    }
    private sealed class ProposalStoreThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; throw failure; }
    }
}
