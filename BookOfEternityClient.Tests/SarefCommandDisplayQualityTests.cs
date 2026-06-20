using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class SarefCommandDisplayQualityTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly StateManager _stateManager;

    public SarefCommandDisplayQualityTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-saref-command-quality-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _stateManager = new StateManager(_fs, new GameSettings(), NullLogger<StateManager>.Instance);
    }

    [Theory]
    [MemberData(nameof(SarefDisplayCommands))]
    public async Task SarefAndMemorySceneCommands_RenderUsablePlayerFacingConsoleOutput(
        string command,
        string expectedText)
    {
        await _fs.WriteFileAtomicAsync(SarefMainStoryState.StatePath, BuildSarefActiveMemorySceneState());

        var result = await ExplorerUniversalMetaCommandResultBuilder.TryBuildAsync(
            command,
            _stateManager,
            _fs,
            new LocalizationManager());

        Assert.NotNull(result);
        var report = ConsoleCommandOutputQualityClassifier.Classify(result);
        var violations = report.Violations.ToList();
        if (!report.VisibleText.Contains(expectedText, StringComparison.OrdinalIgnoreCase))
            violations.Add($"visible text does not include expected text: {expectedText}");

        Assert.True(
            violations.Count == 0,
            $"{command} returned unusable Saref player-facing console output:" +
            Environment.NewLine + string.Join(Environment.NewLine, violations));

        var console = new TestExplorerConsole();
        var renderException = Record.Exception(() => ExplorerCommandResultConsoleRenderer.Render(console, result));
        Assert.Null(renderException);
        Assert.NotEmpty(console.Rendered);
    }

    public static IEnumerable<object[]> SarefDisplayCommands()
    {
        yield return ["/сареф", "Имя Сарефа впервые собрано"];
        yield return ["/saref", "Ложная верность Азалии"];
        yield return ["/воспоминание", "Ложа белых перьев"];
        yield return ["/воспоминание_статус", "Ложа белых перьев"];
        yield return ["/воспоминание_начать", "Ложа белых перьев"];
        yield return ["/воспоминание_способности", "Ложа белых перьев"];
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

    private static string BuildSarefActiveMemorySceneState() => """
    {
      "schemaVersion": 1,
      "revealStage": "name_revealed",
      "guardianQuestlines": [
        {
          "guardianId": "azalia",
          "questStates": [
            { "questOrdinal": 1, "status": "completed", "questId": "azalia_saref_q1" },
            { "questOrdinal": 2, "status": "completed", "questId": "azalia_saref_q2" },
            { "questOrdinal": 3, "status": "completed", "questId": "azalia_saref_q3" },
            { "questOrdinal": 4, "status": "active", "questId": "azalia_saref_q4" }
          ]
        }
      ],
      "latentTraces": [],
      "sarefRevelations": [
        {
          "revelationId": "rev_name",
          "category": "identity",
          "sourceGuardianId": "azalia",
          "sourceQuestId": "azalia_saref_q4",
          "sourceQuestOrdinal": 4,
          "summary": "Имя Сарефа впервые собрано из фрагментов памяти.",
          "revealedAtTurn": 51
        }
      ],
      "sarefAdvantages": [
        {
          "advantageId": "adv_azalia_false_loyalty",
          "name": "Ложная верность Азалии",
          "summary": "Можно распознать след клятвы в словах Сарефа."
        }
      ],
      "sarefAdvantageUses": [],
      "memoryScene": {
        "sceneId": "memory_scene_azalia_q4",
        "title": "Ложа белых перьев",
        "status": "active",
        "layer": "Воспоминание",
        "guardianId": "azalia",
        "questId": "azalia_saref_q4",
        "questOrdinal": 4,
        "role": {
          "roleId": "azalia_white_lodge_witness",
          "displayName": "Свидетель ложи",
          "summary": "Игрок действует через роль свидетеля старого предательства Азалии."
        },
        "boundaries": [
          { "boundaryId": "past_is_fixed", "summary": "Сареф уже вошёл в ложу; это нельзя отменить." }
        ],
        "abilities": [
          { "abilityId": "read_oath", "name": "Прочитать клятву", "summary": "Увидеть скрытую цену белых перьев." },
          { "abilityId": "hold_memory", "name": "Удержать память", "summary": "Не дать сцене рассыпаться." },
          { "abilityId": "name_traitor", "name": "Назвать предателя", "summary": "Связать образ с будущей правдой о Сарефе." }
        ],
        "requiredStoryNodes": [
          { "nodeId": "enter_lodge", "status": "completed", "summary": "Войти в ложу белых перьев." },
          { "nodeId": "see_betrayal", "status": "pending", "summary": "Увидеть предательство и назвать его цену." }
        ],
        "successCondition": {
          "conditionId": "truth_recognized",
          "summary": "Игрок распознал связь ложи с Крыльями Ангелов.",
          "satisfied": false
        },
        "closureTarget": {
          "guardianId": "azalia",
          "questId": "azalia_saref_q4",
          "questOrdinal": 4,
          "revelationId": "rev_azalia_faction",
          "advantageId": "adv_azalia_false_loyalty"
        },
        "startedAtTurn": 43
      },
      "wingsInfiltration": null,
      "factionLinks": { "visibility": "hidden" },
      "finalConfrontation": null,
      "defeatOutcomes": [],
      "endings": [],
      "playerOathState": null,
      "sarefPersonalBond": null
    }
    """;
}
