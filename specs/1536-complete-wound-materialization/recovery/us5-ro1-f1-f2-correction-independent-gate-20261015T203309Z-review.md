# US5-RO1 F1/F2 correction gate

**Disposition: ACCEPT F1/F2 corrections through2844; release fresh bounded verification for the two existing RO1 categories at this reviewed source. GREEN remains UNRUN.** No material correction blocker remains in this delta. Any subsequent source/test edit requires its affected-only gate before using this release.

Reviewed immutable `4fb82ae6b740a9333712a3d4e451a4ddf46dfb28` → `9f98889bc58ad05b4859bb635a9665966d780ef0`, plus the fixture-only precision follow-up to `2844f5422f90ccbf9e22fa46f7d2d450bbbfe5ba` inspected at20:34UTC. Primary reports actual remote2844 verified; reviewer independently checked clean local HEAD and exact changed bytes. Requested Astra XHigh; actual backend telemetry unavailable. Existing full RO1 review202510 remains the base; its tracked MD/JSON copies match original hashes. This follow-up inspected only three changed source/test paths and affected API/fixture dependencies. No repository writes/builds/tests/Plan/discovery/native owner/controller calls.

## F1 corrected

KnownItems now reads only direct inventory.items objects, groups exact item IDs before validity filtering, rejects ambiguous IDs including an unaccepted duplicate, requires null/empty contentsPath, permits only missing/null visibility or the explicit public/known_to_player/owner_visible strings, and calls existing TryReadAcceptedIdentity before taking a readable name. Non-string/unknown/private visibility cannot pass. UpdateInventory/equipment/other occurrence routes are never enumerated. Invalid or unproved labels retain the generic material requirement; no provider/availability/quote authority was introduced.

TryReadAcceptedIdentity is the existing CanonicalPostSeal validator requiring equal exact itemId/existedId, full materialization fields and valid receipt; no new writer/API was added. The nine new real-reader cases include accepted positive, proposal, bare/unaccepted item, contained item, hidden, GM-only, unknown visibility, duplicate accepted and accepted-plus-unaccepted duplicate. Target-owned requirement is prepared before wound identity/history fingerprints. Each case retains full-tree snapshots and the existing real-root disposal receipt.

Fixture API scrutiny: CreateCanonicalRoot, ResealCanonical and TryReadAcceptedIdentity are existing callable helpers/APIs. Fixture acceptance is asserted before negative mutation. A contained item is legal placement evidence: empty-by-design container semantics do not require contentsPath to be null. Extra visibility is not an unknown canonical item-envelope field, so visibility negatives are not expected to fail initial accepted-item validation.

**Proposal precision follow-up accepted at2844:** the9f fixture had an unrelated envelope/top-level creationRef conflict. The follow-up replaces it with exactly items=[] and UpdateInventory=[{itemId:sterile_thread, creationRef:proposal_secret, name:knownLabel}], matching the original source-derived leak. There is now no envelope to create the old catalog conflict, and the old occurrence filter would expose the name. The corrected direct-items-only reader excludes it. Diff confirms only this fixture object and plan/tasks precision fronts changed; production, assertions, categories and budgets are unchanged. This affected-only check is included in the present acceptance, with no further pending correction gate.

## F2 corrected

One new theory contributes two actual cases, Chaos Sea and Shining Abode. Each constructs a nonempty spiritual wound-owned effect with player/player_soul target, afterlife_profile owner in the strict effect identity index and genuine profile activeEffects carrier. It asserts accepted snapshot reconstruction before projection. Wound-owned effect ID, source wound, exact realm and readable consequence align with existing factories/contracts.

Each realm case exercises visible positive plus wrong source, wrong realm, hidden effect and hidden profile scenarios using independent clones. Wrong realm changes effect/profile realm and rebuilt effect identity together, preserving an accepted foreign effect snapshot while original wound input remains current-realm. Hidden-profile exclusion supplies SelfVisible=false, matching the existing reader-owned profile-visibility input; reader profile derivation is covered by the unchanged base case. Assertions check actual visible consequence and reject internal effect/wound IDs. Five loop scenarios per realm are not ten separately discovered test cases.

Static inspection of existing effect carrier/index, realm normalization and spiritual profile APIs found no new fixture/type/API blocker. This is not a claim that compilation or runtime fixture acceptance has already passed.

## Selection and release

Production projection/knowledge/history, helper-link project configuration, tests/categories.json and tests/selection.json are unchanged. The same finite owners remain appropriate: wounds-player-projection and wounds-player-read, each3min protective budget, no frontend or mutable-flow selection. At2844 static source predicts25methods/32cases projection plus8methods/33cases reader (33methods/65cases total); official fresh inventory must establish actual counts/descriptors. Nine added reader cases each own a real filesystem root; afterlife pure cases own none.

Authorized next verification after this correction gate: fresh selected BOTH project builds; official zero-execution Plan/catalog; bounded native run of exactly those two owning categories. Preserve current tested/compiled/source and output pins, exact actual cases/descriptors, complete status/native exit, and strict genuine per-case reader cleanup. No stale NoBuild, old A7/Late2 release, aggregate or historical blanket rerun. First unexpected preparation/fixture/native/cleanup failure remains freeze/classify-before-fix. The2844 proposal refinement has passed its affected-only gate in this report.

No current GREEN, all-history or public consumer acceptance is inferred. Prior immutable RED/build-failure evidence retains its status. RO2 existing console/browser status wiring, safe rendering/parity and GM/player guidance/examples remain next after qualified RO1; full US5/FR063/T070/#1536/lawful ACTIVE stay open.

Changed SHA256 pins:

- Reader: `42dae3059514b6df83ec6f5163831621ad7cdd5652ca66a086811c31de361e21`.
- Reader tests: `8257625a19cb666bc7751a81f6fc05f88f60c933a86418504412dc366306b620`.
- Effect privacy tests: `cd5dad4bcb0bda9c68e4a5ce882ce32696b3637c06acd9f8533e843b01ff1d8f`.

**FINALSTOP. Only this external MD/JSON pair written; no repository edits and no pending reviewer writes.**

