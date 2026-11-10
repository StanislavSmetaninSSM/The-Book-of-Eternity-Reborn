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
        var cursor = 0;
        int ScalarLength(int at) => at + 1 < draft.Length && char.IsHighSurrogate(draft[at]) && char.IsLowSurrogate(draft[at + 1]) ? 2 : 1;
        int PreviousLength() => cursor > 1 && char.IsLowSurrogate(draft[cursor - 1]) && char.IsHighSurrogate(draft[cursor - 2]) ? 2 : 1;
        // Let the terminal place tabs, wide/combining characters and wrapped rows.
        // No cursor query reads keys or introduces another input owner.
        echo("\u001b[s");
        void Render(bool atEnd = false)
        {
            echo("\u001b[u\u001b[J");
            echo(draft.ToString());
            if (!atEnd)
            {
                echo("\u001b[u");
                echo(draft.ToString(0, cursor));
            }
        }
        while (true)
        {
            // This is a wake-up hint only. Selection and generation validation happen
            // later under the original current-session operation and physical lease.
            if (pendingInputHint())
            {
                Render(atEnd: true);
                echo(Environment.NewLine);
                throw new TextComposerInputClosedException(draft.ToString(), interrupted: true);
            }
            if (!source.KeyAvailable)
            {
                Thread.Sleep(20);
                continue;
            }

            var key = source.ReadKey(intercept: true);
            if (key.KeyChar == '\u0004' && key.Modifiers.HasFlag(ConsoleModifiers.Control))
            {
                Render(atEnd: true);
                echo(Environment.NewLine);
                return draft.Length == 0 ? null : draft.ToString();
            }
            if (key.Key == ConsoleKey.Enter)
            {
                Render(atEnd: true);
                echo(Environment.NewLine);
                return draft.ToString();
            }
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0) { cursor -= PreviousLength(); Render(); }
                continue;
            }
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursor < draft.Length) { cursor += ScalarLength(cursor); Render(); }
                continue;
            }
            if (key.Key == ConsoleKey.Home)
            {
                cursor = 0; Render();
                continue;
            }
            if (key.Key == ConsoleKey.End)
            {
                cursor = draft.Length; Render();
                continue;
            }
            if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < draft.Length) { draft.Remove(cursor, ScalarLength(cursor)); Render(); }
                continue;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    // Remove a full Unicode scalar rather than half a surrogate pair.
                    var count = PreviousLength();
                    cursor -= count; draft.Remove(cursor, count); Render();
                }
                continue;
            }
            if (key.KeyChar == '\t' || !char.IsControl(key.KeyChar))
            {
                draft.Insert(cursor, key.KeyChar);
                cursor++;
                if (!char.IsHighSurrogate(key.KeyChar)) Render();
            }
        }
    }
}
