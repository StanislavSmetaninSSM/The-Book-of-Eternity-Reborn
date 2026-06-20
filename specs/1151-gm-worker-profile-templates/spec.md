# Feature Specification: GM Worker Profile Templates

**Feature Branch**: `1151-gm-worker-profile-templates`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User direction to continue priority GM multi-agent work after adding the local worker CLI runner; GitHub issue #1151 tracks disabled local worker profile templates.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1151 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1151
- **Related issue(s)**: #1149 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1149
- **Issue type**: task / enhancement / runtime configuration
- **Spec Kit justification**: This changes worker profile defaults/templates, settings normalization, contract fixtures, GM docs, and tests. It is small but contract-sensitive.
- **Contract scope**: runtime settings, GM-facing docs, examples/contracts, tests.
- **Out of scope**: Interactive UI for editing worker profiles, auto-detecting installed agents, enabling any worker by default, and browser client work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Discover Safe Disabled Worker Templates (Priority: P1)

A user or GM maintainer can inspect settings/diagnostics/docs and see ready-made worker profile templates for Codex validation repair, Codex narrative drafting, and Codex analysis. They are disabled by default and use the local runner protocol.

**Why this priority**: The previous runner work is hard to use if the user must hand-write long launch commands and permission scopes.

**Independent Test**: A focused unit test reads default templates and verifies each one is disabled, hidden, runner-based, and valid according to `GmWorkerContractValidator`.

**Acceptance Scenarios**:

1. **Given** default worker profile templates, **When** they are validated, **Then** every template passes the worker profile contract.
2. **Given** default worker profile templates, **When** they are inspected, **Then** none are enabled by default.
3. **Given** default worker profile templates, **When** their launch commands are inspected, **Then** they use `gm_worker_cli_runner.ps1` and `-AgentCommand`.

---

### User Story 2 - Preserve Existing User Profiles (Priority: P1)

When a user already configured worker profiles, settings normalization must not overwrite, duplicate, or enable templates over the user's choices.

**Why this priority**: Templates are scaffolding. They must not become hidden configuration migration that changes live worker behavior.

**Independent Test**: A settings test loads a custom profile and verifies normalization preserves it instead of replacing it with templates.

**Acceptance Scenarios**:

1. **Given** loaded settings with a custom profile, **When** settings are applied, **Then** the custom profile remains present.
2. **Given** loaded settings with no worker profiles, **When** settings are applied, **Then** disabled templates are supplied for discoverability.
3. **Given** disabled templates, **When** worker routing runs, **Then** they do not dispatch any task until the user enables one.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The codebase MUST provide reusable default GM worker profile templates.
- **FR-002**: Templates MUST include validation-repair Codex, narrative-draft Codex, and analysis Codex profiles.
- **FR-003**: Templates MUST be disabled by default.
- **FR-004**: Templates MUST use hidden launch visibility.
- **FR-005**: Templates MUST use `BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1` with `-AgentCommand`.
- **FR-006**: Templates MUST pass `GmWorkerContractValidator.ValidateProfile`.
- **FR-007**: Settings normalization MUST preserve configured user profiles.
- **FR-008**: Settings with no configured worker profiles SHOULD receive disabled templates for discoverability.
- **FR-009**: Tests/docs/examples MUST stop using bare raw agent launch commands as canonical worker profile examples.
- **FR-010**: Documentation MUST state that templates are disabled and safe until the user explicitly enables them.

### Key Entities

- **Worker Profile Template**: A disabled `WorkerBridgeProfile` with role, runner launch command, timeout, and permissions.
- **Template Catalog**: Static source of default templates used by settings, tests, docs, and fixtures.
- **Settings Normalization**: Existing settings load path that normalizes profile visibility, timeouts, permissions, and now supplies templates when none are configured.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused template tests verify all default templates are disabled, hidden, runner-based, and valid.
- **SC-002**: Focused settings tests verify empty profiles receive templates, and existing profiles are preserved.
- **SC-003**: Documentation/source guards verify canonical examples use the runner-based templates.
- **SC-004**: Full C# suite remains green.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerProfileTemplate|GmWorkerBridgeContract|GmWorkerBridgeDocumentation" -p:BaseOutputPath=TestResults/bin/1151-templates/`
- **Documentation/contract verification**: Included in the focused command above.
- **Frontend verification**: N/A.
- **Manual/player-facing verification**: N/A; templates are disabled and not player-facing.

## Assumptions

- Disabled templates are safe to include in default settings because worker routing ignores disabled profiles.
- Users who want a completely empty list can leave templates disabled; deleting them is not a functional requirement for v1.
- Real CLI availability remains a user responsibility; templates do not check whether Codex is installed.
