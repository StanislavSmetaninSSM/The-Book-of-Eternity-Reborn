# Feature Specification: Authoritative Saref Story Materialization

**Feature Branch**: `1519-saref-story-materialization`

**Created**: 2026-08-10

**Status**: Draft

**Input**: Materialize the complete hidden `Крылья над Бездной` story authority: make the whole plot known to the GM before any story Guardian exists, bind ten Predvechnye Guardians and forty fixed story quests to exact canonical identities, keep those Guardians alive through renewable GM-authored non-story quests, and materialize Saref and the Wings of Angels as exact actor/faction entities.

## Source Issues & Scope *(mandatory)*

- **Source GitHub issue(s)**:
  - Epic [#1519](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1519)
  - Catalog and GM competence [#1520](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1520)
  - Guardian and quest materialization [#1521](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1521)
  - Saref and Wings materialization [#1522](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1522)
- **Issue type**: Epic with three implementation sub-issues; enhancement, story, afterlife hardening.
- **Spec Kit justification**: The feature spans multiple sessions and pull requests and changes hidden-story canonical state, actor and faction materialization, validation, normalization, GM prompt context, console/browser visibility, documentation, examples, manifests, and repair authority.
- **Contract scope**: Player-facing console/browser story and quest projections; GM-facing prompts; Mortal World, Chaos Sea, and Shining Abode runtime state; validation and normalization; actor/faction/quest authority; docs, examples, manifests, and source guards.
- **Out of scope**: GM workers including #1239; multiplayer/network play; generic actor materialization already completed by #1500; generic faction materialization owned by #1510; generic hall/Guardian-plane materialization owned by #1514; legacy-save compatibility; receipt-less state; runtime migration of old test fixtures. New gaps discovered outside these three sub-issues become linked follow-up issues rather than silently expanding an implementation child.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - GM Knows the Hidden Story from the First Turn (Priority: P1)

As the game-running GM, I know that the complete hidden Saref storyline exists, which Guardians participate, what their four quest lines do, and which revelations and advantages they unlock even when no story Guardian has yet been created. I can seed an appropriate hidden trace without inventing identities or rewards.

**Why this priority**: If the GM lacks the plot until an actor exists, the campaign can pass the relevant moments without ever discovering its intended hidden main line.

**Independent Test**: Start a current-schema game with no materialized story Guardian and prepare one GM turn in each realm. The GM context identifies all ten Guardians, all forty quest references, their final reward roles, and the rules for loading a relevant full package; a player-visible response does not reveal private story data.

**Acceptance Scenarios**:

1. **Given** a fresh game with no story actors or progress, **When** a Mortal World, Chaos Sea, or Shining Abode GM turn is prepared, **Then** the GM receives the complete compact story index.
2. **Given** one catalog-backed Guardian, quest, action, request, or latent trace is relevant, **When** the turn is prepared, **Then** the GM receives the exact full package for that line without receiving all ten full dossiers.
3. **Given** the compact index is present but a full quest package is not relevant yet, **When** the GM introduces a story seed, **Then** only a catalog-backed latent trace may be recorded; activation or completion waits for the exact full package.

---

### User Story 2 - A Story Guardian Materializes as a Complete Actor (Priority: P1)

As a player who selects, attracts, or encounters a Predvechnye Guardian, I meet the authored Guardian rather than a generic technical shell. The Guardian has a stable identity, complete presentation and agency, actor-owned memory, and the correct story connection. A trace found before the meeting is not lost.

**Why this priority**: The fixed plot cannot be trustworthy if its central actors are incomplete, interchangeable, or linked by approximate names.

**Independent Test**: Materialize each of the ten Guardians from the same fresh baseline and verify that each result is complete, exact, independently recognizable, memory-bearing, and linked to only its own four story quests. Repeat with a pre-existing latent trace and verify that the intended quest becomes available after the matching Guardian appears.

**Acceptance Scenarios**:

1. **Given** a valid selection, attraction, or story appearance for one registered Guardian, **When** materialization is accepted, **Then** the complete Guardian, matching afterlife actor profile, immutable materialization receipt, actor-owned memory, and story binding appear together.
2. **Given** one required part of that bundle is incomplete or refers to another identity, **When** acceptance is attempted, **Then** no part of the bundle becomes canonical.
3. **Given** a valid latent trace was found before the Guardian existed, **When** that exact Guardian materializes, **Then** the trace remains and the correct registered quest projection becomes available.

---

### User Story 3 - Complete Four Story Quests without Exhausting the Guardian (Priority: P1)

As a player, I can complete a Guardian's four authored Saref quests in order, including the playable fourth memory scene, and afterwards continue receiving new personal quests invented by the GM. The new quests are visibly and mechanically non-story quests and do not counterfeit Saref progress.

**Why this priority**: A permanent Guardian must remain a living actor after their fixed revelation; otherwise completing authored content turns a major relationship into an empty puppet.

**Independent Test**: Complete one full q1-to-q4 line, receive the exact registered revelation and advantage, then offer, accept, progress, hand in, and complete a new GM-authored non-story quest. Verify that the new quest changes ordinary Guardian/world state but leaves all Saref progress and deep-victory proof unchanged.

**Acceptance Scenarios**:

1. **Given** a registered story quest is recognized, **When** the player explicitly accepts and progresses it, **Then** one authoritative progress record drives matching player quest and Guardian-agency views.
2. **Given** quests 1 through 3 are complete and the player successfully completes the registered playable memory scene, **When** quest 4 closes, **Then** only that Guardian's registered revelation and advantage are granted.
3. **Given** quest 4 is complete, **When** the Guardian has a new goal, relationship need, project, political problem, or personal request, **Then** the GM may create a complete non-story quest and the player may run it through the ordinary lifecycle.
4. **Given** a non-story quest is active or completed, **When** story readiness is calculated, **Then** it cannot reveal Saref, grant a Saref advantage, satisfy a numbered story quest, or count toward deep victory.

---

### User Story 4 - Saref and the Wings Exist as Exact Hidden Entities (Priority: P2)

As a player progressing far enough in the hidden line, I can eventually encounter Saref and interact with the Wings of Angels as real, persistent, actionable entities. Before the intended reveal, the GM can reason about them while player-facing surfaces do not expose their private identity or materialization data.

**Why this priority**: The final storyline needs exact actor and faction authority, but it depends on the catalog and Guardian progression that make the reveal earned and coherent.

**Independent Test**: Validate a hidden-stage game containing complete Saref and Wings entities, confirm that player projections reveal neither, then execute the valid reveal transition and confirm that intended actionable information appears while private truth and receipts stay hidden.

**Acceptance Scenarios**:

1. **Given** a valid hidden-stage story bootstrap, **When** Saref and the Wings are materialized, **Then** Saref has exact identity `saref:saref_001` and the Wings have exact faction identity `shine_faction_wings_of_angels_001` with complete receipts and authority.
2. **Given** the reveal stage does not permit disclosure, **When** the player views quests, factions, actors, or status, **Then** no private Saref/Wings identity, catalog data, or materialization receipt is exposed.
3. **Given** the player completes a valid reveal route, **When** the reveal is accepted, **Then** the intended actor/faction information becomes actionable and all exact cross-links remain consistent.

### Edge Cases

- The packaged catalog is missing one Guardian, one quest, a fourth-quest reward, or contains duplicate or case-variant identities.
- The current game binds a different catalog version or digest than the loaded complete catalog.
- A story trace is found before its Guardian has ever been selected, attracted, or encountered.
- The GM attempts to progress a story quest without receiving its full relevant package in that turn.
- A quest is completed out of order, activated without player acceptance, or quest 4 lacks successful memory-scene proof.
- One projected quest view can be written while another required view is malformed or unavailable.
- A catalog quest is labeled non-story, or a GM-authored quest uses a catalog identity or claims story scope.
- Story and non-story quests coexist under an ordinary Guardian quest cap or difficulty ceiling.
- All four fixed quests are complete and the Guardian currently has no available quest; future non-story quest eligibility must remain intact without requiring an always-nonempty list.
- A Guardian already exists when a later story trace is discovered; the existing materialization receipt must not be rewritten.
- Saref is confused with a `saref_agent`, or the Wings faction link differs by case or identity from the story authority.
- A repair proposal changes another Guardian, another actor, unrelated story state, or an unrelated faction.
- Console and browser receive the same reveal state but one surface attempts to show raw enum, receipt, or private catalog terminology.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The game MUST provide one immutable, versioned, all-or-nothing authoritative catalog for the complete `Крылья над Бездной` line.
- **FR-002**: The catalog MUST contain exactly the ten approved Predvechnye Guardians and exactly four fixed story quests for each Guardian.
- **FR-003**: Every Guardian, story quest, revelation, advantage, Saref actor, and Wings faction identity MUST be unique, stable, exact, and case-sensitive.
- **FR-004**: The GM MUST receive a compact index of all ten Guardians, forty story quests, final reward roles, and reveal-route rules for every Mortal World, Chaos Sea, and Shining Abode turn before any story actor exists.
- **FR-005**: A relevant Guardian, quest, trace, action, or request MUST cause the GM to receive the exact full content package for that line without requiring every full line in every turn.
- **FR-006**: A GM turn without the exact full quest package MAY create only a registered latent trace and MUST NOT activate, progress, complete, or reward that story quest.
- **FR-007**: Every current-schema New Game MUST bind its empty hidden-story state to the exact current catalog identity, version, and content digest.
- **FR-008**: Missing current-schema story state, missing catalog binding, or a binding mismatch MUST NOT receive a legacy fallback or runtime migration.
- **FR-009**: The ten canonical Guardian identities MUST be `guard_system_<preset>_001` for the approved preset names Azalia, Brann, Elyara, Ilarion, Lissara, Lucian, Myriel, Seret, Varak, and Veyra.
- **FR-010**: Each approved Guardian MUST have one complete authored materialization definition covering identity, presentation, personality, worldview, motivation, goals and plan, authored abilities, relationship posture, capabilities, intentional empty sections, and story binding.
- **FR-011**: A Guardian materialization MUST create the complete Guardian, matching afterlife actor profile, immutable actor receipt, actor-owned memory, and exact story/location references as one accepted outcome.
- **FR-012**: If any required Guardian materialization component is invalid, missing, ambiguous, or bound to another identity, the entire outcome MUST be rejected without partial canonical state.
- **FR-013**: A latent trace for an unmaterialized Guardian MUST survive until that exact Guardian appears and MUST then resolve to only the registered quest it references.
- **FR-014**: Fixed story quest definitions MUST remain immutable catalog authority while the hidden-story state remains the single mutable authority for their progress.
- **FR-015**: Player quest views and Guardian-agency quest views for a fixed story quest MUST be derived consistently from the one accepted story progress transition.
- **FR-016**: Fixed story quest progression MUST follow the strict `absent/undiscovered -> latent -> recognized -> active -> ready-to-turn-in -> completed` lifecycle with no skipped stage, except that quest 4 completes from `active` only through the successful composite memory-scene contract; explicit player acceptance is required before active play.
- **FR-017**: Official completion for each Guardian MUST follow quest 1, then quest 2, then quest 3, then quest 4; future traces may exist early but cannot satisfy an earlier missing completion.
- **FR-018**: Quest 4 MUST close only through a successful playable `Воспоминание` scene with non-physical evidence and MUST grant only its registered revelation and advantage.
- **FR-019**: Every current-schema Guardian quest snapshot and derived Guardian-profile quest view MUST declare exactly one `storyScope`: `saref_main_story` or `non_story`. Console and browser MUST visibly mark `non_story` quests with an in-world Russian label equivalent to `Несюжетный квест` without exposing the raw enum.
- **FR-020**: The forty catalog quests MUST use `storyScope=saref_main_story`; only those quests may update numbered Guardian story progress, Saref revelations or advantages, reveal readiness, or deep-victory Guardian proof.
- **FR-021**: The GM MUST be able to author complete `storyScope=non_story` Guardian quests whenever ordinary Guardian quest rules allow, both before and indefinitely after quest 4 completion.
- **FR-022**: A GM-authored non-story quest MUST have a stable unique identity, title, description, objective or success authority, difficulty, reward outline, ordinary origin, and grounding in current actor or world state.
- **FR-023**: Non-story quests MUST obey existing Guardian quest caps, difficulty ceilings, player acceptance, Mortal progress, and afterlife hand-in rules.
- **FR-024**: A non-story quest MUST NOT enter numbered Saref progress, use a fixed catalog quest identity, change the reveal stage, grant Saref rewards, or count toward deep victory.
- **FR-025**: Completion of the fourth fixed quest MUST NOT disable the Guardian's goals, agency, memory, ordinary quest generation, projects, politics, relationships, or future interactions.
- **FR-026**: Saref himself MUST use exact identity `actorType=saref`, `actorId=saref_001`; `saref_agent` MUST remain reserved for agents and supporters.
- **FR-027**: Saref MUST have a complete actor receipt, appearance and profile summary, personality and worldview, motivation, goals and agency, abilities, relationships, public masks, private truth, current realm/location, and actor-owned memory.
- **FR-028**: The Wings of Angels MUST use exact faction identity `shine_faction_wings_of_angels_001`, the approved story-creation authority, and a complete immutable faction receipt.
- **FR-029**: The Wings MUST have complete charter, lifecycle, leadership, hall/location reference, strategic memory, chronicle, relationships, and all required capability dispositions.
- **FR-030**: Saref, Wings, story authority, leadership, visibility, provenance, and Guardian/quest reward links MUST agree exactly across all canonical records.
- **FR-031**: The GM MUST be able to reason about Saref and the Wings from the catalog before reveal, while ordinary player projections MUST expose only information permitted by the current reveal stage.
- **FR-032**: Console and browser MUST apply the same reveal and quest-classification semantics and MUST use player-facing Russian labels instead of raw internal enum or receipt terminology.
- **FR-033**: Validation MUST reject incomplete, duplicate, ambiguous, forged, unknown, case-variant, receipt-less, out-of-order, scope-mismatched, or cross-linked story state before it is normalized.
- **FR-034**: A failed multi-record story, Guardian, Saref, Wings, or quest projection MUST leave every affected canonical record at its prior accepted value.
- **FR-035**: Repair instructions MUST name only the exact affected catalog binding, story state, Guardian, actor profile, actor memory, location reference, or Wings faction target and MUST protect all unrelated records.
- **FR-036**: Full Guardian planes and story halls MUST use the shared location authority established by #1514 rather than introduce a private duplicate location model.
- **FR-037**: GM prompts, Mortal and afterlife rules, contract documentation, worked examples, validation manifests, daemon entrypoints, and documentation/source guards MUST be synchronized with every new or changed story contract.
- **FR-038**: Worked examples MUST cover an early pre-Guardian latent trace, Guardian materialization, first quest activation, fourth-quest memory closure, hidden and revealed Saref/Wings materialization, and a post-q4 non-story quest.
- **FR-039**: The feature MUST NOT add GM workers, multiplayer/network behavior, legacy-save compatibility, receipt-less compatibility, or runtime migration machinery.

### Key Entities

- **Saref Story Catalog**: Immutable complete definition and compact index of the hidden line, its participants, fixed quests, reveal routes, and registered rewards.
- **Catalog Binding**: Current-game reference to one exact story catalog identity, version, and digest.
- **Predvechnye Guardian Template**: Authored definition that can materialize one exact permanent Guardian and matching afterlife actor authority.
- **Guardian Story Quest Template**: One of forty immutable numbered quests, with exact Guardian, ordinal, narrative boundaries, evidence, reveal category, and final reward when applicable.
- **Guardian Story Progress**: The single mutable record of discovered, recognized, accepted, ready, and completed fixed quests.
- **Guardian Quest Projection**: Player and Actor Brain views derived from accepted story progress rather than independently authored copies.
- **Non-Story Guardian Quest**: Renewable GM-authored quest explicitly isolated from Saref progression while using the ordinary Guardian lifecycle.
- **Saref Actor**: The unique principal story actor, distinct from Saref agents and materialized with complete private/public authority.
- **Wings of Angels Faction**: The unique hidden Shining faction linked to Saref story authority and later revealed as an actionable faction.
- **Latent Trace**: A catalog-backed story clue that may exist before its Guardian is materialized without prematurely completing or activating a quest.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: In 100% of tested GM turns across all three supported realms with no story actor present, the GM receives the complete compact index of 10 Guardians and 40 fixed quests.
- **SC-002**: Catalog integrity checks accept exactly 10 Guardian templates and 40 quest templates and reject every tested missing, duplicate, case-variant, or mismatched entry.
- **SC-003**: All 10 Guardians can independently materialize with complete exact actor authority and zero partial canonical writes in negative tests.
- **SC-004**: All 40 fixed quests can follow their valid lifecycle, and every tested out-of-order, unaccepted, forged-reward, or scope-mismatched transition is rejected.
- **SC-005**: After each of the 10 Guardians completes quest 4, at least one complete GM-authored non-story quest can be offered and completed without changing any Saref progress or reward count.
- **SC-006**: Hidden-stage player checks across console and browser expose zero private Saref/Wings identities, catalog payloads, prompt instructions, or materialization receipts.
- **SC-007**: A valid reveal makes the intended Saref/Wings information actionable while 100% of exact actor, faction, leadership, visibility, and story links remain consistent.
- **SC-008**: Every tested failure during a multi-record materialization or quest projection preserves all prior accepted canonical values.
- **SC-009**: Every changed GM-facing contract has at least one matching worked example and passes the repository's documentation and source-guard verification.
- **SC-010**: The feature completes through three independently reviewable GitHub sub-issues before the Saref GM worker begins, with no multiplayer or legacy compatibility scope added.

## Verification Plan *(mandatory)*

- **C# verification**: During each child, use `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~SarefMainStoryStateValidationTests|FullyQualifiedName~ActorMaterializationContractTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"`, followed by one meaningful `Fast` checkpoint and one final `PreMerge` for that independently merged child. Run `LifecycleIntegration` for all three children because New Game, accepted-turn, materialization, and pending/control boundaries change; run the related `DeepValidation` Guardian matrix for #1521.
- **Documentation/contract verification**: Use a focused `AfterlifeDocumentationCoverageTests` selection and `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation` whenever afterlife prompts/docs/examples/manifests change. Validate worked examples for all required flows.
- **Frontend verification**: If raw story-scope or reveal behavior changes browser-visible quest/faction/actor views, run `npm run verify` from `BookOfEternityClient.WebFrontend/` and focused parity tests; otherwise record the explicit no-frontend-change rationale.
- **Manual/player-facing verification**: Inspect fresh no-Guardian GM context, a pre-Guardian latent trace, each Guardian materialization route, one complete q1-to-q4 path, a post-q4 non-story quest, hidden Saref/Wings views, and the revealed actor/faction flow in both console and browser where exposed.

## Assumptions

- The game has not shipped; only the current schema and updated repository fixtures need to work.
- The approved permanent story cast is exactly Azalia, Brann, Elyara, Ilarion, Lissara, Lucian, Myriel, Seret, Varak, and Veyra.
- Existing rich Guardian dossiers and four-quest Markdown bibles remain the authored narrative source to structure and validate, not disposable legacy content.
- Existing ordinary Guardian quest mechanics remain available and are the correct lifecycle for renewable non-story quests.
- Generic Actor Materialization from #1500 remains authoritative and is extended only with the new exact Saref actor type and Guardian story templates.
- Generic Faction Materialization from #1510 supplies the Wings story route and receipt contract.
- Generic hall and Guardian-plane materialization from #1514 supplies location authority referenced by this feature.
- The compact all-realm story index is acceptable GM-private context; player-facing no-spoiler filtering remains mandatory even though accidental player discovery is not the primary product risk.
- The three child issues share this epic Spec Kit feature and receive explicit task/phase mapping before any child implementation begins.
