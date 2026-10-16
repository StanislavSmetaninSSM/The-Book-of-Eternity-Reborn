# T179-Pd R01/R03 original establishment and repair restoration — 2026-10-16

Focused DESIGN consultation, not an implementation or activation release. Configured Astra/High; backend unknown. Baseline 24f3d3c468aa7c4def7d9226ee7c90852296b3fd. Repository read-only; source/Git/hash inspection only. No runtime, build, discovery, tests, network or delegation. Parent is sole repository writer. Source pins and current comparisons are in JSON. All proposed checks are UNRUN. Earlier reports remain immutable.

## Ruling

The existing native publisher can atomically establish a declared original/control cohort on an admitted ordinary lease. Current original producers do NOT already issue that cohort. Nor does adding a third file make complete staging recoverable: actual console/helper request publication, snapshot preparation mutations, browser preparing/staged fences and restoration settlement are separate ownership boundaries.

Use the actual original producers to prepare a seed BEFORE original hashing, then their actual publication owners to install exact original manifest + detached authority + initial versioned existing validation_repair_request.json. Preserve final evidence in existing M3. No new pathname, post-issue rewrite, reconstructed live issuer, synthetic original or extra gameplay time/action/random allocation. Existing native transaction IDs remain the storage owner's concern; they are not replacement action, dice, scheduler or domain identities.

A complete R01/R03 release cannot presently be proved for every partial cut. In particular:
- early preparation has already written backup/snapshot/initialization artifacts before the current original pair exists;
- console original issuance precedes final projected request publication;
- browser cold preparing/terminalProcessing refuses before ordinary recovery, and uncommitted native decisions that change original evidence are deliberately fenced;
- generic restore is sequential, whereas joint restore is atomic but its postcommit compensation cohort exists only in memory.
These are precise required owner changes, not lawful permanent HOLD outcomes. This report closes the source/seam analysis and identifies the finite missing contracts; it does not claim they are implemented or fully specified.

All paths below are relative to BookOfEternityClient unless otherwise marked.

## Actual R01 producers and ordering

1. GameEngine ordinary console/browser:
Core/GameEngine/GameEngine.TurnLifecycle.cs:1561–1606 owns StagePlayerTurnAsync. It captures G, opens the actual browser scope when applicable, claims browser preparing BEFORE health checks, consumes explorer rollback, allocates the actual backup ID, captures backup, constructs the real request, builds progression control, attaches actual pending dice/gacha and preview, then creates the original. These existing outputs must be retained, not reconstructed by calling StagePlayerTurnAsync again after a crash.
Core/GameEngine/GameEngine.SessionAndSnapshots.cs:398–419 is the explicit-lease original overload. It validates exact inventory, removes prior original artifacts, and :420–455 may initialize four afterlife owner states and register corresponding rollback sources. It captures the full original source and presence/hash inventories at :457–512, constructs the manifest :514–540 and hashes/signs :541–556. Current :560–567 publishes manifest and authority as two separate writes. Its catch is in-process cleanup, not an all-cut recovery transaction.
CreatePreTurnBackup :1957–2020 writes each genuine .rollback.<actual backupId> member separately. SnapshotFileOrRollbackBackupIfPresentAsync :1018 onward writes snapshot artifacts before the original pair. Issuance completion therefore cannot claim ownership of earlier mutations merely by listing their hashes.

After the original pair, StagePlayerTurnAsync :1608–1632 computes optional characteristics, :1634 clears transient output, and :1643–1652 publishes a provisional request, performs genuine original-backed wound projection, then replaces it with the final request. :1655–1666 checks identity and current original context. A crash after original/control completion but before this stage is not a completed dispatched request.
MortalQteWoundOpportunityProjection.ReadCurrent/Project uses actual PendingTurnSnapshotReader authority and current request context; do not bypass it with a caller map or manufacture a provisional authority to eliminate this ordering. This consultation does not authorize any new QTE offer.

2. Actual live prepare-turn helper:
Services/LiveTurnPreparationService.cs:68–91 enters actual SessionOperationContext and one canonical lease, reads signed inventory, deletes previous preparation and clears pending dice. :96–136 constructs the actual request, including owner-generated request ID/dice/gacha/time where not supplied and actual preview/catalog. :138–193 captures snapshot and original authority. :198–208 writes manifest, authority, then request separately; :210–218 catches known failure. It is a real producer with its existing live-helper source label, not interchangeable with GameEngine's ordinary-player label. It must call the same narrowly factored original completion owner with its own exact source contract. No copied DTO can change that label or confer GameEngine admission.

3. Actual browser original:
Core/GameEngine/GameEngine.BrowserPlayerInput.cs:177–195 claims queued -> preparing with real historyBeforeJson. :198–223 guards that exact preparing state, installs provisional request, projects actual wound opportunities, captures exact manifest/authority/request/history, publishes staged + final request through PendingPlayerActionService.PublishAsync, then seals the actual BrowserOriginalOperationScope.
Services/PendingPlayerActionService.cs:97–105 already supports accompanying writes in the same coordinated publication and notifies the actual browser scope after publication. Its Staged and StagingProof at :17/:73–87 preserve exact immutable request/manifest/authority/history. This is the correct final browser publication seam; do not replace it with a generic console publisher.
Core/FileSystemManager.BrowserOriginalAdmission.cs:47–107 is the privately issued actual MainAdmission-bound scope; :150–188 checks actual phase publication and immutable tuple. Cold :448 refuses preparing/terminalProcessing; :449–461 validates staged evidence and actual main condition. Core/BrowserOriginalMainCondition.cs:36–46 checks real run state/identity, not only stored JSON.
Core/TrustedLocalFilePublication.cs:346–390 rejects uncommitted journal recovery that would change protected original browser members. Core/FileSystemManager.cs:3886 invokes the main/browser preflight BEFORE native recovery :3903; :3947 rechecks afterward. Therefore a final batch may be perfectly journaled yet remain inadmissible to the current cold browser recovery entry. Adding an original/control companion alone does not solve that fence.

## Exact identity, source and byte contract

A proposed OriginalEstablishmentPlan is created only inside the actual producer under its live operation/G/lease. Constructor private to a named owner, not a public byte/JSON import. It retains:
- actual root/G, owner route and source label; actual session/request/turn/player action and request timestamp;
- the ACTUAL existing dice, gacha, progression control, preview/catalog and any final request projection; no new random calls;
- original manifest fields plus the new supported seed before hashing; snapshot token is the final original ManifestPayloadHash, as used by PendingTurnSnapshotReader's authority construction;
- exact final original/authority bytes, all original source/absence/hash/rollback inventories and their actual hash modes;
- exact initial history/control before and after bytes and deterministic origin binding to final original bytes; seed itself must not reference the final hash, avoiding a cycle;
- actual preparation and pending/browser ownership prerequisites and exact final request/publication stage when available.

Original seed is version-dispatched, not retroactively added to an existing original. A newly supported seeded original requires matching history; PH-D01 remains binding: control parser choice cannot downgrade the original. Neither old original + new control nor new original + missing/old control establishes new authority.

Use the existing serializer/options and byte encoding at each producer. WriteFileAtomicAsync/coordinated JSON paths use the existing encoding with preamble; do not substitute SerializeToUtf8Bytes for those existing surfaces. Retain exact rollback bytes and supported legacy hash-mode semantics. Seed addition intentionally changes NEW original hash/token; no stale token can survive that addition. Existing issued originals stay byte-identical.

ReadExistingSessionGeneration under the actual lease must confirm the captured G before calling a publisher whose general API can create G. Do not bootstrap a new generation as a recovery convenience. Exact tuple/token/dice and full source closure must be rechecked immediately before prepare, before commit where supported, and after physical readback.

## Proposed narrow R01 APIs and their limits

Proposed names, not existing APIs:
- private GameEngine.PrepareOriginalEstablishmentAsync(lease, actualRequest, actualRollback, actualSourceRoute, actualBrowserScope) -> OwnedOriginalEstablishment;
- internal LiveTurnPreparationService.PrepareOriginalEstablishmentAsync(lease, actualPreparedRequest, actualSnapshotCapture) -> helper-owned equivalent using shared encoding/closed shape logic;
- FileSystemManager.PublishOriginalEstablishmentAsync(lease, ownerPreparedEstablishment) -> CommittedOriginalEstablishment;
- private PublishBrowserStagingAsync(binding, exactPreparedRequest, ownedOriginalEstablishment) extends the actual browser path and its Staged proof with the exact original-issued history binding;
- named original producer cold classifier -> OriginalEstablishmentDisposition, never a generic imported OriginalEstablishmentPlan.

The filesystem primitive accepts an owner-prepared fixed member set, not arbitrary file maps. Validate the actual lease/no pending decision/current G and each UsesTrustedLocalWriter member. Use PublishLocalFilesAsync only when all members are genuinely admitted to that route. CoordinatedStateWriteHelper :178–190 explicitly refuses mixed ordinary/original storage routes; its recorder branch is not itself a native all-cut transaction. Existing older browser recorder/recovery routes keep their actual owner and receipts. Do not force UsesTrustedLocalWriter or add a fallback.

Minimal complete initial ISSUE cohort: original manifest + detached authority + initial nondispatch history control. Exact original artifacts are guards or members as appropriate; all new snapshot/rollback artifact writes needed for this ISSUE must have an actual preparation owner. For live-helper final request, include its exact request in that same completion batch because it is already available.
For GameEngine, request finalization is later and source-dependent. The owner must distinguish OriginalIssued from RequestStaged; the initial control cannot claim the final request was dispatched. Either:
A. preserve two genuine stages, durably retain the actual request/preparation inputs and exact stage cursor, then use the actual final-request owner to commit final request + control transition (browser also staged slot) together; or
B. factor genuine producer calculations so the final request and complete actual source proof are available before issue, with an independently accepted source-owner projection API.
A is the narrower candidate because B currently meets a real original-reader dependency. Neither permits rerunning request/dice/progression preparation. This report does not select a fabricated unpublished snapshot authority to implement B.

A requires a concrete durable pre-issue preparation disposition. The current proposed history schema begins at original_issued, so it cannot already cover a crash during backup/initialization or an original/control batch rolled back to no seed. Extending the existing original PREPARATION owner may use existing browser preparing proof and/or an explicitly discriminated preparation-only variant at the existing request/control path, but that is a NEW exact R01/R02 contract, not admission history authenticated by a nonexistent original. It may grant only recovery of its own actual declared preparation images under G/main/native ownership; it cannot pass the admission reader or legacy-route fallback. No implementation choice for these bytes is implicitly granted here.

For browser, proposed recovery must be integrated into the actual MainAdmission/browser preflight, before any general native recovery:
- inspect actual pending action/preparing proof plus actual native decision through the existing trusted codec;
- verify exact G, browser action/source/submittedAtUtc, actual main condition, declared before/after members and preparation owner relation;
- issue only a bounded browser-original preparation settlement purpose for that decision;
- native pending -> exact rollback of that declared batch; native committed -> committed cleanup and semantic cohort verification;
- recheck actual browser tuple and then return the appropriate preparing or staged disposition.
This is a narrower extension of the actual browser owner, not permission to skip PreflightBrowserOriginalAdmission or to call general recovery under a new fake scope. Preparing with no qualifying owned decision still lacks enough current evidence to resume/retire. Its eventual full lawful recovery contract must be designed; permanent refusal is not the solution.

## Actual R03 restore semantics

Core/GameEngine/GameEngine.ValidationAndRepair.cs:3317–3328 restores before dispatch for the exact actionable repair families. Do not broaden this predicate. Actual pending checkpoint provenance at :982–1039 comes from a published accepted mechanics plan and physically read pending bytes; it is not a desired JSON supplied by the repair caller. The real plan/publication identity and source binding must survive if cold recovery needs that override.

Core/GameEngine/GameEngine.SessionAndSnapshots.cs:2091–2123:
- joint checkpoint forbids sharing a generic pending checkpoint or foreign G and calls the joint owner;
- ordinary path checks G, runs RestorePreTurnBackupAsync, then writes pendingResolutionRepairCheckpoint separately;
- runtime projection refresh occurs after release.
ReadValidatedRollbackBeforeImagesAsync :2322–2363 validates every backup and its actual exact/legacy hash mode before restore. RestorePreTurnBackupAsync :2366–2408 deletes currently tracked additions and restores backed-up exact bytes sequentially, collecting known failures and preserving evidence. It is not an atomic full-cohort restore.

Core/GameEngine/GameEngine.MortalJointRepairRestore.cs:12–101 is materially stronger:
- actual reconstructed rejected capture, same G, ordinary-only lease and complete original rollback inventory;
- exact baseline plus permitted homogeneous wound command and private pending wave overrides;
- one native batch at :52–60 with final source/before-image guards;
- full physical/inventory/original readback; known failure compensates;
- refresh after release; known refresh failure reacquires same-G lease and compensates using HOT installed cohort.
RequireOrdinaryJointRepairLease :118–123 rejects recorder, legacy recovery and BrowserLocalAccess. A browser caller cannot be routed through this method merely because the desired bytes look similar.

## Proposed R03 owner plan and settlement

Factor actual read/validate/desired-cohort construction from ordinary restore into:
PrepareRepairRestoreAsync(lease, originalReadAuthority, actualRollback, ownedRepairHistory, actualPendingPublicationProof?) -> OwnedRepairRestorePlan.
Joint uses its own corresponding builder with actual RejectedCapture and exact overrides; keep the prohibition on generic pending coexistence.

Plan fields are private actual G/original/token/turn and repair revision; full tracked inventory; validated backup source/hash mode; exact current before bytes; exact baseline after bytes; actual pending/command override source; history before/after and read guards. No path exemption list or public map import.

PublishRepairRestoreAsync(lease, ownedPlan) includes canonical restores/deletes, exact permitted pending override, and existing control history transition in ONE initial native member set. Pending is not appended after commit. Even a canonical no-op needs a genuine control transition if recording a new owner event; do not infer restoration from byte equality alone. Inventory proofs distinguish rollback-tracked domain members from the client-owned control companion: current joint RequireJointRestoreImagesAsync :145–148 would reject a naive extra history member because that path is deliberately excluded by EnumerateOriginalBrowserRollbackTrackedFiles :1911–1929. Add typed separate control verification, not a global inventory exemption.

Recommended settlement preserving current commit-then-refresh behavior:
1. Native restore transaction commits domain cohort + exact history phase 'restoreAppliedAwaitingProjection' with enough retained before/after/source data for the REAL restore owner to confirm or compensate.
2. Perform existing exact rollback runtime projection only after lease release, as current owner does.
3. On success, publish the exact owner continuation transition before repair dispatch. On known refresh failure, actual compensation restores domain AND control using a declared same-G plan; uncertain failures retain actual native debt.
4. Cold sees committed restore phase, checks original/pending/source/cohort, and selects actual projection/continuation or owned compensation. It must not rerun restoration merely because the success acknowledgement is missing.

Those phase/data additions are proposed R03/R02 details, not existing schema. Preserve the genuine pre-restore owner revision/observations in compensation. Once a completed restore receipt allows dispatch, freeze its full relation and prune undo bytes only under an accepted retention rule. A predecessor hash is insufficient compensation evidence.

StateManager.RefreshGameStateAfterExactRollbackAsync :177–181 obtains its own lease; RefreshGameStateReadOnlyAsync :183–186 is an existing held-lease projection seam. Do NOT call the former under an already held lease. RefreshGameStateCoreAsync ends with DetectCurrentSessionTurnNumberAsync, UtcNow for UI LastUpdated and CurrentState assignment (:380–383). It is not admission time/action budget or a source mechanics reducer. Its cold use is runtime reprojection from verified settled state, not permission to resample frozen semantic observations. Do not substitute the profile-repairing RefreshGameStateAsync.
An alternative deferred-decision restore could read/project before commit, but would need exact pending-decision read admission, scalar helper closure and runtime-state rollback. Existing worker DeferredDecision is exact-lease bound and is not automatically a repair-owner API. This consultation recommends preserving the current phase order with explicit durable compensation data rather than silently broadening that worker API.

## Crash-cut accounting

These are conditional dispositions after the named extension, not current guarantees.

| Cut | Actual owner disposition | Remaining proof/obstacle |
|---|---|---|
| Before any declared preparation publication | No issue capability exists | Lost in-memory request/dice/IDs remain R06; no automatic rerun claim |
| During existing backup, initialization or snapshot writes, before original issue | Each existing native write can settle its own decision | No current complete preparation owner/cursor; R01 OPEN |
| Initial original/control batch before intent publication | Publisher proves no declared member application | Must still settle earlier preparation work |
| Initial batch intent/member publication before durable commit | Actual native owner rolls back the complete declared ISSUE set | Browser needs bounded preflight settlement admission; preparing may remain |
| Initial batch committed, response or cleanup lost | Actual native decision proves committed cohort; fresh owner checks original/control/artifacts | RequestStaged may not yet exist; no GM dispatch from history-only state |
| Console provisional request installed, final projection absent | Preserve exact actual request/original and owner cursor | Current route has no durable stage proof; R01 extension required |
| Browser preparing without qualifying journal/complete preparation proof | No generic original admission | Current refusal is sound safety only, not full lawful recovery |
| Browser preparing -> staged native batch pending | Bounded actual browser settlement admits only rollback of that exact batch | Original protection fence extension must be implemented/reviewed |
| Browser staged batch committed before scope.Seal | Cold actual browser owner verifies full staged tuple, original/control and main condition | Seal is hot authority only; no reconstruction of the same live scope |
| Repair restore planned, no native attempt | Same repair revision remains; no applied receipt | Original/source/G must remain exact |
| Restore/native pending, including history and pending override | Actual native owner restores all before-images | No caller-applied pending DTO or split history acknowledgement |
| Restore committed before physical readback/runtime refresh | Fresh restore owner checks exact full cohort and retained compensation data | Current hot-only installed array does not suffice |
| Known refresh failure, compensation not started | Actual durable restore owner prepares exact compensation | No arbitrary second restore; preserve current known-failure policy |
| Compensation native pending/committed | Native owner settles exact compensation cohort and control together | After uncertainty no repair dispatch before outcome proof |
| Projection succeeded but continuation acknowledgement lost | Persisted phase plus exact physical owner proof determines next stage | UI reprojection may be repeated; action/reduction/frozen observations cannot |
| Restore completed/continuation committed before repair request dispatch | Existing history owner continues exact same attempt/start/issues | Worker/GM delivery and full history semantic proof remain R04–R07 |
| Foreign G, unexpected inventory, mixed original/control mode or foreign pending owner | Conflict retains evidence and grants no mutation | Not a legitimate terminal retirement; no downgrade to legacy |

Thus every native decision cut has an existing transactional disposition WHEN entry is genuinely admitted and the whole intended set is declared. Every whole-owner cut is NOT currently covered. The table explicitly identifies those open cases rather than labeling them recoverable on the strength of a filename or hash.

## Finite future checks — UNRUN

1. Actual GameEngine console, live-helper and browser original producers retain exact request identity/dice/gacha/source label and hash/token derivation; no second allocation or scheduler pass.
2. New seed included before hashing; old issued originals immutable; PH-D01 old/new/missing/unknown control matrix.
3. Initial completion faults before intent, each member, commit, cleanup and lost return; exact source/artifact closure, G replacement and foreign before-images.
4. Live-helper request in the same completion; GameEngine original-issued/provisional/final request cuts with genuine original-backed projection.
5. Browser queued/preparing/staged cuts through actual MainAdmission and pending owner; reject arbitrary scope/DTO; no blind native or recorder fallback.
6. Browser uncommitted decision touching protected original refused by default; proposed specific settlement admits only exact owner relation and preserves actual main-run condition.
7. Earlier backup/init/snapshot crash cuts cannot be declared solved by completion-only tests; require genuine preparation-owner fixtures before full R01 acceptance.
8. Ordinary restore missing/corrupt backup, legacy/exact hash mode, newly added tracked files, absent baseline members and exact inventory.
9. Real published pending checkpoint retained in same restore batch; foreign/changed/unpublished checkpoint refused; joint + generic pending combination still refused.
10. Joint actual source reconstruction, permitted command/private-wave overrides, all domain + control members, no-op domain, before-prepare drift and after-readback drift.
11. Restore commit before projection, projection failure, compensation intent/commit/cleanup and lost continuation acknowledgement; same G and exact owner revision retained.
12. No nested lease/profile repair through runtime projection; UI LastUpdated never enters admission UTC or elapsed action budget.
13. Specialized browser/recorder restore ownership remains separately admitted; ordinary joint method does not silently widen.
14. Exact no-publication/debt classification and immutable original retention before dispatch/cleanup; full accepted R04–R07 proof remains separate.

## Remaining acceptance boundary

R01 still needs the precise pre-issue preparation cursor and browser cold-settlement owner contract; a three-member completion helper alone cannot qualify it. R03 needs closed durable restore/compensation phases and exact pending provenance, including specialized browser applicability. These are bounded technical requirements, not a permission question or new conceptual authority grant.

R02 consumer/codec obligations and PH-D01 remain. R04 worker cold disposition, R05 semantic cold validation, R06 lost C/result/identity/observations, R07 no-publication/common/cleanup and R08 synchronized docs/examples/guards remain OPEN. Any emitted seed, pending-action proof or repair-control schema requires same-change Mortal/afterlife/daemon/helper documentation and examples; internal design alone changes no emitted contract.

No new pathname, automatic F2, permanent HOLD as lawful recovery, T197 implementation, new QTE or player activation. Full F1/T179/T192/P0 and D4/guided/visual/Windows/D8 remain open. No runtime qualification is claimed.

FINALSTOP.
