using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SpiritualWoundConsequenceTests
{
    public static IEnumerable<object[]> RegisteredProfileCases()
    {
        foreach (var descriptor in SpiritualWoundEffectProfileCatalog.Profiles.Values
                     .OrderBy(static value => value.Profile, StringComparer.Ordinal))
        {
            yield return new object[]
            {
                descriptor.Profile,
                descriptor.ProjectionKind,
                descriptor.Axis,
                DefaultMagnitudeJson(descriptor.Profile)
            };
        }
    }

    public static IEnumerable<object[]> PersistentActorCases()
    {
        yield return new object[] { "player", "player", "player", "player_soul" };
        yield return new object[] { "player", "player_soul", "player", "player_soul" };
        yield return new object[] { "player", "soul", "player", "player_soul" };
        yield return new object[] { "guardian", "guardian", "guardian", "guardian_projector" };
        yield return new object[] { "resident", "resident", "resident", "resident_projector" };
        yield return new object[] { "radiant_actor", "radiant_actor", "radiant_actor", "radiant_projector" };
        yield return new object[] { "afterlife_actor", "custom_afterlife_actor", "afterlife_actor", "afterlife_projector" };
    }

    public static IEnumerable<object[]> SideAxisCases()
    {
        yield return new object[] { "spiritual_action_cost_burden", "player", "actionCostAudit.player" };
        yield return new object[] { "spiritual_action_cost_burden", "opposition", "actionCostAudit.opposition" };
        yield return new object[] { "spiritual_strain_burden", "player", "playerSideStrain" };
        yield return new object[] { "spiritual_strain_burden", "opposition", "oppositionSideStrain" };
    }

    public static IEnumerable<object[]> MembershipFailureCases()
    {
        yield return new object[] { "missing", "spiritual_wound_conflict_participant_unresolved" };
        yield return new object[] { "duplicate", "spiritual_wound_conflict_participant_ambiguous" };
        yield return new object[] { "confusable", "spiritual_wound_conflict_participant_ambiguous" };
        yield return new object[] { "wrong_realm", "spiritual_wound_conflict_participant_realm_mismatch" };
    }

    public static IEnumerable<object[]> PendingResolutionStateCases()
    {
        yield return new object[] { "concession_pending" };
        yield return new object[] { "surrender_pending" };
        yield return new object[] { "retreat_pending" };
        yield return new object[] { "ready_to_resolve" };
    }

    public static IEnumerable<object[]> InvalidActiveConflictStateCases()
    {
        yield return new object[] { "resolved" };
        yield return new object[] { "repair_cancelled" };
        yield return new object[] { "future_unknown" };
    }

    [Theory]
    [MemberData(nameof(RegisteredProfileCases))]
    public void Project_PreservesEveryRegisteredProfilePayloadAndProvenance(
        string profile,
        object projectionKind,
        string sourceAxis,
        string magnitudeJson)
    {
        var fixture = CreateFixture(profile);

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.True(result.IsAccepted, DescribeIssues(result.Issues));
        var row = Assert.Single(result.Contributions);
        Assert.Equal(EffectMaterializationTestFixture.EffectId, row.EffectId);
        Assert.Equal("component_001", row.ComponentId);
        Assert.Equal(
            new EffectSourceKey(
                "chaos_sea",
                "wound",
                WoundId,
                DefinitionKey),
            row.WoundSource);
        Assert.Equal(
            new EffectTargetKey("chaos_sea", "guardian", "guardian_projector"),
            row.Actor);
        Assert.Equal("opposition", row.ResolvedSide);
        Assert.Equal(projectionKind, row.ProjectionKind);
        Assert.Equal(profile, row.Profile);
        Assert.Equal("pressure", row.Operation);
        Assert.Equal(sourceAxis, row.SourceAxis);
        Assert.Equal(ExpectedAxis(sourceAxis, "opposition"), row.ResolvedAxis);
        Assert.Equal(magnitudeJson, row.Magnitude.GetRawText());
        Assert.Equal(100, row.Priority);
        Assert.Equal(1, row.CurrentStacks);
        Assert.Equal(
            fixture.Snapshot.Components[0].Payload.GetRawText(),
            row.ProfilePayload.GetRawText());
    }

    [Theory]
    [MemberData(nameof(PersistentActorCases))]
    public void Project_UsesClosedParticipantActorAdapter(
        string targetKind,
        string conflictActorType,
        string expectedKind,
        string actorId)
    {
        var side = string.Equals(targetKind, "player", StringComparison.Ordinal)
            ? "player"
            : "opposition";
        var fixture = CreateFixture(
            "spiritual_roll_hindrance",
            targetKind,
            conflictActorType,
            actorId,
            side);

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.True(result.IsAccepted, DescribeIssues(result.Issues));
        var row = Assert.Single(result.Contributions);
        Assert.Equal(expectedKind, row.Actor.Kind);
        Assert.Equal(actorId, row.Actor.TargetId);
        Assert.Equal(side, row.ResolvedSide);
    }

    [Theory]
    [MemberData(nameof(SideAxisCases))]
    public void Project_MapsSameCanonicalPayloadInBothDirections(
        string profile,
        string selectedSide,
        string expectedAxis)
    {
        var selected = CreateFixture(profile, side: selectedSide);
        var oppositeSide = string.Equals(selectedSide, "player", StringComparison.Ordinal)
            ? "opposition"
            : "player";
        var opposite = CreateFixture(profile, side: oppositeSide);

        var selectedResult = SpiritualWoundConflictContributionProjector.Project(
            selected.Snapshot,
            selected.Sources,
            selected.Targets,
            selected.Conflict);
        var oppositeResult = SpiritualWoundConflictContributionProjector.Project(
            opposite.Snapshot,
            opposite.Sources,
            opposite.Targets,
            opposite.Conflict);

        var selectedRow = Assert.Single(selectedResult.Contributions);
        var oppositeRow = Assert.Single(oppositeResult.Contributions);
        Assert.Equal(expectedAxis, selectedRow.ResolvedAxis);
        Assert.Equal(
            selectedRow.ProfilePayload.GetRawText(),
            oppositeRow.ProfilePayload.GetRawText());
        Assert.Equal(selectedRow.WoundSource, oppositeRow.WoundSource);
        Assert.NotEqual(selectedRow.ResolvedAxis, oppositeRow.ResolvedAxis);
    }

    [Theory]
    [MemberData(nameof(MembershipFailureCases))]
    public void Project_FailsClosedForUnprovenParticipantMembership(
        string mutation,
        string expectedCode)
    {
        var fixture = CreateFixture("spiritual_roll_hindrance");
        EffectTargetAuthority targets;
        switch (mutation)
        {
            case "missing":
                targets = CreateTargetAuthority(
                    new EffectTargetExport(
                        "chaos_sea",
                        "player",
                        "player_soul",
                        false));
                break;
            case "wrong_realm":
                targets = CreateTargetAuthority(
                    PlayerTarget(),
                    new EffectTargetExport(
                        "shining_abode",
                        "guardian",
                        "guardian_projector",
                        false));
                break;
            case "duplicate":
                fixture.Conflict["activeConflict"]!["playerSide"]!["supporters"]!
                    .AsArray()
                    .Add(Participant("guardian", "guardian_projector"));
                targets = fixture.Targets;
                break;
            case "confusable":
                fixture.Conflict["activeConflict"]!["playerSide"]!["supporters"]!
                    .AsArray()
                    .Add(Participant("resident", "guardіan_projector"));
                targets = fixture.Targets;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation));
        }

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            targets,
            fixture.Conflict);

        Assert.False(result.IsAccepted);
        Assert.Empty(result.Contributions);
        Assert.Contains(result.Issues, issue =>
            string.Equals(issue.Code, expectedCode, StringComparison.Ordinal));
    }

    [Fact]
    public void Project_ClosedConflictReturnsEmptyWithoutChangingPersistentEffect()
    {
        var fixture = CreateFixture("spiritual_roll_hindrance");
        var payloadBefore = fixture.Snapshot.Components[0].Payload.GetRawText();
        fixture.Conflict["activeConflict"] = null;

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.True(result.IsAccepted);
        Assert.Empty(result.Contributions);
        Assert.Single(fixture.Snapshot.Components);
        Assert.Equal(payloadBefore, fixture.Snapshot.Components[0].Payload.GetRawText());
    }

    [Fact]
    public void Project_WoundedNonparticipantIsIgnored()
    {
        var fixture = CreateFixture("spiritual_roll_hindrance");
        fixture.Conflict["activeConflict"]!["oppositionSide"]!["leadContestant"] =
            Participant("guardian", "guardian_other");

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.True(result.IsAccepted, DescribeIssues(result.Issues));
        Assert.Empty(result.Contributions);
    }

    [Theory]
    [MemberData(nameof(PendingResolutionStateCases))]
    public void Project_LegalPendingActiveConflictStatesRemainLive(
        string resolutionState)
    {
        var fixture = CreateFixture("spiritual_roll_hindrance");
        fixture.Conflict["activeConflict"]!["resolutionState"] = resolutionState;

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.True(result.IsAccepted, DescribeIssues(result.Issues));
        Assert.Single(result.Contributions);
    }

    [Theory]
    [MemberData(nameof(InvalidActiveConflictStateCases))]
    public void Project_TerminalOrUnknownStateUnderActiveConflictRejects(
        string resolutionState)
    {
        var fixture = CreateFixture("spiritual_roll_hindrance");
        fixture.Conflict["activeConflict"]!["resolutionState"] = resolutionState;

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.False(result.IsAccepted);
        Assert.Empty(result.Contributions);
        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.Code,
                "spiritual_wound_conflict_state_invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Project_SourceComponentDisagreementRejectsAllContributions()
    {
        var fixture = CreateFixture("spiritual_action_cost_burden");
        var mismatched = fixture.Definition.DeepClone().AsObject();
        mismatched["components"]![0]!["payload"]!["magnitude"] = 2;
        var sources = CreateSourceAuthority(mismatched);

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.False(result.IsAccepted);
        Assert.Empty(result.Contributions);
        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.Code,
                "spiritual_wound_source_component_mismatch",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Project_DetachesOutputAndLeavesRealCombatConditionSiblingByteIdentical()
    {
        var fixture = CreateFixture("spiritual_action_cost_burden");
        var active = fixture.Conflict["activeConflict"]!.AsObject();
        active["combatConditions"]!.AsArray().Add(CreateCanonicalCombatConditionSibling());
        var conditionBefore = active["combatConditions"]!.ToJsonString();
        var conflictBefore = fixture.Conflict.ToJsonString();
        var effectPayloadBefore = fixture.Effect["components"]![0]!["payload"]!.ToJsonString();

        var result = SpiritualWoundConflictContributionProjector.Project(
            fixture.Snapshot,
            fixture.Sources,
            fixture.Targets,
            fixture.Conflict);

        Assert.True(result.IsAccepted, DescribeIssues(result.Issues));
        var row = Assert.Single(result.Contributions);
        Assert.Equal(conditionBefore, active["combatConditions"]!.ToJsonString());
        Assert.Equal(conflictBefore, fixture.Conflict.ToJsonString());
        Assert.Equal(
            effectPayloadBefore,
            fixture.Effect["components"]![0]!["payload"]!.ToJsonString());

        fixture.Effect["components"]![0]!["payload"]!["magnitude"] = 1;
        fixture.Definition["components"]![0]!["payload"]!["magnitude"] = 1;
        active["oppositionSide"]!["leadContestant"]!["actorId"] = "mutated";

        Assert.Equal("3", row.Magnitude.GetRawText());
        Assert.Equal("guardian_projector", row.Actor.TargetId);
        Assert.Equal(WoundId, row.WoundSource.SourceId);
        Assert.Contains("\"magnitude\":3", row.ProfilePayload.GetRawText());
    }

    private const string WoundId = "wound_projector";
    private const string DefinitionKey = "definition_projector";

    private static Fixture CreateFixture(
        string profile,
        string targetKind = "guardian",
        string conflictActorType = "guardian",
        string actorId = "guardian_projector",
        string side = "opposition")
    {
        var magnitude = JsonNode.Parse(DefaultMagnitudeJson(profile));
        var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(
            profile,
            targetKind,
            "chaos_sea",
            WoundId,
            operation: "pressure",
            magnitude: magnitude);
        effect["target"]!["targetId"] = actorId;
        effect["source"]!["definitionKey"] = DefinitionKey;
        effect["components"]![0]!["priority"] = 100;

        var profiles = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["profiles"] = new JsonArray(new JsonObject
            {
                ["actorType"] = ProfileActorType(targetKind),
                ["actorId"] = actorId,
                ["realm"] = "chaos_sea",
                ["activeEffects"] = new JsonArray(effect.DeepClone())
            })
        };
        var identity = EffectMaterializationTestFixture.CreateIdentityIndex(effect);
        var owner = identity["entries"]![0]!["owner"]!.AsObject();
        owner["ownerId"] = actorId;
        owner["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
        var snapshot = EffectMechanicsSnapshot.Build(new EffectMechanicsInput(
            new EffectCarrierCatalogInput(
                null,
                null,
                null,
                null,
                profiles,
                null),
            identity));
        Assert.True(snapshot.IsAccepted, DescribeIssues(snapshot.Issues));
        var acceptedComponent = Assert.Single(snapshot.Components);
        Assert.Equal(
            new EffectSourceKey(
                "chaos_sea",
                "wound",
                WoundId,
                DefinitionKey),
            acceptedComponent.Source);
        Assert.Equal(targetKind, acceptedComponent.TargetKind);
        Assert.Equal(actorId, acceptedComponent.TargetId);

        var definition = EffectMaterializationTestFixture.CreateSpiritualWoundDefinition(
            profile,
            targetKind,
            "chaos_sea",
            WoundId,
            DefinitionKey,
            "pressure",
            magnitude);
        definition["components"]![0]!["priority"] = 100;
        var sources = CreateSourceAuthority(definition);
        var targetExports = new List<EffectTargetExport>
        {
            PlayerTarget(),
            new(
                "chaos_sea",
                "guardian",
                "guardian_other",
                false)
        };
        if (!string.Equals(targetKind, "player", StringComparison.Ordinal))
        {
            targetExports.Add(new EffectTargetExport(
                "chaos_sea",
                targetKind,
                actorId,
                false));
        }
        var targets = CreateTargetAuthority(targetExports.ToArray());
        var conflict = CreateConflict(
            targetKind,
            conflictActorType,
            actorId,
            side);
        return new Fixture(snapshot, sources, targets, conflict, effect, definition);
    }

    private static EffectSourceAuthority CreateSourceAuthority(JsonObject definition) =>
        EffectSourceAuthority.Build(new EffectSourceAuthorityInput(
            new[]
            {
                new EffectSourceExport(
                    "chaos_sea",
                    "wound",
                    WoundId,
                    new JsonArray(definition.DeepClone()),
                    false,
                    true,
                    false)
            },
            Array.Empty<EffectSourceExport>(),
            new HashSet<string>(StringComparer.Ordinal)));

    private static EffectTargetAuthority CreateTargetAuthority(
        params EffectTargetExport[] targets) =>
        EffectTargetAuthority.Build(new EffectTargetAuthorityInput(
            targets,
            Array.Empty<EffectTargetExport>(),
            new HashSet<string>(StringComparer.Ordinal),
            null));

    private static EffectTargetExport PlayerTarget() =>
        new("chaos_sea", "player", "player_soul", false);

    private static string ProfileActorType(string targetKind) =>
        targetKind switch
        {
            "player" => "player_soul",
            "guardian" => "guardian",
            "resident" => "resident",
            "radiant_actor" => "radiant_actor",
            "afterlife_actor" => "custom_afterlife_actor",
            _ => throw new ArgumentOutOfRangeException(nameof(targetKind))
        };

    private static JsonObject CreateConflict(
        string targetKind,
        string conflictActorType,
        string actorId,
        string side)
    {
        var target = Participant(conflictActorType, actorId);
        var playerLead = string.Equals(targetKind, "player", StringComparison.Ordinal)
            ? target.DeepClone().AsObject()
            : Participant("player", "player_soul");
        var playerSupporters = new JsonArray();
        var oppositionLead = Participant("guardian", "guardian_other");
        if (string.Equals(side, "player", StringComparison.Ordinal) &&
            !string.Equals(targetKind, "player", StringComparison.Ordinal))
        {
            playerSupporters.Add(target.DeepClone());
        }
        else if (string.Equals(side, "opposition", StringComparison.Ordinal))
        {
            oppositionLead = target.DeepClone().AsObject();
        }

        return new JsonObject
        {
            ["schemaVersion"] = 1,
            ["activeConflict"] = new JsonObject
            {
                ["conflictId"] = "conflict_projector",
                ["realm"] = "chaos_sea",
                ["resolutionState"] = "active",
                ["playerSide"] = new JsonObject
                {
                    ["leadContestant"] = playerLead,
                    ["supporters"] = playerSupporters
                },
                ["oppositionSide"] = new JsonObject
                {
                    ["leadContestant"] = oppositionLead,
                    ["supporters"] = new JsonArray()
                },
                ["playerSideStrain"] = 0,
                ["oppositionSideStrain"] = 0,
                ["combatConditions"] = new JsonArray(),
                ["exchangeLog"] = new JsonArray()
            }
        };
    }

    private static JsonObject Participant(string actorType, string actorId) =>
        new()
        {
            ["actorType"] = actorType,
            ["actorId"] = actorId
        };

    private static JsonObject CreateCanonicalCombatConditionSibling()
    {
        var condition = EffectMaterializationTestFixture.CreateCanonicalEffect(
            profile: "afterlife_combat_condition");
        condition["effectId"] = "effect_condition_sibling";
        condition["realm"] = "chaos_sea";
        condition["target"] = new JsonObject
        {
            ["kind"] = "spiritual_conflict_side",
            ["targetId"] = "conflict_projector:opposition"
        };
        condition["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = "art_condition_sibling",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        condition["components"] = new JsonArray(new JsonObject
        {
            ["componentId"] = "component_condition_sibling",
            ["profile"] = "afterlife_combat_condition",
            ["priority"] = 100,
            ["payload"] = new JsonObject
            {
                ["conditionKind"] = "burden",
                ["targetSide"] = "opposition",
                ["actorId"] = "guardian_projector",
                ["operations"] = new JsonArray("pressure"),
                ["axes"] = new JsonArray("actionCostAudit.opposition"),
                ["counterplay"] = new JsonArray("Ответить действием guard."),
                ["payoff"] = "impose_disadvantage"
            }
        });
        condition["lifetime"] = new JsonObject
        {
            ["mode"] = "uses",
            ["remainingUses"] = 2,
            ["consumingTriggerIds"] = new JsonArray("condition_exchange_consumed"),
            ["displayText"] = "Ещё два обмена"
        };
        condition["triggers"] = new JsonArray(new JsonObject
        {
            ["triggerId"] = "condition_exchange_consumed",
            ["eventType"] = "afterlife_exchange_end",
            ["priority"] = 100,
            ["componentIds"] = new JsonArray("component_condition_sibling"),
            ["consumeUses"] = true,
            ["resolutionMode"] = "deterministic"
        });
        Assert.True(
            AfterlifeSpiritualConflictState.TryProjectCombatCondition(
                condition,
                out var projected,
                out var reason),
            reason);
        foreach (var field in AfterlifeSpiritualConflictState
                     .CombatConditionProjectionFields)
        {
            condition[field] = projected[field]?.DeepClone();
        }
        return condition;
    }

    private static string DefaultMagnitudeJson(string profile) =>
        profile switch
        {
            "spiritual_roll_hindrance" => "\"disadvantage\"",
            "spiritual_action_cost_burden" => "3",
            "spiritual_position_burden" => "2",
            "spiritual_control_burden" => "1",
            "spiritual_strain_burden" => "1",
            "spiritual_tempo_burden" => "\"deny_one_gain\"",
            "spiritual_counter_burden" => "\"reduce_one_step\"",
            "spiritual_art_restriction" => "\"forbid\"",
            _ => throw new ArgumentOutOfRangeException(nameof(profile))
        };

    private static string ExpectedAxis(string sourceAxis, string side) =>
        sourceAxis switch
        {
            "actionCostAudit" => "actionCostAudit." + side,
            "sideStrain" when string.Equals(side, "player", StringComparison.Ordinal) =>
                "playerSideStrain",
            "sideStrain" => "oppositionSideStrain",
            _ => sourceAxis
        };

    private static string DescribeIssues(IReadOnlyList<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(static issue =>
                $"{issue.FilePath}: {issue.Code}: {issue.Message}"));

    private sealed record Fixture(
        EffectMechanicsSnapshot Snapshot,
        EffectSourceAuthority Sources,
        EffectTargetAuthority Targets,
        JsonObject Conflict,
        JsonObject Effect,
        JsonObject Definition);
}
