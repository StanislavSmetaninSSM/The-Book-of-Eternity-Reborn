# Feature Specification: World News Detail Footer Depth

**Feature Branch**: `fix/1112-world-news-detail-footer-depth`

**Created**: 2026-06-19

**Status**: Draft

**Input**: User description: "При выборе новости почти нет деталей; внизу появляется странная непонятная надпись."

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1112](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1112)
- **Issue type**: bug / UX hardening
- **Spec Kit justification**: Player-facing console/browser command detail output and console navigation are affected.
- **Contract scope**: player-facing, console, browser, shared command DTO, local test game_session data
- **Out of scope**: GM prompt/schema changes and validator changes. The fix renders and/or seeds existing player-facing fields.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Clear Detail View (Priority: P1)

A player selects a world news entry and sees meaningful details instead of a nearly empty panel.

**Why this priority**: The drilldown exists to explain the selected news item.

**Independent Test**: A Valmont-style event with detail fields renders those fields in the detail command and hides technical ids.

**Acceptance Scenarios**:

1. **Given** a world event has player-facing detail fields, **When** the player opens it, **Then** the panel shows the details with readable labels.
2. **Given** a world event has technical ids, **When** the player opens it, **Then** those ids are not shown as detail content.
3. **Given** a world event has nested objects or arrays, **When** the player opens it, **Then** nested values are formatted as readable rows or bullet-like lines rather than one long semicolon-separated blob.
4. **Given** a world event has known English contract keys, **When** the player opens it, **Then** those keys are rendered with Russian player-facing labels.

### User Story 2 - Clean Console Navigation (Priority: P2)

A player opens a world news detail from the console selector and sees only the detail panel followed by a clear back/close selector.

**Why this priority**: The current footer/prompt mix is confusing and looks broken.

**Independent Test**: Console selection detail output does not render the command footer text before the back/close prompt and still offers `Назад к списку`.

**Acceptance Scenarios**:

1. **Given** a player opens an event from `/новости_мира`, **When** the detail is shown, **Then** no stray "Вернуться к сводке..." footer appears in the rendered detail.
2. **Given** the detail is shown, **When** the navigation prompt appears, **Then** the choices are `Назад к списку` and `Закрыть`.
3. **Given** a player opens an event directly through `/новости_мира событие <id>`, **When** the detail is rendered in the console, **Then** no generic command/action table is shown at the bottom.

### Edge Cases

- Sparse events should still show all available fields without inventing data.
- Rich events should not collapse nested player-facing objects to a single name.
- Console and browser details should share the same DTO content, except console may suppress footer text in interactive mode.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: World news event detail MUST render all meaningful player-facing fields present in the event record.
- **FR-002**: Valmont test game_session data MUST include enough event detail fields to demonstrate the detail view.
- **FR-003**: Console interactive world news detail MUST NOT render confusing footer text before the detail navigation prompt.
- **FR-004**: Detail actions/back navigation MUST remain available for browser/shared DTO consumers.
- **FR-005**: Technical ids/raw/debug/path/url fields MUST remain hidden from player-facing detail content.
- **FR-006**: Console world-news detail MUST suppress generic `Actions` tables; browser/shared DTO actions may remain available.
- **FR-007**: Known world-news contract keys MUST have Russian labels instead of English fallback labels.

### Key Entities

- **World Event Detail**: Player-facing fields on a world event record, including description, summary, evidence, stakes, leads, witnesses, and consequences.
- **Console Detail Navigation**: The interactive prompt shown after a selected detail, offering return to list or close.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: A selected Valmont world event displays at least five player-facing detail rows beyond marker/time/status.
- **SC-002**: The console rendered detail output contains no footer/prompt text collision.
- **SC-003**: Focused automated tests cover rich detail fields, footer suppression, and return-to-list navigation.
- **SC-004**: Focused automated tests fail if direct console detail renders `Доступные действия`, `Команда`, `/новости_мира`, or `Secondary` as a bottom action table.
- **SC-005**: Focused automated tests fail if known fields such as `opportunity`, `openQuestions`, or `playerKnowledge` render as English labels.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true -p:UseSharedCompilation=false --filter "WorldNewsEventDetail|WorldNews_ConsoleSelection|WorldNews_ConsoleExposesSharedEventFlagAndProgressionDrilldowns" --logger "console;verbosity=minimal"`
- **Documentation/contract verification**: N/A; client-owned rendering over existing fields.
- **Frontend verification**: N/A.
- **Manual/player-facing verification**: Run `/новости_мира`, open `Письмо появилось ночью`, verify useful details and clean back/close prompt.

## Assumptions

- Current local `game_session` is test data and may be enriched without changing GM contracts.
- Browser consumers can use actions instead of a text footer to return to overview.
