# T197 DEP-C A1: original joinable story-append subaction

Historical source: `05f39661739e6bae26da5af428d9d182e132b1c5`. Conditional DESIGN only; no implementation release.

## Decision and exact scope

Select the existing **`StoryService.AppendMortalJointStoryAsync` source subaction**, reached from `GameEngine.TryReconcileMortalJointStoryTurnAsync`, and choose an **original, genuinely joinable in-process executor**. Retain the actual StoryService, FileSystemManager, parent operation, canonical lease and freshly issued applied-phase capability in their original process. Do not serialize or transfer those objects to a replacement process. A dedicated runner would require a larger source-capability migration and its death would lose this lease-local story proof; it is not the selected variant.

HC remains a proposed handoff phase, with no implemented HC source publisher to choose at this commit. This existing story subaction is the smallest relevant reachable source publisher with a concrete original proof, one derived story leaf, exact no-change classification and an existing native publication owner. The coordinator explicitly agreed this selection and its subaction boundary. It is not renamed HC and does not make the currently blocked death handoff executable.

This design closes the choice of executor and specifies its bounded registration/native-publication contract for review. It does **not** claim B01 closed for the entire source operation, B02 closed for all sources, or an independently accepted implementation contract. One story receipt cannot retire the parent pin, certify its other work, advance the runtime turn, perform terminal cleanup or acknowledge HC. The source method is reachable in current code, but no runtime execution or full feature qualification was performed here.

For **bridge-process loss**, the proposed custodian-owned session runtime must contain this original owner graph from birth: the relevant GameEngine execution context, FileSystemManager, StoryService and the parent task that calls it. A bridge is only the frontend/native relay under the separate A4 architecture. Keeping a task alive merely in today's UI process proves only that the original UI/owner process remained alive; it does not prove survival if that owner process was the failed bridge. Moving the graph after a crash, rehydrating a lease, or sending a capability DTO is forbidden. This hosting integration remains a concrete prerequisite, not a source-backed existing feature.

Original custodian survival remains required. Keeper loss, cold terminal consumption, reboot, new host/PID namespace/container, migration and distinct-root restore stay unresolved. A safe HOLD on those branches is not successful lawful-death recovery or an accepted reduced outcome policy. DEP-C/I/L, full target, H2/H3/HQ and new joint QTE remain BLOCKED; D4/guided/visual/Windows/D8 remain open. Source Mortal n/action/k_death and evaluation n+1 remain unchanged; no source replay, extra turn/dice/reward, new G or target authority.

## Source entry, call graph and capability custody

Historical source is `05f39661739e6bae26da5af428d9d182e132b1c5`. Current HEAD, if different, is metadata only. No working-tree code is evidence. Full Git blob/SHA-256/byte pins and exact literal resolutions are in the JSON. Author configuration is retained Astra/High per coordinator; backend telemetry UNKNOWN.

| Edge | Actual source and behavior | Proposed enrollment / authority consequence |
|---|---|---|
| Reachable outer entry | `GameEngine.TurnLifecycle.cs:885–953` wraps late terminal processing in `RunParticipatingCurrentSessionAsync`; line 952 calls joint reconciliation when `lateBrowserStaged == null`. Active path calls it at 1756, with additional retry checks at 1040/1831. | Register the real outer participating task and reserve its source lane before any possible source effects. This report inventories only the story child, not all earlier/later work of these large entry points. |
| Caller prerequisites | `GameEngine.MortalJointStoryTurn.cs:16–74`: genuine marker/tuple/current runtime, original/full-union and terminal evidence; complete accepted validation; finalization checkpoint. | Preserve all existing decisions. Parsed marker/tuple alone cannot issue a child ticket or story permission. These preceding owners are parent obligations. |
| Immediate story call | Same file 75–101: obtains canonical lease; reads applied phase; verifies projection and runtime tuple; calls `_storyService.AppendMortalJointStoryAsync(lease, applied)` at 86. | New `BeginOriginalJointStoryChild` must register before the owner call, with direct references to that actual lease and capability. Parent retains lease lifetime while child is alive. |
| Source authorizer | `StoryService.MortalJointRepair.cs:70–190`: current session, applied capability, signed original/manifest, source soul/realm/incarnation/action, exact output binding, calculation inputs, history inventory and current bytes. | All awaits, synchronous reads and callbacks belong to the child task tree; no detached writer. Original reader/proof issuers continue to decide authority. |
| Existing publication | Same file 88–94 → `FileSystemManager.cs:904–967` → ordinary branch `FileSystemManager.TrustedLocalStorage.cs:335–350` → `PublishLocalFilesAsync`/`PublishLocalCoreAsync` → `TrustedLocalFilePublication` format1 member-set journal. | One exact D1 debt per actual native transaction attempt; no D2/D3 alias. Freeze before/after first; bind the real transaction ID before intent write. |
| Exact proof | `StoryService.MortalJointRepair.cs:93–100` verifies exact after, mints `MortalJointStoryProof` with private issuer, and validates it on the same lease. Its constructor and validation at 18–67 retain owner/lease/applied references. | The proof stays in-process and lease-local. Serialized row/hashes are data only. Child result is not the parent operation close. |
| Outside child | `GameEngine.MortalJointStoryTurn.cs:90–115` revalidates story/progression/projection, increments runtime turn if needed, performs terminal cleanup, updates runtime response/notifications and logs. | Parent must separately register and join these obligations. Seal observed at child return prevents beginning these later effects. This slice neither inventories nor authorizes them during recovery. |
| Outer finalization / close | `SessionOperationContext.cs:93–171,191–322`: retained result/failure, read-only finalization lease, same-G check, close frame and callback; nested contexts borrow actual main access. | Parent close/finalization tickets remain live after child join. Existing finalization does not replay publication recovery. Child receipt never completes this chain. |

`JointProgressionPhaseCapability` checks actual FileSystemManager and lease reference identity, current G and fresh whole-phase proof (`MortalQteDecisionPublication.MortalJointProgression.cs:23–95`). `MortalJointStoryProof` likewise refuses a different owner/lease. Capability transport is therefore **no transport**: a registered in-process invocation with retained references. A frontend may receive detached status and display data; it receives neither object nor an instruction that can recreate them. This is a proposed ownership placement, not a claim that the current application already hosts these owners in an outliving custodian.

## Exact publication and guard leaf catalog

The selected member-set contains **one gameplay write leaf**. Directory prerequisites and native journals are separate owned artifacts. Guards never become mutation members merely because they are read.

| Role | Exact path rule / actual owner | Effect and eligibility |
|---|---|---|
| S01 story member | `StoryService.GetStoryPath(beforeSoul.currentRealm, beforeSoul.currentIncarnation)`; original realm must equal current soul realm and current incarnation must equal original. Ordinary Mortal result is `stories/mortal_life_{i > 0 ? i : 1}.jsonl`. Existing helper also maps Chaos Sea/Море Хаоса/empty to `stories/chaos_sea.jsonl`, Shining Abode/Сияющая Обитель to `stories/shining_abode.jsonl`. | Use the actual helper result only after the full Mortal applied/source owner admits it; helper branches are not realm eligibility grants. Replace/create exactly this file with frozen prefix+BOM/newline semantics below. Never delete another story or history file. |
| S02 current mandatory row inputs | `game_state/meta/soul_state.json`, `output/narrative_response.json`, `game_state/world/current_location.json`. | Required non-null object roots. Current realm/incarnation equal original; nonblank action/narrative. Location name falls back to Неизвестно by existing calculator. Re-read exact bytes after calculation. No writes. |
| S03 conditional display inputs | Guardian tag reads `game_state/meta/guardians.json`; resident tag reads `game_state/meta/guardian_abode_residents.json`; NPC tag reads `game_state/npcs/npc_core.json`. | `guardianId`/`sourceGuardianId`, `residentId`/`sourceResidentId`, `npcId`/`NPCId`/`sourceNpcId` via existing tag parser. Only applicable roots read; absent roots yield refs with absent display name, not universal ineligibility. Malformed present object fails. No writes. |
| S04 original headers/context | Exact manifest and authority constants in `ControlAndProofPaths`; request-context candidates are validation_repair_request, input/turn_request and ready/turn_complete. | Reader requires genuine signed original and compatible context, normalized original Realm=mortal_world, exact session/request/snapshot/turn. Headers are preserved, never rewritten or cleaned by this child. |
| S05 original image reads | `manifest.Files[logicalPath]` for the required or genuinely observed optional logical paths requested by the actual reader invocations: soul; quartet; original QTE projection; marker/partition; schedule. | Each selected logical path must map unambiguously under the declared pending snapshot directory and match its signed hash mode. Original absence must come from the validated presence contract. No generic snapshot-directory membership grant. |
| S06 original rollback evidence | Every value of the genuine `RollbackBackups` map passed through `TryValidateManifestForReaderAuthority` → expected authority payload → `CopyNormalizedFileHashes` (`PendingTurnSnapshotAuthority.cs:482–521,765–803,856–885`). | Reader recomputes declared backup hashes with the recorded hash mode. These reads do not authorize rollback or backup deletion. Snapshot hashes, original proof and rollback roles remain distinct. |
| S07 current completion guard union | Exact 42-leaf `JointFootprintPaths` expansion in appendix/JSON, built from quartet, base footprint, item projection, resource-source authority, inactive tail and wound pending. | `ReadJointCompletedProofAsync` checks complete current footprint, receipt/history/resource partition, whole occurrence source and exact output binding. No mechanics, resources, wounds, pending or QTE writes in this child. |
| S08 progression current guards | Seven `MortalJointCalculationSourcePaths` in appendix. Applied schedule/report use exact after-images; other frozen sources use current source bytes. Original schedule is also compared to signed original. | Recomposition validates prior progression data; it is not applying progression again. No schedule/report write or extra tick. |
| S09 command/decision guards | Three MechanicalCommandPaths must be absent. Public decision path may be absent after consumption or present only with exact original payload fingerprint. Private wound pending must be absent per genuine completed proof. | No command issuance, consumption or deletion here. Presence/absence is owner-local, not a generic “missing means no work” test. |
| S10 history continuity | `manifest.ClientOwnedValidationHashes` must contain `game_state/history/chat_log.json` and only valid `stories/*.jsonl` entries otherwise. Other history entries must match existing text-hash/absence semantics. Enumerate physical story names through exact `SelectOriginalStoryContinuityFiles` selector and reject duplicates/case alias/unlisted unrelated names. | Every enumerated history remains a read/guard, except S01. The directory enumeration is a refusal witness, not a namespace write grant. No chat append. |
| S11 native journal | Root-relative `.boe_runtime/trusted-local-publication-v1/{intent.tmp,active.json,commit.tmp}`. | Existing format1 serialized native before/after member-set. Original `TransactionId` is generated by ApplyMembers, not guessed or recomputed by recovery. `active.json` uncommitted means rollback only; committed means preserve after and clean owned scratch. |
| S12 native scratch | Beside S01: `.boe-local-{transactionId}-0.stage` and `.boe-local-{transactionId}-0.undo`. | Exact single member index0, validated transaction identity. Stage holds frozen after; undo holds frozen before only when rollback requires it. Cleanup deletes only those names plus S11's owned metadata. No wildcard scratch deletion. |
| S13 namespace prerequisites | Actual physical root/game_session, parent of S01 (stories), runtime publication directory and lock directory. | Current PublishLocalCore can EnsureDirectory before native intent; BeginPublication/Recover can ensure journal root. For this narrow child profile, require these directories already present and safe under the original fence, so those calls have no creation effect. Missing namespace is a hold for the parent initializer/namespace owner; the child may not quietly create it outside an armed plan. A broader creation profile remains separate. |
| S14 runtime G and lock witnesses | `.boe_runtime/session-generation/current.json`; original main-owner/canonical/lifecycle guards as applicable. | Read actual existing same G; no bootstrap or generation mutation. Locks/metadata are owned by parent/A2/D0, not story-native cohort members. |

The companion JSON contains 12 explicit source-derived registry projections and 45 literal-symbol resolutions, with source pins. `FootprintAdditional` is the deduplicated additional portion of the base footprint, not an independent permission list. The 42-leaf guard union is not the story write set and not a full T197 target catalog. Dynamic S05/S06/S10 membership derives from genuine admitted original/owner data, never a raw DTO/M or an arbitrary root scan.

**Exact row and prefix semantics.** The existing pure `CalculateMortalJointStoryRow` freezes requestId, G, turn, source preparedAtUtc, original realm, player action, normalized narrative, location and conditional refs. It does not read a fresh clock or roll dice. The story owner chooses the suffix bytes `UTF8(rowJson + "\n")`. If the prior story is absent, the after-image begins with the UTF-8 preamble. If present, preserve its exact captured bytes, including BOM framing; nonempty decoded prefix must end in newline. Every prior nonblank row must be valid and have turn<n and no same (requestId,G). The prior history hash uses the existing decoded-text hash semantics; it is not automatically an exact-byte original hash. Native before/after images nevertheless use the exact captured bytes.

**No-change is specific:** `AlreadyAppended` requires the exact suffix, a prefix matching the genuine signed history baseline (or exactly BOM for original absence), valid earlier rows and all fresh original/phase/output/history guards. A missing story file with genuine absent baseline is eligible for a create; it is not no-change. A missing required source, malformed root, foreign suffix, unrelated added history, stale G or changed exact source is refusal. Read-only `ReadMortalJointStoryAsync` can return OriginalUnappended; the selected append mode cannot use that disposition to mean completed append.

## Chosen joinable executor and pre-effect work ownership

Proposed private types are `CustodiedSessionExecution`, `OriginalJointStoryJoinScope`, `FrozenOriginalJointStoryAppend`, `OriginalStoryNativeAttempt` and `JoinedJointStoryReceipt`. Their constructors require the named live issuer; none is serializable or constructed from the proposed metadata below. They are not accepted APIs yet.

`CustodiedSessionExecution.BeginOriginalJointStoryChild(parent, files, storyOwner, lease, applied)` runs under the parent's admission/registration mutex. It checks exact owner/process references, same root/G and original parent operation, open parent registration, no previous child for this exact tuple, actual active lease and applied capability. It installs a not-yet-invoked completion promise and increments the actual parent's work borrow **before** invoking any async body. Only then it invokes the owner; synchronous invocation failure also completes that already-registered promise with a retained failure. Thus an async function's synchronous prefix cannot run unregistered before its Task is returned. It returns a join handle, not a new main-operation grant.

The parent cannot dispose its canonical lease, close its original operation, release the source lane or discard original objects while the child promise or any pre-enrolled native slot is unfinished. Nested filesystem/helper reads borrow this same access and expected G; they do not open a replacement remote pin, use an ambient root from another operation, or create a new G. A fresh capability on a later lease must be issued through the existing real source validator and is not the old child resumed.

| Ticket | Register before | Actual work it joins | Completion / limit |
|---|---|---|---|
| W0 parent invocation | Calling the real outer async delegate, before its synchronous prefix | Real top-level task; all other registered source work and finalization | Not closed by W1–W7. Full parent enrollment remains B01 outside this slice. |
| W1 StoryBody | Invoking CompleteMortalJointStoryAsync | Source/phase/history reads, pure row calculation, append await, readback/proof validation and caught outcome | Original actual Task settles with its typed result/failure; no fire-and-forget work. |
| W2 SourceReadGroup | First original/current filesystem read | S02–S10/S14 reads, synchronous snapshot readers, all async FileStream reads, retries and DisposeAsync | Every opened stream and awaited delay completes/disposes before the group joins. Read loops inherit this ticket; no unbounded detached callback. |
| W3 BorrowedCanonicalLease | Child entry under parent registration mutex | The exact caller-owned canonical lease and its main access borrow | Child releases only its borrow after W1/W2/W4/W5/W6; parent performs real lease disposal/finalization separately. |
| W4 NativeAttempt | Before any attempt initialization/directory effect/journal write | Frozen exact S01 before/after, native transaction preparation, S11/S12 stages, flushes, renames, possible rollback, cleanup | Actual owner records outcome before facade throws; completion requires native calls and exact cleanup classification, not merely the Task or story return. |
| W5 ProofReadback | Reserved at child entry; activated after original publication decision | Re-read source/phase and exact after; repeated ResolveBackupPublicationRecovery may finish the same registered native journal | It cannot recover an unrelated journal or transform a failed native transaction into a new append. Readback failure preserves known native commit. |
| W6 Callbacks/diagnostics | Before invoking each optional callback or diagnostic sink | Read-open/mutation/publication observers, retry delay continuation, committed-cleanup warning | Selected production profile uses existing public FileSystemManager constructor with hooks:null. Instrumented variants must have owned joinable callback tokens; async-void/detached writers refuse qualification. Diagnostics need no canonical mutation authority; if a sink has outstanding operation-owned I/O, its actual join/flush remains required. |
| W7 Parent return/finalization gate | Reserved before W1 invocation | Typed child result handoff and check of sealed state before parent continues; later parent closing hook, readonly finalization lease and main close are separate parent tickets | Child result cannot start turn increment, terminal cleanup or HC after seal. It does not dispose the parent pin or certify close. |

No native process is launched by the selected story subaction. Native I/O here means its actual file opens/reads/writes/flushes/moves/deletes, all awaited or synchronous within joined work. The existing GM terminal and its descendants are separate parent/native-scope obligations and cannot be inferred from this table. Existing InProcessMutationRegistration and AsyncLocal frames are useful mechanisms but not the issuer of this exhaustive join receipt.

The ordinary FileSystemManager public constructor sets hooks:null (`FileSystemManager.cs:589–613`). The currently exposed hook delegates and synchronous publication observer can execute arbitrary code in instrumented configurations. This design does not assume their return proves any detached work has stopped. Either retain the null-hook production profile, or enroll an explicitly reviewed joinable callback implementation before invoking it; the latter is future qualification. Existing logger providers are not audited by this source pass. A diagnostic sink that can mutate canonical files or run unjoined operation work keeps this slice unqualified; a log line is never a substitute receipt.

**Why not a dedicated runner for this unit:** the story proof and applied capability deliberately bind actual owner/lease identity. Moving only this function into a new process would require reconstructing its original capabilities and coordinating the parent's same lease. Serialization is not a lawful solution. Hosting the already-owned execution graph in the custodian from run birth preserves the reference semantics; its broader hosting integration remains B01 prerequisite. The selected executor has no forced-thread-abort guarantee. If its task hangs while the original custodian lives, recovery safely waits/holds; it never kills the custodian to manufacture completion.

## Freeze, arm, seal and publication settlement

Proposed `StoryService.FreezeOriginalJointAppendAsync(joinScope, lease, applied)` reuses the actual private source reader and existing pure calculator. Before any append, it retains the exact source result `(path,row,current,after,already,histories)`, its real issuer references and guard-role inventory. It preserves original/history hash modes and exact native images. This is a routine same-owner extraction of data **only after** genuine source authority; the new join/lifetime issuer itself is not routine D11.

`TrustedLocalFilePublication.BeginOriginalStoryAttempt(joinScope, frozen)` is a new typed adapter to the existing publisher. It requires one ordinary member, no MutationIntentRecorder, no legacy recovery mode, no BrowserLocalAccess recorder, no worker publication purpose and same existing G. Those conditions are proved from the actual lease, not from a field in metadata. The demonstrated callsite is the non-browser branch; no browser-original D3 writer is silently admitted. The adapter binds the native owner's actual generated transaction ID and exact member/guard descriptors under the registration mutex **before** WriteJournal(intent.tmp) or any namespace mutation. This seam must be implemented within the publisher; a later observation callback is too late to establish pre-effect ownership.

The selected profile requires safe existing story/runtime directories and a genuinely idle journal/legacy-storage preflight before W1 starts. Lease acquisition itself can run storage recovery (`FileSystemManager.cs:3850–3938`), so it is parent work and must be registered **before acquisition**, not retroactively attributed to the story child. `ReadJointProgressionPhaseAsync` and `ReadJointCompletedProofAsync` also call ResolveBackupPublicationRecovery. Before this child arms its native attempt, those reads may see only the preflight's idle namespace; unexpected debt is refused and handed to its existing parent/native owner. After arming they may settle only that exact registered story transaction. An absent active.json is insufficient if intent/commit stage, scratch, retained native outcome or another owner ticket remains.

`StoryNativeAttempt` pre-enrolls a fixed set of native slots: journal intent write/flush/dispose; intent rename; one member stage write/flush/dispose; member replace/create; commit write/flush/dispose; commit rename; exact before-restore if required; stage/undo/intent/commit/active cleanup; final exact before/after/absence readback. These are execution edges of an already-armed attempt, not new semantic work registrations after seal. Streams remain owned until actual disposal returns. Synchronous Write/Flush/Move/Delete remain part of W4 until their return. A thrown call is not assumed to have had no filesystem effect; the existing native owner classifies its journal.

**Linearization:** parent registration and seal use the same mutex. If seal wins before child registration, do not invoke story. If child registration wins, it is retained in the sealed inventory. If seal wins before arming, freeze/validation may finish as read work but no append starts. If the publisher wins arming, its exact intent is already authorized; original native owner may finish or roll back that one attempt and clean it after seal. This is settling started work, not continuing into the next gameplay phase. Readback/proof validation may complete within pre-enrolled W5. A source guard mismatch is refusal; it does not alter the frozen after-image to fit current state.

**Transient retries are distinct:** current `PublishLocalCoreAsync:237–247` may retry a RolledBack transient failure and generate a fresh transaction ID. Each attempt must be independently registered before effects while parent admission is still open. After seal, a fresh attempt is forbidden even if it would reuse identical bytes. Preserve the actual prior RolledBack outcome and leave semantic continuation to its rightful later owner. A retry is not mislabeled native cleanup. Bound exhaustion or registration failure occurs before that attempt's side effects.

**Typed outcomes and D-kind mapping:**

| Actual native/semantic result | Required retained outcome | Allowed settlement |
|---|---|---|
| AlreadyAppended, complete source proofs, no native attempt | `AlreadyPresentVerified`, exact same-lease story proof, zero D1 debt | Join reads. No fake native transaction or cleanup. Earlier foreign debt is not erased by this result. |
| Refused before native attempt | `RefusedBeforePublication`, actual reason and no armed attempt | Join reads/borrows; no write, rollback or deletion. Missing story alone is not this result. |
| D1 committed, all cleanup and exact readback succeeded | `CommittedVerified`, actual TransactionId plus exact after/proof | Preserve append. No further append or source-original cleanup. |
| D1 committed, cleanup failed | `CommittedCleanupPending`, original native result and exact retained journal/scratch | Native owner cleans only that transaction. Facade may return successfully because RequireCommittedLocalPublication preserves commit; therefore Story AppendedAndVerified alone does not prove D1 debt-free. |
| D1 rolled back with complete before readback | `RolledBackSettled`, exact original before/absence and actual cause | No append retry after seal. No restoring unrelated gameplay files. |
| Native outcome uncertain, journal conflict or missing provenance | `NativeUncertain`, all original evidence retained | No semantic compensation/retry/new operation. Only original qualified native resolution may proceed. |
| Native commit known but story proof/readback refused | Retain **both** native Committed and semantic proof failure | Never restore the story prefix merely to manufacture a clean result. Block parent continuation; later legitimate owner may validate existing facts without replay. |
| Parent lease release/finalization/main close fails after child succeeds | Child outcome retained; parent lease/close debt separate | Child cannot change its result or certify parent retirement. Existing established-result precedence remains intact. |

S01/S11/S12 are one **D1 TrustedLocalCohort** instance per actual attempt. The label `sourceStoryAppend` is a semantic tag, not a second D5 transaction to settle again. D0 recovery/main metadata belongs to the parent custodian store, not this story member-set. D2 ReversibleOriginalPublication and D3 mutation-recorder/original routes are outside the selected ordinary profile; actual discovery of either blocks this slice rather than being silently ignored. D4 worker state remains a parent admission conjunct. H2/HC/H3/HQ are not this action and are not armed merely by freezing a story plan.

Original main custody and the parent lane precede this child. The caller already holds its canonical lease when child registration briefly takes the registration mutex. Never acquire or wait for canonical/lifecycle locks while holding that mutex, and never hold it across awaits; this forbids the reverse acquisition edge. Parent registration before initial lease acquisition also releases its mutex before awaiting that lease. Recovery seals, releases that mutex, and waits for W1–W6 outside any newly acquired canonical/lifecycle lock; the caller's existing lease is allowed to support its own already-armed native settlement. Only after actual tasks/borrows/native scope are exhausted can the **parent** recovery path obtain lifecycle then canonical for exact residual debt work. No new client attempts to reacquire the custodian's main guard while waiting on the custodian. This child cannot supply the parent’s all-writers/native retirement proof.

## Closed slice metadata and interfaces

These are proposed closed in-process/data contracts, not implemented schemas or live capability deserialization. B03's parent CAS/version/main-Stopped codec and B04's endpoint/caller rights remain open and unchanged. No endpoint/key/service design is added here.

`JointStoryChildDescriptorV1` exact fields: `schemaVersion`, `parentOperationRef`, `childId`, `ownerInstanceId`, `sourceTuple`, `phase`, `story`, `guards`, `nativeAttemptRefs`, `result`. Version is integer1. New child/owner/attempt identifiers use the existing exact lowercase Guid-N convention. Existing parent identifiers retain their original typed domains; RootKey, Backend, BootId and tuple values are not reinterpreted as Guid-N. `parentOperationRef` is exactly `(RootKey,RunId,GenerationId,Epoch,Backend,HostInstanceId,BootId,OperationId,PinId,CloseId)` from the live parent; it is correlation only. `sourceTuple` is `(sessionId,requestId,snapshotToken,turn)` from the real source issuer. Phase is exactly `Registered|Validating|Frozen|NativeArmed|Joining|Joined|Held`. `story` is null until frozen, then `{path,rowUtf8Sha256,before,after,classification}`; classification is `OriginalUnappended|AlreadyAppended`. Images are `{exists,length,sha256,originalImageRef}` with null ref/hash iff absent, length0 iff absent or actual empty bytes; the existing native owner binds real images. A hash/reference does not grant file access.

`guards` is an array of `{role,logicalPath,physicalSourceRef,observationKind,comparisonKind,digest}` emitted by the actual source reader. Role is one of `CurrentRowInput,ConditionalRowInput,OriginalHeader,OriginalContext,OriginalSnapshot,OriginalRollbackBackup,CompletedUnion,ProgressionCurrent,CommandAbsence,DecisionPayload,HistoryBaseline,HistoryNamespace,CurrentGeneration,NamespacePrerequisite`. ObservationKind is exactly `RequiredPresent|OptionalPresent|OptionalAbsent|RequiredAbsent|NameInventory|LiveProof|ExistingDirectory`. ComparisonKind is exactly `ExactBytes|OriginalSnapshotHashMode|OriginalRollbackHashMode|HistoryDecodedTextHash|ExactAbsent|ExactNameSet|ExistingOwnerProof`. An absent observation has a null digest; a live proof/directory records its typed original reference, not a fabricated byte hash. The parent strict codec and exact reference encoding remain B03 prerequisites. Do not replace existing original/hash-mode validation with a universal SHA comparison. Dynamic rows must correspond exactly to the admitted owner maps/registries described above; unknown role/path alias/member is refusal. `ExistingOwnerProof` references the live applied/original proof and records a digest only for correlation; it is never a serialized substitute.

`OriginalStoryAttemptDescriptorV1` exact fields: `schemaVersion,childId,attemptOrdinal,nativeTransactionId,armedParentSequence,generationBefore,generationAfter,member,journalNames,scratchNames,outcome,cleanupState`. Member is the one frozen S01 pair; both G fields equal current existing G. NativeTransactionId is assigned by the actual publisher before effects. Journal/scratch names must equal the deterministic S11/S12 derivation; no caller-supplied freeform namespace. Outcome is `Pending|Committed|RolledBack|Uncertain`; cleanupState is `Pending|ObservedComplete|Uncertain`. Attempt ordinal is positive, monotone and bound by the actual publisher retry policy. New attempts require parent still open; existing attempts remain referenced until their actual outcome/cleanup completes.

`JoinedJointStoryReceiptV1` exact fields: `schemaVersion,childId,parentOperationRef,sourceTuple,storyPath,childDisposition,nativeAttemptResults,readbackDigest,workJoinSequence,parentContinuationDisposition`. ChildDisposition is `AlreadyPresentVerified|CommittedVerified|CommittedCleanupPending|RolledBackSettled|RefusedBeforePublication|NativeUncertain|ReadbackRefused`. ParentContinuationDisposition is only `StillOwnedByParent` or `HeldByParentSeal`; never `Closed`, `Retired`, `TurnCompleted`, `HCResolved` or `TargetAccepted`. The live `JoinedJointStoryReceipt` constructor additionally requires actual joined task handles, same original object references and native outcomes. Decoding V1 yields data only.

Proposed APIs: `BeginOriginalJointStoryChild` (new A1 issuer), `FreezeOriginalJointAppendAsync` (same-owner pure-data extraction), `BeginOriginalStoryAttempt` (native pre-effect registration seam), `PublishFrozenOriginalStoryAppendAsync` (existing ordinary publication with exact frozen member), `ObserveOriginalStoryAttemptOutcome` (retains real typed outcome before facade/cleanup loss), `JoinOriginalJointStoryAsync` (actual task/stream/borrow join), `ObserveParentSealBeforeStoryContinuation` (same-parent gate immediately before turn/cleanup). No API starts an operation from descriptor bytes. Source filters, fixed enums and bounds are checked before invocation; outstanding records cannot be evicted while parent/source references remain.

Bounds reuse the accepted publisher/reader limits where they exist and the proposed parent admission budgets from the prior A4 design; this report does not invent a new smaller legal source limit. The concrete unit has one member per attempt and finite declared guard roles, while original snapshot/backups/histories have actual finite owner-derived counts. An admissible maximum and overflow policy across those dynamic maps is still B03/parent qualification. Do not truncate a map to fit a budget or treat incomplete guard capture as exhaustive.

## Seal, EOF and crash cases

| Race/cut | Correct child behavior and remaining obligation |
|---|---|
| Seal before registration vs after registration | Same mutex decides. Before: no body invocation. After: real task and refs remain in sealed inventory. A missing Task assignment window cannot count as done because the promise is installed first. |
| Seal while an async read or synchronous source proof runs | Join actual read/disposal. No native append may arm after seal; no detached continuation may create a new writer. |
| Seal between freeze and native intent | The pre-effect attempt-arm check decides. A frozen row alone is not started publication and cannot be executed by recovery. |
| Seal after native arm but before member rename | Existing native owner may settle this frozen attempt. It must not recompute a new row or start a next transaction/phase. |
| RolledBack transient failure races seal | Retry admission consults the same parent seal before creating/registering its fresh attempt. Seal wins → retain rollback result; no new append. |
| Commit succeeded, cleanup/observer/logging fails | Preserve native Committed independently from semantic result. Track exact cleanup and any actual callback/I/O obligation; no rollback of committed story. |
| Proof reader sees another journal | Refuse before recovery side effect under the proposed exact-attempt adapter. This is not story debt just because ReadJointProgressionPhase called recovery. Parent original owner remains responsible. |
| Child returns while parent has sealed | Deliver retained outcome to parent, hold before runtime increment/terminal cleanup/HC. Main pin and parent lease/finalization are not discharged by child join. |
| Normal close frame accepted but ACK lost | Parent A2 queries its retained exact close frame. Do not rerun the story child or turn AlreadyAppended into a new operation grant. |
| Bridge frontend/process dies, custodian runtime lives | Same original owner graph/task/native objects remain; join/settle only this actual child. Proposed placement must already exist from birth. If the owner graph was in the failed process, this positive case does not apply. |
| Original executor task hangs | Safe HOLD with original refs. Cancellation request, timeout, thread count and FileShare lock acquisition do not establish join. No forced thread abort. |
| Custodian/owner process dies | Live lease, applied/story proof and join issuer are lost. Native journal may still be data, but this slice cannot create a fresh A1 receipt or parent retirement. B05 blocks cold continuation. |
| Reboot/new host/container/PID namespace/distinct-root copy | No original-domain proof is gained. B05/DEP-L remain open; old host/escaped work may still exist. No new G or copied capability. |
| Native supervisor owner EOF | **O1 precision:** C helper latches owner-lost uncertainty and continues its stop/reap loop. It emits terminal Uncertain and exits only when `stopping && empty && !exec_pending` actually holds. EOF itself proves neither prompt exit nor successful retirement. Current sticky uncertainty is preserved. |
| Foreign/missing source original after known story commit | Preserve accepted story bytes and evidence; no source replay, late cleanup or inferred HC. Parent must resolve its genuine source/target obligations. |

The selected source call is currently gated away from browser-staged execution (`lateBrowserStaged/browserStaged == null` at the actual callsites). This report does not claim browser parity or grant browser original deletion. Console/agent routing that reaches the same real owner can use the same child status once integrated; display status cannot authorize another action. The source's old accepted result and story row are not reoffered to the player.

## What this refinement resolves and what remains open

**Resolved at the proposal level:** named reachable owner/callsite; joinable executor choice; no capability serialization; one native write leaf and exact journal/scratch derivation; 42-leaf existing completion guard union plus explicit direct/conditional/original/history roles; pre-invocation registration race; actual native arming before effects; no fresh retry after seal; committed cleanup and proof-failure distinctions; exact nonauthority child receipt; O1 wording precision.

**Not closed:** (1) moving/hosting the actual source execution graph in a surviving original custodian from run birth and enrolling the entire outer task; (2) parent lane before the source can produce lawful death, native CLI/worker writers and every later increment/cleanup/finalization/close; (3) implementation of exact-attempt recovery interception and callback/diagnostic ownership; (4) B03 durable CAS/schema/version/limits and B04 endpoint/caller authorization; (5) B05 cold keeper/reboot/migration/distinct-root issuer and lawful recovery policy; (6) B06 platform qualification and all independent target/H2/H3/HQ contracts. The noncreating namespace profile is intentionally narrow; a missing required directory remains another owner’s initialization/recovery obligation, not ignored work.

Thus this is a concrete vertical **subaction** design ready for independent review, not “B01 PASS,” a complete all-writers source proof, an accepted reduced lawful-death policy or an implementation release. Actual exclusive-lane hosting and parent orchestration are new ownership contracts; same-owner row extraction is ordinary specialization. Missing helpers alone do not grant new authority. User autonomous direction permits continued agreed application design without repeated permission, while independent exact contract acceptance and bounded TDD/qualification still precede implementation. No endpoint/key/service/OS/runtime changes are made or authorized here.

## Future checks — all UNRUN

* **V01 Invoke-vs-seal race — UNRUN:** Seal on both sides of installing the child promise and before the async body returns its Task; no unregistered synchronous-prefix effect.
* **V02 Original object identity — UNRUN:** Different owner/files/lease/applied capability, copied descriptor or old G refuses; genuine in-process references work only while original host lives.
* **V03 Bridge loss hosting — UNRUN:** Kill frontend bridge while owner graph is in original keeper; real join continues. Kill actual owner process instead and require HOLD, never rehydration.
* **V04 Missing story — UNRUN:** Genuine absent original history creates precisely BOM+one frozen row+newline, not AlreadyAppended/no-change.
* **V05 Already appended — UNRUN:** Exact prior suffix/prefix/source proofs yields no native attempt; foreign suffix, extra turn, duplicate request/G or unlisted history refuses.
* **V06 Conditional inputs — UNRUN:** Each guardian/resident/NPC tag combination; absent optional roots vs malformed roots; frozen output/row unchanged across seal.
* **V07 Native phase cuts — UNRUN:** Before/after actual intent staging/publication, story stage/rename, commit stage/rename, each cleanup; exact one D1 outcome, no duplicate D5 settlement.
* **V08 Fresh retry suppression — UNRUN:** Transient rollback before seal may register next real attempt; after seal no new TransactionId or append even with identical bytes.
* **V09 Committed cleanup debt — UNRUN:** Native Committed plus cleanup failure remains tracked even if facade/story readback returns success; resolve exact original journal only.
* **V10 Readback refusal after commit — UNRUN:** Change a guard/source during readback; retain native commit and semantic refusal independently, no story rollback/replay.
* **V11 Hidden recovery and namespace effects — UNRUN:** Foreign journal at lease/proof boundaries, commit-stage-only evidence, missing stories/runtime parent: no unregistered recovery/creation; preserve parent debt.
* **V12 I/O/callback join — UNRUN:** Delayed async read/disposal, sync native error, hook spawned work, diagnostic outstanding I/O; no receipt before actual owned completion, timeout remains pending.
* **V13 Parent borrow/finalization — UNRUN:** Child success does not release lease/main pin; seal before parent return prevents turn increment/terminal cleanup; finalization/close failure retains child outcome.
* **V14 Original/read-role integrity — UNRUN:** Tampered signed backup, snapshot hash mode, history text hash/BOM, 42-leaf guard union, progression and command absence; reject without broadening write inventory.
* **V15 Native EOF/cold migration — UNRUN:** EOF latches uncertainty; actual empty+no exec required even for helper exit. Keeper death/reboot/new host/distinct root never yields original child or parent retirement.
* **V16 Global limits — UNRUN:** Non-browser callsite and supported storage profile only; no HC/H2/H3/HQ/target activation, new G, source replay or extra tick/dice/reward from child receipt.

No build, test, discovery, native execution, runtime or OS qualification was performed. Static source/constant/digest checks only. No F2 or runtime PASS.

## Final stop

FINALSTOP — read-only source/design refinement complete for independent review; all new interfaces remain UNACCEPTED. No repository writes, no pending file writes after delivery, no subagents or external messages/services. Prior proposal/review pairs remain unchanged. Full G-C and T197 remain blocked.

## Exact source registry projections

These are read/guard membership projections, not write grants or a whole-parent catalog.

**QuartetPaths (4)**

- `game_state/resources/resource_definitions.json`
- `game_state/resources/resource_history.json`
- `game_state/resources/resource_owner_authority.json`
- `game_state/resources/resource_state.json`

**FootprintAdditional (14)**

- `game_state/combat/allies.json`
- `game_state/combat/enemies.json`
- `game_state/control/pending_effect_resolutions.json`
- `game_state/control/pending_mortal_wound_occurrences.json`
- `game_state/effects/effect_identity_index.json`
- `game_state/meta/afterlife_entity_profiles.json`
- `game_state/meta/afterlife_spiritual_conflict_state.json`
- `game_state/npcs/npc_effects.json`
- `game_state/npcs/npc_wounds.json`
- `game_state/player/effects.json`
- `game_state/player/wounds.json`
- `game_state/wounds/wound_history.json`
- `game_state/wounds/wound_identity_index.json`
- `game_state/wounds/wound_opportunity_receipts.json`

**ProjectionRootPaths (13)**

- `game_state/inventory/item_bonds.json`
- `game_state/inventory/item_identity_index.json`
- `game_state/inventory/item_removals.json`
- `game_state/inventory/item_text_updates.json`
- `game_state/inventory/items.json`
- `game_state/inventory/recipes.json`
- `game_state/misc/vehicles.json`
- `game_state/npcs/item_journals.json`
- `game_state/npcs/npc_core.json`
- `game_state/npcs/npc_inventory.json`
- `game_state/quests/quest_history.json`
- `game_state/world/current_location.json`
- `game_state/world/location_storage_contents.json`

**SourceAuthorityPaths (18)**

- `game_state/combat/allies.json`
- `game_state/combat/enemies.json`
- `game_state/inventory/item_bonds.json`
- `game_state/inventory/item_identity_index.json`
- `game_state/inventory/item_text_updates.json`
- `game_state/inventory/items.json`
- `game_state/inventory/recipes.json`
- `game_state/meta/afterlife_entity_profiles.json`
- `game_state/meta/afterlife_spiritual_conflict_state.json`
- `game_state/meta/guardians.json`
- `game_state/meta/shining_abode_state.json`
- `game_state/meta/soul_state.json`
- `game_state/misc/vehicles.json`
- `game_state/npcs/item_journals.json`
- `game_state/npcs/npc_core.json`
- `game_state/quests/quest_history.json`
- `game_state/world/current_location.json`
- `game_state/world/location_storage_contents.json`

**MortalJointInactiveTailPaths (7)**

- `game_state/control/qte_deferred_effect_continuation.json`
- `game_state/control/qte_runtime.json`
- `game_state/core/player_status.json`
- `game_state/player/experience.json`
- `game_state/player/stat_points.json`
- `game_state/player/status_changes.json`
- `output/qte_offer.json`

**JointFootprintPaths (42)**

- `game_state/combat/allies.json`
- `game_state/combat/enemies.json`
- `game_state/control/pending_effect_resolutions.json`
- `game_state/control/pending_mortal_wound_occurrences.json`
- `game_state/control/pending_wound_resolutions.json`
- `game_state/control/qte_deferred_effect_continuation.json`
- `game_state/control/qte_runtime.json`
- `game_state/core/player_status.json`
- `game_state/effects/effect_identity_index.json`
- `game_state/inventory/item_bonds.json`
- `game_state/inventory/item_identity_index.json`
- `game_state/inventory/item_removals.json`
- `game_state/inventory/item_text_updates.json`
- `game_state/inventory/items.json`
- `game_state/inventory/recipes.json`
- `game_state/meta/afterlife_entity_profiles.json`
- `game_state/meta/afterlife_spiritual_conflict_state.json`
- `game_state/meta/guardians.json`
- `game_state/meta/shining_abode_state.json`
- `game_state/meta/soul_state.json`
- `game_state/misc/vehicles.json`
- `game_state/npcs/item_journals.json`
- `game_state/npcs/npc_core.json`
- `game_state/npcs/npc_effects.json`
- `game_state/npcs/npc_inventory.json`
- `game_state/npcs/npc_wounds.json`
- `game_state/player/effects.json`
- `game_state/player/experience.json`
- `game_state/player/stat_points.json`
- `game_state/player/status_changes.json`
- `game_state/player/wounds.json`
- `game_state/quests/quest_history.json`
- `game_state/resources/resource_definitions.json`
- `game_state/resources/resource_history.json`
- `game_state/resources/resource_owner_authority.json`
- `game_state/resources/resource_state.json`
- `game_state/world/current_location.json`
- `game_state/world/location_storage_contents.json`
- `game_state/wounds/wound_history.json`
- `game_state/wounds/wound_identity_index.json`
- `game_state/wounds/wound_opportunity_receipts.json`
- `output/qte_offer.json`

**MortalJointCalculationSourcePaths (7)**

- `game_state/control/progression_report.json`
- `game_state/control/progression_schedule.json`
- `game_state/control/validation_repair_request.json`
- `game_state/meta/soul_state.json`
- `game_state/world/world_time.json`
- `input/turn_request.json`
- `ready/turn_complete.json`

**MechanicalCommandPaths (3)**

- `game_state/effects/effect_commands.json`
- `game_state/resources/resource_commands.json`
- `game_state/wounds/wound_commands.json`

**OriginalQteProjectionPaths (9)**

- `game_state/combat/allies.json`
- `game_state/combat/enemies.json`
- `game_state/control/pending_mortal_wound_occurrences.json`
- `game_state/meta/afterlife_entity_profiles.json`
- `game_state/npcs/npc_wounds.json`
- `game_state/player/wounds.json`
- `game_state/wounds/wound_history.json`
- `game_state/wounds/wound_identity_index.json`
- `game_state/wounds/wound_opportunity_receipts.json`

**DirectStoryPaths (3)**

- `game_state/meta/soul_state.json`
- `game_state/world/current_location.json`
- `output/narrative_response.json`

**ConditionalStoryPaths (3)**

- `game_state/meta/guardian_abode_residents.json`
- `game_state/meta/guardians.json`
- `game_state/npcs/npc_core.json`

**ControlAndProofPaths (8)**

- `.boe_runtime/session-generation/current.json`
- `game_state/control/pending_turn_snapshot.authority.json`
- `game_state/control/pending_turn_snapshot.json`
- `game_state/control/validation_repair_request.json`
- `game_state/wounds/mortal_qte_decision_publication.json`
- `input/turn_request.json`
- `output/mortal_wound_decisions.json`
- `ready/turn_complete.json`

## Historical source pins

| Path | Blob | SHA-256 | Bytes |
|---|---|---|---:|
| `.specify/memory/constitution.md` | `a307f01cf71528cc53a2ff4d45a0c6c16c34f97d` | `f3e8f4de6a6ac7c82f68ea6f6b37d9dcb95901f59f32b1a7f6353217bc490944` | 14081 |
| `AGENTS.md` | `ba14a76f2f3e3ae72f767440412b67b71779da5e` | `4410cc7fb11213629792b280c9a2ba3bdb1d74316dc7950072ce7c158a9c5e23` | 11662 |
| `BookOfEternityClient/Core/FileSystemManager.BackupLifecycle.cs` | `eaa988979b4e709a2a5b4abdd3e24b65d7aa4f84` | `71954c0c0e0b73c989208d870470ea9ab7906c9e28aea8d42c06923db90a507f` | 7681 |
| `BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs` | `3f892cc44d59d81d49d1cba3b4f8199c5d92d33a` | `384f9f65d33e1c70c6f7e337d96aec6e71664d142612546139345bf274923a14` | 20796 |
| `BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs` | `22d07c130ea56b5cab5ff3bbf04c2653c8ff0305` | `7154852b8ef29afa1262cef0e4bb3a0ff28f14afeebe98c2a0834676e4c5cf5b` | 19220 |
| `BookOfEternityClient/Core/FileSystemManager.cs` | `762505d7fbfbd14e4f8e55a7d512fcd35d1f3245` | `14dcb900d73951bf5228d474255c197324ddb32df74a075ce22f4dec64600ec9` | 241343 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointInactiveTail.cs` | `9aacb56c60f7bdc9882852dfe96b4d005c106990` | `df6b509157ac47957df3f2e4ee4c5439f0c0ab403d98281933f4f7b1fbe62ec8` | 5207 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointStoryTurn.cs` | `7f054ea23df4b5156635d9155dd176cdc86a0341` | `bf4a5c905760308cd9bad2579c3eddd3a7320626242acc1d3a299ff5c6b3b5f9` | 10353 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.MortalJointTerminalCleanup.cs` | `04aacf85244601da516466216ff565dfefb868ae` | `ebb42a6ca5bb027fdac4037b788f1b8fb77f6d6a6162cf616b8946bdc55fda83` | 17372 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.SessionAndSnapshots.cs` | `9a000fc10c0778e838c5dd4b9d5ad6e1fecb1ca0` | `85ad957b7fa801930a7d4f13d6abd24e96b2e877d00544c5e74aad4440be6b1e` | 112701 |
| `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs` | `2c017c657d4d3d36be146d052c8a158e42d163c0` | `e2ea305eee08627d224e91fbf10ea274fafb0197cc0c6e54ed03fc5e7db4190a` | 294934 |
| `BookOfEternityClient/Core/SessionOperationContext.cs` | `aa34f9fa8581384a007c1d73b33d927fdabecdd6` | `5b0b98825d0e4f0c6e91a3396b3e54acdda7e774f6afacfcf3fb49b18c155efa` | 23072 |
| `BookOfEternityClient/Core/TrustedLocalFilePublication.Journal.cs` | `418b8da4bdd6c8f3f41f82cd313d600fb234db63` | `4d00cbfdcca3863067c6398ec28175c78288197e095d394efdb13069daa0e0fc` | 7553 |
| `BookOfEternityClient/Core/TrustedLocalFilePublication.cs` | `99081f43140f8f59511d26666e19a6fa57544983` | `b0893b8b12f46f9c76e8b49b764fbac7343e69daea5c058dccd7c70ce570987f` | 33582 |
| `BookOfEternityClient/Services/AcceptedMechanicsPlan.cs` | `1b197626960388f0eb47b2d096297bbf6eb8998b` | `1482737a717b071c781c8a520b92e44e32680fc85d3f56e86e9191b0f2f63fdd` | 96868 |
| `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs` | `86c82a62e138164a7e8158b9aa7adef0fe742916` | `13b716101e4f111435e5a7bf5fe5fa0083b4904b60a0cec920d9512b6efcc885` | 120150 |
| `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs` | `3fb32a3186a15632df4c44a6952bc30959e224b9` | `5d6b770bdf7a4bde1429addbb89cd8354fee99303716dc4ef8106e128eec5ebc` | 51263 |
| `BookOfEternityClient/Services/CanonicalResourceOwnerAuthorityComposer.cs` | `ce8b92001305a754a2cd2c93caacd53c79540551` | `0ebf495c393c9dc7108b0a1c03071336dafcf4f0c4d3cda5006184f84be38900` | 26100 |
| `BookOfEternityClient/Services/EffectAcceptedTurnPlan.cs` | `f8fbf37a4617bf0479dc15aeb2e23e62c563c721` | `fdff8bf9737524d7b0d4437e301dcc38b7088110ac170fc7b2d573fb7bceead1` | 26564 |
| `BookOfEternityClient/Services/EffectCarrierCatalog.cs` | `15ab43b9455efd05f76ffaf564de1aabd55ee0c9` | `ee19e8e3ce918984c0c5dddefd49e7236043ce4a13f452e518ac5763db35c7ab` | 28369 |
| `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.PipePins.cs` | `08ecdab49b89cf0c36cf6cf391c17a46a4db9b9e` | `2473c1f0b83bf142f34ec76ea613127ee8d3ef706c339192e28d9bb907ccad1d` | 5388 |
| `BookOfEternityClient/Services/GmRuntime/GmSessionRunCoordinator.cs` | `7df60d8902617d71daaa28479c4c79a37744d67b` | `eeb23241dff44ce49a484319a76e493d607de4413e1355f9b853ed41c6c7b0b6` | 24413 |
| `BookOfEternityClient/Services/InventoryEquipmentService.cs` | `e147bfb218e387fc80fcbce9b3d802c23d76bac6` | `3d80c8ab26464ee3eddb06cce4a8982807dc0c9e4447e493d28ebe40a22924e5` | 28685 |
| `BookOfEternityClient/Services/LiveTurnPreparationService.cs` | `ce2d9cfebc301cfb6ce678a35df43b65311396e2` | `eea0a8dc65e14bd2234774eb6127b7b9ca16a01583d22d22ea184588fcdcbbd7` | 20289 |
| `BookOfEternityClient/Services/MortalItemAcceptedTransferCatalog.cs` | `c8e49ba2672bc74a1113f6462a1de2b30497b74d` | `5204ff87f59ddc164d716d86a38fde19baa32cdcfeda77cc1b2dadc3b8f956bd` | 22238 |
| `BookOfEternityClient/Services/MortalItemCanonicalProjectionPlanner.cs` | `19c73713357ef4263455ce1343d1b03cb440e9bd` | `e0d9d290889eb598d1568b867a1514c141a66a4be17e1c72036f8d9647be3928` | 20793 |
| `BookOfEternityClient/Services/MortalItemIdentityState.cs` | `9b416723dbee45c8e5f275351cf5367d3c384611` | `50034ba7d600ea21e8da0c34c878719b6cdfc2bb28fb532fe869e3094fd801d2` | 75654 |
| `BookOfEternityClient/Services/MortalLocationStorageContentsState.cs` | `7f22334c4f5b37fbb105c66c0ffefa4a91f2b903` | `27d01910e64b2e8e13cfed19e2fce7ffbadaba7bbc78bfed71f415a07eff2a25` | 12063 |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointProgression.cs` | `7cbb6d25f243e66e32020b13eeb6a2fed2a20bce` | `3b96155adc121ec67ab42b2463cd7c6b4d71e321265b71348d6153273edcfd92` | 19147 |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.MortalJointRepair.cs` | `2ddc3af014dc8ff98d1ebe84622836b154234a5d` | `87dfeec71e98188a10dbfcfcd48ceaf73fd6aca191468a50d7de4e9ec2f950c2` | 33754 |
| `BookOfEternityClient/Services/MortalQteDecisionPublication.cs` | `7e75b1b754db9f688c01727a61a9e77c7b9e8faa` | `18a6dabe883dfcf25c784d3b5148e923eb578e9047ade724a8adb7bb6161bf7f` | 23806 |
| `BookOfEternityClient/Services/MortalQteWoundOpportunityProjection.cs` | `03c8b15353e1fa33c62a7665ce4a39383cbd6cc1` | `33f859765ca5623afe999f646218f4999c9d080bbfec27c4a37cf11e96296174` | 9693 |
| `BookOfEternityClient/Services/MortalWoundOccurrenceState.cs` | `ffb2f1ebc749929c5a4aa5d397df88720772a1ec` | `8429906352687b5e54b2d85b620187d9083778303d497de3aa811648395e5e9d` | 66193 |
| `BookOfEternityClient/Services/MortalWoundOpportunityAdapter.cs` | `4e338e3bfe87a60fe02aa5d6bebf246d7fc6b0a7` | `c6deef4c1e1c5ee5734307e99f7f726c6467f2d3830484931d94a916a410a00a` | 61576 |
| `BookOfEternityClient/Services/MortalWoundOpportunityReceiptState.cs` | `fbceff41e523c9dd9bb5206d6b4f2cc9159139dc` | `73ee8c2837d461b5abcece31db0bac03a34f20a6c025140bb279a39a14ba2fa1` | 63869 |
| `BookOfEternityClient/Services/NpcCoreChangesContract.cs` | `ce25a08ba2833970aa4552708a0f7fbdae131ea8` | `c7bcd5987b3b337fc2c34f1b643a19e52d6cad649516f8e45c704ef430f36969` | 63911 |
| `BookOfEternityClient/Services/PendingTurnSnapshotAuthority.cs` | `19b481dd1880075598ddc41eb95bbc9c44968f79` | `f36c095de1746da11ba50dc0296cae9b238c3a11ad1b40e144c4dc0212267b13` | 41585 |
| `BookOfEternityClient/Services/PendingTurnSnapshotReader.cs` | `b862b7927156ada3783a0cc42e682468fe80bd41` | `11a6fe03846c74145329ad557917f8bf1bad693d1f40e3654efe0577404a5b6c` | 43996 |
| `BookOfEternityClient/Services/ProgressionScheduleService.MortalJointBasis.cs` | `584d5aed93072d4b523462eeb68fab5af2c212ef` | `8da63948ff5d40086892e5dd6f3cfcd33bbbfba9c0e5151855f9df11ba8048aa` | 15932 |
| `BookOfEternityClient/Services/ProgressionScheduleService.MortalJointInputs.cs` | `a8e52a1605153c3d6b46ed6517bcc62bed161ad4` | `893039cbe630faa690eba3273e0a495f525a9807534bea10987bb2c18f560cc9` | 11393 |
| `BookOfEternityClient/Services/ProgressionScheduleService.cs` | `c22b693b973683ca6ccb890fae62a4786a3a87a3` | `d0cb8cbf0b6b67f7ca8f351847817fa2d5ad6e98712c8a39ecbbd18ebb7b88ff` | 93122 |
| `BookOfEternityClient/Services/QteDeferredEffectContinuation.cs` | `d0adeaf89a575dec982f9887d9b1fe12f0965416` | `f2b9b47d454f3d8fc7811404de62e641063dc00f22df782fa735268d4b0cfd85` | 49097 |
| `BookOfEternityClient/Services/QteSceneService.cs` | `d8c7f103af0540cdf97f7ce3d2cb3641b60fca08` | `c7c672bf7fbc6bbe47d15415d29a4b878502542bfcede000aa56f36318176836` | 264036 |
| `BookOfEternityClient/Services/ResourceMaterializationContract.cs` | `0b371748bc9b22052dbdc09a852fa412b13771c4` | `53b3bd3a975636808d0c1a7c099c784e2d155cf6d501a9db24cf1f8d73ad9c7a` | 21516 |
| `BookOfEternityClient/Services/ResourcePendingResolutionState.cs` | `e45849026a827d55f2f6ab9810e575e6279c5a14` | `35c7a124d7022d2c9839eb1e5c50d783b772aa2bdf4fe18ec72dc09816caf204` | 127681 |
| `BookOfEternityClient/Services/ShiningAbodeState.cs` | `c4c1eb9216d6970e64eed48ae1c96ee14a1fd0cd` | `940ed36904be677527cfb8d5fd714ba53f5a20ab7700b54407903907e6d19873` | 96769 |
| `BookOfEternityClient/Services/StorageTransportMoveService.cs` | `ea4a8b1194d8a7112b44fea122d815a00fec2ada` | `4159e270d5f5ecc1a7160729b5acf107d80f15c920d08ca3ba23e104ab0838e1` | 43814 |
| `BookOfEternityClient/Services/StoryService.MortalJointRepair.cs` | `1c3c29e4d43cfc47a82b495d72986e934b8e438c` | `ca4a34edb095b13d77ccfd5554b93b593cda2d428d27e3b92f40149c1f74340a` | 22909 |
| `BookOfEternityClient/Services/StoryService.MortalJointRowCalculation.cs` | `2777ec4c498835876b2fc7bacd7d18c572f74be0` | `bdeb4200ac73f113359ba81a2b2db8a56af21d886fe2954236793ec31bb7b0cf` | 4811 |
| `BookOfEternityClient/Services/StoryService.cs` | `ba6d79e87c0e00471cb71112980b980c404ebdef` | `08ffacbe1c85b27116aa1886b025c4a495c0cf6f198089f12505a66a566b3380` | 14974 |
| `BookOfEternityClient/Services/WoundAcceptedTurnSnapshotContract.cs` | `823f67f04f21021f9a08ee294585cb73169e80f2` | `288e2429bbdb3c79c540f5dc8539cc2cc9b3bfdb4551a02b94c2b443422af3a1` | 9227 |
| `BookOfEternityClient/Services/WoundCarrierCatalog.cs` | `451a1de12612d2b26800e40057fcc3072834189e` | `af9fd37ffd70d54178ca0b3452ce13b47213f2658ac4501d3844172dfe48f50d` | 44369 |
| `BookOfEternityClient/Services/WoundHistoryState.cs` | `44285fc44cc2e2ee39f28e51b23e809620a88d6f` | `7b7083e040ea9fe2338531b6f12e5a54837a1fa186282ed87bd1ec5527f3bea9` | 67254 |
| `BookOfEternityClient/Services/WoundIdentityState.cs` | `beb860a84e8dba429c125ab0e271ee68219ad6a6` | `6fcce26db18bea5f20fd51e127b674beeb4d7662ab72b6d90f0bf44c13907601` | 23240 |
| `docs/development-workflow.md` | `5b105f4f7c0e57212a71d5b110361accd71f7a6e` | `8cc1cb8f4673bf910b0cb3d9c0757b9fdc6facf06e6a2552a6b462c90b270e71` | 66432 |
| `native/linux/boe-lineage-supervisor.c` | `40b7a379056f3aa8cbd3df454f0c8848aa740b85` | `af579f12332914628f7fca4657775478d090b1aceccc2e7c3190c527a2074080` | 24566 |
| `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-death-handoff.md` | `c8d9637e009faebd230d9ecae5f8575532b18c1e` | `aca133abb2a50a5d5b44cfc0bad0631050e0ca6ff89cb46afa3eb390377a8216` | 71017 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-design-20261016.json` | `e077ba66e9332e70fa29ac351d33357d468de3aa` | `25cef182df647d50e403714562b5c159018ac9a352598213cacd4ce3e7f5bfad` | 84819 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-design-20261016.md` | `183c079de96d8939758371b900efaadd76470b04` | `ec43864fec0f3800980989366744e4ca5a275d4c10374c9644d1f85c6a97cdcd` | 69264 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-independent-review-20261016.json` | `16b1292df25efcff80f4b43014a287cf6bf1bf01` | `8a2910a4c8cf9478552f8dfaca88e42749d1d6fbad0e8540b5a62b3e52ee401e` | 45457 |
| `specs/1536-complete-wound-materialization/recovery/t197-depc-a4-targeted-independent-review-20261016.md` | `2775edb6e4352df4b956f74f4570e899a8405324` | `693d1987affa688ac284b60320e1b2ea48544b0981ac3b7a21c58d2f58ac4a34` | 19051 |
