# Mortal Training GM Skill Evolution Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make Mortal training purchases client-owned only for resource payment/simple progress, and GM-owned for skill unlocks or mastery level-ups.

**Architecture:** Reuse the existing pending training request file with a new request kind. Keep the public `/training buy` command, but change Mortal purchases to create a GM work packet whenever skill mechanics must evolve.

**Tech Stack:** C#/.NET, xUnit, existing `FileSystemManager`, `TrainingService`, `TrainingRequestState`, GM daemon compact templates.

## Global Constraints

- Tracked task: GitHub issue #1424.
- Mortal skill effects are GM-authored; the client must not invent or mutate full active/passive skill mechanics on level-up.
- Afterlife standard training remains client-owned for this issue.
- Update GM-facing docs/prompts/examples when changing GM-authored contracts.

---

### Task 1: Failing Tests For GM-Owned Mortal Training Evolution

**Files:**
- Modify: `BookOfEternityClient.Tests/TrainingServiceTests.cs`
- Modify: `BookOfEternityClient.Tests/TrainingWebCommandServiceTests.cs`

**Interfaces:**
- Consumes: `TrainingService.BuyTrainingAsync(string sourceActorId, string offerId, int currentTurn)`
- Produces: regression expectations for `mortal_training_skill_evolution` requests.

- [x] Add a test where an active skill is at level 1 with progress near threshold, training offer targets level 2, and `/training buy` deducts resources but leaves `skills_active.json` and final mastery level unchanged while writing `pending_training_showcase_requests.json`.
- [x] Add a test where a passive skill unlock offer for an unknown skill creates the same GM-owned pending request and leaves `skills_passive.json` unchanged.
- [x] Add a test where a GM-authored skill/mastery update clears the satisfied paid-training pending request.
- [x] Add/update web command assertion that receipt text says the lesson is paid and awaits GM finalization, without exposing raw request JSON.
- [x] Run focused tests and verify the new assertions fail because current implementation still mutates locally.

### Task 2: Pending Request Payload And Purchase Branching

**Files:**
- Modify: `BookOfEternityClient/Services/TrainingRequestState.cs`
- Modify: `BookOfEternityClient/Services/TrainingService.cs`

**Interfaces:**
- Produces: `mortal_training_skill_evolution` pending request with offer audit and skill snapshot.

- [x] Add a typed or JSON-object writer for pending Mortal skill evolution requests.
- [x] In `BuyTrainingAsync`, after cost validation, determine whether the offer is local progress or GM-owned evolution.
- [x] Deduct resources before writing the pending request only when all validations pass.
- [x] Leave active/passive skill objects unchanged for unlock/level-up requests.
- [x] Append training receipt with a `resolutionState`/similar audit marking GM finalization pending.
- [x] Clear paid-training pending requests after GM-authored skill/mastery state satisfies the target.
- [x] Return player-facing Russian text explaining that the lesson is paid and the GM will finalize the changed skill.

### Task 3: GM Docs, Validation, And Verification

**Files:**
- Modify: `BookOfEternityClient/game_master_daemon.ps1`
- Modify: `OtherGuides/Afterlife_Contract_Matrix.md` only if the shared pending request contract needs clarification across realms.
- Modify: relevant tests under `BookOfEternityClient.Tests`

**Interfaces:**
- Consumes: new pending request kind and payload.
- Produces: GM instructions for resolving Mortal skill evolution requests.

- [x] Update compact Mortal skill/training prompt text: client may pay for lesson, but GM must author skill unlock/level-up mechanics from pending request.
- [x] Add/update a worked example if the existing Mortal skill example does not cover training finalization.
- [x] Add validation coverage for the new pending request shape or documentation coverage if the request is documented.
- [x] Run focused training tests and documentation-sensitive tests.
