# Tasks: Browser Shining Abode Politics

**Input**: Design documents from `/specs/810-browser-shining-politics/`

**Prerequisites**: `plan.md`, `spec.md`, GitHub issue #810, constitution 1.1.0.

**Tests**: Behavior changes require strict TDD: write focused failing C# tests/source guards before production code, run them to confirm RED for the expected missing-browser-parity reason, then implement minimal GREEN and rerun affected suites.

**Organization**: Tasks are grouped by independently testable user stories. Codex may combine adjacent implementation tasks in one commit only when RED/GREEN evidence remains clear.

## Phase 1: Setup and Baseline

**Purpose**: Establish the active issue, Spec Kit artifacts, code paths, and clean baseline.

- [X] T001 Confirm worktree `E:/Games/worktrees/boe-810-shining-politics`, branch `task/810-browser-shining-politics`, source issue #810, and `git status --short --branch`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, existing specs #805-#809, console `ExplorerMode.Afterlife.ShiningAbode.Politics.cs`, `ShiningFactionRequestState`, browser prompt/write services, command catalog/help/menu/coverage, and related tests before editing production code.
- [X] T003 Preserve the recorded baseline from Hermes: focused Shining/browser C# filter passed 308/308 before implementation.
- [X] T004 Create and reconcile `specs/810-browser-shining-politics/spec.md`, `plan.md`, and this `tasks.md` with source issue #810 linked in all three.

---

## Phase 2: Foundational RED Tests and Contract Decision

**Purpose**: Define expected browser Shining politics behavior before production changes.

- [X] T005 [P] [US1] Add a RED C# command-result/prompt-session test proving a browser founding command opens a form with founding fields, costs, and eligible supporters in active Shining Abode.
- [X] T006 [P] [US1] Add a RED C# write-handler test proving a valid founding submit writes one `PendingShiningFactionFoundingRequest` through existing contract fields and reserves costs.
- [X] T007 [P] [US2] Add a RED C# command-result/prompt-session test proving a browser realignment command opens a form with canonical ready-to-realign residents and visible target factions.
- [X] T008 [P] [US2] Add a RED C# write-handler test proving accepted-transfer realignment submit writes one `PendingShiningFactionRealignmentRequest`.
- [X] T009 [P] [US2] Add a RED C# write-handler test proving departure-to-neutral realignment submit writes one request without a target faction.
- [X] T010 [P] [US3] Add a RED C# command-result/prompt-session test proving a browser leadership command opens a form with visible factions, transition modes, candidates, and supporters.
- [X] T011 [P] [US3] Add a RED C# write-handler test proving peaceful succession or revolt submit writes one `PendingShiningFactionLeadershipTransitionRequest`.
- [X] T012 [P] [US4] Add RED direct command realm-guard tests for all three Shining politics mutation commands outside Shining Abode.
- [X] T013 [P] [US4] Add a RED stale prompt-submit realm-guard test proving a prompt opened in Shining Abode writes nothing after realm switch.
- [X] T014 [P] [US4] Add RED metadata/help/coverage/source-guard tests proving player-facing labels exist, coverage treats the three forms as browser-supported, and production write handling calls `WriteFoundingRequestAsync`, `WriteRealignmentRequestAsync`, and `WriteLeadershipTransitionRequestAsync`.
- [X] T015 [US4] Decide from current docs/tests whether Shining politics pending shapes and GM closure guidance already cover browser-originated requests. If shape/guidance changes or docs are missing, update docs/examples/tests in Phase 6.

**Checkpoint**: RED tests fail for missing browser Shining politics parity for the expected reason, not because of typo or fixture setup failures.

---

## Phase 3: User Story 1 - Browser founding request (Priority: P1)

**Goal**: Browser command/prompt-session flow requests the same Shining faction founding as the console panel.

**Independent Test**: Focused C# tests open the command, submit form answers, read `pending_shining_faction_foundings.json`, and assert request fields, resource reservation, and result copy.

- [X] T016 [US1] Add command catalog/help/menu metadata for faction founding, including English/Russian aliases, Shining Abode group, browser-supported mutating status, and local-turn mutation mode.
- [X] T017 [US1] Implement founding prompt construction in `ExplorerShiningAbodeCommandResultBuilder`: validate active Shining Abode context, show costs, enumerate eligible ascended supporters, and return player-facing empty-state blockers.
- [X] T018 [US1] Implement `BrowserAfterlifeWriteService` founding submit handling: validate realm, form fields, supporters, resources, duplicate/malformed pending state, local write coordination, and call `ShiningFactionRequestState.WriteFoundingRequestAsync`.
- [X] T019 [US1] Run founding RED tests from T005-T006 and confirm they now pass; rerun the focused Shining/browser C# filter.

**Checkpoint**: User Story 1 is functional from C# command/prompt-session/write tests.

---

## Phase 4: User Story 2 - Browser faction realignment (Priority: P1)

**Goal**: Browser realignment command writes pending resident realignment requests through the existing contract.

**Independent Test**: Focused C# tests select a ready resident, submit accepted-transfer and departure-to-neutral modes, read `pending_shining_faction_realignments.json`, and assert request fields plus result copy.

- [X] T020 [US2] Add command catalog/help/menu metadata for faction realignment, including English/Russian aliases, Shining Abode group, browser-supported mutating status, and local-turn mutation mode.
- [X] T021 [US2] Implement realignment prompt construction that enumerates only canonical ready-to-realign residents and visible eligible target factions.
- [X] T022 [US2] Implement `BrowserAfterlifeWriteService` realignment submit handling: validate realm, resident identity, readiness, source/target factions, mode, duplicate/malformed pending state, local write coordination, and call `ShiningFactionRequestState.WriteRealignmentRequestAsync`.
- [X] T023 [US2] Sanitize duplicate, missing-state, malformed-pending, and local-write failure messages for default browser result surfaces.
- [X] T024 [US2] Run realignment RED tests from T007-T009 and confirm they now pass; rerun the focused Shining/browser C# filter.

**Checkpoint**: User Story 2 is functional from C# command/prompt-session/write tests.

---

## Phase 5: User Story 3 - Browser leadership transition (Priority: P1)

**Goal**: Browser leadership command writes pending leadership transition requests through the existing contract.

**Independent Test**: Focused C# tests seed a visible faction with incumbent, candidate, and supporters; submit a transition form; read `pending_shining_faction_leadership_transitions.json`; and assert request fields plus result copy.

- [X] T025 [US3] Add command catalog/help/menu metadata for leadership transition, including English/Russian aliases, Shining Abode group, browser-supported mutating status, and local-turn mutation mode.
- [X] T026 [US3] Implement leadership prompt construction that enumerates visible factions with current leadership, valid transition modes, candidate choices, and eligible supporters without hidden data leakage.
- [X] T027 [US3] Implement `BrowserAfterlifeWriteService` leadership submit handling: validate realm, faction/incumbent identity, mode, candidate/supporters, duplicate/malformed pending state, local write coordination, and call `ShiningFactionRequestState.WriteLeadershipTransitionRequestAsync`.
- [X] T028 [US3] Run leadership RED tests from T010-T011 and confirm they now pass; rerun the focused Shining/browser C# filter.

**Checkpoint**: User Story 3 is functional from C# command/prompt-session/write tests.

---

## Phase 6: User Story 4 - Realm safety, metadata, fixtures, and contract reconciliation (Priority: P2)

**Goal**: Direct commands and stale prompts enforce realm safety; browser metadata/help/fixtures/docs stay synchronized.

- [X] T029 [US4] Add command-level valid-realm guards before opening all three Shining politics prompts; invalid realms return Russian player-facing blockers and not `RequiresInput`.
- [X] T030 [US4] Add write-level valid-realm guards in `BrowserAfterlifeWriteService` before writing all three pending files; stale prompt sessions after realm switch must not write.
- [X] T031 [US4] Register all three commands with `ExplorerWebPromptSessionService` local-turn lock requirements.
- [X] T032 [US4] Update `/help`, browser command coverage/action metadata, API contract fixtures, and C# source guards so Shining politics actions are discoverable without raw debug framing.
- [X] T033 [US4] Inspect `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and documentation coverage tests if any pending/control or GM-authored contract surface changes. If no contract shape changes occur, record no-docs rationale in the final report.
- [X] T034 [US4] Run docs/contract tests when docs/contracts/examples are touched, or record explicit no-docs rationale when existing contracts are reused unchanged.
- [X] T035 [US4] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` after any frontend/fixture change.
- [X] T036 [US4] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm `FEATURE_DIR` resolves to `specs/810-browser-shining-politics`.

**Checkpoint**: Default browser metadata is player-facing and contract/docs status is explicit.

---

## Phase 7: Final Verification and Commit

**Purpose**: Verify, commit, and leave PR/merge/issue closure for Hermes.

- [X] T037 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal`.
- [X] T038 Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal`.
- [X] T039 Run the focused C# verification filter from `plan.md` and record exact pass/fail/skip counts.
- [X] T040 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files or browser contract fixtures changed; otherwise record that frontend verification was not required.
- [X] T041 Run docs/contract verification when docs/contracts/examples changed, or record the explicit no-docs rationale.
- [X] T042 Run `git diff --check origin/main...HEAD`.
- [X] T043 Run an added-line static security scan excluding Spec Kit docs and inspect any token/secret/raw-diagnostic matches manually.
- [X] T044 Run a refined default player-facing raw diagnostic scan over production UI/browser additions for `.json`, `pending_`, request IDs, API, DTO, rollback, snapshot, debug, raw, and `game_state/` leakage; exclude tests/specs/internal rollback constants from automated failure while still reviewing them.
- [X] T045 Reconcile `spec.md`, `plan.md`, and this `tasks.md` against the final diff. Do not mark tasks complete unless code/tests/docs and verification evidence exist.
- [X] T046 Commit locally with concise message containing `[skip ci]` after verification passes.
- [X] T047 Leave PR creation, merge, issue closure, and umbrella/sibling lifecycle to Hermes.

## Dependencies & Execution Order

- Phase 1 must complete before production edits.
- Phase 2 tests must be written and observed failing before Phase 3/4/5/6 production implementation.
- Founding, realignment, and leadership can be implemented in a pragmatic order, but each behavior needs RED/GREEN evidence.
- Phase 6 docs/contracts work is conditional on actual docs/guidance drift, but inspection/rationale is mandatory.
- Phase 7 requires all relevant phases and verification.

## Parallel Opportunities

- T005-T014 can be drafted in parallel if they touch separate test methods/files, but implementation must still respect RED/GREEN proof.
- Command metadata tasks T016/T020/T025 can be implemented together after RED evidence.
- Write handlers T018/T022/T027 can share helper methods as long as existing request writers remain the authority.
- T033-T034 are conditional and can be skipped only with explicit rationale.

## Notes

- Keep #811-#816 and umbrella #817 out of this branch's lifecycle work.
- Do not implement gameplay rules in React. React may render prompts/results and consume contract fixtures only.
- Avoid broad refactors of existing large C# files except for small helpers required by the tests.
- If the full #810 scope becomes too large for one run, stop after a coherent verified slice and report exactly what remains instead of partially implementing unverified flows.

## Completion Evidence

- RED: `BrowserShiningPoliticsParityTests` initially failed 14/14 before production implementation because the three Shining politics browser commands were unknown/unwired.
- GREEN focused #810: `BrowserShiningPoliticsParityTests` passed 14/14 after implementation.
- Codex final focused C#: Shining/browser filter passed 322/322.
- Codex contract/registry: `BrowserApiContractTests|ExplorerCommandMigrationRegistryTests` passed 43/43.
- Hermes reconciliation removed the transient Spec Kit active-feature pointer from the merge diff (`.specify/feature.json` and the #810-specific `AGENTS.md` plan path) because branch-name feature discovery still resolves this feature and `main` must not keep a stale current-plan pointer.
- Hermes fresh focused #810 before review: `BrowserShiningPoliticsParityTests` passed 14/14 on the final Codex diff.
- Hermes fresh affected Shining/browser/docs/contract slice before review passed 364/364.
- Hermes docs-sensitive afterlife coverage passed 99/99.
- Independent review `E:/Games/codex-runs/20260607-1254-boe-810-shining-politics-review` returned `CHANGES_REQUIRED`: hidden/non-visible Shining factions, residents, and radiant actors could become selectable or writable through browser politics flows.
- Review-fix RED regressions confirmed the blocker: hidden-source resident prompt, stale hidden target submit, and hidden radiant actor submit failed 3/3 before production fix.
- Review fix: browser prompt builders and submit/write handlers now apply stricter player-visible/operational faction filtering, hide residents whose source faction is not visible, hide hidden radiant actors, and re-check target/candidate/supporter visibility at submit time.
- Review-fix GREEN regressions passed 3/3.
- Hermes final focused #810 after review fix: `BrowserShiningPoliticsParityTests` passed 17/17.
- Hermes final affected Shining/browser/docs/contract slice after review fix passed 465/465.
- Builds: `BookOfEternityClient` and `BookOfEternityClient.Tests` both built with 0 warnings and 0 errors.
- Frontend: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed; Vitest reported 29/29 tests passed and Vite production build succeeded.
- Spec Kit prerequisite check resolved `FEATURE_DIR` to `specs/810-browser-shining-politics` without a tracked `.specify/feature.json` pointer.
- `git diff --check origin/main` passed on the final combined diff.
- Added-line secret scan hit only the literal visibility value `"secret"` inside hidden-visibility filtering; no credentials/tokens were added. Raw/default UI scan hits were manually classified as internal identifiers/constants only, not default player-facing leakage.
- Re-review `E:/Games/codex-runs/20260607-1329-boe-810-shining-politics-rereview` returned `APPROVED`, confirmed previous Important finding fixed, no Critical/Important/Minor findings, safe to merge.
- Contracts/docs: no pending/control payload shape, GM response, receipt, validation, or docs/example contract changed; existing Shining politics pending writers/validators are reused.
