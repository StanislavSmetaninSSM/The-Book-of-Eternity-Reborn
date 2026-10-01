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

- [x] T020-A B2a: shared leased bootstrap/config+generation, original-handler-or-block legacy admission, explicit publication outcomes and recorder-free ordinary writer route; focused consumer/cold checks and independent review (round-1 fixes 51/51 GREEN at 6435920d + exact catalog patch; 106/10253 discovery audited; both P2 fixes accepted by independent Astra XHigh at cbc6b29c; normal source/catalog delivery resolved at d3773edf)
- [x] T020-B B2b: prepared browser settings/audio member sets, retaining B2a strict client entrypoint bootstrap, and real Linux console settings/process-restart acceptance plus automated browser checks; later live browser run on user-supplied server/access; independent review
- [x] T020-B1 Browser prepared settings/audio capability: backend 66/66 GREEN at 6075fac4, visible outcome/dispatch/response-ownership frontend 22 scenarios (1 file case) GREEN at 5d304430, exact existing frontend consumers 27/27 at 527dfa1b, 110/10267 discovery-only audit; independent review found one failed-load UI lifecycle P2; correction 34/34 covering frontend cases at 3442c169 and 110/10268 discovery-only audit, scoped Astra XHigh re-review accepted at 44f80acb with no new consequential defect; normal catalog delivery resolved at d3773edf; live browser run deferred by user
- [x] T020-C1 B2c preparation helper: detached candidate plus exact config/projection/manifest member set, explicit config/generation baseline and read-only mod normalization; ten-case intended RED observed, preparation-only 12/12 GREEN at c69e6237 including two existing manifest consumers; integrated menu/preparation evidence now 42/42 interim GREEN; connected Astra XHigh review accepted at c541678e; literal-only P3 readback accepted at 88a02f36
- [x] T020-C2 B2c detached console controller and preview source: real console owner, explicit publication outcomes, retryable pre-publication I/O, captured-generation confirmed reload, portable draft-aware mod reads; 30/30 covering GREEN at 05ab90a5; menu/ordinary-entry integration complete with 37/37 affected covering GREEN at 48357174; live PTY menu/QTE save/process restart passed on the same source; connected Astra XHigh review and P3 scoped readback accepted at 88a02f36
- [x] T020-B2 Console coupled config/projection/system-mod-manifest settings publication and real console settings/process-restart acceptance and automated browser-client checks; five scripted console process cases pass at 48357174; live PTY menu/QTE save/process restart passed at source 48357174; connected console review and scoped P3 readback accepted at 88a02f36; live browser run deferred to user-supplied server/access and not a current-phase blocker; five separate scripted Windows console checks passed at immutable 88a02f36, with PTY/browser/GM/full-game coverage still open

- [x] T020 Add failing client storage/generation/settings restart scenarios before changing `Core/FileSystemManager.cs` and `Core/StateManager.cs`
- [x] T021 Integrate B1 for recorder-free ordinary local writes using common OS code, preserving participating leases/generation and exact bytes; no synthetic FileIdentity. Legacy generation/read/receipt/replacement routes remain B3; Windows storage coverage remains unverified beyond the five scripted B2c console checks at 88a02f36
- [x] T021-B Explicit common bootstrap for valid config and generation, plus the predeclared browser settings/audio transaction route; preserve runtime snapshot rollback and local UI ownership checks
- [x] T022 Add and prove legacy-journal cutover guard, retaining original handler and evidence on unsupported recovery
- [ ] T023 Continue replacing superseded anti-owner tests only as their legacy transaction/receipt routes migrate in B3, with matching content/path/recovery coverage. The B2 migrated ordinary paths have focused common tests and the current `tests/selection.json`; no broad legacy-test deletion is claimed
- [x] T024 Run selected browser consumers and real Linux console menu-setting-restart flows; defer live browser run to user-supplied server/access, document and independently review B2

## B3 — US2/US3 transactions and accepted state

- [x] T030-A Exact leased generation snapshot and wired read/fence consumers accepted: explicit absence/invalid distinction, BOM bytes, common type/link/hard-link admission and no recursive fence; 138-case covering result, 10-case original legacy-gate correction and 1-case source-guard correction passed as separately recorded; 115/10310 discovery and 140-case plan audited; independent Astra XHigh source/evidence and normal-tip readback accepted at 740b4d09; normal source/catalog delivery and fresh 3,595-file restore verified. Rotation/clear remain unchanged
- [ ] T030-B Initial 15-case public clear/rotation/pending-inspector RED observed (14 intended failures, one retained legacy guard pass); first common implementation 15/15 GREEN at 00e2ce0b plus exact packet; expanded 37-case RED observed (30 reported passes, seven intended failures), narrow caller corrections 86/86 covering GREEN at f38a68f6 plus exact packet; eight-case actual proposal-store RED observed (5 passes/3 intended failures), empty-only normalization and exact consumers 30/30 reported GREEN at 438d5bbe plus packet, with three Windows-only bodies unexecuted on Linux; six-case root-key RED observed (five intended scaffold failures, one retained Linux pass), shared-key implementation plus exact consumers passed 17/17 at 37a56703 plus packet; fresh 121-category/10,340-method discovery plus zero-test Linux 123-case / Windows six-case plans passed at bcecdd3a plus packet; independent review, normal delivery and actual Windows execution pending. Prepare generation plus all selected clear/worker file deletions for one existing B1 journal decision, retaining lifecycle-then-replacement lease order and rejecting the complete invalid selected namespace before mutation; exclude preserved context pack at traversal boundary
- [ ] T030-C Make the exact browser/console pending checks ignore validated directory-only residue and allow proposal-ID reuse only after safe recursively-empty destination normalization; retain unknown/nonempty/link/type conflicts and legacy-evidence admission
- [ ] T030-D Prove replacement interruption phases, full conflict preflight, generation/revision/owner fences and representative cold-process recovery; include nested empty snapshot/proposal residue and preserved opaque context-pack negatives

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
