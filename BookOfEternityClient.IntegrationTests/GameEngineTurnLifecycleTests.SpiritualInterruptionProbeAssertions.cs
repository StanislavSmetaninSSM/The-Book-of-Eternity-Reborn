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
    /// <summary>
    /// Retains the first forbidden repair or callback error before any test-only Escape cleanup can change the cut.
    /// </summary>
    private sealed class SpiritualInterruptionObservation
    {
        /// <summary>
        /// Gets the first failure observed on the public request surface.
        /// </summary>
        internal TaskCompletionSource<string> Failure { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Gets or sets the most recent physical repair packet for diagnostics.
        /// </summary>
        internal string? LastRepair { get; set; }

        /// <summary>
        /// Gets or sets the number of genuine warm file decisions submitted.
        /// </summary>
        internal int Responses { get; set; }
    }

    /// <summary>
    /// Responds once during the warm run, or observes cold recovery without supplying a GM decision or repair.
    /// </summary>
    /// <param name="context">
    /// Genuine physical session used only for shared reads and authorized warm file responses.
    /// </param>
    /// <param name="request">
    /// Signed original identity to require on the actual continuation.
    /// </param>
    /// <param name="observation">
    /// Evidence and early failure signal retained until the lifecycle task has drained.
    /// </param>
    /// <param name="respond">
    /// Allows exactly one materialize response when <see langword="true"/>; every cold repair packet is forbidden when <see langword="false"/>.
    /// </param>
    /// <param name="cancellationToken">
    /// Stops polling after the bounded lifecycle phase ends.
    /// </param>
    /// <returns>
    /// A task completing when observation stops or the first forbidden request is recorded.
    /// </returns>
    private static async Task ObserveSpiritualInterruptionRequestAsync(ResourceMaterializationTestContext context,
        TurnRequest request, SpiritualInterruptionObservation observation, bool respond, CancellationToken cancellationToken)
    {
        string? handled = null;
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                string? raw = null;
                try
                {
                    raw = await ReadSequentialSpiritualPhysicalDiagnosticAsync(context.FileSystem.ResolvePath(
                        "game_state/control/validation_repair_request.json"), tail: false, cancellationToken);
                }
                catch (FileNotFoundException) { }
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    observation.LastRepair = raw;
                    if (!respond)
                        throw new InvalidOperationException("Cold recovery requested a new decision or ordinary GM repair: " + raw);
                    using var document = JsonDocument.Parse(raw);
                    var repair = document.RootElement;
                    Assert.True(repair.TryGetProperty("spiritualWoundContinuation", out var envelope), raw);
                    var continuation = SpiritualWoundContinuationProtocol.ReadRequest(envelope);
                    if (handled != continuation.ContinuationId)
                    {
                        Assert.Null(handled);
                        handled = continuation.ContinuationId;
                        Assert.Equal("decision", continuation.Phase);
                        Assert.Empty(SpiritualWoundContinuationProtocol.ValidateRequest(continuation));
                        Assert.Empty(continuation.DependentDraftFields);
                        Assert.Equal(request.SessionId, repair.GetProperty("sessionId").GetString());
                        Assert.Equal(request.RequestId, repair.GetProperty("requestId").GetString());
                        Assert.Equal(42, repair.GetProperty("turnNumber").GetInt32());
                        Assert.Equal(0, repair.GetProperty("errors").GetArrayLength());
                        Assert.False(repair.GetProperty("fullTurnResubmissionRequired").GetBoolean());
                        // This actor represents an external file GM, not a competing client operation.
                        Assert.True(File.Exists(context.FileSystem.ResolvePath(SpiritualWoundCaptureCheckpointState.StatePath)));
                        Assert.True(File.Exists(context.FileSystem.ResolvePath(SpiritualWoundDecisionPendingState.StatePath)));
                        Assert.False(File.Exists(context.FileSystem.ResolvePath(SpiritualWoundOpportunityReceiptState.StatePath)));
                        await WriteSpiritualFileGmJsonAsync(context.FileSystem, "output/narrative_response.json", JsonSerializer.Serialize(new
                        {
                            response = "Чужое давление надломило волю хранителя.", timestamp = DateTime.UtcNow.ToString("O")
                        }));
                        var decision = AfterlifeResourceCutoverTests.CreateSpiritualLifecycleMaterializeDecision(continuation.Offer!.OpportunityRef);
                        await WriteSpiritualFileGmJsonAsync(context.FileSystem, "game_state/control/validation_repair_ready.json", JsonSerializer.Serialize(new
                        {
                            sessionId = request.SessionId, requestId = request.RequestId, turnNumber = 42,
                            timestamp = DateTime.UtcNow.ToString("O"), status = "success",
                            spiritualWoundContinuation = new
                            {
                                schemaVersion = 1, continuationId = continuation.ContinuationId, woundDecisions = new[] { decision }
                            }
                        }));
                        observation.Responses++;
                    }
                }
                await Task.Delay(25, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception error) { observation.Failure.TrySetResult(error.ToString()); }
    }

    /// <summary>
    /// Bounds one caller phase while preserving any forbidden repair observed before test cleanup.
    /// </summary>
    /// <param name="operation">
    /// Actual warm or cold lifecycle task, already started.
    /// </param>
    /// <param name="observer">
    /// Physical request observer to stop before draining an incomplete caller.
    /// </param>
    /// <param name="observation">
    /// First callback failure, which cannot become a passing rollback after Escape.
    /// </param>
    /// <param name="stop">
    /// Cancellation source for the observer.
    /// </param>
    /// <param name="input">
    /// Test input accepting Escape only when an unsuccessful phase remains incomplete.
    /// </param>
    /// <param name="timeout">
    /// Maximum duration before the phase is already considered unsuccessful.
    /// </param>
    /// <returns>
    /// Actual result, primary exception and separate cleanup failure without replacing primary evidence.
    /// </returns>
    private static async Task<(object? Result, Exception? Error, Exception? Cleanup)> AwaitSpiritualInterruptionPhaseAsync(
        Task<object> operation, Task observer, SpiritualInterruptionObservation observation, CancellationTokenSource stop,
        QueuedConsoleInputSource input, TimeSpan timeout)
    {
        object? result = null;
        Exception? failure = null;
        var cleanup = new List<Exception>();
        try
        {
            var first = await Task.WhenAny(operation, observation.Failure.Task).WaitAsync(timeout);
            if (first == observation.Failure.Task)
                throw new InvalidOperationException(await observation.Failure.Task);
            result = await operation;
        }
        catch (Exception error) { failure = error; }
        finally
        {
            try { stop.Cancel(); }
            catch (Exception error) { cleanup.Add(error); }
            if (!operation.IsCompleted) input.Enqueue(Key(ConsoleKey.Escape));
            try { await observer; }
            catch (Exception error) { cleanup.Add(error); }
            try { await operation; }
            catch (Exception error)
            {
                if (!ReferenceEquals(error, failure)) cleanup.Add(error);
            }
            Assert.True(operation.IsCompleted && observer.IsCompleted);
        }
        if (observation.Failure.Task.IsCompletedSuccessfully)
            failure ??= new InvalidOperationException(observation.Failure.Task.Result);
        return (result, failure, cleanup.Count == 0 ? null : new AggregateException(cleanup));
    }

    /// <summary>
    /// Runs actual startup normalization and late terminal processing on a newly constructed engine.
    /// </summary>
    /// <param name="engine">
    /// Fresh engine whose current turn was derived from persisted story before this call.
    /// </param>
    /// <returns>
    /// The real late-caller return value, which is not itself an acceptance discriminator.
    /// </returns>
    private static async Task<object> RunSpiritualInterruptionColdStartupAsync(GameEngine engine)
    {
        await InvokePrivateTaskAsync(engine, "NormalizeRuntimeUiArtifactsAsync");
        await InvokePrivateTaskAsync(engine, "NormalizePendingRepairArtifactsAsync");
        return await InvokePrivateTaskResultAsync(engine, "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync");
    }

    /// <summary>
    /// Captures exactly the production rollback inventory using its real selector under a canonical lease.
    /// </summary>
    /// <param name="engine">
    /// Engine owning the ordinary rollback inventory selector.
    /// </param>
    /// <param name="fileSystem">
    /// Same physical session as the engine; no authority fields are changed.
    /// </param>
    /// <returns>
    /// Exact canonical before or after images, including scheduler and player-output files selected by production.
    /// </returns>
    private static async Task<Dictionary<string, byte[]>> ReadSpiritualInterruptionTrackedImagesAsync(GameEngine engine,
        FileSystemManager fileSystem)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
        var method = typeof(GameEngine).GetMethod("EnumerateRollbackTrackedFiles", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var paths = Assert.IsAssignableFrom<IEnumerable<string>>(method.Invoke(engine, [lease]));
        var images = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        foreach (var path in paths)
            images.Add(path.Replace('\\', '/'), Assert.IsType<byte[]>(await fileSystem.ReadFileBytesAsync(lease, path)));
        return images;
    }

    /// <summary>
    /// Requires genuine common settlement before attempting cold recovery without resetting process-wide registries.
    /// </summary>
    /// <param name="fileSystem">
    /// Published physical root whose current registry must contain no competing live owner.
    /// </param>
    /// <returns>
    /// A task completing after actual common, item, treatment and private-publication claims are absent.
    /// </returns>
    private static async Task AssertSpiritualInterruptionOwnersSettledAsync(FileSystemManager fileSystem)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        await using var lease = await fileSystem.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fileSystem, lease));
        Assert.False(AcceptedTurnAuthorityRegistry.TryPeekSpiritualPublication(fileSystem, lease, out _));
        Assert.False(MortalItemAcceptedTurnAuthority.HasValidatedItems(fileSystem, lease));
        Assert.True(AcceptedTurnAuthorityRegistry.CanBeginSpiritualOriginalIntake(fileSystem, lease,
            new MortalItemIdentityFactory(), checkPlansAndItems: true));
    }

    /// <summary>
    /// Proves the cut follows real wound, effect, receipt and resource publication rather than only a staged decision.
    /// </summary>
    /// <param name="context">
    /// Genuine completed single-exchange physical session.
    /// </param>
    /// <returns>
    /// A task completing after one correlated wound/effect creation and two resource spends are verified.
    /// </returns>
    private static async Task AssertSpiritualInterruptionPublicationAsync(ResourceMaterializationTestContext context)
    {
        var receipt = Assert.IsType<JsonObject>(await context.ReadJsonAsync(SpiritualWoundOpportunityReceiptState.StatePath));
        var parsed = SpiritualWoundOpportunityReceiptState.Parse(receipt.ToJsonString(), SpiritualWoundOpportunityReceiptState.StatePath);
        Assert.True(parsed.IsValid, string.Join("\n", parsed.Issues));
        Assert.Single(receipt["instances"]!.AsArray());
        Assert.Single(receipt["sources"]!.AsArray());
        var decision = Assert.Single(receipt["decisions"]!.AsArray())!;
        Assert.Equal("materialize", decision["decision"]!.GetValue<string>());
        await AssertSpiritualLifecycleMaterializedWoundAsync(context, decision);
        var profiles = Assert.IsType<JsonObject>(await context.ReadJsonAsync(AfterlifeEntityProfileState.StatePath));
        var carriers = WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(null, null, null, null, profiles));
        Assert.Empty(carriers.Issues);
        Assert.True(carriers.TryResolveOne(decision["woundId"]!.GetValue<string>(), out var wound));
        var root = Assert.Single(wound.Wound.Consequences.OwnedEffectSources.RootBindings);
        var effects = EffectIdentityState.Parse(JsonSerializer.SerializeToElement(await context.ReadJsonAsync(EffectIdentityState.StatePath)),
            EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        Assert.Equal(root.EffectId, Assert.Single(effects.State!.Entries).EffectId);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
        Assert.True(definitions.IsValid);
        var resources = ResourceStateContract.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.StatePath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(resources.IsValid);
        var balances = resources.Ledger!.Entries.Where(entry => entry.Coordinate.ResourceKey == "spiritual_action_points").ToArray();
        Assert.Equal(2, balances.Length);
        Assert.All(balances, entry => Assert.Equal(3m, entry.Current));
        var history = ResourceHistoryState.ParseCanonical(await context.FileSystem.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
            definitions.Catalog!, allowMissingPristine: false);
        Assert.True(history.IsValid);
        var spends = history.History!.Transitions.Where(entry => entry.Turn == 42 && entry.Operation == ResourceTransitionOperation.Spend).ToArray();
        Assert.Equal(2, spends.Length);
        Assert.All(spends, entry =>
        {
            Assert.Equal(3m, entry.AppliedAmount);
            Assert.Equal(6m, entry.BeforeState!.Current);
            Assert.Equal(3m, entry.AfterState!.Current);
        });
        foreach (var path in new[] { SpiritualWoundCaptureCheckpointState.StatePath,
                     SpiritualWoundDecisionPendingState.StatePath, AcceptedMechanicsPlan.WoundCommandPath,
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json" })
            Assert.False(context.FileSystem.FileExists(path), path);
    }

    /// <summary>
    /// Accepts either full restoration or full committed mechanics, scheduler, player output, story and terminal cleanup.
    /// </summary>
    /// <param name="context">
    /// Original fixture pointing at the same physical root as the new engine.
    /// </param>
    /// <param name="engine">
    /// Fresh engine after its actual cold lifecycle operation completes without a GM response.
    /// </param>
    /// <param name="original">
    /// Exact rollback-backup bytes registered in the signed original manifest, including roots initialized during signing.
    /// </param>
    /// <param name="originalStory">
    /// Exact real story41 bytes, captured before original signing.
    /// </param>
    /// <param name="cut">
    /// Exact durable session images after common publication at the selected interruption boundary.
    /// </param>
    /// <param name="request">
    /// Original player action and identity, used to bind any single accepted story entry.
    /// </param>
    /// <param name="diagnostics">
    /// Bounded engine diagnostics included if neither atomic outcome holds.
    /// </param>
    /// <returns>
    /// A task completing after one coherent branch passes, with no requirement to roll forward or redeliver notices.
    /// </returns>
    private static async Task AssertSpiritualInterruptionAtomicOutcomeAsync(ResourceMaterializationTestContext context,
        GameEngine engine, Dictionary<string, byte[]> original, byte[] originalStory, Dictionary<string, byte[]> cut,
        TurnRequest request, string diagnostics)
    {
        var current = await ReadSpiritualInterruptionTrackedImagesAsync(engine, GetPrivateField<FileSystemManager>(engine, "_fs"));
        var restoredDifferences = SpiritualInterruptionImageDifferences(original, current);
        var published = cut.ToDictionary(pair => pair.Key.Replace('\\', '/'), pair => pair.Value, StringComparer.Ordinal);
        var currentFiles = await ReadSpiritualEntryGuardFilesAsync(context);
        var currentPhysical = currentFiles
            .ToDictionary(pair => pair.Key.Replace('\\', '/'), pair => pair.Value, StringComparer.Ordinal);
        // Also prove original absence directly for newly published canonical paths, independently of the current selector.
        var retainedNewPaths = published.Keys.Concat(currentPhysical.Keys).Distinct(StringComparer.Ordinal)
            .Where(path => IsSpiritualInterruptionCommittedPath(path) && !original.ContainsKey(path) && currentPhysical.ContainsKey(path))
            .Order(StringComparer.Ordinal).ToArray();
        var acceptedPaths = published.Keys.Concat(current.Keys).Where(IsSpiritualInterruptionCommittedPath).Distinct(StringComparer.Ordinal);
        var committedDifferences = acceptedPaths.Where(path => !published.TryGetValue(path, out var expected) ||
            !current.TryGetValue(path, out var actual) || !expected.AsSpan().SequenceEqual(actual)).Order(StringComparer.Ordinal).ToArray();
        var storyBytes = await context.FileSystem.ReadFileBytesAsync("stories/chaos_sea.jsonl");
        var story = await new StoryService(context.FileSystem, NullLogger<StoryService>.Instance).ReadStoryAsync("stories/chaos_sea.jsonl");
        var turn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber;
        var restored = restoredDifferences.Length == 0 && retainedNewPaths.Length == 0 && storyBytes is not null &&
            originalStory.AsSpan().SequenceEqual(storyBytes) && turn == 41;
        var acceptedStory = story.Where(entry => entry.Turn == 42).ToArray();
        var cutStory = published["stories/chaos_sea.jsonl"];
        var storyWasAlreadyAppended = !cutStory.AsSpan().SequenceEqual(originalStory);
        var accepted = committedDifferences.Length == 0 && turn == 42 && story.Count == 2 && story.Count(entry => entry.Turn == 41) == 1 &&
            storyBytes is not null && storyBytes.Length >= originalStory.Length &&
            storyBytes.AsSpan(0, originalStory.Length).SequenceEqual(originalStory) &&
            (!storyWasAlreadyAppended || cutStory.AsSpan().SequenceEqual(storyBytes)) &&
            acceptedStory.Length == 1 && acceptedStory[0].Player == request.PlayerAction &&
            acceptedStory[0].Narrative == "Чужое давление надломило волю хранителя.";
        Assert.True(restored || accepted,
            $"Neither atomic outcome: turn={turn}; story turns={string.Join(",", story.Select(entry => entry.Turn))}; " +
            $"restored differences={string.Join(",", restoredDifferences.Take(24))}; " +
            $"new canonical paths retained={string.Join(",", retainedNewPaths.Take(24))}; " +
            $"committed differences={string.Join(",", committedDifferences.Take(24))}. {diagnostics}");
        if (accepted)
        {
            await AssertSpiritualInterruptionPublicationAsync(context);
            if (storyWasAlreadyAppended)
                AssertInactiveSnapshotEvidenceArchive(cut, currentFiles);
        }
        foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                     "game_state/control/pending_turn_snapshot.json", "game_state/control/pending_turn_snapshot.authority.json",
                     "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
                     SpiritualWoundCaptureCheckpointState.StatePath, SpiritualWoundDecisionPendingState.StatePath,
                     AcceptedMechanicsPlan.WoundCommandPath })
            Assert.False(context.FileSystem.FileExists(path), $"{(restored ? "Restored" : "Committed")} recovery retained {path}.");
        var snapshotDirectory = context.FileSystem.ResolvePath("game_state/control/pending_turn_snapshot");
        Assert.True(!Directory.Exists(snapshotDirectory) || !Directory.EnumerateFiles(snapshotDirectory, "*", SearchOption.AllDirectories).Any(),
            "Recovery retained signed snapshot payloads after terminal cleanup.");
    }

    /// <summary>
    /// Selects committed mechanical state, lore, scheduler and authored player output while leaving terminal cleanup to separate assertions.
    /// </summary>
    /// <param name="path">
    /// Normalized session-relative path from a real before or after inventory.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for non-transient committed state; <see langword="false"/> for protocol, rollback, snapshot and chat transport files.
    /// </returns>
    private static bool IsSpiritualInterruptionCommittedPath(string path) =>
        !path.Contains(".rollback.", StringComparison.Ordinal) &&
        ((path.StartsWith("game_state/", StringComparison.Ordinal) && !path.StartsWith("game_state/control/", StringComparison.Ordinal) &&
          path != "game_state/history/chat_log.json") || path.StartsWith("lore/", StringComparison.Ordinal) ||
         WoundAcceptedTurnSnapshotContract.SchedulerPaths.Contains(path) || WoundAcceptedTurnSnapshotContract.OutputPaths.Contains(path));

    /// <summary>
    /// Compares exact physical inventory and bytes without normalizing JSON or overlooking newly created paths.
    /// </summary>
    /// <param name="expected">
    /// Authentic original inventory before the interrupted turn.
    /// </param>
    /// <param name="actual">
    /// Actual production rollback inventory after cold processing.
    /// </param>
    /// <returns>
    /// Sorted missing, added or byte-different paths; an empty array means exact restoration.
    /// </returns>
    private static string[] SpiritualInterruptionImageDifferences(Dictionary<string, byte[]> expected, Dictionary<string, byte[]> actual) =>
        expected.Keys.Concat(actual.Keys).Distinct(StringComparer.Ordinal).Where(path =>
            !expected.TryGetValue(path, out var before) || !actual.TryGetValue(path, out var after) ||
            !before.AsSpan().SequenceEqual(after)).Order(StringComparer.Ordinal).ToArray();
}
