# T070 Retained Treatment and Diagnosis Parity Correction

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans. One implementation/C# owner; parent inspects actual changes and artifacts before acceptance.

**Goal:** Resolve review I1 before accepting cumulative graph admission or starting selected complication publication.

**Architecture:** Extract two existing contextual validation calls from the canonical wound parser. Canonical parsing keeps those calls at their original positions; detached graph admission supplies current severity/recovery and only remaining real complication facts to the same owners. No extra parser, canonical after-image, invented identity, rule copy, or replacement diagnosis language.

**Tech Stack:** C#/.NET 8, immutable collections, System.Text.Json, xUnit, PowerShell 7.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), `specs/1536-complete-wound-materialization/tasks.md` T070, FR-027/FR-064, `contracts/mortal-wound-treatment.md` and `contracts/wound-effects-and-atomicity.md` under that feature. This is a correction to Task 1 of `2026-09-06-t070-complication-addition-publication.md`, not a new gameplay decision.

**Exact BASE:** `8057a2623cfe584c7b104a1ce94f4f7dffac7a7f`.

**Evidence:** Git metadata `sdd/t070-complication-addition-publication-task-1-review.md` I1 and `sdd/t070-retained-state-parity-correction-preflight.md`, both fully parent-read. These reports are source evidence, not an observed RED. Parent verified original parser sites, graph validation and shared simulation at BASE.

## Global constraints

- Stay in `E:/Games/worktrees/boe-1536-wound-materialization`, branch `1536-complete-wound-materialization`. No `.serena`, remote writes, merges, issue closure, worktree changes or session cleanup.
- The parent owns all existing dirty plan/spec files and this plan. The implementer must not stage them or mark their checkboxes complete.
- Keep the original canonical `Simulate` callback and `Project` contract/fingerprint, T067/T069 identity and policy authority, eight batch properties, thirteen rematerialization proof properties, and six-argument treatment composer unchanged.
- No task 2 publication code, legacy decision, new operation, or weakening of retained treatment/diagnosis to make a transition pass. Required unfinished legacy RED remains explicit.
- The graph is the sole current topology; the detached original baseline supplies immutable retained metadata. No raw GM route reparsing, fake canonical wound/effect IDs or export fallback for all-existing graphs.
- Read the constitution, feature artifacts and `docs/testing.md` before implementation. Use TDD, systematic debugging, review and verification. One bounded C# lane at a time; parent/reviewer do not run C# concurrently.

### Task 1: Restore retained-state validation parity before claims

**Files:**

- Modify `BookOfEternityClient/Services/WoundMaterializationContract.cs` only for the two shared extractions and original call sites.
- Modify `BookOfEternityClient/Services/MortalWoundTreatmentWorkingGraphProjection.cs` only for retained metadata/fact adaptation and calls in `ValidateGraph`.
- Add regressions to `BookOfEternityClient.Tests/MortalWoundTreatmentWorkingGraphProjectionTests.cs`.
- Add real admission regressions to `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationGraphApplicability.cs`.
- Optionally narrowly share the existing root-bound reaction draft fixture in `BookOfEternityClient.Tests/WoundContractTestData.cs`, with its original caller in `MortalWoundTreatmentContractTests.cs` delegating unchanged. Do not alter unrelated fixture defaults.
- Synchronize `OtherGuides/Wound_Materialization_Contract.md`, `Examples/E_CLI_Wound_Materialization.txt` and `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.Wounds.cs` with the exact retained-state guarantee. Existing worked example marker and manifest entry suffice; no new GM grammar.

**Step 1 — Write and observe pure REDs**

- [x] Add `GraphProjection_RetainedReactionDraftMatchesCanonicalReduction` for retained failed-band and retained policy variants. Start with a canonically valid rank-III wound whose current roots fit rank II (`ReadRankThreeWound(false)`). Retain an independently legal two-slot `event_reaction/apply_definition` complication draft. Its complete bound recipe exists at BASE `MortalWoundTreatmentContractTests.cs:3071–3124`, using `WoundContractTestData.CreateApplyDefinitionRoot` and `CreateOwnedEffectDefinition`; root and target have empty links, matching ownership, slots `event_reaction` and `action_control`, target stacking `replace`, and a positive difficulty modifier. The recipe helper is private, not an already public API. Parse the baseline successfully before checking reduction. Canonical `Project` and canonical `Simulate` reject reduce one; graph simulation must reject too. Add an ordinary one-slot retained-draft positive control where both succeed.
- [x] Pin the policy canonical diagnostic exactly: code `mortal_wound_deterioration_policy_invalid`, path `mortalWoundTreatment.severityReductionProjection.provisionalAfter.recovery.deteriorationPolicy.result.complicationDraft.consequenceDefinitions[0].definition.components[0].payload.definitionKey`, expected `wound-owned apply_definition only at severity III or IV`, actual `2`. Require the equivalent graph-path owner diagnostic without suppressing other aggregate issues. Also inspect a direct `WithScalars` rank-II/value-II/budget-2 graph validation so an unrelated simulation failure cannot satisfy the test.
- [x] Add `GraphProjection_SymbolicAdditionDoesNotSkipRetainedRankValidation`: append an effectless unresolved draft and then reduce; retained reaction validation must still fail. Add removal/diagnosis parity with visible effectless `old_complication` referenced by a valid retained diagnosis prerequisite/reveal. Existing grammar examples are `MortalWoundDiagnosisTests.cs:2040–2057,2077–2087`. Canonical removal and graph removal must both fail with `wound_treatment_diagnosis_fact_unknown`, while unreferenced removal and retained-seed addition remain valid. Directly combine the removed graph with a symbolic addition whose local spelling is `old_complication`; it must not resurrect the accepted diagnosis fact.

Run only these new pure names using Focused before production edits. A failing baseline fixture or compile error is not the required semantic RED; repair test construction and observe the actual lost validation gate. Preserve exact artifact paths and outcomes.

**Source-confirmed test alignment during RED:** artifact `234031-357-46532-009034e935b14f1f9466a67acf9c5f9f-focused` has two legal controls passing and four exact missing retained-rank diagnostics, but the diagnosis row initially stops at the existing canonical writer exception. Parent read actual TRX and `WriteTreatment` at BASE3205: direct `TryRemoveComplication` invokes `TryExportExisting` and throws `InvalidOperationException("Cannot serialize an invalid Mortal wound treatment projection.")` for a dangling diagnosis; canonical `Simulate` catches it and returns inapplicable. Assert this exact existing direct-helper exception, canonical Simulate false, and the complete raw canonical after-image Parse diagnosis diagnostic. Then observe the graph's missing diagnosis gate RED; do not broaden production scope to change the direct helper exception API. Existing route `SourcePath` is also authoritative: the graph's retained failed-band diagnostic uses the baseline import `treatmentAttempt.workingWound.treatment.routes[0]...`, not a rewritten caller path. The policy diagnostic still uses the caller policy path. Preserve both behaviors and exact policy code/Expected/Actual.

**Step 2 — Write and observe the real pre-roll RED**

- [x] Add `ComplicationGraphApplicability_RetainedRankValidationRejectsBeforeDieClaim(bool retainedPolicy)`. Use `CreateDestinationReductionScenario("retained_rank", "resistance_modifier")` in the existing resolver partial: rank III, one active slot, success reduces one. Put the legal reaction addition in the failed result or retained `untreated_infection/not_stabilized/30/10` policy, respectively. Add a legal second known route differing only in success result (`add_recovery` one point), then rebuild current history. Assert accepted baseline validity before the request. The reducing request must be null with `mortal_wound_treatment_procedure_band_inapplicable`; assert the entire resolver fixture tree unchanged. The legal route subsequently owns source index `[0]` and exactly one held `sterile_thread` claim. Use the existing real fixture/check/resource helpers, not a mock callback or manually restored die. Run this exact new Integration theory before production edits and record the semantic RED.

**Step 3 — Share the existing canonical owner and adapt current facts**

- [x] Extract these two internal helpers into `WoundMaterializationContract`, leaving their canonical callers at the same positions and preserving exact ordinal domain/realm/nonempty-target/null-policy gates, parse/build dispatch, diagnostics and ordering:

```csharp
internal static void ValidateRetainedTreatmentProjection(
    WoundTreatment treatment, string treatmentPath,
    string domain, string realm, string ownerTargetKind, int severityRank,
    IReadOnlyList<WoundComplication> complications,
    JsonElement? deteriorationPolicy, List<ValidationIssue> issues);

internal static void ValidateRetainedDeteriorationPolicy(
    JsonElement? deteriorationPolicy, string policyPath,
    string domain, string realm, string ownerTargetKind, int severityRank,
    List<ValidationIssue> issues);
```

The first body is the existing `ParseTreatment` contextual block at BASE2033–2045, calling `MortalWoundTreatmentContract.ParseProjection` with the supplied policy. The second is the existing Parse block at BASE413–424, calling `MortalWoundDeteriorationPolicyContract.Parse`. Keep both existing owners and their signatures unchanged. Do not move component-power validation into ordinary canonical Parse.

- [x] After the existing `ValidateWorkingGraphBoundary` succeeds in `ValidateGraph`, adapt only retained real facts:

```csharp
var retainedReferences = Graph.Complications
    .Where(row => IsExistingCoordinate(row.Reference))
    .Select(row => row.Reference).ToHashSet();
var remainingCanonicalComplications = _baseline.Complications
    .Where(row => retainedReferences.Contains(
        WoundWorkingReference.Existing(row.ComplicationId)))
    .ToImmutableArray();
var targetKind = WoundMaterializationContract.ResolveEffectTargetKind(
    _baseline.Owner.OwnerKind);
```

Call the treatment helper with `_baseline.Treatment`, `path + ".treatment"`, baseline classification domain/owner realm, `targetKind`, **current** `Scalars.Severity.Rank`, filtered complications, and **current** `Scalars.Recovery.DeteriorationPolicy`. Call the policy helper with the same current state and `path + ".recovery.deteriorationPolicy"`. Preserve the existing boundary/common-definition/owned-graph/adapter validation sequence and its error gating. Reuse the existing internal target-kind mapping, not another switch.

This filter intentionally tests the complete `Existing(id, -1, -1)` coordinate, not string value alone. Current approved operations remove whole complications or append new ones, and scalar operations do not mutate retained payload/visibility. Thus detached baseline rows for exact surviving IDs supply the current canonical diagnosis facts. No symbolic reference can satisfy an accepted-ID fact. Do not rewrite diagnosis or route data after removal. If a required invariant is contradicted by actual source, report that narrow conflict before changing the architecture.

**Step 4 — Synchronize worked guidance and verify**

- [x] Next to the existing complete cumulative-admission paragraph in both worked documents, add and guard this exact text: `The same canonical rules revalidate retained treatment routes and deterioration policy at the resulting severity, and diagnosis facts against remaining accepted complications. A symbolic local reference never restores a removed accepted diagnosis fact.` Retain the complete existing worked JSON and its executable guard; it remains the ordinary legal positive control. Inspect afterlife matrix/examples/manifest/guards and record that no afterlife authored field or spiritual mechanic changes, while shared parser preservation is verified. No manifest marker churn or invented new payload.
- [x] Run new pure and Integration rows GREEN, then the coherent owner selections below. One meaningful Fast and one conditional FullValidation are required for this real shared-validation correction; prior controls preceding the correction cannot replace them. No duplicate unchanged broad runs, PreMerge, DeepValidation or LifecycleIntegration. On a failure diagnose the exact cause; do not delete/skip tests or silently widen scope.

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentWorkingGraphProjectionTests|FullyQualifiedName~WoundMaterializationContractTests|FullyQualifiedName~MortalWoundTreatmentContractTests|FullyQualifiedName~MortalWoundDeteriorationPolicyContractTests|FullyQualifiedName~MortalWoundDiagnosisTests|FullyQualifiedName~PromptDocumentationCoverageTests.WoundTreatment" -TimeoutMinutes 10
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalWoundTreatmentResolverTests.ComplicationGraphApplicability_" -TimeoutMinutes 10
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
git diff --check
```

The pure union includes the known unfinished legacy row in `MortalWoundTreatmentContractTests`; keep its required RED explicit and report actual per-test coverage rather than claiming full success. No extra Fast immediately before any future PreMerge. Focused 10 minutes bounds this multi-owner union; FullValidation keeps 15 minutes, Fast 5 minutes. A justified measured limit change requires an explicit note, not timing micro-optimization.

**Step 5 — Commit and hand off for independent review**

Parent-approved scheduling: after fresh new-row/coherent owner/Integration/afterlife
controls show no new failure (required legacy RED remains explicit), a scoped LOCAL
candidate commit and preliminary report may precede Fast/FullValidation so the
independent exact-range read-only review runs alongside those broad controls. This
is not acceptance or a claim that broad lanes passed. The implementer retains sole
C# ownership and completes both controls/report. Any subsequent code delta needs
incremental independent review and the relevant fresh verification before acceptance.

- [x] Inspect exact diff/status, commit only this correction's allowed code/test/GM files with `(#1536) [skip ci]`, and write `sdd/t070-retained-state-parity-correction-report.md`. Include exact BASE/HEAD, all actual artifacts including failed fixture/semantic runs, counts/elapsed/build errors/warnings/cleanup/timeouts/duplicates, source assumptions, canonical diagnostic preservation and documentation/no-update rationale. Distinguish unfinished legacy from new failures and do not claim whole Task1, T070, #1536 or merge readiness. Parent validates actual artifacts and obtains a fresh exact-range independent Spec+Quality review before accepting this correction or starting addition Task2.

## Parent self-review before dispatch

The parent checked both existing canonical blocks and the original graph constructor/removal/append/scalar paths at8057. Current graph operations preserve surviving canonical complication payload/visibility, and the full tagged-reference filter cannot confuse additions with accepted IDs. Helpers share existing route/diagnosis/policy validation without changing canonical parse order; no new authority path or gameplay choice is introduced. Tests cover both retained-rank sources, unresolved additions, dangling diagnosis and real die/resource admission. The old helper recipes are explicitly private, and shared test extraction is narrowly allowed. Required REDs are not yet observed; all checkboxes remain open. The prior broad-verification exception applied only to malformed-input diagnostic bookkeeping and is not reused for this semantic change.

## Parent acceptance — 2026-09-07

Accepted exact `8057a262..ff5ddcb8` after actual complete diff/report review, all10
runner artifact audits and independent exact-range Spec compliant / quality Approved
review with no findings. I1's retained rank and remaining diagnosis axes have real
RED-to-GREEN evidence. The above self-review/open statements describe the historical
pre-dispatch state; all nine bounded correction steps are now complete.

Fresh final controls: owner463/464 only required untouched legacy RED; full graph
admission Integration20/20; Afterlife121/121; FullValidation1856/1856 in9:04.8574487.
ONE Fast6006/6007 stopped at that same legacy RED in3:34.2528254; no full Fast claim.
Actual summaries, all completed TRXs, membership and build logs show clean builds/
cleanup, no timeout, duplicates or skips. Source/GM files did not change after the
reviewed local candidate. Metadata reports are `t070-retained-state-parity-correction-report.md`,
`t070-retained-state-parity-correction-review.md` and parent actual evidence audit
`t070-retained-state-parity-parent-artifact-audit.md` under the worktree's git sdd.

This accepts original addition Task1 together with its correction, not selected-add
publication, T070/T177, #1536 or merge readiness. The unfinished legacy publisher and
unanswered private legacy-preparation choice remain explicit. No remote mutation.
