# Afterlife and GM Bridge Follow-ups Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close issues #1167-#1171 without touching browser client code.

**Architecture:** Keep normal player console output separate from explicit audit diagnostics. Keep hidden GM bridge Codex execution isolated from repository coding-agent context while preserving advanced overrides.

**Tech Stack:** C#/.NET 8, Spectre.Console, xUnit, PowerShell launcher scripts, file-backed JSON game state.

## Global Constraints

- Source GitHub issues are #1167, #1168, #1169, #1170, and #1171.
- Browser client and frontend files are out of scope.
- Behavior changes use TDD: write failing focused tests before implementation.
- Afterlife contract documentation/tests must be checked when afterlife player-visible behavior changes.
- Dynamic Russian text must stay escaped/sanitized for console markup and encoded as UTF-8 in logs.

---

### Task 1: Spec Kit Setup

**Files:**
- Create: `specs/1167-afterlife-gm-followups/spec.md`
- Create: `specs/1167-afterlife-gm-followups/plan.md`
- Create: `specs/1167-afterlife-gm-followups/tasks.md`
- Modify: `AGENTS.md`

- [ ] **Step 1: Create Spec Kit artifacts**

Create feature artifacts linking #1167-#1171 and explicitly excluding browser client work.

- [ ] **Step 2: Update agent pointer**

Update the `<!-- SPECKIT START -->` block in `AGENTS.md` to reference `specs/1167-afterlife-gm-followups/plan.md`.

### Task 2: GM Bridge Reliability

**Files:**
- Inspect first: `BookOfEternityGMBridge/Program.cs`
- Inspect first: `BookOfEternityClient/Launcher/Start_GM_Daemon.ps1`
- Inspect first: `BookOfEternityClient/Launcher/game_master_daemon.ps1`
- Test: `BookOfEternityClient.Tests/*Gm*Tests.cs`

- [ ] **Step 1: Write failing encoding test for #1171**

Add a focused test proving a Russian action such as `Осторожно осматриваю письмо` remains readable through daemon logging.

- [ ] **Step 2: Implement minimal encoding fix**

Set UTF-8 output/log behavior at daemon and launcher boundaries following nearby PowerShell patterns.

- [ ] **Step 3: Write failing bridge isolation test for #1170**

Add a focused test proving Codex GM defaults do not run with repository worktree cwd/context unless configured.

- [ ] **Step 4: Implement minimal bridge isolation**

Use a session-local/default GM-only cwd and diagnostics while preserving explicit override settings.

### Task 3: Afterlife Status Output

**Files:**
- Inspect first: afterlife/ExplorerMode command result builders under `BookOfEternityClient/`
- Test: `BookOfEternityClient.Tests/*Afterlife*Tests.cs`
- Test: `BookOfEternityClient.Tests/*CommandDisplaySaveTests.cs`

- [ ] **Step 1: Write failing default-output tests for #1167**

Tests must assert default `/status` does not contain raw JSON, file paths, canonical fields, or internal closure hints.

- [ ] **Step 2: Implement player summaries**

Render readable Russian summaries by default and move raw details to explicit audit mode.

### Task 4: Shining Abode Detail Output

**Files:**
- Inspect first: Shining Abode command/result builders under `BookOfEternityClient/`
- Test: `BookOfEternityClient.Tests/*Shining*Tests.cs`

- [ ] **Step 1: Write failing detail tests for #1168**

Cover gate, package/receipt, and pending action detail views.

- [ ] **Step 2: Split default details from audit payloads**

Default views show summary, cost, blockers, outcomes, and back navigation; audit views keep raw payloads.

### Task 5: Afterlife Action Preview Output

**Files:**
- Inspect first: guardian trade, resident, offering, archive command/result builders under `BookOfEternityClient/`
- Test: `BookOfEternityClient.Tests/*Afterlife*Tests.cs`

- [ ] **Step 1: Write failing action-preview tests for #1169**

Cover one guardian trade flow, one resident flow, and one archive/offering flow.

- [ ] **Step 2: Implement player confirmations**

Default previews show action, target, cost, risk, expected result, confirm/cancel, and back path. Audit views keep pending payloads.

### Task 6: Verification and Merge

**Files:**
- Modify: `docs/console-afterlife-output-audit.md`
- Possibly modify: `OtherGuides/Afterlife_Contract_Matrix.md`
- Possibly modify: `Examples/E_CLI_Afterlife_Turns.txt`

- [ ] **Step 1: Update docs**

Document which screens are player defaults and which are explicit audit diagnostics.

- [ ] **Step 2: Run verification**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "GmBridge|GmWorkerBridge|GmWorker|Daemon|Encoding|ChaosSeaCommandDisplaySaveTests|ShiningAbodeCommandDisplaySaveTests|ExplorerModeCommandTests.Afterlife"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "FullyQualifiedName!~LocalWebUiBuiltFrontendSmokeTests"
```

- [ ] **Step 3: Review and merge**

Run independent code review, fix Critical/Important findings, create PR, merge only after verification evidence is fresh.
