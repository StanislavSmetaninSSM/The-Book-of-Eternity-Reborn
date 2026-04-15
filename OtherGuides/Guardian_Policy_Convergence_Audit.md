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
- rival/world-event lore clue normalization теперь использует тот же strict baseline-aware `soul_state` contract, что и остальная guardian policy; malformed, absent или mixed valid-plus-unsupported current `soul_state.json` больше не suppress-ят visible rival clue spending silently, normalizer и validator используют один и тот же unsupported-top-level classifier на strict rival bonus-clue paths, а strict soul gate больше не срабатывает на sponsored rival/world surfaces, где completed lore budget есть, но current-pass visible bonus clue source так и не доказан;
- validator dormant rival bonus-clue permissiveness теперь держится end-to-end: malformed current `soul_state.json` больше не abort-ит `ValidateGameStateAsync()` через поздний resident/soul-relic cross-ref parse, а `AfterlifeResidents` current-soul issues теперь поднимаются только на actual resident/relic-dependent cross-ref paths вместо unconditional fallout на dormant rival bonus-clue scenarios или plain resident-only states без relic linkage; strict resident/relic contract при этом покрывает все реальные relic-backed paths, включая reverse-only `soulRelics[].companionSeed.sourceResidentId`, skipped-rival manifested companion `sourceCompanionRelicId`, missing current `soul_state.json` на proven dependency path, malformed reverse-only soul surfaces, и mixed valid-plus-unsupported policy-sensitive current `soul_state.json`, где canonical `currentIncarnation` / `soulRelics` coexist-ят с посторонним visible top-level key; malformed current `rival_soul_arcs.json`, non-object current rival root, readable contract-invalid current rival object без allowed top-level rival keys, readable-but-shape-invalid current rival arc collection, mixed valid-plus-unsupported current top-level rival/world/resident/quest owner files, или authority-relevant current `world_events.json` больше не suppress-ят unrelated `AfterlifeResidents` checks, а manifested-companion current `npc_core.json` теперь использует тот же lifecycle-approved top-level contract, что и `ValidateNpcFile("game_state/npcs/npc_core.json", ...)`, при этом `NPCsRenameData` остаётся lifecycle-valid но non-participating section для manifested-companion dependency scanning, а lifecycle-invalid aliases `NPCs`, `npcs` и `npcDataChanges` больше не считаются clean authority state и не порождают downstream manifested-companion semantic noise поверх local owner-state failure; malformed, missing, readable contract-invalid или readable-but-shape-invalid current `guardian_abode_residents.json` на current-state-proven resident-dependent paths теперь surfaced as local owner issue и short-circuit-ят resident-id–dependent downstream checks instead of fabricating false resident/relic back-link diagnostics, а malformed, missing, readable contract-invalid, readable non-participating или readable-but-shape-invalid current `soul_quests.json` на resident-linked или branch-local quest-owned current cross-ref paths тоже больше не abort-ят validation и surfaced as local `SoulQuests` owner issue вместо throw/fallthrough; skipped-rival quest flow теперь либо реально валидирует `relatedRivalArcId` against reference rival arcs, либо не делает missing current `soul_quests.json` strict без такого reference set, и никогда не использует validated pre-turn rival arc fallback поверх broken current rival state; skipped-rival `world_events.relatedRivalArcId` следует тому же branch-local rival fallback contract, mirror-ит main-path rival-thread visibility checks и использует validated pre-turn rival arc ids только при absent/non-participating current rival state, но не поверх broken current rival file;
- rival/world-event bonus clue normalization и validation больше не treat malformed or missing current `rival_soul_arcs.json` / `world_events.json` as empty state; unreadable or absent current rival/world surfaces теперь fail-close-ятся explicit error path only on actual current-pass rival/world-event-dependent paths, while public-signal-only rival clue flows and backup-only old world-event history no longer over-require current `world_events.json`, including schema-shaped but clue-irrelevant malformed payloads, world-event-only malformed or missing current `world_events.json` no longer slips through just because truncation happened before linked clue markers, before the first current-world candidate field, after a partial relevant field key start, or because the current world-events file was deleted outright, mixed current passes no longer let either one valid sponsored `publicSignal` or already exhausted visible budget weaken strict current-world handling for additional linked world-event clue usage, and missing current `world_events.json` on hostile direct-target rival contracts now surfaces explicit `rival_arc_world_event_invalid_current_state` instead of falling through to partial-evidence clue sufficiency;
- validator rival-clue budget validation теперь current-life-aware: completed `lore_research` project authorizes visible bonus clues только если его `targetIncarnation` applies to the current life;
- owner-state regression coverage for guardian-policy validator теперь сведено в matrix-style rows по всем ключевым current-file contracts этого slice: `guardian_abode_residents.json`, `soul_quests.json`, `world_events.json`, `rival_soul_arcs.json` и policy-sensitive `soul_state.json` покрыты decision-complete invalid-shape classes (`missing` / `malformed` / `non-object` / `contract-invalid top-level` / `invalid collection shape`, плюс readable-partial/valid-readable where supported), включая explicit mixed valid-plus-unsupported top-level `soul_state` owner-failure rows на strict resident/relic и strict rival bonus-clue paths, а manifested-companion `npc_core.json` отдельно pin-ит malformed, contract-invalid, lifecycle-invalid alias и shape-invalid dependency paths вместе с permissive no-dependency paths;
- current owner-state readers inside guardian/rival validator теперь используют единый explicit classification contract (`missing`, `unreadable`, `non-object`, `contract-invalid top-level`, `invalid collection shape`, `readable with collection`, `readable but non-participating`), а shared classifier больше не treat-ит current files с canonical array plus unsupported visible top-level keys как clean authority state; branch-local policy decide-ит strictness/fallback уже после этой classification instead of re-implementing file-shape heuristics ad hoc per reader;
- shared guardian-policy contract descriptors теперь централизуют `soul_state` lifecycle/strict-authority/patch-write/canonical-write semantics: lifecycle path по-прежнему принимает transient accepted-turn command roots вроде `metaStateUpdates`, `afterlifeArchiveUpdates` и `archiveActionResolutions`, strict guardian-policy authority допускает только policy-approved readable surface, а patch-write path теперь domain-aware и conflict-aware вместо blanket-preserve или whole-root strip. Непересекающиеся transient roots сохраняются при unrelated local soul-state mutations, но конфликтующая часть transient payload prune-ится ещё на patch-write по реально materialized operation surface: внутри `metaStateUpdates` `inkFeatherChanges` теперь strip-ится целиком при любом overlapping local `InkFeathers` write, потому что текущий wire-shape остаётся aggregate `add` / `spend` bucket без provenance и не допускает безопасного partial subtraction, а `soulRelicOperations` prune-ится по exact op / `relicId` / `field`; field-only local update больше не удаляет unrelated pending work, но pending same-`relicId` `addRelic` всё же prune-ится, если local writer уже мутировал существующую canonical relic с этим `relicId`, чтобы следующий normalizer pass не создавал duplicate cross-array relic copy. Внутри `afterlifeArchiveUpdates` / `archiveActionResolutions` по-прежнему удаляются только overlapping archive entries/resolutions по `archiveId` / `requestId`. `AfterlifeArchiveActionState` cleanup path теперь тоже идёт через shared patch helper и больше не raw-write-ит `soul_state.json` мимо guardian-policy strip/prune rules, а pending archive request files удаляются только после безопасного reservation release или reservation-safe receipt reconciliation по полной identity (`requestId` + `archiveId` + `requestedMode`) вместо blind cleanup поверх unreconciled reservation; `afterlifeArchive.actionReceipts` canonicalize-ятся по той же полной identity, archive notifications и validator canonical-result proof paths больше не принимают `requestId`-only или archive-only fallback match как closure/result proof, а malformed validated pre-turn snapshot copies of pending archive requests now raise explicit malformed-snapshot issues instead of silently suppressing strict closure checks. Canonical-write contract по-прежнему ограничен реально persisted root fields и применяется только там, где runtime действительно materialize-ит canonical current state. Legacy compatibility payload `crossIncarnationData` допускается только как lifecycle/read-only compatibility key, не считается strict guardian-policy authority root key и consistently strip-ится при любом soul-state write. В результате local rename сохраняет unrelated transient roots, trade и companion-manifestation patch paths не re-emit-ят conflicting `metaStateUpdates`, companion manifestation field-only writes больше не оставляют unsafe same-`relicId` `addRelic` replay, archive reservation / archival / cleanup patch paths не re-emit-ят conflicting archive transient entries и не orphan-ят reservation из-за premature request-file deletion или requestId-only cleanup drift, а normalizer/materialization, new-cycle reset и другие canonical current-state writes по-прежнему strip-ят все transient command roots целиком. Non-canonical aliases вроде top-level `currentTier` остаются unsupported и отдельно guarded against shipped sample/fixture drift. Для `npc_core` lifecycle/carrier split тоже централизован: lifecycle-approved top-level contract включает `UpdateNPCs`, `NPCsInScene`, `NPCsRenameData` и `UpdateNpcTradeInventoryReceipts`, но manifested-companion carrier surface по-прежнему ограничен canonical NPC object sections `UpdateNPCs` / `NPCsInScene`; strict normalizer, kernel validator, accepted-turn proof paths и runtime/read-model NPC consumers теперь читают одни и те же policy-sensitive contracts, legacy alias sections `NPCs` / `npcs` / `npcDataChanges` больше не считаются canonical runtime state ни для guardian-policy validation, ни для scene-memory/world-maturity runtime readers, top-level `UpdateNpcTradeInventoryReceipts` теперь получает full receipt-schema validation даже при broken/missing `tradeInventory` surface и отдельно cross-check-ится against materialized canonical NPC trade state, pending NPC trade resolution видит top-level receipt updates как first-class accepted-turn input, а broken `npc_core.json` fallback больше не использует whole-file raw token mining или suffix-level guessing после unbounded carrier section и поднимает local owner issue только при safely bounded key-level evidence внутри canonical carrier sections `UpdateNPCs` / `NPCsInScene` вместо простых string mentions source-field names; `NPCsRenameData`, `UpdateNpcTradeInventoryReceipts` и lifecycle-invalid aliases не считаются dependency surface даже в degraded path;
- retained malformed pending archive request files теперь видимы для runtime/read-model и validation: create paths, explorer gating и afterlife notifications treat-ят `pending_archive_consultation_request.json` / `pending_archive_project_fuel_request.json` как blocking state even when file exists but is unreadable or structurally incomplete, поэтому broken pending control file нельзя silently overwrite новым request, а validator поднимает explicit malformed-file issue вместо того, чтобы считать такой pending state отсутствующим. `metaStateUpdates.inkFeatherChanges` patch pruning теперь intentionally conservative: overlapping local feather write strip-ит whole aggregate `inkFeatherChanges` object, потому что current contract не различает provenance отдельных deltas внутри `add` / `spend` buckets. Same-`relicId` `addRelic` остаётся preserved only when replay structurally safe, включая stored-only relic field updates. Validator-side `metaStateUpdates` contract теперь зеркалит runtime surface: `inkFeatherChanges` допускает только non-negative integer JSON number `add` / `spend` без string fallback, `CanonicalStateNormalizer` fail-close-ится explicit `InvalidOperationException` на malformed current `metaStateUpdates.inkFeatherChanges` вместо silent consume-and-clear, `soulRelicOperations` — только canonical ops `addRelic` / `removeRelic` / `equipRelic` / `unequipRelic` / `updateRelicField` с обязательными runtime-required identifiers, а `lifeTransitions.recordLifeCompletion` и `memoryLegacyGrant` теперь тоже проходят strict runtime shape gate перед materialization вместо object-only tolerance. Top-level transient command roots (`metaStateUpdates`, `afterlifeArchiveUpdates`, `archiveActionResolutions`) и их runtime item surfaces больше не mask-ятся через wrong-type/unknown-key/skip-then-clear semantics: patch-write и normalizer fail-close-ятся на malformed top-level roots, включая explicit `null` для archive transient arrays, unknown visible `metaStateUpdates` subcommands, malformed `lifeTransitions` / `memoryLegacyGrant`, и malformed archive update/resolution items вместо silent removal или silent skip. Archive runtime apply path теперь требует canonical add-entry / accepted-resolution payload, а не partial identifiers only. Local `soul_state` patch writers больше не repair-ят malformed-present canonical roots перед записью: shared patch helper и mutating trade/archive write paths теперь используют один и тот же strict canonical-root gate для `inkFeathers`, `soulRelics` и `afterlifeArchive`, а validator-side guardian/rival authority reconstruction наконец получает тот же `TriggerLifeEnd` context, что и runtime normalization, поэтому legitimate `recordLifeCompletion` больше не считается unreadable только из-за hardcoded `hasCanonicalTriggerLifeEnd=false`;
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
- focused rival/world-event regressions now pin malformed and missing current `world_events.json` fail-closed behavior even for bare current world-event containers and partial-key truncation that stop before any complete candidate/clue field name appears, while keeping schema-shaped but clue-irrelevant malformed payloads permissive and keeping dormant completed-lore-budget rival paths permissive when no current bonus clue source exists.
- focused rival/world-event validator regressions now pin the end-to-end dormant-path contract for malformed current `soul_state.json`: `ValidateGameStateAsync()` no longer throws through late resident/soul-relic parsing, produces no rival bonus-clue soul/world issues and no false-positive `AfterlifeResidents` current-soul issue on dormant paths, while resident/relic-dependent malformed-current-soul paths still surface the local `AfterlifeResidents` issue explicitly; additional resident-path regressions pin owner-state short-circuit parity for malformed and missing current `guardian_abode_residents.json`, including the current-only reverse resident gate against stale pre-turn-only soul evidence, explicit hostile-path failure on missing current `world_events.json`, and issue-based non-throw behavior plus malformed/missing parity for resident-linked and quest-owned `soul_quests.json` paths.
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
   - covered for sponsored rival/world-event surfaces without an actual current-pass bonus clue path: malformed or missing current `soul_state.json` no longer raises rival bonus-clue authority failures when completed lore budget remains but current `publicSignals` and linked world-event clue surface are absent

9. Rival clue current-life validation:
   - covered for current-life completed `lore_research` budget acceptance and future-incarnation completed `lore_research` budget rejection
   - asserts validator no longer blesses visible bonus clue sources whose `targetIncarnation` is not the current life

10. Rival/world current-file readability:
   - covered for malformed current `rival_soul_arcs.json`, malformed current `world_events.json`, and truncated malformed current `world_events.json` on actual linked rival/world-event clue paths
   - asserts normalizer and validator both fail closed instead of silently skipping clue reconciliation or cross-reference validation
   - includes partial-key truncation shapes such as `{"worldEventsLog":[{"ev` and `{"worldEventsLog":[{"related` on both world-event-only and mixed bonus-clue paths

11. Precise current `world_events.json` gating on rival slice:
   - covered for malformed but irrelevant current `world_events.json` when rival clue usage lives entirely in `publicSignals`
   - includes both trivial malformed `{` and non-trivial malformed but schema-irrelevant payloads such as `{"foo":`
   - covered for mixed current passes where visible sponsored `publicSignals` exist but linked world-event clue usage is still semantically possible; marker-less malformed current `world_events.json` now still fails closed on that path, including equality cases where visible `publicSignals` already occupy the full granted budget
   - asserts public-signal-only rival clue normalization and validation remain permissive and no longer emit world-event current-state failures just because a malformed current world-events file exists, including schema-shaped but clue-irrelevant payloads

12. Strict early `soul_state` gate on guardian/rival authority reads:
   - covered for malformed-present canonical current `soul_state` on the backup-derived current-life guardian-project path
   - asserts `NormalizeAccumulatedStateAsync()` now fails before earlier guardian-project side effects are written when current `inkFeathers` / `soulRelics` / `afterlifeArchive` violate strict canonical root policy
   - validator-side archive request closure no longer synthesizes current `afterlifeArchive` through `NormalizeShape()` / `EnsureStoredArray()` and instead raises an explicit current-owner-state issue

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
- `metaStateUpdates.enlightenmentProgression` now uses a strict runtime/validator-aligned contract: only `newTier`/`experience` are accepted, with required non-negative integer `experience`.
- `metaStateUpdates.lifeTransitions.recordLifeCompletion` no longer materializes without canonical `game_state/control/life_transitions.json` TriggerLifeEnd authority in the same normalization pass.
- real `NormalizeSoulStateAsync` now fail-closes on malformed-present canonical `inkFeathers` / `soulRelics` / `afterlifeArchive` roots instead of auto-healing them; permissive soul-state authority reconstruction for guardian-project side reads remains intentionally unchanged.
- current guardian-project / rival authority reads that actually require readable current soul context now use the same strict canonical-root gate before earlier normalization writes, preventing pre-`NormalizeSoulStateAsync()` partial side effects on malformed-present current canonical roots.
- strict canonical `soulRelics` gating now rejects skeletal companion/imprint relic payloads on policy-sensitive current-state paths instead of treating minimal shell objects as readable canonical relics.
- policy-sensitive current `soulRelics` readers are now converged end-to-end: resident/reverse cross-ref validation and afterlife soul-relic notifications no longer treat legacy array-form current `soulRelics` or skeletal companion/imprint relic payloads as readable owner state, so malformed current `soul_state.soulRelics` now blocks resident/imprint inference and notifications instead of producing downstream reverse-link noise.
- companion-manifestation runtime, abode-offering current soul-relic consumption proof, and gameplay characteristic bonus collection now follow the same strict current `soulRelics` readability contract; legacy array-form remains tolerated only on historical/pre-turn proof paths where backward-compatible snapshot reconstruction still requires it.
- manifestation current-owner reads now fail closed on unreadable `currentIncarnation` instead of clearing retryable pending manifestation requests as if they were merely out-of-incarnation.
- manifestation health cleanup now removes a pending request only after `soulRelics` was actually updated; a matched NPC without readable current relic collections keeps the request retryable instead of silently clearing it.
- current `soulRelics` readers now use whole-root policy-sensitive soul-state readability rather than slice-only `soulRelics` shape checks, so malformed sibling roots and malformed transient command surfaces no longer drive manifestation, abode-offering current proof, characteristic bonuses, or life-evaluation reward delta.
- policy-sensitive current `soul_state` readers now also enforce the `recordLifeCompletion` / `TriggerLifeEnd` trigger-context invariant, so current read paths no longer treat life-transition payloads as readable outside a canonical life-end turn.
- trigger-aware current-reader wiring is now live in production callers instead of existing only as an unused overload: strict soul-relic notifications, characteristic bonuses, manifestation current-owner reads, abode-offering current proof, resident/reverse validation, and guardian-project authority reconstruction now all accept canonical `recordLifeCompletion` only when the current `life_transitions.json` actually carries `TriggerLifeEnd`.
- trigger-aware current-reader authority is now lifecycle-aware, not merely syntactic: canonical-looking `life_transitions.json` no longer authorizes `recordLifeCompletion` readability when current `soul_state.currentRealm` is already in an afterlife bucket.
- trigger-aware current readers no longer derive lifecycle authority from mutated current `soul_state.currentRealm`: they now require mortal pre-turn realm authority from the accepted-turn snapshot, so manual same-turn rewrites of current realm cannot legalize `recordLifeCompletion` before runtime transition.
- trigger-aware current readers now require both manifest-backed pre-turn snapshot authority and a still-mortal current realm on the accepted turn. Orphaned `pending_turn_snapshot/...` copies without `pending_turn_snapshot.json` no longer authorize notifications, manifestation prep, bonuses, or other current-owner reads.
- trigger-aware current readers now require a **validated active** pending snapshot manifest, not merely a present `pending_turn_snapshot.json`: payload-hash mismatch, missing active request context, or tampered snapshot file hashes now fail closed instead of authorizing `recordLifeCompletion`-driven current reads.
- `LifeEvaluationRewardAnalyzer` no longer accepts permissive legacy `inkFeathers` / minimal relic parsing; both runtime reward materialization and validator reward checks now fail closed when pre-turn or current `soul_state` violates the canonical guardian-policy contract.
- `LifeEvaluationRewardAnalyzer` is now trigger-aware on the current/post soul-state side, so a valid current `recordLifeCompletion` on a canonical TriggerLifeEnd turn remains readable for reward-delta checks instead of suppressing the trigger-turn no-reward guard.
- canonical TriggerLifeEnd authority is now shared and strict across runtime/current readers: `reason` alone no longer authorizes `recordLifeCompletion`; `life_transitions.json` must carry a non-empty `summary` together with `reason = Death|Voluntary`.
- afterlife resident/reverse current-soul validation now reuses the shared policy-sensitive current `soul_state` gate, which preserves lifecycle-compatible keys like `crossIncarnationData` while fail-closing on malformed sibling roots/transients.
- rival bonus-clue current-incarnation resolution no longer pre-rejects lifecycle-compatible current `soul_state` through guardian-only top-level gating; `crossIncarnationData` and the rest of the lifecycle-allowed surface now flow through the same lifecycle-aware authority resolver as other strict current-soul readers.

### Subsystem 2: `GameEngine` runtime orchestration

- no reopened runtime caller migration is required;
- current runtime split remains valid and should stay untouched unless the preflight fix reveals a new caller misuse.
- life-evaluation reward presentation no longer falls back to permissive raw `soulRelics` parsing after strict delta computation fails; malformed current `soul_state` now produces a partial/no-relic summary instead of synthetic UI output.
- runtime `CheckLifeTransitions()` no longer silently no-op-ится on a canonical TriggerLifeEnd just because current runtime realm already drifted into Chaos Sea/Shining Abode. It now requires readable pre-turn mortal realm authority from the pending snapshot and fail-close-ится on same-turn manual realm switches instead of suppressing the life-end flow.
- runtime `CheckLifeTransitions()` now resolves TriggerLifeEnd authority through the same validated-active-snapshot helper used by current readers, instead of trusting raw `pending_turn_snapshot.json` realm data. Stale or inactive manifests no longer authorize runtime life-end orchestration.
- life-evaluation reward presentation now reuses the already-validated TriggerLifeEnd runtime context instead of re-deriving trigger awareness from raw `life_transitions.json` after the control file has already been consumed.

### Subsystem 3: validation/repair runtime flow

- no reopened validation/repair routing change is required on this pass;
- runtime inherits the normalizer fix once entrypoint atomicity is corrected.
- validator rival bonus clue checks now validate current-life applicability of completed `lore_research` source projects instead of trusting raw granted budget alone.
- TriggerLifeEnd reward validation no longer fail-open-ится на legitimate current `recordLifeCompletion`: trigger-turn reward guard now uses the same strict canonical trigger contract and still proves `delta = 0` instead of silently skipping the check.
- TriggerLifeEnd reward validation no longer fail-open-ится и на unreadable delta: if pre/post `soul_state` cannot be read on a canonical trigger turn, validator now raises an explicit trigger-turn unreadable-delta issue instead of suppressing premature reward checks entirely.
- TriggerLifeEnd reward validation now also uses lifecycle-authorized trigger authority instead of shape-only `life_transitions.json` plus raw rollback backups; realm-illegal same-turn afterlife rewrites and unusable pre-turn snapshot manifests no longer produce premature reward issues as if the trigger were valid.
- canonical TriggerLifeEnd authority is now validator-equivalent across runtime/current readers: non-string `reason` / `summary` values and extra visible keys on `life_transitions.json` no longer authorize trigger-aware reads.
- accepted-turn `recordLifeCompletion` validation now requires canonical TriggerLifeEnd authority, not merely the presence of a `TriggerLifeEnd` object shell.
- validator-side `recordLifeCompletion` guard now derives TriggerLifeEnd authority from the real `game_state/control/life_transitions.json` surface instead of searching for a phantom `TriggerLifeEnd` inside `game_state/meta/soul_state.json`, so legitimate accepted turns no longer raise a false missing-trigger issue.
- validator-side `recordLifeCompletion` guard now also requires mortal pre-turn realm authority, so a canonical-looking control file on an afterlife pre-turn no longer suppresses `life_transition_record_without_trigger_life_end`.
- validator-side `recordLifeCompletion` guard now also requires the same full lifecycle authority as runtime/current readers: validated active pre-turn snapshot plus current realm still mortal. Same-turn afterlife rewrites no longer legalize `recordLifeCompletion`.
- lifecycle validation now raises an explicit same-turn realm-drift issue when a canonical TriggerLifeEnd turn manually switches `soul_state.currentRealm` into Chaos Sea/Shining Abode before the client-side runtime transition.
- runtime `CheckLifeTransitions()` now uses the same shared strict canonical TriggerLifeEnd parser as validator/current readers; legacy nested `{"TriggerLifeEnd": {...}}` payloads, extra visible keys, and non-canonical control-file shells no longer start the life-end flow.
- invalid TriggerLifeEnd runtime context is now non-sticky: the client logs the failure, surfaces a console warning, and clears `game_state/control/life_transitions.json` instead of warning-looping on the same broken control file forever.
- pending archive consultation / project-fuel validation now treats malformed current `afterlifeArchive` as unreadable owner state and stops inferring reservations/receipts from synthetic normalized containers.
- archive notification read-model no longer normalizes malformed current `afterlifeArchive` owner state before receipt lookup; accepted archive notifications now require the same readable canonical owner state as runtime and validator closure paths.
- archive candidate manifest refresh no longer reparses permissive raw `soul_state` to derive source-life authority. Unreadable current owner state now leaves the existing manifest untouched with a warning, while readable canonical afterlife state still refreshes candidates and readable non-afterlife state still clears them.

### Subsystem 4: tests and guards

- completed
- regressions now pin the readable TriggerLifeEnd reward-delta happy path, the malformed-trigger-control fail-closed path, and the validator-level `life_trigger_turn_awarded_*` guard on premature trigger-turn rewards.
- regressions now also pin trigger-turn unreadable reward delta as an explicit validator failure and reject malformed TriggerLifeEnd payloads with non-string summary / extra visible keys.
- regressions now also pin the cross-file `recordLifeCompletion` guard against a separate canonical `life_transitions.json` and source-guard the runtime life-transition path against a return to the legacy nested TriggerLifeEnd parser.
- regressions now also pin lifecycle-aware TriggerLifeEnd realm authority for representative current readers, the explicit validator issue for same-turn realm drift, and the runtime trigger-context guard that rejects missing pre-turn mortal realm authority or already-afterlife current realm before life-end orchestration continues.
- regressions now also pin manifest-backed TriggerLifeEnd authority for current readers, reject orphaned snapshot copies without a live manifest, and block trigger-aware side effects when current `soul_state.currentRealm` was already rewritten into afterlife on the accepted turn.
- regressions now also pin validated-active manifest authority for TriggerLifeEnd readers: stale/inactive manifests no longer authorize current-reader bonuses or runtime trigger resolution, and source guards now require `CheckLifeTransitions()` to go through the shared validated snapshot resolver rather than raw snapshot realm reads.

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
