# Research: Browser Shining Abode Actions

**Source Issue**: [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)

## Decision 1: Reuse Shining core action request authority

**Decision**: Browser submissions will call `ShiningCoreActionRequestState.ValidateRequestAgainstCurrentStateAsync` and `ShiningCoreActionRequestState.WriteRequestAsync`.

**Rationale**: The console flows for native faction discovery, faction investment, project support/unsupport, and project retirement already write the same pending control file and rely on this service for costs, eligibility, pending conflicts, and runtime contract shape.

**Alternatives considered**:

- React-side gameplay handlers: rejected because C# remains authoritative and the browser must not invent mechanics.
- New browser-specific pending request shape: rejected because #811 only asks for browser parity and AGENTS requires afterlife contract docs/examples if shape changes.

## Decision 2: Add explicit browser command forms

**Decision**: Add five explicit Shining Abode mutating browser commands for the five #811 actions.

**Rationale**: Existing browser command catalog/menu/coverage patterns treat mutating guided forms as first-class commands. Explicit commands let help, menu metadata, local UI locks, direct open tests, and coverage service identify support clearly.

**Alternatives considered**:

- Fold all actions into `/shining_abode`: rejected because it would hide coverage and make direct stale-submit guard coverage harder to target.
- Reuse #810 politics commands: rejected because these actions write the Shining core action request contract, not the political request contract.

## Decision 3: Prompt builders filter from canonical C# state

**Decision**: Browser prompt builders will enumerate visible factions/projects from existing C# state helpers and apply local eligibility filters matching the console/service semantics before displaying options.

**Rationale**: The player should see only actionable visible options, while submit-time service validation remains the final authority for stale state.

**Alternatives considered**:

- Display all raw projects/factions and let submit validation fail: rejected because default browser UI must be player-facing and should not expose hidden/ineligible raw state.

## Decision 4: Preserve afterlife runtime contract shape

**Decision**: This slice must keep `game_state/control/pending_shining_abode_actions.json` request fields unchanged.

**Rationale**: Issue #811 requests browser parity for existing console flows. Changing pending/control shape would expand scope into GM-facing contract documentation and examples.

**Fallback**: If implementation discovers a required contract shape change, update `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `Examples/example_validation_manifest.json`, and relevant documentation coverage tests in the same change before completion.

## Decision 5: Frontend changes are probably unnecessary

**Decision**: Prefer C# command metadata and prompt/write service changes only.

**Rationale**: Recent browser parity work uses backend command descriptors, prompt sessions, and menu metadata consumed by the existing frontend. If the five commands can be expressed through the current prompt schema, React/TypeScript changes add unnecessary risk.

**Verification**: Run frontend verification only if frontend files are touched or if implementation uncovers a metadata shape not supported by the current frontend.
