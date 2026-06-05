# Implementation Plan: Browser Client Player UI Copy Boundary

**Source Issue**: [#738](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/738)
**Spec**: `specs/738-browser-player-copy/spec.md`
**Branch / Worktree**: `fix/738-player-ui-copy` at `E:/Games/worktrees/boe-738-player-ui-copy`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Technical Context

- Frontend workspace: `BookOfEternityClient.WebFrontend/` with React/Vite/TypeScript.
- Current Browser Client direction: minimalist top tabs, single command input, `/help` discovery, and explicit `Расширенный режим` for technical surfaces.
- C# remains gameplay/application authority. React may transform presentation and hide/sanitize default copy, but must not own gameplay rules.
- Existing player-copy helpers live in `BookOfEternityClient.WebFrontend/src/utils/playerCopy.ts`, `playerFacingCommandResult.ts`, and related command/result components.
- Relevant frontend components include `LoadingCard.tsx`, `ConnectionBanner.tsx`, `GameLauncher.tsx`, `tabBarConfig.ts`, `HelpView.tsx`, `CommandResultView.tsx`, `CommandResult.tsx`, `BlockRenderer.tsx`, `JsonTreeViewer.tsx`, `AudioPanel.tsx`, `SettingsView.tsx`, `SceneView.tsx`, `StatusView.tsx`, `TurnStatePanel.tsx`, `AdvancedDiagnostics.tsx`, `App.tsx`, and `hooks/useShellState.ts`.
- Relevant tests/guards include `BookOfEternityClient.WebFrontend/test/playerCopyRobustness.test.ts`, `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`, `BrowserApiContractTests.cs`, `LocalWebUiBuiltFrontendSmokeTests.cs`, and `LocalWebUiDocumentationTests.cs`.
- Browser UI docs/checklists live in `docs/web-ui/browser-parity-checklist.md`, `docs/web-ui/local-web-host.md`, and `BookOfEternityClient.WebFrontend/README.md`.

## Baseline Evidence

On clean `fix/738-player-ui-copy` from `origin/main` (`2757ef6`):

- `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded, 54 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; Vitest passed 23/23 and Vite build succeeded.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`: passed 53/53, 0 failed, 0 skipped; generated `TestResults/browser-smoke/` evidence.
- `git diff --check origin/main...HEAD`: passed; no implementation diff.
- Independent closure review returned `CHANGES_REQUIRED` for default-copy leaks, Help advanced-command exposure, insufficient guard coverage, and possible default command-result raw command/JSON exposure.

## Architecture

Keep this as a presentation-boundary hardening task. Add failing guards first for the confirmed #738 blockers, then minimally rewrite default Browser Client copy and filtering so normal player surfaces speak to the player while advanced diagnostics stay available only after opt-in. Prefer typed/helper-based filtering and sanitization over scattered string hacks.

Use two guard layers:

1. Frontend tests/source scans that assert default files/components do not contain or render banned implementation/meta terms.
2. C# source/documentation/built-smoke guards that catch default-vs-advanced drift and generate reviewable `TestResults/browser-smoke/*.html` artifacts.

Do not remove diagnostics or developer tools; move or keep them behind `Расширенный режим`, `details`, or explicit advanced surfaces. Do not change runtime contracts or GM-facing prompts unless investigation proves a real contract leak, which is not expected.

## Implementation Phases

1. **Investigation / root-cause confirmation**
   - Inspect each independent-review finding and identify the exact default-player code path.
   - Separate source/test/doc/comment/advanced diagnostics occurrences from actual default-player rendered strings.
   - Document any finding that is already safe with concrete code/test evidence before deciding not to change it.
2. **TDD RED - default copy guard**
   - Add or update a focused frontend test and/or C# source guard that fails on confirmed default-player meta copy in default files/components.
   - Include representative bad examples: `игрокоориентированный`, `player-facing`, `player-oriented`, `C# host`, raw `/api/`, `endpoint`, `DTO`, `debug shell`, `Raw validation details`, internal file-path explanations, and implementation-justification prose.
   - Scope exclusions so docs/tests/comments/source-guard definitions and `AdvancedDiagnostics` remain allowed.
3. **TDD RED - advanced filtering guard**
   - Add or update tests proving default Help/action surfaces filter advanced-only diagnostic commands and raw command coverage until advanced mode is enabled.
   - Include the review's command examples: `/help`, `/math`, `/gm`, `/debug`, `/mods`, `/system_guardians`, `/validate`.
4. **TDD RED - command-result sanitization guard**
   - Add or update tests proving default command/result rendering hides or sanitizes raw command strings, raw JSON blocks, endpoint/file/protocol details, and raw technical payloads unless advanced/details mode is explicit.
5. **GREEN - minimal copy/filter/sanitizer changes**
   - Rewrite default-player copy in confirmed files to Russian game-world/plain player wording.
   - Gate advanced Help/diagnostic command coverage behind explicit advanced mode or filter to player-default entries.
   - Ensure raw command/JSON result blocks are hidden, summarized, or only available through explicit advanced/details affordances in normal mode.
6. **Docs / guideline reconciliation**
   - Add or update a short copy boundary guideline/checklist: player UI speaks to the player; implementation comments stay in code/docs/advanced mode.
   - Mention #738 and the existing generated HTML visual smoke artifacts.
7. **Spec Kit / verification**
   - Update `tasks.md` with RED/GREEN evidence and final verification counts.
   - Run frontend verify, focused .NET browser/local web UI tests, client build, `git diff --check`, static scan, and Spec Kit prerequisite check.
   - Commit focused changes with `[skip ci]`.

## Risks and Constraints

- Do not weaken or delete advanced diagnostics; they are important repair tools and should remain explicitly opt-in.
- Do not over-ban technical terms globally; tests/docs/comments/source guard literals and advanced diagnostics may legitimately discuss implementation details.
- Do not change C# runtime contracts, afterlife/mortal contracts, validation/normalizer behavior, or GM docs unless a real runtime contract leak is found.
- Do not commit generated artifacts: `node_modules/`, `dist/`, `TestResults/`, `bin/`, `obj/`, Codex run files, or review scratch directories.
- Current UI direction is top tabs + single console-like command input; do not restore obsolete Feature-branch card-heavy criteria.

## Verification Plan

Minimum commands:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Also run:

```bash
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks
```

and an added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting, excluding generated/scratch artifacts and broad plan/spec prose. Run broader C# tests only if the implementation touches shared runtime behavior.

## Verification Evidence

Codex RED/GREEN/final local evidence for issue #738:

- RED: `npm --prefix BookOfEternityClient.WebFrontend exec -- vitest run test/playerCopyRobustness.test.ts` failed as expected with 3 failed / 17 passed / 20 total for default copy leaks, non-advanced command coverage loading, and raw command/raw JSON rendering. A later notification-specific RED run failed 1 failed / 19 passed / 20 total. `dotnet test ... --filter "FullyQualifiedName~BrowserDefaultPlayerCopy_SourceGuardBlocksImplementationFraming"` failed 1/1, and `dotnet test ... --filter "FullyQualifiedName~LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics"` failed 1/1 before docs were updated.
- GREEN: the focused frontend guard passed 20/20; the focused .NET source guard passed 1/1; the focused documentation guard passed 1/1.
- Final local verification before commit: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 2 files and 26/26 tests plus a successful Vite build; focused .NET browser/local web UI tests passed 54/54 with 0 failed and 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; working-tree `git diff --check` passed; added-line static security scan found no matches; Spec Kit prerequisite check resolved this feature directory; `TestResults/browser-smoke/` regenerated local/offline HTML and JSON evidence. Post-commit `git diff --check origin/main...HEAD` passed, and the committed added-line static security scan found no matches.
- Hermes reconciliation before PR: a broad local suite run exposed one stale `LocalWebUiHostTests.SaveLoadEndpoint_BlocksLoadWhenBrowserWriteIsBlocked` assertion that still expected `заблок...` after the intentional player-facing copy rewrite. The guard now asserts `Книга занята...`/`текущ...`; the exact test passed 1/1, the expanded focused browser/local gate passed 107/107, and the broad local suite passed 3381/3381.
- Independent review blocker fix for default command-result/prompt-session/audio copy:
  - RED: `npm --prefix BookOfEternityClient.WebFrontend exec -- vitest run test/playerCopyRobustness.test.ts` failed 3/21 after adding guards for prompt-session protocol copy, audio browser wording, raw command-result storage, and message-block sanitization. `dotnet test ... --filter "FullyQualifiedName~BrowserDefaultCommandAndAudioCopy_SourceGuardBlocksBackendTechnicalFraming"` failed 1/1 before backend copy changes.
  - GREEN/review-fix frontend: exact player-copy Vitest passed 21/21; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and a successful Vite build.
  - GREEN/review-fix .NET: focused browser/local gate with `BrowserFrontendWorkspaceTests|BrowserApiContractTests|LocalWebUiBuiltFrontendSmokeTests|LocalWebUiDocumentationTests|LocalWebUiHostTests` passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; exact stale-copy regressions passed 2/2; broad local suite passed 3382/3382, 0 failed, 0 skipped.
- Independent re-review blocker fix for Help/rawJson advanced boundary:
  - Re-review returned `CHANGES_REQUIRED` because default Help still rendered `/help` and raw JSON remained unavailable even after explicit advanced opt-in.
  - Default Help now renders player-facing labels in normal mode while preserving raw aliases only for advanced mode, and `BlockRenderer` passes `advancedEnabled` through nested blocks so `JsonTreeViewer` is restored only after explicit advanced opt-in.
  - Verification after these changes: focused frontend guards `playerCopyRobustness.test.ts` + `blockRenderer.test.ts` passed 24/24; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; broad local suite passed 3382/3382, 0 failed, 0 skipped; `git diff --check origin/main...HEAD` passed; Spec Kit prerequisite check resolved `specs/738-browser-player-copy`; refined added-line scan returned `NO_MATCHES`.
- Final settings-copy review blocker fix:
  - Second re-review returned one remaining Important finding: `SettingsView.tsx` rendered `settings.locality.safetySummary` whose backend/fixture payload still said `Браузерный клиент ... localhost/loopback...`.
  - The safety summary now uses player-facing local-device/save-progress wording; `BrowserApiContractTests`, the contract fixture, and source guards were extended to cover settings service/fixture leakage including `localhost/loopback`.
  - Verification after the settings summary fix: exact player-copy Vitest passed 21/21; focused .NET guard/contract filter passed 25/25; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; broad local suite passed 3382/3382, 0 failed, 0 skipped; `git diff --check origin/main...HEAD` passed; Spec Kit prerequisite check resolved `specs/738-browser-player-copy`; refined added-line scan returned `NO_MATCHES`.
- Final settings-session-label review blocker fix:
  - Third re-review returned one remaining Important finding: default Settings still displayed the raw `game_session` folder name through `locality.sessionLabel`.
  - `BrowserClientSettingsService` now emits a player-facing session label (`Текущая глава книги` / `Глава ещё не выбрана`) instead of deriving visible copy from `_fs.GameSessionPath`; fixtures and host/contract guards assert no default `game_session` label and ban the leaked `game_session — локальная папка книги` wording.
  - Current final verification after the session-label fix: exact player-copy Vitest passed 21/21; focused .NET guard/contract/settings-host filter passed 25/25; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; broad local suite passed 3382/3382, 0 failed, 0 skipped; `git diff --check origin/main...HEAD` passed; Spec Kit prerequisite check resolved `specs/738-browser-player-copy`; refined added-line scan returned `NO_MATCHES`.

Hermes owns final acceptance, independent review, PR/merge, and issue closure.
