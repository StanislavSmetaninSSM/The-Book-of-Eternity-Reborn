using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoryAppendUnknown_OriginalApiPreservesDecision(bool marker)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(files);
        const string relative = "stories/chaos_sea.jsonl";
        await files.WriteFileAtomicAsync(relative, "{\"turn\":0,\"narrative\":\"prior\"}\n");
        var before = File.ReadAllBytes(files.ResolvePath(relative));
        cut.Select = (path, member) => path == files.ResolvePath(relative) && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        cut.Armed = true;
        var story = new StoryService(files, NullLogger<StoryService>.Instance);
        var failure = await Record.ExceptionAsync(() => marker
            ? story.AppendMarkerAsync("Chaos Sea", 0, "INCARNATION", "exact marker")
            : story.AppendTurnAsync(3, "Chaos Sea", 0, "action", "exact narrative"));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { marker, before, Failure = failure?.ToString(), Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped();
        Assert.Same(cut.OriginalUncertainty, failure);
        Assert.NotNull(cut.PublishedBytes);
        Assert.True(cut.PublishedBytes!.AsSpan().StartsWith(before));
        Assert.Contains(marker ? "[INCARNATION]" : "exact narrative", Encoding.UTF8.GetString(cut.PublishedBytes), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StoryAppendKnownRollback_PreservesBestEffortPolicy(bool marker)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        FileSystemManager? files = null;
        var armed = false;
        var cuts = 0;
        byte[]? journalAtCut = null;
        byte[]? published = null;
        var known = new InvalidOperationException("known nontransient story publication failure");
        const string relative = "stories/chaos_sea.jsonl";
        var hooks = new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) =>
        {
            if (!armed) return;
            if (cuts != 0) return;
            if (phase != TrustedLocalPublicationPhase.MemberPublished) return;
            var path = Path.Combine(files!.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
            var bytes = File.ReadAllBytes(path);
            using var journal = CleanupPublicationCut.Metadata(bytes);
            if (journal.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString() != files.ResolvePath(relative)) return;
            Assert.Equal(0, index); Assert.False(journal.RootElement.GetProperty("Committed").GetBoolean());
            journalAtCut = bytes; published = File.ReadAllBytes(files.ResolvePath(relative)); cuts++;
            throw known;
        }};
        files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
        await files.WriteFileAtomicAsync(relative, "{\"turn\":0,\"narrative\":\"prior\"}\n");
        var before = File.ReadAllBytes(files.ResolvePath(relative));
        var logger = new StoryFailureLogger();
        var story = new StoryService(files, logger);
        armed = true;
        var failure = await Record.ExceptionAsync(() => marker
            ? story.AppendMarkerAsync("Chaos Sea", 0, "INCARNATION", "known marker")
            : story.AppendTurnAsync(3, "Chaos Sea", 0, "action", "known narrative"));
        var after = File.ReadAllBytes(files.ResolvePath(relative));
        var retainedJournal = CleanupPublicationCut.ReadOptional(Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json"));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { marker, cuts, before, after, published,
            journalAtCut, retainedJournal, Failure = failure?.ToString(), Logged = logger.Failure?.ToString() }));
        Assert.Equal(1, cuts);
        Assert.Null(failure); Assert.Same(known, logger.Failure); Assert.Equal(before, after); Assert.Null(retainedJournal);
        Assert.NotEqual(before, published);
    }

    [Theory]
    [InlineData("life")]
    [InlineData("incarnation")]
    [InlineData("ascension")]
    public async Task StoryMarkerUnknown_OriginalTransitionStopsWithPriorCommits(string mode)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var requestAttempts = 0;
        // A missed marker may not dispatch a GM request. This safety refusal is not the tested cut.
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = cut.Hooks.LocalPublicationObserver,
            LocalPublicationRecoveryObserver = cut.Hooks.LocalPublicationRecoveryObserver,
            BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
            AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync,
            BeforeCanonicalWriteLockOpenAsync = cut.Hooks.BeforeCanonicalWriteLockOpenAsync,
            BeforeCanonicalMutationAsync = path =>
            {
                if (cut.Armed && path.Replace('\\', '/').EndsWith("input/turn_request.json", StringComparison.Ordinal))
                { requestAttempts++; throw new InvalidOperationException("fixture refuses any later GM request"); }
                return Task.CompletedTask;
            }
        };
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, hooks);
        cut.Attach(files);
        var soul = CreateLifecycleSoulState("Story outcome fixture", mode == "life" ? "Mortal World" : "Chaos Sea");
        if (mode == "ascension")
        {
            soul["currentIncarnation"] = 4;
            soul["soulProgression"] = new JsonObject { ["totalExperience"] = AfterlifeProgressionTuning.AscensionReadyEnlightenmentExperience };
        }
        await SeedMortalLifeTransitionAuthorityAsync(soul);
        _fs.DeleteFile("game_state/control/life_transitions.json");
        _fs.DeleteFile("game_state/control/incarnation_trigger.json");
        _fs.DeleteFile("game_state/control/ascension.json");
        var control = mode switch { "life" => "life_transitions", "incarnation" => "incarnation_trigger", _ => "ascension" };
        var trigger = mode switch
        {
            "life" => "{\"reason\":\"Voluntary\",\"summary\":\"bounded story outcome\"}",
            "incarnation" => "{\"worldDescription\":\"A quiet port\",\"characterDescription\":\"A clerk\",\"circumstances\":\"An ordinary arrival\",\"source\":\"test\"}",
            _ => "{\"AscensionTrigger\":true,\"playerChoice\":\"Ascension\"}"
        };
        await _fs.WriteFileAtomicAsync($"game_state/control/{control}.json", trigger);
        if (mode != "ascension")
        {
            await WriteCurrentSoulStateToPendingSnapshotAsync();
            await WritePendingTurnSnapshotManifestAsync("story-session", "story-request", 3, "game_state/meta/soul_state.json");
            await WriteJsonAsync("input/turn_request.json", new { sessionId = "story-session", requestId = "story-request", turnNumber = 3, playerAction = "bounded story outcome" });
            await WriteJsonAsync("ready/turn_complete.json", new { sessionId = "story-session", requestId = "story-request", turnNumber = 3,
                status = "success", timestamp = "2026-10-09T00:00:00Z", filesModified = new[] { $"game_state/control/{control}.json" } });
        }
        var input = new StoryTransitionInput(mode == "ascension" ? 0 : 1);
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        var state = GetPrivateField<StateManager>(engine, "_stateManager");
        await state.RefreshGameStateAsync();
        Assert.Equal(mode == "life" ? "Mortal World" : "Chaos Sea", state.CurrentState.CurrentRealm);
        object? accepted = null;
        if (mode != "ascension")
        {
            var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
            accepted = await InvokePrivateTaskResultAsync(engine, "LoadValidatedPendingTurnSnapshotContextAsync", manifest, true);
            Assert.NotNull(accepted);
            if (mode == "incarnation")
            {
                Assert.True(IncarnationTriggerContract.TryParse(trigger, out var payload));
                Assert.True(await InvokePrivateAsync<bool>(engine, "HasAcceptedTurnAuthorityForIncarnationTriggerAsync", payload, false, accepted));
            }
        }
        else Assert.True(await InvokePrivateAsync<bool>(engine, "HasMaximumEnlightenmentAsync"));
        var relative = mode switch { "life" => "stories/mortal_life_2.jsonl", "incarnation" => "stories/chaos_sea.jsonl", _ => "stories/shining_abode.jsonl" };
        var expectedMarker = mode switch { "life" => "[VOLUNTARY_END]", "incarnation" => "[INCARNATION]", _ => "[ASCENSION]" };
        var tracked = new[] { "game_state/meta/soul_state.json", ShiningAbodeState.StatePath,
            AfterlifeEntityProfileState.StatePath, CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath, "game_state/core/player_status.json" };
        var committed = new List<object>();
        var committedPaths = new HashSet<string>(StringComparer.Ordinal);
        Dictionary<string, byte[]?>? imagesAtCut = null;
        cut.ObserveBeforeCut = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.Committed) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
            var paths = journal.RootElement.GetProperty("Members").EnumerateArray().Select(x => x.GetProperty("Path").GetString()!).ToArray();
            foreach (var path in paths) committedPaths.Add(path);
            committed.Add(new { Transaction = journal.RootElement.GetProperty("TransactionId").GetString(), Paths = paths });
        };
        cut.Select = (path, member) => path == files.ResolvePath(relative) && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        cut.BeforeCut = () =>
        {
            Assert.Contains(expectedMarker, File.ReadAllText(files.ResolvePath(relative)), StringComparison.Ordinal);
            imagesAtCut = tracked.ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(files.ResolvePath(x)), StringComparer.Ordinal);
            var publishedRealm = JsonNode.Parse(File.ReadAllText(files.ResolvePath(tracked[0])))!["currentRealm"]!.GetValue<string>();
            Assert.Equal(mode == "ascension" ? "Shining Abode" : "Mortal World", publishedRealm);
            if (mode != "life") Assert.Contains(files.ResolvePath(tracked[0]), committedPaths);
            if (mode == "ascension") Assert.Contains(files.ResolvePath(ShiningAbodeState.StatePath), committedPaths);
            if (mode == "incarnation") Assert.Contains(files.ResolvePath("game_state/core/player_status.json"), committedPaths);
        };
        cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => mode == "ascension"
            ? InvokePrivateTaskAsync(engine, "CheckAscensionTrigger")
            : InvokePrivateTaskAsync(engine, mode == "life" ? "CheckLifeTransitions" : "CheckGmIncarnationTrigger", accepted));
        var afterImages = tracked.ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(files.ResolvePath(x)), StringComparer.Ordinal);
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, Failure = failure?.ToString(), input.Reads, input.UnexpectedReads,
            requestAttempts, committed, imagesAtCut, afterImages, Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped(); Assert.Same(cut.OriginalUncertainty, failure);
        Assert.Equal(0, requestAttempts); input.AssertCompleted(); Assert.NotNull(imagesAtCut);
        foreach (var path in tracked) Assert.Equal(imagesAtCut![path], afterImages[path]);
    }

    private sealed class StoryFailureLogger : ILogger<StoryService>
    {
        internal Exception? Failure { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter) => Failure = error;
    }

    private sealed class StoryTransitionInput(int allowed) : IConsoleInputSource
    {
        internal int Reads { get; private set; }
        internal int UnexpectedReads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => Reads < allowed;
        public ConsoleKeyInfo ReadKey(bool intercept = true)
        {
            if (++Reads <= allowed) return Key(ConsoleKey.Enter);
            UnexpectedReads++; throw new InvalidOperationException("bounded story transition must not enter later UI");
        }
        public string? ReadLine() { UnexpectedReads++; throw new InvalidOperationException("bounded story transition must not read a line"); }
        public void AssertCompleted() { Assert.Equal(allowed, Reads); Assert.Equal(0, UnexpectedReads); }
    }
}
