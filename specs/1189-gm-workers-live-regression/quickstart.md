# Quickstart: GM Workers Live Regression

## Goal

Run a disposable console playtest where the main Codex GM can use hidden Codex workers for narrative/analysis and validation repair, then record proposal inbox and audit evidence.

## Prerequisites

- Codex CLI is available as `codex --dangerously-bypass-approvals-and-sandbox` for the interactive main GM bridge.
- Codex CLI is available as `codex exec --dangerously-bypass-approvals-and-sandbox --skip-git-repo-check -` for hidden non-interactive workers.
- The repository is built from the active #1189 branch.
- No user `BookOfEternityClient/game_session` files are used as mutable live-test state; copy a fixture session into a temp run root.
- Browser and QTE are disabled for this run.

## Focused Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "GmWorker|GmBridge|AgentConsole"
```

## Live Run Outline

1. Create a temp run root outside the repo, for example `%TEMP%\boe-gm-workers-live-YYYYMMDD-HHMMSS`.
2. Copy `FileSystemExample/game_session` to the run root.
3. Write local settings that enable Codex worker profiles for `NarrativeDraft`, `Analysis`, and `ValidationRepair`.
4. Start the GM daemon and console client with Agent Console enabled.
5. Start the main GM bridge with `codex --dangerously-bypass-approvals-and-sandbox`.
6. Send one normal player action that invites narrative/analysis delegation.
7. Trigger or rehearse one validation-repair delegation.
8. Inspect:
   - `game_state/control/gm_worker_audit.jsonl`
   - `worker_tasks/`
   - `worker_proposals/`
   - Agent Console snapshots/events
   - GM bridge diagnostics
9. Record whether subordinate workers stayed hidden/background from the player perspective.
10. Stop client, daemon, bridge, and any worker processes from run metadata.

## Expected Outcome

- At least one proposal-only narrative/analysis worker proposal is present.
- At least one validation-repair proposal or precise validation-repair worker failure is present.
- The main GM remains the final authority.
- The final report classifies readiness for regular live E2E use.
