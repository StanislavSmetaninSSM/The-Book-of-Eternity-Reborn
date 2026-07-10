# Tasks: Universal Realm-Aware Trade Command

**Source Issue**: [#1491](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1491)

## Phase 1 - RED

- [x] **T001** Add resolver tests for Mortal World, Chaos Sea, Shining Abode, Russian realm aliases, argument preservation, and unresolved realm.
- [x] **T002** Run T001 and confirm failure because the shared resolver does not exist.
- [x] **T003** Add catalog/parser/help tests proving `/trade` and `/торговля` are registered, argument-capable, and discoverable.
- [x] **T004** Run T003 and confirm failure because the aliases are absent.
- [x] **T005** Add console tests proving `/торговля` reaches NPC trade in Mortal World and Guardian trade in Chaos Sea, plus an unresolved-realm no-mutation message.
- [x] **T006** Run T005 and confirm the current silent/unknown-command failures.
- [x] **T007** Add browser tests proving the generic command builds and submits through existing Mortal, Guardian, and Shining trade services.
- [x] **T008** Run T007 and confirm the generic command is currently rejected.

## Phase 2 - GREEN

- [x] **T009** Implement the side-effect-free shared realm trade resolver with canonical command output and untouched arguments.
- [x] **T010** Register the generic command and add localized help/menu metadata.
- [x] **T011** Route the console command through the resolver before existing realm-specific dispatch and preserve explicit unresolved-realm guidance.
- [x] **T012** Route the browser command through the resolver before existing builders/session writes and retain the canonical routed command for submit.
- [x] **T013** Run focused tests and make the smallest implementation adjustments needed for green.

## Phase 3 - Contract and Quality Gates

- [x] **T014** Review the diff for Mortal/afterlife GM-authored contract impact; update prompts/docs/examples if any contract changed, otherwise record the client-owned no-update rationale.
- [x] **T015** Run afterlife documentation guards and the full C# test suite.
- [x] **T016** Request an independent code review and address findings with new RED tests where behavior changes.
- [x] **T017** Merge #1491 to `main`, restart the Golden Path runtime, and replay `/торговля` in Chaos Sea through Agent Console.
- [ ] **T018** Comment verification evidence on #1491 and close it only after automated and live evidence pass.

## Phase 4 - Location-Aware Selection Amendment

- [x] **T019** Add RED tests proving `/торговля` lists named local trade entities without an ID prompt in Mortal World, Chaos Sea, and Shining Abode.
- [x] **T020** Add RED tests proving the selection screen creates no pending GM request or persistent local UI lock.
- [x] **T021** Implement location-aware console selection for Chaos Sea and direct Shining faction selection while preserving the existing Mortal selector.
- [x] **T022** Implement browser trade-target cards/actions for all three realms and keep stable IDs internal to action commands.
- [x] **T023** Remove player-facing ID instructions from universal trade help and empty states; retain explicit target arguments only as internal deep links.
- [ ] **T024** Re-run independent review, focused/full verification, and the Chaos Sea Agent Console replay through the selection screen.

## Verification Notes

- Focused trade/catalog/help coverage: 102 passed.
- Afterlife documentation guards: 110 passed.
- Browser frontend verification: typecheck, 138 tests, and production build passed.
- Full C# suite after the location-aware amendment: 5579 passed and 8 failed. Seven content failures reproduce unchanged on base `c1758e8b`. The remaining `AgentConsoleLiveSmokeTests` failure is an intermittent full-suite interaction; the exact test passes in isolation on both the feature branch and base (`1/1` each). No failure exercises or depends on the #1491 diff.
- Location-aware amendment: 82 trade/service/client tests passed, including read-only named selection, exclusion of remote NPC/Guardian fixtures, no ID prompts, no premature lock/pending files, and successful purchases after selection.
- Final expanded verification: 232 focused C# tests passed; frontend typecheck, 138 player-facing tests, and production build passed. Three independent review passes found four behavioral gaps; each was reproduced with RED coverage and corrected. The final review reported no behavioral findings.
- Golden Path replay on 2026-07-11: `/торговля` in Chaos Sea opened a named local-Guardian selector and then Mirven's existing trade panel. No Guardian id, pending trade file, pending turn, or daemon turn was produced by opening the selection or panel.
- The replay exposed raw `Trade` in the Guardian choice. Console and browser RED tests now require `Торговый домен`; both passed after the shared player-facing domain formatter was applied. The expanded `*Trade*` run passed 293 tests; its five failures are the previously recorded baseline Realm Segregation fixture failures.
