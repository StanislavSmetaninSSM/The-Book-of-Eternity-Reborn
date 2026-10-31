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
