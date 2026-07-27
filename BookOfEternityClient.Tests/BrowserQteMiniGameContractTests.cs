using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class BrowserQteMiniGameContractTests : IDisposable
{
    private static readonly JsonSerializerOptions WebJsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _qte;
    private readonly QteWebInteractionService _web;

    public BrowserQteMiniGameContractTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-browser-qte-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        var characteristicsService = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        _qte = new QteSceneService(
            _fs,
            settings,
            characteristicsService,
            null!,
            null!,
            null!,
            null!,
            null!,
            null!,
            NullLogger<QteSceneService>.Instance);
        _web = new QteWebInteractionService(
            _fs,
            _qte,
            new BrowserLocalWriteCoordinator(
                _fs,
                new LocalUiSessionLockService(_fs)));
    }

    [Fact]
    public async Task BuildReadOnlyStateAsync_ProjectsMiniGameCheckConfigForSupportedBrowserQteTypes()
    {
        await WriteActiveSceneAsync(SupportedActions());

        var state = await _web.BuildReadOnlyStateAsync();
        var actions = ProjectedActions(state);

        AssertConfig(actions, "timingbar", "TimingBar", requiresSubmittedGrade: true);
        AssertConfig(actions, "promptchain", "PromptChain", requiresSubmittedGrade: true);
        AssertConfig(actions, "balancemeter", "BalanceMeter", requiresSubmittedGrade: true);
        AssertConfig(actions, "chargerelease", "ChargeRelease", requiresSubmittedGrade: true);
        AssertConfig(actions, "branchchoice", "BranchChoice", requiresSubmittedGrade: false);
        AssertConfig(actions, "mashinput", "MashInput", requiresSubmittedGrade: true);
        AssertConfig(actions, "patternmemory", "PatternMemory", requiresSubmittedGrade: true);
        AssertConfig(actions, "rhythmpulse", "RhythmPulse", requiresSubmittedGrade: true);
        AssertConfig(actions, "precisionchoice", "PrecisionChoice", requiresSubmittedGrade: true);
        AssertConfig(actions, "stealthnoise", "StealthNoise", requiresSubmittedGrade: true);
        AssertConfig(actions, "lockpinset", "LockPinSet", requiresSubmittedGrade: true);

        Assert.Equal(["success", "partial", "fail"], actions["mashinput"]["gradeOptions"]!.AsArray().Select(item => item!.GetValue<string>()));
        Assert.Equal("space", RequiredString(actions["mashinput"]["checkConfig"]!.AsObject(), "keys", "mash config array first item", arrayIndex: 0));
        Assert.Equal("open_gate", RequiredString(actions["precisionchoice"]["checkConfig"]!.AsObject(), "correctChoiceId", "precision config"));
        Assert.Equal(2, actions["lockpinset"]["checkConfig"]!["pinWindows"]!.AsArray().Count);
    }

    [Fact]
    public async Task BuildReadOnlyStateAsync_ProjectsUnknownCheckAsUnsupportedWithoutRawConfigEcho()
    {
        await WriteActiveSceneAsync(
        [
            BuildAction(
                "futuremirror",
                "FutureMirror",
                JsonNode.Parse("""{ "unsafeRawField": "do not echo", "hiddenGrade": "success" }""")!.AsObject())
        ]);

        var state = await _web.BuildReadOnlyStateAsync();
        var action = Assert.Single(ProjectedActions(state).Values);
        var config = action["checkConfig"]!.AsObject();

        Assert.Equal("Unsupported", RequiredString(config, "kind", "unsupported config"));
        Assert.False(config["supported"]!.GetValue<bool>());
        Assert.Equal("FutureMirror", RequiredString(config, "checkType", "unsupported config"));

        var serializedConfig = config.ToJsonString(WebJsonOptions);
        Assert.DoesNotContain("unsafeRawField", serializedConfig, StringComparison.Ordinal);
        Assert.DoesNotContain("hiddenGrade", serializedConfig, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BuildReadOnlyStateAsync_UsesRuntimeStatTierForEffectiveMiniGameConfig()
    {
        await _fs.WriteFileAtomicAsync(
            "game_state/misc/characteristics.json",
            """{ "dexterity": 90 }""");
        Assert.Equal(3, await _qte.ResolveQteStatTierAsync(Characteristics.Dexterity));
        await WriteActiveSceneAsync(
        [
            BuildAction("timingbar", "TimingBar"),
            BuildAction("mashinput", "MashInput", JsonNode.Parse("""
            {
              "keys": ["space"],
              "durationMs": 3000,
              "targetPresses": 12,
              "partialThreshold": 0.5
            }
            """)!.AsObject()),
            BuildAction("precisionchoice", "PrecisionChoice", JsonNode.Parse("""
            {
              "correctChoiceId": "open_gate",
              "timeoutMs": 6000,
              "choices": [
                { "id": "open_gate", "label": "Открыть врата", "grade": "success" },
                { "id": "dark_cellar", "label": "Тёмный подвал", "grade": "fail" }
              ]
            }
            """)!.AsObject()),
            BuildAction("stealthnoise", "StealthNoise", JsonNode.Parse("""
            {
              "durationMs": 6000,
              "startingNoise": 10,
              "dangerThreshold": 70,
              "noiseDriftPerSecond": 9,
              "recoveryPerInput": 12,
              "allowedOverThresholdMs": 800,
              "gradeThresholds": {
                "successMaxNoise": 45,
                "successMaxOverThresholdMs": 0,
                "partialMaxNoise": 75,
                "partialMaxOverThresholdMs": 800
              }
            }
            """)!.AsObject()),
            BuildAction("lockpinset", "LockPinSet", JsonNode.Parse("""
            {
              "pinCount": 2,
              "pinWindows": [
                { "pin": 1, "min": 20, "max": 30 },
                { "pin": 2, "min": 65, "max": 75 }
              ],
              "timerMs": 10000,
              "pickDurability": 5,
              "maxMistakes": 2,
              "pinDriftPerSecond": 4,
              "gradeThresholds": {
                "successMaxTimeMs": 4000,
                "successMaxMistakes": 0,
                "partialMaxTimeMs": 9000,
                "partialMaxMistakes": 2
              }
            }
            """)!.AsObject())
        ]);

        var actions = ProjectedActions(await _web.BuildReadOnlyStateAsync());

        Assert.Equal(8, actions["timingbar"]["checkConfig"]!["successWidth"]!.GetValue<int>());
        Assert.Equal(125, actions["timingbar"]["checkConfig"]!["tickMs"]!.GetValue<int>());
        Assert.Equal(9, actions["mashinput"]["checkConfig"]!["successTarget"]!.GetValue<int>());
        Assert.Equal(6750, actions["precisionchoice"]["checkConfig"]!["timeoutMs"]!.GetValue<int>());
        Assert.Equal(15d, actions["stealthnoise"]["checkConfig"]!["recoveryPerInput"]!.GetValue<double>());
        Assert.Equal(10750, actions["lockpinset"]["checkConfig"]!["timerMs"]!.GetValue<int>());
        Assert.Equal(3, actions["lockpinset"]["checkConfig"]!["maxMistakes"]!.GetValue<int>());
    }

    [Fact]
    public async Task BuildReadOnlyStateAsync_ProjectsReadOnlyScoreStateWithVisibility()
    {
        await _qte.BeginAcceptedSceneAsync(BuildScoredBrowserOffer(), currentTurnNumber: 12);

        var state = await _web.BuildReadOnlyStateAsync();
        var json = JsonSerializer.Serialize(state, WebJsonOptions);
        var root = JsonNode.Parse(json)!.AsObject();
        var scoreState = root["activeScene"]!["scoreState"]!.AsObject();
        var metrics = scoreState["metrics"]!.AsArray().Select(static item => item!.AsObject()).ToArray();

        var stealth = Assert.Single(metrics, metric => RequiredString(metric, "id", "score metric") == "stealth");
        Assert.Equal("Скрытность", RequiredString(stealth, "label", "stealth score metric"));
        Assert.Equal(50d, stealth["value"]!.GetValue<double>());
        Assert.Equal("always", RequiredString(stealth, "visibility", "stealth score metric"));

        var serializedScore = scoreState.ToJsonString(WebJsonOptions);
        Assert.DoesNotContain("Улики", serializedScore, StringComparison.Ordinal);
        Assert.DoesNotContain("secretPressure", serializedScore, StringComparison.Ordinal);
        Assert.DoesNotContain("scoreDeltas", serializedScore, StringComparison.Ordinal);
    }

    private async Task WriteActiveSceneAsync(IReadOnlyList<QteSceneService.QteAction> actions)
    {
        var state = new QteSceneService.QteRuntimeState
        {
            ActiveScene = new QteSceneService.ActiveQteSceneState
            {
                CurrentChapterId = "start",
                AcceptedAtTurn = 12,
                Offer = new QteSceneService.QteOffer
                {
                    QteId = "qte_browser_parity_contract",
                    Title = "Проверка браузерных быстрых сцен",
                    StartChapterId = "start",
                    Chapters =
                    [
                        new QteSceneService.QteChapter
                        {
                            ChapterId = "start",
                            Title = "Начало",
                            Narrative = "Книга предлагает несколько испытаний.",
                            Actions = actions.ToList()
                        }
                    ],
                    TerminalOutcomes = []
                }
            }
        };

        var json = JsonSerializer.Serialize(state, WebJsonOptions);
        await _fs.WriteFileAtomicAsync(QteSceneService.QteRuntimePath, json);
    }

    private static IReadOnlyList<QteSceneService.QteAction> SupportedActions() =>
    [
        BuildAction("timingbar", "TimingBar"),
        BuildAction("promptchain", "PromptChain"),
        BuildAction("balancemeter", "BalanceMeter"),
        BuildAction("chargerelease", "ChargeRelease"),
        BuildAction("branchchoice", "BranchChoice", JsonNode.Parse("""{ "choiceGrade": "partial" }""")!.AsObject()),
        BuildAction("mashinput", "MashInput", JsonNode.Parse("""
        {
          "keys": ["space"],
          "durationMs": 3000,
          "targetPresses": 12,
          "partialThreshold": 0.5
        }
        """)!.AsObject()),
        BuildAction("patternmemory", "PatternMemory", JsonNode.Parse("""
        {
          "alphabet": ["q", "w", "space"],
          "sequenceLength": 4,
          "revealMs": 1200,
          "inputTimeoutMs": 2400,
          "allowedMistakes": 1
        }
        """)!.AsObject()),
        BuildAction("rhythmpulse", "RhythmPulse", JsonNode.Parse("""
        {
          "pulseCount": 4,
          "beatIntervalMs": 650,
          "hitWindowMs": 90,
          "allowedMisses": 1,
          "patternVariation": "steady"
        }
        """)!.AsObject()),
        BuildAction("precisionchoice", "PrecisionChoice", JsonNode.Parse("""
        {
          "correctChoiceId": "open_gate",
          "timeoutMs": 6000,
          "timeoutGrade": "partial",
          "choices": [
            { "id": "open_gate", "label": "Открыть врата", "grade": "success", "hint": "Верный знак" },
            { "id": "narrow_door", "label": "Боковая дверь", "grade": "partial" },
            { "id": "dark_cellar", "label": "Тёмный подвал", "grade": "fail" }
          ],
          "decoyHints": [
            { "choiceId": "dark_cellar", "hint": "Там слишком тихо" }
          ]
        }
        """)!.AsObject()),
        BuildAction("stealthnoise", "StealthNoise", JsonNode.Parse("""
        {
          "durationMs": 6000,
          "startingNoise": 10,
          "dangerThreshold": 70,
          "noiseDriftPerSecond": 9,
          "recoveryPerInput": 12,
          "allowedOverThresholdMs": 800,
          "recoveryKey": "space",
          "recoveryLabel": "приглушить шаги",
          "warningLabel": "Шум близко к срыву.",
          "gradeThresholds": {
            "successMaxNoise": 45,
            "successMaxOverThresholdMs": 0,
            "partialMaxNoise": 75,
            "partialMaxOverThresholdMs": 800
          }
        }
        """)!.AsObject()),
        BuildAction("lockpinset", "LockPinSet", JsonNode.Parse("""
        {
          "pinCount": 2,
          "pinWindows": [
            { "pin": 1, "min": 20, "max": 30, "label": "первый штифт" },
            { "pin": 2, "min": 65, "max": 75, "label": "второй штифт" }
          ],
          "timerMs": 10000,
          "pickDurability": 5,
          "maxMistakes": 2,
          "pinDriftPerSecond": 4,
          "adjustKey": "q",
          "setKey": "space",
          "pinLabel": "штифт",
          "durabilityLabel": "беречь отмычку",
          "warningLabel": "Замок сопротивляется.",
          "gradeThresholds": {
            "successMaxTimeMs": 4000,
            "successMaxMistakes": 0,
            "partialMaxTimeMs": 9000,
            "partialMaxMistakes": 2
          }
        }
        """)!.AsObject())
    ];

    private static QteSceneService.QteOffer BuildScoredBrowserOffer()
    {
        var json = """
        {
          "qteId": "qte_browser_scored_contract",
          "title": "Тихое проникновение",
          "offerText": "Нужно пройти двор и не поднять тревогу.",
          "introNarrative": "Фонари качаются над мокрым двором.",
          "startChapterId": "start",
          "scoreModel": {
            "metrics": [
              { "id": "stealth", "label": "Скрытность", "initial": 50, "min": 0, "max": 100, "visibility": "always" },
              { "id": "evidence", "label": "Улики", "initial": 0, "min": 0, "max": 100, "visibility": "final" },
              { "id": "secretPressure", "label": "Тайное давление", "initial": 0, "min": 0, "max": 100, "visibility": "hidden" }
            ],
            "rankOrder": ["good", "bad"],
            "ranks": [
              {
                "id": "good",
                "label": "Удачный исход",
                "summary": "Следы остались под контролем.",
                "allOf": [
                  { "metric": "stealth", "op": ">=", "value": 40 }
                ]
              },
              {
                "id": "bad",
                "label": "Провальный исход",
                "summary": "Сцена сорвалась.",
                "fallback": true
              }
            ]
          },
          "chapters": [
            {
              "chapterId": "start",
              "title": "Двор",
              "narrative": "Стража приближается.",
              "actions": [
                {
                  "actionId": "cross_yard",
                  "label": "Пройти двор",
                  "check": {
                    "type": "BranchChoice",
                    "baseDifficulty": 2,
                    "primaryCharacteristic": "dexterity",
                    "config": { "choiceGrade": "success" }
                  },
                  "scoreDeltas": {
                    "success": [
                      { "metric": "stealth", "delta": 10 }
                    ]
                  },
                  "routing": {
                    "success": { "terminalOutcomeId": "done" },
                    "partial": { "terminalOutcomeId": "done" },
                    "fail": { "terminalOutcomeId": "done" }
                  }
                }
              ]
            }
          ],
          "terminalOutcomes": [
            {
              "outcomeId": "done",
              "title": "Выход",
              "finalNarrative": "Вы уходите через сад.",
              "gmSummary": "QTE завершена.",
              "responseFragment": { "response": "Вы уходите.", "experienceGained": 5 }
            }
          ]
        }
        """;

        return JsonSerializer.Deserialize<QteSceneService.QteOffer>(json)!;
    }

    private static QteSceneService.QteAction BuildAction(
        string actionId,
        string checkType,
        JsonObject? config = null) =>
        new()
        {
            ActionId = actionId,
            Label = $"Действие {checkType}",
            Check = new QteSceneService.QteCheck
            {
                Type = checkType,
                BaseDifficulty = 3,
                PrimaryCharacteristic = Characteristics.Dexterity,
                Config = config
            }
        };

    private static Dictionary<string, JsonObject> ProjectedActions(QteWebStateDto state)
    {
        var json = JsonSerializer.Serialize(state, WebJsonOptions);
        var root = JsonNode.Parse(json)!.AsObject();
        return root["activeScene"]!["currentChapter"]!["actions"]!.AsArray()
            .Select(static item => item!.AsObject())
            .ToDictionary(
                static action => action["actionId"]!.GetValue<string>(),
                static action => action,
                StringComparer.OrdinalIgnoreCase);
    }

    private static void AssertConfig(
        IReadOnlyDictionary<string, JsonObject> actions,
        string actionId,
        string kind,
        bool requiresSubmittedGrade)
    {
        Assert.True(actions.TryGetValue(actionId, out var action), $"Missing projected action {actionId}.");
        Assert.Equal(requiresSubmittedGrade, action!["requiresSubmittedGrade"]!.GetValue<bool>());

        var config = Assert.IsType<JsonObject>(action["checkConfig"]);
        Assert.Equal(kind, RequiredString(config, "kind", $"{actionId} checkConfig"));
        Assert.True(config["supported"]!.GetValue<bool>(), $"{actionId} should be supported by the browser mini-game projection.");
    }

    private static string RequiredString(JsonObject root, string propertyName, string context, int? arrayIndex = null)
    {
        Assert.True(root.TryGetPropertyValue(propertyName, out var node), $"{context} is missing {propertyName}.");
        Assert.NotNull(node);

        if (arrayIndex.HasValue)
            return node!.AsArray()[arrayIndex.Value]!.GetValue<string>();

        return node!.GetValue<string>();
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
            // ignored
        }
    }
}
