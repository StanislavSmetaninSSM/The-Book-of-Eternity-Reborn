using System.Text;
using System.Text.Json;

namespace BookOfEternityClient.Tests;

/// <summary>Inert actual .NET cursor query, no game state or service construction.</summary>
internal static class TerminalCursorProbe
{
    internal static int Run(string resultPath)
    {
        if (File.Exists(resultPath)) throw new IOException("Probe result already exists.");
        Console.OutputEncoding = new UTF8Encoding(false);
        Console.Write("\u001b[2J\u001b[1;1H");
        Console.Write("abc\r\n⚙️ Игровое меню\r\nEsc — назад\r\n\r\n");
        int top = Console.CursorTop; // Real runtime query; never substituted.
        File.WriteAllText(resultPath, JsonSerializer.Serialize(new
        {
            CursorTop = top,
            Console.WindowWidth,
            Console.WindowHeight,
            Console.IsInputRedirected,
            Console.IsOutputRedirected,
            Console.IsErrorRedirected
        }));
        return top == 4 && Console.WindowWidth == 100 && Console.WindowHeight == 25
            && !Console.IsInputRedirected && !Console.IsOutputRedirected && !Console.IsErrorRedirected ? 0 : 1;
    }
}
