# Feature Specification: Afterlife Detail Drill-Down Audit

**Feature Branch**: `949-afterlife-drilldown-audit`

**Created**: 2026-06-16

**Status**: Drafted for autonomous implementation

**Input**: GitHub issue #949 — "[Task] Audit afterlife screens for missing detail drill-down flows"

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949
- **Related precedent**: #948 mortal read-only drill-down audit and its closed follow-ups #1054, #1055, #1056, #1057.
- **Issue type**: audit / afterlife UX parity / console-browser command-result discoverability.
- **Spec Kit justification**: Required. #949 spans Chaos Sea and Shining Abode afterlife surfaces, console/browser parity, player-facing detail UX, audit artifacts, tests/source guards, and possible follow-up issue creation. It is explicitly an afterlife analogue of #948 and must preserve afterlife contract documentation guardrails.
- **Contract scope**: player-facing afterlife read-only and local-action overview/detail command surfaces, console/browser command-result parity tests/source guards, afterlife audit documentation, and these Spec Kit artifacts. Runtime state schema, pending/control files, validation, normalizer behavior, GM prompts/examples, and afterlife contracts must not change unless a focused implementation need is proven and the relevant docs/tests are updated in the same PR.
- **Primary surfaces to audit**: `/guardians` / `/хранители`, `/abodes` / `/обители`, `/abode_power` / `/сила_обители`, `/soul_relics` / `/реликвии`, `/archive_candidates` / `/архив_души`, Guardian projects/trade/social/residents, Shining Abode gates/politics/factions/projects/trade/forge/treasury/Source of Light/native faction actions, spiritual conflict commands, afterlife profiles, threats, chronicles, and inbox-linked entities.
- **Explicitly out of scope**: Mortal World drill-downs (#946/#947/#948/#1054-#1057), broad Browser Client visual retheme, new React gameplay authority, mutating prompt-session/write parity unless already required by an existing tracked afterlife issue, hidden runtime contract changes, and closing afterlife child gaps without either a focused fix or a linked follow-up issue.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Audit afterlife overview/detail discoverability (Priority: P1)

A player can rely on the audit to identify which afterlife overview screens already provide natural list-to-detail paths and which screens still require focused follow-up work.

**Why this priority**: #949 is first an audit closure unit. It should not broaden into every afterlife detail implementation at once.

**Independent Test**: A source/audit guard verifies the audit artifact enumerates the known afterlife command/surface categories and classifies each as adequate, fixed in this PR, or linked to a follow-up issue.

**Acceptance Scenarios**:

1. **Given** an afterlife overview command lists rich entities, **When** the audit runs, **Then** the audit records whether the console and browser offer a player-facing detail path for a representative entity.
2. **Given** a confirmed gap is too broad for this PR, **When** #949 is closed, **Then** the audit artifact contains a linked GitHub follow-up issue with the exact command/surface and parity gap.

---

### User Story 2 - Preserve existing overview outputs while adding only small safe fixes (Priority: P1)

Existing afterlife overview panels remain visible. If the audit finds a small safe gap, the PR may add a minimal read-only detail affordance, but larger gaps become linked issues.

**Why this priority**: Afterlife data is contract-sensitive. The audit must not silently rewrite runtime contracts or large flows.

**Independent Test**: Focused tests verify any small in-PR fix keeps overview output green and exposes a safe selected-detail or equivalent detail affordance without raw-only/generic-completion output.

**Acceptance Scenarios**:

1. **Given** an existing afterlife command already renders an overview, **When** a small detail path is added, **Then** the overview output remains available and the selected detail uses Russian/in-world player-facing labels.
2. **Given** a detail path would require new pending/control contracts, validation changes, or GM-authored schema changes, **When** the audit classifies the gap, **Then** it creates or links a follow-up issue rather than hiding the contract work inside #949.

---

### User Story 3 - Keep console/browser parity and afterlife docs guardrails explicit (Priority: P1)

The audit records console/browser parity for each covered surface and states whether afterlife GM-facing documentation changed or was intentionally unaffected.

**Why this priority**: #949 crosses afterlife and UI boundaries where contract drift is risky.

**Independent Test**: Tests/source guards cover the audit artifact and any changed command-result behavior; documentation coverage tests run if afterlife runtime contracts, examples, or GM-authored docs change.

**Acceptance Scenarios**:

1. **Given** a browser detail affordance exists without console parity or vice versa, **When** #949 closes, **Then** the parity gap is either fixed or linked to a follow-up issue.
2. **Given** no runtime contract or GM-authored prompt/example changes are made, **When** #949 reports docs impact, **Then** it states that afterlife contract docs were not changed because the PR was audit/player-facing UI only.
3. **Given** any afterlife contract behavior is changed, **When** verification runs, **Then** `AfterlifeDocumentationCoverageTests` and `ExampleDocumentationValidationTests` are updated and pass.

### Edge Cases

- Missing or sparse afterlife JSON fixtures must produce graceful empty/unavailable audit classifications, not invented placeholder details.
- Raw/debug JSON may remain available only as established diagnostics; it cannot be the only default player-facing way to inspect a covered rich entity.
- Dynamic GM-authored afterlife text must remain escaped/sanitized before Spectre.Console markup or browser-rendered HTML.
- Console and browser can differ in layout, but they must expose equivalent player-facing detail semantics or a tracked follow-up.
- Follow-up issue titles must be specific enough for future autonomous workers to implement without re-running the entire #949 audit.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: Create or update a durable audit artifact under `docs/audits/` that covers afterlife read-only and local-action overview commands in Chaos Sea and Shining Abode flows.
- **FR-002**: The audit MUST classify each covered surface as `adequate`, `fixed in #949`, `follow-up required`, or `not applicable`, with a short player-facing reason.
- **FR-003**: Existing overview outputs MUST remain available for every afterlife surface touched by this PR.
- **FR-004**: Each confirmed missing detail drill-down gap MUST have either a small focused fix in this PR or a linked GitHub follow-up issue created/commented before closure.
- **FR-005**: Browser and console parity gaps MUST be explicitly recorded in the audit artifact and tests/source guards.
- **FR-006**: Any in-PR detail affordance MUST reuse existing C# command-result/Explorer command patterns where practical and MUST NOT add React-side gameplay rules.
- **FR-007**: The branch MUST remain read-only unless a tracked follow-up explicitly authorizes mutating afterlife prompt/write parity work.
- **FR-008**: The implementation MUST NOT change afterlife runtime contracts, pending/control file names, validation rules, normalizer side effects, GM prompts, examples, or manifests unless the same PR updates the required GM-facing docs/tests.
- **FR-009**: Tests/source guards MUST fail if the audit artifact omits the major #949 candidate categories or if a changed afterlife detail path regresses to raw-only/generic-completion output.
- **FR-010**: The PR body and issue evidence comment MUST link #949, list any follow-up issues created, state local verification evidence, and state whether GitHub Actions were used.

### Key Entities

- **Afterlife overview surface**: A command/result section in Chaos Sea or Shining Abode that lists guardians, abodes, relics, archive candidates, projects, politics, residents, threats, chronicles, conflict entries, or related journals.
- **Detail drill-down**: A player-facing command argument, action, selector, panel, or command-result affordance that opens one entity/section without requiring raw JSON inspection.
- **Audit classification**: The durable record that says whether a surface is adequate, fixed in #949, needs a follow-up, or is not applicable.
- **Parity gap**: A difference where console or browser exposes a detail path that the other client cannot reach, or where one client collapses safe detail output into a generic/raw-only result.

## Success Criteria *(mandatory)*

- **SC-001**: `docs/audits/afterlife-drilldown-audit.md` or equivalent records all issue-listed candidate categories with classification, severity, console/browser parity notes, and follow-up links where needed.
- **SC-002**: Focused tests/source guards prove the audit artifact stays synchronized with the candidate category list and any in-PR fixes.
- **SC-003**: Any small in-PR fix has RED/GREEN evidence and preserves overview output.
- **SC-004**: Larger gaps are split into linked GitHub issues rather than hidden or deferred without tracking.
- **SC-005**: Verification includes focused afterlife/audit tests, a broader afterlife/browser/console slice, relevant builds, Spec Kit prerequisite check, `git diff --check`, and an added-line static/security scan over changed non-plan code.
- **SC-006**: Docs/prompts impact is explicitly reported as either no runtime/GM contract change or updated files/tests when contract docs are affected.

## Verification Plan *(mandatory)*

- **Spec Kit prerequisite check**: `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` must resolve `specs/949-afterlife-drilldown-audit`.
- **Baseline/focused C# slice before production changes**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~Afterlife|FullyQualifiedName~Shining|FullyQualifiedName~Chaos|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerCommandMigrationRegistryTests" --logger "console;verbosity=minimal"`.
- **Documentation-sensitive gate if afterlife runtime contracts/docs change**: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity=minimal"`.
- **Build gates if C# source changes**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` and `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true`.
- **Frontend verification if React/Vite files change**: `npm run verify --prefix BookOfEternityClient.WebFrontend`.
- **Diff/security**: `git diff --check origin/main...HEAD` plus an added-line static/security scan over changed non-plan code.

## Assumptions

- #949 should normally close by producing a trustworthy audit plus small safe fixes and linked child issues, not by implementing every afterlife detail gap in one PR.
- Existing good patterns such as guardian list/detail, soul relic/archive inbox detail, and guardian project detail should be recorded as adequate before adding new work.
- If the audit discovers that a desired detail flow requires a new runtime/GM-authored afterlife contract, the safe path is a linked follow-up issue with docs/tests requirements.
- The current Browser Client direction remains minimalist tabs plus a single command input and `/help` discovery; this issue should not recreate obsolete card-heavy UI criteria.
