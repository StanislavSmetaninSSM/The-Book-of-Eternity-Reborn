# Ordinary Portable Load Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: use `superpowers:executing-plans` for the preserved native execution method. The local parent owns implementation, TDD and integration; a separate `gpt-6.1-sol` / `xhigh` agent independently reviews each completed coherent block. Steps use checkboxes. Do not replace this plan with a second Superpowers plan.

**Goal:** Complete the internal ordinary-load filesystem module of [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553), including exact replacement, topology, recovery and native Windows/Linux qualification, for downstream client integration.

**Architecture:** Reuse lifecycle/replacement leases and the existing single B1 intent/commit/recovery decision. Stream v2 metadata without a whole encoded header; introduce an explicitly versioned v3 namespace frame for directories and protected boundaries within the same journal authority. Keep the existing public load callers unchanged until the downstream T032-B4 cutover.

**Tech stack:** Existing C#/.NET 8 application, .NET SDK 10 compiler, PowerShell 7 category runner, xUnit, owned child-process probes and GitHub Actions; no new production package or remote gameplay dependency.

**Spec:** [spec.md, Local load-filesystem continuation revision 1](spec.md#local-load-filesystem-continuation--revision-1-2026-10-03), approved by the owner. [tasks.md](tasks.md) tracks work; [plan.md](plan.md) holds evidence/checkpoints. The owner's subsequent explicit spec/plan/revision review waiver authorizes this execution plan revision 1 and its v3 design.

**Design review:** independent Sol 6.1 XHigh cleared the eight-document block
`6dd0dbeb..8ca3fbca` and related implementation source with no actionable finding.
This clears the design only. The owner subsequently authorized autonomous recommended
spec/plan/revision decisions and waived further written review. Implementation,
runtime/resource/native qualification remain pending.

**Source:** `codex/1553-load-filesystem`, based on published `5d2aa2ceadd8f4424e3ccaf0249a8bb164f32fae`. The nine entry GREEN cases are a first slice; five review fixes and the saved Shining Abode fixture correction are not yet verified. Ordinary-save acceptance at `ddaade72ae44936f6cf61970afb2bc80225b7731` and earlier T032-B0 design acceptance at `05105d58` retain their historical evidence.

## Global constraints

- Preserve current archive ceilings: 8,192 entries, 2 MiB total UTF-8 entry names, 4 MiB manifest, 8 MiB Soul, 64 MiB per expanded payload, 512 MiB total expanded payload, compression ratio 200 and the existing 1 MiB compression-check grace.
- Original archive/schema/manifest/hash validation precedes the finite fixed-path mapping. Reject ordinal Trim aliases; preserve arbitrary payload names/bytes and native Linux case distinctions.
- One complete replacement plus generation decision; no partitioned transactions, second journal, legacy evidence bypass or unjournaled destructive tree pruning.
- Preserve the entire canonical save library, exact selected source, history, choices, settings-byte/absence authority and generation/writer fences. Resolve original evidence with its original handler or block.
- No runtime mutation before confirmed commit. Never rewrite a committed result as rollback; Uncertain establishes no generation and blocks continuation.
- Process-crash recovery only; no additional directory-rename power-loss promise or protection against concurrent owner editing is introduced.
- Category-only verification, isolated mutable roots, owned process cleanup, no Fast/PreMerge/full suite or sweep of all categories. Changed shared mechanisms select their actual affected consumers.
- Native Windows and Linux evidence must exercise the behavior; OS early returns and parser/helper-only tests do not prove native load.
- Public console/browser B4, live GM T033, whole B5 and #1553 closure remain downstream. No GM-authored game field/mechanic changes here.
- Publish source/checkpoint before lengthy tests/review, verify the remote full SHA, and restore final source/evidence from GitHub. A WIP push is not acceptance.

## Review focus

| Failure mode | Required proof and owner |
| --- | --- |
| Original-valid archive spelling is interpreted differently by the resolver or fixed consumers | Trim rejection and canonical fixed-path positives reach actual typed load; selected-source collision rejects after mapping. T032-B1-FIX. |
| Recovery/admission or late cleanup fabricates a safe retry or established generation | Real acquisition, pending/commit conflict and refresh/release cuts preserve both causes and the exact disposition. T032-B1-FIX and B3-COLD. |
| Legal many-member metadata or late corrupt descriptor bypasses complete preflight | Streaming flush/buffer observations, strict reordered/duplicate schema and exact v1/v2 recovery retain all earlier state on late corruption. T032-B1-METADATA. |
| A conversion removes unknown empty directories, protected source/library or scratch evidence | Both topology directions, unknown children and blocked descendants preflight before any earlier restore; stable anchors and repeated rollback recover exact namespaces. T032-B2-NAMESPACE and B3-COLD. |
| Helper success hides large-input/native/cold resource failure | Real current-produced archives and typed load in owned processes measure each phase and both OS; guard stops/noops remain non-passes. T032-B3-RESOURCE and B5-FS. |

## Files and interfaces

All code paths below are repository-relative, including the project directory.

| Files | Responsibility |
| --- | --- |
| `BookOfEternityClient/Services/SaveLoadService.Loading.cs`, `SaveLoadService.cs` | Original-valid preparation, finite fixed-path mapping, typed truth, private scratch ownership; preserve old public load. |
| `BookOfEternityClient/Core/FileSystemManager.cs`, `FileSystemManager.LoadReplacement.cs` | Replacement-acquisition recovery classification, complete namespace capture, generation member, registrations/revision and load adapter. |
| `BookOfEternityClient/Core/TrustedLocalFilePublication.Journal.cs`; new `TrustedLocalFilePublication.FrameMetadata.cs` | v1/v2/v3 dispatch and incremental metadata schema/encoding. v1 semantics remain unchanged. |
| `BookOfEternityClient/Core/TrustedLocalFilePublication.cs`, `TrustedLocalFilePublication.Images.cs`; new `TrustedLocalFilePublication.Namespace.cs` | Same B1 authority and established decisions, namespace trie/preflight/reconciliation, generation-last ordering and cleanup. |
| `BookOfEternityClient/Core/TrustedLocalFileScope.cs`; new `TrustedLocalNamespacePlan.cs` | Narrow lexical/non-following namespace observation plus immutable namespace descriptors; existing strict file APIs remain strict. |
| Existing `BookOfEternityClient.IntegrationTests/PortableLoadReplacementTests.cs`, `.ReviewRegression.cs`; new `.Topology.cs` | Connected admission/outcome/topology load assertions with independent roots. |
| New `BookOfEternityClient.Tests/TrustedLocalFrameMetadataTests.cs`, `TrustedLocalNamespacePublicationTests.cs`, `PortableLoadColdRecoveryTests.cs`, `PortableLoadResourceTests.cs` | Codec, namespace decision, real cold load and phase-specific envelope qualification. |
| New `BookOfEternityClient.TestSupport/PortableLoadColdHost.cs`, `PortableLoadResourceProbe.cs` | Extend existing owned host dispatch; actual typed load, deterministic cut announcements and measurements, no substituted helper success. |
| `tests/categories.json`, `tests/selection.json`; new `tests/selections/1553-load-linux.json`, `.github/workflows/portable-load-linux.yml` | Documented affected selections and bounded native Linux execution/evidence. |
| Existing feature spec/plan/tasks, `recovery/README.md`, `quickstart.md` | Current status, reproducible commands, downstream callable boundary and evidence. |

Preserve existing callable interfaces:
- `Task<LoadReplacementResult> SaveLoadService.LoadGameWithOutcomeAsync(string saveFilePath, CancellationToken cancellationToken = default)`.
- `Task<PreparedLoadArchive> SaveLoadService.PrepareLoadArchiveAsync(string saveFilePath, CancellationToken cancellationToken = default)`.
- `LoadReplacementResult(LoadReplacementDisposition Disposition, string? SelectedSourcePath, string? EstablishedGeneration, bool NeedsFollowUp, Exception? Failure, bool ContinuationBlocked = false)`; `WithFollowUp` retains disposition/source/generation.
- `Task<CanonicalWriteLease> FileSystemManager.AcquireSessionReplacementWriteLeaseAsync(SessionLifecycleLease lifecycleLease, CancellationToken cancellationToken = default)`; these existing nested lease types retain their authority.
- Existing `PublishLoadReplacementImagesAsync` is used by the five-fix block, then replaced internally by the namespace adapter below. Public callers stay original.

New internal namespace interfaces, defined by T032-B2-NAMESPACE:
- `CanonicalLoadNamespaceSnapshot CaptureLoadReplacementNamespace(CanonicalWriteLease lease, string selectedSource)`.
- `Task<TrustedLocalPublicationOutcome> PublishLoadReplacementNamespaceAsync(CanonicalWriteLease lease, LocalSessionGenerationSnapshot expectedGeneration, CanonicalLoadNamespacePlan plan, string replacementGeneration, Action validatePreparedNamespace, CancellationToken cancellationToken = default)`.
- `TrustedLocalPublicationOutcome TrustedLocalFilePublication.PublishNamespaceWithOutcome(CanonicalWriteLease lease, TrustedLocalGeneration generation, TrustedLocalNamespacePlan plan, Action<TrustedLocalPublicationPhase, int>? observer = null)`.
- `TrustedLocalNamespaceKind { Missing, Directory, File }`; `TrustedLocalNamespaceImage(TrustedLocalNamespaceKind Kind, TrustedLocalFileImage? FileImage)`; `TrustedLocalNamespaceChange(string Path, TrustedLocalNamespaceImage Before, TrustedLocalNamespaceImage After)`.
- `TrustedLocalNamespaceBoundary(string Path, TrustedLocalNamespaceKind Kind, long Length, string? Sha256)`; `TrustedLocalNamespacePlan(string RootPath, IReadOnlyList<TrustedLocalNamespaceChange> Changes, IReadOnlyList<TrustedLocalNamespaceBoundary> Boundaries)`.
- Canonical snapshot/plan use session-relative keys/changes plus exact absolute protected boundaries; manager resolves them and appends the sole exact runtime-generation file. Their types are defined alongside the namespace descriptors, not in client DTOs.
- Narrow `LoadPreparationCleanupException` retains preparation and cleanup causes plus owned staging root. It is not canonical uncertainty by itself.

### Fixed-path inventory clarification (T032-B1-FIX)

The source audit found that the eight handoff paths are insufficient even before
public B4 cutover: committed internal load immediately refreshes fixed player,
world and history paths. Use one immutable whole-file lookup composed from
`FileMapping.FieldToFile`/`OutputFiles`, normalizer rollback paths, afterlife
surface declarations and QTE rollback paths. Explicit supplements are config,
chat history, soul/resources/profiles, active world directives, difficulty,
pending dice and resource resolution constants. Repeated identical declarations
are harmless; contradictory case spellings fail. No reflection, global directory
folding, gameplay change or import permission follows from this registry.
Original archive/schema/manifest/hash validation and ephemeral exclusions retain
authority. Original outer whitespace is checked before platform resolution,
because Windows `GetFullPath` can erase trailing spaces. These implement existing
LOAD-FS-001/004, authorized under the owner's review waiver.

Four real-producer manifested preparation cases cover player status, world time,
codex and directives plus unchanged arbitrary `lore/MyCustom.JSON` bytes/key. At
`9a54dd23`, Windows reached preparation and rejected its mismatched complete
staging inventory before the ordinal assertion; all four were causal RED. The
existing trailing-config case also remained RED because resolution had erased
the space. This is not native Linux acceptance. Select the nine exact affected
lease/recovery consumers separately from broad storage/bootstrap categories.

## Streamed metadata design

Keep the already reviewed T032-B0 v2 encoding/order/region design below. Reuse the incremental machinery for v3; do not replace strictness with a permissive general JSON DOM.

### Streaming metadata algorithm for T032-B1

Source anchors: `TrustedLocalFilePublication.Journal.cs:17–63` writes the frame, `:65–110` dispatches/reads it, `:152–186` binds payload regions, and `:192–211` declares metadata fields. `TrustedLocalFilePublication.cs:35–38,295–332,369–376` and `StrictJsonAuthority.cs:9–57` define strict JSON, path, generation and decoded duplicate semantics. Keep these semantics; only the v2 metadata transport/representation changes.

1. **Writer and length slot.** Leave v1 serialization unchanged. For v2, write the exact eight-byte `BOELP2\r\n` magic and an eight-byte zero length slot. Use `Utf8JsonWriter` over the private output stream with the existing serializer's naming/escaping/number/null semantics; emit the existing header fields and iterate existing `Journal.Members` directly. Write each Before/After descriptor with a checked cumulative payload offset, including all required null fields for absence. Do not build `FrameHeader` plus a second `FrameMember[]` or serialize the whole header to bytes. Keep one `Utf8JsonWriter` and its JSON state for the whole header, but call `Flush()` after the fixed header/array opening and after **every completed member**; stream-backed writers otherwise retain all encoded metadata internally until flushed. Flush the closing array/object tail, then dispose the writer without closing output and compute checked metadata length as position minus 16, patch the little-endian signed Int64 slot at offset 8, return to payload start, then stream images in the unchanged member/Before/After order with existing hash-checked 64 KiB copying. Verify exact final position, flush to disk and close before publishing the private stage; bind returned images to this closed journal. The staged/active/commit rename authority matrix and region rebinding remain unchanged.
2. **Reader bounds and buffering.** Keep the existing magic/v1-prefix dispatch, including reordered/escaped v1 fields and long leading whitespace. For v2, read the complete 16-byte prefix and require `0 < metadataLength <= actualFileLength - 16`, using checked/subtractive arithmetic before any metadata-sized allocation. A length-limited read wrapper exposes exactly that metadata region and never supplies payload bytes to the JSON parser. Drive `Utf8JsonReader` with `JsonReaderState` over a reusable 64 KiB buffer; discard consumed bytes and carry only an incomplete token across reads. If one supported token exceeds the buffer, grow only that token's carry buffer from bytes actually read, checked against the remaining physical metadata region; never allocate from the declared whole-header length or keep prior tokens. Release oversized carry storage after decoding its token. Working storage is bounded by the I/O buffer plus the largest actual token, not total metadata size; decoded paths/descriptors remain the unavoidable complete-member inventory. Fixed-domain property names/IDs/hashes use their existing semantic validation, and unsupported values fail safely. Do not add an arbitrary path/header cap to make buffering constant. Resource qualification must measure both >1 MiB many-member headers and long escaped tokens; a controlled resource failure is a non-pass, with evidence retained and no recovery mutation.
3. **Schema state machine.** Parse only the existing root, member, image and generation shapes. Track required/seen fields per current object with bitsets keyed by the fully decoded property name, ordinal case-sensitive as existing `JsonOptions`; escaped duplicate spellings therefore collide. Reject unknown properties, duplicates at every level, missing fields, wrong scalar/container/null types, invalid numeric ranges, comments/trailing commas and extra root values. Use the existing default depth/UTF-8/escape behavior. Properties may arrive in any supported order, including `Members` before `Format`/generation headers and `After` before `Before`; only the array's member order fixes region order. Generation `{Exists, Id}` and all root/image/member required fields remain exact; nullable image hash/offset and generation ID must still satisfy their existing presence semantics. Finish the complete root plus permitted trailing JSON whitespace within the metadata region before acceptance.
4. **Descriptor ownership and complete validation.** Since payload start is already `16 + metadataLength`, turn each completed member into path plus before/after file-region descriptors without retaining a metadata DOM or header graph. Own one accumulated member collection, finalize the existing journal member array, and release temporary parsing state; only a brief reference-array conversion is needed, not duplicate image/path graphs. In array order validate Before then After offsets regardless of property order: absent means length 0/null hash/null offset; present offsets exactly equal the running offset, lengths are nonnegative and contained, arithmetic is checked, and final coverage equals all remaining file bytes with no gaps/overlap/trailer. Apply unchanged `ValidateJournal` to the complete set: normalized unique paths, reserved/scratch conflicts, exact region hashes and declared generation images/binding all pass before any recovery mutation. No stream escapes read/write scope; region descriptors reopen only the owned journal, so cold recovery needs no extracted source.
5. **Narrow causal and compatibility proof.** Add a causal writer test with an observing destination: after each completed member flush, destination bytes must advance before all members have been emitted and `BytesPending` must be zero. Compare different member counts with the same largest member/token, recording pre-flush pending bytes; peak pending encoding must depend on that largest member/token, not the total member count. This is an explicit bounded-encoding claim, not constant memory for arbitrarily large paths/tokens. Verify the final patched length and exact parser-readable frame. Add v2 round-trip/recovery cases for metadata just beyond 1 MiB, the legal incoming/name envelope plus more disjoint old deletion members, and tokens/UTF-8 escapes split around the 64 KiB boundary. Cover reordered root/nested fields, escaped duplicate root/member/image/generation fields, each required-field omission/unknown/wrong-type/null violation, extra roots, truncation at prefix/token/region boundaries, huge declared length in a small file, overflow/overlap/gap/trailing region and wrong hash. Each corrupt late member must retain every earlier member and the original evidence. Retain independent ≤1 MiB v2 pending/committed bytes and the two source-derived original-v1 fixtures as compatibility oracles; assert exact bytes/absence/generation/library under normal acquisition and actual intent/commit cuts, not merely parser success. Existing affected owners are `portable-storage-stream-publication`, `portable-storage-stream-dispatch`, `portable-storage-original-v1-fixtures` and generation cases from `portable-storage-stream-generation`; select/split their actually changed contracts with new narrow load metadata ownership, rather than repeat the full save acceptance. This retained algorithm is implemented by T032-B1-METADATA below; v3 extends its schema through T032-B2-NAMESPACE. Public-client acceptance remains downstream.


## Namespace frame design — execution revision 1

The concrete versioned format is presented for approval with this plan. [data-model.md](data-model.md) records exact descriptors/schema; there is one current design, not a second publication protocol.

1. Use magic `BOELP3\\r\\n` (the actual eight bytes contain CR/LF), signed Int64 little-endian metadata length and streamed JSON/payload regions. Keep `active.json`, `intent.tmp`, `commit.tmp`, the existing trusted-publication runtime root, transaction identity, generation binding and committed flag. v1/v2 readers and existing file-only publishers retain their formats. New load namespace publication uses v3 even when this particular input has no conversion.
2. Capture all live session files and directories, including empty directories, with non-following inspection. Build one unique path collection and trie; derive direct child inventory from it. The session root is immutable Directory. Apart from the sole exact generation member outside this root, all mutation paths are beneath it. Validate normalized uniqueness, ancestors, reserved paths, boundary/scratch collisions, metadata and every file image before intent or recovery mutation.
3. The canonical library is an explicit opaque Directory or Missing boundary with its admitted state unchanged. Never enumerate its contents, create it, delete it or stage scratch in it. An in-session selected archive is an immutable File boundary with length/hash, no payload duplication; cold recovery validates it before mutation. If it lies inside the opaque library, this sole nested exact read boundary is permitted without enumerating the library; it adds no mutation grant. An external selected source is revalidated during preparation/prepublication but grants no external cold access. Every existing covered ancestor of a protected boundary remains Directory→Directory; ancestors inside the opaque library are no-follow validated only for the exact source read. No member or scratch may replace/remove an ancestor or occupy a boundary. Unknown siblings outside the library are never hidden by opacity.
4. Preserve harmless old empty directories as Directory→Directory; declare incoming ancestor directories and missing required directories in the same intent. Required library descendants are handled only by the opaque library rule, not the old RequiredDirectories creation loop. Incoming files at/above genuinely required directories or protected ancestors reject. Files absent from incoming are declared deletions except exact retained config/source/library rules.
5. Add a narrow scope-confined lexical path validator and no-follow observation that walks ancestors first. A child behind a known allowed File ancestor is logical Missing only after that ancestor's bytes/kind have matched a declared admissible snapshot. Never treat arbitrary ENOENT/ENOTDIR, access errors, links, FIFO/special entries or unrecorded blockers as absence; existing ValidateFile/ValidateDirectory contracts remain strict.
6. Pending recovery admits each node only in its Before or After state; an additional Missing conversion gap is allowed only for File↔Directory nodes. Blocked descendants must themselves allow logical Missing. Enumerate every currently covered directory; every direct child must be a trie node, explicit boundary or exact transaction-owned scratch name. Unknown bytes, files, links or **empty directories** reject before changing any earlier member. Committed recovery requires the exact complete After namespace; rollback confirmation requires the exact complete Before namespace, including child inventories.
7. Reconcile the chosen target deterministically: delete known files whose target is Missing/Directory; remove directories whose target is Missing/File deepest-first by **nonrecursive empty deletion**; create only declared target directories shallow-first; stage and atomically publish target files; publish/restore generation last. File→File uses direct by-name overwrite. Recheck each local entry and emptiness before acting. No cursor is necessary: each interrupted state is a declared snapshot or allowed conversion gap.
8. Derive stage/undo names from the existing transaction identity and node index in the nearest ancestor that is Directory in both snapshots and never removed. Validate the stable anchor before intent; do not stage in convertible or protected subtrees. Ordinary file-only cases retain their immediate parent anchor. Generation scratch remains in its narrowly validated runtime parent. Incomplete bytes at an exact owned stage/undo name are cleanup evidence; unexpected kind/name is a conflict. Never use a copy/delete fallback for cross-filesystem rename failure.
9. Do not create live directories or member scratch before active intent publication. Only private load extraction and private journal staging precede the decision. Revalidate the complete target before writing/replacing committed evidence and again before cleanup after observer callbacks. Each active/commit frame independently owns all before/after payloads; cold recovery needs no extraction source. Keep active evidence until other owned cleanup succeeds. Cleanup failure preserves the established outcome.
10. Extend observer phases with `DirectoryRemoved`, `DirectoryCreated`, `RollbackDirectoryRemoved`, `RollbackDirectoryCreated`. Cut index is the stable namespace-node index. Existing file/generation/commit/cleanup cuts remain. Outcome recovery confirmation must be namespace-aware; a file-only match is insufficient to report RolledBack.

Implementation seam: keep the legacy v1/v2 serialized `Journal` shapes unchanged.
Use a separate in-memory namespace evidence type for v3 and dispatch its eight-byte
magic inside the existing single `Recover` entry before legacy decoding. Both types
use the same active/intent/commit paths and decision matrix; this is format dispatch,
not another journal or authority. Reuse the incremental token cursor and generation/
region primitives from B1 metadata. Do not append ignored namespace properties to
legacy serializer models, where recognizing a formerly unknown property could weaken
the preserved strict v1/v2 schema. The outcome attempt may hold either typed prepared
evidence; v3 rollback confirmation checks complete Before namespace and generation.
Implementation separation uses new `TrustedLocalFilePublication.NamespaceJournal.cs`
for the private v3 schema/codec and `FileSystemManager.LoadNamespace.cs` for complete
capture/plan/manager publication. The original file-only load adapter is retained;
the internal typed load uses v3. Observer indices are stable namespace-node indices,
so the existing later-member regression cuts the second actual file publication.

## Dependency-ordered execution

### T032-B1-FIX — five demonstrated defects and current entry baseline

**Files:** existing loading service, FileSystemManager acquisition, ReviewRegression tests, category/selection and checkpoint. **Consumes/produces:** existing typed load boundary; no namespace API yet.

- [ ] Run fresh PlanOnly for `portable-load-alias-admission,portable-load-outcomes`; rebuild the saved Shining Abode fixture. Run these owners as causal baseline. Preserve Linux's historical 13 causal failures plus one fixture failure separately; Windows case-insensitivity may make some positive aliases already pass. A setup failure is not RED for a load fix.
- [ ] In PrepareLoadArchiveAsync, reject original-validated normalized relative names whose ordinal Trim differs before classification/omission/collision/extraction. Map the eight fixed runtime paths from the handoff before destination collision/profile/settings work. Inspect other actual fixed consumers and document the finite table; arbitrary case-distinct names remain untouched.
- [ ] Wrap specifically RecoverTrustedLocalStorage failure during SessionReplacement acquisition as coordinated uncertainty; preserve it if write-lease disposal also fails. Cancellation, admission fences and lock competition retain their distinct meanings.
- [ ] Add the narrow preparation-plus-cleanup exception and map it to NotLoaded/follow-up with both causes and owned residue; private residue alone does not block canonical continuation.
- [ ] Map Committed to new generation, RolledBack to old, Uncertain to null and mandatory follow-up/block. Refresh only on commit; late refresh/log/release/cleanup failures retain the established decision.
- [ ] Preserve original archive-admission behavior with selected existing corrupt/raw-budget/manifest-hash/mandatory-resource/unknown-owner cases whose interpretation the mapping or new preparation can affect. Add original manifest/hash validation before mapping and directory-component alias positives to the narrow alias owner; the existing leaf-only examples are not the complete mapping proof. Reject both incoming ancestor orderings and fixed-path collisions before mutation.
- [ ] Run corrected aliases/outcomes and the actual `portable-load-entry` cohort (9 existing cases). Select the exact affected acquisition/generation consumer methods from the catalog, not every canonical-storage test. Publish evidence and inspect independent review before accepting this coherent fix block.

### T032-B1-METADATA — complete streamed v2 metadata

**Files:** Journal/FrameMetadata partials, new TrustedLocalFrameMetadataTests and affected original fixtures/dispatch/publication tests. **Consumes:** existing file-image Journal and framed regions. **Produces:** WriteJournal/ReadJournal using the retained algorithm; helpers shared by v3.

- [ ] Add causal tests `WriterFlushesEachMemberWithoutWholeHeaderRetention`, `LargeMetadataRoundTripsWithoutAggregateCap`, `StrictMetadataRejectsLateInvalidMemberBeforeRecoveryMutation`, `ReorderedAndEscapedFieldsKeepExistingMeaning`. Assertions include per-member output advance/zero BytesPending, >1 MiB metadata, physical-region confinement, exact bytes/absence/generation and unchanged earlier members on late corruption.
- [ ] Run the new narrow `portable-load-metadata` category RED; use the original immutable v1 and independent ≤1 MiB v2 bytes as compatibility oracles.
- [ ] Implement incremental schema/token carry, strict required/duplicate/unknown/type checks and checked contiguous payload offsets before mutation. Flush after every member. No new arbitrary total metadata/path limit; descriptor inventory is the one required logical collection.
- [ ] Run `portable-load-metadata` plus affected `portable-storage-stream-publication,portable-storage-stream-dispatch,portable-storage-original-v1-fixtures,portable-storage-stream-generation`. Inspect owner responsibilities and split unrelated scenarios rather than adding accepted save resource/client sweeps.
- [ ] Commit/push source and evidence, obtain independent review and record measured buffer/member-envelope limits before B2 depends on the codec.

### T032-B2-NAMESPACE — connected v3 topology and exact rollback

**Files:** NamespacePlan/Namespace publication/Scope/Journal/core/load adapter, Topology connected tests and new namespace publication tests. **Consumes:** streamed metadata, exact generation snapshots and corrected typed load. **Produces:** interfaces and v3 schema above.

- [ ] Add `CurrentProducerLoadConvertsFileAndDirectoryInOneDecision` for both directions, empty/nonempty/nested old directories, retained empty directories, selected source at session root and complete library/config/history invariants.
- [ ] Add `UnknownLateChildPreventsEveryEarlierRestore` with unknown file and empty directory, `ProtectedBoundaryAndAncestorsCannotBeConverted`, `InterruptedConversionUsesStableScratchAnchor`, `ConversionGapDoesNotAuthorizeUnknownBlocker`, `CommittedCleanupCannotRollbackNamespace`. Assert actual named intent/directory/file/generation/commit/rollback/cleanup cuts and evidence retention.
- [ ] Add `NativeNamespaceTypeAndLinkBoundariesPreserveOutsideBytes`: links in target/ancestor/source/library/scratch positions, wrong types and Linux special/FIFO entries reject without mutation; by-name file publication leaves an outside hardlink alias unchanged. Require real native bodies where supported, record unavailable platform mechanisms separately. Test an exact source inside the opaque library as well as session-root/external sources.
- [ ] Run new `portable-load-topology` and `portable-storage-namespace` RED, then implement the full v3 model/preflight/reconcile/cleanup in one B1 decision. Remove the load adapter's pre-intent RequiredDirectories creation. Keep strict old file APIs and old publisher behavior.
- [ ] Switch internal typed load to complete file/directory capture and PublishLoadReplacementNamespaceAsync, with manager-owned generation and full namespace revalidation after hooks. No public Load cutover.
- [ ] Run the two new owners plus affected load entry/alias/outcome and codec/version-dispatch consumers. Document links/wrong types, native Windows name semantics, Linux case-distinct names and cross-filesystem safe failure, without claiming missing OS evidence.
- [ ] Publish and independently review code/tests/docs as one topology block; resolve substantive findings before real cold/envelope qualification.

### T032-B3-COLD — actual load and journal-only restart

**Files:** PortableLoadColdHost, existing owned host dispatch, PortableLoadColdRecoveryTests, recovery docs/category. **Consumes:** real typed load and v3 authority. **Produces:** source-bound cold evidence.

- [ ] Add `FreshAcquisitionRecoversActualLoadWithoutExtractionSources`: current producer archive, distinct old/new soul/resources/history/config, library sentinels and selected source; owned child announces an actual cut, parent terminates only that child, starts a fresh process, removes private extraction before recovery.
- [ ] Exercise intent staged/published, directory remove/create in both directions, first/late file/generation staging/publication, commit staged/committed and cleanup. Add rollback removal/creation/restoration interruption and repeated recovery. Use a Theory for independent roots; do not repeat costly common setup in multiple redundant assertions.
- [ ] Add `ColdUnknownLaterMemberRetainsAllEarlierStateAndEvidence`, including unknown empty directory and incomplete owned scratch; committed cleanup never reverts. Assert typed disposition where available and exact fresh-acquisition namespace, generation, source/library hashes and cleanup, not simply process exit.
- [ ] Run only `portable-load-cold-decision` and `portable-load-cold-topology`; split by decision versus structural recovery responsibility, initial budgets 8 minutes each. Publish Windows evidence, run the same owners on Linux through B5-FS, and review the coherent cold block.

### T032-B3-RESOURCE — actual envelope by phase

**Files:** PortableLoadResourceProbe/Tests, category owners and checkpoint. **Consumes:** current archive producer and real typed load. **Produces:** preparation, publication and journal-only cold measurements.

- [ ] Before launch record exact fixture/member/name counts and predicted disk footprint. Qualify separate 64, 128 and near-512 MiB expanded inputs with near-512 MiB old live bytes where material; include mandatory producer state/manifest within the accepted ceilings. Separately qualify 8,192 actual ZIP entries, near-2 MiB names, >1 MiB metadata and 9,216 disjoint old files so old deletions exceed incoming count. Report actual totals, not a nominal fixture label.
- [ ] Implement independent owned phase probes: preparation, publication and cold recovery. Each uses actual typed load/current producer; cold restarts from journal bytes with extraction absent. Sample peak RSS/owned disk/time, report allocated managed bytes per phase and exact target/history/config/source/library outcomes.
- [ ] Initial load-only controls: child GC heap hard limit **768 MiB**, peak RSS **1 GiB**, owned-disk stop **5 GiB**, each owned child **180 seconds**. At the largest qualification old/live and extracted images can each be ~512 MiB; selected ZIP ~512 MiB; active and commit frames can each hold ~1 GiB before+after, plus file scratch/metadata. Thus the save-only 3 GiB stop cannot qualify this workload. 5 GiB provides bounded headroom for this declared footprint; arbitrary larger live trees are not certified by it. Existing save-probe bounds remain unchanged.
- [ ] Use separate `portable-load-resource-preparation`, `portable-load-resource-publication`, `portable-load-resource-recovery` owners, initial category budgets **10 minutes each**, each command **15 minutes** including build/cleanup. A guard stop, incomplete sampling/cleanup or safe resource rejection is a non-pass. Record a justified plan/budget amendment before any changed control; never silently extend time or reduce the legal envelope.
- [ ] Run phase owners separately on Windows and native Linux, record observations rather than claiming a premeasured speedup, publish and independently review the qualification block.

**Concrete fixture/phase decisions (owner review waiver, 2026-10-04):** Use four
independent roots per phase: 64 MiB, 128 MiB and near-512 MiB expanded payloads with
64 KiB reserved for mandatory producer documents, plus the many-member/name case.
The largest bulk root authors near-512 MiB distinct old live bytes after production.
Write bulk data through 64 KiB buffers into files, never byte-array dictionaries.
Extend the isolated cold fixture with before-producer and after-archive-close streaming
callbacks; reuse fixture code, never mutable prepared state between tests. The
many-member root uses exactly 8,192 actual ZIP entries, including current mandatory
documents and manifest, near-2 MiB actual UTF-8 entry names, and 9,216 disjoint old
files. Fixed CJK/ASCII leaf names keep each Linux component at most 255 UTF-8 bytes
while staying short in Windows UTF-16; the producer's existing relaxed manifest
encoder remains unchanged. Inspect actual count/name/manifest/expanded totals before
loading and record them; do not subtract a guessed required-file count or remove the
original manifest to make limits pass.

Each phase enters actual typed load. Preparation stops at the actual prepared/lease
boundary with a diagnostic exception and proves no publication/session change;
publication measures from that boundary through one committed decision; recovery
stops an actual pending/committed load child on a named operation, removes only its
owned private extraction after termination, then measures fresh normal acquisition
using self-contained journal regions. Independent ZIP/before expectations prove all
namespace/hash/generation/history/config/source/library results. Seed and phase
measurements are reported separately. RSS/time/disk observations and exact heap
inheritance are required; incomplete sampling, failed setup, guard termination,
missing phase or cleanup failure is a non-pass. The existing load-only controls and
phase budgets above remain unchanged; no save-only policy or archive capability limit
is changed by this qualification.

### T032-B5-FS — native Linux CI and durable downstream handback

**Files:** bounded workflow/selection, feature quickstart/recovery docs/checkpoint/handoff; affected catalog contracts if runner integration changes. **Consumes:** accepted blocks above. **Produces:** Windows+Linux filesystem qualification and verified callable handoff, not public-client acceptance.

- [ ] Add Ubuntu `portable-load-linux.yml` with push restricted to `codex/1553-load-filesystem` and paths limited to its own file and `tests/selections/1553-load-linux.json`. Updating this explicit selection/checkpoint deliberately triggers one justified cohort at that exact pushed SHA. No matrix of every owner, schedule, broad runner fallback or secrets; contents permission is read-only.
- [ ] Repository Actions are currently disabled (API evidence); enable only the three required action patterns for this authorized native CI deliverable, preserving the old general workflow's disabled_manually state. Verify settings readback before the deliberate selected push; no cloud/OS screening setting is changed.
- [ ] Use PowerShell 7, setup-dotnet SDK 10/runtime 8, DOTNET_PROCESSOR_COUNT=1 and the standard bounded runner. Validate selection and runtime OS before launching. PlanOnly/fresh build precedes execution; record OS/toolchain/SHA, expected/completed/runtime/noop counts and owned cleanup. Upload plan/summary/TRX/probe logs even on failure. Job timeout 25 minutes, selected runner ceiling ≤20; resource commands use their tighter 15-minute cap.
- [ ] Initial Linux selection is the B1 fix/entry cohort. Subsequent source changes update the explicit selected contract cohort with rationale. Shared codec/namespace changes select affected compatibility consumers; resource phases are separate runs. Do not run every load owner on every push or replay passing unchanged accepted cohorts.
- [ ] An optional future workflow_dispatch choice can select one documented cohort once the workflow is registered on the default branch; it is not the only pre-merge execution path. [GitHub documents that workflow_dispatch requires a default-branch workflow](https://docs.github.com/en/actions/how-tos/manage-workflow-runs/manually-run-a-workflow). Do not merge solely to activate CI; missing actual execution leaves Linux acceptance open.
- [ ] Inspect Actions evidence and count actual Linux behavior; do not substitute console-capable cloud access, Windows noops, build/HTTP success or source-only Linux reasoning. Fix valid failures and rerun only affected owners.
- [ ] Verify final current-source Windows selections affected by later changes, mandatory independent review and exact clean GitHub restoration of source/evidence. Update the existing load handoff with callable result semantics, accepted requirement map, versioned recovery behavior, commands/artifacts and B4/T033/full-B5 remaining work. No game/GM capability prompt update is needed because no GM-authored contract changed.

## Category planning and evidence rules

Initial fixed cohorts are existing `portable-load-alias-admission` (14 cases), `portable-load-outcomes` (4) and `portable-load-entry` (9), budgets 3 minutes each. New category names above are proposed contract owners; the implementing agent may split/rename them with corresponding catalog, selection and docs changes. Metadata/topology owners initially get 5 minutes each; observed >5-minute owners are investigated/split by responsibility. No unmeasured expectedSeconds claim.

For each coherent block:
1. Record changed requirements/consumers and selection rationale; inspect exact discovery before runtime.
2. Run `pwsh -NoProfile -File scripts/test-csharp.ps1 -Category <selected owners> -PlanOnly`, then the same selected owners (fresh build before NoBuild); resource phases run separately with `-TimeoutMinutes 15`.
3. Run `-ValidateCatalog` as discovery-only ownership audit after catalog changes. It compiles/discovers but never executes every test. Keep complete required-field/case counts, skipped/early-return distinctions and owned cleanup.
4. Store safe plan/summary/TRX/probe evidence under existing feature `recovery/evidence/`, commit/push checkpoint, verify full remote SHA, and provide exact diff plus evidence to Sol 6.1 XHigh reviewer. Passing tests repeat only for changed code/dependencies or unresolved risk.
5. After review, record verified versus accepted scopes separately. No task is complete merely from an agent report or push.

## Requirements, dependency and handoff map

| Requirement | Execution owners | Remaining scope |
| --- | --- | --- |
| LOAD-FS-001 | B1-FIX, B2-NAMESPACE admission/collisions, B5-FS native names | Arbitrary names remain meaningful. |
| LOAD-FS-002 | B1-FIX entry controls, B2-NAMESPACE, B3-COLD | Full public clients are B4. |
| LOAD-FS-003 | B1-FIX, B2-NAMESPACE outcome confirmation, B3-COLD | Clients consume this typed truth later. |
| LOAD-FS-004 | Existing detached preparation qualified by B1-FIX/B2/B3 | No precommit runtime changes. |
| LOAD-FS-005 | B1-METADATA, B2 v3 schema, B3-RESOURCE | Existing v1/v2 compatibility retained. |
| LOAD-FS-006 | B2-NAMESPACE, B3-COLD, B5-FS | No temporary same-shape restriction at acceptance. |
| LOAD-FS-007 | B3-COLD, B3-RESOURCE, B5-FS | Process crash; both native OS. |
| LOAD-FS-008 | Every block's selection/review/checkpoint, B5-FS | B4/T033/whole #1553 remain open. |

Order: authorized plan/design (owner's explicit review waiver) → B1-FIX → B1-METADATA → B2-NAMESPACE → B3-COLD → B3-RESOURCE → final B5-FS handback. B5-FS CI infrastructure is installed after plan approval with B1 so each implementation block can obtain actual Linux evidence; its final acceptance waits for all filesystem gates. One implementation writer; optional read-only consultation only for a concrete unresolved problem.

Current policy reassessment: the remote source proves nine first-slice entry cases and five unresolved reviewed defects, but no comparable execution/resource-cost measurements for model routing. This plan consolidates codec/topology ownership and selects causal affected checks; no speedup or token-saving percentage is asserted. At each accepted coherent block record substantive review corrections, rework, test/build elapsed time and observed missed requirements in plan.md; propose routing changes only from evidence.
