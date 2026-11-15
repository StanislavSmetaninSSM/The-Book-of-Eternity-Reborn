# Mortal transport/storage implementation plan

> **For agentic workers:** Use superpowers:executing-plans task-by-task, with independent fresh-context review of each complete bounded deliverable.

**Issue:** [#1515](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1515)
**Goal:** Complete first materialization and make existing transport/storage consumers respect its accepted authority.
**Architecture:** Extend the existing vehicle resource-owner composer and governed location storage metadata; pure birth contracts validate the new semantic envelope and reuse existing item/location planning. Effective access and active selection join existing accepted-turn after-images and backup/readback/rollback, never a second writer.
**Tech stack:** C#/.NET 8 target, SDK 10 compiler, PowerShell 7 category runner, existing System.Text.Json and typed xUnit tests.
**Spec:** [spec.md](spec.md)

## Current checkpoint — ordinary vehicle preparation verified, 2026-11-15

Source `ad9e5bf7d575a27850c90db87da1e56b83d43e52` passes the official
preparation, active-publication and exact allocation-interoperability categories:
**30/30 PASS**, 3 complete descriptors, 27.100s, no skips/duplicates/timeouts,
owned cleanup complete. Serial integration build: 124.15s, 0 errors, 234 warnings.
The new preparation contributes 11 cases. Independent fresh-context review accepts
the narrow result with no remaining Critical/Important finding or refinement.
Raw evidence remains external: `20261115-062343-774-260-1f4327b9796948f6b139185938d87c87-categories`.

Preparation reuses allocation DATA only, keyed by the exact observed original/current
vehicle roots, signed session/snapshot and fresh existing registry generation. It
adds no vehicle/owner file reread, but registry access intentionally observes the
physical generation. No performance speedup claim. All candidates validate before
allocation; explicit-null canonical collections fail; observed no-birth input and
full invalidation clear preparation. Real repeated admission preserves exact ID and
companion, while input/session/snapshot/generation changes and failed preparation
cannot reuse it. Specialized original allocation/replay remains unchanged.

During the build a next-block test file briefly appeared then was removed. The
produced portable PDB contains the current preparation test document and no cargo
test document; the working tree remained exact for the selected verified source.
No cargo test execution or cargo readiness is inferred from these 30 cases.

Next is same-turn cargo admission/sealing in the existing item pipeline. Current
code does not scan `UpdateVehicles[].inventory` and cannot yet qualify full vehicle
birth. Effective access/location/Faction/NPC authority, full semantic birth evidence,
storage and remaining issue requirements are still open.

## Previous checkpoint — vehicle birth structural slice verified, 2026-11-15

Verified source `0f0cbafe4c7719e7c9ee9465b87553dde9efdb08` adds the shared
`MortalTransportStorageContract` at actual raw MetaMisc admission and before
vehicle ID allocation in the existing owner composer. It rejects missing/malformed
capacity, volume, explicit owner/controller/access and unsupported player IDs.
Explicit null owner/controller is not actor inference. It does **not** establish
Faction/NPC authority, effective permission, full cargo or semantic birth evidence.

- Causal RED: owner composition 1 PASS / 20 expected missing-diagnostic FAIL;
  actual MetaMisc admission 1 PASS / 6 expected missing-diagnostic FAIL. Build/fixture
  errors were not counted as these behavioral RED results.
- Final Linux official unit categories `mortal-vehicle-birth-contract` and
  `mortal-vehicle-active-selection`: **54/54 PASS**, 13.116s, 2 complete descriptors.
- Final integration `mortal-vehicle-birth-admission`, `mortal-vehicle-active-publication`,
  `mortal-vehicle-birth-allocation-interop`: **26/26 PASS**, 25.829s, 3 complete descriptors.
  No skips/duplicates/timeouts; owned cleanup complete in both runs.
- Serial unit/integration builds: 93.03s/59.03s, 0 errors, 156/126 preexisting warnings.
  A prior malformed shell category-array argument caused a pre-workload refusal;
  it was corrected, not reported as a production/test failure.
- Primary Block_10 schema, Block_2, CLI rules, daemon guidance and two vehicle examples
  now declare these fields. The source guard includes the primary schema.
- Independent fresh-context GPT-6.1 Sol XHigh review found no Critical issue. Its two
  Important findings (primary schema and two shared allocation fixtures) are fixed
  and statically rechecked. The exact two existing allocation/replay leaves pass;
  only their ordinary Mortal vehicle fixture facts changed, not spiritual logic.
- Optimization: cheap checks reuse parsed/detached roots, perform no I/O or cache,
  and run before minting an ID. Composer serializes its one candidate to JsonElement
  for the shared contract; no demonstrated expensive repeated path or speedup claim.
- Raw evidence remains outside Git. Final TRX directories end in
  `20261115-055813-193-326-c4dcc31fcfe842c1b8ef80f90ef21846-categories` (unit) and
  `20261115-055926-871-616-8bd88ba95d7f4d39bcaad0df1af52075-categories` (integration).
  No hosted CI/main merge/issue closure; whole #1515 remains incomplete.

### Next required prerequisite: real same-turn vehicle cargo

Read-only tracing confirmed that current item catalog scans canonical vehicles[]
only, the item collector ignores UpdateVehicles inventories, every item route
excludes vehicle_inventory and the final resource companion copies raw cargo.
AcceptedItems exports resource owners; it is not a sealed cargo after-image.
Thus empty-cargo positives do not prove a complete vehicle with new cargo.

Use a narrow vehicle identity preparation entry beside the existing composer,
retain its DATA in the existing AcceptedTurnAuthorityRegistry, and pass its exact
vehicleRef-to-ID map into item catalog/route/snapshot. Add `new_vehicle_inventory`
as a birth-specific route and the existing item collector/rewrite path. Reuse
TryProjectItemPhase, then supply its sealed raw-command vehicle root to the existing
vehicle reducer with the same allocation map. Verify the final effect/owner companion
winner cannot replace that cargo. Do not introduce a second writer or inventory,
reuse the treatment-only final-baseline API, or copy NPC initialId-as-ID behavior.
The complete map input/source/factory identity and invalidation must be tested;
identity DATA is never permission to publish. See spec for historical quantity/shape
measurement binding. Receipt/access/storage and bounded repair remain subsequent work.

### WIP ordinary allocation preparation

At `10e1ddd07`, repeated ordinary admission produced causal RED: both admissions
succeeded but returned different vehicle IDs. Official category 1/1 FAIL, 11.133s,
complete cleanup, no duplicate/skip/timeout; build119.31s,0errors234warnings.
The narrow preparation and existing-registry DATA reuse are now implemented but
uncompiled/unrun. Added source/session/token/full-invalidation and no-authority
regressions plus all-candidate validation before allocation. Spiritual original
capture remains on its existing factory path. No cargo integration claim yet.

First preparation verification at `d87b58cd`: 27/28 PASS, one **fixture** failure:
the generation case used an ordinary lease for RotateSessionGeneration, which
correctly requires a lifecycle/replacement lease. Fixture now uses that existing
capability; production generation rules are not weakened. Build124.21s0errors234warnings,
runner25.368s complete. Review adds two behavior regressions before fixes:
explicit-null canonical array must reject before allocation, and observed ordinary
A→no-birth B→A must discard preparation. Exact restored input across unrelated
unobserved failures is safe DATA only; fresh full-source acceptance remains required.
These two new regressions are unrun; no claim of complete GREEN.

### Next implementation step: ordinary allocation preparation

Clean restore of checkpoint `627eac3f7772cf63b4332ba940eb04bc7a2f7931` verified
exact HEAD, clean status and `git fsck --connectivity-only` in a fresh directory.

- [ ] Causal RED integration test repeats ordinary raw resource admission for the
  same authenticated pending snapshot and asserts one stable vehicle identity.
- [ ] `MortalVehicleBirthPreparation.Prepare(JsonObject pre, JsonObject raw,
  VehicleIdentityFactory factory)` produces detached exact ref-to-ID DATA after
  preassigned/duplicate identity and structural-birth checks. No writes/authority.
- [ ] Existing `AcceptedTurnAuthorityRegistry` retains preparation for exact
  session/snapshot token, pre/raw vehicle bytes and current root generation under
  its existing gate. An input change or full invalidation discards it; no new cache
  owner/static dictionary. The lease/generation is checked on every access.
- [ ] `MortalResourceOwnerCompositionInput.PreparedVehicles` is optional detached
  preparation; the ordinary resource service passes it and the composer looks up
  the existing allocation instead of minting twice. Direct pure composition retains
  its explicit factory path. This step leaves specialized original spiritual capture
  on its existing selected factory path; it adds no spiritual replay behavior.
- [ ] Targeted replay/source-change regression, independent review and checkpoint;
  then extend the same preparation into raw item intake/catalog/route/projection.

## Earlier checkpoint — active selection verified, 2026-11-15

Verified source: `633ce3d774ae8a422c8ac914366f34f058e669d2` on this task branch.
This is a completed **narrow active-selection block**, not completion of #1515.

- Existing owner composer now consumes exact activeVehicleChange into client-owned
  activeVehicleId; same-turn refs reuse the allocated identity, selection-only writes
  survive, removal/clear/replacement and final Active agreement are enforced.
- Canonical shape, transient cross-reference admission, protected repair routing,
  primary GM rules, existing worked example and daemon guidance are synchronized.
- Fresh official categories, Linux x64: `mortal-vehicle-active-selection -NoBuild`
  **22/22 PASS**, 9.375s runner; `mortal-vehicle-active-publication -NoBuild`
  **17/17 PASS**, 14.318s runner. No skips/duplicates/timeouts; owned cleanup complete.
  Fresh serial unit/integration builds: 91.72s/58.83s, 0 errors, 156/126 existing warnings.
- The rollback leaf reached one real physical vehicle-member write (member index 0),
  observed canonical active identity, and restored tracked bytes/absence, generation
  and journal absence. This is not a claim of a later all-resources-written cut.
  Real successful birth/removal and existing vehicle health-owner regressions pass.
- Independent fresh-context GPT-6.1 Sol XHigh review accepted final code/routing,
  documentation and scoped selection, with no remaining Critical/Important findings.
  Optional extra non-null rollback-oracle assertion is redundant with the dedicated
  successful birth oracle and is not required for this bounded acceptance.
- Optimization: reuse already loaded detached arrays/owner exports; no extra source
  readers, expensive reconstruction, permission cache or alternate writer. The new
  transient-ref set is a per-pass identity index only. No speedup claim.
- SDK 10.0.404 targets net8.0; .NET/ASP.NET runtime 8.0.34; pwsh 7.7.0. Source environment
  `/workspace/shared/boe-tools/1515-env.sh`; serial build flags are recorded below.
  Raw logs/TRX stay outside Git. No hosted CI, Windows qualification or main merge.
- Next: complete first-birth semantic vehicle/cargo/access contract and evidence,
  then governed storage birth (including an existing location) and bounded repair.
  Existing location birth receipts already cover storage metadata born with that
  location; do not create a duplicate receipt. Vehicle health history is not a full
  semantic vehicle receipt. Ordinary post-birth movement/access redesign is excluded.

Clean restoration of checkpoint `9e951bb6c7626378f86a2b5dcb8615c6a9f92e0d` was independently fetched into a new shallow directory; exact HEAD, clean status and `git fsck --connectivity-only` all verified. The next source block remains unimplemented/unrun.

## WIP birth shape progress, 2026-11-15

At `34497969d`, the official unit category produced causal RED: 1 positive PASS,
20 negative FAIL, all expected missing diagnostics with empty issue collections.
21 planned/executed, no timeout/duplicates, cleanup complete, 9.667s runner;
unit build 89.59s, 0 errors and 156 preexisting warnings. The new structural
contract is now implemented in owner composition but GREEN is unrun. Actual
MetaMisc admission tests are added but still need their separate causal RED;
the observed result was 1 positive PASS / 6 causal missing-field FAIL, all empty diagnostics (7/7 completed, 12.503s runner, complete cleanup). That production entry is now connected; GREEN remains unrun. Integration RED build: 124.65s, 0 errors, 234 warnings across rebuilt projects. Faction/NPC resolution, effective
access, complete receipt/cargo evidence and storage birth remain incomplete.
No publication qualification or complete #1515 acceptance follows from this slice.

Review caught the primary Block_10 vehicle schema still missing the six fields;
it is corrected and included in the source guard. Two existing original-allocation
integration leaves also needed the same Mortal fixture-only six-field migration;
their production logic and assertions stay unchanged. A dedicated two-test category
covers this shared-composer interoperability, not spiritual-combat feature work.
The first GREEN unit build succeeded (97.86s, 0 errors, 156 warnings), but a Bash
comma argument was passed as one category string and the runner refused before
workloads. Correct PowerShell array invocation follows; this is not a test failure.

## Next bounded block: complete vehicle birth shape

- [ ] Add typed causal RED tests against the existing owner composer for missing/invalid cargo limits, ownership/controller/access and unknown single-player IDs; add one valid positive fixture.
- [ ] Add `MortalTransportStorageContract.ValidateVehicleBirth(JsonObject, string)` returning indexed issues, called before identity allocation by owner composition and by new-vehicle shape admission. This first slice is structural and player-reference validation only; faction/location authority and full receipt/cargo evidence remain explicit follow-on work.
- [ ] Migrate active creation fixtures, add a narrow catalog entry, publish WIP before official RED/build. Implement only after causal failures, verify targeted positives/negatives and actual shape entry.
- [ ] Independent review of the bounded implemented slice, then durable checkpoint. No whole-issue completion claim.

## Global constraints

Mortal world only; no legacy save promotion, new travel simulation, hosted CI, full-suite run, main merge or issue closure. Preserve complete source/lease/generation and item/resource identity checks. Raw diagnostics stay outside Git in `/workspace/shared/BoeDiagnostics/1515`; active bootstrap/templates/fixtures follow the current schema.

## Source map and decisions

- `MortalResourceOwnerComposer.ComposeVehicleOwners` already allocates client vehicle IDs and captures health materialization. Extend it; never allocate again in a new subsystem.
- `ValidationService.MetaCodexAndAchievements.cs` has full/partial shape checks but lacks cargo/access/owner semantics. Pure new `MortalTransportStorageContract` validation must be called from both shape admission and final composed state, not just prompts.
- `MortalLocationStorageMetadataContract` and `MortalLocationStorageContentsState` already own storage metadata and contents. Keep these authorities and enrich their existing accepted planning, not duplicate inventory.
- `StorageTransportMoveService` already uses `MortalItemTransitionWriter` and canonical lease. Preserve that existing transition and its ordinary lifecycle; test new-schema interoperability without adding a new movement policy.
- `CanonicalStateNormalizer.AcceptedMechanics.cs` publishes `OwnerCompanionAfterImages` in the common transaction. Storage access must be added to the original backup/source and governed-path sets before using this channel.
- The initial gap in `activeVehicleChange` reduction is now fixed in the verified active-selection block. Canonical `activeVehicleId` must agree with availability, and same-turn vehicleRef must resolve through the existing composer.
- CLI storage-access screen currently shows commands; effective canonical projection must replace that interpretation without UI redesign.

## Review focus

1. Same-turn location + vehicle + item creation resolves once with no stale reference or duplicate ID.
2. Active selection only, removal of selected vehicle and replacement selection are coherent.
3. Shared/restricted access cannot be bypassed with current-location projection or offscreen contents.
4. Capacity equality passes; excess, malformed numeric values and repeated/duplicate contents fail atomically.
5. A late fault restores entity, access/active projection, items/resources and receipt/source evidence together.

## Bounded implementation sequence

### Block A: Complete semantics and cheap contract tests

- [ ] Define concrete fields in the existing vehicle/storage contracts and worked examples, preserving existing IDs, health, location, condition and access semantics where already present.
- [ ] Add typed `MortalTransportStorageContractTests` and category `mortal-transport-storage-contract`; prove missing owner/controller/purpose/cargo/access/provenance and boundary capacity cases fail for the intended reason before implementation.
- [ ] Implement pure one-pass validators; integrate vehicle creation and storage metadata entry points; update active fixtures and GM documentation for changed fields.
- [ ] Run the category through `scripts/test-csharp.ps1`, record exact counts and independent review; publish WIP before long build/review.

### Block B: Canonical selection/access and evidence

- [ ] Add pure reduction tests for exact transient selection, stale/missing targets, grant/share/revoke, duplicate and contradictory access; specify canonical access schema in spec before implementation.
- [ ] Extend existing vehicle composer, storage accepted planning, original-source readers and common companion publication to produce canonical state and versioned creation evidence.
- [ ] Add genuine integration category `mortal-transport-storage-publication` for vehicle + shared/restricted storage birth, same-turn contents, rejected partial writes and late rollback. Retain existing resource/location/item owner invariants.
- [ ] Verify exact tests, review related contracts and update source/daemon guards in the same bounded block.

### Block C: Existing consumers and final qualification

- [ ] Add deposit/retrieve tests for accepted fresh access, wrong location, at/over capacity and failure without mutation; integrate into existing move path.
- [ ] Update CLI/web canonical projection interpretation and corresponding narrow consumer tests.
- [ ] Reconcile `tests/selection.json` against the entire diff; discovery validates ownership without running all categories. Run only new and affected existing resource/location/item/consumer selections.
- [ ] Independent review of complete FR1–FR9 coverage, scoped fixes and verified clean GitHub restoration. Leave branch ready for user integration decision, issue open.

## Optimization assessment

Reuse detached JSON/accepted plans already read by resource/location owners within their stable operation. New pure contract checks are bounded single traversals, so no additional cache is justified. Do not re-read every file in each field helper, reconstruct GameEngine for pure tests, cache authorization, share mutable roots or add a parallel dictionary cache. Current source/lease/version checks remain fresh. Before integration, measure named preparation/planning/publication sections and inspect repeated expensive source reads; no speedup is claimed without measured equal-work evidence.

## Recovery checkpoint — 2026-11-15, WIP design

Remote task branch created and verified at base `f3d0bc668481d1b648381a83dfc39d425fec8bde`. Fresh public GitHub clone provided the complete source; no earlier local WIP assumed. #1511/#1513 live issues are completed and explicitly leave capacity/entity work to #1515; #1514 does not technically block this Mortal implementation. Read-only mapping confirmed the gaps above. Production/tests unchanged. Spec Kit clarify paths validated; design schema details, all implementation and tests/reviews remain UNRUN. Next: complete bounded policy schema and start causal RED tests, not a broad audit.

Linux x64 tool restoration: PowerShell 7.7.0 from official release runs with writable HOME/XDG_CACHE_HOME; SDK 10.0.404 installed in `/workspace/shared/boe-tools/dotnet`, .NET 8 runtime installation pending verification. No hosted Actions dispatched. Current status is not implementation completion or acceptance.

### Ruling: start with the isolated active-selection defect

The existing vehicle composer already provides a pure entry point. Verify its missing active command reduction first while completing ownership schema discovery; this avoids changing broad storage metadata before its identity authority is known. Selection must match one final Active vehicle exactly; same-turn refs resolve only via the existing allocated owner export. This is a partial FR2 block, not full semantics or whole-task completion. Added 6 expected unit cases; compile/execution are UNRUN, production unchanged.

### Active-selection causal RED and first implementation

At source c041c92 the official category selected/executed 6 cases, all 6 failed causally: missing companion for selection-only, absent active ID for same-turn birth, unresolved/Parked selection accepted. No skips/duplicates, cleanup complete; 9.439s runner, 80ms tests. First fresh serial unit build: 0 errors, 156 existing warnings, 88.69s. Ordinary parallel MSBuild failed before tests because this sandbox blocks Unix sockets, including after escalation; serial `-m:1 -p:BuildInParallel=false -p:UseSharedCompilation=false` build followed by official `-NoBuild` category works. Set all XDG directories and DOTNET_CLI_HOME under writable tool home. ASP.NET Core 8 runtime also installed.

First reduction implementation now consumes explicit selection using existing allocated exports and validates final Active agreement. It is WIP: GREEN, direct-field protection, removal/null/replacement coverage, canonical validators, GM guidance and independent review pending. No complete FR2 or issue acceptance is claimed.

First GREEN build exposed a compile-only API name mismatch (`ResourceOwnerExport.OwnerRef`, not `SameTurnRef`); corrected before rerun. This compile failure is not behavioral RED.

### Explicit selection GREEN; next continuity cases

At d1cbc787 the fresh build passed (0 errors, 156 warnings, 93.09s); official `mortal-vehicle-active-selection -NoBuild` passed 6/6, 0 skips/duplicates, complete cleanup, 8.954s runner / 84ms tests. This proves explicit selection only. Seven additional cases now cover direct canonical spoofing, removal, parking/clear/replacement and duplicate Active state. These new cases are UNRUN; independent review and integration remain pending.

Continuity RED at dae28787: 13 planned/executed, 10 PASS and exactly 3 expected failures (direct canonical change accepted, selected removal left ID, parking without clearing accepted); 9.864s runner, clean ownership cleanup. Fresh build 86.53s / 0 errors / 156 warnings. Added client-owned comparison and retained-selection validation, clearing a removed selected identity. GREEN and complete active-flow integration still pending.

Added one further pending case: a new Active vehicle without an explicit selection must not leave canonical selection absent. Next run expects this remaining RED while verifying the three continuity fixes.

At a464fcda, all 13 prior cases passed and only the new missing-selection case failed as expected (14/14 executed, 9.061s runner, 90.51s build). Selection agreement is now checked even without a command, and every published vehicle after-image emits activeVehicleId (null when no vehicle is Active). Final GREEN and review pending.

Independent fresh-context Sol 6.1 XHigh review found two Important reducer defects: retained canonical IDs must ignore the transient namespace, and Active detection must match the existing case-insensitive availability contract (identities remain exact). Five regression cases added, UNRUN, before fixes. Added real cross-reference/common-publication integration tests plus existing ResourceVehicleOwner/Integration selections in a narrow category; no broad resource family run. New integration checks are UNRUN.

Reviewer regression RED at 273f7d4: unit 19 executed, 16 PASS/3 expected FAIL (retained namespace collision, lowercase selected, lowercase clear), 9.623s runner / 94.01s build. The mixed-case duplicate assertion was strengthened to isolate uniqueness rather than rejection of a lowercase selected vehicle. Integration 13 executed, 12 PASS/1 expected FAIL (same-turn ref rejected by cross-reference admission); real active birth/removal and existing vehicle regressions passed, 12.056s runner / 61.84s build. Both owned cleanups complete, no skips/duplicates. Implemented the two reducer fixes and exact transient admission; GREEN pending. Added case/whitespace transient negatives and canonical empty-selection shape admission regression (UNRUN). No cache is added: already loaded final arrays/owner exports are reused; current authority remains fresh.

At bd055fc2 unit 19/19 GREEN (9.139s runner, 93.20s build); integration 16 executed, 15 PASS and only the new activeVehicleId unknown-top-level admission regression RED (12.827s runner, 57.80s build). Allowed canonical field is now added; GREEN pending. Independent Sol6.1 XHigh re-review confirmed both reducer findings fixed and no additional Important issue beyond shape admission. Added one genuine physical vehicle-member publication cut using existing rollback fixture (UNRUN), plus synchronized primary GM rules, existing vehicle worked example and daemon repair guidance/source guard. No separate GM transport template is needed for this selection-only block; full transport materialization documentation remains next. Item/location manifests are unaffected by this field-only vehicle block.

Live issue reread corrects scope: worked example is shared OR restricted storage; no new multiplayer participant authority required. Ordinary post-birth move/access lifecycle redesign is out of scope. First-birth compatibility remains required. Cargo volume ambiguity is confirmed in Rules/Block_9 per-unit vs Block_10 container-internal definitions. Next contract uses explicit external cargo measurement for ambiguous stacks/containers, bound to quantity/shape, not an inferred external footprint or broad item schema rewrite; only proven single ordinary item physical volume can be reused. No such cargo field is implemented yet.

At 123fe7dd the current 20 unit/source cases pass (8.952s runner, 91.75s build). Integration 17 executed, 16 PASS/1 fixture failure: new full-refresh rollback leaf stopped before publication because the fixture omitted the existing Mortal item normalization authority. Zero cuts reached, so this is not rollback evidence or a production failure. Added the empty current item index and existing item admission before resource admission, matching the reference full-refresh fixture; no production change or bypass. Rerun pending. Fresh-context Sol6.1 XHigh review accepted source/docs/tests with no findings, conditional on actual GREEN.

Targeted repair-route tracing found an additional genuine gap before acceptance: new vehicle_active_selection_* errors with section Vehicles are not recognized by ResourceRepairPacketBuilder and could fall into broad repair. Two new tests bind actual composer failures to the existing fail-closed classifier (UNRUN); production unchanged pending causal RED. Planned narrow fix is the existing resource_owner_vehicle_* prefix, not a wider resource repair allowlist. A complete bounded first-birth packet remains future scope, not claimed by a repairHint alone.

Routing RED at bf5b444f:22 planned/executed,20 PASS/2 exact expected failures in protected-rollback classification,9.061s runner/88.45s build. Changed only the two new issue codes to resource_owner_vehicle_active_selection_* so the existing classifier rejects protected/ambiguous selection instead of broad GM repair; bounded allowlist untouched. Rollback fixture at acdf30b reached the real vehicle cut once and passed exact byte/generation/journal restoration, then its final semantic oracle failed on UTF-8 BOM. The oracle now decodes UTF-8 and strips the file preamble before parsing, preserving raw byte assertions; no production change. Integration17 had16 PASS/1 oracle failure,14.366s runner/120.21s build. Final two-category GREEN remains pending.

### Preparation review refinements, 2026-11-15 (SOURCE, not yet GREEN)

At c4e5576d, the official preparation category yielded 9 PASS / 2 causal FAIL
(11.611s, complete cleanup): explicit-null canonical vehicles incorrectly admitted
preparation, and an observed no-birth input retained the previous allocation DATA.
The generation test fixture now uses the required replacement lease and passes.
Source corrections distinguish absent from explicit-null canonical collections and
clear preparation on ordinary observed no-birth input. Reuse remains scoped to
observed preparation input/full invalidation; unrelated earlier failures do not
confer authority and still require fresh source fences. No spiritual branch changes.

### Next bounded block: same-turn new-item cargo (test SOURCE)

Two genuine integration leaves use the existing item/bootstrap/common-plan helper:
vehicle birth with sealed new cargo and exact permanent carrier/health; foreign
vehicle-ref source authority rejects without raw mutation. No result exists yet.

Implementation sequence after causal RED:
1. Prepare ordinary vehicle allocation from signed original plus current raw roots
   before raw item cataloging, reusing the reads and registry owner already present.
2. Explicit birth-command cargo scanning consumes trusted ref/ID preparation, and
   `new_vehicle_inventory` / `new_vehicle` resolves to its exact parent birth ref.
   Do not broadly enable other item routes for vehicles.
3. Frozen route evidence already binds exact ref to permanent carrier and raw roots.
   Derive a local checked ref/ID index from it during projection; reject conflicting
   mappings, wrong parent commands and nonempty unmapped cargo. No new cache or
   full preparation field in the normalization snapshot is needed.
4. Collect/seal cargo through existing item projection and reference rewriting.
   Compare frozen raw vehicle input and route IDs with current preparation before
   passing the item-phase vehicle after-image to the resource owner composer.
   Prepare from raw, never from projected JSON. Keep raw item resource proposals.
5. Verify physical common publication preserves the item receipts/index/carrier and
   owner health. Then add a reached-cut rollback leaf and targeted impact tests.

Existing-only cargo relocation is not authorized by a new-item route. Keep it
fail-closed unless the existing transition machinery is explicitly integrated;
this bounded block does not claim it, capacity, or whole-issue completeness.
