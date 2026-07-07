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
    public async Task TryProcessCommand_MortalTrainingWithoutShowcase_ReturnsPendingGmActionImmediately()
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

        Assert.NotNull(result);
        Assert.NotEqual(string.Empty, result);
        Assert.Contains("витрину обучения", result!, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("Выберите учителя", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(console.SelectionTitles, title => title.Contains("предложения", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(0, console.ReadKeyCalls);
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
}
