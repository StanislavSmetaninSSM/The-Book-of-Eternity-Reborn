using System.Globalization;

namespace BookOfEternityClient.Services;

public enum MathAssistantRoundingMode
{
    None,
    Floor,
    Ceiling,
    ToZero,
    AwayFromZero,
    ToNearest
}

public static class MathAssistantErrorCodes
{
    public const string EmptyExpression = "empty_expression";
    public const string ExpressionTooLong = "expression_too_long";
    public const string InvalidVariableName = "invalid_variable_name";
    public const string DuplicateVariable = "duplicate_variable";
    public const string InvalidNumber = "invalid_number";
    public const string UnexpectedToken = "unexpected_token";
    public const string MissingVariable = "missing_variable";
    public const string UnknownFunction = "unknown_function";
    public const string WrongArgumentCount = "wrong_argument_count";
    public const string InvalidFunctionArgument = "invalid_function_argument";
    public const string DivisionByZero = "division_by_zero";
    public const string Overflow = "overflow";
}

public sealed record MathAssistantEvaluationRequest(
    string Expression,
    IReadOnlyDictionary<string, decimal>? Variables = null,
    MathAssistantRoundingMode RoundingMode = MathAssistantRoundingMode.None,
    int? DecimalPlaces = null);

public sealed record MathAssistantEvaluationResult(
    bool Success,
    string NormalizedExpression,
    IReadOnlyDictionary<string, decimal> Variables,
    decimal? RawResult,
    decimal? Result,
    MathAssistantRoundingMode RoundingMode,
    int? DecimalPlaces,
    IReadOnlyList<string> Warnings,
    string? ErrorCode,
    string? ErrorMessage);

public sealed class MathAssistantService
{
    private const int MaxExpressionLength = 2_000;
    private const int MaxRoundingDecimalPlaces = 8;

    public MathAssistantEvaluationResult Evaluate(MathAssistantEvaluationRequest request)
    {
        var expression = request.Expression ?? "";
        var normalizedExpression = NormalizeExpression(expression);
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(expression))
        {
            return Error(
                normalizedExpression,
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
                request,
                MathAssistantErrorCodes.EmptyExpression,
                "Формула пуста.");
        }

        if (expression.Length > MaxExpressionLength)
        {
            return Error(
                normalizedExpression,
                new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase),
                request,
                MathAssistantErrorCodes.ExpressionTooLong,
                $"Формула слишком длинная: максимум {MaxExpressionLength} символов.");
        }

        var variablesResult = NormalizeVariables(request.Variables);
        if (variablesResult.ErrorCode != null)
            return Error(normalizedExpression, variablesResult.Variables, request, variablesResult.ErrorCode, variablesResult.ErrorMessage!);

        try
        {
            var raw = new Parser(expression, variablesResult.Variables).Parse();
            var rounded = ApplyRequestRounding(raw, request, warnings);

            return new MathAssistantEvaluationResult(
                Success: true,
                NormalizedExpression: normalizedExpression,
                Variables: variablesResult.Variables,
                RawResult: raw,
                Result: rounded,
                RoundingMode: request.RoundingMode,
                DecimalPlaces: request.DecimalPlaces,
                Warnings: warnings,
                ErrorCode: null,
                ErrorMessage: null);
        }
        catch (MathAssistantException ex)
        {
            return Error(normalizedExpression, variablesResult.Variables, request, ex.Code, ex.Message);
        }
        catch (OverflowException)
        {
            return Error(
                normalizedExpression,
                variablesResult.Variables,
                request,
                MathAssistantErrorCodes.Overflow,
                "Вычисление вышло за пределы decimal.");
        }
    }

    private static MathAssistantEvaluationResult Error(
        string normalizedExpression,
        IReadOnlyDictionary<string, decimal> variables,
        MathAssistantEvaluationRequest request,
        string errorCode,
        string errorMessage)
    {
        return new MathAssistantEvaluationResult(
            Success: false,
            NormalizedExpression: normalizedExpression,
            Variables: variables,
            RawResult: null,
            Result: null,
            RoundingMode: request.RoundingMode,
            DecimalPlaces: request.DecimalPlaces,
            Warnings: Array.Empty<string>(),
            ErrorCode: errorCode,
            ErrorMessage: errorMessage);
    }

    private static (Dictionary<string, decimal> Variables, string? ErrorCode, string? ErrorMessage) NormalizeVariables(
        IReadOnlyDictionary<string, decimal>? variables)
    {
        var result = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        if (variables == null)
            return (result, null, null);

        foreach (var pair in variables)
        {
            var name = pair.Key?.Trim() ?? "";
            if (!IsValidIdentifier(name))
            {
                return (result, MathAssistantErrorCodes.InvalidVariableName, $"Недопустимое имя переменной: '{pair.Key}'.");
            }

            if (result.ContainsKey(name))
            {
                return (result, MathAssistantErrorCodes.DuplicateVariable, $"Переменная '{name}' указана несколько раз.");
            }

            result[name] = pair.Value;
        }

        return (result, null, null);
    }

    private static decimal ApplyRequestRounding(decimal raw, MathAssistantEvaluationRequest request, List<string> warnings)
    {
        if (request.RoundingMode == MathAssistantRoundingMode.None)
        {
            if (request.DecimalPlaces.HasValue)
                warnings.Add("decimalPlaces указан без режима округления и был проигнорирован.");
            return raw;
        }

        var places = request.DecimalPlaces ?? 0;
        if (places is < 0 or > MaxRoundingDecimalPlaces)
            throw new MathAssistantException(MathAssistantErrorCodes.InvalidFunctionArgument, $"decimalPlaces должен быть от 0 до {MaxRoundingDecimalPlaces}.");

        return request.RoundingMode switch
        {
            MathAssistantRoundingMode.Floor => decimal.Floor(raw),
            MathAssistantRoundingMode.Ceiling => decimal.Ceiling(raw),
            MathAssistantRoundingMode.ToZero => decimal.Truncate(raw),
            MathAssistantRoundingMode.AwayFromZero => decimal.Round(raw, places, MidpointRounding.AwayFromZero),
            MathAssistantRoundingMode.ToNearest => decimal.Round(raw, places, MidpointRounding.ToEven),
            _ => raw
        };
    }

    private static string NormalizeExpression(string expression) =>
        string.Concat((expression ?? "").Where(ch => !char.IsWhiteSpace(ch)));

    private static bool IsValidIdentifier(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!(char.IsLetter(value[0]) || value[0] == '_'))
            return false;
        return value.Skip(1).All(ch => char.IsLetterOrDigit(ch) || ch == '_');
    }

    private sealed class Parser
    {
        private readonly string _text;
        private readonly IReadOnlyDictionary<string, decimal> _variables;
        private int _position;
        private Token _current;

        public Parser(string text, IReadOnlyDictionary<string, decimal> variables)
        {
            _text = text;
            _variables = variables;
            _current = new Token(TokenKind.End, "", null);
        }

        public decimal Parse()
        {
            Next();
            var value = ParseExpression();
            if (_current.Kind != TokenKind.End)
                throw Unexpected($"лишний токен '{_current.Text}'");

            return value;
        }

        private decimal ParseExpression()
        {
            var left = ParseTerm();

            while (_current.Kind is TokenKind.Plus or TokenKind.Minus)
            {
                var op = _current.Kind;
                Next();
                var right = ParseTerm();
                left = op == TokenKind.Plus
                    ? checked(left + right)
                    : checked(left - right);
            }

            return left;
        }

        private decimal ParseTerm()
        {
            var left = ParseUnary();

            while (_current.Kind is TokenKind.Star or TokenKind.Slash)
            {
                var op = _current.Kind;
                Next();
                var right = ParseUnary();

                if (op == TokenKind.Slash)
                {
                    if (right == 0m)
                        throw new MathAssistantException(MathAssistantErrorCodes.DivisionByZero, "Деление на ноль.");
                    left = checked(left / right);
                }
                else
                {
                    left = checked(left * right);
                }
            }

            return left;
        }

        private decimal ParseUnary()
        {
            if (_current.Kind == TokenKind.Plus)
            {
                Next();
                return ParseUnary();
            }

            if (_current.Kind == TokenKind.Minus)
            {
                Next();
                return checked(-ParseUnary());
            }

            return ParsePrimary();
        }

        private decimal ParsePrimary()
        {
            if (_current.Kind == TokenKind.Number)
            {
                var value = _current.Number!.Value;
                Next();
                return value;
            }

            if (_current.Kind == TokenKind.Identifier)
            {
                var identifier = _current.Text;
                Next();
                if (_current.Kind == TokenKind.LeftParen)
                    return ParseFunction(identifier);

                if (!_variables.TryGetValue(identifier, out var value))
                    throw new MathAssistantException(MathAssistantErrorCodes.MissingVariable, $"Не найдена переменная '{identifier}'.");

                return value;
            }

            if (_current.Kind == TokenKind.LeftParen)
            {
                Next();
                var value = ParseExpression();
                Require(TokenKind.RightParen, "ожидалась закрывающая скобка");
                Next();
                return value;
            }

            throw Unexpected($"ожидалось число, переменная, функция или скобка, найдено '{_current.Text}'");
        }

        private decimal ParseFunction(string name)
        {
            Require(TokenKind.LeftParen, "ожидалась открывающая скобка функции");
            Next();

            var args = new List<decimal>();
            if (_current.Kind != TokenKind.RightParen)
            {
                while (true)
                {
                    args.Add(ParseExpression());
                    if (_current.Kind == TokenKind.Comma)
                    {
                        Next();
                        continue;
                    }

                    break;
                }
            }

            Require(TokenKind.RightParen, "ожидалась закрывающая скобка функции");
            Next();

            return EvaluateFunction(name, args);
        }

        private static decimal EvaluateFunction(string name, IReadOnlyList<decimal> args)
        {
            return name.ToLowerInvariant() switch
            {
                "min" => args.Count >= 1
                    ? args.Min()
                    : throw new MathAssistantException(MathAssistantErrorCodes.WrongArgumentCount, "min требует минимум один аргумент."),
                "max" => args.Count >= 1
                    ? args.Max()
                    : throw new MathAssistantException(MathAssistantErrorCodes.WrongArgumentCount, "max требует минимум один аргумент."),
                "clamp" => EvaluateClamp(args),
                "round" => EvaluateRound(args),
                "floor" => RequireArgs(args, "floor", 1, values => decimal.Floor(values[0])),
                "ceil" or "ceiling" => RequireArgs(args, name, 1, values => decimal.Ceiling(values[0])),
                "abs" => RequireArgs(args, "abs", 1, values => Math.Abs(values[0])),
                _ => throw new MathAssistantException(MathAssistantErrorCodes.UnknownFunction, $"Неизвестная функция '{name}'.")
            };
        }

        private static decimal EvaluateClamp(IReadOnlyList<decimal> args)
        {
            if (args.Count != 3)
                throw new MathAssistantException(MathAssistantErrorCodes.WrongArgumentCount, "clamp требует три аргумента: значение, минимум, максимум.");

            var value = args[0];
            var min = args[1];
            var max = args[2];
            if (min > max)
                throw new MathAssistantException(MathAssistantErrorCodes.InvalidFunctionArgument, "В clamp минимум не может быть больше максимума.");

            return Math.Min(Math.Max(value, min), max);
        }

        private static decimal EvaluateRound(IReadOnlyList<decimal> args)
        {
            if (args.Count is < 1 or > 2)
                throw new MathAssistantException(MathAssistantErrorCodes.WrongArgumentCount, "round требует один или два аргумента.");

            var places = args.Count == 1 ? 0 : ReadDecimalPlaces(args[1]);
            return decimal.Round(args[0], places, MidpointRounding.AwayFromZero);
        }

        private static int ReadDecimalPlaces(decimal value)
        {
            if (value != decimal.Truncate(value) || value is < 0m or > MaxRoundingDecimalPlaces)
                throw new MathAssistantException(MathAssistantErrorCodes.InvalidFunctionArgument, $"Количество знаков округления должно быть целым числом от 0 до {MaxRoundingDecimalPlaces}.");

            return (int)value;
        }

        private static decimal RequireArgs(IReadOnlyList<decimal> args, string name, int count, Func<IReadOnlyList<decimal>, decimal> evaluator)
        {
            if (args.Count != count)
                throw new MathAssistantException(MathAssistantErrorCodes.WrongArgumentCount, $"{name} требует {count} аргумент(а).");

            return evaluator(args);
        }

        private void Require(TokenKind expected, string message)
        {
            if (_current.Kind != expected)
                throw new MathAssistantException(MathAssistantErrorCodes.UnexpectedToken, $"{message}; найдено '{_current.Text}'.");
        }

        private MathAssistantException Unexpected(string details) =>
            new(MathAssistantErrorCodes.UnexpectedToken, $"Формула не разобрана: {details}.");

        private void Next()
        {
            SkipWhitespace();

            if (_position >= _text.Length)
            {
                _current = new Token(TokenKind.End, "", null);
                return;
            }

            var ch = _text[_position];
            switch (ch)
            {
                case '+':
                    _position++;
                    _current = new Token(TokenKind.Plus, "+", null);
                    return;
                case '-':
                    _position++;
                    _current = new Token(TokenKind.Minus, "-", null);
                    return;
                case '*':
                    _position++;
                    _current = new Token(TokenKind.Star, "*", null);
                    return;
                case '/':
                    _position++;
                    _current = new Token(TokenKind.Slash, "/", null);
                    return;
                case '(':
                    _position++;
                    _current = new Token(TokenKind.LeftParen, "(", null);
                    return;
                case ')':
                    _position++;
                    _current = new Token(TokenKind.RightParen, ")", null);
                    return;
                case ',':
                    _position++;
                    _current = new Token(TokenKind.Comma, ",", null);
                    return;
                case '%':
                    throw new MathAssistantException(MathAssistantErrorCodes.UnexpectedToken, "Символ '%' не поддержан: проценты задаются через явные числовые переменные и деление на 100.");
            }

            if (char.IsDigit(ch) || ch == '.')
            {
                _current = ReadNumber();
                return;
            }

            if (char.IsLetter(ch) || ch == '_')
            {
                _current = ReadIdentifier();
                return;
            }

            throw new MathAssistantException(MathAssistantErrorCodes.UnexpectedToken, $"Недопустимый символ '{ch}'.");
        }

        private Token ReadNumber()
        {
            var start = _position;
            var dotCount = 0;
            while (_position < _text.Length && (char.IsDigit(_text[_position]) || _text[_position] == '.'))
            {
                if (_text[_position] == '.')
                    dotCount++;
                _position++;
            }

            var text = _text[start.._position];
            if (dotCount > 1 ||
                text == "." ||
                !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            {
                throw new MathAssistantException(MathAssistantErrorCodes.InvalidNumber, $"Недопустимое число '{text}'.");
            }

            return new Token(TokenKind.Number, text, value);
        }

        private Token ReadIdentifier()
        {
            var start = _position;
            _position++;
            while (_position < _text.Length && (char.IsLetterOrDigit(_text[_position]) || _text[_position] == '_'))
                _position++;

            return new Token(TokenKind.Identifier, _text[start.._position], null);
        }

        private void SkipWhitespace()
        {
            while (_position < _text.Length && char.IsWhiteSpace(_text[_position]))
                _position++;
        }
    }

    private sealed class MathAssistantException : Exception
    {
        public MathAssistantException(string code, string message) : base(message)
        {
            Code = code;
        }

        public string Code { get; }
    }

    private enum TokenKind
    {
        End,
        Number,
        Identifier,
        Plus,
        Minus,
        Star,
        Slash,
        LeftParen,
        RightParen,
        Comma
    }

    private readonly record struct Token(TokenKind Kind, string Text, decimal? Number);
}
