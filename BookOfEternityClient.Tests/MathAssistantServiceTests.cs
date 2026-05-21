using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MathAssistantServiceTests
{
    private readonly MathAssistantService _service = new();

    [Fact]
    public void Evaluate_ArithmeticVariablesAndParentheses_ReturnsDecimalResult()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest(
            "(base + bonus * 2) / divisor",
            new Dictionary<string, decimal>
            {
                ["base"] = 10m,
                ["bonus"] = 4m,
                ["divisor"] = 3m
            }));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("(base+bonus*2)/divisor", result.NormalizedExpression);
        Assert.Equal(6m, result.Result);
        Assert.Equal(6m, result.RawResult);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public void Evaluate_MinMaxClampAndRound_SupportsCommonGameFormulaHelpers()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest(
            "round(clamp(baseReward * difficultyMultiplier, 0, 175) + max(inkBonus, sparkBonus) - min(3, penalty), 0)",
            new Dictionary<string, decimal>
            {
                ["baseReward"] = 80m,
                ["difficultyMultiplier"] = 1.5m,
                ["inkBonus"] = 12m,
                ["sparkBonus"] = 8m,
                ["penalty"] = 9m
            }));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(129m, result.Result);
    }

    [Fact]
    public void Evaluate_PercentageVariables_RequireExplicitDivisionByOneHundred()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest(
            "baseCost * discountPercent / 100",
            new Dictionary<string, decimal>
            {
                ["baseCost"] = 250m,
                ["discountPercent"] = 15m
            },
            RoundingMode: MathAssistantRoundingMode.AwayFromZero,
            DecimalPlaces: 0));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(37.5m, result.RawResult);
        Assert.Equal(38m, result.Result);
    }

    [Theory]
    [InlineData(MathAssistantRoundingMode.Floor, 12)]
    [InlineData(MathAssistantRoundingMode.Ceiling, 13)]
    [InlineData(MathAssistantRoundingMode.ToZero, 12)]
    [InlineData(MathAssistantRoundingMode.AwayFromZero, 13)]
    [InlineData(MathAssistantRoundingMode.ToNearest, 12)]
    public void Evaluate_RequestRoundingMode_AppliesAfterRawResult(MathAssistantRoundingMode mode, decimal expected)
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest(
            "25 / 2",
            RoundingMode: mode,
            DecimalPlaces: 0));

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(12.5m, result.RawResult);
        Assert.Equal(expected, result.Result);
        Assert.Equal(mode, result.RoundingMode);
    }

    [Fact]
    public void Evaluate_MissingVariable_ReturnsStructuredError()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest("base + missing", new Dictionary<string, decimal>
        {
            ["base"] = 10m
        }));

        Assert.False(result.Success);
        Assert.Equal(MathAssistantErrorCodes.MissingVariable, result.ErrorCode);
        Assert.Contains("missing", result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_AmbiguousText_ReturnsStructuredErrorWithoutThrowing()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest("2 apples + 3"));

        Assert.False(result.Success);
        Assert.Equal(MathAssistantErrorCodes.UnexpectedToken, result.ErrorCode);
        Assert.Null(result.Result);
    }

    [Fact]
    public void Evaluate_DivideByZero_ReturnsStructuredError()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest("100 / (5 - 5)"));

        Assert.False(result.Success);
        Assert.Equal(MathAssistantErrorCodes.DivisionByZero, result.ErrorCode);
    }

    [Fact]
    public void Evaluate_Overflow_ReturnsStructuredError()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest("huge * 2", new Dictionary<string, decimal>
        {
            ["huge"] = decimal.MaxValue
        }));

        Assert.False(result.Success);
        Assert.Equal(MathAssistantErrorCodes.Overflow, result.ErrorCode);
    }

    [Fact]
    public void Evaluate_ExponentNotation_IsRejectedInsteadOfProducingNonFiniteNumber()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest("1e309"));

        Assert.False(result.Success);
        Assert.Equal(MathAssistantErrorCodes.UnexpectedToken, result.ErrorCode);
    }

    [Fact]
    public void Evaluate_ShiningTreasuryInterestFormula_IsDeterministicAndCapped()
    {
        var request = new MathAssistantEvaluationRequest(
            "min(depositedInkFeathers * basisPoints / 10000, cycleCap)",
            new Dictionary<string, decimal>
            {
                ["depositedInkFeathers"] = 1_000m,
                ["basisPoints"] = 150m,
                ["cycleCap"] = 25m
            },
            RoundingMode: MathAssistantRoundingMode.Floor,
            DecimalPlaces: 0);

        var first = _service.Evaluate(request);
        var second = _service.Evaluate(request);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Equal(15m, first.RawResult);
        Assert.Equal(15m, first.Result);
        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.NormalizedExpression, second.NormalizedExpression);
    }

    [Fact]
    public void Evaluate_UnsupportedFunction_ReturnsStructuredError()
    {
        var result = _service.Evaluate(new MathAssistantEvaluationRequest("random(1, 20)"));

        Assert.False(result.Success);
        Assert.Equal(MathAssistantErrorCodes.UnknownFunction, result.ErrorCode);
    }

    [Fact]
    public void Evaluate_SameInputRepeated_ProducesSameResultAndNormalizedExpression()
    {
        var request = new MathAssistantEvaluationRequest("clamp(score + tier * 4, 0, 100)", new Dictionary<string, decimal>
        {
            ["score"] = 73m,
            ["tier"] = 8m
        });

        var first = _service.Evaluate(request);
        var second = _service.Evaluate(request);

        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Equal(first.NormalizedExpression, second.NormalizedExpression);
        Assert.Equal(first.Result, second.Result);
        Assert.Equal(first.RawResult, second.RawResult);
    }
}
