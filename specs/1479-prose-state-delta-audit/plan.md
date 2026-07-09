# Implementation Plan: Prose State Delta Audit

**Branch**: `main` | **Date**: 2026-07-09 | **Spec**: `specs/1479-prose-state-delta-audit/spec.md`

**Input**: Feature specification from `specs/1479-prose-state-delta-audit/spec.md`

## Summary

Add a deterministic accepted-turn audit that detects high-risk player-facing prose/state mismatches: known skill names used as successful action evidence without skill progress/rationale, and active quest clues revealed without quest state/rationale. The audit should repair the turn before acceptance rather than letting transient prose become the only source of truth.

## Technical Context

**Language/Version**: C#/.NET 8, JSON file-backed game session state.

**Primary Dependencies**: Existing `ValidationService`, accepted-turn narrative/interface validation, Mortal skill and quest state files, GM repair diagnostics.

**Storage**: Existing files under `game_state/player/`, `game_state/quests/`, and `output/`. If an explicit no-progress rationale surface already exists, reuse it; otherwise add a small accepted audit surface under `game_state/meta/`.

**Testing**: xUnit in `BookOfEternityClient.Tests`.

**Target Platform**: Local Windows console/browser game stack; validator is shared.

**Constraints**:
- Source issue #1479.
- No prompt-only fix.
- Deterministic, conservative audit; no external NLP.
- Update GM-facing prompts/docs/examples if accepted-turn contract changes.

**Source Issue(s)**:
- #1479 https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1479

**Contract Scope**: validation, runtime-state, GM-facing prompts/docs/examples, live-test notes.

**Verification Commands**:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ProseStateDelta|AcceptedTurn|Validation"
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|ValidationSourceGuardTests|GmTurnHelperContractTests"
```

Manual:

- Repeat the Mortal golden-route scene after buying a skill from `/обучение`.
- Confirm skill/quest progress persists or validation repair blocks the turn.

## Constitution Check

- **GitHub traceability**: PASS. Source issue #1479 is linked.
- **Spec Kit fit**: PASS. This changes validation, canonical state authority, and GM-facing contract behavior.
- **Player-facing integrity**: PASS. The feature protects player-visible claims from becoming untracked transient text.
- **Contract/state authority**: PASS. Skill use and quest clue claims must resolve to state deltas or rationale.
- **Test-first path**: PASS. Regression tests precede implementation.
- **Verification evidence**: PASS. Focused validation and docs/source-guard tests are listed.

## Project Structure

```text
specs/1479-prose-state-delta-audit/
├── spec.md
├── plan.md
└── tasks.md
```

## Risk Notes

- False positives are the main risk. Restrict matching to known player skill display names plus success/action verbs, and active quest names plus discovery/clue verbs.
- If no existing no-progress rationale surface exists, keep the new surface narrow and GM-facing, not a player debug dump.
