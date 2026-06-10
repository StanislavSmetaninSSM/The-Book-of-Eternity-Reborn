# Tasks: Daren QTE Training Showcase

**Input**: `specs/919-daren-qte-training/spec.md`, `specs/919-daren-qte-training/plan.md`, `specs/919-daren-qte-training/contracts/daren-qte-training-contract.md`, source issue [#919](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919)

**Prerequisites**: QTE v2 core #911 children, Browser QTE parity #918, scoring/ranks #924, and Practice Mode #925 are already landed on `main`.

**Tests**: Production behavior changes require test-first implementation. Each test task must be run and observed failing for the intended reason before the matching implementation task.

## Phase 1: Setup and Baseline

- [ ] T001 Confirm source GitHub issue #919, current branch `work/919-daren-qte-training`, `git status --short`, and active feature path `specs/919-daren-qte-training/`.
- [ ] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, this spec/plan/tasks, `contracts/daren-qte-training-contract.md`, and relevant QTE references before production edits.
- [ ] T003 Record baseline verification evidence: focused C# QTE/browser/docs tests and frontend verify from the worktree.
- [ ] T004 Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/919-daren-qte-training`.

## Phase 2: RED Tests and Source Guards

- [ ] T005 [P] Add a Daren route coverage test in `BookOfEternityClient.Tests` proving the route definition includes approach, gadget infiltration, stealth crossing, lock pick, rune memory, physical pressure, timed rhythm, route decision, staff theft, pursuit, chase chain, and hideout return beats.
- [ ] T006 [P] Add a QTE type coverage test proving Daren route includes `TimingBar`, `PromptChain`, `BalanceMeter`, `ChargeRelease`, `BranchChoice`, `MashInput`, `PatternMemory`, `RhythmPulse`, `PrecisionChoice`, `StealthNoise`, and `LockPinSet`.
- [ ] T007 [P] Add ending-threshold tests proving `no_reward_failure`, `Тень в бегах` (+1), `Сорванный след` (+2), `Чистая кража` (+4), and `Идеальная тень` (+6) resolve deterministically at boundary scores.
- [ ] T008 [P] Add persistent Daren profile tests proving first completion writes best tier, better replay upgrades, same/worse replay does not downgrade/stack, duplicate records normalize to best tier, and unknown/negative tiers cannot grant rewards.
- [ ] T009 [P] Add New Game reward tests proving a valid profile adds the best-tier Ink Feather bonus exactly once to a newly initialized session and never from save load, repair, reincarnation, in-session life starts, afterlife transitions, ordinary turns, or QTE Practice Mode.
- [ ] T010 [P] Add no-mutation tests proving opening/exiting Daren showcase before a valid ending does not create or mutate ordinary campaign `game_state`, pending action files, chat log turns, inventory, quests, XP, afterlife state, or practice state.
- [ ] T011 [P] Add console player-facing tests for Daren entry, route progress, completion summary, and New Game reward copy with no debug/API/DTO/file-path/manual-grade terms.
- [ ] T012 [P] Add browser API/frontend tests for Daren entry/progress/result surfaces that reuse existing QTE mini-game behavior and preserve C# authority for route state/reward writes.
- [ ] T013 [P] Add docs/source guard tests for `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and Daren reward boundaries.
- [ ] T014 Run each new focused test/source guard and record RED failures caused by missing Daren showcase/profile/New Game behavior, not typos or fixture errors.

## Phase 3: Daren Route and QTE Resolution

- [ ] T015 Implement focused Daren route definition types/service with original project story beats, local choices, route metrics, and required QTE actions.
- [ ] T016 Reuse existing QTE validation/resolution/scoring helpers for Daren actions; do not duplicate gameplay grade authority in browser/React.
- [ ] T017 Implement deterministic route progression and completion summary generation for success, partial, fail, timeout/cancel, stealth/evidence, loot condition, pursuit result, and hideout safety metrics.
- [ ] T018 Run route coverage and QTE type coverage tests; keep them green before profile/reward implementation.

## Phase 4: Endings, Persistent Profile, and New Game Reward

- [ ] T019 Implement ending threshold resolver with exact tiers and bonuses: `shadow_on_the_run`/`Тень в бегах` +1, `broken_trail`/`Сорванный след` +2, `clean_heist`/`Чистая кража` +4, `perfect_shadow`/`Идеальная тень` +6.
- [ ] T020 Implement permanent Daren reward profile persistence outside ordinary `game_state`, with schema/version, best tier, score evidence, timestamp, derived bonus, and upgrade-only semantics.
- [ ] T021 Implement profile validation/normalization for duplicate, corrupt, unknown-tier, negative-bonus, impossible-score, and downgrade cases.
- [ ] T022 Integrate New Game reward grant during new session initialization with per-session idempotency marker and player-facing copy naming the Daren tier and Ink Feather amount.
- [ ] T023 Prove no forbidden lifecycle grants: save load, repair/normalizer for existing session, reincarnation, life start, afterlife transitions, ordinary turns, and QTE Practice Mode.
- [ ] T024 Run ending/profile/New Game/no-mutation focused tests; keep them green before UI/docs work.

## Phase 5: Console and Browser Player Surfaces

- [ ] T025 Add or wire console entry point for Daren showcase without requiring normal campaign start; preserve existing console gameplay/menu behavior.
- [ ] T026 Add console route/progress/result rendering with Russian player-facing copy and escaped dynamic text.
- [ ] T027 Add or wire browser entry point/API DTOs for Daren showcase route state, action resolution, result summary, and New Game reward visibility.
- [ ] T028 Add or wire React player-facing surfaces for Daren route/progress/result using existing QTE mini-game components and frame shortcut bubbling guards.
- [ ] T029 Ensure Browser/React sends only supported action/grade inputs to C# authority and never posts arbitrary reward/profile mutations.
- [ ] T030 Run console/browser focused tests and `npm run verify --prefix BookOfEternityClient.WebFrontend`.

## Phase 6: Documentation, Examples, and Source Guards

- [ ] T031 Update `CLI_API_Specification.md` with Daren showcase/profile reward/New Game grant contract.
- [ ] T032 Update `Rules/Block_CLI_QTE.txt` to distinguish GM-authored campaign QTE offers from client-owned Daren showcase content.
- [ ] T033 Update `Examples/E_CLI_QTE_Offer.txt` or a companion validated example to explain Daren showcase boundaries without changing ordinary GM-authored QTE offer semantics.
- [ ] T034 Update `Examples/example_validation_manifest.json` only if a new or changed example needs manifest coverage.
- [ ] T035 Update docs/source guard tests so future changes cannot grant Daren rewards from practice, save load, reincarnation, or ordinary campaign turns.
- [ ] T036 Run documentation/source guard tests and reconcile any required example validation changes.

## Phase 7: Verification, Review, PR, and Closure

- [ ] T037 Run focused C# verification: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "QteSceneServiceTests|Daren|NewGame|ValidationServiceQteTests|PromptDocumentationCoverageTests|ExampleDocumentationValidationTests|BrowserApiContractTests|BrowserFrontendWorkspaceTests" --logger "console;verbosity=minimal"`.
- [ ] T038 Run frontend verification: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- [ ] T039 Run client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- [ ] T040 Run Spec Kit prerequisites and confirm `FEATURE_DIR` is `specs/919-daren-qte-training`.
- [ ] T041 Run `git diff --check origin/main...HEAD` and an added-line static scan for secrets/shell/eval/pickle/SQL-injection patterns, excluding generated/scratch artifacts.
- [ ] T042 Reconcile this `tasks.md`: mark only tasks with implementation and verification evidence complete before PR.
- [ ] T043 Obtain independent review against #919 acceptance, Spec Kit artifacts, docs/examples, tests, profile/New Game reward semantics, browser/console parity, and player-copy boundaries.
- [ ] T044 Fix critical/important review findings, rerun focused verification, and re-review until approved.
- [ ] T045 Create PR with local verification evidence and `GitHub Actions: not used/not required`.
- [ ] T046 Squash merge after local gates and approved review; delete remote branch, fast-forward local `main`, run post-merge focused verification, comment evidence on #919, and confirm issue closed.

## Dependencies and Execution Order

- Phase 1 must complete before production edits.
- Phase 2 RED tests/source guards must fail for the intended reason before corresponding implementation.
- Phase 3 route/QTE implementation must be green before reward profile/New Game grant work.
- Phase 4 reward/profile/New Game semantics must be green before UI surfaces claim completion.
- Phase 5 browser/console surfaces must be green before docs/PR.
- Phase 6 docs/source guards must be green before review.
- Phase 7 gates and independent review are mandatory before merge/closure.

## Parallel Opportunities

- T005-T013 can be drafted in parallel if each worker owns separate test files and then reconciles RED evidence.
- Documentation updates T031-T035 can proceed after contract behavior is stable.
- Browser UI tests and console UI tests can be implemented independently once C# route/profile DTOs are defined.
