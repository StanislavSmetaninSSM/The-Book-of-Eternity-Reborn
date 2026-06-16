# Feature Specification: Afterlife Profile and Inbox Follow-Through Drill-Downs

**Feature Branch**: `work/1066-afterlife-profile-inbox-drilldowns`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #1066 — "[Task] Add afterlife profile and inbox follow-through drill-downs"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1066 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1066
- **Origin audit**: #949 AFD-005 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949 and `docs/audits/afterlife-drilldown-audit.md`.
- **Issue type**: Browser Client read-only/detail/follow-through parity for afterlife profile, threat, chronicle, and inbox/support rows.
- **Spec Kit justification**: Required. #1066 changes player-facing Browser Client UX, console/browser parity, and afterlife detail/follow-through affordances across multiple shared C# command-result surfaces.
- **Contract scope**: shared C# command-result blocks/actions for existing read-only browser/console-compatible afterlife surfaces. Existing runtime state schemas, pending/control files, write/prompt authority, validation, normalizers, GM prompts/examples/manifests, and React gameplay logic are out of scope unless implementation proves a contract change and updates required docs/tests in the same PR.
- **Primary surfaces**: `/afterlife_profiles`, `/afterlife_threats`, `/afterlife_chronicles`, `/afterlife_inbox`, and read-only follow-through links from inbox/support rows to existing Guardian, archive, Shining, resident, project, and trade views where those links already have safe command-result authority.
- **Explicitly out of scope**: #1063 Guardian/Abode drill-downs, #1064 Soul Relic/Archive selected details, #1065 Shining Abode inspection details, #1067 spiritual conflict exchange/art drill-downs, new afterlife write operations, new pending/control files, new GM-authored contracts, React-side gameplay authority, and broad visual redesign.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Players can inspect one profile/threat/chronicle row (Priority: P1)

A player reading an afterlife profiles, threats, or chronicles overview in the browser can open the row they care about as a selected detail without losing the overview or seeing raw state.

**Why this priority**: AFD-005 found that overview surfaces are useful, but row-level follow-through is incomplete; players should not reconstruct context manually from summaries.

**Independent Test**: Focused browser command-result tests seed visible profile/threat/chronicle data, execute the overview commands, assert that safe detail actions are exposed for concrete rows, execute the selected-detail command/action path, and verify player-facing Russian/in-world detail output without raw JSON, API/DTO/debug wording, hidden/gm-only fields, or local paths.

**Acceptance Scenarios**:

1. **Given** visible afterlife profiles exist, **When** `/afterlife_profiles` renders in default browser mode, **Then** each concrete visible profile row exposes a safe inspect/follow-through action and the selected profile detail preserves relationship/fate/activity context without raw IDs unless advanced mode is explicitly active.
2. **Given** visible afterlife threats exist, **When** `/afterlife_threats` renders, **Then** each concrete threat row exposes a read-only detail action and hidden/gm-only threat material remains hidden in default output.
3. **Given** visible afterlife chronicles exist, **When** `/afterlife_chronicles` renders, **Then** each concrete chronicle/event row exposes a selected-detail action and the detail uses player-facing chronology/cause/effect copy rather than raw state.

---

### User Story 2 - Inbox/support rows lead to their existing context (Priority: P1)

A player reading `/afterlife_inbox` can follow a notification to the relevant existing read-only context such as a Guardian, archive entry, Shining project/faction, resident, project, trade, profile, threat, or chronicle when that link is safely resolvable.

**Why this priority**: The inbox is where the player learns something changed; follow-through should reduce manual command reconstruction while staying read-only.

**Independent Test**: Tests seed inbox notifications with supported target/link metadata and assert overview actions open exact existing context/details without auto-marking messages read unless the existing explicit action says so. Missing or stale targets render a clear player-facing unavailable state.

**Acceptance Scenarios**:

1. **Given** an inbox notification references a known Guardian/archive/resident/project/Shining/profile/threat/chronicle context, **When** the browser renders the notification, **Then** it exposes a follow-through action that opens the existing safe detail/context output for that target.
2. **Given** the target is missing, stale, hidden, or unsupported, **When** the player opens the follow-through, **Then** the result explains the unavailable context in Russian/in-world terms and does not leak raw target IDs, file paths, API/DTO/debug copy, or raw JSON.
3. **Given** an inbox row also has a mutating acknowledgement/read action, **When** a player uses a read-only follow-through action, **Then** it does not auto-mark the notification read or create pending/write state.

---

### User Story 3 - Read-only boundary and existing contracts are preserved (Priority: P2)

The feature improves selected-detail/follow-through presentation only. Existing afterlife profiles, inbox, archive, pending/control, validation, and GM-facing contracts remain authoritative and unchanged.

**Why this priority**: #1066 is an AFD-005 browser parity child, not a runtime/GM contract rewrite.

**Independent Test**: Command-result tests plus existing migration/afterlife audit tests continue to pass; docs/prompts impact review records no runtime contract changes unless implementation discovers a true contract mismatch and updates required docs/tests.

**Acceptance Scenarios**:

1. **Given** existing overview commands still render, **When** selected details/actions are added, **Then** overview output remains present and useful.
2. **Given** existing local write/prompt flows exist for inbox/support actions, **When** follow-through actions are added, **Then** mutating flows still route through existing C# prompt/write services.
3. **Given** no runtime contract changes are made, **When** closure evidence is written, **Then** it explicitly states that afterlife GM docs/examples/manifests were not changed because the diff is presentation/read-only only.

### Edge Cases

- Missing, sparse, stale, malformed, hidden, or gm-only rows must produce safe default output and never raw state/path/debug leakage.
- Dynamic GM-authored text must remain escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Follow-through should prefer stable IDs already present in canonical state/action metadata; if an ID cannot be resolved safely, show a player-facing unavailable reason rather than inventing state.
- Advanced/debug mode may expose raw identifiers/diagnostics where existing advanced pathways allow it; ordinary default output must not.
- Sibling issue #1067 remains separate; do not add spiritual conflict/art drill-downs here except as non-closing references.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Default browser results for `/afterlife_profiles` MUST expose read-only inspect/detail actions for concrete visible profile rows where a stable row identity exists.
- **FR-002**: Default browser results for `/afterlife_threats` MUST expose read-only inspect/detail actions for concrete visible threat rows without exposing hidden/gm-only threat details.
- **FR-003**: Default browser results for `/afterlife_chronicles` MUST expose read-only inspect/detail actions for concrete visible chronicle/event rows and preserve hidden visibility flags.
- **FR-004**: `/afterlife_inbox` MUST expose read-only follow-through actions for supported notification/context links to existing safe detail or context surfaces, without mutating read status.
- **FR-005**: Missing/stale/unsupported link targets MUST return player-facing unavailable text, not raw IDs, raw JSON, API/DTO/debug/protocol wording, local paths, or generic completion text.
- **FR-006**: Existing overview output MUST remain available for all scoped commands.
- **FR-007**: Existing write/prompt authority MUST be preserved: no new pending/control files, no local-turn write contracts, no validation/normalizer/schema churn, and no React-side gameplay rules.
- **FR-008**: Regression/source-guard coverage MUST include overview action exposure, selected-detail rendering, inbox follow-through, missing/stale targets, and default no-raw/no-debug boundaries.
- **FR-009**: Final evidence MUST link back to #1066, #949 AFD-005, `docs/audits/afterlife-drilldown-audit.md`, and record #1067 as an out-of-scope sibling follow-up.

### Key Entities

- **Afterlife row detail action**: A browser command-result action that opens a read-only selected detail for one visible profile, threat, chronicle, or inbox target.
- **Follow-through target**: A stable reference from an inbox/support row to an existing context surface such as Guardian, archive, Shining, resident, project, trade, profile, threat, or chronicle.
- **Default player output**: The ordinary browser/console command-result blocks/actions shown without advanced/debug opt-in.
- **Unsupported or stale target**: A reference that cannot safely resolve to canonical visible state and must become player-facing unavailable text.

## Success Criteria *(mandatory)*

- **SC-001**: Focused tests prove overview commands expose selected-detail/follow-through actions while preserving overview output.
- **SC-002**: Focused tests prove selected profile/threat/chronicle/inbox detail results are player-facing and no-raw/no-debug in default mode.
- **SC-003**: Missing/stale target tests prove graceful unavailable states and no mutation of inbox read status for read-only follow-through.
- **SC-004**: Existing afterlife/browser command suites and #949 audit guards continue to pass.
- **SC-005**: No runtime contract, pending/control, validation, normalizer, GM prompt/example/manifest, or React gameplay authority change is introduced unless the PR updates required docs/tests and explains why.
- **SC-006**: Verification includes Spec Kit prerequisite resolution, focused browser/afterlife tests, a broader afterlife/browser/console slice, C# builds when C# changes, `git diff --check`, and added-line static/security scan.

## Verification Plan *(mandatory)*

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/1066-afterlife-profile-inbox-drilldowns`.
- **Focused RED/GREEN candidate**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~AfterlifeProfiles|FullyQualifiedName~AfterlifeThreats|FullyQualifiedName~AfterlifeChronicles|FullyQualifiedName~AfterlifeInbox|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~AfterlifeDrilldownAudit" --logger "console;verbosity=minimal"`.
- **Broader afterlife/browser slice**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Build gates if C# source changes**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- **Frontend verification if React/Vite files change**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Documentation-sensitive gate if afterlife runtime contracts/docs change**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- **Diff/security**: `git diff --check origin/main...HEAD` plus added-line static/security scan over changed non-plan code.

## Assumptions

- The needed follow-through can be implemented in shared C# command-result builders/action metadata, not by adding React gameplay handlers.
- Existing command/action metadata can carry safe selected-detail/follow-through commands or prompt-safe routes.
- #1066 is presentation/read-only/follow-through work; #1067 remains a separate spiritual conflict/art child.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this work enriches command-result action affordances without resurrecting obsolete card-heavy UI criteria.
