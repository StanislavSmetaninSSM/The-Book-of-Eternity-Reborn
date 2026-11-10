# T179-Pd observed original reader — bounded contract, 2026-10-16

DESIGN ONLY. Assigned Astra/High; backend unknown. Inspected baseline 317bff96784e55952086e39e00153ae1118ca334; exact source/blob/SHA pins in JSON. Repository read-only. No build, discovery, tests, app/runtime, network, fetch/push or delegation. Only this new /tmp pair is written. Earlier consultation pairs remain immutable. All proposed tests are FUTURE/UNRUN.

## Chosen vertical

Keep the existing ReadCurrent(FileSystemManager, CanonicalWriteLease, IReadOnlyCollection<string>) surface, PendingTurnSnapshotReadResult fields, default read behavior and emitted original/v2/GM bytes unchanged.

Add a distinct ReadObservedCurrent with the same three actual-owner arguments. It shares the existing private validation core but uses a privately constructed, lease-explicit source session. Add one genuine FileSystemManager.ReadFileBytesSync(CanonicalWriteLease, string) leaf, using the existing sync storage dispatch and open/completion checks. Do not add a caller-provided reader, byte map, time source or imported source certificate.

Observed success is issued only after COMPLETE read-closure recheck, sticky fault validation, existing G binding and actual owner/lease admission. This barrier goes before the current PendingTurnSnapshotReadAuthority construction at PendingTurnSnapshotReader.cs:365, not in a later whole-admission consumer.

This is meaningful actual original-reader provenance. It is not submitted-output admission, S/CommonAuthority, accepted mechanics or M3 authority. The eventual actual GameEngine source owner consumes this exact evidence; do not build a second source parser later. No active path is enabled by this bounded extraction.

## Source facts that determine the API

- PendingTurnSnapshotReader.ReadCurrent :143–365 accepts a real lease but currently uses scalar manifest text (:161), authority text (:193), rollback callback (:209), selected original bytes (:344), and context presence/text (:716/:722).
- Existing order is selection validation → manifest/duplicate checks → global coverage checks → detached authority validation with rollback callbacks → original-presence validation → exact snapshot hash-mode check → ALL three lifecycle contexts → realm → selected path resolution → selected snapshot bytes → authority construction.
- Context sources :135–140 are repair request, input turn request, ready/turn_complete, in that order. The eager ToArray at :239–241 reads all three before choosing the first invalid result. Diagnostic-only repair context is not a usable sole context.
- PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority :482–521 delegates to the existing checker with requireRollbackArtifactPaths=false. It must stay false. The checker first validates structure/hash/envelope, then BuildAuthorityPayload (:590–618).
- The callback is invoked by CopyNormalizedFileHashes :855–889 for ALL rollback backup entries, not for selected snapshot bytes. Snapshot hashes are copied as metadata into the payload; selected originals are physically read later by the reader.
- Existing rollback normalization is CopyNormalizedFilesDictionary :807–822 and NormalizePath :953; payload equality has its existing case rules. Do not silently impose a new rollback-directory/.rollback restriction or new casing policy on this read-only API.
- FileSystemManager already has explicit sync text :1517–1526 and explicit presence :1777–1782, but no explicit sync byte overload. Explicit async bytes :1417–1430 has the correct trusted-local versus legacy dispatch.
- UsesTrustedLocalWriter (FileSystemManager.TrustedLocalStorage.cs:92–109) respects mutation recorder, legacy recovery and browser artifact admission. ReadOriginalFileBytesAsync/OriginalFileExists (OrdinaryReads.cs:32–43) explicitly own the browser legacy physical route; do not globally replace them.
- EnsureValidCanonicalWriteLease (FileSystemManager.cs:4092–4098) checks actual owner/activity/main admission/worker purpose and context. ReadLocalGenerationSnapshot reads actual existing generation without creation, below its proper recursion boundary (GenerationSnapshot.cs:12–21).
- The existing authority object has an internal DTO-like constructor (:944 onward). This task must not treat arbitrary instances of that old object as evidence. The NEW observed evidence uses reader-private issuance and retains exact issued-instance identity. No project-wide authority-constructor redesign is necessary for this bounded step.

## Exact proposed types and construction

Proposed names, not existing implementations:

1. FileSystemManager leaf:
internal byte[]? ReadFileBytesSync(CanonicalWriteLease lease, string relativePath)

Implementation contract:
- EnsureValidCanonicalWriteLease before dispatch;
- choose UsesTrustedLocalWriter(lease,path) ? ReadOrdinaryFileSnapshotCore(path,lease) : ReadFileSnapshotCore(path);
- run the existing lease validation again after return, including missing result, before returning Content;
- do not call scalar ReadFileBytesSync, async .GetAwaiter().GetResult, Acquire*, RecoverPendingFilePublicationsBeforeCanonicalRead, GetOrCreateSessionGeneration, or nullable-data-failure/world adapters.
- preserve the underlying ordinary/legacy open checks, retries, sync post-open hook order and regular-file/path semantics. Do not introduce new hooks or route every original through the trusted-local reader.
- bytes are detached by the source session before retaining or returning them. This leaf is storage data, not provenance authority.

For exact text decoding, factor the existing DecodeFileSnapshot implementation into a shared pure byte-decoding helper used by that existing function and the observed reader. Preserve StreamReader UTF8 with BOM detection and existing fallback semantics; do not replace it with strict UTF8 or TrimStart(BOM). No extra file read to obtain text.

2. PendingTurnSnapshotReader:
internal static PendingTurnSnapshotObservedReadResult ReadObservedCurrent(
    FileSystemManager files,
    FileSystemManager.CanonicalWriteLease lease,
    IReadOnlyCollection<string> requiredLogicalPaths)

private ReadCurrentCore(...) with a private closed mode Existing / Observed, or two private concrete adapters constructed ONLY inside this static owner. Existing public/internal ReadCurrent calls Existing. No externally implementable source interface and no overload accepting Func/read map/evidence.

internal sealed class PendingTurnSnapshotObservedReadResult
{
    internal bool Success;
    internal PendingTurnSnapshotReadAuthority? Snapshot;
    internal OriginalReadEvidence? Evidence;
    internal IReadOnlyList<ValidationIssue> Issues;
}
Construction private/internal only by the actual reader. Successful result requires BOTH freshly issued Snapshot and Evidence; failure has both null. Clone returned issue collections/data.

private sealed class OriginalSourceReadSession
- constructor private to reader, receives only actual files and active lease;
- stores exact files/lease reference, immutable existing G bytes/id, finite operation log, first observations, sticky fault/drift state, and capture state;
- never accepts a caller path dictionary, byte array or callback; no public methods for registering arbitrary coordinates;
- closed methods: ReadManifestText, ReadDetachedAuthorityText, ReadRollbackBytesFromAuthorityCallback, ProbeCurrentContext, ReadCurrentContextText, ReadSelectedOriginalBytes;
- safe coordinates are derived internally as described below;
- states Capturing → Rechecking → Issued or Failed; no read/append after Issued, no revive after Failed.

internal sealed class OriginalReadEvidence, nested in reader with actual private-issuer construction:
- construction is controlled by the reader's private issuer object, checked by the constructor before accepting any arguments; no factory accepts parsed/imported evidence. Use the existing project's private-issuer pattern rather than relying on general internal constructor accessibility as protection.
- stores the exact files/lease identity and G snapshot, the exact issued Snapshot reference, immutable detached operation records, selected logical coverage/presence and manifest/authority digests;
- exposes detached diagnostic data/read-only role records, never an import constructor;
- RequireCurrent(files,lease,issuedSnapshot) checks actual object identities and live fences, then rechecks the same frozen source closure. It cannot attach another snapshot, original, lease or root.
- after its lease ends, source images remain diagnostic data only. A future fresh owner obtains fresh evidence rather than upgrading a parsed record into this object.

No new receipt/ID/time allocation. No serialized format or durable path in this step.

## Exact lifecycle and mint barrier

Observed entry first validates existing argument/path-selection rules in their existing order. Invalid selections return the same existing diagnostics without source reads. Then it validates the actual lease/no unresolved local decision and captures ReadLocalGenerationSnapshot. Missing existing G produces a NEW observed-only source-generation-unavailable diagnostic, no creation. Malformed generation/main/worker admission and session-replacement exceptions propagate through their existing storage/owner types. These are new observed-entry preconditions, not changes to default ReadCurrent.

The main sequence then remains exactly:
1. Manifest text read; record exact bytes/absence BEFORE decoding. Preserve existing duplicate/JSON failure behavior.
2. Existing global coverage checks.
3. Detached authority exact bytes/text.
4. Existing authority checker. Every requested rollback-byte callback is recorded at the actual callback callsite, in actual order; no speculative prereads.
5. Existing OriginalPathPresenceV1 validation, then ExactSnapshotHashMode check.
6. All three context probes/text reads in existing array order. Preserve separate exists probe then conditional text read; do not collapse to bytes!=null. Missing probe skips text as today; true probe plus missing/empty read is invalid as today.
7. Existing realm and selected-path resolution.
8. Each selected original exact-byte read and existing hash check. Continue collecting selected path issues in the same order as today. Preserve optional signed absence versus unobserved/missing coverage distinctions.
9. If any existing validation failed, return the same failure issues; do not issue Snapshot or Evidence. Physical exceptions propagate as specified below. No end recheck is needed for an already failed result.
10. Otherwise, before constructing ANY success authority, invoke session.CloseAndRecheck. It checks sticky faults, actual owner/lease/main/worker admission, exact G bytes/id and no unresolved local decision. Replay every observed primitive in operation order against actual storage using the same explicit dispatch. Compare exact bytes/absence/presence, not just semantic JSON or digest. Verify G and lease again at the end.
11. Only if all checks pass create PendingTurnSnapshotReadAuthority from the already validated detached bytes, then privately issue OriginalReadEvidence bound to that exact object. Return success. No await, mutable callback or external work between the barrier and issuance.

CloseAndRecheck is not a second authority parse/hash validation or policy pass. It repeats the finite exact IO observations to ensure that all facts used by the first pass still hold. Later S→C→B boundaries call Evidence.RequireCurrent; they cannot replace this BEFORE-ISSUANCE barrier.

Default ReadCurrent shares validation logic but retains its existing source adapter and does not gain new G checks/end-rechecks/errors. No ordinary reader's result/diagnostic contract is silently strengthened by this extraction. Moving default callers to observed semantics, if later desired, is a separate explicit affected change.

## Coordinates, roles and callback confinement

Closed operation roles:
- Manifest: fixed pending_turn_snapshot path, byte observation decoded as existing text.
- DetachedAuthority: fixed authority path, same.
- RollbackArtifact: physical coordinate requested by existing authority checker; corresponding normalized manifest logical/physical relationship retained.
- ContextPresence and ContextText: only the three fixed CurrentContextSources, retaining IsRepairRequest and actual status.
- SelectedOriginal: logical coordinate in the internally validated selection, exact physical coordinate from the actual parsed manifest; current existing safe path + pending snapshot directory checks run before read.
- GenerationFence: actual generation owner snapshot, not arbitrary relative path.

The private rollback callback receives the existing checker’s string, but never passes it to unbounded FS access:
- derive allowed normalized logical/physical rows from THAT invocation’s parsed manifest, using the same normalization routine as BuildAuthorityPayload. Factor that routine narrowly if necessary rather than duplicate normalization rules.
- instantiate this derived mapping only for the current private session; no caller imports.
- accept only coordinates the actual rollback hashing iteration requests and that correspond to its normalized map. Existing HasUsableManifestStructure ran before this callback; retain its safe-relative-path validation.
- retain each logical alias association in diagnostic evidence; if two logical rows point to one physical artifact, preserve the existing policy and all associations/reads. Do not invent rejection of valid duplicates or sort away existing call order.
- a callback invocation outside this closed derived set is an internal unsupported-source fault, never null/absence and never new authority.
- content remains untrusted until the full detached payload comparison succeeds. Recording its read does NOT authenticate it early.

For a cleaner ownership boundary, the authority module may expose an internal DATA helper to derive its normalized rollback coordinates from the existing manifest map, called only by this reader’s private callback. This helper grants no authority and is not a new read-injection API. Keep existing generic authority-checker signatures/behavior unchanged; its legacy callable byte callback is not exposed through the new observed reader.

ClientOwnedValidationHashes and unselected Files/Hash entries are authenticated manifest metadata, not physically read current roots. Record them as declarations only; do not fabricate physical observations or expand the callback to read them. Signed original absence is proved by existing closed presence/manifest rules; it is not a current file absence probe.

This narrow reader uses no Directory enumeration/FileInfo policy. Internal byte-read mtimes are incidental to existing storage snapshots and are not inputs to PendingTurnSnapshotReader decisions. Do not add mtime-equality guards or claim validation-clock provenance here. Those belong to the separate output-admission source session.

## Repeated reads and failure latch

During Capturing, every existing read occurrence remains a physical read; do not memoize away callbacks or context operations. Retain cloned first values plus each operation’s role/ordinal. If repeated reads of the same actual byte coordinate return different bytes/absence, latch source drift even if the final value later returns to its first value. A presence probe and a later byte read are distinct operations; true→missing must not become valid absence.

The end recheck repeats all recorded operations, including repeated occurrences and all absent context probes, in deterministic order. No caller can supply replacement observations. On the first mismatch fail with a new observed-only pending_turn_snapshot_reader_source_changed issue naming the logical/source role and ordinal; Snapshot/Evidence remain null. The exact same physical bytes with incidental changed mtime do not fail this reader’s byte-only contract.

Fault rules:
- log a sticky physical read fault before rethrowing the original exception; never convert IO/UnauthorizedAccess/InvalidData/worker/main/session/cancellation faults to null.
- existing manifest JsonException handling still produces manifest-invalid; existing authority catch of InvalidOperationException still produces rollback_backup_unreadable/authority-invalid at its existing position. The sticky fault guarantees no success if a future internal catch ever suppresses a read failure.
- original returned failures retain the same issue ordering; do not prepend a new observation issue to an already failed parse/authority/context result.
- if an otherwise-successful core reaches the barrier with a latched unsupported/fault state, fail observed-only source-unavailable; never issue then revoke.
- no blanket catch of SessionReplacedException/GmWorkerSessionReplacedException, coordinated storage uncertainty or cancellation into ordinary validation issues. Let their actual owning caller retain existing disposition.
- exact generation changed between capture and issuance fails observed-only source-generation-changed if the existing owner fence has not already thrown. Never GetOrCreate or rotate G.
- stale/disposed/foreign lease fails actual existing EnsureCanonicalWriteLeaseActive before IO. A same-directory second FileSystemManager is not the same owner for evidence reuse.

This is source integrity under the existing trusted-local contract, not protection against unsupported concurrent hostile edit/ABA between invisible observations.

## Why sync is smaller than async precapture

Chosen sync leaf reuses the actual ReadFileSnapshotCore/ReadOrdinaryFileSnapshotCore dispatch already used by explicit sync text. It avoids nested lease acquisition and preserves the existing authority checker’s synchronous callback and short-circuit/error order.

An async design can be sound only if it separates authority structure/envelope validation from rollback materialization, derives the finite coordinate list at that exact point, asynchronously captures each requested artifact in existing order under the same lease, then runs a pure authority comparison against private captured observations and rechecks before issue. That needs a new validated pending-check stage or suspended checker contract. Eagerly reading all manifest/rollback/snapshot paths before current authority validation changes error order, read scope and storage admission; using synchronous blocking on async under this lease adds avoidable risk. A public dictionary-backed callback also turns caller data into apparent original authority. None is needed for this bounded step.

The existing async physical APIs and ordinary browser legacy readers remain untouched.

## One causal TDD vertical and affected consumers

First positive fixture: PendingTurnSnapshotReaderTests.PrepareAsync (:821–830) uses the real LiveTurnPreparationService. Existing ReadCurrent_ReturnsTheExactSignedPreTurnBytesNotTheChangedLiveRoot (:127–150) is the minimal original/current distinction. New observed test calls the real ReadObservedCurrent with actual _fs/lease and requires private evidence + exact original bytes, mutation isolation and no tree writes. Do not use the reflection-based manifest-rewrite test as the primary positive proof.

Causal barrier RED: prepare that genuine original, use existing storage read hooks to mutate an already-read context or original snapshot after its first successful validation but before completion (e.g. at a later selected-original post-open boundary). All old checks would otherwise pass from their captured bytes; the new reader must return source_changed with Snapshot/Evidence null. Setup must first prove the unchanged control succeeds. No fabricated issuer, reflection constructor, dummy context or production test hook.

Rollback/browser companion: GameEngineTurnLifecycleTests.BrowserOriginalAdmission_AllManifestReadersRetainActualSignedCondition (:160–197) uses real PrepareBrowserInputStagingAsync. Its definition is BrowserInputRecovery.cs:468 onward; use withRollback:true to exercise actual emitted rollback coordinates, existing browser main condition and explicit dispatch. Test the new reader through this genuine original owner, not a hand-built authority envelope.

Finite negatives:
- source drift of manifest, authority, selected snapshot, each context presence/text and actual rollback artifact after its read but before mint;
- absent context becomes present, present disappears; repeated callback yields changed bytes then restored value;
- foreign/disposed lease and second genuine FileSystemManager; existing G missing/replaced/changed bytes; preserve actual main/worker admission errors;
- callback missing artifact and physical file-type/path rejection; source fault never absence; unsupported callback cannot mint;
- all-three-context order, malformed/diagnostic-only/conflicting/stale contexts; selected byte/hash mismatch, raw duplicate/case-alias coverage, exact hash-mode rejection and signed optional absence;
- actual browser original/rollback route, ordinary trusted-local route and retained legacy physical dispatch; no nested lease when ambient execution context does not flow;
- default ReadCurrent API reflection guard and existing result fields/diagnostics unchanged; no source-schema/v2/GM-byte changes;
- mutated exported evidence/data cannot mutate retained inputs; old Snapshot alone cannot substitute for observed evidence; lease end invalidates reuse.

Catalog owners found by read-only inspection:
- pending-turn-authority (existing reader/presence/provenance contracts);
- browser-pending-snapshot-roundtrip (actual engine/browser original and manifest readers);
- portable-signed-snapshot-authority-neighbors (bounded affected authority/rollback byte consumers);
- portable-ordinary-readers and portable-ordinary-read-consumers (the new explicit sync leaf’s actual storage contracts);
- portable-ordinary-read-windows only for actual Windows-specific physical coverage, never a Linux pass claim.

Do not automatically run all these categories or the entire graph. Parent should select exact affected existing cases and give the new coherent observed-reader cases a finite documented category; actual discovery/PlanOnly/source/fixture/selection/evidence gates remain future work. No tests were run by this consultation.

## Boundary completion and remaining gates

This closes an implementable narrow API contract: actual original owner/FS/lease → exact observed closure → complete pre-issuance recheck → private source evidence. There is no remaining architectural need for a general source overlay or async recapture layer.

The bounded stage must include actual reuse of existing validation logic, the explicit FS leaf, private observed reader, source-order/failure/recheck controls and real producer fixtures. It may precede the main active engine admission implementation, but is not a terminal standalone-helper delivery: the planned GameEngine submitted-source owner must later accept this exact evidence object and revalidate its actual lease/instance at S capture. Existing v2 consumers remain on ReadCurrent until an explicitly scoped integration changes them.

Independent review still must confirm concrete code preserves storage dispatch, error order, closed callback coordinates and pre-issue barrier. The complete 73-leaf output source session, Full reasoning/comparison semantics, admission history/M3 closed codec, C/B integration, cold semantic proof and pre-P0 full recovery remain separate open prerequisites. No active admission/M3 issuance, new durable path, new QTE, player activation or full F1/T179/T192/T197/D4/guided/visual/Windows/D8 qualification follows.

FINALSTOP.
