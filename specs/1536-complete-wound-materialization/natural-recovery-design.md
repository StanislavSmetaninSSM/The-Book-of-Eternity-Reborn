# Ordinary spiritual natural recovery — bounded design draft

Issue: #1536. Canonical requirements: FR-043A and FR-049–053; tasks T105–T107.
This draft follows the qualified self-treatment/browser/console/graph-repair
slice. It grants no new runtime capability. Independent architectural review is
in progress before implementation.

## Behavior and existing machinery

An accepted ordinary safe afterlife cycle adds `1 + H` points to every eligible
active spiritual wound, using that owner's strict Spiritual Healing tier. Carry
overflow through thresholds 8/6/4/2; terminal healing removes active wounds and
retires their actual effects while retaining History. No healing selection,
request, attempt, dice or self-treatment session is created for ordinary recovery.
An active self-treatment remains first, followed by this same single natural
pass; it must not receive a second tick through the ordinary route.

Use `SpiritualWoundRecoveryPlanner`, the existing private spiritual Continuation,
shared wound effect boundary executor, common wound stage bundle, coordinated
publication and strict durable cycle replay. Generalize the spiritual program to
optional active treatment and per-owner tiers rather than implement a second
executor, scheduler, receipt file or permission/cache registry.

Eligibility comes from canonical persistent afterlife profiles and exact wound
owner coordinates in the cycle's realm, not player visibility/consent/nearby UI.
Player tier must agree with its authoritative soul mirror. Missing/invalid tiers
refuse; they never silently become zero. Reuse the existing strict art-tier reader
in `AfterlifeEntityProfileState`. Active conflict, catchup/prepared-incarnation
mode, malformed source agreement or unproved cycle identity cannot become a safe
cycle. Entity eligibility must be proved before T105 is marked complete.

## Proposed integration

1. At the real turn staging boundary, derive ordinary recovery/required weaker
   graph projection from actual control and canonical source16 DATA. Initialize
   conflict through existing initialized-baseline rollback semantics before the
   source projection; capture prepared schedule in ValidationSnapshotFiles while
   preserving original schedule rollback bytes. Put reference DATA in the existing
   TurnRequest projection; no durable wound-command envelope is produced.
2. The early real GameEngine raw phase authenticates/routs the actual original
   family only. The one complete admission, executable preparation and identity
   allocation run under the existing publication lease before normalization.
   Returned issues enter ordinary repair before any writes; no stale plan fallback.
3. Inject the owned prepared natural plan into existing ordinary effect composition
   (`ValidationService.EffectMaterialization` ComposeIdentityInput/ComposeInput),
   finalize into the existing WoundHandoffSink, then retain actual resource commands,
   capacity/owner/registered outcomes in ResourceMaterialization's common context.
   Extend the existing input-plus-bundle common authority overload for the exact
   private spiritual owner. Do not use self's bundle-only Parse(null) composer.
4. Reuse the existing spiritual strict History result family with an explicit
   ordinary mode, per-owner tier/program DATA and no request/attempt/roll. Exact
   binding/control/before+after schedule/report/cohort remain rederivable. Existing
   scheduler validation/apply and interface admission read the same durable result;
   current-state drift/collisions refuse. No History presence-only permission.
5. GM authors exactly the computed weaker destination graphs using existing closed
   profiles and derived refs. Reuse complete graph semantic comparison/current
   original evidence for repair/post-publication interface checks. Missing graphs
   retain the same turn/cycle/source and publish no partial mechanics.

The ordinary contribution itself spends nothing. It must preserve valid adjacent
resource/effect/registered outcomes of the ordinary action; self-only no-cost
quartet/command exclusions are not a universal resource-change prohibition.
Likewise, legitimate actor/profile art progression must compose with recovery
through existing typed owner transitions, never be erased or admitted from an
unchecked caller root. Exact baseline/currentness details are part of the pending
bounded architecture decision, not an invitation to relax guards.

## Alternatives and operation ownership

Appending recovery later in scheduler Apply would split wounds/effects/History
from their cycle and complicate replay. A fabricated self request would introduce
attempt/entropy/session semantics into an ordinary action. Both are rejected in
favor of the same owned program and common transaction above.

Parsed DATA belongs to the existing staging or accepted-publication operation.
Each operation reads/parses each unique selected source once and shares catalog,
tiers and program DATA among its consumers. Distinct staging/publication phases
require fresh actual physical/source authority; cached DATA never transfers a
lease or permission. Early routing does not repeat full admission/catalog/effect
allocation. No new dictionary/static cache, measurements framework or expiry.
Do not claim speedup without actual named-path measurements.

## Finite verification intent

Use minimal typed fixtures for scalar/per-owner eligibility, overflow/terminal
program, absence of attempt/draw and exact cycle seal. Exercise real accepted
publication and same-cycle/service-reopen replay, source drift, lawful neighboring
resource work, self no-double-tick, GM graph refusal/repair and persistent actor
effects/History. Reuse an actual ordinary GameEngine turn for player-facing proof.
Freeze exact category membership/budgets with independent SOURCE/PREP after the
specific tests exist. Do not repeat qualified self gates without affected
dependencies, and do not resume held Mortal/T609/fullDFR/Windows work.
