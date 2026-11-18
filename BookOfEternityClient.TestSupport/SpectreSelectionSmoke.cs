using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using Spectre.Console;

namespace BookOfEternityClient.Tests;

// T024: inert default Spectre, no game/service/authority construction.
internal static class SpectreSelectionSmoke
{
    internal static int Run(string evidence, string plainOutput)
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
            selected = AnsiConsole.Prompt(new SelectionPrompt<string>()
                .Title("T024 inert selection")
                .AddChoices("SMOKE_ALPHA", "SMOKE_BETA"));
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
