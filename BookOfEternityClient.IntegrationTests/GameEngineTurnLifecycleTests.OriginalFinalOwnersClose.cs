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
    [InlineData(false)] [InlineData(true)]
    public Task OriginalEngineAscensionOwnerRetainsGenuineUncertaintyOnClose(bool uncertain) => RunOriginalFinalOwnerCloseAsync("ascension", uncertain);

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public Task OriginalEngineFinalizedReplayOwnerRetainsGenuineUncertaintyOnClose(bool uncertain) => RunOriginalFinalOwnerCloseAsync("finalized_replay", uncertain);

    private async Task RunOriginalFinalOwnerCloseAsync(string mode, bool uncertain)
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
        HeldTreatmentPipelineContext? context = mode == "finalized_replay"
            ? await CreateHeldTreatmentPipelineContextAsync(fault: null, hooks: hooks) : null;
        await using var disposedContext = context;
        var files = context?.FileSystem ?? new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        var root = context?.Root ?? _rootPath;
        using var owned = context == null ? null : new CleanupOwnedFixture(root, _directGachaOutput!.WriteLine);
        GameEngine engine; object? snapshotContext = null;
        if (context != null)
        {
            await context.ReleaseLeaseAsync();
            var fixture = await CreateHeldTreatmentValidationEngineAsync(context);
            engine = fixture.Engine; snapshotContext = fixture.SnapshotContext;
        }
        else engine = CreateGameEngine(new QueuedConsoleInputSource([]), settings =>
        { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; settings.ImageProvider = "off"; }, fileSystem: files);
        await using var disposedAudio = GetPrivateField<AudioService>(engine, "_audioService");
        cut.Attach(files);
        if (context != null)
        {
            var originalCommandBytes = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            var originalPendingBytes = await files.ReadFileBytesAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            var refresh = await InvokePrivateTaskResultAsync(engine, "RefreshAcceptedTurnCanonicalStateForValidationAsync", HeldTreatmentPipelineContext.Turn, snapshotContext);
            var returnedTransaction = refresh.GetType().GetProperty("TreatmentResourcePublicationTransaction", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(refresh);
            await using var disposedTransaction = returnedTransaction as IAsyncDisposable;
            Assert.True((bool)refresh.GetType().GetProperty("BaselineUsable", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(refresh)!);
            var issues = (IEnumerable<ValidationIssue>)refresh.GetType().GetProperty("PostSealIssues", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!.GetValue(refresh)!;
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
            var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(returnedTransaction);
            var probe = await transaction.ProbeAsync(files); Assert.True(probe.IsValid); Assert.Empty(probe.Issues);
            var finalized = await transaction.CompleteAsync(files);
            Assert.True(finalized.IsValid); Assert.Empty(finalized.Issues);
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, finalized.Outcome);
            // A durable retry restores the exact real submitted bytes after the real receipt has finalized.
            await files.WriteFileAtomicBytesAsync(AcceptedMechanicsPlan.WoundCommandPath, originalCommandBytes);
            if (originalPendingBytes != null) await files.WriteFileAtomicBytesAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath, originalPendingBytes);
            using var command = JsonDocument.Parse(LocalSettingsPreparation.DecodeText((await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath))!));
            using var pending = originalPendingBytes == null ? null : JsonDocument.Parse(LocalSettingsPreparation.DecodeText((await files.ReadFileBytesAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath))!));
            var history = WoundHistoryState.Parse(await files.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            Assert.True(history.IsValid);
            var catalog = MortalWoundTreatmentPersistedRequestCatalog.Parse(command.RootElement, pending?.RootElement, history);
            Assert.True(catalog.IsValid); Assert.Empty(catalog.HeldRequests);
            var actualRequest = Assert.Single(catalog.FinalizedRequests);
            Assert.Equal(context.Request.RequestFingerprint, actualRequest.RequestFingerprint);
            var replay = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(actualRequest, history, before: null, acceptedState: null);
            Assert.Equal("ExactReplay", replay.Disposition); Assert.NotNull(replay.ReplayReceipt);
            var narrative = JsonNode.Parse(LocalSettingsPreparation.DecodeText((await files.ReadFileBytesAsync("output/narrative_response.json"))!))!;
            Assert.Equal(HeldTreatmentPipelineContext.FinalSceneText, narrative["response"]!.GetValue<string>());
            Assert.True(JsonNode.DeepEquals(command.RootElement.GetProperty("commands")[0].GetProperty("result").Deserialize<JsonObject>(),
                WoundResponseInputComposer.ComposeMortalWoundTreatmentPersistedResult(actualRequest, replay.ReplayReceipt!)));
        }
        else
        {
            files.EnsureDirectoryStructure();
            var soul = CreateLifecycleSoulState("Original ascension storage owner", "Chaos Sea");
            soul["currentIncarnation"] = 4;
            soul["soulProgression"] = new JsonObject { ["totalExperience"] = AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience };
            await SeedMortalLifeTransitionAuthorityAsync(soul);
            files.DeleteFile("game_state/control/life_transitions.json");
            files.DeleteFile("game_state/control/incarnation_trigger.json");
            await files.WriteFileAtomicAsync("game_state/control/ascension.json", "{\"AscensionTrigger\":true,\"playerChoice\":\"Ascension\"}");
            var state = GetPrivateField<StateManager>(engine, "_stateManager");
            await state.RefreshGameStateAsync(); Assert.Equal("Chaos Sea", state.CurrentState.CurrentRealm);
            Assert.True(await InvokePrivateAsync<bool>(engine, "HasMaximumEnlightenmentAsync"));
            await using (var seedLease = await files.AcquireCanonicalWriteLeaseAsync()) files.GetOrCreateSessionGeneration(seedLease);
        }
        cut.Select = (path, _) => path == target;
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        Dictionary<string, byte[]> ReadCanonicalFiles() => Directory.EnumerateFiles(files.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);
        var beforeFiles = ReadCanonicalFiles();
        var closeFailure = new IOException("Actual Engine ascension/finalized replay owner late close.");
        var closer = new EngineFinalThrowingClose(closeFailure); FileSystemManager.CanonicalWriteLease? original = null; var attachments = 0;
        EventHandler<FirstChanceExceptionEventArgs> observe = (_, args) =>
        {
            if (ReferenceEquals(args.Exception, cut.OriginalUncertainty) && original != null && Interlocked.CompareExchange(ref attachments, 1, 0) == 0)
                original.ExternalPublicationContext = closer;
        };
        var methodName = mode == "ascension" ? "CheckAscensionTrigger" : "TryQuarantineFinalizedTreatmentReplayAsync";
        var boundary = "GameEngine+<" + methodName + ">";
        object?[]? invokeArguments = mode == "ascension" ? null : [HeldTreatmentPipelineContext.Turn];
        Task? operation = null; Exception? failure = null; AcceptedTurnValidationDisposition? replayDisposition = null;
        Dictionary<string, byte[]>? atIntentFiles = null; string? actualOwnerState = null;
        try
        {
            launchArmed = true;
            operation = (Task)typeof(GameEngine).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(engine, invokeArguments)!;
            Assert.Same(acquireEntered.Task, await Task.WhenAny(acquireEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
            allowAcquire.TrySetResult();
            var publicationOrCompletion = await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12));
            if (ReferenceEquals(publicationOrCompletion, operation))
                _directGachaOutput!.WriteLine(JsonSerializer.Serialize(new { mode, PreparationFailure = (await Record.ExceptionAsync(() => operation))?.ToString(), EarlyCompletion = operation.IsCompletedSuccessfully }));
            Assert.Same(entered.Task, publicationOrCompletion);
            atIntentFiles = ReadCanonicalFiles();
            var inspector = typeof(OriginalOwnedLeaseCloseTests).GetMethod("InspectOriginalNestedOwningLease", BindingFlags.Static | BindingFlags.NonPublic)!;
            (original, actualOwnerState) = ((FileSystemManager.CanonicalWriteLease, string))inspector.Invoke(null, [operation, boundary])!;
            Assert.Same(files, original.Owner); Assert.True(original.IsActive); Assert.Null(original.ExternalPublicationContext);
            AppDomain.CurrentDomain.FirstChanceException += observe; cut.Armed = uncertain; allow.TrySetResult();
            failure = await Record.ExceptionAsync(() => operation);
            if (failure == null && mode == "finalized_replay") replayDisposition = await Assert.IsAssignableFrom<Task<AcceptedTurnValidationDisposition?>>(operation);
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
            attachments, closer.Calls, replayDisposition, failure = failure?.ToString(),
            samePrimary = cut.OriginalUncertainty != null && ReferenceEquals(failure, cut.OriginalUncertainty),
            sameSecondary = ReferenceEquals(failure?.Data["CoordinatedLeaseReleaseFailure"], closeFailure), lockAvailable,
            activeAfter = original!.IsActive, ambientClosed = original.AmbientRegistration == null, mainClosed = original.MainAdmission == null,
            contextClosed = original.ExternalPublicationContext == null, generationBefore, generationAfter, beforeFiles, atIntentFiles, afterFiles, Cut = cut.Evidence() }));
        Assert.Equal(1, acquisitionPauses); Assert.Equal(1, publicationPauses); Assert.True(lockAvailable);
        Assert.False(original.IsActive); Assert.Null(original.AmbientRegistration); Assert.Null(original.MainAdmission); Assert.Null(original.ExternalPublicationContext);
        Assert.Equal(generationBefore, generationAfter);
        if (uncertain)
        {
            Assert.Null(replayDisposition); Assert.Equal(1, attachments); Assert.Equal(1, closer.Calls); Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]); cut.AssertReachedAndStopped(); Assert.Equal(0, cut.ClosingLeases);
            Assert.Equal(atIntentFiles!.Keys.Where(path => path != target).Order(StringComparer.Ordinal), afterFiles.Keys.Where(path => path != target).Order(StringComparer.Ordinal));
            foreach (var pair in atIntentFiles.Where(pair => pair.Key != target)) Assert.Equal(pair.Value, afterFiles[pair.Key]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, attachments); Assert.Equal(0, closer.Calls); Assert.False(File.Exists(cut.JournalPath));
            if (mode == "ascension")
            {
                var soul = JsonNode.Parse(LocalSettingsPreparation.DecodeText(afterFiles[files.ResolvePath("game_state/meta/soul_state.json")]))!;
                Assert.Equal("Shining Abode", soul["currentRealm"]!.GetValue<string>());
                Assert.False(files.FileExists("game_state/control/ascension.json"));
                Assert.Contains("[ASCENSION]", await files.ReadFileAsync("stories/shining_abode.jsonl"), StringComparison.Ordinal);
                Assert.True(files.FileExists("lore/shining_abode/realm_lore.json"));
                Assert.True(GetPrivateField<StateManager>(engine, "_stateManager").CurrentState.IsInShiningAbode);
                Assert.NotEqual(beforeFiles[files.ResolvePath(ShiningAbodeState.StatePath)], afterFiles[files.ResolvePath(ShiningAbodeState.StatePath)]);
                var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
                Assert.Empty(await validator.ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
            }
            else
            {
                Assert.Null(replayDisposition); Assert.False(files.FileExists(AcceptedMechanicsPlan.WoundCommandPath));
                var allowed = new[] { files.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath), files.ResolvePath(WoundAcceptedTurnSnapshotContract.PendingResolutionPath) };
                Assert.Equal(beforeFiles.Keys.Except(allowed).Order(StringComparer.Ordinal), afterFiles.Keys.Except(allowed).Order(StringComparer.Ordinal));
                foreach (var pair in beforeFiles.Where(pair => !allowed.Contains(pair.Key, StringComparer.Ordinal))) Assert.Equal(pair.Value, afterFiles[pair.Key]);
                var pendingAfterBytes = await files.ReadFileBytesAsync(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
                var pendingAfter = pendingAfterBytes == null ? null : JsonNode.Parse(LocalSettingsPreparation.DecodeText(pendingAfterBytes))!.AsObject();
                Assert.False(MortalWoundTreatmentDurableSurfaceQuarantine.ContainsExactRequestRows(new JsonObject(), pendingAfter,
                    context!.Request.Coordinates.OperationKey, context.Request.Coordinates.AttemptId, context.Request.RequestFingerprint));
                Assert.Empty(await new ValidationService(files, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnCanonicalResourceMaterializationAsync());
            }
        }
    }

    private sealed class EngineFinalThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        public void Dispose() { Calls++; throw failure; }
    }
}
