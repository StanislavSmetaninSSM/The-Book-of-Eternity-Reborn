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

    public static TheoryData<string, int, decimal, decimal> MortalMagnitudeBounds => new()
    {
        { "I", 1, 1m, 5m },
        { "II", 2, 2m, 10m },
        { "III", 3, 3m, 20m },
        { "IV", 4, 4m, 30m }
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
            RollComponent("component_roll", operations));
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
            RollComponent("component_roll", "attack_roll", "defense_roll"));
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
    public void MortalSlotDerivation_CountsEveryIndependentAxisAndEveryListedRollOperation()
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
                (3, "effect_mixed", "roll_modifier"),
                (4, "effect_mixed", "roll_modifier")));

        var result = Validate(request);

        AssertValid(result);
        Assert.Equal(
            new[] { "strength", "fire", "attack_roll", "saving_throw" },
            result.Envelope!.Slots.Select(static slot => slot.OperationKey));
        Assert.Equal(
            new[]
            {
                "characteristic", "resistance", "rollMode", "rollMode"
            },
            result.Envelope.Slots.Select(static slot => slot.Axis));
        Assert.Equal(4, result.Envelope.Slots.Select(static slot => slot.Coordinate).Distinct().Count());
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
            RollComponent("component_roll", "attack_roll", "defense_roll"));

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

        AssertValid(Validate(MortalPeriodic(
            4m,
            new[]
            {
                new WoundPeriodicCadenceEvidence("component_periodic", "owner_turn_end", 1),
                new WoundPeriodicCadenceEvidence("component_periodic", "scene_ended", 1)
            })));
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
        var reaction = ReactionComponent("component_reaction", "apply_definition", 1);
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
                (2, "effect_reaction", "roll_modifier"),
                (3, "effect_reaction", "roll_modifier")));

        var result = Validate(request);

        AssertValid(result);
        Assert.Equal(3, result.Envelope!.SlotsUsed);
        Assert.Equal(
            new[] { "owner_damaged:apply_definition", "attack_roll", "defense_roll" },
            result.Envelope.Slots.Select(static slot => slot.OperationKey));
    }

    [Fact]
    public void ReactionExpansion_MustBePresentUniqueFullyFlattenedAndWithinDeclaredBound()
    {
        var missing = Effect(
            "effect_reaction",
            ReactionComponent("component_reaction", "apply_definition", 1));
        AssertIssue(
            Validate(MortalSingle("III", missing, "event_reaction")),
            Path + ".effects[0].components[0].payload.definitionKey",
            "wound_consequence_reaction_expansion_invalid");

        var nested = Effect(
            "effect_reaction",
            new[] { ReactionComponent("component_reaction", "apply_definition", 1) },
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

        var declaredTooSmall = Effect(
            "effect_reaction",
            new[] { ReactionComponent("component_reaction", "apply_definition", 1) },
            expansions: new[]
            {
                new WoundReactionExpansionProposal(
                    "component_reaction",
                    new[]
                    {
                        RollComponent(
                            "component_expanded_roll",
                            "attack_roll",
                            "defense_roll")
                    })
            });
        AssertIssue(
            Validate(MortalRequest(
                "III",
                new[] { declaredTooSmall },
                Entries(3, "effect_reaction", "event_reaction"))),
            Path + ".effects[0].expansions[0].components",
            "wound_consequence_reaction_expansion_invalid");

        var twoReactions = Effect(
            "effect_reaction",
            new[]
            {
                ReactionComponent("reaction_a", "apply_definition", 1),
                ReactionComponent("reaction_b", "apply_definition", 1)
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
            "wound_consequence_reaction_expansion_invalid");
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
        var fiveOperations = RollComponent(
            "component_roll",
            "attack_roll",
            "defense_roll",
            "skill_check",
            "saving_throw",
            "damage_roll");
        AssertIssue(
            Validate(MortalRequest(
                "IV",
                new[] { Effect("effect_roll", fiveOperations) },
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
            new JsonObject
            {
                ["operations"] = new JsonArray(
                    operations.Select(static operation => (JsonNode?)operation).ToArray()),
                ["contribution"] = "disadvantage"
            });

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
