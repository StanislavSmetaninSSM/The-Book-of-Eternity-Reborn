# Contract: Skill Reference Identity

## Authority

`MortalSkillReferenceAuthority` (internal, `BookOfEternityClient/Services/MortalSkillReferenceAuthority.cs`) is the single classifier for skill references on mastery validation surfaces.

- Canonical input: the exact `skillName` strings of the owning actor's skills (player active skills for `skill_mastery.json`; per-NPC skill lists inside `npc_skills.json`).
- Normalization: `MortalLocationIdentityState.BuildConfusableKey` — the same key the accepted-turn composition layer uses to reject confusable skill names.

## Classification guarantee

For a reference `r` against canonical set `C`:

- `Exact` ⇔ `∃ c ∈ C: r == c` (ordinal).
- `Confusable` ⇔ not Exact and `∃ c ∈ C: key(r) == key(c)`; `CanonicalName` is provided only when exactly one such exact-distinct `c` exists.
- `Unknown` otherwise.

The classifier never binds, rewrites, or suggests mutation of state; it only reports identity. Mutation happens exclusively through the GM repair loop.

## Issue codes

### `skill_mastery_confusable_reference`

- **Surface**: `ValidateSkillReferenceIdentityAsync` → player mastery check (`GameStateValidationPhase.PlayerStateFiles`, file `game_state/player/skill_mastery.json`).
- **FilePath**: `game_state/player/skill_mastery.json.skillMasteryChanges[i].skillName`.
- **Meaning**: the mastery entry names a case-variant or Unicode-confusable form of a real active skill instead of its exact canonical name.
- **Repair**: rewrite the entry's `skillName` to the canonical name shown in `Expected`. Never delete the entry to silence the issue.

### `player_active_skill_mastery_initialization_confusable`

- **Surface**: `ValidateSkillReferenceIdentityAsync` → player initialization check (`GameStateValidationPhase.SkillContractConsistency`).
- **FilePath**: `game_state/player/skill_mastery.json`.
- **Meaning**: a newly learned active skill is covered only by a confusable mastery entry; initialization under the exact name is missing.
- **Repair**: rename the confusable `skillMasteryChanges` entry to the exact new skill name (or add the exact entry if the confusable one belongs to another skill).

### `npc_skill_mastery_initialization_confusable`

- **Surface**: `ValidateSkillReferenceIdentityAsync` → NPC initialization check for both `Skills.Active` and `Skills.Passive` sections of `game_state/npcs/npc_skills.json`.
- **FilePath**: `game_state/npcs/npc_skills.json.<masterySection>`; `Actor` carries the NPC identity key.
- **Meaning / Repair**: same as the player initialization code, per NPC.

## Repair packet

Kind: `mortal_skill_reference_identity_repair`.

- Emitted once when any of the three codes above is present.
- `TargetFiles`: union of affected files plus `game_state/player/skills_active.json` / `game_state/npcs/npc_skills.json` as canonical-name sources.
- `ExactFieldCorrections`: one per issue path (expected = canonical name, actual = written reference).
- Safe rules: rename-only; forbidden to delete mastery entries, invent skills, or touch unrelated state.

## Non-goals (part 1)

- Duplicate skill detection inside `skills_active.json` itself (composition layer already covers accepted-turn input; historical-state dedup is a later contour).
- NPC actor-key identity tightening.
- `removeActiveSkills` selector semantics (already exact-string per Block_7 rule 7.7).
