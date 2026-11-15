# Tasks: Mortal transport/storage

Issue: [#1515](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1515)
Spec and implementation plan: [spec.md](spec.md), [plan.md](plan.md).

## Setup
- [x] T001 Verify live #1511/#1513 dependency closure, main base and publish isolated task branch.
- [x] T002 Map existing vehicle/resource/location/item authorities and create this Spec Kit WIP.
- [ ] T003 Finish concrete field schema and cross-artifact analysis before code; verify restored SDK/runtime/category runner.

## US1: Vehicle completeness and selection (FR1–FR3)
- [ ] T004 Add causal RED typed semantic tests in `BookOfEternityClient.Tests/MortalTransportStorageContractTests.cs` with category owner.
- [ ] T005 Implement pure contract in `BookOfEternityClient/Services/MortalTransportStorageContract.cs` and integrate `ValidationService.MetaCodexAndAchievements.cs` and `MortalResourceOwnerComposer.cs`.
- [x] T006a Exact active selection, transient resolution, removal/replacement, canonical admission, protected repair routing and physical publication rollback (22 unit + 17 integration PASS; independent review).
- [ ] T006b Versioned full vehicle creation evidence in existing composer/publication (health history alone is insufficient).
- [ ] T007 Update vehicle current fixtures, GM schema/examples/daemon and narrow guards; verify/review/publish bounded block.

## US2: Storage/access completeness (FR4–FR6)
- [ ] T008 Add RED storage metadata/access/contents tests for shared/restricted ownership, duplicates, exact locations and capacity.
- [ ] T009 Extend `MortalLocationStorageMetadataContract.cs` and existing location planning; add effective `storage_access.json` schema/reduction and source-turn evidence without a second inventory owner.
- [ ] T010 Bind access after-images to original source/backup sets and existing accepted-mechanics publication, with integration rejection and late-fault rollback tests.
- [ ] T011 Update storage bootstrap/fixtures, GM worked examples and guards; verify/review/publish bounded block.

## US3: Consumer interoperability (FR7–FR9)
- [ ] T012 Add RED deposit/retrieve tests for full/inaccessible/wrong-location carriers and exact-boundary success; use `StorageTransportMoveService` existing transition writer.
- [ ] T013 Update CLI/web canonical active/access projection consumers and narrow tests, without UI redesign.
- [ ] T014 Reconcile full intended diff with `tests/selection.json`; discover category ownership, run impact-selected tests and complete independent whole-change review.
- [ ] T015 Publish final source/checkpoint and verify clean GitHub restore; report ready without merging main or closing issue.

Dependencies: Setup → US1/US2 pure contracts → combined publication → US3 → final verification. Read-only mapping or independent tests may run in parallel; shared writer/source changes remain coordinated. No unchecked task is completed by a report alone.
