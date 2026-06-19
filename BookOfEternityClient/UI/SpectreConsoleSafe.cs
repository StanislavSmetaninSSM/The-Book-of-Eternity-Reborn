using Spectre.Console;

namespace BookOfEternityClient.UI;

internal static class SpectreConsoleSafe
{
    public static void Clear()
    {
        try
        {
            AnsiConsole.Clear();
        }
        catch (Exception ex) when (ex is IOException or InvalidOperationException)
        {
            // Redirected/headless Agent Console runs do not always have a console buffer.
        }
    }
}
