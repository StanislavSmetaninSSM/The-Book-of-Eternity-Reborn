# Quickstart: C# Verification Lanes

**Source issue**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

Run commands from the repository root with PowerShell 7.

## Build

```powershell
dotnet restore BookOfEternityClient\BookOfEternityClient.sln
dotnet build BookOfEternityClient\BookOfEternityClient.sln --no-restore --verbosity minimal
dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --verbosity minimal
dotnet build BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj --no-restore --verbosity minimal
```

## Commands

```powershell
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"
.\scripts\test-csharp.ps1 -Lane FullValidation
.\scripts\test-csharp.ps1 -Lane RegressionIntegration
.\scripts\test-csharp.ps1 -Lane ProcessIntegration
.\scripts\test-csharp.ps1 -Lane E2E
.\scripts\test-csharp.ps1 -Lane LifecycleIntegration
.\scripts\test-csharp.ps1 -Lane DeepValidation
.\scripts\test-csharp.ps1 -Lane PreMerge
```

The default/Fast lane selects the fast test project directly and has no
category-exclusion filter. Its one hard limit is five minutes.

The explicit diagnostic lanes select categories in the integration test
project. They are not ordinary post-edit controls. Use them only for a relevant
change or to narrow a bounded failure. DeepValidation selects the
Integration-only union of FullValidation and DeepValidation, excluding
LifecycleIntegration, ProcessIntegration, and E2E. LifecycleIntegration runs
the complete 186-case GameEngine lifecycle class in one external test process
under a ten-minute cap.

`Complete` is a temporary alias for `PreMerge`. PreMerge verifies both test
projects with non-overlapping filters and one 15-minute deadline covering
frontend verification, builds, discovery, tests, and cleanup. It excludes the
complete lifecycle class while retaining exactly ten reviewed
`PreMergeSentinel` lifecycle methods.

## Recommended Workflow

Run the smallest relevant Focused control during implementation and one Fast
control at a meaningful checkpoint. At final verification, run two consecutive
Fast controls and one PreMerge control. Do not serially run all diagnostic
lanes before PreMerge unless a focused failure requires diagnosis.
LifecycleIntegration and DeepValidation are conditional and explicit; use them
for changes to those boundaries, related diagnosis, or an explicitly requested
exhaustive control.

```powershell
# During implementation
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"

# Final verification
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1
.\scripts\test-csharp.ps1 -Lane PreMerge
```

If PreMerge is green, do not follow it with serial FullValidation,
RegressionIntegration, ProcessIntegration, E2E, LifecycleIntegration, and
DeepValidation runs.

## Guardian Benchmark

```powershell
.\scripts\test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReason|FullyQualifiedName~GuardianProjectValidation_CompleteOffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReasonWhenActiveProjectLacksIt" `
  -TimeoutMinutes 2 `
  -NoBuild
```

The accepted post-change median runner-reported test duration is about three
seconds, at least 6.7 times faster than the approximately 20-second baseline.

## Fresh Final Evidence

| Control | Result | Tests | Runner wall | Result directory |
|---|---|---:|---:|---|
| Fast 1 | `PASS` | `2585/2585` | `4:21.152` | `20260801-180837-789-31004-7619904f0c9e49ba8d1716bec5a682f2-fast` |
| Fast 2 | `PASS` | `2585/2585` | `3:16.237` | `20260801-181321-123-18724-6adc52d358924335aaaaadf784f6ce9e-fast` |
| LifecycleIntegration | `PASS` | `186/186` | `5:31.972` | `20260801-181656-093-3652-2665d79ca44447b685df3a20ddee9ca9-lifecycleintegration` |
| DeepValidation (retained) | `PASS` | `2142/2142` | `14:15.857` | `20260801-125643-609-35532-e202ce76a0004beda7e59ab8c0fe72f8-deepvalidation` |
| PreMerge | `PASS` | `4518/4518` | `14:16.500` | `20260801-182302-896-28696-51cbcbdce4604dd48780a69819761273-premerge` |

Each run writes `.trx`, `dotnet-test.log`, and `summary.json` files below its
unique `TestResults/test-lanes/` result directory. A failed descriptor, timeout,
TRX parse error, duplicate composed-lane test ID, or incomplete
exact-owned-tree cleanup returns non-zero. DeepValidation requires at least
1,950 results; LifecycleIntegration requires at least 186; PreMerge requires
at least 4,490 results plus completed ProcessIntegration and E2E phases.
Every accepted control reported exit `0`, no failures or duplicate IDs, no
timeout, complete cleanup, and zero remaining owned processes. PreMerge met
the mandatory 15-minute ceiling but not the preferred below-ten-minute target.

The rejected historical all-inclusive attempt ended at `15:00.393` with exit
`124`: all `4,738/4,738` completed tests passed, failures and duplicates were
`0`, cleanup succeeded, and the projected lower bound was `25:37.741`. This was
a capacity limit, not a correctness failure, and motivated the two-tier design.
