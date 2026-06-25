using System.Globalization;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.UI;

public static class ExplorerMathCommandResultBuilder
{
    private static readonly HashSet<string> CommandAliases = new(StringComparer.OrdinalIgnoreCase)
    {
        "/math",
        "/математик"
    };

    public static bool CanBuild(string command) => CommandAliases.Contains(ExtractCommandToken(command));

    public static ExplorerCommandResult Build(string commandLine)
    {
        var normalizedCommandLine = commandLine.Trim();
        var commandToken = ExtractCommandToken(normalizedCommandLine);
        var remainder = ExtractRemainder(normalizedCommandLine);
        var parsed = ParseRemainder(remainder);
        var service = new MathAssistantService();
        var result = service.Evaluate(new MathAssistantEvaluationRequest(
            parsed.Expression,
            parsed.Variables,
            parsed.RoundingMode,
            parsed.DecimalPlaces));

        var blocks = new List<UiBlock>
        {
            BuildMathDossier(remainder, parsed, result)
        };

        if (parsed.ParseWarnings.Count > 0 || result.Warnings.Count > 0)
        {
            var dossier = (UiEntityDossierBlock)blocks[0];
            dossier.Sections.Add(BuildWarningsSection([.. parsed.ParseWarnings, .. result.Warnings]));
        }

        if (!result.Success)
        {
            blocks.Add(new UiMessageBlock
            {
                Severity = UiNotificationSeverity.Error,
                Title = "Ошибка Математика",
                Message = result.ErrorMessage ?? "Проверь формулу, переменные, скобки и деление на ноль."
            });
        }

        return new ExplorerCommandResult
        {
            Command = string.IsNullOrWhiteSpace(normalizedCommandLine) ? commandToken : normalizedCommandLine,
            State = result.Success ? CommandExecutionState.Completed : CommandExecutionState.Failed,
            Blocks = blocks
        };
    }

    private static UiEntityDossierBlock BuildMathDossier(
        string remainder,
        ParsedMathCommand parsed,
        MathAssistantEvaluationResult result)
    {
        var sections = new List<UiEntityDossierSection>();
        if (result.Variables.Count > 0)
            sections.Add(BuildVariablesSection(result.Variables));

        return new UiEntityDossierBlock
        {
            EntityType = "math-assistant",
            Title = result.Success ? "Математик" : "Формула не вычислена",
            Subtitle = "локальный расчёт без изменения состояния",
            Summary = result.Success
                ? "Расчёт выполнен локально. Если значение применяется к состоянию игры, ГМ должен записать это отдельно."
                : result.ErrorMessage ?? "Проверьте формулу, переменные, скобки и деление на ноль.",
            Badges =
            [
                new UiEntityBadge
                {
                    Label = result.Success ? "расчёт выполнен" : "нужна правка формулы",
                    Tone = result.Success ? UiTone.Success : UiTone.Warning,
                    Icon = result.Success ? "calculator" : "alert-triangle"
                }
            ],
            Facts =
            [
                new UiEntityFact { Label = "Формула", Value = string.IsNullOrWhiteSpace(remainder) ? "(пусто)" : parsed.Expression },
                new UiEntityFact { Label = "Нормализовано", Value = string.IsNullOrWhiteSpace(result.NormalizedExpression) ? "(пусто)" : result.NormalizedExpression },
                new UiEntityFact { Label = "Результат", Value = result.Result?.ToString(CultureInfo.InvariantCulture) ?? "нет" },
                new UiEntityFact { Label = "До округления", Value = result.RawResult?.ToString(CultureInfo.InvariantCulture) ?? "нет" },
                new UiEntityFact { Label = "Округление", Value = DescribeRounding(result) }
            ],
            Sections = sections
        };
    }

    private static UiEntityDossierSection BuildVariablesSection(IReadOnlyDictionary<string, decimal> variables) =>
        new()
        {
            Id = "math-variables",
            Title = "Переменные",
            Summary = "Значения, подставленные в формулу.",
            Icon = "calculator",
            Presentation = "cards",
            CollectionLabel = variables.Count == 1 ? "1 переменная" : $"{variables.Count} переменных",
            Cards = variables
                .OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(static pair => new UiEntityCard
                {
                    Title = pair.Key,
                    Subtitle = "переменная",
                    Summary = pair.Value.ToString(CultureInfo.InvariantCulture),
                    Icon = "hash",
                    Facts =
                    [
                        new UiEntityFact { Label = "Значение", Value = pair.Value.ToString(CultureInfo.InvariantCulture) }
                    ]
                })
                .ToList()
        };

    private static UiEntityDossierSection BuildWarningsSection(IReadOnlyList<string> warnings) =>
        new()
        {
            Id = "math-warnings",
            Title = "Предупреждения",
            Summary = "Что было исправлено или распознано не полностью.",
            Icon = "alert-triangle",
            Presentation = "cards",
            CollectionLabel = warnings.Count == 1 ? "1 предупреждение" : $"{warnings.Count} предупреждений",
            Cards = warnings
                .Where(static warning => !string.IsNullOrWhiteSpace(warning))
                .Select(static warning => new UiEntityCard
                {
                    Title = "Предупреждение",
                    Summary = warning.Trim(),
                    Icon = "alert-triangle"
                })
                .ToList()
        };

    private static ParsedMathCommand ParseRemainder(string remainder)
    {
        var variables = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var expressionParts = new List<string>();
        var warnings = new List<string>();
        var roundingMode = MathAssistantRoundingMode.None;
        int? decimalPlaces = null;

        foreach (var token in SplitCommandTokens(remainder))
        {
            var assignmentIndex = token.IndexOf('=');
            if (assignmentIndex <= 0 || assignmentIndex == token.Length - 1)
            {
                expressionParts.Add(token);
                continue;
            }

            var key = token[..assignmentIndex].Trim();
            var value = token[(assignmentIndex + 1)..].Trim();
            if (TryApplyRoundingOption(key, value, ref roundingMode, ref decimalPlaces, warnings))
                continue;

            if (!IsValidIdentifier(key) ||
                !decimal.TryParse(value, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var numericValue))
            {
                expressionParts.Add(token);
                continue;
            }

            if (!variables.TryAdd(key, numericValue))
                warnings.Add($"Переменная {key} указана несколько раз; использовано первое значение.");
        }

        return new ParsedMathCommand(
            string.Join(' ', expressionParts).Trim(),
            variables,
            roundingMode,
            decimalPlaces,
            warnings);
    }

    private static bool TryApplyRoundingOption(
        string key,
        string value,
        ref MathAssistantRoundingMode roundingMode,
        ref int? decimalPlaces,
        List<string> warnings)
    {
        if (key.Equals("decimalPlaces", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("places", StringComparison.OrdinalIgnoreCase) ||
            key.Equals("знаки", StringComparison.OrdinalIgnoreCase))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var places))
                decimalPlaces = places;
            else
                warnings.Add($"decimalPlaces не распознан: {value}.");
            return true;
        }

        if (!key.Equals("rounding", StringComparison.OrdinalIgnoreCase) &&
            !key.Equals("round", StringComparison.OrdinalIgnoreCase) &&
            !key.Equals("округление", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        roundingMode = NormalizeRoundingMode(value);
        if (roundingMode == MathAssistantRoundingMode.None &&
            !value.Equals("none", StringComparison.OrdinalIgnoreCase) &&
            !value.Equals("нет", StringComparison.OrdinalIgnoreCase))
        {
            warnings.Add($"Режим округления не распознан и заменён на none: {value}.");
        }

        return true;
    }

    private static MathAssistantRoundingMode NormalizeRoundingMode(string value)
    {
        var normalized = value.Trim().Replace("-", "_", StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "none" or "нет" => MathAssistantRoundingMode.None,
            "floor" or "down" or "вниз" => MathAssistantRoundingMode.Floor,
            "ceiling" or "ceil" or "up" or "вверх" => MathAssistantRoundingMode.Ceiling,
            "tozero" or "to_zero" or "zero" or "к_нулю" => MathAssistantRoundingMode.ToZero,
            "awayfromzero" or "away_from_zero" or "away" or "от_нуля" => MathAssistantRoundingMode.AwayFromZero,
            "tonearest" or "to_nearest" or "nearest" or "bankers" or "ближайшее" => MathAssistantRoundingMode.ToNearest,
            _ => MathAssistantRoundingMode.None
        };
    }

    private static string DescribeRounding(MathAssistantEvaluationResult result)
    {
        if (result.RoundingMode == MathAssistantRoundingMode.None)
            return "нет";

        return result.DecimalPlaces.HasValue
            ? $"{DescribeRoundingMode(result.RoundingMode)}, знаков: {result.DecimalPlaces.Value}"
            : DescribeRoundingMode(result.RoundingMode);
    }

    private static string DescribeRoundingMode(MathAssistantRoundingMode mode) =>
        mode switch
        {
            MathAssistantRoundingMode.Floor => "вниз",
            MathAssistantRoundingMode.Ceiling => "вверх",
            MathAssistantRoundingMode.ToZero => "к нулю",
            MathAssistantRoundingMode.AwayFromZero => "от нуля",
            MathAssistantRoundingMode.ToNearest => "до ближайшего",
            _ => "нет"
        };

    private static IReadOnlyList<string> SplitCommandTokens(string text) =>
        text.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static string ExtractCommandToken(string commandLine)
    {
        var trimmed = commandLine.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
            return string.Empty;

        var parts = trimmed.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 0 ? string.Empty : parts[0];
    }

    private static string ExtractRemainder(string commandLine)
    {
        var parts = commandLine.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[1].Trim() : string.Empty;
    }

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!(char.IsLetter(value[0]) || value[0] == '_'))
            return false;
        return value.Skip(1).All(static ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private sealed record ParsedMathCommand(
        string Expression,
        IReadOnlyDictionary<string, decimal> Variables,
        MathAssistantRoundingMode RoundingMode,
        int? DecimalPlaces,
        IReadOnlyList<string> ParseWarnings);
}
