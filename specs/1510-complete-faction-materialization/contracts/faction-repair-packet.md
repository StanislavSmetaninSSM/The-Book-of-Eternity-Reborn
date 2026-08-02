# Contract: Faction Materialization Repair Packet

## Routing

Repair routing recognizes the stable materialization issue families and one
exact coordinate:

```text
mortal_faction:<factionId>
shining_faction:<factionId>
```

For an unbound same-turn Mortal faction, `<factionId>` is the exact effective
`initialId` until canonical binding.

## Packet shape

```json
{
  "repairType": "faction_materialization",
  "coordinate": "mortal_faction:faction_wayfarer_watch",
  "classification": "new",
  "issueCode": "faction_materialization_section_missing",
  "targetSurfaces": [
    {
      "path": "game_state/factions/faction_resources.json",
      "selector": {
        "factionId": "faction_wayfarer_watch"
      },
      "defect": "resources sidecar entry is omitted",
      "allowedResult": "one exact entry with populated arrays or exact empty arrays"
    }
  ],
  "requiredLinks": [],
  "preserve": [
    "all validated core semantic fields",
    "all unrelated factions",
    "all existing chronicles"
  ],
  "prohibited": [
    "changing another faction",
    "rewriting validated history",
    "inventing narrative content",
    "changing materializationId"
  ]
}
```

The runtime packet may use its existing text/task representation, but it must
carry the same semantics.

## Mortal targets

Allowed target files are only the exact members required by the defective
bundle:

```text
game_state/factions/faction_core.json
game_state/factions/faction_structure.json
game_state/factions/faction_resources.json
game_state/factions/faction_projects.json
game_state/factions/faction_custom.json
game_state/factions/faction_chronicles.json
exact Mortal location file(s)
exact Mortal NPC file(s)
```

The packet names the exact faction selector and valid sections to preserve.

## Shining targets

Primary target:

```text
game_state/meta/shining_abode_state.json
```

with exact faction/hall/receipt/political-actor selectors. Cross-link defects may
also name:

```text
game_state/meta/guardian_abode_residents.json
game_state/meta/afterlife_entity_profiles.json
the exact canonical story-state file
```

No unrelated afterlife root is writable merely because it appears in the same
composite state.

## Preservation rules

Every packet:

- identifies one faction coordinate;
- identifies exact missing/contradictory sections and links;
- lists valid sections that must remain unchanged;
- forbids other faction IDs and unrelated roots;
- forbids content invention from names, tags, descriptions, or genre;
- forbids changing an accepted `materializationId` or historical receipt;
- prefers a missing bounded surface over a wholesale rewrite.

## Repair acceptance

After repair, the complete raw bundle is revalidated before normalization, then
canonical continuity/cross-file validation runs. If any required member remains
invalid, the accepted turn stays rejected and the existing bounded
repair/rollback policy continues. No partial repair is accepted.
