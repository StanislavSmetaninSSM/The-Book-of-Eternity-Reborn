using System.Text;

namespace BookOfEternityClient.Core;

internal static class BufferedConsolePasteCapture
{
    public static string Drain(Func<bool> keyAvailable, Func<ConsoleKeyInfo> readKey, int quietPeriodMs = 40, int maxTotalMs = 500)
    {
        if (!keyAvailable())
            return string.Empty;

        var result = new StringBuilder();
        var startedAt = Environment.TickCount64;
        var lastInputAt = startedAt;

        while (Environment.TickCount64 - startedAt < maxTotalMs)
        {
            var consumedAny = false;
            while (keyAvailable())
            {
                consumedAny = true;
                var key = readKey();
                lastInputAt = Environment.TickCount64;

                if (key.Key == ConsoleKey.Enter)
                {
                    result.Append('\n');
                    continue;
                }

                if (key.Key == ConsoleKey.Backspace)
                {
                    if (result.Length > 0)
                        result.Length--;
                    continue;
                }

                if (!char.IsControl(key.KeyChar))
                    result.Append(key.KeyChar);
            }

            if (!consumedAny && Environment.TickCount64 - lastInputAt >= quietPeriodMs)
                break;

            Thread.Sleep(10);
        }

        return result.ToString().TrimEnd('\n');
    }
}
