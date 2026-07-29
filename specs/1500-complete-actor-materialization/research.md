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

## Fourth re-review hardening decisions

**Decision**: Parse current `npc_core.json` once through duplicate-sensitive production validation and report malformed, non-object, or duplicate-member authority as blocking structured issues before any command materialization.

**Rationale**: A malformed command carrier is itself invalid authority. Treating it as “no command” permits silent bypass, while materializing duplicate members through `JsonNode` can throw outside the prior narrow catch.

**Decision**: Compare every actor-owned field of a historical full carrier against validated pre-turn authority, leaving actual mutations to the closed dedicated command contracts. Preserve specialized inventory/materialization diagnostics where they provide narrower repair coordinates.

**Rationale**: A list of selected protected fields inevitably leaves future and existing actor domains open. Continuity is safer as a fail-closed ownership boundary than as an expanding denylist.

**Decision**: Reuse the canonical production Fate Card/skill/combat validator before reducing `fateCardsToAdd`, and recognize combat skills from canonical structured content rather than optional IDs.

**Rationale**: A normalizer must never consume a command that creates state rejected by the ordinary production validator. Block 7 makes skill IDs optional, so capability and promotion logic must follow the canonical skill contract.

## Fifth re-review hardening decisions

### Decision 31: Characteristic repair consumes explicit setting authority

**Decision**: Add `game_state/misc/characteristics.json` to every empty-characteristics worker task as exact hash-pinned read-only context. It is never an allowed proposal path. Dispatch and apply fail closed when the authority cannot be parsed uniquely, and preservation accepts only finite numeric keys already present in that authority.

**Rationale**: Checking only that the repaired object is non-empty and numeric allows a worker to invent a setting vocabulary that the ordinary NPC shape validator cannot distinguish from authored authority.

### Decision 32: Authoritative templates are executable minimal examples

**Decision**: Every GM-facing and Spec Kit `NPCCoreChanges` template contains one concrete non-empty mutation group and omits all unused groups. Tests extract the exact supplied template and run it through the production command validator/reducer fixture.

**Rationale**: Optional groups are closed commands, not form fields. Publishing empty arrays teaches the GM to produce a response that the runtime necessarily rejects.

### Decision 33: Output freshness follows canonical mutation

**Decision**: Resolve the repair boundary from actual writes to issue-targeted canonical files after repair succeeds. Retain the original canonical target set through every derived output-only retry. Require player-facing output to be strictly newer than every retained target's latest actual write; equality is stale. If an original target is rewritten again, recompute the boundary and re-stale earlier output. Fail closed when a required target mutation cannot be observed.

**Rationale**: Repair start time precedes both narrative and canonical writes, so it cannot establish that the narrative describes the accepted canonical state.

### Decision 34: Phase 17 changes no realm-specific state contract but updates shared afterlife guidance

**Decision**: Synchronize the shared daemon, repair guide, daemon specification, worked main-turn example, `OtherGuides/Afterlife_Contract_Matrix.md`, and `Examples/E_CLI_Afterlife_Turns.txt`. Keep afterlife pending/control manifests and realm-specific state schemas unchanged.

**Rationale**: The Phase 17 fixes constrain worker-only Mortal characteristic vocabulary, repair an existing Mortal `NPCCoreChanges` example, and harden the shared repair/output ordering. They add no Mortal or afterlife GM-authored state field, action type, receipt, scheduler contour, normalizer side effect, or realm-specific authority path. Because the shared lifecycle also governs afterlife repairs, the applicable matrix and worked example still document canonical-write-before-output ordering; adding a new afterlife state-contract row or manifest entry would misrepresent the runtime surface.

## Sixth review hardening decisions

### Decision 35: Afterlife repair realm is derived from pinned Soul authority

**Decision**: Every afterlife or mixed validation-repair worker task includes the exact canonical bytes and SHA-256 of `game_state/meta/soul_state.json` as read-only context. The client strictly parses one unambiguous supported `currentRealm`, derives the worker realm contract from those bytes, excludes the authority path from proposal scope, and verifies the same bytes and realm immediately before apply.

**Rationale**: Caller-supplied realm labels can drift from canonical state, and an after-dispatch realm change can turn an otherwise bounded proposal into a wrong-realm write. Making realm authority explicit and immutable closes both ambiguity and TOCTOU paths without asking the worker to reason about them.

### Decision 36: Changed-file operation is explicit closed authority

**Decision**: `WorkerFileChangeKind` has an invalid `Unspecified=0` state. Contract validation accepts exactly `Add`, `Replace`, or `Delete` and rejects omitted, zero, or undefined numeric values before any file-existence or hash semantics run.

**Rationale**: Default enum deserialization must never silently turn a missing operation into an applicable mutation. The operation is part of authority, not a value the apply gate may infer.

### Decision 37: JSON numeric syntax is not enough for characteristics

**Decision**: Characteristic preservation accepts only JSON number tokens that parse to finite runtime numbers. Exponent overflow such as `1e9999` is rejected alongside non-number JSON kinds.

**Rationale**: `System.Text.Json` can classify overflow syntax as a JSON number even when it cannot produce a finite value. Mechanical actor characteristics cannot carry infinity-equivalent state.

### Decision 38: Phase 18 changes shared worker and afterlife repair authority

**Decision**: Update the worker runner, bridge guide/formal contract/worked repair example, shared daemon repair template/spec, Mortal main guide/example, afterlife matrix/example, Spec Kit artifacts, and documentation/source guards. No new afterlife pending/control action, response field, receipt, report, scheduler contour, or normalizer side effect is added.

**Rationale**: The realm-authority pin is a new afterlife validation-repair authority rule and therefore cannot use the earlier Mortal-only no-change rationale. The output freshness and explicit operation rules are shared lifecycle contracts, so both realms require synchronized guidance even though canonical gameplay schemas remain unchanged.

### Decision 39: Workers execute from detached pinned-context snapshots

**Decision**: Materialize each worker task under client-owned `.worker_runtime`
with only the exact context bytes named and hashed in the task packet. Point the
worker task, proposal, session, and working-directory paths at that detached
snapshot. Import only a contract-valid proposal and the exact declared
non-delete `contentRef` bytes; discard direct state edits and every undeclared
artifact, then remove the snapshot.

**Rationale**: A prompt prohibition did not prevent an ordinary worker process
from editing the live session it was given. Removing the live session from the
normal worker protocol makes that mistake ineffective without pretending that
the bridge is an operating-system sandbox against a deliberately malicious
operator-supplied command.

### Decision 40: Apply is one canonical authority lease

**Decision**: Acquire one canonical write lease after proposal/content
prevalidation and retain it through final context/authority verification, all
target compare/exchange writes, complete-state validation, read-only context
revalidation, rollback when required, and accept/reject linearization. Every
`game_state/meta/` repair target is afterlife-scoped; mixed Mortal/meta batches
fail task construction closed. Soul and characteristics authority paths remain
read-only in standalone and mixed tasks.

**Rationale**: Per-file compare/exchange protects target ownership but does not
close the interval in which a cooperating writer can change an authority file
between validation and commit. A single lease makes the supported canonical
writer protocol linearizable, while final byte checks still detect external
non-cooperating mutation.

### Decision 41: Phase 19 changes harness authority, not gameplay schemas

**Decision**: Synchronize the runner prompt, worker bridge guide and formal
contract, worked validation-repair example, afterlife matrix/example, manifest,
active Spec Kit artifacts, and source guards. Do not add a pending/control file,
afterlife action type, response field, receipt, report, scheduler contour,
normalizer side effect, or player-facing command.

**Rationale**: Detached execution and the canonical lease change how shared
Mortal/afterlife repairs are safely delegated and committed. They do not create
new GM-authored game state, but the afterlife classification and Soul authority
rules are GM-facing repair contracts and therefore require both realms' repair
documentation.

### Decision 42: Worker handoff publication is all-verified and identity-preserving

**Decision**: Load and hash every declared non-delete proposal artifact before
publishing any of them. Reject an existing task or proposal identifier instead
of overwriting earlier evidence. If timeout and malformed proposal data coexist,
retain timeout as the authoritative execution outcome and append the malformed
handoff as diagnostic context.

**Rationale**: Per-artifact streaming publication permits a later digest failure
to leave a partial handoff. Overwriting IDs destroys review history, while
letting malformed JSON replace a timeout makes observability dependent on a
worker's final partial write.

### Decision 43: Canonical path and writer authority use one exact protocol

**Decision**: Treat canonical Windows session paths case-insensitively, reject
case-alias duplicates, classify the original issue set before worker-profile
path filtering, reject mixed Mortal/afterlife batches, and accept only exact
wildcard-free afterlife surfaces. Route built-in backup, restore, game-state
clear, and current-world lore clear operations through the same canonical write
lease used by worker apply.

**Rationale**: Windows case aliases and glob allowlists can bypass otherwise
correct read-only checks. A lease closes TOCTOU only when every cooperating
canonical writer participates, not merely the worker apply path.

### Decision 44: Detached cleanup cannot broaden authority or erase results

**Decision**: Delete detached workspaces with top-level post-order traversal,
remove reparse entries as links, and never recurse through their targets. Record
exhausted cleanup failures in worker audit while preserving the already known
completed, timed-out, failed, or rejected result. Synchronize shared Mortal and
afterlife worker guidance and source guards, but add no gameplay state field,
pending/control action, response, receipt, report, scheduler contour, or
normalizer side effect.

**Rationale**: Recursive filesystem deletion can escape the detached workspace
through a worker-created junction and a cleanup exception from `finally` can
mask the result that operators need to review. These are harness lifecycle
rules, not new GM-authored gameplay schemas.

### Decision 45: Repair attempt numbers are labels, not durable identities

**Decision**: Keep the zero-padded repair attempt in each validation-repair task
ID, but append a fresh lowercase GUID for every dispatch.

**Rationale**: The repair-loop attempt counter restarts in later validation
cycles. Using it as the full immutable task ID makes a valid later cycle collide
with evidence from the first cycle.

### Decision 46: Concurrency and evidence identity are harness-owned

**Decision**: Acquire a process-wide per-session/per-worker slot before task
publication and enforce the profile's `MaxConcurrentTasks`. Reserve immutable
task IDs with create-only compare/exchange. Stage `proposal.json` and every
declared content file outside the session, then, under the canonical lease,
verify the exact task bytes still identify the current session generation and
publish the complete bundle through one create-only atomic directory rename.
The durable bundle is authoritative; derived inbox/audit failure cannot erase
it. A losing publisher fails without overwriting prior evidence.
The proposal id `inbox` is reserved for the derived inbox directory and is
rejected before staging, so proposal identity cannot collide with that storage
namespace.

**Rationale**: Sequential existence checks cannot prevent two bridge-pool
instances from launching the same task, and a separate proposal claim followed
by multiple writes can strand partial evidence. Task reservation and complete
directory publication are different atomicity problems. Both are deterministic
harness coordination and must not be delegated to worker prompts.

### Decision 47: Afterlife authority is positive, not inferred by exclusion

**Decision**: Classify current-world lore and core player status explicitly as
Mortal, including nested issue coordinates. Require every exact afterlife
validation-repair surface to live below `game_state/meta/`, while preserving
exact task-provided control/report surfaces for typed afterlife content tasks,
and reject standalone NPC identity collision repair as main-GM-only.

**Rationale**: A path that was omitted from a Mortal prefix list is not thereby
afterlife state. Positive realm ownership prevents narrow profile filtering and
crafted packets from weakening the realm boundary.

### Decision 48: Save/load participates in canonical linearization

**Decision**: Place canonical lock authority under `.boe_runtime` outside the
replaceable session directory. Hold it while creating a save snapshot and while
swapping, refreshing, or restoring a loaded session. Load stages the replacement
and records an external durable journal plus the last valid session backup under
`.boe_runtime/load-transactions`. Startup recovery runs before ordinary session
directory initialization. Failed rollback preserves both journal and backup;
lease-aware refresh and client-owned mirror repair reuse the active lease.

**Rationale**: Loading previously replaced the entire live session outside the
worker apply lease, while saving could archive files from multiple authority
moments. An external lock survives directory replacement and makes both
operations cooperating canonical participants. A lease alone does not recover a
process crash or failed directory rollback, so the swap also needs a durable
recovery record outside the replaceable tree. These harness changes add no GM
response field, afterlife action, receipt, report, scheduler contour, or
normalizer side effect; Mortal and afterlife repair guidance is synchronized.

### Decision 49: Worker slots represent live process-tree ownership

**Decision**: Implement per-session/per-worker limits with reference-counted
gates shared across bridge-pool instances. Retire a gate when its final holder or
waiter leaves, permit a changed profile limit only after retirement, and return
an active limit mismatch as a failed worker result. On cancellation, kill the
complete process tree and await confirmed exit plus output drain before releasing
the slot.

**Rationale**: Permanent static semaphores retain stale profile limits and leak
session keys. Releasing a slot while a canceled parent or child remains alive can
let two workers mutate detached handoff state concurrently despite a one-slot
profile. Process-tree termination and gate lifetime are harness responsibilities,
not timing assumptions or prompt instructions.

## Tenth review hardening decisions

### Decision 50: Lease acquisition includes a recovery fence

**Decision**: Every canonical writer runs interrupted-load recovery immediately
after obtaining the external canonical write lease. Recovery failure disposes
the lease and rejects the writer before it can touch canonical state.

**Rationale**: Startup-only recovery leaves a process that survives a failed load
able to perform later writes against an unresolved transaction. The writer
boundary, not caller memory or timing, must make that state unrepresentable.

### Decision 51: Session generation is external durable authority

**Decision**: Store one nonce at
`.boe_runtime/session-generation/current.json`, rotate it under the canonical
lease on load and New Game, bind task reservation, proposal publication, and
apply to it, remove worker roots during transitions, and omit those roots from
save archives.

**Rationale**: A task file inside `game_session` can be restored byte-for-byte by
load and falsely look current. Authority must survive replacement of the very
directory whose identity it distinguishes.

### Decision 52: Refresh owns one read/modify/write lease

**Decision**: Public refresh acquires one canonical write lease before reading
profile mirrors, retains it through mirror repair and aggregate state refresh,
and exposes an internal lease-aware overload for callers that already own it.

**Rationale**: Locking only the final write allows a concurrent accepted update
to land between mirror read and repair write. A single transaction closes that
TOCTOU window without a replaceable in-session lock.

### Decision 53: Worker code starts only inside privately controlled process-tree authority

**Decision**: Start a hidden client-owned host and attach it to a kill-on-close
Windows Job Object before the configured command can launch. Parent and host use
private current-user named control/status pipe servers with unique endpoint
names. The host command line contains only those endpoints and a unique
per-launch nonce. Parent-side client PID authentication accepts both connected
clients only from that exact host PID before the parent sends the executable, arguments, working directory, and
environment in typed `Launch`; the host retains both channels and passes no pipe
handle to the configured worker. Every complete typed frame binds the nonce, and
no worker-accessible ready/release/completion marker exists. Completion
requires the direct-worker exit code and is sent immediately after direct-process
exit, before bounded output draining. An explicit `OutputDrained`
acknowledgement follows bounded capture and is awaited before ordinary host
teardown. Profile timeout and caller cancellation begin
before and cover the ownership handshake. Platforms without an equivalent
queryable kernel complete-tree boundary fail closed before worker release.
Complete-tree and unattached-host termination confirmation are bounded; timeout
and cancellation remain authoritative, while every cleanup uncertainty
quarantines capacity for the remainder of the process lifetime.

**Rationale**: Worker-visible markers let descendants forge readiness or success.
Attaching after direct launch leaves a child-escape race, beginning timeout after
release allows canceled work to perform external side effects, and waiting for
stdout/stderr EOF can deadlock when descendants inherit those pipes. A managed
Unix process-group abstraction cannot prove that every descendant remains owned
and queryable, so claiming an equivalent boundary is unsafe. Releasing capacity
after uncertain cleanup or waiting forever for an unconfirmable stop violates
`MaxConcurrentTasks`. These are harness-owned facts and cannot be repaired by
worker prompts.

### Decision 54: Worker apply is an externally journaled canonical transaction

**Decision**: Before the first canonical write of a non-empty proposal, persist
an external journal under `.boe_runtime/worker-apply-transactions` with intent,
the complete target manifest, exact before-images or missing baselines, and both
baseline and expected-applied hashes. Every canonical writer recovers an active
uncommitted transaction immediately after lease acquisition or fails closed.
Recovery walks entries in reverse and attempts every independently recoverable
restore while preserving the journal when bytes are unowned or any restore
fails. Commit is made durable before cleanup; committed cleanup is retryable and
never rolls accepted bytes back.

**Rationale**: In-memory rollback cannot survive process interruption between
two canonical writes. A journal inside `game_session` can be replaced by load,
and deleting recovery evidence before commit is durable can revoke a valid apply.
External intent plus before-images lets the next canonical writer finish recovery
without guessing and makes partial accepted proposals unrepresentable.

### Decision 55: Durable reservation and session replacement are typed authority boundaries

**Decision**: At apply time, reload the exact durable reserved task under the
canonical lease and treat it as the sole apply authority. Return an independent
copy of those exact persisted task bytes from reservation, require lowercase
canonical GUID text in `N` format, and propagate typed `SessionReplaced` through
reservation, proposal publication, apply, ready publication, and the repair
loop. A replacement aborts the old repair; no legacy fallback or rollback may
write into the replacement session. Repair telemetry uses a generation-bound
atomic append, the latest validation-repair task is excluded from saves, and a
committed apply transaction directory is removed before its active journal.

**Rationale**: Caller-owned task objects are mutable and cannot be authority.
Likewise, a generic worker failure can trigger legacy repair or rollback against
bytes that belong to a newly loaded game. Durable bytes plus typed replacement
make both error classes mechanically unrepresentable, while generation-bound
telemetry and save exclusions prevent stale evidence from crossing sessions.

**Alternatives rejected**:
- Trust the task object returned by the dispatcher: caller mutation can widen
  allowed paths after reservation.
- Convert generation replacement into a normal failed worker result: the old
  game loop can then fall back or roll back in the new session.
- Append trajectory records directly: a load can redirect stale telemetry into
  replacement state.

### Decision 56: Complete GM flows use an immutable session operation

**Decision**: Capture the durable session generation once at each outer
player-turn, raw life-evaluation, transition, incarnation, GM-wait, or New Game
bootstrap entrypoint. Bind all nested asynchronous work to that immutable
session operation. Every canonical mutation compares the bound generation after
recovery and under the canonical write lease. Terminal polling verifies the
generation before consuming ready/error signals, and the outer operation performs
a final durable verification before returning. Replacement is sticky across
nested catches and escaped tasks. The client must not hold the lifecycle lease
while waiting for the GM; the lifecycle lease exists only for short load/New
Game replacement and is acquired before the canonical lease. Any mismatch
propagates typed `SessionReplaced`.

**Rationale**: A canonical lease protects one mutation, not the complete logical
turn. Without an immutable operation generation, load can land after validation
but before materialization, cleanup, story append, or life-transition final
writes. A repair retry can also recapture the replacement generation and continue
an old turn in a new save. Sticky ambient authority plus in-lease checking makes
that class of write unrepresentable without holding a long-lived lock across a
human/model wait.

**Alternatives rejected**:
- Hold the lifecycle or canonical lease for the complete GM wait: this blocks
  load/New Game for an unbounded external operation and creates deadlock risk.
- Check generation only before starting the wait: replacement may occur during
  terminal polling or after validation.
- Let leaf callers catch replacement: an old outer flow could continue with
  cleanup or final writes.

### Decision 57: Worker runtime and handoff resources are externally bounded

**Decision**: Store detached execution snapshots outside the replaceable game
session. Accept an absolute `BOE_WORKER_RUNTIME_BASE_PATH` override and otherwise
choose a platform runtime base separated by a canonical-session-path hash. Limit
`proposal.json` to 1 MiB, each `contentRef` to 4 MiB, aggregate imported content
to 16 MiB, and captured stdout/stderr to 65,536 characters per stream with a truncation
marker.

**Rationale**: A runtime under the session can be archived, replaced, or traversed
as canonical state. Unbounded model output and artifacts allow one subordinate
worker to consume arbitrary process memory or disk before contract validation.

## Final Sol/max review hardening decisions

### Decision 58: Terminal resolution consumes one immutable signal snapshot

**Decision**: After terminal polling observes a ready file, acquire one canonical
write lease and read both `ready/turn_complete.json` and
`ready/turn_error.json` into one immutable snapshot. Parse, correlate, resolve,
and render only those captured bytes. Do not re-open either ready file after the
lease is released.

**Rationale**: A generation check followed by an unlocked file read still permits
load to replace the session in the verify/read interval. One leased byte snapshot
turns terminal selection into a single authority moment while preserving the
short-lease rule across the model wait.

### Decision 59: Process success, not proposal presence, grants handoff authority

**Decision**: A worker proposal is applyable only after confirmed zero exit and
confirmed process-tree termination. Timeout, cancellation, host failure,
non-zero exit, or unconfirmed cleanup returns no applyable proposal and performs
no workspace import; captured output remains diagnostic evidence.

**Rationale**: A syntactically valid proposal written before a crash or timeout
does not prove that the worker completed its self-check or stopped mutating its
workspace. Proposal presence cannot override the authoritative process result.

### Decision 60: Mortal bootstrap prose is never mechanical authority

**Decision**: The client-owned Mortal bootstrap writes only neutral canonical
scaffolding and empty mechanical collections. Skills, inventory, NPCs,
capabilities, resources, and carrying values appear only when the GM records an
explicit setting-aware decision in `structuredGmAuthority` and writes the
matching complete canonical state. Contract tests construct explicit actor
fixtures independently of the production bootstrap builder.

**Rationale**: Any mortal world may be fantasy, science fiction, post-apocalyptic,
historical, or another setting. Keyword inference makes mechanics accidental,
grandfathers incomplete same-turn actors into pre-turn authority, and lets test
fixtures normalize the very behavior the contract forbids.

### Decision 61: Replacement and rollback leave durable, retryable authority

**Decision**: Save/load replacement resets all session-local runtime fields and
rebinds `GameLoop` from the replacement state, or exits to the menu when no
active game exists. Worker rollback writes a durable `rolledBack` journal state
before deleting transaction artifacts. Configured worker runtime paths are
compared to the physical canonical session identity and reject reparse aliases.

**Rationale**: Disk replacement without runtime rebinding sends the next turn
with stale identity. Cleanup order must survive every partial failure, and
lexical path checks cannot protect canonical state from junction aliases.

## Final Sol/max follow-up hardening decisions

### Decision 62: Live-turn preparation is one immutable session transaction

**Decision**: Capture and bind the durable generation before `prepare-live-turn`
performs cleanup or reads canonical state. Hold one generation-checked canonical
write lease through no-follow cleanup, snapshot enumeration and capture, and
publication of the snapshot manifest, authority packet, and turn request.

**Rationale**: Separate public reads and writes allow load or New Game to replace
the session between phases. An old preparation can otherwise delete replacement
artifacts or publish an internally consistent old request into the new session.

### Decision 63: Proposal publication has one cancellation linearization point

**Decision**: Cancellation and timeout remain authoritative while a successful
worker proposal is staged and waits for publication authority. Under the
canonical lease, one atomic transition chooses either cancellation or durable
publication. Cancellation first leaves no bundle; publication first completes
the durable bundle and derived evidence without later cancellation revoking it.

**Rationale**: A token check before a non-cancellable publish leaves a race in
which the bridge reports `Stopped` or `TimedOut` while an applyable proposal
appears afterward. A shared transition makes the two outcomes mutually
exclusive.

### Decision 64: Production apply cannot bypass complete-state validation

**Decision**: The public `GmWorkerApplyGate` constructor requires the production
`ValidationService`. A non-null delegate constructor remains internal for
focused tests only.

**Rationale**: An optional validation delegate whose default is success turns a
security and consistency boundary into a fail-open API. Construction must make
the production invariant explicit and unskippable.

### Decision 65: Bootstrap scalar mechanics require structured GM authority

**Decision**: The client-owned Mortal bootstrap may create empty collections and
temporary identity/navigation scaffolding, but it does not assign player
progression thresholds, carrying values, faction progression/resources,
influence/control values, or a universal power profile. The GM supplies those
setting-owned mechanics through `structuredGmAuthority` and matching canonical
state during the first accepted turn.

**Rationale**: Zero, one, and one hundred are still authored mechanical values,
not neutral absence. The game may use any setting and progression model; a
client default silently becomes canonical pre-turn authority and constrains the
GM to mechanics the world never selected.

## Four-review integration hardening decisions

### Decision 66: Session closure and replacement are typed linearization boundaries

**Decision**: The outer session operation enters a closing state, performs its
final generation check, and closes while holding canonical authority. Session
replacement requires a purpose-bound replacement capability derived from an
active lifecycle lease. Load journals both old and new generation authority
before the first mutation and the actual UI load path always rebinds runtime
state from canonical replacement bytes.

**Rationale**: A final check followed by an unlocked close admits an escaped
writer. An untyped canonical lease can rotate generation accidentally, and a
session-byte rollback without generation/worker-evidence rollback leaves two
different notions of the active game.

### Decision 67: Canonical paths and rollback are physical and byte-exact

**Decision**: Canonical authority uses one physical session identity, rejects
traversal and reparse ancestors, and rechecks no-follow confinement at mutation.
Rollback captures exact bytes before recording a baseline, reports aggregate
restore failure, and retains evidence until the complete restore succeeds.
Snapshot capture/publication is one generation-bound canonical transaction.

**Rationale**: Lexical paths, text re-encoding, swallowed backup errors, and
multi-lease snapshots cannot prove that the bytes restored belong to the same
session and transaction that were validated.

### Decision 68: Durable worker reservation is the only task authority

**Decision**: `ValidationService` supplies the apply gate's filesystem identity.
The bridge deep-snapshots a task before any asynchronous boundary and persists
that exact snapshot before execution. Apply reloads only that reservation.
Cancellation/timeout remains authoritative until durable publication; audit,
cleanup, malformed handoff, or stale ready publication cannot rewrite the
result.

**Rationale**: Separately supplied roots and mutable caller objects defeat scope
validation. Terminal telemetry is evidence, not authority.

### Decision 69: Browser and image writes obey the same session fence

**Decision**: Browser multi-write actions enter `SessionOperationContext` before
their first read and commit under one canonical generation check. Remote image
generation writes to external staging and atomically commits only if the bound
generation remains active.

**Rationale**: UI-local locks and semaphores do not serialize console load. A
long remote request must not publish old-session data into a replacement save.

### Decision 70: Actor authority is explicit, bounded, and setting-neutral

**Decision**: Effective actor identities are unique. Legacy promotion receives
one closed accepted-turn path; stock/showcase replacement requires exact request
authority. Empty `structuredGmAuthority`, prose keywords, and client-authored
setting defaults grant no mechanics. Afterlife departure, mentoring, and
visibility use exact structured evidence shared by both projections.

**Rationale**: A complete-looking object is not authority. The game can use any
setting, so neither names nor bootstrap convenience values may silently choose
mechanics, and documented afterlife lifecycle operations must agree with common
profile binding.

### Decision 71: Clean-checkout and active-prompt coverage are release gates

**Decision**: Every intended source is tracked before final verification. Source
guards enumerate all active Mortal and afterlife rules, examples, manifests, and
daemon entrypoints rather than a selected subset.

**Rationale**: SDK globs can hide missing files locally, while stale active
instructions can make the GM generate state the validator correctly rejects.

## Post-T156 Sol/max filesystem decisions

### Decision 72: Rollback evidence outlives every fallible restore step

**Decision**: Browser recovery restores every canonical and typed external
before-image before deleting dynamic artifacts. Restore or cleanup failure keeps
the manifest and remaining evidence. Cleanup directories use an exact
client-owned allowlist rather than rollback-root ancestry.

**Rationale**: A partial restore is not a successful transaction resolution.
Deleting snapshots or arbitrary rollback descendants after one failed restore
destroys both repair authority and forensic evidence.

### Decision 73: `.boe_runtime` is physical authority, not a lexical namespace

**Decision**: Runtime locks and proposal/save staging reject reparse roots,
ancestors, and targets and repeat confinement checks around lock acquisition,
publication, and cleanup.

**Rationale**: A canonical session lock is ineffective when a runtime alias lets
two processes acquire physically different locks for the same logical session.

### Decision 74: The Daren reward profile joins durable browser atomicity

**Decision**: The browser transaction records a typed exact-byte before-image of
the external Daren reward profile before the profile write. Staged recovery
restores it before mutation or replacement; committed recovery preserves it.

**Rationale**: An in-memory rollback closure disappears on process termination
and can leave a permanent reward without the canonical completion that granted
it.

### Decision 75: Save publication and retirement use canonical mutation gates

**Decision**: Save archives are assembled under no-follow runtime staging and
published through one generation-bound canonical move. Autosave enumeration
does not grant deletion authority; each relative target is revalidated under
the lease at deletion.

**Rationale**: Validation before ZIP publication or directory enumeration leaves
a reparse swap window that can publish or delete outside the active session.

### Decision 76: A save never transports rollback authority

**Decision**: Browser rollback transaction roots are omitted from new archives
and stripped from replacement state loaded from legacy or crafted archives.

**Rationale**: Recovery evidence belongs to one exact generation. Importing it
into a replacement session can roll new canonical bytes back to an unrelated
old baseline.

## Second post-T156 Sol/max authority decisions

### Decision 77: Every durable runtime sibling is physical authority

**Decision**: Apply the runtime root's no-follow and boundary-revalidation
contract to load transactions, session generation, and worker-apply
transactions, including extraction roots, journals, manifests, and before
images.

**Rationale**: Validating only `.boe_runtime` or selected staging children still
allows an unvalidated sibling junction to redirect durable authority and
recovery evidence.

### Decision 78: A lock lease proves opened-handle identity

**Decision**: On Windows, resolve the final physical path from the acquired
`FileStream.SafeFileHandle` and compare it with the expected canonical or
lifecycle lock path before constructing the lease.

**Rationale**: Checking the pathname before and after open cannot detect a
swap-to-junction/open/swap-back race because the already-open handle remains
bound to the external file.

### Decision 79: Restored browser transactions become cleanup-only

**Decision**: Persist status `restored` after all rollback and dynamic cleanup
work succeeds, then remove backup evidence and delete the manifest last.
Recovery of `restored` or `committed` transactions performs cleanup only.

**Rationale**: Deleting the manifest first can leave undiscoverable evidence;
deleting backups while retaining `staged` can make a retry attempt an impossible
second restore.

### Decision 80: Console Daren completion enters canonical recovery

**Decision**: The console completion path acquires canonical authority before it
reads or writes the external reward profile and therefore resolves any staged
browser rollback first.

**Rationale**: A later browser recovery must not overwrite a better console
reward written after an interrupted browser action.

### Decision 81: Rollback-root cleanup handles file and directory shapes

**Decision**: Save exclusion, load stripping, and New Game cleanup treat the
exact rollback-root path as ephemeral whether it is a file or a directory, and
remove directory trees without following reparse points.

**Rationale**: A crafted exact-path file can block directory creation, while a
manifestless directory tree is not discoverable by manifest-based recovery but
still survives replacement unless explicitly removed.

## Third post-T156 Sol/max authority decisions

### Decision 82: Physical handles own canonical mutations

**Decision**: Treat pathname validation as admission, not mutation authority.
On Windows, retain a validated parent-directory handle opened with directory
list access and without delete sharing across each canonical atomic replace,
runtime-to-canonical publication, and deletion. Keep the validated source or
target handle open and perform rename or disposition against that same object.

**Rationale**: A second pathname check still leaves a check/use interval.
Windows directory handles opened with list access and without
`FILE_SHARE_DELETE` block replacement of that directory and its ancestors,
while handle-based rename/disposition removes the source/target pathname race.

### Decision 83: Runtime bytes are never trusted before opened-handle proof

**Decision**: Read durable runtime authority from validated opened handles.
Create runtime temporary, load-extraction, proposal-staging, and save-staging
files with create-only semantics, validate the opened object before writing
caller bytes, and publish that opened object while its physical parent remains
held. Never use `FileMode.Create` or an equivalent truncating open for an
unproven runtime path.

**Rationale**: Pre/post pathname checks cannot detect
swap-to-external/open/swap-back, and a truncating open damages external state
before the post-open rejection. Create-only plus opened-handle validation makes
the bad target harmless and handle-bound reads prevent external generation or
journal bytes from becoming session authority.

## Fourth post-T156 Sol/max authority decisions

### Decision 84: Authority files are single-link objects

**Decision**: Inspect every accepted regular-file handle with
`FILE_STANDARD_INFO` and require `NumberOfLinks == 1` before using the file as
canonical or durable-runtime authority. Repeat the check at publication and
deletion boundaries where the same handle remains authoritative.

**Rationale**: A hard link is not a reparse point and
`GetFinalPathNameByHandle` may report the expected opened name while another
name reaches the same bytes. Rejecting multi-link files prevents an external
alias from supplying session generation, journal, save, or canonical state
authority and prevents client mutation from propagating through that alias.

### Decision 85: Lock leases own their physical parent

**Decision**: Open and validate the physical `.boe_runtime/locks` directory
before opening or creating a lock file. Retain that non-delete-shared directory
handle together with the lock stream for the complete canonical or lifecycle
lease.

**Rationale**: Post-open lock-file validation prevents a false lease but does
not undo an external file created by `OpenOrCreate`. Parent-first authority
makes the external create impossible and also prevents the lock namespace from
moving while the lease remains active.

### Decision 86: Swap-back evidence is post-open

**Decision**: The runtime-read fault hook executes after the operating system
returns the file handle and before final-path/single-link validation. The
regression restores the expected pathname from this hook and proves the opened
external target is still locked by that handle.

**Rationale**: Restoring the path after read failure or stream disposal tests
only pre-open rejection. The authority claim requires evidence that validation
is applied to the exact still-open object after a swap-back.

## Fifth post-T156 Sol/max authority decisions

### Decision 87: Mutable prompt sessions own a generation

**Decision**: Capture the current generation under canonical authority before
publishing a mutable browser form. Persist it in the in-memory prompt snapshot
and bind submit, cancel, and lock cleanup to that exact generation.

**Rationale**: Owner and entity IDs are not session identity. Reusing the
current generation at submit time lets a form from session A mutate session B.

### Decision 88: Opened handles prove object type

**Decision**: Query `FILE_STANDARD_INFO.Directory` for every opened
publication source and require it to match the requested file/directory kind
before rename.

**Rationale**: Backup semantics allow opening a regular file through a
directory-intended path. Path syntax and flags are not proof of object type.

### Decision 89: Local UI locking is one canonical transaction

**Decision**: Public inspect/acquire/refresh/release operations acquire one
canonical lease and use only lease-aware handle-bound reads and mutations.
Generation-bound callers retain their earlier generation instead of adopting
the current one.

**Rationale**: Separate pathname reads, creates, and deletes permit parent
replacement and can delete a lock that belongs to a replacement session.

### Decision 90: External rewards use typed physical authority

**Decision**: Treat `client_profile/daren_qte_reward.json` as a typed external
authority file. Retain its validated parent while exact bytes are read,
atomically replaced, restored, or deleted, and reject reparse or multi-link
identities.

**Rationale**: Canonical rollback correctness is incomplete when its external
side effect can be redirected or restored through a different path object.

### Decision 91: Pending-turn authority has no raw compatibility reader

**Decision**: Async and sync pending-turn consumers use the same canonical
opened-handle byte reader. Compatibility with synchronous validation is
provided by a synchronous handle-bound API, not by `File.ReadAllBytes/Text`.

**Rationale**: Hash validation authenticates whichever bytes were read; it
cannot repair a path race or hard-link alias in the byte acquisition itself.

### Decision 92: Existing replacement targets are authority objects

**Decision**: Before replacing an existing canonical destination, open it
through the retained parent authority and reject it unless its regular-file
identity is single-link.

**Rationale**: Validating only the temporary source still lets replacement
unlink one name of a multi-link destination and silently mutate authority
semantics reachable through another name.

### Decision 93: UI locks never survive session persistence

**Decision**: Add the local-UI lock to the exact ephemeral save/load exclusion
set.

**Rationale**: A restored owner lease is neither live process authority nor
part of player state and can block a different owner for its full duration.

### Decision 94: Legacy emptiness is not first materialization

**Decision**: Enforce non-empty setting-defined characteristics only when the
current actor operation requires complete materialization. Preserve an
unchanged envelope-free legacy empty object.

**Rationale**: The harness must improve future materialization without forcing
unrelated legacy resends or retroactively invalidating untouched canonical
actors.

### Decision 95: QTE mutations use server-issued interaction tokens

**Decision**: Publish an opaque token for each exact generation-bound QTE offer
or attempt revision and require it for every mutable QTE request.

**Rationale**: Capturing the current generation when a request arrives proves
only where the server is now; it does not prove which page, offer, or attempt
issued the request.

### Decision 96: Prompt generation precedes prompt construction

**Decision**: Capture the immutable generation before building a prompt-bearing
command result. Check and remove stale snapshots before owner or answer
validation.

**Rationale**: A form built from session A and attached after load can otherwise
be labelled as session B, while invalid stale requests can keep the old form
alive.

### Decision 97: Lock owners are not lease identities

**Decision**: Add a unique acquisition token to every local-UI lock and require
the exact generation, owner, and token for refresh/release. Successful
replacement performs no post-rotation release.

**Rationale**: Fixed owner labels describe purpose, not one exclusive lease. A
late operation with the same label must not delete a newer lock.

### Decision 98: Ephemeral files reserve their namespace

**Decision**: Exclude and strip the local-UI lock exact node and every
descendant, regardless of whether the node is a file, directory, or reparse
point.

**Rationale**: ZIP path semantics can materialize a directory below a filename;
exact-file cleanup alone permits a persistent denial of future locking.

### Decision 99: Pending-turn integrity failures never become fallback data

**Decision**: Guardian, gacha, and realm synchronous readers use canonical
relative paths and the shared handle-bound API. Only documented absence may
select a fallback; path-contract and physical-integrity failures propagate.

**Rationale**: A broad catch converts a harness defect or raced authority into
plausible turn-zero/base-rarity data and lets validation accept the wrong state.

### Decision 100: Authority reads validate after consumption

**Decision**: Repeat physical path, kind, and single-link validation after the
complete text/byte/archive read and before accepting its result.

**Rationale**: NTFS permits link-count changes while a read handle remains open;
an initial check does not prove that the consumed authority stayed single-link.

### Decision 101: Daren rollback proves post-image ownership

**Decision**: Retain transaction parent authority and record baseline plus exact
published post-image identity. Restore/delete only that owned post-image.

**Rationale**: Baseline bytes alone cannot distinguish the transaction's write
from a later unrelated file placed at the same pathname.

### Decision 102: Atomic replacement is one reversible physical transaction

**Decision**: Retain source and destination object authority through
publication. If any final or post-publication proof fails, restore exact prior
destination identity/bytes or absence before returning failure.

**Rationale**: Throwing after rename while leaving changed canonical bytes is
not fail closed, and closing the destination handle before rename reopens a
hard-link/identity TOCTOU window.

### Decision 103: Unsafe platform fallbacks fail before mutation

**Decision**: Enable authority-bearing replacement only where an opened-handle
or descriptor-bound single-link/relative-rename protocol is implemented;
otherwise fail closed before touching destination state.

**Rationale**: Pathname `overwrite` cannot satisfy the same security contract
and must not silently weaken authority on another platform.

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
