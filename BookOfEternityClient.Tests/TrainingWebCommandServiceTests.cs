using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class TrainingWebCommandServiceTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;
    private readonly ExplorerWebCommandService _service;

    public TrainingWebCommandServiceTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-training-web-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var validation = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
        _service = new ExplorerWebCommandService(_fs, _stateManager, new LocalizationManager(), validation);
    }

    [Fact]
    public async Task ExecuteAsync_TrainingMortalOverview_RendersNestedTeacherAndOfferCards()
    {
        await SeedMortalTrainingAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/training"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiTableBlock or UiRawJsonBlock);
        var dossier = Assert.Single(result.Blocks.OfType<UiEntityDossierBlock>(), block => block.EntityType == "training-showcase");
        Assert.Equal("training-showcase", dossier.EntityType);
        Assert.Contains("обучен", dossier.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(dossier.Sections, section =>
            section.Presentation == "collection" &&
            section.Cards.Any(card =>
                card.Title.Contains("Рейна Быстрый Нож", StringComparison.OrdinalIgnoreCase) &&
                card.Nested.Any(nested => nested.Title.Contains("Ножевой бой", StringComparison.OrdinalIgnoreCase))));

        var text = CollectText(result);
        Assert.Contains("Деньги", text, StringComparison.Ordinal);
        Assert.Contains("Опыт текущего уровня", text, StringComparison.Ordinal);
        Assert.Contains("Предел учителя", text, StringComparison.Ordinal);
        Assert.Contains("Требование", text, StringComparison.Ordinal);
        Assert.DoesNotContain("currentLevelExperiencePercent", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sourceActorSnapshotHash", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Купить", StringComparison.OrdinalIgnoreCase) &&
            action.Command.Contains("buy", StringComparison.OrdinalIgnoreCase) &&
            action.RequiresConfirmation);
    }

    [Fact]
    public async Task ExecuteAsync_TrainingBuyCommand_SpendsResourcesAndReturnsReadableReceipt()
    {
        await SeedMortalTrainingAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/training buy npc_teacher_reina offer_knife_mastery_2"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        var text = CollectText(result);
        Assert.Contains("Обучение завершено", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Ножевой бой", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trainingPurchaseReceipts", text, StringComparison.OrdinalIgnoreCase);

        using var status = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/core/player_status.json") ?? "{}");
        Assert.Equal(380, status.RootElement.GetProperty("money").GetInt32());
        using var experience = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/player/experience.json") ?? "{}");
        Assert.Equal(640, experience.RootElement.GetProperty("currentLevelExperience").GetInt32());
        using var mastery = JsonDocument.Parse(await _fs.ReadFileAsync("game_state/player/skill_mastery.json") ?? "{}");
        Assert.Contains(mastery.RootElement.GetProperty("skillMasteryChanges").EnumerateArray(), entry =>
            entry.GetProperty("skillId").GetString() == "knife" &&
            entry.GetProperty("newMasteryLevel").GetInt32() == 2);
    }

    [Fact]
    public async Task ExecuteAsync_TrainingAfterlifeOverview_RendersMentorAndSelfFallbackOffers()
    {
        await SeedAfterlifeTrainingAsync();

        var result = await _service.ExecuteAsync(new ExplorerWebCommandRequest("/обучение"));

        Assert.Equal(CommandExecutionState.Completed, result.State);
        Assert.DoesNotContain(result.Blocks, static block => block is UiTableBlock or UiRawJsonBlock);
        var dossier = Assert.Single(result.Blocks.OfType<UiEntityDossierBlock>(), block => block.EntityType == "training-showcase");
        Assert.Equal("training-showcase", dossier.EntityType);

        var text = CollectText(result);
        Assert.Contains("Наставники", text, StringComparison.Ordinal);
        Assert.Contains("Архонт Лиора", text, StringComparison.Ordinal);
        Assert.Contains("Самостоятельная прокачка", text, StringComparison.Ordinal);
        Assert.Contains("400%", text, StringComparison.Ordinal);
        Assert.Contains("Чернильные Перья", text, StringComparison.Ordinal);
        Assert.DoesNotContain("mentorTrainingShowcase", text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallbackMultiplierPercent", text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(result.Actions, action =>
            action.Label.Contains("Наставник", StringComparison.OrdinalIgnoreCase) &&
            action.Command.Contains("guardian_liora", StringComparison.OrdinalIgnoreCase));
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private async Task SeedMortalTrainingAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Eternia",
          "turnNumber": 7
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/core/player_status.json", """
        {
          "money": 500
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/experience.json", """
        {
          "level": 3,
          "currentLevelExperience": 760,
          "experienceForNextLevel": 800
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skills_active.json", """
        {
          "activeSkillChanges": [
            { "skillId": "knife", "skillName": "Ножевой бой", "skillDescription": "Быстрые удары коротким клинком." }
          ]
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skills_passive.json", """
        {
          "passiveSkillChanges": []
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """
        {
          "skillMasteryChanges": [
            { "skillId": "knife", "skillName": "Ножевой бой", "masteryLevel": 1 }
          ]
        }
        """);

        var teacher = new JsonObject
        {
            ["npcId"] = "npc_teacher_reina",
            ["name"] = "Рейна Быстрый Нож",
            ["role"] = "охотница-наставница",
            ["teacherProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 45,
                ["skills"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["skillId"] = "knife",
                        ["skillName"] = "Ножевой бой",
                        ["masteryLevel"] = 3
                    },
                    new JsonObject
                    {
                        ["skillId"] = "skinning",
                        ["skillName"] = "Снятие шкур",
                        ["masteryLevel"] = 2
                    }
                }
            }
        };
        var hash = TrainingService.ComputeSourceSnapshotHash(teacher);
        teacher["trainingShowcase"] = new JsonObject
        {
            ["sourceActorSnapshotHash"] = hash,
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "offer_knife_mastery_2",
                    ["targetId"] = "knife",
                    ["targetName"] = "Ножевой бой",
                    ["targetKind"] = "active_skill_mastery",
                    ["currentValue"] = 1,
                    ["targetValue"] = 2,
                    ["sourceCap"] = 3,
                    ["summary"] = "Рейна учит держать клинок ниже взгляда противника и бить без лишнего замаха.",
                    ["requirements"] = new JsonObject { ["minimumRelationship"] = 30 },
                    ["cost"] = new JsonObject
                    {
                        ["money"] = 120,
                        ["currentLevelExperiencePercent"] = 15
                    }
                },
                new JsonObject
                {
                    ["offerId"] = "offer_skinning_unlock",
                    ["targetId"] = "skinning",
                    ["targetName"] = "Снятие шкур",
                    ["targetKind"] = "passive_skill_unlock",
                    ["currentValue"] = 0,
                    ["targetValue"] = 1,
                    ["sourceCap"] = 2,
                    ["summary"] = "Практика у костра: как не испортить трофей и быстро оценить кожу.",
                    ["requirements"] = new JsonObject { ["minimumRelationship"] = 20 },
                    ["cost"] = new JsonObject
                    {
                        ["money"] = 80,
                        ["currentLevelExperiencePercent"] = 10
                    }
                }
            }
        };

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new JsonObject
        {
            ["NPCs"] = new JsonArray(teacher)
        }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private async Task SeedAfterlifeTrainingAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Chaos Sea",
          "turnNumber": 11,
          "inkFeathers": { "current": 900 },
          "afterlifeCombatProfile": {
            "artTiers": { "pressure": 0, "guard": 1 },
            "spiritFocusTier": 1,
            "specialArts": [
              {
                "artId": "mirror_oath",
                "displayName": "Зеркальная клятва",
                "tier": 1,
                "learned": true,
                "upgradeCost": { "inkFeathers": 75 }
              },
              {
                "artId": "shadow_chain",
                "displayName": "Цепь тени",
                "tier": 0,
                "learned": false,
                "upgradeCost": { "inkFeathers": 75 }
              }
            ]
          }
        }
        """);
        await _fs.WriteFileAtomicAsync("game_state/meta/shining_abode_state.json", """
        {
          "lightSparks": 8,
          "radiance": { "tier": 2, "experience": 120 }
        }
        """);
        var mentor = new JsonObject
        {
            ["actorId"] = "guardian_liora",
            ["displayName"] = "Архонт Лиора",
            ["actorType"] = "guardian",
            ["realm"] = "Chaos Sea",
            ["standardArts"] = new JsonObject
            {
                ["guard"] = 3,
                ["pressure"] = 2
            },
            ["mentorProfile"] = new JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 62,
                ["spiritFocusTier"] = 3
            }
        };
        var hash = TrainingService.ComputeSourceSnapshotHash(mentor);
        mentor["mentorTrainingShowcase"] = new JsonObject
        {
            ["sourceActorSnapshotHash"] = hash,
            ["offers"] = new JsonArray
            {
                new JsonObject
                {
                    ["offerId"] = "mentor_liora_guard_2",
                    ["targetId"] = "guard",
                    ["targetName"] = "Защита",
                    ["targetKind"] = "standard_spiritual_art",
                    ["targetValue"] = 2,
                    ["sourceCap"] = 3,
                    ["summary"] = "Лиора показывает, как удержать край души от расщепления.",
                    ["requirements"] = new JsonObject { ["minimumRelationship"] = 50 },
                    ["cost"] = new JsonObject { ["inkFeathers"] = 1 }
                }
            }
        };
        await _fs.WriteFileAtomicAsync("game_state/meta/afterlife_entity_profiles.json", new JsonObject
        {
            ["profiles"] = new JsonArray(mentor)
        }.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
    }

    private static string CollectText(ExplorerCommandResult result)
    {
        var parts = new List<string>();
        foreach (var block in result.Blocks)
            CollectBlockText(block, parts);
        foreach (var action in result.Actions)
        {
            parts.Add(action.Label);
            parts.Add(action.Command);
        }
        return string.Join("\n", parts);
    }

    private static void CollectBlockText(UiBlock block, List<string> parts)
    {
        switch (block)
        {
            case UiTextBlock text:
                parts.Add(text.Text);
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
                parts.AddRange(dossier.Badges.Select(badge => badge.Label));
                parts.AddRange(dossier.Facts.SelectMany(fact => new[] { fact.Label, fact.Value }));
                parts.AddRange(dossier.Hints.SelectMany(hint => new[] { hint.Title, hint.Text }));
                foreach (var card in dossier.Cards)
                    CollectCardText(card, parts);
                foreach (var section in dossier.Sections)
                {
                    parts.Add(section.Title);
                    parts.Add(section.Summary);
                    parts.Add(section.CollectionLabel);
                    parts.AddRange(section.Facts.SelectMany(fact => new[] { fact.Label, fact.Value }));
                    foreach (var card in section.Cards)
                        CollectCardText(card, parts);
                    foreach (var child in section.Blocks)
                        CollectBlockText(child, parts);
                }
                break;
            case UiMessageBlock message:
                parts.Add(message.Title);
                parts.Add(message.Message);
                break;
            case UiListBlock list:
                parts.AddRange(list.Items);
                break;
            case UiKeyValueGridBlock grid:
                parts.AddRange(grid.Items.SelectMany(item => new[] { item.Key, item.Value }));
                break;
        }
    }

    private static void CollectCardText(UiEntityCard card, List<string> parts)
    {
        parts.Add(card.Title);
        parts.Add(card.Subtitle);
        parts.Add(card.Summary);
        parts.AddRange(card.Badges.Select(badge => badge.Label));
        parts.AddRange(card.Facts.SelectMany(fact => new[] { fact.Label, fact.Value }));
        parts.AddRange(card.Metrics.Select(metric => $"{metric.Label}: {metric.Value}/{metric.Max}"));
        parts.AddRange(card.Hints.SelectMany(hint => new[] { hint.Title, hint.Text }));
        parts.AddRange(card.List);
        if (card.PrimaryAction != null)
        {
            parts.Add(card.PrimaryAction.Label);
            parts.Add(card.PrimaryAction.Command);
        }
        foreach (var nested in card.Nested.Concat(card.Cards))
            CollectCardText(nested, parts);
    }
}
