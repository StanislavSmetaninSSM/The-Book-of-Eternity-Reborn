# Tasks: Browser NPC Social Conversation

**Input**: Design documents from `/specs/807-browser-npc-social/`

**Prerequisites**: `plan.md`, `spec.md`, GitHub issue #807, parent epic #817, constitution 1.1.0.

**Tests**: Behavior changes require strict TDD: write focused failing C# tests before production code, run them to confirm RED, then implement minimal GREEN and rerun affected suites.

**Organization**: Tasks are grouped by independently testable user stories. Codex may combine adjacent tasks in one commit only when RED/GREEN evidence remains clear.

## Phase 1: Setup and Baseline

**Purpose**: Establish the active issue, Spec Kit artifacts, code paths, and clean baseline.

- [ ] T001 Confirm worktree `E:/Games/worktrees/boe-807-npc-social`, branch `fix/807-browser-npc-social`, source issue #807, parent #817, and `git status --short --branch`.
- [ ] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/807-browser-npc-social/spec.md`, `specs/807-browser-npc-social/plan.md`, and this file before editing.
- [ ] T003 Inspect existing authority paths: `ActorSocialInteractionRequestState`, console `ExplorerMode.Npcs.ListAndDetails.cs` talk action, `ExplorerLifecycleLocalTurnCommandResultBuilder`, `BrowserMortalWorldWriteService`, `ExplorerWebPromptSessionService`, `ExplorerCommandCatalog`, `/help`, browser API fixtures, and related tests.
- [ ] T004 Preserve the recorded baseline: focused C# filter passed 35/35 and `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with 27/27 Vitest tests and successful Vite build before implementation.

---

## Phase 2: Foundational Tests and Contract Decision

**Purpose**: Define the expected browser NPC conversation behavior before production changes.

- [ ] T005 [P] [US1] Add a RED C# command-result/prompt-session test proving a browser NPC conversation command opens a form with NPC selection and topic input.
- [ ] T006 [P] [US1] Add a RED C# write-handler test proving valid submission writes one `ActorSocialInteractionRequestState.PendingNpcSocialInteractionRequest` for the selected NPC and current turn.
- [ ] T007 [P] [US1] Add a RED duplicate-pending test proving a second talk request for the same NPC is rejected or kept pending with player-facing copy instead of silently overwriting/duplicating.
- [ ] T008 [P] [US2] Add a RED player-facing copy/source-guard test proving default browser messages for malformed/blocked NPC social state do not expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or `game_state/` wording.
- [ ] T009 [US3] Decide from tests and current `PendingNpcSocialInteractionRequest` whether the browser-supplied topic must become a new optional pending request field. If yes, keep the field scoped to Mortal NPC social requests and update docs/examples/tests in Phase 5.

**Checkpoint**: RED tests fail for missing browser NPC social parity for the expected reason, not because of typos or fixture setup.

---

## Phase 3: User Story 1 - Browser starts a Mortal NPC conversation (Priority: P1) 🎯 MVP

**Goal**: Browser command/prompt-session flow creates the same pending NPC talk request as the console action, with topic captured when required.

**Independent Test**: Focused C# tests open the command, submit form answers, read `pending_npc_social_interactions.json`, and assert request fields plus result copy.

### Implementation for User Story 1

- [ ] T010 [US1] Add command catalog/help metadata for the NPC conversation action, including English/Russian aliases and `MutatingParity` browser status under Mortal World.
- [ ] T011 [US1] Implement command-result prompt construction in `ExplorerLifecycleLocalTurnCommandResultBuilder`: list known NPCs from `npc_core.json`, accept command arguments/manual ID fallback, include a required or explicitly-justified optional topic prompt, and return player-facing empty-state copy when no NPCs are available.
- [ ] T012 [US1] Implement `BrowserMortalWorldWriteService` handling for the command: validate NPC identity, validate topic, check duplicate pending `talk`, write through `ActorSocialInteractionRequestState.WriteNpcRequestAsync`, and return Russian success/validation/blocker results.
- [ ] T013 [US1] If topic is stored, extend `PendingNpcSocialInteractionRequest` with an optional player-facing topic field and update `BuildSystemReminderFragmentAsync` so the GM sees the topic while closure remains `npcInteractionJournalUpdates`.
- [ ] T014 [US1] Run the focused RED tests from T005-T007 and confirm they now pass; then rerun the full focused C# baseline filter from `plan.md`.

**Checkpoint**: User Story 1 is functional from C# command/prompt-session tests.

---

## Phase 4: User Story 2 - Player-facing browser copy and metadata (Priority: P2)

**Goal**: Default browser surfaces for NPC conversation remain Russian/player-facing and fit the current minimalist command/composer direction.

**Independent Test**: Source/contract tests check labels, help/action metadata, default result messages, and absence of raw diagnostic wording.

### Implementation for User Story 2

- [ ] T015 [US2] Update `/help`, browser command coverage/action metadata, API contract fixtures, and C# source guards so the new NPC conversation command is discoverable without raw debug framing.
- [ ] T016 [US2] Ensure prompt-session submission/cancel continues through existing `browserApi.submitPromptSession` and `browserApi.cancelPromptSession`; update React fixture/types only if the C# contract fixture changes.
- [ ] T017 [US2] Run the player-facing copy/source-guard tests from T008 and confirm they pass.
- [ ] T018 [US2] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` after any fixture/frontend change.

**Checkpoint**: Default browser UI/metadata is player-facing and frontend verify is green.

---

## Phase 5: User Story 3 - GM contract/documentation reconciliation (Priority: P3)

**Goal**: Any pending request payload change is documented and tested; no afterlife contract drift is introduced.

**Independent Test**: Documentation/source-guard tests pass when docs/examples change, or the PR documents why no docs update was needed.

### Implementation for User Story 3

- [ ] T019 [US3] If T013 added a topic field or changed reminder semantics, update relevant GM-facing docs/examples/manifests such as `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and documentation coverage/source-guard tests. If no contract shape changed, record the rationale in the PR body and final report.
- [ ] T020 [US3] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` when docs/contracts/examples are touched.
- [ ] T021 [US3] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm `FEATURE_DIR` resolves to `specs/807-browser-npc-social` or document any repo-local Spec Kit script limitation.

---

## Phase 6: Final Verification, Review, PR, Merge

**Purpose**: Verify, review, merge, close #807, and keep #817 open for remaining child tasks.

- [ ] T022 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- [ ] T023 Run the focused C# verification filter from `plan.md` and record exact pass/fail/skip counts.
- [ ] T024 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record typecheck/test/build evidence.
- [ ] T025 Run `git diff --check origin/main...HEAD`.
- [ ] T026 Run an added-line static scan excluding `docs/superpowers/plans/*.md`; inspect any token/secret/raw-diagnostic matches manually.
- [ ] T027 Reconcile `spec.md`, `plan.md`, and this `tasks.md` against the final diff. Do not mark tasks complete unless code/tests/docs and verification evidence exist.
- [ ] T028 Obtain independent review before PR/merge. Critical/Important findings must be fixed and re-reviewed before merging.
- [ ] T029 Create/update PR for #807 with local verification evidence and `GitHub Actions: not required`; squash-merge with `[skip ci]` after local-gated approval.
- [ ] T030 Verify PR is `MERGED`, issue #807 is `CLOSED`/`COMPLETED`, `main` fast-forwards, focused post-merge check passes, and the issue worktree/branch are cleaned up.

---

## Dependencies & Execution Order

- Phase 1 must complete before edits.
- Phase 2 tests must be written and observed failing before Phase 3/4 production implementation.
- Phase 3 MVP must pass before Phase 4 metadata/frontend fixture synchronization.
- Phase 5 runs only if pending contract/docs/examples changed; otherwise record explicit no-docs rationale.
- Phase 6 requires all previous relevant phases and independent review.

## Parallel Opportunities

- T005-T008 can be drafted in parallel if they touch different test files, but implementation must still respect RED/GREEN proof.
- T015-T016 can proceed after the command/write path exists.
- T019-T020 are conditional and can be skipped only with explicit rationale.

## Notes

- Keep #808 Guardian social, #809 resident interactions, and #817 umbrella closure out of this PR unless implementation accidentally satisfies a child acceptance criterion and that is verified separately.
- Do not implement NPC social rules in React. React may render prompts/results and consume contract fixtures only.
- Avoid broad refactors of existing large C# files except for small helpers required by the tests.
