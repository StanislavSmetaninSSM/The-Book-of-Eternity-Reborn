using System.Text.Json;
using System.Text.Json.Nodes;
using System.Security.Cryptography;
using System.Text;
using Spectre.Console;
using Spectre.Console.Rendering;
using BookOfEternityClient.Core;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

/// <summary>
/// Explorer mode: handles local /commands that read game state files
/// and display formatted data without sending to the GM.
/// Supports bilingual commands (Russian/English).
/// </summary>
public partial class ExplorerMode
{
    private const string ExplorerLocalTurnRollbackRoot = "game_state/control/explorer_local_turn_rollback";

    private readonly IExplorerConsole _console;
    private readonly StateManager _stateManager;
    private readonly FileSystemManager _fs;
    private readonly LocalizationManager _loc;
    private readonly Services.ValidationService? _validator;
    private readonly Services.CharacteristicsService? _charService;
    private readonly Services.StoryService? _storyService;
    private readonly Services.ImageService? _imageService;
    private readonly Services.PendingTurnStateService? _pendingTurnState;
    private readonly Services.GuardianTradeService? _guardianTradeService;
    private readonly Services.NpcTradeService? _npcTradeService;
    private readonly Services.SystemModService? _systemModService;
    private readonly Services.SystemGuardianLibraryService? _systemGuardianLibraryService;
    private readonly Services.WorldDirectiveService? _worldDirectiveService;
    private readonly Services.ScenarioCoreService? _scenarioCoreService;
    private readonly Services.AfterlifeArchiveCandidateService? _afterlifeArchiveCandidateService;
    private readonly Services.AfterlifeArchiveConsultationService? _afterlifeArchiveConsultationService;
    private readonly Services.AfterlifeArchiveProjectFuelService? _afterlifeArchiveProjectFuelService;
    private readonly Services.GuardianCorrectionService? _guardianCorrectionService;
    private readonly Services.SoulIdentityService? _soulIdentityService;
    private readonly Services.IClipboardService? _clipboardService;
    private readonly Services.LocalUiSessionLockService _localUiSessionLockService;
    private readonly Services.LocalUiSessionLockOwner _localUiSessionLockOwner;

    // Set by interactive commands (equip/unequip) to signal an action to send to the GM
    private string? _pendingGmAction;
    private string _currentCommandRemainder = string.Empty;
    private PendingLocalTurnRollbackSnapshot? _pendingLocalTurnRollbackSnapshot;
    // Set by Reveal Fate so Rewrite Fate becomes available
    private bool _diceRevealed;

    private sealed record RivalManifestationEntry(int Stage, string Kind, string Title, string Summary, string TimeLabel);

    private sealed record RelatedRivalQuestSummary(string Title, string Status, bool IsCounterQuest);

    private sealed record RelatedRivalWorldEventSummary(
        string Headline,
        string Summary,
        string Visibility,
        string Category,
        string Location,
        string TimeLabel,
        IReadOnlyList<string> ChangeEffects);

    internal sealed class PendingLocalTurnRollbackSnapshot
    {
        public Dictionary<string, string> BackupFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, string> BackupHashes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> TrackedFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> BaselineFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ValidationSnapshotFiles { get; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed record AfterlifeArchiveEntrySummary(
        string ArchiveId,
        string Title,
        string EntryType,
        string Rarity,
        string Summary,
        string Content,
        int SourceLife,
        string SourceKind,
        string SourceEntryId,
        string AcquiredAtUtc,
        string SourceGuardianId,
        string SourceGuardianName,
        IReadOnlyList<string> Tags,
        bool IsReserved,
        string ReservationKind,
        string ReservedForGuardianId,
        string ReservedForGuardianName,
        string ReservedForProjectId,
        string ReservedForProjectName,
        JsonObject RawJson);

    private sealed record AfterlifeArchiveCandidateSummary(
        string CandidateId,
        string SourceKind,
        string SourceEntryId,
        int SourceLife,
        string ProposedEntryType,
        string Title,
        string Summary,
        string Content,
        string Rarity,
        string Status,
        string DiscoveredAt,
        string ArchivedAtUtc,
        string SkippedAtUtc,
        IReadOnlyList<string> Tags);

    private sealed record FriendlyGuardianConsultationChoice(
        string GuardianId,
        string GuardianName,
        int Reputation,
        string Domain,
        bool FuelAvailable,
        string TargetProjectId,
        string TargetProjectName);

    private sealed record VisibleRivalSoulThread(
        string ArcId,
        string DisplayName,
        string RoleSummary,
        string Objective,
        string Scope,
        string ScopeLabel,
        string Status,
        string StatusLabel,
        string ArcType,
        string TypeLabel,
        string SponsorGuardianName,
        string Stakes,
        bool TargetsPlayerDirectly,
        bool HasFreshSignal,
        string ListLabel,
        string LastManifestationSummary,
        IReadOnlyList<RivalManifestationEntry> Manifestations);

    // Commands available in ALL realms
    private readonly Dictionary<string, Func<Task>> _universalCommands;
    // Commands ONLY available in Chaos Sea (afterlife)
    private readonly Dictionary<string, Func<Task>> _chaosSeaOnlyCommands;

    private bool IsOrdinaryAfterlifeInteractionState =>
        _stateManager.CurrentState.IsInChaosSea ||
        _stateManager.CurrentState.IsInShiningAbode;

    private bool EnsureOrdinaryAfterlifeInteractionAvailable(string title)
    {
        if (!_stateManager.CurrentState.IsInAfterlifeRealm)
            return true;

        if (IsOrdinaryAfterlifeInteractionState)
            return true;

        var message = _stateManager.CurrentState.HasInvalidShiningAbodeBootstrapPackage
            ? "Сияющая Обитель содержит повреждённый preparedIncarnationPackage. Обычные действия Моря Хаоса, Хранителей и Сияющей Обители недоступны до ремонта или очистки package fault."
            : "Сейчас активен handoff к следующей смертной жизни. Обычные действия Моря Хаоса, Хранителей и Сияющей Обители недоступны до завершения bootstrap.";

        ShowEmptyPanel(title, message);
        return false;
    }

    private static bool IsExactChaosSeaCommand(string command) =>
        string.Equals(command, "/chaos_sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/море_хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/gacha", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/гача", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/abodes", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/обители", StringComparison.OrdinalIgnoreCase);

    // Commands ONLY available in Mortal Life
    private readonly Dictionary<string, Func<Task>> _mortalOnlyCommands;
    // Commands available in both but behave differently
    private readonly HashSet<string> _allCommandNames;

    private void Write(IRenderable content) => _console.Write(content);

    private void WriteLine() => _console.WriteLine();

    private void MarkupLine(string markup) => _console.MarkupLine(markup);

    private void Clear() => _console.Clear();

    private string Ask(string prompt, string defaultValue = "") => _console.Ask(prompt, defaultValue);

    private bool Confirm(string prompt, bool defaultValue = false) => _console.Confirm(prompt, defaultValue);

    private T Prompt<T>(IPrompt<T> prompt) => _console.Prompt(prompt);

    private string? ReadLine() => _console.ReadLine();

    private ConsoleKeyInfo ReadKey() => _console.ReadKey();

    // ═══ Helper methods ═══

    private async Task SafeExecute(Func<Task> handler, string commandName)
    {
        try
        {
            await handler();
        }
        catch (Exception ex)
        {
            MarkupLine($"[red]❌ Ошибка при выполнении команды {Markup.Escape(commandName)}:[/]");
            MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            MarkupLine($"[dim]{Markup.Escape(ex.GetType().Name)}[/]");
            WaitForKey();
        }
    }

    private static readonly HashSet<string> LocalUiSessionMutatingCommands = new(StringComparer.OrdinalIgnoreCase)
    {
        "/validate",
        "/валидация",
        "/world_setup",
        "/настройка_мира",
        "/distribute",
        "/распределить",
        "/companion_directive",
        "/директива_компаньону",
        "/faction_directive",
        "/директива_фракции",
        "/craft",
        "/ремесло",
        "/abode_offering",
        "/подношение_обители",
        "/found_guardian_mantle",
        "/учредить_хранителя",
        "/shining_treasury",
        "/казначейство",
        "/source_of_light",
        "/источник_света",
        "/spiritual_action",
        "/духовное_действие"
    };

    private async Task<bool> TryAcquireLocalUiSessionMutationLockAsync(string commandName)
    {
        if (!LocalUiSessionMutatingCommands.Contains(commandName))
            return true;

        var operationLabel = $"Команда {commandName}";
        var result = await _localUiSessionLockService.AcquireOrRefreshAsync(_localUiSessionLockOwner, operationLabel);
        if (result.Acquired)
            return true;

        MarkupLine($"[yellow]⚠️ {Markup.Escape(result.BlockerMessage)}[/]");
        MarkupLine($"[dim]Lock-файл: {Markup.Escape(LocalUiSessionLockService.LockPath)}[/]");
        WaitForKey();
        return false;
    }

    private static int GetInt(JsonElement el, string prop, int def)
    {
        if (!el.TryGetProperty(prop, out var val)) return def;
        if (val.ValueKind == JsonValueKind.Number && val.TryGetInt32(out var i)) return i;
        if (val.ValueKind == JsonValueKind.String && int.TryParse(val.GetString(), out var parsed)) return parsed;
        return def;
    }

    private void ShowEmptyPanel(string title, string message)
    {
        var panel = new Panel(new Markup($"[dim]{message}[/]"))
        {
            Header = new PanelHeader($" {title} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 1)
        };
        Write(panel);
    }

    private void WriteJsonAuditPanel(string title, JsonNode? node, Color? borderColor = null)
    {
        if (node == null)
            return;

        var hasRedactedEffectPayload = ContainsRuntimeEffectPayload(node);
        var auditNode = hasRedactedEffectPayload
            ? CloneShiningJsonForPlayerFacingAudit(node)
            : node;
        var json = auditNode?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        if (string.IsNullOrWhiteSpace(json))
            return;
        var panelTitle = hasRedactedEffectPayload
            ? $"{title} (effectPayload redacted; safeEffectDetails shown)"
            : title;

        Write(new Panel(new Text(json))
        {
            Header = new PanelHeader($" {Markup.Escape(panelTitle)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? Color.Grey),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private void WriteJsonAuditPanel(string title, JsonElement element, Color? borderColor = null)
    {
        if (element.ValueKind is JsonValueKind.Undefined)
            return;

        var json = JsonSerializer.Serialize(element, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        var hasRedactedEffectPayload = json.Contains("\"effectPayload\"", StringComparison.Ordinal);
        if (hasRedactedEffectPayload)
        {
            var node = JsonNode.Parse(json);
            var auditNode = CloneShiningJsonForPlayerFacingAudit(node);
            json = auditNode?.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed) ?? json;
        }
        var panelTitle = hasRedactedEffectPayload
            ? $"{title} (effectPayload redacted; safeEffectDetails shown)"
            : title;

        Write(new Panel(new Text(json))
        {
            Header = new PanelHeader($" {Markup.Escape(panelTitle)} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(borderColor ?? Color.Grey),
            Padding = new Padding(1, 1),
            Expand = true
        });
    }

    private static JsonNode? CloneJsonElementForAudit(JsonElement element) =>
        element.ValueKind is JsonValueKind.Undefined
            ? null
            : JsonNode.Parse(element.GetRawText());

    private static bool ContainsRuntimeEffectPayload(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                if (obj.ContainsKey("effectPayload"))
                    return true;
                return obj.Any(pair => ContainsRuntimeEffectPayload(pair.Value));
            case JsonArray array:
                return array.Any(ContainsRuntimeEffectPayload);
            default:
                return false;
        }
    }

    private static JsonObject? CloneJsonObjectElementForAudit(JsonElement element) =>
        CloneJsonElementForAudit(element) as JsonObject;

    private async Task StartSystemGuardianAttractionAsync()
    {
        if (!_stateManager.CurrentState.IsInChaosSea)
        {
            ShowEmptyPanel(
                "Извечные хранители",
                "Притяжение к извечному Хранителю является Chaos Sea-only действием. В Сияющей Обители можно просматривать Хранителей, но нельзя создавать CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION contract.");
            return;
        }

        if (_systemGuardianLibraryService == null)
        {
            ShowEmptyPanel("Извечные хранители", "Библиотека извечных хранителей недоступна");
            return;
        }

        var preset = await PromptSystemGuardianPresetAsync("Выберите извечного Хранителя для притяжения");
        if (preset == null)
            return;

        var actionText = _systemGuardianLibraryService.BuildAttractionActionText(preset);
        var lines = new List<string>
        {
            "[bold magenta1]Притяжение извечного Хранителя[/]",
            "",
            $"  Пресет: [white]{Markup.Escape(preset.DisplayName)}[/] [dim]({Markup.Escape(preset.PresetId)})[/]",
            $"  Домен: [dim]{Markup.Escape(preset.Domain)}[/]",
            $"  Архетип: [dim]{Markup.Escape(preset.Archetype)}[/]",
            $"  Обитель: [dim]{Markup.Escape(preset.AbodeName)}[/]",
            $"  Тон: [dim]{Markup.Escape(preset.Tone)}[/]",
            "",
            "[bold]Контракт материализации для ГМ:[/]",
            "  • Создать/проявить Хранителя из system guardian preset, а не произвольного нового Хранителя.",
            "  • Сохранить targetPresetId/targetPresetDisplayName связь в authored state.",
            "  • playerAction содержит hidden marker CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION:presetId; runtime очищает control file только при этом marker.",
            "  • Обновить guardians.json и navigation как afterlife state, без Mortal World NPC/location side effects."
        };
        AppendChaosSeaPendingFileRule(lines, SystemGuardianLibraryService.AttractionRequestPath);
        AppendChaosSeaCommonContractRules(lines);
        if (!ConfirmChaosSeaContractPreview(
                "Полный предпросмотр притяжения Хранителя",
                lines,
                BuildChaosSeaDirectActionAudit(
                    "CHAOS_SEA_SYSTEM_GUARDIAN_ATTRACTION",
                    actionText,
                    ("presetId", preset.PresetId),
                    ("displayName", preset.DisplayName),
                    ("domain", preset.Domain),
                    ("abodeName", preset.AbodeName)),
                "Полный JSON system guardian attraction contract",
                confirmChoice: "✅ Создать притяжение"))
        {
            return;
        }

        try
        {
            await _systemGuardianLibraryService.WriteAttractionRequestAsync(preset);
        }
        catch (InvalidOperationException ex)
        {
            MarkupLine($"[red]{Markup.Escape(ex.Message)}[/]");
            return;
        }

        _pendingGmAction = actionText;
        MarkupLine($"[magenta1]🧲 Притяжение к «{Markup.Escape(preset.DisplayName)}» подготовлено. Запрос отправится Мастеру Игры как следующий ход.[/]");
    }

    private async Task<SystemGuardianLibraryService.SystemGuardianPresetDescriptor?> PromptSystemGuardianPresetAsync(string title)
    {
        if (_systemGuardianLibraryService == null)
            return null;

        var presets = await _systemGuardianLibraryService.GetAvailablePresetsAsync(includeDossier: true);
        var userDir = _systemGuardianLibraryService.GetUserDirectoryPath();
        if (presets.Count == 0)
        {
            ShowEmptyPanel("Извечные хранители", $"В библиотеке пока нет пресетов.\n\nПапка: {userDir}");
            return null;
        }

        while (true)
        {
            var presetChoices = presets
                .Select(preset => (
                    Label: BuildSystemGuardianPresetChoiceLabel(preset),
                    Identity: preset.PresetId,
                    Preset: preset))
                .ToList();
            var choices = MakeUniqueChoiceLabels(presetChoices
                    .Select(choice => (choice.Label, choice.Identity))
                    .ToList())
                .Append("📂 Открыть папку пользовательских извечных хранителей")
                .Append("← Назад")
                .ToList();

            var choice = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]{Markup.Escape(title)}[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .PageSize(12)
                .AddChoices(choices));

            if (choice.StartsWith("←", StringComparison.Ordinal))
                return null;

            if (choice.StartsWith("📂", StringComparison.Ordinal))
            {
                OpenFolderOrPrintPath(userDir);
                continue;
            }

            var selectedIndex = choices.IndexOf(choice);
            if (selectedIndex < 0 || selectedIndex >= presetChoices.Count)
                continue;

            var preset = presetChoices[selectedIndex].Preset;
            if (ShowSystemGuardianPresetDetail(preset))
                return preset;
        }
    }

    private static string BuildSystemGuardianPresetChoiceLabel(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset)
    {
        var displayName = string.IsNullOrWhiteSpace(preset.DisplayName) ? preset.PresetId : preset.DisplayName;
        var themes = BuildSystemGuardianPlayerFacingThemes(preset);
        var abodeName = string.IsNullOrWhiteSpace(preset.AbodeName) ? "обитель не указана" : preset.AbodeName;
        return $"{Markup.Escape(displayName)} [dim](темы: {Markup.Escape(themes)}; id={Markup.Escape(preset.PresetId)}; обитель: {Markup.Escape(abodeName)})[/]";
    }

    private static string BuildSystemGuardianPlayerFacingThemes(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset)
    {
        if (preset.CoreValues.Count > 0)
            return string.Join(", ", preset.CoreValues.Take(4));

        if (!string.IsNullOrWhiteSpace(preset.Summary))
            return preset.Summary;

        return "темы не указаны";
    }

    private bool ShowSystemGuardianPresetDetail(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset)
    {
        var lines = new List<string>
        {
            $"[bold cyan]{Markup.Escape(preset.DisplayName)}[/]",
            "",
            $"[white]Темы:[/] {Markup.Escape(BuildSystemGuardianPlayerFacingThemes(preset))}",
            $"[white]Обитель:[/] {Markup.Escape(preset.AbodeName)}",
            $"[white]Сводка:[/] {Markup.Escape(preset.Summary)}",
            $"[white]Технический id:[/] {Markup.Escape(preset.PresetId)}"
        };

        if (preset.CoreValues.Count > 0)
            lines.Add($"[white]Ценности:[/] {Markup.Escape(string.Join(", ", preset.CoreValues))}");

        if (!string.IsNullOrWhiteSpace(preset.DossierMarkdown))
        {
            lines.Add("");
            lines.Add("[bold]Досье:[/]");
            lines.AddRange(preset.DossierMarkdown!
                .Trim()
                .Replace("\r\n", "\n")
                .Split('\n')
                .Select(line => Markup.Escape(line)));
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🛡️ Извечный хранитель ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Magenta1),
            Padding = new Padding(1, 1),
            Expand = true
        });

        WriteJsonAuditPanel("Полный JSON system guardian preset", BuildSystemGuardianPresetAuditNode(preset), Color.Magenta1);

        var action = Prompt(new SelectionPrompt<string>()
            .Title("[cyan]Действия:[/]")
            .HighlightStyle(new Style(Color.Cyan1))
            .AddChoices("✅ Выбрать", "← Назад"));

        return action.StartsWith("✅", StringComparison.Ordinal);
    }

    private static JsonObject BuildSystemGuardianPresetAuditNode(SystemGuardianLibraryService.SystemGuardianPresetDescriptor preset) =>
        new()
        {
            ["presetId"] = preset.PresetId,
            ["displayName"] = preset.DisplayName,
            ["summary"] = preset.Summary,
            ["libraryKind"] = preset.LibraryKind,
            ["version"] = preset.Version,
            ["domain"] = preset.Domain,
            ["archetype"] = preset.Archetype,
            ["tone"] = preset.Tone,
            ["coreValues"] = new JsonArray(preset.CoreValues.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["defaultNameVariant"] = preset.DefaultNameVariant,
            ["feminineNameVariant"] = preset.FeminineNameVariant,
            ["masculineNameVariant"] = preset.MasculineNameVariant,
            ["neutralNameVariant"] = preset.NeutralNameVariant,
            ["formFlexibility"] = preset.FormFlexibility,
            ["defaultPresentationStyle"] = preset.DefaultPresentationStyle,
            ["defaultPronouns"] = preset.DefaultPronouns,
            ["defaultAppearanceDescription"] = preset.DefaultAppearanceDescription,
            ["abodeName"] = preset.AbodeName,
            ["abodeTheme"] = preset.AbodeTheme,
            ["mustPreserve"] = new JsonArray(preset.MustPreserve.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["canVary"] = new JsonArray(preset.CanVary.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["forbidden"] = new JsonArray(preset.Forbidden.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["searchLabel"] = preset.SearchLabel,
            ["searchKeywords"] = new JsonArray(preset.SearchKeywords.Select(value => JsonValue.Create(value)).ToArray<JsonNode?>()),
            ["directoryName"] = preset.DirectoryName,
            ["directoryPath"] = preset.DirectoryPath,
            ["manifestPath"] = preset.ManifestPath,
            ["dossierPath"] = preset.DossierPath,
            ["dossierMarkdown"] = preset.DossierMarkdown,
            ["promptPackage"] = preset.PromptPackage
        };

    private void OpenFolderOrPrintPath(string directoryPath)
    {
        Directory.CreateDirectory(directoryPath);

        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = directoryPath,
                UseShellExecute = true
            });
        }
        catch
        {
            MarkupLine($"[yellow]{Markup.Escape(directoryPath)}[/]");
            MarkupLine("[dim]Не удалось открыть папку автоматически. Путь выведен выше.[/]");
            WaitForKey();
        }
    }

    internal PendingLocalTurnRollbackSnapshot? ConsumePendingLocalTurnRollbackSnapshot()
    {
        var snapshot = _pendingLocalTurnRollbackSnapshot;
        _pendingLocalTurnRollbackSnapshot = null;
        return snapshot;
    }

    internal Task StagePendingLocalTurnRollbackSnapshotAsync(params string[] trackedFiles) =>
        EnsurePendingLocalTurnRollbackSnapshotAsync(trackedFiles);

    internal void MarkExistingPendingLocalTurnValidationSnapshotFiles(params string[] trackedFiles)
    {
        var snapshot = _pendingLocalTurnRollbackSnapshot;
        if (snapshot == null)
            return;

        foreach (var trackedFile in trackedFiles
                     .Where(path => !string.IsNullOrWhiteSpace(path))
                     .Select(path => path.Trim())
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_fs.FileExists(trackedFile))
                snapshot.ValidationSnapshotFiles.Add(trackedFile);
        }
    }

    internal Task RestoreStagedLocalTurnRollbackSnapshotAsync() =>
        RestorePendingLocalTurnRollbackSnapshotAsync();

    internal Task RestoreConsumedLocalTurnRollbackSnapshotAsync(PendingLocalTurnRollbackSnapshot? snapshot)
    {
        if (snapshot == null)
            return Task.CompletedTask;

        _pendingLocalTurnRollbackSnapshot = snapshot;
        return RestorePendingLocalTurnRollbackSnapshotAsync();
    }

    private async Task EnsurePendingLocalTurnRollbackSnapshotAsync(params string[] trackedFiles)
    {
        var normalizedTrackedFiles = trackedFiles
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(path => path.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (normalizedTrackedFiles.Count == 0)
            return;

        _pendingLocalTurnRollbackSnapshot ??= new PendingLocalTurnRollbackSnapshot();
        foreach (var trackedFile in normalizedTrackedFiles)
        {
            if (!_pendingLocalTurnRollbackSnapshot.TrackedFiles.Add(trackedFile))
                continue;

            if (!_fs.FileExists(trackedFile))
                continue;

            var backupContent = await _fs.ReadFileAsync(trackedFile);
            if (backupContent == null)
                continue;

            var backupPath = CreateExplorerRollbackBackupPath(trackedFile);
            await _fs.WriteFileAtomicAsync(backupPath, backupContent);
            _pendingLocalTurnRollbackSnapshot.BaselineFiles.Add(trackedFile);
            _pendingLocalTurnRollbackSnapshot.BackupFiles[trackedFile] = backupPath;
            _pendingLocalTurnRollbackSnapshot.BackupHashes[trackedFile] = ComputeExplorerRollbackHash(backupContent);
        }
    }

    private static string CreateExplorerRollbackBackupPath(string trackedFile)
    {
        var normalizedPath = trackedFile.Replace('\\', '/').Trim('/');
        var safePath = new string(normalizedPath
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_')
            .ToArray());
        if (string.IsNullOrWhiteSpace(safePath))
            safePath = "tracked_file";

        return $"{ExplorerLocalTurnRollbackRoot}/{DateTime.UtcNow.Ticks}_{Guid.NewGuid():N}/{safePath}.rollback.{Guid.NewGuid():N}";
    }

    private async Task RestorePendingLocalTurnRollbackSnapshotAsync()
    {
        var snapshot = _pendingLocalTurnRollbackSnapshot;
        if (snapshot == null)
            return;

        foreach (var trackedFile in snapshot.TrackedFiles)
        {
            if (snapshot.BaselineFiles.Contains(trackedFile))
                continue;

            if (_fs.FileExists(trackedFile))
                _fs.DeleteFile(trackedFile);
        }

        foreach (var (originalPath, backupPath) in snapshot.BackupFiles)
        {
            var backupContent = await _fs.ReadFileAsync(backupPath);
            if (backupContent == null)
                continue;

            if (snapshot.BackupHashes.TryGetValue(originalPath, out var expectedHash) &&
                !string.Equals(ComputeExplorerRollbackHash(backupContent), expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            await _fs.WriteFileAtomicAsync(originalPath, backupContent);
        }

        await DiscardPendingLocalTurnRollbackSnapshotAsync();
        await _stateManager.RefreshGameStateAsync();
    }

    private async Task DiscardPendingLocalTurnRollbackSnapshotAsync()
    {
        var snapshot = _pendingLocalTurnRollbackSnapshot;
        _pendingLocalTurnRollbackSnapshot = null;
        if (snapshot == null)
            return;

        foreach (var backupPath in snapshot.BackupFiles.Values.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (_fs.FileExists(backupPath))
                _fs.DeleteFile(backupPath);
        }

        DeleteEmptyExplorerRollbackDirectories();
        await Task.CompletedTask;
    }

    private void DeleteEmptyExplorerRollbackDirectories()
    {
        var rollbackRoot = _fs.ResolvePath(ExplorerLocalTurnRollbackRoot);
        if (!Directory.Exists(rollbackRoot))
            return;

        foreach (var directory in Directory.GetDirectories(rollbackRoot, "*", SearchOption.AllDirectories)
                     .OrderByDescending(path => path.Length))
        {
            if (!Directory.EnumerateFileSystemEntries(directory).Any())
                Directory.Delete(directory);
        }

        if (!Directory.EnumerateFileSystemEntries(rollbackRoot).Any())
            Directory.Delete(rollbackRoot);
    }

    private static string ComputeExplorerRollbackHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes);
    }

    private async Task ShowScenarioCoreReviewAsync()
    {
        if (_scenarioCoreService == null)
        {
            ShowEmptyPanel("Сценарное ядро", "Сервис сценарного ядра недоступен.");
            return;
        }

        var manifest = await _scenarioCoreService.ReadAsync();
        if (manifest == null)
        {
            ShowEmptyPanel("Сценарное ядро", "Сценарное ядро ещё не извлечено. Сначала задайте подготовку следующего мира.");
            return;
        }

        var confirmedCandidateIds = manifest.ScenarioCoreAssertions
            .Where(assertion => !string.IsNullOrWhiteSpace(assertion.CandidateId))
            .Select(assertion => assertion.CandidateId!)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var lines = new List<string>
        {
            "[bold magenta]🧩 Сценарное ядро следующей жизни[/]",
            "",
            "[white]Подтверждённые факты ниже считаются жёстким ядром старта и не должны ломаться Коррективами Хранителя.[/]"
        };

        lines.Add("");
        lines.Add("[bold]Подтверждённое ядро:[/]");
        if (manifest.ScenarioCoreAssertions.Count == 0)
        {
            lines.Add("[dim]Пока пусто.[/]");
        }
        else
        {
            foreach (var assertion in manifest.ScenarioCoreAssertions)
                lines.Add($"  • [magenta]{Markup.Escape(assertion.Category)}[/]: {Markup.Escape(assertion.Value)}");
        }

        lines.Add("");
        lines.Add("[bold]Извлечённые, но не подтверждённые факты:[/]");
        if (manifest.CandidateAssertions.Count == 0)
        {
            lines.Add("[dim]Ничего не ожидает подтверждения.[/]");
        }
        else
        {
            foreach (var candidate in manifest.CandidateAssertions)
            {
                var marker = confirmedCandidateIds.Contains(candidate.CandidateId) ? "[green]✓[/]" : "[yellow]?[/]";
                lines.Add($"  {marker} [cyan]{Markup.Escape(candidate.Category)}[/]: {Markup.Escape(candidate.Text)}");
            }
        }

        lines.Add("");
        lines.Add("[bold]Открытые correction slots:[/]");
        if (manifest.OpenCorrectionSlots.Count == 0)
        {
            lines.Add("[dim]Не сгенерированы.[/]");
        }
        else
        {
            foreach (var slot in manifest.OpenCorrectionSlots.Take(12))
                lines.Add($"  • {Markup.Escape(slot.SlotType)} [dim](max: {Markup.Escape(slot.MaxSeverity)})[/]");
            if (manifest.OpenCorrectionSlots.Count > 12)
                lines.Add($"  [dim]… и ещё {manifest.OpenCorrectionSlots.Count - 12}[/]");
        }

        Write(new Panel(GameInterface.SafeMarkup(string.Join("\n", lines)))
        {
            Header = new PanelHeader(" 🧩 Сценарное ядро ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Magenta1),
            Padding = new Padding(2, 1),
            Expand = true
        });
        WaitForKey();
    }

    private async Task ConfirmScenarioCoreCandidatesAsync()
    {
        if (_scenarioCoreService == null)
        {
            ShowEmptyPanel("Подтверждение фактов", "Сервис сценарного ядра недоступен.");
            return;
        }

        while (true)
        {
            var manifest = await _scenarioCoreService.ReadAsync();
            if (manifest == null)
            {
                ShowEmptyPanel("Подтверждение фактов", "Сначала задайте подготовку следующего мира.");
                return;
            }

            if (manifest.CandidateAssertions.Count == 0)
            {
                ShowEmptyPanel("Подтверждение фактов", "Все извлечённые факты уже подтверждены или пока не извлечены.");
                return;
            }

            var confirmedCandidateIds = manifest.ScenarioCoreAssertions
                .Where(assertion => !string.IsNullOrWhiteSpace(assertion.CandidateId))
                .Select(assertion => assertion.CandidateId!)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var choices = manifest.CandidateAssertions
                .Select(candidate =>
                {
                    var marker = confirmedCandidateIds.Contains(candidate.CandidateId) ? "✅" : "⬜";
                    return $"{marker} [{candidate.Category}] {candidate.Text}";
                })
                .Append("← Назад")
                .ToList();

            var selected = Prompt(
                new SelectionPrompt<string>()
                    .Title("[magenta]Извлечённые факты: выберите пункт, чтобы подтвердить или снять подтверждение[/]")
                    .HighlightStyle(new Style(Color.Magenta1))
                    .PageSize(12)
                    .AddChoices(choices));

            if (selected == "← Назад")
                return;

            var index = choices.IndexOf(selected);
            if (index < 0 || index >= manifest.CandidateAssertions.Count)
                continue;

            var candidate = manifest.CandidateAssertions[index];
            var nextState = !confirmedCandidateIds.Contains(candidate.CandidateId);
            await _scenarioCoreService.SetCandidateConfirmedAsync(candidate.CandidateId, nextState);
            MarkupLine(nextState
                ? $"[green]Факт подтверждён:[/] {Markup.Escape(candidate.Text)}"
                : $"[yellow]Подтверждение снято:[/] {Markup.Escape(candidate.Text)}");
            WaitForKey();
        }
    }

    private async Task ShowSystemGuardianLibrary()
    {
        if (_systemGuardianLibraryService == null)
        {
            ShowEmptyPanel("Извечные хранители", "Библиотека извечных хранителей недоступна");
            return;
        }

        var presets = await _systemGuardianLibraryService.GetAvailablePresetsAsync(includeDossier: true);
        if (presets.Count == 0)
        {
            ShowEmptyPanel("Извечные хранители", $"В библиотеке пока нет пресетов.\n\nПапка: {_systemGuardianLibraryService.GetUserDirectoryPath()}");
            return;
        }

        RenderSystemGuardianPresetOverview(presets);

        while (true)
        {
            var presetChoices = presets
                .Select(preset => (
                    Label: $"🛡 {BuildSystemGuardianPresetChoiceLabel(preset)}",
                    Identity: preset.PresetId,
                    Preset: preset))
                .ToList();
            var choices = MakeUniqueChoiceLabels(presetChoices
                .Select(choice => (choice.Label, choice.Identity))
                .ToList());
            choices.Add("📂 Открыть папку пользовательских извечных хранителей");
            choices.Add("← Назад");

            var choice = Prompt(new SelectionPrompt<string>()
                .Title($"[bold cyan]🛡️ Извечные хранители[/]\n[dim]Постоянные именованные хранители, доступные всегда. Внутри файлов эта библиотека технически называется system guardians.[/]\n[dim]Встроенные: {presets.Count(p => p.LibraryKind == "built_in")} • Пользовательские: {presets.Count(p => p.LibraryKind == "user")}[/]")
                .HighlightStyle(new Style(Color.Cyan1))
                .PageSize(14)
                .AddChoices(choices));

            if (choice.StartsWith("←", StringComparison.Ordinal))
                return;

            if (choice.StartsWith("📂", StringComparison.Ordinal))
            {
                OpenFolderOrPrintPath(_systemGuardianLibraryService.GetUserDirectoryPath());
                continue;
            }

            var selectedIndex = choices.IndexOf(choice);
            if (selectedIndex >= 0 && selectedIndex < presetChoices.Count)
                ShowSystemGuardianPresetDetail(presetChoices[selectedIndex].Preset);
        }
    }

    private void RenderSystemGuardianPresetOverview(IReadOnlyList<SystemGuardianLibraryService.SystemGuardianPresetDescriptor> presets)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .BorderColor(Color.Magenta1)
            .AddColumn("[bold cyan]Хранитель[/]")
            .AddColumn("[bold cyan]Темы[/]")
            .AddColumn("[bold cyan]Обитель[/]")
            .AddColumn("[bold cyan]Кратко[/]");

        foreach (var preset in presets)
        {
            var displayName = string.IsNullOrWhiteSpace(preset.DisplayName) ? preset.PresetId : preset.DisplayName;
            var abodeName = string.IsNullOrWhiteSpace(preset.AbodeName) ? "обитель не указана" : preset.AbodeName;
            var summary = string.IsNullOrWhiteSpace(preset.Summary) ? "сводка не указана" : preset.Summary;

            table.AddRow(
                Markup.Escape(displayName),
                Markup.Escape(BuildSystemGuardianPlayerFacingThemes(preset)),
                Markup.Escape(abodeName),
                Markup.Escape(summary));
        }

        WrapInPanel(table, "Обзор извечных Хранителей", Color.Magenta1);
    }

    private void WrapInPanel(Table table, string title, Color color)
    {
        WrapInPanel((IRenderable)table, title, color);
    }

    private Task ShowGallery()
    {
        if (_imageService == null)
        {
            MarkupLine($"[yellow]{Markup.Escape(_loc.T("image_service_unavailable"))}[/]");
            WaitForKey();
            return Task.CompletedTask;
        }

        var choices = new List<string>
        {
            "🎬 Сцены (ежеходные)",
            "👤 Персонажи (NPC)",
            "📦 Предметы",
            "📍 Локации",
            "🏛️ Фракции",
            "🛡️ Хранители",
            "🏛 Обители",
            "🎭 Игрок",
            "📜 Квесты",
            "🚗 Транспорт",
            "📂 Открыть всю папку изображений",
            "← Назад"
        };

        var choice = Prompt(
            new SelectionPrompt<string>()
                .Title("[bold purple]🖼 Галерея изображений[/]")
                .HighlightStyle(new Style(Color.Purple))
                .AddChoices(choices));

        if (choice.Contains("Назад")) return Task.CompletedTask;
        if (choice.Contains("всю папку")) { _imageService.OpenImagesFolder(); return Task.CompletedTask; }

        var entityType = choice switch
        {
            _ when choice.Contains("Сцены") => "scene",
            _ when choice.Contains("Персонажи") => "npc",
            _ when choice.Contains("Предметы") => "item",
            _ when choice.Contains("Локации") => "location",
            _ when choice.Contains("Фракции") => "faction",
            _ when choice.Contains("Хранители") => "guardian",
            _ when choice.Contains("Обители") => "abode",
            _ when choice.Contains("Игрок") => "player",
            _ when choice.Contains("Квесты") => "quest",
            _ when choice.Contains("Транспорт") => "vehicle",
            _ => "scene"
        };

        _imageService.OpenImagesFolder(entityType);
        return Task.CompletedTask;
    }

    private void WrapInPanel(IRenderable content, string title, Color color)
    {
        var panel = new Panel(content)
        {
            Header = new PanelHeader($" {title} ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(color),
            Expand = true
        };
        Write(panel);
    }

    private void WaitForKey()
    {
        WriteLine();
        MarkupLine("[grey]Нажмите любую клавишу...[/]");
        ReadKey();
    }

    /// <summary>
    /// After showing entity details, offer image actions for saved/generated entity images.
    /// </summary>
    private async Task RegenerateEntityImageAsync(string imagePrompt, string entityType, string entityKey)
    {
        if (_imageService == null)
            return;

        var autoShowAfterGenerate = !_imageService.GenerateWithoutDisplay;
        var generated = await _imageService.GenerateEntityImageAsync(imagePrompt, entityType, entityKey,
            displayAfterGenerate: autoShowAfterGenerate);
        if (!generated || !_imageService.GenerateWithoutDisplay)
            return;

        var showNow = Prompt(new ConfirmationPrompt(
            $"[bold]{Markup.Escape(_loc.T("image_regenerated_show_now"))}[/]")
        { DefaultValue = false });
        if (showNow)
            _imageService.ShowEntityImage(entityType, entityKey, forceDisplay: true);
    }

    private Task ExportEntityImageAsync(string entityType, string entityKey)
    {
        if (_imageService == null)
            return Task.CompletedTask;

        var targetPath = Ask("[cyan]Куда сохранить копию изображения? Укажите папку или полный путь файла:[/]", "").Trim();
        if (string.IsNullOrWhiteSpace(targetPath))
        {
            MarkupLine("[grey]Экспорт изображения отменён.[/]");
            return Task.CompletedTask;
        }

        var result = _imageService.ExportEntityImage(entityType, entityKey, targetPath);
        if (!result.Success && result.FailureReason == ImageExportFailureReason.DestinationExists)
        {
            var overwrite = Prompt(new ConfirmationPrompt(
                $"[yellow]Файл уже существует: {Markup.Escape(result.DestinationPath ?? targetPath)}. Перезаписать?[/]")
            { DefaultValue = false });
            if (overwrite)
                result = _imageService.ExportEntityImage(entityType, entityKey, targetPath, overwrite: true);
        }

        if (result.Success)
        {
            MarkupLine($"[green]Изображение сохранено: {Markup.Escape(result.DestinationPath ?? targetPath)}[/]");
        }
        else
        {
            MarkupLine($"[yellow]{Markup.Escape(result.ErrorMessage)}[/]");
        }

        return Task.CompletedTask;
    }

    private async Task WaitForKeyWithImage(string entityType, string entityName, string imagePrompt, string? entityKey = null)
    {
        if (_imageService == null)
        {
            WaitForKey();
            return;
        }

        var effectiveKey = string.IsNullOrWhiteSpace(entityKey) ? entityName : entityKey;
        var hasPrompt = !string.IsNullOrWhiteSpace(imagePrompt);
        if (!hasPrompt && !_imageService.EntityImageExists(entityType, effectiveKey))
        {
            WaitForKey();
            return;
        }

        while (true)
        {
            var hasImage = _imageService.EntityImageExists(entityType, effectiveKey);
            var choices = new List<string>();
            if (hasImage)
            {
                choices.Add("🖼 Показать сохранённое изображение");
                choices.Add("💾 Экспортировать изображение");
            }
            else if (hasPrompt)
            {
                choices.Add("🖼 Показать/создать изображение");
            }

            if (hasImage && hasPrompt)
                choices.Add("♻ Пересоздать изображение");
            choices.Add("← Назад");

            WriteLine();
            var action = Prompt(
                new SelectionPrompt<string>()
                    .Title("[bold]Действие:[/]")
                    .HighlightStyle(new Style(Color.Purple))
                    .AddChoices(choices));

            if (action.Contains("Назад"))
                return;

            if (action.Contains("Пересоздать"))
            {
                await RegenerateEntityImageAsync(imagePrompt, entityType, effectiveKey);
                WaitForKey();
                continue;
            }

            if (action.Contains("Экспортировать"))
            {
                await ExportEntityImageAsync(entityType, effectiveKey);
                WaitForKey();
                continue;
            }

            if (hasImage)
                _imageService.ShowEntityImage(entityType, effectiveKey, forceDisplay: true);
            else if (hasPrompt)
                await _imageService.ShowOrGenerateEntityImageAsync(imagePrompt, entityType, effectiveKey, forceDisplay: true);
            WaitForKey();
        }
    }

    private static string GetStr(JsonElement el, string prop, string def)
    {
        if (el.TryGetProperty(prop, out var val))
        {
            return val.ValueKind switch
            {
                JsonValueKind.String => val.GetString() ?? def,
                JsonValueKind.Number => val.ToString(),
                _ => val.GetRawText()
            };
        }
        return def;
    }

    private static List<JsonElement> CollectActorJournalEntryElements(JsonDocument? doc, string actorIdProperty, string actorId)
    {
        var result = new List<JsonElement>();
        if (doc == null || string.IsNullOrWhiteSpace(actorId))
            return result;

        if (doc.RootElement.ValueKind != JsonValueKind.Object ||
            !doc.RootElement.TryGetProperty(ActorJournalState.EntriesProperty, out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            if (!string.Equals(GetStr(entry, actorIdProperty, ""), actorId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(entry.Clone());
        }

        return result
            .OrderByDescending(entry => GetInt(entry, "turn", GetInt(entry, "turnNumber", 0)))
            .ThenByDescending(entry =>
            {
                var timestamp = GetStr(entry, "timestamp", "");
                if (!string.IsNullOrWhiteSpace(timestamp))
                    return timestamp;

                timestamp = GetStr(entry, "revealedAtUtc", "");
                if (!string.IsNullOrWhiteSpace(timestamp))
                    return timestamp;

                timestamp = GetStr(entry, "resolvedAtUtc", "");
                if (!string.IsNullOrWhiteSpace(timestamp))
                    return timestamp;

                return GetStr(entry, "appliedAt", "");
            }, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string BuildActorJournalLine(JsonElement entry)
    {
        var turn = GetInt(entry, "turn", GetInt(entry, "turnNumber", 0));
        var eventType = GetStr(entry, "eventType", "");
        var title = GetStr(entry, "title", GetStr(entry, "name", GetStr(entry, "entryId", "")));
        var summary = GetStr(entry, "summary", GetStr(entry, "description", GetStr(entry, "content", "")));

        var prefix = turn > 0 ? $"t{turn}: " : string.Empty;
        if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(summary))
            return $"{prefix}{title} — {summary}";
        if (!string.IsNullOrWhiteSpace(summary))
            return $"{prefix}{summary}";
        if (!string.IsNullOrWhiteSpace(title))
            return $"{prefix}{title}";

        return $"{prefix}{DescribeActorJournalEventType(eventType)}";
    }

    private static string DescribeActorJournalEventType(string eventType)
    {
        return (eventType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "started" => "Начало",
            "completed" => "Завершение",
            "pressured" => "Давление",
            "talk" or "conversation" => "Разговор",
            "lesson" => "Наставление",
            "trade" => "Обмен",
            "abode_devotion_shift" => "Сдвиг преданности Обители",
            "succeeded" => "Успех",
            "relic_grant" => "Дар реликвии",
            "assisted" => "Помощь",
            _ => string.Empty
        };
    }

    private static string GetRarityColor(string rarity) => rarity.ToLower() switch
    {
        "common" or "обычный" => "white",
        "good" or "хороший" => "cyan",
        "uncommon" or "необычный" => "green",
        "rare" or "редкий" => "blue",
        "epic" or "эпический" => "purple",
        "legendary" or "легендарный" => "yellow",
        "unique" or "уникальный" => "orange1",
        _ => "grey"
    };

    private static int GetRarityRank(string rarity) => rarity.ToLowerInvariant() switch
    {
        "common" or "обычный" => 1,
        "good" or "хороший" => 2,
        "uncommon" or "необычный" => 3,
        "rare" or "редкий" => 4,
        "epic" or "эпический" => 5,
        "legendary" or "легендарный" => 6,
        "unique" or "уникальный" => 7,
        _ => 1
    };

    private static string DescribeRarityLabel(string? rarity) =>
        (rarity ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "common" => "обычная",
            "good" => "хорошая",
            "uncommon" => "необычная",
            "rare" => "редкая",
            "epic" => "эпическая",
            "legendary" => "легендарная",
            "unique" => "уникальная",
            "обычный" or "хороший" or "необычный" or "редкий" or "эпический" or "легендарный" or "уникальный" => rarity ?? string.Empty,
            _ => string.IsNullOrWhiteSpace(rarity) ? string.Empty : rarity!
        };

    private static string Truncate(string text, int maxLen) =>
        text.Length <= maxLen ? text : text[..(maxLen - 3)] + "...";

    private static string FormatCharacteristicArray(JsonElement root, string propName)
    {
        if (!root.TryGetProperty(propName, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return "";

        var values = new List<string>();
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var key = item.GetString() ?? "";
            if (string.IsNullOrWhiteSpace(key)) continue;
            values.Add(Characteristics.RussianNames.GetValueOrDefault(key, key));
        }

        return values.Count == 0
            ? ""
            : Markup.Escape(string.Join(", ", values));
    }

    private static void EnumerateFactionCoreEntries(JsonElement root, Action<JsonElement> action)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                action(item);
            return;
        }

        if (root.ValueKind != JsonValueKind.Object)
            return;

        if (root.TryGetProperty("factionDataChanges", out var factionChanges) && factionChanges.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in factionChanges.EnumerateArray())
                action(item);
            return;
        }

        if (root.TryGetProperty("factions", out var factions) && factions.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in factions.EnumerateArray())
                action(item);
            return;
        }

        if (root.TryGetProperty("factionId", out _) || root.TryGetProperty("name", out _))
            action(root);
    }

    private static JsonElement GetCurrentLocationRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("currentLocationData", out var locationData) &&
            locationData.ValueKind == JsonValueKind.Object)
            return locationData;

        return root;
    }

    private static JsonElement GetWeatherRoot(JsonElement root)
    {
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("weatherChange", out var weatherChange) &&
            weatherChange.ValueKind == JsonValueKind.Object)
            return weatherChange;

        return root;
    }

    private static void AppendWorldTimeLines(List<string> lines, JsonElement root, string indent)
    {
        if (TryFormatAbsoluteWorldTime(root, out var absolute))
        {
            lines.Add($"{indent}[bold white]🕐 Время:[/]");
            lines.Add($"{indent}  {Markup.Escape(absolute)}");
            return;
        }

        if (root.TryGetProperty("setWorldTime", out var setWorldTime) &&
            TryFormatAbsoluteWorldTime(setWorldTime, out absolute))
        {
            lines.Add($"{indent}[bold white]🕐 Время:[/]");
            lines.Add($"{indent}  {Markup.Escape(absolute)}");
            return;
        }

        if (TryGetIntLike(root, "timeChange", out var deltaMinutes) && deltaMinutes != 0)
        {
            lines.Add($"{indent}[bold white]🕐 Время:[/]");
            lines.Add($"{indent}  Прошло [white]{deltaMinutes}[/] мин. за ход");
        }
    }

    private static bool TryFormatAbsoluteWorldTime(JsonElement source, out string formatted)
    {
        formatted = "";
        if (source.ValueKind != JsonValueKind.Object)
            return false;

        var year = GetStr(source, "year", "");
        var month = GetStr(source, "monthName", "");
        var day = GetStr(source, "dayOfMonth", "");
        var tod = GetStr(source, "timeOfDay", "");

        if (string.IsNullOrWhiteSpace(year) &&
            string.IsNullOrWhiteSpace(month) &&
            string.IsNullOrWhiteSpace(day) &&
            string.IsNullOrWhiteSpace(tod))
            return false;

        var datePart = string.Join(" ", new[] { day, month, year }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        formatted = !string.IsNullOrWhiteSpace(datePart) && !string.IsNullOrWhiteSpace(tod)
            ? $"{datePart}, {tod}"
            : (!string.IsNullOrWhiteSpace(datePart) ? datePart : tod);
        return !string.IsNullOrWhiteSpace(formatted);
    }

    private static bool TryGetIntLike(JsonElement root, string propName, out int value)
    {
        value = 0;
        if (!root.TryGetProperty(propName, out var field))
            return false;

        if (field.ValueKind == JsonValueKind.Number)
            return field.TryGetInt32(out value);

        return field.ValueKind == JsonValueKind.String &&
               int.TryParse(field.GetString(), out value);
    }

    private static void AppendStatusEffectPreview(List<string> lines, JsonElement root)
    {
        lines.Add("[bold yellow]⚡ Активные эффекты:[/]");
        var hasEffects = false;
        EnumerateJsonItems(root, item =>
        {
            if (item.ValueKind != JsonValueKind.Object) return;
            hasEffects = true;
            var effectType = GetStr(item, "effectType", "?");
            var value = GetStr(item, "value", "");
            var duration = GetStr(item, "duration", "");
            var source = GetStr(item, "sourceSkill", GetStr(item, "source", ""));
            var target = GetStr(item, "targetTypeDisplayName", GetStr(item, "targetType", ""));
            var description = GetStr(item, "effectDescription", GetStr(item, "description", ""));
            var color = effectType.ToLowerInvariant() switch
            {
                "buff" or "heal" or "healovertime" => "green",
                "debuff" or "damage" or "damageovertime" or "control" => "red",
                "damagereduction" => "cyan",
                _ => "yellow"
            };

            var line = $"  [{color}]• {Markup.Escape(effectType)}[/]";
            if (!string.IsNullOrEmpty(value))
                line += $" [white]{Markup.Escape(value)}[/]";
            if (!string.IsNullOrEmpty(target))
                line += $" → {Markup.Escape(target)}";
            if (!string.IsNullOrEmpty(duration) && duration != "0")
                line += $" [dim]({Markup.Escape(duration)} ход.)[/]";
            lines.Add(line);

            if (!string.IsNullOrEmpty(source))
                lines.Add($"    [dim]Источник: {Markup.Escape(source)}[/]");
            if (!string.IsNullOrEmpty(description))
                lines.Add($"    [dim]{Markup.Escape(description)}[/]");
        });

        if (!hasEffects)
            lines.Add("  [dim]Нет активных эффектов[/]");
    }

    private static void AppendStatusWoundPreview(List<string> lines, JsonElement root)
    {
        lines.Add("[bold red]🩸 Раны:[/]");
        var hasWounds = false;
        EnumerateJsonItems(root, item =>
        {
            if (item.ValueKind != JsonValueKind.Object) return;
            hasWounds = true;
            var woundName = GetStr(item, "woundName", "Рана");
            var severity = GetStr(item, "severity", "?");
            var description = GetStr(item, "descriptionOfEffects", GetStr(item, "description", ""));
            var severityColor = severity.ToLowerInvariant() switch
            {
                "light" => "yellow",
                "moderate" => "orange1",
                "serious" => "red",
                "critical" => "red bold",
                _ => "white"
            };

            lines.Add($"  [{severityColor}]• {Markup.Escape(woundName)} ({Markup.Escape(severity)})[/]");
            if (!string.IsNullOrEmpty(description))
                lines.Add($"    [dim]{Markup.Escape(description)}[/]");

            if (item.TryGetProperty("healingState", out var healingState) && healingState.ValueKind == JsonValueKind.Object)
            {
                var state = GetStr(healingState, "currentState", "");
                var progress = GetStr(healingState, "treatmentProgress", "0");
                var needed = GetStr(healingState, "progressNeeded", "?");
                if (!string.IsNullOrEmpty(state))
                    lines.Add($"    [cyan]Лечение:[/] {Markup.Escape(state)} ({Markup.Escape(progress)}/{Markup.Escape(needed)})");
            }
        });

        if (!hasWounds)
            lines.Add("  [dim green]Ран нет[/]");
    }

    private static void AppendStatusCustomStatePreview(List<string> lines, JsonElement root)
    {
        lines.Add("[bold magenta]📊 Особые состояния:[/]");
        var beforeCount = lines.Count;

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
                RenderCustomStateItem(lines, item, "  ");
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            var renderedFromArray = false;
            foreach (var prop in root.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                renderedFromArray = true;
                foreach (var item in prop.Value.EnumerateArray())
                    RenderCustomStateItem(lines, item, "  ");
            }

            if (!renderedFromArray)
                RenderCustomStateItem(lines, root, "  ");
        }

        if (lines.Count == beforeCount)
            lines.Add("  [dim]Нет особых состояний[/]");
    }

    private static void EnumerateArray(JsonElement root, string propName, Action<JsonElement> action)
    {
        if (root.TryGetProperty(propName, out var arr) && arr.ValueKind == JsonValueKind.Array)
            foreach (var item in arr.EnumerateArray())
                action(item);
    }

    private static void EnumerateJsonItems(JsonElement root, Action<JsonElement> action)
    {
        if (root.ValueKind == JsonValueKind.Array)
            foreach (var item in root.EnumerateArray())
                action(item);
        else if (root.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in root.EnumerateObject())
                if (prop.Value.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in prop.Value.EnumerateArray())
                        action(item);
                }
        }
    }

    private async Task ShowValidation()
    {
        if (_validator == null)
        {
            MarkupLine("[yellow]Сервис валидации недоступен[/]");
            WaitForKey();
            return;
        }

        MarkupLine("[dim]Проверка целостности игровых файлов...[/]");
        var issues = await _validator.ValidateGameStateAsync();

        if (issues.Count == 0)
        {
            var okPanel = new Panel(new Markup("[green bold]✅ Все проверки пройдены! Файлы в порядке.[/]"))
            {
                Header = new PanelHeader(" 🔍 Валидация ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Green),
                Padding = new Padding(2, 1)
            };
            Write(okPanel);
        }
        else
        {
            var summary = issues
                .GroupBy(issue => new
                {
                    issue.Category,
                    Section = string.IsNullOrWhiteSpace(issue.Section) ? "General" : issue.Section
                })
                .OrderByDescending(group => group.Count())
                .ThenBy(group => group.Key.Category.ToString(), StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.Key.Section, StringComparer.OrdinalIgnoreCase)
                .Take(6)
                .Select(group => $"{FormatValidationCategory(group.Key.Category)} / {group.Key.Section}: {group.Count()}")
                .ToList();

            if (summary.Count > 0)
            {
                var summaryPanel = new Panel(GameInterface.SafeMarkup(string.Join("\n", summary.Select(item => $"[yellow]• {Markup.Escape(item)}[/]"))))
                {
                    Header = new PanelHeader(" 🧭 Сводка ", Justify.Center),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Yellow),
                    Padding = new Padding(1, 0),
                    Expand = true
                };
                Write(summaryPanel);
                WriteLine();
            }

            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Yellow)
                .AddColumn(new TableColumn("[bold]Уровень[/]").Centered())
                .AddColumn(new TableColumn("[bold]Категория[/]"))
                .AddColumn(new TableColumn("[bold]Проблема[/]"))
                .AddColumn(new TableColumn("[bold]Подсказка[/]"));

            foreach (var issue in issues)
            {
                var severityColor = issue.Severity switch
                {
                    Services.IssueSeverity.Error => "red",
                    Services.IssueSeverity.Warning => "yellow",
                    _ => "dim"
                };
                var icon = issue.Severity switch
                {
                    Services.IssueSeverity.Error => "❌",
                    Services.IssueSeverity.Warning => "⚠️",
                    _ => "ℹ️"
                };
                table.AddRow(
                    $"[{severityColor}]{icon} {issue.Severity}[/]",
                    $"[bold]{Markup.Escape(FormatValidationCategory(issue.Category))}[/]\n[dim]{Markup.Escape(issue.Section ?? "General")}[/]",
                    $"[white]{Markup.Escape(issue.Message)}[/]\n[dim]{Markup.Escape(issue.FilePath)}[/]",
                    string.IsNullOrWhiteSpace(issue.RepairHint)
                        ? "[dim]—[/]"
                        : $"[grey]{Markup.Escape(issue.RepairHint)}[/]");
            }

            var panel = new Panel(table)
            {
                Header = new PanelHeader($" 🔍 Валидация ({issues.Count} проблем) ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(issues.Any(i => i.Severity == Services.IssueSeverity.Error) ? Color.Red : Color.Yellow),
                Padding = new Padding(1, 0)
            };
            Write(panel);
        }

        WaitForKey();
    }

    private static string FormatValidationCategory(Services.IssueCategory category) => category switch
    {
        Services.IssueCategory.ProtocolViolation => "Протокол",
        Services.IssueCategory.ClientOwnedSurface => "Системный файл клиента",
        _ => "Согласованность состояния"
    };

    private async Task ShowLivesHistory()
    {
        var doc = await _stateManager.LoadGameStateFileAsync("game_state/meta/soul_state.json");
        if (doc == null)
        {
            ShowEmptyPanel("История жизней", "Нет данных о прошлых жизнях");
            return;
        }

        var root = doc.RootElement;
        if (!root.TryGetProperty("livesHistory", out var lives) ||
            lives.ValueKind != JsonValueKind.Array || lives.GetArrayLength() == 0)
        {
            var emptyPanel = new Panel(new Markup("[dim italic]Эта душа ещё не прожила ни одной смертной жизни.\n" +
                "Воплотитесь через Врата Души, чтобы начать первую жизнь.[/]"))
            {
                Header = new PanelHeader(" 📜 История жизней ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Blue),
                Padding = new Padding(2, 1)
            };
            Write(emptyPanel);
            WaitForKey();
            return;
        }

        var tree = new Tree("[bold blue]📜 История прожитых жизней[/]");

        var lifeIndex = 0;
        foreach (var life in lives.EnumerateArray())
        {
            static string GetLifeScalar(JsonElement lifeEntry, params string[] propertyNames)
            {
                if (lifeEntry.ValueKind != JsonValueKind.Object)
                    return "";

                foreach (var propertyName in propertyNames)
                {
                    if (!lifeEntry.TryGetProperty(propertyName, out var value))
                        continue;

                    return value.ValueKind switch
                    {
                        JsonValueKind.String => value.GetString() ?? "",
                        JsonValueKind.Number => value.ToString(),
                        JsonValueKind.True => "да",
                        JsonValueKind.False => "нет",
                        _ => ""
                    };
                }

                return "";
            }

            static List<string> ReadLifeStringArray(JsonElement lifeEntry, params string[] propertyNames)
            {
                if (lifeEntry.ValueKind != JsonValueKind.Object)
                    return new List<string>();

                foreach (var propertyName in propertyNames)
                {
                    if (!lifeEntry.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
                        continue;

                    return value.EnumerateArray()
                        .Select(item => item.ValueKind switch
                        {
                            JsonValueKind.String => item.GetString() ?? "",
                            JsonValueKind.Number => item.ToString(),
                            _ => ""
                        })
                        .Where(item => !string.IsNullOrWhiteSpace(item))
                        .ToList();
                }

                return new List<string>();
            }

            static List<string> ReadLifeObjectArraySummaries(JsonElement lifeEntry, string propertyName)
            {
                if (lifeEntry.ValueKind != JsonValueKind.Object)
                    return new List<string>();

                if (!lifeEntry.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
                    return new List<string>();

                var result = new List<string>();
                foreach (var item in value.EnumerateArray())
                {
                    if (item.ValueKind == JsonValueKind.String)
                    {
                        var raw = item.GetString() ?? string.Empty;
                        if (!string.IsNullOrWhiteSpace(raw))
                            result.Add(raw);
                        continue;
                    }

                    if (item.ValueKind != JsonValueKind.Object)
                        continue;

                    var parts = new List<string>();
                    var primary = GetStr(item, "name",
                        GetStr(item, "title",
                            GetStr(item, "person",
                                GetStr(item, "partner",
                                    GetStr(item, "choice",
                                        GetStr(item, "decision",
                                            GetStr(item, "label", "")))))));
                    var secondary = GetStr(item, "relationshipType",
                        GetStr(item, "relationType",
                            GetStr(item, "type",
                                GetStr(item, "alignment",
                                    GetStr(item, "stance", "")))));
                    var summary = GetStr(item, "summary",
                        GetStr(item, "description",
                            GetStr(item, "consequence",
                                GetStr(item, "outcome",
                                    GetStr(item, "bond", "")))));

                    if (!string.IsNullOrWhiteSpace(primary))
                        parts.Add(primary);
                    if (!string.IsNullOrWhiteSpace(secondary))
                        parts.Add(secondary);
                    if (!string.IsNullOrWhiteSpace(summary))
                        parts.Add(summary);

                    var rendered = parts.Count > 0
                        ? string.Join(" — ", parts)
                        : item.GetRawText();
                    if (!string.IsNullOrWhiteSpace(rendered))
                        result.Add(rendered);
                }

                return result;
            }

            lifeIndex++;
            var incarnation = life.TryGetProperty("incarnation", out var inc) ? inc.ToString() : lifeIndex.ToString();
            var lifeRecord = life.TryGetProperty("recordLifeCompletion", out var lifeRecordNode) &&
                             lifeRecordNode.ValueKind == JsonValueKind.Object
                ? lifeRecordNode
                : default;
            var characterFinalState = lifeRecord.ValueKind == JsonValueKind.Object &&
                                      lifeRecord.TryGetProperty("characterFinalState", out var finalStateNode) &&
                                      finalStateNode.ValueKind == JsonValueKind.Object
                ? finalStateNode
                : default;

            var summary = GetStr(life, "summary", "");
            var endedAt = GetStr(life, "endedAt", GetStr(life, "completionDate", ""));
            var turnsLived = GetStr(life, "turnsLived", "?");

            var charName = GetStr(life, "characterName",
                GetLifeScalar(characterFinalState, "characterName", "name"));
            var worldName = GetStr(life, "world",
                GetStr(life, "worldName",
                    GetLifeScalar(characterFinalState, "world", "worldName")));
            var finalLevel = GetStr(life, "finalLevel",
                GetLifeScalar(characterFinalState, "finalLevel", "level"));
            var questsCompleted = GetStr(life, "questsCompleted", "");
            var deathReason = GetStr(life, "deathReason",
                GetLifeScalar(characterFinalState, "deathReason", "causeOfDeath"));
            var worldGenre = GetLifeScalar(life, "worldGenre");
            var totalSoulQuests = GetLifeScalar(life, "totalSoulQuests", "soulQuestsCompleted");
            var feathersEarned = GetLifeScalar(life, "feathersEarned");
            var gmCoefficient = GetLifeScalar(life, "gmCoefficient");
            var enlightenmentTierReached = GetLifeScalar(life, "enlightenmentTierReached");
            var alignmentAtDeath = GetLifeScalar(life, "alignmentAtDeath", "finalAlignment");
            if (string.IsNullOrWhiteSpace(alignmentAtDeath))
                alignmentAtDeath = GetLifeScalar(characterFinalState, "alignmentAtDeath", "finalAlignment", "alignment");
            var worldImpactLevel = GetLifeScalar(life, "worldImpactLevel");
            var moralChoicesRecord = GetLifeScalar(life, "moralChoicesRecord");
            var incarnationStartDate = GetLifeScalar(life, "incarnationStartDate", "startedAt");
            var incarnationDuration = GetLifeScalar(life, "incarnationDuration", "duration");
            var notableAchievements = ReadLifeStringArray(life, "notableAchievements");
            var npcSoulImprints = ReadLifeStringArray(life, "npcSoulImprints");
            var majorAchievements = lifeRecord.ValueKind == JsonValueKind.Object
                ? ReadLifeStringArray(lifeRecord, "majorAchievements")
                : new List<string>();
            var relationshipsFormed = lifeRecord.ValueKind == JsonValueKind.Object
                ? ReadLifeObjectArraySummaries(lifeRecord, "relationshipsFormed")
                : new List<string>();
            var moralChoices = lifeRecord.ValueKind == JsonValueKind.Object
                ? ReadLifeObjectArraySummaries(lifeRecord, "moralChoices")
                : new List<string>();
            var skillsLearned = lifeRecord.ValueKind == JsonValueKind.Object
                ? ReadLifeStringArray(lifeRecord, "skillsLearned")
                : new List<string>();
            var enlightenmentGained = lifeRecord.ValueKind == JsonValueKind.Object
                ? GetLifeScalar(lifeRecord, "enlightenmentGained")
                : string.Empty;

            if (string.IsNullOrWhiteSpace(summary))
            {
                summary = majorAchievements.Count > 0
                    ? $"Канонический итог: {majorAchievements[0]}"
                    : !string.IsNullOrWhiteSpace(deathReason)
                        ? $"Канонический итог: {deathReason}"
                        : "Каноническая запись завершённой жизни.";
            }

            var titleParts = new List<string> { $"[bold cyan]Жизнь #{Markup.Escape(incarnation)}[/]" };
            if (!string.IsNullOrEmpty(charName)) titleParts.Add($"[white]{Markup.Escape(charName)}[/]");
            if (!string.IsNullOrEmpty(worldName)) titleParts.Add($"[dim]🌍 {Markup.Escape(worldName)}[/]");
            titleParts.Add($"[dim]({Markup.Escape(turnsLived)} ходов)[/]");

            var lifeNode = tree.AddNode(string.Join("  ", titleParts));
            lifeNode.AddNode($"[white]{Markup.Escape(summary)}[/]");

            var detailParts = new List<string>();
            if (!string.IsNullOrEmpty(finalLevel)) detailParts.Add($"Ур. {Markup.Escape(finalLevel)}");
            if (!string.IsNullOrEmpty(questsCompleted) && questsCompleted != "0") detailParts.Add($"Квестов: {Markup.Escape(questsCompleted)}");
            if (!string.IsNullOrEmpty(deathReason)) detailParts.Add($"Причина: {Markup.Escape(deathReason)}");
            if (detailParts.Count > 0)
                lifeNode.AddNode($"[dim]{string.Join(" │ ", detailParts)}[/]");

            var metaParts = new List<string>();
            if (!string.IsNullOrEmpty(worldGenre)) metaParts.Add($"Жанр: {Markup.Escape(worldGenre)}");
            if (!string.IsNullOrEmpty(totalSoulQuests) && totalSoulQuests != "0") metaParts.Add($"Квестов души: {Markup.Escape(totalSoulQuests)}");
            if (!string.IsNullOrEmpty(feathersEarned)) metaParts.Add($"Перьев: {Markup.Escape(feathersEarned)}");
            if (!string.IsNullOrEmpty(gmCoefficient)) metaParts.Add($"GM-коэфф.: {Markup.Escape(gmCoefficient)}");
            if (!string.IsNullOrEmpty(enlightenmentTierReached)) metaParts.Add($"Тир просветления: {Markup.Escape(enlightenmentTierReached)}");
            if (!string.IsNullOrEmpty(alignmentAtDeath)) metaParts.Add($"Мировоззрение: {Markup.Escape(alignmentAtDeath)}");
            if (!string.IsNullOrEmpty(worldImpactLevel)) metaParts.Add($"Влияние на мир: {Markup.Escape(worldImpactLevel)}");
            if (metaParts.Count > 0)
                lifeNode.AddNode($"[dim]{string.Join(" │ ", metaParts)}[/]");

            if (life.TryGetProperty("achievements", out var achArr) && achArr.ValueKind == JsonValueKind.Array && achArr.GetArrayLength() > 0)
            {
                var achNames = new List<string>();
                foreach (var ach in achArr.EnumerateArray())
                    achNames.Add(ach.ValueKind == JsonValueKind.String ? (ach.GetString() ?? "") : GetStr(ach, "name", ach.GetRawText()));
                lifeNode.AddNode($"[yellow]🏆 {Markup.Escape(string.Join(", ", achNames))}[/]");
            }

            if (notableAchievements.Count > 0)
                lifeNode.AddNode($"[green]⭐ Значимые достижения: {Markup.Escape(string.Join(", ", notableAchievements))}[/]");
            if (majorAchievements.Count > 0)
                lifeNode.AddNode($"[green]🏔 Главные свершения: {Markup.Escape(string.Join(", ", majorAchievements))}[/]");

            if (!string.IsNullOrWhiteSpace(moralChoicesRecord))
                lifeNode.AddNode($"[italic]⚖ {Markup.Escape(moralChoicesRecord)}[/]");
            if (moralChoices.Count > 0)
                lifeNode.AddNode($"[italic]⚖ Канонические выборы: {Markup.Escape(string.Join(" • ", moralChoices))}[/]");
            if (relationshipsFormed.Count > 0)
                lifeNode.AddNode($"[cyan]🤝 Канонические связи: {Markup.Escape(string.Join(" • ", relationshipsFormed))}[/]");
            if (skillsLearned.Count > 0)
                lifeNode.AddNode($"[cyan]🛠 Освоенные навыки: {Markup.Escape(string.Join(", ", skillsLearned))}[/]");
            if (!string.IsNullOrWhiteSpace(enlightenmentGained))
                lifeNode.AddNode($"[yellow]✨ Просветление за жизнь: {Markup.Escape(enlightenmentGained)}[/]");

            if (npcSoulImprints.Count > 0)
                lifeNode.AddNode($"[mediumpurple2]👤 Слепки души: {Markup.Escape(string.Join(", ", npcSoulImprints))}[/]");

            var timelineParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(incarnationStartDate))
                timelineParts.Add($"Начало: {Markup.Escape(incarnationStartDate)}");
            if (!string.IsNullOrWhiteSpace(incarnationDuration))
                timelineParts.Add($"Длительность: {Markup.Escape(incarnationDuration)}");
            if (!string.IsNullOrEmpty(endedAt))
            {
                if (DateTime.TryParse(endedAt, out var dt))
                    timelineParts.Add($"Завершена: {dt:dd.MM.yyyy HH:mm}");
                else
                    timelineParts.Add($"Завершена: {Markup.Escape(endedAt)}");
            }

            if (timelineParts.Count > 0)
                lifeNode.AddNode($"[dim]{string.Join(" │ ", timelineParts)}[/]");
        }

        var panel = new Panel(tree)
        {
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(2, 1)
        };
        Write(panel);
        WaitForKey();
    }
}



