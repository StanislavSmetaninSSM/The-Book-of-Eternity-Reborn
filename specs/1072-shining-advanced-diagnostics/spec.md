# Feature Specification: Shining Advanced Diagnostics Boundary

**Feature Branch**: `work/1072-shining-advanced-diagnostics`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1072 — "[Task] Move Shining treasury/source diagnostics behind advanced mode"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1072 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1072
- **Origin review/audit**: independent review of #1065 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1065 and #949 AFD-004 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949.
- **Issue type**: Browser/default command-result presentation cleanup for Shining Abode diagnostics.
- **Spec Kit justification**: Required. #1072 changes player-facing Browser Client output boundaries for afterlife/Shining surfaces, must preserve default-vs-advanced semantics, and needs durable handoff/tests after #1065 review.
- **Contract scope**: default browser/player output for existing `/shining_treasury` and `/source_of_light` command results and any malformed/sparse-state diagnostics reached by those surfaces. Existing runtime state schemas, pending/control files, write/prompt authority, validation, normalizers, GM prompts/examples, and React gameplay logic are out of scope unless implementation proves a contract actually changes and the required docs/tests are updated in the same PR.
- **Primary surfaces**: `/shining_treasury`, `/source_of_light`, existing Russian aliases where supported, command-result block/action metadata, advanced/debug diagnostics gating.
- **Explicitly out of scope**: #1065 selected-detail drill-down implementation already closed, #1066 afterlife profile/inbox follow-through, #1067 spiritual conflict/art drill-downs, new Shining write operations, new pending/control files, React-side gameplay authority, and broad visual redesign.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Default Shining diagnostics stay player-facing (Priority: P1)

A player running `/shining_treasury` or `/source_of_light` in the browser default mode sees Russian/in-world summaries, actionable guidance, and graceful unavailable text without raw diagnostic blocks or local state-file details.

**Why this priority**: The #1065 independent review found inherited raw diagnostics outside the newly implemented selected-detail flows; default Browser Client output must not feel like a debug terminal.

**Independent Test**: Focused browser command-result tests seed treasury/source states, execute the commands in default mode, and assert that completed results contain useful player-facing blocks while excluding `UiRawJsonBlock`, raw JSON, `game_state/`, local filesystem paths, API/DTO/endpoint/protocol/debug wording, and malformed JSON warning path text.

**Acceptance Scenarios**:

1. **Given** Shining treasury state exists, **When** `/shining_treasury` renders in default browser/player mode, **Then** the result summarizes treasury/source/resource information in player-facing copy and does not expose raw JSON or state-file paths.
2. **Given** Source of Light state exists or is sparse, **When** `/source_of_light` renders in default browser/player mode, **Then** the result explains the current state or absence in Russian/in-world terms and does not expose raw file diagnostics.
3. **Given** a malformed optional diagnostic/state file is encountered, **When** default output is returned, **Then** the player gets a graceful unavailable/needs-GM-attention explanation without file path text, raw parser exception copy, or advanced/debug framing.

---

### User Story 2 - Advanced mode keeps useful diagnostics explicit (Priority: P1)

An advanced/debug user can still inspect raw diagnostic information for troubleshooting, but only after explicit advanced-mode opt-in rather than through the default player command-result surface.

**Why this priority**: The fix must not remove useful local debugging data from the advanced tools; it must place that data behind the correct boundary.

**Independent Test**: Tests or source guards verify that advanced/debug mode can still include raw diagnostic blocks or safe diagnostic messages where the command-result infrastructure already supports advanced output, while default mode remains clean.

**Acceptance Scenarios**:

1. **Given** advanced/debug mode is active, **When** a Shining treasury/source diagnostic result is requested, **Then** diagnostic details may be present and clearly marked as advanced/debug data.
2. **Given** default player mode is active, **When** the same command runs, **Then** those diagnostics are absent from the default blocks/actions.
3. **Given** a diagnostic cannot be rendered safely even in advanced mode, **When** the command completes, **Then** the implementation documents the limitation and preserves player-facing default behavior.

---

### User Story 3 - Existing Shining write/prompt authority is unchanged (Priority: P2)

The cleanup only changes presentation/read-only command-result output. Existing Shining treasury/source write prompts, pending contracts, and local write services remain authoritative and unchanged.

**Why this priority**: #1072 is a presentation boundary task, not a runtime contract or gameplay-rule change.

**Independent Test**: Regression tests cover default/advanced rendering while existing command catalog, prompt/write, and afterlife drill-down audit tests continue to pass.

**Acceptance Scenarios**:

1. **Given** existing treasury/source commands expose actions/forms, **When** diagnostics are gated, **Then** write/prompt actions still route through existing C# services.
2. **Given** no runtime contract is changed, **When** docs/prompts impact is reviewed, **Then** afterlife GM docs/examples do not need updates and the PR reports that rationale.
3. **Given** implementation discovers a real runtime contract mismatch, **When** it cannot be fixed as presentation-only, **Then** the worker stops or creates a focused follow-up instead of silently changing contracts.

### Edge Cases

- Missing, empty, sparse, or malformed treasury/source state files must produce safe default output.
- Dynamic GM-authored text remains escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Default output must not include raw JSON, `UiRawJsonBlock`, `game_state/`, drive/path separators from local file paths, API, DTO, endpoint, protocol, debug, stack traces, or raw parser exception text.
- Advanced/debug diagnostics must require explicit advanced-mode context; ordinary command execution must not depend on frontend-only hiding.
- If an advanced-mode API/DTO already exists, preserve it; do not invent a new React-side debugging contract unless the existing C# command-result model cannot express the boundary.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Default browser/player output for `/shining_treasury` MUST NOT include raw JSON blocks, raw state-file paths, local filesystem paths, API/DTO/endpoint/protocol/debug wording, malformed JSON warning path text, or raw parser exception/stack-trace copy.
- **FR-002**: Default browser/player output for `/source_of_light` MUST meet the same no-raw-diagnostics boundary as FR-001.
- **FR-003**: Default output MUST retain useful Russian/in-world summary or unavailable guidance instead of becoming empty or silently hiding the whole command result.
- **FR-004**: Advanced/debug diagnostics for treasury/source MUST remain accessible only through explicit advanced-mode mechanisms where such mechanisms already exist or can be wired through shared C# command-result metadata without changing gameplay authority.
- **FR-005**: Regression/source-guard coverage MUST distinguish default vs advanced behavior and include malformed/sparse-state diagnostics where the current code can reproduce them.
- **FR-006**: The implementation MUST preserve existing Shining write/prompt authority: no new pending/control files, no local-turn write contracts, no validation/normalizer/schema churn, no GM prompt/example changes unless a true contract change is made.
- **FR-007**: Final evidence MUST link back to #1072, the #1065 review note, #949 AFD-004, and record #1066/#1067 as non-closing sibling follow-ups.

### Key Entities

- **Default player command result**: The blocks/actions returned to ordinary Browser Client command execution without advanced/debug opt-in.
- **Advanced diagnostics**: Raw or technical output intended for troubleshooting, visible only in explicit advanced/debug contexts.
- **Shining treasury/source surface**: Existing read-only presentation and local action/prompt command surfaces for Shining treasury and Source of Light state.
- **Malformed diagnostic**: Parser/file-state warning currently capable of leaking raw path or implementation details into player-facing output.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove `/shining_treasury` and `/source_of_light` default browser results remain useful but contain no raw diagnostics/path/meta copy.
- **SC-002**: Focused tests or source guards prove advanced/debug diagnostics are gated behind explicit advanced mode where retained.
- **SC-003**: Existing Shining/browser command suites and afterlife drill-down audit tests continue to pass.
- **SC-004**: No React-side gameplay authority, runtime contract, pending/control, validation, normalizer, GM prompt/example, or manifest change is introduced unless the PR updates required docs/tests and explains why.
- **SC-005**: Verification includes Spec Kit prerequisite resolution, focused default-vs-advanced tests, a broader afterlife/Shining/browser command slice, C# builds when C# changes, `git diff --check`, and added-line static/security scan.

## Verification Plan *(mandatory)*

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1072-shining-advanced-diagnostics`.
- **Focused RED/GREEN candidate**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`.
- **Broader afterlife/Shining/browser slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Build gates if C# source changes**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- **Frontend verification if React/Vite files change**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Documentation-sensitive gate if afterlife runtime contracts/docs change**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- **Diff/security**: `git diff --check origin/main...HEAD` plus added-line static/security scan over changed non-plan code.

## Assumptions

- The raw diagnostic leak can be fixed in shared C# command-result builders/services rather than the React frontend.
- Existing advanced/debug mode or command-result metadata can carry diagnostics without exposing them to default player mode.
- #1072 is a focused presentation/read-only cleanup; #1066 and #1067 remain separate afterlife drill-down children.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this change improves command-result output boundaries without reintroducing obsolete card-heavy UI criteria.
