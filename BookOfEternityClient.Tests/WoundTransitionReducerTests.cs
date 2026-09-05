using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class WoundTransitionReducerTests
{
    private const string Path = "woundTransition";

    [Theory]
    [InlineData("I", 1)]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    public void Reduce_Create_ProposesExactActiveFreshOrUntreatedWound(
        string severity,
        int rank)
    {
        var opportunity = WithSeverity(
            PhysicalWound(),
            "IV",
            4,
            updateMaximumAtCreation: true);
        var after = NewTransition(
            WithSeverity(opportunity, severity, rank),
            "create",
            ordinal: 1,
            turn: 42);
        var evidence = CreateEvidence(after, maximumSeverityRank: 4);

        var result = WoundTransitionReducer.Reduce(Request("create", null, after, evidence));

        AssertValid(result);
        Assert.Equal(Fingerprint(after), Fingerprint(result.ProposedAfter!));
        Assert.Contains(result.Intents, intent =>
            intent is WoundCarrierTransitionIntent { Operation: "add" });
        Assert.Contains(result.Intents, intent =>
            intent is WoundEffectTransitionIntent { Operation: "apply" });
        var history = Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.Equal(
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(after.WoundId),
            history.BeforeFingerprint);
        Assert.Equal(Fingerprint(after), history.AfterFingerprint);
        Assert.False(history.Terminal);
    }

    [Fact]
    public void Reduce_Create_DirectZeroSlotMarkerRootEmitsApplyIntent()
    {
        const string effectId = "effect_wound_direct_marker";
        const string definitionKey = "definition_wound_direct_marker";
        var afterJson = WoundContractTestData.CreateActiveWound();
        var consequences = afterJson["consequences"]!.AsObject();
        consequences["slotsUsed"] = 0;
        consequences["entries"] = new JsonArray();
        consequences["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world",
                (effectId, definitionKey, "wound_consequence"));
        var after = NewTransition(
            ParseWound(afterJson),
            "create",
            ordinal: 1,
            turn: 42);

        var result = WoundTransitionReducer.Reduce(Request(
            "create",
            null,
            after,
            CreateEvidence(after)));

        AssertValid(result);
        Assert.Equal(new[] { effectId }, EffectIds(after));
        var intent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal("apply", intent.Operation);
        Assert.Empty(intent.BeforeEffectIds);
        Assert.Equal(new[] { effectId }, intent.AfterEffectIds);
    }

    [Fact]
    public void Reduce_Create_AllowsFreshCareButRejectsPriorIdentity()
    {
        var after = NewTransition(
            PhysicalWound() with
            {
                Care = PhysicalWound().Care with { State = "fresh" }
            },
            "create",
            ordinal: 1,
            turn: 42);
        var evidence = CreateEvidence(after);
        AssertValid(WoundTransitionReducer.Reduce(Request("create", null, after, evidence)));

        var result = WoundTransitionReducer.Reduce(Request(
            "create",
            PhysicalWound(),
            after,
            evidence));

        AssertInvalid(result, "wound_transition_create_prior_exists");
    }

    [Fact]
    public void Reduce_Create_RejectsOverCapTerminalOrWrongRealmDomain()
    {
        var overCap = NewTransition(
            WithSeverity(PhysicalWound(), "III", 3, updateMaximumAtCreation: true),
            "create",
            ordinal: 1,
            turn: 42);
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                overCap,
                CreateEvidence(overCap, maximumSeverityRank: 2))),
            "wound_transition_create_severity_forbidden");

        var healed = overCap with
        {
            Lifecycle = "healed",
            Care = overCap.Care with { State = "healed" }
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                healed,
                CreateEvidence(healed))),
            "wound_transition_create_state_invalid");

        var wrongRealm = NewTransition(
            SpiritualWound() with
            {
                Owner = SpiritualWound().Owner with { Realm = "mortal_world" }
            },
            "create",
            ordinal: 1,
            turn: 42);
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                wrongRealm,
                CreateEvidence(wrongRealm))),
            "wound_transition_realm_domain_invalid");
    }

    [Fact]
    public void Reduce_Create_BindsOwnerRealmChronologyMaximumAndInitialCareBaseline()
    {
        var legal = NewTransition(PhysicalWound(), "create", ordinal: 1, turn: 42);

        var mortalGuardian = legal with
        {
            Owner = legal.Owner with { OwnerKind = "guardian" }
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                mortalGuardian,
                CreateEvidence(mortalGuardian))),
            "wound_transition_owner_coordinate_invalid");

        var wrongCreatedTurn = legal with
        {
            Origin = legal.Origin with { CreatedAtTurn = 41 }
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                wrongCreatedTurn,
                CreateEvidence(wrongCreatedTurn))),
            "wound_transition_create_chronology_invalid");

        var selectedBelowCap = NewTransition(
            WithSeverity(
                legal with
                {
                    Severity = legal.Severity with { MaximumAtCreation = "IV" }
                },
                "I",
                1),
            "create",
            ordinal: 1,
            turn: 42);
        AssertValid(WoundTransitionReducer.Reduce(Request(
            "create",
            null,
            selectedBelowCap,
            CreateEvidence(selectedBelowCap, maximumSeverityRank: 4))));

        var forgedMaximum = selectedBelowCap with
        {
            Severity = selectedBelowCap.Severity with { MaximumAtCreation = "III" }
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                forgedMaximum,
                CreateEvidence(forgedMaximum, maximumSeverityRank: 4))),
            "wound_transition_create_severity_forbidden");

        var forgedCare = legal with
        {
            Care = legal.Care with
            {
                StabilizedAtTurn = 42,
                ActiveCourseId = "course_forged",
                LastAttemptId = "attempt_forged"
            }
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "create",
                null,
                forgedCare,
                CreateEvidence(forgedCare))),
            "wound_transition_create_state_invalid");
    }

    [Fact]
    public void Reduce_NonCreate_RejectsMatchingButIllegalOwnerRealmBaseline()
    {
        var before = PhysicalWound() with
        {
            Owner = PhysicalWound().Owner with { OwnerKind = "guardian" }
        };
        var after = NewTransition(before with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");

        var result = WoundTransitionReducer.Reduce(Request(
            "treat",
            before,
            after,
            TreatEvidence(before, after, Outcome(after, terminalAttempt: true))));

        AssertInvalid(result, "wound_transition_owner_coordinate_invalid");
    }

    [Fact]
    public void Reduce_ActiveTransition_RejectsTurnRegressionButAllowsEqualPriorTurn()
    {
        var before = PhysicalWound();
        var earlier = WorsenRequest(
            before,
            before.LastTransition.Turn - 1,
            "transition_turn_regression");

        AssertInvalid(
            WoundTransitionReducer.Reduce(earlier),
            "wound_transition_turn_regression");

        var equal = WorsenRequest(
            before,
            before.LastTransition.Turn,
            "transition_equal_turn");

        AssertValid(WoundTransitionReducer.Reduce(equal));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("archive")]
    public void Reduce_PostTerminalAudit_RejectsTurnRegressionButAllowsEqualHealTurn(
        string kind)
    {
        var before = HealedWound();

        AssertInvalid(
            WoundTransitionReducer.Reduce(PostTerminalAuditRequest(
                kind,
                before,
                turn: before.LastTransition.Turn - 1,
                transitionId: $"transition_{kind}_regression")),
            "wound_transition_turn_regression");

        AssertValid(WoundTransitionReducer.Reduce(PostTerminalAuditRequest(
            kind,
            before,
            turn: before.LastTransition.Turn,
            transitionId: $"transition_{kind}_equal_turn")));
    }

    [Theory]
    [InlineData(
        "worsen",
        "transition_Exact",
        "transition_Exact",
        "wound_transition_duplicate_transition_id")]
    [InlineData(
        "worsen",
        "transition_Exact",
        "TRANSITION_EXACT",
        "wound_transition_confusable_transition_id")]
    [InlineData(
        "archive",
        "transition_A",
        "transition_А",
        "wound_transition_confusable_transition_id")]
    public void Reduce_RejectsImmediatePriorExactOrUnicodeConfusableTransitionId(
        string kind,
        string priorTransitionId,
        string transitionId,
        string expectedIssue)
    {
        var before = kind == "worsen" ? PhysicalWound() : HealedWound();
        before = before with
        {
            LastTransition = before.LastTransition with
            {
                TransitionId = priorTransitionId
            }
        };
        var request = kind == "worsen"
            ? WorsenRequest(before, before.LastTransition.Turn, transitionId)
            : PostTerminalAuditRequest(
                kind,
                before,
                turn: before.LastTransition.Turn,
                transitionId: transitionId);

        AssertInvalid(WoundTransitionReducer.Reduce(request), expectedIssue);
    }

    [Theory]
    [InlineData("worsen")]
    [InlineData("legacy")]
    [InlineData("archive")]
    public void Reduce_RejectsAppendAtExactPerWoundHistoryCapacityBoundary(string kind)
    {
        var before = kind == "worsen" ? PhysicalWound() : HealedWound();
        before = before with
        {
            LastTransition = before.LastTransition with
            {
                Ordinal = WoundHistoryState.MaxTransitions
            }
        };
        var request = kind == "worsen"
            ? WorsenRequest(
                before,
                before.LastTransition.Turn,
                "transition_capacity_exhausted")
            : PostTerminalAuditRequest(
                kind,
                before,
                turn: before.LastTransition.Turn,
                transitionId: $"transition_{kind}_capacity_exhausted");

        AssertInvalid(
            WoundTransitionReducer.Reduce(request),
            "wound_transition_history_capacity_exhausted");
    }

    [Fact]
    public void Reduce_Worsen_RequiresStrictIncreaseAndResetsCurrentStepRecovery()
    {
        var before = PhysicalWound() with
        {
            Recovery = PhysicalWound().Recovery with { CurrentStepProgress = 2 }
        };
        var after = NewTransition(
            WithSeverity(before, "III", 3) with
            {
                Recovery = before.Recovery with
                {
                    CurrentStepProgress = 0,
                    CurrentStepThreshold = 5
                }
            },
            "worsen");
        var evidence = WorsenEvidence(before, after, maximumSeverityRank: 3);

        var result = WoundTransitionReducer.Reduce(Request("worsen", before, after, evidence));

        AssertValid(result);
        AssertFreshRootIds(before, after);
        Assert.Equal(3, result.ProposedAfter!.Severity.Rank);
        Assert.Equal(0, result.ProposedAfter.Recovery.CurrentStepProgress);
        Assert.Contains(result.Intents, intent =>
            intent is WoundEffectTransitionIntent { Operation: "replace" });
    }

    [Theory]
    [InlineData("II", 2)]
    [InlineData("I", 1)]
    public void Reduce_Worsen_RejectsSameOrLowerSeverity(string severity, int rank)
    {
        var before = PhysicalWound();
        var after = NewTransition(WithSeverity(before, severity, rank), "worsen");

        var result = WoundTransitionReducer.Reduce(Request(
            "worsen",
            before,
            after,
            WorsenEvidence(before, after)));

        AssertInvalid(result, "wound_transition_worsen_severity_invalid");
    }

    [Fact]
    public void Reduce_Worsen_RejectsSeverityIVSourceCapAndPendingTreatmentOrRecovery()
    {
        var before = WithSeverity(PhysicalWound(), "IV", 4);
        var after = NewTransition(before, "worsen");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "worsen",
                before,
                after,
                WorsenEvidence(before, after))),
            "wound_transition_worsen_severity_invalid");

        before = PhysicalWound() with
        {
            Care = PhysicalWound().Care with
            {
                State = "recovering",
                ActiveCourseId = "course_active"
            }
        };
        after = NewTransition(
            WithSeverity(before, "III", 3) with
            {
                Recovery = before.Recovery with { CurrentStepProgress = 0 }
            },
            "worsen");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "worsen",
                before,
                after,
                WorsenEvidence(before, after, hasPendingTreatmentOrRecovery: true))),
            "wound_transition_pending_conflict");
    }

    [Fact]
    public void Reduce_Worsen_RejectsMissingTypedEvidenceAndUnresetProgress()
    {
        var before = PhysicalWound() with
        {
            Recovery = PhysicalWound().Recovery with { CurrentStepProgress = 2 }
        };
        var after = NewTransition(WithSeverity(before, "III", 3), "worsen");
        var wrongEvidence = new WoundArchiveEvidence(
            "terminal_authority",
            Fingerprint(before),
            Fingerprint(after));

        AssertInvalid(
            WoundTransitionReducer.Reduce(Request("worsen", before, after, wrongEvidence)),
            "wound_transition_evidence_kind_mismatch");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "worsen",
                before,
                after,
                WorsenEvidence(before, after))),
            "wound_transition_worsen_progress_not_reset");
    }

    [Fact]
    public void Reduce_Complicate_AddsOneExactComplicationOnceWithoutImplicitSeverityChange()
    {
        var before = PhysicalWound();
        var complication = Complication("complication_infection", "infection");
        var after = NewTransition(before with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray()
        }, "complicate");
        var evidence = ComplicateEvidence(
            before,
            after,
            complication.ComplicationId,
            allowsWorsening: false);

        var result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            evidence));

        AssertValid(result);
        Assert.Equal(2, result.ProposedAfter!.Severity.Rank);
        Assert.Contains(result.ProposedAfter.Complications, value =>
            value.ComplicationId == "complication_infection");
    }

    [Fact]
    public void Reduce_Complicate_AllowsOnlyExplicitBoundedWorseningAndResetsProgress()
    {
        var before = PhysicalWound() with
        {
            Recovery = PhysicalWound().Recovery with { CurrentStepProgress = 2 }
        };
        var complication = Complication("complication_rupture", "impairment");
        var after = NewTransition(WithSeverity(before, "III", 3) with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 0,
                CurrentStepThreshold = 5
            }
        }, "complicate");

        var denied = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId, allowsWorsening: false)));
        AssertInvalid(denied, "wound_transition_complicate_worsening_forbidden");

        var accepted = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId, allowsWorsening: true)));
        AssertValid(accepted);
        AssertFreshRootIds(before, after);
    }

    [Fact]
    public void Reduce_Complicate_RejectsDuplicateReplacementOrMissingCause()
    {
        var existing = Complication("complication_infection", "infection");
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(existing)
        };
        var duplicate = existing with { DisplayName = "Повтор" };
        var duplicateAfter = NewTransition(before with
        {
            Complications = before.Complications.Append(duplicate).ToImmutableArray()
        }, "complicate");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "complicate",
                before,
                duplicateAfter,
                ComplicateEvidence(before, duplicateAfter, existing.ComplicationId))),
            "wound_transition_complication_duplicate");

        var replacementAfter = NewTransition(before with
        {
            Complications = ImmutableArray.Create(
                Complication("complication_new", "pain"))
        }, "complicate");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "complicate",
                before,
                replacementAfter,
                ComplicateEvidence(before, replacementAfter, "complication_new"))),
            "wound_transition_complication_replaced");

        var added = NewTransition(before with
        {
            Complications = before.Complications.Append(
                Complication("complication_new", "pain")).ToImmutableArray()
        }, "complicate");
        var missingCause = ComplicateEvidence(before, added, "complication_new") with
        {
            CauseRef = " invalid"
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request("complicate", before, added, missingCause)),
            "wound_transition_evidence_invalid");
    }

    [Fact]
    public void Reduce_Complicate_ForgedDuplicateRetainedComplicationsReturnsEnvelopeInvalidWithoutThrowing()
    {
        var retained = Complication("complication_retained_duplicate", "infection");
        var beforeCandidate = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(retained)
        };
        var before = ParseWound(JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(beforeCandidate))!.AsObject());
        var added = Complication("complication_new_exact", "pain");
        var after = NewTransition(before with
        {
            Complications = ImmutableArray.Create(retained, retained, added)
        }, "complicate");
        WoundTransitionReductionResult? result = null;

        var exception = Record.Exception(() => result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, added.ComplicationId))));

        Assert.Null(exception);
        Assert.NotNull(result);
        Assert.Equal(
            "wound_transition_envelope_invalid",
            Assert.Single(result!.Issues).Code);
    }

    [Fact]
    public void Reduce_Complicate_PreservesCareWithoutWorseningAndWorsenRecoveryPolicy()
    {
        var before = PhysicalWound();
        var complication = Complication("complication_new", "pain");
        var careDrift = NewTransition(before with
        {
            Care = before.Care with { LastAttemptId = "attempt_smuggled" },
            Complications = before.Complications.Append(complication).ToImmutableArray()
        }, "complicate");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "complicate",
                before,
                careDrift,
                ComplicateEvidence(before, careDrift, complication.ComplicationId))),
            "wound_transition_complicate_care_changed");

        var worsening = NewTransition(WithSeverity(before, "III", 3) with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 0,
                LastTickKey = "tick_smuggled"
            }
        }, "complicate");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "complicate",
                before,
                worsening,
                ComplicateEvidence(
                    before,
                    worsening,
                    complication.ComplicationId,
                    allowsWorsening: true))),
            "wound_transition_complicate_recovery_policy_changed");

        var activeCourse = worsening with
        {
            Care = worsening.Care with { ActiveCourseId = "course_smuggled" },
            Recovery = before.Recovery with { CurrentStepProgress = 0 }
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "complicate",
                before,
                activeCourse,
                ComplicateEvidence(
                    before,
                    activeCourse,
                    complication.ComplicationId,
                    allowsWorsening: true))),
            "wound_transition_pending_conflict");
    }

    [Fact]
    public void Reduce_Complicate_RejectsConsequenceEffectTransferredToNewComplication()
    {
        var before = PhysicalWound();
        var transferred = before.Consequences.Entries[1];
        var complication = Complication(
            "complication_effect_transfer",
            "pain",
            transferred.EffectId);
        var after = NewTransition(before with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Consequences = before.Consequences with
            {
                SlotsUsed = 1,
                Entries = before.Consequences.Entries.Take(1).ToImmutableArray()
            }
        }, "complicate");

        var result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId)));

        AssertInvalid(result, "wound_transition_envelope_invalid");
    }

    [Fact]
    public void Reduce_Complicate_WithoutWorsening_PreservesEveryPriorEffectBinding()
    {
        var before = PhysicalWound();
        var removed = before.Consequences.Entries[1];
        var complication = Complication(
            "complication_declared_addition",
            "pain");
        var graphAfter = AddOwnedRoot(
            RemoveOwnedRoot(before, removed.EffectId),
            "effect_unrelated_replacement",
            "definition_unrelated_replacement",
            "action_control",
            addConsequenceSlot: true);
        var after = NewTransition(graphAfter with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray()
        }, "complicate");

        var result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId)));

        AssertInvalid(result, "wound_transition_effect_binding_changed");
    }

    [Theory]
    [InlineData(true, true, true, false)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, true, false, true)]
    public void Reduce_Complicate_WithoutWorsening_BindsOnlyReciprocalDeclaredEffects(
        bool complicationOwnsEffect,
        bool addReciprocalConsequence,
        bool addUnrelatedConsequence,
        bool expectedValid)
    {
        const string reciprocalEffectId = "effect_complication_reciprocal";
        var before = WithSeverity(PhysicalWound(), "IV", 4);
        var complication = Complication(
            "complication_reciprocal_gate",
            "pain",
            complicationOwnsEffect
                ? new[] { reciprocalEffectId }
                : Array.Empty<string>());
        var graphAfter = before;
        if (addReciprocalConsequence || complicationOwnsEffect)
        {
            graphAfter = AddOwnedRoot(
                graphAfter,
                reciprocalEffectId,
                "definition_complication_reciprocal",
                "characteristic_modifier",
                addConsequenceSlot: true);
            if (!addReciprocalConsequence)
            {
                graphAfter = graphAfter with
                {
                    Consequences = graphAfter.Consequences with
                    {
                        SlotsUsed = graphAfter.Consequences.SlotsUsed - 1,
                        Entries = graphAfter.Consequences.Entries
                            .Where(entry => !string.Equals(
                                entry.EffectId,
                                reciprocalEffectId,
                                StringComparison.Ordinal))
                            .ToImmutableArray()
                    }
                };
            }
        }
        if (addUnrelatedConsequence)
        {
            graphAfter = AddOwnedRoot(
                graphAfter,
                "effect_unrelated_extra",
                "definition_unrelated_extra",
                "resistance_modifier",
                addConsequenceSlot: true);
        }
        var after = NewTransition(graphAfter with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray()
        }, "complicate");

        var request = Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId));
        var result = WoundTransitionReducer.Reduce(request);

        if (expectedValid)
        {
            AssertValid(result);
            var effectIntent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
            Assert.Equal(EffectIds(before), effectIntent.BeforeEffectIds);
            Assert.Equal(EffectIds(after), effectIntent.AfterEffectIds);

            var replay = WoundTransitionReducer.Reduce(request);
            AssertValid(replay);
            var replayIntent = Assert.Single(replay.Intents.OfType<WoundEffectTransitionIntent>());
            Assert.Equal(effectIntent.BeforeEffectIds, replayIntent.BeforeEffectIds);
            Assert.Equal(effectIntent.AfterEffectIds, replayIntent.AfterEffectIds);
        }
        else
        {
            var expectedCode = complicationOwnsEffect &&
                               !addReciprocalConsequence &&
                               !addUnrelatedConsequence
                ? "wound_transition_envelope_invalid"
                : "wound_transition_complication_effect_binding_invalid";
            AssertInvalid(result, expectedCode);
        }
    }

    [Fact]
    public void Reduce_Complicate_WithWorsening_AllowsCompleteEffectRematerialization()
    {
        var before = PhysicalWound();
        var complication = Complication(
            "complication_worsening_rematerialization",
            "pain");
        var changed = WithSeverity(before, "III", 3);
        var rematerialized = changed.Consequences.Entries.ToArray();
        rematerialized[0] = rematerialized[0] with
        {
            ReadableSummary = "Явное ухудшение полностью перематериализовало эффект."
        };
        var after = NewTransition(changed with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Consequences = changed.Consequences with
            {
                Entries = rematerialized.ToImmutableArray()
            },
            Recovery = before.Recovery with { CurrentStepProgress = 0 }
        }, "complicate");

        var result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(
                before,
                after,
                complication.ComplicationId,
                allowsWorsening: true)));

        AssertValid(result);
        AssertFreshRootIds(before, after);
        Assert.Contains(result.Intents, intent =>
            intent is WoundEffectTransitionIntent { Operation: "replace" });
    }

    [Fact]
    public void Reduce_Diagnose_AppliesExactlyOneDeclaredPathIncludingComplicationReveal()
    {
        var complication = Complication("complication_infection", "infection") with
        {
            Visibility = "hidden"
        };
        var diagnosisPath = DiagnosisPath(
            "diagnosis_path_infection",
            "route:clean_and_suture",
            "complication:complication_infection");
        var seed = PhysicalWound();
        var before = seed with
        {
            Complications = ImmutableArray.Create(complication),
            Treatment = seed.Treatment with
            {
                DiagnosisPaths = ImmutableArray.Create(diagnosisPath),
                Routes = seed.Treatment.Routes
                    .Select(static route => route with { Visibility = "hidden" })
                    .ToImmutableArray(),
                KnownRouteIds = ImmutableArray<string>.Empty
            }
        };
        var after = NewTransition(before with
        {
            Complications = ImmutableArray.Create(
                complication with { Visibility = "known_to_player" }),
            Treatment = before.Treatment with
            {
                KnownRouteIds = ImmutableArray.Create("clean_and_suture")
            }
        }, "diagnose");
        var request = DiagnoseRequest(
            before,
            after,
            diagnosisPath.DiagnosisPathId);

        var result = WoundTransitionReducer.Reduce(request);

        AssertValid(result);
        Assert.Equal(before.Severity, result.ProposedAfter!.Severity);
        Assert.Equal(before.Care, result.ProposedAfter.Care);
        Assert.Equal(
            "known_to_player",
            Assert.Single(result.ProposedAfter.Complications).Visibility);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
    }

    [Fact]
    public void Reduce_Diagnose_RejectsUndeclaredPrivateFactDisplayLeakAndMechanicalRegression()
    {
        var declared = Complication("complication_declared", "infection") with
        {
            Visibility = "hidden"
        };
        var privateFact = Complication("complication_private", "other") with
        {
            Visibility = "gm_only"
        };
        var diagnosisPath = DiagnosisPath(
            "diagnosis_path_declared",
            "route:clean_and_suture",
            "complication:complication_declared");
        var seed = PhysicalWound();
        var before = seed with
        {
            Complications = ImmutableArray.Create(declared, privateFact),
            Treatment = seed.Treatment with
            {
                DiagnosisPaths = ImmutableArray.Create(diagnosisPath),
                Routes = seed.Treatment.Routes
                    .Select(static route => route with { Visibility = "hidden" })
                    .ToImmutableArray(),
                KnownRouteIds = ImmutableArray<string>.Empty
            }
        };
        var undeclaredAfter = NewTransition(before with
        {
            Complications = ImmutableArray.Create(
                declared with { Visibility = "known_to_player" },
                privateFact with { Visibility = "known_to_player" }),
            Treatment = before.Treatment with
            {
                KnownRouteIds = ImmutableArray.Create("clean_and_suture")
            }
        }, "diagnose");
        AssertInvalid(
            WoundTransitionReducer.Reduce(DiagnoseRequest(
                before,
                undeclaredAfter,
                diagnosisPath.DiagnosisPathId)),
            "wound_transition_diagnosis_fact_unauthorized");

        var displayLeak = NewTransition(before with
        {
            Complications = ImmutableArray.Create(
                declared with { Visibility = "known_to_player" },
                privateFact),
            Display = before.Display with
            {
                Prognosis = "Приватный прогноз не был объявлен путём диагностики."
            },
            Treatment = before.Treatment with
            {
                KnownRouteIds = ImmutableArray.Create("clean_and_suture")
            }
        }, "diagnose");
        AssertInvalid(
            WoundTransitionReducer.Reduce(DiagnoseRequest(
                before,
                displayLeak,
                diagnosisPath.DiagnosisPathId)),
            "wound_transition_diagnosis_display_changed");

        var healedMechanics = NewTransition(WithSeverity(before, "I", 1), "diagnose");
        AssertInvalid(
            WoundTransitionReducer.Reduce(DiagnoseRequest(
                before,
                healedMechanics,
                diagnosisPath.DiagnosisPathId)),
            "wound_transition_diagnosis_mechanics_changed");
    }

    [Fact]
    public void Reduce_Stabilize_RemovesOnlyDeclaredComplicationEffectAndRecoveryBlocker()
    {
        const string complicationEffectId = "effect_bleeding_complication";
        var complication = Complication(
            "complication_bleeding",
            "bleeding",
            complicationEffectId);
        var beforeWithRoot = AddOwnedRoot(
            WithSeverity(PhysicalWound(), "III", 3),
            complicationEffectId,
            "definition_bleeding_complication",
            "characteristic_modifier",
            addConsequenceSlot: true);
        var before = beforeWithRoot with
        {
            Complications = ImmutableArray.Create(complication),
            Recovery = beforeWithRoot.Recovery with
            {
                Blockers = ImmutableArray.Create("not_stabilized", "unsafe_environment")
            }
        };
        var removedRoot = RemoveOwnedRoot(
            before with { Complications = ImmutableArray<WoundComplication>.Empty },
            complicationEffectId);
        var after = NewTransition(removedRoot with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Complications = ImmutableArray<WoundComplication>.Empty,
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray.Create("unsafe_environment")
            }
        }, "stabilize");
        var evidence = StabilizeEvidence(
                before,
                after,
                removedComplications: new[] { complication.ComplicationId },
                removedEffects: new[] { complicationEffectId },
                removedBlockers: new[] { "not_stabilized" });

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            evidence));

        AssertValid(result);
        Assert.Equal("stabilized", result.ProposedAfter!.Care.State);
        Assert.Equal(3, result.ProposedAfter.Severity.Rank);
        Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
    }

    [Fact]
    public void Reduce_Stabilize_RejectsImplicitSeverityReductionOrUndeclaredRemoval()
    {
        var complication = Complication("complication_bleeding", "bleeding");
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(complication)
        };
        var reduced = NewTransition(WithSeverity(before, "I", 1) with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            }
        }, "stabilize");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "stabilize",
                before,
                reduced,
                StabilizeEvidence(before, reduced))),
            "wound_transition_stabilize_severity_changed");

        var removed = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Complications = ImmutableArray<WoundComplication>.Empty
        }, "stabilize");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "stabilize",
                before,
                removed,
                StabilizeEvidence(before, removed))),
            "wound_transition_stabilize_removal_undeclared");
    }

    [Fact]
    public void Reduce_Stabilize_RejectsRetainedConsequencePayloadDriftAtSameEffectId()
    {
        var before = PhysicalWound();
        var entries = before.Consequences.Entries.ToArray();
        entries[0] = entries[0] with
        {
            ReadableSummary = "Под тем же ID подменён механический профиль."
        };
        var after = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Consequences = before.Consequences with
            {
                Entries = entries.ToImmutableArray()
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        AssertInvalid(result, "wound_transition_retained_consequence_changed");
    }

    [Fact]
    public void Reduce_Stabilize_AcceptsOneRootOwningMultipleConsequenceSlots()
    {
        var before = ParseWound(CreateCollapsedRootWound());
        var after = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        AssertValid(result);
        Assert.Equal(
            new[] { "effect_wound_test_bleeding", "effect_wound_test_bleeding" },
            result.ProposedAfter!.Consequences.Entries
                .Select(static entry => entry.EffectId));
    }

    [Fact]
    public void Reduce_Stabilize_RejectsRetainedOwnedDefinitionGraphDrift()
    {
        var beforeJson = WoundContractTestData.CreateActiveWound();
        var before = ParseWound(beforeJson);
        var afterJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        afterJson["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] =
            "Под тем же effectId подменено исходное определение.";
        PrepareStabilized(afterJson);
        var after = ParseWound(afterJson);

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        AssertInvalid(result, "wound_transition_owned_source_graph_changed");
    }

    [Fact]
    public void Reduce_Stabilize_RejectsCanonicalNumericLexemeDriftInRetainedDefinition()
    {
        const string definitionKey = "definition_numeric_lexeme_root";
        const string canonicalBound = "\"minimum\":-100";
        const string driftedBound = "\"minimum\":-100.0";
        var seed = AddOwnedRoot(
            WithSeverity(PhysicalWound(), "III", 3),
            "effect_numeric_lexeme_root",
            definitionKey,
            "resistance_modifier",
            addConsequenceSlot: true);
        var canonicalBefore = WoundMaterializationContract.SerializeCanonical(seed);
        Assert.Contains(canonicalBound, canonicalBefore, StringComparison.Ordinal);
        var canonicalAfter = canonicalBefore.Replace(
            canonicalBound,
            driftedBound,
            StringComparison.Ordinal);
        Assert.NotEqual(canonicalBefore, canonicalAfter);
        var before = ParseWound(JsonNode.Parse(canonicalBefore)!.AsObject());
        var drifted = ParseWound(JsonNode.Parse(canonicalAfter)!.AsObject());
        var after = NewTransition(drifted with
        {
            Care = drifted.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Recovery = drifted.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            }
        }, "stabilize");
        var beforeDefinition = before.Consequences.OwnedEffectSources.Definitions
            .Single(definition => definition.GetProperty("definitionKey").GetString() ==
                                  definitionKey);
        var afterDefinition = after.Consequences.OwnedEffectSources.Definitions
            .Single(definition => definition.GetProperty("definitionKey").GetString() ==
                                  definitionKey);
        Assert.NotEqual(beforeDefinition.GetRawText(), afterDefinition.GetRawText());

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        Assert.Equal(
            "wound_transition_owned_source_graph_changed",
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Reduce_Stabilize_OwnedGraphChangePrecedesRetainedPayloadDrift()
    {
        var before = PhysicalWound();
        var afterJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        afterJson["consequences"]!["ownedEffectSources"]!["definitions"]![0]!["display"]!["description"] =
            "Изменён канонический граф вместе с отображаемым следствием.";
        afterJson["consequences"]!["entries"]![0]!["readableSummary"] =
            "Одновременно изменено следствие.";
        PrepareStabilized(afterJson);
        var after = ParseWound(afterJson);

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        Assert.Equal(
            "wound_transition_owned_source_graph_changed",
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Reduce_Stabilize_AddedRootPrecedesRetainedPayloadDrift()
    {
        var before = WithSeverity(PhysicalWound(), "III", 3);
        var graphAfter = AddOwnedRoot(
            before,
            "effect_added_during_removal_only",
            "definition_added_during_removal_only",
            "resistance_modifier",
            addConsequenceSlot: true);
        var entries = graphAfter.Consequences.Entries.ToArray();
        entries[0] = entries[0] with
        {
            ReadableSummary = "Одновременно изменено удержанное следствие."
        };
        var after = NewTransition(graphAfter with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            },
            Consequences = graphAfter.Consequences with
            {
                Entries = entries.ToImmutableArray()
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        Assert.Equal(
            "wound_transition_owned_source_graph_invalid",
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void Reduce_OverBoundRawOwnedSourceRemainsEnvelopeInvalid()
    {
        var before = PhysicalWound();
        var definitions = before.Consequences.OwnedEffectSources.Definitions.ToList();
        var bindings = before.Consequences.OwnedEffectSources.RootBindings.ToList();
        for (var index = 0; index < 4; index++)
        {
            var definitionKey = $"definition_over_bound_marker_{index}";
            definitions.Add(JsonSerializer.Deserialize<JsonElement>(
                WoundContractTestData.CreateOwnedEffectDefinition(
                    before.WoundId,
                    before.Owner.Realm,
                    definitionKey,
                    "wound_consequence")
                .ToJsonString()));
            bindings.Add(new WoundRootEffectBinding(
                $"effect_over_bound_marker_{index}",
                definitionKey));
        }
        Assert.Equal(6, definitions.Count);
        Assert.Equal(6, bindings.Count);

        var entries = before.Consequences.Entries.ToArray();
        entries[0] = entries[0] with
        {
            ReadableSummary = "Семантическая ошибка не должна обгонять лимит конверта."
        };
        var after = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            },
            Consequences = before.Consequences with
            {
                Entries = entries.ToImmutableArray(),
                OwnedEffectSources = new WoundOwnedEffectSources(
                    definitions.ToImmutableArray(),
                    bindings.ToImmutableArray())
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        Assert.Equal(
            "wound_transition_envelope_invalid",
            Assert.Single(result.Issues).Code);
    }

    [Fact]
    public void WoundTransitionReducer_ConsumesParserOwnedDefinitionFacts()
    {
        var repositoryRoot = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", ".."));
        var source = File.ReadAllText(System.IO.Path.Combine(
            repositoryRoot,
            "BookOfEternityClient",
            "Services",
            "WoundTransitionReducer.cs"));

        Assert.Contains("OwnedEffectSources.DefinitionFacts", source, StringComparison.Ordinal);
        Assert.Contains(
            "ValidateRetainedRemovedComplicationRootPreflight",
            source,
            StringComparison.Ordinal);
        Assert.DoesNotContain("RawOwnedSourceTransitionView", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ValidateRawOwnedSourceTransitionPreflight", source, StringComparison.Ordinal);
        Assert.DoesNotContain("RawEarlierTransitionGatesPass", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadApplyDefinitionTargets", source, StringComparison.Ordinal);
        Assert.DoesNotContain("DefinitionContainsProfile", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ReadDefinitionKey(JsonElement", source, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("complicate", false)]
    [InlineData("complicate", true)]
    [InlineData("treat", false)]
    [InlineData("treat", true)]
    [InlineData("recover", false)]
    [InlineData("recover", true)]
    public void Reduce_RankChangingPathsRejectExactOrConfusablePriorRootReuse(
        string kind,
        bool confusable)
    {
        var before = kind == "complicate"
            ? PhysicalWound()
            : WithSeverity(PhysicalWound(), "IV", 4);
        var changed = WithSeverity(before, "III", 3);
        var priorEffectId = Assert.Single(EffectIds(before).Take(1));
        changed = RebindFirstRootToPriorIdentity(changed, priorEffectId, confusable);

        WoundTransitionEvidence evidence;
        WoundMaterializationEnvelope after;
        if (kind == "complicate")
        {
            var complication = Complication("complication_rank_change", "impairment");
            after = NewTransition(changed with
            {
                Complications = before.Complications.Append(complication).ToImmutableArray()
            }, kind);
            evidence = ComplicateEvidence(
                before,
                after,
                complication.ComplicationId,
                allowsWorsening: true);
        }
        else if (kind == "treat")
        {
            after = NewTransition(changed with
            {
                Care = before.Care with { LastAttemptId = "attempt_treat" }
            }, kind);
            evidence = TreatEvidence(
                before,
                after,
                Outcome(after, terminalAttempt: true));
        }
        else
        {
            after = NewTransition(changed with
            {
                Recovery = before.Recovery with { LastTickKey = "tick_fresh" }
            }, kind);
            evidence = RecoverEvidence(before, after, Outcome(after));
        }

        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(kind, before, after, evidence)),
            "wound_transition_severity_root_identity_reused");
    }

    [Fact]
    public void Reduce_Complicate_DirectZeroSlotMarkerRootEmitsUpdateIntent()
    {
        const string effectId = "effect_complication_direct_marker";
        var before = WithSeverity(PhysicalWound(), "III", 3);
        var withMarker = AddOwnedRoot(
            before,
            effectId,
            "definition_complication_direct_marker",
            "wound_consequence",
            addConsequenceSlot: false);
        var complication = Complication(
            "complication_direct_marker",
            "impairment",
            effectId);
        var after = NewTransition(withMarker with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray()
        }, "complicate");

        var result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId)));

        AssertValid(result);
        var intent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal("update", intent.Operation);
        Assert.DoesNotContain(effectId, intent.BeforeEffectIds);
        Assert.Contains(effectId, intent.AfterEffectIds);
    }

    [Fact]
    public void Reduce_Create_EmptyOwnedGraphEmitsNoEffectIntent()
    {
        var after = NewTransition(
            EmptyPhysicalWound("I", 1, "I"),
            "create",
            ordinal: 1,
            turn: 42);

        var result = WoundTransitionReducer.Reduce(Request(
            "create",
            null,
            after,
            CreateEvidence(after)));

        AssertValid(result);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
    }

    [Fact]
    public void Reduce_Worsen_EmptyToEmptySeverityChangeEmitsNoEffectIntent()
    {
        var before = EmptyPhysicalWound("I", 1, "II");
        var after = NewTransition(WithSeverity(before, "II", 2), "worsen");

        var result = WoundTransitionReducer.Reduce(Request(
            "worsen",
            before,
            after,
            WorsenEvidence(before, after, maximumSeverityRank: 2)));

        AssertValid(result);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
    }

    [Fact]
    public void Reduce_Treat_RankReductionAndHealFollowUpEmitsExactIntentOrder()
    {
        var before = PhysicalWound();
        var after = NewTransition(WithSeverity(before, "I", 1) with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");

        var result = WoundTransitionReducer.Reduce(Request(
            "treat",
            before,
            after,
            TreatEvidence(
                before,
                after,
                Outcome(after, heals: true, terminalAttempt: true))));

        AssertValid(result);
        Assert.Equal(
            new[]
            {
                typeof(WoundCarrierTransitionIntent),
                typeof(WoundEffectTransitionIntent),
                typeof(WoundAttemptTerminalIntent),
                typeof(WoundFollowUpHealIntent),
                typeof(WoundTransitionHistoryIntent)
            },
            result.Intents.Select(static intent => intent.GetType()).ToArray());
        Assert.Equal(
            "replace",
            Assert.IsType<WoundEffectTransitionIntent>(result.Intents[1]).Operation);
    }

    [Fact]
    public void Reduce_Stabilize_RejectsRetainedRootBindingIdentityDrift()
    {
        var before = PhysicalWound();
        var afterJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        afterJson["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"] =
            "effect_wound_rebound";
        afterJson["consequences"]!["entries"]![0]!["effectId"] =
            "effect_wound_rebound";
        PrepareStabilized(afterJson);
        var after = ParseWound(afterJson);

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedBlockers: new[] { "not_stabilized" })));

        AssertInvalid(result, "wound_transition_effect_binding_changed");
    }

    [Fact]
    public void Reduce_Stabilize_RemovesDeclaredRootsSlotsAndUnreachableDefinitionsTogether()
    {
        var beforeJson = WoundContractTestData.CreateActiveWound();
        beforeJson["complications"]!.AsArray().Add(new JsonObject
        {
            ["complicationId"] = "complication_pain",
            ["kind"] = "pain",
            ["state"] = "active",
            ["displayName"] = "Болевое осложнение",
            ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray("effect_wound_test_pain"),
            ["visibility"] = "known_to_player"
        });
        var before = ParseWound(beforeJson);

        var afterJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        afterJson["complications"] = new JsonArray();
        afterJson["consequences"]!["entries"]!.AsArray().RemoveAt(1);
        afterJson["consequences"]!["slotsUsed"] = 1;
        afterJson["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray().RemoveAt(1);
        afterJson["consequences"]!["ownedEffectSources"]!["definitions"]!
            .AsArray().RemoveAt(1);
        PrepareStabilized(afterJson);
        var after = ParseWound(afterJson);

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedComplications: new[] { "complication_pain" },
                removedEffects: new[] { "effect_wound_test_pain" },
                removedBlockers: new[] { "not_stabilized" })));

        AssertValid(result);
        var canonical = WoundMaterializationContract.SerializeCanonical(result.ProposedAfter!);
        Assert.DoesNotContain("effect_wound_test_pain", canonical, StringComparison.Ordinal);
        Assert.DoesNotContain("definition_wound_test_pain", canonical, StringComparison.Ordinal);
        Assert.Equal(1, result.ProposedAfter!.Consequences.SlotsUsed);
        var effectIntent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal(EffectIds(before), effectIntent.BeforeEffectIds);
        Assert.Equal(EffectIds(after), effectIntent.AfterEffectIds);
        Assert.Equal(
            Fingerprint(result.ProposedAfter!),
            Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).AfterFingerprint);
    }

    [Fact]
    public void Reduce_Stabilize_RemovesFirstSlotAndRootAndRenumbersRetainedGraph()
    {
        const string removedComplicationId = "complication_bleeding";
        const string removedEffectId = "effect_wound_test_bleeding";
        const string removedDefinitionKey = "definition_wound_test_bleeding";
        const string retainedEffectId = "effect_wound_test_pain";
        const string retainedDefinitionKey = "definition_wound_test_pain";
        var beforeJson = WoundContractTestData.CreateActiveWound();
        beforeJson["complications"]!.AsArray().Add(new JsonObject
        {
            ["complicationId"] = removedComplicationId,
            ["kind"] = "bleeding",
            ["state"] = "active",
            ["displayName"] = "Кровоточащее осложнение",
            ["treatmentDifficultyModifier"] = 1,
            ["ownedEffectIds"] = new JsonArray(removedEffectId),
            ["visibility"] = "known_to_player"
        });
        var before = ParseWound(beforeJson);
        var pruned = RemoveOwnedRoot(
            before with { Complications = ImmutableArray<WoundComplication>.Empty },
            removedEffectId);
        var after = NewTransition(pruned with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedComplications: new[] { removedComplicationId },
                removedEffects: new[] { removedEffectId },
                removedBlockers: new[] { "not_stabilized" })));

        AssertValid(result);
        var proposedAfter = result.ProposedAfter!;
        Assert.Equal(Fingerprint(after), Fingerprint(proposedAfter));
        Assert.Equal(1, proposedAfter.Consequences.SlotsUsed);
        var retainedEntry = Assert.Single(proposedAfter.Consequences.Entries);
        Assert.Equal(1, retainedEntry.Slot);
        Assert.Equal(retainedEffectId, retainedEntry.EffectId);
        var retainedBinding = Assert.Single(
            proposedAfter.Consequences.OwnedEffectSources.RootBindings);
        Assert.Equal(retainedEffectId, retainedBinding.EffectId);
        Assert.Equal(retainedDefinitionKey, retainedBinding.DefinitionKey);

        var beforeSources = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!
            ["consequences"]!["ownedEffectSources"]!;
        var proposedSources = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(proposedAfter))!
            ["consequences"]!["ownedEffectSources"]!;
        var expectedRetainedDefinition = beforeSources["definitions"]!.AsArray()
            .Single(definition => string.Equals(
                definition!["definitionKey"]!.GetValue<string>(),
                retainedDefinitionKey,
                StringComparison.Ordinal));
        var actualRetainedDefinition = Assert.Single(
            proposedSources["definitions"]!.AsArray());
        Assert.Equal(
            expectedRetainedDefinition!.ToJsonString(),
            actualRetainedDefinition!.ToJsonString());
        var canonical = WoundMaterializationContract.SerializeCanonical(proposedAfter);
        Assert.DoesNotContain(removedEffectId, canonical, StringComparison.Ordinal);
        Assert.DoesNotContain(removedDefinitionKey, canonical, StringComparison.Ordinal);

        var intent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal("update", intent.Operation);
        Assert.Equal(EffectIds(before), intent.BeforeEffectIds);
        Assert.Equal(new[] { retainedEffectId }, intent.AfterEffectIds);
    }

    [Fact]
    public void Reduce_Stabilize_PrunesRemovedBranchAndPreservesReachableSibling()
    {
        const string removedEffectId = "effect_complication_removed_branch";
        const string retainedEffectId = "effect_complication_retained_branch";
        const string removedDefinitionKey = "definition_complication_removed_branch";
        const string retainedDefinitionKey = "definition_complication_retained_branch";
        var withRemoved = AddOwnedRoot(
            WithSeverity(PhysicalWound(), "IV", 4),
            removedEffectId,
            removedDefinitionKey,
            "characteristic_modifier",
            addConsequenceSlot: true);
        var withBoth = AddOwnedRoot(
            withRemoved,
            retainedEffectId,
            retainedDefinitionKey,
            "resistance_modifier",
            addConsequenceSlot: true);
        var removedComplication = Complication(
            "complication_removed_branch",
            "pain",
            removedEffectId);
        var retainedComplication = Complication(
            "complication_retained_branch",
            "impairment",
            retainedEffectId);
        var before = withBoth with
        {
            Complications = ImmutableArray.Create(removedComplication, retainedComplication)
        };
        var afterWithoutRoot = RemoveOwnedRoot(
            before with { Complications = ImmutableArray.Create(retainedComplication) },
            removedEffectId);
        var after = NewTransition(afterWithoutRoot with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedComplications: new[] { removedComplication.ComplicationId },
                removedEffects: new[] { removedEffectId },
                removedBlockers: new[] { "not_stabilized" })));

        AssertValid(result);
        var canonical = WoundMaterializationContract.SerializeCanonical(result.ProposedAfter!);
        Assert.DoesNotContain(removedEffectId, canonical, StringComparison.Ordinal);
        Assert.DoesNotContain(removedDefinitionKey, canonical, StringComparison.Ordinal);
        Assert.Contains(retainedEffectId, canonical, StringComparison.Ordinal);
        Assert.Contains(retainedDefinitionKey, canonical, StringComparison.Ordinal);
        Assert.Equal(3, result.ProposedAfter!.Consequences.SlotsUsed);
        var intent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal(EffectIds(before), intent.BeforeEffectIds);
        Assert.Equal(EffectIds(after), intent.AfterEffectIds);
    }

    [Fact]
    public void Reduce_Stabilize_RejectsRemovedComplicationWithRetainedUnreachableRoot()
    {
        var request = RetainedUnreachableRootStabilizationRequest();
        var result = WoundTransitionReducer.Reduce(request);

        AssertInvalid(result, "wound_transition_owned_source_graph_invalid");
    }

    [Theory]
    [InlineData("binding", "wound_transition_effect_binding_changed")]
    [InlineData("canonical_definition", "wound_transition_owned_source_graph_changed")]
    public void Reduce_Stabilize_NarrowRawGraphCheckPreservesOwnedSourcePrecedence(
        string mutation,
        string expectedIssue)
    {
        const string effectId = "effect_wound_test_pain";
        var request = RetainedUnreachableRootStabilizationRequest();
        var before = request.Before!;
        var rawAfter = RetainedUnreachableRootAfterWithSourceMutation(request, mutation);
        var evidence = StabilizeEvidence(
            before,
            rawAfter,
            removedComplications: new[] { "complication_pain" },
            removedEffects: new[] { effectId },
            removedBlockers: new[] { "not_stabilized" });

        var result = WoundTransitionReducer.Reduce(request with
        {
            ProposedAfter = rawAfter,
            Evidence = evidence
        });

        Assert.Equal(expectedIssue, Assert.Single(result.Issues).Code);
    }

    [Theory]
    [InlineData("turn", "wound_transition_turn_regression")]
    [InlineData("seal", "wound_transition_before_fingerprint_mismatch")]
    [InlineData("after_seal", "wound_transition_after_fingerprint_mismatch")]
    [InlineData("metadata", "wound_transition_metadata_mismatch")]
    [InlineData("severity", "wound_transition_stabilize_severity_changed")]
    [InlineData("care", "wound_transition_stabilize_state_invalid")]
    public void Reduce_Stabilize_EarlierAndLegalityGatesPrecedeRawOwnedSourceDrift(
        string mutation,
        string expectedIssue)
    {
        const string effectId = "effect_wound_test_pain";
        var request = RetainedUnreachableRootStabilizationRequest();
        var before = request.Before!;
        var rawAfter = RetainedUnreachableRootAfterWithSourceMutation(
            request,
            "canonical_definition");
        rawAfter = mutation switch
        {
            "severity" => rawAfter with
            {
                Severity = rawAfter.Severity with
                {
                    Value = "I",
                    Rank = 1,
                    LastChangeEventRef = request.EventRef
                },
                Consequences = rawAfter.Consequences with { SlotBudget = 1 }
            },
            "care" => rawAfter with { Care = before.Care },
            _ => rawAfter
        };
        var evidence = StabilizeEvidence(
            before,
            rawAfter,
            removedComplications: new[] { "complication_pain" },
            removedEffects: new[] { effectId },
            removedBlockers: new[] { "not_stabilized" });
        request = request with
        {
            ProposedAfter = rawAfter,
            Evidence = evidence
        };
        request = mutation switch
        {
            "turn" => request with { Turn = before.LastTransition.Turn - 1 },
            "seal" => request with
            {
                Evidence = evidence with { ExpectedBeforeFingerprint = ValidFingerprint('e') }
            },
            "after_seal" => request with
            {
                Evidence = evidence with { ExpectedAfterFingerprint = ValidFingerprint('f') }
            },
            "metadata" => request with { TransitionId = "wound_transition_metadata_other" },
            "severity" or "care" => request,
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };

        AssertInvalid(WoundTransitionReducer.Reduce(request), expectedIssue);
    }

    [Theory]
    [InlineData("turn", "wound_transition_turn_regression")]
    [InlineData("seal", "wound_transition_before_fingerprint_mismatch")]
    [InlineData("after_seal", "wound_transition_after_fingerprint_mismatch")]
    [InlineData("metadata", "wound_transition_metadata_mismatch")]
    public void Reduce_Stabilize_NarrowRawGraphCheckPreservesEarlierGatePrecedence(
        string mutation,
        string expectedIssue)
    {
        var request = RetainedUnreachableRootStabilizationRequest();
        request = mutation switch
        {
            "turn" => request with
            {
                Turn = request.Before!.LastTransition.Turn - 1
            },
            "seal" => request with
            {
                Evidence = Assert.IsType<WoundStabilizationEvidence>(request.Evidence) with
                {
                    ExpectedBeforeFingerprint = ValidFingerprint('e')
                }
            },
            "after_seal" => request with
            {
                Evidence = Assert.IsType<WoundStabilizationEvidence>(request.Evidence) with
                {
                    ExpectedAfterFingerprint = ValidFingerprint('f')
                }
            },
            "metadata" => request with
            {
                TransitionId = "wound_transition_metadata_other"
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };

        AssertInvalid(WoundTransitionReducer.Reduce(request), expectedIssue);
    }

    [Theory]
    [InlineData("severity", "wound_transition_stabilize_severity_changed")]
    [InlineData("care", "wound_transition_stabilize_state_invalid")]
    public void Reduce_Stabilize_NarrowRawGraphCheckPreservesStabilizeLegality(
        string mutation,
        string expectedIssue)
    {
        var request = RetainedUnreachableRootStabilizationRequest();
        var before = request.Before!;
        var after = mutation switch
        {
            "severity" => request.ProposedAfter! with
            {
                Severity = request.ProposedAfter!.Severity with
                {
                    Value = "I",
                    Rank = 1,
                    LastChangeEventRef = request.EventRef
                },
                Consequences = request.ProposedAfter.Consequences with { SlotBudget = 1 }
            },
            "care" => request.ProposedAfter! with
            {
                Care = before.Care
            },
            _ => throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null)
        };
        var evidence = StabilizeEvidence(
            before,
            after,
            removedComplications: new[] { "complication_pain" },
            removedEffects: new[] { "effect_wound_test_pain" },
            removedBlockers: new[] { "not_stabilized" });

        AssertInvalid(
            WoundTransitionReducer.Reduce(request with
            {
                ProposedAfter = after,
                Evidence = evidence
            }),
            expectedIssue);
    }

    [Fact]
    public void Reduce_Stabilize_NarrowRawGraphCheckDoesNotRemapMissingDefinitionEnvelope()
    {
        var request = RetainedUnreachableRootStabilizationRequest();
        var before = request.Before!;
        var after = request.ProposedAfter!;
        var sources = after.Consequences.OwnedEffectSources;
        var definitions = sources.Definitions.Where(definition => !string.Equals(
                definition.GetProperty("definitionKey").GetString(),
                "definition_wound_test_pain",
                StringComparison.Ordinal))
            .ToImmutableArray();
        var malformedAfter = after with
        {
            Consequences = after.Consequences with
            {
                OwnedEffectSources = sources with { Definitions = definitions }
            }
        };
        var evidence = StabilizeEvidence(
            before,
            malformedAfter,
            removedComplications: new[] { "complication_pain" },
            removedEffects: new[] { "effect_wound_test_pain" },
            removedBlockers: new[] { "not_stabilized" });

        AssertInvalid(
            WoundTransitionReducer.Reduce(request with
            {
                ProposedAfter = malformedAfter,
                Evidence = evidence
            }),
            "wound_transition_envelope_invalid");
    }

    [Fact]
    public void Reduce_Stabilize_NarrowRawGraphCheckDoesNotRemapUnrelatedMalformedEnvelope()
    {
        var request = RetainedUnreachableRootStabilizationRequest();
        var malformedAfter = request.ProposedAfter! with
        {
            Display = request.ProposedAfter!.Display with { VisibleSymptoms = null! }
        };

        AssertInvalid(
            WoundTransitionReducer.Reduce(request with { ProposedAfter = malformedAfter }),
            "wound_transition_envelope_invalid");
    }

    [Theory]
    [InlineData("null_root_bindings")]
    [InlineData("null_definition_facts")]
    [InlineData("null_owned_effect_ids")]
    [InlineData("over_bound_owned_effect_ids")]
    public void Reduce_Stabilize_NarrowRawGraphCheckIsNullSafeAndBounded(string mutation)
    {
        var request = RetainedUnreachableRootStabilizationRequest();
        var before = request.Before!;
        var after = request.ProposedAfter!;
        switch (mutation)
        {
            case "null_root_bindings":
                after = after with
                {
                    Consequences = after.Consequences with
                    {
                        OwnedEffectSources = new WoundOwnedEffectSources(
                            after.Consequences.OwnedEffectSources.Definitions,
                            null!)
                        {
                            DefinitionFacts = after.Consequences.OwnedEffectSources.DefinitionFacts
                        }
                    }
                };
                break;
            case "null_definition_facts":
                before = before with
                {
                    Consequences = before.Consequences with
                    {
                        OwnedEffectSources = before.Consequences.OwnedEffectSources with
                        {
                            DefinitionFacts = null!
                        }
                    }
                };
                break;
            case "null_owned_effect_ids":
                before = before with
                {
                    Complications = ImmutableArray.Create(
                        before.Complications[0] with { OwnedEffectIds = null! })
                };
                break;
            case "over_bound_owned_effect_ids":
                before = before with
                {
                    Complications = ImmutableArray.Create(before.Complications[0] with
                    {
                        OwnedEffectIds = new[]
                        {
                            "effect_wound_test_pain",
                            "effect_forged_owned_1",
                            "effect_forged_owned_2",
                            "effect_forged_owned_3",
                            "effect_forged_owned_4",
                            "effect_forged_owned_5"
                        }
                    })
                };
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        WoundTransitionReductionResult? result = null;
        var exception = Record.Exception(() => result = WoundTransitionReducer.Reduce(request with
        {
            Before = before,
            ProposedAfter = after
        }));

        Assert.Null(exception);
        AssertInvalid(result!, "wound_transition_envelope_invalid");
    }

    [Fact]
    public void Reduce_Worsen_RequiresFreshCompleteRootIdentitySet()
    {
        var before = PhysicalWound();
        var freshAfter = ParseWound(CreateSeverityThreeWound(retainPriorRootIds: false));
        AssertValid(WoundTransitionReducer.Reduce(Request(
            "worsen",
            before,
            freshAfter,
            WorsenEvidence(before, freshAfter))));

        var retainedAfter = ParseWound(CreateSeverityThreeWound(retainPriorRootIds: true));
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "worsen",
                before,
                retainedAfter,
                WorsenEvidence(before, retainedAfter))),
            "wound_transition_severity_root_identity_reused");
    }

    [Fact]
    public void Reduce_Worsen_RejectsRootIdentityConfusableWithPriorRoot()
    {
        const string priorEffectId = "effect_wound_test_bleeding";
        const string confusableEffectId = "EFFECT_WOUND_TEST_BLEEDING";
        var before = PhysicalWound();
        var afterJson = CreateSeverityThreeWound(retainPriorRootIds: false);
        afterJson["consequences"]!["ownedEffectSources"]!["rootBindings"]![0]!["effectId"] =
            confusableEffectId;
        afterJson["consequences"]!["entries"]![0]!["effectId"] = confusableEffectId;
        var after = ParseWound(afterJson);

        var beforeRootIds = before.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .ToArray();
        var afterRootIds = after.Consequences.OwnedEffectSources.RootBindings
            .Select(static binding => binding.EffectId)
            .ToArray();
        Assert.Equal(beforeRootIds.Length, beforeRootIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(afterRootIds.Length, afterRootIds.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(
            beforeRootIds.Length,
            beforeRootIds.Select(MortalLocationIdentityState.BuildConfusableKey)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Equal(
            afterRootIds.Length,
            afterRootIds.Select(MortalLocationIdentityState.BuildConfusableKey)
                .Distinct(StringComparer.Ordinal)
                .Count());
        Assert.Empty(beforeRootIds.Intersect(afterRootIds, StringComparer.Ordinal));
        Assert.Equal(
            MortalLocationIdentityState.BuildConfusableKey(priorEffectId),
            MortalLocationIdentityState.BuildConfusableKey(confusableEffectId));

        var result = WoundTransitionReducer.Reduce(Request(
            "worsen",
            before,
            after,
            WorsenEvidence(before, after)));

        AssertInvalid(result, "wound_transition_severity_root_identity_reused");
    }

    [Fact]
    public void Reduce_Stabilize_RejectsComplicationEffectTransferredToConsequenceSlot()
    {
        const string effectId = "effect_binding_transfer";
        var complication = Complication(
            "complication_binding_transfer",
            "pain",
            effectId);
        var beforeWithRoot = AddOwnedRoot(
            WithSeverity(PhysicalWound(), "III", 3),
            effectId,
            "definition_binding_transfer_marker",
            "wound_consequence",
            addConsequenceSlot: false);
        var before = beforeWithRoot with
        {
            Complications = ImmutableArray.Create(complication)
        };
        var after = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Complications = ImmutableArray<WoundComplication>.Empty,
            Consequences = before.Consequences with
            {
                SlotsUsed = 3,
                Entries = before.Consequences.Entries.Append(
                    new WoundConsequenceEntry(
                        3,
                        "action_control",
                        effectId,
                        "Эффект нельзя скрыто перенести в слот следствия."))
                    .ToImmutableArray()
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedComplications: new[] { complication.ComplicationId })));

        AssertInvalid(result, "wound_transition_envelope_invalid");
    }

    [Fact]
    public void Reduce_Treat_AppliesOnlyExactSealedRouteGateAndDeclaredResult()
    {
        var complication = Complication("complication_infection", "infection");
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(complication)
        };
        var after = NewTransition(WithSeverity(before, "I", 1) with
        {
            Care = before.Care with
            {
                State = "recovering",
                LastAttemptId = "attempt_treat"
            },
            Complications = ImmutableArray<WoundComplication>.Empty,
            Recovery = before.Recovery with { CurrentStepProgress = 1 },
            Treatment = before.Treatment with
            {
                CompletedRouteIds = ImmutableArray.Create("clean_and_suture")
            }
        }, "treat");
        var outcome = Outcome(after, terminalAttempt: true);
        var evidence = TreatEvidence(before, after, outcome);

        var result = WoundTransitionReducer.Reduce(Request("treat", before, after, evidence));

        AssertValid(result);
        var attempt = Assert.Single(result.Intents.OfType<WoundAttemptTerminalIntent>());
        Assert.Equal("attempt_treat", attempt.AttemptId);
        Assert.Equal("clean_and_suture", attempt.RouteOrGateRef);
        Assert.Equal(1, result.ProposedAfter!.Severity.Rank);
    }

    [Fact]
    public void Reduce_Treat_FailedAttemptMayLeaveMechanicsUnchangedButStillSealsAttempt()
    {
        var before = PhysicalWound();
        var after = NewTransition(before with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var evidence = TreatEvidence(
            before,
            after,
            Outcome(after, terminalAttempt: true));

        var result = WoundTransitionReducer.Reduce(Request("treat", before, after, evidence));

        AssertValid(result);
        Assert.Single(result.Intents.OfType<WoundAttemptTerminalIntent>());
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
    }

    [Fact]
    public void Reduce_Treat_RejectsStaleRouteAttemptAndOutcomeMismatch()
    {
        var before = PhysicalWound();
        var after = NewTransition(before with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var wrongAttempt = TreatEvidence(
            before,
            after,
            Outcome(after, terminalAttempt: true)) with
        {
            AttemptId = "attempt_other"
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request("treat", before, after, wrongAttempt)),
            "wound_transition_treatment_attempt_mismatch");

        var wrongOutcome = Outcome(after, terminalAttempt: true) with
        {
            ResultingSeverityRank = 1
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "treat",
                before,
                after,
                TreatEvidence(before, after, wrongOutcome))),
            "wound_transition_declared_outcome_mismatch");

        var nonTerminal = TreatEvidence(
            before,
            after,
            Outcome(after, terminalAttempt: false));
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request("treat", before, after, nonTerminal)),
            "wound_transition_treatment_attempt_not_terminal");
    }

    [Fact]
    public void Reduce_ComplicateTreatAndRecover_RejectRetainedConsequencePayloadDrift()
    {
        var before = PhysicalWound();
        var complication = Complication("complication_new", "pain");
        var complicateAfter = NewTransition(ChangeConsequencePayload(before) with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray()
        }, "complicate");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "complicate",
                before,
                complicateAfter,
                ComplicateEvidence(
                    before,
                    complicateAfter,
                    complication.ComplicationId))),
            "wound_transition_retained_consequence_changed");

        var treatAfter = NewTransition(ChangeConsequencePayload(before) with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "treat",
                before,
                treatAfter,
                TreatEvidence(
                    before,
                    treatAfter,
                    Outcome(treatAfter, terminalAttempt: true)))),
            "wound_transition_retained_consequence_changed");

        var recoverAfter = NewTransition(ChangeConsequencePayload(before) with
        {
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 1,
                LastTickKey = "tick_fresh"
            }
        }, "recover");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "recover",
                before,
                recoverAfter,
                RecoverEvidence(before, recoverAfter, Outcome(recoverAfter)))),
            "wound_transition_retained_consequence_changed");
    }

    [Fact]
    public void Reduce_Recover_AppliesFreshTickOnceWithBoundedProgressAndSeverityReduction()
    {
        var before = PhysicalWound() with
        {
            Care = PhysicalWound().Care with { State = "recovering" },
            Recovery = PhysicalWound().Recovery with
            {
                CurrentStepProgress = 2,
                LastTickKey = "tick_previous"
            }
        };
        var after = NewTransition(WithSeverity(before, "I", 1) with
        {
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 1,
                CurrentStepThreshold = 2,
                LastTickKey = "tick_fresh"
            }
        }, "recover");
        var evidence = RecoverEvidence(before, after, Outcome(after));

        var result = WoundTransitionReducer.Reduce(Request("recover", before, after, evidence));

        AssertValid(result);
        AssertFreshRootIds(before, after);
        Assert.Single(result.Intents.OfType<WoundRecoverySealIntent>());
        Assert.Equal("tick_fresh", result.ProposedAfter!.Recovery.LastTickKey);
    }

    [Fact]
    public void Reduce_Recover_EmitsExplicitFollowUpHealInsteadOfTerminalRecoverHistory()
    {
        var before = WithSeverity(PhysicalWound(), "I", 1) with
        {
            Care = PhysicalWound().Care with { State = "recovering" },
            Recovery = PhysicalWound().Recovery with
            {
                CurrentStepProgress = 1,
                CurrentStepThreshold = 2
            }
        };
        var after = NewTransition(before with
        {
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 2,
                LastTickKey = "tick_heal_due"
            }
        }, "recover");
        var evidence = RecoverEvidence(
            before,
            after,
            Outcome(after, heals: true),
            tickKey: "tick_heal_due");

        var result = WoundTransitionReducer.Reduce(Request("recover", before, after, evidence));

        AssertValid(result);
        Assert.Contains(result.Intents, intent => intent is WoundFollowUpHealIntent);
        Assert.Equal("active", result.ProposedAfter!.Lifecycle);
        Assert.False(Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).Terminal);
    }

    [Fact]
    public void Reduce_TreatAndRecover_AllowSeverityTwoToStagedOneFollowUpHeal()
    {
        var treatmentBefore = SpiritualWound();
        var treatmentAfter = NewTransition(WithSeverity(treatmentBefore, "I", 1) with
        {
            Care = treatmentBefore.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var treatment = WoundTransitionReducer.Reduce(Request(
            "treat",
            treatmentBefore,
            treatmentAfter,
            TreatEvidence(
                treatmentBefore,
                treatmentAfter,
                Outcome(treatmentAfter, heals: true, terminalAttempt: true))));

        AssertValid(treatment);
        Assert.Contains(treatment.Intents, intent => intent is WoundFollowUpHealIntent);
        Assert.Equal(1, treatment.ProposedAfter!.Severity.Rank);
        Assert.Equal("active", treatment.ProposedAfter.Lifecycle);

        var recoveryBefore = SpiritualWound() with
        {
            Recovery = SpiritualWound().Recovery with
            {
                CurrentStepProgress = 3,
                CurrentStepThreshold = 4
            }
        };
        var recoveryAfter = NewTransition(WithSeverity(recoveryBefore, "I", 1) with
        {
            Recovery = recoveryBefore.Recovery with
            {
                CurrentStepProgress = 1,
                CurrentStepThreshold = 2,
                LastTickKey = "tick_two_step_heal"
            }
        }, "recover");
        var recovery = WoundTransitionReducer.Reduce(Request(
            "recover",
            recoveryBefore,
            recoveryAfter,
            RecoverEvidence(
                recoveryBefore,
                recoveryAfter,
                Outcome(recoveryAfter, heals: true),
                tickKey: "tick_two_step_heal")));

        AssertValid(recovery);
        Assert.Contains(recovery.Intents, intent => intent is WoundFollowUpHealIntent);
        Assert.Equal(1, recovery.ProposedAfter!.Severity.Rank);
        Assert.Equal("active", recovery.ProposedAfter.Lifecycle);
    }

    [Fact]
    public void Reduce_FollowUpHeal_RejectsMoreThanTwoSeveritySteps()
    {
        var before = CreateSpiritualSeverityWound(
            "III",
            3,
            "before_rank_three",
            maximumAtCreation: "III");
        var freshRankOne = CreateSpiritualSeverityWound(
            "I",
            1,
            "after_rank_one",
            maximumAtCreation: "III",
            lastChangeEventRef: "turn_43:wound_transition");
        var after = NewTransition(freshRankOne with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");

        var result = WoundTransitionReducer.Reduce(Request(
            "treat",
            before,
            after,
            TreatEvidence(
                before,
                after,
                Outcome(after, heals: true, terminalAttempt: true))));

        AssertInvalid(result, "wound_transition_follow_up_heal_invalid");
    }

    [Theory]
    [InlineData("treat", "III", 3)]
    [InlineData("treat", "II", 2)]
    [InlineData("recover", "III", 3)]
    [InlineData("recover", "II", 2)]
    public void Reduce_TreatAndRecover_AllowOneOrTwoStepNonHealingReduction(
        string kind,
        string severity,
        int rank)
    {
        var before = WithSeverity(PhysicalWound(), "IV", 4);
        var changed = WithSeverity(before, severity, rank);
        WoundTransitionEvidence evidence;
        WoundMaterializationEnvelope after;
        if (kind == "treat")
        {
            after = NewTransition(changed with
            {
                Care = before.Care with { LastAttemptId = "attempt_treat" }
            }, kind);
            evidence = TreatEvidence(
                before,
                after,
                Outcome(after, terminalAttempt: true));
        }
        else
        {
            after = NewTransition(changed with
            {
                Recovery = before.Recovery with { LastTickKey = "tick_fresh" }
            }, kind);
            evidence = RecoverEvidence(before, after, Outcome(after));
        }

        var result = WoundTransitionReducer.Reduce(Request(kind, before, after, evidence));
        AssertValid(result);
        AssertFreshRootIds(before, after);
        var intent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal("replace", intent.Operation);
        Assert.Equal(EffectIds(before), intent.BeforeEffectIds);
        Assert.Equal(EffectIds(after), intent.AfterEffectIds);
    }

    [Theory]
    [InlineData("treat")]
    [InlineData("recover")]
    public void Reduce_TreatAndRecover_RejectMoreThanTwoStepNonHealingReduction(string kind)
    {
        var before = WithSeverity(PhysicalWound(), "IV", 4);
        var changed = WithSeverity(before, "I", 1);
        WoundTransitionEvidence evidence;
        WoundMaterializationEnvelope after;
        if (kind == "treat")
        {
            after = NewTransition(changed with
            {
                Care = before.Care with { LastAttemptId = "attempt_treat" }
            }, kind);
            evidence = TreatEvidence(
                before,
                after,
                Outcome(after, terminalAttempt: true));
        }
        else
        {
            after = NewTransition(changed with
            {
                Recovery = before.Recovery with { LastTickKey = "tick_fresh" }
            }, kind);
            evidence = RecoverEvidence(before, after, Outcome(after));
        }

        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(kind, before, after, evidence)),
            "wound_transition_severity_reduction_exceeds_limit");
    }

    [Fact]
    public void Reduce_Recover_RejectsReplayUnsealedRegressionAndUndeclaredWorsening()
    {
        var before = PhysicalWound() with
        {
            Recovery = PhysicalWound().Recovery with
            {
                CurrentStepProgress = 2,
                LastTickKey = "tick_used"
            }
        };
        var replayAfter = NewTransition(before, "recover");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "recover",
                before,
                replayAfter,
                RecoverEvidence(before, replayAfter, Outcome(replayAfter), tickKey: "tick_used"))),
            "wound_transition_recovery_tick_replayed");

        var regressionAfter = NewTransition(before with
        {
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 1,
                LastTickKey = "tick_fresh"
            }
        }, "recover");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "recover",
                before,
                regressionAfter,
                RecoverEvidence(before, regressionAfter, Outcome(regressionAfter)))),
            "wound_transition_recovery_regression");

        var worseAfter = NewTransition(WithSeverity(before, "III", 3) with
        {
            Recovery = before.Recovery with
            {
                CurrentStepProgress = 0,
                LastTickKey = "tick_fresh"
            }
        }, "recover");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "recover",
                before,
                worseAfter,
                RecoverEvidence(before, worseAfter, Outcome(worseAfter)))),
            "wound_transition_recovery_worsening_forbidden");
    }

    [Fact]
    public void Reduce_Heal_TerminatesOnlyActiveSeverityIAndRemovesOwnedEffects()
    {
        var before = WithSeverity(PhysicalWound(), "I", 1);
        var after = NewTransition(before with
        {
            Lifecycle = "healed",
            Care = before.Care with
            {
                State = "healed",
                ActiveCourseId = null,
                LastAttemptId = "attempt_heal"
            }
        }, "heal");
        var evidence = HealEvidence(before, after, attemptId: "attempt_heal");

        var result = WoundTransitionReducer.Reduce(Request("heal", before, after, evidence));

        AssertValid(result);
        Assert.Contains(result.Intents, intent =>
            intent is WoundCarrierTransitionIntent { Operation: "remove" });
        var effects = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal("remove", effects.Operation);
        Assert.Equal(EffectIds(before), effects.BeforeEffectIds);
        Assert.Empty(effects.AfterEffectIds);
        AssertExactHealAfter(result, before, after);
    }

    [Fact]
    public void Reduce_Heal_EmptyOwnedSourceGraphEmitsNoEffectIntent()
    {
        var beforeJson = WoundContractTestData.CreateActiveWound();
        beforeJson["severity"]!["value"] = "I";
        beforeJson["severity"]!["rank"] = 1;
        beforeJson["consequences"]!["slotBudget"] = 1;
        beforeJson["consequences"]!["slotsUsed"] = 0;
        beforeJson["consequences"]!["entries"] = new JsonArray();
        beforeJson["consequences"]!["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world");
        var before = ParseWound(beforeJson);
        var after = NewTransition(before with
        {
            Lifecycle = "healed",
            Care = before.Care with
            {
                State = "healed",
                ActiveCourseId = null,
                LastAttemptId = "attempt_heal_empty_graph"
            }
        }, "heal");

        var result = WoundTransitionReducer.Reduce(Request(
            "heal",
            before,
            after,
            HealEvidence(before, after, attemptId: "attempt_heal_empty_graph")));

        AssertValid(result);
        Assert.Empty(EffectIds(before));
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
        AssertExactHealAfter(result, before, after);
    }

    [Fact]
    public void Reduce_Heal_RemovesDirectZeroSlotMarkerRoot()
    {
        var beforeJson = WoundContractTestData.CreateActiveWound();
        var consequences = beforeJson["consequences"]!.AsObject();
        beforeJson["severity"]!["value"] = "I";
        beforeJson["severity"]!["rank"] = 1;
        consequences["slotBudget"] = 1;
        consequences["slotsUsed"] = 1;
        consequences["entries"]!.AsArray().RemoveAt(1);
        consequences["ownedEffectSources"]!["definitions"]!.AsArray().RemoveAt(1);
        consequences["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(1);
        consequences["ownedEffectSources"]!["definitions"]!.AsArray().Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                "wound_test_torn_side",
                "mortal_world",
                "definition_wound_marker",
                "wound_consequence"));
        consequences["ownedEffectSources"]!["rootBindings"]!.AsArray().Add(
            WoundContractTestData.CreateRootBinding(
                "effect_wound_marker",
                "definition_wound_marker"));
        var before = ParseWound(beforeJson);
        var after = NewTransition(before with
        {
            Lifecycle = "healed",
            Care = before.Care with
            {
                State = "healed",
                LastAttemptId = "attempt_heal"
            }
        }, "heal");

        var result = WoundTransitionReducer.Reduce(Request(
            "heal",
            before,
            after,
            HealEvidence(before, after, attemptId: "attempt_heal")));

        AssertValid(result);
        var intent = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
        Assert.Equal("remove", intent.Operation);
        Assert.Equal(EffectIds(before), intent.BeforeEffectIds);
        Assert.Empty(intent.AfterEffectIds);
        AssertExactHealAfter(result, before, after);
    }

    [Fact]
    public void Reduce_Heal_RejectsSeverityIIAndAlreadyHealedWound()
    {
        var before = PhysicalWound();
        var after = NewTransition(before with
        {
            Lifecycle = "healed",
            Care = before.Care with { State = "healed" }
        }, "heal");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "heal",
                before,
                after,
                HealEvidence(before, after))),
            "wound_transition_heal_source_invalid");

        before = WithSeverity(PhysicalWound(), "I", 1) with
        {
            Lifecycle = "healed",
            Care = PhysicalWound().Care with { State = "healed" }
        };
        after = NewTransition(before, "heal");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "heal",
                before,
                after,
                HealEvidence(before, after))),
            "wound_transition_heal_source_invalid");
    }

    [Fact]
    public void Reduce_Heal_EmitsIndependentMechanicalAndCosmeticLegaciesAtomically()
    {
        var before = WithSeverity(PhysicalWound(), "I", 1);
        var cosmetic = CosmeticLegacy("legacy_visible_scar", before.WoundId);
        var mechanical = MechanicalLegacy("legacy_steady_hand", before.WoundId, "trait");
        var after = NewTransition(before with
        {
            Lifecycle = "healed",
            Care = before.Care with { State = "healed" },
            Relations = before.Relations with
            {
                LegacyRefs = before.Relations.LegacyRefs.Append(cosmetic.LegacyId).ToImmutableArray(),
                IndependentEffectRefs = before.Relations.IndependentEffectRefs
                    .Append(mechanical.LegacyId)
                    .ToImmutableArray()
            }
        }, "heal");
        var evidence = HealEvidence(before, after, legacies: new[] { cosmetic, mechanical });

        var result = WoundTransitionReducer.Reduce(Request("heal", before, after, evidence));

        AssertValid(result);
        var cosmeticIntent = Assert.Single(result.Intents.OfType<WoundCosmeticLegacyIntent>());
        var mechanicalIntent = Assert.Single(
            result.Intents.OfType<WoundIndependentMechanicalLegacyIntent>());
        Assert.Equal(before.WoundId, cosmeticIntent.ProvenanceWoundId);
        Assert.Equal(before.WoundId, mechanicalIntent.ProvenanceWoundId);
        Assert.Equal("trait", mechanicalIntent.EntityKind);
        Assert.NotEqual(cosmeticIntent.LegacyId, mechanicalIntent.LegacyId);
    }

    [Fact]
    public void Reduce_Legacy_AcceptsOnlyHealedTerminalAuthorityAndNeverReactivatesWound()
    {
        var before = HealedWound();
        var cosmetic = CosmeticLegacy("legacy_scar", before.WoundId);
        var after = before;
        var evidence = LegacyEvidence(before, after, cosmetic);

        var result = WoundTransitionReducer.Reduce(Request("legacy", before, after, evidence));

        AssertValid(result);
        Assert.Equal("healed", result.ProposedAfter!.Lifecycle);
        Assert.Equal(Fingerprint(before), Fingerprint(result.ProposedAfter));
        Assert.Single(result.Intents.OfType<WoundCosmeticLegacyIntent>());
        Assert.False(Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).Terminal);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundCarrierTransitionIntent);

        var active = PhysicalWound();
        var invalidAfter = NewTransition(active with
        {
            Relations = active.Relations with
            {
                LegacyRefs = ImmutableArray.Create(cosmetic.LegacyId)
            }
        }, "legacy");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "legacy",
                active,
                invalidAfter,
                LegacyEvidence(active, invalidAfter, cosmetic))),
            "wound_transition_legacy_source_invalid");
    }

    [Fact]
    public void Reduce_Legacy_RejectsConflatedOrWrongProvenanceDeclaration()
    {
        var before = HealedWound();
        var invalid = new WoundLegacyDeclaration(
            "legacy_invalid",
            "independent_mechanical",
            null,
            "wound_other",
            "Некорректное наследие.");
        var after = before;

        var result = WoundTransitionReducer.Reduce(Request(
            "legacy",
            before,
            after,
            LegacyEvidence(before, after, invalid)));

        AssertInvalid(result, "wound_transition_legacy_invalid");
    }

    [Fact]
    public void Reduce_Archive_IsHistoryProjectionOnlyAndPreservesTerminalReplayEvidence()
    {
        var before = HealedWound();
        var after = before;
        var evidence = new WoundArchiveEvidence(
            before.LastTransition.TransitionId,
            Fingerprint(before),
            Fingerprint(after));

        var result = WoundTransitionReducer.Reduce(Request("archive", before, after, evidence));

        AssertValid(result);
        Assert.Equal("healed", result.ProposedAfter!.Lifecycle);
        Assert.Equal(Fingerprint(before), Fingerprint(result.ProposedAfter));
        Assert.Single(result.Intents.OfType<WoundArchiveProjectionIntent>());
        Assert.False(Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).Terminal);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundCarrierTransitionIntent);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
    }

    [Theory]
    [InlineData("legacy", true, false)]
    [InlineData("legacy", false, true)]
    [InlineData("archive", true, false)]
    [InlineData("archive", false, true)]
    public void Reduce_PostTerminalAudit_RequiresExactImmediateHealAuthority(
        string kind,
        bool forgePriorKind,
        bool forgeAuthorityRef)
    {
        var before = HealedWound();
        if (forgePriorKind)
        {
            before = before with
            {
                LastTransition = before.LastTransition with { Kind = "archive" }
            };
        }
        var terminalAuthorityRef = forgeAuthorityRef
            ? "wound_transition_other_terminal"
            : before.LastTransition.TransitionId;
        var expectedIssue = kind == "legacy"
            ? "wound_transition_legacy_source_invalid"
            : "wound_transition_archive_source_invalid";

        var result = WoundTransitionReducer.Reduce(PostTerminalAuditRequest(
            kind,
            before,
            terminalAuthorityRef,
            turn: before.LastTransition.Turn,
            transitionId: $"transition_{kind}_invalid_terminal_authority"));

        AssertInvalid(result, expectedIssue);
    }

    [Fact]
    public void Reduce_CreateHealLegacyArchive_ComposesWithExactlyOneTerminalHistoryRow()
    {
        var created = NewTransition(
            WithSeverity(PhysicalWound(), "I", 1, updateMaximumAtCreation: true),
            "create",
            ordinal: 1,
            turn: 42,
            transitionId: "transition_create_composed");
        var create = WoundTransitionReducer.Reduce(Request(
            "create",
            null,
            created,
            CreateEvidence(created),
            transitionId: "transition_create_composed",
            operationKey: "operation_create_composed",
            turn: 42));
        AssertValid(create);

        var healed = NewTransition(created with
        {
            Lifecycle = "healed",
            Care = created.Care with { State = "healed" }
        },
            "heal",
            ordinal: 2,
            turn: 43,
            transitionId: "transition_heal_composed");
        var heal = WoundTransitionReducer.Reduce(Request(
            "heal",
            created,
            healed,
            HealEvidence(created, healed),
            transitionId: "transition_heal_composed",
            operationKey: "operation_heal_composed",
            turn: 43));
        AssertValid(heal);

        var legacy = WoundTransitionReducer.Reduce(Request(
            "legacy",
            healed,
            healed,
            LegacyEvidence(healed, healed, CosmeticLegacy("legacy_composed", healed.WoundId)),
            transitionId: "transition_legacy_composed",
            operationKey: "operation_legacy_composed",
            turn: 44));
        AssertValid(legacy);

        var archive = WoundTransitionReducer.Reduce(Request(
            "archive",
            healed,
            healed,
            new WoundArchiveEvidence(
                healed.LastTransition.TransitionId,
                Fingerprint(healed),
                Fingerprint(healed)),
            transitionId: "transition_archive_composed",
            operationKey: "operation_archive_composed",
            turn: 45));
        AssertValid(archive);

        var historyIntents = new[] { create, heal, legacy, archive }
            .Select(result => Assert.Single(
                result.Intents.OfType<WoundTransitionHistoryIntent>()))
            .ToArray();
        Assert.Single(historyIntents, static intent => intent.Terminal);
        var rows = historyIntents.Select((intent, index) => HistoryRow(
            intent,
            globalOrdinal: index + 1,
            woundOrdinal: index + 1)).ToArray();

        var composed = WoundHistoryState.CreateValidated(5, rows);

        Assert.True(
            composed.IsValid,
            string.Join(Environment.NewLine, composed.Issues.Select(static issue =>
                $"{issue.Code}: {issue.Expected} / {issue.Actual}")));
    }

    [Theory]
    [InlineData("death")]
    [InlineData("dissipate")]
    [InlineData("effect_remove")]
    [InlineData("CREATE")]
    [InlineData("unknown")]
    public void Reduce_UnknownDeathDissipationAndEffectOnlyKindsFailClosed(string kind)
    {
        var before = PhysicalWound();
        var after = NewTransition(before, kind);
        var evidence = new WoundArchiveEvidence(
            "authority_exact",
            Fingerprint(before),
            Fingerprint(after));

        var result = WoundTransitionReducer.Reduce(Request(kind, before, after, evidence));

        AssertInvalid(result, "wound_transition_kind_unknown");
    }

    [Fact]
    public void Reduce_RejectsSeverityVEvenWhenTypedEnvelopeIsForged()
    {
        var before = PhysicalWound();
        var after = NewTransition(before with
        {
            Severity = before.Severity with { Value = "V", Rank = 5 }
        }, "worsen");
        var evidence = WorsenEvidence(before, after, maximumSeverityRank: 5);

        var result = WoundTransitionReducer.Reduce(Request("worsen", before, after, evidence));

        AssertInvalid(result, "wound_transition_envelope_invalid");
    }

    [Theory]
    [InlineData("owner")]
    [InlineData("realm")]
    [InlineData("domain")]
    [InlineData("origin")]
    [InlineData("classification")]
    public void Reduce_RejectsOwnerRealmDomainOrProvenanceRetargeting(string mutation)
    {
        var before = PhysicalWound();
        var after = NewTransition(before with
        {
            Owner = mutation switch
            {
                "owner" => before.Owner with { OwnerId = "player_other" },
                "realm" => before.Owner with { Realm = "chaos_sea" },
                _ => before.Owner
            },
            Origin = mutation == "origin"
                ? before.Origin with { SourceId = "source_other" }
                : before.Origin,
            Classification = mutation switch
            {
                "domain" => before.Classification with { Domain = "spiritual" },
                "classification" => before.Classification with { WoundType = "Иная травма" },
                _ => before.Classification
            },
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var evidence = TreatEvidence(before, after, Outcome(after, terminalAttempt: true));

        var result = WoundTransitionReducer.Reduce(Request("treat", before, after, evidence));

        AssertInvalid(result, "wound_transition_identity_or_provenance_changed");
    }

    [Fact]
    public void Reduce_HealedWoundCannotReopenThroughAnyActiveTransition()
    {
        var before = HealedWound();
        var after = NewTransition(before with
        {
            Lifecycle = "active",
            Care = before.Care with
            {
                State = "untreated",
                LastAttemptId = "attempt_treat"
            }
        }, "treat");
        var evidence = TreatEvidence(before, after, Outcome(after, terminalAttempt: true));

        var result = WoundTransitionReducer.Reduce(Request("treat", before, after, evidence));

        AssertInvalid(result, "wound_transition_active_source_invalid");
    }

    [Fact]
    public void Reduce_RejectsWrongSealedBeforeOrAfterFingerprint()
    {
        var before = PhysicalWound();
        var after = NewTransition(before with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var evidence = TreatEvidence(before, after, Outcome(after, terminalAttempt: true)) with
        {
            ExpectedAfterFingerprint = ValidFingerprint('f')
        };

        AssertInvalid(
            WoundTransitionReducer.Reduce(Request("treat", before, after, evidence)),
            "wound_transition_after_fingerprint_mismatch");

        evidence = TreatEvidence(before, after, Outcome(after, terminalAttempt: true)) with
        {
            ExpectedBeforeFingerprint = ValidFingerprint('e')
        };
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request("treat", before, after, evidence)),
            "wound_transition_before_fingerprint_mismatch");
    }

    [Fact]
    public void Reduce_ForgedNullTypedBoundary_ReturnsExactIssuesWithoutThrowing()
    {
        var before = PhysicalWound();
        var treatAfter = NewTransition(before with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var recoverAfter = NewTransition(before with
        {
            Recovery = before.Recovery with { LastTickKey = "tick_fresh" }
        }, "recover");
        var diagnoseBefore = before with
        {
            Treatment = before.Treatment with
            {
                DiagnosisPaths = ImmutableArray.Create(DiagnosisPath(
                    "diagnosis_path_null", "route:clean_and_suture"))
            }
        };
        var diagnoseRequest = DiagnoseRequest(diagnoseBefore,
            NewTransition(diagnoseBefore, "diagnose"), "diagnosis_path_null");
        AssertValid(WoundTransitionReducer.Reduce(diagnoseRequest));
        var diagnoseEvidence = Assert.IsType<WoundDiagnosisEvidence>(diagnoseRequest.Evidence);
        var stabilizeAfter = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            }
        }, "stabilize");
        var healBefore = WithSeverity(before, "I", 1);
        var healAfter = NewTransition(healBefore with
        {
            Lifecycle = "healed",
            Care = healBefore.Care with { State = "healed" }
        }, "heal");
        var terminal = HealedWound();
        var validOutcome = Outcome(treatAfter, terminalAttempt: true);
        var cases = new (string Name, string Issue, Func<WoundTransitionReductionResult> Act)[]
        {
            (
                "null request",
                "wound_transition_request_invalid",
                () => WoundTransitionReducer.Reduce(null!)),
            (
                "null evidence",
                "wound_transition_evidence_missing",
                () => WoundTransitionReducer.Reduce(Request(
                    "treat", before, treatAfter, null!))),
            (
                "null treatment outcome",
                "wound_transition_declared_outcome_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "treat",
                    before,
                    treatAfter,
                    TreatEvidence(before, treatAfter, null!)))),
            (
                "null recovery outcome",
                "wound_transition_declared_outcome_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "recover",
                    before,
                    recoverAfter,
                    RecoverEvidence(before, recoverAfter, null!)))),
            (
                "null diagnosis reveal list",
                "wound_transition_evidence_invalid",
                () => WoundTransitionReducer.Reduce(diagnoseRequest with
                {
                    Evidence = diagnoseEvidence with { RevealedFacts = null! }
                })),
            (
                "null stabilization removal lists",
                "wound_transition_evidence_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "stabilize",
                    before,
                    stabilizeAfter,
                    new WoundStabilizationEvidence(
                        "stabilization_authority",
                        Fingerprint(before),
                        Fingerprint(stabilizeAfter),
                        "attempt_stabilize",
                        null!,
                        null!,
                        null!)))),
            (
                "null heal legacies",
                "wound_transition_legacy_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "heal",
                    healBefore,
                    healAfter,
                    new WoundHealingEvidence(
                        "healing_authority",
                        Fingerprint(healBefore),
                        Fingerprint(healAfter),
                        "accepted_healing_result",
                        null,
                        null!)))),
            (
                "null heal legacy element",
                "wound_transition_legacy_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "heal",
                    healBefore,
                    healAfter,
                    new WoundHealingEvidence(
                        "healing_authority",
                        Fingerprint(healBefore),
                        Fingerprint(healAfter),
                        "accepted_healing_result",
                        null,
                        ImmutableArray.CreateRange(
                            new WoundLegacyDeclaration[] { null! }))))),
            (
                "null legacy list",
                "wound_transition_legacy_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "legacy",
                    terminal,
                    terminal,
                    new WoundLegacyEvidence(
                        terminal.LastTransition.TransitionId,
                        Fingerprint(terminal),
                        Fingerprint(terminal),
                        null!)))),
            (
                "null legacy element",
                "wound_transition_legacy_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "legacy",
                    terminal,
                    terminal,
                    new WoundLegacyEvidence(
                        terminal.LastTransition.TransitionId,
                        Fingerprint(terminal),
                        Fingerprint(terminal),
                        ImmutableArray.CreateRange(
                            new WoundLegacyDeclaration[] { null! }))))),
            (
                "null resulting complication list",
                "wound_transition_declared_outcome_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "treat",
                    before,
                    treatAfter,
                    TreatEvidence(before, treatAfter, validOutcome with
                    {
                        ResultingComplicationIds = null!
                    })))),
            (
                "null resulting effect list",
                "wound_transition_declared_outcome_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "treat",
                    before,
                    treatAfter,
                    TreatEvidence(before, treatAfter, validOutcome with
                    {
                        ResultingEffectIds = null!
                    })))),
            (
                "null resulting blocker list",
                "wound_transition_declared_outcome_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "treat",
                    before,
                    treatAfter,
                    TreatEvidence(before, treatAfter, validOutcome with
                    {
                        ResultingRecoveryBlockers = null!
                    })))),
            (
                "null resulting route list",
                "wound_transition_declared_outcome_invalid",
                () => WoundTransitionReducer.Reduce(Request(
                    "treat",
                    before,
                    treatAfter,
                    TreatEvidence(before, treatAfter, validOutcome with
                    {
                        ResultingCompletedRouteIds = null!
                    }))))
        };

        var observed = cases.Select(testCase =>
        {
            WoundTransitionReductionResult? result = null;
            var exception = Record.Exception(() => result = testCase.Act());
            return (testCase.Name, testCase.Issue, Result: result, Exception: exception);
        }).ToArray();

        Assert.All(observed, item =>
        {
            Assert.Null(item.Exception);
            Assert.NotNull(item.Result);
            AssertInvalid(item.Result!, item.Issue);
        });
    }

    private static WoundTransitionRequest Request(
        string kind,
        WoundMaterializationEnvelope? before,
        WoundMaterializationEnvelope? after,
        WoundTransitionEvidence evidence,
        string transitionId = "wound_transition_test_002",
        string operationKey = "operation_wound_transition_002",
        int? turn = null) => new(
            kind,
            transitionId,
            operationKey,
            kind == "create" && after is not null
                ? after.Origin.EventRef
                : "turn_43:wound_transition",
            turn ?? after?.LastTransition.Turn ?? 43,
            before,
            after,
            evidence);

    private static WoundTransitionRequest RetainedUnreachableRootStabilizationRequest()
    {
        const string effectId = "effect_wound_test_pain";
        var complication = Complication("complication_pain", "pain", effectId);
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(complication)
        };
        var after = NewTransition(before with
        {
            Care = before.Care with
            {
                State = "stabilized",
                StabilizedAtTurn = 43,
                LastAttemptId = "attempt_stabilize"
            },
            Complications = ImmutableArray<WoundComplication>.Empty,
            Consequences = before.Consequences with
            {
                SlotsUsed = 1,
                Entries = before.Consequences.Entries.Take(1).ToImmutableArray()
            },
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray<string>.Empty
            }
        }, "stabilize");
        return Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(
                before,
                after,
                removedComplications: new[] { complication.ComplicationId },
                removedEffects: new[] { effectId },
                removedBlockers: new[] { "not_stabilized" }));
    }

    private static WoundMaterializationEnvelope RetainedUnreachableRootAfterWithSourceMutation(
        WoundTransitionRequest request,
        string mutation)
    {
        const string effectId = "effect_wound_test_pain";
        const string definitionKey = "definition_wound_test_pain";
        const string reboundDefinitionKey = "definition_wound_test_pain_rebound";
        var before = request.Before!;
        var branchJson = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!.AsObject();
        var sourcesJson = branchJson["consequences"]!["ownedEffectSources"]!.AsObject();
        var definitions = sourcesJson["definitions"]!.AsArray();
        var definitionIndex = definitions
            .Select((definition, index) => (Definition: definition, Index: index))
            .Single(pair => string.Equals(
                pair.Definition!["definitionKey"]!.GetValue<string>(),
                definitionKey,
                StringComparison.Ordinal))
            .Index;

        switch (mutation)
        {
            case "binding":
                definitions[definitionIndex] = WoundContractTestData.CreateOwnedEffectDefinition(
                    before.WoundId,
                    before.Owner.Realm,
                    reboundDefinitionKey,
                    "action_control");
                var targetBinding = sourcesJson["rootBindings"]!.AsArray()
                    .Single(binding => string.Equals(
                        binding!["effectId"]!.GetValue<string>(),
                        effectId,
                        StringComparison.Ordinal))!;
                targetBinding["definitionKey"] = reboundDefinitionKey;
                break;
            case "canonical_definition":
                definitions[definitionIndex]!["display"]!["description"] =
                    "Каноническое тело сохранённой ветви было подменено.";
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }

        var restoredBranch = ParseWound(branchJson);
        return request.ProposedAfter! with
        {
            Consequences = request.ProposedAfter!.Consequences with
            {
                OwnedEffectSources = restoredBranch.Consequences.OwnedEffectSources
            }
        };
    }

    private static WoundTransitionRequest WorsenRequest(
        WoundMaterializationEnvelope before,
        int turn,
        string transitionId)
    {
        var after = NewTransition(
            WithSeverity(before, "III", 3) with
            {
                Recovery = before.Recovery with { CurrentStepProgress = 0 }
            },
            "worsen",
            turn: turn,
            transitionId: transitionId);
        return Request(
            "worsen",
            before,
            after,
            WorsenEvidence(before, after),
            transitionId: transitionId,
            turn: turn);
    }

    private static WoundTransitionRequest PostTerminalAuditRequest(
        string kind,
        WoundMaterializationEnvelope before,
        string? terminalAuthorityRef = null,
        int? turn = null,
        string transitionId = "transition_terminal_audit")
    {
        var authorityRef = terminalAuthorityRef ?? before.LastTransition.TransitionId;
        WoundTransitionEvidence evidence = kind switch
        {
            "legacy" => new WoundLegacyEvidence(
                authorityRef,
                Fingerprint(before),
                Fingerprint(before),
                ImmutableArray<WoundLegacyDeclaration>.Empty),
            "archive" => new WoundArchiveEvidence(
                authorityRef,
                Fingerprint(before),
                Fingerprint(before)),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
        return Request(
            kind,
            before,
            before,
            evidence,
            transitionId: transitionId,
            turn: turn ?? before.LastTransition.Turn);
    }

    private static WoundCreateEvidence CreateEvidence(
        WoundMaterializationEnvelope after,
        int? maximumSeverityRank = null) => new(
            "opportunity_authority",
            WoundHistoryState.ComputeNonexistentBeforeFingerprint(after.WoundId),
            Fingerprint(after),
            after.Origin.OpportunityId,
            maximumSeverityRank ?? SeverityRank(after.Severity.MaximumAtCreation),
            after.Owner,
            after.Classification.Domain);

    private static WoundWorseningEvidence WorsenEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        int maximumSeverityRank = 4,
        bool hasPendingTreatmentOrRecovery = false) => new(
            "worsening_authority",
            Fingerprint(before),
            Fingerprint(after),
            "retrauma",
            maximumSeverityRank,
            hasPendingTreatmentOrRecovery);

    private static WoundComplicationEvidence ComplicateEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        string complicationId,
        bool allowsWorsening = false) => new(
            "complication_authority",
            Fingerprint(before),
            Fingerprint(after),
            complicationId,
            "turn_43:complication_cause",
            allowsWorsening,
            MaximumSeverityRank: 4,
            HasPendingTreatmentOrRecovery: false);

    private static WoundTransitionRequest DiagnoseRequest(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        string diagnosisPathId) => MortalWoundTreatmentPlanner.CreateDiagnosisTransition(
            after.LastTransition.TransitionId, "diagnosis_command_test",
            "operation_wound_transition_002", "attempt_diagnosis_test",
            "turn_43:wound_transition", after.LastTransition.Turn,
            before, after, diagnosisPathId, "success",
            "sha256:" + new string('6', 64), "sha256:" + new string('7', 64));

    private static WoundStabilizationEvidence StabilizeEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        IReadOnlyList<string>? removedComplications = null,
        IReadOnlyList<string>? removedEffects = null,
        IReadOnlyList<string>? removedBlockers = null) => new(
            "stabilization_authority",
            Fingerprint(before),
            Fingerprint(after),
            "attempt_stabilize",
            (removedComplications ?? Array.Empty<string>()).ToImmutableArray(),
            (removedEffects ?? Array.Empty<string>()).ToImmutableArray(),
            (removedBlockers ?? Array.Empty<string>()).ToImmutableArray());

    private static WoundTreatmentEvidence TreatEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        WoundDeclaredTransitionOutcome outcome) => new(
            "treatment_authority",
            Fingerprint(before),
            Fingerprint(after),
            "clean_and_suture",
            "attempt_treat",
            outcome);

    private static WoundRecoveryEvidence RecoverEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        WoundDeclaredTransitionOutcome outcome,
        string tickKey = "tick_fresh") => new(
            "recovery_authority",
            Fingerprint(before),
            Fingerprint(after),
            tickKey,
            "mortal_clock_43",
            outcome);

    private static WoundHealingEvidence HealEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        string? attemptId = null,
        IReadOnlyList<WoundLegacyDeclaration>? legacies = null) => new(
            "healing_authority",
            Fingerprint(before),
            Fingerprint(after),
            "accepted_healing_result",
            attemptId,
            (legacies ?? Array.Empty<WoundLegacyDeclaration>()).ToImmutableArray());

    private static WoundLegacyEvidence LegacyEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        params WoundLegacyDeclaration[] legacies) => new(
            before.LastTransition.TransitionId,
            Fingerprint(before),
            Fingerprint(after),
            legacies.ToImmutableArray());

    private static WoundDeclaredTransitionOutcome Outcome(
        WoundMaterializationEnvelope after,
        bool heals = false,
        bool terminalAttempt = false,
        bool allowsWorsening = false) => new(
            after.Severity.Rank,
            after.Care.State,
            after.Recovery.CurrentStepProgress,
            heals,
            terminalAttempt,
            allowsWorsening,
            after.Complications.Select(static value => value.ComplicationId).ToImmutableArray(),
            EffectIds(after),
            after.Recovery.Blockers.ToImmutableArray(),
            after.Treatment.CompletedRouteIds.ToImmutableArray());

    private static WoundLegacyDeclaration CosmeticLegacy(string id, string woundId) => new(
        id,
        "cosmetic",
        null,
        woundId,
        "После исцеления остался заметный шрам.");

    private static WoundLegacyDeclaration MechanicalLegacy(
        string id,
        string woundId,
        string entityKind) => new(
            id,
            "independent_mechanical",
            entityKind,
            woundId,
            "Пережитая рана стала самостоятельной чертой.");

    private static WoundComplication Complication(
        string id,
        string kind,
        params string[] effectIds) => new(
            id,
            kind,
            "active",
            "Осложнение",
            1,
            effectIds.ToImmutableArray(),
            "known_to_player");

    private static WoundDiagnosisPath DiagnosisPath(
        string diagnosisPathId,
        params string[] reveals) => new(
            diagnosisPathId,
            "Проверить известные признаки",
            "known_to_player",
            ImmutableArray<string>.Empty,
            ImmutableArray<JsonElement>.Empty,
            JsonSerializer.Deserialize<JsonElement>("{}"),
            reveals.ToImmutableArray(),
            "no_reveal");

    private static WoundMaterializationEnvelope ChangeConsequencePayload(
        WoundMaterializationEnvelope wound)
    {
        var entries = wound.Consequences.Entries.ToArray();
        entries[0] = entries[0] with
        {
            ReadableSummary = "Под тем же ID подменено следствие раны."
        };
        return wound with
        {
            Consequences = wound.Consequences with
            {
                Entries = entries.ToImmutableArray()
            }
        };
    }

    private static WoundHistoryTransition HistoryRow(
        WoundTransitionHistoryIntent intent,
        int globalOrdinal,
        int woundOrdinal) => new(
            intent.TransitionId,
            intent.WoundId,
            globalOrdinal,
            woundOrdinal,
            intent.Kind,
            intent.Turn,
            intent.EventRef,
            intent.OperationKey,
            intent.BeforeFingerprint,
            intent.AfterFingerprint,
            ValidFingerprint('d'),
            intent.AttemptId,
            null,
            null,
            intent.TickKey,
            null,
            WoundHistoryState.ComputeOutputFingerprint(
                intent.OperationKey,
                intent.EventRef,
                "Проверяемый переход материализации раны."),
            "Проверяемый переход материализации раны.",
            intent.Terminal);

    private static WoundMaterializationEnvelope PhysicalWound() => ParseWound(
        WoundContractTestData.CreateActiveWound());

    private static WoundMaterializationEnvelope EmptyPhysicalWound(
        string severity,
        int rank,
        string maximumAtCreation)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["severity"]!["value"] = severity;
        wound["severity"]!["rank"] = rank;
        wound["severity"]!["maximumAtCreation"] = maximumAtCreation;
        wound["consequences"]!["slotBudget"] = rank;
        wound["consequences"]!["slotsUsed"] = 0;
        wound["consequences"]!["entries"] = new JsonArray();
        wound["consequences"]!["ownedEffectSources"] = new JsonObject
        {
            ["definitions"] = new JsonArray(),
            ["rootBindings"] = new JsonArray()
        };
        return ParseWound(wound);
    }

    private static WoundMaterializationEnvelope RebindFirstRootToPriorIdentity(
        WoundMaterializationEnvelope wound,
        string priorEffectId,
        bool confusable)
    {
        var raw = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(wound))!.AsObject();
        var bindings = raw["consequences"]!["ownedEffectSources"]!["rootBindings"]!
            .AsArray();
        var oldEffectId = bindings[0]!["effectId"]!.GetValue<string>();
        var replacement = confusable ? priorEffectId.ToUpperInvariant() : priorEffectId;
        bindings[0]!["effectId"] = replacement;
        foreach (var entry in raw["consequences"]!["entries"]!.AsArray())
        {
            if (string.Equals(
                    entry!["effectId"]!.GetValue<string>(),
                    oldEffectId,
                    StringComparison.Ordinal))
            {
                entry["effectId"] = replacement;
            }
        }
        foreach (var complication in raw["complications"]!.AsArray())
        {
            var ownedEffectIds = complication!["ownedEffectIds"]!.AsArray();
            for (var index = 0; index < ownedEffectIds.Count; index++)
            {
                if (string.Equals(
                        ownedEffectIds[index]!.GetValue<string>(),
                        oldEffectId,
                        StringComparison.Ordinal))
                {
                    ownedEffectIds[index] = replacement;
                }
            }
        }
        return ParseWound(raw);
    }

    private static JsonObject CreateCollapsedRootWound()
    {
        var wound = WoundContractTestData.CreateActiveWound();
        var consequences = wound["consequences"]!.AsObject();
        var sources = consequences["ownedEffectSources"]!.AsObject();
        var definitions = sources["definitions"]!.AsArray();
        var secondComponent = definitions[1]!["components"]![0]!
            .DeepClone().AsObject();
        secondComponent["componentId"] = "component_002";
        definitions[0]!["components"]!.AsArray().Add(secondComponent);
        definitions[0]!["triggers"]![0]!["componentIds"]!
            .AsArray().Add("component_002");
        definitions.RemoveAt(1);
        sources["rootBindings"]!.AsArray().RemoveAt(1);
        consequences["entries"]![1]!["effectId"] = "effect_wound_test_bleeding";
        return wound;
    }

    private static JsonObject CreateSeverityThreeWound(bool retainPriorRootIds)
    {
        var wound = WoundContractTestData.CreateActiveWound();
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["lastChangeEventRef"] = "turn_43:wound_transition";
        wound["lastTransition"] = new JsonObject
        {
            ["transitionId"] = "wound_transition_test_002",
            ["ordinal"] = 2,
            ["turn"] = 43,
            ["kind"] = "worsen"
        };
        var rootIds = retainPriorRootIds
            ? new[]
            {
                "effect_wound_test_bleeding",
                "effect_wound_test_pain",
                "effect_wound_fresh_2"
            }
            : new[]
            {
                "effect_wound_fresh_0",
                "effect_wound_fresh_1",
                "effect_wound_fresh_2"
            };
        var definitionKeys = retainPriorRootIds
            ? new[]
            {
                "definition_wound_test_bleeding",
                "definition_wound_test_pain",
                "definition_wound_fresh_2"
            }
            : new[]
            {
                "definition_wound_fresh_0",
                "definition_wound_fresh_1",
                "definition_wound_fresh_2"
            };
        var consequences = wound["consequences"]!.AsObject();
        consequences["slotBudget"] = 3;
        consequences["slotsUsed"] = 3;
        consequences["entries"] = new JsonArray(
            Enumerable.Range(0, 3)
                .Select(index => (JsonNode?)new JsonObject
                {
                    ["slot"] = index + 1,
                    ["profileKey"] = WoundContractTestData.DistinctMortalProfile(index),
                    ["effectId"] = rootIds[index],
                    ["readableSummary"] = $"Перематериализованный слот {index + 1}."
                })
                .ToArray());
        consequences["ownedEffectSources"] =
            WoundContractTestData.CreateOwnedEffectSources(
                "wound_test_torn_side",
                "mortal_world",
                Enumerable.Range(0, 3)
                    .Select(index => (
                        rootIds[index],
                        definitionKeys[index],
                        WoundContractTestData.DistinctMortalProfile(index)))
                    .ToArray());
        return wound;
    }

    private static void PrepareStabilized(JsonObject wound)
    {
        wound["care"]!["state"] = "stabilized";
        wound["care"]!["stabilizedAtTurn"] = 43;
        wound["care"]!["lastAttemptId"] = "attempt_stabilize";
        wound["recovery"]!["blockers"] = new JsonArray();
        wound["lastTransition"] = new JsonObject
        {
            ["transitionId"] = "wound_transition_test_002",
            ["ordinal"] = 2,
            ["turn"] = 43,
            ["kind"] = "stabilize"
        };
    }

    private static WoundMaterializationEnvelope AddOwnedRoot(
        WoundMaterializationEnvelope wound,
        string effectId,
        string definitionKey,
        string profile,
        bool addConsequenceSlot)
    {
        var raw = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(wound))!.AsObject();
        var sources = raw["consequences"]!["ownedEffectSources"]!.AsObject();
        sources["definitions"]!.AsArray().Add(
            WoundContractTestData.CreateOwnedEffectDefinition(
                wound.WoundId,
                wound.Owner.Realm,
                definitionKey,
                profile));
        sources["rootBindings"]!.AsArray().Add(
            WoundContractTestData.CreateRootBinding(effectId, definitionKey));
        if (addConsequenceSlot)
        {
            var entries = raw["consequences"]!["entries"]!.AsArray();
            entries.Add(new JsonObject
            {
                ["slot"] = entries.Count + 1,
                ["profileKey"] = profile,
                ["effectId"] = effectId,
                ["readableSummary"] = "Дополнительное следствие принадлежит осложнению."
            });
            raw["consequences"]!["slotsUsed"] = entries.Count;
        }
        return ParseWound(raw);
    }

    private static WoundMaterializationEnvelope RemoveOwnedRoot(
        WoundMaterializationEnvelope wound,
        string effectId)
    {
        var raw = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(wound))!.AsObject();
        var sources = raw["consequences"]!["ownedEffectSources"]!.AsObject();
        var bindings = sources["rootBindings"]!.AsArray();
        var bindingIndex = bindings
            .Select((node, index) => (Node: node, Index: index))
            .Single(pair => string.Equals(
                pair.Node!["effectId"]!.GetValue<string>(),
                effectId,
                StringComparison.Ordinal))
            .Index;
        var definitionKey = bindings[bindingIndex]!["definitionKey"]!.GetValue<string>();
        bindings.RemoveAt(bindingIndex);
        var definitions = sources["definitions"]!.AsArray();
        var definitionIndex = definitions
            .Select((node, index) => (Node: node, Index: index))
            .Single(pair => string.Equals(
                pair.Node!["definitionKey"]!.GetValue<string>(),
                definitionKey,
                StringComparison.Ordinal))
            .Index;
        definitions.RemoveAt(definitionIndex);
        var retainedEntries = raw["consequences"]!["entries"]!.AsArray()
            .Where(node => !string.Equals(
                node!["effectId"]!.GetValue<string>(),
                effectId,
                StringComparison.Ordinal))
            .Select(static node => node!.DeepClone())
            .ToArray();
        for (var index = 0; index < retainedEntries.Length; index++)
            retainedEntries[index]!["slot"] = index + 1;
        raw["consequences"]!["entries"] = new JsonArray(retainedEntries);
        raw["consequences"]!["slotsUsed"] = retainedEntries.Length;
        return ParseWound(raw);
    }

    private static WoundMaterializationEnvelope SpiritualWound() => ParseWound(
        WoundContractTestData.CreateSpiritualActiveWound());

    private static WoundMaterializationEnvelope CreateSpiritualSeverityWound(
        string value,
        int rank,
        string identitySuffix,
        string maximumAtCreation,
        string lastChangeEventRef = "turn_42:wound_opened")
    {
        var profiles = new[]
        {
            "spiritual_roll_hindrance",
            "spiritual_action_cost_burden",
            "spiritual_position_burden"
        };
        var wound = WoundContractTestData.CreateSpiritualActiveWound();
        wound["severity"]!["value"] = value;
        wound["severity"]!["rank"] = rank;
        wound["severity"]!["maximumAtCreation"] = maximumAtCreation;
        wound["severity"]!["lastChangeEventRef"] = lastChangeEventRef;
        var consequences = wound["consequences"]!.AsObject();
        consequences["slotBudget"] = rank;
        consequences["slotsUsed"] = rank;

        var roots = Enumerable.Range(0, rank)
            .Select(index => (
                $"effect_spiritual_{identitySuffix}_{index}",
                $"definition_spiritual_{identitySuffix}_{index}",
                profiles[index]))
            .ToArray();
        var sources = WoundContractTestData.CreateOwnedEffectSourcesForTarget(
            "wound_spiritual_test",
            "chaos_sea",
            "player",
            roots);
        for (var index = 0; index < rank; index++)
        {
            var componentId = $"component_spiritual_{identitySuffix}_{index}";
            var definition = sources["definitions"]![index]!;
            definition["components"]![0]!["componentId"] = componentId;
            definition["triggers"]![0]!["componentIds"] = new JsonArray(componentId);
        }
        if (rank >= 2)
            sources["definitions"]![1]!["components"]![0]!["payload"]!["magnitude"] = 2;
        if (rank >= 3)
            sources["definitions"]![2]!["components"]![0]!["payload"]!["magnitude"] = 2;
        consequences["ownedEffectSources"] = sources;
        consequences["entries"] = new JsonArray(
            Enumerable.Range(0, rank)
                .Select(index => (JsonNode?)new JsonObject
                {
                    ["slot"] = index + 1,
                    ["profileKey"] = profiles[index],
                    ["effectId"] = roots[index].Item1,
                    ["readableSummary"] = $"Духовное следствие ранга {rank}, слот {index + 1}."
                })
                .ToArray());
        return ParseWound(wound);
    }

    private static WoundMaterializationEnvelope HealedWound()
    {
        var active = WithSeverity(PhysicalWound(), "I", 1);
        return active with
        {
            Lifecycle = "healed",
            Care = active.Care with { State = "healed" },
            LastTransition = new WoundLastTransition(
                "wound_transition_terminal",
                2,
                43,
                "heal")
        };
    }

    private static WoundMaterializationEnvelope WithSeverity(
        WoundMaterializationEnvelope wound,
        string value,
        int rank,
        bool updateMaximumAtCreation = false)
    {
        var retainedEntries = wound.Consequences.Entries.Take(rank).ToArray();
        var changed = wound with
        {
            Severity = wound.Severity with
            {
                Value = value,
                Rank = rank,
                MaximumAtCreation = updateMaximumAtCreation
                    ? value
                    : wound.Severity.MaximumAtCreation,
                LastChangeEventRef = rank == wound.Severity.Rank
                    ? wound.Severity.LastChangeEventRef
                    : "turn_43:wound_transition"
            },
            Consequences = wound.Consequences with
            {
                SlotBudget = rank,
                SlotsUsed = retainedEntries.Length,
                Entries = retainedEntries.ToImmutableArray()
            }
        };
        if (rank == wound.Severity.Rank)
            return changed;

        var raw = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(changed))!.AsObject();
        var entries = raw["consequences"]!["entries"]!.AsArray();
        var sources = raw["consequences"]!["ownedEffectSources"]!.AsObject();
        var definitions = sources["definitions"]!.AsArray();
        var bindings = sources["rootBindings"]!.AsArray();
        var retainedOldIds = entries
            .Select(static entry => entry!["effectId"]!.GetValue<string>())
            .Concat(raw["complications"]!.AsArray().SelectMany(static complication =>
                complication!["ownedEffectIds"]!.AsArray()
                    .Select(static effectId => effectId!.GetValue<string>())))
            .ToHashSet(StringComparer.Ordinal);
        foreach (var binding in bindings)
        {
            var definitionKey = binding!["definitionKey"]!.GetValue<string>();
            var definition = definitions.Single(node => string.Equals(
                node!["definitionKey"]!.GetValue<string>(),
                definitionKey,
                StringComparison.Ordinal));
            var isMarker = definition!["components"]!.AsArray().Any(component =>
                string.Equals(
                    component!["profile"]!.GetValue<string>(),
                    "wound_consequence",
                    StringComparison.Ordinal));
            if (isMarker)
                retainedOldIds.Add(binding["effectId"]!.GetValue<string>());
        }

        var retainedBindings = bindings
            .Where(binding => retainedOldIds.Contains(
                binding!["effectId"]!.GetValue<string>()))
            .Select(static binding => binding!.DeepClone().AsObject())
            .ToArray();
        var idMap = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var index = 0; index < retainedBindings.Length; index++)
        {
            var oldId = retainedBindings[index]["effectId"]!.GetValue<string>();
            var newId = $"effect_{wound.WoundId}_severity_{rank}_root_{index}";
            idMap.Add(oldId, newId);
            retainedBindings[index]["effectId"] = newId;
        }

        foreach (var entry in entries)
        {
            var oldId = entry!["effectId"]!.GetValue<string>();
            entry["effectId"] = idMap[oldId];
        }
        foreach (var complication in raw["complications"]!.AsArray())
        {
            var owned = complication!["ownedEffectIds"]!.AsArray();
            for (var index = 0; index < owned.Count; index++)
                owned[index] = idMap[owned[index]!.GetValue<string>()];
        }

        var reachableDefinitionKeys = retainedBindings
            .Select(static binding => binding["definitionKey"]!.GetValue<string>())
            .ToHashSet(StringComparer.Ordinal);
        var changedReachability = true;
        while (changedReachability)
        {
            changedReachability = false;
            foreach (var definition in definitions.Where(definition =>
                         reachableDefinitionKeys.Contains(
                             definition!["definitionKey"]!.GetValue<string>())))
            {
                foreach (var component in definition!["components"]!.AsArray())
                {
                    if (!string.Equals(
                            component!["profile"]!.GetValue<string>(),
                            "event_reaction",
                            StringComparison.Ordinal) ||
                        !string.Equals(
                            component["payload"]!["resultKind"]?.GetValue<string>(),
                            "apply_definition",
                            StringComparison.Ordinal))
                    {
                        continue;
                    }
                    var targetKey = component["payload"]!["definitionKey"]!.GetValue<string>();
                    changedReachability |= reachableDefinitionKeys.Add(targetKey);
                }
            }
        }

        sources["definitions"] = new JsonArray(definitions
            .Where(definition => reachableDefinitionKeys.Contains(
                definition!["definitionKey"]!.GetValue<string>()))
            .Select(static definition => definition!.DeepClone())
            .ToArray());
        sources["rootBindings"] = new JsonArray(retainedBindings);
        return ParseWound(raw);
    }

    private static WoundMaterializationEnvelope NewTransition(
        WoundMaterializationEnvelope wound,
        string kind,
        int? ordinal = null,
        int turn = 43,
        string transitionId = "wound_transition_test_002") => wound with
    {
        Severity = kind == "create"
            ? wound.Severity with { LastChangeEventRef = wound.Origin.EventRef }
            : wound.Severity,
        LastTransition = new WoundLastTransition(
            transitionId,
            ordinal ?? wound.LastTransition.Ordinal + 1,
            turn,
            kind)
    };

    private static WoundMaterializationEnvelope ParseWound(JsonObject json)
    {
        var result = WoundMaterializationContract.Parse(json.ToJsonString(), "wound");
        Assert.True(
            result.IsValid,
            string.Join(Environment.NewLine, result.Issues.Select(static issue =>
                $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}")));
        return result.Wound!;
    }

    private static ImmutableArray<string> EffectIds(WoundMaterializationEnvelope wound) =>
        JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(wound))!
            ["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray()
            .Select(static binding => binding!["effectId"]!.GetValue<string>())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();

    private static void AssertFreshRootIds(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after) =>
        Assert.Empty(EffectIds(before).Intersect(EffectIds(after), StringComparer.Ordinal));

    private static void AssertExactHealAfter(
        WoundTransitionReductionResult result,
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope requestedAfter)
    {
        var proposedAfter = Assert.IsType<WoundMaterializationEnvelope>(result.ProposedAfter);
        Assert.Equal(
            WoundMaterializationContract.SerializeCanonical(requestedAfter),
            WoundMaterializationContract.SerializeCanonical(proposedAfter));
        var expectedFingerprint = Fingerprint(requestedAfter);
        Assert.Equal(expectedFingerprint, Fingerprint(proposedAfter));
        var history = Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.True(history.Terminal);
        Assert.Equal(expectedFingerprint, history.AfterFingerprint);

        var beforeConsequences = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(before))!["consequences"]!.ToJsonString();
        var terminalConsequences = JsonNode.Parse(
            WoundMaterializationContract.SerializeCanonical(proposedAfter))!["consequences"]!.ToJsonString();
        Assert.Equal(beforeConsequences, terminalConsequences);
    }

    private static string Fingerprint(WoundMaterializationEnvelope wound) =>
        WoundIdentityState.ComputeSemanticFingerprint(wound);

    private static void AssertValid(WoundTransitionReductionResult result)
    {
        Assert.True(result.IsValid, DescribeIssues(result));
        Assert.NotNull(result.ProposedAfter);
        Assert.Empty(result.Issues);
        Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>());
    }

    private static void AssertInvalid(
        WoundTransitionReductionResult result,
        string expectedCode)
    {
        Assert.False(result.IsValid);
        Assert.Null(result.ProposedAfter);
        Assert.Empty(result.Intents);
        Assert.Contains(result.Issues, issue => issue.Code == expectedCode);
    }

    private static string DescribeIssues(WoundTransitionReductionResult result) =>
        string.Join(Environment.NewLine, result.Issues.Select(static issue =>
            $"{issue.Code} {issue.FilePath}: {issue.Expected} / {issue.Actual}"));

    private static string ValidFingerprint(char digit) =>
        "sha256:" + new string(digit, 64);

    private static int SeverityRank(string value) => value switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        "IV" => 4,
        _ => 0
    };
}
