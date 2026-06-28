using System.Reflection;
using BookOfEternityClient.Services.GmWorkers;

namespace BookOfEternityClient.Configuration;

/// <summary>
/// Game client settings - persisted to game_session/config.json
/// </summary>
public class GameSettings
{
    private static readonly PropertyInfo[] WritableProperties = typeof(GameSettings)
        .GetProperties(BindingFlags.Public | BindingFlags.Instance)
        .Where(prop => prop.CanRead && prop.CanWrite)
        .ToArray();

    public string Language { get; set; } = "ru";
    public bool AllowHistoryManipulation { get; set; } = false;
    public bool ShowGmThoughts { get; set; } = false;
    public bool ShowImagesInConsole { get; set; } = true;
    public string ImageProvider { get; set; } = "placeholder";
    public int ImageWidth { get; set; } = 768;
    public int ImageHeight { get; set; } = 512;
    public string PollinationsApiKey { get; set; } = "";
    public string PollinationsImageModel { get; set; } = "flux";
    public string OpenRouterApiKey { get; set; } = "";
    public string OpenRouterImageModel { get; set; } = "";
    public string GameSessionPath { get; set; } = "game_session";
    public int AutosaveIntervalTurns { get; set; } = 1;
    public int MaxAutosaves { get; set; } = 10;
    public int MaxManualSaves { get; set; } = 50;
    public int GmTimeoutSeconds { get; set; } = 300;
    /// <summary>
    /// Enables the local GM bridge transport instead of clipboard/window automation when available.
    /// </summary>
    public bool GmBridgeEnabled { get; set; } = true;
    /// <summary>
    /// Preferred GM daemon backend: ConPTYBridge, Clipboard, or WindowAutoPaste.
    /// </summary>
    public string GmBridgeBackend { get; set; } = "ConPTYBridge";
    /// <summary>
    /// Arbitrary shell command line started inside the GM bridge shell session, for example "codex -m gpt-5.5 -c model_reasoning_effort=\"high\" --dangerously-bypass-approvals-and-sandbox".
    /// </summary>
    public string GmCliLaunchCommand { get; set; } = "codex -m gpt-5.5 -c model_reasoning_effort=\"high\" --dangerously-bypass-approvals-and-sandbox";
    /// <summary>
    /// Optional explicit working directory for the hidden GM bridge shell. Empty means the game_session directory.
    /// </summary>
    public string GmBridgeShellWorkingDirectory { get; set; } = "";
    /// <summary>
    /// Auto-start the local GM bridge helper when the daemon cannot find a live bridge.
    /// </summary>
    public bool GmBridgeAutoStart { get; set; } = false;
    /// <summary>
    /// Optional explicit named-pipe name override for bridge diagnostics / advanced setups.
    /// </summary>
    public string GmBridgePipeNameOverride { get; set; } = "";
    /// <summary>
    /// Controls when the GM bridge may press Enter after a bracketed paste.
    /// </summary>
    public string GmBridgePasteVisibilityPolicy { get; set; } = BookOfEternityClient.Configuration.GmBridgePasteVisibilityPolicy.ExactTextOrConfiguredMarker;
    /// <summary>
    /// Max seconds the GM bridge waits for a CLI prompt to render the pasted text or collapsed paste marker before pressing Enter.
    /// </summary>
    public double GmBridgePromptVisibilityTimeoutSeconds { get; set; } = 15;
    /// <summary>
    /// CLI-specific markers that prove a large pasted prompt was accepted even when the terminal collapses the text.
    /// </summary>
    public List<GmBridgePasteVisibilityMarker> GmBridgePasteVisibilityMarkers { get; set; } =
        BookOfEternityClient.Configuration.GmBridgePasteVisibilityPolicy.CreateDefaultMarkers();
    /// <summary>
    /// Explicit subordinate GM worker bridge profiles. Workers are hidden/background by contract and only return proposals.
    /// </summary>
    public List<WorkerBridgeProfile> GmWorkerBridgeProfiles { get; set; } =
        GmWorkerBridgeProfileTemplates.CreateDefaultTemplates().ToList();
    public string GameVersion { get; set; } = "1.0.0";
    /// <summary>
    /// Game difficulty: "normal", "hard", or "impossible".
    /// Affects enemy stats, action check difficulty, experience and loot (see Block_0.5, Block_0.6).
    /// </summary>
    public string Difficulty { get; set; } = "normal";
    /// <summary>
    /// Automatically discard items with 0 durability (broken) from inventory.
    /// </summary>
    public bool AutoDiscardBrokenItems { get; set; } = false;
    /// <summary>
    /// Whether to automatically generate scene images each turn.
    /// </summary>
    public bool GenerateSceneImages { get; set; } = true;
    /// <summary>
    /// Generate image files, but never display them inside the client UI.
    /// </summary>
    public bool GenerateImagesWithoutDisplay { get; set; } = false;
    /// <summary>
    /// Enables or disables GM-authored QTE offers and local QTE scenes.
    /// </summary>
    public bool EnableQteEvents { get; set; } = true;
    /// <summary>
    /// Enables or disables background music playback.
    /// </summary>
    public bool MusicEnabled { get; set; } = true;
    /// <summary>
    /// Background music volume from 0 to 100.
    /// </summary>
    public int MusicVolume { get; set; } = 65;
    /// <summary>
    /// Enables or disables UI and gameplay sound effects.
    /// </summary>
    public bool SoundEnabled { get; set; } = true;
    /// <summary>
    /// Sound effect volume from 0 to 100.
    /// </summary>
    public int SoundVolume { get; set; } = 75;
    /// <summary>
    /// Preferred console font size in points/pixels for Windows console hosts.
    /// Some terminal emulators may ignore runtime font changes.
    /// </summary>
    public int ConsoleFontSize { get; set; } = 20;
    /// <summary>
    /// Browser client font scale from 80 to 200 percent. Stored in shared settings so the browser
    /// frontend does not need a separate local-only preferences store.
    /// </summary>
    public int BrowserFontScalePercent { get; set; } = 100;
    /// <summary>
    /// Browser client UI element scale from 80 to 200 percent. Controls padding, gaps, and button sizes
    /// independently of font scale.
    /// </summary>
    public int BrowserUiScalePercent { get; set; } = 100;
    /// <summary>
    /// Requests reduced motion in the Browser Client presentation layer.
    /// </summary>
    public bool BrowserReducedMotion { get; set; } = false;
    /// <summary>
    /// Enables a Browser Client contrast-friendly presentation mode.
    /// </summary>
    public bool BrowserContrastFriendly { get; set; } = false;
    /// <summary>
    /// File names of enabled global system mods stored in game_session/mods/.
    /// Each file is one mod and affects the whole game when enabled.
    /// </summary>
    public List<string> EnabledSystemMods { get; set; } = new();

    public void ApplyLoadedValues(GameSettings loaded)
    {
        var currentFontSize = ConsoleFontSize;

        foreach (var property in WritableProperties)
            property.SetValue(this, property.GetValue(loaded));

        MusicVolume = Math.Clamp(MusicVolume, 0, 100);
        SoundVolume = Math.Clamp(SoundVolume, 0, 100);
        BrowserFontScalePercent = loaded.BrowserFontScalePercent > 0
            ? Math.Clamp(loaded.BrowserFontScalePercent, 80, 200)
            : 100;
        ConsoleFontSize = loaded.ConsoleFontSize > 0
            ? Math.Clamp(loaded.ConsoleFontSize, 14, 32)
            : currentFontSize;
        EnabledSystemMods = loaded.EnabledSystemMods?
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList() ?? new List<string>();
        GmBridgePasteVisibilityPolicy = BookOfEternityClient.Configuration.GmBridgePasteVisibilityPolicy.NormalizePolicy(GmBridgePasteVisibilityPolicy);
        GmBridgeShellWorkingDirectory = GmBridgeShellWorkingDirectory?.Trim() ?? string.Empty;
        GmBridgePromptVisibilityTimeoutSeconds = loaded.GmBridgePromptVisibilityTimeoutSeconds > 0
            ? Math.Clamp(loaded.GmBridgePromptVisibilityTimeoutSeconds, 1, 60)
            : 15;
        GmBridgePasteVisibilityMarkers = BookOfEternityClient.Configuration.GmBridgePasteVisibilityPolicy.NormalizeMarkers(GmBridgePasteVisibilityMarkers);
        GmWorkerBridgeProfiles = NormalizeWorkerProfiles(loaded.GmWorkerBridgeProfiles);
    }

    private static List<WorkerBridgeProfile> NormalizeWorkerProfiles(IEnumerable<WorkerBridgeProfile>? profiles)
    {
        var normalized = profiles?
            .Where(profile => profile != null)
            .Select(profile =>
            {
                var permissions = profile.Permissions ?? new WorkerScopePolicy();
                return profile with
                {
                    LaunchVisibility = WorkerLaunchVisibility.Hidden,
                    TimeoutSeconds = profile.TimeoutSeconds > 0 ? profile.TimeoutSeconds : 180,
                    MaxConcurrentTasks = profile.MaxConcurrentTasks > 0 ? profile.MaxConcurrentTasks : 1,
                    Permissions = permissions with
                    {
                        TaskTypes = permissions.TaskTypes ?? [],
                        ReadPaths = permissions.ReadPaths ?? [],
                        ProposalWritePaths = permissions.ProposalWritePaths ?? []
                    }
                };
            })
            .ToList() ?? new List<WorkerBridgeProfile>();

        return normalized.Count == 0
            ? GmWorkerBridgeProfileTemplates.CreateDefaultTemplates().ToList()
            : normalized;
    }
}

/// <summary>
/// The 12 player characteristics as defined in Block_5.txt
/// </summary>
public static class Characteristics
{
    public const string Strength = "strength";
    public const string Dexterity = "dexterity";
    public const string Constitution = "constitution";
    public const string Intelligence = "intelligence";
    public const string Wisdom = "wisdom";
    public const string Faith = "faith";
    public const string Attractiveness = "attractiveness";
    public const string Trade = "trade";
    public const string Persuasion = "persuasion";
    public const string Perception = "perception";
    public const string Luck = "luck";
    public const string Speed = "speed";

    public static readonly Dictionary<string, string> RussianNames = new()
    {
        [Strength] = "Сила",
        [Dexterity] = "Ловкость",
        [Constitution] = "Выносливость",
        [Intelligence] = "Интеллект",
        [Wisdom] = "Мудрость",
        [Faith] = "Вера",
        [Attractiveness] = "Привлекательность",
        [Trade] = "Торговля",
        [Persuasion] = "Убеждение",
        [Perception] = "Восприятие",
        [Luck] = "Удача",
        [Speed] = "Скорость"
    };

    public static readonly string[] All = {
        Strength, Dexterity, Constitution, Intelligence, Wisdom, Faith,
        Attractiveness, Trade, Persuasion, Perception, Luck, Speed
    };

    /// <summary>
    /// Detailed Russian descriptions of what each characteristic does in the game system (Block 5).
    /// </summary>
    public static readonly Dictionary<string, string> Descriptions = new()
    {
        [Strength] = "Физическая мощь. Урон тяжёлым оружием (секиры, молоты, двуручные мечи), грузоподъёмность, запугивание силой. Влияет на макс. здоровье (+1% за ед.) и макс. равновесие.",
        [Dexterity] = "Точность и координация. Урон точным и дальнобойным оружием (рапиры, луки, арбалеты), уклонение, взлом замков, ловкость рук, акробатика.",
        [Constitution] = "Стойкость и выносливость. Основной вклад в макс. здоровье (+2% за ед.), макс. энергию, макс. равновесие. Врождённое сопротивление урону (+1% за 10 ед.). Сопротивление болезням и ядам.",
        [Intelligence] = "Логика, память, аналитическое мышление. Магия на основе интеллекта, расшифровка кодов, понимание механизмов, тактика. Влияет на макс. энергию и макс. равновесие.",
        [Wisdom] = "Интуиция, сила воли, здравый смысл. Магия на основе мудрости, обнаружение обмана, навыки выживания, эмпатия. Влияет на макс. энергию и макс. равновесие.",
        [Faith] = "Сила убеждений и духовная связь. Способности паладина/жреца, сопротивление эффектам на душу, магия на основе веры. Влияет на макс. энергию.",
        [Attractiveness] = "Внешняя привлекательность и обаяние. Соблазнение, первое впечатление, эмоциональное воздействие, ухоженность.",
        [Trade] = "Торговая смекалка. Оценка товаров, торг, переговоры о ценах, знание рынка.",
        [Persuasion] = "Риторика и дипломатия. Убеждение через логику, обман, вдохновляющие речи, переговоры, влияние на решения.",
        [Perception] = "Наблюдательность всех пяти чувств. Поиск скрытых предметов, обнаружение засад, подслушивание, чтение языка тела.",
        [Luck] = "Врождённая удачливость. Шанс крит. удара (каждые 20 ед. расширяют диапазон крита), множитель крит. урона (+1% за 2 ед.), случайные благоприятные события.",
        [Speed] = "Скорость реакции и движения. Инициатива в бою, урон лёгким оружием (кинжалы, удары руками), частота действий, быстрые атаки."
    };
}
