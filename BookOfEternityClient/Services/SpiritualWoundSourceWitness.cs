using System.Text;
using System.Text.Json.Nodes;
using static BookOfEternityClient.Services.SpiritualWoundStateJson;

namespace BookOfEternityClient.Services;

/// <summary>
/// Checks intrinsic durable source evidence without granting original-turn or publication authority.
/// Owning roots reject duplicate properties before invoking this component.
/// </summary>
internal static class SpiritualWoundSourceWitness
{
    private const string Fields = "sourceId coordinate conflictId exchangeId turnEvidence sourceOrdinal exchangeOrdinal " +
        "affectedSide actingActor affectedActor operation appliedArtId appliedArtKind appliedArtTier targetResilienceTier " +
        "priorStrainRank destinationStrainRank extraStrainJumps harmfulMargin maximumSeverityRank guaranteedSeverityRank " +
        "dangerMode dangerDeclarationFingerprint escalationFingerprint retraumaWoundId retraumaWoundJsonBase64 " +
        "exchangeJsonBase64 specialArtJsonBase64 selectedDiceIndices resourceResultFingerprint effectPlanFingerprint " +
        "conflictBeforeFingerprint conflictAfterFingerprint sourceFingerprint";

    /// <summary>
    /// Validates closed fields and joins retained exchange, dice, art and wound evidence before recomputing severity.
    /// Original membership, execution and provenance require independent source-owner reconstruction.
    /// </summary>
    /// <param name="source">
    /// Detached source witness; validation does not mutate its contents.
    /// </param>
    /// <returns>
    /// Recomputed comparison result, never source admission or a wound command.
    /// </returns>
    internal static SpiritualWoundCalculation Validate(JsonObject source)
    {
        ArgumentNullException.ThrowIfNull(source);
        Closed(source, Fields);
        var digest = Hash(source, "source", "sourceId", "sourceFingerprint");
        Require(Fingerprint(source["sourceFingerprint"]) == digest &&
            Text(source["sourceId"]) == "spiritual_source_" + digest[7..], "Source digest or identity differs.");
        Text(source["coordinate"]);
        Text(source["conflictId"]);
        var exchangeId = Text(source["exchangeId"]);
        var evidence = Object(source["turnEvidence"]);
        var exchangeOrdinal = (int)Integer(source["exchangeOrdinal"]);
        SpiritualWoundTurnEvidence.Validate(evidence, exchangeOrdinal);
        Integer(source["sourceOrdinal"], 0, Integer(evidence["bounds"]!["sourceSlotCount"]) - 1);
        var side = Text(source["affectedSide"]);
        Require(side is "player" or "opposition", "Invalid affected side.");
        var actor = Actor(source["actingActor"]);
        var affected = Actor(source["affectedActor"]);
        Require(actor != affected, "A source cannot act from both opposing actor coordinates.");
        var operation = Text(source["operation"]);
        Require(AfterlifeEntityProfileState.StandardArtIds.Any(id => id == operation) &&
            operation != "champion_coordination", "Unsupported admitted source operation.");
        var tier = (int)Integer(source["appliedArtTier"], 0, 5);
        var resilience = (int)Integer(source["targetResilienceTier"], 0, 5);
        foreach (var field in new[] { "dangerDeclarationFingerprint", "resourceResultFingerprint",
            "effectPlanFingerprint", "conflictBeforeFingerprint", "conflictAfterFingerprint" })
            Fingerprint(source[field]);
        if (source["escalationFingerprint"] is not null) Fingerprint(source["escalationFingerprint"]);

        var exchange = Payload(source["exchangeJsonBase64"]);
        Require(Text(exchange["exchangeId"]) == exchangeId, "Exchange identity differs.");
        foreach (var marker in new[] { "turnNumber", "exchangeAtTurn", "resolvedAtTurn" })
            if (exchange.ContainsKey(marker))
                Require(Integer(exchange[marker], 1) == Integer(evidence["turn"]), "Exchange turn differs.");
        var issues = new List<ValidationIssue>();
        ValidationService.ValidateRetainedSpiritualExchangeShape(exchange, "retainedSource.exchange", issues);
        ValidationService.ValidateSpiritualWoundSourceActionShape(exchange, "retainedSource.exchange", issues);
        ValidationService.ValidateRetainedSpiritualDiceAudit(exchange, evidence, issues);
        ValidationService.ValidateRetainedSpiritualExchangeRules(exchange, issues);
        Require(!issues.Any(issue => issue.Severity == IssueSeverity.Error), "Invalid retained exchange payload.");
        var sourceOperation = side == "opposition" ? exchange["operationType"] :
            exchange["matchupAudit"]?["oppositionOperation"] ??
            exchange["incomingAction"]?["finalOperationType"] ?? exchange["incomingAction"]?["operationType"];
        Require(string.Equals(Text(sourceOperation), operation, StringComparison.OrdinalIgnoreCase),
            "Acting-side operation differs.");
        ValidateActingActor(exchange, side, actor);
        if (AfterlifeActionCostRules.HasCost(operation))
        {
            var costSide = side == "player" ? "opposition" : "player";
            var cost = Object(Object(exchange["actionCostAudit"])[costSide]);
            Require(string.Equals(Text(cost["operationType"]), operation, StringComparison.OrdinalIgnoreCase),
                "Acting-side cost operation differs.");
            var costTier = Integer(cost["artTier"], 0, 5);
            // Player cost authority is the soul even when its conflict side has a different lead.
            if (side == "player" || actor.Kind == "player_soul")
                Require(costTier == tier,
                    "Applied tier differs from retained acting-side cost evidence.");
        }

        var before = Object(exchange["before"]);
        var after = Object(exchange["after"]);
        var previousStrain = Text(before[side + "SideStrain"]);
        var nextStrain = Text(after[side + "SideStrain"]);
        var priorRank = SpiritualWoundOpportunityMath.StrainRank(previousStrain);
        var nextRank = SpiritualWoundOpportunityMath.StrainRank(nextStrain);
        Require(priorRank >= 0 && nextRank > priorRank, "Source requires increasing strain.");
        Require(Integer(source["priorStrainRank"], 0, 4) == priorRank &&
            Integer(source["destinationStrainRank"], 0, 4) == nextRank, "Retained strain differs.");
        Require(Integer(source["extraStrainJumps"], 0, 3) == Math.Max(0, nextRank - priorRank - 1),
            "Extra strain jumps differ.");
        var audit = Object(exchange["diceAudit"]);
        var player = Integer(audit["playerTotal"], int.MinValue, int.MaxValue);
        var opposition = Integer(audit["oppositionTotal"], int.MinValue, int.MaxValue);
        Require(Integer(audit["margin"], long.MinValue, long.MaxValue) == player - opposition, "Exchange margin differs.");
        var margin = side == "opposition" ? player - opposition : opposition - player;
        Require(Integer(source["harmfulMargin"], long.MinValue, long.MaxValue) == margin, "Affected-side margin differs.");
        ValidateDiceSelection(source, evidence, audit);

        var kind = Text(source["appliedArtKind"]);
        Require(kind is "standard" or "special", "Invalid applied art kind.");
        JsonObject? art = null;
        var envelope = new SpiritualWoundSourceEnvelope(4, null);
        if (kind == "standard")
        {
            Require(source["specialArtJsonBase64"] is null &&
                Text(source["appliedArtId"]) == actor.Kind + ":" + actor.Id + ":" + operation,
                "Standard source cannot invent an acquired art.");
        }
        else
        {
            art = Payload(source["specialArtJsonBase64"]);
            Require(!ValidationService.ValidateRetainedSpiritualSpecialArt(art, actor.Kind, actor.Id,
                Text(evidence["realm"])).Any(issue => issue.Severity == IssueSeverity.Error), "Invalid original art payload.");
            Require(Text(art["artId"]) == Text(source["appliedArtId"]) &&
                NormalizeActorKind(Text(art["ownerActorType"])) == actor.Kind && Text(art["ownerActorId"]) == actor.Id &&
                Integer(art["tier"], 0, 5) == tier &&
                string.Equals(Text(art["baseOperation"]), operation, StringComparison.OrdinalIgnoreCase),
                "Original applied-art binding differs.");
            Require(SpiritualWoundSourceEnvelope.TryRead(art, out var declared, out _), "Invalid original wound envelope.");
            envelope = declared!;
        }
        ValidationService.ValidateRetainedSpiritualArtBinding(exchange, side, operation, art, issues);
        Require(!issues.Any(issue => issue.Severity == IssueSeverity.Error), "Applied-art audit differs.");
        var calculated = SpiritualWoundOpportunityMath.Calculate(new(margin, tier, resilience,
            previousStrain, nextStrain, Text(source["dangerMode"]), envelope.MaximumSeverityRank));
        Require(calculated is not null && Integer(source["maximumSeverityRank"], 0, 4) == calculated.MaximumSeverityRank,
            "Source severity differs from retained exchange calculation.");
        var guarantee = source["guaranteedSeverityRank"] is null ? (int?)null :
            (int)Integer(source["guaranteedSeverityRank"], 1, envelope.MaximumSeverityRank);
        Require(guarantee == envelope.GuaranteedSeverityRank, "Source guarantee differs from original art.");
        ValidateTarget(source, exchange, affected, Text(evidence["realm"]), side);
        return calculated!;
    }

    /// <summary>
    /// Compares every explicit incoming actor alias without inventing an omitted roster lead.
    /// </summary>
    /// <param name="exchange">
    /// Retained exchange payload.
    /// </param>
    /// <param name="side">
    /// Affected side; only player harm originates from an incoming action.
    /// </param>
    /// <param name="actor">
    /// Retained acting actor coordinate.
    /// </param>
    private static void ValidateActingActor(JsonObject exchange, string side, (string Kind, string Id) actor)
    {
        if (side != "player" || exchange["incomingAction"] is not JsonObject incoming) return;
        var kinds = new[] { "actorType", "ownerActorType" }.Where(incoming.ContainsKey).ToArray();
        var ids = new[] { "actorId", "actorRef", "ownerActorId", "guardianId", "id" }.Where(incoming.ContainsKey).ToArray();
        if (kinds.Length + ids.Length == 0) return;
        Require(kinds.Length > 0 && ids.Length > 0, "Incomplete incoming actor identity.");
        var kind = Text(incoming[kinds[0]]);
        Require(kinds.All(key => Text(incoming[key]) == kind), "Conflicting incoming actor kinds.");
        kind = NormalizeActorKind(kind);
        Require(kind == actor.Kind && ids.All(key => Text(incoming[key]) == actor.Id),
            "Explicit incoming actor differs.");
    }

    /// <summary>
    /// Checks source selection against every retained claim and exchange die entry.
    /// </summary>
    /// <param name="source">
    /// Closed witness.
    /// </param>
    /// <param name="evidence">
    /// Validated durable turn context.
    /// </param>
    /// <param name="audit">
    /// Owning dice audit whose intrinsic rules have passed.
    /// </param>
    private static void ValidateDiceSelection(JsonObject source, JsonObject evidence, JsonObject audit)
    {
        var selected = source["selectedDiceIndices"] as JsonArray ?? throw new FormatException("Selected dice required.");
        var claims = evidence["diceClaims"]!.AsArray();
        var used = audit["diceUsed"] as JsonArray ?? throw new FormatException("Exchange dice required.");
        Require(selected.Count == claims.Count && used.Count == claims.Count, "Dice selection count differs.");
        for (var index = 0; index < claims.Count; index++)
        {
            var claim = Object(claims[index]);
            var sourceIndex = Integer(claim["sourceIndex"]);
            Require(Integer(selected[index]) == sourceIndex, "Selected dice order differs.");
            var matches = used.Select(Object).Where(die => Integer(die["sourceIndex"]) == sourceIndex).ToArray();
            Require(matches.Length == 1 && Integer(matches[0]["value"], 1, 20) == Integer(claim["value"]),
                "Exchange dice differ from retained claims.");
        }
    }

    /// <summary>
    /// Joins an explicit target to its affected actor and optional parsed older spiritual wound.
    /// </summary>
    /// <param name="source">
    /// Closed witness.
    /// </param>
    /// <param name="exchange">
    /// Retained exchange action.
    /// </param>
    /// <param name="affected">
    /// Validated affected actor coordinate.
    /// </param>
    /// <param name="realm">
    /// Exact retained spiritual realm.
    /// </param>
    /// <param name="side">
    /// Exact affected side.
    /// </param>
    private static void ValidateTarget(JsonObject source, JsonObject exchange,
        (string Kind, string Id) affected, string realm, string side)
    {
        var action = side == "opposition" ? exchange : exchange["incomingAction"] as JsonObject ?? exchange;
        var target = action["spiritualWoundTarget"] as JsonObject;
        if (target is not null)
        {
            var kind = Text(target["actorType"]);
            kind = NormalizeActorKind(kind);
            Require(kind == affected.Kind && Text(target["actorId"]) == affected.Id, "Explicit affected actor differs.");
        }
        if (source["retraumaWoundId"] is null)
        {
            Require(source["retraumaWoundJsonBase64"] is null && target?["retraumaWoundRef"] is null,
                "Unbound older wound payload or target.");
            return;
        }
        var id = Text(source["retraumaWoundId"]);
        Require(target is not null && Text(target["retraumaWoundRef"]) == id, "Explicit older wound target differs.");
        var woundJson = Payload(source["retraumaWoundJsonBase64"]);
        var parsed = WoundMaterializationContract.Parse(woundJson.ToJsonString(), "retainedSource.retraumaWound");
        Require(parsed.IsValid, "Invalid retained older wound.");
        var wound = parsed.Wound!;
        var ownerKind = affected.Kind switch
        {
            "player_soul" => "player_soul", "guardian" => "guardian",
            "resident" or "shining_resident" => "resident", "radiant_actor" => "radiant_actor",
            _ => "afterlife_actor"
        };
        Require(wound.WoundId == id && wound.Lifecycle == "active" && wound.Classification.Domain == "spiritual" &&
            wound.Owner == new WoundOwnerCoordinate(realm, ownerKind, affected.Id, WoundCarrierCatalog.AfterlifeProfilesPath),
            "Retained older wound owner or lifecycle differs.");
    }

    /// <summary>
    /// Preserves the source owner's case-insensitive aliases for the player soul.
    /// </summary>
    /// <param name="kind">
    /// Exact incoming actor-kind text.
    /// </param>
    /// <returns>
    /// Canonical player kind for a soul alias; otherwise the unchanged kind.
    /// </returns>
    private static string NormalizeActorKind(string kind) =>
        new[] { "player", "soul", "player_soul" }.Contains(kind, StringComparer.OrdinalIgnoreCase) ? "player_soul" : kind;

    /// <summary>
    /// Reads an exact registered actor coordinate.
    /// </summary>
    /// <param name="node">
    /// Required closed actor object.
    /// </param>
    /// <returns>
    /// Unchanged actor kind and identifier.
    /// </returns>
    private static (string Kind, string Id) Actor(JsonNode? node)
    {
        var actor = Object(node);
        Closed(actor, "actorKind actorId");
        var kind = Text(actor["actorKind"]);
        var id = Text(actor["actorId"]);
        Require(AfterlifeEntityProfileState.ActorTypes.Contains(kind), "Unknown afterlife actor kind.");
        Require(NormalizeActorKind(kind) == kind, "Durable player soul kind must be canonical.");
        Require((kind == "player_soul") == (id == "player_soul"), "Invalid player soul coordinate.");
        return (kind, id);
    }

    /// <summary>
    /// Reads canonical encoded JSON for a retained semantic payload.
    /// </summary>
    /// <param name="node">
    /// Required strict base64 object bytes.
    /// </param>
    /// <returns>
    /// Detached duplicate-free object.
    /// </returns>
    private static JsonObject Payload(JsonNode? node)
    {
        var bytes = DecodeJsonObjectBytes(node);
        var json = Encoding.UTF8.GetString(bytes);
        var result = Parse(json);
        Require(Canonical(result) == json, "Retained semantic payload must be canonical.");
        return result;
    }

    /// <summary>
    /// Requires an object without creating a permissive default.
    /// </summary>
    /// <param name="node">
    /// Required object node.
    /// </param>
    /// <returns>
    /// Borrowed object from the caller's detached tree.
    /// </returns>
    private static JsonObject Object(JsonNode? node) => node as JsonObject ?? throw new FormatException("Object required.");
}
