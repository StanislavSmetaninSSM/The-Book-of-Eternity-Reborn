# Retained singleton pressure with actual maximum zero

Issue [#1536](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1536),
FR-025 / T081-D-RECON13. Date: 2026-11-08.
Status: proposed design, source inspection only. Implementation, builds, discovery
and runtime are UNRUN and require the parent's next gate. This is one bounded
continuation of the existing feature, not a new rule or feature.

## Intent and demonstrated gap

FR-025 requires durable comparison evidence when an existing wound increases
strain but the non-guaranteed actual wound maximum is zero. Such a source must
produce neither an opportunity nor a decision. The already implemented maneuver
route uses preimage-v2 DATA inside receipt-schema-v3; it remains distinct and intact.

The existing real-owner test
`AfterlifeResourceCutoverTests.SpiritualPressureGroupActualGuard.cs`,
`SpiritualPressureGroup_NonpositiveActualMaximumCannotAdvance`, supplies a lawful
singleton III player parent and independently successful ordinary comparison:

- Chaos Sea, continuing conflict, exact last direct-root pressure/setback exchange.
- Signed dice 4/12, harmful margin 8, standard art tier 0, resilience 3,
  hostile danger cap 4, source cap 4.
- Before clear0, ordinary strainedI: pressure 2, wound maximum 0.
- Actual fracturedII after the existing one-step burden: pressure 7, maximum 0.
- The current `AdvanceNextResourceExchangeAsync` refuses before actual evaluation,
  then revokes the owner because the singleton actual issuer requires maximum > 0.

That test is an existing refusal receipt, not a fresh RECON13 causal RED. A new
desired-success method must first prove the complete ordinary premises and fail
at actual admission. Historical refusal evidence is never renamed into success.

## Scope and approach

Use the existing singleton preimage-v3 and existing comparison-only receipt table.
Preserve the accepted singleton parent domain III/IV, including each complete
rank-specific graph and all applicable siblings. Do not add a III-only or
literal-resilience/strain guard to production merely to limit tests. The first
causal geometry is clear0 -> ordinaryI -> actualII; other existing valid singleton
geometries retain their existing admission rules. The change removes only the
unsupported positive-maximum requirement for that singleton.
Maximum0 can come from resilience or a danger cap. Neither the zero cause nor
clear/I/II becomes a new product restriction. In particular the old intrinsic
`actual-zero-ceiling` case (training cap, ordinary10/max0 -> actual15/max0) is
lawful and must move from a negative to a positive control. Contradictory claimed
maximum/cap remains negative, as does the existing invalid `actual-capped` case.

Three approaches were considered:

1. Remove only the issuer check. Rejected: source matching, intrinsic validation
   and three storage consumers would still refuse or omit evidence.
2. Add a new witness/receipt version or fake `none`. Rejected: the existing DATA
   carries every needed fact, and a zero-ceiling decision violates FR-025.
3. Extend the existing v3 singleton through the existing comparison-only table.
   Recommended: no new game rule, response field, wire family, owner API or cache.

Out of scope: pair-v4/guardian-v5 maximum0, ordinary4/no-increase, a third parent,
new actors/actions/realms, special or guaranteed arts, explicit re-trauma targets,
terminal or replacement carriers, mixed healing, native parent births, recovery
start, Engine/browser/Windows/CI qualification and full FR-025/#1536 acceptance.
Mixed receipt DATA remains a required shared-parser control; this does not claim
a new live mixed-C3 producer. Old exact OwnApSpent and four direct-cut HOLDs remain.

## Complete source-to-publication closure

Paths below are relative to `BookOfEternityClient/Services/` unless specified.

1. **Fresh actual issuer and source match.** In
   `Validation/ValidationService.SpiritualExistingPressureProjection.cs`, change
   `ReadOwnedSingletonPressureOrdinaryForProjectionUnderGateAsync` to accept a
   valid increasing actual calculation with maximum0. Pair and guardian issuers
   keep their positive-maximum checks. `PressureProjectionData` retains a private
   singleton discriminator derived from the actual `SingletonActualBasis`, not
   from caller DATA. `MatchesActualSource` permits zero only for that discriminator;
   all conflict/exchange/side/actor/input/rank/non-guaranteed checks remain.
   `AcceptedMechanicsPlanner.SpiritualOrdinaryPressure.cs` needs no rank/cardinality
   relaxation: its complete-group and `CaptureSingletonActualBasis` guards already
   admit III/IV and reject another applicable pressure-wound source.

2. **Actual owner and resource equivalence.** Keep
   `ValidationService.SpiritualWoundSourceAdmission.cs` source construction and
   `SpiritualWoundSourceTransaction.cs` `BuildProspectiveResourceBatches` on the
   same path. Admission must attach exact v3 DATA only after `MatchesActual` and
   `MatchesActualSource`; the resource batch must equal the independently proved
   ordinary transitions and contain exactly one matched source. Only actual
   strain and its final root echo change. Costs, dice, all other exchange fields
   and allocation/ownership fences remain unchanged. No extra ordinary execution
   is charged or published.

3. **Intrinsic DATA.** In `SpiritualStrainPreimageData.Pressure.cs`,
   `ReadPressureIntrinsic` accepts maximum0 only for version3. Version4 remains
   positive-only; version5 uses its unchanged separate reader. Retain the eight
   closed v3 fields, role, original raw fingerprint/pointer, exact last exchange,
   player soul, standard pressure/setback, no guarantee/target, before < ordinary
   < actual <= broken, actual = ordinary + 1, full ordinary validation and full
   III/IV parent graph/History coordinates. The shared witness still validates
   actual and recomputes its calculation before returning any DATA.

4. **No offer and cold re-derivation.**
   `ValidationService.SpiritualOriginalC1FirstOffer.cs` already returns no packet
   when every source is non-guaranteed maximum0. `CommitC2FirstTransportAsync`
   must return `no_offer`, with no pending/checkpoint/command or selected decision.
   Warm preparation followed by disposal and a fresh validator/capture over the
   same signed ordinary input must independently derive the same actual result.
   This is source-only cold re-capture, not `OpenSpiritualC2SessionAsync`: there is
   no C2 transport to reopen. No `_usesColdOriginalInputs` exemption is added.

5. **Comparison composition and exhausted C3.** In
   `Validation/ValidationService.SpiritualZeroStrainWitnesses.cs`,
   `ComposeCompletedZeroStrainWitnesses` includes supported v3 as well as v2
   zero comparisons. It continues using the existing witness builder and complete
   source ordinals, actual exchange/resource/effect facts, original bounds and
   unique dice claims. `SpiritualC3SourceOnlyReduction.cs` already requires every
   exchange/source to be exhausted, owned and zero, rechecks signed/draft/private
   inputs, and issues a receipt identity when comparisons exist. Preserve that
   identity and all these checks; no DATA-only owner construction is allowed.

6. **Existing receipt schema.** In
   `SpiritualWoundDeclineReceiptReducer.ZeroStrain.cs`,
   `IsZeroStrainComparison` admits versions2/3 only. In
   `SpiritualWoundOpportunityReceiptState.ZeroStrain.cs`,
   `ValidateStrainComparisons` accepts the already validated v2/v3 DATA only when
   actual strain increases, maximum is exactly0 and no guarantee exists. A v3
   comparison is appended only to `strainComparisons`; injury `sources` and
   `decisions` remain unchanged. Closed row fields, ordinals, source identities,
   cross-family coordinates/request/dice order, immutable prefixes and accepted
   request non-reopening remain unchanged. Schema1/2 migration and empty schema3
   healing arrays keep their current meaning. No broad `version >= 2` predicate.

7. **Common C4 and retained history.** No new implementation is expected in
   `SpiritualWoundOpportunityReceiptState.StrainPreimage.cs`:
   `ValidateStrainParentRelations`, `HasRetainedPressureComparisons`,
   `HasExactStrainHistory` and `MatchRetainedPressureHistory` already consume v3
   DATA regardless of its table. The common publisher's
   `CanonicalStateNormalizer.SpiritualPublication.cs` must still read History
   for a pressure comparison even with no materialization decisions. The signed
   next-turn frame in `ValidationService.AfterlifeSpiritualConflict.Frame.cs`
   must authenticate the declared receipt and History and require the exact
   accepted actual log. Missing/deleted/changed log or anchor refuses; a later
   lawful parent transition does not erase the recorded anchor or grant live use.

8. **Failure and retry.** Use existing pre-take validation and the existing
   `FileSystemManagerHooks.BeforeCanonicalReadOpenAsync` plus
   `CurrentCommittedPublicationWitness` late receipt-read cut. Require witnessed
   current writes before injecting the one failure. Common compensation must
   restore signed original bytes/absence, generation and publication journal;
   old capture/authority stays unusable. Reauthor only the recorded ordinary
   draft bytes, acquire fresh owners, then publish one comparison and one set of
   actual costs. No new generic fault hook, product cut API or mid-operation read.

## Verification and fixture architecture

- Cheap v3 witness/direct intrinsic tests for III and IV first. Independent literal
  arithmetic is ordinary2/max0 and actual7/max0, not an assertion calculated from
  the function under test. A separate ordinary witness with no comparison proves
  ordinary shape, dice, target and source facts before the intended failure.
- Cheap training-cap positive coverage on the already supported
  beforeI -> ordinaryII -> actualIII geometry proves the fix is not hard-coded
  to the first resilience example.
- New desired-success real-owner III/IV rows use the existing ordinal-zero
  `PressureGroupCase(Parents:1, After:"strained", Resilience:3)` construction.
  Every mutable fixture has its own directory, signed generation, services,
  owners and lease. Retained parent DATA is synthetic complete pre-sign history;
  a real owner/publication does not turn that into an executed native birth.
- Keep the obsolete singleton-zero refusal unchanged during RED. Retire only
  that method/selector at the atomic GREEN change and replace it with desired
  success coverage; keep its historical evidence and the other-wound refusal.
- Cheap receipt tests cover zero-only/prior-injury/mixed-request DATA, exact replay,
  prefix/request/dice refusals, no positive source in the zero table, v4/v5 zero
  refusal, exact/missing/changed History and log joins, and forged source-only owner.
- Real III/IV warm-to-cold source-only publication reaches common C4 and signed47;
  verify complete parent/root/identity/History preservation, actual root, exact
  3->0 AP spends once per side, original dice4/12, no decision, no private files,
  repeat/no-append semantics and current ownership. Add one III pre-take drift
  case and one III witnessed late cut/fresh retry, not a Cartesian fault matrix.
- Preserve representative existing positive singleton, v2 zero, positive pair and
  guardian controls selected by changed contracts. No full suite or all-category
  sequence. The implementation plan supplies exact proposed category ownership.

## Performance and cache contract

Reuse `SpiritualWoundSourceSession._ordinaryOperation`, existing
`DefaultOperationValidationService`, and `OrdinaryPressureComputation`
(`owned-existing-pressure-ordinary`, revision3). The ordinary algorithm has not
changed, so this work does not invent a revision or cross-operation cache.

The existing key binds session/request/snapshot/turn/realm, input role, affected
side/actor/action slot, exact raw fingerprint and pointer, complete group DATA,
cold/source revisions, next ordinal, all draft and physical fingerprints, and
capture/source/resources/effects/prefix/baseline/lease references. Structural
dependencies bind resource owner/capture/source/epoch/wound draft, ordinal,
wound/cut versions, AP before-state, all contributions and availability, and full
current source graph/routing authority. Preserve these complete identities.

Lifetime ends on error, cancellation, lease/epoch change, source revocation,
disposal or actual continuation invalidation. Cold re-capture starts a new
operation; rollback/retry never imports the old one. Fresh input/signed-origin,
owner/current dependency, pre-stage, C3 and C4 checks remain fresh even on hits.
Retained projection and parsed receipt DATA are immutable/detached; source/History
indexes remain per-parse and no permission or live owner is cached.

Measure cold ordinary, repeated warm ordinary, actual advance, cold re-capture,
C3, C4, signed-next and owned cleanup separately using existing counters/timers.
Require one ordinary evaluation followed by a warm hit with unchanged source-read
counts and increasing fresh-check counts. Ordinary queries must perform zero
actual work. Actual advance must do its real work once; later refresh work is
reported honestly, never suppressed to force a zero counter. Parser version checks
are trivial and need no cache. No speedup claim follows from this change.

## Documentation and decisions

Atomic GREEN also updates the active normative Spec Kit bodies in this feature:

- `spec.md`: FR-025's preimage-v2-only storage wording must explicitly include
  supported singleton preimage-v3; the current reconstructed player-v3 boundary
  must distinguish maximum0 comparison-only continuation from a positive offer.
- `data-model.md` section 7.7.3: replace the singleton positive-actual-maximum
  restriction and scope the blanket true-actualmax0 refusal to the still-unsupported
  pair/guardian contours. Keep the existing singleton III/IV and one-step domain.
- `contracts/wound-effects-and-atomicity.md`, the signed-existing pressure paragraph
  under Exact version-1 spiritual registry: make the same singleton maximum0
  distinction and narrow the blanket refusal without relaxing v4/v5 positive-only
  requirements or any other source, owner, History, receipt or atomicity limit.

During future GREEN the implementation worker owns only those precise normative
body sections; the parent owns the top checkpoint prefixes in spec/plan/tasks and
the requirement map, with no concurrent edit to the same section. A new checkpoint
prefix alone does not synchronize the normative contract. Preserve dated historical
receipts and their original qualification limits; do not retroactively relabel a
refusal PASS or rewrite unrelated historical findings. This review amendment edits
only design/plan, not those future normative targets.

Atomic GREEN must update the existing singleton and zero-comparison sections of
`OtherGuides/Wound_Materialization_Contract.md`, `Afterlife_Contract_Matrix.md`,
`TaskGuides/CLI_Step_Main.txt`, the existing `spiritual_wound_strain_result_v1`
worked example in `Examples/E_CLI_Afterlife_Turns.txt`, its manifest entry and typed
documentation guards. Distinguish preimage-v3 from receipt-schema-v3. Preserve the
v2 maneuver example. Replace unconditional singleton-positive/v2-only wording
without broadening pair/guardian or adding a 65th family. No new GM field or UI.

No unresolved product decision was found: FR-025 supplies the behavior and the
existing owner/storage contracts supply its implementation. Parent approval and
independent SOURCE/OUTCOME are engineering gates, not a new gameplay question.
If a further barrier needs a new owner API, new schema, unsupported live contour
or generic hook, stop and report before expanding the slice. No commit, staging,
reset, push, CI automation or diagnostic deletion is authorized.
