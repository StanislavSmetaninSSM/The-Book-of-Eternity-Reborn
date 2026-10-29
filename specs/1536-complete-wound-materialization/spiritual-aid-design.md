# T108: safe player aid to the current Guardian

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Status: bounded DESIGN accepted 8e279e8c. Participation issuer/witness exact13
runtime cases have independent OUTCOME ACCEPT. The full selected player→current
Guardian slice now has 17 distinct PASS across explicit fb/8f/3d/34b lineages:
actual console grant43→cleanup→guided aid44 (d20=14, II/progress2), queued browser
equivalent (d20=18, healed), strict backend/profile/ordinary/SELF/Shining consumers
and protected-failure controls. Independent Sol OUTCOME ACCEPT covers all 17;
Astra OUTCOME covers locality/profile and aid-backend, with optional happy-caller
crosschecks, not the last browser-fault singleton. Source/provenance/limits are
recorded in plan.md. The bounded current Guardian→player reverse slice below now has17 PASS across
explicit04c/c6 lineages, including actual player confirmation in both interfaces;
Sol OUTCOME17 and bounded Astra backend/console OUTCOME are accepted. Wider
helpers remain open.
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

## Next bounded slice: current Guardian helps the player (DESIGN accepted dd41fb931)

Tracked by T108/T121, continuing the accepted first direction. The player asks
its current visible Guardian for free private help with a known active spiritual
wound. An ordinary accepted GM turn records the Guardian's willingness and the
current conscious/capable conditions. After normal cleanup, `/лечить` offers
that Guardian's help for the player's wound. The player can cancel without a
request/draw or explicitly confirm; confirmation submits the actual console or
browser Engine action, which applies active Guardian healing followed by the
player's natural recovery exactly once. This is not the public/paid US6 service.

Chosen approach: extend the same typed participation/profile/result family and
shared active pipeline. A separate helper registry/publisher would duplicate
fresh checks and world-cycle ownership; copying target consent to the player
would let GM DATA substitute for player choice. Both are rejected. The existing
v1 record remains the still-produced player→Guardian target-consent form. A new
closed schema2 provider-offer/revoke form lives on the Guardian profile and names
that exact Guardian healer, exact player_soul target and stable player wound.
The record's enclosing owner is the Guardian, not the treatment target. It states
provider willingness and conscious/capable conditions; it never asserts player
consent. The exact closed field sets and confirmation binding below follow the bounded
independent design recommendation; no new consent policy is inferred.

Closed forms for the new direction:

- Proposal schema2 on the exact Guardian profile: `schemaVersion`,
  `kind=provider_offer`, `operation=offer|revoke`, `targetId=player_soul`, `woundId`,
  `providerConscious`, `providerCapable`, `targetConscious`, `targetCapable`,
  `readableEvidence`. Healer identity comes from the enclosing profile. All four
  factual flags must be true for an offer; no player-consent field is admitted.
- Record schema2: `schemaVersion`, `kind=provider_offer`, `status=offered|revoked`,
  `healerKind=guardian`, `healerId`, `targetKind=player_soul`, `targetId=player_soul`,
  `realm=chaos_sea`, `woundId`, `locationId`, the four factual flags,
  `readableEvidence`, `binding`, `acceptedCycleOrdinal`, `eligibleCycleOrdinal`,
  `primaryTransitionId`. The actual owner derives provenance, cycles and identities.
- Natural result v4 contains exactly one schema2 full provider-offer outcome.
  Active request/result v4 require `mode=aid`, `direction=guardian_to_player`,
  exact offer witness/source and explicit `playerConfirmation=player_target_confirmed`.
  V4 with missing/unknown confirmation/direction or v2/v3 provider payload refuses.
- Selection has an explicit v4 `mode=receive_aid` with exact healer/target/wound/
  content identity and `confirmation=required|player_target_confirmed`. The menu
  emits only `required`. The existing ConfirmAsync returns the confirmed form
  after its fresh choice equality check. PrepareQueued and active staging reject
  an unconfirmed receive-aid selection. The marker remains comparison DATA;
  only the real UI one-shot/queued action supplies the event and actual original.

A provider offer must be issued by the actual natural owner with the same exact
current Guardian/Abode proof and accepted session/request/token/turn/cycle/primary
binding. The player's wound must be in the full original natural cohort; the
Guardian need not itself be wounded. Store the full offer plus content digest in
the same atomic profile + natural History result. Grant-turn natural recovery
may change wound fingerprint, so the offer binds stable woundId while the later
selection/request binds its fresh semantic fingerprint. Eligibility is the next
safe cycle after the actual offer turn, and this first journey uses the immediately
preceding accepted turn, just as the first direction. No new receipt file.

Use explicitly closed v4 natural-with-provider-offer and v4 active request/result
for guardian_to_player. Continue actively producing v2 self/plain natural and v3
player→Guardian aid/target-consent natural. Reject cross-version payloads,
GM-authored player-consent fields, empty/unknown outcomes and mixed unrelated
participation changes. Do not add a legacy schema fallback.

Real confirmation is issued by the existing shared ConfirmAsync path: console
confirmation stores the private one-shot exact action/generation selection;
browser confirmation queues the exact selection through its current bound
transaction and action/generation/source binding. A serialized comparison field
cannot create that event. Staging runs only from this actual player action and
binds it into the retained request before signed snapshot capture. B checks the
actual signed request/original, not a GM assertion. Cancellation never creates
this selection/request. The strict committed offer reader still runs before
BuildControl and before input overwrite; B reuses the frozen original witness
plus fresh current closing, and settled replay resolves the historical offer
without requiring live consent, locality or its old settled schedule again.

Use the exact current Guardian coordinate in the HEALER role for locality, art
and active roll effects. Use player_soul as TARGET with its own natural tier and
soul/profile mirror. One shared scalar stage program/executor performs active
then all eligible natural ticks; no second universal pass. Existing owned two-path
normalization and full profile projection apply unchanged to the actual owner.
Old SourceRoots/Input/fingerprints remain frozen. Current revocation, loss of
conscious/capable condition, move, invalid art or changed wound blocks staging
before entropy, and after a draw refuses publication with that same retained
attempt/dice. No implicit consent from UI visibility, friendship or teaching.

Acceptance is finite and user-facing:

1. A real accepted offer turn naturally advances the player's III/H0 wound, then
   normal cleanup preserves the full provider-offer witness. No manual receipt,
   between-turn seed or resource/scheduler rebootstrap is allowed.
2. Both interfaces show Self first and the current visible Guardian route with
   safely rendered context, no technical IDs. Cancellation makes no request,
   draw or cycle. Actual confirmation drives offer43→cleanup→aid44 through Engine.
3. Unequal Guardian H3 / player H0 proves active healing uses Guardian art/effects
   while natural recovery uses player H0; one attempt, one cycle, exact graphs,
   wound/effects/History, no private-treatment charge or repeated natural tick.
4. Missing/raw/unaccepted/revoked/expired/foreign offer and unconfirmed selection
   refuse before draw. Current provider/target condition and locality are fresh;
   post-draw revocation/move refuses without reroll or partial publication.
5. Exact retained request/original and settled historical offer linkage reject
   altered content, wrong identities/version and omitted prior result. A later
   live offer change does not invalidate an already settled aid result.
6. Existing self and player→Guardian affected consumers remain covered according
   to the final dependency-based selection; no automatic old-suite rerun.

Implementation order: accepted design → minimal causal existing-API control →
closed offer/result producer → shared staged/B/History role binding → shared
choice/confirmation + actual console/browser journey → GM CLI/Matrix/Wound
contract/example/manifest guards → coherent SOURCE/PREP → fresh owning build and
reviewed exact categories → independent OUTCOME. No intermediate reader-only or
math-only build, no new actor framework. Provider/target parsed profiles, catalog,
History and locality DATA are reused within the existing operation owner; actual
lease/original/current closing reads remain fresh. No speedup is claimed.

Scope remains safe Chaos/current persistent Guardian, free private conscious and
capable cooperation. Paid/public services, combat, unwilling/helpless exceptions,
other entities, Shining helper locality, longer offer lifetime, provider/fresh-
process recovery and existing Mortal/T609/DFR/Windows limits remain open.

### Reverse implementation checklist (inline, existing tracked plan)

Source/test/GM steps below are compiled and exact17 PASS are recorded in plan.md.
The original codec RED and final Sol OUTCOME17 are independently accepted;
Astra separately accepted reverse backend/console only. Initial fixture-only seed failure and explicit retained11+fresh6 lineage
are preserved, not called a production RED or all17 rerun.

- [x] Add `GuardianProviderOfferNamesThePlayerWoundWithoutIssuingPlayerConsent`
  to SpiritualTreatmentParticipationTests. It calls the existing ReadChanges and
  CreateOutcome APIs with a schema2 offer on a preexisting Guardian profile;
  exact player target and full offer record must be admitted, player-consent
  fields and incapable offer refused. The current closed v1 parser must reject
  the positive offer first. Literal unit singleton/60, no Main dependency rebuild.
- [x] In SpiritualTreatmentParticipation and planner Participation partial,
  implement exact schema2 typed fields, enclosing Guardian record ownership and
  player-cohort validation; natural result v4 is selected only for that payload.
  Existing v1/v3 producer remains closed. Reuse the same atomic assembler.
- [x] Extend SpiritualSelfTreatmentSelection, request codec Aid partial,
  AfterlifeWoundTreatmentAuthority.Aid, persisted result, History and replay
  reader for explicit confirmed receive_aid/v4. Healer coordinate/profile/locality
  is Guardian, target is player; original and current source18 remain exact.
  The scalar/executor/common publisher stay shared. Add typed full-publication
  offer→confirmed aid, unconfirmed no-draw, changed offer/provider and historical
  omission controls in the existing natural-recovery integration fixture.
- [x] Extend WoundApplicationService choices/ActionFor/ConfirmAsync and existing
  console/browser selection consumers. Menu candidate is unconfirmed; only the
  actual Confirm path emits confirmed DATA. PrepareQueued and Stage reject an
  unconfirmed receive-aid selection. Actual console and queued browser tests use
  the existing two-turn Engine fixture, sole bootstrap/complete Guardian/lore,
  native completion, real cleanup and NPC Brain/journal. Assert cancel/no intent,
  exact request4, healerH3/playerH0, one cycle/attempt, graphs/effects/History,
  no cost and strict replay. No fabricated accepted result or between-turn seed.
- [x] Synchronize CLI_Step_Main, Afterlife_Contract_Matrix,
  Wound_Materialization_Contract, E_CLI_Afterlife_Turns and manifest/guided guards
  with one two-turn provider-offer/real confirmation example. Existing Mortal
  and public-service contracts have no permission change.
- [x] Freeze coherent source/tests/docs, independent SOURCE/PREP and exact
  dependency-based category selection, owning Integration/Main/Support plus Unit
  refsfalse, all owning pins, official PlanOnly then separate actual categories
  under existing 60/120/180-second budgets. First failure gets exact causal
  classification before another dependent run. OUTCOME/readback closes only
  this private free current-Guardian reverse slice; full T108/US4 remains open.

Ruling: canonical #1536 design/plan/tasks are the progress ledger and existing
qualification checkout is the isolated task branch. Do not add a duplicate skill
plan/worktree or run an unrestricted suite; repository/user workflow explicitly
requires these existing artifacts and bounded official category verification.

## Next bounded slice: player helps a current Abode resident — DESIGN WIP

2026-10-29; T108-RESIDENT, #1536. No implementation or runtime qualification yet.
US4 scenario8 and data-model12 already authorize a reachable consenting entity;
no new game rule is needed. A read-only Astra/high architecture consultation at
248263534 identified the exact kind/locality and historical-family boundaries.
Independent Astra/high DESIGN review found two P2 corrections: separate live joins
from historical agreement and explicitly cover every changed authority branch. The
corrections were independently accepted at94a95321b. Implementation/runtime remain
UNRUN; first existing-API causal scaffold does not claim live grant authority.

### Player result and finite boundary

After an ordinary accepted GM turn records a visible, conscious, capable resident's
consent, the player chooses that resident's known active spiritual wound through
`/лечить`, confirms and completes the actual console/browser Engine turn. Reuse
real grant → natural/common acceptance → normal cleanup → guided aid → Stage/B →
common publication. One selected recipient per grant turn, the immediately next
accepted turn and existing next-safe-cycle expiry remain unchanged. Self stays
first. No player-authored JSON, fees, new receipt/registry/publisher, longer consent,
Shining locality, resident-as-healer, combat or helpless/unwilling exception.

Healer coordinate is exactly (chaos_sea, player_soul, player_soul,
AfterlifeProfilesPath). Target coordinate is exactly (chaos_sea, resident, exact
residentId, AfterlifeProfilesPath). Identities are ordinal and kinded; a Guardian
with the same ID cannot stand in for the resident. Do not impose a new global
ID-uniqueness rule across different kinds; reject actual ambiguity/substitution.

### Closed new family; currently produced old forms stay exact

The following new shapes are technical DATA, never independent permission:

- Proposal schema3 on an exact existing `resident` profile:
  `schemaVersion`, `kind=resident_target_consent`, `operation=grant|revoke`,
  `healerId=player_soul`, `woundId`, `conscious`, `capable`, `readableEvidence`.
  Target full coordinate comes from the enclosing exact profile, healer coordinate
  from the restricted current family. The client derives locality/provenance;
  the GM cannot supply canonical stamps. Grant requires both factual flags true.
- Canonical record schema3, closed fields: `schemaVersion`,
  `kind=resident_target_consent`, `status=granted|revoked`, `healer`, `target`,
  `woundId`, `locality`, `conscious`, `capable`, `readableEvidence`, `binding`,
  `acceptedCycleOrdinal`, `eligibleCycleOrdinal`, `primaryTransitionId`.
  Healer/target are existing OwnerJson four-field coordinates; locality is exactly
  `{guardianId, abodeId}` from the authenticated current join. Binding/cycles/
  primary transition use the existing accepted natural owner. Full record and
  digest remain in the same participation outcome; no final-result hash cycle.
- Selection schema5, closed fields: `schemaVersion`, `mode=aid`,
  `direction=player_to_resident`, `woundId`, `expectedWoundFingerprint`, `healer`,
  `target`, `locality`, `participationFingerprint`. Both coordinates/locality must
  match record/source exactly. Add narrow typed resident participant DATA to the
  existing aid selection; no optional missing-kind inference. Existing v3/v4
  serialization and meanings remain byte-exact. Conflicting receive-aid/player-
  confirmation flags cannot coexist with this direction.
- Request/result family5: active mode=aid, direction=player_to_resident; natural
  result mode=natural contains exactly one full schema3 participation outcome.
  Reuse the existing closed active fields, with explicit direction. Full healer/
  target coordinates and full witness record bind locality into request/attempt/
  result fingerprints; selection locality must match witness/source. No separate
  request-locality copy is necessary. V5 self, missing/mixed fields, cross-family
  substitution and unknown versions refuse. Existing actively produced v2 self/
  natural, v3 player→Guardian and v4 Guardian→player remain exact current contracts,
  not legacy-save fallbacks. Their immutable History rows/fingerprints are never
  rewritten while appending v5 results.

Change DATA must retain its full enclosing WoundOwnerCoordinate. Coalesce mirrored
profile proposals by the full coordinate and identical proposal, never TargetId
alone; reject conflicting mirrors and more than one distinct bounded change.
A TargetId convenience accessor may remain, but cannot be a permission/dedup key.
All three raw profile representations and direct/null canonical-record forgery
checks remain mandatory. Unchanged accepted records may be retained without a
regrant. Revocation never grants permission and must match the same typed owner.

### One exact resident join, used by every existing owner transition

Add the narrow typed DATA join inside SpiritualTreatmentParticipation, using the
existing exact current-Guardian/Abode check. Require exactly one case-unambiguous
resident roster entry and exactly one typed resident profile. Require exact
residentId/actorId/actorRef agreement, guardianId and abodeId agreement with current
navigation/Guardian, typed isPresent=true, Chaos realm, profile locationId and exact
wound owner/carrier. Reject wrong/missing/confusable identities, alias conflicts,
foreign/departed targets, hidden profile/wound or active conflict before a grant/
new aid can authorize treatment. Read current factual participation conditions;
visibility/friendliness/teaching/presence alone never means consent.

LocalActorIds, IsAfterlifeActorLocal, FindResident and CollectEntries are useful
context only: their case-insensitive/alias/first-match normalization cannot prove
this treatment join. Both locality roots already belong to AidLocalityPaths;
there is no new source path or source registry. Perform the live exact join in:

1. Real accepted natural participation producer and pure afterimage composition.
2. Menu/confirmation, committed-grant reader after genuine cleanup, and pre-control
   admission under the actual existing single staging lease.
3. Signed-original B/RequireOriginalAidWitness and fresh pre-draw/publication checks.
4. Owned locality normalization before and after the computed write: full typed
   target/Guardian/Abode/presence/profile join must remain equal. Equal untyped
   LocalActorIds sets alone are insufficient. Original snapshots/input/evidence
   remain frozen; only the verified computed live closing image may advance.

Historical agreement is a separate structural contract, not the live roster join.
The persisted result and WoundHistoryState link full typed coordinates, recorded
locality, witness/content fingerprint, exact previous accepted grant result,
request, stages and accepted binding. They cannot reconstruct an old roster after
ordinary cleanup and must not read current presence or require renewed consent.
Use the existing AidGrantAgrees/History agreement path, not a live-check bypass flag.
RequirePublishedRecords still proves the exact current typed profile and accepted
record agree during current settlement; it must not add a blanket live-roster check
to every replay caller. ReadPublishedSpiritualCycleDataCoreAsync retains all current
source/control/schedule after-image, complete cohort and identity/History checks.
A later turn that changes these after-images is not automatically replayable as
that old current settlement. Historical receipt validity does not override them.

New admission always rechecks live consent/locality. Raw same-turn stamps, earlier
unrelated natural results and prepublication grants remain insufficient. Expiry/
turn/session/cycle rules are unchanged; neither a result's own token nor serialized
selection authenticates it.

Use player H and active roll effects for healing; resident H for its one ordinary
natural pass. The same program applies active then the whole eligible natural
cohort. For playerH3/residentH0, a one-step III→II active result followed by natural1
must leave II/progress1, not use healerH3 to recover four points. Weaker graphs are
still GM-authored and client-validated against the computed destination; no new
survivor selection or replacement rule. Late failure retains attempt/dice and the
existing rollback/uncertainty semantics, with no partial common publication.

### Touchpoints, optimization and finite acceptance

Extend existing selection/family dispatch; SpiritualTreatmentParticipation;
planner Participation/LocalityNormalization; AfterlifeWoundTreatmentAuthority
committed and signed Aid paths; SpiritualSelfTreatmentRequestCodec.Aid;
SpiritualHealingPersistedResult; ProgressionScheduleService.SpiritualReplay;
shared request projection, menu/action and pending console/browser consumers.
RequireOriginalAidWitness and the committed-grant reader use the typed family and
live join. RequirePublishedRecords uses full typed owner/profile agreement, while
historical result/History parsing uses only the recorded structural link described
above. Preserve exact old family dispatch rather than infer versions from ReceiveAid
booleans everywhere.

Reuse already parsed catalog/profile/History/locality inputs owned by the existing
operation and actual lease. Resident join DATA has full participant, wound/FP,
record-content, Guardian/Abode and original/current source-image dependencies;
mutation invalidates it. It grants no authority across leases or generations.
Keep fresh original, physical closing, source/effect/history, permission and current
session checks. No new cache/dictionary framework: small typed joins are trivial,
and existing operation/service reuse remains the owner for expensive stable DATA.
Tests use isolated mutable roots and detached template copies. No speedup claim.

Finite acceptance maps each changed branch below. Exact discovered row counts are
recorded after test authoring; fifteen cases were an incomplete draft, not a cap.
Use cheap parameterized DATA controls and the existing backend fixtures for the
additional branches; retain just two expensive guided journeys.

| Branch | Minimal proof / existing owner |
| --- | --- |
| Closed family | Typed proposal/record/selection/request roundtrip; reject mixed/unknown/v5-self and cross-family substitution. Existing v3/v4 serialization/fingerprints stay exact. |
| All raw profile representations | Parameterize profiles/afterlifeEntityProfiles/afterlifeEntityProfileUpdates; identical full-coordinate mirrors coalesce; conflicting mirrors, direct changed canonical record and explicit-null forgery refuse. |
| Kinded identity | A legitimate Guardian and resident may share an ID. The resident alone is accepted; wrong-kind substitution refuses; one valid proposal for each is two distinct changes and must hit the bounded-one-change refusal, never ID-only coalescing. |
| Resident authority join | Cheap exact identity/realm/carrier/duplicate/confusable/alias/foreign Guardian or Abode/departed/hidden controls; no generic presence/visibility consent. |
| Revocation | Schema3 revoke accepts false factual flags while grant with either false flag refuses. Existing backend publishes a genuine revocation after a genuine grant; aid in the revocation's otherwise eligible next turn/cycle refuses without draw, so expiry cannot explain the refusal. |
| Consent lifetime | Family5 aid rejects wrong session, same grant turn, skipped next turn and wrong next cycle before draw; test the family5 dispatcher explicitly rather than infer coverage from unchanged v3/v4 tests. |
| Owned normalization | Actual normalizer retains an unchanged typed join while cosmetic bytes change. A changed exact resident/Guardian/Abode/presence binding refuses even when generic LocalActorIds are unchanged (for example case-only roster Guardian substitution). Original evidence stays frozen. |
| Fresh signed B and publication | Existing backend stages once, then separately drifts consent and locality. Signed-original/publication admission refuses, keeps the original request/die and performs no additional draw or partial common publication; snapshots account explicitly for the injected mutation. |
| Historical agreement and current settlement | Actual producer/result joins exact grant/request/stages, mixed v3/v4 receipts remain byte-exact. Parse its persisted History after current roster locality changes: recorded historical link stays valid, while new live admission refuses. Separately, immediate unchanged current-settlement replay remains inert; do not claim old scheduler after-images survive a later accepted turn. Missing/changed historical grant refuses. |
| Atomic failure | Existing common publication fault restores grant/request/wound/effects/History/scheduler with retained attempt/die under the existing rollback contract. |
| Actual user result | Console and shared browser each run genuine grant → normal Engine cleanup → confirmed aid, unequal player/resident H, exact target and GM-authored weaker graphs, one cycle/attempt, no charge and immediate settled replay. |

Seed the complete existing two-turn Engine fixture once, including resident roster/
profile/wounds/effects/lore/Brain and scheduler as required; no post-acceptance file
insertion or second bootstrap. Backend boundary controls may share one accepted
fixture's immutable parsed evidence but never mutable state between tests.

First causal RED uses the existing ReadChanges API with a complete valid resident
proposal; current Guardian-only admission must refuse the positive case. Then
implement the full typed family/owner connection with cheap controls before actual
journeys. Review the entire known fixture preparation before an expensive caller.
Synchronize canonical data-model technical forms, CLI/daemon/Wound guide/Matrix,
a worked two-turn resident grant/aid example and exact manifest/documentation
consumers with implementation. Mortal and public/paid service contracts do not
change. Fresh affected builds, category discovery/PlanOnly, independent SOURCE/
PREP and OUTCOME use bounded existing-style limits, with no broad successful-suite,
DFR/Mortal/Windows/performance replay as a prerequisite for this functional slice.
