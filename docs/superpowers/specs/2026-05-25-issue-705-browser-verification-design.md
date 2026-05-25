# Issue #705 Browser Verification Pipeline Design

## Context

Issue #705 follows the Browser Client architecture stack already merged in #701, #702, #703, and #704. The repository now has a Vite + React + TypeScript frontend workspace under `BookOfEternityClient.WebFrontend/`, typed browser API contracts, C# static asset hosting, and a first React app shell. The missing piece is a single local/CI verification path that proves both sides still fit together.

Stanislav authorized unattended execution, so this design records the approval rationale instead of pausing for manual sign-off.

## Goals

- Add a frontend-aware CI/local verification sequence: dependency restore, TypeScript typecheck, production build, and C# local-host smoke coverage.
- Add a built-frontend smoke test that launches `LocalWebUiHost` against the actual Vite `dist/` output and checks root, SPA route fallback, API states, and API/static misses.
- Capture practical diagnostics for failures without adding a heavyweight browser automation stack yet: root HTML, SPA-route HTML, JSON API responses, and network/status metadata under `TestResults/browser-smoke/`.
- Document the runbook so future agents know the exact commands and artifacts.
- Preserve project boundaries: C# remains gameplay/application authority; React remains presentation; default player UI stays separate from advanced/debug surfaces.

## Non-goals

- Do not introduce Playwright/Selenium yet. Screenshots are deferred until a tracked browser automation task adds those dependencies.
- Do not require every future Browser Client feature screen to be complete.
- Do not move gameplay logic or DTO authority into TypeScript.

## Approach considered

1. **CI-only npm steps plus docs.** Simple, but it would not prove `LocalWebUiHost` can serve the built frontend or produce diagnostics for agents.
2. **Full browser automation with screenshots now.** Stronger evidence, but it adds new dependencies and moving parts before the project has chosen a browser automation stack.
3. **Recommended: npm CI/typecheck/build plus C# built-frontend smoke tests and artifact upload.** This is small, local/offline-friendly, and catches static host, SPA fallback, API contract, and player/advanced separation regressions using existing .NET test infrastructure.

## Design

### Frontend verification command

`BookOfEternityClient.WebFrontend/package.json` gets a `verify` script that runs the current TypeScript and production build checks. CI and docs use `npm ci --prefix BookOfEternityClient.WebFrontend` followed by `npm run verify --prefix BookOfEternityClient.WebFrontend`.

### CI workflow

`.github/workflows/dotnet-ci.yml` adds Node setup/cache and a Browser frontend verification step before the .NET restore/build/test steps. This ensures the Vite `dist/` output exists before the .NET smoke suite runs. The workflow uploads both standard `TestResults` and `TestResults/browser-smoke` diagnostics.

### Built frontend smoke test

A new `LocalWebUiBuiltFrontendSmokeTests` class uses the real `BookOfEternityClient.WebFrontend/dist/` directory. It starts `LocalWebUiHost` with that directory, then requests:

- `/` for built `index.html`;
- `/game` to prove SPA client routes fall back to the shell;
- `/api/main-menu`, `/api/session`, and `/api/game-screen` for player-facing local state;
- `/api/not-real` and `/assets/not-real.js` to prove API/static misses are not masked by the SPA fallback.

The test writes `root.html`, `game-route.html`, `main-menu.json`, `session.json`, `game-screen.json`, and `network.json` to `TestResults/browser-smoke/`. It asserts the built shell is used, core API state is returned, default source/build contracts still mention Russian player routes and explicit `Расширенный режим`, and missing API/assets return 404.

### Guard/documentation tests

Existing `BrowserFrontendWorkspaceTests` and `LocalWebUiDocumentationTests` are extended to guard the new verify script, CI workflow steps, docs, and artifact path.

## Risks and mitigations

- **Risk:** Running .NET tests without a frontend build can fail the built-frontend smoke test.
  **Mitigation:** CI and docs make frontend build the first step; the smoke failure message points to `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Risk:** Artifact files become untracked local noise.
  **Mitigation:** add `/TestResults/` to `.gitignore`.
- **Risk:** HTML-only smoke cannot execute React and take screenshots.
  **Mitigation:** capture host/API/HTML/network diagnostics now and document screenshots as deferred to a future browser automation task.

## Verification plan

- RED/GREEN focused .NET guard tests:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests.FrontendWorkspace_HasVerifyScriptAndCiFrontendWorkflow|FullyQualifiedName~LocalWebUiDocumentationTests.LocalWebHostDocs_DocumentFrontendVerificationPipeline" --logger "console;verbosity=minimal"`
- Frontend restore/typecheck/build:
  - `npm ci --prefix BookOfEternityClient.WebFrontend`
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`
- Built smoke:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "Category=BrowserWebUiBuiltFrontend|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests" --logger "console;verbosity=minimal"`
- Relevant browser suite:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "BrowserFrontend|LocalWebUi|BrowserWebUiSmoke|BrowserApi" --logger "console;verbosity=minimal"`
- `git diff --check` and added-line security scan.
- PR CI `.NET CI / Build and test` green before merge.

## Self-review

- No placeholders remain.
- Scope is one closure unit for #705.
- The approach satisfies all #705 acceptance criteria without introducing new gameplay logic or a browser automation dependency.
