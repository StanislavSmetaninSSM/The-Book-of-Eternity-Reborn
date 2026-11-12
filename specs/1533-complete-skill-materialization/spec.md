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

## Part 2 Slice — Prose-Claimed Skill ↔ Per-Skill State Delta Coupling (T107 / #1479)

**Branch**: `1533-prose-state-delta-coupling` | **Created**: 2026-11-02 | **Status**: in implementation

Part 2 delivers contour T107 end-to-end: **a prose-claimed skill use/advancement is coupled to a state delta for that exact skill, classified through the part-1 `MortalSkillReferenceAuthority`**. Issue [#1479](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1479) (reopened 2026-11-01) is the source evidence: prose says `Чтение печатей` helped, while `skillMasteryChanges` stays empty.

### Gap corrected by this part

The existing audit `ValidateAcceptedTurnProseStateDeltasAsync` already detects a skill-use claim without any state delta — but the delta check is **file-level**: any change to `skill_mastery.json` / `skills_active.json` / `skills_passive.json` satisfies a claim about *any* known skill. A mastery delta that advances only skill B silently satisfies a prose claim about skill A, and a confusable mastery reference counts as a valid delta without identity classification.

### Functional requirements

- **FR-101**: A prose skill-use claim for canonical player skill X is satisfied only by one of: (a) a `skillMasteryChanges[]` entry whose `skillName` classifies as **Exact** binding to X under `MortalSkillReferenceAuthority` built from the player's canonical active+passive skill names, with this-turn change evidence at entry level — when a validated pre-turn snapshot exists, the entry must be new or structurally different from its pre-turn counterpart under `JsonNode.DeepEquals` (a carried-over unchanged entry never satisfies the claim, even when the file changed for an unrelated skill, and purely lexical serialization drift such as reordered keys or whitespace does not count as a change); when no snapshot exists, the weaker `filesModified` fallback applies and is recorded as such; (b) X first appearing as a **new** skill in this turn's `skills_active.json` / `skills_passive.json` (post-turn name set minus pre-turn name set); (c) for a canonical **passive** skill X, a progression change inside its own `passiveSkillChanges[]` entry: any of `masteryLevel` / `currentMasteryLevel` / `currentMasteryProgress` / `masteryProgressNeeded` still present in the current entry differs from the entry's validated pre-turn counterpart under `JsonNode.DeepEquals` (a field dropped or mis-cased during the GM's rewrite is not progression); when the pre-turn snapshot is absent, the weaker `skills_passive.json` file-changed fallback applies and is recorded as such; (d) the unchanged explicit no-progress rationale escape hatch. Writing a passive skill into `skillMasteryChanges` is not a valid delta path: that surface is active-skill-only by part 1 (`skill_mastery_unknown_active_skill`), and the coupling audit's expected/repair text says so. A snapshot or current file that exists but is **unreadable** (malformed JSON, duplicate keys, blank/whitespace-only content) is not the same as an absent snapshot: there is no weaker fallback, and the coupling audit fails closed with an explicit repair diagnostic. New-skill detection (path (b)) applies the same distinction: an unreadable pre-turn snapshot or current file never fabricates "new this turn" skills from an empty parsed name set. Canonical name sets preserve ordinal case differences (case-colliding names such as `Fireball`/`FIREBALL` stay distinct entries), and claim detection resolves ambiguity **per mention**: a sentence spelling group members in their exact canonical casing claims only those members, while a sentence matching a collision group only case-insensitively claims the whole group (fail closed on ambiguity).
- **FR-102**: Confusable or ambiguous mastery references never satisfy a claim; their identity diagnosis stays with the part-1 layer (`skill_mastery_confusable_reference`), no double-reporting of the same root cause under the coupling code when an exact delta for the claimed skill exists elsewhere.
- **FR-103**: The issue code `accepted_turn_skill_claim_missing_state_delta` is retained; its `actual` names the delta's real target skills (or the absence of a delta), so the GM repair stays narrow — including honest deletion diagnostics: a removed mastery file or a removed mastery entry is reported as removed, never as "unchanged". Quest-clue audit, combat-signal `HasSkillMasteryChanges` boolean semantics, and all legacy codes are unchanged.
- **FR-104**: NPC prose claims remain out of scope — no NPC prose-claim detection surface exists today; recorded here as an explicit no-update rationale, not an omission.

### Acceptance scenarios

1. **Given** known skills `Чтение печатей` and `Ножевой бой`, **When** prose claims `Чтение печатей` helped and the turn's only mastery entry targets `Ножевой бой`, **Then** validation fails with `accepted_turn_skill_claim_missing_state_delta` naming `Ножевой бой` as the actual delta target.
2. **Given** known skill `Чтение печатей`, **When** prose claims it and the only mastery entry references a case-variant `чтение печатей`, **Then** the coupling audit does not count it as a satisfying delta (fail closed); identity diagnosis remains with the part-1 code.
3. **Given** the same claim, **When** the mastery entry names `Чтение печатей` exactly and the file changed this turn, **Then** no coupling issue is raised.
4. **Given** prose claims a skill that is **created** this turn in `skills_passive.json`, **Then** no coupling issue is raised for it.
5. **Given** a stale `skill_mastery.json` identical to the pre-turn snapshot (entries carried over, not rewritten), **When** prose claims a skill listed there, **Then** the claim is not satisfied by the stale entries.
6. **Given** an existing passive skill `Чтение печатей`, **When** prose claims it and its own `passiveSkillChanges[]` entry advances `currentMasteryProgress` (or mastery level fields) versus the pre-turn snapshot, **Then** no coupling issue is raised; when the same entry only changes cosmetically (non-progression field or reordered keys), **Then** the claim is not satisfied.
7. **Given** a duplicated JSON key (e.g. two `newMasteryLevel`) in the current `skill_mastery.json` or in the pre-turn snapshot, **When** prose claims a skill, **Then** the coupling audit fails closed with a repair diagnostic instead of throwing or silently skipping the comparison.
8. **Given** canonical names `Fireball` and `FIREBALL` coexist, **When** prose mentions `FIREBALL` in its exact canonical case and the turn's delta targets `FIREBALL`, **Then** the claim is satisfied; when prose mentions `fireball` (no exact-case canonical member) without its own exact delta, **Then** all colliding members are treated as claimed and unmatched ones are rejected.
9. **Given** a malformed (`{`) or blank (whitespace-only) pre-turn snapshot, **When** prose claims a skill listed in the current file, **Then** the claim is rejected: a corrupted snapshot neither fabricates "new this turn" skills nor unlocks the weaker fallback reserved for absent snapshots.
10. **Given** canonical `Fireball`/`FIREBALL`, **When** one sentence claims `Fireball` in exact case and another sentence claims `fireball` ambiguously, and only `Fireball` has a delta, **Then** the ambiguous sentence still claims the whole colliding group and `FIREBALL` is rejected — an exact mention never suppresses ambiguous mentions in other sentences.

### Out of scope for part 2

T101–T106, quest-side per-objective coupling beyond the existing file-level check, NPC prose claims, save migration. Claim-detection markers stay as-is; the only detection change is the fail-closed disambiguation across ordinal case-colliding canonical names (FR-101), not new markers or broader NLP.

## Part 3 Slice — Canonical Skill Profile Schema Consolidation (T101)

**Branch**: `1533-skill-schema-consolidation` | **Created**: 2026-11-02 | **Status**: in implementation

Part 3 delivers contour T101 end-to-end: **one canonical active/passive skill profile validator shared by the constructor (composition) path and every delta validation path, for player and NPC alike**, with canonical rarity/type enums, `activeEffectDefinitions` and `structuredBonuses` checks in one place.

### Gap corrected by this part

Profile validation exists today as `ValidateActiveSkillObject` / `ValidatePassiveSkillObject` (ValidationService.PlayerAndInventory.cs) and is already reused by the delta surfaces: player `activeSkillChanges`/`passiveSkillChanges` state validation, NPC core changes (`ValidationService.NpcCoreChanges.cs`), NPC skill state and reward payloads (`ValidationService.NpcWorldAndMeta.cs`). Three gaps remain:

1. **The constructor path does not run the profile schema at all.** `EffectAcceptedTurnInputComposer.ComposeSkillAcceptedRootCore` / `ApplySkillUpdate` materialize merged skill objects into accepted canonical input checking only reference identity (exact/confusable `skillName`/`skillId`). A structurally invalid profile — missing `combatEffect`, unknown passive `type`, malformed `structuredBonuses` — passes composition silently as long as the name is exact, and is only caught later if a delta-surface validator happens to run over the same payload.
2. **Enum sources are duplicated.** `AllowedPassiveSkillTypes` exists verbatim in two places (`NpcCoreChangesContract.cs` and `ValidationService.PrivateImplementation.cs`); the two copies can drift.
3. **`rarity` is not enum-checked.** Both profiles validate `rarity` as a non-empty string only, while the rest of the game already pins the canonical ladder `Common | Uncommon | Rare | Epic | Legendary` (items, soul relics, achievements) and GM examples author exactly these values.

### Recorded decisions

- **Passive `group` stays free-form.** The tasks.md contour wording says "rarity/type/group enums", but GM-facing evidence (the compact skill template and worked examples) authors narrative Russian groups such as `"group": "Архивное дело"` alongside `"Knowledge"`. Constraining `group` to an enum would break legitimate GM authoring for no integrity gain — the field is a display/narrative label consumed by no mechanical rule. Consolidation centralizes its existing non-empty-string check into the shared validator; this is an explicit, evidence-based deviation from the contour wording, recorded here for review.
- **Skill `rarity` enum = `Common | Uncommon | Rare | Epic | Legendary`** — the same ladder the game already uses for mortal/afterlife items and relics; GM templates and examples already author within it.
- **Composition-path failures are bounded issues, not silent acceptance.** The composer already has a fail-closed issue channel (`AddSkillCompositionIssue`, codes `effect_skill_composition_*`); profile violations found during composition reuse that channel with a new dedicated code rather than throwing or writing invalid state.

### Functional requirements

- **FR-105**: One canonical skill profile contract (working name `MortalSkillProfileContract`) MUST own the complete active and passive profile validation: required fields, `combatEffect` shape and activation-flag polarity, `scalingCharacteristic` membership, passive `masteryLevel`/`maxMasteryLevel`, `structuredBonuses` presence/mirror rules, `activeEffectDefinitions` via the existing effect-definition check, and enum membership for `rarity` and passive `type`. All current delta-surface validators (player skill changes, NPC core changes, NPC state arrays, reward payloads) MUST delegate to it instead of carrying their own copies.
- **FR-106**: Canonical enum sets MUST live in exactly one shared location: passive `type` (`KnowledgeBased | CharacteristicBonus | BodyModification | CombatEnhancement | Utility`, the two existing copies merged) and skill `rarity` (the five-rung ladder). Passive `group` MUST remain a free-form non-empty string (see Recorded decisions).
- **FR-107**: The constructor path (`EffectAcceptedTurnInputComposer` skill composition) MUST validate every skill object this turn's accepted changes write — brand-new entries and full-object updates — through the same canonical contract, failing closed with a bounded `effect_skill_composition_*`-family issue that names the path and the violated rule. Carried-over pre-turn entries that this turn did not touch are NOT re-validated by the composition gate (they remain the delta/state validators' responsibility); validating them would silently widen the gate's scope. Identity semantics (exact/confusable/ambiguous/unknown) and removal behavior MUST NOT change.
- **FR-108**: Existing issue codes, sections, severities and repair hints on the delta surfaces MUST remain byte-compatible for payloads that were already diagnosed (no double-reporting when both composition and a delta surface see the same payload — composition is the constructor gate, delta surfaces keep their diagnostics for direct state validation).

### Acceptance scenarios

1. **Given** an accepted turn whose `activeSkillChanges` entry lacks `combatEffect`, **When** the composition layer materializes it, **Then** composition fails closed with a bounded invalid-profile issue naming the path (previously: silently materialized).
2. **Given** a passive skill with `"type": "MegaBoost"` on any player or NPC delta surface or in composition, **Then** exactly the canonical enum diagnostic fires from the shared contract, with the same code everywhere.
3. **Given** a skill with `"rarity": "Mythic"`, **When** any path validates it, **Then** validation fails naming the canonical ladder `Common | Uncommon | Rare | Epic | Legendary`.
4. **Given** a passive skill with `"group": "Архивное дело"` (free-form narrative group), **When** any path validates it, **Then** no group diagnostic fires (positive guard for the recorded decision).
5. **Given** the same invalid profile seen by both composition and a delta-surface validation, **Then** the root cause is reported once per surface contract without contradictory or duplicated codes.
6. **Given** the five passive `type` values and five `rarity` values, **When** player and NPC payloads use them, **Then** all paths accept them identically (parity guard).

### Out of scope for part 3

T102–T106; afterlife/spiritual-art schemas; mastery progression semantics; renaming canonical skills; save migration; `activeEffectDefinitions` *content* semantics beyond the existing shared check (effect materialization is #1535); a passive `group` enum (explicitly rejected above).

### T101 review clarification (2026-11-10)

FR-108 preserves each existing individual rule's code, section, severity, message,
expected/actual and repair hint. It does not require preserving two reports for
one rule inside NPC Fate Card validation: the shared production profile diagnostic
is retained and the redundant npc_core_changes_fate_card_invalid profile report is
removed. This also removes the redundant NPC wrapper reports for required profile
strings, active combatEffect/activation/actionCost, passive mastery integers,
structuredBonuses shape, and nested combat effect required fields. Each individual
rule retains the production canonical diagnostic; no previously rejected profile
becomes accepted. NPC-only closed-member restrictions remain independently enforced.
In particular, NPC active Fate Card rewards still require a non-empty combat
`effects` array: absent `effects` keeps its existing NPC shape diagnostic, while
present malformed/empty arrays use their existing shared diagnostic without a
second wrapper report. The analogous absent actionName gap under non-true
activation also retains the independent NPC shape rule.
FR-107 covers NPC canonical activeSkills/passiveSkills, UpdateNPCs writes and
NPCCoreChanges Fate Card reward additions as well as player writes. Unchanged
profiles are identified only within the exact same NPC owner, including a move
between canonical carriers. The gate preserves the exact offending field path.

## Part 4 Slice — Exact duplicate rows in current skill state (T102-A)

**Source**: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533)
**Branch**: `1533-exact-skill-state-duplicates` | **Date**: 2026-11-10

This bugfix continues the issue's fail-closed duplicate-state goal. Current raw
validators accept repeated exact names and later name sets silently collapse
identical or conflicting progression rows. This is supported current state, not
old-save compatibility. T102 as a whole and issue #1533 remain open.

- **FR-109**: Current player `activeSkillChanges`, `passiveSkillChanges` and
  `skillMasteryChanges`, and NPC `NPCActiveSkillChanges[].skillChanges`,
  `NPCPassiveSkillChanges[].skillChanges`, `NPCSkillMasteryChanges` and
  `NPCPassiveSkillMasteryChanges` MUST reject repeated exact skill identities
  within one owner and collection, whether rows are identical or conflicting.
  Repeated same-owner NPC groups share the same collection identity. Validation
  runs through ordinary state-file phases without a pending snapshot or a changed
  composition input. Diagnostics identify the owner, offending indexed skillName
  path and first occurrence. Other invalid-field diagnostics remain independent.
- **FR-110**: Preserve the existing actor-key rule (ID preferred, name fallback;
  owner keys compared case-insensitively), separate owners and active/passive
  collections, and ordinal skill names. Preserve current name-reader semantics:
  player names trim surrounding whitespace, NPC names retain their existing bytes.
  Exact canonical siblings such as `Fireball` / `FIREBALL` remain accepted, as
  required by FR-101 and Part 2 scenario 8. T102's old blanket "confusable"
  wording does not authorize a new global canonical-name prohibition here.
- **FR-111**: Never choose a winning row, auto-deduplicate, discard progression,
  or offer automatic ambiguous deletion. Retain the existing repair flow with
  explicit diagnostics and synchronized GM guidance plus a worked example.

Acceptance: identical and conflicting duplicates fail for every listed collection;
NPC duplicates across same-owner groups fail; different owners, active versus
passive collections, single rows and exact ordinal siblings pass. Malformed rows
still get their existing shape/field errors. Validation does not mutate inputs.

Non-goals: global confusable canonical-name rejection, reference-matching changes,
`npc_core` mirror uniqueness, cross-owner reference ownership (T104), schema or
provenance changes, migration, new repair architecture or other T102-T106 contours.


## Part 5 Slice — Current NPC mastery ownership (T104-A)

**Source**: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533)
**Branch**: `1533-exact-skill-state-duplicates` | **Date**: 2026-11-10

As a player, I need each current NPC mastery row to refer to a real skill of that
same NPC and skill kind, so another actor's progression cannot create a phantom
skill. Existing field checks and new-skill initialization do not reject an extra
mastery row for NPC B when only NPC A owns that skill.

- **FR-112**: During `NpcStateFiles`, every named `NPCSkillMasteryChanges` /
  `NPCPassiveSkillMasteryChanges` row MUST resolve to an ordinal-exact active /
  passive skill owned by that same actor in current canonical NPC carriers:
  `npc_core.json` `UpdateNPCs` / `NPCsInScene` actor `activeSkills` / `passiveSkills`,
  or `npc_skills.json` corresponding same-actor `skillChanges`. Same-turn skill
  additions and retained current core skills count; mastery rows do not create
  their own authority. A missing owner-local skill, including a name present only
  for another owner or skill kind, fails with `npc_skill_mastery_unknown_owned_skill`
  at the indexed mastery `.skillName`, with Actor, section and expected source.
- **FR-113**: Reuse existing owner identity: ID preferred, name fallback, owner
  keys compared case-insensitively. Do not alias a name-only row to an ID-keyed
  owner or let a matching display name override a different ID. Preserve ordinal
  skill names and NPC whitespace bytes, distinct exact siblings, repeated
  same-owner groups and independent active/passive collections. Missing/malformed
  identity or skillName keeps existing shape diagnostics, without fabricated
  ownership findings. Existing duplicate, field and initialization findings remain
  independent, and validation never changes input data.
- **FR-114**: Ownership rejection MUST use the existing repair flow with a bounded
  hint: inspect that actor's current skill source and progression evidence; never
  automatically switch owner, invent a skill, delete progress or choose a similar
  name. Synchronize the Mortal GM rule, main step guidance and worked example.

Acceptance: active and passive cross-owner/orphan/wrong-kind references fail
without a snapshot or changed input. Exact same-owner core skills and same-turn
skillChanges pass, including mastery-only updates of retained core skills, repeated
groups and ordinal siblings. Matching names for two different owners are valid
when each independently owns the skill. Name fallback and ID precedence remain
unchanged. Independent malformed-field and duplicate findings survive. A skill
that exists only in a pre-turn snapshot cannot grant current ownership.

Non-goals: NPC identity alias redesign, core mirror uniqueness, schema or save
migration, removal-selector semantics, new cache/repair architecture, companion
expansion, global confusable-name prohibition, or completing all T103-T106.

## Part 6 slice — T105 display summaries cannot grant characteristics

Source: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).
Apply the existing Block_8/current passive profile contract: `playerStatBonus` is
only display/narrative text, never numerical authority. A passive skill with no
structured characteristic bonus must contribute zero characteristic bonus even
when that summary contains a number and a characteristic name. Preserve the
existing structured permanent/conditional bonuses and accepted effect sources.
No new schema, effect planner, companion lifecycle, save migration, or knowledge
check adjudication is introduced. Remove contradictory summary-fallback guidance
in Blocks 12/14 and provide an executable worked example. This is only a bounded
T105 contour; broader companion materialization remains open.

## Part 7 slice — T103 current canonical mastery closure

At the existing leased post-normalization accepted-turn gate, every player mastery
reference must exactly identify a current composed active skill. Snapshot-only,
removed and same-ID-renamed old names cannot authorize it. Preserve raw-delta
semantics, retained mastery-only updates and exact/confusable selector handling.
Reuse this side-effect-free closure check before save creation and archive load
publication. Reject malformed present skill/mastery containers; both absent is
valid for pristine/other-realm states. Never delete mastery or guess a replacement
name. Existing indexed unknown/confusable diagnostics and the current repair flow
remain authoritative. No persisted stage flag/cache, save migration, selector
rewrite, NPC lifecycle expansion or whole T103 completion claim.

## Part 8 slice — T106 generated skill template guidance

Source: [#1533](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1533).
The session-generated `MORTAL_SKILL_PROGRESSION_TEMPLATE.md` must explicitly teach
ordinal case/Unicode skill reference identity and the existing identity packet's
indexed, same-actor, non-destructive repair discipline. Preserve progression and
unrelated fields; never invent/rename canonical skills, transfer mastery or guess
an ambiguous target. Preserve an entry belonging to another real skill and follow
the packet's separate initialization instruction. Descriptive ambiguous expected
text is not a replacement name. Completion uses current request metadata only
after all corrections are resolved; a spelling repair needs no training purchase.

Acceptance exercises the actual complete daemon and its generated manifest,
template and README from an independently bootstrapped session. Preserve Russian
text and deterministic owned process/stream/lease/root cleanup. Linux uses the
existing installed `pwsh` choice; native Windows PowerShell 5.1 remains separately
unverified. Retain the daemon UTF-8 BOM. The existing indexed multi-actor/ambiguity
worked example and launchers remain authoritative without duplicate examples.
No runtime identity, save/load, owner, removal-selector or afterlife policy changes.

## Part 9 slice — T103 QTE terminal mastery closure

The original #1533 fully composed-result/stale-reference contract also applies to
the existing QTE terminal publication path. A real typed probe confirmed that its
normalizer consumes exact skill removal/replacement while snapshot-aware general
validation still accepts the old mastery reference. Reuse Part 7's current-only
closure after QTE normalization under the caller's existing lease, before success.
Its integrity failures cannot be waived as pre-existing state issues; unrelated
existing diagnostics keep the established waiver behavior. Preserve the current
terminal restoration and cleanup machinery, all mastery bytes, and exact selector
semantics. No new QTE/repair/publication system or broader lifecycle expansion.
