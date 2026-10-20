# Проверка устранения повторной валидации

Полный сценарий: **Failed**, квалифицирован: **False**. Тест: 00:12:19.0149637; runner: 00:12:38.2904705.
Компилированный код `5d4562cb3ad2f0d423e1283c39afc6dec7ac515a`, запуск `20261020-205402-927-27872-c043737257484db1bdd96c54578824be-categories`.

Сохранены все **2510** именованные записи, **1253** завершённых интервалов. Ограничений числа, байтов или времени сбора диагностики нет.

Предсказанное сокращение в настоящем общем плане подтверждено: **True**.

| Участок внутри общего плана | Вызовов раньше | Вызовов сейчас | Раньше, с | Сейчас, с |
| --- | ---: | ---: | ---: | ---: |
| `JointResources.ValidateCurrentAsync` | 8 | 8 | 129.818 | 82.831 |
| `JointBaseline.ValidateCurrentAsync` | 24 | 16 | 106.806 | 62.676 |
| `JointMechanical.ValidateCurrentAsync` | 24 | 16 | 66.276 | 38.695 |
| `Continuation.ValidateCurrentAsync` | 24 | 16 | 32.945 | 19.261 |
| `Checkpoint.CaptureCoreAsync` | 8 | 8 | 12.432 | 10.839 |

Вложенные времена не складываются. Это два инструментированных запуска с разными настоящими идентификаторами исходной сессии; время не является контролируемым бенчмарком.

Проверено завершённых resource-интервалов: 13. Их baseline-проверки и вложенность в QTE приведены в JSON.

## Замеренные этапы

| Участок | Раньше, с | Сейчас, с |
| --- | ---: | ---: |
| `GameEngine.PublishCurrentMortalJointRepairAsync` | 420.684 | 296.971 |
| `GameEngine.CreateJointRepairCommonPlanAsync` | 177.247 | 126.288 |
| `GameEngine.PrevalidateJointRepairPublicationTransactionAsync` | 90.227 | 56.865 |
| `JointPublish.NormalizeAndValidateAsync` | 80.692 | 49.787 |

В новой трассе 2 завершённых postpublication-интервала: 66.313 с, 83.441 с.

## Следующий измеренный участок

Последним начат `JointStory.CleanupEligibleTerminalAsync`. Внутренних таймеров cleanup в этом запуске нет, поэтому конкретный вызов и его длительность неизвестны. Часы коллектора и исходного ожидания процесса различаются; их разность не является измеренной длительностью cleanup. Следующий отдельный блок — короткая тестовая точка входа и именованные таймеры внутри cleanup; полномочия удаления и проверки файлов сохраняются.

## Пределы вывода

- One instrumented case per source with different genuine source identities; stage times are observations, not a controlled benchmark or an uninstrumented speedup guarantee.
- Nested inclusive intervals must not be added. End scopes may unwind on failures. Unfinished spans have no fabricated duration.
- Cheap shared-policy tests prove sequencing; existing38physical controls plus actual original owner flow are separate evidence.
- DEDUP-2/common-plan caller validation and both postpublication passes remain unchanged.
- No Linux runtime or OS/scanner attribution; historical original five not rerun.

Незавершённые интервалы: `Resumer.FirstCurrentOwnerCall`, `JointStory.TryReconcileMortalJointStoryTurnAsync`, `JointStory.FinalMaterializationLeaseAndCleanup`, `JointStory.CleanupEligibleTerminalAsync`. Их полная длительность неизвестна.

Очистка собственных корней: True. EOF/join: True.

Независимое итоговое ревью gpt-6-astra/high принято без замечаний на checkpoint46607a93. Завершающая проверка восстановления ветки ещё ожидается.
