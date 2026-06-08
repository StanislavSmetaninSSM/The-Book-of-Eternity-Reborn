# Feature Specification: Browser Soul page empty states and player copy

**Feature Branch**: `task/789-browser-soul-empty-states`

**Created**: 2026-06-09

**Status**: Draft

**Input**: GitHub issue #789: "feat(browser): страница Душа — пустые поля и плейсхолдерный контент"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #789 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/789
- **Issue type**: enhancement / Browser Client player-facing UX polish
- **Spec Kit justification**: The issue changes player-facing Browser Client status/soul presentation, empty-state behavior, detail/summary copy, and status-meter presentation across frontend source/tests and may require browser source guards. It is a durable UX surface within the Browser Client roadmap, so Spec Kit acceptance and handoff evidence are required.
- **Contract scope**: player-facing browser/frontend presentation only. No game-state, validation, pending/control, GM prompt, example, afterlife/mortal runtime contract, or console-client behavior changes are in scope.
- **Current architecture note**: The issue body uses older names such as `SoulRoute` and `DetailSurfaceCard`. Current `main` exposes this player-facing surface through `BookOfEternityClient.WebFrontend/src/components/StatusView.tsx` under the Soul/status tab. Implement against the current React architecture rather than recreating obsolete components.
- **Out of scope**: Sidebar navigation polish (#790), Home launcher CTA/hero work (#791), command/action palette redesign, C# gameplay/runtime logic, new endpoints, save/session lifecycle, GM-facing docs/examples, generated art, or console UI changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Empty character and soul fields feel intentional (Priority: P1)

A player opening the Soul/status page before full hero creation should see a polished in-world empty state rather than blank values or repeated "не указано" placeholders.

**Why this priority**: #789 names empty hero/soul fields as the first visible problem on the Soul page.

**Independent Test**: A focused frontend guard/component test can assert that `StatusView` detects missing player/soul identity fields and renders player-facing empty-state copy/classes instead of raw blank `<dd>` values.

**Acceptance Scenarios**:

1. **Given** player name, class, race, guardian, or soul name is empty or placeholder-like, **When** the Soul/status page renders, **Then** the UI shows an in-world empty-state message explaining that the chapter has not recorded those details yet.
2. **Given** meaningful player/soul details exist, **When** the Soul/status page renders, **Then** the UI shows those values directly and does not replace them with the empty-state card.
3. **Given** optional world/afterlife fields are absent, **When** the page renders, **Then** the UI uses concise player-facing fallback text without raw API/debug language.

### User Story 2 - Soul details are readable without a hidden click target (Priority: P1)

A player should not need to discover that a compact summary is clickable to understand the Soul/status page.

**Why this priority**: #789 says collapsed detail summaries such as "Душа - Мир смертных" hide the useful information and make the surface feel like a placeholder.

**Independent Test**: Source guards can assert that the current Soul/status page renders open/readable sections by default and does not use a collapsed-only detail summary as the primary information path.

**Acceptance Scenarios**:

1. **Given** the Soul/status tab is opened, **When** status, character, soul, and world cards are available, **Then** the important details are visible immediately in readable sections.
2. **Given** there is no data yet, **When** the page renders, **Then** the empty-state card is visible immediately with a clear next-step hint instead of a terse summary-only surface.

### User Story 3 - Status meters look meaningful on initial data (Priority: P2)

A player seeing 100% health/energy/poise should understand these are current full values, not unstyled placeholders.

**Why this priority**: #789 explicitly calls out status bars displaying 100% without enough color differentiation. #788 has already added semantic status-meter classes; #789 should preserve and use that system for the Soul/status page.

**Independent Test**: Existing and new guards can assert that `StatusView` status meters use semantic good/warning/danger classes, accessible meter metadata, and player-facing labels.

**Acceptance Scenarios**:

1. **Given** health/energy/poise values are `100%`, **When** the page renders, **Then** the meters use the good semantic class and player-facing label/value typography.
2. **Given** the values are low or malformed, **When** the page renders, **Then** the meters clamp safely and use warning/danger semantics without duplicate percent signs.
3. **Given** meter labels are read by assistive technology, **When** the track is focused/read, **Then** it exposes meter metadata rather than a decorative-only bar.

### User Story 4 - Intro/details copy sounds like the game (Priority: P2)

A player should read Russian in-world copy, not system or diagnostic text, in the Soul/status details.

**Why this priority**: #789 calls out `detailsIntro` as system text that does not help the player.

**Independent Test**: A frontend player-facing guard can reject raw/system phrases (`detailsIntro`, DTO/API/debug wording, raw JSON, endpoint terms) in default Soul/status copy and assert the desired Russian text snippets.

**Acceptance Scenarios**:

1. **Given** the Soul/status page renders, **When** the player reads introductory or empty-state text, **Then** it uses concise game-facing Russian copy.
2. **Given** advanced/debug mode is not active, **When** the page renders, **Then** it does not expose raw `detailsIntro`, `/api/`, DTO, endpoint, validation/debug, or JSON terminology.

## Edge Cases

- Empty strings, whitespace, null/undefined values, and placeholder-like values such as `не указан`, `не указана`, `не назначен`, `unknown`, `n/a`, and `—` should be treated as missing for empty-state decisions where appropriate.
- Numeric zero values such as Ink Feathers `0`, turn number `0/1`, or afterlife counts should remain meaningful values, not missing data.
- Real API/session failures must still render visible warning/error states; friendly empty states must not hide failures.
- React remains presentation-only; do not invent gameplay rules, generated hero data, guardian assignment, or banner mechanics.
- If implementation discovers that current C# DTOs cannot support the desired presentation without runtime changes, stop and update/report the Spec Kit conflict before changing runtime contracts.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The Browser Client Soul/status page MUST render polished in-world empty states for missing player/soul identity fields instead of blank values or repeated placeholder values.
- **FR-002**: When meaningful player/soul values exist, the page MUST show them in readable label/value sections without hiding them behind collapsed-only summaries.
- **FR-003**: Missing-detail fallback copy MUST be Russian/player-facing and MUST NOT use raw API/DTO/debug/endpoint/JSON/agent terminology in the default UI.
- **FR-004**: Status meters on the page MUST keep #788 semantic severity classes, accessible meter metadata, readable labels, and safe percent parsing/clamping.
- **FR-005**: The implementation MUST include focused frontend guard/component tests that prove missing identity fields produce empty-state treatment, meaningful values remain visible, system/debug copy is absent, and status-meter semantics are preserved.
- **FR-006**: The change MUST remain frontend presentation-only unless a documented Spec Kit conflict proves a runtime change is required.
- **FR-007**: The implementation MUST preserve Browser Client current direction: minimalist tabs, single console-like command input, and `/help` command discovery.

### Key Entities

- **Soul/status page**: Current Browser Client React status tab implemented by `StatusView.tsx`; this is the issue's current `SoulRoute` equivalent.
- **Identity field**: Player/soul values such as hero name, class, race, current condition, soul name, active guardian, and comparable fields shown to the player.
- **Empty-state card**: A player-facing panel or inline state that explains missing/unrecorded details in-world without implying a broken session.
- **Status meter**: Health/energy/poise bar that maps percent values to semantic good/warning/danger classes and accessibility metadata.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A focused frontend test or source guard fails if missing player/soul identity values render as blank `<dd>` cells or repeated raw placeholders instead of the intended empty-state treatment.
- **SC-002**: A focused frontend test proves meaningful player/soul values remain visible in the normal state.
- **SC-003**: A guard fails if default Soul/status page copy includes `detailsIntro`, `/api/`, DTO, endpoint, debug, raw JSON, or agent terminology.
- **SC-004**: Existing/new status-meter guards pass and continue to prove good/warning/danger classes plus accessible meter metadata.
- **SC-005**: `npm run verify --prefix BookOfEternityClient.WebFrontend` passes after the changes with non-zero Vitest/source-guard counts.
- **SC-006**: Focused `.NET` browser frontend/workspace guards pass when React source expectations change.
- **SC-007**: Visual smoke evidence exists for missing-data and populated-data Soul/status states. If browser automation is unavailable, a dependency-light local HTML artifact under `TestResults/browser-smoke/` is acceptable but must not be described as a screenshot.
- **SC-008**: `git diff --check origin/main...HEAD` and added-line static scan report no whitespace or security/injection blockers.

## Verification Plan *(mandatory)*

- **Frontend verification**: focused Vitest/source guards for #789; `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **C# verification**: focused Browser frontend/source-smoke guard when React/source guard strings or built-frontend smoke expectations change: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~BrowserFrontendWorkspaceTests|FullyQualifiedName~LocalWebUiBuiltFrontendSmokeTests|FullyQualifiedName~LocalWebUiDocumentationTests" --logger "console;verbosity=minimal"` after restore/build prerequisites exist.
- **Documentation/contract verification**: N/A unless implementation changes runtime contracts or GM-facing docs, which is out of scope.
- **Visual verification**: Vite preview/browser inspection when practical; otherwise generate a dependency-light visual-smoke artifact under `TestResults/browser-smoke/` and label it accurately.

## Assumptions

- Current frontend data from `game-screen`/shell DTOs already contains enough values to distinguish missing identity fields from meaningful values.
- #788 semantic status-meter work is already in `main` and should be preserved, not replaced.
- #790 owns sidebar navigation polish and #791 owns Home launcher CTA/hero work; #789 should not absorb those sibling tasks.
- No GM-facing prompts/examples need updates because this issue changes only client presentation of existing state.
