# Implementation Plan: Browser card hierarchy and status styling

**Branch**: `task/788-browser-card-hierarchy` | **Date**: 2026-06-09 | **Spec**: `specs/788-browser-card-hierarchy/spec.md`

**Input**: Feature specification from `/specs/788-browser-card-hierarchy/spec.md`

## Summary

Polish the Browser Client's card visual hierarchy so player-facing action/CTA cards differ from passive info cards, status bars communicate good/warning/danger thresholds, clickable cards provide hover/focus feedback, and `kv-list` typography is scannable. This is frontend presentation-only work for GitHub issue #788 and must preserve the current minimalist browser client direction.

## Technical Context

**Language/Version**: TypeScript/React 19, CSS, Vite; repository also contains .NET 8 C# runtime but C# changes are not expected.

**Primary Dependencies**: React, Vite, TypeScript, Vitest/source-guard scripts.

**Storage**: N/A; frontend presentation only.

**Testing**: `npm run verify --prefix BookOfEternityClient.WebFrontend`, focused source/style/component guards, Vite preview or generated visual smoke artifact.

**Target Platform**: Local browser client served by Vite/local C# web host on loopback.

**Project Type**: React/Vite frontend inside a local game-client repository.

**Performance Goals**: No measurable runtime impact; CSS/React class changes should not add dependencies or heavy rendering work.

**Constraints**: Keep React presentation-only; do not change game state, browser endpoints, C# command logic, GM prompts, examples, validation, or runtime contracts. Keep default UI Russian/player-facing and advanced/debug surfaces separated.

**Scale/Scope**: Shared browser card hierarchy/status/typography surfaces named in #788 only; sibling sidebar navigation/home-page tasks remain #790/#791.

**Source Issue(s)**: #788 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/788

**Contract Scope**: Player-facing browser/frontend visual presentation only; no GM-facing/runtime contract changes.

**Baseline**: In `E:/Games/worktrees/boe-788-card-hierarchy`, `npm ci --prefix BookOfEternityClient.WebFrontend` completed with 52 packages and 0 vulnerabilities, then `npm run verify --prefix BookOfEternityClient.WebFrontend` passed on 2026-06-09 with typecheck, player-facing node guards, Vitest 3 files / 32 tests, and Vite build 46 modules.

**Verification Commands**:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
git diff --check origin/main...HEAD
git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Constitution Check

- **GitHub traceability**: PASS — source issue #788 is linked in `spec.md`, this plan, and `tasks.md`.
- **Spec Kit fit**: PASS — player-facing Browser Client UX polish over multiple shared surfaces benefits from durable acceptance and autonomous handoff.
- **Player-facing integrity**: PASS — no debug/API/DTO copy is planned; default browser UI remains player-facing.
- **Contract/state authority**: PASS — no canonical state, validation, pending/control, GM prompt, example, or runtime contract changes are planned.
- **Test-first path**: PASS — add/update focused guard(s) before CSS/component implementation and run RED/GREEN where practical.
- **Verification evidence**: PASS — frontend verify, diff check, static scan, and visual smoke evidence are planned.
- **Agent orchestration**: PASS — Codex delegation receives this spec/plan/tasks path, constitution path, #788 acceptance criteria, baseline verification, and Superpowers method requirements.

## Project Structure

### Documentation (this feature)

```text
specs/788-browser-card-hierarchy/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient.WebFrontend/
├── src/components/StatusBar.tsx       # likely add semantic severity class derivation
├── src/components/GameLauncher.tsx    # inspect launcher action/card class usage; change only if CSS needs stable CTA/info selectors
├── src/components/TurnStatePanel.tsx  # inspect action-card usage; change only for generic enabled/disabled action affordance classes if needed
├── src/styles/components.css          # shared card/summary/narrative/shell hierarchy and kv-list styles
├── src/styles/command-ui.css          # active launcher/action/status/kv-list/status-meter styles
└── test/                              # add/update focused #788 frontend guard(s)

TestResults/browser-smoke/             # generated local visual smoke artifact only if needed; keep it out of commits unless intentionally tracked by existing test conventions
```

**Structure Decision**: Prefer scoped CSS plus small `StatusBar` semantic class support and focused frontend guards. Avoid new runtime services, new dependencies, or broad component restructuring for this visual hierarchy issue.

## Implementation Approach

1. Inspect current launcher/action/status/kv-list CSS and nearby player-facing guards.
2. Add focused tests/guards for #788 before implementation:
   - action/CTA selectors have golden/accent/gradient/hover/focus differentiation;
   - `StatusBar` maps `>66`, `>33`, `<=33`/unparsable values to semantic classes;
   - `.kv-list dt/dd` typography is visibly differentiated.
3. Run the focused guard(s) to confirm RED or mutation evidence where current production already partially satisfies a requirement.
4. Apply minimal React/CSS changes using existing tokens and scoped classes.
5. Re-run the focused guard(s) and full `npm run verify`.
6. Produce visual smoke evidence via Vite preview or a dependency-light artifact, without overstating it as automated screenshot coverage.
7. Reconcile `tasks.md` evidence before final review/PR.

## Complexity Tracking

No constitution violations are planned. Spec Kit overhead is justified by the browser/player-facing UX surface and autonomous handoff; implementation should remain frontend-focused and small.
