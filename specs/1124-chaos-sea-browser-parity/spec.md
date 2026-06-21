# Feature Specification: Chaos Sea Browser Command Output Parity

**Feature Branch**: `work/1124-chaos-browser`

**Created**: 2026-06-21

**Status**: Implementation evidence pass

**Input**: GitHub issue #1124 — "[Task] Fix Chaos Sea browser command output parity"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1124 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1124
- **Related prior specs/issues**: #949 audit plus #1063 Guardian/Abode drill-downs, #1064 Soul Relic/Archive drill-downs, #1066 profile/inbox follow-through, and #1067 spiritual conflict/art drill-downs.
- **Issue type**: Browser Client player-facing afterlife parity hardening and regression evidence.
- **Spec Kit justification**: Required. The issue spans afterlife/Chaos Sea player-facing browser UX, console/browser parity, hidden/GM-only data boundaries, and multiple shared C# command-result builders.
- **Contract scope**: Browser command-result output for existing read-only afterlife commands. No new runtime state schema, pending/control file, validator rule, normalizer side effect, GM prompt, or example contract is intended.
- **Out of scope**: Shining Abode parity (#1125), generic React block rendering (#1126), new afterlife mechanics, mutating action flows, and frontend visual redesign.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect Chaos Sea overviews (Priority: P1)

A browser player viewing Chaos Sea afterlife overviews can open one visible Guardian, Abode, Soul Relic, Archive row, profile, threat, chronicle, spiritual exchange, recent combat result, or spiritual art without reading raw JSON.

**Why this priority**: The browser client is only playable if dense afterlife state can be inspected as focused player-facing detail.

**Independent Test**: Seed representative afterlife state, execute each overview command, and assert safe secondary `UiAction` detail commands.

**Acceptance Scenarios**:

1. **Given** visible Chaos Sea entities exist, **When** their browser overview command runs, **Then** the result exposes player-facing detail actions.
2. **Given** hidden threats, GM notes, hidden Fate Cards, or concealed combat audit entries exist, **When** default browser output renders, **Then** they are not visible.

---

### User Story 2 - Read focused details safely (Priority: P1)

A browser player opening a detail action receives one focused, readable Russian detail surface and can return to the relevant overview.

**Why this priority**: Detail actions are useful only if the destination is safe, focused, and not a raw diagnostic fallback.

**Independent Test**: Execute selected-detail commands for profiles, threats, chronicles, spiritual exchanges, recent combat results, and arts; assert focused text and absence of technical leakage.

**Acceptance Scenarios**:

1. **Given** a selected visible afterlife entity exists, **When** the detail command runs, **Then** the output names that entity and renders useful player-facing fields.
2. **Given** the selected target is stale or missing, **When** the detail command runs, **Then** the player sees an in-world unavailable explanation.

---

### User Story 3 - Preserve shared authority (Priority: P2)

Browser detail actions continue to come from shared C# command-result builders so React stays presentation-only and console/browser parity remains testable.

**Why this priority**: The project architecture requires command authority in C# rather than duplicating gameplay routing in the browser frontend.

**Independent Test**: Browser command service tests assert commands/actions emitted by shared builders; no React gameplay selection rules are added.

**Acceptance Scenarios**:

1. **Given** React renders command actions, **When** C# returns a detail action, **Then** React can execute the existing command string without knowing afterlife gameplay rules.
2. **Given** a read-only detail route is opened, **When** it completes, **Then** no pending/control file or write service is created.

### Edge Cases

- Sparse or missing afterlife state returns friendly empty/unavailable output.
- Hidden, internal, GM-only, debug, and raw diagnostic fields remain hidden unless advanced diagnostics are explicitly enabled.
- Dynamic GM-authored text remains treated as data, not browser markup or Spectre markup.
- Existing local-action forms such as `/abode_offering` remain forms; this issue does not add new write behavior.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Browser overview commands for Chaos Sea read-only afterlife surfaces MUST expose safe detail actions when visible canonical records exist.
- **FR-002**: Detail commands MUST render focused Russian player-facing panels/tables without default raw JSON, file paths, DTO/API/endpoint/debug wording, or hidden GM-only markers.
- **FR-003**: Missing or stale selected targets MUST return completed, player-facing unavailable output rather than failing or leaking diagnostics.
- **FR-004**: The implementation MUST stay read-only for added/verified detail routes and MUST NOT change afterlife runtime contracts.
- **FR-005**: Regression tests MUST cover profiles, threats, chronicles, spiritual conflict exchange details, spiritual combat log details, recent conflict details, and spiritual art details.
- **FR-006**: Existing #1063 and #1064 Guardian/Abode/SoulRelic/Archive coverage MUST remain passing.

### Key Entities

- **Afterlife profile**: Player-visible actor profile for the soul, Guardian, resident, or other afterlife actor.
- **Afterlife threat**: Player-visible persistent threat from `afterlife_active_threats`.
- **Afterlife chronicle**: Player-visible external memory/chronology record.
- **Spiritual exchange**: One visible exchange from active spiritual conflict state.
- **Spiritual combat result**: One recent completed spiritual conflict entry.
- **Spiritual art**: Standard or special art inspectable without performing a write action.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused browser command tests cover at least 7 Chaos Sea afterlife overview detail action routes.
- **SC-002**: Focused browser command tests cover at least 8 selected detail routes and 6 missing-target unavailable routes.
- **SC-003**: Focused tests assert no default raw JSON block or technical leakage for selected read-only details.
- **SC-004**: Broader `Afterlife|Chaos|ExplorerWebCommand` verification passes before merge.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName~ExecuteAsync_ChaosSeaAfterlife"` and broader `Afterlife|Chaos|ExplorerWebCommand` slice.
- **Documentation/contract verification**: Not required unless runtime afterlife contracts change. If they change, run `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests`.
- **Frontend verification**: Not required unless React/Vite files change.
- **Manual/player-facing verification**: Optional browser command run against test afterlife data for `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, `/spiritual_conflict`, `/spiritual_combat_log`, and `/spiritual_arts`.

## Assumptions

- Previous #1063/#1064/#1067 implementation work already supplied most shared C# detail routes; #1124 closes the aggregate parity gap by adding missing evidence and any fixes discovered by focused regression tests.
- Local-action forms are not reclassified as read-only detail surfaces.
- No afterlife GM-authored contract changes are needed for this branch.
