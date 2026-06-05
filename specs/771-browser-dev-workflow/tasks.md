# Tasks: Browser Client Local Dev Workflow

**Source Issue**: [#771](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/771)
**Spec**: `specs/771-browser-dev-workflow/spec.md`
**Plan**: `specs/771-browser-dev-workflow/plan.md`

## Phase 1: Exploration and Baseline

- [X] Inspect `AGENTS.md` and `.specify/memory/constitution.md` for issue/spec/browser DevEx guardrails.
- [X] Inspect issue #771 body and triage comment from the Feature-branch audit.
- [X] Confirm current Vite config already proxies `/api` and `/assets` to `http://127.0.0.1:8787`.
- [X] Confirm current local web host/startup docs already keep `--web-url` loopback-only and reject `0.0.0.0`.
- [X] Create isolated worktree `E:/Games/worktrees/boe-771-browser-dev-workflow` on `fix/771-browser-dev-workflow` from `origin/main`.
- [X] Run baseline frontend verification: `npm ci` succeeded with 0 vulnerabilities; `npm run verify` succeeded with vitest 23/23 and Vite build.
- [X] Run baseline focused .NET tests: focused BrowserFrontendWorkspace/LocalWebUiDocumentation/ClientStartupOptions/LocalWebUiHost filter passed 92/92.

## Phase 2: Regression / Source Guards (TDD RED)

- [X] Add package/source guard coverage requiring a combined Browser Client local dev command in `BookOfEternityClient.WebFrontend/package.json`.
- [X] Add source guard coverage requiring a focused helper script under `BookOfEternityClient.WebFrontend/scripts/` that uses structured process spawning and loopback defaults.
- [X] Add or update Vite proxy guard coverage requiring `/api` and `/assets` proxy to `http://127.0.0.1:8787`.
- [X] Add documentation guard coverage requiring docs to explain the combined command, Vite proxy, backend/frontend URLs, and local-only `0.0.0.0` boundary.
- [X] Run the focused .NET filter and record RED failures for the missing combined workflow contract.

## Phase 3: Minimal Implementation (GREEN)

- [X] Add the combined local dev package script, expected name `dev:local` unless implementation finds a stronger existing convention.
- [X] Add the helper script that starts `dotnet run --project ../BookOfEternityClient -- --web` and the existing Vite dev server with argument arrays.
- [X] Make the helper forward stdio, handle Ctrl+C/SIGINT/SIGTERM, and terminate both children when one exits.
- [X] Keep existing `dev`, `verify`, `build`, and `preview` behavior compatible with existing tests.
- [X] Do not add broad CORS, public bind, cloud tunnel, telemetry, or backend keep-alive behavior.

## Phase 4: Docs / Spec Reconciliation

- [X] Update `BookOfEternityClient.WebFrontend/README.md` with the combined command and when to use it versus separate backend/frontend commands.
- [X] Update `docs/web-ui/local-web-host.md` tracked-task list/Frontend Workspace section for #771 and the loopback proxy dev workflow.
- [X] Record that remote `0.0.0.0` binding and unreproduced idle-exit keepalive are out of scope for #771.
- [X] Review `spec.md` and `plan.md`; no accepted requirement changes were needed, and final evidence was added to `plan.md`/`tasks.md`.

## Phase 5: Verification and Review Prep

- [X] Run `npm run verify --prefix BookOfEternityClient.WebFrontend` and record exact output/counts.
- [X] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"` and record exact pass/fail/skip counts.
- [X] Run `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and record warnings/errors.
- [X] Run `git diff --check origin/main...HEAD`.
- [X] Run added-line static security scan excluding generated/scratch artifacts.
- [X] Run `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` and confirm it resolves `specs/771-browser-dev-workflow`.
- [X] Commit focused changes with `[skip ci]`.
- [ ] Obtain independent review before PR/merge; fix Critical/Important findings. Hermes owns final review/PR/merge reconciliation for this delegated slice.
- [ ] Create PR, squash-merge after local verification, and verify issue #771 closes. Hermes owns final PR/merge/issue closure.

## Verification Evidence

- Baseline before implementation:
  - `npm ci --prefix BookOfEternityClient.WebFrontend`: succeeded, 54 packages, 0 vulnerabilities.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; vitest passed 23/23 and Vite build succeeded.
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"`: passed 92/92, 0 failed, 0 skipped.
- TDD RED after adding guards:
  - `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~ClientStartupOptionsTests|FullyQualifiedName~LocalWebUiHostTests" --logger "console;verbosity=minimal"`: failed as expected; 2 failed, 93 passed, 0 skipped, total 95. Failures were the missing `dev:local`/helper package guard and missing #771 documentation guard.
- GREEN/final local evidence:
  - `node --check BookOfEternityClient.WebFrontend/scripts/dev-local.mjs`: exit 0.
  - Focused .NET guard rerun after implementation: passed 95/95, 0 failed, 0 skipped.
  - `npm run verify --prefix BookOfEternityClient.WebFrontend`: succeeded; vitest passed 23/23 across 2 files and Vite build succeeded.
  - Final focused .NET gate: passed 95/95, 0 failed, 0 skipped.
  - `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`: succeeded with 0 warnings and 0 errors.
  - `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks`: resolved `specs/771-browser-dev-workflow` with `AVAILABLE_DOCS=["tasks.md"]`.
  - `git diff --check origin/main...HEAD`: exit 0 on the focused committed range.
  - Added-line static scan: broad matches were limited to source-guard text forbidding `exec(`/`shell: true` and the Spec Kit scan instruction itself; refined review found no actionable hardcoded secret, shell injection, eval/exec, unsafe deserialization, or SQL string-formatting additions.
