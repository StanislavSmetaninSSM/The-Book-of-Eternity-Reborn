# US5-RO1 first runtime failure and fixture correction review

**ACCEPTED for fresh bounded verification at `60574090d862e54ca576748b9ff8d84a01200f4c`. Corrected GREEN remains UNRUN.**

Independent causal review of immutable failure carrier `1a57b86971f297c206c4ec12a0720c0a67a96c0f`, plus the exact three-path correction through `60574090`. Requested Astra XHigh configuration is retained; actual backend telemetry is unavailable. Reviewer performed source/recorded-evidence reads only: no source edits, builds, native tests, discovery, runner/controller calls or delegation.

All four failures occurred at accepted-effect fixture prerequisites, before the intended projection assertions. The earlier static review missed these prerequisites; runtime evidence remains authoritative.

| Cause | Actual evidence and contract | Correction reviewed |
|---|---|---|
| Foreign NPC fixture (one failure) | Fixture used `npcId`; effect carrier requires exact `NPCId` (`EffectMaterializationContract.cs:227`, `EffectCarrierCatalog.cs:152`). Missing active identity occurrence is a consequent error. | Only fixture key changes to `NPCId`. Foreign target/owner, known-owned-ID collision and privacy assertion remain. |
| Two-effect fixture (one failure) | Shared helper emits constant transition/event identities (`EffectMaterializationTestFixture.cs:826`); the global identity registries reject both duplicates (`EffectIdentityState.cs:465–472`). Canonical chronology must agree with identity history (`ValidationService.EffectMaterialization.cs:1627–1638`). | Unrelated effect gets unique `transition_independent_effect` and `turn_42:independent_effect` in **both** canonical chronology and its generated index transition. Both effects and positive/nonempty/privacy assertions remain. |
| Afterlife fixture (two realm cases) | Identity `owner.kind=afterlife_profile` violates the closed semantic-owner vocabulary (`EffectIdentityState.cs:133`). Both cases fail their first `visible` iteration; later wrong-source/realm/hidden iterations were unreached. | Identity owner becomes `player`, retaining exact `player_soul`, afterlife profile carrier path and `activeEffects`. Both realms and all five scenarios per realm remain. |

**The production afterlife ownership guard is correct and stays unchanged.** Identity agreement derives semantic owner kind from target kind for profile carriers (`ValidationService.EffectMaterialization.cs:1604–1607`). Carrier discovery supplies coordinate kind `afterlife_profile` (`EffectCarrierCatalog.cs:343`); `EffectMechanicsSnapshot.cs:137–145` places that coordinate kind into accepted-instance `OwnerKind`. Therefore the projection’s exact afterlife `OwnerKind == "afterlife_profile"` check (`WoundPlayerProjection.cs:95–100`) is appropriate. No authority vocabulary, validation, membership, storage or ownership broadening is justified.

The correction contains only eight added and three removed test lines, plus plan/tasks fronts. Assertions, production, shared fixture helper, selection and budgets are unchanged. The new assignments use existing JsonNode APIs; helper enumeration order binds `entries[1]` to the unrelated effect. This affected-only source check found no remaining correction defect.

Evidence was independently reparsed across the three sibling packets: **21 compressed/raw/original chains**, all **2,551 source pins per packet** against immutable tested Git and pre-correction current bytes, and **eight binary pins** per packet. Full actual TRX results, definitions, methods, codeBase, entries, test IDs and execution IDs were reconciled. SHA256 packet anchors:

- Fresh BOTH preparation, compiled/tested `8e4ffcf05ba170806c7bce99d9b2e023346464db`: `5639735e26f31ab6340de8940527e86c489a16fb5a5c49f119b628e5c4f48fe0`; native 0, 146.1301806 s, both builds completed, official Plan executed zero tests.
- Catalog: `f58c5bd02fbbe1dce6bf53056b18a9e0b66f0656a647966bbd91c85c88208455`; native 0, zero test execution, valid 756 categories / 11,640 methods, no unmapped/stale selectors.
- Failed actual, tested `b469b3d679acba40a1b205b0492d57e470331eb4`: `d11554ee78660f91ed1be0b6d0d572da3e9c621cb11d8aeb777efdbadeecf336`; native 1, 7.1836684 s, 25 methods / 32 unique executed cases, 28 PASS / 4 FAIL, no timeout/duplicates. Overall selection incomplete: first descriptor completed; **reader 33 cases UNRUN**. Pure cases own no filesystem roots; zero receipts does not qualify reader cleanup.

After correction, 2,550 pinned source files still match the frozen failure; the sole expected changed pin is `WoundProjectionPrivacyTests.Effects.cs`, SHA256 `9cff26521140f92beb6f1bff7926db81a09664309dca02b33d0fffbbec019bd2` (old `cd5dad4bcb0bda9c68e4a5ce882ce32696b3637c06acd9f8533e843b01ff1d8f`). All eight old binaries and the original failure packet remain unchanged. Those binaries predate the correction and cannot prove its behavior. Local HEAD is clean; parent reports actual remote `60574090` verified.

Released next gate: **fresh BOTH selected-project builds, official Plan and catalog with zero test execution**, then only `wounds-player-projection` (expected 25 methods / 32 cases) and `wounds-player-read` (8 methods / 33 cases), two descriptors / 65 total cases under unchanged three-minute category budgets. Freeze new source/output pins; retain full native/TRX/stdio and genuine reader cleanup evidence. Stop and freeze on the first material discrepancy. No aggregate or historical A7/Late2/header/G3 authorization is reused.

This release does not accept runtime GREEN, full RO1, full US5 or lawful ACTIVE. Original failures remain immutable. Reader privacy regressions and previously unreached afterlife scenarios still require execution. Following qualified RO1, tracked RO2 must connect existing console/browser status consumers; unwired foundation does not complete that work.

**FINALSTOP — only this external MD/JSON pair written; no pending reviewer writes.**

