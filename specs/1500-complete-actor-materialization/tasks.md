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
- [x] T070 Record per-finding RED/GREEN evidence, rerun focused/documentation/full verification, and obtain a fifth independent whole-branch review before T023/T033 integration.
  - Evidence: the fifth independent review inspected all 93 changed files, passed the 497-test materialization/docs matrix, 138 frontend tests, 2 built-frontend smokes, and independently reproduced the seven known base fixture failures in the 6198-test full suite. It confirmed every Phase 16 finding closed and reported three new Important findings, tracked below; integration remains blocked.

## Phase 17 - Fifth final re-review remediation

- [x] T071 Hash-pin `game_state/misc/characteristics.json` as read-only worker context for `npc_characteristics_empty`; reject invented keys and missing, malformed, duplicate, non-object, or empty setting authority during dispatch and apply.
  - Evidence: RED produced 9 expected failures across task construction, worker dispatch, invented-key apply, and five malformed-authority shapes. GREEN passed the exact 35-test characteristics/Mortal-continuity filter; the combined Phase 17 regression filter passed 44/44.
- [x] T072 Replace runtime-invalid present-empty `NPCCoreChanges` groups in every Spec Kit, Block 19, and daemon template; add a guard that extracts and validates the exact authoritative examples through the production command path.
  - Evidence: RED rejected both exact authoritative examples through production validation/reduction because they selected conflicting location aliases, invented a characteristic key, and supplied forbidden empty groups. GREEN passed both extracted-template cases; the combined Phase 17 regression filter passed 44/44.
- [x] T073 Derive player-facing output freshness from the latest actual repaired canonical-target write rather than repair start; fail the output-before-canonical interleaving and unobservable-boundary cases closed.
  - Evidence: RED timed out because output written after repair start but before the canonical target incorrectly avoided the second repair request. GREEN passed 7/7 focused lifecycle tests covering that interleaving, latest-of-many target writes, unobservable-target fail-closed behavior, worker-only explicit boundaries, and actor-memory exceptions; the combined Phase 17 regression filter passed 44/44.
- [x] T074 Synchronize Mortal/afterlife no-change rationale and all affected Spec Kit/GM documentation, record per-finding RED/GREEN evidence, and run focused/documentation/full verification.
  - Evidence: Spec Kit analysis covered 39 FRs, 8 SCs, and 75 unique tasks; its one afterlife-guidance inconsistency was corrected in the matrix and worked example. Phase 17 regressions passed 44/44, mandatory afterlife docs passed 115/115, and the broad materialization/worker/docs/source-guard matrix passed 837/837. Frontend verification passed 138/138 plus typecheck/build. The full C# run passed 6203/6211; the sole transient Agent Console HTTP 500 passed 1/1 in isolation, while all remaining seven failures reproduced 7/7 with identical assertions on exact base `9a1490146b7cecad6101af1f166bde614050a6a3`. PowerShell syntax and `git diff --check` passed.
- [ ] T075 Obtain a sixth independent whole-branch review with exact-base comparison; resolve every confirmed finding before T023/T033 integration.

## Phase 18 - Sixth review worker/lifecycle remediation

- [x] T076 Hash-pin `game_state/meta/soul_state.json` as read-only realm authority for every afterlife/mixed repair worker task; reject missing, malformed, duplicate, ambiguous, unsupported, changed, or realm-mismatched authority at task build and apply.
  - Evidence: RED produced 11 expected task-build/validation/apply failures across absent, malformed, non-object, duplicate, unsupported, mismatched, and after-dispatch changed authority. GREEN passed the 15-test focused authority matrix; the combined worker/delegator/apply regression suite passed 158/158.
- [x] T077 Reject omitted, zero, and undefined `WorkerFileChangeKind` values at proposal contract validation before any apply semantics.
  - Evidence: RED produced four expected contract/apply failures for omitted, raw zero, and undefined numeric values. GREEN passed 4/4 focused operation-authority tests.
- [x] T078 Require finite setting-authorized numeric values in Mortal characteristics worker repair and cover exponent-overflow JSON tokens.
  - Evidence: RED accepted the `1e9999` matrix case; GREEN rejected it and passed the complete 28/28 characteristic-preservation matrix.
- [x] T079 Retain the original repaired canonical target set/boundary through derived output-only retries, require strict output-newer-than-target ordering, and reject a second canonical rewrite after output refresh.
  - Evidence: RED accepted an equal boundary and timed out waiting for a renewed stale-output request after a second canonical rewrite. GREEN passed 4/4 focused lifecycle/boundary tests, including strict equality rejection and retained-target re-staling.
- [ ] T080 Synchronize Spec Kit and shared Mortal/afterlife repair guidance, record RED/GREEN evidence, rerun focused/full verification, and complete segmented sixth independent review before T075/T023/T033 integration.

## Phase 19 - Seventh review worker isolation and authority remediation

- [x] T081 Run every worker in a detached execution snapshot that exposes only pinned task context and imports only a contract-valid proposal plus its declared content refs; prove direct worker writes cannot mutate the live canonical session.
  - Evidence: RED showed a worker launched against the live session could mutate canonical weather outside its proposal. GREEN isolates task context under `.worker_runtime`, imports only the validated proposal and declared `contentRef` bytes, cleans the detached workspace, and passes all 10 bridge lifecycle tests including direct-write isolation and declared-artifact import.
- [x] T082 Hold the canonical write lock from exact-byte context/authority verification through the complete multi-target proposal commit, so a cooperating writer cannot change realm or setting authority between validation and apply.
  - Evidence: RED let a cooperating realm writer complete inside validation and accepted a direct realm mutation after the initial authority read. GREEN uses one canonical write lease through context verification, preservation, all compare-exchange writes, validation, read-only context revalidation, rollback, and decision linearization; explicit Soul and characteristics authority races pass.
- [x] T083 Classify every `game_state/meta/` validation-repair target as afterlife-scoped, including non-actor and mixed issue batches, and fail task construction closed without one exact pinned Soul realm authority contract.
  - Evidence: RED produced four failures for a non-actor Shining repair, absent/malformed Soul authority, and a forged standalone task. GREEN binds every meta repair to Soul authority and fails mixed Mortal/Shining task construction closed; five focused classification cases pass.
- [x] T084 Keep `game_state/misc/characteristics.json` read-only in every mixed Mortal characteristics repair task and enforce the invariant in both task construction and standalone packet validation.
  - Evidence: RED allowed the setting authority file to become writable in a mixed characteristics repair. GREEN rejects it in both builder and standalone packet validation and revalidates its exact bytes before apply acceptance.
- [ ] T085 Synchronize Spec Kit, worker/GM guidance, source guards, and worked examples; record RED/GREEN evidence and repeat segmented independent review before T080/T075/T023/T033 integration.

## Phase 20 - Eighth review import, scope, lease, and cleanup remediation

- [x] T086 Verify every declared `contentRef` digest before importing any worker artifact, reject proposal-ID collisions without overwriting prior review state, and preserve timeout truth when a malformed proposal exists.
  - Evidence: RED accepted mismatched detached bytes, replaced an existing proposal, overwrote repeated dispatch IDs, and reported a malformed proposal timeout as an ordinary failure. GREEN verifies `afterSha256` before publication, preserves immutable task/proposal artifacts, and reports timeout plus the rejected-handoff diagnostic; focused lifecycle verification passes 6/6.
- [x] T087 Reject standalone and builder-created mixed Mortal/afterlife repair batches before path filtering; require exact non-wildcard afterlife surfaces for every `game_state/meta/` target.
  - Evidence: RED let a narrow profile silently drop the Mortal target and accepted a forged mixed packet covered by `game_state/**`. GREEN classifies the original issue set before filtering, rejects mixed standalone packets, and requires exact wildcard-free afterlife surfaces; validation-repair/delegator verification passes 42/42.
- [x] T088 Route built-in backup, restore, and clear operations through the canonical write lease so no cooperating canonical writer can cross an apply decision interval.
  - Evidence: RED showed backup, restore, game-state clear, and current-world lore clear completing while a canonical lease was held. GREEN adds leased async operations with compatible sync wrappers and migrates `StateDistributor` rollback; `FileSystemManagerTests` passes 14/14.
- [x] T089 Treat Windows path aliases case-insensitively for setting/Soul read-only authority, context identity, and proposal-scope exclusion.
  - Evidence: RED accepted case aliases in context/allowed paths and both read-only authorities, while changed-file aliases escaped duplicate detection. GREEN centralizes case-insensitive canonical path identity across builder, validator, dispatch, bridge, and apply gate; focused alias tests pass 5/5 and the broader contract/delegator/apply suite passes 180/180.
- [x] T090 Delete detached workspaces without traversing worker-created reparse points and prevent cleanup failure from replacing a completed/timeout worker result.
  - Evidence: RED cleanup traversed a worker junction and cleared an external file attribute; a deliberately locked runtime file threw from `finally` and replaced a completed result. GREEN performs top-level/post-order deletion without following reparse points and records cleanup failure diagnostically; both regressions pass 2/2.
- [ ] T091 Synchronize Spec Kit and worker/afterlife guidance where review semantics changed, record RED/GREEN evidence, rerun focused/full verification, and obtain a clean independent re-review before T085/T080/T075/T023/T033 integration.

## Phase 21 - Ninth review dispatch identity, concurrency, and realm remediation

- [x] T092 Give every validation-repair dispatch a globally unique task ID while preserving its human-readable attempt number; prove repeated validation cycles in one session cannot collide with immutable task evidence.
  - Evidence: RED reproduced the second-cycle immutable task collision with the same repair attempt number. GREEN appends a unique dispatch suffix while retaining the zero-padded attempt prefix; the complete delegator suite passes 23/23.
- [ ] T093 Reserve immutable task IDs by create-only compare/exchange and publish immutable complete proposal bundles by one create-only atomic directory rename across concurrent bridge-pool instances; enforce each worker profile's `MaxConcurrentTasks` limit at runtime, and cover task reservation plus proposal publication races with deterministic concurrency tests.
  - Ninth-review status: reopened because the first task race was serialized by a one-slot gate, proposal publication was a claim followed by non-atomic writes, and timing-based assertions did not prove the stated guarantees.
- [x] T094 Classify all actual Mortal authorities, including `lore/current_world/**` and `game_state/core/player_status.json`, before profile filtering; require every exact afterlife validation-repair surface to belong to canonical afterlife state while preserving typed task-provided control/report surfaces; reject main-GM-only actor identity collisions in standalone worker packets.
  - Evidence: RED accepted five forged builder/standalone cases covering current-world lore, core player status, foreign afterlife surfaces, and delegated identity collision. GREEN rejects file and nested-field variants before filtering; the complete validation-repair suite passes 31/31.
- [ ] T095 Synchronize Spec Kit and managed agent context, Mortal/afterlife worker guidance, worked examples, and source guards; record RED/GREEN evidence, investigate the save/load lease boundary, rerun focused/full verification, and obtain a clean ninth independent review before T091/T085/T080/T075/T023/T033 integration.

## Phase 22 - Ninth review transactional lifecycle remediation

- [x] T096 Make load a recoverable external-journal transaction: never delete the last valid backup after rollback failure, recover interrupted swaps before directory initialization, refresh restored in-memory state, and allow client-owned mirror repair to reuse the active canonical lease.
  - Evidence: RED exposed startup initialization before interrupted-swap recovery, mirror-repair self-deadlock, runtime/disk divergence after commit-journal failure, and deletion of the only valid backup after rollback-move failure. GREEN adds `.boe_runtime/load-transactions`, lease-aware refresh/mirror repair, retained recovery evidence, and startup retry; the combined actor/worker/afterlife/save-load matrix passes 884/884.
- [x] T097 Remove the replaceable in-session apply lock, acquire the external canonical lease first and only once, and prove load cannot cross any worker-apply decision boundary.
  - Evidence: the deterministic apply/load test holds validation inside the external lease, observes no `game_state/control/gm_worker_apply.lock`, proves load remains blocked until the apply decision, then completes successfully. The combined matrix passes 884/884.
- [x] T098 Publish each complete proposal bundle through one create-only atomic directory rename under the canonical lease, verify the originating task bytes still belong to the current session generation, and keep the durable bundle authoritative if derived inbox/audit publication fails.
  - Evidence: RED admitted a stale worker after load and used a separate claim followed by partial writes. GREEN stages `proposal.json` plus every declared content file, checks exact task bytes under the lease, publishes by one create-only directory rename, rejects concurrent duplicate IDs, and retains the durable bundle after injected inbox failure. The combined matrix passes 884/884.
- [x] T099 Kill and await canceled worker process trees before releasing their slot; replace permanent static semaphores with reference-counted gates that retire when idle, allow an idle profile limit change, and fail an active limit change as a worker result.
  - Evidence: the cancellation regression launches a child process and proves parent plus child have exited before the task returns and a later task can publish. Separate tests cover idle-limit replacement and active-limit failure; lifecycle/store tests pass 26/26 and the combined matrix passes 884/884.
- [x] T100 Replace timing-dependent concurrency tests with explicit pre-reservation/publication barriers; race same task IDs with at least two slots, enforce one-slot limits across separate pool instances, and prove cancellation leaves no live child before another task publishes.
  - Evidence: test-only hooks and explicit async barriers now align same-task reservation, same-proposal publication, and cross-pool slot contention without sleep-based correctness assertions. Lifecycle/store tests pass 26/26 and the combined matrix passes 884/884.
- [ ] T101 Synchronize FR/SC decisions, worker/save-load guidance, source guards, and RED/GREEN evidence; rerun focused/documentation/full verification and obtain a clean tenth independent review before closing T093/T095/T091/T085/T080/T075/T023/T033.
