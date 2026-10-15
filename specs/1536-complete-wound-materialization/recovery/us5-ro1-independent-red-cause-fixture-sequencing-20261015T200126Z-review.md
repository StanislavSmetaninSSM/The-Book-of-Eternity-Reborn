# US5-RO1 independent RED cause, fixture and sequencing review

Disposition: **NARROW FIXTURE CORRECTION AND TRACKED ORDER CONSISTENT; NOT GREEN OR IMPLEMENTATION ACCEPTANCE.** Review completed 2026-10-15 UTC by independent reviewer, requested Astra XHigh; actual backend telemetry unavailable. Sole primary writer remains separate. No source changes, builds, tests, discovery, Plan, controller or native owner execution by this reviewer.

Reviewed immutable RED source `eaec69c31e49582b454701e746a104c6cf78d794`, sequencing `b5dcd37b8def8de6a117e2b76530264d84d31a0e`, evidence carrier `5550db5a1ed640657965144476dad70111f8ea53`. Read AGENTS, development workflow, accepted spec/player contract, consultation112510, refined plan/tasks, frozen tests/stub and relevant validators. Primary reports remote555 verified after transient503; this review independently checked local immutable Git bytes, not a new remote recovery.

## Recorded RED evidence

Freeze `specs/1536-complete-wound-materialization/recovery/us5-ro1-red-eaec69c31-20261015T195141Z-freeze.json` SHA256 `d09ec58be28da8b92b3eacd7f09ac35604c7d7009dcbc08a285945efe77544d3`. Independently verified all six compressed/raw/original chains, all1460 pins against tested Git and five current binary pins. Reparsed actual TRX results, definitions, method/codeBase and test/execution identities: ten methods, twelve distinct cases, one descriptor; exact plan-to-actual counts, zero duplicate/omitted/skipped cases. Recorded native exit1; 12 executed, 1 PASS, 11 FAIL, complete, no timeout,107.6223724s. Build log records90warnings/0errors. Official selection completion is true; category success is false. Cleanup succeeded as recorded; pure tests own no filesystem roots and establish no fixture-receipt PASS.

Classification: ten failures reach unavailable-only stub behavior; one fails fixture validation before Build. The sole PASS (incoherent authority) is vacuous with an always-unavailable stub and does not establish implemented validation.

During inspection current `WoundPlayerProjection.cs` changed after frozen RED. Its tested SHA `34b4f61575429d71f24f8ef406b081d5f636b6e053bad87b94856d4bc9884a98` still matches immutable Git; the other1459 pinned current files and five binaries matched at inspection. Primary confirmed later uncompiled/unreviewed projection plus Knowledge/History helpers. These WIP edits are excluded, not a RED pin failure. Current-tree freeze was released once classification was established.

## Findings and narrow correction

1. **Material fixture defect, observed.** `WoundProjectionPrivacyTests.cs:39–46` makes the sole route hidden and clears KnownRouteIds, while default DiagnosisPaths remains empty. `WoundPlayerProjectionTestData.cs:16` rejects materialization before projection; actual TRX points there. `MortalWoundTreatmentContract.cs:260–310` correctly requires every hidden route to be reachable from its structural diagnosis graph. The emitted routeId issue is consistent with `wound_treatment_hidden_route_undiscoverable`; the TRX exposes the general materialization message, not that internal code.

   Keep KnownRouteIds empty. Add a complete valid public diagnosis path revealing `route:clean_and_suture`, with empty current prerequisites and existing canonical requirements/check/failurePolicy shape; the established builder at `MortalWoundDiagnosisTests.cs:2040` is a suitable shape reference. Assert available wound plus reachable readable diagnosis, but no current method, hidden route display name, route requirement sentinel, ID or future complication. Structural discoverability does not itself disclose knowledge.

2. **Material latent fixture defect, not reached in RED.** `WoundProjectionPrivacyTests.cs:27–35` first asserts a known hidden route, then changes a physical route to gm_only. RED fails at line32, so line33 and its parser never execute. Physical route visibility is explicitly only public/known_to_player/hidden (`MortalWoundTreatmentContract.cs:21–22,121–128`); `MortalWoundTreatmentMemberShapeTests.cs:72` already treats gm_only as invalid.

   Split the already-known hidden-route positive test. Exercise gm_only on a separate valid diagnosis path, preserving an otherwise valid wound (a GM-only path cannot be the sole discovery mechanism for an unknown hidden route). Assert no diagnosis name/requirements/reveals leak. Retain route validation and all production contracts unchanged. Existing hidden/gm_only wound visibility tests remain valid.

3. **Current knowledge must remain distinct from potential discovery.** Validator fixed-point closure is an authoring reachability proof. The current selectable diagnosis rule at `WoundTransitionReducer.cs:1382–1390` uses only actual KnownRouteIds and currently public/known complications; hidden paths need nonempty satisfied prerequisites and gm_only never qualifies. Add a valid chained fixture where public path A could reveal a prerequisite for hidden path B: B and its downstream route stay absent until the actual input knowledge changes. No global diagnosis gate for already-visible wounds or known methods.

## Coverage obligations for later RO1/RO2 review

These are gaps in the twelve frozen starter cases, not findings against excluded WIP:

- Exact self/current realm: positive Mortal and both afterlife fixtures, correct soul identity, foreign NPC/actor and other-realm exclusions, hidden self, and a coherent stored old afterlife profile alongside current Mortal. Changing only Realm on a Mortal fixture proves exclusion but not positive afterlife selection.
- Full canonical agreement before filtering: identity ownership/fingerprint, complete history chain/latest transition, missing/duplicate/foreign carriers and malformed inputs yield generic unavailable, with no healthy fallback or diagnostic leakage. One display-name tamper is insufficient.
- Owned effects: use accepted nonempty effects; prove exact wound-owned/target/realm intersection and restrictive wound/effect/complication visibility; exclude hidden, unrelated and foreign effects. Baseline symptoms containing “кровотечение” do not prove effect projection. Do not serialize effect actions/bindings or canonical metadata.
- Readable declared requirements: cover typed item/resource/capability/provider labels, unknown-reference fallback and route privacy; never infer inventory, provider availability, affordability, successful treatment or sealed quote. The single energy label case is starter coverage.
- Separate authenticated History: construct genuine terminal authority and verify chain/owner/realm/visibility before readable reconstruction; exclude active rows and legacy fallback. Identity/history metadata alone supplies no wound name/severity snapshot. Where a persisted typed treatment/recovery source is used, authenticate its linkage; never infer a terminal envelope from arbitrary summary text. An always-empty History implementation must fail a positive fixture.
- Canonical reader and public consumers: one existing publication read lease, required five carriers plus identity/history/current realm/accepted effects, strict reads and real isolated filesystem missing/duplicate/foreign/realm-switch cases with unchanged full bytes/names and genuine cleanup receipts. Later RO2 must test actual console/browser parity, inert Spectre/HTML text and nested privacy. Current baseline token blacklist does not establish hostile-text safety or UI behavior.

## Sequencing and limits

Consultation112510 required an explicit refinement before early work and explicit inclusion of later public consumers. The b5 plan/tasks now name both RO1 foundation and RO2 replacement of the existing three status consumers, including documentation/examples and independent review. This is consistent with accepted FR067–076 and the player contract: immediate visible active self/current-realm wounds and known requirements; separate History; genuine privacy; no blanket diagnosis gate. The refined order is concrete and sufficient for this bounded read-only work.

Full guided aliases, target/provider workflow, quotes, confirmation, staging and mutable treatment/time semantics retain their original gate. No authority, canonical schema, membership or storage expansion is justified. Lawful ACTIVE, full US5/FR063/T070/#1536 remain open. Historical controls retain original provenance without blanket reruns.

Correct the two fixtures narrowly, retain frozen RED unchanged, and complete the already-tracked RO1/RO2 work with separate independent SOURCE/FIXTURE/SELECTION and fresh bounded verification. This report supplies causal/fixture advice and sequencing consistency only; it grants no new runtime release and accepts no current implementation or final category sufficiency.

**FINALSTOP. Reviewer writes only this external MD/JSON pair; no source edits and no pending reviewer writes.**

