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

1. `independent`: two distinct accepted events create two IDs; replay creates none.
2. `stack`: count rises to `maxStacks`; boundary application follows source max behavior.
3. `refresh`: same ID and count; exact lifetime reset/extend behavior.
4. `replace`: old index entry becomes terminal `replaced`; one new active ID.
5. `merge`: only registered bounded component fields combine deterministically.

Expected: the GM never submits post-count, remaining lifetime, replacement ID, or merged payload.

## 6. Lifetime Boundaries

Exercise:

- turns: 1 → expiry after the declared owner phase;
- uses: 1 → governed trigger consumption → expiry;
- exact time: deadline equal to accepted current time;
- scene: exact scene closure;
- source-bound: source lost with both `suspend` and `expire` policies;
- condition-bound: registered condition turns false;
- permanent: accepted only from explicitly authorized source;
- manual: accepted only with non-empty registered removal authority.

Expected: each event advances exactly once; numeric/text persistence sentinels fail.

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
