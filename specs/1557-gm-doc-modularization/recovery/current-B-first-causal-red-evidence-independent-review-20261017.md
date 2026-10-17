# Block 5 — independent FIRST causal RED EVIDENCE

**Verdict: ACCEPTED_EXPECTED_CAUSAL_RED.** Оба FIRST результата — сохранённые `FAIL`, 0 PASS; это достаточный причинный RED для заявленного отсутствующего переноса/доставки Block 5. Setup failure, underfill или cleanup defect не обнаружены. SOURCE остаётся отдельно принятым; GREEN не установлен. Остальные 73 случая не запускались.

Binding: compiled source `324594a930dcb3aaf17aaf2441f47193f214957c`; actual source `0709cf29646c0e2b40643ea6f138a5d01b3e9155`; опубликованный carrier `799f1c93b19b8e55be80f913f84e043ee5bb42c6`. Проверены точные сохранённые SOURCE MD/JSON `de0dc965…` / `e4facd3a…`; новый аудит SOURCE не выполнялся.

## Проверенное evidence

Все **18 цепочек** (12 native + 6 fixture artifacts) совпали по stored gzip SHA → raw SHA/length → live bytes → committed bytes. Complete GitHub recovery содержит все **30 files / 26 changed paths**; независимо сверены path set, bytes, SHA256, Git blob и mode. Diff compiled→actual→carrier содержит только feature metadata. Все 7 DLL до/после обоих запусков и текущие DLL совпадают.

| FIRST | Native | Executed / PASS / FAIL | Wrapper | Финальный oracle |
|---|---:|---:|---:|---|
| content boundary | 1 | 1 / 0 / 1 | 4.967794172 s | `GmDocumentationBlock5Tests.cs:40`, Assert.Empty |
| genuine publisher | 1 | 1 / 0 / 1 | 8.466411816 s | `GmDocumentationDeliveryTests.cs:23`, Assert.Empty |

Для каждого exact singleton: plan/summary/TRX согласованы, 1/1 descriptors и cases, selection complete, нет duplicate/skip/timeout/underfill. Оба runner cleanup завершены; budgets по 2 минуты соблюдены.

## Причинность

**B-RED-01 — отсутствующий физический перенос.** Boundary успешно прошёл frozen baseline и полный memory fixture, затем проверки 9 исходных extractor occurrences и 2 manifest locators. После этого actual filesystem содержит только старые `Rules/Block_5.txt` и `Examples/E_Block_5.txt`; все 14 новых module files отсутствуют. Live старые файлы независимо сверены с compiled source и frozen bytes: 262995 B / `895eebaba31c6d2527354c682067588142b00245d2b750f585923a6c63718d62`; 268659 B / `c244c3051f5e04bb39c973a08cb270a43500f1ba16c29cc451f134ca53a48d04`.

Поля `Sources` в diagnostic являются объявленными inventory pins; их нельзя выдавать за замер live filesystem. Число 14 относится к реально отсутствующим files. Полный failure collection `VerifyMoved` не перечислен в xUnit message, поэтому отчёт не ограничивает весь collection только этими 14: старые unmoved sources также ещё не имеют ожидаемых relocation wrappers.

**B-RED-02 — отсутствующая текущая доставка 52 docs.** Genuine producer завершился native0. Фактически доставлены прежние **36 docs** с точными role/path и source/package/copy hashes; package inventory244 совпал с accepted fresh preparation. Неизменённый daemon SHA `ceae0092297e503272fe20c8422577e63551f838bfab5091c0f3c7d4805e7c31` и actual UTF16-selected prefix проверены независимо: 202548 B, SHA `17e5b52b4789bd06b4029a8a4c6f29468cdc8e0eca743fd707c743047cccfc9c`. Изменения producer или source mutation отсутствуют.

Финальный oracle содержит ровно **49 B-only issues**: inventory1 + manifestMissing16 + copyMissing16 + routerEdges16. Старые B2 были в package, но не вошли в session publication; новые B14 ещё отсутствуют. Это ожидаемый разрыв между существующим producer36 и требуемой current52 closure, а не сбой его запуска.

Raw подтверждает pwsh PID60418/birth2095219 и два его helper PID60475/birth2095368, PID60488/birth2095398: exact argv/birth/census, затем все три `TerminalOwnedAbsent`; сигналов cleanup не понадобилось. Оба pipes пусты (0 B), EOF без ошибок. Package/session roots с suffix `3429d6773557471aa0fe8e9b48e15a3c` физически отсутствуют; evidence directory сохранён. Общий scenario elapsed 3.0536394 s при work/cleanup/total 45/15/60 s.

Actual generated template сохранён до cleanup: **12977 B**, SHA `2b850da7baee8e5b291722e1c25bfea25ce857a818d778acc8530fdb6f7e5239`. Manifest generated role/path, source/body hash и containment capture согласованы. Важная граница: проверки template в test body после failed line23 **не исполнились**; данный вывод опирается на выполненный CaptureGeneratedTemplate и независимо прочитанный retained raw.

## Решение и пределы

Материальных evidence defects нет. После сохранения этой пары и отдельного root release допустима минимальная авторизованная B implementation; далее требуются affected SOURCE/preparation и отдельный GREEN EVIDENCE gate. Этот verdict сам не разрешает runtime и не квалифицирует current52, оставшиеся73 или более широкую семью. Принятые A255/A1/A2/F1 и их ограничения сохраняются. Windows/provider/backend-worker/CI/HOT/M3 не квалифицируются.

Review выполнен только чтением existing evidence/source и вычислением hashes; repository не изменён, builds/tests/producer не запускались. Машиночитаемые точные paths, hashes, identities, issues и проверенные bindings — в companion JSON и independent-check JSON.
