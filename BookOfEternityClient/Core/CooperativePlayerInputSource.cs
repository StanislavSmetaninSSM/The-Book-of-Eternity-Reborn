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
        static int Scalars(string value) => value.EnumerateRunes().Count();
        void Back(int count) { if (count > 0) echo(new string('\b', count)); }
        void Remove(int at, int count)
        {
            draft.Remove(at, count);
            var tail = draft.ToString(at, draft.Length - at);
            echo(tail + " ");
            Back(Scalars(tail) + 1);
        }
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
            if (key.Key == ConsoleKey.LeftArrow)
            {
                if (cursor > 0) { cursor -= PreviousLength(); Back(1); }
                continue;
            }
            if (key.Key == ConsoleKey.RightArrow)
            {
                if (cursor < draft.Length) { var count = ScalarLength(cursor); echo(draft.ToString(cursor, count)); cursor += count; }
                continue;
            }
            if (key.Key == ConsoleKey.Home)
            {
                Back(Scalars(draft.ToString(0, cursor))); cursor = 0;
                continue;
            }
            if (key.Key == ConsoleKey.End)
            {
                echo(draft.ToString(cursor, draft.Length - cursor)); cursor = draft.Length;
                continue;
            }
            if (key.Key == ConsoleKey.Delete)
            {
                if (cursor < draft.Length) Remove(cursor, ScalarLength(cursor));
                continue;
            }
            if (key.Key == ConsoleKey.Backspace)
            {
                if (cursor > 0)
                {
                    // Remove a full Unicode scalar rather than half a surrogate pair.
                    var count = PreviousLength();
                    cursor -= count; Back(1); Remove(cursor, count);
                }
                continue;
            }
            if (key.KeyChar == '\t' || !char.IsControl(key.KeyChar))
            {
                draft.Insert(cursor, key.KeyChar);
                cursor++;
                var tail = draft.ToString(cursor, draft.Length - cursor);
                echo(key.KeyChar + tail);
                Back(Scalars(tail));
            }
        }
    }
}
