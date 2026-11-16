# Tasks: Mortal transport/storage

Issue: [#1515](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1515)
Spec and implementation plan: [spec.md](spec.md), [plan.md](plan.md).

## Setup
- [x] T001 Verify live #1511/#1513 dependency closure, main base and publish isolated task branch.
- [x] T002 Map existing vehicle/resource/location/item authorities and create this Spec Kit WIP.
- [ ] T003 Finish concrete field schema and cross-artifact analysis before code; verify restored SDK/runtime/category runner.

## US1: Vehicle completeness and selection (FR1–FR3)
- [x] T004 Add causal RED typed vehicle semantic tests with category owners; implemented in MortalVehicleBirthContractTests and the targeted actor/location/capacity policy tests.
- [x] T005a Structural birth contract at real shape/composer boundaries, player identity, current fixtures and primary guidance; causal RED then 54 unit/26 integration PASS, independent review.
- [x] T005b1 Exact accepted Faction/NPC/location birth references and early full existing vehicle shape; e276d562/a88444dc/7b3c9f28 with delivered guidance4b976df6, independent review.
- [x] T005b2 Effective explicit birth access and existing move read/write controls; accepted access block saved in dcf35e11, 42/42 targeted PASS with independent review. Ownership never overrides explicit denial.
- [x] T005c Same-turn new cargo allocation/projection, fresh dependency fences and real publication/rollback.
- [x] T005d First-birth stack/volume measurement, recoverable shape binding, immutable historical evidence and delivered GM guidance; source0ce1cdad,62unit+16combined PASS, independent review.
- [x] T006a Exact active selection, transient resolution, removal/replacement, canonical admission, protected repair routing and physical publication rollback (22 unit + 17 integration PASS; independent review).
- [x] T006b Versioned full vehicle creation evidence in existing composer/publication; source b18ed5a, policy72/72 plus publication9/9, measured cargo4/4, active17/17, earlyshape6/6 and shared allocation2/2 GREEN; independent focused source and evidence review accepted the bounded receipt slice.
- [x] T007 Vehicle current fixtures, GM schema/examples/daemon and narrow guards updated and reviewed in the accepted structural/access/cargo/receipt blocks; final cross-artifact reconciliation remains T014.

## US2: Storage/access completeness (FR4–FR6)
- [ ] T008 Add RED storage metadata/access/contents tests for shared/restricted ownership, duplicates, exact locations and capacity.
- [ ] T009 Extend `MortalLocationStorageMetadataContract.cs` and existing location planning; add effective `storage_access.json` schema/reduction and source-turn evidence without a second inventory owner.
- [x] T009a Current-scene populated/empty storage birth capacity and immutable child evidence; 819ef456 bounded acceptance, 185 passing selected executions across joined checkpoints, independent Astra High review.
- [ ] T009b Complete true populated offscreen storage birth through existing item/location authority; current birth followed by travel is not this coverage.
- [x] T010 Access after-images bound to original source/backup sets and existing accepted-mechanics publication; accepted rejection and reached late-fault rollback evidence. Offscreen extension must retain this contour.
- [ ] T011 Update storage bootstrap/fixtures, GM worked examples and guards; verify/review/publish bounded block.

## US3: Consumer interoperability (FR7–FR9)
- [ ] T012 Add narrow new-schema consumption/transfer interoperability tests through the existing writer; do not add ordinary post-birth movement/access policy.
- [x] T013 Storage effective-access view and canonical-only vehicle display accepted; exact string identities/selection, no pending or legacy fallback. At f431ad1c, 30/30 targeted cases pass, catalog discovery and independent review accepted; no UI redesign.
- [ ] T014 Reconcile full intended diff with `tests/selection.json`; discover category ownership, run impact-selected tests and complete independent whole-change review.
- [ ] T015 Publish final source/checkpoint and verify clean GitHub restore; report ready without merging main or closing issue.

Dependencies: Setup → US1/US2 pure contracts → combined publication → US3 → final verification. Read-only mapping or independent tests may run in parallel; shared writer/source changes remain coordinated. No unchecked task is completed by a report alone.
