using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class LinuxBrowserGameChainTests(ITestOutputHelper output)
{
    [Fact]
    public Task Inventory_ActualReactSplitColdRestartMerge() => RunAsync("inventory");

    [Fact]
    public Task Saves_ActualReactSaveMutateLoadColdRestartContinue() => RunAsync("saves");

    private async Task RunAsync(string mode)
    {
        Assert.True(OperatingSystem.IsLinux(), "Real Linux browser/host chain; Windows is not qualified.");
        var repo = TestRepoPaths.RepoRoot;
        var own = Path.Combine("/tmp", "gc-" + Guid.NewGuid().ToString("N")[..12]);
        Directory.CreateDirectory(own);
        output.WriteLine("Owned browser chain evidence: " + own);
        var files = new FileSystemManager(Path.Combine(own, "play"), NullLogger<FileSystemManager>.Instance);
        PortableSaveFixture.Seed(files);
        // Explicit current-schema initial fixture, before any game host starts.
        // This is not a GM-materialized item or an accepted gameplay turn.
        var extra = new Dictionary<string, string>
        {
            ["config.json"] = """{"Language":"ru","MusicEnabled":false,"SoundEnabled":false,"GenerateSceneImages":false,"GmBridgeEnabled":false,"BrowserReducedMotion":true}""",
            ["game_state/core/player_status.json"] = """{"currentCondition":"neutral fixture","money":0}""",
            ["game_state/meta/achievements.json"] = """{"unlockedAchievements":[],"trackedProgress":[],"stats":{"totalUnlocked":0,"byCategory":{"combat":0,"exploration":0,"story":0,"social":0,"crafting":0,"meta":0,"death":0,"secret":0},"byRarity":{"common":0,"uncommon":0,"rare":0,"epic":0,"legendary":0}}}""",
            ["lore/codex_entries.json"] = """{"entries":[],"categories":{"cosmology":0,"geography":0,"history":0,"cultures":0,"creatures":0,"characters":0,"artifacts":0,"factions":0,"magic":0,"other":0},"totalEntries":0}"""
        };
        foreach (var name in new[] { "cultures", "geography", "history", "threats", "world_setting" })
            extra["lore/current_world/" + name + ".json"] = """{"fixture":"neutral"}""";
        foreach (var entry in extra)
            await files.WriteFileAtomicAsync(entry.Key, entry.Value);
        var item = MortalItemTestFixture.CreateRawRoot(creationRef: "new_item_gc_stack", materializationId: "mat_item_gc_stack");
        item["name"] = "Лунная трава";
        item["description"] = "Изолированный текущий тестовый предмет для сквозной проверки инвентаря.";
        item["type"] = "material";
        // The shared full root contains fateCards, which the current full-state
        // validator permits only at Rare+. Keep the root complete and legal.
        item["quality"] = "Rare";
        item["rarity"] = "Rare";
        item["count"] = 5;
        var receipt = MortalItemIdentityState.CreateRootReceipt(item, "gc_stack", acceptedTurn: 42);
        item["itemId"] = "gc_stack";
        item["existedId"] = "gc_stack";
        item.Remove("creationRef");
        item["materializationReceipt"] = receipt;
        await files.WriteFileAtomicAsync(InventoryEquipmentService.ItemsPath,
            new JsonObject { ["items"] = new JsonArray(item), ["equippedItems"] = new JsonObject() }.ToJsonString());
        await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath, MortalItemTestFixture.CreateIndex(item).ToJsonString());
        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(bootstrap.Definitions!, files.ReadFileAsync,
            bootstrap.State, bootstrap.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
        await files.WriteFileAtomicAsync(CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        File.WriteAllText(Path.Combine(own, "initial-fixture.json"), JsonSerializer.Serialize(new
        {
            Kind = "current-schema neutral Mortal/item fixture, before production startup",
            InitialItemReceiptTurn = 42, AcceptedGameTurns = 0, ModelCalls = 0,
            Quality = "Rare", FateCards = "empty; existing Rare+ field contract",
            NoBridgeRequiredForLocalCommands = true
        }));
        var package = Path.Combine(own, "package");
        var ship = Path.Combine(own, "ship");
        async Task Run(string executable, string[] arguments, string logName, int seconds)
        {
            var info = new ProcessStartInfo(executable)
            {
                UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true, WorkingDirectory = repo
            };
            foreach (var argument in arguments) info.ArgumentList.Add(argument);
            using var process = Process.Start(info)!;
            var text = await LinuxFallbackSupervisorTests.NativeRun.ObserveBuild(process, TimeSpan.FromSeconds(seconds), Path.Combine(own, logName));
            Assert.True(process.ExitCode == 0, "Browser chain/preparation failure; evidence " + own + Environment.NewLine + text);
        }
        await Run("pwsh", ["-NoLogo", "-NoProfile", "-File", Path.Combine(repo, "scripts/build-linux-supervisor.ps1"),
            "-OutputDirectory", package, "-IncludeHostGuardian"], "native-preparation.log", 20);
#if DEBUG
        const string configuration = "Debug";
#else
        const string configuration = "Release";
#endif
        await Run("dotnet", ["publish", Path.Combine(repo, "BookOfEternityClient/BookOfEternityClient.csproj"), "--no-build", "--no-restore",
            "-c", configuration, "-o", Path.Combine(ship, "BookOfEternityClient"), "-p:BoeNativePackageDirectory=" + package,
            "-p:BoeRequireNativePackage=true"], "publish-client.log", 20);
        var python = Environment.GetEnvironmentVariable("BOE_GAME_CHAIN_BROWSER_PYTHON");
        Assert.False(string.IsNullOrWhiteSpace(python), "Set BOE_GAME_CHAIN_BROWSER_PYTHON to the existing Python with Playwright installed.");
        await Run(Path.Combine(package, "host-guardian"), ["--live-turn", Path.Combine(own, "guardian.json"), "180000",
            python!, Path.Combine(repo, "tests/fixtures/LinuxGameChains/browser_chain.py"), mode, repo, own, ship], "chain.log", 190);
        using var result = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own, "result.json")));
        Assert.True(result.RootElement.GetProperty("PASS").GetBoolean(), result.RootElement.ToString());
        Assert.Equal(0, result.RootElement.GetProperty("ModelCalls").GetInt32());
        Assert.True(result.RootElement.GetProperty("WholeHostBrowserColdRestart").GetBoolean());
        using var guardian = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(own, "guardian.json")));
        Assert.True(guardian.RootElement.GetProperty("echild").GetBoolean());
        Assert.Equal(0, guardian.RootElement.GetProperty("emergencySignals").GetInt32());
        Assert.Equal(0, guardian.RootElement.GetProperty("failures").GetInt32());
        Assert.False(guardian.RootElement.GetProperty("deadline").GetBoolean());
    }
}
