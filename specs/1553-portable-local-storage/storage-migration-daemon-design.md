# F14 original daemon read/admission migration

Checkpoint: 2026-10-08, after bounded F13 carrier `38940493`.
This is a source-backed design and finite initial causal plan, not executed F14
failure evidence or a completed daemon migration. No runtime changes in this checkpoint.

## Intent and preserved authority

Move admission-sensitive daemon reads onto its existing original participating
connection and bound generation. Preserve all public PowerShell names, correlation,
eligibility, prompt identity, retry/no-replay, terminal and repair business rules.
The daemon must not dispatch from a transient before-recovery image, turn a storage
refusal into absence/defaults, or delete a replacement based on an older observation.
No new GM/provider/desktop execution, owner reconstruction, journal, worker capability,
whole-operation atomicity promise, or mutation allowlist expansion is authorized.

The preferred route extends the retained A03 connection with narrow read/snapshot/check
operations. Each operation obtains a short `PublicationReadQuiescence` lease under
its existing bound operation and releases it before dispatch, polling sleeps or
external calls. No nested F13 process, connection reopen, or lease across remote
input. Quiescent local A03 behavior remains supported; the causal race fixture must
use an actual Running main because a quiescent original guard would mask the race.

Alternatives rejected: raw reads plus a later guarded write still admit transient
policy/source decisions; a nested helper would transfer neither the original A03
receipt nor role; holding a canonical lease across prompt dispatch would obstruct
other canonical producers and change the existing protocol's concurrency contract.

## Complete source boundary

| Consumer | Required admitted observation and retained behavior |
|---|---|
| `Read-GmPromptPending`, `Test-GmPromptSourceCurrent`, `New-GmPromptOperation`, `Dispatch-WithRetry` | Exact pending bytes/hash from a recovered snapshot; current-source checks before allocation, immediately before send and after the possible send. Preserve frozen operation/input binding, proven zero-write retry, and Unknown after possible input plus replacement. |
| QTE request/current/Ready functions and `Process-QteEffectResolutionRequestCore` | Coherent request/Ready eligibility, exact existing string/numeric correlation and receipt-only message. Initial and subsequent checks use admitted reads. Never infer dispatch authority from read failure. |
| Ordinary turn pending manifest/detached authority/declared files/backup hashes | One coherent short snapshot for the full authority cohort, preserving exact schema/hash validation and the intentional consumed-request/correlated-terminal exception. No adoption from diagnostic copies. |
| Repair request and terminal processing | Read bytes plus last-write metadata under the same admission; advance watcher watermarks only after a successful admitted observation. Existing parse-invalid handling stays distinct from storage/type/generation refusal. |
| Ready and correlated-terminal cleanup | A failed read must not authorize deletion. Any later delete is conditioned on the exact observed current bytes at the original mutation boundary; retain the existing control-path write scope. |
| Terminal payload and repair progress snapshots | Migrate recursive `output`, `game_state`, `lore` enumeration, target file metadata, current repair and Ready checks (daemon3127–3134,3395–3397,3453–3482). Missing and blocked/invalid namespace are distinct; host path spelling must preserve literal Linux backslashes. |
| Config and Bridge/daemon status consumed by decisions | Admitted byte reads, parse-only fallbacks, exact existing config defaults and input-binding policy. PID/status observations remain diagnostic, never owner authority. |
| Watcher/startup/poll notifications | Remain wake-up hints; authoritative eligibility rechecks occur inside the original operation. No watcher event becomes generation/adoption authority. |
| Observed terminal request-key cache | Admitted startup load (daemon5153–5177) feeds work suppression at5801. Preserve actual volatile observations on Add/save5180–5204; do not assume durable-write-before-cache is required or adopt a stale generation. |
| Experience, lessons and trajectory inputs | Canonical inputs at478–515,898–921,998–1012,4007,4315,4374,4555,4729 feed derivative hints and ProcessTurn5821–5823. Preserve parse/hint policy while propagating actual storage refusal. |
| Live-test notes backfill | Read/rewrite4728–4765 must condition its final write on exact observed bytes, preserving malformed lines and an intervening GM append permitted by1308/2659. An admitted read followed by unconditional write is insufficient. |
| Repository context-pack source | `Copy-GmCanonicalFile` is called by `Copy-GmContextPackFile` for repository documentation. That source is intentionally outside game_session; its canonical destination already uses A03. Do not reclassify it as game-session read authority. |

Current source examples: daemon3655–3666 pending/hash;5568–5635 QTE correlation;
5671–5741 QTE eligibility/wait;5469–5499 prompt allocation/send checks;
4913–5137 ordinary authority validation;6150–6154 and6407–6411 watermark paths;
6478–6563 Ready/terminal cleanup;2880–2935 config/status. Line numbers are an index,
not a substitute for tracing all callers when the runtime delta freezes.

## Read transport and failure semantics

Use trusted local scope/regular-file/ancestor checks and the existing exact original
generation before any recovery or read. A snapshot carries exact bytes or absence,
hash and requested metadata from the same short lease. Canonical parsing/correlation
stays in PowerShell. Requests that require multiple authoritative files must read
that cohort under one lease, not sequence independent snapshots and call them atomic.

For the dynamic pending-turn cohort, the initial manifest determines later path names.
Prefer a second snapshot on the same original connection that includes the exact
manifest witness, detached authority, and every validated declared file/backup under
one lease; reject if the initial manifest bytes or generation changed. The first read
alone grants nothing. Alternatively a short original-connection read scope may retain
the lease across only local parsing, with guaranteed release before any dispatch or
sleep. Freeze that choice and its actual manifest-change control before implementation;
a fixed QTE request+Ready batch does not by itself qualify the dynamic cohort.

`Write-DaemonStatus` deliberately has a best-effort volatile fallback. Preserve it;
this design does not claim that fallback is a publication exception-masking defect.
Actual authoritative read refusal still must survive `Process-QteEffectResolutionRequest`
catch handling and outer original-operation projection truthfully.

Reuse/extract the bounded chunk mechanics if it reduces duplication, while keeping
A03 and F13 roles and closure independent. Do not copy the F13 helper's write authority.
No arbitrary new 64-KiB document ceiling or inherited 4-MiB aggregate ceiling for
read results. Frame metadata is fixed/small; paths, enumerations and bytes travel in
sequenced chunks with exact total/offset/final hash and one original-process buffer.
Malformed/lost transfer cannot become absence or allow a later replay; no disk spool.
The exact factoring and new command names remain to be frozen after causal fixtures.

Valid read/admission refusal is distinct from malformed JSON and transport loss.
It must survive forgiving daemon catches and the outer consumer projection. Preserve
its first actual cause and existing original close opportunity. Do not manufacture
a publication disposition from an ordinary read refusal, or mark a live original
connection lost merely because it returned a valid negative response. Existing
actual publication Uncertain remains absorbing; committed cleanup remains committed.
The precise bounded read-failure carrier/effective-close mapping is an explicit
runtime design review item before implementation, not permission to broaden A03's
current outcome classifier.

## Initial causal selection: five new QTE rows plus four delivery neighbors

Use a real neutral Running Bridge/main owner and actual PowerShell A03 context.
Wait for that context's original admission before starting a separate legitimate
writer pin. Pause the actual common publisher at `MemberPublished`, validating
member index/path and exact bytes. The selected request/Ready is not a fabricated
carrier; both A and B are minimal inert daemon-consumer packets with distinguishable keys/content.
They exercise the original eligibility/correlation fields, not a complete client-produced
QTE continuation or gameplay contract.

| New row | Actual producer decision | Required consumer result |
|---|---|---|
| request rollback | Request A→B, hold actual publication, ordinary cut, exact A rollback | Dispatch only the recovered A key/hash/message. |
| request commit | Request A→B, hold actual publication then commit | Dispatch exact B; meaningful known-commit positive. |
| request unknown | Request A→B, replace with valid distinct C and throw | Retain actual Unknown journal/bytes; no dispatch, no cleanup/adoption/replay. |
| Ready rollback | Correlated Ready appears, hold actual publication then rollback to absence | Original request remains eligible and dispatches once. |
| Ready commit | Correlated Ready appears, hold actual publication then commit | Suppress dispatch and preserve exact committed Ready. |

Extract exact daemon functions via AST. Keep public wrapper/core, reads, correlation,
message construction and `Dispatch-WithRetry` original. Only the lower delivery
collaborator is inert; it records the actual frozen operation/message and returns an
identity-matching nonretryable `not-written` result. Set `LogFile=''`, seed explicit
inert configuration, use actual original status/input binding, and never invoke a
provider/desktop/input path. A hard-stop collaborator guards any unintended send.

Synchronize at actual lower-dispatch entry OR existing canonical-lock contention,
not whole handler return: its original `finally` status publication can wait on the
producer even before migration. Release the original writer only after this positive
witness, then collect its real decision and the consumer's exact result. A future
fix must reach the same gate without requiring pre-fix raw behavior. Every row joins
writer, original pwsh and helper IO independently on failure, records the immutable
original close/ACK, and proves same owner retirement plus healthy physical guardian.
Unknown is asserted retained before any explicit owned fixture cleanup.

Retain these four exact existing delivery neighbors (not storage-admission proof):
- `GmDaemonPromptDeliveryTests.ProvenBusy_RetriesIdenticalFrozenOperation`
- `GmDaemonPromptDeliveryTests.CallerSnapshotReplacedBeforeAllocation_CannotSendOldMessage`
- `GmDaemonPromptDeliveryTests.PendingReplacedDuringRemoteCall_CannotReturnSent`
- `GmDaemonPromptDeliveryTests.ReplacedAfterProvenZeroWrite_CancelsWithoutReplaying`

The old `daemon-functions` fixture replaces admission and QTE eligibility, so it
cannot prove this storage defect. The ProductionMain consumer-boundaries fixture
supplies the real AST/original-context pattern, but its missing-file early returns
are not this causal proof. Exact new method names/category ownership/count discovery
will be frozen with the test-only packet before any actual execution. No historical
RED is claimed in advance; known-commit positives may already pass.

## Remaining F14 gate and evidence plan

Initial QTE9 does not close F14. Before runtime/source freeze, enumerate every changed
turn/terminal/repair/config/snapshot caller above and add only the meaningful exact
controls needed for those edits: pending-authority cohort, admitted metadata/watermark,
read-failure/no-deletion and replacement-before-delete, large snapshot/chunk integrity,
malformed JSON versus admission failure, and original closure/loss neighbors. Final
case counts are intentionally not invented before that delta exists.

Order: independent design review → frozen test-only causal source/selection → fresh
PlanOnly → actual bounded causal results/cleanup → preserve source/evidence remotely
→ minimal whole-consumer implementation and exact final selection review → matching
fresh builds/actual GREEN → discovery-only catalog → independent raw/artifact gates
and parent fresh GitHub-only restore. All earlier migration families and native
qualification limits remain in the main inventory; T062–T065 remain unchecked.

## Frozen test-only fixture checkpoint

The initial selection is exactly `portable-gm-daemon-qte-storage-causal-linux`
(five new rows) plus `portable-gm-daemon-storage-delivery-neighbors` (four moved
existing exact methods). The original broader category retains its other methods.
No F14 production behavior changes; fresh build/PlanOnly and independent fixture/
selection review precede any actual run. No RED or PASS is claimed at this checkpoint.

The driver enters through MainRunFenceScenarioDriver after actual neutral Bridge
startup, before other participating scenarios; it creates no unrelated manual
beginMainOperation pin. The real control server serves the original PowerShell
admission before the separate original-owner writer starts. Technical JSON markers
are published only after complete temp-file writes. Synchronization records the
actual command action: old finally status-write contention is never described as
an admitted read. The lower delivery collaborator records the frozen payload and
hash, returns truthful not-written, and never invokes a provider.

Each row independently settles its writer, original PowerShell and helper I/O,
then records same-owner Stopped/no retained authority. Forced termination or
cleanup failure is fixture failure, not causal evidence. Unknown evidence is
recorded before explicit fixture-owned byte repair and recovery for cleanup.
Native execution and the remainder of the whole daemon migration remain open.

## Concrete implementation choice (foundation WIP, unbuilt)

Use a separate chunked daemon exchange on the same A03 process/sequence. Only
in-memory transfer buffers span frames. A completed snapshot opens one short
PublicationReadQuiescence lease, normalizes and validates requested targets,
reads exact bytes/absence/kind and handle-derived timestamps, and enumerates
requested trees. Maps use the host destination comparer; Linux case/backslash
siblings remain distinct. No new aggregate document ceiling, disk spool or helper
role is added. Existing mutation allowlist remains the sole write boundary.

Dynamic turn authority chooses two batches: initial request/manifest/authority
observations determine the declared paths, then the second batch verifies their
exact initial hashes and reads the whole declared cohort under one lease. Witness
change returns a normal mismatch; storage/type/generation/recovery failure returns
the fixed GmDaemonReadRefused marker, thrown outside transport-loss handling.
Authoritative daemon catches will rethrow it. The original outer operation closes
Failed on that same connection and annotates the unchanged first cause with actual
close/ACK; any earlier own publication Unknown retains its existing override.

Notes rewrites and Ready deletion use conditional requests restricted to existing
control write paths. Target witness is mandatory and all supplied witnesses are
checked initially and at the existing publisher's prepared/per-retry seam. No old
Windows physical CAS is revived. Capture actual publication outcome before facade
throw/lease disposal. This foundation is not full consumer wiring or acceptance;
whole caller migration, causal GREEN and additional connected controls remain.

Independent causal/carrier383268f9 PASS covers80 artifacts/48 historical pins.
Three reached defects and two commit positives plus four delivery positives are
retained source-era evidence; no F14 runtime tests have been run.
