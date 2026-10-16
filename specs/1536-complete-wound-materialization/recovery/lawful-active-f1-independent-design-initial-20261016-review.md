Independent F1 DESIGN / affected PLAN review: **NEEDS BOUNDED DESIGN AND PLAN CORRECTIONS**. F1 is a plausible direction, but the current proposal does not yet close five concrete contract/owner boundaries below. No source inspection proves F1 impossible, so there is no basis to select F2. This review grants no implementation or runtime approval.

Reviewed `b6ba27556852253df121f6f89a7c394a66d52cd6` against `c4dde0629cc17aad75196ec509d2cdaf621945a3`. Clean checkout; exactly five documentation paths changed. Actual binary diff SHA256: `c4d35c429d107fd2a423c162d5c3cccf89f46687b7aaa39a60e759c9b3150f0a`. `git diff --check` passes. All 34 inspected source/governance/historical-report pins are byte-identical to the baseline; exact Git blob and SHA256 pins are in the JSON. T178–T191 each occur once as unchecked tasks.

Requested and actually configured spawn arguments were `gpt-6-astra`, `reasoning_effort=xhigh`, `fork_turns=none`, independently confirmed by the parent. Backend resolution telemetry is unavailable and is not inferred.

The findings are confirmed omissions/conflicts in this DESIGN and affected plan. No runtime defect was reproduced, because all build/test/discovery/runtime execution was expressly excluded.


**F1-D01 — HIGH: F1 has no reconciled grant and proof for child output and time changes** (confirmed_design_conflict).

Location: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:117-135`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:596-628`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:637-646`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:769-790`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:171`.

Scenario: The child resolves U and writes a different ordinary narrative/result, potentially a changed interface/debug output and nonzero remaining world time. The parent initial outputBinding and P4 story must remain authentic, while the live output must describe the child result.

Evidence: Section 4 still says interface/debug/combat stay unchanged for the entire tail and live narrative equals the initial image until a D8 terminal. Section 8.7 permits only a selected D8 terminal narrative overlay. A declined F1 never has that terminal. The new F1 text names child phase overlays but gives no output acceptance/preservation rule that supersedes these exact restrictions. World time remains a guard and the named progression cohort writes only schedule/report. Current RequireExactJointPublicOutputsAsync compares live narrative to parent bytes (MortalQteDecisionPublication.MortalJointRepair.cs:323-330); StoryService.MortalJointRepair.cs:125-151 rereads the physical original and live narrative. ProgressionScheduleService.MortalJointInputs.cs:11-13,90-102 reads world time but only calculates schedule/report after-images.

Consequence: A legitimate child result either fails the retained-parent proof or requires an undeclared output/time exception. Keeping the original narrative to satisfy the guard cannot supply the required real child result/story. This contradicts the mandatory actual ordinary resolution and closed-grant requirements.

Required correction: Define the exact F1 submitted-output/time/result surface, who authors versus publishes it, its before/accepted/current images, and the named F1 proof that preserves parent outputBinding/P4 while authenticating child output and the additional story row. Reconcile sections 4, 5/6, 8.7, 9 and the plan inventory. State which interface/debug/combat changes remain forbidden. Keep D8 limits intact; do not use a broad output waiver or grant arbitrary world writes.

Future verification: Add a positive different child narrative with parent P4 unchanged and exact child row; zero/nonzero remaining-time cases; cold repeat after child output/story; changed parent binding, foreign child output and an ungranted output mutation must HOLD.


**F1-D02 — HIGH: The existing joint common owner cannot supply the promised ordinary no-consequence path** (confirmed_design_and_plan_gap).

Location: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:594-628`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:118-119`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:312-325`.

Scenario: U succeeds with a validated no-consequence result, or fails with only an ordinary check-produced wound/resource effect and no repeated treatment/acquisition from the parent.

Evidence: The registered WoundAcceptedTurnPlanner.MortalJointRepairCommonPlan path requires a JointResourcePublicationAuthority, joint continuation/treatment lineage, and exactly one acquisition CREATE (lines 17-51,220-249). It is not an ordinary optional-contribution planner. The actual publisher split is CanonicalStateNormalizer.AcceptedMechanics.cs:103-107,286-334: NoPlan returns null; the Mortal receipt path uses the batch marker; the other ordinary path writes files individually. MortalQteDecisionPublication.CreateAsync:51-56,89-103 requires exactly one qte receipt and writes schema1. F1 names a proposed adapter, but does not define the privately bound ordinary/validated-empty input that it takes, how it obtains the actual existing ordinary candidate/take authority, or its all-or-nothing route when there is no qte receipt. The real AcceptedMechanics.cs owning partial is missing from the F1 file registry; only the joint repair partial is named.

Consequence: The planned success oracle cannot run through the named existing common authority without inventing a wound/treatment, and a non-QTE ordinary plan cannot inherit the present QTE atomic contract. Merely preventing schema1 overwrite does not supply the child mechanics receipt. This is a design/affected-plan gap, not a claim that future F1 cannot implement a bounded adapter.

Required correction: Specify a distinct privately issued F1 accepted-result capability over the genuine ordinary accepted plan, including a validated empty-contribution success and the precise source/plan/take/ack path. Reuse formulas and typed contributions without impersonating JointRepairCommonAuthority or widening its existing invariants. Explicitly name the true publisher partial and the request/result/source-kind consumers that must change or be verified (including the GameResponse/output parser destination for ordinaryResolutionResult). Give the F1 completion/cleanup capability a clear route instead of silently requiring accepted-QTE terminal proof. Remove B11 line 358's alternative transfer-only cleanup wording; lines 359 and 363-367 correctly require actual completion.

Future verification: Use real ordinary non-QTE/check and effect/resource-only plans plus validated empty success, with no parent treatment/acquisition in the child input. Prove exact child mechanics ack for each, unchanged parent M core and histories, and refusal of arbitrary/unbound plans. Existing T186 success/failure tests need these distinct source preconditions.


**F1-D03 — HIGH: Semantic source deduplication is asserted without a machine-defined coordinate** (confirmed_contract_underspecification).

Location: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:503-537`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:316-323`.

Scenario: The offer or child supplies a new sourceEventRef/unit/slot and new wound/event identity for the already committed impact, with reworded prose. Simple equality of the authored strings makes it look disjoint from the committed ledger.

Evidence: The closed slot contains only slotId/family/sourceEventRef, while semantic source coordinate and confusable alias have no defined fields, derivation, normalization/equality relation, or issuing owner. Current occurrence authority is request/producer/event-coordinate based (MortalWoundOccurrenceProducer.cs:173-243; MortalWoundOccurrenceState.cs:378-385,759-778). A new child tuple and new event coordinates naturally produce a different occurrence fingerprint. Those existing owners do not infer that two descriptions denote the same prior consequence.

Consequence: The central promise that changed IDs or paraphrasing cannot replay the committed consequence is not yet implementable or independently testable from this schema. F102 currently states the desired outcome without providing the authority needed to distinguish a duplicate from a lawful second operation on the same wound.

Required correction: Define a finite owner-derived structural source coordinate and its immutable binding into both the admitted remainder slots and every child producer contribution. State which original event/check/operation identity cannot be relabelled, and how an unresolved slot is distinguished from an already committed one before P0. Keep GM authorship of the game question explicit; do not claim that prose/hash comparison provides semantic proof. If part of the distinction remains GM-authored judgment, say so and scope the mechanically enforced guarantee accurately.

Future verification: F101/F102 must vary every caller-controlled situation/unit/slot/event/wound ID and prose while holding the actual source coordinate fixed, reject the replay, and accept a genuinely distinct authorized remainder operation on the same target.


**F1-D04 — HIGH: Restoring the child baseline can restore FD over the transferred marker** (confirmed_missing_repair_boundary).

Location: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:568-586`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:615-618`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:631-633`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:666`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:319-324`.

Scenario: FT commits a child original whose immutable snapshot contains M at FD, then the first ordinary GM response needs validation repair. The current M is transferred with target and child slots, but the child rollback baseline contains pending FD with target=null.

Evidence: F1 deliberately captures FD M in the child snapshot and otherwise reuses existing owner inventories. The existing rollback inventory includes ordinary game_state paths, including M (GameEngine.SessionAndSnapshots.cs:1902-1935); backup capture saves those bytes (1966-1985). Ordinary repair calls RestorePreTurnBackupAsync (2091-2111), which restores every validated backup (2322-2346,2366-2394). The design only says restore the post-prefix child baseline. It does not specify that the FD anchor is proof-only, which paths/phase acks are excluded from repair, or a separate F1 restore owner. The existing special joint checkpoint branch is not automatically an F1 child repair protocol.

Consequence: Following the stated baseline restore can erase the committed transfer and leave a genuine child original with a pending parent marker. Restoring the same baseline after any applied child phase could also undo accepted child effects. Reader overlays alone do not authorize a safe repair writer. This violates retained obligation/transfer and no-replay requirements.

Required correction: Specify a named F1 repair transaction with exact repairable members and phase eligibility. Preserve the current FT/phase M and current child tuple; use immutable FD only as evidence, never as a generic restore target. Before child mechanics, restore only rejected GM-owned candidate data to the authenticated post-prefix baseline; after any applied phase, recover/recompose the accepted phase rather than generic rollback. Bind repair request/ready and resubmission obligations to the same child/obligation and explicitly forbid ordinary fallback.

Future verification: Extend F106/F109 with invalid first child response after FT, process death during repair, repeated repair, and attempted repair after mechanics/story ack. Assert exact M transfer/phase receipts, child headers/request/dice and parent committed effects are retained; no FD restoration or old request regeneration.


**F1-D05 — MEDIUM: Fresh timestamp/dice have no durable reservation before FT retry** (confirmed_allocation_protocol_gap).

Location: `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:541-555`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b.md:565-586`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:171`, `specs/1536-complete-wound-materialization/design/lawful-active-joint-tail-b-implementation-plan.md:318-319`.

Scenario: FD is durable, FT preparation generates a child timestamp/dice pool, then the process dies before publication or the FT batch rolls back to FD. Retry must preserve the promised one frozen timestamp/new pool for the same obligation.

Evidence: The only target timestamp/dice fields are null until FT. FD currently stores no child preparation reservation or deterministic random seed, so a retry cannot recover values generated before FT. The existing dice owner is not a pure bound allocator: PendingTurnStateService.cs:16,29-49 writes pending_dice_state.json on GetOrCreate and reuses any valid existing pool; rotation at 115-137 generates and writes fresh random values. StagePlayerTurn uses this owner (GameEngine.TurnLifecycle.cs:1590-1596,4534-4538). The F1 write table does not name a pending-dice mutation/rotation or an alternative pure reservation contract. Calling the old helper risks a separate write or reuse of the parent pool; pure generation loses identity on precommit retry.

Consequence: Post-FT redelivery is specified, but the stronger no-reroll/one-pool claim across preparation and rolled-back FT is not supported. This is not evidence that a roll was actually repeated in runtime.

Required correction: Choose and state the allocation linearization point. Prefer freeze child timestamp/dice (and any required gacha policy) in an explicit existing-M FD/preparation field before FT, using a pure existing dice calculation; alternatively precisely define and authorize a bound existing pending-dice owner operation. Keep target/header self-hash acyclic and forbid transport before the durable reservation/FT. Do not silently add a second state path or reuse parent dice.

Future verification: Add cuts after value generation but before FT/journal publication and after known FT rollback, then cold retry. Verify the same bound values, no parent pool reuse, no unlisted pending_dice_state mutation, and no GM exposure of an uncommitted request.


The useful design structure should be retained. GM authors the unresolved situation before P0; the client derives committed evidence. FD atomically retains the obligation in M. The FD → child snapshot → child manifest/token → FT → current M ordering is acyclic in principle, and current physical G plus child original plus retained parent artifacts is an appropriate proposed source chain. It is explicitly a new named reader, not authority from a capsule/hash or portable signature. Source retention through actual child completion, one M, distinct child operation IDs and a shared console/browser/agent dispatcher are appropriate requirements. Current destructive/sequential staging helpers are correctly identified as unsuitable unchanged.

F101–F110 and B08a/b/c/B11/B12/B13 cover the major intended boundaries and correctly require an actual ordinary result rather than intent/notification. Their oracles need the specific cases above so they cannot pass only a joint-positive fixture or post-FT retry. Keep category ownership finite, Late2 prerequisite ordering and independent source/fixture/selection/evidence gates. The 12 connected cases per OS supplement the fault matrix. Existing selection metadata is unchanged and is not a current F1 runtime PASS.

The Rule12 audit proposal should remain bounded to the chosen ordinary check contract and honest about GM-authored judgments. It must not silently imply that an existing universal client Rule12/narrative validator already exists, or require implementing a new general rules engine to fix these findings. Define supported structural inputs, existing calculations that are actually rechecked, and the evidence for a deterministic no-check outcome. This is a scope clarification accompanying F1-D02/F1-D03, not a reproduced additional runtime defect.

D4 allocator safety, guided US5, actual frontend DOM/visual qualification, D8 terminal/earned-XP limits, current physical G proof and nonauthorizing released observation remain preserved. Historical corrected reviews were read and do not cover these new F1 bytes. Written acceptance of the corrected F1 grants/turn policy/plan and separate implementation authorization are still required. Full FR063/US5/lawful ACTIVE remains open.

All new runtime evidence is **UNRUN**: build, tests, discovery/catalog/Plan runner, runtime/native tools, GM resolution, console/browser/agent execution, DOM/visual qualification and Linux/Windows crash execution. Only read-only Git/source/hash checks and this external report pair were performed. No repository, source, test, catalog, selection or runtime-tool file was changed; no Git mutation/push occurred. Return the corrected proposal and this review for discussion, then stop.

FINALSTOP — no pending writes. Reports are immutable after this completion.
