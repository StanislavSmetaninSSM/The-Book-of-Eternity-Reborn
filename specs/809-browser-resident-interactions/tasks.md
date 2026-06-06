# Tasks: Browser Shining Abode Resident Interactions

**Input**: Design documents from `/specs/809-browser-resident-interactions/`

**Prerequisites**: `plan.md`, `spec.md`, GitHub issue #809, parent #817, constitution 1.1.0.

**Tests**: Behavior changes require strict TDD: write focused failing C# tests before production code, run them to confirm RED, then implement minimal GREEN and rerun affected suites.

**Organization**: Tasks are grouped by independently testable user stories. Codex may combine adjacent tasks in one commit only when RED/GREEN evidence remains clear.

## Phase 1: Setup and Baseline

**Purpose**: Establish the active issue, Spec Kit artifacts, code paths, and clean baseline.

- [X] T001 Confirm worktree `E:/Games/worktrees/boe-809-residents`, branch `fix/809-browser-residents`, source issue #809, parent #817, and `git status --short --branch`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/809-browser-resident-interactions/spec.md`, `specs/809-browser-resident-interactions/plan.md`, and this file before editing.
- [X] T003 Inspect existing authority paths enough for planning: console `ExplorerMode.Afterlife.GuardiansProjectsTrade.cs` resident roster/talk/history/transfer actions, `GuardianAbodeResidentRequestState`, `GuardianAbodeResidentState`, `BrowserAfterlifeWriteService`, `ExplorerWebPromptSessionService`, `ExplorerCommandCatalog`, `/help`, browser API fixtures, and related tests.
- [X] T004 Preserve the recorded baseline: focused C# filter passed 210/210 and `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with 27/27 Vitest tests and successful Vite build before implementation.

---

## Phase 2: Foundational Tests and Contract Decision

**Purpose**: Define expected browser resident behavior before production changes.

- [X] T005 [P] [US1] Add a RED C# command-result/prompt-session test proving a browser resident roster command opens a form with Guardian/Abode selection when a Guardian has a Shining Abode.
- [X] T006 [P] [US1] Add a RED C# write-handler test proving a valid roster submit writes one `GuardianAbodeResidentRequestState.PendingGuardianAbodeResidentsRequest` for the selected Guardian/Abode and current turn.
- [X] T007 [P] [US1] Add a RED duplicate roster pending test proving a second matching Guardian/Abode roster request is rejected or kept pending with player-facing copy instead of silently overwriting/duplicating.
- [X] T008 [P] [US2] Add a RED C# command-result/prompt-session test proving a browser resident interaction command opens a form with canonical resident selection and `talk` / `history` choice.
- [X] T009 [P] [US2] Add a RED write-handler test proving valid `talk` submission writes one `PendingGuardianAbodeResidentInteractionRequest` with `interactionType=talk`.
- [X] T010 [P] [US2] Add a RED write-handler test proving valid `history` submission writes one `PendingGuardianAbodeResidentInteractionRequest` with `interactionType=history`.
- [X] T011 [P] [US2] Add RED duplicate/malformed-pending tests for resident talk/history that prove duplicate interaction type for the same resident is blocked and malformed bundles are not overwritten.
- [X] T012 [P] [US3] Add RED transfer tests for target transfer and departure-only submit, asserting `PendingGuardianAbodeResidentTransferRequest` fields and no write when resident is not transfer-ready or already has a transfer pending.
- [X] T013 [P] [US4] Add RED realm-guard tests for direct command open outside valid afterlife/Shining Abode context and stale prompt submit after realm switch to Mortal World.
- [X] T014 [P] [US4] Add a RED player-facing copy/source-guard test proving default browser messages for malformed/blocked resident state do not expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or `game_state/` wording.
- [X] T015 [US4] Decide from current docs/tests whether resident roster/interactions/transfers pending shapes and GM closure guidance already cover browser-originated requests. If shape/guidance changes or docs are missing, update docs/examples/tests in Phase 6.

**Checkpoint**: RED tests fail for missing browser resident parity for the expected reason, not because of typos or fixture setup.

---

## Phase 3: User Story 1 - Browser requests resident roster (Priority: P1) 🎯 MVP

**Goal**: Browser command/prompt-session flow requests the same Guardian Abode residents roster as the console panel.

**Independent Test**: Focused C# tests open the command, submit form answers, read `pending_guardian_abode_residents_request.json`, and assert request fields plus result copy.

### Implementation for User Story 1

- [X] T016 [US1] Add command catalog/help metadata for the resident roster action, including English/Russian aliases, browser-supported mutating status under the Shining Abode/Guardians group, and optional Guardian/Abode argument acceptance when consistent with existing catalog patterns.
- [X] T017 [US1] Implement command-result prompt construction in `ExplorerLifecycleLocalTurnCommandResultBuilder`: list eligible Guardians with Abodes from `guardians.json`, include founder-attraction request-mode context when applicable, and return player-facing empty-state copy when no eligible Abodes are available.
- [X] T018 [US1] Implement `BrowserAfterlifeWriteService` handling for roster submit: validate realm, Guardian/Abode identity, duplicate/malformed pending request, current reputation/request mode, local write coordination, and write through `GuardianAbodeResidentRequestState.WriteResidentsRequestAsync`.
- [X] T019 [US1] Run the focused RED tests from T005-T007 and confirm they now pass; then rerun the focused C# verification filter from `plan.md`.

**Checkpoint**: User Story 1 is functional from C# command/prompt-session tests.

---

## Phase 4: User Story 2 - Browser talks/listens to resident history (Priority: P1)

**Goal**: Browser interaction command writes talk/history pending requests for canonical Shining Abode residents.

**Independent Test**: Focused C# tests select a canonical resident, submit `talk` and `history`, read `pending_guardian_abode_resident_interactions.json`, and assert request fields plus result copy.

### Implementation for User Story 2

- [X] T020 [US2] Add command catalog/help metadata for resident interaction action(s), including English/Russian aliases, browser-supported mutating status, and optional resident argument acceptance when consistent with existing catalog patterns.
- [X] T021 [US2] Implement prompt construction that enumerates only canonical resident roster entries from `guardian_abode_residents.json` associated with a Guardian/Abode; do not enumerate nested history/receipt/transfer objects merely because they carry `residentId`.
- [X] T022 [US2] Implement interaction type availability using console-equivalent semantics: default to `talk`/`history` when `availableInteractions` is empty, respect explicit omissions, and block non-present residents when console semantics require it.
- [X] T023 [US2] Implement `BrowserAfterlifeWriteService` handling for `talk`/`history`: validate realm, resident identity, Guardian/Abode relationship, interaction type, duplicate/malformed pending bundle, local write coordination, and write through `GuardianAbodeResidentRequestState.WriteInteractionRequestAsync`.
- [X] T024 [US2] Sanitize duplicate, missing-state, malformed-pending, and local-write failure messages for default browser result surfaces.
- [X] T025 [US2] Run the focused RED tests from T008-T011 and confirm they now pass; then rerun the focused C# verification filter from `plan.md`.

**Checkpoint**: User Story 2 is functional from C# command/prompt-session tests.

---

## Phase 5: User Story 3 - Browser requests resident transfer (Priority: P1)

**Goal**: Browser transfer command writes resident target/departure transfer pending requests through the existing resident transfer contract.

**Independent Test**: Focused C# tests seed source and target Abodes, submit target and departure-only forms, read `pending_guardian_abode_resident_transfers.json`, and assert request fields plus result copy.

### Implementation for User Story 3

- [X] T026 [US3] Add command catalog/help metadata for resident transfer action, including English/Russian aliases and browser-supported mutating status.
- [X] T027 [US3] Implement transfer prompt construction that selects a transfer-ready canonical resident and builds safe target/departure choices using shared `GuardianAbodeResidentState.BuildTransferCompetitionCandidates` or existing equivalent C# logic.
- [X] T028 [US3] Implement `BrowserAfterlifeWriteService` handling for transfer submit: validate realm, resident identity, transfer readiness, target/departure selection, duplicate/malformed pending transfer bundle, local write coordination, and write through `GuardianAbodeResidentRequestState.WriteTransferRequestAsync`.
- [X] T029 [US3] Ensure transfer result copy explains GM resolution without exposing raw IDs/paths in default UI while preserving enough player context about source/target Abode or departure-only choice.
- [X] T030 [US3] Run the focused RED tests from T012 and confirm they now pass; then rerun the focused C# verification filter from `plan.md`.

**Checkpoint**: User Story 3 is functional from C# command/prompt-session tests.

---

## Phase 6: User Story 4 - Realm safety, metadata, frontend fixtures, and GM contract reconciliation (Priority: P2)

**Goal**: Direct commands and stale prompts enforce realm safety; browser metadata/fixtures/docs stay synchronized.

**Independent Test**: Focused C# tests prove invalid realm open/submit and default copy guards; browser contract/source-guard tests and documentation tests pass when changed.

### Implementation for User Story 4

- [X] T031 [US4] Add command-level valid-realm guards before opening resident prompts; invalid realms return Russian player-facing blockers and not `RequiresInput`.
- [X] T032 [US4] Add write-level valid-realm guards in `BrowserAfterlifeWriteService` before writing roster, interaction, or transfer pending files; stale prompt sessions after realm switch must not write.
- [X] T033 [US4] Update `/help`, browser command coverage/action metadata, API contract fixtures, and C# source guards so resident commands are discoverable without raw debug framing.
- [X] T034 [US4] Ensure prompt-session submission/cancel continues through existing `browserApi.submitPromptSession` and `browserApi.cancelPromptSession`; update React fixture/types only if the C# contract fixture changes.
- [X] T035 [US4] Inspect `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, examples/manifests, and documentation coverage tests for resident roster/talk/history/transfer closure coverage. If any guidance or example is missing/stale, update it in this PR; if no docs update is required, record the no-shape-drift rationale in the PR/final report.
- [X] T036 [US4] Run docs/contract tests when docs/contracts/examples are touched, or record explicit no-docs rationale when existing guidance already covers these contracts.
- [X] T037 [US4] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` after any fixture/frontend change.
- [X] T038 [US4] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm `FEATURE_DIR` resolves to `specs/809-browser-resident-interactions` or document any repo-local Spec Kit script limitation.

**Checkpoint**: Default browser metadata is player-facing and contract/docs status is explicit.

---

## Phase 7: Final Verification, Review, PR, Merge

**Purpose**: Verify, review, merge, close #809, and keep #817 open for remaining child tasks.

- [X] T039 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- [X] T040 Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- [X] T041 Run the focused C# verification filter from `plan.md` and record exact pass/fail/skip counts.
- [X] T042 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record typecheck/test/build evidence.
- [X] T043 Run docs/contract verification when docs/contracts/examples changed, or record the explicit no-docs rationale.
- [X] T044 Run `git diff --check origin/main...HEAD`.
- [X] T045 Run an added-line static scan excluding `docs/superpowers/plans/*.md`; inspect any token/secret/raw-diagnostic matches manually.
- [X] T046 Run a refined default player-facing raw diagnostic scan over production UI/frontend additions for `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, raw, and `game_state/` leakage; exclude tests/specs and internal rollback constants from automated failure while still reviewing them.
- [X] T047 Reconcile `spec.md`, `plan.md`, and this `tasks.md` against the final diff. Do not mark tasks complete unless code/tests/docs and verification evidence exist.
- [X] T048 Obtain independent review before PR/merge. Critical/Important findings must be fixed and re-reviewed before merging.
  - Initial independent review `E:/Games/codex-runs/20260607-0857-boe-809-residents-review` returned `CHANGES_REQUIRED`: generic `/resident_interaction` and `/resident_transfer` browser forms derived dependent choices from the first sorted resident, making later residents' `history` or transfer choices unreachable from the default quick-action path.
  - Added RED regressions `SubmitAsync_ResidentInteraction_GenericPromptAllowsLaterResidentHistoryChoice` and `SubmitAsync_ResidentTransfer_GenericPromptAllowsLaterReadyResidentTransferChoice`; both failed before the fix for the expected missing-option reason.
  - Fixed command-result prompt construction so no-argument resident forms aggregate valid interaction/transfer choices across selectable residents while submit/write handlers still validate the selected resident.
  - Post-fix focused #809 gate passed: `BrowserResidentInteractionsParityTests|GuardianAbodeResident` => 65/65; affected browser/social/docs/contract slice => 241/241.
  - Re-review `E:/Games/codex-runs/20260607-0920-boe-809-residents-rereview` returned `VERDICT: APPROVED`, previous Important blocker fixed, Critical/Important/Minor findings none, safe to merge yes.
- [ ] T049 Create/update PR for #809 with local verification evidence and `GitHub Actions: not required`; squash-merge with `[skip ci]` after local-gated approval.
- [ ] T050 Verify PR is `MERGED`, issue #809 is `CLOSED`/`COMPLETED`, `main` fast-forwards, focused post-merge check passes, and the issue worktree/branch are cleaned up.

---

## Dependencies & Execution Order

- Phase 1 must complete before edits.
- Phase 2 tests must be written and observed failing before Phase 3/4/5/6 production implementation.
- Roster, interaction, and transfer slices can be implemented in a sensible order, but each behavior needs RED/GREEN evidence.
- Phase 6 docs/contracts work is conditional on actual docs/guidance drift, but inspection/rationale is mandatory.
- Phase 7 requires all relevant phases and independent review.

## Parallel Opportunities

- T005-T014 can be drafted in parallel if they touch different test methods/files, but implementation must still respect RED/GREEN proof.
- T033-T034 can proceed after command/write paths exist.
- T035-T036 are conditional and can be skipped only with explicit rationale.

## Notes

- Keep #810-#817 umbrella/sibling issues out of this PR unless implementation accidentally satisfies a child acceptance criterion and that is verified separately.
- Do not implement resident gameplay rules in React. React may render prompts/results and consume contract fixtures only.
- Avoid broad refactors of existing large C# files except for small helpers required by the tests.
- If the full #809 scope becomes too large for one Codex run, Codex should stop after a coherent reviewed slice and report exactly what remains instead of partially implementing unverified flows.
