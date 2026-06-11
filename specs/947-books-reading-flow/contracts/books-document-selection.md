# Contract: /books document shelf and selected reading view

**Source issue**: #947 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/947

## Purpose

Define the read-only player-facing contract expected from `/книги` / `/books` after #947. This contract is a client presentation/detail-flow contract over existing Mortal World readable-document authority. It does not add document mutation, local-turn writes, or afterlife pending/control surfaces.

## Authority Inputs

The implementation must continue to resolve documents from existing C# authority:

- Inventory items in `game_state/inventory/items.json` that look like books, letters, scrolls, notes, documents, inscriptions, diaries, journals, or their Russian equivalents.
- Embedded inventory item `textContent`.
- Sidecar text entries from `game_state/inventory/item_text_updates.json`.
- Item journal entries from `game_state/npcs/item_journals.json`.
- Unreadable reason fields such as `unreadableReason`, `sealedReason`, `lockedReason`, `unknownReason`, `readingBlockedReason`, `readBlockedReason`, `cannotReadReason`, `inaccessibleReason`, or `unavailableReason`.

Stable identities (`existedId`, `itemId`, `id`) take precedence over name fallback. Name fallback is allowed only where existing readable-document authority already relies on it.

## Shelf Item Shape

A shelf/list item should expose these concepts to console and browser renderers:

- Stable selection identity safe for command/result actions.
- Player-facing title/name.
- Source/context hint, such as item id/name or standalone sidecar record, when useful and not raw-debuggy.
- Access state: readable, unreadable/sealed/locked/unavailable, or standalone entry.
- Preview or count/status hint, not the full body for long documents.
- Unreadable reason when access is blocked.

Default player-facing output must avoid raw `game_state/` paths, file names, DTO/API/debug terminology, raw JSON, or Spec Kit/acceptance wording.

## Detail View Shape

A selected document detail view should expose:

- Title/name and access status.
- Only the selected document's text entries/paragraphs/pages, or only its unavailable reason.
- Clear separation for multiple entries/paragraphs/pages.
- Back/return affordance in console; browser action metadata or an equivalent typed C# command-result detail path.

The detail view must not include full bodies for other documents in the same shelf.

## Browser Boundary

Browser/React code must remain presentation-only. C# command/result authority must produce the shelf/detail data or action metadata. If full browser interaction is deferred, the implementation must create/link a follow-up issue and still ensure `/books` no longer forces all long text into one table cell.

## Validation Boundary

#947 should not weaken `ReadableInventoryDocumentAuthority` validation. Document-like items with no readable detail authority and no unreadable reason must remain validation issues. The shelf/detail flow may present explicit unreadable reasons, but must not hide malformed authority.

## Documentation Boundary

If the branch changes supported GM-authored document fields or the documented behavior of `/книги` / `/books`, update the relevant GM/player-facing docs, examples, manifests, or source guards in the same branch. If the branch only changes client-owned presentation over already supported fields, record that no GM prompt update was required.
