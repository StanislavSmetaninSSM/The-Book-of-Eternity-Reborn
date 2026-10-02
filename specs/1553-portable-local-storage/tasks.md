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
- [x] T030-B Complete generation plus selected clear/worker deletions through one B1 decision, preserving lifecycle/replacement lease order, full namespace preflight, exclusions and committed revision handling; [bounded B3b acceptance](plan.md#accepted-b3b--replacement-residue-and-root-keys) at tested normal source e5ee470e / evidence a04fa969, independent Astra XHigh accepted 2026-10-01 12:37 UTC
- [x] T030-C Browser/console pending checks and safe recursively-empty proposal-ID reuse preserve unknown/nonempty/link/type and original legacy-evidence conflicts; [distinct Linux/native evidence and limits](plan.md#b3b-distinct-verification-evidence), including Linux admission-only worker-backend qualification, accepted with B3b
- [x] T030-D Replacement interruption/conflict/cold recovery and generation/revision/owner/root-alias fences accepted with B3b; separate Linux 15/86/30/17 results and native historical 4/6 followed by correction 7/7 remain distinct. [Native correction manifest](recovery/evidence/b3b-windows-handle-comparison-20261001/manifest.json) and [review disposition](plan.md#current-checkpoint--b3b-accepted-ordinary-readers-next) address the physical-comparison P2 and direct-write causal assertion; no full new-game/worker/save-load acceptance
- [x] T030-E Ordinary content/presence and real-handle-timestamp readers accepted at normal source 29f48574 / final evidence 9232216f; [bounded acceptance and distinct evidence](plan.md#accepted-t030-e--ordinary-canonical-readers): Linux 76/76 and actual native Windows 25/25, both fixture P2s closed, exact normal delivery/fresh recovery and final independent Astra XHigh review at 2026-10-01 14:50 UTC. Recovery/quiescence/no-reacquire, real leases/fences and explicit browser pre-recorder/original physical boundaries remain intact; full B3/T031/save-load/worker acceptance stays open

- [x] T030-F Ordinary recorder-free canonical directory-tree deletion: complete validated before-image → absence B1 set, no-op absent/empty handling, nonrecursive post-cleanup pruning, real lease/generation and boundary checks, explicit original warm/cold browser cleanup, focused causal/cold/consumer verification and independent review; [accepted bounded plan](plan.md#accepted-t030-f--ordinary-canonical-directory-tree-deletion)

- [x] T030-G Ordinary backup create/restore/cleanup lifecycle through B1 and immediate StateDistributor/QTE uncertainty propagation accepted at evidence `e13dc5a2`: [bounded acceptance](plan.md#accepted-t030-g--ordinary-backup-lifecycle), exact normal source `fda03346`, separate Linux 43/50/19 cohorts with a verified 94-case union, actual Windows 30/30 and final independent Astra XHigh acceptance. Both review findings closed; real Linux quarantine qualified. No whole-flow atomicity or full Linux gameplay acceptance

- [ ] T030 Add multi-file crash/generation/conflict/cleanup tests before migrating FileSystemManager, browser rollback and reward transactions
- [x] T031-A Ordinary coordinated-helper capability accepted at `6c446229`: [bounded contract and distinct evidence](plan.md#accepted-t031-a--ordinary-coordinated-publication), separate Linux 50/20 GREEN cohorts, partial eight-pass/one-preparation-failure and 26-case continuation, plus earlier dependency coverage; verified 66-method/110-case passing union is not an aggregate run. Actual native 16/16 at normal tested `5bfa0a1b`, evidence `9ce7d9e2`, exact normal delivery/fresh recovery and final independent Astra XHigh acceptance at 2026-10-01 19:31 UTC. All fixture findings closed; the Linux kernel32 preparation dependency was open at that helper acceptance; later T030-F/T030-G work qualified the exact real quarantine consumer, while whole preparation remains outside the helper boundary
- [ ] T031 Implement logical receipts and complete member-set recovery without weakening accepted-turn/history ownership; T031-A is the accepted bounded helper slice, not complete accepted-turn/normalizer/browser/save-load migration. T030-F exposed the later Linux StateDistributor → CreateBackupCore original descriptor-bound publication prerequisite before the real quarantine fixture returns; T030-G now reaches and passes that exact Linux body with unchanged quarantine semantics. The [historical backup lifecycle map](backup-lifecycle-cutover.md) identified its immediate consumer boundaries; full T031 remains unchecked

- [ ] T032-A Ordinary save creation and immediate console/browser/autosave callers: causal public-entry RED and producer/B1 resource gate first; then reviewed bounded B1 image prerequisite if needed, create-only publication, owned scratch cleanup, leased stream listing, profile-mirror refresh and truthful outcomes. [Active plan](plan.md#active-t032-a--ordinary-save-creation); [preserved source map](save-creation-cutover.md). Production changes await the coordinator’s independent technical plan review; full T031/T032 remain open

- [ ] T032-A1 Bounded internal image abstraction, strict v2 codec, shared B1 streaming hash/stage/rollback and canonical image-member adapter; preserve v1 authority matrix/generation semantics, prove large before-image cold rollback and resource behavior before caller cutover. Independent technical gate accepted at `7cd1985b`; image tests/scaffold first, capability not yet implemented or accepted

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
