# Tasks: Universal Realm-Aware Trade Command

**Source Issue**: [#1491](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1491)

## Phase 1 - RED

- [ ] **T001** Add resolver tests for Mortal World, Chaos Sea, Shining Abode, Russian realm aliases, argument preservation, and unresolved realm.
- [ ] **T002** Run T001 and confirm failure because the shared resolver does not exist.
- [ ] **T003** Add catalog/parser/help tests proving `/trade` and `/торговля` are registered, argument-capable, and discoverable.
- [ ] **T004** Run T003 and confirm failure because the aliases are absent.
- [ ] **T005** Add console tests proving `/торговля` reaches NPC trade in Mortal World and Guardian trade in Chaos Sea, plus an unresolved-realm no-mutation message.
- [ ] **T006** Run T005 and confirm the current silent/unknown-command failures.
- [ ] **T007** Add browser tests proving the generic command builds and submits through existing Mortal, Guardian, and Shining trade services.
- [ ] **T008** Run T007 and confirm the generic command is currently rejected.

## Phase 2 - GREEN

- [ ] **T009** Implement the side-effect-free shared realm trade resolver with canonical command output and untouched arguments.
- [ ] **T010** Register the generic command and add localized help/menu metadata.
- [ ] **T011** Route the console command through the resolver before existing realm-specific dispatch and preserve explicit unresolved-realm guidance.
- [ ] **T012** Route the browser command through the resolver before existing builders/session writes and retain the canonical routed command for submit.
- [ ] **T013** Run focused tests and make the smallest implementation adjustments needed for green.

## Phase 3 - Contract and Quality Gates

- [ ] **T014** Review the diff for Mortal/afterlife GM-authored contract impact; update prompts/docs/examples if any contract changed, otherwise record the client-owned no-update rationale.
- [ ] **T015** Run afterlife documentation guards and the full C# test suite.
- [ ] **T016** Request an independent code review and address findings with new RED tests where behavior changes.
- [ ] **T017** Merge #1491 to `main`, restart the Golden Path runtime, and replay `/торговля` in Chaos Sea through Agent Console.
- [ ] **T018** Comment verification evidence on #1491 and close it only after automated and live evidence pass.
