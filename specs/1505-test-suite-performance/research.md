# Research: Test Suite Performance and Verification Lanes

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

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

## Decision 10: Raise Only the PreMerge Budget to 30 Minutes

**Evidence**: The #1547 exact 20-minute control completed all `6,608/6,608`
available core results green with zero duplicates and complete owned-tree
cleanup, but timed out before the exclusive process/E2E tail could complete.
The core phase consumed approximately 17 minutes. The isolated
ProcessIntegration lane then passed `523/523` in `3:32` wall time, and the
retained isolated E2E timing adds further unavoidable work. The measured green
lower bound therefore exceeds 20 minutes without indicating a correctness or
cleanup defect.

**Decision**: Change only the PreMerge/Complete lane-wide hard deadline from 20
to 30 minutes. Preserve every other lane timeout, the four-host and two-Fast
ceilings, filters, cases, assertions, retained costs, startup waves,
parallel/exclusive phase boundaries, and phase order. Thirty minutes remains a
protective bound while avoiding repeated contract amendments that merely chase
seconds after legitimate suite growth.

**Alternatives rejected**: Removing coverage or assertions violates the suite
contract. Micro-optimizing individual tests to recover a few seconds would not
restore durable headroom. Increasing process concurrency would change resource
and cleanup risk. Re-running the known-impossible 20-minute gate would consume
development time without new evidence.

**Accepted control**: The exact 30-minute PreMerge run at
`TestResults/test-lanes/20260826-004148-035-3528-42f32409a6b34cf4bbb7950b7e8a10d7-premerge/summary.json`
completed in `00:21:59.6684306` with exit `0`, timeout `false`, zero duplicate
IDs, and complete owned-tree cleanup. All `26` official TRX files completed:
Fast `4,339/4,339`, core integration `2,269/2,269`, ProcessIntegration
`508/508`, and E2E `15/15`, totaling `7,131/7,131`. Frontend verification also
passed `141/141`, and both C# builds reported zero warnings/errors.

## Decision 11: Restore the Physical Fast Boundary Instead of Raising Its Limit

**Evidence**: The #1551 baseline discovers 7,797 Fast cases in 29 descriptors
and reaches the unchanged five-minute hard stop. Four mixed descriptors run in
about `3:30`, `3:55`, `1:05`, and `4:00` without build time. Aggregate TRX
ranking identifies canonical file/restart/rollback workflows, lease contention,
real worker processes, HTTP hosting, browser transport parity, and large
file-backed state machines as the dominant work. Detached wound/effect
contracts, parsers, reducers, and source guards are not the primary cost.

**Decision**: Preserve Fast as the complete fast-project selection with no
negative category filter and keep its five-minute hard limit and two-host
ceiling. Move reviewed integration-heavy sources physically into
`BookOfEternityClient.IntegrationTests` with `RegressionIntegration`,
`ProcessIntegration`, or `E2E` according to behavior. Split mixed QTE coverage
so deterministic input/grading tests remain fixture-free in Fast while its
canonical persistence/rollback lifecycle moves to Integration. Exact
source/category manifests protect both placement and discovery.

**Alternatives rejected**: Raising the Fast timeout would rename the regression
instead of restoring ordinary feedback. `Category!=Slow` would make the fast
assembly depend on a permissive negative filter. More process concurrency is
unsafe before removing contention-heavy work. A numeric duration threshold is
not a semantic test boundary and would move detached unit contracts merely for
being large.

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

## Issue #1551 Final Boundary and Evidence

Fast is not an alias for all repository tests. It is the entire physically
isolated `BookOfEternityClient.Tests` project, selected without a negative
category filter under the unchanged five-minute limit and two-host ceiling.
The final placement rule is:

```text
canonical file/restart/rollback/lifecycle/contention -> RegressionIntegration
real child process -> ProcessIntegration
complete HTTP host/browser flow -> E2E
fixture-free deterministic unit/parser/reducer/contract/source guard -> Fast
```

### Exact executable manifests after #1536 Task 1

The 58 entries below are the complete, ordinal contents of
`FastTestBoundaryTests.ReviewedHeavySourcePaths`. Categories are the exact
Integration ownership enforced at the same HEAD. The three historical special
groups retain their complete class categories; method-level
`PreMergeSentinel` traits are not expanded here.

| Reviewed-heavy relative path | Exact Integration category ownership |
|---|---|
| `AfterlifeSpiritualConflictValidationTests.cs` | `RegressionIntegration`, `RegressionIntegrationOnly` |
| `GameEngineTurnLifecycleTests.cs` | `LifecycleIntegration` |
| `GuardianSystemRegressionTests.cs` | `RegressionIntegration`, `DeepValidation` |
| `FileSystemManagerTests.cs` | `ProcessIntegration` |
| `ConsoleE2ESmokeTests.cs` | `ProcessIntegration`, `E2E` |
| `LocalWebUiBuiltFrontendSmokeTests.cs` | `ProcessIntegration`, `E2E` |
| `MortalWoundRecoveryTests.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentCapabilityAuthorityTests.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentAcceptedStateRegistryTests.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ColdClaimRecovery.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.CourseContinuation.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.DetachedRequirementAuthority.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.DetachedSealCoordinates.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.DetachedSourceValidation.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.DeteriorationPolicyAuthority.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.FreshAuthority.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.HistoryPersistence.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.Legacy.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.OutcomeIntents.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.PersistedRepairWave.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.Persistence.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.PersistenceHardening.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.PersistenceIngress.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.PrerequisiteAuthorities.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ProcedureAuthority.ContractRegression.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ProcedureAuthority.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ProcedureAuthority.ReviewRegression.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ProcedurePublication.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.Replay.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ResourceAuthority.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ResourceFinalization.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.ResourcePublication.cs` | `RegressionIntegration` |
| `MortalWoundTreatmentResolverTests.VehicleTopology.cs` | `RegressionIntegration` |
| `QteSceneServiceTests.cs` | `RegressionIntegration` |
| `GmWorkerLiveSmokeTests.cs` | `ProcessIntegration` |
| `LocalWebUiSmokeTests.cs` | `E2E` |
| `ShiningCoreActionResolutionValidationTests.cs` | `RegressionIntegration` |
| `GuardianCorrectionServiceTests.cs` | `RegressionIntegration` |
| `ShiningBlessingEffectStateTests.cs` | `RegressionIntegration` |
| `AfterlifeNotificationStateTests.cs` | `RegressionIntegration` |
| `DarenQteShowcaseTests.cs` | `RegressionIntegration` |
| `ShiningTradeRequestStateTests.cs` | `RegressionIntegration` |
| `BrowserLocalWriteCoordinatorTests.cs` | `RegressionIntegration` |
| `TrainingServiceTests.cs` | `RegressionIntegration` |
| `NpcTradeServiceRequestFlowTests.cs` | `RegressionIntegration` |
| `MortalWoundOpportunityAdapterTests.cs` | `RegressionIntegration` |
| `ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs` | `RegressionIntegration` |
| `GmWorkerValidationRepairDelegatorTests.cs` | `ProcessIntegration` |
| `WebUi/BrowserMortalWorldGenerationFencingTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserStorageTransportParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserShiningRelicForgeParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserResidentInteractionsParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserAfterlifeArchiveParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserShiningIncarnationGatesParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserInkFeatherFateParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserNpcSocialParityTests.cs` | `RegressionIntegration` |
| `WebUi/BrowserTradeParityTests.cs` | `RegressionIntegration` |

The second executable array,
`IntegrationTestBoundaryTests.RegressionIntegrationSources`, contains exactly
these 35 ordinal entries after class-level ownership hardening:

```text
AfterlifeSpiritualConflictValidationTests.cs
BrowserCommandPresentationAuditTests.cs
ExplorerModeCommandTests.cs
ExplorerWebCommandServiceTests.cs
ExplorerWebCommandServiceTests.Effects.cs
ExplorerWebCommandServiceTestsAfterlifeProfileInboxDrilldowns.cs
ExplorerWebCommandServiceTestsSpiritualConflictArtDrilldowns.cs
GuardianSystemRegressionTests.cs
LocalWebUiHostTests.cs
ResourceConsoleBrowserParityTests.cs
MortalWoundRecoveryTests.cs
MortalWoundTreatmentCapabilityAuthorityTests.cs
MortalWoundTreatmentAcceptedStateRegistryTests.cs
MortalWoundTreatmentResolverTests.cs
QteSceneServiceTests.cs
ShiningCoreActionResolutionValidationTests.cs
GuardianCorrectionServiceTests.cs
ShiningBlessingEffectStateTests.cs
AfterlifeNotificationStateTests.cs
DarenQteShowcaseTests.cs
ShiningTradeRequestStateTests.cs
BrowserLocalWriteCoordinatorTests.cs
TrainingServiceTests.cs
NpcTradeServiceRequestFlowTests.cs
MortalWoundOpportunityAdapterTests.cs
ExplorerWebCommandServiceTestsShiningAbodeDrilldowns.cs
WebUi/BrowserMortalWorldGenerationFencingTests.cs
WebUi/BrowserStorageTransportParityTests.cs
WebUi/BrowserShiningRelicForgeParityTests.cs
WebUi/BrowserResidentInteractionsParityTests.cs
WebUi/BrowserAfterlifeArchiveParityTests.cs
WebUi/BrowserShiningIncarnationGatesParityTests.cs
WebUi/BrowserInkFeatherFateParityTests.cs
WebUi/BrowserNpcSocialParityTests.cs
WebUi/BrowserTradeParityTests.cs
```

Treatment resolver sources that require accepted-state files, persistence,
leases, resource claims, publication, restart, or replay belong in
Integration. The 25-file partial family moves intact with its registry
companion; fixture-free parsers, projectors, fingerprints, reducers, and pure
policy tests remain Fast. This semantic boundary leaves the Fast five-minute
hard limit and all assertions unchanged.

`ActorMaterializationValidationTests.cs` and
`AfterlifeEntityProfileValidationTests.cs` were removed from this class-level
manifest because their owning classes carry `FullValidation`; only reviewed
individual methods carry `RegressionIntegration`. Those method traits remain
unchanged and covered by their dedicated guards, but they no longer masquerade
as whole-class ownership through source-text matching.

The QTE split preserves all 117 prior rows exactly once: 66 fixture-free
input/grading rows moved to Fast `QteDeterministicLogicTests`, while 51
canonical filesystem/persistence/rollback/console/service lifecycle rows remain
in Integration `QteSceneServiceTests`. Final-review remediation pins those
methods and exact normalized InlineData arguments in an executable Roslyn
manifest. It also corrects the mixed Daren move: 67 fixture-free methods / 77
rows now live in Fast `DarenQteDeterministicLogicTests`, while the 12
profile/filesystem/service/browser-projection methods / 12 rows remain in
Integration `DarenQteShowcaseTests`. A second executable manifest proves each
Daren row has exactly one semantic owner. The conditional second group also moved
`UiTestTextCollector` unchanged into TestSupport because five moved WebUi
sources required it; it contains no tests and prevents an Integration-to-Fast
dependency.

### Retained Tasks 2-6 evidence

Counts below are `total/executed/passed/failed`; `T/D/C` is
timeout/duplicate IDs/cleanup complete. Every listed C# build had zero warnings
and errors. PlanOnly rows execute no tests, so their membership is stated in
the selection column.

| Task / selection | Run ID | Counts | Wall | Exit | T/D/C |
|---|---|---:|---:|---:|---|
| T2 Fast ownership expected RED | `20260904-032527-671-56936-b77c2f42039440cbbc971eb993ccb0de-focused` | `1/1/0/1` | `00:01:07.7403826` | 1 | `false/0/true` |
| T2 Integration categories expected RED | `20260904-032643-161-24968-fbdda59496a84196811d003d7ad99c84-focused` | `2/2/0/2` | `00:00:26.6363529` | 1 | `false/0/true` |
| T3 wound Integration, exact retained 23-row #1536 RED | `20260904-034612-251-20992-58eadf68ae95438a8eb131fd940a28e6-focused` | `171/171/148/23` | `00:04:23.7597787` | 1 | `false/0/true` |
| T3 browser Integration | `20260904-035149-444-40188-95f7936a168b447ebe87add44d0f4185-focused` | `32/32/32/0` | `00:00:42.1181461` | 0 | `false/0/true` |
| T3 carrier RED then GREEN | `20260904-040609-753-19200-2bae51006f0d47bc86517d3fcde17c8c-focused` / `20260904-040734-196-54420-b09496576a0f400796c0979396c60d0d-focused` | `2/2/0/2` / `2/2/2/0` | `00:01:11.3016198` / `00:00:18.0064888` | 1 / 0 | both `false/0/true` |
| T3 final PlanOnly, 29 descriptors; moved sources absent | `20260904-040819-457-508-ded67e79bbfb4ff3a24b01a36d466da3-fast` | `0/0/0/0` | `00:00:08.0616409` | 0 | `false/0/true` |
| T4 deterministic QTE Fast | `20260904-042302-971-39008-83794b0d29f74765859b33d86de41d7e-focused` | `66/66/66/0` | `00:00:14.5095196` | 0 | `false/0/true` |
| T4 lifecycle QTE Integration | `20260904-042325-116-19364-2df389aa065b49e99339ea3f49a679cb-focused` | `51/51/51/0` | `00:01:44.4049944` | 0 | `false/0/true` |
| T4 split guard | `20260904-042657-556-10772-ff09b0b7f0af4c608491d131af29151e-focused` | `1/1/1/0` | `00:00:14.7060732` | 0 | `false/0/true` |
| T4 PlanOnly, deterministic source present and lifecycle source absent | `20260904-042717-466-38408-2df7736da83b46c8a8030afcb9592639-fast` | `0/0/0/0` | `00:00:07.6248356` | 0 | `false/0/true` |
| T5 worker / web-host / ownership / category controls | `20260904-044800-283-22496-eabbfe7297ff45469bc78a89d8d95b01-focused`; `20260904-044925-973-19340-48e8e96251ac463da74dfbc29afd6a27-focused`; `20260904-045001-024-24176-34c38db24f52419ca5b5406f9a65c7f7-focused`; `20260904-045046-960-38728-bd1e2492908c4e6d9dca910be1929cb4-focused` | `2/2/2/0`; `3/3/3/0`; `1/1/1/0`; `1/1/1/0` | `00:01:19.9493842`; `00:00:25.4680317`; `00:00:41.4962020`; `00:00:14.4963618` | 0 | all `false/0/true` |
| T5 PlanOnly, both moved sources absent | `20260904-045107-726-53268-4268c88108c44461bab8e4171b5d882a-fast` | `0/0/0/0` | `00:00:07.2837745` | 0 | `false/0/true` |
| T6A ownership/category guards | `20260904-050338-928-27936-004e14ec35404ad5a05af2db9385378e-focused`; `20260904-050532-534-23532-7e16380a5946400e8eca589bac2e7383-focused` | `16/16/16/0`; `2/2/2/0` | `00:01:32.8347878`; `00:00:43.9099028` | 0 | both `false/0/true` |
| T6A PlanOnly, 29 descriptors / 7,642 estimated cases | `20260904-050628-477-30176-f8fc8029c0f94ae5bc3700dd289bbb6f-fast` | `0/0/0/0` | `00:00:07.6398781` | 0 | `false/0/true` |
| T6A Fast, 3/29 descriptors complete | `20260904-050716-260-14408-92e55dd08bfa4578873420c86eb28c98-fast` | `3851/3851/3786/65` | `00:03:47.1066186` | 1 | `false/0/true` |
| T6B moved regression / worker | `20260904-053040-565-34500-b84b30e475884b2c922c5a52ba047d63-focused`; `20260904-053341-627-49856-1c21d778852943e1b4deb4a6ced5f24f-focused` | `618/618/618/0`; `30/30/30/0` | `00:02:55.2524909`; `00:00:56.0599022` | 0 | both `false/0/true` |
| T6B ownership/category guards | `20260904-053445-341-1908-863c48ead0974db6a4159b550b0d83ab-focused`; `20260904-053553-202-34096-7d3ebf58ffb5443a8bbfa652f1b84cbe-focused` | `16/16/16/0`; `2/2/2/0` | `00:01:03.0061816`; `00:00:17.1607749` | 0 | both `false/0/true` |
| T6B PlanOnly, 29 unique descriptors / 6,994 estimated cases | `20260904-053617-304-25836-62faa02a7bc841e59d2103d3bd628f9c-fast` | `0/0/0/0` | `00:00:07.5797893` | 0 | `false/0/true` |
| T6B Fast, 5/29 descriptors complete | `20260904-053631-589-34832-f506ebe681e0413d938c1dd5e2b4132d-fast` | `4682/4682/4617/65` | `00:02:30.3420234` | 1 | `false/0/true` |

Task 6B also retained the expected post-move compile RED
`20260904-052658-019-10816-c9138e1055d04779933dc0470e1fe37c-focused`
(`0/0`, exit `1`, `00:00:23.8396194`, timeout false, zero duplicates, cleanup
complete, six `CS0103` errors) that motivated the unchanged
`UiTestTextCollector` TestSupport move. The same exact 18-class selection then
passed `618/618` in the T6B row above.

### Pre-remediation Task 7 diagnostics and Fast controls

| Lane | Run ID | Counts | Wall | Exit | T/D/C |
|---|---|---:|---:|---:|---|
| RegressionIntegration | `20260904-055025-376-22060-7926a0cfbbcb44d09641bbd2a0ae58ec-regressionintegration` | `151/151/131/20` | `00:07:01.9184530` | 1 | `false/0/true` |
| ProcessIntegration | `20260904-055735-809-43312-0548bbcf39b94f25a30c6bcb39a6d205-processintegration` | `555/555/552/3` | `00:03:19.8007804` | 1 | `false/0/true` |
| E2E, first required attempt before ignored dependencies were restored | `20260904-060102-849-34064-63d1263e811b4726bd91d4a26b905ec9-e2e` | `0/0/0/0` | `00:00:03.3535784` | 1 | `false/0/true` |
| Fast 1 | `20260904-060112-027-15080-3aabb9b5b4d24f6a9b9b0490dc98e97a-fast` | `4682/4682/4617/65` | `00:02:45.7436920` | 1 | `false/0/true` |
| Fast 2 | `20260904-060403-781-54340-98a04ba1518640079b30995f87620ccc-fast` | `4682/4682/4617/65` | `00:02:22.0382468` | 1 | `false/0/true` |
| Exact 20-method Integration diagnostic | `20260904-060841-723-34880-f4ad580b841f4876bd16d455655f0f84-focused` | `23/23/1/22` | `00:01:14.5117495` | 1 | `false/0/true` |
| E2E after `npm ci` restored ignored dependencies | `20260904-061601-948-15524-cd780f2129b04dc782dc4e6122cd00cc-e2e` | `18/18/18/0` | `00:01:28.5961264` | 0 | `false/0/true` |
| Exact two built-frontend smoke methods after `dist` build | `20260904-061737-528-23048-28391b77381c455c95ebccb5d5de8fff-focused` | `2/2/2/0` | `00:00:22.0749183` | 0 | `false/0/true` |

All available TRX files parsed cleanly and reported zero TRX errors, timeouts,
or not-executed rows. Every C# build reported zero warnings/errors. The first
E2E attempt failed before C# build/discovery only because the ignored
`node_modules/.bin/tsc.cmd` was absent after a workstation restart. `npm ci`
changed no tracked package file; the authorized rerun passed frontend
verification `141/141`, built `dist`, and passed E2E `18/18`. The two original
built-frontend ProcessIntegration failures then passed their exact Focused
rerun. The exact diagnostic reproduced all 20 Guardian
TradeOfferingResonance failures with the same stable IDs. Its other pass was
`GmTurnHelperContractTests.Daemon_QteEffectResolutionDispatchesWithoutOrdinaryTurnAuthority`,
so that ProcessIntegration failure is also a one-run intermittent concern.
The 20 Guardian rows are unrelated pre-existing/current-branch functional RED,
not #1551 repair work.

Both pre-remediation Fast controls are capacity successes below five minutes, with zero
duplicates and complete cleanup, but they are official RED and incomplete.
Fail-fast completed five of 29 planned descriptors and left 24 without complete
TRX evidence. Each control's 65 failure display names and stable IDs exactly
match the retained #1536 T067 oracle with zero differences. The display-name
and ID hashes remain
`61ed2c828bc81212d76ec589671d5423bbab0d97871dfa9d455458c296af59c6`
and `4179c375351d0dd2ba976cb4b941768462fd09136db95e025c1555ddc919a264`.
The measured contour improved from the T6A `3:47.106` partial wall to the T6B
`2:30.342` partial wall; functional green and complete Fast membership remain
blocked on #1536 turning those 65 rows green.

### Accepted final-review remediation and current controls

The accepted whole-range review found three related guard gaps: Daren had been
moved wholesale despite containing mostly deterministic coverage; QTE row
preservation was prose-only; and category ownership used source-text matching.
The remediation split Daren semantically, added exact executable QTE/Daren
Fact/Theory/InlineData manifests, and changed category ownership to Roslyn
parsing of attributes on the expected top-level class. Synthetic comment,
string, and method-level trait decoys now fail closed.

The expected RED and GREEN evidence is:

| Selection | Run ID | Counts | Wall | Exit | T/D/C | Result |
|---|---|---:|---:|---:|---|---|
| Three accepted-finding guards, initial RED | `20260904-065523-376-53800-730a3ecb969a45029e491f3cf1b70d72-focused` | `3/3/0/3` | `00:01:18.0284395` | 1 | `false/0/true` | expected: inventory stub, absent Fast Daren owner, category decoys |
| Exact-inventory mutation guard, RED | `20260904-065717-934-24148-4e93e4930c214c9f98975442806ca717-focused` | `1/1/0/1` | `00:00:42.7053343` | 1 | `false/0/true` | expected inventory stub failure |
| Intermediate guards | `20260904-065859-543-10480-ca7b28e02fff438c9600dac312413c21-focused` | `4/4/2/2` | `00:00:42.7426224` | 1 | `false/0/true` | category and mutation guards GREEN; Daren source still absent |
| New exact guards before full real-manifest replay | `20260904-070425-608-24244-c45c2f13227646ceb27cd60c34a4e256-focused` | `4/4/4/0` | `00:00:42.9439882` | 0 | `false/0/true` | GREEN |
| Fast QTE + Daren inventories | `20260904-070626-646-41228-234615f3240c48cc94c9f4eff5ede765-focused` | `143/143/143/0` | `00:00:38.6772669` | 0 | `false/0/true` | QTE 66 + Daren 77 GREEN |
| Integration QTE + Daren inventories | `20260904-070710-393-7732-24492aa15afe432096dda850d82631b9-focused` | `63/63/63/0` | `00:01:20.3481739` | 0 | `false/0/true` | QTE 51 + Daren 12 GREEN |
| Current Fast PlanOnly | `20260904-070836-796-19044-f3c6b8f4a7364bb1913611176107307a-fast` | `0/0/0/0` | `00:00:07.2951573` | 0 | `false/0/true` | 29 descriptors / 7,071 estimated cases; Daren Fast owner exactly once |
| Current Fast 1 | `20260904-070944-389-28440-60e246e1148944bb86b2b8bd2d582246-fast` | `4759/4759/4694/65` | `00:02:17.7762792` | 1 | `false/0/true` | exact known #1536 RED |
| Current Fast 2 | `20260904-071206-972-37312-3696b2f3daf248da93c08092f04e572b-fast` | `4759/4759/4694/65` | `00:02:13.7896671` | 1 | `false/0/true` | exact known #1536 RED |
| Full real category/inventory guard replay, first diagnostic | `20260904-072235-522-29092-da02a9a9e38645c58a69b42c7079c258-focused` | `7/7/5/2` | `00:00:22.9540006` | 1 | `false/0/true` | exposed two method-only Regression sources and stale synthetic input |
| Full real category/inventory guard replay, second diagnostic | `20260904-072435-554-43092-99522e9cfe8a4704a780f3cad28e11b5-focused` | `7/7/6/1` | `00:00:48.7572847` | 1 | `false/0/true` | exposed the `*.Effects.cs` class-name exception |
| Full real category/inventory guard replay, final | `20260904-072614-482-29264-42280f9c7c514b2c81a8105cc709fa63-focused` | `7/7/7/0` | `00:00:53.3636176` | 0 | `false/0/true` | GREEN |
| Final Fast source-boundary guard replay | `20260904-072739-105-12356-0b5fbbe5653545e7b6524678c509a1af-focused` | `3/3/3/0` | `00:00:15.2320432` | 0 | `false/0/true` | GREEN |

Two compile REDs during extraction,
`20260904-070515-491-49892-cbedf0525c914d379bca1c5212f71dbe-focused`
and
`20260904-070548-130-53152-9062058882bb47ceaf895e97a5117aba-focused`,
each reported 41 missing `Characteristics` references and were corrected by
restoring the required Configuration namespace. The fresh-build semantic guard
`20260904-070318-617-31520-bf31057ac2bf4ce091f44387ffe06cf9-focused`
then exposed an over-broad test assertion: static deterministic
`DarenQteRewardProfileService.ResolveEnding` is valid Fast logic, while
constructing the file-backed service is forbidden. The guard was narrowed to
that actual boundary. The earlier `-NoBuild` artifact
`20260904-070259-895-58596-1b61ace80e2a4424bca527ed55c34076-focused`
used stale binaries and is explicitly not acceptance evidence.

The final full-manifest replay removed two false whole-class entries rather
than broadening runtime selection: `ActorMaterializationValidationTests` and
`AfterlifeEntityProfileValidationTests` remain class-owned by FullValidation,
and their reviewed method-level Regression traits remain unchanged. The
class-name resolver preserves the existing
`ExplorerWebCommandServiceTests.Effects.cs` ->
`ExplorerWebCommandServiceEffectTests` convention while still requiring the
attribute on that expected class.

Both current Fast controls execute the restored 77 deterministic Daren rows:
the pre-remediation `4,682/4,617/65` contour is now
`4,759/4,694/65`. Their 65 failure display names and stable IDs have zero set
differences from the retained #1536 oracle; hashes remain
`61ed2c828bc81212d76ec589671d5423bbab0d97871dfa9d455458c296af59c6`
and `4179c375351d0dd2ba976cb4b941768462fd09136db95e025c1555ddc919a264`.
Both are below five minutes with no timeout, zero duplicate IDs, and complete
cleanup. Final independent whole-range review of `3dbf0572..4c8dc6aa`
confirmed exact QTE/Daren body and assertion preservation, closed every
Critical/Important finding, and released T064 without a merge-only PreMerge run.

This work is internal test placement and scheduling only. It changes no
production game capability, command, mechanic, state/validation/normalizer
contract, player-visible UI, GM-authored output, afterlife
pending/control/action/receipt/report surface, or daemon/launcher prompt.
Mortal World and afterlife prompts, gameplay examples, validation manifests,
the afterlife contract matrix, and their source guards therefore require no
update.
