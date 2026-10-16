# T179-Pd pre-P0 admission-history recovery — 2026-10-16

DESIGN PROPOSAL, NOT ACCEPTED IMPLEMENTATION. Assigned Astra/High; backend unknown. Historical source baseline 8b3acffd4a75d58b7b86bc6baade1e994d3ae5e3; exact source/blob/SHA pins in JSON. Source claims use that historical baseline; JSON records each current-file comparison separately so parent metadata/source advancement is not mistaken for a new audit. Read-only repository; no build/discovery/tests/app/runtime/network/OS investigation/delegation. Only this new /tmp pair is written. Earlier reports remain immutable. All future checks are UNRUN.

## Recommendation and exact limit

The smallest candidate with durable pre-P0 HISTORY uses the existing pathname game_state/control/validation_repair_request.json, but promotes its existing client owner into an explicit VERSIONED admission/repair lifecycle. It must include non-dispatch states, an original-issued seed, preserved owner observations, native transition settlement, Ready consumption and a real worker-transaction companion. This is substantive schema/owner/lifecycle work requiring exact design acceptance; the pathname alone supplies no permission or proof.

Keep final frozen admissionEvidence nested in existing M3 as already proposed. Do not introduce a new history pathname. Do not prepublish M as an informal journal. No source-backed necessity for an additional pathname is established.

No COMPLETE all-cut recovery exists in current code. The proposed protocol can preserve issued history and committed observations, but still needs genuine worker cold-disposition integration, original-publication atomicity, repair-baseline settlement, and a separate decision for lost C/result/identity work. These are explicit engineering blockers below. Do not label retention-only HOLD as full lawful recovery or activate public F1 before they are closed.

## Existing facts that rule out a metadata-only addition

Source paths in this report are under BookOfEternityClient unless stated otherwise.

- GameEngine.SessionAndSnapshots.cs:398–410 owns canonical original preparation; :514–569 creates actual manifest and detached authority, currently writes the two files sequentially under its lease. Snapshot artifacts are written earlier. There is no admission-history seed.
- GameEngine.BrowserPlayerInput.cs:198–227 owns genuine browser staging: provisional request, actual manifest/authority capture, validated binding, actual PendingPlayerActionService staged publication and original seal. Browser preparation has its own transaction/recorder rules; ordinary PublishLocalFiles cannot blindly replace them.
- LiveTurnPreparationService and six manifest DTO consumers must preserve any future original seed field in hash/serialization round trips. Already-issued originals stay immutable.
- ValidationAndRepair.cs:354 samples repairStartedAtUtc BEFORE WaitForContractRepair. The raw loop samples separately at :674. The request's DetectedAtUtc is sampled later at :4078. They are not interchangeable.
- Generic merged repair errors/start/fallback/boundary are locals at :369–389, recomputed at :251–269. Actual freshness predicates are :2688–2784. The policy can depend on missing/error/older-than-start metadata and an actual fallback sample, not only target bytes.
- WriteValidationRepairRequestForSessionAsync :4048 deletes prior request/Ready/stall files; :4149 dispatches worker first; Applied returns at :4153–4163 before writing the legacy request at :4174. Current request presence cannot represent every repair branch.
- WaitForContractRepairAsync :3322 restores the actual preturn baseline before request dispatch for applicable packets. That restoration is owned by SessionAndSnapshots.cs:2091–2127, with a special joint route and ordinary restore plus optional pending checkpoint. The history owner cannot treat “about to restore” as “restored”.
- Ready acceptance at ValidationAndRepair.cs:3558–3567 appends diagnostics, deletes Ready separately and returns; the next caller computes freshness afterward. A crash between these points loses hot observations/continuation.
- Invalid/mismatched Ready paths :3468–3547 can invoke another worker attempt. ReportRejectedRepairReadyAsync :8146–8169 replaces the transport request with Ready errors; it does not carry all earlier canonical repair errors. Persist transport-rewrite lineage separately from the actual owner repair state.
- Generic success :304–308, cancellation :3447, protected refusal and other cleanup paths delete repair files. DeleteValidationRepairFiles :8290–8295 unconditionally deletes Ready/request/stall. They cannot remain unchanged under durable pre-P0 history.
- GmWorkerApplyGate.cs:306 begins the actual whole worker B1 transaction, validates and checks ownership, commits at :375–376, then builds Accepted. Audit follows in ApplyReservedAsync :128–139, AFTER commit. Audit is not an atomic applied proof.
- GmWorkerValidationRepairDelegator.cs:223 invokes the actual apply gate; :255 onward publishes Ready separately. Applied plus failed Ready is a real supported route; failure.Data retains hot task/proposal/decision only, not durable continuation.
- GmWorkerDurableExecution.cs:7–9 explicitly says a replacement process does not mint the retained live authority. GmWorkerTaskRunResult.ExecutionAuthority/HasValidatedExecutionFor (Models.cs:458–468) is not reconstructed by deserializing public DTOs.
- GmWorkerBridgePool.TryReserveTaskAsync :1270–1321 rejects existing task/inbox paths. “RunTaskAsync with the saved task” is not a valid cold resume design.
- Worker task ID allocation occurs in Delegator.cs:417, latest-task write at :428–449, and actual reservation separately in BridgePool.cs:528–568/1270–1321. These cuts must be owned, not hidden by a task-id string in history.
- Daemon Process-RepairRequestCore :6204–6219 treats an existing newly written request file as dispatchable; Read-CorrelatedValidationRepairRequest :4294–4312 accepts tuple correlation. Helper Complete-BoeValidationRepair :3233 onward reads root request fields and writes Ready. A non-dispatch checkpoint cannot be introduced without explicit gates.
- ValidationService.BootstrapAndProtocol.cs:895–1024, PendingTurnSnapshotReader.cs:135–140/712 onward, ProgressionScheduleService.cs:1461 and GuardianPowerEventState.cs:555 have current repair context interpretations. They must not mistake a history-only record for a current GM repair.
- InactiveSnapshotEvidence.cs:176–185 treats any request presence as an original-retention blocker. MortalJointTerminalCleanup.cs:247–253 requires current old request tuple and fullTurnResubmissionRequired=true. SaveLoadService.cs:69–76 excludes this pathname as ephemeral. Existing path reuse therefore still affects cleanup, inactive evidence and archive policy.

These facts establish why the proposed contract is new. They do not prove an extra file is needed.

## Proposed named owners and APIs

Proposed names:
- Core/GameEngine/GameEngine.AdmissionRepairHistory.cs: private IssuedAdmissionOrigin, OwnedAdmissionRepairHistory, live attempt and route transitions.
- Services/AdmissionRepairControlState.cs: strict versioned DATA codec, pure state-transition checker and detached row shapes; no imported capability constructors.
- Services/AdmissionRepairControlReader.cs: actual fs/lease/G/original-bound cold reader; issues distinct lease-local VerifiedRepairHistoryState after independent checks.
- GmWorkers/GmWorkerApplyGate.AdmissionHistory.cs: fixed client-owned companion preparation and readback owned by the real apply gate, not ChangedFiles supplied by the worker.
- Owner-specific original/restore/Ready/common/cleanup adapters in their existing files. No generic storage journal/service or caller-selected path registry.

Private production methods:
PrepareOrigin(actual original preparation) -> IssuedAdmissionOrigin;
PrepareRepair(actual route, complete actual issues/packets/obligations, actual sampled start);
PublishRepairBoundaryAsync(actual lease, prior exact control, prepared transition);
CaptureSubmissionAsync(actual Ready or actual worker-owned result, source session);
RecordObservation(actual validation invocation/role/order and physical observation);
FreezeAdmissionAsync(actual original/read/source closure) -> frozen admissionEvidence;
ReadRecoverableStateAsync(actual fs/lease/original) -> typed phase disposition;
TransferToCommon(actual same candidate/plan/take) -> immutable evidence reference, NOT a new writer;
RetireAfterVerifiedCompletionAsync(actual existing terminal/cleanup owner).

No public methods accepting an upstreamAccepted boolean, an arbitrary evidence map, timestamps chosen by caller, parsed worker execution authority or a fake original seed. Live issuers validate exact source owners; cold issuers verify physical source/owner relations and issue DIFFERENT capabilities from live ones.

The current observed-reader work is a prerequisite for original source capture only; it does not implement this history owner.

## Original-issued durable seed without rewriting originals

For NEW originals participating in this proposed version only, add a closed original field, for example:
admissionHistoryOrigin = {version:1, ownerPolicy:"admission_repair_v1", generation, initialOrdinal:0}.

This exact field name/layout is PROPOSED, not accepted. Include it before manifest hashing and detached authority creation. Original session/request/turn/request timestamp/source label remain their actual existing fields. The seed must be issued by actual original preparation, not added after parsing a current manifest. No new UUID is necessary: structural identity derives from actual G + original tuple.

Avoid a hash cycle: original seed has no control-file hash or final manifest hash; the initial control record references the finished immutable original manifest/authority hashes. Publish original pair + seed control as a declared completion cohort of the ACTUAL original owner, with no GM dispatch until settlement. Previously prepared snapshot files remain subject to that owner's exact artifact proof and cleanup.

Current source does NOT atomically issue this three-member cohort. Ordinary original preparation must acquire a real native/coordinated completion plan. Browser preparation must integrate the companion with its actual preparing/staged transaction and original record/recorder; do not call trusted-local publication through a lease for which UsesTrustedLocalWriter is false. All six manifest readers, actual helper producer and browser staged proof must round-trip the field exactly. Missing seed on an old original stays old dispatch, never upgraded in place.

Cold origin proof checks actual original seed/version, actual G bytes/id, exact tuple/original pair, initial control origin relationship and legitimate current transition chain. Self-hash or a NoPreviousRepair boolean is not origin proof. A missing control on a versioned original is an incomplete/retired phase that needs actual original/terminal proof, not implicit empty history.

## Existing request pathname: explicit closed version and phases

Proposed new discriminator:
{schemaVersion:1, kind:"admission_repair_control", ...}
Existing unversioned request objects remain the OLD path. This is version-first dispatch, not permissive extra-field acceptance. Do not modify the old emitted contract until this complete new lifecycle is accepted and selected by an actually newly issued original.

Proposed closed root:
{schemaVersion,kind,policyVersion,binding,revision,phase,origin,route,sources,
 observations,activeRepair,frozenAdmission,integrity}

binding = actual {generation,sessionId,requestId,snapshotToken,turnNumber,realm,
 originalManifestHash,originalAuthorityHash}.
revision is a strictly increasing owner sequence, distinct from the existing revalidationAttempt. Invalid Ready retries may advance revision while keeping that actual attempt number.

phase is exactly:
original_issued | validating | repair_prepared | restoring |
gm_waiting | worker_reserved | worker_applied | submitted |
admission_frozen | terminal.

No completed/validated boolean. Every phase has a strict required/null field table enforced by the codec and pure transition checker. These names define a proposed state machine; they do not grant phase authority on parse.

activeRepair is null until an actual repair. Otherwise it contains:
{ownerRoute,attemptOrdinal,revalidationAttempt,repairStartedObservation,
 ownerIssueRefs,canonicalHistoryRefs,retryBasisRefs,transportRevision,
 delivery,requestPayload,submission}.

- ownerRoute is an actual closed route identity from the named caller, not any string caller can claim. It covers raw, baseline, post-seal/pending, canonical/general repair and exact special continuation owners when they become qualified.
- requestPayload is the complete EXISTING GM request payload built by current owner logic, retaining packet fields, error order, diagnostic meaning, fullTurnResubmissionRequired, required paths and real DetectedAtUtc. It is null in history-only phases; no fake empty GameResponse/request.
- delivery is none | gm | worker. Only phase gm_waiting with delivery=gm dispatches a GM repair. worker_reserved/worker_applied/original_issued/validating/admission_frozen do not.
- submission is null, ordinary Ready evidence, or actual worker-applied evidence, never a fabricated Ready.
- ownerIssueRefs preserve real original repair issue/context/packet facts. Rejected Ready transport errors do not erase them.
- retryBasisRefs preserve actual specialized obligations/source checkpoints needed to reconstruct the correct retry authority; serialized obligation data alone cannot issue their old live capabilities.
- exact input/output source image roles remain production-derived; no caller path imports.

route/observations are ordered typed rows. Each row records actual owner operation and consumed source relationships; all samples carry purpose and invocation ordinal. Full generic history includes actual merged canonical issue state and exact start/fallback provenance, not only the last visible request. Receipt/event IDs already issued by owners are retained; no replacement IDs are manufactured cold.

The actual turn may have arbitrarily many repair attempts under current policy. Do not impose a new retry limit or discard relevant earlier history to bound the file. Initial implementation may retain the complete finite history of the actual run; it must account for size/cost. A future compaction needs a proved policy-sufficient owner representation and independent checker; a saved “all earlier repairs checked” assertion or predecessor hash alone is insufficient.

## Transition and publication contract

Every transition uses an actual owner-prepared before/after record with exact G/original/source guards and existing native/coordinated atomic settlement. It is not a generic callback accepting desired JSON.

1. Original issued -> validating:
same actual original seed/control; only an actual validation pass can enter. Persist no synthetic upstream acceptance.

2. Actual repair start:
record repairStartedAtUtc at the EXISTING caller sampling point. Persist a repair_prepared transition BEFORE rollback, worker launch, GM request exposure or Ready consumption can make that start causally relevant. Later DetectedAtUtc remains separately sampled at its current request construction point. Additional IO may change elapsed wall time, but it must not substitute one clock role for another.

3. Repair baseline restoration:
restoring names the exact already-authorized restore owner, original/rollback sources and any private pending checkpoint. Its completed transition must be committed with the actual restore result, or recoverable from that exact owner's durable outcome/physical full-cohort proof. The present ordinary restore + optional pending write is not already such a combined history receipt. Never repeat a possibly completed restore because a history acknowledgement is absent. Joint specialized checkpoint/request preservation remains owned by its actual restoration branch.

4. GM dispatch:
same batch installs gm_waiting plus complete requestPayload and consumes only the prior owned Ready/stall evidence authorized by that transition. Daemon/helper version-first gates dispatch only this state. Do not delete the entire history on a request rewrite. Invalid Ready produces a new transport revision retaining the actual repair lineage and correct owner error basis.

5. GM submission:
read/validate actual Ready correlation and actual resubmission sources through live owners. Commit submitted + retained exact Ready evidence + necessary source capture in the SAME batch that consumes that exact Ready. On crash before commit, Ready remains owned input; after commit, submitted is the durable continuation. Do not accept a Ready whose data was never owner-validated, and do not infer submitted from an absent Ready.

6. Observation recording:
each actual metadata/UTC/volatile comparison observation becomes durable before the owner advances a stage that depends on it or destroys its supporting source. Preserve actual target selection, error handling, source path normalization, clock role, invocation order and max/fallback arithmetic. Store exact source bytes/absence and actual observed mtime/read-error category where relevant. Frozen validation UTC is not game time or elapsed action budget.

7. Admission freeze:
after all actual source/semantic checks, persist admission_frozen with exact immutable admissionEvidence and its source closure. No C reduction or accepted publication is authorized by this phase. On reentry, use frozen observation inputs and pure checkers; no resampling/allocator/source-work replay.

8. Common transfer:
prefer leaving the frozen control UNCHANGED as a sealed before-image/guard. The existing same-candidate common publisher includes admissionEvidence in M3, commits actual common+M3, and full readback checks the frozen control relation. A separate “transferred” control write is unnecessary: verified M3 itself establishes the owning handoff. The control may remain admission_frozen until the real existing terminal cleanup retires it. This avoids an extra common write member and a split marker/control acknowledgement.
All source/control guards and nested evidence must enter before common input/prepared/cache seals. No after-take evidence attachment. If a design instead mutates this control in the common batch, that exact member/grant must be accepted and included before preparation/seals; existing pathname alone does not authorize expansion.

9. Terminal/cleanup:
generic DeleteValidationRepairFiles must dispatch by version. Old behavior stays old; new history survives while any repair/native/common/worker obligation needs it. Only actual terminal rollback/closure or verified common/cleanup ownership may delete it. Cancellation/protected refusal must settle their real restore/publication first; no unconditional cleanup as proof. Existing inactive-retention blocker remains conservative; final retirement must update it coherently rather than bypass it.

## Worker: required real companion, not post-return bookkeeping

Actual companion pathname is the SAME game_state/control/validation_repair_request.json. It is CLIENT-owned; workers cannot list it in proposal ChangedFiles or expand task permissions to author it.

A proposed ApplyGate-owned PrepareAdmissionHistoryCompanionAsync derives its exact before/after from the actual reserved task, genuine live execution result, current history owner, proposal/scoped baselines and actual current G/original. It is prepared BEFORE BeginWorkerApplyTransactionAsync and participates in the SAME B1 intended set as every worker changed member.

- Scope and proposal validation still concern the actual worker-controlled members. The fixed client companion is supplied only by the real apply owner and checked separately, never accepted from proposal input.
- The companion describes the intended worker_applied state and exact task/proposal/source/after-image relation. Its physical staging is NOT proof of acceptance while B1 decision is pending.
- Existing apply validation/ownership checks run; only their success commits the B1 decision. Failure rolls back both GM members and the control companion. No post-commit append tries to establish that the application happened.
- A valid worker result with zero GM file changes still needs a genuine control transition. Current BeginWorkerApplyTransaction requires a nonempty set and current apply code skips it for zero changes. The fixed client companion can make this transition nonempty, but this is an explicit apply-owner extension requiring scope/validation review.
- Normal Ready publication remains separate transport. It may fail without undoing committed worker_applied. Cold sees the exact committed control+worker output relation and revalidates the current submitted source; it never retries the apply or invents a Ready.
- Audit remains diagnostic. It can be absent after a committed worker operation without losing lineage, because the actual native companion is the retained fact.

The worker hot success gate must remain genuine. For cold verification of an ALREADY applied result, the new owner receipt establishes the actual application observation; cold independently checks exact task/proposal/context/scope/after-image relationship and the main submitted-source semantics. It does not create GmWorkerExecutionAuthority, rerun worker apply validation as a writer, or import the saved ApplyGateDecision as a live permit.

Worker pre-apply cuts remain a separate exact obligation:
- task preparation/ID/latest-task write/reservation/launch/proposal publication are currently separate;
- the history must bind the actual task reservation BEFORE launch, with atomic or actual durable-owner-correlated transition;
- a replacement process cannot mint the old live ExecutionAuthority or re-reserve an existing task;
- existing worker root/ledger recovery must first settle/retire its actual execution and output-publication ownership.
A cold published-but-unapplied proposal may be applied only through an expressly defined genuine cold apply admission, or retired without application followed by the existing lawful repair fallback under a NEW actual repair owner. Neither route is proved by current source. Do not choose rerun/re-reserve/DTO authority implicitly.

This is an OPEN worker-owner contract, not a reason to add another history filename. Any fallback must preserve the same player action/turn, error/packet semantics and already committed effects; it cannot silently discard a lawful accepted repair or deliver duplicate work.

## Version-first cold reader

ReadRecoverableAdmissionRepairStateAsync(files, actual lease):
1. Let genuine native/main/worker recovery owners settle known debt; do not run canonical/source validation while a pending decision is unresolved.
2. Read actual existing G, original pair and current request/control bytes. Old unversioned request -> existing old route, no history authority. New discriminator -> strict closed parser; unknown/missing required fields -> retained conflict, no fallback upgrade.
3. Verify actual new original-issued seed, tuple/G/original hashes, source label and exact control origin. No seed mint from absence/current Ready and no rewriting originals.
4. Verify exact ordered transition grammar, current phase and source roles. Compare actual physical controls, Ready or worker task/proposal/outcome artifacts as applicable. Derive retries/merged canonical error applicability from actual rows and shared policy, never a serialized validated/no-previous-repair flag.
5. Recompute all PURE classification/freshness/comparison consequences from independently bound sources and recorded actual owner observations. Historical clocks/read failures are trusted named-owner observations within the existing trusted-local model; they are not claimed current metadata and hashes do not replace semantic verification.
6. Verify specialized restore/pending/worker/common owner relations. A parsed receipt with plausible fields is not a live reserve/take/release capability.
7. If full verified M3 exists, select its retained P0 path; do not repeat pre-P0 work. If control is admission_frozen and no attempted common debt exists, issue a distinct cold source-history capability only after complete source proof and actual no-publication determination. It is not a deserialized C candidate.
8. Recheck G/original/control/required cohort before returning the typed disposition. No new UTC, scheduler, source reduction or identity allocation in proof.

Expected result variants are phase-specific: Legacy, OriginalReady, RepairDispatchReady, WaitingForGm, WorkerRecoveryRequired, SubmittedSourceReady, FrozenAdmissionReady, CommonP0Verified, TerminalSettlementRequired, NativeDebt, SourceConflict, MissingOwnerProof. These are proposed names, not proof that every resume path exists.

## Which crash cuts are recoverable

“After extension” below means only after the stated real owner protocol is implemented and qualified. Current source supplies no durable admission-history guarantee.

| Cut | Sound action after extension | Remaining condition |
|---|---|---|
| Before original completion batch | Actual original owner resumes/rolls back preparation; no emitted turn authority | Three-member seed completion and browser adapter must exist |
| Original completion committed, reply lost | Fresh original/control reader reconstructs issued origin without resampling request/seed IDs | Exact original/native settlement proof |
| Repair start committed, before restore/dispatch | Reuse stored actual start and original issues; continue named stage | No earlier attempted restore/worker debt |
| Restore attempted, ack missing | Recover actual restore decision and full pending/source cohort | Existing restore-history atomic/semantic adapter is OPEN |
| gm_waiting committed | Resume waiting for same request/revision, no new action budget | Daemon/helper/all consumers honor non-dispatch states |
| Ready exists, submission consume not committed | Validate actual Ready/source; exact atomic transition may run once | No inference from stale/foreign Ready |
| submitted committed, reply lost | Resume subsequent pure admission stages using retained source/observations | Complete source relation and owner stage cursor |
| Worker task reserved/launched | Enter genuine worker recovery, never re-reserve saved ID | Cold execution disposition is OPEN |
| Worker proposal published, apply not begun | Preserve actual outputs; genuine cold admission or proven retirement/fallback | No current cold live-authority issuer; OPEN |
| Worker B1 pending | Existing B1 recovery decides entire outputs+history companion together | Companion must be inside initial intended set |
| Worker B1 committed, before audit/Ready | Verify durable worker_applied relation; do not reapply; revalidate submission | New apply outcome semantic verifier required |
| Completed observation durably recorded | Reuse exact sample/metadata/volatile inputs and pure checker | Immutable source bindings and stage coverage |
| Observation computed only in memory | No claim it was completed/accepted; phase still at prior durable cursor | See lost-work distinction below; no invented sample |
| admission_frozen committed, before C | Reconstruct verified admission source, then follow actual new owned calculation admission | Actual no-common-publication/debt proof |
| C completed hot, before durable whole result | History alone cannot reconstruct C or allocated IDs | Separate candidate/result recovery contract OPEN |
| Common native attempt/commit | Existing common/native owner decides full cohort+M3; cold verifies actual complete P0 | No blanket retry/empty-marker inference |
| Cleanup/terminal attempt | Actual terminal/native outcome decides retirement | All required original/worker/source proof retained until closure |

## Irreducible lost-work distinction

Do not claim “all instructions before P0 execute exactly once across crashes”. A value sampled or identity allocated in memory can be lost before any durable write. Adding a pathname cannot close that interval.

The protocol can guarantee:
- no external dependent mutation/dispatch/consumption happens before its prerequisite observation/state is durably committed;
- completed durable observations and owner effects are reused without replay;
- incomplete observational work grants no admission success.

To resume an unfinished pure validation pass, the design must explicitly distinguish uncommitted observation work from completed owner work. Repeating an unfinished read-only validation pass may be a legitimate new validation attempt ONLY if its sources, policy and no-publication status are genuinely established and no frozen sample or source mechanic is overwritten. It must not be called replay of the original historical observation. This choice is NOT silently accepted here: retaining the exact sample without reexecution requires persisting it before any dependent stage, or a deterministic owner reservation of that sample.

The same limitation is stronger for C: hot C can contain genuine allocated identities and completed mechanics. A durable admission-history file is not a serialized privately owned candidate or a cold result verifier. To recover without rerunning reduction/allocations, the actual C owner must durably preserve its prepared result/identity commitments under an accepted owner protocol and cold semantic checker, or the contract must expressly allow abandoning an uncommitted attempt with no effects while keeping correct identity/source semantics. Neither is solved here, and neither may be assumed from the existing request pathname.

Likewise an absent M is not proof that publication was never attempted. Actual native debt/result/owner phase must be inspected. Do not let a new history revision rearm a foreign or uncertain reservation/take.

## Alternatives and acceptance boundaries

A. Recommended candidate: versioned existing request-control lifecycle + genuine new original seed + actual worker companion; final evidence in existing M3. No extra pathname, but all listed schema/publisher/dispatch/recovery changes require exact acceptance. This is the smallest coherent HISTORICAL observation route found.

B. Preserve old request emission semantics, seed only in new original, final history only in M3: maintains current dispatch surfaces but cannot recover lost later repair observations before P0. Sound bounded internal HOLD only, not full lawful recovery.

C. Store evolving history inside already-issued original: rejected. It destroys immutable original/source meaning. A NEW original version can contain the initial seed but cannot become a mutable historical ledger.

D. Earlier staging state in existing M: avoids repurposing request transport but changes M's before-common lifecycle and requires a new explicit phase owner, cold dispatch and grant. No side-M write or use of completed v3 as a staging marker. Not necessary unless existing-control extension is concretely rejected.

E. Existing worker task/audit files alone: insufficient across no-worker/GM routes and audit-after-commit gap; live execution authority cannot be recreated from them.

F. Additional history pathname: offers no stronger semantic proof and does not solve worker/result/clock cuts. It would need a separate membership/retirement/dispatch design and explicit grant. No necessity is proved, so do not select it by default.

The critical proposed changes are NOT authorized merely because D11 is broadly staged or a pathname already exists. The coordinator must review the concrete original seed/control lifecycle, worker companion and cold phase semantics as one proposal. No new permission question is posed by this consultation; it supplies the reviewable design and exact open decisions.

## Consumer and documentation impact

All uses of validation_repair_request must be assigned one explicit purpose: GM dispatch, actual current repair correlation, original retention, cleanup, archive exclusion, or frozen source. Update those consumers to version-first parsing; a history-only record is never an ordinary repair request.

Source families found:
- daemon Read-CorrelatedValidationRepairRequest/Process-RepairRequestCore/watch correlation/stall watchers;
- Launcher/GM_Turn_Helper.ps1 Get/Complete repair paths and client-owned-write guards; generated launch instructions;
- engine Wait/Write/Delete/Ready rejection/cancellation/terminal and original preparation/browser/late recovery;
- PendingTurnSnapshotReader, ValidationService WorldReadPhase/BootstrapAndProtocol/accepted output and spiritual capture/continuation, GuardianPowerEventState, ProgressionScheduleService and MortalJoint input/basis readers;
- CanonicalStateNormalizer soul/meta and afterlife entity current-context readers;
- BrowserAfterlifeTurnRequestQueue, InactiveSnapshotEvidence, MortalJointTerminalCleanup and SaveLoadService.

Do not make a history-only record metadataDiagnosticOnly=true to hide it: that flag has an existing sentinel/diagnostic meaning. Use the explicit new discriminator and delivery phase. Old unversioned behavior remains exact.

Archive note: current SaveLoadService excludes both original and request evidence. This proposal concerns same-root process-crash recovery. It does NOT qualify in-flight distinct-root export/import; that remains its own open owner contract. Do not quietly add history to ordinary saves without its complete original/G/worker cohort.

Same-change GM/documentation/example/source-guard updates required if emitted:
TaskGuides/CLI_Step_Main.txt repair/full-resubmission rules;
generated game_state/control/gm_context_pack/Templates/VALIDATION_REPAIR_TEMPLATE.md and OUTPUT_ARTIFACT_REPAIR_TEMPLATE.md, whose tracked producer is BookOfEternityClient/game_master_daemon.ps1:1629 and :1487;
Examples/E_CLI_Step_Main.txt current request/Ready examples;
OtherGuides/GM_Worker_Bridges.md worker Applied/Ready-failure behavior;
Examples/example_validation_manifest.json;
launcher/daemon prompt sources and helper API docs/source guards;
OtherGuides/Afterlife_Contract_Matrix.md, Afterlife_Pending_Control_Surface_Inventory.json, Examples/E_CLI_Afterlife_Turns.txt and owning afterlife documentation/example tests for shared repair changes.
Worked examples must show history-only idle state causing NO GM dispatch, ordinary GM repair, rejected Ready retaining owner history, worker apply committed with Ready failure, and final M3 handoff. GM cannot author the history/observation fields.
Internal proposal/reader extraction alone has no emitted contract change; that limited no-update rationale does not cover this future durable protocol.

## Finite future verification matrix — ALL UNRUN

1. Actual console and browser new-original producer: seed/control completion cuts, unchanged old originals and six DTO round trips.
2. No canonical repair history versus real severity-only correction, mixed raw and generic canonical histories; exact applicability/merge/error order.
3. Different repairStarted/Detected/Ready timestamps; missing target, metadata error, too-old target, boundary equality, real fallback and output/source rewrites.
4. Multiple attempts and same-attempt rejected Ready transport revisions retain exact original owner issues/packets.
5. Same-source repair resubmission, foreign/stale Ready, diagnostic-only original loss and source/G replacement.
6. Restore/pending-checkpoint atomic cuts; no repeated destructive restore on lost ack.
7. Worker disabled/skipped/failure/timeout/rejected paths preserve actual fallback and no simultaneous GM/worker writer.
8. Worker reservation/launch/proposal publication cold cuts; no live authority import or duplicate task reservation.
9. Worker apply zero/nonzero changes, validation failure rollback, commit before audit/Ready, failed/uncertain Ready and same exact control companion.
10. Daemon/helper request-only dispatch; no loop on history writes, no forged request/Ready or client-history mutation.
11. Freeze/reentry without new clocks/IDs; source changed after observation/freeze; semantic tamper with recomputed hashes still rejected.
12. Same genuine C/candidate/owner fences; separately prove lost-C strategy before full pre-P0 recovery claim.
13. Common+M3 native cuts with unchanged frozen control guard and full cold source/cohort verification; no side-M/control ack.
14. Success/cancel/terminal/cleanup cuts retain evidence until actual owner closure; inactive-retirement behavior.
15. Actual platform storage admission, browser/worker fences, bounded selection/evidence review. No aggregate runs or Windows/visual claims from source/component checks.

## Open prerequisites: do not call this complete recovery

R01 Exact new original seed plus ordinary/browser preparation completion owner.
R02 Exact closed control schema/phase transition codecs and version-first dispatch in ALL consumers.
R03 Actual restore/pending checkpoint settlement relation.
R04 Actual worker task/cold execution disposition and same-B1 fixed client companion, including zero changes.
R05 Complete pure output/freshness/comparison/source and worker outcome relation checkers.
R06 Exact uncommitted-observation and lost-C/result/identity retry-or-preservation policy, with no accepted work replay.
R07 Actual no-publication/attempt/debt classification and common transfer/cleanup evidence.
R08 Same-change GM/Mortal/afterlife docs/examples/guards and independent whole-proposal acceptance.

Existing storage names make this proposal possible in principle, not implemented or already granted. No additional pathname or F2 is selected. No legal-outcome narrowing, T197 implementation or new QTE/player activation. Full F1/T179/T192, cold P0 and D4/guided/visual/Windows/D8 gates remain open.

FINALSTOP.
