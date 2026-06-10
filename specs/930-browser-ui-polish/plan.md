# Implementation Plan: Browser UI Dark-Fantasy Polish

**Branch**: `boe/930-browser-ui-polish` | **Date**: 2026-06-11 | **Spec**: `specs/930-browser-ui-polish/spec.md`
**Source Issues**: [#930](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/930), parent [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680), related [#929](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/929)

## Summary

Polish the Browser Client as a coherent dark-fantasy game client while preserving C# gameplay authority and keeping technical/debug surfaces behind explicit advanced mode. The first pass focuses on CSS token coherence, tab/navigation glyphs and current-tab smoke guards, command shell styling, responsive text/focus/reduced-motion quality, source guards, and audit/visual evidence.

## Technical Context

**Language/Version**: TypeScript, React 19, Vite 8, plain CSS, C#/.NET 8 tests and local web host.
**Primary Dependencies**: Existing React/Vite workspace, existing local Browser API DTOs, xUnit browser/local-web smoke tests, Vitest/player-facing source guards. No new package dependency planned.
**Storage**: N/A; no persistence, save, generated asset catalog, or game-state changes.
**Testing**: Frontend typecheck/player-facing tests/build, focused .NET browser/local-web tests, visual smoke HTML artifact generation, browser verification when feasible.
**Target Platform**: Local Windows worktree, loopback browser client, Vite preview/dev server, C# local web host.
**Project Type**: Local React browser frontend inside a C# game client repository.
**Performance Goals**: Avoid heavy always-loaded debug UI, broad runtime image work, or unnecessary re-render churn; keep derived UI state simple.
**Constraints**: TypeScript is presentation/request-state only; C# remains authoritative for gameplay, commands, validation, persistence, saves, and afterlife contracts. Do not touch #940 or implement #929 asset catalog work.
**Scale/Scope**: Focused first-pass polish across the current browser shell, CSS design-system layers, current player tabs, core route surfaces, source guards, and smoke artifacts.
**Contract Scope**: Player-facing browser/frontend, source guards, local smoke artifacts, audit docs. No GM-facing prompt/example or afterlife runtime contract changes.
**Verification Commands**:
- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~LocalWebUi|FullyQualifiedName~Browser" --logger "console;verbosity=minimal"`
- `git diff --check`
- Added-line static scan for raw endpoint/DTO/debug/file-path/API language, secrets, and injection hazards.

## Constitution Check

- **GitHub traceability**: #930 is the implementation issue; #680 and #929 are linked for epic/art coordination.
- **Spec Kit fit**: The issue is broad player-facing browser UX work and spans frontend code, CSS, tests, and smoke artifacts.
- **Player-facing integrity**: Default UI remains Russian/in-world and avoids debug/API leakage; advanced mode remains explicit.
- **Contract/state authority**: No gameplay, command, validation, pending/control, save, GM prompt, or afterlife contract changes are planned.
- **Test-first path**: Add failing source/smoke guard coverage before implementation for stale player tabs, emoji navigation, and command shell token drift.
- **Verification evidence**: Required frontend, focused .NET, diff hygiene, static scan, and browser/artifact evidence are listed.
- **Agent orchestration**: Spec Kit artifacts are created before code; Superpowers TDD/debugging/verification applies; Hermes owns PR and issue closure.

## Project Structure

### Documentation (this feature)

```text
specs/930-browser-ui-polish/
├── spec.md
├── plan.md
├── tasks.md
├── ui-audit.md
└── checklists/
    └── requirements.md
```

### Source Code (expected touch points)

```text
BookOfEternityClient.WebFrontend/src/components/tabBarConfig.ts
BookOfEternityClient.WebFrontend/src/components/TabBar.tsx
BookOfEternityClient.WebFrontend/src/styles/tokens.css
BookOfEternityClient.WebFrontend/src/styles/base.css
BookOfEternityClient.WebFrontend/src/styles/layout.css
BookOfEternityClient.WebFrontend/src/styles/components.css
BookOfEternityClient.WebFrontend/src/styles/command-ui.css
BookOfEternityClient.WebFrontend/test/*.test.ts(x)
BookOfEternityClient.Tests/BrowserFrontendWorkspaceTests.cs
BookOfEternityClient.Tests/LocalWebUiBuiltFrontendSmokeTests.cs
BookOfEternityClient.WebFrontend/README.md
```

**Structure Decision**: Keep the existing plain-CSS design-system modules and current React shell. Do not introduce Tailwind, CSS-in-JS, icon libraries, route rewrites, gameplay logic, or asset generation.

## Phase 0 - Research Notes

- `command-ui.css` is imported last and currently overrides `.browser-shell` to a flex shell with GitHub-like fallback colors. This is a design-system drift source and should be corrected at the command shell layer.
- `tabBarConfig.ts` currently contains 5 player tabs after QTE practice work: scene, practice, status, help, settings. The built-frontend smoke guard still asserts 4 tabs, which explains the known baseline failure.
- Player-facing navigation currently uses emoji strings in `tabBarConfig.ts`. #930 touches navigation, so replace those with local styled glyph identifiers/rendering rather than adding an icon package.
- Existing source guards already block many technical-copy leaks, but #930 should add guard coverage for the touched navigation/CSS/default artifact surfaces.
- Existing launcher background art is tracked by a prior issue; #930 should reuse it and not create an image-generation catalog.

## Phase 1 - Design Decisions

- Use semantic CSS aliases in `tokens.css` so legacy command UI selectors resolve to dark-fantasy surfaces instead of fallback blue-gray colors.
- Keep command UI in `command-ui.css`, but remove global shell overrides that fight `layout.css`.
- Convert tab icons from emoji payload strings to typed `glyph` identifiers rendered by a local inline SVG/glyph component.
- Update .NET smoke artifacts to show the current 5-tab player sequence and to preserve advanced/debug exclusion assertions.
- Add/update tests before implementation so stale tab count, emoji navigation markers, and command shell token drift fail first.
- Produce `ui-audit.md` as the closure-oriented before/after artifact and explicitly distinguish offline HTML artifacts from real screenshots.

## Risk Log

- **Risk**: Broad UI polish can grow without a stopping point.
  **Mitigation**: Limit edits to navigation/tab bar, design tokens, command shell styling, existing smoke guards, and focused evidence.
- **Risk**: CSS import order could regress other surfaces.
  **Mitigation**: Add source guards for intentional command UI behavior and run full frontend verify plus focused .NET browser tests.
- **Risk**: Removing emoji icons could break tests relying on exact `tabNav` shape.
  **Mitigation**: Update tests to assert semantic glyph ids and stable shortcuts instead of emoji strings.
- **Risk**: Browser screenshots may be unavailable.
  **Mitigation**: Use Browser plugin where feasible; otherwise report generated HTML smoke artifacts as offline artifacts only.

## Verification Plan

Baseline from Hermes before implementation:

- `npm ci --prefix BookOfEternityClient.WebFrontend` passed with 0 vulnerabilities.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` passed.
- Focused .NET browser/local-web baseline passed 351 tests and failed 1 known smoke guard: `LocalWebUiBuiltFrontendSmokeTests.BuiltFrontendSmoke_LaunchesHostWithViteDistAndCapturesDiagnostics`, `ExtractPlayerTabs` expected 4 actual 5.

Expected implementation verification:

- RED/GREEN focused tests for navigation tab contract, default player leak/source guards, and command shell CSS token drift.
- `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- Required focused .NET browser/local-web command from #930.
- Browser plugin or browser-act verification with screenshots when feasible; otherwise HTML visual artifact evidence only.
- `git diff --check`.
- Added-line static scan for raw endpoint/DTO/debug/file-path/API wording, secrets, and injection hazards.
