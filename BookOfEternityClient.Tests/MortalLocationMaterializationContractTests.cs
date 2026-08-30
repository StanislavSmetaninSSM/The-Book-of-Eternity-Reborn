using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MortalLocationMaterializationContractTests
{
    [Fact]
    public void ValidateRawLocation_MalformedHazardDefinitionFailsAtOwnerBoundary()
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        var definition = EffectMaterializationTestFixture.CreateDefinition();
        definition.Remove("removal");
        location["hazards"] = new JsonArray(new JsonObject
        {
            ["hazardId"] = "hazard_test_blood_mist",
            ["name"] = "Кровавый туман",
            ["status"] = "active",
            ["activeEffectDefinitions"] = new JsonArray(definition)
        });
        using var document = JsonDocument.Parse(location.ToJsonString());

        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            "worldMapUpdates.newLocations[0]",
            "world_map_creation");

        Assert.Contains(issues, issue =>
            issue.Code == "effect_source_definition_missing_field" &&
            issue.FilePath.Contains("hazards[0]", StringComparison.Ordinal));
    }

    private const string Context = "worldMapUpdates.newLocations[0]";
    private const string TargetLocationId = "loc_test_watchtower";

    [Fact]
    public void ValidateRawLocation_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateRawLocation());

        Assert.Empty(MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation"));
    }

    [Fact]
    public void ValidateCanonicalLocation_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateCanonicalLocation());

        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]"));
    }

    [Fact]
    public void ValidateCanonicalLocation_CreationDispositionDoesNotFreezeMutableChildCounts()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        location["locationStorages"]!.AsArray().Add(
            CreateCompleteStorage("storage_added_after_creation"));
        location["activeThreats"]!.AsArray().Add(new JsonObject
        {
            ["threatId"] = "threat_added_after_creation",
            ["name"] = "Поздняя угроза",
            ["description"] = "Полная canonical угроза, добавленная после первой материализации.",
            ["intensity"] = 2,
            ["longTermGoal"] = "Нарушить спокойствие у сторожевой башни.",
            ["currentActivity"] = null,
            ["threatArchetype"] = new JsonObject
            {
                ["motivation"] = "Corruption",
                ["method"] = "Covert",
                ["customMotivation"] = null,
                ["customMethod"] = null
            },
            ["impactProfile"] = new JsonObject
            {
                ["primaryTargetType"] = "Location",
                ["primaryTargetId"] = null,
                ["primaryTargetName"] = "Сторожевая башня",
                ["primaryImpact"] = "Stability",
                ["baseImpactValue"] = 1
            }
        });

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]");

        Assert.DoesNotContain(issues, issue =>
            issue.Code == "mortal_location_materialization_section_disposition_mismatch");
        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("skeletal")]
    [InlineData("empty_owner")]
    [InlineData("invalid_capacity")]
    [InlineData("missing_access")]
    [InlineData("ambiguous_owner_alias")]
    [InlineData("whitespace_owner_id")]
    public void ValidateCanonicalLocation_RejectsIncompleteStorageMetadata(string scenario)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var storage = CreateCompleteStorage("storage_invalid_semantics");
        switch (scenario)
        {
            case "skeletal":
                storage = new JsonObject { ["storageId"] = "storage_invalid_semantics" };
                break;
            case "empty_owner":
                storage["owner"] = new JsonObject();
                break;
            case "invalid_capacity":
                storage["capacity"] = "many";
                break;
            case "missing_access":
                storage.Remove("hasFullAccess");
                break;
            case "ambiguous_owner_alias":
                storage["ownerActorId"] = "npc_conflicting_owner";
                break;
            case "whitespace_owner_id":
                storage["owner"] = new JsonObject
                {
                    ["ownerType"] = "Faction",
                    ["ownerId"] = " faction_road_wardens ",
                    ["ownerName"] = "Дорожная стража"
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(scenario));
        }
        location["locationStorages"] = new JsonArray(storage);
        location["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_storage_semantic_invalid");
    }

    [Fact]
    public void ValidateCanonicalLocation_RejectsStorageContentsInWorldMapMember()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var storage = CreateCompleteStorage("storage_map_contents_forbidden");
        storage["contents"] = new JsonArray();
        location["locationStorages"] = new JsonArray(storage);
        location["materialization"]!["sections"]!["storageMetadata"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "game_state/world/world_map.json.locations[0]");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_map_storage_contents_forbidden" &&
            issue.FilePath.EndsWith(
                ".locationStorages[0].contents",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRawLocation_RejectsNestedClientOwnedCustomStateFields()
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["customStates"] = new JsonArray(new JsonObject
        {
            ["kind"] = "weather_memory",
            ["description"] = "Камни долго сохраняют тепло после грозы.",
            ["details"] = new JsonObject
            {
                ["receiptId"] = "mlocrec_forged_nested_custom_state"
            }
        });
        location["materialization"]!["sections"]!["customStates"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_custom_state_authority_forbidden" &&
            issue.FilePath.EndsWith(
                ".customStates[0].details.receiptId",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("extra_field")]
    [InlineData("wrong_schema")]
    [InlineData("wrong_actor_kind")]
    [InlineData("wrong_consent_status")]
    public void ValidateCanonicalLocation_RejectsMalformedRecognizedTreatmentSceneState(
        string mutation)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var consent = CreateTreatmentConsent();
        switch (mutation)
        {
            case "extra_field":
                consent["lifecycle"] = "active";
                break;
            case "wrong_schema":
                consent["schemaVersion"] = 2;
                break;
            case "wrong_actor_kind":
                consent["providerKind"] = "NPC";
                break;
            case "wrong_consent_status":
                consent["status"] = "revoked";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }
        SetCustomStates(location, new JsonArray(consent));

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_treatment_scene_state_invalid");
    }

    [Fact]
    public void ValidateCanonicalLocation_RejectsConfusableTreatmentSceneIdentifiers()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var first = CreateTreatmentFacility("clean_work_surface", available: true);
        var second = CreateTreatmentFacility("CLEAN_WORK_SURFACE", available: false);
        SetCustomStates(location, new JsonArray(first, second));

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_treatment_scene_state_ambiguous");
    }

    [Fact]
    public void ValidateCanonicalLocation_AcceptsUnavailableFacilityAndWithdrawnConsent()
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        var consent = CreateTreatmentConsent();
        consent["status"] = "withdrawn";
        SetCustomStates(location, new JsonArray(
            CreateTreatmentFacility("clean_work_surface", available: false),
            consent,
            new JsonObject
            {
                ["kind"] = "mortal_wound_treatment_environment",
                ["schemaVersion"] = 1,
                ["environmentId"] = "sterile_field",
                ["displayName"] = "Sterile field",
                ["state"] = "inactive"
            }));

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLocation(
            document.RootElement,
            "world_map.locations[0]");

        Assert.Empty(issues);
    }

    [Theory]
    [InlineData("{\"kind\":\"mortal_wound_treatment_facility\",\"kind\":\"mortal_wound_treatment_facility\",\"schemaVersion\":1,\"facilityId\":\"clean_surface\",\"displayName\":\"Clean surface\",\"available\":true}")]
    [InlineData("{\"kind\":\"mortal_wound_treatment_facility\",\"Kind\":\"mortal_wound_treatment_facility\",\"schemaVersion\":1,\"facilityId\":\"clean_surface\",\"displayName\":\"Clean surface\",\"available\":true}")]
    [InlineData("{\"Kind\":\"mortal_wound_treatment_facility\",\"schemaVersion\":1,\"facilityId\":\"clean_surface\",\"displayName\":\"Clean surface\",\"available\":true}")]
    public void TreatmentSceneContract_RejectsReservedKindAliases(string rowJson)
    {
        using var document = JsonDocument.Parse("[" + rowJson + "]");

        var issues = MortalLocationCustomStateContract.ValidateLocation(
            document.RootElement,
            "current_location.customStates");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_treatment_scene_state_invalid");
    }

    [Fact]
    public void TreatmentSceneContract_RejectsCaseOnlyReservedKindInLink()
    {
        using var document = JsonDocument.Parse("""
        [{"Kind":"mortal_wound_treatment_facility","schemaVersion":1,"facilityId":"clean_surface","displayName":"Clean surface","available":true}]
        """);

        var issues = MortalLocationCustomStateContract.ValidateLink(
            document.RootElement,
            "world_map.links[0].customStates");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_treatment_scene_state_wrong_container");
    }

    [Theory]
    [InlineData("facilityId")]
    [InlineData("environmentState")]
    public void TreatmentSceneContract_RejectsNonNormalizedExactIdentifiers(string field)
    {
        var row = field == "facilityId"
            ? CreateTreatmentFacility("facility_e\u0301", available: true)
            : new JsonObject
            {
                ["kind"] = "mortal_wound_treatment_environment",
                ["schemaVersion"] = 1,
                ["environmentId"] = "sterile_field",
                ["displayName"] = "Sterile field",
                ["state"] = "state_e\u0301"
            };
        using var document = JsonDocument.Parse(new JsonArray(row).ToJsonString());

        var issues = MortalLocationCustomStateContract.ValidateLocation(
            document.RootElement,
            "current_location.customStates");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_treatment_scene_state_invalid");
    }

    [Fact]
    public void TreatmentSceneContract_AllowsDistinctEnvironmentsWithTheSameState()
    {
        using var document = JsonDocument.Parse("""
        [
          {"kind":"mortal_wound_treatment_environment","schemaVersion":1,"environmentId":"sterile_field","displayName":"Sterile field","state":"active"},
          {"kind":"mortal_wound_treatment_environment","schemaVersion":1,"environmentId":"warm_shelter","displayName":"Warm shelter","state":"active"}
        ]
        """);

        Assert.Empty(MortalLocationCustomStateContract.ValidateLocation(
            document.RootElement,
            "current_location.customStates"));
    }

    [Fact]
    public void TreatmentSceneContract_RejectsConfusableEnvironmentIdentifiers()
    {
        using var document = JsonDocument.Parse("""
        [
          {"kind":"mortal_wound_treatment_environment","schemaVersion":1,"environmentId":"sterile_field","displayName":"Sterile field","state":"active"},
          {"kind":"mortal_wound_treatment_environment","schemaVersion":1,"environmentId":"STERILE_FIELD","displayName":"Other field","state":"inactive"}
        ]
        """);

        Assert.Contains(
            MortalLocationCustomStateContract.ValidateLocation(
                document.RootElement,
                "current_location.customStates"),
            issue => issue.Code == "mortal_wound_treatment_scene_state_ambiguous");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CustomStateContract_RejectsNonArrayContainersAtBothDirectEntrypoints(bool location)
    {
        using var document = JsonDocument.Parse("{\"kind\":\"ordinary_setting_state\"}");

        var issues = location
            ? MortalLocationCustomStateContract.ValidateLocation(
                document.RootElement, "current_location.customStates")
            : MortalLocationCustomStateContract.ValidateLink(
                document.RootElement, "world_map.links[0].customStates");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_custom_state_shape_invalid");
    }

    private static JsonObject CreateTreatmentFacility(string id, bool available) => new()
    {
        ["kind"] = "mortal_wound_treatment_facility",
        ["schemaVersion"] = 1,
        ["facilityId"] = id,
        ["displayName"] = "Clean work surface",
        ["available"] = available
    };

    private static JsonObject CreateTreatmentConsent() => new()
    {
        ["kind"] = "mortal_wound_treatment_consent",
        ["schemaVersion"] = 1,
        ["consentRef"] = "consent_field_medic_player",
        ["displayName"] = "Field medic consent",
        ["providerKind"] = "npc",
        ["providerId"] = "npc_field_medic_01",
        ["targetKind"] = "player",
        ["targetId"] = "player_current",
        ["status"] = "granted"
    };

    private static void SetCustomStates(JsonObject location, JsonArray states)
    {
        location["customStates"] = states;
        location["materialization"]!["sections"]!["customStates"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
    }

    [Theory]
    [InlineData("schemaVersion", "mortal_location_materialization_invalid_envelope")]
    [InlineData("entityKind", "mortal_location_materialization_invalid_envelope")]
    [InlineData("realm", "mortal_location_materialization_wrong_realm")]
    [InlineData("state", "mortal_location_materialization_invalid_envelope")]
    [InlineData("sourceTurn", "mortal_location_materialization_invalid_envelope")]
    public void ValidateRawLocation_RejectsClosedEnvelopeValue(
        string field,
        string expectedCode)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        var envelope = location["materialization"]!.AsObject();
        envelope[field] = field switch
        {
            "schemaVersion" => 2,
            "entityKind" => "location",
            "realm" => "shining_abode",
            "state" => "partial",
            "sourceTurn" => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(field))
        };

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == expectedCode);
    }

    [Fact]
    public void ValidateRawLocation_RequiresExpectedRouteAndAuthority()
    {
        var location = MortalLocationTestFixture.CreateRawLocation("current_scene_creation");
        location["materialization"]!["sourceAuthority"]!["kind"] = "npc";

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_route_mismatch");
        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_source_authority_mismatch");
    }

    [Theory]
    [InlineData("envelope")]
    [InlineData("sourceAuthority")]
    [InlineData("sections")]
    [InlineData("disposition")]
    public void ValidateRawLocation_RejectsUnknownClosedMember(string target)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        var envelope = location["materialization"]!.AsObject();
        switch (target)
        {
            case "envelope":
                envelope["futureField"] = true;
                break;
            case "sourceAuthority":
                envelope["sourceAuthority"]!["futureField"] = true;
                break;
            case "sections":
                envelope["sections"]!["futureSection"] = new JsonObject
                {
                    ["disposition"] = "populated",
                    ["reason"] = null
                };
                break;
            case "disposition":
                envelope["sections"]!["presentation"]!["futureField"] = true;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(target));
        }

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_unknown_field");
    }

    [Theory]
    [InlineData("receiptId")]
    [InlineData("seal")]
    [InlineData("locationIdentityIndex")]
    [InlineData("requestId")]
    [InlineData("sessionId")]
    public void ValidateRawLocation_RejectsGmAuthoredClientField(string field)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location[field] = "forged";

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_gm_authored_client_field");
    }

    [Theory]
    [InlineData("features")]
    [InlineData("eventDescriptions")]
    [InlineData("factionControl")]
    [InlineData("actorBindings")]
    [InlineData("locationStorages")]
    [InlineData("activeThreats")]
    [InlineData("loreBindings")]
    [InlineData("customStates")]
    public void ValidateRawLocation_RequiresPhysicalGovernedArrays(string field)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location.Remove(field);

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_governed_field_missing" &&
                     issue.FilePath.EndsWith(field, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("outdoor", "biome", null)]
    [InlineData("outdoor", "indoorType", "cave")]
    [InlineData("indoor", "indoorType", null)]
    [InlineData("indoor", "biome", "riverlands")]
    public void ValidateRawLocation_RejectsIndoorOutdoorShape(
        string locationType,
        string field,
        string? replacement)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["locationType"] = locationType;
        if (locationType == "indoor")
        {
            location["biome"] = null;
            location["biomeDescription"] = null;
            location["indoorType"] = "tower";
        }
        location[field] = replacement;

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_physical_shape_invalid");
    }

    [Theory]
    [InlineData("hidden", "gm_only", null, true)]
    [InlineData("rumored", "player_known", "Говорят о холодной переправе.", true)]
    [InlineData("discovered", "player_known", null, true)]
    [InlineData("visited", "player_known", null, true)]
    [InlineData("hidden", "player_known", null, false)]
    [InlineData("rumored", "player_known", null, false)]
    [InlineData("visited", "gm_only", null, false)]
    public void ValidateRawLocation_EnforcesDiscoveryPair(
        string tier,
        string audience,
        string? rumorSummary,
        bool valid)
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["discovery"] = new JsonObject
        {
            ["tier"] = tier,
            ["audience"] = audience,
            ["rumorSummary"] = rumorSummary
        };

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Equal(valid, issues.All(issue => issue.Code != "mortal_location_materialization_discovery_invalid"));
    }

    [Fact]
    public void ValidateRawLocation_EmptySectionRequiresReasonAndPhysicalEmptyValue()
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["factionControl"] = new JsonArray(new JsonObject { ["factionId"] = "fac_test" });
        location["materialization"]!["sections"]!["actorBindings"]!["reason"] = "   ";

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");

        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_section_disposition_mismatch");
        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_section_empty_reason_missing");
    }

    [Fact]
    public void ValidateRawLocation_DuplicatePropertyIsRejectedBeforeNodeConversion()
    {
        var json = MortalLocationTestFixture.CreateRawLocation().ToJsonString()
            .Replace(
                "\"realm\":\"mortal_world\"",
                "\"realm\":\"mortal_world\",\"realm\":\"mortal_world\"",
                StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLocation(
                document.RootElement,
                Context,
                "world_map_creation"),
            issue => issue.Code == "mortal_location_materialization_duplicate_property");
    }

    [Fact]
    public void ValidateCanonicalLocation_ReceiptlessObjectIsRejected()
    {
        using var document = Parse(MortalLocationTestFixture.CreateReceiptlessNegative());

        Assert.Contains(
            MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "world_map.locations[0]"),
            issue => issue.Code == "mortal_location_materialization_receipt_required");
    }

    [Theory]
    [InlineData("locationId", "loc_other")]
    [InlineData("materializationId", "mlocmat_other")]
    [InlineData("route", "current_scene_creation")]
    [InlineData("sourceAuthorityId", "turn_other")]
    public void ValidateCanonicalLocation_ReceiptMustMatchEnvelopeAndRoot(
        string receiptField,
        string replacement)
    {
        var location = MortalLocationTestFixture.CreateCanonicalLocation();
        location["materializationReceipt"]![receiptField] = replacement;

        using var document = Parse(location);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateCanonicalLocation(
                document.RootElement,
                "world_map.locations[0]"),
            issue => issue.Code == "mortal_location_materialization_receipt_mismatch");
    }

    [Fact]
    public void ValidateRawLink_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId));

        Assert.Empty(MortalLocationMaterializationContract.ValidateRawLink(
            document.RootElement,
            "worldMapUpdates.newLinks[0]",
            "world_map_link_creation"));
    }

    [Fact]
    public void ValidateRawLink_RejectsNestedClientOwnedCustomStateFields()
    {
        var link = MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link["customStates"] = new JsonArray(new JsonObject
        {
            ["kind"] = "trail_omen",
            ["details"] = new JsonObject
            {
                ["repairPacket"] = new JsonObject
                {
                    ["kind"] = "mortal_location_materialization_repair"
                }
            }
        });
        link["materialization"]!["sections"]!["customStates"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };

        using var document = Parse(link);
        var issues = MortalLocationMaterializationContract.ValidateRawLink(
            document.RootElement,
            "worldMapUpdates.newLinks[0]",
            "world_map_link_creation");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_location_custom_state_authority_forbidden" &&
            issue.FilePath.EndsWith(
                ".customStates[0].details.repairPacket",
                StringComparison.Ordinal));
    }

    [Fact]
    public void ValidateRawLink_RejectsReservedTreatmentSceneStateKind()
    {
        var link = MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link["customStates"] = new JsonArray(
            CreateTreatmentFacility("clean_work_surface", available: true));
        link["materialization"]!["sections"]!["customStates"] = new JsonObject
        {
            ["disposition"] = "populated",
            ["reason"] = null
        };

        using var document = Parse(link);
        var issues = MortalLocationMaterializationContract.ValidateRawLink(
            document.RootElement,
            "worldMapUpdates.newLinks[0]",
            "world_map_link_creation");

        Assert.Contains(issues, issue =>
            issue.Code == "mortal_wound_treatment_scene_state_wrong_container");
    }

    [Fact]
    public void ValidateCanonicalLink_CompleteObject_ReturnsNoIssues()
    {
        using var document = Parse(MortalLocationTestFixture.CreateCanonicalLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId));

        Assert.Empty(MortalLocationMaterializationContract.ValidateCanonicalLink(
            document.RootElement,
            "world_map.links[0]"));
    }

    [Theory]
    [InlineData("source", false, false)]
    [InlineData("source", true, true)]
    [InlineData("target", false, false)]
    [InlineData("target", true, true)]
    public void ValidateRawLink_RequiresEndpointSelectorXor(
        string endpoint,
        bool permanentPresent,
        bool temporaryPresent)
    {
        var link = MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link[$"{endpoint}LocationId"] = permanentPresent
            ? $"loc_{endpoint}"
            : null;
        link[$"{endpoint}InitialId"] = temporaryPresent
            ? $"locref_{endpoint}"
            : null;

        using var document = Parse(link);
        Assert.Contains(
            MortalLocationMaterializationContract.ValidateRawLink(
                document.RootElement,
                "worldMapUpdates.newLinks[0]",
                "world_map_link_creation"),
            issue => issue.Code == "mortal_location_link_endpoint_selector_invalid");
    }

    [Fact]
    public void ValidateRawLink_RejectsMalformedSecondEndpointSelector()
    {
        var link = MortalLocationTestFixture.CreateRawLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link["sourceInitialId"] = " locref_alias ";

        using var document = Parse(link);
        var issues = MortalLocationMaterializationContract.ValidateRawLink(
            document.RootElement,
            "worldMapUpdates.newLinks[0]",
            "world_map_link_creation");
        var issue = Assert.Single(
            issues,
            issue => issue.Code == "mortal_location_link_endpoint_selector_invalid");
        Assert.Equal("worldMapUpdates.newLinks[0].sourceInitialId", issue.FilePath);
    }

    [Fact]
    public void ValidateRawLocation_RejectsMalformedSecondParentSelector()
    {
        var location = MortalLocationTestFixture.CreateRawLocation();
        location["parentLocationId"] = "loc_parent_exact";
        location["parentInitialId"] = " locref_alias ";

        using var document = Parse(location);
        var issues = MortalLocationMaterializationContract.ValidateRawLocation(
            document.RootElement,
            Context,
            "world_map_creation");
        var issue = Assert.Single(
            issues,
            issue => issue.Code == "mortal_location_materialization_identity_conflict");
        Assert.Equal(Context + ".parentInitialId", issue.FilePath);
    }

    [Fact]
    public void ValidateCanonicalLink_RejectsTemporaryEndpointAndReceiptMismatch()
    {
        var link = MortalLocationTestFixture.CreateCanonicalLink(
            MortalLocationTestFixture.LocationId,
            TargetLocationId);
        link["targetInitialId"] = "locref_forged";
        link["materializationReceipt"]!["targetLocationId"] = "loc_other";

        using var document = Parse(link);
        var issues = MortalLocationMaterializationContract.ValidateCanonicalLink(
            document.RootElement,
            "world_map.links[0]");

        Assert.Contains(issues, issue => issue.Code == "mortal_location_link_endpoint_selector_invalid");
        Assert.Contains(issues, issue => issue.Code == "mortal_location_materialization_receipt_mismatch");
    }

    private static JsonDocument Parse(JsonObject value) =>
        JsonDocument.Parse(value.ToJsonString());

    private static JsonObject CreateCompleteStorage(string storageId) =>
        new()
        {
            ["storageId"] = storageId,
            ["name"] = "Дорожный сундук",
            ["description"] = "Прочный сундук под навесом.",
            ["image_prompt"] = "A sturdy travel chest beneath a wooden awning, dark fantasy, no text",
            ["capacity"] = 8,
            ["volume"] = 40.0,
            ["owner"] = null,
            ["authorizedUsers"] = new JsonArray(),
            ["hasFullAccess"] = true
        };
}
