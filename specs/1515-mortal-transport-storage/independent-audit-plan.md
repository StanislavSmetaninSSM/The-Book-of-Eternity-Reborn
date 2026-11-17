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
