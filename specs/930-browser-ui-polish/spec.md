# Feature Specification: Browser UI Dark-Fantasy Polish

**Feature Branch**: `boe/930-browser-ui-polish`
**Created**: 2026-06-11
**Status**: Draft for autonomous implementation
**Input**: GitHub issue [#930](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/930), parent epic [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680), related generated-art task [#929](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/929)

## Source Issues & Scope

- **Source GitHub issue(s)**: [#930 Polish Browser UI](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/930)
- **Parent / related issues**: Parent browser epic [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680); generated-art coordination issue [#929](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/929)
- **Issue type**: Player-facing browser UI polish task.
- **Spec Kit justification**: The issue is broad player-facing browser UX work, spans React, CSS, frontend tests, C# browser smoke guards, and visual QA artifacts, so it meets the project Spec Kit policy for durable planning.
- **Contract scope**: Player-facing browser/frontend, source guards, local browser smoke artifact generation, documentation/audit evidence. No GM prompt, runtime-state, validation, save, command, or afterlife contract changes are in scope.
- **Out of scope**: Gameplay rules, save format, command semantics, GM prompts, afterlife contracts, console behavior, Tailwind/shadcn/CSS-in-JS migration, new package dependencies, issue #929 generated asset catalog, issue #940, and runtime image-generation changes.

## User Scenarios & Testing

### User Story 1 - Coherent Game Client Shell (Priority: P1)

A player opens the Browser Client and sees a cohesive dark-fantasy game client rather than a technical web shell. Launcher, scene, status, help, settings, QTE practice, command result, and afterlife/Reborn panels share the same ink/parchment/gold visual language.

**Why this priority**: This is the core of issue #930 and gives the Browser Client a coherent first impression without changing gameplay authority.

**Independent Test**: Build the frontend and inspect source/visual smoke artifacts for unified tokens, navigation states, readable Russian labels, and no default debug/API surfaces.

**Acceptance Scenarios**:

1. **Given** the player is on any default route, **When** the route renders, **Then** the route uses the shared dark-fantasy design system and does not fall back to generic blue-gray shell styling.
2. **Given** the player leaves the launcher, **When** the tab bar renders, **Then** all current player tabs including QTE practice are represented as game sections with stable shortcuts and non-emoji styled glyphs.
3. **Given** a narrow viewport, **When** the player navigates between tabs or uses command/status/settings panels, **Then** labels wrap or compact without horizontal overflow or clipped Russian text.

---

### User Story 2 - Advanced Details Stay Explicit (Priority: P1)

A player should not see API, DTO, endpoint, file-path, raw JSON, debug, validation, or repair internals in default mode. Those details remain available only after the explicit advanced-mode switch.

**Why this priority**: The constitution requires the browser client to be a player-facing game client, not a debug terminal.

**Independent Test**: Add or update source guards and smoke artifacts that fail if default frontend source or generated player artifacts expose raw technical language.

**Acceptance Scenarios**:

1. **Given** advanced mode is off, **When** default launcher, scene, status, help, settings, QTE, media, command result, and Reborn surfaces render, **Then** they use in-world Russian copy and hide raw technical details.
2. **Given** advanced mode is on, **When** diagnostics are opened, **Then** endpoint and DTO language remains confined to explicit diagnostic surfaces.

---

### User Story 3 - Verified First-Pass Polish Evidence (Priority: P2)

Maintainers need a closure-oriented artifact and tests proving what was polished, what was intentionally left out, and how #930 differs from #929.

**Why this priority**: The issue requires first-pass evidence without turning the task into an unbounded redesign or generated-art catalog.

**Independent Test**: Run frontend verification, focused browser/local-web .NET tests, static scans, and inspect the feature audit artifact.

**Acceptance Scenarios**:

1. **Given** the feature is ready for handoff, **When** maintainers review `specs/930-browser-ui-polish/ui-audit.md`, **Then** it lists baseline findings, applied skill/guideline set, implementation evidence, and screenshot/artifact limitations.
2. **Given** the built-frontend smoke guard runs, **When** it parses current navigation, **Then** it reflects the current player tab model and does not hide the QTE practice tab to satisfy stale assertions.

## Edge Cases

- The current frontend includes a fifth player tab, `Тренировка`, so visual smoke artifacts and guards must reflect 5 player tabs instead of the stale 4-tab expectation.
- `command-ui.css` is imported after the design-system files and can override the shell; this polish must keep those overrides intentional and aligned with the dark-fantasy tokens.
- Russian labels, long status names, command result text, and settings rows must not overflow narrow viewports.
- Reduced-motion and contrast-friendly settings must keep the UI readable and avoid unnecessary animation.
- Generated HTML smoke artifacts are not real screenshots; if live browser screenshots cannot be captured, the final report must call that out clearly.

## Requirements

### Functional Requirements

- **FR-001**: Default Browser Client UI MUST present launcher, game scene, status/soul/world, journal/help, inventory-adjacent command surfaces, media/QTE, settings, command results, and Reborn/afterlife panels with a coherent dark-fantasy visual system.
- **FR-002**: Default player UI MUST NOT expose raw API, DTO, endpoint, file-path, raw JSON, debug, repair, validation, or command-coverage language outside explicit advanced mode.
- **FR-003**: Navigation guards and smoke artifacts MUST reflect the current player tab model, including `Тренировка`, without treating player-facing QTE practice as a debug surface.
- **FR-004**: The CSS design system MUST consolidate legacy token aliases and fallback colors that currently cause generic blue-gray shell styling.
- **FR-005**: Player-facing tab/navigation icons MUST avoid default emoji markers where this issue touches navigation and should use styled local glyph rendering instead of adding an icon package.
- **FR-006**: Layout polish MUST protect Russian text fitting, keyboard focus visibility, contrast over image/background surfaces, mobile responsiveness, and reduced-motion behavior.
- **FR-007**: TypeScript MUST remain presentation/request-state code only; C# runtime/gameplay authority, command semantics, persistence, validation, saves, and afterlife contracts MUST remain unchanged.
- **FR-008**: The implementation MUST avoid runtime image generation and large generated bitmap asset catalog work; any static asset coordination with #929 must be documented if it becomes necessary.
- **FR-009**: The feature MUST produce issue-local audit/evidence documentation and verification results for Hermes/manual follow-up.

### Key Entities

- **Browser Player Shell**: React/Vite UI containing launcher, tab bar, content area, command input, route views, and optional advanced diagnostics.
- **Design Tokens**: Plain CSS variables in `src/styles/tokens.css` and imported style modules that define dark-fantasy surfaces, text, state, focus, spacing, and motion.
- **Visual Smoke Artifact**: Dependency-light HTML evidence under `TestResults/browser-smoke/`, generated by focused .NET browser/local-web tests.
- **Advanced Mode**: Explicit user opt-in that allows technical diagnostics and raw implementation detail surfaces.

## Success Criteria

### Measurable Outcomes

- **SC-001**: `npm run verify --prefix BookOfEternityClient.WebFrontend` completes with zero failing frontend/type/build checks.
- **SC-002**: The required focused .NET browser/local-web test command completes with zero failing tests, and the known tab-count smoke failure is resolved by reflecting the current player tabs.
- **SC-003**: Source guards or focused tests cover default player UI against raw endpoint/DTO/debug/file-path/API language leakage.
- **SC-004**: At least one visual QA artifact or documented browser screenshot flow records the before/after audit and current limitations.
- **SC-005**: `git diff --check` and added-line static scans report no whitespace, obvious secret, or accidental technical-copy regressions in changed frontend source.

## Verification Plan

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiDocumentationTests|FullyQualifiedName~LocalWebUi|FullyQualifiedName~Browser" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: N/A for GM/afterlife contracts because this feature is UI-only and does not change runtime contracts; inspect source guards and issue-local audit artifact.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`
- **Manual/player-facing verification**: Browser or browser-act verification of local preview when feasible; otherwise explicitly report dependency-light HTML artifacts as artifacts, not screenshots.
- **Hygiene verification**: `git diff --check` plus added-line static scan for raw endpoint/DTO/debug/file-path/API leaks, secrets, and injection hazards.

## Assumptions

- Hermes has already moved #930 to `status: in-progress`; Hermes owns PR creation, issue closure, Telegram reporting, and final acceptance.
- The implementation can update tests and local smoke artifact generation to reflect current route/tab reality as long as it does not weaken meaningful player-facing assertions.
- #930 is a focused first-pass polish closure; broad redesign, new visual asset catalog, and package/library migrations should become follow-up issues.
- Browser screenshots are desirable, but if the local browser tool cannot capture real screenshots in this environment, the final report will distinguish offline artifacts from screenshots.
