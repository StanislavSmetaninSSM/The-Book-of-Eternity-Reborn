# Tasks: Network Pass-The-Turn Multiplayer

**Input**: Design documents from
`/specs/1251-network-pass-turn-multiplayer/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/relay-protocol.md`, `quickstart.md`

**Tests**: Behavior changes require test-first work. Write focused failing tests
before implementation tasks.

## Phase 1: Setup

- [ ] T001 Confirm GitHub issue #1251, current branch, `git status --short`, and active Spec Kit feature path `specs/1251-network-pass-turn-multiplayer/`.
- [ ] T002 Read `AGENTS.md`, `.specify/memory/constitution.md`, #1251, `spec.md`, `plan.md`, `data-model.md`, and `contracts/relay-protocol.md`.
- [ ] T003 [P] Inventory existing accepted-turn, validation, save/load, local web host, and GM bridge code paths that will participate in handoff.
- [ ] T004 [P] Inventory current console/browser command entry points where multiplayer status, invite/join, read-only, and handoff controls will appear.
- [ ] T005 [P] Identify GM-facing prompt, guide, example, and documentation coverage files that must change for persona ledger and multiplayer handoff contracts.

---

## Phase 2: Foundational Contracts

**Purpose**: Define contracts that block every implementation story.

- [ ] T006 Add failing C# contract tests for campaign metadata and seat/controlled-entity separation in `BookOfEternityClient.Tests/NetworkMultiplayerContractTests.cs`.
- [ ] T007 Add failing C# contract tests for handoff gate blocked reasons in `BookOfEternityClient.Tests/NetworkHandoffGateTests.cs`.
- [ ] T008 Add failing C# contract tests for relay accepting only hash-linked, validation-accepted handoff manifests in `BookOfEternityClient.Tests/NetworkRelayContractTests.cs`.
- [ ] T009 Add failing C# tests proving non-active seats are read-only in `BookOfEternityClient.Tests/NetworkSeatAuthorityTests.cs`.
- [ ] T010 Add failing source/docs guard for required GM-facing multiplayer/persona documentation in `BookOfEternityClient.Tests/NetworkMultiplayerDocumentationTests.cs`.
- [ ] T011 Define runtime records for `NetworkCampaign`, `NetworkSeat`, `SeatCredential`, `ControlledEntityRef`, `HandoffPackageManifest`, `RelayResumePoint`, and `HandoffGate` in the appropriate `BookOfEternityClient/Services/` or runtime contract namespace.
- [ ] T012 Define validation rules for campaign metadata, seats, controlled entity, turn ordinal, and hash continuity.
- [ ] T013 Update developer docs to state relay is transport/coordinator only and cannot mutate game-state semantics.

**Checkpoint**: Core model and tests exist; user stories can proceed.

---

## Phase 3: User Story 1 - Host creates campaign (P1)

**Goal**: Host can create a shared-protagonist campaign from a valid local state.

**Independent Test**: A local single-player session becomes a campaign with host
seat, active seat, GM authority seat, controlled entity, and latest hash.

- [ ] T014 [US1] Add failing create-campaign service tests in `BookOfEternityClient.Tests/NetworkCampaignCreationTests.cs`.
- [ ] T015 [US1] Implement campaign creation service in `BookOfEternityClient/Services/NetworkCampaignService.cs`.
- [ ] T016 [US1] Resolve afterlife controlled entity as shared `player_soul`.
- [ ] T017 [US1] Block Mortal World campaign creation when persona/guise ledger is missing.
- [ ] T018 [US1] Add player-facing console/browser status copy for created campaign and blocked campaign creation.
- [ ] T019 [US1] Update GM-facing docs/examples for campaign creation authority.

---

## Phase 4: User Story 2 - Invite and join through relay (P1)

**Goal**: Guest joins through relay/invite-code flow and synchronizes latest
accepted state.

**Independent Test**: A second local client joins with an invite, receives a seat
credential, downloads resume point, and starts read-only if not active.

- [ ] T020 [US2] Add failing relay invitation/join tests in `BookOfEternityClient.Tests/NetworkRelayJoinTests.cs`.
- [ ] T021 [US2] Implement local/reference relay storage for campaigns, invitations, pending join requests, seats, and credentials.
- [ ] T022 [US2] Implement invitation creation with expiration, revocation, and optional host approval.
- [ ] T023 [US2] Implement join/reconnect with persistent seat credentials.
- [ ] T024 [US2] Add console invite/join/reconnect flows with Russian player-facing errors.
- [ ] T025 [US2] Add browser invite/join/reconnect flows with equivalent affordances.

---

## Phase 5: User Story 3 - Accepted turn handoff (P1)

**Goal**: Active seat completes a GM-resolved turn and relay distributes the
accepted handoff to the next seat.

**Independent Test**: Two-seat local relay campaign completes one accepted turn
handoff without manual save transfer.

- [ ] T026 [US3] Add failing local relay E2E test for active action, host GM resolution, accepted validation, upload, next-seat sync.
- [ ] T027 [US3] Implement handoff package builder with manifest, hashes, validation status, and controlled entity.
- [ ] T028 [US3] Implement handoff gate integration with active GM turn, repair loop, unfinished local action, pending QTE, invalid state, and hash mismatch checks.
- [ ] T029 [US3] Implement relay upload/download/apply path for accepted handoff packages.
- [ ] T030 [US3] Enforce read-only mode for non-active seats in runtime command handling.
- [ ] T031 [US3] Route non-host active-player GM actions through relay to host GM authority.
- [ ] T032 [US3] Add console/browser handoff controls and blocked-state reasons.

---

## Phase 6: User Story 4 - Dormancy and resume (P2)

**Goal**: Campaign can stop and later resume from the latest accepted point.

**Independent Test**: All clients disconnect, reconnect with seat credentials,
sync latest resume point, and no game-time advancement occurs.

- [ ] T033 [US4] Add failing dormancy/resume tests in `BookOfEternityClient.Tests/NetworkDormancyResumeTests.cs`.
- [ ] T034 [US4] Implement relay resume point retention.
- [ ] T035 [US4] Implement reconnect sync-before-act gate.
- [ ] T036 [US4] Block GM-resolved actions when host GM authority is offline.
- [ ] T037 [US4] Document that real-world downtime does not advance in-fiction time or afterlife progression.

---

## Phase 7: User Story 5 - Mortal persona ledger (P2)

**Goal**: Mortal identity changes are fiction events; seat changes are not.

**Independent Test**: Seat handoff does not affect NPC/faction identity state,
but GM-accepted persona/guise change records ledger and consequences.

- [ ] T038 [US5] Add failing persona-ledger tests in `BookOfEternityClient.Tests/MortalPersonaLedgerTests.cs`.
- [ ] T039 [US5] Implement Mortal persona/guise ledger model and validation.
- [ ] T040 [US5] Implement persona/guise change event recording and affected NPC/faction knowledge references.
- [ ] T041 [US5] Ensure network seat handoff does not create persona ledger entries.
- [ ] T042 [US5] Update GM prompts/guides/examples for persona/guise fiction events.

---

## Phase 8: Polish and Hardening

- [ ] T043 [P] Add privacy limitation documentation for trusted/private MVP campaigns and future encrypted payload hardening.
- [ ] T044 [P] Add source guards preventing relay code from mutating known game-state semantic paths.
- [ ] T045 [P] Add browser/frontend verification for multiplayer status/read-only/handoff surfaces when UI is implemented.
- [ ] T046 Run `quickstart.md` local/reference relay validation and record evidence in issue #1251.
- [ ] T047 Run focused C# and docs verification commands from `plan.md`.
- [ ] T048 Reconcile `tasks.md`, GitHub issue comments, and follow-up issues before reporting completion.

## Dependencies & Execution Order

- Phase 1 before all work.
- Phase 2 blocks implementation stories.
- US1, US2, and US3 are MVP and should complete before P2 stories.
- US4 depends on US2 and US3.
- US5 can begin after Phase 2 but blocks Mortal World network handoff release.
- Polish/hardening follows implemented MVP.

## MVP Scope

MVP is US1 + US2 + US3 for local/reference relay, with Mortal World creation
blocked until the persona ledger prerequisite from US5 exists. Afterlife MVP can
prove the shared-soul loop earlier because controlled entity is simpler.
