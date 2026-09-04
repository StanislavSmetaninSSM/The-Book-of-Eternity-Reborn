# Skill-Scoped Roll Modifier Design

**Date**: 2026-09-05

**Source issue**: [GitHub #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)

**Related completed foundation**: [GitHub #1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)

**Spec Kit feature**: `specs/1536-complete-wound-materialization/`

**Status**: Approved in conversation on 2026-09-05

## Purpose

Physical wounds are authored for arbitrary Mortal settings. A wound may therefore
cause a broad disadvantage on all skill checks or a focused disadvantage on one exact
skill chosen by the GM. The current common `roll_modifier` effect profile can identify
`skill_check`, but it cannot identify which skill is being checked. Consequently, a
wound such as an injured hand can mechanically disadvantage either every skill check
or none; it cannot safely affect only Lockpicking.

This design extends the common effect profile rather than adding wound-only behavior.
Wounds, curses, Fate effects, structural bonuses, and other authorized effect sources
will use the same selector and the same roll-resolution boundary. The change remains
part of #1536 because wound materialization exposed the missing capability and requires
it for its accepted consequence model. It updates the completed #1535 effect contract
as a dependent global-contract extension; it does not reopen effect materialization or
create a second active-effect system.

## Accepted Product Decisions

1. The selector belongs to the common `roll_modifier` profile, not to the wound
   envelope.
2. Every `roll_modifier` has one explicit closed `scope`; absence never means `all`.
3. A modifier may cover all matching operations or exactly one canonical skill.
4. A skill-scoped modifier is legal only for `operations: ["skill_check"]`.
5. The GM may choose the consequence, but the client validates and binds its exact
   mechanical identity.
6. A selected `skillId` must identify one current, usable, canonical skill of the
   effect target when the effect is accepted. Unknown, idless, inactive, stale,
   duplicate, ambiguous, and Unicode-confusable identities are rejected.
7. If that skill later becomes unavailable, the effect and its source wound remain;
   the component is derived as dormant. It becomes applicable again only when the same
   permanent skill identity becomes usable again.
8. One skill-scoped component selects one skill and consumes one wound-consequence
   slot. Targeting two skills requires two components and two slots.
9. Existing advantage/disadvantage reduction remains unchanged after scope filtering.
10. Repository state, fixtures, sources, and examples move directly to the explicit
    scope contract. No migration, fallback parser, or implicit legacy default is
    permitted.

## Approaches Considered

### A. Mandatory discriminated `scope` on the common profile — selected

The payload always carries either a closed all-operation scope or a closed exact-skill
scope. The discriminator makes intent explicit, supports strict validation, and leaves
room for future separately designed scope kinds without overloading missing fields.

Advantages:

- one model for wounds and all other effect sources;
- no implicit behavior or ambiguous old payloads;
- exact cross-field validation;
- selectors participate naturally in canonical serialization and fingerprints;
- future extensions can add another discriminator only with their own contract.

Cost: every current `roll_modifier` definition and fixture must be updated in the same
cutover.

### B. Optional `skillId`, with absence meaning all — rejected

This is a smaller textual change, but it turns omission into mechanical authority,
makes incomplete GM output silently broad, and preserves two schema generations. It
also cannot clearly distinguish an old payload from a deliberately broad modifier.

### C. A separate `skill_roll_modifier` profile — rejected

This avoids changing the existing payload but duplicates contribution validation,
stacking, projection, snapshot, and roll-reduction behavior. The two profiles would
inevitably diverge and would make wounds special despite the capability being useful to
ordinary effects.

## Canonical Payload Contract

The common payload gains one required `scope` object.

Broad scope:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": {
    "kind": "all"
  }
}
```

Exact-skill scope:

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": {
    "kind": "skill",
    "skillId": "skill_lockpicking"
  }
}
```

The payload remains a closed object containing exactly `operations`, `contribution`,
and `scope`. The scope union is also closed:

| `kind` | Exact fields | Cross-field rule |
| --- | --- | --- |
| `all` | `kind` | May accompany any non-empty unique subset of registered roll operations. |
| `skill` | `kind`, `skillId` | Requires `operations` to equal exactly `["skill_check"]`. |

`scope.kind=all` forbids `skillId`. `scope.kind=skill` requires exactly one non-empty
canonical `skillId`; arrays, display names, aliases, multiple IDs, and extra fields are
invalid. A skill scope does not also select attacks, saving throws, spiritual arts,
characteristics, item uses, or other future check families.

Both broad and focused modifiers remain one `roll_modifier` component. They use the
existing source-authorized power limits and advantage/disadvantage vocabulary. The
selector neither raises contribution strength nor creates `great`/`dire` modes.

## Responsibility Boundaries

The implementation keeps four independently testable responsibilities.

### Structural profile validation

`EffectComponentProfiles` owns the closed JSON shape, discriminator, registered
operation values, contribution values, and cross-field rules. It does not read target
state and cannot decide whether a `skillId` exists.

### Target skill binding

A common accepted-state skill-scope authority resolves a structurally valid
`scope.kind=skill` against the exact effect target. It consumes a client-authored
catalog of the target's current usable active/passive skill rows, not names supplied by
the GM. “Usable” here means currently available for checks; `activeSkills` and
`passiveSkills` remain source collection names rather than an implicit eligibility
decision.

The authority requires:

- an exact permanent `skillId` on the selected row;
- exact ordinal identity matching;
- uniqueness across every eligible target skill row;
- no exact duplicate or Unicode-confusable competing identity;
- agreement between the skill owner and effect target;
- current availability at final accepted publication.

The GM chooses only from the catalog included in its materialization context. A skill
newly introduced in the same response is not selectable because it was absent from that
catalog; it may be selected in a later turn. If the response removes or disables a
catalogued selection before publication, final revalidation rejects the effect instead
of accepting an immediately dormant new component. A target without an authoritative
skill catalog may receive broad modifiers but not a skill-scoped one.

### Roll contribution resolution

A shared resolver consumes a trusted roll context containing:

- exact actor kind and actor ID;
- exact realm;
- one registered operation;
- nullable canonical `skillId`.

For `skill_check`, a concrete check supplies the exact selected skill identity when it
has one. A deliberately skill-less check may supply no `skillId`. Non-skill operations
do not acquire a skill identity merely because their narration mentions a skill.

The resolver first selects active components for the actor, realm, and operation. It
then applies scope:

- `all` passes;
- `skill` passes only for an exact matching `skillId` that is currently usable by the
  actor;
- `skill` does not pass when the context has no skill identity;
- a legitimately unavailable selected skill makes that component dormant;
- malformed, duplicate, or confusable current skill authority fails closed rather than
  being treated as ordinary dormancy.

Only the filtered contributions enter the existing reducer: advantage without
disadvantage yields advantage, disadvantage without advantage yields disadvantage,
both cancel to normal, and repeated same-direction sources do not escalate.

### Projection

Player and GM projections translate accepted scopes without making display text an
authority. A visible broad component reads, for example, “Помеха на все проверки
навыков”; a visible focused component reads “Помеха на проверки навыка «Взлом»”. The
ordinary player view does not expose `skillId`.

The current canonical display name is resolved from the exact identity. If the row
still exists but is unavailable, the consequence is shown as currently inactive. If
the row is absent, projection uses a neutral “конкретный недоступный навык” description
and never selects a similar name. Existing effect visibility rules remain authoritative:
this design does not reveal an otherwise hidden effect.

## Materialization Data Flow

```text
validated target + current skill catalog
                  |
                  v
GM receives bounded selectable skill rows
                  |
                  v
GM authors roll_modifier with explicit scope
                  |
          +-------+-------+
          |               |
          v               v
closed profile check   exact target-skill binding
          |               |
          +-------+-------+
                  v
accepted effect/wound composition
                  |
                  v
final target-skill revalidation
                  |
                  v
atomic carrier + identity + wound publication
```

The selector is part of the source-authorized component proposal. The client does not
silently replace an invalid exact skill with `all`, remove a selector, choose a similar
skill, or accept the remaining wound consequences. Any invalid component rejects the
whole materialization/repair transaction before permanent IDs or partial writes escape.
The normal repair loop receives a precise field path and code so the GM can resubmit a
complete corrected response.

## Runtime Dormancy and Reactivation

Dormancy is derived, not written as an independent lifecycle mutation. The accepted
effect retains its exact scope and source linkage while each roll evaluation checks the
current target skill authority.

- Removal, suspension, or loss of usability of the selected skill makes only that
  component non-contributing.
- The enclosing effect, wound, severity, treatment state, duration, and history do not
  change merely because the component is dormant.
- Restoring the same permanent canonical skill identity makes the component eligible
  again automatically.
- A newly created similar skill, a renamed alias, case variation, or Unicode-confusable
  identity never inherits the component.
- Reuse of a permanent ID for a semantically different skill remains invalid under the
  skill identity authority; it is not a reactivation route.

An effect may contain other components that remain active while one skill-scoped
component is dormant. Expiry, dispel, cure, source loss, and stacking continue to use
the common effect lifecycle and are not redefined here.

## Wound and Treatment Integration

The GM continues to author each physical wound independently: name, nature, symptoms,
prognosis, treatment routes, and a bounded graph of registered effect consequences.
There is no predefined wound catalog. A roll selector is one possible consequence
detail, not a wound definition.

Each `roll_modifier` component consumes one consequence slot regardless of scope. A
wound affecting two exact skills therefore needs two components and two slots. Display
text, symptoms, and technical wound linkage still consume no slot. Non-wound curses,
Fate effects, structural bonuses, and Saref effects do not consume wound slots unless
the accepted event explicitly materializes them as wound consequences.

Mortal treatment procedure checks use the common resolver:

- `modifierSource.kind=resolved_skill_tier` supplies the exact `skillId` of the
  requirement row actually selected as the roll modifier source, so broad and matching
  focused effects contribute;
- `modifierSource.kind=fixed_zero` has no selected skill identity, so only broad
  `scope.kind=all` effects contribute;
- a focused modifier for any other skill does not affect the procedure;
- filtering occurs before the existing advantage/disadvantage cancellation and Fate
  Shield logic, which otherwise remain unchanged.

Healing, severity reduction, consequence rematerialization, lineage retirement, and
full wound closure preserve their existing ownership. Reducing or healing a wound ends
or replaces its linked consequences through the wound/effect plan; changing skill
availability never impersonates healing.

## Canonical State, Replay, and Tamper Safety

`scope` and `skillId` are semantic effect data. Every path that serializes, clones,
caches, snapshots, compares, fingerprints, stages, repairs, rolls back, replays, or
rematerializes a `roll_modifier` must preserve them exactly.

Consequences include:

- accepted effect mechanics snapshots carry the complete scope payload;
- source definitions and materialized instances must agree on authorized scope;
- cache keys and semantic fingerprints change when `kind` or `skillId` changes;
- exact replay accepts only the originally sealed selector;
- changing `all` to `skill`, `skill` to `all`, or one `skillId` to another is a changed
  request, never replay;
- wound severity rematerialization preserves the authored selector for retained
  consequences while still assigning fresh effect/component identities where the wound
  lifecycle requires them;
- rollback restores the complete pre-transaction carriers, indexes, history, and
  output without leaving a partially rebound component.

No compatibility reader supplies a missing scope. Non-empty technical saves using the
old shape are unsupported by explicit project decision. Repository bootstrap state,
fixtures, examples, built-in definitions, and tests are converted together.

## Error Handling

The contract distinguishes structural, authority, and runtime failures.

Structural failures include missing/unknown `kind`, illegal extra fields, a missing or
forbidden `skillId`, multiple selected skills, an empty/duplicate operation list, or
`scope.kind=skill` with anything other than exactly `skill_check`.

Binding failures include unknown, idless, unavailable, stale, wrong-owner, exact-
duplicate, or Unicode-confusable skill identities and targets without an authoritative
catalog. These reject the accepted effect plan atomically and identify the selector
path in the repair report.

Runtime absence of a previously valid skill is ordinary dormancy. Corrupt or ambiguous
current skill authority is not: the affected mechanical resolution fails closed so the
client does not silently alter a roll under uncertainty.

## GM and Player Contract Synchronization

The implementation must update every GM-facing surface that teaches or accepts
`roll_modifier`, including the effect materialization contract/example and the wound
materialization contract/example. The GM context must provide bounded selectable
`skillId`/display-name rows for the exact target and explicitly teach the difference
between `all` and `skill`.

Because wounds cover both Mortal and afterlife lifecycles, implementation must inspect
and update or record a no-change rationale for:

- Mortal and afterlife GM prompt entrypoints;
- `OtherGuides/Effect_Materialization_Contract.md`;
- `OtherGuides/Wound_Materialization_Contract.md`;
- `Examples/E_CLI_Effect_Materialization.txt`;
- `Examples/E_CLI_Wound_Materialization.txt`;
- `Examples/E_CLI_Afterlife_Turns.txt` where it contains common roll modifiers;
- `Examples/example_validation_manifest.json`;
- effect, wound, prompt, and afterlife documentation/source guards;
- both #1535 effect artifacts and #1536 wound artifacts where they state the old
  two-field payload or unscoped treatment filtering.

At least one GM example must demonstrate a physical wound with a broad skill-check
consequence and one with an exact-skill consequence. Another common-effect example must
prove the selector is not wound-specific. Player-facing examples must show readable
scope text without exposing technical IDs.

## Testing Strategy and Lane Placement

Implementation follows test-driven development with the smallest owning failure first.

Fast, pure contract tests cover:

- both valid scope variants and their exact closed shapes;
- every discriminator/cross-field rejection;
- exact, case-sensitive, Unicode-confusable, duplicate, idless, stale, unavailable,
  wrong-owner, and no-catalog binding cases;
- broad, exact-match, exact-mismatch, and identity-less check filtering;
- existing advantage/disadvantage cancellation after filtering;
- treatment `resolved_skill_tier` and `fixed_zero` behavior;
- one-component/one-consequence-slot accounting;
- safe player projection and hidden-effect visibility preservation.

Integration or lifecycle lanes cover:

- materialization through ordinary effects and wound-owned effects;
- skill loss, dormancy, exact restoration, and non-inheritance by a similar new skill;
- final-publication revalidation when the same response disables the selected skill;
- carrier/index/cache/fingerprint agreement;
- restart, exact replay, changed-selector rejection, rollback, and wound consequence
  rematerialization;
- documentation examples and source-manifest validation.

File, cache, restart, replay, and full lifecycle tests must not be moved into `Fast` to
gain convenient coverage. During implementation use bounded focused filters, then one
meaningful Fast checkpoint. Before merge run one PreMerge control without an adjacent
duplicate Fast run. Because common GM examples and afterlife documentation are touched,
run the required focused documentation guards and the project's conditional
FullValidation control. Limits are adjustable with measured justification; coverage is
not weakened merely to fit an obsolete historical timeout.

## Spec Kit and Execution Integration

Before implementation resumes, the approved #1536 Spec Kit artifacts must be amended
to state:

- the mandatory `scope` union in the common effect component contract;
- exact target skill binding and derived dormancy/reactivation;
- trusted roll context and treatment behavior;
- consequence-slot accounting;
- direct cutover and documentation obligations;
- RED, GREEN, lifecycle, documentation, and verification tasks for this sub-slice.

The new sub-slice executes before the currently paused final wound documentation and
control work. That work then resumes against the final scope-aware schema, avoiding a
second rewrite of examples and source guards. Existing uncommitted, user-authorized
documentation work is preserved and incorporated rather than discarded.

## Non-Goals

- No catalog of predefined physical or spiritual wounds.
- No free-form executable mechanics outside registered effect components.
- No multiple-skill selector in one component.
- No selector by display name, alias, fuzzy match, or GM-supplied label.
- No skill selector for attack rolls, saving throws, characteristics, spiritual arts,
  or items.
- No new advantage-strength levels or change to stacking/cancellation.
- No healing, severity, combat-damage, experience, or skill-progression redesign.
- No persistent mutable “dormant” flag that can drift from current skill authority.
- No migration or compatibility support for missing `scope`.

## Completion Criteria

The extension is complete only when:

1. Every accepted `roll_modifier` has exactly one valid explicit scope.
2. A focused scope can bind only one current canonical skill of the exact target.
3. All mechanical consumers filter through trusted operation/skill context before the
   unchanged contribution reducer.
4. Skill loss derives dormancy without deleting or healing the source, and exact
   identity restoration re-enables the component without name-based inheritance.
5. Wound consequence slots, treatment checks, healing, rematerialization, replay,
   rollback, caches, and fingerprints preserve the agreed semantics.
6. Existing effect sources and repository fixtures use explicit `scope.kind=all` under
   the direct cutover.
7. GM prompts, effect/wound contracts, Mortal/afterlife examples, manifests, player
   projections, and documentation/source guards teach and prove the complete behavior.
8. Focused, Fast, conditional documentation, lifecycle, and PreMerge controls pass in
   their appropriate lanes without making Fast a filesystem/lifecycle suite.
