# Quickstart: Explicit GM Worker Bridges

## Goal

Validate the MVP flow: validation repair and narrative drafting are both delegated to configured worker bridges, workers return proposals, the apply gate validates repair changes or stores proposal-only drafts, and the audit trail records the result.

## Prerequisites

- Repository on feature branch `1113-gm-worker-bridges`.
- A local CLI command available for the selected worker profile, such as `codex --dangerously-bypass-approvals-and-sandbox`.
- Test fixture that produces a known validation failure.

## Scenario 1: No Worker Profiles Preserves Existing Gameplay

1. Start the console client with an ordinary single-GM configuration.
2. Run a short live turn.
3. Expected result: no worker bridge starts, no worker audit file is created, and the turn behavior matches current single-GM behavior.

## Scenario 2: Worker Repairs a Validation Failure

1. Configure a worker profile with role `validation-repair`.
2. Trigger a known validation failure.
3. Dispatch the validation errors to the worker.
4. Wait for a `WorkerProposal`.
5. Run the apply gate.
6. Expected result: proposal changes only allowed files, validation passes, files are applied, and audit contains dispatch/proposal/decision events.

## Scenario 3: Worker Proposal Is Rejected

1. Configure a validation repair worker.
2. Provide a worker proposal that changes a forbidden path.
3. Run the apply gate.
4. Expected result: proposal is rejected, canonical state is unchanged, and audit records the rejection reason.

## Scenario 4: Worker Drafts Narration Without Owning the Turn

1. Configure a worker profile with role `narrative-draft`.
2. Dispatch a scene-drafting task with read-only context and tone/continuity instructions.
3. Wait for a proposal with draft narration.
4. Expected result: the draft is visible to the main GM in the proposal inbox, no canonical files change, and nothing is sent to the player until the main GM explicitly uses or rewrites the draft.

## Scenario 5: Worker Timeout Is Safe

1. Configure a worker profile with a short timeout.
2. Dispatch a task to a worker that does not answer.
3. Expected result: worker status becomes `timed-out`, main GM receives a diagnostic, canonical state is unchanged, and ordinary repair/manual handling can continue.

## Verification Commands

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "WorkerBridge|GmBridge|ValidationRepair|ProposalOnly|AgentConsoleLiveSmokeTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|SourceGuard"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```
