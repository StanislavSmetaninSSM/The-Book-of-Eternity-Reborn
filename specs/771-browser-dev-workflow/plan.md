# Implementation Plan: Browser Client Local Dev Workflow

**Source Issue**: [#771](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/771)
**Spec**: `specs/771-browser-dev-workflow/spec.md`
**Branch / Worktree**: `fix/771-browser-dev-workflow` at `E:/Games/worktrees/boe-771-browser-dev-workflow`
**Constitution**: `.specify/memory/constitution.md` v1.1.0

## Technical Context

- Runtime: .NET 8 C# local web host in `BookOfEternityClient/WebUi/LocalWebUiHost.cs`.
- Startup options: `BookOfEternityClient/Configuration/ClientStartupOptions.cs` already supports `--web-url` and requires loopback URLs for agent/web local hosts.
- Frontend workspace: `BookOfEternityClient.WebFrontend/` is the established React/Vite workspace.
- Vite config already proxies `/api` and `/assets` to `http://127.0.0.1:8787`.
- Documentation: `docs/web-ui/local-web-host.md` and `BookOfEternityClient.WebFrontend/README.md` are the main DevEx touchpoints.
- Guard tests: `BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs`, `LocalWebUiDocumentationTests.cs`, `ClientStartupOptionsTests.cs`, and `LocalWebUiHostTests.cs` currently encode workspace/startup/local-only contracts.

## Baseline Evidence

On clean `fix/771-browser-dev-workflow` from `origin/main` (`9f16cbd`):

- `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded, 54 packages, 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; vitest passed 23/23 and Vite build succeeded.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"`: passed 92/92, 0 failed, 0 skipped.

## Architecture

Keep the local web host security model unchanged and add a small frontend-workspace DevEx wrapper rather than new runtime behavior. The wrapper should live under `BookOfEternityClient.WebFrontend/scripts/` and spawn two structured child processes: the C# backend via `dotnet run --project ../BookOfEternityClient -- --web` and the Vite frontend via the existing Vite dev command. It should forward stdio, coordinate shutdown, and exit with a failing code when either child fails.

Update source guard tests and docs so future agents understand that #771 is satisfied by loopback integrated development plus Vite proxy, not by public bind/CORS/keepalive changes. The implementation must not move gameplay/application authority into TypeScript.

## Implementation Phases

1. **TDD RED - workspace contract**
   - Add or update `BrowserFrontendWorkspaceTests` to require a combined dev script, the helper script path, and Vite proxy entries for `/api` and `/assets`.
   - Add documentation guard expectations for the combined command, proxy behavior, and loopback-only/no-`0.0.0.0` boundary.
   - Run the focused .NET filter and verify failures are due to missing combined workflow docs/source guards.
2. **GREEN - helper and package script**
   - Add `BookOfEternityClient.WebFrontend/scripts/dev-local.mjs` (or an equivalent focused helper) using `node:child_process.spawn` with argument arrays, not interpolated shell strings.
   - Add a package script such as `dev:local` that invokes the helper.
   - Preserve existing `dev`, `verify`, `build`, and `preview` scripts unless a test requires a precise safe update.
3. **Docs reconciliation**
   - Update `BookOfEternityClient.WebFrontend/README.md` and `docs/web-ui/local-web-host.md` with the new command and current-state notes.
   - Mention that Vite is the frontend dev URL, C# stays at `127.0.0.1:8787`, and public/LAN binding remains out of scope.
4. **Spec Kit / verification**
   - Update `tasks.md` with final RED/GREEN and verification evidence.
   - Run frontend verify, focused .NET guards, client build, `git diff --check`, static scan, and Spec Kit prerequisite check.

## Risks and Constraints

- Do not enable remote/public binding. The project constitution requires local play, and the current local host rejects public bind addresses.
- Do not add broad CORS headers unless a future tracked issue requires a non-proxy dev workflow.
- Do not add a keep-alive workaround for the unreproduced idle-exit claim in #771; record it as out of scope unless reproduced.
- If `dotnet watch` is used, ensure it is optional and still passes `--web`/loopback defaults. A plain `dotnet run` wrapper is acceptable for this issue if it provides one-command integrated startup.
- Do not commit generated `node_modules/`, `dist/`, `TestResults/`, `bin/`, or `obj/` artifacts.

## Verification Plan

Minimum commands:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Also run the repository's added-line static scan for hardcoded secrets, shell injection, eval/exec, unsafe deserialization, and SQL string formatting. Run broader C# tests if shared startup/local host code changes beyond tests/docs/source guards.

## Verification Evidence

- TDD RED: after adding the package/helper/docs/source guard coverage, the focused .NET filter failed as expected with 2 failed, 93 passed, 0 skipped, total 95. The failures were the missing `dev:local`/helper workflow and missing #771 docs.
- Helper syntax: `node --check BookOfEternityClient.WebFrontend/scripts/dev-local.mjs` exited 0.
- GREEN guard rerun: the focused .NET filter passed 95/95, 0 failed, 0 skipped.
- Frontend final gate: `npm run verify --prefix BookOfEternityClient.WebFrontend` succeeded; vitest passed 23/23 across 2 files and Vite build succeeded.
- Focused .NET final gate: passed 95/95, 0 failed, 0 skipped.
- Client build: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` succeeded with 0 warnings and 0 errors.
- Spec Kit prerequisite check resolved `specs/771-browser-dev-workflow` with `AVAILABLE_DOCS=["tasks.md"]`.
- Added-line static scan found no actionable hardcoded secret, shell injection, eval/exec, unsafe deserialization, or SQL string-formatting additions after manually reviewing source-guard/spec-text matches.
