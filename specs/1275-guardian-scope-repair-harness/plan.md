# Implementation Plan: Guardian Scope Repair Harness

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1275

## Approach

Add a narrow repair-request enrichment layer in `GameEngine` instead of changing validator semantics. The validator remains strict; the repair request becomes more useful for the GM by grouping related Guardian scope and materialized mirror errors into one actionable packet.

The live test also exposed a harness-level dispatch risk: the bridge can be marked `Ready` while the hosted Codex CLI is still working on an older request. The bridge must block prompt dispatch in that state, because otherwise the player turn can be pasted into an active CLI screen instead of being handled as the next GM task.

## Implementation Steps

1. Add a failing test in `GameEngineTurnLifecycleTests` that invokes `WriteValidationRepairRequestAsync` with representative Guardian scope and mirror errors and asserts a concrete `harnessRepairPackets[]` entry.
2. Extend the internal `ValidationRepairRequest` payload with `HarnessRepairPackets`.
3. Build a guardian-specific packet when prioritized errors contain Guardian scope/mirror codes.
4. Update `TaskGuides/CLI_Step_Main.txt` and `OtherGuides/Afterlife_Contract_Matrix.md` to explain how the GM should use the packet.
5. Add bridge dispatch readiness probing that refuses to paste a prompt while Codex CLI is visibly still working or waiting for workspace trust.
6. Run focused tests, documentation coverage tests, and a live Chaos Sea bridge test.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~GameEngineTurnLifecycleTests.WriteValidationRepairRequestAsync_GuardianScopeErrors_AddsConcreteHarnessRepairPacket"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~GmBridgeDiagnosticsContractTests.BridgeHost_RefusesPromptDispatchWhileCodexCliIsWorking"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

Then run a live Chaos Sea turn through `codex --dangerously-bypass-approvals-and-sandbox` bridge and inspect repair attempts.
