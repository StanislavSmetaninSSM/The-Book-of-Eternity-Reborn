# Feature Specification: Mortal Read-Only Detail Drill-Down Audit

**Source GitHub issue:** #948 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/948
**Created:** 2026-06-15
**Status:** Draft for implementation

## User Need

Players can inspect mortal-world read-only command overviews without losing access to rich details. When an overview mentions effects, combat participants/logs, world news, transport entities, interaction records, or another structured mortal entity, the player should either have a natural detail path or see a tracked follow-up explaining why the gap is larger than this audit slice.

## Scope

This feature is an audit-and-closure slice for #948. It MUST:

- Audit mortal read-only commands in `ExplorerMortalWorldCommandResultBuilder` and the matching console `ExplorerMode` command paths.
- Compare browser command-result rendering with console output for the audited commands.
- Preserve all existing overview outputs unless a small focused improvement is necessary to make a detail path discoverable.
- Record confirmed gaps with severity and recommended scope.
- For each confirmed gap, either implement one small focused improvement under #948 or create/link a dedicated GitHub follow-up issue when the gap is larger than one focused fix.
- Add or adjust tests/source guards so future rich mortal displays do not regress into raw-only or all-in-one-only output.

## Out of Scope

- Rewriting every mortal read-only command in one PR.
- Afterlife / Chaos Sea / Shining Abode detail drill-down work; that is #949.
- NPC section drill-down; already tracked by #946.
- Books/document reading flow; already tracked by #947.
- Browser visual redesign unrelated to command detail discoverability.
- Runtime contract or GM-authored state schema changes unless a small fix truly requires them; if that happens, update GM-facing docs/examples in the same change.

## Acceptance Criteria

1. The audit covers every mortal read-only command handled by `ExplorerMortalWorldCommandResultBuilder` and the matching console command registration/handler surface.
2. The repository contains a concise audit artifact listing command, current console/browser behavior, gap status, severity, recommended action, and linked follow-up issue or in-PR fix.
3. Existing overview outputs are preserved.
4. Each confirmed gap has either a focused fix in this branch or a linked follow-up GitHub issue.
5. Browser and console parity gaps are explicitly recorded.
6. Automated coverage prevents audited rich mortal command surfaces from regressing into raw-only or all-in-one-only output where the branch can reasonably guard that behavior.
7. Verification evidence includes focused .NET tests and `git diff --check`.

## Contract and Documentation Impact

Expected impact is player-facing documentation/audit only, not a GM-authored runtime contract change. If implementation changes command behavior, response fields, state schema, validation, prompts, or GM-facing examples, update the relevant GM-facing docs/examples/tests before closure.

## Follow-Up Policy

Follow-up issues created from this audit must include:

- Source reference to #948.
- Affected command(s).
- Console/browser parity expectation.
- Player-facing acceptance criteria.
- Whether Spec Kit is expected for that follow-up.
