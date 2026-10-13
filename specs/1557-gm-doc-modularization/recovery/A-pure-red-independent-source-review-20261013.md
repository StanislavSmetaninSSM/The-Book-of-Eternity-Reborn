# #1557 A pure test-first SOURCE / FIXTURE / SELECTION review

Reviewed isolated candidate `cebb8835c06a1a4a74df68a9c1994eb71ad22e0d`, base `51594aed0af8c2e33807265d5d04cbcb0dae4a22`, in `/workspace/1557-gm-doc-modularization-cloud-20261013`, using the supplied full nine-path diff and connected source/spec/disposition files. Retained actual Astra XHigh independent reviewer; no substitution. Filesystem tools expose no independent model/effort telemetry. No project, compiler, discovery, catalog, tests, daemon, provider, Git operation or agent was executed. Only static reads/data comparisons and this external report write.

**SOURCE / FIXTURE / SELECTION BLOCKED by two confirmed Important findings. No Critical finding.** No bounded actual39 release yet. This verdict concerns the pure test-first block, not full A, delivery or content-move acceptance.

## 1. Important — route predicates are not checked by the intended route guard

Locations: `BookOfEternityClient.TestSupport/GmDocumentationPreservationFixtures.cs:254–275`, especially267–270; `BookOfEternityClient.Tests/GmDocumentationModularizationTests.cs:143–170` and179–193.

`VerifyRoute` checks only required topics (column3), excluded topics (column4), core (column5) and two specific prose rules. It never reads the activation predicate (column2). Both positive theory families pass only a row ID and expected topic lists. No actual current-contract input is supplied or independently checked. Thus replacing the scheduler predicate `mustEvaluateGuardianProjectProgression=true` with false, changing a current issued frontier to a stale/private checkpoint trigger, or swapping active/inactive predicate text leaves these intended routing checks unchanged. Existing mutation rows alter topic lists/core/prose, not predicates.

The generic byte verifier could separately catch an unlisted document edit, but that does not prove the independently meaningful predicate/activation guard requested by approved FR-003/004/005/011, SC-004 and the accepted delivery-routing supplement §2. Calling these tests active/inactive controls currently overstates their semantic boundary. A self-consistent map/table also must not become its own independent policy oracle.

Minimum correction: keep this a pure documentation verifier. Supply independent expected predicate/precedence constraints for the seven families and ten active/inactive illustrations from the accepted contract, validate the actual predicate cells/directions, and add finite condition-only mutations that preserve every topic/excluded/core cell yet fail the targeted route diagnostic. At least demonstrate false-vs-true due activation and current-issued-vs-stale/private frontier activation; retain simultaneous union and restricted-family scope. No new production selector/runtime framework or provider execution is needed. Merely testing a generic document hash is insufficient for this specific oracle.

## 2. Important — D01 positive checks the old Matrix path instead of its mapped active body

Location: `GmDocumentationModularizationTests.cs:200–206`; navigation edit `N002`; content-map block `A005`.

The loop reads `edit.source` for every D01 after-image. For N002 that is `OtherGuides/Afterlife_Contract_Matrix.md`, but its complete source interval belongs to A005 whose declared destination is `OtherGuides/GM_Common_Core.md`. The Matrix is intentionally converted to a navigation surface. Consequently the correctly moved candidate will still fail line205, or the author would have to duplicate the full D01 paragraph in the old stub to appease the test. That contradicts the mapped single owner and FR-009, and fails the explicit accepted requirement that D01 semantic guards inspect rendered/moved active bodies rather than navigation stubs.

Minimum correction: for an edit inside a moved block, inspect its sole mapped active body owner; continue inspecting the original path for unmoved N001/N003. Preserve exact approved after-image and specific old-grant absence checks across the relevant active scopes. A pure complete moved candidate should satisfy this D01 oracle without inserting the body into the old Matrix; an owner/body loss or restored grant should fail its intended semantic check. The later daemon AST/dataflow work remains separate and need not be invented for this correction.

## Bounded checks and acceptable aspects

The two new C# files are pure data/document fixtures with ordinary net8 APIs and the existing unit→TestSupport reference/friend assembly. No concrete additional namespace/API compile blocker was found by source inspection; compilation is UNRUN.

Independently checked the three current source bodies against their frozen hashes and every mapped block slice: 135 owners span the full source byte ranges contiguously (94 Matrix,40 examples,1 index). All80 recorded manifest non-locator objects equal the corresponding current source objects. The full block hashes/reassembly protect payload text, IDs and companion bodies; explicit context wrappers are kept outside payload identity. JSON body/order/classification diagnostics and their owned mutation construction are distinct from generic reassembly. Manifest controls preserve full recorded runner/input/expected-write/coverage semantics rather than claiming execution of those scenarios. The initial actual destinations/manifest/router/D01 checks are deliberately UNRUN intended positives, not observed RED.

The old-anchor route inventory (124), destination wrappers and36 delivery entries remain authored metadata. Their existence is not complete actual repository/package/session link or delivery proof. The current report explicitly reserves final link/AST/dataflow and real Linux package→session publication for the later source block before moves. That honest deferred scope is not an additional blocker for this pure block. Full A must still prove it. No Block_5 move, gameplay authority change or FR063 evidence reuse is qualified here.

Static source count is exactly9 methods/39 cases: preservation5/16 and reading4/23; two exact unit owners, category2+2/default9. Current catalog contains729 owners, with no broad selectors or global-selection change in the supplied diff. The selection is appropriately bounded for the authored pure methods, but cannot compensate for findings1–2. Correct the methods/controls and update exact counts if needed, then focused SOURCE closure before any fresh official preparation/actual. Do not add unrelated afterlife/gameplay suites or claim prior source reports as execution.

The matching plan/tasks 32-line checkpoint insertions contain no new checked tasks and explicitly keep source/build/discovery/runtime acceptance pending, all original GM bodies/navigation edits unapplied, delivery adapter unwritten, A/B open. Other than the oracle issues above, their intended-versus-observed distinction is sound. The nine-path diff consists of the seven source/selection/status paths plus author/recovery records; it does not move original content or modify daemon/production.

## Read-time supporting pins

- TestSupport helper: `5ab7bb593250df6ed159b70b404226acb5f00d5146b742c008b95af094697f8b`.
- Unit fixture: `31d84aed5e16ec0af412a296cdb96015d7745cb356a2be6cb628636f47ea0d10`.
- Content map: `af1b314c513bafb2730c27a80b3847b435977df433960ec6f1a8d53df24c8b22`.
- Unapplied navigation inventory: `d2dedfdefda6fb4030fb6560546d2861b1edaa58f0e19218cec6a0709f5352d7`.
- Exact afterlife selection: `ffbe9531bdc1ed2ef2a544553eba28badfc955a23cb07f2c72a0277612a1a54e`.
- Catalog: `f2c88f046f49a9c8a79ebd238329fa486c45d63a9c214e969235d2e52a15d75d`.
- Frozen Matrix: `e69198e6db8fb00f060184ce48ec9c9aac98b2a5b2301576570bd625092521a5`.
- Frozen examples: `bef41fb951303d57fa7a9ae1f6cd7ac49ac53653d557ab4f8cddb849814a56a3`.
- Frozen index: `c33838a3a8a0d1dd5ff3305698d6a9ec861c2e3513775f2d0e7b39881f196f29`.

The separate priority FR063 one-leaf gate at a9854149 was accepted in its own report, SHA256 `679e374836852080f0067f3c8e281383845e6cdef6bf0282cf9e5e755c65881b`; no GM finding changes that release.

**FINAL STOPPED.**
