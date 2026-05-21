namespace BookOfEternityClient.CommandProtocol;

public static class ExplorerCommandParser
{
    public static ExplorerParsedCommand Parse(string? input)
    {
        var raw = input?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
            return ExplorerParsedCommand.Failed(raw, "Команда не выполнена", "Команда пустая.");

        if (!HasBalancedQuotes(raw))
        {
            return ExplorerParsedCommand.Failed(
                raw,
                "Некорректные аргументы",
                "В команде есть незакрытая кавычка. Закройте кавычки или уберите их из аргументов.");
        }

        var (token, remainder) = SplitFirstToken(raw);
        var descriptor = ExplorerCommandCatalog.FindByAlias(token);
        if (descriptor == null)
        {
            return ExplorerParsedCommand.Failed(
                raw,
                "Команда не найдена",
                $"Команда {token} не зарегистрирована в ExplorerMode.");
        }

        var exactSubcommand = string.IsNullOrWhiteSpace(remainder)
            ? descriptor.SubcommandDescriptors.FirstOrDefault(subcommand =>
                string.Equals(subcommand.CanonicalCommand, token, StringComparison.OrdinalIgnoreCase))
            : null;
        if (exactSubcommand != null)
        {
            return ExplorerParsedCommand.Succeeded(
                raw,
                token,
                descriptor,
                exactSubcommand,
                exactSubcommand.CanonicalCommand,
                exactSubcommand.CanonicalCommand,
                remainder);
        }

        if (!string.IsNullOrWhiteSpace(remainder) && descriptor.SubcommandDescriptors.Count > 0)
        {
            var matchedSubcommand = MatchSubcommand(descriptor, remainder);
            if (matchedSubcommand.Subcommand == null)
            {
                return ExplorerParsedCommand.Failed(
                    raw,
                    "Неизвестная подкоманда",
                    $"Подкоманда \"{remainder}\" не поддерживается для {token}. Проверьте написание или используйте базовую команду без подкоманды.");
            }

            return ExplorerParsedCommand.Succeeded(
                raw,
                token,
                descriptor,
                matchedSubcommand.Subcommand,
                matchedSubcommand.Subcommand.CanonicalCommand,
                matchedSubcommand.Subcommand.CanonicalCommand,
                matchedSubcommand.Arguments);
        }

        var canonicalCommand = descriptor.PrimaryAlias;
        var builderCommand = string.IsNullOrWhiteSpace(remainder) ? token : $"{token} {remainder}";
        return ExplorerParsedCommand.Succeeded(
            raw,
            token,
            descriptor,
            subcommand: null,
            canonicalCommand,
            builderCommand,
            remainder);
    }

    private static (string Token, string Remainder) SplitFirstToken(string input)
    {
        var parts = input.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length switch
        {
            0 => (string.Empty, string.Empty),
            1 => (parts[0], string.Empty),
            _ => (parts[0], parts[1].Trim())
        };
    }

    private static (ExplorerCommandSubcommandDescriptor? Subcommand, string Arguments) MatchSubcommand(
        ExplorerCommandDescriptor descriptor,
        string remainder)
    {
        var normalizedRemainder = NormalizeSubcommand(remainder);
        foreach (var subcommand in descriptor.SubcommandDescriptors)
        {
            foreach (var alias in subcommand.Aliases)
            {
                var normalizedAlias = NormalizeSubcommand(alias);
                if (string.Equals(normalizedRemainder, normalizedAlias, StringComparison.OrdinalIgnoreCase))
                    return (subcommand, string.Empty);

                if (normalizedRemainder.StartsWith(normalizedAlias + " ", StringComparison.OrdinalIgnoreCase))
                    return (subcommand, remainder[alias.Length..].Trim());
            }
        }

        return (null, string.Empty);
    }

    private static string NormalizeSubcommand(string value)
    {
        var trimmed = value.Trim().Replace('-', '_');
        return string.Join(' ', trimmed.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToLowerInvariant();
    }

    private static bool HasBalancedQuotes(string value)
    {
        var quoteCount = 0;
        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] != '"')
                continue;

            var escaped = i > 0 && value[i - 1] == '\\';
            if (!escaped)
                quoteCount++;
        }

        return quoteCount % 2 == 0;
    }
}

public sealed record ExplorerParsedCommand(
    bool Success,
    string RawInput,
    string CommandToken,
    ExplorerCommandDescriptor? Descriptor,
    ExplorerCommandSubcommandDescriptor? Subcommand,
    string CanonicalCommand,
    string BuilderCommand,
    string Arguments,
    string ErrorTitle,
    string ErrorMessage)
{
    public static ExplorerParsedCommand Succeeded(
        string rawInput,
        string commandToken,
        ExplorerCommandDescriptor descriptor,
        ExplorerCommandSubcommandDescriptor? subcommand,
        string canonicalCommand,
        string builderCommand,
        string arguments) =>
        new(
            Success: true,
            rawInput,
            commandToken,
            descriptor,
            subcommand,
            canonicalCommand,
            builderCommand,
            arguments,
            ErrorTitle: string.Empty,
            ErrorMessage: string.Empty);

    public static ExplorerParsedCommand Failed(string rawInput, string errorTitle, string errorMessage) =>
        new(
            Success: false,
            rawInput,
            CommandToken: string.Empty,
            Descriptor: null,
            Subcommand: null,
            CanonicalCommand: string.Empty,
            BuilderCommand: string.Empty,
            Arguments: string.Empty,
            errorTitle,
            errorMessage);
}
