# Feature Specification: Predvechnye Guardian special-art combat niches (#894)

**Feature Branch**: `codex/894-predvechnye-combat-effects`
**Created**: 2026-06-08
**Status**: Draft for autonomous implementation
**Source Issue**: GitHub issue #894 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/894

## Source Issues & Scope

- **Source GitHub issue(s)**: #894 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/894
- **Dependencies already closed**: #898 (`combatConditions[]` layer), #897 (`specialArts[].combatEffect` field), #895 mechanically-actionable special-art umbrella.
- **Related non-closing follow-up**: #896 will finalize worked examples and broad regression/docs coverage after this dossier/content pass is stable.
- **Spec Kit justification**: This is Saref/afterlife GM-facing content for ten built-in Guardian dossiers and must align with the recently added `combatEffect` / `combatConditions` contract. AGENTS.md and the project constitution require Spec Kit for afterlife/Saref and GM-facing contract-sensitive work.
- **Contract posture**: This issue does not add a new runtime field. It applies the existing #897 `combatEffect` vocabulary to Guardian dossier authoring text and source guards so the GM can later author matching profile data/examples.
- **Out of scope**: final broad worked examples and regression coverage (#896), changing the #897 runtime validation contract, rewriting Saref questlines, changing Guardian manifests, or adding deterministic mini-engine behavior.

## User Scenarios & Testing

### User Story 1 — Each Predvechnye special art has ordinary combat value (Priority: P1)

As a player/GM reading a Predvechnye Guardian dossier, I need each teachable special spiritual art to explain its ordinary afterlife-combat niche beyond the base operation so the art is worth upgrading even outside the Saref-specific storyline.

**Independent Test**: A source-guard test scans all ten built-in Predvechnye Guardian `dossier.md` files and proves each listed art contains an explicit `Боевой эффект:` clause near the existing `Особое духовное искусство` paragraph.

**Acceptance Scenarios**:

1. **Given** the Azalia, Brann, Elyara, Ilarion, Lissara, Lucian, Myriel, Seret, Varak, and Veyra dossiers, **When** the source guard runs, **Then** every required art has an explicit combat-effect clause.
2. **Given** two different Guardians with the same base operation, **When** their clauses are compared, **Then** their trigger/payoff/limit wording is distinct and not a copied generic bonus.

---

### User Story 2 — Combat niches preserve Saref/story utility without spoilers (Priority: P1)

As the GM, I need the new ordinary-combat clause to preserve the existing Saref/story application instead of replacing it, while default player-facing wording avoids premature Wings/Saref secret disclosure.

**Independent Test**: The source guard asserts the original art names and existing `Художественный эффект`/GM note wording remain present, while the new `Боевой эффект:` text is written as ordinary afterlife-combat guidance rather than raw spoiler or debug vocabulary.

**Acceptance Scenarios**:

1. **Given** an existing dossier paragraph, **When** it is updated, **Then** the previous narrative/Saref-safe art identity remains readable.
2. **Given** ordinary player-facing dossier text, **When** it mentions the new combat niche, **Then** it does not reveal unrevealed Saref/Wings secrets or raw contract/debug fields.

---

### User Story 3 — GM can translate dossier text into #897 `combatEffect` / #898 condition axes (Priority: P1)

As a future GM/agent authoring afterlife entity profiles or examples, I need each dossier clause to name the niche, trigger/target, legal axis/payoff, and finite limit/counterplay so it can be transformed into structured `specialArts[].combatEffect` without inventing a one-off rule.

**Independent Test**: A source guard checks that every combat-effect clause contains the required authoring concepts: combat niche, trigger/target, legal payoff/axis vocabulary, and finite limit or counterplay.

**Acceptance Scenarios**:

1. **Given** a dossier clause, **When** the GM later converts it to `specialArts[].combatEffect`, **Then** the clause provides enough information for `summary`, `trigger`, `mechanicalAxis`, `allowedPayoff`, `limit`, and `auditRequirement`.
2. **Given** a proposed clause, **When** it becomes a passive unlimited bonus, Mortal HP/status effect, or bypass of `baseOperation`, **Then** the source guard or review rejects it.

## Requirements

### Functional Requirements

- **FR-001**: Update all ten listed built-in Predvechnye Guardian dossiers with an explicit `Боевой эффект:` clause near the existing `Особое духовное искусство` paragraph.
- **FR-002**: Each clause MUST preserve the existing art name, base operation, artistic/story effect, and GM note; it may add text but must not remove or dilute the Saref/story utility.
- **FR-003**: Each clause MUST state an ordinary afterlife-combat niche that matters outside the final Saref storyline.
- **FR-004**: Each clause MUST include trigger/target, legal payoff/axis, and limit/counterplay in player-safe Russian wording.
- **FR-005**: Effects MUST be unique per Guardian and mechanically distinct from both the base operation and each other.
- **FR-006**: Effects MUST remain compatible with #897/#898 legal surfaces: `rollMode`, `conflictPosition`, `controlState`, `sideStrain`, `tempoAdvantage`, `counterPayoff`, `actionEconomy`, `actionCostAudit`, or `combatCondition(s)`.
- **FR-007**: Text MUST avoid generic passive `+X` bonuses, unlimited stacking, Mortal HP/status vocabulary, raw JSON/DTO/debug wording, and premature unrevealed Saref/Wings spoilers.
- **FR-008**: Update `OtherGuides/System_Guardian_Dossier_Standard.md` so future built-in Guardian dossiers require ordinary combat-effect clauses for special spiritual arts.
- **FR-009**: Update `OtherGuides/Saref_Guardian_Questlines/*.md` only if a dossier addition makes the reward wording inconsistent; otherwise leave questline docs unchanged and record that no change was needed.
- **FR-010**: Add or update tests/source guards so future edits cannot remove the explicit combat-effect clause or collapse it into generic flavor.
- **FR-011**: Keep #896 out of this issue's lifecycle. Any examples or broad coverage work beyond the guard required for #894 remains a separate follow-up.

### Required Guardian Arts

- Azalia / `azalia`: `Пламя Избранной Клятвы`, base `binding`.
- Brann / `brann`: `Клеймо Честной Трещины`, base `pressure`.
- Elyara / `elyara`: `Милость Незаживающей Раны`, base `guard`.
- Ilarion / `ilarion`: `Якорь Невытравленного Имени`, base `guard`.
- Lissara / `lissara`: `След, Которого Не Было`, base `maneuver`.
- Lucian / `lucian`: `Лунный Разрез Клятвы`, base `break_binding`.
- Myriel / `myriel`: `Пепельная Формула Чужого Мира`, base `pressure`.
- Seret / `seret`: `Разомкнутый Договор`, base `break_binding`.
- Varak / `varak`: `Трещина в Строю`, base `pressure`.
- Veyra / `veyra`: `Маска Среди Крыльев`, base `maneuver`.

## Success Criteria

- **SC-001**: Focused source-guard tests fail before the dossier clauses are added and pass after all ten clauses are present.
- **SC-002**: Focused `SystemGuardianLibraryServiceTests` / dossier tests pass with non-zero counts.
- **SC-003**: `AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests` pass with non-zero counts or are explicitly unchanged but run as the afterlife docs gate.
- **SC-004**: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`, `git diff --check origin/main...HEAD`, and the added-line static scan excluding `specs/**` pass before PR/merge.
- **SC-005**: Independent review confirms #894 is satisfied without prematurely closing #896.

## Assumptions

- The C# client remains the source of runtime and validation authority; this issue is primarily GM-facing content plus source guards.
- #897's `specialArts[].combatEffect` contract is already merged into `main` and should be referenced rather than reshaped.
- Dossier text is player/GM prompt material and must stay in Russian except for established contract field names.
- GitHub Actions are not required for this repository's normal local-gated workflow unless Stanislav explicitly asks.
