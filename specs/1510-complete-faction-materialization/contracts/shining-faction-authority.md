# Contract: Shining Faction Authority

## Common raw requirement

Before `ShiningAbodeState.NormalizeStateRoot` runs, every new Shining faction
must already contain exact authored:

- `factionId`, supported `originType`, and `creationProvenance`;
- one exact `hallId`;
- complete `charter`;
- `currentAgenda`;
- explicit `factionLifecycle`;
- explicit `leadership`;
- complete `strategicMemory`;
- at least one complete `chronicle` entry;
- supported `visibility`;
- explicit `storyAuthority` object or JSON `null`;
- all seven governed surfaces;
- complete Shining `materialization`.

The normalizer cannot satisfy these requirements by creating defaults.

## Route matrix

| Route | Required origin | Exact authority | Additional result |
|---|---|---|---|
| `native_discovery` | `native_radiant` | Matching core-action or direct discovery request and receipt | 1 hall, 1 faction, 2–4 new ascended residents, exactly 2 new completed projects, costs and constrained diff |
| `player_founding` | `player_founded` | Matching pending founding request and root founding receipt | Exact proposed hall/charter/supporters, player-soul leadership, reserved costs, history |
| `story` | Supported story-owned origin | Matching canonical story authority ID | Supported visibility and story role; no inferred hidden state |

Existing route validators remain authoritative for request metadata, costs,
locks, receipts, and constrained diffs. This contract adds completeness and
cross-domain actor proof.

## Story/hidden example fragment

```json
{
  "factionId": "shine_faction_wings",
  "originType": "ascended_guardian",
  "hallId": "hall_wings_beneath_abyss",
  "creationProvenance": {
    "route": "story",
    "authorityType": "saref_main_story",
    "authorityId": "shine_faction_wings"
  },
  "currentAgenda": "Recover the oath fragments without revealing the inner circle.",
  "visibility": "hidden",
  "storyAuthority": {
    "authorityType": "saref_main_story",
    "authorityId": "shine_faction_wings",
    "factionRole": "wings_of_angels"
  }
}
```

The exact authority must exist in canonical story state and authorize the
faction ID, role, and visibility.
`creationProvenance.authorityType/authorityId` must equal
`storyAuthority.authorityType/authorityId`. A secretive name or charter is
irrelevant.

Supported story authorities are closed:

| `authorityType` | Exact `authorityId` | Required binding |
|---|---|---|
| `saref_main_story` | `main_story_saref_state.json.factionLinks.wingsFactionId` | The authority ID also equals the enclosing faction ID; generic and story-specific visibility match and `storyAuthority.factionRole=sarefFactionRole=wings_of_angels`, even before reveal |
| `guardian_ascension` | Exact `guardianId` in the canonical union of `guardians.json.activeGuardian` and `guardians[]` | `originType=ascended_guardian`, `factionRole=patron_guardian`, `visibility=revealed`, and secure Guardian leadership uses the same ID |

The Guardian authority object itself must pass Actor Materialization. No
additional story file or inferred Guardian-name mapping is accepted.

## Actor and resident proof

The validator resolves:

- non-vacant non-player `leadership.headActorType/headActorId`;
- newly added `shiningPoliticalActors[]`;
- residents created or made newly significant by the creation route.

Each resolves to an exact afterlife profile with complete Actor
Materialization. The only exceptions are:

- `headActorType=headActorId=player_soul`;
- `leadershipState=vacant` with null head fields.

Resident membership remains authoritative in
`guardian_abode_residents.json.entries[].shiningFactionId`. The faction does not
copy a resident roster. This link and every derived-strength/trade join are
exact and case-sensitive; a case-insensitive-only ID match is rejected and
contributes no resident or mechanical evidence.

## Hall and local records

- `hallId` resolves exactly; display-name matching is forbidden.
- Project, influence, ledger, trade, leadership-history, and local receipt
  records must target the enclosing exact faction.
- Root route receipts must reference the same faction/hall IDs.
- A route may not rewrite unrelated pre-turn hall, faction, project, resident,
  political actor, Soul, or story records.

## Trade

`canTrade` is computed only from existing operational lifecycle, leadership,
derived tier, and realm-local trade rules. It is never inferred from charter
prose.

The `trade` disposition is independently:

- populated if current `tradeInventory` or `tradeInventoryReceipts[]` has
  content;
- empty by design only when `tradeInventory=null` and receipts are empty.

This permits `canTrade=true` before the first inventory cycle and preserves
historical receipts when a faction becomes ineligible.

## Normalization boundary

For every faction carrying a receipt, allowed normalization is limited to:

- supported alias canonicalization where the authored value is present;
- validated same-turn identity binding;
- client-owned empty mechanical containers explicitly named in the contract;
- numeric `factionStrength`, tier, and service projections.

Forbidden synthesis includes origin, charter specialization, leadership,
lifecycle semantics, agenda, visibility, provenance, story authority, strategic
memory summary, chronicle prose, envelope, dispositions, capabilities, and
empty reasons.

Every canonical Shining faction must already carry the complete current receipt.
Receipt-less state fails before normalization and receives no visibility,
reader, or story fallback.
