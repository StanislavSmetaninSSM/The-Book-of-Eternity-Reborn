# Feature Specification: Complete Wound Materialization and Healing

**Feature Branch**: `1536-complete-wound-materialization`

**Created**: 2026-08-26

**Status**: Clarified; ready for planning

**Input**: Materialize independently treatable physical and spiritual wounds with complete creation, consequence, diagnosis, treatment, recovery, healing, history, command, provider, and GM-authoring lifecycles.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536); depends on completed effect materialization [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
- **Issue type**: Epic enhancement and audit hardening task
- **Spec Kit justification**: The feature spans multiple sessions and changes player-facing commands, console/browser parity, canonical state, validation, normalizers, resource use, afterlife conflict, entity progression, GM prompts, documentation, examples, and rollback behavior.
- **Contract scope**: Player-facing Mortal World and afterlife mechanics; GM-facing authoring and repair; runtime state; validation; effects; resources; progression; documentation; examples; console; browser frontend.
- **Save compatibility**: Not required under the pre-release constitution and the user's explicit decision. Repository bootstrap state, fixtures, examples, and tests move directly to the new contract.
- **Out of scope**: A catalog of ready-made wounds; migration of non-empty legacy saves; replacement of the effect engine; a general combat-damage or experience redesign; automatic post-battle fatigue; automatic soul dissipation; a full afterlife inventory; a mandatory public clinic for the whole Shining Abode. If Saref's `memory_suppression` is only audit metadata rather than a complete independent effect, a separate linked issue MUST be created before #1536 closes.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Receive a Complete and Fair Wound (Priority: P1)

As a player, I can receive a unique physical or spiritual wound whose cause, severity, symptoms, consequences, treatment prospects, and acquisition narration agree with the event that produced it.

**Why this priority**: Wound creation is the authority boundary on which every treatment, recovery, command, and provider workflow depends.

**Independent Test**: Resolve one wound-capable event, have the GM choose a legal wound below or at the permitted maximum, and verify that the wound, owned effects, narrative, notification, and history appear together while an overpowered or contradictory proposal publishes nothing.

**Acceptance Scenarios**:

1. **Given** a formal event that permits severity II, **When** the GM chooses no wound, **Then** the event can publish without a wound unless a pre-existing source contract guaranteed one.
2. **Given** the same event, **When** the GM authors a unique severity I or II wound with a complete consequence and treatment contract, **Then** the event, wound, effects, history, and player notification publish atomically.
3. **Given** an event capped at severity I, **When** the GM proposes severity III, **Then** nothing publishes and the GM receives the legal severity range.
4. **Given** a proven source contract that guarantees a wound, **When** the trigger succeeds and the GM omits the wound, **Then** publication is rejected until the promised result is materialized.
5. **Given** an accepted wound, **When** the player inspects it, **Then** no internal identity, validation, or agent terminology is exposed.

---

### User Story 2 - Diagnose and Treat a World-Specific Physical Wound (Priority: P1)

As a Mortal player, I can understand and treat a wound using methods appropriate to the current universe rather than selecting from a universal catalog of wounds or medicines.

**Why this priority**: Mortal wounds derive their interest from setting-specific causes, symptoms, resources, skills, providers, and cures.

**Independent Test**: Materialize two mechanically valid but fictionally unrelated physical wounds in different setting styles, diagnose their available routes, satisfy one route for each, and verify deterministic resource use and state transitions without any pre-authored wound-name lookup.

**Acceptance Scenarios**:

1. **Given** a newly authored physical wound, **When** it is accepted, **Then** it has at least one complete treatment route and any hidden route has a reachable discovery path.
2. **Given** a route requiring several resources and conditions, **When** one required element is missing, **Then** treatment cannot begin and nothing is consumed.
3. **Given** a valid procedure, **When** its sealed check succeeds, partially succeeds, or fails, **Then** only its predeclared outcome and resource policy apply.
4. **Given** an uninterrupted course, **When** every dose and time milestone completes, **Then** the declared wound transition occurs exactly once.
5. **Given** a source explicitly promising guaranteed healing, **When** its exact capability and requirements are proven, **Then** the deterministic result occurs without an improvised GM override.
6. **Given** a player proposes a plausible new cure, **When** the GM materializes an evidence-backed alternative route, **Then** it becomes a legal alternative without rewriting earlier history.

---

### User Story 3 - Suffer or Avoid a Spiritual Wound in Conflict (Priority: P1)

As an afterlife player, I can enter spiritual conflicts whose danger is declared in advance, whose wounds remain optional GM-authored consequences within deterministic limits, and whose defeat never forces soul dissipation.

**Why this priority**: Persistent spiritual wounds must integrate with the existing strain system without turning every battle or training scene into mandatory injury.

**Independent Test**: Run representative training, controlled, hostile, and annihilation conflicts through strain transitions and prove wound prohibitions/caps, GM choice, one-wound-per-side behavior, defeat outcomes, and optional dissipation.

**Acceptance Scenarios**:

1. **Given** a training conflict, **When** strain changes, **Then** no spiritual wound can be created without an explicit prior escalation.
2. **Given** a controlled conflict, **When** the calculated maximum exceeds II, **Then** the legal maximum remains II.
3. **Given** an eligible hostile transition, **When** the GM chooses a lower wound or no wound, **Then** that choice is accepted.
4. **Given** a side already received its new wound in the conflict, **When** another wound opportunity is accepted, **Then** the existing conflict wound may worsen but a second conflict wound cannot be created.
5. **Given** an older active wound, **When** no explicit action targeted it, **Then** the new exchange cannot silently re-traumatize it.
6. **Given** a non-training defeat, **When** the conflict resolves, **Then** a bounded defeat outcome prevents immediate repetition of the same aggression.
7. **Given** annihilation mode and valid winner authority, **When** the winner chooses a softer outcome, **Then** soul dissipation does not occur.

---

### User Story 4 - Heal Spiritual Wounds and Advance the World (Priority: P1)

As an afterlife player or entity, I can diagnose, actively heal, or naturally recover from spiritual wounds using standard spiritual arts, while treatment attempts remain costly in combat or advance the world outside combat.

**Why this priority**: Persistent wounds are only viable if every player and non-player entity has bounded paths out of them without an ad hoc inventory system.

**Independent Test**: Exercise insufficient-tier diagnosis, every healing result band, combat action cost, self-healing, provider healing, natural recovery at tiers 0 and V, and entity progression/recovery.

**Acceptance Scenarios**:

1. **Given** a healer below the wound's severity, **When** treatment is considered, **Then** diagnosis can identify the limitation but severity cannot be reduced.
2. **Given** a sufficient healer in conflict, **When** healing is attempted, **Then** the accepted roll changes the wound according to its result and consumes a counterable action costing at least 2 OD.
3. **Given** self-healing outside conflict, **When** one session is completed, **Then** no currency or item is consumed, one safe world cycle advances, and no second attempt for that wound occurs in the same cycle.
4. **Given** a failed safe healing session, **When** the cycle completes, **Then** the selected wound receives only its ordinary natural recovery and the world still advances.
5. **Given** a tier-0 critical spiritual wound with uninterrupted safe cycles, **When** 20 cycles complete, **Then** it heals naturally.
6. **Given** a tier-V critical spiritual wound with uninterrupted safe cycles, **When** four cycles complete, **Then** it heals naturally.
7. **Given** a wounded Guardian or resident, **When** safe progression cycles complete, **Then** its wound and Spiritual Healing development advance through the same rules used for other entities.
8. **Given** a reachable consenting Guardian or other entity, **When** the player has sufficient Spiritual Healing, **Then** the player can treat that exact entity.

---

### User Story 5 - Inspect Active Wounds and Choose a Target Safely (Priority: P2)

As a player, I can inspect my current wounds and start treatment without knowing technical identities or confusing healed history with active problems.

**Why this priority**: The mechanic is unusable if players cannot discover wounds, distinguish current from historical state, or safely select nearby targets.

**Independent Test**: Compare console and browser flows for active wound detail, History, self-treatment, same-name nearby targets, unknown diagnosis, stale reachability, and hidden information.

**Acceptance Scenarios**:

1. **Given** active and healed wounds, **When** `/раны` is opened, **Then** only active current-realm wounds appear in the primary list.
2. **Given** healed wounds, **When** History is opened, **Then** readable terminal records appear separately.
3. **Given** `/лечить`, **When** target selection opens, **Then** Self appears first and visible reachable nearby entities follow without requiring typed names or IDs.
4. **Given** two nearby entities with the same name, **When** they are listed, **Then** visible contextual descriptions distinguish them while exact identities remain hidden.
5. **Given** a target that moved away before confirmation, **When** treatment is committed, **Then** the stale selection is rejected before resource use.
6. **Given** a hidden NPC wound or treatment route, **When** the player lacks discovery authority, **Then** private details are not exposed.
7. **Given** the same canonical state, **When** console and browser users follow the same action, **Then** they receive equivalent choices, costs, results, and error explanations.

---

### User Story 6 - Find Afterlife Healing Help (Priority: P2)

As an afterlife player, I can seek a known Chaos Sea healer, discover visible healers in Shining factions, pay a public quoted price, or negotiate another form of compensation without bypassing the common healing rules.

**Why this priority**: Provider access gives players alternatives to self-healing and natural recovery while preserving faction play and world progression.

**Independent Test**: Treat one wound through Elyara's command service, one through negotiated compensation, and inspect several Shining factions with different healer tiers and access policies.

**Acceptance Scenarios**:

1. **Given** first entry to the Chaos Sea, **When** the player seeks healing, **Then** Elyara and the Lazaret are discoverable and her tier cannot be downgraded below V.
2. **Given** a public treatment attempt, **When** the player confirms the displayed Ink Feather price, **Then** the exact quoted amount is paid once even on a valid failed or partial attempt.
3. **Given** a canceled or rolled-back attempt, **When** publication fails, **Then** no payment remains consumed.
4. **Given** an accepted roleplay agreement, **When** treatment resolves, **Then** the agreed favor, debt, quest, or free aid replaces currency but not the healer-tier gate, roll, target, or world-cycle cost.
5. **Given** a materialized Shining faction, **When** its visible roster is inspected, **Then** at least one resident has the visible primary `healing_support` role and a valid tier I-V.
6. **Given** a faction healer without public-service access, **When** the player lacks the required relationship or agreement, **Then** the healer remains visible but command treatment is unavailable.

---

### User Story 7 - Recover Safely from Invalid GM Output or Retry (Priority: P1)

As the game-running system, I can reject and repair an invalid wound proposal without exposing private authority or leaving partial state, duplicate charges, duplicate wounds, or contradictory narration.

**Why this priority**: A complex GM-authored constructor must fail safely; prompt instructions alone cannot guarantee state integrity.

**Independent Test**: Inject invalid owner, severity, slot, effect link, treatment, resource, and retry data at each phase and verify bounded repair plus complete rollback.

**Acceptance Scenarios**:

1. **Given** an invalid wound proposal, **When** validation fails, **Then** the GM receives only the offending paths, legal bounds, and safe context.
2. **Given** a repair attempt, **When** unrelated valid authored content is unchanged, **Then** it remains preserved for the repair round.
3. **Given** a changed event, roll, or target during repair, **When** the old pending packet is reused, **Then** the packet is rejected and a new event is required.
4. **Given** a crash during publication, **When** recovery runs, **Then** wound, effects, resources, history, scheduler state, and player output are all restored or all committed.
5. **Given** the same accepted attempt or cycle is replayed, **When** it is processed again, **Then** no duplicate state, charge, roll, notification, or recovery progress appears.

### Edge Cases

- A harmless sealed outcome conflicts with a proposed critical wound.
- A guaranteed source trigger conflicts with a training-mode prohibition.
- A strain transition jumps more than one rank or targets a side already at `broken`.
- Both sides become eligible for wounds in one accepted exchange.
- An active wound worsens while a treatment or recovery transition is pending.
- A critical success would reduce a severity-I wound by two steps.
- An insufficient-tier healer rolls a natural 20.
- A natural 1 also has a numerically successful total.
- One safe cycle contains a partial active treatment plus natural recovery.
- Several wounds naturally cross thresholds during one world cycle.
- A public price multiplier produces a fractional Ink Feather amount.
- The target, provider, item, facility, skill/art, or effect source becomes stale between selection and publication.
- A treatment course is interrupted after some, but not all, declared doses.
- A hidden treatment route has no currently reachable discovery path.
- A wound severity falls and its old consequence set no longer fits the new budget.
- A wound-owned effect is independently removed or dispelled.
- An unrelated curse or Saref memory suppression exists beside a wound and must survive healing.
- Full healing creates a permanent mechanical legacy and a cosmetic scar in the same transition.
- A healed wound is referenced by an attempted active effect or treatment replay.
- A Mortal owner crosses a realm boundary; the physical wound must not silently become spiritual.
- Two nearby entities share a display name, or a selected entity leaves reachability before commit.
- Player-visible summary data lacks canonical wound detail authority.
- GM-authored text contains console markup or browser-unsafe content.

## Requirements *(mandatory)*

### Functional Requirements

#### Common wound authority

- **FR-001**: Every accepted wound MUST have one stable client-owned identity and one exact owner, realm, cause/source, creation event, and chronology.
- **FR-002**: Every accepted wound MUST declare physical or spiritual domain, severity I-IV, an explicit location profile, readable nature, symptoms, prognosis, visibility, care state, recovery policy, and history.
- **FR-003**: Death and soul dissipation MUST remain separate outcomes and MUST NOT be represented as severity V.
- **FR-004**: The GM MUST decide whether an ordinary eligible event creates a wound and MAY choose any severity from none through the event's permitted maximum.
- **FR-005**: The client MUST reject wound severity, owner, realm, consequence, treatment, or chronology that contradicts the sealed event envelope.
- **FR-006**: A proven pre-materialized guaranteed-wound trigger MUST require its declared wound result within all applicable hard caps.
- **FR-007**: Pure roleplay injuries MUST use explicit causal and severity declarations and MUST NOT infer mechanics, anatomy, or treatment from narrative names alone.
- **FR-008**: Wound creation, linked effects, resource changes, history, narration, notification, and projections MUST publish atomically.
- **FR-009**: Invalid wound materialization MUST publish no partial canonical or player-visible result and MUST produce a bounded repair request.
- **FR-010**: Repair MUST preserve valid unrelated authored content while preventing changes to the sealed event, roll, target, or internal authority.
- **FR-011**: Create, worsen, complicate, diagnose, stabilize, treat, recover, heal, legacy, and archive/history transitions MUST be explicit, bounded, deterministic, and replay-safe.
- **FR-012**: The same accepted event, attempt, course milestone, or recovery cycle MUST NOT create or advance a wound more than once.
- **FR-013**: Active wounds MUST remain in exact owner-appropriate carriers governed by one common identity/history authority.
- **FR-014**: A physical wound MUST NOT silently convert into a spiritual wound during realm transition.
- **FR-015**: Full healing MUST retain an immutable historical wound record while excluding it from the active-wound list.
- **FR-016**: A healed wound MUST NOT be reopened; later trauma MUST create a new identity with optional historical provenance.
- **FR-017**: A permanent mechanical legacy MUST be an independent entity linked to the healed wound, while a cosmetic legacy MAY remain history-only.

#### Wound-owned effects and consequence budgets

- **FR-018**: Mechanical wound consequences MUST be separate active effects with exact bidirectional wound/source ownership.
- **FR-019**: A wound-owned effect MUST NOT mutate, heal, worsen, delete, retarget, or reopen its source wound.
- **FR-020**: Removing or dispelling a wound-owned effect alone MUST NOT change the wound's severity, care state, recovery, or history.
- **FR-021**: Full wound healing MUST terminate all active effects owned by that wound and MUST leave unrelated effects unchanged.
- **FR-022**: Spiritual severity I-IV MUST materialize exactly one independently understandable legal afterlife consequence per severity step.
- **FR-023**: Mortal severity I-IV MUST permit at most one, two, three, or four independently understandable mechanical consequences respectively, MUST NOT require a catalog of ready-made wounds, and MUST contain at least one non-display impact: a mechanical consequence, active complication, or care/recovery constraint that changes the legal lifecycle.
- **FR-024**: Display text, symptoms, and technical wound linkage MUST NOT consume a consequence slot; every independent mechanical modifier or restriction MUST consume one slot.
- **FR-025**: Every usable consequence profile MUST belong to the closed version-1 primitive registry below and MUST have client-owned, severity-specific power, cadence, expansion, and scope limits.
- **FR-026**: Heavy or critical restrictions MAY prevent specific actions but MUST preserve at least one path to inspect, communicate, request help, receive treatment, or leave the condition.
- **FR-027**: A severity transition MUST atomically rematerialize a consequence set that fits the new slot and power budget.
- **FR-028**: Independent curses, oaths, Fate Card outcomes, and Saref effects MUST NOT consume wound consequence slots unless the event explicitly materializes them as trauma-caused wound consequences.

Version-1 Mortal wound effects MAY use the existing generic profiles
`characteristic_modifier`, `roll_modifier`, `resistance_modifier`,
`periodic_damage`, `periodic_restore`, `action_control`, and `event_reaction`.
The `wound_consequence` source/display marker costs zero slots, and at most one such
marker may exist across the wound-owned effect set. Each independently affected
characteristic, roll operation, resistance, periodic resource operation, action, or
worst-case reaction result costs one slot. Per-slot severity limits are:

| Limit | I | II | III | IV |
| --- | ---: | ---: | ---: | ---: |
| Absolute flat characteristic/resistance modifier | 1 | 2 | 3 | 4 |
| Absolute percent characteristic/resistance modifier | 5% | 10% | 20% | 30% |
| Periodic amount as accepted maximum of the exact resource per trigger | 5% | 10% | 20% | 30% |
| Absolute action cost modifier | 1 | 2 | 3 | 4 |
| Action `grant` | one action/slot | one | one | one |
| Action `restrict` | allowed for one action/slot | allowed | allowed | allowed |
| Action `forbid` | forbidden | forbidden | one non-safety action/slot | one non-safety action/slot |
| Event reaction | deterministic registered outcome only; no definition expansion | same | at most one fully budgeted expansion | at most one fully budgeted expansion |

For a scalar modifier with a non-null `cap`, the severity limit applies to the exact
runtime value after applying `minimum` and then `maximum`, in that order. Both cap
endpoints MUST be exact decimals and the resulting modifier MUST be nonzero. A safe cap
may bound a larger finite authored value; a cap cannot amplify the runtime value beyond
the severity envelope. A finite generic-effect number that cannot be represented by the
exact wound decimal contract MUST be rejected rather than omitted from slot derivation.

Periodic values MUST be quantum-aligned without rounding above the percentage cap and
MUST execute at most once for one accepted source event. Roll contributions cost one
slot per listed operation. Reaction slot cost includes every worst-case spawned
mechanical component. The single wound expansion ceiling counts wound-owned expansions
only; bounded independent effect siblings neither consume this ceiling nor wound slots.
A Mortal `forbid` may target only `attack`, `cast`, or
`movement`; `defend`, `use_item`, `interact`, and `escape` remain non-forbiddable
safety-capable action keys. Aggregate restrictions MUST preserve inspection,
communication, help, treatment, and exit.

Version-1 spiritual wound consequences use exactly these closed profiles/axes:

| Profile | Mechanical axis | Severity envelope |
| --- | --- | --- |
| `spiritual_roll_hindrance` | `rollMode` for one declared non-safety operation | one operation/slot at I-IV |
| `spiritual_action_cost_burden` | `actionCostAudit` for one declared non-safety operation | +1 at I-II, +2 at III, +3 at IV |
| `spiritual_position_burden` | starting/allowed `conflictPosition` | one adverse step at I-II, at most two at III-IV |
| `spiritual_control_burden` | `controlState` resistance for one binding family | unavailable at I; one adverse step at II-IV |
| `spiritual_strain_burden` | one extra strain step on one declared failed non-safety operation | unavailable at I-II; one step at III-IV, still capped at `broken` |
| `spiritual_tempo_burden` | `tempoAdvantage` for one declared operation | deny one owner tempo gain/slot at I-IV |
| `spiritual_counter_burden` | `counterPayoff` for one declared operation | reduce one payoff step/slot at I-IV |
| `spiritual_art_restriction` | availability of one declared standard combat art | unavailable at I-II; restrict at III; forbid at IV |

`spiritual_healing`, wound inspection, communication, requesting/receiving help,
withdrawal, surrender, negotiation, and the separate dissipation decision are safety
operations and MUST NOT be selected by a hindrance, cost, strain, or art-restriction
profile. The exact eligible non-safety operation set is `pressure`, `counter`, `guard`,
`maneuver`, `binding`, `break_binding`, `force_binding`, `force_incarnation`,
`incarnation_resistance`, `champion_coordination`, and `recover_spiritual_power`.
The narrower `spiritual_art_restriction` target set is exactly the ten combat arts
`pressure`, `counter`, `guard`, `maneuver`, `binding`, `break_binding`,
`force_binding`, `incarnation_resistance`, `champion_coordination`, and
`recover_spiritual_power`; it excludes `force_incarnation` and any present or future
standard noncombat/healing art.
Duplicate profile/operation coordinates within one wound are invalid. These
are consequence primitives, not a catalog of complete spiritual wounds.

#### Spiritual conflict and injury

- **FR-029**: Every spiritual conflict MUST fix one danger mode at start, and any escalation MUST be explicit and accepted before its higher cap applies.
- **FR-030**: Training MUST forbid spiritual wounds; controlled conflict MUST cap them at II; hostile and annihilation conflict MAY permit I-IV.
- **FR-031**: Ordinary spiritual wound eligibility MUST occur only on an accepted harmful strain transition and MUST reuse the accepted exchange evidence without a second injury roll.
- **FR-032**: The maximum spiritual severity MUST be calculated from harmful margin, applied-art tier, target Spiritual Resilience tier, destination strain rank, extra strain jumps, and the conflict-mode cap according to the approved design formula.
- **FR-033**: Natural 1 and natural 20 in the conflict exchange MUST NOT independently raise spiritual wound severity.
- **FR-034**: Each side MUST receive at most one newly created spiritual wound per conflict.
- **FR-035**: Later wound choices in the same conflict MAY worsen that exact conflict wound within the new envelope and MUST NOT create a duplicate.
- **FR-036**: Re-traumatizing an older active wound MUST require an explicit action and exact causal evidence.
- **FR-037**: Every non-training defeat without soul dissipation MUST produce a bounded conflict outcome with scope and end condition that prevents immediate repetition of the same aggression.
- **FR-038**: Soul dissipation MUST always require valid authority and an explicit winner choice and MUST never be automatic or mandatory.
- **FR-039**: Final published narration MUST state wound acquisition clearly, and the player MUST receive a direct wound notification with a route to wound detail.

The destination-strain ceiling used by FR-032 is exact:

| Destination strain | Rank | Maximum wound severity |
| --- | ---: | --- |
| `clear` | 0 | none |
| `strained` | 1 | I |
| `fractured` | 2 | II |
| `overwhelmed` | 3 | III |
| `broken` | 4 | IV |

#### Spiritual arts, healing, and natural recovery

- **FR-040**: Spiritual Resilience and Spiritual Healing MUST be visible standard arts at tier 0 and MUST use ordinary standard-art progression through tier V.
- **FR-041**: Spiritual Resilience MUST passively affect the maximum spiritual injury calculation and MUST NOT consume OD.
- **FR-042**: Spiritual Healing tier 0 MUST support diagnosis only; tiers I-IV MUST treat matching-or-lower severity; tier V MUST treat every spiritual severity.
- **FR-043**: Active spiritual healing MUST use the approved total, difficulty, margin bands, complication modifiers, and sealed die evidence.
- **FR-044**: A healer below the wound's current severity MUST NOT reduce severity even on a natural 20.
- **FR-045**: A sufficient healer's natural 20 or margin at least +8 MUST reduce severity by two steps; margin 0 through +7 by one; margin -1 through -4 MUST add one recovery point; natural 1 or margin at most -5 MUST add no active improvement.
- **FR-046**: In-conflict healing MUST be a counterable action with base cost 5 OD, ordinary art-based reduction, and final minimum 2 OD.
- **FR-047**: Out-of-conflict healing MUST consume one safe afterlife cycle and MUST permit only one session for the same wound during that cycle regardless of healer.
- **FR-048**: Self-healing outside conflict MUST consume no currency or item but MUST advance the same world scheduler as other safe-cycle treatment.
- **FR-049**: A safe healing session MUST resolve active treatment once and natural recovery once in deterministic order; failure MUST provide only ordinary natural recovery.
- **FR-050**: Every safe afterlife cycle MUST add `1 + Spiritual Healing tier` natural recovery points to each eligible spiritual wound.
- **FR-051**: Natural recovery thresholds MUST be 2, 4, 6, and 8 points for leaving severity I, II, III, and IV respectively, with remaining points carried forward.
- **FR-052**: Worsening a spiritual wound MUST reset progress for its current step.
- **FR-053**: The natural-recovery and art-progression rules MUST apply to players, Guardians, residents, leaders, and other authorized afterlife entities.
- **FR-054**: A conscious capable target MUST consent outside conflict; unwilling treatment MUST be a conflict action; a helpless friendly target MAY be treated when current authority permits aid.

#### Mortal construction, diagnosis, and treatment

- **FR-055**: Every Mortal wound MUST be freely authored for the current world and MUST NOT be selected from a predefined wound, symptom, medicine, or cure catalog.
- **FR-056**: A Mortal wound MAY compose any universal registered mechanical primitive permitted by its severity envelope and exact target/resource authority.
- **FR-057**: Every Mortal wound MUST have at least one mechanically complete treatment route at creation.
- **FR-058**: Every hidden treatment route MUST have at least one reachable, materialized diagnostic or discovery path.
- **FR-059**: New evidence-backed alternative treatment routes MAY be added later without changing prior wound history.
- **FR-060**: One treatment route MAY require exact items/doses, capabilities, skills, providers, facilities, time, ordered steps, and environmental conditions; all declared requirements in that route MUST hold.
- **FR-061**: Separate treatment routes MUST be alternatives and MAY have different risks, resource costs, or outcomes.
- **FR-062**: Procedure routes MUST use a predeclared bounded check and result bands; course routes MUST use exact doses/time and interruption behavior; guaranteed routes MUST require an explicit pre-materialized source capability.
- **FR-063**: The client MUST validate and reserve exact resources and conditions before treatment and MUST consume them only through the accepted atomic result.
- **FR-064**: Accepted Mortal treatment MAY stabilize, remove a complication, add recovery progress, reduce severity, or heal only as declared by the selected route.
- **FR-065**: Every Mortal wound MUST use one explicit natural-recovery policy: progressive, requires stabilization, or no natural recovery, with optional bounded deterioration.
- **FR-066**: Mortal recovery and deterioration MUST use exact elapsed/event evidence and MUST NOT be advanced twice by repeated prose or retry.

#### Commands, targeting, history, and privacy

- **FR-067**: `/раны` and `/wounds` MUST show only the player's active current-realm wounds by default.
- **FR-068**: Healed wounds MUST appear only through a separate History action.
- **FR-069**: Active wound detail MUST show readable severity, origin, symptoms, visible effects, recovery state, known treatment options, available help, and a treatment action without internal metadata.
- **FR-070**: `/лечить` and `/treat` MUST provide one guided treatment flow; `/исцелить` and `/heal` MUST act as afterlife aliases.
- **FR-071**: Diagnosis MUST be integrated into the guided treatment flow and MUST reveal only authorized information.
- **FR-072**: Target selection MUST list Self first and then visible reachable nearby entities; players MUST NOT need to type names or technical identities.
- **FR-073**: Equal display names MUST be disambiguated with visible context while the selected exact identity remains hidden.
- **FR-074**: Reachability, consent, wound activity, provider capability, and exact requirements MUST be revalidated immediately before publication.
- **FR-075**: Console and browser clients MUST provide semantically equivalent actions, costs, results, blocking states, privacy, and player-facing Russian terminology.
- **FR-076**: GM-authored text MUST be safely rendered in console and browser surfaces.

#### Providers and afterlife world integration

- **FR-077**: Healing capability MUST be independent from public-service availability; only an explicit service profile MAY advertise a provider.
- **FR-078**: Public afterlife command treatment MUST quote 25, 50, 100, or 200 Ink Feathers for severity I-IV before a bounded 50%-200% provider multiplier rounded upward to a whole Feather.
- **FR-079**: An accepted public attempt MUST charge the sealed price once even on partial success or failure; cancellation, rejection, or rollback MUST retain no charge.
- **FR-080**: A roleplay agreement MAY replace currency with free aid, favor, debt, quest, or another explicit consideration but MUST use the same final healing gate, roll, target, attempt, and world-cycle handler.
- **FR-081**: Elyara of the Last Wound MUST be discoverable from first Chaos Sea entry, fixed at Spiritual Healing tier V, publicly available at the Lazaret, and priced at the ordinary multiplier.
- **FR-082**: Traveling to or receiving safe-cycle treatment from Elyara MUST advance the afterlife world scheduler exactly once.
- **FR-083**: Every materialized Shining faction in the single Shining Abode MUST have at least one visible primary `healing_support` resident with Spiritual Healing tier I-V.
- **FR-084**: A Shining healer's role MUST be visible with the roster, while service access MAY depend on faction-specific relationship, payment, debt, or agreement conditions.
- **FR-085**: Shining factions MUST NOT be required to expose one universal public paid clinic.

#### GM guidance and completion integrity

- **FR-086**: GM prompts, guides, contracts, examples, manifests, and source guards MUST describe every GM-authored wound, treatment, repair, service, role, and afterlife outcome surface changed by the feature.
- **FR-087**: Worked examples MUST include Mortal construction/treatment, spiritual optional and guaranteed wounds, rejected repair, active/natural healing, entity recovery, Elyara, Shining healer access, healed legacy, and independent Saref memory suppression.
- **FR-088**: A wound or healing capability MUST NOT be considered complete while console/browser behavior, canonical detail authority, GM guidance, or required worked examples are missing.

### Key Entities *(include if feature involves data)*

- **Wound**: An independently treatable physical or spiritual injury with stable identity, owner, cause, severity, location, symptoms, care state, consequences, treatment/recovery policy, visibility, and history.
- **Wound Opportunity**: Sealed evidence that an accepted event permits, forbids, caps, or guarantees a wound for one exact target.
- **Wound Transition**: One accepted creation, worsening, complication, diagnosis, stabilization, treatment, recovery, healing, legacy, or archive/history change.
- **Wound Consequence**: One independently understandable mechanical result represented by a wound-owned active effect and charged against the applicable severity budget.
- **Treatment Route**: A world-specific diagnosis/treatment contract with visibility, exact requirements, resolution mode, outcomes, resource behavior, and failure/interruption rules.
- **Recovery Policy**: The exact natural-recovery clock, thresholds, blockers, and optional deterioration rules for one wound.
- **Healing Session**: A sealed active treatment attempt with healer, target, wound, world/combat cost, roll or route result, and replay protection.
- **Healing Service Profile**: A public provider's realm, location, availability, compensation, price, and access contract, separate from raw healing capability.
- **Wound History**: Ordered immutable player-readable provenance backed by terminal transition authority.
- **Independent Legacy/Effect**: A lasting result whose lifecycle is separate from a healed wound but whose origin may reference it.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of accepted representative wounds contain owner, realm, cause, severity, location, consequences, treatment/recovery policy, and history; incomplete proposals publish 0 partial fields.
- **SC-002**: Across the required boundary scenarios, every ordinary GM proposal from no wound through the permitted maximum is accepted, while 100% of over-maximum or guaranteed-result omissions are rejected before publication.
- **SC-003**: Replaying any accepted wound event, treatment attempt, course milestone, or recovery cycle 100 times produces 0 duplicate wounds, effects, charges, rolls, notifications, history rows, or progress ticks.
- **SC-004**: Injected failure at every publication boundary leaves either the complete before-state or complete after-state in 100% of rollback scenarios.
- **SC-005**: A player reaches full active-wound detail or starts the guided treatment flow in no more than two selections after opening `/раны` or `/лечить`.
- **SC-006**: The primary wound list displays 0 healed wounds in every console and browser scenario, while History retains 100% of accepted healed records.
- **SC-007**: Console and browser scenario matrices produce identical available actions, exact prices/costs, accepted results, and blocking reasons for 100% of shared wound workflows.
- **SC-008**: Player-facing wound and treatment surfaces expose 0 technical IDs, validation codes, API terms, or unsafe unescaped GM-authored markup in the required projection tests.
- **SC-009**: A tier-0 critical spiritual wound heals in exactly 20 uninterrupted safe cycles and a tier-V critical wound in exactly four, with 0 double ticks on retry.
- **SC-010**: Every authorized persistent afterlife entity type can naturally recover and develop the new standard arts without requiring a public healer.
- **SC-011**: Every materialized Shining faction has at least one visible valid `healing_support` resident, while factions without public access expose 0 unintended paid-service actions.
- **SC-012**: Every required GM-authored contract has at least one validated worked example, and all changed Mortal/afterlife documentation manifests and source guards agree with runtime behavior.
- **SC-013**: A GM can correct each representative invalid wound proposal using the bounded repair information without receiving hidden player/NPC authority or rewriting unrelated accepted content.

## Verification Plan *(mandatory)*

- **C# verification**: During implementation use `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused` with exact wound-domain filters and an explicit `-FocusedProject Integration` for integration classes; run one meaningful `Fast` checkpoint; run `RegressionIntegration` when changing the exhaustive spiritual-conflict matrix; run one `PreMerge` immediately before each merge.
- **Documentation/contract verification**: Run focused `AfterlifeDocumentationCoverageTests` and `ExampleDocumentationValidationTests`; because afterlife examples/contracts change, run one conditional `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` before final completion.
- **Frontend verification**: Run `npm run verify` from `BookOfEternityClient.WebFrontend`; run focused command-service/browser tests for `/раны`, History, `/лечить`, target selection, privacy, provider prices, and parity.
- **Manual/player-facing verification**: Exercise the same Mortal and afterlife save scenarios in console and browser, inspect active-only wound lists and History, select equal-name nearby entities, diagnose hidden treatment, self-heal, use Elyara, and inspect a Shining faction healer without public access.

## Assumptions

- Effect materialization from #1535 is canonical and supplies wound-source linking, active-effect lifecycle, component profiles, and atomic effect planning.
- Existing accepted-event, resource, actor identity, pending-turn snapshot, rollback, and afterlife scheduling authorities are extended rather than duplicated.
- The current spiritual strain ranks, accepted roll margin, standard-art tier 0-V model, and ordinary art-cost reduction remain available.
- The world remains pre-release; active templates and fixtures can move directly to the new schema without compatibility readers.
- There is one Shining Abode containing multiple independently materialized Shining factions.
- Elyara of the Last Wound remains the canonical always-available Healing-domain Guardian and Lazaret owner.
- Ink Feathers remain the ordinary command-service currency in both Chaos Sea and Shining contexts; Light Sparks are not ordinary payment.
- A safe afterlife cycle is a completed canonical progression cycle outside active conflict and without an explicit unsafe-state blocker.
- Physical wounds do not automatically transfer to a soul; any spiritual legacy requires a separately accepted spiritual wound or independent effect.
- No closed list of complete Mortal or spiritual wounds will be introduced.
