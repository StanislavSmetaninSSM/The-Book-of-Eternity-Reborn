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

## Current bounded slice: player helps a current Abode resident — bounded Linux accepted

2026-10-29; T108-RESIDENT, #1536. Production source accepted at8547d5740;
final reviewed fixture runtimefcfe7cd60 completes actual console/sharedbrowser and
common failure rollback. Backendb3fe0243e proves H3/d20=14 III1→II2. UI rolls5/3
correctly retain III2 after active failure and ordinary H0 recovery. Both actors
retain real memory across grant43, cleanup and aid44. Final69 distinct cases span
the explicit source lineages in plan.md, not one fresh69 run. Independent final
OUTCOME is accepted. The existing-rule GM note guard also passes1/1 at62362c2ee;
T108-RESIDENT is complete within this bounded current-Abode scope, broaderUS4 open.
US4 scenario8 and data-model12 already authorize a reachable consenting entity;
no new game rule is needed. A read-only Astra/high architecture consultation at
248263534 identified the exact kind/locality and historical-family boundaries.
Independent Astra/high DESIGN review found two P2 corrections: separate live joins
from historical agreement and explicitly cover every changed authority branch. The
corrections were independently accepted at94a95321b. The earlier existing-API causal
scaffold alone did not claim live grant authority; actual grant/common publication
and guided callers are now covered by the lineages above.

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

### Resident memory on the same protected operation

Source consultation found a real boundary: meaningful resident Actor Brain reactions
must append first-person memory, but the existing resident journal shares the frozen
locality roster path. Writing ordinary residentThoughtJournalUpdates to that path
before preflight conflicts with full signed-original/current equality. Normalization
runs after preflight, so it cannot repair such an early source mutation.

Carry protected-turn commands in output/interface_updates.json under the narrow
spiritualResidentThoughtJournalUpdates array, using the existing resident journal
entry format. This distinct response field avoids reinterpreting the ordinary
GameResponse.residentThoughtJournalUpdates mapping or guessing a route from graph
presence. Add typed GameResponse/output-only transport and retain it in the existing
Engine response readers/refresh merger. Ordinary resident interactions keep their
current mapping. No new canonical path, memory schema, owner or journal writer.

The existing continuation retains detached response commands from the already parsed
and fully byte-guarded interface image. Only its actual resident record3/family5 can
bind the commands. Require exact intended resident and current turn, typed complete
entryId/residentId/turn/timestamp/title/summary plus existing optional journal text/
tags, unique nonconfusable new entry IDs, and no replacement of any old entry. Reuse
ActorJournalState normalization and GuardianAbodeResidentState.ApplyThoughtJournalUpdates
only after this append proof. Reject malformed/null/orphan commands, never ignore
or silently upsert an old entry.

Under the same live continuation/lease, feed detached commands into the normalizer's
existing current-turn resident projection and drift calculation. Publish with the
existing owned-locality hook, preserving the full typed join and exact expected
journal append. Bind detached before/after journal DATA once from the already parsed original
roster/response, only for this operation's idempotent normalization. Pure projection
adds no source reads or permission; the actual writer retains full fresh opening/
closing source/lease/physical checks. Original snapshots/request/dice/source FP stay
frozen; exact physical readback advances only the owned live closing image. Existing
post-normalization Actor Brain/memory validation remains mandatory. Accepted I
projects the protected field only for an exact published family5 response, same
actual original/control/cohort and complete original-roster→current-journal append.
The existing observed locality reader authenticates that original and its closing
evidence; current roster joins the bound physical closing set. Memory-only responses
are supported, but ordinary/orphan/unpublished/wrong-family fields stay rejected.
Original-dependent I is not rerun after cleanup; historical/current-settlement
readers keep their own strict documented proof. Fault acceptance
must include memory restoration under the real owning rollback route, not a fixture
writeback. Add cheap append/replacement/transport checks and actual grant+aid memory
assertions before claiming the user capability.

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
additional branches; retain two positive guided journeys plus one actual Engine
failure route needed to prove the new memory append is restored with the whole turn.

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


## Next bounded slice: resident offers private help to player — DESIGN accepted

2026-10-29, #1536/T108-RESIDENT-HELP. Independent Astra/high DESIGN accepted
after the sole P2 settlement-versus-live-join clarification; no remaining design
blockers. First existing-API causal singleton compiled and executed at35c5bc8ba:
expected unknown-proposal4 semantic failure, complete/cleanuptrue; GREEN and live
provider authority remain unimplemented/unqualified.
 US4/FR-042–054 and T108 already require
entity-to-player treatment using the common art, consent and world-cycle rules.
The player→resident slice is accepted ata4a03f54; the opposite direction is absent:
current provider_offer2 and receive_aid4 hard-code a Guardian healer. US5 full
nearby/privacy parity and US6 Elyara/public quotes/services remain open. Prioritize
this remaining P1 free private helper direction; no price, clinic, service-access,
compensation, Shining, combat, unwilling or helpless rule is invented.

### Functional result and implementation choice

A visible conscious/capable resident of the CURRENT Chaos Guardian Abode may offer
free private help for one exact known active PLAYER spiritual wound. The real
ordinary accepted offer turn produces its immutable witness and normal recovery;
normal Engine cleanup follows. Immediately next accepted turn /лечить offers that
helper; only the player's actual confirmation permits one treatment draw. Reuse
existing next-safe-cycle expiry, one recipient/change, one attempt, active helper H
then patient H natural recovery, no currency/item/OD, one world cycle, immutable
History, exact retry and common rollback. Cancellation/unconfirmed or withdrawn,
expired, insufficient, moved/hidden/wrong-kind provider refuses before side effects.

Reuse SpiritualAidSelection.Resident's existing complete Healer/Target/Locality
DATA with ReceiveAid=true and existing PlayerConfirmed. Its name does not force
the resident to be the patient. FamilyVersion is6 for Resident+ReceiveAid,5 for
Resident withoutReceiveAid; old Guardian3/4 and self2 serialized forms stay exact.
This avoids a second participant type/property/cache. New closed family6 is safer
than reinterpreting Guardian4 scalar IDs, which lose kind/locality. A general
public/healer registry would unnecessarily include US6 and is not selected.
These are implementation decisions within the already authorized game contract.

### Closed shapes and orientation

- Proposal4 on the exact preexisting resident profile: schemaVersion4,
  kind=resident_provider_offer, operation=offer|revoke, targetId=player_soul,
  woundId, providerConscious,providerCapable,targetConscious,targetCapable,
  readableEvidence. No client stamps or caller healer coordinate. Offer requires
  all four true; revoke may clear factual flags. Owner derives from enclosing
  ordinal typed profile, never an ID-only Guardian fallback.
- Record4: schemaVersion,kind,status=offered|revoked,healer,target,woundId,locality,
  four offer facts,readableEvidence,binding,acceptedCycleOrdinal,
  eligibleCycleOrdinal,primaryTransitionId. Complete closed healer coordinate is
  (chaos_sea,resident,exactId,AfterlifeProfilesPath), target is exact player_soul.
  locality is exact current Guardian/Abode. Only actual natural owner issues it;
  natural result6 embeds its one full outcome. Existing contribution anchor and
  separate exact wound binding remain unchanged.
- Selection6: schemaVersion6,mode=receive_aid,direction=resident_to_player,woundId,
  expectedWoundFingerprint,healer,target,locality,participationFingerprint,
  confirmation=required|player_target_confirmed. Confirmation field is absent
  from v5; no schema accepts contradictory coordinate/role/confirmation flags.
- Active request6: existing aid request fields, direction=resident_to_player,
  playerConfirmation=player_target_confirmed, full record4 witness and sealed
  original attempt/source/die. Active result6 contains that same request/direction
  and ordered stages. A self request/result cannot become family6. No detached
  record/selection/receipt becomes permission.

### Exact participant joins, all owner transitions and History

Factor only the pure typed resident roster/profile/Guardian/Abode join from
RequireResidentScope into RequireResidentParticipantScope(guardians,residents,
profiles,participant). Existing v5 wrapper RETAINS its exact known active resident
wound-owner check. New receiving branch joins resident HEALER while the actual
selected active wound remains owned by PLAYER. Validate patient independently via
existing profile/wound/current-soul/conflict/visibility checks. Preserve aliases,
ordinal identity, duplicate/null/ambiguous/wrong-kind refusals and legitimate same
textual ID across different kinds. No generic LocalActorIds can grant authority.

Natural participation, current committed offer reader, StageSource, before-draw
close, signed B, original capture, planning/publication, owned normalization,
closing and current-settlement reads all select the SAME typed roles. RecordOwner
is resident healer for record4, resident target for record3; ResultVersion maps4→6
and3→5 without changing1→3/2→4. IsResidentParticipation covers3/4 while
IsResidentConsent remains target-only; IsProviderOffer covers2/4. Do not silently
replace existing error/iteration semantics or allow multiple distinct proposals.

History validates full recorded natural offer, binding, witness, original request,
source/tiers/stages and immutable result; it must not demand a later live roster.
New offers, admission/staging, signed B, owned normalization and closing still prove
the current resident roster/profile/locality join. Current-settlement replay instead
retains the existing exact typed profile/record, control/source/schedule/cohort and
History checks, without a blanket renewed live-presence requirement or treating a
receipt as new permission. Departure preserves the historical/settled read while a
new attempt must fail its live join. Preserve existing v3/v4/v5 forms and
past fingerprints; cover unsupported schemas and re-fingerprinted6-self/header
mutations rather than relying only on a stale digest to reject them.

### Existing memory and common publication

Use the existing spiritualResidentThoughtJournalUpdates output-only transport,
retained response fingerprint, append validation, normalizer projection and owned
roster writer. For record4, memory actor is the RESIDENT HEALER (RecordOwner), not
patient. Published interface binding accepts only actual closed family5 OR6 with
its exact same authenticated original/response and complete append. Unowned,
unpublished, wrong-family, response/old/new memory drift stays rejected; no global
extra-field exemption, new store, publisher or diagnostic transport.

Both real offer43/aid44 GM responses must include accurate Mixed resident healer
and ordinary auto-progressing Guardian ActorBrain/memory. Reuse the proven initial
fixtures once before sole bootstrap, genuine cleanup, Guardian ordinary journal,
protected resident output and full original/scheduler/History. No file insertion
between accepted turns. Rollback captures both journals: common C4 restores exact
submitted prepared44 command/die/report/normalizer inputs, outer restores canonical
accepted43 including memory. Assert the real physical roster append, frozen signed
roster, exact old prefix+one current entry and inert settled-read byte sets.

### Work units and finite verification

1. Register this task/design/plan; independent Astra/high DESIGN must resolve role
   orientation across natural/current/original/History/memory before implementation.
2. Add existing-API causal Fact ResidentProviderOfferUsesTypedActorAndPlayerPatient
   in SpiritualResidentProviderParticipationTests.cs. Complete typed fixture calls
   ReadChanges on proposal4; current implementation should compile then reject
   unknown family. Run only exact new RED singleton via official runner after
   fresh owning Unit build/catalog/Plan/PREP. Compile failure is not semantic RED.
3. Implement record4/selection6/request/result6 and typed participant scope in
   existing participation/aid/resolver/current-reader/History/published-I services;
   reuse pure source DATA. Add causal cheap coverage for all three raw forms,
   forgery/null/mirror conflicts, role+kind/alias joins, revoke/expiry/confirmation,
   separate resident H/player natural H/effects, closed headers, old3/4/5 exactforms,
   same-ID Guardian/resident healer joins and protected-memory6 resident-healer owner
   versus wrong player-patient owner/wrong-family/drifts. The real accepted revoke
   must be attempted in its eligible next cycle so expiry cannot mask status refusal.
   No full Engine for pure codec.
4. Update actual WoundApplicationService offers/ActionFor and shared console/
   browser confirmation/queue/Engine action consumers; new distinct Russian
   resident receive-aid label, same player-confirmation authority. Add one genuine
   offer→aid backend and actual revoke/refusal producer, source/old-family impacted
   controls only, then BOTH guided callers and common-fault rollback. Build complete
   first-turn setup and two-actor responses before runtime. Preserve original
   per-category bounds; small coherent cheap60s/backend180s/UI180s groups.
5. Synchronize data-model, GM guide/Matrix, CLI/API, both effective daemon prompts,
   worked offer4/selection6/memory example, manifest and typed documentation guard.
   Independently review full source+fixture+selection before fresh affected builds,
   discovery/exact Plan/PREP, cheapGREEN then backend then native-controlled actual
   UI/rollback. Derive exact case counts from discovery, not this plan. Substantive
   pin review and existing World guard only if a pinned producer actually changes.
   New unknown6-self negative reuses the one existing actual self2 producer when
   needed; do not rerun unrelated accepted69/DFR/Mortal/Windows/performance gates.

Read/validation reuse stays with existing operation source list/parsed roots and
services, keyed by actual manager/lease/generation/binding/path/witness/source DATA,
limited to its existing immutable read window; all current permission/source/
history/closing fences remain fresh. The factored participant function is pure
already-parsed DATA, not cached authority. No new cache, callback or duplicate
read framework; no speedup claim. If actual new repeated expensive reads arise,
prove cause and use the existing service with cheap source-count/invalidation
controls before a heavy run. Full raw remains outside checkout; no deletion or
newclone/restore. WIP/checkpoints publish on current branch with remote SHA checks.

## T108-SHINING-PATIENT — player treats a Shining patient (2026-10-30)

Issue #1536. Base d601411b0c105ca3fdf409a668f1f0dc11098b4a.
Status: written DESIGN accepted by independent Astra/high at
`d4aa700df3ced79bbfa81bb2c0007f2b82ec6798`, including the natural8 and owned
normalization clarifications. Gameplay is already authorized by US4 scenario8,
FR043–054 and FR072–074. The owner authorized autonomous implementation;
no new game rule or repeated design-approval question is introduced.

### Complete player result

At the current revealed faction hall, `/лечить` lists Self first and visible
nearby patients. The player can ask an ordinary resident or the canonical
radiant faction head for permission through the existing ordinary GM turn.
After the genuine consent turn and normal Engine cleanup, the player chooses
that patient's known active spiritual wound and separately confirms treatment.
The real retained roll uses the player's Healing; natural recovery uses the
patient's Healing. The existing active-then-natural program advances one safe
world cycle and publishes wound/effects/History and the proper existing memory
atomically. Cancel, missing consent or stale identity/locality performs no draw,
control write, payment or treatment. Exact replay retains the same outcome.

This is full treatment, not another recommendation-only card. The selected
scope is resident and canonical `radiant_actor` faction head. A leader is a
role, not an owner kind: resolve `leadership.headActorType/headActorId`, never
invent `leader` or alias `afterlife_actor` to `radiant_actor`. Other head kinds,
generic custom actors, unwilling/conflict aid and helpless exceptions remain
outside this bounded acceptance. Diagnosis execution follows its separately approved contract; public/negotiated
provider→player services and their prices do not change. The patient need not
have Healing≥I, `healing_support`, a service profile or a paid-access tariff.

### Chosen approach and rejected alternatives

Reuse participation, accepted natural witness, private selection/attempt,
continuation, active+natural reducer, common publisher, History, existing
memory and shared UI. Add one closed Shining patient branch. A separate healing
service/price/publisher or diagnostic framework would duplicate those owners.
Globally relaxing Chaos patient rules would silently change old schemas and
is rejected. A radiant patient must not be disguised as a resident.

### Closed schema and version boundaries

Participation proposal/record schema5; active selection/request/result family8.
Family7 is already paid service. Schemas1–4 and aid families3–6 stay strict;
no relaxed old owner reader, implicit direction or cross-family fallback.

New proposal on the exact preexisting patient's profile, under existing
`spiritualTreatmentParticipationChange`, has exactly:

```json
{
  "schemaVersion": 5,
  "kind": "shining_target_consent",
  "operation": "grant",
  "healerId": "player_soul",
  "woundId": "wound_patient_example",
  "locality": { "factionId": "faction_example", "hallId": "hall_example" },
  "conscious": true,
  "capable": true,
  "readableEvidence": "Пациент осознанно согласился на лечение игроком."
}
```

`operation` is grant/revoke; grant requires both booleans true. The enclosing
original typed profile determines the patient; no GM-authored canonical record,
binding, digest, cycle or History witness is accepted. Healer is exactly
`(shining_abode,player_soul,player_soul,AfterlifeProfilesPath)`. Locality is only
existing `ShiningFactionHall`; all coordinates are correlation until current
admission and accepted natural publication prove them.

The client-derived schema5 record has exactly schemaVersion, kind, status,
healer, target, woundId, locality, conscious, capable, readableEvidence, binding,
acceptedCycleOrdinal, eligibleCycleOrdinal and primaryTransitionId. It uses the
existing next-cycle/previous-turn lifetime and full accepted witness matching.
Record schema5 maps through `ResultVersion(record)` to OUTER natural result
family8 as well as active result family8. Natural8 remains mode=natural,
request=null and has no active direction. Active8 remains mode=aid with exact
direction=player_to_shining_entity; only the UI selection mode is
shining_entity_aid. Outer natural family5 is existing Chaos resident consent
and does not acquire Shining semantics. Old branch shapes/fingerprints stay strict.
The active family8 retains that full accepted natural8 witness and complete
source tuple for original reconstruction, continuation, terminal replay and
rollback. It must not use a source-shaped public record as authority.

Add `SpiritualShiningPatientParticipant(Healer,Target,Locality)` to
`SpiritualAidSelection` as a distinct variant. It is mutually exclusive with
Resident, GuardianProviderLocality, Compensation, Service and Combat; direction
is player→patient, ReceiveAid=false and no provider/player-target confirmation
field. Use a closed distinct selection mode `shining_entity_aid`; do not remove
the existing Chaos-only patient serializer guard. The ordinary guided action
confirmation remains mandatory before staging.

### Exact current admission

Add a pure operation-DATA patient scope beside existing provider admission:
`RequireShiningPatientAdmission(shining,profiles,residents,target,locality)`.
Its `ShiningPatientAdmission` result identifies the typed target/profile and
head/member role; it is not lease or publication authority.

1. Require ordinary Shining mode, exact `ShiningCurrentHallContract` current hall,
   one revealed current faction with that exact hall and supported lifecycle.
2. Resident: exact ascended membership in the actual existing resident roster,
   one matching typed visible/living materialized profile in the current hall.
3. Radiant head: exact occupied leadership with headActorType=radiant_actor and
   headActorId; one corresponding `shiningPoliticalActors` row with matching
   currentFactionId; one visible/living/materialized radiant_actor profile in
   the current hall. No same-ID wrong-kind or generic afterlife_actor alias.
4. Require the exact known active spiritual wound owned by that participant.
   Full accepted schema5 consent/witness remains an additional prerequisite.

Reuse current role/life/location/materialization helpers only with these exact
joins; permissive alias/ID-set lookup is supporting DATA, not sufficient proof.
Do not reuse provider admission's healing-support/service gate on a patient.
Selected source dependencies are Shining state plus resident roster for a
resident; radiant leadership/political DATA is already in Shining state.
No fake Guardian dependency or GuardianId alias is added for either patient.

### Operation ownership, existing consumers and memory

- `SpiritualTreatmentParticipation.ShiningPatients.cs`: explicit schema5
  proposal/record dispatch, outcome and selection bindings; legacy branches
  stay closed. Reuse the existing one-distinct-change/mirrored-intent rule.
- `AfterlifeWoundTreatmentAuthority.ShiningPatients.cs`: exact pure admission;
  existing Aid source collector carries selected roots through fresh pre-control,
  pre-entropy and closing checks. Extend active family8 and persisted readers
  explicitly through existing serializers/reducer/planner, not a new publisher.
- Shared navigation/projection: reuse prepared wound catalog/profiles. Enrich
  only the treatment operation with caller-owned Shining/roster DATA; `/раны`
  and status acquire no new global roots. Introduce operation-local
  `WoundApplicationMenuData(State,Context)` via `BuildStateAsync`; keep existing
  `CreateStateAsync` wrapper returning State. SelectTarget consumes the enriched
  fresh context; only detached State/choices reach UI sessions. No parsed roots
  are retained in WoundCommandState or serialized to browser. Projection rechecks
  the exact fresh patient scope instead of trusting a forged target DATA value.
- Memory: resident uses existing protected
  `spiritualResidentThoughtJournalUpdates` for the exact schema5 resident only.
  Radiant head requires a genuine append-only existing `ledger[]` entry plus
  current `gmThoughtsSummary`; summary alone is not historical memory for an
  existing actor. Faction chronicle and automatic progression ledger do not
  substitute. The grant/active owner must compose this DATA without raw writes
  to its guarded original profiles. See the correction requirements below.
- Historical completed replay checks the immutable accepted proof/result and
  does not require renewed live consent or a second draw/world cycle. Pending
  execution still requires every fresh participant/source permission check.

Owned locality normalization must explicitly branch on the Shining patient
family rather than call provider admission. Compare the exact admitted typed
patient membership/headship, faction, hall, lifecycle and profile identity tuple
before/after normalization; LocalActorIds or scope-set equality alone is not
proof. A changed head/currentFaction/lifecycle refuses even when all actor IDs
remain local. Legitimate owned after-images are verified by exact physical
readback, then every existing live guard advances together. Signed originals
and parsed authority DATA remain frozen; no optimistic guard advancement.

Reuse stable parsed roots/indexes through the existing read-set/operation
services. Key/dependencies include exact realm/owner/wound/generation, current
hall/faction/lifecycle/visibility, roster or head/political relation, profile
life/art/effects, consent/full witness and source fingerprints. Lifetime is one
read or planning/validation operation. No cross-action authority cache, static
dictionary or new cache framework. Cheap actual source counters prove selected
roots are reused; integrity/source/history/closing checks remain fresh. Speedup
is not claimed without measured production evidence.

### Implementation and finite acceptance plan

1. Freeze a compilable existing-API RED in a dedicated cheap category: schema5
   resident and radiant proposals on independent preexisting profiles must be
   returned by `ReadChanges` without mutation; old code's explicit unsupported
   family refusal is the intended assertion failure. Preserve/review exact input
   completeness before fresh owning build/discovery/PlanOnly/PREP/run.
2. Implement the closed codecs and exact patient scope; cheap typed controls for
   both kinds, same-ID/wrong-kind, cross-family/realm/locality, changed head,
   missing/duplicate political row, hidden/dead/nonmember, grant/revoke/expiry,
   same-turn forged witness, insufficient healer and independent exported DATA.
   Revoke and consciousness/capability refusal controls must remain valid except
   for the one tested field, so expiry cannot mask a broken status check.
   Add actual source-read/compute counters with full dependency invalidation.
3. Connect existing producer/witness, original/request/result/replay/rollback,
   active+natural program and the two memory owners. Update canonical technical
   contract/data model, effective GM instructions, worked schema5 examples and
   source/manifest guards together. Review one coherent SOURCE cut; fresh owning
   projects and affected old self2/Guardian3/4/resident5/6/Shiningprovider6/service7
   codec/native consumers; shared dispatch changes must not omit Guardian3/4.
4. Qualify actual consent turn→normal cleanup→separate confirmation and real
   treatment for resident and radiant head through BOTH console and shared
   browser handlers. Whole initial setup includes all ordinary resource/actor
   progression/brain/memory/timestamp/relationships/ready contracts, using existing
   complete helpers. One actual C4/outer rollback for each owner kind and exact
   retained retry; no post-accepted-turn seed files or fabricated witness.
5. Exact categories, discovery, PlanOnly, resource/artifact/PDB PREP and independent
   OUTCOME before acceptance. Existing 180s functional peer budgets are protective
   bounds; never raise them for GREEN. Reuse existing full original-message,
   macro/named timing and sealed passive gzip capture, preserving footer/CRC/full
   decode/source hashes and owned cleanup, outside checkout. No record/byte cap.
   Check reserve400MiB plus full-output headroom before each real run; preserve
   all unique evidence and current qualified binaries before overwrite.

Implementation is native in the existing clean task checkout, with one writer.
Astra/high reviews this unresolved typed-version/authority design; independent
Sol/xhigh reviews source/fixture/selection/PREP/OUTCOME. WIP code/checkpoints go
to the same branch with verified remote SHA. No new clone/restore, history rewrite,
raw upload, merge or issue closure. DFR/Mortalcompat/Windows and separate diagnosis execution
execution remain separate. No unchanged accepted PASS is replayed without a
new affected dependency.


### Shining patient memory composition correction — T108 / 2026-10-30

The first real resident consent turn reached validation repair and timed out.
Two existing contracts must compose; neither may be skipped for this feature:

- Client-auto strategy really changes the mandatory support healer's profile and
  roster. Current structured-diff reasoning requires its decision audit and
  append-only resident journal, even though it is not an aid participant. Moving
  it outside declared scope does not remove the actual change.
- The exact patient-only protected resident append must remain restricted to the
  selected patient. Currently its whole-journal before/after image prevents an
  ordinary support append from composing. Raw roster writes also violate the
  signed-original/fresh-source contract; they are not a legal workaround.
- A pre-existing radiant patient needs a genuine actor-owned ledger append.
  Natural grant can compose ordinary current profile memory; active8 starts from
  signed originals and forbids raw profile writes. Its memory must be explicit
  detached response DATA composed through the existing profile publisher.

Resolve both through the existing continuation, resident normalizer and common
profile publisher: separate admission of ordinary nonpatient resident memory and
exact selected-radiant memory; preserve all prior journal/ledger entries and all
nonmemory fields; bind actual response/turn/typed actor and signed source roots.
Keep immutable originals and physical current/closing/readback checks, one draw,
cycle, accepted History, completed replay and atomic compensation/outer rollback.
No full-carrier replacement, new publisher/cache/journal, fake economic update or
progression-memory substitution. Command transport is the bounded program below;
independent DESIGN acceptance and causal proof still precede implementation/retry.

#### Exact response programs and ownership

Reuse existing `residentThoughtJournalUpdates` as ordinary nonpatient memory.
Only an actual schema5 natural8/active8 response routes it to the private response
file instead of the generic canonical roster mapping; other families retain
ordinary routing. Transport is not authority: malformed/unsupported treatment
memory refuses, never silently falls back to raw canonical writes. Keep
`spiritualResidentThoughtJournalUpdates` exact-selected-resident only. Ordinary
rows must be unambiguous signed-original local residents, exclude the patient,
match the actual turn, preserve prior entries and have globally fresh IDs across
the original journal and both append sets. Compose original + ordinary + protected
in that deterministic order through the existing resident normalizer. Competing
raw roster updates/journal edits continue to refuse.

For radiant patients reuse the existing ordinary roster channel only when such
memory is actually supplied. Read the roster from the same validated pending
snapshot and compare exact current bytes as a conditional memory dependency;
patient admission itself still has no roster dependency. Missing/stale snapshot
coverage refuses. Bind current signed profiles/locality and the exact local
resident; this confers no healing role, service or consent authority.

Add output-only `spiritualShiningPatientMemory`, a closed object with exactly
`entryId`, `summary`, `turnNumber`, `gmThoughtsSummary`. It targets only the retained
schema5 radiant patient implicitly, never an actor selected by the payload. Require
nonblank strings, exact actual turn, valid fresh ledger ID and no extra fields.
Reject resident/foreign families or absent/ambiguous participation. Both natural8
and active8 use this same response contract. Append the existing legal ordinary
ledger row `{entryId,summary,turnNumber}` and set current `gmThoughtsSummary` in
the existing common profile projection, preserving the whole old ledger prefix
and every unrelated field, plus existing owned healing/progression. An exact
already-composed afterimage is idempotent; partial/different application refuses.

Freeze detached programs once per Continuation, bound to its real session/request/
turn/snapshot/lease and response fingerprint. Reuse operation-private parsed
sources and computed before/after DATA; no persistent cache. Extend the current
normalizer guard only for this conditional memory path, retaining all original/
opening/closing/readback checks and advancing live guards after exact publication.
Common profile/roster rollback covers memory; completed-result reads never replay
commands. Pending retry requires the same bound program and exact before/after.

Synchronize GameResponse/StateDistributor/FileMapping/refresh merging, existing
planner admission/profile and resident projection, conditional locality owner,
interface validation and GM prompts/worked examples. Programs stay out of player
DTOs. No global output-only reclassification of the ordinary resident field.

Finite proof before the next Engine case: cheap disjoint resident append
composition and exact radiant memory with auto-progression disabled; refusal of
foreign/selected-as-ordinary actors, stale turn, duplicate IDs or old-entry edits;
then four real kind×phase43/44 response proofs using actual signed preparation,
normalization/publication and no-argument Full reasoning validation. Phase44
requires a real committed grant. Retain prior PASS; only affected source/owning
builds and gates repeat. The same full gzip capture and 180s Engine budget stay.


## T121-DIAGNOSIS-SPIRITUAL — guided execution design (2026-10-30)

Source: #1536, T093/T121 and FR071, including the author decision accepted
2026-10-30 11:48UTC. The bounded supported-three-kind route after T108 was
independently accepted 2026-10-31: 173 distinct PASS / 70 methods / 31 planned
descriptors across 14 runtime SHAs, not one fresh aggregate. Final production
source is `ab4cd7276fbd871723e79f4016109015c37ef9a0`; the plan retains each
earlier unchanged consumer's reviewed lineage and all failed attempts. Real
console, shared browser and physical rollback passed with complete revalidated
captures. Current source_capability, skill_tier and current-player provider
requirements have explicit proof; item_quantity, resource_quantity, consent,
facility, location, quest_state, effect_state and environment remain unavailable
until their canonical adapters qualify. This closes only the four bounded
spiritual admission/publication/completion/GM tasks, not those adapters, Mortal
procedural diagnosis, T093/FR071/T121, US4/US5 or #1536. The rules and owner design
below remain the governing contract, not a claim that every adapter is implemented.

### Player result and chosen owner

A player with a valid current Healing tier0–V can separately confirm one currently
reachable diagnostic path for a visible active spiritual wound outside conflict.
Every declared requirement must have current canonical proof. Success is guaranteed
and reveals exactly that path's complete declared facts. The diagnostic operation
has no die, OD, Feather, item or other resource mutation and performs no direct
healing. It consumes one ordinary safe cycle, retaining ordinary natural recovery
and other world progression. Opening known detail and cancellation remain inert.
Missing/invalid art authority is not H0; unknown requirements never mean satisfied.
Read the current art directly: do not apply the healing tier>=wound-rank gate to
diagnosis. Healing tier V is valid with an ordinary severity I–IV wound; it is not
a severity-V wound or an exception to the required ordinary recovery suffix.

Chosen approach: extend the existing guided selection, spiritual staging and
AfterlifeWoundHealingPlanner.Continuation with an explicit diagnosis purpose.
Reuse the current requirement/source owners, working wound reducer, scheduler and
common publisher. A fake self-treatment/aid/Mortal route would apply the wrong
severity gate, roll or consent contract; an independent diagnosis executor or
second natural owner would duplicate the cycle and carrier ownership. Both are
rejected. Technical choices below introduce no new check, tariff or game rule.

### Closed selection, requirements and request

- Add a mutually exclusive SpiritualDiagnosisSelection to the existing
  SpiritualSelfTreatmentSelection carrier. Proposed exact selection schema9/mode
  `diagnose`: woundId, expectedWoundFingerprint, target WoundOwnerCoordinate,
  diagnosisPathId and expectedDiagnosisPathFingerprint. Aid/Combat/Service and
  diagnosis cannot coexist. UI generation/target fingerprints remain detached DATA.
- WoundApplicationService builds a diagnostic choice named by the canonical path,
  separate from healing. WoundTargetSelectionService retains Self-first and exact
  visible/reachable target admission. Confirm reloads current sources; it does not
  trust names, prior availability, future-reachable facts or a stale menu.
- Add SpiritualDiagnosisRequirementAuthority within the existing continuation,
  using closed typed requirements rather than opaque JsonElement acceptance.
  Reuse the existing exact requirement shapes for item_quantity, resource_quantity,
  skill_tier, source_capability, provider, consent, facility, location, quest_state,
  effect_state and environment through realm-appropriate canonical readers. The
  diagnostic provider role is the current player; target is the exact selected
  owner. Unsupported capability/source/role shapes are unavailable, never ignored.
  Spiritual standard-art skill requirements use explicit0–5 authority. Requirement
  quantities check current possession/availability only; diagnosis consumes none.
- A detached binding table retains every requirement index, exact statement hash,
  resolved typed role, complete logical source dependencies/bytes hashes and its
  satisfied proof DATA. The private actual owner alone authorizes execution and
  rechecks current source/lease/session. Serialized bindings grant no authority.
- StageDiagnosisAsync lives beside existing StageSelfTreatmentAsync and uses the
  same current session/generation lease, request retention and scheduler owner.
  Proposed request schema1/kind `spiritual_diagnosis_request`: exact session/request/realm/turn binding,
  selection, attemptId, cycleKey, original wound/path and requirement fingerprints,
  admitted Healing source and COMPLETE exact canonical control. Other control
  fields cannot drift merely because realm/ordinal still match. No
  roll/payment/cost fields are accepted.
  The retained request has no snapshot token; the admitted owner/result binds the
  actual signed token without backfilling or changing that request. Repeated staging
  preserves the original request without new work.
- Add explicit diagnosis dispatch in pending action, turn preparation, original
  snapshot classification, interface validation and retained request parsing.
  Ordinary natural admission currently requires command absence and cannot be
  reused by bypassing that guard when a diagnosis request exists.

### One working program, required complete recovery suffix

Keep original signed DATA and diagnosed working DATA separately. Current path
prerequisites/requirements are checked only against original current knowledge.
Use WoundDiagnosisEvidence/CreateRequest and the knowledge-only reducer with a
spiritual admission proof; preserve the existing physical/Mortal factory behavior.
The diagnosed after-image may change only declared route/complication knowledge
and its diagnosis transition. It cannot edit care, rank, recovery, owned effects,
display prose or unrelated fields. Emit one terminal diagnosis-attempt intent.

Then derive the FULL ordinary cohort from admitted original sources using unchanged
ReadNaturalMembers rules: active spiritual ranksI–IV, ordered owner kind, owner ID,
then wound ID. Replace only the selected member's recovery start image with the
validated diagnosis after-image. Do not prioritize that member: the first recovery
wound need not be selected. Execute existing recovery math/graphs in that order.
Every selected recovery stage must follow its diagnosed image exactly. Only real
recover/heal stages may change mechanics, remove a carrier or close identity.

Canonical reconciliation supersedes the earlier technical V-wound/empty-suffix
assumption: FR002/FR003 and the wound contract allow only severity I–IV; FR042
allows Healing art tier V. A valid selected active spiritual wound is therefore
always a member of ReadNaturalMembers, and Build(active:null) always emits recover.
Every admitted diagnosis has a complete NONEMPTY natural suffix and exactly one
existing natural result. Empty/omitted suffixes are refusals, not supported positive
cases. No new wound severity is introduced. Recovery need not heal or lower rank;
it may merely add progress, or emit recover/heal at its real arithmetic boundary.

### Durable results and exact completion

The existing WoundDiagnosisTransitionResult lacks the durable original needed after
terminal recovery. Add an OWN closed schema1 `spiritual_diagnosis` result containing
exact binding/request, complete original selected wound and knowledge-after image,
complete admitted original cohort/tiers, requirement binding, source fingerprint,
cycle/control, scheduler before/report/after images and fingerprint, required
naturalSuffix reference and resultFingerprint. Full private images remain internal
History/replay DATA; public projection exposes only authorized readable discoveries.

The required reference identifies exactly one existing natural persisted result,
its primary transition/result fingerprint and complete nonempty ordered suffix
stage IDs. Null, missing, empty, extra or reordered suffixes refuse. The existing
natural result covers ONLY recover/heal, starting at the diagnosed working images,
with its normal strict cohort/control/result codec; it cannot claim the diagnosis.
No schema5 consent grant, payment or provider receipt is fabricated for diagnosis.

Extend existing continuation/common reduction to preserve the diagnosis result on
its own row AND the natural result on its first recover row. Current
ReduceExecutedSpiritualState attaches one result only at index0 and clears later
results, so it needs this explicit two-result boundary rather than a loose result
injection. Merge final carrier/identity/History/effects through the same common
assembly and publication; claim/consume the scheduler and retained request once.

Completion/replay gets an explicit diagnosis branch: validate its exact request,
original→knowledge delta, one exact nonempty suffix derived from the COMPLETE admitted
cohort, settled scheduler, consumed input absence, current carrier/identity/History
and every final row. The first suffix wound need not be the diagnosed wound, and
terminal recovery may remove that diagnosed carrier. A valid Healing tier V remains
supported with a canonical severity I–IV wound. Retain original prefixes, exact operation
and attempt replay, wrong-family/duplicate/conflict refusals and rollback. Historical
component consistency does not mint fresh source or publication authority.

### Planned code units and interfaces

- `Services/SpiritualSelfTreatmentSelection.Diagnosis.cs`: closed selection arm;
  `Services/WoundApplicationService` and `Services/WoundCommandState` reuse the
  current guided choice/target contract rather than introduce a second UI stack.
- `Services/SpiritualDiagnosisRequirementAuthority.cs`: closed typed requirement
  and binding DATA, subordinate to the actual existing continuation/source owner.
  No reusable live lease, admission or mutation authority is serialized or cached.
- `Services/AfterlifeWoundTreatmentAuthority.Diagnosis.cs`:
  `Task<SpiritualSelfTreatmentStageResult> StageDiagnosisAsync(FileSystemManager,
  CanonicalWriteLease, SpiritualSelfTreatmentStageContext,
  SpiritualSelfTreatmentSelection)`; deliberately no entropy/cost callback.
- `Services/AfterlifeWoundHealingPlanner.Diagnosis.cs`: admit/build diagnosis
  within the existing Continuation, preserving both source and working baselines.
- `Services/SpiritualDiagnosisPersistedResult.cs`: closed result with
  `Parse(JsonElement, string, ICollection<ValidationIssue>)` and canonical writing;
  `Services/WoundHistoryState.SpiritualDiagnosis.cs` proves exact row/suffix links.
- Existing `WoundAcceptedTurnPlannerCore.SpiritualHealing.cs` and common carrier
  assembler preserve both result boundaries and compose each final root once.
  `ProgressionScheduleService.SpiritualReplay.cs` dispatches exact diagnosis
  completion; pending action, turn snapshot and shared handler consumers dispatch
  the same typed purpose. No alternate publisher or cleanup mechanism is added.

### Implementation order and finite qualification

1. Freeze the existing-API menu RED below; independent DESIGN/SOURCE, fresh Unit,
   current artifacts/PDB/catalog/exact PlanOnly/PREP, then exactly2rows/60s. Accept
   only final missing diagnostic-choice failures after valid source preconditions.
2. Add typed requirement/selection/request and immutable operation-owned admission
   tests, then their minimal implementation in existing Services partials. Cover
   every supported requirement kind, unknown/absent/false proof, H0 versus invalid
   source, conflict and stale target/path/realm/generation. Keep all cost/draw paths
   structurally absent and denied, rather than calling them with zero arguments.
3. Add spiritual knowledge-only reduction and closed durable result/reconstruction
   tests, then one-owner common composition. Required cheap cases: genuine Healing
   tier V with severity I–IV, multiple wounds, selected member not first, normal
   no-heal recovery, selected terminal removal, empty/omitted/extra/reordered suffix, wrong original or
   binding, altered private image/fingerprint, duplicate/conflicting and exact replay.
4. Wire shared handlers/confirmation/staging and completed-cycle readers. Add real
   signed native publication/Full reasoning and physical rollback controls before
   console/shared-browser Engine qualification. Test replay after normal cleanup,
   cancellation/read tree identity, no diagnostic draws/spending and one cycle.
   Audit complete initial worlds and every GM response before expensive runs.
5. Synchronize active Wound_Materialization_Contract, Afterlife_Contract_Matrix,
   CLI_API_Specification, CLI_Step_Main, Block32/Actor_Brain, both applicable daemon
   directives, worked examples/manifest and source/doc guards in the same behavior
   block. The GM describes world response and required natural graphs/memory;
   it cannot select success, add reveals, author a receipt or charge diagnosis.
6. Independently review coherent SOURCE, fresh owning builds, exact bounded catalog/
   PlanOnly/PREP, cheap before native before UI, original budgets and full captures.
   Preserve accepted110 and diagnosis-read83 lineages; rerun only changed consumers.

The later Mortal extension must explicitly retain each method's declared procedure
check, duration and cost policy through its existing owners. Its typed model drops
Check and writes{}, and paths have no duration/resourcePolicy fields today. It is
not implemented or silently defaulted by this spiritual slice.

### Preparation and optimization assessment

Initial scaffold: SpiritualWoundDiagnosisMenuTests uses existing CreateState and
WoundPlayerProjectionTestData in two realms. Canonical self owner/path, original
rankII wound, valid H0 profile/soul mirror, no conflict and one public path revealing
an actual hidden spiritual complication; its sole requirement is current
source_capability/spiritual_healing for the provider. Existing PrepareSource proves
that capability; catalog/identity/History agreement, readable recommendation and
input immutability precede the final missing executable-choice assertion. No new
production API, accepted receipt, file setup, Engine, draw or cycle is claimed.

Parse typed wound/path/requirements once per menu or accepted preparation and pass
immutable operation DATA through existing source/continuation owners. Complete
source identity includes session/request/token/realm/turn, original wound/path,
actor roles and every requirement dependency. Freeze original and working images
once; never alias or reparse them at each consumer. Confirmation, current lease,
source mutation/normalization advancement, prepublication and closing remain fresh.
A new call, mutation, rollback, retry or owner close ends reuse. No new cache,
parallel publisher, diagnostic stack, dependency, timer or budget is introduced.

## T121-DIAGNOSIS-MORTAL — existing-owner implementation proposal (2026-10-31)

Source: #1536, open T093/T121 and FR-071; author rules accepted 2026-10-30
11:48UTC. Independent Astra delta DESIGN accepted
`4152f33092317239c3d1ec45cf653617ca186309`, releasing only the two-row
existing-parser causal scaffold. That test-only cut is authored but UNCOMPILED /
UNRUN; no production semantic or live authority implementation is qualified.
Accepted spiritual173/read83/Shining110 lineages are not replayed or reinterpreted
by this proposal; all HOLDs remain.

### Complete player milestone and alternatives

The player chooses one currently reachable, known Mortal diagnostic method, sees
its explicitly declared check, duration and costs, then confirms separately. Fresh
world requirements are proved before reserving anything. One retained procedure
outcome reveals exactly the complete declared facts on success, or none on failure.
Resources, elapsed time, knowledge and History publish atomically; exact retry
reuses the terminal attempt and rollback restores the original bytes and claims.
Read/cancel are inert. Diagnosis adds no treatment/heal operation, no spiritual
safe cycle and no default price, check, duration or success threshold.

Chosen technical approach: add a distinct diagnosis subject to the existing Mortal
accepted-world/procedure/request/persistence/common-publication owners. Extract
shared procedure parameter/band calculations inside those owners where the current
API accepts only a treatment route. Do not manufacture a procedure healing route
from a diagnosis path. A separate dice/reservation/publisher system would duplicate
current claims and atomicity; passing external fingerprints directly to the local
diagnosis factory would supply no fresh authority. Both alternatives are rejected.

### Exact current gaps and reuse boundaries

| Existing owner / source | Reuse and required addition |
| --- | --- |
| `WoundMaterializationContract.ParseTreatment/ReadDiagnosisPath/WriteTreatment` | Shared base path retains raw `Check`; physical/Mortal writing already dispatches to the typed Mortal serializer, while spiritual writing preserves its own path. Keep that realm split. |
| `MortalWoundTreatmentModel` / `.MemberShapes` / `.Contract` | `MortalWoundDiagnosisPathDefinition` drops Check; `WriteDiagnosisPathCanonical` writes `{}`; `ValidateDiagnosisPath` admits only exact-empty check. Preserve the actual payload in the typed definition and add closed Mortal execution validation. |
| `WoundPlayerReadService` / `WoundPlayerProjection.Knowledge` / `WoundApplicationService` | Reuse one read context and current-known/private filtering. Actual CreateStateCore currently returns no Mortal choices. Add readable Mortal method/check/duration/cost projection and the same separate guided confirmation. Menu DATA is not execution authority. |
| `MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent` / requirement bundle | Actual signed original, generation, FS/lease, wound, complete History, current target/provider/location, skills, effects, items and scene proof remain mandatory. Add exact diagnosis-subject/path binding, not caller-provided authority seals. |
| `MortalWoundProcedureCheckAuthority.Create/MatchesFreshAcceptedState` | Currently route-only. Reuse accepted d20 span, effect-derived mode/modifiers, complication difficulty, exact skill, dice reservation and Fate Shield arbitration with an explicitly diagnosis-bound subject. Keep ordinary treatment behavior unchanged. |
| `MortalWoundTreatmentPlanner.Requests` / resource and dice registries | Reuse claim/confirm/release/commit ownership and exact retry. Add a closed diagnosis purpose and path coordinates rather than using RouteId as an untyped alias. |
| `MortalWoundTreatmentPersistence` / `ValidationService.WoundMaterialization` | Live capture currently rejects accepted diagnosis/alternative commands (Persistence:145/2728; validator:1014). Add only the real diagnosis accepted-world adapter; alternative authoring remains independently gated. |
| `WoundDiagnosisEvidence` / reducer / accepted-transition composer | Existing factory proves detached local integrity only; external hashes are bindings. Keep its knowledge-only delta checks and physical realm guard, and attach actual procedure/requirement ownership at live admission. |
| Existing accepted-plan/resource/item publication coordinator | Add diagnosis contribution and exact time images to the same coordinated plan. No direct canonical writes, private publisher, fake receipt or diagnosis-owned inventory mutation. |

### Proposed closed method contract and shared consumers

Keep the base diagnosis-path field set and semantic fact ordering. Put all explicit
Mortal execution parameters inside its existing `check` object, avoiding optional
top-level fields that would silently change spiritual paths. Proposed exact payload:

- `schemaVersion=1`, `kind=mortal_procedure_diagnosis`;
- `resolution`: the existing closed `MortalWoundProcedureResolution` fields
  (`formulaKey`, `difficulty`, `rollSource`, `criticalPolicy`, `modifierSource`);
- `outcomes`: existing ordered, complete nonoverlapping procedure-band coordinates
  (`bandId`, `minimumMargin`, `maximumMargin`, `category`), but only explicit
  `success|failed_attempt`, first band success and last band failed_attempt, with
  complete ordered coverage and no treatment `result` operations. Reuse ordinary
  natural20/natural1/Fate selection; success maps to all path reveals,
  failed_attempt to none. A partial-success category or undeclared threshold is
  not inferred; retain the ordinary explicit refund reasons;
- `durationMinutes`: an explicitly declared nonnegative integer in the existing
  incremental-time representation; zero is valid only when authored explicitly;
- `resourcePolicy`: the existing typed reserve/consume/refund/mutation contract,
  bound to this path's requirement indices and binary outcomes, never raw item or
  resource edits. A no-cost method must explicitly declare its empty cost policy.

This is a technical serialization proposal, not new gameplay. Unsupported check
versions/shapes and missing parameters are unavailable/refused, never defaulted.
Spiritual guarantee still requires its exact supported empty check; no Mortal
procedure parameters, roll or duration may enter that route. Current authored
Mortal methods require the complete explicit closed check. Migrate affected active
bootstrap inputs, fixture builders, draft/alternative authoring and worked examples;
empty current checks are invalid, not a legacy read-only compatibility fallback,
and cannot be normalized into invented procedure defaults.

Immutable historical evidence is a separate boundary. Do not rewrite or backfill
its old path/result seals. If preserving a current immutable historical receipt
requires an old-shape reader, identify that exact receipt/member ingress and retain
only comparison/replay of its sealed original representation. No such exception is
authorized for current wound parsing, current menu/admission or alternative/draft
authoring. Do not add a generic historical/legacy switch or infer historical
permission merely because a current method has an empty check.

Audit the actual shared consumers together: base path parser and canonical writer;
`ParseDiagnosisPathShape` and `WriteDiagnosisPathCanonical`; alternative-route draft
parsing/writing and accepted-transition recomposition; `MortalWoundTreatmentMemberFingerprint`;
`WoundDiagnosisEvidence.ComputePathFingerprint`; read-card projection and documentation
examples. They must preserve the whole check, duration, policy and ordered bands in
round-trip/seals. Structural reachability fixed point remains separate from current
known-path choice; a future reachable hidden path is not immediately selectable.

### One operation, retained terminal attempt and accepted-world adapter

Use a mutually exclusive Mortal diagnosis member in the existing guided selection
carrier and queued binding, with an explicit Mortal purpose/realm and exact owner,
wound/path fingerprints. The carrier's historical spiritual name grants no realm
permission. All old spiritual staging/request fallbacks must reject this purpose;
console, browser detached binding, accepted/recovery/settlement and completion must
dispatch it explicitly. Never turn a Mortal choice into self-healing or schema9
spiritual diagnosis. Show only authorized method/wound labels, never private images.

Discriminate diagnosis versus treatment in coordinates, identity/fingerprint
domains, selected member/path identity, request/check/resource/dice/Fate claims,
persisted results and replay reconstruction. Keep the registry's shared operation
identity collision guard: the same operation/attempt/member string used under a
different purpose must refuse, not alias or receive a fresh independent claim.
Purpose must come from the closed typed request and actual owner, not a caller
label passed beside treatment coordinates.

Pre-original intent is detached selection DATA only. The actual Engine snapshot
signs its exact selected input and ordinary d20 pool. A current accepted-state
export then proves requirements, issues the existing procedure/resource claims and
retains the exact terminal request/outcome before publication. A retry/repair cannot
reserve a new span or roll again. Bind the full session/generation/request/turn,
signed snapshot and complete control; exact method source and check/policy seals;
typed actors/locality; actual requirement, item/resource and effect/check evidence.
Reuse ordinary Fate Shield selection/consumption through its current owner and
common plan, never a local replacement or fabricated check-result fingerprint.

### Retryable compensation before History versus terminal release

One existing request/registry owner must retain the confirmed held request before
common publication, including exact selected source indices/rolls, selected result,
Fate decision and confirmed resource-reservation agreement. This retained input is
not a successful History row or a permission cache. A retryable C4 compensation
restores canonical before-images but leaves that exact same-attempt outcome/claim
binding available under the existing owner; it must not call generic release then
Reserve to obtain another result.

This distinction is required by the actual dice registry:
`ReleaseUnchecked` removes operation registration and reserved indices. The existing
`MortalWoundProcedureClaimRecoveryCoordinator` instead revalidates current accepted
original authority and uses capability-controlled `RestoreExact` or
`RestoreFinalizedExact` with carried coordinates, roll mode, exact source indices/
values, actual accepted pool and pool fingerprint. Use that path, plus the existing
Fate and confirmed/finalized resource-agreement reconstruction, when a lease or
process must rebind the retained diagnosis request. Extend their closed purpose
dispatch rather than constructing a second registry or reserving new dice.

Restore the exact carried claim before admitting competing new allocations; if
the pool, original source, generation, purpose or carried span conflicts, refuse
without choosing another span. Detached result/indices alone cannot reactivate
authority. Fresh original/lease/requirement checks and reconstruction remain
mandatory even when the terminal roll/result is already retained. A terminal
cancellation, superseded/closed turn or explicit abandoned request is different:
only its existing owner may perform final release/cleanup, and a stale detached
request cannot resume it as retryable compensation.

Add a causal C4-before-History retry with a competing claim: the same attempt must
reuse exactly its original span, Fate decision, reservation and result while the
competitor receives only a disjoint lawful span. Also reject a conflicting carried
span/current pool and same-ID cross-purpose restoration. This exposes a mistaken
release-plus-Reserve implementation even when ordinary retry without a competitor
happens to return the same die.

Add a closed versioned Mortal diagnosis durable result to the existing History
result family, retaining the exact request and original/knowledge-after images,
actual check/result and requirement bindings, reservation outcome and clock/policy
evidence needed for strict replay after cleanup. Reconstruct semantic agreement,
not merely a self-consistent outer hash. The current small detached diagnosis
result/factory alone is insufficient to authorize live capture. Success discloses
the exact complete declared set; failure still produces a terminal History attempt
with no knowledge or mechanical wound delta. Preserve the old physical guard and
the independently sealed spiritual result family.

### Duration, ordinary recovery and one common publication

Canonical clock input is `game_state/world/world_time.json`, interpreted through
`ProgressionScheduleService` with signed original Mortal control/schedule and the
subsequently validated request-bound report.
`FileMapping` transports `timeChange|setWorldTime`; those response fields themselves
grant no diagnosis time authority. The existing owner method is
`PrepareAcceptedTurnOutcomeMutationAsync`; `CaptureMortalOutcomeInputsAsync` and
`.MortalJointBasis` provide owned raw inputs and recomposable arithmetic, not a
standalone permission token. Reuse that calculation/current-input discipline inside
the ordinary Mortal owner, not the unrelated private joint-repair exception.

Those entry points alone are not a future-image handoff:
`PrepareAcceptedTurnOutcomeMutationAsync` reads already-current physical time and
returns schedule/report mutations; `PlanRegistered` reads
`acceptedState.CurrentWound/CurrentGameMinute`. Neither may be pointed at temporarily
written future bytes or a fabricated accepted-state instance.

The existing diagnosis operation owner therefore retains one private immutable
working projection, derived only after actual signed-original admission. It binds
the owning FS/lease/generation and complete original binding/control, frozen
canonical roots/catalog/identity/History, original numeric clock T0 and schedule,
exact admitted method/duration, and retained procedure outcome/reservation agreement.
Its constructor/issuer is private to that owner; it is not deserializable live
authority. The projection explicitly contains these reconstructable stages:

1. Original wound and full original cohort, with original signed source identities.
2. Knowledge-after from the existing diagnosis reducer and exact retained terminal
   outcome: complete path facts on success, identical knowledge on failure; no
   care/recovery/effect/mechanical edits.
3. Future numeric clock T1 = checked(T0 + admitted duration), exact canonical
   world-time after-image and expected scheduler progression. No wall-clock or GM
   report chooses T1. Preserve the original signed clock/control/schedule as inputs;
   derive due ordinary progression through existing Mortal arithmetic.
4. Due ordinary physical recovery from the knowledge-working cohort at T1, using
   original care/anchors/consumed cadence and the existing recovery rules. The
   selected wound's recovery predecessor is knowledge-after; unselected wounds
   retain their own original predecessors. Zero due intervals adds no fake row.

Extend the existing progression and recovery owners with a narrowly owner-bound
working-input handoff. They independently reconstruct T1, the knowledge delta and
cohort/cadence inputs against the authenticated original plus retained outcome,
then run their existing pure calculations. For recovery, pass those verified
working wounds/current minute to the existing calculation beneath PlanRegistered;
never mutate CurrentWound/CurrentGameMinute or mint another accepted-state authority.
For progression, use the same existing accepted-outcome mutation representation
and validated schedule/report arithmetic, alongside an exact world-time planned
write in the common plan. No generic caller-provided future image is accepted.

A later progression report is request-bound current response input, not part of
the original signed before-image and not permission to change the method duration.
Validate its actual session/request/turn, full control and due world/faction work
against the projected clock; freeze it only after ordinary report validation.
Consume it through the same common plan. Reject competing timeChange/setWorldTime,
changed control, missing/unresolved original clock, checked overflow, stale report
or repeated time charge. Read/cancel/refusal leaves physical clock and claims
untouched. Before publication re-prove original source equality and owner/lease;
publish world-time, schedule/report, resource/item/Fate, knowledge/recovery and
History after-images atomically. Do not write projected roots temporarily to make
an existing reader accept them.

Ordinary physical recovery during elapsed time stays with
`MortalWoundRecoveryPlanner.Plan/PlanRegistered`, the accepted recovery composer,
cadence math in `MortalWoundRecoveryPersistence.Math` and `MortalWoundRecoveryProgressProgram`.
Use their existing checked care/anchor/consumed-cadence eligibility and ordinary
selected cohort through the verified working projection above. Preserve separate
diagnosis/recovery result and row authority, exact final carrier/effect/identity/
History agreement and unchanged physical sources until the one common publication.
There is no required spiritual suffix or invented Mortal recovery progress.

### Finite causal, regression and actual-route gates

1. After independent DESIGN, author a compilable existing-API causal category:
   two complete Mortal method rows (explicit free/zero-duration and explicit
   item-cost/nonzero-duration), actual parser/member round-trip with canonical
   baseline and ordinary procedure-parameter validity proved first. Current exact
   empty-check rejection is the expected final semantic failure, not a compile or
   malformed baseline failure. Proposed initial budget60s; production unchanged.
2. Before GREEN implementation, add meaningful closed-field/omission/null and
   realm-split tests, exact path/check/policy fingerprint round-trip, current-known
   versus future/private path, mandatory current empty-check refusal and migration,
   shared-parser/alternative/seal negatives, requirements/source drift and
   old-stage refusal. Preserve only narrowly identified immutable history readers.
   Then complete request/check/Fate/resource/time/publication/replay together, with
   success/failure, natural1/20, advantage/disadvantage, exact retry/no reroll,
   changed same-ordinal control, no-cost/cost/refund, clock overflow/competing time,
   due/not-due working-image physical recovery, stale/double-spend, same-ID
   cross-purpose refusal and publication rollback controls. Cover retryable C4
   before History with a competing claim and exact carried span/Fate/resource
   agreement restoration; separate terminal cancellation/release.
3. Direct existing regressions are selected by actual changed contracts, not the
   broad 10-minute Mortal categories: exact diagnosis reducer success/failure,
   immutable path seal and `Reduce_SpiritualWoundCannotUseMortalDiagnosisAuthority`;
   `SpiritualDiagnosisAdmissionTests.GuaranteedDiagnosisDoesNotIgnoreNonemptyCheck`;
   member-shape round-trip; `PrepareProcedureRequest_ResolvesOnlyThroughLeaseBoundAcceptedState`;
   procedure signed dice span, exact retry and released-claim/Fate controls;
   ordinary resource publication and due-cadence reconstruction only if changed.
   Put exact selectors into coherent bounded categories during test authoring;
   actual discovery supplies counts/budgets before runtime, no aggregate replay.
4. Reuse independent `WoundPlayerReadFixture` for menu/confirm/cancel and the real
   typed component seed/accepted-world exporter for native procedure/publication.
   No new reflection, forged snapshots/receipts or full Engine to prove a codec.
   After cheap GREEN, qualify actual signed native success/failure/rollback and
   console/shared-browser journeys with retained terminal outcome and complete
   capture/closing under their existing peer budgets. HOLD compatibility/DFR/Windows
   and accepted173/83/110 are not runtime selections for this milestone.

### DATA ownership, GM synchronization and review boundary

The existing read context owns one menu operation's parsed wound/path/requirements.
Pass one typed method and canonical requirement-source set through each admitted
operation; freeze original versus knowledge-working images once, index immutable
History once and retain current ownership checks. Full identity includes actual
FS/lease/generation, original binding/control/wound/path/check/policy, actor/location,
every requirement/item/resource/effect source and clock/scheduler evidence. Current
confirmation, dice/Fate/resource reservation, prepublication, readback/closing and
physical rollback are fresh; mutation, retry, generation change and owner close end
reuse. No new cache/registry/publisher or speedup claim; cheap source-count controls
must substantiate any actual read reuse after implementation.

Update the current Mortal wound/procedure GM contract, CLI API/step guidance,
shared wound/status advice and applicable daemon instructions with the closed
method check/duration/policy and client-owned terminal result. Add worked success
and failure examples to `Examples/E_CLI_Wound_Materialization.txt`, exact manifest
entries and doc/source guards; preserve spiritual guarantee/empty-check guidance.
GM describes the scene and ordinary world response, not check success, arbitrary
reveals, rolled dice, resource receipt or a second time advance.

Independent DESIGN accepted the purpose-bound procedure reuse, unchanged
spiritual/alternative parser boundaries, working-image handoff and exact C4 claim
restoration. It releases no production GREEN before the actual intended parser
RED and subsequent implementation handoff. No genuine new author-rule contradiction
has been found. Exact new C# member
names/signatures, final schema spelling and fixtures remain technical implementation
choices under that review; all implementation, builds and runtime are UNRUN.


### Mortal implementation candidate status — 2026-10-31, not qualification

The existing owners now have one coherent source candidate for the approved
Mortal route: strict typed current Check and purpose, actual original/retained
claim admission, private T0→knowledge→T1→whole ordinary recovery, durable result
and History, common publication, exact held-versus-original compensation/retry,
guided console/shared browser and effective GM/current-input synchronization.
The actual ordinary report obligations stay tied to signed original control;
newly elapsed diagnosis time creates no additional same-turn report requirement.
Ordinary accepted settlement preserves original pending carry/simulation markers;
the next control build calculates newly due cycles from canonical T1 and those
markers. Elapsed wound recovery still runs in the diagnosis working projection.
Numeric clock presentation is cumulative, with no calendar arithmetic.

Independent source-authored controls include full inner/outer resealed dice/clock
joins, two-member selected-second/private cohort, genuinely published diagnosis
followed by a fresh ordinary recovery owner at the same T1, actual Shield and
pre-History/C4/outer retry claims, signed-command roles and three controlled real
Engine journeys. The latter use the involved medic's ordinary append-only journal
sidecar and preserve exact identity/skills/location; no NPC metadata exception.
All remain UNCOMPILED/UNRUN. The finite selection/source estimates and historical
causal RED are in the current plan. The same ongoing whole SOURCE, fresh builds,
exact discovery and phased outcomes must qualify this candidate. No parent or
broader treatment/adapter/HOLD scope is closed by this status note.


### Original pending observation correction — accepted bounded technical design

The actual first native diagnosis failed because both shared producers omitted
pending_wound_resolutions from originalPathPresenceV1, and ordinary optional
selection intentionally does not export an absent optional image. This is a
production evidence gap, not permission to seed or rewrite signed absence.

Current producers add that path to the shared closed inventory. Preserve the
immediately prior noncombat and combat forms for unrelated readers; diagnosis
must refuse their unobserved pending image before usable authority/claims. Do not
apply observed-optional enforcement to the complete inherited optional-root set.

The existing ReadCurrentCore captures a narrow immutable pending observation only
after authenticated manifest/authority/presence and selected-byte verification,
before CloseAndRecheck. The same OriginalSourceReadSession/FrozenOriginalSource/
evidence retains it. Present requires exact selected authenticated bytes/mapping/hash and agreement
with any supplied presence bit; its omission does not erase exact positive
coverage. Absent requires signed false and no conflicting
mapping/hash. Missing observation is not absence. Copied DATA exports do not
parse raw manifest/authority again or issue permission. Existing original/control/
physical source checks and live pending/held retention remain mandatory. No new
owner, registry, selector, cache, publisher or extra source read is introduced.

Four real producer present/absent proofs, prior noncombat/combat diagnosis
refusals, copied exports, actual source counters and targeted contradiction/drift
controls qualify this correction together with existing input-role/hold/C4/outer
consumers. The actual native failure is causal RED; code and new controls remain
UNCOMPILED/UNRUN until coherent SOURCE and parent-owned qualification. This adds
no GM-authored output or game rule; original/held evidence remains client-owned.

### Bare diagnosis pending hold — accepted envelope correction

The immutable diagnosis selection and its private held request are not a repair
wave. The catalog admits exactly the existing seven-field pending envelope with
empty repairPackets/repairReceipts and one complete diagnosis-purpose request.
Use the existing strict request parser once, then require input/root/row/snapshot/
fingerprint agreement before adding that candidate or coalescing History. Valid
matching History cannot authorize a missing or mismatched input. A bare ordinary
or mixed request, nonempty/malformed wave or joint checkpoint/publication field
refuses. Ordinary submitted treatment retains the existing real-wave contract.

This is reconstruction DATA in the same catalog, not permission: original source,
current lease/control, physical retention, exact claims, C4 rearm and outer rollback
remain under their existing owners. The actual native failure is causal evidence;
new normal/Fate roundtrip, envelope and refusal controls remain UNCOMPILED/UNRUN.
No new GM surface, repair shim, authority/cache or source read is introduced.

### Explicit lease through raw resource/effect admission

The active canonical lease is an explicit dependency of resource Core and raw
effect validation. It must accompany their current/original reads and retained
diagnosis dynamic coverage, including the existing loader's sync request/authority
freshness chain. A lease created in an async factory need not remain ambient in
its caller. Reuse selected retained currentInputs before any physical read;
otherwise use the supplied lease, preserving the existing null-lease behavior for
unrelated callers. Do not change global lock/ambient policy, original loaders,
owner identity/inventory/hash checks, mutation refusal or claim/publication guards.

Three real factory-lease controls cover inert missing-callback refusal, actual
common preparation/retained diagnosis, and ordinary composition/drift. Existing
retained resource/effect and companion read-count consumers remain selected.
This is internal ownership plumbing: no new GM surface, owner/cache or game rule.
The actual native failure is causal evidence; the correction remains UNCOMPILED/
UNRUN until coherent independent SOURCE and parent-owned qualification.

### Shared response omission and complete retained-DATA fixture

The existing complete Mortal response-surface check is shared by ordinary, joint
and diagnosis publication. Only null and JsonElement.Undefined mean omitted;
every defined foreign JsonElement kind, including explicit null, remains forbidden.
Keep the same13 supported skill/item fields and caller-controlled legacy Fate
exception. A typed internal entry point reuses that full validator for cheap
independent controls; no second validator, model/wire change or permission layer.

The retained-resource drift theory's standalone dispatcher requires signed original
schedule coverage. Prepare real scheduler/control only in that theory's existing
pre-snapshot callback, authenticate exact signed schedule bytes/hash and assert
original activeConflict. That conflict makes natural preparation return no owner
before cohort/cycle binding, preserving raw-resource routing and all exact
changedPath/final-freshness/no-mutation assertions. No post-snapshot patch or
production guard exception is permitted. Qualification remains in plan.md; new
source is UNCOMPILED/UNRUN and all native/UI/parent residuals remain open.

### Diagnosis effect-input agreement in the existing procedure family

The registry must dispatch its actual private diagnosis continuation alongside
ordinary treatment, matching the existing prepared/final/publication wrappers.
Require exact prepared/continuation identity, PreparedAgrees and the same full
ComposeMortalDiagnosisEffectInput/ComputeEffectInput equality used by diagnosis
execution. Reuse one predicate in registry admission and BuildMortalDiagnosis;
ordinary treatment's predicate stays unchanged. Keep full canonical lifecycle,
typed Fate order, commands, source/target/carrier/frame dependencies, reservation,
currentness and claims. Invalid DATA refuses; no marker-only exemption or owner.

The cheap actual-owner component has no publication binding/Fate and genuinely
one base lifecycle row. It proves complete input and foreign-scope refusal with
inert bytes/claims, not multi-event/Fate execution. Genuine order is checked by a
detached reversal within the already-required real typed-Fate native case. The
historical broader ordinary-envelope timeout remains UNKNOWN/open; approved
active coverage is typed shape9 plus actual ordinary-procedure native1 if it
passes. No new case/budget/cache/I/O/GM contract or hidden timeout qualification.


### Exact common procedure companion and original diagnosis clock

Both the original PlanningContext and completed Plan bind resource publication to
one actual mutually exclusive diagnosis-or-ordinary prepared companion. Retain
exact reference identity, seal/final item-owner checks and all pending, direct,
anchor and other-family exclusions. Both missing-resource checks include diagnosis.
WithWoundAnchorPlan reconstructs through the same context constructor and cannot
drop this resource authority. No legal diagnosis anchor is manufactured for tests.

The live physical diagnosis clock remains signed original T0, including exact
already-captured bytes. Only checked private effect input and whole clock/schedule/
report after-images use T1. No future canonical write or original-control re-signing
is allowed; ordinary, recovery and spiritual comparisons remain unchanged.

Successful fresh diagnosis composition may return its actual original common input
on the existing result. Invalid/cached results return no input. Input DATA getters
copy their values; its readonly PlanningContext retains existing operation authority
references and identity factories. This is inspection, not wholly detached authority
or renewed permission after lease closure. No source read, reparse, reconstruction,
cache or parallel result/owner is introduced. Tests use two independent real roots/
explicit leases for genuine foreign authority, and reuse one actual carrier assembly
for completed-constructor probes. Live ownership/freshness remains mandatory.

C8 proof and actual historical C7 refusal are recorded in plan.md; this correction
is UNCOMPILED/UNRUN. Internal binding/inspection changes no GM response field or
wire contract, so no GM-guide/manifest update is needed. Existing time guidance
already preserves T0, admitted duration/T1 and ordinary original report obligations.


### Native fixture integrity clarification

The existing closed diagnose History result is required even for generic diagnosis;
complete Mortal request/result retention is never optional. Initial private cohort
seeding must use unique nonnull attempt/operation identities and matching output
fingerprints, with complete History validated before the original is signed.
Shared acquisition events may describe several wounds; no new uniqueness rule is
introduced. Actual guided Stage may refresh the existing scheduler's lastUpdatedUtc
while rebuilding fresh control; this does not permit any other schedule/game-state
change. Reading, cancellation and confirmation remain fully inert. Fixture proof
must check exact path/field/UTC bounds, not ignore timestamp or metadata globally.

Current tests/qualification are in plan.md. These corrections are fixture-only,
with no production/cache/owner or GM wire change. Existing ordinary scheduling and
no-extra-same-turn-report guidance remains correct; no GM/manifest update is needed.


Private fixture effects require distinct creation replay events even when their
wounds share a lawful acquisition event. Generate the private effect event before
carrier/index construction; derive the index chronology from that generated effect.
Validate the complete merged index/carrier and each private canonical source/
definition through existing typed DATA APIs before initial effect writes. This
changes neither wound provenance nor production/GM authority; qualification is
recorded in plan.md and remains UNRUN for the changed cohort singleton.


### Complete dynamic publication before-images and terminal-only cleanup

The common refresh first captures its unchanged static normalizer inventory. After
actual read-only treatment preflight and before taking a nonterminal receipt or
writing, extend that same captured list with missing real plan.TouchedPaths. Read
sealed Plan.BeforeImages once; freshly read each missing path under the same lease
and require exact original presence/bytes. Preserve already captured rows without
rereading. Publish the completed list only after all checks pass; both the transaction
and refresh error compensation use it. The factory rejects incomplete touched
coverage for nonterminal publication receipts. No global inventory expansion or
late expected-image replacement is permitted.

Existing sealed IsTerminalReleaseOnly identifies the distinct cleanup role.
Terminal cleanup retains its current static command/pending before-images, never
requires an unrelated clock to equal T0, and cannot capture published agreement,
publish or rearm. Only exact request quarantine/claim settlement is allowed. No
caller Boolean, new owner/authority/cache or generic rollback exception is added.
This is a client compensation correction; GM fields/rules/manifest stay unchanged.
Current source, dependency identity/lifetime, meaningful typed counter/refusal proof
and still-UNRUN qualification are recorded in plan.md.


### Mortal Engine prepared schedule versus earlier rollback original — C11

The actual Engine captures pre-turn rollback before building fresh control and
staging diagnosis. Both existing control preparation calls may refresh the
canonical scheduler lastUpdatedUtc. Before the unchanged original producer runs,
register the prepared schedule in the same ValidationSnapshotFiles set as the
staged diagnosis command, matching the existing spiritual preparation role.
SnapshotFileOrRollbackBackupIfPresentAsync then signs current prepared schedule
bytes; rollback maps/hashes still reference exact earlier bytes. World time stays
T0, full signed control stays unchanged and PrepareDiagnosisOutcomeAsync must
continue to reject any later physical schedule/clock difference exactly.

This is a client source-role correction, not permission to ignore timestamps,
rebase after GM, change the scheduler or weaken original coverage. The real Engine
pre-wait checkpoint provides finite typed proof without GM waiting: authenticated
prepared/rollback distinction, real retained working T0/T1, independent current
schedule/clock refusals and exact same-generation original restoration after the
hook lease closes. Existing passive capture is reused. A path registration adds
no read/parse/cache/owner; actual source/claim freshness is retained. GM wire,
response fields, rules and examples stay unchanged. Current source and still-open
qualification are recorded in plan.md and T121-DIAGNOSIS-MORTAL-ENGINE-ORIGINAL.


### C12 exact recovered claims and bidirectional prepared/outer schedule roles

After the existing genuine accepted-owner recovery completes, match exactly one
recovered held diagnosis against the retained complete coordinates, request/source
fingerprints, dice span, Fate and resource agreement. Verify current reservations
and confirmed resources before changing only the retained request reference. Keep
its original owner DATA/images and phase; reject missing/ambiguous/foreign recovery.
Never make old reservation objects live or reserve a different span.

Export copied command and schedule outer images together from one frozen validated
authority envelope and already captured rollback-artifact observations. An actual
Engine original has distinct earlier rollback schedule bytes; native single-image
preparation uses signed schedule only when authenticated baseline exists and BOTH
rollback maps omit it. Asymmetric maps, missing baseline and hash/artifact mismatch
refuse. Retain these images once in the existing diagnosis owner; no per-path JSON
reparse, fresh IO or authority/cache is introduced by exporting DATA.

Outer completion compares canonical schedule against that authenticated earlier
image. Resume verifies original evidence/generation/claims and atomically publishes
outer schedule to its exact signed prepared T0 image alongside command/pending,
then reads back all three before clearing phase2. It does not rebuild control or
write future T1. Keep the real Engine operation live across restore/resume; close
inspection leases before the existing restore owner acquires its lease. Existing
strict byte, source, claim, negative and whole-original assertions remain. GM wire,
authored fields, rules and examples do not change. Current qualification is only
in plan.md; the C12 candidate is UNCOMPILED/UNRUN.


### C13 sealed Mortal diagnosis player output

Post-publication output has no live-owner admission. The existing notification
architecture must dispatch genuine MortalDiagnosisAuthority before acquisition
cardinality. Validate the existing complete immutable final/History agreement and
actual sealed execution/result, then bind the complete ordered cohort to exact
carrier mutations: changed members need one update/delete, unchanged zero-due
members need none. Reject omitted/extra/reordered/foreign DATA before any notice.
Only the selected visible diagnosis and its actual ordinary recovery yield escaped
player text and /раны; private/unselected images/facts/IDs stay internal. Success
and failed-attempt are truthful, diagnosis claims no healing, and acquisition
narration is not a requirement for this route.

Use one operation-local export of prepared/effect/final/mutation DATA and the same
existing strict final predicate. No source read/current admission/cache/authority
is introduced by notification. Existing published-output narrative read remains.
The authenticated Mortal prepared-command role and diagnosis progression completion
already dispatch correctly and are unchanged. GM wire/rules/examples remain valid;
current source and still-unqualified outcomes are only in plan.md.


### T121 C14: completed item continuity at actual post-validation

The actual C13 console refusal confirms a client-owned publication/consumer join,
not new gameplay or GM inventory authoring. Keep the original inventory guard in
place. The existing transaction may export private non-deserializable comparison
DATA only after valid Finalized settlement, no issues, ChangedCount1, exact actual
published agreement and observed successful lease disposal. Suppressed close
failure preserves the settled outcome and exports nothing; no compensation or
second spend is introduced. The completed value retains copied whole NPC bytes,
parsed root, the existing sealed before/after comparison, frame scalars and exact
generation/revision, with no executable receipt/owner or full input authority.

Engine consumes it once at the existing completed boundary and clears it on turn,
session or refusal. Repair invalidates it irreversibly. The actual parsed NPC root
is observed once under the private validator invocation; missing, ambiguous or
foreign observations cannot authorize filtering. Console comparison uses its
Usable pending-snapshot invocation and existing physical lease, before final
source recheck and close. Browser comparison uses the tracked World read API at
phase end, before full end/readset proof and close; only after successful proof
and close can the one candidate issue be removed. The browser physical owner
compares against frame scalars captured after real full original admission and
condition agreement in both construction and Seal paths, never from the override.
Native/original generation and every staged-byte/ownership proof remain mandatory.
Private scope state and atomic post-await consumption refuse concurrent invalidation.

The existing item proof is recomposed once for eligible candidate sets and then
reused as immutable comparison DATA. One fresh whole NPC byte read is necessary
because early parsing precedes the owned World phase; tracked final readset/source
proof prevents a stale-root exemption. There is no extra original parse/read,
reader/lease, cache, callback or measured-speed claim. Exactly one matching ordinary
inventory issue may be removed; duplicates/multiple candidates and all unrelated
issues retain their original order and errors. Completed DATA grants no mutation,
readmission, consumed-command reconstruction or late original replacement.

The journey's missing lawful tradeInventoryReceipts default is created with the
existing normalizer before its sole bootstrap/original. All final cost-only whole
NPC root and rollback/compensation equality checks stay exact. Existing GM response,
ordinary NPC journal/Scope/Brain, declared item cost and diagnosis/time rules are
unchanged, so synchronized GM prompts/examples/manifest need no wire update.

Finite source proposal: diagnosis actual post-validator1 + compensated/close-fail
export2 + ordinary actual consumer1 + browser exact/early-drift/final-proof3,
each category bounded60s. Browser singleton rows use actual staged Engine originals
and the existing TerminalWaitStarted controlled stop before any GM wait. Existing
engine-original1 is fresh because its shared seed changes, alongside semantic
source guards3 and explicit World closure1. These are UNCOMPILED/UNRUN estimates;
root alone owns builds/discovery/runtime. Conditional retention is218 old cases
(the prior219 pool minus engine-original1), not a fresh218 pass claim. Original
console/browser/rollback180s remain unqualified until the new finite proof succeeds.


C14 grouped fixture/completion follow-up: a protected console reader requires a
live acquired MainAdmission in the caller's async flow. Factory-created admission
is not inherited permission. Direct diagnosis/lifetime fixtures therefore release
the factory lease, acquire the caller owner, and perform genuine original-source
rebind before production. Ordinary uses the identical energy=true/Fate=false
scenario, exact persisted request/claim restore and current composition under its
caller owner. Full original request and canonical byte comparisons forbid scenario
reconfiguration or post-accepted repair. Both actual post calls begin without a
write lease. The foreign-manager child borrows the same interned root access;
completed comparison still binds its original filesystem identity. Browser3 already
uses actual Engine/current-session/main/staged scopes and releases its native
foreign lease before World access; no new browser preparation is introduced.

The one-argument completion API remains. A narrow typed overload can transfer an
owned IDisposable only to the same actually acquired completion lease. Non-null
enters existing SuppressValidationReuse before acquisition; the existing Core
BeginValidationMutation reservation survives all owning disposal and releases
only afterward. Occupied context refuses with no transfer or caller disposal;
attached context is disposed once by the actual lease. No global acquisition hook
or alternative completion exists. The genuine result/issue/Finalized/Changed1
assertions precede the close counter, then prove no completed DATA, unchanged bytes
and terminal replay without another spend. Suppressed close failure retains the
existing settlement. The initial ordinary-writer hook and missing caller owner
were fixture mistakes, not reasons to weaken production guards. Earlier failed
raw and historical False PASS stay separate; new code/controls remain UNCOMPILED/
UNRUN pending whole SOURCE/root verification. Rebind's fresh source proof is
necessary ownership, while exact request DATA is reused; no cache or speedup claim.
No command/schema/GM wire/game-rule change or new authored output is introduced.


C16 browser component correction: TerminalWaitStarted runs inside
InspectOriginalBrowserWaitAsync's short idle original scope. Its genuine Staged
value does not imply Processing=true; BeginWorldValidationRead correctly returns
null there. Actual Engine finalization later calls ClaimBrowserTerminalAsync and
BeginBrowserOriginalProcessing before its post-publication validation. No production
wait/read guard is changed, and the component does not claim that terminal claim.

All four browser controls read the actual current pending State under a real
lease, require phase staged, obtain ReadStaged DATA and bind exact request/manifest
bytes, action/request identity and detached full seal. After releasing that lease,
they install a nested BeginBrowserOriginalOperation from this actual staged value
and call the existing BeginBrowserOriginalProcessing pattern already used by the
admitted World fixture. This scope owns the whole producer/publication/validator
body under the same live main/current-session operation; no physical lease remains
at World reader entry. Nested and native foreign scopes close before the controlled
stop unwinds the Engine and its owned fixture. No invented fields, re-sign, terminal
signal, repair or canonical state addition establishes authority.

The earlier exact control consumed51.8869351s before foreign/positive suffixes, so
its incomplete prefix cannot qualify the original60s body or justify a raised
budget. Preserve positive/no-World plus production actual-frame acceptance in one
singleton; move every explicit current/foreign Matches assertion and complete
foreign override/issue-order/whole-image proof to an independent singleton. Parse
and final-proof drift remain separate singletons. All four bounds stay60s. Both
positive and foreign reuse the same existing real staged fixture helper; foreign
avoids repeating the positive/no-World/current-post body, and positive avoids
foreign fixture construction. Separate test roots/requests/owners are required for
independent authority lifetimes, so live scope/data is never cached across tests.
This is source-based isolation, with no speedup or unmeasured total-cost claim.
Current full source estimate234 cases /102 methods /61 descriptors /58 categories
is pending root discovery. Accepted post8 and historical218 remain separate;
Engine-original1/UI3 and all broader tasks/HOLDs remain open. Internal component
ownership only changes test preparation; synchronized GM surfaces need no update.
