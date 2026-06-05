# Feature Specification: Browser Client Local Dev Workflow

**Feature Branch**: `fix/771-browser-dev-workflow`
**Created**: 2026-06-06
**Status**: Planned; implementation delegated to Codex from autonomous worker
**Source Issue**: [#771 [Browser Client][DevEx] No integrated dev server setup — Vite and backend must be started separately with no proxy](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/771)

## Current-State Finding

The original issue body is partly stale on current `main`: `BookOfEternityClient.WebFrontend/vite.config.ts` already proxies `/api` and `/assets` to `http://127.0.0.1:8787`, `--web-url` already exists, and the local web host intentionally rejects public bind addresses such as `0.0.0.0`. This feature tightens #771 to the remaining safe DevEx gap: a documented one-command loopback development workflow that starts the C# local web host and the Vite frontend together without weakening the local-only security model.

## User Stories & Testing

### User Story 1 - Start both local dev servers with one command (Priority: P1)

A developer working on the Browser Client can start the C# backend and Vite frontend from the established frontend workspace with one documented command, then use the Vite dev server for hot frontend reload while API calls go to the local C# authority.

**Independent Test**: Source/contract tests assert the frontend package exposes a combined local-dev script and that the helper script launches only loopback backend/frontend commands.

**Acceptance Scenarios**:

1. **Given** dependencies are installed, **When** the developer runs the documented combined dev command, **Then** it starts the backend on `http://127.0.0.1:8787` and Vite on loopback without needing two manually coordinated terminals.
2. **Given** the frontend calls `/api/*` or `/assets/*` through Vite, **When** the backend is running on `127.0.0.1:8787`, **Then** the existing Vite proxy forwards those requests to the backend.
3. **Given** either child process exits, **When** the combined dev script observes the exit, **Then** it terminates the sibling process and exits non-zero for failures instead of leaving orphaned local servers.

### User Story 2 - Keep local-only security explicit (Priority: P1)

A developer reading #771-era instructions understands that remote/public browser testing is not enabled by this task because the project constitution requires local play and the host rejects non-loopback bind addresses.

**Independent Test**: Documentation/source guards assert the docs mention loopback-only development, existing `--web-url` usage, Vite proxy behavior, and that `0.0.0.0` remains rejected/out of scope.

### User Story 3 - Do not invent an idle-exit or CORS bug (Priority: P2)

A maintainer can see what #771 still covers after the Feature-branch audit note: proxy/integrated workflow yes, unreproduced idle-exit and public bind/CORS changes no.

**Independent Test**: The issue/Spec Kit tasks record idle-exit and public bind as out of scope unless later reproduced in a separate tracked issue.

## Requirements

### Functional Requirements

- **FR-001**: The frontend workspace MUST expose a combined local development command for Browser Client work.
- **FR-002**: The combined command MUST start the C# local web host with `--web` and the Vite dev server using loopback defaults.
- **FR-003**: The command/helper MUST avoid shell-specific injection-prone string execution; use structured process spawning or a similarly safe cross-platform approach.
- **FR-004**: The helper MUST shut down both child processes when one exits, or when the parent receives Ctrl+C/SIGTERM/SIGINT.
- **FR-005**: Existing Vite proxy coverage for `/api` and `/assets` to `http://127.0.0.1:8787` MUST remain covered by source guards.
- **FR-006**: Documentation MUST explain the new command, the two-service URL model, the proxy, and the local-only security boundary.
- **FR-007**: The implementation MUST NOT enable `0.0.0.0`, public LAN binding, cloud tunneling, telemetry, or CORS broadening as part of this issue.
- **FR-008**: The implementation MUST NOT add a backend keep-alive workaround unless the idle-exit symptom is reproduced with exact logs and a separate tracked issue/spec.

### Out of Scope

- Remote browser testing from another device or public interface binding.
- Broad CORS policy changes. The Vite proxy is the dev-time API bridge.
- Backend hot-reload beyond what the chosen local dev helper can safely invoke. If `dotnet watch` is added, it must remain loopback-only and documented.
- Any gameplay, GM prompt, Afterlife, Mortal World, validation, or runtime-state contract changes.

## Contract Scope

- DevEx/browser frontend workspace: `BookOfEternityClient.WebFrontend/package.json`, helper scripts under that workspace if needed, and Vite proxy source guards.
- C# startup/local host docs/tests only as needed to preserve the existing loopback `--web-url` contract.
- No player-facing UI, GM-facing prompt, game-state, validation, normalizer, pending/control, Afterlife, Mortal, or console/browser gameplay parity contract changes.

## Verification Commands

Baseline before implementation on clean `fix/771-browser-dev-workflow` from `origin/main`:

```bash
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"
```

Observed baseline from 2026-06-06:

- `npm ci`: succeeded, 54 packages, 0 vulnerabilities.
- `npm run verify`: succeeded; vitest 23/23 passed and Vite build succeeded.
- Focused .NET filter: passed 92/92, 0 failed, 0 skipped.

Minimum final verification:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Run a lightweight static added-line scan before PR/merge. Run broader `dotnet test` only if implementation touches shared startup/local-host behavior beyond source guards/docs.
