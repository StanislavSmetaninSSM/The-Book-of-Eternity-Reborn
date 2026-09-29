using System.Reflection;
using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks durable source comparison evidence independently of live source admission authority.
/// </summary>
public sealed class SpiritualWoundSourceWitnessTests
{
    private const string Fingerprint = "sha256:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    /// <summary>
    /// Accepts a retained standard source without modifying its evidence or authorizing publication.
    /// </summary>
    [Fact]
    public void Validate_AcceptsStandardSourceWithoutMutation()
    {
        var source = Source();
        var before = source.ToJsonString();
        Assert.True(IsValid(source));
        Assert.Equal(before, source.ToJsonString());
    }

    /// <summary>
    /// Requires every source field and rejects extensions to the closed witness object.
    /// </summary>
    [Fact]
    public void Validate_RejectsMissingAndUnknownFields()
    {
        foreach (var key in Source().Select(pair => pair.Key).Append("unknown"))
        {
            var source = Source();
            if (key == "unknown") source[key] = 1;
            else source.Remove(key);
            Assert.False(IsValid(source));
        }
    }

    /// <summary>
    /// Rejects re-sealed changes that disagree with retained exchange, art or dice evidence.
    /// </summary>
    /// <param name="field">
    /// Witness scalar to replace while retaining its embedded source payload.
    /// </param>
    /// <param name="json">
    /// Replacement JSON value.
    /// </param>
    [Theory]
    [InlineData("exchangeId", "\"other-exchange\"")]
    [InlineData("operation", "\"counter\"")]
    [InlineData("appliedArtId", "\"unrelated-art\"")]
    [InlineData("appliedArtKind", "\"special\"")]
    [InlineData("appliedArtTier", "5")]
    [InlineData("priorStrainRank", "1")]
    [InlineData("destinationStrainRank", "2")]
    [InlineData("extraStrainJumps", "1")]
    [InlineData("harmfulMargin", "11")]
    [InlineData("maximumSeverityRank", "2")]
    [InlineData("guaranteedSeverityRank", "1")]
    [InlineData("selectedDiceIndices", "[1,0]")]
    [InlineData("sourceOrdinal", "4")]
    [InlineData("exchangeOrdinal", "2")]
    public void Validate_RejectsChangedWitnessScalar(string field, string json)
    {
        var source = Source();
        source[field] = JsonNode.Parse(json);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Retains a harmful training source with zero ceiling and no invented guarantee.
    /// </summary>
    [Fact]
    public void Validate_AcceptsZeroCeilingTrainingSource()
    {
        var source = Source();
        source["dangerMode"] = "training";
        source["maximumSeverityRank"] = 0;
        Seal(source);
        Assert.True(IsValid(source));
    }

    /// <summary>
    /// Recomputes dice totals from retained rolls and modifiers instead of trusting duplicated scalars.
    /// </summary>
    [Fact]
    public void Validate_RejectsSelfConsistentForgedTotals()
    {
        var source = Source();
        var exchange = Exchange();
        exchange["diceAudit"]!["playerTotal"] = 16;
        exchange["diceAudit"]!["margin"] = 11;
        source["harmfulMargin"] = 11;
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Preserves the source owner's optional supported exchange turn markers.
    /// </summary>
    /// <param name="marker">
    /// Supported marker, or <see langword="null"/> for an unmarked original exchange.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("exchangeAtTurn")]
    [InlineData("resolvedAtTurn")]
    public void Validate_AcceptsOptionalTurnMarkers(string? marker)
    {
        var source = Source();
        var exchange = Exchange();
        exchange.Remove("turnNumber");
        if (marker is not null) exchange[marker] = 42;
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
    }

    /// <summary>
    /// Rejects every present conflicting turn marker even when turnNumber remains correct.
    /// </summary>
    /// <param name="marker">
    /// Supported marker whose value conflicts with the retained turn.
    /// </param>
    [Theory]
    [InlineData("turnNumber")]
    [InlineData("exchangeAtTurn")]
    [InlineData("resolvedAtTurn")]
    public void Validate_RejectsConflictingTurnMarker(string marker)
    {
        var source = Source();
        var exchange = Exchange();
        exchange[marker] = 43;
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Retains an original special-art guarantee only when its exact payload and applied audit agree.
    /// </summary>
    /// <param name="change">
    /// Embedded audit contradiction introduced after the valid control.
    /// </param>
    [Theory]
    [InlineData("owner")]
    [InlineData("specialArtId")]
    [InlineData("specialCostMultiplierPercent")]
    [InlineData("standardEffectiveCost")]
    [InlineData("ambiguous")]
    [InlineData("owner-alias")]
    [InlineData("original-owner-alias")]
    [InlineData("audit-owner-alias")]
    [InlineData("owner-id-equivalence")]
    public void Validate_JoinsOriginalSpecialArtAndGuarantee(string change)
    {
        var source = Source();
        var exchange = Exchange();
        var art = JsonNode.Parse("""
            {"artId":"art-source","displayName":"Нить надлома","effectSummary":"Духовное давление.",
             "ownerActorType":"player_soul","ownerActorId":"player_soul","baseOperation":"pressure","tier":0,
             "costMultiplierPercent":200,"upgradeCost":{"inkFeathers":1},"canTeachPlayer":false,"trainingConditions":[],
             "spiritualWoundEnvelope":{"schemaVersion":1,"maximumSeverityRank":1,"guaranteedSeverityRank":1}}
            """)!.AsObject();
        exchange["specialArtAudit"] = JsonNode.Parse("""
            {"artId":"art-source","ownerActorType":"player_soul","ownerActorId":"player_soul",
             "baseOperation":"pressure","costMultiplierPercent":200,"effectNote":"Надлом духовного узора."}
            """);
        exchange["actionCostAudit"]!["player"]!["specialArtId"] = "art-source";
        exchange["actionCostAudit"]!["player"]!["specialCostMultiplierPercent"] = 200;
        exchange["actionCostAudit"]!["player"]!["standardEffectiveCost"] = 3;
        exchange["actionCostAudit"]!["player"]!["effectiveCost"] = 6;
        exchange["actionCostAudit"]!["player"]!["after"] = 0;
        source["appliedArtKind"] = "special";
        source["appliedArtId"] = "art-source";
        source["guaranteedSeverityRank"] = 1;
        source["specialArtJsonBase64"] = Encode(art);
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
        if (change is "owner-alias" or "original-owner-alias" or "audit-owner-alias")
        {
            if (change != "audit-owner-alias") art["ownerActorType"] = "PLAYER_SOUL";
            if (change != "original-owner-alias") exchange["specialArtAudit"]!["ownerActorType"] = "PLAYER_SOUL";
            source["specialArtJsonBase64"] = Encode(art);
            source["exchangeJsonBase64"] = Encode(exchange);
            Seal(source);
            Assert.True(IsValid(source));
            return;
        }
        if (change == "owner-id-equivalence")
        {
            exchange["specialArtAudit"]!["ownerActorId"] = "foreign-owner";
            source["exchangeJsonBase64"] = Encode(exchange);
            Seal(source);
            Assert.True(IsValid(source));
            return;
        }
        if (change == "owner")
        {
            exchange["specialArtAudit"]!["ownerActorType"] = "guardian";
            exchange["specialArtAudit"]!["ownerActorId"] = "foreign-owner";
        }
        else if (change == "ambiguous")
            exchange["specialArtAudits"] = JsonNode.Parse("""
                [{"artId":"art-other","ownerActorType":"guardian","ownerActorId":"guardian-a",
                  "baseOperation":"pressure","costMultiplierPercent":200,"effectNote":"Встречный эффект."}]
                """);
        else exchange["actionCostAudit"]!["player"]!.AsObject().Remove(change);
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Retains a signed art guarantee above the calculated maximum for later owner resolution.
    /// </summary>
    [Fact]
    public void Validate_RetainsGuaranteeAboveCalculatedMaximumWithoutGrantingMaterialization()
    {
        var source = Source();
        var exchange = Exchange();
        var art = JsonNode.Parse("""
            {"artId":"art-source","displayName":"Нить надлома","effectSummary":"Духовное давление.",
             "ownerActorType":"player_soul","ownerActorId":"player_soul","baseOperation":"pressure","tier":0,
             "costMultiplierPercent":200,"upgradeCost":{"inkFeathers":1},"canTeachPlayer":false,"trainingConditions":[],
             "spiritualWoundEnvelope":{"schemaVersion":1,"maximumSeverityRank":2,"guaranteedSeverityRank":2}}
            """)!.AsObject();
        exchange["specialArtAudit"] = JsonNode.Parse("""
            {"artId":"art-source","ownerActorType":"player_soul","ownerActorId":"player_soul",
             "baseOperation":"pressure","costMultiplierPercent":200,"effectNote":"Надлом духовного узора."}
            """);
        exchange["actionCostAudit"]!["player"]!["specialArtId"] = "art-source";
        exchange["actionCostAudit"]!["player"]!["specialCostMultiplierPercent"] = 200;
        exchange["actionCostAudit"]!["player"]!["standardEffectiveCost"] = 3;
        exchange["actionCostAudit"]!["player"]!["effectiveCost"] = 6;
        exchange["actionCostAudit"]!["player"]!["after"] = 0;
        source["appliedArtKind"] = "special";
        source["appliedArtId"] = "art-source";
        source["guaranteedSeverityRank"] = 2;
        source["specialArtJsonBase64"] = Encode(art);
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.Equal(1, SpiritualWoundSourceWitness.Validate(source).MaximumSeverityRank);
    }

    /// <summary>
    /// Parses an explicit older spiritual wound and rejects a different embedded target identity.
    /// </summary>
    [Fact]
    public void Validate_JoinsExplicitRetraumaTarget()
    {
        var source = Source();
        var exchange = Exchange();
        var wound = WoundContractTestData.CreateSpiritualActiveWound("wound-older", "chaos_sea",
            "guardian", "guardian-a", WoundCarrierCatalog.AfterlifeProfilesPath);
        Assert.True(WoundMaterializationContract.Parse(wound.ToJsonString(), "test.wound").IsValid);
        exchange["after"]!["oppositionSideStrain"] = "broken";
        exchange["spiritualWoundTarget"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian-a", ["retraumaWoundRef"] = "wound-older"
        };
        source["destinationStrainRank"] = 4;
        source["extraStrainJumps"] = 3;
        source["maximumSeverityRank"] = 4;
        source["retraumaWoundId"] = "wound-older";
        source["retraumaWoundJsonBase64"] = Encode(wound);
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
        exchange["spiritualWoundTarget"]!["retraumaWoundRef"] = "another-wound";
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Checks historical condition-reference shape while leaving original existence to the live owner.
    /// </summary>
    [Fact]
    public void Validate_PreservesHistoricalConditionShapeWithoutGrantingMembership()
    {
        var source = Source();
        var exchange = Exchange();
        var audit = exchange["diceAudit"]!.AsObject();
        audit["rollMode"] = JsonNode.Parse("""
            {"player":{"effectiveMode":"normal",
              "advantageSources":[{"sourceType":"combat_condition","conditionId":"condition-older","summary":"Прежний эффект","level":"advantage"}],
              "disadvantageSources":["Встречная помеха"]}}
            """);
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
        var liveIssues = new List<ValidationIssue>();
        var method = typeof(ValidationService).GetMethod("ValidateCombatConditionRollModeSources",
            BindingFlags.Static | BindingFlags.NonPublic)!;
        method.Invoke(null, new object?[] { audit, "test.live", liveIssues, null, true });
        Assert.Contains(liveIssues, issue => issue.Code == "afterlife_combat_condition_roll_source_missing_active_condition");
        audit["rollMode"]!["player"]!["advantageSources"]![0]!["conditionId"] = null;
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Rejects an embedded special-art audit whose missing fields previously hid it from side selection.
    /// </summary>
    [Fact]
    public void Validate_RejectsMalformedUnselectedSpecialArtAudit()
    {
        var source = Source();
        var exchange = Exchange();
        exchange["specialArtAudit"] = new JsonObject();
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Requires an acting cost audit for a cost-bearing admitted source.
    /// </summary>
    [Fact]
    public void Validate_RejectsMissingActingCostAudit()
    {
        var source = Source();
        var exchange = Exchange();
        exchange["actionCostAudit"]!.AsObject().Remove("player");
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Preserves the owning rule that player-side costs use soul tiers even with a different lead.
    /// </summary>
    [Fact]
    public void Validate_PreservesNonPlayerLeadTierDistinction()
    {
        var source = Source();
        source["actingActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-lead" };
        source["appliedArtId"] = "guardian:guardian-lead:pressure";
        source["appliedArtTier"] = 5;
        Seal(source);
        Assert.True(IsValid(source));
    }

    /// <summary>
    /// Joins an explicit incoming actor to the acting source rather than accepting a different guardian.
    /// </summary>
    /// <param name="playerAlias">
    /// Player target alias admitted by the source owner.
    /// </param>
    [Theory]
    [InlineData("player_soul")]
    [InlineData("PLAYER_SOUL")]
    [InlineData("PLAYER")]
    [InlineData("SOUL")]
    public void Validate_RejectsContradictoryIncomingActor(string playerAlias)
    {
        var source = Source();
        var exchange = Exchange();
        source["actingActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-a" };
        exchange["outcome"] = "setback";
        source["affectedActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" };
        source["affectedSide"] = "player";
        source["appliedArtId"] = "guardian:guardian-a:pressure";
        exchange["incomingAction"] = new JsonObject
        {
            ["actorType"] = "guardian", ["actorId"] = "guardian-a", ["operationType"] = "pressure",
            ["spiritualWoundTarget"] = new JsonObject { ["actorType"] = playerAlias, ["actorId"] = "player_soul" }
        };
        exchange["after"]!["playerSideStrain"] = "strained";
        exchange["after"]!["oppositionSideStrain"] = "clear";
        exchange["diceAudit"]!["oppositionTotal"] = 25;
        exchange["diceAudit"]!["margin"] = -10;
        exchange["diceAudit"]!["outcomeBand"] = "decisive_opposition_success";
        exchange["diceAudit"]!["modifierBreakdown"]!["opposition"] =
            JsonNode.Parse("""[{"source":"pressure","value":20,"reason":"Духовное давление."}]""");
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
        exchange["incomingAction"]!["actorId"] = "guardian-b";
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Rejects malformed retained exchange semantics without relying on original profile or resource authority.
    /// </summary>
    /// <param name="change">
    /// Intrinsic exchange rule to violate while recomputing the witness digest.
    /// </param>
    [Theory]
    [InlineData("control")]
    [InlineData("tempo")]
    [InlineData("matchup")]
    [InlineData("position")]
    [InlineData("operation")]
    [InlineData("cost-shape")]
    [InlineData("base-cost")]
    [InlineData("cost-delta")]
    [InlineData("other-cost")]
    [InlineData("cost-floor")]
    public void Validate_RejectsMalformedIntrinsicExchange(string change)
    {
        var source = Source();
        var exchange = Exchange();
        switch (change)
        {
            case "control": exchange["before"]!["controlState"] = "bad"; break;
            case "tempo": exchange["after"]!["tempoAdvantage"] = "bad"; break;
            case "matchup": exchange.Remove("matchupAudit"); break;
            case "position": exchange["before"]!["conflictPosition"] = "unsupported_position"; break;
            case "operation": exchange["after"]!["conflictPosition"] = "player_advantaged"; break;
            case "cost-shape": exchange["actionCostAudit"]!["player"]!.AsObject().Remove("before"); break;
            case "base-cost": exchange["actionCostAudit"]!["player"]!["baseCost"] = 99; break;
            case "cost-delta": exchange["actionCostAudit"]!["player"]!["after"] = 6; break;
            case "other-cost": exchange["actionCostAudit"]!["opposition"]!.AsObject().Remove("effectiveCost"); break;
            case "cost-floor": exchange["actionCostAudit"]!["player"]!["effectiveCost"] = 0; exchange["actionCostAudit"]!["player"]!["after"] = 6; break;
        }
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Accepts intrinsic effective position without reconstructing unavailable wound authority or changing canonical snapshots.
    /// </summary>
    [Fact]
    public void Validate_RetainsEffectivePositionWithoutRebindingCanonicalSnapshot()
    {
        var source = Source();
        var exchange = Exchange();
        exchange["before"]!["conflictPosition"] = "player_advantaged";
        exchange["after"]!["conflictPosition"] = "player_advantaged";
        // No position row records effective contested; only original admission can prove the causal wound burden.
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
    }

    /// <summary>
    /// Checks the declared Light Incarnate bonus without granting unlock authority.
    /// </summary>
    /// <param name="bonus">
    /// Declared modifier value; the retained lead pressure operation permits eight.
    /// </param>
    /// <param name="expected">
    /// Whether the declaration is intrinsically valid.
    /// </param>
    [Theory]
    [InlineData(8, true)]
    [InlineData(9, false)]
    public void Validate_ChecksDeclaredLightIncarnateBonus(int bonus, bool expected)
    {
        var source = Source();
        var exchange = Exchange();
        exchange["diceAudit"]!["modifierBreakdown"]!["player"] = new JsonArray(new JsonObject
        {
            ["source"] = "light_incarnate", ["value"] = bonus, ["reason"] = "Воплощённый Свет."
        });
        exchange["diceAudit"]!["playerTotal"] = 15 + bonus;
        exchange["diceAudit"]!["margin"] = 10 + bonus;
        source["harmfulMargin"] = 10 + bonus;
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.Equal(expected, IsValid(source));
    }

    /// <summary>
    /// Preserves an internally consistent higher cost whose wound burden still needs original reconstruction.
    /// </summary>
    [Fact]
    public void Validate_AllowsRetainedCostBurdenWithoutAuthenticatingIt()
    {
        var source = Source();
        var exchange = Exchange();
        exchange["actionCostAudit"]!["player"]!["effectiveCost"] = 5;
        exchange["actionCostAudit"]!["player"]!["after"] = 1;
        source["exchangeJsonBase64"] = Encode(exchange);
        Seal(source);
        Assert.True(IsValid(source));
    }

    /// <summary>
    /// Prevents noncanonical soul-kind casing from bypassing the reserved soul identity.
    /// </summary>
    /// <param name="field">
    /// Actor coordinate whose kind and identity are forged.
    /// </param>
    [Theory]
    [InlineData("actingActor")]
    [InlineData("affectedActor")]
    public void Validate_RejectsForgedUppercaseSoulCoordinate(string field)
    {
        var source = Source();
        source[field] = new JsonObject { ["actorKind"] = "PLAYER_SOUL", ["actorId"] = "other" };
        if (field == "actingActor") source["appliedArtId"] = "PLAYER_SOUL:other:pressure";
        Seal(source);
        Assert.False(IsValid(source));
    }

    /// <summary>
    /// Builds a standard-source comparison fixture with a complete deterministic exchange audit.
    /// </summary>
    /// <returns>
    /// Mutable source data; it is not accepted history or live authority.
    /// </returns>
    internal static JsonObject Source()
    {
        var claims = new JsonArray();
        for (var index = 0; index < 2; index++)
        {
            var claim = new JsonObject
            {
                ["sourceIndex"] = index, ["value"] = index == 0 ? 15 : 5,
                ["exchangeOrdinal"] = 0, ["claimFingerprint"] = ""
            };
            claim["claimFingerprint"] = SpiritualWoundTurnEvidence.ComputeClaimFingerprint(claim);
            claims.Add(claim);
        }
        var source = new JsonObject
        {
            ["sourceId"] = "", ["coordinate"] = "afterlifeSpiritualConflictUpdate.exchange.opposition",
            ["conflictId"] = "conflict-a", ["exchangeId"] = "exchange-a",
            ["turnEvidence"] = new JsonObject
            {
                ["sessionId"] = "session-a", ["requestId"] = "request-a", ["snapshotToken"] = "snapshot-a",
                ["turn"] = 42, ["realm"] = "chaos_sea", ["originalSnapshotFingerprint"] = Fingerprint,
                ["bounds"] = new JsonObject
                {
                    ["exchangeCount"] = 2, ["sourceSlotCount"] = 4, ["diceCount"] = 4, ["imagePathCount"] = 40
                },
                ["acceptedD20Values"] = new JsonArray(15, 5, 12, 8), ["diceClaims"] = claims
            },
            ["sourceOrdinal"] = 0, ["exchangeOrdinal"] = 0, ["affectedSide"] = "opposition",
            ["actingActor"] = new JsonObject { ["actorKind"] = "player_soul", ["actorId"] = "player_soul" },
            ["affectedActor"] = new JsonObject { ["actorKind"] = "guardian", ["actorId"] = "guardian-a" },
            ["operation"] = "pressure", ["appliedArtId"] = "player_soul:player_soul:pressure",
            ["appliedArtKind"] = "standard", ["appliedArtTier"] = 0, ["targetResilienceTier"] = 0,
            ["priorStrainRank"] = 0, ["destinationStrainRank"] = 1, ["extraStrainJumps"] = 0,
            ["harmfulMargin"] = 10, ["maximumSeverityRank"] = 1, ["guaranteedSeverityRank"] = null,
            ["dangerMode"] = "hostile", ["dangerDeclarationFingerprint"] = Fingerprint,
            ["escalationFingerprint"] = null, ["retraumaWoundId"] = null, ["retraumaWoundJsonBase64"] = null,
            ["exchangeJsonBase64"] = Encode(Exchange()), ["specialArtJsonBase64"] = null,
            ["selectedDiceIndices"] = new JsonArray(0, 1), ["resourceResultFingerprint"] = Fingerprint,
            ["effectPlanFingerprint"] = Fingerprint, ["conflictBeforeFingerprint"] = Fingerprint,
            ["conflictAfterFingerprint"] = Fingerprint, ["sourceFingerprint"] = ""
        };
        Seal(source);
        return source;
    }

    /// <summary>
    /// Creates the intrinsic exchange payload used by the source fixture.
    /// </summary>
    /// <returns>
    /// Detached pressure exchange with both dice and action-cost audits.
    /// </returns>
    private static JsonObject Exchange() => JsonNode.Parse("""
        {
          "exchangeId":"exchange-a","turnNumber":42,"operationType":"pressure","outcome":"success",
          "before":{"playerSideStrain":"clear","oppositionSideStrain":"clear","conflictPosition":"contested"},
          "after":{"playerSideStrain":"clear","oppositionSideStrain":"strained","conflictPosition":"contested"},
          "matchupAudit":{"playerOperation":"pressure","oppositionOperation":"pressure","primaryResolutionLane":"pressure","matchupRationale":"Встречное давление.","riskProfile":"offensive_pressure"},
          "actionCostAudit":{
            "player":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":6,"after":3,"max":6},
            "opposition":{"operationType":"pressure","baseCost":3,"minCost":1,"artTier":0,"effectiveCost":3,"before":6,"after":3,"max":6}
          },
          "diceAudit":{
            "formulaVersion":"afterlife_spiritual_conflict_v1","diceSource":"input/turn_request.json.preGeneratedDices1d20",
            "diceUsed":[{"side":"player","sourceIndex":0,"sides":20,"value":15},{"side":"opposition","sourceIndex":1,"sides":20,"value":5}],
            "playerTotal":15,"oppositionTotal":5,"margin":10,"outcomeBand":"decisive_player_success",
            "modifierBreakdown":{"player":[],"opposition":[]}
          }
        }
        """)!.AsObject();

    /// <summary>
    /// Encodes a canonical comparison payload without adding source authority.
    /// </summary>
    /// <param name="json">
    /// Detached JSON object.
    /// </param>
    /// <returns>
    /// Canonical UTF-8 object bytes encoded as strict base64.
    /// </returns>
    private static string Encode(JsonObject json) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(SpiritualWoundStateJson.Canonical(json)));

    /// <summary>
    /// Reseals comparison data so mismatch tests exercise semantics rather than stale digests.
    /// </summary>
    /// <param name="source">
    /// Mutable source fixture.
    /// </param>
    private static void Seal(JsonObject source)
    {
        var digest = SpiritualWoundStateJson.Hash(source, "source", "sourceId", "sourceFingerprint");
        source["sourceFingerprint"] = digest;
        source["sourceId"] = "spiritual_source_" + digest[7..];
    }

    /// <summary>
    /// Runs intrinsic source validation without constructing any original snapshot authority.
    /// </summary>
    /// <param name="source">
    /// Witness under test.
    /// </param>
    /// <returns>
    /// <see langword="true"/> for valid comparison evidence; <see langword="false"/> for rejected evidence.
    /// </returns>
    private static bool IsValid(JsonObject source)
    {
        var type = typeof(ValidationService).Assembly.GetType("BookOfEternityClient.Services.SpiritualWoundSourceWitness");
        Assert.NotNull(type);
        var method = type.GetMethod("Validate", BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);
        try
        {
            method.Invoke(null, new object[] { source });
            return true;
        }
        catch (TargetInvocationException error) when (error.InnerException is FormatException or OverflowException)
        {
            return false;
        }
    }
}
