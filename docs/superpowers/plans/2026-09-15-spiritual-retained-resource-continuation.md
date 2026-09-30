# Spiritual Retained Resource Continuation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development to implement and review this bounded part of C2. Steps use checkbox syntax for tracking.

**Goal:** Resume a real bounded-resource wait on the same retained execution owner, preserving completed work and accepted use consumption.

**Architecture:** Keep the C1 iterator alive at a receipt frontier. Validate receipts against its original accepted input and real pending state, append their new causal mutations and retain all completed transitions, activation objects and use arbitration. The enclosing C2 implementation still owns signed source integration, missing-side continuation, generation changes and cutover.

**Tech Stack:** C#/.NET 8, existing resource/effect planners, xUnit, PowerShell 7 bounded test lanes.

**Spec:** `specs/1536-complete-wound-materialization/spec.md`, task T081-B2C-J2-C2; `docs/superpowers/plans/2026-09-08-spiritual-effect-draft-journal-design.md`. Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536.

## Global Constraints

- Preserve all reviewed C1/V1 changes; work only in the existing 1536 worktree.
- Do not reconstruct a resource executor, ledger, history, arbiter or accepted activation on resume.
- Do not relax fixed full/semantic replay fingerprints or treat detached data as spiritual admission.
- Invalid or stale receipts must leave the current wait reusable without resource identity allocation.
- Existing ordinary allocation order and closed prefixes remain unchanged.
- C2, D and E remain open. This step is implementation work toward C2, not integrated acceptance or permission to cut over the runtime.
- No commit, staging, push or issue closure in this implementation dispatch. Parent owns integration and verification.

## Complete implementation companion

The adjacent `.patch` is the full initial code/test content for this unit. It modifies:

- `BookOfEternityClient/Services/AcceptedEffectBoundaryTranscript.cs`: non-sealing pending capture, immutable accepted binding extension and preserved causal operation maps.
- `BookOfEternityClient/Services/AcceptedMechanicsPlanner.cs`: retained iterator driver and original context ownership.
- `BookOfEternityClient/Services/AcceptedMechanicsPlanner.LiveTurn.cs`: pending yield and causal continuation.
- `BookOfEternityClient/Services/EffectAcceptedTurnPlanner.BaseResourceRouting.cs`: exact original context reference check.
- New `BookOfEternityClient/Services/AcceptedMechanicsPlanner.PendingContinuation.cs`: receipt validation, projection and owned continuation.
- New `BookOfEternityClient.IntegrationTests/EffectResourceTriggerRoutingScaleTests.PendingContinuation.cs`: five real-resource rows, including an OLD-compatible positive control followed by the absent API assertion.

The companion contains every new method/test body; existing referenced helpers remain in the named planner and budget-fixture partial family. It is a reviewed proposal for execution, not evidence of compilation. Correct compile defects and test-demonstrated defects without weakening assertions or expanding unrelated scope.

### Task 1: Retain execution through bounded receipt waves

**Interfaces consumed:** Existing C1 BeginLiveResourceExecution, ResourceExecutionSession, PendingSpiritualResourceExchange, BaseResourceRouting and real budget fixture.

**Interfaces produced:** `BindOriginalPendingContext(AcceptedMechanicsInput)`, `ReadPendingResourceRequest(PendingSpiritualResourceExchange)`, `ResumePendingResource(PendingSpiritualResourceExchange, JsonArray)` returning ResourceContinuationResult with step/issues. These are resource-layer APIs; their caller must supply actual common-owner authority.

- [x] Apply only the new Integration test file from the companion.
- [x] Run OLD control and preserve its exact failure evidence:

```powershell
. ./.superpowers/sdd/2026-09-08-spiritual-journal-c1-implementation/enter-net8.ps1
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~LivePendingContinuation_TwoWavesKeepTheSameExecutionAndSpentUses"
```

Expected: real existing pending positive assertions pass, then the new owner API assertion fails. Compilation errors are not RED evidence; repair test compilation first.

- [x] Apply the five production-file changes from the full companion, keeping prior C1 changes intact.
- [x] Run the five new rows and four existing interval controls:

```powershell
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~LivePendingContinuation_|FullyQualifiedName~EffectResourceTriggerRoutingScaleTests.SpiritualExchangeInterval_"
```

- [x] Diagnose failures and retain the same real machinery; refresh the companion to the exact final diff relative to the pinned input.
- [x] Parent inspects the implementation diff and actual TRX/summary, then dispatches independent spec/quality review. Review must explicitly inspect no-state-change outputs, exact prefix bindings and behavior after a later graph validation failure.
- [x] Parent runs one meaningful Fast checkpoint after focused evidence and review corrections, alongside the remaining C2 integration work. No redundant PreMerge now.

## Integration ruling and documentation

Ruling: bounded implementation and its RED/GREEN cycle can proceed while the common-owner plan is completed, because the accepted design already requires these exact retained semantics. This does not turn isolated resource tests into C2 acceptance. Deferring all compilation until every D/E producer exists would hide defects in the proposed interfaces and stall authorized work. Any mismatch with signed integration must be fixed before C2 acceptance.

No GM-authored command, pending root or runtime caller is exposed by this lower-layer unit. The integrated source/receipt flow must still update afterlife prompts, matrix, worked examples, manifest and guards, with conditional FullValidation. This internal-step rationale does not waive those obligations for C2.

Verified execution: semantic RED160746, GREEN161216 (9/9), independent task-review PASS, Fast162230 (7882/7882). The adjacent patch was refreshed to actual corrected preimages/postimages. This records the original seven execution actions only. Additional zero-delta consistency correction has its own complete plan dated2026-09-15; receipt/C2 acceptance remains open until that discovered defect is fixed and verified.
