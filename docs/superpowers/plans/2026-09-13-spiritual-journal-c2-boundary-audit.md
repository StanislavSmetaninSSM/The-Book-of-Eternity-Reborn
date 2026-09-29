# Spiritual journal C2: current implementation boundaries

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536

Tracked work: `specs/1536-complete-wound-materialization/tasks.md`, T081-B2C-J2-C2.

Status: parent inspection in progress on 2026-09-13. This is a source audit for
the executable C2 plan, not that plan, a new product design, or an implementation
completion claim. C1 and C1-V1 are accepted. No C2 production/test changes have
been made. The selected architecture remains
`docs/superpowers/plans/2026-09-08-spiritual-effect-draft-journal-design.md`.

## Verified starting point

The current worktree is `D:/Games/worktrees/boe-1536-wound-materialization`,
branch `1536-complete-wound-materialization`, HEAD
`88cc9f5374a43f3c564aa60e684f04691f0435a3` plus the reviewed, uncommitted C1/V1
changes. Preserve those changes and `.serena/`. There has been no commit, staging,
push, issue closure or live spiritual publication in this recovery session.

C1's final Fast result is
`TestResults/test-lanes/20260913-022031-360-25788-7b45153e40764e18b9d0ef56d558d744-fast`:
7882/7882, exit0, 3:18.867 within default5m, no timeout, complete cleanup, no
cross-TRX duplicates. Its exact focused controls and two independent reviews are
recorded in the accepted C1 plan and scratch progress ledger. Do not rerun broad
lanes merely to start this read-only audit.

## Source ownership and original turn

`ValidationService.SpiritualWoundSourceAdmission.cs` owns acquisition through
`BeginSpiritualWoundSourceSessionAsync(CanonicalWriteLease)` and the actual
`PendingTurnSnapshotReader`. Its `SpiritualWoundSourceSession` retains original
and candidate bytes for sixteen explicitly classified paths, original request,
turn, realm, snapshot token, claimed dice, checked exchanges and source-local
pending requirements. `Owns(PreparedSpiritualSource)` checks reference membership;
an equal record or `with` clone fails that test. `BuildInputBinding()` is detached
comparison data and cannot recreate this authority.

The current acquisition entry resets `_spiritualWoundSourceSession` before each
new read. C2 needs a common retained owner whose continuation validates the same
original request/snapshot and accepted prefix, rather than treating a newly
acquired source session as the original owner. A new request must dispose or
invalidate the old owner. Cold reconstruction is still E's separate obligation.

Real source fixtures exist in
`AfterlifeResourceCutoverTests.SpiritualSourceAdmission.cs`, including Chaos Sea,
Shining Abode, forged original bytes, changed request/dice/tier/member/resource
authority, absent original conflict, terminal audit/prefix, passive side and
dice-free incoming harm. Reuse their actual signed reader and common checker.
A synthetic `ExchangeBatch` is only a resource test input, never a positive
source-admission fixture.

The following B1 pending requirements are real dependencies, not source exclusions:
`PriorStartDeclaration`, `PriorEscalationDeclaration`, `TerminalExchangeAudit`,
`TerminalExchangePrefix`, `TerminalClosure`, and `AppliedSourceBinding`.
The final C2 plan must identify the actual producer resolving each requirement.
Neither an empty pending list nor a resource interval alone admits a wound.

## Resource execution is retained, but waiting cannot yet continue

`AcceptedMechanicsPlanner.ResourceExecutionSession` owns the one
`ResourceExecutionState`, enumerator, accepted exchange IDs, next ordinal,
staged/active exchange and immutable-base routing reference. C1 preserves the
one ledger, history, arbiter, allocator registry, accepted candidates, graph
completion set and transcript builder.

`Advance()` currently sets `_pendingExchange` for both missing-side and bounded
resource waits. Later `Advance()` and `StageNextExchange()` reject that state.
In `AcceptedMechanicsPlanner.LiveTurn.cs`, both waiting branches yield their
pending observation and immediately `yield break`. Merely clearing the flag
would therefore resume an exhausted iterator, not the retained causal frontier.

A real continuation must leave the iterator/state alive and admit only validated
new evidence into that exact open boundary. It must not call `BeginLiveResourceExecution`
again, replay accepted reductions, rerun initial lifecycle resolution, reset
use seeds, replace completed-operation IDs, or mark missing side data as zero.
The one interval may be emitted only after both sides and all descendants close.

`ResourcePendingResolutionState.Resolve(receipts, context, definitions)` already
checks all pending requests and companions, exact request/session/turn/full-turn
authority, terminal replay agreement, allowed result shape and bounds. It emits
no state or bindings on error. C2 should share those checks; caller-supplied
`ResourcePendingResolvedBinding` records are not a substitute for invoking them.

The fixed-input path's `ResolvedPendingReplaySession.Bind` and
`ProjectResolvedPendingMutations` already compute exact candidate material,
component bindings, causal dependencies and receipt source exports. They currently
feed a reconstructed planning pass. Reuse their validation/projection core while
adding only the newly resolved nodes to the existing live graph. Preserve the
old fixed replay route and its `EffectPendingWaveIntegrationTests` semantics.

`ResolvePendingBoundary` additionally matches the complete pending companion set
against accepted candidate causal authority before `Resolve`. Its full-turn and
semantic envelope fingerprints are separate checks, including exact terminal
replay behavior. Sharing the receipt parser alone would omit those checks. The
new retained spiritual context must explicitly own its immutable original command
and accepted prefix; it must not weaken the existing fixed resubmission contract
to permit arbitrary future-suffix changes.

The C1 waiting observation currently exports only raw bounded outputs, boundary
ordinal and statistics. The true `AcceptedEffectBoundedResourceResolution` with
its candidate and activation-prefix authority is assembled later in the fixed
result path, after transcript sealing/freezing. Live waiting bypasses that code.
The plan needs a non-sealing owner operation producing the same authenticated
pending authority from retained execution, not a request minted from the raw
waiting DTO. `AcceptedEffectBoundaryTranscript.Builder.HasUnresolvedPendingOutput`
also reads the original accepted candidate: clearing one session flag or replacing
one graph dictionary would leave transcript closure unresolved. A validated
additive resolution must preserve the accepted activation and earlier closed
prefix while making all relevant resource/transcript views agree.

## Effect draft and generation routing

`EffectAcceptedTurnPlanner.EffectAcceptedDraft` is a real ordinary write owner,
with carrier workspace and identity/history allocator ownership. It currently
initializes that state only inside `CompleteCore` after a complete exact
five-field transcript match. Its receipts describe whole canonical phases, and
`ReadCarriers`/`ReadIdentityIndex` require successful completion. There is no
current `AdvanceForWoundInsertion`, draft-before wound authority or incremental
source registry to call by name.

`BaseResourceRouting` captures the original uncompleted effect plan, exact owner
authority and exact resource definitions. Its `Resolve` reads the original
`ResourceTriggerIndex`, and `ValidateCandidateSeeds` checks original seed equality.
This is deliberately insufficient for a newly materialized generation.

`AcceptedEffectUseArbiter.Initialize` seeds one retained budget map and replay
ordinal stream. There is no incremental materialized-instance registration or
explicit retirement API. C2 must derive new seeds from actual successful owned
creation receipts, reject every previously registered typed identity, and leave
all existing remaining uses/duplicate keys/ordinals unchanged. Retirement must
affect routing even for an instance without a consuming-use seed.

New live routing must carry the original base authority and versioned actual
source/target/skill/instance images, including creating operations and retirement.
It must not manufacture another `EffectAcceptedTurnPlan` just to obtain an index,
overwrite an earlier generation's `EffectSourceKey` binding, or make an early
materialized ordinary reaction child immediately eligible this turn.

The accepted dependency-cut algorithm remains mandatory: compute the exact
source/stack/identity/lineage read-write closure; retain an indivisible frozen
replacement batch; apply selected N/R/agreement/U operations once; then capture
the real lineage and apply ordered wound terminals/new roots. Lifetime and the
terminal winner fold remain global final passes. Decline introduces no cut.
Consumption history remains anchored before the exact replacement transition,
even though that consumption ID is allocated later.

The existing trigger index can already build directly from a carrier catalog and
target authority. The remaining fixed-plan dependency is also in
`IndexedResourceTriggerRoutingHandle.ResolvePlanSourceBinding` and the plan's
`TryResolveRoutingSourceBinding`, not only index construction. Factor that source
lookup around an actual owned routing generation, retaining the unchanged fixed
adapter and measured work accounting. A fresh index around old source bindings
would still route the wrong generation.

`WoundAcceptedTurnPlannerCore.ValidateInput` and `CreateBaselineAuthority` consume
explicit pre-turn wound/effect carriers and identity/history. Existing treatment
continuation has a private authority route but still validates its own signed
baseline and recomputed treatment seal; it cannot authorize spiritual insertion.
The new live wound route needs the distinct draft-before authority from the
accepted design and shared lower-level validators. Passing a mutable current
carrier as `PreTurnCarriers` would conceal the authority change and incorrectly
constrain a second same-turn worsening to the original generation.

## Actual validator handoff

`ValidationService.ResourceMaterialization.cs` obtains the effect plan from
`EffectAcceptedTurnPlanAuthority.TryPeekValidated` under the canonical lease and
checks its session and snapshot against the accepted resource manifest. That is
the existing authority handoff to retain. A new public constructor accepting an
arbitrary plan plus matching strings would weaken this boundary.

C2 must connect source ownership, current-generation effect state and the real
resource interval under one original-turn owner. D still owns all eight dependent
wound-profile consumers; E owns cold recovery and one common publication. A
library-only DTO, a pending guard, or positive tests with fabricated receipts
cannot satisfy C2 or close parent Unit C.

## Verification and documentation obligations for the next plan

The executable plan must give concrete code and fixtures for same-owner missing
side and multi-wave receipt continuation; unchanged accepted IDs/dice/uses;
foreign/stale/repeated receipt rejection; exact source-generation binding;
new-instance registration and old-generation retirement; legal start/terminal,
passive/champion/dice-free contours; real new wound and repeated worsening; and
the existing ordinary no-insertion/replacement goldens. Keep Fast and Integration
filters separate through `scripts/test-csharp.ps1` and retain actual result paths.

C1's no-GM-update rationale does not cover new source admission/resume contracts.
When C2's concrete runtime boundary is defined, reconcile the afterlife matrix,
GM prompts, worked turn examples, manifest and documentation guards in the same
change. Run the corresponding Focused documentation checks and conditional
FullValidation when that boundary changes. Use a meaningful Fast checkpoint
after focused evidence and independent review; PreMerge belongs to actual merge.

The next action is to reconcile the pending-request producer and current-generation
effect write path into one complete-code C2 plan. This audit introduces no new
scope exclusion, no accepted intermediate implementation and no inferred source
authority.

Thirteen inspected producer/consumer source blobs are pinned with both SHA256 and
Git blob identity in
`.superpowers/sdd/2026-09-13-spiritual-journal-c2-planning/inspected-source-baseline.json`.
This pins planning inputs including uncommitted reviewed C1 code, not test results
or authority receipts. Compare those inputs before applying a later companion.
