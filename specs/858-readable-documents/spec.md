# Feature Specification: Readable Inventory Document Authority

**Feature Branch**: `fix/858-readable-documents`
**Created**: 2026-06-05
**Status**: Draft for implementation
**Source Issue**: [#858 [Validation][Inventory] Document items must be readable via /книги or explicitly unreadable](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/858)
**Parent Audit**: [#857 [Audit][Validation] Enforce player-facing summary/detail authority links](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/857)

## User Stories & Testing

### User Story 1 - Inspect readable documents through `/книги` (Priority: P1)

A player who has a readable book, letter, scroll, note, document, inscription, or similar text-bearing item in inventory can open `/книги` / `/books` and see either readable text or a clear reason the item cannot currently be read.

**Independent Test**: Seed inventory with document-like items and run the Mortal World books command. Verify that an item with `textContent` renders text, an item resolved by `game_state/inventory/item_text_updates.json` renders text, an item resolved by `game_state/npcs/item_journals.json` renders journal text, and a sealed/unreadable item renders its player-facing reason.

**Acceptance Scenarios**:

1. **Given** an inventory document has non-empty `textContent`, **When** `/книги` renders, **Then** the document title and text are visible.
2. **Given** an inventory document has no inline text but has a matching `item_text_updates` entry by stable id, **When** `/книги` renders, **Then** that sidecar text is visible.
3. **Given** an inventory document has no readable text but is explicitly marked unreadable/sealed/locked/unknown with a player-facing reason, **When** `/книги` renders, **Then** the item appears with the reason instead of disappearing.
4. **Given** a document-like inventory item has no text source and no unreadable reason, **When** validation runs, **Then** validation reports a specific issue instead of accepting the orphan summary.

---

### User Story 2 - Preserve summary/detail authority invariant (Priority: P1)

GM-authored state that exposes a readable-looking inventory item must include detail authority or an explicit unresolved state so the player never sees a document in inventory that `/книги` cannot explain.

**Independent Test**: Validate minimal fixtures for a readable document with text, a missing-text document, a sealed/unreadable document with reason, and a sidecar text update resolving an inventory document.

---

## Requirements

### Functional Requirements

- **FR-001**: Validation MUST identify inventory items that look text-bearing by type, group, name, or metadata (books, letters, scrolls, notes, documents, readable inscriptions, and Russian equivalents such as `Документ`, `Письмо`, `Книга`, `Свиток`, `Записка`) and require readable detail authority or an explicit unreadable reason.
- **FR-002**: Readable detail authority MAY be inline `textContent`, a matching `game_state/inventory/item_text_updates.json` entry, or a matching `game_state/npcs/item_journals.json` entry.
- **FR-003**: Matching MUST prefer stable identity fields when present (`existedId`, item id, or equivalent stable item identifier) and use item name fallback only where existing contracts already rely on name fallback.
- **FR-004**: Explicit unreadable/sealed/locked/unknown state MUST include player-facing reason text that `/книги` can show.
- **FR-005**: `/книги` / `/books` MUST include unreadable/sealed placeholder rows for document-like inventory items with explicit reason and MUST NOT silently behave as if no readable item exists.
- **FR-006**: Default player-facing output MUST avoid raw file paths, API/DTO/debug wording, and developer acceptance-criteria framing.
- **FR-007**: Add focused regression coverage for readable document with text, missing document text, sealed/unreadable document with reason, sidecar text update resolution, and sidecar journal resolution when supported by the existing data shape.

### Contract / Documentation Scope

- This issue changes a Mortal World validation and GM-authored inventory document contract: if a GM creates a readable-looking item, they must also provide readable text, a sidecar text/journal entry, or an explicit unreadable reason.
- Update GM-facing prompts/docs/examples/manifests or documentation coverage tests where the repository already documents Mortal World inventory/readable-item state. If no suitable GM-facing document exists, create a tracked follow-up issue before completion and record it in PR/issue notes.
- No afterlife pending/control contract change is expected.

## Out of Scope

- Full #857 summary/detail authority audit.
- Mechanical item bonus authority (#859).
- Quest reward cross-reference authority (#860).
- Browser redesign or new React gameplay logic beyond existing `/books` read-only parity if tests reveal the shared command result already covers browser output.

## Success Criteria

- `/книги` no longer hides a document-like inventory item merely because inline `textContent` is absent when an explicit unreadable reason exists.
- Validation rejects or warns on document-like inventory items that have no readable detail authority and no explicit unreadable reason.
- Regression tests demonstrate the old missing-detail behavior before the fix and pass after.
- Relevant GM-facing docs/examples or a tracked follow-up issue cover the new Mortal World readable-document authoring requirement.
- Focused C# tests run with real discovery (`-p:IsTestProject=true` on this Windows/.NET host) and pass.
- `git diff --check origin/main...HEAD` passes.
