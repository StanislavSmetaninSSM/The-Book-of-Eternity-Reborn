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
- Scalability review keeps the immutable canonical history as the Task-3 authority and reserves a separate T039/T041/T046 `ResourceHistoryWorkingSet`: one baseline index seed, incremental same-turn replay/continuity admission, and one final full validation/sort/fingerprint. The future reducer is explicitly forbidden from calling the whole-history `Append` path per mutation; T114 measures this contour.
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
- At this historical Task-5 checkpoint the legacy player/item response properties and mappings still remained. T057 and T065 later removed their accepted write routes; no common/legacy dual write was introduced. The remaining item UI-reader cleanup is explicitly T067/T087–T092 and must use the safe projection rather than a raw-ledger or legacy fallback.
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
- At this historical Task-7 checkpoint only client-owned roots plus the bounded command envelope were active. Later T056–T066 evidence below records the Mortal owner/operation cutovers; T054/T067/T068+ and T100–T111 still retain the unfinished afterlife, projection, prompt, example, and template work. No migration/backcompat/dual authority was added, and GitHub Actions remain disabled and unused.

### Task-8 Mortal NPC/combat/vehicle owner evidence (2026-08-16)

- Owner-plan RED/GREEN work culminated in `TestResults/test-lanes/20260816-032935-885-52196-fdd0383449a74e5cbf8e36d782860e3b-focused/summary.json` (`11/11`). Combat lifecycle/cutover is GREEN at `TestResults/test-lanes/20260816-034354-632-57332-bbb8faa1faed4b7a8cd5d3fad1b80f36-focused/summary.json` (`10/10`), and vehicle identity/lifecycle/cutover is GREEN at `TestResults/test-lanes/20260816-034707-311-34492-5d4342867f8e4231a89c919b2bb3c92f-focused/summary.json` (`11/11`).
- Fresh post-item combined control is `TestResults/test-lanes/20260816-053030-394-30352-c58d520722e041299ea192e90786490d-focused/summary.json`: `34/34`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Named NPC combat rows reuse the exact NPC resource owner. Anonymous combatants and group members receive client-owned IDs even without resource/effect commands; group reorder preserves coordinates and removal retires only the exact member. Vehicle create/move/destroy preserves one permanent owner and atomically retires health. Legacy percentage/current/max/poise/positional health arrays are rejection-only mechanical inputs.
- T051/T055/T059–T062 are complete. The remaining old health/poise tokens in UI files are not mechanics authority and remain explicitly owned by the safe projection cutover T091–T092; no raw resource-ledger fallback is permitted.

### Task-9 item-owner and operation evidence (2026-08-16)

- The external legacy-cutover RED is `TestResults/test-lanes/20260816-045750-462-21600-9c0381e14c8e4efc9b4aff411972d1e9-focused/summary.json` (`0/1` because the old item commands were still accepted). GREEN after removing the `inventoryItemsResources` and `NPCInventoryResourcesChanges` mappings/properties/application and making the sidecar incompatible is `TestResults/test-lanes/20260816-045901-296-56372-135e7062fedf474fbc86147fb13bd357-focused/summary.json` (`1/1`).
- The ordinary-operation RED is `TestResults/test-lanes/20260816-051030-964-55528-ca9202cb7bb340d08fd7fd3e85da9499-focused/summary.json`: an item-local source had not yet been registered. GREEN is `TestResults/test-lanes/20260816-051627-585-46008-dff6161d7e9647d1a9a877d25e23d9ed-focused/summary.json` (`2/2`): use/repair/fire/reload reduce through the common ledger, and an item-local source cannot mutate another item coordinate.
- Named-NPC inventory parity is GREEN at `TestResults/test-lanes/20260816-051829-026-30460-7486caf4ac2f48138df2ceb7b2948b6d-focused/summary.json` (`1/1`). A setting-defined generic item reserve uses the same materialization/capacity/mutation route at `TestResults/test-lanes/20260816-052027-224-17908-80bed8e96ff344ed87e0be7d145de7c8-focused/summary.json` (`1/1`).
- Final item owner/lifecycle/operation control is `TestResults/test-lanes/20260816-052207-715-49000-3a9de042a2284c41a6850e8ae60b5fc9-focused/summary.json`: `31/31`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`. Planner/repair compatibility is `TestResults/test-lanes/20260816-052354-601-35460-9e80f35fc4a94494a09ae2c88c874eb6-focused/summary.json`: `48/48` with the same clean lane metadata.
- One permanent item identity now owns all durability/charge/ammunition/setting-defined reserves across inventory, equipment, NPC, location, and offscreen carriers. Movement preserves the coordinate; `proportional_exact` split/merge is explicit; terminal destruction/consumption retires every live coordinate and records history atomically; late write failure restores every owned file.
- T052 and T063–T066 are complete. T067 deliberately remains open until T087–T092 replace every item detail/action reader with `ResourceProjectionService`. This is not legacy support: old state is rejected, old commands cannot be staged, and no canonical sidecar writer/normalizer exists; the pending work prevents a temporary raw-ledger UI dependency from becoming a second authority.

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
- a new afterlife profile reuses its exact Actor-Materialized `actorId`; `materializationId` is the one-turn resource ownerRef, no second actor ID or legacy `actorRef` is created, and an optional setting-defined `resourceMaterialization` envelope is consumed into bindings plus common state/history.

## 7. Mortal ordinary-operation cutover

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~MortalResourceCutoverTests|FullyQualifiedName~ResourceCombatIntegrationTests|FullyQualifiedName~ResourceVehicleIntegrationTests|FullyQualifiedName~ResourceItemIntegrationTests|FullyQualifiedName~MortalBootstrapValidationTests"
```

Expected:

- ordinary damage, healing, energy spend/gain, poise damage/restore, vehicle damage/restore, and item use/repair/fire/reload use the reducer;
- Shining survival derives recovery from exact direct-phase player losses, records it in `registered_system_outcome`, and atomically consumes the blessing plus downgrades the triggering world event through the same plan;
- invoking the remaining Shining runtime hook after that publication is a byte-exact no-op, and late soul/world mutation fails before publication with exact rollback;
- legacy player delta, NPC/vehicle/combat health, group health array, durability, and item resource commands are rejected;
- no persisted legacy mirror remains after bootstrap or a turn;
- one invalid sibling produces zero resource/owner/effect/output writes.

## 8. Afterlife cutover

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~ResourceCapacityFormulaCatalogTests|FullyQualifiedName~ResourceContractSourceGuardTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeResourceCutoverTests|FullyQualifiedName~AfterlifeSpiritualConflictValidationTests|FullyQualifiedName~ResourceAfterlifeOwnerTests"
```

Expected:

- action-point cost/recovery, per-return gacha consumption/reset, and numeric blessing reroll use common state/history;
- registered capacity formulas reproduce existing spirit-focus/reputation/return-cycle rules;
- currencies, progression, relationships, faction ledgers, and spiritual axes remain unchanged in their owning systems;
- conflict/return-cycle closure retires only scoped resources.

### Task-10 afterlife-cutover evidence (2026-08-16)

- Spiritual-conflict cost/recovery/publication RED `TestResults/test-lanes/20260816-082647-026-29972-2281ea9452864f1b825482b71d20dec6-focused/summary.json` passed `15/19`; the four new ledger cases became GREEN at `TestResults/test-lanes/20260816-083004-317-24880-b3ce3c5b53ff467ba85b406338d75e8b-focused/summary.json` (`4/4`). The completed focused cutover class is GREEN at `TestResults/test-lanes/20260816-104323-963-32684-c5315c82099f43ce904d0c650683ed54-focused/summary.json` (`27/27`).
- Shining return-cycle companion-state RED `TestResults/test-lanes/20260816-084547-841-26960-081461c43e9f4f228ff39d66fccd1d97-focused/summary.json` (`0/4`) became GREEN at `TestResults/test-lanes/20260816-084650-556-40876-706217042e1844099481cc80df3c46e4-focused/summary.json` (`4/4`). The blessing/reroll consumer cutover progressed from `TestResults/test-lanes/20260816-095850-771-7784-aee79eaa38a7413caca3e9f8cecc3301-focused/summary.json` (`43/62`) to `TestResults/test-lanes/20260816-101042-361-40944-2ddea643e86141728aa339f90365ac0a-focused/summary.json` (`62/62`).
- Reserved out-of-scope admission RED `TestResults/test-lanes/20260816-104646-680-52840-ae6b667b221944cd87ee6df2bf80d809-focused/summary.json` (`1/15`) became GREEN at `TestResults/test-lanes/20260816-104733-882-50716-058dc4e4d9f9447383cb8291f7ed2b1c-focused/summary.json` (`15/15`). Fresh definition/source-guard control is `TestResults/test-lanes/20260816-111537-560-30940-4acbf47fe59c4af2ae98dd89582a2082-focused/summary.json` (`50/50`).
- GM-facing afterlife documentation RED `TestResults/test-lanes/20260816-105500-915-29468-294ed1cd894f45d1bfbe0bb6ec94abd1-focused/summary.json` (`0/3`) became GREEN at `TestResults/test-lanes/20260816-110812-163-42120-fc42d27c507a48e6a0f67bc13be67247-focused/summary.json` (`3/3`); the full documentation class is GREEN at `TestResults/test-lanes/20260816-110913-423-45520-7fd46e37cb1b42258dd8a0a12e5e6257-focused/summary.json` (`115/115`).
- Fresh controls are GREEN: all owner families `TestResults/test-lanes/20260816-111557-100-54836-6101f7087a8b4c36ab229ae83d234dbd-focused/summary.json` (`30/30`), Mortal/combat/vehicle/item/bootstrap `TestResults/test-lanes/20260816-112120-153-39164-59f21ef8491b4ca7b510388908525601-focused/summary.json` (`86/86`), and afterlife cutover/validation/owners `TestResults/test-lanes/20260816-112258-494-44060-8ab2012b1e2542b890c558bd77034cb0-focused/summary.json` (`392/392`). Every control reports failures `0`, duplicate IDs `0`, timeout `false`, and cleanup `complete`.
- Persistent-owner and lifecycle follow-up is GREEN: owner composition `TestResults/test-lanes/20260816-120513-351-31104-dfcd5b3d2351417986837097ff0e941d-focused/summary.json` (`11/11`), common planner/authority `TestResults/test-lanes/20260816-120816-090-54124-30d6ea841fdd4e2ca67b4e532a841830-focused/summary.json` (`40/40`), Spirit Focus/Radiance reconfiguration `TestResults/test-lanes/20260816-121024-932-54960-801257b7e2304e588da3fca4b57cbbaa-focused/summary.json` (`2/2`), local atomic owner service `TestResults/test-lanes/20260816-122302-999-46528-ab3949907fe946048f9f3ce2c8556ec1-focused/summary.json` (`1/1`), console `TestResults/test-lanes/20260816-122901-426-42252-8220a888306a4ab3b97d10fda19e3dbb-focused/summary.json` (`1/1`), browser `TestResults/test-lanes/20260816-123242-163-29840-fbc5d32346474abc9142261fe621fa8d-focused/summary.json` (`1/1`), Shining return cycle `TestResults/test-lanes/20260816-123439-363-41792-f0ff1161dc3e453795df8314be8367e3-focused/summary.json` (`3/3`), and cross-realm suspend/resume `TestResults/test-lanes/20260816-125008-379-37220-c560a8ff93054f6f8550f26a497f75c0-focused/summary.json` (`1/1`).
- Read-only review found and the implementation closed five Important gaps: Shining `resourceOwnerBindings` allowlist (`123707` RED → `123942` GREEN), stale `activeConflict.actionEconomy` repair instructions (`124203` RED → `124316` GREEN), special-art legacy mechanical axis (`124521` RED → `124605` GREEN), persistent/same-cycle capacity reconfiguration, and realm ledger suspend/resume. The full afterlife documentation class after these corrections is GREEN at `TestResults/test-lanes/20260816-125233-972-38520-d7782a28e1984de29086122a3f322c73-focused/summary.json` (`115/115`).
- Blessing bootstrap now supplies authoritative soul-state formula inputs: `TestResults/test-lanes/20260816-130126-633-48536-777c3362ca1c44dea8acb9c693a05726-focused/summary.json` is `6/6`; combined Task-10 control `TestResults/test-lanes/20260816-130226-717-50392-bce29fcf8d7645bbb9f83ad7bc5ddc71-focused/summary.json` is `80/80`; expanded afterlife control `TestResults/test-lanes/20260816-130405-342-17960-d892b6f9fda14b54afecd14345ec5630-focused/summary.json` is `398/398`.
- The strict new-NPC item fixture now carries its required health resource envelope and uses production item→resource validation. RED `TestResults/test-lanes/20260816-131615-271-27792-41369c4e5111460ca0b45f887c1ff487-focused/summary.json` (`0/1`) became GREEN across complete Routes+Companions at `TestResults/test-lanes/20260816-132945-319-13104-1c9f5a6d198a4759b9770e50159c82dd-focused/summary.json` (`70/70`).
- Fresh conditional documentation-boundary FullValidation is `TestResults/test-lanes/20260816-133413-277-32576-a67a6581285f44c389a001af43da6013-fullvalidation/summary.json`: official terminal shard `171/271` passed, `100` failed, timeout `false`, cleanup `complete`, duplicates `0`. The runner currently retains only the terminal descriptor in its summary; complete log classification is `334` expected later-task failures: Shining display-save `100`, Chaos Sea display-save `95`, Mortal display-save `95`, Guardian policy `29`, fixture integrity `4`, foundation `4`, example docs `2`, Math Assistant `2`, Mortal location lifecycle `2`, archive/trade `1`. They are owned by T102–T113, principally T110 and final T113. This intermediate diagnostic is not a compatibility exception: no migration, dual reader/writer, validator weakening, or fallback is added.
- The realm-transition hardening keeps `player_soul` as one persistent cross-realm owner: Chaos Sea↔Shining Abode bindings and matching ledger entries suspend/resume atomically, while Mortal World does not blanket-suspend unrelated persistent resources such as `blessing_rerolls`. The rejected overbroad rule is captured by RED `TestResults/test-lanes/20260816-135639-892-36504-c23cc866145a4dd8836ca1b96cc6cf77-focused/summary.json`; the corrected realm/source-guard control is GREEN at `TestResults/test-lanes/20260816-140420-080-41976-e77aaf37d809467eb51332d4d34b431d-focused/summary.json` (`37/37`).
- The final four Fast-tail fixtures were corrected at their strict authority boundaries and are GREEN at `TestResults/test-lanes/20260816-141300-358-23072-44e28187e8824c0baf6567f382f84004-focused/summary.json` (`4/4`). The meaningful Task-10 Fast is `TestResults/test-lanes/20260816-141337-327-28620-c1614a23718b4e23bbd5e1be73a62ef0-fast/summary.json`: exit `0`, timeout `false`, `3840/3840` executed and passed, failures `0`, duplicate IDs `0`, cleanup `complete`, wall `00:03:50.8760071`.
- The post-evidence documentation/source-guard control is `TestResults/test-lanes/20260816-142754-593-45976-7e0a907decdd42dabdf888a2e978bf43-focused/summary.json`: `265/265`, failures `0`, duplicate IDs `0`, timeout `false`, cleanup `complete`, build warnings/errors `0/0`.
- Fresh T074 merge-gate controls on the reviewed 2026-08-21 tree are GREEN: definition/source guards `TestResults/test-lanes/20260821-100910-253-34684-0b7eeaeeb28d46c5812bad46ea00fa57-focused/summary.json` (`50/50`); owner/Mortal/combat/vehicle `TestResults/test-lanes/20260821-100929-732-18728-10cd00841720464981386d6638967e9f-focused/summary.json` (`42/42`); item owners/operations `TestResults/test-lanes/20260821-101002-507-38516-c474a14dc1f54eb889d85d747be07c9f-focused/summary.json` (`16/16`); afterlife owners/cutover `TestResults/test-lanes/20260821-101148-637-26964-dab7fad4f1624574ab260df44f12fac7-focused/summary.json` (`49/49`); and `AfterlifeDocumentationCoverageTests` `TestResults/test-lanes/20260821-101504-212-45092-d032cace66d04cf6baec98311a887664-focused/summary.json` (`115/115`). Every summary reports exit `0`, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, and build warnings/errors `0/0` where reported.
- Realm transition authority is now explicit and lease-bound: wrong-source-realm RED `20260821-094251` → GREEN `20260821-094430`, stale-enlightenment RED `20260821-094556` → GREEN `20260821-094742`, and life-transition-conflict RED `20260821-094911` → GREEN `20260821-095006`. Exact synchronize/reentry/ascension kinds are sealed before publication; fresh ascension prerequisites are evaluated under the same canonical lease; `blessing_rerolls` is the sealed realm-independent `player_soul` capability while `spiritual_action_points` remains realm-bound; and only a new Mortal-life return resets Guardian attempts.
- Fresh conditional FullValidation is `TestResults/test-lanes/20260821-101640-675-36656-be86c9f78db94ed7965957545843ca96-fullvalidation/summary.json`: `171/271` passed, `100` failed, exit `1`, timeout `false`, cleanup `complete`, duplicate IDs `0`, wall `00:03:50.5215232`. Its `100` unique failed names exactly equal the previously classified `20260816-150510-928-19064-d9efa47a16874be5acfda0a34781bbf1` set (`OnlyNew=0`, `OnlyOld=0`), so the current tree adds no new diagnostic failure family. The known active-template/save-fixture and remaining legacy groups stay owned by T102–T113; no migration, dual authority, fallback, or validator weakening was introduced.
- The T074 inventory audit found no remaining legacy writer, stager, or normalizer in a completed US3 contour. Surviving production tokens are strict rejection/strip paths or explicit exclusions (QTE-local pin durability, currencies, progression, relationships, faction accounting, spiritual axes). Remaining item detail/action readers are preserved for T067 after T087–T092; Mortal status/detail readers for T091–T092; Guardian/Shining projections for T093; and active Mortal prompts/examples/templates plus the final zero-legacy guard for T102–T113. Those tasks remain open so the planned refactor cannot be mistaken for completed compatibility support.
- Prompt/example rationale: transition kinds, fresh ascension checks, lease fencing, and capability activity policy are client-owned authority and introduce no GM-authored field. Guardian reset semantics changed, so `Rules/Block_32_Guardians.txt` was synchronized; the existing afterlife worked examples and fresh `115/115` documentation control cover the GM-facing behavior.
- Fast exposed and then closed stale fixture assumptions without weakening production authority. `20260821-102219-504-47984-3a06622f97374a90b7c8c7d65d7826b1-fast` found `19` cascading blessing/relic failures and `20260821-102611-240-40588-745f1ad0876a4c05b8777bbde8823027-fast` isolated the last two browser variants. Focused evidence is `20260821-102418-808-44284-44f34633a0b2408f8a2b605073d9c01b-focused` (`0/1`) → `20260821-102501-114-27756-7070bdf9b4cb410ba98120d90f2cd908-focused` (`1/1`), then `20260821-102534-420-29336-e8cb808e890f437b9d4be30f08848a3a-focused` (`30/30`), `20260821-103033-705-44460-8d9f1695bb954a1b816f16b86633821f-focused` (`2/2`), and `20260821-103107-344-40956-2974f8007693458daa49ad670aa9d32f-focused` (`16/16`). The corrected Mortal fixtures suspend their afterlife owner binding while the sealed realm-independent reroll capability remains active.
- Final console-owner review added the missing stale-root guard for Spirit Focus and all adjacent spiritual-art writes. The concurrent Soul mutation case progressed from RED `TestResults/test-lanes/20260821-104017-847-28164-66570469c0e5454fa248c2fdefa3a776-focused/summary.json` (`0/1`) to GREEN `TestResults/test-lanes/20260821-104246-185-3304-3d2a7520c78648169677a93bc6fabfc8-focused/summary.json` (`1/1`). The expanded contour then exposed refresh under the still-held lease at `20260821-104403-131-41940-cd3a21573dd54e1e83b5cf1b727da403-focused` (`9/14`); moving refresh after lease disposal closed it at `TestResults/test-lanes/20260821-104754-151-26212-1d9c2615f540473297c999dd47b8bfa9-focused/summary.json` (`14/14`).
- The meaningful final T074 Fast is `TestResults/test-lanes/20260821-104914-178-43536-0260f7b40c974fef8112ac8bd2ccfeeb-fast/summary.json`: exit `0`, `3847/3847` passed, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, wall `00:03:53.2623518`, build warnings/errors `0/0`.
- Generic Guardian/player/GM resource projection remains T089/T093/T095. This slice exposes only bounded read-only data required by existing consumers, with no raw-ledger or legacy fallback.
- GitHub Actions remain disabled and unused.

## 9. Effect Task 9 integration

### T075/T078 periodic effect-resource adapter evidence (2026-08-21)

- Final compiling RED before implementation is `TestResults/test-lanes/20260821-112309-249-36692-438009f1b4fd4dd181b5c31490de0da2-focused/summary.json`: the `25` pre-existing planner tests passed and all `13` new periodic cases failed only because the typed adapter was absent. Exact target-owner source binding has its own RED at `TestResults/test-lanes/20260821-112732-320-47896-4436d48e2dde4326a976999c019ef5c5-focused/summary.json` (`0/1`).
- Final replay/fail-closed hardening RED is `TestResults/test-lanes/20260821-115153-379-43532-e58ca2beeffd418d8a53ca54d9d8eb61-focused/summary.json`: `0/2`; it proves that changing a result constraint was previously accepted as an exact replay and that an invalid sibling target authority was ignored. Final typed-API GREEN is `TestResults/test-lanes/20260821-115423-887-29552-955f5272fe344dbba861d17e49fbf9a4-focused/summary.json`: `45/45`, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, build warnings/errors `0/0`. It covers damage/restore, player/NPC/anonymous and named combatant, accepted same-turn permanent target, setting-defined afterlife resource, unsupported target/resource/capability, quantum/bounds, closed floor behavior, immutable replay-policy binding, global authority fail-closed behavior, collision-safe effect+trigger+component source identity, and 100-run ordinary/effect semantic byte equivalence.
- Adjacent authority/contract control is `TestResults/test-lanes/20260821-115456-620-14916-f7e432c321004d9a9a5bd6d8179fe536-focused/summary.json`: `169/169`, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`. The meaningful post-hardening checkpoint is `TestResults/test-lanes/20260821-115519-933-7368-0b62bcf77f244a9994717acc1e76df9d-fast/summary.json`: exit `0`, `3867/3867` passed, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, wall `00:04:02.2703338`.
- This first slice intentionally stops at the typed selected-trigger adapter. T076/T079 evidence below owns resource-event trigger/DAG feedback, while T080 still owns lifetime ordering and terminal cleanup; T077 still owns full accepted-turn publication and rollback. No effect-only arithmetic, legacy field adapter, migration, compatibility fallback, or GitHub Actions evidence was added.

### T076/T079 resource-event feedback evidence (2026-08-21)

- Compiling RED `TestResults/test-lanes/20260821-120635-806-40388-4647227165444197b83f78e41bd8128d-focused/summary.json` passed `35/37`: the missing typed resource-event adapter and the still-open exact lifetime event match were the only failures. The full reducer feedback RED is `TestResults/test-lanes/20260821-120833-073-30620-6bbbdb4079b54711ac8937be70628454-focused/summary.json` (`0/2`), proving that a downstream periodic restore was not yet constructible for one exact producer event. The shared source-definition event registry RED is `TestResults/test-lanes/20260821-120954-344-6952-adb8d3cf94334afba6b82a5b8e3207c-focused/summary.json` (`0/12`).
- Final T079 unit GREEN is `TestResults/test-lanes/20260821-121823-265-47372-078ac1a3c08c4dbdae6049f3665b89cf-focused/summary.json`: `109/109`, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, build warnings/errors `0/0`. One closed catalog now admits exactly `resource_damaged`, `resource_restored`, `resource_spent`, `resource_gained`, `resource_depleted`, and `resource_filled` across source, instance, and component contracts. The adapter binds a due trigger to one exact producer operation; the common graph orders the dependency before the effect node, and the common reducer executes the downstream mutation only when that producer actually emits the declared event. Cycle, depth, and `1024/1025` expansion controls remain client-owned.
- T076 is complete as a RED task. Its resource-event/DAG half is GREEN through T079; its exact uses/lifetime/terminal half deliberately remains RED for T080. T077 remains responsible for proving one cached player/NPC/vehicle/combat/afterlife resource+effect publication and rollback rather than treating these unit adapters as a completed file-level feature.

### T077 full publication/rollback evidence (2026-08-21)

- Full player, named NPC, anonymous combatant, and accepted afterlife actor periodic publication now uses one cached resource/effect plan. Unsupported vehicle targeting fails before writes; source/target disappearance after validation rejects publication with exact resource/effect/command bytes preserved. The original afterlife RED exposed the stale `actorRef`/second-ID contour at `TestResults/test-lanes/20260821-143348-498-21564-c4bb46d50f514673aeeefd11b85d934e-focused/summary.json` (`0/2`). The corrected Actor Materialization handoff is GREEN at `TestResults/test-lanes/20260821-143716-135-17404-935099ea43ae4d2d85cde234f74262ff-focused/summary.json` (`2/2`), and the complete T077 plus afterlife-owner control is `TestResults/test-lanes/20260821-144155-204-42696-007d6aee91fc4e33ac8027136d8c006d-focused/summary.json` (`22/22`, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, build warnings/errors `0/0`).
- GM documentation RED→GREEN is `TestResults/test-lanes/20260821-144011-035-11540-1829006e097145ffba144701a8558cd4-focused/summary.json` (`0/1`) to `TestResults/test-lanes/20260821-144135-651-7336-c1c8f10b68144c91b4e61962369379b0-focused/summary.json` (`1/1`). The complete mandatory afterlife documentation control is `TestResults/test-lanes/20260821-144547-778-38580-a36a994f8054457495967b4e84027f4d-focused/summary.json` (`116/116`, failures `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`). Matrix, daemon directive, worked afterlife profile, and manifest now agree: only a complete first profile may author `resourceMaterialization`; the exact actorId is preserved, materializationId is the one-turn resource ownerRef, the client creates no second actor ID, and existing profiles cannot resend the envelope.

### T080 lifetime finalization evidence (2026-08-21)

- Broad diagnostic RED `TestResults/test-lanes/20260821-145342-912-11220-0f01f5d1ec7b4e59a12404e063dcc5e9-focused/summary.json` passed `139/157`. Four T080 lifecycle regressions proved that the obsolete test-only effect publisher stopped before post-graph finalization; the other fourteen failures belong to already tracked breaking resource-cutover fixtures and are not counted as T080 failures. The four affected turn/time/stack/refresh cases are GREEN through the real common plan at `TestResults/test-lanes/20260821-150008-453-41884-703dee44467f44c7bd38d42cb6bd8f8b-focused/summary.json` (`4/4`).
- Complete T080 common publication coverage is `TestResults/test-lanes/20260821-150414-320-36776-f6243dcf799b4a7aa0bfab6389c3619e-focused/summary.json` (`14/14`): exact resource-event uses are consumed only after the graph executes, final-use expiry removes the carrier occurrence and appends one terminal index transition atomically, and turn/time/source-loss continuation publishes through the same cached accepted-mechanics plan. The full lifecycle integration class is GREEN at `TestResults/test-lanes/20260821-150524-212-17420-b8bf29a4db2e4a31b1ff4605dd8ff881-focused/summary.json` (`19/19`).
- Unit control `TestResults/test-lanes/20260821-150501-811-12836-1ff44e48747646aab3a4cc15ab2b0b82-focused/summary.json` passes `86/86` across the common planner, resource graph, all five stack policies, and all eight lifetime modes. The production adapter emits the accepted owner-turn phase and exact canonical world time. Scene and condition transitions remain fail-closed unless their future owning subsystem supplies exact validated lifecycle context; they are never inferred from GM prose. Source/condition continuation precedes removals/applications, graph-triggered uses are consumed from actual executions, and non-bound turn/time/scene advancement plus terminal cleanup follows the graph.
- T085 completes the remaining publication cutover below: `NormalizeEffectsAsync` and the effect cache's independent consume/status APIs are removed; the retained subordinate effect result is readable only by the common planner before one `AcceptedMechanicsPlan` is cached and consumed.
- Meaningful post-T080 checkpoint `TestResults/test-lanes/20260821-150856-791-34180-1f3a6c7ad0724a89804c269a619663fa-fast/summary.json` passes `3888/3888`, failures `0`, exit `0`, timeout `false`, duplicate IDs `0`, cleanup `complete`, wall `00:04:03.3286263`, build warnings/errors `0/0`.

### T081–T084 bounded pending evidence (2026-08-21)

- Unit RED `TestResults/test-lanes/20260821-153057-727-28780-61405f057897424b88f09b352008a292-focused/summary.json` passed `23/25`; GREEN `20260821-153234-691-27676-3bca33722c354f3a8878a312c64efb0d-focused` passes `25/25`. The closed client state and safe packet cover narrated no-change, bounded delta, privacy, deterministic-operation exclusion, missing/stale/partial/extra/cross-target/wrong-operation/out-of-bound receipts, and replay.
- Integration RED `TestResults/test-lanes/20260821-153959-271-39120-65e8dd3036e74560bfcf9572d16d21c8-focused/summary.json` was `0/3`; GREEN `20260821-155344-617-11788-6a28f8a5746446daa1577067783ae298-focused` is `3/3`. The pre-receipt pass publishes only technical pending work, the coherent full-turn receipt becomes one ordinary common mutation, failures roll back exact bytes, and stale output is suppressed.
- Same-turn effect binding RED `TestResults/test-lanes/20260821-164655-867-35136-c1d018711df24345bd344bb2d2036900-focused/summary.json` is `0/1`; GREEN `20260821-165811-784-18448-6f3bf75cd04b4bd59071a947712cd45f-focused` is `1/1`. The request binds an unpublished effect through its accepted application event and re-resolves the random identity on resubmission; it never predicts or persists a temporary random ID as authority.
- Final unit and integration pending controls are `TestResults/test-lanes/20260821-164926-671-19052-9ca7962a2d6a42eea7f5da43c05df6e7-focused/summary.json` (`29/29`) and `20260821-170559-758-46844-52acecb09ee24e1ab93fadb3e15299f5-focused` (`4/4`). They include terminal semantic replay: the complete original command set is consumed as an exact no-op after success, while conflicting replay fails closed.

### T085 sole-publication cutover evidence (2026-08-21)

- Source-guard RED `TestResults/test-lanes/20260821-173041-298-7788-8211c4986c4347aebfe741b473e41291-focused/summary.json` (`0/1`) found the independent effect publisher. GREEN `20260821-173239-135-42304-592d8e60517f4071a1194103d90a4161-focused` (`3/3`) proves production has no `NormalizeEffectsAsync`, `EffectAcceptedTurnPlanCache.TryGetValidated`, or effect-cache `HasValidated`; the common accumulated normalizer still invokes `NormalizeAcceptedMechanicsAsync` and the effect publication binding preflight.
- The remaining test-only callers exposed nine incomplete common-plan fixtures at `TestResults/test-lanes/20260821-173455-115-24408-e1792ff912dc4c43bfe12edb60536c75-focused/summary.json` (`139/148`). After migrating skill/quest/location/item/NPC/combat composition to the full resource+effect authority, the complete class is GREEN at `20260821-175114-670-30688-2123dee4b4f1466294a33e4065b2b89d-focused` (`148/148`). The corrected named-combat model uses one NPC identity (`npcRef -> NPCId`) and reserves `combatantRef -> combatantId` for anonymous combatants.
- The Task-11 integration sweep exposed one stale assertion that still looked for a resource-allocated anonymous combatant rewrite in the effect subplan. The rewrite is intentionally owned by the common owner companion after-image, while the effect subplan consumes the same preallocated identity. The corrected exact-owner assertion is GREEN at `TestResults/test-lanes/20260822-002459-658-27276-380a38b0aa38439888f46e7485f9626f-focused/summary.json` (`1/1`).
- All cited GREEN runs exited `0`, timed out `false`, reported duplicate IDs `0`, and completed owned-tree cleanup. Later afterlife adapters, projections, exhaustive lifecycle rollback/repair, wound integration, active fixture migration, and final verification remain open in their existing tasks.

### T086 final US4 control evidence (2026-08-21)

- Combined common-planner/graph/pending/lifecycle/source-guard/effect-planner unit control: `TestResults/test-lanes/20260821-180604-546-2708-85b2f850c9824e468a33fc50dd02e2a9-focused/summary.json` (`164/164`).
- Combined resource-effect/pending/full effect-validation/canonical-publication integration control: `TestResults/test-lanes/20260821-180628-499-26240-d3d401e6a2b04bde9c4e9a201a2b30cd-focused/summary.json` (`188/188`).
- Meaningful Fast checkpoint: `TestResults/test-lanes/20260821-181034-943-2272-ec1beda269a24b0b94e30d8d904a9488-fast/summary.json` (`3920/3920`). All three runs exited `0`, timed out `false`, reported duplicate IDs `0`, completed owned-tree cleanup, and built with zero warnings/errors.

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

Task-12 evidence (2026-08-22):

- Projection/status TDD: `20260822-102311` RED and `20260822-102455` GREEN; durability/privacy RED runs `20260822-102750` and `20260822-102836`, followed by `20260822-102920` GREEN (`7/7`).
- Projection/privacy unit controls are GREEN at `20260822-103146` (`18/18`) and the combined Task-12 unit selection at `20260822-103925` (`42/42`). Browser DTO contract first exposed one missing field at `20260822-103437` (`49/50`) and is GREEN at `20260822-103831` (`32/32`).
- Web fail-closed status rendering is RED/GREEN at `20260822-103255` / `20260822-103330`; `npm run verify` passes all `139/139` frontend tests, TypeScript checking, and the production build. The React change reuses the existing `StatusEmptyState` and design tokens, so it introduces no new visual layout or asset requiring screenshot approval.
- The monolithic console/browser diagnostic `20260822-103925` exceeded the bounded five-minute Focused lane and exposed four stale fixture/assertion contours before timeout. Those contours were repaired without a legacy fallback: canonical combat effects, exact afterlife owner input, safe structured conflict copy, and opaque Shining cycle status. Targeted recheck `20260822-105455` passed three of five and isolated the last two fixture/assertion mismatches; final recheck `20260822-105710` is GREEN (`2/2`), and the complete parity class `20260822-105751` is GREEN (`8/8`), all with timeout `false`, duplicate IDs `0`, and cleanup complete.
- Serialized C#/TypeScript DTOs expose only the player projection availability flag, safe values, and fixed Russian unavailable message. The projection is client-owned and read-only; no GM-authored response field or afterlife action contract changed, so no GM prompt/example addition is required for this slice.

## 11. Atomic publication and rollback

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration
```

Expected:

- injected failure after every resource definition/state/history, owner companion, effect carrier/index, pending, command deletion, and output write restores exact bytes/prior absence;
- post-validation failure restores all paths and suppresses stale narration/interface output;
- protected failures dispatch no actionable GM repair;
- one bounded GM-owned omission requires coherent full-turn resubmission.

### T098-T100 atomic publication and TOCTOU evidence (2026-08-22)

- The original resource lifecycle REDs are `TestResults/test-lanes/20260822-110329-495-36948-88b934de10d94677bb45d68d4a935160-focused/summary.json` (`5/6`) and `20260822-110712-505-47736-5101199baa944bd9862430d10edb55f0-focused` / `20260822-110859-701-3388-e3c3c23319764780a81cb1e30f120b46-focused` (both `4/5`): command-consumption and post-publication rollback boundaries were not yet proved. The first GREEN contours are `20260822-110525-491-20592-62b7735349cc4db69b0f1666f0cbb084-focused` and `20260822-111006-296-2052-79cff43b93c84c98bbd38cd0722c55ed-focused` (both `6/6`).
- The strengthened physical-publication/post-validation resource control is `TestResults/test-lanes/20260822-160433-921-17448-7a063c7596664be0a18eb1d680ac263e-focused/summary.json` (`5/5`). It injects after the physical definition/state/history/index publication, proves the command-consumption boundary is reached before canonical post-validation fails, and restores exact bytes and prior absence under the same write lease.
- The complete T098-T100 resource selection is `TestResults/test-lanes/20260822-161549-027-34552-d6dcdd8cb26c49e18e6f668a081acaad-focused/summary.json` (`27/27`). Its 12-case common-plan TOCTOU matrix rejects late definition, owner, state, history, source, target, carrier, index, accepted-event, command, pending, and internal-snapshot mutations before the first write; the focused matrix alone is `20260822-161336-249-43680-5fdbf868a33d4b529529f3c0add02b52-focused` (`12/12`).
- The outer accepted-turn rollback and stale-output control is `TestResults/test-lanes/20260822-161708-657-19168-2e39670424974aa9ad74fdd7d112c935-focused/summary.json` (`14/14`). It covers player and every owner effect carrier, resource roots, identity index, bounded pending, command consumption, post-check failure, exact whole-turn rollback, suppression of staged narrative/interface output, and preservation of operator-only diagnostics. Every cited GREEN exits `0`, times out `false`, reports duplicate IDs `0`, completes owned-tree cleanup, and builds with warnings/errors `0/0`.
- Publication now has one cached random-ID common plan and one canonical write lease from exact before-image/fingerprint comparison through ordered owner/resource/effect/pending writes, command consumption, canonical post-validation, and exact reverse restoration. Session-replacement behavior remains owned by the existing session fence; diagnostics are excluded from player output rollback by design.

### T101 bounded resource-repair evidence (2026-08-22)

- The lifecycle RED `TestResults/test-lanes/20260822-162624-530-43852-65130fb3fd5a424e91188ed45e645985-focused/summary.json` is `0/2`: protected resource authority reached the broad GM repair loop, while the sole safe omission dispatched before the rejected turn baseline was restored. The exact-evidence unit RED `20260822-163010-223-27596-cad1d3f3ed634a92a4e5d8951cb69d24-focused` is `14/15` because a lookalike issue without the registered producer evidence was still actionable.
- Final builder/request control `TestResults/test-lanes/20260822-164855-051-10244-750fa3c983b14baa98eda38efee224be-focused/summary.json` passes `24/24`. Only one exact Error/Client/UnifiedResourceAuthority/StateConsistency omission at `resourceChanges[n].reason` is repairable; identity, replay, arithmetic, capacity, cycle, owner, pending-receipt, direct-mutation, wrong-provenance, multiple-distinct-omission, and protected-target cases stay fail-closed. Byte-identical duplicate evidence from composed validation phases is one logical omission, not a second repair grant.
- Full lifecycle GREEN `TestResults/test-lanes/20260822-164952-491-10056-e02b967154624aa1a76fd3e3a7795fd4-focused/summary.json` passes `4/4`. A bounded retry requires rollback authority, restores the pre-turn baseline before dispatch, exposes no protected selector/path/identity, binds the entire rejected duplicate-free command root internally, permits only the missing readable reason to change, and requires every rejected command/output surface to be republished. Ready-only, altered-amount, and command-only retries are rejected; the coherent replay publishes exactly once. The run exits `0`, times out `false`, reports duplicate IDs `0`, completes cleanup, and builds with warnings/errors `0/0`.

### T102-T103 no-legacy source-guard evidence (2026-08-22)

- The earlier guard was `34/34` GREEN at `TestResults/test-lanes/20260822-165429-224-36964-30364ac64df6495b916ab1c13c1110f2-focused/summary.json`, proving that it did not yet cover the complete phase-1 inventory. The expanded T102 RED `TestResults/test-lanes/20260822-171037-196-16160-6e7125db39ff4150b3752aa4ff4a11bd-focused/summary.json` is `33/50`, with `17` expected failures grouped into twelve newly sealed out-of-scope mechanical families, silent compatibility cleanup, and active GM documentation/example legacy authority.
- The T103 production contour is GREEN at `TestResults/test-lanes/20260822-171843-195-27376-f698d0bcb180457d8324494ef8be682d-focused/summary.json` (`33/33`). Production contains no removed resource writer, active-reader fallback, positive legacy validator, or silent `.Remove`/normalization path; the retired item sidecar remains visible only to the strict incompatible-save detector, and the reviewed exclusions are closed in the definition catalog rather than admitted as generic resources.
- Explicit Shining counter rejection progressed from RED `TestResults/test-lanes/20260822-171630-894-28304-89ce9f8d69d04422ab8b37385a818c4f-focused/summary.json` (`0/2`) to GREEN `TestResults/test-lanes/20260822-171740-857-4040-264730fdc53c4203b1752b44a537a611-focused/summary.json` (`2/2`). Old `chargesPerReturn` and `chargesUsedThisReturn` fields are rejected, never stripped, migrated, or used as fallback authority. The adjacent owner control `TestResults/test-lanes/20260822-172042-776-15740-a3e7d9f259ea4252b529ae4527f6fd93-focused/summary.json` is `13/13`.
- The documentation/example/template half of the expanded guard remains intentionally RED and is owned by T107-T111. Existing mechanics such as health and damage formulas are not removed; those active contracts must be rewritten to reference the common resource definition/capacity/ledger authority without preserving legacy current/max mirrors.

### T104 accepted ordinary-source authority evidence (2026-08-22)

- Composer RED `TestResults/test-lanes/20260822-135351-374-18984-447c0abec5f24d41896662d7ecf637dc-focused/summary.json` passed `0/2`: `{kind}` was rejected and a submitted arbitrary `sourceId` was still accepted. Integration RED `TestResults/test-lanes/20260822-135432-652-39192-68bb58600a5a41898d2d5d9af372f978-focused/summary.json` passed `0/1` because no ordinary accepted-event source authority existed.
- Final composer control `TestResults/test-lanes/20260822-141342-366-5184-d8a8598cb8c644188bef57f10bd98634-focused/summary.json` passes `38/38`; all three generic routes accept only `{kind}`, derive `sourceId = eventRef`, and reject submitted source identity. Planner/source-catalog control `TestResults/test-lanes/20260822-140939-712-43312-5b41f1b809a7414dbe76ca5c8ae7dffc-focused/summary.json` passes `58/58`, including exact target-owner binding and wrong-target/unbound rejection for all three routes. Full resource materialization validation control `TestResults/test-lanes/20260822-140734-998-41204-1b81d768b30645fe9fc977208f0ebf0c-focused/summary.json` passes `20/20` with zero writes during raw validation.
- Every cited GREEN exits `0`, times out `false`, reports duplicate IDs `0`, completes owned-tree cleanup, and builds with warnings/errors `0/0`. The post-slice Fast `TestResults/test-lanes/20260822-141052-491-10880-6f0ca07b82184b1482681823f445fa9c-fast/summary.json` confirms no ordinary-source failure but remains globally RED at `1021/1024` on three separately tracked unfinished source/documentation guards (`AfterlifeShiningPlayerFacingSourceGuardTests`, `ExplorerModeSourceGuardTests`, and `PromptDocumentationCoverageTests`); those are not weakened or relabeled as T104 failures.
- Raw generic sources no longer create a GM-owned identity namespace. The accepted ordinal event is the one occurrence identity, its authority fingerprint binds session/request/turn/ordinal/operation/amount/reason and the exact composed owner, and the registered route cannot resolve for another target. Durable item-local routes continue to use the exact permanent item ID.

### T105 carrier-independent combat-member evidence (2026-08-22)

- Resource RED `TestResults/test-lanes/20260822-142302-292-28620-bd2179fc0bfb45339e721a06d35ad254-focused/summary.json` passes only `1/6`: the detached row was interpreted as a new anonymous combatant and mixed member/NPC/combatant selectors were not closed. Effect RED `TestResults/test-lanes/20260822-142357-175-44376-c69fb652afa74f6ba3d0e258be1dea40-focused/summary.json` is `0/3`: a detached `memberId` was neither an effect target/carrier nor part of exact/confusable cross-family ambiguity. The reverse-carrier RED `TestResults/test-lanes/20260822-144241-938-37668-3d3be860cce843f98c33e5e86ee05fa6-focused/summary.json` is `0/1` because moving an active member effect back into `members[]` looked like forbidden deletion.
- Resource GREEN `TestResults/test-lanes/20260822-143629-991-34852-8b44976208654cea8c72235deb8e8ea2-focused/summary.json` is `16/16`. The same permanent member moves nested -> detached -> nested without a new owner, state transition, initialization, retirement, or history coordinate, while mixed `NPCId`/`npcRef`/`combatantId`/`combatantRef` selectors fail closed.
- Final effect GREEN `TestResults/test-lanes/20260822-145041-480-45880-718dfbe331384eedaa646830ce73012b-focused/summary.json` is `165/165`. It covers permanent detachment, nested and detached same-turn `memberRef` allocation, active carrier movement back into the group, identity-index continuity, and exact/confusable member-versus-combatant rejection. The target/planner/common-identity unit control `TestResults/test-lanes/20260822-145020-625-13268-f87bc1b737714dbe95be66641e9a523b-focused/summary.json` is `148/148`, including periodic resource routing from effect target kind `combatant` to resource owner kind `combat_group_member`.
- Every cited GREEN exits `0`, times out `false`, reports duplicate IDs `0`, completes owned-tree cleanup, and builds with warnings/errors `0/0`. `Rules/Block_6.txt` and the existing worked `Examples/E_Block_6.txt` now agree that carrier movement preserves the exact member identity, resources, effects, and histories; no compatibility reader or migration path was added.

### T106 recipient-scoped FullParty resource packet evidence (2026-08-22)

- Honest FullParty RED `TestResults/test-lanes/20260822-150712-496-6320-c2949d0613d34192b7b9cb565d5c5a63-focused/summary.json` is `0/15`: recipient buckets, remote-only authority, global ordinal binding, replay protection, malformed envelopes, and late-mutation rollback were not yet implemented.
- Final FullParty GREEN `TestResults/test-lanes/20260822-152400-579-27904-3d0e82748e5a44138e5234436dfb7361-focused/summary.json` is `18/18`. The adjacent resource-validation control `TestResults/test-lanes/20260822-151835-375-15648-38b67f92d2da4fcba038765f9277962b-focused/summary.json` is `38/38`, and the composer/planner unit control `TestResults/test-lanes/20260822-152024-181-46232-9e3637d36c0b4a51ae5de265756df554-focused/summary.json` is `97/97`. Every cited GREEN exits `0`, times out `false`, reports duplicate IDs `0`, completes owned-tree cleanup, and builds with warnings/errors `0/0`.
- `otherPlayersInteractions` is now a closed object keyed by exact/confusable-unique remote recipient IDs. A resource packet accepts only `resourceChanges`, reuses the ordinary command grammar, binds `player_current` to the recipient rather than the originating client, derives one deterministic local-plus-remote accepted-event ordinal, and never grants the originator local ledger/history/effect authority. Historical packets are shape-validated but not replayed; exact staged bytes participate in the accepted plan before-image so a late packet mutation causes zero mechanics writes.
- The post-slice Fast `TestResults/test-lanes/20260822-152116-027-3616-c54c79636626463fbb60d05ab7058174-fast/summary.json` is `1023/1025`, exit `1`, timeout `false`, duplicate IDs `0`, cleanup complete. Its two official TRX failures are `MathAssistantPromptGuidanceTests.MortalCombatExample_UsesSignedDeltaReference` and `ExplorerModeSourceGuardTests.ExplorerMode_DynamicSelectionPromptChoices_MustBeEscapedBeforeAddChoices`; both belong to the still-open T107-T108 source/documentation cutover and neither exercises T106. No migration or compatibility fallback was added. GM prompt/example synchronization remains explicitly tracked by T107-T111.

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

### T107-T113 breaking-cutover evidence (2026-08-22)

- Exact no-migration rejection is GREEN at `TestResults/test-lanes/20260822-182704-144-15620-9a124004606340c78a62cc2d7e9e020a-focused/summary.json` (`11/11`), covering every removed player, NPC, vehicle, combat, item, and afterlife authority family. The complete resource materialization validation class is `20260822-182753-353-19636-39187355f4524a43b0ee400194ab38c2-focused` (`31/31`). Old technical saves fail closed; no upgrader, legacy parser, dual read/write, or fallback was added.
- Active production/docs guards are GREEN at `TestResults/test-lanes/20260822-182927-802-29708-dfb704dd46d448c9a8c3f4fefd4d3428-focused/summary.json` (`168/168`). Lifecycle/resource validation is `20260822-182953-305-30172-e6efd7b836054387b8740d5639be0c6e-focused` (`36/36`), and the exact canonical resource normalizer class is `20260822-183049-838-8296-564958917fa34833b02ce5b29075d1f8-focused` (`22/22`). Together with the T098-T100 evidence above, these controls preserve exact rollback bytes and prior absence, suppress stale player output, and keep one lease-bound accepted plan through post-validation.
- All three active realm save archives contain the mandatory pristine definition/state/history roots and no removed resource mirrors. Shining/Chaos command-display saves pass `TestResults/test-lanes/20260822-185229-018-27564-ce636013485e48a3949a402bfe70ea01-focused/summary.json` (`154/154`); documentation/example and fixture integrity pass `20260822-185357-884-6104-aeec9afd93ff4664a68a5669c58c3cb8-focused` (`49/49`). The Mortal archive contains `93` entries, Chaos `104`, and Shining `33`; the guarded archive budgets are `104` entries, `339755` expanded bytes, `61171` largest-entry bytes, and `3725` UTF-8 name bytes.
- The final stale-fixture repair controls are `TestResults/test-lanes/20260822-191313-319-31724-75ab396b63ce47dcb4c9a4333e9dacc8-focused/summary.json` (`11/11`), `20260822-193154-260-25396-79f1ed06375b4c29a09570d1cae74237-focused` (`37/37` Guardian policy), and `20260822-193833-254-27524-6b97a7be5c304df48a20fbbf5562a5c0-focused` plus `20260822-194014-680-21272-a63bf25fa90a4e8ca41a43a70bdfe3f7-focused` (broken/fixed validator matrices, both `45/45`). Combat groups now prove stable `members[]` cardinality; Guardian fixtures use only `currentReturnCycleId` and `gachaHistory`.
- Final conditional FullValidation is `TestResults/test-lanes/20260822-194158-015-17568-95c447447c9c42fdb9f19719e3593caf-fullvalidation/summary.json`: `1810/1810`, exit `0`, wall `00:07:05.0804151`, timeout `false`, duplicate IDs `0`, owned-tree cleanup complete, build warnings/errors `0/0`. This replaces the earlier classified RED diagnostics and closes T113 without weakening any validator or preserving compatibility authority.

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
