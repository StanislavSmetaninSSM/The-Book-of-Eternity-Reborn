using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>Checked failure DATA for one fixed incoming guardian pressure action, never execution authority.</summary>
internal sealed record ValidatedSpiritualWoundFailure(string ConflictId, string ExchangeId, int ExchangeOrdinal,
    string ActorId)
{
    /// <summary>Identifies the sole supported affected side.</summary>
    internal string Side => "opposition";
    /// <summary>Identifies the fixed local incoming-action slot, never an arbitrary JSON pointer.</summary>
    internal string ActionSlot => "/incomingAction";
}

/// <summary>Reads the closed guardian declaration separately from the player's harmful source action.</summary>
internal static class SpiritualWoundFailedAction
{
    internal const string Property = "spiritualWoundFailedAction";
    internal const string Role = "existing_guardian_pressure_failed_action_v1";
    /// <summary>Separates complete guardian group comparison DATA from the unchanged singleton wire role.</summary>
    internal const string GroupRole = "existing_guardian_pressure_failed_action_group_v1";

    /// <summary>Checks only exact conflict-model DATA; this predicate grants no roster or current-owner authority.</summary>
    /// <param name="conflict">Detached or caller-authenticated conflict DATA.</param>
    /// <returns>True only for the one supported, explicitly declared direct-duel model.</returns>
    internal static bool HasDirectDuelModel(JsonObject conflict) => conflict["sideModel"] is JsonValue value &&
        value.TryGetValue<string>(out var model) && model == "direct_duel";

    /// <summary>Checks declaration shape and all incoming aliases without claiming the exchange has been validated.</summary>
    /// <param name="exchange">Complete local exchange containing the required declaration.</param>
    /// <returns>The exact declared guardian identifier; malformed or unsupported DATA throws.</returns>
    internal static string ReadGuardianDeclaration(JsonObject exchange)
    {
        var declaration = exchange[Property] as JsonObject ?? throw new FormatException("Guardian failed-action declaration required.");
        Closed(declaration, "version side actorType actorId actionPath operationType");
        Require(Integer(declaration["version"], 1, 1) == 1 && Text(declaration["side"]) == "opposition" &&
            Text(declaration["actorType"]) == "guardian" && Text(declaration["actionPath"]) == "/incomingAction" &&
            Text(declaration["operationType"]) == "pressure", "Unsupported failed participant or action slot.");
        var actorId = Text(declaration["actorId"]);
        Require(actorId != "player_soul" && actorId == actorId.Trim(), "Exact guardian identifier required.");
        Require(Token(exchange["operationType"], "pressure") && Token(exchange["outcome"], "success") &&
            exchange["incomingAction"] is JsonObject, "Guardian attribution requires player pressure/success and incoming pressure.");
        var incoming = exchange["incomingAction"]!.AsObject();
        Require(Token(incoming["operationType"], "pressure") &&
            (!incoming.ContainsKey("finalOperationType") || Token(incoming["finalOperationType"], "pressure")),
            "The declared incoming pressure action differs.");
        foreach (var field in new[] { "actorType", "ownerActorType" })
            if (incoming.ContainsKey(field)) Require(Text(incoming[field]) == "guardian", "Incoming guardian type alias differs.");
        foreach (var field in new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" })
            if (incoming.ContainsKey(field)) Require(Text(incoming[field]) == actorId, "Incoming guardian identity alias differs.");
        Require(!exchange.ContainsKey("specialArtAudit") && !exchange.ContainsKey("specialArtAudits") &&
            exchange["actionCostAudit"]?["player"]?["specialArtId"] is null &&
            exchange["actionCostAudit"]?["opposition"]?["specialArtId"] is null &&
            !exchange.ContainsKey("spiritualWoundTarget") && !incoming.ContainsKey("spiritualWoundTarget"),
            "Guardian attribution supports standard actions without explicit wound targets only.");
        return actorId;
    }

    /// <summary>Binds declaration DATA after the caller has validated the complete ordinary or actual exchange.</summary>
    /// <param name="conflict">Caller-authenticated conflict with exact current participants.</param>
    /// <param name="exchange">Complete exchange whose dice, costs and outcome already passed.</param>
    /// <param name="ordinal">Actual local exchange ordinal supplied by its owner.</param>
    /// <returns>Checked immutable action DATA, never ownership or permission.</returns>
    internal static ValidatedSpiritualWoundFailure ReadValidated(JsonObject conflict, JsonObject exchange, int ordinal)
    {
        var actorId = ReadGuardianDeclaration(exchange);
        Require(HasDirectDuelModel(conflict), "Guardian attribution requires the exact direct_duel conflict model.");
        Require(ordinal == 0 && ValidationService.SpiritualWoundSourceSession.ActorKey(
            conflict["playerSide"]?["leadContestant"] as JsonObject) == "player_soul:player_soul" &&
            ValidationService.SpiritualWoundSourceSession.ActorKey(conflict["oppositionSide"]?["leadContestant"] as JsonObject) == "guardian:" + actorId,
            "Guardian attribution requires the exact player soul and opposition guardian lead at ordinal zero.");
        return new(Text(conflict["conflictId"]), Text(exchange["exchangeId"]), ordinal, actorId);
    }

    /// <summary>Binds failure DATA after complete intrinsic source validation; exchange DATA cannot establish signed roster or conflict-model authority.</summary>
    /// <param name="source">Fully validated intrinsic harmful source, separate from the failed action.</param>
    /// <param name="exchange">The exact validated actual or ordinary payload.</param>
    /// <returns>Checked retained failure DATA for this witness only.</returns>
    internal static ValidatedSpiritualWoundFailure ReadRetained(JsonObject source, JsonObject exchange)
    {
        var actorId = ReadGuardianDeclaration(exchange);
        Require(Text(source["turnEvidence"]!["realm"]) == "chaos_sea" && Text(source["operation"]) == "pressure" &&
            Text(source["appliedArtKind"]) == "standard" && source["guaranteedSeverityRank"] is null &&
            source["retraumaWoundId"] is null &&
            Text(source["affectedSide"]) == "opposition" && Text(source["affectedActor"]!["actorKind"]) == "guardian" &&
            Text(source["affectedActor"]!["actorId"]) == actorId && Text(source["actingActor"]!["actorKind"]) == "player_soul" &&
            Text(source["actingActor"]!["actorId"]) == "player_soul" && Integer(source["exchangeOrdinal"], 0, 0) == 0,
            "Retained guardian failure belongs to another participant or ordinal.");
        return new(Text(source["conflictId"]), Text(exchange["exchangeId"]), 0, actorId);
    }

    private static bool Token(JsonNode? node, string expected) => node is JsonValue value &&
        value.TryGetValue<string>(out var text) && string.Equals(text, expected, StringComparison.OrdinalIgnoreCase);
}
