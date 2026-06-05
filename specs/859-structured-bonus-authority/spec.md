# Feature Specification: Structured Authority for Mechanical Inventory Bonus Summaries

**Feature Branch**: `fix/859-structured-bonus-authority`
**Created**: 2026-06-05
**Status**: Implemented for review
**Source Issue**: [#859 [Validation][Inventory] Mechanical item bonus summaries must have structured authority](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/859)
**Parent Audit**: [#857 [Audit][Validation] Enforce player-facing summary/detail authority links](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/857)

## User Stories & Testing

### User Story 1 - Detect mechanical-looking bonus summaries with no authority (Priority: P1)

A player or GM who sees an inventory item summary such as `Скрытность +1`, `Восстанавливает 15% здоровья`, or `Репутация среди аристократов +3` can trust that the state contains canonical structured authority explaining whether the effect is applied, inspectable, narrative-only, or unresolved.

**Independent Test**: Validate minimal inventory fixtures with mechanical-looking `bonuses`/`effects` strings and empty `structuredBonuses`/`combatEffect`/consumable authority. Verify validation flags stat, skill, reputation, healing, damage, duration, condition, or activated-action summaries that lack authority.

**Acceptance Scenarios**:

1. **Given** an inventory item has `bonuses = ["Скрытность +1"]` and no structured authority, **When** validation runs, **Then** validation reports a mechanical bonus authority issue.
2. **Given** an inventory item has `bonuses = ["Репутация среди аристократов +3"]` and no structured authority, **When** validation runs, **Then** validation reports a mechanical reputation authority issue.
3. **Given** a consumable has `bonuses`/`effects` text such as `Восстанавливает 15% здоровья` and no `combatEffect`/consumable effect authority, **When** validation runs, **Then** validation reports the missing canonical authority.
4. **Given** a flavor/lore-only item has an explicit narrative-only classification, **When** validation runs, **Then** validation accepts the text as non-mechanical.

---

### User Story 2 - Keep player-facing detail rendering aligned with applied mechanics (Priority: P1)

A player inspecting inventory details should not see mechanics that `CharacteristicsService`, combat effects, consumable handling, or another canonical state surface cannot apply or explain.

**Independent Test**: Seed items with structured bonuses and narrative-only text. Confirm existing inventory detail rendering still shows player-facing summaries while validation enforces that mechanical-looking text has a structured counterpart or explicit unresolved/narrative classification.

---

## Requirements

### Functional Requirements

- **FR-001**: Validation MUST detect inventory `bonuses` and comparable item `effects`/summary text that looks mechanical because it includes numeric modifiers, percentages, healing/damage wording, stat/skill/reputation changes, duration, condition, or activated action semantics.
- **FR-002**: Mechanical-looking summary text MUST resolve to matching canonical authority for that displayed summary: exact display/description/summary text or clear target/value metadata in `structuredBonuses`, `combatEffect`, canonical consumable effect data, or an existing project-approved structured effect field. A non-empty but unrelated/empty authority array is not sufficient.
- **FR-003**: Pure flavor/lore strings MUST remain valid only when the item is explicitly classified as narrative-only or flavor-only in a player/GM-authorable field documented by this change.
- **FR-004**: Unknown, unidentified, sealed, narrative-only, or unresolved mechanics MUST be allowed only when the item includes explicit player-facing classification/reason; the UI must not imply that unresolved or narrative-only text is applied mechanics.
- **FR-005**: Validation issue messages MUST identify the item and the unresolved mechanical summary without exposing raw debug/API phrasing to player-facing command output.
- **FR-006**: `CharacteristicsService` alignment MUST be preserved: what it treats as applied equipment/passive bonuses should remain driven by structured authority, not by parsing free-text summaries.
- **FR-007**: Add focused tests for stat bonus, skill bonus, reputation bonus, healing consumable, structured-authority success, unresolved-reason success, and narrative-only success.

### Contract / Documentation Scope

- This issue changes a Mortal World inventory validation and GM-authored item contract: mechanical-looking `bonuses`/`effects` summaries must not be the sole source of mechanical truth.
- Update GM-facing rules/prompts/examples/manifests or source-guard tests where Mortal inventory item bonuses/effects are documented, especially `Rules/Block_10.txt` and relevant examples if they already cover inventory bonus authoring.
- No afterlife pending/control contract change is expected.

## Out of Scope

- Full #857 summary/detail authority audit.
- Readable document authority already covered by #858.
- Quest reward cross-reference authority (#860).
- Rewriting `CharacteristicsService` to parse natural-language bonus text.
- Browser visual redesign or new browser-specific mechanics.

## Success Criteria

- Validation fails on the issue examples when mechanical `bonuses`/`effects` text lacks structured authority.
- Validation accepts equivalent items when they provide matching structured authority, explicit narrative-only classification, or explicit unresolved-player-facing reason.
- Regression tests demonstrate the old acceptance gap before the fix and pass after.
- GM-facing rules/examples describe how to author mechanical summaries and narrative-only/unresolved exceptions.
- Focused C# tests run with real discovery (`-p:IsTestProject=true` on this Windows/.NET host) and pass.
- `git diff --check origin/main...HEAD` passes.
