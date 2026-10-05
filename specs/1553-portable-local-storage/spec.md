# Feature Specification: Trusted local storage and cross-platform runtime

**Feature Branch**: `codex/1553-load-filesystem` (local continuation from `codex/1553-save-windows`)
**Created**: 2026-09-30
**Status**: Original feature approved; ordinary-save capability accepted at `ddaade72`. The owner approved the exact local load-filesystem specification revision 1 below. The owner subsequently waived further written spec/plan/revision review and authorized recommended autonomous decisions; execution plan revision 1 and its namespace-frame design addendum are authorized; full Load and remaining B3/B4/B5/platform acceptance remain open.

## Source Issues & Scope

- Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553)
- Type: cross-platform enhancement spanning multiple sessions and both clients
- Contract scope: runtime-state, console, browser, process/IPC, GM-facing operational guidance, tests and documentation
- Save compatibility: no general pre-release compatibility promise. Current-format transition states are explicit below
- Approved source: “Поддержка Linux и Windows”, revision 2, four pages, 2026-09-30; owner approved at 02:27 UTC and requested autonomous execution
- Latest direction, 18:26 UTC: replace Windows-only mechanisms, remove protection against the owner editing their own saves, demonstrate complete Linux behavior; the owner will execute Windows checks
- Wound implementation was merged by explicit owner request in [PR #1554](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/pull/1554). Its unfinished acceptance remains deferred under #1536, not declared complete by this feature


## Approved two-backend Linux ownership — 2026-10-05 15:12 UTC

Owner decision: primary existing **systemd user manager**, plus a **native ordinary-lineage fallback** for cloud/similar environments. This explicitly supersedes the earlier unconditional complete-descendant requirement only for the declared fallback scope. It is a product guarantee change, not permission to bypass environment security. Source base `2defe92cd8b7d313d07b059db76b73e97905f66f`, same branch/sole writer. First implementation is T041-FALLBACK-NATIVE synthetic helper qualification only; production pool/main Release wiring requires the next handoff/authorization.

- `systemd-user` declares the owned transient unit/cgroup boundary. Prefer it in Auto only when an already-running accessible user manager supplies the required transient-unit, stop and authoritative empty-boundary capabilities. No root, new persistent service, enable/autostart, cgroup delegation or policy changes. Positive native systemd qualification is absent here; mocks cannot establish it.
- `native-lineage` declares **ordinary descendants remaining within tracked lineage in the same PID namespace**, including ordinary double-fork, setsid and process-group changes. It does not cover external brokers/services, descendants outside the tracked lineage or unrestricted namespace migration. Those absences need not be universally proved on every ordinary launch. Detected escape/scope breach, lost authority, timeout, incomplete cleanup, owner loss or restart ambiguity is `Uncertain`; preserve quarantine and slot and do not automatically accept the task result.
- Default `Auto`: prefer available systemd-user; if unavailable before any launch, select native-lineage explicitly after its prerequisites pass. Explicit `SystemdUser` never silently downgrades. Explicit `NativeLineage` declares its limited scope. After a launch may have happened, backend switching is forbidden; uncertain startup retires through the original authority. Neither backend claims external delegated work. Windows Job behavior is unchanged.
- Every readiness/status/stop-evidence record exposes backend, guarantee scope, run identity, state, reason, whether managed authority is retained and whether scoped cleanup actually completed. `StoppedWithinScope` is never an Accepted proposal or durable run/fence. Consumers must preserve the scope and typed uncertainty; an old unqualified bool must not erase them. Source guards for existing production gates remain; new tests must assert the revised two-mode contract without enabling Linux Release.

## Worker-host integration — staged contract, 2026-10-05

From accepted native checkpoint `1434be00abd143e9f2ed9d0589d1c30b8063ba06`,
T041-FALLBACK-INTEGRATION-DESIGN defines two connected slices under US4/FR-012/014/015.
The parent authorized only A from `e7b26f9c`; implementation and bounded verification
are recorded in the current plan. B remains separately gated. This specification
is not execution or qualification evidence. [Source-backed plan](plan.md#worker-host-native-integration-design--2026-10-05).

- One owned launch seam must be consumed by the actual worker host and pool. Native
  supervisor forks the neutral managed host itself and binds its pidfd before exec;
  attaching an already-running host or using its reported numeric PID as authority
  is prohibited. Supervisor and actual-host identities remain distinct. Both host
  control/status peers must match the checked actual-host PID/EUID and retained live
  identity before any worker Launch payload; Windows keeps its original Process/Job path.
- Slice A connects packaged helper, private ownership/binding channels, separate
  host output streams, typed backend selection and the real host Ready/owner-close
  path. Ordinary RunTaskAsync requests WorkerRelease and, in A, rejects native before
  starting helper or host; neutral tests use its same extracted host-preparation method.
  It never sends worker Release. Slice B separately exercises synthetic worker
  Release/result/stop/quarantine through `GmWorkerBridgePool.RunTaskAsync` and existing
  proposal/generation/lease guards. Production Linux Release and persistent main/PTY
  remain closed pending their explicit integration authorization and restart guard.
- Backend availability distinguishes implemented/qualified capabilities from runtime
  prerequisite availability. An unimplemented or unqualified systemd adapter cannot
  satisfy Auto's primary preference. Report its exact reason and select qualified
  native-lineage only for the requested admitted stage. Explicit SystemdUser fails
  closed; no pretend primary, silent downgrade or switch after possible launch.
- Preserve backend, scope, run binding and sticky uncertainty through pool, result,
  quarantine and downstream success checks. Root exit, helper exit, Completed or
  OutputDrained is not scoped stop. Detected Uncertain never automatically permits
  proposal import, workspace deletion or slot release, even with cleanupComplete=true.
- Player launches consume a relocatable prebuilt helper and version/source/binary
  manifest; no compiler, PowerShell build, PATH fallback, download or service setup
  at runtime. Initial Linux ABI/RID support is explicit and checked before host launch.
- Native owner death may clean its lineage but cannot restore a dead owner's durable
  authority. Existing in-memory worker slots are not a restart fence. Restart/reboot
  admission and unresolved quarantine recovery remain a rollout prerequisite, not a
  claimed consequence of these two synthetic integration slices.

## Authorized synthetic POOL B — 2026-10-05

The parent accepted HOST A at `55f43c9e5d81ec0ef1e3944d2932f49f67b22723` and
explicitly authorized T041-FALLBACK-POOL with a separate typed evidence/quarantine/
consumer design review. The [B refinement](plan.md#pool-b--authorized-refinement-from-accepted-host-a-2026-10-05)
extends only that accepted base. A distinct injected internal synthetic admission
can execute the actual pool in isolated fixtures; no public configuration/environment
switch admits ordinary Linux WorkerRelease. Durable restart/fencing remains unconnected.

Correlated Completed must precede validated matching run/backend/scope stop and
bounded owned-output settlement. Only then may cancellation/generation/lease/exact
reservation checks, detached proposal validation and existing publication proceed.
Both real delegating consumers require that evidence; Proposal, exit0, root/helper
exit, pidfd readability and OutputDrained alone cannot establish success. Uncertain
retains the original owner/evidence/workspace/slot/reservation, may have lost authority,
and cannot become certain merely because cleanup later finishes. No cleanup-confirmed
receipt or automatic result acceptance follows uncertainty. Valid-stop filesystem/
receipt retries preserve idempotent phase order and exactly-once capacity release.
Only independent finite guardians retire failed synthetic fixtures; their cleanup
cannot repair production authority. Windows Job meaning stays intact, runtime here
unqualified. B handoff precedes a separately authorized durable restart/fencing plan.

## Input transport lifetime — T042-INPUT-LIFETIME, 2026-10-05

Bounded implementation passes37/37 actual managed-consumer cases at `dc29b37d0c3e3067acf9943a048360fbba66d0e3`, with causal lifetime RED and a separately reproduced/fixed review regression. [Qualification](recovery/gm-input-lifetime-qualification.json) retains exact build, test, discovery and cleanup evidence. Independent actual Sol6.1/xhigh final PASS and clean GitHub-only restoration verified at `a5aefe8f8151c67a15fa107e78b487581bd43458`; full T042 remains open.

The owner authorized only the first T042 slice from exact `618a8a20cba345f367a5e15659019d3dec6fd2be`: bind queued writes and keyboard consumption to the originating shell-input lifetime, cancel/reject stale work and settle the old keyboard pump before admitting a replacement. This local binding is not a run/generation/fence. Preserve exact current paste/submit/key bytes and all process/ownership/platform guards. Do not implement transaction arbitration, readiness/trust changes, daemon/QTE outcomes/retry, queue limits or manual takeover policy.

Serialize local start/retirement and write-start/revocation. Retain underlying queued/started I/O and keyboard tasks until settled; a five-second managed drain observation timeout retains context and blocks replacement, never proves process stop. Dispose closes new admission before drain and does not dispose synchronization/token resources under unsettled consumers. A late old key read cannot write into B. Pre-write rejection/cancellation means zero new bytes; failure/cancellation after write-start is conservatively uncertain even if an underlying stream reports success after ignoring cancellation. Retain a fixed local input-error diagnostic, revoke that lifetime, and never report the failed dispatch Completed or swallow keyboard write uncertainty. Old completion/error cannot overwrite replacement status.

Use actual built BridgeHost methods and normal constructor with controlled streams/key source, causal RED/GREEN, narrow catalog and independent actual gpt-6.1-sol/xhigh design/final review. No CLI/PTY/ConPTY/process Release; no native stop qualification. This client-owned transport repair adds no GM-authored gameplay field; its operational limit belongs in the existing plan/quickstart, not a new gameplay example.

## Persistent-main output decoding — T041-OUTPUT-UTF8, 2026-10-05

Bounded implementation status: actual managed bridge output pump passes48/48 on Linux at `ff7e5a2b08f8fde196151909487c1d94089ea91d` (runtime source `a9472a35`), after causal19/48 RED. [Qualification](recovery/gm-output-qualification.json). Independent actual Sol6.1/xhigh final PASS at `426b1680`, with clean GitHub-only source restoration. This qualifies only the bounded managed output component; no terminal/ownership readiness follows.

From exact `08e9805d10237d3bc50438e67b58fd33c5509c42`, fix the production-consumed `BridgeHost.PumpOutputAsync`, preserving raw byte forwarding and the current UTF-8 replacement fallback. Each invocation owns one decoder; incomplete scalars span reads and finalize only at a genuine successful zero-byte EOF, never cancellation or read/write/flush failure. Every successfully forwarded nonempty byte read still advances output version and wakes observers even when no character is complete. EOF replacement output wakes text observers without inventing byte activity. Decoder state cannot cross pump invocations.

Preserve recent-output and diagnostic upper bounds of 65536 and 12000 UTF-16 code units; truncation drops an additional low surrogate if the cut would bisect a valid pair. Keep diagnostics DTO and raw console behavior. This client-owned correction does not add a GM-authored field or gameplay capability, so no GM worked example is required. Readiness/autotrust, manual input, configured visibility/paste/submit behavior, terminal backend, process/stop/ownership and all production guards are outside scope.

Proof must execute the actual bridge pump method from its freshly built production assembly with synthetic streams on Linux, including fragmented Unicode, malformed bytes, EOF, faults, cancellation, activity, separate lifetimes and both tail limits. No CLI/ConPTY/PTY/GM starts. Native Windows and terminal/readiness/lifecycle qualification remain open. Existing environment evidence shows no usable systemd user manager/writable cgroup delegation in this selected environment only; it is not a Linux-wide conclusion or authorization for configuration changes.
## Portable quarantine audit receipt — T041-QUARANTINE-RECEIPT, 2026-10-05

The authorized bounded continuation from `c815c4fd3890871c95355c42546a3bd88df65867` ports the existing private runtime terminal receipt, not process ownership. Preserve stable EventId/session-generation/schema and compact exact UTF-8 bytes, create-only publication, same-content idempotent retry and conflicting-content rejection. Partial/failed/cancelled writes must not become terminal evidence; a published exact receipt remains recoverable after lost acknowledgement. Fresh trusted-local path/kind/link admission applies before reads, creation, publication and owned temporary cleanup. Linux uses the accepted by-name model (ordinary hardlinks may be read/unlinked, never overwritten); Windows keeps retained-handle/single-link checks. No hostile-owner resistance or new power-loss claim.

Confirmed death remains mandatory before quarantine cleanup. Failure to record terminal audit retains runtime authority and worker slot; retries use the original event identity. No receipt may bypass stop, widen canonical acceptance, or release a slot on failure. Synthetic filesystem fixtures and an already-confirmed/no-process quarantine owner may exercise this ordering; no actual GM/Release/process boundary or namespace settings are authorized. This is an internal client-owned existing format, not new GM-authored output; update operational guidance and exact guards, with no Mortal World/afterlife prompt or worked-example change required.

Bounded native Linux receipt implementation is verified at `da35de5498d8edf2787c987bd5a45ea5aa39e988`,41/41, with independent actual Sol6.1/xhigh PASS. [Exact qualification and limits](recovery/worker-quarantine-receipt-qualification.json). This does not qualify process ownership or enable Linux Release.

## Linux ownership design boundary — T041-OWNERSHIP-DESIGN, 2026-10-05

The owner requested an analysis-only prerequisite from exact `31de2e33a6f44001fee9c0c3b6c4e61c8d3aab0e`: full-descendant ownership must exist before Release, and only a trustworthy complete-stop observation may permit artifact import or workspace cleanup. Process-group signaling, PID enumeration, direct-root exit, EOF and elapsed timeout are not complete-stop proofs. Keep Accepted/Uncertain/quarantine meanings unchanged and retain evidence while an old writer might remain. Parent/host/supervisor failure, setsid, double fork, concurrent creation during stop, ignored TERM, PID reuse and zombies must be analyzed explicitly. No application runtime change or production execution is authorized in this design block. Normal-permission isolated synthetic capability probes are allowed; actual denial must be recorded, not bypassed. No privileged service, permanent capability, cgroup, security-setting or credential setup.

The [design candidate and exact capability limits](linux-ownership-design.md) prefer a kernel PID namespace over subreaper-only proof. Creation capability is distinct from mapped UID/GID/proc compatibility and terminal teardown qualification; normal read-only mapping routes remain blocked. No production Release follows from this analysis.

## Detached workspace portability — T041-WORKSPACE, 2026-10-05

The owner authorized a bounded component from exact `b487526fc5f4d11a2c02816813f870f063e9fdc5`: create and stage pinned context plus task bytes into an isolated runtime directory outside canonical state; bounded proposal/contentRef byte reads; cancel, roll back partial creation and dispose only the owned workspace. Reuse the accepted #1553 trusted-local namespace/byte authority. No second journal or anti-owner save protection, and no changes to accepted load behavior. Preserve physical path/kind/link validation, exact pinned bytes, existing artifact limits and retryable cleanup. Windows retained-handle behavior remains source-compared and unqualified natively here.

Linux admits ordinary hardlinks for read and unlink-only cleanup without changing an outside alias; CreateNew never overwrites an existing target. Windows retains its existing single-link handle authority. This explicit component difference does not change canonical save semantics or promise protection against a hostile concurrent computer owner.

This does not launch a process, import/apply a proposal, publish a quarantine receipt, release a worker or qualify stop/ownership/process-tree/PTY. Production execution gates remain closed. Only isolated temporary synthetic fixtures may be mutated. Native permission refusal is evidence, never a reason to bypass environment controls. Independent actual gpt-6.1-sol/xhigh design and final reviews, causal RED/GREEN through the narrow catalog runner and remote/fresh-restore evidence are required.

## Worker environment portability — T041-ENV, 2026-10-05

Under US4/FR-012/015, preserve the existing caller-selected worker environment
through owner payload capture, strict Launch JSON and host ProcessStartInfo
reconstruction. Names follow the target local platform: Linux case-distinct names
remain distinct; Windows retains case-insensitive behavior. Values, spelling,
null/empty entries and snapshot independence retain the existing contract. Exact
JSON property duplicates remain rejected without payload/key/value diagnostics.
No filtering, normalization, new inheritance, secret policy or network/proxy
configuration is introduced. Cross-platform JSON is not a remote-launch feature.

The owner authorized this bounded continuation from
`9d5ffa0a3f513a89a9f4e4c89b0743bb2f4b5d6b`, retaining the sole writer and normal
non-force publication. Evidence uses only synthetic environment fixtures and
whitelisted metadata. Pure codec/reconstruction and an actual host reaching Ready
then owner close without Release are required. Native Windows and successful
worker execution remain separate; process-tree/workspace/PTY guards stay closed.
No GM-authored field, gameplay capability or prompt changes. Operational guidance
and its exact source/documentation guard must describe the environment boundary.

## Persistent-main run-record component — T041-RUN-RECORD, 2026-10-05

This bounded internal prerequisite implements US4/FR-014 record interpretation,
not persistent process execution. A record describes **only the persistent-main
ownership slot**. Missing or confirmed-stopped main evidence satisfies only that
slot's admission condition; concurrent worker ownership must independently be
composed before any whole-root mutation. The worker pool is not fenced by a
single main record. No production caller is wired in this component slice.

- Persisted identity binds normalized root key, run ID, current existing generation,
  positive session epoch, ownership backend, host instance and boot identity
- Malformed, absent and valid evidence are distinct; malformed bytes never authorize
  startup or mutation. Cold nonterminal evidence remains Uncertain
- Active same-run operations require exact current identity; second starts and
  quiescent mutation require missing or confirmed-stopped slot evidence
- Uncertain cannot become stopped or reusable from PID exit, EOF, timeout or status
  absence; only an exact identity-bound, trusted complete-scope stop or verified
  host-reboot observation can produce a retained terminal record
- The codec never authenticates an observation or acquires a filesystem/path grant.
  Pure and ordinary-file tests prove interpretation and decisions, not atomic
  persistence, power-loss/crash durability, canonical integration or native stop

Detailed schema and future consumer obligations are in [data-model.md](data-model.md)
and the current [plan](plan.md). Atomic record publication outside replaceable
state, all-writer composition, admission under held leases/actual mutations and
nonmutating diagnostic reads remain a separate connected prerequisite. This
client-owned prerequisite changes no GM-authored gameplay field or capability;
no gameplay prompt/example update is required until operational integration.

## Local load-filesystem continuation — revision 1, 2026-10-03

The owner requested completing the entire ordinary-load filesystem contour locally,
then returning a verified module to the cloud developer for the remaining game work.
Continue the existing implementation at `5d2aa2ceadd8f4424e3ccaf0249a8bb164f32fae`;
retain the accepted ordinary-save implementation and all source-specific evidence.
The current nine passing Load entry cases cover a first internal slice only. The
five review defects and unverified Shining Abode fixture correction remain open.

### Result and scope

Deliver the portable internal load operation together with its filesystem publication,
recovery and verification. This covers T032-B1, B2 and B3, plus the filesystem-specific
Windows/Linux qualification portion of B5. Add an explicitly selected native Linux
CI job so qualification does not depend on a Linux installation on the owner's PC.
CI is development infrastructure; local gameplay gains no remote dependency.

The downstream developer receives an operation with an explicit disposition, selected
source, established generation or absence of that knowledge, follow-up requirement,
continuation block and failure details. The downstream work is T032-B4 console/browser
integration and later live game/GM acceptance. Public Load cutover stays gated by the
filesystem prerequisites; this block does not itself close B4, full B5, T033 or #1553.

The earlier platform flag does not establish that archives or any specific operation
caused it. This task implements ordinary app-owned save processing and records test
results; finding or changing the platform's screening behavior is outside its scope.

### Preserved contracts and requirements

- **LOAD-FS-001 — Admission and names:** Validate the original ZIP, schema, manifest
  and hashes before applying the documented fixed-state-path mapping. Reject relative
  names that differ from their ordinal Trim; canonicalize only a finite, documented
  set of fixed runtime-consumed state paths, before destination collision, topology,
  extraction and settings/profile preparation. Qualify the eight paths named in the
  five-fix handoff and inspect other fixed-path consumers before full acceptance.
  Arbitrary payload names and bytes retain their meaning, including case-distinct
  Linux names. Retain current archive limits: 8,192 entries, 2 MiB total UTF-8 names,
  4 MiB manifest, 8 MiB Soul state, 64 MiB individual expanded payload and 512 MiB total
  expanded payload, plus the existing compression checks.
- **LOAD-FS-002 — Complete replacement:** Prepare private closed images outside the
  legacy load-transactions namespace. Publish the complete incoming/live replacement
  and generation through one B1 decision under lifecycle then replacement leases.
  Preserve the entire save library and the exact selected archive, including a source
  outside `saves/`; reject file and ancestor/descendant collisions with that source.
  Preserve generation fencing, accepted history, player choices and participating
  writer ownership. Resolve old evidence with its original handler or retain/block it.
- **LOAD-FS-003 — Truthful outcomes:** A recovery failure specifically encountered
  while acquiring replacement authority yields Uncertain with follow-up, blocked
  continuation and no established generation. A failed private preparation plus
  failed cleanup yields NotLoaded with follow-up; that private cleanup failure alone
  does not block canonical continuation. Preserve both causes and the owned residue.
  Committed establishes the replacement generation; RolledBack establishes the previous
  generation; Uncertain establishes neither. Uncertain always needs follow-up. Ordinary
  cancellation, lock competition and invalid input are not automatically uncertainty.
  Late refresh/release failures retain an already established publication outcome.
- **LOAD-FS-004 — Settings and runtime:** Decode and normalize archive or retained-live
  settings against a detached fresh baseline. If config is absent from the archive,
  preserve exact live config bytes; if both are absent, use standard defaults without
  creating config. Prepare canonical profile projection before publication. Refresh
  runtime only after confirmed commit; refuse same-root nested generation binding.
- **LOAD-FS-005 — Metadata capacity:** Remove the load-blocking aggregate 1 MiB v2
  metadata bottleneck while retaining strict frame, schema, duplicate, region and hash
  validation and existing v1/v2 readability. Stream encoded/decoded metadata with bounded
  per-token/member buffers; retain the logical inventory needed for complete validation,
  rather than accumulating a second whole encoded header. The old deletion inventory
  may be larger than the incoming archive and is not limited by its entry/name budget.
  Complete validation precedes recovery mutation.
- **LOAD-FS-006 — Files and directories:** Support valid file-to-directory and
  directory-to-file replacement, including empty and nonempty directory cases, within
  the same publication/recovery decision. Preserve exact prior files/absence and required
  directory structure on rollback; preserve committed replacements after interruption.
  Do not add an independent journal or perform unjournaled destructive pruning. Existing
  type/link checks and confinement protect outside data and source/library ancestors.
- **LOAD-FS-007 — Crash and resource proof:** Qualify actual preparation/publication/
  generation/commit/rollback/cleanup cuts and cold restart without private extraction
  sources. Unknown contents retain evidence and block unsafe continuation. Exercise
  64/128/near-512 MiB inputs, maximum entry/name metadata and independently larger old
  deletion inventories. Measure and record memory, disk, time and owned child cleanup;
  review category/resource bounds before launch and justify any adjustments. The promise
  is process-crash recovery, with no additional power-loss durability claim.
- **LOAD-FS-008 — Verification and delivery:** Run only affected, documented categories
  through the existing PowerShell 7 runner, with independent mutable fixtures and
  discovery-only catalog validation. Preserve causal RED/GREEN, exact source, OS,
  toolchain, counts and cleanup. Require native Windows and Linux filesystem evidence;
  an early-return OS guard is not native qualification. CI uses explicit category
  selections and uploads results. A separately configured Sol 6.1 XHigh reviewer checks
  each completed coherent block under the owner's 2026-10-03 strategy. Publish verified
  commits and restore final source/evidence from GitHub before handback.

### Acceptance and remaining decisions

The five demonstrated defects and corrected fixture have passing causal checks; the
connected operation satisfies LOAD-FS-001–008 on Windows and native Linux at recorded
revisions. Cold recovery preserves exact supported state/history/config and the complete
library/source; full-size and metadata envelopes pass their documented resource bounds.
Independent review has no remaining actionable correctness or missing-contract finding.
The handoff identifies the verified callable boundary and all remaining public-client,
live-GM and overall platform gates. No GM-authored game field or mechanic changes here;
storage/recovery documentation is updated, with no GM prompt/example capability change.

No new player-policy choice is required by this revision. The implementation of journaled
topology changes and streaming metadata needs concrete design/consistency checks under
the existing approved outcomes. If it requires a material format/contract change beyond
those outcomes, present that change for review before dependent implementation. Native
Linux acceptance requires actual execution; inability to run CI leaves that gate open.

## Local namespace design addendum — execution revision 1

This concrete engineering design implements approved LOAD-FS-005/006 and is presented
with [ordinary-load-plan.md](ordinary-load-plan.md) for written review before code.
It does not change archive, GM-authored state or public-client contracts.

New internal load publication uses a distinct v3 namespace frame in the existing
single B1 journal authority: the same intent, committed decision, generation and
recovery entry point. Existing v1/v2 evidence remains readable and existing publishers
retain their formats. v3 adds explicit Missing/Directory/File before/after nodes and
preserved library/source boundaries so a file-to-directory interruption is represented
without a second journal or destructive tree pruning. Exact schema, confinement and
stable scratch anchors are in [data-model.md](data-model.md) and the written plan.

Whole covered child inventories, including empty directories, must validate before
mutation. Pending recovery accepts declared before/after states and a missing gap
only for a recorded file/directory conversion; unknown contents retain evidence.
Protected library/source ancestors cannot be converted or removed. The library is
opaque; an in-session selected source is checked by length/hash without storing
another archive payload. External source access never becomes a cold-recovery grant.
Committed cleanup validates and preserves the complete after namespace; rollback
confirmation validates the complete before namespace. This addendum and execution plan revision 1 are authorized by the owner's later explicit
waiver of spec/plan/revision review; the earlier exact spec approval alone did not cover it.

## Public load integration — T032-B4 execution revision 1, 2026-10-04

Filesystem prerequisites are accepted at `7deb7c3e`. Expose the existing typed
`LoadGameWithOutcomeAsync` and its four-state result publicly, preserving the exact
selected source, established generation, follow-up and continuation flags. Replace
old `LoadGameAsync` internals with a thin commitment-only compatibility wrapper:
true for Committed even when follow-up blocks continuation; false for NotLoaded,
RolledBack or Uncertain. This bool is deliberately lossy and is never a safe-retry
or continuation decision. Console and browser player-facing consumers must use the
typed API exclusively; no active original Windows-only loading route remains.

The console binds refresh to the established replacement generation. Browser
replacement preserves pending-turn/UI-owner admission, exact typed transport on all
HTTP statuses, and confirmed identity through menu/required refresh failures. Both
frontend handlers distinguish non-loading, rollback, commit and uncertain/lost
responses, suppress repeated dispatch and stale/unmounted navigation, and stop
continuation until required reconciliation succeeds. No early runtime publication,
committed-to-failed rewriting or blind automatic load retry is permitted.

Implement in bounded public-entry, console and browser/handler blocks with causal
and affected consumer tests. Unchanged accepted filesystem/resource cohorts are not
repeated. Actual live console/GM remains T033; service/component proof alone
does not close those gates. Browser verification uses automated code-level client tests
under the owner amendment below, without a live-browser or visual-QA gate. This is client-owned lifecycle transport; GM-authored
fields/mechanics/examples are unchanged, while operational load guidance is updated.
The owner-authorized autonomous design/revision waiver applies; separate Sol 6.1
XHigh review remains mandatory. T032-A4-NATIVE-NAMES stays separately open.

## Verification-scope amendment — owner decision, 2026-10-04 15:45 UTC

The owner explicitly directed: browser verification is code-based through automated
tests; live runs will use the console client. This supersedes earlier live-browser
or deferred-browser-access requirements throughout this feature's active artifacts.
Do not make cloud live-browser access, another hostname/tunnel or a later browser
visual run an acceptance gate. Automated browser handler/component/transport tests
must still exercise the actual client paths, outcome text/state and interruption
boundaries; they are not claimed as visual QA or a live browser run. Live console,
real GM/provider, full gameplay/restart and applicable native Windows requirements
remain in force. Historical evidence keeps its original source and scope.

## User Scenarios & Testing

### US1 — Open either client and retain settings (P1)

A player starts a fresh local session in the console or browser, changes an ordinary setting, exits, then sees the same setting after restarting.

Acceptance:
1. Both clients on Linux open a usable main menu and create valid configuration and session generation
2. Setting changes survive a full process restart in each client
3. A second participating writer cannot interleave a transaction; stale-generation operations are refused
4. Invalid paths, existing symbolic links/reparse points, and directories in place of files fail without changing outside data

### US2 — Recover an interrupted save operation (P1)

A process crash during a supported write is recovered before the next writer or accepted game result.

Acceptance:
1. At every journal phase, restart yields exact previous bytes/absence unless a durable commit recorded the complete new state
2. A multi-file operation recovers as one declared member set, bound to its session generation
3. Unexpected current bytes produce a conflict; the journal and unknown data remain available
4. Committed cleanup never rolls back accepted data, including interruption during cleanup
5. A hard-linked destination is replaced by name rather than modified in place, leaving the other name's bytes untouched

### US3 — Play and save through the actual GM workflow (P2)

The Linux console completes the main game path with real GM input, then save/load and restart without losing accepted state. Equivalent browser actions and state transitions are verified through automated client/backend tests under the owner amendment above.

Acceptance:
1. Live console actions and automated actual browser-handler/component actions produce the expected player-visible results and accepted state; a build, static page, HTTP 200 or service-only test is insufficient. Browser test evidence is not visual QA or a live-browser claim.
2. Accepted-turn history, player choices, generation fencing and exactly-once resources/results are preserved
3. Save/load validates the complete staging set and restores the previous logical session on failure

### US4 — Keep one interactive GM session running (P2)

The bridge owns a persistent arbitrary interactive CLI. The daemon automatically sends text as human typing/pasting would, across multiple requests in the same session.

Acceptance:
1. The main GM remains interactive and persistent; one-shot stdin or manual copy/export is not an equivalent replacement
2. Manual input, automatic delivery, output/readiness, restart, cancellation and owned process cleanup use a common supported contract on Linux and Windows
3. Unknown termination outcome preserves workspace/slot and blocks unsafe reuse/rollback within the explicitly selected backend contract; native-lineage fallback has the owner-approved scope above
4. Real CLI evidence is required beyond controlled fixtures. Ordinary same-namespace descendants, including setsid/double-fork, belong to native-lineage scope; external delegation/unrestricted namespace migration are explicit limits, not universally claimed coverage

### US5 — Use other local system capabilities (P3)

Audio, clipboard, launcher and system helpers have working cross-platform behavior or an explicit user-visible capability result; silently skipping Linux is not completion.

### Edge cases

Empty files, non-UTF8 bytes, BOMs, absent files, case-distinct Linux names, duplicate journal entries/properties, stale generations, read-only files, interrupted staging/commit/cleanup, same-content replacements, unknown concurrent edits, symlink ancestors/dangling links, external hard links and failed process ownership recovery.

## Requirements

- **FR-001**: Use the same trusted-local-player storage contract on both OSes. Closed-game edits are allowed if the current schema remains valid; simultaneous external editing is unsupported
- **FR-002**: Preserve validated roots, exact permitted external paths, type/link checks, schema/GM-output/archive validation and non-following cleanup
- **FR-003**: Single-file writes stage complete flushed bytes in the destination filesystem, record intent and before-image/absence, publish atomically by name, record commit, then clean up. Create and replace remain distinct
- **FR-004**: Multi-file journals bind transaction members, generation, exact before-images/absence, expected result hashes and commit state; recover before new mutations
- **FR-005**: Recovery restores exact bytes/absence, not the same physical identity. Unknown contents cause evidence-preserving conflict; uncertainty is never success
- **FR-006**: Preserve one participating writer, current generation, accepted-turn atomicity, saved choices, immutable accepted history and once-only publication
- **FR-007**: Resolve old journals using their original handler before cutover. Unsupported legacy recovery blocks cutover and retains evidence; never reinterpret or silently delete an old journal
- **FR-008**: Explicitly support fresh roots and current-schema, quiescent roots with no unresolved old journal. Back up the existing state before any format conversion. Other historical saves are not promised compatibility
- **FR-009**: Do not require a privileged service, security-setting change, protection from the computer owner, synthetic physical FileIdentity, or Windows APIs activated merely by deleting IsWindows guards
- **FR-010**: Promise process-crash recovery. Do not claim power-loss durability without separately verified file/directory synchronization ordering
- **FR-011**: Both actual Linux clients must satisfy US1 and US3. Windows uses the same intended behavior; provide a reproducible owner-run checklist and report Windows coverage by the capabilities actually executed at their exact source revision, leaving all remaining capabilities explicitly unverified
- **FR-012**: Replace Windows-only process, IPC, bridge, daemon, launcher, audio and clipboard mechanisms with supported cross-platform behavior. Thin platform adapters are permissible where a native primitive is necessary, with equivalent declared behavior
- **FR-013**: Persistent arbitrary interactive CLI and automatic daemon text delivery are mandatory. No special game API is required of the CLI
- **FR-014**: Resolve ownership/cancellation/timeout/restart under the declared active backend scope above. Unknown outcomes retain workspace/quarantine/slot; no automatic result acceptance or unsafe reuse. Native-lineage may positively confirm its ordinary same-namespace lineage without proving absence of arbitrary external work; observable scope breach remains Uncertain
- **FR-015**: Run only affected categories via scripts/test-csharp.ps1, retain isolated mutable test state, publish source/checkpoint before lengthy tests or review, and independently review completed blocks

### Superseded storage guarantees

Approved LOCAL-FR-01 replaces physical-identity/adversarial-swap requirements in [#1500, Complete Actor Materialization](../1500-complete-actor-materialization/spec.md) FR-073 and FR-090–092 with schema, generation, content/journal authority and exact-byte rollback. LOCAL-FR-02 replaces platform restrictions in FR-095–096 with same-filesystem atomic name publication. LOCAL-FR-03 preserves FR-123 staging/session integrity without mandatory protective hard links or retained Win32 handles. LOCAL-FR-05 changes SC-029 storage acceptance accordingly. Related identity-only clauses are superseded to the same extent: in particular FR-081 does not prohibit replacing a hard-linked destination name when the other name's bytes remain untouched. Existing symbolic links/reparse points, invalid types and out-of-scope paths still fail; this is not permission to edit through a link or weaken logical accepted-state authority. The cited FR/SC numbers belong to #1500, not #1536; wound gameplay criteria are unchanged. LOCAL-FR-04's foreground process-group proposal is only a bounded candidate; later requirements cover actual provider lifecycle and persistent interactive CLI behavior.

### Key entities

- Validated local scope: permitted root or exact external file set
- Logical content authority: presence, exact bytes/hash, generation and operation ownership, distinct from physical FileIdentity
- Durable transaction: unique ID, format, member set, before-images, expected after hashes and commit state
- Participating writer lease: serializes application writers; not a sandbox against the owner
- Persistent GM session: terminal/process ownership and correlated automatic input across requests

## Success Criteria

- **SC-001**: Both Linux clients pass fresh startup, setting change and full restart scenarios
- **SC-002**: Every declared storage crash phase has a passing recovery/conflict test; no outside sentinel file changes
- **SC-003**: Both Linux clients complete the selected core game/GM/save/load path with inspected visible results and persisted accepted state
- **SC-004**: Real interactive CLI receives multiple automatic requests in one retained bridge session and has tested termination/uncertainty behavior
- **SC-005**: Each converted system capability has Linux evidence and a Windows owner-run check; no unimplemented Windows-only mechanism is presented as converted
- **SC-006**: Every completed block has a verified remote SHA, required targeted checks and independent review under the current owner-approved model strategy (Sol 6.1 XHigh from 2026-10-03); a fresh GitHub checkout restores source and instructions

## Verification Plan

Add small storage-path, publication/recovery and client-startup categories as their tests are introduced. Select existing canonical, session, browser/console and save/load consumers by actual changed contracts; split oversized mixed categories instead of running them indiscriminately. Validate category ownership by discovery only. Update tests/selection.json for each reviewed code block. No full-suite, Fast, PreMerge or sequential all-category run.

This specification itself changes no GM-authored game field. Storage/runtime documentation must explain supported editing/recovery. Later GM workflow changes require prompts, operational examples and guards in the same block. Linux console and browser evidence is separate; Windows checks are handed to the owner and not represented as locally passed.

## Clarifications and limits

The approved storage decisions are settled. The former GM design document is a proposal, not blanket approval; implementation must preserve FR-013 and resolve concrete lifecycle risks. New material behavior losses require direction rather than silent redesign. Final all-platform claims remain limited by actual Windows results.
