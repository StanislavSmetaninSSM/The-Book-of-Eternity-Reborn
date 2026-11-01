# Implementation Plan: Complete Mortal Skill and Mastery Materialization

**Branch**: `1533-complete-skill-materialization` | **Date**: 2026-11-01 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/1533-complete-skill-materialization/spec.md`

## Summary

### Current checkpoint — 2026-11-01, PR #1579 review corrections (WIP)

- Source issue: #1533; branch: `1533-complete-skill-materialization`; reviewed implementation: `673c531710ec3acf5132369f42a43f1a7ce8c13c`.
- The user requested fixes for all five confirmed review findings. The specification now includes correction revision 1 and awaits explicit user approval under the user's AGENTS.md workflow. No production or test sources have been changed for this correction block.
- Existing design text below describes the reviewed implementation, not an approved correction plan. After approval, update this plan and `tasks.md` through Spec Kit phases and run consistency analysis before implementation.
- Review evidence on Windows: both test projects built with XML documentation enabled; `MortalSkillReferenceAuthorityTests` passed 19/19; the new skill repair integration test passed 1/1. `validation-pipeline` stopped after 61 executed cases (56 passed, 5 failed); 228 were planned and the category was incomplete. All five failing source guards passed against base-commit source files using unchanged test binaries/test sources. Owned-tree cleanup succeeded.
- The five findings are: Unicode double-reporting routes identity repairs to a training packet; case-insensitive input sets lose canonical ambiguity and exact coverage; repair corrections collapse by section path; partial-file relocation breaks source guards; new tests lack category ownership and current selection.
- No implementation verification or independent correction review has run yet. Next action: obtain user approval of `spec.md` correction revision 1, then plan the fixes and execute regression tests before code changes.

Part 1 closes the identity gap between the strict skill composition layer and the case-insensitive mastery validation layer. One new internal authority class, `MortalSkillReferenceAuthority`, indexes canonical skill names by exact (ordinal) name and by the shared confusable key (`MortalLocationIdentityState.BuildConfusableKey`). An additive validation layer classifies every mastery-surface skill reference through that authority and fails closed on confusable-only references with three new issue codes, while the legacy validators keep their unknown/missing codes as backstops. A bounded `mortal_skill_reference_identity_repair` harness packet teaches the GM the rename-only fix.

## Technical Context

**Language/Version**: C# / .NET 8
**Primary Dependencies**: `System.Text.Json`; existing `MortalLocationIdentityState.BuildConfusableKey` normalization
**Storage**: JSON canonical state files under `game_state/`
**Testing**: xUnit (`BookOfEternityClient.Tests`, `BookOfEternityClient.IntegrationTests`); temp-root `FileSystemManager` + `ValidationService` harness
**Target Platform**: Windows/Linux console client
**Project Type**: single console client + test projects
**Constraints**: fail closed; never silently retarget; repair hints in Russian; issue codes snake_case; no save migration (Pre-Alpha)
**Scale/Scope**: 1 new authority class, 1 additive validation layer (3 checks), 3 new issue codes, 1 new repair packet kind, unit + integration tests

## Constitution Check

- Test-first: RED tests for all three new codes and the authority classification precede implementation; initial run fails, wiring turns them green.
- Fail closed: confusable references are rejected, never silently bound.
- No dead controls / no doc drift: new codes route to a repair packet; rules already require exact `skillName` strings (Block_7 rule 7.7), so no GM-prompt contract change is needed in this part.

## Project Structure

### Documentation (this feature)

```text
specs/1533-complete-skill-materialization/
├── spec.md
├── plan.md
├── research.md
├── data-model.md
├── contracts/
│   └── skill-reference-identity.md
├── quickstart.md
├── tasks.md
└── checklists/
    └── requirements.md
```

### Source Code (repo root)

```text
BookOfEternityClient/
├── Services/
│   ├── MortalSkillReferenceAuthority.cs            # NEW: exact/confusable/unknown classifier
│   ├── MortalLocationIdentityState.cs              # REUSED: BuildConfusableKey
│   └── Validation/
│       ├── ValidationService.SkillReferenceIdentity.cs   # NEW: additive identity layer (3 checks)
│       └── ValidationService.ValidationPhases.cs         # EDIT (8 KB): dispatch the new layer
└── Core/GameEngine/
    ├── GameEngine.SkillReferenceIdentityRepair.cs  # NEW: packet predicate + builder
    ├── GameEngine.ValidationAndRepair.cs           # EDIT: 5-line dispatch into packet list
    └── GameEngine.ValidationAndRepair.Part02..Part18.cs  # NOTE: pre-existing 480 KB partial split
        # into per-member part files (transport limit of the file-push channel); byte-identical
        # member sequence, no code change.

BookOfEternityClient.Tests/
└── MortalSkillReferenceAuthorityTests.cs           # NEW: unit + phase-driven integration tests

BookOfEternityClient.IntegrationTests/
└── GameEngineTurnLifecycleTests.SkillReferenceIdentity.cs # NEW partial: repair packet test
```

The legacy validators (`ValidatePlayerSkillMasteryCore`,
`ValidatePlayerActiveSkillMasteryInitializationAsync`,
`ValidateNpcSkillMasteryInitializationAsync`) stay byte-identical: their
case-insensitive matching remains as the backstop that still emits the legacy
unknown/missing codes, while the additive layer emits only the new confusable
codes. This keeps the diff out of the monolithic partial files and consolidates
the whole identity contour in one file.

## Phase 0: Research

See [research.md](research.md). Key decisions: reuse `BuildConfusableKey` (single normalization authority, already used by the strict composition path); classification instead of bare boolean so repair hints can name the canonical target; ambiguity (multiple exact-distinct names per confusable key) fails closed without a named target; actor-keyed NPC dictionaries stay case-insensitive because actor identity is a separate contour.

## Phase 1: Design & Contracts

See [data-model.md](data-model.md), [contracts/skill-reference-identity.md](contracts/skill-reference-identity.md), [quickstart.md](quickstart.md).

### Wiring design

1. `ValidationService.SkillReferenceIdentity.cs` (new partial) implements `ValidateSkillReferenceIdentityAsync(phases, issues)` with three checks:
   - Player mastery reference (under `PlayerStateFiles`): rebuilds the same known-active-skills set as `ValidatePlayerSkillMastery` (current + pre-turn − removals), classifies each `skillMasteryChanges[].skillName`, emits `skill_mastery_confusable_reference` only on Confusable. Exact passes silently; Unknown is left to the legacy `skill_mastery_unknown_active_skill` — no double reporting.
   - Player initialization (under `SkillContractConsistency`): same pre/post diff as the legacy validator; emits `player_active_skill_mastery_initialization_confusable` only when a new skill is covered solely by a confusable mastery entry.
   - NPC initialization (under `SkillContractConsistency`, active + passive sections): emits `npc_skill_mastery_initialization_confusable` per NPC actor under the same rule.
2. `ValidationService.ValidationPhases.cs` gains one dispatch call for the new layer.
3. `GameEngine.SkillReferenceIdentityRepair.cs` (new partial) hosts `IsMortalSkillReferenceIdentityRepairIssue` and `BuildMortalSkillReferenceIdentityRepairPacket`; `GameEngine.ValidationAndRepair.cs` gains a 5-line dispatch (collect + emit) into the harness packet list.

### Explicitly unchanged

- `ParsePlayerSkillNames` / `ParsePlayerSkillMasteryNames` / `ParseNpcSkillChangesByActor` / `ParseNpcSkillMasteryByActor` keep returning `OrdinalIgnoreCase` sets and all their existing consumers are untouched; the additive layer reuses them read-only.
- NPC actor identity keys (`BuildNpcIdentityKey`) stay as-is.
- Pre-turn/new-skill diffing semantics stay case-insensitive (rename detection is a later contour).
