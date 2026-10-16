# FR063: lawful ACTIVE через фиксированные joint-tail фазы — вариант B

> **Current delegation from f87, 2026-10-16:** document-only source/dependency audit in the [implementation plan](lawful-active-joint-tail-b-implementation-plan.md#t179-pd-audit-sbm--static-dependency-boundary-at-f87), governed by [current authority](../plan.md). Windows permission is unanswered; failed hold delivery and technical reviews give no user permission. No runtime/CI/settings/transfer/release/catalog change or new QTE. Earlier continuation/release text is checkpoint history. This audit accepts no new evidence codec/issuer or full F1/T197; all D4/guided/DOM+visual/Windows/D8 limits remain.

> Current bounded3b source reviewed8e0/compiled a092. Sole Windows run38978917195 at release53a reportsJOBsuccess, but official artifact ZIP download is Forbidden; no raw Windows packet retrieved. Dependent EVIDENCE/final bounded qualification STOPPED/BLOCKED, no retry/alternate transfer/settings enablement. Prior13PASS+first2LinuxFAIL retained; no full3b/S/B/M3/F1/T197/new-QTE or prior-limit closure. See current plan.

> Ready3 causal RED is independently qualified atcc8 (2legacyPASS+solemissing-export RED228, no material finding); immutable review/root96pins/16projections/threeexactdiffs/6artifactchains/52document recovery are preserved. Continue the already authorized bounded3b engineering after decision persistence; no additional actual or GREEN is released. See current [plan](../plan.md).

> **Current execution authority, 2026-10-16:** new direct delegation resumes autonomous accepted bounded3b implementation from745b, superseding the completed discussion STOP. The [current plan entry](../plan.md) starts at saved FIRST Late2's separate EVIDENCE gate; Ready3 and causal TDD/implementation follow only their engineering gates. No new F1 redesign or full-S/M3/F1/T197/new-QTE release; all D4/guided/DOM/visual/Windows/D8 limits remain.

> **Completed DESIGN-only pass at745b:** documents/review/source recovery only; its STOP was followed for that pass. The corrected final review below closes its documentation finding and does not itself issue implementation/runtime authority. The new direct delegation above authorizes subsequent bounded work.

Current [corrected independent final DESIGN review](../recovery/lawful-active-f1-final-design-corrected-review-20261016.md) at709a closes the authorization-text finding F1-FINAL-D01; no new confirmed mechanical F1 defect was found. READY FOR USER DISCUSSION only: no implementation or actual-run release, full future F1/ordinary-owner/T197 qualification remains open.

**Статус:** PROPOSED DESIGN, подготовлен для обсуждения. Не accepted runtime contract,
не implementation plan и не разрешение менять production, тесты или полномочия записи.
**Задача:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), FR063/T070.
**Исходники:** `10611c69aa0d25683b9d74de04d1f96482f87d59`, ветка `1536-wound-b2-cloud-20261012`.
**Разрешение:** пользователь разрешил только подготовку B, сравнение с C/A и независимое DESIGN review;
перед реализацией требуется обсуждение и отдельное решение по этому документу.
**Доказательства:** все новые runtime/crash/platform проверки ниже **UNRUN**. Принятые 96 cases
относятся к readonly US5-RO2 на `737092c39`, а не к этому проекту ACTIVE.

**Уточнение 2026-10-16:** пользователь выбрал B для planning, затем разрешил DESIGN F1 и независимое review, без кода.
D4/D8 сохраняются. D5 пересмотрен в §8.5: подтверждённый ordinary/GM contract требует
не терять обязательное разрешение ситуации; новый follow-up schema/authority остаётся
отдельным предложением, не выданным grant. Исторический corrected DESIGN review a8479c86d
относится к прежним bytes; прежний D5 delta проверен с планом на f8ab919a. Первый F1 независимо проверен на 564424a9; его n+1/three-family policy superseded R2.
R2: одна игровая граница, полный lawful ordinary outcome; independent corrected review на bddf93041: READY FOR USER DISCUSSION. Все grants/implementation unaccepted.

## 1. Результат и границы

После genuine совместной публикации QTE-раны/guaranteed treatment клиент должен один раз
обработать законные `statsIncreased` и новое повышение уровня, получить согласованную
computed projection, записать story/turn и безопасно завершить либо передать управление
новому QTE. Crash/restart/repeat не начисляет характеристики/очки, не расходует ресурсы
и не публикует story повторно. Непонятное состояние удерживается с доказательствами.

B оставляет существующий common publisher и `PublishLocalFilesAsync`. Он добавляет
несколько фиксированных фаз **в существующем marker**, а не общий journal/service,
универсальный список разрешённых записей или новую storage architecture.
Не изменяются формулы wounds/resources/effects, afterlife, capabilities лечения и XP economy.
Наличие пути в guard inventory само по себе не даёт права писать/удалять/откатывать его.

Автоматическое начисление отделено от распределения игроком. Предложение B: allocation
отложено до terminal cleanup joint/QTE; очки остаются в canonical balance, выбор не теряется
и заранее не замораживается. Безопасность существующего обычного allocator после этой
границы — отдельная зависимость, а не автоматически решённая часть B (§10, D4).

## 2. Что установлено по исходникам

Все ссылки относятся к baseline 10611c69a; это source findings, не наблюдённые ACTIVE crashes.

| Источник | Установленное ограничение |
|---|---|
| [Accepted treatment contract](../contracts/mortal-wound-treatment.md), блок FR063 около строк 1092–1130 | Принят INACTIVE contour; progression владеет только schedule/report/marker. ACTIVE открыт. |
| [Independent assessment](../recovery/fr063-lawful-active-tail-architecture-assessment-72be660ca-20261015T094409Z-completed-v2-design-review.md) | B уже выделен как более узкое направление DESIGN; никакой grant там не принят. |
| [Inactive tail](../../../BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInactiveTail.cs) | Непустые stats, непокрытый level и новый offer/runtime запрещены; допускается terminal continuation другого предыдущего turn. |
| [TurnLifecycle](../../../BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs), `ProcessStatsIncreasedAsync`, `CheckLevelUpAsync` | Base stats пишутся до удаления `statsIncreased`; award points — до processed-level acknowledgement. Между записями возможен повтор. `compObj` для XP создаётся/логируется, но не записывается. |
| [CharacteristicsService](../../../BookOfEternityClient/Services/CharacteristicsService.cs), строки 150–235, 245–400 | Ordered stats, cap `level*2`, максимум 100; award 5/level; allocator пишет stats/points/projection отдельно. Initialization выдаёт 8 points и watermark 1. |
| [Derived owner](../../../BookOfEternityClient/Services/CharacteristicsService.MortalJointProjection.cs) | Реальный calculator под lease, deterministic frozen timestamp, точный readback; replay проверяет semantics без переписывания timestamp. |
| [Story/turn owner](../../../BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs), [cleanup](../../../BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointTerminalCleanup.cs) | Genuine original/G/union/phase/story proof; runtime только n−1 или n; текущий cleanup удаляет original/request/backups. Обычный ACTIVE tail не вызывается. |
| [QTE acceptance](../../../BookOfEternityClient/Services/QteSceneService.cs), `BeginAcceptedSceneCoreAsync` | Уже имеется atomic runtime+continuation+offer/Ready handoff, с удержанием original. Его нельзя просто вызвать из нового joint branch. |
| [QTE terminal](../../../BookOfEternityClient/Services/QteSceneService.cs), строки 1880–1985, 2284–2300, 2615–2725 | XP/state distribution, QTE history, runtime и terminal closure выполняются отдельными шагами. Catch/backup compensation не доказывает recovery после process death; cold replay XP требует отдельного action+ack. |
| [Continuation](../../../BookOfEternityClient/Services/QteDeferredEffectContinuation.cs), `CaptureAsync` | Любая существующая continuation запрещает новую; current pair/captured roots — источник authority. |
| [Ordinary story witness](../../../BookOfEternityClient/Services/StoryService.AcceptedStoryAppend.cs) | Schema 1 принимает только `console_turn`/`browser_turn`; настоящий joint row нельзя переименовать в них. |
| [Physical source](../../../BookOfEternityClient/Services/QtePhysicalInjurySource.cs) | Genuine official original/G/source-label capture нужен для новой physical-injury декларации; terminal historical source не даёт новое harmful occurrence. |

## 3. B, C и A

| Вариант | Предлагаемое изменение | Цена и предел |
|---|---|---|
| **B — рекомендуется для обсуждения** | Fixed phase state в joint marker; automatic action+ack; отдельные projection/story, QTE handoff и selected-terminal action+ack/cleanup. | Без нового pathname. Нужны явные schema/member/issuer grants и новый post-handoff proof; D8 затрагивает также terminal XP/history owner. Не чинит автоматически обычный allocator/все обычные turn owners. |
| C | Каждый stats/award/QTE owner получает durable per-operation receipt/idempotent action; joint делегирует им. | Лучше повторное использование обычными lifecycle callers, но шире shared schema, consumers, receipt retirement и regression cohort. Это тоже не общий journal. |
| A | Оставить принятые INACTIVE/refusal guards. | Новые grants не нужны; lawful ACTIVE и полный FR063 остаются открытыми. |

Рекомендация B — выбор направления проекта, **не пользовательское принятие** перечисленных ниже grants.
Если нужна немедленная безопасная интерактивная allocation во всех callers, C или отдельный
owner-specific allocation проект могут оказаться предпочтительнее; B не скрывает эту цену.

## 4. Предлагаемые пути, операции и владельцы

Обозначение **M** = `game_state/wounds/mortal_qte_decision_publication.json`.
Все строки таблицы являются **proposed grants**, сейчас не действуют.
Unchanged members остаются точными guards; actual write set — ровно byte-different subset
фиксированного data cohort плюс M, а не произвольное «не больше N файлов».
Каждый guard проверяется до/после boundary awaits и в publisher callback.
Таблица fixed P/D8 не выдаёт full ordinary grants: D9–D11/R2 задают отдельные owners и точные
планы всех применимых ordinary families; no new generic storage service.

| Фаза | Точный data cohort; M участвует в том же batch | Proposed writer / ограничения |
|---|---|---|
| COMMON | Существующий accepted common set; новый M schema 3 | Только taken joint common publisher. Старые membership/rollback права не расширяются этой строкой. |
| PROGRESSION | `game_state/control/progression_schedule.json`, `game_state/control/progression_report.json`, M | Существующий progression owner; report consumption только по его frozen calculation. Сохраняется собственная трёхчленная фаза. |
| **AUTOMATIC** | `game_state/misc/characteristics.json`, `game_state/player/status_changes.json`, `game_state/player/stat_points.json`, M | Новый узкий joint automatic publisher; `CharacteristicsService` поставляет чистый расчёт. Никаких `experience.json`, item/resource/effect writers. |
| **PROJECTION** | `game_state/player/computed_characteristics.json`, M | Существующий named derived owner, связанный с applied automatic capability. Не вызывает normal refresh/repair или `AddStatPoints`. |
| **STORY** | `stories/mortal_life_{i}.jsonl`, i=original incarnation>0, иначе existing fallback1; единственный `StoryService.GetStoryPath`, M | Named joint story writer. Только точный signed prefix + один deterministic row; `chat_log.json` не новый writer. |
| **QTE ACCEPT** | `game_state/control/qte_runtime.json`, `game_state/control/qte_deferred_effect_continuation.json`, `output/qte_offer.json`, `ready/turn_complete.json`, `ready/turn_error.json`, M | Named QTE handoff publisher. runtime/continuation устанавливаются вместе, offer/Ready становятся absent. `turn_error` должен быть absent, а не стёрт как конфликт. |
| **QTE DECLINE base (недостаточен для revised D5)** | `game_state/control/qte_runtime.json`, `output/qte_offer.json`, `ready/turn_complete.json`, `ready/turn_error.json`, M | Устанавливает existing `lastDeclinedQteId/lastDeclinedAtTurn`, обнуляет pending/active только при доказанной пустоте исходного runtime, consumes только свои offer/Ready. Prior terminal continuation остаётся unchanged. FD добавляет в тот же M pending F1 obligation/source capsule (§8.5); base receipt без F1 obligation/FT/actual ordinary completion недостаточен для D5. Все grants proposed. |
| **QTE TERMINAL PREPARE** | M | Named QTE selected-terminal owner фиксирует только уже известный selected outcome и exact terminal before/after plan (§8.6); не будущий выбор в P0. |
| **QTE TERMINAL COMMIT** | Existing genuine deferred plan owned data set + `game_state/player/experience.json`, `game_state/player/status_changes.json`, `game_state/history/qte_history.json`, `game_state/control/qte_runtime.json`, `game_state/control/qte_deferred_effect_continuation.json`, `output/narrative_response.json`, три exact QTE wave controls из guard table, M | Proposed selected-terminal publisher: complete outcome action+terminal ack, без прямого ordinary distributor/normalizer writer. Новый source amount не выдаётся, earned XP берётся только из выбранного authored outcome. Не XP compensation. D8. |
| **QTE AUTOMATIC / PROJECTION** | Тот же four-member AUTOMATIC и two-member PROJECTION, отдельные phase IDs | Обрабатывает только новые stats/level после genuine QTE terminal, с новым terminal-bound frozen basis; P2 никогда не переигрывается. |
| **F1 TRANSFER / CHILD (D9–D11)** | Existing original/snapshot/request inventories + M; complete typed ordinary owner result cohort (exact applicable data/companions), child named progression/automatic/projection/story; R2 §§4–7 | Actual current domain owners with complete taken composite and same-action event/time proof; parent artifacts retained, no recursive originalAction or FileMapping allowlist. New grants proposed, not accepted. |
| **CLEANUP** | Только K (для F1 — KF1 §8.5) плюс M | Existing terminal cleanup owner с новым typed terminal capability и atomic release ack. Не обычный cleanup helper по флагу/голому списку из JSON. |

### K: замкнутая cleanup membership

K вычисляется из **проверенного signed original payload**, с теми же path validators:

- все значения `payload.Files` (только `IsValidatedPendingSnapshotArtifactPath`);
- все значения `payload.RollbackBackups` (только `IsValidatedRollbackBackupArtifactPath`,
  исключая unsupported Explorer rollback artifacts);
- только те ключи `payload.Files`, которые признаёт `ConsoleLocalTurnRollbackArtifacts.IsMarker`;
- `game_state/control/pending_turn_snapshot.json`;
- `game_state/control/pending_turn_snapshot.authority.json`;
- `input/turn_request.json`;
- `ready/turn_complete.json` (может уже быть consumed в proved QTE phase);
- `output/mortal_wound_decisions.json` (absent только по прежнему authenticated output-binding);
- `game_state/control/validation_repair_request.json`;
- `game_state/control/validation_repair_ready.json`.

Это конечный набор текущего signed оригинала, **не glob по директории**. Указанные три
optional controls/draft сохраняют прежние absent правила; consumed Ready становится optional
только по новой конкретной QTE accept/decline receipt. Прочий отсутствующий member — конфликт.
`ready/turn_error.json`, stall report и terminal protocol failure — negative guards,
не разрешение стирать чужую ошибку. M никогда не входит в K и сохраняет release receipt.
Никаких новых deletions canonical mechanics, QTE history, current continuation, story,
computed projection, outputs narrative/interface/debug, generation metadata или signing material.

### Read/guard inventory; исключения только в named фазах

`game_state/player/experience.json` (кроме separately proposed D8 earned-QTE-XP и D10/D11 genuine ordinary XP owners; никогда compensation), `game_state/core/player_status.json`,
`game_state/meta/soul_state.json`, `game_state/world/world_time.json` (F1/D9 exact actual remaining-time acceptance §8.5.3 / R2),
`game_state/control/life_transitions.json`, `game_state/inventory/items.json`,
`game_state/player/skills_active.json`, `game_state/player/skills_passive.json`,
`game_state/npcs/npc_core.json`, accepted effect carrier/identity/source inventories,
actual original headers/artifacts, current generation storage admission, story prefix,
`game_state/history/qte_history.json` (только D8 разрешает terminal append),
`input/qte_effect_resolution_request.json`, `output/qte_effect_resolution_receipts.json`,
`ready/qte_effect_resolution_complete.json` (только D8 разрешает consumption этих трёх controls).

Также exact guard-only reads: `output/narrative_response.json`, `output/interface_updates.json`,
`output/debug_logs.json`, `game_state/combat/combat_log.json`,
`game_state/world/current_location.json`, `game_state/meta/guardians.json`,
`game_state/meta/guardian_abode_residents.json`. Последний — именно conditional resident source
`ReadMortalJointStoryRefsAsync`, не похожий pathname другого afterlife каталога. Для fixed P
все эти paths захватываются с present/absent images до common; использование entity refs
выводится из signed manifest.PlayerAction. Это не write/delete/rollback grants. Только
terminal narrative получает отдельный D8 writer; interface/debug/combat остаются unchanged
на accepted-QTE branch, а F1 имеет complete named ordinary owner overlays §8.5.3 / R2; combat/XP/location/other paths
меняются только при genuine typed contribution, otherwise guard-only. Это proposed D10/D11, не D8 grant. Live narrative до D8 terminal либо F1 submitted-result admission совпадает с initial image.

Полный read set P — union этих literals, existing `JointFootprintPaths`,
`QteDeferredEffectContinuation.SealedRootPaths`, при physical injury — `QtePhysicalInjurySource.WoundRootPaths`,
и всех реальных source reads named derived calculator на baseline106. Списки берутся из
фиксированных production inventories, а не из ключей caller marker; при реализации
обязательна exact inventory/source-guard reconciliation. Новое read dependency без записи
в frozen capture — fail-closed, не lazy permissive reread.
Existing QTE outcome/wave writers по их ранее принятым grants могут менять свои roots
после ACCEPT; этот документ **не расширяет** их set/формулы и не даёт joint автоматике эти права.

## 5. Immutable basis и genuine authority

### 5.1. Захват до common take

Новый узкий `JointActiveTailBasis` предлагает выдавать только реальному engine capture owner
на текущем canonical lease после complete original/G, accepted response/normalization,
canonical/narrative и common mechanical admission. Private issuer; не DTO constructor.
До take common plan получает detached immutable basis; COMMON атомарно сохраняет его в M3.
Crash до common не оставляет accepted tail: исходники остаются, можно заново пройти admission.

В basis входят:

1. Original tuple `{generation, sessionId, requestId, snapshotToken, turn, realm, incarnation}`,
   оба physical original header и validated signed manifest/history/backup bindings.
   G читается из текущего storage owner; никакого `GetOrCreate`/Load-created G для recovery.
2. Раздельные origin roles: signed before-turn roots; фактически validated submitted roots
   после обычного distribution/normalization перед common; запланированные common after-images.
   `status_changes.statsIncreased`, levels и offer — current submitted data, не выдавать их за original.
   Реальные validated response fields/Ready/narrative/output binding и их точные bytes должны
   быть связаны с current accepted frame. Hash/readability сами это не доказывают.
3. Exact present/absent before-images трёх automatic data roots, computed before-image,
   ordered stats input, оба level resolver inputs, processed watermark/unspent balance,
   весь P read set и разрешённые common transforms. Raw narrative уже связан existing
   six-field outputBinding; frozen capture не заменяет этот authenticated seal.
   Все inputs story composition фиксируются с их source role; deterministic P4 row можно
   заново вывести после terminal без чтения нового narrative (§8.7). Common не может одновременно писать
   один из трёх automatic roots: такое пересечение владельцев отклоняется до take.
4. Полный ordinary progression frozen basis сохраняется отдельно без изменения его semantics.
5. Raw offer bytes либо genuine absence; validated complete offer с source turn n (исходный
   `sourceTurnNumber` только absent или n), qteId, original/G/source label и declared branches.
   Bound offer строится в памяти; до выбора не вызывать отдельный disk-writing BindOffer.
6. Prior terminal continuation descriptor и его actual byte digest; old continuation/runtime/history
   и story prefix проверяются как в §8.4. До ACCEPT actual old file остаётся для fresh проверки.
7. R2 actionBoundarySource (binding/actual source roster/closure inputs/prefix time inputs/encoder)
   captured in S and raw B before CommonAuthority/input/prepared/cache seals; actual C provides
   planned event/closure results. Immutable source enters basisFingerprint, mutable sibling acks
   excluded. Exact cold reconstruction/DAG in R2 §2; reply-dependent original absence in R2 §4.
8. Fixed encoder/version и captured `preparedAtUtc`; все after-image bytes/serialization,
   UTC и arithmetic policy воспроизводимы. После crash не подставлять новый UTC.

Ни player allocations, ни accept/decline, ни неизвестный будущий QTE action/grade/outcome
в этот момент не выбираются и не замораживаются. Declared возможные branches — ещё не факт исхода.

### 5.2. Новые capability boundaries (предлагаются)

- `JointActiveTailCapability`: current physical original/G + complete phase-aware common proof
  + frozen basis + exact before/current images; даёт только следующую named phase.
- `JointQteHandoffCapability`: additionally applied automatic/projection/story, exact current offer/Ready,
  known submitted UI choice и genuine prior terminal retirement proof.
- `JointQteSelectedTerminalCapability`: actual persisted selected outcome/resolved wave + genuine
  joint transfer lineage/current original/G; только terminal PREPARE и exact COMMIT cohort D8.
- `JointQteTerminalTailCapability`: genuine committed terminal proof, actual lawful terminal
  stats/levels/balance и fixed terminal basis; только P7b automatic/projection.
- `JointQteTerminalCapability`: actual runtime/continuation/history/wave terminal closure,
  terminal COMMIT и P7b receipts + joint transfer lineage + retained original/G; только cleanup.
- `PriorQteTerminalRetirementProof`: historical terminal validation; не active QTE permission,
  не новый harmful occurrence и не право восстановить старую continuation.

Все constructors private/named issuer; capabilities привязаны к конкретным filesystem owner,
lease, G, original tuple, phase и bytes. Перед каждым commit owner заново экспортирует proof.
Cold читатель получает их только от реальных canonical readers; parsed marker, copied key,
rehash, diagnostic JSON, old process object или сохранённая capability authority не заменяют.
Session replacement пробрасывается, не преобразуется в retry/compensation.

## 6. Proposed schema и отсутствие циклических seals

### 6.1. Marker M schema 3

Root — **ровно текущие JointFields v2 плюс `activeTail`**, `schemaVersion=3`, отдельный
fingerprint domain `T070-MORTAL-QTE-JOINT-COMMON-PUBLICATION-V3\n`.
Existing `jointTreatment`, six-field `outputBinding`, consumed command/private-path rules,
receipts/mechanical reconstruction и progression basis сохраняются. V2 не становится ACTIVE.

Closed `activeTail` fields:
`version`, `policy`, `basis`, `basisFingerprint`, `automatic`, `projection`, `story`, `qte`, `cleanup`, `actionBoundary`.
`version=2`, `policy="fixed_tail_b_v2"`; дополнительно closed `actionBoundary` по R2 §2, captured before P0.

- `basis`: closed `{binding, preparedAtUtc, inputImages, sourceRoles, automaticPlan,
  storyPlan, offer, priorTerminal, projectionReadPaths, encoderVersion, actionBoundarySource, admissionEvidence}`.
- **T179-Pd refinement, proposed/independent review pending:** first implemented v3 basis layout uses integer `encoderVersion=1` and requires the closed `admissionEvidence` slot; old closed v2 and already-issued originals remain exact. No parser fallback from a missing slot or an earlier proposed layout. This is an explicit design amendment, not an existing runtime field or implementation acceptance.
- `admissionEvidence`: closed `{version,policyVersion,sourceEncoderVersion,binding,origin,route,sources,observations,finalOrdinal}`; integer `version=1`, `sourceEncoderVersion=1` names the exact qualified finite source/route policy. Binding is `{generation,sessionId,requestId,snapshotToken,turn,realm,originalManifestHash,originalAuthorityHash}`. Origin is `{kind,sourceLabel,originalRequestTimestamp}`, kind exactly `issued_original`. Ordered route/source/observation rows are strict tagged unions issued by the real original/repair/read owners, as specified in the [location correction](../recovery/t179pd-admission-history-location-design-20261016.md) and its [semantic/provenance consultation](../recovery/t179pd-admission-cold-proof-design-20261016.md); exact row enums/field codecs and complete transition checkers remain engineering prerequisites before an issuer exists. No saved verdict, caller map/time delegate, imported issuer or unknown-field acceptance. The initial private seed must originate at actual original issuance, not later parsing of an existing manifest/Ready.
- Owner freezes admission history and exact source observations before C/raw B/common seals. This same immutable evidence belongs to genuine S, same retained C linkage, CommonAuthority/input/prepared/cache/take and immutable basis/common proof; no after-seal attachment or side M write. Actual common publisher serializes it into the existing M3/native cohort. Cold parses DATA, independently checks actual original/G/cohort/source dispositions and shared policy/owner semantics, and uses only the named local owner's recorded historical samples for intrinsically irrecoverable observations. Selfhash alone never grants acceptance. No new durable filename or cryptographic credential is needed.
- Pre-P0 loss of genuine hot history gives internal HOLD/no P0 claim for this bounded unactivated contour; it does not qualify complete crash recovery or become final lawful-outcome refusal. Full F1 activation requires a separately complete qualified pre-P0 recovery route using actual existing owners/versioned controls. Observable projection equivalence, worker-direct submission closure and complete cold C/lifecycle/resource/wound checkers remain required. Missing proof blocks M3 emission. Historical repair UTC/mtime does not become action elapsed time or an extra tick.
- `binding` — ровно tuple из §5.1. `inputImages` — exact production-derived finite path map
  P **без M, runtime/offer/prior-continuation control trio**; offer.rawImage хранится отдельно,
  а control digests/retirement descriptor проверяются fresh из actual controls до handoff;
  каждый image `{exists, bytesBase64, digest}`, null bytes **iff** absent. Digest absent=`"absent"`,
  present=uppercase64hex SHA256 exactbytes, как existing publication Digest; base64 canonical. Strict UTF8/BOM,
  duplicate keys/confusable aliases/extra fields запрещены. Для старой continuation
  хранится compact descriptor/digest, не recursively embedded predecessor bytes (§8.4).
- `sourceRoles` отделяет coordinates: closed enums `signed_original|submitted_current|common_after|guard_current`;
  каждый role/path выводится только из действительного source owner, не caller-selected authority paths.
  Map имеет ровно inputImages keys и один role на captured coordinate каждого path. Signed-original
  image проверяется по physical signed original; submitted-current — по admitted frame до common;
  common-after — по named common contribution after-image; guard-current — exact unaffected bytes.
  Другие coordinates того же path берутся только из genuine original/owner plans и phase overlays,
  а не подменяют captured image. Поздние auto/terminal transforms не меняют P0 sourceRoles.
- `automaticPlan`: `{trainingLevel, awardLevel, processedBefore, unspentBefore,
  orderedStats, appliedStats, blockedStats, ignoredStats, pointsAwarded,
  automaticAfterImages, mutationFingerprint}`. Exact three after-images; plan заново вычисляется.
- `offer`: null либо `{rawImage, boundOffer, qteId, offerFingerprint, sourceTurn}`.
- `storyPlan`: `{path,preparedAtUtc,rowBase64,rowDigest,originalHistoryBindings,readPaths}`;
  path — единственный GetStoryPath (§4), row — exact serialized P4 row без newline, UTC — basis UTC.
  History bindings сверяются с genuine signed manifest; readPaths — fixed actual story reads
  включая location/soul/narrative и guardian/resident/NPC roots. Named owner заново строит row
  из authenticated initial inputs; сохранённые row/hash сами story proof не выдают.
- `priorTerminal`: null либо compact terminal identity/closure descriptor §8.4, не snapshot старого marker.
- `automatic`, `projection`, `story`: `{state, receipt}`, state `pending|applied`;
  pending имеет null receipt, applied — closed owner receipt.
- `qte`: `{state, choice, decisionReceipt, transfer, terminal, followUp}`; state `none|offered|active|declined|terminal`.
  `choice` null до действия, иначе closed `{decision,generation,sessionId,requestId,snapshotToken,turn,qteId,offerFingerprint,basisFingerprint}`; decision=`accept|decline`.
  `decisionReceipt` — null iff none/offered, иначе required closed phase receipt ниже:
  qte_accept maps имеют ровно runtime/continuation/offer/turn_complete/turn_error data keys §4;
  qte_decline — ровно runtime/offer/turn_complete/turn_error (continuation guard unchanged).
  M исключён из maps. Choice и receipt публикуются одной decision transaction с action;
  phaseId/operationId, tuple, basis, before/after digests обязаны согласоваться. Receipt
  сохраняется unchanged при active→terminal→released, доказывает только собственный consumed Ready.
  `followUp` — только declined F1 (§8.5), иначе null. `transfer` только для accepted handoff. `terminal` null до реального selected-terminal capture,
  затем `{basis,receipt,automatic,projection}`: receipt=null до terminal COMMIT;
  `automatic` — `{basis,state,receipt}`, `projection` — `{state,receipt}`.
  Nested state=`pending|applied`; pending receipt=null. Automatic basis=null до отдельного
  post-terminal PREPARE, applied требует non-null immutable basis. Они не содержат неизвестный выбор.
- `cleanup`: `{state, reason, receipt}`, state `retained|released`;
  reason null до release, затем `no_qte|declined|qte_terminal`.

Receipt формы: `{phaseId, operationId, basisFingerprint, previousMarkerDigest,
memberBeforeDigests, memberAfterDigests, resultFingerprint}`.
Member maps содержат **точный data cohort без M**: нельзя помещать hash marker-after
внутрь самого marker. `previousMarkerDigest` — прошлый marker, без self-reference.
`operationId` privately deterministic из G/original tuple/basis/phase ordinal.
Root fingerprint покрывает marker; стабильный `basisFingerprint` покрывает canonical basis
и immutable common proof core (including immutable actual action/closure/time plan results; progression calculation **без mutable phase**, existing immutable
receipt/output fields), но исключает сам `basisFingerprint`, root fingerprint и phase receipts.
Хеши — corruption/correlation checks; права выдаёт §5.2, а не совпавшая строка.

Closed `terminal.basis`:
`{binding,transferId,selectedTerminalBinding,resolvedWaveBinding,preparedAtUtc,
sourceReadImages,memberPaths,memberBeforeImages,memberAfterImages,planInputFingerprint,
terminalResultFingerprint}`. Fingerprint этого basis имеет отдельный domain
`T070-MORTAL-QTE-JOINT-SELECTED-TERMINAL-V1\n`; qte_terminal receipt связывает его.
`selectedTerminalBinding`/nullable `resolvedWaveBinding` — exact
existing typed-owner schemas, не arbitrary payload. `sourceReadImages` — полный fixed read set
terminal calculator/normalizer/selected plan с теми же image правилами; M исключён. `memberPaths`
выводит named owner из принятого D8 subset и existing typed contributions (§8.6), не caller.
Before/after maps имеют ровно эти data keys, включая present/absent wave controls. Внутри maps
нет M; current continuation может содержать только текущий joint capsule, без predecessor blobs.

Closed `terminal.automatic.basis`:
`{binding,transferId,terminalResultFingerprint,preparedAtUtc,inputImages,sourceRoles,
automaticPlan,projectionReadPaths,encoderVersion}`. Это тот же strict P2 arithmetic/image policy,
но actual inputs после terminal COMMIT; P2 receipt/watermark проверяются, не служат новыми inputs.
Его fingerprint вычисляется из всего basis с отдельным domain
`T070-MORTAL-QTE-JOINT-TERMINAL-TAIL-V1\n`; phase receipt `basisFingerprint` у P7b связывает именно его,
а `terminalResultFingerprint` связывает terminal outcome. Initial P2/P3/P4/P6 receipts связывают
immutable activeTail.basis. Phase IDs закрыты: `progression|automatic|projection|story|qte_accept|
qte_decline|f1_transfer|f1_mechanics|f1_progression|f1_automatic|f1_projection|f1_story|f1_repair|f1_complete|qte_terminal|qte_automatic|qte_projection|cleanup`; PREPARE не выдаёт applied receipt.
Один deterministic operationId на named phase, а не общий ID всех transactions turn.

Фазовые invariants: automatic applied требует progression applied; projection applied требует
active automatic applied; story applied требует projection applied. Offered в P0 означает только
наличие declared offer: UI допускается лишь после story applied. Active/declined требует
P1–P4 и choice; terminal требует actual terminal COMMIT receipt. Released требует всех
предшествующих фаз, для declined ещё F1 completionReceipt, а после QTE ещё terminal automatic/projection applied. Нарушенные
combinations не исправляются default значениями.
None/offered требуют null choice/decisionReceipt/transfer/terminal/followUp; none также null offer.
Active требует accept choice/receipt, transfer и null followUp; terminal PREPARE допускает non-null terminal
с null terminal receipt при active. Declined требует decline choice/receipt, non-null followUp и null transfer/terminal.
Terminal требует сохранённые accept choice/decisionReceipt/transfer, committed terminal receipt и null followUp.
Cleanup reason согласуется с none/declined/terminal; released не удаляет decisionReceipt.
Declined/decisionReceipt сами не дают cleanup authority: revised D5 требует accepted genuine
ordinary-resolution/transfer proof, proposed F1 schema задана в §8.5; до её принятия/реализации new QTE admission blocked.

### 6.2. Continuation schema 2 для joint source

Только новый joint ACCEPT выпускает новую закрытую continuation family:
existing continuation schema-1 fields + `sourceKind="mortal_joint_turn"` и `jointTransfer`,
`schemaVersion=2`. Ordinary schema 1 не переименовывается/не ослабляется.
Runtime использует **существующие** deferred continuation ID/fingerprint links; новых runtime
полей для generic authority не требуется. Unknown schema/family смешение отвергается.

`jointTransfer` — closed `{version, transferId, basisFingerprint, originalBinding,
commonProofCore, automaticReceipt, progressionReceipt, projectionReceipt,
storyWitness, handoffRootPaths, retirementReceipt}`.
Raw handoff baseline bytes берутся из **единственного** actual `sealedRootBindings` capture,
включающего fixed joint mechanical cohort даже у offer без physical injury. Не дублировать
внутри него entire old marker/continuation. Map keys derive из source catalogs, не JSON authority.

`clientStoryAppend` получает отдельную closed schema-2 joint writer variant
`writerKind="mortal_joint_turn"`: existing story fields + `jointBinding`
`{basisFingerprint, transferId}`. Её выпускает genuine `MortalJointStoryProof` на handoff boundary.
Существующая schema-1 проверка `console_turn|browser_turn` остаётся точной.
Одна строка story не выдаёт witness; rehydrate требует actual canonical QTE pair,
retained original/G, exact prefix+row и accepted joint transfer proof.

Acyclic order: frozen joint basis → known chosen transferId → story witness/captured roots
→ new continuation authority fingerprint → runtime links + M accept receipt.
Continuation не содержит fingerprint итогового M, который сам содержит её fingerprint.
Physical-injury source остаётся genuine existing source capture; новая accepted injury
наступает только из нового законного QTE terminal selection, не из retired receipt.

## 7. Fixed phases и automatic action+ack

| Фаза / before | Один outcome и after | Cold/repeat |
|---|---|---|
| P0 COMMON: approved pre-common state | Common mechanics + M3 с immutable basis; progression unapplied, automatic/projection/story pending; qte none/offered; cleanup retained | Fresh complete common proof; consumed public draft не требуется снова, present draft обязан совпасть с authenticated payload. |
| P1 PROGRESSION: existing unapplied | Existing schedule/report/M applied batch | Existing exact frozen reconstruction/three-member compensation; не grants stats. |
| P2 AUTOMATIC: progression applied + exact three data before-images | Stats changes + consume field + stat award/watermark + M automatic receipt **одним** batch | Applied требует все three after-images/receipt; повтор ничего не прибавляет. Marker pending + nonidentical data-after без resolved publisher outcome — HOLD. |
| P3 PROJECTION: automatic applied | Actual calculator после stats/award, computed bytes + M projection receipt | Recompute every source, same frozen UTC; read-only validation after applied, не rewrite timestamp. |
| P4 STORY: projection applied | Exact story prefix+row + M story receipt atomically | n−1→n in-memory reconcile только после durable story receipt; n stays n, чужой turn HOLD. |
| P5 NONE / OFFERED | None идёт к cleanup. Offered освобождает lease и показывает current tuple-bound UI | Crash до choice снова показывает тот же offer; потерянный неcommitted click не подменяется выбором. |
| P6 ACCEPT / DECLINE | ACCEPT cohort §4 + M ack; DECLINE base недостаточен без отдельно принятого Q-D5 handoff | Нового award/story/common take нет. Duplicate decision сохраняет/resumes то же mandatory ordinary obligation, не объявляет его resolved. Conflicting choice HOLD. |
| P7a QTE TERMINAL PREPARE/COMMIT | Selected-terminal basis фиксируется в M; terminal outcome/history/runtime/continuation + M ack публикуются одним exact cohort | Already committed не вызывает повтор XP/distribution/history. Paused/wave debt не terminal. |
| P7b QTE AUTOMATIC/PROJECTION | Один дополнительный AUTOMATIC slot по terminal-known basis, затем актуальная computed projection с отдельным receipt | Только новые QTE-earned stats/level, watermark уже включает P2; никакого второго P2/story/turn. |
| P8 CLEANUP | K (F1: KF1 после actual ordinary completion) deletions + M released ack atomically | Partial cleanup/debt не completion. Released observation даёт **никакой** новой mutation/replay authority. |

P2 intentionally объединяет stats и award в один four-member logical cohort. Нет окна
«stat +1, command ещё не consumed» или «+5 points, watermark старый».
Начисление переносится перед QTE prompt относительно ordinary пути; это часть D3. Оно не
выбирает будущий outcome. P7b обрабатывает только возникшие позже QTE stats/level и не повторяет P2.
P1/P3/P4 — отдельные уже понятные владельцы, не повод объединять всё хранилище в transaction.
В v3 `publishedFootprint` остаётся initial common evidence. Current expectation — точные
phase overlays: progression after, automatic after (3 paths), computed after, story suffix,
позже только authenticated QTE chain/terminal after. Никакой обход целого guard inventory:
любая другая разница отвергается. До handoff projection source reads должны совпадать с
frozen submitted/common-planned/automatic image соответствующей роли.
P7a/P7b известны как фиксированные слоты, но их outcome inputs появляются только после
реального terminal selection; неизвестный QTE результат не замораживается до common.

### P2 arithmetic и initialization

- Сохранять ordered stats processing, trim/lower и existing valid-name policy, **не превращать
  array в set**. Duplicate stat применяется последовательно до cap/100. Malformed input
  отвергается admission; неизвестные допустимые по существующему validator строки
  учитываются как ignored, не становятся характеристиками.
- `trainingLevel` повторяет `CharacteristicsService.GetPlayerLevel`: experience.level,
  затем experience.playerLevel; иначе player_status.level, soul_state.level, baseline 1.
  `awardLevel` повторяет CheckLevelUp: experience level/playerLevel, но при результате ≤1
  берётся player_status.level; иначе 1, **без soul fallback**. Не молча объединять эти resolver.
  Valid divergent sources должны быть отражены в plan/test; изменение их приоритета — отдельное решение.
- `pointsAwarded=5*max(0, awardLevel-max(1, processedBefore))`;
  watermark становится `max(processedBefore, awardLevel)` при positive award, не понижается.
  unspent и арифметика checked; overflow/invalid levels/negative balance — отказ **до common**.
- B требует existing valid integer watermark ≥1 и existing nonnegative integer balance,
  полные canonical base stats. Не использовать `_lastKnownLevel` как cold receipt и не делать
  implicit init из computedLevel/unspent heuristic. Отсутствие initializer evidence — отказ до common.
  Existing new-incarnation initializer остаётся отдельным owner до original; 8 initial points
  и watermark 1 не выдаются этой фазой. Это сохраняет уже строгую joint admission, но выбор D2 явный.
- При processedBefore≥awardLevel award=0; не обнулять balance/watermark. Empty/absent stats
  остаётся как в обычном no-work case; непустой обработанный array удаляется только как поле,
  остальные status_changes siblings сохраняются. Stat_points siblings сохраняются.
- Base stat after-image имеет ровно canonical Characteristics.All (12 полей baseline106),
  policy/serializer фиксированы. Present invalid roots не лечатся defaults.
- **XP compensation writer отсутствует.** Blocked result можно показать; `experience.json`
  guard-only. Никаких подразумеваемых +XP из комментария/compObj.
- Named projection выполняется **после award**, поэтому содержит окончательный unspent balance.
  Отличие от промежуточной ordinary projection намеренное, требует D3; формулы calculator прежние.

## 8. Безопасный QTE handoff и retention

### 8.1. Offer и UI

Complete validated offer фиксируется до common, а его принятие — только после P4.
До prompt lease освобождается. UI token связывает G/original/turn/qteId/offer/basis/expected phase;
он не является storage authority. После выбора reacquire, fresh genuine proofs и сверка
того же offer/phase. Session replaced, changed/stale/foreign offer, другой original или
подменённый token запрещают действие. Browser reload и console restart видят одинаковый
canonical offered/active/declined status. Остальной обычный ввод/allocation временно не допускается,
readonly status разрешён. Никакого ожидания пользовательского ответа под canonical lease.

### 8.2. ACCEPT

QTE owner заново validates original/G/common+P1/P2/P3/P4/Ready/output binding, prior terminal
и все capture roots. Создаёт closed joint story witness, actual current effect/resource/wound
capture и existing branch identity ledger; только затем публикует six-member §4 batch.
`continuationId` предлагается privately deterministic из G/original/qteId/offer/basis/accept domain,
чтобы cold повтор до commit не выделял второй logical continuation. Не брать ID/receipt из UI/GM.

Exact current original/request/snapshot/backups **удерживаются**. Repair controls тоже
сохраняются до P8 и остаются correlation guards; ordinary ready consumed в ACCEPT batch.
Existing `AllowBrowserOriginalCleanup` flag не заменяет typed transfer/cleanup capability.
Нельзя normal-cleanup текущий original, mint следующий original/G или вызвать full ordinary
post-turn tail поверх joint принятия. `_inGame` не открывает ordinary следующий turn при active QTE.

### 8.3. После ACCEPT: две доказуемые цепочки

Current mechanics законно меняются QTE. Проверять их всегда против P0 footprint неверно;
ослабить сравнение до «marker похож» также неверно.

1. **Original → COMMON → handoff H.** Physical retained original/G + immutable common proof core,
   typed wound/treatment/resource receipts и frozen P1/P2 receipts восстанавливают accepted
   common completion. H — exact current `sealedRootBindings` на genuine acceptance boundary.
   Read-only reconstruction известных phase before-images из этих owned receipts не пишет rollback.
   Сохраняются прежние genuine-completion и точные base==initial / final==phase-current checks;
   raw initial==completed не вводится.
2. **H → current QTE.** Только existing QTE scene/wave/outcome owners и их exact identity ledger,
   causal bindings/receipts/history могут объяснить изменения. Новое named `jointTransfer` reader
   реэкспортирует capability лишь из actual linked runtime/continuation, current original/G,
   валидного source chain и полного current after-image agreement. Ни naked captured roots,
   ни marker/hash wrapper/rehydrated JSON сами права не дают.

Для этого capture расширяется **guard-only** до fixed joint mechanical proof cohort
(excluding M и prior-QTE guard trio), даже если новая сцена не physical. Это proposed reader/schema
change. Никакого general virtual filesystem; только named joint/QTE proof composition.
Изменения вне exact existing QTE write cohort или с wrong original/G удерживаются.
Если implementation не может доказать эту композицию обычными typed owners без authority
shortcut, design снова обсуждается; не обходить это widening старого CompletedProof.

### 8.4. Retirement предыдущей terminal continuation

До ACCEPT разрешена только одна prior terminal source: same G/session, positive earlier turn,
valid exact continuation schema/fingerprint, runtime без active/pending scene, один agreeing
terminal qte_history row, terminal/wave closure без unresolved request/receipt/Ready/debt.
Original current story prefix authenticates historical old story after-prefix; foreign/missing
history, non-prefix/duplicate matching row или changed terminal payload удерживаются.
Старый story witness не выдаётся за current joint witness. Проверяется отдельный
historical-retirement owner, не active `ValidateQteBinding` с чужим original.

Нельзя сначала удалить old continuation и потом создавать новую. ACCEPT атомарно заменяет
только этот exact old terminal before-image; new runtime/continuation/M/offer/Ready признаются
вместе. Пока нет commit, old file остаётся. После committed ACCEPT old source retired,
её восстановление/новая выборка запрещены. DECLINE/NONE сохраняют old terminal unchanged.

Для отсутствия рекурсивного роста новый capsule не вкладывает full predecessor marker,
full predecessor continuation или цепочку predecessor capsules. `priorTerminal`/retirementReceipt
содержат только `{generation, sessionId, turn, qteId, continuationId, authorityFingerprint,
terminalFingerprint, terminalHistoryRowDigest, storyAfterPrefixDigest, priorBytesDigest}`.
Actual old bytes нужны fresh **до замены**, но после verified ACCEPT compact receipt —
только completed-retirement correlation, не новая retirement/restore authority. Generic inputImages
исключает old control trio; runtime/offer/prior continuation before-digests идут в handoff receipt,
а bytes остаются current до commit и в existing publisher recovery inventory во время debt.
Current-turn mechanical H snapshots могут быть велики, но не содержат predecessor blobs;
новый общий архив/каталог не создаётся. Это существенный review focus для cold proof.

### 8.5. D5 / F1: обязательное ordinary resolution в существующем M

**R2 supersedes e7 first-F1 n+1/three-family policy.** Canonical detailed contract, source
inventory/owner DAG, alternatives, action/time budget, proposed D9–D11 and R201–R218 are in
[F1 ordinary outcomes](./lawful-active-joint-tail-b-ordinary-outcomes.md). All are DESIGN
proposals; neither grants nor implementation accepted. F2 only with a proven F1 obstacle.

Decline preserves mandatory same-situation resolution and committed prefix, through a new
ordinary request in the **same game action/turn n**. No recursive originalAction resubmit,
second IncrementTurn, owner-turn-end delivery, guessed elapsed time or repeated cost/reward.
Actual elapsed time/new operations retain their normal consequences once, in accept and decline.
Calendar-only setWorldTime is an accepted unresolved/fail-open time projection, not forced to
a numeric interval or repair; resolved absolute jumps/rewinds preserve actual owner policy (R2 §3).
Full currently lawful ordinary Mortal outcomes (movement, items, quests, XP, NPC/factions,
world/meta/misc and eligible life-end handoff) require genuine full-family typed owners.
No supported-family refusal/drop/reoffer may substitute for valid ordinary resolution.
This is substantially wider than e7; release depends on T192–T198, not an implicit grant.

#### 8.5.1. R2 source authority and budget

Before P0, GM declares nonempty remaining question units, not guessed future outcomes.
Client source capture binds actual original/source rows and structural operation coordinates,
full committed ledger and proposed engine ActionBoundaryCapability (R2 §§2/6). Accepted common
records actual owner-turn closure n and consumed [T0,T1] prefix. New child request/action result
must prove this chain before admission; no user `skipLifecycle` flag or hash-only authority.
Labels, renamed entity/unit/event IDs and request retries cannot replay committed coordinates.
GM causal/narrative classification remains authored, not a claimed client prose oracle.

`ordinaryResolution={version:2,situationId,unresolvedUnits}`; unit fields closed
`{unitId,sourceRowRef,description,ruleRefs,knownTargets}`. No pre-P0 consequence-family slots.
Actual ordinary result `{version:2,obligationId,units}` closes exactly these units; unit record
`{unitId,outcome,checkAudit,contributionRefs}`, outcome=`success|failure|mixed`. Numeric standard
Rule12 audit uses real captured stats/level/Luck/dice/mode and existing rules. Other currently
lawful ordinary checks/deterministic rules use their actual owning validators/evidence; missing
adapter is release debt, never a reason to refuse a lawful remainder. No-check claims require
actual rule/source evidence. All actual contributions get owner dispositions and causal keys.

#### 8.5.2. Closed M field, FD reservation and atomic FT

Proposed `qte.followUp` null для none/offered/accept/terminal; non-null для decline:
`{version,state,obligationId,source,remaining,committedLedger,sourceHeaders,reservation,target,transferReceipt,
child,repair,completionReceipt}`. `version=2`; state=`pending|transferred|resolved|released`.
`source` — parent Binding/G + qteId/offerFingerprint/basisFingerprint/decline operationId.
`remaining` — exact admitted ordinaryResolution; `sourceHeaders` — exact bytes обоих parent
headers, captured fresh при decline, **без секретов/keys**; referenced parent snapshot/backups
остаются физически на месте. Только один parent и один child, без nested M/continuation blobs.
`reservation` required с FD; её точная форма и linearization ниже. `obligationId` privately deterministic из domain `T070-JOINT-ORDINARY-F1-V2`, source и remaining;
он не основан на UI click/UTC, повторный decline получает тот же ID.

`reservation={requestId,gameTurn,requestTimestamp,dicePool,computedContextFingerprint}` фиксируется
**в том же FD batch**, не при FT. RequestId deterministic от obligation, turn=n, timestamp UTC
захвачен один раз; dicePool — новый normal-check pool, созданный pure extraction существующего
`GameLoop.GenerateSecureRandomDice` алгоритма через PendingTurnState owner. Это не вызов
GetOrCreate/Rotate/Reveal/Rewrite: `game_state/control/pending_dice_state.json` guard-only, parent
pool не переиспользуется и не меняется. Gacha в F1 child запрещена: request.gachaBaseResult=null;
F1 не создаёт новую gacha reward authority. Pure computation до FD может быть потеряна при
all-before FD: тогда choice/obligation ещё не accepted и ни одно значение не exposed. После
committed FD любые FT rollback/restart используют ровно сохранённую reservation; никакого
reroll/reclock. FT до transport проверяет её exact equality в request/manifest. Будущий обычный
player turn получает свой pool через existing post-release owner; F1 child никогда не вызывает
обычную rotate на отдельном шаге и не объявляет весь ordinary dice lifecycle исправленным.

`target` null до TRANSFER; затем closed `{binding,requestImage,manifestImage,authorityImage,
requestTimestamp,dicePool,baselineMarkerDigest}`. Binding child имеет тот же physical G,
session/realm/incarnation, requestId/timestamp/dice из FD reservation,
новый snapshot token (hash exact child manifest), тот же gameTurn n/actionId, segmentOrdinal=1. Дайсы уже выделены и durable в FD reservation; FT не генерирует их вновь.
Новые proposed request/manifest members: `requestKind="mortal_joint_ordinary_resolution"`,
`jointOrdinaryResolution={version,obligationId,parentBinding,qteId,remainingFingerprint,
committedLedgerFingerprint,baselineMarkerDigest,actionId,segmentOrdinal}`. В request дополнительно safe remaining/ledger
projection; hashes — correlation, не capability. Ready/repair должны echo exact child tuple
и obligation binding. Accepted original source label — отдельный closed F1 kind, не поддельный
`console_turn`/`browser_turn`; QTE offer запрещён на таком system continuation.

Named existing engine original/request owner работает в следующем ациклическом порядке:

1. **FD DECLINE**: fresh physical parent/G/P1–P4/offer proofs; один existing decline base
   cohort (§4) + M сохраняет choice, decisionReceipt и pending obligation/sourceHeaders/ledger/reservation.
   Parent pair/request/artifacts сохранены. Runtime не становится active, prior terminal unchanged.
2. **FT TRANSFER**: под lease проверить FD снова, построить exact child baseline **после prefix**.
   Child snapshot захватывает FD marker как immutable original image M (и прочие existing
   inventory roots); FD ещё не содержит target/transferReceipt. Child artifacts получают distinct
   snapshot/backups paths текущего owner, не пересекаются с parent artifacts. Snapshot artifacts,
   child manifest/authority/request, consumption только parent-bound Ready/repair/offer controls,
   и M transferred+target+transferReceipt публикуются **одним PublishLocalFilesAsync**. Все paths
   определены original/snapshot owner inventories и source manifests, не произвольной JSON map.
   Старый physical header/request slot заменяется только в этом batch; parent artifacts не удаляются.
   Никакой CleanPending/Load/init/ordinary staging между этими шагами.
3. **Cold read after FT**: сначала current physical G и child original pair/request, затем
   immutable FD image M из child snapshot → parent header capsule → retained parent artifacts →
   common/P1–P4/decline/ledger/remaining proof. Current M обязан быть ровно разрешённым overlay
   над FD плюс child phases. Новый named reader получает relocated-parent read authority только
   этой цепочкой; ordinary reader по одному parsed capsule или released M её не выдаёт.
   Отсутствующий/foreign child, parent artifact или несовпавший FD — HOLD без resnapshot/reseal.
4. Child request отправляется ordinary GM transport после committed FT. Повтор доставки — тот
   же exact request/obligation/dice, не новое действие, новый original или второй запрос. Новый
   mutable M не участвует в child snapshot hash: FD → child manifest/token → FT receipt → M;
   self-hash/цикла нет. Completion receipts исключают M из data digest maps, как §6.

Target artifact inventory — existing snapshot owner output, включая exact present/absent
baseline/rollback/validation artifacts; F1 даёт новый **operation grant** подготовки их after-images,
а не grant писать произвольный path. Точные owner enumerators и transitive consumers перечислены
в B08. Обычный `CreateCanonicalBaselineSnapshotAsync` и live-test PrepareAsync unchanged непригодны;
их pure extraction/atomic adapter — обязательная работа реализации, не уже существующий API.

#### 8.5.3. Complete accepted outcome, repair and child phases

`child={acceptedBasis,mechanics,progression,automatic,projection,story,handoff}` is fixed inside M;
acceptedBasis null until actual response admitted, phases pending/applied with null receipt iff
pending; handoff null unless lawful outgoing life-end. Basis contains full source/input/output,
owner plan seals/DAG, exact before/after/absence maps, real time events, frozen UTC and take proof.
Normalizer issues private `OrdinaryOutcomeTakeCapability` union Plan|ValidatedEmpty after
complete validation/owner takes, before any side-effecting ordinary normalizer. Specialized item/
location/wound/treatment capabilities remain genuine. NoPlan/null/DTO is not Empty authority.
Complete typed result cohort+M ack is atomic through existing publisher, no ordinary schema1 M
replacement, legacy full distributor/finalizer or broad rollback inventory writer.

Ordinary XP uses its existing canonical XP/status semantics and genuine rewards, not the local
QTE XP converter. Later child progression/automatic/projection/story use distinct owner operation
IDs and accepted inputs; no parent P2/P4 replay. Shared D9 event/time admission suppresses only
already consumed owner-turn closure, preserves actual elapsed/operation/condition/scene effects.
All lawful outcomes, actual empty result and full output disposition are covered in R2 §§4–6.

`repair={attempt,state,receipt}` is one current slot, state awaiting|ready|repaired. Before result
commit, candidate-only exact restore + child-bound repair controls + M ack in one transaction;
new attempt only for new rejected response fingerprint. Restores use signed post-prefix child
baseline and actual submitted family membership. M/FD anchor/FT/current headers/request/G/dice/
source artifacts/phase receipts are guard-only; FD proof M is excluded from RollbackBackups.
No unchanged generic RestorePreTurnBackup (currently includes M). Full candidate is retained;
no stripping valid siblings/unsupported fields. After any accepted phase: settle publisher debt,
recompose/verify frozen basis and resume; no generic repair/resubmit/rollback of accepted result.

Parent P4 exact row stays immutable turn n. Child append-only `entryKind=action_resolution`,
new requestId, same actionId/turn n, segmentOrdinal=1 has separate genuine story witness. Story
readers/context/export/UI derive logical turns rather than count rows. Live narrative/interface/
debug are exact accepted child encodings. Other outputs/canonical paths change only under full
actual owner plan with exact proof; parent input images never get rewritten to current values.

Completion binds full composite + event/time + automatic/projection/story receipts, no debt,
all units resolved and exact current after-images. ValidatedEmpty has no invented mechanics.
Outgoing TriggerLifeEnd additionally requires actual durable LifeEvaluation target-owner accepted
handoff, not raw flag/redirect. KF1=parent artifact-only K ∪ child K; shared headers/request/
controls consumed only once under current child binding. Artifacts retained until this completion/
handoff proof; M released receipt retained. No accepted history/mechanics/story deletion.

#### 8.5.4. Cold proof, parity and release gate

Cold chain is actual current G→child pair/request→immutable FD→retained parent artifacts/common/
P1–P4/action closure→complete accepted child overlays; no stale parent-live equality and no
capsule/rehashed marker shortcut. Named overlays reconcile each actual owner after-image, while
parent P4 source/narrative binding stays exact. Source readers, Ready/repair, daemon, console,
browser and agent routes accept distinct request with same game n only under this F1 source.
One dispatcher resumes waiting/repair/result after reload; no lease across transport/UI.

FD/FT rollback preserves the exact reservation. Missing/foreign original/G/artifact or uncertain
publisher debt holds with evidence. A lawful outcome rejected for missing family support is an
implementation failure/release blocker, not successful completion or acceptable product HOLD.
D4, guided US5, DOM/visual and platform gates stay OPEN; D8 payload scope unchanged. T192–T198
and explicit D9–D11 written decisions precede new-QTE activation. All future checks UNRUN.

### 8.6. Terminal, abort и следующая original

Pause/awaiting GM/deferred wave/cancel UI не terminal и не cleanup authority. Resume продолжает
тот же captured offer/continuation, не повторяет P2/P4. Новая сцена не создаёт новый original,
пока старый защищает её source. Current legacy terminal sequence не удовлетворяет этому требованию сама по себе.
B предлагает **D8: selected-terminal action+ack** и один post-QTE automatic slot:

1. На genuine persisted selected terminal и закрытом resolved-wave boundary named QTE owner
   фиксирует `terminal.basis` marker-only PREPARE: original/G/transferId, exact selected binding,
   current continuation/runtime, actual plan/receipt/Ready source, captured terminal UTC,
   exact terminal data before/after images, expected one history row и frozen result fingerprint.
   Choice/outcome к этому моменту уже известны. Raw caller result не может выдать этот basis.
   PREPARE требует исходный M до basis, exact before cohort и complete source proof;
   repeated PREPARE принимает только тот же frozen basis. Crash до PREPARE допускает fresh
   calculation по тому же genuine selected source; после PREPARE captured UTC/after-images
   больше не заменяются. Другой selected outcome/Ready/wave — HOLD.
2. COMMIT использует existing publisher **одним** atomic batch: genuine deferred plan after-images
   (включая physical wound/effect/resource contribution по existing typed grants), earned XP,
   submitted new stats, one history append, runtime inactive/summary, continuation terminal,
   own wave request/receipt/Ready consumption, narrative output, M terminal receipt.
   Все untouched members guard-only с exact inventory; `NormalizerRollbackTrackedFiles`
   не становится write allowlist. Все calculations/normalization должны быть pure owner-produced
   after-images до publication; blind call к сегодняшнему multi-write normalizer запрещён.
3. Exact proposed wire grammar — реальные `terminalOutcomes[].responseFragment`, напрямую
   десериализуемые в `GameResponse`; контейнер `GameStateChanges` отсутствует.
   `responseFragment` omitted/null означает empty fragment. В object разрешены **только**:
   - `response`: string/null; nonblank string имеет existing precedence над sibling
     `finalNarrative`, иначе берётся `finalNarrative`. Existing selected score summary, если
     применим, добавляется тем же deterministic formatter; terminal output UTC frozen в basis.
   - `experienceGained`: existing validated integer/null; amount/XP formula не меняются.
   - `statsIncreased`: existing validated ordered string array/null; те же name/type/cap rules.
   - `resourceChanges`: omitted либо array, принятый existing exact QTE terminal resource
     composer; **proposed narrow B** отвергает explicit null. Только lowercase `damage`,
     target ровно `{kind:"player",targetId:"player_current"}`, source ровно
     `{kind:"narrative_outcome"}`, eventRef по existing sourceTurn/outcomeOrdinal/commandOrdinal.
     Никаких definition/capacity/restore/transfer/general resource grants.
   Duplicate/confusable/unknown keys запрещены, в том числе unknown:null.
   `resourceDefinitionCreations`/`resourceCapacityChanges` запрещены **даже null**, как сейчас.
   `effectChanges`, `playerStatus`, `moneyChange`, `statsDecreased`, `setCharacteristics`,
   interface/debug/image fields и прочие GameResponse fields не входят в proposed subset.
   Отдельный typed sibling `physicalInjury` остаётся ровно existing schema/selection/source
   binding; effect/wound contributions могут возникнуть лишь внутри genuine existing deferred
   plan по поддержанному resource/physical source, а не через новые raw fields.
   Непустой `responseFragment.response` не заменяется безусловно на `finalNarrative`.
   Все branches полностью проверяются **до common**, без выбора будущего outcome; extension
   subset требует отдельного accepted owner contribution и обсуждения D8/C.
4. `experience.json` writer сохраняет только **existing QTE-earned XP** authority/формулу;
   его frozen terminal computation/UTC и exact XP before/after входят в тот же action+ack.
   Нет Training Cap compensation XP. Pure projection writers не получают XP permissions.
5. После terminal COMMIT actual source/ledger re-export выдаёт отдельный frozen
   `JointQteTerminalTailBasis`: terminal selected/final fingerprint, tuple/transferId,
   P2 watermark, current lawful terminal stats/levels/balance и expected automatic after-images.
   Marker PREPARE фиксирует этот уже известный basis; P7b four-member action+ack начисляет только
   разницу levels поверх P2, consuming только QTE stats, затем two-member final projection.
   Один слот на один QTE, без recursive scene/tail loop, повтор PREPARE обязан дать те же inputs.
6. Fresh terminal proof требует all owed waves settled, runtime inactive, exact history/terminal
   continuation/XP/state after-images плюс P7b automatic/projection receipts и no publisher debt.
   Только затем P8; ни UI завершение, ни старый `state="terminal"` отдельно недостаточны.

D8 расширяет **atomic cohort/reader contract** существующего QTE outcome owner; это реальная
дополнительная цена B, не уже доказанная гарантия QTE. Для неограниченных ordinary QTE payloads
C может быть лучше. Если D8/ограниченный subset не принят, поддержка full new-QTE terminal
combinations остаётся blocker: один безопасный acceptance handoff не закрывает FR063.
Успех/ошибка показа интерфейса не terminal proof.
Если existing QTE abort не имеет принятого terminal protocol, B остаётся held/resumable;
не изобретать стирание original на cancel. Новый original допускается после P8 released либо только exact F1 FT (§8.5); accepted-QTE branch не использует F1.

### 8.7. Initial story и terminal narrative — отдельные exact proofs

Этот раздел описывает accepted-QTE/D8 branch. Для declined F1 единственное разрешённое
child output/time/story исключение — named §8.5.3 / R2; default initial/live equality не применяется
вместо его exact chain и не убирается для остальных фаз.

Existing six-field outputBinding сохраняет initial normalized narrative bytes. Сейчас common
reader сравнивает с ними live narrative, а story reader перечитывает live output. Эти old
readers нельзя вызвать unchanged после D8 и нельзя просто убрать их сравнение. Proposed
named phase-aware reader обязан выполнить следующий closed protocol:

1. Resolve publisher debt, fresh physical original/G, actual linked QTE pair и selected-terminal
   source proof; проверить terminal basis/receipt/plan result и точные data after-images.
   Наличие terminal JSON/хеша без этой цепочки не разрешает narrative overlay.
2. Original→common→H proof использует **original outputBinding bytes** и genuine common
   receipts/typed reconstruction. Captured narrative image обязана быть byte-identical этому
   binding. Только признанный D8 terminal after-image может заменить live narrative для current
   phase; все original coordinates, genuine completion/base==initial checks сохраняются.
3. Named story owner read-only заново строит exact P4 row из signed manifest.PlayerAction,
   original realm/incarnation/history prefix, frozen initial normalized narrative/location/entity
   inputs и basis UTC. Сверяет immutable storyPlan, P4 receipt и actual prefix+единственный row.
   Terminal narrative **не** становится narrative прежней строки, новая story/turn не создаётся.
   Guardians/residents/NPC labels не берутся заново из законно изменившегося terminal current state.
4. Отдельно live `output/narrative_response.json` обязана совпасть byte-for-byte с frozen
   terminal memberAfterImages для выбранного outcome, включая response precedence/score/UTC.
   Terminal selected source + genuine receipt объясняют ровно это изменение. Interface/debug/combat
   сравниваются с их initial guard images; никакого broad output waiver.
5. До terminal COMMIT действует прежнее live==initial правило. После COMMIT — exact live==terminal
   after, при unchanged initial binding/story. Mixed/debt/foreign output или rehashed changed
   initial/terminal narrative — HOLD. Public draft сохраняет отдельные exact present/consumed-absent
   правила accepted output-binding; terminal receipt не разрешает новый draft.
6. P7b/P8 используют оба доказательства. Ни frozen storyPlan, ни terminal after-image отдельно
   capability не выдают; named current owner проверяет весь chain перед commit/cleanup.

Это explicit reader/reconstruction delta D6/D8, без general virtual filesystem и новых writers.
Если такую composition нельзя реализовать без authority shortcut, implementation остаётся blocked
для отдельного обсуждения; предложенный protocol не выдаётся за существующий runtime proof.

## 9. Recovery, uncertain и компенсация

Классификация каждой phase всегда после recovery existing publisher journal его владельцем.
Нет дополнительного tail recovery journal или «best effort» последовательных записей.

| Наблюдение | Разрешённое действие |
|---|---|
| Publisher known RolledBack + exact complete before cohort, unchanged original/G/union | Fresh re-export next pending capability, retry same phase. |
| Known Committed + exact complete after + marker ack + genuine chain | Continue next owner; повтор applied — без повторной mutation. |
| Uncertain, journal debt, mixed before/after, foreign bytes, broken generation/admission | HOLD, retain all current evidence; existing publisher resolves debt. Никаких sequential restores, cleanup или ordinary fallback. |
| Cold applied receipt + complete physical/semantic agreement | Only next-phase capability; не known-attempt compensation token. |
| Session replaced | Propagate replacement; не записывать чужой session. |
| M исчез/сменил family при сохранившемся original/committed union | Nonauthorizing protective hold перед ordinary fallback; no authority-by-absence. |

**AUTOMATIC compensation:** только в том же known-committed attempt, после fresh proof exact
applied four-member cohort, до любого P3/P4/P6 acknowledgement. Один atomic restore тех же
трёх data before-images + M-before, с readback. Оригинальные mechanics/progression не откатываются,
claims не rearms/releases. Failed/uncertain compensation — HOLD без второго restore.
Cold restart не конструирует этот token из receipt.

**PROJECTION/STORY:** no mechanical compensation. Retain/resolve publisher state, exact repeat
reconstructs deterministic expected output; не переписывать чужой story suffix.
**QTE terminal / post-QTE automatic:** freeze/recompose известного selected outcome и separate terminal basis;
terminal COMMIT не компенсирует already accepted waves и не допускает XP replay. Post-QTE automatic
имеет такой же ограниченный same-attempt four-member compensation до final projection; предыдущий
P2/common/story не откатываются. Debt/mixed terminal или XPafter без ack — HOLD, не warning-success.
**F1:** FD reservation retained; FT/child debt settles through same publisher. Candidate repair
только §8.5.4, current M/headers/request/parent evidence никогда не generic restore targets.
После child mechanics only frozen phase recovery, no rollback/resubmit. Child output/time
только §8.5.3 / R2 accepted overlay, не освобождение guard inventory.
**ACCEPT/DECLINE:** после known commit нет app-level resurrection старой continuation/offer,
даже при ошибке readback/UI. Existing publisher может только закончить собственное recovery;
fresh reader доказывает before или after либо HOLD. После exposure QTE scene откат запрещён.
**CLEANUP:** нет re-creation original/backups после committed release. K+M batch recovery
заканчивает existing owner. Known after без свежего original — observational completion,
не новый replay/restore/delete capability. Partial K with retained marker не считается PASS.

Границы retained set: весь current mechanical union, computed/story/narrative/interface/debug,
M, original headers/artifacts/backups/request и correlated control evidence. QTE ACCEPT вправе
consume только свои Ready/offer согласно receipt; они не должны требоваться вновь после
полностью authenticated handoff. Unresolved phase сохраняет остальные evidence.

## 10. Интерактивное распределение и player flow

B автоматически **начисляет** points и подтверждает watermark; распределение не вызывается
в publisher callback и не держит lease. Пока original удерживается joint/QTE, allocation
admission блокирует изменение frozen stats/projection. Полезное сообщение игроку:
«Очки начислены. Распределение доступно после завершения события». Readonly status доступен.

После P8 игрок открывает существующее распределение: свежий current balance, новый выбор,
повторная проверка session/incarnation и current expected baseline. Не подавать заранее
замороженные allocations и не считать кнопку evidence начисления. Одноразовые уведомления
показываются только owner result NewlyApplied; cold ExistingApplied показывает текущее состояние
без нового award. Exactly-once mechanics не означает гарантированную доставку toast при crash.

**Предел B:** ordinary `DistributePointsAsync` сейчас имеет отдельные записи. B не выдаёт ему
новую atomic/idempotent гарантию. Для безопасного guided allocation до full US5 acceptance
нужен отдельно принятый owner-specific atomic stats+balance+projection/submit-id contract
(возможная часть C) либо явное решение держать guided allocation вне текущего release.
Здесь для него нет новых root/schema/write grants. Matrix должна доказывать, что during-tail
команды не могут обойти блокировку через console, browser, background/agent route.

## 11. Изменения schema/ownership, требующие решения

| Решение | Конкретное предложение B | Что требуется утвердить |
|---|---|---|
| D1 | Новый marker3/activeTail, fixed AUTOMATIC four-member cohort и named issuers; old v2 остаётся inactive | Все §4/§6 phase grants, exact action+ack и phase-aware proof, без generic storage expansion. |
| D2 | Strict existing initialized stats/points/watermark; никакого heuristic init в tail | При отсутствии baseline отказ до common; отдельный initializer до original. |
| D3 | P2 объединяет stats+award; P3 computed после award | Final projection содержит current points; две existing level priorities не объединяются; no XP writer. |
| D4 | Distribution deferred до P8; allocation safety отдельно | Player flow и explicit guided US5 dependency; не объявлять обычный allocator crash-safe. |
| D5 | Отказ только от QTE, без отмены сцены/committed effects, повторного source action и потери обязательного ordinary resolution | F1 §8.5: one obligation, atomic parent→child original/request, actual ordinary result и KF1 cleanup; proposed grants/turn policy требуют written acceptance. New joint QTE blocked до common. |
| D6 | Continuation2/joint story witness, compact prior retirement, physical actual-pair proof, original retention до QTE terminal | Новый reader/schema/issuer/cleanup handoff §8 и строгий post-handoff composition proof. |
| D7 | Pre-release schema cutover без migration незавершённых v2 в ACTIVE | Existing v1/v2 recovery только их прежние contours; schema3 только новый common publication после accepted design и implementation. |
| D8 | Fixed selected-terminal PREPARE/COMMIT + post-QTE automatic/projection; exact responseFragment subset §8.6 | Atomic terminal/XP/history/closure cohort, terminal-derived basis и initial-story/terminal-output proof §8.7; расширение payload families отдельно, no implicit legacy crash-safety. |

| D9 | Shared action-boundary/event/time admission for accept and decline; no request-driven extra tick | R2 §§2–3 exact typed continuation events, closure budget, true remaining elapsed consequences/cohort. |
| D10 | Full lawful ordinary owner registry + pure contributions/complete DAG | R2 §§4–5 literal 182-name inventory + canonical-only owner catalogs; every applicable adapter, no blanket mapper write grant. |
| D11 | Complete taken Plan/Empty cohort, same-turn child story/readers, durable eligible handoff | R2 §§5–7 exact issuer/compositor/repair/cleanup and target-owner receipt, future T192–T198. |

D1–D11 требуют обсуждения; R2 существенно расширяет первый F1, без implicit grants. D4/D8 остаются отдельными границами.
Несогласованный пункт означает конкретный blocker; никакой fallback «сам решит implementation».
Даже согласование DESIGN не заменяет следующий implementation plan и его review/authorization.

## 12. Матрица будущих проверок — все UNRUN

Столбец proof описывает требование, а не существующий test count. Каталог/selection/budgets
сейчас не меняются. При будущей реализации finite owners выделяются по этим фазам;
никакого all-category/full-suite/fast/premerge. Каждая narrow change: independent corrected gates,
fresh selected BOTH подготовка, catalog/Plan (zero execution), bounded actual run, raw freeze.

| ID | Сценарий / fault boundary | Обязательный oracle |
|---|---|---|
| A01 | lawful stats only; award only; оба; neither; несколько stats/duplicates | Correct ordered cap/100 and +5 per uncovered level, exact 3 before/after + M, once. |
| A02 | missing/malformed/duplicate-key/invalid UTF8 stats, points, levels; unknown/ignored names по existing policy | Fail before common, no init/default/clear/award, полный raw namespace unchanged. |
| A03 | cap-blocked, stat100, checked overflow, processed>current, zero balance, levels jump | Exact frozen arithmetic/ignored/blocked outcomes; XP file byte-identical. |
| A04 | experience.level/playerLevel vs player_status/soul resolver conflicts | Обе source priorities сохранены; no opportunistic current reread. |
| A05 | missing watermark, computed-high/unspent-positive heuristic bait; new incarnation | No heuristic acknowledgement or 8-point init; genuine earlier initializer separately. |
| A06 | Failure before/after each P2 data member/marker and commit/readback/compensation | Complete before or complete after via real publisher, otherwise HOLD; no double +1/+5, no mechanical rollback. |
| A07 | Fresh process after P2 commit before caller receipt; repeated exact retry | Rehydrate from durable physical original/G, AlreadyApplied, unchanged bytes/resources/story. |
| A08 | Changed stats/level/balance/foreign G/header/original after freeze, before take and after await | Exact source mismatch holds; no reacquire/resnapshot/rehash grant. |
| P01 | Projection before write, between computed and M, commit/readback, replay timestamp | Real calculator, full accepted effects; current points final, deterministic frozen UTC, once semantics. |
| S01 | Story prefix only/after row/foreign suffix, row or M crash, runtime n−1/n/n+1 | One exact original/G row, guarded story+ack, runtime n once; no duplicate chat/story or destructive fallback. |
| Q01 | No offer vs lawful offer; invalid/changed/stale/foreign offer; wrong source turn | Absent/valid frozen source only; typed original/Ready/narrative binding. |
| Q02 | Console/browser offer display + reload/restart; no choice; lost click | Same offered state, no lease held over UI, no guessed choice or first auto-accept. |
| Q03 | Accept once, repeated same click, simultaneous accept/decline, stale token, generation replacement | One linked runtime/continuation/offer/Ready/M outcome; conflicts fail, no second continuation/award. |
| Q04 | Every ACCEPT member boundary, same-root restart, distinct-root durable-only copy | Before retains old terminal; after one new active pair; mixed/debt held. Exact source/binary pins and actual child PID required. |
| Q05 | Prior terminal absent/valid/active/foreign/history absent/duplicate/non-prefix/owed wave | Only genuine terminal retirement; no delete-then-create, no restore old active source, no authority from compact digest. |
| Q06 | New physical and nonphysical offer | Physical uses genuine current original source; nonphysical still captures complete joint proof cohort; no fake harmful occurrence. |
| Q07 | After ACCEPT lawful QTE effects/resources/wounds/waves; unrelated mutation or rehashed marker | Two-chain H→current exact proof; neither stale common footprint equality nor arbitrary drift acceptance. |
| Q08 | Pause/awaiting/deferred/error/abort; scene terminal and terminal-ack crash | Retain original/backups until genuine terminal; do not treat UI cancel/EOF as cleanup permission. |
| Q11 | Crash after XP/state/history/runtime before terminal receipt; replay same selected terminal | Single terminal action+ack, exact XP level/history once; legacy multi-write sequence forbidden. |
| Q12 | QTE-earned level/stat after pre-offer award; cold before/after P7b | One new terminal-bound automatic slot; watermark prevents P2 reaward; final points/projection agree, no XP compensation. |
| Q13 | Every authored responseFragment: allowed keys/nulls/types, unknown:null, forbidden definition/capacity even null, wrong resource operation/target/source/eventRef, physical sibling | Exact §8.6 grammar before common; no broad GameResponse/normalizer writes. Nonblank response takes precedence, blank/null uses finalNarrative; score/UTC deterministic. |
| Q14 | Cold crash/repeat P7a→P7b→P8 with lawful changed terminal narrative; corrupt/rehashed initial binding, terminal output, original story/labels | Original P4 story reconstructed unchanged from authenticated initial inputs; separate exact terminal after-image. No stale live==initial refusal, no output waiver/story rewrite. |
| Q09 revised | Decline once/repeated/conflicting; crash/restart до/после decision/intent/request/original/ordinary completion | Mandatory actual standard-mechanics resolution; no lost remainder or duplicate source action/wound/treatment/award. Same obligation/request identity on redelivery, console/browser parity; accepted scene cannot decline. Q-D5 grants required. |
| F101 | Missing/empty/prose-only question, already resolved unit, foreign source coordinate | Refuse malformed source before P0; no inference from declineHint. Lawful outcome family never refused as unsupported; R205–R208. |
| F102 | Genuine FD with committed acquisition+treatment/spend/+5 and disjoint unresolved U | Client-derived ledger exact; same structural source with new IDs cannot replay prefix. |
| F103 | Every FT artifact/header/authority/request/M cut; fresh process before constructor | Whole parent FD or whole child FT after publisher recovery; retained source and no mint/G init. |
| F104 | Child snapshot FD anchoring, changed capsule/ledger/remaining/target/Ready, rehashed current M, lost artifact | Fresh G→child original→FD→parent artifact/common proof; mismatches HOLD without reseal. |
| F105 | Real ordinary GM success/failure/mixed, Rule12 dice/critical audit; new W2/damage vs attempted W1 replay | Actual permitted ordinary result once; all units/slots closed, old W1/T1/spend/+5 once; no intent-only PASS. |
| F106 | Child ordinary wound publication and validation repair while parent occupies M | One M retains parent core; child common typed contribution never overwrites with schema1; repair only post-prefix baseline. |
| F107 | Lost dispatch/GM response/Ready/completion, duplicate/conflicting decline, second browser tab/agent | One obligation/request/dice/child original; resume actual resolution, no source action submission or auto-cancel. |
| F108 | Parent n/time/cost consumed, child same n with zero/nonzero actual remaining duration, repeated child phases | No staging turn/time/cost, no parent progression/award replay; one action closure n; real interval/operation events once, no second IncrementTurn. |
| F109 | Child mechanics→story→completion→KF1 cuts, foreign controls, artifact overlap, partial cleanup | Old artifacts retained until actual ordinary completion, child slot deleted once, full before/after or HOLD, released observation no authority. |
| F110 | Console/browser/agent decline→waiting→repair/restart→ordinary result, allocation/input blocked; real DOM | Same actual mechanics/obligation/state; browser notification alone fails; visual qualification separately recorded. |
| F111 | Different child narrative/interface/debug, zero/nonzero remainder time, parent initial-binding damage, foreign output/unowned combat write; cold after child story | Parent exact P4 prefix unchanged, child result/row n and accepted output/time exact; ungranted changes HOLD. |
| F112 | Genuine ordinary non-QTE plan, effect/resource-only plan, validated-empty success; no repeated parent CREATE/treatment; null/unbound plan | Distinct private Plan/Empty capability and atomic ack in same M; no joint authority impersonation or sequential fallback. |
| F113 | Caller relabels every situation/unit/slot/event/wound ID/prose with fixed actual origin coordinate; distinct lawful op same target | Reused consumed coordinate refuses, client source row immutable; distinct admitted op allowed; no claimed prose oracle. |
| F114 | Invalid first child response after FT, crash/repeated repair, attempted repair after mechanics/story applied | Restore only submitted candidate cohort; exact current FT/M/phases/child headers/request/dice retained, FD proof image never restore target. |
| F115 | Crash during generation before FD commit; FD committed before FT preparation; known FT all-before rollback then cold retry | No uncommitted transport; after FD same request UTC/dice/no parent reuse, pending_dice_state unchanged; before FD no accepted reservation claim. |
| Q10 | Old prior capsule + next + third accepted QTE | No recursively nested predecessor marker/continuation growth; current terminal/source lineage still reconstructs. |
| C01 | K absent/present exactly, foreign Ready/repair controls, stall/protocol failure | Only authenticated K paths; no whole-directory deletion, source-G crossing or arbitrary optionality. |
| C02 | Crash each deletion/M release; recovery/debt; all before/all after/partial K | Actual journal recovery, exact fixed member readback, partial!=completion, no recreated old original after release. |
| C03 | After terminal cleanup cold observe, missing both headers vs mixed/malformed headers | Completed observation never mints new admission; keep accepted initial9/P6 negative protections. |
| U01 | Unspent after award; allocation command while offered/active; restart and same balance | No allocation during retention, no lease over UI, actual fresh choice only after P8. |
| U02 | Deferred allocation later ordinary failure/cancel/repeat | Separate allocator contract/coverage required; B records the limitation, no false atomic PASS. |
| R01 | Changed/stale/foreign public draft, consumed absent draft, narrative bytes/BOM/format | Previous authenticated output-binding rules unchanged; current exactness at each appropriate phase. |
| R02 | Bad G/authority seeded before constructor in fresh process; same/different root | No Load-created G/pristine mint/copied signing key/early write before legitimate owner admission. |
| R03 | Changed interface/debug/combat guard; residentId/guardianId/NPC story reads present/absent/changed, path-family confusion | Fixed P capture covers each real read with correct role; no dynamic authority path, write expansion or label reread after terminal. |
| V01 | v1 ordinary, v2 inactive pending/applied/completed, unknown schema, forged v2→v3 rehash | Strict dispatch; no automatic ACTIVE upgrade/migration; changed private fields refuse. |
| OS01 | Linux actual native fresh-process, file-share/lock/rename faults, cleanup | Native exit/actual cases/source-binary pins/real cleanup; no Windows PASS inference. |
| OS02 | Windows actual native same matrix, sharing/replace semantics, long/case/path aliases | Windows evidence independently required; no Linux substitute/elevated ACL/security requirement. |
| GM01 | GM docs/examples/manifest/source guards, accepted ordinary fields, held/recovery descriptions | GM can author stats/offer without reading implementation; cannot author marker/ack/witness. |

Для каждого executed fixture обязательны ненулевые реальные receipts, distinct absolute roots,
explicit null failure, inactive lease и фактическое отсутствие owned roots. Pure controls владеют
нулём roots и не выдают cleanup PASS. Failure freeze сохраняет native exit, actual selection,
pre/post source/binary pins, raw logs/TRX и reached/unreached oracles до correction/retry.

R2 adds R201–R218 in [full future matrix](./lawful-active-joint-tail-b-ordinary-outcomes.md#9-future-verification--all-unrun): action/time parity, every family and interactions, Empty, lifecycle handoff, crash/repair/current-source proof. All UNRUN.

## 13. Compatibility, platform и storage constraints

Нет требования migrate nonempty legacy/pre-release saves. Но уже принятые supported v1/v2
in-flight recovery нельзя молча переименовать в ACTIVE: они завершаются/отказывают по старому
закрытому протоколу, не получают новый `activeTail` при read. New v3 создаётся только новым
реальным common owner с полным capture. Unknown/malformed schema удерживается с evidence.
Bootstrap/fixtures/current examples обновляются при будущей реализации принятого B, без
legacy fallback. Этот DESIGN сейчас не меняет bootstrap или canonical state.

Одинаковые logical publication и source rules для trusted local Linux/Windows; никаких новых
OS privilege, permanent ACL, cloud dependencies, global lock/journal/security model. Существующий
coordinated publisher и G admission остаются единственными storage owners. Platform inability
или отсутствие actual OS evidence означает UNRUN/held scope, а не simulated acceptance.

## 14. Guided US5 и GM-facing dependencies

Принятый readonly US5-RO1/RO2 остаётся самостоятельным. Этот DESIGN не включает guided
aliases/provider discovery/quotes/confirmation/staging, не закрывает T113/T119/T127–129.
Для полного guided US5 нужны принятые US2/US4 publication/course/resource/time owners,
включая lawful ACTIVE integration, safe allocation decision, recovery и terminal cleanup,
а также настоящее console/browser interaction и DOM/visual coverage.

До implementation release должен быть согласован contract delta в
`contracts/mortal-wound-treatment.md`; затем вместе с кодом синхронизируются
`Rules/Block_CLI_QTE.txt` (owning QTE rule), applicable `Rules/Block_CLI_Operations.txt`, `CLI_API_Specification.md`,
`OtherGuides/Wound_Materialization_Contract.md`, applicable
`OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Wound_Materialization.txt`,
`Examples/E_CLI_Afterlife_Turns.txt`, `Examples/E_CLI_QTE_Offer.txt`,
`Examples/example_validation_manifest.json` и owning documentation/source guards.
Новые marker/continuation receipts — client-owned, GM их не пишет; GM продолжает existing
stats/level/QTE authoring, видит понятный repair/hold contract и пересмотренный D5 ordinary-resolution contract.
Сейчас runtime/GM authoring contract **не изменён**, поэтому active GM prompts/examples
не переписываются на неутверждённое предложение.

## 15. Gate перед реализацией и статус работы

1. Независимый DESIGN reviewer проверяет exact ownership, phase/receipt DAG, frozen source roles,
   post-handoff genuine proof, retirement/cleanup membership и полноту matrix.
2. B выбран пользователем для planning; mandatory D5 семантика уточнена 2026-10-16.
   F1 follow-up schema/authority (§8.5) и implementation plan требуют written acceptance; реализация не разрешена.
3. Только после written-design approval возможен отдельный implementation plan/selection,
   его review и разрешение реализации. Никаких production/test/runtime-tools edits в этой задаче.
4. Full FR063/T070/#1536, ACTIVE runtime PASS и Windows acceptance остаются открытыми.
   #1557 frozen branch и #1558/#1559 backlog не запускаются.

Текущая выдача — проект документа. Исторический 96-PASS readonly checkpoint сохранён,
новый DESIGN не добавляет к нему ни одного runtime PASS.

Historical first-F1 closeout: [corrected independent review](../recovery/lawful-active-f1-independent-design-corrected-20261016-review.md) на `564424a92469555a807f0f77f53002ff1f7e621f` закрыл F1-D01–D05. Тот historical carrier добавлял только review/status metadata; R2 independently reviewed на bddf93041; written acceptance и implementation authorization отсутствуют. STOP для обсуждения.

R2 closeout: [corrected independent review](../recovery/lawful-active-f1-r2-independent-design-corrected-20261016-review.md)
on `bddf930414b12944b5383ec2857404c26f7b01a9` is READY FOR USER DISCUSSION, F1-R2-D01 resolved,
no material findings in correction scope. D9–D11 and T197 exact target protocol remain proposed
and unapproved; T178–T198 unchecked/UNRUN. Metadata carrier does not change reviewed design
semantics. STOP for contract/plan/implementation discussion.
