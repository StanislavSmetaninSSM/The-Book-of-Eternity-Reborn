# Contract: Mortal Reference Browser Detail Actions

Source issue: #1057 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1057

## Scope

This contract defines the expected player-facing command-result behavior for #1057. It is a client/browser/console parity contract over existing read-only Mortal World command state. It is not a runtime state schema contract and does not authorize GM prompt/example, validation, normalizer, pending/control, or afterlife changes.

## Covered command families

The implementation must evaluate the following reference-style read-only commands:

| Command | Russian alias | Expected #1057 behavior |
| --- | --- | --- |
| Quests | `/квесты` | Browser result can inspect one quest/reward/history reference without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Skills | `/навыки` | Browser result can inspect one skill entry/detail reference without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Factions | `/фракции` | Browser result can inspect one faction/reputation entry without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Locations | `/локации` | Browser result can inspect one location/detail reference without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Rival threads | `/чужие_нити` | Browser result can inspect one rival-thread entry without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Guardian corrections | `/коррективы_хранителя` | Browser result can inspect one correction entry without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Storage access | `/доступ_к_хранилищам` | Browser result can inspect one storage access entry without raw JSON as the only detail path, or an exact follow-up is recorded. |
| Transport | `/транспорт` | Browser result can inspect one transport entry without raw JSON as the only detail path, preserving #948 transport player-facing labels. |

## Invariants

1. Existing overview outputs remain available.
2. Detail affordances are read-only and must not mutate game state or start write/prompt sessions.
3. Browser detail actions must be backed by shared C# command/catalog/result authority, not duplicated React gameplay logic.
4. Default player-facing output uses Russian/in-world labels and avoids `DTO`, `API`, `endpoint`, raw JSON, local paths, debug framing, and raw slash-command leakage.
5. Safe `ExplorerCommandResult` blocks/actions returned by read-only commands must remain visible in the default browser result surface; they must not collapse to a generic completion state.
6. Existing advanced/raw diagnostics may remain as diagnostics, but cannot be the only way to inspect covered representative entities.
7. Dynamic state text must use existing escaping/sanitization patterns before Spectre.Console markup or browser-rendered HTML.
8. If a command cannot be safely completed in #1057, the PR must update the audit artifact with the exact deferred command, reason, and linked follow-up issue.

## Verification obligations

- Focused tests/source guards must cover representative browser detail action metadata and selected detail rendering.
- Source guards must preserve console/browser parity expectations for covered command aliases/catalog entries.
- Overview-preservation coverage must remain green.
- `docs/audits/mortal-readonly-drilldown-audit.md` must reflect which command families are implemented or explicitly deferred after #1057.
- No GM-facing docs/examples are required unless implementation changes state schema, validation, prompts, examples, or command contract semantics beyond read-only presentation.
