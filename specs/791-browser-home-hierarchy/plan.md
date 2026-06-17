# Implementation Plan: Browser Home Action Hierarchy

**Source issue**: [#791](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/791)
**Parent epic**: [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)
**Feature spec**: `specs/791-browser-home-hierarchy/spec.md`
**Branch/worktree**: `work/791-browser-home-hierarchy` at `E:/Games/worktrees/boe-791-browser-home-hierarchy`

## Technical Context

- Browser frontend workspace: `BookOfEternityClient.WebFrontend/` (React 19, TypeScript, Vite, Vitest).
- Primary component: `BookOfEternityClient.WebFrontend/src/components/GameLauncher.tsx`.
- Primary styles: `BookOfEternityClient.WebFrontend/src/styles/components.css` and shared tokens in nearby CSS.
- Existing source guard: `BookOfEternityClient.WebFrontend/test/gameLauncherMenuLayout.test.ts` already checks launcher reachability, local `main-menu-bg.webp`, `.launcher-art-bg`, and basic menu classes.
- Existing player-facing frontend verification script: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- C# remains gameplay/application authority. React may only present action state from `BrowserMainMenuDto` and call existing shell/API paths already used by `GameLauncher`.

## Architecture

Keep the feature as a frontend presentation slice. `GameLauncher` should derive action labels, disabled reasons, and primary state from the existing `BrowserMainMenuDto` and local helper functions. Styling should use explicit launcher-scoped classes and data attributes so source guards can verify hierarchy without broad CSS rewrites. No C# runtime, GM docs, afterlife/mortal contracts, or console behavior should change.

## Files Expected to Change

- Modify: `BookOfEternityClient.WebFrontend/src/components/GameLauncher.tsx`
  - Add launcher-scoped action state/affordance markup, disabled reason rendering, validation warning element, and image fallback handling if needed.
- Modify: `BookOfEternityClient.WebFrontend/src/styles/components.css`
  - Add or refine `.launcher-menu__item` action affordance, disabled, warning, and ambient fallback styling.
- Modify: `BookOfEternityClient.WebFrontend/test/gameLauncherMenuLayout.test.ts`
  - Extend source/structure guard assertions for #791 acceptance.
- Optional modify/create: `BookOfEternityClient.WebFrontend/test/browserHomeHierarchy.test.ts` if Codex decides the existing guard would become too broad.
- Optional create: `TestResults/browser-smoke/home-launcher-hierarchy.html`
  - Dependency-light visual-smoke artifact, clearly labelled as an artifact rather than an automated screenshot.
- Optional modify: `BookOfEternityClient.Tests/*Browser*` only if existing C# browser source guards require new selector/asset expectations.

## Implementation Strategy

1. Baseline current frontend/browser tests before editing, with real counts.
2. Add or extend focused frontend guard(s) first so the current Home hierarchy gaps fail or use a documented temporary mutation proof if current code already satisfies part of the guard.
3. Implement minimal `GameLauncher` markup/state changes and CSS changes.
4. Add/update visual-smoke artifact if automated browser screenshots are not practical.
5. Run frontend verification, focused .NET browser source guards, diff check, and static scan.
6. Update `tasks.md` with exact RED/GREEN/verification evidence.
7. Leave independent review, PR, merge, issue comment, and issue closure to Hermes orchestration after Codex exits.

## Verification Commands

Codex should run the smallest useful sequence and report exact observed counts:

```bash
npm ci --prefix BookOfEternityClient.WebFrontend
npm run verify --prefix BookOfEternityClient.WebFrontend

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj \
  -p:IsTestProject=true \
  --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" \
  --logger "console;verbosity=minimal"

git diff --check origin/main...HEAD
```

If the focused .NET command no-ops or lacks test counts, rerun once without `--no-restore` and with `-p:IsTestProject=true`; do not treat an empty/no-count run as a pass.

## Risk and Rollback

- Risk: over-styling every card/action and regressing the current minimalist Browser direction. Mitigation: keep selectors scoped to `GameLauncher` and preserve existing tabs/composer architecture.
- Risk: accidentally inventing React-side save/load/new-chapter rules. Mitigation: reuse existing `findLauncherMenuAction`, route transitions, and `browserApi` calls only.
- Risk: visual source guards pass but UI remains hard to read. Mitigation: produce Vite preview evidence or a clearly-labelled local visual-smoke HTML artifact.
- Risk: primary checkout has unrelated dirty Daren prose changes. Mitigation: this feature runs in a separate ASCII worktree from `origin/main`; do not touch primary checkout changes.
