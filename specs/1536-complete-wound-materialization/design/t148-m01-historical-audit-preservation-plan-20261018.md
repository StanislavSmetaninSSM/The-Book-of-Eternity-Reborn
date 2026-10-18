# T148 M01 historical audit preservation implementation plan

> For execution: use `superpowers:executing-plans`; sole writer remains Root.

**Goal:** one regression Fact proves that existing historical Saref audit bytes
survive genuine accepted Mortal wound healing. It does not qualify an independent
Saref runtime effect, spiritual healing, E01, full T148 or Phase10.

**Spec:** [accepted M01 source audit](t148-saref-memory-suppression-source-audit-20261018.md).
**Base:** `0292234faa8eb1cd405ade611057be07bafa5e19`, existing task branch
`1536-wound-b2-cloud-20261012`; #1536/T148. User authorizes implementation after
independent plan/route review. [PLAN ACCEPTED](../recovery/t148-m01-plan-independent-review-20261018.md)
at reviewed `b5f6ab881fa80cdea260a6dccde968dea5f1383d`; SOURCE/PREP/runtime pending.

**Architecture:** call production `SarefMainStoryState.ApplyUpdate` with valid
historical `record_defeat_outcome` inputs, persist its canonical output before the
first `PrepareSignedCreationTurn`, then use the existing recovery composer and
`Fixture.PublishWithCanonicalRefresh`. Fresh per-case preparation bypasses both
fixture caches. No production change, new route, effect or carrier.
**Tech:** existing C# integration project, xUnit, SDK10/.NET8, PowerShell category runner.

## Constraints and verified route

- L01 stays 52/57 at original compiled provenance; old66529 UNKNOWN/unavailable/
  recoveryClaim=false; original crash5 unqualified. No crash/sampling/Windows/CI.
- Production source, assertions, 220/900 budgets and existing outcomes stay intact.
  A proved production defect requires a report and minimal fix plan before editing.
- `ApplyUpdate` (:640–652,728+) is a real producer. Typed `GameResponse` lacks Saref
  fields, so M01 does not claim StateDistributor ingestion. The fixture persists
  genuine producer output; later canonical refresh reaches the existing normalizer.
- `PrepareRecoverySourceTemplate` (:1375+) creates canonical authority roots and
  signs creation at1443. A new optional callback runs immediately before that sign,
  before lease acquisition, not against an already signed/cached tree.
- `CreateUncached` (:1526+) can take a privately supplied `PreparedRecoverySource`;
  `isolatedSource ?? existing GetOrAdd(...)` must be lazy. The new factory calls
  this directly, bypassing `Fixture.Create`/`PreparedRecoveryTemplates` entirely.
- Existing `Scenario.AtDueBoundary` with `currentStepThreshold=1` reaches actual
  terminal heal in `PublicationRollback`; use its genuine success composition,
  raw item authority and full canonical refresh seam. No rejected-wave dispatcher,
  joint CurrentResume/late consumer or L01 crash closure is required.
- `PublishWithCanonicalRefresh` returns canonical materialization diagnostics;
  they are not the entire `ValidateGameStateAsync` universe. Validate SarefStory
  separately before signing and after healing; assert no Saref contract Error
  (path/code), and retain all emitted diagnostics. Require no Error in actual
  canonical-refresh result, keeping the existing helper assertions unchanged.

## Files and interfaces

- Create `BookOfEternityClient.IntegrationTests/MortalWoundRecoveryTests.SarefAuditPreservation.cs`:
  exactly one `[Fact]`, named
  `Publication_TerminalHealingPreservesExistingSarefMemorySuppressionAudit`, plus
  its test-only seed/assert helpers and nested `Fixture` factory.
- Modify only test preparation in `MortalWoundRecoveryTests.cs`:
  `PrepareRecoverySourceTemplate(Scenario, string, Action<FileSystemManager>? beforeCreation = null)`;
  `CreateUncached(Scenario, PreparedRecoverySource? isolatedSource = null)`.
  Old callers use defaults; existing cache keys and assertions remain unchanged.
- SOURCE implementation ruling: add readonly `PreparedFixtureTree.ReadFileBytes`
  returning a copy, so the Fact can compare completed source snapshot bytes after
  source root cleanup. This avoids reflection or a third mutable materialization;
  existing Capture/Materialize paths and default callers remain unchanged.
- Nested `Fixture.CreateForHistoricalSarefAudit(Scenario)` returns the actual Fixture
  and a detached `byte[]` expected audit image. It can access its private source
  types; do not expose them or use reflection. Per-case source key is unique and
  never aliases a shared preparation-counter/cache profile. Both physical roots
  retain existing owned cleanup; the Fact uses `OriginalFixtureCompletion`.
- Add singleton category `saref-audit-mortal-heal-preservation` in `tests/categories.json`:
  integration exact Fact selector, no frontend, exclusive, Linux metadata, 10-minute
  category bound (existing recovery owner bound, not a budget increase/retry remedy).
  Current `tests/selection.json` selects only this category with audit-survival reason.
  Discovery may retain overlapping existing wildcard ownership; actual selection is1.
- Update existing #1536 plan/tasks and M01 evidence only. No GM contract changed,
  therefore no runtime prompt/example change. Source audit and prior reports retain
  their bytes/provenance; later statuses are recorded separately.

## Review focus and exact assertions

- Producer must not be vacuous: start from `CreateDefaultRoot`; use two genuine
  memory_suppression updates with distinct fixed outcomeIds, turns1/2 before creation42,
  sceneType=`memory_attack`, gmMotivation, scope/severity/summary. One row is a sentinel.
  Assert both exact semantic rows and no response wrapper before capturing bytes.
- Baseline is the file's physical bytes captured before creation signing. Assert
  the source completion, newly signed evaluation and pre-heal file still match it.
  Do not derive expected bytes from the post-heal output or serialize for equality.
- Preserve the existing seam order: `PrepareCanonicalRefreshItemAuthority` →
  `ReadmitCurrentRecoveryBinding` → capture current pre-heal state → `InvokePlan`/
  `Compose` → verify no writes → `PublishWithCanonicalRefresh`. Never prepare raw
  item authority after Compose. Assert Composed/no issues and real plan/receipt.
  Verify same composed plan was published and canonical result contains no Error.
- Assert wound is genuinely healed, absent from active carrier, retained History and
  wound identity agree; capture exact current pre-heal owned root bindings with
  active/suspended identity proof, then require them absent from active carrier and
  retained expired with exact expire history. Do not union retired historical
  generations into that oracle. Reuse existing assertions.
- Compare file existence and exact original bytes after healing plus fixed semantic
  audit rows; run separate SarefStory validation. This is historical persisted data,
  not a new Mortal Saref defeat, active cross-realm effect or test of Soul Gates.

## Work and gates

- [x] Save/recover this plan, independent Astra XHigh plan/route/selection review;
  resolve concrete findings before implementation. User already chose M01 and
  autonomous execution; no second routine permission question.
- [x] Write the one Fact and minimal test-only preparation seams. This adds coverage
  of existing behavior: no production implementation is permitted to manufacture RED.
  A production change clearing/changing the audit on healing must fail the byte oracle.
  No test outcome is preclaimed; any first failure freezes original evidence for
  independent diagnosis. Preserve TDD's production-change gate if a real defect appears.
- [x] Save/push/recover source WIP, then independent SOURCE review of real dataflow,
  default caller preservation, pre-sign seed, assertions, exact singleton selection
  and adequate cleanup/evidence. No actual test before this gate.
- [ ] Fresh `pwsh -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog`:
  build/discovery/catalog only, no executed tests. Fresh source/binaries/tool hashes
  and raw output preserved. Then singleton
  `-Category saref-audit-mortal-heal-preservation -PlanOnly -NoBuild`:
  exactly1 descriptor/method/Fact, no actual tests. Independent PREP before actual.
- [ ] Exactly one first actual via the same category and `-NoBuild`, in a new M01
  TMPDIR/XDG namespace. Never source a prior L01 diagnostic environment script.
  Preserve native exit, stdout/stderr, record/summary, TRX complete counts/duration,
  discovery/selection, phase source/binary identities and cleanup witness. A PASS
  requires1 executed/1passed/0failed with all strong assertions; no partial promotion.
- [ ] Save/recover original actual outcome before independent EVIDENCE review.
  Failure/incomplete result: freeze, diagnose independently, do not change assertions,
  widen budgets or retry automatically. Source corrections require affected SOURCE/PREP.
  After accepted evidence, final commit/push/exact remote SHA/clean recovery/readback.

## Historical L01 pins and M01 evidence

Adding the category/current selection necessarily changes **two of the historical
101 L01 source pins**, only `tests/categories.json` and `tests/selection.json`.
Keep the L01 source packet, cloud/method ledgers and all outcomes byte-exact to0292234f.
Verify unchanged99 and product23; record exact old/new hashes for those two common
metadata files. Do not rewrite old packet hashes or assert current all101 equivalence.
This is not L01 source/PREP/actual admission, a replay requirement or crash dependency.

M01 gets independent current source/catalog/selection/preparation/binary binding and
recovery verification. Recovery checks every changed file/blob/mode, historical
ledger bytes, exact2 expected L01 pin differences, unchanged99/product23 and the new
test/preparation closure. The older all101-current recovery helper cannot be used
unchanged after category edits. Review new recovery/source packet before native.

M01 PASS can close only this audit-state regression. E01, T148, Phase10, T177 and
all full L01/NI/HOT/F1/M3/T197/A3/Windows403 holds remain open/held as recorded.
