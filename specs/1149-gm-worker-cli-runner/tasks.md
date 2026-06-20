# Tasks: GM Worker CLI Runner

**Input**: Design documents from `specs/1149-gm-worker-cli-runner/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`, `contracts/gm-worker-cli-runner-contract.md`

**Tests**: Behavior changes require test-first tasks. Runner tests must fail before the script is added.

**Organization**: Tasks are grouped by independently testable user story.

## Phase 1: Setup

- [x] T001 Confirm source GitHub issue #1149, active branch `1149-gm-worker-cli-runner`, and initial `git status --short`.
- [x] T002 Read `AGENTS.md`, constitution, source issue #1149, and existing GM worker docs/tests.
- [x] T003 Create Spec Kit artifacts under `specs/1149-gm-worker-cli-runner/`.

---

## Phase 2: User Story 1 - Dry-Run Prompt Generation (Priority: P1)

**Goal**: The runner can generate a strict worker prompt without launching Codex/Gemini.

**Independent Test**: `GmWorkerCliRunnerTests` invokes the runner with `-DryRun` and verifies prompt content.

- [x] T004 Add failing `GmWorkerCliRunnerTests.RunnerDryRun_WritesPromptWithProposalProtocol` in `BookOfEternityClient.Tests/GmWorkerCliRunnerTests.cs`.
- [x] T005 Run focused test and confirm it fails because `BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1` is missing.
- [x] T006 Add `BookOfEternityClient/Launcher/gm_worker_cli_runner.ps1` with environment validation and dry-run prompt writing.
- [x] T007 Run focused test and confirm dry-run prompt generation passes.

---

## Phase 3: User Story 2 - Invalid Environment Failure (Priority: P1)

**Goal**: The runner exits clearly before launching an agent when required environment is invalid.

**Independent Test**: `GmWorkerCliRunnerTests` invokes the runner with missing env vars and verifies non-zero exit plus missing variable name.

- [x] T008 Add failing `GmWorkerCliRunnerTests.RunnerDryRun_WhenEnvironmentMissing_ReportsMissingVariable`.
- [x] T009 Run focused test after adding the missing-env assertion; it passed because T006 already introduced required environment validation.
- [x] T010 Complete runner environment validation for missing env, missing task file, missing session directory, and proposal directory creation.
- [x] T011 Run focused tests and confirm invalid-environment behavior passes.

---

## Phase 4: User Story 3 - Real Agent Launch Handoff (Priority: P2)

**Goal**: In real mode, the runner launches a configured CLI command, feeds the prompt through stdin, enforces timeout, and requires a non-empty proposal file.

**Independent Test**: `GmWorkerCliRunnerTests` uses a temporary fake agent script that reads stdin and writes the proposal handoff.

- [x] T012 Add failing `GmWorkerCliRunnerTests.RunnerRealMode_FeedsPromptToAgentAndRequiresProposal`.
- [x] T013 Run focused test and confirm the real-mode handoff fails before implementation.
- [x] T014 Implement real-mode process launch, stdin prompt feed, timeout, non-zero exit handling, and non-empty proposal check in `gm_worker_cli_runner.ps1`.
- [x] T015 Run focused tests and confirm real-mode handoff passes.

---

## Phase 5: Docs, Contracts, and Source Guards

- [x] T016 Update `OtherGuides/GM_Worker_Bridges.md` with runner-based profile examples and bare-command guidance.
- [x] T017 Update `Examples/E_CLI_GM_Worker_Validation_Repair.txt` and `Examples/E_CLI_GM_Worker_Narrative_Draft.txt` to mention the runner path.
- [x] T018 Extend `BookOfEternityClient.Tests/GmWorkerBridgeDocumentationTests.cs` or `GmWorkerCliRunnerTests.cs` to guard docs and runner path references.
- [x] T019 Run documentation/source guard focused tests.

---

## Phase 6: Verification and Merge

- [x] T020 Run focused verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorkerCliRunner|GmWorkerBridgeDocumentation|WorkerBridge" -p:BaseOutputPath=TestResults/bin/1149-runner/`.
- [x] T021 Run full verification: `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:BaseOutputPath=TestResults/bin/1149-full/`.
- [x] T022 Inspect `git diff`, ensure `BookOfEternityClient/client_profile/` remains untracked and excluded, then commit.
- [ ] T023 Open PR for #1149, wait for checks if available, merge if all verification is green.
