# Feature Specification: Browser card spacing polish

**Feature Branch**: `787-browser-card-spacing`

**Created**: 2026-06-08

**Status**: Draft

**Input**: GitHub issue #787: "feat(browser): улучшить внутренние отступы и spacing карточек"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #787 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/787
- **Issue type**: enhancement / browser visual polish
- **Spec Kit justification**: The issue changes a player-facing Browser Client surface and spans shared card/sidebar/detail-surface styling, so durable browser UX acceptance and verification evidence are useful for autonomous handoff.
- **Contract scope**: player-facing browser/frontend visual presentation only.
- **Out of scope**: No runtime-state, validation, GM prompt, game-contract, command coverage, data-loading, generated art, or gameplay changes. Do not resurrect obsolete card-heavy Feature-branch flows; preserve the current minimalist browser direction with top tabs, a single console-like input, and `/help` command discovery.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Comfortable card content spacing (Priority: P1)

A player reading the browser client should see ShellPanel, summary cards, narrative cards, and detail-surface summaries with consistent breathing room instead of text appearing cramped against borders.

**Why this priority**: This is the direct complaint in #787 and affects the whole player-facing browser shell.

**Independent Test**: Source/style guards can assert the shared card classes use desktop/mobile spacing tokens and consistent internal gaps; Vite preview visual smoke can inspect the default layout.

**Acceptance Scenarios**:

1. **Given** the browser shell on a desktop viewport, **When** card-like player-facing surfaces render, **Then** their internal padding is at least `--space-4` where the layout is not explicitly compact.
2. **Given** the browser shell on a mobile viewport, **When** the responsive rules apply, **Then** card padding remains at least `--space-3` instead of collapsing into edge-hugging content.
3. **Given** cards with eyebrow/title/content structure, **When** the content is read vertically, **Then** the gaps between eyebrow, title, and body are consistent and not dependent on one-off margins.

### User Story 2 - Detail summary and sidebar spacing (Priority: P2)

A player using collapsed detail cards or the right status/sidebar should see values and summaries laid out freely enough to scan without crowding.

**Why this priority**: #787 explicitly names DetailSurfaceCard summaries and PlayerStatusSidebar/right panel spacing.

**Independent Test**: Source/style guards can assert the summary/detail/sidebar selectors use the same spacing system and do not regress to smaller ad-hoc padding.

**Acceptance Scenarios**:

1. **Given** a collapsed detail surface summary, **When** it renders a title, summary line, and metadata/value rows, **Then** those elements have tokenized gaps and card padding that match the shared card rhythm.
2. **Given** the right player-status/sidebar panel, **When** status summary cards render, **Then** each card uses player-facing spacing tokens rather than cramped local values.

## Edge Cases

- When a card is intentionally compact, it may use a smaller local layout only if it still keeps the #787 minimum (`--space-4` desktop, `--space-3` mobile) for player-facing card interiors.
- Mobile breakpoints must not reduce padding below `--space-3` for the affected card/sidebar surfaces.
- The implementation must keep default UI player-facing: no raw `/api/`, endpoint names, debug, DTO, or raw JSON wording should be introduced.
- CSS-only presentation changes must not change game state, prompt contracts, command semantics, save/load behavior, or console behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Browser Client MUST define a reusable card spacing rhythm for ShellPanel, summary-card, narrative-card, DetailSurfaceCard summaries, and status/sidebar cards using existing spacing tokens.
- **FR-002**: Desktop card interiors named by #787 MUST keep at least `var(--space-4)` padding unless an existing accessible control requires a larger value.
- **FR-003**: Mobile card interiors named by #787 MUST keep at least `var(--space-3)` padding at the relevant responsive breakpoint.
- **FR-004**: Eyebrow/title/content stacks inside affected cards MUST use consistent tokenized gaps instead of uneven one-off margins.
- **FR-005**: PlayerStatusSidebar/right-status cards MUST receive normal internal padding and gaps consistent with the shared card rhythm.
- **FR-006**: DetailSurfaceCard summary/collapsed surfaces MUST remain readable and scan-friendly without hiding existing player-facing summary data.
- **FR-007**: The change MUST include focused automated coverage or source/style guards that would fail if the spacing classes revert to cramped values.
- **FR-008**: The change MUST not introduce runtime-state, GM-facing, afterlife/mortal contract, generated-art, or gameplay logic changes.

### Key Entities

- **Card-like browser surfaces**: Shell panels, summary cards, narrative cards, detail-surface summaries, and status/sidebar cards rendered by the React/Vite Browser Client.
- **Spacing tokens**: Existing CSS custom properties such as `--space-3` and `--space-4` that define the visual rhythm.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A focused frontend guard/test fails when affected card styles drop below the #787 desktop/mobile spacing minimums.
- **SC-002**: `npm run verify --prefix BookOfEternityClient.WebFrontend` passes after the changes.
- **SC-003**: A Vite preview visual smoke or generated local visual artifact records that the default browser layout no longer looks cramped in the affected card/sidebar surfaces.
- **SC-004**: `git diff --check origin/main...HEAD` and the added-line static scan report no whitespace or security/injection blockers.

## Verification Plan *(mandatory)*

- **C# verification**: N/A unless implementation unexpectedly touches C# host/tests.
- **Documentation/contract verification**: N/A; no GM prompts, examples, validation, or runtime contracts are in scope.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend`; focused style/source guard(s) added or updated for #787 spacing; optional targeted `vitest` command if new guard lives in a specific frontend test.
- **Manual/player-facing verification**: Vite preview visual smoke for the default Browser Client layout, including desktop and mobile-width inspection when practical. If browser automation is unavailable in the Codex run, create a dependency-light local visual smoke artifact under `TestResults/browser-smoke/` and describe the limitation.

## Assumptions

- Existing React components remain presentation-only over shared C# runtime state.
- Existing Browser Client design direction remains minimalist and command-composer-first; this task is spacing polish, not a navigation or control-panel redesign.
- Existing CSS tokens (`--space-3`, `--space-4`, and related gaps) are the preferred authority for consistent spacing.
- If a broader card hierarchy or action-button redesign is needed, that belongs to sibling issues #788/#791 rather than this #787 closure unit.
