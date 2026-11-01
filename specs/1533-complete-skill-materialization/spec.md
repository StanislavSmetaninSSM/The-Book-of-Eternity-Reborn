# Feature Specification: Complete Mortal Skill and Mastery Materialization

**Feature Branch**: `1533-complete-skill-materialization`

**Created**: 2026-11-01

**Status**: Part 1 review correction revision 1 approved by the user on 2026-11-01; corrections implemented, verified and independently reviewed

**Input**: User description: "[Harness/RLM] Enforce complete Mortal skill and mastery materialization — one canonical skill schema with active/passive profiles, validated constructors and deltas, fail-closed handling of duplicate, Unicode/case-confusable, stale, cross-owner, and orphaned skill references, governed companions, and bounded repair packets."

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1533 — [Harness/RLM] Enforce complete Mortal skill and mastery materialization](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533)
- **Issue type**: P1 Mortal World canonical-state, runtime, validation, and GM-prompt feature
- **Spec Kit justification**: The feature changes GM-authored skill and mastery payloads, canonical runtime state files, cross-file reference validation, normalization, repair packets, and GM-facing documentation across many files and sessions.
- **Contract scope**: Player-facing console output; GM-facing prompts and examples; Mortal World runtime state (`game_state/player/skills_active.json`, `skills_passive.json`, `skill_mastery.json`, `game_state/npcs/npc_skills.json`); validation; normalization; repair; documentation.
- **Save compatibility**: Not required. The game is technical Pre-Alpha; templates and fixtures move to the current contract and legacy skill state is unsupported rather than migrated.
- **Out of scope (for part 1)**: effect materialization (#1535), spiritual-art progression (#1534), quest materialization (#1537), actor progression (#1538), governed-companion characteristic/effect/combat-action materialization, and the remaining skill-materialization contours listed in `tasks.md` as later parts.

## Part 1 Slice — Skill Reference Identity Authority

Part 1 delivers one contour of the issue end-to-end: **every skill reference inside mastery and cross-file validation surfaces resolves against one fail-closed identity authority**. Before this part, the composition layer (`EffectAcceptedTurnInputComposer`) already rejected case-variant and Unicode-confusable skill names, while mastery validation (`ValidatePlayerSkillMasteryCore`) and mastery-initialization validation (`ValidatePlayerActiveSkillMasteryInitializationAsync`, `ValidateNpcSkillMasteryInitializationAsync`) accepted the same references through `StringComparer.OrdinalIgnoreCase` matching. A mastery entry naming `fireball` or Cyrillic-homoglyph `Firеball` for canonical `Fireball` passed validation and silently bound to the wrong-or-ambiguous identity.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Exact Mastery Reference Binding (Priority: P1)

As a player, I want every mastery change to bind to exactly one real active skill by its canonical name, so that mastery progress is never silently attached to a case-variant, homoglyph-lookalike, or nonexistent skill.

**Why this priority**: Mastery is permanent progression state. A misdirected mastery entry corrupts progression permanently and is invisible to the player until numbers stop matching the fiction.

**Independent Test**: Write `skills_active.json` with a canonical skill and `skill_mastery.json` entries that reference it exactly, with a case-variant, with a Cyrillic-homoglyph, and with an unknown name; run `GameStateValidationPhase.PlayerStateFiles` and verify only the exact reference passes.

**Acceptance Scenarios**:

1. **Given** active skill `Fireball` in canonical state, **When** `skillMasteryChanges` references `Fireball` exactly, **Then** validation accepts the reference.
2. **Given** active skill `Fireball`, **When** `skillMasteryChanges` references `fireball`, `FIREBALL`, or a Unicode-confusable variant, **Then** validation fails closed with `skill_mastery_confusable_reference` naming the canonical `skillName` to use.
3. **Given** active skill `Fireball`, **When** `skillMasteryChanges` references a name with no exact or confusable match, **Then** validation keeps failing with `skill_mastery_unknown_active_skill`.
4. **Given** several canonically distinct active skills that collapse to one confusable key, **When** a mastery reference matches that key non-exactly, **Then** validation fails closed without choosing a target.

### User Story 2 - Mastery Initialization Identity (Priority: P1)

As a player, I want every newly learned player or NPC skill to get its mastery initialization under the exact canonical skill name, so that a lookalike mastery entry can never satisfy the initialization requirement for a different skill.

**Why this priority**: Initialization is the moment a skill enters permanent progression. Accepting `fireball` mastery as initialization for new skill `Fireball` both hides a missing initialization and creates a dangling reference.

**Independent Test**: Add a new active skill for the player and for an NPC (active and passive sections), provide mastery entries under case-variant names, and run `GameStateValidationPhase.SkillContractConsistency`; verify confusable-only coverage fails closed with the new codes while exact coverage passes and missing coverage keeps the legacy codes.

**Acceptance Scenarios**:

1. **Given** a new player active skill with an exact-name mastery initialization, **When** the turn is validated, **Then** no initialization issue is raised.
2. **Given** a new player active skill whose only mastery entry is a case-variant or homoglyph of the skill name, **When** the turn is validated, **Then** validation fails with `player_active_skill_mastery_initialization_confusable` naming both the skill and the canonical name to write.
3. **Given** a new NPC skill (active or passive) whose only mastery entry is confusable, **When** the turn is validated, **Then** validation fails with `npc_skill_mastery_initialization_confusable` with the NPC actor attached.
4. **Given** a new skill with no mastery entry at all, **When** the turn is validated, **Then** the legacy `player_active_skill_missing_mastery_initialization` / `npc_skill_missing_mastery_initialization` codes still fire.

### User Story 3 - Bounded Identity Repair (Priority: P1)

As the local validation-repair harness, I want every skill reference identity error to produce a bounded repair packet, so that the GM is told exactly which file and field to rename to which canonical name instead of deleting progression data.

**Why this priority**: Routing a confusable-reference error into a generic packet that suggests removing the entry would destroy valid progression. The repair must be a rename to the exact canonical name.

**Independent Test**: Submit a validation issue list containing each new code to `WriteValidationRepairRequestAsync`; verify one `mortal_skill_reference_identity_repair` packet is emitted with the touched files, exact field corrections, and rename-only safe correction rules.

**Acceptance Scenarios**:

1. **Given** confusable-reference issues for player mastery, player initialization, and NPC initialization, **When** the repair request is written, **Then** exactly one skill-reference-identity packet is emitted containing all affected files.
2. **Given** the packet, **When** the GM follows it, **Then** the only allowed edit is rewriting the offending `skillName` to the exact canonical name from the issue's expected/actual data — never deleting mastery entries or inventing new skills.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The client MUST resolve every skill reference in mastery validation through one shared `MortalSkillReferenceAuthority` that classifies a reference as Exact, Confusable, or Unknown against the canonical skill name set.
- **FR-002**: Confusable classification MUST use the same normalization as the composition layer (`MortalLocationIdentityState.BuildConfusableKey`: trim, FormKC, invariant uppercase, Cyrillic/Greek homoglyph skeleton).
- **FR-003**: An exact (ordinal) reference MUST be the only accepted binding; confusable references MUST fail closed and name the canonical name in the repair hint; unknown references MUST keep the existing unknown-reference codes.
- **FR-004**: When several distinct canonical names share one confusable key, a non-exact reference to that key MUST fail closed without selecting a target.
- **FR-005**: New mastery-initialization coverage MUST satisfy the requirement only by exact name; confusable-only coverage MUST raise `player_active_skill_mastery_initialization_confusable` / `npc_skill_mastery_initialization_confusable` with the owning actor attached for NPCs.
- **FR-006**: Every new issue code MUST route to a bounded `mortal_skill_reference_identity_repair` harness packet with rename-only correction rules.
- **FR-007**: Actor-keyed NPC lookup semantics and unrelated consumers of the existing parse helpers MUST remain unchanged.

### Key Entities

- **MortalSkillReferenceAuthority**: immutable index over a canonical skill-name set exposing exact membership, confusable-key lookup, and ambiguity detection.
- **Skill reference**: a `skillName` string inside `skillMasteryChanges`, `NPCSkillMasteryChanges`, or `NPCPassiveSkillMasteryChanges` that claims to identify a skill owned by the same actor.

## Success Criteria *(mandatory)*

- **SC-001**: A mastery entry differing from the canonical skill name only by case or homoglyphs is rejected in 100% of player, NPC-active, and NPC-passive paths (was: accepted).
- **SC-002**: Exact references and unknown references keep their previous accept/reject behavior (no regression in existing codes).
- **SC-003**: Each new rejection carries a repair hint naming the exact canonical name and routes to one bounded repair packet.
- **SC-004**: All impact-selected verification categories complete successfully without weakening unrelated expectations. Discovery verifies category ownership of the new tests. Selection follows the current category-only policy in `AGENTS.md`, `docs/testing.md`, and the constitution; no full-suite execution is required or allowed.

## Исправления ревью PR #1579 — редакция 1 (2026-11-01)

**Запрос**: исправить пять подтверждённых замечаний независимого ревью к commit `673c531710ec3acf5132369f42a43f1a7ce8c13c` в существующем PR [#1579](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1579), связанном с issue #1533.

### Результат и границы

Ссылки mastery проверяются по точным именам без потери различий регистра. Ошибка похожего имени получает однозначное и безопасное руководство по исправлению. Пакет ремонта сохраняет все отдельные исправления. Перенос методов между partial-файлами сохраняет смысл существующих проверок, а новые тесты исполняются штатным механизмом категорий.

Это исправление части 1. Последующие части T101–T107, миграция сохранений, изменение схемы навыков, переименование канонических навыков и изменение акторной идентичности NPC остаются за пределами блока. Проверка не исправляет игровые файлы автоматически и не выбирает навык при неоднозначности.

### Сохраняемые контракты

- Точное имя принимается; полностью неизвестное имя сохраняет прежний код unknown, отсутствие initialization — прежний код missing.
- Одна и та же confusable-ссылка не объявляется одновременно неизвестной. Независимые ошибки полей и действительно неизвестные ссылки в том же файле продолжают сообщаться.
- Акторные ключи NPC, существующие потребители общих parse helpers, определение нового навыка относительно pre-turn snapshot, same-turn authority и rollback сохраняют своё назначение.
- Mastery levels, progress и остальные поля не меняются при исправлении имени. При отсутствии единственного доказуемого имени ремонт остаётся заблокированным; произвольное переназначение запрещено.
- Разбиение GameEngine на partial-файлы допустимо. Существующие assertions сохраняются; удаление или ослабление теста ради зелёного результата запрещено.
- Новые и изменённые API получают XML-документацию по пользовательскому правилу. Проверки выполняются по категориям с обязательным независимым ревью законченного блока.

### Критерии приёмки исправлений

1. При каноническом `Fireball` ссылки `fireball` и `Firеball` (кириллическая `е`) отклоняются как confusable. Одна ссылка не создаёт дополнительную unknown-ошибку или пакет эволюции навыка. Действительно неизвестная ссылка в том же payload сохраняет прежнюю диагностику.
2. При канонических `Fireball` и `FIREBALL` неточное `fireball` отклоняется без выбора цели; точные `Fireball` и `FIREBALL` не объявляются ошибочными ссылками. Сценарии проверяются через реальные validation phases, а не только изолированный classifier.
3. Если initialization содержит точное имя и похожее имя, наличие точного покрытия определяется независимо от порядка записей. Это проверяется для игрока и NPC, включая active/passive NPC sections. Это не разрешает некорректные ссылки на других поверхностях.
4. Несколько ошибок одного NPC, разных NPC или игрока в одной секции сохраняются в repair-пакете как отдельные адресуемые исправления. Коррекция однозначно указывает актор/навык или индекс записи; неоднозначная ссылка не получает выдуманного имени для подстановки.
5. Пять регрессий `ValidationRepairRequestTests` / `ValidationSourceGuardTests`, найденных ревью, устранены. Остальные проверки, читающие перенесённые методы, учитывают все соответствующие partial-файлы и сохраняют проверку прежних инвариантов.
6. Все 20 исходных новых тестов и добавленные регрессионные тесты имеют владельцев в `tests/categories.json`. Текущий `tests/selection.json` описывает именно исправления #1533 и затронутые проверки. Инвентаризация не обнаруживает новых бесхозных тестов; план выбранных категорий включает новые сценарии.
7. Выбранные категории завершаются полностью без failures/skips и с успешной очисткой. Зафиксированы команды, проверенный SHA и результаты. Независимый ревьюер проверяет исправления и достаточность selection до завершения блока.

### Обоснование подхода и статус

Предлагается восстановить уже заявленные гарантии части 1 и добавить проверки воспроизведённых случаев. Изменение общих правил идентичности для всех потребителей и откат разбиения большого файла не требуются: первое расширяет область воздействия, второе не устраняет зависимость проверок от размещения методов. Детали реализации остаются за исполнителем в рамках этих контрактов.

Открытых продуктовых вопросов нет. Пользователь одобрил редакцию 1 ответом «Одобряю, выполняй исправления» 2026-11-01. Далее обновляются `plan.md` и `tasks.md`, проводится анализ согласованности, RED/GREEN и независимое ревью. Слияние PR не входит в запрос на исправления.
