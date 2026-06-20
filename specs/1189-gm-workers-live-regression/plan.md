# Implementation Plan: GM Workers Live Regression

**Branch**: `work/1189-gm-workers-live-regression` | **Date**: 2026-06-21 | **Spec**: `specs/1189-gm-workers-live-regression/spec.md`

## Summary

Run and document an integrated console live regression for GM Workers. The work should prove hidden Codex worker launch for narrative/analysis delegation and validation-repair delegation, record proposal inbox/audit evidence, and produce a readiness assessment for regular live E2E use. Runtime code changes are out of scope unless a focused blocker is found and can be safely fixed under #1189.

## Technical Context

- **Language/runtime**: C# / .NET 8, PowerShell launcher scripts, Codex CLI, file-backed JSON game state.
- **Existing contracts**:
  - `specs/1113-gm-worker-bridges/`
  - `specs/1143-live-worker-validation-repair/`
  - `specs/1145-gm-worker-proposal-inbox/`
  - `specs/1147-proposal-only-worker-dispatch/`
  - `specs/1149-gm-worker-cli-runner/`
  - `specs/1151-gm-worker-profile-templates/`
  - `OtherGuides/GM_Worker_Bridges.md`
- **Primary touched files expected**:
  - `docs/e2e/gm-workers-live-regression-runbook.md`
  - `docs/audits/gm-workers-live-regression-1189.md`
  - `specs/1189-gm-workers-live-regression/*`
- **Out of scope**: browser, QTE, Gemini CLI, non-Codex workers, broad worker architecture changes.

## Constitution Check

- **GitHub Issue Traceability**: Pass. Source issue #1189 is linked in spec, plan, and tasks.
- **Player-Facing Integrity**: Pass. Worker diagnostics are GM/maintainer-facing; player must see only the main console flow.
- **Contract and State Authority**: Pass. Workers remain proposal-only and main GM/apply gate owns final decisions.
- **Test-First Verification**: Pass. If code changes become necessary, add RED/GREEN tests first. For pure live-run/report work, verification is runbook execution plus focused tests.
- **Agent Orchestration Discipline**: Pass. Spec Kit records the E2E plan; Superpowers controls debugging, review, and verification.

## Implementation Phases

1. Create Spec Kit artifacts and mark #1189 in progress.
2. Read existing worker contracts, docs, launcher scripts, and focused worker tests.
3. Create or update a reproducible live-test runbook for #1189.
4. Prepare a disposable game session and worker-enabled settings.
5. Run baseline focused tests for GM worker/bridge/Agent Console surfaces.
6. Launch console client, Agent Console, GM daemon, main Codex GM bridge, and hidden Codex worker profiles.
7. Produce narrative/analysis delegation evidence.
8. Produce validation-repair delegation evidence, using a controlled repair rehearsal if needed.
9. Inspect proposal inbox, worker audit, process visibility, and player-facing snapshots.
10. File focused blocker/major bugs if needed.
11. Record the final report and readiness score.
12. Verify, comment on #1189, PR, merge, and close when evidence is complete.

## Verification Commands

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"
```

If runtime/contract docs change beyond runbook/report:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|GmWorkerBridgeDocumentationTests"
```

Final broad non-browser check if code changes:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests"
```

## Deliverables

- `docs/e2e/gm-workers-live-regression-runbook.md`
- `docs/audits/gm-workers-live-regression-1189.md`
- Issue #1189 comment with run root, verification, readiness assessment, and follow-up links.
- Optional focused bug issues for blocker/major findings.
