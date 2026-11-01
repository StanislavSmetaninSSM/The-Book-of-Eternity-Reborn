# Quickstart: Skill Reference Identity Authority

## What breaks now (and used to pass)

```jsonc
// game_state/player/skills_active.json
{ "activeSkillChanges": [ { "skillName": "Fireball", "...": "..." } ] }

// game_state/player/skill_mastery.json — BEFORE: silently accepted; NOW: rejected
{ "skillMasteryChanges": [
  { "skillName": "fireball",          // case-variant → skill_mastery_confusable_reference
    "newMasteryLevel": 1, "newCurrentMasteryProgress": 0,
    "newMasteryProgressNeeded": 3, "masteryLeveledUp": false }
] }
```

The same applies to a Cyrillic/Greek homoglyph such as `Firеball` (Cyrillic `е`) and to NPC sections of `game_state/npcs/npc_skills.json`.

## Verify in 2 minutes

```bash
DOTNET_ROOT=/usr/share/dotnet dotnet test BookOfEternityClient.Tests \
  --filter "FullyQualifiedName~MortalSkillReferenceAuthorityTests"
DOTNET_ROOT=/usr/share/dotnet dotnet test BookOfEternityClient.IntegrationTests \
  --filter "FullyQualifiedName~SkillReferenceIdentity"
```

## Manual scenario

1. Start a turn where the player learns `Fireball` (new entry in `activeSkillChanges`).
2. Write mastery initialization with `"skillName": "FIREBALL"`.
3. Run validation: expect `player_active_skill_mastery_initialization_confusable` with a repair hint naming `Fireball`.
4. Rewrite the entry to `"Fireball"` exactly: validation passes.
5. Remove the mastery entry entirely: the legacy `player_active_skill_missing_mastery_initialization` fires instead.

## Repair flow

Any of the three new codes routes into one `mortal_skill_reference_identity_repair` harness packet inside `game_state/control/validation_repair_request.json`. The packet lists the exact file and field to rename and the canonical name to write. Rename-only: deleting mastery data is explicitly forbidden by the packet.
