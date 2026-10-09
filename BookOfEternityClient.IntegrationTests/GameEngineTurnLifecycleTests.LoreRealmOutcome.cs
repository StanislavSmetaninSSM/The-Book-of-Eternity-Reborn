using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    private static readonly string[] LoreOutcomeFiles =
        ["lore/current_world/a.json", "lore/current_world/nested/b.json", "lore/current_world/literal\\name.json"];
    private const string LoreOutcomeSentinel = "lore/current_world/keep.rollback.original";
    private static readonly string[] RealmOutcomeImages =
        ["game_state/meta/soul_state.json", AfterlifeEntityProfileState.StatePath, ShiningAbodeState.StatePath,
         CanonicalResourceOwnerAuthorityComposer.AuthorityPath, ResourceMaterializationContract.DefinitionsPath,
         ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath];

    [Fact]
    public async Task LoreClear_OriginalConsumerUsesOrdinaryPublicationsForExactFiles()
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new LoreRealmProbe(_rootPath);
        await SeedLoreOutcomeFilesAsync(probe.Files);
        probe.Cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => probe.Files.ClearCurrentWorldLoreAsync());
        var remaining = LoreOutcomeFiles.ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(probe.Files.ResolvePath(x)));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { Failure = failure?.ToString(), remaining,
            probe.Committed, Sentinel = File.ReadAllText(probe.Files.ResolvePath(LoreOutcomeSentinel)) }));
        Assert.Null(failure);
        Assert.All(remaining.Values, Assert.Null);
        Assert.Equal("retained rollback", File.ReadAllText(probe.Files.ResolvePath(LoreOutcomeSentinel)));
        // Old physical deletion reaches the effect but has zero ordinary decisions. This is a routing oracle, not CSP.
        Assert.Equal(LoreOutcomeFiles.Length, probe.Committed.Keys.Count(IsLoreOutcomePath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LoreClear_OriginalIncarnationStopsOrRestoresKnownSetupFailure(bool known)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new LoreRealmProbe(_rootPath);
        await SeedMortalLifeTransitionAuthorityAsync(CreateLifecycleSoulState("Lore outcome", "Chaos Sea"));
        var incarnationFiles = LoreOutcomeFiles.Take(2).ToArray();
        await SeedLoreOutcomeFilesAsync(probe.Files, incarnationFiles);
        var before = incarnationFiles.ToDictionary(x => x, x => File.ReadAllBytes(probe.Files.ResolvePath(x)));
        var refusal = new InvalidOperationException("bounded pending-setup preflight refusal");
        var setupRefusals = 0;
        probe.BeforeMutation = path =>
        {
            if (probe.Cut.Armed && probe.Cut.Cuts == 0 && path == WorldDirectiveService.PendingSetupPath)
            { setupRefusals++; throw refusal; }
            return Task.CompletedTask;
        };
        probe.Cut.Select = (path, member) => !known && IsLoreOutcomePath(path) &&
            !member.GetProperty("After").GetProperty("Exists").GetBoolean() &&
            probe.Committed.Keys.Count(IsLoreOutcomePath) == 1;
        probe.Cut.BeforeCut = () => Assert.Single(probe.Committed.Keys.Where(IsLoreOutcomePath));
        var input = new LoreRealmInput(["A clerk", "A quiet port", "An ordinary arrival"], 0);
        var engine = CreateGameEngine(input, InertRealmSettings, fileSystem: probe.Files);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        using var console = new LoadMenuAnsiConsole();
        var originalConsole = AnsiConsole.Console;
        Exception? failure;
        try
        {
            AnsiConsole.Console = console;
            probe.Cut.Armed = true;
            failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "HandleIncarnation"));
        }
        finally { AnsiConsole.Console = originalConsole; }
        var after = incarnationFiles.ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(probe.Files.ResolvePath(x)));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { known, setupRefusals, probe.RequestAttempts,
            input.LineReads, input.KeyReads, ConsoleReads = console.Reads, Failure = failure?.ToString(), before, after,
            probe.Committed, Cut = probe.Cut.Evidence() }));
        input.AssertCompleted(); Assert.Equal(1, console.Reads); Assert.Equal(0, probe.RequestAttempts);
        Assert.Equal("retained rollback", File.ReadAllText(probe.Files.ResolvePath(LoreOutcomeSentinel)));
        if (known)
        {
            Assert.Equal(1, setupRefusals); Assert.Same(refusal, failure);
            foreach (var path in incarnationFiles) Assert.Equal(before[path], after[path]);
            Assert.Equal(0, probe.Cut.Cuts);
            Assert.Null(CleanupPublicationCut.ReadOptional(probe.Cut.JournalPath));
        }
        else
        {
            // On the old raw route, setup refusal prevents any GM dispatch; Cuts=0 is recorded as routing absence.
            probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
            Assert.Equal(0, setupRefusals);
            Assert.Single(probe.Committed.Keys.Where(IsLoreOutcomePath));
            Assert.Null(CleanupPublicationCut.ReadOptional(probe.Committed.Keys.Single(IsLoreOutcomePath)));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealmUnknown_OriginalLifeTransitionPreservesDecisionAndPriorCommits(bool reset)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new LoreRealmProbe(_rootPath);
        await SeedMortalLifeTransitionAuthorityAsync(CreateLifecycleSoulState("Realm outcome", "Mortal World"));
        await probe.Files.WriteFileAtomicAsync(RivalSoulArcService.StatePath, "{\"arcs\":[]}");
        await WriteCurrentSoulStateToPendingSnapshotAsync();
        await WritePendingTurnSnapshotManifestAsync("realm-session", "realm-request", 3, "game_state/meta/soul_state.json");
        await WriteJsonAsync("input/turn_request.json", new { sessionId = "realm-session", requestId = "realm-request", turnNumber = 3, playerAction = "bounded life end" });
        await WriteJsonAsync("ready/turn_complete.json", new { sessionId = "realm-session", requestId = "realm-request", turnNumber = 3,
            status = "success", timestamp = "2026-10-09T00:00:00Z", filesModified = new[] { "game_state/control/life_transitions.json" } });
        await WriteJsonAsync("game_state/control/life_transitions.json", new { reason = "Voluntary", summary = "bounded realm outcome" });
        var input = new LoreRealmInput([], 1);
        var engine = CreateGameEngine(input, InertRealmSettings, fileSystem: probe.Files);
        await GetPrivateField<StateManager>(engine, "_stateManager").RefreshGameStateAsync();
        var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
        var accepted = await InvokePrivateTaskResultAsync(engine, "LoadValidatedPendingTurnSnapshotContextAsync", manifest, true);
        Assert.NotNull(accepted);
        var acceptedEvidence = new[] { LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, "input/turn_request.json", "ready/turn_complete.json" }
            .ToDictionary(x => x, x => File.ReadAllBytes(probe.Files.ResolvePath(x)));
        var generation = await GetOrCreateSessionGenerationAsync();
        probe.Cut.Select = (path, member) => reset
            ? path == probe.Files.ResolvePath(RivalSoulArcService.StatePath) && !member.GetProperty("After").GetProperty("Exists").GetBoolean()
            : probe.IsFirstRealmMember(path);
        Dictionary<string, byte[]?>? atCut = null;
        probe.Cut.BeforeCut = () =>
        {
            probe.AssertGeneration(generation);
            atCut = RealmOutcomeImages.ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(probe.Files.ResolvePath(x)));
            if (reset)
            {
                Assert.Equal("Chaos Sea", JsonNode.Parse(File.ReadAllText(probe.Files.ResolvePath(RealmOutcomeImages[0])))!["currentRealm"]!.GetValue<string>());
                foreach (var path in new[] { RealmOutcomeImages[0], AfterlifeEntityProfileState.StatePath,
                    ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath })
                    Assert.Equal(probe.Committed[probe.Files.ResolvePath(path)], atCut[path]);
            }
        };
        probe.Cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine, "CheckLifeTransitions", accepted));
        var after = RealmOutcomeImages.ToDictionary(x => x, x => CleanupPublicationCut.ReadOptional(probe.Files.ResolvePath(x)));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { reset, generation, Failure = failure?.ToString(),
            probe.RequestAttempts, input.KeyReads, acceptedEvidence, atCut, after, probe.Committed, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.Equal(0, probe.RequestAttempts); input.AssertCompleted();
        if (reset) foreach (var path in RealmOutcomeImages) Assert.Equal(atCut![path], after[path]);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RealmUnknown_OriginalShiningReturnPreservesDecision(bool reentry)
    {
        using var owned = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var probe = new LoreRealmProbe(_rootPath);
        var realm = reentry ? "Chaos Sea" : "Shining Abode";
        var soul = CreateLifecycleSoulState("Return outcome", realm);
        soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject { [AfterlifeSpiritualConflictState.SpiritFocusTierProperty] = 0 };
        var shining = ShiningAbodeState.CreateDefaultState();
        shining["availability"] = ShiningAbodeState.AvailabilityActive;
        shining["lightSparks"] = 88;
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(_fs, new AfterlifeOwnerResourceAcceptedState(
            Profiles: CreatePlayerSoulProfiles("Return outcome", realm),
            SpiritualConflict: AfterlifeSpiritualConflictState.CreateDefaultRoot(), SoulState: soul,
            ShiningAbode: shining, Guardians: CreateEmptyGuardiansState()));
        var input = new LoreRealmInput([], 0);
        var engine = CreateGameEngine(input, InertRealmSettings, fileSystem: probe.Files);
        var state = GetPrivateField<StateManager>(engine, "_stateManager");
        await state.RefreshGameStateAsync(); Assert.Equal(realm, state.CurrentState.CurrentRealm);
        var generation = await GetOrCreateSessionGenerationAsync();
        probe.Cut.Select = (path, _) => probe.IsFirstRealmMember(path);
        probe.Cut.BeforeCut = () => probe.AssertGeneration(generation);
        using var console = new LoadMenuAnsiConsole();
        var originalConsole = AnsiConsole.Console;
        Exception? failure;
        try
        {
            AnsiConsole.Console = console;
            probe.Cut.Armed = true;
            failure = await Record.ExceptionAsync(() => InvokePrivateTaskAsync(engine,
                reentry ? "HandleReenterShiningAbode" : "TryPerformOrdinaryReturnToChaosSeaFromShiningAbodeAsync"));
        }
        finally { AnsiConsole.Console = originalConsole; }
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { reentry, generation, Failure = failure?.ToString(),
            ConsoleReads = console.Reads, probe.RequestAttempts, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.Equal(reentry ? 1 : 0, console.Reads); input.AssertCompleted(); Assert.Equal(0, probe.RequestAttempts);
    }

    private static void InertRealmSettings(BookOfEternityClient.Configuration.GameSettings settings)
    { settings.GmBridgeAutoStart = false; settings.MusicEnabled = false; settings.SoundEnabled = false; }
    private static bool IsLoreOutcomePath(string path) => LoreOutcomeFiles.Any(x => path.EndsWith("/" + x, StringComparison.Ordinal));
    private static async Task SeedLoreOutcomeFilesAsync(FileSystemManager files, string[]? paths = null)
    {
        foreach (var path in paths ?? LoreOutcomeFiles) await files.WriteFileAtomicAsync(path, JsonSerializer.Serialize(new { path }));
        await files.WriteFileAtomicAsync(LoreOutcomeSentinel, "retained rollback");
    }
    private sealed class LoreRealmInput(string[] lines, int keys) : IConsoleInputSource
    {
        private readonly Queue<string> _lines = new(lines);
        internal int LineReads { get; private set; }
        internal int KeyReads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => false;
        public string? ReadLine() { LineReads++; Assert.NotEmpty(_lines); return _lines.Dequeue(); }
        public ConsoleKeyInfo ReadKey(bool intercept = true) { Assert.True(++KeyReads <= keys, "no later input allowed"); return Key(ConsoleKey.Enter); }
        public void AssertCompleted() { Assert.Empty(_lines); Assert.Equal(keys, KeyReads); }
    }
    private sealed class LoreRealmProbe : IDisposable
    {
        internal CleanupPublicationCut Cut { get; } = new();
        internal FileSystemManager Files { get; }
        internal Dictionary<string, byte[]?> Committed { get; } = new(StringComparer.Ordinal);
        internal Func<string, Task>? BeforeMutation { get; set; }
        internal int RequestAttempts { get; private set; }
        internal LoreRealmProbe(string root)
        {
            var hooks = new FileSystemManagerHooks
            {
                LocalPublicationObserver = Cut.Hooks.LocalPublicationObserver,
                LocalPublicationRecoveryObserver = Cut.Hooks.LocalPublicationRecoveryObserver,
                BeforeCanonicalMutationBoundaryAsync = Cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = Cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                SessionOperationClosingAsync = Cut.Hooks.SessionOperationClosingAsync,
                BeforeCanonicalWriteLockOpenAsync = async () =>
                {
                    var before = Cut.LaterLeases;
                    await Cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                    if (Cut.LaterLeases > before) throw new InvalidOperationException("fixture refuses post-Unknown logging admission");
                },
                BeforeCanonicalMutationAsync = async path =>
                {
                    if (Cut.Armed && path == "input/turn_request.json")
                    { RequestAttempts++; throw new InvalidOperationException("fixture refuses GM dispatch"); }
                    if (BeforeMutation != null) await BeforeMutation(path);
                }
            };
            Files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance, hooks);
            Cut.Attach(Files);
            Cut.ObserveBeforeCut = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
                foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
                {
                    var path = member.GetProperty("Path").GetString()!;
                    Committed[path] = CleanupPublicationCut.ReadOptional(path);
                }
            };
        }
        internal bool IsFirstRealmMember(string path)
        {
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
            var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
            return members[0].GetProperty("Path").GetString() == path &&
                members.Any(x => x.GetProperty("Path").GetString() == Files.ResolvePath("game_state/meta/soul_state.json")) &&
                members.Any(x => x.GetProperty("Path").GetString() == Files.ResolvePath(ResourceMaterializationContract.StatePath));
        }
        internal void AssertGeneration(string generation)
        {
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
            Assert.Equal(generation, journal.RootElement.GetProperty("GenerationBefore").GetProperty("Id").GetString());
            Assert.Equal(generation, journal.RootElement.GetProperty("GenerationAfter").GetProperty("Id").GetString());
        }
        public void Dispose() => Cut.Dispose();
    }
}
