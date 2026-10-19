# Проверка спецификации CRASH5, редакция 2

Дата: 2026-10-19. Feature: [1536, дополнение CRASH5](../spec.md). Issue: #1536.
Проверяется новое дополнение Linux + Windows; WIN5 редакция1 заменена, прежняя спецификация всей feature не переоценивается.

- [x] Результат и место исполнения Linux/Windows различены; текущий этап только документация.
- [x] Пять identities и cut0…4 сопоставлены с исходным Theory без изменения результата.
- [x] Process loss, identity, nonce, containment, EOF/join, cleanup, механические/culture assertions и бюджеты сохранены.
- [x] Windows ordinary guards охватывают shared helper и оба прямых child entrypoints; integrity/diagnostic consumers сохранены.
- [x] Baseline G01 selection выявлен; будущий ordinary five не смешивается с guard/regression controls.
- [x] Linux ordinary уже не включает capture; влияние среды — рабочая гипотеза, timeout/policy cause не объявлены установленными.
- [x] Self-report ограничен самим тестовым процессом; external capture/diagnostic IPC и изменение политик не требуются.
- [x] Существующие процессные механизмы мостика изучены; retained owner/Uncertain и ограничения reuse записаны.
- [x] Причинный Linux fix требует regression evidence; production/material contract changes требуют уточнения scope.
- [x] Подготовка исходников, PREP, Windows qualification и Linux cloud outcome имеют разные критерии.
- [x] Исторические52/57 не повышены, builds/discovery/runtime UNRUN.
- [x] Нужные решения/формат self-report/численные пределы/guard categories явно назначены следующему plan до кода.
- [x] Нет placeholder требований, скрытых расширений в production/GM/CI или обещания исправления без доказательств.
- [x] Независимое документарное review принято: gpt-6-astra/high, diff125332..5112149, действенных замечаний нет. Это не approval владельца и не runtime PASS.
- [ ] Владельцем согласована именно редакция2.

Техническое дополнение намеренно называет существующие API и файлы, чтобы сохранить реальные контракты по прямому требованию владельца. Это не реализация; plan/tasks/analyze предстоят после approval. В документах учтён выполненный статический анализ, а не прохождение тестов.
