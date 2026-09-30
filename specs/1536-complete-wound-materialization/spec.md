# Feature Specification: Complete Wound Materialization and Healing

**Feature Branch**: `1536-complete-wound-materialization`

**Created**: 2026-08-26

**Status**: Implementation in progress; US2 RED contracts complete and GREEN authority slices underway

**Input**: Materialize independently treatable physical and spiritual wounds with complete creation, consequence, diagnosis, treatment, recovery, healing, history, command, provider, and GM-authoring lifecycles.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536); depends on completed effect materialization [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
- **Issue type**: Epic enhancement and audit hardening task
- **Spec Kit justification**: The feature spans multiple sessions and changes player-facing commands, console/browser parity, canonical state, validation, normalizers, resource use, afterlife conflict, entity progression, GM prompts, documentation, examples, and rollback behavior.
- **Contract scope**: Player-facing Mortal World and afterlife mechanics; GM-facing authoring and repair; runtime state; validation; effects; resources; progression; documentation; examples; console; browser frontend.
- **Save compatibility**: Not required under the pre-release constitution and the user's explicit decision. Repository bootstrap state, fixtures, examples, and tests move directly to the new contract.
- **Out of scope**: A catalog of ready-made wounds; migration of non-empty legacy saves; replacement of the effect engine; a general combat-damage or experience redesign; automatic post-battle fatigue; automatic soul dissipation; a full afterlife inventory; a mandatory public clinic for the whole Shining Abode. If Saref's `memory_suppression` is only audit metadata rather than a complete independent effect, a separate linked issue MUST be created before #1536 closes.

## Clarifications

### Session 2026-09-22 — T081-C Durable Spiritual Decision Contract Approved

The next architectural checkpoint is the exact durable T081-C contract already written in
`contracts/spiritual-wound-live-turn-boundary.md`. The recommended design uses two distinct
client-owned roots: resumable in-turn evidence in
`game_state/control/pending_spiritual_wound_decisions.json`, and append-only accepted
instance/closure/source/decision evidence in
`game_state/wounds/spiritual_wound_opportunity_receipts.json`. The common accepted-mechanics
transaction is the only publisher. Persisting a pending packet never creates an accepted receipt;
an explicit `none` decision still creates a durable receipt, while `materialize` must agree with
one exact wound/history transition. Cold reconstruction must rebuild authority from the original
pending snapshot, retained source prefix and staged decisions without trusting caller hashes or
process-local objects. Failure and rollback restore the original baseline, including originally
absent roots, and exact replay produces no new command, spend, transition or notification.

The owner approved this exact contract on 2026-09-22. This checkpoint also owns final live
wound-carrier assembly and one common publication, including
the source-only/decline case. It does not change the approved wound formula, profile set, option-A
reaction boundary, resource chronology, healing rules, player commands, or the still-unresolved
recovery/force-incarnation cost decision. Implementation planning and task decomposition may now
proceed against this approved boundary.

### Session 2026-09-19

- Q: Preserve the eight spiritual wound profiles and correct the impossible spiritual-reaction acceptance scenario, or expand spiritual wounds with reactions? → A: Owner selected recommended option A. Preserve the eight profiles plus `wound_consequence`; spiritual reaction graphs remain forbidden. Require real spiritual insertion, next-exchange contributions, worsening/re-trauma and common completion; cover reaction descendants, replacements and anchored consumption through legal physical wounds/general effects in the same engine. This changes the acceptance matrix explicitly; it does not claim the former impossible scenario passed or waive cold reconstruction.

### Session 2026-08-29

- Q: How is a hidden-route discovery cycle represented and rejected without moving exact world-reference authority into the structural parser? → A: Each diagnosis path declares closed `requiresKnownFacts`; the client computes a least fixed point from already known routes and visible complications, while exact world reachability remains a separate fresh authority proof.
- Q: How does a failed diagnosis differ from a malicious empty reveal? → A: Client-sealed diagnosis evidence declares exactly `success` or `failure`; success reveals the complete declared fact set, while failure reveals nothing and is still one terminal retry-safe attempt.
- Q: How is a later evidence-backed cure added without abusing `diagnose` or rewriting history? → A: `author_alternative_treatment` is a distinct sealed transition that appends exactly one complete route and, for a hidden route, one reachable diagnosis path; prior definitions and history remain immutable.
- Q: What do natural 1 and natural 20 mean for a Mortal procedure? → A: After every hard requirement and authority seal succeeds, natural 20 selects the first/best predeclared band and natural 1 normally selects the last/worst predeclared band, regardless of numeric margin. If the exact roll actor is the current player and a prepared valid Fate Shield exists, the shared client arbiter converts natural-1 critical failure to ordinary failure, selects the first authored failed band, and consumes that shield atomically. Neither critical result bypasses a requirement or invents a new outcome; rolls 2-19 use the sealed numeric margin.
- Q: How do procedure bands map to resource policy? → A: Every band declares exactly one `success|partial_success|failed_attempt` category. Bands are an ordered, gapless, non-overlapping inclusive partition from positive infinity to negative infinity; selection returns exactly one authored band and its ordered bounded transition list. Every `success` band must actually contain an applicable positive state change, so a no-op cannot earn route completion.
- Q: What course evidence survives a restart? → A: Every accepted milestone stores the complete sealed request plus exact course/route identity, route fingerprint, start and resolution game-time coordinates, clock fingerprint, ordinal, course disposition, and typed result in durable history. The active wound keeps only the agreeing `activeCourseId`; history reconstructs the rest. Only intermediate milestones may have an empty result; the final milestone is non-empty and the complete course contains a positive treatment operation.
- Q: How is a guaranteed Mortal route proven? → A: It requires both the unchanged successful `source_capability` requirement and a separate canonical proof exported from the selected actor's exact current active/passive skill with permanent exact/confusable-unique `skillId` and an optional wound-treatment capability extension. The T066 snapshot composer derives the ordinary unchanged capability row from that same skill rather than a second caller source. The route outcome must fit that proof's owner, domain, severity, and aggregate operation limits; any mastery gate is a separate unchanged-T060 `skill_tier` requirement. Immediately before publication the proof is re-exported from the final composed skill after-image if the plan touches that root. A transient snapshot row, duplicate/confusable or idless skill, generic capability presence, or GM success flag is insufficient.
- Q: Does a terminal treatment attempt mean the wound is terminal? → A: No. Every accepted procedure, course milestone/interruption, or guaranteed attempt is terminal and retry-safe as an attempt, including no improvement or a harmful declared result. The wound/history `terminal=true` flag remains reserved for the separate accepted `heal` transition; validation failure, cancellation, and rolled-back publication create no terminal attempt.
- Q: Can a guaranteed result heal any severity directly? → A: No result bypasses the canonical severity-I heal gate. Ordered typed treatment operations may first reduce the working severity to I and then request the separate follow-up heal; otherwise `heal` is invalid. The canonical source proof must authorize every operation and the starting severity.
- Q: What distinguishes a genuinely interrupted course from forged/stale evidence? → A: A separate scoped authority classifies freshly exported common/current-milestone predicates as `Satisfied`, `Unsatisfied`, or `InvalidAuthority`. Only trustworthy live predicate loss or an exceeded deadline can select the declared non-beneficial interruption; malformed, ambiguous, cross-realm, changed-coordinate/history, or fingerprint evidence rejects without interrupting.
- Q: How can an outcome add a setting-specific complication or lasting legacy without an open payload? → A: Results are closed typed operation objects. `add_complication` reuses the existing response-local complication/consequence sub-proposal whose permanent IDs are allocated only if selected. Heal legacies are exactly cosmetic declarations or complete `mechanical_effect` drafts; arbitrary mechanics use #1535 components under client-only `wound_legacy` source authority persisted in typed history. There is no open skill/trait/other branch. Colon strings, names, accepted-result fields, and incomplete JSON never create mechanics.
- Q: How can exact treatment replay work after restart if current resources, wound state, route definition, or skill source have changed? → A: The complete production-sealed request, including the canonical detached pre-attempt wound used as its route source, that wound's semantic fingerprint, its nested full requirement bundle, and resource authority, lives in typed command/pending authority before acceptance and in `transitionResult.requestAuthority` afterward. A history-parse-result probe re-parses the detached wound, derives the exact selected route/result semantics, recomputes every nested request/bundle/result/receipt seal, and returns the detached original request plus intent-free receipt before reading any current state. Fingerprint-only proof is insufficient.
- Q: Can two courses run on one wound? → A: No. The first milestone requires `activeCourseId=null` and seals a complete starting-wound authority; continuation reconstructs that baseline and exact next ordinal from history. Procedure/guaranteed treatment may occur during the course, but a second course is rejected and heal/completion/interruption clears the pointer.
- Q: Who owns treatment operation IDs and the handoff to final composition? → A: T067 emits one closed immutable typed outcome intent for every typed declared operation, with deterministic complication/legacy IDs, namespaced local refs, and derived heal/legacy child coordinates. T070 may compose the established wound/effect batches but may not parse raw route JSON or allocate/reinterpret those identities.
- Q: Can a physical wound on a live combatant be treated? → A: Yes. Procedure/course context accepts exact combatant and combatant-member coordinates from the accepted combat root. Guaranteed proof still comes from a canonical player/NPC skill; a provider guarantee can target a combatant, while a target-owned guarantee requires the existing persistence/promotion transition first.
- Q: May a Mortal wound adapter trust the causal profile and harmful outcome carried by its source-shaped input? → A: No. That input is correlation only. Every formal, QTE, combat, trap, check, hazard, or narrative adapter must resolve exactly one client-sealed accepted occurrence from the active signed pending-turn snapshot and derive owner, source state, bounded causal profile, outcome, legal locations, hard maximum, and any guarantee from that authority. The input may repeat only the selected ordinal and public owner/profile/source/outcome/safe-context plus an absent-or-exact worsening target; binding, hard maximum, guarantee, event authority, receipt, and after-images are forbidden. A current combatant, hazard, die, or source definition proves availability but not that a harmful occurrence happened.
- Q: How is the complete accepted event set protected when one occurrence can create or worsen a wound? → A: Initial composition and accepted-turn validation independently reconstruct the same complete ordered event set from the immutable accepted response plus the sealed occurrence. The wound selects one exact ordinal inside that set but seals the entire set; adding, removing, or reordering a sibling event invalidates the command. An absent worsening coordinate means create, while worsening always requires one explicit exact active-wound coordinate and is never inferred.
- Q: Where does an ordinary `none` decision survive after the wound command is consumed? → A: Every accepted opportunity decision, including `none`, produces one append-only client-owned decision receipt published by the same common accepted transaction. While the same active pending-turn snapshot remains, an exact cold replay reconstructs the opportunity from its signed pre-consumption occurrence plus the current receipt/pending partition and produces no command or transition. A changed decision conflicts; after terminal cleanup or a newer snapshot the old correlation is stale. A decline cannot disappear merely because it created no wound-history row.

### Session 2026-08-30

- Q: What is the canonical source for a Mortal treatment facility, environmental condition, or current provider/target consent when the existing location and NPC contracts have no such registries? → A: The exact validated current Mortal location may carry closed version-1 registered rows inside its existing location `customStates[]`: `mortal_wound_treatment_facility`, `mortal_wound_treatment_environment`, and `mortal_wound_treatment_consent`. Their identifiers and environment state values remain setting-specific exact IDs rather than a universal catalog. The client validates each recognized row's closed shape, identifier uniqueness, availability/status, and actor bindings; the lease-bound treatment exporter additionally proves exact current-location identity and actor co-presence. These reserved kinds are invalid in link `customStates[]`. For an existing location the GM replaces the complete location `customStates[]` only through `worldMapUpdates.locationUpdates[]`, preserving every unrelated sibling; a same-turn new selected or remote location carries the complete array in its ordinary `currentLocationData` or `worldMapUpdates.newLocations[]` creation envelope. Unrecognized custom states remain ordinary setting data and never grant treatment authority.

### Session 2026-09-02

- Q: What happens when treatment consumes only part of a stack that owns live resources? → A: The same permanent item identity and carrier remain active with the exact reduced count; every live item-owned `instance_fixed` maximum and current value is scaled by the exact remaining/source-count ratio with quantum alignment and no rounding. An inexact or non-`instance_fixed` projection rejects the entire publication. Consuming the complete remaining stack instead retires every live item-owned resource and terminally consumes the item.
- Q: How are repeated selected treatment claims against one item represented? → A: Finalization order is authoritative: each selected claim emits one sequential item `consume` transition with its own exact before/after quantity, and all item/resource operation identities are privately derived from the sealed request, result/finalization, claim fingerprint, and ordinal. Claims are never silently aggregated and callers cannot supply IDs or history.
- Q: Which same-turn response fields and normalized roots may participate in T070-B.4 treatment item publication? → A: Alongside the existing six closed skill-operation fields, B.4 admits only the ordinary Mortal item command properties `UpdateInventory`, `moveInventoryItems`, `removeInventoryItems`, `NPCInventoryAdds`, `NPCInventoryUpdates`, `NPCInventoryRemovals`, and `NPCEquipmentChanges`; every other non-null response property remains unsupported. The snapshot owns detached `JsonNode?` current/backup and output roots; every map retains the complete bidirectional path set, with a null value proving an absent file, and preserves exact content plus top-level object/array topology, including legacy vehicles and effective post-location roots. The sealed live item baseline is the exact output immediately before common-plan publication after only the ordinary transforms that can touch the selected item graph. `TransformRegistry` owns execution in exact base order `quest_history:v1`, `npc_core:v1`, `npc_trade:v1`, `inventory_items_journal:v1`, `item_bonds:v1`, `item_text_updates:v1`, `npc_item_journals:v1`; the same dispatch loop emits the fingerprinted `AppliedTransformIds`, specializing NPC trade to `npc_trade:apply:v1` or `npc_trade:skip_untouched_treatment_continuation:v1`. `Apply` consumes `UpdateNpcTradeInventoryReceipts` into canonical receipts, whereas `SkipUntouchedTreatmentContinuation` leaves the post-NPC-core root and command untouched and creates no receipt. The baseline explicitly excludes the B.2 treatment skill projection, which receives the supplied semantic final ordinary NPC root while transaction rollback retains its separate true live canonical before-image. Each ordinary transformation is shared with the normalizer rather than duplicated, and every authority, pending byte, route/transfer catalog, and snapshot on which it depends is detached, sealed, and forwarded from validation without reread or rebuild. Accepted same-turn creations use snapshot-owned deterministic root receipt and create-transition IDs in production collector order `UpdateInventory` -> NPC core -> NPC commands -> current location -> offscreen storage, and transfers use deterministic transition IDs; neither the projector nor normalizer may invoke the writing transfer service or allocate a second random receipt/history row.
- Q: What may full treatment consumption unlink automatically? → A: Only an existing supported inline equipment reference may be cleared by the shared Mortal item transition contract. A container, quest, bond, or any other companion reference requires its own genuine atomic transition authority; because B.4 does not mint that authority, the complete mixed publication rejects before any write.

### Session 2026-09-05

- Q: How may a wound consequence affect either every skill check or one exact skill without creating wound-only roll mechanics? → A: The completed #1535 common `roll_modifier` gains one mandatory closed `scope`: either `{ "kind": "all" }` or `{ "kind": "skill", "skillId": "<canonical-id>" }`. Focused scope is legal only for exactly `operations: ["skill_check"]`; the client binds the selected permanent skill identity against the exact target's offered and final usable catalogs, and no missing-scope compatibility default exists.
- Q: What happens after a valid focused modifier's selected skill becomes unavailable? → A: The effect and source wound remain unchanged; current authority derives only that component as dormant. The exact same permanent identity may reactivate it, while a similar name, alias, case variant, or Unicode-confusable identity cannot inherit it.
- Q: Which skill identity is used by a Mortal treatment procedure? → A: `resolved_skill_tier` seals the exact selected requirement row's canonical `RollSkillId`; `fixed_zero` seals null. The shared resolver filters broad or exact-skill modifiers before the unchanged advantage/disadvantage reducer and Fate Shield logic.
- Q: How does detached treatment replay verify scoped roll modifiers without persisting the full effect snapshot? → A: The procedure authority persists one versioned normalized roll-source authority containing only ordered mechanical `roll_modifier` rows. Live, fresh, and detached paths use the same common reducer. Detached validation proves that the persisted result follows from that source, while fresh validation remains the canonical origin check: it recaptures the source from accepted mechanics, requires exact agreement, and restores every tentative claim registry on mismatch.

### Session 2026-09-07

- Q: Do Spiritual Resilience and Spiritual Healing need a separate experience system? → A: No. Both develop like all other standard spiritual arts: ordinary player training/upgrades and ordinary persistent-entity progression, from visible tier 0 through V. No per-art experience track or progression redesign is introduced.

### Session 2026-09-08

- Q: Do ordinary spiritual actions have an additional source-specific wound-severity ceiling? → A: No. Ordinary actions use the approved formula, target Spiritual Resilience, destination strain, and danger limits without an additional source restriction. A special restriction or guaranteed wound must already be declared by a materialized canonical source and still obey every harder limit. Absence of such a declaration means no additional source cap and no guarantee; it does not disable that source's ordinary harmful-strain wound eligibility.

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
3. **Given** a valid procedure, **When** its sealed check succeeds, partially succeeds, fails, or rolls a natural 1/20, **Then** exactly one predeclared categorized band and its resource policy apply after all hard requirements pass.
4. **Given** an uninterrupted course, **When** every exact dose and inclusive game-time milestone completes, **Then** the declared wound transition occurs exactly once and the course can be reconstructed from durable history after restart.
5. **Given** a source explicitly promising guaranteed healing, **When** the unchanged capability requirement plus the exact canonical owner/skill/domain/severity/operation-limit proof and every sibling requirement, including any separately declared tier gate, succeed, **Then** the deterministic ordered result occurs without an improvised GM override or bypass of the canonical heal gate.
6. **Given** a player proposes a plausible new cure, **When** the GM materializes an evidence-backed alternative route, **Then** it becomes a legal alternative without rewriting earlier history.
7. **Given** a failed diagnosis attempt, **When** its sealed result is applied, **Then** it reveals no hidden route or complication and cannot be replayed as a second attempt.
8. **Given** hidden routes whose only diagnosis paths depend on facts revealed by one another, **When** the wound is validated, **Then** the unresolved discovery cycle is rejected.
9. **Given** a stabilized Mortal wound with a recovery schedule, **When** a later accepted formal re-trauma worsens it, **Then** the client clears stabilization, starts a new deterioration grace period from that exact worsening transition, and preserves the stabilization-rebased recovery schedule.

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
8. **Given** an ordinary action or a special art without a pre-materialized wound restriction, **When** its validated harmful strain transition permits severity IV, **Then** no additional source ceiling lowers that maximum and the GM may still choose a lower wound or none.
9. **Given** a source with a pre-materialized severity-II ceiling, **When** the formula, destination, and danger would allow IV, **Then** its maximum remains II; a newly authored or changed declaration in the harmful exchange cannot supply prior source authority.
10. **Given** a pre-materialized guaranteed wound whose trigger is proven by the accepted source, **When** the declared severity fits every hard limit, **Then** declining that required result is invalid; if it contradicts a harder limit, the guarantee cannot override that limit or silently raise the maximum.

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

1. **Given** first entry to the Chaos Sea with Elyara alive in her initial location, **When** the player seeks healing, **Then** Elyara and the Lazaret are discoverable and her tier cannot be downgraded below V.
2. **Given** a public treatment attempt, **When** the player confirms the displayed Ink Feather price, **Then** the exact quoted amount is paid once even on a valid failed or partial attempt.
3. **Given** a canceled or rolled-back attempt, **When** publication fails, **Then** no payment remains consumed.
4. **Given** an accepted roleplay agreement, **When** treatment resolves, **Then** the agreed favor, debt, quest, or free aid replaces currency but not the healer-tier gate, roll, target, or world-cycle cost.
5. **Given** a materialized Shining faction, **When** its visible roster is inspected, **Then** at least one resident has the visible primary `healing_support` role and a valid tier I-V.
6. **Given** a faction healer without public-service access, **When** the player lacks the required relationship or agreement, **Then** the healer remains visible but command treatment is unavailable.
7. **Given** Elyara has accompanied the player to the Shining Abode, **When** the player selects «Пойти к Элиаре за лечением», **Then** the same command resolves her current canonical location and ordinary reachability/access requirements instead of routing to the Chaos Sea Lazaret; console and browser agree.
8. **Given** Elyara has died, **When** healing offers are built or a previously offered command is submitted, **Then** her treatment command is unavailable and the stale request is rejected before payment, travel or treatment; bootstrap and normalization must not revive or duplicate her to restore the service.
9. **Given** Elyara moves after a treatment offer is displayed, **When** that offer is confirmed, **Then** the client revalidates her current location and access before any side effect rather than using the stale destination.

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

### User Story 8 - Apply Roll Consequences to an Exact Skill (Priority: P1)

As a player, I want a wound or ordinary effect to hinder one exact skill when that is the
authored consequence, without silently affecting every skill or changing the wound when
that skill later becomes unavailable.

**Why this priority**: Arbitrary setting-specific wounds need precise mechanics, and an
implicit or display-name selector would broaden GM output or bind it to the wrong actor.

**Independent Test**: Materialize broad and focused `roll_modifier` components through
ordinary and wound sources, resolve matching and non-matching checks and treatment
procedures, remove/restore the selected skill, restart/replay, and inject publication
failure while verifying exact scope, one-slot accounting, and unchanged source state.

**Acceptance Scenarios**:

1. **Given** a structurally valid exact-skill modifier and one offered/current usable canonical target skill, **When** the effect is accepted, **Then** the permanent `skillId` is bound exactly and no GM display name becomes authority.
2. **Given** a missing scope, illegal field, wrong operation set, same-response new skill, final-disabled skill, wrong owner, duplicate, confusable, idless, unknown, or missing catalog selection, **When** validation runs, **Then** the complete ordinary or wound transaction rejects before any permanent identity or partial write.
3. **Given** broad, matching focused, non-matching focused, and identity-less check contexts, **When** contributions resolve, **Then** scope filtering happens before the unchanged advantage/disadvantage cancellation.
4. **Given** an accepted focused component whose skill later disappears or becomes unusable, **When** mechanics resolve, **Then** only that component is dormant and the effect, wound, severity, treatment, lifetime, and history remain unchanged; restoring the same permanent identity reactivates it.
5. **Given** a Mortal procedure using `resolved_skill_tier` or `fixed_zero`, **When** roll mode is resolved, **Then** the former supplies the selected row's exact sealed `RollSkillId`, the latter supplies null, and focused modifiers contribute only on an exact usable match.
6. **Given** visible broad, focused, dormant, or missing-skill effects, **When** console or browser projection runs, **Then** Russian text describes the scope without exposing a technical ID or revealing a hidden effect.

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
- A legal zero-slot marker root must remain owned even though no consequence entry points to it.
- A persisted `apply_definition` descendant materializes after the creation turn and must be healed with its source wound without becoming a preallocated root or mutating the wound at reaction time.
- One accepted wound event materializes several roots; each receives a unique derived `createdEventRef` while retaining the shared wound event as separate `causalEventRef`, and the wound planner allocates no permanent effect IDs.
- A root-bound definition is also a later reaction target; only same-ownership-domain `replace` may retire its current identity and create a descendant with first-create causal lineage but no additional root binding.
- Four ordinary one-slot roots are combined with a reaction producer, a mechanical leaf, and an optional marker; the fifth mechanical slot must be rejected rather than used to evade the five-definition bound.
- Individually valid wound preparation, effect results, operation events, and final wound bindings are mixed from different plans; the common prepared-plan seal must reject the composition.
- Two complications attempt to claim the same root effect, or a descendant lineage crosses into another wound source.
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
- **FR-011**: Create, worsen, complicate, diagnose, `author_alternative_treatment`, stabilize, treat, recover, heal, legacy, and archive/history transitions MUST be explicit, bounded, deterministic, and replay-safe.
- **FR-012**: The same accepted event, attempt, course milestone, or recovery cycle MUST NOT create or advance a wound more than once. `eventRef`, `operationKey`, non-null `attemptId`, and `(courseId, courseMilestoneOrdinal)` MUST each be exact/confusable-unique replay-safe semantic coordinates. Mortal treatment MUST persist the complete sealed request in typed command/pending authority and accepted history, including the canonical detached pre-attempt wound route source and its semantic fingerprint, and persist the complete resolved requirement-authority bundle in accepted history. Detached parsing MUST re-parse that wound and independently derive the selected route, declared result, resource policy, and mode evidence before recomputing all nested request/bundle/result/receipt seals and replay classification. An exact repeat returns a detached receipt with no actionable intents before reading current state; changed semantics conflict, and fingerprint-only route, request, or requirement history MUST NOT prove replay.
- **FR-013**: Active wounds MUST remain in exact owner-appropriate carriers governed by one common identity/history authority.
- **FR-014**: A physical wound MUST NOT silently convert into a spiritual wound during realm transition.
- **FR-015**: Full healing MUST retain an immutable historical wound record while excluding it from the active-wound list.
- **FR-016**: A healed wound MUST NOT be reopened; later trauma MUST create a new identity with optional historical provenance.
- **FR-017**: A lasting mechanical legacy MUST be an independent #1535 effect linked to the healed wound through a durable typed `sourceKind=wound_legacy` history authority, while a cosmetic legacy MAY remain history-only. The source MUST be non-GM-materializable, the #1535 planner MUST remain the sole effect-ID allocator, active-wound healing MUST NOT capture it, and later effect removal MUST NOT erase terminal/legacy provenance. Version 1 MUST reject open skill/trait/other legacy payloads.

#### Wound-owned effects and consequence budgets

- **FR-018**: Mechanical wound consequences MUST be separate effects whose exact wound source persists as a complete bounded #1535 definition graph plus separate directly materialized root-effect bindings. A wound source MUST be non-materializable through ordinary GM effect commands; only the sealed typed wound batch MAY authorize its exact roots, while later descendants require the #1535 reaction executor. Wound preparation MUST allocate no permanent effect ID; the cached effect plan MUST be the sole opaque ID allocator and MUST return one exact typed result for every root application. Each typed root application MUST carry an internally derived expected component count and domain/versioned materialization fingerprint over its exact source coordinate, schema, parameters, and ordered fully bound components. Its effect result MUST carry the actual count and fingerprint derived from the newly created effect, and both effect planning and wound finalization MUST independently recompute exact agreement from detached source/effect payloads rather than trust carried strings. Every root MUST use a unique client-derived creation event while preserving the accepted wound event as separate causal chronology. Internally computed, independently revalidated stage and common-plan fingerprints MUST bind the exact source exports, typed operations, subordinate result maps/after-images, finalized wound bindings, and common publication plan so valid pieces from different plans cannot be mixed. Every active root or later reaction descendant MUST resolve back to the active wound through its exact source coordinate and persisted `definitionKey`.
- **FR-019**: A wound-owned effect MUST NOT mutate, heal, worsen, delete, retarget, or reopen its source wound.
- **FR-020**: Removing or dispelling a wound-owned effect alone MUST NOT change the wound's severity, care state, recovery, or history.
- **FR-021**: Full wound healing MUST terminate all active root and reaction-descendant effects found through the indexed exact wound source coordinate, MUST reject any active wound-source effect whose `definitionKey` is absent from the persisted graph, and MUST leave unrelated effects unchanged. Selective complication cleanup MUST require pairwise-disjoint complication roots, reconstruct one `base_wound` or exact-complication ownership domain per root, reject cross-domain stack/refresh/merge/replace before mutation, follow only validated same-source first-create causal lineage from those roots including when a declared root is already terminal, remove those root bindings/reciprocal slots, recompute slot use, and prune only definitions unreachable from every remaining root; replacement succession MUST remain non-ownership lifecycle evidence and global effect identity history MUST retain terminal provenance.
- **FR-022**: Spiritual severity I-IV MUST materialize exactly one independently understandable legal afterlife consequence per severity step.
- **FR-023**: Mortal severity I-IV MUST permit at most one, two, three, or four independently understandable mechanical consequences respectively, MUST NOT require a catalog of ready-made wounds, and MUST contain at least one non-display impact: a mechanical consequence, active complication, or care/recovery constraint that changes the legal lifecycle.
- **FR-024**: Display text, symptoms, and technical wound linkage MUST NOT consume a consequence slot; every independent mechanical modifier or restriction MUST consume one slot.
- **FR-025**: Every usable consequence profile MUST belong to the closed version-1 primitive registry below and MUST have client-owned, severity-specific power, cadence, expansion, and scope limits. Here, power means the catalog's heterogeneous profile/severity validation policy and MUST NOT be represented as a universal scalar handoff field. The eight spiritual profiles MUST also be registered as deterministic #1535 components with only `profile_specific` merge, a closed `{ operation, axis, magnitude }` payload, afterlife persistent-actor and exact wound-source-link scope, safe generic player projection, and no `spiritual_conflict_side` or `afterlife_combat_condition` substitution.
- **FR-026**: Heavy or critical restrictions MAY prevent specific actions but MUST preserve at least one path to inspect, communicate, request help, receive treatment, or leave the condition.
- **FR-027**: A severity transition MUST atomically terminate the complete old active/suspended wound-source effect group and then materialize a fresh consequence root set that fits the new slot and power budget; unchanged definitions MUST NOT preserve old runtime effect identities. Every new root retaining an existing definition and ownership coordinate MUST record exactly its prior canonical root as sealed same-definition first-create generation provenance, shared by worsen, treatment, and recovery severity changes. An initial or genuinely new root MUST remain parentless, and a removed definition MAY retain only terminal history. Every retained-coordinate generation spine MUST be unique, acyclic, non-branching, ownership-preserving, and distinguishable from reaction lineage; all prior generations and their reaction descendants MUST be terminal, while forged disconnected terminal history whose definition remains current MUST fail closed.
- **FR-028**: Independent curses, oaths, Fate Card outcomes, and Saref effects MUST NOT consume wound consequence slots unless the event explicitly materializes them as trauma-caused wound consequences.

Version-1 Mortal wound effects MAY use the existing generic profiles
`characteristic_modifier`, `roll_modifier`, `resistance_modifier`,
`periodic_damage`, `periodic_restore`, `action_control`, and `event_reaction`.
The `wound_consequence` source/display marker costs zero slots, and at most one such
marker definition may exist across the wound-owned definition graph. When the marker is
materialized directly, it remains a root binding even though it has no consequence
entry; when it is a reaction-only descendant, it follows the ordinary descendant rule.
A definition reached later through `apply_definition` has neither a root binding nor a
preallocated effect identity; its worst-case mechanics are charged to the originating
reaction root and its identity is allocated only by the accepted effect runtime. Version
1 permits zero or one reachable leaf and no nested wound-owned expansion; when the edge
exists, the reaction's #1535 `maxExpansion` is exactly `2` while the wound budget counts
one downstream expansion. A zero-edge graph remains legal. A reaction-created instance
records the producing effect identity
but receives no additional root binding, including when its definition is also
root-bound. A root-bound reaction target MUST use the exact legal `replace` policy and
MUST share its producer's reconstructed root-ownership domain; other stacking policies
or cross-domain mutation are rejected before effect state changes. The reaction
component itself consumes one consequence slot, and every flattened non-marker leaf
component consumes another. Each independently affected characteristic, resistance,
periodic resource operation, action, or worst-case reaction result costs one slot. A
complete `roll_modifier` component costs exactly one slot regardless of how many
registered operations share its single contribution and scope; selecting another exact
skill requires another component and another slot.

Every wound-owned definition has an exact/confusable-unique `stackKey` and exact
`maxStacks = 1`; all remaining stacking fields obey their #1535 policy combinations.
This permits no more than one active/suspended instance per definition without removing
ordinary legal refresh, replace, merge, or same-domain reaction reuse.

Per-slot severity limits are:

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
MUST execute at most once for one accepted source event. Each complete `roll_modifier`
component costs one slot regardless of its registered operation list or `all|skill`
scope; the scope selects applicability and does not expand power or slot count. Reaction
slot cost includes every worst-case spawned
mechanical component. The single wound expansion ceiling counts wound-owned expansions
only; bounded independent effect siblings neither consume this ceiling nor wound slots.
Independently of that semantic ceiling, one raw effect proposal may carry at most 64
flattened reaction-expansion rows as a structural work bound; rows from an independent
proposal remain preserved through exact ownership classification.
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

These exact eight identifiers are first-class registered #1535 component profiles,
not aliases for `afterlife_combat_condition`. Each is deterministic and declares only
the `profile_specific` merge reducer. Their common component payload is the
closed object `{ operation, axis, magnitude }`; the common effect contract validates
the profile-invariant operation, axis, magnitude domain, and closed shape, while this
wound contract remains the sole authority for severity availability, exact
severity-specific magnitude, slot count, duplicate coordinates, and aggregate safe
exit. The exact JSON operation/axis/magnitude domains are normative in
`contracts/wound-effects-and-atomicity.md`. A source definition containing one of these profiles is legal only in
`chaos_sea` or `shining_abode`, targets a persistent actor (`player`, `guardian`,
`resident`, `radiant_actor`, or `afterlife_actor`), and contains an exact wound link
with role `source`. It MUST NOT target `spiritual_conflict_side`.

The accepted effect persists on the exact actor/profile carrier under the ordinary
#1535 lifetime contract; the normal wound-owned form is `source_bound(active, expire)`.
During an active conflict, when that target actor resolves to exactly one current
accepted participant and side, a typed owner-to-current-side projector MUST emit the
exact conflict-only mechanical contribution for every applicable component. With no
active conflict it emits none; absent, duplicate, wrong-realm, or side-ambiguous
membership MUST reject before any partial contribution or after-image. This behavior is
not caller-selectable. Derived evidence is not a canonical effect, wound, or combat
condition, MUST NOT mutate or duplicate `combatConditions[]`, and disappears with the
conflict while the actor's wound and effect remain governed by their own lifecycles.

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
  The declaration/persistence prerequisite requires exact lowercase JSON strings
  `training`, `controlled`, `hostile`, or `annihilation` on the selected start
  seed, canonical active conflict and each recent proof, with no implicit default
  or old-save fallback. Ordinary exchange/replacement omission preserves the
  accepted declaration; explicit null or a different echo does not authorize a
  change. Resolve/repair-cancel retain the declaration in the terminal proof.
  During a validated turn, every retained same-ID active/recent occurrence MUST
  agree with the signed pre-turn declarations; missing, invalid or conflicting
  retained baseline declarations fail closed. Existing legal recent-history
  truncation and independent terminal-proof qualification remain unchanged.
  This prerequisite does not define accepted escalation evidence, global
  conflict-ID uniqueness, wound seals, actual injury, healing or the full
  player-visible pre-first-exchange workflow.
- **FR-030**: Training MUST forbid spiritual wounds; controlled conflict MUST cap them at II; hostile and annihilation conflict MAY permit I-IV.
- **FR-031**: Ordinary spiritual wound eligibility MUST occur only on an accepted harmful strain transition and MUST reuse the accepted exchange evidence without a second injury roll.
  As a prerequisite, historical exchange dice/matchup/action-cost exemption MUST
  consume one occurrence from the same validated pre-turn active conflict, not
  trust `exchangeAtTurn` alone. Exact payload matching remains first. The existing
  old-marker readable-summary audit compatibility MAY ignore only an optional
  missing/null/string top-level `summary` when every other member is exact;
  this MUST NOT authorize rewriting the accepted resource-history prefix or
  treating historical prose as a new harmful strain event. The resource publisher's
  exact-prefix fence remains independent and unchanged.
  The related Light Incarnate pre-grant/no-marker exemption during a validated
  turn MUST use accepted pre-turn payload evidence as well: active exchanges
  reuse their same-conflict one-use classifier, while recent resolutions consume
  exact occurrences from their own pre-turn list. These surfaces MUST NOT share
  an exemption pool, and absent dice MUST NOT erase a validated baseline.
  Existing offline compatibility without a validated baseline and the exact
  capstone closure/bonus rules remain unchanged. This prerequisite does not
  establish full grant, wound, or publication authority.
- **FR-032**: The maximum spiritual severity MUST be calculated from harmful margin, applied-art tier, target Spiritual Resilience tier, destination strain rank, extra strain jumps, and the conflict-mode cap according to the approved design formula. Ordinary spiritual actions MUST have no additional source-specific ceiling. A special source restriction or guaranteed wound MUST derive from an exact pre-materialized canonical source declaration proven before the harmful event, never from the current wound proposal, a caller-provided cap, or art cost. Such a declaration MUST NOT override the formula, destination-strain, danger, or other harder bounds. Absence of a special declaration MUST mean no additional source restriction and no guarantee, while preserving ordinary harmful-strain eligibility for that source.
- **FR-033**: Natural 1 and natural 20 in the conflict exchange MUST NOT independently raise spiritual wound severity.
- **FR-034**: Each side MUST receive at most one newly created spiritual wound per conflict.
- **FR-035**: Later wound choices in the same conflict MAY worsen that exact conflict wound within the new envelope and MUST NOT create a duplicate.
- **FR-035A (2026-09-24 approved clarification)**: When a later spiritual source has a proven pre-materialized wound guarantee and the same side already has its one new conflict wound, the client MUST compare the exact current wound severity with the guaranteed rank. If it is lower, a lawful worsening MUST reach exactly the guaranteed rank without exceeding the source's hard maximum. If it is equal or higher, the client MUST treat the guarantee as already satisfied by that exact wound, including when the wound is at or above the source maximum.
  - The client MUST NOT create another wound, raise severity beyond the source maximum, append a wound-history transition for an already satisfied guarantee, ask the GM to decline a guaranteed result, or silently omit the source.
  - The client MUST derive one append-only `guarantee_satisfied` decision from the authenticated source and current owned wound state; this is a client-owned outcome, not a GM-authored choice. Its receipt row MUST bind the exact existing `woundId` and `satisfiedSeverityRank` at satisfaction time, carry null `transitionId` and `selectedSeverityRank`, and preserve the complete source witness and causal order. The closed receipt schema MUST accept this outcome while continuing to read existing version-1 `none` and `materialize` rows.
  - Cold replay MUST reproduce the same decision without reapplying mechanics; a changed source, wound identity, severity, side or conflict instance MUST reject. An explicit older-wound re-trauma retains its separate target authority and MUST NOT use the side's conflict wound as a substitute. If no exact current conflict wound proves satisfaction, the turn MUST fail closed rather than invent a result.
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

- **FR-040**: Spiritual Resilience and Spiritual Healing MUST be visible standard arts at tier 0 and MUST use ordinary standard-art progression through tier V: existing player training/upgrades and persistent-entity progression, with the same eligibility, costs, and acceptance rules as other standard arts. Neither art introduces a separate experience track or a new progression system.
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
- **FR-058**: Every diagnosis path MUST have a readable name, at most 16 unique ordered typed already-known fact prerequisites, at most 16 exact world requirements, a sealed check, and at most 16 unique ordered exact declared reveals. The client MUST compute the least discovery fixed point from current known routes and public/player-known complications; `hidden` paths require a non-empty satisfied known-fact prerequisite, `gm_only` paths never establish player reachability, unresolved cycles MUST fail closed, and every hidden treatment route MUST be present in the resulting reachable set. Exact item/provider/location/quest/capability reachability MUST be proven separately against fresh canonical authority.
- **FR-059**: A new evidence-backed alternative treatment route MAY be added only by one sealed `author_alternative_treatment` transition. It MUST append exactly one complete route, MUST append one reachable diagnosis path atomically when the new route remains hidden, MUST preserve all prior routes, paths, and history rows byte-for-byte, and MUST append exactly one new transition history row.
- **FR-060**: One treatment route MAY require exact items/doses, capabilities, skills, providers, facilities, time, ordered steps, and environmental conditions; all declared requirements in that route MUST hold.
- **FR-061**: Separate treatment routes MUST be alternatives and MAY have different risks, resource costs, or outcomes.
- **FR-062**: Procedure routes MUST use a predeclared bounded check, a closed zero-or-exact-satisfied-skill-tier modifier source, accepted roll-actor `skill_check` effect authority, current complication difficulty, and categorized ordered gapless non-overlapping result bands with exact/confusable-unique IDs. Every `success` band, including natural 20, MUST structurally contain a positive kind and exclude no-op/harm kinds; before each fresh attempt every band MUST simulate to a legal complete transition against the sealed current wound, and every success band MUST additionally produce an actual monotone improvement. An inapplicable partial/failed band MUST block the fresh attempt before its sealed die can become a free reroll while leaving the persisted route structurally valid. New attempts MUST derive context, wound, history, dice, time, skill, effects, complete requirement/resource authority, and deterministic outcome identities under one canonical lease; raw JSON, caller dice/index/modifier, detached wound/route/baseline, or caller seal is invalid. After hard gates, natural 20 MUST select the first band, natural 1 MUST first establish last-band `critical_failure`, and rolls 2-19 MUST select one band by sealed margin. A prepared player Fate Shield MUST reuse the shared oldest-eligible arbiter, remap only that natural 1 to ordinary `failure` and the first failed band, and emit one atomic intent; NPC checks MUST NOT consume it, the legacy GM API MUST remain unchanged, and a same-turn typed treatment plus legacy GM critical report MUST be rejected before either claim is confirmed. Course routes MUST use exact success milestones, inclusive canonical-minute windows, one complete sealed starting-wound/course-mode authority, and one non-beneficial failed interruption. A first course MUST require no active course; continuation MUST reconstruct the same baseline and next ordinal; another course MUST reject, while procedure/guaranteed treatment MAY occur and heal/completion/interruption MUST clear the pointer. Only intermediate milestones MAY be empty; the complete sequence MUST simulate to a real monotone improvement at start and each selected milestone MUST revalidate against current state. Every non-early course MUST classify fresh requirements even after deadline; only trustworthy `Unsatisfied` predicates or deadline interrupt, deadline dominates, `InvalidAuthority` rejects, interruption complication MUST be effectless/difficulty 1-4, and deterioration MUST be strictly worsening. Guaranteed routes MUST require both the unchanged capability row and an exact canonical player/NPC skill proof whose version-1 domain is exactly `physical`, permanent skill identity/actor role/severity/aggregate bounds agree, and final composed proof is re-exported before publication. Combatant/member wounds MUST remain treatable by procedure/course and by a provider-owned canonical guarantee; a target-owned guarantee MUST first use existing accepted persistence/promotion authority.
  Every non-empty ordinary course milestone MUST contain only monotone positive operation
  kinds. `no_improvement`, `add_complication`, and `apply_deterioration` MUST be forbidden
  there and confined to the separately validated interruption branch.
- **FR-063**: The client MUST validate and provisionally reserve exact resources before semantic treatment resolution, seal the resulting full requirement/resource authorities inside one immutable request, and consume/release them only through post-resolution `Finalize(resolution)`. Command, pending, and accepted history MUST persist that complete request; exact command/pending duplicates MUST coalesce by operation/attempt/request fingerprint and divergent duplicates MUST conflict. A cold restart from the same durable bytes under a new filesystem root and fresh process-local registries MUST reconstruct the same request and claims without relying on cached root identity. The reconstructed request MUST pass through the ordinary mode-specific T067 reducer with parsed history and fresh canonical before/accepted-state authority to recreate the exact full Resolution, including ordered outcome intents and nullable critical-reaction intent, before the existing six-argument T070 publication consumes that typed pair. Actionable intents MUST NOT be copied from raw persisted command result JSON; every nested request/result seal MUST be recomputed and any post-seal result tamper MUST fail parsing or recomposition. A failure before durable confirmation MUST release every provisional die, Fate, and resource claim, leave no command/pending/history duplicate, and permit the exact logical operation to reserve again. A terminal accepted-turn rollback or cancellation after confirmation MUST atomically remove the exact durable command/pending copies and release the matching confirmed hold; if either half fails, it MUST restore the removed bytes and retain the hold so no dangling or overbookable state is exposed. A retryable in-flight publication failure after treatment-specific writes but before full-pipeline completion MUST compensate those writes, retain the exact durable request and confirmed hold, and re-arm the original exact plan only when its cache slot and receipt fence are still valid; otherwise it MUST preserve any competing admitted plan and fail closed with a restart-required blocker. The detached replay receipt MAY expose only verified fingerprints. Resolution emits only a declared `success|partial_success|failed_attempt|none` trigger; course interruption consumes neither the unmet/current nor future step, reusable requirements MAY be reserved without consumption, and raw item/resource mutations remain outside the resolver.
  Every selected `item_quantity` claim MUST publish through that same immutable common plan
  and transaction in frozen finalization order. A partial consume retains the permanent
  active item identity, receipt, and carrier, decrements its count exactly, and scales every
  live item-owned `instance_fixed` resource maximum and current by the exact remaining/
  source-count ratio with quantum alignment and no rounding; any unsupported or inexact
  capacity makes the complete mixed item/resource plan invalid before publication. A full
  consume removes the item, clears only supported inline equipment, records terminal
  `consumed` identity authority, and retires every live item-owned resource; a container,
  quest, bond, or other companion reference without its own genuine transition authority
  rejects the complete plan. Repeated selected claims emit sequential transitions
  rather than one aggregate transition, with private deterministic identities bound to the
  sealed request, result/finalization, claim fingerprint, and ordinal. Item carrier/index,
  shared NPC skill/item roots, the resource quartet, wound/effect/history/output, and
  command state MUST be composed without an alternate writer or lost same-turn work and
  MUST roll back or replay as one unit. B.4 MAY admit only the seven closed ordinary Mortal
  item command properties named in the 2026-09-02 clarification alongside the existing six
  skill fields; every other non-null response property remains unsupported. Consumption
  MUST apply to a sealed exact baseline equal to the output immediately before common-plan
  publication of every ordinary normalizer transform that can touch the selected carrier
  roots, and the live roots MUST match that baseline before publication. The baseline MUST
  seal detached `NpcCoreChangesContract.Authority` plus exact NPC-trade and training pending
  bytes used by ordinary NPC-core processing. It MUST exclude the treatment B.2 skill
  projection; the common candidate applies that sealed projection to the verified baseline
  and applies item consumption to the resulting skill root. The accepted item snapshot MUST
  seal complete backup/current carrier roots plus privately derived deterministic root
  receipt and create/transfer transition IDs; the ordinary normalizer and treatment
  projector MUST use one shared pure transfer transform and produce the same receipt/history
  without a direct writer call. Current and backup root maps MUST use nullable `JsonNode`
  values and output maps MUST also use nullable `JsonNode` values, retaining every key and
  using null to prove an absent file; a present JSON-null root is invalid before projection.
  Snapshot proof MUST retain exact file presence and top-level object/array topology.
  Snapshot validation MUST be
  bidirectional over the complete carrier/command/index/companion root-path set, including
  legacy vehicle object/array forms and effective post-location roots; no missing frozen or
  extra supplied path is acceptable. Only ordinary transforms touching the selected item
  graph participate, in exact tail order: quest history, NPC core, conditional NPC trade,
  inventory items journal, item bonds, item text updates, then NPC item journals. The
  baseline result MUST expose and fingerprint this order as exact `AppliedTransformIds`
  `quest_history:v1`, `npc_core:v1`, one disposition-matching NPC-trade ID,
  `inventory_items_journal:v1`, `item_bonds:v1`, `item_text_updates:v1`, and
  `npc_item_journals:v1`; planner dispatch MUST use that ordered registry rather than
  merely reporting it after independently ordered execution. The NPC-trade ID MUST be
  exactly `npc_trade:apply:v1` or
  `npc_trade:skip_untouched_treatment_continuation:v1`. The
  planner MUST expose the corresponding base `TransformRegistry` with `npc_trade:v1`,
  iterate it exactly once, invoke `ApplyRegisteredTransform` once per registration, and
  append each returned applied ID inside that same loop. `Apply` MUST apply and remove
  `UpdateNpcTradeInventoryReceipts`; `SkipUntouchedTreatmentContinuation` MUST leave the
  post-NPC-core root and command unchanged and MUST NOT create the receipt. The
  authenticated `MortalItemNpcTradeTailDisposition` closed to `Apply` or
  `SkipUntouchedTreatmentContinuation` and its treatment-continuation skip gate MUST be
  sealed and fingerprinted. The B.2 projector MUST retain the true live canonical
  before-image for rollback separately from its supplied semantic final ordinary NPC
  baseline. Creation ordinals MUST preserve collector order `UpdateInventory`, NPC core,
  NPC commands, current location, then offscreen storage. Already validated route and
  transfer catalogs, effective roots, and snapshots MUST be forwarded and MUST NOT be
  reread or rebuilt by registration, projection, or normalization.
  Before any new live procedure reservation, accepted-state binding MUST perform exactly
  one atomic claim-recovery phase in the persisted catalog's coalesced first-authoritative
  order. Recovery MUST restore exact dice spans; prove every carried player natural-1
  Fate choice reachable from oldest-candidate selection using only lower, unoccupied,
  roll-topology-compatible spans; keep temporary historical span-to-candidate mappings
  local to the recovery batch; and treat a later accepted span overlap as evidence that
  the earlier temporary reservation was released. Only exact reconstructed dice/Fate
  registries may publish after fresh-authority validation. An exact repeated recovery is
  idempotent, a changed request set conflicts, late recovery rejects, and virtual claims
  MUST NOT enter live state.
- **FR-064**: Accepted Mortal treatment MAY stabilize, add/remove a declared complication, add recovery progress, reduce severity, apply an exact declared deterioration policy, or heal only through selected closed ordered typed operations. Every result MUST contain at most one `heal`, it MUST be final, and its eight-row legacy limit MUST be aggregate. Recovery and capability aggregates MUST use checked signed-64-bit arithmetic; severity reduction MUST aggregate to at most two without heal or at most three solely to reach working severity I before the separate follow-up heal. In an initial same-proposal route, removal MUST name a response-local `complicationRef` and the client adapter MUST rewrite it to canonical `complicationId`; a persisted/later route MAY name only an existing exact ID. New-complication refs MUST be exact/confusable unique across the selected result and their nested refs MUST be operation/ref-namespaced. A new complication MUST reuse the existing response-local complication plus optional exact-empty-or-complete consequence graph. Mechanical-legacy refs MUST be namespaced by exact/confusable-unique `localLegacyRef`; cross-draft local spellings MAY repeat only under distinct namespaces. A lasting legacy MUST be cosmetic or a complete proposal-safe #1535 `mechanical_effect` draft with 1-5 definitions/applications under `heal`; #1535 remains the sole effect-ID allocator. T067 MUST emit one immutable typed outcome intent per declared operation with deterministic IDs/ref maps/child coordinates, and T070 MUST consume that handoff without reparsing raw route JSON or reallocating identity. Legacy preparation MUST precede #1535 and durable legacy history MUST be emitted only after a finalizer verifies the accepted result map/effect-plan seal. Open entities, names, colon tokens, accepted-result fields, incomplete payloads, raw mutations, arithmetic overflow, and coordinate collisions MUST reject before publication. No critical, course, or guarantee bypasses the severity-I heal gate.
  Legacy preparation MUST expose one ordered `WoundEffectOperationBatch` per selected
  `mechanical_effect` legacy and none for cosmetic legacies. Each batch MUST export only
  that legacy's independent `wound_legacy/legacyId` source; missing, extra, reordered,
  merged, or split batches MUST reject before publication.
- **FR-065**: Every Mortal wound MUST use one explicit natural-recovery policy: progressive, requires stabilization, or no natural recovery, with optional bounded deterioration. A version-1 deterioration policy MUST contain exactly `policyRef`, singleton `unmetConditions`, non-negative signed-64-bit `graceMinutes`, positive signed-64-bit `cadenceMinutes`, and one closed result. Its adverse result MUST be exactly one-tier `increase_severity`, one complete existing-dialect `add_complication` draft with treatment difficulty 1-4 and an optional complete consequence graph that fits the current wound envelope, or explicit `death_contour`; no severity-IV increase may silently become death. `no_change`, `add_recovery`, an unknown/open result, or a currently inapplicable adverse result MUST NOT grant strictly-worsening authority. Every elapsed Mortal cadence/grace MUST use canonical `world_time.currentTimeInMinutes` and checked minute arithmetic rather than wall time or a parallel seconds clock. Stabilization MUST clear the active `not_stabilized` deterioration anchor and rebase only the recovery anchor; a later accepted worsening that re-enters the condition MUST allocate a new deterioration anchor bound to that exact `worsen` transition without resetting the recovery anchor.
- **FR-066**: Mortal recovery and deterioration MUST use exact elapsed/event evidence and MUST NOT be advanced twice by repeated prose or retry. Every formal, QTE, combat, trap, check, hazard, or narrative Mortal opportunity MUST resolve exactly one client-sealed accepted occurrence from the active signed pending-turn snapshot; source-shaped input is correlation only and MUST NOT grant outcome, causal-profile, owner, source-state, legal-location, or severity authority. A current combatant, hazard, die, source definition, or prose description alone MUST NOT prove that a harmful occurrence happened. A harmless typed result MUST produce no occurrence or public opportunity. Each harmful occurrence MUST persist its complete bounded ordered accepted-event authority, stable producer-operation replay key, and exact ordinal/count in the complete source-result candidate batch. Initial composition and accepted-turn validation MUST independently reconstruct and compare that complete set, and the selected wound event MUST be one exact ordinal within it. Missing, added, removed, reordered, stale, cross-profile, harmless, inactive, or tampered occurrence/event evidence MUST publish nothing; an exact producer retry MUST reuse a pending occurrence or its durable consumed receipt and changed semantics under its key MUST conflict. An absent worsening coordinate MUST mean create; worsening MUST require one explicit exact active-wound coordinate and MUST NOT be inferred. Every accepted opportunity decision, including `none`, MUST publish one append-only client-owned decision receipt through the common accepted transaction; that receipt MUST retain the consumed occurrence's source replay coordinates and fingerprints. Exact cold replay MUST emit no new command or transition, a changed decision for the same opportunity MUST conflict, and only a newly sealed occurrence/snapshot MAY create a new opportunity. `none` MUST have no wound-history row, while `materialize` MUST name and exactly agree with one `create|worsen` history transition. A formal re-trauma MUST enter through the production Mortal opportunity adapter bound to that authority, then the ordinary `StateDistributor` and `ValidationService` contour; a test, GM response, treatment binding, or caller MUST NOT manufacture or repurpose its occurrence, event binding, evidence seal, transition ID, receipt, or anchor.

For FR-066, source replay coordinates are exactly session ID, request ID, snapshot token,
and turn. The stored historical accepted-event vector MUST be recomposed under those
coordinates before the same ordered coordinate vector is rebound to the active decision
snapshot; generic sibling seals change with that binding while selected wound semantics
remain derived. Cold replay authority exists only while that active snapshot retains the
signed pre-consumption occurrence. The source-shaped input MUST NOT contain hard maximum,
minimum/guarantee, binding, event kind/ID/ref/seal, receipt, transition, proposal, anchor,
or after-image fields.

For FR-066, the nullable minimum severity and guaranteed-trigger authority are one exact
pair: both are null for an ordinary occurrence, while a guaranteed occurrence stores a
complete pre-materialized trigger whose required rank equals the minimum and remains
inside the source-result and hard maxima. The guarantee is part of the occurrence seal,
cannot be supplied by later correlation input, and makes `none` invalid after restart.

#### Commands, targeting, history, and privacy

- **FR-067**: `/раны` and `/wounds` MUST show only the player's active current-realm wounds by default.
- **FR-068**: Healed wounds MUST appear only through a separate History action.
- **FR-069**: Active wound detail MUST show readable severity, origin, symptoms, visible effects, recovery state, known treatment options, available help, and a treatment action without internal metadata.
- **FR-070**: `/лечить` and `/treat` MUST provide one guided treatment flow; `/исцелить` and `/heal` MUST act as afterlife aliases.
- **FR-071**: Diagnosis MUST be integrated into the guided treatment flow. Client-sealed diagnosis evidence MUST declare exactly `success` or `failure`: success reveals the complete fact set declared by exactly one reachable path, while failure reveals none, leaks no hidden route or complication, and remains one terminal retry-safe attempt.
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
- **FR-081**: Elyara of the Last Wound MUST initially be discoverable from first Chaos Sea entry, fixed at Spiritual Healing tier V, and provide public treatment at the Lazaret at the ordinary multiplier. The Lazaret is her initial service location, not a permanent presence guarantee. The universal «Пойти к Элиаре за лечением» command MUST resolve the same canonical Elyara's current location, including the Shining Abode when she accompanies the player, subject to ordinary reachability and access rules. Her death MUST make the command unavailable entirely. Offer construction and execution MUST revalidate life state, location and access; stale offers MUST NOT spend payment, travel or treat. Bootstrap/normalization MUST preserve accepted relocation and death rather than restore, duplicate or resurrect her for service availability. These rules apply equally to console and browser.
- **FR-082**: Traveling to or receiving safe-cycle treatment from Elyara MUST advance the afterlife world scheduler exactly once.
- **FR-083**: Every materialized Shining faction in the single Shining Abode MUST have at least one visible primary `healing_support` resident with Spiritual Healing tier I-V.
- **FR-084**: A Shining healer's role MUST be visible with the roster, while service access MAY depend on faction-specific relationship, payment, debt, or agreement conditions.
- **FR-085**: Shining factions MUST NOT be required to expose one universal public paid clinic.

#### GM guidance and completion integrity

- **FR-086**: GM prompts, guides, contracts, examples, manifests, and source guards MUST describe every GM-authored wound, treatment, repair, service, role, and afterlife outcome surface changed by the feature.
- **FR-087**: Worked examples MUST include Mortal construction/treatment, spiritual optional and guaranteed wounds, rejected repair, active/natural healing, entity recovery, Elyara, Shining healer access, healed legacy, and independent Saref memory suppression.
- **FR-088**: A wound or healing capability MUST NOT be considered complete while console/browser behavior, canonical detail authority, GM guidance, or required worked examples are missing.

#### Exact skill scope for common roll modifiers

- **FR-089**: Every accepted common `roll_modifier` payload MUST be a closed object containing exactly `operations`, `contribution`, and one explicit closed `scope`; missing scope MUST reject and MUST NOT imply `all`.
- **FR-090**: `scope` MUST be exactly `{ "kind": "all" }` or `{ "kind": "skill", "skillId": "<canonical-id>" }`; `kind=all` MUST forbid `skillId`, and `kind=skill` MUST require exactly one non-empty permanent `skillId`, forbid aliases/names/arrays/extra fields, and require `operations` to equal exactly `["skill_check"]`.
- **FR-091**: New focused modifiers MUST bind by exact ordinal permanent identity to exactly one current usable active/passive skill row owned by the exact effect target in both the offered pre-turn catalog and composed final accepted state; idless, inactive, terminal, stale, newly introduced, unknown, wrong-owner, exact-duplicate, Unicode-confusable, ambiguous, no-catalog, and over-bound authorities MUST reject atomically.
- **FR-092**: The client MUST publish a bounded, deterministic, detached `effectSkillScopeCatalog` of selectable pre-turn target skill IDs and display names to the GM, while accepted-state validation MUST recompute authority from canonical roots and MUST NOT trust an edited request catalog.
- **FR-093**: Runtime roll resolution MUST consume one trusted context containing exact realm, actor kind/ID, registered operation, and nullable canonical `skillId`; it MUST filter actor/realm/operation and scope before applying the existing non-escalating advantage/disadvantage reduction.
- **FR-094**: A previously valid focused component MUST be derived as dormant when the selected skill is missing or unavailable, without mutating the effect, source wound, severity, treatment, duration, or history; only restoration of the same permanent canonical identity MAY reactivate it, and invalid/ambiguous current authority MUST fail closed.
- **FR-095**: Mortal procedure authority MUST seal nullable `RollSkillId`: `resolved_skill_tier` MUST use the exact canonical skill row selected by its satisfied requirement, while `fixed_zero` MUST use null; live resolution, fresh validation, detached replay, fingerprints, and final treatment after-images MUST agree on that value.
- **FR-096**: Each broad or focused `roll_modifier` component MUST consume exactly one wound-consequence slot; selecting two skills MUST require two components and two slots, while scope metadata MUST NOT raise contribution power or create an additional slot.
- **FR-097**: `scope` and `skillId` MUST participate in every semantic clone, serialization, source/instance agreement check, cache key, snapshot, fingerprint, rematerialization, restart, replay, repair, and rollback boundary; selector changes MUST be changed requests, and failure MUST leave no partial carrier, index, wound, history, pending, or output publication.
- **FR-098**: Player projection MUST translate broad, focused, unavailable, and missing-skill scopes into safe in-world Russian text without exposing technical `skillId`, selecting a similar name, or bypassing existing hidden-effect visibility.
- **FR-099**: Repository bootstrap state, fixtures, built-in sources, tests, prompts, guides, examples, manifests, and source guards MUST move directly to explicit scope; no migration, fallback parser, dual schema, or implicit legacy default is permitted.
- **FR-100**: Fast pure tests MUST own structural validation, skill authority, reduction, slot accounting, and projection; file/cache/restart/replay/rollback and treatment lifecycle evidence MUST remain in Integration or other appropriate lifecycle lanes, followed by focused documentation guards, one meaningful Fast checkpoint, conditional FullValidation, review, and PreMerge only at the actual merge boundary.
- **FR-101**: Mortal procedure authority MUST persist one immutable, versioned, bounded normalized roll-source authority captured from every active common `roll_modifier` row before realm, actor, operation, or skill filtering. It MUST contain only ordered effect/component identity, realm, exact target, complete operations, contribution, and closed scope data; it MUST exclude display text, descriptions, owners, carrier roots, arbitrary payload members, and the full `EffectMechanicsSnapshot`. Live creation, fresh validation, and detached replay MUST invoke one common validation/filter/reduction core. Detached replay MUST derive any exact selected-skill usability proof only from the recursively validated requirement row and witness; `fixed_zero` supplies no such proof. Fresh validation MUST independently recapture and compare the normalized source and result, and any jointly resealed source/result mismatch during cold claim recovery MUST restore all previous die, Fate, and treatment-resource registries before returning failure.

### Key Entities *(include if feature involves data)*

- **Wound**: An independently treatable physical or spiritual injury with stable identity, owner, cause, severity, location, symptoms, care state, consequences, treatment/recovery policy, visibility, and history.
- **Wound Opportunity**: Sealed evidence that an accepted event permits, forbids, caps, or guarantees a wound for one exact target.
- **Wound Transition**: One accepted creation, worsening, complication, diagnosis, alternative-treatment authoring, stabilization, treatment, recovery, healing, legacy, or archive/history change.
- **Wound Consequence**: One independently understandable mechanical result represented by a slot-charged wound-owned root effect or its already-budgeted reaction descendant; the wound persists the complete source-definition graph separately from directly materialized root bindings.
- **Treatment Route**: A world-specific diagnosis/treatment contract with visibility, exact requirements, resolution mode, outcomes, resource behavior, and failure/interruption rules.
- **Recovery Policy**: The exact natural-recovery clock, thresholds, blockers, and optional deterioration rules for one wound.
- **Healing Session**: A sealed active treatment attempt with healer, target, wound, world/combat cost, roll or route result, and replay protection.
- **Healing Service Profile**: A public provider's realm, location, availability, compensation, price, and access contract, separate from raw healing capability.
- **Wound History**: Ordered immutable player-readable provenance backed by terminal transition authority.
- **Independent Legacy/Effect**: A lasting result whose lifecycle is separate from a healed wound but whose origin may reference it.
- **Roll Modifier Scope**: The closed common `all` or exact canonical `skill` selector carried by one #1535 `roll_modifier`, bound against one target and evaluated through a trusted current roll context.
- **Skill Scope Authority**: Detached offered/current canonical player and NPC skill catalogs used for new binding, runtime usability, GM selection projection, deterministic fingerprints, and fail-closed ambiguity detection.

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
- **SC-014**: Contract tests accept 100% of the two legal closed scope variants and reject every tested missing, extra, cross-field, wrong-target, stale, duplicate, confusable, idless, unavailable, or no-catalog variant before publication.
- **SC-015**: Broad/exact/mismatch/identity-less, treatment, dormancy/restoration, replay, and rollback matrices produce the expected contribution and state outcomes with zero technical `skillId` leakage and zero wound mutation from skill availability alone.
- **SC-016**: Semantic scans and executable documentation/source guards find zero active two-field `roll_modifier` payloads or implicit missing-scope behavior across repository state, fixtures, sources, examples, and GM guidance.

## Internal spiritual exchange staging boundary — 2026-09-08

Source issue: #1536. T081-B2C-J2-C1 introduces one shared client-owned resource
execution state for fixed input and staged whole exchanges. Preparing or replacing
the next unaccepted exchange allocates nothing and changes no accepted prefix.
Both sides and all causal descendants close before one owned interval is emitted;
an accepted exchange identity cannot close again, including zero-cost exchanges.
Exact fixed-input state, history, identities, statistics and causal results remain
unchanged. Missing evaluation or a receipt stays unsealed pending, not zero evidence.

This internal refactor adds no Mortal World or afterlife GM-authored capability,
command, schema, pending file or publication path. C1 does not enable live gameplay.
C2 must bind actual B1 source ownership and current effect generations and implement
same-owner receipt/missing-side continuation; D must insert wounds and consume their
effects in all eight dependent combat surfaces; E must reconstruct interrupted turns
and publish exactly once. These are mandatory parts of #1536, not deferred gameplay
exclusions or grounds for closing the full feature.

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
- Elyara of the Last Wound remains the canonical Healing-domain Guardian and initial Lazaret healer; she may accompany the player to the Shining Abode or die in combat. Service availability follows her canonical life state, current location and access, not an always-available NPC assumption (owner clarification, 2026-09-22).
- Ink Feathers remain the ordinary command-service currency in both Chaos Sea and Shining contexts; Light Sparks are not ordinary payment.
- A safe afterlife cycle is a completed canonical progression cycle outside active conflict and without an explicit unsafe-state blocker.
- Physical wounds do not automatically transfer to a soul; any spiritual legacy requires a separately accepted spiritual wound or independent effect.
- No closed list of complete Mortal or spiritual wounds will be introduced.

## Chronological spiritual resources — clarification 2026-09-16

#1536 / C2-ORDER: the owner chose chronological exchanges over global whole-turn
phase sorting. Execute the original ordinary/lifecycle prefix once, then each
exchange and its causal closure in journal order. A wait retains that same owner
and prefix. Recovery in an earlier exchange precedes a later exchange cost.
The fixed adapter keeps its old behavior; cross-mode equality is required only
when ordering agrees. Detailed rules and examples are authoritative in
`contracts/spiritual-wound-live-turn-boundary.md`, chronological execution section.

## C2-PREFIX — ordinary resources before the first exchange (review version 2026-09-19)

Source: [issue #1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536), task T081-B2C-J2-C2-PREFIX.
Status: this written version was explicitly approved by the owner on 2026-09-19; implementation remains subject to canonical planning, task decomposition and consistency analysis.

### Requested result and scope

In the retained chronological path, the first spiritual exchange must use the
resource state produced by the accepted ordinary/lifecycle prefix of this turn.
For example, starting with 6 action points, an ordinary spend of 1 leaves 5;
the first exchange's audit 5 -> 2 for a cost of 3 must be accepted. The original
signed before-state remains 6 and must not be rewritten to manufacture agreement.
Cover both direct resource changes and resource changes caused by accepted effects.

### Preserved contracts

- Execute the ordinary/lifecycle prefix once, including its causal work and required receipts, before the first exchange. An unfinished prefix cannot authorize an exchange.
- Preserve signed snapshot identity, original input fingerprints, source ownership, audit arithmetic, actor membership, dice ownership, source tiers and all unrelated source validation. A caller-supplied ledger or claimed starting balance is not sufficient evidence.
- Bind source acceptance to the actual prefix produced by the retained resource/effect owners. Do not replay ordinary commands or rewrite the original snapshot, earlier audits or accepted resource history.
- Pending completion and rejected retries retain the same owners and accepted prefix. A stale or foreign prefix, changed pinned input, or failed prefix must not authorize source commitment or canonical publication.
- Keep existing per-exchange chronology and causal closure. Preserve the fixed adapter's existing global ordering and validation behavior; cross-mode equality remains conditional on matching order.

### Acceptance criteria

1. A real signed capture accepts ordinary spend 6 -> 5 followed by an exchange audit 5 -> 2; it records each accepted mutation once and preserves the original signed balance of 6.
2. An equivalent net change caused by an accepted effect is reflected in the first exchange audit, including required receipt completion. An unresolved prefix prevents exchange acceptance; a rejected receipt neither advances the source nor repeats accepted prefix work.
3. An audit still claiming the old balance (6 -> 3 after that prefix) is rejected. Forged, stale or foreign prefix evidence cannot substitute for the actual retained owners' result.
4. Direct and triggered prefix recovery also precede exchange costs, subject to the existing resource bounds. A net-zero prefix and a turn without ordinary resource changes preserve existing behavior.
5. Missing-side continuation preserves completed prefix transitions and identities; retrying or completing evidence does not charge ordinary costs again. Late failure publishes no partial canonical state and leaves no usable accepted source/common capability.
6. Existing signed-source rejection controls and fixed-mode behavior remain valid. Focused regression evidence and independent XHigh review are required before marking this task complete; run one Fast control at the meaningful implementation checkpoint.

### Boundaries and pending decisions

This block repairs internal prefix-to-source binding. It does not enable the GM
entrypoint, add commands or receipt formats, admit wounds/current generations,
implement cold reconstruction/publication, or complete C2/D/E. Therefore this
internal block requires no new GM prompt/example; integrated runtime cutover
still requires the existing GM documentation, worked examples and source guards.
The separately approved effect-profile extension below does change the generic
GM contract and therefore requires its own synchronized documentation and examples.
The implementation plan must settle the exact ownership/evidence API and the
separation of original source checks from prefix-dependent audit checks without
weakening either. These are open design details, not permission to bypass checks.
The owner's chronological ordering decision is retained; approval of this written
scope was received on 2026-09-19. Update canonical plan.md and tasks.md through
Spec Kit planning/task phases and perform consistency analysis before code changes.

### C2-PREFIX effect capability decision — approved, 2026-09-19

Implementation inspection confirmed a pre-existing contract gap affecting
acceptance criteria2/4: canonical spiritual_action_points allows Spend/Gain,
whereas the existing periodic_damage/periodic_restore effect profiles produce
Damage/Restore. Therefore a genuine accepted effect cannot currently perform the
AP change that those criteria require. Tests on soul_integrity prove prefix
causal closure and receipt handling, but do NOT satisfy AP-changing-effect proof.
The criteria above remain unchanged and open.

Owner explicitly approved this written extension on 2026-09-19: add periodic_spend and periodic_gain
resource-effect profiles, subject to each resource's existing allowed operations,
source/owner authority, event routing, use budgets and bounded receipt rules.
Preserve existing Damage/Restore profiles and AP Spend/Gain policy. Cover genuine
AP spending/recovery effects before first exchange and synchronize GM effect
contracts/examples/manifests/source guards. This is additional gameplay/GM scope;
implementation must follow the updated plan/tasks and consistency check.
Criteria2/4 remain open until genuine AP-effect tests pass. Do not relabel
custom-resource tests as AP-effect evidence.

### C2-GEN: духовные реакции — вариант A согласован, 2026-09-19 (#1536)

Независимое ревью подтвердило противоречие между действующей спецификацией и
архитектурными примерами B/C в `docs/superpowers/plans/2026-09-08-spiritual-effect-draft-journal-design.md`.
Духовной ране разрешены восемь специальных профилей и маркер `wound_consequence`.
`event_reaction` и расширение графа реакцией разрешены физическим ранам, но
отклоняются для всех определений духовной раны, включая недостигаемые и дочерние.
Поэтому пример «новая духовная рана → реакция от её корня → дочерний эффект →
повторная травма» сейчас нельзя построить из разрешённых игровых возможностей.
Ранее одобренные `periodic_spend`/`periodic_gain` относятся к общим эффектам;
то решение прямо сохраняет списки разрешённых последствий ран.

Владелец проекта явно выбрал рекомендованный вариант A. Игровые ограничения
сохраняются; критерии ниже уточняются по разрешённым областям применения.
GEN остаётся открытым до реализации и проверки, включая восемь существующих
механических потребителей, повторные травмы и единую публикацию.

**Принятый вариант A — сохранить текущие возможности духовных ран.**
Сохраняются восемь профилей и маркер; духовные раны не создают дочерних эффектов
через реакции. По решению владельца архитектурные примеры и приёмочная
матрица разделяются: духовная цепочка проверяет реальную вставку, следующий обмен,
повторные травмы и завершение; реакции, замены, дочерние эффекты и привязанные
списания проверяются на разрешённых физических ранах и общих эффектах в том же
движке. Запрещённые духовные графы остаются отрицательными тестами. Это явное
уточнение критериев, а не заявление о выполнении прежнего невозможного сценария.
Проверки привязки нового корня раны, его источника, потомков и прекращения требуют
реальной физической раны, вставленной через тот же механизм черновика. Общие эффекты
дополняют эти проверки и не заменяют их обычным заранее существовавшим эффектом.

**Отклонённый вариант B — расширить духовные раны ограниченными реакциями.**
Следующее описание сохраняется только как запись непринятой альтернативы и
не разрешает реализацию:
Для тяжести III–IV разрешается `event_reaction` только с `resultKind=apply_definition`,
с одним достижимым дочерним определением без вложенных расширений. Дочерние
механические компоненты используют только существующие восемь духовных профилей;
маркер остаётся без стоимости. Реакция занимает один слот, каждый компонент
дочернего последствия — ещё один; применяются исходные ограничения тяжести и
безопасных действий. Используется существующая ограниченная семантика
`maxExpansion=2`, точного источника/владельца и допустимой замены в одной области
владения. Общие ресурсные профили автоматически в духовные раны не добавляются.
Приёмка дополнительно требует реальной духовной цепочки B/C, проверок бюджета,
запрета рекурсии и чужих источников, полного холодного восстановления, а также
синхронного обновления GM-документации, примера и защитных тестов.

Принятый вариант A отражается в `plan.md`, `tasks.md` и архитектурной матрице
с сохранением всех тринадцати сценариев в их законных областях применения.
Перед зависимой реализацией проводится проверка согласованности Spec Kit.

## Уточнение стоимости духовных последствий — утверждено 2026-09-26 (#1536)

Статус: **точная редакция утверждена пользователем 2026-09-26; зависимая реализация разрешена после согласования plan/tasks/contracts и проверки согласованности**.
Это уточняет два незавершённых случая существующего `spiritual_action_cost_burden`,
не добавляет профиль или духовные реакции. Обычные платные операции уже имеют
формулу; для следующих случаев код и таблица профиля пока не задают полное правило.

Утверждённые правила:

1. `recover_spiritual_power`: сначала оплачивается штраф раны, затем восстанавливается
   сила по действующим правилам результата/противодействия и ограничения максимума.
   `actionCostAudit.before` — ресурс до оплаты, `after` — после собственного
   восстановления действия. Последующие реакции остаются отдельными переходами;
   `after` не подменяет окончательный ledger после их исполнения.
   При нехватке ресурса для штрафа действие не принимается. Например, при запасе 1,
   штрафе 1 и успешном восстановлении 3 результат равен 3; при запасе 0 действие
   недоступно. Прежние величины восстановления 3/2/0 и 0..1 под противодействием
   применяются после оплаты; неудачная попытка также оплачивается. Например,
   запас 1, штраф 1, восстановление 0 дают итог 0: итоговая разница может быть
   отрицательной. Ресурсный журнал содержит настоящее Spend, затем Gain, а не только
   итоговую разницу; реакции и остаток применений проходят через прежнего владельца.
   Без штрафа сохраняется прежнее восстановление.
2. `force_incarnation`: сохраняется отсутствие базовой платы; при применимом штрафе
   раны списывается только этот штраф. Для такого действия нужен `actionCostAudit`
   с baseCost=0, minCost=0, artTier=0, effectiveCost=сумме применимых штрафов.
   artTier=0 здесь — новая конвенция стоимостного audit, не уровень искусства актёра.
   Без применимого штрафа прежний путь без платы и без обязательного audit сохраняется.
   Недостаток ресурса отклоняет действие. Это не меняет допуск/контроль воплощения.

Приёмка: обе стороны конфликта, отсутствие штрафа, правильный и
пропущенный штраф, нехватка ресурса, восстановление у максимума и под противодействием,
настоящий упорядоченный ресурсный журнал, неизменность закрытого префикса, повтор
без двойной оплаты. Синхронизация GM-контракта и рабочих примеров обязательна перед
включением возможностей в общий ход. До завершения реализации оба случая с применимым
штрафом остаются явно незавершёнными и отклоняются, остальные задачи GEN продолжаются.

## Восстановление исходного духовного хода — предложение C2-R1, 2026-09-22 (#1536)

Статус: **утверждено пользователем 2026-09-22; реализация разрешена**.
Относится к T081-C2-PENDING-RECONSTRUCTION и последующему общему опубликованию C4.
Завершённый C1 сохраняет статус проверенного ядра текущего закрытого формата.

### Причина уточнения

Исходный snapshot содержит состояние до хода, а текущие candidateAfterImages могут
меняться после решений о ранах. Вместе они не сохраняют полный первоначальный
распределённый черновик GM. Кроме того, обычные владельцы эффектов и ресурсов
выдают случайные идентификаторы и время создания запросов. Новый процесс не
воспроизведёт их одним повторным исполнением. Проверка вычислимого хеша пакета
эти пробелы не закрывает.

### Предлагаемое поведение и границы

1. Клиент сохраняет отдельную закрытую контрольную запись незавершённого захвата:
   `game_state/control/spiritual_wound_capture_checkpoint.json`.
   Она привязана к точным исходным session/request/turn/snapshot и исходному
   инвентарю путей. Запись содержит неизменные первоначальные входные файлы
   с точными байтами и признаком отсутствия; последовательность принятых
   продолжений с их исходными входами и решениями; журнал фактически выданных
   случайных идентификаторов и времён; номер последнего успешно сохранённого
   продвижения и ожидаемый fingerprint соответствующего pending-пакета.
   Это служебное состояние клиента, недоступное для записи через GM-команды.
2. Исходный snapshot никогда не заменяется, не дополняется задним числом и не
   перепечатывается ради легализации черновика. Контрольная запись сохраняет
   происхождение входов внутри существующей переносимой границы доверия
   клиентских файлов. Хеши обнаруживают расхождение, но не являются подписью.
   Защита от согласованной подмены всех клиентских файлов доверия сторонним
   редактором не заявляется; новый сервис ключей или шифрования не добавляется.
3. Идентификаторы остаются случайными и непрозрачными: сохраняются реально
   выданные значения, без опубликованного seed и без вывода ID из GM-входов.
   Запись связывает выдачу с типом, владельцем, причинной операцией и порядком.
   При восстановлении владелец обязан запросить те же выдачи на тех же шагах:
   пропуск, лишняя выдача, иной тип или причинная операция отклоняют восстановление.
   Существующие детерминированные идентификаторы ран не меняются.
   Время создания исходного ресурсного запроса также повторяется точно.
4. Клиент начинает с прежнего snapshot, заново проверяет исходный черновик
   обычными владельцами и исполняет сохранённые продолжения в исходном порядке.
   Сохранённые source/receipt/image/fingerprint сами по себе не дают разрешения
   на расход ресурса, создание раны или принятие хода. Действительность каждого
   перехода повторно устанавливают реальные владельцы ресурсов, эффектов и ран.
   Неизменными остаются первоначальные действия, актёры, координаты кубиков и
   независимые корректные части черновика; правки зависимых частей проходят
   прежние проверки продолжения.
5. Контрольная запись определяет последнее сохранённое продвижение. Pending-пакет
   остаётся производным состоянием существующего закрытого формата C1.
   Оба файла сверяются до выдачи контекста GM или разрешения продолжить ход.
   Незавершённая запись, устаревший либо отсутствующий производный пакет после
   сбоя не позволяют принять более поздние решения. Восстановить пакет можно
   только повторным исполнением последнего сохранённого продвижения.
   Отсутствующая или повреждённая контрольная запись при существующем pending
   останавливает продолжение; она не создаётся задним числом из pending.
6. Сохранение промежуточного продвижения не публикует канонические эффекты,
   раны, оплату или уведомления. Под действующей блокировкой записи клиент
   сначала полностью проверяет следующее ограниченное продвижение в памяти,
   затем атомарно заменяет контрольную запись и проверяет её чтением.
   Это единственная точка сохранения продвижения. После неё клиент записывает
   и проверяет производный pending. Ошибка этой второй записи требует
   восстановления проекции уже сохранённого продвижения, а не повторной
   выдачи ID или исполнения нового решения. Если результат первой записи
   неизвестен, точное чтение прежней записи означает отсутствие продвижения,
   точное чтение новой — сохранённое продвижение; иной результат останавливает
   работу. До точки сохранения неудачная попытка не добавляет принятый журнал
   и не меняет исходные решения. Завершение и потребление обеих служебных
   записей входят в единственный общий план, включая read-back и rollback.
   Состояния несовпадающих поколений проверяются и при перезапуске после
   прерванного сохранения, и при неудачном общем опубликовании.
7. Новый путь получает те же защиты владения, snapshot/rollback-покрытие и
   исключение из пользовательского статуса и GM repair-resubmission, что и
   существующие приватные корни C1. Руководство GM, пример продолжения,
   manifest и защитные тесты обновляются в C2. Новых команд игрока, правил
   стоимости или расширения возможностей эффектов это уточнение не вводит.
   Активное содержимое самой контрольной записи и pending не включается
   рекурсивно в candidateAfterImages. Их исходные before-images и членство
   в зарегистрированном инвентаре сохраняются для точного rollback.

### Приёмка

- Холодный запуск до решения и после принятого решения восстанавливает точные
  исходные входы, ID, время запросов, источники, ресурсы, эффекты и кубики.
- Подмена только pending, его источников/решений/образов с пересчитанными хешами
  не заменяет сохранённое исходное продвижение.
- Неправильный порядок, тип или причинная привязка выдач, лишняя либо
  недостающая выдача и изменённое время отклоняются до принятия хода.
- Нельзя изменить ещё не выполненное первоначальное действие, его актёра,
  координату кубика или независимого корректного соседа под видом продолжения.
- Сбой между сохранениями, исчезновение производного pending и повторный
  запуск восстанавливают только последнее сохранённое продвижение; нет
  двойной оплаты, новых ID для прежнего результата или повторного уведомления.
- Изначальное отсутствие обоих служебных файлов учитывается точно. Отказ
  до сохранения контрольной записи оставляет прежнюю запись/отсутствие;
  отказ pending после её сохранения удерживает новую контрольную запись
  и требует восстановления pending. Неудачное общее опубликование выполняет
  rollback к точным исходным байтам/отсутствию по прежнему контракту.
- Реальные случайные выдачи эффектов, переходов, combatant/member, ресурсных
  определений/seal и запросов покрыты; новый seed не используется.
- Независимое XHigh-ревью, owning Focused, связанные интеграционные проверки,
  документационные проверки, FullValidation и Fast остаются обязательными.

Перед зависимой реализацией требуется уточнить закрытую схему новой записи,
точки выдачи ID/времени и протокол сохранения в data-model/contracts/plan/tasks,
затем выполнить проверку согласованности Spec Kit. Этот раздел не утверждает
реализацию восстановления и не меняет пока действующий формат pending.

## Доставка ответа GM в том же духовном ходе — C4-GM-TRANSPORT, 2026-09-26 (#1536)

Статус: **редакция 2 утверждена пользователем 2026-09-26; реализация разрешена**.
Редакция 2 добавляет сохранение выбранного решения до зависимого исправления;
предыдущее предложение без этого правила заменено после независимого ревью.
Уточняет оставленный открытым транспортный выбор в плане C4. Цель: GM получает
только текущее предложение о ране и отвечает в рамках исходного хода; клиент
применяет решение один раз, затем продолжает следующий обмен или общее
опубликование. Уже утверждённые игровые правила, последовательность обменов,
границы C2/C3/C4 и единственный канонический publisher не меняются.

### Выбранный путь и границы

Предлагается расширить существующие `validation_repair_request.json` и
`validation_repair_ready.json` в `game_state/control/` необязательным полем
`spiritualWoundContinuation`. Это отдельный режим продолжения в существующем
транспорте, а не объявление корректного предложения ошибкой. Первичное предложение
не требует выдуманной ошибки; `errors` может быть пустым только в этом режиме.
`fullTurnResubmissionRequired` равен `false`; перед dispatch не восстанавливается
baseline и не запрашивается повтор всего хода. Настоящие ошибки по-прежнему
получают ограниченную диагностику и исправление в пределах текущей фазы.

Альтернативы: отдельный control-файл добавил бы ещё одну границу сохранения и
очистки; перенос решения в narrative смешал бы текст сцены с командами и изменил
бы его закрытую схему. Расширение существующего request/ready сохраняет общий
путь console/browser и оба уже поддерживаемых способа работы GM — файлы и worker.
Новые HTTP endpoints, команды игрока и канонические корни не входят в предложение.

### Закрытые конверты запроса и ответа

Request envelope содержит ровно следующие поля:

| Поле | Контракт |
|------|----------|
| `schemaVersion` | Целое `1`. |
| `continuationId` | Непустая непрозрачная клиентская корреляция текущего проверенного продвижения и фазы. Не полномочие и не замена проверки C2. |
| `phase` | Только `decision` или `dependent_draft`. |
| `offer` | В `decision` — точная безопасная проекция `SpiritualC2PrivateOffer`; в `dependent_draft` — `null`. |
| `sceneTextSource` | Ровно `{ "path": "output/narrative_response.json", "field": "response" }`. |
| `dependentDraftFields` | Массив уникальных объектов `{ "path": string, "jsonPointer": string }`, выданных клиентом; в `decision` пуст. |

`offer` содержит только `opportunityRef`, `minimumSeverityRank`,
`requiredSeverityRank` (целое или `null`), `maximumSeverityRank`, `target`,
`cause`, `allowedLocationKinds`, `allowedDecisions` из текущего C2-адаптера.
Границы ранга и словари не назначает GM. Приватные source/receipt/owner,
allocation journal, checkpoint и pending images в предложение не включаются.

Response envelope содержит ровно `schemaVersion`, `continuationId` и
`woundDecisions`. Например, ответ на необязательное предложение:

```json
{
  "spiritualWoundContinuation": {
    "schemaVersion": 1,
    "continuationId": "swc_current_client_reference",
    "woundDecisions": [
      { "opportunityRef": "current_client_opportunity", "decision": "none" }
    ]
  }
}
```

Пример показывает добавляемое поле; обычные обязательные metadata ready остаются.
Значения ссылок копируются из реального request, а не из примера. В `decision`
массив содержит ровно одно решение текущего offer; `materialize` использует
существующую закрытую proposal-схему `WoundResponseInputComposer`. В
`dependent_draft` массив строго пуст: прежнее выбранное решение не заменяется.
Вне этого режима конверт ответа запрещён; отсутствие конверта при активном
продолжении не означает `none`. Неизвестные поля и повторяющиеся JSON-ключи
в обоих конвертах отклоняются, типы и регистр имён проверяются точно.

Текст остаётся только в `output/narrative_response.json.response`. Разрешено
исправлять `response` и обычный `timestamp` по существующей схеме, сохраняя
остальные корректные output-соседи; решения внутрь narrative не помещаются.
Клиент читает согласованный текст и ответ под своей блокировкой, передаёт
решение в C2 и проверяет обязательное описание каждой материализованной раны.
Публикация и показ игроку ждут завершения всего исходного хода.

### Сохранение решения и допустимое зависимое исправление

`sessionId`, `requestId`, `turnNumber`, session generation, исходный signed
snapshot, действия, актёры, координаты кубиков и progression control сохраняются.
После ожидания GM клиент повторно открывает текущий C2 checkpoint под lease и
сверяет фазу, текущее предложение и разрешённые поля с выданным request.
`continuationId` связывается с точным проверенным checkpoint, фазой и allowlist;
ответ от другого продвижения, фазы, запроса или session не применяется.
Для зависимого исправления эта привязка включает точный `pendingSubmission`,
описанный ниже: одинаковый committed cursor не разрешает заменить выбор.
После холодного запуска допускается перевыпуск корреляции только после
восстановления C2 реальными владельцами; старый ready не становится новым решением.
Request/ready сами по себе не восстанавливают отсутствующую приватную authority.

Если новая рана меняет результат следующего обмена, клиент запрашивает
`dependent_draft` с точными JSON pointers, вычисленными существующей проверкой
зависимостей C2. Это разрешение предложить исправление, а не обойти проверки
владельцев. Сохраняются выбранное решение и точные клиентские wound-command bytes;
после ограниченного исправления повторяется сохранение C2. При невозможности
доказать допустимый набор полей клиент блокирует продолжение вместо расширения
разрешения до всего файла. Нет разрешения менять первоначальные действия,
актёров, кубики, закрытые обмены, порядок либо независимых корректных соседей.

Перед запросом `dependent_draft` клиент атомарно сохраняет и проверяет чтением
закрытый необязательный `pendingSubmission` внутри уже существующего
`spiritual_wound_capture_checkpoint.json`. Новый файл не вводится. Эта запись
отдельна от `advances` и `committedAdvance`: прежние сохранённые обмены, cursor
и производный pending остаются на последнем принятом продвижении. Запись
связывает исходные session/request/turn/snapshot, предыдущее продвижение и
координату текущего источника с точными составленными клиентом command bytes
(включая привязку исходного текста), а также с реально выданным ID/time-prefix
до окончания выбранной вставки раны. Она не сохраняет как принятые результаты
ещё невалидного следующего обмена. Физический command, публичный request/ready
или вычислимый хеш не заменяют эту запись и не дают полномочий на исполнение.

Если гарантия уже удовлетворена, запись вместо GM-решения связывает реальный
клиентский исход `guarantee_satisfied` и отсутствие нового command с тем же
источником и предыдущим продвижением. Она не придумывает новое предложение GM.
После холодного запуска реальные владельцы повторяют committed layers, заново
проверяют сохранённый выбор либо гарантию и строго воспроизводят сохранённый
ID/time-prefix. Только после этого определяется зависимый участок и allowlist.
Несовпадение команды, происхождения или журнала блокирует продолжение; выбор не
заменяется новым ответом и не восстанавливается задним числом из command-файла.

Успешное исправление записывает обычное продвижение C2 и очищает
`pendingSubmission` одной атомарной заменой checkpoint с проверкой чтением,
после чего обновляется производный pending по C2-R1. Сбой до подтверждённой
замены сохраняет прежний точный выбранный вариант для повторения; неоднозначное
чтение блокирует работу. Перезапуск между этими записями не создаёт второй выбор,
новые ID для уже сохранённой вставки или повторную оплату. Отсутствующая запись
при неподтверждённом command не разрешает считать его прежним выбранным решением.

GM не пишет checkpoint, spiritual pending, wound commands, receipts, signed
snapshot или журналы клиентских ID. Изменения вне разрешённых полей отклоняются
до сохранения продвижения. Ошибка до commit не расходует ресурс и не создаёт
каноническую рану. Сбой pending после checkpoint восстанавливается по C2-R1;
неудачная общая публикация сохраняет существующий точный rollback.
Повтор уже сохранённого ответа не выполняет его заново: клиент восстанавливает
текущее продвижение и выдаёт актуальный следующий шаг. Чужая session не очищается.

### Два способа доставки и приёмка

Файловый GM передаёт response envelope через существующий helper завершения
repair. Worker получает тот же request envelope в task packet и возвращает
тот же response envelope в proposal. Apply gate проверяет корреляцию, фазу,
решение и допустимые изменения до их применения; проверенный ответ переносится
в обычный ready. Перед записью ready клиент ещё раз под lease проверяет точную
корреляцию продолжения, фазу и текущее продвижение, а не только session generation:
задержавшийся worker не перезаписывает ответ более нового продолжения той же session.
Worker не записывает ready или приватные C2-файлы через
`changedFiles`. Завершённый proposal без `changedFiles` разрешён только для
валидного ответа текущего `decision`, если текст уже подходит; это не ослабляет
обычные worker-контракты. `draftText` и `note` не служат скрытым транспортом.

Приёмка: оба способа проходят реальный исходный signed ход с `none` и
`materialize`, двумя последовательными предложениями и зависимым исправлением;
сохраняются исходные request/dice/progression и независимые части черновика.
Пропущенный/лишний/чужой/stale ответ, подмена фазы, неизвестный ключ и запрещённая
правка не продвигают C2. Подмена client command при сохранённом
`pendingSubmission`, его потеря, сбой записи и холодный повтор обоих вариантов
(`materialize` и `guarantee_satisfied`) проверяются отдельно; неизменный вариант
должен успешно продолжаться. Проверяется гонка старого worker после apply перед
ready нового предложения. Перезапуск и повтор доставки не дублируют оплату, ID,
рану или уведомление. Проверяются отмена/смена session и отсутствие удерживаемой
canonical lease во время ожидания GM. Приёмка C4 дополнительно требует общего
опубликования, конечного narrative, уведомлений и cold-after-success.
Обычные repair, worker и Mortal пути сохраняют прежние ограничения.

Перед реализацией после утверждения: точная схема и dispatch описываются в
contracts/plan/tasks текущей feature с проверкой согласованности Spec Kit.
GM matrix/guide, helper/daemon/worker instructions, пример, manifest и guards
обновляются вместе с кодом. Owning Focused, связанные lifecycle/worker проверки,
XML, Fast, условный FullValidation и независимый Astra XHigh обязательны.

## Уточнение POSITION-DECISION — штраф стартовой позиции, редакция 1

Статус: **редакция 1 явно утверждена владельцем 2026-09-27**.
Задача: #1536 / T081-D-POSITION-DECISION. Ответ владельца: «Утвердить редакцию 1 (рекомендуется)».
Зависимая реализация следует после согласования plan/tasks/contracts и проверки их непротиворечивости.

Цель — определить уже предусмотренный `spiritual_position_burden` без повторного
автоматического ухудшения сохранённой позиции от одной и той же раны. Величина
компонента остаётся прежней: один шаг при I–II, не более двух при III–IV.
Предлагается штраф к эффективной стартовой позиции конкретного обмена.

### Предлагаемое правило

Каноническая позиция перед обменом имеет существующий ранг:
`opposition_dominant = -2`, `opposition_advantaged = -1`, `contested = 0`,
`player_advantaged = +1`, `player_dominant = +2`.
Клиент вычисляет `effective = clamp(canonicalBefore + oppositionBurden - playerBurden, -2, +2)`.

1. Для каждой стороны складываются величины действующих компонентов
   `spiritual_position_burden` точного действующего участника, операция которых
   совпадает с его операцией в этом обмене. Каждый принятый компонент учитывается
   один раз. Компоненты других участников, операций и уже завершившиеся эффекты
   не участвуют; неоднозначное владение не заменяется нулевым штрафом.
2. Сначала складываются все применимые величины обеих сторон, затем ограничивается
   итоговый ранг диапазоном -2..+2. Суммы сторон отдельно не обрезаются.
   Равные штрафы сторон компенсируются в общей позиции. Дубли одного компонента
   не становятся дополнительным штрафом.
3. Эффективная позиция задаёт существующий позиционный модификатор броска:
   при ненулевом ранге преимущество получает соответствующая сторона в размере
   `2 * abs(effective)`, при нуле позиционного модификатора нет. Поле `position`
   существующего `conflict_position` модификатора обозначает эту эффективную
   позицию; новые поля, канонические файлы и полномочия GM не вводятся.
4. Эффективная позиция применяется также к требованиям, зависящим от стартовой
   позиции текущего действия, включая позиционные предпосылки обычных и усиленных
   оков. Остальные способы законно выполнить эти предпосылки сохраняются.
5. `exchange.before.conflictPosition` остаётся точным каноническим состоянием
   перед обменом; `after.conflictPosition` — обычным результатом действия.
   Штраф не переписывает ни одно из этих значений автоматически. Последующий
   обмен вычисляет эффективную позицию от своего принятого канонического `before`,
   без повторного вычитания из уже скорректированного временного значения.
6. Успешный или частично успешный манёвр по-прежнему должен реально изменить
   каноническую позицию `before -> after`. Нельзя принять одинаковые значения
   под предлогом, что штраф поглотил результат. Существующие ограничения манёвра
   на strain, control и улучшение позиции под активным контролем сохраняются.
7. Прежняя действующая рана влияет с первого подходящего обмена. Вставленная
   после обмена рана влияет только на следующие подходящие обмены в порядке
   принятого исходного хода. Закрытый обмен не пересчитывается. Холодное
   восстановление и последующая проверка используют его исходный принятый
   контекст, а не позднейший набор ран. Лечение или окончание эффекта снимает
   только будущий применимый штраф и само по себе не повышает каноническую позицию.

### Игровые примеры и приёмка

- У игрока рана I со штрафом `pressure: 1`; канонически он имеет преимущество
  `+1`. Для давления стартовая позиция становится эффективно `0`, поэтому
  позиционного бонуса нет. Если действие не меняет позицию, канонически остаётся
  `+1`. Следующее давление снова начинается эффективно с `0`, а не с `-1`.
- Противник после предыдущего обмена получил штраф `pressure: 2`. Игрок выполняет
  манёвр со своим штрафом `maneuver: 1`, противник — давление; каноническая позиция
  `0`. Эффективно получается `0 + 2 - 1 = +1`, что даёт игроку существующий бонус
  `+2`. Успешный манёвр может изменить каноническую позицию с `0` на `+1`.
  Новая рана противника не меняет результат предыдущего обмена.

Приёмка требует обе стороны, совпадение/несовпадение операции и точного актёра,
суммирование нескольких ран, равные встречные штрафы, насыщение обоих краёв,
несколько последовательных действий без накопительного ухудшения, обычные и
усиленные оковы, сохранение требований манёвра, вставку/окончание эффекта между
обменами, зависимое исправление следующего черновика, холодное восстановление и
отклонение подделанного модификатора. Канонические данные, расход ресурса,
закрытые исходные кубики и уже принятые обмены сохраняются. GM-руководства,
пример, manifest и проверки обновляются вместе с реализацией после утверждения.

Альтернатива — постоянное ухудшение либо ограничение канонической позиции —
не входит в эту редакцию: она потребовала бы других правил повторного применения,
лечения и успешного манёвра при насыщении. Остальные неуточнённые профили T081-D,
лечение T107 и общий объём #1536 этим решением не закрываются.

## DEPENDENT-FRONTIER-DECISION — редакция 1, 2026-09-28

**Статус: редакция 1 явно утверждена пользователем 2026-09-28.**
Задача: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
T081-D-POSITION-DEPENDENCY. Это уточнение протокола GM после сохранённого выбора
раны; POSITION-DECISION, игровые формулы и правила исходов не меняются.
Разрешена зависимая реализация этой редакции по плану и задачам текущей feature.

### Причина и выбранное решение

Пересчёт позиции может потребовать нового описания критического результата в
среднем обмене. Клиент знает обязательные числовые поля, но не может сочинить
два текста GM. Поэтому разрешённые исправления следующего обмена иногда можно
доказать только после получения и проверки этого описания. Один заранее
вычисленный запрос на все исправления не покрывает такой случай.

Предлагается последовательность точных запросов: GM завершает выданный набор A,
затем клиент при необходимости выдаёт новый набор B. Отклонение всего такого
хода оставляет согласованный сценарий невыполненным; выдумывание текста или
заранее разрешённые будущие изменения нарушают сохранённые контракты. Эти две
альтернативы не выбираются.

### Обязательный контракт

1. Запрос A разрешает только указанные в нём исправления. Клиент восстанавливает
   его полный конверт из исходной приватной пары и доказанного состояния обхода;
   идентификатор корреляции и полный конверт должны совпасть. Пути из ответа GM
   или сохранённого транспортного файла сами по себе не дают прав на изменение.
2. GM исправляет A и отвечает существующим конвертом `dependent_draft` с пустым
   `woundDecisions`. Весь фактический кандидат сначала проверяется по политике A.
   Добавлять изменения будущего B в ответ A запрещено, даже если позже они могли
   бы стать допустимыми. Сохраняются закрытые обмены, подписанные кубики,
   независимые модификаторы, выбранная рана и её идентичность.
3. Дальнейший обход разрешён только после полного точного исправления A и
   обычной проверки фактического обмена. Для нового критического результата
   обязательны точные вычисленные поля и оба непустых текста GM. Неполный либо
   недопустимый ответ не открывает следующий набор исправлений. Клиент не
   подставляет выдуманные тексты или последствия действия.
4. `Ready`, связанный с A, означает завершение именно A. Если после этого
   доказаны новые поддерживаемые зависимости, клиент выдаёт B с собственным
   конвертом и корреляцией и ожидает отдельный ответ. A не считается ответом на B.
   Если новых зависимостей нет и черновик полностью допустим, продолжается
   существующий путь завершения. Неподдерживаемая зависимость не превращается
   в успешное завершение A или разрешение произвольных правок.
5. Между A и B клиент сохраняет исходный выбор раны, приватное состояние и
   исходные данные хода; канонический результат ещё не публикуется. Удаляются
   только точно сопоставленные транспортные записи A. Устаревший ответ/Ready A
   после выдачи B не принимается. Замена исходной приватной пары, поколения
   сессии или владельца отменяет допустимость прежнего ответа.
6. Файловый GM и worker имеют одинаковые правила. Worker проверяет разрешения A
   до записи, фактический результат после записи и владение перед Ready.
   Проверенное завершение A может открыть B; недопустимые изменения откатываются
   существующим механизмом. Сохраняются резервация worker, точное сравнение
   запроса, контроль исходного хода и условная запись/очистка Ready.
7. После перезапуска существующий запрос A и его ответ сверяются до автоматического
   продолжения сохранённого черновика. Прогресс восстанавливается из исходной
   приватной пары и допустимых фактических исправлений. Наличие транспортного
   файла не заменяет эту проверку; непрошенные будущие правки не принимаются
   только потому, что после перезапуска весь черновик выглядит допустимым.
8. Новых JSON-полей, игроковых команд или хранимых источников полномочий не
   добавляется. Формула корреляции сохраняется. Различение отказа, полного
   разрешения и перехода к следующему набору — внутренний результат клиента;
   обычная строгая проверка полностью исправленного черновика не ослабляется.

### Пример и критерии приёмки

В первом обмене игрок получает рану со штрафом давления 1. Во втором обмене
исходные кубики 20/18 и независимый бонус игрока +1 дают 21/18; штраф позиции
даёт противнику +2, итог становится 21/20. Полоса остаётся `player_success`,
но требуется критическое описание. Запрос A содержит только точные исправления
второго обмена. После допустимого описания GM клиент может доказать запрос B
для третьего обмена: исходные 9/8 становятся 9/10. В этом примере результаты
`no_effect` и неизменный strain допустимы; новых последствий клиент не сочиняет.

- Действительные файловый и worker пути проходят A → B → завершение с тем же
  выбором раны и однократным логическим применением ресурса, раны и результата хода.
- Холодное восстановление проверяется после записи исправлений A, после Ready A
  и после выдачи B. При сбое публикации сохраняется прежняя гарантия: все исходные
  данные либо весь принятый результат, без заявления о физическом exactly-once.
- Ответ A с будущими правками B, пустым критическим текстом, неверным числовым
  полем, изменённым кубиком или независимым модификатором отклоняется. Проверяются
  сохранность приватных/канонических данных, worker rollback, устаревший Ready A
  и замена приватной пары перед Ready.
- Существующие исправления расхода и полностью вычислимые позиционные цепочки
  сохраняют прежнюю стабильную корреляцию и завершение одним ответом.
- Обновляются GM-руководства, реальный пример A → B, manifest и source guards;
  выполняются адресные проверки, Fast, требуемый FullValidation и независимое
  ревью Astra XHigh. T081-D-POSITION-RESULT-CLOSURE остаётся отдельной открытой
  задачей; эта редакция не разрешает новые зависимости последствий операции.

## DEPENDENT-FRONTIER-DECISION — редакция 2, 2026-09-28

**Статус: редакция 2 явно утверждена пользователем 2026-09-28.**
Задача: [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
T081-D-POSITION-DEPENDENCY-FRONTIER. Редакция 1 утверждена; эта редакция уточняет
только приватное сохранение прогресса между запросами A и B и заменяет её
ограничения в пунктах 5, 7 и 8 в явно указанной ниже части. Расширение приватного
checkpoint разрешено по обновлённому плану и задачам. Остальные правила редакции 1,
включая отдельные ответы, сохранённую рану и запрет будущих правок, сохраняются.

### Почему нужно уточнение

Без сохранённого принятого изображения A два состояния после перезапуска
неразличимы: клиент действительно принял A и открыл B либо в черновик сразу
внесли A+B и подменили публичный запрос на B. Кроме того, другое допустимое
критическое описание A даёт ту же арифметику и тот же перечень полей. Простая
повторная проверка правильности не доказывает неизменность уже принятого текста.

Существующий `pendingSubmission` разрешает только первоначальную запись выбора
или её точное повторение. Обычный `advances` требует нового решения и полного
продолжения; его нельзя использовать как скрытый частичный переход для A.
Поэтому предлагается минимальное расширение существующего приватного checkpoint.

### Приватная запись принятого исправления

В `checkpoint.pendingSubmission` разрешается необязательное закрытое поле
`dependentDraftProgress`: упорядоченный массив записей со следующими полями:

- `ordinal`: последовательный номер, начиная с 1, без пропусков и повторов;
- `acceptedContinuationId`: точная корреляция завершённого запроса A;
- `dependentDraftFields`: полный точный перечень полей этого запроса как
  проверочное свидетельство, а не источник разрешений;
- `inputChanges`: точные принятые изображения изменённых разрешённых файлов в
  существующем формате `path`, `existed`, `contentBase64`, `contentFingerprint`.

Хранятся сами принятые байты, включая оба текста критического результата, а не
только хеш текущего файла. Удаление файла не разрешается. Все вложенные формы
закрытые; неизвестные поля, дубли, подмена путей и неверные отпечатки отклоняются.
Число записей ограничено числом исходных оставшихся обменов: запись требует
реального прохождения хотя бы одного нового обмена до следующей поддерживаемой
зависимости. Пустой повтор либо прежняя ошибка не создаёт запись.
До первой записи поле отсутствует; пустой массив не является альтернативной
формой. Приватный checkpoint записывается в единственной канонической форме
клиента: восстановление префикса до каждой записи должно давать точные байты
предыдущего checkpoint и соответствующую корреляцию, а не только равный JSON.

Эта запись принадлежит клиенту и не может быть authored/исправлена GM. Сама по
себе она не даёт полномочий на исполнение или публикацию: при каждом восстановлении
реальные исходные владельцы повторно выводят разрешения и проверяют сохранённые
изображения. Новый файл состояния или отдельное хранилище не добавляется.

### Переход A → B

1. Под существующей блокировкой клиент сопоставляет полный текущий запрос A,
   его Ready и исходный ход. Проверяет весь фактический кандидат только по A,
   полный исправленный обмен обычным валидатором и наличие доказанного следующего
   поддерживаемого набора исправлений. A+B одним ответом остаётся запрещённым.
2. Клиент повторно сверяет фактические изображения и добавляет одну запись
   `dependentDraftProgress`, сохраняя предыдущие записи побайтно. Выбранное
   решение, исходная команда, выделенные идентификаторы/время, обычные `advances`,
   `committedAdvance` и приватный pending packet не меняются.
3. Checkpoint атомарно заменяется и проверяется чтением по существующему правилу
   «старое / новое / неоднозначное изображение». Только подтверждённая новая
   запись устойчиво открывает следующий набор B. Неоднозначность блокирует
   дальнейшее исполнение. Повторное восстановление не добавляет ту же запись
   второй раз, не применяет рану и не списывает ресурс повторно.
4. После подтверждения клиент освобождает прежних владельцев и восстанавливает
   их из обновлённого checkpoint. Удаляет только точно сопоставленные устаревшие
   транспортные записи A и публикует запрос B. Формула корреляции сохраняется:
   новые байты checkpoint естественно связывают B с принятым A.
5. Запись checkpoint открывает B до публикации публичного запроса. Если между
   этими действиями произошёл сбой, восстановление выводит B из приватного
   прогресса. Старые A request/Ready — только кандидаты на точную очистку:
   проверяются исходные координаты хода, записанная корреляция и восстановленный
   полный конверт A. Несовпадающий транспорт сохраняется, продолжение блокируется.
   Старый A ни при каких обстоятельствах не считается ответом на B.
6. Worker проверяет A до записи, после записи и перед Ready, но сам не меняет
   этот журнал. Его дописывает GameEngine при приёме точного Ready, одинаково
   для файлового и worker маршрутов. Проверки владения и rollback сохраняются.
7. После полного исправления хода обычное продвижение сохраняет совокупное
   принятое изображение, включая все предыдущие A, прежде чем очищает
   `pendingSubmission` вместе с журналом. Холодное восстановление после этой
   границы также не теряет ранее принятые тексты и изменения.

### Холодное восстановление и приёмка

Восстановление сначала воспроизводит исходный принятый префикс и сохранённый
выбор раны. Затем последовательно проверяет каждую запись прогресса: независимо
выводит политику и полный запрос, сравнивает записанные поля и корреляцию,
проверяет всё сохранённое изменение только по этой политике, исполняет обычную
проверку исправленного обмена и использует принятое изображение как исходное
для следующей записи. Текущие публичные файлы не заменяют предыдущие изображения.

Текущий черновик рассматривается только как кандидат для первого ещё не принятого
набора исправлений. Без записи A актуальным остаётся A; поддельный публичный B
не позволяет его обойти. После открытия B ранее принятые поля A, включая точные
критические тексты, неизменны. Обычные разрешённые поля текущего ответа сохраняют
свои прежние правила, в том числе текст narrative response и timestamp.

К приёмке редакции 1 добавляются: поддельный B без записи A; подмена принятого
текста A в текущем черновике после открытия B; недопустимая вставка, перестановка
или замена записей журнала, нарушающая его форму, цепочку либо исходный контракт;
расширенные записанные разрешения; A+B в одном изображении; сбой и неоднозначное
чтение checkpoint; перезапуск после принятия A, но до очистки A/публикации B;
устаревший Ready A после открытия B; замена checkpoint перед worker Ready.
Проверяются точное сохранение решения/команды/идентификаторов и отсутствие
повторной записи, ресурса, раны или публикации при восстановлении.

Граница проверки соответствует существующим приватным данным клиента: GM и worker
не получают права записи журнала; при живом переходе замена checkpoint выявляется
точным сравнением. Холодное восстановление проверяет форму, отпечатки, цепочку и
реально воспроизведённые разрешения/механику. Это не криптографическое доказательство
истории: внешний процесс, способный согласованно переписать приватный журнал,
его изображения и все отпечатки в другую целиком допустимую историю, находится
вне этой гарантии. Новая криптографическая защита сохранений не вводится.

Это разрешение нового **приватного поля checkpoint и проверяемого перехода
сохранения**, а не новых публичных JSON-полей, команд или самостоятельного
источника полномочий. Игровые формулы, публичный конверт, формула корреляции,
запрет частичного обычного advancement и отложенная каноническая публикация
сохраняются. RESULT-CLOSURE остаётся отдельной задачей.

## RESULT-CLOSURE-BINDING — предложение, редакция 1, 2026-09-29

Статус: **утверждено владельцем 2026-09-29**. Явный ответ «Утвердить редакцию 1
(рекомендуется)» относится к точному представленному тексту с SHA-256 `65BDA9AB…`.
Перед зависимой реализацией обязательны соответствующие plan.md, tasks.md и
проверка согласованности; утверждение не означает завершение реализации.
Источник: GitHub #1536 / T081-D-POSITION-RESULT-CLOSURE. Это ограниченный первый
блок этой задачи; он не закрывает остальные зависимости исходов и длинных цепочек.
Утверждённые POSITION-DECISION и DEPENDENT-FRONTIER-DECISION не изменяются.

### Требуемый результат и границы

После материализации выбранной раны последний исходный обмен `binding` или
`force_binding` может потерять обязательное эффективное преимущество. Если
подготовка и решающий успех также не дают законной предпосылки, клиент должен
выдать ограниченный запрос исправления, сохранив весь исходный ход и выбор раны.
GM исправляет неуспешный исход и состояние контроля по существующим правилам;
клиент проверяет их, а не придумывает результат за GM.

Этот блок охватывает только случай, когда успешные оковы меняли состояние
контроля, а остальные поля `before` и `after` последнего обмена совпадали.
Полная исходная проверка должна подтверждать законность обмена до новой раны.
Неприменимые раны, утрата исходной власти, уже ошибочный исходный обмен, иные
изменения strain/позиции и последующие обмены не расширяют эти разрешения:
они остаются заблокированными либо обрабатываются своим ранее утверждённым путём.
Остальная RESULT-CLOSURE остаётся открытой; её критерии не сокращаются.

### Предлагаемое правило исправления

1. Только владелец исходного хода выводит потерю предпосылки из принятого
   префикса, сохранённой раны и текущего конкретного обмена. Одного публичного
   сообщения об ошибке или имени `operationType` недостаточно для разрешений.
2. Первый запрос разрешает существующую точную группу зависимой арифметики,
   если она требуется, и только два новых поля этого обмена: `outcome` и
   `after.controlState`. Операция, подготовка, участники, входящее действие,
   кубики, независимые модификаторы, strain и каноническая позиция остаются
   исходными. Это правило не добавляет разрешений расхода, но сохраняет все
   независимо выведенные разрешения его исправления по уже утверждённым правилам:
   одна рана может иметь одновременно штраф позиции и стоимости действия.
   Нельзя менять закрытый предшествующий обмен.
3. GM выбирает существующий `no_effect` или `blocked`; выбранный вариант должен
   пройти обычную полную проверку обмена. `after.controlState` копируется точно
   из принятого `before.controlState`, включая все поля и их отсутствие.
   Смешанный вариант «неуспешный исход с прежними успешными оковами», частичное
   исправление арифметики и добавление новых последствий не принимаются.
4. Если законная подготовка, решающий успех либо оставшееся эффективное
   преимущество по-прежнему разрешают эти оковы, разрешения результата не
   открываются. Формула броска не превращается в универсальное правило выбора
   `outcome`; диапазон результата может остаться тем же после штрафа.
   Сильная предпосылка `force_binding` сохраняется отдельно: эффективного `+1`
   или одного boolean `setup: true` недостаточно. Нужны эффективное `+2`,
   существующие `setupState`/`bindingSetup = ready` либо решающий успех.
5. Лишь после настоящего ответа GM и обычной проверки исправленного обмена
   клиент может выдать отдельный следующий `dependent_draft` для итогового
   состояния контроля конфликта. Он разрешает только точное поле текущего
   исходного носителя, соответствующее `activeConflict.controlState`.
   Путь, значение и наличие поля выводятся владельцем по существующему приоритету
   исходных носителей; игнорируемые соседние поля wrapper, целые контейнеры и
   другой жизненный контур не разрешаются. Допустимое значение и наличие —
   точная копия уже проверенного последнего `exchange.after.controlState`.
   Одновременно исправлять будущий итог в первом
   ответе запрещено; это не новый выбор исхода и не полномочие произвольной записи.
   Для этой промежуточной границы допускается только независимо доказанное
   несоответствие итогового контроля: физически он остаётся точным исходным полем.
   В отдельном временном изображении для проверки можно заменить лишь этот
   итог точной копией действительно исправленного последнего обмена. Все обычные
   правила самого обмена, ресурсов и остальных данных обязательны; никакая иная
   ошибка не игнорируется. Это не физическая запись и не каноническая публикация.
   После ответа на итоговое поле нужна полная обычная проверка настоящего сырого
   черновика без такой промежуточной замены.
6. Для каждого запроса нужны его собственный `continuationId` и Ready. Приватный
   журнал принятого прогресса, сохранение точных прежних изображений, холодный
   повтор, отказ при подмене и отложенная единственная публикация используют
   утверждённые гарантии редакции 2. Принятая арифметика и результат первого
   ответа не меняются при исправлении итогового поля.
   Первый ответ может добавить запись прогресса только после реального закрытия
   последнего оставшегося исходного обмена. Холодный повтор независимо
   восстанавливает его запрос до записи, политику и принятые изображения, затем
   выводит итоговый запрос, даже когда непринятых обменов больше нет. Итоговый
   запрос не создаёт нового обмена/возможности раны или синтетической записи
   прогресса. Он завершается своим ответом и обычным сохранением совокупного
   принятого изображения; форма журнала и формула корреляции не меняются.

### Пример и приёмка

В двух обменах каноническая позиция остаётся `player_advantaged` (`+1`).
Первое давление: исходные кубики `5/15`, позиционный бонус игрока `+2`,
итоги `7/15`, margin `-8`; игрок получает strain `clear → strained` и
сохранённую рану I со штрафом позиции `binding: 1`. Этот обмен остаётся закрытым.
Последние оковы до раны: кубики `13/10`, бонус `+2`, итоги `15/10`, margin `5`,
`player_success`, контроль `none → player hindered`. После раны эффективная
позиция равна `0`: итоги `13/10`, margin `3`, диапазон всё ещё `player_success`.
Без setup и decisive успеха предпосылка оков потеряна. GM может исправить этот
обмен на `no_effect` с неизменным контролем `none`, затем отдельным ответом
исправить итоговый контроль на уже проверенное `none`. Ни один кубик, расход,
strain, сохранённый выбор или каноническая позиция не переписываются.

Приёмка требует настоящего сохранённого выбора и последовательных публичных
ответов в исходном GameEngine-ходе; оба вида оков; холодное восстановление после
принятия исправленного обмена до ответа на итоговое поле; прежние ресурсы и
однократную публикацию. Отрицательные проверки отклоняют добавленную подготовку,
подмену операции/кубиков/участника, сохранённый успешный контроль, изменения
закрытого префикса, чужие поля `after`, итоговое поле в первом ответе, поддельный
последующий запрос и изменение ранее принятого результата после перезапуска.
Отдельно проверяются достаточная setup/decisive предпосылка, неприменимая рана и
случай за пределами этого ограниченного блока: новые разрешения не возникают.
Обязательны три отдельные регрессии: одновременное исправление позиции и
независимо доказанной стоимости; `force_binding` с падением эффективного ранга
`+2 → +1` и только `setup: true` открывает исправление результата; посторонняя
ошибка обычной проверки вместе с разрешённым несоответствием итогового контроля
по-прежнему отклоняется и не скрывается временным изображением.

GM-руководства, живой контракт, worked example, manifest и документационные
проверки обновляются вместе с реализацией. Нужны адресные прогоны, Fast,
условный FullValidation и независимое ревью Astra XHigh; тяжёлые реальные
сценарии остаются в соответствующей интеграционной группе.

### Рассмотренные варианты

Рекомендуется отдельный ответ GM на итоговое поле: он сохраняет существующую
границу авторства и явно проверяемую последовательность. Автоматическая запись
итогового контроля клиентом сократила бы число ответов, но расширила бы его
полномочия на исходный черновик; эта альтернатива не разрешается данной редакцией.
Сохранение общего отказа без ограниченного исправления безопасно, но не даёт
завершить уже законно начатый ход с выбранной раной и не выполняет этот пункт.

## MORTAL-RECOVERY-PUBLICATION — редакция 1, утверждена 2026-09-29

**Статус: точная редакция 1 утверждена владельцем 2026-09-29.** Связанные задачи:
#1536, T069-C/T070 и T081-D-PERFORMANCE-RUNTIME-UNBLOCK. Разрешение устранить
три блокера PreMerge получено; прежняя спецификация прямо оставляла постоянный
формат квитанции восстановления неопределённым. Эта редакция закрывает именно
эту границу. Владелец ответил «Утвердить редакцию 1» на ссылку и описание этой
точной редакции; зависимая реализация разрешена в указанных ниже границах.

### Результат и границы

Реальная, принятая клиентом проверка естественного восстановления получает
замкнутый неизменяемый результат и квитанцию в существующей истории раны.
Планировщик, эффектный этап и общий принятый план сохраняют одного владельца;
только обычная координированная публикация записывает состояние. Новый файл
контроля, команда GM, произвольный JSON-переход, обход создания раны или второй
издатель не вводятся. Полная T070, команды лечения, RESULT-CLOSURE и потребитель
смертельного исхода вне этого блока остаются открытыми.

### Механический результат принятой проверки

Один новый прошедший интервал восстановления добавляет один пункт прогресса.
При достижении существующего `currentStepThreshold` рана переходит на одну
ступень тяжести вниз; `carryOverflow=true` переносит остаток, а `false` отбрасывает
его после первого достигнутого порога. Тот же объявленный порог применяется к
следующей ступени. Достижение порога на ступени I использует отдельный существующий
этап полного исцеления и завершения последствий в том же общем плане. Вся цепочка
ограничена четырьмя ступенями, не циклом по каждому прошедшему интервалу; арифметика
проверяется на переполнение. Смена тяжести действительно завершает старые эффекты
и создаёт законные новые поколения по FR-027; пустой эффектный пакет не заменяет
рематериализацию. Этот пункт требует явного утверждения: прежний Mortal-контракт
определял расписание и порог, но не задавал полную функцию перехода через порог.

Один применимый результат ухудшения выполняется один раз за принятую проверку:
`increase_severity` повышает тяжесть ровно на одну ступень даже при нескольких
пропущенных интервалах. Все учтённые интервалы при этом отмечаются как потреблённые.
Результат обязан оставаться применимым; IV не превращается в смерть автоматически.
Если одновременно есть восстановление и ухудшение, сохраняется порядок уже
выданных типизированных намерений; невозможный совмещённый результат отвергается
целиком без частичной публикации. `death_contour` сохраняется как типизированная
передача владельцу жизненного цикла, без прямого изменения жизни или смерти раной.
Фактическое исполнение отдельного жизненного цикла не объявляется реализованным.

`NotDue`, `BlockedNotStabilized` и `NoNaturalRecovery` могут публиковать результат
проверки и квитанцию, но не добавляют прогресс, не ухудшают рану и не потребляют
ещё не наступившие интервалы. Нет нового броска, платы или сообщения о лечении.

### Расписание и постоянное доказательство

Якоря сохраняют исходную принятую эпоху создания/стабилизации и начала условия.
Результат хранит отдельно уже учтённые порядковые номера интервалов для каждой
эпохи. Новая проверка использует только разницу между прошедшими и уже учтёнными
интервалами. Например, якорь100, период10 и время135 дают три интервала и следующий
срок140; сохранение результата не сдвигает следующий срок на150. Якорь ухудшения
не переписывается для подавления повторного применения. Настоящая стабилизация
или повторная травма меняет только соответствующую эпоху по FR-065; записи другой
эпохи не доказывают потребление её интервалов.

Новый клиентский результат `recover` хранится в существующем `transitionResult`.
Его замкнутые части: версия, полный источник раны до проверки, исходная принятая
привязка/событие, исходный типизированный результат планировщика и принятая минута,
координаты обеих эпох и потреблённые интервалы до/после, координаты перехода и
отпечатки до/после, типизированная передача жизненному циклу при её наличии,
квитанция и отпечаток всего результата. Коллекции и JSON-проекции отделены от
авторитета; внешний вызывающий код не может изменять их или выдавать собственную
структуру за полномочие. Квитанция по-прежнему имеет ровно четыре поля:
`AuthorityFingerprint`, `ReceiptFingerprint`, `TickKey`, `WoundId`.

Парсер заново проверяет закрытую форму, исходную рану, арифметику, семантические
координаты, квитанцию и согласие с внешней строкой истории. Один лишь правильный
формат отпечатка не является доказательством результата. Одна логическая проверка
имеет одну итоговую квитанцию; дополнительные стадии тяжести/исцеления связаны с
тем же принятым результатом и не выпускают её повторно.

Порядок повторного применения: текущий реестровый владелец и привязка → полная
проверка текущей истории и её подписанного состояния → поиск постоянного результата
по актуальному `lastTickKey` и принятой минуте → только затем новая арифметика.
Повтор в новую формальную привязку при той же минуте возвращает исходную квитанцию,
без новых намерений, плана или записи. Чужой/изменённый результат отвергается.
Повреждённая история или квитанция даёт `InvalidHistory` раньше обращения к
испорченным живым часам. Новый `TickKey` из изменившейся после публикации раны
не заменяет исходное доказательство повторного применения.

### Приёмка

Нужны реальные общие публикации для срока−1/срока/срока+1, нескольких интервалов,
перебазирования стабилизацией, блокировки и отсутствия естественного восстановления,
ухудшения и типизированной передачи жизненному циклу. Проверяются точные состояния
прогресса/тяжести/эффектов и постоянные квитанции, последующий срок без пропуска,
неповторение ухудшения внутри одного интервала, холодный повтор при той же минуте,
повреждение истории/квитанции, изменённый результат, прежние владельцы и откат.
Композиция не пишет ни один канонический байт; неуспех не регистрирует план.
Существующие сценарии сохраняются, а ожидания расположения полностью исцелённой
раны уточняются до точной проверки архива и завершения всех последствий.

Руководство GM получает честное описание реализованной клиентской границы и
пошаговый пример прогресса/повтора; оно не утверждает завершение всего смертельного
жизненного цикла. Требуются адресные проверки, XML-сборка, независимое Astra XHigh
ревью и полный PreMerge≤30 минут. Fast остаётся7 минут; после контроля работа
останавливается для проверки владельцем. Это не разрешает менять отбор проверок
или выдавать быстрое падение за измерение полного успешного набора.

## TEST-LANE-BOUNDARY-DECISION, редакция 1 — утверждена владельцем (2026-09-30)

**Остановлено последующим указанием владельца 2026-09-30.** Обязательные широкие
контроли из этого раздела и предыдущих разделов больше не выполняются. Новая
стратегия описана в [CATEGORY-SELECTION-DECISION, редакция 1](../1505-test-suite-performance/spec.md)
и утверждена владельцем 2026-09-30 («подтверждаю»). Прежняя реализация и результаты сохранены;
незавершённые прогоны не считаются успешными. Разработка игровых возможностей
по-прежнему приостановлена до окончания и проверки работы над тестовой средой.

Источник: #1536 и задача производительности тестов #1505. Это утверждённое
изменение прежнего условия неизменного отбора PreMerge из FR-014 задачи #1505
и из разделов выше. Предел `PreMerge≤30` пока сохраняется.

### Результат и границы

Сохранить короткий повседневный контроль и все значимые проверки материализации,
не заставляя один PreMerge последовательно проходить каждую подробную матрицу.
`Fast` остаётся в пределах семи минут, PreMerge — в пределах 30 минут.
Если полный измеренный набор не укладывается после оптимизации, изменение
предела требует отдельного точного предложения и утверждения; ни одна штатная
группа не получает часовой предел. Изменяются
только состав тестовых групп, подготовка фикстур, проверка покрытия и документация
тестирования. Игровые правила, производственный код и контракты GM вне области.

В прогоне 2026-09-30 после 29:22 завершились 9345 случаев с двумя падениями,
но из 67 параллельных дескрипторов завершились лишь 36; две из четырёх групп
AfterlifeResourceCutoverTests и последующие ProcessIntegration/E2E не дошли до
полной проверки. Число в итоговом JSON считало выполненные TRX, а не весь план.
Поэтому 29:22 не служит доказательством выполнения полного PreMerge за 30 минут.
Два падения исправлены только в тестах; адресный контроль прошёл 2/2 и
независимое Astra XHigh ревью не нашло замечаний.

### Предлагаемая граница

1. Вынести 704 обычных случая `AfterlifeResourceCutoverTests` в именованную
   интеграционную группу духовного конфликта с отдельным 30-минутным запуском.
   Пять случаев этого же тестового класса с категорией
   `ProcessIntegration` остаются в прежней отдельной фазе и не попадают в
   новый обычный запуск. Не удалять ни один уникальный сценарий.
   Подготовленные неизменяемые байты
   можно повторно использовать только при точном совпадении параметров; каждый
   тест по-прежнему получает новый корень, поколение, lease, владельцев и
   настоящий холодный переход там, где он является предметом проверки.
2. Оставить в PreMerge небольшой явно зафиксированный набор представителей
   этой матрицы: исходное допущение, выбор/переход раны, зависимый следующий
   запрос, холодное возобновление и окончательную публикацию. Для каждого
   механизма должна оставаться хотя бы одна проверка полного пути; конкретный
   перечень тестов закрепляется в плане и защищается проверкой отбора. Остальная
   матрица обязательна отдельной группой при изменении духовного конфликта,
   ран, восстановления, C2/C3/C4 или соответствующих контрактов, а не при
   каждом небольшом изменении проекта.
3. Сохранить полный Fast, остальную штатную Integration, отдельные фазы
   ProcessIntegration и E2E, стартовую волну ExplorerWeb, существующие
   ограничения параллелизма и выявление дублей внутри PreMerge. Проверка
   отбора должна доказывать отсутствие потерь относительно прежней полной
   обычной матрицы: каждый из 704 случаев присутствует в новой отдельной
   группе, пять процессных остаются в ProcessIntegration, а
   представители PreMerge — ожидаемое намеренное пересечение между группами.
4. Проводить адресные проверки изменённого тестового механизма, контроль Fast
   при значимом рубеже, полную новую интеграционную группу и затем один полный
   PreMerge. Каждый запуск должен удержать JSON/TRX/лог, действительное число
   запланированных и выполненных тестов, отсутствие пропусков/дублей внутри
   запуска и успешную очистку. Одна проверка всей границы проходит независимое
   Astra XHigh ревью. После успешного полного контроля сообщить измеренное
   время и остановиться для проверки владельцем.

### Рассмотренные варианты

Продолжать включать все подробные случаи в PreMerge и просто поднять его
тайм-аут — не рекомендуется: завершённая часть текущего плана уже заняла
почти 30 минут, а полное время не измерено. Удалять тяжелые случаи без
доказанной повторяемости — также не рекомендуется. Отдельная полная группа
сохраняет доступность этих проверок и делает обычный PreMerge обозримым.

### Нерешённое измерение

Точный список восьми представителей закреплён в `plan.md` и защищается
проверкой отбора. Предел отдельной группы — 30 минут.
Если он недостижим без потери важного покрытия, отдельную матрицу следует
разделить ещё раз. Любое повышение предела требует измерений и отдельного
утверждения точного значения. Первая полная проверка покажет, достаточно ли
одного переноса
духовной матрицы или требуется дополнительное разделение других тяжелых классов.
