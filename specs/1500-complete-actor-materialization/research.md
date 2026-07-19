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

## Decision 10: Metadata remains private

**Decision**: Console and browser projections continue to render gameplay fields and ignore `materialization`. Source/projection tests guard against exposing contract tokens in player mode.

**Rationale**: Schema versions and disposition states are harness data, not in-world information.

## Existing integration findings

- Mortal `ValidateNpcCoreObjectShape` already requires broad field presence but permits empty arrays.
- `NPCsInScene` and `UpdateNPCs` share full object validation; existing NPC inventory resends are already forbidden.
- Common afterlife profiles require identity, currencies, progression, arts, strategy, and ledger, while several actor-rich sections remain optional.
- Guardians have a strong type-specific dossier; residents and Shining leadership use separate files and require cross-file binding to common profiles.
- The accepted-turn validator already has validated pre-turn authority and Actor Brain memory checks suitable for current-turn classification.
- Existing normalizers preserve unknown JSON properties; tests will prove envelope preservation and prohibit envelope invention.

## Open questions resolved by the specification

- **Must every actor own items or Fate Cards?** No. Every section must be addressed, not populated.
- **Does this replace dedicated NPC delta commands?** No. The envelope is first-materialization authority only.
- **Does every mentioned name become an actor?** No. Persistent/significant canonical participation is the boundary.
- **Do afterlife actors receive Mortal inventory?** No.
- **Can a prompt-only solution suffice?** No. The class of error is machine-detectable and therefore belongs in the harness first, with prompts updated afterward.
