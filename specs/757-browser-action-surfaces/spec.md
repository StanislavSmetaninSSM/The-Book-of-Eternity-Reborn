# Feature Specification: Browser Action Result Surfaces (#757)

**Source issue:** [#757 — [Browser Client UX] Open polished game windows and forms from selected actions](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/757)
**Parent epic:** [#680 — Browser Client](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)

## Scope

The Browser Client default player UI must render selected action/command results as useful player-facing game surfaces instead of generic or technical output. The immediate regression from #757 is that safe read-only result blocks can collapse to an empty/generic `Выполнено` surface when default sanitization drops them.

This feature is Browser Client presentation work. The C# client/application layer remains the gameplay authority; React may render existing `ExplorerCommandResult` blocks/actions/prompts and submit through existing browser command and prompt-session APIs, but must not invent gameplay rules or separate command semantics.

## User Stories

### Story 1 — Read-only action results show useful cards/panels

As a player selecting a read-only action such as “Где я”, I want the browser to show the safe location/result blocks the C# client returned, so I see useful in-world information instead of only a generic completion status.

**Acceptance criteria**

- Safe `ExplorerCommandResult.blocks` survive default player sanitization and render through `CommandResultView`/`BlockList`.
- Unsafe technical/raw blocks are sanitized, removed, or replaced with player-facing fallback copy in default mode.
- Default player UI does not expose raw slash commands, API/DTO/endpoint/protocol language, file paths, raw JSON, or implementation/debug details.

### Story 2 — Interactive actions use existing prompt/session flows

As a player selecting a mutating action that needs choices or confirmation, I want a form/confirmation surface that uses the existing C# prompt-session flow, so the browser behaves like the console without adding React-side gameplay logic.

**Acceptance criteria**

- Result prompts render whenever they are present, including read-only prompt summaries when there is no active `interactiveSession`.
- Live interactive prompt sessions continue to submit/cancel through `browserApi.submitPromptSession` and `browserApi.cancelPromptSession`.
- Action buttons continue to execute through the shared shell command flow, not duplicated React gameplay handlers.

### Story 3 — Advanced diagnostics stay opt-in

As a player, I should not see raw command/debug/API payload details during normal play, but I can opt into advanced mode for diagnostics.

**Acceptance criteria**

- Advanced mode may preserve rawer diagnostics where existing advanced UI already allows it.
- Default mode uses Russian, player-facing wording for result labels, empty states, loading/error copy, and fallback messages.
- Any technical details remain behind explicit advanced/details affordances.

## Requirements

- Preserve safe blocks for default action/command result surfaces.
- Render result blocks, notifications, actions, prompts, and prompt-session responses through shared player-facing components.
- Keep browser/frontend changes presentation-only; no C# gameplay/runtime contract, validation, normalizer, pending/control, or GM prompt changes are expected.
- Update focused frontend/source-guard tests and, if useful, a dependency-light visual smoke artifact under `TestResults/browser-smoke/` to demonstrate the player-facing result surface.
- If implementation discovers a real runtime/contract gap, stop and update this spec/plan/tasks before changing contracts.

## Out of Scope

- Implementing all interactive parity child issues (#805–#816) in this change.
- Adding new gameplay commands or changing command semantics.
- Changing afterlife/Mortal/Chaos Sea/Shining Abode/Saref runtime contracts.
- Reintroducing the old deleted Feature-branch card-heavy UI direction.

## Verification

- `npm run verify --prefix BookOfEternityClient.WebFrontend`
- Focused frontend tests for action result/player copy surfaces.
- Focused .NET Browser Client guard tests with real non-zero counts.
- `git diff --check origin/main...HEAD`
- Added-line static scan for secrets/shell/eval/pickle/SQL hazards.

## Implementation Record

2026-06-06 closure hardening keeps the scope presentation-only. The React
selected-action shell already used `sanitizeExplorerCommandResultForPlayer` and
`CommandResultView`/`BlockList` to preserve safe blocks. The remaining #757 risk
was the shared default player command-result sanitizer still defaulting
`preserveSafeBlocks` to `false`, which could reintroduce the reopened generic
result surface if a default action path used that helper without an explicit
override.

No C# gameplay/runtime contracts, validation/normalizer behavior, pending/control
files, GM prompts, examples, or afterlife/Mortal/Chaos Sea/Shining Abode/Saref
contracts changed.
