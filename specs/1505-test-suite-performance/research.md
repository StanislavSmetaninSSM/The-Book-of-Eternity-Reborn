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

## Decision 4: Conditional Class Partition

**Decision**: First remove the 26-phase multiplier. Partition the partial class
only if bounded post-migration evidence shows the remaining sequential cost
prevents the accepted budgets.

**Rationale**: Private helpers and fixture setup span partial files. Premature
class extraction risks duplicated inherited tests or shared mutable state.
Phase selection is the measured root-cause fix and retains deterministic
sequential execution.

## Decision 5: Trait-Based Lanes

**Decision**:

- `FullValidation`: tests/classes intentionally invoking the public full pipeline.
- `ProcessIntegration`: tests/classes starting a real child process.
- `E2E`: end-to-end client or Agent Console workflows.
- Fast filter: `Category!=FullValidation&Category!=ProcessIntegration&Category!=E2E`.
- Complete: no filter.

Class-level traits are acceptable when a file/class is predominantly a slow
integration group. Method-level traits are used for isolated sentinels.

## Decision 6: Bounded Local Runner

**Decision**: Add `scripts/test-csharp.ps1` as the stable local entry point.
It maps lane names to filters, writes a timestamped log and TRX, enforces a
timeout, and calls `.Kill($true)` only on the `dotnet` process it started.

**Rationale**: Raw `dotnet test` has no suite-wide wall-clock timeout and an
agent/tool timeout does not reliably document or clean the owned child tree.

**Safety boundary**: The script never enumerates and kills processes by name.
It owns only the root process object it creates and its operating-system process
tree. Normal test completion remains responsible for its own resources; the
script records an error if the root cannot be confirmed exited.

## Secondary Work

The guardian fixture copies a 47-file base session for every test instance.
Fixture-copy optimization is deferred unless phase selection misses the budget:
it is measurable but secondary, and shared mutable templates could compromise
test isolation.
