# FR063: lawful ACTIVE через фиксированные joint-tail фазы — вариант B

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
относится к прежним bytes; прежний D5 delta проверен с планом на f8ab919a. Новый F1 ниже — WIP до отдельного independent review; все grants unaccepted.

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
| **F1 TRANSFER / CHILD** | Existing original/snapshot/request inventories + M; child common/progression/automatic/projection/story cohorts; only narrative/interface/debug outputs + world_time overlay §8.5.6; no combat/location/quest/XP writer | Named current owners с новым closed F1 adapter; parent artifacts retained, no recursive originalAction, no generic path grants. |
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

`game_state/player/experience.json` (кроме отдельно proposed D8 earned-QTE-XP writer; никогда compensation), `game_state/core/player_status.json`,
`game_state/meta/soul_state.json`, `game_state/world/world_time.json` (F1-only exact remaining-time acceptance §8.5.6),
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
на accepted-QTE branch, а F1 имеет только named output/time overlay §8.5.6; combat остаётся guard
и в F1. Live narrative до D8 terminal либо F1 submitted-result admission совпадает с initial image.

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
7. Fixed encoder/version и captured `preparedAtUtc`; все after-image bytes/serialization,
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
`version`, `policy`, `basis`, `basisFingerprint`, `automatic`, `projection`, `story`, `qte`, `cleanup`.
`version=1`, `policy="fixed_tail_b_v1"`.

- `basis`: closed `{binding, preparedAtUtc, inputImages, sourceRoles, automaticPlan,
  storyPlan, offer, priorTerminal, projectionReadPaths, encoderVersion}`.
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
и immutable common proof core (progression calculation **без mutable phase**, existing immutable
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

**DESIGN-only F1, 2026-10-16; recovery/source base c4dde0629cc17aad75196ec509d2cdaf621945a3.**
Пользователь разрешил разработать и независимо проверить F1, не реализацию и не новые
runtime grants. Это уточнение заменяет прежний unchosen F1/F2 gate. F1 — выбранный предмет
дизайна; written acceptance контракта и implementation authorization ещё отсутствуют.
F2 не выбран: source препятствия ниже требуют расширения текущих owners, но не доказывают
невозможность F1. Новый pathname/journal/service здесь не предлагается.

Отказ отменяет только QTE-механизм. Та же ещё не решённая ситуация **обязательно получает
реальный результат стандартных механик**. Ни scene cancellation, ни повтор исходного
действия, ни «отказ записан», ни durable intent сами по себе не являются разрешением.
Common/stats/award/story/turn родителя уже committed и сохраняются.

#### 8.5.1. Source findings и необходимые owner changes

Все строки — статический анализ c4dde062; это не наблюдённый runtime PASS.

| Current source / exact owner | Ограничение; F1 design response |
|---|---|
| `GameEngine.TurnLifecycle.cs:1484–1666,2345–2361` | Console decline вызывает `ProcessPlayerTurn(originalAction,reminder)`. Staging генерирует request/timestamp/dice, делает backup, snapshot и clear outputs отдельными шагами. В F1 этот путь unchanged запрещён; нужен named continuation entry без player action submission. |
| `GameEngine.SessionAndSnapshots.cs:410–422,514–568` | Snapshot owner сначала очищает старый original, затем отдельно пишет manifest/authority. F1 использует его pure capture/encoding/validation, но не вызывает destructive helper. Передача старого→нового original должна быть одним batch с M. |
| `Models/TurnRequest.cs`; `GameEngine.PrivateImplementation.cs:24–58` | Нет typed ordinary-remainder binding. Предлагается closed request/manifest member, связанный с parent и obligation; reminder/PlayerAction не authority. Engine Frame остаётся private. |
| `LiveTurnPreparationService.cs:86–90,195–213` | Live helper тоже очищает prepared artifacts и последовательно пишет headers/request. Это consumer нового shared preparation contract; не независимый issuer F1, не cold-recovery shortcut. |
| `MortalQteDecisionPublication.cs:16,91,210–216` | Один M используется ordinary schema1 и joint schema2. Child ordinary wound publication не вправе заменить retained parent M на v1. Нужен named child contribution внутри единственного `followUp`, не второй marker и не запрещение legitimate ordinary wounds. |
| `PendingTurnSnapshotAuthority.cs:11–15,428–431,967–989`; `PendingTurnSnapshotReader.cs:193–216` | Supported portable authority использует exact payload/hash integrity и физические original artifacts; это не anti-owner signature. Новый reader должен заново проверить G, оба headers, source artifacts и closed chain; JSON/hash/copy signing key не mint authority. |
| `QteWebInteractionService.cs:267–283`; `BrowserPlayerActionService.cs:78–113` | Browser decline сейчас лишь clears/notifies, composer создаёт новое player action. Оба клиента должны вызывать тот же F1 resume/submit owner; browser composer queue не используется для remainder. |
| `Rules/Block_CLI_QTE.txt:19–28`; CLI specification QTE Offer; QTE example | Existing ordinary offer запрещает same-situation state changes и требует ordinary follow-up. Нужна явная joint-only exception: committed **prefix**, но unresolved **remainder**; весь исход не может быть объявлен уже решённым. Ordinary offer contract вне joint не меняется. |
| `Rules/Block_12.txt` ordinary action-check algorithm; accepted Mortal wound/resource/effect owners | Remainder исполняется обычными check/dice/typed mechanical owners. QTE score/terminal selection не заменяют ordinary check. Новый closed audit связывает существующий расчёт с unresolved unit; формулы не меняются. |

#### 8.5.2. Откуда берётся unresolved ситуация и что именно дедуплицируется

До P0 genuine GM offer обязан содержать новый **proposed**, joint-only closed
`ordinaryResolution={version:1,situationId,unresolvedUnits}`. `situationId` — local nonblank
ID внутри exact original/offer; это не global ID и не authority. `unresolvedUnits` — непустой
ordered список `{unitId,description,ruleRefs,targetRefs,consequenceSlots}`. `unitId` уникален,
`ruleRefs` ссылаются только на существующие обычные правила (например Block_12), targetRefs
разрешаются точно по текущему realm/incarnation. Slot задаёт `{slotId,family,sourceCoordinate}`;
family — закрытый `wound|effect|resource` existing accepted Mortal common family. Это descriptor
ещё не решённого вопроса, не выбранный outcome, не canonical after-image и не arbitrary path.
Все units должны быть исполнимы этими обычными owners; unsupported family, ambiguous target,
empty remainder или уже resolved unit — refusal **до common**, а не успешный decline.
Расширение на quest/location/general GameResponse families требует отдельного design delta.

GM авторитативно описывает игровой вопрос в accepted offer; клиент не извлекает его из
cinematic prose/declineHint и не придумывает после отказа. Admission связывает descriptor с
source player-driven original, accepted response и common plan **до их seals**, как §5/B01.
Source action хранится только как context. Новый исполнимый текст запроса: «Разреши оставшиеся
units … обычными механиками с учётом committed prefix»; source PlayerAction не переотправляется.
GM не пишет M, obligationId, transfer/receipt или client-derived committed ledger.

Named source owner строит `committedLedger` из настоящих common/treatment/occurrence/resource/
effect histories и P1–P4 receipts. Для каждой строки фиксируются family, original source tuple,
source event/opportunity/command coordinate, target, instance/transition/receipt IDs и exact
before→after evidence. Award/progression/story включаются как client-owned operations, а не
как новые GM commands. Ledger хранит также **structural source coordinate** из следующего протокола; новое
woundId/eventId не изменяет координату и не позволяет повторить её consumed consequence.
Unit/slot coordinates дизъюнктны committed coordinates; equality/confusable alias — pre-P0 refusal.

Machine coordinate — closed `{originBinding,producerKind,sourceOrdinal,operationOrdinal,
family,targetKind,targetId}`. `originBinding` — exact parent source tuple, producerKind — accepted
source-owner enum, ordinals — nonnegative integers исходного admitted source inventory, family —
три enum выше; target берётся из actual canonical entity authority с exact ID, без trim/casefold/
Unicode fuzzy matching. Source owner до P0 перечисляет **один** ordered inventory producer
occurrences/operations из genuine original + accepted submitted producer/offer inputs. Каждой
строке присваивает ordinal один раз; existing occurrence/opportunity/command references
разрешаются к этой строке, не становятся новыми identities. Он фиксирует partition
`committed|unresolved` по настоящим common contributions и accepted unresolved descriptor.
Одна строка не может принадлежать обеим частям. `situationId`, unitId/slotId, eventRef и prose —
только labels/refs; они не могут выбрать новый ordinal, producerKind или originBinding.
Неизвестный/неоднозначный ref и попытка переименовать consumed source — refusal до P0.

Для prospective remainder source owner создаёт client-bound source row из admitted offer unit
до common; child получает только эту row и ещё не consumed operation/slot, а не свободное
право объявить новый source event. Child producer capability связывает `(obligationId,
sourceCoordinate,unitOrdinal,slotOrdinal,acceptedOutcomeWitness)`; fingerprints зависят от
этого origin, а child tuple — transport/execution binding, не новый root cause. Wound/effect/
resource producers обязаны consume эту capability, не создавать новую occurrence по произвольным
child eventRef. Same coordinate+operation уже в ledger отвергается при любых новых IDs/labels;
другая реально admitted operation на том же target допустима с отдельным outcome witness.

**Граница доказательства:** GM отвечает за игровую классификацию вопроса и причинную связь
описанного события при initial authoring. Клиент проверяет immutable structural partition,
refs/typed outcome и consumed operations; он не распознаёт семантическую эквивалентность двух
заведомо ложно разных GM-описаний. Если исходный source inventory не позволяет установить
такую partition без догадки, F1 отказывает до common. Нельзя заявлять anti-paraphrase oracle
по хешу/строке. Это честная граница существующего GM-authored source contract, не повод
автоматически выбирать F2, который тоже не создаёт игровой causal oracle.

Для child normalizer/composer обязателен source gate: каждый новый typed contribution имеет
ровно один unresolved unit/slot из sealed descriptor, existing ordinary authority и новую
source occurrence только для этого remainder. Reference на parent committed instance допускается
как read/context, но повтор create/treat/spend/award запрещён; legitimate later operation на той
же ране требует отдельного unresolved slot и existing lifecycle authority. Не переписывать
history, не вычитать/компенсировать prefix и не доверять GM-списку «already applied».
Raw child state writes вне projected supported plan должны попасть в existing validation/repair,
а не расширять cohort. Подмена ID при том же source event отвергается до publication.

#### 8.5.3. Closed M field, binding и ациклическая передача current original

Proposed `qte.followUp` null для none/offered/accept/terminal; non-null для decline:
`{version,state,obligationId,source,remaining,committedLedger,sourceHeaders,reservation,target,transferReceipt,
child,repair,completionReceipt}`. `version=1`; state=`pending|transferred|resolved|released`.
`source` — parent Binding/G + qteId/offerFingerprint/basisFingerprint/decline operationId.
`remaining` — exact admitted ordinaryResolution; `sourceHeaders` — exact bytes обоих parent
headers, captured fresh при decline, **без секретов/keys**; referenced parent snapshot/backups
остаются физически на месте. Только один parent и один child, без nested M/continuation blobs.
`reservation` required с FD; её точная форма и linearization ниже. `obligationId` privately deterministic из domain `T070-JOINT-ORDINARY-F1-V1`, source и remaining;
он не основан на UI click/UTC, повторный decline получает тот же ID.

`reservation={requestId,turn,requestTimestamp,dicePool,computedContextFingerprint}` фиксируется
**в том же FD batch**, не при FT. RequestId deterministic от obligation, turn=n+1, timestamp UTC
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
новый snapshot token (hash exact child manifest). Дайсы уже выделены и durable в FD reservation; FT не генерирует их вновь.
Новые proposed request/manifest members: `requestKind="mortal_joint_ordinary_resolution"`,
`jointOrdinaryResolution={version,obligationId,parentBinding,qteId,remainingFingerprint,
committedLedgerFingerprint,baselineMarkerDigest}`. В request дополнительно safe remaining/ledger
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

#### 8.5.4. Реальное ordinary выполнение и completion, включая single M collision

Child идёт через existing ordinary GM request/Ready/validation/repair и **ordinary** accepted
Mortal wound/effect/resource planner, с новым named F1 adapter. `JointRepairCommonAuthority`
не используется: existing joint planner требует acquisition CREATE+treatment и не умеет
no-consequence ordinary result. Actual owner — `CanonicalStateNormalizer.AcceptedMechanics.cs`
(103–107,286–334): NoPlan сейчас возвращает null, receipt branch batch, прочий ordinary branch
sequential. Этот existing split не предоставляет F1 atomicity сам по себе.

Proposed `F1AcceptedResultCapability` — privately issued union `ValidatedPlan|ValidatedEmpty`.
Issuer только normalizer F1 preflight на fresh child original/G/FT/Ready/source partition и
complete accepted output/check/slot validation. Для ValidatedPlan использует genuine ordinary
AcceptedMechanicsInput→GetOrBuildCommonValidated→validated binding→TryTakeCommonValidated;
требующие специальных taken owners contributions сохраняют свои existing authority gates и
не impersonate joint/treatment receipt. Candidate без complete ordinary authority отвергается.
Для ValidatedEmpty issuer заново доказывает отсутствие pending/required typed contributions,
NoPlan write authority и submitted changes вне разрешённого output/time cohort, плюс полный
nonempty actual ordinary result со всеми explicit allowed no-consequence outcomes. `null plan`
или один NoPlan не capability. Empty — отдельный frozen zero-contribution basis/receipt, не
фиктивная рана или вылеченная parent wound. Его batch содержит только exact allowed output/time
members (если changed) и M child.mechanics ack; mechanical family members guard-only.

Оба variants publish через новый normalizer-owned F1 atomic action+ack path до ordinary
sequential fallback или CreateAsync/schema1. Существующий joint publisher/invariants не
ослабляются. Named take/basis связывает immutable input/result/complete fixed cohort, issuer/
lease/G/child tuple/obligation; public raw AcceptedMechanicsPlan/DTO/fingerprint этого не дают.
Cold reader reconstructs same variant из retained original/accepted basis/current phase proof,
не создаёт capability из `NoPlan`/result JSON alone. Не вызывать QTE terminal/D8
или legacy full ordinary finalizer unchanged. Proposed response member
`ordinaryResolutionResult={version:1,obligationId,units}` содержит ровно исходные unitIds;
каждая строка `{unitId,outcome,checkAudit,contributionRefs}`. Outcome — `success|failure|mixed`;
checkAudit связывает applicable existing Rule12 inputs, ordered dice source indices/selection,
modifiers, critical calculation и итог; client adapter проверяет bounded numeric core и typed contribution/slot correlation.
Профиль `ordinary_rule12_numeric_v1` использует captured current computed characteristic,
level/permanently-modified Luck, последовательные d20 indices, effective roll mode и existing
Rule12 critical threshold/stat cap/level-scaling/difference/result bands. Action difficulty,
выбор relevant characteristic и narrative interpretation остаются GM-authored judgments,
проверяемыми на source/тип/границы, а не семантическим клиентским oracle. Неподдержанные
specialized Rule12 rules требуют отдельного accepted profile до P0; general rules engine
и universal narrative validator не подразумеваются. Deterministic profile требует заранее
admitted exact ordinary rule/sourceCoordinate/evidence refs и expected slot disposition;
одна строка «проверка не нужна» не доказательство. Новый pure numeric validator — proposed
F1 owner, не существующий Rule12 runtime API. Ни `pending`, ни пустой result, ни notification
не исполняют unit. Для no-check existing rule указывается её exact deterministic evidence;
GM не может объявить contested unit no-check. Все slots учтены materialized contribution либо
explicit no-consequence outcome, допустимым исходным rule; blanket empty success не принимается.

`repair` null до первого rejected response; форма ниже. `child` null до FT, затем `{acceptedBasis,mechanics,progression,automatic,projection,story}`; acceptedBasis null до validated response, phase slots `{state,receipt}` с `pending|applied` и null receipt iff pending. Это фиксированные slots
внутри **того же M**, не recursive activeTail и не общий operations journal. Accepted basis
фиксирует только уже полученный и validated ordinary result, child original/G/Ready/response,
existing composed plan inputs/results, exact before/after cohort и UTC. Child wound publisher
вместо замены M schema1 предоставляет свой обычный typed contribution этому adapter; parent
common/basis/decision остаются immutable. Wound/resource/effect formulas/ownership прежние.

Каждый child phase публикует свой accepted owner cohort + M ack атомарно, по тем же action+ack
правилам P1–P4; mechanics первым использует genuine ordinary accepted plan либо ValidatedEmpty cohort, без
generic StateDistributor/NormalizerRollbackTrackedFiles allowlist. Source/normalizer readers
проверяют **child original→prefix baseline→child phase overlays**, не stale parent final equality.
Child progression/stat/projection/story имеют отдельные operation IDs/inputs от parent; no P2/P4
replay. Схема F1 не принимает arbitrary GameResponse: raw XP grants, quest/location/afterlife,
новый QTE и third turn запрещены. Raw ordinary XP/stat awards вне принятого typed F1 cohort не допускаются; если unit требует
таких grants, она несовместима с первой схемой F1 и refuses до P0. Child automatic слот лишь
consumes уже законно admitted inputs по принятому named automatic contract, без новых grants. D8 не даёт child XP authority.

`completionReceipt` выпускает current ordinary completion owner только после mechanics,
progression, automatic, projection и story applied, полного source/slot resolution, exact
current after-images и отсутствия publisher/repair debt. Receipt связывает obligationId,
parent и child Bindings, accepted result fingerprint, все phase receipts и one child story row.
Запись result/Ready сама по себе не completion. Повтор после принятия возвращает ExistingApplied
без GM re-execution/награды/переброса. Потерянный ответ до acceptance redelivers тот же request;
после mechanics commit перед receipt crash восстанавливает publisher/phase ack, не выполняет
действие снова. Validation repair сохраняет obligation/request/dice и восстанавливает только
child baseline **после committed prefix**, не parent pre-common состояние.

**F1 repair — отдельный proposed named transaction, не ordinary fallback.** Immutable FD image M
в child snapshot — **proof-only**, исключается из child RollbackBackups/restore/delete inventory
вместе с physical current child headers, request, M, G, parent/child source artifacts и reservation.
Обычный EnumerateRollbackTrackedFiles сейчас включает M; RestorePreTurnBackupAsync восстанавливает
его, поэтому оба unchanged paths запрещены на F1 source kind. Snapshot read inventory и repair
write membership — разные списки. M/FT/source/projection/story/history receipts guard-only.

До child.mechanics applied named F1 repair может восстановить только rejected GM candidate
members: fixed accepted common-family **submitted** carrier paths и named output/time roots,
пересечённые с exact child signed baseline plus original present/absent inventory. Family maps
берутся от existing ordinary composer/source catalogs, не keys из repair JSON. Read-only
parent proof и child current baseline проверяются до/после; unknown path HOLD. Restore
after-images — точный post-prefix child baseline, M остаётся current FT, не snapshot FD.
Restore candidates + own-bound repair request/Ready transition + M repair receipt публикуются
одним batch. `followUp.repair={attempt,state,receipt}`; state=`awaiting|ready|repaired`, receipt null до
repaired и exact phase receipt затем. Это closed current slot, не growing journal;
attempt повышается только по новому rejected child response fingerprint, повтор того же repair
не mint новый attempt. Request/Ready связывают child tuple, obligationId, rejected-response
fingerprint и attempt; redelivery сохраняет original/dice/remaining identity.

После child.mechanics **или любого accepted child phase** generic repair/restore/resubmission
запрещены. Existing publisher сначала settles debt; затем phase owner recompose/verify принятого
basis и resume оставшиеся slots, либо HOLD. Accepted mechanics/story не откатываются ради
плохого display/output. Repair receipt не сбрасывает child phase ack, current FT или source
partition и не регенерирует old request. До публикации нового child response прежний accepted
basis отсутствует; после его принятия immutable basis не заменяется GM repair payload.

#### 8.5.5. Turn/time, cleanup, crash и клиенты

Parent остаётся accepted turn **n**, child — **n+1**, системное продолжение той же ситуации,
не новая команда игрока. FD/FT/staging/redelivery не увеличивают turn, не запускают progression,
не расходуют ресурсы и не меняют world time. Child n+1 публикуется/reconciles один раз только
с его story receipt; crash допускает runtime n или n+1, но не n−1/n+2.
Parent time/cost/turn-based recovery остаются consumed. Child uses current post-prefix baseline,
собственный ordinary progression control и **только remaining** duration/cost. Нулевая длительность
допустима по ordinary rule; elapsed time не берётся повторно из source action. Child per-turn
lifecycle выполняется один раз для n+1 по существующему ordinary contract (даже при нулевом
elapsed), не переигрывает n. Dice pool новый для unresolved units, а consumed parent dice остаются
в ledger; source checks не повторяются. UI не запрашивает заново действие/оплату для отправки.
Это явное proposed turn policy, подлежащее written acceptance, не скрытая оптимизация времени.

P8 для decline разрешён **после completionReceipt**, не после transfer. Его отдельный privately
issued `F1CompletedCleanupCapability` следует из F1 completion reader/current child source chain,
не из accepted-QTE terminal proof и не из голого transferred marker. Cleanup inventory
`KF1 = parent artifact-only K ∪ child K`, deduplicated exact paths: reused header/request/Ready/
repair slots принадлежат только текущему child и удаляются один раз при proof его tuple.
Parent artifact-only K берётся из validated parent payload Files.Values/RollbackBackups.Values
и exact local-turn marker keys; excludes reused current header/request/control slots. Member
overlap child artifacts/parent artifacts или механические roots — HOLD. Parent Ready/repair
были consumed только own-bound FT receipt; чужой error/stall/protocol failure — HOLD. M не
удаляется: atomic KF1+M released ack сохраняет оба completion/source bindings. Никаких output,
story/history/mechanics deletions, parent restore или re-creation original после release.
До завершения source artifacts retained, allocation D4 и прочий ordinary input заблокированы.
После release M только наблюдаем; он не mint новый request/replay/cleanup capability.

| Crash cut | Допустимое восстановление |
|---|---|
| До/после FD, lost UI return | All-before offered либо all-after pending той же obligation; повтор/conflicting accept не создаёт иной путь. |
| Во время FT artifact/header/request/M publication | Existing publisher сначала решает debt: целый parent pending или целый child transferred; mixed namespace HOLD. Нет side delete/нового request. |
| FT committed, до dispatch/после GM read/до Ready | Resume same request; durable obligation не потеряна, нет второго child original/dice pool. |
| Child validation/repair либо любой phase/ack cut | Complete before/after named phase или HOLD; repair baseline post-prefix, exact old ledger сохраняется. |
| Child mechanics/story committed, lost caller/completion ack | Phase-aware reconstruction до completion; не blind resubmit source action или ordinary mechanics. |
| KF1 cleanup/released cut | Atomic before/after/debt, partial deletion не completion; released read observational. |

Console, browser и agent dispatch вызывают один engine F1 owner через existing session/main
participation. Browser decline получает «Событие продолжается обычным ходом», видит ожидание,
repair/held либо **реальный ordinary результат** и после reload/restart тот же статус. Browser
не посылает originalAction в composer. Console не вызывает recursive ProcessPlayerTurn.
UI polling не создаёт запросы/ack; resume dispatcher запускает pending/transferred работу при
легитимном owner admission. Смена G/session/incarnation удерживает source, не rebinding автоматически.
Один действующий owner/lease; транспорт и UI ожидания вне lease. Actual console/browser/agent
integration, frontend DOM/visual и Linux/Windows crash checks остаются UNRUN.

**Worked future oracle:** parent n материализовал новую царапину W1, выполнил genuine treatment
T1 другой раны и начислил +5; unresolved unit U — исход опасного прохода под балкой, отличный
от already committed impact. После decline child n+1 GM выполняет обычный Rule12 check U с
сохранёнными дайсами; failure legitimately materializes **новую** травму W2/resource damage
по U-slot, success закрывает U с проверенным no-consequence outcome. Проверка должна увидеть
actual check/result + W2/damage в failure, one child story row и completion, W1/T1/их spends/+5
ровно один раз. Повторное создание W1 с новым ID при том же immutable source coordinate отвергается; prose
сам по себе не causal oracle (§8.5.2). Это будущая
проверка обычного результата, не запуск игры в этой документационной сессии.

**Gate:** F1 предлагает точный источник remaining, source→request→ordinary completion и bounded
owner extension; current helpers этого не реализуют. До written acceptance F1/grants/плана и
отдельной реализации+qualification new joint offers продолжают refuse **до common**. Даже
no-offer этапы не разрешены этим DESIGN. F2 можно открыть только с конкретным доказательством,
что этот closed owner/slot/atomic handoff нельзя реализовать в F1; добавление отдельного path
само по себе не устраняет ни semantic dedup, ни source/ordinary completion проблемы.

#### 8.5.6. Named F1 child output/time overlay и two-story proof

Proposed F1 submitted cohort — ровно `output/narrative_response.json` (ordinary response плюс
closed ordinaryResolutionResult), `output/interface_updates.json`, `output/debug_logs.json`,
`game_state/world/world_time.json` (только existing nonnegative integer `timeChange` минут
**remainder**, zero allowed), плюс existing typed wound/effect/resource candidate surfaces.
Это именно новые F1 operation grants: source producer/GM может authored submitted files только
для текущего committed FT request; normalizer F1 owner accepts их exact images с child mechanics
ack. `setWorldTime`, combat_log, current_location, quests/lore, arbitrary output files, XP grants,
afterlife и QTE offer запрещены первой схемой F1. Unknown/null/confusable field policy strict;
normal narrative/interface/debug сохраняют existing ordinary schema и visibility filtering.

Child original snapshot содержит exact post-prefix before images этих roots. До Ready они
лишь unaccepted candidate bytes, не отображаются как accepted result и не дают capability.
На complete correlated child Ready named reader captures submitted images, validates exact
closed fields/result/slots/time amount и freezes `child.acceptedBasis.outputTime={beforeImages,
submittedImages,acceptedAfterImages,resultFingerprint}`. Immutable accepted basis PREPARE в M
проверяет полный source set и current submitted equality; repeated PREPARE только те же bytes.
Mechanics batch включает эти exact accepted output/time members + ordinary plan contribution
либо ValidatedEmpty + M mechanics ack. Already equal data members guard-only; receipt сообщает
полное cohort, а не право на arbitrary output drift. После mechanics current bytes обязаны
равняться acceptedAfterImages до cleanup; changed/foreign output или время — HOLD.

F1 named proof всегда реконструирует parent P4 row из retained FD parent **initial** outputBinding,
source labels и original artifacts, не из live child narrative. Отдельно проверяет child original/
FT/accepted result+outputTime+mechanics receipt и одну additional child story row n+1 с новым
source writer kind `mortal_joint_ordinary_resolution`. Оба prefixes/rows имеют exact before/after
witness; после child story parent row остаётся неизменным prefix, не повторно appended. Ни
schema1 console/browser writer label, ни D8 joint terminal witness не подделываются. Current
UI показывает accepted child response; initial parent response остаётся source/history evidence.

World time: source amount берётся только из accepted child remainder/timeChange, прочитанного
ordinary time semantics (FileMapping timeChange→world_time; current progression frozen-time
resolver), и его complete source images. Named owner сохраняет existing calendar/fallback rules,
не переиздаёт parent timeChange как child command; ambiguous baseline/unsupported time representation
refuses до P0. Progression child freezes этот accepted world-time image и строит только свои
schedule/report/M after-images; он не получает world_time writer. Parent time/progression proof
реконструируется из frozen FD, current time — exact child after. Длительность не значит новый
player action. Это узкий F1 overlay, D8 terminal/narrative/earned-XP cohort unchanged.

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
child output/time/story исключение — named §8.5.6; default initial/live equality не применяется
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
только §8.5.6 accepted overlay, не освобождение guard inventory.
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

Документ рекомендует согласовать D1–D8 как package B с отдельным обсуждением D4/D5/D8; уточнение D5 в §8.5 открыло новый обязательный planning gate.
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
| F101 | Missing/empty/prose-only remainder, already resolved unit, source/target/slot alias, unsupported family | Refuse before P0; no inference from declineHint, no fabricated obligation. |
| F102 | Genuine FD with committed acquisition+treatment/spend/+5 and disjoint unresolved U | Client-derived ledger exact; same structural source with new IDs cannot replay prefix. |
| F103 | Every FT artifact/header/authority/request/M cut; fresh process before constructor | Whole parent FD or whole child FT after publisher recovery; retained source and no mint/G init. |
| F104 | Child snapshot FD anchoring, changed capsule/ledger/remaining/target/Ready, rehashed current M, lost artifact | Fresh G→child original→FD→parent artifact/common proof; mismatches HOLD without reseal. |
| F105 | Real ordinary GM success/failure/mixed, Rule12 dice/critical audit; new W2/damage vs attempted W1 replay | Actual permitted ordinary result once; all units/slots closed, old W1/T1/spend/+5 once; no intent-only PASS. |
| F106 | Child ordinary wound publication and validation repair while parent occupies M | One M retains parent core; child common typed contribution never overwrites with schema1; repair only post-prefix baseline. |
| F107 | Lost dispatch/GM response/Ready/completion, duplicate/conflicting decline, second browser tab/agent | One obligation/request/dice/child original; resume actual resolution, no source action submission or auto-cancel. |
| F108 | Parent n/time/cost consumed, child n+1 with zero/nonzero remaining duration, repeated child phases | No staging turn/time/cost, no parent progression/award replay; child lifecycle once, runtime n/n+1 reconciliation. |
| F109 | Child mechanics→story→completion→KF1 cuts, foreign controls, artifact overlap, partial cleanup | Old artifacts retained until actual ordinary completion, child slot deleted once, full before/after or HOLD, released observation no authority. |
| F110 | Console/browser/agent decline→waiting→repair/restart→ordinary result, allocation/input blocked; real DOM | Same actual mechanics/obligation/state; browser notification alone fails; visual qualification separately recorded. |
| F111 | Different child narrative/interface/debug, zero/nonzero remainder time, parent initial-binding damage, foreign child output/combat write; cold after child story | Parent exact P4 prefix unchanged, child result/row n+1 and accepted output/time exact; ungranted changes HOLD. |
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
