# T197 pending-fate family reservation — producer design

Pinned source `8c098487398830c20bd3705261c2a2c507bce5d8`; selected production comparison `ec50e4cd6c6e17ab651711eaf0c9cbc957dd8842`. Configured Astra/High; backend telemetry UNKNOWN.

**Current issuer missing. Proposed protocol remains conditional; full writer/native exclusion is OPEN. No engineering/activation/F2 release.**

## Finding and recommendation

There is NO presently issuable OriginalPendingFateFamilyReservation with the required exclusion semantics. Current facts prove actual owner/G/main admission and an eventual lease family, but not absence of pending-path writers before RNG. CanonicalRootIdentity is explicitly an in-process interner. A separate participating helper accepts arbitrary game_state/control/* write/append/delete under its own actual lease. Paid fate, incidental GetOrCreate, live preparation deletion, rollback/load/replacement and family transitions add real competing owners. Exact bytes afterward detect some races; they do not undo consumed entropy or prove exclusion.

Recommend a conditional producer in the ORIGINAL main coordinator: a narrowly scoped pending-path reservation with a closed writer-admission protocol. It is a new source-exclusion/custody contract, not an existing method merely awaiting extraction. The required authority is to defer/refuse NEW conflicting operations before their domain effects, drain exact ALREADY-started operation/debt scopes, and bind future leases/family transitions to that same live original owner. It grants no new writes/fee/refund/reward/RNG beyond the admitted source action. Prior autonomous design authorization permits review of it; no repeat permission request is needed. Independent acceptance and qualification remain required before implementation.

The proposal below supplies concrete issuance and refusal rules, but full writer/native coverage and cross-process admission are missing. Therefore the producer is NOT closed/accepted or usable today. This is not a permanent HOLD outcome: successful future same-situation continuation remains mandatory under PF-P05, which this producer does not fabricate.

## Birth and actual caller integration

At the shared Stage1596 boundary, before AttachPendingDiceAndGachaAsync invokes its public no-lease service, call the proposed original Stage parent method ReservePendingFateFamilyAsync. It is reached from both ProcessPlayerTurn console1495/1497 and browser1509/1510. The reservation request is constructed inside the retained original Stage task; it receives the actual request reference and live source operation, not a mode flag or imported tuple. Capture/root/G work and earlier Stage preparation stay under their existing owners. This child cannot retroactively take custody of earlier MainMenu/Explorer, fees, callbacks or backup effects.

Issuance requires the REAL coordinator Access/OperationPin and actual outer+inner main admission identities from the born original host, same FileSystemManager/CanonicalRootAuthorityIdentity, same already-bound G and original operation identity. If an ancestor owns or is awaiting canonical/lifecycle storage, this call may not wait for a reservation/drain: that would create a cycle. The parent must provide an earlier registered source cut before such acquisition or an original owner-directed handoff after release. No nested generic lease, borrowed string generation, fresh root operation or old-pin rebind substitutes. This actual entry integration is PB01/PB03, still OPEN.

For ordinary browser Stage, retain the genuine BrowserOriginalOperationScope created at1565, its Binding, Condition and issuing MainAdmission; ValidateOwner must observe an acquired, nonclosed, nonmetadata real owner sharing actual access, and Condition.RequireCurrent. Issuer retains that real lifetime until handoff/join; simply storing its fields after the using scope disposes does not retain authority. Existing browser staging wrapper closes after Stage and must gain the separately reviewed held route. Ordinary browser is not MutationIntentRecorder; paid browser Reveal/Rewrite/direct-gacha are DIFFERENT source actions despite possibly sharing native storage family. No UI/bare DTO grants or refuses reservation.

## Proposed owner APIs and proof fields

Proposed files and seams, all UNACCEPTED:
- Services/GmRuntime/GmSessionRunCoordinator.PendingFateReservation.cs: ReservePendingFateFamilyAsync(OriginalStageCustody stage), TryBeginPendingPathWriter(OriginalWriterCustody writer, ExactMutationFootprint footprint), CapturePendingFateReservationAfterDrain, CompletePendingFateWriter, TransferPendingFateReservation and ReleasePendingFateReservation. Constructors private; no public API takes desired dice/JSON or caller-supplied proof flags. Coordinator owns the registry under its existing source gate, with explicit epoch and operation identity.
- Core/CanonicalRootIdentity.cs may hold only a local reference to that same coordinator-owned registry. It is not the cross-process authority. Core/FileSystemManager.PendingFateAdmission.cs validates the actual originating operation/lease and mutation footprint against that registry before effects. Every mutating family, recovery and generation replacement must enter through its real owner; no central method name alone proves complete coverage.
- Services/PendingTurnStateService.OriginalFrame.cs consumes the issued live reservation, retaining same-final-read bytes and original hot frame. Public GetOrCreate/Reveal/Rewrite/Rotate/Clear also need original writer admission at method entry before possible draw, not solely an FS write hook. Their existing caller/action authority remains unchanged.
- GmMainParticipatingControl.MutateAsync154 needs actual original command admission before157C, backed by the existing retained operation/connection. Cross-process messages can identify a reservation/writer ordinal, but server must bind them to the real current live pin/connection/root/G and original command sequence. A serialized identifier/ACK alone is never a capability. No new endpoint/key/service is selected here; exact existing-transport integration remains PB02 OPEN.
- FS family setters must become guarded owner transitions: actual MutationIntentRecorder install/remove, RunLegacyStorageRecoveryAsync enter/restore, BrowserLocalAccess create/dispose. Validate prior to installing recorders or publishing their manifests; the current assignments are too late to retroactively certify pre-RNG family.

The private reservation holds: actual coordinator/Access/pin/root/G/Stage/request references; exact O/I admission references; optional genuine browser original scope/Binding/Condition/issuing owner; exact logical pending path plus validated physical-root/namespace identity; issuer epoch; original acquisition ordinal; intended family descriptor {MutationIntentRecorder:none, Legacy:false, captured optional BrowserLocalAccess identity and its registered callback/debt owner}; outstanding predecessor writer/debt records; source gate phase; only this original fate child and explicitly delegated existing-debt handles; actual pending-acquisition/release tasks; transfer/close state. No copied bytes/hash substitute for any reference. A diagnostic projection can contain IDs/counts/path/phase only and is nonimportable; no new durable metadata schema or write is accepted.

## Finite exclusion predicate

The protected semantic member is exactly game_state/control/pending_dice_state.json. Conflict includes replace/write/append/delete of that member, type/identity changes of it, ancestor tree deletion/replacement/move (game_state/control, game_state, session root), any load/session-generation replacement, or declared namespace plan containing/intersecting the member. File-to-directory and directory-to-file transitions count. Mere identical after-bytes do not remove conflict. Backup/snapshot copies outside the protected target are not automatically conflicting writes, but their later restore to the target is. Parent-directory validation/read is not by itself mutation permission.

Compare paths through actual existing root/physical scope normalization and validation; Windows case/spelling aliases and Linux exact names follow their real owner. A string prefix test cannot establish physical authority; symlink/wrong-type/alias uncertainty refuses. Unknown/dynamic mutation footprints are conservatively conflicting until that ORIGINAL owner supplies a validated finite member/namespace plan. This is denial/exclusion only, not permission to write a namespace snapshot.

A writer admission lives from BEFORE its first domain/native/callback effect (including potential RNG and fee) through original publication decision, cleanup, namespace debt and actual release. Registration only at file publication would allow competing GetOrCreate/Rewrite to consume entropy before reservation denial. The candidate Stage fate child is excluded from predecessor counts only by its exact original identity and has not drawn yet. Other same-root/same-parent borrowers are not implicitly exempt; only specifically delegated original debt may cross the reservation without new semantic work. O/I owner frames are not counted as unfinished child writers, but their actual child acquisitions/operations are counted.

## Issuance sequence and lock order

1. Before domain effects, the original Stage child registers a reservation-acquisition task under parent source gate. The coordinator verifies original identity/root/G/access, actual browser scope when present, source Open, and that the applicant has no storage acquisition/lease or dependency on a child that must wait behind its reservation. No RNG is possible in PendingRequest.
2. Under that same coordinator gate install PendingFence(epoch, pending path, original child). All NEW conflicting writer admissions refuse/defer BEFORE draw/fee/publication. Already-admitted writers are tagged as predecessors, including acquisitions not yet granted, callbacks, native attempts, retries already armed, original restoration and release debt. Unknown writer/native membership prevents progression. Do not set a boolean 'empty' from currently held leases.
3. Release the gate and await actual predecessor completion/debt/lease joins; the applicant owns no C/lifecycle lock while waiting. Predecessors may finish only already-started work under their original authority. A stopped/failed/uncertain predecessor does not disappear from counts. Prior legacy/native recovery needs its actual existing issuer; PendingFence grants none. If that issuer is lost, issuance blocks and the task is not successfully recovered.
4. With predecessor set truly drained and source still Open, perform an explicitly registered bounded validation acquisition if necessary to verify current G/root, settle already-owned permitted prior storage evidence, and classify the actual original family's allowed transitions. Release that validation lease before declaring Issued. It is NOT a lease held across RNG and is NOT proof of future family by itself: the still-active reservation fence plus guarded transitions prevents future substitution. Recheck source epoch and predecessor closure after release. Callback/release failure remains real debt; no issue.
5. Atomically change PendingFence→Issued with the live family record and original browser owner still valid. Only now may the corrected hot-frame factory begin its same-final-read and then its first RNG slot. Preserve existing public read selection/decoding/hook behavior through the proposed observation seam. Every recovery effect under that read needs exact member/debt authorization; unresolved prior or unknown journal blocks the family, not ignored as a read.
6. For the later write lease, register acquisition BEFORE await; on acquisition verify actual G/root/main, reservation epoch/source child, optional actual browser Condition, recorder==null, !legacy and UsesTrustedLocalWriter for the exact pending path. Check captured BrowserLocalAccess/callback family identity if present. If family changed, retain original values, no native write/redraw. A seal prevents new acquisition/native arm; actual acquired resources still release through original owner.

Gate order is original parent source gate → reservation registry/frame state; never await or invoke provider code while holding it. Writer admission precedes C/lifecycle acquisition. A lower mutation hook invoked under existing C only performs a nonwaiting validation of a PREEXISTING writer ticket; if none or conflict, it refuses before side effects. It must not enqueue/wait for reservation under C. All real caller preflights are necessary so paid actions do not discover this after spending. Metadata stop still follows original native/debt/storage quiescence; it does not acquire the pending reservation or wait for semantic continuation while holding C. This protocol removes a proposed cycle only if every entry obeys it; it is not existing qualified behavior.

## Family transitions and browser lifetime

Future family stability is a prohibition enforced by the issuer, not prophecy. Under Issued reservation, leases for the fate child derive only from its original parent and captured family descriptor. Installing an actual recorder, entering legacy recovery, or changing a local browser storage access/callback scope requires a separately admitted transition owner with its full footprint before mutation. A conflicting transition cannot proceed while reservation is live; a preexisting transition must finish and release before issuance. Plain internal property assignment currently bypasses this rule and is a concrete missing implementation seam.

BrowserOriginalOperationScope and BrowserLocalStorageAccess are distinct actual owners. The first narrows the genuine main admission with action Binding/Condition; it does not make the writer legacy. The second is created under a lease, carries an actual generation and optional RecordIntent callback. Do not ban every browser path or every nonnull BrowserLocalAccess; validate its actual original ownership, allowed pending-member family and all callback debt. An unknown callback/provider/transition is a blocker, not a fabricated no-op. Paid browser transactions retain their transaction, fees, rollback/receipt and native ownership; the producer only schedules conflicting admission before those effects.

On browser held exit the current using scope atStage1565 would dispose when the method returns. The producer cannot keep a copied scope logically alive. Parent integration must retain the real scope/issuing admissions until exact held/join handoff, then dispose once under original owner. No ValidateOwner after disposed scope, no reconstructing Condition from equivalent JSON. This is PB01/PB07, not a finished browser-host implementation.

## Hot retention, handoff and release

The corrected consumer's per-slot RNG, clock and same-final-read/native outcome contract remains unchanged. Source seal latches Sealed on reservation and prohibits all unarmed source work. Already-returned values remain in the original frame; an already-armed Fill may return and be retained, but next slot cannot start. Already-armed native publication settles only exact original bytes/attempt/debt with its original owner. Reservation alone authorizes no new durability write after seal. Original publication Committed plus cleanup debt is not ready for storage quiescence.

Reservation lifetime cannot end merely at native file write: Stage later captures original/validation snapshot evidence from that pending value. Normal handoff must therefore transfer the SAME live claim, without an admission gap, to an actual OriginalStagePathCustody owner at the Ready/Attach boundary. That owner records the retained exact original fate result and completes the required later snapshot/request handoff before allowing conflicting next action/rotation. This transfer is a new producer/parent interface; its exact later release criterion is PB07 OPEN, not guessed from Attach returning. No second root pin or duplicate claim appears.

On Held, release storage only after real original debts and acquired/pending tasks join; keep the semantic claim and frame in original custodian ownership until lawful PF-P05 continuation or other accepted same-action disposition. Do not keep a lease/lifecycle lock merely to preserve values; do not block main metadata on continuation. Full main pin release/retirement remains original whole-parent conjunction. A detached reference/retained exclusion entry is not running work, but it is not successful ordinary completion. Keeper death/cold/reboot/migration/distinct-root restore provides no issuer in this design. A permanently stranded claim is a liveness blocker; neither eviction/timeout nor newG can erase it.

## Proof limits and principled dependency

A local dictionary, a new semaphore, a short preflight C lease, current file hash, or a caller boolean cannot issue this reservation. Proof needs control of every legitimate conflicting writer's entry and lifetime, including the separate helper process and unexposed native/main-agent writers. The source registry below is a finite actual-owner inventory and structural mutation boundary; it is NOT a complete audit of all dynamic/raw/native writer call graphs. W16 and lower-family coverage remain explicitly OPEN. If any supported writer can bypass this admission protocol, no reservation is issued and no first RNG starts under the claimed protected family.

The genuinely new authority proposed is operation-bound exclusion/transfer over all cooperating pending-path writers and family changes, attached to original coordinator custody. That is a lifecycle admission contract needing explicit independent engineering acceptance; it is not inferred from existing general canonical write permission. No new gameplay permission is necessary to preserve an already-authorized source action, and a missing helper alone would not be a principled grant. Cross-process/native enforcement, complete root membership and actual browser scope retention are the substantive missing owners. No permanent OS service, endpoint/key/credential or security-policy change is installed/proposed as automatically authorized. This report remains DESIGN only, conditional and not an engineering release.

## Preserved scope

PF-P01/P03/P04 now have a concrete proposed producer and an explicit writer registry; they are not marked closed. PF-P02 same-final-read seam, PF-P05 continuation, PF-P06 cold/keeper loss and PF-P07/full H-B02 still remain open. The earlier23 consumer checks are not rerun or reclassified. New checks below are FUTURE UNRUN. QTE reminder design and reader/F1 qualification are separate. No global host location choice, activation/F2, newQTE, DEP-C/I/L/globalH2/H3 closure, target authority or altered Mortal n/action/evaluation n+1 follows. D4/guided/visual/D8 remain preserved.

## Actual consumer and writer registry

All rows name source-backed entrypoints or structural mutation families. No composite/native frontier is certified a completed leaf.

### W01 — Stage ordinary console AND ordinary browser

Sources: `T1495/1497,1509/1510,1565,1596,4534–4539;D29–49`.

- **Trigger:** GetOrCreate reuses valid or generates fresh; same shared call, browser original scope additional
- **Effect:** Reads exact pending path; fresh24Fill+timestamps+write. Reuse still source read/recovery.
- **Required original admission:** Producer candidate itself; reserve before no-lease read/draw. Preserve real browser scope/Binding/Condition/issuingMainAdmission; neither UI label grants/refuses.
- **Coverage:** Exact source call + concrete service effect; host issuance OPEN

### W02 — PendingTurnStateService Generate/CreateFresh/GetOrCreate

Sources: `D29–49,124–191;G36–90`.

- **Trigger:** Missing/invalid branch of either public or actual lease overload
- **Effect:** One pending file write,20 visible+4hidden original draws. GameLoop generator alone writes no file; its other calls are not automatically pending writers.
- **Required original admission:** All service entries that may generate must enroll BEFORE first read/draw, not only FS write. Same original read/byte retention and actual native debt.
- **Coverage:** Application paths known; all caller ownership must be covered

### W03 — Explorer menu and direct gacha plus local command builder

Sources: `EXP613–628,2728–2742;EXPLOCAL3976–3997`.

- **Trigger:** Menu GetOrCreate in conditional Mortal branch; direct /gacha access; local builder instantiates its own service
- **Effect:** Even presentation/data building may generate and persist pool. New service instance does not isolate canonical root.
- **Required original admission:** Register potential fate mutation before GetOrCreate. Distinct original action retains its own result; no replay or semantic conversion to Stage.
- **Coverage:** Concrete additional callers; outer UI/transitive costs not closed

### W04 — Explorer Reveal Fate

Sources: `EXP1150–1173;D65–91`.

- **Trigger:** Confirmed action; GetOrCreate then DeductInkFeathers then Reveal. Reveal locks only when not already locked.
- **Effect:** Pool may be created; soul fee then fate lock timestamps/write; later display/input.
- **Required original admission:** Composite source admission BEFORE generation/fee. Reservation conflict must be resolved before fee, not by late FS rejection after spend. Existing paid owner owns fee/rollback/receipt.
- **Coverage:** Exact order shown; fee/native composite adapter OPEN

### W05 — Explorer Rewrite Fate

Sources: `EXP1200–1237;D94–112`.

- **Trigger:** GetOrCreate, require IsFateLocked, deduct fee, then Rewrite
- **Effect:** Always draws fresh locked pool after fee; earlier original pool retained for display.
- **Required original admission:** Enroll whole existing paid action before its first possibly generating GetOrCreate; no producer-issued rewrite/refund/retry.
- **Coverage:** Exact order shown; composite atomicity not asserted

### W06 — Browser paid Reveal/Rewrite

Sources: `WEB2020–2054,2096–2132`.

- **Trigger:** ExecuteAtomic owned soul+pending set; realm/fee checks, real explicit lease; Rewrite requires existing locked pool
- **Effect:** Soul spend + pending creation/lock/rewrite + runtime refresh and payload.
- **Required original admission:** Before transaction fee/draw: declare exact soul+pending member set and original action authority. Drain already-started original transaction; never split it or label browserOriginal scope a recorder.
- **Coverage:** Actual owners retained; ExecuteAtomic graph/rollback frontier OPEN

### W07 — Browser direct ChaosSea gacha

Sources: `WEB2175–2258;WEBQ184–203`.

- **Trigger:** ExecuteAtomic includes pending; GetOrCreate at2212, spend/refresh, QueueDirectChaosSeaGacha with rollback evidence
- **Effect:** Potential pool generation, soul spend, queued request/snapshot, browser backup/rollback cleanup may restore pending
- **Required original admission:** Register this real composite before first preparation/effect; exact member plan and descendants belong this action. Stage reservation cannot preempt its paid work.
- **Coverage:** Concrete additional writer; complete queue/transaction graph OPEN

### W08 — Accepted-turn rotation

Sources: `D115–119;T172,1060,1870`.

- **Trigger:** Existing accepted/finalization callsites only
- **Effect:** Fresh pool and write for NEXT turn
- **Required original admission:** Distinct original accepted owner; acquire writer admission before24Fill, not Stage frame continuation. No rotation as lost recovery.
- **Coverage:** All three static GameEngine callsites identified; surrounding finalizers OPEN

### W09 — Clear and live prepare-turn deletion

Sources: `D122;LIVE68–102,402–403`.

- **Trigger:** Public Clear deletes; production LiveTurnPreparationService PrepareBound acquiresC then ClearStalePendingDiceState
- **Effect:** Delete pending, including under original prepare-turn composite. No direct production call to PendingTurnStateService.Clear found by this bounded textual lookup; API remains callable.
- **Required original admission:** Live prepare action must declare pending overlap BEFORE C/cleanup. Existing public Clear must use same mutation admission.
- **Coverage:** Concrete callable/delete owner; no unreachable claim

### W10 — Engine/Explorer rollback and backup restore

Sources: `SS2366–2410;EXREST834–896;BACK75–95`.

- **Trigger:** Actual validated backup inventory contains pending, or pending is newly created outside baseline
- **Effect:** Restore exact pending before-image or delete new pending; generic backup restoration consumes backup and republishes target
- **Required original admission:** Original rollback receipt/member inventory; cannot bypass reservation as cleanup. Existing started debt settles before grant; later rollback needs own original owner, not Stage grant.
- **Coverage:** Structural membership rule; full restore/refresh callbacks OPEN

### W11 — Generic FileSystemManager mutations and original-tree removal

Sources: `FS840–883,1879–1920,1923+;LOCAL169–251,317–332`.

- **Trigger:** Exact pending path, ancestor game_state/control or replacement subtree contains it; dynamic planned writes/images/namespace
- **Effect:** Write/append/delete/copy/restore/move/import or ancestor tree destruction can affect value/type/name. No literal pending string required.
- **Required original admission:** Guard actual resolved path/member/namespace interval under writer admission BEFORE first directory/namespace/native effect. Unknown dynamic footprint conflicts conservatively until original owner supplies exact plan.
- **Coverage:** Finite mutation-family boundary; not exhaustive native API proof

### W12 — Separate-process participating control helper

Sources: `CLI26–40,77–124,154–185`.

- **Trigger:** --gm-main-operation creates own FS/process; command path can be any game_state/control/*
- **Effect:** write/append/delete exact pending accepted by NormalizeControlPath; directory operations also use physical authority
- **Required original admission:** Actual original retained connection/pin must acquire reservation-aware command admission BEFORE lease/effect. Local CanonicalRootIdentity table cannot cover this process.
- **Coverage:** Concrete cross-process counterexample to in-process-only reservation

### W13 — Load and session replacement

Sources: `LOAD195–244,434–456;LOADNS199–258;REPLACE24–90`.

- **Trigger:** Archive pending path explicitly canonicalized; actual Loading calls PublishLoadReplacementNamespaceAsync, not older image-only helper
- **Effect:** Replacement namespace and new G; clearGameState can delete pending; root/generation revision changes.
- **Required original admission:** Whole original replacement admission conflicts regardless whether final bytes equal. Must finish original stop/replacement lifecycle, never bind reservation to new G.
- **Coverage:** Actual namespace owner identified; full archive/cold lifecycle OPEN

### W14 — Recovery/native journal/namespace debt

Sources: `LOCAL15–22,169–251;PUB245–326;FS1319–1352`.

- **Trigger:** Read acquisition or cleanup invokes retained original recovery; previous native publication may still affect pending/ancestor
- **Effect:** Commit/rollback/restoration/cleanup; not necessarily a new high-level write call
- **Required original admission:** Register exact original debt/member scope before reservation; unresolved/unknown prior journal blocks issuance. Reservation cannot invent legacy or cold recovery authority.
- **Coverage:** Exact lower owner family; native full debt graph OPEN

### W15 — Family transitions / recorder ownership

Sources: `FS300–335;ROLL507–527,575–590;WEBLOCAL351,363,480;LOCAL15–22;BL103–115`.

- **Trigger:** Recorder install/remove; temporary legacy recovery; BrowserLocalAccess creation/disposal
- **Effect:** Changes future publication semantics and callback namespace even if pending bytes unchanged
- **Required original admission:** Guard actual owner-authorized transition before side effects; no public mutable property can bypass active reservation. BrowserOriginalOperationScope is separate and not a family transition.
- **Coverage:** Concrete assignment sites enumerated; complete transition enforcement NEW

### W16 — Worker purpose / main agent and raw native boundary

Sources: `WORK11–59;PROFILE156–170;CLI154–185`.

- **Trigger:** Worker dispatch/cleanup have purpose guards; pending appears as read input in soul profile; main/native/OS tools may have other dynamic accesses
- **Effect:** Profile read path is not write permission. Generic worker publishes exact declared task/audit only where enforced. No audit here proves every main/agent/tool syscall cooperates.
- **Required original admission:** Qualify all legitimate native writers under original scope/command gate or prove they cannot touch pending. Unregistered writer possibility blocks issuance; do not add OS/service/security controls here.
- **Coverage:** Explicit external/whole-writer blocker; not claimed complete from textual search

## Producer states

| State | Retained fact | Advancement | Limit |
|---|---|---|---|
| PendingRequest | Actual Stage acquisition task registered; no effects | Verify original identity/browser owner and no storage wait cycle | No caller-data issuer |
| PendingFence | New conflicting admissions blocked; exact predecessor set retained | Drain real predecessor effects/debts/releases without holding C | Unknown writer or lost predecessor blocks |
| Validating | Registered bounded original validation acquisition | Validate current G/root/evidence; actual release; recheck gate/epoch | No nested generic long lease or future-family inference |
| Issued | Private original reference, actual family transition fence, same owner/root/G | Consumer read and first RNG may be armed | No authority before this state |
| Publishing | Actual lease and family revalidated; registered exact native attempt | Original decision/cleanup/release joins | No unarmed postseal write/retry |
| TransferPending | Ready state plus live claim and original StagePathCustody receiver | Atomic same-owner handoff with receiver acknowledgement | No release/claim gap or DTO capability |
| Held | Progress retained; source sealed; semantic claim survives | Only existing joins/settlement until PF-P05 lawful source continuation | No permanent HOLD success |
| Released | Actual receiver/completed owner acknowledged lawful release; all native/storage debt joined | Close exact registry entry once | Timeout/PIDabsence/hash never release |
| Uncertain | Actual authority/debt/transport/lifetime failure | Preserve original evidence; no new RNG/issuer | Not recovered by new coordinator/G |

## Exact producer prerequisites

| ID / owner | Required closure | Status |
|---|---|---|
| PB01 / Original host/Stage/browser issuer | Actual retained main O/I/G/root and real browser scope lifetime before1596; no held-scope disposal/reconstruction | PF-P01/PF-P07 OPEN |
| PB02 / Cross-process writer admission | Existing retained helper connection/pin/sequence must bind new writer tickets; server-owned membership and refusal beforeC | Actual CLI bypasses local table; transport contract OPEN |
| PB03 / Lock/acquisition/debt coverage | Every source acquisition and transition registered pre-effect; no C→reservation wait; native debt release and stop metadata ordering | Prior conditional stop-order remains prerequisite, unqualified |
| PB04 / Complete pending writer/native coverage | All W01–W16 entries and dynamic namespace/native paths obey guard; main/agent/raw writers qualified or proved incapable | Finite registry is not full transitive/native closure |
| PB05 / Guarded family-transition issuer | Remove bypass by direct recorder/legacy/access setter; preserve actual original browser/legacy recovery owners | New admission enforcement contract, not existing permission |
| PB06 / Single original read and finite publication | Consumer observation keeps exact last-read bytes; same-before publication and original native attempt outcome/cleanup retained | Existing PF-P02/PF-P03 not implemented |
| PB07 / Release/transfer and successful continuation | Same claim handed to actual later Stage snapshot owner; exact release criterion and PF-P05 source continuation/remaining budget | No liveness closure; permanent HOLD unacceptable |
| PB08 / Keeper/cold boundary | Original custodian/native owner survival; original-work-stop proof after keeper loss/cold/migration | Outside hot producer; full G-C/T197 blocked |

## Future checks — UNRUN

- **PR-T01 FUTURE UNRUN:** Ordinary console and ordinary browser original owner issue identical family semantics only with real references; bare mode/DTO/AsyncLocal cannot issue.
- **PR-T02 FUTURE UNRUN:** Browser actual scope disposed, issuingMainAdmission closed/mismatched or Condition stale refuses before RNG; scope lifetime retained through held/join.
- **PR-T03 FUTURE UNRUN:** Concurrent GetOrCreate from two service instances: reservation admission before draw, exact predecessor drain, no losing pool consumed.
- **PR-T04 FUTURE UNRUN:** Explorer/local-command incidental GetOrCreate writers participate even when caller presents UI rather than an action button.
- **PR-T05 FUTURE UNRUN:** Reveal/Rewrite paid actions conflict before fee and first generation; already-started fee+fate composite drains its own receipts, no duplicate spend/refund/redraw.
- **PR-T06 FUTURE UNRUN:** Browser paid fate and directChaosSeaGacha transaction/queue/rollback retain original native and soul obligations; not admitted as Stage merely by trusted writer.
- **PR-T07 FUTURE UNRUN:** Three accepted-turn Rotate callsites are separate original writers, never resumed Stage draw; Clear and live preparation delete conflict.
- **PR-T08 FUTURE UNRUN:** Separate participating-control process write/append/delete of pending cannot bypass reservation through own FS/root interner or command sequence; helper EOF retains debt.
- **PR-T09 FUTURE UNRUN:** Generic path alias/type/ancestor directory/namespace replacement and dynamic unknown footprint conservatively conflict; exact unrelated snapshot/backup destination not blanket blocked.
- **PR-T10 FUTURE UNRUN:** Rollback/Explorer restore/backup restoration to pending drains original before-image receipt, no cleanup exemption; unresolved legacy evidence blocks issue.
- **PR-T11 FUTURE UNRUN:** Actual load namespace owner and session replacement conflict before newG/target publication; identical pending bytes do not preserve old reservation authority.
- **PR-T12 FUTURE UNRUN:** Recorder install, legacy enter/restore, browser-local access/callback change cannot cross Issued reservation by property setter; ordinary browser original scope remains distinct.
- **PR-T13 FUTURE UNRUN:** Pending canonical acquisition counted before await; no caller waits for reservation while holding or waiting C/lifecycle; metadata remains after actualstoragejoin.
- **PR-T14 FUTURE UNRUN:** PendingFence/last predecessor completion/validation release/source seal races cannot issue from a stale epoch or empty current-held list.
- **PR-T15 FUTURE UNRUN:** Same final public-read snapshot, recovery hooks and optional reread preserved; unknown pending recovery/native callback never fabricated empty.
- **PR-T16 FUTURE UNRUN:** Per-slot Fill return versus seal retains only original returned values; no nextunarmedslot or new durability write after seal.
- **PR-T17 FUTURE UNRUN:** Actual writelease revalidates family/reservation before native arm; all native disposition/cleanup/release cases remain separately owned.
- **PR-T18 FUTURE UNRUN:** Ready→StagePathCustody transfer has no gap/duplicate claim; later snapshot handoff release needs actual receiver, not attach success.
- **PR-T19 FUTURE UNRUN:** Held preserves real original claim without retaining C; PF-P05 continuation remains sameaction/remainingbudget and never reopens sealed old admission.
- **PR-T20 FUTURE UNRUN:** Unknown main/native/raw writer or lost custodian prevents issuer; no timeout/evict/newhost/newG/copy-root/hash substitute; no false successful ordinary outcome.

## Source pins and exact comparison

Selected source diff `/tmp/t197-pending-fate-family-reservation-producer-design-20261016-source.diff`: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855`, 0 bytes. This compares only the32 named production files, not independent reader/preparation/runtime work. No external remote read was performed.

| Alias/path | Git blob | SHA-256 | Bytes | Unchanged |
|---|---|---|---:|---|
| D `BookOfEternityClient/Services/PendingTurnStateService.cs` | `74dfbbcecfbae0d4671dbfe28078c18d5f743b38` | `ae7afcfddf00b69e4a6dc77fedf456f20213ed9b4436e6391c8a74ee03b584d5` | 6769 | True |
| T `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | `2c017c657d4d3d36be146d052c8a158e42d163c0` | `e2ea305eee08627d224e91fbf10ea274fafb0197cc0c6e54ed03fc5e7db4190a` | 294934 | True |
| G `BookOfEternityClient/Core/GameLoop.cs` | `c89bb3098f4b4c00b8f683047f9b0ecd1699dc3e` | `2b65cc471048f7f68252e1262a6fe5ac62b142f21c61bae9c594c5da5e3ea90d` | 2673 | True |
| ROOT `BookOfEternityClient/Core/CanonicalRootIdentity.cs` | `7152787bd91c20630deda394d7a32f52f6a82867` | `d6e1bc480d50227ce43d1703e48fee9cf19bb45d859f5bd937a2a3432751d928` | 4859 | True |
| MAIN `BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs` | `3f892cc44d59d81d49d1cba3b4f8199c5d92d33a` | `384f9f65d33e1c70c6f7e337d96aec6e71664d142612546139345bf274923a14` | 20796 | True |
| CTX `BookOfEternityClient/Core/SessionOperationContext.cs` | `aa34f9fa8581384a007c1d73b33d927fdabecdd6` | `5b0b98825d0e4f0c6e91a3396b3e54acdda7e774f6afacfcf3fb49b18c155efa` | 23072 | True |
| BO `BookOfEternityClient/Core/FileSystemManager.BrowserOriginalAdmission.cs` | `50d49a20cdf7305d04412b35f25998f01e8448f4` | `1decb1beb44d2fd0466199e58ff06fb6b39e7795341493fe8da09a977ec8d202` | 29382 | True |
| LOCAL `BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs` | `22d07c130ea56b5cab5ff3bbf04c2653c8ff0305` | `7154852b8ef29afa1262cef0e4bb3a0ff28f14afeebe98c2a0834676e4c5cf5b` | 19220 | True |
| FS `BookOfEternityClient/Core/FileSystemManager.cs` | `e6f7852156ca2a9c1f77fcb961f71068562d5257` | `c134530a5ef7d384146c4fac263dc96ba7bde4e83e79673027a2cad9387a514f` | 241886 | True |
| READ `BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs` | `022181b24c6e33e1be5d79cb6f705808a87c595a` | `094d4d2f2c2cf7abbf959165c9f2722f4057902f9ec0f78edc4b9ed9bc87a148` | 13822 | True |
| PUB `BookOfEternityClient/Core/TrustedLocalFilePublication.cs` | `99081f43140f8f59511d26666e19a6fa57544983` | `b0893b8b12f46f9c76e8b49b764fbac7343e69daea5c058dccd7c70ce570987f` | 33582 | True |
| REL `BookOfEternityClient/Services/CoordinatedStateWriteHelper.cs` | `f8accbc3e70feb6e9de4a5bc3337b68e11ba2d14` | `5b79e2b7d42839ff6dadc0a67040e40ac4b2bf0a18fd589d2b66ffd22cff97b4` | 14216 | True |
| COORD `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.cs` | `7df60d8902617d71daaa28479c4c79a37744d67b` | `eeb23241dff44ce49a484319a76e493d607de4413e1355f9b853ed41c6c7b0b6` | 24413 | True |
| PIPE `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.PipePins.cs` | `08ecdab49b89cf0c36cf6cf391c17a46a4db9b9e` | `2473c1f0b83bf142f34ec76ea613127ee8d3ef706c339192e28d9bb907ccad1d` | 5388 | True |
| CLI `BookOfEternityClient/Services/GmRuntime/GmMainParticipatingControl.cs` | `527dd1f4f99f16faef164d0e5ee91832636632a1` | `223ff5442373639857570fa6a86b3c0b9a85e3ffd51697699c19afb171045202` | 10981 | True |
| LIVE `BookOfEternityClient/Services/LiveTurnPreparationService.cs` | `ce2d9cfebc301cfb6ce678a35df43b65311396e2` | `eea0a8dc65e14bd2234774eb6127b7b9ca16a01583d22d22ea184588fcdcbbd7` | 20289 | True |
| EXP `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.InkFeathersAndOfferings.cs` | `e173e90939701f39d927f65d2d583555bb61615f` | `dae8ca299532f3b5db0277e7017be2dfc3ab38c4182c8665e4a25a5654648257` | 155530 | True |
| EXPLOCAL `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` | `469a367355b1fb8298e95782e736fd87ffd836a7` | `3ebbb3944a626ae82388ed608c87041536f1a354ce16a32ce02e8c9aa4848415` | 283076 | True |
| WEB `BookOfEternityClient/WebUi/BrowserAfterlifeWriteService.cs` | `c3d4a605c35a68200290e6a1335066f17350de83` | `f3ed3ac34ec28d201c1da63b1316496734d23e3b6d9befd075b1dbe771d9fb1f` | 241907 | True |
| WEBQ `BookOfEternityClient/WebUi/BrowserAfterlifeTurnRequestQueue.cs` | `58f30ca196c88d2b0ef279993de7c058de135e99` | `8864b4e44b0d4e104585d92f299e72c97f9f685e1279c4458cb6cbbc84758f49` | 17577 | True |
| WEBLOCAL `BookOfEternityClient/WebUi/BrowserLocalWriteCoordinator.cs` | `91c6e761c290d9feaa97cf1dabcd52eb8785d84c` | `95d98475770283a31e96f7d51aafba3bbfb348d9143f5f61598e1c0327db5450` | 30336 | True |
| BL `BookOfEternityClient/Core/FileSystemManager.BrowserLocalStorage.cs` | `902a0c93e8ceb503bf5467a4bc1638b6cff20259` | `1e1bca8520a3e65d2177a9819af87f10adbcef65a7fb77fb2628a0bcb322abb7` | 6476 | True |
| ROLL `BookOfEternityClient/Services/ExplorerLocalTurnRollbackArtifacts.cs` | `6e22b319d73096dce6cd00a6d28f602164c7796e` | `fe8da16e44de41740550260583bfb9ae1bb170bed98bd70c903bcd9f5b8ab556` | 84903 | True |
| SS `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` | `9a000fc10c0778e838c5dd4b9d5ad6e1fecb1ca0` | `85ad957b7fa801930a7d4f13d6abd24e96b2e877d00544c5e74aad4440be6b1e` | 112701 | True |
| EXREST `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.PrivateImplementation.cs` | `0ed55370267c4b5e82ca59a81067e2d492df7f61` | `84af40b35a48fc499adf1c2434ce2846befedd3b7ed4370369163df146f7a06d` | 92399 | True |
| BACK `BookOfEternityClient/Core/FileSystemManager.BackupLifecycle.cs` | `eaa988979b4e709a2a5b4abdd3e24b65d7aa4f84` | `71954c0c0e0b73c989208d870470ea9ab7906c9e28aea8d42c06923db90a507f` | 7681 | True |
| LOAD `BookOfEternityClient/Services/SaveLoadService.Loading.cs` | `3e634af234ddfb26b0861fb4dd3e47f3eed63516` | `a88d3deb51e378beb24d2152b3cbff4dd0f885518af50668c3043ff0c8243251` | 27722 | True |
| LOADNS `BookOfEternityClient/Core/FileSystemManager.LoadNamespace.cs` | `64a8cf5fb7b1d4d040fd9678eeb1f5ac6c1ce8a2` | `228cb24f1ebbce770a328c0ab21b203980d7b7b0039895ec0571bad04d0fa82a` | 15169 | True |
| LOADOLD `BookOfEternityClient/Core/FileSystemManager.LoadReplacement.cs` | `f55330f7d0b68f17585c8912c90054520e8e5b7c` | `a3c3125953985a95996c7eb21295f3e9f1daa8156e4b97150860854aa5e85941` | 6154 | True |
| REPLACE `BookOfEternityClient/Core/FileSystemManager.SessionReplacement.cs` | `d940d9846796cd05589d1a9142e4bb29f7bc9d9a` | `beac7a4733bc8de2816c3acedc365b69be19a048efff513cb47ee60c82ef1d21` | 9987 | True |
| WORK `BookOfEternityClient/Core/FileSystemManager.WorkerCanonicalFence.cs` | `e934fbca58256b1d0ebb57416450978ff644fe48` | `7a857ec50550365f6576b8e2ccc056e662c87622e1a32f652952760761d3e753` | 3674 | True |
| PROFILE `BookOfEternityClient/Services/GmWorkers/GmWorkerBridgeProfileTemplates.cs` | `f35085a49a53eb265b3428abd7a688b61ea5dc01` | `b67ac3a85819ffabf1df34f4dbf2a9818edd50761721125bbd17f818756c1193` | 9888 | True |

## Immutable accepted-conditional context

- `specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-pfr1-corrected-design-20261016.md`: blob `28f40b5d47c6ab26da1a98b60610acdc795b29bb`, SHA-256 `0717e9e428b442027505abceec203d76d98bdef79633a22aec43b8a54a0a27f6`, 52152 bytes. Conditional consumer review does not accept this producer.
- `specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-pfr1-corrected-design-20261016.json`: blob `f472d7bb0e64396b0c5c653adc74de846db3fd32`, SHA-256 `5ad003b44148e146c1acc3baa465d8d045e9ad9f2c6514a19ce1d4719db283ab`, 147940 bytes. Conditional consumer review does not accept this producer.
- `specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-pfr1-corrected-independent-review-20261016.md`: blob `a66ef31ee33592aba2fea355126b54560150665c`, SHA-256 `37213767ea6c168d64740c84c96f44db301f5ed7606dfcd45d0edcec08047708`, 14013 bytes. Conditional consumer review does not accept this producer.
- `specs/1536-complete-wound-materialization/recovery/t197-stage-pending-fate-original-frame-pfr1-corrected-independent-review-20261016.json`: blob `9696d34313f91fb8f78b0e428874eaec57813e50`, SHA-256 `d0673ff2f34e136c80c6d70df2d2e6dda38c541c899fcdb0523cbeb61aef7e40`, 65179 bytes. Conditional consumer review does not accept this producer.

## Final stop

16 concrete writer/consumer families; 9 protocol states; 8 open producer prerequisites; 32 historical production pins; 38 exact source projections; 3 hashed bounded static searches; 20 FUTURE UNRUN checks. No current issuer or complete writer coverage claimed. New /tmp artifacts only; previous author/review pairs immutable. No code, repository, runtime/native, build/test/discovery, network, fetch/push, OS or delegation action. No pending writes after freeze.
