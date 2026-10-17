# T179-Pd.3d ordinary-A — initial concrete review

**SOURCE_NOT_READY / CHANGES_REQUIRED.** One material fixture defect, NI-S01. The finite selection is appropriate; the first preparation is valid zero-execution evidence.

Bound source: `56dbe1dedf555436d5870e24b88b37cfc0eec22c`; published preparation carrier: `ec60cc7afddffe306540f7e97f8e7dbabe8bce0a`. Product remains byte-unchanged from `a5a41cfad227ad316da96071bd7ad9c8a467c0f1`.

## NI-S01 — close failure oracle expects the wrong exception

[AcceptedTurnNarrativeInterfaceOrdinaryTests.cs:138](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient.Tests/AcceptedTurnNarrativeInterfaceOrdinaryTests.cs:138) asserts that the returned exception is the seeded IOException. The real [SessionOperationContext.cs:294](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/SessionOperationContext.cs:294) instead preserves completed callback result17 in `MainOperationContinuationException<int>`, with that IOException as its inner exception and `SessionFinalizationFailure`. Consequently this test would fail before its close-count/afterimage assertions even when product behavior is correct. No runtime reproduction was performed.

Correct only the fixture oracle: inspect the typed continuation, established result/outcome and exact retained inner/closing failure; retain public-owner return, overall nonreturn, one close and matching-afterimage checks. Bind the [actual exception declaration](/workspace/The-Book-of-Eternity-Reborn/BookOfEternityClient/Core/MainOperationContinuationException.cs). Do not change product close semantics or infer operation success from matching bytes.

## Checked scope and preparation

Boundary2 reaches real public validators and verifies both raw seed images before the structural missing-stage assertion. Four unique frozen A fragments and the exact partial template support whole-A inverse preservation, including ambient timestamp placement, catch/read order and asynchronous achievement/prose calls. The new behavioral controls and unchanged direct ordinary consumers cover the bounded extraction. Catalog transfer removes only the intended 13 unit and two integration methods from their former owners.

Catalog/BOTH completed native0 in93.495509468s; PlanOnly/NoBuild completed native0 in5.596283s. Actual discovery: **5 owners / 5 descriptors / 29 methods / 52 planned cases; zero executed**. Catalog789 categories/11755 methods-files has no unmapped/stale selectors. Independently checked all18 gzip→raw→live/committed chains, original55 versus current62 source bindings, all9 fresh DLL copies, all335 managed inventory files and exact ledger/plan membership. Both runner cleanups completed without timeout; owned namespaces stayed empty. The complete new GitHub deltas are25/all21 and33/all29, verified against recovered bytes and Git blobs.

The new ordinary fixture has a strict disposal receipt after construction. Existing SP disposal is best-effort: a later namespace-absence observation must not be described as an unavailable per-case strict receipt. Current metadata now states that limitation accurately.

## Required continuation and limits

Preserve this initial source/preparation history. Make one narrow NI-S01 tests-only correction, publish it, bind fresh managed preparation and obtain affected SOURCE review. Boundary2 remains the smallest eventual first release; the remaining50 are held for changed-product SOURCE. This report grants no runtime or production authority.

The old33-pin World fact remains unchanged, unselected and HELD; the new A inverse gate does not qualify its nine stale pins. No HOT/history, full Pd.3d, F1/M3, Windows/CI or runtime GREEN is claimed.

Support: [independent static/raw check](/tmp/t179pd3d-ordinary-tests-source-independent-check-v2-20261017.json), SHA256 `78b62dbde8e1ebf1bc20bf6d54400db2c5422454f29c9233f3791bed515c6703`.
