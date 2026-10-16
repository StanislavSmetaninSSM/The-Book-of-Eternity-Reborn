# F1-R2: одна игровая граница, полноценный ordinary outcome

**Статус:** PROPOSED DESIGN; initial independent review F1-R2-D01 corrected, affected re-review pending. Source/recovery base
`e7f86f590892e4c335ad29ece88c265ad94b8138`, branch `1536-wound-b2-cloud-20261012`.
[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), FR063/T070.
Пользователь согласовал дальнейший DESIGN-only: убрать лишний tick только из-за decline и
обработать все законные ordinary outcomes. Код/тесты/runtime tools не меняются; build, discovery,
runtime, GM, console/browser, visual, Linux/Windows checks **UNRUN**. Новые grants не приняты.
Этот документ — canonical уточнение [B §8.5](./lawful-active-joint-tail-b.md) и
[implementation plan](./lawful-active-joint-tail-b-implementation-plan.md), заменяющее e7 F1
`n+1 + narrow wound/effect/resource-only` policy. Предыдущие immutable reviews не покрывают R2.

## 1. Требование и source evidence

Decline отменяет необязательный QTE-способ разрешения. Он должен реально разрешить ту же
ситуацию обычными правилами, сохраняя committed prefix. Дополнительный GM request не означает
дополнительный player action, turn end, время или оплату. Movement/quest/XP/прочие законные
результаты не удаляются и не превращаются в «неподдержано», повторный QTE либо notification.
Валидный результат без новых последствий тоже является полноценным разрешением.

| Source на e7 / owner | Наблюдение по коду; вывод для DESIGN |
|---|---|
| `GameEngine.TurnLifecycle.cs:1585–1606,1867–1918,2345–2357` | Staging делает TurnNumber+1, обычный finalizer increment + story + progression; console decline вызывает ProcessPlayerTurn(originalAction). R2 не вызывает этот путь unchanged. |
| `EffectAcceptedTurnInputComposer.cs:334–448` | Создаёт accepted_effect и `turn_{turn}:lifecycle:owner_turn_end:...`; даже empty commands создают owner-turn event. Новый request с n+1 реально меняет event identity. |
| `EffectLifecycleScheduler.cs:221–294,660–696,906–929` | Turns lifetime уменьшается по matching advancePhase; replay eventRef — error, не AlreadyApplied. Нельзя просто повторить n или переименовать request и надеяться на idempotence. |
| `EffectAcceptedTurnPlanner.cs:6299–6417,6957–7045` | Фактические lifecycle events применяются к current occurrences и записывают identity/history. Новый effect/target сам по себе не имеет старого processed-event ref; одного per-effect dedup недостаточно против повторной доставки закрытого turn end. |
| `QteDeferredAcceptedMechanicsPlanner.cs:588–635` | Terminal composer использует acceptedSourceTurn и Compose(empty commands), то есть тоже затрагивает lifecycle admission. Это source-level risk/required adaptation, не утверждение о воспроизведённом double-tick. |
| `QteSceneService.cs:1742–1935`; `GameEngine.TurnLifecycle.cs:2364–2379` | Local QTE terminal использует source turn, затем отдельные XP/state/history/runtime операции; новый ordinary player turn не создаётся. D8 не является доказанной crash-safe реализацией. |
| `ProgressionScheduleService.MortalJointInputs.cs:55–118`; `.MortalJointBasis.cs:167–185` | Счёт времени/report context и schedule/report after-images имеют собственного frozen owner. Нельзя повторно применять prefix timeChange или просто пропустить весь progression. |
| `IO/StateDistributor.cs:21–24,110–190,240–272,439–572` | Complete distribution последовательна; generic unknown field skip; field-specific merge/control semantics. Нельзя считать её universal atomic owner или использовать DTO roundtrip с потерей полей. |
| `CanonicalStateNormalizer.cs:406–481`; `.AcceptedMechanics.cs:286–334` | Нормализация многих family пишет до common publisher, ordinary non-receipt path sequential. Реальный universal result требует новых pure owner contributions и полного принятого composite. |
| `.MortalLocations.cs:19–99`; `.QuestsAndRivals.cs:10–40` | Location имеет typed planner + identity/storage companions; quests/history — отдельные normalizers. FieldToFile не перечисляет все derived writes и сам не даёт authority. |
| `GameEngine.TurnLifecycle.cs:2506–2565`; `Rules/Block_CLI_QTE.txt:152–156` | Ordinary level берётся из GM-authored canonical XP/status; local QTE отдельно converts earned XP. R2 не переносит QTE XP helper на ordinary response и не применяет grant дважды. |

## 2. Request identity отдельно от игровой границы

Предлагается named engine `ActionBoundaryCapability`, issued при genuine original capture до
P0, не из caller skip-флага. Value `action={version:1,actionId,sourceBinding,gameTurn:n}`;
actionId deterministic от physical G/session/incarnation/source original identity. Parent P4
продвигает runtime n−1→n один раз. Decline continuation имеет **другой requestId и snapshotToken,
тот же gameTurn=n и actionId**, segmentOrdinal=1. Request field turnNumber остаётся n; отдельный
requestKind/source label/parent binding отличают его от duplicate old request. Expected-current
entry не использует обычную проверку `_gameLoop.TurnNumber+1`. Ready, repair, daemon, browser
original/main participation и все accepted source readers проверяют полный tuple, а не один n.

Новый proposed activeTail policy=`fixed_tail_b_v2`, version=2; новый common создаёт его только
после отдельного approval/implementation. У e7 proposal нет runtime compatibility obligation;
поддержанные existing v1/v2 INACTIVE contours и их recovery не переименовываются в R2.
В M добавляется closed `actionBoundary={binding,ownerTurnClosure,timeSegments,eventReceipts}`:

- binding — action value выше, full source tuple/G заново доказывается actual original chain;
- ownerTurnClosure — `{state,rosterFingerprint,receipt}`, state=`pending|consumed`. Roster и receipt
  derive из реального prefix common lifecycle contribution/history, не user JSON. Если P0 уже
  consumed n, R2 фиксирует это, не откатывает и не доставляет closure новым effects/targets;
- timeSegments — fixed prefix и one remainder slot `{state,projection,sourceEventRefs,receipt}`;
  projection is the explicit resolved/unresolved owner union in §3, never a mandatory interval.
  Prefix actual owner results frozen; remainder появляется из actual selected/ordinary result,
  неизвестная duration или calendar-to-minutes conversion не выбирается в P0;
- eventReceipts — closed immutable prefix plus bounded remainder owner-produced event set
  (не append-unbounded log). Structural operation keys от source/unit/owner ordinal независимы
  от request retry и human labels. Data cohorts включают M ack; hashes не authority.

**Immutable seal map (O01 precision):** engine capture S includes detached
`actionBoundarySource={binding,sourceRoster,closureInputRefs,prefixTimeInputs,encoderVersion}`,
where every ref/image is derived from actual original/current submitted/common planned source
roles. These fields enter raw B as `basis.actionBoundarySource` **before** CommonAuthority(S,B,C),
input/prepared/cache/take seals. C supplies actual planned closure/event/time contribution, not
caller maps. After take, common immutable proof core authenticates those plan inputs/results;
`basisFingerprint` covers this source member and common proof core, excludes mutable phase acks.
Sibling actionBoundary is only the deterministic initial projection + permitted action/ack overlays.
Cold read reconstructs S/B/C from genuine child→FD→parent originals/retained artifacts and owner
plans, rederives initial roster/closure/time, then verifies every fixed prefix/remainder receipt.
No mutable ack, M/root/basis fingerprint or final common digest is fed back into earlier seals.
Unknown future remainder data enters only its later acceptedBasis/terminal.basis, never P0 seals.

Source owner выдаёт `ActionEventPlan` из capability и complete accepted result. Event classes:
`owner_turn_end`, `elapsed_interval`, `accepted_operation`, `source_condition_target_change`,
`scene_closed`. Существующие effect/resource/wound owners по-прежнему validate/apply actual
lifecycle, триггеры, condition/source/realm loss; R2 не удаляет весь lifecycleEvents array.
Отсутствующее/foreign closure proof означает HOLD/defect, а не произвольное suppress.

Для уже consumed owner turn n composer **не emits новый owner_turn_end**, включая newly born
effects/NPC targets после prefix. Это admission перед strict scheduler, а не перехват replay
error и не fake processed history. Если closure ещё pending, ровно один настоящий owner делает
её action+ack и фиксирует actual target roster; другой request не получает второй budget.
New accepted operations внутри того же действия имеют отдельные source/unit/operation refs;
их нельзя выдавать за `turn_n:accepted_effect` родителя. Не менять old accepted event strings
ретроактивно: новое typed continuation-event family требует explicit composer/source grants.
Новый реальный player action после completion получает n+1 обычным владельцем.

## 3. Время, реальные действия и accept/decline

Prefix accepted time/cost already consumed. FD/FT/Ready/repair/retry/request UTC create no
game time. Actual elapsed duration, a resolved absolute setting and an accepted calendar-only
setting are different source results. No absolute jump is automatically a duration or tick.

Closed `projection` union (same in prefix/remainder; pending remainder projection=null):

- `resolved_incremental={kind,sourceRefs,deltaMinutes,canonicalBeforeMinutes,canonicalAfterMinutes,
  effectCurrentTime,progressionResult}`. Kind literal=`resolved_incremental`; delta from actual
  validated timeChange/elapsed-operation evidence, not UTC or an absolute date difference.
  Both canonical minute coordinates and effectCurrentTime remain nullable according to their
  real owning readers; ordinary timeChange-only is accepted even without an absolute epoch.
  Progression still uses its genuine fallback+delta/control/report rules. A numeric interval
  exists only when both endpoints are actually resolved; no missing-epoch repair/guess.
- `resolved_numeric={kind,sourceRefs,canonicalBeforeMinutes,canonicalAfterMinutes,
  effectCurrentTime,progressionResult}`. Kind literal=`resolved_numeric`; canonicalBeforeMinutes
  nullable only when original actual numeric time unresolved, never guessed from report/UTC;
  after/effect values follow their actual canonical readers; progression is separately preserved,
  not forced equal to the effect coordinate. `progressionResult` is exact
  owning schedule/control/report result, not an independently minted time authority. Only an
  actual elapsed operation with numeric endpoints yields [from,to]; endpoint changes alone
  do not prove elapsed. Numeric absolute forward jumps/rewinds use existing owner policies.
- `unresolved_absolute={kind,sourceRefs,calendarImage,hasUnresolvedAbsoluteOverride,
  effectCurrentTime,progressionResult}`. Kind literal=`unresolved_absolute`, flag=true,
  effectCurrentTime=null, calendarImage is exact accepted raw/calendar before/after source
  binding. **This is an accepted ordinary result**, not malformed/unsupported or repair because
  minutes are absent. Progression preserves existing fallback/max(report/control) handling;
  numeric fallback is not promoted to elapsed/canonical effect time. Genuine effect owner
  preserves nullable CurrentTime behavior (until_time no_change), no invented zero interval.

For ordinary no new time command, carry the exact post-prefix owner state/source, not the old
incremental timeChange again. Absent/unresolved baseline retains its owning fallback/null policy;
no new time operation is emitted. Every projection source is retained as exact images and real
owner plan refs in the accepted basis, not merely a self-declared number/hash.

Source evidence: `ValidationService.QuestsRivalsFactionsAndWorld.cs:1831–1875` explicitly allows
calendar-only setWorldTime; `.MortalJointInputs.cs:84–91,125–142` skips numeric validation and
applies unresolved fallback/report/control max; `.MortalJointBasis.cs:167–186` preserves the flag.
`EffectAcceptedTurnInputComposer.cs:707–737` yields null and `EffectLifecycleScheduler.cs:770–787`
no_change for until_time without CurrentTime. `Rules/Block_23.A.txt:19–40`/`Block_2.txt:668–679`
permit plot/calendar skips and time travel. Their existing syntax/realm validation remains;
**absence of currentTimeInMinutes is never a new validation/repair reason**. A stronger calendar
conversion/elapsed interpretation would require a separate explicit design/grant, not D9 silently.

Real until-time expiry, periodic due work, source/target/condition loss, usage/on-hit and
scene-close still run by actual owning source events once with causal receipts. On a genuinely
resolved forward elapsed interval, consumed T≤T1 is not replayed and actual due work after T1
is preserved, with exact owning boundary semantics. On resolved absolute/rewind use the current
owner's endpoint/cycle policy; on accepted unresolved use current fail-open/null policy. No extra
owner-turn closure from decline, no blanket lifecycle suppression. Real zero-duration movement
still removes location-bound effects. New authorized hit/use is a new event; source action is
not resubmitted. Only a new real player action after release creates the next action boundary.

| Одна ситуация / одинаковый actual event trace | Accept (D8 branch) | Decline (ordinary branch) |
|---|---|---|
| request/UI/wave/retry | 0 новых game turns/time/cost | 0 новых game turns/time/cost |
| prefix n owner-turn closure consumed | не emits повторно | не emits повторно, request.turnNumber=n |
| zero remaining duration, no new operation | no extra turn/timed ticks | то же, valid empty result accepted |
| real +5min, expiry due within interval | time owner обрабатывает due interval once | тот же time owner/interval policy |
| calendar-only/plot setWorldTime without numeric minutes | separately granted D9 projection follows existing fail-open/null owner behavior | lawful ordinary result accepted once, same fail-open/null policy; no forced interval/repair |
| resolved absolute jump/rewind | existing endpoint/cycle policy, no guessed elapsed | same policy and source proof, no transport duration |
| real on-hit/use/target/source change | existing typed causal owner once | existing typed causal owner once |
| effect появился после consumed prefix closure | не retro-tick старую границу | то же; eligible следующая настоящая граница |
| исход/время реально различаются из-за выбора | actual authored trace, реальные расходы | actual ordinary trace, реальные расходы; различие не transport penalty |
| story/runtime | source P4 n остаётся; D8 terminal history/output, no second P4 row/IncrementTurn | source P4 n остаётся, separate resolution entry n; no IncrementTurn |

**D9 proposal:** shared action/event/time owner требуется и для QTE terminal, и для ordinary
continuation. D8 responseFragment whitelist/damage/earned-XP limits **не расширены**. Time data
не прячутся в запрещённом terminal field: scene/source timing evidence получает отдельный typed
D9 contribution, pure composed с D8 selected outcome до terminal commit. Новый exact union
cohort с world_time и вызванными им existing typed contributions требует отдельного grant.
Без D9 acceptance обе ветви нельзя считать parity-qualified. D8 не выдаёт ordinary XP rights.
Полный ordinary result universe не значит полный QTE payload universe: сценарий с обязательным
quest/location outcome, непредставимым в D8, требует отдельного QTE design delta до предложения
такого QTE. Decline всё равно обязан поддерживать любой законный ordinary result; нельзя
«решать» D8 ограничение урезанием обычного исхода.

## 4. Полнота ordinary outcomes и границы realm/source

182-name inventory ниже — **coverage inventory**, не write allowlist/новые grants. Он различает
169 top-level GameResponse DTO names, 176 FileMapping names/83 primary paths, их union 182;
четыре output-only и два client-consumed имени не входят в 176. PlayerStatus.activeConditions/
currentCondition — nested fields, не дополнительные GameResponse outcomes. QteOffer и
ink_feather_action_result — отдельные output/control routes, не DTO properties.
13 mapped names отсутствуют в DTO: roundtrip через GameResponse может потерять их; raw-file
ordinary route и actual domain readers проверяются отдельно. Нельзя объявить каждый mapped
field законным в Mortal только потому, что у него есть destination.

Family codes в inventory:

| Код | Owning transformation/admission / derived companions | Eligibility |
|---|---|---|
| P | StateDistributor field merge; StateManager/CharacteristicsService; ordinary XP/status/skills/transformation/custom/stealth validators; normalizer Skills.cs | Mortal legal/conditional ordinary progression/identity rules, не новые формулы |
| E | AcceptedMechanicsInput/Registry, Effect/Wound/Resource accepted planners, normalizer AcceptedMechanics.cs/Wounds.cs/Effects.cs | Actual source/target, catalogs, identity/history/resources/carriers/commands, specialized treatment taken authority сохраняется |
| I | MortalItemAcceptedTurnPlanner/normalizer MortalItems.cs, MortalItemTransfers.cs, InventorySidecars.cs; identity/content/storage companions | Trade/transfer/text/item generation — genuine ordinary owners, no direct ID/receipt mint |
| L | MortalLocationAcceptedTurnPlanner + normalizer MortalLocations.cs; location identities/storage/world-map/current projection | Перемещение и новые locations, item/location dependency graph before publication |
| W | world/time/weather/progression inputs + ProgressionScheduleService; normalizer QuestsAndRivals.cs for rival arcs | timeChange/setWorldTime/world events/flags; due real cycles once |
| Q | normalizer QuestsAndRivals.cs, QuestRewardAuthority + quest validation | Regular/soul quest and history/plot, rewards через их реальных owners; no double claim |
| N | normalizer Npcs.cs + NPC skill/relationship/goal/inventory/mask/activity validators | NPC core/id/scene/activities/journals/etc; effect-target exports same complete plan |
| F | normalizer Factions.cs/FactionAndInventoryHelpers.cs + faction validators | Core/structure/resources/projects/custom/chronicles, actual actor links |
| C | normalizer SoulAndMeta.cs/SharedAndSoulHelpers.cs/SarefMainStory.cs + specific pending/realm contract validators | Conditional cross-domain/meta surface; narrow guardianQuestProgress exception, no ordinary Guardian write bypass |
| R | Afterlife normalizer partials and their pending/source/realm owners | Not Mortal ordinary just by mapping; only lawful dedicated target-realm handoff, no realm segregation relaxation |
| X | vehicle/storage/access/multiplier/player-interaction owner validators + StateDistributor exact merge | Lawful ordinary misc fields; companion/source/cleanup inventory must be explicit |
| H | GameEngine lifecycle CheckLifeTransitions/CheckAscensionTrigger/CheckGmIncarnationTrigger + exact pending/source owners | Legal outgoing control receipt, затем отдельный genuine handoff; не прямой вход в next realm |
| O | BuildGameResponseFromFiles, StoryService, StateDistributor output encoders | narrative/interface/debug visibility; no QTE re-offer as resolution, actual input/result retained |

P includes XP/level and ordinary reward production, **not** training-cap XP compensation writer.
Existing ordinary GM canonical experience/status semantics must be extracted from ordinary
contract (Rules/Block_5/17, validators/source images), not replaced by QteSceneService's local
ApplyAuthoritativeExperienceAsync. R2 receives canonical ordinary XP result + source operation,
then named automatic awards only newly uncovered levels versus parent watermark. Currency,
item/quest rewards, statsDecreased/extraordinary setCharacteristics keep current governing rules;
no blanket stat bypass or unchanged total-XP double addition. Accepted XP and derived level/points
contributions form one acyclic plan; final projection follows both, D4 allocation still deferred.

Scope is every **currently lawful** ordinary Mortal outcome plus its already governed outgoing
lifecycle controls. Existing forbidden afterlife/pending/debug-only operations remain forbidden
on wrong source/realm. Dedicated target-realm owners remain separate with exact authority.
Canonical-only GM-authored roots beyond DTO/map are covered by their current accepted family
read/write/source inventories (world profiles, item text content, definitions etc), never by
unchecked namespace keys. Client-owned histories/receipts/computed state/signing/G files stay
owner-produced/guard-only. Future literal owner inventory reconciliation is required for each
family, including dynamic generated paths under its existing confinement validator.

**Reply-dependent generated paths (O02 precision):** PendingTurnSnapshotReader:648–669,
961–968 separates selected bytes, authenticated absence and mere declaration. A missing map key
or current candidate is never absence authority. T193 extends genuine original source capture
for applicable owners: before FT, each owner freezes a complete finite domain catalog of actual
members/digests + exact confinement-contract version into signed child snapshot artifacts.
Named proposed `OrdinaryGeneratedPathSourceAuthority` privately derives a reply-selected path
from a validated owner command and that actual signed catalog: existing member requires selected
original bytes; a new member requires owner-proven complete catalog coverage and nonmembership
under its confinement validator. No generic namespace/wildcard write grant: only that genuine
owner's exact accepted plan may introduce the particular generated path. An uncovered path has
no proof; before feature activation all currently legal generated-path forms must have a real
owner catalog/selected-absence adapter. This is new proposed D10 original/read-absence authority,
not an assumption that current declaredOriginalLogicalPaths already grants it. Cold readers
reconstruct it from signed original artifacts/G; foreign, incomplete, merely declared or edited
catalogs fail. Snapshot original owner supplies artifact membership/encoding in the same FT;
no new storage service, wildcard journal or keys from GM decide membership.

## 5. Minimum reliable architecture and alternatives

| Вариант | Надёжность/объём / решение |
|---|---|
| **R2 recommended: F1 envelope + complete typed ordinary composite + shared D9 boundary** | M и existing PublishLocalFilesAsync остаются storage owners. Pure extraction всех применимых ordinary domain transforms, complete source proof, one taken result cohort+M, named post-tail/handoff. Это заметно шире e7 three-family F1, но сохраняет весь законный ответ и accepted atomicity. |
| Owner-local durable receipts (bounded C expansion) | Те же full family/event/source obligations; больше shared schemas/readers/retirement/partial-visibility coordination. Может улучшить reuse обычным ходом. Не даёт automatically full atomic accepted result, нужны общая decision/visibility boundary и proof. |
| Legacy StateDistributor/normalizer + snapshot/catch wrapper; restricted family/refusal | Не удовлетворяет: sequential source writes переживают process death, catch не crash recovery; whitelist/refusal/drop скрывает законный ordinary исход. Не рекомендуемый запасной gameplay путь. |

**D10 proposal:** fixed built-in `OrdinaryOutcomeOwnerRegistry` groups actual fields/roots by
these domain owners; это code-owned registry, не registry JSON от GM. Every lawful field and
canonical-only command has one owner and disposition `applied|delegated|no_change`, no skipped
field. Дефекты/новые неизвестные поля получают обычный validation/repair с сохранением полного
candidate. Нормальный валидный reply не может стать rejected_unsupported_family.
Отсутствующий adapter — implementation/release blocker до activation, не средство разрешить
уже accepted ситуацию. Если такой баг всё же найден in-flight, сохранить весь result/source и
обязательство для исправления; это failure (future acceptance test FAIL), не successful HOLD
или разрешённая семантика ответа. No second QTE вместо ordinary resolution.

Complete plan строится pure в normalizer F1 owner: location/item source identities → quest/NPC/
faction/meta/source exports → effect/wound/resource/common and authorized rewards → exact
world-time/event consequence contribution → ordinary canonical XP/stats/automatic/projection
inputs → story/output and outgoing handoff intent. Это **dependency DAG** реальных readers, не
новый постоянный универсальный fixed order из списка; actual overlapping paths соединяет
один explicit owning compositor. Два writers одного root без named composition — reject plan
before publication, затем repair/recompose сохранённого result, а не выбрасывание одного writer.
All family reducers получают detached genuine original/post-prefix/current candidate snapshots;
не запускают production mutating normalizers в shadow FS/copy и не изменяют canonical namespace
во время planning. Existing StateDistributor pure merge/encoders извлекаются с теми же special
field/null/ordered array/UTC rules; не использовать его Unknown skip или DistributeAsync.

**D11 proposal:** privately issued `OrdinaryOutcomeTakeCapability` binds complete source input,
request/parent/action tuple, owner plan seals, cross-owner DAG, all candidate fields/operation
receipts, exact finite data before/after/absence maps и outgoing handoff. Existing specialized
item/location/wound/treatment authorities сохраняются внутри него, не impersonated by common DTO.
`ValidatedEmpty` — proven nonempty resolved units с no new canonical consequences; zero family
contributions + output/story ack, no invented wound/quest/XP. NoPlan/null != Empty capability.

PREPARE сохраняет actual known complete result/identity/time/UTC in fixed child acceptedBasis
M slot; неизвестный исход в P0 не frozen. COMMIT — one complete owner-produced result cohort+M
ack through existing publisher, guard all untouched original paths. No namespace wildcard,
FileMapping.Values/rollback inventory grant or «<=N files» approximation. Family companions,
consumed command paths и generated content paths имеют exact owner proofs. Automatic/projection/
story могут оставаться named later action+ack phases B с frozen basis и complete readback; UI
accepted result and next player input раскрываются только после required completion/handoff.
Полные accepted semantics не выводятся из числа полей или наличия файла.

## 6. F1 source/request/dedup/repair/release protocol (R2)

GM authoritatively declares непустой remaining question до P0; клиент закрепляет structural
origin inventory/partition, committed operation ledger и action boundary. Descriptor R2:
`ordinaryResolution={version:2,situationId,unresolvedUnits}`; each ordered unit
`{unitId,sourceRowRef,description,ruleRefs,knownTargets}`. Никакого three-family consequenceSlots
allowlist или заранее угаданного списка outcomes. Actor/location creation может introduce new
canonical IDs only through actual owner plans after reply; source labels не authority.
Result `ordinaryResolutionResult={version:2,obligationId,units}` связывает actual answer per unit,
ordinary check/deterministic evidence и contribution refs по всем owners. Numeric audit bounded
existing Rule12 profile; human causal/narrative judgment не превращается в hash/prose oracle.

Structural coordinate сохраняет `{originBinding,producerKind,sourceOrdinal,operationOrdinal,
family,targetKind,targetId}`, family теперь actual built-in ordinary domain enum из registry,
не свободная строка. Unit labels/new requestId/new item/quest/wound IDs не меняют root cause.
Candidate owner capabilities derive every contribution from admitted unit+actual outcome+owner
operation ordinal. Existing legitimate distinct operation on same target remains allowed.
Complete committed ledger includes time/currency/XP/items/quests/movement/lifecycle/event receipts,
не только wounds; before/after comparisons и source keys guard double reward/transfer/time.
Не выводить equivalence из prose; GM отвечает за исходную игровую classification, ambiguous
source inventory требует уточнения до принятия source, не обещание магического semantic oracle.

**FD:** choice+one obligation+parent exact headers/source ledger+action boundary+reservation
publish in M with own decline cohort. Reservation `{requestId,gameTurn:n,requestTimestamp,
dicePool,computedContextFingerprint}` known at FD; pure fresh normal-check pool, gacha=null,
parent/used checks not rerolled, pending_dice_state guard-only. Before FD no exposure; after FD
exact values survive FT rollback. New dice source is transport for unresolved checks, not tick.
**FT:** exact parent→child original/request slot switch, one batch incl distinct snapshot/backups,
M transfer ack and only owned controls. Current child pair uses new requestId, **same turn n**,
sourceLabel=mortal_joint_ordinary_resolution, parentBinding/actionId/segmentOrdinal=1/obligation.
Immutable child snapshot contains FD M image (with reservation) → child manifest/token → FT;
acyclic, no self hash. Parent artifacts retained. No unchanged destructive CreateSnapshot/Prepare.
**Cold proof:** current physical G→child headers/request→frozen FD→parent headers/artifacts→common/
P1–P4/action closure→current complete outcome overlays. Hash/capsule/released M alone not authority.
Source capture full ordinary owner catalogs before candidate seal; new reply-dependent reads
use child original/validated candidate, not rewritten P0 source basis or mutable current fallback.

**REPAIR:** before result COMMIT, restore only rejected GM-owned candidate paths to post-prefix
child baseline under actual family ownership, bound child request/obligation/attempt; M, G,
headers/request/reservation, FD proof artifact and source/phase receipts guard-only. Atomic
candidate restores+repair control+M repair ack. Never generic RestorePreTurnBackup over FD M.
After accepted result phase, no generic restore or GM resubmission; publisher debt/known basis
recompose/resume only. Same request and dice, preserve full unknown/malformed candidate for
ordinary repair, do not strip valid siblings/new outcomes. Every retry unit result remains due.

**STORY:** one immutable parent P4 row turn n + one append-only `entryKind=action_resolution`,
`actionId`, `requestId`, `segmentOrdinal=1`, **turn=n**. Separate exact story witness/source kind;
parent initial labels/narrative stay frozen, live output = accepted remainder. No parent rewrite,
no fake ordinary schema1 writer, no second IncrementTurn. Story readers/export/context/UI must
group same action continuation and derive runtime from logical turn, not row count; all readers
must be reconciled before release. One result row does not mean second action/owner-turn budget.
Concrete story consumers: `StoryService.cs:168–211` currently displays physical EntryCount;
`:217–294` reads last-N rows/system-reminder summaries; `GameEngine.MainMenu.cs:528+` exports
stories; `GameEngine.BrowserPlayerInput.cs:420–442` binds source/request and unique row;
`StoryService.AcceptedStoryAppend.cs:65–80` accepts only schema1 console/browser writers.
T196 must add a separate F1 witness/read branch, preserve those old restrictions and reconcile
entry counts/labels and context truncation so both rows of the one action remain interpretable.
No evidence is claimed that an unchanged reader already supports action_resolution.

**COMPLETION:** complete composite result receipt + event/time acknowledgements + automatic/
projection/story + no repair/publisher debt, or typed outgoing lifecycle handoff as below.
**KF1:** parent artifact-only K ∪ child K, shared current headers/request/controls deleted once
under child tuple, M retained released ack; source artifacts only after completion/handoff proof.
No partial delete completion, old source resurrection, mechanics/history/story deletion.

## 7. Outgoing lifecycle results (H)

В текущем Mortal source законный outgoing lifecycle — TriggerLifeEnd (с authoritative trigger/source
validation). `CheckGmIncarnationTrigger:3046–3050` допускает только Chaos Sea/Shining bootstrap,
`CheckAscensionTrigger:4192–4195` только eligible afterlife; эти два mapped controls **не** получают
Mortal admission. Conditional pending outcomes проходят их existing realm/stage checks. Валидный
переход не блокируется на основании «F1 supports only wounds». Outgoing typed family plan
сохраняет control intent и actual accepted result once. Raw trigger flag не completion proof.
Предлагается `F1OutcomeHandoffCapability={sourceAction,sourceBinding,obligationId,resultReceipt,
kind,targetOwner,operationId}` issued by genuine completed outcome owner; это новая capability,
не capability из JSON. Existing GameEngine lifecycle owner must accept it through a named
F1 entry, preserving control/transition once-only semantics and source retention.

Prepare target operation/acceptance receipt and source transfer ack under current owning session;
current source artifacts/current G retained until **actual target owner accepted handoff**, not
UI redirect. Target owner must expose durable accepted request/transition receipt, exact input
binding, retry/recovery and owned cleanup; source M stores that receipt binding. Then KF1 may
release source before destination work starts only if target receipt proves it owns all remaining
work. If target installation replaces G/session/incarnation, named target-side owner verifies
its current G and source handoff capsule; old G remains correlation, not a mutation grant in
new generation. Do not insist on old physical G after lawful generation transition and do not
rebind a generic old marker. Destination player action count/reset follows existing realm owner;
F1 transport never adds a Mortal tick. No new Life Evaluation/afterlife writer granted here.

**Concrete dependency:** existing CheckLifeTransitions/CheckAscensionTrigger/CheckGmIncarnationTrigger
and their cleanup paths are not already this durable handoff API. Family H cannot be marked
qualified until the specific target owner protocol and current-generation receipt/cleanup are
independently designed, implemented and verified. Current document enumerates necessary contract;
T197 задаёт exact MortalDeath→LifeEvaluation target cohort/receipt before implementation release;
иные realm transitions потребуют самостоятельного lawful source design, не активируются из Mortal. This is
an explicit remaining design dependency for outgoing lifecycle qualification, not proof by
«call existing handler», automatic F2 selection or permission to discard the transition.

Source target inventory: `CheckLifeTransitions:2695–2810` пишет death story marker, вызывает
`UpdateSoulStateRealmWithPublicationAsync(MortalDeathToChaosSea)`, refresh/clear world lore,
удаляет life_transitions/Ready, затем создаёт original/request с
`LifeEvaluationRewardAnalyzer.AutomaticLifeEvaluationSourceLabel`, fresh dice/gacha и Chaos Sea
progression control. Далее raw response validate/repair, increment и Life Evaluation story,
reward display, return guard, possible incarnation, cleanup. Эти steps **не** образуют один
существующий handoff receipt. T197 должен до activation извлечь pure prepare target snapshot/
request и определить точные owner-produced realm-transition/marker/control/data cohorts,
durable prepared/accepted target ack и source release cut; UI death prompt вне lease. После
receipt destination owner продолжает normal Life Evaluation, включая её genuine отдельный
Chaos Sea turn, awards и return guard. Этот настоящий target turn не следует suppress ради
«same Mortal n»; extra Mortal transport tick по-прежнему отсутствует. Не смешивать full
Life Evaluation execution с atomic acceptance of remaining-work ownership.

## 8. Proposed grants and release decision

D9 shared event/time action-boundary, D10 full ordinary owner contribution registry and D11
complete take/Empty/handoff/stories are substantial proposed extensions to e7 F1. They do not
follow from the user's «да» to DESIGN. No new storage service/path/journal, migration or OS
privilege/security change. M remains fixed phase coordinator; registry lives in code and exact
source catalogs. D4 ordinary allocator, guided US5 and actual DOM/visual/platform qualification
remain separate. D8 grammar/earned-XP/damage scope stays narrow; no training-cap XP compensation.

Minimal reliable release requires all **applicable** ordinary family adapters and full input/
output coverage, shared accept/decline event-budget/time protocol, Plan/Empty, repair/cold proof
and any eligible outgoing handoff owner. A smaller internal no-offer slice may be reviewed,
but cannot activate QTE with a restricted or nonfunctional ordinary decline. Existing refusal
before common is a current protection until feature ready, not the user-facing solution.
Return for written contract/plan/grant discussion after independent review; **STOP before code**.

## 9. Future verification — all UNRUN

| ID | Scenario / oracle |
|---|---|
| R201 | Same source/action, accept vs decline, zero duration with per-turn poison/remainingTurns; old and newly born effects/NPC targets. Exactly one owner-turn closure n, no extra damage/expiry, no synthetic n+1. |
| R202 | Boundary pending vs consumed; missing/forged/foreign receipt, changed roster/G before child constructor. Genuine one close or HOLD, never caller skip flag/fake processed history. |
| R203 | T0→T1 prefix + T1→T2 genuine elapsed; numeric absolute jumps/rewinds and lawful calendar-only unresolved command; until_time/periodic boundaries; usage/on-hit/scene/condition/source loss. Preserve real due effects once, no blanket lifecycle suppression; equivalent event traces same mechanics. Calendar-only accepted with actual fallback/null effects; no forced minutes/repair/guessed delta. |
| R204 | New requestId/token, same turn n; lost Ready/dispatch/FD/FT rollback, same/distinct-root cold recovery. One action/obligation, same dice/UTC, stale parent signal cannot bind child. |
| R205 | Real movement plus location identities/storage transfer, location-bound effect expiry at zero duration. Actual canonical location changes, every companion/source edge valid, no extra turn. |
| R206 | Quest-only progress/completion/quest history and lawful item/currency/XP reward; repeat/crash. Actual ordinary outcome accepted, reward claimed once; parent reward/time unchanged. |
| R207 | XP jump, stats increase/decrease/extraordinary allowed set, NPC/faction/item/meta combined result. Ordinary semantics/source authority retained, parent watermark not re-awarded, final projection agrees, D4 allocation blocked. |
| R208 | Every 182-name inventory row has exact disposition/schema/actual source eligibility; DTO-missing/raw-file commands, nested PlayerStatus, canonical-only derived/dynamic companion roots. New generated path proves selected original bytes or genuine owner-covered signed complete catalog absence; mere declared/missing key/current candidate fails. No silent loss or FileMapping grant; realm-forbidden examples stay rejected. |
| R209 | Same multi-family response under different field order; two owner plans overlap root; dependent location/item/quest/NPC/effect exports. Identical allowed DAG result or explicit ownership error, no last-writer winner. |
| R210 | Pure extraction negative controls (no IO), every complete outcome batch member/M cut and lost caller return. Complete accepted before/after or publisher HOLD, never sequential partial PASS or caught-exception compensation claim. |
| R211 | ValidatedEmpty ordinary answer at same n; complete unit/check/explicit no-consequence proof, empty/null plan or notification alone. Valid result accepted and story appended once; no forced wound/XP/time. |
| R212 | Invalid first child reply after FT, repair mixed valid/new outcome siblings, repeated repair/crash, attempted repair after result/story ack. M/headers/request/action/dice retained; full result not silently trimmed, no FD rollback. |
| R213 | Real eligible Mortal life-end handoff; wrong-realm ascension/incarnation negative controls; conflicting/foreign trigger/control, target G replacement, lost target acknowledgement. One accepted outgoing result/target request; source artifacts retained until genuine owner takeover, old G no new authority. |
| R214 | Parent story n + resolution n through console/browser/agent/history/export/context/reload. Row count not turn count, source P4 immutable, current result visible, next real player action n+1 only. |
| R215 | Repeated same root-cause contribution with relabelled request/event/item/quest/wound IDs/prose, vs genuinely distinct authorized op same target. Structural dedup once across all owners, no semantic prose oracle claimed. |
| R216 | D8 unsupported raw terminal fields remain rejected; separately granted D9 same actual duration/event trace in accept and decline. No ordinary quest/location/XP permissions leak through responseFragment; no decline-only time penalty. |
| R217 | New valid family appears in ordinary reply after P0 (quest/location/NPC/reward). No unsupported-family rejection/drop/reoffer QTE; all completed dispositions, actual canonical result, implementation missing-adapter bug fails qualification. |
| R218 | Linux/Windows native same fault matrix, real console/browser and DOM/visual; source/binary pins/full TRX/owned roots/cleanup. Neither metadata nor other OS result counts as runtime PASS. |

Every native test remains future/unauthorized now. Finite owners and applicable Late2/independent
SOURCE/FIXTURE/SELECTION/EVIDENCE gates precede actual runs; no full-suite/Fast/PreMerge or every-
category loop. New category membership and counts must be discovered later, not invented here.

## 10. Literal response inventory (static, not runtime qualification)

Sources below are exact e7 production bytes. Paths in field rows are primary mapping destinations;
full read/write/delete companions come only from typed owner plans. Family codes P/E/I/L/W/Q/N/F/
C/R/X/H/O are defined in §4; they route owning transformations, not a new broad permission. All runtime checks UNRUN.

| Field | DTO route | Primary destination / explicit consumer | Family |
|---|---|---|---|
| `achievementUnlocks` | yes | `game_state/meta/achievements.json` | C |
| `activeGuardian` | yes | `game_state/meta/guardians.json` | R |
| `activeSkillChanges` | yes | `game_state/player/skills_active.json` | P |
| `activeVehicleChange` | yes | `game_state/misc/vehicles.json` | X |
| `addOrUpdateRecipes` | yes | `game_state/inventory/recipes.json` | I |
| `afterlifeActorActiveMaskChanges` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeActorActivityUpdates` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeActorGoalUpdates` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeActorMaskAdds` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeActorMaskRemovals` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeActorMaskUpdates` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeActorQuestUpdates` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeArchiveUpdates` | yes | `game_state/meta/soul_state.json` | C |
| `afterlifeBreakthroughQuestUpdates` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeChronicleUpdates` | yes | `game_state/meta/afterlife_chronicles.json` | R |
| `afterlifeEntityCustomStateChanges` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeEntityProfiles` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeEntityProfileUpdates` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeEntityProgressionOverrides` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeFateCardUnlocks` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeGlobalFlagUpdates` | yes | `game_state/meta/afterlife_global_flags.json` | R |
| `afterlifeRelationshipChanges` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeRelationshipLockUpdates` | yes | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeSpecialArtLearningReceipts` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `afterlifeSpiritualConflictUpdate` | yes | `game_state/meta/afterlife_spiritual_conflict_state.json` | R |
| `afterlifeStoryOutline` | **raw mapped only** | `game_state/meta/afterlife_story_outline.json` | R |
| `afterlifeThreatsToAdd` | yes | `game_state/meta/afterlife_active_threats.json` | R |
| `afterlifeThreatsToRemove` | yes | `game_state/meta/afterlife_active_threats.json` | R |
| `afterlifeThreatsToUpdate` | yes | `game_state/meta/afterlife_active_threats.json` | R |
| `alliesData` | yes | `game_state/combat/allies.json` | N |
| `archiveActionResolutions` | yes | `game_state/meta/soul_state.json` | C |
| `AscensionTrigger` | yes | `game_state/control/ascension.json` | H |
| `calculatedWeightData` | yes | `game_state/player/weight_calc.json` | P |
| `chaosSeaNavigation` | yes | `game_state/meta/guardians.json` | R |
| `characterChronicleUpdates` | yes | `game_state/meta/character_chronicle.json` | C |
| `combat_log_markdown` | yes | `game_state/combat/combat_log.json` | N |
| `completeAfterlifeActorActivities` | **raw mapped only** | `game_state/meta/afterlife_entity_profiles.json` | R |
| `completeAfterlifeThreatActivities` | yes | `game_state/meta/afterlife_active_threats.json` | R |
| `completeFactionProjects` | yes | `game_state/factions/faction_projects.json` | F |
| `completeGuardianPoliticalProjects` | yes | `game_state/meta/chaos_sea_guardian_politics.json` | R |
| `completeGuardianProjects` | yes | `game_state/meta/guardian_projects.json` | R |
| `completeNPCActivities` | yes | `game_state/npcs/npc_activities.json` | N |
| `currentLocationData` | yes | `game_state/world/current_location.json` | L |
| `customStateChanges` | yes | `game_state/player/custom_states.json` | P |
| `dialogueOptions` | yes | `output/interface_updates.json` | O |
| `effectChanges` | yes | `game_state/effects/effect_commands.json` | E |
| `effectEventReports` | yes | `game_state/effects/effect_commands.json` | E |
| `effectResolutionReceipts` | yes | `game_state/effects/effect_commands.json` | E |
| `enemiesData` | yes | `game_state/combat/enemies.json` | N |
| `experienceGained` | yes | `game_state/player/experience.json` | P |
| `factionBonusChanges` | yes | `game_state/factions/faction_structure.json` | F |
| `factionChronicleUpdates` | yes | `game_state/factions/faction_chronicles.json` | F |
| `factionCoreChanges` | yes | `game_state/factions/faction_core.json` | F |
| `factionCustomStateChanges` | yes | `game_state/factions/faction_custom.json` | F |
| `factionDataChanges` | yes | `game_state/factions/faction_core.json` | F |
| `factionProjectUpdates` | yes | `game_state/factions/faction_projects.json` | F |
| `factionRankChanges` | yes | `game_state/factions/faction_structure.json` | F |
| `factionResourceChanges` | yes | `game_state/factions/faction_resources.json` | F |
| `gm_thoughts_markdown` | yes | `output/debug_logs.json` | O |
| `grantStorageAccess` | yes | `game_state/misc/storage_access.json` | X |
| `guardianPoliticalChronicleUpdates` | yes | `game_state/meta/chaos_sea_guardian_politics.json` | R |
| `guardianPoliticalInfluenceUpdates` | yes | `game_state/meta/chaos_sea_guardian_politics.json` | R |
| `guardianPoliticalProjectUpdates` | yes | `game_state/meta/chaos_sea_guardian_politics.json` | R |
| `guardianPoliticalRelationUpdates` | yes | `game_state/meta/chaos_sea_guardian_politics.json` | R |
| `guardianPowerEvents` | yes | `game_state/meta/guardians.json` | R |
| `guardianProjectUpdates` | yes | `game_state/meta/guardian_projects.json` | R |
| `guardianQuestProgressUpdates` | yes | `game_state/meta/guardians.json` | C |
| `guardians` | yes | `game_state/meta/guardians.json` | R |
| `guardianSocialJournalUpdates` | yes | `game_state/meta/guardian_social_journal.json` | R |
| `guardianThoughtJournalUpdates` | yes | `game_state/meta/guardian_thought_journal.json` | R |
| `historyManipulationCoefficient` | yes | `game_state/meta/player_behavior.json` | C |
| `image_prompt` | yes | `output/interface_updates.json` | O |
| `interNPCRelationshipChanges` | yes | `game_state/npcs/npc_relationships.json` | N |
| `itemBondLevelChanges` | yes | `game_state/inventory/item_bonds.json` | I |
| `itemFateCardUnlocks` | yes | `game_state/inventory/item_bonds.json` | I |
| `itemJournalUpdates` | yes | `game_state/npcs/item_journals.json` | N |
| `loreCodexUpdates` | yes | `lore/codex_entries.json` | P |
| `mathAudit` | yes | `game_state/meta/math_audit.json` | C |
| `mathRequests` | yes | `game_state/meta/math_audit.json` | C |
| `metaStateUpdates` | yes | `game_state/meta/soul_state.json` | C |
| `moneyChange` | yes | `game_state/player/status_changes.json` | P |
| `moveInventoryItems` | yes | `game_state/inventory/item_movements.json` | I |
| `moveToLocationStorage` | yes | `game_state/inventory/storage_operations.json` | I |
| `multipliers` | yes | `game_state/misc/multipliers.json` | X |
| `NPCActiveMaskChange` | yes | `game_state/npcs/npc_masks.json` | N |
| `NPCActiveSkillChanges` | yes | `game_state/npcs/npc_skills.json` | N |
| `NPCActivityUpdates` | yes | `game_state/npcs/npc_activities.json` | N |
| `NPCCoreChanges` | yes | `game_state/npcs/npc_core.json` | N |
| `NPCCustomStateChanges` | yes | `game_state/npcs/npc_custom_states.json` | N |
| `NPCEquipmentChanges` | yes | `game_state/npcs/npc_inventory.json` | N |
| `NPCFateCardUnlocks` | yes | `game_state/npcs/npc_fate_cards.json` | N |
| `NPCGoalUpdates` | yes | `game_state/npcs/npc_goals.json` | N |
| `npcInteractionJournalUpdates` | yes | `game_state/npcs/npc_interaction_journal.json` | N |
| `NPCInventoryAdds` | yes | `game_state/npcs/npc_inventory.json` | N |
| `NPCInventoryRemovals` | yes | `game_state/npcs/npc_inventory.json` | N |
| `NPCInventoryUpdates` | yes | `game_state/npcs/npc_inventory.json` | N |
| `NPCJournals` | yes | `game_state/npcs/npc_journals.json` | N |
| `NPCMaskAdds` | yes | `game_state/npcs/npc_masks.json` | N |
| `NPCMaskRemovals` | yes | `game_state/npcs/npc_masks.json` | N |
| `NPCMaskUpdates` | yes | `game_state/npcs/npc_masks.json` | N |
| `NPCPassiveSkillChanges` | yes | `game_state/npcs/npc_skills.json` | N |
| `NPCPassiveSkillMasteryChanges` | yes | `game_state/npcs/npc_skills.json` | N |
| `NPCPersonalityTraitChanges` | yes | `game_state/npcs/npc_personality.json` | N |
| `NPCQuestUpdates` | yes | `game_state/npcs/npc_goals.json` | N |
| `NPCRelationshipChanges` | yes | `game_state/npcs/npc_relationships.json` | N |
| `NPCRelationshipLockUpdates` | yes | `game_state/npcs/npc_relationships.json` | N |
| `NPCsInScene` | yes | `game_state/npcs/npc_core.json` | N |
| `NPCSkillMasteryChanges` | yes | `game_state/npcs/npc_skills.json` | N |
| `NPCsRenameData` | yes | `game_state/npcs/npc_core.json` | N |
| `NPCUnlockedMemories` | yes | `game_state/npcs/npc_memory.json` | N |
| `otherPlayersInteractions` | yes | `game_state/misc/player_interactions.json` | X |
| `passiveSkillChanges` | yes | `game_state/player/skills_passive.json` | P |
| `playerAppearanceChange` | yes | `game_state/player/transformation.json` | P |
| `playerAutoCombatSkillChange` | yes | `game_state/player/transformation.json` | P |
| `playerBehaviorAssessment` | yes | `game_state/meta/player_behavior.json` | C |
| `playerCharacterNameChange` | yes | `game_state/player/transformation.json` | P |
| `playerChoice` | yes | `game_state/control/ascension.json` | H |
| `playerClassChange` | yes | `game_state/player/transformation.json` | P |
| `playerClassDescriptionChange` | yes | `game_state/player/transformation.json` | P |
| `playerEffortTrackerChange` | yes | `game_state/player/experience.json` | P |
| `playerGuardianFoundationHistory` | yes | `game_state/meta/guardians.json` | R |
| `playerImagePromptChange` | yes | `game_state/player/transformation.json` | P |
| `playerRaceChange` | yes | `game_state/player/transformation.json` | P |
| `playerRaceDescriptionChange` | yes | `game_state/player/transformation.json` | P |
| `playerStatus` | yes | `game_state/core/player_status.json` | P |
| `playerStealthStateChange` | yes | `game_state/player/stealth.json` | P |
| `plotOutline` | yes | `game_state/quests/plot_outline.json` | Q |
| `progressionProcessingReport` | yes | `game_state/control/progression_report.json` | W |
| `questLog` | yes | `game_state/quests/quest_history.json` | Q |
| `removeActiveSkills` | yes | `game_state/player/skills_active.json` | P |
| `removeInventoryItems` | yes | `game_state/inventory/item_removals.json` | I |
| `removePassiveSkills` | yes | `game_state/player/skills_passive.json` | P |
| `removeRecipes` | yes | `game_state/inventory/recipes.json` | I |
| `removeVehicles` | yes | `game_state/misc/vehicles.json` | X |
| `removeWorldStateFlags` | yes | `game_state/world/world_flags.json` | W |
| `residentInteractionLogUpdates` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `residentThoughtJournalUpdates` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `resourceCapacityChanges` | yes | `game_state/resources/resource_commands.json` | E |
| `resourceChanges` | yes | `game_state/resources/resource_commands.json` | E |
| `resourceDefinitionCreations` | yes | `game_state/resources/resource_commands.json` | E |
| `response` | yes | `output/narrative_response.json` | O |
| `retrieveFromLocationStorage` | yes | `game_state/inventory/storage_operations.json` | I |
| `revokeStorageAccess` | yes | `game_state/misc/storage_access.json` | X |
| `sarefMainStoryState` | **raw mapped only** | `game_state/meta/main_story_saref_state.json` | C |
| `sarefMainStoryUpdate` | **raw mapped only** | `game_state/meta/main_story_saref_state.json` | C |
| `setCharacteristics` | yes | `game_state/misc/characteristics.json` | P |
| `setWorldTime` | yes | `game_state/world/world_time.json` | W |
| `shareStorageAccess` | yes | `game_state/misc/storage_access.json` | X |
| `shiningFactionChronicleUpdates` | yes | `game_state/meta/shining_abode_state.json` | R |
| `shiningFactionInfluenceUpdates` | yes | `game_state/meta/shining_abode_state.json` | R |
| `shiningFactionResourceLedgerUpdates` | yes | `game_state/meta/shining_abode_state.json` | R |
| `shiningFactionStrategicMemoryUpdates` | yes | `game_state/meta/shining_abode_state.json` | R |
| `skillMasteryChanges` | yes | `game_state/player/skill_mastery.json` | P |
| `startGuardianProjects` | yes | `game_state/meta/guardian_projects.json` | R |
| `statsDecreased` | yes | `game_state/player/status_changes.json` | P |
| `statsIncreased` | yes | `game_state/player/status_changes.json` | P |
| `timeChange` | yes | `game_state/world/world_time.json` | W |
| `TriggerIncarnation` | yes | `game_state/control/incarnation_trigger.json` | H |
| `TriggerLifeEnd` | yes | `game_state/control/life_transitions.json` | H |
| `updateFactionProgressionTracker` | yes | `game_state/world/progression.json` | W |
| `UpdateGuardianAbodeResidentHistoryLog` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `UpdateGuardianAbodeResidentInteractionReceipts` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `UpdateGuardianAbodeResidentRosterReceipts` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `UpdateGuardianAbodeResidents` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `UpdateGuardianAbodeResidentTransferReceipts` | yes | `game_state/meta/guardian_abode_residents.json` | R |
| `UpdateGuardians` | yes | `game_state/meta/guardians.json` | R |
| `UpdateGuardianTradeInventoryReceipts` | yes | `game_state/meta/guardians.json` | R |
| `UpdateInventory` | yes | `game_state/inventory/items.json` | I |
| `updateItemTextContents` | yes | `game_state/inventory/item_text_updates.json` | I |
| `UpdateNPCs` | yes | `game_state/npcs/npc_core.json` | N |
| `UpdateNpcTradeInventoryReceipts` | yes | `game_state/npcs/npc_core.json` | N |
| `UpdateQuests` | yes | `game_state/quests/regular_quests.json` | Q |
| `UpdateRivalSoulArcs` | yes | `game_state/world/rival_soul_arcs.json` | W |
| `UpdateSoulQuests` | yes | `game_state/quests/soul_quests.json` | Q |
| `UpdateVehicles` | yes | `game_state/misc/vehicles.json` | X |
| `updateWorldProgressionTracker` | yes | `game_state/world/progression.json` | W |
| `weatherChange` | yes | `game_state/world/weather.json` | W |
| `worldEventsLog` | yes | `game_state/world/world_events.json` | W |
| `worldMapUpdates` | yes | `game_state/world/world_map.json` | L |
| `worldStateFlags` | yes | `game_state/world/world_flags.json` | W |
| `woundDecisions` | yes | `client-owned wound command adapter` | E |
| `woundTreatmentAuthorings` | yes | `client-owned treatment authoring adapter` | E |

Extra routes: `output/qte_offer.json` — QteSceneService offer owner; not an ordinary re-offer
for unresolved declined situation. `output/ink_feather_action_result.json` — specific pending
Ink Feather action, not a generic reward field. Narrative/helper manifests and canonical-only
source catalog roots are reconciled per family; no direct story/receipt/G authoring.

| Source inventory pin | SHA256 |
|---|---|
| `BookOfEternityClient/Models/GameResponse.cs` | `dcc178fdcb21ed5bd5d14d2fc48c03371b8f0858e0ada86d65ab9778271f27bb` |
| `BookOfEternityClient/Configuration/FileMapping.cs` | `0e657357c315ffe740983d3f9c3347475dc1907d84f059753eddcf95f707b538` |
| `BookOfEternityClient/IO/StateDistributor.cs` | `0ceecfb1f5d517673504e413f138bad5593065791f1f85a222238c7f19a3893d` |
| `BookOfEternityClient/Services/CanonicalStateNormalizer.cs` | `af3eed10ef8f253aea0aa4d4b47b3b4690a7ba81dab4163a16aef5c62bc2dab9` |
| `BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs` | `486eb3820a6b6706560f21987ebcc00cc6cbfff62f2f05485d1ef9602c59c753` |
| `BookOfEternityClient/Services/EffectLifecycleScheduler.cs` | `f2b2b853dc727bd7d254f705c2169537411fbc199df8b0f4489dc3b2411d8d86` |
| `BookOfEternityClient/Services/QteDeferredAcceptedMechanicsPlanner.cs` | `8558d2df4ad3d2362361c9b1ff3ae38656432711a5bf39487aacfbf47ad7a18f` |
