# Feature Specification: Complete Effect Materialization

**Feature Branch**: `1535-effect-materialization`

**Created**: 2026-08-14

**Status**: Implementation verification in progress

**Input**: User description: "Materialize active effects across Mortal World and afterlife so the GM cannot create incomplete, ambiguous, non-expiring, incorrectly stacked, or mechanically unsafe runtime effects. Effects caused by wounds must remain separate from the wound itself, and no migration of technical Pre-Alpha saves is required."

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**: [#1535 — Enforce complete effect materialization across Mortal World and afterlife](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535); blocking foundation [#1543 — Materialize one canonical resource authority](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543)
- **Issue type**: P1 cross-realm canonical-state and validation feature
- **Spec Kit justification**: The feature changes GM-authored commands, canonical runtime state, identity and lifecycle authority, derived mechanics, accepted-turn and afterlife transactions, rollback and repair, console/browser projections, and both Mortal and afterlife documentation across many files and sessions.
- **Contract scope**: Player-facing console and browser; GM-facing prompts and examples; Mortal World and afterlife runtime state; validation; normalization; lifecycle scheduling; identity and source authority; rollback and repair; documentation and manifests.
- **Save compatibility**: Not required. The game is technical Pre-Alpha, active repository templates and fixtures move to the current contract, and legacy non-empty effect state is unsupported rather than migrated or promoted.
- **Out of scope**: Materializing static skill, spiritual-art, item, Fate Card, quest, location, faction, event, wound, or combat-action definitions themselves; wound treatment and healing (#1536); skill and spiritual-art progression (#1533/#1534); quest materialization (#1537); actor progression (#1538); general combat redesign; converting Shining blessing entitlements into GM-authored active effects.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Trustworthy Active Effect Creation (Priority: P1)

As a player, I want every active effect applied to my character or another actor to be a complete, source-authorized mechanical entity so that the game never applies a bonus, penalty, restriction, or periodic change from an incomplete description.

**Why this priority**: Creation is the authority boundary for every later tick, stack, cure, projection, and derived mechanic. If it is incomplete, all downstream behavior is unsafe.

**Independent Test**: Apply representative valid buff, debuff, periodic, triggered, environmental, item/skill/art, quest, wound-derived, and Fate Card-authorized instances; verify complete instances are accepted with client-owned identity and every missing or unauthorized required section rejects the entire transition before any mechanic or state is published.

**Acceptance Scenarios**:

1. **Given** an exact active source definition and exact same-realm target, **When** the GM requests an allowed active effect with bounded parameters, **Then** one complete active instance is created with client-owned identity and source-authorized mechanics.
2. **Given** a request with a missing source, target, realm, readable display, lifetime, stacking policy, removal rule, or non-empty mechanical payload, **When** the turn is validated, **Then** no active instance and no derived mechanic is published.
3. **Given** a static `combatEffect`, `structuredBonuses`, Fate Card effect, or other definition that does not authorize a runtime instance, **When** it is merely present in canonical source data, **Then** it remains a definition and does not become an active effect.
4. **Given** a case-variant, Unicode-confusable, historical, cross-realm, or ambiguous source or target selector, **When** application is requested, **Then** the request fails closed without retargeting.

---

### User Story 2 - Deterministic Stacking and Lifetime (Priority: P1)

As a player, I want repeated applications, triggers, ticks, expiry, dispels, and removals to occur in a deterministic order and exactly once so that retries cannot duplicate damage, healing, stacks, rewards, or duration.

**Why this priority**: Effects are stateful mechanics. Identity without a deterministic lifecycle would still permit double application, immortal statuses, and divergent clients.

**Independent Test**: Exercise every supported stack policy and lifetime mode at minimum, maximum, repeated-event, and expiry boundaries; replay the same accepted input and verify the final state and mechanics are unchanged.

**Acceptance Scenarios**:

1. **Given** an existing stack coordinate, **When** the same source applies again, **Then** the source-authorized `independent`, `stack`, `refresh`, `replace`, or `merge` behavior is selected without allowing the GM to choose the resulting identity, count, or policy.
2. **Given** a turns, uses, exact-time, scene, source-bound, condition-bound, authorized permanent, or registered manual lifetime, **When** its governed continuation or completion event occurs, **Then** it advances or terminates exactly once.
3. **Given** two due components at the same lifecycle phase, **When** they resolve, **Then** declared priority and stable identity ordering produce the same result on every run.
4. **Given** a duplicate event, trigger cycle, conflicting stack policy, numeric persistence sentinel, or prose-only lifetime, **When** validation runs, **Then** the complete effect transition is rejected before mutation.

---

### User Story 3 - One Authority Across Owners and Realms (Priority: P1)

As a player moving between Mortal World and afterlife play, I want active effects on the player, named NPCs, combatants, Guardians, residents, radiant actors, and spiritual-conflict participants to obey the same identity and lifecycle guarantees while retaining their setting-specific mechanics.

**Why this priority**: Issue #1535 explicitly covers both realms and multiple actor families. Independent schemas would recreate the same ambiguity in a different carrier.

**Independent Test**: Materialize and advance one valid instance in every supported owner family, including a specialized afterlife combat condition, and prove that cross-realm targeting, duplicate identity, and direct canonical mutation are rejected uniformly.

**Acceptance Scenarios**:

1. **Given** any supported owner family, **When** a source-authorized effect is applied, **Then** it has one logical owner coordinate and one common identity/lifecycle authority while remaining in its owner-appropriate active carrier.
2. **Given** a spiritual-conflict `mark`, `ward`, `burden`, `opening`, or `vow`, **When** it is applied or advanced, **Then** its existing counterplay and legal spiritual-combat axes are preserved while common identity, stacking, retry, and terminal-history rules apply.
3. **Given** a persistent Guardian, resident, or radiant-actor effect, **When** its owner state changes, **Then** unrelated profile state is preserved and the effect lifecycle remains atomic.
4. **Given** a generic effect whose target changes realm, **When** no exact source policy authorizes suspension or expiry for that transition, **Then** the transition fails rather than silently carrying the effect across realms.

---

### User Story 4 - Complete Mechanics and Safe Player Inspection (Priority: P2)

As a player, I want console and browser views to show every visible mechanical consequence, source context, stacks, remaining lifetime, and available counteraction without exposing internal identity, routing, validation, or GM-agent data.

**Why this priority**: An accepted effect must be understandable and actionable, and player surfaces must not become technical debug channels.

**Independent Test**: Render the same accepted visible, hidden, and private effect sets in console and browser; compare semantic facts and actions, then recursively scan serialized output for internal authority and repair data.

**Acceptance Scenarios**:

1. **Given** a visible accepted effect, **When** the player inspects status, actor, combat, or afterlife views, **Then** both clients expose equivalent readable mechanics, source label when visible, current/max stacks, remaining lifetime, and allowed cure, counterplay, or dispel actions.
2. **Given** a hidden or GM-only effect, **When** ordinary player views, counts, actions, quests, maps, news, dossiers, or game-screen data are produced, **Then** the effect and its private implications are not disclosed.
3. **Given** a malformed or partially accepted effect set, **When** characteristics, rolls, resistance, actions, periodic deltas, or afterlife axes are computed, **Then** none of that set contributes mechanics.
4. **Given** nested identity, receipt, route, transition, repair, diagnostic, file-path, validation-code, or agent-instruction data, **When** a player DTO is built, **Then** the complete internal object is suppressed without discarding adjacent legitimate in-world semantics.

---

### User Story 5 - Atomic Failure and Bounded Repair (Priority: P2)

As a player, I want an invalid or partially written effect transition to restore the complete pre-turn state and suppress stale narration, while the GM receives only a narrow repair opportunity for unambiguous semantic omissions.

**Why this priority**: Multiple distributed carriers and companions make partial publication especially damaging. Repair must never grant authority to rewrite identity or retarget an effect.

**Independent Test**: Inject a failure after every planned effect/index/companion publication and after post-validation; verify byte-and-existence-exact rollback, stale-output suppression, safe player copy, operator diagnostics, and no GM dispatch for protected authority failures.

**Acceptance Scenarios**:

1. **Given** any write or composed-state check failure, **When** publication aborts, **Then** all touched state and output return to the validated pre-turn baseline.
2. **Given** one unambiguous missing GM-owned semantic field with exact actor/source/target binding, **When** repair is offered, **Then** the repair packet permits only that bounded field and requires a coherent complete resubmission.
3. **Given** an identity, target, source, realm, stack coordinate, receipt, lifecycle event, duplicate/confusable, cycle, direct-mutation, or ambiguous-owner error, **When** failure handling runs, **Then** no actionable repair is dispatched and the player sees only generic in-world failure text.
4. **Given** a ready marker, stale receipt, partial fragment, or replayed repair response, **When** retry validation runs, **Then** it cannot become an accepted no-op or duplicate transition.

---

### User Story 6 - Wound and Effect Independence (Priority: P2)

As a player, I want wounds and their symptoms to remain related but independently governed so that removing a pain, bleeding, shock, or restriction effect never silently heals the underlying wound.

**Why this priority**: Wounds are a separate treatable entity planned in #1536. Collapsing wound and effect lifecycles would destroy medical gameplay authority.

**Independent Test**: Apply an exact wound-derived source-bound effect, remove or dispel only the effect, and prove every wound field remains unchanged; then apply an independently accepted wound transition and prove only its explicitly authorized linked effect changes occur.

**Acceptance Scenarios**:

1. **Given** a wound-derived active effect, **When** the effect is dispelled, removed, or expires, **Then** wound identity, severity, healing state, and existence are unchanged.
2. **Given** a valid wound treatment transition, **When** its exact source rule ends or changes linked effects, **Then** those effect transitions are applied without treating effect deletion as wound treatment.
3. **Given** a missing or ambiguous wound link, **When** an effect is applied or repaired, **Then** the system fails closed and never retargets it to another wound.

### Edge Cases

- A missing effect carrier in a pristine session represents an empty active set; a present non-empty legacy carrier is rejected and never silently promoted.
- An application that contains an `effectId`, an existing-effect operation that omits one, or a direct edit to identity/index/history/receipt state is rejected.
- A source permits an instantaneous damage or heal but no active lifetime; the action resolves without creating an active effect.
- `maxStacks` is reached, zero uses remain, an exact-time deadline equals current time, a scene closes, or a source-bound dependency disappears at the same transition that another application arrives.
- The same accepted event is replayed after a retry, session replacement, partial worker response, or post-check failure.
- Two effects share display names but have different exact source and stack coordinates.
- A trigger produces another effect and the dependency graph is cyclic, unbounded, or exceeds declared expansion limits.
- A deferred accepted QTE terminal damage event matches active `resource_damaged` or `resource_depleted` triggers after the original offer turn has already closed; the client must use the immutable continuation captured at QTE acceptance rather than rebuilding authority from live post-turn files.
- A source, target, wound, quest, item, location, faction, event, combat, or afterlife actor is deleted or becomes invalid during the same composed transition.
- A hidden effect would alter a visible total; the player may see the allowed derived outcome only when its source visibility policy authorizes that implication.
- A player-visible source is unreadable or unresolved even though the active identity remains technically valid; the view provides an in-world unavailable explanation instead of raw authority data.
- A realm transition occurs while ordinary and source-bound effects are active.
- A manual or permanent lifetime is requested without an exact source-authorized removal/persistence rule.
- A wound transition and direct effect removal target the same wound-derived instance in one accepted turn.
- A write fails after one of several owner carriers, companions, pending requests, or the identity authority has been published.

## Requirements *(mandatory)*

### Functional Requirements

#### Scope and authority

- **FR-001**: The system MUST govern active runtime effect instances separately from static source definitions.
- **FR-002**: Static `combatEffect`, `structuredBonuses`, Fate Card effects, item mechanics, skill mechanics, special-art descriptions, and other source definitions MUST NOT become active instances unless an exact source contract authorizes application.
- **FR-003**: Every active instance MUST have exactly one client-assigned permanent identity, logical owner coordinate, realm, lifecycle state, and immutable source binding.
- **FR-004**: The GM MUST NOT author, choose, overwrite, or reuse permanent effect identities, identity history, transition history, receipts, seals, or the identity authority.
- **FR-005**: Active and terminal identities, lifecycle event identities, and stack coordinates MUST reject exact duplicates, case variants, Unicode-confusable reuse, ambiguous same-turn references, and historical replay where applicable.
- **FR-006**: Direct mutation of canonical active carriers or client-owned effect authority without an accepted effect operation MUST fail closed.

#### Source, target, and mechanics

- **FR-007**: Every application MUST resolve one exact source definition and one exact target in the composed accepted state.
- **FR-008**: Source and target resolution MUST be case-sensitive, realm-aware, and reject aliases, name inference, historical selectors, ambiguity, and unauthorized cross-realm references.
- **FR-009**: A source definition MUST declare allowed target kinds, realm, component profiles and bounds, stacking policy, lifetime modes, triggers, visibility, removal routes, and relevant companion links before it may create an active instance.
- **FR-010**: Every active instance MUST contain readable display semantics and at least one non-empty registered mechanical component.
- **FR-011**: The supported component catalog MUST include characteristic/stat modification, advantage/disadvantage, resistance/damage reduction, periodic damage, periodic healing/resource restoration, action permission/restriction/control, deterministic event reaction, wound-bound consequence, and specialized afterlife spiritual-combat condition.
- **FR-012**: Unknown component profiles, incomplete payloads, out-of-bound parameters, and pseudo-mechanics expressed only in prose MUST be rejected.
- **FR-013**: Instantaneous damage or healing MUST remain an action result unless its source separately authorizes an active lifetime, trigger, use, source-bound state, or explicit active-instance declaration.
- **FR-014**: Exact source adapters MUST cover representative skill, spiritual art, item, wound, quest, location or hazard, faction or event, Fate Card, and combat sources without rewriting their static definitions.

#### Stacking and lifetime

- **FR-015**: Stack identity MUST be scoped by realm, target, and the exact source-authorized stack key.
- **FR-016**: A repeated application MUST resolve through exactly one closed policy: `independent`, `stack`, `refresh`, `replace`, or `merge`.
- **FR-017**: The source MUST define the stack policy, maximum, refresh behavior, replacement behavior, and bounded merge function; the GM MUST NOT submit the resulting count, identity, or policy.
- **FR-018**: The system MUST reject conflicting policies or incompatible component payloads at one logical stack coordinate before publication. An exact incoming source-owned `replace` is the sole policy-transition exception: it MAY supersede one prior valid active identity at that coordinate, while multiple prior identities remain ambiguous and MUST be rejected.
- **FR-019**: Every active instance MUST use exactly one closed lifetime mode: turns, uses, exact world time, exact scene, source-bound, condition-bound, source-authorized permanent, or registered manual.
- **FR-020**: Numeric sentinels, unsupported free-form duration text, and unbounded permanence without exact source authorization MUST NOT serve as lifecycle authority.
- **FR-021**: Permanent and manual effects MUST have explicit registered persistence and removal rules.
- **FR-022**: Realm transitions MUST apply an exact source-declared suspend-or-expire policy; ordinary effects MUST NOT silently cross realms.

#### Deterministic lifecycle

- **FR-023**: Each accepted turn, deferred accepted QTE terminal continuation, or afterlife exchange MUST reconcile bound continuations, apply removals/dispels, resolve applications/stacks, materialize side-effect-free trigger candidates, arbitrate only actual events into one causal accepted-activation transcript, resolve conditionally released components, consume uses/time exactly once, expire terminal instances, and recompute derived mechanics in one documented deterministic phase order.
- **FR-024**: Within a lifecycle phase, ordering MUST be stable by declared priority, permanent effect identity, and component identity.
- **FR-025**: Every application, tick, trigger, receipt, stack transition, removal, dispel, suspension, resumption, and expiry MUST be idempotent for one exact accepted event.
- **FR-026**: Triggered downstream effects MUST form a finite acyclic declared dependency graph with bounded expansion.
- **FR-027**: Deterministic components MUST resolve without GM invention; story-facing components MAY request only a bounded result whose allowed kinds, targets, numeric bounds, and companion changes are fixed by client authority.
- **FR-027a**: A GM-reported trigger event MUST use a closed registered report adapter bound to sealed accepted-turn evidence; the client MUST select the exact effect, trigger, ordering, use consumption, and post-state, and a report MUST NOT contain those client-owned selectors or results.
- **FR-028**: Bounded result receipts MUST match the exact pending effect, target, source, trigger, event, causal activation ordinal/priority/uses evidence, producer, component dependency, candidate/transcript fingerprints, wave, and accepted session; they MUST replace only that activation's output during full graph replay, preserve earlier resolved waves, and MUST NOT change identity, definition, realm, profile, or bounds.
- **FR-028a**: Every actual resource event MUST open one immutable causal boundary whose complete candidate batch is frozen before any reaction output. The common accepted-use arbiter MUST accept or deny the whole batch before release; denied candidates MUST leave no transcript output or expansion use. One monotonic mechanics order MUST prove producer mutation, boundary open, `before_current_event`, causal continuation and nested boundaries, `after_current_event`, then one final uses/lifetime/terminal projection. `after_component` MUST release only from exact nonzero applied-component evidence. A `suspend` or `remove` release MUST NOT revoke sibling outputs already accepted in the same frozen boundary, but MUST make that effect unavailable to every later boundary. For every released `apply_definition(policy=replace)`, the client MUST derive the exact pre-reaction replacement target from `(realm,target kind/id,source kind/id,stackKey)` authority, excluding `definitionKey`; release MUST make that target unavailable to later or nested boundaries, while `after_current_event` MUST reserve it immediately after frozen-batch acceptance. Before any reaction-owned identity or transition allocation, the client MUST build one indexed linear preflight and simulate every released `apply_definition` on each replacement coordinate in `MechanicsOrdinal` order. Every replacement MUST then satisfy one closed occupancy expectation: exact typed frozen target, frozen absence, or the exact typed result of the immediately preceding authorized self-replacement. A preceding non-replace release MUST NOT implicitly occupy `FrozenAbsent`, introduce ambiguous occupancy, or carry a known incompatible stack policy into a later replacement; a compatible identity-preserving sibling application MAY complete. Two releases MUST NOT independently claim one frozen-empty coordinate, and releases owned by different typed effect identities MUST NOT replace one common frozen target. The only multi-release replacement fold is a same-boundary consuming self-replacement cascade whose typed owner equals the frozen target; every later member MUST revalidate and retire the exact typed result of its predecessor. Already accepted valid siblings MUST still complete, and the replacement identity MUST first become trigger-eligible in the next accepted mechanics transition. If one or more accepted consuming activations belong to the retired target, the system MUST validate their immutable `N, N-1, ...` budgets and record every `consume` in activation order immediately before that target's single authoritative `replace`; intermediate and final replacement identities retain their complete source-created lifetimes, including when the old effect spends its final use. Final validation MUST prove the typed runtime target and exact create/replace transition for every released replacement, not only the first raw effect ID in a group. Expansion MUST be charged only when an accepted reaction is actually released.

#### Owners, realms, and derived mechanics

- **FR-029**: The common authority MUST cover Mortal player, named NPC, stable Mortal combatant, Guardian, Shining Abode resident, radiant actor, and applicable afterlife spiritual-conflict active effects.
- **FR-030**: Active effects MUST remain in owner-appropriate carriers while one common authority proves identity, source, target, lifecycle, stack, and terminal-history agreement across all carriers.
- **FR-031**: Terminal instances MUST leave active carriers while immutable terminal evidence remains sufficient to prevent replay.
- **FR-032**: Afterlife `combatConditions[]` MUST retain their specialized kinds, legal axes, visibility, counterplay, affected operations, and exchange/scene semantics while adopting common identity, stack, retry, and terminal-history guarantees.
- **FR-033**: Client-derived Shining blessing entitlement state MUST remain outside GM-authored active-effect operations.
- **FR-034**: Characteristics, rolls, resistances, action permissions, periodic deltas, and afterlife axes MUST be derived all-or-nothing from one complete accepted effect set, never raw commands or partially valid arrays.
- **FR-035**: Player-facing summary fields MUST be derived from accepted active instances and MUST NOT become a second GM-authored mechanical authority.

#### Wounds and companions

- **FR-036**: A wound-derived effect MUST retain an exact link to its source wound and its own independent effect identity and lifecycle.
- **FR-037**: Dispel, removal, expiry, or cleanup of an effect MUST NOT heal, delete, reduce severity, or otherwise mutate its source wound.
- **FR-038**: Only a separately accepted wound transition MAY cause source-authorized changes to linked effects.
- **FR-039**: Missing, ambiguous, or mismatched wound authority MUST fail closed and MUST NOT be repaired by selecting another wound.
- **FR-040**: Effect-owned companions and pending work MUST be created or cleaned exactly once without deleting unrelated owner, source, target, or wound state.

#### Atomicity, repair, privacy, and documentation

- **FR-041**: Validation and publication MUST use one accepted effect plan bound to the validated session, pre-turn state, source authority, target authority, and accepted input.
- **FR-042**: The complete composed final carrier set, identity authority, stack outcomes, references, companion changes, and derived mechanics MUST validate before publication.
- **FR-043**: All touched carrier, identity, companion, derived-state, pending, and player-output paths MUST publish atomically under one accepted transition.
- **FR-044**: Any write or post-publication validation failure MUST restore every touched path byte-for-byte and by existence to the pre-turn state and suppress stale player output.
- **FR-045**: A repair packet MAY target only one unambiguous GM-owned semantic omission with exact actor/source/target binding and MUST require one coherent complete resubmission.
- **FR-046**: Identity, target, source, realm, stack, receipt, history, lifecycle-event, duplicate/confusable, cycle, direct-mutation, ambiguous-owner, and cross-realm failures MUST NOT produce an actionable repair packet.
- **FR-047**: Player-facing failure and waiting text MUST use generic in-world Russian wording; detailed codes, paths, authority, validation, repair, rollback, and agent instructions MUST remain operator-only.
- **FR-048**: Console and browser MUST expose equivalent visible mechanics, source context when permitted, stacks, remaining lifetime, and cure/counterplay/dispel eligibility from one accepted projection.
- **FR-049**: Ordinary player DTOs MUST recursively suppress hidden/GM-only effects and internal identities, routes, source-authority objects, receipts, transitions, pending/repair/diagnostic wrappers, paths, codes, and agent instructions.
- **FR-050**: GM rules, compact constructor guidance, Mortal and afterlife worked examples, validation manifests, prompt entrypoints, documentation tests, and source guards MUST change together with the executable contract.
- **FR-051**: Worked examples MUST cover every supported component family and include application, stack, refresh, replace or merge, periodic tick, trigger, expiry, dispel, environmental source, item/skill/art source, quest source, wound-derived source, afterlife condition, and one bounded repair.
- **FR-052**: The implementation MUST update active new-game state, templates, examples, and tests to the new schema and MUST NOT add migration, compatibility readers, or legacy promotion for non-empty technical Pre-Alpha effect state.
- **FR-053**: Accepting a Mortal QTE offer MUST capture an immutable deferred effect continuation bound to the exact session and session generation, accepted source turn, offer fingerprint, byte/existence-exact pre-terminal roots, source and target authority, trigger snapshot/index, pending causal state, full semantic turn, and a persistent semantic identity ledger. Terminal QTE resource events MUST consume only that continuation through the common accepted-mechanics planner; live/post-image rebuild, random identity fallback, stale or cross-session continuation, and resource-only trigger bypass are forbidden. A bounded result MUST use a dedicated `qte_effect_resolution` request/response/ready transport and resume handler, not `turn_request`, a synthetic pending-turn snapshot, or the ordinary validation-repair loop. The request exposes only the standard safe pending packet and exact correlation; the closed GM response may contain only matching effect-resolution receipts. Receipt resume MUST NOT rerun story/progression, increment the ordinary turn, or reselect the terminal branch. Resource quartet, effect carriers/index/history, pending/reaction outputs, QTE history, runtime closure, continuation state, and transport cleanup MUST publish or roll back together. If a bounded result is required, the client MUST seal the selected terminal binding once, keep the QTE open in an awaiting-receipt state across restart and every bounded wave, preserve all prior receipt bindings and preallocated identities, and close/publish mechanics only after the final complete replay produces no pending work.

### Key Entities *(include if feature involves data)*

- **Active Effect Instance**: One live or suspended, client-identified mechanical state bound to a realm, exact target, exact source definition, registered components, lifetime, stacking coordinate, visibility, removal rules, and chronology.
- **Effect Source Definition**: A validated static authority that permits a bounded active instance without itself becoming active state.
- **Effect Target**: An exact same-realm owner coordinate for a player, NPC, combatant, Guardian, resident, radiant actor, or spiritual-conflict participant.
- **Mechanical Component**: One registered, closed, ordered payload with validation, execution, and player-projection semantics.
- **Stack Coordinate**: The realm/target/source-authorized key that determines repeated-application behavior.
- **Lifetime State**: The closed continuation and remaining evidence for turns, uses, time, scene, source-bound, condition-bound, permanent, or manual lifetime.
- **Effect Identity Authority**: Client-owned active and terminal identity, owner, stack, source, lifecycle, and transition evidence used for uniqueness and retry safety.
- **Accepted Effect Plan**: One immutable composed transition that contains final owner effects, identity changes, companions, derived mechanics, and touched state for validation and atomic publication.
- **Pending Effect Resolution**: Client-owned bounded work for an effect component requiring constrained GM narration or a permitted story decision.
- **Effect Resolution Receipt**: Exact response to one pending resolution that cannot expand or redirect its authority.
- **Deferred QTE Effect Continuation**: Client-owned acceptance-time authority that seals one active Mortal QTE's source generation, semantic planning roots, terminal selection, persistent identities, bounded waves, and terminal replay evidence independently of an ordinary turn snapshot.
- **Player Effect Projection**: The discovery/visibility-aware readable view shared by console and browser, with internal authority removed.
- **Wound Link**: An exact source relationship that lets wound state govern a linked effect without giving the effect permission to heal or delete the wound.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: 100% of representative valid constructors for buff, debuff, periodic, triggered, environmental, item, skill, spiritual art, quest, wound, Fate Card, Mortal combat, and afterlife condition sources create one complete active instance or a documented instantaneous result.
- **SC-002**: 100% of tested applications missing any required source, target, realm, display, component, lifetime, stack, or removal authority are rejected before state or mechanics change.
- **SC-003**: All five stack policies and all eight lifetime modes produce identical final state under at least three repeated executions of the same accepted event, with no duplicate transition, tick, use, reward, or terminal result.
- **SC-004**: Every supported owner family passes creation, lifecycle, direct-mutation rejection, and terminal-history tests, including specialized afterlife conditions.
- **SC-005**: Failure injection after every planned publication path and after final validation restores 100% of tracked files byte-for-byte and by existence while producing no stale player output.
- **SC-006**: Console and browser parity tests show 100% agreement on visible effect mechanics, source labels, stacks, lifetime, and available actions for the same accepted state.
- **SC-007**: Recursive privacy tests find zero internal identity, route, receipt, transition, repair, diagnostic, path, validation, or agent-instruction tokens in ordinary player outputs.
- **SC-008**: Wound-bound tests show zero wound-field changes when an effect alone is dispelled, removed, cleaned, or expires.
- **SC-009**: Doubling a representative active-effect and trigger population increases validation and lifecycle work by no more than 2.5 times and does not introduce repeated full-catalog scans per effect.
- **SC-010**: GM documentation validation proves at least one executable worked example for every supported component family and lifecycle operation named in FR-051 across Mortal and afterlife guidance.
- **SC-011**: All required local focused, documentation, lifecycle, broad, and pre-merge controls pass with zero duplicate test execution before integration.
- **SC-012**: Deferred QTE resource-event tests prove that matching effect triggers execute exactly once from the acceptance-time continuation, restart/retry preserves the same terminal selection, wave bindings, and semantic identities, exact terminal replay is a no-op, stale/tampered/missing continuation or transport fails before mutation, bounded pending waves require no ordinary turn snapshot or turn increment, and per-path injected writes restore every resource/effect/pending/QTE/transport path byte-for-byte and by existence.

## Verification Plan *(mandatory)*

- **C# verification**: Use `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused` with exact effect contract, identity, planner, scheduler, characteristics, afterlife condition, projection, repair, rollback, and documentation filters during TDD; run one meaningful `Fast` checkpoint, conditional `LifecycleIntegration`, and one final `PreMerge` without a duplicate Fast immediately before it.
- **Documentation/contract verification**: Run focused `PromptDocumentationCoverageTests`, `AfterlifeDocumentationCoverageTests`, and `ExampleDocumentationValidationTests`; run `FullValidation` because shared Mortal/afterlife rules, examples, and validation manifests change.
- **Frontend verification**: Existing C# browser DTO/projection tests are required. Run frontend verification only if frontend source or typed contracts change; perform rendered browser checks only if visual layout changes.
- **Manual/player-facing verification**: In a generated Mortal session and an afterlife conflict, inspect visible, hidden, stacked, periodic, source-bound, expiring, dispellable, and malformed effects in console and browser; confirm equivalent in-world Russian semantics, correct actions, no internal authority data, no stale output after rejection, and unchanged wound state after effect-only removal.

## Assumptions

- The project remains technical Pre-Alpha and has no supported public save population; repository-owned current fixtures are updated rather than migrated at runtime.
- Static source definitions remain governed by their existing materialization features; #1535 consumes their exact accepted authority through bounded adapters.
- Missing carriers in pristine sessions represent empty active sets, while present non-empty carriers must satisfy the new schema exactly.
- Existing afterlife spiritual-combat condition semantics are valid specialized mechanics and are adapted rather than replaced.
- Persistent afterlife actor effects use the accepted owner/profile authority; Shining blessing entitlements remain client-derived pending state.
- Wound lifecycle and healing remain owned by #1536; #1535 only establishes exact links and effect-side behavior.
- Trigger execution, periodic damage/restoration, uses advancement, terminal cleanup, and bounded resource receipts in T042–T043/T047/T049–T050 use #1543 US4's single canonical resource ledger and accepted mechanics planner. The direct QTE damage adapter established by #1543 is only the canonical producer foundation; T042a/T047b independently own and verify its immutable deferred effect continuation. No effect-only adapter, live authority rebuild, random fallback, or legacy resource field is permitted; afterlife, projection, repair, and wound behavior remain independently owned by their recorded tasks and evidence.
- All gameplay remains local/offline; the feature introduces no cloud service, telemetry, or GitHub Actions dependency.

## 2026-09-05 — exact skill scope extension from #1536

GitHub issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536)
extends the completed #1535 common `roll_modifier` profile. Every current modifier now
has the closed three-field payload below; missing `scope` is invalid and never means
`all`.

```json
{
  "operations": ["skill_check"],
  "contribution": "disadvantage",
  "scope": { "kind": "skill", "skillId": "skill_lockpicking" }
}
```

The closed scope union is either `{ "kind": "all" }` or exactly
`{ "kind": "skill", "skillId": "<canonical-id>" }`; `kind=skill` is legal only
with `operations: ["skill_check"]`. Exact target-skill binding, derived
dormancy/reactivation, trusted roll-context filtering, treatment integration,
projection, lifecycle/replay/rollback coverage, repository-wide direct cutover, and
GM documentation are new #1536 work. Historical #1535 tasks remain complete and are
not reopened or unchecked by this dependent contract amendment.
