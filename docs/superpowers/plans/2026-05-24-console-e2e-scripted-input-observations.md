# Console E2E Scripted Input and Observations Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers-subagent-driven-development (recommended) or superpowers-executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Close the current console E2E closure unit by finishing #676 built-in scripted input mode and #696 real observation/runbook integration without changing gameplay mechanics.

**Architecture:** Add an `IConsoleInputSource` boundary so the normal console path and the scripted E2E path feed the same menu/prompt code. The scripted input source reads JSON steps, emits diagnostics/failure artifacts, and writes player-facing observation snapshots through the existing observation artifact writer. Focused smoke tests launch the real client against a disposable sandbox and assert artifacts rather than scraping ANSI stdout.

**Tech Stack:** .NET 8, xUnit, `dotnet test`, GitHub issues/PRs, existing `ConsoleE2ESandbox`, `ConsoleE2EObservationArtifactWriter`, and Spectre.Console UI.

---

## Design note

Problem: agent-driven console E2E cannot rely on `stdin`, raw ANSI screen scraping, or ConPTY as the primary path. Issue #676 selected built-in scripted input; #696 requires wiring that input into real observation artifacts and refreshing the runbook with executable commands.

Constraints:
- Tracked tasks: #676 and #696 are the primary closure targets; #677/#678/#679 are adjacent foundations referenced by the docs/tests.
- No Mortal World or Afterlife mechanics are changed.
- No ConPTY/winpty primary path.
- Player-facing snapshots must not leak hidden/internal-only state.

Approaches considered:
1. External PTY automation: closest to human keyboard input, but brittle on Windows/Hermes and explicitly out of scope for #676.
2. Test-only helpers around menu methods: easy to unit test but would not prove the real client can be launched by an agent.
3. Built-in input abstraction plus real process smoke tests: deterministic, CI-friendly, and exercises the production menu/prompt code. Selected.

Test strategy:
- Parser/unit tests for CLI options and required script key/text support.
- Negative tests for invalid JSON, invalid keys, exhausted scripts, and failure artifacts.
- Production key-handler test proving scripted keys drive the same main-menu selection logic.
- Real process smoke test from disposable sandbox to scripted main-menu exit and observation snapshots.
- Real process negative smoke test for exhausted script preserving `failure.txt` plus `screens/*error*.json`.
- Runbook coverage tests for exact commands, artifact paths, and troubleshooting text.

## Tasks

### Task 1: Scripted input CLI and parser coverage

**Files:**
- Modify: `BookOfEternityClient/Configuration/ClientStartupOptions.cs`
- Modify: `BookOfEternityClient.Tests/ClientStartupOptionsTests.cs`

- [x] Add `--e2e-script`, `--e2e-artifacts`, and `--plain-output` startup options.
- [x] Cover explicit values and missing-value diagnostics in tests.
- [x] Verify with `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter ClientStartupOptions`.

### Task 2: Console input abstraction and scripted input source

**Files:**
- Create: `BookOfEternityClient/Core/IConsoleInputSource.cs`
- Create: `BookOfEternityClient/Configuration/ConsoleE2EScriptedInput.cs`
- Modify: `BookOfEternityClient/Core/StandardTextComposerConsole.cs`
- Modify: `BookOfEternityClient/UI/SpectreExplorerConsole.cs`

- [x] Add `IConsoleInputSource` with system console and scripted implementations.
- [x] Support key steps for Up/Down/Left/Right/W/S/Enter/Escape/digits/space/ConsoleKey names and text steps for `ReadLine()` flows.
- [x] Write `failure.txt` for invalid JSON, invalid keys, kind mismatches, exhaustion, and unconsumed script steps.
- [x] Cover supported keys, printable text, invalid JSON, invalid key, and exhausted script diagnostics.

### Task 3: Route production console input through the abstraction

**Files:**
- Modify: `BookOfEternityClient/Program.cs`
- Modify: `BookOfEternityClient/Core/GameEngine.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.TurnLifecycle.cs`
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.ValidationAndRepair.cs`
- Create: `BookOfEternityClient/Core/ConsoleMainMenuInputHandler.cs`

- [x] Inject `IConsoleInputSource` into `GameEngine` and explorer/text-composer consoles.
- [x] Replace direct `Console.ReadKey`, `Console.ReadLine`, and `Console.KeyAvailable` calls in the touched console paths.
- [x] Extract main-menu key handling into `ConsoleMainMenuInputHandler` so tests prove scripted keys use the production selection logic.
- [x] Preserve normal interactive behavior through `SystemConsoleInputSource`.

### Task 4: Real observation integration and smoke tests

**Files:**
- Modify: `BookOfEternityClient/Core/GameEngine/GameEngine.MainMenu.cs`
- Modify: `BookOfEternityClient.Tests/ConsoleE2ESmokeTests.cs`

- [x] Write `screens/*.json` and `screens/*.txt` snapshots for scripted main menu, options menu, selection changes, exit, and scripted-run exceptions.
- [x] Add a real process smoke test: disposable sandbox → `dotnet run` with `--e2e-script` → exit menu → artifact assertions.
- [x] Add a real process options-menu smoke test: main menu → options → back → exit, asserting menu observations.
- [x] Add a real process negative test: empty script → exit code `2` → `failure.txt` → `screens/*error*.json`.
- [x] Add a real process invalid-JSON startup test: invalid script → exit code `2` → `failure.txt` before host startup.

### Task 5: Documentation and drift coverage

**Files:**
- Modify: `docs/e2e/console-agent-runbook.md`
- Modify: `docs/e2e/console-observation-artifacts.md`
- Modify: `BookOfEternityClient.Tests/ConsoleE2ERunbookTests.cs`
- Modify: `BookOfEternityClient.Tests/ConsoleE2EObservationArtifactTests.cs`

- [x] Document the real `--e2e-script`, `--e2e-artifacts`, `--plain-output` command sequence.
- [x] Document artifact paths exactly: script/stdout/stderr under `$RUN_ROOT`, observations under `$RUN_ROOT/artifacts/screens`, and failure diagnostics under `$RUN_ROOT/artifacts/failure.txt` plus error snapshots.
- [x] Add runbook coverage text for the real runner failure evidence.
- [x] Re-run focused docs/tests filters after the docs update.

### Task 6: Review, PR, CI, merge

**Files:**
- All intentional files from Tasks 1-5.

- [x] Run `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter ConsoleE2E`.
- [x] Run a broader relevant test command for the modified startup/input surface.
- [x] Run an independent review against #676/#696 acceptance criteria.
- [ ] Commit only intentional source/tests/docs/plan files; do not commit `bin/`, `obj/`, review scratch dirs, `.superpowers`, or unrelated old plan files.
- [ ] Push, create PR with `Closes #676` and `Closes #696` only if verification/review confirm both are satisfied.
- [ ] Check CI; debug systematically if it fails.
- [ ] Squash-merge when verification/CI are acceptable, then verify the issue state.
