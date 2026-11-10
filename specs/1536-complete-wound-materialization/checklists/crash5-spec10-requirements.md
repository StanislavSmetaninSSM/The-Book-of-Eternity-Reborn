# Spec10 / plan8 — consistency and design quality

- [x] F10-1→T266/map: все пять реальных publication cuts и разные recovery oracles указаны с code anchors.
- [x] F10-2→T266/boundary: source42 preparation отделена от новой Stage43; размеры и длительности не придуманы и не вычитаются между runs.
- [x] F10-3→T267/seed: подлинный генератор, DATA schema, quiescence, независимые copies, root/generation checks; private capabilities не копируются.
- [x] F10-4→T267/coverage: actual exit73/new owner/mechanics/culture/held/repeat сохраняются; seed/component не закрывают original five.
- [x] F10-5→T267/gates: скрытого source42 fallback/retry/увеличения бюджета нет; выгода и скорость восстановления пока неизвестны.
- [x] F10-6→T268/files/review: proposed files/interfaces/negative controls/stop gates; текущий diff только документация, GM contracts и Linux runtime не меняются.
- [x] setup-plan/setup-tasks/check-prerequisites выполнены в существующей feature1536. Локальный check-prerequisites не поддерживает RequireSpec; использован RequireTasks/IncludeTasks, spec.md проверен напрямую.
- [x] before_specify git-feature extension относится к созданию новой feature и требует один вызов на feature; существующая ветка и feature.json переиспользованы по user instruction. Optional git commit/context hooks не меняют AGENTS.md; сохранение выполняется явным commit/push/readback.
- [x] Constitution: issue1536 traceability; own mutable state; no production/game/GM contract changes; tests-first потребуется при будущей реализации. Текущий deliverable — source-only investigation/design.
- [x] Независимое gpt-6-astra/high ревью принято на04c6a9bbc без замечаний; GitHub recovery T268: 10 files, clean/exactHEAD/all blobs.

Bridge equivalent speckit-analyze: требования, план, tasks и границы verification согласованы; непроверенные runtime свойства помечены gates, не заменены положительным выводом. Весь проект проверяется одним coherent review.
