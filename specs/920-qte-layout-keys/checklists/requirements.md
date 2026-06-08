# Requirements Checklist: QTE Layout-Independent Key Input

Feature path: `specs/920-qte-layout-keys/`
Source issue: #920

## Spec quality

- [x] Source GitHub issue #920 is linked in spec, plan, and tasks.
- [x] Parent/related QTE epics are named but not included as closure targets.
- [x] Spec Kit justification is explicit and matches AGENTS/constitution policy.
- [x] Contract scope lists player-facing, GM-facing, validation/docs/examples, console, browser, and frontend surfaces.
- [x] Out-of-scope boundaries exclude QTE v2 mini-game implementation and Daren training mode.
- [x] User stories are independently testable and prioritized.
- [x] Acceptance criteria from the issue are mapped to functional requirements and tasks.
- [x] Verification commands are listed for C#, docs/contracts, frontend, and diff hygiene.

## Implementation readiness

- [x] Console fallback mapping coverage is required before implementation.
- [x] Browser physical-code preference and character fallback coverage are required before implementation.
- [x] Docs/examples updates are required before closure.
- [x] Ordinary text input is explicitly protected from QTE normalization.
- [x] Existing QTE v1 compatibility is explicitly protected.
