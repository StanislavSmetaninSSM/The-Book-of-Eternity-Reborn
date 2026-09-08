# Wound Broad-Validation Boundary Correction Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use the parent-owned superpowers:subagent-driven-development controller for this single bounded implementation task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Restore the reviewed Integration broad-validation boundary after #1536 added three zero-argument validation calls, while retaining the one call whose assertions genuinely require complete-state output.

**Architecture:** Convert the ordinary skill-catalog preflight and the severity failure diagnostic to two named, assertion-owned Integration validation profiles. Keep the publication filter oracle parameterless, add `FullValidation` only to its four exact calling methods, and extend the existing syntax-aware boundary manifest from seven to eight reviewed calls. The companion patch is complete and lives at `docs/superpowers/plans/2026-09-08-wound-broad-validation-boundary.patch`.

**Tech Stack:** C#/.NET 8, xUnit, Roslyn syntax inspection, existing `GameStateValidationSelection`, PowerShell 7 bounded C# lanes.

## Global Constraints

- Tracked authority is [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), open Spec Kit task T177. Before applying this plan, the parent creates/records the approved nested maintenance task under T177; this plan itself makes no repository implementation change.
- Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/1536-complete-wound-materialization/{spec,plan,tasks}.md`, and `docs/testing.md` before implementation. Preserve the issue/spec/task links and do not mark T177 or #1536 complete from this slice.
- Work only in `E:/Games/worktrees/boe-1536-wound-materialization` on `1536-complete-wound-materialization`. Preserve unrelated `.serena/`. Do not create another branch, push, open/merge a PR, close an issue, clean sessions, or perform remote actions.
- Modify exactly the five Integration test-support/source files listed below. Do not change production validation behavior, runner filters, lane timeouts, project membership, behavioral assertions, test rows, or the seven previously reviewed broad-validation calls.
- Keep `CreatePublishedTreatmentFullStateProbeAsync` as a real parameterless `ValidateGameStateAsync()` call. Its complete returned issue list and `Assert.Single(errors)` are part of the exact NPC inventory publication proof.
- Do not place `FullValidation` on any `GameEngineTurnLifecycleTests` partial class. Add it only to the four exact test methods that call the full-state helper; preserve their inherited `LifecycleIntegration` ownership.
- Do not disguise parameterless calls with string concatenation, aliases, reflection, wrappers, or syntax tricks. Do not exempt LifecycleIntegration from the broad-call guard. Do not raise the reviewed budget for the two calls that have no full-state assertion.
- One C# owner runs the exact Focused commands sequentially at the default five-minute limit. If the coherent 12-row owner selection actually times out, retain the same filter and report actual diagnostics to the parent before a measured bounded override; do not narrow coverage to win seconds.
- The parent owns the later combined Fast checkpoint and T177's required FullValidation. Do not rerun FullValidation, LifecycleIntegration, RegressionIntegration, PreMerge, or another expensive broad lane for this maintenance slice.
- Test-only lane/profile maintenance changes no game capability, command, mechanic, state field, validator/normalizer behavior, pending/control surface, response, receipt, or GM-authored output. Mortal World and afterlife prompts, examples, manifests, matrices, daemon context packs, and GM source guards require no update.

---

## Diagnosis and chosen boundary model

The retained artifact
`TestResults/test-lanes/20260908-191851-547-28128-e81df7516e2e46e09e5a20299287550e-focused`
is a valid RED: default five-minute Focused Integration completed 60/60 rows in
`00:00:41.1512485`, with 58 passing and exactly these two failures, no timeout,
no duplicates, and successful owned-tree cleanup:

- `BroadValidationCalls_MatchReviewedSevenCallManifest` observed ten calls against the reviewed seven-call/five-file manifest.
- `IntegrationAndSupportBroadValidationSources_AreExplicitlyCategorized` named the three new physical source files because none contained an explicit `FullValidation` trait.

The calls predate B0: blame places them at `302a66ab` (published treatment full-state probe), `5a7c0acf` (severity failure diagnostic), and `6b511724` (ordinary catalog preflight), while the boundary dates to August. `bd8f3d74..4de64a34` changed only the pending-snapshot presence feature plus its one manifest registration; it did not touch these sources. After the parent accepted B0 fixture commit `084146ea`, all five proposed files remain byte-unchanged from `4de64a34`; only B0 fixture/docs files changed. The boundary run therefore exposed accumulated branch debt rather than a B0 runtime regression.

| Call | Actual contract | Correction |
|---|---|---|
| `EffectSkillScopeLifecycleTests.cs` ordinary GameEngine catalog preflight | It copies the known base session, writes only `skills_active.json` and `skill_mastery.json`, and asserts that those inputs are valid before checking the player-only catalog. It does not assert global issue completeness. | Use new `EffectSkillScopeCatalog = PlayerStateFiles | SkillContractConsistency`; keep the preflight assertion unchanged. |
| `MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs` owner-carrier parity | The call executes only when `AcceptedTurnValidationDisposition` is not accepted and contributes text to the assertion message. It is not the disposition oracle. The relevant failure surface is the player/NPC/combatant carrier set, skill binding, accepted effect/wound continuity, and control files. | Use new `MortalWoundTreatmentLifecycle` with exactly those phases. The accepted/rejected assertion and every publication assertion remain unchanged. |
| `MortalWoundTreatmentResourcePublicationLifecycleTests.cs` full-state probe | Four methods/eight rows depend on the complete issue list: the helper requires exactly one global error, requires its exact `npc_existing_inventory_resend_forbidden` code, and passes that list through exact publication-proof filtering/adversarial cases. | Preserve the zero-argument call, add one reviewed manifest entry, and add method-level `FullValidation` to the exact four callers only. |

The resulting model has eight parameterless calls in six exact files: the original
seven plus this one reviewed publication oracle. The two scoped calls are pinned by
the existing Roslyn profile-source guard. The retained full-state helper is pinned
by an exact caller manifest which proves: (1) precisely four methods call it, (2)
precisely those methods carry method-level `FullValidation`, and (3) the containing
partial class does not carry `FullValidation`. Its row count is eight: facts 1 + 1 +
1 and the five-row adversarial theory.

Rejected alternatives:

- Updating seven directly to ten leaves a setup preflight and a failure-only diagnostic in the expensive complete-state contour without assertion need.
- Replacing the publication helper call with a profile weakens its complete-state single-error oracle and could make unrelated errors disappear before exact filtering.
- Adding `FullValidation` to a partial class attributes the compiled `GameEngineTurnLifecycleTests` type and recategorizes the entire large lifecycle suite.
- Accepting `LifecycleIntegration` as a source-wide exemption weakens the established rule that every retained parameterless call has explicit complete-validation ownership.
- Hiding the call from Roslyn or the regex repairs the guard symptom while preserving unreviewed work.

## File map

- Modify/test `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs`: add the two exact profiles; no production API.
- Modify/test `BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs`: pass `EffectSkillScopeCatalog` to the ordinary catalog preflight.
- Modify/test `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs`: pass `MortalWoundTreatmentLifecycle` only to the rejection diagnostic.
- Modify/test `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`: add `FullValidation` to the four full-state helper callers; preserve their bodies and helper unchanged.
- Modify/test `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`: pin profile names/phases, scoped-source mappings, exact helper callers/method categories, and the eight-call/six-file manifest plus ninth-call negative control.
- Parent owns task recording, diff/evidence inspection, independent review, the combined Fast checkpoint, later required FullValidation, Spec Kit evidence, and acceptance.

### Task 1: Correct and seal the broad-validation ownership boundary

**Files:**
- Modify/test: `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs`
- Modify/test: `BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs`
- Modify/test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs`
- Modify/test: `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs`
- Modify/test: `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs`

**Interfaces:**
- Consume: `ValidationService.ValidateGameStateAsync(GameStateValidationSelection)`, `GameStateValidationPhase`, existing `ActorAndAfterlifeScopedProfileViolations`, `ParameterlessValidationCallLocations`, `MethodCategoryTraits`, `CategoryTraits`, and `InvokedMemberName`.
- Produce: internal test-only profiles `IntegrationValidationProfiles.EffectSkillScopeCatalog` and `IntegrationValidationProfiles.MortalWoundTreatmentLifecycle`; reviewed eight-call/six-file manifest; exact four-method/eight-row FullValidation caller ownership.
- Preserve: every test name outside the manifest's three Seven/Eighth names, every behavioral assertion and row, the parameterless helper body, class-level LifecycleIntegration, runner/lane contracts, and production code.

- [X] **Step 1: Adopt the existing actual boundary failure as RED; do not manufacture another defect.**

Inspect `summary.json`, `dotnet-test.log`, and the TRX from the retained artifact.
Required RED is 60 total/executed, 58 passed, 2 failed, with observed ten versus
seven plus the exact three uncategorized paths. The parent has already accepted
this as the genuine RED. Do not add a throwaway test, weaken an assertion, or spend
another C# run reproducing identical evidence. If current sources no longer match
the ten call sites or the five proposed files changed after `084146ea`, stop and
report the exact diff before applying the patch.

- [X] **Step 2: Apply the complete companion patch exactly.**

Use `apply_patch` with the complete contents of:

```text
E:/Games/worktrees/boe-1536-wound-materialization/docs/superpowers/plans/2026-09-08-wound-broad-validation-boundary.patch
```

The patch contains every source edit. In particular, the new profile bodies are:

```csharp
internal static readonly GameStateValidationSelection EffectSkillScopeCatalog = Select(
    GameStateValidationPhase.PlayerStateFiles |
    GameStateValidationPhase.SkillContractConsistency);

internal static readonly GameStateValidationSelection MortalWoundTreatmentLifecycle = Select(
    GameStateValidationPhase.RequiredFields |
    GameStateValidationPhase.CrossReferences |
    GameStateValidationPhase.PlayerStateFiles |
    GameStateValidationPhase.NpcStateFiles |
    GameStateValidationPhase.SkillContractConsistency |
    GameStateValidationPhase.WorldQuestCombatFactionStateFiles |
    GameStateValidationPhase.MetaMiscStateFiles |
    GameStateValidationPhase.AcceptedTurnEffectMaterializationCompleteness |
    GameStateValidationPhase.AcceptedTurnWoundMaterializationCompleteness |
    GameStateValidationPhase.ClientOwnedControlFiles);
```

Do not add item materialization to the severity diagnostic: this exact owner-parity
case supplies no item and asserts wound/effect generation. Do not remove any phase
listed above without first proving the omitted player/NPC/combatant, skill,
continuity, or control surface is outside the method's assertions.

- [X] **Step 3: Run the boundary class GREEN at the default five-minute limit.**

```powershell
.\scripts\test-csharp.ps1 `
  -Lane Focused `
  -FocusedProject Integration `
  -Filter "FullyQualifiedName~IntegrationTestBoundaryTests"
```

Expected: exactly 60/60 PASS, no skips, duplicate IDs, timeout, build warning/error,
or cleanup failure. The row count stays 60 because existing Facts are strengthened
and renamed; no boundary Fact is added. The renamed positive/negative manifest
methods must prove eight exact calls in six exact files and reject a ninth call.
The category Fact must prove the full-state helper has exactly four callers, those
four and only those four methods have method-level `FullValidation`, and the partial
class has none.

- [X] **Step 4: Run all affected behavioral owners GREEN in one exact 12-row Focused selection.**

```powershell
$ownerFilter = @(
  "FullyQualifiedName~GameEngineTurnLifecycleTests.EffectSkillScopeLifecycleTests_TurnRequestCatalog_OrdinaryGameEnginePublishesCanonicalCatalog"
  "FullyQualifiedName~GameEngineTurnLifecycleTests.SeverityReduction_OwnerCarrierParityUsesAcceptedNearbyCanonicalTarget"
  "FullyQualifiedName~GameEngineTurnLifecycleTests.GuaranteedItemConsumption_ExactPublishedNpcInventoryPassesFullStateAndRetry"
  "FullyQualifiedName~GameEngineTurnLifecycleTests.GuaranteedItemConsumption_FullStateInventoryIssueRequiresExactOpenPublicationProof"
  "FullyQualifiedName~GameEngineTurnLifecycleTests.GuaranteedItemConsumption_FullStateInventoryIssueFilteringRequiresExactlyOneProvenMatch"
  "FullyQualifiedName~GameEngineTurnLifecycleTests.GuaranteedItemConsumption_SameTurnItemNormalizationSurvivesPublication"
) -join "|"

.\scripts\test-csharp.ps1 `
  -Lane Focused `
  -FocusedProject Integration `
  -Filter $ownerFilter
```

Expected: exactly six methods/twelve rows PASS: catalog fact 1, severity theory 3,
published/retry fact 1, adversarial publication-proof theory 5, exact-match fact 1,
and same-turn normalization fact 1. The eight full-state rows must still execute
the real parameterless helper and preserve the single exact inventory-continuity
issue/filter assertions. Empty or different discovery is a failure. Use the default
five-minute limit first. Only if this unchanged coherent selection measurably times
out may the owner rerun the same filter with one explicit bounded override and
record both artifacts.

- [X] **Step 5: Inspect the exact five-file correction and commit only after parent authorization.**

```powershell
git diff --check
git status --short
git diff --stat
git diff -- `
  BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs `
  BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs `
  BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs `
  BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs `
  BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
```

Required diff: two test-only profiles, two argument substitutions, four method
traits, and exact guard/manifest updates. No production, runner, docs, examples,
Spec Kit, timeout, or project file may appear. Preserve `.serena/` as unrelated.
The parent has authorized one local commit of exactly these five files only after both named Focused controls genuinely pass (60/60 and 12/12). Preserve every failure and stop for a nontrivial code/fixture deviation rather than weakening assertions. Then:

```powershell
git add -- `
  BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs `
  BookOfEternityClient.IntegrationTests/EffectSkillScopeLifecycleTests.cs `
  BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.Severity.cs `
  BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.cs `
  BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs
git diff --cached --check
git diff --cached --stat
git commit -m "test: align wound broad-validation lane ownership (#1536)"
```

The owner report must include the base/commit, exact two Focused commands,
artifacts/counters/walls/build diagnostics/cleanup, observed broad call count and
method-category set, changed files, and concerns. It must explicitly state that no
Fast or FullValidation result is claimed and that T177/#1536 remain open.

- [ ] **Step 6: Parent performs review and folds the slice into the next meaningful checkpoints.**

The parent inspects the actual diff and both artifacts, requests independent review,
and applies only verified corrections. At the next combined implementation
checkpoint, run the one required Fast control; immediately before actual merge, run
PreMerge rather than duplicating Fast. T177's later required FullValidation should
discover exactly eight additional FullValidation rows from this slice relative to
the unchanged prior selection. Record the actual total rather than assuming a stale
absolute lane count. DeepValidation excludes LifecycleIntegration and PreMerge
already excludes both FullValidation and ordinary LifecycleIntegration; neither
filter needs modification.

## Actual implementation evidence — 2026-09-08

Source commit `ecffabdc002cacfc3362d071c26c747d443313b2` changes exactly the five
Integration files above from base `293dd3e52933076416381b824a90b57f8ae7aae2`.
The companion includes the tested local direct-call query correction:
`IdentifierNameSyntax` is recognized before the existing `InvokedMemberName`
fallback. Shared helper semantics and the exact four-caller assertions are unchanged.

All artifacts are under `TestResults/test-lanes/`:

- Original RED `20260908-191851-547-28128-e81df7516e2e46e09e5a20299287550e-focused`:
  58/60 in `00:00:41.1512485`, ten versus seven broad calls and three uncategorized sources.
- First patch attempt `20260908-195515-776-40396-1c885b8c00ad4433b421d73b59e46244-focused`:
  59/60 in `00:01:40.4561960`. The new query missed unqualified helper calls;
  the correction recognizes their actual Roslyn syntax without weakening assertions.
- Corrected boundary `20260908-200014-800-42492-d560af4509a94f028458c9b8a76c5de3-focused`:
  60/60 in `00:01:11.2190827` at the default five-minute limit.
- Identical twelve-row owner selection at the default limit,
  `20260908-200135-785-39400-333c2689f8254191a81d3d9a601dfef9-focused`:
  timed out at `00:05:00.1250762`, exit 124. All twelve rows were discovered and the
  test host ran; no final TRX was emitted. Summary 0/0 is not zero discovery or PASS.
- One explicitly approved measured extension,
  `20260908-200900-983-32428-3418b08e567c4a85abcc810fe71e2d6e-focused`:
  12/12 in `00:05:17.0916827` with `-TimeoutMinutes 10`, same filter and assertions.
  Actual per-case durations range from 12.99 to 39.69 seconds, confirming real
  execution cost rather than a build/discovery stall. No global lane limit changed.

The parent inspected both GREEN summaries, complete logs, actual TRX rows and
unique IDs: all expected rows executed, no skips or duplicate IDs, build zero
warnings/errors, no GREEN timeout, and successful owned-tree cleanup. These
results do not claim Fast, FullValidation, completion of T177, or completion of #1536.
Fresh independent source review at `ecffabdc` is Spec COMPLIANT / Quality APPROVED
with zero Critical, Important or Minor findings. The parent read the complete
review, verified the unchanged helper and actual runner filters, and accepts only
T177-BROAD-OWNERSHIP. The combined B0/B1/GM Fast checkpoint remains pending.

## Parent self-review

- **Root cause covered:** all three accrued calls are classified by their actual
  assertions, not by file size, duration, or the current B0 diff.
- **TDD covered:** retained 60-row artifact is the witnessed semantic RED; boundary
  and exact behavioral owners provide GREEN without artificial breakage.
- **Full-state contract preserved:** helper, issue collection, single-error check,
  exact code, adversarial inputs, and filtering assertions are unchanged.
- **Lane ownership bounded:** two non-oracle calls leave the broad contour; only
  four exact methods/eight rows gain FullValidation, with a Roslyn caller/category
  manifest and no partial-class trait.
- **No placeholders:** the companion patch contains all edits; commands and expected
  row memberships are exact.
- **Governance covered:** tracked nested task required before apply; one C# owner,
  bounded lane script, later parent Fast/FullValidation/PreMerge, no remote action,
  no GM or afterlife documentation impact, and no T177/#1536 completion claim.


Parent review: full plan and complete code read at source084146ea/docs6237be94.
No GM/docs/testing lane text refers to a hard-coded seven-call budget; no such
text needs a corresponding update. Five absolute patch targets are in this
worktree. Parent metadata report path: sdd/wound-broad-validation-boundary-report.md.
