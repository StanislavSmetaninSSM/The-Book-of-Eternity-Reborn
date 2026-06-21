# Implementation Plan: Browser Block Renderer Rich Command Output

**Branch**: `work/1126-block-renderer` | **Date**: 2026-06-21 | **Spec**: `specs/1126-browser-block-renderer/spec.md`

**Input**: Feature specification from `specs/1126-browser-block-renderer/spec.md`

## Summary

Close #1126 by making the React command-result renderer preserve typed `UiBlock` structure, hide raw diagnostics by default, and render actions/tables/nested content as readable player-facing UI. The implementation path is frontend TDD: add focused failing tests against representative fixtures, then make the smallest React/CSS changes needed inside the existing dark-fantasy design system.

## Technical Context

**Language/Version**: TypeScript, React, Vite

**Primary Dependencies**: Existing frontend React components and CSS; no new UI libraries planned.

**Storage**: None.

**Testing**: Existing frontend test runner through package scripts.

**Target Platform**: Browser client over local game backend.

**Project Type**: React frontend inside a desktop/local game repo.

**Performance Goals**: Component rendering remains lightweight; avoid expensive per-render transformations for static command-result blocks.

**Constraints**: Preserve PR #1116 dark-fantasy style, avoid generic component libraries, and keep diagnostics gated behind advanced mode.

**Scale/Scope**: Generic renderer and closely related CSS/tests only.

**Source Issue(s)**: #1126 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1126

**Contract Scope**: Frontend rendering of existing DTOs. Backend DTO changes are out of scope unless tests prove a missing field.

**Verification Commands**:

- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `git diff --check`

## Constitution Check

- **GitHub traceability**: PASS. #1126 is linked in spec, plan, and tasks.
- **Spec Kit fit**: PASS. This is broad browser UX and parity work.
- **Player-facing integrity**: PASS. Typed block fidelity and hidden diagnostics are explicit requirements.
- **Contract/state authority**: PASS. No GM/game-state/afterlife contract changes are planned.
- **Test-first path**: PASS. Renderer changes require failing frontend tests first.
- **Verification evidence**: PASS. Frontend verify, diff check, and screenshots are required.
- **Agent orchestration**: PASS. Work is local Codex implementation, no delegation.

## Project Structure

### Documentation (this feature)

```text
specs/1126-browser-block-renderer/
├── spec.md
├── plan.md
└── tasks.md
```

### Source Code (repository root)

```text
BookOfEternityClient.WebFrontend/
├── src/api/contracts.ts
├── src/components/BlockRenderer.tsx
├── src/components/CommandResultView.tsx
├── src/styles/components.css
└── test/
    ├── blockRenderer.test.ts
    └── commandResultViewSections.test.ts
```

**Structure Decision**: Keep the renderer generic and CSS-token-based; avoid command-specific React branches unless the DTO itself carries a distinct typed block.

## Complexity Tracking

No constitution violations.
