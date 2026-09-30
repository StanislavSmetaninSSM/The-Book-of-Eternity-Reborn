using System.Collections;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundConsequenceEnvelopeTests
{
    private const string Path = "wound.consequences";
    private const string WoundId = "wound_test_exact";

    private static readonly string[] SpiritualOperationValues =
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "binding",
        "break_binding",
        "force_binding",
        "force_incarnation",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    private static readonly string[] StandardSpiritualArtValues =
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "break_binding",
        "binding",
        "force_binding",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    public static TheoryData<string, int, decimal, decimal> MortalMagnitudeBounds => new()
    {
        { "I", 1, 1m, 5m },
        { "II", 2, 2m, 10m },
        { "III", 3, 3m, 20m },
        { "IV", 4, 4m, 30m }
    };

    public static TheoryData<string, string, int, int, int> SafeScalarCaps => new()
    {
        { "characteristic_modifier", "flat", 100, -1, 1 },
        { "characteristic_modifier", "flat", -100, -1, 1 },
        { "characteristic_modifier", "percent", 100, -5, 5 },
        { "characteristic_modifier", "percent", -100, -5, 5 },
        { "resistance_modifier", "flat", 100, -1, 1 },
        { "resistance_modifier", "flat", -100, -1, 1 },
        { "resistance_modifier", "percent", 100, -5, 5 },
        { "resistance_modifier", "percent", -100, -5, 5 }
    };

    public static TheoryData<string, string, string, string> SpiritualProfiles => new()
    {
        { "spiritual_roll_hindrance", "I", "\"disadvantage\"", "rollMode" },
        { "spiritual_action_cost_burden", "I", "1", "actionCostAudit" },
        { "spiritual_position_burden", "I", "1", "conflictPosition" },
        { "spiritual_control_burden", "II", "1", "controlState" },
        { "spiritual_strain_burden", "III", "1", "sideStrain" },
        { "spiritual_tempo_burden", "I", "\"deny_one_gain\"", "tempoAdvantage" },
        { "spiritual_counter_burden", "I", "\"reduce_one_step\"", "counterPayoff" },
        { "spiritual_art_restriction", "III", "\"restrict\"", "artAvailability" }
    };

    public static TheoryData<string> SpiritualOperations => new()
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "binding",
        "break_binding",
        "force_binding",
        "force_incarnation",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    public static TheoryData<string> SpiritualSafetyOperations => new()
    {
        "spiritual_healing",
        "wound_inspection",
        "communication",
        "request_help",
        "receive_help",
        "withdrawal",
        "surrender",
        "negotiation",
        "dissipation"
    };

    public static TheoryData<string> StandardSpiritualArts => new()
    {
        "pressure",
        "counter",
        "guard",
        "maneuver",
        "break_binding",
        "binding",
        "force_binding",
        "incarnation_resistance",
        "champion_coordination",
        "recover_spiritual_power"
    };

    [Fact]
    public void Registry_IsExactlyVersionOneAndSeparatesTheOnlyMortalZeroSlotMarker()
    {
        Assert.Equal(
            new[]
            {
                "action_control",
                "characteristic_modifier",
                "event_reaction",
                "periodic_damage",
                "periodic_restore",
                "resistance_modifier",
                "roll_modifier"
            },
            WoundConsequenceEnvelopeCatalog.MortalMechanicalProfiles.OrderBy(
                static value => value,
                StringComparer.Ordinal));
        Assert.Equal(
            new[] { "wound_consequence" },
            WoundConsequenceEnvelopeCatalog.MortalZeroSlotProfiles);
        Assert.Equal(
            new[]
            {
                "spiritual_action_cost_burden",
                "spiritual_art_restriction",
                "spiritual_control_burden",
                "spiritual_counter_burden",
                "spiritual_position_burden",
                "spiritual_roll_hindrance",
                "spiritual_strain_burden",
                "spiritual_tempo_burden"
            },
            WoundConsequenceEnvelopeCatalog.SpiritualProfiles.OrderBy(
                static value => value,
                StringComparer.Ordinal));
        Assert.Equal(
            SpiritualOperationValues.OrderBy(
                static value => value,
                StringComparer.Ordinal),
            WoundConsequenceEnvelopeCatalog.SpiritualOperationKeys.OrderBy(
                static value => value,
                StringComparer.Ordinal));
        Assert.Equal(
            StandardSpiritualArtValues.OrderBy(
                static value => value,
                StringComparer.Ordinal),
            WoundConsequenceEnvelopeCatalog.SpiritualArtRestrictionKeys.OrderBy(
                static value => value,
                StringComparer.Ordinal));
    }

    [Fact]
    public void DetachedMortalEnvelope_ValidatesStaticSlotsWithoutMaterializationAuthority()
    {
        const string draftPath =
            "wound.treatment.routes[0].outcomes[3].result[0].complicationDraft.consequenceDefinitions";
        var definitionPath = draftPath + "[0].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 2,
                draftPath,
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        definitionPath,
                        new[]
                        {
                            RawScalarComponent(
                                "component_strength",
                                "characteristic_modifier",
                                "characteristic",
                                "strength",
                                "percent",
                                "100",
                                "{\"minimum\":-10,\"maximum\":10}"),
                            PeriodicComponent("component_bleeding", 999m)
                        })
                }));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.FilePath}: {issue.Code}")));
        Assert.Empty(result.Issues);
        Assert.True(result.HasDeferredPeriodicAuthority);
        Assert.Collection(
            result.Slots.OrderBy(static slot => slot.ProfileKey, StringComparer.Ordinal),
            slot =>
            {
                Assert.Equal("definition_root", slot.EffectRef);
                Assert.Equal("characteristic_modifier", slot.ProfileKey);
                Assert.Equal("characteristic_modifier:characteristic:strength", slot.Coordinate);
                Assert.Equal(
                    definitionPath + ".components[0].payload.characteristic",
                    slot.AuthorPath);
            },
            slot =>
            {
                Assert.Equal("definition_root", slot.EffectRef);
                Assert.Equal("periodic_damage", slot.ProfileKey);
                Assert.Equal("periodic_damage:resource:health", slot.Coordinate);
                Assert.Equal(
                    definitionPath + ".components[1].payload.resource",
                    slot.AuthorPath);
            });
    }

    [Theory]
    [InlineData(2, "attack", "forbid", null, "operation", "wound_consequence_action_forbid_invalid")]
    [InlineData(3, "escape", "forbid", null, "action", "wound_consequence_action_forbid_invalid")]
    [InlineData(2, "attack", "cost_modifier", 3, "modifier", "wound_consequence_magnitude_exceeded")]
    public void DetachedMortalEnvelope_ReusesActionSeverityMagnitudeAndForbidClosure(
        int severityRank,
        string action,
        string operation,
        int? modifier,
        string issueField,
        string issueCode)
    {
        const string definitionPath = "draft.consequenceDefinitions[0].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank,
                "draft.consequenceDefinitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        definitionPath,
                        new[]
                        {
                            ActionComponent(
                                "component_action",
                                action,
                                operation,
                                modifier)
                        })
                }));

        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.FilePath,
                definitionPath + ".components[0].payload." + issueField,
                StringComparison.Ordinal) &&
            string.Equals(issue.Code, issueCode, StringComparison.Ordinal));
    }

    [Fact]
    public void DetachedMortalEnvelope_DerivesOneRollSlotAndRejectsTheSecondAuthoredCoordinate()
    {
        const string firstPath = "draft.definitions[0].definition";
        const string secondPath = "draft.definitions[1].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 4,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_first",
                        firstPath,
                        new[]
                        {
                            RollComponent(
                                "component_first_roll",
                                "attack_roll",
                                "defense_roll")
                        }),
                    new WoundDetachedMortalEffectRef(
                        "definition_second",
                        secondPath,
                        new[]
                        {
                            RollComponent("component_second_roll", "attack_roll")
                        })
                }));

        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.FilePath,
                secondPath + ".components[0].payload.operations[0]",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_duplicate_coordinate",
                StringComparison.Ordinal));
        var slot = Assert.Single(result.Slots);
        Assert.Equal("roll_modifier", slot.ProfileKey);
        Assert.Equal("attack_roll+defense_roll", slot.OperationKey);
        Assert.Equal("roll_modifier:attack_roll+defense_roll:all", slot.Coordinate);
        Assert.Equal(firstPath + ".components[0].payload.operations", slot.AuthorPath);
    }

    [Fact]
    public void SkillScope_BroadMultiOperationComponentConsumesOneSlot()
    {
        const string definitionPath = "draft.definitions[0].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 4,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_roll",
                        definitionPath,
                        new[]
                        {
                            RollComponent(
                                "component_roll",
                                "attack_roll",
                                "defense_roll",
                                "saving_throw")
                        })
                }));

        Assert.Empty(result.Issues);
        var slot = Assert.Single(result.Slots);
        Assert.Equal("component_roll", slot.ComponentId);
        Assert.Equal("roll_modifier", slot.ProfileKey);
        Assert.Equal("attack_roll+defense_roll+saving_throw", slot.OperationKey);
        Assert.Equal(
            "roll_modifier:attack_roll+defense_roll+saving_throw:all",
            slot.Coordinate);
    }

    [Fact]
    public void SkillScope_DifferentFocusedSelectorsUseDistinctMechanicalCoordinates()
    {
        const string definitionPath = "draft.definitions[0].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 2,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_roll",
                        definitionPath,
                        new[]
                        {
                            FocusedRollComponent("component_lockpicking", "skill_lockpicking"),
                            FocusedRollComponent("component_medicine", "skill_medicine")
                        })
                }));

        Assert.Empty(result.Issues);
        Assert.Equal(2, result.Slots.Length);
        Assert.Equal(
            new[]
            {
                "roll_modifier:skill_check:skill:skill_lockpicking",
                "roll_modifier:skill_check:skill:skill_medicine"
            },
            result.Slots.Select(static slot => slot.Coordinate));
    }

    [Fact]
    public void SkillScope_RepeatedFocusedSelectorIsDuplicateCoordinate()
    {
        const string definitionPath = "draft.definitions[0].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 2,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_roll",
                        definitionPath,
                        new[]
                        {
                            FocusedRollComponent("component_first", "skill_lockpicking"),
                            FocusedRollComponent("component_second", "skill_lockpicking")
                        })
                }));

        var issue = Assert.Single(result.Issues, issue => string.Equals(
            issue.Code,
            "wound_consequence_duplicate_coordinate",
            StringComparison.Ordinal));
        Assert.Equal(
            definitionPath + ".components[1].payload.operations[0]",
            issue.FilePath);
        Assert.Equal("roll_modifier:skill_check:skill:skill_lockpicking", issue.Actual);
        Assert.Single(result.Slots);
    }

    [Fact]
    public void DetachedMortalEnvelope_ValidatesDeterministicFlattenedReactionChildren()
    {
        const string rootPath = "draft.definitions[0].definition";
        const string leafPath = "draft.definitions[1].definition";
        var expansion = new WoundDetachedMortalReactionExpansionRef(
            "component_reaction",
            leafPath,
            new[]
            {
                ResistanceComponent(
                    "component_leaf_resistance",
                    "percent",
                    1m)
            },
            Element(new JsonObject
            {
                ["value"] = 21m,
                ["notPresentInPayload"] = "must_not_be_inserted"
            }));
        var boundPayload = expansion.Components.Single().GetProperty("payload");
        Assert.Equal(21m, boundPayload.GetProperty("value").GetDecimal());
        Assert.False(boundPayload.TryGetProperty("notPresentInPayload", out _));

        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 3,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent("component_reaction", "apply_definition", 2)
                        },
                        new[]
                        {
                            new WoundDetachedMortalReactionExpansionRef(
                                "component_reaction",
                                leafPath,
                                new[]
                                {
                                    ResistanceComponent(
                                        "component_leaf_resistance",
                                        "percent",
                                        20m)
                                })
                        })
                }));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.FilePath}: {issue.Code}")));
        Assert.Equal(
            new[] { "event_reaction", "resistance_modifier" },
            result.Slots.Select(static slot => slot.ProfileKey).OrderBy(
                static profile => profile,
                StringComparer.Ordinal));
        Assert.Contains(result.Slots, slot =>
            string.Equals(slot.EffectRef, "definition_root", StringComparison.Ordinal) &&
            string.Equals(
                slot.AuthorPath,
                leafPath + ".components[0].payload.resistance",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(
        "characteristic_modifier",
        "characteristic",
        "strength",
        "flat",
        "100",
        "{\"minimum\":-3,\"maximum\":3}",
        "cap")]
    [InlineData(
        "resistance_modifier",
        "resistance",
        "fire",
        "percent",
        "11",
        "null",
        "value")]
    public void DetachedMortalEnvelope_ReusesScalarEffectiveMagnitudeBounds(
        string profile,
        string targetField,
        string target,
        string operation,
        string valueJson,
        string capJson,
        string issueField)
    {
        const string definitionPath = "draft.definitions[0].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 2,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        definitionPath,
                        new[]
                        {
                            RawScalarComponent(
                                "component_scalar",
                                profile,
                                targetField,
                                target,
                                operation,
                                valueJson,
                                capJson)
                        })
                }));

        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.FilePath,
                definitionPath + ".components[0].payload." + issueField,
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_magnitude_exceeded",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DetachedMortalEnvelope_ReportsReactionAndChildFailuresAtAuthoredPaths()
    {
        const string rootPath = "draft.definitions[0].definition";
        const string leafPath = "draft.definitions[1].definition";
        var light = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 2,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent("component_reaction", "apply_definition", 2)
                        },
                        new[]
                        {
                            new WoundDetachedMortalReactionExpansionRef(
                                "component_reaction",
                                leafPath,
                                Array.Empty<JsonElement>())
                        })
                }));
        Assert.Contains(light.Issues, issue =>
            string.Equals(
                issue.FilePath,
                rootPath + ".components[0].payload.definitionKey",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_reaction_expansion_invalid",
                StringComparison.Ordinal));

        var excessiveChild = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 3,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent("component_reaction", "apply_definition", 2)
                        },
                        new[]
                        {
                            new WoundDetachedMortalReactionExpansionRef(
                                "component_reaction",
                                leafPath,
                                new[]
                                {
                                    ResistanceComponent(
                                        "component_leaf_resistance",
                                        "percent",
                                        21m)
                                })
                        })
                }));
        Assert.Contains(excessiveChild.Issues, issue =>
            string.Equals(
                issue.FilePath,
                leafPath + ".components[0].payload.value",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_magnitude_exceeded",
                StringComparison.Ordinal));

        var unknownResult = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 3,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent(
                                "component_reaction",
                                "unregistered_result",
                                1)
                        })
                }));
        Assert.Contains(unknownResult.Issues, issue =>
            string.Equals(
                issue.FilePath,
                rootPath + ".components[0].payload.resultKind",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_reaction_result_invalid",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DetachedMortalEnvelope_BindsReactionEdgeParametersBeforeChildValidation()
    {
        const string rootPath = "draft.definitions[0].definition";
        const string leafPath = "draft.definitions[1].definition";
        var expansion = new WoundDetachedMortalReactionExpansionRef(
            "component_reaction",
            leafPath,
            new[]
            {
                ResistanceComponent(
                    "component_leaf_resistance",
                    "percent",
                    1m)
            },
            Element(new JsonObject { ["value"] = 21m }));
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 3,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent("component_reaction", "apply_definition", 2)
                        },
                        new[]
                        {
                            expansion
                        })
                }));

        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.FilePath,
                leafPath + ".components[0].payload.value",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_magnitude_exceeded",
                StringComparison.Ordinal) &&
            string.Equals(issue.Actual, "21", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("periodic_amount", "amount")]
    [InlineData("action", "action")]
    public void DetachedMortalEnvelope_RevalidatesMaterializedReactionChildShape(
        string mutation,
        string expectedField)
    {
        const string rootPath = "draft.definitions[0].definition";
        const string leafPath = "draft.definitions[1].definition";
        JsonElement leafComponent;
        JsonElement parameters;
        switch (mutation)
        {
            case "periodic_amount":
                leafComponent = PeriodicComponent("component_leaf_periodic", 1m);
                parameters = Element(new JsonObject { ["amount"] = 0m });
                break;
            case "action":
                leafComponent = ActionComponent(
                    "component_leaf_action",
                    "movement",
                    "restrict");
                parameters = Element(new JsonObject { ["action"] = "unregistered_action" });
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 3,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent("component_reaction", "apply_definition", 2)
                        },
                        new[]
                        {
                            new WoundDetachedMortalReactionExpansionRef(
                                "component_reaction",
                                leafPath,
                                new[] { leafComponent },
                                parameters)
                        })
                }));

        Assert.Contains(result.Issues, issue =>
            string.Equals(
                issue.FilePath,
                $"{leafPath}.components[0].payload.{expectedField}",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "effect_materialization_invalid_component",
                StringComparison.Ordinal));
    }

    [Fact]
    public void DetachedMortalEnvelope_ZeroChildExpansionChargesOnlyRootBoundReaction()
    {
        const string rootPath = "draft.definitions[0].definition";
        const string rootedTargetPath = "draft.definitions[1].definition";
        var result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
            new WoundDetachedMortalEnvelopeRequest(
                severityRank: 3,
                "draft.definitions",
                new[]
                {
                    new WoundDetachedMortalEffectRef(
                        "definition_root",
                        rootPath,
                        new[]
                        {
                            ReactionComponent("component_reaction", "apply_definition", 2)
                        },
                        new[]
                        {
                            new WoundDetachedMortalReactionExpansionRef(
                                "component_reaction",
                                rootedTargetPath,
                                Array.Empty<JsonElement>())
                        })
                }));

        Assert.True(
            result.IsValid,
            string.Join(
                Environment.NewLine,
                result.Issues.Select(issue => $"{issue.FilePath}: {issue.Code}")));
        var slot = Assert.Single(result.Slots);
        Assert.Equal("event_reaction", slot.ProfileKey);
        Assert.Equal(rootPath + ".components[0].payload.resultKind", slot.AuthorPath);
    }

    [Fact]
    public void DetachedMortalEnvelope_MalformedReactionMaximumFailsClosedWithoutThrowing()
    {
        const string rootPath = "draft.definitions[0].definition";
        var reaction = JsonNode.Parse(
            ReactionComponent("component_reaction", "apply_definition", 2).GetRawText())!
            .AsObject();
        reaction["payload"]!["maxExpansion"] = "2";
        WoundDetachedMortalEnvelopeValidationResult? result = null;

        var exception = Record.Exception(() =>
            result = WoundConsequenceEnvelopeCatalog.ValidateDetachedMortal(
                new WoundDetachedMortalEnvelopeRequest(
                    severityRank: 3,
                    "draft.definitions",
                    new[]
                    {
                        new WoundDetachedMortalEffectRef(
                            "definition_root",
                            rootPath,
                            new[] { Element(reaction) },
                            new[]
                            {
                                new WoundDetachedMortalReactionExpansionRef(
                                    "component_reaction",
                                    "draft.definitions[1].definition",
                                    Array.Empty<JsonElement>())
                            })
                    })));

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.Contains(result!.Issues, issue =>
            string.Equals(
                issue.FilePath,
                rootPath + ".components[0].payload.maxExpansion",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "effect_materialization_invalid_component",
                StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    public void MortalSeverity_DerivesRankAsMaximumSlotCount(
        string severity,
        int rank)
    {
        var operations = new[]
        {
            "attack_roll", "defense_roll", "skill_check", "saving_throw"
        }.Take(rank).ToArray();
        var effect = Effect(
            "effect_roll",
            operations.Select((operation, index) => RollComponent(
                "component_roll_" + index,
                operation)).ToArray());
        var request = MortalRequest(
            severity,
            new[] { effect },
            Entries(rank, "effect_roll", "roll_modifier"));

        var result = Validate(request);

        AssertValid(result);
        Assert.Equal(rank, result.Envelope!.SlotsUsed);
        Assert.Equal(operations, result.Envelope.Slots.Select(static slot => slot.OperationKey));
        Assert.Equal(Enumerable.Range(1, rank), result.Envelope.Slots.Select(static slot => slot.Slot));
    }

    [Fact]
    public void MortalSeverity_RejectsMoreMechanicalSlotsThanRankEvenWhenDeclaredBudgetLies()
    {
        var effect = Effect(
            "effect_roll",
            RollComponent("component_attack", "attack_roll"),
            RollComponent("component_defense", "defense_roll"));
        var request = MortalRequest(
            "I",
            new[] { effect },
            Entries(2, "effect_roll", "roll_modifier"),
            declaredSlotBudget: 4,
            declaredSlotsUsed: 2);

        var result = Validate(request);

        AssertIssue(
            result,
            Path + ".derivedSlots",
            "wound_consequence_slot_budget_exceeded");
        Assert.Null(result.Envelope);
    }

    [Fact]
    public void MortalZeroSlots_RequiresProvenLifecycleImpactAndDisplayMarkerDoesNotQualify()
    {
        var marker = Effect(
            "effect_marker",
            MarkerComponent("component_marker"));
        var entries = Array.Empty<WoundConsequenceEntry>();

        AssertIssue(
            Validate(MortalRequest("I", new[] { marker }, entries)),
            Path + ".lifecycleEvidence",
            "wound_consequence_non_display_impact_required");

        AssertValid(Validate(MortalRequest(
            "I",
            new[] { marker },
            entries,
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                ActiveComplicationChangesLifecycle: true,
                CareConstraintChangesLifecycle: false,
                RecoveryConstraintChangesLifecycle: false))));

        AssertValid(Validate(MortalRequest(
            "I",
            Array.Empty<WoundConsequenceEffectProposal>(),
            entries,
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                ActiveComplicationChangesLifecycle: false,
                CareConstraintChangesLifecycle: true,
                RecoveryConstraintChangesLifecycle: false))));

        AssertValid(Validate(MortalRequest(
            "I",
            Array.Empty<WoundConsequenceEffectProposal>(),
            entries,
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                ActiveComplicationChangesLifecycle: false,
                CareConstraintChangesLifecycle: false,
                RecoveryConstraintChangesLifecycle: true))));
    }

    [Fact]
    public void MortalZeroSlotMarker_IsLimitedOnceWithinOneOwnedEffect()
    {
        var lifecycle = new WoundConsequenceLifecycleEvidence(true, false, false);
        var withinOneEffect = Validate(MortalRequest(
            "I",
            new[]
            {
                Effect(
                    "effect_markers",
                    MarkerComponent("marker_a"),
                    MarkerComponent("marker_b"))
            },
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: lifecycle,
            declaredSlotsUsed: 0));
        AssertIssue(
            withinOneEffect,
            Path + ".effects",
            "wound_consequence_marker_limit_exceeded");
    }

    [Fact]
    public void MortalZeroSlotMarker_IsLimitedOnceAcrossOwnedEffects()
    {
        var lifecycle = new WoundConsequenceLifecycleEvidence(true, false, false);
        var acrossEffects = Validate(MortalRequest(
            "I",
            new[]
            {
                Effect("effect_marker_a", MarkerComponent("marker_a")),
                Effect("effect_marker_b", MarkerComponent("marker_b"))
            },
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: lifecycle,
            declaredSlotsUsed: 0));
        AssertIssue(
            acrossEffects,
            Path + ".effects",
            "wound_consequence_marker_limit_exceeded");
    }

    [Fact]
    public void MortalZeroSlotMarker_IsLimitedOnceAcrossDirectAndFlattenedComponents()
    {
        var directAndFlattened = Effect(
            "effect_direct_and_flattened_marker",
            new[]
            {
                MarkerComponent("marker_direct"),
                ReactionComponent("reaction", "apply_definition", 2)
            },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "reaction",
                    new[] { MarkerComponent("marker_flattened") })
            });
        var directAndFlattenedResult = Validate(MortalRequest(
            "III",
            new[] { directAndFlattened },
            Entries(1, "effect_direct_and_flattened_marker", "event_reaction")));
        AssertIssue(
            directAndFlattenedResult,
            Path + ".effects",
            "wound_consequence_marker_limit_exceeded");
    }

    [Fact]
    public void MortalSlotDerivation_CountsEveryIndependentComponentAndOneMultiOperationRollComponent()
    {
        var effect = Effect(
            "effect_mixed",
            CharacteristicComponent("component_characteristic", "flat", -4m),
            ResistanceComponent("component_resistance", "percent", -30m),
            RollComponent("component_roll", "attack_roll", "saving_throw"));
        var request = MortalRequest(
            "IV",
            new[] { effect },
            Entries(
                (1, "effect_mixed", "characteristic_modifier"),
                (2, "effect_mixed", "resistance_modifier"),
                (3, "effect_mixed", "roll_modifier")));

        var result = Validate(request);

        AssertValid(result);
        Assert.Equal(
            new[] { "strength", "fire", "attack_roll+saving_throw" },
            result.Envelope!.Slots.Select(static slot => slot.OperationKey));
        Assert.Equal(
            new[] { "characteristic", "resistance", "rollMode" },
            result.Envelope.Slots.Select(static slot => slot.Axis));
        Assert.Equal(3, result.Envelope.Slots.Select(static slot => slot.Coordinate).Distinct().Count());
    }

    [Fact]
    public void MortalDuplicateMechanicalCoordinate_IsNotASecondIndependentSlot()
    {
        var effect = Effect(
            "effect_duplicate",
            RollComponent("component_a", "attack_roll"),
            RollComponent("component_b", "attack_roll"));

        AssertIssue(
            Validate(MortalRequest(
                "II",
                new[] { effect },
                Entries(2, "effect_duplicate", "roll_modifier"))),
            Path + ".effects[0].components[1].payload.operations[0]",
            "wound_consequence_duplicate_coordinate");
    }

    [Fact]
    public void DerivedSlots_RejectHiddenPackingAndEveryDeclaredSlotsUsedOrEntryMismatch()
    {
        var packed = Effect(
            "effect_roll",
            RollComponent("component_attack", "attack_roll"),
            RollComponent("component_defense", "defense_roll"));

        var hidden = MortalRequest(
            "II",
            new[] { packed },
            Entries(1, "effect_roll", "roll_modifier"));
        AssertIssue(
            Validate(hidden),
            Path + ".declared.entries",
            "wound_consequence_declared_slots_mismatch");

        var wrongCount = MortalRequest(
            "II",
            new[] { packed },
            Entries(2, "effect_roll", "roll_modifier"),
            declaredSlotsUsed: 1);
        AssertIssue(
            Validate(wrongCount),
            Path + ".declared.slotsUsed",
            "wound_consequence_declared_slots_mismatch");

        var wrongProfile = MortalRequest(
            "II",
            new[] { packed },
            Entries(
                (1, "effect_roll", "roll_modifier"),
                (2, "effect_roll", "action_control")));
        AssertIssue(
            Validate(wrongProfile),
            Path + ".declared.entries[1]",
            "wound_consequence_declared_slots_mismatch");

        var nonContiguous = MortalRequest(
            "II",
            new[] { packed },
            new[]
            {
                Entry(1, "effect_roll", "roll_modifier"),
                Entry(3, "effect_roll", "roll_modifier")
            });
        AssertIssue(
            Validate(nonContiguous),
            Path + ".declared.entries[1].slot",
            "wound_consequence_declared_slots_mismatch");
    }

    [Theory]
    [MemberData(nameof(MortalMagnitudeBounds))]
    public void MortalMagnitudeBounds_AreExactForFlatPercentAndActionCost(
        string severity,
        int rank,
        decimal flatLimit,
        decimal percentLimit)
    {
        AssertValid(Validate(MortalSingle(
            severity,
            CharacteristicComponent("component", "flat", -flatLimit),
            "characteristic_modifier")));
        AssertValid(Validate(MortalSingle(
            severity,
            ResistanceComponent("component", "percent", percentLimit),
            "resistance_modifier")));
        AssertValid(Validate(MortalSingle(
            severity,
            ActionComponent("component", "movement", "cost_modifier", -rank),
            "action_control")));

        AssertIssue(
            Validate(MortalSingle(
                severity,
                CharacteristicComponent("component", "flat", -(flatLimit + 0.01m)),
                "characteristic_modifier")),
            Path + ".effects[0].components[0].payload.value",
            "wound_consequence_magnitude_exceeded");
        AssertIssue(
            Validate(MortalSingle(
                severity,
                ResistanceComponent("component", "percent", percentLimit + 0.01m),
                "resistance_modifier")),
            Path + ".effects[0].components[0].payload.value",
            "wound_consequence_magnitude_exceeded");
        AssertIssue(
            Validate(MortalSingle(
                severity,
                ActionComponent("component", "movement", "cost_modifier", rank + 0.01m),
                "action_control")),
            Path + ".effects[0].components[0].payload.modifier",
            "wound_consequence_magnitude_exceeded");
    }

    [Fact]
    public void ScalarModifierCaps_CannotAmplifyAValueBeyondTheSeverityEnvelope()
    {
        var characteristic = JsonNode.Parse(
            CharacteristicComponent("component_characteristic", "flat", -1m)
                .GetRawText())!.AsObject();
        characteristic["payload"]!["cap"] = new JsonObject
        {
            ["minimum"] = 100m,
            ["maximum"] = 100m
        };
        AssertIssue(
            Validate(MortalSingle(
                "I",
                Element(characteristic),
                "characteristic_modifier")),
            Path + ".effects[0].components[0].payload.cap",
            "wound_consequence_magnitude_exceeded");

        var resistance = JsonNode.Parse(
            ResistanceComponent("component_resistance", "percent", -5m)
                .GetRawText())!.AsObject();
        resistance["payload"]!["cap"] = new JsonObject
        {
            ["minimum"] = -100m,
            ["maximum"] = -100m
        };
        AssertIssue(
            Validate(MortalSingle(
                "I",
                Element(resistance),
                "resistance_modifier")),
            Path + ".effects[0].components[0].payload.cap",
            "wound_consequence_magnitude_exceeded");
    }

    [Theory]
    [InlineData("minimum")]
    [InlineData("maximum")]
    public void ScalarModifierCaps_RequireExactDecimalEndpoints(string invalidEndpoint)
    {
        var minimum = string.Equals(invalidEndpoint, "minimum", StringComparison.Ordinal)
            ? "1e50"
            : "-1";
        const string maximum = "1e50";
        var component = RawElement($$"""
            {
              "componentId": "component_characteristic",
              "profile": "characteristic_modifier",
              "priority": 0,
              "payload": {
                "characteristic": "strength",
                "operation": "flat",
                "value": -1,
                "cap": { "minimum": {{minimum}}, "maximum": {{maximum}} }
              }
            }
            """);

        AssertIssue(
            Validate(MortalSingle("I", component, "characteristic_modifier")),
            Path + $".effects[0].components[0].payload.cap.{invalidEndpoint}",
            "wound_consequence_magnitude_exceeded");
    }

    [Fact]
    public void ScalarModifierCaps_CannotTurnAMechanicalSlotIntoRuntimeZero()
    {
        var component = JsonNode.Parse(
            CharacteristicComponent("component_characteristic", "flat", -1m)
                .GetRawText())!.AsObject();
        component["payload"]!["cap"] = new JsonObject
        {
            ["minimum"] = 0m,
            ["maximum"] = 0m
        };

        AssertIssue(
            Validate(MortalSingle(
                "I",
                Element(component),
                "characteristic_modifier")),
            Path + ".effects[0].components[0].payload.cap",
            "wound_consequence_magnitude_exceeded");
    }

    [Theory]
    [InlineData("characteristic_modifier", "characteristic", "strength", false)]
    [InlineData("characteristic_modifier", "characteristic", "strength", true)]
    [InlineData("resistance_modifier", "resistance", "fire", false)]
    [InlineData("resistance_modifier", "resistance", "fire", true)]
    public void ScalarModifiers_RejectGenericValidNonDecimalRawBeforeSlotAccounting(
        string profile,
        string targetField,
        string target,
        bool capped)
    {
        var capJson = capped
            ? "{ \"minimum\": 1, \"maximum\": 1 }"
            : "null";
        var component = RawScalarComponent(
            "component_non_decimal",
            profile,
            targetField,
            target,
            "flat",
            "1e-29",
            capJson);
        var result = Validate(MortalRequest(
            "I",
            new[] { Effect("effect_non_decimal", component) },
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                true,
                false,
                false),
            declaredSlotsUsed: 0));

        AssertIssue(
            result,
            Path + ".effects[0].components[0].payload.value",
            "wound_consequence_magnitude_exceeded");
        Assert.Null(result.Envelope);
    }

    [Theory]
    [MemberData(nameof(SafeScalarCaps))]
    public void ScalarModifierCaps_BudgetTheSafeRuntimeValueAfterMinimumThenMaximum(
        string profile,
        string operation,
        int rawValue,
        int minimum,
        int maximum)
    {
        var component = JsonNode.Parse(
            (string.Equals(profile, "characteristic_modifier", StringComparison.Ordinal)
                ? CharacteristicComponent("component_safe_cap", operation, rawValue)
                : ResistanceComponent("component_safe_cap", operation, rawValue))
            .GetRawText())!.AsObject();
        component["payload"]!["cap"] = new JsonObject
        {
            ["minimum"] = minimum,
            ["maximum"] = maximum
        };

        var result = Validate(MortalSingle("I", Element(component), profile));

        AssertValid(result);
        var slot = Assert.Single(result.Envelope!.Slots);
        Assert.Equal(profile, slot.ProfileKey);
    }

    [Fact]
    public void ScalarModifierCaps_RetainGenericMinimumMaximumOrderingValidation()
    {
        var component = JsonNode.Parse(
            CharacteristicComponent("component_reversed_cap", "flat", -1m)
                .GetRawText())!.AsObject();
        component["payload"]!["cap"] = new JsonObject
        {
            ["minimum"] = 1,
            ["maximum"] = -1
        };

        AssertIssue(
            Validate(MortalSingle(
                "I",
                Element(component),
                "characteristic_modifier")),
            Path + ".effects[0].components[0].payload.cap",
            "effect_materialization_invalid_component");
    }

    [Theory]
    [InlineData("I", 5)]
    [InlineData("II", 10)]
    [InlineData("III", 20)]
    [InlineData("IV", 30)]
    public void PeriodicAmount_UsesExactMaximumPercentageAndAcceptedQuantumWithoutUpwardRounding(
        string severity,
        int percent)
    {
        var maximum = 100m;
        var acceptedAmount = maximum * percent / 100m;
        var cadence = new[]
        {
            new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1)
        };
        var effect = Effect(
            "effect_periodic",
            new[] { PeriodicComponent("component_periodic", acceptedAmount) },
            cadences: cadence);

        AssertValid(Validate(MortalRequest(
            severity,
            new[] { effect },
            Entries(1, "effect_periodic", "periodic_damage"),
            resourceBounds: new[] { new WoundResourceEnvelopeBound("health", maximum, 0.25m) })));

        var over = Effect(
            "effect_periodic",
            new[] { PeriodicComponent("component_periodic", acceptedAmount + 0.25m) },
            cadences: cadence);
        AssertIssue(
            Validate(MortalRequest(
                severity,
                new[] { over },
                Entries(1, "effect_periodic", "periodic_damage"),
                resourceBounds: new[] { new WoundResourceEnvelopeBound("health", maximum, 0.25m) })),
            Path + ".effects[0].components[0].payload.amount",
            "wound_consequence_periodic_amount_invalid");
    }

    [Fact]
    public void PeriodicAmount_RejectsMissingBoundNonPositiveMisalignedAndRoundedAboveCap()
    {
        var cadence = new[]
        {
            new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1)
        };

        AssertIssue(
            Validate(MortalPeriodic(4m, cadence, Array.Empty<WoundResourceEnvelopeBound>())),
            Path + ".effects[0].components[0].payload.resource",
            "wound_consequence_resource_bound_missing");
        AssertIssue(
            Validate(MortalPeriodic(0m, cadence)),
            Path + ".effects[0].components[0].payload.amount",
            "wound_consequence_periodic_amount_invalid");
        AssertIssue(
            Validate(MortalPeriodic(4.5m, cadence)),
            Path + ".effects[0].components[0].payload.amount",
            "wound_consequence_periodic_amount_invalid");

        var roundedAbove = MortalPeriodic(
            5m,
            cadence,
            new[] { new WoundResourceEnvelopeBound("health", 95m, 1m) });
        AssertIssue(
            Validate(roundedAbove),
            Path + ".effects[0].components[0].payload.amount",
            "wound_consequence_periodic_amount_invalid");

        AssertValid(Validate(MortalPeriodic(
            4m,
            cadence,
            new[] { new WoundResourceEnvelopeBound("health", 95m, 1m) })));
    }

    [Fact]
    public void PeriodicCadence_IsAtMostOnceForEachAcceptedSourceEvent()
    {
        AssertIssue(
            Validate(MortalPeriodic(4m, Array.Empty<WoundPeriodicCadenceEvidence>())),
            Path + ".effects[0].components[0]",
            "wound_consequence_periodic_cadence_invalid");
        AssertIssue(
            Validate(MortalPeriodic(
                4m,
                new[]
                {
                    new WoundPeriodicCadenceEvidence(
                        "component_periodic",
                        "owner_turn_end",
                        2)
                })),
            Path + ".effects[0].cadences[0].maximumExecutionsPerSourceEvent",
            "wound_consequence_periodic_cadence_invalid");
        AssertIssue(
            Validate(MortalPeriodic(
                4m,
                new[]
                {
                    new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1),
                    new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1)
                })),
            Path + ".effects[0].cadences[1]",
            "wound_consequence_periodic_cadence_invalid");
        AssertIssue(
            Validate(MortalPeriodic(
                4m,
                new[]
                {
                    new WoundPeriodicCadenceEvidence(
                        "component_periodic",
                        "unregistered_event",
                        1)
                })),
            Path + ".effects[0].cadences[0].sourceEvent",
            "wound_consequence_periodic_cadence_invalid");

        AssertValid(Validate(MortalPeriodic(
            4m,
            new[]
            {
                new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1),
                new WoundPeriodicCadenceEvidence("component_periodic", "scene_ended", 1)
            })));
    }

    [Fact]
    public void EveryCadenceEntry_MustBelongToExactlyOnePeriodicComponent()
    {
        var nonPeriodic = Effect(
            "effect_characteristic",
            new[] { CharacteristicComponent("component_characteristic", "flat", -1m) },
            cadences: new[]
            {
                new WoundPeriodicCadenceEvidence(
                    "component_characteristic",
                    "owner_turn_end",
                    1)
            });
        AssertIssue(
            Validate(MortalRequest(
                "I",
                new[] { nonPeriodic },
                Entries(1, "effect_characteristic", "characteristic_modifier"))),
            Path + ".effects[0].cadences[0]",
            "wound_consequence_periodic_cadence_invalid");

        var missingComponent = Effect(
            "effect_periodic",
            new[] { PeriodicComponent("component_periodic", 4m) },
            cadences: new[]
            {
                new WoundPeriodicCadenceEvidence(
                    "component_missing",
                    "unregistered_event",
                    1)
            });
        AssertIssue(
            Validate(MortalRequest(
                "I",
                new[] { missingComponent },
                Entries(1, "effect_periodic", "periodic_damage"),
                resourceBounds: new[]
                {
                    new WoundResourceEnvelopeBound("health", 100m, 1m)
                })),
            Path + ".effects[0].cadences[0]",
            "wound_consequence_periodic_cadence_invalid");
    }

    [Fact]
    public void PeriodicRestore_UsesTheSameExactResourceEnvelopeAndOneSlot()
    {
        var component = Component(
            "component_restore",
            "periodic_restore",
            new JsonObject
            {
                ["resource"] = "health",
                ["amount"] = 10m,
                ["capPolicy"] = "cannot_exceed_maximum"
            });
        var effect = Effect(
            "effect_restore",
            new[] { component },
            cadences: new[]
            {
                new WoundPeriodicCadenceEvidence("component_restore", "owner_turn_end", 1)
            });

        AssertValid(Validate(MortalRequest(
            "II",
            new[] { effect },
            Entries(1, "effect_restore", "periodic_restore"),
            resourceBounds: new[] { new WoundResourceEnvelopeBound("health", 100m, 0.5m) })));
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(100, 0)]
    [InlineData(100.5, 1)]
    public void ResourceEnvelopeContext_MustContainPositiveAlignedExactBounds(
        double maximum,
        double quantum)
    {
        var request = MortalPeriodic(
            4m,
            new[]
            {
                new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1)
            },
            new[]
            {
                new WoundResourceEnvelopeBound(
                    "health",
                    Convert.ToDecimal(maximum),
                    Convert.ToDecimal(quantum))
            });

        AssertIssue(
            Validate(request),
            Path + ".resourceBounds[0]",
            "wound_consequence_resource_bound_invalid");
    }

    [Theory]
    [InlineData("I")]
    [InlineData("II")]
    public void MortalForbid_IsUnavailableAtLightSeverities(string severity)
    {
        AssertIssue(
            Validate(MortalSingle(
                severity,
                ActionComponent("component", "attack", "forbid"),
                "action_control")),
            Path + ".effects[0].components[0].payload.operation",
            "wound_consequence_action_forbid_invalid");
    }

    [Theory]
    [InlineData("attack")]
    [InlineData("cast")]
    [InlineData("movement")]
    public void MortalForbid_AtHeavySeverityTargetsOnlyExactNonSafetyActions(string action)
    {
        AssertValid(Validate(MortalSingle(
            "III",
            ActionComponent("component", action, "forbid"),
            "action_control")));
    }

    [Theory]
    [InlineData("defend")]
    [InlineData("use_item")]
    [InlineData("interact")]
    [InlineData("escape")]
    public void MortalForbid_NeverTargetsSafetyCapableActions(string action)
    {
        AssertIssue(
            Validate(MortalSingle(
                "IV",
                ActionComponent("component", action, "forbid"),
                "action_control")),
            Path + ".effects[0].components[0].payload.action",
            "wound_consequence_action_forbid_invalid");
    }

    [Fact]
    public void AggregateMortalRestrictions_StillPreserveInspectCommunicationHelpTreatmentAndExit()
    {
        var effect = Effect(
            "effect_control",
            ActionComponent("component_attack", "attack", "forbid"),
            ActionComponent("component_cast", "cast", "forbid"),
            ActionComponent("component_movement", "movement", "forbid"));
        var request = MortalRequest(
            "IV",
            new[] { effect },
            Entries(3, "effect_control", "action_control"));

        var result = Validate(request);

        AssertValid(result);
        Assert.True(result.Envelope!.PreservesSafeExit);
        Assert.Equal(
            new[] { "communication", "exit", "help", "inspection", "treatment" },
            result.Envelope.PreservedSafetyOperations.OrderBy(
                static operation => operation,
                StringComparer.Ordinal));
    }

    [Fact]
    public void MortalGrantAndRestrict_EachConsumeOneDeclaredActionSlot()
    {
        var effect = Effect(
            "effect_action",
            ActionComponent("component_grant", "attack", "grant"),
            ActionComponent("component_restrict", "cast", "restrict"));
        var result = Validate(MortalRequest(
            "II",
            new[] { effect },
            Entries(2, "effect_action", "action_control")));

        AssertValid(result);
        Assert.Equal(new[] { "attack", "cast" }, result.Envelope!.Slots.Select(static slot => slot.OperationKey));
    }

    [Theory]
    [InlineData("I")]
    [InlineData("II")]
    public void ReactionDefinitionExpansion_IsUnavailableAtSeverityOneAndTwo(string severity)
    {
        var reaction = ReactionComponent("component_reaction", "apply_definition", 2);
        var effect = Effect(
            "effect_reaction",
            new[] { reaction },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { CharacteristicComponent("component_expanded", "flat", -1m) })
            });

        AssertIssue(
            Validate(MortalRequest(
                severity,
                new[] { effect },
                Entries(2, "effect_reaction", "event_reaction"))),
            Path + ".effects[0].components[0].payload.definitionKey",
            "wound_consequence_reaction_expansion_invalid");
    }

    [Fact]
    public void ReactionExpansion_ChargesTheReactionAndEveryWorstCaseExpandedComponent()
    {
        var reaction = ReactionComponent("component_reaction", "apply_definition", 2);
        var expandedRoll = RollComponent(
            "component_expanded_roll",
            "attack_roll",
            "defense_roll");
        var effect = Effect(
            "effect_reaction",
            new[] { reaction },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { expandedRoll })
            });
        var request = MortalRequest(
            "III",
            new[] { effect },
            Entries(
                (1, "effect_reaction", "event_reaction"),
                (2, "effect_reaction", "roll_modifier")));

        var result = Validate(request);

        AssertValid(result);
        Assert.Equal(2, result.Envelope!.SlotsUsed);
        Assert.Equal(
            new[] { "owner_damaged:apply_definition", "attack_roll+defense_roll" },
            result.Envelope.Slots.Select(static slot => slot.OperationKey));
    }

    [Theory]
    [InlineData(1, false)]
    [InlineData(2, true)]
    [InlineData(3, false)]
    public void OwnedApplyDefinition_RequiresExactExecutionMaximumTwo(
        int maxExpansion,
        bool expectedValid)
    {
        var effect = Effect(
            "effect_reaction",
            new[]
            {
                ReactionComponent(
                    "component_reaction",
                    "apply_definition",
                    maxExpansion)
            },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { MarkerComponent("component_marker") })
            });

        var result = Validate(MortalRequest(
            "III",
            new[] { effect },
            Entries(1, "effect_reaction", "event_reaction")));

        if (expectedValid)
            AssertValid(result);
        else
            AssertIssue(
                result,
                Path + ".effects[0].components[0].payload.maxExpansion",
                "wound_consequence_reaction_expansion_invalid");
    }

    [Theory]
    [InlineData("2.0")]
    [InlineData("2e0")]
    public void OwnedApplyDefinition_RejectsNonCanonicalNumericLexeme(
        string maxExpansionJson)
    {
        var reaction = RawElement($$"""
            {
              "componentId": "component_reaction",
              "profile": "event_reaction",
              "priority": 0,
              "payload": {
                "eventType": "owner_damaged",
                "resultKind": "apply_definition",
                "dependency": "after_current_event",
                "definitionKey": "wound_reaction_definition",
                "parameters": {},
                "maxExpansion": {{maxExpansionJson}}
              }
            }
            """);
        var effect = Effect(
            "effect_reaction",
            new[] { reaction },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { MarkerComponent("component_marker") })
            });

        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { effect },
                Entries(1, "effect_reaction", "event_reaction"))),
            Path + ".effects[0].components[0].payload.maxExpansion",
            "effect_materialization_invalid_component");
    }

    [Fact]
    public void ReactionExpansion_MustBePresentUniqueFullyFlattenedAndOneRelease()
    {
        var missing = Effect(
            "effect_reaction",
            ReactionComponent("component_reaction", "apply_definition", 2));
        AssertIssue(
            Validate(MortalSingle("III", missing, "event_reaction")),
            Path + ".effects[0].components[0].payload.definitionKey",
            "wound_consequence_reaction_expansion_invalid");

        var nested = Effect(
            "effect_reaction",
            new[] { ReactionComponent("component_reaction", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { ReactionComponent("nested_reaction", "remove", 1) })
            });
        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { nested },
                Entries(2, "effect_reaction", "event_reaction"))),
            Path + ".effects[0].expansions[0].components[0].profile",
            "wound_consequence_reaction_expansion_invalid");

        var repeatedRelease = Effect(
            "effect_reaction",
            new[] { ReactionComponent("component_reaction", "apply_definition", 3) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[]
                    {
                        CharacteristicComponent("component_expanded", "flat", -1m)
                    })
            });
        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { repeatedRelease },
                Entries(
                    (1, "effect_reaction", "event_reaction"),
                    (2, "effect_reaction", "characteristic_modifier")))),
            Path + ".effects[0].components[0].payload.maxExpansion",
            "wound_consequence_reaction_expansion_invalid");

        var duplicateExpansion = Effect(
            "effect_reaction",
            new[] { ReactionComponent("component_reaction", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { MarkerComponent("marker_a") }),
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[] { MarkerComponent("marker_b") })
            });
        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { duplicateExpansion },
                Entries(1, "effect_reaction", "event_reaction"))),
            Path + ".effects",
            "wound_consequence_limit_exceeded");

        var twoReactions = Effect(
            "effect_reaction",
            new[]
            {
                ReactionComponent("reaction_a", "apply_definition", 2),
                ReactionComponent("reaction_b", "apply_definition", 2)
            },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "reaction_a",
                    new[] { MarkerComponent("marker_a") }),
                new WoundReactionExpansionProposal(
                    "reaction_b",
                    new[] { MarkerComponent("marker_b") })
            });
        AssertIssue(
            Validate(MortalRequest(
                "IV",
                new[] { twoReactions },
                Entries(2, "effect_reaction", "event_reaction"))),
            Path + ".effects",
            "wound_consequence_limit_exceeded");
    }

    [Fact]
    public void ReactionExpansion_TotalAcrossTheWound_IsBoundedToOne()
    {
        var first = Effect(
            "effect_reaction_a",
            new[] { ReactionComponent("reaction_a", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "reaction_a",
                    new[] { CharacteristicComponent("expanded_a", "flat", -1m) })
            });
        var second = Effect(
            "effect_reaction_b",
            new[] { ReactionComponent("reaction_b", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "reaction_b",
                    new[] { ResistanceComponent("expanded_b", "flat", -1m) })
            });

        foreach (var effects in new[]
                 {
                     new[] { first, second },
                     new[] { second, first }
                 })
        {
            AssertIssue(
                Validate(MortalRequest(
                    "IV",
                    effects,
                    Entries(
                        (1, "effect_reaction_a", "event_reaction"),
                        (2, "effect_reaction_a", "characteristic_modifier"),
                        (3, "effect_reaction_b", "event_reaction"),
                        (4, "effect_reaction_b", "resistance_modifier")))),
                Path + ".effects",
                "wound_consequence_limit_exceeded");
        }
    }

    [Fact]
    public void IndependentReactionExpansions_ArePreservedOutsideTheOwnedCeiling()
    {
        var owned = Effect(
            "effect_owned_reaction",
            new[] { ReactionComponent("owned_reaction", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "owned_reaction",
                    new[] { CharacteristicComponent("owned_expanded", "flat", -1m) })
            });
        var independent = Effect(
            "effect_independent_reaction",
            new[] { UnknownComponent("independent_component") },
            "curse",
            "curse_reaction",
            null,
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "independent_reaction",
                    new[] { UnknownComponent("independent_expanded") })
            });

        var result = Validate(MortalRequest(
            "III",
            new[] { independent, owned },
            Entries(
                (1, "effect_owned_reaction", "event_reaction"),
                (2, "effect_owned_reaction", "characteristic_modifier"))));

        AssertValid(result);
        Assert.Equal(2, result.Envelope!.SlotsUsed);
        var preserved = Assert.Single(result.IndependentEffects);
        Assert.Equal(independent.Components[0].GetRawText(), preserved.Components[0].GetRawText());
        Assert.Equal(
            independent.Expansions[0].ReactionComponentId,
            Assert.Single(preserved.Expansions).ReactionComponentId);
        Assert.Equal(
            independent.Expansions[0].Components[0].GetRawText(),
            preserved.Expansions[0].Components[0].GetRawText());
    }

    [Fact]
    public void IndependentReactionExpansions_MultipleRowsInOneProposalArePreservedOutsideTheOwnedCeiling()
    {
        var owned = Effect(
            "effect_owned_reaction",
            new[] { ReactionComponent("owned_reaction", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "owned_reaction",
                    new[] { CharacteristicComponent("owned_expanded", "flat", -1m) })
            });
        var independentComponents = new[]
        {
            ReactionComponent("independent_reaction_a", "apply_definition", 1),
            ReactionComponent("independent_reaction_b", "apply_definition", 1)
        };
        var independentExpansionComponents = new[]
        {
            CharacteristicComponent("independent_expanded_a", "flat", -1m),
            ResistanceComponent("independent_expanded_b", "flat", -1m)
        };
        var independent = Effect(
            "effect_independent_reaction",
            independentComponents,
            "curse",
            "curse_reaction_bundle",
            null,
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "independent_reaction_a",
                    new[] { independentExpansionComponents[0] }),
                new WoundReactionExpansionProposal(
                    "independent_reaction_b",
                    new[] { independentExpansionComponents[1] })
            });

        var result = Validate(MortalRequest(
            "III",
            new[] { independent, owned },
            Entries(
                (1, "effect_owned_reaction", "event_reaction"),
                (2, "effect_owned_reaction", "characteristic_modifier"))));

        AssertValid(result);
        Assert.Single(Assert.Single(result.Envelope!.OwnedEffects).Expansions);
        var preserved = Assert.Single(result.IndependentEffects);
        Assert.Equal(2, preserved.Components.Length);
        Assert.Equal(2, preserved.Expansions.Length);
        for (var index = 0; index < 2; index++)
        {
            Assert.Equal(
                independentComponents[index].GetRawText(),
                preserved.Components[index].GetRawText());
            Assert.Equal(
                independent.Expansions[index].ReactionComponentId,
                preserved.Expansions[index].ReactionComponentId);
            Assert.Equal(
                independentExpansionComponents[index].GetRawText(),
                Assert.Single(preserved.Expansions[index].Components).GetRawText());
        }
    }

    [Fact]
    public void IndependentOnlyReactionExpansions_DoNotConsumeTheOwnedCeiling()
    {
        var independent = Enumerable.Range(0, 2)
            .Select(index => Effect(
                $"effect_independent_{index}",
                new[] { UnknownComponent($"component_independent_{index}") },
                "curse",
                $"curse_{index}",
                null,
                expansions: new[]
                {
                    new WoundReactionExpansionProposal(
                        $"reaction_independent_{index}",
                        new[] { UnknownComponent($"expanded_independent_{index}") })
                }))
            .ToArray();

        var result = Validate(MortalRequest(
            "I",
            independent,
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(true, false, false),
            declaredSlotsUsed: 0));

        AssertValid(result);
        Assert.Empty(result.Envelope!.OwnedEffects);
        Assert.Equal(2, result.IndependentEffects.Length);
        Assert.All(result.IndependentEffects, effect => Assert.Single(effect.Expansions));
    }

    [Fact]
    public void ReactionResults_MustBeRegisteredDeterministicAndDoNotHideBoundedReceipts()
    {
        AssertValid(Validate(MortalSingle(
            "I",
            ReactionComponent("component", "remove", 1),
            "event_reaction")));
        AssertIssue(
            Validate(MortalSingle(
                "I",
                ReactionComponent("component", "bounded_receipt", 1),
                "event_reaction")),
            Path + ".effects[0].components[0].payload.resultKind",
            "wound_consequence_reaction_result_invalid");
    }

    [Theory]
    [MemberData(nameof(SpiritualProfiles))]
    public void SpiritualRegistry_SealsExactProfileAxisAndAllowedMagnitude(
        string profile,
        string severity,
        string magnitudeJson,
        string axis)
    {
        var request = SpiritualSingle(
            severity,
            SpiritualComponent(
                "component",
                profile,
                "pressure",
                axis,
                JsonNode.Parse(magnitudeJson)!),
            profile);

        var result = Validate(request);

        AssertValid(result);
        var slot = Assert.Single(
            result.Envelope!.Slots,
            static candidate => candidate.ComponentId == "component");
        Assert.Equal(axis, slot.Axis);
        Assert.Equal("pressure", slot.OperationKey);
        Assert.Equal(profile + ":pressure", slot.Coordinate);
    }

    [Fact]
    public void SpiritualRegistry_UsesTheCommonComponentPriorityEnvelope()
    {
        var component = JsonNode.Parse(SpiritualComponent(
            "component",
            "spiritual_roll_hindrance",
            "pressure",
            "rollMode",
            "disadvantage").GetRawText())!.AsObject();
        component["priority"] = int.MaxValue;

        AssertIssue(
            Validate(SpiritualSingle(
                "I",
                Element(component),
                "spiritual_roll_hindrance")),
            Path + ".effects[0].components[0].priority",
            "wound_consequence_spiritual_profile_invalid");
    }

    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    public void SpiritualSeverity_RequiresExactlyRankIndependentSlots(
        string severity,
        int rank)
    {
        var components = Enumerable.Range(0, rank)
            .Select(index => SpiritualComponent(
                "component_" + index,
                "spiritual_roll_hindrance",
                SpiritualOperationValues[index],
                "rollMode",
                "disadvantage"))
            .ToArray();
        var effect = Effect("effect_spiritual", components);
        var entries = Entries(rank, "effect_spiritual", "spiritual_roll_hindrance");

        AssertValid(Validate(SpiritualRequest(severity, new[] { effect }, entries)));

        if (rank > 1)
        {
            AssertIssue(
                Validate(SpiritualRequest(
                    severity,
                    new[] { Effect("effect_spiritual", components.Take(rank - 1).ToArray()) },
                    Entries(rank - 1, "effect_spiritual", "spiritual_roll_hindrance"))),
                Path + ".derivedSlots",
                "wound_consequence_spiritual_slot_count_invalid");
        }


        var excess = components.Append(SpiritualComponent(
            "component_excess",
            "spiritual_roll_hindrance",
            SpiritualOperationValues[rank],
            "rollMode",
            "disadvantage")).ToArray();
        AssertIssue(
            Validate(SpiritualRequest(
                severity,
                new[] { Effect("effect_spiritual", excess) },
                Entries(rank + 1, "effect_spiritual", "spiritual_roll_hindrance"))),
            Path + ".derivedSlots",
            "wound_consequence_spiritual_slot_count_invalid");
    }

    [Fact]
    public void SpiritualUnavailableProfilesAndSeveritySpecificBurdenStepsFailClosed()
    {
        AssertIssue(
            Validate(SpiritualSingle(
                "I",
                SpiritualComponent(
                    "component", "spiritual_control_burden", "binding", "controlState", 1),
                "spiritual_control_burden")),
            Path + ".effects[0].components[0].profile",
            "wound_consequence_spiritual_profile_unavailable");
        AssertIssue(
            Validate(SpiritualSingle(
                "II",
                SpiritualComponent(
                    "component", "spiritual_strain_burden", "pressure", "sideStrain", 1),
                "spiritual_strain_burden")),
            Path + ".effects[0].components[0].profile",
            "wound_consequence_spiritual_profile_unavailable");
        AssertIssue(
            Validate(SpiritualSingle(
                "II",
                SpiritualComponent(
                    "component", "spiritual_art_restriction", "pressure", "artAvailability", "restrict"),
                "spiritual_art_restriction")),
            Path + ".effects[0].components[0].profile",
            "wound_consequence_spiritual_profile_unavailable");
        AssertIssue(
            Validate(SpiritualSingle(
                "III",
                SpiritualComponent(
                    "component", "spiritual_action_cost_burden", "pressure", "actionCostAudit", 3),
                "spiritual_action_cost_burden")),
            Path + ".effects[0].components[0].payload.magnitude",
            "wound_consequence_spiritual_magnitude_invalid");
        AssertIssue(
            Validate(SpiritualSingle(
                "II",
                SpiritualComponent(
                    "component", "spiritual_position_burden", "pressure", "conflictPosition", 2),
                "spiritual_position_burden")),
            Path + ".effects[0].components[0].payload.magnitude",
            "wound_consequence_spiritual_magnitude_invalid");
        AssertIssue(
            Validate(SpiritualSingle(
                "IV",
                SpiritualComponent(
                    "component", "spiritual_art_restriction", "pressure", "artAvailability", "restrict"),
                "spiritual_art_restriction")),
            Path + ".effects[0].components[0].payload.magnitude",
            "wound_consequence_spiritual_magnitude_invalid");
    }

    [Theory]
    [InlineData("II", 1)]
    [InlineData("III", 2)]
    [InlineData("IV", 3)]
    public void SpiritualActionCostBurden_UsesTheExactSeverityStep(
        string severity,
        int magnitude)
    {
        AssertValid(Validate(SpiritualSingle(
            severity,
            SpiritualComponent(
                "component",
                "spiritual_action_cost_burden",
                "pressure",
                "actionCostAudit",
                magnitude),
            "spiritual_action_cost_burden")));
    }

    [Fact]
    public void SpiritualAxisAndArtMagnitude_AreExactOrdinalContracts()
    {
        AssertIssue(
            Validate(SpiritualSingle(
                "I",
                SpiritualComponent(
                    "component",
                    "spiritual_roll_hindrance",
                    "pressure",
                    "actionCostAudit",
                    "disadvantage"),
                "spiritual_roll_hindrance")),
            Path + ".effects[0].components[0].payload.axis",
            "wound_consequence_spiritual_axis_invalid");

        AssertValid(Validate(SpiritualSingle(
            "IV",
            SpiritualComponent(
                "component",
                "spiritual_art_restriction",
                "pressure",
                "artAvailability",
                "forbid"),
            "spiritual_art_restriction")));
    }

    [Theory]
    [MemberData(nameof(StandardSpiritualArts))]
    public void SpiritualArtRestriction_AcceptsEveryExactStandardArt(string operation)
    {
        AssertValid(Validate(SpiritualSingle(
            "IV",
            SpiritualComponent(
                "component",
                "spiritual_art_restriction",
                operation,
                "artAvailability",
                "forbid"),
            "spiritual_art_restriction")));
    }

    [Theory]
    [InlineData("force_incarnation")]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public void SpiritualArtRestriction_RejectsNoncombatAndHealingArts(string operation)
    {
        AssertIssue(
            Validate(SpiritualSingle(
                "IV",
                SpiritualComponent(
                    "component",
                    "spiritual_art_restriction",
                    operation,
                    "artAvailability",
                    "forbid"),
                "spiritual_art_restriction")),
            Path + ".effects[0].components[0].payload.operation",
            "wound_consequence_spiritual_operation_invalid");
    }

    [Fact]
    public void SpiritualDuplicateProfileOperationCoordinate_IsRejectedEvenAcrossEffects()
    {
        var first = Effect(
            "effect_a",
            SpiritualComponent(
                "component_a", "spiritual_roll_hindrance", "pressure", "rollMode", "disadvantage"));
        var second = Effect(
            "effect_b",
            SpiritualComponent(
                "component_b", "spiritual_roll_hindrance", "pressure", "rollMode", "disadvantage"));

        AssertIssue(
            Validate(SpiritualRequest(
                "II",
                new[] { first, second },
                Entries(
                    (1, "effect_a", "spiritual_roll_hindrance"),
                    (2, "effect_b", "spiritual_roll_hindrance")))),
            Path + ".effects[1].components[0].payload.operation",
            "wound_consequence_duplicate_coordinate");
    }

    [Theory]
    [MemberData(nameof(SpiritualOperations))]
    public void SpiritualOperations_UseTheExactElevenNonSafetyKeys(string operation)
    {
        AssertValid(Validate(SpiritualSingle(
            "I",
            SpiritualComponent(
                "component", "spiritual_roll_hindrance", operation, "rollMode", "disadvantage"),
            "spiritual_roll_hindrance")));
    }

    [Theory]
    [MemberData(nameof(SpiritualSafetyOperations))]
    public void SpiritualConsequences_NeverTargetHealingInspectionCommunicationHelpOrExit(string operation)
    {
        AssertIssue(
            Validate(SpiritualSingle(
                "I",
                SpiritualComponent(
                    "component", "spiritual_roll_hindrance", operation, "rollMode", "disadvantage"),
                "spiritual_roll_hindrance")),
            Path + ".effects[0].components[0].payload.operation",
            "wound_consequence_spiritual_operation_invalid");
    }

    [Fact]
    public void IndependentEffects_ArePreservedAndNeverConsumeSlotsOrEnterProfileValidation()
    {
        var owned = Effect(
            "effect_owned",
            CharacteristicComponent("owned_component", "flat", -1m));
        var independent = new[]
        {
            Effect("effect_curse", new[] { UnknownComponent("curse_component") }, "curse", "curse_exact", null),
            Effect("effect_oath", new[] { UnknownComponent("oath_component") }, "oath", "oath_exact", null),
            Effect("effect_fate", new[] { UnknownComponent("fate_component") }, "fate_card", "fate_exact", null),
            Effect("effect_saref", new[] { UnknownComponent("saref_component") }, "saref", "memory_suppression", null),
            Effect("effect_other_wound", new[] { UnknownComponent("other_component") }, "wound", "wound_other", "wound_other")
        };
        var request = MortalRequest(
            "I",
            independent.Append(owned).ToArray(),
            Entries(1, "effect_owned", "characteristic_modifier"));

        var result = Validate(request);

        AssertValid(result);
        Assert.Equal(1, result.Envelope!.SlotsUsed);
        Assert.Equal(new[] { "effect_owned" }, result.Envelope.OwnedEffects.Select(static effect => effect.EffectId));
        Assert.Equal(
            independent.Select(static effect => effect.EffectId).OrderBy(
                static id => id,
                StringComparer.Ordinal),
            result.IndependentEffects.Select(static effect => effect.EffectId));
        var preservedSaref = Assert.Single(
            result.IndependentEffects,
            static effect => effect.EffectId == "effect_saref");
        Assert.Equal(
            independent.Single(static effect => effect.EffectId == "effect_saref")
                .Components[0]
                .GetRawText(),
            preservedSaref.Components[0].GetRawText());
        Assert.DoesNotContain(result.Issues, issue =>
            string.Equals(issue.Code, "wound_consequence_profile_unsupported", StringComparison.Ordinal));
    }

    [Fact]
    public void ExactBinding_HasNoCaseNameOrConfusableFallbackAndPartialBindingsAreInvalid()
    {
        const string confusable = "wоund_test_exact"; // Cyrillic 'о'.
        var unrelated = Effect(
            "effect_confusable",
            new[] { UnknownComponent("component") },
            "wound",
            confusable,
            confusable);
        var markerOnly = MortalRequest(
            "I",
            new[] { unrelated },
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(true, false, false));

        var preserved = Validate(markerOnly);

        AssertValid(preserved);
        Assert.Empty(preserved.Envelope!.OwnedEffects);
        Assert.Equal("effect_confusable", Assert.Single(preserved.IndependentEffects).EffectId);

        var caseVariant = Effect(
            "effect_case_variant",
            new[] { UnknownComponent("component") },
            "wound",
            WoundId.ToUpperInvariant(),
            WoundId.ToUpperInvariant());
        var casePreserved = Validate(MortalRequest(
            "I",
            new[] { caseVariant },
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(true, false, false)));
        AssertValid(casePreserved);
        Assert.Equal("effect_case_variant", Assert.Single(casePreserved.IndependentEffects).EffectId);

        var partial = Effect(
            "effect_partial",
            new[] { CharacteristicComponent("component", "flat", -1m) },
            "wound",
            WoundId,
            "wound_other");
        AssertIssue(
            Validate(MortalRequest(
                "I",
                new[] { partial },
                Entries(1, "effect_partial", "characteristic_modifier"))),
            Path + ".effects[0].reciprocalWoundId",
            "wound_consequence_binding_invalid");

        var wrongMarker = Effect(
            "effect_marker",
            MarkerComponent("component_marker")).Components[0];
        var wrongMarkerNode = JsonNode.Parse(wrongMarker.GetRawText())!.AsObject();
        wrongMarkerNode["payload"]!["woundId"] = "wound_other";
        AssertIssue(
            Validate(MortalSingle(
                "I",
                Element(wrongMarkerNode),
                "wound_consequence")),
            Path + ".effects[0].components[0].payload.woundId",
            "wound_consequence_binding_invalid");
    }

    [Fact]
    public void RequestAndProposalIdentifiers_AreValidatedExactlyBeforeClassification()
    {
        var invalidWound = new WoundConsequenceEnvelopeRequest(
            " wound_test_exact",
            "physical",
            "I",
            new WoundConsequences(1, 0, Array.Empty<WoundConsequenceEntry>()),
            new WoundConsequenceLifecycleEvidence(true, false, false),
            Array.Empty<WoundConsequenceEffectProposal>(),
            Array.Empty<WoundResourceEnvelopeBound>());
        AssertIssue(
            Validate(invalidWound),
            Path + ".woundId",
            "wound_consequence_identifier_invalid");

        var invalidEffectId = Effect(
            " effect_owned",
            CharacteristicComponent("component", "flat", -1m));
        var invalidEffectResult = Validate(MortalRequest(
            "I",
            new[] { invalidEffectId },
            Entries(1, " effect_owned", "characteristic_modifier")));
        AssertIssue(
            invalidEffectResult,
            Path + ".effects[0].effectId",
            "wound_consequence_identifier_invalid");
        AssertIssue(
            invalidEffectResult,
            Path + ".declared.entries[0].effectId",
            "wound_consequence_identifier_invalid");

        var malformedBindings = new[]
        {
            Effect(
                "effect_kind",
                new[] { UnknownComponent("component") },
                " wound",
                WoundId,
                WoundId),
            Effect(
                "effect_source",
                new[] { UnknownComponent("component") },
                "wound",
                " wound_other",
                "wound_other"),
            Effect(
                "effect_reciprocal",
                new[] { UnknownComponent("component") },
                "wound",
                "wound_other",
                " wound_other")
        };
        var malformedResult = Validate(MortalRequest(
            "I",
            malformedBindings,
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(true, false, false),
            resourceBounds: new[]
            {
                new WoundResourceEnvelopeBound(" health", 100m, 1m)
            }));
        AssertIssue(
            malformedResult,
            Path + ".effects[0].sourceKind",
            "wound_consequence_identifier_invalid");
        AssertIssue(
            malformedResult,
            Path + ".effects[1].sourceId",
            "wound_consequence_identifier_invalid");
        AssertIssue(
            malformedResult,
            Path + ".effects[2].reciprocalWoundId",
            "wound_consequence_identifier_invalid");
        AssertIssue(
            malformedResult,
            Path + ".resourceBounds[0].resourceKey",
            "wound_consequence_identifier_invalid");

        var invalidEvidence = Effect(
            "effect_evidence",
            new[] { CharacteristicComponent("component", "flat", -1m) },
            cadences: new[]
            {
                new WoundPeriodicCadenceEvidence(" component", " owner_turn_end", 1)
            },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    " reaction",
                    new[] { MarkerComponent("marker") })
            });
        var evidenceResult = Validate(MortalRequest(
            "I",
            new[] { invalidEvidence },
            Entries(1, "effect_evidence", "characteristic_modifier")));
        AssertIssue(
            evidenceResult,
            Path + ".effects[0].cadences[0].componentId",
            "wound_consequence_identifier_invalid");
        AssertIssue(
            evidenceResult,
            Path + ".effects[0].cadences[0].sourceEvent",
            "wound_consequence_identifier_invalid");
        AssertIssue(
            evidenceResult,
            Path + ".effects[0].expansions[0].reactionComponentId",
            "wound_consequence_identifier_invalid");
    }

    [Theory]
    [InlineData("effect_shared")]
    [InlineData("EFFECT_SHARED")]
    public void EffectIdentities_AreExactAndConfusableUniqueAcrossEveryProposal(
        string secondEffectId)
    {
        var ownedDuplicate = Validate(MortalRequest(
            "II",
            new[]
            {
                Effect(
                    "effect_shared",
                    CharacteristicComponent("component_a", "flat", -1m)),
                Effect(
                    secondEffectId,
                    ActionComponent("component_b", "movement", "restrict"))
            },
            Entries(
                (1, "effect_shared", "characteristic_modifier"),
                (2, secondEffectId, "action_control"))));
        AssertIssue(
            ownedDuplicate,
            Path + ".effects[1].effectId",
            "wound_consequence_effect_identity_duplicate");

        var ownedIndependentDuplicate = Validate(MortalRequest(
            "I",
            new[]
            {
                Effect(
                    "effect_shared",
                    CharacteristicComponent("component_owned", "flat", -1m)),
                Effect(
                    secondEffectId,
                    new[] { UnknownComponent("component_independent") },
                    "curse",
                    "curse_exact",
                    null)
            },
            Entries(1, "effect_shared", "characteristic_modifier")));
        AssertIssue(
            ownedIndependentDuplicate,
            Path + ".effects[1].effectId",
            "wound_consequence_effect_identity_duplicate");
    }

    [Theory]
    [InlineData("component_shared")]
    [InlineData("COMPONENT_SHARED")]
    public void ComponentIdentities_AreExactAndConfusableUniqueAcrossOneEffect(
        string secondComponentId)
    {
        var topLevel = Effect(
            "effect_components",
            CharacteristicComponent("component_shared", "flat", -1m),
            ActionComponent(secondComponentId, "movement", "restrict"));
        AssertIssue(
            Validate(MortalRequest(
                "II",
                new[] { topLevel },
                Entries(
                    (1, "effect_components", "characteristic_modifier"),
                    (2, "effect_components", "action_control")))),
            Path + ".effects[0].components[1].componentId",
            "wound_consequence_component_identity_duplicate");

        var flattened = Effect(
            "effect_flattened_components",
            new[]
            {
                MarkerComponent("component_shared"),
                ReactionComponent("component_reaction", "apply_definition", 2)
            },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[]
                    {
                        CharacteristicComponent(secondComponentId, "flat", -1m)
                    })
            });
        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { flattened },
                Entries(
                    (1, "effect_flattened_components", "event_reaction"),
                    (2, "effect_flattened_components", "characteristic_modifier")))),
            Path + ".effects[0].expansions[0].components[0].componentId",
            "wound_consequence_component_identity_duplicate");
    }

    [Fact]
    public void IndependentComponentIdentifiers_AreValidatedBeforeSemanticDetachment()
    {
        var result = Validate(MortalRequest(
            "I",
            new[]
            {
                Effect(
                    "effect_owned",
                    CharacteristicComponent("component_owned", "flat", -1m)),
                Effect(
                    "effect_independent",
                    new[] { UnknownComponent(" component_invalid") },
                    "curse",
                    "curse_exact",
                    null)
            },
            Entries(1, "effect_owned", "characteristic_modifier")));

        AssertIssue(
            result,
            Path + ".effects[1].components[0].componentId",
            "wound_consequence_identifier_invalid");
    }

    [Fact]
    public void EveryProposalAndPresentExpansion_RequiresNonEmptyComponents()
    {
        var emptyOwned = Effect(
            "effect_empty_owned",
            Array.Empty<JsonElement>());
        AssertIssue(
            Validate(MortalRequest(
                "I",
                new[] { emptyOwned },
                Array.Empty<WoundConsequenceEntry>(),
                lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                    true,
                    false,
                    false),
                declaredSlotsUsed: 0)),
            Path + ".effects[0].components",
            "wound_consequence_components_required");

        var emptyIndependent = Effect(
            "effect_empty_independent",
            Array.Empty<JsonElement>(),
            "curse",
            "curse_exact",
            null);
        AssertIssue(
            Validate(MortalRequest(
                "I",
                new[]
                {
                    Effect(
                        "effect_owned",
                        CharacteristicComponent("component_owned", "flat", -1m)),
                    emptyIndependent
                },
                Entries(1, "effect_owned", "characteristic_modifier"))),
            Path + ".effects[1].components",
            "wound_consequence_components_required");

        var emptyExpansion = Effect(
            "effect_empty_expansion",
            new[] { ReactionComponent("component_reaction", "apply_definition", 2) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    Array.Empty<JsonElement>())
            });
        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { emptyExpansion },
                Entries(1, "effect_empty_expansion", "event_reaction"))),
            Path + ".effects[0].expansions[0].components",
            "wound_consequence_components_required");
    }

    [Fact]
    public void DuplicateRawProperties_AreRejectedRecursivelyBeforeMortalSemantics()
    {
        var component = RawElement("""
            {
              "componentId": "component_duplicate",
              "profile": "action_control",
              "profile": "action_control",
              "priority": 0,
              "payload": {
                "action": "escape",
                "action": "attack",
                "operation": "forbid",
                "modifier": null
              }
            }
            """);
        var result = Validate(MortalSingle("III", component, "action_control"));

        AssertIssue(
            result,
            Path + ".effects[0].components[0].profile",
            "wound_consequence_duplicate_property");
        AssertIssue(
            result,
            Path + ".effects[0].components[0].payload.action",
            "wound_consequence_duplicate_property");
    }

    [Fact]
    public void DuplicateRawProperties_AreRejectedRecursivelyBeforeSpiritualSemantics()
    {
        var component = RawElement("""
            {
              "componentId": "component_duplicate",
              "profile": "unknown_profile",
              "profile": "spiritual_art_restriction",
              "priority": 0,
              "payload": {
                "operation": "withdrawal",
                "operation": "pressure",
                "axis": "artAvailability",
                "magnitude": "restrict",
                "magnitude": "restrict"
              }
            }
            """);
        var result = Validate(SpiritualSingle(
            "III",
            component,
            "spiritual_art_restriction"));

        AssertIssue(
            result,
            Path + ".effects[0].components[0].profile",
            "wound_consequence_duplicate_property");
        AssertIssue(
            result,
            Path + ".effects[0].components[0].payload.operation",
            "wound_consequence_duplicate_property");
        AssertIssue(
            result,
            Path + ".effects[0].components[0].payload.magnitude",
            "wound_consequence_duplicate_property");
    }

    [Fact]
    public void ForgedNullAndDefaultTypedInputs_ReturnPreciseIssuesWithoutThrowing()
    {
        WoundConsequenceEnvelopeValidationResult? result = null;
        var exception = Record.Exception(() =>
        {
            var nullCollections = new WoundConsequenceEffectProposal(
                "effect_null_collections",
                "wound",
                WoundId,
                WoundId,
                null!,
                null!,
                null!);
            var defaultComponent = new WoundConsequenceEffectProposal(
                "effect_default_component",
                "wound",
                WoundId,
                WoundId,
                new[] { default(JsonElement) },
                Array.Empty<WoundPeriodicCadenceEvidence>(),
                Array.Empty<WoundReactionExpansionProposal>());
            var request = new WoundConsequenceEnvelopeRequest(
                WoundId,
                "physical",
                "I",
                null!,
                null!,
                new WoundConsequenceEffectProposal[]
                {
                    nullCollections,
                    defaultComponent,
                    null!
                },
                new WoundResourceEnvelopeBound[] { null! });
            result = Validate(request);
        });

        Assert.Null(exception);
        Assert.NotNull(result);
        AssertIssue(result!, Path + ".declared", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".lifecycleEvidence", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".effects[0].components", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".effects[0].cadences", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".effects[0].expansions", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".effects[1].components[0]", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".effects[2]", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".resourceBounds[0]", "wound_consequence_input_invalid");
    }

    [Fact]
    public void ForgedNullTopLevelListsAndDeclaredEntries_ReturnIssuesWithoutThrowing()
    {
        WoundConsequenceEnvelopeValidationResult? result = null;
        var exception = Record.Exception(() =>
        {
            var request = new WoundConsequenceEnvelopeRequest(
                WoundId,
                "physical",
                "I",
                new WoundConsequences(1, 0, null!),
                new WoundConsequenceLifecycleEvidence(true, false, false),
                null!,
                null!);
            result = Validate(request);
        });

        Assert.Null(exception);
        Assert.NotNull(result);
        AssertIssue(result!, Path + ".declared.entries", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".effects", "wound_consequence_input_invalid");
        AssertIssue(result!, Path + ".resourceBounds", "wound_consequence_input_invalid");
    }

    [Fact]
    public void NullRequest_ReturnsInputIssueWithoutThrowing()
    {
        WoundConsequenceEnvelopeValidationResult? result = null;
        var exception = Record.Exception(() =>
            result = WoundConsequenceEnvelopeCatalog.Validate(null!, Path));

        Assert.Null(exception);
        Assert.NotNull(result);
        AssertIssue(result!, Path, "wound_consequence_input_invalid");
    }

    [Fact]
    public void Result_IsDeterministicAndImmutableAcrossInputOrderAndCallerMutation()
    {
        var effects = new List<WoundConsequenceEffectProposal>
        {
            Effect("effect_z", ActionComponent("component_z", "movement", "restrict")),
            Effect("effect_a", CharacteristicComponent("component_a", "flat", -2m))
        };
        var entries = Entries(
            (1, "effect_a", "characteristic_modifier"),
            (2, "effect_z", "action_control"));
        var request = MortalRequest("II", effects, entries);
        effects.Clear();

        var forward = Validate(request);
        var reverse = Validate(MortalRequest(
            "II",
            request.Effects.Reverse().ToArray(),
            entries));

        AssertValid(forward);
        AssertValid(reverse);
        Assert.Equal(
            forward.Envelope!.Slots.Select(static slot => slot.Coordinate),
            reverse.Envelope!.Slots.Select(static slot => slot.Coordinate));
        Assert.Equal(
            new[] { "effect_a", "effect_z" },
            forward.Envelope.OwnedEffects.Select(static effect => effect.EffectId));
        Assert.IsType<ImmutableArray<WoundDerivedConsequenceSlot>>(forward.Envelope.Slots);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<WoundDerivedConsequenceSlot>)forward.Envelope.Slots).Add(
                forward.Envelope.Slots[0]));
    }

    [Fact]
    public void BoundedValidation_RejectsMoreThanFourDirectSlotsAndOversizedReactionExpansion()
    {
        var fiveComponents = new[]
        {
            RollComponent("component_attack", "attack_roll"),
            RollComponent("component_defense", "defense_roll"),
            RollComponent("component_skill", "skill_check"),
            RollComponent("component_save", "saving_throw"),
            RollComponent("component_damage", "damage_roll")
        };
        AssertIssue(
            Validate(MortalRequest(
                "IV",
                new[] { Effect("effect_roll", fiveComponents) },
                Entries(4, "effect_roll", "roll_modifier"))),
            Path + ".derivedSlots",
            "wound_consequence_slot_budget_exceeded");

        var expanded = Enumerable.Range(
                0,
                WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionComponents + 1)
            .Select(index => MarkerComponent("marker_" + index))
            .ToArray();
        var reaction = Effect(
            "effect_reaction",
            new[] { ReactionComponent("reaction", "apply_definition", 64) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal("reaction", expanded)
            });
        AssertIssue(
            Validate(MortalRequest(
                "IV",
                new[] { reaction },
                Entries(1, "effect_reaction", "event_reaction"))),
            Path + ".effects[0].expansions[0].components",
            "wound_consequence_limit_exceeded");
    }

    [Fact]
    public void DeclaredConsequenceBound_IsCheckedBeforeReadingEntryFive()
    {
        var guarded = new GuardedReadOnlyList<WoundConsequenceEntry>(
            WoundMaterializationContract.MaxConsequences + 1,
            WoundMaterializationContract.MaxConsequences,
            index => Entry(index + 1, "effect_unused", "wound_consequence"));

        AssertLimitBeforeRead(
            guarded,
            () => Validate(MortalRequest(
                "I",
                Array.Empty<WoundConsequenceEffectProposal>(),
                guarded,
                lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                    true,
                    false,
                    false),
                declaredSlotsUsed: 0)),
            Path + ".declared.entries");
    }

    [Fact]
    public void EffectProposalBound_IsCheckedBeforeReadingProposalOneHundredTwentyNine()
    {
        var guarded = new GuardedReadOnlyList<WoundConsequenceEffectProposal>(
            129,
            128,
            index => Effect(
                $"effect_{index:D3}",
                UnknownComponent($"component_{index:D3}")));

        AssertLimitBeforeRead(
            guarded,
            () => Validate(MortalRequest(
                "I",
                guarded,
                Array.Empty<WoundConsequenceEntry>(),
                lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                    true,
                    false,
                    false),
                declaredSlotsUsed: 0)),
            Path + ".effects");
    }

    [Fact]
    public void ComponentBound_IsCheckedBeforeReadingComponentSixtyFive()
    {
        var guarded = new GuardedReadOnlyList<JsonElement>(
            65,
            64,
            index => MarkerComponent($"component_{index:D2}"));

        AssertLimitBeforeRead(
            guarded,
            () =>
            {
                var effect = Effect("effect_components", guarded);
                return Validate(MortalRequest(
                    "I",
                    new[] { effect },
                    Array.Empty<WoundConsequenceEntry>(),
                    lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                        true,
                        false,
                        false),
                    declaredSlotsUsed: 0));
            },
            Path + ".effects[0].components");
    }

    [Fact]
    public void CadenceBound_IsCheckedBeforeReadingCadenceSixtyFive()
    {
        var guarded = new GuardedReadOnlyList<WoundPeriodicCadenceEvidence>(
            65,
            64,
            index => new WoundPeriodicCadenceEvidence(
                $"component_{index:D2}",
                "owner_turn_end",
                1));

        AssertLimitBeforeRead(
            guarded,
            () =>
            {
                var effect = Effect(
                    "effect_cadences",
                    new[]
                    {
                        CharacteristicComponent("component_characteristic", "flat", -1m)
                    },
                    cadences: guarded);
                return Validate(MortalRequest(
                    "I",
                    new[] { effect },
                    Entries(1, "effect_cadences", "characteristic_modifier")));
            },
            Path + ".effects[0].cadences");
    }

    [Fact]
    public void ReactionExpansionProposalBound_PreservesExpansionSixtyFour()
    {
        var guarded = new GuardedReadOnlyList<WoundReactionExpansionProposal>(
            64,
            64,
            index => new WoundReactionExpansionProposal(
                $"reaction_{index:D2}",
                new[] { UnknownComponent($"expanded_{index:D2}") }));
        var independent = Effect(
            "effect_expansion_boundary",
            new[] { UnknownComponent("component_independent") },
            "curse",
            "curse_expansion_boundary",
            null,
            expansions: guarded);

        var result = Validate(MortalRequest(
            "I",
            new[] { independent },
            Array.Empty<WoundConsequenceEntry>(),
            lifecycleEvidence: new WoundConsequenceLifecycleEvidence(true, false, false),
            declaredSlotsUsed: 0));

        AssertValid(result);
        Assert.Equal(64, guarded.ReadCount);
        Assert.Equal(64, Assert.Single(result.IndependentEffects).Expansions.Length);
    }

    [Fact]
    public void ReactionExpansionProposalBound_IsCheckedBeforeReadingExpansionSixtyFive()
    {
        var guarded = new GuardedReadOnlyList<WoundReactionExpansionProposal>(
            65,
            64,
            index => new WoundReactionExpansionProposal(
                $"reaction_{index}",
                new[] { MarkerComponent($"marker_{index}") }));

        var result = AssertLimitBeforeRead(
            guarded,
            () =>
            {
                var effect = Effect(
                    "effect_expansions",
                    new[] { UnknownComponent("component_independent") },
                    "curse",
                    "curse_expansion_overflow",
                    null,
                    expansions: guarded);
                return Validate(MortalRequest(
                    "I",
                    new[] { effect },
                    Array.Empty<WoundConsequenceEntry>(),
                    lifecycleEvidence: new WoundConsequenceLifecycleEvidence(
                        true,
                        false,
                        false),
                    declaredSlotsUsed: 0));
            },
            Path + ".effects[0].expansions");
        var issue = Assert.Single(result.Issues.Where(issue =>
            string.Equals(
                issue.FilePath,
                Path + ".effects[0].expansions",
                StringComparison.Ordinal) &&
            string.Equals(
                issue.Code,
                "wound_consequence_limit_exceeded",
                StringComparison.Ordinal)));
        Assert.Equal("at most 64 flattened reaction expansions per effect proposal", issue.Expected);
        Assert.Equal("65", issue.Actual);
    }

    [Fact]
    public void FlattenedComponentBound_IsCheckedBeforeReadingComponentSixtyFive()
    {
        var guarded = new GuardedReadOnlyList<JsonElement>(
            WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionComponents + 1,
            WoundConsequenceEnvelopeCatalog.MaximumReactionExpansionComponents,
            index => MarkerComponent($"expanded_{index:D2}"));

        AssertLimitBeforeRead(
            guarded,
            () =>
            {
                var expansion = new WoundReactionExpansionProposal("reaction", guarded);
                var effect = Effect(
                    "effect_flattened",
                    new[] { ReactionComponent("reaction", "apply_definition", 2) },
                    expansions: new[] { expansion });
                return Validate(MortalRequest(
                    "III",
                    new[] { effect },
                    Entries(1, "effect_flattened", "event_reaction")));
            },
            Path + ".effects[0].expansions[0].components");
    }

    [Fact]
    public void ResourceBoundEvidenceLimit_IsCheckedBeforeReadingEntryOneHundredTwentyNine()
    {
        var guarded = new GuardedReadOnlyList<WoundResourceEnvelopeBound>(
            129,
            128,
            index => new WoundResourceEnvelopeBound($"resource_{index:D3}", 100m, 1m));

        AssertLimitBeforeRead(
            guarded,
            () => Validate(MortalRequest(
                "I",
                new[]
                {
                    Effect(
                        "effect_resource_context",
                        CharacteristicComponent("component_characteristic", "flat", -1m))
                },
                Entries(1, "effect_resource_context", "characteristic_modifier"),
                resourceBounds: guarded)),
            Path + ".resourceBounds");
    }

    [Fact]
    public void OneOwnedAndOneHundredTwentySevenIndependentEffects_ArePreservedAtTheBound()
    {
        var effects = new List<WoundConsequenceEffectProposal>
        {
            Effect(
                "effect_owned",
                CharacteristicComponent("component_owned", "flat", -1m))
        };
        effects.AddRange(Enumerable.Range(0, 127).Select(index => Effect(
            $"effect_independent_{index:D3}",
            new[] { UnknownComponent($"component_independent_{index:D3}") },
            "curse",
            $"curse_{index:D3}",
            null)));

        var result = Validate(MortalRequest(
            "I",
            effects,
            Entries(1, "effect_owned", "characteristic_modifier")));

        AssertValid(result);
        Assert.Equal(127, result.IndependentEffects.Length);
        Assert.Equal("effect_owned", result.Envelope!.OwnedEffects.Single().EffectId);
    }

    [Theory]
    [InlineData("afterlife_combat_condition", "physical")]
    [InlineData("wound_consequence", "spiritual")]
    [InlineData("unknown_profile", "physical")]
    [InlineData("characteristic_modifier", "spiritual")]
    public void OwnedEffects_RejectEveryProfileOutsideTheExactDomainRegistry(
        string profile,
        string domain)
    {
        var component = profile switch
        {
            "characteristic_modifier" => CharacteristicComponent("component", "flat", -1m),
            "wound_consequence" => MarkerComponent("component"),
            _ => UnknownComponent("component", profile)
        };
        var request = domain == "physical"
            ? MortalSingle("I", component, profile)
            : SpiritualSingle("I", component, profile);

        AssertIssue(
            Validate(request),
            Path + ".effects[0].components[0].profile",
            "wound_consequence_profile_unsupported");
    }

    private static WoundConsequenceEnvelopeValidationResult Validate(
        WoundConsequenceEnvelopeRequest request) =>
        WoundConsequenceEnvelopeCatalog.Validate(request, Path);

    private static WoundConsequenceEnvelopeRequest MortalSingle(
        string severity,
        JsonElement component,
        string profile) =>
        MortalRequest(
            severity,
            new[] { Effect("effect_single", component) },
            Entries(1, "effect_single", profile));

    private static WoundConsequenceEnvelopeRequest MortalSingle(
        string severity,
        WoundConsequenceEffectProposal effect,
        string profile) =>
        MortalRequest(
            severity,
            new[] { effect },
            Entries(1, effect.EffectId, profile));

    private static WoundConsequenceEnvelopeRequest SpiritualSingle(
        string severity,
        JsonElement component,
        string profile)
    {
        var rank = Rank(severity);
        var components = new List<JsonElement> { component };
        var entries = new List<WoundConsequenceEntry>
        {
            Entry(1, "effect_spiritual", profile)
        };
        var paddingOperations = new[] { "guard", "maneuver", "binding" };
        for (var index = 1; index < rank; index++)
        {
            components.Add(SpiritualComponent(
                "component_pad_" + index,
                "spiritual_roll_hindrance",
                paddingOperations[index - 1],
                "rollMode",
                "disadvantage"));
            entries.Add(Entry(
                index + 1,
                "effect_spiritual",
                "spiritual_roll_hindrance"));
        }

        return SpiritualRequest(
            severity,
            new[] { Effect("effect_spiritual", components.ToArray()) },
            entries);
    }

    private static WoundConsequenceEnvelopeRequest MortalPeriodic(
        decimal amount,
        IReadOnlyList<WoundPeriodicCadenceEvidence> cadence,
        IReadOnlyList<WoundResourceEnvelopeBound>? bounds = null)
    {
        var effect = Effect(
            "effect_periodic",
            new[] { PeriodicComponent("component_periodic", amount) },
            cadences: cadence);
        return MortalRequest(
            "I",
            new[] { effect },
            Entries(1, "effect_periodic", "periodic_damage"),
            resourceBounds: bounds ?? new[]
            {
                new WoundResourceEnvelopeBound("health", 100m, 1m)
            });
    }

    private static WoundConsequenceEnvelopeRequest MortalRequest(
        string severity,
        IReadOnlyList<WoundConsequenceEffectProposal> effects,
        IReadOnlyList<WoundConsequenceEntry> entries,
        WoundConsequenceLifecycleEvidence? lifecycleEvidence = null,
        IReadOnlyList<WoundResourceEnvelopeBound>? resourceBounds = null,
        int? declaredSlotBudget = null,
        int? declaredSlotsUsed = null) =>
        Request(
            "physical",
            severity,
            effects,
            entries,
            lifecycleEvidence ?? new WoundConsequenceLifecycleEvidence(false, false, false),
            resourceBounds ?? Array.Empty<WoundResourceEnvelopeBound>(),
            declaredSlotBudget,
            declaredSlotsUsed);

    private static WoundConsequenceEnvelopeRequest SpiritualRequest(
        string severity,
        IReadOnlyList<WoundConsequenceEffectProposal> effects,
        IReadOnlyList<WoundConsequenceEntry> entries) =>
        Request(
            "spiritual",
            severity,
            effects,
            entries,
            new WoundConsequenceLifecycleEvidence(false, false, false),
            Array.Empty<WoundResourceEnvelopeBound>(),
            null,
            null);

    private static WoundConsequenceEnvelopeRequest Request(
        string domain,
        string severity,
        IReadOnlyList<WoundConsequenceEffectProposal> effects,
        IReadOnlyList<WoundConsequenceEntry> entries,
        WoundConsequenceLifecycleEvidence lifecycleEvidence,
        IReadOnlyList<WoundResourceEnvelopeBound> resourceBounds,
        int? declaredSlotBudget,
        int? declaredSlotsUsed)
    {
        var rank = Rank(severity);
        return new WoundConsequenceEnvelopeRequest(
            WoundId,
            domain,
            severity,
            new WoundConsequences(
                declaredSlotBudget ?? rank,
                declaredSlotsUsed ?? entries.Count,
                entries),
            lifecycleEvidence,
            effects,
            resourceBounds);
    }

    private static int Rank(string severity) => severity switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        "IV" => 4,
        _ => throw new ArgumentOutOfRangeException(nameof(severity))
    };

    private static WoundConsequenceEffectProposal Effect(
        string effectId,
        params JsonElement[] components) =>
        Effect(effectId, components, "wound", WoundId, WoundId);

    private static WoundConsequenceEffectProposal Effect(
        string effectId,
        IReadOnlyList<JsonElement> components,
        string sourceKind = "wound",
        string sourceId = WoundId,
        string? reciprocalWoundId = WoundId,
        IReadOnlyList<WoundPeriodicCadenceEvidence>? cadences = null,
        IReadOnlyList<WoundReactionExpansionProposal>? expansions = null) =>
        new(
            effectId,
            sourceKind,
            sourceId,
            reciprocalWoundId,
            components,
            cadences ?? Array.Empty<WoundPeriodicCadenceEvidence>(),
            expansions ?? Array.Empty<WoundReactionExpansionProposal>());

    private static WoundConsequenceEntry[] Entries(
        int count,
        string effectId,
        string profile) =>
        Enumerable.Range(1, count)
            .Select(slot => Entry(slot, effectId, profile))
            .ToArray();

    private static WoundConsequenceEntry[] Entries(
        params (int Slot, string EffectId, string Profile)[] entries) =>
        entries.Select(entry => Entry(entry.Slot, entry.EffectId, entry.Profile)).ToArray();

    private static WoundConsequenceEntry Entry(
        int slot,
        string effectId,
        string profile) =>
        new(slot, profile, effectId, "Readable consequence");

    private static JsonElement CharacteristicComponent(
        string componentId,
        string operation,
        decimal value) =>
        Component(
            componentId,
            "characteristic_modifier",
            new JsonObject
            {
                ["characteristic"] = "strength",
                ["operation"] = operation,
                ["value"] = value,
                ["cap"] = null
            });

    private static JsonElement RawScalarComponent(
        string componentId,
        string profile,
        string targetField,
        string target,
        string operation,
        string valueJson,
        string capJson) =>
        RawElement($$"""
            {
              "componentId": "{{componentId}}",
              "profile": "{{profile}}",
              "priority": 0,
              "payload": {
                "{{targetField}}": "{{target}}",
                "operation": "{{operation}}",
                "value": {{valueJson}},
                "cap": {{capJson}}
              }
            }
            """);

    private static JsonElement ResistanceComponent(
        string componentId,
        string operation,
        decimal value) =>
        Component(
            componentId,
            "resistance_modifier",
            new JsonObject
            {
                ["resistance"] = "fire",
                ["operation"] = operation,
                ["value"] = value,
                ["cap"] = null
            });

    private static JsonElement RollComponent(
        string componentId,
        params string[] operations) =>
        Component(
            componentId,
            "roll_modifier",
            EffectMaterializationTestFixture.CreateBroadRollModifierPayload(
                "disadvantage",
                operations));

    private static JsonElement FocusedRollComponent(
        string componentId,
        string skillId) =>
        Component(
            componentId,
            "roll_modifier",
            EffectMaterializationTestFixture.CreateFocusedRollModifierPayload(
                skillId));

    private static JsonElement PeriodicComponent(
        string componentId,
        decimal amount) =>
        Component(
            componentId,
            "periodic_damage",
            new JsonObject
            {
                ["resource"] = "health",
                ["amount"] = amount,
                ["damageType"] = "bleeding",
                ["floorPolicy"] = "registered_resource_floor"
            });

    private static JsonElement ActionComponent(
        string componentId,
        string action,
        string operation,
        decimal? modifier = null) =>
        Component(
            componentId,
            "action_control",
            new JsonObject
            {
                ["action"] = action,
                ["operation"] = operation,
                ["modifier"] = modifier.HasValue
                    ? JsonValue.Create(modifier.Value)
                    : null
            });

    private static JsonElement ReactionComponent(
        string componentId,
        string resultKind,
        int maxExpansion)
    {
        var payload = new JsonObject
        {
            ["eventType"] = "owner_damaged",
            ["resultKind"] = resultKind,
            ["dependency"] = "after_current_event",
            ["maxExpansion"] = maxExpansion
        };
        if (string.Equals(resultKind, "apply_definition", StringComparison.Ordinal))
        {
            payload["definitionKey"] = "wound_reaction_definition";
            payload["parameters"] = new JsonObject();
        }
        return Component(componentId, "event_reaction", payload);
    }

    private static JsonElement MarkerComponent(string componentId) =>
        Component(
            componentId,
            "wound_consequence",
            new JsonObject
            {
                ["woundId"] = WoundId,
                ["symptom"] = "pain",
                ["consequence"] = "roll_modifier"
            });

    private static JsonElement SpiritualComponent(
        string componentId,
        string profile,
        string operation,
        string axis,
        JsonNode magnitude) =>
        Component(
            componentId,
            profile,
            new JsonObject
            {
                ["operation"] = operation,
                ["axis"] = axis,
                ["magnitude"] = magnitude.DeepClone()
            });

    private static JsonElement UnknownComponent(
        string componentId,
        string profile = "independent_unknown_profile") =>
        Component(
            componentId,
            profile,
            new JsonObject { ["independent"] = true });

    private static JsonElement Component(
        string componentId,
        string profile,
        JsonObject payload) =>
        Element(new JsonObject
        {
            ["componentId"] = componentId,
            ["profile"] = profile,
            ["priority"] = 0,
            ["payload"] = payload
        });

    private static JsonElement Element(JsonNode node) =>
        JsonSerializer.SerializeToElement(node);

    private static JsonElement RawElement(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private sealed class GuardedReadOnlyList<T> : IReadOnlyList<T>
    {
        private readonly int _readableCount;
        private readonly Func<int, T> _factory;

        internal GuardedReadOnlyList(
            int count,
            int readableCount,
            Func<int, T> factory)
        {
            Count = count;
            _readableCount = readableCount;
            _factory = factory;
        }

        public int Count { get; }

        internal int ReadCount { get; private set; }

        public T this[int index]
        {
            get
            {
                if (index < 0 || index >= _readableCount)
                {
                    throw new InvalidOperationException(
                        $"Boundary item {index} must not be read.");
                }

                ReadCount++;
                return _factory(index);
            }
        }

        public IEnumerator<T> GetEnumerator()
        {
            for (var index = 0; index < Count; index++)
                yield return this[index];
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    private static WoundConsequenceEnvelopeValidationResult AssertLimitBeforeRead<T>(
        GuardedReadOnlyList<T> guarded,
        Func<WoundConsequenceEnvelopeValidationResult> validate,
        string path)
    {
        WoundConsequenceEnvelopeValidationResult? result = null;
        var exception = Record.Exception(() => result = validate());

        Assert.Null(exception);
        Assert.Equal(0, guarded.ReadCount);
        Assert.NotNull(result);
        AssertIssue(result!, path, "wound_consequence_limit_exceeded");
        return result!;
    }

    private static void AssertValid(WoundConsequenceEnvelopeValidationResult result)
    {
        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.NotNull(result.Envelope);
        Assert.Empty(result.Issues);
    }

    private static void AssertIssue(
        WoundConsequenceEnvelopeValidationResult result,
        string path,
        string code) =>
        Assert.Contains(result.Issues, issue =>
            string.Equals(issue.FilePath, path, StringComparison.Ordinal) &&
            string.Equals(issue.Code, code, StringComparison.Ordinal));

    private static string DescribeIssues(WoundConsequenceEnvelopeValidationResult result) =>
        string.Join(
            Environment.NewLine,
            result.Issues.Select(issue =>
                $"{issue.FilePath}: {issue.Code} ({issue.Expected}; actual={issue.Actual})"));
}
