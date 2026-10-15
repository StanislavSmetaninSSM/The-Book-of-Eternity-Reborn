# US5-RO2 initial RED source/fixture/selection review

**HOLD preparation/runtime: two fixture prerequisites need correction.** Reviewed exactly `681e49dd7c9208884208fa392a8b7a95fcc46002..627643abe902c03c0ed9132629e84eb80cce93ae`: nine paths, no production changes. Configured independent Astra XHigh retained; actual backend telemetry unavailable.

1. **Mortal privacy fixture is invalid before status execution** (`WoundPlayerStatusTests.cs:79–81`). It clears `knownRouteIds`, hides the only route and leaves `diagnosisPaths=[]`. `InputInRealm` immediately runs materialization parsing; `MortalWoundTreatmentContract.cs:261–308` rejects that undiscoverable hidden route. Add a complete public diagnosis path revealing `route:<actual routeId>`, while keeping current known routes empty. Preserve the privacy assertions and validator. This is potential discovery, not current knowledge.
2. **Afterlife known-method fixture has no route** (`WoundPlayerStatusTests.cs:150–155`). `CreateSpiritualActiveWound` intentionally creates empty routes/known IDs (`WoundContractTestData.cs:355–363`). Six afterlife cases depend on a known method; four privacy/markup cases immediately index an empty array. Add one complete structurally valid known spiritual route in the local status helper before test mutations, using existing route fields/mode. Preserve generic requirement/availability notices; do not invent a spiritual resolver or executable authority.

Other bounded checks are consistent:

- Both consumers are real entrypoints: console `TryProcessCommand("/статус")` and browser `ExecuteAsync("/status")`. Constructors/hooks/block fields match existing APIs. Existing status paths call ordinary refresh and lack the new typed blocks, so read-only/typed-output RED is meaningful after fixture correction.
- Existing lease-bound generation creation occurs before the whole-tree baseline. Both calls are followed by exact tree, mutation-count and repair-hook assertions. Per-case physical roots and strict teardown remain unchanged.
- The terminal helper body is byte-identical to the formerly embedded helper (SHA256 `b00f459f3ff907e7510be5db932b209cc977420c3466a0f100068debcfd4a29f`), linked without tests. It remains accepted detached codec/history evidence, not a live publication issuer. Optional supplied input preserves the old fixture default branch.
- Fingerprints are derived after cloned fixture edits; identity/history/carrier agreement stays asserted. The existing private afterlife owner mapping is retained.
- Static selection is exactly `wounds-player-status`, integration only, **6 methods / 15 literal cases / one descriptor**, unchanged three-minute budget. New helper contains no Fact/Theory/InlineData declarations. `git diff --check` is clean.

The accepted task-order refinement covers this status slice. The test checks actual browser transport values, not DOM execution; source guards/docs/examples and final connected GREEN selection remain RO2 obligations. RO1 remains accepted at its original evidence and is not claimed rerun.

Publish only the fixture corrections for affected-only review. Fresh integration preparation, official zero-execution Plan/catalog and status-only native intentional RED are **not released by this report**. Unexpected build/setup failures must freeze before correction; intended RED must also be frozen before product changes.

No native builds/tests/runners/discovery or tracked edits were performed by reviewer. Local HEAD was clean627 at reviewed capture; parent reports remote verified. Subsequent primary fixture/front WIP and RO1 review copies appeared after this report was saved and are excluded.

**FINALSTOP — unique external MD/JSON pair only; no pending reviewer writes.**

