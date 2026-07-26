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

## Phase 23 - Tenth review recovery, generation, refresh, and process-tree remediation

- [x] T102 Fence every canonical writer on interrupted-load recovery: after acquiring the external canonical lease, recover any active load journal or fail closed before ordinary writes, clear, save, or worker apply can touch canonical state; cover retained rollback failure, rejected writers, and successful retry with deterministic tests.
  - Evidence: RED allowed ordinary writers to proceed while a failed load rollback journal and retained backup still described unresolved canonical ownership. GREEN centralizes post-lease recovery before canonical mutation; `EnsureDirectoryStructure_RecoversInterruptedLoadBeforeCreatingEmptySession` and `UnresolvedLoadRollback_FencesCanonicalWritersUntilRecoverySucceeds` cover startup, rejection, retained evidence, and successful retry without timing sleeps.
- [x] T103 Introduce an explicit durable session-generation nonce, rotate it under the canonical lease on load and New Game, bind worker task reservation, proposal publication, and proposal apply to that nonce, and strip or invalidate worker task/proposal roots during session transitions; cover paused workers across clear/load and archives containing byte-identical stale task evidence.
  - Evidence: RED allowed restored byte-identical task/proposal bytes and a worker paused across load or clear to regain authority. GREEN stores the generation under `.boe_runtime/session-generation/current.json`, captures it with task context, rotates it under the canonical lease, removes live handoff roots, excludes them from saves, and rejects stale reservation/publication/apply/ready paths. Deterministic load, clear/New Game, restored-proposal, and save/archive tests exercise every boundary.
- [x] T104 Make public runtime refresh a single lease-scoped read/modify/write operation so afterlife mirror repair cannot overwrite a concurrently accepted canonical update; reproduce the old read/apply/write interleaving with explicit barriers.
  - Evidence: RED used barriers to interleave refresh's old pre-lease read with a canonical update and reproduced stale mirror overwrite. GREEN holds one external canonical lease across read, normalization/mirror repair, and write; `RefreshGameStateAsync_ProfileMirrorReadModifyWrite_HoldsOneCanonicalLease` proves the accepted update survives the former interleaving.
- [x] T105 Own canceled worker processes through a process-tree abstraction that does not release the shared concurrency slot until the complete descendant tree has exited; on Windows use a Job Object or an equivalently queryable kernel boundary, retain a cross-platform fail-safe, and prove both task completion and a second task remain blocked while a descendant is alive.
  - Evidence: RED showed direct-process exit or cancellation could release capacity while a descendant remained alive. GREEN attaches the hidden host to a kill-on-close Windows Job Object before worker release, awaits confirmed complete-tree exit, fails closed on unsupported ownership, and quarantines uncertain cleanup. Lifecycle tests explicitly hold a descendant alive and prove both authoritative task completion and the next shared slot remain blocked.
- [x] T106 Replace the remaining delay-based apply/load linearization assertion with an explicit hook immediately before load lease acquisition and prove load cannot enter the apply decision interval.
  - Evidence: RED's delay-only assertion could pass without observing the intended contention point. GREEN exposes a test-only pre-load-lease hook and uses explicit barriers in `ApplyAsync_LoadWaitsForDecisionWithoutCreatingReplaceableSessionLock`; the test proves load cannot acquire the shared canonical lease during the apply decision interval.
- [ ] T107 Synchronize FR/SC decisions, Spec Kit artifacts, worker/save-load guidance, GM prompts/docs/examples/manifests, source guards, and RED/GREEN evidence; run focused, documentation-sensitive, broad, and full verification, then obtain a clean eleventh independent exact-diff review before closing T101/T093/T095/T091/T085/T080/T075/T023/T033.

## Phase 24 - Eleventh review generation, transaction, and process-host remediation

- [x] T108 Capture worker context hashes and the session-generation nonce under one canonical lease before task construction; reserve only a task already bound to that exact generation, create no canonical inbox path outside the lease, and suppress ready-signal publication when the bound generation is no longer current. Cover load/New Game between task construction, reservation, apply, and ready publication with deterministic barriers.
  - Evidence: RED reproduced stale rebinding at dispatch and stale ready publication after accepted apply. GREEN captures context plus generation under one lease, preserves exact task bytes during reservation, removes out-of-lease inbox creation, and reports `SessionReplaced`; the 3 focused generation-boundary tests pass.
- [x] T109 Make multi-file worker apply a recoverable external-journal transaction under `.boe_runtime`: persist intent and before-images before the first canonical write, recover or fail closed before any later canonical writer proceeds, continue rollback across individual restore failures, and prove process interruption cannot leave an accepted partial proposal.
  - Evidence: RED left the first canonical file applied after a simulated interrupted multi-file transaction. GREEN persists manifest, active journal, and before-images before mutation; recovers entries in reverse, continues recoverable restores, fails closed on unknown bytes, and preserves committed state. The 7 focused recovery/apply tests pass.
- [x] T110 Replace the remaining timing-only T106 assertion with the canonical-lock contention hook; serialize and document `sessionGeneration` in the formal worker packet and worked examples; remove or internalize unbound proposal-save and lease-free client-authority APIs so production callers cannot bypass generation or transaction binding.
  - Evidence: RED showed missing `sessionGeneration` was accepted, the deterministic load test used an unbound test generation, and both bypass APIs remained public. GREEN makes generation mandatory in builders, validation, examples, and source guards; uses explicit lease contention; removes unbound save/mirror writes. The combined T110 contract/API/docs/linearization filter passes 95/95.
- [x] T111 Replace worker-accessible ready/release/completion marker files with authenticated private host channels; require complete typed frames with a per-launch nonce and non-null exit code, publish completion immediately after the direct worker exits, and prove descendants cannot forge readiness or success and inherited output pipes cannot delay the authoritative completion event.
  - Evidence: RED proved legacy marker files were forgeable, inherited anonymous endpoints were not process-authenticated, and incomplete/defaulted frames could release or complete a worker. GREEN uses parent-created private current-user named control/status pipe servers, endpoint-and-nonce-only host arguments, exact hidden-host PID authentication before the typed `Launch` payload, host-owned channels never passed to the configured worker, strict typed frames, direct-process completion before output draining, and explicit `OutputDrained`. The latest focused host run passed 10/10 and the host/lifecycle/process-tree run passed 46/46; final Phase 25 verification remains T119.
- [x] T112 Retain Windows Job Object ownership as the supported complete descendant boundary; fail closed before worker release on platforms without an equivalent queryable kernel containment boundary, remove false Unix process-group guarantees, and preserve timeout/cancellation as the authoritative task result while quarantining cleanup uncertainty.
  - Evidence: RED showed the prior managed Unix process-group abstraction could not prove complete descendant ownership and cleanup errors could replace cancellation or timeout. GREEN supports Windows Job Objects as the complete-tree boundary, fails closed before release elsewhere, removes the false Unix contract, preserves authoritative timeout/cancellation, and quarantines unconfirmed cleanup. The combined host/lifecycle/process-tree suite passes 42/42.
- [x] T113 Bound unattached-host and complete-tree cleanup, replace lifecycle timing assertions with explicit synchronization, and prove timeout/cancellation never releases a slot while owned processes remain or changes into a cleanup exception.
  - Evidence: RED reproduced unbounded unattached-host cleanup and a timing-only pre-release assertion. GREEN adds a bounded termination deadline, an explicit pre-release test barrier after Job attachment, and deterministic timeout/cancellation cleanup tests. The combined host/lifecycle/process-tree suite passes 42/42.
- [ ] T114 Synchronize Spec Kit decisions, Mortal/afterlife worker guidance, GM prompts, worked examples, manifests, and source guards; record every Phase 24 RED/GREEN cycle, run focused/documentation/broad/full verification, and obtain a clean twelfth independent Sol/max exact-diff review before closing T107 and the inherited integration tasks.

## Phase 25 - Twelfth review durable authority and stale-session remediation

- [x] T115 Propagate typed `SessionReplaced` from reservation, bundle publication, apply, ready publication, and validation-repair delegation through every game-loop entrypoint; abort the old repair before legacy fallback, rollback, audit, ready, or trajectory writes can target a replacement session.
  - Evidence: RED reproduced a stale repair returning an ordinary failure and entering legacy repair/rollback after session rotation. GREEN throws a typed session-replacement signal to each game-loop boundary and refreshes the replacement runtime without old-session cleanup. The focused session-replacement run passed 4/4.
- [x] T116 Make the exact durable reserved task the sole production apply authority and deep-clone the exact reserved bytes before execution so caller mutation cannot widen scope, role, identity, or generation.
  - Evidence: RED accepted caller-side allowed-path mutation after reservation and allowed the apply caller to supply task authority. GREEN adds production `ApplyReservedAsync`, reloads `worker_tasks/<taskId>/task.json` under the canonical lease, and returns an independently deserialized reservation. The focused authority run passed 3/3.
- [x] T117 Route repair trajectory writes through a generation-bound atomic append and make committed worker-apply cleanup remove the transaction directory before its active journal so cleanup failure remains recoverable.
  - Evidence: RED allowed stale trajectory telemetry to cross a session replacement and deleted the active journal before fallible transaction-root cleanup. GREEN drops stale telemetry under the canonical lease and retains a committed journal when root cleanup fails. Focused source-guard and recovery runs passed 107/107 and 3/3 respectively.
- [x] T118 Exclude the latest validation-repair task from save archives and require session generation authority to use exact lowercase canonical GUID text in `N` format.
  - Evidence: RED archived the latest task control file and accepted uppercase `N` text. GREEN classifies the task as ephemeral and compares persisted text to canonical lowercase `Guid.ToString("N")`; the focused run passed 2/2.
- [ ] T119 Synchronize all Mortal/afterlife guides, prompts, examples, manifests, Spec Kit decisions, and source guards with private named-pipe transport, durable apply authority, typed replacement, trajectory, cleanup, save, and generation contracts; run documentation-sensitive, broad worker, and full verification.
- [ ] T120 Obtain a clean independent Sol/max exact-diff review, resolve every Critical/Important finding, update evidence with fresh command output, and close T119/T114/T107 plus inherited integration tasks only after inspection.

## Phase 26 - Twelfth review follow-up: exact retry fencing, host identity, fixtures, and documentation

- [x] T121 Close stale-session and namespace gaps found by review: abort every repeated validation-repair dispatch on typed `SessionReplaced`, reject reserved `proposalId=inbox`, and preserve canonical lowercase session-generation authority throughout retry and proposal paths.
  - Evidence: focused RED tests reached legacy repair after a replacement-session retry and accepted the reserved derived inbox name. GREEN checks every initial and repeated dispatch before fallback/rollback/audit/trajectory work and rejects the reserved proposal namespace.
- [x] T122 Prove named-pipe client PID authentication behaviorally with a real foreign process, then synchronize the exact host handshake: parent-created servers, endpoint-and-nonce-only host arguments, authenticated typed `Launch`, host-owned channels, typed `Ready`/`Release`/`Completed`/`OutputDrained`, and no worker payload in argv.
  - Evidence: the new foreign-client process test failed when both `ValidateConnectedHost` calls were mutation-removed and passes with exact hidden-host PID checks restored. The process-host suite passed 18/18 before documentation synchronization; all worker examples and source guards now describe the implemented sequence rather than pipe handles being passed to the configured worker.
- [x] T123 Repair Guardian validation test authority: snapshot only current runtime production roots, exclude world profiles, prefer explicit rollback backup bytes, deserialize camelCase manifests, and replace broad fallback assertions with exact expected issue codes.
  - Evidence: RED included excluded `world_profiles`, omitted a runtime-only probe, allowed stale copied snapshots to override explicit backups, and let unrelated Guardian authority errors satisfy tests. GREEN snapshots current `game_state` plus `lore`, excludes world profiles, honors explicit backup bytes, reads camelCase manifests, and asserts exact trade/resident-transfer diagnostics. The Guardian class passed 58/58 before the final helper rename.
- [x] T124 Require canonical `sessionGeneration` in every worked worker task packet and synchronize Mortal/afterlife guides, launcher prompts, formal bridge contract, manifest, Spec Kit artifacts, and source guards with the exact authenticated host protocol.
  - Evidence: the JSON-parsing source guard failed on the first of six incomplete worked examples, then passed after all task packets received lowercase GUID `N` values. The authenticated-handshake documentation test failed against the prior prose and the complete bridge documentation class now passes 19/19 after synchronization.
- [ ] T125 Rerun Guardian and process-host suites after final refactors, run mandatory afterlife documentation tests, broad worker/harness tests, the complete test project, `git diff --check`, and Spec Kit analysis; then obtain a fresh independent Sol/max exact-diff review and resolve every Critical/Important finding before closing T119/T120/T114/T107 and inherited integration tasks.

## Phase 27 - Twelfth review follow-up: runtime budgets and full-flow session fencing

- [x] T126 Move detached worker execution roots outside the replaceable game session, support an absolute `BOE_WORKER_RUNTIME_BASE_PATH`, reject traversal/reparse output, and bound proposal JSON, individual/aggregate imported content, and captured stdout/stderr.
  - Evidence: lifecycle tests reject escaped paths, reparse output, proposals above 1 MiB, individual content above 4 MiB, aggregate content above 16 MiB, and bound each output stream to 65,536 characters plus a truncation marker.
- [x] T127 Make `StateDistributor` one canonical-lease transaction, route StoryService and pending-snapshot/error-log fallbacks through canonical writers, and keep save/New Game traversal from following directory junctions.
  - Evidence: deterministic transaction tests prove rollback restores only this transaction's completed mutations, cleanup failure after commit does not revoke accepted state, story appends use atomic canonical writes, and save/clear do not traverse junction targets.
- [x] T128 Bind complete GM interactions to an immutable session operation and verify the captured generation after recovery inside every canonical write, during terminal polling, and before outer return; keep the lifecycle lease short and propagate typed `SessionReplaced` through all finalization paths.
  - Evidence: seven session-context tests plus deterministic terminal, ordinary-turn, raw life-evaluation, load, clear, and New Game races reject stale finalization without touching replacement files; the complete turn-lifecycle class passes 169/169.
- [x] T129 Synchronize the worker guide/formal contract and all Spec Kit artifacts with external runtime budgets and full-flow session fencing; record that this client-owned harness remediation changes no Mortal or afterlife GM-authored state/pending/control schema.
  - Evidence: documentation source guards first failed for the absent runtime/fence language and pass after synchronization. `OtherGuides/Afterlife_Contract_Matrix.md`, afterlife worked examples, and manifest require no additional schema entry because no afterlife action, response, receipt, report, scheduler, or authority surface changed.
- [ ] T130 Run mandatory afterlife documentation, broad harness/actor/full verification, `git diff --check`, Release build, Spec Kit analysis, and a fresh independent Sol/max exact-diff review; resolve every Critical/Important finding before closing T125/T119/T120/T114/T107 and inherited integration tasks.

## Phase 28 - Final Sol/max review remediation

- [x] T131 Bind `/incarnate` to one immutable session generation before its first canonical read or staging mutation; make staging snapshot capture/restore one generation-bound canonical transaction.
  - Evidence: deterministic incarnation/load barriers and the complete lifecycle/source-guard run pass 284/284.
- [x] T132 Make timeout, cancellation, host failure, non-zero exit, and unconfirmed worker termination diagnostic-only; never return or import an applyable proposal from those outcomes, and require terminal process success in the validation-repair delegator.
  - Evidence: valid-proposal-before-nonzero and valid-proposal-before-timeout regressions plus the complete bridge/delegator run pass 69/69.
- [x] T133 Rebind `GameLoop` session/turn and clear response, image, pending-memory, transition, warning, and Explorer transient state after save/load replacement; return to the menu when replacement state has no active game.
  - Evidence: replacement-with-active-game and replacement-without-active-game tests pass in the 284/284 lifecycle/source-guard run.
- [x] T134 Persist a durable `rolledBack` worker-apply journal phase before fallible cleanup and prove root deletion followed by active-journal deletion failure cannot poison the next canonical writer.
  - Evidence: the rollback cleanup fault-injection case passes in the 74/74 filesystem/save-load/session-lease run.
- [x] T135 Remove prose/genre keyword inference and client-created incomplete teachers from fresh Mortal bootstrap; require explicit `structuredGmAuthority` for GM-authored mechanics and add whole-source guards against helper-based inference.
  - Evidence: setting-neutral bootstrap/source guards pass 53/53; materialization and NPC command contracts pass 298/298 with independent explicit test actors.
- [x] T136 Reject configured and derived worker runtime roots equal to or nested under canonical `game_session`, including junction/reparse aliases.
  - Evidence: direct and junction-alias containment tests pass in the 69/69 bridge/delegator run.
- [x] T137 Capture complete/error terminal bytes together under one canonical lease and resolve exclusively from that immutable snapshot; reject a load inserted after capture without reading or deleting replacement signals.
  - Evidence: focused terminal snapshot tests pass 2/2 and the complete lifecycle/source-guard run passes 284/284.
- [x] T138 Decouple actor/NPC contract fixtures from production Mortal bootstrap so tests cannot silently depend on forbidden generated mechanics.
  - Evidence: explicit setting-neutral actor fixtures restore the complete actor/NPC contract run to 298/298.
- [ ] T139 Synchronize final Spec Kit decisions and review evidence, run mandatory afterlife documentation, broad harness/actor/full verification, `git diff --check`, Release build, Spec Kit analysis, and a fresh independent Sol/max exact-diff review before closing T130 and inherited integration tasks.

## Phase 29 - Final Sol/max follow-up authority remediation

- [x] T140 Record the four confirmed exact-diff review findings as FR-046 through FR-049, SC-016, and Decisions 62 through 65 before changing production behavior.
- [x] T141 Bind `prepare-live-turn` before its first read and execute cleanup, snapshot capture, and request publication as one generation-checked no-follow canonical transaction; prove load/New Game cannot receive stale preparation artifacts with deterministic barriers.
  - Evidence: RED let replacement rotate and clean the session before the paused old preparation republished `input/turn_request.json`; GREEN holds one bound canonical lease through no-follow cleanup, snapshot capture, authority/request publication, and the complete preparation suite passes 5/5.
- [x] T142 Add one atomic cancellation/publication transition for successful worker handoff; make staging and lease waiting cancellable and prove caller cancellation plus timeout cannot produce a later applyable bundle.
- [x] T143 Remove every public fail-open `GmWorkerApplyGate` construction path, require production `ValidationService`, and prove a scope-valid proposal that leaves invalid canonical state rolls back.
- [x] T144 Remove client-invented Mortal bootstrap progression, carrying, faction-resource, influence/control, and universal power-profile values while retaining only neutral first-turn scaffolding; update validators, prompts, and tests to require structured GM authority for those mechanics.
- [ ] T145 Synchronize Mortal and shared worker prompts, docs, examples, manifests, source guards, and final review evidence; run mandatory afterlife documentation, focused/broad/full verification, Release build, `git diff --check`, Spec Kit analysis, and a fresh independent Sol/max exact-diff review before closing T139/T130 and inherited integration tasks.

## Phase 30 - Four-review integration remediation

- [x] T146 Record the confirmed session/filesystem, worker, actor/bootstrap, and cross-boundary review findings as FR-050 through FR-060, SC-017 through SC-022, Decisions 66 through 71, and the corresponding data-model/plan changes before production edits.
- [ ] T147 Close complete-flow session fencing: bind late terminal and idle-trigger paths, make outer context verification-and-close atomic against escaped tasks, preserve sticky `SessionReplaced` through Story/finalization, and prove all races with deterministic barriers.
- [ ] T148 Introduce a purpose-bound replacement capability under the lifecycle lease; journal old/new generation and worker evidence before load mutation; recover them together; route every actual console load entrypoint through canonical runtime rebind and test stale transient cleanup.
- [ ] T149 Harden canonical physical identity and no-follow confinement for every read/write/delete/apply/recovery path; preserve rollback and snapshot bytes exactly; retain evidence on partial restore; atomically capture/publish incarnation and New Game snapshots; fail malformed StateDistributor input closed.
- [ ] T150 Make `ValidationService` the sole production source of apply filesystem identity, remove every mutable/unbound apply bypass, deep-snapshot tasks before asynchronous boundaries, and require exact durable reservation reload under the canonical lease.
- [ ] T151 Complete worker terminal arbitration: cancellation/timeout wins every pre-publication path, context staging observes the terminal token and budgets, stale ready publication returns typed replacement, and audit/cleanup failure never changes an authoritative outcome.
- [ ] T152 Bind browser direct actions and generated media to immutable session generation; make afterlife spend/snapshot/authority/request one transaction and commit externally staged image bytes only through an atomic generation-checked canonical write.
- [ ] T153 Close Mortal actor accepted-turn authority: reject duplicate effective identities, implement one bounded true-legacy-promotion path, and require exact request/receipt or dedicated command authority for trade stock and training showcase replacement.
- [x] T154 Remove remaining prose/genre merchant inference and setting-owned bootstrap location/faction/quest mechanics; require non-empty domain-bound `structuredGmAuthority`; narrow placeholder detection to identity/title fields; decouple all NPC tests from production bootstrap.
- [ ] T155 Reconcile afterlife actor lifecycle and projections: permit only proven `departure_only` binding removal, require positive mentor lessons, share hidden-profile visibility across console/browser, and document every accepted actor type.
- [ ] T156 Synchronize every active Mortal/afterlife rule, final audit, daemon entrypoint, worked example, manifest, launcher guide, contract matrix, and source guard; remove stale bootstrap-NPC guidance; add every intended source file to version control and verify a clean-checkout build.
- [ ] T157 Run focused RED/GREEN suites per task, mandatory afterlife documentation tests, broad harness/actor/browser tests, the complete test project, Release build, PowerShell/JSON parsing, `git diff --check`, Spec Kit analysis, and a fresh independent Sol/max exact-diff review; resolve every Critical/Important finding before closing T145/T139/T130 and inherited integration tasks.
