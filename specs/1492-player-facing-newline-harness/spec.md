# Feature Specification: Player-facing Newline Harness

**Source Issue**: [#1492](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1492)

## Problem

A live Chaos Sea turn produced literal PowerShell line-break tokens such as `` `n`n `` inside `output/narrative_response.json.response`. The turn passed validation and the console showed the tokens to the player.

## Requirements

- **FR-001**: Player-facing narrative and dialogue values MUST convert literal PowerShell newline tokens `` `n ``, `` `r ``, and `` `r`n `` to real line breaks.
- **FR-002**: The GM JSON write helper MUST normalize known player-facing output fields before serialization.
- **FR-003**: Accepted-output ingestion/validation MUST normalize the same fields when a GM bypasses the helper.
- **FR-004**: Runtime response construction MUST remain a final safety net.
- **FR-005**: Existing real line breaks, backslash-escaped line breaks, ordinary backticks, and unknown text MUST remain safe.
- **FR-006**: Mortal World and afterlife use the same realm-agnostic behavior.
- **FR-007**: GM-facing compact guidance and examples MUST tell the GM to author real paragraph breaks rather than PowerShell escape tokens.

## Success Criteria

- **SC-001**: RED/GREEN tests cover narrative, dialogue text/input, helper writes, accepted-output fallback, and preservation cases.
- **SC-002**: The saved Chaos Sea conflict opening renders with real paragraphs and no literal PowerShell newline tokens.
- **SC-003**: Existing output, documentation, and source-guard tests pass.

## Out of Scope

- General prose rewriting.
- Converting arbitrary PowerShell escapes other than line breaks.
- Changing afterlife state, spiritual-combat mechanics, or GM authority.
