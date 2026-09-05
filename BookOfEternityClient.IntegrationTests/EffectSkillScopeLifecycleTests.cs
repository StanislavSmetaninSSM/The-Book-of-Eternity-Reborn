using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectSkillScopeLifecycleTests : IAsyncDisposable
{
    private const string ActiveSkillsPath = "game_state/player/skills_active.json";
    private const string NpcCorePath = "game_state/npcs/npc_core.json";
    private readonly string _rootPath;
    private readonly FileSystemManager _fileSystem;

    public EffectSkillScopeLifecycleTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-effect-skill-scope-" + Guid.NewGuid().ToString("N"));
        _fileSystem = new FileSystemManager(
            _rootPath,
            NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance);
        _fileSystem.EnsureDirectoryStructure();
    }

    [Fact]
    public async Task TurnRequestCatalog_LiveHelperPublishesExactPlayerAndNpcUsableCatalog()
    {
        await SeedScopedSkillsAsync(_fileSystem);

        await new LiveTurnPreparationService(_fileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "scope-live",
            RequestId = "scope-live-request",
            TurnNumber = 42,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть рану.",
            PreGeneratedDices1d20 = new[] { 8 }
        });

        var request = await ReadTurnRequestAsync();
        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(CreateExpectedCatalog(), catalog));
    }

    [Fact]
    public async Task TurnRequestCatalog_UnrelatedMalformedEffectCarrierDoesNotSuppressValidCatalog()
    {
        await SeedScopedSkillsAsync(_fileSystem);
        await _fileSystem.WriteFileAtomicAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            new JsonArray(new JsonObject
            {
                ["legacyEffect"] = "unrelated malformed carrier"
            }).ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));

        await new LiveTurnPreparationService(_fileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "scope-live-malformed-carrier",
            RequestId = "scope-live-malformed-carrier-request",
            TurnNumber = 44,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть рану при повреждённом носителе эффекта.",
            PreGeneratedDices1d20 = new[] { 10 }
        });

        var request = await ReadTurnRequestAsync();
        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(CreateExpectedCatalog(), catalog));
    }

    [Fact]
    public async Task TurnRequestCatalog_MalformedSkillRootFailsClosedToExplicitEmptyCatalog()
    {
        await SeedScopedSkillsAsync(_fileSystem);
        await _fileSystem.WriteFileAtomicAsync(ActiveSkillsPath, "{ malformed skill root");

        await new LiveTurnPreparationService(_fileSystem).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "scope-live-malformed-skills",
            RequestId = "scope-live-malformed-skills-request",
            TurnNumber = 45,
            CurrentRealm = "Mortal World",
            PlayerAction = "Осмотреть недоступный каталог навыков.",
            PreGeneratedDices1d20 = new[] { 11 }
        });

        var request = await ReadTurnRequestAsync();
        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["targets"] = new JsonArray()
        }, catalog));
    }

    [Fact]
    public void TurnRequestCatalog_AfterlifeOnlyDirectRequestUsesExplicitEmptyCatalog()
    {
        var request = JsonSerializer.SerializeToNode(
            new TurnRequest { PlayerAction = "Продолжить духовный путь." },
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();

        var catalog = Assert.IsType<JsonObject>(request["effectSkillScopeCatalog"]);

        Assert.True(JsonNode.DeepEquals(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["targets"] = new JsonArray()
        }, catalog));
    }

    [Fact]
    public async Task TurnRequestCatalog_EditedRequestCatalogDoesNotChangeCanonicalScopeAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var definition = EffectMaterializationTestFixture.CreateDefinition(profile: "roll_modifier");
        definition["components"]![0]!["payload"]!["operations"] = new JsonArray("skill_check");
        definition["components"]![0]!["payload"]!["scope"] = new JsonObject
        {
            ["kind"] = "skill",
            ["skillId"] = "skill_forged"
        };
        await context.SeedPlayerSkillSourceAsync(definition);
        await context.CaptureValidatedPendingSnapshotAsync();

        var request = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            LiveTurnPreparationService.TurnRequestPath));
        request["effectSkillScopeCatalog"] = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["targets"] = new JsonArray(new JsonObject
            {
                ["realm"] = "mortal_world",
                ["kind"] = "player",
                ["targetId"] = "player_current",
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_forged",
                    ["displayName"] = "Подмена"
                })
            })
        };
        await context.WriteJsonAsync(
            LiveTurnPreparationService.TurnRequestPath,
            request);
        var command = EffectMaterializationTestFixture.CreateApplyCommand();
        command["source"] = new JsonObject
        {
            ["kind"] = "skill",
            ["sourceId"] = EffectMaterializationTestContext.MaterializableSkillId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));
        var before = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        var issues = await context.Validator.ValidateAcceptedTurnRawEffectMaterializationAsync();
        var after = await context.CaptureBytesAsync(
            EffectMaterializationTestContext.PlayerEffectsPath,
            EffectMaterializationTestContext.IdentityIndexPath,
            EffectMaterializationTestContext.CommandPath);

        Assert.Contains(
            issues,
            issue => issue.Code == "effect_roll_skill_scope_unavailable");
        Assert.Equal(before, after);
        Assert.Null(await AcceptedMechanicsAuthorityTestProbe.PeekEffectAsync(context.FileSystem));
    }

    public ValueTask DisposeAsync()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
        return ValueTask.CompletedTask;
    }

    internal static async Task SeedScopedSkillsAsync(FileSystemManager fileSystem)
    {
        await fileSystem.WriteFileAtomicAsync(ActiveSkillsPath, new JsonObject
        {
            ["activeSkillChanges"] = new JsonArray(
                Skill("skill_lockpicking", "Взлом", active: true),
                Skill("skill_inactive", "Спящий", active: false),
                new JsonObject { ["skillName"] = "Без идентификатора" },
                Skill("skill_duplicate", "Первый дубликат", active: true),
                Skill("skill_duplicate", "Второй дубликат", active: true),
                Skill("skill_confusable", "Первый похожий", active: true),
                Skill("SKILL_CONFUSABLE", "Второй похожий", active: true))
        }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await fileSystem.WriteFileAtomicAsync(NpcCorePath, new JsonObject
        {
            ["UpdateNPCs"] = new JsonArray(new JsonObject
            {
                ["NPCId"] = "npc_healer",
                ["activeSkills"] = new JsonArray(
                    Skill("skill_medicine", "Медицина", active: true),
                    Skill("skill_npc_inactive", "Недоступный", active: false))
            })
        }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task<JsonObject> ReadTurnRequestAsync() =>
        (JsonNode.Parse(await _fileSystem.ReadFileAsync(LiveTurnPreparationService.TurnRequestPath)
            ?? throw new InvalidOperationException("turn_request.json is missing.")) as JsonObject)
        ?? throw new InvalidOperationException("turn_request.json is not an object.");

    private static JsonObject Skill(string skillId, string skillName, bool active) => new()
    {
        ["skillId"] = skillId,
        ["skillName"] = skillName,
        ["lifecycle"] = active ? "active" : "inactive",
        ["active"] = active
    };

    internal static JsonObject CreateExpectedCatalog() => new()
    {
        ["schemaVersion"] = 1,
        ["targets"] = new JsonArray(
            new JsonObject
            {
                ["realm"] = "mortal_world",
                ["kind"] = "npc",
                ["targetId"] = "npc_healer",
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_medicine",
                    ["displayName"] = "Медицина"
                })
            },
            new JsonObject
            {
                ["realm"] = "mortal_world",
                ["kind"] = "player",
                ["targetId"] = "player_current",
                ["skills"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_lockpicking",
                    ["displayName"] = "Взлом"
                })
            })
    };

}

public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public async Task EffectSkillScopeLifecycleTests_TurnRequestCatalog_OrdinaryGameEnginePublishesCanonicalCatalog()
    {
        CopyDirectory(TestRepoPaths.BaseSessionRoot, _fs.GameSessionPath);
        await WriteOrdinaryTurnPlayerSkillAsync();
        var preflightIssues = await new ValidationService(
            _fs,
            NullLogger<ValidationService>.Instance).ValidateGameStateAsync();
        Assert.True(
            preflightIssues.Count == 0,
            string.Join(Environment.NewLine, preflightIssues.Select(static issue => issue.ToString())));
        var engine = CreateGameEngine(new QueuedConsoleInputSource([Key(ConsoleKey.Enter)]));
        var capturedCatalog = new TaskCompletionSource<JsonObject?>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var terminalError = Task.Run(async () =>
        {
            var request = await WaitForTurnRequestAsync();
            var root = JsonNode.Parse(await _fs.ReadFileAsync("input/turn_request.json")
                ?? throw new InvalidOperationException("turn_request.json is missing."))!.AsObject();
            capturedCatalog.TrySetResult(
                root["effectSkillScopeCatalog"] is JsonObject requestedCatalog
                    ? requestedCatalog.DeepClone().AsObject()
                    : null);
            await _fs.WriteFileAtomicAsync("ready/turn_error.json", JsonSerializer.Serialize(new
            {
                sessionId = request.SessionId,
                requestId = request.RequestId,
                turnNumber = request.TurnNumber,
                timestamp = DateTime.UtcNow.ToString("o"),
                status = "error",
                error = "Test terminal cleanup."
            }, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        });

        var processTurn = InvokePrivateTaskAsync(
            engine,
            "ProcessPlayerTurn",
            "Осмотреть рану.",
            null);
        var catalog = await capturedCatalog.Task.WaitAsync(TimeSpan.FromSeconds(35));

        await processTurn;
        await terminalError.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.NotNull(catalog);
        Assert.True(JsonNode.DeepEquals(CreatePlayerOnlyCatalog(), catalog));
    }

    private async Task WriteOrdinaryTurnPlayerSkillAsync()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/player/skills_active.json",
            new JsonObject
            {
                ["activeSkillChanges"] = new JsonArray(new JsonObject
                {
                    ["skillId"] = "skill_lockpicking",
                    ["skillName"] = "Взлом",
                    ["skillDescription"] = "Открывает сложные механизмы.",
                    ["rarity"] = "Common",
                    ["combatEffect"] = new JsonObject
                    {
                        ["actionName"] = "Взлом",
                        ["actionCost"] = "Main",
                        ["effects"] = new JsonArray(new JsonObject
                        {
                            ["effectType"] = "Damage",
                            ["targetType"] = "Enemy",
                            ["effectDescription"] = "Преодолевает сопротивление механизма.",
                            ["value"] = "45%",
                            ["poiseDamage"] = "0%"
                        }),
                        ["isActivatedEffect"] = true
                    },
                    ["scalingCharacteristic"] = "Intelligence"
                })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        await _fs.WriteFileAtomicAsync(
            "game_state/player/skill_mastery.json",
            new JsonObject
            {
                ["skillMasteryChanges"] = new JsonArray(new JsonObject
                {
                    ["skillName"] = "Взлом",
                    ["newMasteryLevel"] = 1,
                    ["newCurrentMasteryProgress"] = 0,
                    ["newMasteryProgressNeeded"] = 100,
                    ["masteryLeveledUp"] = false
                })
            }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static JsonObject CreatePlayerOnlyCatalog() => new()
    {
        ["schemaVersion"] = 1,
        ["targets"] = new JsonArray(new JsonObject
        {
            ["realm"] = "mortal_world",
            ["kind"] = "player",
            ["targetId"] = "player_current",
            ["skills"] = new JsonArray(new JsonObject
            {
                ["skillId"] = "skill_lockpicking",
                ["displayName"] = "Взлом"
            })
        })
    };
}
