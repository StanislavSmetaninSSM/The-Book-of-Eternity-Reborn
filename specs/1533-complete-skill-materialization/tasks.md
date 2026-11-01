# Tasks: Complete Mortal Skill and Mastery Materialization

**Input**: Design documents from `/specs/1533-complete-skill-materialization/`
**Prerequisites**: plan.md, research.md, data-model.md, contracts/skill-reference-identity.md

## Part 1 — Skill Reference Identity Authority (this branch, complete)

- [X] T001 Create Spec Kit feature docs (spec/plan/research/data-model/contracts/quickstart/checklists)
- [X] T002 [RED] Unit tests for `MortalSkillReferenceAuthority` classification (exact, case-variant, Cyrillic homoglyph, Greek homoglyph, unknown, ambiguous confusable key, null/whitespace handling)
- [X] T003 [RED] Integration test: confusable player mastery reference fails with `skill_mastery_confusable_reference` via `GameStateValidationPhase.PlayerStateFiles`; exact passes; unknown keeps `skill_mastery_unknown_active_skill`
- [X] T004 [RED] Integration tests: confusable-only mastery initialization coverage fails with `player_active_skill_mastery_initialization_confusable` and `npc_skill_mastery_initialization_confusable` (active + passive sections) via `GameStateValidationPhase.SkillContractConsistency`; exact and missing keep existing behavior
- [X] T005 [GREEN] Implement `MortalSkillReferenceAuthority` reusing `MortalLocationIdentityState.BuildConfusableKey`
- [X] T006 [GREEN] Additive identity layer `ValidationService.SkillReferenceIdentity.cs` dispatched from `ValidationPhases.cs` (legacy validators untouched as backstops)
- [X] T007 [GREEN] Player mastery reference check + player/NPC initialization checks wired through the authority
- [X] T008 Implement `mortal_skill_reference_identity_repair` packet routing and builder in `GameEngine.ValidationAndRepair`
- [X] T009 Integration test: repair request emits exactly one bounded identity packet with rename-only rules
- [X] T010 Run focused tests, then the full `BookOfEternityClient.Tests` + `BookOfEternityClient.IntegrationTests` suites for regressions

## PR #1579 review corrections (approved revision 1, issue #1533)

- [ ] T011 [RED] Capture review regressions in BookOfEternityClient.Tests/MortalSkillReferenceAuthorityTests.cs and BookOfEternityClient.IntegrationTests/GameEngineTurnLifecycleTests.SkillReferenceIdentity.cs.
- [ ] T012 [US1] Preserve ordinal canonical names and precise legacy-issue replacement in BookOfEternityClient/Services/Validation/ValidationService.SkillReferenceIdentity.cs and parser helpers.
- [ ] T013 [US3] Preserve indexed actor/skill corrections and unresolved ambiguity in BookOfEternityClient/Core/GameEngine/GameEngine.SkillReferenceIdentityRepair.cs.
- [ ] T014 [P] Update all affected partial-file source guards in BookOfEternityClient.Tests and BookOfEternityClient.IntegrationTests without weakening assertions.
- [ ] T015 Register test category owners and current impact selection in tests/categories.json and tests/selection.json; run discovery and category planning.
- [ ] T016 Synchronize specs/1533-complete-skill-materialization contracts/research/quickstart, GM worked example and XML API documentation.
- [ ] T017 Run selected categories and independent whole-block review; record actual results in specs/1533-complete-skill-materialization/plan.md.
- [ ] T018 Publish corrections, verify remote SHA and clean GitHub restore; reconcile evidence in specs/1533-complete-skill-materialization/tasks.md and plan.md.

Dependencies: T011 -> T012/T013; T014 is independently implementable; T012-T014 -> T015/T016 -> T017 -> T018. Parent owns integration and catalog edits. Later issue contours remain deferred.

## Later parts (issue #1533 contours, not in this branch)

- [ ] T101 Canonical skill schema consolidation: single active/passive profile validator shared by constructor and delta paths (player + NPC), including `activeEffectDefinitions`, `structuredBonuses`, rarity/type/group enums
- [ ] T102 Duplicate and confusable detection inside historical skill state files themselves (composition layer covers accepted-turn input only)
- [ ] T103 Stale skill reference detection across save/load boundaries and `removeActiveSkills` selector authority
- [ ] T104 Cross-owner reference detection (mastery pointing at another actor's skill)
- [ ] T105 Governed companions: characteristics, effects, combat actions materialization for companion entities
- [ ] T106 GM prompt/worked-example sweep for exact-`skillName` discipline beyond Block_7 rule 7.7, with documentation coverage guards
- [ ] T107 Prose-claimed skill change ↔ state delta coupling audit (`HasSkillMasteryChanges` path) under the new authority

## Notes

- Codes introduced in part 1: `skill_mastery_confusable_reference`, `player_active_skill_mastery_initialization_confusable`, `npc_skill_mastery_initialization_confusable`; repair kind `mortal_skill_reference_identity_repair`.
- Existing codes `skill_mastery_unknown_active_skill`, `player_active_skill_missing_mastery_initialization`, `npc_skill_missing_mastery_initialization` are unchanged.
