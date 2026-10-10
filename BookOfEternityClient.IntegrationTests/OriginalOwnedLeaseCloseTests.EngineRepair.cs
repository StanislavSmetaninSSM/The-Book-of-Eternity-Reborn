using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("repair_files", false)] [InlineData("repair_files", true)]
    [InlineData("repair_ready", false)] [InlineData("repair_ready", true)]
    [InlineData("repair_ready_session", false)] [InlineData("repair_ready_session", true)]
    [InlineData("guardian_command", false)] [InlineData("guardian_command", true)]
    public async Task OriginalEngineRepairCleanupOwnersRetainGenuineUncertaintyOnClose(string mode, bool uncertain)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-original-engine-repair-close-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var launchArmed = false; var acquisitionPauses = 0; var publicationPauses = 0;
        string? target = null;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    if (launchArmed && phase == TrustedLocalPublicationPhase.IntentPublished && publicationPauses == 0)
                    {
                        using var actual = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        if (actual.RootElement.GetProperty("Members").EnumerateArray().Any(m => m.GetProperty("Path").GetString() == target))
                        { publicationPauses++; entered.TrySetResult(); allow.Task.GetAwaiter().GetResult(); }
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
        await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        var settings = new GameSettings { MusicEnabled = false, SoundEnabled = false, GmBridgeAutoStart = false, ImageProvider = "off" };
        const string ready = "game_state/control/validation_repair_ready.json";
        const string request = "game_state/control/validation_repair_request.json";
        const string stall = "game_state/control/gm_validation_repair_artifact_stall_report.json";
        const string guardians = "game_state/meta/guardians.json";
        var seeds = new[] { ready, request, stall, guardians };
        foreach (var path in seeds)
            await files.WriteFileAtomicAsync(path, path == guardians
                ? new JsonObject { ["guardians"] = new JsonArray(), [GuardianProjectState.QuestProgressUpdatesProperty] = new JsonArray(), ["retained"] = "original" }.ToJsonString()
                : "{\"retained\":\"" + path + "\"}");
        target = files.ResolvePath(mode == "guardian_command" ? guardians : ready); cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(Path.Combine(files.GameSessionPath, "game_state"), "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual engine repair original owner late close.");
        var closer = new ThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null;
        var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var method = mode switch
        {
            "repair_files" => "DeleteValidationRepairFilesAsync",
            "repair_ready" => "DeleteValidationRepairReadyCoreAsync",
            "repair_ready_session" => "DeleteValidationRepairReadyForSessionAsync",
            _ => "RemoveGuardianQuestProgressUpdatesCommandSurfaceAsync"
        };
        var boundary = "GameEngine+<" + method + ">";
        using var generationDocument = JsonDocument.Parse(LocalSettingsPreparation.DecodeText(generationBefore!));
        var generation = generationDocument.RootElement.GetProperty("GenerationId").GetString();
        Assert.False(string.IsNullOrWhiteSpace(generation));
        Task? operation = null; Exception? failure = null; string? actualOwnerState = null;
        var (engine, audio) = CreateOriginalCloseEngine(files, settings);
        try
        {
            launchArmed = true;
            var arguments = mode == "guardian_command" ? new object?[] { generation, null }
                : mode == "repair_ready_session" ? new object?[] { generation } : null;
            operation = (Task)typeof(GameEngine).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, arguments)!;
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            (original, actualOwnerState) = InspectOriginalNestedOwningLease(operation, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allow.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe;
            await audio.DisposeAsync();
        }
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var targetAfter = CleanupPublicationCut.ReadOptional(target);
        var generationAfter = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        var afterFiles = ReadCanonicalFiles();
        output.WriteLine(JsonSerializer.Serialize(new { mode, uncertain, root, boundary, actualOwnerState, acquisitionPauses, publicationPauses,
            attachments, closer.Calls, failure = failure?.ToString(), samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null,
            mainClosed = original.MainAdmission == null, contextClosed = original.ExternalPublicationContext == null,
            generationBefore, generationAfter, targetAfter, beforeFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        if (uncertain)
        {
            Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.Equal(beforeFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            foreach (var path in beforeFiles.Keys.Where(path => path != target)) Assert.Equal(beforeFiles[path], afterFiles[path]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            var changed = mode == "repair_files" ? new[] { ready, request, stall }.Select(files.ResolvePath).ToArray() : new[] { target };
            foreach (var path in beforeFiles.Keys.Where(path => !changed.Contains(path, StringComparer.Ordinal))) Assert.Equal(beforeFiles[path], afterFiles[path]);
            if (mode == "guardian_command")
            {
                Assert.NotNull(targetAfter); var persisted = JsonNode.Parse(LocalSettingsPreparation.DecodeText(targetAfter))!.AsObject();
                Assert.False(persisted.ContainsKey(GuardianProjectState.QuestProgressUpdatesProperty));
                Assert.Empty(persisted["guardians"]!.AsArray()); Assert.Equal("original", persisted["retained"]!.GetValue<string>());
                Assert.Equal(beforeFiles.Keys.Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            }
            else
            {
                foreach (var path in changed) Assert.False(File.Exists(path));
                Assert.Equal(beforeFiles.Keys.Where(path => !changed.Contains(path, StringComparer.Ordinal)).Order(StringComparer.Ordinal), afterFiles.Keys.Order(StringComparer.Ordinal));
            }
        }
    }
}
