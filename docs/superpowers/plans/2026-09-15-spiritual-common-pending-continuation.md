# Common original-capture pending continuation implementation plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans task by task. Parent owns delegation and the checkout build lock. Execute after the listed dependencies pass and the parent hands off the checkout build lock.

**Goal:** Resume the actual retained resource wait under its same signed source and common original-input owner, committing source evidence only after accepted resource advancement.

**Architecture:** Source preparation reads the actual canonical candidate under the real lease and returns a private prospective ticket. Missing completion is built only from that ticket and consumed through the exact resource-owned preparation; receipt commands pass through the existing retained typed resolver. The common capture privately retains each consumed ticket, missing preparation, previous/result step, source revisions and cloned accepted receipt commands.

**Tech Stack:** C#/.NET 8, System.Text.Json.Nodes, xUnit; PowerShell 7 bounded test runner.

**Spec:** `specs/1536-complete-wound-materialization/spec.md`, `plan.md`, `tasks.md`; issue https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536, T081-B2C-J2-C2.

## Global constraints

- Apply only after the signed source transaction, corrected resource missing-side, and eleven-file original-capture units are implemented and verified. Those dependencies are accepted as of 2026-09-16.
- No canonical pending intake/publication, ordinary GM cutover, wound admission, current-generation seal, D insertion or E reconstruction is added.
- Keep `CheckRetainedInputsAsync` unchanged: receipt DTOs are validated commands from the safe packet, not permission to edit pinned command files.
- Serialize builds with the parent's current owner. No direct unbounded `dotnet test`, no duplicate unit Fast, no stage/commit/push.
- This unit is intentionally internal and client-owned. Integrated C2/E still owns afterlife matrix/examples/manifests/source guards and conditional FullValidation.

## Task 1: same capture continuation

**Files:**

- Modify `BookOfEternityClient/Services/Validation/ValidationService.SpiritualOriginalTurnCapture.cs` only to declare the nested capture partial.
- Create `BookOfEternityClient/Services/Validation/ValidationService.SpiritualOriginalTurnContinuation.cs`.
- Create `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.SpiritualOriginalContinuation.cs`.
- Create `BookOfEternityClient.IntegrationTests/AfterlifeResourceCutoverTests.SpiritualOriginalContinuation.Receipts.cs`.

The complete four-file delta is `common-pending-proposal.patch` in the existing C2 planning directory. Its baseline was regenerated from the accepted actual capture before application, preserving the expanded read-set. The fourteen original-capture tests are dependency evidence.

**Consumes:**

```csharp
source.PrepareContinuationAsync(lease);
source.BuildPreparedResourceBatches(lease, ticket, owners, state);
source.CommitPreparedContinuation(lease, ticket);
resources.ReadPendingResourceRequest(exactWait);
resources.ResumePendingResource(exactWait, receipts);
resources.PrepareMissingAuditSide(exactWait, checkedCompletion);
resources.CommitMissingAuditSide(exactPreparation);
resources.Owns(actualInterval);
```

**Produces:**

```csharp
capture.ReadPendingResourceRequest(lease, exactResourceWait);
await capture.ResumePendingResourceAsync(lease, exactResourceWait, receipts);
await capture.ResumeMissingAuditSideAsync(lease, exactMissingWait);
```

The two continuations return `AcceptedMechanicsPlanner.ResourceContinuationResult`; the reader returns a detached `JsonObject`. No API accepts caller-composed source batches or exposes the private consumed-continuation list as authority.

- [x] Read `common-pending-proposal.md`, the complete companion and dependency evidence. Verify that zero-correction/original-capture changes have not invalidated the planned preimage; regenerate in scratch if necessary.
- [x] Verify the actual capture baseline and `git apply --check`; retain evidence in the existing C2 journal rather than creating another execution directory.
- [x] Copy only the two complete test postimages into their target paths. They use existing source/resource/helper types and reflection for the new common APIs, so OLD can compile.
- [x] Run the signed missing-side OLD row:

```powershell
. ./.superpowers/sdd/2026-09-08-spiritual-journal-c1-implementation/enter-net8.ps1
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~OriginalSpiritualContinuation_MissingSideUsesSignedTicketAndSameResourceOwner'
```

Expected OLD: signed capture and known-side resource transition pass, then reflection reports missing `ResumeMissingAuditSideAsync`. Record that this is a real positive control followed by absent API RED, not an old/new state equality proof. If failure happens earlier, diagnose the fixture before applying code.

- [x] Run receipt OLD family alone, still before code:

```powershell
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~OriginalSpiritualContinuation_RealReceiptRetryCommitsSourceOnlyAfterAcceptance'
```

Expected OLD: real sealed custom resource, canonical spiritual-art effect, signed original capture, and damage transition reach a real bounded wait; then missing common request reader fails. If source/capacity/effect admission rejects earlier, stop and repair this fixture using actual canonical producers. Do not inject a preconstructed effect plan, source capability, private capture or accepted flag to bypass a failing fixture.

- [x] Apply the two production postimages. Complete method bodies are in the companion; this preserves all original-capture behavior and changes only the partial declaration in its existing file.
- [x] Run the nine-row new family:

```powershell
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~OriginalSpiritualContinuation_'
```

Expected GREEN: nine rows, no errors/warnings, no duplicate IDs, cleanup successful, normal deadline. If runtime exceeds a bounded lane, split the seven missing rows from the two receipt rows; do not increase limits speculatively.

- [x] Confirm existing capture controls remain applicable: fourteen cases passed in run `20260916-004624-419-37492-836b54d3feef4cd4a43b014c0ad7139f-focused`. This unit changes only its partial declaration; new continuation tests exercise capture as a prerequisite. Under the development workflow, another unchanged capture run is unnecessary unless review identifies a related risk. The command for that conditional control is:

```powershell
./scripts/test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter 'FullyQualifiedName~OriginalSpiritualCapture_'
```

- [x] Inspect returned counts/TRX/build output and record actual evidence. Confirm known transitions, source references/dice, invalid retry, intermediate missing wait, zero receipt, exact alias preparation, late resource failure and pinned command rejection assertions all ran.
- [x] Confirm actual files match the complete patch with reverse apply check. Independent common_pending_review (XHigh) accepted the bounded delta; Fast passed 7872/7872 in 4:48.949. C2/D/E remain open.

## Explicit coverage boundaries

The nine rows cover both missing-side and receipt continuations. Both receipt fixtures passed real source/capacity/effect OLD positive controls before failing at the absent request reader. No deterministic test seam currently forces source commit failure specifically between a successful synchronous resource advance and the immediately following commit under the same lease; the catch/commit-failure invalidation branch is included, but independent source ticket stale/foreign/lease tests and the common late resource failure are separate evidence. This unit also does not exercise missing-fill → bounded receipt in one exchange, subsequent wound generation insertion, or cold E publication; those remain integrated C2/D/E controls.


Proposal inputs and generator are retained in .superpowers/sdd/2026-09-13-spiritual-journal-c2-planning. The accepted dependency baseline and complete four-file delta are retained there; do not rerun the pre-application generator over an already applied capture. Execution results and independent review are recorded in that directory's RESUME.md.
