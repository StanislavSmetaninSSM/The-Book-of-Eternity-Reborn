# Ordinary-load namespace model — execution revision 1

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Requirements: [spec.md](spec.md). Algorithms/interfaces: [ordinary-load-plan.md](ordinary-load-plan.md).
Status: concrete v3 model cleared by independent design review and authorized by the owner's subsequent explicit spec/plan/revision review waiver; no implementation claim.

## In-memory descriptors

- `TrustedLocalNamespaceKind`: Missing, Directory, File.
- `TrustedLocalNamespaceImage(Kind, FileImage)`: Missing/Directory require null FileImage;
  File requires a present `TrustedLocalFileImage`, including a valid zero-length image.
  FileImage owns exact length/hash and a closed byte or file-region source.
- `TrustedLocalNamespaceChange(Path, Before, After)`: normalized absolute mutation path
  plus its two states. One unique collection/trie owns complete descriptors.
- `TrustedLocalNamespaceBoundary(Path, Kind, Length, Sha256)`: immutable admitted library
  or in-session selected source. Directory/Missing require Length=0 and Sha256=null;
  File requires nonnegative length and exact hash. Boundaries have no payload offset.
- `TrustedLocalNamespacePlan(RootPath, Changes, Boundaries)`: exact game_session root,
  complete covered node set and protected boundaries. The root itself remains Directory.
  The only mutation outside that root is the manager-appended exact generation file.
- `CanonicalLoadNamespaceChange(RelativePath, Before, After)` uses session-relative paths.
  `CanonicalLoadNamespaceSnapshot(Nodes, Boundaries)` has
  `IReadOnlyDictionary<string, TrustedLocalNamespaceImage> Nodes` and
  `IReadOnlyList<TrustedLocalNamespaceBoundary> Boundaries`.
  `CanonicalLoadNamespacePlan(Changes, Boundaries)` has
  `IReadOnlyList<CanonicalLoadNamespaceChange> Changes` and the same boundary list.
  The manager resolves relative paths, validates exact boundaries and adds generation;
  callers do not obtain a general runtime/external write grant.

## v3 frame schema

Eight ASCII magic bytes are `BOELP3` followed by CR and LF, then a signed little-endian
Int64 metadata length, then strict UTF-8 JSON and contiguous file-image regions.
Property order is arbitrary; decoded property names are ordinal/case-sensitive.
All listed properties are required, including nullable fields; unknown, duplicate,
wrong-type and missing properties reject before any recovery mutation.

| Object | Required properties and values |
| --- | --- |
| Root | Format=3; TransactionId (existing canonical transaction-ID contract); Committed boolean; GenerationBefore; GenerationAfter; NamespaceRoot (exact normalized current game_session path); Members array; Boundaries array |
| Generation | Exists boolean; Id nullable string, with unchanged existing presence/ID validation |
| Member | Path normalized absolute string; Before image; After image |
| Image | Kind exact string Missing/Directory/File; Length nonnegative Int64; Sha256 nullable string; Offset nullable nonnegative Int64 |
| Boundary | Path normalized absolute string; Kind exact string Missing/Directory/File; Length nonnegative Int64; Sha256 nullable string; no Offset |

Missing/Directory images require Length=0, Sha256=null and Offset=null. File images
require the existing valid SHA-256 and an Offset even when Length=0. Traverse Members
in array order, Before then After, counting only File regions: each offset must equal
the checked running offset. Total coverage exactly equals the bytes after metadata;
gaps, overlap, overflow, truncation, trailing payload or wrong hash reject.

The exact generation member is a Missing/File before image and File after image,
bound to GenerationBefore/After with existing BOM/extensions/exact-old-byte semantics.
It is reconciled last regardless of its metadata array position. No other member
outside NamespaceRoot, extra generation path, reserved lock/journal target or boundary
overlap is admissible. The sole allowed boundary nesting is the exact selected-source
File beneath the opaque library Directory: validate its read-only length/hash without
enumerating other library contents or adding mutation nodes there. Boundary identities
are the manager's canonical library and admitted source, not arbitrary caller exclusions.
Root/path normalization and OS comparison retain existing rules.

## Namespace and evidence invariants

Every child within covered directories is a mutation node, explicit preserved boundary
or exact transaction-owned scratch name. Empty directories count. Ancestor directories
are explicit or the immutable root; covered protected-boundary ancestors remain Directory
in both snapshots. Ancestors inside an opaque library are validated without following
links only for an exact selected-source read. The save library is opaque at its exact admitted Directory/Missing
state. In-session source is an immutable File length/hash boundary; external source
never appears as a recovery authority path.

Before intent: complete Before namespace, source, prepared images and generation match.
Pending: each node matches Before/After, with logical Missing additionally allowed only
for declared File↔Directory conversion gaps. An inaccessible descendant is not absence:
a blocking File ancestor must first match an allowed recorded image and the descendant
must allow Missing. All current direct child inventories validate before reconciliation.
Committed: complete After namespace validates, then only owned cleanup is permitted.
Rollback confirmation: complete Before namespace and exact generation validate.

Each active/commit frame contains its own full Before/After file images. Stable stage
and undo anchors are derived from transaction/node identity under an ancestor Directory
present in both snapshots; generation uses its exact runtime parent. No operation cursor,
external payload dependency or second authority record is introduced. Unknown contents
retain evidence and block; committed cleanup failure never becomes rollback.
