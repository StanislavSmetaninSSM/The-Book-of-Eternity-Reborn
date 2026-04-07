# Guardian Strict Authority Remediation Plan

## Статус документа

`IMPLEMENTED ARCHITECTURE`

Документ фиксирует достигнутый guardian authority contract и финальную converged architecture.

## Итоговая цель

Основной split уже закрыт между:

- proof-strict guardian/tracker authority для power-event-sensitive и journal proof paths;
- generic/shared strict guardian authority для shared policy readers;
- compatibility/raw snapshot surfaces, которые оставлены только для internal/debug/non-policy use-cases.

Итоговый контракт:

- policy-sensitive guardian/project/power-event validation больше не использует raw `PreTurnRoot`;
- compatibility `CurrentAuthorityRoot` больше не используется как validation truth;
- tracker truth для political validation/proof строится только поверх strict guardian authority;
- missing tracker/journal/snapshot provenance больше не превращается в usable fallback baseline в policy paths;
- current tracker authority и tracker drift validation доведены до fail-closed strict contract;
- normalizer, proof-local tracker snapshot validation и tracker issue taxonomy доведены до того же tracker authority contract.

## Final Source Of Truth

Для policy paths в коде действуют два strict канала current/pre-turn authority и один strict tracker channel.

### 1. Proof-Strict Guardian Authority

Используется для accepted-turn, journal proof и power-event-sensitive paths.

- pre-turn source: `PreTurnAuthorityRoot`
- current source: `StrictCurrentAuthorityRoot`
- gate: `HasResolvedStrictPreTurnGuardianAuthority(...)`

Этот канал может требовать proof-scoped snapshot provenance:

- validated snapshot journal
- validated snapshot tracker
- scoped raw `guardianPowerEvents`

### 2. Generic-Shared Strict Guardian Authority

Используется для shared policy readers и generic policy validation, где нужен canonical guardian baseline без proof-only assumptions.

- pre-turn source: `GenericSharedStrictPreTurnAuthorityRoot`
- current source: `CurrentAuthorityRoot`
- gate: `HasResolvedGenericSharedStrictPreTurnGuardianAuthority(...)`

Важно:

- `CurrentAuthorityRoot` в финальной архитектуре означает не compatibility truth, а generic-shared strict current authority;
- он materialize-ится только от `GenericSharedStrictPreTurnAuthorityRoot`;
- shared identity/project readers используют именно этот канал.

### 3. Strict Guardian-Backed Tracker Authority

Используется в political validation/proof.

- proof/power-event-sensitive political paths используют strict guardian-backed tracker projection;
- generic political validation использует shared-strict guardian-backed tracker projection;
- tracker projections, построенные поверх compatibility guardian roots, не являются validation truth.

## Compatibility Surfaces

Compatibility/raw surfaces сохранены только для:

- debug snapshots
- internal diagnostics
- non-policy/internal listing helpers

Нельзя использовать как validation truth:

- raw `PreTurnRoot`
- compatibility pre-turn guardian roots
- compatibility tracker projections
- any current guardian root built from compatibility pre-turn state

## Реализованные фазы

### Phase 1. Kernel Authority Split

В kernel разведены separate channels:

- proof-strict guardian authority
- generic-shared strict guardian authority
- compatibility authority

Policy-sensitive helpers больше не должны читать compatibility current/pre-turn roots как source of truth.

### Phase 2. Guardian-Aware Snapshot Tracker Dependency

Snapshot tracker dependency сделана fail-closed и guardian-aware:

- `NoTrackerDependency` допустим только когда irrelevance доказана;
- missing/malformed tracker surface-ится как snapshot authority failure, если tracker мог materially affect guardian baseline.

### Phase 3. Strict Tracker Projection For Political Validation

Political validation и political journal proof используют strict guardian-backed tracker truth вместо compatibility tracker authority.

### Phase 3.5. Generic Shared Strict Pre-Turn Authority

В kernel добавлен отдельный `GenericSharedStrictPreTurnAuthorityRoot` для shared policy readers.
Он не тянет proof-only requirements там, где validated snapshot guardians уже достаточен для:

- guardian ids
- guardian names
- relationship scores
- shared project identity lookups

### Phase 4. Shared Guardian/Project Readers

Shared policy readers migrated off raw fallback:

- pre-turn merge идёт через `GenericSharedStrictPreTurnAuthorityRoot`
- current merge идёт через generic-shared strict `CurrentAuthorityRoot`
- unresolved strict baseline surface-ится как authority failure, а не как ordinary missing guardian/project

### Phase 4.5. Shared-Strict Tracker Pre-Turn Authority And Proof-Local Context Split

Фаза закрыта.

Что сделано:

- убрать последний raw pre-turn tracker seam из shared project identity readers;
- развести proof-local snapshot contexts и generic-shared strict authority channel так, чтобы proof flows больше не spoof-или shared-strict root.

Результат:

- shared project identity readers не должны читать readable raw `trackerContext.PreTurnRoot` как validation truth;
- для них нужен отдельный shared-strict pre-turn tracker authority accessor, который разрешён только при:
  - resolved `GenericSharedStrictPreTurnAuthorityRoot`
  - usable validated pre-turn tracker snapshot
  - semantic-valid enough tracker baseline for shared project identity knowledge
- snapshot proof local context не должен помечать raw snapshot root как synthetic `GenericSharedStrictPreTurnAuthorityRoot`;
- если proof-local readable root нужен для snapshot command authorization, он должен идти через отдельный proof-local helper, не через shared-strict channel.

Критерий закрытия:

- `knownProjects`, `knownCompletedProjects`, `knownProjectDetails` и active-project map больше не materialize-ятся из raw `trackerContext.PreTurnRoot`;
- hash-valid, но semantically invalid pre-turn tracker snapshot не authorizes phantom shared project identity knowledge;
- accepted-turn snapshot proof продолжает проходить без reuse synthetic shared-strict root;
- shared readers и proof-local readers больше не делят один и тот же fake-resolved authority channel.

### Phase 5. Strict Current Tracker Authority And Failure Precedence

Фаза закрыта.

Что сделано:

- `TryResolveGuardianProjectTrackerValidationRootSync(...)` больше не строит current tracker authority из raw parseable `trackerContext.PreTurnRoot`;
- current tracker authority использует тот же shared-strict pre-turn tracker semantic gate, что уже применён к shared project identity readers;
- semantically invalid, но readable validated pre-turn tracker snapshot invalidates current tracker authority consumers и больше не продолжает authority build;
- tracker-dependent policy consumers в trade / afterlife archive / rival clue / gacha paths surface-ят explicit current tracker authority failure раньше business-rule ошибок вроде `guardian_project_update_unknown_project_id`, `guardian_project_completion_unknown_project_id` и `guardian_process_gacha_bonus_audit_forge_steps_exceeded`;
- current tracker authority consumers больше не обходят semantic gate через direct raw pre-turn tracker parse в policy paths.

Итог:

- semantically invalid validated pre-turn tracker snapshot не может кормить current tracker authority consumers;
- tracker authority failure surface-ится раньше ordinary unknown-project / missing-project validation errors;
- pre-turn shared tracker authority и current tracker authority используют один и тот же semantic baseline contract;
- questManagement current tracker authority требуется только для tracker-backed lore/archive quest provenance; cap/difficulty checks остаются guardian-derived и не over-require tracker;
- guardian-focused regressions и full suite остаются зелёными без возврата raw parseable tracker fallback в policy paths.

### Phase 6. Strict Current Tracker Input Semantics And Authority Drift Enforcement

Фаза закрыта.

Что сделано:

- `GuardianProjectTrackerPolicyContext` больше не считает current tracker usable только по parseable current `guardian_projects.json`;
- current tracker input проходит semantic gate before authority build;
- semantically invalid current tracker state invalidates current tracker authority even when validated pre-turn tracker baseline already resolved;
- current tracker authority build больше не продолжает materialization на semantically invalid current commands/entries;
- validator current tracker file больше не silently skips materialized-state authority comparison, если current tracker authority unavailable;
- tracker file validation surface-ит explicit current tracker authority failure раньше ordinary drift/content mismatches.

Итог:

- semantically invalid current tracker state не может кормить current tracker authority consumers;
- materialized-state validation для current tracker не silently skips, когда current tracker authority unavailable;
- authority-level issue surface-ится для invalid current tracker semantics раньше ordinary tracker content drift outcomes;
- guardian-focused regressions и full suite остаются зелёными без возврата parse-only current tracker gate в policy paths.

### Phase 7. Tracker Authority Convergence Across Validation, Proof, And Normalization

Фаза закрыта.

Что сделано:

- `CanonicalStateNormalizer` больше не использует raw current tracker arrays как authority input для `game_state/meta/guardian_projects.json`;
- proof-local tracker snapshot validation доведена до тех же semantic rules, что shared/current strict tracker authority;
- issue taxonomy разведена между truly missing validated tracker snapshot и semantic-invalid validated tracker authority baseline;
- runtime QTE normalization доведён до baseline-aware rollback contract с полным tracked write-set для guardian project journal и related normalizer writes;
- transient `game_state/control/qte_normalizer_backups/*` классифицирован как internal ephemeral/client-owned surface и не участвует в persistent save/load или validation truth.

Итог:

- normalizer rebuild-ит tracker state только из validated pre-turn baseline + same-turn commands и не сохраняет phantom current materialized entries/modifiers;
- proof-local tracker snapshot validation reject-ит unknown-guardian / duplicate / malformed authority-bearing modifier data на том же уровне строгости, что kernel strict authority path;
- shared guardian-project validation отличает `missing/unreadable validated tracker snapshot` от `semantic-invalid validated tracker authority`;
- QTE rollback восстанавливает guardian project journal и другие tracked normalizer writes before surfacing validation failure;
- temporary QTE normalizer backup artifacts удаляются после run completion и не считаются persistent state.

## Validation Rules To Preserve

При будущих изменениях нельзя ломать следующие правила:

1. Proof paths не должны деградировать до generic/shared strict authority, если path power-event-sensitive или journal-proof-sensitive.
2. Shared policy readers не должны деградировать до raw `PreTurnRoot` или compatibility current roots.
3. Political validation не должна читать tracker projections, построенные поверх compatibility guardian roots.
4. Missing snapshot tracker benign only when irrelevance to the target guardian is actually proven.
5. Compatibility helpers могут существовать, но они не должны влиять на validation outcomes.
6. Shared project identity readers не должны брать authority truth из raw readable pre-turn tracker snapshot.
7. Proof-local snapshot contexts не должны spoof-ить shared-strict authority channel.
8. Current tracker authority не должен строиться из raw parseable pre-turn tracker snapshot без shared-strict semantic gate.
9. Tracker authority failure не должен маскироваться ordinary `unknown_project_id` / `missing project` errors.
10. Current tracker authority не должен считаться usable только по parseable current tracker object без semantic gate.
11. Current tracker materialized-state validation не должен silently skip authority comparison, если current tracker authority unavailable.
12. `CanonicalStateNormalizer` не должен использовать raw current tracker materialized arrays как authority input для guardian project state.
13. Proof-local tracker snapshot validation не должна быть слабее shared/current strict tracker authority semantics.
14. Semantic-invalid validated tracker baseline не должен репортиться как просто `missing snapshot`.

## Regression Expectations

Изменения в guardian policy считаются безопасными только если сохраняются такие инварианты:

- accepted-turn `DONATE_TO_GUARDIAN`, `GUARDIAN_FAVOR`, `ABODE_OFFERING` и lifecycle `pending_abode_offering` используют strict snapshot-aware guardian baseline;
- current non-political journal proof использует `StrictCurrentAuthorityRoot`, не generic/shared current authority;
- generic shared identity/project readers не используют raw `PreTurnRoot` после strict baseline failure;
- generic political validation использует strict guardian-backed tracker authority;
- shared pre-turn project identity readers не используют raw readable `trackerContext.PreTurnRoot` как canonical truth;
- readable but semantically invalid pre-turn tracker snapshot не authorizes phantom project keys/details for shared validation;
- accepted-turn snapshot proof не зависит от synthetic `GenericSharedStrictPreTurnAuthorityRoot` flag на raw local snapshot context;
- current tracker authority consumers fail-close-ятся на semantically invalid validated pre-turn tracker snapshot;
- explicit tracker authority failure takes precedence over `guardian_project_update_unknown_project_id` / `guardian_project_completion_unknown_project_id` when tracker baseline is invalid;
- semantically invalid current tracker state не authorizes current tracker authority only because current tracker JSON is parseable;
- current tracker file validation does not silently skip authority comparison when current tracker authority is unavailable;
- normalizer does not preserve phantom current `activeProjects` / `completedProjects` / `temporaryProjectModifiers` solely because they already exist in the current tracker file;
- proof-local tracker snapshot validation rejects authority-bearing modifier/project data that shared/current strict tracker authority would reject;
- semantic-invalid validated pre-turn tracker baseline emits an authority-invalid tracker issue surface, not only `guardian_project_missing_validated_preturn_tracker_snapshot`;
- readable raw snapshot сам по себе не считается usable policy baseline, если strict authority unresolved;
- compatibility/debug helpers могут оставаться зелёными, но validation truth от них не зависит.

## Repo Note

Этот файл должен оставаться tracked design artifact и описывать фактическую архитектуру, а не старый незавершённый rollout plan.
