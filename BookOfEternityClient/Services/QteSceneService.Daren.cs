using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.UI;
using Spectre.Console;

namespace BookOfEternityClient.Services;

public sealed partial class QteSceneService
{
    private const string DarenRouteId = "daren_qte_showcase";
    private const string DarenScoreMetric = "normalized_score";
    private const string DarenStealthMetric = "stealth";
    private const string DarenLootMetric = "loot";
    private const string DarenPursuitMetric = "pursuit_control";
    private const string DarenEvidenceMetric = "evidence";
    private const string DarenHideoutMetric = "hideout_safety";
    private const string DarenTerminalOutcomeId = "daren_hideout_return";

    public static DarenShowcaseRouteDefinition GetDarenShowcaseRoute() =>
        new()
        {
            RouteId = DarenRouteId,
            Beats = BuildDarenBeats(),
            Offer = BuildDarenOffer(),
            EndingTiers = DarenQteRewardProfileService.EndingTiers
        };

    public DarenShowcaseAttemptState StartDarenShowcaseAttempt()
    {
        var offer = BuildDarenOffer();
        return new DarenShowcaseAttemptState
        {
            AttemptId = $"{DarenRouteId}_{Guid.NewGuid():N}",
            State = "Active",
            ActiveScene = new ActiveQteSceneState
            {
                Offer = offer,
                CurrentChapterId = offer.StartChapterId,
                AcceptedAtTurn = 0,
                ScoreState = BuildInitialScoreState(offer.ScoreModel)
            },
            FeedbackTitle = "Ограбление поместья Дареном",
            Feedback = "Дарен начинает отдельную QTE-вылазку за магическим посохом.",
            BoundaryNotice = DarenBoundaryNotice,
            RewardNotice = DarenRewardNotice
        };
    }

    public async Task<QteActionResolution> ResolveDarenShowcaseActionAsync(
        DarenShowcaseAttemptState attempt,
        string actionId,
        string? submittedGrade,
        DateTime? completedAtUtc = null)
    {
        if (!string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Daren showcase attempt is not active.");

        var active = attempt.ActiveScene ?? throw new InvalidOperationException("Daren showcase scene is not active.");
        var offer = active.Offer ?? throw new InvalidOperationException("Daren showcase route is missing.");
        var chapter = offer.Chapters.FirstOrDefault(item =>
            string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
        if (chapter == null)
            throw new InvalidOperationException($"Daren showcase chapter '{active.CurrentChapterId}' not found.");

        var action = chapter.Actions.FirstOrDefault(item =>
            string.Equals(item.ActionId, actionId, StringComparison.OrdinalIgnoreCase));
        if (action == null)
            throw new InvalidOperationException($"Daren showcase action '{actionId}' not found.");

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
            var scoreSummary = BuildFinalScoreSummary(offer.ScoreModel, active.ScoreState);
            var normalizedScore = ResolveDarenNormalizedScore(active.ScoreState);
            var ending = DarenQteRewardProfileService.ResolveEnding(
                reachedHideout: true,
                normalizedScore);
            var profileResult = await new DarenQteRewardProfileService(_fs)
                .RecordCompletionAsync(ending, completedAtUtc ?? DateTime.UtcNow);
            var rewardMessage = ending.GrantsReward
                ? profileResult.Message
                : ending.Summary;
            var summary = BuildDarenCompletionSummary(ending, rewardMessage, scoreSummary);

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
                    OutcomeId = ending.OutcomeId,
                    Summary = summary,
                    Response = new GameResponse
                    {
                        Response = $"{ending.DisplayName}. {ending.Summary} {rewardMessage}"
                    },
                    ScoreSummary = scoreSummary
                }
            };

            attempt.State = "Completed";
            attempt.LastCompletion = resolution.Completion;
            attempt.Ending = new DarenShowcaseEnding(
                ending.TierId,
                ending.DisplayName,
                ending.NormalizedScore,
                ending.InkFeatherBonus,
                ending.GrantsReward,
                rewardMessage);
            attempt.FeedbackTitle = ending.DisplayName;
            attempt.Feedback = $"{ending.Summary} {rewardMessage}";
        }
        else
        {
            if (string.IsNullOrWhiteSpace(target.NextChapterId))
                throw new InvalidOperationException($"Daren showcase action '{action.ActionId}' has no next chapter or terminal outcome.");

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

            attempt.FeedbackTitle = "Следующий участок";
            attempt.Feedback = resultText;
        }

        attempt.LastResolution = resolution;
        return resolution;
    }

    public async Task RunDarenShowcaseModeAsync()
    {
        while (true)
        {
            AnsiConsole.Clear();
            AnsiConsole.Write(new Panel(new Markup(
                "[bold cyan]Ограбление поместья Дареном[/]\n\n" +
                "Отдельная QTE-вылазка: обычная глава не меняется. Лучший итог сохраняет бонус Чернильных Перьев для будущей новой игры."))
            {
                Border = BoxBorder.Rounded,
                BorderStyle = new Style(Color.Cyan1),
                Padding = new Padding(2, 1),
                Expand = true
            });

            var selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Действие:[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("Начать вылазку", "Выйти"));
            if (!selected.Contains("Начать", StringComparison.OrdinalIgnoreCase))
                return;

            var attempt = StartDarenShowcaseAttempt();
            while (string.Equals(attempt.State, "Active", StringComparison.OrdinalIgnoreCase))
            {
                var active = attempt.ActiveScene;
                var offer = active.Offer!;
                var chapter = offer.Chapters.First(item =>
                    string.Equals(item.ChapterId, active.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
                ShowChapterPrelude(offer, chapter, active.ScoreState);

                var action = chapter.Actions.Single();
                var grade = await RunCheckAsync(action);
                var resolution = await ResolveDarenShowcaseActionAsync(attempt, action.ActionId, GradeKey(grade));
                await ShowIntermediateResultAsync(offer, chapter, action, grade);

                if (resolution.Completion == null)
                    continue;

                RenderDarenCompletion(attempt, resolution.Completion);
            }

            var next = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("[bold]Что дальше?[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .AddChoices("Повторить вылазку", "Выйти"));
            if (!next.Contains("Повторить", StringComparison.OrdinalIgnoreCase))
                return;
        }
    }

    private static IReadOnlyList<DarenShowcaseBeat> BuildDarenBeats() =>
    [
        new("approach_manor", "Подступ к поместью", "Дарен выбирает тихий подступ к стене поместья."),
        new("gadget_infiltration", "Крюк и леска", "Дарен запускает складной крюк к балкону сторожевой башни."),
        new("stealth_crossing", "Галерея без звука", "Дарен гасит шум плаща и шагает мимо сонных стражей."),
        new("lock_pick", "Замок кабинета", "Дарен выставляет штифты старого замка у кабинета управляющего."),
        new("rune_memory", "Руны на дверце", "Дарен запоминает вспыхнувший узор защитных рун."),
        new("physical_pressure", "Тяжёлая решётка", "Дарен удерживает решётку, пока футляр с посохом выходит из ниши."),
        new("timed_rhythm", "Пульс сигнализации", "Дарен двигается в паузах между ударами сигнального кристалла."),
        new("route_decision", "Развилка в оранжерее", "Дарен выбирает, куда уходить с добычей."),
        new("staff_theft", "Кража посоха", "Дарен балансирует посох на ремне, не задев звонкие подвески."),
        new("pursuit", "Первый рывок", "Дарен ловит момент, чтобы сорваться от проснувшейся погони."),
        new("chase_chain", "Цепочка дворов", "Дарен повторяет связку прыжков и поворотов через задние дворы."),
        new("hideout_return", "Убежище под мостом", "Дарен решает, как спрятать посох и зачистить след.")
    ];

    private static QteOffer BuildDarenOffer()
    {
        var beats = BuildDarenBeats();
        var chapters = new List<QteChapter>();
        for (var index = 0; index < beats.Count; index++)
        {
            var beat = beats[index];
            var nextBeat = index + 1 < beats.Count ? beats[index + 1].BeatId : null;
            chapters.Add(new QteChapter
            {
                ChapterId = beat.BeatId,
                Title = beat.Title,
                Narrative = beat.PlayerText,
                Actions = [BuildDarenAction(beat.BeatId, nextBeat)]
            });
        }

        return new QteOffer
        {
            QteId = DarenRouteId,
            Title = "Ограбление поместья Дареном",
            OfferText = "Дарен, хитрый вор из переулков Вечной Книги, идёт за магическим посохом в запертое поместье.",
            IntroNarrative = "Эта вылазка авторская и клиентская: она не отправляет обычный ход GM и не меняет текущую главу.",
            DeclineHint = "Можно выйти в меню без последствий для обычной игры.",
            CinematicJustification = "Client-owned QTE showcase route for learning every landed QTE mechanic.",
            StartChapterId = "approach_manor",
            Chapters = chapters,
            TerminalOutcomes =
            [
                new QteTerminalOutcome
                {
                    OutcomeId = DarenTerminalOutcomeId,
                    Title = "Возвращение в убежище",
                    FinalNarrative = "Дарен добирается до убежища; итог зависит от чистоты вылазки и погони.",
                    GmSummary = "Client-owned Daren showcase completion; not a GM-authored campaign QTE offer and no campaign state mutation.",
                    ResponseFragment = new JsonObject
                    {
                        ["response"] = "Дарен добирается до убежища; итог зависит от чистоты вылазки и погони."
                    }
                }
            ],
            ScoreModel = BuildDarenScoreModel()
        };
    }

    private static QteAction BuildDarenAction(string beatId, string? nextBeatId)
    {
        var isTerminal = string.IsNullOrWhiteSpace(nextBeatId);
        var routing = new QteRouting
        {
            Success = isTerminal
                ? new QteBranchTarget { TerminalOutcomeId = DarenTerminalOutcomeId }
                : new QteBranchTarget { NextChapterId = nextBeatId },
            Partial = isTerminal
                ? new QteBranchTarget { TerminalOutcomeId = DarenTerminalOutcomeId }
                : new QteBranchTarget { NextChapterId = nextBeatId },
            Fail = isTerminal
                ? new QteBranchTarget { TerminalOutcomeId = DarenTerminalOutcomeId }
                : new QteBranchTarget { NextChapterId = nextBeatId }
        };

        return beatId switch
        {
            "approach_manor" => Action(
                beatId,
                "Выбрать тень у старой липы",
                "BranchChoice",
                Characteristics.Wisdom,
                2,
                DarenBranchChoiceConfig("success"),
                routing,
                "Дарен находит слепую зону между фонарями.",
                "Дарен проходит, но задевает сухую ветку.",
                "Дарен теряет время у освещённой калитки.",
                DarenScoreDeltas(stealth: 4, evidence: -2)),
            "gadget_infiltration" => Action(
                beatId,
                "Запустить складной крюк",
                "ChargeRelease",
                Characteristics.Dexterity,
                3,
                null,
                routing,
                "Крюк цепляется за балкон почти без звука.",
                "Крюк держит, но леска звенит по камню.",
                "Крюк срывается и будит дворовую собаку.",
                DarenScoreDeltas(stealth: 3, pursuit: 2)),
            "stealth_crossing" => Action(
                beatId,
                "Пройти галерею без шума",
                "StealthNoise",
                Characteristics.Dexterity,
                3,
                DarenStealthNoiseConfig(),
                routing,
                "Галерея остаётся тихой.",
                "Страж шевелится, но снова засыпает.",
                "Доска скрипит, и в дальнем крыле вспыхивает фонарь.",
                DarenScoreDeltas(stealth: 5, evidence: -2)),
            "lock_pick" => Action(
                beatId,
                "Выставить штифты замка",
                "LockPinSet",
                Characteristics.Dexterity,
                3,
                DarenLockPinSetConfig(),
                routing,
                "Замок кабинета сдаётся сухим щелчком.",
                "Замок открывается, но отмычка оставляет след.",
                "Замок щёлкает слишком громко.",
                DarenScoreDeltas(stealth: 3, evidence: -1)),
            "rune_memory" => Action(
                beatId,
                "Повторить узор защитных рун",
                "PatternMemory",
                Characteristics.Perception,
                3,
                DarenPatternMemoryConfig(),
                routing,
                "Руны гаснут в правильном порядке.",
                "Одна руна трескается, но дверь остаётся открытой.",
                "Руны вспыхивают тревожным светом.",
                DarenScoreDeltas(loot: 3, evidence: -1)),
            "physical_pressure" => Action(
                beatId,
                "Удержать тяжёлую решётку",
                "MashInput",
                Characteristics.Strength,
                3,
                DarenMashInputConfig(),
                routing,
                "Решётка держится ровно до последней секунды.",
                "Решётка проседает, но посох уже свободен.",
                "Решётка грохочет по камню.",
                DarenScoreDeltas(loot: 4, pursuit: 2)),
            "timed_rhythm" => Action(
                beatId,
                "Двигаться между ударами кристалла",
                "RhythmPulse",
                Characteristics.Speed,
                3,
                DarenRhythmPulseConfig(),
                routing,
                "Дарен проходит точно в паузах сигнализации.",
                "Один шаг попадает на край звона.",
                "Сигнальный кристалл режет тишину.",
                DarenScoreDeltas(stealth: 4, pursuit: 2)),
            "route_decision" => Action(
                beatId,
                "Выбрать выход через оранжерею",
                "PrecisionChoice",
                Characteristics.Perception,
                3,
                DarenPrecisionChoiceConfig(),
                routing,
                "Дарен выбирает влажный проход без следов.",
                "Дарен уходит быстрым путём, но листья показывают направление.",
                "Дарен бросается к яркой арке и попадает в свет.",
                DarenScoreDeltas(pursuit: 4, evidence: -2)),
            "staff_theft" => Action(
                beatId,
                "Удержать посох на ремне",
                "BalanceMeter",
                Characteristics.Dexterity,
                4,
                null,
                routing,
                "Посох ложится на ремень без звона.",
                "Посох звякает раз, но Дарен ловит равновесие.",
                "Посох бьёт по подвескам и зовёт погоню.",
                DarenScoreDeltas(loot: 5, stealth: 2)),
            "pursuit" => Action(
                beatId,
                "Рвануть в окно погони",
                "TimingBar",
                Characteristics.Speed,
                4,
                null,
                routing,
                "Дарен срывается ровно до того, как стражи смыкают двор.",
                "Дарен проскальзывает, но плащ мелькает в свете.",
                "Дарен теряет шаг, и погоня садится на хвост.",
                DarenScoreDeltas(pursuit: 5, stealth: 1)),
            "chase_chain" => Action(
                beatId,
                "Повторить цепочку дворов",
                "PromptChain",
                Characteristics.Speed,
                4,
                null,
                routing,
                "Цепочка прыжков выходит чисто.",
                "Дарен сбивает темп, но не падает.",
                "Дарен цепляет бочку и оставляет громкий след.",
                DarenScoreDeltas(pursuit: 4, evidence: -2)),
            "hideout_return" => Action(
                beatId,
                "Спрятать посох и зачистить след",
                "BranchChoice",
                Characteristics.Wisdom,
                3,
                DarenBranchChoiceConfig("success"),
                routing,
                "Посох исчезает в тайнике под мостом.",
                "Дарен прячет посох, но оставляет поспешный след.",
                "Убежище шумно принимает добычу.",
                DarenScoreDeltas(hideout: 6, evidence: -3)),
            _ => throw new InvalidOperationException($"Unknown Daren beat '{beatId}'.")
        };
    }

    private static QteAction Action(
        string beatId,
        string label,
        string checkType,
        string characteristic,
        int baseDifficulty,
        JsonObject? config,
        QteRouting routing,
        string successText,
        string partialText,
        string failText,
        Dictionary<string, List<QteScoreDelta>> scoreDeltas) =>
        new()
        {
            ActionId = $"{beatId}_action",
            Label = label,
            Check = new QteCheck
            {
                Type = checkType,
                BaseDifficulty = baseDifficulty,
                PrimaryCharacteristic = characteristic,
                Config = config
            },
            Routing = routing,
            SuccessText = successText,
            PartialText = partialText,
            FailText = failText,
            ScoreDeltas = scoreDeltas
        };

    private static QteScoreModel BuildDarenScoreModel() =>
        new()
        {
            Metrics =
            [
                new QteScoreMetricDefinition { Id = DarenScoreMetric, Label = "Счёт вылазки", Initial = 35, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenStealthMetric, Label = "Скрытность", Initial = 50, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenLootMetric, Label = "Добыча", Initial = 50, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenPursuitMetric, Label = "Контроль погони", Initial = 50, Min = 0, Max = 100, Visibility = "always" },
                new QteScoreMetricDefinition { Id = DarenEvidenceMetric, Label = "Улики", Initial = 35, Min = 0, Max = 100, Visibility = "hidden" },
                new QteScoreMetricDefinition { Id = DarenHideoutMetric, Label = "Безопасность убежища", Initial = 50, Min = 0, Max = 100, Visibility = "always" }
            ],
            RankOrder = ["perfect_shadow", "clean_heist", "broken_trail", "shadow_on_the_run", "no_reward_failure"],
            Ranks =
            [
                DarenRank("perfect_shadow", "Идеальная тень", "Дарен уходит с посохом чисто, быстро и без следов.", 90),
                DarenRank("clean_heist", "Чистая кража", "Посох добыт, погоня отрезана, последствия управляемы.", 75),
                DarenRank("broken_trail", "Сорванный след", "Дарен сбивает погоню, но часть следов остаётся.", 55),
                DarenRank("shadow_on_the_run", "Тень в бегах", "Дарен выжил и ушёл, но вылазка получилась грязной.", 40),
                new QteScoreRankDefinition
                {
                    Id = "no_reward_failure",
                    Label = "Провал вылазки",
                    Summary = "Безопасный итог не достигнут: постоянная награда не записывается.",
                    Fallback = true
                }
            ]
        };

    private static QteScoreRankDefinition DarenRank(string id, string label, string summary, int threshold) =>
        new()
        {
            Id = id,
            Label = label,
            Summary = summary,
            AllOf = [new QteScoreThreshold { Metric = DarenScoreMetric, Op = ">=", Value = threshold }]
        };

    private static Dictionary<string, List<QteScoreDelta>> DarenScoreDeltas(
        int stealth = 0,
        int loot = 0,
        int pursuit = 0,
        int evidence = 0,
        int hideout = 0) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["success"] = DarenGradeDeltas(5, stealth, loot, pursuit, evidence, hideout),
            ["partial"] = DarenGradeDeltas(2, Math.Max(0, stealth / 2), Math.Max(0, loot / 2), Math.Max(0, pursuit / 2), evidence / 2, Math.Max(0, hideout / 2)),
            ["fail"] = DarenGradeDeltas(-8, -Math.Max(3, stealth), -Math.Max(2, loot), -Math.Max(2, pursuit), Math.Max(4, Math.Abs(evidence)), -Math.Max(2, hideout))
        };

    private static List<QteScoreDelta> DarenGradeDeltas(
        int normalizedScore,
        int stealth,
        int loot,
        int pursuit,
        int evidence,
        int hideout) =>
    [
        new QteScoreDelta { Metric = DarenScoreMetric, Delta = normalizedScore },
        new QteScoreDelta { Metric = DarenStealthMetric, Delta = stealth },
        new QteScoreDelta { Metric = DarenLootMetric, Delta = loot },
        new QteScoreDelta { Metric = DarenPursuitMetric, Delta = pursuit },
        new QteScoreDelta { Metric = DarenEvidenceMetric, Delta = evidence },
        new QteScoreDelta { Metric = DarenHideoutMetric, Delta = hideout }
    ];

    private static JsonObject DarenBranchChoiceConfig(string grade) =>
        new()
        {
            ["choiceGrade"] = grade
        };

    private static JsonObject DarenMashInputConfig() =>
        new()
        {
            ["keys"] = DarenStringArray("space"),
            ["durationMs"] = 3200,
            ["targetPresses"] = 13,
            ["partialThreshold"] = 0.55
        };

    private static JsonObject DarenPatternMemoryConfig() =>
        new()
        {
            ["alphabet"] = DarenStringArray("q", "w", "e", "space"),
            ["sequenceLength"] = 4,
            ["revealMs"] = 2400,
            ["inputTimeoutMs"] = 6500,
            ["allowedMistakes"] = 1
        };

    private static JsonObject DarenRhythmPulseConfig() =>
        new()
        {
            ["pulseCount"] = 5,
            ["beatIntervalMs"] = 640,
            ["hitWindowMs"] = 125,
            ["allowedMisses"] = 1,
            ["patternVariation"] = "swing"
        };

    private static JsonObject DarenPrecisionChoiceConfig()
    {
        var choices = new JsonArray
        {
            new JsonObject
            {
                ["id"] = "wet_glass",
                ["label"] = "Влажная оранжерея",
                ["grade"] = "success",
                ["description"] = "Следы смоет вода из разбитых трубок.",
                ["hint"] = "Вода и тень скрывают направление."
            },
            new JsonObject
            {
                ["id"] = "servant_gate",
                ["label"] = "Служебная калитка",
                ["grade"] = "partial",
                ["description"] = "Быстрее, но на земле остаётся грязный след."
            },
            new JsonObject
            {
                ["id"] = "bright_arch",
                ["label"] = "Освещённая арка",
                ["grade"] = "fail",
                ["description"] = "Прямой выход виден из караульной."
            }
        };

        return new JsonObject
        {
            ["correctChoiceId"] = "wet_glass",
            ["timeoutMs"] = 6000,
            ["timeoutGrade"] = "fail",
            ["choices"] = choices,
            ["decoyHints"] = new JsonArray
            {
                new JsonObject
                {
                    ["choiceId"] = "servant_gate",
                    ["hint"] = "Быстрый проход не всегда самый чистый."
                }
            }
        };
    }

    private static JsonObject DarenStealthNoiseConfig() =>
        new()
        {
            ["durationMs"] = 6500,
            ["startingNoise"] = 14,
            ["dangerThreshold"] = 70,
            ["noiseDriftPerSecond"] = 9,
            ["recoveryPerInput"] = 12,
            ["allowedOverThresholdMs"] = 800,
            ["recoveryKey"] = "space",
            ["recoveryLabel"] = "приглушить шаг",
            ["warningLabel"] = "страж слышит шум",
            ["gradeThresholds"] = new JsonObject
            {
                ["successMaxNoise"] = 48,
                ["successMaxOverThresholdMs"] = 0,
                ["partialMaxNoise"] = 76,
                ["partialMaxOverThresholdMs"] = 850
            }
        };

    private static JsonObject DarenLockPinSetConfig() =>
        new()
        {
            ["pinCount"] = 3,
            ["pinWindows"] = new JsonArray
            {
                new JsonObject { ["pin"] = 1, ["min"] = 18, ["max"] = 32, ["label"] = "нижний штифт" },
                new JsonObject { ["pin"] = 2, ["min"] = 44, ["max"] = 58, ["label"] = "средний штифт" },
                new JsonObject { ["pin"] = 3, ["min"] = 68, ["max"] = 82, ["label"] = "верхний штифт" }
            },
            ["timerMs"] = 12000,
            ["pickDurability"] = 6,
            ["maxMistakes"] = 2,
            ["pinDriftPerSecond"] = 3,
            ["adjustKey"] = "q",
            ["setKey"] = "space",
            ["pinLabel"] = "штифт",
            ["durabilityLabel"] = "прочность отмычки",
            ["warningLabel"] = "замок шумит",
            ["gradeThresholds"] = new JsonObject
            {
                ["successMaxTimeMs"] = 6500,
                ["successMaxMistakes"] = 0,
                ["partialMaxTimeMs"] = 11000,
                ["partialMaxMistakes"] = 2
            }
        };

    private static JsonArray DarenStringArray(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
            array.Add(value);
        return array;
    }

    private static int ResolveDarenNormalizedScore(QteScoreState? scoreState)
    {
        var metric = scoreState?.Metrics.FirstOrDefault(item =>
            string.Equals(item.Id, DarenScoreMetric, StringComparison.OrdinalIgnoreCase));
        return metric == null ? 0 : Math.Clamp((int)Math.Round(metric.Value), 0, 100);
    }

    private static string BuildDarenCompletionSummary(
        DarenEndingResult ending,
        string rewardMessage,
        QteScoreSummary? scoreSummary)
    {
        var rank = scoreSummary?.Rank?.Label;
        var rankText = string.IsNullOrWhiteSpace(rank) ? ending.DisplayName : rank;
        return $"{ending.DisplayName}: {ending.Summary} Счёт вылазки {ending.NormalizedScore}/100. Ранг: {rankText}. {rewardMessage}";
    }

    private static void RenderDarenCompletion(DarenShowcaseAttemptState attempt, QteSceneCompletion completion)
    {
        var lines = new List<string>
        {
            completion.Response.Response ?? completion.Summary,
            "",
            DarenBoundaryNotice,
            DarenRewardNotice
        };
        if (attempt.Ending is { } ending)
            lines.Add($"Итог: {ending.DisplayName}, счёт {ending.NormalizedScore}/100, бонус +{ending.InkFeatherBonus}.");

        AnsiConsole.Write(new Panel(new Markup(Markup.Escape(string.Join("\n", lines))))
        {
            Header = new PanelHeader(" Итог вылазки Дарена "),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Green),
            Padding = new Padding(2, 1),
            Expand = true
        });
    }

    public sealed class DarenShowcaseRouteDefinition
    {
        public string RouteId { get; init; } = DarenRouteId;
        public IReadOnlyList<DarenShowcaseBeat> Beats { get; init; } = [];
        public QteOffer Offer { get; init; } = new();
        public IReadOnlyList<DarenEndingTier> EndingTiers { get; init; } = [];
    }

    public sealed record DarenShowcaseBeat(string BeatId, string Title, string PlayerText);

    public sealed class DarenShowcaseAttemptState
    {
        public string AttemptId { get; init; } = "";
        public string State { get; set; } = "Active";
        public ActiveQteSceneState ActiveScene { get; init; } = new();
        public QteActionResolution? LastResolution { get; set; }
        public QteSceneCompletion? LastCompletion { get; set; }
        public DarenShowcaseEnding? Ending { get; set; }
        public string FeedbackTitle { get; set; } = "";
        public string Feedback { get; set; } = "";
        public string BoundaryNotice { get; init; } = DarenBoundaryNotice;
        public string RewardNotice { get; init; } = DarenRewardNotice;
    }

    public sealed record DarenShowcaseEnding(
        string? TierId,
        string DisplayName,
        int NormalizedScore,
        int InkFeatherBonus,
        bool GrantsReward,
        string RewardMessage);

    private const string DarenBoundaryNotice =
        "Это отдельная авторская QTE-вылазка: обычная глава, обычные ходы и свободная тренировка QTE не меняются.";

    private const string DarenRewardNotice =
        "Лучший итог Дарена запоминается книгой и даёт Чернильные Перья только при создании будущей новой игры.";
}
