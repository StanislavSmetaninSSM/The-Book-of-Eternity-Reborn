# T148 — независимая SOURCE-проверка цепочки Saref

Base: `12d2bac1e680799c176346b6e6f29f48859e32d1`. Только чтение source; C#/tests/build/discovery/diagnostics/runtime не запускались, repository не редактировался.

**Вывод:** текущий defeat `memory_suppression` производит persisted validated story audit. В закрытых путях source export, materialization, carrier и mechanics не установлен мост от этого audit к runtime effect. Это не утверждение, что Saref вообще не может иметь отдельно authored lawful generic effects или действующие story guards.

| Звено | Прямое source evidence |
|---|---|
| Producer и identity | `SarefMainStoryState.ApplyUpdate:649` → `ApplyDefeatOutcomeUpdate:728`; audit копируется на759, outcomeId заменяется/добавляется на764. `ApplyDefeatOutcomeSideEffects:784` не создаёт effect и не меняет advantage state. |
| Persistence и validation | `IO/StateDistributor.cs:546` → ApplyUpdate553; `CanonicalStateNormalizer.SarefMainStory.cs:24` → запись story root25. `ValidationService.SarefMainStory.cs:2009` проверяет outcomeId; `ValidateSarefMemorySuppressionDefeat:3426` требует audit scope/severity/summary, не effect identity/lifetime. |
| Альтернативные lawful sources | `EffectSourceAuthority.cs:259` допускает spiritual_art/fate_card/quest/faction/world_event и другие kinds. `EffectAcceptedTurnInputComposer.CollectSources:1581` экспортирует activeEffectDefinitions1644 через закрытый exact-path switch2282–2450. Profiles specialArts2412/fateCards2420 — реальный альтернативный путь; Saref defeat audit этим путём не экспортируется. |
| Saref-agent bridge | `ValidationService.ActorMaterializationBinding.TryCollectSarefAgentBindings:607` читает knownAgents/agentId, не defeatOutcomes. Saref state в `ValidationService.EffectMaterialization.cs:739` — transitive actor-validation dependency, не export. |
| Materialization и carriers | `EffectAcceptedTurnPlanner.ParseApplication:5563` требует SourceAuthority.Resolve; затем effectId6125, carrier6186, identity entry6188–6196. `EffectCarrierCatalog.Build:69` сканирует шесть определённых roots, без story audit. |
| Consumers и lifecycle | `EffectMechanicsSnapshot.Build:67` требует carrier/index agreement98 и отдаёт active components153. Profiles закрыты (`EffectComponentProfiles.cs:355`); generic roll consumer — `EffectRollContributionResolver.cs:28`, реальное использование — `MortalWoundProcedureCheckAuthority.cs:300`. `EffectLifecycleScheduler.AdvanceLifetime:217` исполняет типизированный lifetime; у audit такого lifecycle нет. |
| Реакции не обходят контракт | `EffectReactionResultCatalog.cs:37` — закрытые actions; `EffectEventOutcomeCatalog.cs:17` допускает только critical_failure→failure, а не произвольное стирание памяти. |
| Реальные отдельные story guards | `ValidateSarefAdvantageUses:1875` запрещает использование suppressed/disabled преимущества; `SarefMainStoryState.BuildAvailableWingsAdvantageArray:1351` пропускает только available/passive1360. Нет обязательной связи этого state с memorySuppressionAudit. Нельзя называть весь Saref state неисполняемой декорацией. |
| UI/правила | `ExplorerUniversalMetaCommandResultBuilder.BuildSarefStory:2688` показывает defeatOutcomes2735; `GameEngine.TurnLifecycle.cs:4624/4642` инструктирует GM записывать audit. Это не materialization proof. |

Все упомянутые source/test bytes сверены с base; точные SHA256, members и линии находятся в соседнем JSON.

Существующий `WoundConsequenceEnvelopeTests.IndependentEffects_ArePreservedAndNeverConsumeSlotsOrEnterProfileValidation:2322` вручную создаёт kind=`saref` и UnknownComponent2332. Он проверяет локальное исключение чужого эффекта из wound envelope; это не законный end-to-end producer. Запрошенный `SarefMemorySuppressionWoundIsolationTests.cs` отсутствует.

Минимальный честный будущий regression: получить валидный **historical story audit** через настоящий ApplyUpdate/normalizer до wound-operation baseline, затем настоящий Mortal recovery/heal и точное сравнение story audit/identity/bytes, одновременно доказав фактическое healing и нужное retirement wound effects. Возможный существующий seam — `MortalWoundRecoveryTests.ComposeAndPublishRecovery:852` и `MortalWoundRecoveryTests.TerminalState.cs:16/66`. Нельзя выдавать seed исторического audit за afterlife defeat в Mortal realm или активный cross-realm effect.

Этот тест не закрывает требование **spiritual-wound + materialized independent effect survival** (`contracts/wound-effects-and-atomicity.md:657–663`). Для него сначала нужны отдельно принятые lawful producer/source definition, client identity/carrier, suppression consumer и lifecycle/realm contract. T148 остаётся открытым; linked follow-up нужен до закрытия1536 при сохранении пробела, без расширения wound kernel в этом SOURCE-аудите.

Подтверждённого cleanup defect в этой ограниченной цепочке не установлено; cleanup/preservation отдельно проверяет parent. L01 **52/57**, пять crash targets unqualified, старый66529 UNKNOWN и остальные holds неизменны. Никакого runtime release.
