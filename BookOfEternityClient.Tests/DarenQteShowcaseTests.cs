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
        "client-owned",
        "QTE",
        "score",
        "JSON",
        "files",
        "tests",
        "agent"
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

    private static readonly string[] ForbiddenRewardReceiptTerms =
    [
        "+",
        "бонус",
        "стартов",
        "на старте",
        "к старту",
        "профиль",
        "system",
        "tier"
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
    public void DarenApproachManor_ReadsAsFullLiteraryPage()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "approach_manor");
        var (chapter, action) = RequiredChapterAction(route.Offer, "approach_manor");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Подступ к поместью", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("approach_manor_action", action.ActionId);
        Assert.Equal("BranchChoice", action.Check.Type);
        Assert.Equal("Выбрать тень у старой липы", action.Label);

        Assert.True(text.Length >= 1200,
            "Daren approach_manor narrative should be a substantial literary page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 8,
            "Daren approach_manor narrative should unfold across multiple scene sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren approach_manor narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, terms) in new (string Context, string[] Terms)[]
        {
            ("manor wall and gate", ["помест", "стен", "калит"]),
            ("wet night atmosphere", ["мокр", "трава", "сыр", "ноч"]),
            ("patrol lantern pressure", ["фонар", "патрул", "страж"]),
            ("old linden shadow route", ["липа", "тень"]),
            ("Daren body language", ["ладон", "плеч", "дых", "колен", "ребр"]),
            ("Daren intent", ["посох", "добыч", "вор"]),
            ("failure stakes", ["крик", "тревог", "погон", "увид"]),
            ("choice lead-in", ["выб", "скольз", "полз", "шаг", "подступ"])
        })
        {
            AssertContainsAny($"approach_manor full-page {context}", text, terms);
        }

        AssertNoPlayerFacingTechnicalTerms("approach_manor full-page narrative", text);
    }

    [Fact]
    public void DarenInformantParley_ReadsAsMiraLiteraryPageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "informant_parley");
        var (chapter, action) = RequiredChapterAction(route.Offer, "informant_parley");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Шёпот Миры", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("informant_parley_action", action.ActionId);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Wisdom, action.Check.PrimaryCharacteristic);
        Assert.Equal(2, action.Check.BaseDifficulty);
        Assert.Equal("Ответить Мире Ночной Нити", action.Label);
        Assert.Equal("gadget_infiltration", action.Routing.Success.NextChapterId);
        Assert.Equal("gadget_infiltration", action.Routing.Partial.NextChapterId);
        Assert.Equal("gadget_infiltration", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("old_captain_shift", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["old_captain_shift", "pay_for_rumor", "threaten_contact"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));

        Assert.True(text.Length >= 1500,
            "Daren informant_parley narrative should be a substantial literary page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren informant_parley narrative should unfold across a real scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren informant_parley narrative should keep Daren as the active point-of-view protagonist.");
        Assert.True(CountOccurrences(text, "Мира") >= 5,
            "Daren informant_parley narrative should make Mira a present social scene partner.");
        Assert.True(CountOccurrences(text, "—") >= 6,
            "Daren informant_parley narrative should include a voiced exchange between Daren and Mira.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("rear-road awning meeting place", [["навес"], ["задн"], ["дорог"]]),
            ("wet night and ribbon detail", [["мокр", "дожд"], ["лент"], ["ноч", "тёмн"]]),
            ("old-contact relationship subtext", [["стар", "знаком", "Когда-то"], ["долг", "молч", "памят"]]),
            ("body language and social tension", [["запяст", "пальц", "плеч", "ладон"], ["взгляд", "усмеш", "нож", "тень"]]),
            ("source exposure and guard stakes", [["источник", "слух"], ["страж"], ["погон", "Орвальд"]]),
            ("gallery shift information pressure", [["парол"], ["смен"], ["галер"], ["Лукьян", "ключник"]]),
            ("precision-choice lead-in", [["назов", "ответ"], ["точн", "правильн"], ["довер", "повер"]])
        })
        {
            AssertContainsEveryTermGroup($"informant_parley full-page {context}", text, termGroups);
        }

        AssertNoPlayerFacingTechnicalTerms("informant_parley full-page narrative", text);
    }

    [Fact]
    public void DarenGadgetInfiltration_ReadsAsHookAndLineLiteraryPageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "gadget_infiltration");
        var (chapter, action) = RequiredChapterAction(route.Offer, "gadget_infiltration");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Крюк и леска", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("gadget_infiltration_action", action.ActionId);
        Assert.Equal("ChargeRelease", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Null(action.Check.Config);
        Assert.Equal("Запустить складной крюк", action.Label);
        Assert.Equal("stealth_crossing", action.Routing.Success.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Partial.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Fail.NextChapterId);

        Assert.True(text.Length >= 1500,
            "Daren gadget_infiltration narrative should be a substantial literary page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren gadget_infiltration narrative should unfold across a real scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren gadget_infiltration narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("tower cold stone pressure", [["башн"], ["холод", "ледян"], ["камн"]]),
            ("balcony and courtyard space", [["балкон"], ["двор"]]),
            ("folding hook and line as tactile objects", [["складн"], ["крюк"], ["леск", "шнур", "корд"], ["металл", "желез"]]),
            ("Daren hands body and ascent preparation", [["ладон", "пальц", "плеч", "колен", "дых", "ребр"], ["подтян", "подн", "перен", "согнул", "движ"]]),
            ("guard sound and light stakes", [["страж", "караул"], ["фонар", "свет"], ["звук", "шум", "звон", "скрип", "оклик", "крик"]]),
            ("hook launch anchor climb lead-in", [["запуст", "брос", "метн"], ["зацеп", "якор", "край"], ["подн", "подъем", "лез", "взоб"]])
        })
        {
            AssertContainsEveryTermGroup($"gadget_infiltration full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("gadget_infiltration full-page narrative", text);
    }

    [Fact]
    public void DarenGadgetInfiltrationSuccess_ReadsAsCleanHookAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "gadget_infiltration");
        var (chapter, action) = RequiredChapterAction(route.Offer, "gadget_infiltration");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Крюк и леска", beat.Title);
        Assert.Equal("gadget_infiltration", chapter.ChapterId);
        Assert.Equal("gadget_infiltration_action", action.ActionId);
        Assert.Equal("Запустить складной крюк", action.Label);
        Assert.Equal("ChargeRelease", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Null(action.Check.Config);
        Assert.Equal("stealth_crossing", action.Routing.Success.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Partial.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Fail.NextChapterId);
        Assert.False(string.IsNullOrWhiteSpace(action.PartialText));
        Assert.False(string.IsNullOrWhiteSpace(action.FailText));
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);
        Assert.NotEqual(
            "Крюк ложится на балкон мягко, и Дарен поднимается над двором, пока леска молчит в ладони.",
            text);

        Assert.True(text.Length >= 900,
            "Daren gadget_infiltration success should be a substantial clean hook aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren gadget_infiltration success should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren gadget_infiltration success should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("folding hook and line control", [["складн"], ["крюк"], ["леск"], ["натяж", "натян", "тян"]]),
            ("soft silent catch", [["мягк", "тих", "без зв", "молч"], ["цеп", "зацеп", "лег", "лож", "держ"], ["перил", "балкон", "край"]]),
            ("Daren body breath and hands", [["Дарен"], ["дых", "вдох", "выдох"], ["ладон", "пальц", "рук", "запяст"], ["плеч", "ребр", "колен", "тело"]]),
            ("clean courtyard risk reduction", [["двор"], ["свидетел", "улик", "след", "оклик", "тревог"], ["не остав", "без след", "не подня", "не услыш", "молч", "теряет"]]),
            ("courtyard lantern patrol atmosphere", [["двор"], ["фонар", "патрул", "страж", "караул"], ["мокр", "влаж", "камн", "дожд"]]),
            ("balcony window ascent", [["балкон"], ["окн", "перил", "дерев", "дос"], ["подн", "подтян", "взоб", "перебрал", "перевал"]]),
            ("next silent gallery continuity", [["галер"], ["без звука", "тиш", "молч"], ["дальш", "следующ", "вперёд", "вперед", "служебн", "порог"]])
        })
        {
            AssertContainsEveryTermGroup($"gadget_infiltration success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("gadget_infiltration success aftermath", text);
    }

    [Fact]
    public void DarenGadgetInfiltrationPartial_ReadsAsCostlyHookAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "gadget_infiltration");
        var nextBeat = Assert.Single(route.Beats, item => item.BeatId == "stealth_crossing");
        var (chapter, action) = RequiredChapterAction(route.Offer, "gadget_infiltration");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Крюк и леска", beat.Title);
        Assert.Equal("Галерея без звука", nextBeat.Title);
        Assert.Equal("gadget_infiltration", chapter.ChapterId);
        Assert.Equal("gadget_infiltration_action", action.ActionId);
        Assert.Equal("Запустить складной крюк", action.Label);
        Assert.Equal("ChargeRelease", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Null(action.Check.Config);
        Assert.Equal("stealth_crossing", action.Routing.Success.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Partial.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Fail.NextChapterId);
        Assert.True(
            FindChapterIndex(route.Offer, "gadget_infiltration") < FindChapterIndex(route.Offer, "stealth_crossing"),
            "Daren gadget_infiltration partial should still bridge directly into the silent gallery beat.");

        Assert.Contains("Крюк лёг за внутренний край балкона мягко", action.SuccessText);
        Assert.Contains("галерея без звука", action.SuccessText, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(action.FailText));
        Assert.NotEqual(
            "Крюк держит, но леска звенит по камню; Дарен замирает на балконе, слушая двор.",
            text);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren gadget_infiltration partial should be a substantial costly hook aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren gadget_infiltration partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren gadget_infiltration partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("folding hook and line tension", [["складн"], ["крюк"], ["леск", "шнур", "корд"], ["натяж", "натян", "тян", "держ"]]),
            ("partial cost through sound scrape delay and trace", [["звен", "звяк", "скреж", "скрип", "цок", "шум", "звук"], ["камн", "перил", "дерев", "балкон"], ["след", "царап", "улик", "заруб", "шрам"], ["задерж", "медлен", "опозд", "позд", "потер"]]),
            ("Daren breath body and hand control", [["Дарен"], ["дых", "вдох", "выдох"], ["ладон", "пальц", "рук", "запяст"], ["плеч", "ребр", "колен", "тело", "горл"]]),
            ("courtyard lantern patrol suspicion", [["двор"], ["фонар", "свет", "луч"], ["страж", "патрул", "караул"], ["подозр", "свидетел", "оклик", "слуш", "прислуш"]]),
            ("balcony window continuation", [["балкон"], ["окн", "перил", "дерев", "дос"], ["добрал", "подня", "подтян", "взоб", "перебрал", "перевал"]]),
            ("next silent gallery continuity with mixed success pressure", [["галер"], ["без звука", "тиш", "молч"], ["дальш", "следующ", "впер", "служебн", "порог"], ["цена", "не чист", "сомнен", "памят", "след"]])
        })
        {
            AssertContainsEveryTermGroup($"gadget_infiltration partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertDownstreamDarenResultSurfacesPreserved(route.Offer);
        AssertNoPlayerFacingTechnicalTerms("gadget_infiltration partial aftermath", text);
    }

    [Fact]
    public void DarenGadgetInfiltrationFail_ReadsAsDangerousHookAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "gadget_infiltration");
        var nextBeat = Assert.Single(route.Beats, item => item.BeatId == "stealth_crossing");
        var (chapter, action) = RequiredChapterAction(route.Offer, "gadget_infiltration");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Крюк и леска", beat.Title);
        Assert.Equal("Галерея без звука", nextBeat.Title);
        Assert.Equal("gadget_infiltration", chapter.ChapterId);
        Assert.Equal("gadget_infiltration_action", action.ActionId);
        Assert.Equal("Запустить складной крюк", action.Label);
        Assert.Equal("ChargeRelease", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Null(action.Check.Config);
        Assert.Equal("stealth_crossing", action.Routing.Success.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Partial.NextChapterId);
        Assert.Equal("stealth_crossing", action.Routing.Fail.NextChapterId);
        Assert.True(
            FindChapterIndex(route.Offer, "gadget_infiltration") < FindChapterIndex(route.Offer, "stealth_crossing"),
            "Daren gadget_infiltration fail should still bridge directly into the silent gallery beat.");

        Assert.Contains("Крюк лёг за внутренний край балкона мягко", action.SuccessText);
        Assert.Contains("Складной крюк удержался, но ночь не приняла его молча.", action.PartialText);
        Assert.NotEqual(
            "Крюк срывается с края; шум будит двор, и Дарен успевает уйти в тень только после собачьего лая.",
            text);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren gadget_infiltration fail should be a substantial dangerous hook aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren gadget_infiltration fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren gadget_infiltration fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("folding hook and line fail equipment", [["складн"], ["крюк"], ["леск", "шнур", "корд"], ["натяж", "рван", "сорв", "скольз", "удар", "звен"]]),
            ("failed launch noise and evidence pressure", [["сорв", "вырв", "скольз", "удар", "рван"], ["звон", "грох", "скреж", "шум", "лязг"], ["двор"], ["ули", "след", "царап", "оскол", "облом", "знак"]]),
            ("dog guard pursuit pressure", [["пёс", "собач", "лай"], ["страж", "караул", "патрул"], ["погон", "преслед", "оклик", "крик", "тревог"]]),
            ("Daren breath body and hand control", [["Дарен"], ["дых", "вдох", "выдох", "горл"], ["ладон", "пальц", "рук", "запяст"], ["плеч", "ребр", "колен", "тело"]]),
            ("courtyard lantern patrol atmosphere", [["двор"], ["фонар", "свет", "луч"], ["мокр", "влаж", "камн", "дожд"], ["страж", "патрул", "караул"]]),
            ("balcony window continuation under danger", [["балкон"], ["окн", "перил", "дерев", "дос"], ["тень", "проскольз", "добрал", "подня", "перевал", "внутр"], ["опас", "погон", "тревог", "дальш"]]),
            ("next silent gallery continuity with danger", [["галер"], ["без звука", "тиш", "молч"], ["дальш", "следующ", "порог", "впер"], ["не чист", "след", "погон", "ули", "опас"]])
        })
        {
            AssertContainsEveryTermGroup($"gadget_infiltration fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertDownstreamDarenResultSurfacesPreserved(route.Offer);
        AssertNoPlayerFacingTechnicalTerms("gadget_infiltration fail aftermath", text);
    }

    [Fact]
    public void DarenStealthCrossing_ReadsAsGalleryStealthPageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "stealth_crossing");
        var (chapter, action) = RequiredChapterAction(route.Offer, "stealth_crossing");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Галерея без звука", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("stealth_crossing_action", action.ActionId);
        Assert.Equal("StealthNoise", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("Пройти галерею без шума", action.Label);
        Assert.Equal("guard_interrogation", action.Routing.Success.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Partial.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(6500, RequiredInt(config, "durationMs"));
        Assert.Equal(14, RequiredInt(config, "startingNoise"));
        Assert.Equal(70, RequiredInt(config, "dangerThreshold"));
        Assert.Equal(9, RequiredInt(config, "noiseDriftPerSecond"));
        Assert.Equal(12, RequiredInt(config, "recoveryPerInput"));
        Assert.Equal(800, RequiredInt(config, "allowedOverThresholdMs"));
        Assert.Equal("space", RequiredString(config, "recoveryKey"));
        Assert.Equal("приглушить шаг", RequiredString(config, "recoveryLabel"));
        Assert.Equal("страж слышит шум", RequiredString(config, "warningLabel"));
        var thresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(48, RequiredInt(thresholds, "successMaxNoise"));
        Assert.Equal(0, RequiredInt(thresholds, "successMaxOverThresholdMs"));
        Assert.Equal(76, RequiredInt(thresholds, "partialMaxNoise"));
        Assert.Equal(850, RequiredInt(thresholds, "partialMaxOverThresholdMs"));

        Assert.True(text.Length >= 1500,
            "Daren stealth_crossing narrative should be a substantial gallery stealth page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren stealth_crossing narrative should unfold across a real stealth scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren stealth_crossing narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("gallery portraits dust and confined light", [["галер"], ["портрет"], ["пыл"], ["полос", "луч", "свет"], ["пол", "доск", "паркет", "камн"]]),
            ("sleeping guard pressure", [["страж", "караул", "Лукьян"], ["сон", "спал", "дрем"], ["дых", "храп", "вдох"], ["фонар", "свет"]]),
            ("Daren controlled body movement", [["ладон", "пальц", "плеч", "колен", "ступн", "сапог"], ["дых", "вдох", "выдох"], ["перен", "вес", "согнул", "скольз", "шаг"]]),
            ("silence noise and exposure stakes", [["тиш", "молч", "без звук"], ["шум", "скрип", "треск", "звон", "шорох"], ["разбуд", "прос", "услыш"], ["след", "кабин", "двер"]]),
            ("stealth-noise action lead-in", [["пройти", "перейти", "пересеч", "двин"], ["приглуш", "сдерж", "удерж", "останов"], ["шаг"], ["без шум", "беззвуч", "тихо", "тиш"]])
        })
        {
            AssertContainsEveryTermGroup($"stealth_crossing full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 5),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 2),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -5),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("stealth_crossing full-page narrative", text);
    }

    [Fact]
    public void DarenStealthCrossingPartial_ReadsAsCostlyGalleryAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "stealth_crossing");
        var (chapter, action) = RequiredChapterAction(route.Offer, "stealth_crossing");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Галерея без звука", beat.Title);
        Assert.Equal("stealth_crossing", chapter.ChapterId);
        Assert.Equal("stealth_crossing_action", action.ActionId);
        Assert.Equal("Пройти галерею без шума", action.Label);
        Assert.Equal("StealthNoise", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("guard_interrogation", action.Routing.Success.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Partial.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(6500, RequiredInt(config, "durationMs"));
        Assert.Equal(14, RequiredInt(config, "startingNoise"));
        Assert.Equal(70, RequiredInt(config, "dangerThreshold"));
        Assert.Equal(9, RequiredInt(config, "noiseDriftPerSecond"));
        Assert.Equal(12, RequiredInt(config, "recoveryPerInput"));
        Assert.Equal(800, RequiredInt(config, "allowedOverThresholdMs"));
        Assert.Equal("space", RequiredString(config, "recoveryKey"));
        Assert.Equal("приглушить шаг", RequiredString(config, "recoveryLabel"));
        Assert.Equal("страж слышит шум", RequiredString(config, "warningLabel"));
        var thresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(48, RequiredInt(thresholds, "successMaxNoise"));
        Assert.Equal(0, RequiredInt(thresholds, "successMaxOverThresholdMs"));
        Assert.Equal(76, RequiredInt(thresholds, "partialMaxNoise"));
        Assert.Equal(850, RequiredInt(thresholds, "partialMaxOverThresholdMs"));

        Assert.NotEqual(
            "Один страж шевелится от скрипа; сомнение уже тянется к фонарю, но Дарен удерживает тишину до открытых глаз.",
            text);
        Assert.False(string.IsNullOrWhiteSpace(action.FailText),
            "Daren stealth_crossing fail aftermath should remain authored while partial semantics stay distinct.");
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren stealth_crossing partial should be a substantial costly gallery aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren stealth_crossing partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren stealth_crossing partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("gallery surfaces and stale air", [["галер"], ["портрет"], ["рам", "стекл"], ["пыл", "воздух"], ["портьер", "занавес", "штор", "двер"]]),
            ("floorboard noise and lantern pressure", [["доск", "пол", "паркет"], ["скрип", "треск", "шум", "звук"], ["страж", "караул"], ["фонар", "свет", "луч"]]),
            ("Daren weight breath hand and boot control", [["Дарен"], ["вес", "перен", "согнул", "скольз", "шаг"], ["дых", "вдох", "выдох"], ["ладон", "пальц", "рук"], ["сапог", "ступн", "подошв"]]),
            ("partial cost suspicion and evidence", [["след", "пыл", "отпечат", "царап", "улик"], ["сомнен", "подозр", "запом", "памят"], ["цен", "риск", "задерж", "ошиб", "не чист"]]),
            ("achieved passage and service-door continuity", [["прош", "пересек", "миновал", "добрал", "выбрал"], ["служебн"], ["двер", "порог"], ["коридор", "Лукьян", "ключник"]])
        })
        {
            AssertContainsEveryTermGroup($"stealth_crossing partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 5),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 2),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -5),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("stealth_crossing partial aftermath", text);
    }

    [Fact]
    public void DarenStealthCrossingFail_ReadsAsDangerousGalleryAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "stealth_crossing");
        var (chapter, action) = RequiredChapterAction(route.Offer, "stealth_crossing");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Галерея без звука", beat.Title);
        Assert.Equal("stealth_crossing", chapter.ChapterId);
        Assert.Equal("stealth_crossing_action", action.ActionId);
        Assert.Equal("Пройти галерею без шума", action.Label);
        Assert.Equal("StealthNoise", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("guard_interrogation", action.Routing.Success.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Partial.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(6500, RequiredInt(config, "durationMs"));
        Assert.Equal(14, RequiredInt(config, "startingNoise"));
        Assert.Equal(70, RequiredInt(config, "dangerThreshold"));
        Assert.Equal(9, RequiredInt(config, "noiseDriftPerSecond"));
        Assert.Equal(12, RequiredInt(config, "recoveryPerInput"));
        Assert.Equal(800, RequiredInt(config, "allowedOverThresholdMs"));
        Assert.Equal("space", RequiredString(config, "recoveryKey"));
        Assert.Equal("приглушить шаг", RequiredString(config, "recoveryLabel"));
        Assert.Equal("страж слышит шум", RequiredString(config, "warningLabel"));
        var thresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(48, RequiredInt(thresholds, "successMaxNoise"));
        Assert.Equal(0, RequiredInt(thresholds, "successMaxOverThresholdMs"));
        Assert.Equal(76, RequiredInt(thresholds, "partialMaxNoise"));
        Assert.Equal(850, RequiredInt(thresholds, "partialMaxOverThresholdMs"));

        Assert.NotEqual(
            "Доска отвечает резким треском, и Дарен видит, как в дальнем крыле поднимается тревожный фонарь со свидетелем.",
            text);
        Assert.Contains("Дарен перенёс вес с последней опасной доски", action.SuccessText);
        Assert.Contains("Дарен успел погасить скрип не сразу", action.PartialText);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren stealth_crossing fail should be a substantial dangerous gallery aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren stealth_crossing fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren stealth_crossing fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("gallery portraits dust and listening surfaces", [["галер"], ["портрет"], ["рам", "стекл"], ["пыл", "воздух"], ["слуш", "тиш", "молч"], ["портьер", "занавес", "штор", "двер"]]),
            ("floorboard parquet and noise break", [["доск", "пол", "паркет"], ["треск", "скрип", "шум", "звук", "хруст"], ["слом", "сорвал", "рван", "выдал", "разбуд"]]),
            ("Daren breath body hand and boot control failing", [["Дарен"], ["дых", "вдох", "выдох"], ["ладон", "пальц", "рук"], ["сапог", "ступн", "подошв"], ["вес", "колен", "плеч", "тело", "согнул"]]),
            ("guard lantern witness alarm pursuit and evidence pressure", [["страж", "караул", "Лукьян"], ["фонар", "свет", "луч"], ["свидетел", "увид", "запом"], ["тревог", "погон", "крик", "оклик"], ["след", "улик", "отпечат", "пыл"]]),
            ("continued passage to service-door keykeeper under danger", [["добрал", "миновал", "выбрал", "проскольз", "рванул"], ["служебн"], ["двер", "порог"], ["коридор"], ["ключник", "Лукьян"], ["опас", "давл", "спеш", "обход", "подступ"]])
        })
        {
            AssertContainsEveryTermGroup($"stealth_crossing fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 5),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 2),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -5),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("stealth_crossing fail aftermath", text);
    }

    [Fact]
    public void DarenStealthCrossingSuccess_ReadsAsCleanGalleryAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "stealth_crossing");
        var (chapter, action) = RequiredChapterAction(route.Offer, "stealth_crossing");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Галерея без звука", beat.Title);
        Assert.Equal("stealth_crossing", chapter.ChapterId);
        Assert.Equal("stealth_crossing_action", action.ActionId);
        Assert.Equal("Пройти галерею без шума", action.Label);
        Assert.Equal("StealthNoise", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("guard_interrogation", action.Routing.Success.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Partial.NextChapterId);
        Assert.Equal("guard_interrogation", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(6500, RequiredInt(config, "durationMs"));
        Assert.Equal(14, RequiredInt(config, "startingNoise"));
        Assert.Equal(70, RequiredInt(config, "dangerThreshold"));
        Assert.Equal(9, RequiredInt(config, "noiseDriftPerSecond"));
        Assert.Equal(12, RequiredInt(config, "recoveryPerInput"));
        Assert.Equal(800, RequiredInt(config, "allowedOverThresholdMs"));
        Assert.Equal("space", RequiredString(config, "recoveryKey"));
        Assert.Equal("приглушить шаг", RequiredString(config, "recoveryLabel"));
        Assert.Equal("страж слышит шум", RequiredString(config, "warningLabel"));
        var thresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(48, RequiredInt(thresholds, "successMaxNoise"));
        Assert.Equal(0, RequiredInt(thresholds, "successMaxOverThresholdMs"));
        Assert.Equal(76, RequiredInt(thresholds, "partialMaxNoise"));
        Assert.Equal(850, RequiredInt(thresholds, "partialMaxOverThresholdMs"));

        Assert.False(string.IsNullOrWhiteSpace(action.FailText),
            "Daren stealth_crossing fail aftermath should remain authored while success semantics stay distinct.");
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);
        Assert.NotEqual(
            "Дарен переводит вес с доски на доску, проходит чисто и не оставляет галерее ни следа, ни проснувшегося дыхания.",
            text);

        Assert.True(text.Length >= 900,
            "Daren stealth_crossing success should be a substantial clean gallery aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren stealth_crossing success should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren stealth_crossing success should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("gallery surfaces and dead watching", [["галер"], ["доск", "пол", "паркет"], ["портрет"], ["рам", "стекл"], ["пыл"]]),
            ("Daren weight breath hand and boot control", [["Дарен"], ["вес", "перен", "согнул", "скольз", "шаг"], ["дых", "вдох", "выдох"], ["ладон", "пальц", "рук"], ["сапог", "ступн", "подошв"]]),
            ("quiet floorboard passage", [["доск", "пол", "паркет"], ["скрип", "треск", "шум", "звук"], ["приглуш", "удерж", "молч", "тиш"], ["прош", "пересек", "миновал", "добрал"]]),
            ("sleeping guard no alarm no evidence", [["страж", "караул"], ["сон", "спящ", "храп", "дых"], ["тревог", "крик", "оклик", "фонар"], ["след", "улик", "свидетел"], ["не прос", "не разбуд", "без след", "не остав"]]),
            ("curtain service-door and keykeeper continuity", [["портьер", "занавес", "штор"], ["служебн"], ["двер", "порог"], ["Лукьян", "ключник"], ["коридор", "кабин", "дальш", "следующ"]])
        })
        {
            AssertContainsEveryTermGroup($"stealth_crossing success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 5),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 2),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -5),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("stealth_crossing success aftermath", text);
    }

    [Fact]
    public void DarenGuardInterrogation_ReadsAsKeykeeperLiteraryPageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "guard_interrogation");
        var (chapter, action) = RequiredChapterAction(route.Offer, "guard_interrogation");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Ключник в галерее", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("guard_interrogation_action", action.ActionId);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Persuasion, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("Успокоить Лукьяна у служебной двери", action.Label);
        Assert.Equal("lock_pick", action.Routing.Success.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Partial.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("mira_phrase", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["mira_phrase", "late_order", "hide_face"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(
            ["Передать фразу Миры", "Сослаться на поздний приказ", "Спрятать лицо"],
            choices.Select(choice => RequiredString(choice, "label")));
        Assert.Equal(
            [
                "Дарен говорит тихую фразу, которую Лукьян ждал от ночной связной.",
                "Дарен изображает посыльного с приказом по дому.",
                "Дарен пытается пройти мимо без ответа."
            ],
            choices.Select(choice => RequiredString(choice, "description")));
        Assert.Equal(
            [
                "Фраза превращает ключника в сонного союзника.",
                "Приказ звучит правдоподобно, но Лукьян запомнит лицо.",
                "Молчание для стража громче любого скрипа."
            ],
            choices.Select(choice => RequiredString(choice, "hint")));

        Assert.False(string.IsNullOrWhiteSpace(action.PartialText));
        Assert.False(string.IsNullOrWhiteSpace(action.FailText));
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 1500,
            "Daren guard_interrogation narrative should be a substantial keykeeper social-pressure page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren guard_interrogation narrative should unfold across a real exchange, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren guard_interrogation narrative should keep Daren as the active point-of-view protagonist.");
        Assert.True(CountOccurrences(text, "Лукьян") >= 5,
            "Daren guard_interrogation narrative should make Lukyan a present social scene partner.");
        Assert.True(CountOccurrences(text, "—") >= 6,
            "Daren guard_interrogation narrative should include a voiced exchange between Daren and Lukyan.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("gallery and service-door continuation", [["галер"], ["служ"], ["двер", "порог"], ["кабин", "коридор"]]),
            ("Lukyan keykeeper body and tools", [["Лукьян"], ["ключник", "страж"], ["стар", "сед"], ["фонар"], ["ключ", "кольц"], ["ладон", "пальц", "рук", "плеч", "лиц", "бород", "ус"]]),
            ("suspicion question and witness stakes", [["вопрос", "спрос"], ["подозр", "сомнен", "недовер"], ["ответ", "фраз"], ["свидетел", "тревог", "оклик", "погон"]]),
            ("Daren observation and improvisation", [["Дарен"], ["замет", "вид", "слуш", "счит"], ["Мира", "фраз", "слух"], ["импровиз", "изобраз", "сказал", "назвал", "сыграл"], ["капюшон", "плащ", "дых", "ладон", "лицо"]]),
            ("social choice lead-in", [["успоко", "убед", "унять"], ["выб", "реш", "ответ", "назвать"], ["молч", "приказ", "фраз", "лицо"], ["пропуст", "двер", "кабин"]])
        })
        {
            AssertContainsEveryTermGroup($"guard_interrogation full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("guard_interrogation full-page narrative", text);
    }

    [Fact]
    public void DarenGuardInterrogationSuccess_ReadsAsCleanKeykeeperAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "guard_interrogation");
        var (chapter, action) = RequiredChapterAction(route.Offer, "guard_interrogation");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Ключник в галерее", beat.Title);
        Assert.Equal("guard_interrogation", chapter.ChapterId);
        Assert.Equal("guard_interrogation_action", action.ActionId);
        Assert.Equal("Успокоить Лукьяна у служебной двери", action.Label);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Persuasion, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("lock_pick", action.Routing.Success.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Partial.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("mira_phrase", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["mira_phrase", "late_order", "hide_face"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(
            ["Передать фразу Миры", "Сослаться на поздний приказ", "Спрятать лицо"],
            choices.Select(choice => RequiredString(choice, "label")));
        Assert.Equal(
            [
                "Дарен говорит тихую фразу, которую Лукьян ждал от ночной связной.",
                "Дарен изображает посыльного с приказом по дому.",
                "Дарен пытается пройти мимо без ответа."
            ],
            choices.Select(choice => RequiredString(choice, "description")));
        Assert.Equal(
            [
                "Фраза превращает ключника в сонного союзника.",
                "Приказ звучит правдоподобно, но Лукьян запомнит лицо.",
                "Молчание для стража громче любого скрипа."
            ],
            choices.Select(choice => RequiredString(choice, "hint")));

        Assert.False(string.IsNullOrWhiteSpace(action.PartialText));
        Assert.False(string.IsNullOrWhiteSpace(action.FailText));
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren guard_interrogation success should be a substantial clean keykeeper aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren guard_interrogation success should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren guard_interrogation success should keep Daren as the active point-of-view protagonist.");
        Assert.NotEqual(
            "Лукьян Седой Ключник узнаёт пароль Миры, отворачивает фонарь и оставляет Дарену чистую дверь к кабинету.",
            text);

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("Mira phrase and social proof", [["Мира"], ["фраз", "парол"], ["связн", "передал", "велел", "ночн"], ["довер", "узна", "правильн", "знак"]]),
            ("Lukyan lantern keys voice and breath", [["Лукьян"], ["фонар"], ["ключ", "связк", "кольц"], ["голос", "хрип", "дых", "выдох"], ["стар", "сед", "палец", "ладон"]]),
            ("Daren face voice and body control", [["Дарен"], ["лицо", "губ", "щёк", "капюш"], ["голос", "сказал", "произн"], ["дых", "плеч", "ладон", "рук"], ["ровн", "спокой", "медлен"]]),
            ("reduced witness and alarm risk", [["страж", "карауль"], ["сон", "спящ", "храп"], ["тревог", "крик", "оклик"], ["свидетел"], ["тиш", "молч"]]),
            ("clean service-door passage", [["служебн"], ["двер", "порог"], ["пропуст", "отступ", "отвор", "отодвин"], ["чист", "без след", "не остав", "не зацеп"]]),
            ("cabinet corridor continuity", [["коридор"], ["кабин"], ["замок", "скваж", "дальш"], ["следующ", "вперед", "за двер"]])
        })
        {
            AssertContainsEveryTermGroup($"guard_interrogation success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("guard_interrogation success aftermath", text);
    }

    [Fact]
    public void DarenGuardInterrogationPartial_ReadsAsMixedKeykeeperAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "guard_interrogation");
        var (chapter, action) = RequiredChapterAction(route.Offer, "guard_interrogation");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Ключник в галерее", beat.Title);
        Assert.Equal("guard_interrogation", chapter.ChapterId);
        Assert.Equal("guard_interrogation_action", action.ActionId);
        Assert.Equal("Успокоить Лукьяна у служебной двери", action.Label);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Persuasion, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("lock_pick", action.Routing.Success.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Partial.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Fail.NextChapterId);
        Assert.True(
            FindChapterIndex(route.Offer, "guard_interrogation") < FindChapterIndex(route.Offer, "lock_pick"),
            "Daren guard_interrogation should still bridge directly into the cabinet lock beat.");

        var config = RequiredConfig(action);
        Assert.Equal("mira_phrase", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["mira_phrase", "late_order", "hide_face"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(
            ["Передать фразу Миры", "Сослаться на поздний приказ", "Спрятать лицо"],
            choices.Select(choice => RequiredString(choice, "label")));
        Assert.Equal(
            [
                "Дарен говорит тихую фразу, которую Лукьян ждал от ночной связной.",
                "Дарен изображает посыльного с приказом по дому.",
                "Дарен пытается пройти мимо без ответа."
            ],
            choices.Select(choice => RequiredString(choice, "description")));
        Assert.Equal(
            [
                "Фраза превращает ключника в сонного союзника.",
                "Приказ звучит правдоподобно, но Лукьян запомнит лицо.",
                "Молчание для стража громче любого скрипа."
            ],
            choices.Select(choice => RequiredString(choice, "hint")));

        Assert.Contains("Зелёная нить не звенит на чужом кольце", action.SuccessText);
        Assert.Contains("Проход открылся чисто", action.SuccessText);
        Assert.False(string.IsNullOrWhiteSpace(action.FailText));
        Assert.NotEqual("Лукьян пропускает Дарена с сомнением, но его взгляд цепляется за плащ и уже ищет вторую встречу.", text);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren guard_interrogation partial should be a substantial mixed keykeeper aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren guard_interrogation partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren guard_interrogation partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("late order and imperfect social proof", [["приказ", "поручен", "посыльн"], ["Мир", "фраз", "имен", "связ"], ["правдопод", "запозд", "поздн", "непол", "крив"], ["ответ", "сказал", "произн", "голос"]]),
            ("Lukyan lantern keys voice and breath", [["Лукьян"], ["фонар"], ["ключ", "связк", "кольц"], ["голос", "хрип", "дых", "выдох"], ["стар", "сед", "ладон", "пальц"]]),
            ("Daren face voice and body control", [["Дарен"], ["лицо", "капюш", "плащ", "щёк"], ["голос", "сказал", "произн"], ["дых", "плеч", "ладон", "рук"], ["ровн", "спокой", "сдерж", "не улыб"]]),
            ("sleeping guard gallery silence and service door", [["галер"], ["страж", "карауль"], ["сон", "спящ", "храп"], ["служебн"], ["двер", "порог"], ["тиш", "молч"]]),
            ("passage with lingering witness evidence risk", [["пропуст", "прош", "проход", "отступ", "отвор"], ["сомнен", "подозр", "недовер"], ["запом", "памят", "лицо", "плащ", "деталь"], ["журнал", "запис", "строк", "помет", "свидетел", "след"], ["потом", "утр", "позже", "втор", "задерж"]]),
            ("cabinet corridor continuity", [["коридор"], ["кабин"], ["замок", "скваж"], ["дальше", "следующ", "впер"], ["за двер", "позади", "за спин"]])
        })
        {
            AssertContainsEveryTermGroup($"guard_interrogation partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("guard_interrogation partial aftermath", text);
    }

    [Fact]
    public void DarenGuardInterrogationFail_ReadsAsDangerousKeykeeperAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "guard_interrogation");
        var (chapter, action) = RequiredChapterAction(route.Offer, "guard_interrogation");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Ключник в галерее", beat.Title);
        Assert.Equal("guard_interrogation", chapter.ChapterId);
        Assert.Equal("guard_interrogation_action", action.ActionId);
        Assert.Equal("Успокоить Лукьяна у служебной двери", action.Label);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Persuasion, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("lock_pick", action.Routing.Success.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Partial.NextChapterId);
        Assert.Equal("lock_pick", action.Routing.Fail.NextChapterId);
        Assert.True(
            FindChapterIndex(route.Offer, "guard_interrogation") < FindChapterIndex(route.Offer, "lock_pick"),
            "Daren guard_interrogation fail should still bridge directly into the cabinet lock beat.");

        var config = RequiredConfig(action);
        Assert.Equal("mira_phrase", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["mira_phrase", "late_order", "hide_face"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(
            ["Передать фразу Миры", "Сослаться на поздний приказ", "Спрятать лицо"],
            choices.Select(choice => RequiredString(choice, "label")));
        Assert.Equal(
            [
                "Дарен говорит тихую фразу, которую Лукьян ждал от ночной связной.",
                "Дарен изображает посыльного с приказом по дому.",
                "Дарен пытается пройти мимо без ответа."
            ],
            choices.Select(choice => RequiredString(choice, "description")));
        Assert.Equal(
            [
                "Фраза превращает ключника в сонного союзника.",
                "Приказ звучит правдоподобно, но Лукьян запомнит лицо.",
                "Молчание для стража громче любого скрипа."
            ],
            choices.Select(choice => RequiredString(choice, "hint")));

        Assert.Contains("Зелёная нить не звенит на чужом кольце", action.SuccessText);
        Assert.Contains("Проход открылся чисто", action.SuccessText);
        Assert.Contains("Дарен понял, что точной фразы Миры", action.PartialText);
        Assert.Contains("Проход был выигран, но не очищен", action.PartialText);
        Assert.NotEqual("Лукьян поднимает фонарь к лицу Дарена, и в галерее рождается свидетель, которого нельзя назвать случайным.", text);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren guard_interrogation fail should be a substantial dangerous keykeeper aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren guard_interrogation fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren guard_interrogation fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("failed answer hidden face and missing Mira authority", [["молч", "без ответа", "не ответ"], ["Мир", "фраз", "парол", "Ночная"], ["капюш", "лицо", "спрят"], ["ответ", "слово", "сказ"]]),
            ("Lukyan lantern keys voice and witness reaction", [["Лукьян"], ["фонар"], ["ключ", "связк", "кольц"], ["голос", "крик", "оклик", "хрип"], ["свидетел", "памят", "запом"]]),
            ("Daren exposed face and body control", [["Дарен"], ["лицо", "щёк", "губ", "подбород"], ["плеч", "ладон", "пальц", "дых"], ["сдерж", "замер", "удерж", "ровн"]]),
            ("sleeping guard gallery silence", [["галер"], ["сон", "спящ", "храп"], ["страж", "карауль"], ["тиш", "молч"]]),
            ("alarm evidence and pursuit pressure", [["тревог", "оклик", "крик"], ["погон", "преслед"], ["ули", "след", "журнал", "запис"], ["страж", "караул"]]),
            ("service-door passage under danger", [["служебн"], ["двер", "порог"], ["проход", "прош", "проскольз", "шаг"], ["опас", "задерж", "вслед", "за спин"]]),
            ("cabinet corridor continuity", [["коридор"], ["кабин"], ["замок", "скваж"], ["дальше", "впер", "следующ"]])
        })
        {
            AssertContainsEveryTermGroup($"guard_interrogation fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("guard_interrogation fail aftermath", text);
    }

    [Fact]
    public void DarenLockPick_ReadsAsCabinetLockLiteraryPageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "lock_pick");
        var (chapter, action) = RequiredChapterAction(route.Offer, "lock_pick");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Замок кабинета", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("lock_pick_action", action.ActionId);
        Assert.Equal("LockPinSet", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("Выставить штифты замка", action.Label);
        Assert.Equal("rune_memory", action.Routing.Success.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Partial.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(3, RequiredInt(config, "pinCount"));
        var pinWindows = RequiredObjectArray(config, "pinWindows");
        Assert.Equal([1, 2, 3], pinWindows.Select(pin => RequiredInt(pin, "pin")));
        Assert.Equal([18, 44, 68], pinWindows.Select(pin => RequiredInt(pin, "min")));
        Assert.Equal([32, 58, 82], pinWindows.Select(pin => RequiredInt(pin, "max")));
        Assert.Equal(["нижний штифт", "средний штифт", "верхний штифт"], pinWindows.Select(pin => RequiredString(pin, "label")));
        Assert.Equal(12000, RequiredInt(config, "timerMs"));
        Assert.Equal(6, RequiredInt(config, "pickDurability"));
        Assert.Equal(2, RequiredInt(config, "maxMistakes"));
        Assert.Equal(3, RequiredInt(config, "pinDriftPerSecond"));
        Assert.Equal("q", RequiredString(config, "adjustKey"));
        Assert.Equal("space", RequiredString(config, "setKey"));
        Assert.Equal("штифт", RequiredString(config, "pinLabel"));
        Assert.Equal("прочность отмычки", RequiredString(config, "durabilityLabel"));
        Assert.Equal("замок шумит", RequiredString(config, "warningLabel"));
        var gradeThresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(6500, RequiredInt(gradeThresholds, "successMaxTimeMs"));
        Assert.Equal(0, RequiredInt(gradeThresholds, "successMaxMistakes"));
        Assert.Equal(11000, RequiredInt(gradeThresholds, "partialMaxTimeMs"));
        Assert.Equal(2, RequiredInt(gradeThresholds, "partialMaxMistakes"));

        Assert.True(text.Length >= 1500,
            "Daren lock_pick narrative should be a substantial cabinet-lock burglary page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren lock_pick narrative should unfold across a tactile scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren lock_pick narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("cabinet threshold and old lock", [["кабин"], ["двер", "порог"], ["замок"], ["стар"]]),
            ("keyhole plate and traceable scratches", [["скваж", "ключев"], ["наклад", "пластин"], ["царап"], ["след", "улик"]]),
            ("pins and lock-picking craft", [["штифт"], ["отмыч", "крюч", "щуп", "натяж"], ["выстав", "подн", "прижал", "поверн"], ["слуш", "слыш"]]),
            ("Daren body control", [["ладон", "пальц", "рук"], ["дых", "вдох", "выдох"], ["слуш", "слыш", "ух"], ["пульс", "сердц", "плеч"]]),
            ("stealth and evidence pressure", [["шум", "звон", "щелк", "скрип"], ["страж", "Лукьян", "свидетел"], ["посох"], ["пыль", "след", "улик"]]),
            ("LockPinSet lead-in", [["штифт"], ["выстав", "постав", "посад", "пойм"], ["замер", "момент", "окно"], ["откры", "поверн"]])
        })
        {
            AssertContainsEveryTermGroup($"lock_pick full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("lock_pick full-page narrative", text);
    }

    [Fact]
    public void DarenLockPickSuccess_ReadsAsCleanCabinetAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "lock_pick");
        var (chapter, action) = RequiredChapterAction(route.Offer, "lock_pick");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Замок кабинета", beat.Title);
        Assert.Equal("lock_pick", chapter.ChapterId);
        Assert.Equal("lock_pick_action", action.ActionId);
        Assert.Equal("Выставить штифты замка", action.Label);
        Assert.Equal("LockPinSet", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("rune_memory", action.Routing.Success.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Partial.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(3, RequiredInt(config, "pinCount"));
        var pinWindows = RequiredObjectArray(config, "pinWindows");
        Assert.Equal([1, 2, 3], pinWindows.Select(pin => RequiredInt(pin, "pin")));
        Assert.Equal([18, 44, 68], pinWindows.Select(pin => RequiredInt(pin, "min")));
        Assert.Equal([32, 58, 82], pinWindows.Select(pin => RequiredInt(pin, "max")));
        Assert.Equal(["нижний штифт", "средний штифт", "верхний штифт"], pinWindows.Select(pin => RequiredString(pin, "label")));
        Assert.Equal(12000, RequiredInt(config, "timerMs"));
        Assert.Equal(6, RequiredInt(config, "pickDurability"));
        Assert.Equal(2, RequiredInt(config, "maxMistakes"));
        Assert.Equal(3, RequiredInt(config, "pinDriftPerSecond"));
        Assert.Equal("q", RequiredString(config, "adjustKey"));
        Assert.Equal("space", RequiredString(config, "setKey"));
        Assert.Equal("штифт", RequiredString(config, "pinLabel"));
        Assert.Equal("прочность отмычки", RequiredString(config, "durabilityLabel"));
        Assert.Equal("замок шумит", RequiredString(config, "warningLabel"));
        var gradeThresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(6500, RequiredInt(gradeThresholds, "successMaxTimeMs"));
        Assert.Equal(0, RequiredInt(gradeThresholds, "successMaxMistakes"));
        Assert.Equal(11000, RequiredInt(gradeThresholds, "partialMaxTimeMs"));
        Assert.Equal(2, RequiredInt(gradeThresholds, "partialMaxMistakes"));

        Assert.Contains("Дарен", action.PartialText);
        Assert.Contains("царап", action.PartialText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("наклад", action.PartialText, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Дарен", action.FailText);
        Assert.Contains("страж", action.FailText, StringComparison.OrdinalIgnoreCase);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.NotEqual("Штифты становятся ровно, и Дарен открывает кабинет без следа, так тихо, что пыль на ручке не дрожит.", text);
        Assert.True(text.Length >= 900,
            "Daren lock_pick success should be a substantial clean cabinet-lock aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren lock_pick success should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren lock_pick success should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("pins and pick craft", [["штифт"], ["отмыч", "крюч", "щуп"], ["натяж", "поворот", "подд", "выстав"], ["замок", "скваж"]]),
            ("Daren hands breath and body control", [["Дарен"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох", "горл"], ["плеч", "сердц", "пульс", "ребр"]]),
            ("clean no-trace no-alarm success", [["чист", "без след", "не остав", "нет след"], ["царап", "наклад", "пластин", "бронз"], ["тиш", "молч", "без шума", "не дрож"], ["тревог", "крик", "страж", "Лукьян", "свидетел"]]),
            ("cabinet opening aftermath", [["кабин"], ["двер", "створк"], ["откры", "подал"], ["пыль", "руч"]]),
            ("next rune and futlar continuity", [["футляр"], ["рун"], ["дверц"], ["посох"], ["дальше", "следующ"]])
        })
        {
            AssertContainsEveryTermGroup($"lock_pick success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("lock_pick success aftermath", text);
    }

    [Fact]
    public void DarenLockPickPartial_ReadsAsMixedCabinetAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "lock_pick");
        var (chapter, action) = RequiredChapterAction(route.Offer, "lock_pick");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Замок кабинета", beat.Title);
        Assert.Equal("lock_pick", chapter.ChapterId);
        Assert.Equal("lock_pick_action", action.ActionId);
        Assert.Equal("Выставить штифты замка", action.Label);
        Assert.Equal("LockPinSet", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("rune_memory", action.Routing.Success.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Partial.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Fail.NextChapterId);
        Assert.True(
            FindChapterIndex(route.Offer, "lock_pick") < FindChapterIndex(route.Offer, "rune_memory"),
            "Daren lock_pick should still bridge directly into the rune-memory beat.");

        var config = RequiredConfig(action);
        Assert.Equal(3, RequiredInt(config, "pinCount"));
        var pinWindows = RequiredObjectArray(config, "pinWindows");
        Assert.Equal([1, 2, 3], pinWindows.Select(pin => RequiredInt(pin, "pin")));
        Assert.Equal([18, 44, 68], pinWindows.Select(pin => RequiredInt(pin, "min")));
        Assert.Equal([32, 58, 82], pinWindows.Select(pin => RequiredInt(pin, "max")));
        Assert.Equal(["нижний штифт", "средний штифт", "верхний штифт"], pinWindows.Select(pin => RequiredString(pin, "label")));
        Assert.Equal(12000, RequiredInt(config, "timerMs"));
        Assert.Equal(6, RequiredInt(config, "pickDurability"));
        Assert.Equal(2, RequiredInt(config, "maxMistakes"));
        Assert.Equal(3, RequiredInt(config, "pinDriftPerSecond"));
        Assert.Equal("q", RequiredString(config, "adjustKey"));
        Assert.Equal("space", RequiredString(config, "setKey"));
        Assert.Equal("штифт", RequiredString(config, "pinLabel"));
        Assert.Equal("прочность отмычки", RequiredString(config, "durabilityLabel"));
        Assert.Equal("замок шумит", RequiredString(config, "warningLabel"));
        var gradeThresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(6500, RequiredInt(gradeThresholds, "successMaxTimeMs"));
        Assert.Equal(0, RequiredInt(gradeThresholds, "successMaxMistakes"));
        Assert.Equal(11000, RequiredInt(gradeThresholds, "partialMaxTimeMs"));
        Assert.Equal(2, RequiredInt(gradeThresholds, "partialMaxMistakes"));

        Assert.Contains("Чистая работа оставляет после себя не победу, а отсутствие истории.", action.SuccessText);
        Assert.Contains("кабинет, который всё ещё верил, что его замок никто не будил.", action.SuccessText);
        Assert.Contains("Дарен", action.FailText);
        Assert.Contains("страж", action.FailText, StringComparison.OrdinalIgnoreCase);
        var (_, runeAction) = RequiredChapterAction(route.Offer, "rune_memory");
        Assert.Contains("аккуратно погашенную печать", runeAction.SuccessText);
        Assert.Contains("синяя трещина в стекле", runeAction.PartialText);
        Assert.Contains("дом, который держит в памяти имя Дарена", runeAction.FailText);

        Assert.NotEqual("Замок сдаётся, но отмычка царапает накладку; Дарен уносит этот след вместе с тревогой.", text);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren lock_pick partial should be a substantial mixed cabinet-lock aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren lock_pick partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren lock_pick partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("pins and pick craft", [["штифт"], ["отмыч", "крюч", "щуп"], ["натяж", "поворот", "подд", "выстав"], ["замок", "скваж"]]),
            ("Daren breath body and hand control", [["Дарен"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох", "горл"], ["плеч", "сердц", "пульс", "ребр"]]),
            ("partial trace cost and later evidence", [["царап", "след", "улик", "шрам"], ["наклад", "пластин", "бронз"], ["тревог", "сомнен", "задерж", "цена", "долг", "опозд"], ["страж", "Лукьян", "свидетел", "утр"]]),
            ("cabinet opens despite the cost", [["кабин"], ["двер", "створк", "порог"], ["откры", "подал", "впуст", "шагнул"], ["тиш", "молч", "скрип", "щелк", "звук"]]),
            ("next rune and futlar continuity", [["футляр"], ["рун"], ["дверц"], ["посох"], ["дальше", "следующ", "впер"]])
        })
        {
            AssertContainsEveryTermGroup($"lock_pick partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("lock_pick partial aftermath", text);
    }

    [Fact]
    public void DarenLockPickFail_ReadsAsDangerousCabinetAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "lock_pick");
        var (chapter, action) = RequiredChapterAction(route.Offer, "lock_pick");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Замок кабинета", beat.Title);
        Assert.Equal("lock_pick", chapter.ChapterId);
        Assert.Equal("lock_pick_action", action.ActionId);
        Assert.Equal("Выставить штифты замка", action.Label);
        Assert.Equal("LockPinSet", action.Check.Type);
        Assert.Equal(Characteristics.Dexterity, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("rune_memory", action.Routing.Success.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Partial.NextChapterId);
        Assert.Equal("rune_memory", action.Routing.Fail.NextChapterId);
        Assert.True(
            FindChapterIndex(route.Offer, "lock_pick") < FindChapterIndex(route.Offer, "rune_memory"),
            "Daren lock_pick fail should still bridge directly into the rune-memory beat.");

        var config = RequiredConfig(action);
        Assert.Equal(3, RequiredInt(config, "pinCount"));
        var pinWindows = RequiredObjectArray(config, "pinWindows");
        Assert.Equal([1, 2, 3], pinWindows.Select(pin => RequiredInt(pin, "pin")));
        Assert.Equal([18, 44, 68], pinWindows.Select(pin => RequiredInt(pin, "min")));
        Assert.Equal([32, 58, 82], pinWindows.Select(pin => RequiredInt(pin, "max")));
        Assert.Equal(["нижний штифт", "средний штифт", "верхний штифт"], pinWindows.Select(pin => RequiredString(pin, "label")));
        Assert.Equal(12000, RequiredInt(config, "timerMs"));
        Assert.Equal(6, RequiredInt(config, "pickDurability"));
        Assert.Equal(2, RequiredInt(config, "maxMistakes"));
        Assert.Equal(3, RequiredInt(config, "pinDriftPerSecond"));
        Assert.Equal("q", RequiredString(config, "adjustKey"));
        Assert.Equal("space", RequiredString(config, "setKey"));
        Assert.Equal("штифт", RequiredString(config, "pinLabel"));
        Assert.Equal("прочность отмычки", RequiredString(config, "durabilityLabel"));
        Assert.Equal("замок шумит", RequiredString(config, "warningLabel"));
        var gradeThresholds = RequiredObject(config, "gradeThresholds");
        Assert.Equal(6500, RequiredInt(gradeThresholds, "successMaxTimeMs"));
        Assert.Equal(0, RequiredInt(gradeThresholds, "successMaxMistakes"));
        Assert.Equal(11000, RequiredInt(gradeThresholds, "partialMaxTimeMs"));
        Assert.Equal(2, RequiredInt(gradeThresholds, "partialMaxMistakes"));

        Assert.Contains("Чистая работа оставляет после себя не победу, а отсутствие истории.", action.SuccessText);
        Assert.Contains("кабинет, который всё ещё верил, что его замок никто не будил.", action.SuccessText);
        Assert.Contains("на накладке уже жила улика", action.PartialText);
        Assert.Contains("Замок открыл путь, но не очистил его", action.PartialText);
        var (_, runeAction) = RequiredChapterAction(route.Offer, "rune_memory");
        Assert.Contains("аккуратно погашенную печать", runeAction.SuccessText);
        Assert.Contains("синяя трещина в стекле", runeAction.PartialText);
        Assert.Contains("дом, который держит в памяти имя Дарена", runeAction.FailText);

        Assert.NotEqual(
            "Замок щёлкает слишком громко, оставляя улику на накладке, и Дарен слышит, как за стеной меняется дыхание стражи.",
            text);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren lock_pick fail should be a substantial dangerous cabinet-lock aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren lock_pick fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren lock_pick fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("pins and failed pick craft", [["штифт"], ["отмыч", "крюч", "щуп"], ["натяж", "поворот", "подд", "выстав", "сорв"], ["замок", "скваж"], ["ошиб", "крив", "дрог", "лом", "срыв"]]),
            ("Daren breath body and hand control", [["Дарен"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох", "горл"], ["плеч", "сердц", "пульс", "ребр"]]),
            ("loud lock consequence", [["щелк", "щёлк", "звук", "грох", "стук"], ["громк", "резк", "сух", "звон", "шум"], ["замок", "кабин", "двер"]]),
            ("trace evidence and pursuit pressure", [["царап", "след", "улик", "шрам"], ["наклад", "пластин", "бронз"], ["пыль", "прах"], ["погон", "преслед", "опас", "выда", "запом"]]),
            ("guard house awareness", [["страж", "Лукьян", "караул", "свидетел"], ["дыхан", "шаг", "фонар", "коридор", "стен"], ["дом", "помест", "кабинет"], ["слуш", "слыш", "просып", "запом"]]),
            ("cabinet forced movement", [["кабин"], ["двер", "створк", "порог"], ["подал", "откры", "рван", "впуст", "протис"], ["заклин", "упер", "тяж", "скрип", "сил"]]),
            ("next rune and futlar continuity", [["футляр"], ["рун"], ["дверц"], ["посох"], ["дальше", "следующ", "впер", "вперед"]])
        })
        {
            AssertContainsEveryTermGroup($"lock_pick fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 3),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 1),
            ("loot", 0),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("lock_pick fail aftermath", text);
    }

    [Fact]
    public void DarenRuneMemory_ReadsAsRuneWardMemoryPageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "rune_memory");
        var (chapter, action) = RequiredChapterAction(route.Offer, "rune_memory");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Руны на дверце", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("rune_memory_action", action.ActionId);
        Assert.Equal("PatternMemory", action.Check.Type);
        Assert.Equal(Characteristics.Perception, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("Повторить узор защитных рун", action.Label);
        Assert.Equal("ward_steward_parley", action.Routing.Success.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Partial.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["q", "w", "e", "space"], RequiredStringArray(config, "alphabet"));
        Assert.Equal(4, RequiredInt(config, "sequenceLength"));
        Assert.Equal(2400, RequiredInt(config, "revealMs"));
        Assert.Equal(6500, RequiredInt(config, "inputTimeoutMs"));
        Assert.Equal(1, RequiredInt(config, "allowedMistakes"));

        Assert.True(text.Length >= 1500,
            "Daren rune_memory narrative should be a substantial rune-ward memory page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren rune_memory narrative should unfold across a real magical-security scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren rune_memory narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("case door glass and cold blue runes", [["футляр"], ["дверц", "стекл"], ["син", "голуб"], ["рун"]]),
            ("magical ward and watchful house pressure", [["защит", "печат", "замок", "вард"], ["дом"], ["смотр", "слуш", "наблюд", "запом"]]),
            ("Daren eyes breath memory and body craft", [["глаз", "век", "зрач", "взгляд"], ["дых", "вдох", "выдох"], ["памят", "запом", "счит"], ["пальц", "ладон", "рук", "плеч"]]),
            ("alarm trace guard stakes before theft", [["тревог", "сигнал"], ["след", "улик", "страж", "караул", "Лукьян"], ["посох", "краж"]]),
            ("PatternMemory lead-in through ordered rune repetition", [["узор", "последователь", "знак"], ["повтор", "перелож", "назов"], ["поряд", "ошиб"], ["руна"]])
        })
        {
            AssertContainsEveryTermGroup($"rune_memory full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 3),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -3),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("rune_memory full-page narrative", text);
    }

    [Fact]
    public void DarenRuneMemoryPartial_ReadsAsCostlyRuneAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "rune_memory");
        var (chapter, action) = RequiredChapterAction(route.Offer, "rune_memory");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Руны на дверце", beat.Title);
        Assert.Equal("rune_memory", chapter.ChapterId);
        Assert.Equal("rune_memory_action", action.ActionId);
        Assert.Equal("Повторить узор защитных рун", action.Label);
        Assert.Equal("PatternMemory", action.Check.Type);
        Assert.Equal(Characteristics.Perception, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("ward_steward_parley", action.Routing.Success.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Partial.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["q", "w", "e", "space"], RequiredStringArray(config, "alphabet"));
        Assert.Equal(4, RequiredInt(config, "sequenceLength"));
        Assert.Equal(2400, RequiredInt(config, "revealMs"));
        Assert.Equal(6500, RequiredInt(config, "inputTimeoutMs"));
        Assert.Equal(1, RequiredInt(config, "allowedMistakes"));

        Assert.Contains("аккуратно погашенную печать", action.SuccessText);
        Assert.NotEqual("Одна руна трескается и оставляет след в стекле, но Дарен удерживает порядок знаков, пока дверь открыта.", text);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren rune_memory partial should be a substantial costly rune aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren rune_memory partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren rune_memory partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("runed glass door and futlar ward pattern", [["рун"], ["стекл", "дверц"], ["футляр"], ["узор", "последователь", "поряд", "знак"], ["защит", "вард", "печать"]]),
            ("Daren imperfect but controlled memory and body work", [["Дарен"], ["памят", "запом", "вспомн", "помн"], ["удерж", "сдерж", "застав", "не дрог"], ["ошиб", "сбил", "дрог", "не точн"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох", "горл"]]),
            ("door opens and futlar access continues", [["дверц", "проход", "откры"], ["футляр"], ["посох"], ["внутр", "дальше", "следующ", "двин"]]),
            ("visible trace cost and later evidence", [["трещ", "трес", "царап", "шрам", "след", "улик"], ["стекл"], ["задерж", "цена", "боль", "горл", "сомнен", "запом"], ["дом", "Ренар", "страж", "погон"]]),
            ("dust stone metal cold and listening house", [["син", "голуб", "холод"], ["пыль"], ["камн", "стен"], ["металл", "бронз", "желез"], ["слуш", "прислуш", "слыш"]]),
            ("Renara continuity after the partial opening", [["Ренар"], ["голос"], ["дальше", "следующ", "впер", "ждал", "готов"], ["вард", "печать", "дом"]])
        })
        {
            AssertContainsEveryTermGroup($"rune_memory partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 3),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -3),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("rune_memory partial aftermath", text);
    }

    [Fact]
    public void DarenRuneMemoryFail_ReadsAsDangerousRuneAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "rune_memory");
        var (chapter, action) = RequiredChapterAction(route.Offer, "rune_memory");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Руны на дверце", beat.Title);
        Assert.Equal("rune_memory", chapter.ChapterId);
        Assert.Equal("rune_memory_action", action.ActionId);
        Assert.Equal("Повторить узор защитных рун", action.Label);
        Assert.Equal("PatternMemory", action.Check.Type);
        Assert.Equal(Characteristics.Perception, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("ward_steward_parley", action.Routing.Success.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Partial.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["q", "w", "e", "space"], RequiredStringArray(config, "alphabet"));
        Assert.Equal(4, RequiredInt(config, "sequenceLength"));
        Assert.Equal(2400, RequiredInt(config, "revealMs"));
        Assert.Equal(6500, RequiredInt(config, "inputTimeoutMs"));
        Assert.Equal(1, RequiredInt(config, "allowedMistakes"));

        Assert.Contains("аккуратно погашенную печать", action.SuccessText);
        Assert.Contains("синяя трещина в стекле", action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(
            !string.Equals(
                "Руны вспыхивают тревожным светом, и Дарен понимает, что дом уже запомнил его прикосновение.",
                text,
                StringComparison.Ordinal),
            "Daren rune_memory fail should not keep the old one-sentence result notification.");
        Assert.True(text.Length >= 900,
            "Daren rune_memory fail should be a substantial dangerous rune aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren rune_memory fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren rune_memory fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("runed glass door and hostile ward flare", [["рун"], ["стекл", "дверц"], ["футляр"], ["син", "голуб"], ["вспых", "свет"], ["защит", "вард", "печать", "узор"]]),
            ("Daren failed memory and body reaction", [["Дарен"], ["памят", "запом", "вспомн", "помн"], ["ошиб", "сбил", "лом", "провал", "невер"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох", "горл"]]),
            ("house remembers Daren through mark evidence or heat", [["дом"], ["запом", "помнит", "памят"], ["касани", "тепло", "имя", "отпечат"], ["след", "улик", "метк", "знак"]]),
            ("alarm witness or pursuit pressure escalates", [["тревог", "сигнал", "крик"], ["страж", "свидетел", "Лукьян", "фонар"], ["погон", "Орвальд", "охот"], ["Ренар"]]),
            ("dust stone metal silence and listening house", [["пыль"], ["камн", "стен"], ["металл", "бронз", "желез"], ["тиш", "молч"], ["слуш", "прислуш", "слыш"]]),
            ("Renara continuity after the fail opening", [["Ренар"], ["голос"], ["дальше", "следующ", "впер", "ждал", "готов"], ["вард", "печать", "дом"]])
        })
        {
            AssertContainsEveryTermGroup($"rune_memory fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 3),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -3),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("rune_memory fail aftermath", text);
    }

    [Fact]
    public void DarenRuneMemorySuccess_ReadsAsCleanRuneAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "rune_memory");
        var (chapter, action) = RequiredChapterAction(route.Offer, "rune_memory");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Руны на дверце", beat.Title);
        Assert.Equal("rune_memory", chapter.ChapterId);
        Assert.Equal("rune_memory_action", action.ActionId);
        Assert.Equal("Повторить узор защитных рун", action.Label);
        Assert.Equal("PatternMemory", action.Check.Type);
        Assert.Equal(Characteristics.Perception, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("ward_steward_parley", action.Routing.Success.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Partial.NextChapterId);
        Assert.Equal("ward_steward_parley", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["q", "w", "e", "space"], RequiredStringArray(config, "alphabet"));
        Assert.Equal(4, RequiredInt(config, "sequenceLength"));
        Assert.Equal(2400, RequiredInt(config, "revealMs"));
        Assert.Equal(6500, RequiredInt(config, "inputTimeoutMs"));
        Assert.Equal(1, RequiredInt(config, "allowedMistakes"));

        Assert.NotEqual("Одна руна трескается и оставляет след в стекле, но Дарен удерживает порядок знаков, пока дверь открыта.", action.PartialText);
        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren rune_memory success should be a substantial clean rune aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren rune_memory success should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren rune_memory success should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("runed glass door and futlar ward pattern", [["рун"], ["стекл", "дверц"], ["футляр"], ["узор", "последователь", "знак"], ["защит", "вард", "печать"]]),
            ("Daren precise memory and body control", [["Дарен"], ["памят", "запом", "вспомн", "помн"], ["точн", "без ошиб", "верн", "чист"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох", "горл"]]),
            ("obedient extinguishing runes and cold light", [["гас", "погас", "угас", "потух"], ["свет"], ["син", "голуб", "холод"], ["руна", "знак"], ["подчин", "слуш", "послуш"]]),
            ("quiet house and reduced alarm evidence risk", [["дом"], ["молч", "тиш", "без крик", "не подня"], ["тревог", "сигнал", "погон"], ["след", "улик", "отпечат"], ["меньше", "стер", "снял", "сглад"]]),
            ("dust stone metal sensory aftermath", [["пыль"], ["камн", "стен"], ["металл", "бронз", "желез"], ["холод"], ["слуш", "прислуш", "слыш"]]),
            ("quiet access toward Renara voice", [["дверц", "проход", "откры"], ["футляр"], ["дальше", "следующ", "впер"], ["Ренар"], ["голос"]])
        })
        {
            AssertContainsEveryTermGroup($"rune_memory success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 3),
            ("pursuit_control", 0),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 0),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -3),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("rune_memory success aftermath", text);
    }

    [Fact]
    public void DarenWardStewardParley_ReadsAsRenaraWardDialoguePageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "ward_steward_parley");
        var (chapter, action) = RequiredChapterAction(route.Offer, "ward_steward_parley");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Голос Ренары", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("ward_steward_parley_action", action.ActionId);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Wisdom, action.Check.PrimaryCharacteristic);
        Assert.Equal(4, action.Check.BaseDifficulty);
        Assert.Equal("Ответить Ренаре Вардовой", action.Label);
        Assert.Equal("physical_pressure", action.Routing.Success.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Partial.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("false_seal", RequiredString(config, "correctChoiceId"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["false_seal", "promise_return", "mock_house"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(["Назвать ложную печать", "Пообещать возврат", "Спорить с домом"], choices.Select(choice => RequiredString(choice, "label")));

        Assert.True(text.Length >= 1500,
            "Daren ward_steward_parley narrative should be a substantial Renara ward-dialogue page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren ward_steward_parley narrative should unfold across a magical-security conversation, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren ward_steward_parley narrative should keep Daren as the active point-of-view protagonist.");
        Assert.True(CountOccurrences(text, "Ренар") >= 3,
            "Daren ward_steward_parley narrative should personify Renara Wardova beyond a single mention.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("extinguished rune and glass carry-forward", [["погас", "гас"], ["рун"], ["стекл", "футляр"], ["син", "холод"]]),
            ("Renara as ward authority", [["Ренар"], ["Вардов", "вард"], ["управ", "хозяйк", "смотрител", "сторож"], ["печать", "защит"]]),
            ("voice reflection and embodied pressure", [["голос"], ["отраж", "лиц", "силуэт", "стекл"], ["дав", "давлен", "холод", "шеп"], ["дом"]]),
            ("Daren observation body and intent", [["Дарен"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох"], ["смотр", "вид", "замет"], ["реш", "выбр", "подобр", "искал"]]),
            ("question answer dialogue pressure", [["спрос", "вопрос", "зачем"], ["ответ", "отвеч", "сказал", "произн"], ["молч", "усып", "унять"], ["голос", "имя", "чуж"]]),
            ("false seal strategy and house-silencing stakes", [["ложн", "стар", "трещ", "провер"], ["печать"], ["сигнал", "тревог"], ["дом"], ["посох"]]),
            ("PrecisionChoice lead-in toward physical pressure", [["назвать", "пообещ", "спор", "выб"], ["Ренар"], ["ответ"], ["футляр", "ниша", "решет", "посох"]])
        })
        {
            AssertContainsEveryTermGroup($"ward_steward_parley full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 3),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 1),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -3),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("ward_steward_parley full-page narrative", text);
    }

    [Fact]
    public void DarenWardStewardParleyPartial_ReadsAsMixedSocialAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "ward_steward_parley");
        var (chapter, action) = RequiredChapterAction(route.Offer, "ward_steward_parley");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Голос Ренары", beat.Title);
        Assert.Equal("ward_steward_parley", chapter.ChapterId);
        Assert.Equal("ward_steward_parley_action", action.ActionId);
        Assert.Equal("Ответить Ренаре Вардовой", action.Label);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Wisdom, action.Check.PrimaryCharacteristic);
        Assert.Equal(4, action.Check.BaseDifficulty);
        Assert.Equal("physical_pressure", action.Routing.Success.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Partial.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("false_seal", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["false_seal", "promise_return", "mock_house"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(["Назвать ложную печать", "Пообещать возврат", "Спорить с домом"], choices.Select(choice => RequiredString(choice, "label")));

        Assert.NotEqual(action.SuccessText, text);
        Assert.NotEqual(action.FailText, text);

        Assert.True(text.Length >= 900,
            "Daren ward_steward_parley partial should be a substantial mixed social aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren ward_steward_parley partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren ward_steward_parley partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("Renara ward voice pressure", [["Ренар"], ["Вардов", "вард"], ["голос"], ["холод", "строг", "тих", "шеп"]]),
            ("Daren promise-return answer", [["Дарен"], ["пообещ", "обещ", "клятв", "верн", "возврат"], ["печать", "вард"], ["ответ", "сказал", "произн", "слова"], ["горл", "дых", "ладон", "пальц", "рук"]]),
            ("alarm delayed but not erased", [["тревог", "сигнал", "крик"], ["задерж", "отлож", "смягч", "подожд", "медл"], ["дом"], ["молч", "тиш", "не подня", "не позвала", "не зов"]]),
            ("voice trace and later consequence remain", [["голос", "имя"], ["след", "улик", "отпечат", "метк", "запом"], ["рун", "стекл", "печать"], ["потом", "утр", "позже", "верн", "послед", "долг"]]),
            ("cold glass runes and listening house", [["стекл", "футляр"], ["рун"], ["син", "холод", "свет"], ["печать", "вард"], ["дом", "камн", "стен"]]),
            ("continuity toward heavy grate", [["решет", "решёт"], ["тяжел", "тяжёл", "желез"], ["футляр", "ниша", "посох"], ["дальше", "вперёд", "следующ", "пош"]])
        })
        {
            AssertContainsEveryTermGroup($"ward_steward_parley partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 3),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 1),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -3),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("ward_steward_parley partial aftermath", text);
    }

    [Fact]
    public void DarenWardStewardParleyFail_ReadsAsDangerousSocialAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "ward_steward_parley");
        var (chapter, action) = RequiredChapterAction(route.Offer, "ward_steward_parley");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Голос Ренары", beat.Title);
        Assert.Equal("ward_steward_parley", chapter.ChapterId);
        Assert.Equal("ward_steward_parley_action", action.ActionId);
        Assert.Equal("Ответить Ренаре Вардовой", action.Label);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Wisdom, action.Check.PrimaryCharacteristic);
        Assert.Equal(4, action.Check.BaseDifficulty);
        Assert.Equal("physical_pressure", action.Routing.Success.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Partial.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("false_seal", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["false_seal", "promise_return", "mock_house"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(["Назвать ложную печать", "Пообещать возврат", "Спорить с домом"], choices.Select(choice => RequiredString(choice, "label")));

        Assert.NotEqual(action.SuccessText, text);
        Assert.NotEqual(action.PartialText, text);

        Assert.True(text.Length >= 900,
            "Daren ward_steward_parley fail should be a substantial dangerous social aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren ward_steward_parley fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren ward_steward_parley fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("Renara ward voice becomes hostile authority", [["Ренар"], ["Вардов", "вард"], ["голос"], ["свет", "холод", "резк", "стекл"]]),
            ("Daren failed challenge wakes the house", [["Дарен"], ["спор", "вызов", "ошиб", "ответ", "слово"], ["дом", "Ренар"], ["горл", "дых", "ладон", "пальц", "рук"]]),
            ("alarm turns into concrete pursuit pressure", [["тревог", "сигнал", "крик"], ["буд", "прос", "подня", "ожил"], ["погон", "страж", "лов", "преслед"], ["шум", "звон", "скреж", "желез"]]),
            ("identity evidence and witness pressure remain", [["нарушител", "чуж", "вор"], ["след", "улик", "отпечат", "свидетел", "имя"], ["рун", "печать", "стекл"], ["запом", "узна", "назов", "найд"]]),
            ("listening house turns against Daren", [["дом"], ["слуш", "прислуш", "слыш"], ["стен", "камн", "галере", "пол"], ["против", "выдал", "выдавал", "предал"]]),
            ("continuity toward heavy grate", [["решет", "решёт"], ["тяжел", "тяжёл", "желез"], ["футляр", "ниша", "посох"], ["дальше", "вперёд", "следующ", "пош"]])
        })
        {
            AssertContainsEveryTermGroup($"ward_steward_parley fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 3),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 1),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -3),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("ward_steward_parley fail aftermath", text);
    }

    [Fact]
    public void DarenWardStewardParleySuccess_ReadsAsCleanSocialAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "ward_steward_parley");
        var (chapter, action) = RequiredChapterAction(route.Offer, "ward_steward_parley");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Голос Ренары", beat.Title);
        Assert.Equal("ward_steward_parley", chapter.ChapterId);
        Assert.Equal("ward_steward_parley_action", action.ActionId);
        Assert.Equal("Ответить Ренаре Вардовой", action.Label);
        Assert.Equal("PrecisionChoice", action.Check.Type);
        Assert.Equal(Characteristics.Wisdom, action.Check.PrimaryCharacteristic);
        Assert.Equal(4, action.Check.BaseDifficulty);
        Assert.Equal("physical_pressure", action.Routing.Success.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Partial.NextChapterId);
        Assert.Equal("physical_pressure", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal("false_seal", RequiredString(config, "correctChoiceId"));
        Assert.Equal(7000, RequiredInt(config, "timeoutMs"));
        Assert.Equal("fail", RequiredString(config, "timeoutGrade"));
        var choices = RequiredObjectArray(config, "choices");
        Assert.Equal(["false_seal", "promise_return", "mock_house"], choices.Select(choice => RequiredString(choice, "id")));
        Assert.Equal(["success", "partial", "fail"], choices.Select(choice => RequiredString(choice, "grade")));
        Assert.Equal(["Назвать ложную печать", "Пообещать возврат", "Спорить с домом"], choices.Select(choice => RequiredString(choice, "label")));

        Assert.NotEqual(action.SuccessText, action.PartialText);
        Assert.NotEqual(action.SuccessText, action.FailText);
        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren ward_steward_parley success should be a substantial clean social aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren ward_steward_parley success should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren ward_steward_parley success should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("Renara ward voice scrutiny", [["Ренар"], ["Вардов", "вард"], ["голос"], ["не подня", "не повыс", "не зов", "не крик", "не тревож"]]),
            ("Daren controlled false-seal answer", [["Дарен"], ["ложн", "трещ", "стар"], ["печать"], ["ответ", "объясн", "назвал"], ["дых", "горл", "ладон", "пальц", "рук"]]),
            ("accepted explanation and reduced social pressure", [["приня", "повер", "соглас", "не спор"], ["объясн", "верси", "ответ"], ["Ренар"], ["молч", "тиш", "не стала"]]),
            ("house quiets extra seal and risk", [["дом"], ["печать", "сигнал"], ["гас", "затих", "молч", "умолк"], ["тревог", "погон", "след", "улик"]]),
            ("cold glass runes and listening stone", [["стекл", "витрин", "футляр"], ["рун"], ["камн", "пыль", "холод", "свет"], ["слуш", "прислуш"]]),
            ("continuity toward heavy grate", [["решет", "решёт"], ["тяжел", "тяжёл", "желез"], ["футляр", "ниша", "посох", "коридор"], ["дальше", "следующ", "вперёд"]])
        })
        {
            AssertContainsEveryTermGroup($"ward_steward_parley success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 3),
            ("evidence", -2),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 1),
            ("pursuit_control", 1),
            ("evidence", -1),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -2),
            ("pursuit_control", -3),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("ward_steward_parley success aftermath", text);
    }

    [Fact]
    public void DarenPhysicalPressure_ReadsAsHeavyGratePageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "physical_pressure");
        var (chapter, action) = RequiredChapterAction(route.Offer, "physical_pressure");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Тяжёлая решётка", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("physical_pressure_action", action.ActionId);
        Assert.Equal("MashInput", action.Check.Type);
        Assert.Equal(Characteristics.Strength, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("Удержать тяжёлую решётку", action.Label);
        Assert.Equal("timed_rhythm", action.Routing.Success.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Partial.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["space"], RequiredStringArray(config, "keys"));
        Assert.Equal(3200, RequiredInt(config, "durationMs"));
        Assert.Equal(13, RequiredInt(config, "targetPresses"));
        Assert.Equal(0.55, RequiredDouble(config, "partialThreshold"), precision: 2);

        Assert.True(text.Length >= 1500,
            "Daren physical_pressure narrative should be a substantial heavy-grate physical-pressure page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren physical_pressure narrative should unfold across a tactile action scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren physical_pressure narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("heavy grate iron weight", [["решет", "решёт"], ["желез", "чугун", "прут"], ["тяж", "вес", "дав", "навис"]]),
            ("Daren body control", [["Дарен"], ["плеч", "лопат", "ключиц"], ["ладон", "пальц", "рук"], ["дых", "вдох", "выдох"], ["удерж", "упер", "толк", "подня", "держ"]]),
            ("staff case Renara rune continuity", [["Ренар", "вард", "печать", "голос"], ["рун", "стекл"], ["футляр"], ["посох"], ["ниш"]]),
            ("silence alarm wing stakes", [["тиш", "молч"], ["грох", "скреж", "звон", "шум"], ["тревог", "сигнал", "крик"], ["страж", "караул", "Лукьян"], ["крыл", "дом", "коридор"]]),
            ("mechanism last-inch lead-in", [["механизм", "зубец", "противовес", "пружин"], ["дюйм", "палец", "волос", "щель"], ["удерж", "держ", "подним", "выдерж"], ["освобод", "выйд", "выход", "выдвин"], ["футляр", "посох"]]),
            ("MashInput physical action lead-in", [["рывок", "удар", "нажим", "сила"], ["решет", "решёт"], ["плеч", "рук"], ["последн", "край", "конец"], ["добыч", "посох"]])
        })
        {
            AssertContainsEveryTermGroup($"physical_pressure full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 4),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -4),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("physical_pressure full-page narrative", text);
    }

    [Fact]
    public void DarenPhysicalPressureSuccess_ReadsAsCleanAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "physical_pressure");
        var (chapter, action) = RequiredChapterAction(route.Offer, "physical_pressure");
        var text = action.SuccessText?.Trim() ?? "";

        Assert.Equal("Тяжёлая решётка", beat.Title);
        Assert.Equal("physical_pressure", chapter.ChapterId);
        Assert.Equal("Удержать тяжёлую решётку", action.Label);
        Assert.Equal("physical_pressure_action", action.ActionId);
        Assert.Equal("MashInput", action.Check.Type);
        Assert.Equal(Characteristics.Strength, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("timed_rhythm", action.Routing.Success.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Partial.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["space"], RequiredStringArray(config, "keys"));
        Assert.Equal(3200, RequiredInt(config, "durationMs"));
        Assert.Equal(13, RequiredInt(config, "targetPresses"));
        Assert.Equal(0.55, RequiredDouble(config, "partialThreshold"), precision: 2);

        Assert.NotEqual(action.SuccessText, action.FailText);

        Assert.True(text.Length >= 900,
            "Daren physical_pressure success should be a substantial clean-outcome aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 8,
            "Daren physical_pressure success should unfold as aftermath prose with several scene sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 4,
            "Daren physical_pressure success should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("heavy-grate resolution", [["решет", "решёт"], ["последн", "край", "ход", "зубец"], ["удерж", "держ", "выдерж"], ["механизм", "противовес", "пружин"]]),
            ("Daren body breath and control", [["Дарен"], ["плеч", "ребр", "ладон", "пальц", "кров"], ["дых", "вдох", "выдох"], ["боль", "бол"], ["сдерж", "застав", "не позвол", "удерж"]]),
            ("staff case niche extraction", [["футляр"], ["посох"], ["ниш"], ["выш", "вывел", "освобод"], ["камн", "стекл", "бархат"]]),
            ("silence no-crash reduced-risk stakes", [["тиш", "молч"], ["без грох", "без звона", "не удар", "не звяк"], ["тревог", "сигнал"], ["страж", "Лукьян", "дом"], ["след", "улик", "погон"]]),
            ("next-corridor continuity", [["коридор"], ["кристалл", "пульс", "красн", "ал"], ["следующ", "дальше", "за двер"], ["футляр", "посох"], ["плеч", "ладон", "кров", "боль"]])
        })
        {
            AssertContainsEveryTermGroup($"physical_pressure success {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 4),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -4),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("physical_pressure success aftermath", text);
    }

    [Fact]
    public void DarenPhysicalPressurePartial_ReadsAsCostlyAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "physical_pressure");
        var (chapter, action) = RequiredChapterAction(route.Offer, "physical_pressure");
        var text = action.PartialText?.Trim() ?? "";

        Assert.Equal("Тяжёлая решётка", beat.Title);
        Assert.Equal("physical_pressure", chapter.ChapterId);
        Assert.Equal("Удержать тяжёлую решётку", action.Label);
        Assert.Equal("physical_pressure_action", action.ActionId);
        Assert.Equal("MashInput", action.Check.Type);
        Assert.Equal(Characteristics.Strength, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("timed_rhythm", action.Routing.Success.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Partial.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["space"], RequiredStringArray(config, "keys"));
        Assert.Equal(3200, RequiredInt(config, "durationMs"));
        Assert.Equal(13, RequiredInt(config, "targetPresses"));
        Assert.Equal(0.55, RequiredDouble(config, "partialThreshold"), precision: 2);

        Assert.NotEqual(action.PartialText, action.FailText);

        Assert.True(text.Length >= 850,
            "Daren physical_pressure partial should be a substantial costly aftermath insert, not a one-sentence mixed-result notification.");
        Assert.True(CountSentences(text) >= 7,
            "Daren physical_pressure partial should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 3,
            "Daren physical_pressure partial should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("mixed heavy-grate resolution", [["решет", "решёт"], ["посох"], ["свобод", "освобод", "выш", "вывел", "вынул"], ["желез", "прут", "чугун"]]),
            ("Daren body breath and control", [["Дарен"], ["плеч", "ребр", "лопат", "кость"], ["дых", "вдох", "выдох", "хрип"], ["боль", "бол", "кров", "рана"], ["сдерж", "застав", "удерж", "не позвол"]]),
            ("staff case niche extraction", [["футляр"], ["посох"], ["ниш"], ["камн", "паз", "зуб"], ["вывел", "выш", "вынул", "освобод"]]),
            ("cost trace doubt pursuit stakes", [["след", "ули", "кров", "пятн", "царап"], ["задерж", "позд", "лишн", "медл"], ["сомнен", "погон", "страж", "Лукьян", "дом"], ["шум", "скреж", "звук", "грох", "звон"]]),
            ("sensory listening-house pressure", [["желез", "прут", "решет", "решёт"], ["камн", "стекл", "масл"], ["скреж", "звон", "грох", "звук"], ["тиш", "молч", "слуш"]]),
            ("next-corridor continuity", [["коридор"], ["кристалл", "пульс", "красн", "ал"], ["дальше", "следующ", "за двер"], ["плеч", "кров", "боль", "рана"], ["футляр", "посох"]])
        })
        {
            AssertContainsEveryTermGroup($"physical_pressure partial {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 4),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -4),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("physical_pressure partial aftermath", text);
    }

    [Fact]
    public void DarenPhysicalPressureFail_ReadsAsDangerousAftermathWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "physical_pressure");
        var (chapter, action) = RequiredChapterAction(route.Offer, "physical_pressure");
        var text = action.FailText?.Trim() ?? "";

        Assert.Equal("Тяжёлая решётка", beat.Title);
        Assert.Equal("physical_pressure", chapter.ChapterId);
        Assert.Equal("Удержать тяжёлую решётку", action.Label);
        Assert.Equal("physical_pressure_action", action.ActionId);
        Assert.Equal("MashInput", action.Check.Type);
        Assert.Equal(Characteristics.Strength, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("timed_rhythm", action.Routing.Success.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Partial.NextChapterId);
        Assert.Equal("timed_rhythm", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(["space"], RequiredStringArray(config, "keys"));
        Assert.Equal(3200, RequiredInt(config, "durationMs"));
        Assert.Equal(13, RequiredInt(config, "targetPresses"));
        Assert.Equal(0.55, RequiredDouble(config, "partialThreshold"), precision: 2);

        Assert.NotEqual(action.SuccessText, text);
        Assert.NotEqual(action.PartialText, text);

        Assert.True(text.Length >= 850,
            "Daren physical_pressure fail should be a substantial dangerous aftermath insert, not a one-sentence result notification.");
        Assert.True(CountSentences(text) >= 7,
            "Daren physical_pressure fail should unfold across several aftermath sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 3,
            "Daren physical_pressure fail should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("failed heavy-grate crash", [["решет", "решёт"], ["пад", "рух", "сорв", "обруш"], ["камн", "пол"], ["грох", "удар", "звон", "скреж"], ["тяж", "желез", "прут", "чугун"]]),
            ("Daren compromised body breath and control", [["Дарен"], ["плеч", "ребр", "ладон", "пальц", "спин", "колен"], ["дых", "вдох", "выдох", "хрип"], ["боль", "бол", "кров", "жг"], ["сорв", "опозд", "не успел", "потер", "соскольз", "дрог"]]),
            ("staff case niche salvage under pressure", [["футляр"], ["посох"], ["ниш"], ["схват", "выхват", "прижал", "вытащ", "забрал", "вывел"], ["оскол", "стекл", "бархат", "камн"]]),
            ("noise evidence witness pursuit stakes", [["тревог", "сигнал", "крик", "колокол"], ["шум", "грох", "звон", "эхо", "скреж"], ["улик", "след", "кров", "оскол"], ["свидетел", "Лукьян", "страж", "караул", "Ренар"], ["погон", "преслед", "Орвальд", "догон"]]),
            ("sensory ruined silence", [["масл", "пыл", "камн", "желез"], ["оскол", "заноз", "щеп", "крош"], ["кров", "дых", "хрип"], ["дом", "крыл", "слуш", "просып"]]),
            ("next-corridor continuity", [["коридор"], ["кристалл", "пульс", "красн", "ал"], ["дальше", "следующ", "за двер"], ["посох", "футляр"], ["тревог", "грох", "шум", "погон", "сигнал"]])
        })
        {
            AssertContainsEveryTermGroup($"physical_pressure fail {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 0),
            ("loot", 4),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 0),
            ("loot", 2),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -3),
            ("loot", -4),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("physical_pressure fail aftermath", text);
    }

    [Fact]
    public void DarenTimedRhythm_ReadsAsAlarmPulsePageWithoutMechanicDrift()
    {
        var route = QteSceneService.GetDarenShowcaseRoute();
        var beat = Assert.Single(route.Beats, item => item.BeatId == "timed_rhythm");
        var (chapter, action) = RequiredChapterAction(route.Offer, "timed_rhythm");
        var text = chapter.Narrative?.Trim() ?? "";

        Assert.Equal("Пульс сигнализации", beat.Title);
        Assert.Equal(beat.PlayerText, chapter.Narrative);
        Assert.Equal("timed_rhythm_action", action.ActionId);
        Assert.Equal("RhythmPulse", action.Check.Type);
        Assert.Equal(Characteristics.Speed, action.Check.PrimaryCharacteristic);
        Assert.Equal(3, action.Check.BaseDifficulty);
        Assert.Equal("Двигаться между ударами кристалла", action.Label);
        Assert.Equal("route_decision", action.Routing.Success.NextChapterId);
        Assert.Equal("route_decision", action.Routing.Partial.NextChapterId);
        Assert.Equal("route_decision", action.Routing.Fail.NextChapterId);

        var config = RequiredConfig(action);
        Assert.Equal(5, RequiredInt(config, "pulseCount"));
        Assert.Equal(640, RequiredInt(config, "beatIntervalMs"));
        Assert.Equal(125, RequiredInt(config, "hitWindowMs"));
        Assert.Equal(1, RequiredInt(config, "allowedMisses"));
        Assert.Equal("swing", RequiredString(config, "patternVariation"));

        Assert.True(text.Length >= 1500,
            "Daren timed_rhythm narrative should be a substantial alarm-pulse literary page, not a compact synopsis.");
        Assert.True(CountSentences(text) >= 12,
            "Daren timed_rhythm narrative should unfold across a real rhythm-stealth scene, not two briefing sentences.");
        Assert.True(CountOccurrences(text, "Дарен") >= 5,
            "Daren timed_rhythm narrative should keep Daren as the active point-of-view protagonist.");

        foreach (var (context, termGroups) in new (string Context, string[][] TermGroups)[]
        {
            ("signal crystal and red corridor", [["кристалл"], ["красн", "ал"], ["коридор"], ["пол", "стен"]]),
            ("pulse pauses rhythm and intervals", [["пульс", "удар", "вспыш"], ["пауз", "промежут", "интервал"], ["ритм", "счёт"], ["между"]]),
            ("Daren body breath step and shadow control", [["Дарен"], ["дых", "вдох", "выдох"], ["сапог", "ступн", "шаг"], ["тень", "силуэт"], ["плеч", "ладон", "ребр", "колен"]]),
            ("staff case and heavy-grate continuity", [["посох"], ["футляр", "добыч"], ["решет", "решёт", "желез"], ["плеч", "кров", "боль", "ладон"]]),
            ("silence noise alarm guards and trace stakes", [["тиш", "молч"], ["шум", "звон", "скреж", "крик"], ["тревог", "сигнал"], ["страж", "караул", "Лукьян", "Орвальд"], ["след", "улик", "выда", "пойм"]]),
            ("rhythm action lead-in", [["двиг", "скольз", "шаг", "пересеч"], ["между"], ["удар", "пульс", "вспыш"], ["кристалл"], ["пойм", "схват", "накры"]])
        })
        {
            AssertContainsEveryTermGroup($"timed_rhythm full-page {context}", text, termGroups);
        }

        AssertScoreDeltas(action, "success",
            ("normalized_score", 5),
            ("stealth", 4),
            ("loot", 0),
            ("pursuit_control", 2),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "partial",
            ("normalized_score", 0),
            ("stealth", 2),
            ("loot", 0),
            ("pursuit_control", 1),
            ("evidence", 0),
            ("hideout_safety", 0));
        AssertScoreDeltas(action, "fail",
            ("normalized_score", -8),
            ("stealth", -4),
            ("loot", -2),
            ("pursuit_control", -2),
            ("evidence", 4),
            ("hideout_safety", -2));
        AssertNoPlayerFacingTechnicalTerms("timed_rhythm full-page narrative", text);
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
        Assert.Contains(960, RequiredIntArray(spine, "sourceIssues"));
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
    public void DarenEndingResolver_ProvidesDistinctEpiloguesForEveryOutcome()
    {
        var endings = RequiredDarenEndingOutcomes();
        var epilogues = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var (outcomeId, ending) in endings)
        {
            var epilogue = RequiredStringProperty(ending, "Epilogue");

            Assert.InRange(epilogue.Length, 420, 1600);
            Assert.True(CountSentences(epilogue) >= 5,
                $"Daren ending '{outcomeId}' needs a substantial epilogue page, not a short outcome summary.");
            Assert.True(CountOccurrences(epilogue, "Дарен") >= 2,
                $"Daren ending '{outcomeId}' should keep Daren centered as the protagonist.");
            AssertNoPlayerFacingTechnicalTerms($"{outcomeId} epilogue", epilogue);
            Assert.DoesNotContain("+", epilogue, StringComparison.Ordinal);
            epilogues.Add(outcomeId, epilogue);
        }

        Assert.Equal(epilogues.Count, epilogues.Values.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void DarenEndingResolver_EpiloguesAndRewardExplanationsCarryTierConsequences()
    {
        foreach (var (outcomeId, ending) in RequiredDarenEndingOutcomes())
        {
            var epilogue = RequiredStringProperty(ending, "Epilogue");
            var rewardExplanation = RequiredStringProperty(ending, "RewardExplanation");

            AssertNoPlayerFacingTechnicalTerms($"{outcomeId} reward explanation", rewardExplanation);
            AssertContainsAny($"{outcomeId} epilogue consequence", epilogue, outcomeId switch
            {
                "no_reward_failure" => ["опас", "тревог", "погон", "убежищ", "след"],
                "shadow_on_the_run" => ["погон", "ули", "свидетел", "шум", "след"],
                "broken_trail" => ["след", "свидетел", "Орвальд", "погон", "ули"],
                "clean_heist" => ["чист", "ули", "убежищ", "погон", "тайник"],
                "perfect_shadow" => ["без след", "чист", "молчит", "легенд", "Орвальд"],
                _ => throw new InvalidOperationException(outcomeId)
            });

            if (ending.GrantsReward)
            {
                Assert.Contains("Дарен", rewardExplanation, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("постоян", rewardExplanation, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("будущ", rewardExplanation, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("Черниль", rewardExplanation, StringComparison.OrdinalIgnoreCase);
                AssertContainsInkFeatherAmountSignal(outcomeId, ending.InkFeatherBonus, rewardExplanation);
            }
            else
            {
                Assert.Contains("не запис", rewardExplanation, StringComparison.OrdinalIgnoreCase);
                Assert.Contains("постоян", rewardExplanation, StringComparison.OrdinalIgnoreCase);
            }
        }
    }

    [Fact]
    public void DarenEndingResolver_RewardExplanationsUseInWorldLoreInsteadOfReceipts()
    {
        foreach (var (outcomeId, ending) in RequiredDarenEndingOutcomes())
        {
            var rewardExplanation = RequiredStringProperty(ending, "RewardExplanation");

            Assert.InRange(rewardExplanation.Length, 220, 1200);
            Assert.True(CountSentences(rewardExplanation) >= 3,
                $"Daren ending '{outcomeId}' reward explanation should read as in-world prose, not a receipt.");
            Assert.Contains("Книга", rewardExplanation, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("Дарен", rewardExplanation, StringComparison.OrdinalIgnoreCase);
            AssertNoPlayerFacingTechnicalTerms($"{outcomeId} reward explanation", rewardExplanation);
            foreach (var forbidden in ForbiddenRewardReceiptTerms)
            {
                Assert.DoesNotContain(
                    forbidden,
                    rewardExplanation,
                    StringComparison.OrdinalIgnoreCase);
            }

            if (ending.GrantsReward)
            {
                Assert.Contains("Черниль", rewardExplanation, StringComparison.OrdinalIgnoreCase);
                AssertContainsInkFeatherAmountSignal(outcomeId, ending.InkFeatherBonus, rewardExplanation);
            }
            else
            {
                AssertContainsAny($"{outcomeId} book refusal", rewardExplanation, ["отказыва", "не впис", "не бер", "молч"]);
            }
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
        Assert.Contains("постоян", first.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("будущ", first.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Черниль", first.Message, StringComparison.OrdinalIgnoreCase);

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
        Assert.NotNull(attempt.Ending);
        var epilogue = RequiredStringProperty(attempt.Ending!, "Epilogue");
        var rewardExplanation = RequiredStringProperty(attempt.Ending!, "RewardExplanation");
        Assert.Contains(epilogue, resolution.Completion.Summary, StringComparison.Ordinal);
        Assert.Contains(rewardExplanation, resolution.Completion.Summary, StringComparison.Ordinal);
        Assert.Contains(epilogue, resolution.Completion.Response.Response, StringComparison.Ordinal);
        Assert.Contains(rewardExplanation, attempt.Feedback, StringComparison.Ordinal);
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
    public async Task DarenBrowserState_ReplayAfterPerfectKeepsBestFutureRewardSeparateFromLowerEnding()
    {
        await _profile.RecordCompletionAsync(
            DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90),
            new DateTime(2026, 6, 11, 1, 0, 0, DateTimeKind.Utc));

        var state = await _web.StartDarenShowcaseAsync();
        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var activeAction = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await _web.ResolveDarenShowcaseActionAsync(new DarenShowcaseActionRequest(activeAction.ActionId, "partial"));
        }

        Assert.Equal("Completed", state.State);
        Assert.NotNull(state.BestReward);
        Assert.NotNull(state.Ending);
        Assert.Equal("perfect_shadow", state.BestReward!.TierId);
        Assert.Equal(6, state.BestReward.InkFeatherBonus);
        Assert.Equal("shadow_on_the_run", state.Ending!.TierId);
        Assert.Equal(1, state.Ending.InkFeatherBonus);
        Assert.Contains("луч", state.Ending.RewardMessage, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("1 Черниль", state.Ending.RewardMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task DarenBrowserState_ExposesSharedEndingEpilogueAndRewardExplanation()
    {
        var state = await _web.StartDarenShowcaseAsync();
        while (string.Equals(state.State, "Active", StringComparison.OrdinalIgnoreCase))
        {
            var activeAction = Assert.Single(state.ActiveScene!.CurrentChapter!.Actions);
            state = await _web.ResolveDarenShowcaseActionAsync(new DarenShowcaseActionRequest(activeAction.ActionId, "success"));
        }

        Assert.Equal("Completed", state.State);
        Assert.NotNull(state.Ending);
        Assert.NotNull(state.Completion);
        var epilogue = RequiredStringProperty(state.Ending!, "Epilogue");
        var rewardExplanation = RequiredStringProperty(state.Ending!, "RewardExplanation");

        Assert.Equal("perfect_shadow", state.Ending!.TierId);
        Assert.True(state.Ending.GrantsReward);
        Assert.Contains(epilogue, state.Completion!.Summary, StringComparison.Ordinal);
        Assert.Contains(rewardExplanation, state.Completion.Summary, StringComparison.Ordinal);
        Assert.Contains("Черниль", rewardExplanation, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("будущ", rewardExplanation, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void DarenConsoleCompletion_DoesNotDuplicateEndingTextAlreadyInCompletionResponse()
    {
        var darenSource = ReadRepoFile("BookOfEternityClient", "Services", "QteSceneService.Daren.cs");

        Assert.Contains("completion.Response.Response ?? completion.Summary", darenSource, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add(ending.Epilogue)", darenSource, StringComparison.Ordinal);
        Assert.DoesNotContain("lines.Add(ending.RewardExplanation)", darenSource, StringComparison.Ordinal);
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
        var rewardSource = ReadRepoFile("BookOfEternityClient", "Services", "DarenQteRewardProfileService.cs");
        var browserServiceSource = ReadRepoFile("BookOfEternityClient", "WebUi", "QteWebInteractionService.cs");
        var frontendDarenSource = ReadRepoFile("BookOfEternityClient.WebFrontend", "src", "components", "DarenShowcaseView.tsx");

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
        Assert.Contains("Epilogue", rewardSource, StringComparison.Ordinal);
        Assert.Contains("RewardExplanation", rewardSource, StringComparison.Ordinal);
        Assert.Contains("Epilogue", browserServiceSource, StringComparison.Ordinal);
        Assert.Contains("RewardExplanation", browserServiceSource, StringComparison.Ordinal);
        Assert.Contains("epilogue", frontendDarenSource, StringComparison.Ordinal);
        Assert.Contains("rewardExplanation", frontendDarenSource, StringComparison.Ordinal);
        Assert.DoesNotContain("лучший результат сохранён", frontendDarenSource, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[] { "shadow_on_the_run", "broken_trail", "clean_heist", "perfect_shadow", "no_reward_failure" })
            Assert.DoesNotContain(forbidden, frontendDarenSource, StringComparison.OrdinalIgnoreCase);

        var productionGrantCallSites = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("ApplyBestRewardToNewSoulStateAsync", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(["BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs", "BookOfEternityClient/Services/DarenQteRewardProfileService.cs"], productionGrantCallSites);

        var productionRewardProfilePaths = Directory
            .EnumerateFiles(Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient"), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
            .Where(path => File.ReadAllText(path).Contains("qte_showcase_rewards.json", StringComparison.Ordinal))
            .Select(path => Path.GetRelativePath(TestRepoPaths.RepoRoot, path).Replace('\\', '/'))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        Assert.Equal(["BookOfEternityClient/Services/DarenQteRewardProfileService.cs"], productionRewardProfilePaths);

        foreach (var source in new[] { darenSource, rewardSource, browserServiceSource })
        {
            Assert.DoesNotContain("DarenEndingState", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("CampaignState", source, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pending_daren", source, StringComparison.OrdinalIgnoreCase);
        }
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

    private static IReadOnlyList<(string OutcomeId, DarenEndingResult Ending)> RequiredDarenEndingOutcomes() =>
    [
        ("no_reward_failure", DarenQteRewardProfileService.ResolveEnding(reachedHideout: false, normalizedScore: 100)),
        ("shadow_on_the_run", DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 40)),
        ("broken_trail", DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 55)),
        ("clean_heist", DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 75)),
        ("perfect_shadow", DarenQteRewardProfileService.ResolveEnding(reachedHideout: true, normalizedScore: 90))
    ];

    private static string RequiredStringProperty(object instance, string propertyName)
    {
        var property = instance.GetType().GetProperty(propertyName);
        Assert.True(property != null, $"{instance.GetType().Name} must expose shared string property '{propertyName}'.");
        Assert.True(
            property!.PropertyType == typeof(string),
            $"{instance.GetType().Name}.{propertyName} must be a string property.");

        var value = property.GetValue(instance) as string;
        Assert.False(string.IsNullOrWhiteSpace(value),
            $"{instance.GetType().Name}.{propertyName} must be non-empty.");
        return value.Trim();
    }

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

    private static void AssertDownstreamDarenResultSurfacesPreserved(QteSceneService.QteOffer offer)
    {
        var (_, stealthAction) = RequiredChapterAction(offer, "stealth_crossing");
        Assert.Contains("Дарен перенёс вес с последней опасной доски", stealthAction.SuccessText);
        Assert.Contains("Дарен успел погасить скрип не сразу", stealthAction.PartialText);
        Assert.Contains("Доска под сапогом Дарена не скрипнула", stealthAction.FailText);

        var (_, guardAction) = RequiredChapterAction(offer, "guard_interrogation");
        Assert.Contains("Зелёная нить не звенит на чужом кольце", guardAction.SuccessText);
        Assert.Contains("Проход был выигран, но не очищен", guardAction.PartialText);
        Assert.Contains("Молчание оказалось не укрытием, а признанием", guardAction.FailText);

        var (_, lockAction) = RequiredChapterAction(offer, "lock_pick");
        Assert.Contains("Чистая работа оставляет после себя не победу, а отсутствие истории.", lockAction.SuccessText);
        Assert.Contains("на накладке уже жила улика", lockAction.PartialText);
        Assert.Contains("Верхний штифт сорвался с тонкого края", lockAction.FailText);

        var (_, runeAction) = RequiredChapterAction(offer, "rune_memory");
        Assert.Contains("аккуратно погашенную печать", runeAction.SuccessText);
        Assert.Contains("синяя трещина в стекле", runeAction.PartialText);
        Assert.Contains("дом, который держит в памяти имя Дарена", runeAction.FailText);
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

    private static void AssertScoreDeltas(
        QteSceneService.QteAction action,
        string grade,
        params (string Metric, double Delta)[] expected)
    {
        Assert.NotNull(action.ScoreDeltas);
        Assert.True(action.ScoreDeltas!.TryGetValue(grade, out var actual),
            $"Daren action '{action.ActionId}' needs score deltas for {grade}.");
        Assert.NotNull(actual);

        var actualByMetric = actual!.ToDictionary(delta => delta.Metric, delta => delta.Delta, StringComparer.OrdinalIgnoreCase);
        Assert.Equal(
            expected.Select(item => item.Metric).OrderBy(item => item, StringComparer.OrdinalIgnoreCase),
            actualByMetric.Keys.OrderBy(item => item, StringComparer.OrdinalIgnoreCase));

        foreach (var (metric, delta) in expected)
            Assert.Equal(delta, actualByMetric[metric]);
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

    private static double RequiredDouble(JsonObject root, string propertyName)
    {
        Assert.True(root[propertyName] is JsonValue, $"Expected '{propertyName}' to be a number.");
        var value = (JsonValue)root[propertyName]!;
        Assert.True(value.TryGetValue<double>(out var number), $"Expected '{propertyName}' to be a number.");
        return number;
    }

    private static void AssertChapterNarrativeLooksLikeSceneProse(string chapterId, string? value)
    {
        Assert.False(string.IsNullOrWhiteSpace(value), $"Daren chapter '{chapterId}' needs authored narrative prose.");
        var text = value.Trim();
        var maxLength = string.Equals(chapterId, "approach_manor", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "informant_parley", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "gadget_infiltration", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "stealth_crossing", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "guard_interrogation", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "lock_pick", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "rune_memory", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "ward_steward_parley", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "physical_pressure", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "timed_rhythm", StringComparison.OrdinalIgnoreCase)
            ? 3600
            : 520;
        Assert.InRange(text.Length, 140, maxLength);
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
        var isLongAftermathResult =
            (string.Equals(chapterId, "stealth_crossing", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "stealth_crossing_action", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(grade, "success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "partial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "fail", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(chapterId, "lock_pick", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "lock_pick_action", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(grade, "success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "partial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "fail", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(chapterId, "guard_interrogation", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "guard_interrogation_action", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(grade, "success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "partial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "fail", StringComparison.OrdinalIgnoreCase))) ||
            (string.Equals(chapterId, "gadget_infiltration", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "gadget_infiltration_action", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(grade, "success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "partial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "fail", StringComparison.OrdinalIgnoreCase))) ||
            string.Equals(chapterId, "physical_pressure", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "physical_pressure_action", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(chapterId, "rune_memory", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "rune_memory_action", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(grade, "success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "partial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "fail", StringComparison.OrdinalIgnoreCase)) ||
            string.Equals(chapterId, "ward_steward_parley", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(actionId, "ward_steward_parley_action", StringComparison.OrdinalIgnoreCase) &&
            (string.Equals(grade, "success", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "partial", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(grade, "fail", StringComparison.OrdinalIgnoreCase));
        var maxLength = isLongAftermathResult
            ? 2600
            : 260;
        Assert.InRange(text.Length, 70, maxLength);
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

    private static int CountOccurrences(string value, string needle)
    {
        var count = 0;
        var searchStart = 0;
        while (true)
        {
            var index = value.IndexOf(needle, searchStart, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return count;

            count++;
            searchStart = index + needle.Length;
        }
    }

    private static void AssertContainsInkFeatherAmountSignal(string context, int amount, string value)
    {
        string[] amountSignals = amount switch
        {
            1 => ["1", "одн"],
            2 => ["2", "дв"],
            4 => ["4", "четыр"],
            6 => ["6", "шест"],
            _ => [amount.ToString(System.Globalization.CultureInfo.InvariantCulture)]
        };

        AssertContainsAny($"{context} Ink Feather amount", value, amountSignals);
    }

    private static void AssertContainsAny(string context, string value, IReadOnlyList<string> terms)
    {
        Assert.True(ContainsAny(value, terms),
            $"Daren chapter '{context}' needs one of these story signals: {string.Join(", ", terms)}.");
    }

    private static void AssertContainsEveryTermGroup(string context, string value, IEnumerable<IReadOnlyList<string>> termGroups)
    {
        var index = 0;
        foreach (var terms in termGroups)
        {
            Assert.True(ContainsAny(value, terms),
                $"Daren chapter '{context}' needs story signal group {index}: {string.Join(", ", terms)}.");
            index++;
        }
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
