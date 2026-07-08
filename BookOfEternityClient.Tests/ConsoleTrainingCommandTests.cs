using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
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

        await _fs.WriteFileAtomicAsync("game_state/npcs/npc_core.json", """
        {
          "UpdateNPCs": [
            {
              "npcId": "npc_teacher_knife",
              "name": "Рейна Быстрый Нож",
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
}
