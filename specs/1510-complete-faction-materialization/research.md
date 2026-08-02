# Research: Complete Faction Materialization

## Scope and evidence

Research was performed against issue
[#1510](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1510),
the approved design, the current accepted-turn validation/repair pipeline, the
Actor Materialization implementation, Mortal faction normalizers and
validators, Shining request/receipt validators, and the bounded test-lane
policy.

No unresolved clarification remains. The decisions below are implementation
constraints, not optional suggestions.

## Decision 1: Reuse the Actor Materialization contract shape

**Decision**: Add `FactionMaterializationContract` as a pure, closed validator
with `FactionMaterializationFamily`, `FactionMaterializationEvidence`, exact
capability/section sets, duplicate-property detection, scalar validation,
disposition/evidence validation, and unique `materializationId` checks.

**Rationale**: `ActorMaterializationContract` already proves the desired
properties and has production-tested issue-code and repair-coordinate patterns.
Keeping a faction-specific contract avoids coupling actor and faction schemas
while preserving the same mental model.

**Rejected alternatives**:

- Extend `ActorMaterializationContract`: actor types and faction profiles have
  unrelated capability and section sets and would make both contracts harder to
  audit.
- Store a global faction-materialization registry: this creates a second source
  of identity truth and conflicts with the approved embedded-authority design.
- Accept open-ended capabilities: unknown keys would silently become
  unvalidated game mechanics.

## Decision 2: Validate raw state before semantic normalization

**Decision**: Extend `CollectAcceptedTurnRawStateIssuesAsync` with faction
materialization and `FactionCoreChanges` checks. Raw checks parse current and
validated pre-turn authority with duplicate-member detection, classify touches,
and validate full candidate bundles before `RefreshCanonicalStateAsync`.

**Rationale**: `ShiningAbodeState.NormalizeStateRoot` currently creates default
origin, charter, leadership, lifecycle, chronicle visibility, and strategic
memory containers. Final validation alone cannot distinguish authored semantics
from synthesized defaults.

**Rejected alternatives**:

- Tighten only post-normalization validation: it cannot prove authorship.
- Remove all normalization defaults globally: untouched legacy saves and
  client-owned projections would regress.
- Infer missing semantics from names, tags, or actor descriptions: this violates
  issue #1510 and makes genre vocabulary into hidden authority.

## Decision 3: Classify touches against validated pre-turn authority

**Decision**: Build exact, case-sensitive identity maps from duplicate-sensitive
validated pre-turn snapshots and classify each target as:
`new`, `legacy_promotion`, `already_materialized`, `client_derived_only`, or
`untouched_legacy`.

**Rationale**: Current full carriers accept several aliases and same-turn
temporary IDs. A permanent-looking ID, repeated display name, or normalized
object shape is not reliable evidence of newness.

**Rejected alternatives**:

- Use `isNewFaction` alone: it is GM-authored and can lie.
- Compare names: names are mutable and non-unique.
- Promote every legacy faction at load: this would fabricate semantic content
  and break backward compatibility.

## Decision 4: Keep one embedded immutable receipt

**Decision**: Every new/promoted canonical faction stores exactly one private
`materialization` object with schema version 1, stable ID, exact domain/type and
faction ID, accepted turn, `complete` state, closed capability snapshot, and
closed section dispositions. Once present in validated history, it is
semantically immutable.

**Rationale**: The receipt is historical proof of initial completeness, not a
live summary. Ongoing state belongs to canonical gameplay fields and narrow
commands.

**Rejected alternatives**:

- Recompute the receipt after each update: historical evidence would drift.
- Put reasons in visible gameplay state: private harness rationale would leak to
  players.
- Allow missing sections to mean empty: omission would again make hollow
  factions technically valid.

## Decision 5: Add explicit Mortal semantic fields

**Decision**: The full Mortal carrier gains:

- `purpose` and `currentAgenda` non-empty strings;
- `principles` as a non-empty unique string array;
- `memory` with `summary`, `lastUpdatedTurn`, `enduringFacts[]`, and
  `openThreads[]`;
- `governance` with `model` and `decisionProcess`;
- `leadership` with `leadershipState`, `summary`, and `leaderNpcIds[]`.

`leadershipState` is closed to `headed`, `collective`, and `vacant`.
`headed` requires at least one exact NPC ID; `collective` may use explicit NPC
IDs or a non-person collective described in `summary`; `vacant` requires an
empty ID list.

**Rationale**: Existing `description` and `developmentArchetype` prose do not
prove purpose, current intent, decision authority, or memory. Structured
leadership represents a real vacancy or collective without inventing a person.

**Rejected alternatives**:

- Treat `description` as all semantic fields: it cannot be validated or updated
  independently.
- Require one leader NPC for every faction: leaderless and distributed
  factions are valid.
- Add setting-specific ideology enums: the contract must remain
  setting-agnostic.

## Decision 6: Make `scribeChronicle` real creation authority

**Decision**: Preserve the existing creation field name `scribeChronicle`. A new
Mortal faction must supply at least one non-empty turn-prefixed entry. A legacy
promotion may rely on an existing canonical chronicle; if none exists, it must
supply at least one entry. The normalizer extracts these entries to
`faction_chronicles.json` with the exact faction ID and consumes the carrier
field. Later history still uses `factionChronicleUpdates`.

**Rationale**: `Rules/Block_21.txt` already reserves `scribeChronicle` for
creation and forbids resending it during ordinary updates, but the current
normalizer discards it. Repairing that missing bridge is smaller and clearer
than inventing a competing `initialChronicle` field.

**Rejected alternatives**:

- Use `factionChronicleUpdates` for same-turn new factions: current validation
  requires an existing permanent canonical faction.
- Keep the entry only inside faction core: it would duplicate chronicle
  authority.
- Add timestamps during normalization: nondeterministic timestamps make
  canonical equality and rollback harder; the accepted turn prefix is the
  stable anchor.

## Decision 7: Define exact Mortal empty surfaces without a schema-wide rewrite

**Decision**: The raw full carrier must explicitly contain every governed
surface. Canonical evidence is:

- hierarchy: exact faction-bound structure entry with mandatory governance,
  leadership, `ranks.branches=[]`, and `structuredBonuses=[]`;
- resources: exact faction-bound entry with `metaResources=[]` and
  `strategicGoods=[]`;
- relations: exact `relations=[]` on the canonical core;
- projects: explicit raw `activeProjects=[]` and `completedProjects=[]`, plus
  no canonical project rows for the faction;
- territory/influence: explicit `controlledTerritories=[]` and no conflicting
  location-control link;
- player membership: the exact non-member/non-player state;
- custom states: exact faction-bound custom entry with `customStates=[]`.

For sidecars whose current canonical schema has one root project collection
rather than one record per faction, the receipt plus explicit raw arrays and
absence of target rows is the canonical empty proof. This avoids a repository-
wide project-file migration unrelated to the feature.

**Rationale**: The repository already uses these file shapes. Requiring exact
raw carriers and exact post-normalization evidence distinguishes omission from
deliberate emptiness without creating a duplicate project index.

**Rejected alternatives**:

- Add a redundant project-state registry: it could diverge from actual project
  rows.
- Reshape all legacy project storage to per-faction objects: high migration
  risk with no additional gameplay authority.
- Let missing sidecars count as empty: this recreates the original defect.

## Decision 8: Add one closed `FactionCoreChanges` command

**Decision**: Add `factionCoreChanges` to `GameResponse` and map it to
`faction_core.json`. Each command has exact permanent `factionId`, non-empty
`reason`, and at least one closed absolute-value group:

- `profile`;
- `purposeAndPrinciples`;
- `progressionAndPower`;
- `governanceAndLeadership`;
- `playerMembership`;
- `relations`.

Unknown members are rejected recursively. Identity, `materialization`, raw
sidecar carriers, chronicles, location control, NPC affiliation, and fields
owned by existing rank/bonus/resource/project/custom commands are protected.
Successful commands are consumed; failed commands remain available for repair.

**Rationale**: `NPCCoreChanges` provides the established command pattern. Code
research also found that Mortal relations currently have no standalone
relation-change command. A closed `relations` group supplies narrow authority
without adding another top-level response surface. All commands that actually
exist today retain their authority.

**Rejected alternatives**:

- Continue accepting full existing faction objects: this permits unrelated
  history and receipt rewrites.
- Add a second new `FactionRelationChanges` response field: it increases the GM
  contract surface without gaining a separate canonical owner.
- Use JSON Patch: path expressions are harder to close recursively and audit in
  repair packets.

## Decision 9: Keep Shining route checks compositional

**Decision**: Faction Materialization adds completeness evidence around existing
native-discovery and founding request/receipt validation. It does not replace
their exact cost, count, reservation, or constrained-diff checks. Story/hidden
creation uses an explicit provenance object and exact story authority.

**Rationale**: Existing route validators already encode significant business
rules. Reimplementation would risk divergence and duplicate issue families.

**Rejected alternatives**:

- Treat every new Shining faction as generic: route-specific counts, costs, and
  ownership would be lost.
- Derive route from `originType` alone: origin is necessary but not sufficient
  proof of a request or story contract.

## Decision 10: Add explicit Shining provenance, agenda, visibility, and story authority

**Decision**: New/promoted Shining factions require:

- `creationProvenance` with route, authority type, and exact authority ID;
- `currentAgenda`;
- `visibility` closed to `revealed`, `rumored`, or `hidden`;
- `storyAuthority`, either explicit `null` for a non-story faction or a closed
  object containing `authorityType`, `authorityId`, and `factionRole`.

Native discovery binds to the core-action request/receipt. Player founding
binds to the pending founding request/receipt. Story/hidden creation binds to a
documented canonical story state such as Saref authority. Existing
guardian-derived `ascended_guardian` creation is treated as story/guardian
authority and must bind to the exact materialized Guardian actor rather than a
derived name. The closed Guardian form uses
`authorityType=guardian_ascension`, `authorityId=<exact guardianId>`,
`factionRole=patron_guardian`, `visibility=revealed`, and exact secure Guardian
leadership. Saref authority uses the exact canonical
`factionLinks.wingsFactionId`, which also equals the enclosing faction ID; it
does not depend on the later infiltration request and therefore works for
hidden pre-reveal materialization.

**Rationale**: Current `originType` and charter do not prove why a faction
exists, whether it may be hidden, or which contract owns its story state.

**Rejected alternatives**:

- Reuse only `sarefVisibility`: it is one story's field and cannot govern every
  Shining creation.
- Infer hidden status from faction names or archetypes: explicitly forbidden.
- Make `storyAuthority` optional: absence would again be ambiguous.

## Decision 11: Preserve only mechanical Shining normalization

**Decision**: For every faction carrying a receipt, normalization may
canonicalize already-present values, ensure explicitly client-owned containers, and compute
`factionStrength`, tier, and service multiplier. It may not create or replace
origin, charter specialization, leadership, lifecycle, agenda, visibility,
provenance, strategic-memory semantics, chronicle prose, story authority,
receipt, capabilities, dispositions, or reasons.

Untouched legacy factions retain current load compatibility. The raw classifier
determines when strict behavior applies.

**Rationale**: This removes semantic laundering without forcing eager migration.

**Rejected alternatives**:

- Globally delete all defaulting logic: legacy and local UI flows would break.
- Accept defaults but mark the receipt complete: a receipt would no longer
  prove GM-authored completeness.

## Decision 12: Reuse Actor Materialization for Shining people

**Decision**: Every new non-player head, political actor, and newly significant
resident must resolve through exact actor type/ID to complete Actor
Materialization authority. `player_soul` and vacant leadership keep their
existing explicit exceptions.

**Rationale**: Faction completeness must not be satisfied with identity-only
people. Issue #1500 already owns their semantic contract.

**Rejected alternatives**:

- Duplicate actor fields inside the faction: this creates conflicting
  authority.
- Require a faction-local resident roster: membership is resident-owned.

## Decision 13: Add a selectable validation phase

**Decision**: Add
`AcceptedTurnFactionMaterializationCompleteness` to
`GameStateValidationPhase`, include it in relevant profiles, call it from
`ValidationService.ValidationPhases`, and update selection/equivalence/boundary
tests. Raw pre-normalization validation remains a separate earlier fence.

**Rationale**: Canonical bundle and receipt continuity must be independently
selectable and visible in validation reports, as Actor Materialization is.

**Rejected alternatives**:

- Hide the work inside generic cross-reference validation: focused execution
  and diagnostics would be poor.
- Run the complete validation pipeline after every edit: contrary to the
  bounded-lane policy.

## Decision 14: Route repair by stable faction coordinate

**Decision**: Extend the existing validation repair harness with faction issue
recognition, exact target-file resolution, and bounded packets for
`mortal_faction:<id>` and `shining_faction:<id>`. Packets include preserved
sections and prohibited roots and never ask for an unrelated full-state rewrite.

**Rationale**: Strict validation is usable only if repair can target the exact
missing or contradictory authority.

**Rejected alternatives**:

- Route by display name: names are mutable and non-unique.
- Send the whole faction domain to repair: broad writes can destroy validated
  history.
- Implement #1222's content worker: outside #1510.

## Decision 15: Use a bounded verification ladder

**Decision**: Run focused tests after each RED/GREEN slice, one Fast checkpoint
after cross-domain stability, one FullValidation because afterlife docs change,
and one clean PreMerge immediately before integration. Do not widen timeouts,
increase concurrency, or append deep suites.

**Rationale**: The repository test policy was created specifically to prevent
hour-long routine feedback. The design-worktree baseline is already green at
2,633/2,633 Fast tests in 4:16.219.

**Rejected alternatives**:

- Run all integration tests after each change: blocks development.
- Skip FullValidation: afterlife contract documentation is explicitly changed.
- Repeat Fast directly before PreMerge: PreMerge already includes the fast
  project.
