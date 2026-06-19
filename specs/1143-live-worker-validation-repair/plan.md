# Implementation Plan: Live GM Worker Validation Repair

**Branch**: `1143-live-worker-validation-repair` | **Date**: 2026-06-20 | **Spec**: `specs/1143-live-worker-validation-repair/spec.md`

**Source GitHub issue**: #1143 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1143

## Summary

Wire the existing worker bridge foundation into the live validation-repair path. The implementation should keep the old repair request path as the fallback, use fake workers for deterministic tests, and route all returned proposals through the existing validator/apply-gate services.

## Technical Context

**Language/Version**: C# / .NET 8

**Primary Areas**:

- `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- `BookOfEternityClient/Services/GmWorkers/`
- `BookOfEternityClient.Tests/*GmWorker*`
- `OtherGuides/GM_Worker_Bridges.md`
- `Examples/E_CLI_GM_Worker_Validation_Repair.txt`

**Constraints**:

- No worker may become canonical state authority.
- No worker path should be player-facing.
- No configured worker means no behavior change.
- Worker failure must degrade to current repair request behavior.
- Keep this slice validation-repair only.

## Architecture

The validation failure handler already packages validation issues and writes the legacy repair request. This feature adds a runtime delegator around that point:

1. Build the validation-repair `GmWorkerTaskPacket`.
2. Select a matching enabled worker profile from settings.
3. Run the task via `GmWorkerBridgePool.RunTaskAsync`.
4. Load/validate the proposal through `GmWorkerProposalStore` and `GmWorkerContractValidator`.
5. Route the proposal to `GmWorkerApplyGate`.
6. Record audit/status details.
7. Keep or restore legacy repair request behavior on skip/failure/rejection.

## Constitution Check

- **GitHub traceability**: PASS. Source issue #1143 is linked.
- **Spec Kit fit**: PASS. Runtime validation flow and worker contracts are cross-cutting.
- **Player-facing integrity**: PASS. This is GM/runtime diagnostics, not player copy.
- **Contract/state authority**: PASS. Apply gate remains the only supported worker proposal application path.
- **Test-first path**: PASS. Tests must be written and observed failing before production changes.
- **Verification evidence**: PASS. Focused and full C# commands are specified.

## Verification Commands

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|ValidationRepair"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
git diff --check
```
