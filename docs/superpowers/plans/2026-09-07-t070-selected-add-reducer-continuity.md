# T070 Selected Addition: Reducer and Final Continuity Addendum

> Execute inside Task 2 of `2026-09-06-t070-complication-addition-publication.md`,
> not a separate branch or implementation lane. The existing Task 2 implementer
> remains the sole source/test writer and C# runner. Parent owns this plan.

**Tracking:** [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
`specs/1536-complete-wound-materialization/tasks.md` T070;
`contracts/mortal-wound-treatment.md` and `contracts/wound-canonical-lifecycle.md`
under that feature. T070/T177 and the feature remain open.

**Exact source BASE:** `109d4f927aebeaf75f4ba52d47e246f729454a39`.
Parent inspected the source and the complete six-section, read-only
`sdd/t070-selected-add-reducer-delta-preflight.md`. This addendum resolves two
source-proven seams in the already approved complete selected-result graph. It
does not add a gameplay choice or widen spiritual treatment.

**Parent accepted (2026-09-07):** implemented within `109d4f92..9f9f6c86` and
independently reviewed with Task2 (Spec Compliance and Code Quality Approved,
no findings). Parent inspected the exact diff and actual evidence: reducer140,
working graph31 and related coherent pure239 all GREEN; real applicability23
plus final-stamp2 GREEN; all26 selected addition rows have current passing
results, with original removal24, severity/binding39 and recovery8 preserved.
Afterlife documentation121/121 and FullValidation1856/1856 pass. The one real
Fast6015/6016 retains only the known unfinished legacy RED and is incomplete,
not a successful full Fast run. Exact artifact IDs and timing are recorded in
the original plan's Task2 acceptance and the parent artifact audit. Only this
bounded addendum is complete; T070/T177/#1536 remain open.

## Why this is required

At BASE, `ValidateTreat` calls the removal-only same-rank owner at
`WoundTransitionReducer.cs:1583`. Its branch at 2444 rejects every new effect root,
including an authenticated selected complication addition. The real effectful
Task 2 row now reaches that rejection after preparation and effect allocation.

A second mismatch exists before the roll: removing a complication prunes its
unreachable definitions; adding a new complication can then reuse an absent
semantic key. Final same-rank publication nevertheless preserves the original
root-key/effect-ID binding and intersecting definition bodies. Admission must
apply those same existing final-continuity rules. Neither the specs nor the
existing reducer establish an attempt-wide ban on pruned semantic keys; a later
legal rank reduction must still work.

## Additional file ownership

In addition to the original Task 2 allowlist, authorize only these seams:

- `BookOfEternityClient/Services/WoundTransitionReducer.cs`: narrow treatment
  mixed-delta branch, tagged ownership, and extraction of existing continuity.
- New `BookOfEternityClient/Services/WoundSameRankOwnedSourceContinuity.cs`:
  internal pure shared comparison, no public or serialized fields.
- `BookOfEternityClient/Services/MortalWoundTreatmentWorkingWoundSimulator.cs`:
  final original-to-result continuity gate in `SimulateGraph` only.
- `BookOfEternityClient.Tests/WoundTransitionReducerTests.cs` and
  `BookOfEternityClient.Tests/MortalWoundTreatmentWorkingGraphProjectionTests.cs`:
  pure RED matrix below; use existing fixture helpers.
- `BookOfEternityClient.IntegrationTests/MortalWoundTreatmentResolverTests.ComplicationGraphApplicability.cs`:
  real pre-roll continuity and resource-preservation controls.
- `BookOfEternityClient/Services/MortalWoundTreatmentOutcomePublicationPlanner.Course.cs`:
  already source-reviewed narrow pass-through of authenticated result category
  to the existing private supported-grammar helper. No new public grammar.

No working-graph append retirement ledger, canonical parser expansion, new
evidence fields, duplicate publisher, public allow-add switch, migration, skill
authority bypass, or edits to the canonical `Simulate` facade are authorized.

## Reducer design

Keep the existing four-argument `ValidateSameRankOwnedSourceDelta` as a wrapper.
All ordinary complicate, stabilize and recover callers keep its old behavior.
Factor its body into a private core receiving the original optional single
complication plus an optional treatment outcome. A new treatment wrapper selects
the mixed branch only when all of these are true:

- `before.Owner.Realm == "mortal_world"`;
- `before.Classification.Domain == "physical"`;
- `AddedComplicationIds(before, after).Count > 0`.

Other cases call the original wrapper. Only `ValidateTreat`, after its existing
zero-issue evidence, declared-outcome and severity checks, calls the new wrapper
for unchanged rank. Different-rank handling remains `ValidateFreshSeverityRootSet`.
The existing six-field `WoundTreatmentEvidence` and declared result already carry
exact before/after fingerprints and full final ID lists. They are structural
evidence, not independent authentication; the sealed selected packet and outer
publisher remain the authority. Preserve `AllowedTreatmentScope` unchanged.

Suggested private interfaces:

```csharp
ValidateSameRankTreatmentOwnedSourceDelta(before, after, outcome, issues);
ValidateSameRankOwnedSourceDeltaCore(before, after, addedComplication, treatmentOutcome, issues);
ValidateTreatmentComplicationOwnedSourceDelta(prior, current, before, after, outcome, issues);
```

In the core preserve, at their existing positions, exact retained root binding
and owner checks; original root-key continuity; ordinary single-add preservation
of every old root; filtered retained slot multiplicity/order; intersecting
definition body continuity; then delta validation; same budget/contiguous slots;
and retained slot payload checks. Do not reorder their diagnostic precedence.

The new mixed branch validates the complete set difference, not an addition
boolean or a heuristic based only on new roots:

1. New root IDs exactly equal the disjoint union of `OwnedEffectIds` of all new
   complication rows, each in that exact tagged complication domain. Effectless
   new complications contribute the empty set. No new base root, root added to a
   retained complication, or stolen retained root is legal.
2. Removed root IDs exactly equal the union owned by removed complication rows.
   No base/retained-complication root can disappear and no removed complication
   root may remain or change owner. Mixed removal plus multiple additions is legal.
3. Final definitions are exactly prior reachability from retained roots union
   current reachability from new roots, using the existing graph walker/marker
   rules. No unrelated orphan or extra definition is admitted. The shared exact
   body checks continue to protect all intersecting prior/current definition keys.
4. Every new non-marker root has reciprocal consequence slots. Preserve retained
   slot order, multiplicity and payload while allowing absolute slot compaction
   and new-root entries. Do not sort or rewrite the proposed after-image.

Replace the private string ownership map with the existing
`WoundRootOwnershipDomain.BaseWound` / `.ForComplication(id)` value objects.
Use record equality at both retained-domain and addition-domain comparisons.
The valid complication ID `base_wound` must not alias base ownership. Preserve
all existing diagnostics and original no-addition behavior except this demonstrated
identity collision; do not opportunistically tighten retained complication metadata.

## One final same-rank continuity owner

Extract exactly the two original loops at reducer 2280–2294 and 2330–2346:

```csharp
internal sealed record WoundRootDefinitionRebinding(
    string DefinitionKey, WoundWorkingReference Before, WoundWorkingReference After);
internal sealed record WoundSameRankSourceContinuityResult(
    WoundRootDefinitionRebinding? RootRebinding, string? ChangedDefinitionKey);

// On WoundSameRankOwnedSourceContinuity:
internal static WoundSameRankSourceContinuityResult Compare(
    IReadOnlyDictionary<string, WoundWorkingReference> beforeRootByDefinitionKey,
    IReadOnlyDictionary<string, WoundWorkingReference> afterRootByDefinitionKey,
    IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> beforeDefinitions,
    IReadOnlyDictionary<string, WoundOwnedEffectDefinitionFact> afterDefinitions);
```

Return the first ordinal-key mismatch in each category. The reducer adapts its
actual root IDs through `WoundWorkingReference.Existing(id)`. Consume root mismatch
at the old root-loop position with the existing `oldId->newId` diagnostic, and
body mismatch at the old body-loop position. Computing both results does not
authorize reporting the later body error ahead of intervening ownership/order gates.

`SimulateGraph` first runs its existing `SimulateCore` and per-operation validators.
On success, for Mortal/physical and unchanged **final** rank, compare original
starting graph with final graph using this owner. A mismatch returns
`NotApplicable()` before request admission/claims. Genuine Existing/DirectAddition/
PolicyAddition tagged references remain distinct; never invent canonical IDs.
Different final rank skips only this new same-rank gate, not existing envelope,
destination, retained treatment/policy/diagnosis or slot checks.

Build facts using `WoundMaterializationContract.BuildOwnedEffectDefinitionFacts`
over graph-stored definition bodies. Those bodies already use the real
`BindDefinitionToLocalWound` converter; do not compare raw unlinked proposal JSON,
add a second source-link normalizer, or export a fake canonical wound. Keep this
as a final baseline-to-result check, never inside append or per-operation validation.

## RED matrix and bounded verification

- [x] First reproduce pure same-rank effectful treatment-addition RED at the
  removal-only reducer gate, plus multiple additions and effectless zero-root case.
  Zero-root treatment still requires the upstream authenticated 0/0 batch.
- [x] Cover mixed removal/additions with retained projection, full root/definition
  closure, exact declared lists, wrong/new base or retained-complication ownership,
  stolen/disappearing old roots, retained binding/body/profile/summary/order drift,
  reachable children and orphan rejection. Include a legal `base_wound` complication
  and an illegal transfer from that complication to base, also on the no-add path.
- [x] Keep ordinary recover/stabilize/complicate rejection controls and an actually
  canonical-valid non-Mortal treatment-addition negative; do not use parser failure
  as proof of the new routing gate.
- [x] Pure graph RED: remove old complication then add same root key at same rank,
  with identical and changed bodies. Direct current-graph removal/append can succeed,
  but final admission rejects. Fresh-key replacement passes. Same-key replacement
  followed by legal one-step reduction passes destination checks. Shared-helper
  controls allow a reappearing non-root key with identical body and reject changed
  body; preserve root-rebinding-before-body diagnostic precedence.
- [x] Real `ComplicationGraphApplicability_*`: use the existing destination-rank
  fixture with an old complication root; same-rank invalid failed band is rejected
  before claims, full fixture tree is unchanged, and a fresh-key sibling still
  gets die index zero. A valid remove/add/reduce band is admitted. Strip only old
  canonical links when constructing a draft; let the existing converter restore them.
- [x] Re-run real selected publication rows and the original Task 2 matrix,
  including partial success, cold replay and post-write rollback. Successful
  effectless turns preserve wound-owned carrier/index/history, not the entire
  unrelated effect store: the fixture's seeded roll disadvantage legitimately
  ticks 3→2 at owner-turn end. Assert that tick separately. Rejected/rolled-back
  transactions retain their full-tree equality oracle.
- [x] Include full `WoundTransitionReducerTests`, working-graph tests and real
  applicability class in coherent Focused owner selections. Sole child C# owner
  uses the normal bounded runner; no extra broad lane solely for this addendum.
  Original Task 2 afterlife controls, one Fast and conditional FullValidation remain.
- [x] Update the original Task 2 Mortal guide/worked example/source guard to show
  mixed additions and exact retained-source continuity before the roll; document
  that rank reduction remains legal. No new afterlife-authored surface. Parent
  inspects actual diff/artifacts and independent exact-range Spec+Quality review
  before checking any task or accepting this correction.

Parent self-review: no new public authority, no standalone approval shortcut,
diagnostic positions preserved, tagged source identity throughout, no per-step
retirement restriction, and no false equality expectation across a successful
turn's unrelated lifecycle effects. This supplements rather than replaces the
selected packet, fresh-skill, batch, lineage and atomicity obligations in Task 2.
