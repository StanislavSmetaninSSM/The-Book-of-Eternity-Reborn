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

## Later parts (issue #1533 contours, not in this branch)

- [ ] T101 Canonical skill schema consolidation: single active/passive profile validator shared by constructor and delta paths (player + NPC), including `activeEffectDefinitions`, `structuredBonuses`, rarity/type/group enums
- [ ] T102 Duplicate and confusable detection inside historical skill state files themselves (composition layer covers accepted-turn input only)
- [ ] T103 Stale skill reference detection across save/load boundaries and `removeActiveSkills` selector authority
- [ ] T104 Cross-owner reference detection (mastery pointing at another actor's skill)
- [ ] T105 Governed companions: characteristics, effects, combat actions materialization for companion entities
- [ ] T106 GM prompt/worked-example sweep for exact-`skillName` discipline beyond Block_7 rule 7.7, with documentation coverage guards
- [X] T107 Prose-claimed skill change ↔ state delta coupling audit (`HasSkillMasteryChanges` path) under the new authority — completed as Part 2 above (T107-A..T107-E); evidence in plan.md part-2 ledger

## Notes

- Codes introduced in part 1: `skill_mastery_confusable_reference`, `player_active_skill_mastery_initialization_confusable`, `npc_skill_mastery_initialization_confusable`; repair kind `mortal_skill_reference_identity_repair`.
- Existing codes `skill_mastery_unknown_active_skill`, `player_active_skill_missing_mastery_initialization`, `npc_skill_missing_mastery_initialization` are unchanged.

Correction completion evidence: implementation SHA 8ac29afd; 299 unique selected cases Passed across bounded selected runs, catalog discovery valid, independent Astra High finding resolved, 28 changed source blobs recovered from a clean GitHub clone. Exact commands, partial-run failures and recovery limits are recorded in plan.md. T101-T106 remain unchecked; T107 completed in Part 2 (see ledger above).
