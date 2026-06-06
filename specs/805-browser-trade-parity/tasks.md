# Tasks: Browser Trade Parity (#805)

**Input:** `specs/805-browser-trade-parity/spec.md`, `specs/805-browser-trade-parity/plan.md`
**Source issue:** [#805](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/805)
**Parent epic:** [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)

## Format: `[ID] [P?] [Story] Description`

- **[P]** means the task can run in parallel after its prerequisites because it touches separate files or test slices.
- Stories map to independently testable scenarios in `spec.md`.

## Phase 1: Investigation and RED Tests

- [X] T001 [US1,US2,US3] Inspect console/service authority before editing: `ExplorerMode.Npcs.Trade.cs`, `ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs`, `ExplorerMode.Afterlife.GuardiansProjectsTrade.cs`, `NpcTradeService`, `ShiningTradeService`, `GuardianTradeService`, and their pending request state classes. Record in `plan.md` which operations are service-backed and which issue-body claims are unsupported by current console/service code.

- [X] T002 [US1] RED: add a focused C# browser test proving mortal NPC trade is not yet prompt-backed in the browser. The test should fail before implementation because `/npc_trade <npc_id>` or the chosen browser action is missing/blocked, then should pass when it returns a player-facing prompt/status result.

- [X] T003 [US1] RED: add focused C# write-service tests for NPC trade buy, sell, and buyback prompt submissions. The tests should fail before implementation because `BrowserMortalWorldWriteService` does not handle the command, then pass when it validates confirmation and delegates to `NpcTradeService`.

- [X] T004 [US2] RED: add focused C# browser tests proving Shining faction trade is not yet prompt-backed. Cover status/request inventory and at least one ready-stock buy submission through `BrowserAfterlifeWriteService`.

- [X] T005 [US3] RED: add focused C# browser tests proving Guardian trade is not yet prompt-backed. Cover status/request inventory and ready-stock buy/sell/buyback submissions through `BrowserAfterlifeWriteService` when existing `GuardianTradeService` authority supports those operations.

- [X] T006 [US4] RED: add/update browser command coverage/source-guard tests proving #805 trade commands/actions are represented explicitly and that the default browser audit no longer leaves trade hidden only under the `npcs` tracked follow-up. The guard should still keep unrelated #806–#816 gaps tracked.

## Phase 2: Mortal NPC Trade Implementation

- [X] T007 [US1] Add the browser-executable NPC trade command/action metadata in the command catalog and player command menu. Use player-facing labels and aliases; keep raw command/coverage diagnostics out of default UI.

- [X] T008 [US1] Implement C# prompt/result construction for mortal NPC trade. It must show trader identity, stock readiness/pending request state, buy/sell/buyback choices, prices, blockers, and confirmation prompts using safe `ExplorerCommandResult` blocks/prompts.

- [X] T009 [US1] Implement `BrowserMortalWorldWriteService` handling for NPC trade buy/sell/buyback. Validate command token, NPC id, operation, target slot/item/buyback id, required confirmation, money/price/readiness/blockers, and use `NpcTradeService` for mutations.

- [X] T010 [US1] Verify NPC trade GREEN: focused NPC trade tests pass; invalid submissions return player-facing validation errors and keep the prompt session open where appropriate.

## Phase 3: Shining Faction Trade Implementation

- [X] T011 [US2] Add browser-executable Shining faction trade command/action metadata. Keep it distinct from `shining_treasury`, Shining politics, Shining core actions, and relic forge work tracked by other issues.

- [X] T012 [US2] Implement C# prompt/result construction for Shining faction trade. It must show faction identity, trade tier, stock readiness, pending request state, available offers, blockers, and confirmation prompts using safe blocks/prompts.

- [X] T013 [US2] Implement `BrowserAfterlifeWriteService` handling for Shining trade request/prep and ready-stock buy using `ShiningTradeService.RequestInventoryAsync` and `ShiningTradeService.BuyAsync`. Include explicit confirmation, local-write safety, active GM-turn/pending cost blockers, and player-facing failure copy.

- [X] T014 [US2] Investigate Shining faction sell parity. If existing console/service authority supports a Shining sell operation, implement browser submission with tests. If not, record the authority-boundary finding in `spec.md`/`plan.md` and do not invent a browser-only sell mechanic.

- [X] T015 [US2] Verify Shining trade GREEN: focused Shining browser/write tests pass and any contract-sensitive docs tests pass if contract docs changed.

## Phase 4: Guardian Trade Implementation

- [X] T016 [US3] Add browser-executable Guardian trade command/action metadata. Keep it distinct from Guardian social/lore (#808), Guardian projects, and Guardian foundation/offering flows.

- [X] T017 [US3] Implement C# prompt/result construction for Guardian trade. It must show guardian identity/domain, reputation tier, trade stock readiness, pending request state, available buy/sell/buyback choices, blockers, and confirmation prompts using safe blocks/prompts.

- [X] T018 [US3] Implement `BrowserAfterlifeWriteService` handling for Guardian trade request/prep and ready-stock buy/sell/buyback using `GuardianTradeService`. Include explicit confirmation, local-write safety, canonical soul/guardian write checks, active GM-turn blockers, and player-facing failure copy.

- [X] T019 [US3] Verify Guardian trade GREEN: focused Guardian browser/write tests pass and contract-sensitive docs tests pass if any contract docs changed.

## Phase 5: Coverage, Frontend, Docs, and Spec Reconciliation

- [X] T020 [US4] Update browser command coverage metadata/fixtures/source guards so #805 trade coverage is explicit and unrelated gaps remain tracked under their own issues.

- [X] T021 [US4] If React components or frontend fixtures changed, run frontend TDD/guards and `npm run verify --prefix BookOfEternityClient.WebFrontend`. If React is unchanged because generic `CommandResultView`/`PromptForm` already renders the new C# prompts, record that in `plan.md`.

- [X] T022 [US1,US2,US3] Review docs/prompts/contracts impact. If pending/control contract shapes or GM closure rules changed, update `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and documentation coverage tests. If contracts are reused unchanged, record the no-docs rationale in `plan.md`.

- [X] T023 [P] Run Spec Kit prerequisite/discoverability check: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`. The active `FEATURE_DIR` must be `specs/805-browser-trade-parity`.

- [X] T024 [P] Run final local verification: focused trade/browser tests with counts, relevant build commands, frontend verify if applicable, docs coverage if applicable, `git diff --check origin/main...HEAD`, and added-line static security scan.

- [X] T025 Reconcile Spec Kit artifacts: update `spec.md`, `plan.md`, and this `tasks.md` with implementation record, RED/GREEN evidence, verification counts, contract/docs impact, and remaining risks. Mark checkboxes complete only after evidence exists.

- [X] T026 Commit one focused implementation with `[skip ci]` in the commit message. Hermes owns independent review, PR creation/merge, issue closure, and post-merge verification.

## Phase 6: Review Reconciliation

- [X] T027 Independent review found that Shining/Guardian trade service failures could surface raw local-write coordinator text such as `Browser-write` / `rollback` through default browser prompt-session results. Add regression coverage for failed Shining and Guardian prompt submissions through `ExplorerWebPromptSessionService.SubmitAsync`.

- [X] T028 Sanitize `BrowserAfterlifeWriteService` local-write failure copy so default player-facing browser results do not expose raw coordinator, rollback, snapshot, file-path, or debug framing while still preserving a useful failure reason and rollback-restored meaning.

- [X] T029 Re-run focused and affected local verification after review fixes, then amend the implementation commit before PR/re-review.

## Phase 7: Re-review Reconciliation

- [X] T030 Independent re-review found that trade prompt/status and submit failure copy could still display player-hostile diagnostic fragments such as `pending_*.json`, `canonical`, `contract`, `slotId`, `repair`, or `cleanup` in default browser surfaces. Add regressions for default prompt/status and prompt-session submit failure paths.

- [X] T031 Sanitize browser trade status text in default command results and extend afterlife write failure sanitization so Shining/Guardian trade blockers stay player-facing by default while preserving useful "GM verification needed" guidance.

- [X] T032 Re-run focused and affected gates after second review fixes, amend the implementation commit, and send the final committed diff through independent re-review before PR.

## Phase 8: Final Review Reconciliation

- [X] T033 Final review found that the afterlife write-service diagnostic sanitizer is global, so a trade-specific fallback would be wrong for non-trade afterlife actions. Replace it with a generic action fallback and add a non-trade Source of Light regression.

- [X] T034 Re-run frontend, C# affected, docs/afterlife, build, Spec Kit, diff, and player-facing static gates after the generic fallback fix before final re-review/PR.

## Out-of-Scope Guard

Do not implement #806–#816 or close #817 in this feature. If trade work reveals a separate missing inventory/social/politics/storage/archive action, keep it as evidence for the relevant existing child issue or create a tracked follow-up rather than broadening #805.

## Completion Evidence

RED:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserTradeParity" --logger "console;verbosity=minimal"` failed before implementation with 14 failed / 0 passed / 0 skipped / 14 total.

GREEN and final local verification:

- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserTradeParity" --logger "console;verbosity=minimal"` passed with 19 passed / 0 failed / 0 skipped / 19 total after final review reconciliation.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~BrowserAfterlifeWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Trade" --logger "console;verbosity=minimal"` passed with 273 passed / 0 failed / 0 skipped / 273 total after final review reconciliation.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"` passed with 98 passed / 0 failed / 0 skipped / 98 total after final review reconciliation.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` passed: TypeScript typecheck passed, player-facing tests passed, Vitest 27 passed / 0 failed, and Vite build succeeded.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` passed with 0 warnings / 0 errors.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` passed with 0 warnings / 0 errors.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned `FEATURE_DIR=E:\Games\worktrees\boe-805-browser-trade\specs\805-browser-trade-parity` and `AVAILABLE_DOCS=["tasks.md"]`.
- `git diff --check origin/main...HEAD` passed after review reconciliation.
- Refined player-facing surface static scan over production UI/frontend additions returned `NO_MATCHES` for raw coordinator/path/API/debug/default-copy hazards; write-service internals are covered by prompt-session regressions.

Review reconciliation:

- Initial independent review returned `CHANGES_REQUIRED` with one Important finding: Shining/Guardian trade service failures could expose raw `Browser-write` / `rollback` coordinator text through default browser prompt-session results.
- `BrowserAfterlifeWriteService` now sanitizes local-write failure copy like the mortal write surface.
- Added prompt-session regressions for Shining and Guardian trade failures; both verify no `Browser-write`, `rollback`, `snapshot`, `game_state/`, `pending_turn`, `pending_`, `.json`, `canonical`, `contract`, `slotId`, `repair`, `cleanup`, `raw`, `debug`, `api`, or `DTO` fragments reach default player-facing blocks/notifications and that soul state is restored.
- Independent re-review then returned `CHANGES_REQUIRED` with one Important finding: default trade prompt/status copy could still surface raw pending-contract diagnostics. `ExplorerLifecycleLocalTurnCommandResultBuilder` now sanitizes trade status text before browser display, and a Guardian duplicate-slot regression verifies the default prompt status stays player-facing.
- Final review returned one remaining Important finding: the afterlife write-service sanitizer is shared by non-trade afterlife actions, so the fallback must not mention trade. `BrowserAfterlifeWriteService` now uses generic action wording, and `TryApplyAsync_SourceOfLightMalformedPendingFailure_UsesGenericFailureCopyWithoutTradeLeak` covers non-trade diagnostics.
