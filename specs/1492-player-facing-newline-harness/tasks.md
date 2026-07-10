# Tasks: Player-facing Newline Harness

**Source Issue**: [#1492](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1492)

## RED

- [x] T001 Add runtime normalizer tests for PowerShell newline tokens and preservation cases.
- [x] T002 Add GM helper test proving player-facing output is normalized before JSON serialization.
- [x] T003 Add accepted narrative/interface fallback tests for direct malformed writes.
- [x] T004 Run focused tests and record RED evidence: all four new tests failed on literal backtick line-break tokens before implementation.

## GREEN

- [x] T005 Extend the shared C# normalizer.
- [x] T006 Add scoped `Write-BoeJson` player-facing payload normalization.
- [x] T007 Add accepted-output persisted fallback normalization.
- [x] T008 Update GM compact guidance, main guide/example, and source guards.

## Verification

- [x] T009 Run focused runtime/helper/validation tests: 138/138 passed.
- [x] T010 Run documentation guards and `git diff --check`: 110/110 documentation tests passed and the final diff check is clean.
- [x] T011 Request independent review and address findings: preserved multiword and uppercase-adjacent backtick spans in both C# and PowerShell; final re-review found no blocking or significant issues.
- [x] T012 Replay the saved Chaos Sea conflict opening without literal escape tokens: Agent Console showed real paragraph breaks over the unchanged raw live artifact.
- [ ] T013 Comment evidence on #1492, close it, merge/push, and resume the spiritual-combat Golden Path.
