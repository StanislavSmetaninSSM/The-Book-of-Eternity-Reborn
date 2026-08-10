# Contract: Saref Story Catalog and GM Context

**Issues**: #1519, #1520

## Purpose

Guarantee that the GM knows the complete hidden story from the first turn without eagerly creating mutable story actors or exposing private content to the player.

## Packaged input

- `BookOfEternityClient/story_content/saref/catalog.json`
- ten `system_guardians/built_in/<preset>/guardian_materialization.json` files
- ten `system_guardians/built_in/<preset>/saref_questline.json` files
- `saref_actor_materialization.json`
- `wings_faction_materialization.json`

All files are application-owned, current-schema JSON. User presets, game state, GM output, and network content are not catalog inputs.

## Loader guarantees

On first use, `SarefStoryCatalogService` MUST:

1. Resolve every inventory path below an allowed packaged root.
2. Parse every document without fallback.
3. Verify the deterministic digest.
4. Verify exactly ten approved Guardians and forty exact quests.
5. Verify case-sensitive identity, four ordered quests per Guardian, q4 reward completeness, template cross-links, and no unlisted semantic file.
6. Render and verify a complete compact index no larger than 32 KiB UTF-8.
7. Cache the immutable typed catalog and rendered fragments for the process lifetime.

Any failure is an installation/content-integrity error. The game MUST NOT load a partial catalog, synthesize missing entries, ask the GM to repair packaged content, or bind a game to another version.

## New Game binding

Every current-schema New Game MUST create `game_state/meta/main_story_saref_state.json` schema 2 with an exact `catalogBinding`, even when the selected Guardian is freeform or unrelated. Missing or mismatched binding is invalid; no runtime migration exists.

## Turn context output

Every prepared Mortal World, Chaos Sea, and Shining Abode GM turn MUST include one GM-private compact fragment containing:

- all ten exact Guardian IDs and names;
- all forty exact quest IDs, titles, ordinals, and Guardian ownership;
- q4 revelation/advantage roles;
- story/non-story classification and transition rules;
- exact instructions for requesting/using a full line package;
- the current game binding and current relevant progress summary.

The fragment MUST be present before any story Guardian, Saref, Wings, trace, or progress exists.

## Full-package relevance

The composer attaches the exact Guardian/questline package when any exact reference is relevant through current action, pending request, story state, latent trace, Guardian materialization/attraction, active Guardian, Saref/Wings action, or explicit catalog ID. It MUST:

- use exact catalog membership, never fuzzy name/prose matching as authority;
- deduplicate the same line;
- include all distinct relevant lines;
- allow only `absent -> latent` when no full package is present;
- never silently truncate a relevant package.

## Privacy

Catalog and full-package fragments are GM-private prompt inputs. They MUST NOT be copied to narrative/interface output, console/browser DTOs, status panels, logs, error text shown to the player, or receipts. Player projections resolve only canonical reveal-filtered state.

## Local observability

Allowed local log facts: catalog ID/version/digest, one-time load result, compact byte count, selected package IDs/count, and exact content file/JSON pointer on failure. Full package prose and player action text are not logged.

## Verification examples

- Empty New Game in each realm receives 10/40 compact index.
- One exact trace attaches one full line and not nine unrelated lines.
- Two relevant lines attach both once.
- Missing/duplicate/case-variant quest, wrong q4 reward, unlisted content file, digest mismatch, or index overflow blocks load.
- Player-facing output remains byte-free of private catalog markers and receipts.
