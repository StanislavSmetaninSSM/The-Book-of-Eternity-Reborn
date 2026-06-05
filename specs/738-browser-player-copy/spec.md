# Feature Specification: Browser Client Player UI Copy Boundary

**Feature Branch**: `fix/738-player-ui-copy`
**Created**: 2026-06-06
**Status**: Planned; implementation delegated to Codex from autonomous worker
**Source Issue**: [#738 [Browser Client] Убрать технические термины и meta-комментарии из player UI](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/738)
**Related Issues / Evidence**: [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680), [#689](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/689), [#739](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/739), [#741](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/741), [#743](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/743), [#745](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/745), [#746](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/746)

## Current-State Finding

Several child audit/follow-up issues have already improved the Browser Client player-vs-advanced boundary, but independent closure review for #738 still found unresolved default-player copy and guard gaps on current `main`. The exact bad phrase style `Сводка Обители остаётся игрокоориентированной...` is gone, and visual smoke artifacts exist, but normal player surfaces can still mention browser/client implementation framing or expose advanced command diagnostics too early.

## User Stories & Testing

### User Story 1 - Ordinary player sees game-world/plain copy first (Priority: P1)

A normal player opening the Browser Client or moving through default tabs sees Russian game-client wording that describes the book, scene, state, status, help, settings, audio, and actions without being told about implementation concepts such as browser/client internals, endpoints, DTOs, debug shells, raw command coverage, raw JSON, or source files.

**Independent Test**: A focused source guard scans default-player frontend source sections and representative generated visual artifacts for banned technical/meta wording while explicitly excluding comments, tests, docs, and advanced diagnostics surfaces.

**Acceptance Scenarios**:

1. **Given** the Browser Client is loading, **When** the loading/connection/error surfaces appear, **Then** the main message uses player-readable wording and technical details are hidden behind explicit details/advanced affordances.
2. **Given** the launcher/home/shell default route is shown, **When** no active session exists, **Then** the copy reads as a game launcher/no-session state rather than a broken local client or implementation dashboard.
3. **Given** Reborn/Afterlife/Shining Abode/Chaos Sea panels are visible by default, **When** the player reads summaries, **Then** they describe the world state/actions and do not justify implementation intent or mention internal files.

### User Story 2 - Advanced diagnostics stay opt-in (Priority: P1)

A player can discover ordinary `/help` guidance without default exposure to raw command/API/validation/debug tools. Direct diagnostic commands, raw coverage, validation internals, JSON trees, endpoint descriptions, and similar repair aids remain available only after explicit `Расширенный режим` opt-in or in a clearly labeled details disclosure.

**Independent Test**: Help/action/menu tests prove advanced-only command coverage and raw command details are filtered from default player mode and remain reachable after advanced mode is enabled.

### User Story 3 - Future copy drift is blocked (Priority: P1)

A future agent editing Browser Client strings gets a failing guard if default UI reintroduces meta labels such as `player-facing`, `игрокоориентированный`, raw `/api/`, endpoint/DTO/debug wording, file-path explanations, or implementation-justification prose.

**Independent Test**: Source guard and frontend tests fail on representative bad strings in default-player files but allow the same terms in tests, docs, comments, source guard definitions, and `AdvancedDiagnostics`/explicit advanced surfaces.

### User Story 4 - Closure evidence remains reviewable (Priority: P2)

A maintainer can inspect a generated offline HTML smoke artifact for default-player surfaces and a short browser UI copy guideline/checklist that states the boundary clearly.

**Independent Test**: Built-frontend smoke tests generate `TestResults/browser-smoke/*.html` artifacts and documentation/source guards assert the guideline/checklist exists.

## Requirements

### Functional Requirements

- **FR-001**: Default Browser Client source and representative generated artifacts MUST NOT contain player-visible implementation/meta terms such as `player-facing`, `player-oriented`, `игрокоориентированный`, `C# host`, `DTO`, raw `/api/`, `endpoint`, `debug shell`, `Raw validation details`, internal file paths, or implementation-justification copy.
- **FR-002**: Default loading, connection, no-session, launcher, tab, status, help, settings, audio, action, prompt, command-result, and Reborn panel surfaces MUST use Russian game-world or plain player-readable wording.
- **FR-003**: Advanced/technical diagnostics MUST remain accessible through explicit `Расширенный режим`, details disclosures, or clearly advanced routes; normal player paths MUST NOT auto-open them.
- **FR-004**: Default Help/action menus MUST filter advanced-only commands and diagnostic coverage unless advanced mode is enabled.
- **FR-005**: Command/result rendering in default mode MUST sanitize or hide raw command strings, raw JSON, endpoint/file/protocol diagnostics, and technical command payloads unless the player explicitly opens technical details/advanced mode.
- **FR-006**: Add or update a scoped regression/source guard for default player UI copy. The guard MUST exclude tests, docs, comments, source-guard literals, and explicit advanced diagnostics surfaces so it does not ban legitimate technical documentation.
- **FR-007**: Add or update a short browser UI copy guideline/checklist that states: player UI speaks to the player; implementation comments stay in code/docs/advanced mode.
- **FR-008**: Verification MUST include frontend verify, focused browser/local web UI .NET tests with visible counts, `git diff --check`, an added-line static security scan, and generated HTML visual smoke evidence.

### Out of Scope

- No gameplay/application logic changes in React.
- No C# runtime contract, validation, normalizer, pending/control, Mortal World, Afterlife, Chaos Sea, Shining Abode, Saref, or GM-authored output contract changes are expected.
- Do not remove advanced diagnostics completely; keep them opt-in.
- Do not resurrect obsolete card-heavy Feature-branch UI criteria. Preserve the current minimalist top-tab shell, single command input, and `/help` discovery direction.
- Do not add new browser automation dependencies unless a separate tracked issue/spec explicitly selects them.

## Contract Scope

- Browser/frontend presentation copy and source guards in `BookOfEternityClient.WebFrontend/src/`, `BookOfEternityClient.WebFrontend/test/`, and C# source/documentation guard tests under `BookOfEternityClient.Tests/`.
- Browser UI docs/checklists under `docs/web-ui/` and/or `BookOfEternityClient.WebFrontend/README.md`.
- Generated `TestResults/browser-smoke/` evidence is local/ignored and must not be committed.
- No GM-facing prompt/example/contract updates unless implementation discovers an actual runtime contract change; expected docs/prompts impact is browser UI docs only.

## Independent Closure Review Findings to Address

The 2026-06-06 independent review returned `CHANGES_REQUIRED` with these blockers:

1. Default UI still contains technical/meta framing, including examples in `LoadingCard.tsx`, `GameLauncher.tsx`, `tabBarConfig.ts`, `ConnectionBanner.tsx`, `App.tsx`, and `useShellState.ts`.
2. Default Help loads command coverage and can include advanced-only commands such as `/help`, `/math`, `/gm`, `/debug`, `/mods`, `/system_guardians`, and `/validate` without advanced opt-in.
3. Existing guard coverage does not scan the full default-player copy boundary scoped to #738.
4. `CommandResultView.tsx` / `BlockRenderer.tsx` may expose raw commands or raw JSON in default command result flow unless gated/sanitized.

Codex should verify each finding against current code, fix confirmed blockers, and push back only with code/test evidence if a finding is already safely handled.

## Verification Commands

Baseline before implementation on clean `fix/738-player-ui-copy` from `origin/main`:

```bash
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
```

Observed baseline from 2026-06-06:

- `npm ci`: succeeded, 54 packages, 0 vulnerabilities.
- `npm run verify`: succeeded; Vitest 23/23 passed and Vite build succeeded.
- Focused .NET browser/local web UI filter: passed 53/53, 0 failed, 0 skipped, and generated `TestResults/browser-smoke/*` artifacts.
- `git diff --check origin/main...HEAD`: passed with no implementation diff.
- Added-line static scan: no matches because no implementation diff yet.
- Independent closure review: `CHANGES_REQUIRED` as listed above.

Minimum final verification:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~BrowserApiContractTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Run an added-line static scan excluding generated/scratch artifacts and broad plan/spec prose. Run broader `dotnet test` only if implementation touches shared runtime behavior beyond browser UI/source guards/docs.
