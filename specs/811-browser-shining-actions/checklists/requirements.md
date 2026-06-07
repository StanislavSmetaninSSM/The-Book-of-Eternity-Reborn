# Requirements Checklist: Browser Shining Abode Actions

**Source Issue**: [#811](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/811)
**Reviewed**: 2026-06-07

## Completeness

- [x] Source GitHub issue is linked.
- [x] User stories cover native faction discovery, faction investment, project support, project unsupport, and project retirement.
- [x] Direct command-open and stale prompt-submit guards are explicitly required.
- [x] Command/help/coverage metadata is explicitly required.
- [x] Sibling issues #812-#816 and umbrella #817 closure are out of scope.

## Contract Scope

- [x] Existing Shining core action pending contract is named.
- [x] No afterlife runtime contract change is planned.
- [x] Required documentation/example/test updates are specified if a contract change becomes necessary.
- [x] GM-facing prompt and response surfaces are not changed by this slice.

## Player-Facing UX

- [x] Default browser labels and blockers must be Russian/player-facing.
- [x] Raw `.json`, `pending_`, API/DTO/endpoint/debug wording is forbidden in default UI.
- [x] Forms must enumerate only visible eligible factions/projects.

## Verification

- [x] TDD RED/GREEN path is specified.
- [x] Focused browser parity tests are specified.
- [x] Source guards are specified.
- [x] Final build, diff, security, and optional frontend verification are specified.
