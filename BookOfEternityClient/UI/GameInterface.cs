using Spectre.Console;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models.GameState;
using BookOfEternityClient.Models;
using BookOfEternityClient.Configuration;
using System.Text.RegularExpressions;

namespace BookOfEternityClient.UI;

/// <summary>
/// Main game interface using Spectre.Console.
/// Renders the multi-panel game layout with rich formatting.
/// Realm-aware: afterlife realms use blue/gold theme, mortal life uses green/cyan theme.
/// </summary>
public class GameInterface
{
    private static readonly Regex AchievementUnlockMarkerRegex =
        new(@"\[ACHIEVEMENT_UNLOCK:\s*(.+?)\]", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly LocalizationManager _loc;
    private readonly GameSettings _settings;

    public GameInterface(LocalizationManager loc, GameSettings settings)
    {
        _loc = loc;
        _settings = settings;
    }

    public void RenderGameScreen(AggregatedGameState state, GameResponse? lastResponse, int turnNumber)
    {
        SpectreConsoleSafe.Clear();
        
        // Header (realm-aware with distinct visual theme)
        RenderHeader(state, turnNumber);
        
        // Narrative
        if (lastResponse?.Response != null || !string.IsNullOrEmpty(state.Narrative))
        {
            RenderNarrative(lastResponse?.Response ?? state.Narrative, state.IsInAfterlifeRealm);
        }

        // Combat log if present (mortal life only)
        if (!state.IsInAfterlifeRealm && !string.IsNullOrEmpty(lastResponse?.CombatLogMarkdown))
        {
            RenderCombatLog(lastResponse.CombatLogMarkdown);
        }
        
        // Status bars only in mortal life; afterlife realms use the soul-status block.
        if (ShouldRenderAfterlifeStatus(state))
        {
            RenderAfterlifeStatus(state);
        }
        else
        {
            RenderStatusBar(state.PlayerStatus);
        }

        // Dialogue options or suggested actions
        if (lastResponse?.DialogueOptions != null && lastResponse.DialogueOptions.Length > 0)
        {
            RenderDialogueOptions(lastResponse.DialogueOptions);
        }

        // GM thoughts (if enabled)
        if (_settings.ShowGmThoughts && !string.IsNullOrEmpty(lastResponse?.GmThoughtsMarkdown))
        {
            RenderGmThoughts(lastResponse.GmThoughtsMarkdown);
        }

        // Soul info line (always visible)
        RenderSoulInfo(state);
        
        // Separator
        var sepColor = state.IsInAfterlifeRealm ? "blue" : "green";
        AnsiConsole.Write(new Rule().RuleStyle(sepColor));
    }

    /// <summary>
    /// Renders the realm transition banner when switching between Chaos Sea and Mortal Life.
    /// </summary>
    public static void RenderRealmTransition(bool enteringChaosSea, IConsoleInputSource? inputSource = null)
    {
        var consoleInput = inputSource ?? SystemConsoleInputSource.Instance;
        SpectreConsoleSafe.Clear();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();

        if (enteringChaosSea)
        {
            AnsiConsole.Write(new FigletText("Chaos Sea")
                .Color(Color.SteelBlue1)
                .Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold blue]═══ 🌊 Возвращение в Море Хаоса 🌊 ═══[/]")
                .RuleStyle("blue"));
            AnsiConsole.WriteLine();

            var text = new Markup(
                "[steelblue1]  Смертная оболочка рассыпается как песок...\n" +
                "  Душа, невесомая и свободная, поднимается ввысь.\n\n" +
                "[/][blue]  Бесконечное море мерцающего хаоса расстилается вокруг.\n" +
                "  Хранитель ждёт — чтобы выслушать историю вашей жизни\n" +
                "  и помочь подготовиться к следующему странствию.[/]");

            AnsiConsole.Write(new Panel(text)
            {
                Border = BoxBorder.Heavy,
                BorderStyle = new Style(Color.Blue),
                Padding = new Padding(4, 1),
                Expand = true
            });
        }
        else
        {
            AnsiConsole.Write(new FigletText("Mortal Life")
                .Color(Color.Green3)
                .Centered());
            AnsiConsole.WriteLine();
            AnsiConsole.Write(new Rule("[bold green]═══ ⚔️ Воплощение в мир смертных ⚔️ ═══[/]")
                .RuleStyle("green"));
            AnsiConsole.WriteLine();

            var text = new Markup(
                "[green3]  Врата Души распахиваются ослепительным светом...\n" +
                "  Душа ныряет в поток перерождения.\n\n" +
                "[/][green]  Тьма. Первый вдох. Первое ощущение.\n" +
                "  Новая жизнь начинается.\n\n" +
                "[/][dim]  ⚠ Реликвии души действуют, но сменить их нельзя.\n" +
                "  ⚠ Хранители недоступны до конца жизни.[/]");

            AnsiConsole.Write(new Panel(text)
            {
                Border = BoxBorder.Heavy,
                BorderStyle = new Style(Color.Green3),
                Padding = new Padding(4, 1),
                Expand = true
            });
        }

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[grey dim]  Нажмите любую клавишу...[/]");
        if (consoleInput is AgentConsoleLiveInputSource liveInput)
        {
            var title = enteringChaosSea ? "Возвращение в Море Хаоса" : "Воплощение в мир смертных";
            var text = enteringChaosSea
                ? "Смертная оболочка рассыпается, душа возвращается в Море Хаоса. Продолжите, чтобы открыть посмертный экран."
                : "Врата Души распахиваются, начинается новая смертная жизнь. Продолжите, чтобы увидеть первый смертный ход.";
            var now = DateTimeOffset.UtcNow;
            liveInput.PublishSnapshot(new AgentConsoleSnapshot
            {
                ScreenId = enteringChaosSea ? "realm-transition-chaos-sea" : "realm-transition-mortal-life",
                Mode = AgentConsoleMode.TextPrompt,
                Title = title,
                PlainText = text,
                AwaitingInput = true,
                InputKind = AgentConsoleInputKind.Key,
                Actions =
                [
                    new AgentConsoleAction
                    {
                        Id = "continue",
                        Label = "Продолжить",
                        Shortcut = "Enter",
                        IsDefault = true
                    }
                ],
                Prompt = new AgentConsolePrompt
                {
                    PromptId = enteringChaosSea ? "realm-transition-chaos-sea:key" : "realm-transition-mortal-life:key",
                    Text = text,
                    InputKind = AgentConsoleInputKind.Key,
                    DefaultValue = "Enter"
                },
                RenderedAtUtc = now,
                UpdatedAtUtc = now
            }, $"Rendered {(enteringChaosSea ? "realm-transition-chaos-sea" : "realm-transition-mortal-life")}.");
        }

        consoleInput.ReadKey(intercept: true);
    }

    internal static bool ShouldRenderAfterlifeStatus(AggregatedGameState state)
    {
        return state.IsInAfterlifeRealm;
    }

    private void RenderHeader(AggregatedGameState state, int turnNumber)
    {
        var isAfterlife = state.IsInAfterlifeRealm;
        var isShiningAbode = state.IsInShiningAbode;
        var isPendingShiningAbodeBootstrap = state.IsInShiningAbodePendingBootstrap;
        var themeColor = isPendingShiningAbodeBootstrap
            ? Color.Khaki1
            : (isShiningAbode ? Color.Gold1 : (isAfterlife ? Color.Blue : Color.Green3));
        var accentColor = isPendingShiningAbodeBootstrap
            ? "khaki1"
            : (isShiningAbode ? "yellow" : (isAfterlife ? "blue" : "green3"));
        var dimAccent = isPendingShiningAbodeBootstrap
            ? "wheat1"
            : (isShiningAbode ? "khaki1" : (isAfterlife ? "steelblue1" : "darkseagreen"));

        // Realm banner line
        var realmIcon = isPendingShiningAbodeBootstrap ? "⏳" : (isShiningAbode ? "✨" : (isAfterlife ? "🌊" : "⚔️"));
        var realmName = isPendingShiningAbodeBootstrap
            ? "Сияющая Обитель: handoff"
            : (isShiningAbode ? _loc.T("realm_shining_abode") : (isAfterlife ? _loc.T("realm_chaos_sea") : _loc.T("realm_mortal")));
        var timeStr = string.IsNullOrEmpty(state.WorldTime) ? "" : $" 🕐 {EscapeMarkup(state.WorldTime)}";

        AnsiConsole.Write(new Rule($"[bold {accentColor}]{realmIcon} {realmName}[/]  [dim]Ход {turnNumber}{timeStr}[/]")
            .RuleStyle(accentColor));

        // Character identity line
        if (isAfterlife)
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(state.SoulName))
                parts.Add($"[bold white]👻 {EscapeMarkup(state.SoulName)}[/]");
            if (!string.IsNullOrEmpty(state.ActiveGuardianName))
                parts.Add($"[{dimAccent}]🛡️ Хранитель: {EscapeMarkup(state.ActiveGuardianName)}[/]");
            if (parts.Count > 0)
                AnsiConsole.Write(ConsoleLayout.CreateFactGrid(parts.ToArray()));
        }
        else
        {
            var parts = new List<string>();
            if (!string.IsNullOrEmpty(state.CharacterName))
                parts.Add($"[bold white]👤 {EscapeMarkup(state.CharacterName)}[/]");
            if (!string.IsNullOrEmpty(state.CharacterRace))
                parts.Add($"[{dimAccent}]{EscapeMarkup(state.CharacterRace)}[/]");
            if (!string.IsNullOrEmpty(state.CharacterClass))
                parts.Add($"[{dimAccent}]{EscapeMarkup(state.CharacterClass)}[/]");
            if (!string.IsNullOrEmpty(state.CurrentLocation))
                parts.Add($"[green]📍 {EscapeMarkup(state.CurrentLocation)}[/]");
            if (parts.Count > 0)
                AnsiConsole.Write(ConsoleLayout.CreateFactGrid(parts.ToArray()));
        }

        AnsiConsole.WriteLine();
    }

    private void RenderNarrative(string narrative, bool isAfterlife)
    {
        if (string.IsNullOrWhiteSpace(narrative))
        {
            AnsiConsole.MarkupLine("[dim italic]  (Ожидание ответа мастера...)[/]");
            return;
        }

        var unlocks = new List<string>();
        var cleanNarrative = AchievementUnlockMarkerRegex.Replace(narrative, match =>
        {
            var achievementName = match.Groups[1].Value.Trim();
            if (!string.IsNullOrWhiteSpace(achievementName))
                unlocks.Add(achievementName);
            return string.Empty;
        }).Trim();

        var borderColor = isAfterlife ? Color.Blue : Color.Green3;
        var borderStyle = isAfterlife ? BoxBorder.Heavy : BoxBorder.Rounded;
        var headerIcon = isAfterlife ? "🌊" : "📜";
        var textColor = isAfterlife ? "white" : "white";
        if (!string.IsNullOrWhiteSpace(cleanNarrative))
        {
            var escaped = EscapeMarkup(cleanNarrative);

            var panel = new Panel(
                new Markup($"[{textColor}]{escaped}[/]"))
            {
                Header = new PanelHeader($" {headerIcon} {_loc.T("narrative")} {headerIcon} ", Justify.Center),
                Border = borderStyle,
                BorderStyle = new Style(borderColor),
                Padding = new Padding(3, 1),
                Expand = true
            };

            AnsiConsole.Write(panel);
            AnsiConsole.WriteLine();
        }

        if (unlocks.Count > 0)
        {
            var body = string.Join("\n", unlocks
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(name => $"  🏆 [bold gold1]{EscapeMarkup(name)}[/]"));

            AnsiConsole.Write(new Panel(new Markup(body))
            {
                Header = new PanelHeader(" ✨ Новые достижения ", Justify.Center),
                Border = BoxBorder.Double,
                BorderStyle = new Style(Color.Gold1),
                Padding = new Padding(2, 1),
                Expand = true
            });
            AnsiConsole.WriteLine();
        }
    }

    public static void RenderAscensionTransition()
    {
        SpectreConsoleSafe.Clear();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new FigletText("Shining Abode")
            .Color(Color.Gold1)
            .Centered());
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]═══ ✨ Вознесение в Сияющую Обитель ✨ ═══[/]")
            .RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var text = new Markup(
            "[gold1]  Хаос остаётся далеко внизу, словно забытый сон.\n" +
            "  Душа поднимается туда, где нет распада и страха.\n\n" +
            "[/][yellow]  Перед вами раскрывается Сияющая Обитель.\n" +
            "  Здесь можно проводить время с Хранителями в свободном ролеплее\n" +
            "  и начать Новый Цикл, когда вы сами этого захотите.[/]");

        AnsiConsole.Write(new Panel(text)
        {
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(4, 1),
            Expand = true
        });
    }

    public static void RenderShiningAbodeReturnTransition()
    {
        SpectreConsoleSafe.Clear();
        AnsiConsole.WriteLine();
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new FigletText("Shining Abode")
            .Color(Color.Gold1)
            .Centered());
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold yellow]═══ ✨ Возвращение в Сияющую Обитель ✨ ═══[/]")
            .RuleStyle("yellow"));
        AnsiConsole.WriteLine();

        var text = new Markup(
            "[gold1]  Море Хаоса медленно отступает, словно туман за спиной.\n" +
            "  Душа снова находит путь к уже знакомому сиянию.\n\n" +
            "[/][yellow]  Перед вами вновь раскрывается Сияющая Обитель.\n" +
            "  Её залы пробуждаются без нового вознесения,\n" +
            "  и прежний ритм её сияния продолжается дальше.[/]");

        AnsiConsole.Write(new Panel(text)
        {
            Border = BoxBorder.Heavy,
            BorderStyle = new Style(Color.Yellow),
            Padding = new Padding(4, 1),
            Expand = true
        });
    }

    private void RenderCombatLog(string combatLog)
    {
        var panel = new Panel(
            new Markup(EscapeMarkup(combatLog)))
        {
            Header = new PanelHeader(" ⚔️ Боевой лог ", Justify.Center),
            Border = BoxBorder.Double,
            BorderStyle = new Style(Color.Red),
            Padding = new Padding(2, 1)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void RenderStatusBar(PlayerStatusState status)
    {
        var healthPct = ParsePercentage(status.HealthPercentage);
        var energyPct = ParsePercentage(status.EnergyPercentage);
        var poisePct = ParsePercentage(status.PoisePercentage);

        var healthColor = healthPct > 60 ? "green" : healthPct > 30 ? "yellow" : "red";
        var energyColor = energyPct > 60 ? "deepskyblue1" : energyPct > 30 ? "yellow" : "red";
        var poiseColor = poisePct > 60 ? "steelblue" : poisePct > 30 ? "yellow" : "red";

        var healthBar = ConsoleLayout.CreateBarFromPercent(healthPct, 22, healthColor);
        var energyBar = ConsoleLayout.CreateBarFromPercent(energyPct, 22, energyColor);
        var poiseBar = ConsoleLayout.CreateBarFromPercent(poisePct, 22, poiseColor);

        var conditionEmoji = status.CurrentCondition?.ToLower() switch
        {
            var c when c != null && (c.Contains("отлич") || c.Contains("хорош") || c.Contains("normal") || c.Contains("норм")) => "😊",
            var c when c != null && (c.Contains("ранен") || c.Contains("hurt") || c.Contains("wounded")) => "🩸",
            var c when c != null && (c.Contains("крит") || c.Contains("critical") || c.Contains("смерт")) => "💀",
            var c when c != null && (c.Contains("устал") || c.Contains("exhaust") || c.Contains("tired")) => "😩",
            _ => "🎭"
        };

        var table = ConsoleLayout.CreateBarMetricTable(labelWidth: 16, barWidth: 22, valueWidth: 6);

        table.AddRow(
            new Markup($"[{healthColor}]Здоровье[/]"),
            new Markup(healthBar),
            new Markup($"[{healthColor}]{healthPct,3}%[/]"),
            new Markup(string.Empty));
        table.AddRow(
            new Markup($"[{energyColor}]Энергия[/]"),
            new Markup(energyBar),
            new Markup($"[{energyColor}]{energyPct,3}%[/]"),
            new Markup(string.Empty));
        table.AddRow(
            new Markup($"[{poiseColor}]Равновесие[/]"),
            new Markup(poiseBar),
            new Markup($"[{poiseColor}]{poisePct,3}%[/]"),
            new Markup(string.Empty));

        var content = new Grid().AddColumn(new GridColumn());
        content.AddRow(table);
        content.AddRow(new Markup($"[white]{conditionEmoji} Состояние: {EscapeMarkup(status.CurrentCondition ?? "—")}[/]"));

        if (status.ActiveConditions.Length > 0)
        {
            foreach (var condition in status.ActiveConditions)
                content.AddRow(new Markup($"[yellow]⚠ {EscapeMarkup(condition)}[/]"));
        }

        var panel = new Panel(content)
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void RenderAfterlifeStatus(AggregatedGameState state)
    {
        var parts = new List<string>();

        if (!string.IsNullOrEmpty(state.ActiveGuardianName))
            parts.Add($"[steelblue1]🛡️ {_loc.T("guardian")}: {EscapeMarkup(state.ActiveGuardianName)}[/]");

        parts.Add($"[gold1]🪶 {_loc.T("ink_feathers")}: {state.InkFeathers}[/]");

        if (!string.IsNullOrEmpty(state.EnlightenmentTier))
            parts.Add($"[mediumpurple2]✨ {EscapeMarkup(state.EnlightenmentTier)}[/]");

        if (state.IsInShiningAbode)
        {
            parts.Add($"[gold1]✨ Radiance: {state.ShiningRadianceExperience} XP / tier {state.ShiningRadianceTier}[/]");
            parts.Add($"[yellow]✦ Light Sparks: {state.ShiningLightSparks}[/]");
            parts.Add("[dim]Полный Shining audit: /status или /shining_abode[/]");
        }
        else if (state.IsInShiningAbodePendingBootstrap)
        {
            parts.Add("[khaki1]✨ Shining handoff: prepared package ожидает TriggerIncarnation[/]");
        }

        var panel = new Panel(ConsoleLayout.CreateFactGrid(parts.ToArray()))
        {
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Blue),
            Padding = new Padding(1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void RenderDialogueOptions(DialogueOption[] options)
    {
        var table = new Table()
            .Border(TableBorder.None)
            .HideHeaders()
            .Expand()
            .AddColumn(new TableColumn("").RightAligned().NoWrap().Width(4))
            .AddColumn(new TableColumn(""));

        foreach (var (opt, index) in options.Select((opt, index) => (opt, index)))
        {
            var visibleText = DialogueOptionControlTagNormalizer.NormalizeVisibleText(opt.Text);
            var label = EscapeMarkup(visibleText ?? "");
            var category = EscapeMarkup(opt.Category ?? "");
            if (!string.IsNullOrWhiteSpace(category))
                label = $"[dim]({category})[/] {label}";

            table.AddRow(
                new Markup($"[bold cyan]{index + 1}[/]"),
                new Markup(label));
        }

        var panel = new Panel(table)
        {
            Header = new PanelHeader($" 💬 {_loc.T("dialogue_options")} ", Justify.Center),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Cyan1),
            Padding = new Padding(1, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    private void RenderSoulInfo(AggregatedGameState state)
    {
        if (state.IsInAfterlifeRealm)
        {
            AnsiConsole.Write(ConsoleLayout.CreateFactGrid(
                $"[{(state.IsInShiningAbodePendingBootstrap ? "khaki1" : (state.IsInShiningAbode ? "gold1" : "blue"))} dim]🔄 {_loc.T("incarnation")}: {state.Incarnation}[/]",
                state.IsInShiningAbodePendingBootstrap
                    ? "[khaki1 dim]⏳ Подготовка следующей жизни уже передана в bootstrap; обычные действия Обители и Моря Хаоса заблокированы.[/]"
                    : (state.IsInShiningAbode
                        ? "[gold1 dim]✨ Вы находитесь в Сияющей Обители — над Морем Хаоса.[/]"
                        : $"[blue dim]🌊 {_loc.T("chaos_sea_welcome")}[/]")));
        }
        else
        {
            AnsiConsole.Write(ConsoleLayout.CreateFactGrid(
                $"[grey]🔄 {_loc.T("incarnation")}: {state.Incarnation}[/]",
                $"[grey]🪶 {_loc.T("ink_feathers")}: {state.InkFeathers}[/]",
                $"[grey]✨ {EscapeMarkup(state.EnlightenmentTier)}[/]"));
        }
        AnsiConsole.WriteLine();
    }

    private void RenderGmThoughts(string thoughts)
    {
        var panel = new Panel(
            new Markup(EscapeMarkup(thoughts)))
        {
            Header = new PanelHeader(" 🧠 Мысли Мастера Игры ", Justify.Center),
            Border = BoxBorder.Ascii,
            BorderStyle = new Style(Color.Grey),
            Padding = new Padding(2, 0)
        };

        AnsiConsole.Write(panel);
        AnsiConsole.WriteLine();
    }

    public void RenderThinking()
    {
        AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots12)
            .SpinnerStyle(Style.Parse("cyan"))
            .Start(_loc.T("thinking"), ctx => { });
    }

    public void ShowError(string message)
    {
        AnsiConsole.MarkupLine($"[red]{_loc.T("error_occurred")}: {EscapeMarkup(message)}[/]");
    }

    public void ShowMessage(string message, string color = "green")
    {
        AnsiConsole.MarkupLine($"[{color}]{EscapeMarkup(message)}[/]");
    }

    private static int ParsePercentage(string? pct)
    {
        if (string.IsNullOrEmpty(pct)) return 100;
        var cleaned = pct.Replace("%", "").Trim();
        return int.TryParse(cleaned, out var val) ? Math.Clamp(val, 0, 100) : 100;
    }

    public static Markup SafeMarkup(string text, string? fallbackContext = null)
    {
        try
        {
            return new Markup(text ?? string.Empty);
        }
        catch (Exception ex) when (IsMarkupParseFailure(ex))
        {
            var plainText = TryRemoveMarkup(text ?? string.Empty);
            var fallbackTitle = string.IsNullOrWhiteSpace(fallbackContext)
                ? "[yellow dim]⚠ Обнаружена повреждённая UI-разметка. Показан безопасный текст.[/]"
                : $"[yellow dim]⚠ Обнаружена повреждённая UI-разметка ({EscapeMarkup(fallbackContext)}). Показан безопасный текст.[/]";

            return new Markup($"{fallbackTitle}\n[white]{EscapeMarkup(plainText)}[/]");
        }
    }

    public static Markup SafeMarkupText(string? text)
    {
        return new Markup(EscapeMarkup(text ?? string.Empty));
    }

    public static PanelHeader SafePanelHeader(string? text, Justify justify = Justify.Center)
    {
        return new PanelHeader($" {EscapeMarkup(text ?? string.Empty)} ", justify);
    }

    public static string SafePromptChoice(params string?[] parts)
    {
        var label = string.Join(" | ", parts.Where(part => !string.IsNullOrWhiteSpace(part)));
        return EscapeMarkup(label);
    }

    private static string TryRemoveMarkup(string text)
    {
        try
        {
            return Markup.Remove(text ?? string.Empty);
        }
        catch (Exception ex) when (IsMarkupParseFailure(ex))
        {
            return text ?? string.Empty;
        }
    }

    public static string EscapeMarkup(string text)
    {
        return Markup.Escape(text ?? "");
    }

    private static bool IsMarkupParseFailure(Exception ex)
    {
        return ex is InvalidOperationException &&
               (ex.Message.Contains("markup", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("color or style", StringComparison.OrdinalIgnoreCase));
    }
}
