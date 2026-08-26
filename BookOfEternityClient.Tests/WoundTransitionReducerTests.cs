using System.Collections.Immutable;
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
        var after = NewTransition(
            WithSeverity(PhysicalWound(), severity, rank, updateMaximumAtCreation: true),
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
        Assert.Equal(WoundTransitionReducer.NonexistentFingerprint, history.BeforeFingerprint);
        Assert.Equal(Fingerprint(after), history.AfterFingerprint);
        Assert.False(history.Terminal);
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
        var complication = Complication("complication_infection", "infection", "effect_infection");
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
    public void Reduce_Diagnose_RevealsOnlyReachableDeclaredRoutesWithoutMechanicalHealing()
    {
        var before = PhysicalWound() with
        {
            Treatment = PhysicalWound().Treatment with
            {
                KnownRouteIds = ImmutableArray<string>.Empty
            }
        };
        var after = NewTransition(before with
        {
            Display = before.Display with
            {
                Prognosis = "Диагностика подтвердила возможность обработки."
            },
            Treatment = before.Treatment with
            {
                KnownRouteIds = ImmutableArray.Create("clean_and_suture")
            }
        }, "diagnose");
        var evidence = DiagnoseEvidence(before, after, "clean_and_suture");

        var result = WoundTransitionReducer.Reduce(Request(
            "diagnose",
            before,
            after,
            evidence));

        AssertValid(result);
        Assert.Equal(before.Severity, result.ProposedAfter!.Severity);
        Assert.Equal(before.Care, result.ProposedAfter.Care);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
    }

    [Fact]
    public void Reduce_Diagnose_RejectsUndeclaredRouteAndAnyMechanicalRegression()
    {
        var before = PhysicalWound() with
        {
            Treatment = PhysicalWound().Treatment with
            {
                KnownRouteIds = ImmutableArray<string>.Empty
            }
        };
        var routeAfter = NewTransition(before with
        {
            Treatment = before.Treatment with
            {
                KnownRouteIds = ImmutableArray.Create("clean_and_suture")
            }
        }, "diagnose");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "diagnose",
                before,
                routeAfter,
                DiagnoseEvidence(before, routeAfter, "other_route"))),
            "wound_transition_diagnosis_fact_unauthorized");

        var healedMechanics = NewTransition(WithSeverity(before, "I", 1), "diagnose");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "diagnose",
                before,
                healedMechanics,
                DiagnoseEvidence(before, healedMechanics))),
            "wound_transition_diagnosis_mechanics_changed");
    }

    [Fact]
    public void Reduce_Stabilize_RemovesOnlyDeclaredComplicationEffectAndRecoveryBlocker()
    {
        var complication = Complication(
            "complication_bleeding",
            "bleeding",
            "effect_bleeding_complication");
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(complication),
            Recovery = PhysicalWound().Recovery with
            {
                Blockers = ImmutableArray.Create("not_stabilized", "unsafe_environment")
            }
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
            Recovery = before.Recovery with
            {
                Blockers = ImmutableArray.Create("unsafe_environment")
            }
        }, "stabilize");
        var evidence = StabilizeEvidence(
            before,
            after,
            removedComplications: new[] { complication.ComplicationId },
            removedEffects: new[] { "effect_bleeding_complication" },
            removedBlockers: new[] { "not_stabilized" });

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            evidence));

        AssertValid(result);
        Assert.Equal("stabilized", result.ProposedAfter!.Care.State);
        Assert.Equal(2, result.ProposedAfter.Severity.Rank);
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
        var evidence = RecoverEvidence(before, after, Outcome(after, heals: true));

        var result = WoundTransitionReducer.Reduce(Request("recover", before, after, evidence));

        AssertValid(result);
        Assert.Contains(result.Intents, intent => intent is WoundFollowUpHealIntent);
        Assert.Equal("active", result.ProposedAfter!.Lifecycle);
        Assert.False(Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).Terminal);
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
        Assert.NotEmpty(effects.BeforeEffectIds);
        Assert.Empty(effects.AfterEffectIds);
        Assert.True(Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).Terminal);
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
        var after = NewTransition(before with
        {
            Relations = before.Relations with
            {
                LegacyRefs = before.Relations.LegacyRefs.Append(cosmetic.LegacyId).ToImmutableArray()
            }
        }, "legacy");
        var evidence = LegacyEvidence(before, after, cosmetic);

        var result = WoundTransitionReducer.Reduce(Request("legacy", before, after, evidence));

        AssertValid(result);
        Assert.Equal("healed", result.ProposedAfter!.Lifecycle);
        Assert.Single(result.Intents.OfType<WoundCosmeticLegacyIntent>());
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
        var after = NewTransition(before with
        {
            Relations = before.Relations with
            {
                IndependentEffectRefs = ImmutableArray.Create(invalid.LegacyId)
            }
        }, "legacy");

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
        var after = NewTransition(before, "archive");
        var evidence = new WoundArchiveEvidence(
            before.LastTransition.TransitionId,
            Fingerprint(before),
            Fingerprint(after));

        var result = WoundTransitionReducer.Reduce(Request("archive", before, after, evidence));

        AssertValid(result);
        Assert.Equal("healed", result.ProposedAfter!.Lifecycle);
        Assert.Single(result.Intents.OfType<WoundArchiveProjectionIntent>());
        Assert.True(Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>()).Terminal);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundCarrierTransitionIntent);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
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

    private static WoundTransitionRequest Request(
        string kind,
        WoundMaterializationEnvelope? before,
        WoundMaterializationEnvelope? after,
        WoundTransitionEvidence evidence) => new(
            kind,
            "wound_transition_test_002",
            "operation_wound_transition_002",
            "turn_43:wound_transition",
            43,
            before,
            after,
            evidence);

    private static WoundCreateEvidence CreateEvidence(
        WoundMaterializationEnvelope after,
        int maximumSeverityRank = 4) => new(
            "opportunity_authority",
            WoundTransitionReducer.NonexistentFingerprint,
            Fingerprint(after),
            after.Origin.OpportunityId,
            maximumSeverityRank,
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
            maximumSeverityRank: 4,
            hasPendingTreatmentOrRecovery: false);

    private static WoundDiagnosisEvidence DiagnoseEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        params string[] reachableRouteIds) => new(
            "diagnosis_authority",
            Fingerprint(before),
            Fingerprint(after),
            reachableRouteIds.ToImmutableArray());

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

    private static WoundMaterializationEnvelope PhysicalWound() => ParseWound(
        WoundContractTestData.CreateActiveWound());

    private static WoundMaterializationEnvelope SpiritualWound() => ParseWound(
        WoundContractTestData.CreateActiveWound(
            woundId: "wound_spiritual_test",
            realm: "chaos_sea",
            ownerKind: "player_soul",
            ownerId: "player_soul_current",
            carrierPath: "game_state/meta/afterlife_entity_profiles.json#/playerSoul",
            domain: "spiritual"));

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
        bool updateMaximumAtCreation = false) => wound with
    {
        Severity = wound.Severity with
        {
            Value = value,
            Rank = rank,
            MaximumAtCreation = updateMaximumAtCreation
                ? value
                : wound.Severity.MaximumAtCreation,
            LastChangeEventRef = "turn_43:wound_transition"
        },
        Consequences = wound.Consequences with { SlotBudget = rank }
    };

    private static WoundMaterializationEnvelope NewTransition(
        WoundMaterializationEnvelope wound,
        string kind,
        int? ordinal = null,
        int turn = 43) => wound with
    {
        LastTransition = new WoundLastTransition(
            "wound_transition_test_002",
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
        wound.Consequences.Entries.Select(static entry => entry.EffectId)
            .Concat(wound.Complications.SelectMany(static value => value.OwnedEffectIds))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static value => value, StringComparer.Ordinal)
            .ToImmutableArray();

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
}
