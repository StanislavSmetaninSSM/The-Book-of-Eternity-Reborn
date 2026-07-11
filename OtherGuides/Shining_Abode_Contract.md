# Shining Abode Contract

This is the compact GM-facing Shining Abode source document. It replaces the old
practice of reading several historical implementation plans in `OtherGuides`.

Use it together with `OtherGuides/Afterlife_Contract_Matrix.md` and
`Examples/E_CLI_Afterlife_Turns.txt`.

## Realm And Lifecycle Gates

- `game_state/meta/soul_state.json.currentRealm = Shining Abode` and
  `game_state/meta/shining_abode_state.json.preparedIncarnationPackage = null`
  means ordinary active Shining Abode.
- `currentRealm = Shining Abode` and `preparedIncarnationPackage != null` means
  pending-bootstrap handoff. In this mode ordinary Shining Abode actions are not
  legal.
- `game_state/control/afterlife_return_guard.json` is a client-owned post-life
  safety guard, not a Shining bootstrap marker.
- Ordinary afterlife turns may consume only a semantic-valid
  `afterlife_return_guard.json` with `reason = post_life_return`.
- Malformed, unreadable, wrong-reason, active, or otherwise blocking
  `afterlife_return_guard.json` remains fail-closed until validation repair or
  explicit client/runtime clear.

## Local Client-Owned Routes

These routes are local client actions, not GM-authored accepted-turn results:

- `/reenter_shining_abode`
- `/return_to_chaos_sea`

`/reenter_shining_abode` is allowed only from Chaos Sea when stored Shining
availability is active, no active afterlife spiritual conflict exists, and
`afterlife_return_guard.json` is absent or semantic-valid and inactive.
Malformed or wrong-reason guard state blocks re-entry fail-closed until
validation repair or explicit client/runtime clear.

`/return_to_chaos_sea` is allowed only from ordinary active Shining Abode. It is
blocked by pending-bootstrap handoff, malformed/non-empty Shining pending
contracts, active afterlife spiritual conflict, unresolved Source of Light
capstone, or unresolved legacy native faction discovery.

## Canonical Ownership

`game_state/meta/shining_abode_state.json` owns:

- `availability`
- `radiance`
- `lightSparks`
- `halls[]`
- `factions[]`
- `shiningPoliticalActors[]`
- `pendingNativeFactionDiscovery`
- `gates`
- `preparedIncarnationPackage`
- Shining founding, realignment, leadership, and trade receipts.

`game_state/meta/guardian_abode_residents.json` owns resident identity and
resident-facing Shining additions such as faction membership, resident role,
faction loyalty, restlessness, and realignment state.

Do not duplicate resident membership into `faction.residents[]`; faction
membership is derived from resident state.

## Current Hall Local Interactions

`shining_abode_state.currentHallId` is the location authority for local
`/обучение` and `/торговля` interactions. It must identify an entry in
`halls[]`; missing or unknown values fail closed instead of exposing every
Shining actor or faction.

- A mentor is local when its direct `hallId` matches, or when its canonical
  resident/faction/leadership/political-actor relationship resolves to a
  faction whose `hallId` is current.
- Resident membership comes from
  `guardian_abode_residents.json.entries[].shiningFactionId`.
- Faction heads may resolve through `leadership.headActorId`; political actors
  may resolve through `currentFactionId` or `originFactionId`.
- Shining `/торговля` lists only player-visible factions in the current hall.
- A cached `mentorTrainingShowcase` or trade inventory does not override
  locality. Direct actions targeting a faction or mentor from another hall are
  rejected before any currency or progression state changes.
- Player-triggered training and trade recheck the actual realm and current hall
  immediately before commit, after reading showcase, price, and pending state.

## Pending And Receipt Surfaces

Shining political and core actions must close through explicit pending/control
requests and receipts. They must not be prose-only mutations.

Important Shining control surfaces include:

- `pending_shining_abode_actions.json`
- `pending_shining_faction_foundings.json`
- `pending_shining_faction_realignments.json`
- `pending_shining_faction_leadership_transitions.json`
- `pending_shining_trade_inventory_requests.json`
- `pending_source_of_light_capstone.json`
- legacy `shining_abode_state.json.pendingNativeFactionDiscovery`

Every accepted closure must echo the relevant request identity and quoted costs
where the pending request contains them.

## Wrong-Realm Mortal Files

MortalWorldProfile-only files must not be resolved by afterlife turns:

- `pending_resident_companion_manifestation_request.json`
- `pending_npc_social_interactions.json`
- `pending_npc_trade_inventory_requests.json`
- `[NPC_TRADE_REQUEST]`

In Chaos Sea or Shining Abode:

- valid non-empty `pending_resident_companion_manifestation_request.json` is
  preserved as next-life context and does not block Soul Gates;
- malformed manifestation files block Soul Gates until repair;
- NPC social/trade pending files are wrong-realm repair-only context;
- do not materialize Mortal NPCs, encounters, NPC trade stock, NPC journals,
  world events, or quests from these files in afterlife.

## Faction Politics

Shining faction state separates charter from leadership:

- `charter` describes faction identity and intent;
- `leadership` describes the current head actor and leadership state;
- `shiningPoliticalActors[]` records non-resident political actors when needed.

Player-led or player-founded faction mechanics must use explicit request and
receipt flows. Guardian, resident, radiant actor, and player-soul leadership
must be represented through canonical actor references rather than copied
objects.

## Source Of Light And Saref Boundaries

Source of Light capstone is not a Shining core action. It has its own pending
contract and closure surface.

Saref Wings search is ordinary-active-Shining only and closes through
`sarefMainStoryUpdate.mode = reveal_wings | refuse_wings | block_wings`.
Do not resolve it through Shining faction receipts, Mortal faction files, or
free prose.

## Archived Background

Historical plans and design/audit notes live in:

`docs/audits/afterlife/shining-abode/`

They may be useful for formula recovery or archaeology, but they are not current
GM prompt context. When an archived rule is promoted back to live guidance, copy
it into this file, `Afterlife_Contract_Matrix.md`, or a worked example.
