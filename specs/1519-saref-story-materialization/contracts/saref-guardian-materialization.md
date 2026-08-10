# Contract: Predvechnye Guardian Materialization

**Issues**: #1519, #1521

## Trigger

An exact registered Guardian may materialize through an existing built-in selection, deterministic attraction, or catalog-backed story appearance. The trigger must name the exact `guard_system_<preset>_001`, current catalog binding, and existing system-Guardian manifest. Display name, prose, or archetype cannot select the template.

## Required atomic bundle

One accepted materialization MUST publish together:

1. The complete exact Guardian in `game_state/meta/guardians.json`.
2. The matching common afterlife profile with `actorType=guardian`, exact actor ID, and complete immutable Actor Materialization envelope.
3. One deterministic initial first-person entry for that Guardian in `game_state/meta/guardian_thought_journal.json`.
4. Exact packaged preset/template/catalog bindings.
5. Exact abode/plane/location reference owned by #1514.
6. Every fixed quest view derivable from already accepted story progress for that exact Guardian.
7. The active-Guardian mirror when the existing route makes this Guardian active.

If any component is missing, incomplete, duplicated, mismatched, or invalid, none of the proposed roots becomes canonical.

## Template ownership

The packaged template owns identity, names/presentation, appearance, personality, worldview, motivation, goals/plan, authored arts, relationship posture, capabilities, intentional empty sections, initial thought, and story/location bindings. Runtime may fill only turn/time/request/session fields and the documented current-soul relationship baseline.

User preset overrides cannot alter story authority. GM output cannot replace the complete template with a generic shell or add mechanics inferred from prose.

## Existing actor and idempotence

- A new exact Guardian ID may use the full materialization route once.
- Retry with the same accepted receipt is idempotent: no duplicate Guardian, profile, memory entry, or projection.
- A different receipt/template/digest for an existing exact ID is rejected.
- Later changes use dedicated Guardian/profile/journal commands and cannot rewrite the receipt or authored binding.
- A trace or progress row created before materialization remains authoritative and is projected after the bundle validates.

## Quest projection

The materialization operation reads story state; it does not invent progress. `latent` has no ordinary quest view. Eligible `recognized`, `active`, `ready_to_turn_in`, and `completed` rows are projected from immutable templates into the Guardian and profile. Projection failure rejects the whole materialization.

## Memory rule

The initial materialization reaction is written to the thought journal only. Do not duplicate it through `UpdateGuardians.addMusings`. Existing entries remain an exact semantic prefix and the deterministic entry ID prevents replay.

## Repair boundary

A Guardian materialization repair may name only the exact Guardian, exact profile, exact thought entry, exact story projection, and exact location reference. It may not modify another Guardian, another actor, unrelated story progress, currencies, projects, politics, or factions.

## Verification matrix

Run the same positive/negative bundle tests for Azalia, Brann, Elyara, Ilarion, Lissara, Lucian, Myriel, Seret, Varak, and Veyra across selection/attraction paths. Include partial profile, wrong ID/case, missing thought, wrong location, forged binding, retry, and pre-existing latent-progress cases.
