# Retained pressure maximum-zero implementation plan

> Execution status, 2026-11-08: implementation and the bounded runtime selection
> are complete with181 effective distinct PASS cases across retained runs. See the
> current `spec.md`, `plan.md` and `tasks.md` RECON13 checkpoint for exact lineage,
> independent review and retention status. The task checklists below preserve the
> original design-time sequence; they do not override that measured checkpoint.

**Goal:** Complete FR-025 comparison-only persistence for an already supported
singleton player pressure source whose actual non-guaranteed wound maximum is0.

**Architecture:** Reuse preimage-v3, the existing owned ordinary/actual projection,
receipt-schema-v3 comparison rows, exhausted source-only C3 and common C4. Keep all
current authority and input fences; add no game rule, owner API or cache.

**Tech stack:** C#/.NET8 target, SDK10 compiler, xUnit, PowerShell7 category runner,
file-backed JSON and the existing operation-validation service.

**Spec:** [pressure-zero-v3-design.md](pressure-zero-v3-design.md), canonical
`spec.md` FR-025, `plan.md`/`tasks.md` T081-D-RECON13, issue
[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536).

## Global constraints

- Work only in `/workspace/scratch/2f2a1767475c/wounds-reconstructed-237b-20261107`.
  Preserve dirty work, HEAD237b087, index and default `tests/selection.json`.
- Parent owns top checkpoint sections in spec/plan/tasks, requirement map, builds,
  discovery, runtime and evidence handling. Worker edits this design/plan only
  until explicitly released. During future GREEN the worker owns only the precise
  normative Spec Kit body sections named in Task5; the parent will not edit those
  sections concurrently. All checks below are proposed, not executed.
- No stage, commit, reset, push, GitHub diagnostics, CI automation or new feature.
  Latest user restriction overrides repository publication defaults.
- Existing singleton III/IV domain stays; v4/v5 max0, ordinary4/no-increase and
  broader actor/action/terminal contours stay refused. No reflection or new hook.
- Read AGENTS, workflow1.7, performance/testing, constitution2.1.0 and active
  feature artifacts. Each fixture owns all mutable state and cleanup.
- Evidence root: `../reconstruction-evidence-20261107/pressure-zero-v3-20261108`.
  Preserve baseline31565 pins, prior diagnostics including `native-terminal`,
  and all unsuccessful captures. No speedup or native-birth claim.

## Review focus

1. A shared predicate must not admit pair-v4/guardian-v5 maximum0; Task2 keeps
   positive-only issuer guards and Task3 tests the table/version boundary.
2. A valid zero source must not disappear at C3 or acquire a fake decision;
   Task3 asserts exact one-row append and Task4 proves real source-only publication.
3. A cold capture or retry must not add another strain step, die claim or spend;
   Task4 re-derives from unchanged ordinary bytes with fresh owners.
4. No materialization is not an exemption from pressure History/log validation;
   Task3 invokes the actual normalizer History gate and signed-log joins.
5. Pre-take refusal preserves authored edits, whereas late C4 compensation
   restores signed origin; Task4 distinguishes these two physical oracles.

## File ownership and intended edits

Production changes are expected only in these five files under
`BookOfEternityClient/Services/`:

- `Validation/ValidationService.SpiritualExistingPressureProjection.cs`: singleton
  actual ceiling eligibility and family-specific `MatchesActualSource`.
- `SpiritualStrainPreimageData.Pressure.cs`: intrinsic v3 zero acceptance, v4 guard.
- `Validation/ValidationService.SpiritualZeroStrainWitnesses.cs`: compose v2/v3 zero.
- `SpiritualWoundDeclineReceiptReducer.ZeroStrain.cs`: classify v2/v3 zero witnesses.
- `SpiritualWoundOpportunityReceiptState.ZeroStrain.cs`: parse v2/v3 zero rows.

New focused tests, keeping unrelated old fixtures unmodified where practical:

- `BookOfEternityClient.TestSupport/SpiritualPressureZeroTestData.cs`: detached
  literal ordinary/actual/full III/IV parent DATA; reuse existing typed fixtures.
- `BookOfEternityClient.Tests/SpiritualPressureZeroDataTests.cs`: intrinsic RED and
  bounded negatives, no live authority.
- `BookOfEternityClient.Tests/SpiritualPressureZeroReceiptTests.cs`: append/History/
  log/normalizer/source-only-owner DATA controls.
- `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.SpiritualPressureZero.cs`:
  genuine desired-success owner rows and warm reuse/freshness.
- `.../AfterlifeResourceCutoverTests.SpiritualPressureZeroPublication.cs`: private
  singleton setup, cold source-only publication and signed-next helpers.
- `.../AfterlifeResourceCutoverTests.SpiritualPressureZeroFailures.cs`: pre-take
  drift and witnessed late cut/retry using existing hooks.
- Modify the obsolete method in `AfterlifeResourceCutoverTests.SpiritualPressureGroupActualGuard.cs`
  only after accepted RED. Keep the other-wound refusal and its helper.
- Modify `BookOfEternityClient.Tests/SpiritualExistingPressurePreimageTests.cs`
  only after accepted RED to reclassify its lawful training `actual-zero-ceiling`
  negative; preserve `actual-capped` and replace lost refusal coverage with a
  genuinely contradictory scalar/cap case.

Do not generalize pair-specific publication helpers by silently dropping their
two-parent assertions. Reuse scalar snapshot/cleanup/authoring helpers, or introduce
a private singleton test wrapper over the same real owners. No shared live fixture.

## Task1: establish causal intrinsic and owner failure

**Consumes:** `SpiritualExistingPressureTestData.CreateData(int rank)`, existing
typed witness/parent parsers and `WithPressureGroupCaseAsync` private integration
fixture. **Produces:** independent III/IV zero DATA and desired-success regressions.

- [ ] Add `SpiritualPressureZeroTestData.CreateData(int parentRank, bool trainingCap = false)` using complete
  existing rank-specific graphs. Set ordinary before clear/after strained,
  actual after fractured, resilience3, dice4/12, harmful margin8, standard tier0,
  ordinary calculation2/max0 and actual7/max0. Rebuild exact raw ordinary bytes,
  pointer/fingerprint and source sealing consistently; never use retained DATA as
  proof of original membership. Keep all sibling action, cost, dice and actor data.
  The training alternative keeps the existing beforeI/ordinaryII/actualIII,
  resilience0 comparison with literal pressures10/15 and both maxima0. It proves
  the already supported geometry with a different cause of maximum0, not a new
  product domain.
- [ ] Add `PressureZeroDataRetainsOrdinaryAndActual(int parentRank)` for3/4. First
  validate the parent and an independent comparison-free ordinary witness, then
  assert expected success through `ValidateAndReadStrainData` and
  `SpiritualStrainPreimageData.ReadIntrinsic`. Prove literal calculations, closed
  version3 fields, parent graph, input binding, detached results and input inertness.
- [ ] Add `SpiritualPressureZero_RealOwnerAdvancesWithoutOffer(int parentRank)` for3/4.
  Use `Parents:1, After:"strained", Resilience:3, AdvanceExpected:true` with exact
  rank alternatives. Prove complete signed/current parent, ordinary successful
  comparison2/max0, physical inertness and no actual work before the final
  desired-success `AdvanceNextResourceExchangeAsync` assertion. Subsequent actual
  source7/max0/C1/no_offer checks remain UNREACHED on baseline failure.
- [ ] Keep the old `SpiritualPressureGroup_NonpositiveActualMaximumCannotAdvance`
  method unchanged for this stage. Add exact RED category membership and dedicated
  selection; no default selection edit. Independent SOURCE before any build.
- [ ] Parent obtains fresh owning builds/provenance, official catalog and exact
  PlanOnly, then bounded causal RED. Expected: intrinsic positive-ceiling refusal
  and real-owner `spiritual_pressure_ordinary_contour_unsupported` at Advance;
  ordinary prerequisites and cleanup pass. Compilation/setup failures are not RED.
  Parent obtains independent OUTCOME before releasing Task2.

## Task2: connect singleton zero to the existing storage owner

**Consumes:** accepted Task1 causal evidence and the existing closed owner APIs.
**Produces:** the same v3 witness admitted as zero-comparison DATA, never an offer.

- [ ] In `ReadOwnedSingletonPressureOrdinaryForProjectionUnderGateAsync`, require
  a valid increasing actual calculation rather than a positive maximum. Preserve
  actual=ordinary+1 and current basis. Keep pair and guardian branches unchanged.
- [ ] Bind a private singleton flag in `PressureProjectionData` from its actual
  basis type. In `MatchesActualSource(PreparedSpiritualSource source)`, allow
  maximum0 only for that singleton; retain every other equality/fence. Do not
  derive family eligibility from caller-provided wire/version fields.
- [ ] In `ReadPressureIntrinsic(JsonObject source, JsonObject actual,
  SpiritualWoundCalculation actualCalculation)`, permit maximum0 for version3
  while retaining valid increasing actual and version4 positive-only behavior.
  Preserve all closed fields, ordinary validation, rank III/IV parent and exact
  sibling/raw/dice/input constraints.
- [ ] Extend `ComposeCompletedZeroStrainWitnesses(string originalFingerprint)`,
  `IsZeroStrainComparison(JsonObject witness)` and `ValidateStrainComparisons(...)`
  from versions2 to2/3 only. Preserve exact maximum0/no-guarantee/increasing checks,
  existing `ComposeFirstOfferSource`, reducer issuance and immutable append rules.
- [ ] At this atomic correction retire only the obsolete zero-refusal method and
  selector. Keep its evidence/history; update the category description to identify
  replacement desired-success coverage. Leave other current refusals unchanged.
  Remove only lawful `actual-zero-ceiling` from the old intrinsic `SemanticCases`
  negatives and replace it with a resealed contradictory maximum/cap case. Keep
  the exact `actual-capped` negative: it still violates the one-step/data contract.
- [ ] Make the documentation changes in Task5 in this same GREEN source package.
  No C3/History/normalizer or cache implementation change is expected. If source
  tracing finds one necessary, report the exact barrier before expanding edits.

## Task3: prove storage, scope and fresh History closure cheaply

**Consumes:** Task1 DATA and existing public/internal typed parsers/reducer gates.
**Produces:** independent cheap controls owned by the new focused unit categories.

- [ ] After a valid v3-zero baseline, test closed-field/type/pointer/ordinary-
  calculation mismatch, unchanged-sibling violation, foreign parent/component and
  forbidden guardian attribution. Recompute lexical seals where needed so each
  negative reaches its intended semantic guard. Preserve valid III/IV controls.
- [ ] Add `PressureZeroDataAcceptsTrainingCapWithoutNarrowingStrainGeometry` for
  parentIII/IV using the training fixture above. Assert ordinary10/max0,
  actual15/max0, beforeI/ordinaryII/actualIII, exact original cap and full siblings
  through both witness and direct-reader paths. This replaces the lawful old
  negative without reclassifying its historical execution.
- [ ] Add `PressureZeroReceiptAppendsWithoutDecision` for zero-only, prior-injury
  and same-request mixed-positive DATA. Assert exact old arrays/prefixes, one
  comparison, no invented `none`, v3 receipt schema, detached witnesses and
  `appended` then `exact_replay`. Add a III/IV parent control without multiplying
  the entire negative matrix by rank.
- [ ] Add after-valid duplicate source/coordinate, ordinal, original request,
  dice reuse, prefix mutation and accepted-request reopening negatives. Positive
  v3 may remain only in the injury source/decision table; v4/v5 maximum0 remains
  rejected at intrinsic/receipt boundaries. Keep v2 zero and schema2 healing
  migration controls separate and unchanged.
- [ ] Add `PressureZeroHistoryRequiresExactParent` for exact, missing and changed
  anchors plus later lawful healing DATA. Call the real
  `CanonicalStateNormalizer.ValidateSpiritualReceiptPublicationHistoryAsync`
  with an independently valid zero receipt, proving one History read even when
  decisions are empty. Missing/malformed/mismatched History refuses.
- [ ] Add `PressureZeroHistoryMatchesSignedActual` for exact/missing/duplicate/
  altered actual exchange and signed-current-turn summary rules through
  `MatchRetainedPressureHistory`. Verify `HasRetainedPressureComparisons` is true.
- [ ] Add forged `AuthenticatedC3SourceOnly` key and detached-copy refusal control:
  valid DATA and a made-up receipt identity do not grant source-only publication.
- [ ] Parent reviews complete source, builds Unit+Integration freshly and verifies
  pins/catalog/exact plans. Run cheap GREEN first; real publication waits for its
  complete outcome and independent review. Do not rerun every old codec case.

## Task4: prove cold source-only C4, signed-next and rollback

**Consumes:** real ordinal-zero signed retained-parent setup, actual source APIs,
`ReadPressurePairSignedOriginAsync` pattern (not its pair cardinality assumptions),
`CurrentCommittedPublicationWitness` and existing common publisher hooks.
**Produces:** genuine retained III/IV owner/publication receipts, not native births.

- [ ] Add `SpiritualPressureZero_ColdSourceOnlyPublishesAndValidatesSignedNext(int parentRank)`
  for3/4, independent fixtures. Capture complete signed origin before authoring,
  then author only the ordinary input/output. Warm capture proves cold ordinary,
  warm hit, actual advance, exactly one actual zero source, detached C1 no packet
  and `no_offer`; actual resource companion differs only at the two player-strain
  paths. No pending/checkpoint/command/decision or canonical publication appears.
- [ ] Dispose warm owners and release their lease. Use a fresh validator/capture
  and the same unchanged ordinary bytes, re-prove actualII7/max0, complete the
  actual reduction and call `PrepareC4PublicationAsync`. Assert original input
  remained ordinaryI, receipt contains one v3 comparison, all parent/root/identity/
  History data and prior source/decision/healing prefixes remain exact, and no
  live wound completion exists. No cold C2 Open or fake none submission.
- [ ] Repeat the supported prepared/current query to prove no extra source,
  comparison row, allocation or AP charge. Register authentic authority, validate
  raw resources, publish through `NormalizeAndValidateWithPlanAsync`. Assert
  player/opposition AP3->0 once each, one claim for each signed die4/12, actualII
  conflict, receipt once, consumed private paths absent, actual planned writes and
  immutable parents/history/roots. Capture signed47 and validate the actual log.
- [ ] Add `SpiritualPressureZero_PreTakeDriftPreservesAuthoredInput`: mutate exact
  ordinary raw bytes after completed authority, then require pre-take refusal,
  zero publication writes, unchanged edited draft/generation and revoked authority.
  Cover warmed raw/receipt drift with representative existing owner controls or
  a small zero-fixture row if the shared selectors cannot reach the changed path.
- [ ] Add `SpiritualPressureZero_LateCutRestoresSignedOriginAndFreshRetryPublishesOnce`:
  arm existing read-open hook only after the publication witness has observed
  current conflict/resource/receipt writes. Inject once at receipt read-back.
  Assert the original failure, complete tracked bytes/absence + generation +
  journal restoration, no pending controls, and old authority rejection. Reauthor
  only exact ordinary draft bytes and use fresh validator/capture/lease; compare
  actual semantics, publish once, assert one comparison/charge and signed47.
- [ ] Use existing real foreign/disposed/revoked owner and another-wound controls;
  do not call live probes after revocation. No generic callback or product cut.
  Retain named setup/ordinary/actual/cold/C3/C4/rollback/retry/cleanup durations and
  actual source/read/fresh-check counters. No setup-only cache-hit claim.

## Task5: synchronize guidance and qualify the bounded change

**Files:** this feature's `spec.md` FR-025 and current reconstructed player-v3
boundary, `data-model.md` section 7.7.3, and
`contracts/wound-effects-and-atomicity.md` signed-existing pressure paragraph under
Exact version-1 spiritual registry; existing Wound/Afterlife guides, `TaskGuides/CLI_Step_Main.txt`,
`Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`,
`SpiritualStrainComparisonTests` and `SpiritualGuardianAttributionDocumentationTests`,
`tests/categories.json` and a new dedicated selection file under `tests/`.

- [ ] Synchronize those three active normative Spec Kit body sections in the atomic
  GREEN package. In `spec.md`, extend preimage-v2-only FR-025 wording to the supported
  singleton preimage-v3 comparison and replace the singleton-positive requirement.
  In `data-model.md` section 7.7.3 and `contracts/wound-effects-and-atomicity.md`,
  distinguish singleton maximum0 comparison-only continuation from positive offers
  and narrow blanket true-actualmax0 refusals to still-unsupported v4/v5 contours.
  Preserve singleton III/IV, before<ordinary<actual=ordinary+1<=4, ordinary below4,
  pair/guardian positive-only and every other domain/ownership limit. Worker owns
  these precise normative bodies; parent owns top checkpoint prefixes and the
  requirement map. Do not substitute a prefix note for the normative edits, alter
  dated historical receipts, or reclassify old runtime evidence. Cross-check all
  three bodies against the design before requesting GREEN SOURCE review.
- [ ] Add a complete ordinary4/12/resilience3 pressure example inside existing
  `spiritual_wound_strain_result_v1`, with full III/IV parent alternatives and
  ordinary2/max0 -> actual7/max0. Typed guard independently composes the witness
  through the production v3 reader, checks exact raw/root/siblings/costs/dice and
  no-offer semantics. Update focused manifest scope; preserve the 64-family registry.
- [ ] Remove contradictory singleton-positive and unconditional v2-only wording
  from active guides/prompt/guards. Explicitly keep v2 maneuver distinct and v4/v5
  actual maximum positive. The GM supplies ordinary pressure, never private DATA,
  actual strain, opportunity, none or extra charge for the zero case.
- [ ] Create exact category ownership: `wounds-spiritual-pressure-zero-data`
  (unit, 60s), `wounds-spiritual-pressure-zero-receipt` (unit, 60s),
  `wounds-spiritual-pressure-zero-owner` (integration, 60s),
  `wounds-spiritual-pressure-zero-publication` (integration, 120s) and
  `wounds-spiritual-pressure-zero-failures` (integration, 120s). These are initial
  protective bounds, not measured performance. Use exact method selectors and
  explain exclusions; split by responsibility if measurements require it, never
  raise a bound to conceal a hang. Small RED selects only the Task1 methods.
- [ ] Create `tests/selection.spiritual-pressure-zero-v3.json` for this block,
  preserving `tests/selection.json` byte-for-byte. Include new methods and exact
  affected controls: existing v2 zero positive/refusal + one real v2 source-only
  route; positive singleton intrinsic and III/IV real owner; positive v4 source and
  v4/v5 max0 refusal; source-only input/incomplete-owner controls; pressure History
  normalizer/log controls; existing strain/guardian guidance and family registry.
  Prefer exact existing category owners; if a broad one contains unrelated methods,
  add a focused regression category with exact methods and a written responsibility.
  Do not select every related category or the prior default 49-entry selection.
- [ ] Parent gets independent whole SOURCE/selection review, fresh normal-reference
  owning builds with disabled build servers/shared compilation and no automatic
  retry, DLL/PDB/source joins and unchanged source/output pins. Use official
  `scripts/test-csharp.ps1 -NoBuild -ValidateCatalog`, then
  `-NoBuild -SelectionFile tests/selection.spiritual-pressure-zero-v3.json -PlanOnly`.
  These are permitted only after the relevant fresh successful builds.
- [ ] Parent executes the reviewed cheap categories, then separately bounded real
  owner/publication/failure selections with `-NoBuild -Category <exact reviewed IDs>`.
  Record precise planned/completed cases, failures/skips/duplicates/timeouts,
  actual costs, original budgets and owned cleanup. Do not claim combined results
  are one binary if source/build lineage differs. After two failures revisit cause.
- [ ] Independent OUTCOME review checks source/evidence, dependency closure and
  optimization assessment. Parent updates canonical checkpoint/tasks and preserves
  needed diagnostics in Space only through its existing verified process. Report
  UNRUN/UNKNOWN explicitly. Acceptance is this retained singleton zero cell only.

## Plan consistency and stop condition

This plan maps source admission to Task2, intrinsic/receipt/History to Tasks1/3,
complete real source-only publication and compensation to Task4, and GM/catalog
closure to Task5. Task1 RED and Task2-5 GREEN are gated stages of one coherent
contract change, not separately published features. There is no new product
decision. A newly discovered authority/storage/API requirement must return to the
parent before implementation. At design time only these two documents existed and all counts/runtime were
unqualified. That historical planning state is superseded by the execution
checkpoint above; it is not the current implementation status.
