# Quickstart: Selected Test Categories

Current policy: CATEGORY-SELECTION-DECISION rev1, approved 2026-09-30.
Read [docs/testing.md](../../docs/testing.md), choose categories by changed
contracts/consumers and update `tests/selection.json` with reasons.

```powershell
./scripts/test-csharp.ps1 -ListCategories
./scripts/test-csharp.ps1 -Category test-selection-contracts -PlanOnly
./scripts/test-csharp.ps1 -SelectionFile tests/selection.json
./scripts/test-csharp.ps1 -ValidateCatalog
```

The last command audits ownership by discovery without executing tests.
Category names are extensible catalog data. Full-suite and serial-all controls
are prohibited everywhere. Use independent mutable fixtures; immutable copied
templates remain conditional, not mandatory. See current spec/plan/tasks for
scope and evidence. No gameplay continuation until owner inspection.

## Historical lane quickstart (superseded; do not execute these commands)

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

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
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~QteSceneServiceTests"
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

Fast contains fixture-free deterministic unit, parser, reducer, contract, and
source-guard coverage. Canonical file/restart/rollback/lifecycle and
lease-contention tests live in Integration with `RegressionIntegration`; real
child-process tests use `ProcessIntegration`; complete host/browser flows use
`E2E`. For a mixed class, split deterministic tests from the integration
fixture rather than adding a negative Fast filter. Use
`-Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~..."`
to iterate on a moved class.

Treatment resolver tests requiring accepted-state files, persistence, leases,
resource claims, publication, restart, or replay are `RegressionIntegration`.
Move the entire `MortalWoundTreatmentResolverTests*.cs` partial family with its
registry companion; fixture-free treatment parsers, projectors, fingerprints,
reducers, and pure policy tests remain Fast. This does not raise Fast's
five-minute limit or reduce assertions.

The explicit diagnostic lanes select categories in the integration test
project. They are not ordinary post-edit controls. Use them only for a relevant
change or to narrow a bounded failure. DeepValidation selects the
Integration-only union of FullValidation and DeepValidation, excluding
LifecycleIntegration, ProcessIntegration, and E2E. LifecycleIntegration runs
the complete 186-case GameEngine lifecycle class in one external test process
under a ten-minute cap.

`Complete` is a temporary alias for `PreMerge`. PreMerge verifies both test
projects with non-overlapping filters and one 30-minute deadline covering
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

The #1547 capacity-red control later completed `6,608/6,608` available core
results green but reached the 20-minute deadline before the exclusive tail
completed. Isolated ProcessIntegration passed `523/523` in `3:32`; together
with the approximately 17-minute core and retained E2E tail, this proves the
old cap is obsolete. The current 30-minute bound changes no selection,
assertion, phase, ordering, or concurrency rule.

The accepted #1547 30-minute control is
`TestResults/test-lanes/20260826-004148-035-3528-42f32409a6b34cf4bbb7950b7e8a10d7-premerge/summary.json`.
It passed all `7,131/7,131` C# results in `00:21:59.6684306`: Fast
`4,339/4,339`, core integration `2,269/2,269`, ProcessIntegration `508/508`,
and E2E `15/15`. All `26` TRX files completed; frontend verification separately
passed `141/141`, both C# builds had zero warnings/errors, exit was `0`, timeout
was `false`, duplicate IDs were zero, and owned-tree cleanup completed. No
selection, assertion, phase, ordering, or concurrency contract changed.

The rejected historical all-inclusive attempt ended at `15:00.393` with exit
`124`: all `4,738/4,738` completed tests passed, failures and duplicates were
`0`, cleanup succeeded, and the projected lower bound was `25:37.741`. This was
a capacity limit, not a correctness failure, and motivated the two-tier design.

## Current #1551 Boundary and Evidence

Fast is the complete physically isolated `BookOfEternityClient.Tests` project,
not shorthand for all repository tests. It retains no negative category filter,
the five-minute hard limit, and the two-host ceiling. Use this exact placement
rule:

```text
canonical file/restart/rollback/lifecycle/contention -> RegressionIntegration
real child process -> ProcessIntegration
complete HTTP host/browser flow -> E2E
fixture-free deterministic unit/parser/reducer/contract/source guard -> Fast
```

For moved classes, iterate in the Integration project:

```powershell
.\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ClassOrMethod"
```

The QTE split keeps 66 deterministic input/grading rows in Fast
`QteDeterministicLogicTests` and 51 canonical lifecycle rows in Integration
`QteSceneServiceTests`. The Daren split keeps 77 deterministic
route/prose/reducer/contract rows in Fast `DarenQteDeterministicLogicTests` and
12 profile/filesystem/service/browser-projection rows in Integration
`DarenQteShowcaseTests`. Executable Roslyn manifests pin each Fact/Theory and
normalized InlineData row at QTE `66/51` and Daren `77/12`; category ownership
comes only from attributes on the expected top-level class. Moved WebUi classes
use the test-free `UiTestTextCollector` helper from TestSupport so Integration
never references Fast. The exact final 58-entry reviewed-heavy manifest and
35-entry class-level regression category array are recorded in `research.md`
directly from the executable guards. Method-only Regression traits do not count
as class ownership.

Post-remediation PlanOnly artifact
`20260904-070836-796-19044-f3c6b8f4a7364bb1913611176107307a-fast`
contains 29 unique Fast-project descriptors and 7,071 estimated cases/cost,
with the 77-row Daren deterministic owner exactly once, no Integration Daren
owner, and zero duplicates. The current Fast controls
are:

| Run ID | Executed / passed / failed | Wall | Exit | Timeout / duplicates / cleanup |
|---|---:|---:|---:|---|
| `20260904-070944-389-28440-60e246e1148944bb86b2b8bd2d582246-fast` | `4759/4694/65` | `00:02:17.7762792` | 1 | `false/0/complete` |
| `20260904-071206-972-37312-3696b2f3daf248da93c08092f04e572b-fast` | `4759/4694/65` | `00:02:13.7896671` | 1 | `false/0/complete` |

Both satisfy the Fast capacity, duplicate, and cleanup requirements, but both
remain official RED and incomplete. All 65 failures exactly match the retained
#1536 T067 display names and stable TRX IDs; fail-fast completed five of 29
descriptors, leaving 24 without complete TRX evidence. The measured contour
now contains 77 additional passing rows, exactly the Daren deterministic rows
restored to Fast, while the known failure set remains unchanged.

Task 7's required diagnostics retained a `151/151/131/20` unrelated Guardian
RED in RegressionIntegration and an initial `555/555/552/3` ProcessIntegration
RED. The first E2E attempt could not start because ignored frontend dependencies
were absent after restart. After `npm ci`, the environment-valid E2E artifact
`20260904-061601-948-15524-cd780f2129b04dc782dc4e6122cd00cc-e2e`
passed frontend `141/141` and E2E `18/18` in `00:01:28.5961264`; the exact two
built-frontend ProcessIntegration smoke methods then passed `2/2` in
`20260904-061737-528-23048-28391b77381c455c95ebccb5d5de8fff-focused`.
The third ProcessIntegration failure, a QTE helper method, passed within the
exact failed-method diagnostic and remains intermittent. The 20 Guardian rows
reproduced exactly and remain unrelated pre-existing/current-branch functional
RED, not #1551 work.

#1551 changes only test placement and scheduling. It changes no production game
capability, command, mechanic, state/validation/normalizer contract,
player-visible UI, GM-authored output, afterlife pending/control/action/receipt
or report surface, or daemon/launcher prompt. Mortal World/afterlife prompts,
gameplay examples, validation manifests, afterlife contract matrix, and source
guards require no update.
