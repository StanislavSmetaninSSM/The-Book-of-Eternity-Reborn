# Tasks: Browser Guardian Social Conversation and Lore

**Input**: Design documents from `/specs/808-browser-guardian-social/`

**Prerequisites**: `plan.md`, `spec.md`, GitHub issue #808, parent #817, constitution 1.1.0.

**Tests**: Behavior changes require strict TDD: write focused failing C# tests before production code, run them to confirm RED, then implement minimal GREEN and rerun affected suites.

**Organization**: Tasks are grouped by independently testable user stories. Codex may combine adjacent tasks in one commit only when RED/GREEN evidence remains clear.

## Phase 1: Setup and Baseline

**Purpose**: Establish the active issue, Spec Kit artifacts, code paths, and clean baseline.

- [X] T001 Confirm worktree `E:/Games/worktrees/boe-808-guardian-social`, branch `fix/808-browser-guardian-social`, source issue #808, parent #817, and `git status --short --branch`.
- [X] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, `specs/808-browser-guardian-social/spec.md`, `specs/808-browser-guardian-social/plan.md`, and this file before editing.
- [X] T003 Inspect existing authority paths enough for planning: console `ExplorerMode.Afterlife.GuardiansProjectsTrade.cs` talk/lore actions, `ActorSocialInteractionRequestState`, `BrowserAfterlifeWriteService`, `ExplorerWebPromptSessionService`, `ExplorerCommandCatalog`, `/help`, browser API fixtures, and related tests.
- [X] T004 Preserve the recorded baseline: focused C# filter passed 166/166 and `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with 27/27 Vitest tests and successful Vite build before implementation.

---

## Phase 2: Foundational Tests and Contract Decision

**Purpose**: Define the expected browser Guardian social behavior before production changes.

- [X] T005 [P] [US1] Add a RED C# command-result/prompt-session test proving a browser Guardian social command opens a form with Guardian selection and interaction type choice (`talk` vs `lore`).
- [X] T006 [P] [US1] Add a RED C# write-handler test proving valid `talk` submission writes one `ActorSocialInteractionRequestState.PendingGuardianSocialInteractionRequest` for the selected Guardian and current turn.
- [X] T007 [P] [US1] Add a RED C# write-handler test proving valid `lore` submission writes one pending Guardian social request with `interactionType=lore`.
- [X] T008 [P] [US1] Add a RED duplicate-pending test proving a second request for the same Guardian and interaction type is rejected or kept pending with player-facing copy instead of silently overwriting/duplicating.
- [X] T009 [P] [US2] Add RED realm-guard tests for both direct command open outside valid afterlife/Chaos Sea context and stale prompt submit after realm switch to Mortal World.
- [X] T010 [P] [US2] Add a RED player-facing copy/source-guard test proving default browser messages for malformed/blocked Guardian social state do not expose raw `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, or `game_state/` wording.
- [X] T011 [US3] Decide from current docs/tests whether `pending_guardian_social_interactions.json` shape and GM closure guidance already cover browser-originated Guardian social requests. If shape/guidance changes or docs are missing, update docs/examples/tests in Phase 5.

**Checkpoint**: RED tests fail for missing browser Guardian social parity for the expected reason, not because of typos or fixture setup.

---

## Phase 3: User Story 1 - Browser starts a Guardian talk/lore request (Priority: P1) 🎯 MVP

**Goal**: Browser command/prompt-session flow creates the same pending Guardian talk/lore request as the console action.

**Independent Test**: Focused C# tests open the command, submit form answers, read `pending_guardian_social_interactions.json`, and assert request fields plus result copy.

### Implementation for User Story 1

- [X] T012 [US1] Add command catalog/help metadata for the Guardian social action, including English/Russian aliases, `MutatingParity` browser status under the Chaos Sea/Guardians group, and argument acceptance when a Guardian ID/name is provided.
- [X] T013 [US1] Implement command-result prompt construction in `ExplorerLifecycleLocalTurnCommandResultBuilder`: list known Guardians from `guardians.json` / active Guardian mirrors, accept command arguments/manual ID fallback, include `talk` and `lore` interaction choices, and return player-facing empty-state copy when no Guardians are available.
- [X] T014 [US1] Implement `BrowserAfterlifeWriteService` handling for the command: validate Guardian identity, validate interaction type, check duplicate pending requests, write through `ActorSocialInteractionRequestState.WriteGuardianRequestAsync`, and return Russian success/validation/blocker results.
- [X] T015 [US1] Run the focused RED tests from T005-T008 and confirm they now pass; then rerun the focused C# verification filter from `plan.md`.

**Checkpoint**: User Story 1 is functional from C# command/prompt-session tests.

---

## Phase 4: User Story 2 - Realm safety and player-facing browser copy (Priority: P1)

**Goal**: Direct command and prompt submit/write paths enforce afterlife/Chaos Sea context and default browser copy stays Russian/player-facing.

**Independent Test**: Focused C# tests prove invalid realm direct command returns a blocker without prompt, stale prompt submit returns a blocker without writing, and raw diagnostics do not leak.

### Implementation for User Story 2

- [X] T016 [US2] Add command-level valid-realm guard before opening the Guardian social prompt; invalid realms must return Russian player-facing blocker copy and `Completed`/blocked result, not `RequiresInput`.
- [X] T017 [US2] Add write-level valid-realm guard in `BrowserAfterlifeWriteService` before writing pending Guardian social requests; stale prompt sessions after realm switch must not write.
- [X] T018 [US2] Sanitize malformed/duplicate/local-write failure messages for default browser result surfaces so raw local-write, rollback, file path, or protocol wording stays out of normal UI.
- [X] T019 [US2] Run the realm-guard and player-facing copy/source-guard tests from T009-T010 and confirm they pass.

**Checkpoint**: Direct-command and stale-prompt realm safety is covered by regression tests.

---

## Phase 5: User Story 3 - Metadata, frontend fixtures, and GM contract reconciliation (Priority: P2)

**Goal**: Browser action metadata, fixtures, and GM-facing contract guidance stay synchronized.

**Independent Test**: Browser contract/source-guard tests and documentation tests pass when changed.

### Implementation for User Story 3

- [X] T020 [US3] Update `/help`, browser command coverage/action metadata, API contract fixtures, and C# source guards so the new Guardian social command is discoverable without raw debug framing.
- [X] T021 [US3] Ensure prompt-session submission/cancel continues through existing `browserApi.submitPromptSession` and `browserApi.cancelPromptSession`; update React fixture/types only if the C# contract fixture changes.
- [X] T022 [US3] Inspect `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, examples/manifests, and documentation coverage tests for Guardian social closure coverage. If any guidance or example is missing/stale, update it in this PR; if no docs update is required, record the no-shape-drift rationale in the PR/final report.
- [X] T023 [US3] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` when docs/contracts/examples are touched.
- [X] T024 [US3] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` after any fixture/frontend change.
- [X] T025 [US3] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm `FEATURE_DIR` resolves to `specs/808-browser-guardian-social` or document any repo-local Spec Kit script limitation.

**Checkpoint**: Default browser metadata is player-facing and contract/docs status is explicit.

---

## Phase 6: Final Verification, Review, PR, Merge

**Purpose**: Verify, review, merge, close #808, and keep #817 open for remaining child tasks.

- [X] T026 Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- [X] T027 Run `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` after restore/build artifacts exist.
- [X] T028 Run the focused C# verification filter from `plan.md` and record exact pass/fail/skip counts.
- [X] T029 Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record typecheck/test/build evidence.
- [X] T030 Run docs/contract verification when docs/contracts/examples changed, or record the explicit no-docs rationale.
- [X] T031 Run `git diff --check origin/main...HEAD`.
- [X] T032 Run an added-line static scan excluding `docs/superpowers/plans/*.md`; inspect any token/secret/raw-diagnostic matches manually.
- [X] T033 Run a refined default player-facing raw diagnostic scan over production UI/frontend additions for `.json`, `pending_`, `requestId`, API, DTO, rollback, snapshot, debug, raw, and `game_state/` leakage; exclude tests/specs and internal rollback constants from automated failure while still reviewing them.
- [X] T034 Reconcile `spec.md`, `plan.md`, and this `tasks.md` against the final diff. Do not mark tasks complete unless code/tests/docs and verification evidence exist.
- [X] T035 Obtain independent review before PR/merge. Critical/Important findings must be fixed and re-reviewed before merging.
  - Initial Hermes independent review `20260606-1920-boe-808-guardian-social-review`: `CHANGES_REQUIRED`; Important finding that Guardian social option/write resolution accepted nested non-Guardian objects in `guardians.json` such as `knownAbodes[]` / trade receipts carrying `guardianId`.
  - Review fix added RED regressions `ExecuteAsync_GuardianSocial_IgnoresNestedNonGuardianReferences`, `ExecuteAsync_GuardianSocial_UsesActiveGuardianMirrorWhenGuardiansArrayMissing`, and `SubmitAsync_GuardianSocial_RejectsNestedNonGuardianReferenceWithoutPendingWrite`; RED was 3 failed / 9 passed, then GREEN was 12 passed / 0 failed.
  - Resolver now enumerates only canonical `guardians[]` records plus the `activeGuardian` mirror in both command-open and write-submit paths; nested Abode/trade/quest/buyback-like objects are not listed or accepted.
  - Post-fix fresh gates before re-review: affected .NET slice 193/193 passed; frontend verify typecheck/player-facing/Vitest 27/27/build passed; client/tests builds 0 warnings / 0 errors; Spec Kit prerequisite resolved `specs/808-browser-guardian-social`; `git diff --check` passed; refined default UI raw diagnostic scan `NO_MATCHES`.
  - Focused re-review `20260606-1939-boe-808-guardian-social-rereview`: `APPROVED`, safe to merge yes; previous Important fixed; Critical/Important/Minor findings none. Reviewer reran focused `BrowserGuardianSocialParityTests` 12/12 and `git diff --check`.
- [ ] T036 Create/update PR for #808 with local verification evidence and `GitHub Actions: not required`; squash-merge with `[skip ci]` after local-gated approval.
- [ ] T037 Verify PR is `MERGED`, issue #808 is `CLOSED`/`COMPLETED`, `main` fast-forwards, focused post-merge check passes, and the issue worktree/branch are cleaned up.

---

## Dependencies & Execution Order

- Phase 1 must complete before edits.
- Phase 2 tests must be written and observed failing before Phase 3/4 production implementation.
- Phase 3 MVP must pass before Phase 4 metadata/frontend fixture synchronization.
- Phase 5 docs/contracts work is conditional on actual docs/guidance drift, but the inspection/rationale is mandatory.
- Phase 6 requires all previous relevant phases and independent review.

## Parallel Opportunities

- T005-T010 can be drafted in parallel if they touch different test methods/files, but implementation must still respect RED/GREEN proof.
- T020-T021 can proceed after the command/write path exists.
- T022-T023 are conditional and can be skipped only with explicit rationale.

## Notes

- Keep #809 resident interactions and #817 umbrella closure out of this PR unless implementation accidentally satisfies a child acceptance criterion and that is verified separately.
- Do not implement Guardian social rules in React. React may render prompts/results and consume contract fixtures only.
- Avoid broad refactors of existing large C# files except for small helpers required by the tests.
