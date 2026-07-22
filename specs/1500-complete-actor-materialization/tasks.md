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
  - Scope note: this evidence closes the first final re-review wave only; it does not claim the second final re-review invariants below.

## Phase 12 - Final verification and integration

- [ ] T033 Rerun focused/documentation/full verification, perform fresh independent review, then commit, open the PR, and merge only after the evidence is clean.

## Phase 13 - Second final re-review remediation

- [x] T052 Reject duplicate-key current and validated pre-turn actor/inventory/materialization authority through structured production-path validation while retaining order-insensitive semantic equality for valid JSON.
- [x] T053 Add exact Mortal actor/section/snapshot metadata for collision, inventory-resend, and empty-characteristics issues.
- [x] T054 Fail collision and non-snapshot inventory worker dispatch/apply closed; constrain exact-snapshot inventory and numeric-characteristics proposals to one actor, carrier, and field with a public negative apply matrix.
- [x] T055 Replace the generated universal characteristic list with a setting-defined placeholder and guard source plus rendered output against regression.
- [x] T056 Distinguish genuinely new, ordinary existing, and true legacy-promotion inventory repairs in the high-priority packet; synchronize the worked GM example, prompt/source guards, and Spec Kit decisions.
- [x] T057 Run focused, broad, mandatory afterlife, syntax, diff, and full-project verification; self-review the complete wave and publish `final-rereview-2-fixes-report.md`.
  - Evidence: 235 actor validation/contract, 206 GmWorker, 156 turn-lifecycle, 261 prompt/example/source-guard, and 114 mandatory afterlife tests passed. PowerShell parse and `git diff --check` passed. The final full C# run passed 6084/6091; its seven failures exactly match the base-reproduced content-fixture set documented in T021, and the intermittent Agent Console smoke passed both isolated and in the final full run. See `.git/worktrees/boe-1500-complete-actor-materialization/sdd/final-rereview-2-fixes-report.md`.

## Phase 14 - Controller follow-up after first implementation report

- [x] T058 Remove the residual ordinary-existing partial-object repair branch from validator metadata, the high-priority packet, and the worked GM example; require whole-entry removal, dedicated deltas, and main-GM fallback, then append fresh RED/GREEN and affected-suite evidence to the report. Evidence: focused 3/3, validator 191/191, lifecycle 156/156, and documentation/source guards 153/153 passed; see `sdd/final-rereview-2-fixes-report.md`.

## Phase 15 - Third final re-review remediation

- [x] T059 Apply exact validated pre-turn inventory continuity to both `UpdateNPCs` and `NPCsInScene`, preserving genuinely new initial inventory and exact-snapshot legacy promotion while rejecting changed, added, or removed inventory for historical actors.
- [x] T060 Add a bounded, setting-agnostic `NPCCoreChanges` command for supported ordinary-existing core mutations; validate exact permanent identity and a closed mutation schema, reduce it into every unambiguous canonical carrier, preserve protected actor state, and consume the command before canonical validation.
- [x] T061 Replace every contradictory ordinary-existing full-`UpdateNPCs` mandate with a real dedicated command or `NPCCoreChanges`; update Block 2, CLI/daemon guidance, worked examples, manifests, and full-source negative guards.
- [x] T062 Remove universal Strength/Constitution/carrying and class-stat assumptions from authoritative Mortal rules and examples; derive all characteristic keys and formulas from explicit current-world authority and guard the complete Block 19 source against regression.
- [x] T063 Require three to five first-materialization personality traits with mandatory integer `value`, repair the Mortal worked example, and validate that example through the production NPC validator with truthful manifest coverage.
- [x] T064 Record per-finding RED/GREEN evidence, run focused and full verification, update Spec Kit artifacts, and repeat independent whole-branch review before T023/T033 integration.
  - Evidence: third-wave runtime 768/768 and prompt/docs/source/afterlife 267/267 passed; full suite passed 6166/6174 with seven base-reproduced fixture failures and one isolated-green Agent Console reset. Independent re-review 4 is recorded in `sdd/final-rereview-4-report.md`; its four Important findings are the Phase 16 remediation scope below.

## Phase 16 - Fourth final re-review remediation

- [x] T065 Fail malformed, non-object, and duplicate-member current `npc_core.json` authority closed through stable structured pre-normalization issues without throwing or silently skipping `NPCCoreChanges` validation.
- [x] T066 Enforce validated pre-turn continuity for every actor-owned field in historical `NPCsInScene` and envelope-free `UpdateNPCs`, while preserving genuinely new actors and requiring existing actors to use the exact dedicated command surface.
- [x] T067 Validate `NPCCoreChanges.fateCardsToAdd` against the complete production Fate Card, skill, combat-action, and combat-effect contract before reduction; retain the command and canonical actor unchanged on any nested error.
- [x] T068 Derive Mortal combat capability and promotion evidence from production-valid active/passive skill structure rather than optional synthetic skill IDs, including complete ID-less Block 7 skills.
- [x] T069 Synchronize the formal contract, Spec Kit decisions, source/documentation guards, and explicit Mortal/afterlife prompt-update rationale; remove contract EOF whitespace.
  - Evidence: per-finding RED failures were observed for three malformed/current authority cases, ten protected actor domains, four nested combat-effect variants plus reducer atomicity, two ID-less combat paths, and duplicate pre-turn authority. The combined actor/NPCCore/docs/source-guard matrix passed 497/497; the broader skill/Fate Card/Combat Action matrix passed 61/61.
- [ ] T070 Record per-finding RED/GREEN evidence, rerun focused/documentation/full verification, and obtain a fifth independent whole-branch review before T023/T033 integration.
