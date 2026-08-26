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

        AssertInvalid(result, "wound_transition_effect_binding_changed");
    }

    [Fact]
    public void Reduce_Complicate_WithoutWorsening_PreservesEveryPriorEffectBinding()
    {
        var before = PhysicalWound();
        var removed = before.Consequences.Entries[1];
        var replacement = removed with
        {
            EffectId = "effect_unrelated_replacement",
            ReadableSummary = "Новый эффект не может заменить прежнюю связь без ухудшения."
        };
        var complication = Complication(
            "complication_declared_addition",
            "pain",
            "effect_declared_complication");
        var after = NewTransition(before with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Consequences = before.Consequences with
            {
                Entries = ImmutableArray.Create(
                    before.Consequences.Entries[0],
                    replacement)
            }
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
        var entries = before.Consequences.Entries.ToList();
        if (addReciprocalConsequence)
        {
            entries.Add(new WoundConsequenceEntry(
                entries.Count + 1,
                "action_control",
                reciprocalEffectId,
                "Добавлен взаимный слот эффекта осложнения."));
        }
        if (addUnrelatedConsequence)
        {
            entries.Add(new WoundConsequenceEntry(
                entries.Count + 1,
                "action_control",
                "effect_unrelated_extra",
                "Посторонний эффект не принадлежит осложнению."));
        }
        var after = NewTransition(before with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Consequences = before.Consequences with
            {
                SlotsUsed = entries.Count,
                Entries = entries.ToImmutableArray()
            }
        }, "complicate");

        var result = WoundTransitionReducer.Reduce(Request(
            "complicate",
            before,
            after,
            ComplicateEvidence(before, after, complication.ComplicationId)));

        if (expectedValid)
            AssertValid(result);
        else
            AssertInvalid(result, "wound_transition_complication_effect_binding_invalid");
    }

    [Fact]
    public void Reduce_Complicate_WithWorsening_AllowsCompleteEffectRematerialization()
    {
        var before = PhysicalWound();
        var complication = Complication(
            "complication_worsening_rematerialization",
            "pain",
            "effect_worsening_complication");
        var rematerialized = before.Consequences.Entries.ToArray();
        rematerialized[0] = rematerialized[0] with
        {
            ProfileKey = "action_control",
            ReadableSummary = "Явное ухудшение полностью перематериализовало эффект."
        };
        var after = NewTransition(WithSeverity(before, "III", 3) with
        {
            Complications = before.Complications.Append(complication).ToImmutableArray(),
            Consequences = WithSeverity(before, "III", 3).Consequences with
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
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(complication),
            Treatment = PhysicalWound().Treatment with
            {
                DiagnosisPaths = ImmutableArray.Create(diagnosisPath),
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
        var evidence = DiagnoseEvidence(
            before,
            after,
            diagnosisPath.DiagnosisPathId,
            diagnosisPath.Reveals.ToArray());

        var result = WoundTransitionReducer.Reduce(Request(
            "diagnose",
            before,
            after,
            evidence));

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
        var before = PhysicalWound() with
        {
            Complications = ImmutableArray.Create(declared, privateFact),
            Treatment = PhysicalWound().Treatment with
            {
                DiagnosisPaths = ImmutableArray.Create(diagnosisPath),
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
            WoundTransitionReducer.Reduce(Request(
                "diagnose",
                before,
                undeclaredAfter,
                DiagnoseEvidence(
                    before,
                    undeclaredAfter,
                    diagnosisPath.DiagnosisPathId,
                    diagnosisPath.Reveals.ToArray()))),
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
            WoundTransitionReducer.Reduce(Request(
                "diagnose",
                before,
                displayLeak,
                DiagnoseEvidence(
                    before,
                    displayLeak,
                    diagnosisPath.DiagnosisPathId,
                    diagnosisPath.Reveals.ToArray()))),
            "wound_transition_diagnosis_display_changed");

        var healedMechanics = NewTransition(WithSeverity(before, "I", 1), "diagnose");
        AssertInvalid(
            WoundTransitionReducer.Reduce(Request(
                "diagnose",
                before,
                healedMechanics,
                DiagnoseEvidence(
                    before,
                    healedMechanics,
                    diagnosisPath.DiagnosisPathId,
                    diagnosisPath.Reveals.ToArray()))),
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
            ProfileKey = "action_control",
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
            }
        }, "stabilize");

        var result = WoundTransitionReducer.Reduce(Request(
            "stabilize",
            before,
            after,
            StabilizeEvidence(before, after)));

        AssertInvalid(result, "wound_transition_retained_consequence_changed");
    }

    [Fact]
    public void Reduce_Stabilize_RejectsComplicationEffectTransferredToConsequenceSlot()
    {
        const string effectId = "effect_binding_transfer";
        var complication = Complication(
            "complication_binding_transfer",
            "pain",
            effectId);
        var before = WithSeverity(PhysicalWound(), "III", 3) with
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

        AssertInvalid(result, "wound_transition_effect_binding_changed");
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
        var before = WithSeverity(SpiritualWound(), "III", 3);
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

        AssertValid(WoundTransitionReducer.Reduce(Request(kind, before, after, evidence)));
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
        var diagnoseAfter = NewTransition(before, "diagnose");
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
                () => WoundTransitionReducer.Reduce(Request(
                    "diagnose",
                    before,
                    diagnoseAfter,
                    new WoundDiagnosisEvidence(
                        "diagnosis_path_null",
                        Fingerprint(before),
                        Fingerprint(diagnoseAfter),
                        null!)))),
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

    private static WoundDiagnosisEvidence DiagnoseEvidence(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        string diagnosisPathId,
        params string[] revealedFacts) => new(
            diagnosisPathId,
            Fingerprint(before),
            Fingerprint(after),
            revealedFacts.ToImmutableArray());

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
            "known_to_player",
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
            "Проверяемый переход материализации раны.",
            intent.Terminal);

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
            LastChangeEventRef = rank == wound.Severity.Rank
                ? wound.Severity.LastChangeEventRef
                : "turn_43:wound_transition"
        },
        Consequences = wound.Consequences with
        {
            SlotBudget = rank,
            SlotsUsed = Math.Min(wound.Consequences.SlotsUsed, rank),
            Entries = wound.Consequences.Entries.Take(rank).ToImmutableArray()
        }
    };

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

    private static int SeverityRank(string value) => value switch
    {
        "I" => 1,
        "II" => 2,
        "III" => 3,
        "IV" => 4,
        _ => 0
    };
}
