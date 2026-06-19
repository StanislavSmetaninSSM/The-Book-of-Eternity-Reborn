# Implementation Plan: Proposal-Only GM Worker Dispatch

**Branch**: `1147-proposal-only-worker-dispatch` | **Date**: 2026-06-20 | **Spec**: `specs/1147-proposal-only-worker-dispatch/spec.md`

**Source GitHub issue**: #1147 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1147

## Summary

Add a read-only dispatch path that lets the main GM/bridge send narrative-draft and analysis requests to configured workers and receive proposals in the existing inbox.

## Technical Context

**Primary files**:

- `BookOfEternityClient/Services/GmWorkers/`
- `BookOfEternityGMBridge/Program.cs`
- `BookOfEternityClient.Tests/*GmWorker*`
- `OtherGuides/GM_Worker_Bridges.md`
- `Examples/E_CLI_GM_Worker_Narrative_Draft.txt`

## Architecture

Create a proposal-only dispatch service that validates request input, hashes readable context files, builds `WorkerTaskPacket` via existing builders, runs the selected worker with `GmWorkerBridgePool`, and returns a compact dispatch result. Add a bridge command for narrative/analysis dispatch that uses the same service.

## Constitution Check

- **GitHub traceability**: PASS. Source issue #1147 is linked.
- **Spec Kit fit**: PASS. Runtime bridge command and GM-facing worker contract are cross-cutting.
- **Player-facing integrity**: PASS. GM/bridge diagnostics only, no ordinary player UI.
- **Contract/state authority**: PASS. Proposal-only, no canonical apply path.
- **Test-first path**: PASS. Add failing tests before implementation.

## Verification Commands

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|GmBridgeDiagnostics"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
git diff --check
```
