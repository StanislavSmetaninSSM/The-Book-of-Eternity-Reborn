# Saref Oath Break Post-Story Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement GitHub issue #557: after accepting Saref's deal, the player can break the oath only through a strong post-story mini-arc with explicit proof and consequences.

**Architecture:** Extend the existing `main_story_saref_state.json` contract rather than adding a new file. The oath break is stored under `postStoryAgenda.oathBreakArc`, updated through `sarefMainStoryUpdate.mode=record_oath_break`, validated together with the deal/oathbound state, and rendered by `/сареф`.

**Tech Stack:** C#/.NET 8, `System.Text.Json.Nodes`, xUnit, existing afterlife documentation guardrail.

---

### Task 1: State Projection

**Files:**
- Modify: `BookOfEternityClient/Services/SarefMainStoryState.cs`
- Test: `BookOfEternityClient.Tests/SarefMainStoryStateValidationTests.cs`

- [ ] Add failing test `ApplyUpdate_RecordOathBreak_MergesArcAndUpdatesOathState`.
- [ ] Verify RED with `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ApplyUpdate_RecordOathBreak_MergesArcAndUpdatesOathState"`.
- [ ] Add constants for `record_oath_break`, arc states `not_started|active|failed|broken`, routes `seret|lucian|ilarion|veyra|deep_story_evidence`, and consequences `renegade_from_wings|oath_reversed|beloved_traitor|second_confrontation_unlocked`.
- [ ] Implement `ApplyOathBreakUpdate` to merge `oathBreakArc` into `postStoryAgenda`, plus optional `playerOathState`, `sarefPersonalBond`, and `sarefAdvantageUses`.
- [ ] Verify GREEN with the same targeted test.

### Task 2: Validation

**Files:**
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.SarefMainStory.cs`
- Test: `BookOfEternityClient.Tests/SarefMainStoryStateValidationTests.cs`

- [ ] Add failing tests for:
  - Broken/removed oath after a deal without `oathBreakArc.state=broken` is rejected.
  - `oathBreakArc.state=broken` without proof/advantages/consequences is rejected.
  - Unknown oath-break advantage usage is rejected.
  - A strong Seret/Lucian/Ilarion/Veyra/deep-evidence oath break with known anti-oath advantage, serious consequences, and `playerOathState.state=broken` passes.
  - Romance/bonded oath break requires `beloved_traitor` or a tragic romance outcome note.
- [ ] Verify RED with `dotnet test ... --filter "SarefMainStoryStateValidationTests"`.
- [ ] Validate the arc shape, route, state, turn fields, proof fields, route-specific lead/evidence, advantage usage references, and allowed consequences.
- [ ] Update the deal/oathbound root rule so `playerOathState.state=broken|oath_reversed` is allowed only when the arc is proven broken.
- [ ] Verify GREEN with `SarefMainStoryStateValidationTests`.

### Task 3: Player/GM Visibility

**Files:**
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SarefStory.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`

- [ ] Add failing `/сареф` test showing the oath-break arc, route, consequences, and second confrontation unlock.
- [ ] Verify RED with the targeted ExplorerMode test.
- [ ] Render `postStoryAgenda.oathBreakArc` in Russian player-facing language.
- [ ] Verify GREEN with the targeted ExplorerMode test.

### Task 4: GM-Facing Docs And Coverage

**Files:**
- Modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Modify: `Examples/E_CLI_Afterlife_Turns.txt`
- Modify: `Examples/example_validation_manifest.json`
- Modify: `OtherGuides/Afterlife_Pending_Control_Surface_Inventory.json`
- Modify: `CLI_API_Specification.md`
- Modify: `CLI_Agent_Daemon_Specification.md`
- Modify: `TaskGuides/CLI_Step_Main.txt`
- Modify: `Rules/Block_CLI_Operations.txt`
- Modify: `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`

- [ ] Add failing documentation coverage assertions for `record_oath_break`, `oathBreakArc`, route ids, consequence ids, and the anti-oath advantage requirement.
- [ ] Verify RED with `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`.
- [ ] Update every GM-facing prompt/contract document listed above.
- [ ] Verify GREEN with the same documentation test filter.

### Task 5: Completion

- [ ] Run `dotnet build BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:UseSharedCompilation=false --verbosity:minimal`.
- [ ] Run `dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-build`.
- [ ] Commit as `feat: add Saref oath break post-story arc`.
- [ ] Merge into `main`, rerun build and full tests, push, close #557.
