# Implementation Plan: GM Worker Proposal Inbox Diagnostics

**Branch**: `1145-gm-worker-proposal-inbox` | **Date**: 2026-06-20 | **Spec**: `specs/1145-gm-worker-proposal-inbox/spec.md`

**Source GitHub issue**: #1145 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1145

## Summary

Add a read-only proposal inbox service and surface it in existing GM worker diagnostics. The implementation should reuse `GmWorkerProposalStore`, `GmWorkerAuditLog`, and existing console diagnostics patterns, while keeping proposal application policy unchanged.

## Technical Context

**Primary files**:

- `BookOfEternityClient/Services/GmWorkers/`
- `BookOfEternityClient/Core/GameEngine/GameEngine.OptionsAndSettings.cs`
- `BookOfEternityClient.Tests/*GmWorker*`
- `OtherGuides/GM_Worker_Bridges.md`
- `Examples/E_CLI_GM_Worker_Narrative_Draft.txt`

## Architecture

Create a proposal inbox reader that scans stored proposal files, tolerates malformed JSON, and joins proposal entries with audit events by proposal id/task id. Existing advanced console diagnostics can render a compact table with detail-oriented fields; future bridge APIs can reuse the same service.

## Constitution Check

- **GitHub traceability**: PASS. Source issue #1145 is linked.
- **Spec Kit fit**: PASS. Runtime diagnostics and GM-facing worker contract are cross-cutting.
- **Player-facing integrity**: PASS. Surface is advanced diagnostics only, not ordinary player UI.
- **Contract/state authority**: PASS. Read-only inbox; no new apply path.
- **Test-first path**: PASS. Add failing tests before implementation.

## Verification Commands

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|GmBridgeDiagnostics"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
git diff --check
```
