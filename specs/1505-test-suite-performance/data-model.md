# Data Model: Validation Selection and Test Lanes

**Source issues**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505); Phase 45 capacity amendment [#1502](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1502); suite-growth scheduling correction [#1526](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1526); measured deadline correction [#1547](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1547); Fast project-boundary repair [#1551](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1551)

This feature adds no persisted gameplay data. The model consists of internal
runtime/test values and lane-result artifacts.

## GameStateValidationPhase

An internal 32-bit flags enum with these single-bit members in production order:

1. `JsonIntegrity`
2. `RequiredFiles`
3. `RequiredFields`
4. `LoreBootstrapRequiredFiles`
5. `MortalBootstrapPlayerVisibleNames`
6. `MortalBootstrapContentAnchors`
7. `CrossReferences`
8. `SoulStateConsistency`
9. `PlayerStateFiles`
10. `NpcStateFiles`
11. `SkillContractConsistency`
12. `TrainingShowcases`
13. `WorldQuestCombatFactionStateFiles`
14. `MetaMiscStateFiles`
15. `AcceptedTurnActorMaterializationCompleteness`
16. `AfterlifeSpiritualConflictState`
17. `SourceOfLightCapstoneGlobalState`
18. `ShiningLeadershipHeadReferences`
19. `LifeEvaluationRewardCycle`
20. `NoLifeEvaluationRewardsOnTriggerTurn`
21. `GuardianResonancePowerEvents`
22. `ShiningTreasuryClientOwnedState`
23. `AfterlifeActiveThreatPreTurnContinuity`
24. `AfterlifeGlobalFlagPreTurnContinuity`
25. `ClientOwnedControlFiles`
26. `RealmSegregation`

`RivalAndResidentCrossReferences` is an additional internal test-only rule-group
scope. It reuses existing cross-reference rules, is not a 27th production
phase, and is excluded from `All`. `Selectable` contains `All` plus this
targeted scope. `None` is not a valid execution request.

### Invariants

- Every phase member is a unique power of two.
- `All` contains every production phase and no targeted-only bit.
- A request is valid only when it is non-zero and contains no bit outside
  `Selectable`.
- Selected phases execute at most once and in the numbered order above.
- The public validation facade always uses `All`.
- Per-run mutable caches are reset after mask validation and before the first
  selected phase.

## GameStateValidationSelection

An internal immutable request containing a non-empty phase/scope mask and an
optional non-empty set of normalized state-file paths. No path set means the
selected phases retain their normal all-file behavior.

### Invariants

- An explicitly empty state-file set is rejected.
- Path filtering applies only to generic strict/flexible state-file walkers;
  phase-specific cross-reference reads remain explicit.
- The public facade always uses production `All` with no state-file filter.
- Consecutive selections do not share active selection or policy-cache state.

## GuardianValidationProfile

A test-side named `GameStateValidationSelection` composed from reviewed
phase/scope values and, where useful, the exact state files asserted by the
domain.

### Invariants

- Every profile is non-empty and valid.
- Profiles are named for a coherent guardian source domain.
- Dependencies are explicit in the mask.
- No profile changes production behavior.
- A profile expands only after a focused RED test demonstrates missing rule
  ownership.

## TestLane

| Lane | Project/selection | Hard limit | Purpose |
|---|---|---:|---|
| `Fast` | Entire fast project; no category filter | 5 min | Ordinary local changes |
| `Focused` | Caller-supplied VSTest filter in the selected fast or integration project | 5 min by default; explicit override up to 15 min | One class/method/domain |
| `FullValidation` | Integration project; `Category=FullValidation` | 15 min | Diagnostic full-pipeline sentinels |
| `RegressionIntegration` | Integration project; `Category=RegressionIntegration` | 15 min | Diagnostic file-backed workflows |
| `ProcessIntegration` | Integration project; `Category=ProcessIntegration` | 15 min | Diagnostic real child-process tests |
| `E2E` | Integration project; `Category=E2E` | 15 min | Diagnostic end-to-end workflows |
| `LifecycleIntegration` | Integration project; `Category=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E` | 10 min | Conditional complete GameEngine lifecycle control |
| `DeepValidation` | Integration project; `(Category=FullValidation|Category=DeepValidation)&Category!=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E` | 15 min | Conditional exhaustive validation control |
| `PreMerge` | Both projects; non-overlapping parallel and exclusive phases | 30 min total | Final integration control |
| `Complete` | Temporary alias for `PreMerge` | 30 min total | Compatibility only |

The fast and integration assemblies are the classification boundary. Fast does
not use negative category filters: it discovers
`BookOfEternityClient.Tests.csproj` directly. Slow categories remain useful for
focused diagnostic selection inside
`BookOfEternityClient.IntegrationTests.csproj`.

Under #1551, exact relative source paths form the reviewed-heavy manifest.
Canonical file/restart/rollback/lifecycle and lease-contention sources carry
`RegressionIntegration`; real child-process sources carry
`ProcessIntegration`; full host/browser flows carry `E2E`. A mixed source is
split so fixture-free deterministic logic remains in Fast and its integration
lifecycle has exactly one categorized owner in Integration.

Fast therefore means the complete physically isolated
`BookOfEternityClient.Tests` project, not every repository test. Its project
selection has no negative category filter, its hard limit remains five minutes,
and its external-host ceiling remains two. The exact semantic mapping is:

```text
canonical file/restart/rollback/lifecycle/contention -> RegressionIntegration
real child process -> ProcessIntegration
complete HTTP host/browser flow -> E2E
fixture-free deterministic unit/parser/reducer/contract/source guard -> Fast
```

`QteDeterministicLogicTests` is the fixture-free Fast owner for 66 input and
grading rows. `QteSceneServiceTests` is the Integration owner for 51 canonical
persistence, rollback, console, save/archive, and service lifecycle rows.
`UiTestTextCollector` is a test-free TestSupport helper shared by moved WebUi
sources so Integration has no dependency on the Fast project. The exact
32-entry reviewed-heavy manifest and 35-entry
`RegressionIntegrationSources` array at `b1165f27` are recorded in
`research.md`; the executable guard arrays remain authoritative.

PreMerge has one deadline across frontend verification, both project builds,
discovery, tests, and cleanup. Its parallel phase selects the complete fast
assembly and integration tests with
`Category!=FullValidation&Category!=DeepValidation&Category!=ProcessIntegration&Category!=E2E&(Category!=LifecycleIntegration|Category=PreMergeSentinel)&(Category!=RegressionIntegrationOnly|Category=PreMergeSentinel)`;
its exclusive phases use `Category=ProcessIntegration&Category!=E2E` and then
`Category=E2E`. DeepValidation is the disjoint Integration-only union of
FullValidation and DeepValidation categories, excluding LifecycleIntegration,
ProcessIntegration, and E2E. LifecycleIntegration selects all 186 lifecycle
cases as one external process. Exactly ten methods additionally carry
`PreMergeSentinel`; those ten are the only intentional overlap between the
complete lifecycle lane and routine PreMerge. The exhaustive 358-case
`AfterlifeSpiritualConflictValidationTests` class carries
`RegressionIntegrationOnly`; exactly ten reviewed methods also carry
`PreMergeSentinel`, while all 358 remain available through
RegressionIntegration. The Phase 45 PreMerge minimum is 4,240 merged,
non-duplicate results plus completed ProcessIntegration and E2E phases.

PreMerge has its own retained class-duration map for small, slow integration
classes whose case-count fallback materially understates wall time. The map
affects long-first bin packing only; it never changes filters, case discovery,
phase membership, concurrency ceilings, or assertions.

PreMerge partitions its existing parallel phase into two resource-isolation
waves. Wave one contains every `ExplorerWebCommandServiceTests` descriptor and
must drain completely. Wave two contains every remaining parallel descriptor
and uses the ordinary four-host/two-Fast scheduler. No descriptor, filter, test,
or assertion moves between the parallel, ProcessIntegration, or E2E phases;
ProcessIntegration and E2E remain exclusive and sequential after both waves.

`BrowserCommandPresentationAuditFixture` owns three lazy prepared save
contexts. Each context stores an already loaded template root plus its file-hash
snapshot. Every theory row receives a cloned case root and fresh state/service
objects, and the case root is deleted after execution. Fixture disposal compares
the prepared roots with their snapshots before deleting the owned fixture tree.

`ExplorerWebCommandSeedTemplateFixture` owns one lazy empty canonical skeleton
per test host and a keyed set of lazy prepared seed profiles. A test instance
starts from a distinct clone of the empty skeleton; deterministic repeated
theory setup is materialized once into a hashed prepared profile and copied
into each row root. Fixture disposal verifies that neither the skeleton nor a
prepared profile changed.

Default Fast is the ordinary post-edit control. Final merge verification is one
Fast checkpoint plus one PreMerge run. DeepValidation, LifecycleIntegration,
and the exhaustive RegressionIntegration matrix are conditional and explicit.
Diagnostic lanes are not serial final gates and do not run after every edit.

### Workflow commands

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

## #1551 Final Result State

The final PlanOnly result
`20260904-053617-304-25836-62faa02a7bc841e59d2103d3bd628f9c-fast`
contains 29 unique, well-formed Fast-project descriptors and 6,994 estimated
cases/cost, with zero moved-class membership, duplicate IDs, timeout, or cleanup
debt.

The two final Fast controls
`20260904-060112-027-15080-3aabb9b5b4d24f6a9b9b0490dc98e97a-fast`
and
`20260904-060403-781-54340-98a04ba1518640079b30995f87620ccc-fast`
finished in `00:02:45.7436920` and `00:02:22.0382468`. Each executed
`4,682`, passed `4,617`, and failed the exact retained 65 #1536 T067 rows;
exit was `1`, timeout was false, duplicate IDs were zero, and cleanup was
complete. Their capacity result satisfies the five-minute bound, but their
functional result is official RED and their fail-fast contour is incomplete:
five of 29 descriptors completed and 24 lack complete TRX evidence. The
measured pre/post contour is Task 6A `3:47.106` partial to Task 6B `2:30.342`
partial.

Task 7's environment-valid E2E rerun
`20260904-061601-948-15524-cd780f2129b04dc782dc4e6122cd00cc-e2e`
passed frontend verification `141/141` and E2E `18/18` in
`00:01:28.5961264`, with exit `0`, no timeout/duplicates, and complete cleanup.
The exact two built-frontend smoke methods also passed `2/2` in
`20260904-061737-528-23048-28391b77381c455c95ebccb5d5de8fff-focused`.
RegressionIntegration remains official RED with 20 unrelated, reproduced
Guardian trade/offering/resonance rows, and one ProcessIntegration QTE helper
failure passed on exact rerun and remains intermittent. These results are lane
artifacts only and do not add persisted game state.

Issue #1551 changes only internal test placement and scheduling. No production
game capability, command, mechanic, state/validation/normalizer contract,
player-visible UI, GM-authored output, afterlife pending/control/action/receipt
or report surface, or daemon/launcher prompt changed. Mortal World/afterlife
prompts, gameplay examples, validation manifests, afterlife contract matrix,
and source guards therefore require no update.

## TestLaneResult

Ignored artifacts under
`TestResults/test-lanes/<timestamp>-<pid>-<guid>-<lane>/`:

- One or more `.trx` files: machine-readable VSTest results for the lane or its
  discovery-balanced chunks.
- `dotnet-test.log`: merged standard output/error.
- `summary.json`: requested/effective lane, timeout, wall time, exit code,
  timeout/cleanup outcome, total/executed/passed/failed counters, and
  cross-descriptor duplicate test IDs.
- Console summary: the same primary evidence plus result and log paths.

Each result directory includes timestamp, runner PID, GUID, and lane to prevent
same-lane invocations from sharing artifacts. Skipped cases are derived as
`Total - Executed`.

No lane result is canonical game state or committed repository data.
