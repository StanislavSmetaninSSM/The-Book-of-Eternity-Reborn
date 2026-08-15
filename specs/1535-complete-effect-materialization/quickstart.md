# Quickstart: Validate Complete Effect Materialization

**Feature**: [spec.md](spec.md)
**Source issue**: [#1535](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1535)
**Contracts**: [commands/envelope](contracts/effect-command-and-envelope.md), [identity/authority](contracts/effect-identity-source-target-authority.md), [stack/lifecycle](contracts/effect-stacking-and-lifecycle.md), [afterlife](contracts/effect-afterlife-adapter.md), [repair/rollback](contracts/effect-atomic-repair-and-rollback.md), [projection](contracts/effect-player-projection.md)

## 1. Prerequisites

```powershell
Set-Location 'E:\Games\worktrees\boe-1535-effect-materialization'
git status --short --branch
dotnet restore .\BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj
dotnet restore .\BookOfEternityClient.IntegrationTests\BookOfEternityClient.IntegrationTests.csproj
```

Use only local bounded lanes from `docs/testing.md`. GitHub Actions are not needed or enabled.

### Recorded clean baseline

The first local Fast attempt stopped before test execution because the isolated
worktree did not yet contain generated NuGet `project.assets.json` files. After
restoring the two test projects shown above, the unchanged feature base passed:

| Lane | Result directory | Total | Passed | Failed | Duplicates | Timed out | Cleanup | Wall time |
| --- | --- | ---: | ---: | ---: | ---: | --- | --- | --- |
| `Fast` | `TestResults/test-lanes/20260814-155111-688-51532-c209acdb419149cd906a66a3c36a0158-fast` | 3172 | 3172 | 0 | 0 | no | complete | `00:04:56.3252288` |

This is dependency-restoration evidence, not a product regression. Future
Focused and broad controls must still use `scripts/test-csharp.ps1`.

### Task 1 fixture/context evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Shared deterministic fixture | `TestResults/test-lanes/20260814-210058-504-47372-b5dd4f879e17400c88cb885d90c7a3e8-focused` | `TestResults/test-lanes/20260814-210323-432-29280-c4c4f652d51d45a5a114e5cec7d042e6-focused` | 6 |
| File-backed Integration context | `TestResults/test-lanes/20260814-210502-155-22620-230144b9623a420ba278102cb6dead11-focused` | `TestResults/test-lanes/20260814-210622-008-44200-76e97ad1b8894f91aa6d898ce20d779b-focused` | 4 |

The first Integration attempt at
`TestResults/test-lanes/20260814-210434-143-22176-81d6bb932d684926b88b9492ea6ce8e0-focused`
failed before the intended RED because the isolated Integration project lacked
`obj/project.assets.json`. After the prerequisite restore, the repeated run
failed for the intended missing context type and is the RED recorded above.

### Task 2 closed contract evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Envelope, nine profiles, source definitions, fixture control | `TestResults/test-lanes/20260814-211506-098-19088-f1600182c52a4dbaa9304b85ab610865-focused` | `TestResults/test-lanes/20260814-213329-791-14516-ea2ec87d69c34244abc572e63d27a488-focused` | 101 |

The RED was the expected compile failure for the not-yet-created contract
types. The GREEN includes malformed non-finite numeric, unknown nested field,
prose-without-mechanics, exact/confusable definition key, and legacy-carrier
negative controls. All recorded lanes had zero duplicate IDs, no timeout, and
complete owned-tree cleanup.

### Task 3 identity and carrier evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Identity index and owner-carrier authority | `TestResults/test-lanes/20260814-213825-580-54224-e84a066a3091429a871a5042a972bb77-focused` | `TestResults/test-lanes/20260814-214311-977-51088-25c1f82d24804eb8904e1a572b837aee-focused` | 26 |
| Combined foundational control | — | `TestResults/test-lanes/20260814-214356-410-20460-bf20e324700d4ccc900a8843dfb0714a-focused` | 127 |

The identity RED failed on the missing client identity/carrier production API.
GREEN proves random opaque prefixes, closed immutable transitions, active and
terminal agreement, exact/confusable uniqueness, direct-index mutation
rejection, all approved owner families, byte-identical cross-carrier duplicate
rejection, and preservation of adjacent NPC wound/profile state.

### Task 4 exact source, target, combatant, and plan-cache evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Source, target/combatant, and accepted-turn cache authority | `TestResults/test-lanes/20260814-214808-735-19056-ca2223a834824bc9903366e513ed7bfe-focused` | `TestResults/test-lanes/20260814-215457-332-5996-1e7b370e730347d7af3f2cc1ab9c4899-focused` | 48 |
| Same-turn selector hardening | `TestResults/test-lanes/20260814-220355-174-20284-cb940c85a8a24a96a19a2f8f6ca7b3fe-focused` | `TestResults/test-lanes/20260814-220445-223-56300-a835a997224c4f4abd40679daa0498be-focused` | 22 |
| Immutable plan and catalog hardening | `TestResults/test-lanes/20260814-220702-246-52480-89537691dbdc455fa3531fb1551c29f9-focused` | `TestResults/test-lanes/20260814-220738-267-20256-a651fffc9d534be1a88065d1acebcc17-focused` | 55 |
| Combined foundational authority control | — | `TestResults/test-lanes/20260814-220814-083-15604-35e8f01e875947b38cd13d7f5b5211cd-focused` | 176 |
| Foundational phase `Fast` checkpoint | — | `TestResults/test-lanes/20260814-215832-804-30660-300e5200e2064f7bbca598f766eb6058-fast` | 3347 |

The RED was the expected compile failure for the absent authority and plan
types. GREEN proves all eleven source adapters, exact parameter and target-kind
binding, passive/instantaneous non-promotion, exact/confusable/historical and
cross-realm rejection, client-owned combatant IDs, all declared target families,
and a one-instance cache whose fingerprint invalidates on every accepted input
or authority change without deriving permanent IDs from that fingerprint. The
review hardening additionally proves that same-turn targets are addressable only
through one exact non-confusable `targetRef`, and that cached plans, combatant
mappings, and returned source definitions cannot mutate their authority behind
read-only interfaces.

### Task 5 common command surface and pure apply planner evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Complete Mortal apply plan | `TestResults/test-lanes/20260814-221442-928-23352-6c3d2c4e6a234f6c92c8fec7fe45f161-focused` | `TestResults/test-lanes/20260814-222548-108-3708-2d0a52aa9f104e659efc5b047186510e-focused` | 27 |
| Closed StateDistributor command root | `TestResults/test-lanes/20260814-222205-619-18488-c84d2d50fbfa48e487f61ed97f20e75e-focused` | `TestResults/test-lanes/20260814-222240-514-40488-80d7bd8118b2446a8478ab15a88fff7e-focused` | 1 |
| Exact accepted-event binding | `TestResults/test-lanes/20260814-223158-481-29252-c98d73e7d6814d2d84660866cc34217d-focused` | `TestResults/test-lanes/20260814-223318-864-27964-98bd016b6a5145e78f7d4407f6c78cf9-focused` | 31 |
| Cached JSON after-image immutability | `TestResults/test-lanes/20260814-223515-805-22356-c856c1031d644a419250c5ddf4ee0b8c-focused` | `TestResults/test-lanes/20260814-223550-119-46708-98fa3e20173f47d2bd324a48fec24b83-focused` | 1 |
| Multiple distinct accepted events | `TestResults/test-lanes/20260814-223937-577-40996-ecd5710492de4afab9ae09e1d5732fac-focused` | `TestResults/test-lanes/20260814-224059-722-52852-7b4f96066a5d44efba32fe43d803f8e0-focused` | 1 |
| Combined foundational Task 5 control | — | `TestResults/test-lanes/20260814-224221-441-33532-ad147e4676194f568d6601290d05a6ec-focused` | 211 |

The planner RED failed only on the absent after-image/input API. GREEN proves a
complete source-parameterized player, NPC, buff, and debuff instance; exact
carrier and identity after-images; adjacent-state and raw-input preservation;
client post-state and legacy-route rejection before allocation; cache
invalidation for carrier/index changes; exact command-to-accepted-event binding;
duplicate-event rejection before identity allocation; immutable cached JSON
after-images; multiple distinct event applications with distinct transition
evidence; and command deletion in touched paths.
The staging RED isolated generic `_lastUpdated` metadata; the narrow GREEN keeps
all canonical-file timestamps while leaving only the two closed transient effect
arrays in `effect_commands.json`.

### Task 6 raw validation and canonical publication evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Explicit accepted-plan exports; no raw sibling or publication fallback | `TestResults/test-lanes/20260815-004112-462-55940-a47a252ce01a479984366d67c66a8ae9-focused` | `TestResults/test-lanes/20260815-004433-206-18488-319db491335f4be6b048a58585315b86-focused` | 2 |
| Canonical publication and command consumption | — | `TestResults/test-lanes/20260815-005735-444-13396-c11397c44c164f36ba6eccd9ae107339-focused` | 8 |
| Raw/composed validation and same-turn owner adapters | — | `TestResults/test-lanes/20260815-005822-150-51972-18f2208b4f1c4449b6e8531dd75fe95d-focused` | 37 |
| Source, planner, cache, and item authority control | — | `TestResults/test-lanes/20260815-005910-154-41368-c3617723ad10477bb871447b0e6a329e-focused` | 155 |
| Validation-phase flag capacity | `TestResults/test-lanes/20260815-010449-286-52284-76c278ebe12d459d8d24b967fb6466a8-focused` | `TestResults/test-lanes/20260815-010554-687-48404-9d0a13b5632a41318751f496cbd2a966-focused` | 28 |
| Malformed-present authority (`empty`, whitespace, late JSON `null`) | `TestResults/test-lanes/20260815-071348-161-42140-051c63f99d29462ba153d7901e2d3c2f-focused`; `TestResults/test-lanes/20260815-072316-498-44252-aa2b0e6493474859a1ac9f377c9db4da-focused` | `TestResults/test-lanes/20260815-072900-141-42364-25379c74eed94acd94e8b71fb204f5f0-focused` | 12 |
| Optional receipts-only root is a consumed no-op | `TestResults/test-lanes/20260815-072624-761-30956-e776b91f32964fd8aa2a12da2a2d21bb-focused` | `TestResults/test-lanes/20260815-072816-752-56376-6495f464a1944aed9e99b2a822fe10af-focused` | 1 |
| Accepted turn/event handoff rejects late turn mutation | `TestResults/test-lanes/20260815-073651-639-48964-3881995b5d874c2b96408b2674aaa017-focused` | `TestResults/test-lanes/20260815-073831-615-37716-46bd35213a4d4b628329392afa448fbf-focused` | 1 |
| Final Task 6 Integration control | — | `TestResults/test-lanes/20260815-074014-231-22712-9823ff35f5494c9c8153666f20cd0e3a-focused` | 154 |
| Final Task 6 unit control | — | `TestResults/test-lanes/20260815-074207-479-21552-3795b00a2e544763b5df8431db431a22-focused` | 205 |

Same-turn item and location sources use `sourceRef` and are resolved only from
their accepted plan exports. Stable-ID owners are validated first and exported
through a closed adapter. Canonical publication consumes the exact plan cached
by raw validation; it cannot rebuild authority after another normalizer changes
the files.

Adding the effect phase occupied the last `uint` flag. The phase enum now uses
`ulong`, preserving a real unknown-bit fail-closed guard without changing the
defined phase order or selection semantics.

### Task 7 accepted mechanics snapshot evidence

| Boundary | RED result | GREEN result | Green tests |
| --- | --- | --- | ---: |
| Immutable all-or-nothing snapshot API | `TestResults/test-lanes/20260815-075556-717-55648-0eb11b14e88a4ab78052e9154854e295-focused` | `TestResults/test-lanes/20260815-081542-563-22808-f1b701fd4663495286672d2744a9036a-focused` | 34 |
| Exact Block 5 characteristic catalog | `TestResults/test-lanes/20260815-080459-052-38868-21d39b90a60b4dc8b46f874654563ea7-focused` | `TestResults/test-lanes/20260815-091318-036-3324-2a942f7254b34b31910fc2775760e811-focused` | 54 |
| Aggregate percentage/cap/floor semantics | `TestResults/test-lanes/20260815-080918-525-38028-920b7632a75c417b962340bafee05723-focused` | `TestResults/test-lanes/20260815-081542-563-22808-f1b701fd4663495286672d2744a9036a-focused` | 34 |
| Hidden effects apply without player-safe audit disclosure | `TestResults/test-lanes/20260815-081302-428-38600-50da44b6b81d4a5aa156fd7dd1b44079-focused` | `TestResults/test-lanes/20260815-081542-563-22808-f1b701fd4663495286672d2744a9036a-focused` | 34 |
| Wrong-typed governed sibling carrier rejects the whole snapshot | `TestResults/test-lanes/20260815-084903-761-12700-30ee4fa1adb84d80ae8ebd88379d0279-focused` | `TestResults/test-lanes/20260815-091318-036-3324-2a942f7254b34b31910fc2775760e811-focused` | 54 |
| Finite out-of-Int32 characteristic aggregate fails closed | `TestResults/test-lanes/20260815-085718-263-3324-0b185988f2b24baf8f37acc94d97df86-focused` | `TestResults/test-lanes/20260815-091318-036-3324-2a942f7254b34b31910fc2775760e811-focused` | 54 |
| Hidden characteristic mechanics stay internal while direct console/shared browser projections remain safe | `TestResults/test-lanes/20260815-085304-691-43656-3ef512d044de4106996d4ef7fdff4105-focused`; `TestResults/test-lanes/20260815-090146-237-36928-fce2b363fd0a4555b1ce6a6642c8c1e4-focused` | `TestResults/test-lanes/20260815-090238-686-55632-b7f488b307eb416dab47fc87262b9d1b-focused`; `TestResults/test-lanes/20260815-090345-811-15484-17fd42ff51254fa2bcf9f507ed50ec3d-focused` | 2 |
| Missing/wrong-type player-safe computed map never falls back to internal values | `TestResults/test-lanes/20260815-090747-882-35720-4cda4c16cc614c6bb586ac81134420e1-focused` | `TestResults/test-lanes/20260815-090838-664-49496-3ad05329191a4bea9c5b1199019394a5-focused` | 3 |
| Unrelated normalization is a no-op, commandless combatant refs still publish, and a published validated plan is consumed | `TestResults/test-lanes/20260815-082252-230-38628-f1a5c5bf2ee94423ba39949cc5659838-focused`; `TestResults/test-lanes/20260815-091945-981-41912-3028ded2fc8e4b18a53cda46b4df66b8-focused` | `TestResults/test-lanes/20260815-082449-941-20056-37a2b8a317da444a8595a1a0d380948f-focused`; `TestResults/test-lanes/20260815-082602-698-39028-64f6879400a34df7b2ccfefd7ada4ac4-focused`; `TestResults/test-lanes/20260815-092031-448-47408-9620e1d76b284a6b8d81bd4634ead2fa-focused` | 4 |
| Reserved future pending-effect path is explicitly inventoried, not activated | `TestResults/test-lanes/20260815-082636-432-51948-005858e21a264ab8b8a60a46698e7766-fast` | `TestResults/test-lanes/20260815-082903-661-32512-b1a2f1a96df740239dd15f40d8e2f5bf-focused` | 1 |
| Final Task 7 normalizer control | — | `TestResults/test-lanes/20260815-092133-666-27872-47f720acd7a24ce38c301d7abbe1926d-focused` | 27 |
| Final Task 7 Fast control | — | `TestResults/test-lanes/20260815-092308-521-57072-f1333ebcd02045bdaab0d3deadefd2ed-fast` | 3441 |

`EffectMechanicsSnapshot` reads all six canonical carriers plus the identity
index under one publication-read lease. Any malformed carrier, duplicate,
component/profile error, target mismatch, or carrier/index disagreement rejects
the whole snapshot and contributes zero active-effect mechanics. Static source
definitions remain authority only and are not counted as active instances.
The current `CharacteristicsService` consumes only exact player
`characteristic_modifier` components, using the twelve-value Block 5 catalog,
source-value caps, summed flat/percentage values, and one final floor. Hidden
effects retain internal GM/QTE mechanics but never enter player-safe totals,
attribution, direct console stats, or the shared console/browser stats DTO. A
missing/malformed safe map fails closed to base/permanent values; legacy internal
computed maps are not treated as a compatibility projection.

## 2. Contract and Identity Control

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~EffectMaterializationContractTests|FullyQualifiedName~EffectIdentityStateTests|FullyQualifiedName~EffectSourceAuthorityTests|FullyQualifiedName~EffectTargetAuthorityTests"
```

Expected:

- complete envelope, registered component, lifetime, stack, removal, source, and target cases pass;
- unknown/duplicate/wrong-type fields fail;
- exact/case/whitespace/Unicode-confusable/historical identities are distinguished;
- the GM cannot author effect/transition/index/receipt identity;
- static source definitions do not become active instances merely by existing.

## 3. Valid Mortal Buff

Precondition: an accepted skill contains a source-authorized `activeEffectDefinitions[]` entry for a three-turn characteristic modifier.

GM response excerpt:

```json
{
  "effectChanges": [
    {
      "operation": "apply",
      "target": { "kind": "player", "targetId": "player_current" },
      "source": {
        "kind": "skill",
        "sourceId": "skill_exact",
        "definitionKey": "battle-focus"
      },
      "parameters": {},
      "eventRef": { "kind": "accepted_turn", "authorityId": "turn_exact" },
      "reason": "Герой сосредоточился перед ударом."
    }
  ]
}
```

When one response contains multiple `effectChanges[]`, the first entry uses
`turn_<turn>` and each later entry uses its exact one-based position, such as
`turn_42_effect_2`. This is accepted-turn evidence only; the client still owns
the unique durable transition and event history.

If the skill itself is accepted in the same turn and already has a stable
effective ID, its owning validator must pass before the effect adapter exports
that ID. A newly materialized item or location instead uses `sourceRef` in the
raw command; the canonical effect still stores only the resolved `sourceId`.

Expected:

- the client creates one opaque effect ID and one create transition;
- player carrier and identity index agree;
- the characteristic bonus appears only through the accepted mechanics snapshot;
- the raw command file is absent after normalization;
- repeating the same accepted event produces no second buff.

## 4. Missing Authority Is Rejected

Try the same command without `source`, with `targetId` changed only by case, or with `duration: 999`/description-only mechanics.

Expected:

- raw validation reports a bounded exact issue;
- no carrier, index, characteristic, narrative, or interface output changes;
- identity/target/persistence errors produce no actionable retargeting repair.

## 5. Stack, Refresh, Replace, and Merge

Run source fixtures for each policy:

1. `independent`: with source `maxStacks=2`, three distinct accepted events create exactly two IDs; each canonical instance remains `currentStacks=1,maxStacks=1`, and replay creates none.
2. `stack`: count rises to `maxStacks`; boundary application follows source max behavior.
3. `refresh`: same ID and count; exact lifetime reset/extend behavior.
4. `replace`: old index entry becomes terminal `replaced`; one new active ID.
5. `merge`: only registered bounded component fields combine deterministically, and every component profile must authorize the selected reducer.

Expected: the GM never submits post-count, remaining lifetime, replacement ID, or merged payload.

## 6. Lifetime Boundaries

Exercise:

- turns: 1 → expiry after the declared owner phase;
- uses: 1 → governed trigger consumption → expiry;
- exact time: source `duration=30` plus accepted `world_time.currentTimeInMinutes=120` creates numeric deadline `150`; exact same-turn `setWorldTime.currentTimeInMinutes=300` instead creates `330`; an override without that exact numeric value cannot reuse the retained `120`; deadline equality expires it;
- scene: exact scene closure;
- source-bound: source lost with both `suspend` and `expire` policies;
- condition-bound: registered condition turns false;
- permanent: accepted only from explicitly authorized source;
- manual: accepted only with non-empty registered removal authority.

Expected: each event advances exactly once; numeric/text persistence sentinels fail.

This slice intentionally stops before trigger execution and bounded receipts.
`component_response`, periodic/event triggers, and
`pending_effect_resolutions.json` remain owned by T042–T043/T047/T049–T050 and
must fail closed rather than being approximated by the stack/lifetime reducer.
The pure scheduler already covers all eight lifetime reducers. The production
Mortal adapter in this slice supplies owner-turn and exact world-time events;
uses, scene, and condition advancement waits for its owning exact trigger or
scene/condition adapter. `profile_specific` merge execution is likewise kept in
the later registered component phase.

## 7. Periodic and Triggered Ordering

Create multiple effects with due components at the same phase and declared priorities. Replay the accepted turn and reorder the physical carrier array before validation.

Expected:

- execution remains priority → ordinal effect ID → ordinal component ID;
- periodic resource changes occur once;
- carrier-array order cannot change the result;
- a downstream-effect cycle or expansion beyond the source limit fails before writes.

## 8. NPC and Combatant Targets

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~EffectMaterializationValidationTests|FullyQualifiedName~CanonicalStateNormalizerEffectTests"
```

Expected:

- exact named NPC effect appears in its one NPC carrier entry;
- a new anonymous combatant exposes an exact same-turn `combatantRef`, receives a client-owned stable combat-local ID once, and the canonical effect stores only that permanent target;
- non-empty raw `activeBuffs`/`activeDebuffs` without `effectChanges[]` fail;
- case/name/index targeting fails;
- the same effect cannot exist in both buff and debuff arrays.

## 9. Afterlife Actor and Spiritual Condition

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~AfterlifeSpiritualConflictValidationTests|FullyQualifiedName~AfterlifeEntityProfileValidationTests|FullyQualifiedName~EffectAfterlifeAdapterTests"
```

Expected:

- persistent Guardian/resident/radiant actor effect lives only in the accepted afterlife profile;
- a `mark`, `ward`, `burden`, `opening`, or `vow` keeps legal axes/counterplay while receiving common identity/history;
- direct `combatConditions[]` creation is rejected;
- exchange replay does not apply or consume twice;
- hidden/GM-only condition tokens are absent from console/browser output;
- Shining blessing entitlement cannot be authored as an active effect.

## 10. Wound Independence

Apply a wound-derived source-bound consequence, then:

1. dispel only the effect;
2. remove only the effect;
3. let it expire;
4. perform an independently accepted wound treatment that ends its source binding.

Expected:

- steps 1–3 leave every wound field and wound existence unchanged;
- step 4 changes wound state only through the wound transition and changes linked effects only through declared source-loss behavior;
- missing/confusable wound ID cannot be repaired by retargeting.

## 11. All-or-Nothing Mechanics

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~CharacteristicsServiceTests|FullyQualifiedName~EffectMechanicsSnapshotTests"
```

Expected:

- valid complete sets apply all registered modifiers;
- one malformed sibling invalidates the whole active set for mechanics;
- legacy aliases and prose fields are not read as mechanical authority;
- static passive `structuredBonuses` remain source-owned and are not double-counted as active effects.

## 12. Bounded Resolution and Repair

Use one registered story-facing trigger that requires a bounded receipt and one malformed apply command with only a repairable readable omission.

Expected:

- deterministic bounds and exact effect/source/target/event appear in operator-only pending/repair context;
- a complete exact receipt is consumed once;
- stale, partial, cross-target, or out-of-bound receipt fails closed;
- baseline is restored before repair dispatch;
- ready-only or effect-only partial retry cannot accept a no-op or discard unrelated turn changes;
- identity/source/target/stack/cycle errors dispatch nothing.

## 13. Failure-Injection Rollback

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane LifecycleIntegration -Filter "FullyQualifiedName~EffectMaterializationLifecycleTests"
```

Expected for every planned path and post-check injection:

- all pre-existing files match baseline bytes;
- all rejected-attempt new files are absent;
- deleted baseline files are restored;
- no stale narrative/interface output survives;
- operator diagnostic survives caller-owned rollback;
- player output contains no exception, path, validation, materialization, repair, rollback, backup, harness, bridge, or agent vocabulary.

## 14. Console/Browser Projection Parity

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExplorerModeCommandTests|FullyQualifiedName~ExplorerWebCommandServiceTests|FullyQualifiedName~EffectPlayerProjectionTests"
```

Expected:

- visible name, description, source label, mechanics, stacks, lifetime, and actions agree;
- hidden/GM-only entries are absent from rows/counts/actions/audits/game-screen DTOs;
- full identity/index/pending/repair/diagnostic wrappers are suppressed recursively;
- adjacent legitimate `{kind,title,steps,turn,source,route}` semantics remain visible;
- stale/forged cure/dispel selector cannot mutate state;
- effect-only removal never claims wound healing.

## 15. Documentation and Examples

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -Filter "FullyQualifiedName~PromptDocumentationCoverageTests|FullyQualifiedName~AfterlifeDocumentationCoverageTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Focused -FocusedProject Integration -Filter "FullyQualifiedName~ExampleDocumentationValidationTests"
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane FullValidation
```

Expected:

- Mortal and afterlife GM entrypoints require the current constructor guide;
- manifest and worked examples cover every required profile/lifecycle operation;
- old application routes and `duration: 999` positive examples are gone;
- explicit malformed examples remain labeled negative and prove bounded/fail-closed behavior;
- no runtime migration or compatibility promise exists.

## 16. Performance Control

Run the focused scaling fixture with representative owners, effects, components, and triggers at size N and 2N.

Expected:

- 2N validation/lifecycle work is at most 2.5 × N;
- source and target catalogs are built once per plan;
- no full-catalog scan occurs inside each effect or trigger loop.

## 17. Broad Local Controls

At the meaningful implementation checkpoint:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane Fast
```

Before integration, after focused, documentation, and lifecycle controls are green:

```powershell
pwsh -NoProfile -File .\scripts\test-csharp.ps1 -Lane PreMerge
```

Do not run another Fast immediately before PreMerge; PreMerge already includes the fast project. Record result directory, total/passed/failed/duplicate counts, timeout state, cleanup state, and wall time.

## 18. Manual Player Check

In one generated Mortal session and one afterlife conflict:

1. Inspect visible, stacked, periodic, source-bound, suspended, expiring, and dispellable effects in console and browser.
2. Confirm in-world Russian terminology and equal action eligibility.
3. Reject a malformed effect and confirm no stale narrative.
4. Remove a wound consequence and confirm the wound remains.
5. Search serialized browser state and console output for internal IDs, paths, validation/repair words, pending packets, and hidden effect tokens; expect zero matches.
