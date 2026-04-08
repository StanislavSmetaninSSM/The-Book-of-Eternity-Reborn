# Guardian Policy Convergence Audit

## Статус документа

`IMPLEMENTED CONVERGENCE`

Этот файл фиксирует финальное converged состояние по остаточным проблемам в системе политики хранителей.

Правило работы по нему:

- новые numbered phases по перечисленным seam’ам не открывать;
- любые дальнейшие правки должны сравниваться с этим файлом как с final audit record;
- closure по текущему remediation считается достигнутой, пока новый discovery-review не найдёт undocumented seam.

## Цель аудита

Остановить phase creep и собрать decision-complete inventory всех оставшихся seams между:

- validator authority build
- guardian-project normalizer
- runtime orchestration / rollback
- test coverage

## Итог аудита

Последний corrective batch закрыт.

Итоговое состояние:

- reopened `normalizer atomicity` seam закрыт ранним guardian-project current-input preflight;
- reopened `missing current guardians for guardian-side side-consumption` seam закрыт точным incarnation-aware remaining-consumables gate для current guardians;
- guardian-project preflight и сам guardian-project pass теперь используют один и тот же effective `soul_state` context, включая backup-derived `currentIncarnation/currentRealm`;
- backup baseline может дополнять readable current `soul_state.json`, но malformed или absent current `soul_state.json` теперь fail-close-ится всякий раз, когда guardian-project normalization зависит от soul-derived context;
- validator kernel и proof-local tracker materialization теперь используют тот же baseline-aware и project-aware `soul_state` contract, что и normalizer; `lore_research` остаётся realm-aware только там, где `targetIncarnation` ещё нужно derive-ить, materialized lore current-pass checks тянут только нужный `currentIncarnation`, а `soul_preparation` требует только readable integer `currentIncarnation`;
- rival/world-event lore clue normalization теперь использует тот же strict baseline-aware `soul_state` contract, что и остальная guardian policy; malformed или absent current `soul_state.json` больше не suppress-ит visible rival clue spending silently;
- rival/world-event bonus clue normalization и validation больше не treat malformed current `rival_soul_arcs.json` / `world_events.json` as empty state; unreadable current rival/world surfaces теперь fail-close-ятся explicit error path only on actual current-pass rival/world-event-dependent paths, while public-signal-only rival clue flows and backup-only old world-event history no longer over-require current `world_events.json`;
- validator rival-clue budget validation теперь current-life-aware: completed `lore_research` project authorizes visible bonus clues только если его `targetIncarnation` applies to the current life;
- missing coverage on both reopened seams добавлено и зафиксировано regressions;
- runtime refresh split, validator/proof parity и QTE rollback remain closed.

## Residual Seam Inventory

### RCG-01

- Severity: `High`
- Classification: `runtime orchestration bug`
- Layers: `runtime`, `normalizer`
- Paths:
  - `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs`
  - `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`
  - `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
  - `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
  - `BookOfEternityClient/Core/GameEngine/GameEngine.IncarnationAndAfterlife.cs`
- Final resolved behavior:
  - `GameEngine` runtime refresh split into:
    - baseline-backed canonical refresh
    - view-only runtime refresh
  - production null-backup canonical refresh path removed;
  - accepted-turn canonicalization always stays snapshot-backed on every pass.
- Resolution:
  - remains closed; not part of the reopened corrective batch.

### NFG-01

- Severity: `High`
- Classification: `normalizer atomicity bug`
- Layers: `normalizer`, `runtime`
- Paths:
  - `BookOfEternityClient/Services/CanonicalStateNormalizer.cs`
  - `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs`
  - `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SharedAndSoulHelpers.cs`
- Final resolved behavior:
  - guardian-project current tracker/current guardians readability is preflighted before any earlier accumulated normalizers run;
  - malformed current `guardian_projects.json`, malformed current `guardians.json`, and malformed or missing current `soul_state.json` on soul-context-dependent guardian-project paths now fail before `NormalizeSoulStateAsync(...)`, `NormalizeGuardiansAsync(...)` or `NormalizeGuardianAbodeResidentsAsync(...)` can rewrite files.
- Resolution:
  - closed by shared guardian-project normalization input preflight plus atomicity regressions.

### NFG-02

- Severity: `High`
- Classification: `normalizer fail-open bug`
- Layers: `normalizer`
- Paths:
  - `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardiansAndProjects.cs`
  - `BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.GuardianProjectHelpers.cs`
  - `BookOfEternityClient/Services/GuardianProjectState.cs`
- Final resolved behavior:
  - current guardians readability is required whenever guardian-project command reconciliation or completed-project reconciliation still has remaining guardian-side consumables that can be reconciled in the current normalization pass;
  - tracker-local completed-project effects, such as rival-clue budget, no longer over-require current `guardians.json`;
  - absent current `guardians.json` no longer silently disables lore/gacha consumption when remaining guardian-side consumables still exist.
- Resolution:
- closed by replacing the coarse project-type gate with an incarnation-aware remaining-consumables predicate shared with `GuardianProjectState` semantics and pinning strict, tracker-local permissive, and future-incarnation permissive paths with regressions.

### TCG-01

- Severity: `Medium`
- Classification: `test coverage gap`
- Layers: `tests`
- Final resolved behavior:
  - focused regressions now pin atomic current-input failure before earlier writes;
  - focused regressions also pin missing current `guardians.json` failure for completed-project guardian-side side-consumption;
  - focused regressions now pin malformed and absent current `soul_state.json` failure for backup-derived soul-context-dependent guardian-project normalization.
  - focused kernel regressions now pin readable-partial current `soul_state` merge from validated snapshot baseline, malformed current `soul_state` failure for guardian-project tracker authority, and strict snapshot tracker-proof invalidation for readable partial validated snapshot `soul_state` without trusted baseline completion.
- Resolution:
  - closed.

## Runtime Caller Inventory

### `RefreshCanonicalStateAsync(...)` callers

| Caller | Baseline source | `guardian_projects.json` possible | Final status | Resolution |
| --- | --- | --- | --- | --- |
| `BuildContinueDescriptionAsync()` | none | yes | safe | migrated to view-only refresh |
| `ContinueCurrentSessionFlow()` | none | yes | safe | migrated to view-only refresh |
| `InitializeChaosSea()` | none | no after `ClearGameState()` | safe | remains harmless view/runtime refresh |
| `HandleReenterShiningAbode()` | none | yes | safe | migrated to view-only refresh |
| `HandleReturnToChaosSeaFromShiningAbode()` | none | yes | safe | migrated to view-only refresh |
| `HandleNewGamePlus()` | none | no after reset | safe | remains harmless view/runtime refresh |
| `EnterGameLoop()` ready-signal refresh | none | yes | safe | migrated off null-backup canonical refresh |
| `EnterGameLoop()` console-resize refresh | none | yes | safe | migrated to view-only refresh |
| `EnterGameLoop()` manual `/refresh` | none | yes | safe | migrated to view-only refresh |
| `ShowLifeEvaluationRewards()` | none | yes | safe | migrated to view-only refresh |
| `CheckGmIncarnationTrigger()` | none | yes | safe | migrated to view-only refresh |
| `CheckAscensionTrigger()` | none | yes | safe | migrated to view-only refresh |
| `ValidateAcceptedTurnOutcomeWithRepairLoop()` final refresh | none | yes | safe | final refresh is view-only |
| `RefreshAcceptedTurnCanonicalStateForValidationAsync()` first pass | validated pending-turn snapshot | yes | safe | unchanged snapshot-backed canonical refresh |
| `RefreshAcceptedTurnCanonicalStateForValidationAsync()` later pass | validated pending-turn snapshot | yes | safe | later passes now also stay snapshot-backed |
| `FinalizePendingMemoryLegacyConsumptionAsync()` | none | yes | safe | migrated to view-only refresh |
| `RestorePreTurnBackup()` post-restore refresh | restored files only | yes | safe | now uses view-only refresh after restore |

### Direct `NormalizeAccumulatedStateAsync(...)` callers

| Caller | Baseline source | Final status | Notes |
| --- | --- | --- | --- |
| `GameEngine.RefreshCanonicalStateAsync(...)` | explicit backups only | safe | no nullable runtime canonical wrapper remains |
| `QteSceneService.ApplyTerminalOutcomeStateChangesCoreAsync(...)` | explicit captured QTE baseline | safe | already converged with rollback |

## Guardian Project Normalizer Authority Input Set

| Input | Layer usage | Required? | Current behavior | Currently fail-closed? |
| --- | --- | --- | --- | --- |
| Pre-turn tracker backup baseline | normalizer | yes when tracker exists | explicit preflight + local guard | yes |
| Pre-turn guardians backup baseline | normalizer | yes when tracker exists and current guardians file exists | explicit preflight + local guard | yes |
| Current tracker readability | normalizer | yes when current tracker file exists | shared preflight validates readability before any earlier accumulated writes; guardian-project step reuses the same contract | yes |
| Current guardians readability | normalizer | yes when command reconciliation or current-pass-reconcilable guardian-side consumables require guardian state | shared preflight validates requirement/readability early; guardian-project step rereads canonicalized current guardians after `NormalizeGuardiansAsync(...)` | yes |
| Current soul-state context (`currentIncarnation`, `currentRealm`) | normalizer | yes for soul-context-dependent gating | derived from the same normalized `soul_state` contract in both entrypoint preflight and guardian-project execution, including backup baseline merge when current `soul_state.json` is readable; required fields are project-aware (`lore_research`: incarnation + realm only when target must be derived, otherwise only needed current-pass incarnation checks; `soul_preparation`: incarnation only) | yes |
| Current guardian project journal readability | normalizer append path | no baseline required | malformed journal rebuilds from empty object | acceptable current behavior |

## Authority Channel Taxonomy Result

На этом проходе новых authority-channel correctness bugs не выявлено.

Текущее состояние:

- proof-local strict current power-event context uses strict slot, not generic slot;
- generic current active-guardian wording in system flows already says `current guardian authority`, not `strict`;
- guardian-trade current resolution wording is aligned with generic current authority;
- tracker baseline taxonomy already distinguishes missing vs semantic-invalid validated tracker baseline.

Остаточных authority-channel or guardian-project convergence seams на этом документе больше не зафиксировано.

## Cross-Layer Convergence Matrix

| Scenario | Validator | Proof | Normalizer | Runtime | Divergence |
| --- | --- | --- | --- | --- | --- |
| Missing validated pre-turn tracker / guardians baseline | fail-closed | fail-closed | fail-closed | fail-closed | no |
| Malformed current `guardian_projects.json` | fail-closed | fail-closed | fail-closed | fail-closed | no |
| Missing current `guardians.json` for guardian-side side-consumption | fail-closed | fail-closed | fail-closed | fail-closed | no |
| Malformed current `guardians.json` during command reconciliation | fail-closed | fail-closed | fail-closed | fail-closed | no |
| Backup-derived `soul_state` for incarnation-aware lore gating | fail-closed | fail-closed | fail-closed | fail-closed | no |
| Rival clue consumption with malformed or absent current `soul_state` | fail-closed | n/a | fail-closed | fail-closed | no |
| Future-incarnation `lore_research` visible clue budget | fail-closed | n/a | fail-closed | fail-closed | no |
| Malformed current `rival_soul_arcs.json` / `world_events.json` on actual rival/world-event-dependent lore clue paths | fail-closed | n/a | fail-closed | fail-closed | no |
| QTE tracker normalization / rollback | converged | n/a | converged | converged | no |

## Coverage Audit

### Existing focused regressions

1. Malformed current `game_state/meta/guardian_projects.json` during guardian-project normalization:
   - covered by explicit fail-closed regression
   - asserts no rewrite on failure inside guardian-project path

2. Malformed current `game_state/meta/guardians.json` during guardian-project normalization:
   - covered for command reconciliation
   - asserts no silent command-skip path

3. Backup-derived `currentIncarnation` / `currentRealm` for incarnation-aware lore gating:
   - covered for both strict current-pass lore reconciliation and permissive future-incarnation lore reconciliation
   - asserts guardian-project preflight uses the same effective `soul_state` contract as the later guardian-project step

4. Incarnation-only `soul_preparation` soul-state contract:
   - covered in normalizer, kernel tracker authority, and proof-local snapshot tracker materialization
   - asserts readable partial `soul_state` without `currentRealm` remains valid for `soul_preparation`, while realm-aware lore paths stay strict

5. Materialized completed-lore soul-state contract:
   - carried-over completed `lore_research` with tracker-local effects stays permissive without `currentRealm`
   - completed lore with remaining guardian-side consumables still requires the current-pass `currentIncarnation` needed to decide reconciliation

6. Real `GameEngine` refresh paths with existing `guardian_projects.json` and null backups:
   - covered through migrated runtime contract and updated source guards

7. Source-guard update:
   - stale null-refresh assertion replaced
   - source guard pins snapshot-backed canonical refresh plus runtime view refresh split

8. Rival clue soul-state contract:
   - covered for malformed current `soul_state.json`, absent current `soul_state.json`, and readable partial current `soul_state` completed from backup baseline during visible rival clue consumption
   - asserts rival/world-event lore clue normalization uses the same strict soul resolver instead of a raw `currentIncarnation = 0` fallback

9. Rival clue current-life validation:
   - covered for current-life completed `lore_research` budget acceptance and future-incarnation completed `lore_research` budget rejection
   - asserts validator no longer blesses visible bonus clue sources whose `targetIncarnation` is not the current life

10. Rival/world current-file readability:
   - covered for malformed current `rival_soul_arcs.json` and malformed current `world_events.json` on actual linked rival/world-event clue paths
   - asserts normalizer and validator both fail closed instead of silently skipping clue reconciliation or cross-reference validation

11. Precise current `world_events.json` gating on rival slice:
   - covered for malformed but irrelevant current `world_events.json` when rival clue usage lives entirely in `publicSignals`
   - asserts public-signal-only rival clue normalization and validation remain permissive and no longer emit world-event current-state failures just because a malformed current world-events file exists

### Missing regressions

None in the scope of this audit. The reopened atomicity seam and both sides of the current-guardians predicate are now pinned directly.

### Already covered and not the current gap

- missing baseline failure for guardian-project normalizer
- partial backup map without tracker baseline
- partial backup map with tracker baseline but without guardians baseline
- QTE baseline capture / rollback
- generic current wording drift in system-guardian flows
- validated pre-turn tracker semantic-invalid taxonomy

## Final Fix Batch

### Subsystem 1: `CanonicalStateNormalizer`

- completed

### Subsystem 2: `GameEngine` runtime orchestration

- no reopened runtime caller migration is required;
- current runtime split remains valid and should stay untouched unless the preflight fix reveals a new caller misuse.

### Subsystem 3: validation/repair runtime flow

- no reopened validation/repair routing change is required on this pass;
- runtime inherits the normalizer fix once entrypoint atomicity is corrected.
- validator rival bonus clue checks now validate current-life applicability of completed `lore_research` source projects instead of trusting raw granted budget alone.

### Subsystem 4: tests and guards

- completed

## Exit Criteria

Closure state for this audit is satisfied:

1. No production caller can invoke guardian-project normalization without a usable full baseline when tracker state may exist.
2. Malformed current `guardian_projects.json` causes explicit normalization failure before any earlier accumulated file is rewritten.
3. Missing or malformed current `guardians.json` causes explicit normalization failure whenever command reconciliation or current-pass-reconcilable guardian-side consumables require current guardian state, and does not block tracker-local or future-incarnation completed-project normalization.
4. Runtime, validator, proof, and normalizer agree on the same guardian-project baseline/readability contract, including the absent current guardians case and the future-incarnation lore distinction.
5. Focused regressions exist for:
   - malformed current tracker normalization
   - malformed or missing current guardians normalization in all authority-relevant scenarios
   - tracker-local completed-project normalization without current guardians
   - future-incarnation lore normalization without current guardians
   - entrypoint atomicity for guardian-project current-input failure
6. Source guards no longer encode the old null-backup canonical refresh model.
7. After these fixes, no further discovery pass is needed before closure review.

## Notes For Implementation

- Do not reopen raw/current fallback behavior.
- Do not create new numbered phases for the items tracked in this document.
- Treat this file as the final audit record for the implemented guardian-policy convergence state.
