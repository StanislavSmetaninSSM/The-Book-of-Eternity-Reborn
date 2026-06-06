# Tasks: Browser Inventory Management (#806)

**Input:** `specs/806-browser-inventory-management/spec.md`, `specs/806-browser-inventory-management/plan.md`
**Source issue:** [#806](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/806)
**Parent epic:** [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## Format: `[ID] [P?] [Story] Description`

- **[P]** means the task can run in parallel after prerequisites because it touches separate files or test slices.
- Stories map to independently testable scenarios in `spec.md`.
- Mark tasks complete only after implementation and verification evidence exist.

## Phase 1: Investigation and RED tests

- [X] T001 [US1,US2,US3] Inspect console inventory authority before production edits: `ExplorerMode.Inventory.cs` drop/split/merge helpers, inventory identity helpers, count/quantity handling, equipment clearing, and merge signature semantics. Record any extracted helper/service choice in `plan.md`.

- [X] T002 [US1] RED: add focused C# browser tests proving inventory drop is not yet prompt-backed in the browser. The test should fail before implementation because `/inventory_drop <item_id>` or the chosen browser action is missing/blocked, then pass when it returns a player-facing prompt/result requiring confirmation.

- [X] T003 [US1] RED: add focused C# write-service tests proving confirmed inventory drop removes the item and clears matching equipment, while missing confirmation/invalid item id/local-write blockers do not mutate state.

- [X] T004 [US2] RED: add focused C# browser tests proving stack split is not yet prompt-backed. Cover prompt bounds for `count` and/or `quantity`, invalid split amounts, and confirmed split creating a fresh stack entry.

- [X] T005 [US3] RED: add focused C# browser tests proving stack merge is not yet prompt-backed. Cover no-compatible-stack unavailable state and confirmed merge summing compatible stack counts while removing duplicates.

- [X] T006 [US4] RED: add/update browser command coverage/source-guard tests proving #806 drop/split/merge commands/actions are represented explicitly and that the default browser audit no longer leaves stack-management hidden only under the generic `inventory` tracked follow-up. The guard should still keep unrelated #807–#816 gaps tracked.

## Phase 2: Shared inventory mutation authority

- [X] T007 [US1,US2,US3] Extract or add focused C# inventory mutation helper/service logic for drop, split, and merge if needed. Preserve console semantics: stable item identity matching, equipment reference clearing on drop, `count`/`quantity` field preservation, fresh split identity assignment, and merge signature compatibility.

- [X] T008 [US1,US2,US3] If console `ExplorerMode.Inventory.cs` is changed to use the helper, keep the change scoped and add/keep tests proving existing console behavior is not weakened. If console remains unchanged, document why helper logic is behavior-equivalent in `plan.md`.

## Phase 3: Browser command/action metadata

- [X] T009 [US1] Add browser-executable inventory drop command/action metadata in `ExplorerCommandCatalog`, `/help` if applicable, and `BrowserPlayerCommandMenuBuilder`. Use Russian player-facing labels and aliases; keep raw command/coverage diagnostics out of default UI.

- [X] T010 [US2] Add browser-executable inventory split command/action metadata. It must be local-turn/mutating, accept an item argument, and surface as a safe guided form rather than a React-only handler.

- [X] T011 [US3] Add browser-executable inventory merge command/action metadata. It must be local-turn/mutating, accept an item argument, and stay distinct from storage/transport work tracked by #814.

## Phase 4: Prompt/result construction

- [X] T012 [US1] Implement C# prompt/result construction for inventory drop. It must show item name, stack/equipment context, confirmation prompt, and player-facing unavailable/error states.

- [X] T013 [US2] Implement C# prompt/result construction for stack split. It must show current stack count, input bounds, split quantity prompt, confirmation prompt, and unavailable state for non-stack/count-1 items.

- [X] T014 [US3] Implement C# prompt/result construction for stack merge. It must show compatible stack summary, confirmation prompt, and unavailable state when no compatible stack exists.

- [X] T015 [US1,US2,US3] Add player-facing sanitization/source guards for default inventory-management prompt/result text. Default blocks/notifications must not expose raw `game_state/`, `.json`, `api`, `DTO`, `endpoint`, `protocol`, `debug`, `raw`, `slotId`, `contract`, `canonical`, `repair`, or internal identity implementation details beyond the selected item id needed for command targeting.

## Phase 5: Write-service implementation

- [X] T016 [US1] Implement `BrowserMortalWorldWriteService` handling for confirmed inventory drop. Validate command token, item identity, confirmation, local-write state, and mutate through the shared C# inventory authority.

- [X] T017 [US2] Implement `BrowserMortalWorldWriteService` handling for confirmed stack split. Validate item identity, split quantity, stack bounds, confirmation, local-write state, and mutate through the shared C# inventory authority.

- [X] T018 [US3] Implement `BrowserMortalWorldWriteService` handling for confirmed stack merge. Validate item identity, compatible stack availability, confirmation, local-write state, and mutate through the shared C# inventory authority.

- [X] T019 [US1,US2,US3] Verify GREEN for focused browser inventory tests and ensure failed submissions keep useful player-facing errors without leaking technical diagnostics.

## Phase 6: Coverage, frontend, docs, and Spec Kit reconciliation

- [X] T020 [US4] Update browser command coverage metadata/fixtures/source guards so #806 coverage is explicit and the generic `inventory` row no longer reports stack-management as an open gap after the new commands exist.

- [X] T021 [US4] If React components, TypeScript contracts, or frontend fixtures changed, run frontend tests/guards and `npm run verify --prefix BookOfEternityClient.WebFrontend`. If React is unchanged because generic `CommandResultView`/`PromptForm` already renders the new C# prompts, record that in `plan.md`.

- [X] T022 [US1,US2,US3] Review docs/prompts/contracts impact. If inventory JSON schema, GM-facing guidance, validation, normalizer, pending/control, response field, receipt/report, or lifecycle authority changed, update the relevant docs/examples/tests. If existing client-owned inventory shape is reused unchanged, record the no-docs rationale in `plan.md`.

- [X] T023 [P] Run Spec Kit prerequisite/discoverability check: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`. The active `FEATURE_DIR` must be `specs/806-browser-inventory-management`.

- [X] T024 [P] Run final local verification: focused browser inventory tests with counts, affected browser/write/command suites, relevant build commands, frontend verify if applicable, docs coverage if applicable, `git diff --check origin/main...HEAD`, and added-line static security scan.

- [X] T025 Reconcile Spec Kit artifacts: update `spec.md`, `plan.md`, and this `tasks.md` with implementation record, RED/GREEN evidence, verification counts, contract/docs impact, review findings, and remaining risks. Mark checkboxes complete only after evidence exists.

- [X] T026 Commit one focused implementation with `[skip ci]` in the commit message. Hermes owns independent review, PR creation/merge, issue closure, and post-merge verification.

## Out-of-Scope Guard

Do not implement #807–#816 or close #817 in this feature. If inventory management work reveals a separate missing storage/social/politics/archive action, keep it as evidence for the relevant existing child issue or create a tracked follow-up rather than broadening #806.

## Completion Evidence

RED:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"` failed before implementation with 12 failed / 0 passed / 0 skipped / 12 total. Failures were the expected missing browser command/prompt/write/coverage support for `/inventory_drop`, `/inventory_split`, and `/inventory_merge`.

GREEN and final local verification:

- Focused GREEN: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"` passed with 12 passed / 0 failed / 0 skipped / 12 total.
- Affected C# suite: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"` passed with 88 passed / 0 failed / 0 skipped / 88 total.
- Builds: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` both completed with 0 warnings / 0 errors.
- Spec Kit prerequisite check resolved `FEATURE_DIR` to `E:\Games\worktrees\boe-806-browser-inventory-management\specs\806-browser-inventory-management`.
- Whitespace check: `git diff --check` exited 0; Git printed CRLF conversion warnings only.
- Added-line static scan over production `BookOfEternityClient` additions, excluding tests/spec docs, found no forbidden default player-facing diagnostic terms.
- Frontend verification was not run because no React, TypeScript contract, generated fixture, or frontend file changed.
- Documentation coverage was not run because no GM-facing docs, afterlife contract, validation/normalizer contract, pending/control file, response field, receipt/report, or inventory schema changed.
