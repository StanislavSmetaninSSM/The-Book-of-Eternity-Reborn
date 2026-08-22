# Resource Definition and Command Contract

## 1. Purpose

This contract is the only GM-authored entrypoint for resource definition creation, capacity lifecycle, and ordinary damage/restore/spend/gain. The GM never writes canonical definitions, live values, maxima, history, IDs, phases, policies, after-images, or paths.

## 2. Response fields

The shared Mortal/afterlife response may contain:

```json
{
  "resourceDefinitionCreations": [],
  "resourceCapacityChanges": [],
  "resourceChanges": []
}
```

All three fields are optional arrays. They are staged together in the transient resource command root. Any unknown command-root property, duplicate property, wrong root, or wrong array/object type blocks the complete accepted turn.

Legacy `currentHealthChange`, `currentEnergyChange`, `currentPoiseChange`, `inventoryItemsResources`, `NPCInventoryResourcesChanges`, direct NPC/combat/vehicle health fields, item durability/resource fields, and afterlife action-economy/charge current values are forbidden after cutover.

## 3. Definition creation

`resourceDefinitionCreations[]` is allowed only during accepted world/bootstrap materialization or an explicit setting-expansion transition.

Required command fields:

```text
definitionRef
definition
eventRef
reason
```

The nested definition contains every public field in [data-model.md §3](../data-model.md#3-resource-definition-catalog) except `materialization`. The client validates the complete closed policy, exact/confusable uniqueness, and setting-expansion authority; then it allocates `definitionId`, seal, and chronology exactly once.

An existing resource key/version cannot be resent, edited, removed, or aliased. A same-turn capacity command may refer to the exact `definitionRef`; the ref is consumed and never canonical.

The common catalog is for bounded consumable/mechanical reserves, not a universal numeric-property adapter. Definition creation fails closed for these reserved out-of-scope keys: `money`, `ink_feathers`, `light_sparks`, `treasury_balance`, `faction_resource_ledger`, `experience`, `mastery`, `level`, `reputation`, `relationship`, `owner_bond_level`, `spiritual_power`, `spiritual_strain`, and `spiritual_shield`. Those values remain in their established currency, treasury, faction, progression, relationship, or spiritual-axis authorities. A setting may still define a genuinely new bounded reserve such as `mana`; spelling an excluded mechanic as a setting definition does not bypass admission.

## 4. Capacity lifecycle

`resourceCapacityChanges[]` uses this closed shape:

```text
operation
target
resourceKey | resourceDefinitionRef
capacity?             # required by initialize/reconfigure
currentDisposition?   # required by initialize/reconfigure as defined below
source
eventRef
reason
```

Allowed operations:

| Operation | Preconditions | Disposition |
| --- | --- | --- |
| `initialize` | Coordinate absent; exact owner and definition accepted | `initialize_from_definition` |
| `reconfigure` | Coordinate active/suspended; exact capacity source accepted | `preserve`, `clamp_to_new_maximum`, or `scale_ratio_exact` |
| `suspend` | Coordinate active; owner lifecycle permits suspension | omitted |
| `resume` | Coordinate suspended; owner/source permits resumption | omitted |
| `retire` | Owner reaches terminal lifecycle and terminal evidence is complete | omitted |

For `definition_fixed`, submitted `capacity` is forbidden. For `instance_fixed`, it is `{ "kind":"instance_fixed", "maximum": <exact positive number> }`. For `registered_formula`, it names only the exact formula key already sealed in the definition; all formula inputs come from validated owner state.

The version-1 registered keys are exactly `mortal_health_capacity_v1`, `mortal_energy_capacity_v1`, `mortal_poise_capacity_v1`, `afterlife_spiritual_action_points_v1`, and `afterlife_return_gacha_attempts_v1`. Their input variants are closed typed client records; the GM cannot supply a parameter bag. Mortal player variants consume the exact permanent characteristics used by the existing maximum equations, while other allowed Mortal owners consume their validated materialization maximum. Afterlife variants consume Spirit Focus/conflict-side authority or Guardian/Shining return-cycle authority. Every variant binds the exact owner-authority fingerprint, and every resolved capacity binds the sealed definition ID/seal.

Initialization is resolved after capacity. `minimum`, `maximum`, and `fixed` are sealed static policies; `registered_formula` uses one of the same closed typed functions. The initialization fingerprint includes the resolved-capacity fingerprint and maximum, so a stale capacity cannot initialize a coordinate.

The GM does not author current state. Initialization uses the definition initialization policy. Reconfiguration applies only the selected closed disposition and fails on inexact ratio or unauthorized clamp.

## 5. Ordinary changes

Each `resourceChanges[]` entry has exactly:

```text
operation
target
resourceKey
amount
source
eventRef
reason
```

Allowed operations are `damage`, `restore`, `spend`, and `gain`. `amount` is an exact positive number aligned to the sealed definition quantum. A definition must allow both the target owner kind and requested operation.

`target` is `{kind,targetId}` for an accepted existing owner or `{kind,targetRef}` for an accepted same-turn owner. Exactly one ID/ref is present. Display names, aliases, paths, indexes, case variants, and historical identities are not accepted.

`source` uses one of two closed route-specific shapes. `action_cost`, `combat_outcome`, and `narrative_outcome` are exactly `{kind}`. After validating the exact global command ordinal, the client derives their plan-local `sourceId` from `eventRef`, binds the source to the resolved target owner, and fingerprints the full accepted occurrence. Raw input cannot submit or rename that identity. Durable owner-bound item routes are exactly `{kind,sourceId}` as described below. Source kind decides phase, priority, applicable floor/cap binding, and allowed operation; raw input cannot override them.

For item operations, `local_item_cost` and `local_item_outcome` use the exact permanent `itemId` as `sourceId` and are bound to that same item owner coordinate. They authorize ordinary use/fire/reload costs or repair/outcome changes only for their bound item. A case variant, historical item, different item target, or carrier-local alias fails with no writes; moving the item does not change this authority.

`eventRef` must be the exact ordinal accepted event for that command. Swapped, reused, stale, missing, or future event refs fail closed.

Nested FullParty resource packets use the same ordinary command grammar with the additional recipient restrictions in [resource-full-party-interaction.md](resource-full-party-interaction.md). They are validated and staged as outbound packets and are never applied to the originating client's ledger.

## 6. Forbidden raw data

At every depth, reject:

- canonical current/maximum/state/chronology/history/transition fields;
- permanent client IDs for same-turn definitions/owners/operations/transitions/receipts;
- phase, priority, dependencies, trigger graph, floor/cap policy, applied amount, result event, or final value;
- file paths, carrier paths, JSON pointers, arbitrary selectors, hashes, seals, fingerprints, snapshot/lease/rollback/repair fields;
- nested canonical root wrappers or pending/receipt DTOs.

## 7. Consumption and replay

The command root remains present until the exact accepted plan is published and post-validated. Successful publication deletes it atomically with every after-image. Failure leaves/restores the command bytes for coherent retry/repair.

An exact semantic replay returns the prior accepted result without another history entry. Reusing an event/origin/coordinate/operation tuple with different amount/source/policy semantics is protected and non-actionable.

## 8. GM examples

At least one Mortal and one afterlife worked example must show:

- legal definition or owner initialization where applicable;
- ordinary resource mutation with exact source/target/event;
- absence of current/max/history/IDs/paths;
- a failed legacy/direct-write example and the correct command replacement.
