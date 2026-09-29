using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Verifies effective operation authority for conditional spiritual wound costs.
/// </summary>
public sealed class SpiritualWoundSpecialActionCostTests
{
    /// <summary>
    /// Preserves opposition final-operation authority when resolving conditional force costs.
    /// </summary>
    /// <param name="declared">
    /// Original incoming action, or <see langword="null"/> when no incoming action exists.
    /// </param>
    /// <param name="final">
    /// Optional effective replacement that must take precedence over the original incoming action.
    /// </param>
    /// <param name="matchup">
    /// Matchup declaration, which cannot override conflicting final-operation evidence.
    /// </param>
    /// <param name="expected">
    /// Effective cost operation, or <see langword="null"/> when the final operation has no ordinary or conditional cost.
    /// </param>
    [Theory]
    [InlineData("guard", "force_incarnation", "force_incarnation", "force_incarnation")]
    [InlineData("force_incarnation", "guard", "force_incarnation", "guard")]
    [InlineData("force_incarnation", "surrender", "force_incarnation", null)]
    [InlineData("guard", null, "force_incarnation", "guard")]
    [InlineData("force_incarnation", null, "force_incarnation", "force_incarnation")]
    [InlineData(null, null, "force_incarnation", "force_incarnation")]
    public void OppositionCostOperation_PreservesFinalOperationPrecedence(
        string? declared, string? final, string matchup, string? expected)
    {
        var exchange = new JsonObject
        {
            ["operationType"] = "guard",
            ["matchupAudit"] = new JsonObject { ["oppositionOperation"] = matchup }
        };
        if (declared != null)
        {
            exchange["incomingAction"] = new JsonObject { ["operationType"] = declared };
            if (final != null)
                exchange["incomingAction"]!["finalOperationType"] = final;
        }
        Assert.Equal(expected, ValidationService.ResolveSpiritualCostOperation(exchange, "opposition"));
        Assert.Equal("guard", ValidationService.ResolveSpiritualCostOperation(exchange, "player"));
    }
}
