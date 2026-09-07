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
- **FR-081**: Elyara of the Last Wound MUST be discoverable from first Chaos Sea entry, fixed at Spiritual Healing tier V, publicly available at the Lazaret, and priced at the ordinary multiplier.
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
