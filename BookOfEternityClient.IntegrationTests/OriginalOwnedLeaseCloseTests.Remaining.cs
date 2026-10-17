using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("bootstrap", false)]
    [InlineData("bootstrap", true)]
    [InlineData("ui_lock", false)]
    [InlineData("ui_lock", true)]
    [InlineData("prompt_generation", false)]
    [InlineData("prompt_generation", true)]
    [InlineData("prompt_lock", false)]
    [InlineData("prompt_lock", true)]
    public Task RemainingOriginalOwningPublishersPreserveActualUncertaintyOnClose(string mode, bool uncertain) =>
        RemainingOriginalOwningPublisherCoreAsync(mode, uncertain);

    private async Task RemainingOriginalOwningPublisherCoreAsync(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-other-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        FileSystemManager? files = null;
        string? target = null;
        files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (launchArmed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                    {
                        using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        if (actual.RootElement.GetProperty("Members").EnumerateArray().Any(m => m.GetProperty("Path").GetString() == target))
                        {
                            publicationPauses++;
                            entered.TrySetResult(); allow.Task.GetAwaiter().GetResult();
                        }
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
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        if (mode != "prompt_generation")
            await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        var manager = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        var lockService = new LocalUiSessionLockService(files);
        var promptService = new ExplorerWebPromptSessionService(files, manager, lockService);
        target = mode switch
        {
            "bootstrap" => files.ResolvePath("config.json"),
            "prompt_generation" => files.SessionGenerationPath,
            _ => files.ResolvePath(LocalUiSessionLockService.LockPath)
        };
        Assert.False(File.Exists(target));
        cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var beforeRuntime = manager.CurrentState;
        var releaseFailure = new IOException("Actual remaining original owner close failure.");
        var closer = new ThrowingClose(releaseFailure);
        FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null &&
                Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var prompt = new ExplorerCommandResult { Command = "/world_setup", State = CommandExecutionState.RequiresInput,
            Prompts = [new UiTextInputPrompt { Id = "setting", Prompt = "World", Required = true }] };
        var noPrompt = new ExplorerCommandResult { Command = "/inspect", State = CommandExecutionState.Completed };
        var boundary = mode switch
        {
            "bootstrap" => "StateManager+<BootstrapLocalStorageCoreAsync>",
            "ui_lock" => "LocalUiSessionLockService+<RunCanonicalAsync>",
            _ => "ExplorerWebPromptSessionService+<AttachSessionIfNeededAsync>"
        };
        Task? operation = null; Exception? failure = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = mode switch
            {
                "bootstrap" => manager.BootstrapLocalStorageAsync(),
                "ui_lock" => lockService.AcquireOrRefreshAsync(new("owned-close", "test", "Original lock", TimeSpan.FromSeconds(120)), "Original acquire"),
                "prompt_generation" => promptService.AttachSessionIfNeededAsync(noPrompt, new("/inspect")),
                _ => promptService.AttachSessionIfNeededAsync(prompt, new("/world_setup"),
                    JsonDocument.Parse(generationBefore!).RootElement.GetProperty("GenerationId").GetString()!)
            };
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe;
            cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var targetAfter = CleanupPublicationCut.ReadOptional(target);
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, actualOwnerState,
            acquisitionPauses, publicationPauses, attachments, closer.Calls, failure = failure?.ToString(),
            samePrimary = ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], releaseFailure),
            lockAvailable, activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null,
            mainClosed = original.MainAdmission == null, contextClosed = original.ExternalPublicationContext == null,
            generationBefore, generationAfter, targetAfter, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission);
        Assert.Null(original.ExternalPublicationContext); Assert.True(lockAvailable);
        Assert.Same(beforeRuntime, manager.CurrentState);
        if (uncertain)
        {
            Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
            Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(releaseFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
            cut.AssertReachedAndStopped();
            if (mode != "prompt_generation") Assert.Equal(generationBefore, generationAfter);
            var sessions = promptService.GetType().GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(promptService)!;
            Assert.Equal(0, sessions.GetType().GetProperty("Count")!.GetValue(sessions));
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls);
            Assert.NotNull(targetAfter); Assert.False(targetAfter.SequenceEqual(CleanupPublicationCut.Foreign));
            Assert.False(File.Exists(cut.JournalPath));
            var returned = operation!.GetType().GetProperty("Result")!.GetValue(operation);
            using var generationDocument = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(generationAfter!));
            var actualGeneration = generationDocument.RootElement.GetProperty("GenerationId").GetString();
            if (mode != "prompt_generation") Assert.Equal(generationBefore, generationAfter);
            else Assert.False(string.IsNullOrWhiteSpace(actualGeneration));
            if (mode == "bootstrap")
            {
                Assert.Equal(actualGeneration, Assert.IsType<string>(returned));
                var decoded = StateManager.PrepareLocalLoadSettings(targetAfter);
                Assert.NotNull(decoded);
                Assert.Equal(JsonSerializer.Serialize(manager.Settings), JsonSerializer.Serialize(decoded));
            }
            if (mode == "ui_lock")
            {
                var acquired = Assert.IsType<LocalUiSessionLockResult>(returned);
                Assert.True(acquired.Acquired); Assert.NotNull(acquired.Lease); Assert.NotNull(acquired.ActiveLock);
                Assert.Equal("owned-close", acquired.Lease.OwnerId); Assert.Equal(actualGeneration, acquired.Lease.SessionGeneration);
                using var persisted = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(targetAfter));
                Assert.Equal(acquired.Lease.LeaseToken, persisted.RootElement.GetProperty("leaseToken").GetString());
                Assert.Equal(acquired.ActiveLock.OwnerId, persisted.RootElement.GetProperty("ownerId").GetString());
            }
            if (mode == "prompt_generation")
            {
                Assert.Same(noPrompt, returned); Assert.Equal(CommandExecutionState.Completed, noPrompt.State);
                Assert.Null(noPrompt.InteractiveSession);
                var sessions = promptService.GetType().GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(promptService)!;
                Assert.Equal(0, sessions.GetType().GetProperty("Count")!.GetValue(sessions));
            }
            if (mode == "prompt_lock")
            {
                var attached = Assert.IsType<ExplorerCommandResult>(returned);
                Assert.Equal(CommandExecutionState.RequiresInput, attached.State); Assert.NotNull(attached.InteractiveSession);
            }
        }
    }

    private static (FileSystemManager.CanonicalWriteLease Lease, string StateType) InspectOriginalNestedOwningLease(Task operation, string boundary)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var tasks = new Queue<Task>(); tasks.Enqueue(operation);
        var visited = new HashSet<Task>(ReferenceEqualityComparer.Instance);
        var owners = new List<(FileSystemManager.CanonicalWriteLease, string)>();
        var actualStates = new List<object>();
        var capturedObjects = new HashSet<object>(ReferenceEqualityComparer.Instance);
        void InspectOriginalCapturedOwner(object? value, string location)
        {
            if (value is FileSystemManager.CanonicalWriteLease { IsActive: true } lease)
            { owners.Add((lease, location)); return; }
            if (value == null || !capturedObjects.Add(value)) return;
            var type = value.GetType();
            // The original RunCanonical/prompt lambdas capture their owning lease
            // in a compiler-generated DisplayClass, rather than a direct state field.
            // Follow only that actual hoisted capture, never service/FS object graphs.
            if (!type.IsDefined(typeof(System.Runtime.CompilerServices.CompilerGeneratedAttribute), false) ||
                !type.Name.Contains("DisplayClass", StringComparison.Ordinal)) return;
            foreach (var captured in type.GetFields(flags))
                InspectOriginalCapturedOwner(captured.GetValue(value), location + "/" + captured.Name);
        }
        while (tasks.TryDequeue(out var task))
        {
            if (!visited.Add(task)) continue;
            var state = task.GetType().GetField("StateMachine", flags)?.GetValue(task);
            if (state == null) continue;
            var stateType = state.GetType().FullName!;
            actualStates.Add(new { TaskType = task.GetType().FullName, StateType = stateType,
                Fields = state.GetType().GetFields(flags).Select(field => new { field.Name,
                    DeclaredType = field.FieldType.FullName, ActualType = field.GetValue(state)?.GetType().FullName }) });
            foreach (var field in state.GetType().GetFields(flags))
            {
                var value = field.GetValue(state);
                if (stateType.Contains(boundary, StringComparison.Ordinal))
                    InspectOriginalCapturedOwner(value, stateType + "/" + field.Name);
                if (value is Task child) tasks.Enqueue(child);
                else if (value != null && field.Name.StartsWith("<>u__", StringComparison.Ordinal))
                    foreach (var awaiterField in value.GetType().GetFields(flags))
                        if (awaiterField.GetValue(value) is Task awaited) tasks.Enqueue(awaited);
            }
        }
        var distinct = owners.DistinctBy(owner => owner.Item1).ToArray();
        Assert.True(distinct.Length == 1,
            "Actual owning-lease preparation failed: " + JsonSerializer.Serialize(new { boundary, OwnerCount = distinct.Length, actualStates }));
        return distinct[0];
    }
}
