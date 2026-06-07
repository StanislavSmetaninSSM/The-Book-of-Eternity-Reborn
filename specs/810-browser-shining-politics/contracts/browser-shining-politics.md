# Contract: Browser Shining Abode Politics

## Command Surface

The browser exposes three local-turn Shining Abode commands in addition to the read-only `/shining_politics` overview:

| Command ID | English alias | Russian alias | Browser status |
| --- | --- | --- | --- |
| `shining_faction_founding` | `/shining_faction_founding` | `/основание_сияющей_фракции` | mutating parity |
| `shining_faction_realignment` | `/shining_faction_realignment` | `/перестройка_сияющей_фракции` | mutating parity |
| `shining_faction_leadership` | `/shining_faction_leadership` | `/смена_главы_сияющей_фракции` | mutating parity |

Exact labels may be refined during implementation, but default browser action metadata must remain Russian/player-facing.

## Prompt Contract

All prompts use existing browser prompt answer shapes. No new HTTP endpoint or React gameplay handler is introduced.

### Founding Submit

Required answers:

- `faction_name`
- `hall_name`
- `charter_summary`
- `hall_description`
- `favored_archetype`
- `patron_effect_family`
- `hall_secondary_service_tag` (optional)
- `supporting_resident_ids`
- `confirm_shining_politics_write`

Submit result:

- Valid submit writes an existing `PendingShiningFactionFoundingRequest`.
- Invalid submit returns a player-facing blocker and writes nothing.

### Realignment Submit

Required answers:

- `resident_id`
- `realignment_mode`
- `target_faction_id` when mode is accepted transfer
- `confirm_shining_politics_write`

Submit result:

- Valid submit writes an existing `PendingShiningFactionRealignmentRequest`.
- Invalid submit returns a player-facing blocker and writes nothing.

### Leadership Submit

Required answers:

- `faction_id`
- `transition_mode`
- `candidate_head_choice` when mode requires a candidate
- `supporting_resident_ids` when mode requires supporters
- `confirm_shining_politics_write`

Submit result:

- Valid submit writes an existing `PendingShiningFactionLeadershipTransitionRequest`.
- Invalid submit returns a player-facing blocker and writes nothing.

## Runtime Contract Stability

Browser parity must not alter:

- pending file names
- `requests` bundle shape
- request DTO field names
- GM response/receipt tags
- scheduler/normalizer behavior
- GM-authored closure guidance

If any of these change, update afterlife GM documentation, examples, manifest, and documentation coverage tests in the same branch.
