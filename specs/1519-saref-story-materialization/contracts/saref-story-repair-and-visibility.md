# Contract: Saref Story Repair, Rollback, and Visibility

**Issues**: #1519, #1520, #1521, #1522

## Failure classes

### Packaged content failure

Examples: missing template, invalid JSON, wrong inventory, digest mismatch, duplicate/case-variant ID, incomplete q4 binding, compact-index overflow.

Behavior: fail catalog load/New Game/turn preparation with a local installation-content error. Do not generate a GM repair packet, synthesize content, fall back to Markdown, or continue with a partial story.

### Current-schema binding/schema failure

Examples: missing story state, schema 1, missing binding, digest/version mismatch.

Behavior: fail closed. Because the game is unreleased, fix/reset the development session or repository fixture. Product runtime MUST NOT rewrite the binding or migrate the state.

The contract registry must describe Saref story state as current-schema authority in Mortal World, Chaos Sea, and Shining Abode; it must not retain the old `absent legacy file is valid` statement.

### Accepted-turn state failure

Examples: invalid story transition, stale acceptance request, partial Guardian bundle, forged reward, projection disagreement, Saref/Wings cross-link mismatch.

Behavior: reject the proposed accepted turn, restore all targeted roots from the validated pre-turn snapshot, and issue a bounded repair packet only for mutable canonical output/state.

## Repair packet contents

A Saref story repair packet MUST include:

- `kind` selecting the exact repair family;
- current catalog binding and digest;
- exact Guardian/quest/actor/faction/request IDs;
- `targetFiles[]` and exact JSON pointers;
- allowed command/transition mode;
- expected pre-turn status and permitted post-status;
- protected files/entities and semantic hashes;
- a complete minimal repair template;
- instruction to finish through the normal validation-repair completion path.

It MUST NOT instruct the GM to read implementation code, rewrite packaged catalog files, replace whole unrelated roots, or infer a new identity.

## Allowed target boundaries

- Catalog/binding problems: no GM repair target.
- Quest transition: story state plus exact derived Guardian/profile views.
- Non-story offer/accept/progress/complete: exact Guardian and documented ordinary audit roots; Saref state protected.
- Guardian materialization: exact Guardian/profile/thought entry/location reference and its projections.
- Saref materialization: exact profile and actor link.
- Wings materialization: exact Shining faction and faction link.

Every unrelated Guardian, actor, profile, memory entry, faction, location, quest, project, currency, and story branch remains semantically identical.

## Transaction/rollback rule

For multi-root changes:

1. Load and verify the validated pre-turn snapshots.
2. Build all proposed roots in memory.
3. Run catalog, schema, command/request, per-root, cross-link, and protected-state validation.
4. Publish only after all checks pass.
5. Verify written content.
6. On any write/post-write failure, restore every targeted pre-turn root before returning failure.

A result is successful only when all target roots are present and mutually consistent.

## Visibility projection

GM context and player projection are separate data paths. Player DTOs must be built from canonical reveal-filtered state, not from catalog/template objects. The filter applies equally to:

- console Guardian/quest/Saref/faction/status panels;
- browser command menus, detail cards, notifications, and results;
- narrative/interface output validation;
- logs and repair messages visible to the player.

Before permitted reveal, player output must not contain exact hidden actor/faction IDs, catalog/template IDs, digest, full questline content, receipts, private truth, GM instructions, or unrevealed reward mechanics.

After reveal, only registered public fields become visible. Internal enums use Russian in-world labels, and dynamic authored text is escaped/sanitized before Spectre.Console or browser rendering.

## Repair visibility

Developer/GM repair diagnostics may identify exact internal targets but remain outside ordinary player output. A player-facing failure message states that the current story action could not be accepted and that prior state was preserved; it does not reveal the hidden reason when that reason itself is secret.

## Tests

- Semantic pre/post comparison for every rejected multi-root operation.
- Exact repair target and protected-root assertions.
- Catalog failures produce no mutable repair request.
- Hidden console/browser/string scans contain none of the private markers.
- Revealed projections contain only approved public fields and equivalent console/browser semantics.
- Dynamic story/quest/faction text is escaped in console and safely rendered in browser.
