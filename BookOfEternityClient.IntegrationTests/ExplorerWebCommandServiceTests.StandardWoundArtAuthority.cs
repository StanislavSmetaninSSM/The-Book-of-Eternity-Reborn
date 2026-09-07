using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json.Nodes;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWebCommandServiceTests
{
    public static IEnumerable<object[]> StandardWoundArtInvalidBrowserAuthorityCases()
    {
        foreach (var artId in AfterlifeSpiritualConflictState.RequiredWoundArtIds)
        {
            foreach (var mutation in new[] { "missing_leaf", "null_leaf", "string_leaf", "object_leaf" })
                yield return [artId, mutation, $"afterlifeCombatProfile.artTiers.{artId}"];

            var sibling = artId == AfterlifeSpiritualConflictState.SpiritualResilienceArtId
                ? AfterlifeSpiritualConflictState.SpiritualHealingArtId
                : AfterlifeSpiritualConflictState.SpiritualResilienceArtId;
            yield return [artId, "invalid_sibling", $"afterlifeCombatProfile.artTiers.{sibling}"];
        }

        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "null_profile", "afterlifeCombatProfile"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "scalar_profile", "afterlifeCombatProfile"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "null_map", "afterlifeCombatProfile.artTiers"];
        yield return [AfterlifeSpiritualConflictState.SpiritualResilienceArtId, "array_map", "afterlifeCombatProfile.artTiers"];
        yield return ["pressure", "invalid_healing_for_old_art", "afterlifeCombatProfile.artTiers.spiritual_healing"];
    }

    [Theory]
    [MemberData(nameof(StandardWoundArtInvalidBrowserAuthorityCases))]
    public async Task StandardWoundArt_InvalidPlayerAuthority_BrowserSubmissionRejectsWithoutMutationAndReleasesLock(
        string selectedArtId,
        string mutation,
        string expectedContext)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString());
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 2000, ["total"] = 2000 };
        PrepareValidRequiredWoundArts(soul);
        MutateStandardWoundArtAuthority(soul, selectedArtId, mutation);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString());

        var watchedPaths = new[]
        {
            "game_state/meta/soul_state.json",
            ShiningAbodeState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath
        };
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-wound-authority",
            OwnerLabel: "Browser wound authority"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        var before = new Dictionary<string, string?>();
        foreach (var path in watchedPaths)
            before[path] = await _fs.ReadFileAsync(path);

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["upgrade_target"] = JsonValue.Create(selectedArtId),
                ["upgrade_currency"] = JsonValue.Create("ink_feathers")
            },
            OwnerId: "browser-wound-authority"));

        Assert.Equal(CommandExecutionState.Failed, completed.State);
        Assert.Contains(expectedContext, CollectBlockText(completed.Blocks), StringComparison.Ordinal);
        foreach (var path in watchedPaths)
            Assert.Equal(before[path], await _fs.ReadFileAsync(path));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task StandardWoundArt_MissingOptionalOldArt_BrowserSubmissionStillPurchasesRequiredArt()
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString());
        var soul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        soul["inkFeathers"] = new JsonObject { ["current"] = 600, ["total"] = 600 };
        var profile = Assert.IsType<JsonObject>(soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
        profile["artTiers"] = AfterlifeSpiritualConflictState.CreateDefaultArtTiers();
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", soul.ToJsonString());

        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-old-optional-control",
            OwnerLabel: "Browser old optional control"));
        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["upgrade_target"] = JsonValue.Create(AfterlifeSpiritualConflictState.SpiritualResilienceArtId),
                ["upgrade_currency"] = JsonValue.Create("ink_feathers")
            },
            OwnerId: "browser-old-optional-control"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        var updated = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        Assert.Equal(1, updated["afterlifeCombatProfile"]!["artTiers"]![AfterlifeSpiritualConflictState.SpiritualResilienceArtId]!.GetValue<int>());
        Assert.Equal(100, updated["inkFeathers"]!["current"]!.GetValue<int>());
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("malformed_json")]
    [InlineData("array_root")]
    [InlineData("duplicate_profile_key")]
    public async Task StandardWoundArt_UnreadableFreshSoulAuthority_BrowserSubmissionFailsAndReleasesLock(
        string mutation)
    {
        await SeedAfterlifeCombatAndEntityFilesAsync();
        await _fs.WriteFileAtomicAsync(
            AfterlifeSpiritualConflictState.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeConflict"] = null,
                ["recentConflicts"] = new JsonArray()
            }.ToJsonString());

        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/spiritual_arts",
            OwnerId: "browser-unreadable-soul-authority",
            OwnerLabel: "Browser unreadable soul authority"));
        Assert.Equal(CommandExecutionState.RequiresInput, started.State);

        var watchedPaths = new[]
        {
            ShiningAbodeState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath
        };
        var before = new Dictionary<string, string?>();
        foreach (var path in watchedPaths)
            before[path] = await _fs.ReadFileAsync(path);

        string? expectedSoul;
        switch (mutation)
        {
            case "missing":
                _fs.DeleteFile("game_state/meta/soul_state.json");
                expectedSoul = null;
                break;
            case "malformed_json":
                expectedSoul = "{";
                await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", expectedSoul);
                break;
            case "array_root":
                expectedSoul = "[]";
                await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", expectedSoul);
                break;
            case "duplicate_profile_key":
                expectedSoul = """
                    {
                      "afterlifeCombatProfile": { "artTiers": { "spiritual_resilience": 0, "spiritual_healing": 0 } },
                      "afterlifeCombatProfile": { "artTiers": { "spiritual_resilience": 0, "spiritual_healing": 0 } }
                    }
                    """;
                await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", expectedSoul);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        ExplorerCommandResult? completed = null;
        var exception = await Record.ExceptionAsync(async () =>
        {
            completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
                started.InteractiveSession!.SessionId,
                new Dictionary<string, JsonNode?>
                {
                    ["upgrade_target"] = JsonValue.Create(AfterlifeSpiritualConflictState.SpiritualResilienceArtId),
                    ["upgrade_currency"] = JsonValue.Create("ink_feathers")
                },
                OwnerId: "browser-unreadable-soul-authority"));
        });

        Assert.Null(exception);
        Assert.NotNull(completed);
        Assert.Equal(CommandExecutionState.Failed, completed.State);
        Assert.Contains("состояние души", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Equal(expectedSoul, await _fs.ReadFileAsync("game_state/meta/soul_state.json"));
        foreach (var path in watchedPaths)
            Assert.Equal(before[path], await _fs.ReadFileAsync(path));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
    }

    [Fact]
    public async Task StandardWoundArt_FreshSoulReadIOException_BrowserSubmissionFailsAndReleasesLock()
    {
        var caseRoot = _seedFixture.CreateIsolatedCaseRoot();
        try
        {
            var rejectSoulReads = false;
            var hooks = new FileSystemManagerHooks
            {
                BeforeCanonicalReadOpenAsync = path =>
                {
                    if (rejectSoulReads &&
                        string.Equals(path, "game_state/meta/soul_state.json", StringComparison.OrdinalIgnoreCase))
                    {
                        throw new IOException("simulated soul read failure");
                    }

                    return Task.CompletedTask;
                }
            };
            var fs = new FileSystemManager(
                caseRoot,
                NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance,
                hooks);
            var resourceBootstrap = ResourceBootstrapStateBuilder.BuildPristine();
            Assert.True(resourceBootstrap.IsValid, string.Join(Environment.NewLine, resourceBootstrap.Issues));
            await fs.WriteFileAtomicAsync(
                ResourceMaterializationContract.DefinitionsPath,
                resourceBootstrap.Definitions!.ToCanonicalJson());
            await fs.WriteFileAtomicAsync(
                ResourceMaterializationContract.StatePath,
                resourceBootstrap.State!.ToCanonicalJson());
            await fs.WriteFileAtomicAsync(
                ResourceMaterializationContract.HistoryPath,
                resourceBootstrap.History!.ToCanonicalJson());
            await fs.WriteFileAtomicAsync(
                "game_state/meta/soul_state.json",
                new JsonObject
                {
                    ["currentRealm"] = "Chaos Sea",
                    ["inkFeathers"] = new JsonObject { ["current"] = 1000, ["total"] = 1000 },
                    [AfterlifeSpiritualConflictState.SoulStateProfileProperty] =
                        AfterlifeSpiritualConflictState.CreateDefaultCombatProfile()
                }.ToJsonString());
            await fs.WriteFileAtomicAsync(
                AfterlifeSpiritualConflictState.StatePath,
                AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());

            var stateManager = new StateManager(
                fs,
                new GameSettings(),
                NullLogger<StateManager>.Instance);
            var service = new ExplorerWebCommandService(
                fs,
                stateManager,
                new LocalizationManager(),
                new ValidationService(fs, NullLogger<ValidationService>.Instance));
            var started = await service.ExecuteAsync(new ExplorerWebCommandRequest(
                "/spiritual_arts",
                OwnerId: "browser-soul-io-failure",
                OwnerLabel: "Browser soul IO failure"));
            Assert.Equal(CommandExecutionState.RequiresInput, started.State);
            rejectSoulReads = true;

            ExplorerCommandResult? completed = null;
            var exception = await Record.ExceptionAsync(async () =>
            {
                completed = await service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
                    started.InteractiveSession!.SessionId,
                    new Dictionary<string, JsonNode?>
                    {
                        ["upgrade_target"] = JsonValue.Create(AfterlifeSpiritualConflictState.SpiritualResilienceArtId),
                        ["upgrade_currency"] = JsonValue.Create("ink_feathers")
                    },
                    OwnerId: "browser-soul-io-failure"));
            });

            Assert.Null(exception);
            Assert.NotNull(completed);
            Assert.Equal(CommandExecutionState.Failed, completed.State);
            Assert.Contains("состояние души", CollectBlockText(completed.Blocks), StringComparison.OrdinalIgnoreCase);
            Assert.False(fs.FileExists(LocalUiSessionLockService.LockPath));
        }
        finally
        {
            if (Directory.Exists(caseRoot))
                Directory.Delete(caseRoot, recursive: true);
        }
    }

    private static void PrepareValidRequiredWoundArts(JsonObject soul)
    {
        var profile = Assert.IsType<JsonObject>(soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
        var arts = profile["artTiers"] as JsonObject ?? new JsonObject();
        arts[AfterlifeSpiritualConflictState.SpiritualResilienceArtId] = 0;
        arts[AfterlifeSpiritualConflictState.SpiritualHealingArtId] = 0;
        arts["pressure"] = 0;
        profile["artTiers"] = arts;
    }

    private static void MutateStandardWoundArtAuthority(JsonObject soul, string selectedArtId, string mutation)
    {
        if (mutation == "null_profile")
        {
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = null;
            return;
        }

        if (mutation == "scalar_profile")
        {
            soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty] = 1;
            return;
        }

        var profile = Assert.IsType<JsonObject>(soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]);
        if (mutation == "null_map")
        {
            profile["artTiers"] = null;
            return;
        }

        if (mutation == "array_map")
        {
            profile["artTiers"] = new JsonArray();
            return;
        }

        var arts = Assert.IsType<JsonObject>(profile["artTiers"]);
        var targetId = mutation switch
        {
            "invalid_sibling" when selectedArtId == AfterlifeSpiritualConflictState.SpiritualResilienceArtId =>
                AfterlifeSpiritualConflictState.SpiritualHealingArtId,
            "invalid_sibling" => AfterlifeSpiritualConflictState.SpiritualResilienceArtId,
            "invalid_healing_for_old_art" => AfterlifeSpiritualConflictState.SpiritualHealingArtId,
            _ => selectedArtId
        };
        switch (mutation)
        {
            case "missing_leaf":
                arts.Remove(targetId);
                break;
            case "null_leaf":
                arts[targetId] = null;
                break;
            case "string_leaf":
                arts[targetId] = "1";
                break;
            case "object_leaf":
            case "invalid_sibling":
            case "invalid_healing_for_old_art":
                arts[targetId] = new JsonObject { ["tier"] = 1, ["experience"] = 0 };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
    }
}
