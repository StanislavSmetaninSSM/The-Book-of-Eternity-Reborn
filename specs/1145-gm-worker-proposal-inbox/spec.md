# Feature Specification: GM Worker Proposal Inbox Diagnostics

**Feature Branch**: `1145-gm-worker-proposal-inbox`

**Created**: 2026-06-20

**Status**: Draft

**Source GitHub issue**: #1145 - https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1145

## Scope

GM workers can now store proposals, and validation repair can consume accepted proposals through the apply gate. This feature adds a GM-facing inbox/diagnostics surface so the main GM can list and inspect stored proposals without using raw JSON.

This is not an ordinary player command and not a proposal-application UI. It is a diagnostics/read surface for the main GM/daemon.

## User Story

As the main GM, I can open worker diagnostics and see which proposals exist, whether they are readable, what worker/task produced them, whether they contain draft text/findings/file changes, and what apply/audit state is known.

## Requirements

- **FR-001**: The system MUST list proposals stored under `worker_proposals/<proposalId>/proposal.json`.
- **FR-002**: Proposal listing MUST be deterministic and stable.
- **FR-003**: Malformed proposal files MUST appear as unreadable inbox entries instead of crashing diagnostics.
- **FR-004**: Proposal summaries MUST include proposal id, worker id, task id, status, summary, created time, changed-file count, finding count, and whether draft text exists.
- **FR-005**: Proposal details MUST expose changed files, findings, draft text, and self-check notes for GM review.
- **FR-006**: Proposal details SHOULD include apply/audit outcome when `gm_worker_audit.jsonl` contains related events.
- **FR-007**: Proposal-only narrative/analysis entries MUST be marked review-only; this feature MUST NOT auto-apply them.
- **FR-008**: Existing player-facing console output MUST remain unchanged unless advanced diagnostics are explicitly opened.
- **FR-009**: Tests MUST cover normal listing, malformed proposals, and diagnostics rendering/serialization.

## Out of Scope

- Manual proposal approval UI.
- Applying proposal-only narrative or analysis tasks.
- Browser UI.
- Remote/cloud orchestration.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmWorker|GmBridgeDiagnostics"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```
