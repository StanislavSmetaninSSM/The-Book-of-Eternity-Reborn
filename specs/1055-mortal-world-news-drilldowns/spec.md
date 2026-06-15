# Feature Specification: Mortal World-News Read-Only Detail Drill-Downs

**Feature Branch**: `1055-mortal-world-news-drilldowns`

**Created**: 2026-06-16

**Status**: Draft for autonomous implementation

**Input**: GitHub issue #1055 — "[Task] Add mortal world-news read-only detail drill-downs for /новости_мира"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1055 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1055
- **Issue type**: task / audit follow-up / player-facing console-browser parity
- **Spec Kit justification**: #1055 is multi-file player-facing Mortal World command UX work spawned by #948. It affects read-only command-result rendering, console/browser semantic parity, regression/source-guard tests, and audit evidence. Durable requirements are needed so Codex does not broaden the slice into every mortal read-only command or afterlife world-state surface.
- **Contract scope**: player-facing console/browser command UX, read-only C# command-result DTOs, tests, docs/audit artifact, and Spec Kit artifacts. No GM prompt, runtime-state schema, validation, normalizer, pending/control, afterlife, Chaos Sea, or Shining Abode contract change is intended.
- **Out of scope**: #1054 `/combat` already closed; #1056 `/interactions`; #1057 reference-command browser detail actions; #949 afterlife drill-down audit; browser visual redesign; new GM-authored world-news schema. If implementation discovers a missing canonical authority or a section too large for this slice, create/link a narrower follow-up before merge rather than silently changing contracts.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect a world event without raw JSON (Priority: P1)

A player who opens `/новости_мира` or `/world_news` can inspect one visible world event and read its title, timing/location, actors, status, narrative summary, and consequences in player-facing Russian/in-world copy without reading raw JSON sidecars.

**Why this priority**: `world_events.json` is the command's central source of news entries and the most direct all-in-one output risk.

**Independent Test**: Seed `game_state/world/world_events.json` with at least one rich event and verify the shared command-result surface exposes an overview action/detail path plus a player-facing event detail that does not rely on raw JSON.

**Acceptance Scenarios**:

1. **Given** canonical world-event state exists, **When** the player opens `/новости_мира`, **Then** the overview remains available and exposes a player-facing way to inspect the event.
2. **Given** the player selects or invokes the event detail path, **When** the detail renders, **Then** it shows available title/timing/location/actors/status/consequences in Russian/in-world copy and omits raw file paths or debug terms from ordinary blocks.

---

### User Story 2 - Inspect a world flag, threat, or news subsection item (Priority: P2)

A player can inspect one non-event world-news item from the major subsections the command already renders, such as a world flag/state item, location threat/news item, NPC activity note, or faction-project news item when those sections are present in canonical state.

**Why this priority**: The issue explicitly calls out large subsections beyond events; they must not remain only generic bundle counts or raw JSON after event details are introduced.

**Independent Test**: Seed the canonical files used by the current command's major non-event sections and verify at least one representative subsection item receives a player-facing detail path. If current code does not expose a named canonical source for one listed subsection, document the observed source boundary and create/link a follow-up before merge.

**Acceptance Scenarios**:

1. **Given** a rich non-event world-news subsection item exists, **When** the player opens `/world_news`, **Then** the overview names the section and exposes a player-facing detail affordance.
2. **Given** the player inspects that item, **When** the detail renders, **Then** it describes the item in in-world Russian terms without making raw JSON the only explanation.

---

### User Story 3 - Inspect a progression entry without raw JSON (Priority: P2)

A player can inspect one progression entry from `game_state/world/progression.json` and understand what changed, where it applies, and what the current consequence is without searching a long raw state dump.

**Why this priority**: Progression records are part of the existing `/новости_мира` bundle and can become long-running timeline/status notes.

**Independent Test**: Seed `progression.json` with multiple entries and verify the command-result surface provides a player-facing list/action and an individual progression detail path.

**Acceptance Scenarios**:

1. **Given** progression entries exist, **When** the player opens `/новости_мира`, **Then** recent/active entries are summarized separately from events and flags.
2. **Given** the player inspects one progression entry, **When** the detail renders, **Then** it shows the progression title/status/description/consequence using player-facing copy and stable identifiers.

---

### User Story 4 - Preserve overview and console/browser parity boundaries (Priority: P1)

The existing `/новости_мира` overview remains available for quick use, and console/browser surfaces expose equivalent read-only detail capabilities even if their visual presentation differs.

**Why this priority**: #1055 is a drill-down improvement, not a replacement for the current overview or a browser-only control panel.

**Independent Test**: Existing read-only command tests continue to pass; new tests prove console command dispatch and browser/shared command-result DTO paths can reach equivalent detail content or record a narrower follow-up for any intentionally deferred parity gap.

**Acceptance Scenarios**:

1. **Given** world-news files are missing or sparse, **When** the player opens `/новости_мира`, **Then** the command returns a graceful player-facing empty/sparse overview rather than failing or leaking local paths.
2. **Given** detail paths are available in the browser, **When** a console player uses the equivalent command form, **Then** the same canonical entry can be inspected through player-facing output.

### Edge Cases

- Missing `world_events.json`, `world_flags.json`, `progression.json`, or optional subsection files must produce useful overview/empty states, not exceptions.
- Entries without stable ids must still be addressable by a documented stable fallback such as a generated slug/index, while preserving any canonical id when present.
- Dynamic GM-authored text must be escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Default player-facing blocks must not expose raw JSON, file paths, `DTO`, `API`, `endpoint`, or debug/meta-language. Existing advanced/raw diagnostic blocks may remain only behind the established advanced/raw sidecar behavior.
- If the current command aggregates a subsection whose canonical authority is unclear, the implementation must document the exact gap and create/link a follow-up before merge rather than inventing new state semantics.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/world_news` and `/новости_мира` MUST continue to render the existing Mortal World news overview.
- **FR-002**: The shared browser command-result path MUST expose player-facing world-event list/detail content sourced from `game_state/world/world_events.json` when canonical events exist.
- **FR-003**: The shared command-result path MUST expose at least one major non-event world-news subsection item as player-facing list/detail content when canonical state exists, covering world flags/threat/news/NPC/faction-project-style sections according to the current command's real inputs.
- **FR-004**: The shared command-result path MUST expose player-facing progression list/detail content sourced from `game_state/world/progression.json` when canonical progression entries exist.
- **FR-005**: The console client MUST expose semantically equivalent world-news detail affordances for event, non-event subsection, and progression inspection, or the implementation MUST document the exact console gap and create/link a narrower follow-up before merge.
- **FR-006**: Default player-facing output MUST use Russian/in-world terminology and MUST NOT rely on raw JSON-only output for event, subsection, or progression inspection.
- **FR-007**: The implementation MUST NOT change GM-authored Mortal World state schema, validation, prompts, examples, afterlife contracts, Chaos Sea, or Shining Abode behavior unless a newly tracked follow-up explicitly covers that change.
- **FR-008**: Regression tests/source guards MUST cover rich world-news output and must fail if `/новости_мира` regresses to raw-only or all-in-one-only output for the covered sections.

### Key Entities

- **World event**: A visible entry from `game_state/world/world_events.json`; may contain id/title/name, location, date/time, actors, status, description, consequences, related factions/NPCs, and tags.
- **World-news subsection item**: A non-event item already rendered by the command, such as a world flag/state entry, location threat/news item, NPC activity note, or faction-project item; may contain id/key/title, scope/location, status, description, and consequences.
- **Progression entry**: A chronological or state progression record from `game_state/world/progression.json`; may contain id/title/name, stage/status, description, trigger/source, consequence, and timestamp.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove at least one world event can be inspected through player-facing command-result output without raw JSON dependency.
- **SC-002**: Focused tests prove at least one major non-event world-news subsection item can be inspected, or the branch creates/links a narrower follow-up with evidence explaining why that subsection is outside the safe current slice.
- **SC-003**: Focused tests prove at least one progression entry can be inspected through player-facing command-result output without raw JSON dependency.
- **SC-004**: Existing `/новости_мира` overview behavior remains covered by existing or updated tests.
- **SC-005**: Browser and console parity is demonstrated by tests/source guards, or a documented follow-up exists for any intentionally deferred parity sub-slice.
- **SC-006**: Verification includes focused C# tests, relevant `dotnet build` commands, Spec Kit prerequisite check, `git diff --check`, and an added-line security/static scan over the implementation diff.
- **SC-007**: The final PR body and issue evidence comment link #1055, state that GitHub Actions were not required, and record any follow-ups created for remaining subsection detail gaps.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`, plus a narrower focused filter for new world-news tests once added.
- **Documentation/contract verification**: Spec Kit prerequisite check for `specs/1055-mortal-world-news-drilldowns`; update `docs/audits/mortal-readonly-drilldown-audit.md` only if implementation status or follow-up split changes. GM prompt/docs/example coverage is N/A unless runtime/GM-authored contracts change.
- **Frontend verification**: C# command-result DTO tests are expected to cover browser command-result data. Run frontend verification only if React/Vite files change.
- **Manual/player-facing verification**: Inspect `/новости_мира` overview and representative detail commands/actions for Russian/in-world copy if automated tests expose ambiguous copy or renderer behavior.

## Assumptions

- The feature is read-only and should reuse existing file-backed canonical state instead of creating new runtime files.
- Detail command words may follow existing Russian console conventions (for example `/новости_мира событие <id>`), but Codex may refine exact aliases to match nearby command patterns as long as tests and contract artifacts record the final syntax.
- Browser presentation should consume existing shared command-result DTO actions/blocks; React gameplay logic should not be added for this issue unless current DTO metadata is insufficient and a follow-up is recorded.
- The old Browser Client card-heavy feature-branch criteria are not relevant; this issue is about current minimalist browser command-result parity.
