using System.Diagnostics;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.Services.GmWorkers;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData(false, "success")]
    [InlineData(true, "success")]
    [InlineData(false, "close")]
    [InlineData(true, "close")]
    [InlineData(false, "evidence-close")]
    [InlineData(true, "evidence-close")]
    [InlineData(false, "uncertain-close")]
    [InlineData(true, "uncertain-close")]
    [InlineData(true, "generation-close")]
    public async Task OriginalEngineRollbackOwnersPreserveActualPrimaryAndExactColdState(bool forSession, string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-t067-engine-rollback-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var cut = new CleanupPublicationCut();
        var acquireEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowAcquire = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var readEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allowRead = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false; var acquirePauses = 0; var readPauses = 0; var faultReached = 0;
        var terminal = false; var laterReads = 0; var laterMutations = 0; var laterAcquisitions = 0;
        string? firstBackup = null, lastMutation = null; var finalPublication = false;
        FileSystemManager.CanonicalWriteLease? original = null;
        var primaryLeaf = new IOException("T067 actual opened rollback evidence read fault " + Guid.NewGuid().ToString("N"));
        var closeFailure = new IOException("T067 actual original rollback owner external-context close " + Guid.NewGuid().ToString("N"));
        var closer = new ThrowingClose(closeFailure); var attachments = 0;
        void AttachClose()
        {
            if (Interlocked.CompareExchange(ref attachments, 1, 0) != 0) return;
            Assert.NotNull(original); Assert.Null(original.ExternalPublicationContext);
            original.ExternalPublicationContext = closer;
        }
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    if (terminal) laterAcquisitions++;
                    if (armed && acquirePauses == 0)
                    { acquirePauses++; acquireEntered.TrySetResult(); await allowAcquire.Task; }
                    await cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                },
                AfterCanonicalReadInitialValidationAsync = async path =>
                {
                    if (terminal) laterReads++;
                    await cut.Hooks.AfterCanonicalReadInitialValidationAsync!(path);
                    if (!armed || path != firstBackup || readPauses != 0) return;
                    readPauses++; readEntered.TrySetResult(); await allowRead.Task;
                    if (mode == "evidence-close") { faultReached++; throw primaryLeaf; }
                },
                BeforeCanonicalMutationBoundaryAsync = async path =>
                {
                    if (terminal) laterMutations++;
                    await cut.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
                },
                LocalPublicationObserver = (phase, index) =>
                {
                    if (armed && mode == "close" && phase == TrustedLocalPublicationPhase.IntentPublished)
                    {
                        using var header = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
                        finalPublication = header.RootElement.GetProperty("Members").EnumerateArray()
                            .Any(member => member.GetProperty("Path").GetString() == lastMutation);
                    }
                    // CleanupComplete is after actual durable decision + scratch/journal cleanup.
                    if (armed && mode == "close" && phase == TrustedLocalPublicationPhase.CleanupComplete && finalPublication)
                    { AttachClose(); finalPublication = false; }
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                },
                LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync
            });
        cut.Attach(files); files.EnsureDirectoryStructure();
        const string present = "game_state/world/weather.json", second = "lore/current_world/world_setting.json";
        const string absent = "game_state/wounds/wound_history.json";
        byte[] presentBefore = [0xEF, 0xBB, 0xBF, 0x7B, 0x7D], secondBefore = "{\"worldName\":\"T067 до\"}"u8.ToArray();
        await files.WriteFileAtomicBytesAsync(present, presentBefore);
        await files.WriteFileAtomicBytesAsync(second, secondBefore);
        await using (var seed = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seed);
        var (engine, audio) = CreateOriginalCloseEngine(files, new GameSettings
        { MusicEnabled = false, SoundEnabled = false, GmBridgeAutoStart = false, ImageProvider = "off" });
        using var audioOwner = new AsyncAudioOwner(audio);
        var manager = (StateManager)typeof(GameEngine).GetField("_stateManager", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(engine)!;
        var runtimeBefore = manager.CurrentState;
        string? generation = null;
        var snapshot = await SessionOperationContext.RunParticipatingCurrentSessionAsync(files, async () =>
        {
            await using (var lease = await files.AcquireCanonicalWriteLeaseAsync()) generation = files.ReadExistingSessionGeneration(lease);
            var task = (Task)typeof(GameEngine).GetMethod("CreatePreTurnBackup", BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null, types: [typeof(string)], modifiers: null)!.Invoke(engine, ["t067_owned_close"])!;
            await task; return task.GetType().GetProperty("Result")!.GetValue(task)!;
        });
        Assert.False(string.IsNullOrWhiteSpace(generation));
        var backups = Assert.IsAssignableFrom<Dictionary<string, string>>(snapshot.GetType().GetProperty("BackupFiles")!.GetValue(snapshot));
        Assert.NotEmpty(backups); Assert.Contains(present, backups.Keys); Assert.Contains(second, backups.Keys); Assert.DoesNotContain(absent, backups.Keys);
        firstBackup = backups.First().Value;
        lastMutation = files.ResolvePath(forSession ? backups.Last().Value : backups.Last().Key);
        var backupImages = backups.Values.ToDictionary(path => path, path => File.ReadAllBytes(files.ResolvePath(path)));
        await files.WriteFileAtomicBytesAsync(present, [0x42]); await files.WriteFileAtomicBytesAsync(second, [0x52]);
        await files.WriteFileAtomicBytesAsync(absent, [0x61]);
        var before = EngineRollbackColdProbe.Images(files);
        var generationBefore = File.ReadAllBytes(files.SessionGenerationPath);
        cut.Select = (path, _) => path == files.ResolvePath(absent);
        var expectedGeneration = mode == "generation-close" ? Guid.NewGuid().ToString("N") : generation!;
        var method = forSession ? "RestorePreTurnBackupForSessionAsync" : "RestorePreTurnBackup";
        var boundary = "GameEngine+<" + method + ">";
        Task? operation = null; Exception? primary = null, observationFailure = null; string? ownerState = null;
        BrowserOriginalMainCondition? condition = null;
        var invocationMarker = new AsyncLocal<object?>(); var marker = new object();
        void CaptureOwner()
        {
            (original, ownerState) = InspectOriginalNestedOwningLease(operation!, boundary);
            Assert.Same(files, original.Owner); Assert.True(original.IsActive);
            Assert.NotNull(original.MainAdmission); Assert.True(original.MainAdmission.Acquired); Assert.True(original.MainAdmission.PhysicalScope);
            Assert.Same(files.CanonicalRootAuthorityIdentity, original.MainAdmission.RootIdentity);
            condition = original.MainAdmission.CaptureBrowserCondition(generation!);
            Assert.Equal("quiescentAbsent", condition.Kind); Assert.Null(condition.ActiveIdentity);
            Assert.Equal(generation, condition.Generation); Assert.Equal(files.BasePath, condition.RootKey);
        }
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (!ReferenceEquals(invocationMarker.Value, marker)) return;
            var error = args.Exception;
            var isPrimary = mode == "uncertain-close" && ReferenceEquals(error, cut.OriginalUncertainty) ||
                mode == "evidence-close" && error is InvalidDataException &&
                error.Message == "Pre-turn rollback evidence could not be validated. Rollback evidence was retained." && ContainsRollbackFailure(error, primaryLeaf) ||
                mode == "generation-close" && error is GmWorkerSessionReplacedException &&
                error.Message == "The validation-repair cycle belongs to a game session that is no longer current.";
            if (isPrimary && primary == null)
            {
                primary = error; terminal = true;
                try { if (original == null) CaptureOwner(); AttachClose(); }
                catch (Exception observerError) { observationFailure = observerError; }
            }
            if (ReferenceEquals(error, closeFailure)) terminal = true;
        };
        Exception? failure;
        AppDomain.CurrentDomain.FirstChanceException += observe;
        try
        {
            armed = true;
            var previousMarker = invocationMarker.Value;
            try
            {
                invocationMarker.Value = marker;
                operation = (Task)typeof(GameEngine).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!
                    .Invoke(engine, forSession ? [snapshot, expectedGeneration] : [snapshot])!;
            }
            finally { invocationMarker.Value = previousMarker; }
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            if (mode != "generation-close")
            {
                Assert.Same(readEntered.Task, await Task.WhenAny(readEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
                CaptureOwner(); cut.Armed = mode == "uncertain-close"; allowRead.TrySetResult();
            }
            failure = await Record.ExceptionAsync(() => operation);
        }
        finally
        {
            allowAcquire.TrySetResult(); allowRead.TrySetResult();
            if (operation != null && !operation.IsCompleted) await Record.ExceptionAsync(() => operation);
            AppDomain.CurrentDomain.FirstChanceException -= observe; armed = false;
        }
        var after = EngineRollbackColdProbe.Images(files);
        bool lockAvailable;
        using (var probe = new FileStream(files.CanonicalWriteLockPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) lockAvailable = true;
        var cold = await RunRollbackColdReadAsync(files, Path.Combine(root, "cold-result.json"));
        output.WriteLine(JsonSerializer.Serialize(new
        {
            Scope = "real private GameEngine rollback component #26/#27; not whole player turn or active GM",
            forSession, mode, root, boundary, ownerState, condition, generation, expectedGeneration,
            acquirePauses, readPauses, faultReached, attachments, closer.Calls,
            primary = primary?.ToString(), failure = failure?.ToString(), observationFailure = observationFailure?.ToString(),
            SamePrimary = ReferenceEquals(primary, failure), SameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure),
            laterReads, laterMutations, laterAcquisitions, lockAvailable, before, after, backupImages,
            RuntimeUnchanged = ReferenceEquals(runtimeBefore, manager.CurrentState), Cut = cut.Evidence(), Cold = cold
        }));
        // Evidence and fresh-process checks precede the potentially causal SAME-primary assertion.
        AssertImagesEqual(after, JsonSerializer.Deserialize<Dictionary<string, byte[]>>(cold.GetProperty("Before"))!);
        AssertImagesEqual(after, JsonSerializer.Deserialize<Dictionary<string, byte[]>>(cold.GetProperty("After"))!);
        Assert.NotEqual(Environment.ProcessId, cold.GetProperty("ProcessId").GetInt32());
        Assert.Equal(mode != "uncertain-close", cold.GetProperty("acquired").GetBoolean());
        if (mode == "uncertain-close")
        {
            Assert.Equal(typeof(InvalidDataException).FullName, cold.GetProperty("FailureType").GetString());
            Assert.False(string.IsNullOrWhiteSpace(cold.GetProperty("Failure").GetString()));
            Assert.Empty(cold.GetProperty("reads").EnumerateObject());
        }
        else
        {
            Assert.Null(cold.GetProperty("Failure").GetString()); Assert.Equal(generation, cold.GetProperty("generation").GetString());
            var coldReads = JsonSerializer.Deserialize<Dictionary<string, byte[]>>(cold.GetProperty("reads"))!;
            foreach (var path in Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories))
                Assert.Equal(File.ReadAllBytes(path), coldReads[Path.GetRelativePath(files.GameSessionPath, path)]);
        }
        Assert.Null(observationFailure); Assert.NotNull(original); Assert.NotNull(condition);
        Assert.Equal(1, acquirePauses); Assert.False(original.IsActive); Assert.Null(original.MainAdmission);
        Assert.Null(original.AmbientRegistration); Assert.Null(original.ExternalPublicationContext); Assert.True(lockAvailable);
        Assert.Equal(generationBefore, File.ReadAllBytes(files.SessionGenerationPath));
        if (mode is "success" or "close")
        {
            Assert.Equal(presentBefore, File.ReadAllBytes(files.ResolvePath(present))); Assert.Equal(secondBefore, File.ReadAllBytes(files.ResolvePath(second)));
            Assert.False(File.Exists(files.ResolvePath(absent)));
            foreach (var (path, bytes) in backupImages)
                if (forSession) Assert.False(File.Exists(files.ResolvePath(path))); else Assert.Equal(bytes, File.ReadAllBytes(files.ResolvePath(path)));
        }
        else if (mode != "uncertain-close") AssertImagesEqual(before, after);
        if (mode == "success") { Assert.Null(failure); Assert.Equal(0, closer.Calls); Assert.NotSame(runtimeBefore, manager.CurrentState); }
        else
        {
            Assert.Same(runtimeBefore, manager.CurrentState); Assert.Equal(0, laterReads); Assert.Equal(0, laterMutations); Assert.Equal(0, laterAcquisitions);
            Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls);
            if (mode == "close") Assert.Same(closeFailure, failure);
            else
            {
                Assert.NotNull(primary); Assert.Same(primary, failure); Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
                Assert.Contains(mode == "generation-close" ? "ThrowIfRepairSessionReplaced" : "RestorePreTurnBackupAsync", primary.StackTrace!);
                if (mode == "evidence-close") { Assert.Equal(1, faultReached); Assert.True(ContainsRollbackFailure(primary, primaryLeaf)); }
                if (mode == "uncertain-close") { cut.AssertReachedAndStopped(); foreach (var (path, bytes) in backupImages) Assert.Equal(bytes, File.ReadAllBytes(files.ResolvePath(path))); }
            }
        }
    }

    private static bool ContainsRollbackFailure(Exception error, Exception wanted) => ReferenceEquals(error, wanted) ||
        error is AggregateException aggregate && aggregate.InnerExceptions.Any(item => ContainsRollbackFailure(item, wanted)) ||
        error.InnerException is { } inner && ContainsRollbackFailure(inner, wanted);

    private static void AssertImagesEqual(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual)
    {
        Assert.Equal(expected.Keys.Order(StringComparer.Ordinal), actual.Keys.Order(StringComparer.Ordinal));
        foreach (var path in expected.Keys) Assert.Equal(expected[path], actual[path]);
    }

    private async Task<JsonElement> RunRollbackColdReadAsync(FileSystemManager files, string report)
    {
        var assembly = typeof(OriginalOwnedLeaseCloseTests).Assembly.Location;
        var info = new ProcessStartInfo(Environment.GetEnvironmentVariable("DOTNET_HOST_PATH") ?? "dotnet")
        { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, CreateNoWindow = true };
        foreach (var arg in new[] { "exec", "--runtimeconfig", Path.ChangeExtension(assembly, ".runtimeconfig.json"),
                     "--depsfile", Path.ChangeExtension(assembly, ".deps.json"), typeof(EngineRollbackColdProbe).Assembly.Location,
                     "engine-rollback-cold-read", files.BasePath, report }) info.ArgumentList.Add(arg);
        using var process = Process.Start(info) ?? throw new InvalidOperationException("Owned T067 cold process did not start.");
        var stdout = process.StandardOutput.ReadToEndAsync(); var stderr = process.StandardError.ReadToEndAsync();
        try { await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20)); }
        finally { if (!process.HasExited) { process.Kill(entireProcessTree: true); await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); } }
        var text = await stdout; var error = await stderr;
        output.WriteLine(JsonSerializer.Serialize(new { ColdProcess = process.Id, process.ExitCode, IoDrained = true, Stdout = text, Stderr = error }));
        Assert.Equal(0, process.ExitCode); Assert.True(string.IsNullOrWhiteSpace(error), error);
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(report)); return doc.RootElement.Clone();
    }

    private sealed class AsyncAudioOwner(AudioService audio) : IDisposable
    {
        public void Dispose() => audio.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
