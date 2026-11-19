using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using Spectre.Console;

namespace BookOfEternityClient.Tests;

// T024: inert default Spectre, no game/service/authority construction.
internal static class SpectreSelectionSmoke
{
    internal static int Run(string evidence, string plainOutput, bool inventory = false, bool load = false, bool cleanup = false, bool longLoad = false)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.InputEncoding = Encoding.UTF8; // Linux Program.cs branch.
        var options = ClientStartupOptions.Parse([evidence, plainOutput], evidence);
        if (!options.PlainOutput) throw new ArgumentException("Expected actual --plain-output parser semantics.");
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        string? selected = null;
        Exception? failure = null;
        try
        {
            // Record actual default geometry before either prompt; stdout may be PIPE.
            File.WriteAllText(Path.Combine(evidence, "geometry.json"), JsonSerializer.Serialize(new {
                ConsoleWidth = Console.WindowWidth, ConsoleHeight = Console.WindowHeight,
                SpectreWidth = AnsiConsole.Profile.Width, SpectreHeight = AnsiConsole.Profile.Height,
                InputRedirected = Console.IsInputRedirected, OutputRedirected = Console.IsOutputRedirected
            }));
            if (load)
            {
                var first = AnsiConsole.Prompt(new SelectionPrompt<string>()
                    .Title("[bold yellow]🎒 📦 Инвентарь[/]  [dim](⚖ 1.8kg/45.0kg кг)[/]  [dim](выберите для просмотра / управления)[/]")
                    .PageSize(20).AddChoices("💰 Деньги: 500", "← Назад"));
                if (first != "← Назад") throw new InvalidOperationException("First inert prompt did not return Back.");
                BookOfEternityClient.UI.SpectreConsoleSafe.Clear();
                AnsiConsole.Write(new Rule("[cyan]📂 Загрузка игры[/]").RuleStyle("cyan"));
                AnsiConsole.WriteLine();
            }
            selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title(load ? "Выберите сохранение:" : inventory
                    ? "[bold yellow]🎒 📦 Инвентарь[/]  [dim](⚖ 1.8kg/45.0kg кг)[/]  [dim](выберите для просмотра / управления)[/]"
                    : "T024 inert selection")
                .PageSize(load ? 15 : 20)
                .AddChoices(load ? new[] { longLoad ? "s | Ход 43 | Хозяйственный двор у нижней лестницы | 18.11.2026 18:00 | 99999KB" : "s | Ход 43 | Двор совета | 18.11.2026 18:00 | 99999KB", "↩️ Назад" } : inventory ? new[] { "💰 Деньги: 500", "⚔ ⚔️ Основная рука: Древний меч", "📦 Древний меч [[Оружие]]", "📦 🔒 Кладовая материалов совета (1 пр.)", "← Назад" } : new[] { "SMOKE_ALPHA", "SMOKE_BETA" }));
            if (cleanup)
            {
                if (selected != "← Назад") throw new InvalidOperationException("Cleanup did not return Back.");
                Console.WriteLine("Ваш ход"); Console.Write(" >  ");
                if (Console.ReadLine() != "exit") throw new InvalidOperationException("Expected inert normal exit after prompt.");
            }
            return 0;
        }
        catch (Exception error) { failure = error; return 1; }
        finally
        {
            File.WriteAllText(Path.Combine(evidence, "choice.json"), JsonSerializer.Serialize(new {
                Selected = selected, Failure = failure?.ToString(),
                InputRedirected = Console.IsInputRedirected,
                OutputRedirected = Console.IsOutputRedirected,
                ErrorRedirected = Console.IsErrorRedirected,
                Term = Environment.GetEnvironmentVariable("TERM"),
                NoColor = Environment.GetEnvironmentVariable("NO_COLOR"),
                PlainOutput = options.PlainOutput
            }));
        }
    }
}
