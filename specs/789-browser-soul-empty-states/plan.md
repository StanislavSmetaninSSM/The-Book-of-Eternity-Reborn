# Implementation Plan: Browser Soul page empty states and player copy

**Branch**: `task/789-browser-soul-empty-states` | **Date**: 2026-06-09 | **Spec**: `specs/789-browser-soul-empty-states/spec.md`

**Input**: Feature specification from `/specs/789-browser-soul-empty-states/spec.md`

## Summary

Polish the Browser Client Soul/status page so missing player/soul identity data appears as intentional in-world empty states, meaningful values are visible by default, status meters retain semantic severity/accessibility, and default copy avoids system/debug language. This is frontend presentation-only work for GitHub issue #789.

## Technical Context

**Language/Version**: TypeScript/React 19, CSS, Vite; .NET 8 C# source-guard tests may need synchronization if frontend source expectations change.

**Primary Dependencies**: React, Vite, TypeScript, Vitest/source-guard scripts.

**Storage**: N/A; presentation over existing shell/game-screen state only.

**Testing**: focused frontend guard/component tests; `npm run verify --prefix BookOfEternityClient.WebFrontend`; focused `.NET` browser frontend/source-smoke filter when relevant; visual smoke artifact or browser inspection.

**Target Platform**: Local browser client served by Vite/local C# web host on loopback.

**Project Type**: React/Vite frontend inside a local game-client repository.

**Performance Goals**: No measurable runtime impact; helper functions and CSS should be small and local.

**Constraints**: Keep React presentation-only; do not change game state, browser endpoints, C# command logic, GM prompts, examples, validation, or runtime contracts. Preserve current minimalist Browser Client direction.

**Scale/Scope**: Soul/status page UX and status meter presentation only; sibling #790 sidebar and #791 Home page work remain separate.

**Source Issue(s)**: #789 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/789

**Contract Scope**: Player-facing browser/frontend visual/copy presentation only; no GM-facing/runtime contract changes.

**Baseline**: In `E:/Games/worktrees/boe-789-soul-page-empty-states`, `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 52 packages and 0 vulnerabilities. `npm run verify --prefix BookOfEternityClient.WebFrontend` passed on 2026-06-09 with typecheck, player-facing node guards, Vitest 4 files / 37 tests, and Vite build 46 modules. Initial `.NET` browser source-smoke baseline with `--no-restore` failed because fresh worktree lacked `BookOfEternityClient.Tests/obj/project.assets.json`; rerun without `--no-restore` restored packages and passed 31/31.

**Verification Commands**:

```bash
(cd BookOfEternityClient.WebFrontend && npm exec -- vitest run test/browserSoulEmptyStates.test.tsx)
npm run verify --prefix BookOfEternityClient.WebFrontend
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"
git diff --check origin/main...HEAD
git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' ':(exclude)specs/**/*.md' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Constitution Check

- **GitHub traceability**: PASS — source issue #789 is linked in `spec.md`, this plan, and `tasks.md`.
- **Spec Kit fit**: PASS — player-facing Browser Client UX over a durable status/soul surface spans frontend source/tests and requires handoff evidence.
- **Player-facing integrity**: PASS — planned copy is Russian/in-world and default UI must avoid API/DTO/debug language.
- **Contract/state authority**: PASS — no canonical state, validation, pending/control, GM prompt, example, or runtime contract changes are planned.
- **Test-first path**: PASS — add focused guards before implementation and run RED/GREEN or mutation evidence.
- **Verification evidence**: PASS — frontend verify, focused `.NET` browser source-smoke, diff check, static scan, and visual smoke evidence are planned.
- **Agent orchestration**: PASS — Codex delegation receives this spec/plan/tasks path, constitution path, #789 acceptance criteria, baseline verification, and Superpowers method requirements.

## Project Structure

### Documentation (this feature)

```text
specs/789-browser-soul-empty-states/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient.WebFrontend/
├── src/components/StatusView.tsx        # likely add missing-value helpers, empty-state rendering, visible sections, player copy
├── src/components/StatusBar.tsx         # inspect/preserve #788 semantic status-bar thresholds; change only if shared component use is needed
├── src/styles/command-ui.css            # status/soul card empty-state/status-meter styling
├── src/styles/components.css            # shared empty-state/card/detail styles if current status page uses shared classes
└── test/browserSoulEmptyStates.test.tsx # focused #789 frontend guards/component tests

BookOfEternityClient.Tests/
└── BrowserFrontendWorkspaceTests.cs or LocalWebUiBuiltFrontendSmokeTests.cs # update only if source/built-frontend guards require new expectations

TestResults/browser-smoke/              # generated local visual smoke artifact only if needed; keep untracked unless intentionally required
```

**Structure Decision**: Prefer local helper functions and scoped classes in `StatusView.tsx` plus source/component guards. Avoid new endpoints, state mutation, broad app-shell redesign, or new dependencies.

## Implementation Approach

1. Inspect current `StatusView.tsx`, status-meter CSS, existing player-facing/source guards, and browser smoke builders.
2. Add focused #789 tests before implementation:
   - missing player/soul identity values produce a polished empty-state card or inline empty marker;
   - meaningful identity values remain visible;
   - default copy contains desired Russian text and excludes raw/system/debug terms;
   - status meters preserve semantic severity/accessibility.
3. Run the focused guard/test from the frontend workspace cwd and confirm RED against current `main`, or produce mutation evidence if a requirement is already partially satisfied.
4. Apply minimal React/CSS changes: missing-value helper, empty-state rendering, player-facing copy, visible sections by default, and scoped visual polish.
5. Re-run focused guard(s), `npm run verify`, focused `.NET` browser source-smoke, `git diff --check`, and static scan.
6. Produce visual smoke evidence for missing-data and populated-data states via Vite/browser when practical or a dependency-light HTML artifact.
7. Reconcile `tasks.md` with implementation and verification evidence before final review/PR.

## Complexity Tracking

No constitution violations are planned. Spec Kit overhead is justified by the player-facing Browser Client UX surface and autonomous handoff; implementation should remain frontend-focused and small.
