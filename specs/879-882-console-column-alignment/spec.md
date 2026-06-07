# Feature Spec: Console Column Alignment and HUD Meter Verification

**Source issues:**
- #879 — Bug(regression): Console HUD Health/Energy/Poise bars still misaligned; require Console Client screenshot verification — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/879
- #882 — Console: add shared column-alignment layout helper for visually aligned rows — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/882

**Created:** 2026-06-07
**Feature directory:** `specs/879-882-console-column-alignment/`

## Summary

The Console Client needs a reusable layout path that renders aligned multi-row metric and label/value groups without Spectre.Console expansion drift. The immediate regression target is the Mortal World HUD: `Здоровье`, `Энергия`, and `Равновесие` must appear as one stable meter group with matching label, track, fill, baseline, and value columns. The broader task is to make representative console surfaces use shared `ConsoleLayout` helpers instead of ad-hoc drifting columns where the shared pattern applies.

## User Stories and Scenarios

### Scenario 1: Mortal HUD metrics align visually

Given a Console Client screen that renders the Mortal World status HUD, when Health, Energy, and Poise rows are displayed together, then every row uses the same label boundary, meter-track left edge, meter-track width, row baseline, and right-aligned percentage column.

### Scenario 2: Console code has a reusable aligned-row mechanism

Given future console surfaces need multi-row label/value or metric columns, when an implementer reaches for the shared layout API, then the API makes the aligned-column shape explicit and avoids unbounded middle columns or per-row spacing drift.

### Scenario 3: Representative existing surfaces do not regress

Given existing console status, NPC, faction/world-news, or afterlife panels already use `ConsoleLayout`, when the helper is changed or extended, then focused source guards and tests protect the migrated surfaces from returning to hand-rolled drifting columns.

## Acceptance Criteria

- Health, Energy, and Poise bars are visually aligned in the Console Client.
- Meter labels, tracks, fills, row baselines, and percentages use stable columns.
- The fix targets the console HUD rendering path and shared `ConsoleLayout` helpers, not Browser Client CSS or gameplay logic.
- A shared console layout helper/pattern exists for aligned column rows and is used by the HUD/status case from #879.
- Representative console surfaces no longer hand-roll visually drifting columns where the shared helper applies.
- A focused regression/source guard covers the HUD layout structure and at least one shared helper invariant.
- Relevant focused C# tests/source guards pass.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` passes after restore/build state is available.
- A real Console Client screenshot or terminal capture is created after the fix and referenced in the PR/issue evidence.
- The PR/closure report explicitly states: `Verified visually in Console Client screenshot/terminal capture`.

## Scope

In scope:
- `BookOfEternityClient/UI/ConsoleLayout.cs` helper additions or refinements.
- `BookOfEternityClient/UI/GameInterface.cs` Mortal HUD status rendering.
- Representative console call sites that already use or should use `ConsoleLayout` for aligned metric/label-value rows.
- Source guards or focused tests under `BookOfEternityClient.Tests/`.
- A local screenshot/terminal-capture artifact under a non-runtime test-results/evidence path suitable for PR/issue linking.

Out of scope:
- Browser Client styling or UI work.
- Gameplay state, command semantics, GM prompts, validation contracts, or runtime JSON contracts.
- Broad redesign of every console screen beyond representative aligned-row consumers.
- Closing #879 from source inspection alone; visual evidence is mandatory.

## Contract and Documentation Impact

This feature is Console Client presentation-only. It does not add or change game commands, GM-authored contracts, afterlife pending/control files, runtime state, validation rules, or player action semantics. GM-facing docs/prompts are not expected to change unless implementation unexpectedly changes command behavior, which should be treated as scope drift and stopped.

## Verification Requirements

Minimum local gates:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter 'FullyQualifiedName~ExplorerModeSourceGuardTests' --logger 'console;verbosity=minimal'
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore
git diff --check origin/main...HEAD
```

Visual gate:

1. Run the real Console Client from the worktree.
2. Open or create a state/screen where the Mortal HUD shows Health, Energy, and Poise together.
3. Capture a real screenshot/terminal capture after the fix.
4. Store the artifact under a review/test-results path and reference it in PR/issue comments.
5. Visually inspect and state: `Verified visually in Console Client screenshot/terminal capture`.

## Open Questions Resolved by Issue Text

- Browser Client is explicitly out of scope.
- Screenshot/terminal capture is mandatory for closure, because #853 source-only closure missed the player-visible regression.
- Spec Kit is applicable because #882 is shared player-facing Console UX/refactor work and #879 requires durable verification evidence across code, tests, and visual artifacts.
