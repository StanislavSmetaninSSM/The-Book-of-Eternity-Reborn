using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("base")]
    [InlineData("status_clear")]
    [InlineData("level_mark")]
    [InlineData("computed")]
    public async Task ProgressionUnknown_OriginalConsumerPreservesDecisionAndStops(string mode)
    {
        using var ownedFixture = new CleanupOwnedFixture(_rootPath, line => _directGachaOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        using var cut = new CleanupPublicationCut();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(files);
        var input = new ProgressionNoInput();
        var engine = CreateGameEngine(input, s => { s.GmBridgeAutoStart = false; s.MusicEnabled = false; s.SoundEnabled = false; }, fileSystem: files);
        var state = GetPrivateField<StateManager>(engine, "_stateManager");
        await state.BootstrapLocalStorageAsync();
        // This bounded original consumer receives an already selected mortal runtime state.
        state.CurrentState.CurrentRealm = "Mortal World";
        Assert.False(state.CurrentState.IsInAfterlifeRealm);
        const string basePath = "game_state/misc/characteristics.json";
        const string statusPath = "game_state/player/status_changes.json";
        const string pointsPath = "game_state/player/stat_points.json";
        const string computedPath = "game_state/player/computed_characteristics.json";
        await files.WriteFileAtomicAsync(basePath, JsonSerializer.Serialize(Characteristics.All.ToDictionary(x => x, _ => 1)));
        await files.WriteFileAtomicAsync("game_state/player/experience.json", """{"level":2,"playerLevel":2}""");
        await files.WriteFileAtomicAsync(pointsPath, """{"unspentStatPoints":0}""");
        await files.WriteFileAtomicAsync(statusPath, """{"statsIncreased":["strength"],"retained":"status sentinel"}""");
        await files.WriteFileAtomicAsync(computedPath, """{"playerLevel":2,"unspentStatPoints":0,"characteristics":{"strength":1}}""");
        var targetRelative = mode switch { "base" => basePath, "status_clear" => statusPath, "level_mark" => pointsPath, _ => computedPath };
        var target = files.ResolvePath(targetRelative);
        var before = File.ReadAllBytes(target);
        var priorRelative = mode == "status_clear" ? basePath : mode == "computed" ? pointsPath : null;
        var priorPath = priorRelative == null ? null : files.ResolvePath(priorRelative);
        byte[]? priorCommittedBytes = null;
        string? priorTransaction = null;
        var priorCommitted = 0;
        cut.ObserveBeforeCut = (phase, _) =>
        {
            if (priorPath == null || phase != TrustedLocalPublicationPhase.Committed) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(cut.JournalPath));
            Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
            if (!journal.RootElement.GetProperty("Members").EnumerateArray().Any(x => x.GetProperty("Path").GetString() == priorPath)) return;
            priorCommitted++;
            priorTransaction = journal.RootElement.GetProperty("TransactionId").GetString();
            priorCommittedBytes = File.ReadAllBytes(priorPath);
        };
        cut.Select = (path, member) => path == target && member.GetProperty("After").GetProperty("Exists").GetBoolean();
        cut.BeforeCut = () =>
        {
            if (priorPath != null) { Assert.Equal(1, priorCommitted); Assert.NotNull(priorTransaction); Assert.Equal(priorCommittedBytes, File.ReadAllBytes(priorPath)); }
            var published = JsonNode.Parse(File.ReadAllText(target))!.AsObject();
            if (mode == "base") Assert.Equal(2, published["strength"]!.GetValue<int>());
            if (mode == "status_clear") { Assert.False(published.ContainsKey("statsIncreased")); Assert.Equal("status sentinel", published["retained"]!.GetValue<string>()); }
            if (mode == "level_mark") Assert.Equal(2, published["levelUpStatPointsAwardedThroughLevel"]!.GetValue<int>());
            if (mode == "computed") Assert.Equal(5, published["unspentStatPoints"]!.GetValue<int>());
        };
        cut.Armed = true;
        var failure = await Record.ExceptionAsync(() => mode == "computed"
            ? GetPrivateField<CharacteristicsService>(engine, "_charService").AddStatPoints(5)
            : InvokePrivateTaskAsync(engine, mode == "level_mark" ? "CheckLevelUpAsync" : "ProcessMortalProgressionAfterAcceptedTurnAsync"));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, Failure = failure?.ToString(), before,
            priorCommitted, priorTransaction, priorCommittedBytes,
            PriorAfter = priorPath == null ? null : CleanupPublicationCut.ReadOptional(priorPath), input.Reads, Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped();
        Assert.Same(cut.OriginalUncertainty, failure); Assert.Equal(0, input.Reads);
        if (priorPath != null) { Assert.Equal(1, priorCommitted); Assert.Equal(priorCommittedBytes, File.ReadAllBytes(priorPath)); }
    }

    private sealed class ProgressionNoInput : IConsoleInputSource
    {
        public int Reads { get; private set; }
        public bool IsScripted => true;
        public bool KeyAvailable => false;
        public ConsoleKeyInfo ReadKey(bool intercept = true) { Reads++; throw new InvalidOperationException("This bounded progression case must not enter interactive distribution."); }
        public string? ReadLine() { Reads++; throw new InvalidOperationException("This bounded progression case must not read input."); }
    }
}
