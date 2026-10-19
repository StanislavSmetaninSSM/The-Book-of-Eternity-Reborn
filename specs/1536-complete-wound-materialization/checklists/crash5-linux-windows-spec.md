# Проверка спецификации CRASH5, редакция 3

Дата: 2026-10-19. Feature: [1536, дополнение CRASH5](../spec.md). Issue: #1536.
Проверяется дополнение NativeLineage Linux + Windows; WIN5 редакция1 и CRASH5 редакция2 заменены, прежняя спецификация всей feature не переоценивается.

- [x] Результат и место исполнения Linux/Windows различены; текущий этап только документация.
- [x] Пять identities и cut0…4 сопоставлены с исходным Theory без изменения результата.
- [x] Process loss, identity, nonce, containment, EOF/join, cleanup, механические/culture assertions и бюджеты сохранены.
- [x] Windows ordinary guards охватывают shared helper и оба прямых child entrypoints; integrity/diagnostic consumers сохранены.
- [x] Baseline G01 selection выявлен; будущий ordinary five не смешивается с guard/regression controls.
- [x] Linux ordinary уже не включает capture; влияние среды — рабочая гипотеза, timeout/policy cause не объявлены установленными.
- [x] Self-report ограничен самим тестовым процессом; external capture/diagnostic IPC и изменение политик не требуются.
- [x] Выбран прямой pipe entrypoint существующего NativeLineageOwner с неизменным C supervisor; GM handshake/PTY/worker admission не подменяются.
- [x] Отличия child environment, output truncation и fast-exit identity разобраны; pre-exec birth, root/supervisor exit, partial owner и EOF/stop contracts записаны.
- [x] Узкое внутреннее расширение production owner явно включено в scope; существующие GM/terminal defaults и C-протокол сохраняются.
- [x] Native package/PREP и native regression controls вынесены в облачный Linux; Windows остаётся отдельной квалификацией.
- [x] Подготовка исходников, PREP, Windows qualification и Linux cloud outcome имеют разные критерии.
- [x] Исторические52/57 не повышены, builds/discovery/runtime UNRUN.
- [x] Нужные решения/формат self-report/численные пределы/guard categories явно назначены следующему plan до кода.
- [x] Нет placeholder требований, скрытых расширений в production/GM/CI или обещания исправления cloud timeout без доказательств.
- [x] Независимое документарное review редакции3 принято: gpt-6-astra/high, diff3c4c34c..72979026b и адресные исправления L3/L5/L9; один Minor устранён, новых замечаний нет. Прежнее review редакции2 не переносится.
- [x] Владельцем согласована именно редакция3 из f22e51730e090e5cdc86531f589bdff05193158d ответом «да»; следующий этап — план реализации.

Техническое дополнение намеренно называет существующие API и файлы, чтобы сохранить реальные контракты по прямому требованию владельца. Это не реализация; plan/tasks/analyze предстоят после approval. В документах учтён выполненный статический анализ, а не прохождение тестов.
