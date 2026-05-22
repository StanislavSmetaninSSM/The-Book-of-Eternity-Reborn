using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static partial class ExplorerUniversalMetaCommandResultBuilder
{
    private enum CommandKind
    {
        Status,
        Soul,
        SoulRelics,
        AfterlifeArchive,
        ArchiveCandidates,
        SoulQuests,
        Codex,
        Achievements,
        Chronicle,
        Story,
        Behavior,
        Lives,
        Feathers,
        WorldRules,
        Gallery,
        Gm,
        Debug,
        Mods,
        SystemGuardians,
        SarefStory,
        SarefFindWings,
        SarefUseAdvantage,
        SarefFinalConfrontation,
        SarefOathBreak,
        SarefAgenda,
        MemoryScene
    }

    private static readonly IReadOnlyDictionary<string, CommandKind> CommandKinds =
        new Dictionary<string, CommandKind>(StringComparer.OrdinalIgnoreCase)
        {
            ["/status"] = CommandKind.Status,
            ["/статус"] = CommandKind.Status,
            ["/soul"] = CommandKind.Soul,
            ["/душа"] = CommandKind.Soul,
            ["/soul_relics"] = CommandKind.SoulRelics,
            ["/реликвии"] = CommandKind.SoulRelics,
            ["/afterlife_archive"] = CommandKind.AfterlifeArchive,
            ["/архив_души"] = CommandKind.AfterlifeArchive,
            ["/archive_candidates"] = CommandKind.ArchiveCandidates,
            ["/архив_кандидаты"] = CommandKind.ArchiveCandidates,
            ["/soul_quests"] = CommandKind.SoulQuests,
            ["/квесты_души"] = CommandKind.SoulQuests,
            ["/codex"] = CommandKind.Codex,
            ["/кодекс"] = CommandKind.Codex,
            ["/achievements"] = CommandKind.Achievements,
            ["/достижения"] = CommandKind.Achievements,
            ["/chronicle"] = CommandKind.Chronicle,
            ["/хроника"] = CommandKind.Chronicle,
            ["/story"] = CommandKind.Story,
            ["/рассказ"] = CommandKind.Story,
            ["/история"] = CommandKind.Story,
            ["/behavior"] = CommandKind.Behavior,
            ["/поведение"] = CommandKind.Behavior,
            ["/lives"] = CommandKind.Lives,
            ["/жизни"] = CommandKind.Lives,
            ["/feathers"] = CommandKind.Feathers,
            ["/перья"] = CommandKind.Feathers,
            ["/world_rules"] = CommandKind.WorldRules,
            ["/правила_мира"] = CommandKind.WorldRules,
            ["/gallery"] = CommandKind.Gallery,
            ["/галерея"] = CommandKind.Gallery,
            ["/gm"] = CommandKind.Gm,
            ["/гм"] = CommandKind.Gm,
            ["/debug"] = CommandKind.Debug,
            ["/отладка"] = CommandKind.Debug,
            ["/mods"] = CommandKind.Mods,
            ["/моды"] = CommandKind.Mods,
            ["/system_guardians"] = CommandKind.SystemGuardians,
            ["/системные_хранители"] = CommandKind.SystemGuardians,
            ["/извечные_хранители"] = CommandKind.SystemGuardians,
            ["/saref"] = CommandKind.SarefStory,
            ["/сареф"] = CommandKind.SarefStory,
            ["/saref_story"] = CommandKind.SarefStory,
            ["/история_сарефа"] = CommandKind.SarefStory,
            ["/wings_of_angels"] = CommandKind.SarefStory,
            ["/крылья_над_бездной"] = CommandKind.SarefStory,
            ["/сареф найти_крылья"] = CommandKind.SarefFindWings,
            ["/saref find_wings"] = CommandKind.SarefFindWings,
            ["/сареф преимущество"] = CommandKind.SarefUseAdvantage,
            ["/saref use_advantage"] = CommandKind.SarefUseAdvantage,
            ["/сареф конфронтация"] = CommandKind.SarefFinalConfrontation,
            ["/saref confrontation"] = CommandKind.SarefFinalConfrontation,
            ["/сареф разорвать_клятву"] = CommandKind.SarefOathBreak,
            ["/saref break_oath"] = CommandKind.SarefOathBreak,
            ["/сареф поручение"] = CommandKind.SarefAgenda,
            ["/saref agenda"] = CommandKind.SarefAgenda,
            ["/воспоминание"] = CommandKind.MemoryScene,
            ["/воспоминание_статус"] = CommandKind.MemoryScene,
            ["/воспоминание_начать"] = CommandKind.MemoryScene,
            ["/воспоминание_способности"] = CommandKind.MemoryScene
        };

    public static bool CanBuild(string command) => CommandKinds.ContainsKey(command.Trim());

    public static async Task<ExplorerCommandResult?> TryBuildAsync(
        string command,
        StateManager stateManager,
        FileSystemManager fs,
        LocalizationManager loc)
    {
        var normalizedCommand = command.Trim();
        if (!CommandKinds.TryGetValue(normalizedCommand, out var kind))
            return null;

        await stateManager.RefreshGameStateAsync();

        return kind switch
        {
            CommandKind.Status => BuildStatus(normalizedCommand, stateManager),
            CommandKind.Soul => await BuildSoul(normalizedCommand, fs),
            CommandKind.SoulRelics => await BuildSoulSection(normalizedCommand, fs, "Реликвии души", "soulRelics"),
            CommandKind.AfterlifeArchive => await BuildSoulSection(normalizedCommand, fs, "Архив души", "afterlifeArchive"),
            CommandKind.ArchiveCandidates => await BuildJsonFile(normalizedCommand, fs, "Кандидаты в Архив", AfterlifeArchiveCandidateService.ManifestPath),
            CommandKind.SoulQuests => await BuildJsonFile(normalizedCommand, fs, "Квесты души", "game_state/meta/guardians.json"),
            CommandKind.Codex => await BuildJsonFile(normalizedCommand, fs, "Кодекс", "lore/codex_entries.json", BuildCodexSummary),
            CommandKind.Achievements => await BuildJsonFile(normalizedCommand, fs, "Достижения", "game_state/meta/achievements.json", BuildAchievementsSummary),
            CommandKind.Chronicle => await BuildChronicle(normalizedCommand, fs),
            CommandKind.Story => BuildStory(normalizedCommand, fs),
            CommandKind.Behavior => await BuildJsonFile(normalizedCommand, fs, "Поведение игрока", "game_state/meta/player_behavior.json"),
            CommandKind.Lives => await BuildSoulSection(normalizedCommand, fs, "История жизней", "livesHistory"),
            CommandKind.Feathers => await BuildSoulSection(normalizedCommand, fs, "Чернильные Перья", "inkFeathers"),
            CommandKind.WorldRules => await BuildJsonFile(normalizedCommand, fs, "Досье текущего мира", WorldDirectiveService.ActiveDirectivesPath),
            CommandKind.Gallery => BuildGallery(normalizedCommand, fs),
            CommandKind.Gm => await BuildGmThoughts(normalizedCommand, fs, loc),
            CommandKind.Debug => BuildDebug(normalizedCommand, fs, stateManager, loc),
            CommandKind.Mods => BuildDirectoryList(normalizedCommand, fs, "Моды", "mods"),
            CommandKind.SystemGuardians => BuildSystemGuardians(normalizedCommand, fs),
            CommandKind.SarefStory => await BuildSarefStory(normalizedCommand, fs),
            CommandKind.SarefFindWings => await BuildSarefFindWings(normalizedCommand, stateManager, fs),
            CommandKind.SarefUseAdvantage => await BuildSarefUseAdvantage(normalizedCommand, fs),
            CommandKind.SarefFinalConfrontation => await BuildSarefFinalConfrontation(normalizedCommand, fs),
            CommandKind.SarefOathBreak => await BuildSarefOathBreak(normalizedCommand, fs),
            CommandKind.SarefAgenda => await BuildSarefAgenda(normalizedCommand, fs),
            CommandKind.MemoryScene => await BuildSarefMemoryScene(normalizedCommand, fs),
            _ => null
        };
    }

    private static ExplorerCommandResult BuildStatus(string command, StateManager stateManager)
    {
        var state = stateManager.CurrentState;
        return Completed(command,
            Panel("Статус",
                Grid(
                    ("Realm", EmptyFallback(state.CurrentRealm)),
                    ("Душа", EmptyFallback(state.SoulName)),
                    ("Инкарнация", state.Incarnation.ToString()),
                    ("Персонаж", EmptyFallback(state.CharacterName)),
                    ("Класс / раса", JoinNonEmpty(" / ", state.CharacterClass, state.CharacterRace)),
                    ("Локация", EmptyFallback(state.CurrentLocation)),
                    ("Время мира", EmptyFallback(state.WorldTime)),
                    ("Состояние", EmptyFallback(state.PlayerStatus.CurrentCondition)),
                    ("Здоровье", EmptyFallback(state.PlayerStatus.HealthPercentage)),
                    ("Энергия", EmptyFallback(state.PlayerStatus.EnergyPercentage)),
                    ("Равновесие", EmptyFallback(state.PlayerStatus.PoisePercentage)),
                    ("Чернильные Перья", state.InkFeathers.ToString()),
                    ("Просветление", EmptyFallback(state.EnlightenmentTier)),
                    ("Активный Хранитель", EmptyFallback(state.ActiveGuardianName)),
                    ("Сияние", $"{state.ShiningRadianceExperience} XP / тир {state.ShiningRadianceTier}"),
                    ("Искры Света", state.ShiningLightSparks.ToString()))));
    }

    private static async Task<ExplorerCommandResult> BuildSoul(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, "game_state/meta/soul_state.json");
        if (read.Node == null)
            return MissingOrMalformed(command, "Душа", read);

        return Completed(command,
            Panel("Душа",
                Grid(
                    ("Имя души", GetString(read.Node, "soulName")),
                    ("Realm", GetString(read.Node, "currentRealm")),
                    ("Инкарнация", GetNumberOrString(read.Node, "currentIncarnation")),
                    ("Чернильные Перья", DescribeInkFeathers(read.Node)),
                    ("Просветление", DescribeNested(read.Node, "enlightenment")),
                    ("Жизней в истории", CountArray(read.Node, "livesHistory").ToString()))),
            Raw("Полный JSON game_state/meta/soul_state.json", read.Node));
    }

    private static async Task<ExplorerCommandResult> BuildSoulSection(
        string command,
        FileSystemManager fs,
        string title,
        string propertyName)
    {
        var read = await ReadJson(fs, "game_state/meta/soul_state.json");
        if (read.Node == null)
            return MissingOrMalformed(command, title, read);

        var section = read.Node[propertyName]?.DeepClone();
        return Completed(command,
            Panel(title,
                Grid(
                    ("Источник", "game_state/meta/soul_state.json"),
                    ("Поле", propertyName),
                    ("Статус", section == null ? "не найдено" : "найдено"))),
            Raw($"JSON: soul_state.{propertyName}", section ?? new JsonObject()));
    }

    private static async Task<ExplorerCommandResult> BuildChronicle(string command, FileSystemManager fs)
    {
        var blocks = new List<UiBlock>
        {
            Panel("Хроника", Grid(
                ("character_chronicle", await DescribeJsonFile(fs, "game_state/meta/character_chronicle.json")),
                ("player_chronicle", await DescribeJsonFile(fs, "lore/chaos_sea/player_chronicle.json")),
                ("plot_outline", await DescribeJsonFile(fs, "game_state/quests/plot_outline.json"))))
        };

        await AddRawJsonIfPresent(blocks, fs, "game_state/meta/character_chronicle.json", "JSON: character_chronicle");
        await AddRawJsonIfPresent(blocks, fs, "lore/chaos_sea/player_chronicle.json", "JSON: player_chronicle");
        await AddRawJsonIfPresent(blocks, fs, "game_state/quests/plot_outline.json", "JSON: plot_outline");
        return Completed(command, blocks);
    }

    private static ExplorerCommandResult BuildStory(string command, FileSystemManager fs)
    {
        var storiesDir = fs.ResolvePath("stories");
        if (!Directory.Exists(storiesDir))
            return Completed(command, Message(UiNotificationSeverity.Info, "Рассказ", "Папка stories пока не создана."));

        var rows = Directory.GetFiles(storiesDir, "*.jsonl", SearchOption.TopDirectoryOnly)
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Select(path => new UiTableRow
            {
                Cells =
                [
                    Path.GetFileName(path),
                    SafeCountLines(path).ToString(),
                    ToRelativeGameSessionPath(fs, path)
                ]
            })
            .ToList();

        return Completed(command,
            new UiTableBlock
            {
                Title = "Рассказ",
                Columns = ["Файл", "Записей", "Путь"],
                Rows = rows
            });
    }

    private static ExplorerCommandResult BuildGallery(string command, FileSystemManager fs)
    {
        var imagesDir = fs.ResolvePath("images");
        var rows = new List<UiTableRow>();
        if (Directory.Exists(imagesDir))
        {
            foreach (var directory in Directory.GetDirectories(imagesDir).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        Path.GetFileName(directory),
                        Directory.GetFiles(directory, "*", SearchOption.AllDirectories).Length.ToString(),
                        ToRelativeGameSessionPath(fs, directory)
                    ]
                });
            }
        }

        if (rows.Count == 0)
        {
            rows.Add(new UiTableRow { Cells = ["images", "0", "game_session/images"] });
        }

        return Completed(command,
            new UiTableBlock
            {
                Title = "Галерея",
                Columns = ["Раздел", "Файлов", "Путь"],
                Rows = rows
            });
    }

    private static async Task<ExplorerCommandResult> BuildGmThoughts(string command, FileSystemManager fs, LocalizationManager loc)
    {
        var read = await ReadJson(fs, "output/debug_logs.json");
        if (read.Node == null)
            return MissingOrMalformed(command, loc.T("gm_thoughts"), read);

        return Completed(command,
            new UiTextBlock
            {
                Tone = UiTone.Subtle,
                Text = GetString(read.Node, "gm_thoughts_markdown", "Нет данных ГМ.")
            },
            Raw("Полный JSON output/debug_logs.json", read.Node));
    }

    private static ExplorerCommandResult BuildDebug(
        string command,
        FileSystemManager fs,
        StateManager stateManager,
        LocalizationManager loc)
    {
        var files = fs.GetAllGameStateFiles();
        var rows = files
            .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase)
            .Take(40)
            .Select(path => new UiTableRow
            {
                Cells =
                [
                    ToRelativeGameSessionPath(fs, path),
                    SafeFileLength(path).ToString()
                ]
            })
            .ToList();

        return Completed(command,
            Panel(loc.T("debug_info"),
                Grid(
                    ("Файлов состояния", files.Length.ToString()),
                    ("Сессия", EmptyFallback(stateManager.CurrentState.SessionId)),
                    ("Язык", loc.CurrentLanguage),
                    ("BasePath", fs.BasePath))),
            new UiTableBlock
            {
                Title = "Файлы состояния",
                Columns = ["Путь", "Байт"],
                Rows = rows
            });
    }

    private static ExplorerCommandResult BuildDirectoryList(string command, FileSystemManager fs, string title, string relativePath)
    {
        var root = fs.ResolvePath(relativePath);
        var rows = new List<UiTableRow>();
        if (Directory.Exists(root))
        {
            foreach (var path in Directory.EnumerateFileSystemEntries(root).OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        Path.GetFileName(path),
                        Directory.Exists(path) ? "папка" : "файл",
                        ToRelativeGameSessionPath(fs, path)
                    ]
                });
            }
        }

        return Completed(command,
            new UiTableBlock
            {
                Title = title,
                Columns = ["Имя", "Тип", "Путь"],
                Rows = rows
            });
    }

    private static ExplorerCommandResult BuildSystemGuardians(string command, FileSystemManager fs)
    {
        var root = Path.Combine(fs.BasePath, SystemGuardianLibraryService.RootDirectoryName);
        var rows = new List<UiTableRow>();
        if (Directory.Exists(root))
        {
            foreach (var manifest in Directory.GetFiles(root, "manifest.json", SearchOption.AllDirectories)
                         .OrderBy(static path => path, StringComparer.OrdinalIgnoreCase))
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        Path.GetFileName(Path.GetDirectoryName(manifest) ?? manifest),
                        ToRelativeBasePath(fs, manifest)
                    ]
                });
            }
        }

        return Completed(command,
            new UiTableBlock
            {
                Title = "Извечные хранители",
                Columns = ["Preset", "Manifest"],
                Rows = rows
            });
    }

    private static async Task<ExplorerCommandResult> BuildSarefStory(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, SarefMainStoryState.StatePath);
        if (read.Node == null)
        {
            if (read.FileExists)
                return MissingOrMalformed(command, "Крылья над Бездной", read);

            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Крылья над Бездной",
                    "Ты пока не знаешь, что искать."));
        }

        if (read.Node is not JsonObject root)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Warning,
                    "Крылья над Бездной",
                    $"{SarefMainStoryState.StatePath} должен быть JSON object."));
        }

        if (IsSarefStoryStillUnknown(root))
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Крылья над Бездной",
                    "Ты пока не знаешь, что искать."));
        }

        return Completed(command,
            Panel("Крылья над Бездной",
                Grid(
                    ("Стадия раскрытия", DescribeSarefRevealStage(GetString(root, "revealStage", SarefMainStoryState.RevealStageUnknown))),
                    ("Фрагментов", CountArray(root, "sarefRevelations").ToString()),
                    ("Преимуществ", CountArray(root, "sarefAdvantages").ToString()),
                    ("Использований преимуществ", CountArray(root, "sarefAdvantageUses").ToString()),
                    ("Известных агентов", CountNestedArray(root, "factionLinks", "knownAgents").ToString()),
                    ("Финал", DescribeNested(root, "finalConfrontation")),
                    ("Клятва", DescribeNested(root, "playerOathState")))),
            Raw($"Полный JSON {SarefMainStoryState.StatePath}", root));
    }

    private static async Task<ExplorerCommandResult> BuildSarefMemoryScene(string command, FileSystemManager fs)
    {
        var read = await ReadJson(fs, SarefMainStoryState.StatePath);
        if (read.Node is not JsonObject root)
        {
            var message = read.FileExists
                ? $"Файл найден, но не разобран как состояние скрытой линии: {read.Path}. {read.Error}"
                : "Активного Воспоминания нет. Это не Врата Памяти и не Наследие Памяти: Воспоминание появляется только как особый слой 4-го квеста Хранителя в линии Сарефа.";
            return Completed(command, Message(read.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info, "Воспоминание", message));
        }

        if (root["memoryScene"] is not JsonObject scene)
        {
            return Completed(command,
                Message(
                    UiNotificationSeverity.Info,
                    "Воспоминание",
                    "Активного Воспоминания нет. Это не Врата Памяти и не Наследие Памяти: Воспоминание появляется только как особый слой 4-го квеста Хранителя в линии Сарефа."));
        }

        var title = GetString(scene, "title", GetString(scene, "sceneTitle", GetString(scene, "sceneId", "без названия")));
        var role = scene["role"] as JsonObject;
        var roleName = role == null
            ? "не указана"
            : GetString(role, "displayName", GetString(role, "roleId", "не указана"));
        var roleSummary = role == null ? string.Empty : GetString(role, "summary", string.Empty);

        var blocks = new List<UiBlock>
        {
            Panel("Воспоминание",
                Grid(
                    ("Сцена", title),
                    ("Состояние", DescribeSarefMemorySceneStatus(GetString(scene, "status", string.Empty))),
                    ("Память Хранителя", GetString(scene, "guardianId")),
                    ("Квест", JoinNonEmpty(" / ", GetString(scene, "questId", string.Empty), GetNumberOrString(scene, "questOrdinal"))),
                    ("Роль внутри сцены", JoinNonEmpty(" - ", roleName, roleSummary)))),
            new UiTextBlock
            {
                Tone = UiTone.Warning,
                Text = "Это не Врата Памяти и не Наследие Памяти. Смертный инвентарь не переносится; исторический факт нельзя напрямую переписать."
            }
        };

        blocks.Add(BuildMemorySceneObjectTable("Границы сцены", scene["boundaries"] as JsonArray, "boundaryId", preferName: false));
        blocks.Add(BuildMemorySceneObjectTable("Доступные способности", scene["abilities"] as JsonArray, "abilityId", preferName: true));
        blocks.Add(BuildMemorySceneNodeTable(scene["requiredStoryNodes"] as JsonArray));
        blocks.Add(BuildMemorySceneSuccessCondition(scene["successCondition"] as JsonObject));
        blocks.Add(BuildMemorySceneClosureTarget(scene["closureTarget"] as JsonObject));
        return Completed(command, blocks);
    }

    private static UiTableBlock BuildMemorySceneObjectTable(string title, JsonArray? array, string idProperty, bool preferName)
    {
        var rows = new List<UiTableRow>();
        if (array != null)
        {
            foreach (var item in array.OfType<JsonObject>())
            {
                var name = preferName
                    ? GetString(item, "name", GetString(item, "displayName", GetString(item, idProperty)))
                    : GetString(item, "displayName", GetString(item, "name", GetString(item, idProperty)));
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        name,
                        GetString(item, "summary", GetString(item, "description", "не указано"))
                    ]
                });
            }
        }

        if (rows.Count == 0)
            rows.Add(new UiTableRow { Cells = ["не указано", "не указано"] });

        return new UiTableBlock
        {
            Title = title,
            Columns = ["Название", "Описание"],
            Rows = rows
        };
    }

    private static UiTableBlock BuildMemorySceneNodeTable(JsonArray? nodes)
    {
        var rows = new List<UiTableRow>();
        if (nodes != null)
        {
            foreach (var node in nodes.OfType<JsonObject>())
            {
                rows.Add(new UiTableRow
                {
                    Cells =
                    [
                        DescribeSarefMemorySceneNodeStatus(GetString(node, "status", string.Empty)),
                        GetString(node, "summary", GetString(node, "title", GetString(node, "nodeId")))
                    ]
                });
            }
        }

        if (rows.Count == 0)
            rows.Add(new UiTableRow { Cells = ["не указано", "не указано"] });

        return new UiTableBlock
        {
            Title = "Обязательные сюжетные узлы",
            Columns = ["Состояние", "Узел"],
            Rows = rows
        };
    }

    private static UiPanelBlock BuildMemorySceneSuccessCondition(JsonObject? condition)
    {
        if (condition == null)
            return Panel("Условие успеха", Grid(("Описание", "не указано")));

        return Panel("Условие успеха",
            Grid(
                ("Описание", GetString(condition, "summary", GetString(condition, "conditionId"))),
                ("Состояние", GetBool(condition, "satisfied") ? "выполнено" : "ещё не выполнено")));
    }

    private static UiPanelBlock BuildMemorySceneClosureTarget(JsonObject? target)
    {
        if (target == null)
            return Panel("Что закрывает сцена", Grid(("Цель", "не указано")));

        return Panel("Что закрывает сцена",
            Grid(
                ("Хранитель", GetString(target, "guardianId")),
                ("Квест", JoinNonEmpty(" / ", GetString(target, "questId", string.Empty), GetNumberOrString(target, "questOrdinal"))),
                ("Фрагмент истины", GetString(target, "revelationId")),
                ("Преимущество", GetString(target, "advantageId"))));
    }

    private static async Task<ExplorerCommandResult> BuildJsonFile(
        string command,
        FileSystemManager fs,
        string title,
        string path,
        Func<JsonNode, UiBlock>? summaryBuilder = null)
    {
        var read = await ReadJson(fs, path);
        if (read.Node == null)
            return MissingOrMalformed(command, title, read);

        var blocks = new List<UiBlock>();
        if (summaryBuilder != null)
            blocks.Add(summaryBuilder(read.Node));
        else
            blocks.Add(Panel(title, Grid(("Источник", path), ("Статус", "прочитано"))));

        blocks.Add(Raw($"Полный JSON {path}", read.Node));
        return Completed(command, blocks);
    }

    private static UiBlock BuildCodexSummary(JsonNode node) =>
        Panel("Кодекс", Grid(
            ("entries", CountArray(node, "entries").ToString()),
            ("codexEntries", CountArray(node, "codexEntries").ToString()),
            ("Корневой тип", node.GetType().Name)));

    private static UiBlock BuildAchievementsSummary(JsonNode node) =>
        Panel("Достижения", Grid(
            ("Открыто", CountArray(node, "unlockedAchievements").ToString()),
            ("В процессе", CountArray(node, "trackedProgress").ToString()),
            ("Корневой тип", node.GetType().Name)));

    private static ExplorerCommandResult MissingOrMalformed(string command, string title, JsonReadResult read) =>
        Completed(command,
            Message(
                read.FileExists ? UiNotificationSeverity.Warning : UiNotificationSeverity.Info,
                title,
                read.FileExists
                    ? $"Файл найден, но не разобран как JSON: {read.Path}. {read.Error}"
                    : $"Файл не найден: {read.Path}."));

    private static async Task AddRawJsonIfPresent(List<UiBlock> blocks, FileSystemManager fs, string path, string title)
    {
        var read = await ReadJson(fs, path);
        if (read.Node != null)
            blocks.Add(Raw(title, read.Node));
    }

    private static async Task<string> DescribeJsonFile(FileSystemManager fs, string path)
    {
        var read = await ReadJson(fs, path);
        if (read.Node != null)
            return "прочитано";
        return read.FileExists ? "повреждён" : "отсутствует";
    }

    private static async Task<JsonReadResult> ReadJson(FileSystemManager fs, string path)
    {
        var raw = await fs.ReadFileAsync(path);
        if (string.IsNullOrWhiteSpace(raw))
            return new JsonReadResult(path, FileExists: fs.FileExists(path), Node: null, Error: string.Empty);

        try
        {
            return new JsonReadResult(path, FileExists: true, Node: JsonNode.Parse(raw), Error: string.Empty);
        }
        catch (JsonException ex)
        {
            return new JsonReadResult(path, FileExists: true, Node: null, Error: ex.Message);
        }
    }

    private static ExplorerCommandResult Completed(string command, params UiBlock[] blocks) =>
        Completed(command, blocks.AsEnumerable());

    private static ExplorerCommandResult Completed(string command, IEnumerable<UiBlock> blocks) =>
        new()
        {
            Command = command,
            State = CommandExecutionState.Completed,
            Blocks = blocks.ToList()
        };

    private static UiPanelBlock Panel(string title, params UiBlock[] blocks) =>
        new()
        {
            Title = title,
            Blocks = blocks.ToList()
        };

    private static UiKeyValueGridBlock Grid(params (string Key, string Value)[] items) =>
        new()
        {
            Items = items
                .Select(static item => new UiKeyValueItem { Key = item.Key, Value = EmptyFallback(item.Value) })
                .ToList()
        };

    private static UiMessageBlock Message(UiNotificationSeverity severity, string title, string message) =>
        new()
        {
            Severity = severity,
            Title = title,
            Message = message
        };

    private static UiRawJsonBlock Raw(string title, JsonNode node) =>
        new()
        {
            Title = title,
            Json = node.DeepClone()
        };

    private static string GetString(JsonNode? node, string propertyName, string fallback = "не указано") =>
        TryGetScalarString(node?[propertyName], out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static bool TryGetScalarString(JsonNode? node, out string value)
    {
        value = string.Empty;
        if (node is not JsonValue jsonValue)
            return false;

        if (jsonValue.TryGetValue<string>(out var text))
        {
            value = text;
            return true;
        }

        if (jsonValue.TryGetValue<int>(out var intValue))
        {
            value = intValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<long>(out var longValue))
        {
            value = longValue.ToString();
            return true;
        }

        if (jsonValue.TryGetValue<bool>(out var boolValue))
        {
            value = boolValue ? "true" : "false";
            return true;
        }

        return false;
    }

    private static string GetNumberOrString(JsonNode? node, string propertyName)
    {
        var value = node?[propertyName];
        return value switch
        {
            JsonValue jsonValue when jsonValue.TryGetValue<int>(out var number) => number.ToString(),
            JsonValue jsonValue when jsonValue.TryGetValue<string>(out var text) => EmptyFallback(text),
            _ => "не указано"
        };
    }

    private static string DescribeInkFeathers(JsonNode? node)
    {
        var feathers = node?["inkFeathers"];
        return feathers switch
        {
            JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            JsonObject obj => JoinNonEmpty(" / ",
                obj["current"]?.ToString() is { Length: > 0 } current ? $"сейчас {current}" : string.Empty,
                obj["total"]?.ToString() is { Length: > 0 } total ? $"всего {total}" : string.Empty),
            _ => "не указано"
        };
    }

    private static string DescribeNested(JsonNode? node, string propertyName)
    {
        if (node?[propertyName] is not JsonObject obj)
            return GetNumberOrString(node, propertyName);

        return JoinNonEmpty(" / ",
            GetOptionalString(obj, "currentTier"),
            GetOptionalString(obj, "tier"),
            GetOptionalString(obj, "experience") is { Length: > 0 } xp ? $"{xp} XP" : string.Empty,
            GetOptionalString(obj, "progressPercent") is { Length: > 0 } pct ? $"{pct}%" : string.Empty);
    }

    private static string GetOptionalString(JsonObject obj, string propertyName)
    {
        var node = obj[propertyName];
        return node switch
        {
            JsonValue value when value.TryGetValue<string>(out var text) => text,
            JsonValue value when value.TryGetValue<int>(out var number) => number.ToString(),
            _ => string.Empty
        };
    }

    private static bool GetBool(JsonObject obj, string propertyName) =>
        obj[propertyName] is JsonValue value &&
        value.TryGetValue<bool>(out var boolValue) &&
        boolValue;

    private static int CountArray(JsonNode? node, string propertyName) =>
        node?[propertyName] is JsonArray array ? array.Count : 0;

    private static int CountNestedArray(JsonNode? node, string objectName, string arrayName) =>
        node?[objectName] is JsonObject obj && obj[arrayName] is JsonArray array ? array.Count : 0;

    private static bool IsSarefStoryStillUnknown(JsonObject root)
    {
        var revealStage = GetString(root, "revealStage", string.Empty);
        var hasContent = CountArray(root, "sarefRevelations") > 0 ||
                         CountArray(root, "sarefAdvantages") > 0 ||
                         CountArray(root, "sarefAdvantageUses") > 0 ||
                         CountNestedArray(root, "factionLinks", "shadowTraces") > 0 ||
                         CountNestedArray(root, "factionLinks", "knownAgents") > 0;

        return !hasContent &&
               (string.IsNullOrWhiteSpace(revealStage) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageUnknown, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(revealStage, SarefMainStoryState.RevealStageShadow, StringComparison.OrdinalIgnoreCase));
    }

    private static string DescribeSarefRevealStage(string stage) =>
        stage.ToLowerInvariant() switch
        {
            "unknown" => "ты пока не знаешь, что искать",
            "shadow" => "есть тень, но нет имени",
            "name_revealed" => "имя раскрыто",
            "wings_revealed" => "Крылья Ангелов раскрыты",
            "infiltration_active" => "идёт внедрение",
            "confrontation_available" => "можно выйти к финальному столкновению",
            "completed" => "линия завершена",
            _ => EmptyFallback(stage)
        };

    private static string DescribeSarefMemorySceneStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "available" => "доступно",
            "active" => "активно",
            "blocked" => "заблокировано",
            "completed" => "завершено",
            "failed" => "провалено",
            _ => EmptyFallback(status)
        };

    private static string DescribeSarefMemorySceneNodeStatus(string status) =>
        status.ToLowerInvariant() switch
        {
            "pending" => "ожидает",
            "active" => "активно",
            "completed" => "выполнено",
            "failed" => "провалено",
            _ => EmptyFallback(status)
        };

    private static string JoinNonEmpty(string separator, params string?[] values)
    {
        var parts = values
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .Select(static value => value!.Trim())
            .ToArray();
        return parts.Length == 0 ? "не указано" : string.Join(separator, parts);
    }

    private static string EmptyFallback(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "не указано" : value.Trim();

    private static int SafeCountLines(string path)
    {
        try
        {
            return File.ReadLines(path, Encoding.UTF8).Count(static line => !string.IsNullOrWhiteSpace(line));
        }
        catch
        {
            return 0;
        }
    }

    private static long SafeFileLength(string path)
    {
        try
        {
            return new FileInfo(path).Length;
        }
        catch
        {
            return 0;
        }
    }

    private static string ToRelativeGameSessionPath(FileSystemManager fs, string fullPath)
    {
        var root = fs.GameSessionPath;
        return Path.GetRelativePath(root, fullPath).Replace('\\', '/');
    }

    private static string ToRelativeBasePath(FileSystemManager fs, string fullPath) =>
        Path.GetRelativePath(fs.BasePath, fullPath).Replace('\\', '/');

    private sealed record JsonReadResult(string Path, bool FileExists, JsonNode? Node, string Error);
}
