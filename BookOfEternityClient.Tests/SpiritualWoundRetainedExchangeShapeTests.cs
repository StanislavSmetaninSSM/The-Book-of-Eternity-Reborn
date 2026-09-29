using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Exercises the owning exchange-shape checks shared by live and historical payload validation.
/// These projections alone do not prove a complete exchange or its original source authority.
/// </summary>
public sealed class SpiritualWoundRetainedExchangeShapeTests
{
    /// <summary>
    /// Accepts a measurable exchange projection without changing its retained JSON.
    /// </summary>
    [Fact]
    public void Validate_AcceptsMeasurableShapeWithoutMutation()
    {
        var exchange = Exchange();
        var original = exchange.ToJsonString();
        Assert.Empty(Validate(exchange));
        Assert.Equal(original, exchange.ToJsonString());
    }

    /// <summary>
    /// Retains the original shape and outcome/state-change rules in the shared owner.
    /// </summary>
    /// <param name="mutation">
    /// One independent exchange-shape violation.
    /// </param>
    [Theory]
    [InlineData("id")]
    [InlineData("operation")]
    [InlineData("outcome")]
    [InlineData("before")]
    [InlineData("after")]
    [InlineData("no_effect_changed")]
    [InlineData("success_unchanged")]
    [InlineData("blocked_missing_incoming")]
    [InlineData("countered_missing_incoming")]
    public void Validate_RejectsShapeViolation(string mutation)
    {
        var exchange = Exchange();
        switch (mutation)
        {
            case "id": exchange.Remove("exchangeId"); break;
            case "operation": exchange["operationType"] = "unsupported"; break;
            case "outcome": exchange["outcome"] = "unsupported"; break;
            case "before": exchange.Remove("before"); break;
            case "after": exchange["after"] = "invalid"; break;
            case "no_effect_changed": exchange["outcome"] = "no_effect"; break;
            case "success_unchanged": exchange["after"] = exchange["before"]!.DeepClone(); break;
            case "blocked_missing_incoming": exchange["outcome"] = "blocked"; break;
            case "countered_missing_incoming": exchange["outcome"] = "countered"; break;
        }
        Assert.Contains(Validate(exchange), issue => issue.Severity == IssueSeverity.Error);
    }

    /// <summary>
    /// Builds the narrow shape projection without pretending it is a complete admitted exchange.
    /// </summary>
    /// <returns>
    /// Mutable shape fixture.
    /// </returns>
    private static JsonObject Exchange() => new()
    {
        ["exchangeId"] = "exchange-a", ["operationType"] = "pressure", ["outcome"] = "success",
        ["before"] = new JsonObject { ["oppositionSideStrain"] = "clear" },
        ["after"] = new JsonObject { ["oppositionSideStrain"] = "strained" }
    };

    /// <summary>
    /// Calls the shared owning shape validator without constructing fabricated authority contexts.
    /// </summary>
    /// <param name="exchange">
    /// Projection whose intrinsic shape is under test.
    /// </param>
    /// <returns>
    /// Collected shape diagnostics; other exchange checks remain separate owning responsibilities.
    /// </returns>
    private static IReadOnlyList<ValidationIssue> Validate(JsonObject exchange)
    {
        var method = typeof(ValidationService).GetMethod("ValidateRetainedSpiritualExchangeShape",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        var issues = new List<ValidationIssue>();
        method.Invoke(null, new object[] { exchange, "retainedSource.exchange", issues });
        return issues;
    }
}
