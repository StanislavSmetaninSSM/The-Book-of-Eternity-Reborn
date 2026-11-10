# T179-Pd early R01 preparation and cold disposition candidate — 2026-10-16

DESIGN ONLY. Configured Astra/High; backend unknown. Source baseline f3bef7097660b12317842903f0849af43b29c06c. Repository read-only; source/Git/hash inspection only. No runtime/build/discovery/tests/network/delegation. Root is sole repository writer. Previous reports are immutable. All future checks UNRUN.

## Decision and source eligibility

Recommend an explicit preparation-only variant at the EXISTING game_state/control/validation_repair_request.json, produced by actual GameEngine ordinary preparation, with actual per-step publication ownership. It is a preparation recovery record, never an accepted-original or admission credential. The first successful original issue atomically replaces it with seeded original-bound history. No new pathname is necessary.

This source-version selection is a proposed concrete contract requiring design acceptance; it is not inferred implementation permission. The selected candidate issuer is actual GameEngine StagePlayerTurnAsync with OrdinaryPlayerTurnSourceLabel ("обработки хода", Core/GameEngine.cs:97), actual console or browser owner, and actual bound Mortal source realm for the active F1 contour. Source/realm eligibility comes from the owner and real source closure, not a caller source string or active flag. Subsequent S/target qualification remains mandatory; having a preparation record does not establish a qualifying wound, accepted output, target capability or common candidate.

LiveTurnPreparationService stays its existing live-helper producer, unseeded/outside ACTIVE history issuance. ReadObservedCurrent accepting its genuine original bytes for the bounded reader is not a GameEngine active-source grant. Do not rename the helper source or upgrade its old original. Helper schema round trips/readers must still preserve new fields if they consume them; its PrepareAsync must refuse a live foreign/new preparation/history lifecycle rather than erase it. This is source eligibility, not a narrowing of lawful outcomes of admitted GameEngine actions.

The full early R01 protocol is NOT closed by current source. A finite record grammar and transactional step contract can be specified, but complete production issuer/abort semantics still require three concrete owner changes identified below. In particular a lost never-persisted random sample is not recoverable. This report does not choose resampling as recovery, claim a universal safe abort, or hide missing information behind a permanent HOLD.

## Actual ordered work and allocations

Paths below are relative to BookOfEternityClient.

### GameEngine ordinary console/browser

Core/GameEngine/GameEngine.TurnLifecycle.cs:1484–1519 enters actual SessionOperationContext. Console keeps its continuous owner; browser staging captures real main close facts afterward. StagePlayerTurnAsync :1561–1738 runs:

1. Capture G; enter actual BrowserOriginalOperationScope if applicable; ClaimBrowserPreparationAsync before health. Browser claim publishes queued -> preparing with historyBeforeJson (BrowserPlayerInput.cs:177–195). Console player text at this point is a method argument, not a persisted turn request.
2. ValidateCurrentGameStateOrShowErrorsAsync("перед отправкой хода") :1568. It defaults allowRepairLoop=false, progressionControl=null. Its EnsureClientOwnedSystemFilesHealthyCoreAsync (ValidationAndRepair.cs:147–176) can clean orphan input, refresh/repair profile mirrors, write mod/settings, ensure afterlife notifications/return guard/attraction, ensure QTE runtime and initialize progression. The broad validator then runs. Success deletes repair controls (:295–308). Therefore placing the new record before this unchanged call would delete it; placing it after the call leaves the browser pre-health preparing cut outside it.
3. Consume the explorer's HOT PendingLocalTurnRollbackSnapshot :1571. This nulls the field (ExplorerMode.PrivateImplementation.cs:671–675); it does not atomically transfer durable authority.
4. Sample backupId = UtcNow.Ticks, CreatePreTurnBackup, overlay actual explorer baseline :1580–1582. Backup files are individually published. Overlay may delete redundant backup paths and substitute earlier local-turn baselines (TurnLifecycle.cs:2006–2047).
5. Construct actual TurnRequest :1585–1593. Its property defaults allocate a technical RequestId GUID and timestamp before initializer assignments (Models/TurnRequest.cs:12/21). Timestamp is overwritten with the actual request sample; browser RequestId is overwritten with existing ActionId :1594. Unused overwritten defaults are not domain/wound/effect IDs or accepted dice. Do not claim they are retained; do not use them as meaningful receipt identity.
6. BuildTurnSystemReminderAsync during the initializer :1592. It may publish mod manifest/settings, and refresh scenario manifest from pending setup (:4936–4944). ScenarioCoreService.BuildManifest samples LastExtractedAt (:424); the reminder then invokes its actual read/owner helpers. It is not a pure string formatter.
7. BuildControlForNextTurnAsync :1595. ProgressionScheduleService :132–195 may initialize/sanitize schedule; :207–300 computes next-turn control and writes progression_schedule.json, including actual LastUpdatedUtc. This is preparatory schedule state, not execution of the later accepted-turn scheduler; it must nevertheless not be repeated to recover a completed preparation step.
8. AttachPendingDiceAndGachaAsync :1596/:4534–4538 calls actual PendingTurnStateService.GetOrCreateAsync. Existing valid state is reused unchanged. New state is generated and written before returned. PendingTurnStateService :124–136 draws visible dice and hidden gacha dice; WriteAsync :169–183 samples its own LastUpdatedUtc. GetOrCreate's published state is the actual retained dice/gacha source.
9. Build actual afterlife preview, register staging validation files and load skill catalog :1597–1605. The preview returns null without reading afterlife mechanics when the actual realm is not afterlife (AfterlifeSpiritualConflictTurnPreviewService.cs:18–41). RegisterOrdinaryTurnStagingValidationSnapshotFiles :1990–2003 includes scenario manifest and pending dice where present. These are validation snapshots, not a declaration that all must be rolled back to their just-current bytes.
10. CreateCanonicalBaselineSnapshotAsync :1606. SessionAndSnapshots.cs:417–455 cleans terminal/old-original artifacts and may initialize afterlife conflict/entity/chronicle/global-flag defaults. It captures exact inventory, snapshot copies, path presence and client hashes, then currently writes manifest and detached authority separately (:514–567).
11. Optional computed characteristics :1608–1632; known computation exceptions log and omit context under current policy. Preserve that exact result/omission, not a new cold computation result.
12. ClearTransientOutputFiles :1634. Exact deletion set at SessionAndSnapshots.cs:1832–1847: narrative, interface, debug, wound decision, ink-feather result, QTE offer, progression report. It is not equivalent to "delete everything under output".
13. Publish actual provisional turn request; original-backed projection; final request publication/readback. Console :1643–1652; browser PublishBrowserStagingAsync :198–223. Final identity checks :1655–1666. Only then does ProcessPlayerTurn wait for the real terminal result.
14. Known console staging failure :1683–1736 deletes request, restores actual backup plus consumed explorer local-turn rollback, cleans backup/original, then throws. Browser failure :1674–1681 retains preparing/staged evidence and throws. These are different policies; neither catch block is a cold atomic abort protocol.

### Dice/fate facts

PendingTurnStateService.cs:29–49 reuses a valid existing pool; :65–111 Reveal/Rewrite persists locking and actual replacement state. :115–119 RotateAfterAcceptedTurn creates the next pool, and actual accepted-turn code calls it at TurnLifecycle.cs:1870 (other accepted routes have their own calls). Preparation recovery must never call Rewrite, Rotate or AttachFreshDiceAndGacha.

Existing committed pending dice with exact bytes are recoverable after native settlement. Revealed/rewritten fate must remain bound to its actual preceding local owner and any actual rollback policy; a new preparation may neither silently replace it nor infer a fresh pool from invalid/absent data after a previously recorded commitment.

Cold need not, and cannot, infer whether an unrecorded draw actually ran; it can establish only that the required exact result is unavailable. If CreateFreshState generated dice only in RAM and its write never established a durable intent/value, those numbers are lost. The next GetOrCreate would draw again. That is NOT recovery of those numbers. A new design can ensure no dependent action uses a draw until its native publication is committed; it cannot recover entropy lost before the first persistent representation without changing the random-generation contract.

### Live helper compatibility, not active issuer

LiveTurnPreparationService.cs:89–112 cleans prepared artifacts, clears pending dice, constructs request, and directly generates dice if options omitted; :113 always computes gacha from a fresh hidden draw. Request/time/gacha remain RAM until its later original/request writes :198–208. These are genuine helper behavior and test fixtures, but not the ordinary pending-fate contract. Excluding this source from the new ACTIVE/history issuer avoids silently changing it into ordinary gameplay. Existing helper emissions stay unchanged; conflicting new control/original must be refused before its cleanup. Shared codecs and original consumers still require version dispatch.

### Information that current files do not prove

- Console draft/extra reminder/wait semantics before any durable turn request; no source here guarantees recovery of a console string after process loss.
- Backup ID/request timestamp/request ID or computed reminder/context generated only in memory before publication.
- Exact earlier explorer TrackedFiles/BaselineFiles/absences/ValidationSnapshotFiles and live rollback ownership from a marker alone. The marker has only schema/G/snapshot ID.
- A newly generated dice/gacha value before its durable write.
- The fact that a broad health/reminder subcall completed merely because some of its target files exist.
These are bounded missing facts, not a justification to manufacture authority.

## Exact preparation record candidate

Proposed NEW strict discriminator, not accepted emitted bytes:
{schemaVersion:1,kind:"ordinary_original_preparation",policyVersion:1,
 binding,intent,phase,cursor,entry,completed,pending,abort,integrity}

All root fields required; unknown/duplicate properties rejected. JSON strings preserve exact producer text; binary images use canonical base64 plus length/hash for consistency. Hashes are integrity/correlation, not authority.

binding is closed:
{generation,sessionId,nextTurnNumber,sourceLabel,sourceRealm,producer,browser}
producer = console | browser. sourceLabel must equal the actual ordinary label. sourceRealm must be the actual bound supported Mortal source for this profile. browser is null for console, otherwise exact {actionId,source,submittedAtUtc,queuedJson,expectedPendingJson,mainCondition}. Browser source remains browser-composer/browser-effect-action, never overwritten. nextTurnNumber is the actual already chosen ordinary coordinate, not a counter increment by this record.

intent is closed:
{playerAction,extraSystemReminder,playerFacingTurn,waitingTitle,waitingText}
Nullable optional values are explicit null. Capture actual API inputs, including flags needed to resume the same pipeline; do not reread defaults from a later UI. This data preserves intent only; no acceptance claim.

entry is closed:
{turnRequestBefore,originalManifestBefore,originalAuthorityBefore,controlBefore,
 pendingDiceBefore,terminalSignalsBefore,localRollback,sourceBinding}
Every Image = {present,bytesBase64,sha256,length}; absent requires bytes/hash null and length 0. Present uses canonical padded RFC4648 base64, nonnegative byte length and uppercase 64-hex SHA256 of those exact bytes; an empty present file differs from absence. terminalSignalsBefore has exactly complete/error fields.
localRollback is null or the actual transfer from the genuine explorer owner:
{evidenceRoot,markerImage,trackedFiles,baselineFiles,validationFiles,technicalArtifacts,
 backups:[{logicalPath,artifactPath,hashMode,image}],ownedCurrentImages}.
Exact path and complete set validation by existing owner rules is mandatory. A parser cannot supply this transfer. Cold uses a distinct verified preparation-rollback disposition only after actual producer/protocol and physical relation checks; it does not register a caller DTO in _consoleEvidenceOwners.
sourceBinding stores closed actual source roles required by the selected recipe, not a caller dictionary or arbitrary exemption list. Its complete role payloads are an owner implementation prerequisite listed below.

phase = intent | step_prepared | step_committed | issue_prepared | abort_prepared | aborted.
cursor = the next exact recipe ordinal. completed is ordered, unique by ordinal, containing only completed receipts. pending is null except step_prepared/issue_prepared, and abort null except abort_prepared/aborted. No issued phase in this preparation kind: successful issue replaces it atomically with the authenticated-original history kind.

Each completed/pending receipt:
{ordinal,kind,inputRefs,result,changes,rollbackRule}
kind is exactly one of:
entry_health, local_rollback_transfer, backup_capture, reminder_preparation,
progression_control, pending_dice_binding, preview_catalog,
snapshot_preparation, optional_characteristics, transient_output_cleanup, issue_original.

result is a tagged object specific to kind; no untyped property bag:
- entry_health: actual completed entry-owner evidence, not an accepted-output verdict;
- local_rollback_transfer: exact transfer above;
- backup_capture: actual backupId and complete logical/backup/hash/absence inventory;
- reminder_preparation: exact reminder string and actual changed settings/scenario/mod owner outputs;
- progression_control: exact returned ProgressionControl JSON and committed schedule image/actual sample;
- pending_dice_binding: existing|newly_published, exact state image, actual source owner relation;
- preview_catalog: exact preview/null, skill catalog and validation-file selection;
- snapshot_preparation: complete files/presence/client-hash/rollback source capture, pre-issue seed inputs and exact prepared manifest/authority bytes once ready;
- optional_characteristics: present + exact context, or omitted + actual current policy exception class/disposition; no fabricated success;
- transient_output_cleanup: exact fixed-path before/after images;
- issue_original: exact final seeded manifest/authority/control images plus expected tuple/token and genuine artifact closure.

changes is an ordered complete list of owner-derived {role,path,before,after}. Role selects a specific owning policy and validates the path, including dynamic original artifact paths derived by that owner. These are serialized data only. They cannot be imported into a generic write method; the corresponding owner must independently verify the stage grammar, G, source, before/after semantics and actual native debt before preparing a bounded mutation.

rollbackRule = retain_committed_owner | restore_original_local_baseline | remove_preparation_artifact | preserve_initialized_baseline, fixed by receipt kind AND actual subowner. It is not chosen by a caller or trusted from serialization. Exact per-member disposition must be derived from source owner policy. This enumeration prevents a blanket reverse-all algorithm; it does not claim every entry_health/reminder leaf policy is already proved.

abort is closed:
{reason,fromCursor,settlementMembers,retainedIntent,retainedValueRefs,disposition}
reason = known_preparation_failure | explicit_preissue_cancel | unrecorded_required_value.
disposition = settlement_pending | aborted_unissued.
No source original, M3, effect, wound, lifecycle closure or actionBoundary authority is emitted. retainedValueRefs point only to durable completed results. A value lost before persistence is explicitly unavailable, never a substitute value.

integrity carries only encoder/hash integrity. No validated flag, no capability ID, no asserted NoPublication boolean.

This is a finite OUTER grammar. Result/sourceBinding inner owner schemas for entry_health/reminder/local rollback are NOT closed by present source. They cannot be represented by a generic map to declare this proposal complete. That specific incompleteness is an engineering blocker, not silently delegated to future runtime validation.

## Transitions and actual owner APIs

Private issuer:
GameEngine.BeginOrdinaryOriginalPreparationAsync(actualSessionOperation, actualInput, actualBrowserBinding?) -> OwnedPreparation.
It uses the acquired current owner/G, rejects existing original/terminal/common/native/worker conflicts through their actual owners, captures the input and entry ownership, and writes intent before any newly owned preparation mutation. It must not treat absent manifest/M as proof of no prior work.

For browser, this initial control write accompanies the actual queued -> preparing publication in PendingPlayerActionService.PublishAsync. The actual BrowserOriginalOperationScope validates its original binding. For console, the actual ProcessPlayerTurn owner commits retained intent before consuming explorer state. No extra GUID is needed for a preparation capability; the live object is private, while cold derives its own narrow disposition from actual source/protocol. Technical correlation uses G + exact input/turn/binding and control revision, never an invented gameplay identity.

Proposed owner operations:
PrepareNextStep(lease, OwnedPreparation, actualTypedStepResult) -> OwnedPreparationStep;
PublishPreparationStepAsync(lease, OwnedPreparationStep) -> CompletedPreparationStep;
PrepareOriginalIssue(lease, actual completed capture) -> OwnedOriginalEstablishment;
PublishOriginalIssueAsync(lease, OwnedOriginalEstablishment) -> IssuedOriginal;
ReadPreparationDispositionAsync(actual files, actual lease/purpose) -> distinct cold disposition;
PreparePreissueAbort(actual verified preparation, actual owner failure/cancel) -> OwnedPreparationAbort;
PublishPreissueAbortAsync(lease, OwnedPreparationAbort).

Allowed transitions:
intent/step_committed -> step_prepared for exactly cursor;
step_prepared -> step_committed with cursor+1 only together with actual step effects;
intent/step_committed -> abort_prepared -> aborted;
step_prepared -> abort_prepared only after its actual native decision is settled;
completed required prefix -> issue_prepared -> NEW authenticated-original history kind in same original issue transaction.
An uncertain native outcome prevents any competing transition or cleanup until its owner settles it.
No arbitrary backstep, skip, appended write after seal, or stale result reuse across G/source changes.

Per-step native protocol:
A. Derive a complete typed step using actual subowner policy; persist step_prepared with exact result and before/after images BEFORE applying dependent canonical changes.
B. Commit step changes plus step_committed control together in one native declared cohort, under exact G and full source guards. A no-op source calculation still commits its exact result/cursor as a real owner control transition.
C. Cold pending native decision restores all before-images; committed decision confirms all after-images and the completed cursor. If A committed but B never began, use the stored exact result after source recheck; never call the generating owner again.
D. No pre-issue step may dispatch GM work or publish accepted mechanics. Final request consumers must honor this protocol before acting.

This is a request to extract actual typed owner preparation, not permission to wrap arbitrary side-effecting calls. Current progression/mod/scenario/health calls write internally and often acquire their own leases. They cannot simply run under a new outer lease. Each selected writer must expose explicit-lease preparation/publication shared with its existing path, or use its own actual native outcome relation with the companion included from the beginning. No global filesystem overlay, mutation interceptor, public map import or bool acceptance.

## Issuance and final request projection

Original issue atomically installs NEW seeded original manifest, detached authority and history control; all prepared snapshot artifacts have their actual preceding owner proof. History origin retains the exact completed preparation prefix needed to prove issue and any pending final-request work. Original is immutable after this point. Original token derives from its final seed-inclusive payload hash; never reuse a pre-seed token.

Final request projection is POST-ISSUE; it cannot be an excuse to abort an already issued original through an unissued record. Actual provisional request, original-backed MortalQteWoundOpportunityProjection and final request are preserved at the same source coordinate. Preparation history needs an explicit nondispatch issued-but-not-staged phase and exact retained provisional/final request results; these belong to original-bound history, not the preparation-only parser.

Daemon Process-TurnCore (game_master_daemon.ps1:5840 onward) watches an actual request and checks matching original; it does not presently check the proposed preparation/history phase. Its preexisting 300 ms delay is not a seal. Therefore new version consumers MUST return a typed not-yet-dispatchable disposition for issued-but-not-staged, without marking that request terminal/stale in observed request keys. Final request + history staged transition (browser also pending staged proof) must publish atomically before ordinary dispatch. This is a same-change consumer prerequisite, not a silent timing assumption.

Browser final stage remains PendingPlayerActionService + genuine BrowserOriginalOperationScope; it is not replaced by a console publisher. A cold preparation settlement purpose must validate the actual slot/G/main condition and exact native decision before permitting its precise rollback or committed cleanup. Current preflight's preparing refusal is not removed globally. Cold cannot import or re-create the original live scope.

## ABORT: what can and cannot be justified

A successful UNISSUED abort is a terminal harness-preparation disposition, not a rejected accepted turn, not a player-action completion, and not an additional n or lifecycle event. It may preserve the player's draft while recording that no original/request was dispatched. It must not call StagePlayerTurnAsync again, draw dice, execute progression, rotate fate, allocate effect/wound IDs or emit a GM terminal signal.

Conditions for a proved abort:
1. Actual native/worker/main owners establish the last decision and no dispatch/accepted publication, rather than using absence of M/original alone.
2. Every preparation-owned mutation has complete actual before/after/source evidence and its real abort disposition.
3. Every preexisting local command or independent maintenance owner is respected; already committed consequences are not undone merely because preparation stopped.
4. Actual source and G remain bound; unknown later bytes are conflicts, not overwritten by rollback.
5. For browser, the actual slot transition and cleanup are owned by its publisher; a new explicit preparation-aborted disposition would require a versioned pending-action contract. Current settled receipt requires real original/terminal proof and cannot be reused as fake unissued settlement.
6. Retained intent/durable values remain accessible for an explicit product disposition. This report does not automatically submit the draft or reclassify it as a new accepted action.

The current console known-failure catch supports the general notion of failing unissued staging and restoring its genuine local rollback owner. It does not establish a complete cold abort proof. Specifically:
- A prior explorer local action has its own legitimate baseline/rollback semantics. Stage's overlay intentionally may reach BEFORE preparation entry. Its existing owner must transfer exact entitlement while hot, with a cold semantic checker; a generic entry snapshot is not an authorized substitute.
- New afterlife defaults are intentionally added to RollbackSnapshot baseline (SessionAndSnapshots.cs:589–682). Removing them in a generic undo would change current policy.
- Config/mod changes are not all in the game's ordinary rollback-tracked set. Some preparatory owner changes survive the current catch. A reverse-every-write compensation would alter behavior.
- Existing fate may be part of an earlier local owner. Preserve its exact actual rollback/retention rules, not a global "always undo dice" or "always keep dice" rule.
- A draw that never persisted cannot be recovered. If no actual effect/dispatch used it, a terminal unissued abort can explicitly mark it unavailable. Whether a later human resubmission is a NEW preparation is a product/owner policy question; it must not be sold as recovery of the old values or automatic same-action redraw.

Thus a terminal abort is a possible lawful recovery endpoint only after these owner relations are implemented. Present source does not justify every listed abort branch. Retaining a conflict meanwhile is a safety behavior, not completion of lawful recovery.

## Three exact remaining early-R01 obstacles

P01 — entry/health boundary and record lifetime.
Actual browser claim precedes health; health success deletes the proposed control path. Its transitive health/validator/reminder owners are not yet factored into the finite step receipts, and some commit independently before backup. A complete profile must either integrate the named typed subowners and purpose-aware cleanup, or define an owner-proved terminal pre-issue abort preserving their actual committed effects. Do not move all health effects into rollback by assumption. The narrower Mortal profile proves afterlife preview/init branches unreachable only from actual bound Mortal source and unchanged realm at those owners. It does not widen their existing afterlife seed/rollback policy; source replacement must refuse, not silently switch profile; it does not prove every other helper pure.

P02 — local explorer ownership across the first record.
FileSystemManager.ConsoleRollbackEvidence.cs:36–74 accepts a current hot registered owner or authentic original-covered marker/backups. Without either, it calls RequireCurrentPendingRollbackAuthority and refuses. Its marker is deliberately not a restore journal. A new preparation-only record cannot mint this current signed-original authority. The actual explorer producer must durably transfer its full typed local rollback ownership into the preparation protocol before Consume clears RAM, with a narrowly different cold restore admission. Crash before that transfer remains a preexisting local-owner recovery obligation, not solved by later preparation serialization. Full cold eligibility for this source is open until that owner contract is closed.

P03 — first persistence and volatile work.
Before console intent commit, the console draft and volatile defaults may be absent from every durable file. Before a random-result write, exact entropy may be absent too. A protocol cannot prove recovery of nonexistent bytes. Define initial durable acceptance of preparation intent before newly owned effects; preserve browser's already durable queued action. Each result becomes usable only after recorded commitment. An unfinished unrecorded computation may terminate as an unissued abort after genuine settlement; continuing with a fresh sample is NOT allowed by this candidate. Full product recovery must provide the exact terminal intent disposition instead of permanent HOLD. This is not solved by another filename or deterministic replacement GUID.

The task's "complete finite schema" therefore stops at a real ownership/information boundary: the outer states and per-step atomic relation above are implementable design, but entry-health result roles, local transfer proof and unissued terminal UI/owner disposition remain unclosed. No helper-only engineering stage is recommended as satisfying early R01.

## Future cut/verification matrix — all UNRUN

| Cut/scenario | Required actual result |
|---|---|
| Console before initial intent commit | No claimed recovered draft/value; no preparation acknowledgement or new effects; actual initial input contract needed |
| Browser queued before claim | Exact existing queued ActionId/source/text retained; no new action |
| Initial claim/control native pending or committed | Exact paired rollback or actual preparing+intent; no generic preflight bypass |
| Health/cleanup encounters preparation kind | Purpose-aware consumer must retain it; current delete path cannot run unchanged |
| Existing revealed/rewritten fate | Exact prior state and genuine local-owner relation reused; no CreateFresh/Rewrite/Rotate |
| New dice generated but never persisted | No reconstructed values; owned unissued abort only, not automatic redraw |
| Step-prepared committed, effect batch absent | Exact retained result reused after source/G checks, no recalculation |
| Step effect batch pending/committed, return lost | Same native decision settles effect + cursor together |
| Progression schedule written before crash | Exact schedule/control relation retained; no BuildControl rerun/tick |
| Explorer local owner hot transfer/cold restart | Exact full tracked/absent/baseline/technical cohort; marker or caller DTO insufficient |
| Afterlife initialized baseline / mod/settings effects | Real per-owner abort policy, not blanket pre-entry bytes |
| Original issue pending/committed | Native full-cohort rollback versus immutable seeded original; no PH-D01 downgrade |
| Provisional input visible before final projection | Typed nondispatch state; daemon must not dispatch or permanently mark stale |
| Final console/browser stage commit lost | Exact request/history/(browser slot) recovered; no new request ID/time/action |
| Old/helper original vs new active mode | Legacy source retained, no upgrade, no erasure of foreign preparation |
| Missing/foreign G/control/original/unknown member | Retain conflict; no authority or overwrite |
| Terminal unissued abort | Exact settlement + retained intent, no fake settled/GM signal/accepted turn |
| Source n/action/k; lawful death | Same admitted source coordinate; later real death-to-evaluation n+1 stays separate |

Source n/action/k, target qualification and all legal outcomes remain unchanged. The automatic life-evaluation request at TurnLifecycle.cs:2719–2738 uses its own source label, fresh dice/gacha and n+1/Chaos Sea progression; this candidate neither issues it nor folds it into ordinary n recovery. Likewise a completed original/action cannot be declared unissued because a preparation acknowledgement is absent.

R03 complete restore and R04–R08 remain separate OPEN work. Same-change Mortal/afterlife/daemon/helper/GM docs/examples/guards are mandatory for emitted control/original/pending-action changes; merely being client-owned does not exempt a dispatch-visible protocol. No implementation release, F2 selection, T197 implementation, new QTE or player activation. Full F1/T179/T192/P0 and D4/guided/visual/Windows/D8 remain open.

FINALSTOP.
