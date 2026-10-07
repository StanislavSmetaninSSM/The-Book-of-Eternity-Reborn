# Остаток переноса после принятого Daren

## Current bounded T050 console audio checkpoint

Runtime/tests `cf787a8f7873e83e7c02e96504ad8e7ff2d9870c`:27distinct scoped PASS
(20new+7affected), actual console lifecycle/Linux backend and real browser
server-output isolation. [Handoff](console-audio-linux-handoff.md) /
[qualification](recovery/console-audio-linux-qualification.json) pin typed
capabilities, original cleanup/debt, two causal phases and dummy-only native
WAV/MP3 evidence. Separate actual Sol6.1/xhigh design/source/evidence/metadata PASS;
candidate GitHub-only20917file restore clean/fullfsck verified. Final metadata
carrier exact-tip push/readback/fresh restore remains the delivery guard.
SDL2 optional, NLayer bundled; no mandatory player installation or device access.
Physical/default-device Linux, native Windows, live HTML Audio and full T050 remain
open, as do primary systemd/launcher helpers/Q1/Q2/production/live-game boundaries.
Prior clipboard/Daren source inventories below remain historical; no repeated
unrelated audits or qualification claims.


Текущая clipboard delta: runtime/tests `b5255b1fb96e405eb1ed195c6437b16ad5788246`
дают50 distinct scoped PASS через синтетические reader bytes и реальные console
consumers; [handoff](clipboard-linux-handoff.md) / [receipts](recovery/clipboard-linux-qualification.json).
Independent design/source/evidence/metadata Sol PASS; candidate61a548ab
GitHub-only restore20,755files verified clean/fsck. Final carrier exact-tip
push/readback/fresh restore precedes writer handoff.
Положительная desktop/Windows qualification и полный T050 открыты. Таблица ниже
остаётся принятым source-only audit базы Daren, до этой runtime delta; остальные
границы и provenance не переаудировались.

2026-10-07 UTC · [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553) · `T050-SOURCE-INVENTORY-AFTER-DAREN`.
База: `d73e2cdf43d63cbda3e9cda033834d0e902ae734`, принята владельцем.
Это source/inventory audit, не новая runtime qualification: **0 запусков CLI,
сервисов, probes, сборок и тестов**. Следующая реализация не начата.
Версии инструментов и состояние login ниже относятся к прежним receipts, не к новой проверке.

## Что уже принято и не повторяется

Использованы ограниченные [R1–R3/F3](main-run-fence-f3-handoff.md),
[T042](recovery/gm-input-transaction-qualification.json),
[owned terminal](recovery/owned-main-terminal-neutral.json),
[M1](production-main-m1-handoff.md), [browser rollback](browser-rollback-linux-handoff.md),
[Load UX](load-session-lifecycle-handoff.md), [direct gacha](browser-direct-gacha-linux-handoff.md)
и [standalone Daren](daren-standalone-linux-handoff.md).
Принятый Load уже означает original stop → подтверждённый Load → обязательный
current-owner refresh → fresh configured session; без активного ГМа он остаётся отсутствующим.
Старые «pending Load UX / next Daren / next browser rollback» описывают свои исторические checkpoint.
Полные T031/T032/T041/T042/T043/T052 и вся игра от этого не становятся завершёнными.
Процент не рассчитывается: checkbox объединяет разные объёмы реализации и qualification,
а число тестов не измеряет долю перенесённой игры.

## Реальные оставшиеся границы

Все source-ссылки относятся к указанной базе; точные blob/SHA256 и ordinary Git
provenance сохранены в [inventory](recovery/roadmap-after-daren-inventory.json).

| Область / реальные потребители | Фактический статус | Следующее обязательство / зависимость |
|---|---|---|
| Console clipboard: [SystemClipboardService](../../BookOfEternityClient/Services/ClipboardService.cs), `GameEngine.TurnLifecycle.GetPlayerInput` → `TextComposer.Read`, `SpectreExplorerConsole.Ask` → тот же composer | Вне Windows service возвращает явный отказ; Windows `Get-Clipboard` через `powershell.exe`/`pwsh.exe`. Composer:153–169 потребляет paste shortcut и превращает отказ в пустую строку, поэтому обычный turn:3974–3984 тоже не достигает error message в `ResolveClipboardPlayerInput`:4046–4052. Этот поздний fallback может повторно прочитать clipboard, если его успешное содержимое само равно shortcut. Единственный service test проверяет normalization, не Linux consumer. | T050: portable read adapter + видимый результат через настоящие turn/Explorer composer entrypoints, сохранение черновика, одно чтение на gesture. Без пользовательской desktop-сессии нельзя обещать clipboard capability; положительная Wayland/X11 qualification требует соответствующей пользовательской сессии. |
| Console sound/music: [AudioService](../../BookOfEternityClient/Services/AudioService.cs):107–137, 226–253 | `AudioFileReader` + `WaveOutEvent` без Linux backend. Cue исключения идут в debug log, music — в warning; это не пользовательский capability outcome. | Отдельный T050 audio block: совместимый playback/capability, volume/settings/cancel/dispose. Положительный звук требует audio device/session; никакой установки сервиса в текущей VM. |
| Browser sound: [AudioPanel](../../BookOfEternityClient.WebFrontend/src/components/AudioPanel.tsx):103–152, [BrowserAudioService](../../BookOfEternityClient/WebUi/BrowserAudioService.cs):74–104 | Playback — HTML `Audio.play()` с пользовательским unlock/error, assets — backend. Это не Windows WaveOut. Backend settings settlement вызывает общий `AudioService.ApplySettingsAsync`; эту связь учитывать при audio change. | Автотесты затронутых settings/playback handlers; отдельные codec/assets/autoplay возможности браузера. Live browser не является требованием владельца. Browser не использует console clipboard service; переносить ему Windows clipboard API не требуется. |
| Ordinary main launch: [bookofeternity](../../BookOfEternityClient/Launcher/bookofeternity.ps1):690–869, [ProductionMainLaunch](../../BookOfEternityClient/Services/GmRuntime/ProductionMainLaunch.cs):10–30 | M1 уже квалифицировал настоящий shipped route с configured persistent neutral CLI, prebuilt helper, explicit NativeLineage и отдельными caller-supplied terminal/stdin. Windows `Start-Process`/ConPTY/Job сохранены как отдельная ветвь. Это не произвольный TUI/live GM/Release gameplay qualification. | Не создавать второй launcher. Player needs .NET8 + PowerShell7 + admitted linux-x64/glibc package; SDK/compiler нужны publisher, не startup. ARM/musl и desktop/Windows qualification отдельно. |
| Launcher extras / platform helpers | `prepare-turn` в `bookofeternity`:880–908 всё ещё проверяет source project и использует Debug `.exe`/`dotnet run`; это source-oriented auxiliary route, не принятый packaged main startup. `Register_GM_CLI_Window.ps1` использует user32 и принадлежит legacy Windows route. | T050: отдельно привести нужные auxiliary commands и operational docs к shipped layout. Quickstart ещё содержит старое pending Load/browser debt и Windows instructions; не считать их текущей политикой или требованием Linux window registration. |
| Primary systemd-user: [selector](../../BookOfEternityClient/Services/GmWorkers/GmWorkerBackendSelector.cs):22–46, main resolve выше | **Не реализован и не квалифицирован**, а не просто не проверен здесь. Main принимает только explicit NativeLineage; selector возвращает SystemdUser/NotImplemented. Native fallback не закрывает primary obligation. | T041-SYSTEMD-MAIN: existing original terminal/run fence, authoritative transient unit/cgroup identity, scoped stop/ACK и mutation admission. Реальная положительная qualification — на уже работающем доступном user manager. В этой VM его нет по принятым receipts; не запускать/устанавливать/настраивать. Explicit SystemdUser не downgrade; production Auto policy требует отдельной реализации/qualification. |
| Production workers, общие console/browser daemon/QTE/repair consumers | [BridgePool](../../BookOfEternityClient/Services/GmWorkers/GmWorkerBridgePool.cs):389–395 запрашивает WorkerRelease, selector отказывает Linux; [profile templates](../../BookOfEternityClient/Services/GmWorkers/GmWorkerBridgeProfileTemplates.cs):197–198 и daemon role templates остаются `powershell.exe`-oriented. IPC peer identity уже имеет Linux SO_PEERCRED; не повторять его как отсутствующий adapter. | Отдельное production worker pool/quarantine/durable admission + actual Release/daemon wiring qualification. R1–R3 synthetic authority/cleanup не разрешают включить helpers. Main M1/Q1 может идти с helpers disabled, сохраняя original worker inventory. |
| Folder/image/font helpers: `ImageService.OpenImageInViewer/OpenImagesFolder`, оба mods-folder consumers, `GameEngine.MainMenu.OpenFolderOrPrintPath`, [ConsoleAppearanceService](../../BookOfEternityClient/Services/ConsoleAppearanceService.cs):42–46 | Folder/image opening — managed `UseShellExecute=true`, зависит от desktop association, не Windows-only syscall; image failure только logged, folder fallback печатает путь. Font resize — classic-Windows kernel32, на Linux false; options handler:176–183 уже показывает `font_size_apply_note` при failed preview. | T050 scoped desktop capability/results; сохранённый font adapter не считать отсутствующим Linux game path. Не устанавливать terminal emulator/file manager и не обещать программную смену font произвольного Linux terminal. |
| Storage / retained Windows compatibility | Новые Linux browser rollback/gacha/Daren идут через принятые trusted-local dispatch. `ExplorerLocalTurnRollbackArtifacts.StageBrowserWriteTransactionAsync`:366–368 выбирает Linux route до `DarenRewardProfileRollbackTransaction`; standalone file store:112–115 выбирает Linux publisher. `PendingTurnSnapshotAuthority`:940–972 читает старые CNG envelopes лишь для compatibility, новые portable envelope hashes уже существуют. | Эти оставшиеся Windows bodies не являются доказанным новым Linux consumer defect. Полные accepted-turn/member-set/recovery, ordinary live turn/save/load/restart остаются T030/T031/T032/T033/T052; выбирать конкретный недоказанный source-delta перед новым bounded block, не повторять все старые cohorts и не возвращать anti-player protection. |

Четыре исторических F2 Windows-only IDs остаются **непройденными** и сохранены
дословно в [Daren handoff](daren-standalone-linux-handoff.md#historical-nonpasses-and-remaining-scope).
Принятые новые Linux consumer доказательства не переименовывают их в PASS.

## Рекомендуемый следующий кодовый блок

**T050-CLIPBOARD-LINUX**: сохранить `IClipboardService`/`ClipboardReadResult` и
Windows adapter; реализовать ограниченное чтение через доступный пользовательский
Wayland/X11 tool, без установки/настройки, и подключить реальный GetPlayerInput →
TextComposer и SpectreExplorerConsole.Ask → TextComposer
failure path. Конкретный tool selection и bounded process I/O — технические решения
следующего плана, не новый clipboard UX или установка desktop в облаке.

Пользовательский результат: `/paste`, `/вставить`, `\p` читают Unicode/multiline
clipboard в поддержанной Linux desktop сессии; отсутствие capability, пустой clipboard,
ошибка или timeout дают понятное сообщение и сохраняют существующий ручной черновик.
Обычная ручная terminal paste продолжает работать независимо от service.
Критерии следующего causal RED→GREEN: настоящие service + оба consumer пути,
Unicode/paragraph normalization, одно чтение на gesture (включая содержимое, равное shortcut),
отсутствующий tool/session, child failure/timeout,
ограничение stdout/cleanup, draft retention, отсутствие автоматического submit/replay;
только узкие выбранные категории. Controlled adapter fixtures квалифицируют wiring,
а реальный Wayland/X11 clipboard остаётся отдельной native environment проверкой.
**В этом checkpoint этот план не реализован и тесты не запущены.**

Дальнейшая последовательность: clipboard → отдельный console audio/platform-helper
срез; независимо готовить primary systemd source adapter с qualification на подходящей
среде; Q1 real CLI readiness/VT → отдельно разрешённый Q2/provider+turn/save/load/restart;
production workers только отдельным admission блоком; в конце source-specific Windows
owner-run checklist и полная игровая acceptance. Это зависимости и очередь, не обещание
линейной оценки объёма или разрешение следующих запусков.

## Codex Q1: что доступно, что не доказано

Исторический [F2 receipt](main-run-fence-f2-handoff.md): `/opt/codex/bin/codex`,
`codex-cli 0.159.0-alpha.3`, help/login-status exit0, `Logged in using ChatGPT`.
Единственный [production design startup](production-main-admission-design.md#separate-real-codex-qualification)
остановился на `TERM=dumb` / `Continue anyway? [y/N]`; ответ не посылался,
Ctrl+C завершил root, независимый guardian достиг ECHILD. Ready/VT/provider не доказаны.
Точные события — [старый receipt](recovery/production-main-design.json).

Q1 **не готов к утверждению PASS**: нужен действительно поддержанный foreground
presentation, ручное наблюдение unexpected access/trust/update prompts и qualification
actual configured CLI через original M1/fence в пустом scratch, без model prompt/game files.
[TerminalScreen](../../BookOfEternityGMBridge/TerminalScreen.cs):5–61 поддерживает neutral-v1
subset (UTF8, CR/LF/BS, clear/home); unknown VT/width/alternate screen делает view unreliable.
Расширять только фактически нужный pinned CLI subset по причинным данным; TERM override
не доказывает renderer. Login receipt не доказывает provider/model availability.
Нужный terminal presentation или ручной ответ неожиданному prompt может потребовать
другой среды/решения владельца. Нового игрового model choice не требуется: сохранять profile.
Q2/model requests требуют отдельного разрешения, не вытекают из audit или Q1.

## Происхождение `5be3aa31` и отсутствие наблюдаемого конфликта

Ordinary Git: sole parent `399564194f11e2ad336023fedceb971c2f5fa699`;
author **Петр &lt;ronald.morgan@hmlservice.com&gt;**, committer тот же;
обе даты **2026-10-07T01:36:11Z**. Subject: `docs: verified bounded standalone Daren Linux verdict carrier`.
**7 metadata files, +80/−30**: status, Daren handoff/plan/qualification, общий plan/spec/tasks.
Полные paths и commit objects — inventory JSON. Runtime/test/catalog diff отсутствует.

Цепочка линейная: `39956419 → 5be3aa31 → cc84b7e9 → d73e2cdf`; обычный reflog
содержит commit entries, не actor/session/process identity. Доступные опубликованные
task receipts уже фиксируют unknown actor, независимый metadata review и последующие
исправления reviewed-base label / candidate unit / pending closure. Старый final-tip
GitHub-only restore подтверждает d73/tree, clean и 20,693 tracked files byte comparisons.
Эти receipts не содержат доказательства, кто создал `5be3aa31`.

**Actor не установлен; доказательства параллельного writer нет.** Это не доказательство,
что параллельного writer никогда не было. Git identity сама по себе actor не атрибутирует.
В проверенном состоянии конфликта нет: единственная линейная история, совпавшие local/remote
d73, clean tree на входе, нулевой production/test/catalog delta, итоговые metadata labels
согласованы с принятыми counts/review/restore receipts. История сохранена без переписывания.
Не читались секреты, auth/config files, private/closed session storage или чужие VM.

## Checkpoint discipline

Spec Kit cross-artifact consistency проверяется вручную по existing artifacts/constitution;
прежние prerequisites переиспользованы, новые CLI/hooks/test discovery не исполнялись.
Superpowers/bridge применены к source-backed inventory и ordinary публикации.
Независимый `/root/roadmap_after_daren_review`, **Sol6.1/xhigh PASS** на
`c38a5a5bfa0eb2fcb2b14bf927dc9bea4256913e`: 44 source pins, 29 links, ordinary
Git provenance и границы qualification сверены. Исправлена одна содержательная
неточность: actual turn проходит через composer до позднего clipboard fallback.
Review не выполнял writes/network/CLI/services/builds/tests/probes/discovery.
Финальный carrier содержит только verdict/task/status metadata. Exact final remote
SHA/byte readback и fresh GitHub-only source restore — в writer handoff/own host proof
без самоссылочного commit. Это восстановление исходников, не новая сборка/qualification.
Остановка перед следующим code block.
