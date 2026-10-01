# Tasks: Trusted local storage and cross-platform runtime

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)
Spec: [spec.md](spec.md) | Plan/checkpoint: [plan.md](plan.md)

## R0 — Reconstruct approved requirements

- [x] T001 Verify published main-based `1553-cross-platform-runtime` and adopt reviewed remote-persistence instructions
- [x] T002 Reconstruct approved storage scope and later interactive-GM/Windows-owner-verification clarifications in spec.md and plan.md
- [x] T003 Run Spec Kit path/consistency checks, independently review this document block, publish evidence and verify clean restoration

## B1 — Foundations for US1/US2

- [x] T010 Write failing `TrustedLocalFileScopeTests.cs` for confined roots/exact files, traversal, links, wrong types, case sensitivity and non-following cleanup; add a narrow catalog owner
- [x] T011 Implement `Core/TrustedLocalFileScope.cs` from those tests; run its category and publish checkpoint (35/35 Linux cases and independent Astra XHigh review at source 18461c67)
- [x] T012 Write failing `TrustedLocalFilePublicationTests.cs` for create/replace/delete, exact bytes/absence, expected-content conflicts, crash phases, evidence retention and hard-linked outside-byte safety (RED confirmed; corrected candidate 66/66 Linux-run cases at 8d36e708)
- [x] T013 Implement `Core/TrustedLocalFilePublication.cs` and journal data contracts; no production cutover yet (source 8d36e708, accepted at reviewed checkpoint 795d79be)
- [x] T013-M Add declared multi-member/generation/conflict-preflight cases to that same journal protocol; browser settings needs this slice before B2 acceptance (explicit bootstrap generation and complete conflict preflight accepted at 795d79be)
- [x] T014 Validate catalog ownership by discovery, run only B1 categories, independent Astra XHigh review; fix findings, publish and verify remote SHA (66/66 GREEN, 104/10232 discovery-only audit, both P2 findings addressed; accepted 795d79be)

## B2 — US1 ordinary startup/settings

- [ ] T020-A B2a: shared leased bootstrap/config+generation, original-handler-or-block legacy admission, explicit publication outcomes and recorder-free ordinary writer route; focused consumer/cold checks and independent review (round-1 fixes 51/51 GREEN at 6435920d + exact catalog patch; 106/10253 discovery audited; both P2 fixes accepted by independent Astra XHigh at cbc6b29c; normal catalog publication remains the acceptance gate)
- [ ] T020-B B2b: prepared browser settings/audio member sets, retaining B2a strict client entrypoint bootstrap, and actual Linux console/browser settings/process-restart acceptance; independent review
- [ ] T020-B1 Browser prepared settings/audio capability: backend 66/66 GREEN at 6075fac4, visible outcome/dispatch/response-ownership frontend 22 scenarios (1 file case) GREEN at 5d304430, exact existing frontend consumers 27/27 at 527dfa1b, 110/10267 discovery-only audit; independent review found one failed-load UI lifecycle P2; correction 34/34 covering frontend cases at 3442c169 and 110/10268 discovery-only audit, scoped re-review and normal catalog delivery pending
- [ ] T020-B2 Console coupled config/projection/system-mod-manifest settings publication and actual two-client settings/process-restart acceptance; console slice remains pending, actual Browser Use is environment-blocked, Windows is owner-run

- [ ] T020 Add failing client storage/generation/settings restart scenarios before changing `Core/FileSystemManager.cs` and `Core/StateManager.cs`
- [ ] T021 Integrate B1 for common writes on both OSes, preserving participating leases/generation and exact bytes; no synthetic FileIdentity
- [ ] T021-B Explicit common bootstrap for valid config and generation, plus the predeclared browser settings/audio transaction route; preserve runtime snapshot rollback and local UI ownership checks
- [ ] T022 Add and prove legacy-journal cutover guard, retaining original handler and evidence on unsupported recovery
- [ ] T023 Replace superseded anti-owner tests only with matching content/path/recovery coverage; maintain actual `tests/selection.json`
- [ ] T024 Run selected consumers and actual Linux console/browser menu-setting-restart flows; document and independently review B2

## B3 — US2/US3 transactions and accepted state

- [ ] T030 Add multi-file crash/generation/conflict/cleanup tests before migrating FileSystemManager, browser rollback and reward transactions
- [ ] T031 Implement logical receipts and complete member-set recovery without weakening accepted-turn/history ownership
- [ ] T032 Migrate save/load staging and restoration with exact bytes, not required inode identity; add negative staging tests
- [ ] T033 Execute real file-GM turn and save/load/restart in both Linux clients; run affected categories, update operational docs/examples as applicable, independent review

## B4 — US4 persistent GM/session lifecycle

- [ ] T040 Record current bridge/daemon/process/IPC design, exact supported provider behavior and failure boundaries in plan.md before edits
- [ ] T041 Test first, then implement owned persistent interactive terminal/session and automatic daemon input on Linux/Windows
- [ ] T042 Verify manual/automatic input, readiness, multiple turns, restart, timeout/cancel and uncertain ownership recovery; preserve workspace until stop is confirmed
- [ ] T043 Verify a real CLI workflow, synchronize GM operational prompts/examples and independently review B4

## B5 — US5 and acceptance

- [ ] T050 Audit and convert remaining Windows-only audio/clipboard/launcher/system mechanisms with common behavior
- [ ] T051 Address narrow QTE source newline contract if reproduced; no inventory weakening or broad normalization
- [ ] T052 Audit complete Linux console/browser/GM/gameplay/save-load scope; collect end-to-end evidence and independent review
- [ ] T053 Deliver reproducible Windows owner-run checklist, explicit unverified items and verified remote recovery checkpoint

Dependencies: R0 before B1; B1 before B2; B2 before B3; B4 investigation may run in parallel, but shared-state edits are serialized. Every bounded code block uses RED/GREEN, current category selection, remote WIP before lengthy tests/review, independent review and verified publication. A checked task requires inspected implementation/evidence, not an agent report alone.
