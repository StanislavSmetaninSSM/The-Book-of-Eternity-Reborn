# Spec: GM Compact Turn And Repair Templates

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1280

## Goal

Reduce live GM turn latency and repair mistakes by giving the GM small
session-local executable templates for common turn, repair, progression,
actor-reasoning, and afterlife tempo-advantage shapes.

## Requirements

- Generate compact templates in `game_state/control/gm_context_pack/Templates`.
- Include templates for ordinary turn output, validation repair,
  `progression_report.json`, actor reasoning blocks, and afterlife spiritual
  conflict `tempoAdvantage`.
- List the templates in `context_pack_manifest.json` with explicit roles.
- Update the generated context-pack README to route the GM to templates before
  large copied examples.
- Turn prompts must require compact templates before opening large examples.
- Repair prompts must require the compact repair template and prefer
  `validation_repair_request.json.harnessRepairPackets[]`.
- Large examples remain available for route-specific afterlife contracts, but
  they are not the first source for basic field names.

## Non-Goals

- Do not change accepted JSON schemas or validation semantics.
- Do not remove full GM guides or examples from the context pack.
- Do not automate full turn authoring; this is a harness assist layer only.

## Verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~GmTurnHelperContractTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName~GmBridgeDiagnosticsContractTests|FullyQualifiedName~GmTurnHelperContractTests|FullyQualifiedName~ConsoleE2ESandboxTests|FullyQualifiedName~CleanupAcceptedTurnTerminalArtifactsAsync|FullyQualifiedName~ValidateGameState_EmptyGuardianProjectTrackerWithoutPreTurnBaselineDoesNotPoisonIdleValidation|FullyQualifiedName~GuardianProcessGachaValidation_FailsClosedOnCurrentTrackerAuthorityBeforeForgeExceeded|FullyQualifiedName~ValidateGameState_CurrentTemporaryProjectModifiersCannotMaterializeOutsideKernelAuthority|FullyQualifiedName~GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReason|ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
dotnet build BookOfEternityGMBridge\BookOfEternityGMBridge.csproj --no-restore
```

Manual/live:

- Start a Chaos Sea session with hidden bridge.
- Confirm generated `gm_context_pack/Templates/*` exists.
- Dispatch one afterlife turn and inspect bridge diagnostics for compact
  template reads before huge example reads.
- Record turn duration and whether validation repair was needed.
