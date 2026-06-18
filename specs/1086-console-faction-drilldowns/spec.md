# Feature Specification: Console Faction Detail Drill-Down Menu Sections

**Feature Branch**: `work/1086-console-faction-drilldowns`

**Created**: 2026-06-18

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1086 — "[Task] Add console faction detail drill-down menu sections"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1086 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1086
- **Issue type**: task / player-facing Console Client UX / Mortal World faction drill-down follow-up.
- **Spec Kit justification**: Required. #1086 changes player-facing Console Client navigation and detail affordances, spans console rendering/tests and possibly shared read-only command-result builders, and must preserve visibility boundaries for Mortal World and any reused Shining faction surfaces. Durable requirements are needed so implementation does not broaden into afterlife runtime contracts, browser redesign, or mutating faction actions.
- **Contract scope**: player-facing read-only console faction detail actions/menu sections, C# command/result rendering and tests/source guards, terminal capture evidence, and these Spec Kit artifacts. No GM prompt, runtime-state schema, validation, normalizer, pending/control, browser frontend, or afterlife/Chaos Sea/Shining Abode write-contract change is intended.
- **Primary affected surfaces**: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.FactionsAndWorldNews.cs`, `BookOfEternityClient/UI/ExplorerMortalWorldCommandResultBuilder.cs`, `BookOfEternityClient/UI/ExplorerShiningAbodeCommandResultBuilder.cs` if Shining faction detail actions are reused, `BookOfEternityClient.Tests/ExplorerModeCommandTests*.cs`, `BookOfEternityClient.Tests/ExplorerModeSourceGuardTests.cs`, and focused browser/service tests only if shared command-result metadata changes.
- **Explicitly out of scope**: changing faction JSON schemas, adding or mutating faction actions, prompt-session writes, browser React UI work, Shining politics write flows, afterlife pending/control contracts, GM-authored state contract changes, NPC/book/world-news drill-downs, and #1085 column-alignment regression work already closed.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Open faction sections from selected console faction detail (Priority: P1)

A player selecting a faction in the Console Client sees the faction page as a hub with meaningful section choices rather than a dead-end panel with only image display.

**Why this priority**: The issue was created from console faction-detail testing where the detail panel was useful but the follow-up menu/actions below the selected faction were too thin.

**Independent Test**: Seed a representative faction with resources, chronicles, structure/ranks, projects, strategic directive/memory, and territorial influence where supported; open the selected faction detail flow through console command/test harness code; verify the available actions include player-facing section entries beyond `Показать изображение`.

**Acceptance Scenarios**:

1. **Given** a faction has rich canonical detail data, **When** the player opens the selected faction detail screen, **Then** the console offers dedicated player-facing section choices for the available data.
2. **Given** only image display is available for a faction, **When** other canonical section files are missing, **Then** the console still presents useful empty-state choices or clear unavailable section copy rather than silently implying no faction knowledge exists.

---

### User Story 2 - Read full player-visible section details (Priority: P1)

A player can choose a faction section and see full player-visible details for that section with Russian in-world labels, not a compact summary or raw JSON dump.

**Why this priority**: The selected faction should expose economics/resources, chronicles, ranks/hierarchy, projects/operations, strategic state, territorial influence, and ledgers where those data exist.

**Independent Test**: Run focused command/result tests for at least representative resource/economic, chronicle, hierarchy/rank, and project/territory/strategy sections; assert section output includes the expected Russian labels and relevant canonical values.

**Acceptance Scenarios**:

1. **Given** a faction has resources and projects, **When** the player opens the economics/projects section, **Then** the section shows the full player-visible resource/project data rather than only the overview row.
2. **Given** a faction has rank or structure information, **When** the player opens the hierarchy section, **Then** ranks, branches, or hierarchy data appear with stable labels and escaped dynamic text.
3. **Given** a faction has chronicles or strategic memory entries, **When** the player opens those sections, **Then** hidden/GM-only entries stay hidden in default player mode and visible entries are presented without raw debug framing.

---

### User Story 3 - Preserve read-only authority and visibility boundaries (Priority: P1)

The feature improves navigation over existing faction data without changing how the GM authors state or how faction writes work.

**Why this priority**: Faction state contains GM-authored text and potentially hidden memory; default console output must be spoiler-safe and must not invent gameplay authority.

**Independent Test**: Add source guards/tests that prove the new detail flow is read-only, avoids raw JSON/API/DTO/debug wording in default mode, and hides hidden/GM-only entries while preserving existing overview behavior.

**Acceptance Scenarios**:

1. **Given** a section has no data, **When** it is opened, **Then** a useful Russian empty state is shown.
2. **Given** hidden/GM-only data exists, **When** default player mode renders the section, **Then** hidden content and internal identifiers are not exposed.
3. **Given** the console overview previously worked, **When** section actions are added, **Then** existing faction overview/detail rendering remains available and #1085 column alignment is not regressed.

### Edge Cases

- Missing optional files such as `faction_resources.json`, `faction_projects.json`, `faction_structure.json`, `faction_chronicles.json`, or Shining faction sidecars must render empty states rather than exceptions.
- Dynamic GM-authored text must be escaped before Spectre.Console markup.
- If a data field is not safe or not clearly player-visible, default mode must summarize its existence or omit it rather than dumping raw values.
- If complete section coverage across Mortal and Shining faction surfaces becomes too broad, the PR may cover the Mortal World selected-faction hub and document a tracked follow-up for Shining-specific parity rather than silently changing afterlife contracts.
- Browser Client work is not required unless shared command-result DTO changes would otherwise break browser command execution.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The selected Console Client faction detail screen MUST offer meaningful detail actions/menu items beyond `Показать изображение` when faction section data exists.
- **FR-002**: The implementation MUST include player-facing section paths for available economics/resources, chronicles, ranks/hierarchy, projects/operations, strategic state, territorial influence, or ledger data where canonical data exists for the selected faction surface.
- **FR-003**: Each detail section MUST render useful full player-visible details with Russian in-world labels and escaped dynamic text.
- **FR-004**: Missing or sparse section data MUST produce useful Russian empty states instead of exceptions, silent disappearance, or raw JSON dumps.
- **FR-005**: Default player-facing output MUST NOT expose raw JSON, file paths, API, DTO, endpoint, debug, internal id, hidden faction, hidden chronicle, raw `strategicMemory`, or raw `resourceLedger` wording/content unless an explicit advanced/debug mode is active.
- **FR-006**: Existing `/factions` overview and selected faction summary behavior MUST remain available, including the #1085 shared column alignment expectations.
- **FR-007**: The feature MUST remain read-only: no mutations, prompt-session writes, pending files, validation/schema changes, or GM-authored contract changes.
- **FR-008**: Focused console command/result tests and source guards MUST cover the new faction detail actions and representative detail section views.
- **FR-009**: Closure evidence MUST include a Console Client screenshot/terminal capture or deterministic terminal/plain-text capture plus measurement/evidence artifact showing the new faction-detail menu/actions.

### Key Entities

- **Faction detail hub**: The selected faction screen reached from Console Client faction selection or equivalent read-only command result.
- **Faction section action**: A player-facing menu item/action that opens one specific faction knowledge section.
- **Faction section detail**: A read-only console detail view for resources/economics, chronicles, ranks/hierarchy, projects/operations, strategic state, territory/influence, or ledger data.
- **Visibility boundary**: The rule that default player mode shows only player-visible faction knowledge and keeps raw/hidden/GM-only state out of ordinary output.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove selected faction details expose section actions beyond `Показать изображение` for representative rich faction data.
- **SC-002**: Focused tests prove representative section details render full player-facing data and safe empty states.
- **SC-003**: Tests/source guards prove hidden/GM-only/raw diagnostic data is not exposed in default player-facing section output.
- **SC-004**: Existing overview/summary behavior and #1085 alignment guard coverage remain green.
- **SC-005**: Verification includes focused C# tests, build of `BookOfEternityClient`, Spec Kit prerequisite check, `git diff --check`, added-line security/static scan, and console terminal capture evidence.
- **SC-006**: PR and issue evidence link #1086, state that GitHub Actions were not required, and document any follow-up if Shining-specific faction sections are intentionally deferred.

## Verification Plan *(mandatory)*

- **Baseline/focused tests**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerModeSourceGuardTests|FullyQualifiedName~ExplorerWebCommandServiceTests" --logger "console;verbosity=minimal"`.
- **Post-implementation focused tests**: Codex must add/update exact test names for #1086 faction section actions/details and run a narrower filter that includes those names plus `ExplorerModeSourceGuardTests`.
- **Build gate**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore`.
- **Spec Kit discoverability**: `SPECIFY_FEATURE_DIRECTORY=specs/1086-console-faction-drilldowns powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1086-console-faction-drilldowns`. The explicit override is required because `.specify/feature.json` on `main` still points at the previous active feature.
- **Diff/security**: `git diff --check origin/main...HEAD` plus an added-line static/security scan over changed non-plan code.
- **Visual/console evidence**: produce terminal/plain-text capture and a brief evidence note under `TestResults/console-faction-drilldowns-*` and copy/preserve it under the owning Codex run directory before worktree cleanup.

## Assumptions

- The primary implementation remains in C# Console Client/shared command-result code and tests.
- Existing Mortal World faction files are the canonical read-only source for Mortal faction sections.
- If Shining faction data is reused in the same console detail flow, changes must stay read-only and must not alter afterlife runtime/write contracts.
- No GM-facing prompt/example update is expected because the feature exposes existing state through new read-only console navigation; if implementation changes the authoring contract, it must update docs/examples or create a tracked follow-up before closure.
