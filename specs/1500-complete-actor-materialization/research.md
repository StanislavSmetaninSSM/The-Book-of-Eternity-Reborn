# Research: Complete Actor Materialization

## Decision 1: A private envelope, not client-generated actor content

**Decision**: Add a `materialization` object to first canonical Mortal NPC objects and common afterlife entity profiles. It records exact actor binding, version, turn, explicit capabilities, and the disposition of each governed section.

**Rationale**: Existing shape validators can prove that arrays exist but cannot distinguish an intentionally itemless or questless actor from a GM that skipped work. A structured disposition makes intent machine-checkable without asking the client to invent narrative data.

**Rejected alternatives**:

- Require every array to be non-empty. This forbids legitimate non-combatants, itemless actors, and actors without current quests or Fate Cards.
- Infer intent from occupation, name, history, tags, or descriptions. The Mortal World can use any setting and no finite keyword map is authoritative.
- Generate missing content in the normalizer. This transfers semantic authority from the GM to deterministic client code.

## Decision 2: Exact actor binding prevents copied metadata

**Decision**: The envelope carries `actorType` and `actorId`; validation compares both with canonical identity. `materializationId` is stable and unique within the validated actor set.

**Rationale**: A generic complete envelope could otherwise be copied between actors and still satisfy structural validation. Exact binding also gives repair packets a stable target.

## Decision 3: Pre-turn authority distinguishes legacy from new actors

**Decision**: Require the current contract when an actor is created or promoted in the accepted turn, determined from validated pre-turn snapshots and structured current-turn commands. If an envelope is present in any file, validate it regardless of age. Untouched baseline actors without envelopes remain loadable.

**Rationale**: ID shapes and file paths cannot reliably distinguish old from new data. Blanket migration would either break saves or force the client to invent data.

**Promotion triggers**: first common afterlife profile, teacher/mentor enablement, merchant/trade enablement, combat capability enablement, persistent Actor Brain scope, supported political/leadership assignment, or equivalent significant role.

## Decision 4: Common semantics with type-specific sections

**Decision**: Use one parser and invariant vocabulary, with separate allowed section sets.

- Mortal: `skills`, `inventory`, `fateCards`, `personalQuests`, `relationships`.
- Afterlife: `standardArts`, `specialArts`, `customStates`, `fateCards`, `relationships`, `agency`, `progressionHistory`.

**Rationale**: Mortal inventory is not a valid afterlife possession model. A single universal list would either weaken validation or create invalid cross-realm state.

## Decision 5: Capabilities cross-check existing canonical authority

**Decision**: Capabilities never create data. `canFight`, `canTeach`, `canTrade`, and Mortal-only `ownsItems` must agree with existing skills/arts, teacher/mentor, trade, inventory, and equipment surfaces.

**Rationale**: The contract should catch contradictions while preserving existing gameplay systems and update commands.

## Decision 6: Bounded harness repair

**Decision**: Add dedicated materialization issue codes and a repair packet that names the exact actor, missing/contradictory sections, allowed states, and target files. It explicitly forbids deleting valid sections or rewriting unrelated actors.

**Rationale**: Generic shape errors produce broad repair loops. A bounded packet lets the GM repair only what the validator can prove is incomplete.

## Decision 7: Deterministic client-owned System Guardian seed

**Decision**: `SystemGuardianLibraryService` emits a deterministic afterlife envelope for fresh-game System Guardians. It uses canonical preset identity and existing seeded profile content; it does not synthesize prose.

**Rationale**: This actor is already client-owned. Requiring the GM to rematerialize it would duplicate authority and make New Game brittle.

## Decision 8: Exact-byte compare/exchange owns canonical repair writes

**Decision**: Validation-repair tasks pin exact file bytes with SHA-256 hashes. The apply gate and rollback path use one cross-process canonical-write lock plus exact-byte compare/exchange; a write proceeds only when current bytes still equal the task or rollback baseline. Proposal content is bound to `worker_proposals/<proposalId>/<canonicalPath>` and its own exact after hash.

**Rationale**: Semantic JSON comparison alone cannot prevent a concurrent daemon, helper, or external GM writer from being silently overwritten between validation and atomic replacement. Exact-byte CAS makes stale proposals and stale rollback ownership mechanically unrepresentable.

## Decision 9: Worker repair ownership precedes legacy fallback

**Decision**: Clear stale repair artifacts, attempt the worker first, and expose the legacy main-GM request only when worker dispatch/apply does not succeed. An accepted apply-gate decision remains authoritative if ready publication fails; the client records that terminal state and revalidates directly. Output freshness receives an explicit repair timestamp rather than discovering it from a request file.

**Rationale**: Publishing the legacy request before worker completion creates two possible repair owners. Treating a post-apply ready failure as worker failure can cause a second GM to overwrite an already accepted repair.

## Decision 10: Client-owned seeds project exact capability authority

**Decision**: The deterministic System Guardian envelope copies capability truth from the exact seeded current Guardian/Abode authority. In particular, the active Guardian of the current Chaos Sea abode declares `canTrade=true`; no role-name or prose inference is involved.

**Rationale**: A client-owned seed is exempt from GM authorship, not from capability consistency. Hardcoding a contradictory capability would make a fresh valid seed fail the same contract imposed on GM-authored actors.

## Decision 11: Audit telemetry cannot revoke canonical authority

**Decision**: Worker audit append performs its read-and-append while holding the shared cross-process canonical-write lock. I/O failure is contained as best-effort telemetry failure after argument validation and never rolls back an already accepted canonical apply.

**Rationale**: Audit records must not lose concurrent events, but observability is not the owner of canonical state. Rolling back accepted bytes because telemetry publication failed creates a second, unsafe authority transition.

## Decision 12: Terminal worker failures are never apply candidates

**Decision**: Only `status=completed` proposals can enter the apply gate. `failed`, `timed-out`, and `rejected` proposals must use `changedFiles: []` and are retained only as diagnostics.

**Rationale**: A terminal failure carrying mutations is contradictory. Rejecting that shape in both contract validation and the apply gate makes accidental application impossible even when one boundary is called directly.

## Decision 13: Legacy promotion inventory is continuity evidence

**Decision**: Retain the validated canonical pre-turn inventory JSON for each Mortal identity. A complete legacy promotion may include its schema-required inventory only when that array is semantically identical to the retained snapshot; new identities may still provide their complete initial inventory, and all actual existing-identity mutations remain dedicated-command-only.

**Rationale**: Promotion needs a complete object but must not become an alternate inventory mutation channel. Comparing structured pre-turn authority preserves both requirements without inventing content or parsing prose.

## Decision 14: Mortal characteristics are open-vocabulary but non-empty

**Decision**: Require at least one numeric property in a complete Mortal `characteristics` object while leaving property names entirely setting-defined.

**Rationale**: `{}` is not meaningful actor materialization, but a fixed characteristic dictionary would make the supposedly setting-agnostic contract depend on one world's vocabulary.

## Decision 15: First Guardian journal creation is one narrow Add

**Decision**: When the exact Guardian thought journal is absent, treat it as `{ "entries": [] }` only for memory-missing issues routed to one exact `guardian:<id>`. Reuse append-only normalization and the ordinary Add/hash/content-reference gate; wrong owners, extra roots, unrelated issues, rewrites, and extra entries remain rejected.

**Rationale**: The canonical journal has a safe empty state, but a general missing-baseline exception would bypass protected-data preservation for unrelated files.

## Decision 16: Audit identity is independent of clock granularity

**Decision**: Generate readable worker audit IDs from the UTC millisecond timestamp plus a GUID suffix.

**Rationale**: The timestamp remains useful during inspection, while uniqueness no longer depends on scheduler timing or call serialization.

## Decision 17: Metadata remains private

**Decision**: Console and browser projections continue to render gameplay fields and ignore `materialization`. Source/projection tests guard against exposing contract tokens in player mode.

**Rationale**: Schema versions and disposition states are harness data, not in-world information.

## Decision 18: Mortal continuity uses one effective identity

**Decision**: Resolve a current Mortal actor from its canonical permanent ID or exact same-turn `initialId`, reject any `initialId` that collides with a validated pre-turn permanent `NPCId`, and run inventory continuity against that same effective ID.

**Rationale**: Identity and inventory gates must not disagree about whether one payload is new. A null permanent field cannot be a way to bypass existing-actor mutation rules.

## Decision 19: Memory repair routing is actor-type-aware

**Decision**: Route Guardian issues to the canonical/supported Guardian journal, resident issues to resident state/journal, and all common-profile actor types to the exact profile. Common-profile repair may remove only the exact actor's `gmThoughtsSummary` from preservation comparison.

**Rationale**: Validation issues originate in type-specific source files while the repair authority may live elsewhere. Path-prefix matching alone silently disables preservation for Radiant and Saref repairs.

## Decision 20: Proposal status has no successful default

**Decision**: Reserve enum zero for `Unspecified`, require the JSON member, and reject unspecified/unknown values in contract validation. Explicit `completed` remains applyable; explicit terminal statuses remain mutation-free diagnostics.

**Rationale**: Missing protocol data must fail closed. Deserialization must never upgrade omission into permission to store or apply canonical changes.

## Decision 21: First envelope is the afterlife memory boundary

**Decision**: When an already bound legacy profile gains its first envelope, add it to accepted-turn required bindings and validate actual actor-owned memory. Do not retroactively require memory from an unchanged envelope-free legacy profile.

**Rationale**: Binding existence proves identity, not memory. The envelope transition is the precise compatibility boundary already used for new profiles and promotions.

## Decision 22: One generator owns all audit event identities

**Decision**: Centralize the readable UTC-millisecond plus GUID format in one generator, migrate every dispatch/proposal/apply/repair producer, expose a deterministic overload for tests, and source-guard the prefix outside that utility.

**Rationale**: A collision-safe format is ineffective when only one producer uses it; central ownership prevents timestamp-only and GUID-only variants from returning.

## Decision 23: Duplicate-key continuity authority fails before comparison

**Decision**: Detect duplicate JSON members recursively in current actor authority and reject duplicate/malformed values before order-insensitive semantic comparison. Convert current and validated pre-turn failures into structured validation issues rather than allowing `JsonNode` duplicate-key exceptions to escape.

**Rationale**: Catching only one parser exception can still accept duplicate new-actor data or leave another reparse path throwable. Duplicate-free validation is the prerequisite for meaningful semantic equality.

## Decision 24: Mortal continuity repair has per-code mechanical policy

**Decision**: Keep identity collision on the main-GM rollback/repair path. Dispatch legacy-promotion inventory repair only when the issue carries the exact validated pre-turn JSON-array snapshot, and permit only exact snapshot restoration. Permit empty-characteristics repair only as a non-empty setting-defined numeric object on the exact actor/carrier. Preserve every sibling, other actor, and root value through comparison.

**Rationale**: Adding production codes to a broad repair-code set without target normalization silently grants whole-file rewrite authority. Some identity corrections can have cross-file consequences and are not safely worker-representable.

## Decision 25: Generated characteristic examples are authority-neutral

**Decision**: The generated Mortal NPC template uses one `setting_defined_characteristic_key` placeholder and tells the GM to copy actual keys from current-world canonical authority. The setting-specific worked example uses world-specific keys rather than a universal tabletop list.

**Rationale**: A fixed list in an authoritative minimal template contradicts the open-vocabulary validator and steers non-fantasy worlds toward fabricated mechanics.

## Decision 26: Inventory repair guidance classifies actor lifecycle

**Decision**: The high-priority repair packet distinguishes genuinely new initial inventory, ordinary existing updates, and true legacy promotions. For an ordinary existing actor it removes the whole full-object resend and re-authors every supported change through dedicated delta/command surfaces; absence of a required delta surface forces main-GM rollback/repair. It never removes only `inventory` while retaining a schema-invalid full object. True legacy promotions retain inventory and restore the exact validated pre-turn snapshot.

**Rationale**: An absolute instruction to remove inventory from every existing actor makes a schema-valid legacy promotion impossible to repair, while removing only that property from an ordinary retained full object violates the full-object schema. Lifecycle classification must choose one valid authority path rather than create a second validation failure.

## Decision 27: One bounded command owns ordinary-existing core mutations

**Decision**: Add exactly one Mortal-only non-carrier response command named `NPCCoreChanges`. It targets exact existing permanent identity, requires a reason and one closed mutation group, carries absolute resulting values, rejects protected/unknown members recursively, updates every unambiguous canonical mirror, and is consumed only after successful reduction. Dedicated commands retain their existing domains.

**Rationale**: Authoritative rules require existing worldview, race/history, location, progression, setting-owned characteristic, faction-affiliation, and locked/unrealized Fate Card definition changes, but complete `UpdateNPCs` objects conflict with inventory continuity and skeletal patches conflict with full-object shape. A closed command preserves those mechanics without reopening a generic full-object or JSON Patch bypass.

## Decision 28: Carrying and progression are setting-owned authority

**Decision**: Characteristic keys, carrying-capacity formulas/inputs/units, level thresholds, and characteristic grants come only from explicit current-world authority. If no carrying authority exists, `maxWeight` and compatible weight totals remain nullable setting-owned results. If no characteristic grant exists, progression preserves characteristics rather than inventing points or class-stat assumptions.

**Rationale**: A fixed characteristic vocabulary or class allocation contradicts the open-vocabulary validator and fails in valid science-fiction, historical, modern, or custom worlds. Null is more truthful than fabricated mechanical authority.

## Decision 29: First-materialization personality is complete and honestly validated

**Decision**: Current Mortal first materialization requires 3-5 personality traits, each with mandatory integer `value` in the inclusive range 1-10. The canonical worked NPC enters the production `ValidationService.ValidateResponse` -> `ValidateNpcContract` path. Manifest metadata names that route and its focused-fragment limit; separate `NPCCoreChanges` command fragments carry an explicit focused-fragment rationale rather than claiming accepted-turn reducer execution.

**Rationale**: The prior one-trait/value-less example contradicted Block 19, while a narrow envelope helper could not prove full-NPC shape. Explicit route and coverage-limit metadata prevents descriptive examples from being mistaken for runtime scenarios.

## Decision 30: Third re-review command/docs wave is Mortal-only

**Decision**: R3-I-2, R3-I-3, and R3-M-1 change Mortal response, rules, examples, validation, and normalization only. They do not change any Chaos Sea or Shining Abode pending/control file, action type, response field, receipt, report, profile schema, scheduler, lifecycle mode, or authority path.

**Rationale**: `NPCCoreChanges` maps only to `game_state/npcs/npc_core.json`; setting-neutral Mortal characteristics and Mortal personality shape do not alter afterlife actor profiles. Updating the afterlife contract matrix/examples would falsely imply an afterlife schema change. Mandatory afterlife documentation tests remain a no-drift verification gate.

## Existing integration findings

- Mortal `ValidateNpcCoreObjectShape` already requires broad field presence but permits empty arrays.
- `NPCsInScene` and `UpdateNPCs` share full object validation and exact validated pre-turn inventory continuity. Historical scene objects are retained presence, not an alternate mutation carrier.
- `NPCCoreChanges` is a non-carrier input to the NPC core validator/normalizer and is absent from successfully normalized canonical state.
- Common afterlife profiles require identity, currencies, progression, arts, strategy, and ledger, while several actor-rich sections remain optional.
- Guardians have a strong type-specific dossier; residents and Shining leadership use separate files and require cross-file binding to common profiles.
- The accepted-turn validator already has validated pre-turn authority and Actor Brain memory checks suitable for current-turn classification.
- Existing normalizers preserve unknown JSON properties; tests will prove envelope preservation and prohibit envelope invention.

## Open questions resolved by the specification

- **Must every actor own items or Fate Cards?** No. Every section must be addressed, not populated.
- **Does this replace dedicated NPC delta commands?** No. The envelope is first-materialization authority only, and `NPCCoreChanges` owns only the closed existing-core gaps that had no dedicated route.
- **Does every mentioned name become an actor?** No. Persistent/significant canonical participation is the boundary.
- **Do afterlife actors receive Mortal inventory?** No.
- **Can a prompt-only solution suffice?** No. The class of error is machine-detectable and therefore belongs in the harness first, with prompts updated afterward.
