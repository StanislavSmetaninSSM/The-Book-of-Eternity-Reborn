using System.Text;

namespace BookOfEternityClient.Core;

/// <summary>Player prompt only. The owning thread reads keys; no abandoned ReadLine task.</summary>
internal sealed class CooperativePlayerInputSource(
    IConsoleInputSource source,
    Func<bool> pendingInputHint,
    Action<string> echo) : IConsoleInputSource
{
    public bool IsScripted => source.IsScripted;
    public bool KeyAvailable => source.KeyAvailable;
    public ConsoleKeyInfo ReadKey(bool intercept = true) => source.ReadKey(intercept);
    public void AssertCompleted() => source.AssertCompleted();

    public string? ReadLine()
    {
        var draft = new StringBuilder();
        while (true)
        {
            // This is a wake-up hint only. Selection and generation validation happen
            // later under the original current-session operation and physical lease.
            if (pendingInputHint())
                throw new TextComposerInputClosedException(draft.ToString(), interrupted: true);
            if (!source.KeyAvailable)
            {
                Thread.Sleep(20);
                continue;
            }

            var key = source.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                echo(Environment.NewLine);
                return draft.ToString();
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (draft.Length > 0)
                {
                    // Remove a full Unicode scalar rather than half a surrogate pair.
                    var count = draft.Length > 1 && char.IsLowSurrogate(draft[^1]) &&
                        char.IsHighSurrogate(draft[^2]) ? 2 : 1;
                    draft.Length -= count;
                    echo("\b \b");
                }
                continue;
            }
            if (key.KeyChar == '\t' || !char.IsControl(key.KeyChar))
            {
                draft.Append(key.KeyChar);
                echo(key.KeyChar.ToString());
            }
        }
    }
}
