# Research: Test Suite Performance and Verification Lanes

**Source issue**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

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

PreMerge uses one 15-minute deadline across frontend verification, both
test-project builds, discovery, tests, and cleanup. Its non-overlapping
schedule is:

1. the complete fast assembly plus integration tests filtered with
   `Category!=FullValidation&Category!=DeepValidation&Category!=ProcessIntegration&Category!=E2E&(Category!=LifecycleIntegration|Category=PreMergeSentinel)`,
   with at most four test hosts overall and at most two fast hosts;
2. `Category=ProcessIntegration&Category!=E2E` sequentially;
3. `Category=E2E` sequentially.

DeepValidation is the disjoint Integration-only union
`(Category=FullValidation|Category=DeepValidation)&Category!=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E`.
It has a 1,950-result floor. LifecycleIntegration selects
`Category=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E` as
one descriptor under a ten-minute cap and has a 186-result floor. PreMerge has
a 4,490-result floor. Exactly ten lifecycle methods also carry
`PreMergeSentinel`; every other lifecycle method stays outside routine
PreMerge.

## Final Verification Decision

Run focused controls during implementation, two consecutive Fast controls at
final verification, and one PreMerge control. Do not serially run all
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
