# Feature Specification: Browser card hierarchy and status styling

**Feature Branch**: `task/788-browser-card-hierarchy`

**Created**: 2026-06-09

**Status**: Draft

**Input**: GitHub issue #788: "feat(browser): доработать дизайн карточек — визуальная иерархия и разнообразие"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #788 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/788
- **Issue type**: enhancement / browser visual polish
- **Spec Kit justification**: The issue changes player-facing Browser Client UX across shared card surfaces, action cards, status meters, hover affordances, and key-value typography. It spans multiple frontend files/tests and is a durable visual-design sibling to #787, so Spec Kit acceptance and handoff evidence are required.
- **Contract scope**: player-facing browser/frontend visual presentation only.
- **Out of scope**: No runtime-state, validation, GM prompt, game-contract, command coverage, save/session lifecycle, C# gameplay, afterlife/mortal contract, generated art, or console changes. Preserve the current minimalist browser direction with top tabs, a single console-like input, and `/help` command discovery; do not resurrect obsolete card-heavy Feature-branch flows.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Distinguishable action and information cards (Priority: P1)

A player on the browser Home/Game surfaces should immediately distinguish primary actions such as "Продолжить главу" or "Настроить книгу" from passive information cards.

**Why this priority**: #788 explicitly says all cards look like the same dark rectangle and calls out action-card differentiation as an expected result.

**Independent Test**: A focused frontend source/style guard can assert that action/primary launcher card selectors have distinct golden border, gradient/interactive treatment, and hover/focus affordances separate from ordinary info cards.

**Acceptance Scenarios**:

1. **Given** the Home launcher renders primary/selectable actions, **When** a player scans the card list, **Then** action cards use a visibly distinct golden/accent border and gradient/interactive surface rather than the plain info-card treatment.
2. **Given** a passive info card renders, **When** it is shown near action cards, **Then** it retains a calmer panel/glow style and is not mistaken for the primary CTA.
3. **Given** a clickable card/action receives hover or keyboard focus, **When** the pointer or focus reaches it, **Then** the UI gives obvious feedback without relying on technical/debug copy.

### User Story 2 - Status meters communicate severity (Priority: P1)

A player reading health, energy, poise, or comparable status bars should see threshold coloring that communicates good/warning/danger states.

**Why this priority**: #788 explicitly requires green above 66%, yellow above 33%, and red below that.

**Independent Test**: A component/unit test or source guard can assert that `StatusBar` derives semantic classes from numeric percentages and CSS defines good/warning/danger meter colors.

**Acceptance Scenarios**:

1. **Given** a status value greater than 66%, **When** `StatusBar` renders, **Then** its bar uses the good/green semantic class.
2. **Given** a status value greater than 33% and at most 66%, **When** `StatusBar` renders, **Then** its bar uses the warning/yellow semantic class.
3. **Given** a status value at or below 33%, **When** `StatusBar` renders, **Then** its bar uses the danger/red semantic class.
4. **Given** malformed or missing values, **When** `StatusBar` renders, **Then** it falls back safely to the danger/empty state without displaying duplicate percent signs or runtime errors.

### User Story 3 - Key-value typography is scannable (Priority: P2)

A player reading `kv-list` details inside launcher/detail surfaces should distinguish labels from values at a glance.

**Why this priority**: #788 calls out `dt`/`dd` blending inside DetailSurface-like key-value lists.

**Independent Test**: A style guard can assert that `.kv-list` uses a grid layout with muted `dt`, brighter `dd`, and spacing/weight differences.

**Acceptance Scenarios**:

1. **Given** a key-value list renders in the launcher or detail surface, **When** labels and values are read, **Then** `dt` text is muted/secondary and `dd` text is brighter or stronger.
2. **Given** multiple key-value rows render, **When** the list wraps on narrow screens, **Then** the layout remains readable and does not collapse labels and values into a single indistinct block.

## Edge Cases

- Disabled actions must remain visibly disabled and must not receive the same active hover/CTA treatment as enabled actions.
- Hover/focus affordances must be keyboard-accessible and must not remove existing `disabled` behavior.
- Status values may include `%`, whitespace, empty strings, or non-numeric labels; parsing must clamp to 0–100 and choose a safe severity class.
- CSS-only/player-facing visual changes must not change game state, prompt contracts, command semantics, save/load behavior, console behavior, or GM-facing docs.
- Default browser UI must remain Russian/player-facing and must not expose raw `/api/`, endpoint, DTO, debug, raw JSON, or agent meta-language.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Browser Client MUST visually distinguish action/CTA cards from passive information cards using existing design tokens or new local presentation tokens scoped to the frontend.
- **FR-002**: Primary action cards, especially launcher actions such as "Продолжить главу" and settings/new-chapter actions, MUST use a golden/accent border and gradient or glow treatment that is clearly interactive.
- **FR-003**: Passive info cards MUST remain calmer than action cards and must not inherit the strongest CTA treatment.
- **FR-004**: Clickable cards/actions MUST provide hover and focus-visible feedback; disabled actions MUST not appear active.
- **FR-005**: `StatusBar` MUST classify numeric percentage values as good when `> 66`, warning when `> 33` and `<= 66`, and danger when `<= 33` or unparsable.
- **FR-006**: Status meter CSS MUST define visually distinct good/warning/danger fills while preserving readable labels and avoiding broad cascade regressions.
- **FR-007**: `.kv-list` MUST use scannable typography/layout: muted labels, stronger values, tokenized gaps, and responsive wrapping/grid behavior.
- **FR-008**: The change MUST include focused automated coverage or source/style guards that would fail if action-card differentiation, status severity colors, or key-value typography regress.
- **FR-009**: The change MUST not introduce runtime-state, GM-facing, afterlife/mortal contract, generated-art, gameplay logic, or console-client changes.

### Key Entities

- **Action card / CTA surface**: A player-facing browser element that starts or selects an action, including launcher menu items and action cards rendered by the React/Vite frontend.
- **Info card**: Passive player-facing browser panel summarizing status, text, or details without acting as a primary CTA.
- **StatusBar severity**: The good/warning/danger class derived from a numeric percent value.
- **Key-value list**: `.kv-list` data-display pattern for `dt`/`dd` label/value pairs.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A focused frontend guard/test fails if CTA/action card selectors lose golden/accent/gradient/hover/focus differentiation from passive cards.
- **SC-002**: A focused `StatusBar` test or guard proves the `>66`, `>33`, and `<=33` thresholds render distinct semantic classes.
- **SC-003**: A focused frontend guard/test fails if `.kv-list dt` and `.kv-list dd` typography becomes indistinguishable.
- **SC-004**: `npm run verify --prefix BookOfEternityClient.WebFrontend` passes after the changes with non-zero Vitest/source-guard counts.
- **SC-005**: Vite preview visual smoke or a dependency-light local visual artifact records that action cards, info cards, status meters, and key-value lists are visually differentiated. If no real browser screenshot is captured, the artifact must be described as visual-smoke HTML, not a screenshot.
- **SC-006**: `git diff --check origin/main...HEAD` and the added-line static scan report no whitespace or security/injection blockers.

## Verification Plan *(mandatory)*

- **C# verification**: N/A unless implementation unexpectedly touches C# host/tests.
- **Documentation/contract verification**: N/A; no GM prompts, examples, validation, or runtime contracts are in scope.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`; focused Vitest/source guard(s) for #788 action-card hierarchy, status severity thresholds, and `.kv-list` typography.
- **Manual/player-facing verification**: Vite preview visual smoke for the default Browser Client layout and responsive/narrow behavior when practical. If browser automation is unavailable in the Codex run, create a dependency-light local visual smoke artifact under `TestResults/browser-smoke/` and describe the limitation.

## Assumptions

- Existing React components remain presentation-only over shared C# runtime state.
- Existing Browser Client direction remains minimalist and command-composer-first; #788 is visual hierarchy polish, not a navigation taxonomy or full action-palette redesign.
- #787 spacing has already landed in `main`; #788 should build on that spacing rather than replace it.
- #790 owns sidebar navigation polish and #791 owns broader Home page CTA/visual hierarchy if their issue-specific acceptance goes beyond shared card styling.
