using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>Reads a GM choice from retained exchange DATA without granting source authority.</summary>
internal static class SpiritualWoundNoRollMargin
{
    internal const string Property = "spiritualWoundHarmfulMargins";

    /// <summary>Validates every supplied side, distinguishing a missing choice from explicit zero.</summary>
    /// <param name="exchange">Original or retained exchange whose normal rules remain separately validated.</param>
    /// <param name="affectedSide">Exact side for which the caller needs a choice.</param>
    /// <returns>No-roll classification and optional actual signed choice, never an inferred value.</returns>
    internal static (bool IsVoluntaryNoRoll, long? ChosenMargin) Read(JsonObject exchange, string affectedSide)
    {
        Require(affectedSide is "player" or "opposition", "Exact affected side required.");
        var noRoll = !exchange.ContainsKey("diceAudit") && ValidationService.IsExplicitVoluntaryNonContest(exchange);
        if (!exchange.ContainsKey(Property)) return (noRoll, null);
        Require(noRoll, "GM wound margins require an explicit voluntary exchange without a dice audit.");
        Require(!exchange.ContainsKey("matchupAudit") || exchange["matchupAudit"] is JsonObject,
            "A supplied matchup audit must be an object.");
        var margins = exchange[Property] as JsonObject ?? throw new FormatException("GM wound margins must be an object.");
        Require(margins.Count > 0, "At least one explicit GM wound margin is required.");
        foreach (var pair in margins)
        {
            Require(pair.Key is "player" or "opposition", "Exact affected margin side required.");
            var before = SpiritualWoundOpportunityMath.StrainRank(Text(exchange["before"]?[pair.Key + "SideStrain"]));
            var after = SpiritualWoundOpportunityMath.StrainRank(Text(exchange["after"]?[pair.Key + "SideStrain"]));
            Require(before >= 0 && after > before, "A supplied GM margin must belong to an increasing-strain side.");
            Integer(pair.Value, long.MinValue, long.MaxValue);
        }
        return (true, margins.ContainsKey(affectedSide)
            ? Integer(margins[affectedSide], long.MinValue, long.MaxValue) : null);
    }
}
