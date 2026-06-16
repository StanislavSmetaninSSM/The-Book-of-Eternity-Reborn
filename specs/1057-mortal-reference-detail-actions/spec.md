# Feature Specification: Browser Detail Actions for Mortal Reference Commands

**Feature Branch**: `1057-mortal-reference-detail-actions`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1057 — "[Task] Add browser detail actions for mortal read-only reference commands"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1057 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1057
- **Parent audit**: #948 mortal read-only drill-down audit. This follows the closed command-specific drill-down children #1054 (`/combat`), #1055 (`/world_news`), and #1056 (`/interactions`).
- **Issue type**: task / audit follow-up / player-facing browser-console parity UX.
- **Spec Kit justification**: Required. #1057 is multi-command, player-facing Browser Client command-result parity work that may touch shared C# command-result DTOs, console command/source-guard tests, browser command-service tests, and the #948 audit artifact. Durable requirements are needed so implementation stays bounded to reference-style Mortal World read-only commands and does not broaden into NPC (#946), books (#947), afterlife (#949), or obsolete Browser Feature-branch UI criteria.
- **Contract scope**: player-facing browser command-result actions/detail affordances for existing read-only Mortal World command outputs, shared C# command-result DTOs, tests/source guards, `docs/audits/mortal-readonly-drilldown-audit.md`, and these Spec Kit artifacts. No GM prompt, runtime-state schema, validation, normalizer, pending/control, afterlife, Chaos Sea, or Shining Abode contract change is intended.
- **Affected commands**: `/quests` / `/квесты`, `/skills` / `/навыки`, `/factions` / `/фракции`, `/locations` / `/локации`, `/rival_threads` / `/чужие_нити`, `/guardian_corrections` / `/коррективы_хранителя`, `/storage_access` / `/доступ_к_хранилищам`, `/transport` / `/транспорт`.
- **Explicitly out of scope**: NPC detail sections (#946), books/document reading (#947), afterlife screen drill-down audit (#949), already closed command-specific children #1054/#1055/#1056, browser card-heavy redesign, new React gameplay logic, mutating actions, prompt-session writes, GM-authored schema changes, and broad navigation/visual retheme work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discover browser detail actions from reference command results (Priority: P1)

A player using the browser command result for a reference-style Mortal World command can see player-facing actions or equivalent detail affordances for one rich entity instead of needing to inspect a summary row plus raw JSON.

**Why this priority**: The issue exists because the #948 audit found console detail selectors for several reference commands while browser command-result DTOs still rely on summary rows/raw JSON.

**Independent Test**: Seed representative canonical state for at least three affected commands, execute through the shared browser command service or command-result builder, and verify default player-facing blocks/actions expose stable detail affordances without raw JSON as the only inspection path.

**Acceptance Scenarios**:

1. **Given** a reference command has multiple rich entities, **When** the browser opens the command result, **Then** each representative entity has a player-facing detail action/selector or equivalent command-result affordance.
2. **Given** a player selects one detail action or uses the equivalent detail command path, **When** the result renders, **Then** it shows the selected entity's player-facing detail blocks using Russian/in-world labels and without requiring raw JSON.

---

### User Story 2 - Preserve existing overview outputs and console behavior (Priority: P1)

The current command overviews remain available, and console/browser semantic parity is improved without replacing the current minimalist browser command-input direction.

**Why this priority**: #1057 is a parity/detail improvement over existing command results, not a redesign of the Browser Client into a card-heavy control panel.

**Independent Test**: Existing overview tests remain green; new or updated source guards document which reference commands expose browser detail actions and confirm console command registrations/aliases remain intact.

**Acceptance Scenarios**:

1. **Given** an affected command already has a summary/overview, **When** a player runs it without detail arguments, **Then** the same overview remains visible and gains safe detail affordances where data exists.
2. **Given** a console player uses the existing command selectors, **When** browser detail actions are added, **Then** console behavior is not degraded and source guards document the intended parity boundary.

---

### User Story 3 - Keep default browser output player-facing and spoiler-safe (Priority: P1)

Default browser command results expose useful safe details while keeping raw JSON, file paths, API/DTO/debug names, and low-level slash-command internals out of ordinary player-facing content.

**Why this priority**: Browser Client rules require player-facing surfaces by default and advanced/debug separation for raw diagnostics.

**Independent Test**: Regression/source-guard assertions verify representative detail actions preserve safe `ExplorerCommandResult` blocks/actions while raw/technical blocks are not the only path and are not shown as ordinary player copy.

**Acceptance Scenarios**:

1. **Given** canonical state includes rich nested fields, **When** a browser detail result renders, **Then** safe player-facing details are preserved rather than collapsed to a generic `Выполнено` surface.
2. **Given** raw diagnostic sidecars remain available behind established advanced/debug paths, **When** the default player view renders, **Then** it does not expose `DTO`, `API`, `endpoint`, `raw JSON`, local file paths, or debug framing.

### Edge Cases

- Missing/sparse files for any affected command must keep graceful overview/empty states.
- A command whose console selector is adequate but whose browser DTO lacks action metadata should receive the smallest shared C# metadata/detail path needed; React must not invent gameplay rules.
- If a command's canonical authority is unclear or too broad for this PR, record the exact reason in the audit artifact and create/link a narrower follow-up before merge rather than silently changing schema.
- Dynamic GM-authored text must be escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Existing advanced/raw diagnostic blocks may remain only as diagnostics; they cannot be the sole default inspection path for covered representative commands.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Existing overview output for all affected commands MUST remain available.
- **FR-002**: Browser command-result DTOs for representative affected commands MUST expose player-facing detail actions, detail commands, or equivalent detail affordances for one selected rich entity.
- **FR-003**: The implementation MUST cover the affected command set as an audit/parity slice: either implement detail affordances for each command or document a precise follow-up for any command that cannot be safely completed in this PR.
- **FR-004**: Detail affordances MUST reuse existing console behavior, shared command-result DTO patterns, `ExplorerCommandCatalog`, or existing command dispatch where practical; React/browser frontend code must remain presentation-only if touched.
- **FR-005**: Default browser/player-facing output MUST use Russian/in-world terminology and MUST NOT rely on raw JSON-only output for covered entity inspection.
- **FR-006**: The implementation MUST preserve read-only behavior and MUST NOT mutate game state, pending files, turn-control state, or prompt sessions.
- **FR-007**: The implementation MUST NOT change GM-authored Mortal World state schema, validation, normalizer behavior, prompts, examples, afterlife contracts, Chaos Sea, or Shining Abode behavior unless a newly tracked follow-up explicitly covers that change.
- **FR-008**: Regression tests/source guards MUST document console/browser parity expectations for the covered commands and fail if browser detail actions regress to summary-only/raw-only inspection for representative commands.
- **FR-009**: If React/Vite files change, frontend verification MUST run and default UI must keep raw/debug/API/DTO copy behind advanced/debug mode.

### Key Entities

- **Reference command result**: A read-only Mortal World command result that lists quests, skills, factions, locations, rival threads, guardian corrections, storage access entries, or transport entries.
- **Detail affordance**: A player-facing action, command argument, selector, or equivalent command-result metadata that lets the browser inspect one entity/record without scanning raw JSON.
- **Detail result**: The command-result output for one selected entity/record, preserving safe blocks and labels while excluding raw/default debug framing.
- **Parity guard**: A test/source guard proving the covered browser DTO detail path and console command/catalog behavior stay aligned.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove browser/shared command-result output exposes safe detail affordances for representative reference commands without raw JSON as the only inspection path.
- **SC-002**: Focused tests prove at least one selected detail path renders useful safe detail blocks instead of collapsing to generic completion or raw-only output.
- **SC-003**: Existing overview behavior remains covered and green for affected commands.
- **SC-004**: Console/browser parity expectations are documented in tests/source guards or explicit follow-up links for any deferred command.
- **SC-005**: Verification includes focused C# tests, broader mortal read-only command-result/console/browser slice, relevant builds, Spec Kit prerequisite check, `git diff --check`, and an added-line static/security scan over the implementation diff.
- **SC-006**: The final PR body and issue evidence comment link #1057, state that GitHub Actions were not required, and record any follow-ups created for deferred command details.

## Verification Plan *(mandatory)*

- **C# baseline/focused verification**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~MortalReadOnlyDrilldownAudit|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Focused post-implementation filter**: Codex must add/update a narrower filter in `tasks.md` once exact test names exist for #1057 browser detail actions.
- **Build gates**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true` if C# source changes.
- **Frontend verification**: `npm run verify --prefix BookOfEternityClient.WebFrontend` only if React/Vite/frontend files change.
- **Documentation/contract verification**: `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1057-mortal-reference-detail-actions`; update `docs/audits/mortal-readonly-drilldown-audit.md` for #1057 only after tests prove the implementation/deferred follow-ups.
- **Diff/security**: `git diff --check origin/main...HEAD` plus an added-line static/security scan over changed non-plan code.

## Assumptions

- Most work should remain in C# shared command-result/metadata/tests; React changes are only needed if the existing browser renderer cannot consume safe action/detail DTOs.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this issue should not recreate obsolete card-heavy Feature-branch UX.
- Console selectors that already exist should be treated as the reference for browser detail action semantics.
- If complete coverage of all eight affected commands is too large for one safe PR, the implementation may cover a representative high-value subset only if it updates the audit artifact with exact follow-up issues for the remainder before merge.
