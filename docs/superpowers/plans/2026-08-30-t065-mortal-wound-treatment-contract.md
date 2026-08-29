# T065 Mortal Wound Treatment Contract Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `executing-plans` to implement this plan task-by-task.

**Goal:** Replace opaque Mortal wound diagnosis and treatment JSON with one strict, readable, canonically serializable version-1 contract that rejects unreachable discoveries, malformed route modes, ambiguous selectors, inert success branches, and unsafe complication or legacy drafts.

**Architecture:** Keep `WoundMaterializationContract` as the owner of the complete wound envelope and canonical writer. Introduce `MortalWoundTreatmentContract` as a pure structural parser/validator for the nested treatment surface. It returns immutable typed members plus path-specific validation issues; the outer wound parser supplies the current complications and recovery policy needed for same-wound reference validation. Attempt-time authority, applicability, commands, reducers, history, capability resolution, and publication remain outside this slice.

**Tech Stack:** C#/.NET 8, `System.Text.Json`, immutable records, existing `ValidationIssue` vocabulary, xUnit, PowerShell 7 bounded test lanes.

**Tracked task:** GitHub issue #1536, Spec Kit task T065 in `specs/1536-complete-wound-materialization/tasks.md`.

**Global constraints:** No migration, compatibility reader, name-derived treatment catalog, GM-authored canonical IDs, raw mutation authority, or runtime orchestration. Preserve exact authored order. Use `apply_patch` for edits and only `pwsh -NoProfile -File .\scripts\test-csharp.ps1` for C# verification. Do not mark T065 complete until diffs and fresh evidence are independently reviewed. `.serena/` is unrelated user state and must remain untouched.

## Contract references

- `specs/1536-complete-wound-materialization/spec.md`
- `specs/1536-complete-wound-materialization/plan.md`
- `specs/1536-complete-wound-materialization/data-model.md`
- `specs/1536-complete-wound-materialization/contracts/mortal-wound-treatment.md`
- `specs/1536-complete-wound-materialization/tasks.md` (T058, T059, T061, T065)
- `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`
- `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs` parser tests through `Parse_DiagnosisPathRequiresReadableClosedVersionOneShape`
- `BookOfEternityClient/Services/WoundMaterializationContract.cs`

The existing RED baselines are:

- `MortalWoundTreatmentContractTests`: 153 total, 29 pass, 124 fail; `TestResults/test-lanes/20260830-093538-110-23776-b46bd7e2cc864ebdaac7b30ab6205f63-focused`.
- `MortalWoundDiagnosisTests`: 97 total, 3 pass, 94 fail; `TestResults/test-lanes/20260830-093655-118-7988-990b53efe05f4af69548bca11a4740d1-focused`. Only the parser tests at lines 20–480 are T065; command/history/reducer tests remain intentionally RED for T067.

---

### Task 1: Establish the typed treatment boundary and common route envelope

**Files:**

- Create: `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs`
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- Modify: `BookOfEternityClient.Tests/WoundContractTestData.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`

**Step 1: Select the common-envelope RED tests**

Run the exact focused class and record failures for:

- `Parse_CompleteMortalProcedureRouteRequiresEveryMechanicalSection`
- `Parse_IncompleteMortalProcedureRouteIsRejected`
- `Parse_TreatmentTextCannotBeTheOnlyNonDisplayMortalImpact`
- `Parse_RequirementsWithinOneRouteAreConjunctiveNotNestedAlternatives`
- `Parse_SeparateCompleteRoutesAreAlternativesButInvalidSiblingFailsWholeWound`
- `Parse_ClosedModeCannotReuseAnotherModesResolutionShape`
- `Parse_RouteModeIsClosedAndOrdinal`
- `Parse_ProcedureMechanicalObjectsAreClosed`
- `Parse_ExactVersionOneTreatmentRouteShapeIsAccepted`
- `Parse_TreatmentRouteIdsAreExactCaseAndUnicodeConfusableUnique`

Command:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentContractTests"
```

**Step 2: Add immutable typed members and a pure parse result**

Create internal version-1 records for:

- common route envelope;
- exact typed requirement predicates;
- resource consumption policy/selectors;
- procedure, course, and guaranteed resolution discriminators;
- ordered outcomes and typed operations;
- complication and heal-legacy proposal drafts.

The nested parser must receive the outer source path, current complications, and nullable current deterioration-policy reference. It must never inspect wound names, display text, or setting vocabulary.

**Step 3: Close the common envelope**

Require exactly `routeId`, `displayName`, `visibility`, `mode`, `requirements`, `resourcePolicy`, `resolution`, `outcomes`, and `interruption`. Accept only `public|known_to_player|hidden`, reject `gm_only`, validate exact/confusable route-ID uniqueness, cap collections at their version-1 bounds, and keep requirements conjunctive without nested alternatives.

Validate each requirement as one closed typed predicate from the contract vocabulary. Keep structural kind validity separate from T066 attempt-time satisfaction.

**Step 4: Delegate canonical writing to typed members**

Change `WoundMaterializationContract.ParseTreatment` to invoke the nested parser and append its issues. Change `WriteTreatment` to write the typed model in authored order. An accepted round trip must not retain arbitrary unknown JSON.

Replace the old opaque/string-token route in the shared active-wound fixture with the exact version-1 procedure shape. This is test data, not a compatibility branch: the strict parser must reject the former draft shape, and no production migration is permitted.

**Step 5: Verify and commit the slice**

Run the focused class, inspect `git diff --check`, and commit only the common boundary:

```powershell
git add BookOfEternityClient/Services/MortalWoundTreatmentContract.cs BookOfEternityClient/Services/WoundMaterializationContract.cs BookOfEternityClient.Tests/WoundContractTestData.cs
git commit -m "feat(wounds): type mortal treatment routes (#1536)"
```

---

### Task 2: Implement readable diagnosis paths and fixed-point discovery

**Files:**

- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs`
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundDiagnosisTests.cs`

**Step 1: Run only the parser-owned diagnosis contour**

Use method-level filters or the full class with a documented split. T065 owns parser tests from `Parse_ReachableHiddenRoutePreservesReadableDiagnosisGraph` through `Parse_DiagnosisPathRequiresReadableClosedVersionOneShape`; it does not own later `CommandParsing`, `History`, `Repair`, or `Reduce` failures.

**Step 2: Parse the closed readable path**

Require exactly `diagnosisPathId`, `displayName`, `visibility`, `requiresKnownFacts`, `requirements`, `check`, `reveals`, and `failurePolicy`. Accept only exact typed `route:<id>` and `complication:<id>` facts resolving inside the same wound. Preserve order; cap known prerequisites, world requirements, and reveals independently at 16; reject exact and Unicode-confusable duplicates where specified; require `failurePolicy=no_reveal`.

**Step 3: Validate known/completed route agreement**

Require every known/completed ID to resolve exactly, completed to be a subset of known, every visible route to be known, hidden routes to remain authored hidden after diagnosis, and no `gm_only` route or reachability seed.

**Step 4: Compute the least fixed point**

Seed it from `knownRouteIds` and player-visible complications. Repeatedly activate player-reachable diagnosis paths whose complete prerequisite set is known, adding their ordered reveals until stable. Hidden paths require at least one prerequisite. Reject self-only discovery, unseeded cycles, foreign facts, and every hidden route absent from the final fixed point.

**Step 5: Verify canonical order and commit**

Run the parser-owned diagnosis tests, then the complete treatment class to catch shared-envelope regressions. Commit:

```powershell
git add BookOfEternityClient/Services/MortalWoundTreatmentContract.cs BookOfEternityClient/Services/WoundMaterializationContract.cs
git commit -m "feat(wounds): validate diagnosis discovery graph (#1536)"
```

---

### Task 3: Implement procedure bands and the closed outcome algebra

**Files:**

- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs`
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`

**Step 1: Lock the procedure RED subset**

Target the tests for registered formula, exact fixed-zero/resolved-skill modifier source, inclusive signed-int difficulty, critical policy, exact/confusable band IDs, 2–16 best-to-worst gapless bands, monotone categories, and closed typed operations.

**Step 2: Parse and validate the procedure contour**

Accept only `mortal_wound_procedure_v1`, checked signed-int difficulty, the two closed modifier-source variants, and the specified critical policy. Require null outer bounds at the extremes, checked adjacency, no overlaps/gaps, first category `success`, last `failed_attempt`, and monotone category order.

**Step 3: Implement the typed operation union**

Support only `no_improvement`, `stabilize`, `add_recovery`, `reduce_severity`, `remove_complication`, `add_complication`, `apply_deterioration`, and `heal`, each with exact fields and bounds. `no_improvement` is singleton. Every categorized procedure success band must contain a structurally positive operation and no adverse/no-op operation. Do not perform current-wound applicability simulation here; T066 owns that attempt-time proof.

**Step 4: Enforce canonical/proposal selector dialects**

Canonical input accepts permanent `complicationId` and rejects `complicationRef`. Proposal parsing accepts only `complicationRef`, which must resolve within the current proposal or an explicit bounded existing-selector namespace before being rewritten by the client. Reject unresolved, duplicate, cross-wound, and Unicode-confusable local references.

**Step 5: Verify and commit**

Run `MortalWoundTreatmentContractTests`, inspect canonical round trips and `git diff --check`, then commit:

```powershell
git add BookOfEternityClient/Services/MortalWoundTreatmentContract.cs BookOfEternityClient/Services/WoundMaterializationContract.cs
git commit -m "feat(wounds): validate treatment outcome algebra (#1536)"
```

---

### Task 4: Implement course, guaranteed, interruption, complication, and heal-legacy shapes

**Files:**

- Modify: `BookOfEternityClient/Services/MortalWoundTreatmentContract.cs`
- Modify: `BookOfEternityClient/Services/WoundMaterializationContract.cs`
- Test: `BookOfEternityClient.Tests/MortalWoundTreatmentContractTests.cs`

**Step 1: Implement canonical-minute courses**

Require 1–32 ordered milestones with contiguous ordinals and strictly increasing positive canonical `afterMinutes`. Each ordinary milestone result is non-empty and positive-only. The final milestone is non-empty and the complete course contains at least one applicable positive operation kind; enforce at most one final heal and aggregate severity/legacy bounds structurally.

**Step 2: Close interruption**

Require the sole adverse course branch to be structurally non-beneficial: either `no_improvement`, one exact current-policy `apply_deterioration`, or an effectless one-complication proposal with positive treatment difficulty 1–4. It may not stabilize, improve recovery/severity, remove a complication, heal, or smuggle effect definitions.

**Step 3: Close guaranteed mode**

Require one exact capability source reference, one singleton `success` outcome, no interruption, and at least one positive operation. Reject course/procedure resolution members and any adverse/no-op result.

**Step 4: Reuse existing safe proposal subcontracts**

For `add_complication`, reuse the complete bounded GM-safe complication/consequence sub-proposal: one proposal-local complication ref, closed complication shape, definitions/root bindings with exact local namespace, and no parallel draft dialect. For `heal`, accept only closed cosmetic or mechanical-effect `wound_legacy` drafts, enforce per-draft and aggregate definition/application limits, local-reference isolation, at-most-one heal, and non-public materialization semantics. Reject caller-authored canonical recovery or deterioration-anchor fields; proposal values must be absent/null and client-owned.

**Step 5: Verify and commit**

Run the complete treatment contract class and commit:

```powershell
git add BookOfEternityClient/Services/MortalWoundTreatmentContract.cs BookOfEternityClient/Services/WoundMaterializationContract.cs
git commit -m "feat(wounds): close treatment mode payloads (#1536)"
```

---

### Task 5: Integrate, review, and close T065 only

**Files:**

- Modify: `specs/1536-complete-wound-materialization/tasks.md`
- Modify: `specs/1536-complete-wound-materialization/quickstart.md` only if it is the existing evidence ledger for this phase

**Step 1: Run focused controls**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundTreatmentContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~MortalWoundDiagnosisTests"
```

Expected: the complete treatment class is GREEN. In diagnosis, every `Parse_*` test through line 480 is GREEN; remaining failures must map only to T067 command/history/repair/reducer ownership.

**Step 2: Run neighboring contract controls**

Run the smallest filters covering wound canonical serialization, complication/effect source validation, recovery policy, and T064 occurrence replay. Diagnose any new failure before broadening the lane.

**Step 3: Run one meaningful Fast checkpoint**

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Compare the normalized failure set against the pre-T065 baseline. Only previously declared T066–T070 RED tests may remain; no unrelated failure, duplicate ID, timeout, warning, or cleanup failure is acceptable. Increase the documented lane time limit if evidence shows the suite legitimately exceeds an old bound; do not distort implementation to save seconds.

**Step 4: Audit GM/docs boundaries**

Record that this slice changes strict authoring validation but introduces no reachable player command, pending/control file, response field, status projection, or publication path. Update GM-facing examples/docs/source guards only if the final diff exposes a GM-authored shape beyond the already-linked feature contract; otherwise record the explicit no-update rationale. Recheck Mortal World and afterlife independently.

**Step 5: Obtain independent review and mark only T065 complete**

Review exact scope ownership, canonical/proposal dialect separation, fixed-point reachability, numeric bounds, typed serialization, and tests. After inspecting the final diff and fresh evidence, change only T065 to `[X]`; leave T066–T070 open. Commit the evidence/task ledger separately.

Do not run PreMerge, push, open a PR, merge, or close issue #1536 in this task; those actions belong to final feature completion unless the user explicitly requests them.
