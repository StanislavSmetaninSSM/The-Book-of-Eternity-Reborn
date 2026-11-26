# Feature Specification: Mortal living-world materialization

**Feature Branch**: `1516-mortal-living-world`  
**Created**: 2026-11-16  
**Status**: WIP: full-engine journey verification reopened; not merged

## Source Issues & Scope

- Source: [#1516](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1516).
- Contract-sensitive enhancement: validation, canonical publication, GM guidance, examples, and tests.
- Mortal only. No afterlife/spiritual combat, new simulation AI, forced event generation, UI redesign, or old-save migration. Current bootstrap/test fixtures follow the supported schema.
- #1368 is closed `not_planned`, not a successful live acceptance result; this work neither reopens it nor claims its acceptance. #1540 remains separate; new private evidence must not leak through current news views.
- The user delegated routine implementation choices and requested continued work without intermediate permission pauses. Technical choices below are implementer decisions, not claimed verbatim user approval.

## User Scenarios & Testing

### US1 — Account for elapsed world time (P1)
After a meaningful time advance, the player receives a world whose accepted changes are justified, rather than advancing only personal counters. The GM may legitimately conclude that no significant autonomous event occurred.

1. Crossing the existing configured world/faction cycle boundary, including during this turn, requires a correlated complete living-world assessment.
2. Missing, stale, wrong-realm or structurally incomplete assessments fail with actionable repair information.
3. A complete bounded no-significant-change assessment is accepted without inventing NPCs, projects, actors or state deltas.
4. Short ordinary player scene actions without autonomous events are outside the trigger.

### US2 — Materialize coherent events (P1)
An autonomous event/action identifies its time window, participating entities or systems, targets, causes, visibility/knowledge, concrete canonical effects, consequences, news/chronicle evidence and provenance.

1. Exact referenced NPC/faction/location/project identities resolve against authoritative supported state.
2. Declared effects prove actual changed semantic values between authenticated original and accepted canonical state. A pending command, `filesModified`, unrelated cosmetic edit or prose alone is not effect evidence.
3. New supported autonomous event/project records are complete on first acceptance; historical/bootstrap narrative records do not retroactively invent mechanics.
4. Existing reducers remain authoritative for NPC/faction/location/project lifecycle. The report is not an arbitrary JSON mutation API.

### US3 — Preserve atomicity, privacy and recovery (P1)

1. Event effects and their versioned client-owned receipt are accepted together; reached late failure restores exact original bytes/absence through the existing publication owner.
2. Retry cannot duplicate accepted history or grant permission from serialized receipt data.
3. Private causes, undiscovered references and audit/provenance stay on a private surface. Player news receives only explicitly visible information, with both console/browser retaining their common projection.
4. A repair is bounded to the offending package/row/reference and does not authorize new semantics to be guessed.

## Requirements

- **FR-001** Derive meaningful-time trigger from authoritative preturn clock/schedule and resulting accepted clock, covering already pending cycles and this turn's newly due cycles.
- **FR-002** Require exact session/request/turn correlation; reject foreign/future/replayed reports and malformed/duplicate properties.
- **FR-003** Support explicit autonomous-change packages and legal bounded no-significant-change assessments; never force change solely to satisfy a counter.
- **FR-004** Require complete event semantics, exact cross-surface identities, explicit visibility, original/canonical effect evidence, consequence and chronicle/news proof.
- **FR-005** Validate effects after existing reducers; do not invent IDs, actors, causes, rewards, progress or missing payloads during normalization.
- **FR-006** Retain authenticated source and current ownership checks until publication; integrate common rollback, including new report/history paths and their absence.
- **FR-007** Persist versioned, source-bound client-owned immutable receipt/history once per accepted package; reject history tampering and conflicting replay. "Immutable" means the accepted owner binds and rechecks the exact declaration, history and completed-state evidence across its supported publication/recovery flow. The portable local-storage fingerprints are integrity checks, not cryptographic provenance against a coordinated rewrite of the affected joint completion marker and living-world history by the same user process. Those two relevant client-owned witnesses can be rewritten coherently without changing the authenticated original or clocks.
- **FR-008** Preserve privacy of new private evidence and existing undiscovered references. No general #1540 redesign.
- **FR-009** Synchronize current GM entrypoints, guide, autonomous/no-change worked examples, validation manifest and source/documentation tests. Document no-update decisions.
- **FR-010** Use independent cheap typed unit fixtures plus real canonical publication/rollback tests; impact-selected official runner only, no hosted CI or broad suite.

## Edge Cases

Initial 24h skip; pending cycles with no new time; short scene; absent/malformed clock; wrong or ambiguous actor; new/deleted target; project completion; stale evidence; cosmetic-only change; duplicate event IDs; hidden location references; no actors/projects; negative/reversed window; late failure; repeated normalization; changed source after preparation; Bootstrap narrative-only opening; unsupported old saves.

## Success Criteria

- Missing audit for newly meaningful elapsed time cannot pass the selected real acceptance path.
- Autonomous and legal unchanged examples pass the actual contract implementation.
- Counter-only, wrong-owner, wrong-effect, partial-package, stale-source and private-news cases fail precisely.
- A real reached late failure restores affected canonical files and receipt absence; retry succeeds once without duplicate history.
- Independent review finds no unresolved blocking requirement in the bounded implementation; limitations are recorded instead of claiming live/Windows/full-suite coverage.

## Clarifications and assumptions

Existing cycle configuration supplies the meaningful-time threshold (default world 240 minutes, faction 1440); no new arbitrary hourly threshold. The client checks explicit declarations and state evidence, not the truth of unstructured natural-language assertions through NLP. No-significant-change is an explicit GM decision with a bounded review of current relevant systems, not a client simulation result. Any significant autonomous state claim must be declared and evidenced.

## Live-discovered NPC terminal activity contract, 2026-11-18 (bounded implementation verified)

Live investigation found that the documented `completeNPCActivities` command
lacked a canonical consumer. T019 now supplies the bounded implementation,
verified by the targeted outcome recorded in live-relay-phase-plan.md. This is
a bounded completion of existing Mortal activity
materialization, not a new simulation or reward system. The delegated working
choice is explicit outcome data, never a prose-derived/default outcome:

- Require `NPCId`, `NPCName`, `activityName`, `finalState`, `finalOutcome` and
  `narrativeSummary` for an existing original active NPC activity.
- `Completed` means brought to a terminal result and admits explicit `Success`,
  `SuccessWithComplication` or `Failure`. `Abandoned` admits only `Failure`.
  Missing/unknown outcomes and the other two Abandoned combinations fail closed.
- `completionTurn` is client-owned, obtained from the exact authenticated original
  request; a command-supplied value is rejected. Do not infer identity, outcome,
  actors, XP, rewards or references from narrative text.
- Keep `finalState` command-local. Canonical history retains its existing exact
  four fields: `activityName`, integer `completionTurn`, `finalOutcome`,
  `narrativeSummary`. Clear `currentActivity` to null and append exactly once;
  preserve previous history, unrelated state and agreeing canonical NPC mirrors.
- Resolve exact original NPC/name/activity and current-source agreement before
  writes. Missing original/owner, contradictory mirrors, duplicate targets,
  same-turn newly born NPCs, mixed progress/completion or unsupported specialized
  publication routes refuse. No generic unsourced normalizer call gains authority.
- Reuse existing normalizer rollback inventory for npc_core/npc_activities and
  consume the command only inside the same accepted publication. A failed turn
  must preserve/restore its original bytes and accepted history; same-request
  repair must not append twice.
- Current fixtures/prompts/examples migrate to the explicit field; there is no
  legacy save compatibility or automatic old-command fallback requirement.

The bounded T019 implementation does not qualify other activity/faction
commands, NPC progression or the whole living-world feature. Live outcomes and
their exact limits are recorded separately in live-relay-phase-plan.md.

## Existing project partial-update preservation

For a supported partial factionProjectUpdates command against an existing exact
project, omitted fields retain their accepted values. In particular an omitted
projectName must not synthesize projectId as a replacement name. This implements
the existing Rule19.F.2 changed-fields-only contract; it does not add a new
project creation or rename capability. Preserve owner identity, unrelated
projects and completed history. The live mm2 p02 violation is tracked as T020.

## T016 live verification plan: one retained off-screen Mortal world (2026-11-26)

Continuation source: `1516-mortal-living-world-live-gm`, based on the verified
`75ac02d5ecb90e39988ab9ef2b95a34ca44c8677` checkpoint. It includes the current
Mortal contracts and the Linux GM-relay fixes. Prior scout/project/threat passes
are reference evidence, not this run's acceptance. Track completion in T016
and T018. This plan does not add autonomous AI: the GM makes decisions from the
live prompt and the client validates and publishes their effects. Mortal only.

| Phase | Ordinary player action and GM decision | Required canonical evidence |
| --- | --- | --- |
| A: baseline and control | Prepare one new current-schema test world with the player away from the NPCs and threatened forest. Establish named actors, exact locations and links, one active forest threat, and a guild road project. Derive due markers from the actual saved clock. Take a short ordinary turn that does not cross either configured boundary. | Before/after clock and due markers; player's unchanged location; no new autonomous effect, duplicate receipt, or unearned faction work. Fixture preparation is not a live result. |
| B: first world interval | The player waits or works elsewhere. GM advances the captain's murder inquiry within the interval; the courier takes an existing route and is allowed to arrive only after enough travel time. The spores advance their own activity and affect the forest. Keep this phase before the next faction boundary where possible. | Captain's task steps/minutes, courier's exact origin and destination IDs and plausible elapsed travel time, and the same threat ID still owned by the forest. Independently verify the forest's canonical condition (for example a supported custom state), not merely the threat's counters or the narrative. Verify a correlated report and publication receipt. |
| C: faction boundary and catch-up | Approach the actual next faction marker, then cross it in the same retained world. Inspect the crossing request and, if the cycle becomes pending there, the next request that observes it. For the seeded guild, adjudicate a market decision and one period of work on the road between the two locations. | Separate the living-world assessment requirement on the crossing turn from the progression report for cycles already pending on the request. Verify the exact configured world/faction counts and last-processed markers, guild agenda and project owner/steps/minutes/resources where applicable. Only credit the interval once. The usual periods are 240 and 1440 game minutes; use authenticated values rather than the host clock. |
| D: continuity and quiet control | Save and reload normally, then take a short turn and a later quiet meaningful interval if the world premise supports it. No replay of an accepted request; no forced new event for a quiet interval. | All accepted outcomes remain after reload; no duplicate faction work, project completion, NPC outcome, news, or receipt. An explicitly quiet due interval has the full five-scope audit, no event effects, and no invented deltas. Private causes stay out of public news. |

Use the existing Linux native guardian, ordinary client, launched GM daemon and
GM relay. Write each GM answer against the fresh authenticated request, not a
pre-recorded output packet. For each turn record session/request/turn, actual
world-time interval, publication state plus receipt, exactly one accepted story
entry and complete process/relay cleanup. Retain the exact accepted predecessor
for continuation; do not repair it by editing files. A relay acknowledgement,
setup fixture, unsupported action, or test-only direct state mutation is not
acceptance. If a legal command is refused, capture the earliest causal failure,
add a bounded regression, fix that contract through its existing owner, and
repeat only the affected live phase from an admissible baseline.

Preparation uses the existing `MortalLiveSeedBuilder`/`MortalLiveSeedExporter`
and `tests/fixtures/ProductionMain/live-mortal-phase.py`. Inspect support for
both exact off-screen NPC locations and a separately governed forest condition
before adapting the test seed. Preserve existing seed users: if a new setup is
needed, make it opt-in and prove it with a test before relying on it. Reuse
immutable build inputs only after source/output identity checks; each phase
must read current clock, canonical state, owner and receipt evidence freshly.
Keep raw diagnostics outside Git; update this plan with concise evidence only.
T016/T018 remain open until the connected live evidence and review are complete.

### T016 first live checkpoint, 2026-11-26

- Off-screen test seed passed the dedicated category (4/4). The opt-in
  seed uses the real ordinary canonical reservation; the default seed is
  unchanged. The courier's two-hour travel duration is scenario data, not a
  new elapsed-time validation field on the link.
- Phase A passed one actual GM-relay turn: 480→485 game minutes, one story
  append, no living-world receipt, unchanged NPC/location/threat/faction/project
  data, and successful logical/native cleanup.
- Phase B reached the next genuine request. The combined courier move and
  governed map update stopped in pre-normalization with
  `npc_core_changes_location_invalid`. The destination exists in the exact
  authenticated original. Both command-only and unchanged canonical map plus
  command wrapper were refused on the same original request. The strict
  current-map reader excludes a pending wrapper; do not grant authority to
  temporary/raw projections. Test authenticated permanent destination reuse
  before implementing any correction; reject raw-only or unauthenticated
  destinations. The attempted Phase B is not accepted and cannot be replayed
  as a new turn. Phase C and reload are not yet qualified.

### T016 second live checkpoint: pending-map fix qualified, final NPC blocker open

- Fix: an ordinary authenticated Mortal move may recover permanent location IDs
  from the exact signed original when a pending map wrapper otherwise hides the
  canonical view. Raw-only IDs and modified canonical siblings do not qualify.
  The location publication owner still validates/materializes the actual update;
  the normalizer rechecks the resulting canonical map before NPC mutation.
- Focused RED moved to 5/5 PASS. The broader existing NPC core category still
  has the same seven fixture-only storage projection failures seen before this
  fix; it is not claimed green. No new cache or shared mutable fixture.
- A fresh corrected world passed the control again. The next real turn passed
  the former map/NPC gate and the reducers produced the expected off-screen
  captain progress and courier destination in the pending candidate. It did
  not append a story or complete acceptance: final validation rejected the
  changed historical `UpdateNPCs` rows as if the GM had resent their original
  materialization and inventory. First-person journals and reasoning were also
  incomplete in the initial GM answer. The journal/reasoning repair did not
  remove those canonical-carrier refusals. Never move these NPCs into the
  player scene, drop their envelopes/inventories, or count pending after-images
  as accepted facts.
- Next: reproduce the final-validator rejection with actual normalizer-owned
  off-screen activity/movement output. Use the existing accepted-turn
  ownership/after-image comparison pattern, not a blanket exception for
  changed `UpdateNPCs` rows. Keep GM raw resend, envelope and inventory
  drift, mismatched after-image, and late-rollback refusals. Then restart in
  fresh verified state; the original combined turn is not a predecessor.
  Suited NPC reasoning, exact journal path, and output freshness will be
  included on first authoring. Faction and reload are still unverified.

### T016 phase B accepted through the live GM relay

- Final console validation now recognizes only the exact normalizer-owned
  off-screen row, using the existing completed comparison owner. The projection
  starts from the signed original and exact retained raw core, covers only
  permanent-location and active-tracker commands, and is bound to the actual
  canonical core and consumed/unchanged/missing activity carrier. Entering
  and leaving checks read these dependencies afresh; no reusable authority
  cache and no raw or specialized-route exemption.
- Actual reducer/final-validation RED reproduced the false resend and inventory
  rejection. The separate move-only RED reproduced a late activity carrier.
  Final official selection passed 12/12, including direct mutation, envelope,
  inventory, changed/deleted core and newly pending activity refusals.
- Fresh live normal-console phases A and B accepted one story entry each with
  verified logical/native closure. At 485→725, the player stayed in the
  original room, the captain advanced 0→240 minutes and step 0→1, the courier
  moved town→forest, and the same threat intensified 1→2 while independent
  forest infection state changed 0→1. One history receipt bound the three
  events to the actual request. Public news carried the courier arrival, not
  the hidden investigation or infection. Faction and project remained
  unchanged. This proves B, not the daily cycle or reload; T016/T018 stay open.

### T016 daily faction boundary and ordinary Save verified live

- Used the same accepted Mortal world and a fresh actual relay request per turn.
  The approach (725→1919) and crossing (1919→1921) left the faction's agenda,
  road project and faction marker unchanged. The crossing request had no faction
  cycle due when signed; its end made one day due for the following request.
- The next request opened at 1921 with one world and one faction cycle due.
  Exactly one accepted GM turn advanced the time to 1926 and both markers to
  1920. The merchant guild adopted the Eastgate market decision and its existing
  road project advanced 0→240 minutes and step 0→1. The project identity, name
  and resources stayed intact. Exactly one faction chronicle entry was appended;
  the captain, courier and forest state did not change on this turn.
- A fresh quiet request started at 1926 with neither cycle due and ended at
  1927. Faction core/projects/chronicles, NPC core and world map stayed
  byte-identical; no repeated project, news or assessment was requested.
  The accepted private assessment history remained at four receipts.
- The real console Save after that turn produced a verified manual archive.
  Its checked world, NPC, faction, schedule and story bytes matched the accepted
  files. Explicit Load of this archive is a separate acceptance step. These
  checkpoints do not qualify spiritual combat, general replay/rollback, all
  feature coverage, or Windows, and do not close the issue. No hosted CI or raw
  diagnostic publication.

### T016 explicit cold Load and remaining live plan

The exact turn-47 manual archive was selected in the released-path console's
ordinary **Load profile** menu on a separate empty test root. None of the 18
checked chapter files existed there beforehand. The client confirmed the load,
established a session generation and returned to the Mortal player screen at
turn 47 and game minute 1927. The 18 files matched the archive byte for byte,
including the NPCs, forest, faction/project/chronicle, world clock and markers,
private living-world history and story. No GM turn was sent by the load. The
exit interaction of the cold probe was not verified as an ordinary exit; the
process guardian confirmed descendant cleanup. The accepted source world and
the original archive are unchanged. This is cold console Load continuity, not
an active GM-owner replacement proof or a post-Load GM turn.

Remaining sequence for this live check, without unrelated Mortal redesign:

1. On the retained accepted world, drive a fresh world boundary before
   concluding the captain's investigation. Use the actual signed request and
   the existing explicit terminal activity outcome. Verify the active tracker
   clears and one exact result is appended, with no private detail in news.
2. Continue from a verified predecessor. Only on the requisite signed faction
   cycle(s), advance the existing road toward its declared cost and finish it
   through the existing project path. Verify ownership, completion/history,
   public consequence and no repeated event on a subsequent short turn.
3. Review the reached cases against T016 and T018. Keep evidence for missing
   gameplay cases separate from relay, Save and Load success. Record a
   reproducible defect only after the actual supported path refuses or changes
   the wrong state. No spiritual combat, old-save migration, hosted CI, raw
   artifacts in Git, merge or issue closure in this checkpoint.

### T016 off-screen terminal activity: live refusal and bounded correction plan

- Turn 48 accepted an advance from 1927 to 2167. No cycle was pending in its
  signed opening request; it crossed the world threshold without mutating the
  captain, forest or guild. The next actual request carried one world cycle
  and zero faction cycles.
- Turn 49's exact relay packet used the existing terminal activity command
  for the captain outside the player's scene. The real reducer cleared its
  active tracker and produced one explicitly qualified completed result.
  A separate comparison found that only the tracker and completed history
  changed in the NPC core; the historical envelope, inventory, other NPC and
  other core data were unchanged. Final validation still refused stale-style
  carrier and inventory resend errors. A separate authoring issue in the
  private debug output omitted the explicit current-location heading.
  No turn-49 story was accepted. The pending candidate is not a checkpoint.
- Add a causal test through the ordinary publication and final-validation
  boundary before changing production. Extend only the existing operation-local
  exact comparison to accept the terminal command's settled after-image,
  bound to the authenticated turn and exact original history. It must not
  excuse any raw carrier, unrelated actor changes, changed history or an
  unconsumed/reintroduced sidecar. Keep current ordinary owner checks and
  final validation unchanged for browser, specialized routes and raw input.
  Derive the expected projection once from already captured immutable DATA;
  keep current and closing authority/bytes checks fresh and add no cache.
- Verify the real reducer/final gate and its negative controls using the
  bounded ordinary-publication and terminal-activity categories. Do not
  continue the unaccepted live phase or report the road complete on that basis.
  A further live attempt requires a verified accepted predecessor and a
  corrected GM current-location section.

The actual reducer/final-boundary test reproduced the exact three historical
refusals after asserting the reducer's complete output and command consumption:
12 existing controls passed, one new case failed as intended. The correction
in e289a9a7 extends the existing completed comparison only for a terminal-only
off-screen activity carrier; it proves the exact signed original actor,
activity and turn, one appended history row, and a consumed sidecar. Raw input,
specialized and browser behavior stay unchanged. Independent source review found
no blocking issue. Fresh Linux integration build had no errors. The five
selected ordinary publication, terminal publication, refusal, source and
post-consumption categories passed 35/35, with no skips, duplicate IDs or
timeouts and complete owned cleanup (runner 61.195 s). This is not a live pass.

During the live rejection the bridge later returned an empty response. Physical
process drain was verified, but logical cleanup, terminal-owner retirement and
rollback were not. The original live story stops at 48 and the rejected request
is still pending on disk; do not resume from this root or from a cold readback
copy. The live terminal acceptance, road finish, public consequence and
following duplicate control require a new independently verified live chain on
the corrected runtime. No task or main-branch state was changed.

### T016 corrected live relay: investigation, road and duplicate control

An independently validated current test baseline on the corrected Linux build
(product e289a9a7) accepted 12 consecutive ordinary console turns, 42–53,
in one Mortal session. Each phase used one fresh actual request and one
GM-authored response through the normal relay, one accepted story append, and
verified ordinary client/owner stop and native descendant drain. All request
IDs were distinct, story hashes joined across starts, and no repair was
invoked. The 12 phase durations totalled 833.626 seconds. The rejected old
session was not reused.

- Turn 43 reproduced off-screen captain progress, the courier's town-to-forest
  movement, and both threat intensification and the separate forest infection
  effect. Turns 44, 45, 48 and 50 retained explicit five-scope audits with no
  significant change; the faction did not receive work early at the day
  boundary. Turn 46 again accepted the guild's Eastgate market decision and
  its first 240 minutes of road work.
- Turn 49 accepted the original investigation's explicit
  `SuccessWithComplication` result, whereas the previous runtime refused
  this path. The precise off-screen NPC lost its active task, gained one
  completed history record at turn 49 and one private journal entry.
  The command sidecar was consumed. The rest of the NPC core, public news,
  map and faction project state were unchanged.
- Turns 51 and 52 each opened on a signed daily faction cycle. The same road
  progressed from 240 to 1,560 to 2,880 minutes; only the final turn reached
  step 2 of 2. Turn 52 removed the active row and preserved the exact project
  and owner in the completed archive with `completionTurn: 52`. It appended
  exactly one faction chronicle entry and one public announcement. The private
  receipt used one whole-entity completion effect and one exact news reference.
- Turn 53 had neither cycle due. The accepted one-minute control left the
  project, chronicle, public news, NPC core/journal, map and living-world
  history byte-identical. The chain ended with nine distinct typed assessment
  receipts tied to their actual requests and no pending request or snapshot.

This qualifies the connected current Mortal live scenario above, not a
whole-task closure. Ordinary Continue persisted the same world between
phases; the separate cold archive Load evidence above was at turn 47 on the
earlier build, not a new cold Load after turn 53. The task checkboxes are
unchanged pending independent outcome review and reconciliation of those
limits. No gameplay/source changes, hosted CI, main merge or raw artifacts
are included in this checkpoint.
