# Feature Specification: GM Worker CLI Runner

**Feature Branch**: `1149-gm-worker-cli-runner`

**Created**: 2026-06-20

**Status**: Draft

**Input**: User direction to continue the GM multi-agent work autonomously, with GitHub issue #1149 tracking a local runner that makes Codex/Gemini worker profiles follow the existing proposal protocol.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: #1149 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1149
- **Related architecture issue(s)**: #1141 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1141
- **Issue type**: task / enhancement / runtime protocol hardening
- **Spec Kit justification**: The work changes the GM worker launch contract, adds a repo-owned CLI entrypoint, updates GM-facing worker documentation, and adds regression coverage for a bridge protocol intended for multiple future agent profiles.
- **Contract scope**: GM-facing docs, runtime worker launch protocol, tests, examples/contracts. No player-facing console/browser command behavior changes.
- **Out of scope**: Automatic proposal application, full proposal merger UI, remote/cloud orchestration, requiring Codex/Gemini in CI, and browser client work.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Generate a Strict Worker Prompt Without an Agent Installed (Priority: P1)

A developer or GM bridge maintainer can run the local worker runner in dry-run mode with the same `BOE_WORKER_*` environment variables used by the live bridge. The runner writes the exact prompt it would feed to a worker, making the protocol testable without installing Codex, Gemini, or another CLI agent.

**Why this priority**: The bridge needs regression coverage for the prompt/proposal contract. Dry-run makes that possible in CI and local tests without external agent dependencies.

**Independent Test**: A C# test creates a fake task packet and session directory, sets `BOE_WORKER_TASK_PATH`, `BOE_WORKER_PROPOSAL_PATH`, and `BOE_WORKER_SESSION_PATH`, invokes the runner with `-DryRun`, and asserts the generated prompt contains the task JSON, proposal path, `worker-proposal-v1`, and the direct-state-write ban.

**Acceptance Scenarios**:

1. **Given** valid worker environment variables and an existing task packet, **When** the runner is invoked with `-DryRun -PromptOutPath`, **Then** it exits with code 0 and writes the generated prompt to the requested file.
2. **Given** the generated prompt, **When** it is inspected, **Then** it tells the worker to write exactly one proposal JSON to `BOE_WORKER_PROPOSAL_PATH`.
3. **Given** the generated prompt, **When** it is inspected, **Then** it explicitly forbids direct canonical `game_session` edits.

---

### User Story 2 - Fail Clearly on Invalid Worker Runtime Environment (Priority: P1)

When a worker profile points at the runner but the bridge or operator misconfigures the task path, proposal path, or session path, the runner exits non-zero with a clear error before launching an external agent.

**Why this priority**: Bad environment setup should fail at the runner boundary, not create confusing empty proposal inbox entries or launch an agent with unusable instructions.

**Independent Test**: A C# test invokes the runner without required `BOE_WORKER_*` variables and asserts non-zero exit with the missing variable name in stderr.

**Acceptance Scenarios**:

1. **Given** `BOE_WORKER_TASK_PATH` is missing, **When** the runner starts, **Then** it exits non-zero and reports `BOE_WORKER_TASK_PATH`.
2. **Given** `BOE_WORKER_TASK_PATH` points to a missing file, **When** the runner starts, **Then** it exits non-zero before launching the configured agent command.
3. **Given** `BOE_WORKER_SESSION_PATH` points to a missing directory, **When** the runner starts, **Then** it exits non-zero before launching the configured agent command.

---

### User Story 3 - Launch a Configured CLI Agent Through One Protocol Entry Point (Priority: P2)

A user can configure a worker profile to launch the repo runner and pass the actual Codex/Gemini command as a runner argument. The runner feeds the generated prompt through stdin, keeps execution non-interactive/hidden under the existing bridge process options, and requires a non-empty proposal file after the agent exits.

**Why this priority**: This makes future worker profiles repeatable and less error-prone. The bridge already handles hidden process launch and proposal validation; the runner standardizes the subprocess prompt handoff.

**Independent Test**: Source and documentation tests assert the runner accepts an agent command argument, uses stdin for the prompt, and documents profile examples for Codex/Gemini-style commands. CI does not need to execute real Codex/Gemini.

**Acceptance Scenarios**:

1. **Given** a valid environment and an agent command, **When** the runner runs in real mode, **Then** it launches that command and writes the prompt to stdin.
2. **Given** the agent exits successfully but no proposal file is written, **When** the runner finishes, **Then** it exits non-zero with a clear "proposal missing" error.
3. **Given** the agent exits successfully and writes a non-empty proposal file, **When** the runner finishes, **Then** it exits 0 and leaves proposal validation to the existing bridge pool/apply gate.

### Edge Cases

- Task packet file is empty or unreadable.
- Proposal output directory does not exist yet.
- Proposal output path exists from a previous failed run.
- Agent command exits non-zero.
- Agent command hangs longer than the runner timeout.
- Agent command writes stdout/stderr with non-ASCII text.
- Agent command writes a malformed proposal; the runner only checks non-empty handoff, and the existing bridge pool remains responsible for JSON/schema validation.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The repository MUST provide a local worker runner script under `BookOfEternityClient/Launcher/`.
- **FR-002**: The runner MUST read `BOE_WORKER_TASK_PATH`, `BOE_WORKER_PROPOSAL_PATH`, and `BOE_WORKER_SESSION_PATH`.
- **FR-003**: The runner MUST fail before launching an agent when a required environment variable is missing.
- **FR-004**: The runner MUST fail before launching an agent when the task packet file or session directory does not exist.
- **FR-005**: The runner MUST create the proposal output directory when needed.
- **FR-006**: The runner MUST support `-DryRun` mode that writes the generated prompt to `-PromptOutPath` or stdout without launching an agent.
- **FR-007**: The generated prompt MUST include the raw task packet JSON and all three worker path values.
- **FR-008**: The generated prompt MUST require exactly one `worker-proposal-v1` JSON proposal at `BOE_WORKER_PROPOSAL_PATH`.
- **FR-009**: The generated prompt MUST forbid direct edits to canonical `game_session` files and direct writes outside proposal content references.
- **FR-010**: In real mode, the runner MUST launch a configured agent command with stdin redirected and feed the generated prompt to stdin.
- **FR-011**: The runner MUST wait for the agent command, enforce a timeout, and exit non-zero on timeout or non-zero agent exit.
- **FR-012**: After a successful agent exit, the runner MUST require `BOE_WORKER_PROPOSAL_PATH` to exist and be non-empty.
- **FR-013**: Existing `GmWorkerBridgePool` proposal validation and apply gate MUST remain the authority for proposal schema, scope, and canonical application.
- **FR-014**: GM worker documentation MUST show how to configure a worker profile through the runner rather than a bare raw Codex/Gemini command.
- **FR-015**: Automated tests MUST cover dry-run prompt generation, missing environment failure, and docs/source guard coverage for the runner.

### Key Entities *(include if feature involves data)*

- **Worker Runner Script**: Repo-owned PowerShell entrypoint that adapts bridge environment variables into a strict agent prompt and optional subprocess launch.
- **Generated Worker Prompt**: Text prompt containing task packet JSON, proposal path, session path, authority rules, and output requirements.
- **Agent Command**: User-configured CLI command passed to the runner, for example Codex or Gemini.
- **Proposal Handoff File**: The file at `BOE_WORKER_PROPOSAL_PATH` that the external agent must create before the bridge reads and validates it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Focused tests can validate prompt generation and missing-env failure without Codex/Gemini installed.
- **SC-002**: The generated prompt includes `worker-proposal-v1`, the configured proposal path, the task packet JSON, and the direct canonical-state-write ban.
- **SC-003**: Worker bridge docs include at least one runner-based Codex launch command and explain why bare commands are not recommended.
- **SC-004**: Existing worker bridge lifecycle tests still pass, proving the runner did not bypass the existing bridge pool/apply gate.

## Verification Plan *(mandatory)*

- **C# verification**: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerCliRunner|GmWorkerBridgeDocumentation|WorkerBridge" -p:BaseOutputPath=TestResults/bin/1149-runner/`
- **Documentation/contract verification**: Included in `GmWorkerBridgeDocumentationTests` and source guard tests for the runner path and environment contract.
- **Frontend verification**: N/A.
- **Manual/player-facing verification**: N/A for player-facing UI; optionally run the runner in `-DryRun` mode with a sample task packet to inspect the prompt.

## Assumptions

- PowerShell remains an acceptable local launcher scripting surface because existing GM daemon/launcher scripts already use PowerShell.
- The bridge pool remains responsible for hidden process creation; the runner itself also avoids interactive windows when it launches a nested agent command.
- Real Codex/Gemini CLI compatibility is command-string based for v1; richer per-agent adapters can be added later through separate issues if a CLI requires special flags.
- A non-empty proposal file is the runner's final handoff check; schema and scope validation stay in the existing C# bridge.
