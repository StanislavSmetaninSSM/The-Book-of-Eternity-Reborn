# Feature Specification: Shining Abode Browser Command Output Parity

**Feature Branch**: `work/1125-shining-browser`

**Created**: 2026-06-21

**Status**: Draft for implementation

**Input**: GitHub issue #1125 — "[Task] Fix Shining Abode browser command output parity"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1125 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1125
- **Parent issue**: #1118 — browser command output semantic parity epic.
- **Related prior specs/issues**: #949 afterlife drill-down audit, #1124 Chaos Sea browser parity, and existing Shining Abode parity tests.
- **Issue type**: Browser Client player-facing Shining Abode parity hardening.
- **Spec Kit justification**: Required. The issue spans afterlife/Shining Abode UX, browser/console parity, hidden-data boundaries, mutating preview safety, and documentation-sensitive contract surfaces.
- **Contract scope**: Existing Shining Abode browser command output and existing action preview/form surfaces. No new runtime state schema, pending/control action type, validator rule, normalizer side effect, GM prompt, or example contract is intended unless tests expose a necessary contract gap.
- **Out of scope**: Chaos Sea browser parity (#1124), generic React block renderer (#1126), new afterlife mechanics, visual redesign, and non-Shining mortal-world command output.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Inspect Shining Abode overviews (Priority: P1)

A browser player viewing Shining Abode overviews can understand politics, factions, projects, gates, treasury, trade, forge, source-of-light, and resident interaction surfaces without reading raw JSON or internal identifiers.

**Why this priority**: Shining Abode is dense and contract-sensitive; browser output must expose useful player-facing summaries before players commit irreversible or pending actions.

**Independent Test**: Seed representative Shining Abode state and execute overview commands; assert readable Russian summaries, safe actions, and absence of raw diagnostic blocks.

**Acceptance Scenarios**:

1. **Given** visible Shining Abode records exist, **When** their browser overview command runs, **Then** the result shows a useful player-facing summary and safe detail/action affordances.
2. **Given** hidden, debug, or GM-only Shining state exists, **When** default browser output renders, **Then** that data is not visible outside advanced diagnostics.

---

### User Story 2 - Open selected Shining details before acting (Priority: P1)

A browser player can open focused details for dense Shining records before choosing a mutating action.

**Why this priority**: The browser must not force players to choose actions from cryptic identifiers or compressed rows.

**Independent Test**: Execute selected detail commands for representative Shining surfaces where console has focused inspection panels/selectors; assert the selected record is fully described and can return to the parent surface.

**Acceptance Scenarios**:

1. **Given** a selected Shining record exists, **When** the player opens its detail command, **Then** the detail includes the selected title, summary, current state, risks/costs where applicable, and back action.
2. **Given** a selected target is stale or missing, **When** the detail command runs, **Then** the player receives a completed unavailable message rather than a crash or diagnostic dump.

---

### User Story 3 - Keep action previews and local writes safe (Priority: P1)

Browser Shining actions that create pending/control files remain explicit preview/form flows with confirmation-oriented copy.

**Why this priority**: Shining Abode has strict afterlife contract rules; output parity must not accidentally create writes or obscure GM-resolved pending work.

**Independent Test**: Existing Shining action/form tests remain passing; new tests distinguish read-only details from mutating previews.

**Acceptance Scenarios**:

1. **Given** a Shining command can mutate state or create pending work, **When** opened in browser, **Then** it uses the existing safe preview/form path and does not masquerade as a read-only detail.
2. **Given** the player cancels or only inspects a preview, **When** the command completes, **Then** no pending/control write occurs.

### Edge Cases

- Sparse or missing Shining state returns friendly empty/unavailable output.
- Internal ids may appear only where needed as command targets; player-facing labels must carry enough meaning without them.
- Hidden/GM-only fields, concealed political truths, debug markers, raw pending/control JSON, and Fate/Card internals remain hidden in default output.
- Mutating commands remain clearly distinguished from read-only inspection.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Browser overview commands for Shining Abode dense systems MUST render useful player-facing summaries without default raw JSON.
- **FR-002**: Dense lists/tables MUST expose selected detail actions when console has a corresponding inspection panel/selector or when a row cannot be understood from the overview alone.
- **FR-003**: Selected detail commands MUST render focused Russian player-facing detail surfaces and include a back action to the parent command.
- **FR-004**: Missing selected targets MUST return completed, player-facing unavailable output.
- **FR-005**: Mutating Shining actions MUST remain safe preview/form flows and MUST NOT create pending/control files during read-only inspection.
- **FR-006**: Hidden, GM-only, debug, and raw contract fields MUST remain hidden in default browser output.
- **FR-007**: If runtime afterlife contracts change, the PR MUST update `OtherGuides/Afterlife_Contract_Matrix.md`, examples/manifests, and afterlife documentation coverage tests as required by `AGENTS.md`.

### Key Entities

- **Shining Abode overview**: Player-facing summary for settlement, politics, factions, projects, gates, residents, treasury, trade, forge, or source-of-light surfaces.
- **Shining detail route**: Read-only browser command that opens one selected visible record.
- **Shining action preview/form**: Existing safe browser surface for commands that may create pending/control writes.
- **Hidden Shining data**: GM-only, concealed, debug, or internal state not intended for default player output.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused tests cover representative Shining overview output for the systems listed in #1125.
- **SC-002**: Focused tests cover selected detail actions/details for every audited Shining gap found during implementation.
- **SC-003**: Tests assert default output does not include raw JSON/debug/hidden markers for the covered Shining surfaces.
- **SC-004**: Broad `Shining|Afterlife|ExplorerWebCommand` verification passes before merge.
- **SC-005**: Documentation coverage tests pass if, and only if, runtime afterlife contracts change.

## Verification Plan *(mandatory)*

- **Audit commands**: Inspect `ExplorerMode.Afterlife.ShiningAbode*.cs`, `ExplorerShiningAbodeCommandResultBuilder`, existing Shining browser tests, `docs/audits/afterlife-drilldown-audit.md`, and `OtherGuides/Afterlife_Contract_Matrix.md`.
- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "Shining|Afterlife|ExplorerWebCommand" --verbosity minimal`.
- **Documentation/contract verification**: Run `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests` only if runtime afterlife contracts change.
- **Frontend verification**: Not required unless React/Vite files change.
- **Manual/browser verification**: Optional command run against Shining test data for the audited commands.

## Assumptions

- Existing Shining browser tests already cover some parity surfaces; this issue should harden gaps rather than rewrite the Shining browser architecture.
- Some Shining commands are intentionally mutating preview/form flows, not read-only detail routes.
- No afterlife GM-authored contract change should be necessary unless implementation discovers a missing contract route.
