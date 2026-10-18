# T148 — SOURCE-аудит memory_suppression Сарефа

Задача [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
T148; база `12d2bac1e680799c176346b6e6f29f48859e32d1`, ветка
`1536-wound-b2-cloud-20261012`, 2026-10-18. Пользователь выбрал этот SOURCE-аудит.
Статус: **WIP, независимое review ожидается**. Только чтение исходников и запись
аудита; ни тесты, ни build/discovery, ни gameplay/diagnostics не запускались.
[Входные hashes и точный search scope](../recovery/t148-saref-source-audit-inputs-20261018.json).

## Вывод и предел доказательства

В найденном production пути `memory_suppression` — **каноническая запись исхода
Сарефа с audit metadata**, а не доказанный самостоятельный runtime effect.
Реальные producer, persistence и story identity есть; мост к effect source,
effect identity/carrier, исполняемому подавлению памяти и его lifetime не найден.
Это вывод о прослеженном механизме на указанной базе, не запрет законным generic
effects или утверждение, что любая способность Сарефа не может иметь эффект.

**Saref story state не является целиком декоративным:** отдельно authored
`sarefAdvantages[].state=suppressed|disabled` реально запрещает использование
([ValidateSarefAdvantageUses:1875](../../../BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs#L1875));
`BuildAvailableWingsAdvantageArray` (:1351) пропускает только available/passive.
Но `record_defeat_outcome` возвращает результат сразу (:649–652), а его side effects
не меняют `sarefAdvantages`. Связь конкретного memory suppression audit с
advantageId/state, фрагментом памяти или effect identity не установлена.

Подтверждённого дефекта удаления/изменения существующего Saref audit при wound
cleanup статическим разбором не обнаружено. Runtime survival не проверен.
Плана исправления данных сейчас нет: отсутствие механизма нельзя чинить как
ошибку cleanup. T148 и Phase10 остаются открытыми; нужен отдельный follow-up.

## Producer, identity, carrier и lifecycle

| Звено | Реализовано | Что этим не доказано |
| --- | --- | --- |
| Вход/публикация | [FileMapping:157](../../../BookOfEternityClient/Configuration/FileMapping.cs#L157) направляет `sarefMainStoryState` и `sarefMainStoryUpdate` в `game_state/meta/main_story_saref_state.json`; [StateDistributor:536](../../../BookOfEternityClient/IO/StateDistributor.cs#L536) заменяет canonical root либо применяет `SarefMainStoryState.ApplyUpdate`. | Ни field name, ни путь сами не создают effect. Полная замена root явным Saref response — отдельная story операция, не wound cleanup. |
| Реальный producer | [ApplyDefeatOutcomeUpdate:729](../../../BookOfEternityClient/Services/SarefMainStoryState.cs#L729) клонирует defeat outcome, переносит `memorySuppressionAudit`, ставит turn и upsert по `outcomeId`. [SideEffects:784](../../../BookOfEternityClient/Services/SarefMainStoryState.cs#L784) обновляет oath/bond/final confrontation/advantage uses. | В этих branches нет создания effect definition/instance или изменения доступности памяти. |
| Story identity | [Validation:2001](../../../BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs#L2001) требует уникальный `outcomeId` с case-insensitive comparison, поддерживаемый outcome type, scene и положительный turn. | `outcomeId` не равен `effectId`; не появляется запись effect identity index или create/expire history. |
| Audit payload | [ValidateSarefMemorySuppressionDefeat:3426](../../../BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs#L3426) проверяет область памяти, severity и summary. | Эти строки не связываются с механическим target/компонентом/ограничением доступа к памяти. |
| Persistence | [Normalizer:7](../../../BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SarefMainStory.cs#L7) проецирует wrapper против canonical/backup baseline; canonical root без wrapper не переписывается этой функцией. [NormalizerBackupInputFiles:99](../../../BookOfEternityClient/Services/CanonicalStateNormalizer.cs#L99) включает Saref state; normalizer вызывает этот шаг отдельно (:447). | Общая нормализация/backup не добавляет effect carrier или effect lifetime. |
| Effect authority | [SourceKinds:259](../../../BookOfEternityClient/Services/EffectSourceAuthority.cs#L259), [SourceAuthorityPaths:25](../../../BookOfEternityClient/Services/EffectAcceptedTurnInputComposer.cs#L25), `BuildCanonicalSourceAuthority` и `EnumerateRegisteredSourceObjects` задают закрытые kinds/roots/exports. Saref story root/defeatOutcomes не являются найденным экспортом. | Отсутствия kind=`saref` отдельно недостаточно: законны `spiritual_art`, `fate_card`, `quest`, `faction`, `world_event` и другие kinds. Нужен реальный мост producer→export. |
| Carrier/consumer | [EffectCarrierCatalog](../../../BookOfEternityClient/Services/EffectCarrierCatalog.cs) читает player/NPC/combat/afterlife effect collections. Saref UI читает story root, а [пример:11162](../../../Examples/E_CLI_Afterlife_Turns.txt#L11162) требует audit. | Story отображение и текст правил не доказывают effect occurrence, mechanical consumer или скрытие/восстановление памяти. |
| Lifetime | [EffectLifecycleScheduler](../../../BookOfEternityClient/Services/EffectLifecycleScheduler.cs) выполняет source-authorized generic lifetime. Для audit нет найденного lifetime binding, expire/release или восстановления подавленной памяти. | Нельзя назначить вечность или иммунитет к generic lifecycle только по названию Saref. |

Альтернативный generic route проверен до `EffectAcceptedTurnPlanner`:
application требует реального `SourceAuthority.Resolve` (:5563), а client-owned
effect allocation/index/carrier появляется лишь после законного resolve (:6125+).
Открытого «изменить память» через outcome token тоже нет: `EffectEventOutcomeCatalog`
(:17–24) допускает только `owner_critical_failure: critical_failure → failure`.
Само присутствие Saref state среди transitive validation inputs не означает export.

## Ветви wound cleanup

Здесь прослежены все три production вызова `WoundEffectTerminalOperationPlanner.Plan`
и их общий lineage/mutation путь; дополнительно — wound collection assembly,
terminal recovery/source removal и reducer legacy/archive. Это не повторная
квалификация всех accepted-turn путей и не доказательство полного spiritual healing.

| Ветвь | Фактический отбор и мутация | Граница сохранности |
| --- | --- | --- |
| Worsen/полная rematerialization | [WoundAcceptedTurnPlanner:2738](../../../BookOfEternityClient/Services/WoundAcceptedTurnPlanner.cs#L2738) передаёт roots исходной раны. | Отбор относится к exact wound source group, не ко всем эффектам владельца. |
| Treatment severity/complication removal | [MortalWoundTreatmentSeverityRematerializationPlanner:394](../../../BookOfEternityClient/Services/MortalWoundTreatmentSeverityRematerializationPlanner.cs#L394) выбирает полный старый graph либо roots удаляемых осложнений/selected graph. [PrepareWoundBatchApplications:2615](../../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs#L2615) перепроверяет exact sealed wound и roots. | Same-rank retained roots и unrelated sources не становятся terminal по одной общей принадлежности персонажу. |
| Recovery severity/heal | [EffectAcceptedTurnPlanner.MortalRecovery:609](../../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.MortalRecovery.cs#L609) выбирает owned roots при изменении severity/heal; иначе пустой terminal set. | Завершение раны не является удалением всех player/afterlife effects. |
| Общая lineage | [LineagePlanner:2149](../../../BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs#L2149) и [Analyzer:151](../../../BookOfEternityClient/Services/WoundEffectIdentityLineageAnalyzer.cs#L151) ограничены `(realm, "wound", woundId)`, exact roots и их подтверждёнными descendants. [Terminal planner:2765](../../../BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs#L2765) сверяет definition/target/carrier/identity/stack. | Нельзя включить unrelated effect только по похожему имени, carrier или player target. Несогласованная lineage даёт ошибку вместо расширенного удаления. |
| Реальная effect mutation | [ApplyWoundTerminalOperation:5749](../../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs#L5749) повторно сверяет occurrence/identity, удаляет exact `EffectId`, добавляет expire transition в identity history. [TryRemoveEffect:8897](../../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.cs#L8897) требует одну mutable occurrence. | History сохраняется; mutation не очищает collection целиком и не принимает Saref audit ID как effect ID. |
| Active wound/source removal | [BuildRecoveryFinalSources:759](../../../BookOfEternityClient/Services/EffectAcceptedTurnPlanner.MortalRecovery.cs#L759) удаляет только exact wound row из cloned owner root. [Recovery final plan:104](../../../BookOfEternityClient/Services/WoundAcceptedTurnPlannerCore.MortalRecovery.cs#L104) создаёт selected `delete` contribution; [ApplyMutation:1150](../../../BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs#L1150) сверяет owner/woundId/before-image и удаляет один элемент activeWounds. | Saref story root не является wound collection и не выбирается этой операцией. |
| Совместный carrier | `AcceptedMechanicsCarrierAssembler.Compose` проверяет non-effect fields against accepted baseline (:625+). [ComposeLive:128](../../../BookOfEternityClient/Services/AcceptedMechanicsCarrierAssembler.LiveWounds.cs#L128) заменяет только выбранную wound collection в клоне итогового effect/owner root. [Owner projection:188](../../../BookOfEternityClient/Services/WoundAcceptedOwnerCarrierAuthority.cs#L188) удаляет только activeWounds неподдерживаемого profile. | Наличие effects и activeWounds в одном profile не разрешает очистку effects вместе с раной. Это source argument; нового runtime PASS нет. |
| Heal/legacy/archive reducer | [Reducer:2068](../../../BookOfEternityClient/Services/WoundTransitionReducer.cs#L2068) выдаёт remove wound + owned `EffectIds` для heal; legacy/archive не удаляют carrier. `EffectIds` (:2825) берёт только owned root bindings. `ValidateLegacyDeclarations` (:1963) сохраняет старые relation refs и разрешает лишь объявленные additions; `AddLegacyIntents` (:2195) выдаёт typed intents. | `independentEffectRefs` — не приказ массового удаления. Typed legacy intent сам по себе не доказывает runtime materialization Saref. |

Generic effect operations в том же accepted turn могут законно менять lifetime,
uses, reactions, source/target/realm loss. Поэтому будущий тест сохранности обязан
исключить такие события либо ожидать их точный независимый результат. Сохранность
от wound cleanup не означает безусловной неизменности любого эффекта при любом ходе.
Числовые `SpiritualHealingOutcomeMath`/`SpiritualWoundRecoveryMath` не являются
accepted spiritual healing authority; этот аудит её не вводит и не квалифицирует.

## Существующие тесты и минимальный новый test contract

`WoundConsequenceEnvelopeTests.IndependentEffects_ArePreservedAndNeverConsumeSlotsOrEnterProfileValidation`
использует искусственный `effect_saref` с неизвестным component и kind=`saref`.
Он проверяет отделение envelope, не законное создание через effect authority.
Saref defeat-normalizer и ApplyUpdate tests существуют для **forced oath**;
они не являются memory suppression/healing survival tests. Требуемый T148
`SarefMemorySuppressionWoundIsolationTests.cs` отсутствует. Нового PASS нет.

**M01 — минимальный доступный metadata regression, один будущий Fact, UNRUN.**
Предлагаемое имя: `MortalWoundRecoveryTests.Publication_TerminalHealingPreservesExistingSarefMemorySuppressionAudit`.
Это проверка существующих canonical audit данных, не full T148 effect test.

1. В новом disposable root создать существующий Saref canonical audit настоящим
   `record_defeat_outcome` → StateDistributor/normalizer producer: допустимые
   outcomeId/scene/turn, scope/severity/summary и дополнительная sentinel запись.
   Подтвердить canonical audit validity через существующий Saref validation profile,
   затем зафиксировать итоговые canonical bytes после projection. Он представляет
   уже сохранённое сюжетное прошлое, не новое поражение Сарефу в Mortal realm.
2. Сделать это **до** подписания wound creation/recovery baseline. Существующий
   recovery fixture materializes подписанный immutable template и сразу делает
   `ExportRecoveryBinding`; post-sign injection недопустима. Нужен отдельный
   per-case preparation hook/fresh factory до `PrepareSignedCreationTurn` (:1443),
   без изменения общей cached template и без forged/resealed authority.
3. Использовать реальный существующий terminal Mortal recovery/heal path, как в
   `MortalWoundRecoveryTests.TerminalState` и `PublicationRollback`: compose,
   genuine common publication, `Fixture.PublishWithCanonicalRefresh` с raw item
   authority, полный canonical refresh/validation. Не применять reducer вручную.
4. Доказать actual healed History-only wound, отсутствие её active carrier,
   exact terminal effect identities/history и ноль canonical validation errors.
   Одновременно сохранить exact Saref file existence/bytes и semantic audit rows
   (outcomeId/type/turn/scope/severity/summary/sentinel), без нового Saref response.
   Ничего не выдавать за effectId/carrier/поведение памяти. Owned cleanup root
   обязателен. Один PASS такого теста оставит T148/Phase10 открытыми.

**E01 — полный T148 independent-effect survival, BLOCKED/UNRUN.** Нужны принятый
законный Saref producer→source/export→effect identity/carrier и mechanical memory
consumer/lifetime. Тогда один настоящий end-to-end test должен материализовать
effect через production, вылечить рану законным accepted route и доказать прежние
effect identity/payload/carrier/поведение с точным ожидаемым lifecycle. Синтетический
`effect_saref`, готовый вручную carrier или generic spiritual_art вместо отсутствующего
Saref producer не закрывают E01. Реальный afterlife healing route — отдельная
зависимость; M01 в Mortal не заявляет его эквивалентность.

Это обязательное различие следует из
[contracts/wound-effects-and-atomicity.md:657–663](../contracts/wound-effects-and-atomicity.md#saref-boundary):
там требуется именно **spiritual wound healing** и unchanged independent Saref effect
bytes. Нормативное требование не является свидетельством существующей реализации.
M01 не закрывает эту acceptance; E01 остаётся BLOCKED.

**Verification dependencies:** сейчас нет C#/catalog/selection changes и нет test
release. M01 потребовал бы отдельного SOURCE/test review, fresh build/discovery и
PlanOnly, явного category owner для одного нового Fact. Ближайшие существующие
owners: `mortal-wounds-treatment-course` (recovery), `narrative-world` (Saref),
`canonical-normalization-afterlife` (Saref normalizer), `mortal-wounds-effects`
(synthetic envelope). Их существующий состав не является допуском запуска всех
этих категорий. Результаты соседних tests не переносить в M01/E01. T177 и final
Phase10 dependencies сохраняются. L01 closure/Windows не нужны для source audit;
никакой crash/sampling/CI запуск из этого документа не следует.

## Follow-up — подготовленный текст, issue не создана

**Название:** «Сареф: определить и материализовать независимый runtime контракт
memory_suppression вместо одной записи defeat audit».

**Проблема:** `record_defeat_outcome` сохраняет область/severity/summary в story
state, но прослеженный путь не создаёт отдельный effect identity/carrier и не
связывает audit с исполняемым ограничением/восстановлением памяти. Существующий
envelope test использует синтетический payload и не доказывает эту возможность.

**Приёмка отдельной задачи:** принять точный gameplay/source/identity/target/
carrier/consumer/lifetime контракт; реализовать законный producer и завершение
подавления, синхронизировать GM docs/examples/guards; доказать реальную memory
семантику и независимость от wound treatment/heal/legacy cleanup без поддельных
carriers и ослабления assertions. Архитектура и решение памяти здесь не выбраны.
Отдельно сохранить canonical audit и его provenance. Миграции старых saves не
добавлять без отдельного принятого исключения.

Spec.md требует связанную issue при metadata-only результате **до закрытия #1536**.
Этот текст — передаваемый follow-up, не созданная issue и не закрытие условия T148.
Нет подтверждённого cleanup defect для немедленного fix; при появлении такого
доказательства сначала нужны точный regression и минимальный план до реализации.

L01 остаётся 52/57; original crash5 unqualified; session66529 UNKNOWN/unavailable/
recoveryClaim=false. Все прежние failures/frozen evidence, 220/900, once-only,
no double-I, Ready immutable, exact owned transition, oldv1/child1 и broader holds
сохранены. FullNI/HOT/F1/M3/T197 не завершены; A3 NO-PROCEED; Windows403 blocked;
no CI. Gameplay/GM contract не менялся, поэтому runtime prompts/examples не правятся.
