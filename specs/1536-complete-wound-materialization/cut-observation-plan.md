# Composed Cut Observation Implementation Plan

> For agentic workers: use the executing-plans workflow for each bounded source stage; independent SOURCE and OUTCOME gates are mandatory.

**Goal:** Prove cut-only invalidation on the genuine materialization path, then independently prove complete post-materialization cold/warm behavior, without inventing a separate cut authority.

**Architecture:** One capture-bound, one-shot diagnostic request uses the existing MaterializeWoundAsync transaction. It may warm ordinary DATA after selection and before speculation, then records only cheap detached facts around the real cut. No cold query or committed export occurs inside speculation. The existing owner method completes preparation, insertion, routing and commit normally.

**Tech stack:** C#/.NET 8 application, SDK 10, xUnit, official PowerShell category runner. Existing #1536 Spec Kit feature. Design: cut-observation-design.md; composed-proof choice accepted, exact old oracle remains HOLD.

## Constraints and risk focus

- No raw draft/scope/clock/preparation capability or arbitrary callback escapes. No standalone cut method, no nested scope support, no early commit, no guard relaxation.
- Diagnostic request binds exact current capture, lease and admission. Duplicate, foreign, stale and disposed requests fail closed without mutation or arming another owner.
- Disabled path must do no extra work. Diagnostic errors or unavailable observations must remain explicit and must not manufacture a valid report or silently swallow owner failures.
- Report only detached immutable DATA; no live authority. Preserve same-owner exact retry, no repeated allocation, cleanup and post-mutation revocation.
- Keep new/touched tests typed and reflection-free. Do not migrate the legacy candidate-only corruptBefore fault injection or claim its coverage here.
- Tiny scalar observation needs no cache. Reuse the existing ordinary computation and its fresh input/dependency checks; independent mutable fixtures remain fresh. No speedup claim.

## One cohesive implementation block

Affected code: new ValidationService.SpiritualWoundCutObservation.cs partial; narrow hooks in SpiritualOriginalTurnMaterialization.cs; private under-gate extraction in SpiritualExistingPressureOrdinary.cs only where needed to reuse identical checks without semaphore reentry. Prefer a private retained witness to exposing any mechanics owner. New integration partial AfterlifeResourceCutoverTests.SpiritualPressureCutObservation.cs; minimal shared-fixture parameter distinguishing materialization from direct cut. Existing legacy HOLD method bodies remain intact.

- [x] Step 1. Write one typed composed scenario first. Minimal compileable observation surface may return no observation for RED; do not implement stage recording yet. Drive existing OwnApSpent fixture through normal materialization, requiring a non-null report and the complete intended invariants. Independent SOURCE gate, fresh owning build, official exact plan and runtime must fail specifically at missing observation. Do not call compile errors causal RED.
- [x] Step 2. Implement an exact-admission one-shot request and detached historical report. Warm ordinary DATA only after SelectCore refresh and before BeginSpeculation. Capture the actual warmed mechanics witness or a precisely justified equivalent production predicate, asserting unchanged owner/ordinal/source/routing/wound identity and cut version change. After cut, inspect currentness/stale-read only; no full probe, cold compute or export. Keep transaction/failure behavior unchanged.
- [x] Step 3. Verify the composed Fact: literal before/after versions, cut-specific stale witness and exact stale diagnostic; genuine cold then zero-work warm before cut; no actual-evaluation work caused by ordinary queries; fresh fences; materialized rank/roots/history; complete post-materialization cold/warm pair; exact retry and unchanged byte/generation/publication state. This remains explicitly distinct from an isolated post-cut cold query.
- [x] Step 4. Add the necessary request/report boundary negatives and a disabled-path control. Select tests by affected contract, including representative existing ordinary behavior and the existing normal materialization lifecycle. Do not rerun all prior passing cases. Confirm no report authority/mutable aliasing, stale/foreign/duplicate/disposed handling and failure/unavailable semantics at supported boundaries. If safe post-mutation failure coverage needs a new generic fault hook, stop and report instead of expanding scope.
- [x] Step 5. Independent SOURCE review of final code, tests, catalog and exact selection before fresh owning builds. Verify current DLL/PDB/source provenance, official catalog and PlanOnly counts; run bounded exact categories. Preserve all failed attempts and actual completed counts/cleanup. Obtain independent OUTCOME review, update this feature's compact checkpoint, and save selected evidence in Space with exact readback/hash. No staging, commit, push or publication.

## Verification discipline

Use fresh normal-reference builds, --no-restore, disabled build servers/shared compilation, then official scripts/test-csharp.ps1 -NoBuild with exact categories/selection. Protect each small integration category with a finite budget based on actual cases. Never run full suite or all-category aggregates. The existing ordinary selection remains unchanged. Preserve all source/output pins and existing diagnostics; runtime artifacts live outside Git. After two unsuccessful fixes of the same issue, revisit the hypothesis before another attempt.

## RED checkpoint, 2026-11-08

Independent SOURCE and OUTCOME accepted the one-case causal RED. A fresh normal-reference integration build succeeded (179.813 seconds, 217 warnings, zero errors). Six DLL/PDB modules and 3,091 source documents joined; 31,561 source and 1,612 output hashes remained unchanged. Catalog discovery and the exact one-case plan succeeded. Runtime executed one case and failed only at the missing observation assertion after real OwnApSpent materialization (517.6781 ms). No skip, duplicate or timeout; owned cleanup complete, with temporary roots empty before and after. This is not GREEN or closure of any legacy HOLD.

## Bounded GREEN checkpoint, 2026-11-08

Independent SOURCE and OUTCOME accepted the distinct composed proof: nine of nine
exact cases passed in four completed descriptors, with no skips, duplicates or
timeouts and complete owned/runtime cleanup. A causal one-case RED first failed
only at the missing observation after lawful materialization. The source keeps
the actual warmed ordinary context private and observes only scalar/currentness
and stale-read facts across the real cut. Complete post-materialization cold/warm
remains a separate lifecycle proof, not the old isolated post-cut query.

Fresh normal-reference build succeeded (175.761 seconds, 217 warnings, zero errors).
Six DLL/PDB modules and 3,094 source documents joined; 31,565 source and 1,612 output
pins stayed unchanged through runtime. Catalog 1,557 / 13,665; exact plan 4 / 9.
Composed body 7.374 seconds; observed materialization 978.4924 ms; post-operation
cold/warm 181.1783 / 106.3417 ms. Runner wall 74.052 seconds. No speedup claim.

One-shot request/report boundaries, unavailable comparison with successful
materialization, genuine stale/foreign/disposed owners and unchanged owner
exceptions passed. The disabled path performs no extra ordinary computations.
Exact retry retains fresh source validation (+1 actual, zero ordinary work/new
allocation); no source guard was weakened to obtain a zero counter. Scalar
historical DATA needs no new cache; ordinary work uses its existing cache and
fresh input/dependency fences. Independent mutable fixtures remain separate.

Old OwnApSpent and four direct-cut HOLDs are still open and their exact source
remains unchanged. Post-mutation fault injection and busy-source-gate behavior
were not tested. No synthetic lineage is called native, no full 115-case rerun,
Windows, CI or publication qualification. This client-owned diagnostic surface
changes no GM-authored gameplay/schema/pending/receipt contract, so GM prompts
and examples need no update. Selective diagnostics saved in Space: 13,289,435 bytes / 119 files,
exact byte/hash readback verified; SHA256 92460ff8e913718318b0c57df8bb8579c2372c59a7f397684289822a0139de1b.

