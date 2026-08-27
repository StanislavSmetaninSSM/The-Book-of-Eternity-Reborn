using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundCarrierCatalogTests
{
    public static TheoryData<string, string, string, string> AfterlifeOwnerMappings => new()
    {
        { "player_soul", "player_soul", "Chaos Sea", "chaos_sea" },
        { "player_soul", "player_soul", "Shining Abode", "shining_abode" },
        { "guardian", "guardian", "Chaos Sea", "chaos_sea" },
        { "guardian", "guardian", "Shining Abode", "shining_abode" },
        { "resident", "resident", "Chaos Sea", "chaos_sea" },
        { "resident", "resident", "Shining Abode", "shining_abode" },
        { "shining_resident", "resident", "Chaos Sea", "chaos_sea" },
        { "shining_resident", "resident", "Shining Abode", "shining_abode" },
        { "radiant_actor", "radiant_actor", "Chaos Sea", "chaos_sea" },
        { "radiant_actor", "radiant_actor", "Shining Abode", "shining_abode" },
        { "shining_faction_head", "afterlife_actor", "Chaos Sea", "chaos_sea" },
        { "shining_faction_head", "afterlife_actor", "Shining Abode", "shining_abode" },
        { "saref_agent", "afterlife_actor", "Chaos Sea", "chaos_sea" },
        { "saref_agent", "afterlife_actor", "Shining Abode", "shining_abode" },
        { "system_actor", "afterlife_actor", "Chaos Sea", "chaos_sea" },
        { "system_actor", "afterlife_actor", "Shining Abode", "shining_abode" },
        { "custom_afterlife_actor", "afterlife_actor", "Chaos Sea", "chaos_sea" },
        { "custom_afterlife_actor", "afterlife_actor", "Shining Abode", "shining_abode" }
    };

    public static TheoryData<string, string> AcceptedAfterlifeRealmSpellings => new()
    {
        { "Chaos Sea", "chaos_sea" },
        { "Море Хаоса", "chaos_sea" },
        { "chaos_sea", "chaos_sea" },
        { "Shining Abode", "shining_abode" },
        { "Сияющая Обитель", "shining_abode" },
        { "shining_abode", "shining_abode" }
    };

    public static TheoryData<string, string> OwnerMismatchCases => new()
    {
        { "realm", "chaos_sea" },
        { "ownerKind", "npc" },
        { "ownerId", "player_other" },
        { "carrierPath", "game_state/npcs/npc_wounds.json" }
    };

    [Fact]
    public void Build_IndexesPlayerAndDedicatedNpcWithExactCoordinates()
    {
        var playerWound = Wound("wound_player");
        var npcWound = Wound(
            "wound_npc",
            ownerKind: "npc",
            ownerId: "npc_healer",
            carrierPath: WoundCarrierCatalog.NpcPath);

        var catalog = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(playerWound),
            npcs: WoundContractTestData.CreateNamedNpcCarrier("npc_healer", npcWound));

        Assert.Empty(catalog.Issues);
        Assert.Equal(2, catalog.Occurrences.Count);
        AssertOccurrence(
            catalog,
            "wound_player",
            "mortal_world",
            "player",
            "player_current",
            WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        var npcOccurrence = AssertOccurrence(
            catalog,
            "wound_npc",
            "mortal_world",
            "npc",
            "npc_healer",
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.NpcPath + ".entries[0].activeWounds[0]");
        Assert.False(
            npcOccurrence.FilePath.Contains("npc_effects.json", StringComparison.Ordinal),
            "Named NPC wounds must never be indexed through the effect carrier.");
    }

    [Fact]
    public void Build_IndexesEnemyAllyAndNestedGroupMemberByPermanentIdentity()
    {
        var enemyWound = Wound(
            "wound_enemy",
            ownerKind: "combatant",
            ownerId: "combatant_enemy",
            carrierPath: WoundCarrierCatalog.EnemiesPath);
        var allyWound = Wound(
            "wound_ally",
            ownerKind: "combatant",
            ownerId: "combatant_ally",
            carrierPath: WoundCarrierCatalog.AlliesPath);
        var memberWound = Wound(
            "wound_member",
            ownerKind: "combatant_member",
            ownerId: "member_scout",
            carrierPath: WoundCarrierCatalog.AlliesPath);
        var enemyRoot = CombatRoot(
            "enemiesData",
            Combatant("combatant_enemy", enemyWound));
        var allyRoot = CombatRoot(
            "alliesData",
            Combatant("combatant_ally", allyWound),
            new JsonObject
            {
                ["combatantId"] = "combatant_group",
                ["isGroup"] = true,
                ["members"] = new JsonArray(new JsonObject
                {
                    ["memberId"] = "member_scout",
                    ["displayName"] = "Следопыт",
                    ["activeWounds"] = Array(memberWound)
                })
            });

        var catalog = BuildCatalog(enemies: enemyRoot, allies: allyRoot);

        Assert.Empty(catalog.Issues);
        AssertOccurrence(
            catalog,
            "wound_enemy",
            "mortal_world",
            "combatant",
            "combatant_enemy",
            WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.EnemiesPath + ".enemiesData[0].activeWounds[0]");
        AssertOccurrence(
            catalog,
            "wound_ally",
            "mortal_world",
            "combatant",
            "combatant_ally",
            WoundCarrierCatalog.AlliesPath,
            WoundCarrierCatalog.AlliesPath + ".alliesData[0].activeWounds[0]");
        AssertOccurrence(
            catalog,
            "wound_member",
            "mortal_world",
            "combatant_member",
            "member_scout",
            WoundCarrierCatalog.AlliesPath,
            WoundCarrierCatalog.AlliesPath + ".alliesData[1].members[0].activeWounds[0]");
    }

    [Theory]
    [MemberData(nameof(AfterlifeOwnerMappings))]
    public void Build_MapsEveryClosedAfterlifeActorTypeInBothRealms(
        string actorType,
        string ownerKind,
        string declaredRealm,
        string normalizedRealm)
    {
        var actorId = "actor_" + actorType;
        var woundId = "wound_" + actorType + "_" + normalizedRealm;
        var wound = Wound(
            woundId,
            realm: normalizedRealm,
            ownerKind: ownerKind,
            ownerId: actorId,
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
            domain: "spiritual");
        var root = ProfileRoot(Profile(actorType, actorId, declaredRealm, wound));

        var catalog = BuildCatalog(profiles: root);

        Assert.Empty(catalog.Issues);
        AssertOccurrence(
            catalog,
            woundId,
            normalizedRealm,
            ownerKind,
            actorId,
            WoundCarrierCatalog.AfterlifeProfilesPath,
            WoundCarrierCatalog.AfterlifeProfilesPath + ".profiles[0].activeWounds[0]");
    }

    [Theory]
    [MemberData(nameof(AcceptedAfterlifeRealmSpellings))]
    public void Build_NormalizesOnlyExistingAcceptedAfterlifeRealmSpellings(
        string declaredRealm,
        string normalizedRealm)
    {
        var wound = Wound(
            "wound_realm_spelling",
            realm: normalizedRealm,
            ownerKind: "guardian",
            ownerId: "guardian_realm",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
            domain: "spiritual");

        var catalog = BuildCatalog(profiles: ProfileRoot(
            Profile("guardian", "guardian_realm", declaredRealm, wound)));

        Assert.Empty(catalog.Issues);
        Assert.Equal(normalizedRealm, Assert.Single(catalog.Occurrences).Coordinate.Realm);
    }

    [Fact]
    public void Build_RejectsByteIdenticalDuplicatesAcrossCarriersAndWithinOneCarrier()
    {
        var wound = Wound("wound_duplicate");
        var acrossCarriers = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(wound),
            npcs: WoundContractTestData.CreateNamedNpcCarrier(
                "npc_duplicate",
                wound.DeepClone().AsObject()));

        Assert.Equal(2, acrossCarriers.Occurrences.Count);
        AssertIssue(acrossCarriers, "wound_carrier_duplicate_occurrence");
        Assert.False(acrossCarriers.TryResolveOne("wound_duplicate", out _));

        var withinCarrier = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(
                wound,
                wound.DeepClone().AsObject()));

        Assert.Equal(2, withinCarrier.Occurrences.Count);
        AssertIssue(withinCarrier, "wound_carrier_duplicate_occurrence");
        Assert.False(withinCarrier.TryResolveOne("wound_duplicate", out _));
    }

    [Fact]
    public void Build_CountsExactOrdinalOccurrencesWithoutAliasFallback()
    {
        var empty = BuildCatalog();
        var exact = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(Wound("wound_Exact")));
        var duplicate = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(
                Wound("wound_duplicate"),
                Wound("wound_duplicate")));

        Assert.Equal(0, empty.CountExactOccurrences("wound_missing"));
        Assert.Equal(1, exact.CountExactOccurrences("wound_Exact"));
        Assert.Equal(0, exact.CountExactOccurrences("WOUND_EXACT"));
        Assert.Equal(0, exact.CountExactOccurrences("wound_Еxact"));
        Assert.Equal(0, exact.CountExactOccurrences("Рваная рана левого бока"));
        Assert.Equal(2, duplicate.CountExactOccurrences("wound_duplicate"));
    }

    [Fact]
    public void Build_RejectsConfusableButDistinctWoundIdsAndInvalidatesBoth()
    {
        var latin = Wound("wound_A");
        var cyrillic = Wound("wound_А");

        var catalog = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(latin, cyrillic));

        Assert.Equal(2, catalog.Occurrences.Count);
        AssertIssue(catalog, "wound_carrier_confusable_wound_id");
        Assert.False(catalog.TryResolveOne("wound_A", out _));
        Assert.False(catalog.TryResolveOne("wound_А", out _));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Build_ReviewRegression_MalformedWoundIdentityParticipatesInGlobalUniqueness(
        bool acrossCarriers,
        bool confusable)
    {
        const string validWoundId = "wound_A";
        var malformedWoundId = confusable ? "wound_А" : validWoundId;
        var valid = Wound(validWoundId);
        WoundCarrierCatalog catalog;

        if (acrossCarriers)
        {
            var malformed = Wound(
                malformedWoundId,
                ownerKind: "npc",
                ownerId: "npc_malformed_identity",
                carrierPath: WoundCarrierCatalog.NpcPath);
            malformed["display"]!.AsObject().Remove("description");
            catalog = BuildCatalog(
                player: WoundContractTestData.CreatePlayerCarrier(valid),
                npcs: WoundContractTestData.CreateNamedNpcCarrier(
                    "npc_malformed_identity",
                    malformed));
        }
        else
        {
            var malformed = Wound(malformedWoundId);
            malformed["display"]!.AsObject().Remove("description");
            catalog = BuildCatalog(
                player: WoundContractTestData.CreatePlayerCarrier(valid, malformed));
        }

        Assert.Contains(catalog.Issues, issue =>
            issue.Code == "wound_materialization_missing_field");
        AssertIssue(
            catalog,
            confusable
                ? "wound_carrier_confusable_wound_id"
                : "wound_carrier_duplicate_occurrence");
        Assert.Single(catalog.Occurrences);
        Assert.False(catalog.TryResolveOne(validWoundId, out _));
        Assert.False(catalog.TryResolveOne(malformedWoundId, out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_ReviewRegression_DuplicateNpcOwnerCarriersIncludeEmptyObjects(
        bool secondCarrierIsEmpty)
    {
        const string npcId = "npc_duplicate_owner";
        var firstWound = Wound(
            "wound_npc_owner_first",
            ownerKind: "npc",
            ownerId: npcId,
            carrierPath: WoundCarrierCatalog.NpcPath);
        var secondWound = Wound(
            "wound_npc_owner_second",
            ownerKind: "npc",
            ownerId: npcId,
            carrierPath: WoundCarrierCatalog.NpcPath);
        var root = WoundContractTestData.CreateNamedNpcCarrier(npcId, firstWound);
        root["entries"]!.AsArray().Add(new JsonObject
        {
            ["npcId"] = npcId,
            ["activeWounds"] = secondCarrierIsEmpty
                ? new JsonArray()
                : Array(secondWound)
        });

        var catalog = BuildCatalog(npcs: root);

        AssertIssue(catalog, "wound_carrier_duplicate_owner_carrier");
        Assert.False(catalog.TryResolveOne("wound_npc_owner_first", out _));
        if (!secondCarrierIsEmpty)
            Assert.False(catalog.TryResolveOne("wound_npc_owner_second", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_ReviewRegression_DuplicateCombatOwnersCollideAcrossEnemyAndAllyRoots(
        bool nestedMember)
    {
        var ownerKind = nestedMember ? "combatant_member" : "combatant";
        var ownerId = nestedMember ? "member_cross_root" : "combatant_cross_root";
        var enemyWound = Wound(
            "wound_cross_root_enemy",
            ownerKind: ownerKind,
            ownerId: ownerId,
            carrierPath: WoundCarrierCatalog.EnemiesPath);
        var allyWound = Wound(
            "wound_cross_root_ally",
            ownerKind: ownerKind,
            ownerId: ownerId,
            carrierPath: WoundCarrierCatalog.AlliesPath);
        JsonObject enemyOwner;
        JsonObject allyOwner;

        if (nestedMember)
        {
            enemyOwner = new JsonObject
            {
                ["combatantId"] = "group_enemy",
                ["isGroup"] = true,
                ["members"] = Array(new JsonObject
                {
                    ["memberId"] = ownerId,
                    ["activeWounds"] = Array(enemyWound)
                })
            };
            allyOwner = new JsonObject
            {
                ["combatantId"] = "group_ally",
                ["isGroup"] = true,
                ["members"] = Array(new JsonObject
                {
                    ["memberId"] = ownerId,
                    ["activeWounds"] = Array(allyWound)
                })
            };
        }
        else
        {
            enemyOwner = Combatant(ownerId, enemyWound);
            allyOwner = Combatant(ownerId, allyWound);
        }

        var catalog = BuildCatalog(
            enemies: CombatRoot("enemiesData", enemyOwner),
            allies: CombatRoot("alliesData", allyOwner));

        AssertIssue(catalog, "wound_carrier_duplicate_owner_carrier");
        Assert.False(catalog.TryResolveOne("wound_cross_root_enemy", out _));
        Assert.False(catalog.TryResolveOne("wound_cross_root_ally", out _));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Build_ReviewRegression_EmptyAmbiguousCombatObjectStillClaimsItsPositionalOwner(
        bool nestedMember)
    {
        var ownerKind = nestedMember ? "combatant_member" : "combatant";
        var ownerId = nestedMember ? "member_ambiguous_owner" : "combatant_ambiguous_owner";
        var wound = Wound(
            "wound_ambiguous_owner_collision",
            ownerKind: ownerKind,
            ownerId: ownerId,
            carrierPath: WoundCarrierCatalog.EnemiesPath);
        JsonObject validOwner;
        JsonObject emptyAmbiguousOwner;

        if (nestedMember)
        {
            validOwner = new JsonObject
            {
                ["combatantId"] = "group_valid_owner",
                ["isGroup"] = true,
                ["members"] = Array(new JsonObject
                {
                    ["memberId"] = ownerId,
                    ["activeWounds"] = Array(wound)
                })
            };
            emptyAmbiguousOwner = new JsonObject
            {
                ["combatantId"] = "group_ambiguous_owner",
                ["isGroup"] = true,
                ["members"] = Array(new JsonObject
                {
                    ["memberId"] = ownerId,
                    ["combatantId"] = "conflicting_nested_identity",
                    ["activeWounds"] = new JsonArray()
                })
            };
        }
        else
        {
            validOwner = Combatant(ownerId, wound);
            emptyAmbiguousOwner = new JsonObject
            {
                ["combatantId"] = ownerId,
                ["memberId"] = "conflicting_top_level_identity",
                ["activeWounds"] = new JsonArray()
            };
        }

        var catalog = BuildCatalog(
            enemies: CombatRoot("enemiesData", validOwner),
            allies: CombatRoot("alliesData", emptyAmbiguousOwner));

        AssertIssue(catalog, "wound_carrier_duplicate_owner_carrier");
        Assert.False(catalog.TryResolveOne("wound_ambiguous_owner_collision", out _));
    }

    [Fact]
    public void Build_ReviewRegression_DerivedAfterlifeOwnerAliasesAndRealmsCannotCollide()
    {
        const string actorId = "resident_duplicate_owner";
        var firstWound = Wound(
            "wound_afterlife_owner_first",
            realm: "chaos_sea",
            ownerKind: "resident",
            ownerId: actorId,
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
            domain: "spiritual");
        var secondWound = Wound(
            "wound_afterlife_owner_second",
            realm: "chaos_sea",
            ownerKind: "resident",
            ownerId: actorId,
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
            domain: "spiritual");
        var catalog = BuildCatalog(profiles: ProfileRoot(
            Profile("resident", actorId, "Chaos Sea", firstWound),
            Profile("shining_resident", actorId, "chaos_sea", secondWound)));

        AssertIssue(catalog, "wound_carrier_duplicate_owner_carrier");
        Assert.False(catalog.TryResolveOne("wound_afterlife_owner_first", out _));
        Assert.False(catalog.TryResolveOne("wound_afterlife_owner_second", out _));
    }

    [Theory]
    [InlineData("false")]
    [InlineData("missing")]
    [InlineData("non_boolean")]
    [InlineData("true")]
    public void Build_ReviewRegression_MemberCarrierRequiresExactBooleanTrueGroupParent(
        string groupState)
    {
        var memberWound = Wound(
            "wound_group_parent_gate_" + groupState,
            ownerKind: "combatant_member",
            ownerId: "member_group_parent_gate_" + groupState,
            carrierPath: WoundCarrierCatalog.EnemiesPath);
        var group = new JsonObject
        {
            ["combatantId"] = "group_parent_gate_" + groupState,
            ["members"] = Array(new JsonObject
            {
                ["memberId"] = "member_group_parent_gate_" + groupState,
                ["activeWounds"] = Array(memberWound)
            })
        };
        if (groupState == "true")
            group["isGroup"] = true;
        else if (groupState == "false")
            group["isGroup"] = false;
        else if (groupState == "non_boolean")
            group["isGroup"] = "true";

        var catalog = BuildCatalog(enemies: CombatRoot("enemiesData", group));

        if (groupState == "true")
        {
            Assert.Empty(catalog.Issues);
            Assert.True(catalog.TryResolveOne(memberWound["woundId"]!.GetValue<string>(), out _));
            return;
        }

        AssertIssue(catalog, "wound_carrier_group_identity_invalid");
        Assert.Single(catalog.Occurrences);
        Assert.False(catalog.TryResolveOne(memberWound["woundId"]!.GetValue<string>(), out _));
    }

    [Theory]
    [InlineData(" Chaos Sea", "chaos_sea")]
    [InlineData("Chaos Sea ", "chaos_sea")]
    [InlineData("\tShining Abode", "shining_abode")]
    [InlineData("Shining Abode\n", "shining_abode")]
    public void Build_ReviewRegression_RejectsPaddedAfterlifeRealmBeforeNormalization(
        string declaredRealm,
        string normalizedRealm)
    {
        var wound = Wound(
            "wound_padded_afterlife_realm",
            realm: normalizedRealm,
            ownerKind: "guardian",
            ownerId: "guardian_padded_realm",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
            domain: "spiritual");
        var catalog = BuildCatalog(profiles: ProfileRoot(
            Profile("guardian", "guardian_padded_realm", declaredRealm, wound)));

        AssertIssue(catalog, "wound_carrier_afterlife_identity_invalid");
        Assert.Empty(catalog.Occurrences);
        Assert.False(catalog.TryResolveOne("wound_padded_afterlife_realm", out _));
    }

    [Theory]
    [MemberData(nameof(OwnerMismatchCases))]
    public void Build_EnvelopeOwnerMustMatchEveryPhysicallyDerivedCoordinateField(
        string field,
        string wrongValue)
    {
        var wound = field switch
        {
            "realm" => Wound("wound_mismatch", realm: wrongValue),
            "ownerKind" => Wound("wound_mismatch", ownerKind: wrongValue),
            "ownerId" => Wound("wound_mismatch", ownerId: wrongValue),
            "carrierPath" => Wound("wound_mismatch", carrierPath: wrongValue),
            _ => throw new ArgumentOutOfRangeException(nameof(field), field, null)
        };

        var catalog = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(wound));

        Assert.Contains(catalog.Issues, issue =>
            issue.Code == "wound_carrier_agreement_mismatch" &&
            issue.FilePath == WoundCarrierCatalog.PlayerPath + $".activeWounds[0].owner.{field}");
        Assert.False(catalog.TryResolveOne("wound_mismatch", out _));
    }

    [Fact]
    public void Build_RejectsMalformedAndLegacyPlayerAndNpcRoots()
    {
        var legacyPlayer = new JsonObject
        {
            ["playerWoundChanges"] = new JsonArray(Wound("legacy_player"))
        };
        var malformedPlayerOwner = WoundContractTestData.CreatePlayerCarrier();
        malformedPlayerOwner["owner"]!["ownerKind"] = "Player";
        var malformedPlayerArray = WoundContractTestData.CreatePlayerCarrier();
        malformedPlayerArray["activeWounds"] = new JsonObject();

        foreach (var root in new[] { legacyPlayer, malformedPlayerOwner, malformedPlayerArray })
        {
            var catalog = BuildCatalog(player: root);
            Assert.NotEmpty(catalog.Issues);
            Assert.Empty(catalog.Occurrences);
        }
        AssertIssue(BuildCatalog(player: legacyPlayer), "wound_carrier_legacy_unsupported");
        AssertIssue(BuildCatalog(player: malformedPlayerOwner), "wound_carrier_invalid_field");
        AssertIssue(BuildCatalog(player: malformedPlayerArray), "wound_carrier_invalid_field");

        var legacyNpc = new JsonObject
        {
            ["NPCWoundChanges"] = new JsonArray(Wound("legacy_npc"))
        };
        var malformedNpcEntries = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonObject()
        };
        var malformedNpcEntry = WoundContractTestData.CreateNamedNpcCarrier(
            "npc_malformed",
            Wound(
                "wound_npc_malformed",
                ownerKind: "npc",
                ownerId: "npc_malformed",
                carrierPath: WoundCarrierCatalog.NpcPath));
        malformedNpcEntry["entries"]![0]!["npcId"] = " npc_malformed ";

        AssertIssue(BuildCatalog(npcs: legacyNpc), "wound_carrier_legacy_unsupported");
        AssertIssue(BuildCatalog(npcs: malformedNpcEntries), "wound_carrier_invalid_field");
        AssertIssue(BuildCatalog(npcs: malformedNpcEntry), "wound_carrier_invalid_field");
        Assert.Empty(BuildCatalog(npcs: legacyNpc).Occurrences);
        Assert.Empty(BuildCatalog(npcs: malformedNpcEntries).Occurrences);
        Assert.Empty(BuildCatalog(npcs: malformedNpcEntry).Occurrences);
    }

    [Fact]
    public void Build_MalformedStrictRootsStillParseEveryNonEmptyCanonicalWoundArray()
    {
        var player = WoundContractTestData.CreatePlayerCarrier(Wound("wound_invalid_player_root"));
        player["unexpectedRootField"] = true;
        var npcWound = Wound(
            "wound_invalid_npc_entry",
            ownerKind: "npc",
            ownerId: "npc_invalid_entry",
            carrierPath: WoundCarrierCatalog.NpcPath);
        var npcs = WoundContractTestData.CreateNamedNpcCarrier("npc_invalid_entry", npcWound);
        npcs["entries"]![0]!["unexpectedEntryField"] = true;

        var playerCatalog = BuildCatalog(player: player);
        var npcCatalog = BuildCatalog(npcs: npcs);

        AssertIssue(playerCatalog, "wound_carrier_invalid_root");
        Assert.Single(playerCatalog.Occurrences);
        Assert.False(playerCatalog.TryResolveOne("wound_invalid_player_root", out _));
        AssertIssue(npcCatalog, "wound_carrier_invalid_field");
        Assert.Single(npcCatalog.Occurrences);
        Assert.False(npcCatalog.TryResolveOne("wound_invalid_npc_entry", out _));

        player["activeWounds"] = new JsonArray("malformed-wound-item");
        var malformedItemCatalog = BuildCatalog(player: player);
        Assert.Contains(malformedItemCatalog.Issues, issue =>
            issue.Code == "wound_materialization_invalid_root" &&
            issue.FilePath == WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
    }

    [Fact]
    public void Build_ParsesEveryWoundAtItsSemanticPathAndRejectsHealedCarrierEntries()
    {
        var malformed = WoundContractTestData.CreatePlayerCarrier();
        malformed["activeWounds"] = new JsonArray("not-a-wound");

        var malformedCatalog = BuildCatalog(player: malformed);

        Assert.Contains(malformedCatalog.Issues, issue =>
            issue.Code == "wound_materialization_invalid_root" &&
            issue.FilePath == WoundCarrierCatalog.PlayerPath + ".activeWounds[0]");
        Assert.Empty(malformedCatalog.Occurrences);

        var healed = Wound("wound_healed_in_active", lifecycle: "healed");
        healed["care"]!["state"] = "healed";
        var healedCatalog = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(healed));

        AssertIssue(healedCatalog, "wound_carrier_non_active_lifecycle");
        Assert.False(healedCatalog.TryResolveOne("wound_healed_in_active", out _));
    }

    [Fact]
    public void Build_RejectsTemporaryAmbiguousMissingAndMalformedCombatCarrierIdentity()
    {
        var temporaryCombatant = Combatant(
            "combatant_temp",
            Wound(
                "wound_temp",
                ownerKind: "combatant",
                ownerId: "combatant_temp",
                carrierPath: WoundCarrierCatalog.EnemiesPath));
        temporaryCombatant["combatantRef"] = "temporary_ref";

        var temporaryMember = new JsonObject
        {
            ["combatantId"] = "group_temp",
            ["isGroup"] = true,
            ["members"] = new JsonArray(new JsonObject
            {
                ["memberId"] = "member_temp",
                ["memberRef"] = "temporary_member_ref",
                ["activeWounds"] = Array(Wound(
                    "wound_member_temp",
                    ownerKind: "combatant_member",
                    ownerId: "member_temp",
                    carrierPath: WoundCarrierCatalog.EnemiesPath))
            })
        };

        var ambiguous = Combatant(
            "combatant_ambiguous",
            Wound(
                "wound_ambiguous",
                ownerKind: "combatant",
                ownerId: "combatant_ambiguous",
                carrierPath: WoundCarrierCatalog.EnemiesPath));
        ambiguous["memberId"] = "member_ambiguous";

        var missing = new JsonObject
        {
            ["activeWounds"] = Array(Wound(
                "wound_missing_identity",
                ownerKind: "combatant",
                ownerId: "combatant_missing",
                carrierPath: WoundCarrierCatalog.EnemiesPath))
        };
        var malformedArray = new JsonObject
        {
            ["combatantId"] = "combatant_malformed",
            ["activeWounds"] = new JsonObject { ["woundId"] = "wound_malformed_array" }
        };

        var temporaryCatalog = BuildCatalog(enemies: CombatRoot(
            "enemiesData",
            temporaryCombatant,
            temporaryMember));
        Assert.Equal(2, temporaryCatalog.Occurrences.Count);
        Assert.Equal(
            2,
            temporaryCatalog.Issues.Count(issue => issue.Code == "wound_carrier_temporary_identity"));
        Assert.False(temporaryCatalog.TryResolveOne("wound_temp", out _));
        Assert.False(temporaryCatalog.TryResolveOne("wound_member_temp", out _));

        var ambiguousCatalog = BuildCatalog(enemies: CombatRoot("enemiesData", ambiguous));
        AssertIssue(ambiguousCatalog, "wound_carrier_combat_identity_invalid");
        Assert.False(ambiguousCatalog.TryResolveOne("wound_ambiguous", out _));

        var missingCatalog = BuildCatalog(enemies: CombatRoot("enemiesData", missing));
        AssertIssue(missingCatalog, "wound_carrier_combat_identity_invalid");
        Assert.False(missingCatalog.TryResolveOne("wound_missing_identity", out _));

        var malformedCatalog = BuildCatalog(enemies: CombatRoot("enemiesData", malformedArray));
        AssertIssue(malformedCatalog, "wound_carrier_invalid_field");
        Assert.Empty(malformedCatalog.Occurrences);
    }

    [Fact]
    public void Build_TemporaryCombatRefsWithoutActiveWoundsAreNotCarrierErrors()
    {
        var root = CombatRoot(
            "enemiesData",
            new JsonObject
            {
                ["combatantRef"] = "temporary_ref",
                ["activeWounds"] = new JsonArray()
            });

        var catalog = BuildCatalog(enemies: root);

        Assert.Empty(catalog.Issues);
        Assert.Empty(catalog.Occurrences);
    }

    [Fact]
    public void Build_NpcBoundCombatantsCannotRetainWoundsWithOrWithoutStaleCombatantId()
    {
        var withStaleId = Combatant(
            "combatant_stale",
            Wound(
                "wound_stale_named",
                ownerKind: "combatant",
                ownerId: "combatant_stale",
                carrierPath: WoundCarrierCatalog.EnemiesPath));
        withStaleId["NPCId"] = "npc_promoted";
        var withoutStaleId = new JsonObject
        {
            ["NPCId"] = "npc_promoted_without_id",
            ["activeWounds"] = Array(Wound(
                "wound_named_without_id",
                ownerKind: "npc",
                ownerId: "npc_promoted_without_id",
                carrierPath: WoundCarrierCatalog.NpcPath))
        };

        var catalog = BuildCatalog(enemies: CombatRoot(
            "enemiesData",
            withStaleId,
            withoutStaleId));

        Assert.Equal(
            2,
            catalog.Issues.Count(issue => issue.Code == "wound_carrier_npc_bound_combatant"));
        Assert.False(catalog.TryResolveOne("wound_stale_named", out _));
        Assert.False(catalog.TryResolveOne("wound_named_without_id", out _));
    }

    [Fact]
    public void Build_AcceptsMovedDedicatedNpcOnlyStateButRejectsStaleCombatPlusNpcAsContinuity()
    {
        var moved = Wound(
            "wound_promoted",
            ownerKind: "npc",
            ownerId: "npc_promoted",
            carrierPath: WoundCarrierCatalog.NpcPath);
        var emptyPromotedCombatant = new JsonObject
        {
            ["NPCId"] = "npc_promoted",
            ["combatantId"] = "combatant_old",
            ["activeWounds"] = new JsonArray()
        };

        var movedCatalog = BuildCatalog(
            npcs: WoundContractTestData.CreateNamedNpcCarrier("npc_promoted", moved),
            enemies: CombatRoot("enemiesData", emptyPromotedCombatant));

        Assert.Empty(movedCatalog.Issues);
        AssertOccurrence(
            movedCatalog,
            "wound_promoted",
            "mortal_world",
            "npc",
            "npc_promoted",
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.NpcPath + ".entries[0].activeWounds[0]");

        var staleCombatWound = Wound(
            "wound_promoted",
            ownerKind: "combatant",
            ownerId: "combatant_old",
            carrierPath: WoundCarrierCatalog.EnemiesPath);
        var staleCombatant = Combatant("combatant_old", staleCombatWound);
        staleCombatant["NPCId"] = "npc_promoted";

        var staleCatalog = BuildCatalog(
            npcs: WoundContractTestData.CreateNamedNpcCarrier("npc_promoted", moved),
            enemies: CombatRoot("enemiesData", staleCombatant));

        AssertIssue(staleCatalog, "wound_carrier_npc_bound_combatant");
        AssertIssue(staleCatalog, "wound_carrier_duplicate_occurrence");
        Assert.False(staleCatalog.TryResolveOne("wound_promoted", out _));
    }

    [Theory]
    [InlineData("unsupported_actor", "actor_invalid", "Chaos Sea")]
    [InlineData("Guardian", "actor_invalid", "Chaos Sea")]
    [InlineData("guardian", " actor_invalid ", "Chaos Sea")]
    [InlineData("guardian", "actor_invalid", "Mortal World")]
    public void Build_RejectsUnsupportedOrInexactAfterlifeCarrierIdentity(
        string actorType,
        string actorId,
        string realm)
    {
        var wound = Wound(
            "wound_invalid_afterlife",
            realm: "chaos_sea",
            ownerKind: "guardian",
            ownerId: "actor_invalid",
            carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
            domain: "spiritual");
        var root = ProfileRoot(Profile(actorType, actorId, realm, wound));

        var catalog = BuildCatalog(profiles: root);

        AssertIssue(catalog, "wound_carrier_afterlife_identity_invalid");
        Assert.False(catalog.TryResolveOne("wound_invalid_afterlife", out _));
    }

    [Fact]
    public void Build_RejectsMalformedAfterlifeActiveWoundsWithoutClosingSiblingFields()
    {
        var malformed = Profile("guardian", "guardian_malformed", "Chaos Sea");
        malformed["activeWounds"] = new JsonObject();
        malformed["progression"] = new JsonObject { ["insight"] = 3 };
        var root = ProfileRoot(malformed);
        root["profileAudit"] = new JsonObject { ["lastTurn"] = 42 };

        var catalog = BuildCatalog(profiles: root);

        AssertIssue(catalog, "wound_carrier_invalid_field");
        Assert.Empty(catalog.Occurrences);
    }

    [Fact]
    public void Build_SpiritualConflictRootCannotBecomeAWoundCarrier()
    {
        Assert.DoesNotContain(
            typeof(WoundCarrierCatalogInput).GetProperties(),
            property => property.Name.Contains("Conflict", StringComparison.OrdinalIgnoreCase));

        var conflictShapedRoot = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict_test",
                ["activeWounds"] = Array(Wound(
                    "wound_conflict_forbidden",
                    realm: "chaos_sea",
                    ownerKind: "player_soul",
                    ownerId: "player_soul",
                    carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath,
                    domain: "spiritual"))
            }
        };

        var catalog = BuildCatalog(profiles: conflictShapedRoot);

        AssertIssue(catalog, "wound_carrier_invalid_root");
        Assert.Empty(catalog.Occurrences);
        Assert.False(catalog.TryResolveOne("wound_conflict_forbidden", out _));
    }

    [Fact]
    public void Build_DoesNotMutateInputsAndOccurrencesAreDetachedFromLaterCallerMutation()
    {
        var player = WoundContractTestData.CreatePlayerCarrier(Wound("wound_detached"));
        var npc = WoundContractTestData.CreateNamedNpcCarrier();
        var enemies = CombatRoot(
            "enemiesData",
            new JsonObject
            {
                ["combatantRef"] = "not-yet-materialized",
                ["activeWounds"] = new JsonArray(),
                ["unrelatedState"] = new JsonObject { ["morale"] = 7 }
            });
        var allies = CombatRoot("alliesData");
        var profiles = ProfileRoot(Profile("guardian", "guardian_unwounded", "Chaos Sea"));
        profiles["profileAudit"] = new JsonObject { ["lastTurn"] = 42 };
        var before = new[] { player, npc, enemies, allies, profiles }
            .Select(static root => root.DeepClone())
            .ToArray();

        var catalog = BuildCatalog(player, npc, enemies, allies, profiles);

        Assert.Empty(catalog.Issues);
        var roots = new[] { player, npc, enemies, allies, profiles };
        for (var index = 0; index < roots.Length; index++)
            Assert.True(JsonNode.DeepEquals(before[index], roots[index]));

        Assert.True(catalog.TryResolveOne("wound_detached", out var occurrence));
        var originalName = occurrence.Wound.Display.Name;
        player["activeWounds"]![0]!["display"]!["name"] = "Подменённое имя";
        player["activeWounds"]![0]!["owner"]!["ownerId"] = "forged_owner";

        Assert.Equal(originalName, occurrence.Wound.Display.Name);
        Assert.Equal("player_current", occurrence.Wound.Owner.OwnerId);
    }

    [Fact]
    public void Build_MissingPristineRootsAreValidAndResolutionIsExactOrdinalOnly()
    {
        var empty = BuildCatalog();

        Assert.Empty(empty.Issues);
        Assert.Empty(empty.Occurrences);

        var exact = BuildCatalog(
            player: WoundContractTestData.CreatePlayerCarrier(Wound("wound_Exact")));

        Assert.Empty(exact.Issues);
        Assert.True(exact.TryResolveOne("wound_Exact", out _));
        Assert.False(exact.TryResolveOne("WOUND_EXACT", out _));
        Assert.False(exact.TryResolveOne("wound_Еxact", out _));
        Assert.False(exact.TryResolveOne("Рваная рана левого бока", out _));
    }

    private static WoundCarrierCatalog BuildCatalog(
        JsonObject? player = null,
        JsonObject? npcs = null,
        JsonObject? enemies = null,
        JsonObject? allies = null,
        JsonObject? profiles = null) =>
        WoundCarrierCatalog.Build(new WoundCarrierCatalogInput(
            player,
            npcs,
            enemies,
            allies,
            profiles));

    private static JsonObject Wound(
        string woundId,
        string realm = "mortal_world",
        string ownerKind = "player",
        string ownerId = "player_current",
        string carrierPath = "game_state/player/wounds.json",
        string lifecycle = "active",
        string domain = "physical") =>
        WoundContractTestData.CreateActiveWound(
            woundId,
            realm,
            ownerKind,
            ownerId,
            carrierPath,
            lifecycle,
            domain);

    private static JsonObject Combatant(string combatantId, params JsonObject[] wounds) => new()
    {
        ["combatantId"] = combatantId,
        ["displayName"] = "Боевой участник",
        ["activeWounds"] = Array(wounds)
    };

    private static JsonObject CombatRoot(string collection, params JsonObject[] combatants) => new()
    {
        [collection] = Array(combatants),
        ["unrelatedCombatAudit"] = new JsonObject { ["round"] = 1 }
    };

    private static JsonObject Profile(
        string actorType,
        string actorId,
        string realm,
        params JsonObject[] wounds) => new()
    {
        ["actorType"] = actorType,
        ["actorId"] = actorId,
        ["realm"] = realm,
        ["displayName"] = "Духовный актор",
        ["activeWounds"] = Array(wounds)
    };

    private static JsonObject ProfileRoot(params JsonObject[] profiles) => new()
    {
        ["schemaVersion"] = 1,
        ["profiles"] = Array(profiles)
    };

    private static JsonArray Array(params JsonObject[] nodes)
    {
        var result = new JsonArray();
        foreach (var node in nodes)
            result.Add(node.DeepClone());
        return result;
    }

    private static WoundCarrierOccurrence AssertOccurrence(
        WoundCarrierCatalog catalog,
        string woundId,
        string realm,
        string ownerKind,
        string ownerId,
        string carrierPath,
        string jsonPath)
    {
        Assert.True(catalog.TryResolveOne(woundId, out var occurrence), Describe(catalog));
        Assert.Equal(woundId, occurrence.WoundId);
        Assert.Equal(carrierPath, occurrence.FilePath);
        Assert.Equal(jsonPath, occurrence.JsonPath);
        Assert.Equal(
            new WoundCarrierCoordinate(realm, ownerKind, ownerId, carrierPath),
            occurrence.Coordinate);
        Assert.Equal(woundId, occurrence.Wound.WoundId);
        return occurrence;
    }

    private static void AssertIssue(WoundCarrierCatalog catalog, string code) =>
        Assert.Contains(catalog.Issues, issue =>
            issue.Code == code &&
            issue.Severity == IssueSeverity.Error &&
            issue.Section == "wound_materialization" &&
            !string.IsNullOrWhiteSpace(issue.Expected) &&
            !string.IsNullOrWhiteSpace(issue.RepairHint));

    private static string Describe(WoundCarrierCatalog catalog) =>
        string.Join(
            Environment.NewLine,
            catalog.Issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));
}
