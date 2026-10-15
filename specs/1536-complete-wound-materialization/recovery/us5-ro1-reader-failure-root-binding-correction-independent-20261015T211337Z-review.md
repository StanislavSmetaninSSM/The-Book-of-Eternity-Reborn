# US5-RO1 reader failure and root-definition correction review

**Accepted `7c1db7294a1678a5f630cc67b0d3080cdd6e5dae` for fresh BOTH preparation, official zero-execution Plan/catalog and then the two owning categories. Corrected GREEN remains UNRUN.**

Independent causal review of frozen `ac093e21052c5b322304e794dbe753e6b993b541` plus the exact five-path correction. No source edits, native builds/tests, discovery, runner/controller calls or reflection probes were performed by reviewer. Requested Astra XHigh configuration retained; actual backend telemetry unavailable.

**R1 — the reader's strict-data exception was outside its bounded catch.** All twelve actual net8 failures are `System.IO.InvalidDataException`, raised by missing required carriers/text, duplicate JSON, or invalid UTF8. The existing `IOException` branch does not cover that type: `InvalidDataException` derives from `SystemException`. Actual TRX escape through `LoadAsync` is primary evidence; the parent's separate .NET10 reflection is supporting context only. Sources: `WoundPlayerReadService.cs:60,70,80`, `StrictJsonAuthority.cs:68`, `CanonicalJsonUtf8.cs:30`.

Adding only `InvalidDataException` to the existing exception filter is the faithful fix. Strict parsers, required inputs, lease scope, generic unavailable response and assertions are unchanged. The twelve failing cases are five missing wound carriers, missing identity/history/soul/resources, duplicate carrier JSON and invalid wound/effect UTF8. They throw at the test's read call (line76), so generic-unavailable/empty-result and post-read whole-tree assertions (lines77–81) were unreached. Successful teardown cannot turn those into behavioral passes.

**R2 — an owned root must match its exact persisted definition key.** FR-018 explicitly retains the exact source coordinate and `definitionKey`; the effects contract persists exact `{effectId, definitionKey}` root pairs. Existing `WoundEffectIdentityLineageAnalyzer.cs:212–224` independently requires the effect's key to equal its specific root binding. `EffectMechanicsSnapshot.Build` checks carrier/index structure and agreement; it does not establish this cross-wound-root association.

The old Mortal positive's `bleeding_consequence` key disagreed with the wound's `definition_wound_test_bleeding` binding. Its observed PASS is not proof of a genuine joint wound-root binding. Requiring only source kind/id, owner/target/realm and effect ID was insufficient.

The correction uses an ordinal dictionary of existing parsed `RootBindings` and requires the key for the **same effect ID** to equal `source.definitionKey`. All existing ownership/source/realm/visibility guards remain. This excludes mismatched linked effects without changing canonical state, source authority, validation vocabulary or storage membership. Merely accepting any key found elsewhere in the wound graph would not satisfy the exact pair.

Four Mortal fixtures now use their existing bleeding/pain keys, preserving the intended positive, foreign-target, hidden-complication and wrong-source checks. The new theory covers Mortal World, Chaos Sea and Shining Abode: each realm has a structurally accepted matching-key positive and a structurally accepted foreign-key negative, with carrier/index agreement, identical selected effect/owner/source identities and visible label. It asserts the label only for the positive and never exposes the foreign key. Existing afterlife semantic owner `player` / carrier OwnerKind `afterlife_profile` distinction remains correct. This review qualifies direct-root association, not full source-definition component/fingerprint validation or descendant expansion.

The affected source diff uses existing exception/JsonNode/JsonElement/LINQ APIs; parsed root uniqueness precedes dictionary construction. Only the two guards, affected effect fixtures/new three-case theory, and plan/tasks fronts changed. No catalog, selection, budget, source-definition, authority, write/delete/rollback or storage expansion; `git diff --check` is clean.

Frozen evidence independently checked:

- **20 total compressed/raw/original chains**, strict duplicate-key JSON; every packet's **2,551 source pins** against immutable tested Git `5d4b416a84e64620d8fee38a48c61595aab433ad`; all **eight original193 output pins** still match current files. The three expected current source divergences are exactly the reviewed reader, projection and effect test files.
- Full actual TRX results/definitions/entries/TestMethod/codeBase/testId/executionId bijection: **65 unique cases / 33 methods**, native1 / **12.5043371 s**, both planned descriptors fully executed, no timeout or duplicates. Projection **32 PASS**; reader **21 PASS / 12 FAIL**. Complete selection is not successful execution.
- **33 distinct genuine reader cleanup roots**, one strict receipt per reader case, `OwnedFixtureRemoved=true`, `LeaseInactive=true`, explicit `CleanupFailure=null`, absolute paths currently absent. Pure projection cases have no root receipts.
- Reader's five passing fault cases are stale history, duplicate identity, stale identity, legacy shape and foreign owner. The **16 other passing reader cases include inventory/privacy negatives**; the plan/tasks phrase “positive16” should be corrected in the next metadata carrier. Frozen evidence stays unchanged.

Packet SHA256 anchors:

- Actual failure: `c7a897796018ebe5e2e422ae32b7fbd2eb8be39ddb9652d3751883fdd2b95b64` (eight chains).
- Prior corrected catalog: `51356dae85ac77d35bdb5ffaa8c2fc66ecc345443e9c69edaff5a7500bfb47af` (six chains, valid756/11640, zero execution).
- Prior exact Plan: `e68c195dce802dbebcf9a3b31615838603e21b572868114bc989caaf1fd3889a` (six chains, zero execution).

Released next: **fresh BOTH selected-project builds**, new pins, official zero-execution Plan/catalog, then only `wounds-player-projection` and `wounds-player-read`. Expected counts are provisional until discovery: projection **26 methods / 35 cases**, reader **8 / 33**, total **34 / 68 / two descriptors**, unchanged three-minute category budgets. Old193 binaries cannot prove corrected source. Require all68 actual cases, all17 reader refusal assertions and whole-tree comparisons, and all33 genuine cleanup receipts. Keep the accepted shell exit-capture handling; first material discrepancy freezes before dependent work. Final independent evidence acceptance is still required.

Current local HEAD is clean; parent reports actual remote7c1d verified. Prior failures remain immutable. No public-wiring/fullRO1/fullUS5 acceptance follows from this static gate. RO2 existing console/browser status consumers remains next tracked work after RO1 evidence; fullFR063/T070/#1536/lawfulACTIVE remain open.

**FINALSTOP — only this external MD/JSON pair written; no pending reviewer writes.**

