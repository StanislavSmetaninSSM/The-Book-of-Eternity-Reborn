using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ConsoleTrainingCommandTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;

    public ConsoleTrainingCommandTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-console-training-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
    }

    [Fact]
    [Trait("Category", "ConsoleTraining")]
    public async Task TryProcessCommand_MortalTrainingWithoutShowcase_WaitsInPlaceForGmVitrine()
    {
        await SeedMortalTrainingTeacherWithoutShowcaseAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            trainingService: new TrainingService(_fs, NullLogger<TrainingService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/обучение");

        Assert.Equal(string.Empty, result);
        Assert.Contains(console.MarkupLines, line =>
            line.Contains("Витрина подготавливается. Дождитесь завершения, ГМ работает", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(console.MarkupLines, line =>
            line.Contains("откройте /обучение снова", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("Выберите учителя", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("предложения", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, console.ReadKeyCalls);
    }

    [Fact]
    [Trait("Category", "ConsoleTraining")]
    public async Task TryProcessCommand_MortalTraining_ListsOnlyTeachersInCurrentLocation()
    {
        await SeedMortalTrainingTeachersByLocationAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        console.QueueSelection("Выберите учителя", "← Закрыть обучение");
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            trainingService: new TrainingService(_fs, NullLogger<TrainingService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/обучение");

        Assert.Equal(string.Empty, result);
        var choices = console.SelectionChoicesHistory
            .Single(entry => entry.Title.Contains("Выберите учителя", StringComparison.OrdinalIgnoreCase))
            .Choices;
        Assert.Contains(choices, choice => choice.Contains("Рейна Быстрый Нож", StringComparison.Ordinal));
        Assert.DoesNotContain(choices, choice => choice.Contains("Дальний наставник", StringComparison.Ordinal));
        Assert.False(_fs.FileExists(TrainingRequestState.PendingRequestPath));
    }

    [Fact]
    [Trait("Category", "ConsoleTraining")]
    public async Task TryProcessCommand_AfterlifeSelfTraining_UsesLocalizedLockedOfferNames()
    {
        await SeedAfterlifeSelfTrainingAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        console.QueueSelection(
            "Выберите наставника",
            "◇ Самостоятельная прокачка души",
            "← Закрыть обучение");
        console.QueueSelection("Самостоятельная прокачка", "← К обучению души");
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            trainingService: new TrainingService(_fs, NullLogger<TrainingService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/обучение");

        Assert.Equal(string.Empty, result);
        var selfTrainingChoices = console.SelectionChoicesHistory
            .Single(entry => entry.Title.Contains("Самостоятельная прокачка", StringComparison.OrdinalIgnoreCase))
            .Choices;
        Assert.Contains(selfTrainingChoices, choice => choice.Contains("Давление", StringComparison.Ordinal));
        Assert.Contains(selfTrainingChoices, choice => choice.Contains("Контрприём", StringComparison.Ordinal));
        Assert.DoesNotContain(selfTrainingChoices, choice => choice.Contains("Pressure", StringComparison.Ordinal));
        Assert.DoesNotContain(selfTrainingChoices, choice => choice.Contains("Counter", StringComparison.Ordinal));
        Assert.DoesNotContain(selfTrainingChoices, choice => choice.Contains("Guard", StringComparison.Ordinal));
        Assert.DoesNotContain(selfTrainingChoices, choice => choice.Contains("Maneuver", StringComparison.Ordinal));
    }

    [Fact]
    [Trait("Category", "ConsoleTraining")]
    public async Task TryProcessCommand_SpiritualArts_HidesImplementationOwnershipCopy()
    {
        await SeedAfterlifeSelfTrainingAsync();
        await _stateManager.RefreshGameStateAsync();
        var console = new TestExplorerConsole();
        var explorer = new ExplorerMode(
            _stateManager,
            _fs,
            new LocalizationManager(),
            trainingService: new TrainingService(_fs, NullLogger<TrainingService>.Instance),
            console: console);

        var result = await explorer.TryProcessCommand("/духовные_искусства");

        Assert.Equal(string.Empty, result);
        var renderedText = string.Join("\n", console.Rendered.Select(ExtractRenderableText));
        Assert.Contains("Самостоятельная прокачка здесь намеренно дорогая", renderedText, StringComparison.Ordinal);
        Assert.Contains("наставник", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fallback-режим", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Клиент локально", renderedText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("ГМ не пишет", renderedText, StringComparison.OrdinalIgnoreCase);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // ignore temp cleanup failures
        }
    }

    private async Task SeedMortalTrainingTeacherWithoutShowcaseAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Тестовая Душа",
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/console-training-test.json", """
        {
          "turnNumber": 9
        }
        """);

        await SeedCanonicalMortalLocationAsync("loc_training_yard", "Тренировочный двор");

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_teacher_knife",
              "name": "Рейна Быстрый Нож",
              "currentLocationId": "loc_training_yard",
              "teacherProfile": {
                "canTeach": true,
                "relationshipLevel": 45,
                "skills": [
                  {
                    "skillId": "knife",
                    "skillName": "Ножевой бой",
                    "masteryLevel": 3
                  }
                ]
              }
            }
          ]
        }
        """);
    }

    private async Task SeedAfterlifeSelfTrainingAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "soulName": "Северная Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "inkFeathers": { "current": 0, "total": 0 },
          "afterlifeCombatProfile": {
            "enlightenmentRank": 0,
            "radianceRank": 0,
            "retainedRadianceRank": 0,
            "spiritFocusTier": 0,
            "artTiers": {}
          }
        }
        """);

        await _fs.WriteFileAtomicAsync("stories/console-training-test.json", """
        {
          "turnNumber": 1
        }
        """);
    }

    private async Task SeedMortalTrainingTeachersByLocationAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """
        {
          "currentRealm": "Mortal World",
          "currentIncarnation": 1
        }
        """);
        await _fs.WriteFileAtomicAsync("stories/console-training-test.json", """
        {
          "turnNumber": 9
        }
        """);
        await SeedCanonicalMortalLocationAsync("loc_training_yard", "Тренировочный двор");

        var localTeacher = BuildTeacher("npc_teacher_local", "Рейна Быстрый Нож", "loc_training_yard");
        var remoteTeacher = BuildTeacher("npc_teacher_remote", "Дальний наставник", "loc_remote_tower");
        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", new System.Text.Json.Nodes.JsonObject
        {
            ["NPCs"] = new System.Text.Json.Nodes.JsonArray(localTeacher, remoteTeacher)
        }.ToJsonString());
    }

    private static System.Text.Json.Nodes.JsonObject BuildTeacher(string actorId, string name, string locationId)
    {
        var teacher = new System.Text.Json.Nodes.JsonObject
        {
            ["npcId"] = actorId,
            ["name"] = name,
            ["currentLocationId"] = locationId,
            ["teacherProfile"] = new System.Text.Json.Nodes.JsonObject
            {
                ["canTeach"] = true,
                ["relationshipLevel"] = 45,
                ["skills"] = new System.Text.Json.Nodes.JsonArray
                {
                    new System.Text.Json.Nodes.JsonObject
                    {
                        ["skillId"] = "knife",
                        ["skillName"] = "Ножевой бой",
                        ["masteryLevel"] = 3
                    }
                }
            }
        };
        teacher["trainingShowcase"] = new System.Text.Json.Nodes.JsonObject
        {
            ["sourceActorSnapshotHash"] = TrainingService.ComputeSourceSnapshotHash(teacher),
            ["offers"] = new System.Text.Json.Nodes.JsonArray()
        };
        return teacher;
    }

    private async Task SeedCanonicalMortalLocationAsync(string locationId, string displayName)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(locationId, displayName);
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationTestFixture.CreateWorldMap(location).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationMaterializationContract.CurrentLocationPath,
            MortalLocationTestFixture.CreateCurrentProjection(location).ToJsonString());
        await _fs.WriteFileAtomicAsync(
            MortalLocationIdentityState.StatePath,
            MortalLocationTestFixture.CreateIdentityIndex(location).ToJsonString());
    }

    private static string ExtractRenderableText(IRenderable renderable)
    {
        return renderable switch
        {
            Panel panel => ExtractPanelText(panel),
            Markup markup => ExtractParagraphText(markup),
            Text text => text.ToString() ?? string.Empty,
            _ => renderable.ToString() ?? string.Empty
        };
    }

    private static string ExtractPanelText(Panel panel)
    {
        var childField = typeof(Panel).GetField("_child", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return childField?.GetValue(panel) is IRenderable child
            ? ExtractRenderableText(child)
            : string.Empty;
    }

    private static string ExtractParagraphText(object renderable)
    {
        var paragraphField = renderable.GetType().GetField("_paragraph", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var paragraph = paragraphField?.GetValue(renderable);
        if (paragraph == null)
            return string.Empty;

        var linesField = paragraph.GetType().GetField("_lines", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        if (linesField?.GetValue(paragraph) is not IEnumerable<object> lines)
            return string.Empty;

        var lineTexts = new List<string>();
        foreach (var line in lines)
        {
            var itemsField = line.GetType().GetField("_items", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            if (itemsField?.GetValue(line) is not Array items)
                continue;

            lineTexts.Add(string.Concat(items.Cast<object?>().Where(static segment => segment != null).Select(static segment =>
            {
                var textProperty = segment!.GetType().GetProperty("Text", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                return textProperty?.GetValue(segment)?.ToString() ?? string.Empty;
            })));
        }

        return string.Join("\n", lineTexts);
    }
}
