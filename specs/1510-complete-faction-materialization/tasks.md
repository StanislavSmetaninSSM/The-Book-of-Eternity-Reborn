# Tasks: Complete Faction Materialization

**Input**: Design documents from
`/specs/1510-complete-faction-materialization/`

**Source issue**:
[#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510)

**Method**: Superpowers TDD, systematic debugging on unexpected failures,
code-review gates per coherent slice, and fresh verification before completion.

**Test policy**: Use only `pwsh .\scripts\test-csharp.ps1`. During development,
run exact project-scoped `Focused` filters: omission selects the fast project
and `-FocusedProject Integration` selects the integration project. Never mix
classes from both projects in one filter. Run one Fast checkpoint after stable
cross-domain implementation, one required FullValidation after the Shining
docs boundary stabilizes, and one clean PreMerge immediately before
integration. Do not widen timeouts/concurrency or invoke an unbounded full
suite.

## Format

- `[P]` means work is independent in different files after its prerequisites.
- `[US1]` through `[US4]` map directly to the approved user stories in
  `spec.md`.
- Every implementation/test task names exact repository paths; verification and
  commit tasks name their exact command or prerequisite task.
- A test task must reach an observed RED result before its paired production
  task begins.

## Phase 1: Setup and baseline

- [ ] T001 Confirm branch/worktree, run `git status --short`, read issue #1510,
  `AGENTS.md`, `.specify/memory/constitution.md`, this feature directory, and
  `docs/testing.md`; preserve unrelated/untracked `.serena/`, `bin/`, and
  `obj/` artifacts.
- [ ] T001a Record the user-approved pre-release save policy in
  `.specify/memory/constitution.md`, `.specify/templates/plan-template.md`,
  `.specify/templates/spec-template.md`, `.specify/templates/tasks-template.md`,
  and `AGENTS.md`: the game has not shipped, backward compatibility with old
  save/state schemas is not a requirement, and obsolete test fixtures are not
  compatibility authority unless a tracked issue/spec explicitly creates an
  exception. Reconcile this feature's legacy requirements before further code
  changes.
- [ ] T002 Confirm the saved Fast baseline (2,633/2,633 in 4:16.219) still
  describes the starting commit; do not rerun Fast merely for setup unless the
  branch base changed.
- [ ] T003 Implement the approved bounded Focused integration selector from
  `docs/superpowers/specs/2026-08-03-focused-integration-selection-design.md`:
  add RED closed/default/scope/override coverage in
  `BookOfEternityClient.Tests/FastTestBoundaryTests.cs`, add
  `-FocusedProject Fast|Integration` with default `Fast` and non-Focused
  rejection in `scripts/test-csharp.ps1`, document it in `docs/testing.md`,
  then prove both project plans and one exact pre-existing integration method
  within the unchanged five-minute Focused limit. Record the project-scoped
  filters from `quickstart.md` in the implementation log/PR notes.

**Checkpoint**: Issue, branch, scope boundaries, and bounded verification are
known.

---

## Phase 2: Foundational common contract and accepted-turn fence

**Purpose**: Establish the common immutable contract and touch classifier that
both domains require.

### Tests first

- [ ] T004 [P] Add RED closed-schema tests in
  `BookOfEternityClient.Tests/FactionMaterializationContractTests.cs` for exact
  fields, both profiles, duplicate members, unknown members, scalar errors,
  unique materialization IDs, disposition forms, capability mismatch, exact
  empty evidence, and identity mismatch.
- [ ] T005 [P] Add RED accepted-turn classification tests in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for new, legacy promotion, already materialized, untouched legacy,
  client-derived-only, duplicate pre-turn members, case-sensitive IDs, and
  immutable historical envelopes, including one duplicate
  `materializationId` shared by a Mortal and a Shining faction.
- [ ] T006 Run
  `pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~FactionMaterializationContractTests"`
  and
  `pwsh .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~FactionMaterializationValidationTests"`;
  record the expected missing-contract/classifier RED failures separately for
  the fast and integration projects.

### Implementation

- [ ] T007 Implement the closed common kernel in
  `BookOfEternityClient/Services/FactionMaterializationContract.cs`, mirroring
  the audited Actor Materialization shape without sharing actor-specific
  capability logic.
- [ ] T008 Implement duplicate-sensitive pre-turn loading, exact identity maps,
  touch categories, immutable semantic envelope comparison, and one shared
  cross-domain `materializationId` candidate set containing every current
  receipt exactly once per case-sensitive domain/identity coordinate—including
  untouched accepted history—and validate it only after both Mortal and
  Shining collection in
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`.
- [ ] T009 Wire raw faction checks into
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
  `CollectAcceptedTurnRawStateIssuesAsync` before canonical refresh.
- [ ] T010 Re-run both T006 project-scoped focused commands to GREEN and inspect
  issue coordinates for exact `mortal_faction:<id>` /
  `shining_faction:<id>` routing.

### Selectable canonical phase

- [ ] T011 Add RED phase-bit/profile tests in
  `BookOfEternityClient.Tests/ValidationPhaseSelectionTests.cs`,
  `BookOfEternityClient.IntegrationTests/FullValidationEquivalenceTests.cs`, and
  `BookOfEternityClient.IntegrationTests/IntegrationTestBoundaryTests.cs` for
  `AcceptedTurnFactionMaterializationCompleteness`.
- [ ] T012 Add the new non-overlapping flag to
  `BookOfEternityClient/Services/Validation/GameStateValidationPhase.cs`,
  include it in `All`/`Selectable`, add relevant Mortal/Shining profiles in
  `BookOfEternityClient.IntegrationTests/IntegrationValidationProfiles.cs`,
  and invoke it from
  `BookOfEternityClient/Services/Validation/ValidationService.ValidationPhases.cs`.
- [ ] T013 Run the exact phase-selection/equivalence/boundary focused filter
  from `quickstart.md` to GREEN.
- [ ] T014 Commit the common foundation as
  `feat: add faction materialization foundation (#1510)`.

**Checkpoint**: Common envelope, touch classification, raw fence, immutable
continuity, and selectable canonical phase are independently green.

---

## Phase 3: User Story 1 — Complete Mortal factions (Priority: P1) 🎯 MVP

**Goal**: New/promoted Mortal factions have complete semantic core, all seven
governed surfaces, exact cross-file identity, and real initial history.

**Independent test**: A populated creation and a seven-section
empty-by-design creation pass; omitted/contradictory semantics, sidecars, and
references fail atomically.

### Semantic and bundle tests first

- [ ] T015 [US1] Extend
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  with RED Mortal tests for missing/invalid `factionColor`, `purpose`,
  `currentAgenda`, `principles`, `memory`, governance, leadership, initial
  chronicle, invalid leadership states, and missing/duplicate IDs.
- [ ] T016 [US1] Add RED Mortal governed-section tests in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for populated evidence, all seven exact empty surfaces, missing sidecar
  entries in canonical validation (but not before normalization), populated/
  empty contradictions, whitespace reasons, project-row evidence,
  territory/location control, player membership, and custom state.
- [ ] T017 [US1] Add RED exact cross-reference tests in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for same-turn effective IDs, location control, relation targets, NPC
  affiliation, leader NPC IDs, orphaned sidecars, and collision with pre-turn
  permanent IDs.
- [ ] T018 [US1] Run the common materialization focused filter and record the expected
  Mortal RED failures.

### Mortal validator

- [ ] T019 [US1] Implement semantic-core/evidence construction and raw/canonical
  Mortal bundle validation in
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`;
  require complete live shape for every receipt-bearing faction while
  deferring historical snapshot-to-live evidence after raw promotion, and wire
  it into
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`.
- [ ] T020 [US1] Tighten full-carrier shape and creation/promotion legality in
  `BookOfEternityClient/Services/Validation/ValidationService.FactionsAndProjects.cs`,
  preserving existing production field validators and rejecting ordinary full
  resends.
- [ ] T021 [US1] Extend canonical sidecar validation in
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
  for mandatory governance/leadership structure fields and exact materialized
  empty records without forcing untouched legacy entries.
- [ ] T022 [US1] Re-run the Mortal-focused tests from T018 to GREEN.

### Normalization tests first

- [ ] T023 [US1] Add RED normalizer tests in
  `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Factions.cs`
  proving full-carrier extraction to core/structure/resources/projects/custom,
  exact empty entries, same-turn ID binding, preservation of legacy content,
  and removal of carrier-only sidecar fields from new/promoted core.
- [ ] T024 [US1] Add RED chronicle tests in
  `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.Factions.cs`
  proving `scribeChronicle[]` becomes deterministic target-bound
  `faction_chronicles.json.entries[]`, is consumed, preserves prior history on
  promotion, and is forbidden on ordinary updates.
- [ ] T025 [US1] Run the normalizer focused filter and record the expected RED
  extraction/chronicle failures.

### Mortal normalizer

- [ ] T026 [US1] Extend
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.FactionAndInventoryHelpers.cs`
  with exact materialized entry extraction, structure governance/leadership,
  deterministic chronicle conversion, and effective-ID binding.
- [ ] T027 [US1] Update
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs`
  to distribute/consume the complete raw carrier, create exact materialized
  sidecar emptiness, preserve untouched legacy state, and never invent
  narrative semantics/reasons/receipts.
- [ ] T028 [US1] Verify the call order in
  `BookOfEternityClient/Services/CanonicalStateNormalizer.cs` keeps full-carrier
  evidence available until every Mortal sidecar and chronicle has extracted its
  target records; change ordering only with a focused regression test.
- [ ] T029 [US1] Re-run the T025 normalizer filter and the T018 Mortal validation
  filter to GREEN.
- [ ] T030 [US1] Commit the Mortal creation/promotion slice as
  `feat: enforce complete mortal faction materialization (#1510)`.

**Checkpoint**: US1 passes independently, including minimal deliberate
emptiness and atomic cross-file identity.

---

## Phase 4: User Story 3 — Safe legacy promotion and narrow Mortal updates (Priority: P1)

**Goal**: Untouched legacy factions load; the first GM touch promotes fully;
already materialized factions use closed commands and preserve the receipt.

**Independent test**: One legacy faction remains unchanged, one promotes, one
uses each allowed core group, and all bypass/partial/unknown/protected commands
fail.

### Contract tests first

- [ ] T031 [P] [US3] Add RED closed-command tests in
  `BookOfEternityClient.Tests/FactionCoreChangesContractTests.cs` for exact root
  fields, required reason/group, all six complete groups, recursive unknown
  members, protected fields, absolute values, relation target identity, and
  immutable receipt protection.
- [ ] T032 [P] [US3] Add RED production-flow tests in
  `BookOfEternityClient.IntegrationTests/FactionCoreChangesTests.cs` for valid
  application/consumption, invalid-command preservation, unrelated-field
  preservation, governance/leadership structure updates, relations, unknown
  target, legacy target, same-turn promotion plus dedicated mutation, and
  already-materialized full resend. Prove an initially empty receipt remains
  unchanged when a valid narrow update adds the first live relation.
- [ ] T033 [US3] Run the focused core-change filter and record the expected missing
  response/contract/normalizer RED failures.

### Command implementation

- [ ] T034 [US3] Add `FactionCoreChanges` serialization to
  `BookOfEternityClient/Models/GameResponse.cs` and map
  `factionCoreChanges` to `game_state/factions/faction_core.json` in
  `BookOfEternityClient/Configuration/FileMapping.cs`.
- [ ] T035 [US3] Implement closed validation/evaluation/apply primitives in
  `BookOfEternityClient/Services/FactionCoreChangesContract.cs`, following the
  consume-on-success `NpcCoreChangesContract` pattern.
- [ ] T036 [US3] Implement duplicate-sensitive current/pre-turn command validation in
  `BookOfEternityClient/Services/Validation/ValidationService.FactionCoreChanges.cs`
  and call it from the raw accepted-turn fence.
- [ ] T037 [US3] Update
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
  so `factionCoreChanges` is an allowed closed command surface and invalid
  markers remain repair-visible.
- [ ] T038 [US3] Add `NormalizeFactionCoreChangesAsync` to
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.Factions.cs`
  and place it after full-core materialization but before downstream readers in
  `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`.
- [ ] T039 [US3] Re-run the T033 focused filter and the Mortal materialization filter
  to GREEN.

### Legacy and bypass closure

- [ ] T040 [US3] Add/confirm RED-to-GREEN tests in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for every GM touch channel, derived-only whitelisting, receipt removal/change,
  promotion preserving history, and unrelated historical mutation rejection.
- [ ] T041 [US3] Commit the safe-update slice as
  `feat: add narrow mortal faction updates (#1510)`.

**Checkpoint**: US3 passes independently; there is no legal full-object bypass
for an already materialized Mortal faction.

---

## Phase 5: User Story 2 — Complete Shining factions (Priority: P1)

**Goal**: Native, player-founded, and story/hidden Shining factions are complete
before normalization and retain every existing route-specific invariant.

**Independent test**: One creation per route passes; missing raw semantics,
wrong counts/receipts/authority, and incomplete actors fail.

### Raw semantic fence tests first

- [ ] T042 [P] [US2] Add RED Shining tests in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for missing/invalid provenance, hall, charter, agenda, lifecycle, leadership,
  visibility, story-authority shape, strategic memory, chronicle, governed
  sections, receipt, exact hall/resident binding, exact empty trade surface,
  and `canTrade` behavior for operational non-vacant versus vacant leadership.
- [ ] T043 [P] [US2] Add RED semantic-default laundering tests in
  `BookOfEternityClient.IntegrationTests/CanonicalStateNormalizerTests.ShiningAbode.cs`
  proving missing origin/charter/leadership/lifecycle/agenda/visibility/memory
  fails raw validation rather than becoming valid after
  `NormalizeStateRoot`.
- [ ] T044 [US2] Run the materialization/normalizer focused filters and record the
  expected Shining RED failures.

### Common Shining profile

- [ ] T045 [US2] Implement the common Shining semantic core,
  capability/disposition, provenance/story-authority shape, hall/resident
  binding, and canonical evidence in
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`;
  require complete live shape for every receipt-bearing faction while
  deferring historical snapshot-to-live evidence after raw creation/promotion,
  and wire it into
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`.
- [ ] T046 [US2] Extend
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningAbode.cs`
  with the new closed authored fields and production-valid nested shapes while
  preserving existing lifecycle/trade/political validators.
- [ ] T047 [US2] Restrict
  `BookOfEternityClient/Services/ShiningAbodeState.cs` and
  `BookOfEternityClient/Services/ShiningAbodeState.Gameplay.cs` plus
  `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.ShiningAbode.cs`
  so every faction carrying a receipt receives only allowed mechanical
  normalization;
  retain explicit compatibility behavior for untouched legacy factions, never
  auto-create an absent Guardian faction/hall, and never rewrite authored
  semantics on a faction carrying a receipt.
- [ ] T048 [US2] Re-run the T044 focused filters to GREEN for common Shining
  completeness and default-laundering cases.

### Native discovery route

- [ ] T049 [US2] Add RED native-discovery cases in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for exactly 1 hall, 1 faction, 2–4 new residents, exactly 2 completed seeded
  projects, exact request/receipt/cost binding, Actor Materialization, and
  unchanged unrelated state; run the exact new methods and record the expected
  materialization-composition RED failures before T050.
- [ ] T050 [US2] Compose materialization evidence with existing native discovery
  validation in
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
  without duplicating its cost/count/constrained-diff authority.
- [ ] T051 [US2] Run the exact native-discovery test methods to GREEN.

### Player founding route

- [ ] T052 [US2] Add RED player-founding cases in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for exact request/charter/hall/supporters, player-soul leadership, reserved
  costs, unique founding receipt, leadership history, resident links, and full
  materialization; run the exact new methods and record the expected
  materialization-composition RED failures before T053.
- [ ] T053 [US2] Compose materialization evidence with existing founding closure in
  `BookOfEternityClient/Services/Validation/ValidationService.LifecycleControlAndStateFiles.cs`
  and extend expected composite construction only with explicitly authored
  materialization fields.
- [ ] T054 [US2] Run the exact player-founding test methods to GREEN.

### Story/hidden route

- [ ] T055 [US2] Add RED story/hidden cases in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  for exact supported story ID/role/visibility, Saref linkage, hidden metadata
  completeness, guardian-ascension authority, and rejection of name/tag/prose
  inference; run the exact new methods and record the expected
  story-authority RED failures before T056.
- [ ] T056 [US2] Implement exact story-authority resolution in
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`
  and update any necessary canonical story cross-reference helper without
  broadening #1368 or #1462.
- [ ] T057 [US2] Run the exact story/hidden test methods to GREEN.

### Actor and derived-value boundary

- [ ] T058 [US2] Add RED cases for non-player heads, political actors, newly
  significant residents, vacant leadership, `player_soul`, and client-derived
  strength/tier/service recomputation in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`;
  run the exact new methods and record the expected actor/derived-boundary RED
  failures before T059.
- [ ] T059 [US2] Implement the actor and derived-value boundary in
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`:
  validate linked actors through the existing evidence-aware
  `ValidateAfterlifeProfile` path with the exact current trade-authority set
  (not the canonical helper that hardcodes `canTrade=false`), whitelist only
  exact client-owned strength/tier/service paths, then run the complete
  Shining-focused selection to GREEN and confirm existing route-specific issue
  codes remain alongside, not underneath or instead of, materialization
  issues.
- [ ] T060 [US2] Commit the Shining slice as
  `feat: enforce complete shining faction materialization (#1510)`.

**Checkpoint**: US2 passes independently for all three creation routes and raw
validation proves semantic defaults cannot legalize incomplete state.

---

## Phase 6: User Story 4 — Bounded repair and clear GM contract (Priority: P2)

**Goal**: Materialization failures produce exact repair packets; GM guidance is
complete; player clients do not expose private metadata.

**Independent test**: Representative Mortal/Shining defects route to one exact
coordinate and target; worked examples validate; console/browser source guards
find no envelope/reason rendering.

### Repair tests and routing

- [ ] T061 [US4] Add RED repair-packet tests in
  `BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.cs` for
  stable issue-family recognition, one packet per exact Mortal/Shining
  coordinate, target files/selectors, preserved sections, and prohibited
  unrelated roots; run the exact new methods and record the expected missing
  faction-routing RED failures before T062.
- [ ] T062 [US4] Add faction issue recognition, exact file resolution, and bounded
  packet construction to
  `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`,
  following `BuildActorMaterializationRepairPacket` without invoking #1222.
- [ ] T063 [US4] Run the exact repair tests to GREEN, including a repair that attempts
  an unrelated faction mutation and must fail.

### Player metadata privacy

- [ ] T064 [P] [US4] Add RED source/privacy guards to
  `BookOfEternityClient.Tests/ValidationSourceGuardTests.cs`,
  `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs`, and
  `BookOfEternityClient.Tests/AfterlifeShiningPlayerFacingSourceGuardTests.cs`
  proving ordinary console/browser faction readers do not render
  `materialization`, schema/version IDs, disposition tokens, or private reasons;
  add canonical `visibility=hidden|rumored|revealed` behavior tests in
  `BookOfEternityClient.Tests/ShiningAbodeStateTests.cs`, then run the exact new
  methods and record the expected privacy/visibility RED failures before T065.
- [ ] T065 [US4] Update
  `BookOfEternityClient/Services/SarefMainStoryState.cs` to filter all
  non-revealed materialized factions with legacy Saref fallback, and route
  ordinary enumerations/audit clones in
  `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.ShiningAbode.cs`
  and
  `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs` through
  that filter while recursively removing private materialization metadata; do
  not redesign UI or expose a debug panel.
- [ ] T066 [US4] Run the exact privacy/source-guard methods to GREEN.

### GM rules, prompts, examples, and manifests

- [ ] T067 [P] [US4] Add RED documentation/source-coverage assertions in
  `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`,
  `BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`, and
  `BookOfEternityClient.IntegrationTests/ExampleDocumentationValidationTests.cs`
  for both profiles, all routes, `FactionCoreChanges`, repair, and the Guardian
  politics boundary; run those exact three classes and record the expected
  missing-guidance RED failures before T068–T071.
- [ ] T068 [P] [US4] Update Mortal rules and examples in `Rules/Block_21.txt`,
  `Examples/E_Block_21.txt`, `TaskGuides/CLI_Step_Main.txt`, and
  `Examples/E_CLI_Step_Main.txt` with populated/minimal creation, promotion,
  core update, and bounded repair.
- [ ] T069 [P] [US4] Update protocol/prompt surfaces in `CLI_API_Specification.md`,
  `CLI_Agent_Daemon_Specification.md`,
  `Rules/Block_CLI_Operations.txt`, and
  `BookOfEternityClient/game_master_daemon.ps1`, including the compact faction
  template path used by the daemon.
- [ ] T070 [P] [US4] Update Shining contract guidance in
  `OtherGuides/Afterlife_Contract_Matrix.md`,
  `docs/audits/afterlife/shining-abode/Shining_Abode_Faction_Politics_Addendum.md`,
  and `Examples/E_CLI_Afterlife_Turns.txt` for native, founding, story/hidden,
  Actor Materialization links, and raw semantic authority.
- [ ] T071 [P] [US4] Update `Examples/example_validation_manifest.json` with all
  eight worked-example families. Do not change
  `BookOfEternityClient/Services/AfterlifeContractRegistry.cs`: this feature
  changes the existing Shining state contract and introduces no new afterlife
  state-file surface.
- [ ] T072 [US4] Run the focused documentation/privacy filter from `quickstart.md` to
  GREEN and validate that all eight required worked-example families are
  discoverable.
- [ ] T073 [US4] Commit repair/docs/privacy as
  `docs: complete faction materialization contracts (#1510)`.

**Checkpoint**: US4 passes independently and the GM has executable guidance
without player-facing harness leakage.

---

## Phase 7: Cross-cutting verification and review

- [ ] T074 Run every project-scoped focused command from `quickstart.md`:
  fast common and core contracts; integration common/core/normalizer/Shining
  behavior; fast phase selection; integration phase
  equivalence/boundary/repair; fast documentation/privacy guards; and
  integration example validation. Confirm each result names the intended
  project; diagnose any unexpected failure with systematic debugging rather
  than broad changes.
- [ ] T075 Run one Fast checkpoint:
  `pwsh .\scripts\test-csharp.ps1 -Lane Fast`; record result count, duration,
  duplicates, timeout, and owned-process cleanup.
- [ ] T076 Run the required afterlife docs checks:
  `pwsh .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AfterlifeDocumentationCoverageTests"`
  and `pwsh .\scripts\test-csharp.ps1 -Lane FullValidation`; record evidence.
- [ ] T077 Review the complete diff against FR-001 through FR-035, all success
  criteria, `data-model.md`, each contract, the #1222/#1368/#1462 boundaries,
  and the no-timeout/no-concurrency constraint.
- [ ] T078 Request code review of the implementation, reconcile every Critical
  or Important finding in #1510, and create a linked follow-up issue only for a
  truly unrelated or explicitly deferred non-blocking finding.
- [x] T078a Correct the T065 legacy visibility compatibility regression in
  `BookOfEternityClient/Services/SarefMainStoryState.cs` and add exact
  materialized strict-visibility plus untouched-legacy fallback coverage in
  `BookOfEternityClient.Tests/ShiningAbodeStateTests.cs`: factions carrying a
  materialization envelope remain revealed-only/fail-closed, while a legacy
  faction without materialization and with `visibility=public` remains visible
  unless the preexisting Saref Wings fallback hides it.
- [x] T078b Require production-valid `image_prompt` semantics for Mortal
  faction creation, promotion, and canonical materialization in
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  and
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`:
  cover omitted, empty, whitespace, non-English, and overlong values with
  stable missing/invalid issue codes across raw creation/promotion carriers and
  canonical materialized factions.
- [x] T078c Reject forged Shining derived-strength trade authority across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`,
  `BookOfEternityClient/Services/ShiningAbodeState.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`,
  and
  `BookOfEternityClient/Services/Validation/ValidationService.ActorMaterializationTradeAuthority.cs`,
  and record the bounded evidence in
  `sdd/task-11-fix3-cantrade-report.md`: cover forged-high and forged-low raw
  `factionStrength` values whose independently derived strengths cross the
  trade threshold in the opposite direction, plus linked non-player Actor
  Materialization trade-authority parity, without changing Guardian authority.
- [x] T078c1 Reject forged completed-project `strengthReward` trade authority
  across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  and `BookOfEternityClient/Services/ShiningAbodeState.cs`, and record the
  bounded evidence in
  `sdd/task-11-fix3b-project-strength-report.md`: cover high and negative/low
  raw project rewards whose canonical clamped-tier rewards cross the trade
  threshold in the opposite direction, plus linked non-player Actor
  Materialization parity, by using the same tier-to-reward projection as
  normalization.
- [x] T078d Require complete same-turn promotion for every externally touched
  receipt-less legacy faction across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`,
  and
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`:
  classify raw duplicate-sensitive touches from Mortal `factionRankChanges`,
  `factionBonusChanges`, `factionResourceChanges`, `factionProjectUpdates`,
  `completeFactionProjects`, `factionCustomStateChanges`,
  `factionChronicleUpdates`, current-location/world-map `factionControl`, and
  NPC faction-affiliation authority, plus Shining resident
  affiliation/realignment and other governed political, story, leadership,
  memory, influence, resource, project, and supported-route authority; emit
  stable `faction_legacy_promotion_required` at the exact Mortal or Shining
  faction coordinate while preserving the `FactionCoreChanges` guard,
  untouched legacy compatibility, and documented Shining client-derived-only
  projections.
- [x] T078d1 Complete raw legacy-touch carrier and comparator coverage across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`,
  and
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`:
  project Mortal `NPCCoreChanges[].factionAffiliationsToUpsert` and Shining
  `UpdateGuardianAbodeResidents`, `sarefMainStoryUpdate`, and
  `UpdateGuardians` from validated pre-turn exact identity; emit old and new
  source/target legacy coordinates without unrelated coordinates; treat
  political current-faction moves separately from immutable origin links;
  preserve identity-upsert omission and unrelated Guardian controls; and use
  duplicate-sensitive counted/indexed canonical projections for only the
  contract-defined unordered collections so reorder-only changes do not
  promote while duplicate or same-identity semantic changes do. Record exact
  RED/GREEN/build evidence and superseded T078d rationale in
  `sdd/task-11-fix4b-touch-carriers-report.md`.
- [x] T078d2 Close omitted Shining legacy-target touch classification across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`,
  and
  `BookOfEternityClient/Services/Validation/ValidationService.ShiningFactionMaterialization.cs`:
  project the raw Shining `factions` identity-upsert surface from validated
  pre-turn authority with normalizer-equivalent nested merge semantics; feed
  that effective root to Guardian story-authority touch collection; and
  classify externally touched receipt-less pre-turn factions omitted from the
  current raw `factions` array over a synthetic effective legacy carrier.
  Cover Guardian create plus resident and Saref old/new moves with an
  unrelated full faction upsert, exact promotion coordinates, and no unrelated
  promotion. Replace nested linear projection scans with exact actor,
  affiliation, Guardian-existence, and Guardian-count lookups without changing
  identity semantics. Record exact RED/GREEN/build evidence in
  `sdd/task-11-fix4c-omitted-shining-touch-report.md`.
- [x] T078e Reject Mortal legacy promotions that rewrite validated historical
  authority across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`,
  `BookOfEternityClient.IntegrationTests/FactionCoreChangesTests.cs`,
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`,
  and, only if required for safe reuse,
  `BookOfEternityClient/Services/FactionCoreChangesContract.cs` or
  `BookOfEternityClient/Services/Validation/ValidationService.FactionMaterializationContinuity.cs`:
  before normalization preserve representative pre-turn core/profile,
  progression/power, governance/leadership/ranks, resources,
  relations/projects/custom state, chronicle, current-location/world-map
  `factionControl`, and NPC affiliation values; allow promotion to add missing
  required semantics, receipt/disposition, and explicit-empty evidence; allow
  a simultaneous delta only when an existing successfully validated narrow
  command proves that exact change; emit stable
  `faction_materialization_promotion_history_changed` at the exact Mortal
  faction coordinate; and record bounded RED/GREEN/build evidence in
  `sdd/task-11-fix5-promotion-history-report.md`.
- [x] T078e1 Make Mortal promotion-history preservation duplicate-safe and
  bounded across
  `BookOfEternityClient.IntegrationTests/FactionMaterializationValidationTests.cs`
  and
  `BookOfEternityClient/Services/Validation/ValidationService.MortalFactionMaterialization.cs`:
  fold repeated project, structure, resource, and custom-state authority in
  encounter order with the normalizer's exact identity and shallow
  last-property merge semantics; preserve exact validator-owned
  relation/territory multiplicity; build one pre-turn sidecar/project index
  before the promotion loop so no per-promotion method can rescan global
  arrays; retain addition, omission, chronicle, and valid/invalid
  `FactionCoreChanges` boundaries; and record exact RED/GREEN/build evidence
  in `sdd/task-11-fix5b-history-index-report.md`.
- [ ] T079 Run `git diff --check`, verify no `.serena/`, `bin/`, `obj/`, test
  result, or unrelated file is staged, and commit any reviewed corrections in
  coherent `(#1510)` commits.
- [ ] T080 From a clean checkout of the exact final candidate commit, run
  `pwsh .\scripts\test-csharp.ps1 -Lane PreMerge` once; do not precede it with a
  duplicate Fast run.
- [ ] T081 Reconcile every checked task with actual diff and fresh evidence,
  update the PR summary with commands/counts/durations/residual risk, link
  #1510, and mark tasks complete only after inspection.

**Final checkpoint**: All four user stories pass, required bounded lanes are
green within existing limits, and issue #1510 is ready for PR/integration.

---

## Dependencies and execution order

```text
Phase 1 setup
  └─ Phase 2 common contract/raw fence/phase
       ├─ Phase 3 Mortal creation + bundle
       │    └─ Phase 4 legacy promotion + FactionCoreChanges
       └─ Phase 5 Shining routes
            └─ Phase 6 repair/docs/privacy
                 └─ Phase 7 bounded verification/review
```

- Phase 2 blocks every user story.
- Phase 4 depends on Phase 3 because it mutates the canonical Mortal model.
- Phase 5 can begin after Phase 2 in a separately isolated implementation, but
  the recommended single-branch sequence completes Phase 4 first to stabilize
  common classification and continuity.
- Repair routing in Phase 6 depends on stable issue codes from both domains.
- Documentation work may begin in parallel after exact contracts stabilize,
  but its tests and examples cannot be marked complete before runtime shapes are
  final.

## Parallel opportunities

- T004 and T005 touch separate test projects.
- T031 and T032 touch separate test projects.
- T042 and T043 target validator versus normalizer behavior.
- T064 and T067 target privacy versus documentation guards.
- Documentation files T068–T071 can be edited independently after the runtime
  contract is frozen.

Do not parallelize edits to shared validator/normalizer files without isolated
worktrees and an explicit integration owner.

## Implementation strategy

The recommended delivery is incremental:

1. common receipt and raw fence;
2. complete Mortal creation/promotion bundle;
3. narrow existing-Mortal updates;
4. complete Shining route materialization;
5. repair, docs, examples, manifests, and privacy;
6. bounded cross-domain verification and review.

Each slice ends in focused GREEN tests and a small issue-linked commit. Fast,
FullValidation, and PreMerge remain checkpoints, not per-edit loops.
