# Quickstart: Unified Resource Authority

## 1. Purpose

This guide is the executable validation handoff for [#1543](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1543) and the resumed resource-dependent Effect Task 9 in [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535). It proves the final single-authority cutover; it is not a migration guide.

## 2. Preconditions

- Work only in the isolated `E:/Games/worktrees/boe-1535-effect-materialization` worktree.
- Read `AGENTS.md`, `.specify/memory/constitution.md`, this feature's `spec.md`, `plan.md`, `research.md`, `data-model.md`, every `contracts/*.md`, and the approved Superpowers design.
- Confirm GitHub issues #1543 and #1535 remain open and #1535 records the #1543 blocker.
- Confirm `git status --short --branch` contains only the active feature's expected work.
- Use PowerShell 7 and `scripts/test-csharp.ps1`; never run an unbounded full-solution `dotnet test` and never enable or invoke GitHub Actions.
- Read `docs/testing.md` before choosing a lane.

### Phase-1 baseline evidence (2026-08-15)

- Issues: #1543 `OPEN`; #1535 `OPEN` and explicitly blocked by #1543 for Effect Task 9.
- Workspace: branch `1535-effect-materialization`; root `E:/Games/worktrees/boe-1535-effect-materialization`; clean before implementation.
- Command: `pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast`.
- Result: exit `0`; timeout `false`; `3479/3479` executed and passed; failures `0`; duplicate test IDs `0`; owned-tree cleanup `complete`; wall `00:04:02.3268448`.
- Summary: `TestResults/test-lanes/20260815-152211-507-33852-278bd0d07af7444aaa060a26783f082c-fast/summary.json`.
- Harness RED 1: `TestResults/test-lanes/20260815-153733-515-18216-f163ecfa317c4cf2a846976d0654ff8f-focused/summary.json`; expected build failure because `ResourceMaterializationTestContext` did not exist.
- Harness RED 2: `TestResults/test-lanes/20260815-153846-219-17504-c9674d2b64a04d999064e487ad9f804e-focused/summary.json`; expected build failure after the strict-root assertion was added and before the harness existed.
- Harness GREEN (final Task-1 snapshot): `TestResults/test-lanes/20260815-154703-722-41500-00f376b0b55348318972aaa262945bce-focused/summary.json`; exit `0`; timeout `false`; `6/6` executed and passed; failures `0`; duplicate test IDs `0`; owned-tree cleanup `complete`; wall `00:00:15.8617710`.
- GitHub Actions were neither enabled nor invoked.

### Task-2 definition/formula evidence (2026-08-15)

- Strict-root RED: `TestResults/test-lanes/20260815-155710-015-49484-de5d819a9e684f22bd6278e5921c66dc-focused/summary.json`; expected build failure before `ResourceMaterializationContract` existed.
- Definition RED: `TestResults/test-lanes/20260815-155803-263-56844-1dcddf84cbe74cf8a18c258dbaf7c00a-focused/summary.json`; expected build failure before `ResourceDefinitionCatalog` existed.
- Formula RED: `TestResults/test-lanes/20260815-155817-070-47076-1e519cd3071a48fb9819f89c9c6b40e5-focused/summary.json`; expected build failure before the closed formula registry existed.
- The first combined implementation check passed `80/80`; the follow-up warning-free run is `TestResults/test-lanes/20260815-161005-214-42364-6359775727804188888bf2dc485334ce-focused/summary.json` (`80/80`, failures `0`, duplicates `0`, timeout `false`, cleanup `complete`).
- Contract review rejected a fixed-100 health/energy/poise shortcut because existing player equations and NPC/combat/group/vehicle maxima differ. The owner-typed formula RED is `TestResults/test-lanes/20260815-162015-617-42904-6c99e1af2afd45199d82fb7aa57fad8d-focused/summary.json`; it fails at build on the intentionally missing typed records/keys/resolver.
- Exact catalog-limit and materialization-identity collision RED is `TestResults/test-lanes/20260815-162830-623-7888-9151c4cd8e604342be793c86f36ffed8-focused/summary.json` (`23/26` passed; the three new guards failed before implementation). Its focused GREEN is `TestResults/test-lanes/20260815-162937-229-54592-6a491fc4e40b468eb3da8ad2fc95f3f2-focused/summary.json` (`26/26`).
- Unsafe control/bidirectional-format identifier RED is `TestResults/test-lanes/20260815-163115-462-55040-4d064e2b9d894edb8b777de0f1212262-focused/summary.json` (`7/9` passed before hardening); focused GREEN is `TestResults/test-lanes/20260815-163206-319-45804-d59158a750b64eca987e58f526c4b1dc-focused/summary.json` (`9/9`).
- Final combined Task-2 GREEN: `TestResults/test-lanes/20260815-163421-269-37484-bd8cfe7ab53644109046e35425c1a980-focused/summary.json`; exit `0`; timeout `false`; `98/98` executed and passed; failures `0`; duplicate test IDs `0`; owned-tree cleanup `complete`; wall `00:00:15.4103477`; build warnings/errors `0/0`.
- GitHub Actions remain disabled and are not used for any evidence.

### Task-3 ledger/history evidence (2026-08-15)

- State RED: `TestResults/test-lanes/20260815-164524-592-30380-887b6462604249e6877a133e552f488d-focused/summary.json`; expected build failure before the state contract existed. State GREEN: `TestResults/test-lanes/20260815-165010-088-29464-e28451046d0c4c9fb5da903d3c38aedc-focused/summary.json`; `31/31`.
- History RED: `TestResults/test-lanes/20260815-165505-677-46248-1bddeb4a69284d6595583047850042ad-focused/summary.json`; expected build failure before immutable history/replay authority existed.
- State/history agreement RED→GREEN: `TestResults/test-lanes/20260815-170434-939-51912-3dd2d38725cc4ed88920171418e47098-focused/summary.json` to `TestResults/test-lanes/20260815-170525-218-20688-9c0af1de5981442f9d72fb11831256bd-focused/summary.json` (`33/33`).
- Active-only initialization RED: `TestResults/test-lanes/20260815-170804-314-33660-6c52afca753f4a198dce592ccfdf1d50-focused/summary.json` (`34/35`) before the lifecycle gate.
- Final combined Task-3 GREEN: `TestResults/test-lanes/20260815-170843-851-19000-dd077cb0ca1a44b489776d06ddf0dec2-focused/summary.json`; exit `0`; timeout `false`; `68/68` passed; failures `0`; duplicate test IDs `0`; cleanup `complete`; build warnings/errors `0/0`.
- Expanded definition/formula/state/history compatibility control: `TestResults/test-lanes/20260815-171335-724-6536-5b2f1b18a33b43c6b82e3d09e010bac9-focused/summary.json`; `166/166`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Review RED/GREEN proves decimal-scale canonicalization (`171547...` -> `171637...`), terminal-history coordinate confusable rejection (`171829...` -> `172003...`), and requested/applied quantum enforcement through clamps (`172159...` -> `172254...`). Full paths are recorded in the Superpowers execution plan.
- Ordering review proves same-turn event ordinals cannot reorder the closed phase sequence and lexical origin order cannot invert a DAG edge. The history now stores a unique per-turn client-owned `executionSequence`; RED evidence is `TestResults/test-lanes/20260815-173333-855-53400-db8034bc80244aaebe51857d87e3e85c-focused/summary.json` and `TestResults/test-lanes/20260815-173840-685-41892-ebebadeed2184be88adea3307de936fe-focused/summary.json`; focused GREEN is `TestResults/test-lanes/20260815-173951-093-35536-74a90c8830ef4936ad385e02d59f63c9-focused/summary.json` (`2/2`).
- Capacity-policy review proves reconfigure cannot launder arbitrary current state: every transition carries nullable `capacityDisposition`, with exact initialize/preserve/clamp/ratio rules. RED is `TestResults/test-lanes/20260815-174600-773-50036-bece17b6810b48349247627d0f39c377-focused/summary.json` (`0/2`); focused GREEN is `TestResults/test-lanes/20260815-174900-685-44276-e918f13d792e4db6b0491c357241a9f6-focused/summary.json` (`3/3`).
- Pre-independent-review Task-2+3 control after the ordering/capacity review: `TestResults/test-lanes/20260815-175724-287-21772-33386f7f63d746859fe2e8d8006a6fac-focused/summary.json`; `175/175`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Initialization-policy review proves that `initialize_from_definition` cannot attest an arbitrary current value for sealed static policies: RED `TestResults/test-lanes/20260815-180933-371-55304-15a892430a724fa2b430cf99706f9998-focused/summary.json` (`0/3`) -> GREEN `TestResults/test-lanes/20260815-181058-601-30332-bdaac3df18474547a927e90992ced419-focused/summary.json` (`3/3`). Registered-formula typed input/fingerprint recomputation remains assigned to the later composed reducer.
- Exact-decimal review proves no checked-decimal scale reduction can authorize a false ratio, quantum alignment, or no-op ordinary transition: behavioral REDs are `TestResults/test-lanes/20260815-181457-525-15004-1f406a6eb18c49b9acb140bdb1d1122e-focused/summary.json` (`0/2`) and `TestResults/test-lanes/20260815-181551-821-35700-0b70881f74b047bc9b37e8bbb0237e67-focused/summary.json` (`0/1`); shared-helper compile RED is `TestResults/test-lanes/20260815-181637-330-56232-82fd125097ef4453b7d0d252ce1ed826-focused/summary.json`; focused GREEN is `TestResults/test-lanes/20260815-181930-810-54456-81d21df58bbc4d9f8d294be6d018530f-focused/summary.json` (`4/4`).
- Pre-final clamp review Task-2+3 control: `TestResults/test-lanes/20260815-182614-352-33816-80c054cdd55a4d97be9fafc75d1fbd66-focused/summary.json`; `182/182`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Requested-candidate clamp RED/GREEN proves that neither max-scale loss nor `decimal.MaxValue` overflow can be hidden by `appliedAmount: 0` at a reached bound: `TestResults/test-lanes/20260815-183219-021-22400-d10603c5acce4c04a04867ab7e6bc70f-focused/summary.json` (`0/2`) -> `TestResults/test-lanes/20260815-183307-136-2576-957a4d91c9fd4eff887e5285fd373dd8-focused/summary.json` (`2/2`).
- Exact legitimate min/max clamp control is `TestResults/test-lanes/20260815-183845-646-34548-150b3ec89aa34605afc5b119883addec-focused/summary.json` (`2/2`). Fresh final reviewed Task-2+3 control is `TestResults/test-lanes/20260815-183915-753-7996-94e3b96e46d048bba4712cbb0105b0c7-focused/summary.json`; `186/186`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Final post-review definition/formula/state/history control: `TestResults/test-lanes/20260815-172438-891-14496-49e48093d3384a76bc2a7ccb5798c556-focused/summary.json`; `170/170`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- The immutable history stores complete before/after state snapshots, not only scalar deltas. Reconfigure, suspend/resume, retire, exact replay, long untruncated history, and the 20,000 live-entry boundary are covered.
- Scalability review keeps the immutable canonical history as the Task-3 authority and reserves a separate T039/T041/T046 `ResourceHistoryWorkingSet`: one baseline index seed, incremental same-turn replay/continuity admission, and one final full validation/sort/fingerprint. The future reducer is explicitly forbidden from calling the whole-history `Append` path per mutation; T111 measures this contour.
- No GM-authored surface changes in Task 3; active prompts/examples/manifests remain unchanged by design. GitHub Actions remain disabled and unused.

### Task-4 owner/common-identity evidence (2026-08-15)

- Owner/common-identity RED: `TestResults/test-lanes/20260815-184853-327-30232-202d06451bb0438a8abae73824cfa140-focused/summary.json`; expected build failure before `ResourceOwnerAuthority` and `CombatantIdentityState` existed.
- Compile-fix evidence: `TestResults/test-lanes/20260815-185731-698-33500-293cc634634548168f9878770267ee74-focused/summary.json` and `TestResults/test-lanes/20260815-185913-744-48236-fec50d0a5c7342c7a3c3e5536d8cbda5-focused/summary.json`; both failed only on the newly generalized tuple/API references before correction.
- Narrow GREEN: `TestResults/test-lanes/20260815-190130-170-28124-419ceefb9f9a4618b724afb2d5167e75-focused/summary.json`; `43/43`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Final common-owner/effect compatibility GREEN: `TestResults/test-lanes/20260815-190315-160-19392-c2684c5e553e48d58f1c1ce74b2f14ca-focused/summary.json`; `89/89`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- `EffectCombatantIdentityState` has zero remaining production/test references. The common identity allocates and consumes every accepted combatant/member ref, while the composed owner authority covers all nine owner families without name/index fallback.
- This slice is client-owned authority infrastructure only; it adds no GM-authored command, state surface, response field, prompt, example, or afterlife contract. GitHub Actions remain disabled and unused.

### Task-5 accepted-mechanics plan/cache evidence (2026-08-15)

- Plan/cache RED: `TestResults/test-lanes/20260815-190804-468-51056-9c0361055921412ea415de21239db914-focused/summary.json`; expected build failure before `AcceptedMechanicsInput`, `AcceptedMechanicsPlan`, `CanonicalBeforeImage`, authority fingerprints, planning result, and cache existed.
- First cache GREEN: `TestResults/test-lanes/20260815-191328-090-11864-9bfd9073faf645d8a099326172b7c286-focused/summary.json`; `29/29`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`.
- Review RED/GREEN proves exception-safe handoff invalidation and defensive planning-result issues: `TestResults/test-lanes/20260815-191604-928-41540-d7ef7bec7c5c48d487b8c38ee198aba3-focused/summary.json` (`29/31`) to `TestResults/test-lanes/20260815-191656-102-40652-31dd13fcc37842febe6cc5500fc21ab0-focused/summary.json` (`31/31`).
- Changed-live-binding consume RED/GREEN: `TestResults/test-lanes/20260815-191919-477-44740-c30044a486f74e978840d3a933fa6748-focused/summary.json` (`1/2`) to `TestResults/test-lanes/20260815-191957-142-52232-dfce620ec8454547949845021d62e5f8-focused/summary.json` (`2/2`). A mismatched preflight now invalidates rather than preserving a retryable stale handoff.
- Empty planner failure and rooted-path REDs are `TestResults/test-lanes/20260815-192154-135-30148-2ec6aa4f299c40229757378579c647bc-focused/summary.json` (`0/1`) and `TestResults/test-lanes/20260815-192250-529-21520-d1f1564154a94b4eb0e59bdf00724828-focused/summary.json` (`3/4`). Final cache GREEN is `TestResults/test-lanes/20260815-192445-744-29996-d23f432c2db14621a5058e27d3b829fa-focused/summary.json`; `39/39`.
- Final Phase-2 compatibility control: `TestResults/test-lanes/20260815-192635-421-14676-5d478306e0624027979271e47b2f44b0-focused/summary.json`; `314/314`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- The common plan defensively owns every resource/effect/pending/owner after-image, exact present-vs-missing before-image, touched/consumed path, event/projection input, and all twelve authority fingerprints. The cache canonicalizes object/dictionary order, invokes the planner once per exact input, clears on every failed validation/exception/mismatch, and consumes an exact live binding once.
- The planned follow-on refactor remains explicit: `EffectAcceptedTurnPlan` is retained as the common plan's immutable `EffectPlan` subplan, while T032–T035/T048 wire the common validation/publication contour and retire the independent effect handoff only after all callers move. T078–T080 still own periodic resource components, trigger feedback, effect lifetime, and terminal cleanup. No migration or compatibility fallback was added.
- This foundation is client-owned and does not yet add a GM-authored resource response field or runtime file mapping; therefore prompts/examples/manifests remain unchanged. Those surfaces are synchronized in their tracked US1/US6 tasks. GitHub Actions remain disabled and unused.

### Task-5b resource command-envelope evidence (2026-08-15)

- Response/mapping RED: `TestResults/test-lanes/20260815-193216-881-51464-548f0cb539314e4688e66c6cb78241fe-focused/summary.json`; expected build failure before the three `GameResponse` properties existed.
- The first implementation run exposed an invalid transient-root `_lastUpdated` sibling: `TestResults/test-lanes/20260815-193318-542-54200-6ac0702b6b134352b1079b628232aed8-focused/summary.json` (`47/48`). Final response/mapping/distributor GREEN is `TestResults/test-lanes/20260815-193434-078-47964-d815a11bb33e40bd8954e9ede90f530d-focused/summary.json` (`48/48`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`).
- Strict composer RED: `TestResults/test-lanes/20260815-193917-474-54192-2cf6ab22c6fb4c0ab63dc7a0a40e9979-focused/summary.json`; expected build failure before the composer/types existed. GREEN is `TestResults/test-lanes/20260815-194213-762-18232-41027d70a00c41d58aaa19b2ab5eacab-focused/summary.json` (`28/28`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`).
- Combined response/mapping/composer control: `TestResults/test-lanes/20260815-194449-767-50984-a4d6fb0b8024470dbcb64f7d8cde8c12-focused/summary.json`; `76/76`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- The transient root is closed and metadata-free; missing is distinct from malformed; duplicate properties are rejected recursively; definition/capacity/ordinary commands use exact bounded selectors and exact positive decimal amounts. Global command ordinals are retained for T047 event binding.
- Legacy player/item response properties and mappings deliberately remain until their atomic T057/T065 consumer cutovers. This is not compatibility support: no common/legacy dual write is introduced, and the executable absence guards remain tracked in T056/T063.
- The response fields are not considered a finished GM capability until raw validation, the common reducer/plan, atomic publication, and synchronized Mortal/afterlife GM examples are complete in this feature. No merge or public handoff occurs at this intermediate checkpoint; GitHub Actions remain disabled and unused.

### Task-6 reducer/planner/graph evidence (2026-08-15)

- Reducer/history working-set RED began at `TestResults/test-lanes/20260815-195121-706-18064-515ab278cedb4a67be33c0a1af04e83a-focused/summary.json` with the intentionally missing reducer types; `TestResults/test-lanes/20260815-195702-892-50816-55930d2980154807a53536b3050d69a7-focused/summary.json` then exposed four exact harness/policy failures before GREEN `TestResults/test-lanes/20260815-200235-648-5216-304691210ef04b43adbdb38a5ae5dd83-focused/summary.json` (`17/17`).
- Immutable working-ledger review RED/GREEN is `TestResults/test-lanes/20260815-200503-759-29256-823b57a1d5e848e1b6380300a58e8900-focused/summary.json` (`0/2`) to `TestResults/test-lanes/20260815-200552-772-49464-1cca92b1090c4b29ae9b6368573ca75e-focused/summary.json` (`2/2`). Formula/capacity binding RED/GREEN is `TestResults/test-lanes/20260815-200947-341-57320-faf24c4e313a44fa9718f348e7e15980-focused/summary.json` to `TestResults/test-lanes/20260815-201501-133-38684-0b18684683ea48bab904ef633a4f2b62-focused/summary.json` (`1/1`).
- Expanded exact definition/formula/state/history/reducer control is `TestResults/test-lanes/20260815-201746-647-40068-6714e011432145f3b17d84607cbcbb00-focused/summary.json`; `166/166`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`.
- Closed source-route RED is `TestResults/test-lanes/20260815-202055-429-49060-91a886b897aa45b5b5fe35a236bb2c27-focused/summary.json`; source catalog GREEN is `TestResults/test-lanes/20260815-202355-151-37836-f5b4883ce40f43a186c0cb95627f84f7-focused/summary.json` (`16/16`). GM input has no phase, priority, policy, fingerprint, path, or after-image override.
- Trigger-graph RED is `TestResults/test-lanes/20260815-202751-162-54424-588e1bfff21b4820a9d311bf715ecf8c-focused/summary.json`; planner RED is `TestResults/test-lanes/20260815-203714-838-49668-8e082a38cac84f78a30795ef5215e0f0-focused/summary.json`. They fail only because the new graph/planner types were intentionally absent.
- Accepted-event ordinal RED is `TestResults/test-lanes/20260815-204419-392-42456-0738fb58fbf34884bc6e6350af21e305-focused/summary.json`; exact `turn_<turn>:resource:<global command ordinal>` binding now rejects swapped, reused, stale, missing, or future authority.
- Final Task-6 reducer/composer/planner/graph GREEN is `TestResults/test-lanes/20260815-204834-191-30756-6b0d2f72f6ca4288b66368c04086e997-focused/summary.json`; exit `0`; timeout `false`; `79/79` executed and passed; failures `0`; duplicate IDs `0`; cleanup `complete`; build warnings/errors `0/0`.
- Meaningful post-slice Fast control is `TestResults/test-lanes/20260815-205224-856-50300-b361247220434388919cdf520ca43561-fast/summary.json`; exit `0`; timeout `false`; `3806/3806` executed and passed; failures `0`; duplicate IDs `0`; cleanup `complete`; wall `00:03:17.9751896`.
- Purity search over `ResourceMutationReducer.cs` and `AcceptedMechanicsPlanner.cs` finds no `FileSystemManager`, read, write, delete, move, or direct file API. One immutable working ledger and one indexed history working set are plan-local; success freezes history exactly once, while any invalid sibling returns no state/history/events/transitions.
- This slice establishes client-owned reduction and event authority but is not yet a publishable GM capability: T048–T049 still own raw-validation/cache/publication/post-seal wiring, and later tracked tasks own synchronized Mortal/afterlife prompts/examples/manifests. No intermediate runtime dual authority, migration, or compatibility fallback was added. GitHub Actions remain disabled and unused.

### Task-7 atomic publication/bootstrap/persistence evidence (2026-08-15)

Every bare run ID in this section is the exact directory name under `TestResults/test-lanes/`; its evidence file is `<run-id>/summary.json`.

- Common raw/publication entrypoint RED `TestResults/test-lanes/20260815-210743-024-14572-2e64904e0ea44bb3853f8f85cb7b7ba0-focused/summary.json` failed at build before the common validator/normalizer existed; the first end-to-end GREEN is `TestResults/test-lanes/20260815-211407-431-34460-73c6aa8467c54fc587b5a36ded225195-focused/summary.json` (`8/8`). Effect-only composition RED/GREEN is `20260815-212056-190-49048-93d8c54422dd48afa529ed36b4285c82-focused` (`8/11`) to `20260815-212352-949-54756-dd45e97e5408486c9f5cc203c7e94868-focused` (`11/11`).
- Bootstrap RED/GREEN proves both pristine root creation and Mortal initialization: `20260815-212630-440-29808-5988491934e340bd8239b547d7e06f5a-focused` to `20260815-212748-525-3900-abba547bc9dc4d1e82c6fe6de644585e-focused`, and `20260815-213544-664-39432-09cff4aeba554c638e31c483e070537e-focused` to `20260815-213628-361-54264-e7517cd67d994644a77840b97b34b0a2-focused`. Exact UTF-8/BOM publication RED/GREEN is `20260815-213221-371-57040-808fd125e8554b13bab46a0d97ff164a-focused` to `20260815-213317-302-42628-465561c6f95845309a22488a87115f82-focused`.
- Strict incompatible-save rejection and archive preservation are proven by `20260815-214558-064-52716-a4f854948c694f5fb2191bf047a4477e-focused` to `20260815-214725-968-49272-066abeab6b23481b8373c6f5f9f650b9-focused`; full save/load GREEN is `TestResults/test-lanes/20260815-225610-770-28464-e57763b494624841a7bfddc0c1282b4e-focused/summary.json` (`69/69`). Pending-turn snapshot inclusion is GREEN at `20260815-215418-696-17656-0d315e11ecce4cdf9ede0fd4f886f6ee-focused` (`1/1`).
- Lease-bound post-write equality and rollback injection are proven by `20260815-215607-616-38436-7056cbd40f1540df8d290eebbb9e577f-focused` (`2/4`) to `20260815-215749-493-48552-cb16c685bc6a4c629289c0a51dd0020e-focused` (`4/4`) and `20260815-220027-299-12320-d25972ccb3dd430b855ff27a1b58555c-focused` (`1/1`). Effect-only publication was re-bound to the common input fingerprint by RED `20260815-221157-346-40140-f470ebc980e2451f827f6f419d401b2a-focused` and GREEN `20260815-221309-388-40096-ae70e84dfe5d4148a84582cef8754316-focused`.
- Review hardening closes five adjacent authority holes with fresh RED/GREEN evidence: canonical owner agreement (`20260815-223207-459-53544-3aeaba0c42ad4f60992a2bc5f1d18b29-focused` to `20260815-223654-516-28368-df0feb9b041543b1bd5cbcc6f775ed59-focused`), save/load owner agreement (`20260815-223938-339-44524-5b7db6d74c364b32971f35de12051589-focused` to `20260815-224112-130-51024-75d6b928cc3b4bf4804e3d25a40e5c0f-focused`), preservation of prior setting definitions/state/history during Mortal incarnation (`20260815-224438-370-17532-f3de509280f049ab8168a3b0b0488dd3-focused` to `20260815-224536-351-32492-f2f5c13430ec465d9276e49719ea10c5-focused`), same-turn capability restriction to the definition's exact `allowedOwnerKinds` (`20260815-224857-712-22156-5d62d91aee1e43098ded8072d06ab058-focused` to `20260815-224945-646-16828-f7e80f71d1a245a69e3e47e4e627cd3b-focused`), and common-handoff invalidation after an effect preflight failure (`20260815-232059-304-16200-290604a94f4b4084aeea7d53f6775bdc-focused` to `20260815-232147-107-52924-642d209c892e401ba42fc97e1270f093-focused`).
- Final focused controls are `TestResults/test-lanes/20260815-232245-586-39276-0e4d0d6a9ea24d4c9f552e35c7a7d947-focused/summary.json` (`29/29` resource integration/publication), `20260815-225417-704-42708-a8c79bef313c4ae18ced25392647ffb0-focused` (`27/27` effect normalizer compatibility), `20260815-225507-455-49416-4e77bc1a6b5e46838a88fb52a551488c-focused` (`254/254` resource/common-plan unit contracts), `20260815-225547-923-53600-0c02e0a265dd4ff1a8965045dc0aabfb-focused` (`118/118` bootstrap/snapshot source guards), and the save/load control above. All have failures `0`, duplicate IDs `0`, timeout `false`, and cleanup `complete`.
- The first Fast exposed only four pre-resource test fixtures, never a production fallback. Their isolated RED/GREEN controls are `20260815-230236-883-47956-718b65c7eb894b14bd6b7bbc59cfe293-focused` (`0/3`) to `20260815-230410-818-2652-67f86c17816345c0a0b5a8f5921b09d1-focused` (`3/3`), and `20260815-230851-791-54704-72bc84b1e062415486ad5beab63a403c-focused` (`0/1`) to `20260815-230921-009-27312-12e8493db07f416988ddade74ef82d95-focused` (`1/1`). Final meaningful Fast after the preflight hardening is `TestResults/test-lanes/20260815-232323-370-17256-698e67268d6b4ba8baf1663a9596743e-fast/summary.json`: exit `0`; timeout `false`; `3807/3807`; failures `0`; duplicate IDs `0`; cleanup `complete`; wall `00:02:47.0066298`.
- Review confirms one common validated cache, one common plan instance, one canonical write lease, exact present/missing before-images, deterministic writes/deletes, post-seal resource/effect validation, and outer exact rollback. `ResourceMutationReducer.cs` and `AcceptedMechanicsPlanner.cs` contain no filesystem access. The independent effect normalizer is no longer a production entrypoint.
- This slice intentionally activates only client-owned roots plus the already tracked bounded command envelope. It does not claim the incomplete player/NPC/combat/vehicle/item/afterlife cutovers as public GM capability: T054+ and T100–T108 retain those owner and prompt/example/template changes. No Mortal or afterlife prompt/example is updated prematurely, no migration/backcompat/dual authority is added, and GitHub Actions remain disabled and unused.

## 3. Canonical example state

The active new-game example must contain:

```text
game_state/resources/resource_definitions.json
game_state/resources/resource_state.json
game_state/resources/resource_history.json
```

It must not contain persisted mechanical values in the removed player, NPC, vehicle, combat, item, or afterlife fields. `resource_commands.json` is absent outside an accepted staged turn.

Minimum built-in examples:

- Mortal `player_current`: health, energy, poise;
- one named NPC health owner;
- one anonymous combatant and one stable group member;
- one vehicle health owner bound to permanent `vehicleId`;
- one item with durability and one with charges/ammunition;
- one afterlife spiritual-action-points owner;
- one Guardian/Shining per-return charge owner;
- one setting-defined resource materialization example;
- one immutable `initialize` history row for every bootstrap state coordinate, exactly matching its creation/latest chronology.

## 4. Contract and reducer RED/GREEN

Run after the contract, definition/state/history, owner authority, and reducer tasks:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceMaterializationContractTests|FullyQualifiedName~ResourceDefinitionCatalogTests|FullyQualifiedName~ResourceStateContractTests|FullyQualifiedName~ResourceHistoryStateTests|FullyQualifiedName~ResourceOwnerAuthorityTests|FullyQualifiedName~ResourceMutationReducerTests"
```

Expected:

- all tests pass;
- duplicate/confusable/unknown/wrong-root/empty/null/legacy cases fail closed;
- integer, decimal, quantum, min/max, clamp/reject, overflow, exact ratio, order, replay, and conflicting replay cases are covered;
- no test process survives lane cleanup.

## 5. Accepted mechanics plan RED/GREEN

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~AcceptedMechanicsPlannerTests|FullyQualifiedName~AcceptedMechanicsPlanCacheTests|FullyQualifiedName~ResourceTriggerGraphTests|FullyQualifiedName~ResourcePendingResolutionTests"
```

Expected:

- one plan instance and one random identity set are reused from validation through publication;
- all four phases and stable within-phase ordering pass;
- cycles, missing dependencies, 1,025th node, depth 33, stale fingerprints, late literal-null/malformed changes, and cache reuse after consumption fail with zero writes;
- deterministic mutations never create pending work;
- bounded no-state-change/resource-delta receipts are exact and replay-safe.

## 6. Owner lifecycle RED/GREEN

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceOwnerMaterializationTests|FullyQualifiedName~ResourceCombatOwnerTests|FullyQualifiedName~ResourceVehicleOwnerTests|FullyQualifiedName~ResourceItemOwnerTests|FullyQualifiedName~ResourceAfterlifeOwnerTests"
```

Expected:

- player, named NPC, anonymous combatant, group member, vehicle, item, persistent afterlife actor, conflict side, and afterlife scope resolve exact owners;
- named NPC combat entry uses the same owner;
- every accepted `combatantRef`/`memberRef` is consumed even without a resource/effect command;
- item movement preserves the coordinate;
- vehicle activation/parking/movement preserves the coordinate and destruction retires it;
- member/item/conflict terminal transitions leave history and no orphan live state;
- case/confusable/historical/cross-realm/unknown bindings fail atomically.

## 7. Mortal ordinary-operation cutover

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalResourceCutoverTests|FullyQualifiedName~ResourceCombatIntegrationTests|FullyQualifiedName~ResourceVehicleIntegrationTests|FullyQualifiedName~ResourceItemIntegrationTests|FullyQualifiedName~MortalBootstrapValidationTests"
```

Expected:

- ordinary damage, healing, energy spend/gain, poise damage/restore, vehicle damage/restore, and item use/repair/fire/reload use the reducer;
- legacy player delta, NPC/vehicle/combat health, group health array, durability, and item resource commands are rejected;
- no persisted legacy mirror remains after bootstrap or a turn;
- one invalid sibling produces zero resource/owner/effect/output writes.

## 8. Afterlife cutover

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests|FullyQualifiedName~GuardianGachaResourceTests|FullyQualifiedName~ShiningGachaResourceTests|FullyQualifiedName~ShiningBlessingResourceTests"
```

Expected:

- action-point cost/recovery, per-return gacha consumption/reset, and numeric blessing reroll use common state/history;
- registered capacity formulas reproduce existing spirit-focus/reputation/return-cycle rules;
- currencies, progression, relationships, faction ledgers, and spiritual axes remain unchanged in their owning systems;
- conflict/return-cycle closure retires only scoped resources.

## 9. Effect Task 9 integration

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectResourceTriggerPlannerTests|FullyQualifiedName~EffectLifecycleSchedulerTests|FullyQualifiedName~ResourceTriggerGraphTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectResourceMaterializationTests|FullyQualifiedName~CanonicalStateNormalizerEffectTests|FullyQualifiedName~ResourcePendingResolutionIntegrationTests"
```

Expected:

- ordinary and effect-equivalent mutations produce the same state/history semantics;
- periodic damage/restore, depletion/fill triggers, nested downstream mutations, stack/lifetime advancement, terminal cleanup, and replay use one plan;
- story-facing pending requests change no mechanics until a valid receipt/full-turn resubmission;
- no effect-only resource field adapter exists.

## 10. Projection, privacy, and parity

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceProjectionServiceTests|FullyQualifiedName~ResourcePlayerPrivacyTests|FullyQualifiedName~BrowserApiContractTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ResourceConsoleBrowserParityTests|FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests"
```

Expected:

- console/browser expose equivalent visible facts/actions;
- hidden and GM-only entries produce no player row/count/delta/action/implication;
- malformed/missing safe projection never falls back to legacy raw state;
- recursive DTO scans find no owner/transition/event/receipt/fingerprint/path/validation/repair/agent data.

Manual spot-check after automated parity is green:

1. Start one fresh technical game from the active template.
2. Compare status bar, `/status`, browser game screen, one NPC detail, one combatant detail, one item detail, and one afterlife conflict/charge view.
3. Confirm all copy is in-world Russian and no IDs, paths, JSON/API/DTO language, repair guidance, or hidden values appear.

## 11. Atomic publication and rollback

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

Expected:

- injected failure after every resource definition/state/history, owner companion, effect carrier/index, pending, command deletion, and output write restores exact bytes/prior absence;
- post-validation failure restores all paths and suppresses stale narration/interface output;
- protected failures dispatch no actionable GM repair;
- one bounded GM-owned omission requires coherent full-turn resubmission.

## 12. Documentation, examples, and active template

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests|FullyQualifiedName~ResourceContractSourceGuardTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests|FullyQualifiedName~FileSystemExampleFixtureIntegrityTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Expected:

- Mortal and afterlife prompts/docs/examples/manifests describe only the new command/owner/projection rules;
- at least one worked Mortal and one worked afterlife response validate in production;
- active template/fixtures contain complete canonical resource roots and no removed authority fields;
- historical docs may mention removed fields only through an explicit source-guard allow-list.

## 13. Performance and meaningful control

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceAuthorityScaleTests|FullyQualifiedName~AcceptedMechanicsPlannerScaleTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Expected:

- doubling representative definitions/owners/state/history/mutations/triggers remains at or below 2.5x measured work;
- Fast passes with zero failures, duplicates, timeout, or leaked processes.

Record summary paths and elapsed times in the Superpowers implementation plan. Do not run another Fast immediately before PreMerge.

## 14. Final merge gate

Run only after every #1543 slice and resumed #1535 resource-dependent task is complete, docs/examples are synchronized, the legacy source guard is clean, and the diff has been reviewed:

```powershell
git diff --check
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Expected:

- `git diff --check` has no errors;
- PreMerge exits `0`, times out `false`, all completed tests pass, duplicate count is `0`, and cleanup is complete;
- no GitHub Actions are enabled or invoked;
- no compatibility reader, migration, dual-write, raw fallback, or second persisted resource authority remains.
