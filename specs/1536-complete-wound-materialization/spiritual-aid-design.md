# T108: safe player aid to the current Guardian

Issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Status: technical DESIGN WIP, no implementation or runtime qualification. The
approved FR-054/FR-074 already require consent and fresh access; this document
chooses their representation and existing consumers. Routine technical decisions
do not require a new user approval pause. Independent bounded DESIGN remains
required. Full T108, combat/helper services and #1536 remain open.

## User result and exclusions

The player asks the current Guardian for help through an ordinary in-game action.
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
with a closed typed participation outcome: exact canonical record CONTENT digest
and identities/scope/status, linked to the deterministic primary recovery
transition and actual binding. Publish profile grant + witness + natural effects/
History/scheduler atomically in the same common plan. Do not bind only a tuple
that could be copied onto another record. Do not create a separate receipt.
Avoid a hash cycle: record references stable primary transition/binding; the
result contains the record digest; the record does not contain the final result FP.

The strict committed grant reader proves matching result, current cohort/identity/
History, real schedule and exact record content before draw. A raw same-turn grant
or failed/prepublication proposal lacks that witness. Current revocation or move
must still refuse before aid publication. Settled aid replay validates the already
accepted aid result and original binding, not a newly granted live consent.

### Remaining bounded DESIGN question: staging order

The existing strict published-cycle reader requires consumed command/report
absence and the exact accepted schedule afterimage. Next-turn control preparation
changes pending fields, and aid B later has new commands/report. Do not weaken that
reader or add a bypass flag. Pin its exact placement BEFORE those changes, then
carry comparison DATA only through the existing owned staging operation; enforce
fresh unchanged witness/profile/locality and the exact client-prepared scheduler
delta before draw. At B use the actual signed-start aid witness and fresh closing
checks, not a rerun of the old grant reader against the new aid phase.

Bounded review must choose the concrete existing lease/control seam and confirm
that DATA is not a transferable publication capability. No new coordinator/cache
or cross-lease owner rebinding is authorized by this draft.

## One active family and one natural pass

Use one normalized active context with explicit self/aid variants in the existing
`wound_commands` path. Aid binds healer, target/wound/fresh FP, exact participation
content/witness, cycle and source identity into its request/attempt hash. GM
`spiritualHealing` projection must state aid and both actors without exposing
private player/UI internals. Missing/invalid lower graphs retains the same attempt
and draws through repair. Actual original family/proof selects the bound I route;
live kind, graph-field presence or a serialized participation DTO grants nothing.

Keep currently PRODUCED self-v2 and no-participation natural contracts as active
closed forms, not compatibility fallbacks for old saves. A participation/aid form
must have an explicit new closed version/variant; no absent-mode defaults or
legacy migrations. Bounded review decides the exact current wire/result version
split before implementation. Update active writers/parsers/fixtures/docs together.

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

1. Close the staging-order/version decisions through bounded DESIGN. Add causal
   controls for the real accepted profile issuer/witness, not just a valid-looking
   current stamp. Make no user-facing completion claim from a codec alone.
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
   Explorer/Engine. Controlled native-shaped GM journey: player asks through an
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
