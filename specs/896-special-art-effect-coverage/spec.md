# Feature Specification: Special-art combat-effect examples and regression coverage (#896)

**Feature Branch**: `codex/896-special-art-coverage`  
**Created**: 2026-06-08  
**Status**: Draft for autonomous implementation  
**Source Issue**: GitHub issue #896 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/896

## Source Issues & Scope

- **Source GitHub issue(s)**: #896 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/896
- **Dependencies already closed on `main`**: #898 shared `combatConditions[]`, #897 structured `specialArts[].combatEffect`, #895 mechanically actionable effect authority, and #894 Predvechnye Guardian dossier combat-effect clauses.
- **Spec Kit justification**: The work updates afterlife/Saref GM-facing examples and documentation/source guards across multiple files. The project constitution and `AGENTS.md` require Spec Kit for afterlife, Saref, GM-prompt, docs/example, and multi-session handoff changes.
- **Contract posture**: This feature should not add or reshape runtime state fields. It proves the existing #897/#898/#894 contract through worked examples and regression/docs coverage.
- **Out of scope**: changing the `specialArts[].combatEffect` schema, changing `combatConditions[]` validation, reworking Guardian dossier combat-effect prose from #894, adding deterministic mini-engine rules, or closing Browser/UX epics.

## User Scenarios & Testing

### User Story 1 — Player-owned art changes an afterlife conflict result (Priority: P1)

As a player and GM, I need a worked afterlife spiritual-conflict example where the player uses a learned Predvechnye Guardian special art and its unique `combatEffect` changes the tactical or narrative result beyond the base operation.

**Independent Test**: Documentation/example tests scan `Examples/E_CLI_Afterlife_Turns.txt` and prove the player-owned special-art example includes `specialArts[].combatEffect`, a concrete `specialArtAudit.effectNote`, and a result delta tied to a #897/#898 legal axis.

**Acceptance Scenarios**:

1. **Given** a player-owned learned special art from the Saref/Predvechnye set, **When** it is used in an afterlife spiritual conflict, **Then** the example shows the base operation plus a distinct effect payoff.
2. **Given** the example result, **When** the GM/audit text explains why the result changed, **Then** `specialArtAudit.effectNote` references the unique combat effect rather than generic flavor.

---

### User Story 2 — Opposition or Guardian art demonstrates effect-note authority (Priority: P1)

As the GM, I need a worked example where a non-player Guardian/opposition special art is used and its effect note clearly explains the unique combat effect so future opposition authoring cannot collapse special arts into ordinary base operations.

**Independent Test**: Documentation/source-guard coverage proves at least one non-player Guardian/opposition art example includes structured `combatEffect` data and a player-safe `specialArtAudit.effectNote` that names the special-art-specific tactical effect.

**Acceptance Scenarios**:

1. **Given** a Guardian/opposition special art in an afterlife conflict, **When** the art affects the conflict, **Then** the example explains the effect in `specialArtAudit.effectNote`.
2. **Given** the effect note, **When** it is rendered or audited, **Then** it remains player-safe and does not leak raw JSON/debug framing or premature Saref/Wings spoilers.

---

### User Story 3 — Coverage requires at least two distinct Saref-set arts (Priority: P1)

As a future maintainer, I need tests and docs to prove at least two Predvechnye Guardian arts from different tactical niches/base operations remain represented in examples.

**Independent Test**: Coverage tests fail if the worked examples stop mentioning at least two Guardian arts from the #894 Saref/Predvechnye set, or if the examples omit combat-actionable effect wording.

**Acceptance Scenarios**:

1. **Given** the afterlife examples, **When** coverage tests run, **Then** they find at least two of the updated Guardian art names from #894.
2. **Given** future edits to examples/docs, **When** an editor removes combat-effect requirements or turns examples into generic story flavor only, **Then** coverage tests fail.

## Functional Requirements

- **FR-001**: Update `Examples/E_CLI_Afterlife_Turns.txt` with at least one worked example where a player-owned learned Predvechnye Guardian special art is used in an afterlife spiritual conflict and its unique combat effect changes the tactical/narrative result beyond the base operation.
- **FR-002**: Update the examples with at least one worked example where a non-player Guardian/opposition special art is used and `specialArtAudit.effectNote` clearly references the unique combat effect.
- **FR-003**: The examples MUST include at least two Guardian arts from the #894 Saref/Predvechnye set, preferably with different base operations.
- **FR-004**: Example profile/conflict snippets MUST use the existing #897 structured `specialArts[].combatEffect` field where applicable, including combat-actionable `summary`, `trigger`, `mechanicalAxis`, `allowedPayoff`, `limit`, and audit guidance/effect-note semantics.
- **FR-005**: Example conflict/result snippets MUST use #898 legal effect axes/payoffs such as `rollMode`, `conflictPosition`, `controlState`, `sideStrain`, `tempoAdvantage`, `counterPayoff`, `actionEconomy`/`actionCostAudit`, or `combatCondition`, without inventing a new mini-engine.
- **FR-006**: Update GM-facing documentation that explains special-art examples and `specialArtAudit.effectNote` requirements, including `OtherGuides/Afterlife_Combat_Terminology_Glossary.md` and/or `OtherGuides/Afterlife_Contract_Matrix.md` as the existing organization requires.
- **FR-007**: Update `Examples/example_validation_manifest.json` if the example validator requires a manifest entry for new/changed example sections.
- **FR-008**: Update `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` and/or `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs` so future edits cannot reduce special-art examples to generic flavor or remove `specialArtAudit.effectNote` coverage.
- **FR-009**: Coverage MUST mention the new combat-effect requirement explicitly and assert at least two #894 Guardian art names or examples remain present.
- **FR-010**: Player-facing example text MUST avoid raw DTO/debug framing, generic passive `+X` stacking, Mortal HP/status vocabulary, and premature Saref/Wings spoiler disclosure.

## Required Guardian Art Coverage

At least two of these #894 arts must appear in examples/coverage; choose different base operations when practical:

- Azalia — `Пламя Избранной Клятвы` (`binding`).
- Brann — `Клеймо Честной Трещины` (`pressure`).
- Elyara — `Милость Незаживающей Раны` (`guard`).
- Ilarion — `Якорь Невытравленного Имени` (`guard`).
- Lissara — `След, Которого Не Было` (`maneuver`).
- Lucian — `Лунный Разрез Клятвы` (`break_binding`).
- Myriel — `Пепельная Формула Чужого Мира` (`pressure`).
- Seret — `Разомкнутый Договор` (`break_binding`).
- Varak — `Трещина в Строю` (`pressure`).
- Veyra — `Маска Среди Крыльев` (`maneuver`).

## Success Criteria

- **SC-001**: A focused RED test/source guard fails before example/docs updates because the new #896 coverage requirement is missing.
- **SC-002**: The same focused coverage passes after the worked examples/docs are updated.
- **SC-003**: `ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|SystemGuardianLibraryServiceTests` pass with non-zero counts.
- **SC-004**: `dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --verbosity minimal`, `git diff --check origin/main...HEAD`, and the added-line static scan excluding `specs/**` pass before PR/merge.
- **SC-005**: Independent review confirms the issue is satisfied and the PR closes #896 only.

## Assumptions

- `specialArts[].combatEffect` and `combatConditions[]` are already merged authority; this feature demonstrates them rather than changing them.
- `Examples/E_CLI_Afterlife_Turns.txt` is the preferred worked-example surface for afterlife turn and conflict examples unless the current repo structure shows a more specific existing section.
- The C# client and validation tests remain authoritative for example parsing and documentation coverage.
- GitHub Actions are not required for normal closure; local gates are the merge/closure authority unless Stanislav explicitly requests CI.
