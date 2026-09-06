# Mortal Follow-Up Heal Staging Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans. Steps use checkbox syntax; parent owns acceptance. This is a future plan, not a parallel implementation grant.

**Goal:** Align the pure treatment reducer with #1536 FR-064's already-approved allowance of up to three Mortal severity reductions solely to reach active severity I before a separate heal.

**Architecture:** Extend only the private follow-up staging predicate at its treatment call site. Recovery and spiritual treatment retain their existing two-total-step bound. The terminal heal validator, source authority, outcome publisher and legacy protocol do not change.

**Tech Stack:** C#/.NET 8, xUnit, existing in-memory reducer fixtures, PowerShell 7 bounded runner.

## Global Constraints

- Tracked issue: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open T070 in `specs/1536-complete-wound-materialization/tasks.md`; governance is `.specify/memory/constitution.md` and `AGENTS.md`.
- Work only in the existing `1536-complete-wound-materialization` branch/worktree. No migration, new branch, remote operation or change to unrelated `.serena/` files.
- FR-064 permits aggregate reduction of at most two without heal, or at most three solely to reach working severity I before the separate final heal. This exception applies to Mortal treatment, not to spiritual treatment or generic recovery.
- Staging remains `treat`, active severity I, one terminal attempt and a nonterminal wound history row. It emits a follow-up intent; it must not remove the wound, heal it, archive it, publish legacies or claim full healing support.
- The actual `heal` transition still requires an active severity-I source and terminal healed severity-I after-state, exact accepted-result/provenance, cleared active course and unchanged mechanical/source scope. Do not modify `ValidateHeal` or bypass it.
- Do not relax non-healing reduction, spiritual treatment/recovery, legal realm/domain/owner, exact declared outcome, fresh effect identity, source scope, or old negative tests.
- An unchanged authored graph must genuinely fit the staging rank. Tests must not establish support by pruning the original graph; start with an empty graph or an already-I-compatible one-root graph.
- No new public/serialized field, producer, authority factory, publisher/finalizer, legacy API, game-clock shortcut, effect allocation or client command belongs in this task. The unanswered legacy preparation choice remains unresolved.
- One C# owner only. Run the bounded Focused selection and one meaningful Fast checkpoint; no unbounded test command. Required unfinished legacy-source RED remains visible and is never skipped or waived.

## Source-backed preflight

`WoundTransitionReducer.ValidateTreat` and `ValidateRecover` currently both call
`FollowUpHealStageIsLegal(before, after)`, which accepts only original I/II and
staging I. The separate `ValidateNonHealingSeverityReduction` already imposes
the non-heal two-step cap. `ValidateHeal` independently enforces the final I-only
terminal transition. Therefore the approved Mortal III/IV treatment exception
is missing locally; changing the shared predicate unconditionally would wrongly
raise the spiritual/recovery limit.

Existing `Reduce_FollowUpHeal_RejectsMoreThanTwoSeveritySteps` is a spiritual III
test and must remain unchanged. Existing II-to-I spiritual treatment/recovery,
non-healing caps, exact terminal heal, fresh-root and intent-order tests remain
preservation controls. `CreateSpiritualSeverityWound` currently has only three
profile entries; do not call it with IV or expand it for this task.

`EmptyPhysicalWound` and `WithSeverity` are existing pure test helpers. For the
effectful positive, first construct the one-root I-compatible graph, then create
the original severe wound from it. Only the subsequent before -> staging pair is
the transition under test, and its definition graph must remain identical.

## File map and documentation boundary

- Modify only `BookOfEternityClient/Services/WoundTransitionReducer.cs` and `BookOfEternityClient.Tests/WoundTransitionReducerTests.cs`.
- Parent links this sub-plan/evidence under the open T070 task and updates these checkboxes only after independent review and actual test evidence.
- This is an internal reducer prerequisite for an already-specified but still unfinished healing publisher. It exposes no GM-authored contract, command or runtime healing workflow. Existing Mortal/afterlife prompts, matrix, examples, manifest and source guards require no update for this isolated stage; preserve their explicit unfinished-healing boundary. Record that rationale in the report. No conditional FullValidation is justified by this two-file change alone.

### Task 1: Realm- and operation-specific follow-up staging

- [ ] **Step 1: Add exact positive and preservation tests before changing the reducer.**

Insert the following methods beside the existing follow-up heal tests. Reuse all
existing helpers without changing their behavior or existing assertions.

```csharp
    [Theory]
    [InlineData("III", 3, false)]
    [InlineData("IV", 4, false)]
    [InlineData("III", 3, true)]
    [InlineData("IV", 4, true)]
    public void Reduce_MortalFollowUpHeal_TreatStagesSevereWoundWithoutTerminalPublication(
        string severity, int rank, bool effectful)
    {
        var before = effectful
            ? WithSeverity(WithSeverity(PhysicalWound(), "I", 1), severity, rank,
                updateMaximumAtCreation: true)
            : EmptyPhysicalWound(severity, rank, severity);
        var beforeFingerprint = Fingerprint(before);
        var after = NewTransition(WithSeverity(before, "I", 1) with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var evidence = TreatEvidence(before, after,
            Outcome(after, heals: true, terminalAttempt: true));

        var result = WoundTransitionReducer.Reduce(Request("treat", before, after, evidence));

        AssertValid(result);
        Assert.Equal(beforeFingerprint, Fingerprint(before));
        Assert.Equal(Fingerprint(after), Fingerprint(result.ProposedAfter!));
        Assert.Equal("active", result.ProposedAfter!.Lifecycle);
        Assert.Equal(1, result.ProposedAfter.Severity.Rank);
        Assert.Equal("treat", result.ProposedAfter.LastTransition.Kind);
        Assert.Equal(before.Care.State, result.ProposedAfter.Care.State);
        Assert.Equal("replace", Assert.Single(result.Intents.OfType<WoundCarrierTransitionIntent>()).Operation);
        Assert.Single(result.Intents.OfType<WoundAttemptTerminalIntent>());
        var followUp = Assert.Single(result.Intents.OfType<WoundFollowUpHealIntent>());
        Assert.Equal(before.WoundId, followUp.WoundId);
        Assert.Equal(evidence.AuthorityRef, followUp.AuthorityRef);
        var history = Assert.Single(result.Intents.OfType<WoundTransitionHistoryIntent>());
        Assert.False(history.Terminal);
        Assert.Equal(Fingerprint(after), history.AfterFingerprint);
        Assert.DoesNotContain(result.Intents, intent => intent is WoundArchiveProjectionIntent
            or WoundCosmeticLegacyIntent or WoundIndependentMechanicalLegacyIntent);
        var beforeSources = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(before))!
            ["consequences"]!["ownedEffectSources"]!["definitions"];
        var afterSources = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(after))!
            ["consequences"]!["ownedEffectSources"]!["definitions"];
        Assert.True(JsonNode.DeepEquals(beforeSources, afterSources));
        Assert.Equal(effectful ? 1 : 0, after.Consequences.Entries.Count);
        if (effectful)
        {
            AssertFreshRootIds(before, after);
            var effect = Assert.Single(result.Intents.OfType<WoundEffectTransitionIntent>());
            Assert.Equal("replace", effect.Operation);
            Assert.Equal(EffectIds(before), effect.BeforeEffectIds);
            Assert.Equal(EffectIds(after), effect.AfterEffectIds);
        }
        else
        {
            Assert.DoesNotContain(result.Intents, intent => intent is WoundEffectTransitionIntent);
        }
    }

    [Theory]
    [InlineData("III", 3)]
    [InlineData("IV", 4)]
    public void Reduce_MortalFollowUpHeal_RecoveryRetainsExistingBound(string severity, int rank)
    {
        var before = EmptyPhysicalWound(severity, rank, severity);
        const string tick = "tick_follow_up_mortal";
        var after = NewTransition(WithSeverity(before, "I", 1) with
        {
            Recovery = before.Recovery with { LastTickKey = tick }
        }, "recover");
        var result = WoundTransitionReducer.Reduce(Request("recover", before, after,
            RecoverEvidence(before, after, Outcome(after, heals: true), tickKey: tick)));

        AssertInvalid(result, "wound_transition_follow_up_heal_invalid");
    }

    [Theory]
    [InlineData("II", 2)]
    [InlineData("III", 3)]
    public void Reduce_MortalFollowUpHeal_TreatStillRequiresStagingOne(string severity, int rank)
    {
        var before = EmptyPhysicalWound("IV", 4, "IV");
        var after = NewTransition(WithSeverity(before, severity, rank) with
        {
            Care = before.Care with { LastAttemptId = "attempt_treat" }
        }, "treat");
        var result = WoundTransitionReducer.Reduce(Request("treat", before, after,
            TreatEvidence(before, after, Outcome(after, heals: true, terminalAttempt: true))));

        AssertInvalid(result, "wound_transition_follow_up_heal_invalid");
    }
```

- [ ] **Step 2: Confirm actual semantic RED.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~WoundTransitionReducerTests.Reduce_MortalFollowUpHeal_"
```

Expected: 8 executed, 4 positive rows fail only because the existing follow-up
bound rejects III/IV -> I, and 4 preservation negatives pass. A compile or fixture
failure is not the intended RED. Correct a concrete fixture issue without changing
the required invariant, then observe semantic RED before production changes.

- [ ] **Step 3: Add the narrow private treatment exception.**

Change only `ValidateTreat`'s call to pass the explicit private option:

```csharp
            !FollowUpHealStageIsLegal(before, after, allowMortalTreatment: true))
```

Keep `ValidateRecover`'s two-argument call unchanged. Replace the private predicate:

```csharp
    private static bool FollowUpHealStageIsLegal(
        WoundMaterializationEnvelope before,
        WoundMaterializationEnvelope after,
        bool allowMortalTreatment = false) =>
        after.Severity.Rank == 1 &&
        (before.Severity.Rank is 1 or 2 ||
         allowMortalTreatment && before.Owner.Realm == "mortal_world" &&
         before.Classification.Domain == "physical" && before.Severity.Rank is 3 or 4);
```

Update only the treatment branch's diagnostic expected text to
`"severity-I staging within the realm-specific treatment follow-up heal bound"`.
The issue code is unchanged. No other reducer, validation or publisher behavior
changes. Run the exact Step 2 command: expected 8/8 GREEN.

- [ ] **Step 4: Verify the full owning reducer and one bounded checkpoint.**

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Fast -TimeoutMinutes 5 -Filter "FullyQualifiedName~WoundTransitionReducerTests"
.\scripts\test-csharp.ps1 -Lane Fast
git diff --check
```

The whole owner must pass with existing spiritual III rejection, spiritual II
staging, non-healing two-step limits, terminal heal gates, graph/source scope,
history/provenance and intent-order tests unchanged. Inspect actual TRX rows,
summary, build warnings/errors, cleanup, timeout and duplicates. If Fast stops at
the existing mandatory unfinished legacy-source RED, report exact counts/name and
uncompleted discovery; do not claim a full green Fast or fix that separate task.

- [ ] **Step 5: Report and independently review the bounded change.**

Commit only the two named code/test files; parent owns this plan and Spec Kit
evidence updates. Record task BASE/candidate, exact commands/artifacts, 8-row
RED/GREEN, owning-class result, single Fast result and source/GM boundary. A fresh
task reviewer returns Spec Compliance plus Code Quality; parent inspects source
and actual artifacts before checking this plan. Keep T070 and #1536 open.

## Parent self-review

The exception is closed by exact realm, domain, treatment-only call site, original
rank III/IV and destination I. Existing legal-coordinate validation remains an
independent prerequisite. New effectful positives preserve the complete before
definition graph and require fresh effect identities, while effectless positives
emit no invented effect operation. Terminal heal and legacy semantics are untouched.
The test helper used for spiritual III preservation is not broadened to unsupported
IV fixture shapes. This plan does not resolve either legacy-preparation authority
or the separate spiritual-art storage/specification inconsistency.
