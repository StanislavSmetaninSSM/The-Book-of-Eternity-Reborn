# Contract: Guardian Story and Non-Story Quest Lifecycle

**Issues**: #1519, #1521

## Classification

Every current-schema Guardian quest snapshot MUST carry exactly one value:

```json
"storyScope": "saref_main_story"
```

or:

```json
"storyScope": "non_story"
```

Catalog membership and exact scope jointly select authority. Titles, prose, ID prefixes, and actor guesses never select it. Console and browser render `non_story` with the in-world Russian marker `Несюжетный квест`; neither exposes the raw enum.

## Fixed story quests

- Definition: immutable `saref_questline.json`.
- Progress: only `main_story_saref_state.json.guardianQuestlines[].questStates[]`.
- Views: client-derived Guardian `questManagement` and exact profile `personalQuests`.
- Origin: exactly `saref_main_story_catalog`.
- Transition: `sarefMainStoryUpdate.mode=advance_guardian_quest`, except q4 success uses `record_memory_scene`.
- Ordinary `offerQuest`, `acceptQuest`, `guardianQuestProgressUpdates`, and `completeQuest` MUST reject fixed story quests.

Allowed lifecycle and authority are defined in `data-model.md`. q1–q4 ordering is strict beyond latent evidence. q4 can complete only from active through a successful playable memory scene with registered non-physical proof, revelation, and advantage.

## Renewable non-story quests

The GM may create a `non_story` quest before or indefinitely after q4 when ordinary cap/difficulty rules allow. Use:

```json
{
  "command": "offerQuest",
  "guardianId": "guard_system_varak_001",
  "quest": {
    "questId": "guardian_varak_border_oath_0042",
    "storyScope": "non_story",
    "questOrigin": "guardian_post_story_personal_request",
    "title": "Клятва у треснувшего рубежа",
    "description": "...",
    "objective": {},
    "successAuthority": {},
    "difficulty": "hard",
    "rewardOutline": {},
    "grounding": {}
  }
}
```

The command targets one existing Guardian, requires a new stable ID outside the catalog, validates origin/source grants/grounding, and must fit the shared available cap and difficulty ceiling. It cannot write Saref state.

## Player acceptance request

`/guardian_quest_accept` (`/принять_квест_хранителя`) is available in afterlife realms. Console and browser select the exact Guardian and available quest and write only:

`game_state/control/pending_guardian_quest_acceptances.json`

The request contains exact Guardian/quest/scope, expected available status, a semantic offer snapshot digest, current story binding for story quests, turn/time, and request ID. It does not mutate canonical quest state before the accepted turn.

The pending path is a first-class client-owned afterlife contract. Register it in `AfterlifeContractRegistry` and `Afterlife_Pending_Control_Surface_Inventory.json`, include it in validated snapshots/client-owned classification, block Soul Gates while a non-empty request remains unresolved, clear it only after a validated resolution, and keep daemon/prompt/docs/source guards synchronized.

## Acceptance response

The GM returns one matching `guardianQuestAcceptanceResolutions[]` row.

- `accepted + saref_main_story`: exactly one `advance_guardian_quest recognized -> active`; projector moves the view.
- `accepted + non_story`: exactly one pending-backed `UpdateGuardians.acceptQuest`; normalizer moves the unchanged offer snapshot to active.
- `rejected`: reason required; no quest mutation.

The request is cleared only after validation accepts the resolution. Missing resolution, wrong request/offer digest, scope switch, unknown identity, duplicate acceptance, unrelated quest mutation, or accepted-without-authority enters bounded repair/rollback.

## Mortal progress and afterlife hand-in

- Story q1–q3 Mortal progress uses the story transition `active -> ready_to_turn_in` with exact catalog evidence.
- Non-story Mortal progress uses `guardianQuestProgressUpdates` on an existing active `non_story` quest.
- Story q1–q3 hand-in uses `ready_to_turn_in -> completed` in afterlife.
- Story q4 uses successful `record_memory_scene` from active.
- Non-story hand-in uses `UpdateGuardians.completeQuest` in afterlife and preserves the complete classified historical snapshot.

Physical Mortal item transfer remains forbidden; use an echo, memory, knowledge trace, life-event evidence, or soul resonance.

## Cap and difficulty

Story and non-story available projections share `AbodePowerRules.GetGuardianQuestCap` and the same difficulty ceiling. A recognized story quest waits unprojected when no legal slot exists. New non-story offers are rejected when illegal. Active/completed history remains valid if power later decreases.

## Post-q4 guarantee

Completing q4 changes no generic Guardian eligibility flag. Goals, memory, relationships, projects, politics, social interactions, and `UpdateGuardians.offerQuest` remain available. An empty current offer list is valid; the contract guarantees future eligibility, not permanent filler quests.

## Story isolation assertions

A non-story quest cannot:

- use a catalog quest ID or `saref_main_story_catalog` origin;
- enter `guardianQuestlines[]`;
- grant a Saref revelation/advantage;
- change reveal stage or story actor/faction links;
- satisfy q1–q4 or deep-victory proof;
- invoke `record_memory_scene` as story completion.

Every worked test must compare the complete Saref state before/after a non-story lifecycle and prove semantic equality.
