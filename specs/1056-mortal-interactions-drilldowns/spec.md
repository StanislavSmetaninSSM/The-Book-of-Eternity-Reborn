# Feature Specification: Mortal Player-Interaction Read-Only Detail Drill-Downs

**Feature Branch**: `1056-mortal-interactions-drilldowns`

**Created**: 2026-06-16

**Status**: Implemented locally; pending Hermes review/PR/merge

**Input**: GitHub issue #1056 — "[Task] Add mortal player-interaction read-only detail drill-downs for /взаимодействия"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1056 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1056
- **Parent audit**: #948 mortal read-only drill-down audit; follows completed #1054 `/combat` and #1055 `/world_news` child slices.
- **Issue type**: task / audit follow-up / player-facing console-browser parity
- **Spec Kit justification**: #1056 is multi-file player-facing Mortal World command UX work. It affects a read-only command family, shared command-result rendering, console/browser semantic parity, regression/source-guard tests, and audit evidence. Durable requirements are needed so Codex keeps this slice bounded to `/interactions` / `/взаимодействия` and does not broaden into sibling #1057 reference-command work or afterlife social/pending contracts.
- **Contract scope**: player-facing console/browser command UX, read-only C# command-result DTOs, tests, `docs/audits/mortal-readonly-drilldown-audit.md`, and Spec Kit artifacts. No GM prompt, runtime-state schema, validation, normalizer, pending/control, afterlife, Chaos Sea, or Shining Abode contract change is intended.
- **Out of scope**: #1054 `/combat` and #1055 `/world_news` already closed; #1057 browser detail actions for reference-style mortal read-only commands; #949 afterlife drill-down audit; NPC/Guardian/resident mutating social request flows; browser visual redesign; new GM-authored interaction schema. If implementation discovers missing canonical authority or a section too broad for this slice, create/link a narrower follow-up before merge rather than silently changing contracts.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect one player entry without scanning the aggregate (Priority: P1)

A player who opens `/взаимодействия` or `/interactions` can inspect one other-player entry and see that player's relationship/context, summary, and current interaction hooks in player-facing Russian/in-world copy without reading raw JSON sidecars.

**Why this priority**: The issue explicitly names nested records for other players. A player-level drill-down is the first step away from long all-in-one output.

**Independent Test**: Seed `game_state/misc/player_interactions.json` with at least two players and rich nested interaction payloads. Verify the shared command-result overview exposes player-level detail affordances and that selecting one player returns only that player's player-facing detail content.

**Acceptance Scenarios**:

1. **Given** canonical player-interaction state exists for multiple players, **When** the player opens `/взаимодействия`, **Then** the overview remains available and exposes a player-facing way to inspect a single player.
2. **Given** the player selects or invokes the player detail path, **When** the detail renders, **Then** it shows the selected player's available name/id, relation/context, status, summary, and visible interaction hooks in Russian/in-world copy, without raw file paths or debug terms in ordinary blocks.

---

### User Story 2 - Inspect one interaction record/payload without raw JSON (Priority: P1)

A player can inspect one interaction record or nested payload for a selected player and understand what happened, who was involved, when it happened, current status, and visible consequence or next step.

**Why this priority**: The issue's acceptance criteria require one interaction record to be inspectable without raw JSON or scanning the whole aggregate panel.

**Independent Test**: Seed a selected player with multiple interaction records including stable ids, titles/summaries, status, turn/time, participants, notes, outcomes, and nested payload fields. Verify a command-result action/detail path for one record shows player-facing content and hides raw-only representation from the default surface.

**Acceptance Scenarios**:

1. **Given** a selected player has several interaction records, **When** the player opens the player detail, **Then** each visible record has a stable label or action so one record can be inspected independently.
2. **Given** the player inspects one record, **When** the detail renders, **Then** it shows the record's title/summary/status/timing/participants/outcome using in-world copy and stable identifiers, without relying on raw JSON.

---

### User Story 3 - Preserve overview and console/browser parity boundaries (Priority: P1)

The existing `/взаимодействия` overview remains available for quick use, and console/browser surfaces expose equivalent read-only drill-down capabilities even if rendered differently.

**Why this priority**: #1056 is a drill-down improvement, not a replacement for the current overview or a browser-only control panel.

**Independent Test**: Existing read-only command tests continue to pass; new tests prove console command dispatch and browser/shared command-result DTO paths can reach equivalent player/record detail content or record a narrower follow-up for any intentionally deferred parity gap.

**Acceptance Scenarios**:

1. **Given** player interaction files are missing or sparse, **When** the player opens `/взаимодействия`, **Then** the command returns a graceful player-facing empty/sparse overview rather than failing or leaking local paths.
2. **Given** detail paths are available in browser/shared DTO output, **When** a console player uses the equivalent command form, **Then** the same canonical player or record can be inspected through player-facing output.

### Edge Cases

- Missing `game_state/misc/player_interactions.json` or sparse/empty interaction groups must produce useful overview/empty states, not exceptions.
- The implementation must support the real shapes already observed in this repository, including top-level `interactions` arrays, `otherPlayersInteractions` objects keyed by player id, and canonical `otherPlayersInteractions[playerId]` arrays of command objects such as `{ "UpdateInventory": [...] }`.
- Entries without stable ids must still be addressable by a documented stable fallback such as player key, generated slug, or deterministic index, while preserving any canonical id when present.
- Dynamic GM-authored text must be escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Default player-facing blocks must not expose raw JSON, file paths, `DTO`, `API`, `endpoint`, or debug/meta-language. Existing advanced/raw diagnostic sidecars may remain only behind established raw/advanced behavior.
- If the current command aggregates a subsection whose canonical authority is unclear, the implementation must document the exact gap and create/link a follow-up before merge rather than inventing new state semantics.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: `/interactions` and `/взаимодействия` MUST continue to render the existing Mortal World interactions overview.
- **FR-002**: The shared browser command-result path MUST expose player-facing player-entry list/detail content sourced from `game_state/misc/player_interactions.json` when canonical entries exist.
- **FR-003**: The shared command-result path MUST expose player-facing individual interaction-record detail content for at least one selected player/record when canonical nested records exist.
- **FR-004**: The console client MUST expose semantically equivalent player and record detail affordances, or the implementation MUST document the exact console gap and create/link a narrower follow-up before merge.
- **FR-005**: Default player-facing output MUST use Russian/in-world terminology and MUST NOT rely on raw JSON-only output for player-entry or record inspection.
- **FR-006**: The implementation MUST preserve read-only behavior and MUST NOT mutate `player_interactions.json`, pending social request files, or turn-control state.
- **FR-007**: The implementation MUST NOT change GM-authored Mortal World state schema, validation, prompts, examples, afterlife contracts, Chaos Sea, Shining Abode, Guardian, NPC, or resident social-request behavior unless a newly tracked follow-up explicitly covers that change.
- **FR-008**: Regression tests/source guards MUST cover rich player interaction output and must fail if `/взаимодействия` regresses to raw-only or all-in-one-only output for the covered player/record detail paths.

### Key Entities

- **Player interaction entry**: A player-level grouping from `game_state/misc/player_interactions.json`; may be represented by a key under `otherPlayersInteractions`, a `playerId`, `characterId`, name/displayName, relationship/context fields, summary, status, and nested records.
- **Interaction record**: A nested record/payload belonging to a player entry; may contain `interactionId`, `recordId`, title/summary, participants, location, turn/time, status, notes, outcomes, consequences, tags, visibility fields, or a canonical command-object payload with nested player-facing fields such as item name, quantity, and description.
- **Interaction overview**: The existing read-only aggregate summary exposed by `/interactions` / `/взаимодействия`, retained as the entry point for discovering player and record drill-downs.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove at least one player interaction entry can be inspected through player-facing command-result output without raw JSON dependency.
- **SC-002**: Focused tests prove at least one nested interaction record/payload can be inspected through player-facing command-result output without raw JSON dependency.
- **SC-003**: Existing `/взаимодействия` overview behavior remains covered by existing or updated tests.
- **SC-004**: Browser and console parity is demonstrated by tests/source guards, or a documented follow-up exists for any intentionally deferred parity sub-slice.
- **SC-005**: Verification includes focused C# tests, the broader mortal read-only command-result/console/browser slice, relevant `dotnet build` commands, Spec Kit prerequisite check, `git diff --check`, and an added-line security/static scan over the implementation diff.
- **SC-006**: The final PR body and issue evidence comment link #1056, state that GitHub Actions were not required, and record any follow-ups created for remaining detail gaps.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`, plus a narrower focused filter for new interactions tests once added.
- **Documentation/contract verification**: Spec Kit prerequisite check for `specs/1056-mortal-interactions-drilldowns`; update `docs/audits/mortal-readonly-drilldown-audit.md` to mark #1056 implemented only after code/tests prove it. GM prompt/docs/example coverage is N/A unless runtime/GM-authored contracts change.
- **Frontend verification**: C# command-result DTO tests are expected to cover browser command-result data. Run frontend verification only if React/Vite files change.
- **Manual/player-facing verification**: Inspect `/взаимодействия` overview and representative detail commands/actions for Russian/in-world copy if automated tests expose ambiguous copy or renderer behavior.

## Assumptions

- The feature is read-only and should reuse existing file-backed canonical state instead of creating new runtime files.
- Detail command words may follow nearby Russian console conventions, for example `/взаимодействия игрок <id-or-slug>` and `/взаимодействия запись <id-or-slug>`, but Codex may refine exact aliases to match existing command patterns as long as tests and contract artifacts record the final syntax.
- Browser presentation should consume existing shared command-result DTO actions/blocks; React gameplay logic should not be added for this issue unless current DTO metadata is insufficient and a follow-up is recorded.
- The old Browser Client card-heavy feature-branch criteria are not relevant; this issue is about current minimalist browser command-result parity.
