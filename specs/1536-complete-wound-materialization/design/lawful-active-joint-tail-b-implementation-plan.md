# Lawful ACTIVE fixed joint-tail B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans` после отдельного разрешения реализации. Один Sol writer, отдельный обязательный Astra/XHigh reviewer на каждый связный этап. Checkboxes описывают будущую работу, не выдают разрешение её начинать.

**Goal:** Завершить lawful ACTIVE stats/level-up/new-QTE joint tail без повторных эффектов, потери ordinary resolution, преждевременного cleanup или ослабления genuine original/G proof.

**Architecture:** Сохранить existing common publisher и coordinated storage; добавить fixed owner phases в marker3, atomic action+ack и named phase-aware proof. Automatic award отделён от interactive allocation. QTE acceptance/terminal сохраняют original; revised D5 требует отдельного ещё не принятого durable remaining-resolution handoff.

**Tech Stack:** C#/.NET 8, SDK 10.0.401 baseline, PowerShell 7.6.6 baseline, JSON file state, Spectre.Console, existing loopback browser/React/TypeScript. Linux/Windows — отдельные native qualifications; новые зависимости/storage services не нужны.

**Spec:** [spec.md](../spec.md), FR063/T070/[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536); [B design](./lawful-active-joint-tail-b.md), [accepted INACTIVE contract](../contracts/mortal-wound-treatment.md).

**Planning base:** `ccb8eed3e57debdc3a5023c0cc28e75721c80c4c`, `1536-wound-b2-cloud-20261012`. В этом блоке разрешены только документы, независимое review, commit/push/readback. Implementation, tests, discovery/build/runtime tools, new authority и main не разрешены. Статус этого черновика: **READY FOR USER DISCUSSION; Q-D5 BLOCKED**. Все будущие implementation/runtime/platform checks **UNRUN**.

## Global Constraints

- Только supported Mortal joint family; v1 ordinary и v2 INACTIVE не получают ACTIVE права при чтении. Никаких legacy-save migrations/fallback initialization.
- Genuine physical original/G/common completion; exact base==initial и final==phase-current. Raw initial==completed, copied key, Load-created G, parsed JSON/hash не authority.
- Fixed named data cohorts + M; unchanged members guard-only. Actual writes — byte-different subset, не произвольное ограничение «<=N».
- Automatic stats/award/watermark атомарны; cap level*2/max100, ordered duplicates, +5/uncovered level; strict existing initialized stats/points/watermark. Training/award level priorities остаются разными.
- Нет XP compensation. D8 допускает только existing selected-QTE-earned XP/формулы, exact supported responseFragment и typed damage/physical contributions из DESIGN.
- D4: allocation блокируется пока original retained; никакой lease через UI, никакого заранее замороженного выбора. Ordinary allocator crash-safety — отдельная зависимость, не результат этого плана.
- **D5 mandatory:** при отказе от QTE ситуация обязательно разрешается стандартными механиками. Decline отменяет только QTE-вариант, не сцену/committed эффекты; source action и committed wound/treatment/award не применяются снова.
- Active/accepted QTE не отменяется через decline; UI pause/abort/EOF не terminal proof. Unknown outcome не выбирается до реального действия.
- Until Q-D5 accepted: **new joint offer refuses before common**. No-offer ACTIVE часть может быть реализована отдельно только после разрешения; это не full FR063/new-QTE acceptance.
- Все schema/write/delete/rollback изменения вне согласованного B, особенно follow-up request/original authority, требуют отдельного решения. Main/merge/force-push/branch deletion/secrets/issue closure запрещены.
- Никаких all-category/Fast/PreMerge/full-suite прогонов. Exact selection, fresh BOTH preparation, bounded actual owners и реальный cleanup; static acceptance, runtime PASS и UNRUN разделены.
- GM не читает implementation. Новый GM-facing contract требует guidance + worked example + manifest + source guards в том же изменении; не откладывать синхронизацию до последнего этапа.

## Review Focus

1. Decline после committed common, до/после crash: ordinary remaining resolution не потеряна, original action/effects не replay (B08, Q-D5).
2. Real fresh process с authority повреждённой **до constructor/PID admission**: no pristine G mint, unchanged raw namespace и точная failure identity (B01/B11/B13).
3. Lawful QTE drift/narrative replacement: original story остаётся прежней, current terminal output exact, changed/rehashed wrappers не получают authority (B10).
4. UI submits после generation/offer change либо после accept: no cancellation of accepted scene, no lease over UI, no allocation bypass через browser/agent (B05/B07/B12).
5. Partial publication/cleanup и lost caller receipt: actual native outcome, complete before/after or HOLD, original/debt retained, ненулевые cleanup receipts (каждый этап, B11/B13).

## Q-D5: существенное противоречие, решение до new-QTE реализации

Пользователь 2026-10-16 00:01 UTC: «Если отказываемся от QTE, то ситуация должна разрешаться стандартными механиками». Это обязательная игровая семантика, не новый schema/authority grant.

Source facts на ccb8eed3 (Core/Services/WebUi paths ниже внутри `BookOfEternityClient/`):

| Source | Факт / следствие |
|---|---|
| `Core/GameEngine/GameEngine.TurnLifecycle.cs:2345–2361` | Ordinary console decline записывает отказ, удаляет Ready и вызывает ProcessPlayerTurn(originalAction,QTE_DECLINED). Это повторное обращение для разрешения ситуации; перенести helper unchanged в committed joint tail нельзя. |
| `Rules/Block_CLI_QTE.txt:19–28`; `CLI_API_Specification.md:1240–1250`; `Examples/E_CLI_QTE_Offer.txt:7–9` | Offer-turn не разрешает normal state changes той же ситуации; decline требует ordinary GM resolution. Удалить follow-up значит потенциально потерять сам исход. Literal «обычные game_state untouched» также требует явного joint-only contract delta, прежде чем разрешать joint mechanics+offer. |
| `Services/QteSceneService.cs:6402–6441` | В QteOffer нет optional/mandatory remainder field. DeclineHint/cinematic prose не machine authority, отсутствие поля не доказывает отсутствие обязательств. |
| `WebUi/QteWebInteractionService.cs:267–283` | Existing browser decline только records/clears offer, notification прямо отличает console follow-up. Это текущая parity gap; не выдавать её за корректный shared implementation. |

**Требуемый исход:** exact decline receipt + сохранённое remaining-resolution обязательство → один lawful ordinary resolution → подтверждение исполнения. Already committed common/stats/award/story и QTE waves не откатываются и не становятся новыми commands. Повторная доставка возвращает status того же обязательства/решения; новый original/request не mint повторно. Accepted/active QTE отклонить нельзя.

| Предложение для обсуждения | Необходимый новый contract / риск |
|---|---|
| **F1 — рассмотреть первым:** fixed joint follow-up handoff в существующем M, через existing original/request owners | Proposed `activeTail.qte.followUp` (новое closed поле, **не принято**): version, state, source tuple/G/qteId/offer/basis/decision operation binding, immutable remaining-resolution specification, client-derived committed-effect receipt bindings, target request/original binding и completion receipt. Нужны новый named issuer/consumer, связь old→new genuine original и atomic cleanup/staging boundary. Released-marker observation сейчас nonauthorizing: нельзя превратить её в mint authority простым hash check. Viability доказать отдельным DESIGN. |
| **F2:** owner-specific durable continuation, ограниченный вариант C | Отдельный ordinary-continuation owner/receipt и, возможно, новый proposed path `game_state/control/mortal_joint_qte_followup.json`. Тот же once-only/restart/source proof, больше schema/member/cleanup coordination. Path — только кандидат; ни writer, ни deletion/rollback grant не выданы. |
| **Временная безопасная граница, не решение D5:** оставить new-joint-QTE refusal | До принятия F1/F2/иного полного решения B01–B05 no-offer могут планироваться отдельно; B06–B11/QTE activation заблокированы. Нельзя выдавать HOLD/refusal за успешный decline или завершение FR063. |

До выбора должны быть письменно закрыты: кто авторитативно описывает **remaining** ситуацию и уже applied effects; exact IDs/dedup; связь GM offer-turn rule с joint publication; staging next original/request; logical turn/time/progression policy; source retention до acknowledged transfer; fail/uncertain/cleanup boundaries; console/browser/agent parity. Текст originalAction можно сохранить как context, но не выдать повторно как новый исполнимый player command. Новые effects ordinary resolution законны только для remainder, без повторного применения committed wound/treatment/awards.

Новый механизм сейчас не выбран и не проектируется как уже выданный grant. B08 — gated task; implementation plan в целом возвращается для обсуждения с этим конкретным blocker.

## Очередность этапов

Один последовательный writer; Bxx — engineering stage, Pxx — durable DESIGN phase, они не взаимозаменяемы.

| Task | Stage / результат | Зависит от |
|---|---|---|
| T178 | B00 contract/selection preparation и Q-D5 decision gate | plan approval + implementation authorization |
| T179 | B01 marker3/source capture/strict admission | B00 |
| T180 | B02 automatic action+ack | B01 |
| T181 | B03 projection action+ack | B02 |
| T182 | B04 original story/turn action+ack | B03 |
| T183 | B05 no-offer vertical completion, D4 entry guards | B04 |
| T184 | B06 QTE grammar/capture/retirement | B05 + accepted Q-D5 subdesign |
| T185 | B07 QTE accept/choice handoff | B06 |
| T186 | B08 mandatory ordinary decline continuation | B06 + accepted exact follow-up grants; activation paired with B07 |
| T187 | B09 selected-terminal atomic outcome D8 | B07 |
| T188 | B10 post-QTE auto/projection and two-chain/output proof | B09 |
| T189 | B11 QTE terminal cleanup/cold admission | B08+B10 |
| T190 | B12 console/browser/GM interaction qualification | B05+B11 |
| T191 | B13 Linux/Windows bounded connected acceptance | affected prior stages |

Следующие sections задают точные будущие изменения; Q-D5 остаётся обязательным decision gate. Ни один implementation checkbox сейчас не выполнен.

## File ownership и интерфейсы

Ниже **планируемые** имена/сигнатуры, не существующие API и не новые public authority constructors.
`F=FileSystemManager`, `L=F.CanonicalWriteLease`, `M=MortalQteDecisionPublication`.
`Frame=GameEngine.ValidatedPendingTurnSnapshotContext` остаётся **private engine-only**;
никакой Services API его не принимает и visibility не расширяется. `Binding=M.JointActiveTailBinding` —
новый internal immutable value `(Generation,SessionId,RequestId,SnapshotToken,Turn,Realm,Incarnation)`;
строки/целые — только expected correlation, не proof. Каждый reader независимо проверяет actual
physical original/G. Новые value/proof types internal; arrays/maps detached immutable.
Сокращение `.MortalJointX.cs` сохраняет directory/class prefix предыдущего полного path.
Integration shorthand `.JointActiveX.cs` означает
`BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointActiveX.cs`;
все они дополняют existing partial `GameEngineTurnLifecycleTests`, не вводят shared mutable fixture.

| Files (от корня repository) | Responsibility / proposed API |
|---|---|
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointActiveTailSource.cs` (new); `GameEngine.MortalJointInactiveTail.cs` (modify) | Engine-private `MortalJointActiveTailSource`; `CaptureMortalJointActiveTailSourceAsync(L,Frame)` → `Task<MortalJointActiveTailSource>`. Только actual original/G/normalization/common candidate admission, до take. V2 inactive guard не ослаблять. |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointActiveTail.cs` (new); `.MortalJointRepair.cs` (modify) | Closed M3 schemas/phase graph. `CreateJointActiveTailBasisAsync(F,L,GameEngine.MortalJointActiveTailSource,WoundAcceptedTurnPlanner.JointActiveCommonInputs)` → `Task<JointActiveTailBasis>`; `ReadJointActiveTailPhaseAsync(F,L,Binding)` → `Task<JointActiveTailCapability>`. Basis — value; capability только fresh physical issuer. |
| `BookOfEternityClient/Services/MortalJointRepairPublicationPreparation.cs`, `MortalJointRepairPublicationTransaction.cs`; `Core/GameEngine/GameEngine.MortalJointRepairAdmission.cs`, `.MortalJointRepairCanonicalPublication.cs`, `.MortalJointRepairCompletion.cs` (modify) | Forward basis через настоящий prepared/taken common owner; M3 выпускается одним common batch, не side write. Cold dispatch version-first, no generic ordinary fallback при retained active evidence. |
| `BookOfEternityClient/Services/CharacteristicsService.MortalJointAutomatic.cs` (new) | `CalculateMortalJointAutomatic(M.JointAutomaticInputs)` → `M.JointAutomaticPlan`, pure. Inputs/plan types объявлены в M; ordered arrays, exact three after-images, checked arithmetic. Ordinary CharacteristicsService APIs/allocator не менять формулой. |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointAutomaticPublication.cs` (new) | `PublishJointAutomaticAsync(F,L,JointActiveTailCapability)` → `Task<JointTailApplyResult>`; privately issued same-attempt `JointAutomaticCommittedAttempt` и `CompensateJointAutomaticAsync(F,L,JointAutomaticCommittedAttempt)` → `Task<JointTailApplyResult>`. Token не сериализуется/не rehydrate cold. |
| `BookOfEternityClient/Services/CharacteristicsService.MortalJointProjection.cs` (modify) | Named overload `WriteMortalJointActiveProjectionAsync(L,M.JointActiveTailCapability)` → `Task<M.JointTailApplyResult>` и read-only `VerifyMortalJointActiveProjectionAsync` с той же сигнатурой. Current phase определяет initial/post-QTE slot; caller не выбирает произвольный source mode. Existing v2 overload остаётся точным. |
| `BookOfEternityClient/Services/StoryService.MortalJointRepair.cs`; `Core/GameEngine/GameEngine.MortalJointStoryTurn.cs` (modify) | `WriteMortalJointActiveStoryAsync(L,M.JointActiveTailCapability)` → `Task<M.JointTailApplyResult>`; `ReadMortalJointActiveStoryProofAsync` → `Task<MortalJointStoryProof>`. Story+M ack atomic, frozen initial inputs; runtime n−1/n reconcile. |
| `BookOfEternityClient/Services/QteDeferredEffectContinuation.MortalJoint.cs` (new); `QteDeferredEffectContinuation.cs`, `StoryService.AcceptedStoryAppend.cs` (modify) | Сделать existing static continuation class partial; strict schema2/joint story variant, guard-only capture inventory, named genuine retirement reader. `ReadPriorMortalJointTerminalRetirementAsync(F,L,Binding)` → `Task<PriorQteTerminalRetirementProof?>`; proof private issuer внутри continuation owner. Existing v1 shape/source guards сохраняются. |
| `BookOfEternityClient/Services/QteSceneService.MortalJointHandoff.cs` (new); `Core/GameEngine/GameEngine.MortalJointQteTail.cs` (new) | `BeginAcceptedMortalJointSceneAsync(L,M.JointActiveTailCapability,M.JointQteChoice)` → `Task<M.JointTailApplyResult>`. Choice — value, not authority; exact decisionReceipt, no original cleanup. Decline continuation API намеренно не объявлен как accepted: см. Q-D5/B08. |
| `BookOfEternityClient/Services/QteSceneService.MortalJointTerminal.cs` (new); `QteSceneService.cs`, `QteDeferredAcceptedMechanicsPlanner.cs` (modify) | `PrepareMortalJointTerminalAsync(L,M.JointActiveTailCapability)` → `Task<M.JointTailApplyResult>`; `CommitMortalJointTerminalAsync` с теми же arguments/result. Only persisted selected outcome/resolved wave. Pure XP/history/runtime/narrative after-images, exact existing typed plan contributions. |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointQteProof.cs` (new); `Core/GameEngine/GameEngine.MortalJointRepairCompletion.cs` (modify) | `ReadMortalJointQteTailPhaseAsync(F,L,Binding)` → `Task<JointActiveTailCapability>`: original→H→current, initial story vs current terminal output; `PrepareMortalJointTerminalTailAsync(F,L,JointActiveTailCapability)` → `Task<JointTailApplyResult>` freezes terminal-known auto basis. |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointTerminalCleanup.cs` (modify) | `CompleteMortalJointActiveCleanupAsync(L,M.JointActiveTailCapability)` → `Task<M.JointTailApplyResult>`; K+M release atomic. Q-D5 may require separate accepted cleanup/staging boundary; не добавить follow-up paths в K молча. |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`, `.AgentConsole.cs`; `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs`, `QteWebInteractionService.cs`, `BrowserPlayerActionService.cs` (modify) | Actual retained-tail admission для allocation/ordinary input; joint UI dispatch к одному named owner. Separate ordinary browser allocator уже имеет coordinator wrapper: не объявлять его crash-unsafe только по console implementation и не расширять его contract этим планом. |

### B01 pre-seal dependency и fingerprint DAG

Active inputs захватываются **до** CreateJointRepairCommonPlanAsync, composition/cache seal/preflight/take,
как existing progression source. Источник не принимает PublicationPreparation: она уже требует sealed plan.
Common owner получает source, privately строит `WoundAcceptedTurnPlanner.JointActiveCommonInputs`
(immutable exact planned common after-images/resource binding из настоящего resource authority, не caller map),
передаёт его в CreateJointActiveTailBasisAsync и связывает source + frozen raw basis в CommonAuthority
**до** BindInput/ComputePlanningInput/ComputePrepared/cache sealing. Новое overload
`CreateJointRepairCommonPlanAsync(F,L,object resourceAuthority,GameEngine.MortalJointProgressionSource,GameEngine.MortalJointActiveTailSource)`
создаёт v3; existing overload/v2 не получает nullable «upgrade later».

Acyclic order: physical capture S → owner-produced common inputs C → raw basis B без final common proof core
→ CommonAuthority fingerprint(S,B,C) → planning input → prepared plan/cache/take → final M basisFingerprint(B,immutable common proof core).
Final marker fingerprint/basisFingerprint не подаются обратно в CommonAuthority/input/prepared hashes.
Если planned after-images требуют pure composition helper, его вызвать до cache candidate seal;
не достраивать данные после seal, не recapture current bytes, не пересоздавать уже принятый plan.
`GetOrCreateCommonAuthority` обязан reject другой active source/basis при уже bound instance.

| Exact path | Planned change или обязательная проверка |
|---|---|
| `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalJointRepairCommonPlan.cs` | Modify constructor/one-time binding/fingerprint/ValidateCurrentAsync + new overload, source/raw basis до input seal. |
| `BookOfEternityClient/Services/WoundAcceptedTurnPlanner.MortalJointRepairResources.cs` | Modify GetOrCreateCommonAuthority source/basis identity; cached owner не игнорирует новые inputs. |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointRepairCanonicalPublication.cs` | Modify capture/handoff до common composition; no Preparation dependency. |
| `BookOfEternityClient/Services/AcceptedMechanicsWoundCommonInputComposer.MortalJointRepair.cs` | Verify existing common-authority fingerprint propagation into actual planning input; менять только если test докажет missing dependency, без нового raw DTO authority. |
| `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs` | Verify ComputePlanningInput/ComputePrepared transitively cover changed CommonAuthority; не добавлять циклический final marker hash. |
| `BookOfEternityClient/Services/AcceptedMechanicsPlanCache.MortalJointRepair.cs`, `AcceptedTurnAuthorityRegistry.MortalJointRepairPublication.cs` | Verify same candidate/cache fence/common source validation through seal/take; no generic cache access/rebuild. |
| `BookOfEternityClient/Services/MortalJointRepairPublicationPreparation.cs`, `MortalJointRepairPublicationTakeReceipt.cs`, `MortalJointRepairPublicationTransaction.cs`, `CanonicalStateNormalizer/CanonicalStateNormalizer.MortalJointRepair.cs` | Verify unchanged transitive common/input/prepared/receipt/inventory guards; modify only named v3 forwarding/serialization where required. Preparation/take never capture new source. |

Tests B01 must demonstrate that changing/foreign source before seal/take changes/refuses the real candidate,
after-seal attachment is rejected, both planning/prepared fingerprints cover source/raw basis, while mutable
phase receipts do not alter immutable input seal. Existing member/fence/original and one-time issuer assertions stay.

`M.JointTailApplyResult` — closed `{Disposition,Phase,Failure}`; Disposition=`NewlyApplied|ExistingApplied|KnownRolledBack|Refused|PublicationUncertain`. Result value сам next capability не выдаёт: следующий owner всегда fresh re-export. `Failure=null` только для verified success/known rollback; uncertain требует retained evidence и не считается warning-success. `JointAutomaticInputs/Plan`, `JointQteChoice`, `JointActiveTailBasis/Capability` имеют ровно DESIGN schemas/roles; public constructors/caller path lists запрещены. Общий helper результата не превращается в общий journal или arbitrary writer.

### Paths и grants

M=`game_state/wounds/mortal_qte_decision_publication.json`. Exhaustive approved-B cohorts и K — DESIGN §4; не копировать меняющийся path inventory в ещё один runtime allowlist. Implementation должен собрать fixed catalogs и проверить equality с owner-produced paths.

| Stage | Exact introduced write set (unchanged members remain guards) |
|---|---|
| B01 | Existing common publication data + M3 only. Capture P/headers — read-only. |
| B02 | `game_state/misc/characteristics.json`, `game_state/player/status_changes.json`, `game_state/player/stat_points.json`, M. |
| B03 | `game_state/player/computed_characteristics.json`, M. |
| B04 | One `stories/mortal_life_{i}.jsonl` from genuine original/GetStoryPath, M. |
| B05/B11 | Only exact K from signed original + M released ack. No wildcard directory deletion. |
| B07 | `game_state/control/qte_runtime.json`, `game_state/control/qte_deferred_effect_continuation.json`, `output/qte_offer.json`, `ready/turn_complete.json`, `ready/turn_error.json`, M; error must already be absent. |
| B08 | B decision cohort is insufficient for mandatory ordinary handoff. Any followUp/new request/original writer, logical turn/time and cleanup changes are **ungranted Q-D5 proposals**; no executable writer set is assumed. |
| B09 | M-only selected-terminal PREPARE, then exact existing supported deferred plan set + `game_state/player/experience.json`, `game_state/player/status_changes.json`, `game_state/history/qte_history.json`, runtime/continuation, `output/narrative_response.json`, `input/qte_effect_resolution_request.json`, `output/qte_effect_resolution_receipts.json`, `ready/qte_effect_resolution_complete.json`, M. No broader NormalizerRollbackTrackedFiles membership. |
| B10 | M-only terminal auto basis PREPARE, then separate B02/B03 data cohorts + M for terminal slots. |

Read/guard P: full DESIGN §4/§5, including initial narrative/interface/debug/combat/location/guardians/residents, original headers/request/artifacts, source/effect/resource inventories, projection and story reads. Any new actual dependency missing from capture is an admission failure; no lazy recovery reread/default or permission by filename.

## Общий RED→GREEN, review и checkpoint protocol G

Для каждого B01–B13 после отдельного разрешения реализации:

- [ ] **G1 — test first:** добавить конкретные tests этапа и finite category owner/reason/contracts. Сначала наблюдать meaningful assertion RED на реальном entrypoint; compiler/setup/discovery failure не behavioural RED. Для нового symbol допустим лишь compile scaffold, возвращающий refusal/no writes, после написания test; зафиксировать затем semantic RED до success logic.
- [ ] **G2 — narrow implementation:** только owner/files/cohort этого этапа; synchronized GM docs/example/manifest/source guards включены в тот же diff, если changed authoring contract. Existing successful mechanics/recovery assertions не удалять и не ослаблять.
- [ ] **G3 — publish before review:** diff/status/ignored/untracked check, WIP commit + non-force push, actual `git ls-remote` equality. Один clean HEAD для compilation/execution pins. Failure evidence сохранять до correction.
- [ ] **G4 — independent gate:** отдельный Astra/XHigh проверяет SOURCE/FIXTURE/SELECTION, actual fixture setup/cleanup, exact roles/transactions и достаточность negatives. После findings — source-backed correction, affected corrected gate; не повторять весь review без причины.
- [ ] **G5 — fresh bounded verification:** сначала определить impact и обязательные `related.when` prerequisites выбранных owners. Fresh selected BOTH preparation → catalog/discovery/Plan (zero execution). Если затронут common/source/admission/phase/story/cleanup owner или выбран protected header/generation/output owner: **Late2 первый actual**, затем raw freeze + независимая EVIDENCE классификация Late2, только после неё отдельно release stage/negative owner actual. Failure останавливает дальнейшие actuals; correction требует fresh preparation. UI-only/evidence-only diff с неизменными shared source pins не повторяет уже PASS Late2 без нового impact/failure; основание и прежние точные pins записываются явно, не выдаются за fresh run.
- [ ] **G6 — evidence acceptance:** независимое EVIDENCE review реальных native exit/selected vs executed/reached oracles/source-binary pins/cleanup; затем final stage commit/push/readback, SpecKit task закрывается только по evidence. Перезапускать PASS лишь после relevant changes/failure/unresolved concern.

Будущие команды из repository root (не запускались в planning):

```powershell
# B02 example; planned category becomes callable only after its tests/catalog entry exist.
pwsh -NoLogo -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-active-b-automatic,fr063-joint-late-completion -PlanOnly
pwsh -NoLogo -NoProfile -File ./scripts/test-csharp.ps1 -ValidateCatalog -NoBuild
pwsh -NoLogo -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-active-b-automatic -PlanOnly -NoBuild
pwsh -NoLogo -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-joint-late-completion -NoBuild
# STOP: freeze exact Late2 packet; independent EVIDENCE classification and release.
# With unchanged source/binaries, only then execute the separately released stage owner:
pwsh -NoLogo -NoProfile -File ./scripts/test-csharp.ps1 -Category fr063-active-b-automatic -NoBuild
```

BOTH означает actual successful build unit+integration selected projects, не два PASS-теста. Если stage category только integration, preparation дополнительно выбирает самый узкий затронутый unit owner из таблицы ниже; если такого owner нет, existing `fr063-joint-output-basis` только для preparation с явно записанным основанием shared output proof. Не запускать его actual по инерции. Preparation-only category не считается actual selection/PASS; если actual output-basis всё же выбран, его first-Late2/EVIDENCE prerequisite обязателен. `fr063-joint-initial-header-refusal`, `fr063-joint-generation-admission`, `fr063-joint-output-basis`/output-positive явно требуют этот порядок. При generation-admission учитывать related generation-refusal; conditional graph анализировать по реальному impact, не запускать весь граф автоматически. При frontend contract change отдельный `npm ci --prefix BookOfEternityClient.WebFrontend` и finite frontend owner; не полный npm test/verify.

Runner имеет собственный unique result directory; external stdout/log filename дополнительно содержит UTC+PID+nonce+stage+attempt. Реальный native exit берётся непосредственно от завершённого процесса, не из последней успешной команды/pipeline. Preserve command, actual category plan.json/full TRX IDs, compile/source HEAD и binary SHA256 до/после, timeout/kill/drain, complete vs UNRUN, per-phase operationId/transactionId и raw before/after payloads. Baseline removal, progression consumption, automatic и QTE transfer наблюдаются как **разные named transactions**; не считать события через raw aggregate <=2.

Each actual fixture имеет собственные distinct absolute mutable roots и emits real nonzero cleanup receipts: actual root list, inactive lease, explicit null failure, directories absent после disposal. Pure parser controls own zero roots и не получают cleanup PASS. Snapshot нельзя снимать через exclusive lease его же generic filesystem reader; no shared mutable fixture/cache/active owner. Fresh process tests фиксируют actual child PID и immutable durable bytes, без inherited process registry/root cache; bad authority seed выполняется до constructor/child admission.

New categories ниже — proposals for implementation, сейчас catalog/selection не меняются. Initial budget каждого нового narrow owner ≤5 minutes; если body не помещается, split по coherent cases с измеренными expectedSeconds, не повышать limits. Existing budgets не менять. Не включать broad `qte-rules`, `browser-qte`, `frontend-qte`, все wound categories или category sequence вместо impact-selected finite cases. Conditional existing consumer regressions выбираются точными owners, а не всем их соседним графом.

Finite GM guidance owner для B06/B08/B12: proposed `fr063-active-b-qte-guidance` с exact selectors в
`BookOfEternityClient.Tests/PromptDocumentationCoverageTests.cs`:
`QtePhysicalOpportunityProjection_IsDocumentedAsClosedReadOnlyInput`,
`QteTerminalResourcePenaltyContract_IsDocumentedAcrossGmSurfaces`,
`DaemonSpecDocumentsQteOfferRuntimeContract` и новый planned
`MortalJointQteDecline_MandatoryResolutionAndNoReplay_AcrossGmSurfaces`.
Первые два уже имеют narrow QTE-documentation owners: intentional overlap документировать,
исполнять каждый method один раз через выбранного владельца, не wildcard whole PromptDocumentationCoverageTests.
Синхронизировать также `CLI_Agent_Daemon_Specification.md` и `TaskGuides/CLI_Step_Main.txt`, которые эти
настоящие guards читают; same-change joint-only guidance не должна противоречить owning Block_CLI_QTE.

## Этапы: конкретные deliverables, tests и acceptance

### T178 / B00 — contract baseline и Q-D5 gate

**Files:** modify `specs/1536-complete-wound-materialization/{spec.md,plan.md,tasks.md,contracts/mortal-wound-treatment.md}`, DESIGN/этот план; при принятии Q-D5 — связанный closed contract appendix в том же feature. Пока только последующая разрешённая подготовка contract delta, не runtime.
**Consumes:** approved plan и отдельное permission-to-implement; **produces:** точная source-pinned contract/task/selection basis, accepted Q-D5 subdesign до B06 activation.

- [ ] Зафиксировать current actual remote SHA, ancestry/clean, AGENTS/workflow/constitution, required setup из tracked recovery, версии/недоступные ОС; старые machine paths не использовать.
- [ ] Разнести B/D1–D8 planning approval и implementation authorization. Для Q-D5 выбрать/спроектировать F1/F2, independent DESIGN review до code: exact schema/member/issuer/old→new binding/remaining spec/turn-time policy/uncertain/cleanup; нельзя закрыть gate словами «reuse ordinary handler».
- [ ] Синхронизировать accepted contract delta и новые task IDs. B00 не считается закрытым для new-QTE, пока Q-D5 не принят. No-offer B01–B05 разрешать только явно в границах отдельного slice.
- [ ] Gate: отсутствие contradictory D5/no-follow-up clauses, no new implicit grants, source contract/example позволяют GM authored flow без implementation. Commit/push/readback.

### T179 / B01 — M3 closed contract и genuine before-common capture

**Files:** create engine source и M active-tail partial из registry; modify CommonPlan/Resources/canonical handoff и version dispatch; exact changed-vs-verified common/input/cache/registry/preparation/take/normalizer files — таблица B01 pre-seal выше. Tests: new `BookOfEternityClient.Tests/MortalJointActiveTailContractTests.cs`; new `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResourcePublicationLifecycleTests.JointActiveAdmission.cs` (existing partial class GameEngineTurnLifecycleTests).
**Interfaces:** source→basis→common M3, then `ReadJointActiveTailPhaseAsync`; no raw DTO issues capability.
**Owner:** proposed `fr063-active-b-admission`.

- [ ] RED `M3_RejectsUnknownFieldsIllegalPhaseAndCyclicSeals`, `M3_SourceRolesRequireExactOriginalSubmittedCommonGuards`, `JointActive_MissingInitializerRefusesBeforeCommon`, `JointActive_BadAuthorityBeforeFreshProcessAdmissionPreservesNamespace`, `JointActive_SourceChangeBeforeSealOrTakeRefuses`, `JointActive_AfterSealAttachmentIsRejected`, `JointActive_SourceAndRawBasisReachPlanningAndPreparedSeals`:  exact schema/roles/digests and no writes/G mint on refusal.
- [ ] Capture P before common, strict present/absent/BOM/duplicate/path policy, distinct training/award inputs, offer/old-control exclusions and storyPlan. Bind source/raw basis before CommonAuthority/input/cache sealing; final M basis fingerprint only after immutable common proof core. Publish v3 through actual prepared/taken publisher; retain existing v2 inactive constraints and family dispatch.
- [ ] Reconcile actual reader catalogs with P and permitted overlaps; new dependency without frozen capture refuses. New offer stays refused before common until Q-D5 implementation is approved and complete.
- [ ] G; conditional regression `fr063-joint-output-basis`, `fr063-joint-initial-header-refusal`, `fr063-joint-generation-admission` + related `fr063-joint-generation-refusal` when touched dispatch/admission; first-Late2/EVIDENCE gate precedes these actuals, exact case plan before run. **Accept:** M3 common receipt+whole union genuine, no ACTIVE-v2 migration, no changed/stale/foreign draft acceptance. Covers A02/A05/A08/R02/V01.

### T180 / B02 — atomic automatic action+ack

**Files:** create CharacteristicsService automatic and M automatic-publication partials; tests new `BookOfEternityClient.Tests/MortalJointAutomaticPlanTests.cs`, integration `.JointActiveAutomatic.cs`.
**Interfaces:** pure `CalculateMortalJointAutomatic`; publish exact 3 data+M; same-attempt compensation token.
**Owner:** proposed `fr063-active-b-automatic`.

- [ ] RED `Automatic_OrderedDuplicatesCapsAndResolverPriorities`, `Automatic_AwardsOnlyUncoveredLevelsAndPreservesSiblings`, `Automatic_OverflowOrMalformedRefusesWithoutDefaults`, `Automatic_CrashEveryMemberIsBeforeAfterOrHeld`, `Automatic_ColdReceiptNeverGrantsCompensation`.
- [ ] Compute detached three after-images; ordered trim/lower/cap level*2/max100, checked +5 arithmetic, strict existing ≥1 watermark/nonnegative balance, no initialization/XP writer. Remove only consumed nonempty stats field; preserve status/points siblings.
- [ ] Publish action+M receipt one coordinated batch; revalidate guards in callback/await boundaries. Same-attempt exact four-member compensation only before later phase attempts/debt, no source/common/progression rollback; cold after returns ExistingApplied.
- [ ] G + Late2; **accept:** +1/+5 once over crash/restart/repeat, exact before/after or HOLD; XP raw bytes unchanged. Covers A01–A08. Don't rewrite old active-refusal fixture into fabricated PASS; retain v2 refusal, add genuine v3 path.

### T181 / B03 — actual derived projection + M acknowledgement

**Files:** modify `CharacteristicsService.MortalJointProjection.cs`, M phase partial; integration `.JointActiveProjection.cs`.
**Interfaces:** registry named active projection write/verify overloads; pure result does not grant story authority.
**Owner:** proposed `fr063-active-b-projection`; preparation unit owner B02 arithmetic.

- [ ] RED `Projection_AfterAwardUsesFinalBalanceAndFrozenUtc`, `Projection_CrashBetweenDataAndAckRecoversOneResult`, `Projection_ChangedHiddenEffectSourceRefusesWithoutRepair`.
- [ ] Reuse actual ComputeAsync/named read scope, complete accepted effect/relic/equipment/skill sources; fixed timestamp. Publish computed+M atomically; applied replay verifies semantics and exact receipt bytes without timestamp rewrite/default repair.
- [ ] Keep old v2 source overload strict. No calls AddStatPoints/normal refresh/interactive allocator; capture/read inventory equality before and after awaited work.
- [ ] G + Late2; **accept:** exact current points/projection, source drift held, no mechanical compensation. Covers P01/A04 and Review Focus 3 source class.

### T182 / B04 — original story/turn publication

**Files:** modify `StoryService.MortalJointRepair.cs`, `GameEngine.MortalJointStoryTurn.cs`, M phase partial; integration `.JointActiveStory.cs`.
**Interfaces:** `WriteMortalJointActiveStoryAsync`/genuine `ReadMortalJointActiveStoryProofAsync`; actual P4 row from initial storyPlan, not future terminal output.
**Owner:** proposed `fr063-active-b-story`.

- [ ] RED `Story_InitialBindingLabelsAndPrefixAreImmutable`, `Story_CrashRowAckAndRuntimeNMinusOneOrNAppendExactlyOnce`, `Story_ForeignSuffixOrTurnNPlusOneHolds`.
- [ ] Recompose frozen row against genuine signed original/history inventory and source labels; story+M atomic. Preserve unrelated histories, no new chat writer; runtime reconcile n−1→n only from durable proof, existing n unchanged.
- [ ] Freeze guardian/resident/NPC/location read dependencies including conditional tags; no label fallback from later current state. Same row never rewritten on replay.
- [ ] G; affected existing `fr063-joint-story-owner` only because story owner changed, plus Late2. **Accept:** one exact row, one runtime turn, source/output mismatch retains original/debt. Covers S01/R03.

### T183 / B05 — no-offer ACTIVE vertical completion и D4 admission

**Files:** engine completion/story/cleanup/active source; `GameEngine.TurnLifecycle.cs`, `.AgentConsole.cs`, `WebUi/BrowserMortalWorldWriteService.cs`, `BrowserPlayerActionService.cs`; integration `.JointActiveNoOffer.cs` and targeted existing browser command tests.
**Interfaces:** named active cleanup; retained-tail admission shared by console/browser/agent, no allocator redesign.
**Owner:** proposed `fr063-active-b-no-offer` (split UI guard subset if budget exceeded).

- [ ] RED `NoOffer_StatsAndLevelsCompleteAcrossLiveLateColdAndRepeat`, `Allocation_AllEntryRoutesBlockWhileOriginalRetained`, `NoOffer_PartialCleanupIsNotCompletion`.
- [ ] Wire P1→P2→P3→P4→P8 for v3 no-offer only, keep no ordinary tail/replay fallback. K+M atomic release, exact authorized optional absence. Preserve all source/output/private guards and genuine-completion rules.
- [ ] Block console ShowStatDistribution, browser /distribute writes and agent/local routes while tail retained. Award notification distinct from allocation; no UI wait under lease, no preselected allocations. After release return ordinary UI availability; no claim about ordinary allocator crash safety.
- [ ] G + actual Live and Late2 impact owners; **accept:** smallest usable stats/level no-QTE slice with actual cleanup. New offers still refusal. D4 allocator and full guided US5 remain OPEN. Covers C01–C03/U01/U02 and Review Focus 4.

### T184 / B06 — QTE wire grammar, capture и prior terminal retirement

**Gate:** accepted Q-D5 schema/authority/GM joint-only rule before new-offer activation; tests may not smuggle that grant through fixtures.
**Files:** new continuation joint partial, existing continuation declaration/dispatch, StoryService.AcceptedStoryAppend, QteSceneService; new unit `MortalJointQteGrammarTests.cs`, integration `.JointActiveQteCapture.cs`. Update owning source scanner literal for new partial declaration without removing inventory/assertions.
**Interfaces:** strict continuation2/clientStoryAppend2, genuine `PriorQteTerminalRetirementProof`, common-basis offer capture.
**Owner:** proposed `fr063-active-b-qte-capture`.

- [ ] RED `QteGrammar_ExactDirectFieldsNullsPrecedenceAndDamageOnly`, `QteCapture_PriorTerminalRequiresActualClosureAndStoryPrefix`, `QteCapture_ThreeSuccessiveTransfersNeverNestPredecessorBlobs`.
- [ ] Implement DESIGN §6/§8.4 exact offer/responseFragment branches, sourceTurn absent-or-n, type/unknown/null policy; resource definitions/capacity forbidden even null, damage-only typed resource/physical grants. Validate every authored branch before common, don't choose outcome.
- [ ] Genuine current original/G/Ready/output capture; bound offer in memory. Actual old terminal validation before replacement; compact retirement descriptor after commit is correlation only. Nonphysical capture still covers fixed joint proof cohort; no new harmful occurrence from old receipt.
- [ ] G; targeted current `qte-physical-injury-producer-continuation` and exact story witness consumers where changed. Sync actual `Rules/Block_CLI_QTE.txt`, QTE guide/example/manifest and exact owning PromptDocumentationCoverageTests methods in the same change after accepted joint GM exception. **Accept:** no v1 weakening, stale/foreign/active prior fails, capsule acyclic. Covers Q01/Q05/Q06/Q10/Q13/V01.

### T185 / B07 — tuple-bound accept, decisionReceipt и retention

**Files:** new QteSceneService handoff/engine QteTail partials; `WebUi/QteWebInteractionService.cs`, `QteInteractionTokenAuthority.cs`; integration `.JointActiveQteHandoff.cs`.
**Interfaces:** named BeginAcceptedMortalJointSceneAsync, actual linked continuation/runtime, closed decisionReceipt; token is correlation only.
**Owner:** proposed `fr063-active-b-qte-handoff`.

- [ ] RED `JointQte_OfferUiReleasesLeaseAndRejectsStaleToken`, `JointQte_AcceptAtomicCohortRetainsOriginalAcrossEveryCut`, `JointQte_DuplicateAcceptAndAcceptDeclineRaceHaveOneDecision`, `JointQte_DeclineCannotCancelActiveScene`.
- [ ] UI reads offered state then releases lease; after click fresh admission/capability under reacquired lease. Named writer commits runtime/continuation/offer/own Ready+M decision receipt as one exact cohort; turn_error absent guard, no generic cleanup.
- [ ] Same deterministic transfer/continuation operation identity, retained request/original/backups; ordinary next input/allocation blocked. Do not expose offered UI in production until B08 mandatory decline path is complete; an unimplemented decline button/HOLD after click is not product acceptance.
- [ ] G + Late2, affected exact generation-token browser tests. **Accept:** repeat read-only same result, wrong G/choice fails, no already-accepted cancellation. Covers Q02–Q04/Q08/Review Focus 4.

### T186 / B08 — обязательное ordinary resolution после decline (BLOCKED Q-D5)

**Files:** exact future follow-up owner/schema files определяются только accepted Q-D5 subdesign; known consumers `GameEngine.MortalJointQteTail.cs`, `GameEngine.TurnLifecycle.cs`, `LiveTurnPreparationService.cs`, `WebUi/QteWebInteractionService.cs`, `BrowserPlayerActionService.cs`. Test file `.JointActiveQteDecline.cs`; contract/`Rules/Block_CLI_QTE.txt`/CLI/QTE example + manifest/source guards must move together, including finite owning PromptDocumentationCoverageTests methods.
**Interfaces:** не выдавать invented follow-up API за accepted B. Approved Q-D5 appendix обязан до code назвать producer/consumer signatures, exact maps, receipt/IDs и source/target original admission; отсутствующий appendix — STOP этого этапа.
**Owner:** proposed `fr063-active-b-qte-decline` after Q-D5 closure.

- [ ] RED `Decline_CommittedJointEffectsRemainAndOrdinaryRemainderResolves`, `Decline_CrashBeforeAfterDecisionTransferRequestCompletionNeverLosesObligation`, `Decline_RedeliveryAndRestartDoNotDuplicateEffectsOrMintSecondRequest`, `Decline_ConsoleBrowserAgentHaveSameOrdinaryResolution`, `Decline_AcceptedSceneCannotBeCancelled`.
- [ ] Fixture starts with genuine committed acquisition+treatment/stat award, an explicitly unresolved ordinary situation and offered QTE. Ordinary test GM must actually produce/commit a distinct permitted remainder result. Assert exact once-only old wound/treatment/resource/history/award and once-only new remainder result; recording intent/notification alone is not PASS.
- [ ] Implement **only** separately accepted handoff schema/authority. Preserve ordinary game meaning, no blind ProcessPlayerTurn(originalAction) replay, no copied operation IDs, no source cleanup until authoritative transfer/recovery boundary. Unsupported/ambiguous remainder before common refuses; accepted unresolved tail retains recoverable obligation.
- [ ] Exercise native process death before/after each decision/intent/request/original/cleanup/completion boundary, same-root restart and distinct-root durable-only recovery; repeated identical and conflicting delivery, changed G/remaining payload. Actual GM response loss/delayed receipt cannot silently mark done.
- [ ] G + Late2 + scoped ordinary decline regression; **accept:** mandatory standard-mechanics outcome is observed, no lost work/double effects, exact chosen turn/time policy, no cleanup of active source. Until then B07 offered UI/new-joint-QTE release and full FR063 remain blocked. Covers revised Q09 and explicit D5 requirement.

### T187 / B09 — selected-terminal D8 atomic outcome

**Files:** new QteSceneService terminal partial; modify existing QteSceneService/QteDeferredAcceptedMechanicsPlanner through named pure contributions; integration `.JointActiveQteTerminal.cs`. No generic StateDistributor/normalizer write expansion.
**Interfaces:** PrepareMortalJointTerminalAsync/CommitMortalJointTerminalAsync; exact closed terminal.basis, no future outcome in P0.
**Owner:** proposed `fr063-active-b-qte-terminal`.

- [ ] RED `Terminal_PrepareFreezesKnownSelectionAndUtcOnlyOnce`, `Terminal_XpHistoryRuntimeContinuationAndWaveConsumeCommitTogether`, `Terminal_EveryMemberCrashOrLostCallerReceiptNeverReplaysOutcome`, `Terminal_UnsupportedBranchRefusesBeforeCommon`.
- [ ] At actual persisted selected outcome/resolved wave freeze exact source reads and owner-produced before/after; PREPARE M-only. Reuse existing XP arithmetic (including total/current/level thresholds) as pure calculation with frozen timestamp, existing typed deferred plan/identity ledger; no XP compensation source.
- [ ] Produce status/narrative/history/runtime/continuation/wave after-images without calling legacy multi-write distributor/normalizer. Narrow pure extraction must preserve existing formula/normalization; if it needs extra write roots, STOP for amended grant, not use broad rollback inventory.
- [ ] Commit complete fixed subset+M terminal receipt atomically. Nonblank responseFragment.response precedence/finalNarrative fallback/score exact. Applied history row unique; resolved wave only own request/receipt/Ready. No terminal rollback of previously accepted waves.
- [ ] G; exact affected existing deferred-continuation uncertainty and closure consumers, not all qte-rules. **Accept:** full terminal before/after or HOLD, one XP/history outcome across cold/repeat, unchanged unrelated outputs/roots. Covers Q08/Q11/Q13.

### T188 / B10 — post-QTE automatic/projection и lawful drift proof

**Files:** new M QteProof partial; automatic/projection owner partials from B02/B03; engine completion, story read proof; integration `.JointActiveQteTail.cs`.
**Interfaces:** ReadMortalJointQteTailPhaseAsync, PrepareMortalJointTerminalTailAsync; existing publish/projection owner APIs consume fresh terminal slot capability.
**Owner:** proposed `fr063-active-b-qte-tail`.

- [ ] RED `QteTail_OriginalToHToCurrentAllowsOnlyTypedOutcomeDrift`, `QteTail_EarnedLevelAwardsOnlyDifferenceAfterP2`, `QteTail_InitialStoryAndNewTerminalNarrativeRemainSeparatelyExact`, `QteTail_RehashChangedCapturedRootsNeverCreatesProof`.
- [ ] Recompose genuine original→common→H and H→current with existing typed source/receipts; do not compare every lawful current root to stale P0 or waive entire footprint. M/control trio excluded from recursive capsules.
- [ ] After terminal COMMIT fresh terminal-known auto basis PREPARE; one B02-like automatic slot, then B03 projection slot. P2 watermark fences prior award, new stats consumed once, no second story/turn or recursive scene/tail loop.
- [ ] Read-only reconstruct original P4 row from original manifest/history/initial bound narrative/frozen location+labels. Separately require current narrative exactly selected-terminal after-image; public draft rules remain independent. Changed interface/debug/combat/labels not explained by supported typed writes hold.
- [ ] G + Late2; **accept:** actual QTE-earned stats/levels reflected exactly once, initial story unchanged, drift outside exact cohort fails cold. Covers Q07/Q12/Q14/R01/R03/Review Focus 3.

### T189 / B11 — terminal cleanup и cold current-owner admission

**Files:** engine terminal cleanup/active completion/QteTail; integration `.JointActiveCleanup.cs`, `.JointActiveNative.cs`; extend actual child fixture `GameEngineTurnLifecycleTests.SpiritualWorkerFixture.cs` only with bounded scenarios/args.
**Interfaces:** named CompleteMortalJointActiveCleanupAsync and fresh physical reader. D5 follow-up transfer/cleanup uses only separately accepted exact boundary.
**Owner:** proposed `fr063-active-b-cleanup`, `fr063-active-b-native-admission` (separate coherence/budget).

- [ ] RED `Cleanup_ExactSignedKAndMReleaseEveryMemberCut`, `Cleanup_ActivePausedWaveDebtOrOrdinaryRemainderNeverReleasesEarly`, `Cold_InitiallyBadAuthorityBeforePidCannotMintAdmission`, `Cold_ReleasedObservationNeverGrantsReplayOrRestore`.
- [ ] Require terminal receipt + post-auto/projection + genuine QTE/history/wave closure and no publisher debt; decline additionally proves mandatory ordinary resolution or accepted genuine transfer boundary. This dependency cannot be replaced with state=declined.
- [ ] Atomic K+M release, no recreation original/backups after commit. Fresh after-release reader only observes completion; any new follow-up mutation authority must come from accepted Q-D5 genuine owner protocol, not released marker alone.
- [ ] Before-launch corrupt real authority/header/G namespace in parent, record full raw files/directories, then actual child constructor/entrypoint/PID; include existing early JsonException shape where applicable. Same/different root durable-only cold resume, no warm parent admission or shared mutable cache.
- [ ] G + conditional exact initial9/P6/phase-refusal owners and Late2. **Accept:** no partial/zero-receipt cleanup PASS; unchanged corruption namespace, exact source-binary pins and actual root absence. Covers C01–C03/R02/Q04/Q08/Review Focus 2/5.

### T190 / B12 — player parity и GM-facing qualification

**Files:** actual joint console/browser adapters from registry; `BookOfEternityClient.WebFrontend/src/components/QteScenePanel.tsx`, `src/components/StatusView.tsx` only if product behavior requires change; new focused `test/jointTailInteraction.test.tsx` if needed. Integration `.JointActiveUi.cs`; docs **`Rules/Block_CLI_QTE.txt`** (actual QTE owner), applicable `Rules/Block_CLI_Operations.txt`, `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `TaskGuides/CLI_Step_Main.txt`, `OtherGuides/Wound_Materialization_Contract.md`, applicable `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Wound_Materialization.txt`, `Examples/E_CLI_QTE_Offer.txt`, applicable `Examples/E_CLI_Afterlife_Turns.txt`, manifest and owning source/documentation guards.
**Interfaces:** shared named joint dispatch/state tokens, clear in-world Russian UI; no API/DTO/receipt jargon to player.
**Owner:** proposed `fr063-active-b-player-parity` and finite `fr063-active-b-frontend-interaction`; exact new test methods/files only.

- [ ] RED `PlayerFlow_OfferAcceptDeclineAndResumeKeepSameMandatoryOutcome`, `PlayerFlow_AwardNotificationDoesNotAutoAllocate`, `PlayerFlow_ReadonlyWoundsAndKnownTreatmentRemainImmediatelyVisible`.
- [ ] Console/browser/agent actual entrypoints: available choice, wrong/stale/session token response, retained-state message, reload/restart, no duplicate click execution; accepted scene never has offered decline action. No technical details/hidden wounds/diagnosis blanket reveal gate.
- [ ] Worked GM examples: offer vs committed joint contribution vs **remaining** ordinary resolution, decline once with no repeated wound/treatment, accepted terminal damage/XP, crash/hold/retry. GM can complete request from explicit safe packet/guidance, not implementation reading. No prompt-only fix for missing durable guarantee.
- [ ] Run scoped documentation/source guards and selected component interactions; then actual app console/browser flows with captured output/screenshots/input/reload. Backend service PASS/static React-text checks do not qualify DOM/visual runtime. Unavailable browser/OS remains explicit UNRUN, not simulated PASS.
- [ ] G + only impacted existing readonly status guards. **Accept:** semantic parity and actual interaction evidence for this joint contour. Full guided US5 aliases/providers/quotes/confirmation/staging and ordinary allocator remain separate gates, not closed by this task. Covers GM01/U01/U02/Q02/Q09.

### T191 / B13 — bounded connected Linux/Windows acceptance и handoff

**Files:** new native integration file from B11, scoped case owners/catalog/selection, spec/plan/tasks/evidence. No general storage redesign, new global runner or privilege requirements.
**Interfaces:** existing test-csharp category runner + actual native app entrypoints; final evidence references immutable stage SHAs/binaries, not rewritten historical packets.
**Owners:** proposed `fr063-active-b-connected-{linux|windows}-{no-offer|accept|decline}`; по 4 theory rows на family. Это planned новые owners, не существующие IDs/catalog changes.

- [ ] Добавить `JointActive_ConnectedNoOffer`, `JointActive_ConnectedAccept`, `JointActive_ConnectedMandatoryDecline`, каждый с 4 literal rows `stats|level|both|none`: **12 новых connected cases на ОС**, finite exact theory membership. В accept rows закрепить `stats=deferred wave`, `level=QTE-earned additional level`, `both=physical`, `none=nonphysical`; в каждом decline row actual ordinary remainder result обязателен. Exact expected +1/+5/watermark/charge/history/story/turn/request counts вычисляются из genuine fixture before-images, не из результата проверяемого writer.
- [ ] Earlier stage evidence покрывает duplicates/caps/resolver conflicts/prior terminal/phase crash/restart/repeat/corruption. На Linux не повторять прошедшие owners без relevant change; на Windows исполнить ещё не квалифицированные затронутые native fault bodies узкими owners. Не выдавать 12 connected smoke cases за полную fault matrix и не запускать весь repository catalog.
- [ ] Linux actual native exit/child PID/lease sharing/rename/recovery/cleanup + real console/browser interactions. Windows actual equivalent with replace/sharing/case/path alias semantics and PowerShell7/.NET8/SDK10 supported setup; CI metadata/compile is not Windows run. Нет privileged ACL/service/security-setting changes.
- [ ] For every selected descriptor record planned/completed counts/method IDs/full TRX, native exit, source/binary pins, raw cleanup receipts/absent roots. Exactly before/after/uncertain/held and actual ordinal/transaction IDs; no failure overwritten by retry, no zero-receipt PASS.
- [ ] Independent final SOURCE/FIXTURE/SELECTION/EVIDENCE across intended B diff, not just last stage. Verify current selection reasons, no widening grants, retained initial9/P6, GM manifests and category ownership completeness by discovery only.
- [ ] Final non-force checkpoint/push, remote SHA readback, GitHub-only clean recovery exact commit/files, main unchanged. Close only evidence-qualified bounded tasks. Full FR063/#1536 stays OPEN until every accepted remainder (including guided/allocator/visual/platform dependencies) qualifies; no issue closure/merge is authorized.

## Coverage и acceptance ledger

| DESIGN matrix / requirement | Stage / explicit oracle |
|---|---|
| A01–A08 | B01/B02: exact initialized authority, ordered +1/+5 once, pure XP guard; B03 divergent-source projection. |
| P01 | B03: actual calculation + frozen timestamp + M ack. |
| S01 | B04: one original row/turn, exact signed prefix, foreign suffix hold. |
| Q01/Q05/Q06/Q10/Q13 | B06: all branch grammar before common, actual retirement/current source, no nested blobs; B09 terminal supported payload. |
| Q02–Q04/Q08 | B07/B11/B12: UI lease released, same chosen handoff once, source retained until terminal, actual PID and debt. |
| Q07/Q11/Q12/Q14 | B09/B10: one terminal action/XP/history, one post-QTE award; initial story/current output separate proofs. |
| **Q09 revised / D5 mandatory** | B08: actual ordinary remainder resolution, every transfer/request/completion crash, repeat delivery, same console/browser result, committed effects byte/count unchanged. Earlier no-follow-up oracle superseded. |
| C01–C03/R01–R03/V01 | B01/B04/B10/B11: exact K/headers/G/output/story/cold-current proof, no V2 upgrade/released authority. |
| U01/U02/GM01 | B05/B12: retained-tail allocation blocked all routes, safe UI and worked GM flow, ordinary allocator dependency explicit. |
| OS01/OS02 | B13 + each stage's fault controls: real OS native process/cleanup; neither inferred from the other. |
| Review Focus 1–5 | B08; B01/B11; B10; B05/B07/B12; G+B11/B13 respectively. |

**Planning proportion:** 14 stages, одна canonical implementation plan; существующий plan.md/tasks.md содержат только status/index links, а не копии этого body. Execution method уже выбран: один Sol writer + independent Astra/XHigh, не свежий implementation subagent на каждый task.

**Stage closure:** actual RED→GREEN and independent corrected/EVIDENCE gates + verified remote checkpoint; checkbox is not proof. **B no-offer acceptance** only after B05 evidence; **new-QTE acceptance** requires accepted Q-D5 + B06–B11; **player/platform acceptance** requires B12/B13 actual observations. Static schema/source checks never become runtime PASS. Existing 96 readonly cases retain original737092c39 pins; this plan adds no test result.

## Separate dependencies и открытые вопросы

- **Q-D5 — blocking new joint QTE:** выбрать и независимо спроектировать durable mandatory ordinary continuation (F1/F2 or equivalent), exact source/target schema/authority, no duplicate effects, logical turn/time policy и joint-only GM offer rule. Existing ordinary browser parity gap зафиксирован, broader rewrite вне этого плана.
- **D4 ordinary allocator:** console `CharacteristicsService.DistributePointsAsync` имеет separate writes; browser ApplyStatDistribution имеет existing coordinator wrapper, но ни он, ни B не доказывают full submit-id/crash/repeat allocation contract. Нужен отдельный accepted owner contract/verification до guided allocation; не вносить allocator redesign скрытно в B05.
- **D8 terminal limits:** only exact responseFragment/damage/physical subset, frozen earned XP; unsupported branches fail before common. Новая payload family или required normalization write root → отдельное решение, не broad legacy normalizer fallback.
- **Guided US5:** readonly RO1/RO2 не закрывают T113/T119/T127–129. Target/provider discovery, same-name/stale reachability, quote/confirmation/staging, US2/US4 resource/course/time owners и allocator — самостоятельные принятые/будущие dependencies.
- **Frontend visual qualification:** API/component/static PASS не реальные console/browser input+DOM+visual. Linux/Windows qualification нужна отдельно; недоступный runtime/OS — честный blocker/UNRUN, не повод менять security или бесконечно повторять backend.
- **Execution authorization:** user сейчас разрешил подготовить и проверить план. Ни acceptance DESIGN, ни reviewer ready-for-discussion не разрешают начинать B00 implementation/code/test/tool changes. После передачи — STOP для обсуждения.

## Self-review / consistency (planning checks, не runtime)

- [x] Сопоставить все DESIGN §1–§15 и 36 matrix IDs с stages/acceptance ledger; отметить D5 superseded semantics, а не скрыть drift.
- [x] Проверить все existing file paths и ссылки; new paths явно пометить proposed/create. Никакой генерации setup-plan поверх существующего history, agent-context или scaffold edits: manual SpecKit reconciliation; extension hooks здесь optional.
- [x] Type scan registry↔stage consumes/produces; capabilities только private issuers, no unused/unowned proposed public API.
- [x] Проверить tasks.md IDs T178–T191 уникальны, все implementation tasks unchecked, plan/spec/DESIGN/front status согласованы; no [P] при одном writer и shared storage state.
- [x] Независимое Astra/XHigh plan review, source-backed corrections, final immutable reports после FINALSTOP; remote SHA/clean GitHub recovery до handoff.

После review вернуть документ, короткую очередность, Q-D5 и зависимости; остановиться. #1557 frozen2601fd6, #1558/#1559 не запускать.

Planning closeout: [corrected independent PLAN review](../recovery/lawful-active-b-corrected-independent-plan-20261016T002709Z-review.md) на f8ab919aae3f5a62841704e37fe63b3fce020a32 закрыл P-F1–P-F4; Q-D5 по-прежнему blocked/ungranted. Итоговый carrier меняет здесь только status/review metadata; никакого implementation release.
