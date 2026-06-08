# Implementation Plan: Browser card spacing polish

**Branch**: `787-browser-card-spacing` | **Date**: 2026-06-08 | **Spec**: `specs/787-browser-card-spacing/spec.md`

**Input**: Feature specification from `/specs/787-browser-card-spacing/spec.md`

## Summary

Polish the Browser Client's shared card spacing so ShellPanel, summary/narrative cards, DetailSurfaceCard summaries, and right sidebar/status cards have consistent internal breathing room on desktop and mobile. This is a frontend presentation-only change anchored to issue #787 and must preserve the current minimalist browser direction and player-facing/default-vs-advanced boundaries.

## Technical Context

**Language/Version**: TypeScript/React 19, CSS, Vite; repository also contains .NET 8 C# runtime but C# changes are not expected.

**Primary Dependencies**: React, Vite, TypeScript, Vitest/source-guard scripts.

**Storage**: N/A; CSS/React presentation only.

**Testing**: `npm run verify --prefix BookOfEternityClient.WebFrontend`, focused source/style guards, Vite preview or generated visual smoke artifact.

**Target Platform**: Local browser client served by Vite/local C# web host on loopback.

**Project Type**: React/Vite frontend inside a local game-client repository.

**Performance Goals**: No measurable runtime impact; CSS-only/tokenized layout changes should not add dependencies or heavy rendering work.

**Constraints**: Keep React presentation-only; do not change game state, browser endpoints, C# command logic, GM prompts, examples, validation, or runtime contracts. Keep default UI Russian/player-facing and advanced/debug surfaces separated.

**Scale/Scope**: Shared browser card/sidebar/detail surfaces named in #787 only; sibling visual hierarchy/action-button tasks remain #788/#791.

**Source Issue(s)**: #787 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/787

**Contract Scope**: Player-facing browser/frontend visual presentation only; no GM-facing/runtime contract changes.

**Verification Commands**:

```bash
npm run verify --prefix BookOfEternityClient.WebFrontend
git diff --check origin/main...HEAD
git diff origin/main...HEAD -- . ':(exclude)docs/superpowers/plans/*.md' | grep '^+' | grep -iE "(api_key|secret|password|token|passwd)\s*=\s*['\"][^'\"]{6,}['\"]|os\.system\(|subprocess.*shell=True|(^|[^.])\bexec\(|\beval\(|pickle\.loads?\(|execute\(f\"|\.format\(.*SELECT|\.format\(.*INSERT" || echo NO_MATCHES
```

## Constitution Check

- **GitHub traceability**: PASS — source issue #787 is linked in `spec.md`, this plan, and `tasks.md`.
- **Spec Kit fit**: PASS — player-facing Browser Client UX polish over shared surfaces benefits from durable acceptance and autonomous handoff.
- **Player-facing integrity**: PASS — no debug/API/DTO copy is introduced; default browser UI remains player-facing.
- **Contract/state authority**: PASS — no canonical state, validation, pending/control, GM prompt, example, or runtime contract changes are planned.
- **Test-first path**: PASS — add/update a focused style/source guard before CSS implementation and run it RED/GREEN where practical.
- **Verification evidence**: PASS — frontend verify, diff check, static scan, and visual smoke evidence are planned.
- **Agent orchestration**: PASS — Codex delegation must receive this spec/plan/tasks path, constitution path, #787 acceptance criteria, baseline verification, and Superpowers method requirements.

## Project Structure

### Documentation (this feature)

```text
specs/787-browser-card-spacing/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient.WebFrontend/
├── src/styles/components.css      # likely shared card/sidebar/detail spacing selectors
├── src/styles/layout.css          # inspect only if shell/sidebar layout spacing lives here
├── src/components/                # inspect ShellPanel, DetailSurfaceCard/status/sidebar components if class names or structure must change
└── test/                          # add/update focused player-facing/source/style guard for #787 spacing

TestResults/browser-smoke/         # generated local visual smoke artifact only if needed; keep it out of commits unless intentionally tracked by existing test conventions
```

**Structure Decision**: Prefer tokenized CSS changes in existing style files and a focused frontend guard test. Avoid new runtime services, new dependencies, or broad component restructuring for this spacing-only issue.

## Implementation Approach

1. Inspect current card/sidebar/detail CSS and nearby tests/source guards.
2. Add a focused guard that captures #787 spacing minima and would fail against a cramped regression.
3. Run the focused guard or `npm run test:player-facing --prefix BookOfEternityClient.WebFrontend` to confirm RED where practical.
4. Apply minimal tokenized CSS/markup changes to shared card/sidebar/detail selectors.
5. Re-run the focused guard and full `npm run verify`.
6. Produce visual smoke evidence via Vite preview or a dependency-light artifact, without overstating it as automated screenshot coverage.
7. Reconcile `tasks.md` evidence before final review/PR.

## Complexity Tracking

No constitution violations are planned. Spec Kit overhead is justified by the browser/player-facing UX surface and autonomous handoff; implementation remains small and CSS-focused.
