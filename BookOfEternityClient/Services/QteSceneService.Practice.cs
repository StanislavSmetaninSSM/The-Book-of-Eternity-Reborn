using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;
using Spectre.Console;

namespace BookOfEternityClient.Services;

public sealed partial class QteSceneService
{
    private const string PracticeChapterId = "practice_challenge";
    private const string PracticeMetricFocus = "focus";
    private static readonly string[] PracticeSupportedSurfaces = ["console", "browser"];

    private static readonly IReadOnlyList<QtePracticeDifficultyPreset> PracticeDifficulties =
    [
        new QtePracticeDifficultyPreset("easy", "Мягкая", "Больше времени и шире окно успеха.", 1),
        new QtePracticeDifficultyPreset("normal", "Обычная", "Базовая скорость и требования.", 3),
        new QtePracticeDifficultyPreset("hard", "Сложная", "Меньше права на ошибку и плотнее темп.", 5)
    ];

    private static readonly IReadOnlyList<QtePracticeCatalogEntry> PracticeCatalog =
    [
        PracticeEntry("BranchChoice", "Выбор ветки", "Статичный выбор ветки без наград и без изменения сюжета.", "Выберите вариант и посмотрите, как клиент закрывает ветку локально."),
        PracticeEntry("TimingBar", "Полоса реакции", "Тренировка попадания в движущееся окно без наград и без изменения сюжета.", "Остановите маркер в зоне успеха или частичного успеха."),
        PracticeEntry("PromptChain", "Цепь знаков", "Тренировка последовательности физических QTE клавиш без наград и без изменения сюжета.", "Повторите цепочку Q/W/E/A/S/D/Space; русская раскладка обрабатывается клиентом."),
        PracticeEntry("BalanceMeter", "Равновесие", "Тренировка удержания показателя в безопасной зоне без наград и без изменения сюжета.", "Компенсируйте смещение, пока счётчик не завершится."),
        PracticeEntry("ChargeRelease", "Накопление силы", "Тренировка отпускания заряда в нужном диапазоне без наград и без изменения сюжета.", "Накопите силу и отпустите её внутри целевой зоны."),
        PracticeEntry("MashInput", "Рывок усилия", "Тренировка частых нажатий без наград и без изменения сюжета.", "Нажимайте указанную физическую клавишу до конца таймера."),
        PracticeEntry("PatternMemory", "Память рун", "Тренировка запоминания и повтора последовательности без наград и без изменения сюжета.", "Запомните показанную цепочку и повторите её QTE клавишами."),
        PracticeEntry("RhythmPulse", "Пульс ритма", "Тренировка нажатий в ритм без наград и без изменения сюжета.", "Нажимайте Space в момент пульсации."),
        PracticeEntry("PrecisionChoice", "Точный выбор", "Тренировка выбора верной опции под таймером без наград и без изменения сюжета.", "Выберите лучший вариант до истечения времени."),
        PracticeEntry("StealthNoise", "Тихий проход", "Тренировка контроля шума без наград и без изменения сюжета.", "Приглушайте шум, не позволяя ему держаться выше опасной черты."),
        PracticeEntry("LockPinSet", "Штифты замка", "Тренировка выставления штифтов без наград и без изменения сюжета.", "Поднимайте, опускайте и фиксируйте штифты в разрешённых окнах.")
    ];

    public static IReadOnlyList<QtePracticeCatalogEntry> GetPracticeCatalog() => PracticeCatalog;

    public QtePracticeAttemptState StartPracticeAttempt(string? typeId, string? difficultyId)
    {
        var entry = FindPracticeEntry(typeId);
        var difficulty = FindPracticeDifficulty(difficultyId);
        var offer = BuildPracticeOffer(entry, difficulty);

        return new QtePracticeAttemptState
        {
            AttemptId = offer.QteId,
            TypeId = entry.TypeId,
            DifficultyId = difficulty.DifficultyId,
            State = "Active",
            ActiveScene = new ActiveQteSceneState
            {
                Offer = offer,
                CurrentChapterId = offer.StartChapterId,
                AcceptedAtTurn = 0,
                ScoreState = BuildInitialScoreState(offer.ScoreModel)
            },
            FeedbackTitle = entry.Title,
            Feedback = $"{entry.Instructions} Тренировка не меняет сюжет и не выдаёт награды.",
            LocalScoreNotice = PracticeLocalScoreNotice
        };
    }

    public QteActionResolution ResolvePracticeAction(QtePracticeAttemptState attempt, string actionId, string? submittedGrade)
    {
        if (!string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Practice attempt is not active.");

        var active = attempt.ActiveScene ?? throw new InvalidOperationException("Practice QTE scene is not active.");
        var offer = active.Offer ?? throw new InvalidOperationException("Practice QTE offer is missing.");

        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
            throw new InvalidOperationException($"Practice QTE chapter '{active.CurrentChapterId}' not found.");

        var action = chapter.Actions.FirstOrDefault(item =>
            string.Equals(item.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null)
            throw new InvalidOperationException($"Practice QTE action '{actionId}' not found.");

        var grade = ResolveBrowserSubmittedGrade(action, submittedGrade);
        var target = grade switch
        {
            QteGrade.Success => action.Routing.Success,
            QteGrade.Partial => action.Routing.Partial,
            _ => action.Routing.Fail
        };
        var resultText = ResolveResultText(action, grade);
        ApplyScoreDeltas(active.ScoreState, action, grade);

        QteActionResolution resolution;
        if (!string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
        {
            var outcome = offer.TerminalOutcomes.FirstOrDefault(item =>
                string.Equals(item.OutcomeId, target.TerminalOutcomeId, StringComparison.OrdinalIgnoreCase));
            if (outcome == null)
                throw new InvalidOperationException($"Practice QTE outcome '{target.TerminalOutcomeId}' not found.");

            var scoreSummary = BuildFinalScoreSummary(offer.ScoreModel, active.ScoreState);
            var summary = BuildCompletionSummary(offer, outcome, grade, scoreSummary);
            resolution = new QteActionResolution
            {
                State = "Completed",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                Completion = new QteSceneCompletion
                {
                    QteId = offer.QteId,
                    OutcomeId = outcome.OutcomeId,
                    Summary = summary,
                    Response = new GameResponse { Response = outcome.FinalNarrative },
                    ScoreSummary = scoreSummary
                }
            };

            attempt.State = "Completed";
            attempt.LastCompletion = resolution.Completion;
            attempt.FeedbackTitle = "Попытка завершена";
            attempt.Feedback = $"{resultText} Итог показан только для тренировки: сюжет, награды, опыт, предметы и прогресс не меняются.";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.NextChapterId))
                throw new InvalidOperationException($"Practice QTE action '{action.ActionId}' has no nextChapterId or terminalOutcomeId.");

            active.CurrentChapterId = target.NextChapterId;
            resolution = new QteActionResolution
            {
                State = "Active",
                QteId = offer.QteId,
                ChapterId = chapter.ChapterId,
                ActionId = action.ActionId,
                Grade = grade.ToString().ToLowerInvariant(),
                ResultText = resultText,
                NextChapterId = target.NextChapterId
            };

            attempt.FeedbackTitle = "Следующий шаг";
            attempt.Feedback = $"{resultText} Тренировка продолжается без наград и без изменения сюжета.";
        }

        attempt.LastResolution = resolution;
        attempt.LocalScoreNotice = PracticeLocalScoreNotice;
        return resolution;
    }

    public async Task RunPracticeModeAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Panel(new Markup(
                "[bold cyan]Свободная тренировка QTE[/]\n\n" +
                "Выберите мини-игру. Попытки используют обычные QTE проверки, но не меняют сюжет, награды, опыт, предметы или прогресс."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var choices = BuildUniqueOptions(PracticeCatalog, entry =>
                ConsoleLayout.PlainChoiceLabel($"⚡ {entry.Title}", entry.TypeId, "без наград"));
            var exitLabel = "Выйти из тренировки";
            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите тип QTE:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(choices.Select(item => item.Label).Append(exitLabel)));
            if (string.Equals(selected, exitLabel, StringComparison.Ordinal))
                return;

            var entry = choices.First(item => string.Equals(item.Label, selected, StringComparison.Ordinal)).Value;
            var difficultyChoices = BuildUniqueOptions(PracticeDifficulties, difficulty =>
                ConsoleLayout.PlainChoiceLabel(difficulty.Label, difficulty.DifficultyId, difficulty.Description));
            var selectedDifficulty = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Выберите сложность:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices(difficultyChoices.Select(item => item.Label)));
            var difficulty = difficultyChoices.First(item => string.Equals(item.Label, selectedDifficulty, StringComparison.Ordinal)).Value;

            var mode = await RunSinglePracticeAttemptAsync(entry.TypeId, difficulty.DifficultyId);
            if (mode == PracticeConsoleNext.Exit)
                return;
        }
    }

    private async Task<PracticeConsoleNext> RunSinglePracticeAttemptAsync(string typeId, string difficultyId)
    {
        var attempt = StartPracticeAttempt(typeId, difficultyId);
        while (true)
        {
            var active = attempt.ActiveScene;
            var offer = active.Offer!;
            var chapter = offer.Chapters.First(item =>
                string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            ShowChapterPrelude(offer, chapter, active.ScoreState);

            var action = chapter.Actions.Single();
            var grade = await RunCheckAsync(action);
            var resolution = ResolvePracticeAction(attempt, action.ActionId, GradeKey(grade));
            await ShowIntermediateResultAsync(offer, chapter, action, grade);

            if (resolution.Completion == null)
                continue;

            RenderPracticeCompletion(resolution.Completion);
            var next = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Что дальше?[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("Повторить", "Сменить сложность", "Выбрать другое QTE", "Выйти"));
            return next switch
            {
                "Повторить" => await RunSinglePracticeAttemptAsync(typeId, difficultyId),
                "Сменить сложность" => PracticeConsoleNext.ChangeDifficulty,
                "Выбрать другое QTE" => PracticeConsoleNext.ChooseAnother,
                _ => PracticeConsoleNext.Exit
            };
        }
    }

    private static void RenderPracticeCompletion(QteSceneCompletion completion)
    {
        var lines = new List<string>
        {
            completion.Response.Response ?? completion.Summary,
            "",
            PracticeLocalScoreNotice
        };
        if (!string.IsNullOrWhiteSpace(completion.ScoreSummary?.Rank?.Label))
            lines.Add($"Ранг: {completion.ScoreSummary.Rank.Label}");

        AnsiConsole.Write(new Panel(new Markup(Markup.Escape(string.Join("\n", lines))))
        {
            Header = new PanelHeader(" Итог тренировки "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        });
    }

    private static QtePracticeCatalogEntry FindPracticeEntry(string? typeId)
    {
        var normalized = typeId?.Trim();
        var entry = PracticeCatalog.FirstOrDefault(item =>
            string.Equals(item.TypeId, normalized, StringComparison.OrdinalIgnoreCase));
        if (entry == null)
            throw new InvalidOperationException("Practice QTE type is unavailable.");

        return entry;
    }

    private static QtePracticeDifficultyPreset FindPracticeDifficulty(string? difficultyId)
    {
        var normalized = string.IsNullOrWhiteSpace(difficultyId) ? "normal" : difficultyId.Trim();
        return PracticeDifficulties.FirstOrDefault(item =>
            string.Equals(item.DifficultyId, normalized, StringComparison.OrdinalIgnoreCase)) ??
            PracticeDifficulties.First(item => item.DifficultyId == "normal");
    }

    private static QteOffer BuildPracticeOffer(QtePracticeCatalogEntry entry, QtePracticeDifficultyPreset difficulty)
    {
        var actionId = $"practice_{entry.TypeId.ToLowerInvariant()}_{difficulty.DifficultyId}";
        var successOutcomeId = $"{actionId}_success";
        var partialOutcomeId = $"{actionId}_partial";
        var failOutcomeId = $"{actionId}_fail";

        return new QteOffer
        {
            QteId = $"practice_{entry.TypeId.ToLowerInvariant()}_{difficulty.DifficultyId}",
            Title = $"Тренировка: {entry.Title}",
            OfferText = $"{entry.Description} Эта попытка не меняет сюжет и не выдаёт награды.",
            IntroNarrative = entry.Instructions,
            DeclineHint = "Можно выйти в меню тренировки без последствий.",
            CinematicJustification = "Client-owned practice mode for learning QTE mechanics.",
            StartChapterId = PracticeChapterId,
            ScoreModel = BuildPracticeScoreModel(),
            Chapters =
            [
                new QteChapter
                {
                    ChapterId = PracticeChapterId,
                    Title = entry.Title,
                    Narrative = $"{entry.Instructions} Тренировочный результат останется только в этой сессии.",
                    Actions =
                    [
                        new QteAction
                        {
                            ActionId = actionId,
                            Label = entry.Title,
                            Check = new QteCheck
                            {
                                Type = entry.TypeId,
                                BaseDifficulty = difficulty.BaseDifficulty,
                                PrimaryCharacteristic = PracticeCharacteristic(entry.TypeId),
                                Config = BuildPracticeConfig(entry.TypeId, difficulty)
                            },
                            Routing = new QteRouting
                            {
                                Success = new QteBranchTarget { TerminalOutcomeId = successOutcomeId },
                                Partial = new QteBranchTarget { TerminalOutcomeId = partialOutcomeId },
                                Fail = new QteBranchTarget { TerminalOutcomeId = failOutcomeId }
                            },
                            SuccessText = "Уверенный тренировочный успех.",
                            PartialText = "Частичный тренировочный успех.",
                            FailText = "Тренировочная ошибка без последствий.",
                            ScoreDeltas = PracticeScoreDeltas()
                        }
                    ]
                }
            ],
            TerminalOutcomes =
            [
                PracticeOutcome(successOutcomeId, "Успешная тренировка", "Вы уверенно справились с упражнением."),
                PracticeOutcome(partialOutcomeId, "Частичная тренировка", "Вы завершили упражнение с запасом для улучшения."),
                PracticeOutcome(failOutcomeId, "Ошибка тренировки", "Попытка сорвалась, но это только тренировка.")
            ]
        };
    }

    private static QteTerminalOutcome PracticeOutcome(string outcomeId, string title, string finalNarrative) =>
        new()
        {
            OutcomeId = outcomeId,
            Title = title,
            FinalNarrative = finalNarrative,
            GmSummary = "Client-owned QTE Practice Mode result; no GM-authored practice scene and no campaign mutation.",
            ResponseFragment = new JsonObject
            {
                ["response"] = finalNarrative
            }
        };

    private static QteScoreModel BuildPracticeScoreModel() =>
        new()
        {
            Metrics =
            [
                new QteScoreMetricDefinition
                {
                    Id = PracticeMetricFocus,
                    Label = "Тренировочная точность",
                    Initial = 50,
                    Min = 0,
                    Max = 100,
                    Visibility = "always"
                }
            ],
            RankOrder = ["gold", "silver", "learning"],
            Ranks =
            [
                new QteScoreRankDefinition
                {
                    Id = "gold",
                    Label = "Уверенный результат",
                    Summary = "Механика освоена уверенно.",
                    AllOf = [new QteScoreThreshold { Metric = PracticeMetricFocus, Op = ">=", Value = 80 }]
                },
                new QteScoreRankDefinition
                {
                    Id = "silver",
                    Label = "Удачный исход",
                    Summary = "База понятна, есть место для ускорения.",
                    AllOf = [new QteScoreThreshold { Metric = PracticeMetricFocus, Op = ">=", Value = 50 }]
                },
                new QteScoreRankDefinition
                {
                    Id = "learning",
                    Label = "Учебная попытка",
                    Summary = "Попробуйте ещё раз на выбранной сложности.",
                    Fallback = true
                }
            ]
        };

    private static Dictionary<string, List<QteScoreDelta>> PracticeScoreDeltas() =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = [new QteScoreDelta { Metric = PracticeMetricFocus, Delta = 35 }],
            ["partial"] = [new QteScoreDelta { Metric = PracticeMetricFocus, Delta = 10 }],
            ["fail"] = [new QteScoreDelta { Metric = PracticeMetricFocus, Delta = -25 }]
        };

    private static string PracticeCharacteristic(string typeId) => typeId switch
    {
        "MashInput" or "ChargeRelease" => Characteristics.Strength,
        "PatternMemory" or "PrecisionChoice" => Characteristics.Perception,
        "BranchChoice" => Characteristics.Wisdom,
        "RhythmPulse" => Characteristics.Speed,
        "StealthNoise" or "LockPinSet" => Characteristics.Dexterity,
        _ => Characteristics.Dexterity
    };

    private static JsonObject? BuildPracticeConfig(string typeId, QtePracticeDifficultyPreset difficulty) => typeId switch
    {
        "BranchChoice" => new JsonObject
        {
            ["choiceGrade"] = difficulty.DifficultyId switch
            {
                "easy" => "success",
                "hard" => "fail",
                _ => "partial"
            }
        },
        "MashInput" => new JsonObject
        {
            ["keys"] = JsonStringArray("space"),
            ["durationMs"] = difficulty.DifficultyId == "hard" ? 2500 : difficulty.DifficultyId == "easy" ? 4000 : 3000,
            ["targetPresses"] = difficulty.DifficultyId == "hard" ? 16 : difficulty.DifficultyId == "easy" ? 8 : 12,
            ["partialThreshold"] = 0.5
        },
        "PatternMemory" => new JsonObject
        {
            ["alphabet"] = JsonStringArray("q", "w", "e", "space"),
            ["sequenceLength"] = difficulty.DifficultyId == "hard" ? 5 : difficulty.DifficultyId == "easy" ? 3 : 4,
            ["revealMs"] = difficulty.DifficultyId == "hard" ? 1800 : difficulty.DifficultyId == "easy" ? 3200 : 2500,
            ["inputTimeoutMs"] = difficulty.DifficultyId == "hard" ? 5000 : difficulty.DifficultyId == "easy" ? 8000 : 6500,
            ["allowedMistakes"] = difficulty.DifficultyId == "hard" ? 0 : 1
        },
        "RhythmPulse" => new JsonObject
        {
            ["pulseCount"] = difficulty.DifficultyId == "hard" ? 6 : difficulty.DifficultyId == "easy" ? 3 : 4,
            ["beatIntervalMs"] = difficulty.DifficultyId == "hard" ? 520 : difficulty.DifficultyId == "easy" ? 850 : 650,
            ["hitWindowMs"] = difficulty.DifficultyId == "hard" ? 90 : difficulty.DifficultyId == "easy" ? 160 : 120,
            ["allowedMisses"] = difficulty.DifficultyId == "hard" ? 0 : 1,
            ["patternVariation"] = difficulty.DifficultyId == "hard" ? "accelerating" : "steady"
        },
        "PrecisionChoice" => new JsonObject
        {
            ["correctChoiceId"] = "clear_path",
            ["timeoutMs"] = difficulty.DifficultyId == "hard" ? 3500 : difficulty.DifficultyId == "easy" ? 8000 : 6000,
            ["timeoutGrade"] = "fail",
            ["decoyHints"] = new JsonArray
            {
                new JsonObject
                {
                    ["choiceId"] = "rough_path",
                    ["hint"] = "Рискованный проход выглядит быстрее, но оставляет шумный след."
                }
            },
            ["choices"] = JsonChoiceArray()
        },
        "StealthNoise" => new JsonObject
        {
            ["durationMs"] = difficulty.DifficultyId == "hard" ? 8500 : difficulty.DifficultyId == "easy" ? 5000 : 6500,
            ["startingNoise"] = difficulty.DifficultyId == "hard" ? 24 : 12,
            ["dangerThreshold"] = 70,
            ["noiseDriftPerSecond"] = difficulty.DifficultyId == "hard" ? 12 : difficulty.DifficultyId == "easy" ? 6 : 9,
            ["recoveryPerInput"] = difficulty.DifficultyId == "hard" ? 10 : 12,
            ["allowedOverThresholdMs"] = difficulty.DifficultyId == "hard" ? 500 : difficulty.DifficultyId == "easy" ? 1100 : 800,
            ["recoveryKey"] = "space",
            ["recoveryLabel"] = "приглушить шум",
            ["warningLabel"] = "опасный шум",
            ["gradeThresholds"] = new JsonObject
            {
                ["successMaxNoise"] = difficulty.DifficultyId == "hard" ? 42 : 50,
                ["successMaxOverThresholdMs"] = 0,
                ["partialMaxNoise"] = 78,
                ["partialMaxOverThresholdMs"] = difficulty.DifficultyId == "hard" ? 600 : 900
            }
        },
        "LockPinSet" => new JsonObject
        {
            ["pinCount"] = difficulty.DifficultyId == "hard" ? 4 : difficulty.DifficultyId == "easy" ? 2 : 3,
            ["pinWindows"] = JsonPinWindowArray(difficulty.DifficultyId == "hard" ? 4 : difficulty.DifficultyId == "easy" ? 2 : 3),
            ["timerMs"] = difficulty.DifficultyId == "hard" ? 9000 : difficulty.DifficultyId == "easy" ? 16000 : 12000,
            ["pickDurability"] = difficulty.DifficultyId == "hard" ? 4 : 6,
            ["maxMistakes"] = difficulty.DifficultyId == "hard" ? 1 : 2,
            ["pinDriftPerSecond"] = difficulty.DifficultyId == "hard" ? 5 : difficulty.DifficultyId == "easy" ? 2 : 3,
            ["adjustKey"] = "q",
            ["setKey"] = "space",
            ["pinLabel"] = "штифт",
            ["durabilityLabel"] = "прочность отмычки",
            ["warningLabel"] = "замок вот-вот сорвётся",
            ["gradeThresholds"] = new JsonObject
            {
                ["successMaxTimeMs"] = difficulty.DifficultyId == "hard" ? 4500 : 6500,
                ["successMaxMistakes"] = 0,
                ["partialMaxTimeMs"] = difficulty.DifficultyId == "hard" ? 8500 : 11000,
                ["partialMaxMistakes"] = difficulty.DifficultyId == "hard" ? 1 : 2
            }
        },
        _ => null
    };

    private static JsonArray JsonStringArray(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private static JsonArray JsonChoiceArray()
    {
        var array = new JsonArray
        {
            new JsonObject { ["id"] = "clear_path", ["label"] = "Тихий проход", ["grade"] = "success" },
            new JsonObject { ["id"] = "rough_path", ["label"] = "Рискованный проход", ["grade"] = "partial" },
            new JsonObject { ["id"] = "bright_path", ["label"] = "Слишком яркая приманка", ["grade"] = "fail" }
        };
        return array;
    }

    private static JsonArray JsonPinWindowArray(int count)
    {
        var array = new JsonArray();
        for (var i = 0; i < count; i++)
        {
            var center = 20 + (i * 18);
            array.Add(new JsonObject
            {
                ["pin"] = i + 1,
                ["min"] = Math.Clamp(center, 5, 88),
                ["max"] = Math.Clamp(center + 10, 12, 98),
                ["label"] = $"{i + 1}-й штифт"
            });
        }

        return array;
    }

    private static QtePracticeCatalogEntry PracticeEntry(string typeId, string title, string description, string instructions) =>
        new()
        {
            TypeId = typeId,
            Title = title,
            Description = description,
            Instructions = instructions,
            Available = true,
            SupportedSurfaces = PracticeSupportedSurfaces,
            Difficulties = PracticeDifficulties
        };

    public sealed class QtePracticeCatalogEntry
    {
        public string TypeId { get; init; } = "";
        public string Title { get; init; } = "";
        public string Description { get; init; } = "";
        public string Instructions { get; init; } = "";
        public bool Available { get; init; }
        public string? UnavailableReason { get; init; }
        public IReadOnlyList<string> SupportedSurfaces { get; init; } = [];
        public IReadOnlyList<QtePracticeDifficultyPreset> Difficulties { get; init; } = [];
    }

    public sealed record QtePracticeDifficultyPreset(
        string DifficultyId,
        string Label,
        string Description,
        int BaseDifficulty);

    public sealed class QtePracticeAttemptState
    {
        public string AttemptId { get; init; } = "";
        public string TypeId { get; init; } = "";
        public string DifficultyId { get; init; } = "";
        public string State { get; set; } = "Active";
        public ActiveQteSceneState ActiveScene { get; init; } = new();
        public QteActionResolution? LastResolution { get; set; }
        public QteSceneCompletion? LastCompletion { get; set; }
        public string FeedbackTitle { get; set; } = "";
        public string Feedback { get; set; } = "";
        public string LocalScoreNotice { get; set; } = PracticeLocalScoreNotice;
    }

    private const string PracticeLocalScoreNotice =
        "Тренировочный счёт остаётся только в этой попытке: без наград, опыта, предметов, достижений, Ink Feathers и прогресса.";

    private enum PracticeConsoleNext
    {
        ChooseAnother,
        ChangeDifficulty,
        Exit
    }
}
