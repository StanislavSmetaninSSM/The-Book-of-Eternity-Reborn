# #1515/#1589 physical-lease observation: bounded qualification

## Goal and limit
The accepted mm23 capture proves terminal polling exhausted its existing 200 open attempts (199×50ms delays), but cannot identify one sustained holder versus repeated holders. Main-operation close markers do not show physical lease intervals; the canonical lock file is empty. Do not change retries, timeout, exceptions, owner admission or mm23. A known-holder test alone is redundant: the deliverable is the missing opt-in physical interval evidence, qualified in that same existing contention fixture before any further live decision.

## Existing source seams and finite change list
- `Core/FileSystemManager.cs`: `AcquireCanonicalWriteLeaseCoreAsync` physical constructor at4795, successful-open postvalidation catch4827–4841, three real successful lease-return branches (MainMetadata, SessionFinalization, ordinary), typed exhaustion4817, and `CanonicalWriteLease.DisposeAsync` actual `stream.DisposeAsync` around482. Preserve all existing control flow and disposal behavior.
- `Services/GmRuntime/MainOperationFailureDiagnostic.cs`: reuse its DEBUG-only `ReadNonce()` and best-effort stderr pattern gated by exact valid `BOE_MAIN_STOP_DIAGNOSTIC_NONCE`; add a small typed physical-lease observation/write method, not another logger/service/process abstraction. No file or canonical-state writes.
- `Core/FileSystemManager.MainRunFence.cs` only if needed for a read-only snapshot of already held admission tuple; never call DescribeClose to manufacture a terminal outcome, never reread/adopt main.json. Existing `_access`/retained original/remote data are the sole sources. Where a tuple is absent or unavailable, emit null and explain that limit.
- Existing `GameEngineTurnLifecycleTests.TerminalWaitContention.cs`, its current category `mortal-terminal-wait-contention`, and a small pure serializer/noninterference control in the same fixture/category. No new world/provider/relay/native-host framework.

## Observation contract
Capture one diagnostic-only occurrence per actual acquisition call when a valid nonce exists. Use process-local monotonic sequence plus PID, normalized root, requested purpose, UTC capture time and Stopwatch timestamp; optionally copy the existing full original operation/pin/close-ID/owner tuple as immutable DATA, never authority. Same occurrence follows its stream through disposal. No request/body/file content or secrets, no global retention/cache.

Emit these finite facts through the existing stderr channel:
1. `physical-opened` immediately after the successful FileStream constructor, BEFORE subsequent handle/path validation. This is physical possession, not a granted canonical lease.
2. `lease-granted` only at each existing successful return after its existing checks.
3. `physical-dispose-completed` only after actual stream DisposeAsync returns successfully, both pre-lease validation cleanup and ordinary lease disposal. Preserve the occurrence captured at open; do not invent a close on attempted/failed disposal. A separate `physical-dispose-failed` may record exception type only and leaves completion UNKNOWN.
4. `contention-exhausted` at the existing final supported sharing-failure branch, before throwing the exact same typed exception. Record observed attempt count, not an inferred holder. Optional first-contention event can establish the interval without per-attempt noise; no diagnostic record quota or sampling that loses lifecycle facts.

No-nonce/release builds remain no-op without JSON/timer/event allocations. A nonce change refuses emission; a failed diagnostic sink/serialization cannot change return, exception identity, retry count or cleanup. Logging may observe transitions but never authorizes retry, release or admission. Avoid blocking/deferred background writers or another channel lifecycle.

## Test-first within one coherent block
Extend the CURRENT three modes rather than execute a separate redundant known-holder project. Use the existing actual physical contention hooks and original independently opened raw canonical FileStream holder. A second public lease is unsuitable here because its outer gm-main-owner guard blocks the waiter before canonical acquisition. Release the raw handle only at first actual contention (released mode) or owned finally (exhausted mode), with no sleep as causality. Its actual open/dispose timestamps are separate test receipts, NOT BOE events or authority; assert released timestamp precedes waiter physical-open. Production waiter events are released1open/1grant/1close, exhausted0open/0grant/0close plus1exhaustion, integrity1open/0grant/1close. Existing five public-lease tests independently qualify actual granted-lease disposal. For exhausted mode retain unchanged retry window and exact typed failure, then release/join in owned teardown. For post-open-integrity use the existing sentinel: opened then successful physical cleanup but NO grant, same original sentinel identity.

Initial causal assertions require missing events AFTER the existing actual result/cleanup assertions. No nonexistent API should cause the RED. Add bounded pure output tests for missing/wrong nonce, malformed optional tuple refusal, throwing writer/serializer and exact exception/result preservation. Actual lifecycle cases verify one completion per real stream despite repeated Dispose, no positive completion on a controlled close failure if an existing seam can express it without replacing real disposal. If no such seam exists, verify failed-write/noninterference separately and do not pretend a synthetic close is actual OS-close proof.

Then implement only the observer plumbing, independent SOURCE review, fresh owning build and the one explicitly counted existing contention category; no broad suite. RED→GREEN is one test-first block, not sequential standalone projects for known mechanics and later observability. Existing invariant snapshots (request/ready/weather), original waiter joined, fresh subsequent lease and cleanup remain mandatory. Do not run before the design/source gates.

## Sufficient conclusions
An isolated GREEN proves emitted intervals correspond to real acquisition/grant/release and controlled competition, with noninterference and truthful absence on failures. It does NOT identify the historical mm23 holder or prove the normal retry policy wrong. It enables a later separately reviewed capture to distinguish actual competing processes/operations; any attribution still requires overlapping open→completed-dispose facts for that run. Missing events/sink failure/absent tuple remain UNKNOWN. No mm24 or live attempt is authorized by this plan.

## Accepted design refinements and initial test source
Same occurrence is captured before constructor and follows successful open through pre-grant failure cleanup and all three grant branches. Capture transition timestamps before calling the sink; physical-dispose completion precedes parent/main cleanup and stays true even if later cleanup fails. Existing nonce checks remain entirely no-throw/read-only. Initial test source extends existing3 lifecycle modes and adds5 actual-acquire nonce/sink/repeated-dispose cases (official8 total); production is unchanged. Missing physical events after otherwise proved actual results are intended RED. Direct typed serializer malformed-tuple controls are required before GREEN once its concrete typed API exists, not a missing-API compile failure in RED. No live/world run.

Initial expected RED is six failing cases/two passing missing-or-malformed nonce cases, not eight failures. Controlled wire assertions inspect ALL emitted physical rows: no nonce filtering may hide resealed disposal after nonce change; each grant joins its own opened/completed-dispose occurrence with ordered transition timestamps.

Corrected source follows independently verified partialRED6961749f: raw-holder restoration fixes only the fixture admission mismatch. Production remains unchanged; next exact8 run must still fail six missing-observer cases with two absence controls passing after original behavior and cleanup. No new lock or owner API.

## Corrected RED accepted; instrumentation WIP
Independent review accepted corrected outcome e9060ab5: fresh Integration56.53s (coarse shell57s),134warnings/0errors; official exact8 in22.7716963s gave6 causal missing-observer failures and2 absence passes. All original contention/result/cleanup/fresh-lease invariants preceded these failures; no setup errors. Earlier partialRED6961749f remains distinct. Source32064 and8 outputs joined.

Current unrun implementation captures one immutable context per physical acquisition and passes it to the actual lease; all3 grant branches and both actual disposal sites report the same occurrence. Remote tuple is the already held immutable original handshake, local tuple absent because local OperationPin has no protocol identifiers; no DescribeClose, reads, connection, authority or lock-policy change. Added typed malformed-tuple/nonce/stage/wire/throwing-sink controls inside existing repeated-dispose case, keeping exact8. Synchronous stderr overhead is unmeasured and can block; only exception/policy noninterference is claimed. No asynchronous logger or cache: per-acquisition immutable DATA reuse ends with the actual lease, timestamps/current nonce remain fresh. No-nonce path avoids context/JSON/timer allocations. Source review and fresh owning Integration GREEN remain required; no historical holder attribution or live/mm24 permission.

Before GREEN, two compile-only setup failures were preserved: missing file-scoped alias (7.15s), then mistaken local OperationPin.Reply reference (32.92s). No tests ran. The latter confused private RemotePin protocol mapping with the local reference-counted pin. Source reassessment removed the unavailable projection rather than inventing IDs or looking up another owner. Remote original grant remains held immutable DATA; local OriginalPin is explicitly null. Independent full added-symbol audit precedes the next build.

## Current result: bounded GREEN independently accepted
Outcome d662a484 independently verified all19 artifacts and exact32064 source/8output joins. Fresh owning Integration113.50s (shell114),182warnings/0errors; official PlanOnly8 and actual8/8PASS in25.0551318s, no timeout, complete owned runner cleanup. Released waiter emitted same occurrence open→grant→completed-dispose at attempt2 after actual raw-holder release; post-open sentinel emitted open→completed-dispose/no grant; exhausted waiter emitted only exhaustion at attempt200. Original behavior, task closure and fresh subsequent lease passed. Missing/malformed/changed nonce, throwing sink, repeated disposal and typed wire/tuple controls passed. Two earlier compile-only setup failures remain distinct.

This qualifies finite local physical interval observation and exception/policy noninterference, not remote-owner lifecycle or exceptional OS-disposal completion. Authored remote tuple serialization is DATA only. Synchronous stderr overhead has not been isolated; whole-category timing is not a speedup measurement or nonblocking guarantee. Historical mm23 holder remains UNKNOWN. No live/mm24 run, prior-world repair or lock-policy change is authorized. Future clean packages must include the actual new owning compiled outputs and sources; old immutable runtime packages must not be patched/reused as current.

## Actual remote-helper qualification follow-up: fixture SOURCE WIP
Reuse existing `terminal-main-operation-helper-snapshot-stopping` only. Its pre-Stop actual canonical write already acquires a SessionMutation lease under the retained remote helper; initial generation capture may acquire another, so do not demand one total mutation. Driver sets a fresh nonce only in that PowerShell child's environment. Fixture captures real helper PID and before/after-write monotonic timestamps while alive, then after actual Dispose requires both reply/stderr joins and validates ALL physical rows from retained stderr against nonce/PID/root/full Active original grant. Each occurrence requires unique ordered open/grant/dispose; at least one cycle lies within actual pre-Stop write bounds. Existing same-endpoint snapshot-end refusal/Failed exact close/durable Stop/guardian remain unchanged. No production/runtime/world changes.

Fresh owning Unit/TestSupport and exact official existing regression3 (one native plus two tiny PS compatibility cases) required after independent source review. Native15s/outer20s unchanged; no local8 rerun. Full source/compiled-output/native/package joins must be current, never old immutable runtime substitution. This follow-up is UNRUN and does not authorize live/mm24.

## Actual remote-helper result: independently accepted
Outcome374126e6 verified all36 artifacts, source32064/owning8 output joins and6 actual producer DLL/PDB/native/compiler joins. Fresh Unit/TestSupport39.63s/70warnings/0errors; exact official3/3PASS15.3691565s. Actual helper emitted9 physical events: separate generation, actual write and finalization triples, all same full Active original grant/nonce/PID. Write occurrence2 is strictly bounded by the real pre-Stop write timestamps, not the generation cycle. Same helper then refused actual snapshot-end after Stop and settled exact Failed close with both reply/stderr joined; original owner Stopped/OwnedScopeEmpty and guardian complete/no emergency/failure/deadline. Raw UTC bytes remain intact; PowerShell derived row timestamps reserialize at local offset with the same instant/fraction, not identical text.

This closes the bounded actual remote projection/physical lifecycle gate only. Local8 were not rerun. Historical mm23 holder remains UNKNOWN; full live scheduling/P01/P02/P03 and exceptional OS disposal failure remain unqualified. No new live launch or prior-world reuse is authorized. Future preparation must use current compiled production, not old immutable producers with only PS overlays.

## 2026-11-19: explicit physical-only routing, test-first WIP

Tracked #1589/#1515. Both independent design reviews accepted the closed DEBUG
contract: physical observation defaults disabled; exact `stderr` is for controlled
fixtures only and requires the external path to be absent; exact `external`
requires an explicitly owned private external directory and matching 32-byte
`owner-nonce`. Unknown/empty/malformed or changed route/path/nonce suppresses
physical output without fallback. Existing failure/stop stderr is unchanged.

The initial test-only block preserves production bytes and adds twenty real
public-lease routing cases to the existing eight-case local category (28 planned).
Existing local and native fixtures now explicitly request stderr. Cases cover
default/unknown route, valid stderr/external, rejected path/marker/privacy/link
forms, route/path drift, actual disposal and unchanged failure channel. All are
UNRUN pending saved source review. Initial RED cannot claim later negative
assertions ran after its first failing assertion. Before GREEN the concrete sink
API also needs explicit empty-value DATA (environment setters may remove empty
values), special entry/collision/partial failure controls and fresh marker drift;
these must not be silently claimed from this initial matrix.

Implementation remains gated on causal RED. Use only an immutable per-acquisition
route/path/nonce DATA record and fresh per-emission validation after the existing
transition timestamp. TrustedLocalFileScope is ordinary namespace DATA, not a
retained authority handle; require ValidateDirectory(false), concrete private
Unix directory/marker/event modes and no links/special entries or canonical-root
overlap in either direction. Observer creates no directory or marker. Each event
is a private CreateNew PID/occurrence/closed-stage file, written and closed
synchronously; collision/partial failure is retained UNKNOWN with no retry,
repair, deletion or stderr fallback. No registry/cache/worker/async logger.

Optimization: reuse immutable occurrence/route identity only during its lease;
mutable route/marker/path checks remain fresh per emission. Synchronous IO may
block and perturb lock timing; promise exception/policy noninterference, not
timing neutrality or exhaustive holder coverage. A completely dropped occurrence
is undetectable. Measure observed event count/bytes and whole inert elapsed.

After reviewed local GREEN: existing remote3 with explicit child stderr, then
ONE existing fullroute1 external same-PTY gate under unchanged45/55/60. Require
actual foreground PID intervals, zero physical prefix in raw/emulator/cells,
physical raw exactly equal to ordered emulator WRITE bytes and normal owned
cleanup. No mainwire run, no live world, no mm24 preparation yet. Fresh owning
Integration and Unit/Support outputs are required after production changes.
