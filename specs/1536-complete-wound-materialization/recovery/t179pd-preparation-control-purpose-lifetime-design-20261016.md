# T179-Pd / R01P01: existing repair-control purpose and lifetime

Status: **DESIGN ONLY; no engineering or activation release.** Frozen source: `a85a726229e74465f986af69ca00a8e0225c0dea`. Consultation requested as Astra/High; actual serving backend is not observable. Repository read-only; only this new /tmp pair is produced. No application, build, discovery, test, network, or delegated work was run. Every proposed check below is **FUTURE / UNRUN**.

## Ruling and scope

Using the existing `game_state/control/validation_repair_request.json` for a preparation-only variant is feasible as a **proposed purpose contract**, but not by retaining the file inside the current repair cleanup helpers. The current name means transport request, current readers interpret raw presence or a root tuple as an active repair, and current cleanup assumes an original plus turn_complete. Those assumptions conflict directly with a record that must precede original issuance. The correction must change the participating consumers together before any such record is emitted.

Recommend a strict closed union at the existing path, with authenticated-original obligations selected **before** request-kind parsing, phase-specific projections, and named owner transitions. This closes the P01 *deletion/dispatch design contradiction*: preparation/history does not dispatch, does not feed tuple fallback, does not satisfy Ready, and cannot be erased by ordinary repair cleanup. It does **not** establish that preparation can already resume cold, nor complete original issuance, worker history, restore, or abort. Those exact owner protocols remain prerequisites below. A retained conflict is containment, never a claimed lawful resolution of all future ordinary outcomes.

No new durable pathname is proposed. Keep prior immutable reports and PH-D01 correction unchanged. This is an explicit proposed refinement to their outer control contract, not a retroactive declaration that their nested producer schemas or existing runtime already implement it. Existing v2 emitted bytes and ordinary/helper/afterlife behavior stay unchanged until a coherent versioned implementation is accepted; no default ACTIVE route or new QTE offer follows from this consultation.

## Source facts establishing the conflict

- Entry health invokes orphan cleanup before original creation; raw repair/Ready presence can retain an input request (VR145–197). Successful zero-error validation deletes repair files (VR294–311). The shared deletion routine removes Ready, request and stall separately (VR8249–8295).
- Startup normalization deletes the request when original is missing/unusable, or when turn_complete is missing/mismatched (SS1237–1312). Those are normal intermediate states for proposed preparation or `original_issued`, so an unconditional call is incompatible.
- Pending-turn and gameplay consumers resolve root tuples in different orders; some choose input first and therefore never notice a conflicting control. PendingTurnSnapshotReader instead authenticates original before all contexts, giving a real acyclic seam for purpose dispatch (READER204–238,260–310,732–823). An invalid required new control must not disappear through fallback.
- Generic repair tries a worker before publishing the legacy request (VR4145–4178). Worker application commits before its returned decision/audit and before later Ready (GATE350–387,128–130; WORK502–570). Spiritual repair uses a different real owner and writes its transport before worker (SP540–605).
- Daemon repair processing consumes LastRepairRequestWrite before its diagnostic check (DAEMON6204–6220,6335). Turn processing can mark an unstaged source stale before incrementing TurnCount/starting the turn (DAEMON5840–5864). Both are wrong places to discover a nondispatchable preparation.
- Save archive construction excludes this path as ephemeral (SAVE69–99,551–574), while shared save preparation validates the resource quartet, not a preparation-history owner (SAVE159–195). Browser admission already blocks actual pending actions and cold preparing/native phases; this does not prove every shared save caller rejects a control-only pending history.
- Rotation-only session replacement retains the request while deleting latest worker task/Ready and changing G (SR24–110). Retained bytes would no longer carry a valid new generation-bound owner. Clear/load are actual native complete-cohort operations, but their new-owner settlement proof is not yet implemented.

Aliases and exact blob/SHA-256 pins are in the companion JSON; all ranges refer to the frozen commit, not moving HEAD. The consumer registry below follows the actual paths and indirect callers; literal matches are used as coverage leads, not as proof of semantic closure.

## Proposed closed discriminator and original-first precedence

Do not add a caller-controlled `purpose` string to the old request. The only accepted new outer discriminators are the exact pairs below. All new roots reject unknown fields, duplicate/case-confusable fields, invalid scalar types and unsupported versions before any legacy tuple fallback. JSON parsing is not an authority issuer.

| Wire form | Interpretation | Producer |
|---|---|---|
| Existing unversioned ValidationRepairRequest, including genuine diagnostic-only and spiritual payloads | Legacy transport, **only when the actual original/entry owner admits the existing contract** | Existing genuine legacy repair owners |
| schemaVersion=1, kind=ordinary_original_preparation | P: unissued original preparation | New private ordinary preparation owner attached to actual console/browser Stage entry |
| schemaVersion=1, kind=admission_repair_control | A: issued-original admission/repair history, optionally a GM transport projection | Same genuine original/repair owner, after atomic original issuance |
| Anything else or mixed legacy root tuple plus new envelope | Conflict / unsupported control | No producer may silently normalize or upgrade it |

The two new schemas are proposals, not claims of current emitted support. The exact existing request payload remains nested in A. **No top-level legacy sessionId/requestId/turnNumber, metadataDiagnosticOnly shim, or synthetic sentinel on P/A.** That prevents accidental tuple reuse but is not sufficient protection: daemon defaults and presence predicates still require explicit purpose checks.

Order for every new-aware consumer:

1. Enter its actual current owner/G/lease, honor native recovery and browser admission first. An actual pending publication gets its owning disposition before ordinary parsing; a caller record cannot manufacture that disposition.
2. Obtain actual authenticated original protocol facts using the real original validation contract (including detached authority and required signed/source checks). This stage precedes current-context selection and does not issue full snapshot/admission authority. Do not recurse through the whole current reader to classify that same reader.
3. Read the exact control presence/bytes in that same source scope. Its discriminator selects a parser only; the original determines required obligations. Recheck the full observed source before a result is used for mutation/dispatch.
4. Apply the table. Presence, a valid tuple, an envelope selfhash, Ready, or a parsed terminal flag never supersedes it.

| Actual original/owner state | Control | Required result |
|---|---|---|
| Supported new authenticated original requiring seed/history | Matching valid A with exact original/G binding and allowed owner transition | Phase projection, still subject to real owner/closure checks |
| Same new original | Missing, legacy, P, unknown, malformed, wrong-G or foreign A | MissingOwnerProof/Conflict; never Legacy |
| Supported old ordinary/helper/spiritual original | Existing compatible unversioned request or absent old-optional transport | Exact existing legacy route; helper/spiritual source is not upgraded |
| Same old original | P or A | Conflict; no post-issue seed rewrite |
| No original, actual private unissued entry owner (or independently qualified cold preparation owner) | Matching P | Preparation-only recognition; no successful original/admission/common authority |
| No original and no qualifying owner | Parseable P | RetainedPreparation/Conflict diagnostic only; cannot advance, abort, clean or dispatch based on parse |
| No original | A | Conflict unless a genuine terminal/native settlement proves retirement; do not infer original was never issued |
| Unknown/invalid original authority/version | Any request | Original/owner conflict; discriminator cannot downgrade it |
| No original | Legacy bytes | Not a usable pending repair context. Existing legacy cleanup requires its real legacy operation/retirement scope; absence alone cannot prove a new seeded history never existed |
| No original, no control | Actual fresh Stage admission | Potential new entry only after existing entry/source/pending checks; absence is not a NoPreviousRepair receipt |

The penultimate row is a real cold-cleanup frontier: current SS1250–1260 deletes unconditionally; this proposal intentionally does not rename that behavior a proof. Preserve it only inside a demonstrably legacy owner contour. A new-aware cold route without that proof must retain conflict until its actual entry/retirement protocol is closed. This is not accepted permanent HOLD recovery and not permission to narrow all lawful F1 histories.

## Exact outer fields and phase/null contract

All listed fields are required; null is explicit only where the table or the phase-specific payload rule allows it. Enum values are closed and ordinal; integral counters must be valid nonnegative owner counters. All exact source images preserve bytes/presence, not reserialized equivalence. No new action/tick/domain identity/time sample is introduced by this schema.

**P root** (retains early R01 proposal):
```text
schemaVersion, kind, policyVersion,
binding, intent, phase, cursor, entry, completed, pending, abort, integrity
```
`schemaVersion=1, kind="ordinary_original_preparation", policyVersion=1`.
Binding exact keys: `generation, sessionId, nextTurnNumber, sourceLabel, sourceRealm, producer, browser`.
Producer is console or browser, selected by genuine entry; source eligibility is actual ordinary Mortal GameEngine. Browser is null for console; otherwise the actual `actionId, source, submittedAtUtc, queuedJson, expectedPendingJson, mainCondition`, never synthesized. No requestId is invented before the real TurnRequest initializer. Intent has `playerAction, extraSystemReminder, playerFacingTurn, waitingTitle, waitingText` with the already-specified explicit optional nulls. Entry retains `turnRequestBefore, originalManifestBefore, originalAuthorityBefore, controlBefore, pendingDiceBefore, terminalSignalsBefore, localRollback, sourceBinding`. Existing actual owner must issue sourceBinding/localRollback evidence; the parser cannot import them as capability. Exact image is `present, bytesBase64, sha256, length`; absent => bytes/hash null, length zero. Present uses canonical padded RFC4648 base64, nonnegative exact byte length and uppercase 64-hex SHA256; empty present differs from absent.

| P phase | pending | abort | Allowed projection |
|---|---|---|---|
| intent | null | null | Retained unissued intent, never GM input/repair |
| step_prepared | exact named step publication proposal | null | Native owner must settle decision before continuation |
| step_committed | null | null | Actual returned result/effect receipt plus cursor in completed |
| issue_prepared | exact original-issue native proposal | null | Owner settles original+authority+P→A decision; no dispatch |
| abort_prepared | null | exact lawful abort proposal | Owner must settle actual effects/intent; not blanket rollback |
| aborted | null | genuine completed abort disposition | Eligible for retirement only via actual owner/native proof |

Cursor/completed must agree with the fixed preparation graph and actual returned values. The QTE reminder step cannot become committed just because runtime bytes changed; its result, effect and cursor need the proposed same native decision. There is **no P issued phase**. P atomically becomes A with real original manifest+detached authority. The prior preparation graph/receipt bodies remain R01P02/P03 prerequisites; this table is not a new receipt mint.

**A root**, proposed explicit refinement to the prior history envelope:
```text
schemaVersion, kind, policyVersion, binding, revision, phase,
origin, requestStage, route, sources, observations,
activeRepair, frozenAdmission, settlement, integrity
```
`schemaVersion=1, kind="admission_repair_control", policyVersion=1`.
Binding exact keys remain `generation, sessionId, requestId, snapshotToken, turnNumber, realm, originalManifestHash, originalAuthorityHash`. Exact source label/producer/seed relations are checked against genuine origin and authenticated original; not accepted from an arbitrary external binding. The new required nullable keys `requestStage` and `settlement`, and phase `turn_staged`, close the previously implicit staging/retirement distinctions before first emission. No existing issued original or historical version is rewritten to add them. Any future emitted change after version 1 would need explicit new dispatch, never stale upgrade.

`requestStage` is null before final request staging; afterwards it carries exact final request image and the real native staging disposition binding, plus browser staged/pending owner evidence or explicit null for console. Its schema must preserve actual final request bytes/defaults/action/dice/time, not regenerate them. `settlement` is null until a real terminal owner supplies exact disposition and native/common/restore proof reference. These are **required owner codec prerequisites**, not caller-importable proof objects. Exact body encoding belongs with those owners; outer nullability alone does not close it.

`activeRepair`, when nonnull, keeps the prior exact keys:
`ownerRoute, attemptOrdinal, revalidationAttempt, repairStartedObservation, ownerIssueRefs, canonicalHistoryRefs, retryBasisRefs, transportRevision, delivery, requestPayload, submission`.
Delivery is none/gm/worker. requestPayload is explicit null until the actual packet owner constructs it (repair_prepared/restoring may precede that construction); once constructed it is the complete genuine existing request structure, preserving all guards/packets/DetectedAtUtc/full-resubmission/error ordering; no new reward/XP/level/time policy. submission is null in repair_prepared/restoring/gm_waiting/worker_reserved; worker_applied requires the genuine worker commit disposition and submitted requires the actual source-bound submitted Ready/worker evidence. No empty fabricated response. gm_waiting and worker_reserved/worker_applied/submitted require a real requestPayload; null is never filled with defaults or a new DetectedAtUtc during cold checking. A repair_prepared/restoring payload, if already constructed, is retained exactly; its presence must agree with the actual ordered construction observation. A retains completed route history before clearing/changing activeRepair. revision/transportRevision record actual transitions, not extra turn/attempt budget.

| A phase | requestStage | activeRepair / delivery | frozenAdmission | settlement | Projection |
|---|---|---|---|---|---|
| original_issued | null | null | null | null | History only; original exists, request not yet staged |
| turn_staged | required | null | null | null | Original turn dispatch eligible through actual staging owner, not repair |
| validating | required | null | null | null | History only; prior completed repair remains in route |
| repair_prepared | required | required / none | null | null | Prepared route; neither GM dispatch nor Ready acceptance |
| restoring | required | required / none | null | null | Genuine restore in progress, native disposition first |
| gm_waiting | required | required / gm | null | null | Only A repair-dispatch projection |
| worker_reserved | required | required / worker | null | null | Worker reservation owner only, no GM repair |
| worker_applied | required | required / worker | null | null | Actual committed worker disposition, not returned Applied boolean |
| submitted | required | required / gm or worker | null | null | Genuine submitted source awaiting actual validation; not accepted |
| admission_frozen | required | null | required | null | Genuine closed admission transferred toward same M3; no GM dispatch |
| terminal | required | null | allowed only with corresponding accepted settlement, otherwise null | required | Actual terminal owner disposition; eligible retirement only after proof |

`original_issued → turn_staged` must atomically join final input plus A stage proof and actual browser pending transition where applicable; if an implementation instead issues original and stages request in one genuine atomic operation, it can directly emit turn_staged with that actual proof. The record cannot claim staging merely because the input file happens to exist. `terminal` is not a license to manufacture a proof after deleting original: any terminal evidence must be fixed by the actual terminal owner before retirement, with same-native settlement or a separately qualified durable terminal receipt. All partial cuts must retain a real proof, not a temporary unowned gap.

## Concrete ownership interface, not a universal wrapper

Suggested new internal files are `Core/GameEngine/GameEngine.OrdinaryPreparationControl.cs` for orchestration, `Services/ValidationRepairControlCodec.cs` for pure strict wire decoding, and a fixed owner component `Core/ValidationRepairControlOwner.cs` integrated with actual original/repair/native owners. Names are proposals. The pure decoder returns parsed data only; it cannot issue a private owner, ReadAuthority, CommonAuthority, reservation or acceptance.

An owner-created `ObservedRepairControlView` carries exact P/A/legacy classification, original contract facts, current G and full source witness/fault state. Construction is private to actual FS/original owner under its real lease. No public `Read(Func...)`, caller path maps, filesystem overlay, caller accepted flag, arbitrary purpose setter or time delegate. Readers already holding a lease reuse it. Bound validation readers keep their existing source session. Every repeated read is reconciled and final closure validated; read/type/metadata/source faults cannot fall back to legacy.

Named operations, with genuine private inputs:
- `BeginOrdinaryPreparationAsync`: actual admitted console/browser entry, source/intent/beforeimages, same G; no generic helper original.
- `PublishPreparationStepAsync`: actual named step's result/effect/native proposal; binds cursor and exact control beforeimage before prepare, including zero-write result cases. This is not a replay scheduler.
- `IssuePreparedOriginalAsync`: actual original issuer candidate, signed/source snapshots, detached authority and control P→A in same native decision; browser specialized owner remains producer.
- `StageIssuedOriginalRequestAsync`: actual finalized TurnRequest and browser owner; exact input+A requestStage+browser state cohort.
- `BeginOriginalValidationAsync`, `PrepareOriginalRepairAsync`, `PublishOriginalGmRepairAsync`: genuine validation result/repair packet/route; no broad desired JSON parameter.
- `BindOriginalWorkerRepairAsync`: actual task reservation before native prepare/plan/cache, plus fixed existing-path companion. No post-return audit lineage. This operation is an explicit R04 prerequisite.
- `AcceptOriginalRepairSubmissionAsync`: actual source-bound Ready or actual worker outcome; enters submitted, never accepted mechanics.
- `FreezeOriginalAdmissionAsync`: future genuine full source/semantic admission owner, not a parsed validated bit.
- `SettleOriginalControlAsync` / `RetireSettledControlAsync`: actual common/terminal/restore owner with proof; exact cleanup membership fixed before native seal.
- `RetireLegacyRepairArtifactsAsync`: genuine existing legacy cleanup scope and exact source images. It cannot accept a P/A or a seeded original with missing control.
- `CheckControlBeforeSessionReplacementAsync`: actual replacement owner; decides existing settled legacy path or requires new-owner settlement, never silently changes binding G.

Each transition checks expected exact control+original+source bytes and actual G, prepares all affected members before sealing, uses the real native owner decision, then returns a result only after readback. Publication uncertainty is retained as that same operation's pending disposition. No delete-then-write gap and no independent journal/selfhash as lineage. Actual implementation signatures must use existing private owner types, not expose these conceptual names as interchangeable DTOs.

Daemon/helper integration needs a **named fixed read-only control-purpose projection** at the actual GmMainParticipatingControl.Daemon / GmTurnHelperControl lease seam. Script code may inspect the result to decide whether to dispatch, but this data is not a capability. Actual side-effect entrypoints still verify current source/G/owner and their complete witness. Raw generic snapshot APIs remain data services and are insufficient by themselves. The engine reader, normalization context leaves, progression/guardian leaves and script helper must share the strict codec/phase policy without centralizing unrelated lifecycle mutation.

## Daemon, clocks, and staged-input ordering

For P and non-GM A, return a nondispatch result **before** updating LastRepairRequestWrite, observed/stale turn keys, TurnCount, repair watchers, prompt-operation GUID, bridge invocation or timing baselines. Final staging for the same action must remain observable even if mtime precision matches a previously seen file. On actual gm_waiting, deduplication must use the exact owner revision/payload/source witness rather than treating an earlier ignored file's timestamp as processed. Keep existing legacy repair behavior on its real route.

Do not replace actual freshness semantics with A revision or a new timestamp. Pending-file observation currently includes bytes/hash/LastWriteTimeUtc; repair detection, dispatch start, canonical target write times and elapsed stall time have distinct roles. Preserve those Mortal and afterlife policies and store actual named owner observations only where R05 defines durable provenance. An old observation is not recreated by resampling UtcNow or by copying a control field into a source fact. External dispatch still has real uncertainty; not dispatching P solves the premature-launch bug, not transport completion/recovery.

## Consumer registry

Every row names a real source family, current behavior, proposed obligation, and failure/cold/deletion policy. C14 and C37 are protection/instruction consumers, not state authority. C28/C30/C34–36 are important indirect owner consumers even where the literal pathname is absent. Exact source excerpts and full-file pins appear in JSON. No arbitrary grep hit has been promoted to proof of a lifecycle.

### C01 — EnsureClientOwnedSystemFilesHealthy[Core]Async / CleanupOrphanedTurnRequestBeforeValidationAsync / successful validation cleanup

Source: [VR:145–197](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:145); [VR:294–311](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:294).

Current: Health precedes original; orphan input survives on raw request or Ready presence; zero-error validation deletes repair artifacts.

Proposed: Classify actual current original/control/entry owner before presence shortcuts; preparation is not a pending repair or a current input context. Successful health may clean only an eligible legacy repair, not the preparation carrying that health step.

Failure/cold/deletion: P retained; unrelated input is conflict, not preserved as authorized work. Cold continuation/abort requires R01 owner. Do not rerun health to recreate a receipt.

### C02 — DeleteValidationRepairFiles[ForSession]Async / DeleteValidationRepairReadyAsync / read-write wrappers

Source: [VR:8249–8295](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:8249); [VR:8357–8390](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:8357).

Current: G/lease guards then separate Ready/request/stall deletion; generic write accepts path/content.

Proposed: Keep legacy operation behind genuine legacy cleanup witness. P/A mutations use named owner transitions; cleanup is joined to actual step/terminal/native cohort.

Failure/cold/deletion: No public force/delete-purpose flag; mismatch/G drift preserves evidence and fails. Ready/stall absence alone cannot retire history.

### C03 — WriteValidationRepairRequestForSessionAsync; private ValidationRepairRequest

Source: [VR:4019–4184](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:4019); [PRI:87–109](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.PrivateImplementation.cs:87).

Current: Deletes prior request first, builds unversioned payload with real DetectedAtUtc; worker may apply and return before request is ever written.

Proposed: For new original, retain A and append route before dispatch; build exact existing payload inside activeRepair.requestPayload. Replace transport projection without erasing history. Distinguish original_issued, turn_staged and repair_prepared/gm_waiting.

Failure/cold/deletion: No delete-and-recreate A gap. Native/worker companion pre-plan prerequisite R04; do not treat post-return Applied as history proof.

### C04 — BuildValidationRepairTrajectoryRepairPacketRefsAsync; rejected-Ready retry

Source: [VR:3910–3952](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:3910); [VR:8154–8168](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:8154).

Current: Telemetry deserializes raw request or rebuilds packet refs from errors; invalid Ready deletes/reissues request.

Proposed: Telemetry reads only a typed repair payload; retry performs exact owner revision transition and Ready deletion with retained lineage.

Failure/cold/deletion: Telemetry/fallback packets cannot issue history. Preserve revalidationAttempt and old error/freshness order; revision is not a gameplay attempt.

### C05 — NormalizePendingRepairArtifacts[Core]Async

Source: [SS:1237–1312](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1237).

Current: Missing/invalid original deletes all; absent/mismatched complete deletes repair artifacts, even before a repair can be staged.

Proposed: Purpose/obligation classification must precede legacy stale rules. P and original_issued A do not require turn_complete; gm_waiting A needs actual repair route proof.

Failure/cold/deletion: Missing original is not proof P is stale; seeded original + missing/old/malformed control is missing-owner conflict. Only genuine abort/terminal settlement can retire; legacy-only scope remains exact.

### C06 — TryPromoteValidationRepairArtifactStallToTerminalErrorAsync

Source: [SS:1317–1417](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1317).

Current: Request presence + legacy tuple lets stall become a new turn_error; creates UTC timestamp, deletes complete/Ready.

Proposed: Run only typed active GM repair watch for same route/revision/original; preparation/history/worker-only never triggers it.

Failure/cold/deletion: Old diagnostic/error ordering retained for legacy; new conflict cannot be converted into a player terminal error from stale stall.

### C07 — IsCurrentPendingTurnSnapshotAsync / pending request metadata

Source: [SS:919–981](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:919).

Current: Repair tuple first, then input, then complete; parse exceptions fall through.

Proposed: Read control obligations before fallback; derive correlation from actual authenticated original and permitted payload phase.

Failure/cold/deletion: No P tuple or intent becomes source action; bad required new control cannot be bypassed by otherwise valid input.

### C08 — PendingTurnSnapshotReader.ReadCurrent / ReadObservedCurrent / ReadCurrentContext

Source: [READER:138–310](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/PendingTurnSnapshotReader.cs:138); [READER:732–834](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/PendingTurnSnapshotReader.cs:732).

Current: Authenticates manifest/authority before reading all three contexts; repair diagnostic-only specially skipped, otherwise root tuple used.

Proposed: Reuse pre-context authenticated-original facts for protocol dispatch, then phase-aware context projection within the same observed session and closure barrier.

Failure/cold/deletion: No recursion through whole reader to classify itself. P cannot issue snapshot authority. A cannot downgrade via missing root tuple; observed closure faults still precede authority export.

### C09 — CanonicalStateNormalizer pending snapshot and progression-report correlation

Source: [NORM:917–963](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.SoulAndMeta.cs:917); [NORMP:25–75](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/CanonicalStateNormalizer/CanonicalStateNormalizer.AfterlifeEntityProfiles.cs:25).

Current: Repair-first current-context fallback; progression reads first valid tuple from input/complete/request.

Proposed: Require purpose-aware correlation independently of normalizer mutation; legacy branch unchanged, new conflicts reported before choosing earlier tuple.

Failure/cold/deletion: Do not normalize P/A into legacy or repair hashes/seed; no normalizer under a lease as admission substitute.

### C10 — GuardianPowerEventState; ProgressionScheduleService context resolution

Source: [GUARD:550–625](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GuardianPowerEventState.cs:550); [PROG:1453–1505](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/ProgressionScheduleService.cs:1453).

Current: Various first-valid tuple orders include raw repair request.

Proposed: Consume shared fixed typed correlation projection for new contract, preserving actual original/time/schedule semantics.

Failure/cold/deletion: P intent is not accepted guardian/player input; missing/unknown required A never silently falls back.

### C11 — MortalJoint frozen progression basis

Source: [PB:204–231](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/ProgressionScheduleService.MortalJointBasis.cs:204); [PI:10–14](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/ProgressionScheduleService.MortalJointInputs.cs:10).

Current: Fixed raw roots contain request; pure tuple scan falls back by order.

Proposed: New profile must bring owner-classified context as part of actual closed basis; preserve existing inactive v2 bytes/calculation until coherent integration.

Failure/cold/deletion: No public DTO/context or helper ACTIVE; this purpose design does not activate F1 or alter accepted calculator policy.

### C12 — Validation accepted-turn manifest matching; Guardian action/turn fallback

Source: [AC:5158–5220](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.AcceptedTurnAndInkFeathers.cs:5158); [GK:651–674](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:651); [GK:2685–2696](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.GuardianPolicyKernel.cs:2685).

Current: Repair tuple/action/turn can be selected ahead of other sources or validated manifest.

Proposed: Original-first obligation check then allowed context projection; only genuine current request/action semantics survive.

Failure/cold/deletion: Do not feed P's retained intent into accepted-event computation. No changed XP/alias/time/reward policy.

### C13 — ValidateValidationRepairReadyProtocolAsync

Source: [PROTO:898–1024](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs:898).

Current: No Ready returns; missing request/diagnostic/mismatched tuple rejected, malformed request partly delegated elsewhere.

Proposed: Known P/non-GM A + Ready is purpose conflict; gm_waiting validates exact existing payload tuple plus owner route/revision source; worker outcome follows its owner.

Failure/cold/deletion: Never mint ready acceptance from A.phase or request tuple alone. Preserve legacy malformed/error precedence on legacy route.

### C14 — Client-owned protection / bound world read set / issue classification

Source: [PROTO:678–690](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.BootstrapAndProtocol.cs:678); [WORLD:205–218](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.WorldReadPhase.cs:205); [VP:725–793](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.PrivateImplementation.cs:725); [NPC:4930–4950](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.NpcWorldAndMeta.cs:4930); [VR:8811–8820](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:8811).

Current: Request excluded from some original-equality checks, bound as a current read, classified client-owned protocol surface.

Proposed: Keep exact client-owned protections and bound-read closure; add purpose parse in same source session, not scalar fallback.

Failure/cold/deletion: No broader ordinary guard exemption or GM write right; these predicates are protection consumers, not issuers.

### C15 — Snapshot inventory / ShouldTrack rollback filtering / RestorePreTurnBackupAsync

Source: [SS:1850–1944](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:1850); [SS:2366–2410](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs:2366).

Current: Browser inventory knows request/Ready; rollback deliberately excludes them while restoring tracked world and removing new files.

Proposed: Preserve exclusion; control history transitions with real restore owner, never as arbitrary original before-image restoration.

Failure/cold/deletion: Native restore disposition R03 still required. No blanket pre-prep rollback or loss of QTE/committed effects.

### C16 — Browser restored/accepted settlement; inactive snapshot evidence

Source: [BR:342–429](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.BrowserPlayerInput.cs:342); [IN:175–187](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.InactiveSnapshotEvidence.cs:175).

Current: Settlement requires work paths gone and exact terminal/browser history; inactive retirement blocks on raw request presence.

Proposed: Join typed history retirement to genuine final settlement, then absence checks can succeed; retaining history indefinitely is not success.

Failure/cold/deletion: P/A blocks inactive retirement. No parsed phase/Ready substitutes for browser acceptance/restoration proof.

### C17 — Mortal joint terminal cleanup

Source: [TERM:45–100](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointTerminalCleanup.cs:45); [TERM:243–264](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointTerminalCleanup.cs:243).

Current: Real cohort proof; present request must match root tuple and fullTurnResubmissionRequired=true.

Proposed: New profile validates nested payload + history/common settlement proof and includes retirement in same presealed cleanup cohort; default v2 stays unchanged.

Failure/cold/deletion: No after-seal appended deletion. Missing/foreign/incompatible control prevents cleanup; full M3 proof remains a separate gate.

### C18 — Spiritual continuation transport publication/reconciliation/wait/cleanup

Source: [SP:134–142](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs:134); [SP:300–345](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs:300); [SP:540–590](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs:540); [SP:605–774](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.SpiritualContinuation.cs:605).

Current: Private C2 owner controls exact envelope; request written before worker; exact-byte deletion on continuation/escape; public envelope without private checkpoint refused.

Proposed: Recognize legacy spiritual only with its actual private owner; P/A conflicts cannot be overwritten or treated as absent. Do not automatically migrate to Mortal A.

Failure/cold/deletion: Preserve exact transport bytes, realm/Ready/error behavior and cold private owner requirements. Shared discrimination changes require afterlife documentation/tests.

### C19 — Spiritual original witness / draft exclusion / MatchesIssuedSpiritualContinuation / progress commit

Source: [SC:136–156](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.SpiritualOriginalTurnCapture.cs:136); [SD:130–150](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SpiritualOriginalDraftInputs.cs:130); [SCD:181–201](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.SpiritualContinuationDraft.cs:181); [SPD:105–129](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/Validation/ValidationService.SpiritualDependentProgress.cs:105).

Current: Physical request witness retained; client control excluded from GM draft; absent transport allowed under private owner; present strict envelope checked and C2 uses exact request/Ready.

Proposed: Keep physical witness; unknown/new envelope is present conflict, never absent continuation; same control classifier may select only existing supported spiritual branch.

Failure/cold/deletion: No new generic afterlife authority; no omission of control from semantic source or same-cohort checks.

### C20 — Get-GmExperienceQuery and watcher/startup/poll entrypoints

Source: [DAEMON:479–491](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:479); [DAEMON:6702–6805](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:6702).

Current: Any existing parseable request implies validation_repair mode; existing file repeatedly processed after restart.

Proposed: Classify before creating query/dispatch; P and non-GM A are nondispatchable retained control. Only gm_waiting projected payload enters repair mode.

Failure/cold/deletion: Do not delete ignored files or change polling's ability to see later same-action stage; unknown/mixed yields technical conflict.

### C21 — Process-RepairRequestCore

Source: [DAEMON:6204–6220](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:6204); [DAEMON:6335–6375](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:6335).

Current: Reads actual bytes/mtime then advances LastRepairRequestWrite before diagnostic check; defaults missing tuple/attempt.

Proposed: Perform strict purpose/original/route classification BEFORE watermark, key, message, operation or watch. Advance repair watermark only for a real eligible dispatch observation.

Failure/cold/deletion: Ignoring P must not suppress later final request even with same action/mtime resolution. Preserve actual legacy DetectedAtUtc/start/mtime observations.

### C22 — Process-TurnCore / Test-TurnRequestHasPendingSnapshotContext

Source: [DAEMON:5840–5864](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:5840); [DAEMON:5108–5175](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:5108).

Current: Provisional original failure may mark terminal key stale; then increments TurnCount and samples start clock.

Proposed: Recognize actual original_issued/not-staged under owner binding BEFORE stale-key insertion/counter/timer. turn_staged proof must bind real final input and browser condition.

Failure/cold/deletion: Do not create synthetic completed context or dispatch on original alone; ordinary sampled-state mismatch and legacy behavior remain intact.

### C23 — Repair watch current/stalled/promote

Source: [DAEMON:3410–3560](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:3410).

Current: Tuple+attempt currentness, raw Ready completion, actual elapsed clocks/mtime and bridge progress determine stall.

Proposed: New watch carries exact control revision/payload/original witness and only GM route; purpose transition invalidates watch without consuming future stage.

Failure/cold/deletion: Preserve Mortal/afterlife freshness boundaries and warning/error policy. Historical timestamps remain R05 owner observations, not validated by selfhash.

### C24 — Prompt pending observation / operation allocation / Dispatch-WithRetry

Source: [DAEMON:3630–3680](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:3630); [DAEMON:5511–5545](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:5511).

Current: Actual bytes/hash/mtime observation; operation keyed by kind/revision/path/hash; source-hash rechecks surround external dispatch.

Proposed: Do not allocate operation for nondispatchable phases. Typed dispatch projection binds whole control/original/final-request source and rechecks before actual side effect.

Failure/cold/deletion: Existing external-dispatch uncertainty remains; no claim hashing a packet proves receipt or original lineage. No new time delegate.

### C25 — Read/Wait-CorrelatedValidationRepairRequest / turn trajectory

Source: [DAEMON:4294–4338](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:4294); [DAEMON:6126–6140](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/game_master_daemon.ps1:6126).

Current: Raw matching tuple after turn completion is classified as repair/rejection.

Proposed: Require actual repair-route projection, not A history/preparation presence. Preserve real worker outcome distinction.

Failure/cold/deletion: Trajectory is telemetry, not admission/history proof. Do not misclassify ordinary accepted turn just because A exists.

### C26 — Test-BoeActiveValidationRepairRequestForTurn / no-terminal-signal guard

Source: [HELPER:1782–1850](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Launcher/GM_Turn_Helper.ps1:1782).

Current: Root tuple + not diagnostic can bypass no-terminal guard.

Proposed: Only genuine dispatchable repair view can enable existing repair exception; P/non-GM A/unknown blocks it without becoming GM authority.

Failure/cold/deletion: Preserve old helper source eligibility; no caller boolean accepted and no ACTIVE ordinary source upgrade.

### C27 — Protected surface checks / Complete-BoeValidationRepair

Source: [HELPER:1523–1528](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Launcher/GM_Turn_Helper.ps1:1523); [HELPER:3225–3298](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Launcher/GM_Turn_Helper.ps1:3225).

Current: Request protected from GM writes; completion captures request bytes, strict spiritual/realm checks, writes real timestamped Ready with source witness.

Proposed: Maintain protected path; unwrap payload only through fixed observed control view; Ready completion verifies gm_waiting route plus whole envelope witness before publication.

Failure/cold/deletion: Worker/history/preparation cannot produce Ready; preserve spiritual owner checks and timestamp policy. A tuple alone is insufficient.

### C28 — Daemon/helper participating read/mutation bridges

Source: [DC:82–143](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmRuntime/GmMainParticipatingControl.Daemon.cs:82); [MC:150–190](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmRuntime/GmMainParticipatingControl.cs:150); [HC:92–176](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmRuntime/GmTurnHelperControl.cs:92); [SCOPE:1–100](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GmHelperCanonicalScope.cs:1).

Current: Actual quiescence/lease/G and exact byte/mtime witnesses; generic scope checks do not implement control-purpose policy.

Proposed: Add named fixed read-only purpose projection under actual owner/lease; script consumes data, later mutation/dispatch rechecks same witnesses. No generic caller-purpose/read-map authority.

Failure/cold/deletion: Transport snapshot is not owner proof. Nested lease forbidden; current browser/worker admission remains mandatory.

### C29 — Developer relay capture/read/apply

Source: [RELAY:36–52](/workspace/The-Book-of-Eternity-Reborn/tools/gm-relay/relay_cli.py:36); [RC:145–190](/workspace/The-Book-of-Eternity-Reborn/tools/gm-relay/relay_contract.py:145); [RA:35–43](/workspace/The-Book-of-Eternity-Reborn/tools/gm-relay/relay-apply-response.ps1:35).

Current: Prompt prefix selects request path; tuple/original/hash witnesses; repair application calls Complete-BoeValidationRepair.

Proposed: Capture only actual typed repair payload for repair kind, retain whole envelope and original witness; reject nondispatchable phases before relay packet issuance.

Failure/cold/deletion: No developer relay/helper source upgrade; apply still passes real helper owner guard. Unknown/new unsupported version fails closed.

### C30 — Worker task/reservation/apply/Ready/audit

Source: [WORK:104–145](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs:104); [WORK:219–288](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs:219); [WORK:315–375](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs:315); [WORK:415–450](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs:415); [WORK:502–570](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerValidationRepairDelegator.cs:502); [GATE:50–150](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs:50); [GATE:350–387](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/GmWorkers/GmWorkerApplyGate.cs:350).

Current: Task can be issued before legacy request; apply committed before returned Accepted and audit; Ready later, actual new task GUID and UTC.

Proposed: New A route must join actual reservation and transaction before prepare/plan/cache; worker companion updates exact control path in same native decision including zero-change results.

Failure/cold/deletion: R04 OPEN: post-return Applied/audit/Ready cannot reconstruct lost owner or prove atomic history. No re-reservation/rerun/imported capability on crash.

### C31 — LiveTurnPreparationService.PrepareAsync / cleanup

Source: [LIVE:16–90](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/LiveTurnPreparationService.cs:16); [LIVE:394–403](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/LiveTurnPreparationService.cs:394).

Current: Helper source distinct; snapshots exclude request; cleanup removes input/original/authority/snapshot/dice, leaves repair request.

Proposed: Before destructive helper preparation, genuine source gate refuses conflicting P/A; keep helper behavior outside new ordinary active profile.

Failure/cold/deletion: No relabeling helper original. Cold helper original cannot seed A; shared version reader compatibility still required.

### C32 — BrowserAfterlifeTurnRequestQueue

Source: [BA:24–100](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/WebUi/BrowserAfterlifeTurnRequestQueue.cs:24); [BA:230–250](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/WebUi/BrowserAfterlifeTurnRequestQueue.cs:230).

Current: Specialized direct-gacha original, prepaid rollback/dice; request excluded from snapshot/rollback; cleanup other pending artifacts.

Proposed: Do not replace specialized publisher; add conflict check before mutation if new ordinary P/A exists, otherwise preserve actual afterlife route.

Failure/cold/deletion: No rollback/redraw/tick from purpose dispatch; no new generic afterlife seed.

### C33 — Save preparation/archive/public entry

Source: [SAVE:69–99](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.cs:69); [SAVE:159–195](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.cs:159); [SAVE:551–574](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.cs:551); [CREATE:243–335](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.Creation.cs:243).

Current: Repair path globally ephemeral and skipped; resource-only archive admission does not itself classify preparation purpose.

Proposed: For new profile, save admission must reject unresolved P/A before archive enumeration, preserving bytes. Existing settled/legacy ephemeral cleanup remains exact.

Failure/cold/deletion: No new permission to archive pending history. A caller-owned lease does not prove terminal settlement. Dormant DeleteEphemeralArtifacts declaration is not a live cleanup claim.

### C34 — Browser/console save entry admission and native fences

Source: [BI:35–53](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/WebUi/BrowserPendingTurnInspector.cs:35); [BL:25–58](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/WebUi/BrowserLocalWriteCoordinator.cs:25); [MENU:195–225](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/WebUi/LocalWebUiMainMenuService.cs:195); [OPT:1210–1224](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.OptionsAndSettings.cs:1210); [BO:398–450](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs:398); [FENCE:150–184](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs:150).

Current: Browser pending action/request/snapshot and UI busy block saves; cold preparing/terminalProcessing fenced by genuine owner; console calls shared CreateSave.

Proposed: Retain existing refusals; add new-purpose check at shared save admission because control alone is absent from BrowserPendingTurnInspector list.

Failure/cold/deletion: Do not bypass native recovery to archive P. Existing UI refusal does not prove every direct SaveLoad caller filters control.

### C35 — Load archive filtering and actual complete replacement

Source: [LOAD:300–341](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.Loading.cs:300); [SAVE:706–715](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.cs:706); [SAVE:1116–1140](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/SaveLoadService.cs:1116); [LR:25–109](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/FileSystemManager.LoadReplacement.cs:25).

Current: Ephemeral entries omitted/filtered; prepared load publishes complete before/after images + new G under actual native transaction.

Proposed: Old archive remains unchanged. New unresolved P/A must be settled by real replacement owner or reject before preparing replacement; no opaque import of history authority.

Failure/cold/deletion: R03/R07 replacement debt frontier: same native complete-cohort proof required; generic load caller cannot silently erase history or rehydrate capabilities.

### C36 — PublishSessionReplacementAsync clear versus rotation

Source: [SR:24–110](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/FileSystemManager.SessionReplacement.cs:24).

Current: Clear validates pending original and deletes selected state in same publication; rotation deletes worker-latest/Ready but retains request while changing G.

Proposed: Classify before replacement; genuine owner may settle/retire compatible state within native cohort, otherwise refuse new P/A. Never rebind old A by editing G.

Failure/cold/deletion: Rotation retention is not transfer of authority. Full lawful replacement/abort for new profile remains R03/R07, not permanent HOLD acceptance.

### C37 — Repair packet instruction producers / launcher

Source: [VR:7095–7172](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:7095); [VR:7233–7327](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:7233); [VR:7388–7418](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs:7388); [ITEM:315–320](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/MortalItemRepairPacketBuilder.cs:315); [LOC:299–304](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Services/MortalLocationRepairPacketBuilder.cs:299); [LAUNCH:295–301](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Launcher/Generate_CLI_Launch_Script.ps1:295).

Current: Instructions tell GM to read root fields and copy current request tuple.

Proposed: Update producer text to fixed dispatchable payload guidance when schema emitted; do not instruct GM to edit history/P or infer request purpose from presence.

Failure/cold/deletion: Same-change docs/examples/source guards required. Default legacy emitted text/bytes remain unchanged during unactivated extraction.

## Save, restore, rotation, cleanup: three distinct cases

**Already refused.** Actual browser preparation/pending action and native pending publication have existing admission fences (BI35–53; BO398–450; FENCE150–184). They must remain effective. A purpose reader cannot acquire a metadata-only or helper lease and bypass these gates.

**Admitted archive omits new required control.** Shared SaveLoad archive preparation has no new-purpose check and treats the request path as ephemeral. Even if a particular UI route currently cannot reach this state, the service contract does not establish that all callers are safe. Add an actual new-profile admission refusal before archive preparation where P/A is unresolved. Do not add P/A to the archive, grant archive import authority, or silently drop it. Supporting a pending-history save later is a separate complete save/owner contract, not implied here.

**Actual postsettlement ephemera.** Legacy transport can keep its existing eligible cleanup; new history can be retired only by real settlement proof. Browser settlement currently insists work paths are absent; joint cleanup has exact original/cohort proof. New control retirement must be a member of those genuine operations, not a later free deletion. The dormant SaveLoad `DeleteEphemeralArtifacts` declaration is not evidence of a live cleanup path. Load/session clear replace a complete native cohort with G; rotation changes G while retaining the request. Each requires an explicit new-owner disposition before accepting P/A. The source proves the conflict and available owner seam, not an implemented transfer/recovery solution.

## Same-change documentation and guard obligations

This /tmp design emits no runtime schema, changes no GM byte contract, and needs no repository GM migration now. **Any implementation that emits/validates P/A, changes presence/dispatch meaning, or exposes the new projection must synchronize both Mortal and afterlife material in that same accepted change.** Client ownership does not excuse this: daemon/GM/helper currently read the path. Do not globally reformat old examples or silently change old spiritual envelopes.

| Surface | Current contract and necessary update |
|---|---|
| CLI_API_Specification.md:921,977–1004 | Path means rejected-turn request; root fullTurnResubmissionRequired/tuple copied to Ready. Add closed purpose/version table, nondispatch P/history, original-issued versus staged, exact payload access, true repair-only Ready, unchanged full-response duties. |
| CLI_Agent_Daemon_Specification.md:645–658 | Restart processes any existing request; root packet fields and canonical-target freshness. Restrict to genuine dispatchable projection; preserve strict target-write freshness and warning/error ordering. |
| TaskGuides/CLI_Step_Main.txt:695–720; Rules/Block_CLI_Operations.txt | Explain that control presence is not a GM task and history is client-owned. Expose only real pending GM work; no new-turn/Ready response to preparation. Review shared protected-write guidance even where no literal request path occurs. |
| OtherGuides/Afterlife_Contract_Matrix.md:291,533; Afterlife_Pending_Control_Surface_Inventory.json:702 | Inventory currently excludes this as generic protocol. Keep accurate generic classification while documenting new purpose discrimination shared by afterlife consumers; do not invent an afterlife lifecycle or silently upgrade specialized sources. |
| OtherGuides/GM_Worker_Bridges.md:694; Examples/E_CLI_GM_Worker_Validation_Repair.txt:6 | Worker is actually attempted before legacy fallback request. Document new owner/history route separately from GM dispatch and actual applied/native outcome; never imply request presence precedes every worker. |
| Examples/E_CLI_Step_Main.txt:132,853–921 | Add worked preparation/no-dispatch → genuine original stage → actual repair → Ready → settlement flow; show that GM sees only permitted payload, cannot write history. Preserve original-action metadata and full response semantics. |
| Examples/E_CLI_Afterlife_Turns.txt:301 and spiritual continuation examples | Explicitly show existing spiritual/helper/source route remains supported with its private owner; P/new Mortal A cannot pass as spiritual request. Keep exact old envelopes immutable; new-version examples must be clearly distinct. |
| Examples/example_validation_manifest.json:467,1325; FileSystemExample/validator_fixtures/protocol_request_surface/ | Fixtures need version/purpose/Ready/unknown/mixed cases and current expected errors; update manifest coverage, not only explanatory prose. |
| OtherGuides/Wound_Materialization_Contract.md; Effect_Materialization_Contract.md | Audit source/repair/full-resubmission and client-owned-root guidance against changed dispatch; no new QTE offer or F1 publication implied. |
| game_master_daemon.ps1 embedded directives/templates; Launcher/Generate_CLI_Launch_Script.ps1; actual packet instruction builders VR/ITEM/LOC | All instructions that currently read root .errors/.harnessRepairPackets or copy tuple must select actual payload through supported projection. Prevent “read any existing request” from reintroducing premature dispatch. |
| PromptDocumentationCoverageTests, AfterlifeDocumentationCoverageTests, GameEngineSourceGuardTests, ExampleDocumentationValidationTests, GmTurnHelperContractTests | Assert new purpose branch coverage and actual unchanged legacy examples, protected surfaces, source eligibility, no root-tuple fallback for P/A, no daemon watermark/stale-key mutation on ignored records. Runtime causal tests remain necessary; textual assertions do not prove owner semantics. |

The companion JSON pins these source/doc/guard files and prior design/review inputs. Some are synchronization obligations rather than runtime consumers; they are labeled separately. No test category or broad suite is authorized by this report. Future implementation must select current catalog categories for its actual changed consumers, with independent sufficiency review.

## Finite future acceptance matrix — all UNRUN

| ID | Causal fixture / cut | Required observation |
|---|---|---|
| PL01 | Genuine console entry with proposed owner-issued P before actual original; entry health succeeds | P survives successful validation/orphan normalization; no dispatch/Ready/context authority; no health rerun claimed as recovery. |
| PL02 | Genuine browser queued→preparing owner, same P; cold process loses scope | Existing browser refusal remains; no synthetic scope from JSON. Advance/abort only after independently qualified preparation recovery. |
| PL03 | Actual new original + A original_issued, final input unstaged; daemon sees it repeatedly | No stale key, turn counter, timing baseline, watcher, prompt operation or repair watermark consumed. |
| PL04 | Same actual action staged later, including equal timestamp precision | Actual original request dispatches once through genuine stage proof; earlier ignored record cannot hide it. |
| PL05 | New original + missing/legacy/unknown/mixed/wrong-G control | Exact missing-owner/conflict before tuple fallback, Ready or cleanup; no legacy downgrade or default tuple dispatch. |
| PL06 | Old ordinary/helper/spiritual original + P/A; old original + supported old request | New control refuses upgrade; genuine old route retains exact payload/guards/diagnostics and bytes. |
| PL07 | Missing/invalid original + P/A; old orphan artifacts with/without real legacy retirement owner | No absence-based stale deletion of new history; only actual eligible legacy cleanup succeeds. Unsupported cold disposition remains explicit open blocker. |
| PL08 | Current-context order permutations input-first/repair-first/complete-first | A conflict cannot be skipped by an earlier valid tuple; P intent cannot affect Guardian/progression/player action. Observed reader closure barrier still precedes authority. |
| PL09 | Ready/stall present for P/history/worker-only A and actual gm_waiting A | Wrong purpose yields conflict without terminal success/error synthesis; real GM route preserves tuple/error/clock semantics. |
| PL10 | Actual worker selected before legacy fallback; cuts before reservation, after apply commit, before audit/Ready and after returned Applied | Every history decision requires real same-native companion. Existing unowned gap must fail the future acceptance test until R04 closed; no invented successful recovery. |
| PL11 | Actual spiritual continuation publish/reconcile/Ready/escape; new P/A occupies path | Genuine private C2 and exact-byte cleanup preserved; foreign purpose blocks before overwrite, including absent-transport logic. |
| PL12 | Helper no-terminal bypass, Complete-BoeValidationRepair, relay repair capture | P/history cannot bypass terminal checks or author Ready; actual GM repair unwraps payload while preserving whole-source witness. Helper original never becomes ACTIVE. |
| PL13 | Control/original/request/G drift between purpose read and mutation/dispatch; parse/read/metadata failure | Fault is latched; no scalar fallback, no stale cached capability, no side effect. Session replacement surfaces its actual failure. |
| PL14 | Save through browser, direct shared service and console; actual unsettled P/A versus settled legacy | Existing native/browser admission refusals remain; new required history never silently omitted from an admitted save; no unsupported pending archive added. |
| PL15 | Load, clear, rotation with actual unresolved/new settled control | No G rebind or independent deletion; real replacement/settlement cohort required. Old supported replacement behavior remains exact. |
| PL16 | Original issuance/native uncertain cuts, final staging cut, terminal retirement cut | Exactly old/new cohort or actual owner pending decision; never original-without-required-history success. P→A and cleanup members fixed before seal; no appended write. |
| PL17 | QTE reminder consume commits before return, then entry cleanup/abort | Actual result/effect/cursor ownership retained; no rerun summary consumption or blanket rollback. R01P02/P03 remain unqualified until actual causal test passes. |
| PL18 | GM docs/examples/manifest/source guard worked flows for Mortal and afterlife | New purpose is understood without reading client implementation; old spiritual diagnostic/full-resubmission guards retained; no public QTE/unfinished F1 activation. |

Run actual positive fixtures through genuine GameEngine console/browser originals, not helper-authored upgrades or constructed issuer/reflection. Reuse existing PendingTurnSnapshotReader/observed-reader fixtures for read closure; actual GameEngine lifecycle repair/BrowserOriginalAdmission/RepairDiagnosticStorageOutcome and joint completion fixtures for ownership; GmDaemonStorage/GmTurnHelper/worker outcome fixtures for actual script/worker behavior; portable save/load/session replacement fixtures for native cuts. The report names future test contours, not executed results, and does not approve synthetic P/A fixtures as production proof.

## What is closed, what is not

**Closed as a recommendation:** one existing path; unambiguous strict outer purpose union; original-first precedence; explicit nondispatch preparation/history; original-issued versus staged distinction; concrete phase/null rules; consumer-specific read/write/delete/dispatch obligations; no watermark/stale-key consumption on ignored phases; no tuple fallback through required new history; no automatic helper/afterlife upgrade; actual save-admission versus archive omission versus terminal cleanup separation.

**Still OPEN, with exact frontier:**
- **R01P01 engineering / R02 codec-and-consumer migration:** private original-auth fact projection, typed legacy-cleanup eligibility, shared strict codecs and all C01–C37 routing do not exist yet. In particular, absent-original cold legacy cleanup cannot be called new-owner retirement proof. The proposed outer fields do not specify the complete nested requestStage/settlement/step native proof codecs. These must be closed with actual owning methods before any version is emitted; no broad generic wrapper release.
- **R01P02/P03 original/preparation and QTE:** durable real first record, pre-issue effects/results/draft/default IDs, actual cold continuation/abort, same-cohort original+authority+control and browser specialized publication remain prerequisites. No Stage/RNG/gacha/health replay, extra tick/draw/cost/IDs, or unsupported pre-prep rollback.
- **R03 restore/replacement:** exact pending debt and restored terminal ownership, browser settlement, load/clear/rotation native members and lawful cold dispositions; no permanent HOLD presented as all-outcome recovery.
- **R04 worker:** same actual reservation/apply native transaction must carry control transition before prepare/plan/cache, including zero-change worker result. Audit-after-commit/Ready/returned Applied do not fill the proof gap. Real cold live-authority/reservation rejection remains.
- **R05 source/semantic clock proof:** preserved actual start-versus-detected/canonical-target mtime/elapsed observations and full validation semantics. Parsed history/hash/time is not cold semantic authority.
- **R06 lost C/result/GUID/candidate and R07 no-publication/attempt debt/common transfer:** neither solved by a pathname nor by durable admission history. No rerun/reallocation/re-reservation/imported DTO capability. Actual same candidate/fence remains required.
- **R08** exact docs/fixtures/category selection and independent coherent design/source/evidence reviews. This consultation is not its own independent acceptance.

Full H-B02, R01P01–03/R02–08, target qualification, ordinary source n/action/k death→evaluation n+1, F1/T179/T192/P0/M3/T197, DEP-C/I/L, D4/guided/visual/Windows/D8 and all public-activation gates remain open. This proposal supplies no new principled grant and gives no reason to autochoose F2. Prior immutable design/review pairs remain authoritative evidence of their own bounded claims, not runtime success.

FINALSTOP.
