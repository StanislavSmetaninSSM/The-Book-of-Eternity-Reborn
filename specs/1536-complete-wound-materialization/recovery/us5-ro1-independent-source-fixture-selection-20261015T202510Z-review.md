# US5-RO1 independent SOURCE / FIXTURE / SELECTION review

**Disposition: CHANGES REQUIRED; current GREEN release HELD.** One concrete label-authority/privacy defect and one focused afterlife-effect coverage gap require correction. No new native/build/runner release from this report.

Reviewed immutable `4fb82ae6b740a9333712a3d4e451a4ddf46dfb28` against sequencing base `b5dcd37b8def8de6a117e2b76530264d84d31a0e`:14 code/test/catalog/selection paths,1228 added lines. Local HEAD and bytes matched, tree clean. Primary reports matching actual remote; this reviewer did not repeat remote recovery. Independent reviewer requested Astra XHigh; actual backend telemetry unavailable. AGENTS/workflow, tracked RO1/RO2 outline, accepted FR067–076/player contract and prior200126 obligations govern this review. All source/fixture reads and recorded-data parsing were read-only; no builds/tests/discovery/Plan/native owner/controller calls.

## Findings

### F1 — P1: unaccepted inventory occurrences become known item labels

Location: `BookOfEternityClient/Services/WoundPlayerReadService.cs:87–98`, especially93–96.

KnownItems treats every unambiguous player_inventory occurrence with itemId as an authorized label. MortalItemCarrierCatalog is broader than accepted current inventory: `ScanPlayerInventory:223–239` scans both items and UpdateInventory; `ScanCreationCommandItemArray:625–650` accepts a creationRef proposal; `AddItem:675–712` registers itemId/name without requiring accepted identity or a materialization receipt. ContainerPath is retained but the new filter ignores it. Catalog.Issues ==0 is therefore not accepted-label validation.

Concrete source-derived scenario: a known route has target-owned item_quantity for sterile_thread. An otherwise valid reader fixture receives inventory `{"items":[],"UpdateInventory":[{"itemId":"sterile_thread","creationRef":"proposal_secret","name":"UNACCEPTED SECRET LABEL"}]}`. The proposal becomes the sole player_inventory occurrence, passes the new filter, and its name becomes a readable method requirement through Knowledge.ItemName. A bare unaccepted object in items also passes; a contained occurrence is not excluded. The player receives information without current accepted/visible top-level item authority. Existing pure tests inject KnownItemNames directly and all reader fixtures omit inventory, so none covers this boundary.

Narrow correction: select only exact current top-level items, exclude transient command/equipment/container occurrences, require existing `MortalItemMaterializationContract.TryReadAcceptedIdentity` (CanonicalPostSeal validation at193–218), exact uniqueness and permitted visibility; otherwise retain the generic “Лечебные материалы” fallback. Reuse current read lease and existing authority APIs; do not expand provider discovery, storage or mutable workflow. Add reader positive accepted-label evidence and negative unaccepted/proposal/contained/hidden/duplicate cases, with whole-tree unchanged and cleanup checks. This is a required correction before GREEN.

### F2 — P2: nonempty afterlife owned-effect branch lacks a positive oracle

Locations: `WoundPlayerProjection.cs:93–107`, `WoundProjectionPrivacyTests.Effects.cs`, realm/reader fixtures.

The projection explicitly selects afterlife_profile ownership with target player/player_soul and exact realm/source. All four nonempty accepted effect tests currently use Mortal fixtures. Positive Chaos Sea/Shining Abode tests carry empty effects, so a defect dropping every afterlife effect would still satisfy them. The separate branch and actual profile carrier need meaningful positive coverage under the accepted current-realm/visible-owned-effect contract.

Add an accepted nonempty current-self afterlife effect positive (prefer both realms), assert its readable consequence appears, and assert wrong-realm/source or hidden effect/profile input cannot leak it. Reuse existing effect/wound factories and accepted snapshot/identity parsing. This is a focused fixture/selection gap, not an observed runtime defect. No new mutable mechanics test is required.

## Source and existing fixture assessment

Outside F1, inspection found the intended boundaries in source: one PublicationReadQuiescence lease; five required wound carriers; strict UTF8/duplicate handling for canonical roots and extra effect-loader text inputs; parsed identity/history with full catalog agreement before display; exact self/current realm; coherent stored old afterlife profile permitted during Mortal life; generic unavailable on missing/incoherent required authority. Explicit effect loader overload reuses the lease. No writer, repair, reservation, quote or binding enters the result.

Projection immediately displays visible wounds, Roman severity, readable care/recovery and already-known methods. Hidden diagnosis uses current known routes and visible complications, not structural fixed-point reveals; GM-only diagnosis remains excluded. Typed declared requirements use readable labels or generic unresolved conditions and explicitly disclaim availability/price. Existing effect filtering intersects owned IDs, target/realm/owner/source and wound/complication/effect visibility; only display fields/facts are copied, not selector/action metadata. F2 remains necessary to exercise its afterlife branch.

History is explicitly partial: only complete strict reconstructed recovery stages linked to healed identity and terminal history produce display records. Parser verifies retained stages/prefix, and projection rechecks owner, IDs, before/after fingerprints and visibility. Technical summary alone, treatment-only or unproved history stays omitted. Positive/hidden/other-realm/no-snapshot fixtures cover this narrow behavior. This does not complete all T119/History/application semantics.

Static API/type scrutiny found no additional concrete compile blocker: treatment requirements/resource policy use existing typed models, strict root/read APIs and afterlife target helpers exist, recovery result/stage methods are existing, and integration helper linkage includes no tests. This is static inspection, not a build-success claim.

Existing fixtures cover current Mortal and both afterlife realms; foreign/NPC/old-profile exclusion; hidden self; missing five carriers plus identity/history/soul/resources; duplicate identity/JSON, foreign/stale authority, legacy shapes, invalid UTF8 including effect text; current versus future diagnosis; nonempty Mortal owned-effect privacy; private resource labels; separate authenticated recovery History. Reader cases own isolated roots, snapshot all descendant names/file hashes/directories, and dispose with lease-inactive/removal/explicit-null receipts. Runtime effectiveness is still UNRUN for current source.

## Recorded reader RED independently qualified as RED only

Packet `us5-ro1-reader-intentional-red-8c6164188-20261015T200826Z-freeze.json` SHA256 `0200fff03e3a34e9771d51cdb0c0e363ff2f4b1eed5a23468a46dd0a1b414457`.

Verified6 compressed/raw/original chains;2085 source pins against tested `8c6164188c7186e10087c2665e565c98c70974c9`;4 original selected output binaries. Reparsed full actual TRX results/definitions/test methods/codeBase/testId/executionId and exact plan method counts:3methods/11cases/1descriptor. Native1,67.8429414s,complete/no timeout/duplicates/skips;9 vacuous unavailable PASS and2 expected positive availability FAIL. Positive read-only assertions after availability did not run.

All11 exact per-case receipt objects have unique keys and the closed four-field shape, distinct absolute roots, removed=true, leaseInactive=true, explicit null failure; every root is currently absent including symlink checks. Original projection RED and zero-execution build failure retain their original classification. Current changed source/new tests are not represented by these old binaries or RED results.

## Selection, next gate and remaining work

Both finite categories are appropriate owners: wounds-player-projection for pure model/privacy and wounds-player-read for coherent filesystem reads; both explicitly selected, unchanged3min category budgets, integration helper-only link, no frontend/public consumer scope. Static source count predicts24methods/30cases pure plus7methods/24cases reader at4fb (31/54 total); these are not official discovery counts and will change with fixes.

F1/F2 must be corrected and published, then independently checked. After acceptance, fresh selected BOTH project builds, official zero-execution Plan/catalog and the exact two-category GREEN run are the appropriate next verification scope; no release is issued now. Require actual current-source/binary pins, completed exact cases, real per-case reader cleanup, and separate preparation/native evidence. No A7/Late2 scope reuse, aggregate or historical blanket rerun.

RO1 is still unwired/client-owned; its recorded no-GM-authored-surface-change rationale is consistent. RO2 must follow with actual existing console/browser status wiring, shared safe rendering, parity and inert markup/HTML tests, guidance/worked examples/source guards. This review must not become a stopping point at an unwired foundation. Full guided aliases/provider selection/quotes/staging, full US5/FR063/T070/#1536 and lawful ACTIVE remain open.

**FINALSTOP. Only this external MD/JSON pair written; no repository edits and no pending reviewer writes.**

