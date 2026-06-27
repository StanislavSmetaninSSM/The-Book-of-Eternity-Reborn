namespace BookOfEternityClient.Services;

internal static class LiveTurnPreparationCli
{
    internal const string ModeSwitch = "--prepare-live-turn";

    public static bool TryParse(
        IReadOnlyList<string> args,
        out LiveTurnPreparationOptions options,
        out string? error)
    {
        options = new LiveTurnPreparationOptions();
        error = null;

        if (!args.Any(arg => string.Equals(arg, ModeSwitch, StringComparison.OrdinalIgnoreCase)))
            return false;

        var sessionId = (string?)null;
        var requestId = (string?)null;
        int? turnNumber = null;
        var playerAction = (string?)null;
        int[]? dice = null;
        var currentRealm = (string?)null;

        for (var index = 0; index < args.Count; index++)
        {
            var arg = args[index];
            if (string.Equals(arg, ModeSwitch, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(arg, "--session-id", StringComparison.OrdinalIgnoreCase))
            {
                sessionId = ReadRequiredValue(args, ref index, "--session-id");
                continue;
            }

            if (string.Equals(arg, "--request-id", StringComparison.OrdinalIgnoreCase))
            {
                requestId = ReadRequiredValue(args, ref index, "--request-id");
                continue;
            }

            if (string.Equals(arg, "--turn-number", StringComparison.OrdinalIgnoreCase))
            {
                var raw = ReadRequiredValue(args, ref index, "--turn-number");
                if (!int.TryParse(raw, out var parsed) || parsed < 1)
                {
                    error = "--turn-number must be a positive integer.";
                    return true;
                }

                turnNumber = parsed;
                continue;
            }

            if (string.Equals(arg, "--action", StringComparison.OrdinalIgnoreCase))
            {
                playerAction = ReadRequiredValue(args, ref index, "--action");
                continue;
            }

            if (string.Equals(arg, "--dice", StringComparison.OrdinalIgnoreCase))
            {
                var raw = ReadRequiredValue(args, ref index, "--dice");
                if (!TryParseDice(raw, out dice))
                {
                    error = "--dice must contain integers from 1 to 20 separated by comma, semicolon, or whitespace.";
                    return true;
                }

                continue;
            }

            if (string.Equals(arg, "--current-realm", StringComparison.OrdinalIgnoreCase))
            {
                currentRealm = ReadRequiredValue(args, ref index, "--current-realm");
                continue;
            }
        }

        if (string.IsNullOrWhiteSpace(playerAction))
        {
            error = "--action is required for --prepare-live-turn.";
            return true;
        }

        options = new LiveTurnPreparationOptions
        {
            SessionId = sessionId,
            RequestId = requestId,
            TurnNumber = turnNumber,
            PlayerAction = playerAction!,
            PreGeneratedDices1d20 = dice,
            CurrentRealm = currentRealm
        };
        return true;
    }

    private static string ReadRequiredValue(IReadOnlyList<string> args, ref int index, string optionName)
    {
        if (index + 1 >= args.Count)
            throw new ArgumentException($"Missing value for {optionName}.", nameof(args));

        var value = args[++index];
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException($"Missing value for {optionName}.", nameof(args));

        return value;
    }

    private static bool TryParseDice(string value, out int[] dice)
    {
        dice = value
            .Split(new[] { ',', ';', ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var parsed) && parsed is >= 1 and <= 20 ? parsed : -1)
            .ToArray();

        return dice.Length > 0 && dice.All(item => item >= 1);
    }
}
