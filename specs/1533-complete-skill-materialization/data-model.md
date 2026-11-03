# Data Model: Skill Reference Identity Authority

## MortalSkillReferenceAuthority (new, internal, client-owned)

Immutable index built once per validation unit from a canonical skill-name set.

| Member | Type | Notes |
| --- | --- | --- |
| `Build(IEnumerable<string?> canonicalNames)` | static factory | Trims and ignores null/whitespace entries. |
| `Count` | `int` | Number of distinct exact canonical names. |
| `Classify(string? reference)` | `MortalSkillReferenceMatch` | Never throws on null/empty (returns Unknown). |

### Internal indexes

- `HashSet<string>` exact names, `StringComparer.Ordinal` — the only accepting set.
- `Dictionary<string, List<string>>` confusable key → distinct exact names, key from `MortalLocationIdentityState.BuildConfusableKey`, ordinal comparer.

## MortalSkillReferenceMatch (result)

| Property | Type | Notes |
| --- | --- | --- |
| `Kind` | `MortalSkillReferenceMatchKind` | `Exact`, `Confusable`, `Unknown`. |
| `CanonicalName` | `string?` | Set only when `Kind == Confusable` and exactly one exact-distinct canonical name shares the key; `null` for ambiguous keys and for Exact/Unknown. |
| `IsAmbiguous` | `bool` | `true` when the confusable key maps to more than one exact-distinct canonical name. |

Classification order:

1. Exact ordinal hit → `Exact`.
2. Else confusable-key hit → `Confusable` (with `CanonicalName` when unambiguous).
3. Else → `Unknown`.

## Validation surfaces and codes

| Surface | File | Exact | Confusable | Unknown |
| --- | --- | --- | --- | --- |
| Player mastery reference | `game_state/player/skill_mastery.json` → `skillMasteryChanges[].skillName` | accept | `skill_mastery_confusable_reference` | `skill_mastery_unknown_active_skill` (existing) |
| Player initialization coverage | `skills_active.json` vs `skill_mastery.json` | satisfied | `player_active_skill_mastery_initialization_confusable` | `player_active_skill_missing_mastery_initialization` (existing) |
| NPC initialization coverage | `game_state/npcs/npc_skills.json` (`NPCActiveSkillChanges`/`NPCPassiveSkillChanges` vs `NPCSkillMasteryChanges`/`NPCPassiveSkillMasteryChanges`) | satisfied | `npc_skill_mastery_initialization_confusable` | `npc_skill_missing_mastery_initialization` (existing) |

All confusable issues: `IssueSeverity.Error`, `Actor` attached when known (NPC key), `Expected` = canonical `skillName` (or note about ambiguity), `Actual` = the written reference, Russian `RepairHint` instructing the exact rename.

## State transitions

None. This part changes validation and repair routing only; canonical skill and mastery file shapes are untouched.
