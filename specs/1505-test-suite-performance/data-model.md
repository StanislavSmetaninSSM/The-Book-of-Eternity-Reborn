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

`All` is the bitwise union of all 26 members. `None` is not a valid execution
request.

### Invariants

- Every phase member is a unique power of two.
- `All` contains every defined phase and no undefined bits.
- A request is valid only when it is non-zero and `(request & ~All) == 0`.
- Selected phases execute at most once and in the numbered order above.
- The public validation facade always uses `All`.
- Per-run mutable caches are reset after mask validation and before the first
  selected phase.

## GuardianValidationProfile

A test-side named constant composed only from `GameStateValidationPhase` values.

### Invariants

- Every profile is non-empty and valid.
- Profiles are named for a coherent guardian source domain.
- Dependencies are explicit in the mask.
- No profile changes production behavior.
- A profile expands only after a focused RED test demonstrates missing rule
  ownership.

## TestLane

| Lane | Filter | Default timeout | Purpose |
|---|---|---:|---|
| `Fast` | Exclude `FullValidation`, `ProcessIntegration`, `E2E` | 7 min | Ordinary local changes |
| `Focused` | Caller-supplied VSTest filter | 10 min | One class/method/domain |
| `FullValidation` | `Category=FullValidation` | 15 min | Full-pipeline sentinels and broad validation groups |
| `ProcessIntegration` | `Category=ProcessIntegration` | 15 min | Real child-process tests |
| `E2E` | `Category=E2E` | 15 min | End-to-end workflows |
| `Complete` | No filter | 20 min | Final integration control |

## TestLaneResult

Ignored artifacts under `TestResults/test-lanes/<timestamp>-<lane>/`:

- `test-results.trx`: machine-readable VSTest result.
- `dotnet-test.log`: merged standard output/error.
- Console summary: lane, filter, timeout, wall time, exit code, result path, and
  timeout/cleanup outcome.

No lane result is canonical game state or committed repository data.
