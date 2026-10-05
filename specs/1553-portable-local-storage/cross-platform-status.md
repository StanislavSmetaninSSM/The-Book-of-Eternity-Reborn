# Cross-platform: текущий статус и восстановление

2026-10-05 · [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553) · ветка `codex/1553-load-filesystem`.
Этот документ — точка входа после потери контекста. Исторические «next» в старых
секциях plan/handoff не задают текущую очередь. Spec/tasks остаются требованиями,
qualification — доказательствами конкретного ограниченного блока.

## Текущий bounded native checkpoint, 2026-10-05

Восстановлен `3e1b11c220ea3286002ea78c90e4eb4498e887dd`: точное дерево исходного
WIP8339e9c2, без восстановления реализации от main. Native helper неизменён;
итоговые fixture/test corrections — `0f7b3ef366d16e40b1295ef22c6a24df04a65c4f`.
[Qualification](recovery/linux-fallback-qualification.json) содержит **22 разных
сценария** с точными source SHA:20 native helper,1 pidfd primitive,1 preparation.
Все выбранные GREEN прошли; два замечания независимого Sol6.1/xhigh получили
причинный RED→GREEN. Каталог **252/10759**,0исполнений,0пробелов. Guardian ECHILD,
ноль аварийной очистки,34 записанных собственных PID отсутствуют. Итоговое независимое
**gpt-6.1-sol/xhigh PASS** получено для `40deee8e829149f0018ec99d35cc1a19ff3ed792`.
Чистое GitHub-only восстановление подтвердило точные commit/tree и все5898файлов.
[Review/restore evidence](recovery/evidence/linux-fallback-final-review/manifest.json).
Это приёмка ограниченного native prototype; полная игра остаётся вне этой проверки.

Гарантия native-lineage ограничена обычными потомками в одной PID namespace;
внешние службы/брокеры не охватываются. Timeout/lost authority остаются Uncertain,
не разрешают автоматическое принятие, освобождение slot или удаление quarantine.
Systemd-user остаётся основным согласованным backend; его положительной native
qualification здесь нет. Windows Job, production pool/main Release, PTY/live GM и
canonical writes не менялись. Forced cleanup навсегда зависшего компилятора также
не квалифицирован: после observational timeout fixture сохраняет wait/drain до
реального выхода и затем сообщает исходную ошибку подготовки.

Текущая VM: SDK10.0.401/runtime8.0.31/**PowerShell7.5.4**, Debian13.6,
kernel6.18.44/x86_64, GCC14.2.0-19/glibc2.41. Использована существующая активация
`source /workspace/.onboarding/activate.sh`; сеть, credentials и security не менялись.
Текущий verdict carrier сохраняется обычным push/readback и отдельным свежим
GitHub-only восстановлением; точный итоговый SHA передаётся в handoff без self-reference.
Следующий шаг — handoff родителю перед отдельно назначаемой production-интеграцией.
Исторические19 zombies в старой VM не трогали и не объявляют очищенными.

## Исторический prerequisite blocker, 2026-10-05

Последний проверенный исходный checkpoint — `04fd23f5dec35cfd447b05787129faab88d535bd`.
[Полные результаты и ограничения](recovery/linux-fallback-blocked.json),
[логи/транскрипты/audit](recovery/evidence/linux-fallback-blocked/manifest.json).
Исходный native прогон19:1PASS(stale-pidfd)/18FAIL до workerlaunch из-за отсутствия
proc-children. Это не causalRED/GREEN. Guardian ошибочно оставил18helper+1sentinel
zombies уPID1; живых процессов блока нет. Исправления guardian получили отдельное
Sol6.1/XHigh PASS; проверка его отказа доfork —1/1PASS, не nativeownershipPASS.
Полный evidence-review/положительная qualification не завершены. Согласованная
fallback-стратегия сохраняется; после handoff отдельно исследуется переносимое
перечисление собственных детей. Production Release закрыт.

Приоритетный audit уведомления disconnect15:41: в это время выполнялась.NETсборка;
первый native запуск был15:42:47. Изменений сети/CLONE_NEWNET/DNS/proxy/routes/firewall,
остановкиruntimeслужб или broadgroupkill в проверенном блоке не найдено. Сигналы
nativeкода направлены своим pidfd; точныеPIDstaleprimitive не записаны. Связь с
уведомлением не установлена; историческойCPU/networkтелеметрии нет. Текущий cgroup:
peakоколо12GiB/limit16GiB,oom_kill0; это не замер ресурса в15:41. Executionдоступен.

## Восстановление и правила продолжения

**Историческая принятая база input-slice перед native работой: `f0af8c4b3affe4689e675d1dc7b1f50da334fcf6`.**
Она опубликована обычным non-force push и восстановлена в чистый GitHub-only
checkout: совпали HEAD/tree, 79 изменённых файлов и 8 входов input qualification.
Runtime input-код проверен на `dc29b37d0c3e3067acf9943a048360fbba66d0e3`;
`f0af8c4b` содержит итоговые review/evidence, а не новый runtime-прогон.

```sh
git clone --single-branch --branch codex/1553-load-filesystem https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn.git boe-1553
git -C boe-1553 rev-parse HEAD
git -C boe-1553 merge-base --is-ancestor 40deee8e829149f0018ec99d35cc1a19ff3ed792 HEAD
git -C boe-1553 status --porcelain
git -C boe-1553 ls-remote origin refs/heads/codex/1553-load-filesystem
```

Status должен быть пустым, локальный HEAD — совпадать с remote SHA; проверка
предка должна завершиться с кодом 0. Для handoff сохранять опубликованный tip,
содержащий этот документ; source SHA каждого принятого блока берётся из его
qualification. Native runtime — `3e1b11c2`, итоговые fixture/tests — `0f7b3ef3`.
Последующие docs commits находятся на той же ветке. Перед записью
fetch/readback текущего tip и проверка единственного writer обязательны.
В saved environment обычный
`git push origin HEAD:refs/heads/codex/1553-load-filesystem` работает; новых секретов
или разрешений для него не потребовалось. После bounded блока сохранять WIP,
проверять remote SHA/байты и чистое восстановление. При неожиданном продвижении
ветки остановить запись и согласовать writer; force push не использовать.

Исторический toolchain input-slice (текущая VM указана выше): Debian 13.6/x86_64, kernel 6.18.44, .NET SDK **10.0.401**,
.NET runtime **8.0.31**, PowerShell **7.6.6**. Для существующего Windows-target
bridge доступен reference pack 8.0.31; его cross-build не означает native запуск.
Для frontend сохранённая [browser qualification](recovery/load-browser-qualification.json)
использует Node **24.19.0** и npm **11.9.0**.
Подготовка — официальными разрешёнными способами, без смены security settings.

Читать [AGENTS.md](../../AGENTS.md), [workflow](../../docs/development-workflow.md),
[testing](../../docs/testing.md), [constitution](../../.specify/memory/constitution.md),
[spec](spec.md), [tasks](tasks.md), [quickstart](quickstart.md).
Repo skills: [speckit-analyze](../../.agents/skills/speckit-analyze/SKILL.md),
[speckit-plan](../../.agents/skills/speckit-plan/SKILL.md),
[speckit-tasks](../../.agents/skills/speckit-tasks/SKILL.md),
[speckit-implement](../../.agents/skills/speckit-implement/SKILL.md).
Использовать Spec Kit/Superpowers bridge, TDD/debugging/verification и отдельный
**Sol 6.1/XHigh** review по текущему owner-решению. Проверки выбирать по контракту
через `scripts/test-csharp.ps1 -Category …`; `-NoBuild` только после свежей сборки.
Не повторять неизменённые cohorts, не запускать Fast/PreMerge/full/all categories.

## Что уже подтверждено

Каждая строка имеет свою область и SHA; результаты пересекаются, поэтому общего
числа тестов или процента готовности здесь нет. «Evidence SHA» — опубликованный
carrier итогового пакета; точный reviewed candidate и OS указаны в qualification.
Старые B1/B2/B3 foundations, settings/generation/readers/backup имеют отдельные
принятые источники в [tasks](tasks.md); они не становятся новой очередью.

| Принятый bounded блок / qualification | Runtime или точный источник проверки | Evidence SHA | Независимое ревью |
| --- | --- | --- | --- |
| [Обычное Save: producer, callers, recovery/resource; Windows/Linux раздельно](save-task-handoff.md) | несколько точных источников в handoff | [ddaade72](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/ddaade72ae44936f6cf61970afb2bc80225b7731) | Astra XHigh PASS |
| [Load filesystem: Linux, включая namespace/cold/resource](recovery/load-filesystem-linux-qualification.json) | [2da4d545](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/2da4d545c9398ab1db019609d14429f9480574c2); resource fixtures [eed0ca99](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/eed0ca99e7ad8ef8306c6f4758ed55cce722fd46) | [7deb7c3e](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/7deb7c3e9fd5262d0750f3abefddc57d6e941892) | Sol 6.1/XHigh PASS |
| [Load filesystem: историческая native Windows qualification](recovery/load-filesystem-windows-qualification.json) | [e593bcfa](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/e593bcfa5e45502de9eb6d6b3949565f7c7881b2) | [07d354e1](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/07d354e16ce06796dee435003bd5fd2a023be62e) | Sol 6.1/XHigh cleared code/native Windows |
| [Typed Load: console handlers; полный live-путь отдельно](recovery/load-console-qualification.json) | [e813ad84](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/e813ad8438a25d7882810a241bd51d0be9ad3f42) | [49b1b364](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/49b1b3649881d2f44322ecef14220e61686c42ae) | Sol 6.1/XHigh PASS |
| [Typed Load: реальные browser client/backend handlers, автоматизированно](recovery/load-browser-qualification.json) | [cff44deb](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/cff44debfd7c0e091c6bbc7ec608c3fae2a8a46c) | [2bae7bb4](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/2bae7bb48855c11042b93dea0ecd48fcc7f89ccb) | Sol 6.1/XHigh PASS |
| [Worker apply/decision/rollback/recovery, без запуска worker CLI](recovery/worker-qualification.json) | [ce0d39f3](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/ce0d39f349982a44fde16bb6e85b6a499fc552b9); validator supplement [c122d3e1](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/c122d3e18550f8169b288628ecab3276758bdd7c) | [34a4690f](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/34a4690fc1587a531719e68b574ce26fc6152949) | Sol 6.1/XHigh PASS |
| [Native Linux Save names: producer → archive → Load case-pair roundtrip](recovery/save-native-names-qualification.json) | [ddd86922](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/ddd869229b79016b9b595f5eb21235c699927e6a) | [34a4690f](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/34a4690fc1587a531719e68b574ce26fc6152949) | Sol 6.1/XHigh PASS |
| [FRAME: bounded pure framing/JSON/deadlines](recovery/worker-frame-qualification.json) | [e9f9452d](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/e9f9452deb296988a0ced6feb9b8003b54449a19) | [c706e2c3](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/c706e2c3efa2c360358f99f1495f11be53c764a5) | Sol 6.1/XHigh PASS |
| [Persistent-main run record: codec/admission component; production wiring отсутствует](recovery/gm-run-record-qualification.json) | [dc8c742c](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/dc8c742cb3dc86d6cff09dadbbb412c77ae779a6) | [914f27dc](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/914f27dc331250bfd98639982d408f0e66a0bcf8) | Sol 6.1/XHigh PASS |
| [Linux worker IPC: оба peer PID/UID, native host Ready, закрытие без Release](recovery/worker-ipc-linux-qualification.json) | [9a346308](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/9a346308d02d55ddb50a49fab109fc99b8871505) | [9d5ffa0a](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/9d5ffa0a3f513a89a9f4e4c89b0743bb2f4b5d6b) | Sol 6.1/XHigh PASS |
| [Worker environment: Linux case aliases, synthetic maps; Ready без Release](recovery/worker-environment-qualification.json) | [43b60b95](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/43b60b954265e3bb404bf562cf7b9ff13dc4f953) | [b487526f](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/b487526fc5f4d11a2c02816813f870f063e9fdc5) | Sol 6.1/XHigh PASS |
| [Detached workspace: prepare/stage/read/dispose; без процесса](recovery/worker-workspace-qualification.json) | [f8c7eeae](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/f8c7eeae24cf702b87f581f442ea7c720264bc34) | [31de2e33](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/31de2e33a6f44001fee9c0c3b6c4e61c8d3aab0e) | Sol 6.1/XHigh PASS |
| [Quarantine receipt: portable filesystem audit и retry ordering; stop предпосылка](recovery/worker-quarantine-receipt-qualification.json) | [da35de54](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/da35de5498d8edf2787c987bd5a45ea5aa39e988) | [08e9805d](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/08e9805d10237d3bc50438e67b58fd33c5509c42) | Sol 6.1/XHigh PASS |
| [UTF-8: actual managed output pump/diagnostic tail](recovery/gm-output-qualification.json) | [a9472a35](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/a9472a35ebcc0e3fc0f09c168c49cef0a2629402); final tests [ff7e5a2b](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/ff7e5a2b08f8fde196151909487c1d94089ea91d) | [8ef45247](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/8ef4524742c0233b11a9e2d023c93a7ffad8c564) | Sol 6.1/XHigh PASS |
| [Input lifetime: queued writes, old keyboard drain, local uncertainty](recovery/gm-input-lifetime-qualification.json) | [dc29b37d](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/dc29b37d0c3e3067acf9943a048360fbba66d0e3) | [f0af8c4b](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/commit/f0af8c4b3affe4689e675d1dc7b1f50da334fcf6) | Sol 6.1/XHigh design/final PASS |

Ordinary-save handoff хранит несколько Windows/Linux runtime-срезов: его старые
указания «next Load» исторические. Таблица не переносит один результат на другой
SHA или ОС. Сохранённые RED, подготовительные ошибки и неполные прогоны остаются
в qualification. Эта таблица сохраняет прежние принятые блоки; текущий принятый native prototype описан выше.

## Что ещё не квалифицировано

**Owner согласовал два Linux backend (2026-10-05 15:12 UTC):** основной — уже
существующий пользовательский systemd, резервный — native supervisor для обычных
потомков в tracked lineage и том же PID namespace. [Точный контракт и текущий план](linux-ownership-design.md).
Auto предпочитает доступный systemd; до запуска явно выбирает fallback при его
недоступности. Backend/guarantee видны в status/outcome; после возможного запуска
молчаливое переключение запрещено. Windows Job semantics сохранены.

Fallback включает обычные double-fork/setsid/group changes, но не external brokers,
внешнюю lineage или unrestricted namespace migration. Для обычного запуска не
требуется невозможное доказательство отсутствия всякой внешней работы. Наблюдаемая
потеря authority, timeout/incomplete cleanup, owner/restart ambiguity — Uncertain:
quarantine/slot сохраняются, результат не принимается автоматически.

Saved environment подтверждает sockets/pidfd/subreaper, но прежние normal maps/
cgroup delegation недоступны, systemd user manager не найден. Namespace не нужен
для fallback и не объявлен единственным backend. Systemd положительно здесь не
квалифицирован; его не устанавливают/запускают/настраивают. Native helper получил
bounded положительную qualification через numeric proc/stat/PPID discovery и
exclusive wait/reap; исторический proc-children blocker снят для этой реализации.
Production Release/liveGM/canonical writes не запускались.

Открыты T041 ownership/Release/PTY и основной bridge/daemon/launcher portability,
production run/generation/fence wiring, owner loss/reboot/unknown-stop recovery
и безопасное переиспользование workspace. Local input lifetime не заменяет эти
гарантии. Пятисекундный drain — ожидание managed tasks, не native stop proof.
T042 paste/observe/submit queue, manual arbitration, configurable bindings,
unknown outcome/no unsafe replay и readiness/unknown-screen/trust требуют
отдельного связного design. Следующие фазы не приняты автоматически.

Полный live console → persistent arbitrary CLI/daemon → принятый turn → save →
typed Load → полный restart остаётся открытым. Native Windows evidence выше
сохраняет свои узкие границы; полная Windows/main-GM квалификация отсутствует.
Browser проверяется автоматизированными actual client/backend tests; новый
live-browser/visual-QA gate не требуется. Аудио/clipboard/другие platform helpers
и общая приёмка #1553 также остаются отдельными задачами.

## Следующий допустимый шаг

Решение о двух режимах принято; portable own-child discovery и bounded native
prototype прошли положительные проверки и независимое ревью. Передать родителю
точный remote SHA, qualification и финальное GitHub-only восстановление. Следующий
production integration блок назначается отдельно; T041 в целом остаётся открыт.
Никаких remount/security bypass, production pool/main Release или новых probes в
этом handoff. Main merge и issue closure не разрешены.

Неизменные требования: бесплатная single-player игра; trusted-local-player
storage без anti-player save protection; сохранение проверок формата/путей,
поколений, history и атомарного принятия результата. Нужен произвольный persistent
interactive CLI с автоматической доставкой и настраиваемыми paste/submit,
без one-shot или manual-copy замены. Main merge и закрытие issues — только
по отдельному разрешению владельца. Работа сохраняется удалёнными checkpoints.
