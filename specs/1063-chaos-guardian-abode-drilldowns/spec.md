# Feature Specification: Chaos Sea Guardian/Abode Browser Drill-Downs

**Feature Branch**: `1063-chaos-guardian-abode-drilldowns`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1063 — "[Task] Add browser drill-downs for Chaos Sea guardian and abode overviews"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1063 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1063
- **Origin audit**: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949 and `docs/audits/afterlife-drilldown-audit.md` row AFD-001.
- **Issue type**: Browser Client read-only detail actions / Chaos Sea afterlife UI parity.
- **Spec Kit justification**: Required. #1063 changes browser player-facing UX, console/browser parity, afterlife/Chaos Sea surfaces, shared C# command-result metadata, tests/source guards, and durable handoff artifacts.
- **Contract scope**: player-facing browser command-result detail actions for existing read-only Chaos Sea Guardian/Abode overview commands; console/browser parity tests; this Spec Kit feature. Runtime state schemas, pending/control files, validation, normalizers, GM prompts/examples, and mutating write flows are out of scope unless an implementation finding proves they changed and the required docs/tests are updated in the same PR.
- **Primary surfaces**: `/guardians` / `/хранители`, `/abodes` / `/обители`, `/abode_power` / `/сила_обители`, `/guardian_projects` / `/проекты_хранителей`, and related read-only Guardian/Abode entry links from local systems where they can remain read-only.
- **Explicitly out of scope**: #1064 soul relic/archive drill-downs, #1065 Shining Abode inspection drill-downs, #1066 afterlife profile/inbox follow-through, #1067 spiritual conflict drill-downs, mutating Guardian trade/social/resident prompt-session parity, new React-side gameplay rules, raw/debug/default UI exposure, and runtime/GM-authored afterlife contract changes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browser overview rows expose safe detail actions (Priority: P1)

A browser player viewing Chaos Sea Guardian/Abode overview commands can choose a player-facing detail action for a listed Guardian, Abode, power ledger entry, or Guardian project without typing raw identifiers manually or opening raw advanced payloads.

**Why this priority**: #949 identified Guardian/Abode overview collapse as the first afterlife drill-down gap. Browser needs the same effective detail affordance that console exposes through selectors/detail panels.

**Independent Test**: Browser command service tests seed rich Guardian/Abode state, execute each overview command, and assert that result actions contain stable secondary detail commands with player-facing labels and no confirmation requirement.

**Acceptance Scenarios**:

1. **Given** `/guardians` lists at least one Guardian, **When** the browser result is rendered, **Then** the result includes a secondary action that opens that Guardian detail using the shared command path and the overview text remains visible.
2. **Given** `/abodes` lists at least one Abode, **When** the browser result is rendered, **Then** the result includes a secondary action that opens that Abode detail and does not expose raw `game_state/`, API, DTO, endpoint, or debug wording in default output.
3. **Given** `/guardian_projects` lists a project, **When** the browser result is rendered, **Then** the result includes a secondary detail action for that project and keeps read-only semantics.

---

### User Story 2 - Selected details render one focused entity without raw payload leakage (Priority: P1)

A player can invoke a detail action/argument and receive one focused, readable Russian detail surface for the selected Guardian/Abode/power/project while sparse or missing data gets a graceful player-facing unavailable state.

**Why this priority**: Overview action affordances are only useful if the target detail command is safe, focused, and player-facing.

**Independent Test**: Tests execute representative selected-detail commands and assert expected title/detail text, absence of `UiRawJsonBlock` as default content, no raw technical copy, and graceful missing-id behavior.

**Acceptance Scenarios**:

1. **Given** a Guardian has dossier/journal/quest/trade/project links, **When** the selected detail command is executed, **Then** the browser output names that Guardian and summarizes the available read-only detail sections in player-facing text.
2. **Given** an Abode has power, residents, projects, and navigation metadata, **When** the selected detail command is executed, **Then** the browser output focuses on that Abode and keeps unrelated overview rows out of the main detail surface.
3. **Given** the requested id is missing or hidden, **When** the selected detail command is executed, **Then** the result completes with an in-world unavailable explanation rather than throwing, leaking a file path, or falling back to raw JSON.

---

### User Story 3 - Shared C# authority drives browser actions (Priority: P1)

Browser detail actions are produced from shared C# command-result builders/services, not hard-coded React gameplay logic, so console/browser parity remains testable and action commands survive frontend refactors.

**Why this priority**: The issue explicitly asks for shared C# command-result detail actions/arguments rather than React-only logic.

**Independent Test**: Source guards or command-result parity tests verify the relevant Explorer command descriptors accept arguments and the browser command service/result builders provide the detail actions from C#.

**Acceptance Scenarios**:

1. **Given** React renders command result actions, **When** Guardian/Abode commands return detail actions, **Then** React can invoke the existing command strings without knowing Guardian/Abode gameplay rules.
2. **Given** a command is read-only, **When** a detail action is chosen, **Then** no pending/control file or write service is created or modified.
3. **Given** a future worker inspects #949 audit links, **When** it reads this feature, **Then** it can trace the implementation back to #1063 and AFD-001.

### Edge Cases

- Missing or sparse `guardians.json`, `guardian_projects.json`, `guardian_abode_residents.json`, or related afterlife files must produce graceful player-facing empty/detail-unavailable output.
- Detail action ids must be stable enough for tests and UI rendering, but must not expose raw local file paths.
- Dynamic GM-authored text must remain escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Default browser output must not include raw JSON, API/DTO/endpoint/debug language, or `game_state/` paths; advanced diagnostics can remain behind existing advanced mode only.
- Console and browser may differ in layout, but selected detail semantics and command coverage must stay aligned.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Existing overview outputs for `/guardians`, `/abodes`, `/abode_power`, and `/guardian_projects` MUST remain available and player-facing.
- **FR-002**: Browser command results for overview rows MUST expose secondary `UiAction` detail affordances for representative listed Guardians, Abodes, Abode power ledger/details, and Guardian projects when canonical data is present.
- **FR-003**: The detail affordances MUST use shared C# command-result/action metadata and command arguments; React/TypeScript MUST remain presentation-only for this feature.
- **FR-004**: Selected-detail commands MUST render one focused entity/entry in Russian/in-world copy and MUST NOT require default raw JSON inspection.
- **FR-005**: Missing/sparse/unknown selected ids MUST return graceful player-facing unavailable text without exceptions or file-path leakage.
- **FR-006**: The implementation MUST stay read-only: no new pending/control files, no local-turn write contracts, no state mutation rules, and no changes to GM-authored runtime schemas unless separately tracked and documented.
- **FR-007**: Console/browser parity tests/source guards MUST prove the commands accept arguments/detail actions and preserve overview behavior.
- **FR-008**: Link final implementation evidence back to #1063, #949, and `docs/audits/afterlife-drilldown-audit.md`.
- **FR-009**: If implementation discovers a required sub-surface is too broad for #1063, create or link a focused follow-up issue rather than hiding it in prose.

### Key Entities

- **Guardian overview**: Read-only command result from `/guardians` listing Chaos Sea guardians and their player-visible status/links.
- **Abode overview**: Read-only command result from `/abodes` listing Guardian Abodes/navigation and related state.
- **Abode power entry**: Read-only detail from `/abode_power` that explains power causes/ledger entries without mutating state.
- **Guardian project**: Read-only project record from `/guardian_projects` or related Guardian/Abode local systems.
- **Detail action**: A `UiAction` with a stable id, player-facing label, secondary style, no confirmation requirement, and a command string that opens the selected detail via existing command execution.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove overview commands still render and expose browser detail actions for seeded Guardian/Abode/Abode Power/Guardian Project data.
- **SC-002**: Focused tests prove selected-detail commands render one focused player-facing detail without default raw JSON/API/DTO/debug/path leakage.
- **SC-003**: The implementation uses shared C# command-result/action metadata; no React-side gameplay selection rules are introduced.
- **SC-004**: Verification includes focused Browser/afterlife command tests, a broader afterlife/browser/console slice, C# builds, `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files change, Spec Kit prerequisite check, `git diff --check`, and added-line static/security scan.
- **SC-005**: Docs/prompts impact is explicitly reported as no runtime/GM contract change, or relevant GM-facing docs/examples/source guards are updated if that boundary changes.

## Verification Plan *(mandatory)*

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1063-chaos-guardian-abode-drilldowns`.
- **Focused RED/GREEN candidate**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`.
- **Broader afterlife/browser/console slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Build gates if C# source changes**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- **Frontend verification if React/Vite files change**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Documentation-sensitive gate if afterlife runtime contracts/docs change**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- **Diff/security**: `git diff --check origin/main...HEAD` plus added-line static/security scan over changed non-plan code.

## Assumptions

- The existing shared command/result infrastructure can carry the selected-detail actions and commands without new runtime state contracts.
- #1063 can close as a focused browser/read-only parity slice for Guardian/Abode overview details; #1064-#1067 remain separate afterlife follow-up work.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this feature enriches command-result affordances without recreating obsolete card-heavy UI criteria.
