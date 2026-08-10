using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class FactionCoreChangesContractTests
{
    [Theory]
    [InlineData("profile")]
    [InlineData("purposeAndPrinciples")]
    [InlineData("progressionAndPower")]
    [InlineData("governanceAndLeadership")]
    [InlineData("playerMembership")]
    [InlineData("relations")]
    public void Evaluate_CompleteGroup_AppliesAbsoluteValuesAndPreservesUnrelatedState(
        string groupName)
    {
        var preTurn = BuildMaterializedFactionCore();
        var command = BuildCommand(
            "faction_watch",
            groupName,
            BuildCompleteGroup(groupName));
        var current = AddCommand(preTurn, command);
        var originalReceipt = preTurn["factions"]![0]!["materialization"]!.ToJsonString();

        var evaluation = Evaluate(current, preTurn);

        Assert.True(evaluation.CanApply);
        var result = current.DeepClone().AsObject();
        FactionCoreChangesContract.Apply(result, evaluation);
        var faction = FindFaction(result, "faction_watch");
        AssertAppliedGroup(faction, command, groupName);
        Assert.Equal(
            "The first watch captain kept the bridge ledger.",
            faction["memory"]!["summary"]!.GetValue<string>());
        Assert.Equal(originalReceipt, faction["materialization"]!.ToJsonString());
        Assert.False(result.ContainsKey(FactionCoreChangesContract.PropertyName));
    }

    [Fact]
    public void Evaluate_RelationsGroup_ReplacesOnlyAbsoluteRelations()
    {
        var preTurn = BuildMaterializedFactionCore();
        var current = preTurn.DeepClone().AsObject();
        current[FactionCoreChangesContract.PropertyName] = new JsonArray
        {
            new JsonObject
            {
                ["factionId"] = "faction_watch",
                ["reason"] = "The watch signed a bridge compact.",
                ["relations"] = new JsonObject
                {
                    ["entries"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["targetFactionId"] = "faction_bridge_compact",
                            ["status"] = "allied",
                            ["description"] = "Both factions defend the bridge."
                        }
                    }
                }
            }
        };
        var originalReceipt = current["factions"]![0]!["materialization"]!.ToJsonString();

        var evaluation = FactionCoreChangesContract.Evaluate(
            current,
            preTurn,
            Authority());

        Assert.True(evaluation.CanApply);
        var result = current.DeepClone().AsObject();
        FactionCoreChangesContract.Apply(result, evaluation);
        var faction = result["factions"]![0]!.AsObject();
        Assert.Single(faction["relations"]!.AsArray());
        Assert.Equal(
            "fmat_watch",
            faction["materialization"]!["materializationId"]!.GetValue<string>());
        Assert.Equal(originalReceipt, faction["materialization"]!.ToJsonString());
        Assert.False(result.ContainsKey(FactionCoreChangesContract.PropertyName));
    }

    [Theory]
    [InlineData("profile")]
    [InlineData("purposeAndPrinciples")]
    [InlineData("progressionAndPower")]
    [InlineData("governanceAndLeadership")]
    [InlineData("playerMembership")]
    [InlineData("relations")]
    public void Evaluate_PartialCompleteGroup_IsRejected(string groupName)
    {
        var preTurn = BuildMaterializedFactionCore();
        var group = BuildCompleteGroup(groupName);
        RemoveRequiredGroupMember(groupName, group);
        var current = AddCommand(
            preTurn,
            BuildCommand("faction_watch", groupName, group));

        var evaluation = Evaluate(current, preTurn);

        Assert.False(evaluation.CanApply);
        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == $"faction_core_changes_{GroupCode(groupName)}_invalid" &&
            issue.Actor == "mortal_faction:faction_watch");
    }

    [Theory]
    [InlineData("progressionAndPower")]
    [InlineData("governanceAndLeadership")]
    [InlineData("relations")]
    public void Evaluate_RecursiveUnknownMember_IsRejected(string groupName)
    {
        var preTurn = BuildMaterializedFactionCore();
        var group = BuildCompleteGroup(groupName);
        var expectedPath = groupName switch
        {
            "progressionAndPower" => "powerProfile.futureScale",
            "governanceAndLeadership" => "leadership.futureLeaderNote",
            _ => "entries[0].futureRelationNote"
        };
        switch (groupName)
        {
            case "progressionAndPower":
                group["powerProfile"]!["futureScale"] = 1;
                break;
            case "governanceAndLeadership":
                group["leadership"]!["futureLeaderNote"] = "not authorized";
                break;
            default:
                group["entries"]![0]!["futureRelationNote"] = "not authorized";
                break;
        }

        var evaluation = Evaluate(
            AddCommand(
                preTurn,
                BuildCommand("faction_watch", groupName, group)),
            preTurn);

        var issue = Assert.Single(evaluation.Issues, item =>
            item.Code == "faction_core_changes_unknown_member");
        Assert.Contains(expectedPath, issue.FilePath, StringComparison.Ordinal);
        Assert.Equal("mortal_faction:faction_watch", issue.Actor);
    }

    [Theory]
    [InlineData("initialId")]
    [InlineData("initialFactionId")]
    [InlineData("isNewFaction")]
    [InlineData("materialization")]
    [InlineData("ranks")]
    [InlineData("structuredBonuses")]
    [InlineData("resources")]
    [InlineData("activeProjects")]
    [InlineData("completedProjects")]
    [InlineData("customStates")]
    [InlineData("scribeChronicle")]
    [InlineData("controlledTerritories")]
    [InlineData("NPCFactionAffiliationChanges")]
    public void Evaluate_ProtectedRootMember_IsRejected(string protectedMember)
    {
        var preTurn = BuildMaterializedFactionCore();
        var command = BuildCommand(
            "faction_watch",
            "profile",
            BuildCompleteGroup("profile"));
        command[protectedMember] = protectedMember == "materialization"
            ? new JsonObject()
            : new JsonArray();

        var evaluation = Evaluate(AddCommand(preTurn, command), preTurn);

        Assert.False(evaluation.CanApply);
        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_protected_member" &&
            issue.FilePath.EndsWith($".{protectedMember}", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_ProtectedNestedIdentityMember_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var profile = BuildCompleteGroup("profile");
        profile["factionId"] = "faction_other";

        var evaluation = Evaluate(
            AddCommand(
                preTurn,
                BuildCommand("faction_watch", "profile", profile)),
            preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_protected_member" &&
            issue.FilePath.EndsWith(".profile.factionId", StringComparison.Ordinal));
    }

    [Fact]
    public void Evaluate_DuplicateCommandTarget_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var current = preTurn.DeepClone().AsObject();
        current[FactionCoreChangesContract.PropertyName] = new JsonArray(
            BuildCommand(
                "faction_watch",
                "profile",
                BuildCompleteGroup("profile")),
            BuildCommand(
                "faction_watch",
                "purposeAndPrinciples",
                BuildCompleteGroup("purposeAndPrinciples")));

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_duplicate_target" &&
            issue.Actor == "mortal_faction:faction_watch");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Evaluate_DuplicateRelationTarget_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var relations = BuildCompleteGroup("relations");
        relations["entries"]!.AsArray().Add(new JsonObject
        {
            ["targetFactionId"] = "faction_bridge_compact",
            ["status"] = "neutral",
            ["description"] = "A duplicate absolute relation is ambiguous."
        });

        var evaluation = Evaluate(
            AddCommand(
                preTurn,
                BuildCommand("faction_watch", "relations", relations)),
            preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_relations_invalid" &&
            issue.FilePath.EndsWith(".targetFactionId", StringComparison.Ordinal));
        Assert.False(evaluation.CanApply);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("blank")]
    public void Evaluate_MissingOrBlankReason_IsRejected(string variation)
    {
        var preTurn = BuildMaterializedFactionCore();
        var command = BuildCommand(
            "faction_watch",
            "profile",
            BuildCompleteGroup("profile"));
        if (variation == "missing")
            command.Remove("reason");
        else
            command["reason"] = "   ";

        var evaluation = Evaluate(AddCommand(preTurn, command), preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_reason_required");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Evaluate_CommandWithoutGroup_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var current = AddCommand(
            preTurn,
            new JsonObject
            {
                ["factionId"] = "faction_watch",
                ["reason"] = "This command deliberately omits all groups."
            });

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_empty_mutation");
        Assert.False(evaluation.CanApply);
    }

    [Theory]
    [InlineData("faction_unknown", "faction_core_changes_target_not_existing")]
    [InlineData("Faction_watch", "faction_core_changes_target_not_exact")]
    public void Evaluate_UnknownOrCaseVariantFactionTarget_IsRejected(
        string factionId,
        string expectedCode)
    {
        var preTurn = BuildMaterializedFactionCore();
        var current = AddCommand(
            preTurn,
            BuildCommand(
                factionId,
                "profile",
                BuildCompleteGroup("profile")));

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == expectedCode &&
            issue.Actor == $"mortal_faction:{factionId}");
        Assert.False(evaluation.CanApply);
    }

    [Theory]
    [InlineData("faction_unknown")]
    [InlineData("Faction_bridge_compact")]
    [InlineData("faction_watch")]
    public void Evaluate_UnknownCaseVariantOrSelfRelationTarget_IsRejected(
        string targetFactionId)
    {
        var preTurn = BuildMaterializedFactionCore();
        var relations = BuildCompleteGroup("relations");
        relations["entries"]![0]!["targetFactionId"] = targetFactionId;
        var current = AddCommand(
            preTurn,
            BuildCommand("faction_watch", "relations", relations));

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_relations_invalid" &&
            issue.Actor == "mortal_faction:faction_watch");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Evaluate_ReceiptlessTarget_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        preTurn["factions"]![0]!.AsObject().Remove("materialization");
        var current = AddCommand(
            preTurn,
            BuildCommand(
                "faction_watch",
                "profile",
                BuildCompleteGroup("profile")));

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_target_not_materialized" &&
            issue.Actor == "mortal_faction:faction_watch");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Evaluate_ReceiptlessFullResendPlusCommand_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var receiptless = preTurn["factions"]![0]!.DeepClone().AsObject();
        receiptless.Remove("materialization");
        preTurn["factions"] = new JsonArray(receiptless);

        var resent = receiptless.DeepClone().AsObject();
        resent["materialization"] = BuildEnvelope(
            "faction_watch",
            "fmat_resent_watch");
        var command = BuildCommand(
            "faction_watch",
            "purposeAndPrinciples",
            BuildCompleteGroup("purposeAndPrinciples"));
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(resent),
            [FactionCoreChangesContract.PropertyName] = new JsonArray(command)
        };

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_existing_full_resend_forbidden" &&
            issue.Actor == "mortal_faction:faction_watch");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Apply_InvalidCommand_PreservesCommandAndFactionState()
    {
        var preTurn = BuildMaterializedFactionCore();
        var command = BuildCommand(
            "faction_watch",
            "profile",
            BuildCompleteGroup("profile"));
        command.Remove("reason");
        var current = AddCommand(preTurn, command);
        var before = current.ToJsonString();
        var evaluation = Evaluate(current, preTurn);

        FactionCoreChangesContract.Apply(current, evaluation);

        Assert.False(evaluation.CanApply);
        Assert.True(current.ContainsKey(FactionCoreChangesContract.PropertyName));
        Assert.Equal(before, current.ToJsonString());
    }

    [Fact]
    public void Evaluate_IdenticalExistingFullResend_IsForbidden()
    {
        var preTurn = BuildMaterializedFactionCore();
        var resent = preTurn["factions"]![0]!.DeepClone();
        var current = new JsonObject
        {
            ["factionDataChanges"] = new JsonArray(resent)
        };

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_existing_full_resend_forbidden" &&
            issue.Actor == "mortal_faction:faction_watch");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Evaluate_OrdinaryCanonicalBaseline_IsNotTreatedAsFullResend()
    {
        var preTurn = BuildMaterializedFactionCore();
        var current = preTurn.DeepClone().AsObject();

        var evaluation = Evaluate(current, preTurn);

        Assert.DoesNotContain(evaluation.Issues, issue =>
            issue.Code == "faction_existing_full_resend_forbidden");
        Assert.False(evaluation.HasCommand);
    }

    [Fact]
    public void Evaluate_DuplicateEffectiveCurrentIdentity_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var current = AddCommand(
            preTurn,
            BuildCommand(
                "faction_watch",
                "profile",
                BuildCompleteGroup("profile")));
        current["factionDataChanges"] = new JsonArray(
            preTurn["factions"]![0]!.DeepClone());

        var evaluation = Evaluate(current, preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_duplicate_effective_identity" &&
            issue.Actor == "mortal_faction:faction_watch");
        Assert.False(evaluation.CanApply);
    }

    [Fact]
    public void Evaluate_UnknownLeaderNpc_IsRejected()
    {
        var preTurn = BuildMaterializedFactionCore();
        var governance = BuildCompleteGroup("governanceAndLeadership");
        governance["leadership"]!["leaderNpcIds"] =
            new JsonArray("npc_unknown_captain");

        var evaluation = Evaluate(
            AddCommand(
                preTurn,
                BuildCommand(
                    "faction_watch",
                    "governanceAndLeadership",
                    governance)),
            preTurn);

        Assert.Contains(evaluation.Issues, issue =>
            issue.Code == "faction_core_changes_governance_and_leadership_invalid" &&
            issue.FilePath.Contains("leaderNpcIds", StringComparison.Ordinal));
        Assert.False(evaluation.CanApply);
    }

    private static FactionCoreChangesContract.Evaluation Evaluate(
        JsonObject current,
        JsonObject preTurn) =>
        FactionCoreChangesContract.Evaluate(
            current,
            preTurn,
            Authority());

    private static FactionCoreChangesContract.Authority Authority() =>
        new(
            new HashSet<string>(StringComparer.Ordinal)
            {
                "faction_watch",
                "faction_bridge_compact"
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                "npc_watch_captain"
            });

    private static JsonObject AddCommand(
        JsonObject preTurn,
        JsonObject command)
    {
        var current = preTurn.DeepClone().AsObject();
        current[FactionCoreChangesContract.PropertyName] =
            new JsonArray(command);
        return current;
    }

    private static JsonObject BuildCommand(
        string factionId,
        string groupName,
        JsonObject group) =>
        new()
        {
            ["factionId"] = factionId,
            ["reason"] = "A complete absolute faction update is required.",
            [groupName] = group
        };

    private static JsonObject BuildCompleteGroup(string groupName) =>
        groupName switch
        {
            "profile" => new JsonObject
            {
                ["name"] = "Bridge Watch",
                ["description"] = "Wardens who protect both bridge approaches.",
                ["image_prompt"] =
                    "weathered bridge wardens beneath blue and brass banners",
                ["factionColor"] = "#315A88"
            },
            "purposeAndPrinciples" => new JsonObject
            {
                ["purpose"] = "Guard both banks of the river crossing.",
                ["currentAgenda"] = "Ratify the bridge compact before winter.",
                ["principles"] = new JsonArray(
                    "No traveler is denied a warning.",
                    "Bridge tolls fund bridge repairs.")
            },
            "progressionAndPower" => new JsonObject
            {
                ["level"] = 4,
                ["experience"] = 120,
                ["experienceForNextLevel"] = 200,
                ["developmentArchetype"] = "Balanced",
                ["customArchetypePriorities"] = null,
                ["powerProfile"] = new JsonObject
                {
                    ["military"] = 2,
                    ["economic"] = 4,
                    ["social"] = 3,
                    ["covert"] = 1,
                    ["logistics"] = 4,
                    ["stability"] = 3,
                    ["arcane_tech"] = 0,
                    ["exploration"] = 2
                }
            },
            "governanceAndLeadership" => new JsonObject
            {
                ["governance"] = new JsonObject
                {
                    ["model"] = "Elected bridge council",
                    ["decisionProcess"] =
                        "Five seats decide by a simple majority."
                },
                ["leadership"] = new JsonObject
                {
                    ["leadershipState"] = "headed",
                    ["summary"] = "The watch captain chairs the council.",
                    ["leaderNpcIds"] = new JsonArray("npc_watch_captain")
                }
            },
            "playerMembership" => new JsonObject
            {
                ["isPlayerFaction"] = false,
                ["isPlayerMember"] = true,
                ["playerRank"] = "Road Warden",
                ["playerBranch"] = "western_road",
                ["playerStrategyDirective"] = null,
                ["reputation"] = 85,
                ["reputationDescription"] = "Trusted road ally"
            },
            "relations" => new JsonObject
            {
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["targetFactionId"] = "faction_bridge_compact",
                    ["status"] = "allied",
                    ["description"] =
                        "Both factions defend the bridge."
                })
            },
            _ => throw new ArgumentOutOfRangeException(
                nameof(groupName),
                groupName,
                "Unknown faction core group.")
        };

    private static void RemoveRequiredGroupMember(
        string groupName,
        JsonObject group)
    {
        switch (groupName)
        {
            case "profile":
                group.Remove("description");
                break;
            case "purposeAndPrinciples":
                group.Remove("principles");
                break;
            case "progressionAndPower":
                group["powerProfile"]!.AsObject().Remove("exploration");
                break;
            case "governanceAndLeadership":
                group["leadership"]!.AsObject().Remove("summary");
                break;
            case "playerMembership":
                group.Remove("reputationDescription");
                break;
            case "relations":
                group["entries"]![0]!.AsObject().Remove("description");
                break;
        }
    }

    private static string GroupCode(string groupName) =>
        groupName switch
        {
            "purposeAndPrinciples" => "purpose_and_principles",
            "progressionAndPower" => "progression_and_power",
            "governanceAndLeadership" =>
                "governance_and_leadership",
            "playerMembership" => "player_membership",
            _ => groupName
        };

    private static void AssertAppliedGroup(
        JsonObject faction,
        JsonObject command,
        string groupName)
    {
        var group = command[groupName]!.AsObject();
        switch (groupName)
        {
            case "relations":
                Assert.True(JsonNode.DeepEquals(
                    group["entries"],
                    faction["relations"]));
                break;
            case "governanceAndLeadership":
                Assert.True(JsonNode.DeepEquals(
                    group["governance"],
                    faction["governance"]));
                Assert.True(JsonNode.DeepEquals(
                    group["leadership"],
                    faction["leadership"]));
                break;
            default:
                foreach (var property in group)
                {
                    Assert.True(
                        JsonNode.DeepEquals(
                            property.Value,
                            faction[property.Key]),
                        $"Expected absolute field {property.Key} to be replaced.");
                }

                break;
        }
    }

    private static JsonObject FindFaction(
        JsonObject root,
        string factionId) =>
        root["factions"]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(faction =>
                faction["factionId"]!.GetValue<string>() == factionId);

    private static JsonObject BuildMaterializedFactionCore() =>
        new()
        {
            ["factions"] = new JsonArray(
                BuildMaterializedFaction(
                    "faction_watch",
                    "Wayfarer Watch",
                    "fmat_watch"),
                BuildMaterializedFaction(
                    "faction_bridge_compact",
                    "Bridge Compact",
                    "fmat_bridge_compact"))
        };

    private static JsonObject BuildMaterializedFaction(
        string factionId,
        string name,
        string materializationId) =>
        new()
        {
            ["factionId"] = factionId,
            ["name"] = name,
            ["description"] = "A complete historical faction.",
            ["image_prompt"] = "weathered wardens beside an old stone bridge",
            ["factionColor"] = "#6A7382",
            ["purpose"] = "Keep the old road open.",
            ["currentAgenda"] = "Repair the western bridge.",
            ["principles"] = new JsonArray("Warn before judgment."),
            ["level"] = 2,
            ["experience"] = 30,
            ["experienceForNextLevel"] = 100,
            ["developmentArchetype"] = "Balanced",
            ["customArchetypePriorities"] = null,
            ["powerProfile"] = new JsonObject
            {
                ["military"] = 1,
                ["economic"] = 1,
                ["social"] = 1,
                ["covert"] = 1,
                ["logistics"] = 1,
                ["stability"] = 1,
                ["arcane_tech"] = 0,
                ["exploration"] = 1
            },
            ["isPlayerFaction"] = false,
            ["isPlayerMember"] = false,
            ["playerRank"] = null,
            ["playerBranch"] = null,
            ["playerStrategyDirective"] = null,
            ["reputation"] = 0,
            ["reputationDescription"] = null,
            ["relations"] = new JsonArray(),
            ["memory"] = new JsonObject
            {
                ["summary"] =
                    "The first watch captain kept the bridge ledger."
            },
            ["materialization"] = BuildEnvelope(
                factionId,
                materializationId)
        };

    private static JsonObject BuildEnvelope(
        string factionId,
        string materializationId) =>
        new()
        {
            ["schemaVersion"] = 1,
            ["materializationId"] = materializationId,
            ["factionType"] = "mortal_faction",
            ["factionId"] = factionId,
            ["materializedAtTurn"] = 12,
            ["state"] = "complete",
            ["capabilities"] = new JsonObject
            {
                ["hasFormalHierarchy"] = false,
                ["usesFactionResources"] = false,
                ["maintainsRelations"] = false,
                ["runsProjects"] = false,
                ["holdsTerritoryOrInfluence"] = false,
                ["supportsPlayerMembership"] = false,
                ["usesCustomMechanics"] = false
            },
            ["sections"] = new JsonObject
            {
                ["hierarchy"] = EmptyDisposition("No formal ranks exist yet."),
                ["resources"] = EmptyDisposition("No treasury exists yet."),
                ["relations"] = EmptyDisposition("No compact existed at materialization."),
                ["projects"] = EmptyDisposition("No chartered projects exist yet."),
                ["territoryAndInfluence"] = EmptyDisposition("No territory is claimed."),
                ["playerMembership"] = EmptyDisposition("The player was not a member."),
                ["customStates"] = EmptyDisposition("No custom mechanics exist.")
            }
        };

    private static JsonObject EmptyDisposition(string reason) =>
        new()
        {
            ["state"] = "empty_by_design",
            ["reason"] = reason
        };
}
