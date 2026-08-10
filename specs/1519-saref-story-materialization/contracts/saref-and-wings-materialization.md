# Contract: Saref Actor and Wings Faction Materialization

**Issues**: #1519, #1522
**Dependencies**: #1500, #1510, #1514, #1520, #1521

## Immutable templates and lazy canonical state

The complete Saref and Wings templates are part of the private catalog from the first turn. They do not create mutable canonical entities until an authorized story transition requires persistent Saref action or Wings structural influence. The accepted transition must materialize the needed entity no later than that same turn.

## Saref exact identity

Saref himself is exactly:

```json
{
  "actorType": "saref",
  "actorId": "saref_001"
}
```

`saref_agent` remains reserved for agents/supporters and cannot use `saref_001`. A display name, private truth, or relationship to the Wings cannot substitute for exact type/ID.

The materialized profile MUST satisfy the complete #1500 Actor Materialization contract: appearance/profile, personality, worldview, motivation, current realm/location, goals/plan, standard and special arts, relationships, masks/private truth, populated agency, meaningful actor-owned `gmThoughtsSummary`, and immutable receipt.

## Wings exact identity

The Wings faction is exactly:

```json
{
  "factionId": "shine_faction_wings_of_angels_001",
  "creationRoute": "story",
  "storyAuthority": "saref_main_story",
  "factionRole": "wings_of_angels"
}
```

It MUST materialize through the complete #1510 Shining faction story route and include the exact #1514 hall reference, charter, lifecycle, leadership, relationships, capabilities/dispositions, strategic memory, initial chronicle, provenance, visibility, and immutable faction receipt. No Mortal faction record or private substitute schema is allowed.

## Exact cross-links

The following must agree under ordinal comparison:

- story catalog/template IDs and digest;
- `main_story_saref_state.actorLinks` and `saref:saref_001`;
- `main_story_saref_state.factionLinks.wingsFactionId` and `shine_faction_wings_of_angels_001`;
- Wings leader actor type/ID and Saref profile;
- Wings hall ID and #1514 location authority;
- Wings `storyAuthority`, role, creation route, visibility, and provenance;
- every Guardian q4 reward reference that concerns Saref or the Wings.

Old values such as `wings_of_angels`, `shine_faction_wings`, or case variants are invalid when used in identity-bearing `factionId`/`wingsFactionId` fields and are not ID aliases. The exact `factionRole=wings_of_angels` value and existing player command aliases remain valid.

## Atomicity and immutability

Saref materialization stages the profile and story actor link together. Wings materialization stages the Shining faction and story faction link together. A same-turn combined operation also verifies mutual leadership/location/story links before any write. Failure preserves every prior root.

An existing valid receipt cannot be rewritten. Later actor changes use dedicated profile/activity/relationship/memory deltas. Later Wings decisions append chronicle entries; strategic-memory updates require matching history and cannot replace it.

## Reveal behavior

GM-private context always knows both templates. Ordinary player surfaces read only reveal-filtered canonical projections:

- hidden stages expose neither private identity nor materialization data;
- partial stages expose only registered clues/roles;
- valid reveal exposes intended actor/faction actions and public details;
- receipts, private truth, full catalog packages, GM instructions, and hidden plans remain private at every stage.

Console and browser must use the same projection DTO/semantic service and Russian in-world labels.

## Verification examples

- Hidden Saref and Wings entities are complete but absent from player projections.
- Exact reveal makes the intended public records actionable.
- `saref_agent:saref_001`, old Wings IDs, wrong hall, wrong leader, receipt rewrite, faction in Mortal state, or mismatched story link fails.
- A partial combined materialization restores all pre-turn roots.
