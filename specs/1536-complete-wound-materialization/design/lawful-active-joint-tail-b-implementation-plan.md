# Lawful ACTIVE fixed joint-tail B Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: `superpowers:executing-plans` после отдельного разрешения реализации. Один Sol writer, отдельный обязательный Astra/XHigh reviewer на каждый связный этап. Checkboxes описывают будущую работу, не выдают разрешение её начинать.

**Goal:** Завершить lawful ACTIVE stats/level-up/new-QTE joint tail без повторных эффектов, потери ordinary resolution, преждевременного cleanup или ослабления genuine original/G proof.

**Architecture:** Сохранить existing common publisher и coordinated storage; добавить fixed owner phases в marker3, atomic action+ack и named phase-aware proof. Automatic award отделён от interactive allocation. QTE acceptance/terminal сохраняют original; revised D5 требует отдельного ещё не принятого durable remaining-resolution handoff.

**Tech Stack:** C#/.NET 8, SDK 10.0.401 baseline, PowerShell 7.6.6 baseline, JSON file state, Spectre.Console, existing loopback browser/React/TypeScript. Linux/Windows — отдельные native qualifications; новые зависимости/storage services не нужны.

**Spec:** [spec.md](../spec.md), FR063/T070/[#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536); [B design](./lawful-active-joint-tail-b.md), [accepted INACTIVE contract](../contracts/mortal-wound-treatment.md).

**Planning base:** `ccb8eed3e57debdc3a5023c0cc28e75721c80c4c`, `1536-wound-b2-cloud-20261012`. В этом блоке разрешены только документы, независимое review, commit/push/readback. Implementation, tests, discovery/build/runtime tools, new authority и main не разрешены. Статус этого черновика: **WIP, independent plan review pending**. Все будущие implementation/runtime/platform checks **UNRUN**.

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

Source facts на ccb8eed3:

| Source | Факт / следствие |
|---|---|
| `Core/GameEngine/GameEngine.TurnLifecycle.cs:2345–2361` | Ordinary console decline записывает отказ, удаляет Ready и вызывает ProcessPlayerTurn(originalAction,QTE_DECLINED). Это повторное обращение для разрешения ситуации; перенести helper unchanged в committed joint tail нельзя. |
| `Rules/Block_CLI_Operations.txt:4618–4627`; `CLI_API_Specification.md:1240–1250`; `Examples/E_CLI_QTE_Offer.txt:7–9` | Offer-turn не разрешает normal state changes той же ситуации; decline требует ordinary GM resolution. Удалить follow-up значит потенциально потерять сам исход. Literal «обычные game_state untouched» также требует явного joint-only contract delta, прежде чем разрешать joint mechanics+offer. |
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

## План этапов и тестов — WIP

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

Подробные files/interfaces/RED→GREEN шаги, coverage mapping и checkpoint protocol дописываются в этом WIP до независимого review. Этот черновик не пригоден для реализации.
