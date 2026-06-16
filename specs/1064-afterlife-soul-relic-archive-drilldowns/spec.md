# Feature Specification: Afterlife Soul Relic/Archive Browser Drill-Downs

**Feature Branch**: `1064-afterlife-soul-relic-archive-drilldowns`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1064 — "[Task] Add browser drill-downs for afterlife soul relic and archive surfaces"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1064 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1064
- **Origin audit**: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949 and `docs/audits/afterlife-drilldown-audit.md` row AFD-003.
- **Issue type**: Browser Client read-only detail actions / afterlife Soul Relic and Archive UI parity.
- **Spec Kit justification**: Required. #1064 changes browser player-facing UX, console/browser parity, afterlife read-only detail affordances, shared C# command-result/action metadata, tests/source guards, and durable handoff artifacts.
- **Contract scope**: player-facing browser command-result detail actions for existing read-only Soul Relic and Archive overview/local-action commands. Runtime state schemas, pending/control files, validation, normalizers, GM prompts/examples, and mutating write flows are out of scope unless an implementation finding proves they changed and the required docs/tests are updated in the same PR.
- **Primary surfaces**: `/soul_relics`, `/soul_relic_equip`, `/soul_relic_unequip`, `/afterlife_archive`, `/archive_candidates`, `/archive_consultation`, and `/archive_project_fuel` plus their Russian aliases where existing command infrastructure supports them.
- **Explicitly out of scope**: #1063 Guardian/Abode drill-downs already closed, #1065 Shining Abode inspection drill-downs, #1066 afterlife profile/inbox follow-through, #1067 spiritual conflict drill-downs, new Soul Relic forge/economy mechanics, new Archive pull/write contracts, React-side gameplay authority, raw/debug/default UI exposure, and runtime/GM-authored afterlife contract changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browser relic and archive rows expose safe detail actions (Priority: P1)

A browser player viewing Soul Relic or Archive command results can choose a player-facing detail action for a listed relic, archive entry, archive candidate, consultation context, or project-fuel row without manually typing raw identifiers or opening raw advanced payloads.

**Why this priority**: AFD-003 identifies that browser relic/archive surfaces preserve useful overview and local action forms but lack the console-style one-row inspection affordances players need before acting.

**Independent Test**: Browser command service tests seed rich afterlife relic/archive state, execute each covered overview/local-action command, and assert stable secondary detail actions with Russian/player-facing labels, no confirmation requirement, and preserved overview/local action content.

**Acceptance Scenarios**:

1. **Given** `/soul_relics` lists at least one relic, **When** the browser result is rendered, **Then** the result includes a secondary action that opens that relic detail while the overview remains visible.
2. **Given** `/afterlife_archive` or `/archive_candidates` lists entries/candidates, **When** the browser result is rendered, **Then** each useful row exposes a read-only detail action and default output does not expose raw `game_state/`, API, DTO, endpoint, protocol, or debug wording.
3. **Given** `/archive_consultation` or `/archive_project_fuel` presents a local action form or candidate row, **When** browser actions are built, **Then** existing local action forms remain available and any added drill-down action is read-only and clearly distinct from mutating choices.

---

### User Story 2 - Selected details render one focused row without raw payload leakage (Priority: P1)

A player can invoke a detail action/argument and receive one focused, readable Russian detail surface for the selected relic/archive/candidate/fuel row while sparse or missing data gets a graceful player-facing unavailable state.

**Why this priority**: The detail action must provide useful inspection instead of a generic completion result or raw JSON dump.

**Independent Test**: Tests execute representative selected-detail commands and assert expected title/detail text, absence of default raw JSON blocks, no technical copy, and graceful missing/stale-id behavior.

**Acceptance Scenarios**:

1. **Given** a Soul Relic has name, rarity, status, slots/effects, owner/equip eligibility, or history, **When** its detail command executes, **Then** the browser output focuses on that relic and summarizes available player-facing detail sections.
2. **Given** an archive entry/candidate has source, status, linked Guardian/project/codex context, or required resource/fuel information, **When** its detail command executes, **Then** the browser output focuses on that row and keeps unrelated overview rows out of the main detail surface.
3. **Given** the requested id is missing, stale, hidden, or not eligible for player visibility, **When** the selected detail command executes, **Then** the result completes with an in-world unavailable explanation rather than throwing, leaking a path, or falling back to raw JSON.

---

### User Story 3 - Shared C# authority preserves browser/console parity (Priority: P1)

Browser detail actions are produced from shared C# command-result builders/services, not hard-coded React gameplay logic, so console/browser parity remains testable and action commands survive frontend refactors.

**Why this priority**: The issue asks for shared C# command-result/Explorer patterns and preservation of current action forms rather than a separate frontend implementation.

**Independent Test**: Source guards or command-result parity tests verify the relevant Explorer command descriptors accept read-only arguments/detail actions and the browser command service/result builders provide the detail actions from C#.

**Acceptance Scenarios**:

1. **Given** React renders command result actions, **When** Soul Relic/Archive commands return detail actions, **Then** React can invoke existing command strings without knowing relic/archive gameplay rules.
2. **Given** a command currently starts a local action form, **When** #1064 adds drill-down affordances, **Then** the form still works through existing prompt/write paths and the new detail action stays read-only.
3. **Given** a future worker inspects #949 audit links, **When** it reads this feature, **Then** it can trace the implementation back to #1064 and AFD-003 without absorbing #1065-#1067.

### Edge Cases

- Missing or sparse Soul Relic, afterlife archive, candidate, consultation, or project-fuel files must produce graceful player-facing empty/detail-unavailable output.
- Detail action ids must be stable enough for tests and UI rendering, but must not expose raw local file paths.
- Dynamic GM-authored text must remain escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Default browser output must not include raw JSON, API/DTO/endpoint/protocol/debug language, `game_state/` paths, or raw slash-command leakage; advanced diagnostics can remain behind existing advanced mode only.
- Console and browser may differ in layout, but selected detail semantics and command coverage must stay aligned.
- If a selected-detail sub-surface would require new archive/relic runtime contracts or mutating write behavior, split or document it as a follow-up instead of expanding #1064.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Existing overview outputs and local action forms for `/soul_relics`, `/soul_relic_equip`, `/soul_relic_unequip`, `/afterlife_archive`, `/archive_candidates`, `/archive_consultation`, and `/archive_project_fuel` MUST remain available and player-facing.
- **FR-002**: Browser command results for representative relic/archive/candidate/fuel rows MUST expose secondary `UiAction` detail affordances when canonical player-visible data is present.
- **FR-003**: The detail affordances MUST use shared C# command-result/action metadata and command arguments; React/TypeScript MUST remain presentation-only for this feature.
- **FR-004**: Selected-detail commands MUST render one focused entity/entry in Russian/in-world copy and MUST NOT require default raw JSON inspection.
- **FR-005**: Missing/sparse/stale/unknown selected ids MUST return graceful player-facing unavailable text without exceptions or file-path leakage.
- **FR-006**: The implementation MUST stay read-only for added drill-downs: no new pending/control files, no local-turn write contracts, no state mutation rules, and no changes to GM-authored runtime schemas unless separately tracked and documented.
- **FR-007**: Console/browser parity tests/source guards MUST prove the commands accept arguments/detail actions and preserve overview/form behavior.
- **FR-008**: Link final implementation evidence back to #1064, #949, and `docs/audits/afterlife-drilldown-audit.md` row AFD-003.
- **FR-009**: If implementation discovers a required sub-surface is too broad for #1064, create or link a focused follow-up issue rather than hiding it in prose.

### Key Entities

- **Soul Relic row**: A player-visible relic record from the afterlife relic overview/equip/unequip context.
- **Archive entry**: A read-only afterlife archive record that may link to Guardian/project/codex context.
- **Archive candidate**: A candidate row that can be inspected before an archive action or consultation.
- **Archive project fuel row**: A project/resource/fuel row shown before local archive support actions.
- **Detail action**: A `UiAction` with a stable id, player-facing label, secondary/read-only style, no confirmation requirement, and a command string that opens the selected detail through existing command execution.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove covered overview/local-action commands still render and expose browser detail actions for seeded relic/archive/candidate/fuel data.
- **SC-002**: Focused tests prove selected-detail commands render one focused player-facing detail without default raw JSON/API/DTO/debug/path leakage.
- **SC-003**: The implementation uses shared C# command-result/action metadata; no React-side gameplay selection or mutation rules are introduced.
- **SC-004**: Verification includes focused Browser/afterlife command tests, a broader afterlife/browser/console slice, C# builds, `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files change, Spec Kit prerequisite check, `git diff --check`, and added-line static/security scan.
- **SC-005**: Docs/prompts impact is explicitly reported as no runtime/GM contract change, or relevant GM-facing docs/examples/source guards are updated if that boundary changes.

## Verification Plan *(mandatory)*

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1064-afterlife-soul-relic-archive-drilldowns`.
- **Focused RED/GREEN candidate**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`.
- **Broader afterlife/browser/console slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~SoulRelic|FullyQualifiedName~Archive|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Build gates if C# source changes**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- **Frontend verification if React/Vite files change**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Documentation-sensitive gate if afterlife runtime contracts/docs change**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- **Diff/security**: `git diff --check origin/main...HEAD` plus added-line static/security scan over changed non-plan code.

## Assumptions

- Existing shared command/result infrastructure can carry selected-detail actions and commands without new runtime state contracts.
- #1064 can close as a focused browser/read-only parity slice for Soul Relic and Archive selected details; #1065-#1067 remain separate afterlife follow-up work.
- Local action forms for equip/unequip/archive consultation/fuel already have write authority elsewhere; #1064 adds inspection around those forms, not new write semantics.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this feature enriches command-result affordances without recreating obsolete card-heavy UI criteria.
