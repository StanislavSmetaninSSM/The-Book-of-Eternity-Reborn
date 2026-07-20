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
- [x] T022 Perform independent code review against issue #1500 and Spec Kit artifacts; repair findings and rerun verification.
- [ ] T023 Update task checkboxes only from verified evidence, commit, push, open PR, review CI-independent evidence, and merge when clean.

## Phase 7 - Independent review remediation

- [x] T024 Add exact positive afterlife trade-authority tests and fail closed on ambiguous current actor identity aliases.
- [x] T025 Require actor-owned memory for every new afterlife profile and require goals, quests, or activity for populated agency.
- [x] T026 Enforce bounded actor repair scope mechanically in the worker apply gate, including exact actor/section coordinates and protected-data comparison.
- [x] T027 Add executable Mortal and afterlife worked examples, broaden source guards, document exact authority, and replace the false-positive vacant-leadership test with a genuinely vacant current slot.

## Phase 8 - Adversarial review closure

- [x] T028 Make the Mortal, Chaos Sea, and Shining Abode worked examples executable under the runtime validator and deserialize their manifest coverage explicitly.
- [x] T029 Reject malformed or contradictory optional identity aliases and deletion of one bound afterlife profile from an otherwise valid profile set.
- [x] T030 Treat exact trade-role acquisition as a materialization promotion, verify the negative trade-authority matrix, and defer response-carrier trade evidence only to accepted-turn exact authority checks.
- [x] T031 Route afterlife actor repair through the realm-bound worker contract, mechanically preserve unrelated actor/scalar state, and enforce append-only Guardian/resident memory repair.
- [x] T032 Expand setting-neutral source guards and add behavioral Mortal/afterlife UI tests proving private materialization metadata cannot leak.

## Phase 9 - Concurrent repair and handoff hardening

- [x] T034 Pin validation-repair context/proposal content to exact byte hashes, proposal-bound content refs, and explicit add/replace/delete preconditions.
- [x] T035 Apply and roll back canonical files through one cross-process exact-byte compare/exchange protocol; reject stale apply and stale rollback ownership under deterministic race tests.
- [x] T036 Route Guardian memory repairs to the dedicated append-only journal, preserve unrelated actor state, and distinguish afterlife proposal-only tasks from afterlife validation-repair changed-file tasks.
- [x] T037 Make worker dispatch precede legacy fallback, clear stale repair artifacts, preserve accepted worker ownership across ready publication failure, and use an explicit player-output freshness boundary; synchronize bridge docs and worked examples.

## Phase 10 - Final review remediation

- [x] T038 Make the deterministic System Guardian envelope match exact current Guardian trade authority for preset and freeform fresh-game seeds.
- [x] T039 Serialize worker audit read-and-append under the shared canonical lock and treat audit publication failure as non-authoritative telemetry after an accepted apply.
- [x] T040 Reject canonical changes on `failed`, `timed-out`, or `rejected` proposals in both contract validation and the apply gate; synchronize the runtime prompt, guide, formal contract, worked example, and source guard.
- [x] T041 Retain validated pre-turn Mortal inventory and reject legacy-promotion `UpdateNPCs.inventory` unless it is semantically unchanged; preserve new-actor initial inventory and historical resend protection.
- [x] T042 Reject empty complete Mortal `characteristics` with `npc_characteristics_empty` while keeping characteristic names setting-defined.
- [x] T043 Permit only the exact missing Guardian thought journal Add for one routed memory-missing owner, reusing append-only preservation and the normal hash/content apply gate; cover wrong-owner and extra-root rejection.
- [x] T044 Synchronize authoritative Block 19, Block 19.A, CLI/daemon guidance, the existing Mortal worked example, and source guards with complete actor domains and the unchanged-inventory legacy-promotion rule.
- [x] T045 Make generated worker audit IDs unique under tight same-millisecond calls with a readable timestamp plus GUID suffix and deterministic regression coverage.

## Phase 11 - Final whole-branch re-review closure

- [x] T046 Reject a same-turn `initialId` that collides with validated pre-turn permanent Mortal identity, apply effective identity to inventory continuity, and synchronize the exact legacy-promotion repair hint.
- [x] T047 Route Guardian, resident, Radiant, Saref, and common-profile memory repairs to actor-owned authority and reject every unrelated canonical mutation in the apply gate.
- [x] T048 Make worker proposal `status` explicitly required, reject omitted/unspecified values before storage or apply, and preserve explicit terminal diagnostic proposals.
- [x] T049 Require exact actor-owned memory when an already-bound afterlife profile gains its first envelope while preserving untouched legacy profile compatibility.
- [x] T050 Centralize every generated worker audit event ID on one UTC-millisecond-plus-GUID generator with deterministic, concurrent, and production-source coverage.
- [x] T051 Synchronize Mortal/afterlife/worker GM contracts and Spec Kit artifacts, record per-finding RED/GREEN evidence, run broad verification, and publish the final re-review fix report.
  - Evidence: every confirmed re-review finding has a recorded RED and GREEN cycle; final focused suites passed 186 actor-materialization, 174 GmWorker, 15 Mortal prompt, 114 mandatory afterlife-documentation, and 374 documentation/source-guard tests. The full C# suite passed 6045/6052; its seven residual content-fixture failures are the same failures already recorded as reproducing on base `9a149014` in T021.

## Phase 12 - Final verification and integration

- [ ] T033 Rerun focused/documentation/full verification, perform fresh independent review, then commit, open the PR, and merge only after the evidence is clean.
