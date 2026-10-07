using System.Text.Json;
using System.Text.Json.Nodes;
using System.Runtime.ExceptionServices;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

// Every test owns its root. Hooks cut actual storage boundaries; the service,
// coordinator, Stage, queue and pending authority remain their production code.
internal sealed class BrowserDirectGachaLinuxFixture(ITestOutputHelper output, string? root = null) : IDisposable
{
    internal const string Soul = "game_state/meta/soul_state.json";
    internal const string DirectRoot = ExplorerLocalTurnRollbackArtifacts.Root + "/browser_direct_gacha";
    internal string Root { get; } = root ?? Path.Combine(Path.GetTempPath(), "boe-direct-gacha-linux-" + Guid.NewGuid().ToString("N"));
    internal FileSystemManager Files { get; private set; } = null!;
    internal StateManager State { get; private set; } = null!;
    internal BrowserAfterlifeWriteService Service { get; private set; } = null!;
    internal Func<string, Task>? Mutation { get; set; }
    internal Action<TrustedLocalPublicationPhase, int>? Publication { get; set; }
    internal Func<Task>? Closing { get; set; }
    internal byte[] BeforeSoul { get; private set; } = [];
    internal byte[] BeforeProfile { get; private set; } = [];
    internal byte[] BeforeDice { get; private set; } = [];
    internal byte[] BeforeHistory { get; private set; } = [];
    internal Exception? StageFailure { get; private set; }
    internal Exception? ConsumerFailure { get; private set; }

    internal async Task InitializeAsync()
    {
        Assert.True(OperatingSystem.IsLinux(), "Linux body execution required");
        Directory.CreateDirectory(Root);
        Files = new(Root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = path => Mutation?.Invoke(path) ?? Task.CompletedTask,
                LocalPublicationObserver = (phase, index) => Publication?.Invoke(phase, index),
                SessionOperationClosingAsync = () => Closing?.Invoke() ?? Task.CompletedTask
            });
        State = new(Files, new GameSettings(), NullLogger<StateManager>.Instance);
        await State.BootstrapLocalStorageAsync();
        var soul = new JsonObject
        {
            ["soulName"] = "Нейтральная душа 世界",
            ["currentRealm"] = "Chaos Sea",
            ["currentIncarnation"] = 4,
            ["inkFeathers"] = new JsonObject { ["current"] = 18, ["total"] = 55 },
            ["soulRelics"] = new JsonObject { ["stored"] = new JsonArray(), ["equipped"] = new JsonArray() }
        };
        await Files.WriteFileAtomicAsync(Soul, soul.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        await Files.WriteFileAtomicAsync(PendingTurnStateService.PendingDiceStatePath, new JsonObject
        {
            ["preGeneratedDices1d20"] = new JsonArray(Enumerable.Range(1, 20).Select(n => (JsonNode?)JsonValue.Create(n)).ToArray()),
            ["gachaBaseResult"] = new JsonObject
            {
                ["diceUsed"] = new JsonArray(18, 18, 18, 18), ["baseScore"] = 72, ["baseRarity"] = "Rare",
                ["formula"] = "client-computed gacha base (range 4-80)"
            },
            ["isFateLocked"] = false, ["createdAtUtc"] = "2026-10-07T00:00:00Z", ["lastUpdatedUtc"] = "2026-10-07T00:00:00Z"
        }.ToJsonString());
        await Files.WriteFileAtomicAsync("game_state/history/chat_log.json", "{\"messages\":[]}");
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(Files,
            new AfterlifeOwnerResourceAcceptedState(SoulState: soul));
        await State.RefreshGameStateAsync();
        Service = new(Files, State, new BrowserLocalWriteCoordinator(Files, new LocalUiSessionLockService(Files)));
        BeforeSoul = File.ReadAllBytes(Files.ResolvePath(Soul));
        BeforeProfile = File.ReadAllBytes(Files.ResolvePath(AfterlifeEntityProfileState.StatePath));
        BeforeDice = File.ReadAllBytes(Files.ResolvePath(PendingTurnStateService.PendingDiceStatePath));
        BeforeHistory = File.ReadAllBytes(Files.ResolvePath("game_state/history/chat_log.json"));
    }

    internal async Task<BrowserPromptWriteResult> PullAsync(LocalUiSessionLockOwner? owner = null)
    {
        void Observe(object? sender, FirstChanceExceptionEventArgs args)
        {
            if (args.Exception.StackTrace?.Contains("ExplorerLocalTurnRollbackArtifacts.StageFileAsync", StringComparison.Ordinal) == true)
                StageFailure = args.Exception;
            if (args.Exception.StackTrace?.Contains("BrowserAfterlife", StringComparison.Ordinal) == true)
                ConsumerFailure = args.Exception;
        }
        AppDomain.CurrentDomain.FirstChanceException += Observe;
        try { return await Service.TryApplyAsync("/gacha", new Dictionary<string, JsonNode?>
        {
            ["gacha_banner"] = JsonValue.Create("direct_chaos_sea"), ["feather_cost"] = JsonValue.Create(7),
            ["confirm_gacha_pull"] = JsonValue.Create(true)
        }, owner ?? new("linux-gacha-fixture", "browser", "Fixture", TimeSpan.FromMinutes(2))); }
        finally { AppDomain.CurrentDomain.FirstChanceException -= Observe; }
    }

    internal string BackupPath() => Assert.Single(Directory.GetFiles(Files.ResolvePath(DirectRoot), "*.rollback.*", SearchOption.AllDirectories));
    internal int Feathers() => JsonNode.Parse(File.ReadAllText(Files.ResolvePath(Soul)))!["inkFeathers"]!["current"]!.GetValue<int>();
    internal JsonObject Manifest() => JsonNode.Parse(File.ReadAllText(Files.ResolvePath(BrowserPendingTurnInspector.PendingTurnSnapshotManifestPath)))!.AsObject();

    public void Dispose()
    {
        Mutation = null; Publication = null; Closing = null;
        if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        output.WriteLine("FixtureCleanup root=" + Root + " rootRemoved=" + !Directory.Exists(Root));
    }
}
