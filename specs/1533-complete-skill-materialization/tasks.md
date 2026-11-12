# Tasks: Complete Mortal Skill and Mastery Materialization

**Input**: Design documents from `/specs/1533-complete-skill-materialization/`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/skill-reference-identity.md

## Original part 1 implementation (producer checklist; correction evidence below)

- [X] T001 Create Spec Kit feature docs (spec/plan/research/data-model/contracts/quickstart/checklists)
- [X] T002 [RED] Unit tests for `MortalSkillReferenceAuthority` classification (exact, case-variant, Cyrillic homoglyph, Greek homoglyph, unknown, ambiguous confusable key, null/whitespace handling)
- [X] T003 [RED] Integration test: confusable player mastery reference fails with `skill_mastery_confusable_reference` via `GameStateValidationPhase.PlayerStateFiles`; exact passes; unknown keeps `skill_mastery_unknown_active_skill`
- [X] T004 [RED] Integration tests: confusable-only mastery initialization coverage fails with `player_active_skill_mastery_initialization_confusable` and `npc_skill_mastery_initialization_confusable` (active + passive sections) via `GameStateValidationPhase.SkillContractConsistency`; exact and missing keep existing behavior
- [X] T005 [GREEN] Implement `MortalSkillReferenceAuthority` reusing `MortalLocationIdentityState.BuildConfusableKey`
- [X] T006 [GREEN] Additive identity layer `ValidationService.SkillReferenceIdentity.cs` dispatched from `ValidationPhases.cs` (legacy validators untouched as backstops)
- [X] T007 [GREEN] Player mastery reference check + player/NPC initialization checks wired through the authority
- [X] T008 Implement `mortal_skill_reference_identity_repair` packet routing and builder in `GameEngine.ValidationAndRepair`
- [X] T009 Integration test: repair request emits exactly one bounded identity packet with rename-only rules
- [X] T010 Historical producer verification report reviewed; its full-suite command is superseded by the current category-only policy. Current completion evidence is T017 and the final checkpoint in plan.md.

## PR #1579 review corrections (approved revision 1, issue #1533)

- [X] T011 [RED] Capture review regressions in BookOfEternityClient.Tests/MortalSkillReferenceAuthorityTests.cs and BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SkillReferenceIdentity.cs.
- [X] T012 [US1] Preserve ordinal canonical names and precise legacy-issue replacement in BookOfEternityClient/Services/Validation/ValidationService.SkillReferenceIdentity.cs and parser helpers.
- [X] T013 [US3] Preserve indexed actor/skill corrections and unresolved ambiguity in BookOfEternityClient/Core/GameEngine/GameEngine.SkillReferenceIdentityRepair.cs.
- [X] T014 [P] Update all affected partial-file source guards in BookOfEternityClient.Tests and BookOfEternityClient.IntegrationTests without weakening assertions.
- [X] T015 Register test category owners and current impact selection in tests/categories.json and tests/selection.json; run discovery and category planning.
- [X] T016 Synchronize specs/1533-complete-skill-materialization contracts/research/quickstart, GM worked example and XML API documentation.
- [X] T017 Run selected categories and independent whole-block review; record actual results in specs/1533-complete-skill-materialization/plan.md.
- [X] T018 Publish corrections, verify remote SHA and clean GitHub restore; reconcile evidence in specs/1533-complete-skill-materialization/tasks.md and plan.md.

Dependencies: T011 -> T012/T013; T014 is independently implementable; T012-T014 -> T015/T016 -> T017 -> T018. Parent owns integration and catalog edits. Later issue contours remain deferred.

## Part 2 — T107 prose-claimed skill ↔ per-skill state delta coupling (branch `1533-prose-state-delta-coupling`)

- [X] T107-A Spec/plan/tasks revision for per-skill authority-backed coupling (this section)
- [X] T107-B [RED] Coupling tests: mastery delta targeting only a different known skill, confusable mastery reference as the only delta, stale unchanged mastery file; positive controls: exact mastery delta, claimed skill newly created this turn
- [X] T107-C [GREEN] Per-skill coupling in `ValidateAcceptedTurnProseStateDeltasAsync` through `MortalSkillReferenceAuthority`, reusing `ReadMasteryIdentityReferences`; enriched `actual` naming the delta's real targets
- [X] T107-D GM-facing doc sync for the narrowed coupling semantics, tests/selection.json update, catalog validation
- [X] T107-E Bounded category runs, independent review, checkpoint and remote SHA verification

Dependencies: T107-A -> T107-B -> T107-C -> T107-D -> T107-E. Parent owns integration and catalog edits.

### Part 2 review corrections (PR #1583, external reviewer findings on 0cb4d736)

- [X] T107-F [RED] Six review-driven tests: passive progression delta satisfies claim; cosmetic-only passive entry change rejected; duplicate JSON keys in current mastery file fail closed; duplicate keys in pre-turn snapshot fail closed; case-colliding canonical names with exact-case prose claim + matching delta accepted; ambiguous lowercase claim across colliding names rejected without own delta. RED confirmed: exactly 4 failures matching the three findings, 2 characterization guards passing immediately
- [X] T107-G [GREEN] `ReadSkillEntriesByExactName` with `Missing/Unreadable/Ok` state (catches `ArgumentException` from duplicate-key lazy initialization → fail closed, no fallback); passive progression coupling via `passiveSkillChanges[]` entry-level `DeepEquals` on progression fields; ordinal-preserving canonical name sets; fail-closed claim disambiguation across ordinal case-colliding canonical names; expected/repair text names the passive path and warns against writing passive skills into `skillMasteryChanges`
- [X] T107-H GM-facing doc sync for the passive progression path and unreadable-snapshot semantics (`TaskGuides/CLI_Step_Main.txt`, `Launcher/CLI_Launch_Script.md`, `Launcher/Generate_CLI_Launch_Script.ps1`, `Examples/E_CLI_Step_Main.txt`); spec FR-101 and acceptance scenarios 6–8
- [X] T107-I Bounded verification (narrative class, doc-guard, selected categories), independent review resolution — including the reviewer's residual-defect fix (no-snapshot mastery fallback now requires a readable current file) with RED-first test `RejectsSkillClaimWhenCurrentMasteryUnreadableWithoutSnapshot`; verdict RESOLVED — commit+push, reviewer reply on PR #1583

### Part 2 review corrections, round 2 (PR #1583, external findings on 0ce848787)

- [X] T107-J [RED] Four review-driven tests: malformed pre-turn passive snapshot (`{`) must not fabricate new-skill credit; blank (whitespace-only) mastery snapshot must not unlock the weak fallback; ambiguous lowercase sentence alongside an exact-case mention still claims the whole case-colliding group; positive guard for both members mentioned in exact case with own deltas. RED confirmed: exactly 3 failures matching the findings, guard passing immediately
- [X] T107-K [GREEN] Snapshot blank-collapse fixed at the source with an opt-out (`treatBlankAsMissing`, default preserves all existing consumers); the coupling audit reads pre-turn payloads through `ReadCouplingPreTurnSnapshotAsync` so blank = Unreadable, fail closed; new-skill detection uses tri-state entry reads and never diffs against an empty set from an unparseable payload; claim detection resolves case-colliding ambiguity per sentence/mention; unreadable pre-turn passive snapshot gets its own repair diagnostic
- [X] T107-L GM-facing doc sync (exact-canonical-casing discipline sentence on all four surfaces); spec FR-101 + acceptance scenarios 9–10
- [X] T107-M Bounded verification, independent review resolution (verdict RESOLVED, no new defects), commit+push, reviewer reply on PR #1583

### Part 2 proactive adversarial round (self-hosted, before external re-review)

- [X] T107-N [ADVERSARIAL] Independent reviewer tasked to BREAK the change with executable probes (not to confirm fixes) found 3 confirmed defects: passive progression accepted field removal/rename (fail-open), `actual` misreported deleted mastery file/entry as "unchanged", whitespace-padded canonical name defeated all satisfaction paths (Trim on one side only); plus 2 unverified suspicions recorded as non-blocking notes
- [X] T107-O [RED] Five probe-driven tests: progression-field removal rejected, field rename rejected, deleted mastery file described honestly, removed mastery entry described honestly, padded canonical name accepted with clean delta. RED confirmed: exactly 5 failures
- [X] T107-P [GREEN] `HasPassiveProgressionDelta` counts only fields still present in the current entry; `DescribeActualForMissingClaim` reports deleted mastery file and removed mastery entry explicitly; `ParsePlayerSkillNames` trims names consistently. Class 31/31
- [X] T107-Q Bounded verification, spec/tasks/plan sync, resolution review (RESOLVED, no new defects; two non-blocking notes: passive-side deletion diagnostics gap, field-added-with-zero thin fail-open), commit+push, PR note

### Part 2 external review round 3 (PR #1583, `e374204e`)

- [X] T107-R [RED+GREEN] External reviewer (Codex) reported one P2 on `e374204e`: the D3 trim fix was applied only to `ParsePlayerSkillNames`, while `ParsePlayerSkillMasteryNames` kept untrimmed names, so a new active skill and its mastery reference padded with the same trailing whitespace produced a false `player_active_skill_missing_mastery_initialization` (the prose check passed). RED test `SkillContractConsistency_PaddedNewSkillAndMastery_DoNotProduceFalseMissingInitialization` failed exactly on the finding (1/1 fail), then GREEN after trimming mastery-side names too; NPC-side parsers verified symmetric (both untrimmed, only compared against each other — untouched). Editorial: three single-line XML `<param>` blocks on `ReadValidatedPendingTurnSnapshotFileAsync` reformatted to the file's multi-line style
- [X] T107-S Bounded verification (padded test 1/1; class 40/40; narrative + doc-guard 32/32; mortal-skill-reference-identity 44/44; console-explorer 277 planned accounted; documentation-examples 171/172 with pre-existing failures only), resolution review by the independent reviewer — verdict RESOLVED WITH NOTES (no new defects; one pre-existing P3 note: NPC-side asymmetric padding still yields a false missing-initialization error, follow-up candidate outside this PR), commit+push, reviewer reply on PR #1583

## Part 3 — T101 canonical skill profile schema consolidation (branch `1533-skill-schema-consolidation`)

- [X] T101-A Spec/plan/tasks revision for the shared profile validator (spec Part 3 slice: gap, recorded decisions incl. free-form `group`, FR-105..108, acceptance scenarios; plan Part 3; this section)
- [X] T101-B [RED] Composition gate tests (missing `combatEffect` materialized silently today; update-write with unknown rarity gated; carried-over untouched invalid entry NOT flagged — FR-107 scope), shared-code tests for unknown `rarity` on player active/passive + NPC delta paths, positive guards (free-form `group`; all canonical rarity values), byte-compatibility guard (legacy `passive_skill_missing_mastery_level` kept); no-double-reporting (scenario 5) pinned via single-issue count assertion on the composition surface; passive `type` parity is pre-existing (both actor paths already share `ValidatePassiveSkillObject`) and stays covered by existing tests. Fixture sweep: no out-of-ladder skill `rarity` in repo (lowercase spellings exist → enum check must be case-insensitive). RED confirmed 2026-11-02: exactly 6 failures matching the design, 7 guards passing immediately
- [X] T101-C [GREEN] `MortalSkillProfileContract` extraction with delegating delta validators, enum dedupe (`AllowedPassiveSkillTypes` single source), rarity enum check, composer constructor gate via bounded `effect_skill_composition_*` issue
- [X] T101-D GM-facing doc sync (compact skill template in `game_master_daemon.ps1`, `TaskGuides/CLI_Step_Main.txt`, launcher scripts, `Examples/E_CLI_Step_Main.txt`: canonical rarity ladder sentence; group explicitly free-form), spec/contracts sync, tests/selection.json update, catalog validation
- [X] T101-E Bounded category runs, independent review, checkpoint and remote SHA verification

- [X] T101-F PR #1586 review corrections: full shared profile algorithms; remove duplicate NPC profile checks while preserving closed shapes; all-actor changed-profile gate and exact field paths; direct RED/GREEN, category ownership, bounded integration and independent review.

Dependencies: T101-A -> T101-B -> T101-C -> T101-D -> T101-E. Parent owns integration and catalog edits.

## Part 4 — T102-A exact current-state duplicates (branch `1533-exact-skill-state-duplicates`)

Source: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).

- [X] T102-A Record FR-109..111 and bounded plan before implementation; preserve ordinal siblings and explicitly defer blanket confusable/ownership/mirror rules.
- [X] T102-B [RED] Minimal direct typed tests and real state-phase regressions for all seven collections, repeated same-owner groups and independent malformed-field diagnostics; add category ownership/selection.
- [X] T102-C [GREEN] Add exact duplicate diagnostics in existing collection validators, preserving names/owners/fields and avoiding new reads or mutation.
- [X] T102-D Compact GM rule, worked duplicate example and semantic documentation guard; existing repair flow, no automatic ambiguous deletion.
- [X] T102-E Frozen-source bounded verification, independent review, optimization assessment and remote checkpoint verification (coordinator owns publication).

Dependencies: T102-A -> T102-B -> T102-C -> T102-D -> T102-E. This does not complete all T102 or close #1533.

## Part 5 — T104-A current NPC mastery ownership

Source: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).

- [X] T104-A Record FR-112..114, current core/skillChanges authority evidence and bounded design in specs/1533-complete-skill-materialization/spec.md and plan.md; coordinator owns publication; the earlier local-only publication restriction is historical and is superseded by the 2026-11-12 publication checkpoint in plan.md.
- [X] T104-B [RED] Add direct typed ownership and real NpcStateFiles regressions in BookOfEternityClient.Tests/MortalNpcSkillMasteryOwnershipTests.cs and BookOfEternityClient.IntegrationTests/MortalNpcSkillMasteryOwnershipPhaseTests.cs, including current core-only/new-turn positives and owner/kind/snapshot negatives; register tests/categories.json and tests/selection.json.
- [X] T104-C [GREEN] Add operation-local current-root ownership validation in BookOfEternityClient/Services/Validation/ValidationService.NpcSkillMasteryOwnership.cs and reuse existing parsed roots from ValidationService.LifecycleControlAndStateFiles.cs / ValidationService.NpcWorldAndMeta.cs without changing initialization or duplicate validators.
- [X] T104-D Synchronize Rules/Block_7.txt, TaskGuides/CLI_Step_Main.txt and Examples/E_CLI_Step_Main.txt with a worked ownership example and executable PromptDocumentationCoverageTests guard; preserve existing repair flow.
- [X] T104-E Bounded implementation/review evidence remains 159/159 selected cases, 41/41 after name-only correction, valid 1668-category inventory and no remaining review findings. Source was published in `2cc3f4bab10cb97be73698821a31e255fc29b2db`; the 2026-11-12 checkpoint in plan.md records the exact tree and current limited API recovery verification. This does not complete broad T104.

Dependencies: T104-A -> T104-B -> T104-C -> T104-D -> T104-E. No parallel mutation; this bounded slice does not close issue #1533.

## Broader issue #1533 contours (bounded implemented slices are recorded below; whole contours remain unqualified)

- [ ] T102 Duplicate and confusable detection inside historical skill state files themselves (composition layer covers accepted-turn input only)
- [ ] T103 Stale skill reference detection across save/load boundaries and `removeActiveSkills` selector authority
- [ ] T104 Cross-owner reference detection (mastery pointing at another actor's skill)
- [ ] T105 Governed companions: characteristics, effects, combat actions materialization for companion entities
- [ ] T106 GM prompt/worked-example sweep for exact-`skillName` discipline beyond Block_7 rule 7.7, with documentation coverage guards
- [X] T107 Prose-claimed skill change ↔ state delta coupling audit (`HasSkillMasteryChanges` path) under the new authority — completed as Part 2 above (T107-A..T107-E); evidence in plan.md part-2 ledger

## Notes

- Codes introduced in part 1: `skill_mastery_confusable_reference`, `player_active_skill_mastery_initialization_confusable`, `npc_skill_mastery_initialization_confusable`; repair kind `mortal_skill_reference_identity_repair`.
- Existing codes `skill_mastery_unknown_active_skill`, `player_active_skill_missing_mastery_initialization`, `npc_skill_missing_mastery_initialization` are unchanged.

Correction completion evidence: implementation SHA 8ac29afd; 299 unique selected cases Passed across bounded selected runs, catalog discovery valid, independent Astra High finding resolved, 28 changed source blobs recovered from a clean GitHub clone. Exact commands, partial-run failures and recovery limits are recorded in plan.md. T107 completed in Part 2 and merged via PR #1583 (see ledger above); T101 Part 3 and its PR #1586 review corrections are verified: 186/186 selected cases, valid discovery-only catalog and independent READY review (2026-11-10 checkpoint in plan.md). Publication of the final checkpoint is coordinator-owned; T102-T106 remain unchecked.

## Part 6 — T105 display-only passive summary authority

Source: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).
- [X] T105-A Record the existing display-only contract and concrete reproduced fallback defect.
- [X] T105-B Add causal ComputeAsync regressions; remove only the legacy prose-derived characteristic fallback.
- [X] T105-C Correct contradictory GM fallback guidance and add executable worked example/guard.
- [X] T105-D Bounded verification, independent review and local checkpoint; source published in `2cc3f4b` (see the 2026-11-12 checkpoint in plan.md).

## Part 7 — T103 current canonical mastery closure
- [X] T103-A Record reproduced snapshot/save-load gaps and existing stage-boundary design.
- [X] T103-B Add causal accepted-refresh/save/load and closure regressions.
- [X] T103-C Add current-only closure at leased canonical validation and save/archive admission, preserving raw-delta behavior and existing repair diagnostics.
- [X] T103-D Bounded verification, independent review and local checkpoint; source published in `2cc3f4b` (see the 2026-11-12 checkpoint in plan.md).

## Part 8 — T106 generated skill template guidance
- [X] T106-A Record the bounded generated-template gap and exact identity/repair contract; preserve existing worked examples and launcher guidance.
- [X] T106-B Extend the existing full-daemon render test with real bootstrap, opt-in OS shell, manifest/guidance assertions and deterministic owned cleanup; obtain causal Linux PowerShell 7 RED.
- [X] T106-C Add the missing paragraph while preserving the daemon BOM; obtain isolated actual-render GREEN and register the narrow category/selection.
- [X] T106-D Coordinator verifies fresh selected-project builds, official bounded category/catalog results, independent review and local checkpoint. Source published in `2cc3f4b`; current limited API recovery is recorded in plan.md. Windows PowerShell 5.1 remains unrun.

## Part 9 — T103 QTE terminal closure
- [X] T103-Q1 Confirm original issue scope and real typed QTE stale-reference acceptance.
- [X] T103-Q2 Add terminal removal/replacement, waiver, exact retention/acquisition and restoration regressions.
- [X] T103-Q3 Reuse current-only closure at the existing final QTE gate without changing ordinary waiver or publication machinery.
- [X] T103-Q4 Focused verification/review and local checkpoint; objective original-criteria status; subsequently published in `2cc3f4b`, not a new test run.

## Part 10 — T103 quest reward exact reference closure
- [X] T103-R1 Permanent RED for case-only stale quest reward retargeting and exact controls.
- [X] T103-R2 Correct the existing resolver without changing item/relationship behavior or explicit historical-reason support.
- [X] T103-R3 GM example/guard synchronization, bounded verification, independent review and published checkpoint.

## Part 11 — Existing UI exact mastery closure
- [X] T103-U1 Reproduce NPC/console/browser exact identity and canonical mastery defects through typed/public existing projections.
- [X] T103-U2 Correct only existing maps, skill selector/dedup and active browser mastery display; preserve other consumers.
- [X] T103-U3 Bounded 19-case verification, discovery, independent review and remote source recovery. See Part11 ledger for test-oracle corrections and limits.

## Part 12 — Mortal training residual checks
- [ ] T103-T1 Public-route causal tests for exact training target and local profile completeness.
- [ ] T103-T2 Fix confirmed local training defects using existing identity/profile contracts, preserving other routes.
- [ ] T103-T3 Focused RED/GREEN, documentation alignment, independent review and durable checkpoint.
