# Independent Mortal audit implementation plan

> For agentic workers: use superpowers:executing-plans with test-first steps and independent final review.

Goal: gate every candidate Mortal turn on an independent narrative/state review.
Architecture: immutable client-owned packet, strict response validation, read-only
Codex transport, existing accepted-turn/repair owner. No parallel state writer.
Tech stack: existing C#/.NET 8, PowerShell 7, Codex CLI, System.Text.Json, xUnit.
Spec: [independent-audit-design.md](independent-audit-design.md). Issues #1515/#1516.

## Global constraints and review focus

No merge/close, afterlife change, legacy-save migration, hosted CI or raw logs in
Git. No regex semantic classifier, self-authored claim completeness assumption,
unsafe CLI bypass or stale approval reuse. All publication authority stays fresh.
Review especially late text changes, false-empty reports, consumed reports,
actual read isolation, and material changes after a previously approved packet.

## Work blocks

- [x] T1: `Services/MortalTurnAuditContract.cs` and focused unit tests define
  immutable identity/documents, digest, closed response schema, exact review
  coverage, quotes and packet-local JSON evidence resolution. Explicit unknown
  and uncertain results hold. Tests first; causal RED, then GREEN. This DATA
  contract alone does not enable the gate or prove semantic detection.
- [ ] T2: dedicated read-only Codex transport and prompt/schema, reusing existing
  process/owned-launch primitives where compatible. Typed async request/result;
  no inherited author conversation, disabled worker fallback or shell injection.
  Test launch args/timeout/cancel/malformed response. Run actual model on exact
  omission and negation pairs; record real cost/time and all failures.
- [ ] T3: integrate packet capture and fresh recheck at the existing accepted-turn
  boundary before cleanup, progression finalization/story and rollback disposal.
  Original DATA must come from authenticated pending snapshot, candidate from
  validated materialized owner; preserve consumed-report evidence. Add genuine
  engine failure/repair/quiet/valid/rollback/stale-result tests, not helper-only
  substitutes. Do not hold a canonical lease across model wait.
- [ ] T4: synchronize GM prompts, daemon repair and complete worked examples;
  add exact categories and intended-diff selection. Verify actual runner selection,
  executed counts and owned cleanup. Independent architecture/source/outcome
  review with concrete results, then remote checkpoint and clean restore.

## Checkpoint 2026-11-17

Branch starts at verified remote #1516 `fa5d5ec8`; includes prior #1515 receipt-path
fix but not1515 dedicated prose test file. That test is retained on #1515 branch
`3e7f62aa` and must be brought into T3. Existing #1516 omission is causal RED.
No audit code/modelcall has run yet. Shell push lacks credentials; connector
branch creation succeeded. Subsequent checkpoints use atomic connector commits.
Ruling: shared audit branch starts at1516 to preserve newer accepted-turn code;
#1515's independent repro will be transferred without rewriting historical proofs.

## T1 in progress and design review

WIP scaffold/tests persisted as 1a35e7def6d2a137c2e0ab0f5a08f24ad422a5a6.
Fresh Linux SDK 10.0.404/PowerShell 7.7.0 restore requires serial MSBuild here:
`dotnet restore BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --ignore-failed-sources --source /workspace/shared/toolchain/nuget --disable-parallel -m:1 -nr:false`.
Ordinary parallel restore failed with0 warnings/0 errors before execution; not RED.
Serial build passed96 warnings/0 errors in91.21s. Official category
`mortal-independent-audit-contract -NoBuild` then gave21/21 executed, 18 PASS / 3 FAIL
in 9.4937495 s, complete cleanup/no skips/duplicates/timeouts. The3failures are the
positive/negative-narrative and packet-identity requirements against the
always-hold/unbound scaffold; adverse replies already hold. Runtime gate absent.

Independent Astra High design review identified four important gaps before T3:
route-specific finalization, committed-joint recovery, enforceable capture
inventory, and state-origin findings/legitimate silence. Clarifications are in
the design. Transport no-tool isolation remains unresolved. T1 parsing tests are
not model quality tests. Current next step: finish T1 response parser and focused
verification, then transport decision/qualification and publisher route map.

Optimization: T1 handles immutable strings and parses state once per response
into an operation-local evidence index; no filesystem reads, global cache,
publication authority or speedup claim. Full packets must not be repeatedly
reconstructed around individual findings; T3 owns actual capture reuse/fences.

## T1 source review corrections pending verification

At44467f the narrow category passed 22/22 in 10.7664966 s (build 97.26 s,
96 warnings/0 errors); clean fresh GitHub clone and connectivity fsck confirmed
exact44467f source. Independent Sol XHigh review nevertheless found a real
identity bug: JsonSerializer maps distinct ill-formed UTF-16 to U+FFFD. Six new
runtime-character regression cases failed as expected (28 executed, 22 PASS / 6 FAIL,
10.0860799s, complete cleanup). Strict UTF-8 validation before serialization
now rejects those inputs rather than allowing digest collisions.

The review also caught duplicate full state parsing. State is now parsed once
per document and retained as detached JsonElement DATA for evidence resolution;
only referenced evidence values are converted for semantic JSON equality.
Pointer/duplicate-state/nested-field and remaining identity tests were added.
These corrections are WIP until the fresh selected run completes. Typed
semantic-failure/uncertainty/finding delivery belongs to T2; callers must never
reparse unvalidated raw replies or branch on human Error strings.

## T1 accepted bounded DATA block, 2026-11-17

Independent Sol XHigh correction/outcome review accepts production and tests
at 862f17b1ba71771e79d8d1001878cd1cf44d769d:42/42 PASS, 42 unique IDs,
10.7819795s official category wall, no skipped/duplicate/timed-out cases and
complete owned cleanup. Build 94.64 s,96 warnings,0 errors. Review verified the
source/TRX joins and both Important fixes, with no remaining T1 blockers.
This is only packet/response DATA validation. T2 / T3 / T4 remain unimplemented;
engine and real model behavior are unchanged and original semantic REDs remain.

A final test-only fixture clarification makes the quiet/negated example's
candidate vehicle state equal its original empty state, rather than inheriting
the unrelated positive birth fixture. Production is unchanged; that fixture
change receives a new focused run before its verification is claimed. No new
semantic inference guarantee is created by passing these unit tests.

The independent Astra owner-route investigation is retained in the design.
T3 uses existing actual settled contributions for ordinary routes and needs a
proven close-unattempted/rebuild-equality protocol for joint projected outcomes.
No canonical lease may span model wait, and committed recovery can only hold.

Next work: qualify a real no-tool transport (parent investigation ongoing), then
implement typed verdict/findings and the explicit route-specific capture/final
fences. T1 must not be wired as a Boolean substitute for those owners.

Final fixture clarification is verified at source
`c70bc9ffd3082e6ba2192223f9f91b8fbd2922ce`: official
`pwsh -NoProfile -File scripts/test-csharp.ps1 -Category mortal-independent-audit-contract -NoBuild`
executed 42/42 PASS in 10.4728750 s, one complete descriptor, 42 unique cases,
no skips/duplicates/timeouts, complete owned cleanup. Fresh serial unit build:
97.54 s, 96 existing warnings, 0 errors. Production is byte-identical to the
independently accepted `862f17b1` implementation. Only the honest quiet fixture
changed; no model run or engine gate has been introduced. Final checkpoint after
this note is documentation-only. No raw artifacts are staged or uploaded.

## T2 typed-response preparation, WIP

The next bounded block retains validated verdict/finding/evidence DATA for the
future repair consumer instead of forcing a second parse of raw model output or
branching on English error strings. Exact packet binding and immutable detached
evidence remain mandatory. Any malformed later finding discards the whole reply,
including earlier valid findings. Explicit uncertainty remains distinct from
semantic failure; adverse findings cannot hide behind a pass verdict.

No cache is needed: reuse the already parsed response/state within one validation
call and return detached read-only DATA. No source or authority reuse is added.
Tests and typed scaffold are WIP/UNRUN; model transport and engine gate remain
absent. Transport qualification continues separately with no credential copying.

The typed scaffold produced the expected causal RED: 47 executed, 43 PASS /
4 FAIL in 10.4112125 seconds, complete cleanup, no skips/duplicates/timeouts.
Build: 93.84 seconds, 96 existing warnings, zero errors. All three explicit
verdict cases and adverse-pass repair retention failed against the scaffold.
Implementation now returns only fully validated, detached read-only findings;
malformed responses retain no partial findings or packet binding. Effective
semantic failure overrides uncertainty; uncertainty overrides a claimed pass.
Fresh build/run and independent review are pending for this implementation.

## T2 typed response accepted; transport remains blocked, 2026-11-17

Source `72c174bc55c615901b2f93cf1639ca2c5dee6ca8` passed the official
`mortal-independent-audit-contract -NoBuild` category: 47/47 PASS, 47 unique IDs,
one descriptor, 12.0666261 seconds, no skips/duplicates/timeouts, complete runtime
and owned-tree cleanup. Fresh serial build: 99.61 seconds, 96 existing warnings,
zero errors. Independent Sol XHigh review verified the clean source/result join,
detached evidence, all-or-nothing reply delivery and verdict precedence; no
Critical/Important findings. This accepts only the typed-response block, not T2.

Minor deferred test coverage: add exact typed mapping assertions for missing,
uncertain, nonasserted, stateToNarrative and mixed failure/uncertainty when this
contract is extended. Existing Boolean negative coverage and new explicit
verdict/repair-retention tests pass; reviewed production mappings are correct.
No measured speedup is claimed. Intended-diff selection stays a T4 prerequisite.

Read-only Codex transport qualification has not reached model inference. The
installed app-server accepts ephemeral threads with environments=[] and no
dynamic tools in a clean profile; MCP inventory is empty, but account is null.
The ordinary profile fails before initialize because of read-only runtime state.
The documented sqlite_home override removes the SQLite-specific error but leaves
an unidentified read-only startup error; a log_dir override does not resolve it.
No credentials were copied, secret configuration changed, or security bypass used.
Empty MCP inventory alone is not proof of no built-in tools. Therefore no live
semantic result, transport isolation, cost or model latency is claimed. Do not
wire an unqualified generic worker or interpret missing transport as acceptance.

Next: qualify supported authenticated model transport, then the route-specific
T3 capture/repair/freshness work already mapped in the design. The original
prose-only engine failures remain open. No merge, closure or hosted CI dispatch.

## T3 bounded preview/rebuild design refinement, not execution evidence

Independent Astra review of the actual owner code found same-engine preview,
unattempted close and fresh rebuild feasible while the enclosing Main remains
live. The current publisher cannot simply be invoked twice: refusal retains
PublicationUncertain, and attempt expiry disposes the live narrative invocation.
A second invocation without fresh N would install a sticky narrative hold. Never
clear either real hold to make preview appear to work.

The next coherent lifecycle block is:
1. Extract the existing under-lease preparation/preflight unchanged, keeping all
   returned authority lease-local and shared with the original publisher.
2. Add an actual preview entry resolving its own authenticated current frame.
   Return only detached DATA after ClosedNoNativeAttempt, source closing checks,
   successful lease disposal and attempt expiry. A successful preview has its
   own CapturedAndClosed outcome, not PublicationUncertain; close failure holds.
3. Issue a fresh read-only observed narrative policy pass through the existing
   validator core (readOnly:true, source:source). Retain actual projection/source
   observations. Normalization-required sealed bytes are refused, never rewritten.
4. Recapture a new engine N invocation from the actual same frame, generation,
   live Main/Ready/stage and frozen interface iteration. Do not rearm FIRST/I,
   recreate Ready, reuse disposed N, or invent historical-resume admission.
5. Rebuild under fresh ownership and compare detached semantic candidate DATA
   before delegation/take. Fresh continuation identity deliberately misses the
   old prepared-stage authority cache; existing invalidation handles this.
6. Exercise the genuine JointNarrativeSource lifecycle fixture at a named
   checkpoint after Published I and before the canonical attempt: two distinct
   attempts, first expired/no publication, frozen I/consumed Ready preserved,
   equivalent candidate DATA, then one real publication. Changed source/owner,
   wrong iteration and failed close must hold without take. A two-preview-only
   result is not publication/readback proof.

Post-I fresh N is compatible with the existing exact-source check, which first
accepts expected==current and only otherwise permits authenticated I-before to
I-after. Resource mutation identities already use the joint deterministic seed;
ordinary random ID factories do not establish a defect in this route. No old
capability may cross the model wait. After the enclosing Main closes, only the
existing genuine resume route applies. BrowserLocalAccess is rejected by the
current unattempted-close owner and needs its own supported route, not a bypass.
No T3 implementation or lifecycle test has run at this checkpoint.


## T3 lifecycle test-first scaffold WIP

Added a named post-I/pre-attempt checkpoint and a genuine live-owner lifecycle
case. The preview API intentionally throws until implemented: no audit gate is
activated. The case asks for two closed previews, equal detached fingerprints,
then the original complete publication assertions and settled-plan equality.
Build and causal RED are pending. The narrow new category owns this case only.
Reuse: existing full genuine source-turn fixture, no shared mutable owner/cache.
Rebuild deliberately requires fresh authority; DATA equality is under test,
not asserted as a speedup. Model transport remains blocked separately.

The first execution at 5ba55cbc did not reach preview: 1/1 FAIL in
19.1443973 s, complete cleanup, no skips/duplicates/timeouts. Fresh build took
137.07 s, 171 warnings, zero errors. The genuine source fixture fails its own
canonical precondition because its newly seeded clinic map lacks the
current client-owned storage-access projection (even with no storages). This is not
a causal preview RED. The fixture now derives the projection from accepted map
metadata before original42, matching the existing living-world fixture; no GM
command, runtime validator relaxation or gameplay change. Reverification pending.

Independent Sol XHigh source review accepted the pre-original projection seed
and identified two missing mandatory npcSkillClaims arrays in the actual
source42/Stage43 debug authors. The ed67f52c run reached a real source42 repair
request containing exactly npc_skill_claim_report_invalid, then waited for a
repair the fixture does not author. It was deliberately interrupted (exit130),
not a preview RED/PASS; final runner-owned cleanup receipt is unavailable and
raw evidence is retained. Its fresh build passed171 warnings/zero errors in
124.69s. Both no-NPC-use authors now explicitly declare []; corrected Stage43
resubmission already copies that initial debug output. No validator is relaxed.
The reviewer found no other concrete schema gap on this bounded path. Next
execution still has to reach the intentional preview scaffold refusal.

## T3 first shared preparation/closed-preview implementation WIP

At ef4d7d8a, the genuine source42 completed and Stage43 reached the post-I
checkpoint. Engine diagnostics confirm the intentional preview NotSupported
exception, with publication suppressed. Run: 1 executed / 1 FAIL, 35.8911401 s,
no skips/duplicates/timeout, complete owned cleanup. A subsequent old fixture
byte assertion masks that exception; path-specific mismatch diagnostics now
retain its exact expected/actual bytes locally rather than weakening it.

WIP implementation shares the original preparation/preflight with publication.
Preview creates real ownership, copies full semantic afterimages and observed
read-only narrative DATA, checks source closure, requires ClosedNoNativeAttempt,
and returns only after lease disposal/attempt expiry. A fresh read-only observed
N invocation uses the actual current Main/Ready/I/frame; no normalization or
Ready replay. Later preview and publication compare DATA before take. Existing
normal publication remains unactivated unless an explicit preview was captured.
The proof and negative cases remain UNVERIFIED; no live model gate is installed.
Reuse remains operation-local; authority cache misses across fresh continuations
are deliberate. Shared preflight avoids a parallel preparation algorithm.

## T3 first runtime result and concrete existing-owner blocker

At ecc99fda the first preview implementation compiled (172 warnings, zero errors,
121.71 s). Its genuine live test failed1/1 in44.6260421s with complete cleanup,
no skips/duplicates/timeout. It reached full common preparation but refused
before the first preview close: current progression_schedule differs from its
signed original solely in lastUpdatedUtc. Existing fixture dispatch assertions
independently detect the same mismatch before preview. The original signed
schedule is later than the restored current schedule.

The exact existing fr063-joint-live-n-source-unchanged control, with NO preview,
also failed1/1 in57.0183571s with complete cleanup/no duplicates/skips/timeout,
at the same progression-source equality gate. This is not a model result or
successful preview. Stage creates its rollback backup before BuildControlForNextTurn
updates schedule; original capture occurs afterward. The legitimate original vs
rollback distinction needs a narrowly justified owner correction, not ignoring
timestamps or changing the signed original. Separately, joint outcome capture
samples UtcNow for each candidate, so fresh rebuild determinism needs existing
frozen DATA reuse under fresh authority. Targeted independent analysis is pending.

Independent Astra source review found no confirmed critical/important production
defect in this initial preview diff, but correctly requires changed-source/owner/
iteration/failed-close/normalization-required negatives before acceptance. A
positive semantic readback comparison is also pending. None are claimed covered.
No broad activation, live model inference, merge or closure occurred.

## Existing joint resume schedule correction WIP

Targeted Astra analysis confirmed an explicit existing distinction: Stage signs
prepared schedule after backup (TurnLifecycle RegisterOrdinaryTurnStagingValidationSnapshotFiles),
while cancellation retains the earlier rollback image. Existing diagnosis retry
resumes the signed prepared schedule atomically with held command/pending. Joint
restore omitted that third resume override. The narrow correction reads the
same authenticated original once with observed evidence, joins exact tuple/realm,
and includes its prepared schedule in the existing compensatable restore cohort.
Original evidence is freshly checked before batch and after readback. Rollback
metadata, backup bytes, hashes and signed artifacts remain untouched. No clock
sampling, arithmetic or fresh schedule normalization is added. Existing live-N
control is the causal regression; re-run pending before altering candidate-time
reuse. The independent reviewer is checking this exact correction.

## Prepared schedule correction: runtime control verified

At 46a3e173, independent Astra High exact-diff review found no blocker in the
schedule resume correction. Serial integration build passed 0 errors / 129
warnings in 76.80 s. The unchanged real live-N consumer with NO preview then
passed 1/1, no skips/duplicates/timeout, complete runner-owned cleanup, wall
207.1874982 s. Command: `TMPDIR=/workspace/shared/mortal-audit-tmp pwsh -NoProfile
-File scripts/test-csharp.ps1 -Category fr063-joint-live-n-source-unchanged -NoBuild`.
The standard /tmp attempt had failed earlier before repair dispatch with ENOSPC;
a concurrent recovery clone had temporarily filled that tmpfs. It is not a code
RED or proof of correction. Earlier concurrent compilation ended csc 137; its
retry was intentionally stopped (130), then builds were serialized. No foreign
files were removed. Raw evidence stays local. Fresh recovery checkout is clean
at 46a3e173; download verification does not substitute for these runtime checks.

This validates the narrow prepared/rollback schedule role correction, not the
audit lifecycle. Targeted reviewer corrected an earlier missed Important issue:
each progression capture samples UTC anew, so repeated candidate preparation is
not deterministic. Next run is the existing preview lifecycle to isolate that
remaining failure before implementation. The proposed fix retains only detached
progression calculation JSON DATA, checks exact sources/control/tuple/generation,
and constructs new service inputs and engine authority under each fresh lease.
No retained lease-bound object, new clock, normalized comparison, authority cache
or full feature acceptance is intended. Changed inputs must reject old DATA.

## First preview terminal-prefix fence correction WIP

The preview lifecycle on unchanged 46a3 source (checkpoint2cc1) executed 1/1 FAIL
in81.1955878s, complete owned cleanup/no skips/duplicates/timeout. It reached full
preflight, then FIRST preview failed `debug_prefix_attempt_phase_unavailable`:
its final source recheck incorrectly called the early PreparationStarted/stages0
API after all six stages were retained. Thus no second-preview clock result is
claimed. A separate preflight recheck now requires selected current live owner,
unchanged bindings, no delegation/close, V2CommonSealed, six actual stages and
reference equality to the retained preflight. It rechecks existing detached prefix
DATA before and after the await. The old early phase guard is unchanged; no new
prefix evaluation or relaxation for arbitrary stages. Shared binding checks are
factored, not removed. This correction is UNBUILT/UNRUN, narrow independent review
assigned by parent because earlier reviewer followup hit the agent-thread limit.
Clock DATA rebinding and negative lifecycle tests remain pending. No model gate.

Narrow independent source review found no blocking phase defect; it requested
avoiding duplicate early owner checks introduced by extraction. Those checks now
run once at each entry; common context fences remain identical. New terminal
negative coverage is still required (Common-only, foreign preflight, closed or
delegated owner and source drift); existing early-consumption negatives do not
claim this coverage. No test result is inferred from source review.

## Preview source-operation completion ordering WIP

At d4ed557b integration build passed0errors/172warnings/131.52s. Preview executed
1/1FAIL79.0148770s, complete owned cleanup/no skips/duplicates/timeout. It passed
the new terminal prefix fence, then first unattempted close was unavailable.
The implementation incorrectly completed SourceDataOperation BEFORE registry
close, although close performs fresh source reads and Complete makes RequireOpen
throw. Existing publisher closes before completion. Minimal correction moves
source completion after verified ClosedNoNativeAttempt and before lease release;
a completion failure still prevents returning preview DATA. Refusal diagnostics
now include the actual close disposition rather than hiding it. No registry
condition, rollback or close guarantee changed. This ordering correction is
UNBUILT/UNRUN; independent review is checking the source explanation. Repeated
clock capture is still a separate unimplemented issue, not a reached second
preview failure yet.
