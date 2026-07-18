# Tasks: Complete Actor Materialization

**Source issue**: [#1500](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1500)

## Phase 1 - Contract foundation

- [x] T001 Add focused failing tests for envelope parsing, exact actor binding, allowed fields, dispositions, and capability shape.
- [x] T002 Implement `ActorMaterializationContract` constants, parser, and reusable structural validation without genre/prose inference.
- [x] T003 Add source-guard tests that forbid genre keyword dictionaries and player-facing rendering of materialization metadata.

## Phase 2 - Mortal NPC materialization

- [x] T004 Add failing tests for new Mortal NPCs: missing envelope, valid populated sections, deliberate emptiness, section/content conflicts, and capability contradictions.
- [x] T005 Integrate Mortal materialization validation with full first-object validation and stable same-turn/permanent identity binding.
- [x] T006 Add failing tests and enforcement preventing first-materialization envelopes from bypassing dedicated existing-NPC delta commands.
- [x] T007 Add legacy/new/promotion tests using validated pre-turn authority; preserve untouched legacy NPCs.

## Phase 3 - Afterlife materialization and cross-file binding

- [x] T008 Add failing tests for new common profiles, section completeness, capabilities, actor memory, and untouched legacy profiles.
- [x] T009 Integrate afterlife profile envelope validation and normalizer preservation without content invention.
- [x] T010 Add failing cross-file tests for Guardians, abode residents, radiant/Saref/custom actors, and non-vacant Shining faction heads.
- [x] T011 Implement exact type-and-ID profile binding and current-turn promotion detection, including player/vacancy/client-owned exceptions.
- [x] T012 Add deterministic System Guardian envelope tests and update the fresh-game builder.

## Phase 4 - Harness repair behavior

- [x] T013 Add failing tests for materialization issue classification and bounded repair packets.
- [x] T014 Implement a dedicated repair packet that preserves valid actor sections and names only missing/contradictory targets.
- [x] T015 Verify repair packets do not request implementation-code inspection, broad actor rewrites, deletion, or client invention.

## Phase 5 - GM contract synchronization

- [x] T016 Update Mortal GM prompt/docs with the first-materialization contract and a complete setting-neutral worked NPC example.
- [x] T017 Update afterlife prompt entrypoints, `Afterlife_Contract_Matrix.md`, and worked Chaos Sea/Shining examples.
- [x] T018 Update `example_validation_manifest.json` and documentation/source-guard tests for both realms.
- [x] T019 Record explicit no-frontend-change rationale and test that console/browser player projections do not expose private metadata.

## Phase 6 - Verification and integration

- [x] T020 Run focused actor/materialization, NPC, afterlife, Shining, System Guardian, docs, and source-guard tests.
  - Evidence: 1643 focused C# tests, 262 command-display fixture tests, 255 documentation/source-guard tests, 138 frontend player-facing tests, and 2 built-frontend smoke tests passed.
- [x] T021 Run the complete C# test project and `git diff --check`.
  - Evidence: `git diff --check` passed. The complete project ran 5878 tests: 5870 passed; seven failures reproduce identically on base `9a149014`, and the remaining Agent Console HTTP 500 passed immediately when rerun in isolation.
- [ ] T022 Perform independent code review against issue #1500 and Spec Kit artifacts; repair findings and rerun verification.
- [ ] T023 Update task checkboxes only from verified evidence, commit, push, open PR, review CI-independent evidence, and merge when clean.
