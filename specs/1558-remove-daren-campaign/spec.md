# Feature Specification: Remove the Daren campaign

**Source issue**: [#1558](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1558) (read 2026-10-17: open, zero comments).
**Branch**: `1558-remove-daren-campaign-20261017`.
**Baseline**: `46ff637a01390a27c45a7fc36dc30e31d688e499`.
**Status**: E02-P01 catalog relation cause accepted and only new related entry repaired to existing id/when schema;1of277pins changed, selectors/product/fixtures/React guard unchanged. Successful17329frontend113 retained; fresh corrected BOTH/catalog/full+remainingPlan0 pending. Original catalog1 and all earlier raw/sentinels immutable; retain5qualified PASS, all further actual/RootChromium held.

## Source issues and scope

#1558 supersedes further development of the separate Daren manor-heist campaign. Preserve common QTE, minigames and training from #925; #955/#1080 and other historical issues are not automatically closed. Spec Kit is required because this removal crosses console/browser UI, host routes, reward storage, recovery, GM guidance and regression fixtures.

The outcome removes campaign-only scenes, prose, outcomes, route/spine data, reward generation, menu entries, browser view/API/DTO/fixtures/styles and unused campaign-specific dependencies. It preserves generic `ScenarioCoreService` (next-life/world-directive assertions), ordinary campaign QTE, all eleven current practice types and useful generic transaction, generation, uncertainty and cleanup regressions. `RivalSoulArcService` substring `одарен` is unrelated and stays.

Out of scope: new gameplay/campaign/rules, user-data deletion or bulk migration, save/archive rewriting, revocation of earned currency, repository history removal, issue/PR closure/merge, main, Windows/CI/F1/artifact workarounds, HOT/M3/provider/backend qualification. Accepted #1536 A1/A2 and #1557 A/B evidence remains historical and unchanged; it does not prove #1558.

## User scenarios and testing

### US1 — Both clients offer training without Daren (P1)

Given the console main menu or browser launcher, Daren is absent and cannot be started through retained commands, route state or stale direct API requests. Practice remains reachable. All eleven supported types can start and complete through their supported real client paths, with existing scoring/input behavior and no permanent rewards. Stale browser navigation returns the ordinary safe/default route; removed host endpoints do not dispatch a campaign or mutate data. Ordinary QTE offer/action endpoints continue to function.

Independent proof combines actual console input/owner calls, real local host requests, frontend interactions and a bounded local browser/visual check when available. A component test or HTTP response is not described as a completed interactive browser flow.

### US2 — Existing data remains safe and campaign rewards stop (P1)

Given existing `client_profile/qte_showcase_rewards.json` (valid, duplicate/old, malformed, BOM/non-UTF8 or arbitrary bytes), entering menus, practice, ordinary load and new-game flows does not read/normalize/rewrite/delete it. Already persisted `inkFeathers` and `clientRewardGrants.darenQteShowcase` are not revoked, regranted or scrubbed merely because the campaign was removed. A fresh soul receives no Daren bonus even if the old profile exists. Existing saves, histories, unknown fields and unrelated files remain intact through existing supported load/save behavior; no new round-trip compatibility promise is inferred for arbitrary schemas.

Given interrupted Daren external publication evidence, acquisition/recovery refuses before mutation and retains exact evidence, profile and ordinary sibling bytes/absence. It does not silently ignore, migrate, roll back, commit, clean or delete those journals. Generic non-Daren recovery remains supported. Error copy should explain that unsupported retired-campaign evidence was retained, without inventing gameplay or recommending destructive cleanup.

### US3 — Common regressions and guidance remain useful (P2)

Campaign narrative/reward-success tests retire with their subject. Shared tests that used Daren as a convenient fixture either use an existing real ordinary/practice owner with independent test data, or explicitly retire only a now-absent publisher row while retaining the common invariant through existing ordinary controls. No production replacement profile or external-write mechanism is invented for tests. Active docs/examples no longer instruct players/GM to launch Daren or award its rewards; ordinary QTE and no-reward practice contracts remain synchronized.

## Functional requirements

- **FR-001**: Remove all active Daren launch/menu/view/host/API/action surfaces in console and browser, including stale-route/action reachability; no aliases or hidden launch fallback.
- **FR-002**: Delete campaign-only authored route, scene/outcome prose, spine data, reward definitions and unused runtime/frontend resources. Preserve Git history and historical evidence; only tracked active repository content is in scope.
- **FR-003**: Preserve common QTE offer/action/scoring/deferred-effect machinery, minigames, eleven practice types, accessibility/keyboard behavior and training start/complete/retry/exit flows in both clients.
- **FR-004**: Remove every production campaign reward/profile caller, including read-normalization and fresh-soul grant. Existing stored profile bytes remain inert; already persisted currency/receipts remain unchanged by retirement.
- **FR-005**: Reject retired Daren external browser journals before either original/current recovery branch and before generic mutation/cleanup, covering classifier admission and schema-specific handling.
- **FR-006**: Reject shared trusted-local publication journals containing the exact retired profile member before any member is restored/deleted or cleanup runs. Validate the whole member list first, including mixed ordinary/retired-member evidence; keep unrelated ordinary recovery and path/generation checks intact.
- **FR-007**: Preserve saves, archives, receipts/history and unrelated user files; no startup scan that erases/migrates/rewards old Daren data. Genuine current Linux interrupted-journal fixtures come from identified original producers in owned test roots, not a relabeled new journal. An unavailable original-schema producer is covered by explicitly source-derived old-shape refusal controls and source-order review; native Windows remains unverified.
- **FR-008**: Migrate useful generic regression fixtures and category ownership without weakening assertions, silently deleting shared coverage, creating duplicate ownership or replaying entire obsolete campaign families.
- **FR-009**: Synchronize relevant GM/player docs, examples, source guards and test-boundary inventories. Preserve ordinary QTE semantic authority and D01's prohibition of normal GM source-code reading; no new game rule or grant contract.
- **FR-010**: Publish bounded WIP, exact remote readback/recovery and independent contract/SOURCE/EVIDENCE reviews; select actual changed dependencies and retain first RED/failure evidence. Prior #1557 B selection is not the #1558 selection.

## Explicit narrow save-safety exception

The pre-release default does not require general historical compatibility. #1558 expressly requires evaluation and safe behavior for existing campaign data; this specification narrows that exception to (a) inert `client_profile/qte_showcase_rewards.json`, (b) already persisted soul currency/grant markers and save/history bytes, and (c) interrupted original/current browser or trusted-local publication evidence targeting that exact retired member.

No old campaign is supported or resumed. No compatibility profile reader, normalizer, grant adapter or automatic migration is retained. Recognized retired evidence fails closed before recovery effects and remains available for separate manual diagnosis. The support horizon is non-destructive refusal/preservation for the #1558 retirement release; any later migration or removal of this protection requires a separately tracked, accepted decision. The exception does not relax canonical authority, accepted-turn atomicity, schema/path/generation checks, or immutable history.

## Success criteria

- **SC-001**: Real menu/host/frontend reachability checks prove Daren unavailable in both clients and stale calls cannot execute/mutate the campaign.
- **SC-002**: All eleven practice types and common ordinary QTE complete the selected real console/browser/host and frontend checks; limits of interactive/visual proof are explicit.
- **SC-003**: Old-profile and existing-grant preservation plus fresh-soul no-grant checks pass; provenance-labelled original/current/standalone/mixed interrupted-evidence tests refuse before mutation with exact bytes/absence and owned cleanup.
- **SC-004**: Active campaign resources/references are removed or correctly documented as retirement-only; useful shared regressions, category ownership and fresh selected build/discovery/actual membership are reconciled and accepted independently.
- **SC-005**: Current docs/examples/guards agree with the removed capability, no unrelated gameplay/data/history changes occur, and final checkpoint is pushed/read back/recovered with explicit platform and historical-evidence limits.

## Provenance and open engineering gate

The historical read-only [initial inventory](recovery/preflight-source-inventory-20261017.json) pins 80 baseline files, 14 existing category candidates, 15 campaign spine beats and 11 practice types. [The active-document addendum](recovery/preflight-active-doc-addendum-20261017.json) adds exact CLI API baseline byte/blob/mode/line pins without rewriting that snapshot; total81 distinct baseline files. These are source counts, not executed cases or an approved runtime selection. [plan.md](plan.md) owns the bounded dependency/migration decisions; [tasks.md](tasks.md) owns execution status. [Independent G0 acceptance](recovery/gm1558-g0-plan-independent-review-20261017.md) covers the original inventory/save-safety/verification plan; its exact support and Root readback are retained. [Targeted addition](recovery/gm1558-g0-cli-api-scope-independent-addendum-20261017.json) and [Root G1 release](recovery/gm1558-root-g1-authoring-release-c83396d2-20261017.json) are now accepted/persisted; publication/readback activates tests-only authoring. Concrete SOURCE/fresh preparation and separate Root release still precede any native capture/causal RED.


Historical pre-activation G1 source-only disposition was [accepted independently](recovery/gm1558-g1-corrected-source-independent-20261017.md), with [conditional Root capture2 eligibility](recovery/gm1558-root-corrected-capture2-only-release-230d5980-20261017.json). Findings S01/S02/S03 are closed at source only; authentic packets, derived transport and consumer RED remain unaccepted. No functional requirement or success criterion is declared complete. Exact metadata persistence/readback/recovery and separate Root activation still precede a corrected capture; all other actuals and G2 remain held.


Historical [corrected capture2 raw outcome](recovery/g1-corrected-first-capture-20261017.json) preserves two original-producer PASS results and exact packets, with independent frozen-data/derived-transport EVIDENCE pending. No consumer retirement RED or functional/success-criterion completion is claimed; production and all noncapture actuals remain held.


Historical source-installation [capture and actual-byte transport EVIDENCE/SOURCE acceptance](recovery/gm1558-corrected-capture-evidence-independent-20261017.md) permits exact runtime JSON installation and selector partition only. [Current binding](g1-data-selector-source-packet-20261017.json) distinguishes57 unchanged compiled source pins/seven0b DLLs from current data/catalog/selectors. Fresh zero-execution discovery/finitePlanOnly and independent binding review still precede consumer runtime and meaningful retirement RED; no requirement/success criterion or production removal is complete.


Historical [NoBuild data/selector preparation](recovery/g1-data-selector-first-preparation-20261017.json) observes exact finite5owners/5descriptors/10methods/28cases with0test execution and unchanged compiled0b/sevenDLLs. Targeted independent binding review and explicit Root runtime activation remain pending; no source-derived consumer behavior, retirement RED or functional/success-criterion completion is claimed.


Historical [accepted data/selector/PREP binding](recovery/gm1558-g1-data-selector-independent-20261017.md) remains source/preparation qualification only. [Root conditional per-owner decision](recovery/gm1558-root-finite-red-conditional-release-7a3bf9a0-20261017.json) requires exact metadata publication/readback/recovery and separately names ordinary2 first, then gates each other owner on preserved FIRST raw. No consumer behavior/retirement RED or requirement/success-criterion completion is declared here; product removal remains held.


Historical [FIRST finite-consumer partialSTOP](recovery/g1-first-consumer-stop-20261017.json) preserves11executed/2PASS/9FAIL with5locallycausal retirement failures and4unqualified save fixtureBOMparse failures; HTTP17 remainsUNRUN. Independent causal classification/remaining fixture correction is pending. No save-preservation proof, full28 execution or requirement/success-criterion completion is claimed; production removal and additional runtime remain held.


Historical [partial causal EVIDENCE acceptance](recovery/gm1558-first-consumer-stop-evidence-independent-20261017.md) qualifies ordinary2PASS and5storage/menuRED while retaining4save fixture failures and HTTP17unrun. [Narrow S04 test correction](g1-s04-correction-source-packet-20261017.json) awaits fresh affected preparation/independentSOURCE before remaining21 actuals. Production/removal and all functional/success-criterion completion claims remain held.


Historical [S04 fresh zero-execution preparation](recovery/g1-s04-first-preparation-20261017.json) observes remaining2owners/5methods/21cases withnewcompiled72a/sevenDLL binding. Affected independentSOURCE and exactRootactivation remain pending; corrected save4 and firstHTTP17 are unrun. Accepted ordinary/storage/menu evidence keeps its originalprovenance; production and functional/success completion stay held.


Historical pre-activation [S04 SOURCE/FIXTURE/SELECTION/PREP acceptance](recovery/gm1558-g1-s04-source-independent-20261017.md) leaves actual validation pending. [Root conditional remaining-owner protocol](recovery/gm1558-root-s04-remaining-red-conditional-release-89023323-20261017.json) gates corrected save4 first, then HTTP17, after exact persistence/readback/recovery and separate activation. No new runtime or requirement/success completion is declared; old accepted/unqualified evidence remains distinct and production is held.


Historical pre-decision [FIRST remaining consumer evidence](recovery/g1-first-s04-consumers-20261017.json) freezes corrected save4 and previously unrun HTTP17 atactual5e6/compiled72a:21complete/12PASS/9locallycausal finalFAIL,16rawchains/21strict physically absent roots. Retained7oldqualified cases and4oldsave fixtureFAIL keep originaldf3/0b provenance. Finite28unique/10methods=14positivePASS+14causalRED is a local aggregate pending independent EVIDENCE; no all-command GREEN, completeG1, FR/SC/task completion or product removal is claimed.


Current [independent accepted G1 causalbaseline](recovery/gm1558-first-s04-consumers-evidence-independent-20261017.md) is28distinct/10methods=14positivePASS+14causalRED, with old4savefixtureFAIL and capture/retained7provenance preserved. [Root conditional boundedG2decision](recovery/gm1558-root-g1-evidence-accepted-g2-release-d36ee9a0-20261017.json) accepts baseline sufficiency and makes implementation next only after thisdecision's publication/readback/recovery and namedRootactivation. No removal/GREEN/FR/SC completion or additionalruntime is claimed; user data/product/raw/selectors remain untouched here.
