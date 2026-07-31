# Quickstart: C# Verification Lanes

**Source issue**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

Run commands from the repository root.

## Build

```powershell
dotnet restore BookOfEternityClient\BookOfEternityClient.sln
dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore
```

## Ordinary Local Verification

```powershell
.\scripts\test-csharp.ps1 -Lane Fast
```

Target: at most five minutes. The script uses a seven-minute timeout and excludes
`FullValidation`, `ProcessIntegration`, and `E2E`.

## Focused Validation Work

```powershell
.\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
```

Guardian benchmark:

```powershell
.\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReason|FullyQualifiedName~GuardianProjectValidation_CompleteOffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReasonWhenActiveProjectLacksIt" `
  -TimeoutMinutes 2
```

Run the benchmark three times against the same build and compare median
runner-reported duration. The pre-change pair is approximately 20 seconds; the
accepted post-change result is at most 4 seconds.

## Explicit Slow Lanes

```powershell
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
```

## Final Complete Control

```powershell
.\scripts\test-csharp.ps1 -Lane Complete -TimeoutMinutes 20
```

The complete control is run once at the final gate, not during ordinary
iteration. It must finish within 15 minutes on the baseline machine; the extra
five minutes is a hard cleanup bound, not the performance target.

## Results and Cleanup

Each run writes `test-results.trx` and `dotnet-test.log` below
`TestResults/test-lanes/<timestamp>-<lane>/`. On timeout the runner terminates
only the `dotnet` process tree it started. A timed-out or failed lane returns a
non-zero exit code and is not treated as passing evidence.
