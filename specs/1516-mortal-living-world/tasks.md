# Tasks: Mortal living-world materialization

Source: [#1516](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1516). Follow [spec.md](spec.md) and [plan.md](plan.md).

## Setup
- [x] T001 Inspect live issue/main, governance, existing owners and publish task branch; record in plan.md.
- [ ] T002 Finalize contract data model and independent risk review of owner joins in data-model.md.

## US1 — Meaningful elapsed time and legitimate no-change
- [x] T003 [US1] Add failing minimal typed tests in BookOfEternityClient.Tests/MortalLivingWorldAssessmentTests.cs for first time skip, already pending cycles, exact boundary and missing/stale/incomplete report.
- [x] T004 [US1] Implement pure input/report admission in BookOfEternityClient/Services/MortalLivingWorldAssessment.cs; validate bounded no-change assessment without invented deltas.
- [x] T005 [US1] Add category ownership and run official targeted RED/GREEN; record actual outcomes in plan.md.

## US2 — Coherent materialization
- [ ] T006 [US2] Test and implement complete event rows, entity/target links, exact original/result effects and public news proof in MortalLivingWorldEvents.cs and its typed tests (updates first, explicit creation/removal follow-up).
- [x] T007 [US2] Add authenticated source capture in Services/MortalLivingWorldSource.cs and closed current-Mortal observation family; real source integration tests in PendingTurnSnapshotPresenceIntegrationTests.LivingWorld.cs. Runtime publication remains T008.
- [ ] T008 [US2] Integrate ordinary raw gate and canonical effect/receipt publication into existing canonical refresh owner; update snapshot/absence inventories. Prove partial packages and tampered history fail.

## US3 — Atomic consumers and privacy
- [ ] T009 [US3] Add reached late-failure rollback, exact absence and retry tests in MortalLivingWorldPublicationTests.cs.
- [ ] T010 [US3] Join private report/history to Mortal joint plan before seal/take and verify real joint consumer; cover or explicitly reject unsupported clock paths in QteSceneService without a bypass.
- [ ] T011 [US3] Prove new private evidence is not news and undiscovered references do not leak through common console/browser projection.

## Delivery
- [ ] T012 Update TaskGuides/OtherGuides, daemon prompt, Examples and validation manifest plus documentation/source guards for autonomous and no-change authoring.
- [ ] T013 Update active bootstrap/test fixtures as necessary; record no-update rationale for afterlife and unrelated UI.
- [ ] T014 Reconcile tests/selection.json and catalog discovery with intended diff; independent final review, selected checks and fresh remote recovery.

Dependencies: T003–T005 precede T006–T008; T009–T011 require actual publication owner. T012 can run alongside runtime work after contract is fixed. No test helper alone satisfies T007–T011. Do not merge or close issue in this assignment.
