# Quickstart: C# Verification Lanes

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526)

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
projects with non-overlapping filters and one 20-minute deadline covering
frontend verification, builds, discovery, tests, and cleanup. It excludes the
complete lifecycle class while retaining exactly ten reviewed
`PreMergeSentinel` lifecycle methods. It also excludes the exhaustive
358-case `AfterlifeSpiritualConflictValidationTests` matrix while retaining
exactly ten reviewed sentinels from that class.

Within that parallel phase, retained PreMerge class costs keep slow,
small-case integration classes from being packed at the tail. Each file-backed
browser-presentation test host prepares each selected save once, then runs every
browser and console row against an isolated clone. This keeps the same
four-host/two-Fast ceilings and does not change selection or assertions.
Each ExplorerWeb test host also prepares one empty canonical skeleton, clones
it for every row, and reuses immutable, hashed seed profiles for repeated
deterministic theory setup. Every row retains its own writable root.
The four ExplorerWeb shards form the first resource-isolation wave and all four
must finish before the remaining parallel descriptors start. The second wave
then uses the ordinary scheduler. Both waves remain inside the existing
parallel phase and keep the same four-host/two-Fast ceilings.

The full spiritual-conflict matrix is not deleted or weakened. Run
`RegressionIntegration` when changing that validator/normalizer boundary,
diagnosing a related failure, or when an exhaustive control is explicitly
requested. Unrelated ordinary work and final merges use the sentinel sample in
PreMerge.

## Recommended Workflow

Run the smallest relevant Focused control during implementation and one Fast
control at a meaningful checkpoint. Immediately before merge, run one PreMerge
control. Do not repeat Fast immediately before PreMerge or serially run all
diagnostic lanes unless a focused failure requires diagnosis.
LifecycleIntegration and DeepValidation are conditional and explicit; use them
for changes to those boundaries, related diagnosis, or an explicitly requested
exhaustive control.

```powershell
# During implementation
.\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ValidationPhaseSelectionTests"

# Meaningful checkpoint
.\scripts\test-csharp.ps1

# Immediately before merge
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

## Historical #1505 Evidence

| Control | Result | Tests | Runner wall | Result directory |
|---|---|---:|---:|---|
| Fast 1 | `PASS` | `2587/2587` | `2:59.057` | `20260801-195606-147-20340-c486827ab39b4cdf914e0c72bc8fde60-fast` |
| Fast 2 | `PASS` | `2587/2587` | `2:28.905` | `20260801-195915-638-5272-2d2fa823c35b48f08be4368f1a96dd16-fast` |
| LifecycleIntegration | `PASS` | `186/186` | `5:31.972` | `20260801-181656-093-3652-2665d79ca44447b685df3a20ddee9ca9-lifecycleintegration` |
| DeepValidation (retained) | `PASS` | `2142/2142` | `14:15.857` | `20260801-125643-609-35532-e202ce76a0004beda7e59ab8c0fe72f8-deepvalidation` |
| PreMerge | `PASS` | `4522/4522` | `12:12.687` | `20260801-200153-781-36812-b84a1ae9818741b9a67590fa9b40711e-premerge` |

Each run writes `.trx`, `dotnet-test.log`, and `summary.json` files below its
unique `TestResults/test-lanes/` result directory. A failed descriptor, timeout,
TRX parse error, duplicate composed-lane test ID, or incomplete
exact-owned-tree cleanup returns non-zero. DeepValidation requires at least
1,950 results; LifecycleIntegration requires at least 186; PreMerge requires
at least 4,240 results plus completed ProcessIntegration and E2E phases.
Every accepted control reported exit `0`, no failures or duplicate IDs, no
timeout, complete cleanup, and zero remaining owned processes. PreMerge met
the mandatory 15-minute ceiling but not the preferred below-ten-minute target.

The Phase 45 amendment's current PlanOnly contract contains 19
non-overlapping descriptors and 4,262 estimated cases, including exactly ten
spiritual-conflict sentinels; Theory rows make merged TRX totals
authoritative. Its exact clean-checkout executable result is retained in the
#1502 PR/issue evidence before merge.

The #1526 PlanOnly contract contains 22 unique descriptors and 4,825 estimated
cases. The two browser-presentation descriptors retain all 166 parameterized
rows and use the measured prepared-template cost. Parameterized rows mean the
merged TRX total remains the authoritative coverage count. The first four plan
rows are the four ExplorerWeb shards; the executable wave guard proves they
drain before Fast begins in the second wave.

The accepted #1526 exact PreMerge control passed `4,836/4,836` results in
`14:18.302` under the 20-minute deadline. ProcessIntegration passed `490/490`,
E2E passed `15/15`, duplicate IDs were zero, and owned-tree cleanup completed.
The result directory is
`20260813-044749-940-43368-f64c53dc7a7e48f5a30055b05c1b7e95-premerge`.

The rejected historical all-inclusive attempt ended at `15:00.393` with exit
`124`: all `4,738/4,738` completed tests passed, failures and duplicates were
`0`, cleanup succeeded, and the projected lower bound was `25:37.741`. This was
a capacity limit, not a correctness failure, and motivated the two-tier design.
