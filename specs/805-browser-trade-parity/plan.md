# Implementation Plan: Browser Trade Parity (#805)

**Source issue:** [#805](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/805)
**Parent epic:** [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Spec:** `specs/805-browser-trade-parity/spec.md`
**Branch/worktree:** `fix/805-browser-trade` in `E:/Games/worktrees/boe-805-browser-trade`

## Architecture

Browser trade parity will extend the existing C# command/prompt-session pipeline rather than adding React gameplay logic. New or updated command descriptors should route through `ExplorerWebCommandService` to C# result/prompt builders. Prompt submission should flow through `ExplorerWebPromptSessionService` into `BrowserMortalWorldWriteService` or `BrowserAfterlifeWriteService`, which then call authoritative trade services and local-write coordination.

The implementation should stay split by authority boundary:

- **Command/catalog layer:** `ExplorerCommandCatalog`, browser command coverage, action metadata, source guards, and `/help`/player action visibility.
- **Prompt/result construction:** C# browser command result builder/helper code that reads current trade views and creates `UiPrompt`/`UiMessageBlock`/safe data blocks for NPC, Shining faction, and Guardian trade.
- **Write layer:** `BrowserMortalWorldWriteService` for mortal NPC trade; `BrowserAfterlifeWriteService` for Shining faction and Guardian trade. These should delegate mutations to `NpcTradeService`, `ShiningTradeService`, and `GuardianTradeService`.
- **Frontend layer:** React should remain generic. Change TypeScript only if existing `PromptForm`/`CommandResultView` cannot render the new safe prompt/result blocks.
- **Docs/contracts:** If no pending/control contract shape changes, GM-facing contract docs do not need content changes. If any contract shape or GM closure guidance changes, update the matrix/examples/tests before closure.

## Files Expected to Change

Likely C# source changes:

- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs` — add/adjust dedicated browser-executable trade descriptors or subcommands.
- `BookOfEternityClient/WebUi/ExplorerWebCommandService.cs` and/or a focused helper under `BookOfEternityClient/WebUi/` — build player-facing trade command results and prompts.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs` — include any new mutating trade commands in local UI lock requirements.
- `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs` — handle NPC trade prompt submissions.
- `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs` — handle Shining faction and Guardian trade prompt submissions.
- `BookOfEternityClient/WebUi/BrowserCommandCoverageService.cs` and `BrowserPlayerCommandMenuBuilder.cs` if command coverage/action metadata requires explicit updates.
- Possibly `BookOfEternityClient/Program.cs` if dependency injection must pass existing trade services into browser write/result services.

Likely tests/fixtures:

- `BookOfEternityClient.Tests/WebUi/BrowserAfterlifeWriteServiceTests.cs` — Shining/Guardian trade submission tests.
- New or existing `BookOfEternityClient.Tests/WebUi/BrowserMortalWorldWriteServiceTests.cs` — NPC trade submission tests.
- Browser command coverage/source guard tests covering `ExplorerCommandCatalog`, `BrowserCommandCoverageService`, and player-facing metadata.
- Browser prompt/session tests covering interactive form attachment and submit/cancel where needed.
- Frontend tests under `BookOfEternityClient.WebFrontend/test/` only if React rendering changes.

Spec Kit artifacts:

- `specs/805-browser-trade-parity/spec.md`
- `specs/805-browser-trade-parity/plan.md`
- `specs/805-browser-trade-parity/tasks.md`

## Technical Constraints

- Preserve C# as gameplay/application authority.
- Preserve current Browser Client direction: minimalist shell, top tabs, single command/composer input, `/help` discovery.
- Do not expose raw slash-command audit framing, DTO/API/protocol wording, raw JSON, or file paths in default player UI.
- Do not create new trade economy mechanics or pricing logic.
- Do not broaden into inventory stack management (#806), NPC conversations (#807), Guardian social/lore (#808), resident actions (#809), Shining politics/actions (#810/#811), incarnation gates (#812), relic forge (#813), storage/transport (#814), Ink Feather fate work (#815), or archive actions (#816).
- Treat afterlife/Chaos Sea/Shining Abode pending/control surfaces as contract-sensitive. Updating contract docs/tests is mandatory if their shape or closure rules change.
- Use `[skip ci]` in commits; GitHub Actions are not required for this local-gated workflow.

## Test Strategy

1. RED tests/source guards before production changes for each surface:
   - NPC trade command/prompt exists and is blocked before implementation.
   - Shining trade command/prompt/request/buy path exists and is blocked before implementation.
   - Guardian trade command/prompt/request/buy/sell/buyback path exists and is blocked before implementation.
   - Command coverage no longer reports #805 as an unresolved trade gap after implementation.
2. GREEN implementation with the smallest C# changes that reuse trade services.
3. Focused verification for trade/write/prompt/coverage.
4. Frontend verify only if frontend files or built frontend fixtures changed.
5. Contract/docs verification if runtime pending/control or GM-facing guidance changes.
6. Independent review before PR/merge.

## Baseline Evidence

Hermes will provide pre-delegation baseline command results in the Codex prompt. Final RED/GREEN and verification evidence must be appended here before the feature is accepted.

## Implementation Evidence

Authority findings recorded before production edits:

- Mortal NPC trade is service-backed for request/prep, buy, sell, and buyback through `NpcTradeService` and `NpcTradeRequestState`.
- Shining faction trade is service-backed for request/prep and ready-stock buy through `ShiningTradeService` and `ShiningTradeRequestState`.
- Shining faction sell is not supported by current console/service authority: `ExplorerMode.Afterlife.ShiningAbode.TradeAndForge.cs` exposes Shining request/buy paths only, and `ShiningTradeService` has no sell operation. Browser parity therefore documents/rejects Shining sell rather than adding a React-only mechanic.
- Guardian trade is service-backed for request/prep, buy, sell, and buyback through `GuardianTradeService` and `GuardianTradeRequestState`.

Implemented files:

- `ExplorerCommandCatalog.cs`, `ExplorerHelpCommandResultBuilder.cs`, `BrowserPlayerCommandMenuBuilder.cs`, and `BrowserCommandCoverageService.cs` expose and audit `/npc_trade`, `/shining_trade`, and `/guardian_trade`.
- `ExplorerLifecycleLocalTurnCommandResultBuilder.cs` builds player-facing trade prompt/status blocks with local-turn gating and no default raw JSON blocks.
- `BrowserMortalWorldWriteService.cs` handles NPC request/buy/sell/buyback through `NpcTradeService` under `BrowserLocalWriteCoordinator`.
- `BrowserAfterlifeWriteService.cs` handles Shining request/buy and Guardian request/buy/sell/buyback through existing afterlife trade services under `BrowserLocalWriteCoordinator`.
- `NpcTradeService.cs` gained an optional non-mutating inventory-read path so opening the browser prompt does not create a pending request before confirmation; existing callers keep the previous default behavior.
- `BookOfEternityClient.Tests/WebUi/BrowserTradeParityTests.cs` covers prompt, write, authority-boundary, coverage, and player-facing/raw-wording expectations.

Verification evidence collected during implementation:

- RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserTradeParity" --logger "console;verbosity=minimal"` failed with 14 failed / 0 passed / 0 skipped / 14 total before implementation.
- GREEN: the same focused filter passed with 14 passed / 0 failed / 0 skipped / 14 total.
- Broader focused C# verification passed: 268 passed / 0 failed / 0 skipped / 268 total for `FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~BrowserAfterlifeWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Trade`.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` passed with 0 warnings / 0 errors.
- `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal` passed with 0 warnings / 0 errors.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` passed and returned the active `specs/805-browser-trade-parity` feature directory.
- `git diff --check` passed before commit.
- Added-line static scan over code/tests found no real hardcoded secret, shell injection, eval/exec, unsafe deserialization, or SQL string-formatting issues.

Frontend impact:

- No React source or frontend fixtures changed. Existing generic `CommandResultView`/`PromptForm` support the new C# command result blocks and prompt types, so `npm run verify --prefix BookOfEternityClient.WebFrontend` was not required by the implementation delta; Hermes still ran it as a final browser-local gate after review reconciliation.

Docs/contracts impact:

- No GM-facing contract docs/examples changed because the implementation reuses existing `NpcTradeRequestState`, `ShiningTradeRequestState`, and `GuardianTradeRequestState` pending/control contracts and does not alter action types, response fields, receipts, validation rules, normalizer effects, or authority paths.

## Closure Gates

Before PR/merge, all of the following must be true:

- #805 acceptance criteria in `spec.md` are mapped to changed code/tests or to a documented authority-boundary finding.
- `tasks.md` checkboxes are marked complete only where implementation and verification evidence exist.
- No accidental run artifacts (`prompt.md`, `final.md`, `events.jsonl`, `stderr.log`, `exit-code.txt`, `run-codex.sh`) appear in the repo diff.
- `git diff --check origin/main...HEAD` passes.
- Added-line static scan has no real secret/injection/eval/deserialization/SQL hazards.
- Independent review returns no Critical/Important blockers.
- Local verification has exact pass/fail/skip counts.

## Initial Plan Self-Review

- Spec coverage: the plan covers mortal NPC trade, Shining faction trade, Guardian trade, command coverage, local-write safety, and docs/contract boundaries from #805/#817.
- Scope check: #805 is broad but still one trade-parity closure unit because the issue explicitly groups NPC, Shining faction, and Guardian trade. Other interactive parity child issues remain out of scope.
- Contract check: no contract shape change is planned; if Codex discovers one is required, it must update Spec Kit artifacts and GM-facing documentation/tests before closure.
- Placeholder scan: no unspecified tasks are required for the handoff; Codex must fill implementation evidence, not invent new acceptance criteria.
