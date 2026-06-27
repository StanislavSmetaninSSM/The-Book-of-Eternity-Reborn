# Quickstart: RLM-Inspired GM Harness

## Prerequisites

- Working repository checkout.
- Existing local game session data.
- GitHub issue links #1281-#1283 and #1285-#1290 available for traceability.
- Codex CLI available for live GM bridge tests when running the manual scenario.

## Focused automated verification

Run daemon/bridge contract tests:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmTurnHelperContractTests|GmBridgeDiagnosticsContractTests"
```

Run GM documentation coverage when prompts/docs/examples change:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

## Manual live-test scenario

1. Start a short live GM bridge run using the normal hidden GM bridge command with:

```text
codex --dangerously-bypass-approvals-and-sandbox
```

2. Prepare each live-test player turn with the repository-owned helper instead of hand-writing JSON:

```powershell
.\BookOfEternityClient\Launcher\bookofeternity.ps1 -SessionPath "C:\Temp\boe-live-test\game_session" prepare-turn --action "Надеть руническую перчатку и изучить письмо." --session-id live-session --request-id live-request-001 --turn-number 1 --dice "14,8,17"
```

The helper writes `input/turn_request.json`, `game_state/control/pending_turn_snapshot.json`, and `game_state/control/pending_turn_snapshot.authority.json`.
It normalizes snapshot paths and excludes generated harness files such as bridge/daemon status, `gm_context_pack`, repair requests, prior pending snapshots, and the trajectory ledger.

3. Run one ordinary turn or repair-sensitive turn selected by current priority.
4. Confirm the context pack includes compact templates and, after the relevant implementation slice, compact experience lessons.
5. Confirm a GM trajectory ledger record is written.
6. If a validation repair happens, confirm the ledger records issue kind, repair packet, attempt count, and final status.
7. If a worker is used, confirm dispatch/proposal/apply decision appears in the ledger.
8. For afterlife turns, confirm `Complete-BoeTurn` / `Complete-BoeValidationRepair` block raw Mortal World profile mutations before terminal completion.
9. For Chaos Sea spiritual conflict turns, confirm the compact `AFTERLIFE_TEMPO_ADVANTAGE_TEMPLATE.json` prevents missing `advantageId` / `sourceId` repairs when a guard-created tempo window is used.
10. Apply the live-test rubric:
   - valid turn
   - player-facing output present
   - no implementation-source browsing as ordinary workflow
   - repair count
   - duration
   - missing harness tool moments
   - worker usefulness
   - experience lesson usefulness
11. Convert repeated high-friction findings into GitHub issues or comments.

## Expected result

The live test should produce not only a pass/fail result, but a compact harness-quality report showing what the GM could do unaided, what the harness prevented, and what tool/template/validator/worker improvement should come next.
