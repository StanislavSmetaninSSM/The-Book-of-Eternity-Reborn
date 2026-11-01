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
