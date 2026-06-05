# Tasks: Browser Client Player UI Copy Boundary

**Source Issue**: [#738](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/738)
**Spec**: `specs/738-browser-player-copy/spec.md`
**Plan**: `specs/738-browser-player-copy/plan.md`

## Phase 1: Exploration and Baseline

- [X] Inspect `AGENTS.md` and `.specify/memory/constitution.md` for issue/spec/browser player-copy guardrails.
- [X] Inspect issue #738 body and closure-related child issues/comments.
- [X] Confirm related audit/follow-up issues #689, #739, #741, #743, #745, and #746 are closed.
- [X] Create isolated worktree `E:/Games/worktrees/boe-738-player-ui-copy` on `fix/738-player-ui-copy` from `origin/main`.
- [X] Run baseline frontend verification: `npm ci` succeeded with 0 vulnerabilities; `npm run verify` succeeded with Vitest 23/23 and Vite build.
- [X] Run baseline focused .NET browser/local web UI tests: focused BrowserFrontendWorkspace/BrowserApiContract/LocalWebUiBuiltFrontendSmoke/LocalWebUiDocumentation filter passed 53/53 and generated `TestResults/browser-smoke/*` artifacts.
- [X] Run independent closure review; review returned `CHANGES_REQUIRED` with blockers for default copy leaks, Help advanced-command exposure, missing scoped guard, and command-result raw command/JSON risk.

## Phase 2: Regression / Source Guards (TDD RED)

- [X] Add or update a scoped default-player copy source guard covering Browser Client default source files/components.
- [X] Ensure the guard fails before implementation on confirmed default-player leak examples from the independent review.
- [X] Include banned terms/patterns for default UI: `игрокоориентированный`, `player-facing`, `player-oriented`, `C# host`, raw `/api/`, `endpoint`, `DTO`, `debug shell`, `Raw validation details`, internal file paths, and implementation-justification prose.
- [X] Explicitly allow the same terms in docs, tests, comments, source-guard definitions, and `AdvancedDiagnostics` / explicit advanced surfaces.
- [X] Add or update Help/action-menu tests proving advanced-only commands and raw command coverage are not shown by default.
- [X] Add or update command-result tests proving default result flow sanitizes or hides raw command strings, raw JSON blocks, endpoint/file/protocol details, and raw technical payloads.
- [X] Run the focused failing test command(s) and record RED failure output/counts in this file.

## Phase 3: Minimal Implementation (GREEN)

- [X] Rewrite confirmed default copy leaks in `LoadingCard.tsx`, `GameLauncher.tsx`, `tabBarConfig.ts`, `ConnectionBanner.tsx`, `App.tsx`, `useShellState.ts`, and any other default source confirmed by the new guard.
- [X] Keep wording Russian-first and player-readable: describe the book/session/scene/action/state rather than browser/client implementation internals.
- [X] Gate or filter default Help/action command coverage so advanced-only diagnostic commands remain behind explicit `Расширенный режим`.
- [X] Keep advanced diagnostics reachable after opt-in; do not delete `AdvancedDiagnostics` or repair/details surfaces.
- [X] Update command-result rendering/sanitization so normal flow does not expose raw command strings/raw JSON/endpoint/file/protocol details without explicit advanced/details affordance.
- [X] Preserve C# runtime authority and avoid new gameplay logic in React.

## Phase 4: Docs / Spec Reconciliation

- [X] Add or update a concise browser UI copy guideline/checklist in `docs/web-ui/browser-parity-checklist.md`, `docs/web-ui/local-web-host.md`, and/or `BookOfEternityClient.WebFrontend/README.md`.
- [X] The guideline must state the #738 boundary in substance: player UI speaks to the player; implementation comments stay in code/docs/advanced mode.
- [X] Document that generated HTML smoke artifacts are local/offline evidence and not committed screenshots.
- [X] Reconcile `spec.md`, `plan.md`, and this `tasks.md` if implementation changes accepted scope or discovers runtime/GM-facing impact.

## Phase 5: Verification and Review Prep

- [X] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact output/counts.
- [X] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"` and record exact pass/fail/skip counts.
- [X] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and record warnings/errors.
- [X] Run `git diff --check` before commit and `git diff --check origin/main...HEAD` after commit.
- [X] Run added-line static security scan excluding generated/scratch artifacts and broad plan/spec prose.
- [X] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/738-browser-player-copy`.
- [X] Confirm generated visual smoke artifacts exist under `TestResults/browser-smoke/`, especially default/player copy evidence relevant to #738.
- [X] Commit focused changes with `[skip ci]`.
- [ ] Obtain independent review before PR/merge; fix Critical/Important findings. Hermes owns final review/PR/merge reconciliation for this delegated slice.
- [ ] Create PR, squash-merge after local verification, and verify issue #738 closes. Hermes owns final PR/merge/issue closure.

## Verification Evidence

- Baseline before implementation:
  - `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded, 54 packages, 0 vulnerabilities.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; Vitest passed 23/23 and Vite build succeeded.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`: passed 53/53, 0 failed, 0 skipped.
  - `TestResults/browser-smoke/` generated `first-screen-visual-qa.html`, `reborn-panels.html`, `start-new-chapter-flow.html`, `detail-surfaces.html`, `navigation-ia.html`, `main-menu-background-art.html`, `root.html`, `game-route.html`, and JSON/network evidence.
  - `git diff --check origin/main...HEAD`: passed on the clean branch.
  - Added-line static scan: no matches before implementation because there was no code diff.
  - Independent closure review: `CHANGES_REQUIRED`; see `spec.md` for blocker summary.
- TDD RED after adding guards:
  - `npm --prefix BookOfEternityClient.WebFrontend exec -- vitest run test/playerCopyRobustness.test.ts`: failed as expected; 3 failed, 17 passed, 20 total. Failures covered default copy leaks (18 reported matches), non-advanced command coverage loading, and raw command/raw JSON result rendering.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserDefaultPlayerCopy_SourceGuardBlocksImplementationFraming" --logger "console;verbosity=minimal"`: failed as expected; 1 failed, 0 passed, 1 total.
  - `npm --prefix BookOfEternityClient.WebFrontend exec -- vitest run test/playerCopyRobustness.test.ts` after adding notification guard: failed as expected; 1 failed, 19 passed, 20 total, proving notifications still rendered raw title/message.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics" --logger "console;verbosity=minimal"`: failed as expected; 1 failed, 0 passed, 1 total, proving #738 checklist text was missing.
- Focused GREEN evidence:
  - `npm --prefix BookOfEternityClient.WebFrontend exec -- vitest run test/playerCopyRobustness.test.ts`: passed; 1 file, 20/20 tests.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserDefaultPlayerCopy_SourceGuardBlocksImplementationFraming" --logger "console;verbosity=minimal"`: passed; 1/1 tests.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~LocalWebHostDocs_SeparatePlayerDefaultFromAdvancedDiagnostics" --logger "console;verbosity=minimal"`: passed; 1/1 tests.
- GREEN/final local evidence:
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`: passed. TypeScript typecheck passed; player-facing tests passed with Vitest 2 files and 26/26 tests; Vite production build succeeded.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"`: passed 54/54, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: succeeded with 0 warnings and 0 errors.
  - `git diff --check`: passed before commit for working-tree changes.
  - `git diff --check origin/main...HEAD`: passed after the focused `[skip ci]` commit.
  - Added-line static security scan over working-tree and committed diffs, excluding `specs/**`, generated `dist/`, `TestResults/`, `bin/`, and `obj/`: no matches for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, or SQL string formatting.
  - `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`: resolved `FEATURE_DIR=E:\Games\worktrees\boe-738-player-ui-copy\specs\738-browser-player-copy`, `AVAILABLE_DOCS=["tasks.md"]`.
  - `TestResults/browser-smoke/` regenerated local/offline evidence: `detail-surfaces.html`, `first-screen-visual-qa.html`, `game-route.html`, `game-screen.json`, `main-menu-background-art.html`, `main-menu.json`, `navigation-ia.html`, `network.json`, `reborn-panels.html`, `root.html`, `session.json`, and `start-new-chapter-flow.html`.
  - Focused commit created with message `fix: harden browser player copy boundary [skip ci]`; Hermes-owned independent review, PR/merge, and issue closure remain open.
- Hermes reconciliation evidence before PR:
  - Initial broad suite exposed one stale assertion in `LocalWebUiHostTests.SaveLoadEndpoint_BlocksLoadWhenBrowserWriteIsBlocked`: the production copy intentionally changed from `заблок...` to player-facing `Книга занята текущим ходом...`; the guard now asserts the new player-facing busy wording instead of the old technical/blocking term.
  - Exact regression after the guard update: `dotnet test ... --filter "FullyQualifiedName~SaveLoadEndpoint_BlocksLoadWhenBrowserWriteIsBlocked"` passed 1/1.
  - Expanded focused browser/local gate including `LocalWebUiHostTests` passed 107/107, 0 failed, 0 skipped.
  - Broad local suite passed 3381/3381, 0 failed, 0 skipped.
- Independent review blocker fix evidence:
  - RED: `npm --prefix BookOfEternityClient.WebFrontend exec -- vitest run test/playerCopyRobustness.test.ts` failed as expected; 3 failed, 18 passed, 21 total. Failures covered prompt-session protocol copy, default audio browser wording, raw command-result storage, prompt-session local result storage, and message-block sanitization. `dotnet test ... --filter "FullyQualifiedName~BrowserDefaultCommandAndAudioCopy_SourceGuardBlocksBackendTechnicalFraming"` failed as expected; 1 failed, 0 passed, 1 total.
  - Focused GREEN: exact player-copy Vitest passed 21/21; backend command/audio source guard passed 1/1; existing default-player source guard passed 1/1.
  - Review-fix verification: `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with TypeScript checks, player-facing tests, Vitest 27/27, and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; exact stale assertion regressions passed 2/2; broad local suite passed 3382/3382, 0 failed, 0 skipped.
- Re-review blocker fix evidence:
  - Independent re-review returned two Important findings: default Help still rendered `/help`, and `rawJson` blocks stayed hidden even after explicit advanced opt-in.
  - Fix: default Help now renders player-facing labels while keeping raw command aliases for advanced mode only; `BlockRenderer` keeps raw JSON hidden by default and restores `JsonTreeViewer` only when `advancedEnabled` is true.
  - Verification after those fixes: focused frontend guards `playerCopyRobustness.test.ts` + `blockRenderer.test.ts` passed 24/24; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; broad local suite passed 3382/3382, 0 failed, 0 skipped; `git diff --check origin/main...HEAD` passed; Spec Kit prerequisite check resolved `FEATURE_DIR=E:\Games\worktrees\boe-738-player-ui-copy\specs\738-browser-player-copy`, `AVAILABLE_DOCS=["tasks.md"]`; added-line static security scan excluding spec prose returned `NO_MATCHES`.
- Final settings-copy review blocker fix evidence:
  - Second re-review returned one Important finding: default Settings rendered `settings.locality.safetySummary` from `BrowserClientSettingsService.cs`/`client-settings.json` with browser-client and `localhost/loopback` implementation wording.
  - Fix: the settings safety summary now says `Книга открыта только на этом устройстве и хранит настройки вместе с вашим прохождением.`, fixtures/contract expectations match it, and source guards now include `SettingsView.tsx`, `BrowserClientSettingsService.cs`, and `client-settings.json` with a `localhost/loopback` default-copy ban.
  - Verification after the settings summary fix: exact `playerCopyRobustness.test.ts` passed 21/21; focused .NET guard/contract filter `BrowserDefaultPlayerCopy_SourceGuardBlocksImplementationFraming|BrowserApiContractTests` passed 25/25; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; broad local suite passed 3382/3382, 0 failed, 0 skipped; `git diff --check origin/main...HEAD` passed; Spec Kit prerequisite check resolved `FEATURE_DIR=E:\Games\worktrees\boe-738-player-ui-copy\specs\738-browser-player-copy`, `AVAILABLE_DOCS=["tasks.md"]`; added-line static security scan excluding spec prose returned `NO_MATCHES`.
- Final settings-session-label review blocker fix evidence:
  - Third re-review returned one Important finding: default Settings still rendered the internal `game_session` folder name through `locality.sessionLabel`.
  - Fix: `BrowserClientSettingsService` now exposes a player-facing session label (`Текущая глава книги` / `Глава ещё не выбрана`) rather than deriving a visible label from `_fs.GameSessionPath`; fixtures and host/contract tests assert no visible `game_session` session label, and source guards reject the leaked `game_session — локальная папка книги` wording.
  - Current verification after the session-label fix: exact `playerCopyRobustness.test.ts` passed 21/21; focused .NET guard/contract/settings-host filter passed 25/25; `npm run verify --prefix BookOfEternityClient.WebFrontend` passed with Vitest 27/27 and successful Vite build; focused browser/local .NET gate passed 108/108, 0 failed, 0 skipped; `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors; broad local suite passed 3382/3382, 0 failed, 0 skipped; `git diff --check origin/main...HEAD` passed; Spec Kit prerequisite check resolved `FEATURE_DIR=E:\Games\worktrees\boe-738-player-ui-copy\specs\738-browser-player-copy`, `AVAILABLE_DOCS=["tasks.md"]`; added-line static security scan excluding spec prose returned `NO_MATCHES`.
