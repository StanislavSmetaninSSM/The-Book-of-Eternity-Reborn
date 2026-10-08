# Независимый changeset-аудит #1553 — WIP

Источник: [issue #1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), прямое поручение владельца 2026-10-08. Только аудит, без ремонта, сборок, тестов, live GM и HOME-PC. Runtime-исполнитель один: `1553-storage-migration-cloud-20261008`.

База `1fc5e59b253c358a8622df66a4e9459f5f3fe78b` имеет то же дерево, что интеграция PR1554 `f6dc2a1c`. Проверенный remote main: `d0241e71e349fbe2020e4a41b6be7c627c81cbb4`; ветка исполнителя на первом чтении: `852953b338ebfc7574df5c1f5503b29d250e0b03`. Отчётная ветка `1553-changeset-audit-cloud-20261008` от main. Код обеих веток не изменяется.

## Срочные дополнительные findings — не runtime FAIL

### A01 — Windows Bridge status publisher не подключён (высокий приоритет)

PR1555 `6381a950`: прежний `BookOfEternityGMBridge/Program.cs` `WriteStatusFile` напрямую писал JSON; теперь :1775–1778 вызывает только `QueueOriginalStatusPublication`. Последняя в `BridgeHost.StatusPublication.cs:44` возвращается при `_statusSealed` (initial true) или отсутствии `_mainRun`. Windows production `StartShellCoreAsync` (`Program.cs:500–529`) создаёт `ConPtySession` и вызывает `WriteStatusFile`, но не создаёт coordinator и не открывает publisher. `OpenOriginalStatusPublication` находится только в neutral/Linux ветках (:474,:497). `gm_bridge_status.json` остаётся отсутствующим или устаревшим для launcher/daemon. Статическая цепочка проверена; branch executor не меняет Bridge на указанном SHA.

Causal test исполнителю: настоящий Windows production startup на чистом изолированном root → свежий status с текущими pipe/input binding → launcher/daemon automatic operation → restart со сменой binding. Проверить фактическую публикацию и отказ stale status; relay-only и neutral Linux тесты не доказывают Windows dispatch.

### A02 — normalizer сохраняет прежнюю компенсацию после нового uncertain outcome (статический высокий риск)

PR1555 меняет ordinary facade; migration `83f6e9a7` делает uncertainty typed. Неизменённый `CanonicalStateNormalizer.PrivateImplementation.cs:166–199` оборачивает любую такую ошибку в `CanonicalStateWriteException`. `AcceptedTurnCanonicalStateRefresh` в `CanonicalStateNormalizer.MortalItems.cs:1842–1884` ловит всё, вызывает `RestoreBeforeImagesAsync`; :1927 оборачивает ошибки каждого restore и продолжает цикл. `GameEngine.ValidationAndRepair.cs:677–710` переводит итоговую ошибку в fail-closed report/TerminalRejected; :2265–2314 обещает rollback. Это дополнительная конкретная цепочка к F08/F09, отдельная от уже найденных QTE/pre-turn catches. На executor852953b3 эти файлы не изменены.

Causal test: вызвать настоящий accepted canonical refresh с исходным plan/baseline, inject current publication uncertainty (и отдельно uncertainty во время compensation), считать дальнейшие writes/restore/settlement, проверить сохранение typed decision и отсутствие неподтверждённого rollback/notification. Не смешивать с самостоятельным wound settlement contract; его handlers требуют отдельной трассировки.

## Незавершённое

Полный пофайловый индекс и реестр контрактов составляются. Независимый агент проверяет полноту GM/session/worker/Bridge/protocol и итоговый документ. Текущий WIP не означает полный аудит или принятую корректность. Никакие tests/build/discovery/PlanOnly не запускались.
