# Research: Test Suite Performance and Verification Lanes

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526)

## Baseline Findings

| Evidence | Result |
|---|---:|
| Discovered C# cases | 6,560 |
| Test classes | 228 |
| C# test source files | 273 |
| Declared Fact/Theory methods | 5,174 |
| Broad `ValidateGameStateAsync()` calls | 965 |
| Validation phases per broad call | 26 |
| Guardian cases / declared methods | 460 / 436 |
| Broad guardian calls | 295 |
| Targeted guardian sample | about 1 second |
| Broad guardian sample | about 10 seconds |
| Two broad guardian samples | about 20 seconds |
| Estimated avoidable guardian cost | about 44 minutes |
| Sampled Agent Console live smoke | about 3 seconds |

Release and Debug both took about 20 seconds for the same two broad guardian
tests. The build configuration is not the cause.

## Post-Change Evidence

| Evidence | Result |
|---|---:|
| Direct broad guardian calls | 0 |
| Fixed benchmark, three runs | about 3 seconds each |
| Fixed benchmark median speedup | at least 6.7x |
| Fast external concurrency | 2 test hosts |
| Reviewed broad-validation manifest | 8 direct call sites (budget: 8) |
| Fast control 1 | `2587/2587`, `2:59.057`, PASS |
| Fast control 2 | `2587/2587`, `2:28.905`, PASS |
| LifecycleIntegration control | `186/186`, `5:31.972`, PASS |
| DeepValidation control (retained) | `2142/2142`, `14:15.857`, PASS |
| PreMerge control | `4522/4522`, `12:12.687`, PASS |

The benchmark wall times were 7.92, 7.32, and 7.64 seconds; runner-reported
test duration rounded to 3 seconds in all three runs. Attempts with four and six
external test hosts increased file-backed test durations and produced a false
three-second lock timeout. The same lock test passed three isolated controls in
0.6–0.7 seconds. The accepted Fast schedule therefore caps external
parallelism at two.

## Fixed Benchmark

The before/after micro-benchmark uses these two methods from
`GuardianSystemRegressionTests.ProjectsPower.cs`:

- `GuardianProjectValidation_OffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReason`
- `GuardianProjectValidation_CompleteOffensiveIntrigueAgainstTrustedTarget_RequiresBetrayalReasonWhenActiveProjectLacksIt`

The comparison uses the same built assembly, three bounded runs, runner-reported
test duration, and median duration. Wall time is retained separately because
`dotnet test` startup contributes several seconds.

## Decision 1: Internal Flags-Based Phase Selection

**Decision**: Add an internal `[Flags]` enum with one bit for each existing phase
and an `All` mask. Add an internal overload used by tests. The public no-argument
method always delegates with `All`.

**Rationale**:

- 26 phases fit safely in a 32-bit mask.
- Combined selections naturally deduplicate phases.
- Explicit conditionals preserve the existing source order.
- `InternalsVisibleTo` already exposes runtime internals to the test assembly.
- No runtime caller can accidentally request partial validation through the
  public API.

**Rejected alternatives**:

- Three coarse phase groups: at most about 3x theoretical improvement and too
  much unrelated work for rule-focused guardian tests.
- Public optional parameters: expands the production contract and makes partial
  validation available to runtime callers.
- Separate test validator: risks rule duplication and divergence.
- Reflection into private methods: brittle and bypasses orchestration invariants.

## Decision 2: Explicit Fail-Closed Mask Validation

**Decision**: Reject `None` and any bit outside `All` before caches are reset or
phase work begins.

**Rationale**: An empty or undefined selection could make a regression test pass
without exercising validation. Failing before side effects also keeps repeated
test runs predictable.

## Decision 3: Test-Side Guardian Profiles

**Decision**: Store reviewed combinations in
`BookOfEternityClient.Tests/GuardianValidationProfiles.cs`; production code owns
only individual phase names and canonical ordering.

Initial profiles are derived from issue-code ownership:

| Guardian domain file | Candidate phases |
|---|---|
| `IdleValidation` | Cross references; meta/misc state |
| `LifecycleSnapshots` | Mortal bootstrap anchors; world/quest state; meta/misc state; life-evaluation cycle; trigger-turn reward exclusion; client-owned control; realm segregation |
| `AcceptedAuthority` | Cross references; meta/misc state; accepted-turn actor materialization; guardian resonance/power events; client-owned control |
| `PowerJournalOfferings` | Required fields; meta/misc state; guardian resonance/power events |
| `ProjectsPower` | Required fields; cross references; world/quest state; meta/misc state; guardian resonance/power events; client-owned control |
| `QuestProgress` | Required fields; world/quest state; meta/misc state; realm segregation |
| `RivalResidents` | NPC state; world/quest state; meta/misc state; client-owned control |
| `TradeOfferingResonance` | Meta/misc state; guardian resonance/power events; client-owned control |

A phase is added only when a focused failing assertion demonstrates that the
owning rule lies outside the candidate profile.

**Rejected alternatives**:

- One guardian-wide profile: easier migration but hides domain ownership and
  leaves unnecessary work in every test.
- Caller-file inference inside production: makes behavior depend on source paths.
- Per-method maps for 436 tests: precise but costly to maintain and unnecessary
  unless file-domain profiles miss the performance gate.

## Decision 4: Discovery-Balanced Process Composition

**Decision**: Keep the shared partial Guardian fixture intact. The bounded
runner discovers tests, assigns every Guardian source method to one reviewed
domain, splits only large sequential classes by method, and retains one TRX per
non-overlapping chunk.

**Rationale**: Private helpers and fixture setup span partial files. Physical
class extraction would duplicate a large fixture surface and risk shared
mutable state. Discovery-balanced chunks provide independent execution without
changing test identities, assertions, or fixture ownership. Source checks fail
when a new partial Guardian source is not assigned.

## Decision 5: Trait-Based Lanes

**Decision**:

- The ordinary Fast boundary is physical: it selects the complete
  `BookOfEternityClient.Tests` project directly, with no category filter.
- `FullValidation`: tests/classes intentionally invoking the public full pipeline.
- `RegressionIntegration`: file-backed Guardian, Explorer, GameEngine,
  browser-command, and local-host workflow regressions.
- `RegressionIntegrationOnly`: an exhaustive matrix available through
  RegressionIntegration but excluded from routine PreMerge except for its
  exact reviewed sentinels.
- `ProcessIntegration`: tests/classes starting a real child process.
- `E2E`: end-to-end client or Agent Console workflows.
- `LifecycleIntegration`: the complete GameEngine turn-lifecycle class, run
  explicitly in one external test process.
- `PreMergeSentinel`: an exact reviewed method sample admitted to routine
  PreMerge without admitting its complete heavy class or matrix.
- `DeepValidation`: the exhaustive Guardian regression matrix; the explicit
  lane selects its union with FullValidation.
- All diagnostic categories live in
  `BookOfEternityClient.IntegrationTests`.
- `Complete` is a temporary alias for `PreMerge`, not a separate unbounded
  selection.

Class-level traits are acceptable when a file/class is predominantly a slow
integration group. Method-level traits are used for isolated sentinels. Traits
remain useful for diagnostic selection inside the integration project, but
Fast does not depend on category exclusions.

## Decision 6: Three-Project Test Topology

**Decision**: Use one non-test support library plus physically isolated fast
and integration test projects:

- `BookOfEternityClient.TestSupport` contains shared fixtures/helpers and has no
  xUnit or `Microsoft.NET.Test.Sdk` dependency.
- `BookOfEternityClient.Tests` references production and TestSupport and owns
  ordinary fast sources.
- `BookOfEternityClient.IntegrationTests` references production and TestSupport,
  never the fast project, and owns reviewed slow sources.

**Rationale**: A direct project boundary makes Fast discovery deterministic.
Category drift cannot silently pull an integration source back into ordinary
post-edit feedback. Boundary tests enforce references, package roles, partial
class ownership, and source classification.

## Decision 7: Bounded Local Runner

**Decision**: Add `scripts/test-csharp.ps1` as the stable local entry point.
It routes lanes to explicit project paths, writes a timestamped log,
`summary.json`, and one or more TRX files, enforces one global deadline, and
uses exact ownership containment for the process roots it started.

**Rationale**: Raw `dotnet test` has no suite-wide wall-clock timeout and an
agent/tool timeout does not reliably document or clean the owned child tree.

**Safety boundary**: The script never enumerates and kills processes by name.
On Windows every target starts behind a named-event gate. The launcher enters
a dedicated kill-on-close Job Object before the gate opens, so the target and
its inherited descendants remain observable and terminable after an
intermediate root exits. Direct and parallel-batch executable self-tests
establish the root-exited/child-live precondition and verify exact-PID
descendant cleanup before output streams are drained. The script records an
error unless both the launcher has exited and its containment is empty.

PreMerge uses one 20-minute deadline across frontend verification, both
test-project builds, discovery, tests, and cleanup. Its non-overlapping
schedule is:

1. the complete fast assembly plus integration tests filtered with
   `Category!=FullValidation&Category!=DeepValidation&Category!=ProcessIntegration&Category!=E2E&(Category!=LifecycleIntegration|Category=PreMergeSentinel)&(Category!=RegressionIntegrationOnly|Category=PreMergeSentinel)`,
   with at most four test hosts overall and at most two fast hosts;
2. `Category=ProcessIntegration&Category!=E2E` sequentially;
3. `Category=E2E` sequentially.

DeepValidation is the disjoint Integration-only union
`(Category=FullValidation|Category=DeepValidation)&Category!=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E`.
It has a 1,950-result floor. LifecycleIntegration selects
`Category=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E` as
one descriptor under a ten-minute cap and has a 186-result floor. PreMerge has
a 4,240-result floor. Exactly ten lifecycle methods and ten methods from the
358-case `AfterlifeSpiritualConflictValidationTests` matrix also carry
`PreMergeSentinel`; every other case in those exhaustive groups stays outside
routine PreMerge and remains explicitly selectable.

## Final Verification Decision

Run focused controls during implementation, one Fast control at a meaningful
checkpoint, and one final PreMerge control. Do not serially run all
diagnostic lanes before PreMerge unless a focused failure requires diagnosis.
LifecycleIntegration and DeepValidation are conditional and explicit. They run
only for a relevant boundary change, related diagnosis, or an explicitly
requested exhaustive control.

Final executable evidence accepted two Fast controls, one
LifecycleIntegration control, the retained DeepValidation control, and one
PreMerge control. After the final review tests were added, PlanOnly
independently reported PreMerge `22/4507`,
LifecycleIntegration `1/186`, and unchanged DeepValidation `23/1950`. The
retained DeepValidation executable result was not repeated because the
selection was unchanged and still contained no lifecycle test.

Every accepted control exited `0`, reported zero failures and duplicate IDs,
did not time out, completed exact-owned-tree cleanup, and left zero owned
processes. PreMerge included ProcessIntegration `440/440`, E2E `15/15`, and
exactly ten lifecycle sentinels. Its `12:12.687` runner time meets the mandatory
below-15-minute ceiling but not the preferred below-ten-minute target.

The rejected all-inclusive attempt is retained as historical capacity evidence:
`15:00.393`, exit `124`, `4,738/4,738` completed tests passed, failures `0`,
duplicates `0`, cleanup succeeded, projected lower bound `25:37.741`. It was
correctness-clean and capacity-invalid, motivating the approved two-tier
design.

Phase 45 later reached the same capacity boundary after adding regressions:
the exact clean-checkout attempt completed `4606/4606` available cases green
in `15:00.159`, but had only about eleven seconds left after
ProcessIntegration and could not complete the final 15 E2E cases. Replay of
the retained descriptor timings showed that applying the existing
RegressionIntegration duration costs to PreMerge saves about 72 seconds.
Keeping the exhaustive 358-case spiritual-conflict class in its diagnostic
lane and admitting ten representative sentinels saves about another 126
parallel slot-seconds. This changes selection, not coverage ownership: the
full matrix remains in RegressionIntegration and no assertion is removed.

## Decision 8: Retain Measured Costs and Prepare Browser-Audit Saves Once

**Evidence**: Issue #1526 retained a PreMerge timeout at `15:00.187` after all
`4,326/4,326` completed core results passed with zero failures and duplicates.
The browser-presentation shards took `340.801s` and `435.133s` while they ran
beside two internally parallel Fast hosts. The same exact filters took
`156.631s` and `205.832s` alone. Running only the two shards concurrently took
`169.210s` and `219.204s`, isolating the material slowdown to Fast overlap
rather than the shard split or deterministic test growth.

An exact experiment that delayed browser audit descriptors until Fast drained
still timed out at `15:00.341`, with all `4,327/4,327` completed results green.
The prerequisite moved contention rather than removing the critical path and
was rejected.

**Decision**: Retain measured PreMerge costs for underestimated slow classes so
long-first bin packing does not leave a two-minute small-case class at the tail.
For `BrowserCommandPresentationAuditTests`, load each selected source save once
per test-host fixture into a lazy template. Preserve isolation by cloning the
prepared tree for every browser and console row and constructing fresh state/service
objects. Hash each prepared tree after load and compare it once during fixture
disposal; do not hash the tree after every row.

Run the four ExplorerWeb shards as an isolated startup wave before Fast. Their
no-Fast walls were `180.81s`, `120.83s`, `159.88s`, and `125.34s`; the two
ExplorerWeb shards that started with Fast in the rejected run took `364.54s`
and `293.45s`. An order-only exact run demonstrated that admitting Fast after
the first ExplorerWeb completion still stretched the two remaining shards to
`291.17s` and `258.72s`; it timed out at `15:00.199` after `4,819/4,819`
completed results and `490/490` ProcessIntegration results passed, before E2E
could publish a TRX. Therefore all four startup descriptors drain before the
ordinary second wave begins under the unchanged four-host/two-Fast limits.

**Guard evidence**: The source guard requires the retained cost map, one
fixture-owned `LoadGameAsync` call site, isolated clone/delete calls, browser and
console fixture routing, and template hash verification. The full class passes
all `166/166` rows in `1:49` test time, versus `362.463s` combined for the two
old isolated shards. PlanOnly retains 22 unique descriptors and 4,825 estimated
cases without a project prerequisite; its first four rows are the four
ExplorerWeb shards. A direct cap-two run of the generated browser shards passes
`85/85` and `81/81` in `59.24s` and `51.47s` test time.

## Decision 9: Reuse Explorer Seed Profiles and Set the PreMerge Budget to 20 Minutes

**Evidence**: Source tracing found that the two 50-row player-default theories
rewrote the same large realm fixture families for every row. Their rows summed
`165.50s` and `138.60s` in the retained true-wave run. A class fixture now
creates one empty canonical skeleton per test host, clones it into a distinct
writable root per row, and lazily prepares immutable keyed seed profiles for
repeated deterministic theory setup. A focused isolation contract proves that
two case roots differ, a seed factory runs once per profile, and a mutation in
one case is absent from the next. Fixture disposal hashes the skeleton and all
created seed profiles. The full ExplorerWeb class passes `436/436` in `3:51`
test time. The final four-host diagnostic passed all four shard selections at
`96.83s`, `90.76s`, `95.88s`, and `91.88s` TRX wall time without Fast.

Later exact 15-minute controls remained correctness-green but capacity-red:
the prefix-only run reached `4,819/4,819` plus completed
ProcessIntegration before E2E, and the true-drain run reached `4,329/4,329`
core results before its in-progress ProcessIntegration phase was terminated.
The user therefore explicitly approved changing only the PreMerge lane-wide
hard limit from 15 to 20 minutes. All other lane limits, the four-host and
two-Fast ceilings, filters, cases, assertions, and parallel/exclusive phase
boundaries remain unchanged.

**Accepted control**: The exact 20-minute PreMerge run passed `4,836/4,836`
results in `14:18.302`, including ProcessIntegration `490/490` and E2E
`15/15`. It reported exit `0`, no timeout, no duplicate IDs, and complete
owned-tree cleanup. Its result directory is
`20260813-044749-940-43368-f64c53dc7a7e48f5a30055b05c1b7e95-premerge`.

The documented runner interface is:

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

## Secondary Work

Phase selection met the micro-benchmark but the first bounded Fast controls
still missed their budget under concurrent fixture initialization. The Guardian
fixture now captures one prepared 47-file snapshot in memory per test host and
materializes independent physical roots per test. An isolation regression
proves that two roots do not share writes and that the repository baseline is
unchanged. No mutable on-disk template or hard link is shared.
