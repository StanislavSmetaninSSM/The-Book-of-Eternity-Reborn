using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.WebUi;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class DarenQteShowcaseTests : IDisposable
{
    private static readonly string[] OriginalHeistBeatIds =
    [
        "approach_manor",
        "gadget_infiltration",
        "stealth_crossing",
        "lock_pick",
        "rune_memory",
        "physical_pressure",
        "timed_rhythm",
        "route_decision",
        "staff_theft",
        "pursuit",
        "chase_chain",
        "hideout_return"
    ];

    private static readonly string[] RequiredDialogueBeatIds =
    [
        "informant_parley",
        "guard_interrogation",
        "ward_steward_parley"
    ];

    private static readonly string[] RequiredBeatIds =
    [
        "approach_manor",
        "informant_parley",
        "gadget_infiltration",
        "stealth_crossing",
        "guard_interrogation",
        "lock_pick",
        "rune_memory",
        "ward_steward_parley",
        "physical_pressure",
        "timed_rhythm",
        "route_decision",
        "staff_theft",
        "pursuit",
        "chase_chain",
        "hideout_return"
    ];

    private static readonly string[] RequiredQteTypes =
    [
        "TimingBar",
        "PromptChain",
        "BalanceMeter",
        "ChargeRelease",
        "BranchChoice",
        "MashInput",
        "PatternMemory",
        "RhythmPulse",
        "PrecisionChoice",
        "StealthNoise",
        "LockPinSet"
    ];

    private static readonly string[] RequiredNarrativeArcStages =
    [
        "preparation",
        "approach",
        "infiltration",
        "reconnaissance",
        "security",
        "complication",
        "theft",
        "alarm",
        "chase",
        "hideout",
        "epilogue"
    ];

    private static readonly string[] RequiredNarrativeCastSlots =
    [
        "contact_informant",
        "estate_staff_guard",
        "magical_security_authority",
        "pursuit_figure"
    ];

    private static readonly IReadOnlyDictionary<string, string> RequiredCastNamesBySlotId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["contact_informant"] = "Мира Ночная Нить",
            ["estate_staff_guard"] = "Лукьян Седой Ключник",
            ["magical_security_authority"] = "Ренара Вардовая",
            ["pursuit_figure"] = "капитан Орвальд Шпиль"
        };

    private static readonly IReadOnlyDictionary<string, string> RequiredDialogueNpcByBeatId =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["informant_parley"] = "Мира Ночная Нить",
            ["guard_interrogation"] = "Лукьян Седой Ключник",
            ["ward_steward_parley"] = "Ренара Вардовая"
        };

    private static readonly string[] DialogueChoiceCheckTypes =
    [
        "BranchChoice",
        "PrecisionChoice"
    ];

    private static readonly string[] ExistingRiskMetricIds =
    [
        "stealth",
        "loot",
        "pursuit_control",
        "evidence",
        "hideout_safety"
    ];

    private static readonly string[] StrongConsequenceTerms =
    [
        "чист",
        "без след",
        "не остав",
        "молчит",
        "сбивает",
        "теряет"
    ];

    private static readonly string[] PartialConsequenceTerms =
    [
        "цен",
        "задерж",
        "сомнен",
        "след",
        "запом",
        "царап",
        "шум"
    ];

    private static readonly string[] PoorConsequenceTerms =
    [
        "тревог",
        "погон",
        "свидетел",
        "ули",
        "опас",
        "обход",
        "запом",
        "выда"
    ];

    private static readonly string[] PoorOutcomePressureTerms =
    [
        "тревог",
        "погон",
        "свидетел",
        "след",
        "шум",
        "задерж",
        "обход",
        "опас",
        "ули",
        "фонар",
        "Орвальд",
        "Ренар",
        "Лукьян"
    ];

    private static readonly string[] SocialReactionTerms =
    [
        "Мира",
        "Лукьян",
        "Ренара",
        "шеп",
        "отвеч",
        "подозр",
        "страж",
        "ключник",
        "дом",
        "след"
    ];

    private static readonly string[] LaterSocialEchoTerms =
    [
        "Мира",
        "Лукьян",
        "Ренара",
        "Орвальд",
        "ключник",
        "донос",
        "подозр",
        "свидетел"
    ];

    private static readonly int[] FutureDarenIssueIds = [957, 958, 959, 960, 961];

    private static readonly string[] ForbiddenPlayerFacingTechnicalTerms =
    [
        "GM",
        "DTO",
        "API",
        "endpoint",
        "debug",
        "Spec Kit",
        "manual-grade",
        "client-owned"
    ];

    private static readonly string[] ForbiddenStoryCopyTerms =
    [
        "debug",
        "tutorial",
        "endpoint",
        "DTO",
        "manual-grade",
        "Spec Kit",
        "QTE"
    ];

    private static readonly IReadOnlyDictionary<string, string[][]> ChapterSignalGroups =
        new Dictionary<string, string[][]>(StringComparer.OrdinalIgnoreCase)
        {
            ["approach_manor"] =
            [
                ["помест", "стен"],
                ["фонар", "патрул", "страж"],
                ["тень", "подступ", "калит"]
            ],
            ["gadget_infiltration"] =
            [
                ["балкон", "башн"],
                ["леск", "камн", "двор"],
                ["крюк", "цепля", "подъем"]
            ],
            ["stealth_crossing"] =
            [
                ["галер", "пол", "портрет"],
                ["страж", "сон", "фонар"],
                ["шум", "тиш", "шаг"]
            ],
            ["informant_parley"] =
            [
                ["Мира", "нить", "информ"],
                ["слух", "смен", "страж"],
                ["ответ", "шеп", "договор"]
            ],
            ["guard_interrogation"] =
            [
                ["Лукьян", "ключник", "страж"],
                ["галер", "служ", "двер"],
                ["вопрос", "ответ", "подозр"]
            ],
            ["lock_pick"] =
            [
                ["кабин", "замок", "двер"],
                ["след", "шум", "страж"],
                ["штифт", "отмыч", "откры"]
            ],
            ["rune_memory"] =
            [
                ["рун", "дверц", "стекл"],
                ["защит", "сигнал", "тревож"],
                ["запом", "узор", "повтор"]
            ],
            ["ward_steward_parley"] =
            [
                ["Ренара", "вард", "дом"],
                ["руна", "сигнал", "печать"],
                ["ответ", "посох", "управ"]
            ],
            ["physical_pressure"] =
            [
                ["решет", "ниша", "футляр"],
                ["грох", "крыл", "тяж"],
                ["удерж", "посох", "освобод"]
            ],
            ["timed_rhythm"] =
            [
                ["кристалл", "коридор"],
                ["сигнал", "тревог", "свет"],
                ["пауза", "ритм", "двиг"]
            ],
            ["route_decision"] =
            [
                ["оранжер", "выход"],
                ["след", "свет", "погон"],
                ["выб", "уйти", "путь"]
            ],
            ["staff_theft"] =
            [
                ["посох", "кольц", "подвес"],
                ["звон", "тревог", "погон"],
                ["баланс", "рем", "снять"]
            ],
            ["pursuit"] =
            [
                ["окн", "двор", "зал"],
                ["страж", "погон", "крик"],
                ["рывок", "момент", "выскоч"]
            ],
            ["chase_chain"] =
            [
                ["двор", "алле", "стен", "телег"],
                ["след", "преслед", "погон"],
                ["цепоч", "прыж", "поворот"]
            ],
            ["hideout_return"] =
            [
                ["мост", "убежищ", "тайник"],
                ["след", "погон", "опас"],
                ["спрят", "зачист", "посох"]
            ]
        };

    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly QteSceneService _qte;
    private readonly DarenQteRewardProfileService _profile;
    private readonly QteWebInteractionService _web;

    public DarenQteShowcaseTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-daren-qte-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();

        var settings = new GameSettings();
        var stateManager = new StateManager(_fs, settings, NullLogger<StateManager>.Instance);
        var characteristics = new CharacteristicsService(_fs, stateManager, NullLogger<CharacteristicsService>.Instance);
        _qte = new QteSceneService(
            _fs,
            settings,
            characteristics,
            null!,
            null!,
            null!,
            null!,
            null!,
            stateManager,
            NullLogger<QteSceneService>.Instance);
        _profile = new DarenQteRewardProfileService(_fs);
        _web = new QteWebInteractionService(_fs, _qte);
    }

    [Fact]
    public void DarenRouteDefinition_IncludesRequiredStoryBeatsAndQteTypes()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        Assert.Equal("daren_qte_showcase", route.RouteId);
        Assert.Equal(RequiredBeatIds, route.Beats.Select(beat => beat.BeatId));
        AssertOrderedSubsequence(OriginalHeistBeatIds, route.Beats.Select(beat => beat.BeatId), "Daren route beats");
        Assert.All(route.Beats, beat =>
        {
            Assert.False(string.IsNullOrWhiteSpace(beat.Title));
            Assert.False(string.IsNullOrWhiteSpace(beat.PlayerText));
            Assert.DoesNotContain("GM", beat.PlayerText, StringComparison.OrdinalIgnoreCase);
        });

        var routeTypes = route.Offer.Chapters
            .SelectMany(chapter => chapter.Actions)
            .Select(action => action.Check.Type)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(type => type, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(RequiredQteTypes.OrderBy(type => type, StringComparer.OrdinalIgnoreCase), routeTypes);
        Assert.Equal("approach_manor", route.Offer.StartChapterId);
        Assert.Contains("Дарен", route.Offer.Title, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DarenChapters_HaveLiterarySceneProseForEveryBeat()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        Assert.Equal(RequiredBeatIds, route.Offer.Chapters.Select(chapter => chapter.ChapterId));
        AssertOrderedSubsequence(OriginalHeistBeatIds, route.Offer.Chapters.Select(chapter => chapter.ChapterId), "Daren route chapters");
        foreach (var chapter in route.Offer.Chapters)
            AssertChapterNarrativeLooksLikeSceneProse(chapter.ChapterId, chapter.Narrative);
    }

    [Fact]
    public void DarenActionResultText_ReadsAsTransitionProse()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var chapter in route.Offer.Chapters)
        {
            var action = Assert.Single(chapter.Actions);
            AssertActionResultLooksLikeTransition(chapter.ChapterId, action.ActionId, "success", action.SuccessText);
            AssertActionResultLooksLikeTransition(chapter.ChapterId, action.ActionId, "partial", action.PartialText);
            AssertActionResultLooksLikeTransition(chapter.ChapterId, action.ActionId, "fail", action.FailText);
        }
    }

    [Fact]
    public void DarenKeyQteResults_HaveDistinctBranchConsequenceSignals()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var beatId in new[]
        {
            "stealth_crossing",
            "lock_pick",
            "rune_memory",
            "timed_rhythm",
            "pursuit",
            "hideout_return"
        })
        {
            var (_, action) = RequiredChapterAction(route.Offer, beatId);

            Assert.Equal(3, new[] { action.SuccessText, action.PartialText, action.FailText }
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count());
            AssertContainsAny($"{beatId} success consequence", action.SuccessText ?? "", StrongConsequenceTerms);
            AssertContainsAny($"{beatId} partial consequence", action.PartialText ?? "", PartialConsequenceTerms);
            AssertContainsAny($"{beatId} fail consequence", action.FailText ?? "", PoorConsequenceTerms);
        }
    }

    [Fact]
    public void DarenLaterScenes_EchoSeveralEarlierChoicesAndResults()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var (sourceBeatId, laterBeatId, terms) in new (string SourceBeatId, string LaterBeatId, string[] Terms)[]
        {
            ("informant_parley", "guard_interrogation", ["Мир", "фраз", "слух"]),
            ("guard_interrogation", "pursuit", ["Лукьян", "ключник", "свидетел"]),
            ("lock_pick", "staff_theft", ["замок", "царап", "наклад"]),
            ("ward_steward_parley", "physical_pressure", ["Ренар", "голос", "вард"]),
            ("route_decision", "chase_chain", ["оранжер", "калитк", "арка"]),
            ("pursuit", "hideout_return", ["Орвальд", "капитан", "погон"])
        })
        {
            var sourceIndex = FindChapterIndex(route.Offer, sourceBeatId);
            var laterIndex = FindChapterIndex(route.Offer, laterBeatId);
            Assert.True(sourceIndex < laterIndex, $"Daren carry-forward echo '{laterBeatId}' must occur after '{sourceBeatId}'.");

            var (laterChapter, _) = RequiredChapterAction(route.Offer, laterBeatId);
            AssertContainsAny($"{sourceBeatId} echo in {laterBeatId}", BuildPlayerFacingRouteText([laterChapter]), terms);
        }
    }

    [Fact]
    public void DarenDialoguePlanningChoices_ReachLaterConsequenceProse()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var (dialogueBeatId, terms) in new (string DialogueBeatId, string[] Terms)[]
        {
            ("informant_parley", ["Мир", "фраз", "слух"]),
            ("guard_interrogation", ["Лукьян", "ключник", "свидетел"]),
            ("ward_steward_parley", ["Ренар", "дом", "вард"])
        })
        {
            var dialogueIndex = FindChapterIndex(route.Offer, dialogueBeatId);
            var laterText = BuildPlayerFacingRouteText(route.Offer.Chapters.Skip(dialogueIndex + 1));

            AssertContainsAny($"{dialogueBeatId} later consequence", laterText, terms);
        }
    }

    [Fact]
    public void DarenNonTerminalPoorOutcomes_KeepPlayMovingWithSpecificPressure()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var beatId in new[]
        {
            "gadget_infiltration",
            "route_decision",
            "staff_theft",
            "pursuit",
            "chase_chain"
        })
        {
            var (_, action) = RequiredChapterAction(route.Offer, beatId);

            Assert.False(string.IsNullOrWhiteSpace(action.Routing.Fail.NextChapterId),
                $"Daren non-terminal poor outcome '{beatId}' should continue to a later scene.");
            Assert.True(string.IsNullOrWhiteSpace(action.Routing.Fail.TerminalOutcomeId),
                $"Daren non-terminal poor outcome '{beatId}' should not collapse into a terminal outcome.");
            AssertContainsAny($"{beatId} fail pressure", action.FailText ?? "", PoorOutcomePressureTerms);
            AssertFailureScoreDeltasIncreasePressure(beatId, action);
        }
    }

    [Fact]
    public void DarenBranchConsequences_StayInSharedRouteAndSpineContract()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var spine = LoadDarenNarrativeSpine();
        var darenSource = ReadRepoFile("BookOfEternityClient", "Services", "QteSceneService.Daren.cs");

        Assert.Equal("daren_qte_showcase", route.RouteId);
        Assert.Contains(959, RequiredIntArray(spine, "sourceIssues"));

        var contract = RequiredObject(spine, "branchConsequenceContract");
        var sharedFields = RequiredStringArray(contract, "sharedRouteFields");
        Assert.Contains(sharedFields, item => item.Contains("success", StringComparison.OrdinalIgnoreCase) && item.Contains("partial", StringComparison.OrdinalIgnoreCase) && item.Contains("fail", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sharedFields, item => item.Contains("ScoreDeltas", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(sharedFields, item => item.Contains("routing", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(RequiredStringArray(contract, "forbiddenExpansions"), item => item.Contains("branch-memory", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(RequiredStringArray(contract, "forbiddenExpansions"), item => item.Contains("React-only", StringComparison.OrdinalIgnoreCase));

        foreach (var forbidden in new[] { "BranchMemory", "CampaignState", "ConsequenceService", "DarenConsequence", "React-only" })
            Assert.DoesNotContain(forbidden, darenSource, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DarenDialogueCast_HasNamedFiguresVisibleInRouteAndSpine()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var routeText = BuildPlayerFacingRouteText(route.Offer);
        var spine = LoadDarenNarrativeSpine();
        var castSlots = RequiredObjectArray(spine, "castSlots");

        foreach (var (slotId, displayName) in RequiredCastNamesBySlotId)
        {
            var slot = Assert.Single(castSlots, item =>
                string.Equals(RequiredString(item, "slotId"), slotId, StringComparison.OrdinalIgnoreCase));

            Assert.Equal(displayName, RequiredString(slot, "displayName"));
            AssertStoryCopyLooksAuthored(slotId, "persona", RequiredString(slot, "persona"));

            var dialogueBeatIds = RequiredStringArray(slot, "dialogueBeatIds");
            Assert.All(dialogueBeatIds, beatId =>
                Assert.Contains(beatId, RequiredBeatIds, StringComparer.OrdinalIgnoreCase));

            Assert.Contains(displayName, routeText, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void DarenRoute_ContainsDialogueSocialChoiceMomentsThroughExistingQteActions()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var beatId in RequiredDialogueBeatIds)
        {
            var (chapter, action) = RequiredChapterAction(route.Offer, beatId);
            var npcName = RequiredDialogueNpcByBeatId[beatId];

            Assert.Contains(action.Check.Type, DialogueChoiceCheckTypes);
            Assert.Equal("PrecisionChoice", action.Check.Type);
            Assert.Contains(npcName, $"{chapter.Title} {chapter.Narrative} {action.Label}", StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(action.Routing.Success.NextChapterId ?? action.Routing.Success.TerminalOutcomeId);
            Assert.NotNull(action.Routing.Partial.NextChapterId ?? action.Routing.Partial.TerminalOutcomeId);
            Assert.NotNull(action.Routing.Fail.NextChapterId ?? action.Routing.Fail.TerminalOutcomeId);
        }
    }

    [Fact]
    public void DarenDialogueChoices_ExposePlayerFacingAnswerOptions()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var beatId in RequiredDialogueBeatIds)
        {
            var (_, action) = RequiredChapterAction(route.Offer, beatId);

            Assert.Equal("PrecisionChoice", action.Check.Type);
            var choices = RequiredObjectArray(RequiredConfig(action), "choices");
            Assert.InRange(choices.Count, 3, 8);

            var grades = choices
                .Select(choice => RequiredString(choice, "grade"))
                .ToArray();
            Assert.Contains("success", grades, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("partial", grades, StringComparer.OrdinalIgnoreCase);
            Assert.Contains("fail", grades, StringComparer.OrdinalIgnoreCase);

            foreach (var choice in choices)
            {
                var label = RequiredString(choice, "label");
                var description = RequiredString(choice, "description");
                var hint = RequiredString(choice, "hint");
                AssertNoPlayerFacingTechnicalTerms($"{beatId} choice label", label);
                AssertNoPlayerFacingTechnicalTerms($"{beatId} choice description", description);
                AssertNoPlayerFacingTechnicalTerms($"{beatId} choice hint", hint);
                Assert.True(label.Length <= 48, $"Daren dialogue choice '{beatId}' label is too long for compact choice UI.");
                Assert.True(description.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3,
                    $"Daren dialogue choice '{beatId}' needs a meaningful answer description.");
                Assert.True(hint.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3,
                    $"Daren dialogue choice '{beatId}' needs a meaningful answer hint.");
            }
        }
    }

    [Fact]
    public void DarenDialogueResponses_HaveDistinctNpcSocialVariants()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();

        foreach (var beatId in RequiredDialogueBeatIds)
        {
            var (_, action) = RequiredChapterAction(route.Offer, beatId);
            var resultTexts = new[]
            {
                action.SuccessText,
                action.PartialText,
                action.FailText
            };

            Assert.Equal(3, resultTexts.Distinct(StringComparer.OrdinalIgnoreCase).Count());
            AssertActionResultLooksLikeTransition(beatId, action.ActionId, "success", action.SuccessText);
            AssertActionResultLooksLikeTransition(beatId, action.ActionId, "partial", action.PartialText);
            AssertActionResultLooksLikeTransition(beatId, action.ActionId, "fail", action.FailText);

            foreach (var text in resultTexts)
            {
                Assert.True(ContainsAny(text ?? "", SocialReactionTerms),
                    $"Daren dialogue action '{beatId}' result should read as an NPC or social reaction.");
            }
        }
    }

    [Fact]
    public void DarenDialogueConsequences_AffectRiskAndEchoLaterInRouteProse()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var dialogueActions = RequiredDialogueBeatIds
            .Select(beatId => RequiredChapterAction(route.Offer, beatId))
            .ToArray();

        var hasRiskDelta = dialogueActions.Any(item =>
            item.Action.ScoreDeltas != null &&
            item.Action.ScoreDeltas.Values
                .SelectMany(delta => delta)
                .Any(delta =>
                    delta.Delta != 0 &&
                    ExistingRiskMetricIds.Contains(delta.Metric, StringComparer.OrdinalIgnoreCase)));

        Assert.True(hasRiskDelta, "At least one Daren dialogue/social choice must affect an existing score or risk metric.");

        foreach (var (_, action) in dialogueActions)
        {
            Assert.NotNull(action.ScoreDeltas);
            foreach (var grade in new[] { "success", "partial", "fail" })
            {
                Assert.True(action.ScoreDeltas!.TryGetValue(grade, out var deltas), $"Daren dialogue action '{action.ActionId}' needs score deltas for {grade}.");
                Assert.NotEmpty(deltas);
            }
        }

        var lastDialogueIndex = route.Offer.Chapters.FindLastIndex(chapter =>
            RequiredDialogueBeatIds.Contains(chapter.ChapterId, StringComparer.OrdinalIgnoreCase));
        Assert.InRange(lastDialogueIndex, 0, route.Offer.Chapters.Count - 2);

        var laterText = BuildPlayerFacingRouteText(route.Offer.Chapters.Skip(lastDialogueIndex + 1));
        Assert.True(ContainsAny(laterText, LaterSocialEchoTerms),
            "Later Daren route prose should echo an earlier NPC interaction or social consequence.");
    }

    [Fact]
    public void DarenPlayerFacingRouteCopy_DoesNotLeakTechnicalTerms()
    {
        var offer = QteSceneService.GetDarenShowcaseRoute().Offer;

        AssertNoPlayerFacingTechnicalTerms("offer text", offer.OfferText);
        AssertNoPlayerFacingTechnicalTerms("intro narrative", offer.IntroNarrative);
        AssertNoPlayerFacingTechnicalTerms("decline hint", offer.DeclineHint);
        AssertNoPlayerFacingTechnicalTerms("cinematic justification", offer.CinematicJustification);
        foreach (var chapter in offer.Chapters)
        {
            AssertNoPlayerFacingTechnicalTerms($"{chapter.ChapterId} narrative", chapter.Narrative);
            foreach (var action in chapter.Actions)
            {
                AssertNoPlayerFacingTechnicalTerms($"{action.ActionId} success", action.SuccessText);
                AssertNoPlayerFacingTechnicalTerms($"{action.ActionId} partial", action.PartialText);
                AssertNoPlayerFacingTechnicalTerms($"{action.ActionId} fail", action.FailText);
            }
        }
    }

    [Fact]
    public void DarenNarrativeSpine_ExistsAndDeclaresRouteIssuesAndPlaytime()
    {
        var spine = LoadDarenNarrativeSpine();

        Assert.Equal(1, RequiredInt(spine, "schemaVersion"));
        Assert.Equal("daren_qte_showcase", RequiredString(spine, "routeId"));
        Assert.Contains(957, RequiredIntArray(spine, "sourceIssues"));
        Assert.Contains(958, RequiredIntArray(spine, "sourceIssues"));
        Assert.Contains(956, RequiredIntArray(spine, "sourceIssues"));
        Assert.Contains(955, RequiredIntArray(spine, "sourceIssues"));
        Assert.Contains(919, RequiredIntArray(spine, "sourceIssues"));

        var playtime = RequiredObject(spine, "targetPlaytimeMinutes");
        Assert.Equal(20, RequiredInt(playtime, "min"));
        Assert.Equal(30, RequiredInt(playtime, "max"));
    }

    [Fact]
    public void DarenNarrativeSpine_BeatsMatchSharedRouteOrderAndQteTypes()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var routeTypesByBeat = route.Offer.Chapters.ToDictionary(
            chapter => chapter.ChapterId,
            chapter => Assert.Single(chapter.Actions).Check.Type,
            StringComparer.OrdinalIgnoreCase);
        var spineBeats = RequiredObjectArray(LoadDarenNarrativeSpine(), "beats");

        var spineBeatIds = spineBeats.Select(beat => RequiredString(beat, "beatId")).ToArray();
        Assert.Equal(route.Beats.Select(beat => beat.BeatId), spineBeatIds);
        AssertOrderedSubsequence(OriginalHeistBeatIds, spineBeatIds, "Daren narrative spine beats");
        Assert.Equal(spineBeatIds.Length, spineBeatIds.Distinct(StringComparer.OrdinalIgnoreCase).Count());

        foreach (var beat in spineBeats)
        {
            var beatId = RequiredString(beat, "beatId");
            Assert.True(routeTypesByBeat.ContainsKey(beatId), $"Narrative spine beat '{beatId}' is not in the Daren route.");
            Assert.Equal(routeTypesByBeat[beatId], RequiredString(beat, "qteType"));
        }
    }

    [Fact]
    public void DarenNarrativeSpine_BeatsDeclareNarrativeStructureAndConsequences()
    {
        foreach (var beat in RequiredObjectArray(LoadDarenNarrativeSpine(), "beats"))
        {
            var beatId = RequiredString(beat, "beatId");

            foreach (var field in new[] { "phase", "title", "dramaticPurpose", "playerGoal", "sceneFraming" })
                AssertStoryCopyLooksAuthored(beatId, field, RequiredString(beat, field));

            foreach (var field in new[] { "branchPoints", "consequenceHooks", "carryForward" })
            {
                var values = RequiredStringArray(beat, field);
                foreach (var value in values)
                    AssertStoryCopyLooksAuthored(beatId, field, value);
            }

            var futureLinks = RequiredIntArray(beat, "futureIssueLinks");
            Assert.All(futureLinks, issue => Assert.Contains(issue, FutureDarenIssueIds));
            Assert.True(RequiredInt(beat, "pacingMinutes") > 0, $"Narrative spine beat '{beatId}' needs positive pacingMinutes.");
        }
    }

    [Fact]
    public void DarenNarrativeSpine_CoversArcCastHandoffAndSharedRuntimeBoundary()
    {
        var spine = LoadDarenNarrativeSpine();
        var spineBeats = RequiredObjectArray(spine, "beats");
        var beatIds = spineBeats
            .Select(beat => RequiredString(beat, "beatId"))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(RequiredNarrativeArcStages, RequiredStringArray(spine, "arcStages"));
        var beatPhaseText = string.Join(" | ", spineBeats.Select(beat => RequiredString(beat, "phase")));
        foreach (var stage in RequiredNarrativeArcStages)
            Assert.Contains(stage, beatPhaseText, StringComparison.OrdinalIgnoreCase);

        var targetPlaytime = RequiredObject(spine, "targetPlaytimeMinutes");
        var totalPacing = spineBeats.Sum(beat => RequiredInt(beat, "pacingMinutes"));
        Assert.InRange(totalPacing, RequiredInt(targetPlaytime, "min"), RequiredInt(targetPlaytime, "max"));

        var castSlots = RequiredObjectArray(spine, "castSlots");
        foreach (var slotId in RequiredNarrativeCastSlots)
        {
            var slot = Assert.Single(castSlots, item =>
                string.Equals(RequiredString(item, "slotId"), slotId, StringComparison.OrdinalIgnoreCase));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(slot, "role")));
            Assert.All(RequiredStringArray(slot, "plannedBeatIds"), plannedBeatId =>
                Assert.Contains(plannedBeatId, beatIds));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(slot, "displayName")));
            Assert.False(string.IsNullOrWhiteSpace(RequiredString(slot, "persona")));
            Assert.All(RequiredStringArray(slot, "dialogueBeatIds"), beatId =>
                Assert.Contains(beatId, beatIds));
            Assert.Contains(958, RequiredIntArray(slot, "futureIssueLinks"));
        }

        var allFutureLinks = spineBeats
            .SelectMany(beat => RequiredIntArray(beat, "futureIssueLinks"))
            .Concat(castSlots.SelectMany(slot => RequiredIntArray(slot, "futureIssueLinks")))
            .ToArray();
        foreach (var issueId in FutureDarenIssueIds)
            Assert.Contains(issueId, allFutureLinks);

        var contractBoundaries = RequiredStringArray(spine, "contractBoundaries");
        Assert.Contains(contractBoundaries, item => item.Contains("existing QTE route", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contractBoundaries, item => item.Contains("console and browser", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contractBoundaries, item => item.Contains("no new", StringComparison.OrdinalIgnoreCase) && item.Contains("runtime", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contractBoundaries, item => item.Contains("no new QTE check type", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(contractBoundaries, item => item.Contains("no reward/profile", StringComparison.OrdinalIgnoreCase));

        var handoffNotes = RequiredStringArray(spine, "handoffNotes");
        foreach (var issueId in FutureDarenIssueIds)
            Assert.Contains(handoffNotes, note => note.Contains($"#{issueId}", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(false, 100, null, 0)]
    [InlineData(true, 39, null, 0)]
    [InlineData(true, 40, "shadow_on_the_run", 1)]
    [InlineData(true, 54, "shadow_on_the_run", 1)]
    [InlineData(true, 55, "broken_trail", 2)]
    [InlineData(true, 74, "broken_trail", 2)]
    [InlineData(true, 75, "clean_heist", 4)]
    [InlineData(true, 89, "clean_heist", 4)]
    [InlineData(true, 90, "perfect_shadow", 6)]
    public void DarenEndingResolver_UsesExactThresholdsAndBonuses(bool reachedHideout, int score, string? expectedTierId, int expectedBonus)
    {
        var ending = DarenQteRewardProfileService.ResolveEnding(reachedHideout, score);

        Assert.Equal(expectedTierId, ending.TierId);
        Assert.Equal(expectedBonus, ending.InkFeatherBonus);
        if (expectedTierId == null)
        {
            Assert.False(ending.GrantsReward);
            Assert.Equal("no_reward_failure", ending.OutcomeId);
        }
        else
        {
            Assert.True(ending.GrantsReward);
            Assert.False(string.IsNullOrWhiteSpace(ending.DisplayName));
        }
    }

    [Fact]
    public async Task DarenProfile_WritesBestTierAndNeverDowngradesOrStacks()
    {
        var first = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 75),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));
        var worse = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 55),
            new DateTime(2026, 6, 11, 2, 0, 0, DateTimeKind.Utc));
        var same = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 75),
            new DateTime(2026, 6, 11, 3, 0, 0, DateTimeKind.Utc));
        var upgrade = await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90),
            new DateTime(2026, 6, 11, 4, 0, 0, DateTimeKind.Utc));

        Assert.True(first.Updated);
        Assert.False(worse.Updated);
        Assert.False(same.Updated);
        Assert.True(upgrade.Updated);

        var profile = await _profile.ReadProfileAsync();
        Assert.Equal(1, profile.SchemaVersion);
        Assert.Equal("perfect_shadow", profile.DarenShowcase?.BestTierId);
        Assert.Equal("Идеальная тень", profile.DarenShowcase?.BestTierName);
        Assert.Equal(6, profile.DarenShowcase?.InkFeatherBonus);
        Assert.Equal(90, profile.DarenShowcase?.BestScore);
    }

    [Fact]
    public async Task DarenProfile_NormalizesDuplicateAndCorruptRecordsBeforeGranting()
    {
        await WriteClientProfileAsync("""
        {
          "schemaVersion": 1,
          "darenShowcase": {
            "bestTierId": "shadow_on_the_run",
            "bestTierName": "Тень в бегах",
            "inkFeatherBonus": -20,
            "bestScore": 41,
            "completedAtUtc": "2026-06-11T01:00:00Z",
            "source": "daren_qte_showcase"
          },
          "darenShowcases": [
            {
              "bestTierId": "clean_heist",
              "bestTierName": "Чистая кража",
              "inkFeatherBonus": 999,
              "bestScore": 82,
              "completedAtUtc": "2026-06-11T02:00:00Z",
              "source": "daren_qte_showcase"
            },
            {
              "bestTierId": "unknown_shadow",
              "bestTierName": "Unknown",
              "inkFeatherBonus": 50,
              "bestScore": 100,
              "completedAtUtc": "2026-06-11T03:00:00Z",
              "source": "daren_qte_showcase"
            }
          ]
        }
        """);

        var profile = await _profile.ReadProfileAsync();

        Assert.Equal("clean_heist", profile.DarenShowcase?.BestTierId);
        Assert.Equal("Чистая кража", profile.DarenShowcase?.BestTierName);
        Assert.Equal(4, profile.DarenShowcase?.InkFeatherBonus);
        Assert.Equal(82, profile.DarenShowcase?.BestScore);
    }

    [Fact]
    public async Task DarenNewGameReward_AppliesBestTierOnceToFreshSoulStateOnly()
    {
        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));
        var soulRoot = JsonNode.Parse("""
        {
          "soulName": "Искра",
          "currentRealm": "Chaos Sea",
          "currentIncarnation": 0,
          "inkFeathers": { "current": 0, "total": 0 }
        }
        """)!.AsObject();

        var first = await _profile.ApplyBestRewardToNewSoulStateAsync(soulRoot);
        var second = await _profile.ApplyBestRewardToNewSoulStateAsync(soulRoot);

        Assert.True(first.Granted);
        Assert.Equal("Идеальная тень", first.TierName);
        Assert.Contains("Дарен", first.PlayerMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("6", first.PlayerMessage, StringComparison.Ordinal);
        Assert.False(second.Granted);
        var inkFeathers = soulRoot["inkFeathers"]!.AsObject();
        var grants = soulRoot["clientRewardGrants"]!.AsObject();
        var darenGrant = grants["darenQteShowcase"]!.AsObject();
        Assert.Equal(6, inkFeathers["current"]!.GetValue<int>());
        Assert.Equal(6, inkFeathers["total"]!.GetValue<int>());
        Assert.Equal("daren_qte_showcase", darenGrant["source"]!.GetValue<string>());
    }

    [Fact]
    public async Task DarenShowcaseAttempt_ReachesRewardEndingWithoutCampaignMutation()
    {
        WriteCampaignSentinels();
        var before = SnapshotGameSessionFiles();

        var attempt = _qte.StartDarenShowcaseAttempt();
        QteSceneService.QteActionResolution? resolution = null;
        while (attempt.State == "Active")
        {
            var chapter = attempt.ActiveScene.Offer!.Chapters.Single(item =>
                string.Equals(item.ChapterId, attempt.ActiveScene.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            var action = chapter.Actions[0];
            resolution = await _qte.ResolveDarenShowcaseActionAsync(
                attempt,
                action.ActionId,
                "success",
                completedAtUtc: new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));
        }

        var after = SnapshotGameSessionFiles();

        Assert.NotNull(resolution?.Completion);
        Assert.Equal("perfect_shadow", resolution!.Completion!.OutcomeId);
        Assert.Contains("Идеальная тень", resolution.Completion.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, after);
        Assert.True(File.Exists(Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json")));
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public async Task DarenShowcaseAttempt_PreHideoutFailureNeverWritesPermanentRewardEvenWithHighScore()
    {
        WriteCampaignSentinels();
        var before = SnapshotGameSessionFiles();

        var attempt = _qte.StartDarenShowcaseAttempt();
        QteSceneService.QteActionResolution? resolution = null;
        while (attempt.State == "Active")
        {
            var chapter = attempt.ActiveScene.Offer!.Chapters.Single(item =>
                string.Equals(item.ChapterId, attempt.ActiveScene.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            var action = chapter.Actions[0];
            var grade = string.Equals(chapter.ChapterId, "gadget_infiltration", StringComparison.OrdinalIgnoreCase)
                ? "fail"
                : "success";
            resolution = await _qte.ResolveDarenShowcaseActionAsync(
                attempt,
                action.ActionId,
                grade,
                completedAtUtc: new DateTime(2026, 6, 11, 1, 30, 0, DateTimeKind.Utc));
        }

        var after = SnapshotGameSessionFiles();
        var profile = await _profile.ReadProfileAsync();

        Assert.NotNull(resolution?.Completion);
        Assert.Equal("no_reward_failure", resolution!.Completion!.OutcomeId);
        Assert.Equal("no_reward_failure", resolution.Completion.ScoreSummary?.Rank?.Id);
        Assert.Equal("Провал вылазки", resolution.Completion.ScoreSummary?.Rank?.Label);
        Assert.DoesNotContain("Чистая кража", resolution.Completion.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(attempt.Ending!.GrantsReward);
        Assert.Null(profile.DarenShowcase);
        Assert.False(File.Exists(Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json")));
        Assert.Equal(before, after);
        AssertNoCampaignQteFiles();
    }

    [Fact]
    public async Task DarenShowcaseAttempt_AllPartialValidCompletionCanReachShadowTier()
    {
        var attempt = _qte.StartDarenShowcaseAttempt();
        QteSceneService.QteActionResolution? resolution = null;
        while (attempt.State == "Active")
        {
            var chapter = attempt.ActiveScene.Offer!.Chapters.Single(item =>
                string.Equals(item.ChapterId, attempt.ActiveScene.CurrentChapterId, StringComparison.OrdinalIgnoreCase));
            var action = chapter.Actions[0];
            resolution = await _qte.ResolveDarenShowcaseActionAsync(
                attempt,
                action.ActionId,
                "partial",
                completedAtUtc: new DateTime(2026, 6, 11, 1, 45, 0, DateTimeKind.Utc));
        }

        var profile = await _profile.ReadProfileAsync();

        Assert.NotNull(resolution?.Completion);
        Assert.Equal("shadow_on_the_run", resolution!.Completion!.OutcomeId);
        Assert.Equal("shadow_on_the_run", resolution.Completion.ScoreSummary?.Rank?.Id);
        Assert.Equal("Тень в бегах", resolution.Completion.ScoreSummary?.Rank?.Label);
        Assert.True(attempt.Ending!.GrantsReward);
        Assert.Equal(1, attempt.Ending.InkFeatherBonus);
        Assert.Equal("shadow_on_the_run", profile.DarenShowcase?.BestTierId);
    }

    [Fact]
    public async Task DarenBrowserState_UsesExistingQteProjectionAndCSharpRewardAuthority()
    {
        var intro = await _web.BuildDarenShowcaseStateAsync();

        Assert.Equal("Intro", intro.State);
        Assert.Contains("Дарен", intro.IntroTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("отдель", intro.BoundaryNotice, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("start", intro.AvailableOperations);

        var started = await _web.StartDarenShowcaseAsync();
        Assert.Equal("Active", started.State);
        Assert.NotNull(started.ActiveScene);
        var firstAction = Assert.Single(started.ActiveScene!.CurrentChapter!.Actions);
        Assert.Contains(firstAction.CheckType, RequiredQteTypes);

        var resolved = await _web.ResolveDarenShowcaseActionAsync(new DarenShowcaseActionRequest(firstAction.ActionId, "success"));
        Assert.Equal("Active", resolved.State);
        Assert.NotNull(resolved.Resolution);
        Assert.Contains("submitAction", resolved.AvailableOperations);
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteRuntimePath.Replace('/', Path.DirectorySeparatorChar))));
    }

    [Fact]
    public void DarenShowcaseDocsAndSourceGuards_PreserveClientOwnedBoundary()
    {
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var qteRules = ReadRepoFile("Rules", "Block_CLI_QTE.txt");
        var qteExample = ReadRepoFile("Examples", "E_CLI_QTE_Offer.txt");
        var mainMenuSource = ReadRepoFile("BookOfEternityClient", "Core", "GameEngine", "GameEngine.MainMenu.cs");
        var darenSource = ReadRepoFile("BookOfEternityClient", "Services", "QteSceneService.Daren.cs");

        foreach (var requiredText in new[]
        {
            "Daren showcase",
            "client-owned",
            "not a GM-authored QTE offer",
            "New Game",
            "Ink Feather",
            "QTE Practice Mode must not grant Daren rewards"
        })
        {
            Assert.Contains(requiredText, apiSpec, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(requiredText, qteRules, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(requiredText, qteExample, StringComparison.OrdinalIgnoreCase);
        }

        Assert.Contains("RunDarenShowcaseModeAsync", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("ApplyBestRewardToNewSoulStateAsync", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("clientRewardGrants", mainMenuSource, StringComparison.Ordinal);
        Assert.Contains("Ограбление поместья Дареном", darenSource, StringComparison.Ordinal);
        Assert.Contains("Начать вылазку", darenSource, StringComparison.Ordinal);
        Assert.Contains("Чернильных Перьев", darenSource, StringComparison.Ordinal);
        Assert.DoesNotContain("/api/", darenSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DTO", darenSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("manual-grade", darenSource, StringComparison.OrdinalIgnoreCase);

        var productionGrantCallSites = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("ApplyBestRewardToNewSoulStateAsync", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(["BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs", "BookOfEternityClient/Services/DarenQteRewardProfileService.cs"], productionGrantCallSites);
    }

    private async Task WriteClientProfileAsync(string json)
    {
        var profilePath = Path.Combine(_rootPath, "client_profile", "qte_showcase_rewards.json");
        Directory.CreateDirectory(Path.GetDirectoryName(profilePath)!);
        await File.WriteAllTextAsync(profilePath, json);
    }

    private void WriteCampaignSentinels()
    {
        WriteSessionFile("game_state/meta/soul_state.json", """{ "inkFeathers": { "current": 17, "total": 17 } }""");
        WriteSessionFile("game_state/player/experience.json", """{ "experience": 345, "level": 4 }""");
        WriteSessionFile("game_state/inventory/items.json", """{ "items": [{ "id": "sentinel-staff", "quantity": 1 }] }""");
        WriteSessionFile("game_state/quests/active_quests.json", """{ "quests": [{ "id": "main", "stage": "before_daren" }] }""");
        WriteSessionFile("game_state/control/pending_campaign_action.json", """{ "kind": "ordinary-turn", "status": "pending" }""");
        WriteSessionFile("game_state/history/chat_log.json", """{ "turns": [{ "turnNumber": 7 }] }""");
        WriteSessionFile("game_state/meta/afterlife_state.json", """{ "state": "untouched" }""");
    }

    private void WriteSessionFile(string relativePath, string contents)
    {
        var fullPath = Path.Combine(_rootPath, "game_session", relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, contents);
    }

    private Dictionary<string, string> SnapshotGameSessionFiles() =>
        Directory.EnumerateFiles(Path.Combine(_rootPath, "game_session"), "*", SearchOption.AllDirectories)
            .Select(path => (Path: Path.GetRelativePath(Path.Combine(_rootPath, "game_session"), path).Replace('\\', '/'), Contents: File.ReadAllText(path)))
            .OrderBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(item => item.Path, item => item.Contents, StringComparer.OrdinalIgnoreCase);

    private void AssertNoCampaignQteFiles()
    {
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteOfferPath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteRuntimePath.Replace('/', Path.DirectorySeparatorChar))));
        Assert.False(File.Exists(Path.Combine(_rootPath, "game_session", QteSceneService.QteHistoryPath.Replace('/', Path.DirectorySeparatorChar))));
    }

    private static string ReadRepoFile(params string[] relativeParts) =>
        File.ReadAllText(Path.Combine(new[] { TestRepoPaths.RepoRoot }.Concat(relativeParts).ToArray()));

    private static JsonObject LoadDarenNarrativeSpine()
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient", "Content", "DarenQteNarrativeSpine.json");
        Assert.True(File.Exists(path), $"Missing Daren narrative spine artifact at {path}.");
        return Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(path)));
    }

    private static string BuildPlayerFacingRouteText(QteSceneService.QteOffer offer) =>
        string.Join("\n", BuildPlayerFacingRouteParts(offer.Chapters)
            .Prepend(offer.CinematicJustification ?? "")
            .Prepend(offer.DeclineHint ?? "")
            .Prepend(offer.IntroNarrative ?? "")
            .Prepend(offer.OfferText ?? "")
            .Prepend(offer.Title ?? ""));

    private static string BuildPlayerFacingRouteText(IEnumerable<QteSceneService.QteChapter> chapters) =>
        string.Join("\n", BuildPlayerFacingRouteParts(chapters));

    private static IEnumerable<string> BuildPlayerFacingRouteParts(IEnumerable<QteSceneService.QteChapter> chapters)
    {
        foreach (var chapter in chapters)
        {
            yield return chapter.Title ?? "";
            yield return chapter.Narrative ?? "";
            foreach (var action in chapter.Actions)
            {
                yield return action.Label;
                yield return action.SuccessText ?? "";
                yield return action.PartialText ?? "";
                yield return action.FailText ?? "";
                yield return action.Check.Config?.ToJsonString() ?? "";
            }
        }
    }

    private static (QteSceneService.QteChapter Chapter, QteSceneService.QteAction Action) RequiredChapterAction(
        QteSceneService.QteOffer offer,
        string chapterId)
    {
        var chapter = Assert.Single(offer.Chapters, item =>
            string.Equals(item.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase));
        var action = Assert.Single(chapter.Actions);
        return (chapter, action);
    }

    private static int FindChapterIndex(QteSceneService.QteOffer offer, string chapterId)
    {
        var index = offer.Chapters.FindIndex(item =>
            string.Equals(item.ChapterId, chapterId, StringComparison.OrdinalIgnoreCase));
        Assert.True(index >= 0, $"Daren route should contain chapter '{chapterId}'.");
        return index;
    }

    private static void AssertFailureScoreDeltasIncreasePressure(string beatId, QteSceneService.QteAction action)
    {
        Assert.NotNull(action.ScoreDeltas);
        Assert.True(action.ScoreDeltas!.TryGetValue("fail", out var deltas), $"Daren action '{beatId}' needs fail score deltas.");
        Assert.NotNull(deltas);
        Assert.Contains(deltas!, delta =>
            string.Equals(delta.Metric, "normalized_score", StringComparison.OrdinalIgnoreCase) &&
            delta.Delta < 0);
        Assert.Contains(deltas!, delta =>
            ExistingRiskMetricIds.Contains(delta.Metric, StringComparer.OrdinalIgnoreCase) &&
            delta.Delta != 0);
    }

    private static JsonObject RequiredConfig(QteSceneService.QteAction action)
    {
        Assert.NotNull(action.Check.Config);
        return action.Check.Config;
    }

    private static JsonObject RequiredObject(JsonObject root, string propertyName)
    {
        Assert.True(root[propertyName] is JsonObject, $"Expected '{propertyName}' to be an object.");
        return (JsonObject)root[propertyName]!;
    }

    private static IReadOnlyList<JsonObject> RequiredObjectArray(JsonObject root, string propertyName)
    {
        var array = RequiredArray(root, propertyName);
        var values = new List<JsonObject>(array.Count);
        for (var index = 0; index < array.Count; index++)
        {
            Assert.True(array[index] is JsonObject, $"Expected '{propertyName}[{index}]' to be an object.");
            values.Add((JsonObject)array[index]!);
        }

        Assert.NotEmpty(values);
        return values;
    }

    private static IReadOnlyList<string> RequiredStringArray(JsonObject root, string propertyName)
    {
        var array = RequiredArray(root, propertyName);
        var values = new List<string>(array.Count);
        for (var index = 0; index < array.Count; index++)
        {
            Assert.True(array[index] is JsonValue, $"Expected '{propertyName}[{index}]' to be a string.");
            var value = (JsonValue)array[index]!;
            Assert.True(value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text),
                $"Expected '{propertyName}[{index}]' to be a non-empty string.");
            values.Add(text.Trim());
        }

        Assert.NotEmpty(values);
        return values;
    }

    private static IReadOnlyList<int> RequiredIntArray(JsonObject root, string propertyName)
    {
        var array = RequiredArray(root, propertyName);
        var values = new List<int>(array.Count);
        for (var index = 0; index < array.Count; index++)
        {
            Assert.True(array[index] is JsonValue, $"Expected '{propertyName}[{index}]' to be an integer.");
            var value = (JsonValue)array[index]!;
            Assert.True(value.TryGetValue<int>(out var number), $"Expected '{propertyName}[{index}]' to be an integer.");
            values.Add(number);
        }

        Assert.NotEmpty(values);
        return values;
    }

    private static JsonArray RequiredArray(JsonObject root, string propertyName)
    {
        Assert.True(root[propertyName] is JsonArray, $"Expected '{propertyName}' to be an array.");
        return (JsonArray)root[propertyName]!;
    }

    private static string RequiredString(JsonObject root, string propertyName)
    {
        Assert.True(root[propertyName] is JsonValue, $"Expected '{propertyName}' to be a string.");
        var value = (JsonValue)root[propertyName]!;
        Assert.True(value.TryGetValue<string>(out var text) && !string.IsNullOrWhiteSpace(text),
            $"Expected '{propertyName}' to be a non-empty string.");
        return text.Trim();
    }

    private static int RequiredInt(JsonObject root, string propertyName)
    {
        Assert.True(root[propertyName] is JsonValue, $"Expected '{propertyName}' to be an integer.");
        var value = (JsonValue)root[propertyName]!;
        Assert.True(value.TryGetValue<int>(out var number), $"Expected '{propertyName}' to be an integer.");
        return number;
    }

    private static void AssertChapterNarrativeLooksLikeSceneProse(string chapterId, string? value)
    {
        Assert.False(string.IsNullOrWhiteSpace(value), $"Daren chapter '{chapterId}' needs authored narrative prose.");
        var text = value.Trim();
        Assert.InRange(text.Length, 140, 520);
        Assert.True(CountSentences(text) >= 2,
            $"Daren chapter '{chapterId}' should have at least two scene sentences, not a bare mechanic label.");
        Assert.Contains("Дарен", text, StringComparison.OrdinalIgnoreCase);
        AssertNoPlayerFacingTechnicalTerms($"{chapterId} narrative", text);

        Assert.True(ChapterSignalGroups.TryGetValue(chapterId, out var signalGroups),
            $"Missing chapter signal guard for Daren beat '{chapterId}'.");
        foreach (var signalGroup in signalGroups)
            AssertContainsAny(chapterId, text, signalGroup);
    }

    private static void AssertActionResultLooksLikeTransition(
        string chapterId,
        string actionId,
        string grade,
        string? value)
    {
        var context = $"{chapterId}/{actionId}/{grade}";
        Assert.False(string.IsNullOrWhiteSpace(value), $"Daren action result '{context}' needs transition prose.");
        var text = value.Trim();
        Assert.InRange(text.Length, 70, 260);
        Assert.True(text.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 8,
            $"Daren action result '{context}' is too terse to carry the scene forward.");
        AssertNoPlayerFacingTechnicalTerms(context, text);
        Assert.True(
            text.Contains("Дарен", StringComparison.OrdinalIgnoreCase) ||
            ContainsAny(text, ["погон", "след", "страж", "посох", "тревог", "убежищ", "двор", "шум", "тайник"]),
            $"Daren action result '{context}' should name Daren or an immediate scene consequence.");
    }

    private static void AssertNoPlayerFacingTechnicalTerms(string context, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return;

        foreach (var forbidden in ForbiddenPlayerFacingTechnicalTerms)
        {
            Assert.DoesNotContain(
                forbidden,
                value,
                StringComparison.OrdinalIgnoreCase);
        }
    }

    private static int CountSentences(string value) =>
        value.Split(['.', '!', '?', '…'], StringSplitOptions.RemoveEmptyEntries)
            .Count(sentence => sentence.Trim().Length >= 10);

    private static void AssertContainsAny(string context, string value, IReadOnlyList<string> terms)
    {
        Assert.True(ContainsAny(value, terms),
            $"Daren chapter '{context}' needs one of these story signals: {string.Join(", ", terms)}.");
    }

    private static bool ContainsAny(string value, IReadOnlyList<string> terms) =>
        terms.Any(term => value.Contains(term, StringComparison.OrdinalIgnoreCase));

    private static void AssertOrderedSubsequence(
        IReadOnlyList<string> expectedSubsequence,
        IEnumerable<string> actualSequence,
        string context)
    {
        var actual = actualSequence.ToArray();
        var searchStart = 0;
        foreach (var expected in expectedSubsequence)
        {
            var foundIndex = Array.FindIndex(
                actual,
                searchStart,
                item => string.Equals(item, expected, StringComparison.OrdinalIgnoreCase));
            Assert.True(foundIndex >= 0, $"{context} should keep original Daren heist beat '{expected}' in order.");
            searchStart = foundIndex + 1;
        }
    }

    private static void AssertStoryCopyLooksAuthored(string beatId, string field, string value)
    {
        foreach (var forbidden in ForbiddenStoryCopyTerms)
        {
            Assert.DoesNotContain(
                forbidden,
                value,
                StringComparison.OrdinalIgnoreCase);
        }

        Assert.True(value.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 3,
            $"Narrative spine beat '{beatId}' field '{field}' should be authored copy, not a bare label.");
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
