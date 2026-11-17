# Independent Mortal audit implementation plan

> For agentic workers: use superpowers:executing-plans with test-first steps and independent final review.

Goal: gate every candidate Mortal turn on an independent narrative/state review.
Architecture: immutable client-owned packet, strict response validation, read-only
Codex transport, existing accepted-turn/repair owner. No parallel state writer.
Tech stack: existing C#/.NET 8, PowerShell7, Codex CLI, System.Text.Json, xUnit.
Spec: [independent-audit-design.md](independent-audit-design.md). Issues #1515/#1516.

## Global constraints and review focus

No merge/close, afterlife change, legacy-save migration, hosted CI or raw logs in
Git. No regex semantic classifier, self-authored claim completeness assumption,
unsafe CLI bypass or stale approval reuse. All publication authority stays fresh.
Review especially late text changes, false-empty reports, consumed reports,
actual read isolation, and material changes after a previously approved packet.

## Work blocks

- [ ] T1: `Services/MortalTurnAuditContract.cs` and focused unit tests define
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

Branch starts at verified remote1516 `fa5d5ec8`; includes prior1515 receipt-path
fix but not1515 dedicated prose test file. That test is retained on1515 branch
`3e7f62aa` and must be brought into T3. Existing1516 omission is causal RED.
No audit code/modelcall has run yet. Shell push lacks credentials; connector
branch creation succeeded. Subsequent checkpoints use atomic connector commits.
Ruling: shared audit branch starts at1516 to preserve newer accepted-turn code;
1515's independent repro will be transferred without rewriting historical proofs.

## T1 in progress and design review

WIP scaffold/tests persisted as1a35e7def6d2a137c2e0ab0f5a08f24ad422a5a6.
Fresh Linux SDK10.0.404/PowerShell7.7.0 restore requires serial MSBuild here:
`dotnet restore BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --ignore-failed-sources --source /workspace/shared/toolchain/nuget --disable-parallel -m:1 -nr:false`.
Ordinary parallel restore failed with0warnings/0errors before execution; not RED.
Serial build passed96warnings/0errors in91.21s. Official category
`mortal-independent-audit-contract -NoBuild` then gave21/21 executed,18PASS/3FAIL
in9.4937495s, complete cleanup/no skips/duplicates/timeouts. The3failures are the
positive/negative-narrative and packet-identity requirements against the
always-hold/unbound scaffold; adverse replies already hold. Runtime gate absent.

Independent Astra High design review identified four important gaps beforeT3:
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

At44467f the narrow category passed22/22 in10.7664966s (build97.26s,
96warnings/0errors); clean fresh GitHub clone and connectivity fsck confirmed
exact44467f source. Independent Sol XHigh review nevertheless found a real
identity bug: JsonSerializer maps distinct ill-formed UTF-16 toU+FFFD. Six new
runtime-character regression cases failed as expected (28executed,22PASS/6FAIL,
10.0860799s, complete cleanup). Strict UTF-8 validation before serialization
now rejects those inputs rather than allowing digest collisions.

The review also caught duplicate full state parsing. State is now parsed once
per document and retained as detached JsonElement DATA for evidence resolution;
only referenced evidence values are converted for semantic JSON equality.
Pointer/duplicate-state/nested-field and remaining identity tests were added.
These corrections are WIP until the fresh selected run completes. Typed
semantic-failure/uncertainty/finding delivery belongs toT2; callers must never
reparse unvalidated raw replies or branch on human Error strings.
