# Ordinary-load namespace model — execution revision 1

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Requirements: [spec.md](spec.md). Algorithms/interfaces: [ordinary-load-plan.md](ordinary-load-plan.md).
Status: concrete v3 model cleared by independent design review and authorized by the owner's subsequent explicit spec/plan/revision review waiver; no implementation claim.

## Persistent-main ownership slot — schema 1 (T041-RUN-RECORD)

This schema is separate from the ordinary-load namespace frames below. It records
one persistent-main GM ownership slot, never the set of all active worker writers.
No filesystem record writer or production reader is wired in this component.

`GmSessionRunRecord` has exactly `SchemaVersion` (1), `Identity` (object),
`Disposition` (exact enum string), and required nullable `StopEvidence`.
`Identity` has exactly `RootKey`, `RunId`, `GenerationId`, `Epoch`, `Backend`,
`HostInstanceId`, `BootId`. Epoch is an Int64 >=1. Run/host IDs are canonical
lowercase N-format nonempty GUIDs; generation follows the current generation
reader's canonical N-format domain, **including all-zero GUID**. RootKey is a
nonblank non-control string bounded to 4096 strict UTF-8 bytes; it is a normalized
root identity supplied by a trusted adapter, not a filesystem path grant. BootId
has 1–128 ASCII letters/digits/dot/underscore/colon/hyphen. Backend is exactly
`WindowsJob` or `LinuxSupervisor`, not a claim that either adapter is qualified.

Disposition is `Prepared`, `Running`, `Stopping`, `Uncertain` or `Stopped`.
Nonterminal records require null StopEvidence. Stopped requires an object with
exactly `Identity`, `Kind`, `ObservedBootId`; the complete evidence identity must
match the record. `OwnedScopeEmpty` requires the same boot identity;
`VerifiedHostReboot` requires a distinct nonempty observed boot identity. These are
trusted adapter observations; the codec does not authenticate a host, reboot or
process-tree stop. PID/EOF/status/elapsed-time observations are not substitutes.
A terminal record retains the evidence rather than deleting the epoch tombstone.

The complete UTF-8 document is at most 65,536 bytes. Reject BOM, invalid UTF-8,
comments, trailing JSON, excessive nesting, duplicate (including escaped-equivalent),
unknown, missing or wrong-typed properties at every object depth, numeric/unknown
states and invalid cross-field combinations. Diagnostics are fixed and omit payload
and parser exceptions. Empty/truncated/invalid bytes are Unreadable, not Missing.
Only the trusted caller's explicit absence observation can represent Missing.

`GmSessionRunTarget(RootKey, Backend, GenerationId)` identifies current trusted
context independently of stored bytes. Admission rejects a stored backend mismatch
before using the target's Windows ordinal-ignore-case or Linux ordinal root
comparison. Every other identity field is ordinal/exact. Generation may be absent
in the target; it must exist before StartRun or ActiveRunMutation. Syntax-only Decode
is not live ownership: cold interpretation first maps every nonterminal record to
Uncertain. DiagnosticRead never requests recovery writes.

Transitions are immutable: Prepared -> Running; Prepared/Running -> Stopping;
nonterminal -> Uncertain; identity-bound valid stop evidence -> Stopped. Cold
interpretation maps Prepared/Running/Stopping/Uncertain to Uncertain and preserves
Stopped. Exact repeated stop evidence is idempotent; stopped records cannot be
reopened, and rejected transitions preserve their original value. The running
transition is a plan to persist **before** writer release after independently
verified ownership, not permission to release a process by itself.

Admission is slot-local and returns a decision, never a reusable authority lease:
- DiagnosticRead: allowed for any observation, including unreadable evidence
- QuiescentMutation: main-slot condition satisfied only by Missing or validated
  Stopped for the trusted root/backend; terminal generation may predate replacement
- StartRun: same main-slot condition plus current generation and a matching fresh
  candidate. Missing requires epoch 1; Stopped requires previous epoch + 1 and a
  different RunId. Int64 exhaustion blocks without wrapping
- ActiveRunMutation: only Running with the complete current identity and generation

Unreadable/Uncertain and unknown operations block every mutation. A later complete
fence composes all worker ownership conditions and re-evaluates under the same
ordering as record transitions and actual mutations, including already-held leases.

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
