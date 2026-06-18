# Browser Inventory and Document Detail Contract

## Scope

This contract describes the expected player-facing Browser Client command-result surfaces for issue #1089. It is a presentation and command-result contract over existing C# authority; it does not add a new game-state JSON schema.

## Inventory Summary

When the Browser Client executes `/инв` or `/inventory` in Mortal World state, the result should include:

- Existing inventory summary data such as money, equipment, and item rows.
- For visible items with detail authority, a player-facing action/command that opens that item's detail view.
- Russian labels and descriptions suitable for default player mode.
- No raw local paths, raw JSON, API/DTO/protocol/debug wording, or acceptance/spec framing.

## Inventory Item Detail

A selected item detail result should include available player-facing sections:

- Title/name and item category/status.
- Description or explicit unavailable reason.
- Equipped/location/quantity context when available.
- Ordinary display bonuses/effects.
- Structured bonus details when authority is present: target, value, value type, and readable category.
- Combat effect information when present.
- Special properties/custom properties when they can be rendered safely.
- A player-facing back/return action when the command-result pattern supports it.

If an item is hidden, missing, or lacks visible detail authority, the result should show a short Russian unavailable state rather than raw diagnostics.

## Document/Book Summary

When the Browser Client executes `/книги`, `/books`, or an equivalent readable-document command, the result should include:

- A shelf/list of readable or explicitly unreadable documents/books.
- Stable selectors derived from existing authority where possible.
- Title/name, short preview/status, source/context hint, and player-facing read/open action.
- Unreadable/sealed reasons when a document-looking item cannot be read.

## Selected Document/Book Detail

A selected document/book detail result should include:

- The selected title/status.
- Only that document's text/pages/entries, rendered as readable player-facing blocks.
- Source/context only in player-facing form.
- Explicit unavailable/unreadable reason when applicable.
- Numeric stable ids must win before numeric shelf-index fallback when both could match.

## React Rendering Contract

React may render safe blocks and action buttons from `ExplorerCommandResult`, and may call existing shell command/prompt-session APIs. React must not compute item matching, stack rules, readable-document authority, or inventory mutation semantics.

## Advanced Diagnostics

Advanced/debug mode may preserve raw diagnostics where existing code already supports them. Default mode must hide them while preserving useful safe player-facing content.
