# T108: safe player aid to the current Guardian

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Status: bounded DESIGN accepted8e279e8c. Participation issuer/witness SOURCE
acceptedef406058 and exact13 runtime cases PASS; independent OUTCOME ACCEPT.
Active aid staging/draw/result/UI and full Engine grant remain implementation work.
FR-054/FR-074 already require consent and fresh access. Technical decisions need
no new user approval pause. Full T108, combat/helper services and #1536 remain open.

## User result and exclusions

The player offers to heal the current Guardian through an ordinary in-game action
and asks that Guardian for permission to treat its wound.
The GM records that Guardian's affirmative participation through the existing
profile update contract. Once that turn is accepted, the existing guided
`/лечить` lets the player choose that exact visible Guardian's known active
spiritual wound, review the safe-cycle quote, confirm and execute a real Engine
turn. The player never authors JSON. Cancel, absent consent and stale targets
produce no healing draw or treatment intent.

First scope is player → current persistent Guardian in an ordinary safe Chaos Sea
Abode. The target is conscious/capable and explicitly consenting. No implicit
consent from friendliness, visibility, faction, presence or `canTeach`/`canFight`.
Paid services, other NPC healers, unwilling/conflict aid, helpless-target exception,
other target kinds and Shining helper locality remain unsupported explicitly.
Entity → player will separately require the player's real guided confirmation;
a GM-authored player consent substitute is forbidden. Existing self and ordinary
natural routes keep their current behavior.

## Existing machinery and missing source

- `SpiritualHealingResolver` already separates Healer/Target and prepares actor/art/
  effect DATA. Reuse it, the existing stage/draw protocol, Continuation, ordered
  wound/effect program, common publisher and strict History/result readers.
- `LocalInteractionScopeService.ResolveAsync(lease)` resolves Chaos locality from
  soul state, `guardians.json` navigation/current Guardian/Abode and
  `GuardianAbodeResidentState.StatePath`; training and trade already recheck it.
  Require a unique exact current Guardian and matching persistent profile/wound,
  not the convenience alias/name fallback alone. Visibility and known wound
  projection remain separate from locality and mechanical cohort eligibility.
- No existing non-Mortal consent/conscious/helpless contract was found. Mortal
  reachability/consent evidence can inform DATA semantics, never grant afterlife
  permission. Add one bounded domain record to the existing target profile;
  do not add a registry, receipt file, permission cache or general actor framework.

## Participation authoring and committed witness

Proposed names are `spiritualTreatmentParticipationChange` for a nested GM proposal
and `spiritualTreatmentParticipation` for the canonical target-profile record.
Both are closed versioned shapes. All three existing representations (`profiles`,
`afterlifeEntityProfiles`, `afterlifeEntityProfileUpdates`) must pass the same
validation; direct/full profile replacement cannot bypass the command rule.
An unchanged existing canonical record is retained without regranting it.

The GM proposal names grant/revoke, exact healer and wound, conscious/capable target
state and readable evidence. Target identity is resolved against the enclosing
profile and actual wound. Canonical grant tuple, cycle coordinates, primary
transition and content fingerprint are client-derived by the actual accepted
owner from its real manifest/control. Supplied canonical stamps cannot mint a
grant. Normal profile upsert alone is not an accepted issuer.

The canonical record binds exact healer + target owner/realm + stable woundId,
scope/location, status and accepted provenance. Wound semantic FP belongs to the
separate fresh selection/attempt; a lawful natural tick during the grant turn must
not immediately invalidate consent. Grant at accepted shared cycle10 permits
exactly the next safe cycle11, not preturn9 or current10. Missing, malformed,
expired, revoked or mismatched content never becomes implicit consent.

For this wounded-Guardian safe grant turn, the existing natural cohort/publisher
already produces a client-owned spiritual result. Extend THAT existing family
with a closed typed participation outcome: the FULL closed canonical record,
including exact identities/scope/status, plus its derived content digest, linked
to the deterministic primary recovery
transition and actual binding. Publish profile grant + witness + natural effects/
History/scheduler atomically in the same common plan. Do not bind only a tuple
that could be copied onto another record. Do not create a separate receipt.
Avoid a hash cycle: record references stable primary transition/binding; the
result contains the full record and its digest; the record does not contain the
final result FP.

The strict committed grant reader proves matching result, current cohort/identity/
History, real schedule and exact record content before draw. A raw same-turn grant
or failed/prepublication proposal lacks that witness. Current revocation or move
must still refuse before aid publication. Settled aid replay validates the already
accepted aid result and original binding, not a newly granted live consent.

### Staging order and actual lease

The existing strict published-cycle reader requires consumed command/report
absence and the exact accepted schedule afterimage. Actual successful Engine
cleanup removes input/turn_request.json. The accepted cleanup seam therefore
selects the unique v3 natural result from actual current History by real Engine
session/nextTurn-1 and primary row/full record, parses History once, then applies
the SAME strict current settlement proof. The result's own token/projection is
not an independent expectedOriginal. If the preceding request is present, require
exact agreement; its normal absence is a guarded input. Never retain or recreate
the deleted request solely to make a fixture pass. Next-turn control preparation
changes pending fields, and aid B later has new commands/report. Do not weaken that
reader or add a bypass flag. Resolve the aid selection before BuildControl, inside
StagePlayerTurnBodyAsync's existing try/rollback scope after preturn backup and
TurnRequest construction. Hold ONE actual aid staging canonical lease through
strict committed-grant read, control preparation, fresh witness/profile/locality
and source checks, then draw/stage. Release it before existing baseline capture.

Add a narrow lease-aware overload/internal core to the existing BuildControl,
plumbing the same lease through its actual read/write helpers and retaining ONE
control calculation. Do not invoke the old no-lease method under a held lock or
copy the Chaos control algorithm. The non-aid branch retains its existing path.
Carry only comparison DATA within this owned operation; validate the exact
client-prepared scheduler delta before draw. At B use actual signed-start aid
witness and fresh closing checks, not the old grant reader against new aid
commands/report. No new coordinator/cache, transferable publication capability
or cross-lease owner rebinding is introduced.

## One active family and one natural pass

Use one normalized active context with explicit self/aid variants in the existing
`wound_commands` path. Aid binds healer, target/wound/fresh FP, exact participation
content/witness, cycle and source identity into its request/attempt hash. GM
`spiritualHealing` projection must state aid and both actors without exposing
private player/UI internals. Missing/invalid lower graphs retains the same attempt
and draws through repair. Actual original family/proof selects the bound I route;
live kind, graph-field presence or a serialized participation DTO grants nothing.

Keep these currently PRODUCED forms as closed current contracts, not save
compatibility or migrations. The exact exhaustive writer/parser table is:

| Surface | Version / mode | Required content |
| --- | --- | --- |
| Self request | v2 self | Existing exact self request |
| Self result | v2 self | Existing active self result, no participation |
| Ordinary natural result | v2 natural | Existing natural result, no participation |
| Aid request | v3 aid | Exact healer, target/wound and accepted grant witness |
| Participation natural result | v3 natural | NONEMPTY bounded participationOutcomes, each full record + digest |
| Aid result | v3 aid | Exact accepted grant witness and active healer/target source, no new grant outcomes |

V2 writers remain active for turns without participation. Reject v2 aid or any
v2 participation payload, v3 missing its mandatory payload, unknown versions or
modes, absent-mode defaults, and self treatment mixed with a new grant/revoke.
Update active writers/parsers/fixtures/docs together; no legacy fallback branch.

`SpiritualHealingRequestProjection.BuildProgram` already applies active only to
the selected wound and then per-member natural recovery. Reuse that pass. Separate
active healer H from target/member natural H: healerH3 → GuardianH0 must not apply
four natural points to the Guardian. Extend the scalar input/binding check and
strict result rederivation, not a second executor. Preserve per-wound one-session/
cycle identity regardless of healer and the complete mechanical natural cohort.

## Ownership, data reuse and privacy

Every source path used by locality/participation belongs in the aid's actual
original snapshot mapping/hash and fresh current closing images; current16 self
paths omit Guardian/resident locality inputs. Keep default self reads unchanged.
Reuse the existing locality resolver's parsed DATA through a small pure input seam
where needed, rather than reread/parse soul/profile/catalog or add another cache.
Operation-owned DATA has full source/path/hash/identity dependencies; mutation
invalidates it. Real lease/generation/original/owner/response guards stay fresh.

Extend existing pending typed selection/phase/blob invariants for aid, including
both original tuple comparisons and terminal/accepted/settled phases. Exact staged
member path and byte-hash mode remain mandatory; prose/queued data cannot carry a
fake staged proof. Browser action cannot consume console one-shot selection.

Only current visible profile + known/public active wound is offered in UI. Hidden
NPC wounds remain in universal mechanical recovery without counts/details leaking
through target selection. Render safe existing text; no raw IDs/tokens/client
metadata in confirmation or output. Current free aid slice introduces no fee or
compensation contract; quote the one safe cycle and bind every actual requirement.

## Finite implementation and acceptance sequence

1. Bind these concrete staging-order/version decisions through bounded DESIGN. Add causal
   controls for the real accepted profile issuer/witness, not just a valid-looking
   current stamp. Include copied accepted tuple with foreign record content,
   prepublication/rollback grant absence, and cross-version payload refusal.
   Make no user-facing completion claim from a codec alone.
2. Implement the closed proposal/record/result witness and existing natural grant
   producer atomically. Prove all three raw profile representations and raw stamp
   forgery/prepublication rollback paths. Earlier grant's natural wound FP change
   must remain compatible with later exact fresh selection.
3. Implement aid source/staging and shared program/result/replay. Cheap unequal-tier
   control proves active one-step + exactly1 natural point for targetH0 (for
   example III0→II1); wrong use of healerH3 would over-recover. Verify no draw for
   missing/revoked/expired/wrong healer/location/wound/capability and after-draw
   revocation/move refusal retains exact attempt/dice without partial publication.
4. Connect real guided console/browser selection/quote/confirm/cancel to the SAME
   Explorer/Engine. Controlled native-shaped GM journey: player offers aid through an
   ordinary turn, GM authors participation, accepted natural result witnesses it,
   then guided aid executes wound/effect/History and one new cycle. Include graph
   repair as actually required by its computed stages. No manual player JSON.
5. Synchronize CLI/GM daemon, Wound guide/Matrix, at least one worked two-turn GM
   example and exact manifest/source guards. Mortal has no new permission/format;
   document that exclusion. SOURCE/PREP selects fresh owning projects and small
   literal categories/old bounds, cheap before actual callers. Retain unaffected
   lineages; no full selection/DFR/Mortal/Windows retries. Require independent
   OUTCOME before describing this bounded user journey as complete.

Backend, producer and UI may be separate bounded checkpoints but must converge on
that actual user result. Full T108/provider symmetry, helpless/conflict routes,
Shining aid and broader producer qualification remain explicit follow-through.

### First backend causal checkpoint (test-only, genuine RED a3fee4bd6)

The exact singleton compiled and completed its intended semantic RED at missing
canonical participation after every natural-publication precondition below.
PlanOnly1/1 zero executed; actual1 complete FAIL, no timeout, both cleanup true.
The canonical grant/full v3 witness and helper paths are still unimplemented.
Full evidence and hash are recorded in plan.md; no unchanged RED repeat is needed.

Existing HealingFixture seeds a single current Guardian III/H0 wound and playerH3
before its sole resource bootstrap. This is lawful initial canonical create DATA,
not an earlier participation grant. Initial catalog/identity/History and real local
scope agreement must pass; the actual signed natural ResourceCore/common publisher
must first produce III1, one cycle and no treatment attempt/cost. Only then does
the test require the canonical participation and full v3 result witness. Missing
grant is the intended semantic RED; fixture refusal or failed publication is not.
The new literal spiritual-aid-participation-producer singleton/120s owns this
bounded control. Fresh Integration-only refsfalse against unchanged qualified
Main/Support, exact PlanOnly then one actual RED after independent SOURCE/PREP.
Existing defaults/bodies and production remain unchanged, so no prior reruns.
No shared mutable fixture/cache is added; the one actual common publisher is the
subject, and no repeated full-world setup or speedup claim is introduced.

After causal RED, implement in order: closed profile participation codec and
all-three-representation issuer validation; natural owner-bound full-record
outcome/profile contribution and v3 strict reader; pre-control aid staging and
separate healer/target program binding; guided console/browser plus actual Engine
grant/aid journey and GM synchronization. Preserve staged attempts through repair
and all fresh ownership/original/closing guards at each boundary.

### Producer SOURCE/PREP accepted ef406058; exact13 runtime PASS

The first code block extends only accepted participation issuance: real natural
owner/control/primary transition, immutable full record+digest in v3 result, pure
common carrier assembly and strict current published record agreement. V3 aid is
still intentionally refused until its active-family block is implemented. V2
self and no-participation natural remain actively written, not legacy fallback.

Raw validation inspects all three retained original response representations,
independently of the already computed canonical projection.
The existing exact actorId/actorRef identity is reused; command rows may obtain
unchanged realm from the existing profile. Identical mirrored proposals coalesce
as one DATA intent; conflicting mirrors refuse. Existing projection preserves an
omitted owned record and an earlier same-turn proposal across exact replacements;
explicit null/modified raw canonical record refuses. Actual owner/profile/wound/
locality checks remain necessary beyond those cheap projection DATA controls.

Cost assessment: reuse already parsed raw/current/previous profile inputs and the
existing locality calculation. Extra signed/current Guardian/resident reads occur
only for participation proposals; required fresh original/source closing is not
cached permission. No repeated catalog/control calculation, new receipt/cache or
second publisher is added. Pure afterimage assembly performs no new file I/O.
Core's earlier current.Guardians DTO carries no exact-byte read image/original
evidence. Proposal admission therefore freshly authenticates both locality images
and decodes its Guardian bytes once; byte equality lets the signed/current locality
check share that parse. The older owner DTO is not substituted for this proof.
Default non-participation paths only inspect property presence; no extra locality
read or outcome clone. No runtime cost or speedup is claimed before verification.

Executed after independent coherent SOURCE: fresh owning Integration/Main/Support
and Unit refsfalse; official four-category PlanOnly13 cases/in5 descriptors.
Participation-contract6/60 → producer2/120 → natural-publication4/120 → existing
authored-self-graphs1/120 all PASS in separate actual calls; no timeout/duplicates,
both cleanup flags true, all9 pins/exact13uniqueIDs verified. Timings/hashes and
limits are in plan.md; independent OUTCOME is pending.
The last two protect changed common assembly/result/replay and real SELF consumed-I
semantics; no browser/console/whole19/held matrix repeat. GM CLI/daemon/Wound guide/
Matrix/rules, worked nested proposal and manifest/source guards are synchronized.
No Mortal or Shining aid contract is introduced. Full user journey remains open.

### Active program causal control (test-only, UNCOMPILED/UNRUN)

Call the real existing SpiritualWoundRecoveryPlanner.Build on a valid Guardian II0
with real active H3/d20=8 one-step calculation and independent target naturalH0.
Expected single program: treat→I0, then recover→I1. Current equality guard rejects
the unequal tiers; this is the intended RED after canonical/formula preconditions.
Equal naturalH3 terminates, demonstrating why healer tier cannot leak into target
recovery. A forged calculation must still refuse after the shared program supports
independent tiers. This pure DATA control grants no healer/consent/turn authority.
One Unit singleton/60s, fresh Unit refsfalse against current Main/Support; no
artificial API, new scalar stack or repeat of the qualified producer13.
