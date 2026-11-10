# T179-Pd-AUDIT-SBM independent documentary review

**Verdict: BLOCKED_DOCUMENTARY_CORRECTION_REQUIRED — SBM-D01.** The source-backed audit is otherwise supported within its stated static scope. Correct the capture-order sentence before closing this documentary item. This review releases no implementation, build, discovery, tests, CI, download, settings change, catalog/release mutation, schema, or gameplay activation.

Reviewed candidate: `96bd6acbfa0a403c3c6bf53c9cdcadddedc69899`; baseline: `f87aa544bd60897a2a60d5623a751589fc78c896`. Independent reviewer: `/root/t179pc_independent_review`, separate from the author and root writer. Retained configured model/effort: **gpt-6-astra / xhigh**, supported by the coordinator's actual spawn/configuration receipt; backend telemetry is unavailable.

## Confirmed material finding

**SBM-D01 — the dependency map reverses the capture/preparation boundary.** At candidate `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:414`, the S→C→raw B row calls for a “prepare/seal boundary before closing capture.”

This conflicts with the same audit's SBM02 (line 395), V05 (line 427), and the current actual owner. In `GameEngine.MortalJointPublicationAttempt.cs:83–87`, `CloseCapture` irreversibly leaves Capturing; `RetainStage` at 89–94 refuses an attempt still capturing. `CaptureCurrentHotCustodyAsync` closes capture at 177. The canonical publisher at `GameEngine.MortalJointRepairCanonicalPublication.cs:43–49` awaits that capture closure before creating and retaining the preparation. A future implementer following line 414 literally could place preparation/sealing on the wrong side of the accepted boundary.

Required narrow correction: **source-family capture/freeze while Capturing → irrevocable CloseCapture before preparation/seals; the future genuine prepare/seal split binds retained S/C/raw B and source guards before seals.** Preserve current stage ordering and the prohibition on after-seal attachment or v2 promotion. This is a documentary consistency correction, not approval to move stages or design a new authority owner. The parent acknowledged the finding; no correction was present in the reviewed candidate.

No additional confirmed material defect was found in this seven-file block. This statement does not qualify any proposed future codec, semantic source completeness, or runtime scenario.

## Bounded substantive assessment

The ten seams distinguish implemented structural custody, original-reader closure, real retained C/two-pass ledger, exact unattempted registry cleanup, and current v2 native/cold behavior from the missing full semantic S, active B, ordered admission history, and M3/cold verification work. The actual repair route can replace prior repair files and complete worker apply without Ready; neither absent Ready/request nor the final current JSON proves no repair history. The existing completed-musings comparison acquires a lease, so the proposed provided-lease extraction remains a real dependency. Expiry is in the publisher's final cleanup after the physical close scope, including close failure; it supplies no successful-close or source-acceptance proof.

The proposed strict codec remains nested in existing M and explicitly unimplemented. Hashes, a schema bump, exported data, or hot custody cannot establish cold semantic authority. The dependency map properly distinguishes a pure/shared-kernel data dependency from the current worker evidence acceptance hold.

The twelve checks are explicitly FUTURE_UNRUN, with no category or release created. The original T178–T191 tasks are each uniquely present and unchecked. Their order retains action+ack, same original turn/time boundary, committed-prefix deduplication, actual same unresolved situation through existing ordinary child-request/M owners after decline, repair/retry/lost-completion/restart, target ownership before cleanup, and console/browser parity. Notification, intent, HOLD, replay, forced accept, skipped outcome, or reoffer is not treated as ordinary completion. T192+ remain prerequisites; D4, guided US5, actual DOM/visual, Windows, D8, full F1 and T197 are not closed. No proved F1 obstruction selecting F2 is established here.

## Authority and historical evidence

The five current notices consistently limit this pass to the documentary source/dependency audit. The Windows permission question remains unanswered; failed hold delivery and technical SOURCE/availability review are not user permission. Earlier continuation/release entries remain historical rather than authorizing a new action.

The prior 13 PASS and first two Linux FAIL retain their original f9bc provenance. The reported Windows job success remains separate from unavailable raw worker evidence (WIN-E01). This review neither retries access nor requalifies those outcomes. The narrow no-GM-update rationale is supported by the document-only diff; future emitted/shared contract changes still need the required synchronization.

## Verification and limits

- Exact diff: seven documentary files, 2,667 insertions and three deletions; complete binary/full-index diff 414,668 bytes, SHA256 `5036c47cc7995585d349bae6aa485d3daf12ada6a388c1809dcf5602ae94c661`.
- All 36 whole Git pins and 46 exact source projections independently match their bytes, Git object identity and SHA256. All three nonremote Git command records were reproduced locally; the fourth historical `ls-remote` stdout/hash was inspected without network reexecution.
- Product, tests, catalogs/selections, scripts/workflows and governance have an empty baseline-to-candidate diff. Candidate HEAD was clean during inspection.
- Prior f87 delta: 220 exact files covering all 217 changed paths; current candidate delta: 222 exact files covering all 219 changed paths; no deletions. Every listed local recovery byte/mode/hash matches its exact Git blob; current rows also match the writer.
- The retained previously verified full7acd source base has its exact receipt, identity, clean status and lack of alternates/sparse checkout rechecked. This is full base plus complete delta, not a new full checkout, transport replay, 3.6 GB rescan, build, or runtime proof.
- Current delta receipt SHA256: `76912d6a5facd64adcf35013a898ccd63da631975327ca64b8af7b343fe46827`. Prior f87 receipt SHA256: `454601a941d39b7b3507fcde5091b8353e25fa977d28b1f33b3711e0b3b8c287`.

The paired JSON contains exact Git/blob/file pins, all projection digests, the material finding's source text, five substantive documentary diff texts and all seven diff hashes, local command results, historical-command distinctions, recovery checks, task/check inventories, and the read-only checker text. Only this new MD/JSON pair was written. No repository change, additional agent, application/runtime execution, network access, or actual release occurred.

After the single wording correction, affected documentary verification and persistence are the next steps. **FINALSTOP.**
