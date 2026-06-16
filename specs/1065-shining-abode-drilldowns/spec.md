# Feature Specification: Shining Abode Browser Inspection Drill-Downs

**Feature Branch**: `1065-shining-abode-drilldowns`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1065 — "[Task] Add browser Shining Abode inspection drill-downs"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1065 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1065
- **Origin audit**: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949 and `docs/audits/afterlife-drilldown-audit.md` row AFD-004.
- **Issue type**: Browser Client read-only detail actions / Shining Abode inspection UI parity.
- **Spec Kit justification**: Required. #1065 changes browser player-facing UX, console/browser parity, afterlife/Shining Abode read-only inspection affordances, shared C# command-result/action metadata, tests/source guards, and durable handoff artifacts.
- **Contract scope**: player-facing browser command-result detail actions for existing read-only Shining overview/politics/inspection commands and surrounding migrated action forms. Runtime state schemas, pending/control files, validation, normalizers, GM prompts/examples, and mutating write flows are out of scope unless an implementation finding proves they changed and the required docs/tests are updated in the same PR.
- **Primary surfaces**: `/shining_abode`, `/shining_politics`, `/shining_faction_founding`, `/shining_faction_realignment`, `/shining_faction_leadership`, `/shining_native_faction_discovery`, `/shining_faction_investment`, `/shining_project_support`, `/shining_project_unsupport`, `/shining_project_retirement`, `/shining_gates_open`, `/shining_gates_select`, `/shining_gates_deselect`, `/shining_gates_reroll`, `/shining_incarnation_prepare`, `/shining_relic_forge`, `/shining_trade`, `/shining_treasury`, `/source_of_light`, plus existing Russian aliases where command infrastructure supports them.
- **Explicitly out of scope**: #1063 Guardian/Abode drill-downs already closed, #1064 Soul Relic/Archive drill-downs already closed, #1066 afterlife profile/inbox follow-through, #1067 spiritual conflict drill-downs, new Shining write contracts, new pending/control files, React-side gameplay authority, raw/debug/default UI exposure, and runtime/GM-authored afterlife contract changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browser Shining rows expose safe inspection actions (Priority: P1)

A browser player viewing Shining Abode, politics, treasury, gates, trade, forge, source-of-light, or related local-action command results can choose a player-facing inspection action for a listed row/receipt/faction/project/gate/resource without manually typing raw identifiers or opening raw advanced payloads.

**Why this priority**: AFD-004 identifies that console has dense Shining inspection panels while browser preserves useful overviews/forms but lacks equivalent selected inspection flows.

**Independent Test**: Browser command service tests seed rich Shining state, execute representative covered commands, and assert stable secondary read-only detail actions with Russian/player-facing labels, no confirmation requirement, and preserved overview/local action content.

**Acceptance Scenarios**:

1. **Given** `/shining_abode` or `/shining_politics` lists structures, factions, chronicles, pending actions, or political resolutions, **When** the browser result is rendered, **Then** useful rows expose secondary inspection actions while the overview remains visible.
2. **Given** Shining local-action commands such as gate selection, incarnation preparation, relic forge, trade, treasury, faction investment, or project support return guided forms/choices, **When** browser actions are built, **Then** existing mutating/prompt actions remain available and any added inspection action is read-only and clearly distinct.
3. **Given** default browser output is shown to a player, **When** Shining detail actions appear, **Then** labels and result text stay in-world/Russian and avoid raw `game_state/`, API, DTO, endpoint, protocol, debug, or slash-command leakage.

---

### User Story 2 - Selected details render one focused Shining inspection surface (Priority: P1)

A player can invoke a Shining inspection action/argument and receive one focused, readable detail surface for the selected gate, core receipt, pending core action, trade lifecycle entry, resident project audit row, structure, faction, chronicle, pending political action, political resolution, treasury/resource/source-of-light row, or supported forge/action context while sparse or missing data gets a graceful player-facing unavailable state.

**Why this priority**: Shining systems are dense; players need focused inspection before committing local actions or interpreting political/history rows.

**Independent Test**: Tests execute representative selected-detail commands and assert expected title/detail text, absence of default raw JSON blocks, no technical copy, and graceful missing/stale-id behavior.

**Acceptance Scenarios**:

1. **Given** a selected Shining row has name, status, influence/resource data, linked resident/project/faction/gate context, or history, **When** its detail command executes, **Then** the browser output focuses on that row and summarizes available player-facing detail sections.
2. **Given** a selected row belongs to a mutating local-action surface, **When** a detail action is opened, **Then** the result remains read-only and does not submit or alter pending/action state.
3. **Given** the requested id is missing, stale, hidden, sparse, or not eligible for player visibility, **When** the selected detail command executes, **Then** the result completes with an in-world unavailable explanation rather than throwing, leaking a path, or falling back to raw JSON.

---

### User Story 3 - Shared C# authority preserves browser/console parity (Priority: P1)

Browser Shining detail actions are produced from shared C# command-result builders/services, not hard-coded React gameplay logic, so console/browser parity remains testable and action commands survive frontend refactors.

**Why this priority**: The issue asks for browser detail affordances over existing Shining command authority, not a separate frontend implementation or new runtime contract.

**Independent Test**: Source guards or command-result parity tests verify relevant Explorer command descriptors accept read-only arguments/detail actions and the browser command service/result builders provide the detail actions from C#.

**Acceptance Scenarios**:

1. **Given** React renders command result actions, **When** Shining commands return detail actions, **Then** React can invoke existing command strings without knowing Shining gameplay rules.
2. **Given** a command currently starts a local action form, **When** #1065 adds drill-down affordances, **Then** the form still works through existing prompt/write paths and the new detail action stays read-only.
3. **Given** a future worker inspects #949 audit links, **When** it reads this feature, **Then** it can trace the implementation back to #1065 and AFD-004 without absorbing #1066-#1067.

### Edge Cases

- Missing or sparse Shining state files must produce graceful player-facing empty/detail-unavailable output.
- Detail action ids must be stable enough for tests and UI rendering, but must not expose raw local file paths.
- Dynamic GM-authored text must remain escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Default browser output must not include raw JSON, API/DTO/endpoint/protocol/debug language, `game_state/` paths, raw slash-command leakage, or local filesystem paths; advanced diagnostics can remain behind existing advanced mode only.
- Console and browser may differ in layout, but selected detail semantics and command coverage must stay aligned.
- If a selected-detail sub-surface would require new Shining runtime contracts, pending/control files, validation, normalizer, or mutating write behavior, split or document it as a follow-up instead of expanding #1065.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Existing overview outputs and local action forms for the covered Shining commands MUST remain available and player-facing.
- **FR-002**: Browser command results for representative Shining rows/receipts/factions/projects/gates/resources MUST expose secondary `UiAction` detail affordances when canonical player-visible data is present.
- **FR-003**: The detail affordances MUST use shared C# command-result/action metadata and command arguments; React/TypeScript MUST remain presentation-only for this feature.
- **FR-004**: Selected-detail commands MUST render one focused entity/entry in Russian/in-world copy and MUST NOT require default raw JSON inspection.
- **FR-005**: Missing/sparse/stale/unknown selected ids MUST return graceful player-facing unavailable text without exceptions or file-path leakage.
- **FR-006**: The implementation MUST stay read-only for added drill-downs: no new pending/control files, no local-turn write contracts, no state mutation rules, and no changes to GM-authored runtime schemas unless separately tracked and documented.
- **FR-007**: Console/browser parity tests/source guards MUST prove relevant Shining commands accept arguments/detail actions and preserve overview/form behavior.
- **FR-008**: Link final implementation evidence back to #1065, #949, and `docs/audits/afterlife-drilldown-audit.md` row AFD-004.
- **FR-009**: If implementation discovers a required sub-surface is too broad for #1065, create or link a focused follow-up issue rather than hiding it in prose.

### Key Entities

- **Shining inspection row**: A player-visible structure, faction, chronicle, pending political/core action, political resolution, gate, treasury/resource/source-of-light, trade lifecycle, resident project audit, or related Shining row from existing command results.
- **Local action form**: Existing guided command-result form/prompt/action for Shining gates, politics, investment, project support, forge, trade, treasury, source-of-light, or incarnation preparation.
- **Selected-detail result**: A focused read-only command result that explains one Shining inspection row without mutating state.
- **Detail action**: A `UiAction` with a stable id, player-facing label, secondary/read-only style, no confirmation requirement, and a command string that opens the selected detail through existing command execution.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove representative covered overview/local-action commands still render and expose browser detail actions for seeded Shining data.
- **SC-002**: Focused tests prove selected-detail commands render one focused player-facing detail without default raw JSON/API/DTO/debug/path leakage.
- **SC-003**: The implementation uses shared C# command-result/action metadata; no React-side gameplay selection or mutation rules are introduced.
- **SC-004**: Verification includes focused Browser/afterlife/Shining command tests, a broader afterlife/browser/console slice, C# builds, `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files change, Spec Kit prerequisite check, `git diff --check`, and added-line static/security scan.
- **SC-005**: Docs/prompts impact is explicitly reported as no runtime/GM contract change, or relevant GM-facing docs/examples/source guards are updated if that boundary changes.

## Verification Plan *(mandatory)*

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1065-shining-abode-drilldowns`.
- **Focused RED/GREEN candidate**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`.
- **Broader afterlife/browser/console slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Build gates if C# source changes**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- **Frontend verification if React/Vite files change**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Documentation-sensitive gate if afterlife runtime contracts/docs change**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- **Diff/security**: `git diff --check origin/main...HEAD` plus added-line static/security scan over changed non-plan code.

## Assumptions

- Existing shared command/result infrastructure can carry selected-detail actions and commands without new runtime state contracts.
- #1065 can close as a focused browser/read-only parity slice for Shining selected inspection details; #1066-#1067 remain separate afterlife follow-up work.
- Existing Shining local action forms already have write authority elsewhere; #1065 adds inspection around those forms, not new write semantics.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this feature enriches command-result affordances without recreating obsolete card-heavy UI criteria.
