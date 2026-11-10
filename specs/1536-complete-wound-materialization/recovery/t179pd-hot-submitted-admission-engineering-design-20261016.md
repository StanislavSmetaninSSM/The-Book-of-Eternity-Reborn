# T179-Pd step 3→4: implementable bounded HOT custody and publication-attempt owner

**DESIGN for independent review, not implementation/evidence acceptance.** Source `3fdc16e8a954458f1e56bafa42ca24a66c06d020`. Assigned Astra/High; serving backend unknown. Repository read-only; only this new /tmp pair is written. No build, discovery, test, application, network, OS mutation or delegation. Existing accepted reader/storage 131 PASS and fresh 986-file recovery are parent-reported prior evidence and were not rerun or requalified here. All tests proposed below are **FUTURE / UNRUN**.

## Decision: close one real owner vertical now

A HOT-only path is possible without durable P/A control or original rewrites. Complete full submitted-current admission is **not** released by this design. The immediately implementable unit is:

**actual original producer → in-process original lineage → actual accepted-Ready observation and completed consumption → current engine canonical attempt → accepted observed-original reader consumed by existing mechanical admission → actual reservation-owned no-publication disposition.**

This is a source-custody and attempt-ownership contract. Its success is called `HotSubmissionCustody`, **not** `AutomaticAdmission`, `AcceptedSource`, S, B, Full validation, or NoPreviousRepair. The positive case proves changed stats/level/output were submitted after the actual original and retained through the real repair route. It does not authorize those changes. Existing v2 mechanical equality/INACTIVE refusal still applies, so that positive may end with an honestly unattempted held v2 refusal.

This is more than an unused helper: the actual v2 `PublishCurrentMortalJointRepairAsync` owns the new attempt, the actual Ready reader/deleter creates the custody record, and `CreateMortalJointRepairMechanicalAdmissionAsync` consumes the newly observed original evidence. Actual v2 preparation/reservation/preflight/take/abort use the same retained attempt. There is no new synthetic ACTIVE flag, production test callback, UI route or fabricated upstream acceptance.

Full HOT S requires the seven output roots/73 IO leaves **plus** raw/domain/player/level/time/automatic/freshness policies and complete bound-read/volatile handling. The existing read-closure report is an inventory, not a completed implementation contract for every non-emitting comparison. This unit deliberately leaves full semantic admission UNAVAILABLE. After its coherent review/TDD/implementation, continue with policy extraction in the same real owner; do not stop after custody or claim full Pd.

The missing shared validator/projection APIs are routine engineering under the authorized direction, not a new principled permission requirement. Cold R01–R08/P0/M3 and public activation remain separately blocked.

## Exact current control flow and the suspected cycle

Source aliases and precise projections are in JSON.

1. TL1484–1555 `ProcessPlayerTurn` calls actual `StagePlayerTurnAsync`; console retains ordinary participation, browser exits staging ownership and later enters a new actual browser original operation. TL1556–1742 does health, backup, actual TurnRequest/default values, reminder, progression, dice, preview/catalog, original creation, optional computed context, final request projection/staging, then validated current original.
2. SS410–592 is the real original producer. It captures original/rollback sources, builds manifest+detached authority, writes them sequentially under the actual lease, then returns. It does not provide an atomic new P/A issue owner. HOT provenance can originate here without claiming those writes are atomic or changing bytes.
3. VR517–807 is the accepted-turn raw loop. It calls raw owners, resource/joint intake and real repair/resubmission. The corrected joint path reaches VR809 → VR2272–2286 → CP18–120. VR850–858 then returns through dedicated joint post-publication validation. **Do not assume generic VR199–390 Full payload validation has already accepted this corrected submission.**
4. `WaitForContractRepairAsync` VR3460–3567 reads actual Ready, checks genuine current context, appends actual trajectory, then deletes Ready before returning. A later absence is not a submission witness. Worker-direct can return earlier at VR3358–3361; it needs a different real owner proof.
5. CP28–41 acquires the real canonical lease, reconstructs raw intake and rejected capture, then creates the joint continuation. CP44–77 reserves/builds prepared/effect/final/anchors/mechanical/baseline/resource/progression/common. CP82 preflights; CP89 marks delegation; CP90 enters the actual coordinator. The ordinary catch conflates prepublication refusal with PublicationUncertain, so neither that enum nor null Plan proves NoPublicationAttempted.
6. WP134–208 `CreateJointRepairPreparationAsync` rejects concurrent resource/effect commands, reconstructs the actual held treatment/corrected CREATE, and captures current guard beforeimages. It does **not** impose automatic-current equals original or invoke RequireInactive. MC115–145 imposes those later. Checkpoint CPt98–174 reconstructs the genuine request and `AcceptedState.ExportCurrent` AS434–490 reads signed original treatment sources. No automatic-input circular dependency was found before the mechanical gate.
7. AR217–246 reserves the actual exclusive publication slot. It admits a new private reservation only for the live accepted state and no prior reservation/restart block/publication fingerprint. “Exact” with null Authority is not permission to manufacture one. AR272–277 abort is currently void/no-op when ownership is absent. NR30–40 preflight does not take; NR96–108 delegates to real take, which owns the later uncertainty boundary.

Thus **do not add a second raw-publication reservation family for this unit**. A private capture gate can exist before continuation/prepared construction while the actual lease and genuine rejected capture own the read contour. It grants no take, reservation or publication. The existing reservation is acquired once at its present position and then owns the no-attempt disposition. Full future S must close before continuation/prepared/cache seals that are to bind it; it cannot be attached later because the current v2 candidate happened not to publish.

## Two deliberately separate permissions

| Fact/object | Earliest issuer | What it permits | What it never permits |
|---|---|---|---|
| `HotOriginalLineage` | Actual original producer, completed by real Stage return | Attribute this in-process original/staging operation | Parse an old/helper original into a new seed, resurrect browser scope, assert preparation cold recovery |
| `HotAcceptedReadyCustody` | Actual successful Ready branch before deletion, completed only after actual deletion/lease close succeeds | Retain exact observed submitted source and consumed-Ready provenance across lease turnover as data linked to the same live engine origin | Full output/automatic acceptance, reusing old reader evidence on a new lease |
| `CapturingGate` | Actual CP attempt before `CreateJointRepairPreparationAsync` | One bounded custody read; future full source capture only before gate is irreversibly closed | Attach S/B to any existing v2 prepared/cache/common candidate |
| `NoNativePublicationAttempted` | Actual reservation owner verifies exact live attempt and closes its unattempted slot | Report/retain this attempt's pre-delegation abort; no common native publication was called | Restore unrelated sources, rearm/take, prove no earlier repair/preparation write, reopen Capturing, import S |
| `OriginalReadEvidence` | Accepted reader with actual files/lease/G/Snapshot | Recheck exactly that original-read closure on exactly that lease | Full semantic admission or capability surviving lease disposal |

The distinction is mandatory even if both booleans would be true during an early capture. **A sealed v2 candidate may be unattempted but is no longer source-capturable.** No code path converts NoNativePublicationAttempted into CapturingGate.

## Bounded files and concrete APIs

Task identifier after the parent recorded this scope: **T179-Pd.3b**. The parent's subsequent documentation-only checkpoints do not change this report's frozen production source.

Proposed names below are fixed for the reviewed unit; normal private naming adjustments are routine. No caller supplies root maps, accepted flags, issuer objects, paths, time delegates, exemption lists, Frame or GameResponse.

New `Core/GameEngine/GameEngine.MortalJointHotSubmission.cs`:
```csharp
// All construction/factories private to GameEngine; exported diagnostics are DATA.
private sealed class HotOrdinaryOriginal;
private sealed class HotAcceptedRepairReady;
internal sealed class MortalJointPublicationAttempt; // private constructor; AR can name, never construct
private sealed class JointSourceCapturingGate;
private sealed class JointHotSourceCustody;

private HotOrdinaryOriginal BeginHotOrdinaryOriginal(
    string actualGeneration, PendingPlayerActionService.Binding? actualBrowserBinding);
private void ObserveHotOriginalIssued(HotOrdinaryOriginal owner,
    FileSystemManager.CanonicalWriteLease actualLease,
    PendingTurnSnapshotManifest actualProducedManifest,
    string actualManifestJson, string actualAuthorityJson);
private void CompleteHotOriginalStaging(HotOrdinaryOriginal owner,
    PlayerTurnPreparation actualPreparation, string actualFinalRequestJson);

private Task<HotRepairReadyRead> ReadHotRepairReadyForSessionAsync(
    string expectedGeneration); // fixed Ready path, real current engine origin or unavailable
private Task<HotAcceptedRepairReady?> CaptureMatchingHotRepairReadyAsync(
    HotRepairReadyRead read, ValidationRepairReady actualParsedReady,
    PendingTurnSnapshotResolution actualResolution, string expectedGeneration);
private Task DeleteAcceptedRepairReadyAndCompleteHotCustodyAsync(
    string expectedGeneration, HotAcceptedRepairReady? actualProvisionalCustody);
// Fixed Ready deletion, actual existing match branch only; null follows old deletion behavior.

private MortalJointPublicationAttempt BeginCurrentJointAttempt(
    FileSystemManager.CanonicalWriteLease actualLease,
    ValidatedPendingTurnSnapshotContext actualPrivateFrame,
    MortalJointRepairCheckpoint.RejectedCapture actualCapture);
private Task<JointHotSourceCustody?> CaptureCurrentHotCustodyAsync(
    MortalJointPublicationAttempt attempt); // once; null means explicitly unavailable, not acceptance
```

Private `HotRepairReadyRead` is an owning invocation object, not a public bytes record. It stores exact raw read+decoded text and whether the genuine hot origin was available. Existing old/cold/helper paths retain their existing read method and behavior when no hot origin exists.

Modify TL/SS to carry the same private original owner through actual ordinary Stage and original publication only. Use a private overload/wrapper for the ordinary producer; no general optional `seed=true` parameter and no source-label string interpreted as authority. Modify VR at the matching Ready cut and actual dispatch/unsupported-route observations, CP for the retained attempt, MC for the actual observed-original consumer. Modify AR with an owner-checked unattempted-close result; do not make all existing abort callers silently produce new proof. NR transaction semantics stay unchanged; CP marks delegation before entering NR96.

The accepted reader's public/internal signatures, parser/core, 64-selected-path policy, physical callbacks and default readers need **no change**. FileSystemManager's existing snapshot/bytes/explicit-lease APIs suffice. Use the existing internal FileSystemManager.DecodeFileBytes (FS1537–1545), already factored and used by the ordinary text reader. No new FS API, decoder, filesystem abstraction or strict BOM/UTF8 policy.

### Original owner: exact lifetime and retained fields

Begin the private owner inside actual `StagePlayerTurnAsync`, after actual generation/browser entry is established, before that invocation calls the actual original producer. It has:
- exact engine/files object identity and existing G bytes/id;
- actual source eligibility: ordinary GameEngine Mortal producer, never a parsed source label alone;
- actual browser binding reference/data or null for console; no retained browser scope/lease;
- phase Beginning → Issued → Staged → Ended/Unavailable;
- exact produced manifest JSON bytes/text as supplied to the real writer, exact authority JSON, manifest token/tuple/action/request timestamp/dice/gacha/progression copied from that invocation;
- final request JSON and real PlayerTurnPreparation reference once Stage has validated it;
- monotonic in-process operation ordinal and route-custody state, **no generated domain ID, timestamp or replacement request ID**.

At SS after BOTH writes returned, call ObserveHotOriginalIssued with exact producer values. It records returned issuance only. It must not call the original writer again, edit the manifest, read old files to mint a seed, or claim atomicity. If write/lease close/Stage validation fails, owner becomes Unavailable and never Staged. CompleteHotOriginalStaging is called only immediately before TL1742 returns its actual PlayerTurnPreparation. For browser it retains data/reference lineage, never the disposed `BeginBrowserOriginalOperation` scope. Later consumption requires the real current browser/MainAdmission and canonical lease.

At reset/new actual original/G change, end the old owner. Preserve only a detached last-diagnostic snapshot if needed; it has no live authority and cannot make the next owner Staged. At cancellation/failure/terminal cleanup, end or invalidate by actual control-flow event; do not erase physical evidence or generate a new original. An old origin retained accidentally is harmless because every use checks engine/files/G/tuple/token/current source and actual stage reference. Cold engine and LiveTurnPreparation helper have no issuer and stay HotUnavailable while existing v2 follows its existing rules.

This is an actual hot issued-original lineage, **not** a complete pre-issue preparation history. The QTE/reminder/health/default-value/cold-frontier R01 records remain unproved; no preparation rollback/replay follows from this object.

### Accepted Ready: exact read, consumption and branch coverage

At VR3460, if the engine retains an eligible actual Stage origin, replace only this fixed-path text read with the following owning operation. **There are three separate leases**, not one scope held through context resolution or trajectory publication:
1. **Read lease Lr (VR8373 wrapper):** check actual G/current engine origin, call existing `ReadFileSnapshotAsync(lease, ValidationRepairReadyPath)` once, and decode that exact Content with existing `FileSystemManager.DecodeFileBytes`. This is the same final physical snapshot read and BOM behavior used by ReadFileAsync; no second substitute read. Return raw snapshot plus decoded text after Lr closes. Missing and present-empty differ. Then call existing ReadValidationRepairReady and ResolveActivePendingTurnSnapshotContextAsync in their unchanged order. Malformed/mismatched branches do not call the new accepted-capture method.
2. **Capture lease Lc (new private wrapper, only after existing match succeeds, immediately before AppendAccepted trajectory):** acquire a fresh actual lease, check G/current origin/current browser condition, read Ready physically and require exact agreement with Lr's bytes. Run the genuine ReadObservedCurrent for the six original paths (soul required; the other five optional under the existing path-selection policy), all its actual authority/context/rollback closure, and compare its real binding to the actual matching resolution and original producer. Capture the fixed nine current roles and current repair request/final input/turn_complete/G under Lc. The six input/current output files are custody bytes; do not add a new JSON/level validator here. Existing Ready/original parsers retain their policy. Capture every operation, physically recheck it and Evidence.RequireCurrent before closing the provisional object. Close Lc; if closure faults, this optional HOT channel is Unavailable. No live reader authority survives Lc.
3. Keep the existing AppendAcceptedValidationRepairTrajectoryAsync call in its exact position, using the same arguments and its own existing ownership. It must not run under Lc or be folded into a claimed transaction. Its returned result is an observed route event, not Full validation.
4. **Deletion lease Ld (VR8336 owner):** use the existing actual deletion wrapper with a private accepted-custody overload. Check G; if provisional custody exists, physically compare Ready and the fixed source/original/control closure with Lc's detached observations before the existing deletion. Obtain a genuine new original-reader result if the original closure is checked, never revive Lc's evidence. Any mismatch/fault invalidates HOT custody; keep existing v2 deletion behavior and diagnostic order. Then perform the same fixed Ready deletion. Mark the custody Consumed only **after** deletion returns and ReleaseOwnedLeaseAsync returns successfully. A finally/release throw, loss of return, replacement or uncertainty cannot issue consumption. No inference from absent Ready.
5. At the later canonical attempt use another fresh actual lease Lp and genuine observed original evidence. Require exact unchanged submitted/original/request/control/G observations and Ready absent, which is a current condition linked to the actual completed consumption event, never its substitute. New/mismatched Ready, intervening source change, fault or unsupported route makes HOT unavailable.

No lease is held across the user/GM wait, generic context resolver, trajectory publication or return to the canonical loop. Lr/Lc/Ld observations are historical in-memory data tied to this private invocation/origin. Only Lp's exact Snapshot+Evidence can satisfy the new live canonical consumer. Added reads are confined to the optional HOT channel and cannot become gameplay acceptance. Closure failures do not preserve a partial success.

Read-source faults used only by this new *optional custody* channel latch HotUnavailable and cannot become successful absence. Do not translate them into new ordinary semantic errors or suppress old ordinary diagnostics. Existing physical SessionReplaced/coordinated-publication uncertainty still propagates by its owning path; the channel cannot catch those and continue as accepted. The original Ready parser/current-context/trajectory/deletion logic remains authoritative for v2 behavior. No extra clock sample is needed for custody; existing snapshot mtime is an observation only, not a freshness policy.

Every actual branch has a finite custody disposition:

| Existing branch | HOT disposition | Existing ordinary behavior |
|---|---|---|
| Ready missing/malformed/mismatched | No accepted custody; retain rejected observation only, invalidate any earlier pending custody | Existing reissue/error/retry order unchanged |
| Matching Ready, trajectory and deletion/close returned | ConsumedReadyCustody after exact source checks | Existing return true unchanged |
| Source changed during optional capture or before deletion/current canonical use | HotUnavailable(SourceChanged) | Existing v2 checks still decide v2; no full S |
| Ready deletion or lease-close uncertainty/G replacement | Unavailable/owner failure, never consumed proof | Existing exception/debt handling preserved |
| Worker direct success without Ready, or Ready manufactured by worker | HotUnavailable(WorkerDispositionNotIntegrated) for this unit | Existing worker success and ordinary path unchanged; do not reject it |
| Session cancellation/stall/error/diagnostic-only/rejected restore | HotUnavailable(named actual route) | Existing owner settlement/cleanup unchanged |
| Subsequent repair attempt | Previous custody cannot be silently overwritten as same submission; new private invocation ordinal only from actual new Ready branch | Existing retries unchanged; no retry cap |
| Existing parsed/checkpoint/cold/helper frame with no hot Stage lineage | HotUnavailable(MissingLiveOrigin) | Existing v2 remains usable under its own rules |

Worker-produced Ready cannot be assumed ordinary GM submission just because its tuple matches: the real dispatch outcome must tag that invocation before the wait path. The closed branch tag comes from existing actual `ValidationRepairDispatchState`, not caller JSON. Any unknown/unsupported route marks HOT unavailable. This unit does not infer complete canonical repair history from an empty collection.

### Fixed custody source roles and closure

The role enum is closed and source-owned:
```text
OriginalManifest, OriginalAuthority, ExistingGeneration,
CurrentTurnRequest, CurrentRepairRequest, CurrentTurnComplete,
AcceptedRepairReady,
OriginalStatusChanges, OriginalExperience, OriginalPlayerStatus,
OriginalSoul, OriginalCharacteristics, OriginalStatPoints,
CurrentStatusChanges, CurrentExperience, CurrentPlayerStatus,
CurrentSoul, CurrentCharacteristics, CurrentStatPoints,
CurrentNarrative, CurrentInterface, CurrentDebug,
OriginalReaderOperation (nested genuine reader provenance, never caller-imported).
```

The six exact logical paths are:
`game_state/player/status_changes.json`,
`game_state/player/experience.json`,
`game_state/core/player_status.json`,
`game_state/meta/soul_state.json`,
`game_state/misc/characteristics.json`,
`game_state/player/stat_points.json`.
The three output paths are `output/narrative_response.json`, `output/interface_updates.json`, `output/debug_logs.json`. Original/current malformed JSON in an otherwise readable file remains exact custody bytes and may later be rejected by existing ordinary validation; custody introduces no new JSON acceptance policy. Original reader retains its actual manifest/authority/rollback callbacks/all three context-presence+text operations/selected original operations, in real order. These nested operations are not capped by the six logical selections and are never reconstructed from a map.

Each occurrence retains role, original/current provenance, actual internally derived coordinate, presence, exact bytes, decoding status, ordinal and outcome. Ready snapshot's LastWriteTimeUtc is retained as an observation without turning it into a new freshness rule; no clock sample is added. A private collection may store these; there is no constructor/import API from such data and no generic path-reader exposed to callers. Callback/rollback physical paths come only from the accepted original reader. Original absence, missing declared artifact, unreadable current, empty bytes and JSON null are different states. Every observed fault latches; every repeated read is real; final source recheck runs before a successful custody object. Later canonical matching repeats physical checks and observes actual G/current MainAdmission. No directory reader, metadata freshness authority, seven-root validator or arbitrary current-root fallback is introduced in this unit.

### Actual canonical attempt, capture gate and reader consumption

At CP after genuine intake/current rejected capture and **before** CreateJointRepairPreparationAsync:
- construct `MortalJointPublicationAttempt` privately from this engine, actual files/lease/G/private frame and genuine capture;
- reject overlapping live attempt/reentry; retain the object on the engine for failure/debt ownership;
- mint one `JointSourceCapturingGate` belonging to that attempt. It is live only in Capturing and can be consumed once;
- match current source to the genuine HotAcceptedRepairReady if available; use fresh ReadObservedCurrent with the custody selection, retain exact Snapshot+Evidence, verify both against actual frame/origin and current closure. If unavailable, record only HotUnavailable; no new source authority;
- consume/close CapturingGate **before invoking any joint preparation, prepared/effect/final cache or common seal**. This closed custody is not full S and cannot later be “upgraded” by adding semantic flags.

Then invoke the existing continuation factory and reserve at the existing location. Retain the exact continuation/reservation references and subsequent prepared/effect/final/common/preflight references on the attempt, assigning each once from its actual returned result. No re-reservation, repeated reducer, regenerated identity or clone may satisfy an occupied slot.

MC's real consumer is changed narrowly: use `ReadObservedCurrent` with its **existing exact selection**, same required soul path and optional guard paths. Check the same original/frame/binding predicates and execute existing current-read/equality/concurrent-command/INACTIVE rules in their current order. Keep its two loop boundaries and same late continuation revalidation. Before issuing its existing mechanical proof, call the exact evidence.RequireCurrent on the same lease/Snapshot; retain exact OriginalReadEvidence and its issued PendingTurnSnapshotReadAuthority as readonly fields in the existing proof and call evidence.RequireCurrent(files, lease, exactSnapshot) during subsequent ValidateCurrentAsync after its existing owner/G guards. Do not replace current reads with custody-time cached bytes, merge six-input selection with a >64-path mechanical selection, or silently widen the old selection. The custody-time and mechanical-read observations are separate named original operations; each is independently closed and bound, and cross-operation overlapping original/source bytes must agree if both are available.

This is the accepted reader's real engine consumer. It strengthens source closure at the intended bounded integration point while preserving successful v2 mechanics, hash grammar, output bytes and INACTIVE rules. Existing semantic issue ordering is preserved: do not prevalidate a different set of ordinary game policies or move equality checks behind newly speculative reads. Reader source-change failures are legitimate new source-protection failures, not a new gameplay rule.

A successful mechanical proof is still v2-only. New S/B cannot be attached to it or its common candidate. Future full HOT semantic admission must run while the source gate is open and be an explicit required input to a separate active preparation/common domain **before those seals**.

### State machine and NoPublicationAttempted

```text
Created → Capturing → CustodyClosed
       → PreparationStarted → Reserved → Building → V2CommonSealed → Delegated
       → (actual transaction's committed / rearmed / uncertain disposition)

Before Delegated, a failed attempt with a still-owned reservation:
       → ClosingUnattempted → ClosedNoNativeAttempt

Before reservation or with lost/debt ownership:
       → Unavailable (no NoPublicationAttempted capability)

Any source/owner fault closes Capturing permanently.
ClosedNoNativeAttempt, Delegated and terminal states cannot reopen or rebuild.
```

These are private states, not serialized flags. `BeginPreparation` consumes CapturingGate even when custody is unavailable. NoNativePublicationAttempted describes this **current joint common publication call only**, not earlier QTE, baseline restoration, Ready deletion, original issue or resource reservation effects.

Add a named AR entry:
```csharp
internal static JointUnattemptedCloseResult CloseOwnedJointAttemptUnattempted(
    FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
    GameEngine.MortalJointPublicationAttempt attempt);
```
The nested attempt is internal solely so AR can name its type; its constructor/factory remain private to GameEngine. It exposes only internal owner-verifying access needed by AR, no setters/import/caller transition API. AR performs actual registry checks under its gate: exact current files/lease/G; exact continuation/reservation; live accepted-state agreement; no restart blocker; both _openTreatmentPublicationReceipt and _openJointRepairPublicationReceipt absent; no published fingerprint; attempt not Delegated. Before entering the registry lock, actual lease.EnsureNoPendingLocalDecision and existing ordinary MainAdmission/lease/G guards must pass; faults return no receipt. Within the registry lock recheck exact accepted state, attempt and reservation references before clearing. Only then call the existing ClearMortalWoundTreatmentPublicationReservationCore for **that same** publication slot and issue a private `JointNoNativeAttemptReceipt` bound to attempt/continuation/reservation/source operation identity. It must not use the old void/no-op abort return as proof. If any predicate fails, return a typed unavailable/debt/foreign result with no receipt. Exact close-result alternatives are ClosedNoNativeAttempt, UnavailableNotReserved, UnavailableForeignOwner, UnavailableDelegated, UnavailablePendingDecision, UnavailableSourceFault, and UnavailableOwnerLost; keep diagnostic error details separate. Session replacement/lease-release uncertainty still propagates through the existing owning exception path. Do not clear another owner's cache/reservation/held resource claim.

CP replaces its local publicationEntered boolean with the private attempt transition. Immediately before the existing NR96 call, irreversibly mark Delegated; if the call throws or returns no receipt, generic catch still cannot obtain NoNativeAttempt. Preflight failures before delegation can close only if actual registry ownership remains intact. A candidate may already have prepared/cache/common seals when this close succeeds; the receipt therefore deliberately contains **no source-capture capability**.

At Refused/catch before delegation, use the named close. Preserve current outward v2 PublicationUncertain/held behavior and issues for compatibility; attach the private close disposition only to retained engine metadata. Physical recovery and the transaction's actual settlement remain authoritative after delegation. SessionReplaced propagates, with no attempt-owned deletion of the new session. If a source validator already invalidated the registry, classify NoProofUnavailable; do not infer that void invalidation means no attempt.

The owner is retained for production reentry/failure ownership, not a test field: overlapping entry refuses, repeated read returns the same closed diagnostic, attempted/uncertain work stays with its existing transaction, and a disposed attempt cannot take/abort/rebuild. On lease close, all hot reader/custody capabilities expire. A detached immutable diagnostic export is the only supported after-lease observation. The export contains state/ordinals/binding/hashes/source bytes/statuses/close result as DATA, no constructors or token strings that can reissue authority.

## What must not be called in this unit

No encompassing health/repair/progression loop is called as a planning helper. Existing loops continue their original work; the new custody code only observes the named real boundaries. No sequential normalizer/distributor, shared scheduler, source reducer, identity/reward/XP/time operation is rerun for a proof. Existing continuation/prepared/effect/final/common computation stays exactly once at its current owner. No added native control/M3 path, original rewrite, P/A producer, marker stripping, user-offer/QTE activation or T197 handoff.

No new semantic exception for changed stats, soul, characteristics, stat-points or output. No XP formula, level alias equality or timestamp policy. No fake empty GameResponse, arbitrary map, parsed issuer, reflection-minted object or upstreamAccepted flag. Physical reader APIs return data; only the real owner construction/flow links custody.

## Why full submitted admission remains unavailable after this unit

The full-output frontier has seven roots and 73 direct IO-bearing methods, preserved in the prior source consultation and copied as a verified index into this JSON. They are **output closure only**, not the whole source. This unit records exact current output bytes but invokes none as a new semantic admission proof. The distinction is enforced by type: there is no implicit conversion from JointHotSourceCustody to MortalJointSubmittedAdmission/S.

| Required next family | Exact shared production work still required before full S |
|---|---|
| Raw critical/domain input | Critical raw guardians/soul/achievements/codex, NPC/faction/location/item gates VR1695–1705; physical size/existence policy; genuine held resource/item evidence. Do not invoke the encompassing lease-acquiring/state-mutating pipeline under the attempt lease. |
| Six automatic roots | Share PlayerAndInventory player/status/experience/characteristic bodies, same-turn reader and required-field reread; preserve warnings, siblings and no-init/client-owned stat-point baseline. Current calculator is data arithmetic, not input authority. |
| Level and time | Share A760–807 level materialization with real resolver priorities; world time command shape/mutual exclusion Q1796/1831/1907 and N8145. Preserve calendar-only legality, actual D9 discriminator and separate P0 effects/P1 progression. |
| Narrative/interface | Share read-only narrative, achievement/prose delta dependencies and fixed-point interface projection. Existing interface wrapper writes; cannot be used as read-only. Missing timestamp fallback is Unavailable/normalization-needed unless the existing real writer already produced valid bytes. |
| Full reasoning | Thread actual current/original/control family through all six Full scopes and guardian/NPC/resident/faction/alias/journal/projection dependencies. Mortal only skips the leaf guards that actually test original realm, not structured actor extraction. |
| Special action/combat/pending memory/no-offer | Preserve exact tagged-action, Ink context, original/request/gacha/backup, file-presence and actual runtime/prior-source predicates. No-offer alone does not make the other roots unreachable. |
| Completed musings/caches | Actual retained enabled completion, same lease and dependency checks; isolated invocation/recursion/cache scope, no nested lease and no fake empty completion. |
| Canonical repair freshness/full resubmission | Actual start/detected/fallback/merged-error history; all target/output mtimes and strict greater-than comparison; complete rollback inventory/beforeimage comparison. Existing Ready/custody success is not that proof. |
| Volatile comparisons | Actual first-pass detached projection samples/results, or proven shared no-emission semantic kernels. Guardian/resident/project DTO defaults can sample UTC/GUID; never rerun to reconstruct evidence or discard observable fields. HOT can retain genuine first-pass observations; cold needs independent semantic proof under the trusted-local model. |

For each of the 73 existing methods the attached index retains actual path/range/calls/operation text and an explicit disposition `NOT_IMPLEMENTED_IN_THIS_UNIT__FULL_S_UNAVAILABLE`, except that the underlying original-reader seam is already implemented and now consumed. The two ordinary output writers are never part of admission; they remain ordinary producers. No direct leaf is silently “covered” merely because this unit captures narrative/interface/debug bytes.

After this unit, implement fixed source-owned role operations in `ValidationService.MortalJointSubmittedSource.cs` and shared policy kernels in coherent families. Each operation is backed by actual lease/reader; preserve presence/bytes/error/callback semantics, conditional branch closure, per-invocation caches and sticky unsupported-read faults. Before claiming full S, all seven roots run through that session, all required raw/player/level/time/freshness branches have real evidence, default observable diagnostics/volatiles match or the hot path returns Unavailable. Only then add private semantic admission issuance while Capturing remains open. These are engineering steps, not another approval question.

## Causal TDD contract for this bounded unit

Use the actual fixture `MortalWoundTreatmentResourcePublicationLifecycleTests.JointCompletion.cs`. It already invokes real StagePlayerTurnAsync (line136) or ProcessPlayerTurn (line311), signs original43, authors the first rejected response after original, observes actual request publication (line435), resubmits complete corrected outputs (line483+) and writes Ready last. Reuse the real prior42 source and ownership; do not switch to LiveTurnPreparation helper or mutate active inputs before original.

New positive fixture:
1. Select actual ProcessPlayerTurn caller for the principal case. At its existing TerminalWaitStarted callback, assert actual original43 and retain original six inputs.
2. Author the first response with real changed status_changes statsIncreased, experience/player_status levels and normalized output bytes **after original**. Preserve original stat-points balance/watermark, characteristics and soul baseline. Include these public changes in the real resubmission obligations and corrected complete reply; do not author private pending/issuer/common data.
3. Drive real severity III rejection, native baseline restore, actual repair packet and corrected II reply through existing callback; write accepted Ready last. Do not call the new issuer from the test.
4. Actual default canonical owner captures hot custody and later refuses changed automatic work through unchanged v2 mechanical/INACTIVE policy. Assert zero common native publication/charge/progression/stat award/story/turn advancement for this attempt, held resource/request intact, genuine closed unattempted receipt where the actual registry predicates allow it.
5. Read only the retained diagnostic export after the lease closes. It must distinguish original blank/level1 from current stats/level2 and exact three output bytes, actual Ready observed-before-delete, actual same-G origin and source recheck. FullSemanticAdmission/S/B/M3 are explicitly absent. Calling a retained capability with a new/disposed/foreign lease fails.

RED1 first runs this genuine existing flow and asserts its existing held/refusal/source facts before looking for the missing private custody/attempt result. Missing owner result is the intended RED, not fixture setup failure. No production success logic precedes the causal RED gate.

RED2 demonstrates a real custody source drift using existing FS read/open hooks between capture and final recheck, or an actual accepted Ready replacement before consumption. Require the diagnostic custody be unavailable, no S and no unowned slot cleanup; tests must first establish the corresponding unchanged genuine owner. Do not temporarily issue successful evidence before the barrier.

RED3 exercises the actual reserved attempt before vs after delegation: before-delegation failure can yield the owner-checked no-attempt receipt; a take/delegation failure/uncertain case cannot. Existing native/FS faults are the stimulus; no production test callback or forged attempt construction.

| ID | Future causal case | Required bounded result |
|---|---|---|
| HS01 | Actual Stage→ProcessPlayerTurn, corrected post-original stats/level/output, Ready last | Genuine original/submission custody, unchanged v2 refusal, owned predelegation close if reservation survives; no S/M3 |
| HS02 | Unchanged real v2 late and live positives | Same mechanics/bytes/fingerprints/charge/effect/story/turn; actual observed evidence consumed, final attempt Delegated/settled |
| HS03 | Old/cold/helper original, same apparent tuple | No retroactive hot lineage; v2 own behavior unaffected |
| HS04 | Actual browser Stage then later actual browser owner, no retained scope | Hot data can match only under current real browser/MainAdmission/lease; cold preparing/refusal unchanged |
| HS05 | Missing/malformed/mismatched/repeated Ready | Existing diagnostic/retry order; no accepted custody from rejection or mere absence |
| HS06 | Genuine worker-direct and worker-generated Ready | Explicit hot unavailable, existing successful worker route unchanged |
| HS07 | Current fixed-role byte/presence drift, repeated-read changed-then-restored | Sticky unavailable; no successful custody/source gate; default v2 remains independently governed |
| HS08 | Manifest/authority/rollback/context/selected-original drift | Accepted observed barrier blocks; no copied Snapshot-only substitute; source semantics preserved |
| HS09 | Genuine foreign FS/lease/G/engine original and two genuine live reservations | No rebinding or cross-abort; own held request preserved, unrelated owner untouched |
| HS10 | Source gate consumed then preparation/cache/common built; no native call yet | NoNativeAttempt may be true but Capturing permanently unavailable; no S/B attachment or reseal |
| HS11 | Predelegation refusal vs delegated take error/native uncertainty | Only actual owned unattempted close issues proof; transaction debt cannot fall through generic abort/rearm |
| HS12 | Overlapping/repeated entry, stage/original/Ready deletion/lease-close failure, cancellation | One actual attempt/capture; no rerun/resample/re-reserve; capability expiry and explicit unavailable/debt |
| HS13 | Diagnostic copies mutated; old live references after disposal | Export detached; authority unaffected; all live rechecks require same original owner/lease |
| HS14 | Complete source roles with optional original absence and malformed/empty/BOM text | Physical distinctions retained; default decoding unchanged; unavailable is not empty acceptance |
| HS15 | Existing pre-original active-tail three refusals | Remain refusal fixtures, never upgraded into HS01 positive |
| HS16 | No extra writes/identities/scheduler/turn/stat awards from custody | Compare genuine identity/history/resource/automatic/story/output and original/request bytes; no P/A/M3 files emitted |

Keep fixtures per-test with real root/lease cleanup receipts and zero ownership leakage. Secondary reflection may inspect detached diagnostics, never construct/seed private issuer fields. No fake engine field to select ACTIVE and no empty response.

### Finite category plan

The HS rows are coverage obligations, **not 16 expensive whole-QTE tests**. Choose the cheapest genuine owner for each obligation and record the final selectors before review:
- `fr063-hot-joint-submission-custody`: cheaper actual ordinary Stage + actual WaitForContractRepair fixtures for original/Ready custody, decode/presence, malformed/mismatch and read/delete/close drift. They still use genuine original writer/G/leases and public submissions, never fake private origin fields. These prove custody only, not joint reservation/no-publication.
- `fr063-hot-joint-attempt-ownership`: approximately one or two full genuine joint cases selected from HS01/HS09–12/HS16, using actual prior-QTE→Stage43→repair→registry/native ownership. Split additional causal native/foreign-owner scenarios into separate finite categories when needed; each category must stay within its real ≤5-minute budget. Existing Late2 is about 216s for two cases (parent-reported), so no assumption that many full cases fit and no budget increase to hide cost.
- `fr063-hot-joint-origin-compatibility`: targeted actual console/browser/helper/worker scenarios required by the changed call sites. Existing source-specific fixtures may provide cheaper coverage; preserve per-test filesystem/owner isolation and real browser MainAdmission.
- Pure transition/immutable-copy component tests may cover phase tables and diagnostic mutation cheaply **only as non-authority state components**. They cannot establish an issuer, G/lease/registry ownership, Capturing or NoNativeAttempt. Full owner integrations must prove those independently.

The existing protected first Late2 method bodies remain byte-unchanged. An optional **test-helper-only read-only observer** may inspect detached engine diagnostics after the lease, but no production callback or mutable shared fixture is introduced. Exact counts follow source-reviewed fixtures and measured categories; no fixed count is claimed here.

Keep current catalog singletons/owners affected by this real diff: `fr063-joint-late-completion` (first unchanged Late2 required by workflow), `fr063-joint-live-completion`, `fr063-joint-active-tail`; select the exact current checkpoint-provenance and phase-refusal/settlement cases if registry abort/take or checkpoint consumers change. Existing `fr063-observed-original-reader/default/browser/conformance` 131-case acceptance is not automatically rerun: reader/storage source is unchanged; engine integration scenarios cover the new consumer. If implementation changes those four files or an actual failure implicates them, select affected reader categories anew.

Do not run broad turn-lifecycle-core/repair or all related categories. Catalog responsibility, exact selectors/counts/budgets and BOTH build requirements must be finalized from actual tests and independently reviewed. New actual categories cannot borrow a previous category's PASS. Follow fresh preparation/catalog/PlanOnly, SOURCE/FIXTURE/SELECTION, first unchanged Late2, frozen EVIDENCE and separately released causal RED/GREEN gates. No tests were run for this design.

## Sequential work after this contract

1. Preserve and independently review this exact bounded owner/source contract. Address concrete findings, then test-first implement this real vertical. No ordinary technical permission re-request.
2. Keep CAPTURING before any active preparation/cache seal. Implement shared automatic/player/level/time/raw policy source adapters and the seven-root output read session, beginning with genuine source fixtures and preserving all branch obligations above. Capture first-pass repair/volatile observations at actual owners; don't retrofit absent history.
3. Issue full private HOT semantic admission only after complete current/original/read/freshness/owner closure. Replace no-work equality only for exact admitted owners; keep sibling/initializer/concurrent guards. No changed XP/alias policy.
4. Extend the actual active preparation domain to consume that admission **before seals**; S→one retained C→raw B→same active candidate. Do not reuse a completed v2 candidate with appended S/B. If cold M3 still unfinished, retain the explicitly blocked step5 intermediate and its real abort/debt owner.
5. Complete cold owner semantics/codec and R01–R08 before M3/public activation as specified. HOT custody loss before P0 remains unavailable, never proof of no action or permission to replay Stage/health/reminder/dice.

This design adds no durable path or P/A emission. Steps1–4 target hot construction; each later semantic integration still has to prove its actual source/owner closure. Public console/browser/agent v2 remains default. No T197 engineering, new QTE offer, F2 choice, host-selection change or principled grant follows from this report.

## Completion limits and documentation

This proposed contract is concrete enough to enter independent source review of the **bounded custody/attempt/observed-reader consumer unit**. It is intentionally not a complete full-S engineering release. Shared Full reasoning comparisons, actual repair freshness/full resubmission, all raw/player/time validation and future active seal integration remain named, sequential work; missing APIs are not permission blockers.

Internal private lineage/diagnostics and unchanged v2 emitted bytes have a narrow no-GM-update rationale. If implementation changes reachable request/Ready/original/control schemas, public diagnostics contract, GM instructions, packet semantics or source eligibility, AGENTS requires synchronized Mortal/afterlife prompts/examples/manifests/guards in that same change. Do not use this rationale for later P/A/M3 or active gameplay.

All prior immutable reports are untouched. Full T179/T192/Pd/F1, H-B02, R01P01–03/R02–08, T197, DEP-C/I/L, target qualification, D4/guided/visual/Windows/D8 and public activation remain open. Supported trusted-local integrity is sufficient; deliberate offline/concurrent external rewriting is outside the threat model, and no cryptographic custodian grant is invented.



## Evidence identity

Frozen production/test target: `3fdc16e8a954458f1e56bafa42ca24a66c06d020`. Local completion HEAD: `d99e45102e2fb64088d771eb473de6671617f9b6`; scoped production/tests/catalog diff is empty. Parent documentation checkpoints advanced independently. SHA-256/blob pins and exact line projections are in the paired JSON; the 73 historical output IO rows were checked against the frozen source and all listed IO lines still match. This is static source verification, not runtime evidence.

FINALSTOP.
