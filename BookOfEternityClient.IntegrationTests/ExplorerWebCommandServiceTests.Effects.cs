using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class ExplorerWebCommandServiceEffectTests : IDisposable
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ExplorerWebCommandService _service;

    public ExplorerWebCommandServiceEffectTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-web-effect-projection-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        var stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(
            _fs,
            stateManager,
            new LocalizationManager { CurrentLanguage = "ru" },
            validation);
    }

    [Fact]
    public async Task ExecuteAsync_EffectsOverviewAndDetailUseOpaqueAcceptedProjection()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["stacking"]!["currentStacks"] = 2;
        await SeedCanonicalEffectsAsync(effect);
        var snapshot = await EffectMechanicsSnapshot.LoadAsync(_fs);
        Assert.True(snapshot.IsAccepted, string.Join("\n", snapshot.Issues.Select(static issue =>
            $"{issue.Code}: {issue.FilePath}")));
        Assert.NotEmpty(snapshot.Effects);

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты"));
        var overviewText = CollectBlockText(overview.Blocks);
        var overviewPayload = JsonSerializer.Serialize(overview, JsonOptions);

        Assert.Equal(CommandExecutionState.Completed, overview.State);
        Assert.Contains("Кровотечение", overviewText, StringComparison.Ordinal);
        Assert.Contains("Рваная рана", overviewText, StringComparison.Ordinal);
        Assert.Contains("2/3", overviewText, StringComparison.Ordinal);
        Assert.Contains("Периодический урон", overviewText, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, overviewPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", overviewPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/", overviewPayload, StringComparison.OrdinalIgnoreCase);

        var detailAction = Assert.Single(overview.Actions, action =>
            action.Label.Contains("Кровотечение", StringComparison.Ordinal));
        Assert.Contains("effect_view_", detailAction.Command, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, detailAction.Command, StringComparison.Ordinal);

        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(detailAction.Command));
        var detailText = CollectBlockText(detail.Blocks);
        var detailPayload = JsonSerializer.Serialize(detail, JsonOptions);

        Assert.Equal(CommandExecutionState.Completed, detail.State);
        Assert.Contains("Рана продолжает отнимать силы", detailText, StringComparison.Ordinal);
        Assert.Contains("Ещё три хода владельца", detailText, StringComparison.Ordinal);
        Assert.Contains("Снятие", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("не лечит", detailText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, detailPayload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_EffectActionRevalidatesAndWritesOnlyPlayerSafeIntent()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(effect);

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты"));
        var detailAction = Assert.Single(overview.Actions, action =>
            action.Id.StartsWith("effects-detail-", StringComparison.Ordinal));
        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(detailAction.Command));
        var effectAction = Assert.Single(detail.Actions, action =>
            action.Id.StartsWith("effects-action-", StringComparison.Ordinal) &&
            action.Label.Contains("Противодействовать", StringComparison.Ordinal));
        var actionPayload = JsonSerializer.Serialize(effectAction, JsonOptions);

        Assert.Contains("effect_action_", effectAction.Command, StringComparison.Ordinal);
        Assert.Contains(
            "не лечит",
            effectAction.Payload!["description"]!.GetValue<string>(),
            StringComparison.OrdinalIgnoreCase);
        Assert.Equal(
            "effect_only",
            effectAction.Payload!["actionScope"]!.GetValue<string>());
        Assert.False(effectAction.Payload!["woundTreatment"]!.GetValue<bool>());
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, actionPayload, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", actionPayload, StringComparison.Ordinal);

        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            effectAction.Command,
            OwnerId: "browser-effect-test",
            OwnerLabel: "Browser effect test"));

        Assert.Equal(CommandExecutionState.RequiresInput, started.State);
        Assert.NotNull(started.InteractiveSession);
        Assert.True(started.InteractiveSession.RequiresLocalUiLock);
        Assert.IsType<UiConfirmationPrompt>(Assert.Single(started.Prompts, prompt =>
            prompt.Id == "confirm_effect_action"));

        var completed = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["confirm_effect_action"] = JsonValue.Create(true)
            },
            OwnerId: "browser-effect-test"));

        Assert.Equal(CommandExecutionState.Completed, completed.State);
        Assert.Null(completed.InteractiveSession);
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));

        var pendingRaw = await _fs.ReadFileAsync("input/pending_player_action.json");
        Assert.False(string.IsNullOrWhiteSpace(pendingRaw));
        var pending = JsonNode.Parse(pendingRaw!)!.AsObject();
        var playerAction = pending["playerAction"]!.GetValue<string>();
        Assert.Contains("Кровотечение", playerAction, StringComparison.Ordinal);
        Assert.Contains("противодейств", playerAction, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, pendingRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", pendingRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_action_", pendingRaw, StringComparison.Ordinal);
        Assert.DoesNotContain("game_state/", pendingRaw, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SubmitPromptSessionAsync_StaleEffectActionFailsClosedWithoutWriting()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(effect);

        var overview = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/effects"));
        var detailAction = Assert.Single(overview.Actions, action =>
            action.Id.StartsWith("effects-detail-", StringComparison.Ordinal));
        var detail = await _service.ExecuteAsync(new ExplorerWebCommandRequest(detailAction.Command));
        Assert.Contains(detail.Actions, action =>
            action.Id.StartsWith("effects-action-", StringComparison.Ordinal));
        var effectAction = detail.Actions.First(action =>
            action.Id.StartsWith("effects-action-", StringComparison.Ordinal));
        var started = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            effectAction.Command,
            OwnerId: "browser-effect-stale",
            OwnerLabel: "Browser stale effect test"));

        await SeedCanonicalEffectsAsync();

        var rejected = await _service.SubmitPromptSessionAsync(new ExplorerPromptSessionSubmitRequest(
            started.InteractiveSession!.SessionId,
            new Dictionary<string, JsonNode?>
            {
                ["confirm_effect_action"] = JsonValue.Create(true)
            },
            OwnerId: "browser-effect-stale"));
        var payload = JsonSerializer.Serialize(rejected, JsonOptions);

        Assert.Equal(CommandExecutionState.Blocked, rejected.State);
        Assert.Null(rejected.InteractiveSession);
        Assert.Contains("действие", CollectBlockText(rejected.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.Contains("недоступ", CollectBlockText(rejected.Blocks), StringComparison.OrdinalIgnoreCase);
        Assert.False(_fs.FileExists("input/pending_player_action.json"));
        Assert.False(_fs.FileExists(LocalUiSessionLockService.LockPath));
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("identity", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_EffectsHiddenAndGmOnlyNeverEnterSerializedResult()
    {
        var hidden = CreateDistinctEffect("effect_hidden_private", "hidden");
        var gmOnly = CreateDistinctEffect("effect_gm_private", "gm_only");
        await SeedCanonicalEffectsAsync(hidden, gmOnly);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты"));
        var payload = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain("PRIVATE EFFECT", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_hidden_private", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_gm_private", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("hidden", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("gm_only", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Actions, action =>
            action.Id.StartsWith("effects-detail-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task BrowserGameScreen_UsesAcceptedVisibleEffectsInsteadOfRawStatusFallback()
    {
        var visible = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var hidden = CreateDistinctEffect("effect_hidden_game_screen", "gm_only");
        await SeedCanonicalEffectsAsync(visible, hidden);
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "currentCondition": "Собран",
          "healthPercentage": "90%",
          "energyPercentage": "75%",
          "poisePercentage": "80%",
          "activeConditions": [
            "PRIVATE EFFECT effect_hidden_game_screen",
            "PRIVATE STATUS FALLBACK"
          ]
        }
        """);

        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, "http://127.0.0.1:18787"));
        var screen = await app.Services
            .GetRequiredService<BrowserGameScreenService>()
            .BuildAsync();
        Assert.Contains(screen.Player.ActiveConditions, static condition =>
            condition.Contains("Кровотечение", StringComparison.Ordinal));
        var payload = JsonSerializer.Serialize(screen, JsonOptions);

        Assert.DoesNotContain("PRIVATE EFFECT", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE STATUS FALLBACK", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_hidden_game_screen", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("gm_only", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("game_state/", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BrowserGameScreen_MalformedEffectAuthorityShowsSafeUnavailableStatus()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(effect);
        await _fs.WriteFileAtomicAsync(
            "game_state/effects/effect_identity_index.json",
            "{\"schemaVersion\":1,\"entries\":[]}");
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "currentCondition": "Собран",
          "activeConditions": ["PRIVATE RAW STATUS CONDITION"]
        }
        """);

        await using var app = LocalWebUiHost.Build(
            Array.Empty<string>(),
            new LocalWebUiHostOptions(_rootPath, "http://127.0.0.1:18787"));
        var screen = await app.Services
            .GetRequiredService<BrowserGameScreenService>()
            .BuildAsync();

        Assert.Equal(
            [EffectPlayerProjection.UnavailableMessage],
            screen.Player.ActiveConditions);
        var payload = JsonSerializer.Serialize(screen, JsonOptions);
        Assert.DoesNotContain("PRIVATE RAW STATUS CONDITION", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("identity", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_StatusUsesAcceptedEffectsInsteadOfRawActiveConditions()
    {
        var visible = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var hidden = CreateDistinctEffect("effect_hidden_status", "gm_only");
        await SeedCanonicalEffectsAsync(visible, hidden);
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "currentCondition": "Собран",
          "activeConditions": ["PRIVATE RAW STATUS CONDITION"]
        }
        """);

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/status"));
        var text = CollectBlockText(result.Blocks);
        var payload = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Contains("Кровотечение", text, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE RAW STATUS CONDITION", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("PRIVATE EFFECT", payload, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_EffectsMalformedAuthorityIsWholeProjectionUnavailable()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(effect);
        await _fs.WriteFileAtomicAsync(
            "game_state/effects/effect_identity_index.json",
            "{\"schemaVersion\":1,\"entries\":[]}");

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/эффекты"));
        var text = CollectBlockText(result.Blocks);
        var payload = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Сейчас невозможно надёжно определить действующие эффекты", text, StringComparison.Ordinal);
        Assert.DoesNotContain("Кровотечение", text, StringComparison.Ordinal);
        Assert.DoesNotContain("effect_test_bleeding", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("identity", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("validation", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(result.Actions, action =>
            action.Id.StartsWith("effects-detail-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ExecuteAsync_EffectDetailRejectsPermanentIdAndForgedSelector()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        await SeedCanonicalEffectsAsync(effect);

        var permanent = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/эффекты эффект " + EffectMaterializationTestFixture.EffectId));
        var forged = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/эффекты эффект effect_view_000000000000000000000000"));

        Assert.Contains("Такой эффект не найден", CollectBlockText(permanent.Blocks), StringComparison.Ordinal);
        Assert.Contains("Такой эффект не найден", CollectBlockText(forged.Blocks), StringComparison.Ordinal);
        Assert.DoesNotContain("Кровотечение", CollectBlockText(permanent.Blocks), StringComparison.Ordinal);
        Assert.DoesNotContain("Кровотечение", CollectBlockText(forged.Blocks), StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_NpcMechanicsUsesAcceptedEffectProjection()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("npc");
        await SeedMortalRealmAsync();
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "NPCsInScene": [
            {
              "NPCId": "npc_test_healer",
              "name": "Лекарь Иара",
              "shortDescription": "Полевой лекарь."
            }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync(
            "game_state/npcs/npc_effects.json",
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["NPCId"] = "npc_test_healer",
                    ["activeEffects"] = new JsonArray(effect.DeepClone())
                })
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            "game_state/effects/effect_identity_index.json",
            CreateDistinctIdentityIndex([effect]).ToJsonString());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest(
            "/npc section npc_test_healer mechanics"));
        var text = CollectBlockText(result.Blocks);
        var payload = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Лекарь Иара", text, StringComparison.Ordinal);
        Assert.Contains("Кровотечение", text, StringComparison.Ordinal);
        Assert.Contains("Периодический урон", text, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("wound_test_torn_side", payload, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_CombatantCardsUseAcceptedEffectProjection()
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect("combatant");
        await SeedMortalRealmAsync();
        await _fs.WriteFileAtomicAsync(
            "game_state/combat/enemies.json",
            new JsonObject
            {
                ["enemiesData"] = new JsonArray(new JsonObject
                {
                    ["combatantId"] = EffectMaterializationTestFixture.CombatantId,
                    ["name"] = "Пепельный налётчик",
                    ["currentHealth"] = 27,
                    ["maxHealth"] = 40,
                    ["activeBuffs"] = new JsonArray(),
                    ["activeDebuffs"] = new JsonArray(effect.DeepClone())
                })
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            "game_state/effects/effect_identity_index.json",
            CreateDistinctIdentityIndex([effect]).ToJsonString());

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/бой"));
        var text = CollectBlockText(result.Blocks);
        var payload = JsonSerializer.Serialize(result, JsonOptions);

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.Contains("Пепельный налётчик", text, StringComparison.Ordinal);
        Assert.Contains("Кровотечение", text, StringComparison.Ordinal);
        Assert.DoesNotContain(EffectMaterializationTestFixture.EffectId, payload, StringComparison.Ordinal);
        Assert.DoesNotContain("activeDebuffs", payload, StringComparison.Ordinal);
    }

    private async Task SeedCanonicalEffectsAsync(params JsonObject[] effects)
    {
        await SeedMortalRealmAsync();

        var entries = new JsonArray();
        foreach (var effect in effects)
            entries.Add(effect.DeepClone());
        await _fs.WriteFileAtomicAsync(
            "game_state/player/effects.json",
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = entries
            }.ToJsonString());
        await _fs.WriteFileAtomicAsync(
            "game_state/effects/effect_identity_index.json",
            CreateDistinctIdentityIndex(effects).ToJsonString());
    }

    private async Task SeedMortalRealmAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая душа",
          "currentRealm": "Mortal Realm",
          "currentIncarnation": 1
        }
        """);
    }

    private static JsonObject CreateDistinctIdentityIndex(IReadOnlyList<JsonObject> effects)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
        var entries = index["entries"]!.AsArray();
        for (var i = 0; i < effects.Count; i++)
        {
            var chronology = effects[i]["chronology"]!.AsObject();
            var transition = entries[i]!["transitions"]![0]!.AsObject();
            transition["transitionId"] = chronology["lastTransitionId"]!.GetValue<string>();
            transition["eventRef"] = chronology["createdEventRef"]!.GetValue<string>();
        }
        return index;
    }

    private static JsonObject CreateDistinctEffect(string effectId, string visibility)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = effectId;
        effect["source"]!["sourceId"] = "wound_" + effectId;
        effect["stacking"]!["stackKey"] = "stack_" + effectId;
        effect["chronology"]!["lastTransitionId"] = "transition_" + effectId;
        effect["chronology"]!["createdEventRef"] = "turn_42:" + effectId;
        effect["display"]!["name"] = "PRIVATE EFFECT " + effectId;
        effect["display"]!["description"] = "PRIVATE EFFECT DESCRIPTION";
        effect["display"]!["sourceLabel"] = "PRIVATE EFFECT SOURCE";
        effect["display"]!["visibility"] = visibility;
        return effect;
    }

    private static string CollectBlockText(IEnumerable<UiBlock> blocks)
    {
        var parts = new List<string>();
        foreach (var block in blocks)
            CollectBlockText(block, parts);
        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(static item => new[] { item.Key, item.Value }));
                break;
            case UiTableBlock table:
                parts.Add(table.Title);
                parts.AddRange(table.Rows.SelectMany(static row => row.Cells));
                break;
            case UiPanelBlock panel:
                parts.Add(panel.Title);
                foreach (var child in panel.Blocks)
                    CollectBlockText(child, parts);
                break;
            case UiEntityDossierBlock dossier:
                parts.Add(dossier.Title);
                parts.Add(dossier.Subtitle);
                parts.Add(dossier.Summary);
                parts.AddRange(dossier.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
                parts.AddRange(dossier.Metrics.SelectMany(static metric => new[]
                    { metric.Label, $"{metric.Value:0.##}/{metric.Max:0.##}", metric.Note }));
                parts.AddRange(dossier.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
                parts.AddRange(dossier.List);
                foreach (var card in dossier.Cards)
                    CollectCardText(card, parts);
                foreach (var section in dossier.Sections)
                {
                    parts.Add(section.Title);
                    parts.Add(section.Summary);
                    parts.AddRange(section.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
                    parts.AddRange(section.Metrics.SelectMany(static metric => new[]
                        { metric.Label, $"{metric.Value:0.##}/{metric.Max:0.##}", metric.Note }));
                    parts.AddRange(section.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
                    parts.AddRange(section.List);
                    foreach (var card in section.Cards)
                        CollectCardText(card, parts);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, parts);
                }
                break;
        }
    }

    private static void CollectCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Facts.SelectMany(static fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(card.Metrics.SelectMany(static metric => new[]
            { metric.Label, $"{metric.Value:0.##}/{metric.Max:0.##}", metric.Note }));
        parts.AddRange(card.Hints.SelectMany(static hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(card.List);
        foreach (var nested in card.Nested)
            CollectCardText(nested, parts);
        foreach (var nested in card.Cards)
            CollectCardText(nested, parts);
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
