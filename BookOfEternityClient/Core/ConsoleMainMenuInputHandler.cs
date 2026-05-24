namespace BookOfEternityClient.Core;

internal static class ConsoleMainMenuInputHandler
{
    public static ConsoleMainMenuInputResult Apply(ConsoleKeyInfo key, int selectedIndex, int optionCount)
    {
        if (optionCount <= 0)
            return new ConsoleMainMenuInputResult(0, SelectionChanged: false, ActivateSelection: false);

        var boundedIndex = Math.Clamp(selectedIndex, 0, optionCount - 1);
        switch (key.Key)
        {
            case ConsoleKey.UpArrow:
            case ConsoleKey.W:
                return new ConsoleMainMenuInputResult(
                    (boundedIndex - 1 + optionCount) % optionCount,
                    SelectionChanged: true,
                    ActivateSelection: false);
            case ConsoleKey.DownArrow:
            case ConsoleKey.S:
                return new ConsoleMainMenuInputResult(
                    (boundedIndex + 1) % optionCount,
                    SelectionChanged: true,
                    ActivateSelection: false);
            case ConsoleKey.Enter:
                return new ConsoleMainMenuInputResult(
                    boundedIndex,
                    SelectionChanged: false,
                    ActivateSelection: true);
            default:
                if (TryMapNumberSelection(key, optionCount, out var numericIndex))
                {
                    return new ConsoleMainMenuInputResult(
                        numericIndex,
                        SelectionChanged: true,
                        ActivateSelection: false);
                }

                return new ConsoleMainMenuInputResult(
                    boundedIndex,
                    SelectionChanged: false,
                    ActivateSelection: false);
        }
    }

    private static bool TryMapNumberSelection(ConsoleKeyInfo key, int optionsCount, out int index)
    {
        index = -1;

        int? numeric = key.Key switch
        {
            ConsoleKey.D1 or ConsoleKey.NumPad1 => 1,
            ConsoleKey.D2 or ConsoleKey.NumPad2 => 2,
            ConsoleKey.D3 or ConsoleKey.NumPad3 => 3,
            ConsoleKey.D4 or ConsoleKey.NumPad4 => 4,
            ConsoleKey.D5 or ConsoleKey.NumPad5 => 5,
            ConsoleKey.D6 or ConsoleKey.NumPad6 => 6,
            ConsoleKey.D7 or ConsoleKey.NumPad7 => 7,
            ConsoleKey.D8 or ConsoleKey.NumPad8 => 8,
            ConsoleKey.D9 or ConsoleKey.NumPad9 => 9,
            _ => null
        };

        if (!numeric.HasValue || numeric.Value > optionsCount)
            return false;

        index = numeric.Value - 1;
        return true;
    }
}

internal readonly record struct ConsoleMainMenuInputResult(
    int SelectedIndex,
    bool SelectionChanged,
    bool ActivateSelection);
