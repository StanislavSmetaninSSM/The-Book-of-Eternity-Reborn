# Data Model: Validation Selection and Test Lanes

**Source issue**: [#1505](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1505)

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
| `Focused` | Caller-supplied VSTest filter in the fast project | 5 min | One class/method/domain |
| `FullValidation` | Integration project; `Category=FullValidation` | 15 min | Diagnostic full-pipeline sentinels |
| `RegressionIntegration` | Integration project; `Category=RegressionIntegration` | 15 min | Diagnostic file-backed workflows |
| `ProcessIntegration` | Integration project; `Category=ProcessIntegration` | 15 min | Diagnostic real child-process tests |
| `E2E` | Integration project; `Category=E2E` | 15 min | Diagnostic end-to-end workflows |
| `LifecycleIntegration` | Integration project; `Category=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E` | 10 min | Conditional complete GameEngine lifecycle control |
| `DeepValidation` | Integration project; `(Category=FullValidation|Category=DeepValidation)&Category!=LifecycleIntegration&Category!=ProcessIntegration&Category!=E2E` | 15 min | Conditional exhaustive validation control |
| `PreMerge` | Both projects; non-overlapping parallel and exclusive phases | 15 min total | Final integration control |
| `Complete` | Temporary alias for `PreMerge` | 15 min total | Compatibility only |

The fast and integration assemblies are the classification boundary. Fast does
not use negative category filters: it discovers
`BookOfEternityClient.Tests.csproj` directly. Slow categories remain useful for
focused diagnostic selection inside
`BookOfEternityClient.IntegrationTests.csproj`.

PreMerge has one deadline across frontend verification, both project builds,
discovery, tests, and cleanup. Its parallel phase selects the complete fast
assembly and integration tests with
`Category!=FullValidation&Category!=DeepValidation&Category!=ProcessIntegration&Category!=E2E&(Category!=LifecycleIntegration|Category=PreMergeSentinel)`;
its exclusive phases use `Category=ProcessIntegration&Category!=E2E` and then
`Category=E2E`. DeepValidation is the disjoint Integration-only union of
FullValidation and DeepValidation categories, excluding LifecycleIntegration,
ProcessIntegration, and E2E. LifecycleIntegration selects all 186 lifecycle
cases as one external process. Exactly ten methods additionally carry
`PreMergeSentinel`; those ten are the only intentional overlap between the
complete lifecycle lane and routine PreMerge.

Default Fast is the ordinary post-edit control. Final merge verification is two
Fast runs plus one PreMerge run. DeepValidation is conditional and explicit;
LifecycleIntegration follows the same conditional policy. Diagnostic lanes are
not serial final gates and do not run after every edit.

### Workflow commands

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
