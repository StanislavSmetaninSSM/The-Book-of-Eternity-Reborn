# Data Model: Complete Actor Materialization

## Actor Materialization Envelope v1

```json
{
  "materialization": {
    "schemaVersion": 1,
    "materializationId": "mat_npc_marius_turn_3",
    "actorType": "mortal_npc",
    "actorId": "npc_marius_de_valmont",
    "materializedAtTurn": 3,
    "state": "complete",
    "capabilities": {
      "canFight": true,
      "canTeach": false,
      "canTrade": true,
      "ownsItems": true
    },
    "sections": {
      "skills": { "state": "populated" },
      "inventory": { "state": "populated" },
      "fateCards": {
        "state": "empty_by_design",
        "reason": "Его судьба пока не открыла отдельной карты."
      },
      "personalQuests": { "state": "populated" },
      "relationships": { "state": "populated" }
    }
  }
}
```

### Common fields

| Field | Type | Rule |
|---|---|---|
| `schemaVersion` | integer | Exactly `1` for this contract |
| `materializationId` | string | Non-empty, stable, unique in actor scope |
| `actorType` | enum | Exact supported actor type |
| `actorId` | string | Exact canonical stable ID; same-turn Mortal actors use their `initialId` binding until canonicalization |
| `materializedAtTurn` | integer | Non-negative accepted-turn number |
| `state` | enum | Exactly `complete` |
| `capabilities` | object | Exact allowed boolean keys for actor family |
| `sections` | object | Exact governed keys for actor family |

### Section disposition

```json
{ "state": "populated" }
```

or:

```json
{
  "state": "empty_by_design",
  "reason": "In-world explanation written by the GM."
}
```

Rules:

- `populated` forbids `reason` and requires non-empty canonical section content.
- `empty_by_design` requires non-empty `reason` and requires canonical section content to be empty.
- Unknown fields and unknown section names are rejected.
- Reasons are private GM/harness evidence and are not rendered as player prose automatically.

## Mortal actor contract

### Identity binding

- `actorType`: `mortal_npc`
- `actorId`: existing `NPCId` for permanent actors; exact `initialId` for same-turn first creation before canonical ID assignment.
- Effective identity is resolved once from the canonical permanent fields or the exact same-turn `initialId`. If validated pre-turn state already owns that value as a permanent `NPCId`, `NPCId=null` plus the colliding `initialId` is invalid and inventory continuity still treats the actor as existing.

### Capabilities

| Capability | Required canonical evidence |
|---|---|
| `canFight` | At least one structurally valid active or passive skill |
| `canTeach` | `teacherProfile.canTeach=true` and non-empty `teacherProfile.skills` |
| `canTrade` | Existing NPC trade/merchant authority indicates trading is available |
| `ownsItems` | Non-empty inventory; equipped references resolve to inventory |

### Sections

| Section | Canonical source |
|---|---|
| `skills` | `activeSkills` + `passiveSkills` |
| `inventory` | `inventory` and equipment references |
| `fateCards` | `fateCards` |
| `personalQuests` | `personalQuests` |
| `relationships` | `relationshipLevel`, `attitude`, `relationshipLock`, and any initial inter-NPC relationship authority |

The existing core NPC fields remain mandatory and are not duplicated in `sections`.
`characteristics` must contain at least one numeric property. Property names come from the current world's schema; the materialization contract does not define a universal characteristic vocabulary.
Current first materialization contains 3-5 `personalityTraits`; every trait has a mandatory integer `value` from 1 through 10. Untouched historical legacy records are not retroactively rejected only for older cardinality.

### Ordinary-existing `NPCCoreChanges`

`NPCCoreChanges` is a Mortal-only non-carrier command mapped to `game_state/npcs/npc_core.json`. It is validated before normalization, reduced into every unambiguous canonical mirror of one existing actor, removed after successful reduction, and absent from final canonical state. Invalid commands remain unconsumed for repair.

```json
{
  "NPCCoreChanges": [
    {
      "NPCId": "exact-existing-permanent-id",
      "reason": "non-empty in-world/mechanical reason",
      "profile": {
        "worldview": "absolute replacement from the current story"
      }
    }
  ]
}
```

Unused optional mutation groups are omitted. A present empty object or array is
not a placeholder and is rejected by the runtime contract.

Boundaries:

- `NPCId` is one exact existing permanent identity. Names, `initialId`, new/stale targets, case-variant ambiguity, and divergent mirrors fail closed.
- `reason` is non-empty and at least one mutation group is non-empty. Values are absolute resulting values; expressions and prose-derived arithmetic are invalid.
- Unknown members are rejected recursively. Identity, name, materialization, inventory/equipment, skills/mastery, relationships/locks, journals/memory, goals/quests, activities, masks, custom states, teacher/trade capabilities, and arbitrary paths remain protected or owned by dedicated commands.
- Current and validated pre-turn `npc_core.json` are parsed duplicate-sensitively before command evaluation. Malformed, non-object, or duplicate-member authority is a blocking structured error rather than an absent command.
- Historical `NPCsInScene` and envelope-free `UpdateNPCs` preserve every actor-owned field. Inventory and materialization use their narrower continuity diagnostics; every other direct carrier mutation is rejected by the shared actor-ownership boundary.
- `characteristicValues` contains finite numeric results only for actor-owned keys or explicit current-world characteristic authority. Carrying and progression formulas also require explicit current-world authority; absent carrying authority leaves the setting-owned nullable result null.
- A location mutation carries both fields and uses exactly one authority branch: permanent `currentLocationId` plus null `initialLocationId`, or null current plus exact same-turn `initialLocationId`.
- Changing any level/experience threshold field requires the coherent non-negative tuple. Include `lastPlayerXPValueOnSync` when a role/progression transition requires synchronization.
- Faction upserts use exact faction identity and the existing complete affiliation shape. Fate Card additions reuse the full production Fate Card, active/passive skill, Combat Action, and effect validator, have unique IDs, and begin locked; any nested failure keeps the command unconsumed and all mirrors unchanged. Removals target only validated pre-turn locked/unrealized cards. Unlocking remains `NPCFateCardUnlocks`.

Mortal combat capability and promotion evidence use the canonical active/passive
skill structure. A complete Block 7 skill does not require `skillId` or `id`;
legacy identifier-only records remain detectable for migration without changing
the production skill validator.

### Fresh Mortal bootstrap mechanical authority

The client creates only neutral structural containers before the first Mortal
turn. `game_state/player/experience.json` is empty; inventory contains empty
`items` and `equipment` without carrying totals; faction and quest collections,
faction resources, and current-location control arrays are empty. No temporary
faction identity, faction chronicle, or quest/objective is generated.

The scaffold exposes empty `structuredGmAuthority.playerProgression`,
`carryingRules`, and `factionMechanics` arrays. If the accepted first turn adds
any progression tuple, carrying result, faction resource/control value, or
faction progression/power field, the matching authority array contains at least
one non-empty domain-specific GM decision. Each decision names the exact
`canonicalPath` and contains a non-empty `values` object whose members and values
match the mechanics introduced at that path; a faction decision additionally
names the exact `factionId`. An empty object, a reason-only record, an unrelated
path, or prose alone does not grant authority. Missing or unbound authority is a
blocking, focused validation issue. The bootstrap also leaves location type,
biome, traversal, safety, difficulty, faction chronicle, quests, and objectives
absent until the GM materializes them. This allows arbitrary Mortal settings
while preventing the client from silently imposing fantasy vocabulary,
indoor/walkable assumptions, a fixed XP threshold, a universal carrying formula,
or a fixed faction power model.

## Afterlife actor contract

### Supported `actorType` values

- `guardian`
- `resident`
- `shining_resident`
- `shining_faction_head`
- `radiant_actor`
- `saref_agent`
- `system_actor`
- `custom_afterlife_actor`

Exact aliases already accepted by canonical profile identity are normalized only for comparison; the stored value must use a documented canonical token.

### Capabilities

| Capability | Required canonical evidence |
|---|---|
| `canFight` | At least one usable standard or special spiritual art |
| `canTeach` | Existing mentor authority and at least one teachable art/showcase entry |
| `canTrade` | Exact current realm authority: the one active Guardian whose abode is the current Chaos Sea abode, or a non-player secure/contested head of an operational Shining faction at trade tier 1 or higher. Missing or ambiguous authority fails closed to `false`; prose, role names, and genre vocabulary never count. |

Afterlife envelopes do not use `ownsItems`; Mortal inventory remains forbidden.

### Sections

| Section | Canonical source |
|---|---|
| `standardArts` | Common profile standard arts |
| `specialArts` | Common profile special arts |
| `customStates` | Common profile custom states |
| `fateCards` | Common profile Fate Cards |
| `relationships` | Common profile relationship records |
| `agency` | Meaningful goals, non-empty personal quests, a meaningful current activity, or non-empty completed activity history. Masks, disposition, and progression strategy may inform behavior but do not satisfy this section alone. |
| `progressionHistory` | Ledger and progression ledger evidence |

### Cross-file binding

A significant non-player afterlife record must resolve by exact actor type and ID to one common profile. Guardians, residents, and Shining leadership retain their type-specific dossiers. The common profile is complementary authority for spiritual progression, Actor Brain inputs, relationships, and memory. Every newly materialized profile, including the first envelope on an existing bound profile, must have actor-owned memory: the exact dedicated Guardian/resident thought journal when that surface exists, otherwise a non-empty exact profile `gmThoughtsSummary`. An unchanged legacy profile with no envelope transition remains grandfathered.

Exceptions:

- A vacant Shining seat has no head profile.
- The player soul resolves to its existing client-owned profile.
- A System Guardian fresh-game seed remains `actorType=guardian`, is recognized through its existing client-owned source authority, and receives a deterministic valid envelope whose capability booleans, including `canTrade`, match that exact seeded current authority.

## State transitions

1. `absent` -> `complete`: permitted only on first materialization with complete envelope.
2. `legacy_without_envelope` -> unchanged: accepted for load compatibility.
3. `legacy_without_envelope` -> significant promotion: blocked until materialized; Mortal effective identity cannot be disguised with a colliding `initialId`, the complete object may repeat `inventory` only when it is semantically identical to the validated pre-turn snapshot, and an afterlife first envelope must prove actual actor-owned memory.
4. `complete` -> ordinary update: dedicated delta commands mutate their owned gameplay fields; valid `NPCCoreChanges` mutates only its closed core groups; envelope identity remains stable.
5. `complete` -> resent as first materialization: rejected for existing actors when it attempts to bypass delta authority.
6. `complete` -> invalid contradiction: blocked with bounded repair.

Legacy Mortal promotion is a distinct accepted-turn transition, not an ordinary
full-object update. It binds one validated pre-turn permanent identity without a
materialization envelope to one current carrier with its first complete
envelope. Only the closed promotion-owned role fields may differ; exact
inventory and every unrelated actor-owned field remain equal to pre-turn
authority. Stock and training showcase payloads are not promotion fields and
require their own request/receipt authority.

Afterlife `departure_only` is the only binding-removal transition. It requires
the exact validated pending transfer, departure receipt, and history record for
the same resident identity. A missing target alone never authorizes deletion.

## Session replacement and canonical mutation authority

A canonical lease carries an explicit purpose. Ordinary mutation leases cannot
rotate session generation. A replacement capability is created only while an
active lifecycle lease is held and binds the physical canonical root plus the
replacement purpose.

The durable load journal records:

- transaction identity and phase;
- physical canonical root;
- previous and replacement generation values;
- staged, live-backup, failed-live, and worker-evidence locations;
- commit state.

Recovery restores canonical bytes, previous generation, and previous worker
evidence together for every uncommitted replacement. A committed replacement
ensures the replacement generation and strips stale loaded worker evidence.

Rollback before-images are byte arrays with exact SHA-256 hashes and an explicit
missing baseline. A baseline enters the manifest only after durable capture.
Restore failure is aggregate and evidence remains until every target succeeds.

## Worker terminal authority

The immutable task snapshot is created before validation, queueing, hooks, or
reservation. Its durable reservation supplies scope, identity, generation, and
hash authority to apply. The production apply gate derives the same physical
canonical filesystem from `ValidationService`.

Proposal publication is the only success linearization point. Before it,
cancellation or timeout wins over malformed input, rejection, generic faults,
audit failure, and cleanup failure. After it, later cancellation cannot revoke
the complete bundle. Ready publication against a replacement session returns
typed `SessionReplaced`.

## Browser mutation authority

Browser direct actions bind a session operation before reading canonical state.
Spend, snapshot, authority, and request files form one canonical transaction.
Media generation downloads to an external staging file and commits the final
bytes atomically only after generation verification under canonical authority.

Durable browser rollback manifest schema 3 contains canonical `entries`, exact
allowlisted `cleanupDirectories`, and typed `externalEntries`. An external entry
uses a closed client-owned file identifier, an `existed` flag, an optional
canonical backup path beneath the transaction root, and SHA-256 of the exact
before-image. The current typed external surface is the Daren reward profile.
`staged` recovery restores all canonical and external entries before any dynamic
cleanup; any failure retains the manifest and evidence. After successful restore,
the manifest durably transitions to `restored`, a cleanup-only state. `committed`
and `restored` recovery preserve current accepted bytes, remove backup evidence
first, and remove the manifest last so a failed cleanup remains discoverable and
retryable without repeating rollback.

`.boe_runtime` is the physical authority root for canonical/lifecycle locks and
client staging. Locks, proposal/save staging, load transactions, session
generation, and worker-apply transactions reject reparse ancestors/targets and
are revalidated before every durable boundary. A granted canonical or lifecycle
lease additionally proves the opened handle's final physical path is the expected
lock file. Save ZIP bytes move from `.boe_runtime/save-staging` into
`game_session/saves/**` only under the canonical generation-bound lease.
Autosave candidates become canonical relative paths before a deletion barrier
and each deletion repeats no-follow validation. Browser rollback roots are
ephemeral: save capture omits both the exact-file form and all descendants, while
load and New Game remove exact-file and manifestless-directory forms without
following reparse points.

Path validation is only the admission step for physical file authority. On
Windows, a canonical or runtime operation retains a validated directory handle
with delete sharing denied for its physical parent until the operation
completes. Atomic and staged files are created with create-only semantics,
validated before caller bytes are written, and remain represented by the same
opened handle through rename and post-rename identity verification. Existing
runtime authority files are read from their validated opened handle. Canonical
file deletion marks the validated opened object for deletion rather than
reopening a previously checked pathname. These handle-bound rules cover session
generation, load and worker-apply journals/manifests/before-images, load
extraction, proposal staging, save staging, and runtime-to-canonical
publication.

Every regular file accepted by this physical layer additionally carries
single-link identity: `FILE_STANDARD_INFO.NumberOfLinks` must equal one before
its bytes or mutation authority are used. Hard-linked canonical state, session
generation, transaction evidence, save files, and lock files are invalid
authority even when the opened pathname is lexically and physically expected.

Canonical and lifecycle leases own two handles as one lifetime-bound authority:
the exclusive lock-file stream and the validated non-delete-shared
`.boe_runtime/locks` parent directory. The parent is acquired before any
lock-file `OpenOrCreate`; both handles are released only when the lease is
disposed. A runtime-read test barrier belongs between OS open and final
path/single-link validation, never after stream disposal.

The Daren reward profile is external to `game_session` but shares canonical
recovery authority. Browser writes stage its exact before-image; console writes
must first acquire canonical authority, recover any staged browser transaction,
and only then compare and publish the best reward.

## Validation issue families

- `actor_materialization_missing`
- `actor_materialization_invalid_envelope`
- `actor_materialization_duplicate_property`
- `actor_materialization_invalid_actor_type`
- `actor_materialization_actor_binding_mismatch`
- `actor_materialization_duplicate_id`
- `actor_materialization_section_missing`
- `actor_materialization_section_content_mismatch`
- `actor_materialization_capability_mismatch`
- `actor_materialization_inventory_reference_mismatch`
- `afterlife_actor_materialization_profile_missing`
- `afterlife_actor_materialization_profile_ambiguous`
- `actor_materialization_existing_resend_forbidden`
- `npc_characteristics_empty`
- `npc_existing_inventory_resend_forbidden`
- `npc_initial_id_collides_with_existing_permanent_id`
- `npc_core_changes_unknown_member`
- `npc_existing_core_direct_mutation_forbidden`
- `afterlife_actor_materialization_memory_missing`

Every issue records actor type/ID, section or capability where applicable, expected canonical target, and a bounded repair hint.

Raw continuity authority is duplicate-sensitive before semantic comparison. Current Mortal/afterlife actor subtrees, current inventory/materialization values, and validated pre-turn actor/inventory authority reject duplicate members through structured issues. Only duplicate-free valid JSON reaches order-insensitive semantic equality.

The three Mortal continuity issue policies are explicit:

| Issue code | Required metadata | Worker policy |
|---|---|---|
| `npc_initial_id_collides_with_existing_permanent_id` | exact `mortal_npc:<id>`, `NPCIdentity` | Never dispatch/apply through a worker; use main-GM rollback/repair because identity correction may have cross-file consequences. |
| `npc_existing_inventory_resend_forbidden` | exact actor, `NPCInventory`, current inventory in `actual`; exact validated pre-turn JSON array in `expected` only for a true legacy promotion | Dispatch only with the exact JSON-array snapshot. Apply may replace only the named actor/carrier inventory with that snapshot. Ordinary existing resends remain main-GM-only. |
| `npc_characteristics_empty` | exact actor, `NPCCharacteristics`, setting-defined numeric requirement | Dispatch pins `game_state/misc/characteristics.json` as read-only context. Apply may replace only the named actor/carrier empty object with finite numeric keys present in that authority; missing/malformed/empty authority rejects. |

For every supported worker correction, deleting/adding the file or actor, changing a sibling field, another actor, root data, a different carrier, or a non-snapshot target remains protected and rejects before full validation.

The high-priority main-GM inventory repair packet never turns an ordinary existing full object into a partial object: because canonical full-object shape requires `inventory`, the whole ordinary-existing `UpdateNPCs` resend is removed. Skill, inventory, relationship, journal, goal/quest, activity, equipment/resource, rename, and unlock changes are re-authored through dedicated commands; the bounded profile/location/progression/setting-owned characteristic/faction/Fate Card definition groups use `NPCCoreChanges`. A protected field outside both contracts forces main-GM rollback/repair rather than widening the command or deleting required fields. Genuinely new initial inventory and exact-snapshot legacy promotion remain the two complete-object branches.

The third re-review additions in this section are Mortal-only. They do not change Chaos Sea or Shining Abode pending/control files, response fields, receipts, reports, actor-profile schema, validation, normalization, scheduler, lifecycle mode, or authority path.

The fourth re-review adds no afterlife pending/control surface. The shared Block 5
Combat Action validator now enforces its already documented mandatory effect
`value`; the afterlife matrix/example call out inherited item/relic/skill use and
explicitly keep `specialArts[].combatEffect` on its separate spiritual-conflict schema.

Before applying a worker-authored actor materialization repair, the apply gate routes memory issues by actor type, removes only the exact mutable subtree named by the issue, and semantically compares the remaining canonical JSON. Guardian targets use the canonical/supported Guardian journal path, residents use resident state/journal, and Radiant/Saref/other common-profile actors use only their exact profile `gmThoughtsSummary`. Any change to protected actor data, another actor, root state, currencies, progression, envelope, or unrelated scalar rejects the proposal. An ambiguous-profile repair may only remove duplicates while retaining one otherwise unchanged canonical profile. Dedicated Guardian/resident memory repair is stricter than ordinary scalar repair: all existing journal entries remain an exact prefix, and the worker may append exactly one meaningful thought for the issue-bound actor without rewriting or deleting history. If and only if `game_state/meta/guardian_thought_journal.json` is absent and every scoped issue is `afterlife_actor_materialization_memory_missing` for one exact `guardian:<id>`, preservation uses `{ "entries": [] }` as the baseline; the normal proposal contract still requires Add, `beforeSha256=missing`, exact `afterSha256`, and the proposal-bound content reference.

Every validation-repair context path has one exact byte state: a 64-character SHA-256 digest or `missing`. A changed-file entry repeats that state as `beforeSha256`; add is legal only from `missing`, replace/delete only from an existing digest. The worker runs in an ephemeral detached `.worker_runtime` snapshot containing only those pinned context bytes. Non-delete content is imported only when declared at `worker_proposals/<proposalId>/<path>` and its bytes match `afterSha256`; delete uses `afterSha256=missing` and no content reference. Direct snapshot edits and undeclared artifacts are discarded. Apply and rollback both compare expected bytes under one canonical write lease retained through full validation, read-only context revalidation, and final decision. A mismatch is a conflict and never overwrites the newer owner.

All declared non-delete content is loaded and digest-verified before any proposal
artifact is published. Task and proposal IDs are immutable identities; a
collision preserves the earlier artifact and rejects the new handoff. Canonical
session-path identity is case-insensitive, duplicate aliases reject, and
afterlife validation-repair surface authority is an exact wildcard-free path
set wholly under `game_state/meta/`; typed afterlife content tasks retain their
exact task-provided control/report surfaces. `lore/current_world/**` and
`game_state/core/player_status.json` are Mortal authorities even when an issue
points at a nested field. Validation-repair dispatch IDs combine the readable
attempt number with a unique dispatch suffix. Task identities are reserved by
create-only compare/exchange before launch. A proposal's JSON and declared
content form one staged bundle; after exact task-byte/session-generation
verification under the canonical lease, one create-only directory rename makes
the complete bundle durable. The bundle remains authority if derived inbox or
audit publication fails. The proposal id `inbox` is reserved for the derived
inbox directory and cannot identify a proposal.
Cancellation/timeout and publication meet at one lock-protected transition.
Staging writes and canonical-lease acquisition remain cancellable. If
cancellation wins, staged content is removed and neither the bundle nor derived
inbox can appear later. If publication wins, cancellation no longer revokes the
complete durable bundle or its apply authority.
A reference-counted
per-session/per-worker gate owns a
`MaxConcurrentTasks` slot before any task artifact is written, retires when idle,
and is not released on cancellation until the complete process tree has exited.
Session-generation authority is the versioned document
`.boe_runtime/session-generation/current.json`, outside `game_session`; load and
New Game rotate it under the canonical lease, delete live worker task/proposal
roots, and save snapshots omit those roots. Reservation, bundle publication,
and apply compare the bound nonce, so restoring byte-identical handoff files
cannot restore their authority.

A worker process begins as a gated hidden client-owned host. The parent creates
private current-user named control/status pipe servers with unique endpoint
names and starts the host with those names plus one unique 32-lowercase-hex
launch nonce only. Parent-side client PID authentication accepts both channels
only from the expected hidden host. The configured executable, arguments,
working directory, and environment then cross the control pipe in typed
`Launch`; the host retains both channels and the configured worker receives no
pipe handle. The configured command is released only after the host belongs to
the supported kill-on-close Windows Job Object. No
ready/release/completion marker exists in a worker-accessible directory. Every
frame carries schema, nonce, and explicit kind; completion additionally carries
a non-null direct-worker exit code. The host publishes completion immediately
after direct-process exit and before bounded output draining. An explicit
`OutputDrained` acknowledgement follows bounded capture and is awaited before
ordinary host teardown. Missing/default kinds, malformed frames, wrong nonces,
unexpected client PIDs, and missing exit codes fail closed.

A platform without an equivalent queryable kernel complete-tree boundary rejects
worker execution before release. Cancellation and normal completion terminate
and query the complete Windows Job before slot release. Complete-tree and
unattached-host termination confirmation have bounded deadlines. Timeout or
cancellation remains the authoritative task result even if cleanup fails; any
failure to confirm stop or dispose process-tree authority quarantines the slot
for the remaining client process lifetime.
An observed timeout remains the execution result even when malformed proposal
bytes also exist. Built-in backup, restore, game-state clear, and current-world
lore clear participate in the canonical write lease. Save reads and
live-session replacement on load participate in that lease as well; the lock
file and a durable load journal live under `.boe_runtime` outside the replaceable
`game_session` directory. The journal identifies staged, backup, and failed
session directories so startup can restore interrupted swaps before normal
initialization and retain the last valid backup when rollback fails. Every
later canonical writer repeats recovery immediately after lease acquisition or
fails closed. Public state refresh holds one lease across profile-mirror read,
repair, and aggregate refresh, while lease-aware callers reuse that same lease.
Multi-file worker apply has a separate external durable journal under
`.boe_runtime/worker-apply-transactions`. Before the first canonical mutation it
stores transaction intent, the complete target manifest, exact before-images or
missing baselines, baseline hashes, and expected applied hashes. Every canonical
writer recovers an uncommitted journal after acquiring the same lease or fails
closed. Recovery restores entries in reverse, continues across independent
restore failures, and preserves evidence if current bytes match neither baseline
nor expected applied bytes. Commit is journaled before cleanup; a committed
cleanup failure remains retryable and cannot revoke or roll back accepted bytes.
Detached cleanup traverses only
ordinary child entries, removes reparse entries as links, and records exhausted
cleanup failure without replacing the worker result.

The exact durable reserved task is the sole apply authority. The apply gate
reloads it under the canonical lease, while reservation gives execution an
independent copy of those exact persisted bytes so caller mutation cannot widen
scope. Session identity is lowercase canonical GUID text in `N` format. Typed
`SessionReplaced` aborts the old repair before legacy fallback or rollback can
touch a replacement session. Repair telemetry uses a generation-bound atomic
append, and the latest validation-repair task is ephemeral and omitted from save
archives. Committed apply cleanup deletes the transaction directory before its
active journal, preserving retry evidence if cleanup fails.
Every public production apply gate is constructed with `ValidationService`.
Only an internal test seam accepts a non-null validation delegate. A scope-valid
proposal that leaves any production validation issue restores exact before
images and returns `ValidationFailed`; no runtime empty-validator path exists.

The durable generation also owns every complete logical GM flow through an
immutable session operation. Its binding contains the normalized canonical root,
the expected generation, sticky replacement state, and a closed flag. Every
canonical mutation verifies that binding after recovery and under the canonical
write lease. Terminal polling verifies it before reading completion signals, and
the outer scope verifies it once more before returning. Nested same-generation
work reuses the binding; a conflicting generation, replacement, or escaped task
after scope closure produces typed `SessionReplaced`. The client must not hold
the lifecycle lease while waiting for the GM. The lifecycle lease is short,
serializes only load/New Game replacement, and precedes the canonical lease in
the global lock order.

Detached worker runtime lives outside the replaceable game session. An absolute
`BOE_WORKER_RUNTIME_BASE_PATH` may select its base; otherwise the client derives a
platform base and separates sessions with a canonical-path hash. The bounded
handoff data model admits at most 1 MiB of proposal JSON, 4 MiB per `contentRef`,
16 MiB of aggregate imported content, and 65,536 characters of captured output
per stream plus a truncation marker.

`WorkerProposalStatus.Unspecified=0` is never a completion state. JSON `status` is required; omission fails deserialization, while direct unspecified/unknown models fail contract validation and apply. Only explicit `status=completed` proposals may reach the apply path. `failed`, `timed-out`, and `rejected` proposals carry no `changedFiles` and remain diagnostic records. Worker audit lines use one lock-protected read-and-append operation so concurrent writers preserve every event. Every producer calls one shared generator with `worker_audit_<UTC yyyyMMddHHmmssfff>_<32 lowercase hex GUID>`; a source guard forbids hand-built prefixes elsewhere, and audit publication failure is telemetry loss that cannot revoke an already accepted canonical commit.

Repair handoff has one owner state. Before dispatch, request/ready/stall artifacts from older cycles are removed. `Applied + ReadySignalCreated` enters the correlated ready path; `Applied + !ReadySignalCreated` records `worker_apply_gate_accepted` and immediately revalidates; every non-applied result writes one fresh legacy request. Player-facing output freshness retains the original repaired canonical target set through all derived output-only retries and requires every output write to be strictly newer than the latest actual write of every retained target, including worker-only cycles where no legacy request exists. Equal timestamps are stale; rewriting an original target recomputes the boundary and re-stales older output. Request creation, dispatch, and repair-loop start timestamps are not canonical mutation boundaries; an unobservable required target mutation fails freshness closed.

`WorkerFileChangeKind` is a closed operation authority: `Unspecified=0` is invalid, and only explicit `Add`, `Replace`, or `Delete` reaches apply semantics. Omitted or undefined numeric values reject during proposal contract validation.

Every `game_state/meta/` validation-repair target is afterlife-scoped, including non-actor metadata. `game_state/meta/soul_state.json` is an exact-byte, hash-pinned, read-only realm-authority context file. Strict parsing requires one supported `currentRealm` without duplicate members. The authority file is never an allowed proposal path; mixed Mortal/meta batches and missing, malformed, unsupported, changed, or contract-mismatched authority fail task construction or apply closed. `game_state/misc/characteristics.json` follows the same read-only rule in every empty-characteristics task, including mixed Mortal batches.

## Phase 35 authority additions

### Generation-bound prompt snapshot

A mutable browser prompt snapshot adds one immutable
`ExpectedSessionGeneration`. The generation is captured under the same
canonical authority that acquires the local-UI lock. Submit and cancel may
complete only inside that binding. Replacement removes the stale in-memory
snapshot but does not release or inspect the replacement session's lock.

### Ephemeral local-UI lock

`game_state/control/local_ui_session_lock.json` remains a client-owned
single-link canonical file containing owner, lease, heartbeat, and operation
metadata. It is never save data. Public operations are wrappers around one
generation-bound canonical lease and the lease-aware lock methods.

### Typed opened publication object

An opened publication source carries an expected object kind
(`RegularFile` or `Directory`). Physical `Directory` metadata must match before
rename. An existing replacement destination is separately opened under the
same stable parent and must be a single-link regular file.

### External Daren profile authority

The Daren reward profile has a stable external parent authority and exact-byte
operations: optional read, create-only temporary write plus opened-handle
replace, optional exact before-image, exact restore, and opened-object delete.
No operation follows reparse points or accepts a multi-link regular file.

### Legacy characteristics classification

`RequiresCompleteCurrentMortalPersonality` remains the operation classifier for
current first materialization and true legacy promotion. Empty
`characteristics` is an error only when that classifier is true. Numeric value
shape validation still applies whenever characteristics are present.

## Phase 36 authority additions

### Browser QTE interaction token

Every QTE state that permits mutation publishes an opaque token with server-side
meaning:

- immutable session generation;
- interaction kind (`offer`, `practice`, or `daren`);
- exact offer/attempt identity;
- current revision.

Every accept, decline, action, retry, exit, or Daren request presents that token.
The server compares it with the current interaction before reading action data
or mutating canonical/QTE state. Starting or retrying an interaction produces a
new revision and invalidates older tokens.

### Prompt construction binding

A prompt build context contains the generation captured before command-result
construction. Attachment accepts that generation as input and never substitutes
the current generation. Stale snapshot lookup performs generation comparison
and atomic removal before owner, field, or answer validation.

### Local-UI lock lease identity

The lock record and acquisition result add one unique `leaseToken`. Lock
identity is the tuple `(sessionGeneration, ownerId, leaseToken)`. Refresh and
release require the complete tuple. A successful session replacement strips the
old lock as part of replacement and does not perform a release against the new
generation.

### Ephemeral lock namespace

`game_state/control/local_ui_session_lock.json` denotes a forbidden persistence
namespace, including the exact node and descendants. Replacement cleanup treats
the node as an untrusted file-system object and removes its file, directory, or
reparse shape without traversing a reparse target.

### Completion-validated stable read

A stable read retains:

- opened file handle;
- retained parent authority;
- expected full physical path;
- expected regular-file kind.

The consumer calls a completion gate after consuming all bytes or archive
entries and before using the result. The gate repeats path, kind, and single-link
validation. Integrity failure is distinct from optional absence.

### Transactional physical replacement

One replacement operation owns:

- retained source handle and source parent;
- retained existing-destination handle and destination parent when present;
- exact baseline destination identity/bytes or exact absence;
- expected post-publication identity;
- deterministic rollback state.

The operation validates at the publication boundary, commits one complete
replacement, and validates again before success. Any raced source/target/link or
post-publication failure restores the exact baseline or absence before returning
failure. Unsupported platforms reject before mutation.

### Daren post-image ownership

The Daren rollback manifest records baseline identity/bytes, transaction parent
identity, and the exact published post-image identity/hash. Recovery restores or
deletes only when the current profile is the transaction-owned post-image;
otherwise it preserves evidence and fails closed.
