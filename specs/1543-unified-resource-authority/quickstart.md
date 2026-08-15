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
