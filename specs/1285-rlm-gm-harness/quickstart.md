# Quickstart: RLM-Inspired GM Harness

## Prerequisites

- Working repository checkout.
- Existing local game session data.
- GitHub issue links #1285-#1290 available for traceability.
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

2. Run one ordinary turn or repair-sensitive turn selected by current priority.
3. Confirm the context pack includes compact templates and, after the relevant implementation slice, compact experience lessons.
4. Confirm a GM trajectory ledger record is written.
5. If a validation repair happens, confirm the ledger records issue kind, repair packet, attempt count, and final status.
6. If a worker is used, confirm dispatch/proposal/apply decision appears in the ledger.
7. Apply the live-test rubric:
   - valid turn
   - player-facing output present
   - no implementation-source browsing as ordinary workflow
   - repair count
   - duration
   - missing harness tool moments
   - worker usefulness
   - experience lesson usefulness
8. Convert repeated high-friction findings into GitHub issues or comments.

## Expected result

The live test should produce not only a pass/fail result, but a compact harness-quality report showing what the GM could do unaided, what the harness prevented, and what tool/template/validator/worker improvement should come next.
