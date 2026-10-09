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
    [InlineData("profile", false)]
    [InlineData("profile", true)]
    [InlineData("prompt_generation", false)]
    [InlineData("prompt_generation", true)]
    [InlineData("prompt_lock", false)]
    [InlineData("prompt_lock", true)]
    [InlineData("qte_terminal", false)]
    [InlineData("qte_terminal", true)]
    public async Task RemainingOriginalOwningPublishersPreserveActualUncertaintyOnClose(string mode, bool uncertain)
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
        string? target = null; string? qteAtPause = null;
        DarenShowcaseAttemptState? attempt = null;
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
                            qteAtPause = attempt == null ? null : JsonSerializer.Serialize(attempt);
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
        var profile = new DarenQteRewardProfileService(files);
        QteSceneService? qte = null; string? terminalAction = null;
        if (mode == "qte_terminal")
        {
            var characteristics = new CharacteristicsService(files, manager, NullLogger<CharacteristicsService>.Instance);
            qte = new QteSceneService(files, manager.Settings, characteristics,
                null!, null!, null!, null!, null!, manager, NullLogger<QteSceneService>.Instance);
            attempt = qte.StartDarenShowcaseAttempt();
            for (var count = 0; count < 64; count++)
            {
                var scene = attempt.ActiveScene!;
                var chapter = Assert.Single(scene.Offer!.Chapters.Where(item => item.ChapterId == scene.CurrentChapterId));
                var action = Assert.Single(chapter.Actions);
                if (!string.IsNullOrWhiteSpace(action.Routing.Success.TerminalOutcomeId))
                { terminalAction = action.ActionId; break; }
                await qte.ResolveDarenShowcaseActionAsync(attempt, action.ActionId, "success");
                Assert.Equal("Active", attempt.State);
            }
            Assert.NotNull(terminalAction); Assert.Equal("Active", attempt.State);
            Assert.Null(attempt.LastCompletion); Assert.Null(attempt.Ending);
        }
        target = mode switch
        {
            "bootstrap" => files.ResolvePath("config.json"),
            "prompt_generation" => files.SessionGenerationPath,
            "profile" or "qte_terminal" => Path.Combine(root, DarenQteRewardProfileService.ProfileRelativePath),
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
            "profile" => "DarenQteRewardProfileService+<RunCanonicalAsync>",
            "qte_terminal" => "QteSceneService+<ResolveDarenShowcaseActionAsync>",
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
                "profile" => profile.RecordCompletionAsync(DarenQteRewardProfileService.ResolveEnding(true, 90), DateTime.UtcNow),
                "qte_terminal" => qte!.ResolveDarenShowcaseActionAsync(attempt!, terminalAction!, "success"),
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
            generationBefore, generationAfter, targetAfter, qteAtPause,
            qteAfter = attempt == null ? null : JsonSerializer.Serialize(attempt), Cut = cut.Evidence() }));
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
            if (attempt != null)
            { Assert.Equal("Active", attempt.State); Assert.Null(attempt.LastCompletion); Assert.Null(attempt.Ending); Assert.Equal(qteAtPause, JsonSerializer.Serialize(attempt)); }
            var sessions = promptService.GetType().GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(promptService)!;
            Assert.Equal(0, sessions.GetType().GetProperty("Count")!.GetValue(sessions));
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls);
            Assert.NotNull(targetAfter); Assert.False(targetAfter.SequenceEqual(CleanupPublicationCut.Foreign));
            Assert.False(File.Exists(cut.JournalPath));
            var returned = operation!.GetType().GetProperty("Result")!.GetValue(operation);
            using var generationDocument = JsonDocument.Parse(generationAfter!);
            var actualGeneration = generationDocument.RootElement.GetProperty("GenerationId").GetString();
            if (mode != "prompt_generation") Assert.Equal(generationBefore, generationAfter);
            else Assert.False(string.IsNullOrWhiteSpace(actualGeneration));
            if (mode == "bootstrap")
            {
                Assert.Equal(actualGeneration, Assert.IsType<string>(returned));
                var decoded = JsonSerializer.Deserialize<GameSettings>(targetAfter, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
                Assert.NotNull(decoded);
                Assert.Equal(JsonSerializer.Serialize(manager.Settings), JsonSerializer.Serialize(decoded));
            }
            if (mode == "ui_lock")
            {
                var acquired = Assert.IsType<LocalUiSessionLockResult>(returned);
                Assert.True(acquired.Acquired); Assert.NotNull(acquired.Lease); Assert.NotNull(acquired.ActiveLock);
                Assert.Equal("owned-close", acquired.Lease.OwnerId); Assert.Equal(actualGeneration, acquired.Lease.SessionGeneration);
                using var persisted = JsonDocument.Parse(targetAfter);
                Assert.Equal(acquired.Lease.LeaseToken, persisted.RootElement.GetProperty("leaseToken").GetString());
                Assert.Equal(acquired.ActiveLock.OwnerId, persisted.RootElement.GetProperty("ownerId").GetString());
            }
            if (mode == "profile")
            {
                var recorded = Assert.IsType<DarenRewardProfileWriteResult>(returned);
                Assert.True(recorded.Updated); Assert.NotNull(recorded.Profile.DarenShowcase);
                Assert.Equal("perfect_shadow", recorded.Profile.DarenShowcase.BestTierId);
                Assert.Equal(90, recorded.Profile.DarenShowcase.BestScore);
                var persisted = JsonSerializer.Deserialize<DarenRewardProfileState>(targetAfter)!;
                Assert.Equal(recorded.Profile.DarenShowcase.BestTierId, persisted.DarenShowcase!.BestTierId);
                Assert.Equal(recorded.Profile.DarenShowcase.BestScore, persisted.DarenShowcase.BestScore);
            }
            if (mode == "prompt_generation")
            {
                Assert.Same(noPrompt, returned); Assert.Equal(CommandExecutionState.Completed, noPrompt.State);
                Assert.Null(noPrompt.InteractiveSession);
                var sessions = promptService.GetType().GetField("_sessions", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(promptService)!;
                Assert.Equal(0, sessions.GetType().GetProperty("Count")!.GetValue(sessions));
            }
            if (attempt != null)
            {
                var completed = Assert.IsType<QteActionResolution>(returned);
                Assert.Equal("Completed", completed.State); Assert.Equal("Completed", attempt.State);
                Assert.Same(completed.Completion, attempt.LastCompletion); Assert.NotNull(attempt.Ending);
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
        while (tasks.TryDequeue(out var task))
        {
            if (!visited.Add(task)) continue;
            var state = task.GetType().GetField("StateMachine", flags)?.GetValue(task);
            if (state == null) continue;
            var stateType = state.GetType().FullName!;
            foreach (var field in state.GetType().GetFields(flags))
            {
                var value = field.GetValue(state);
                if (stateType.Contains(boundary, StringComparison.Ordinal) && value is FileSystemManager.CanonicalWriteLease { IsActive: true } lease)
                    owners.Add((lease, stateType));
                if (value is Task child) tasks.Enqueue(child);
                else if (value != null && field.Name.StartsWith("<>u__", StringComparison.Ordinal))
                    foreach (var awaiterField in value.GetType().GetFields(flags))
                        if (awaiterField.GetValue(value) is Task awaited) tasks.Enqueue(awaited);
            }
        }
        return Assert.Single(owners.DistinctBy(owner => owner.Item1));
    }
}
