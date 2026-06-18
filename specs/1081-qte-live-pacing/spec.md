# Feature Specification: Console QTE Live Playability

**Feature Branch**: `work/1081-qte-live-pacing`  
**Created**: 2026-06-18  
**Status**: Draft for autonomous implementation  
**Input**: GitHub issue [#1081](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)

## Source Issues & Scope

- **Source GitHub issue(s)**: [#1081 [QTE] Fix live mini-game pacing and memory-input visibility bugs](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1081)
- **Issue type**: Reopened bug / player-facing console QTE usability regression.
- **Spec Kit justification**: The issue affects several live console QTE mini-game loops, player-facing pacing/readability, and closure requires durable live-console or harness evidence. It meets the constitution policy for player-facing UX, multi-file debugging/testing, and work requiring durable handoff evidence.
- **Contract scope**: Console-client live QTE UX, focused automated tests/source guards, live console/manual or harness evidence, and player-facing control/help copy if changed. No save format, afterlife/Chaos Sea/Shining Abode contract, Browser interactive parity, QTE reward/scoring/rank contract, or Daren prose/reward-profile change is intended.
- **Out of scope**: Browser QTE parity, new QTE types, QTE scoring/rank model, practice mode, Daren route content/prose/rewards, GM-authored QTE schema changes unless a root-cause investigation proves they are required, and broad console UI rewrites outside the four reported mini-games.

## Reopen Context

Issue #1081 was previously marked fixed by static/source-guard-style evidence, then reopened after the user re-tested the live console client and reported the original problems still remained. This feature must not close on source guards alone. Closure requires evidence from the real live console path or a deterministic harness/test mode that exercises the same live mini-game rendering/timer/control code.

Still reported by the user:

1. **TimingBar / Полоса реакции** remains too slow and trivially winnable regardless of difficulty.
2. **PromptChain / Цепь Знаков** still fails immediately before the player can read/react.
3. **BalanceMeter / Равновесие** remains poorly legible; A/D movement step is hard to understand in live play.
4. **PatternMemory / Память Рун** still exposes the sequence during input, removing the memorization challenge.

## User Scenarios & Testing

### User Story 1 - TimingBar Difficulty Matters (Priority: P1)

A console player starts a live TimingBar QTE and sees a marker that moves fast enough for difficulty to matter, while still remaining fair on normal/easier configurations.

**Independent Test**: A focused test or harness probe proves effective marker speed/window timing differs by difficulty/stat tier and prevents a trivially stationary/easy marker at high difficulty.

**Acceptance Scenarios**:

1. **Given** a low-difficulty TimingBar, **When** the live loop starts, **Then** marker motion is readable and fair.
2. **Given** a high-difficulty TimingBar with the same stat tier, **When** the live loop starts, **Then** the marker progresses materially faster or gives less trivial success timing than low difficulty.
3. **Given** the implementation is verified, **When** closure evidence is recorded, **Then** it includes either live console/harness observations or deterministic timing-state assertions from the live path.

---

### User Story 2 - PromptChain Gives Readable Reaction Time (Priority: P1)

A console player starts a PromptChain QTE and has enough time to see the current sign, understand the required key, and press it before timeout.

**Independent Test**: A focused regression or harness probe proves PromptChain has a readable display/grace/input window and does not immediately fail before the first actionable prompt can be read.

**Acceptance Scenarios**:

1. **Given** a PromptChain starts, **When** the first sign is displayed, **Then** the remaining timer is not already near zero.
2. **Given** a player waits briefly to read the screen, **When** input begins, **Then** the QTE has not already failed solely due to a startup timing bug.
3. **Given** PromptChain copy is shown, **When** the player reads it, **Then** it remains player-facing Russian/in-world and does not expose debug/API terms.

---

### User Story 3 - BalanceMeter Controls Are Legible (Priority: P2)

A console player can infer what A/D or arrow keys do and how much each press moves the balance marker.

**Independent Test**: A focused source/copy test or harness snapshot proves the live BalanceMeter panel shows current position/safe range and explicit left/right step information.

**Acceptance Scenarios**:

1. **Given** BalanceMeter is active, **When** the player reads the live panel, **Then** it shows current position, safe/target range, and A/← / D/→ movement direction.
2. **Given** step size is configurable or computed, **When** the panel renders, **Then** the visible copy communicates the effective step size or movement impact.
3. **Given** the player presses A/D or arrows, **When** state updates, **Then** the movement matches the visible control hint.

---

### User Story 4 - PatternMemory Hides the Reveal During Input (Priority: P1)

A console player must memorize the rune/key sequence because the full reveal sequence is not shown during the input phase.

**Independent Test**: A RED regression test or harness snapshot fails on the reopened behavior by detecting reveal text/sequence in the input phase, then passes when only input progress/current prompt is visible.

**Acceptance Scenarios**:

1. **Given** PatternMemory reveal phase has completed, **When** input phase begins, **Then** the full original sequence is no longer rendered above the input prompt.
2. **Given** input phase is active, **When** the player reads the panel, **Then** it shows progress and controls without leaking the answer.
3. **Given** timeout/cancel/fail/success occur, **When** the result is shown, **Then** existing grade semantics and summaries remain compatible.

---

### User Story 5 - Closure Evidence Exercises the Live Console Path (Priority: P1)

Maintainers need evidence that the real path the player sees was checked, because prior static tests did not catch the live regression.

**Independent Test**: The branch includes either automated live-console/harness artifacts or a deterministic in-app/scripted test mode that drives the same mini-game loops. If a true manual visual smoke cannot be performed autonomously, the final report must state that limitation honestly and include the strongest harness/runtime evidence available.

**Acceptance Scenarios**:

1. **Given** the four reported mini-games, **When** verification runs, **Then** evidence covers TimingBar, PromptChain, BalanceMeter, and PatternMemory specifically.
2. **Given** static source guards pass, **When** closing the issue, **Then** they are not the only evidence; live/harness/runtime evidence is also recorded.
3. **Given** a residual manual-human limitation remains, **When** the issue evidence comment is posted, **Then** it names the limitation and explains the automated substitute rather than claiming unobserved human visual success.

## Edge Cases

- The implementation must preserve QTE result grades (`success`, `partial`, `fail`), timeout/cancel behavior, input normalization for RU/EN physical-key support, and existing Daren route mechanics.
- Any player-facing copy added for controls must escape dynamic/GM-authored text before Spectre.Console markup rendering.
- Timer/pacing fixes must avoid replacing one unfair state with another: normal difficulty should be playable, high difficulty should not be trivially winnable, and PromptChain must not become impossible.
- If root cause is fixture/config-specific rather than code, the branch must still add regression coverage and document the correct fixture/config requirements.
- Browser/React QTE behavior may be inspected for parity impact but should not be modified unless needed to keep shared DTOs compiling after a C# change.

## Requirements

### Functional Requirements

- **FR-001**: TimingBar live marker speed/window timing MUST scale with difficulty/stat in a way that prevents high-difficulty attempts from being trivially winnable.
- **FR-002**: PromptChain MUST provide a readable initial display/grace/input window and MUST NOT fail immediately before the player can read/react.
- **FR-003**: BalanceMeter live panel MUST communicate direction and effective movement step for A/← and D/→ controls, plus current position/safe range.
- **FR-004**: PatternMemory input phase MUST NOT render the full reveal sequence or answer text after the reveal phase ends.
- **FR-005**: The fix MUST preserve QTE contracts, validation behavior, grade semantics, input normalization, browser authority boundaries, and Daren route mechanics unless a conflict is documented and the spec is updated before code drifts.
- **FR-006**: The branch MUST include focused RED/GREEN regression coverage for each reported bug where practical.
- **FR-007**: The branch MUST include live console/manual or deterministic harness evidence for each of the four reported mini-games; static source guards alone are insufficient closure evidence.
- **FR-008**: Verification MUST include focused QTE tests, any new live/harness tests or artifacts, client build, `git diff --check`, added-line static scan, independent review, and post-merge focused verification before issue closure.

### Key Entities

- **Live QTE Loop**: Console runtime path that renders a mini-game frame, processes keyboard input, updates timer/state, and resolves grade.
- **TimingBar State**: Marker position, success zone, speed, difficulty/stat adjustment, and remaining time.
- **PromptChain State**: Current sign/key, step index, mistakes, display/grace/input timing, and remaining time.
- **BalanceMeter State**: Marker position, safe range, movement step, direction controls, and remaining time.
- **PatternMemory State**: Reveal sequence, reveal phase duration, input phase prompt/progress, and entered sequence.
- **Live Evidence Artifact**: A test log, harness snapshot, scripted observation, or manual transcript that demonstrates the live path for a specific mini-game.

## Success Criteria

### Measurable Outcomes

- **SC-001**: Focused RED evidence exists for TimingBar pacing, PromptChain immediate-fail timing, BalanceMeter control readability, and PatternMemory reveal leakage, or the implementation records why a specific bug required a different runtime/harness proof.
- **SC-002**: Focused QTE tests pass with zero failures after implementation and include a non-zero count.
- **SC-003**: Live/harness/manual evidence artifacts cover all four reported mini-games and are referenced in `tasks.md` and the GitHub issue comment.
- **SC-004**: `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passes with zero errors.
- **SC-005**: `git diff --check origin/main...HEAD` passes.
- **SC-006**: Added-line static scan over `origin/main...HEAD` reports no hardcoded secrets, shell/eval/pickle/SQL injection patterns, or accidental run artifacts.
- **SC-007**: Independent review approves the implementation or all Critical/Important findings are fixed and re-reviewed before PR/merge.

## Assumptions

- The issue is console-client first. Browser QTE parity remains a separate surface unless shared compile/DTO changes are unavoidable.
- A deterministic harness or in-app scripted path is acceptable evidence when autonomous cron cannot perform a human visual smoke, but it must exercise the same live mini-game code path that player testing uses.
- If the current Windows host cannot run a true ConPTY/manual console smoke in this tick, the implementation must preserve artifacts and state the residual risk rather than claiming a human visual pass.
