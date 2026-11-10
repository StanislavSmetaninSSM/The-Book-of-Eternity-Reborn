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
    [InlineData("inactive_archive", false)] [InlineData("inactive_archive", true)]
    [InlineData("stall_terminal", false)] [InlineData("stall_terminal", true)]
    [InlineData("rejected_input", false)] [InlineData("rejected_input", true)]
    [InlineData("repair_restore", false)] [InlineData("repair_restore", true)]
    public async Task OriginalEngineSnapshotOwnersRetainGenuinePublicationUncertaintyOnClose(string mode, bool uncertain)
    {
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
        ResourceMaterializationTestContext context; GameEngine engine; object? snapshotContext = null;
        if (mode == "inactive_archive")
        {
            var fixture = await CreateInactiveSnapshotEvidenceFixtureAsync(hooks);
            context = fixture.Context; engine = fixture.Engine;
        }
        else
        {
            var fixture = await CreateSpiritualEntryGuardOriginalAsync(absentConflict: false, hooks: hooks);
            context = fixture.Context; engine = fixture.Engine; snapshotContext = fixture.Snapshot;
        }
        await using var disposedContext = context;
        var files = context.FileSystem; var root = context.RootPath;
        using var owned = new CleanupOwnedFixture(root, _directGachaOutput!.WriteLine);
        await using var disposedAudio = GetPrivateField<AudioService>(engine, "_audioService");
        cut.Attach(files);
        const string complete = "ready/turn_complete.json";
        const string ready = "game_state/control/validation_repair_ready.json";
        const string request = "game_state/control/validation_repair_request.json";
        const string stall = "game_state/control/gm_validation_repair_artifact_stall_report.json";
        const string restorePath = "game_state/world/original_repair_restore.json";
        const string addedPath = "game_state/world/new_repair_restore.json";
        object? rollback = null; byte[]? restoredBytes = null;
        Dictionary<string, byte[]>? realBackupBytes = null;
        if (mode == "stall_terminal")
        {
            var session = snapshotContext!.GetType().GetProperty("SessionId")!.GetValue(snapshotContext);
            var requestId = snapshotContext.GetType().GetProperty("RequestId")!.GetValue(snapshotContext);
            var turn = snapshotContext.GetType().GetProperty("TurnNumber")!.GetValue(snapshotContext);
            var identity = JsonSerializer.Serialize(new { sessionId = session, requestId, turnNumber = turn });
            await files.WriteFileAtomicAsync(request, identity);
            await files.WriteFileAtomicAsync(complete, identity);
            await files.WriteFileAtomicAsync(ready, identity);
            await files.WriteFileAtomicAsync(stall, "{\"retained\":\"actual original stall report\"}");
        }
        else if (mode == "repair_restore")
        {
            await files.WriteFileAtomicAsync(restorePath, "{\"retained\":\"real baseline\"}");
            restoredBytes = await files.ReadFileBytesAsync(restorePath);
            rollback = await InvokePrivateTaskResultAsync(engine, "CreatePreTurnBackup", "original-repair-owning-close");
            var backupMap = (System.Collections.IDictionary)rollback.GetType().GetProperty("BackupFiles")!.GetValue(rollback)!;
            realBackupBytes = backupMap.Values.Cast<string>().ToDictionary(path => files.ResolvePath(path), path => File.ReadAllBytes(files.ResolvePath(path)), StringComparer.Ordinal);
            await files.WriteFileAtomicAsync(restorePath, "{\"retained\":\"rejected state\"}");
            await files.WriteFileAtomicAsync(addedPath, "{\"retained\":\"new rejected state\"}");
        }
        Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
        cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual afterlife state original owner late close.");
        var closer = new EngineSnapshotThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null; var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var methodName = mode switch
        {
            "inactive_archive" => "ArchiveInactivePendingSnapshotEvidenceAsync",
            "stall_terminal" => "TryPromoteValidationRepairArtifactStallToTerminalErrorAsync",
            "rejected_input" => "CleanupCorrelatedRejectedTurnRequestAsync",
            _ => "RestorePreTurnBaselineForRepairSessionAsync"
        };
        var boundary = "GameEngine+<" + methodName + ">";
        using var generationDocument = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(generationBefore!));
        var generation = generationDocument.RootElement.GetProperty("GenerationId").GetString()!;
        object?[]? invokeArguments = mode switch
        {
            "inactive_archive" => null,
            "stall_terminal" => [snapshotContext, generation],
            "rejected_input" => [snapshotContext],
            _ => [rollback, generation, null]
        };
        Task? operation = null; Exception? failure = null; bool? boolResult = null;
        Dictionary<string, byte[]>? atIntentFiles = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = (Task)typeof(GameEngine).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, invokeArguments)!;
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            atIntentFiles = ReadCanonicalFiles();
            var inspector = typeof(OriginalOwnedLeaseCloseTests).GetMethod("InspectOriginalNestedOwningLease", BindingFlags.Static | BindingFlags.NonPublic)!;
            (original, actualOwnerState) = ((FileSystemManager.CanonicalWriteLease, string))inspector.Invoke(null, [operation, boundary])!;
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null && (mode is "inactive_archive" or "stall_terminal")) boolResult = await Assert.IsAssignableFrom<Task<bool>>(operation);
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
            attachments, closer.Calls, boolResult, failure = failure?.ToString(),
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, generationBefore, generationAfter, beforeFiles, atIntentFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        if (uncertain)
        {
            Assert.Null(boolResult); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.Equal(atIntentFiles!.Keys.Where(path => path != target).Order(StringComparer.Ordinal), afterFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal));
            foreach (var pair in atIntentFiles.Where(pair => pair.Key != target)) Assert.Equal(pair.Value, afterFiles[pair.Key]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode == "inactive_archive")
            {
                Assert.True(boolResult);
                AssertInactiveSnapshotEvidenceArchive(
                    beforeFiles.ToDictionary(pair => Path.GetRelativePath(files.GameSessionPath, pair.Key), pair => pair.Value, StringComparer.Ordinal),
                    afterFiles.ToDictionary(pair => Path.GetRelativePath(files.GameSessionPath, pair.Key), pair => pair.Value, StringComparer.Ordinal));
                Assert.DoesNotContain(afterFiles.Keys, path => IsInactiveSnapshotEvidenceSource(Path.GetRelativePath(files.GameSessionPath, path)));
            }
            else if (mode == "stall_terminal")
            {
                Assert.True(boolResult); Assert.False(files.FileExists(complete)); Assert.False(files.FileExists(ready));
                var error = (await context.ReadJsonAsync("ready/turn_error.json"))!;
                var matching = (await context.ReadJsonAsync(request))!;
                Assert.Equal(matching["sessionId"]!.GetValue<string>(), error["sessionId"]!.GetValue<string>());
                Assert.Equal(matching["requestId"]!.GetValue<string>(), error["requestId"]!.GetValue<string>());
                Assert.Equal(matching["turnNumber"]!.GetValue<int>(), error["turnNumber"]!.GetValue<int>());
                Assert.Equal("error", error["status"]!.GetValue<string>());
                Assert.True(JsonNode.DeepEquals(await context.ReadJsonAsync(stall), error["validationRepairArtifactStall"]));
            }
            else if (mode == "rejected_input")
            {
                var expected = new Dictionary<string, byte[]>(beforeFiles, StringComparer.Ordinal);
                Assert.True(expected.Remove(files.ResolvePath("input/turn_request.json")));
                Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
                foreach (var pair in expected) Assert.Equal(pair.Value, afterFiles[pair.Key]);
            }
            else
            {
                Assert.Equal(restoredBytes, File.ReadAllBytes(files.ResolvePath(restorePath))); Assert.False(files.FileExists(addedPath));
                foreach (var pair in realBackupBytes!) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
            }
            Assert.Empty(await context.Validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
        }
    }

    private sealed class EngineSnapshotThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; throw failure; }
    }
}
