# Feature Specification: Browser Status and Effect Details

**Feature Branch**: `work/1091-browser-status`

**Created**: 2026-06-21

**Status**: Draft for implementation

**Input**: GitHub issue #1091 — "Browser: улучшить статус персонажа - bars, время, эффекты и detail-пути"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1091 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1091
- **Parent/related issues**: #1118 browser command output parity epic, #1087 browser player-copy cleanup, #855 Mortal status effect fallback.
- **Issue type**: Browser Client player-facing `/статус` and status/effect detail UX.
- **Spec Kit justification**: Required. The work changes browser/console semantic parity and player-facing command UX.
- **Contract scope**: Existing status/effects DTOs and browser command-result output. No new GM-authored state contract, validator rule, normalizer side effect, pending/control file, or afterlife contract is intended unless tests prove an existing DTO gap.
- **Out of scope**: Console rendering, NPC details (#1090), broad browser redesign, new effect mechanics, and QTE.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Read the character status at a glance (Priority: P1)

A browser player opening `/статус` can immediately understand health, energy, balance/poise, realm, location, and time without reading raw text or internal enum values.

**Independent Test**: Seed browser status data with health/energy/poise, Mortal World realm, and English-ish world time; execute `/статус` through browser command service and assert visual bars/cards and localized labels.

**Acceptance Scenarios**:

1. **Given** current player status has health, energy, and poise percentages, **When** `/статус` renders in browser, **Then** each value appears as a stable visual meter/card with a readable percent.
2. **Given** realm/time values are present, **When** `/статус` renders, **Then** default player output uses Russian player-facing labels and avoids raw enum/debug wording.

---

### User Story 2 - Inspect active effect details (Priority: P1)

A browser player can see active effects as useful summaries and open focused detail routes when structured data includes duration, source, mechanical modifiers, or narrative text.

**Independent Test**: Seed structured effects with detailed fields; execute `/статус`, assert effect detail actions; execute the effect detail command and assert full player-facing details plus back action.

**Acceptance Scenarios**:

1. **Given** structured visible effects exist, **When** `/статус` renders, **Then** effects show concise summaries and action buttons to inspect details.
2. **Given** a selected effect has duration/source/modifiers/narrative text, **When** the detail route opens, **Then** all player-facing details are visible without raw JSON.
3. **Given** an effect id is stale or missing, **When** the detail route opens, **Then** the browser returns a friendly unavailable message.

---

### User Story 3 - Empty effects remain pleasant (Priority: P2)

A browser player with no active effects sees a short empty state instead of missing-data or JSON-shaped output.

**Independent Test**: Seed status without structured effects and assert `/статус` renders current condition and a useful empty/fallback effect state.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Browser `/статус` MUST render health, energy, and balance/poise as stable visual bars/cards with labels and percentages.
- **FR-002**: Browser `/статус` MUST use player-facing Russian labels for realm, location, and time.
- **FR-003**: Browser `/статус` MUST render active effect summaries when current status or structured effect data contains visible conditions.
- **FR-004**: Browser `/статус` MUST expose read-only detail actions for visible structured effects that have additional detail fields.
- **FR-005**: Effect detail routes MUST show duration, source, mechanical modifiers, narrative text, and related visible metadata when present.
- **FR-006**: Missing or empty effects MUST render a human-readable empty state and MUST NOT expose raw JSON or file/API/debug wording by default.
- **FR-007**: Advanced diagnostics may still expose raw data only when advanced mode is enabled.
- **FR-008**: If implementation changes accepted GM-authored effect/status data shape, the PR MUST update GM-facing docs/examples/tests per `AGENTS.md`.

### Key Entities

- **Browser status surface**: Browser command-result output for `/статус`.
- **Status meter**: Player-facing visual representation of health, energy, or balance/poise.
- **Effect summary**: Short visible entry for an active condition/effect.
- **Effect detail route**: Read-only browser command opening one selected effect.

## Success Criteria *(mandatory)*

- **SC-001**: Focused xUnit browser command tests cover status meters, localized realm/time labels, effect summaries, effect detail route, and empty effects state.
- **SC-002**: Default browser output for covered status/effect cases contains no raw JSON/debug/internal API wording.
- **SC-003**: `dotnet test ... --filter "Status|Effect|ExplorerWebCommand"` passes for touched backend/browser command behavior.
- **SC-004**: If frontend React/CSS changes are required, `npm run verify --prefix BookOfEternityClient.WebFrontend` passes.
- **SC-005**: Browser Act screenshot evidence is captured for `/статус` and one effect detail route.

## Verification Plan *(mandatory)*

- **Audit files**: browser command builders/services, existing status/effect tests, `StatusView.tsx`, status CSS, and status/effect canonical state fixtures.
- **C# verification**: focused xUnit filters for browser status/effects and broader ExplorerWeb command coverage.
- **Frontend verification**: run only if React/CSS files change.
- **Style verification**: `git diff --check`.
- **Manual/browser verification**: Browser Act screenshots for `/статус` and effect detail output.

## Assumptions

- Existing backend status/effect data is enough for a useful browser output; new GM contract fields are not expected.
- Console client remains the semantic reference, but this task does not alter console rendering.
- Existing #855 fallback logic should be reused rather than duplicated where possible.
