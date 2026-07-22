# Quickstart: Complete Actor Materialization

## Purpose

Use this checklist when implementing or reviewing issue #1500. It is not a player guide.

## Mortal valid first materialization

1. Build a complete first NPC object using existing `NPCsInScene`/new-NPC authority.
2. Keep all existing core fields and arrays.
3. Add `materialization` bound to `mortal_npc` and the exact permanent `NPCId` or same-turn `initialId`.
4. Declare every Mortal governed section.
5. Provide 3-5 `personalityTraits`; every trait includes integer `value` from 1 through 10.
6. Make each capability agree with canonical skill, teacher, trade, and inventory data.
7. Do not infer anything from setting vocabulary. Characteristic keys, progression grants, and carrying formulas come only from explicit current-world authority; nullable carrying results stay null when that authority is absent.
8. Resolve one effective identity; a same-turn `initialId` must not collide with any validated pre-turn permanent `NPCId`, and true legacy inventory promotion must repeat the unchanged snapshot while mutations use atomic inventory commands.
9. Reject duplicate members in current actor/inventory/materialization data and validated pre-turn actor/inventory authority before semantic comparison; property order alone remains irrelevant.

## Ordinary existing Mortal NPC

1. Do not resend a complete or unchanged ordinary-existing object through `UpdateNPCs`.
2. Keep historical `NPCsInScene` only as retained scene state for a physically present actor; every actor-owned field remains semantically unchanged there and in envelope-free `UpdateNPCs`.
3. Use the exact dedicated command for rename, unlock, inventory/equipment, skills/mastery, relationships/locks, journals/memory, goals/quests, activities, and masks.
4. Use `NPCCoreChanges` only for its closed profile, paired location, coherent progression, setting-owned characteristic results, faction-affiliation upsert, and locked/unrealized Fate Card definition add/remove groups.
5. Target one exact existing permanent `NPCId`, include a non-empty `reason`, and send only absolute resulting values. Invalid commands remain unconsumed for repair.
6. Keep `npc_core.json` valid and duplicate-free. New Fate Cards pass the full production nested skill/Combat Action contract before reduction.
7. Treat complete Block 7 active/passive skills as combat evidence even when optional `skillId`/`id` fields are absent.

## Afterlife valid first materialization

1. Create the actor's type-specific canonical record when required.
2. Create exactly one matching common profile in `game_state/meta/afterlife_entity_profiles.json`.
3. Add an envelope bound to exact actor type and ID.
4. Declare all afterlife governed sections and initialize actor-owned memory.
5. Cross-check combat, mentor, and trade capabilities against their existing realm-specific authority.
6. Treat the first envelope on an existing profile as a memory-validation transition: Guardian/resident journals are dedicated authority; Radiant/Saref/common actors use only the exact profile `gmThoughtsSummary`.

## Legacy rule

Do not modify or fabricate untouched actors merely because they lack an envelope. Require the current contract only when the accepted turn creates or promotes the actor, or when an existing envelope is malformed.

## Worker repair protocol

- Run each worker in a detached `.worker_runtime` execution snapshot containing only pinned task context. Import only the validated proposal and its declared `contentRef` bytes; direct snapshot edits and undeclared artifacts are discarded.
- Route afterlife memory repair by actor type and preserve every field outside the exact actor-owned memory target.
- Keep `npc_initial_id_collides_with_existing_permanent_id` on the main-GM rollback/repair path.
- Dispatch `npc_existing_inventory_resend_forbidden` only when `expected` is the exact validated pre-turn JSON-array snapshot; restore only that actor/carrier field. Ordinary existing resends stay main-GM-only.
- For `npc_characteristics_empty`, hash-pin `game_state/misc/characteristics.json` as read-only context and allow only finite numeric keys present in that authority on the exact actor/carrier field. Missing or malformed authority and sibling, other-actor, root, add, or delete changes reject.
- Treat JSON exponent overflow such as `1e9999` as non-finite and invalid even though the token has number syntax.
- Treat every `game_state/meta/` repair target as afterlife-scoped. Hash-pin `game_state/meta/soul_state.json` as read-only realm authority, derive the contract from its strict duplicate-free `currentRealm`, exclude it from proposal paths, and fail mixed Mortal/meta task construction closed.
- Hold one canonical write lease from final context/authority verification through every write, full validation, read-only context recheck, rollback, and decision.
- Require every changed-file entry to declare exactly `Add`, `Replace`, or `Delete`; omission, `Unspecified`, zero, and undefined values reject before apply semantics.
- Require explicit proposal `status`; omission/`Unspecified` is invalid, only explicit `completed` may apply, and terminal statuses use empty `changedFiles`.
- Generate every worker audit event ID through the shared UTC-millisecond plus GUID utility; source guards reject hand-built prefixes.
- Retain the original canonical target set through output-only retries. Treat player-facing output as fresh only when it is strictly newer than every target's latest actual write; equality and output followed by another target rewrite are stale.

## Red/green verification loop

```powershell
$env:SPECIFY_FEATURE='1500-complete-actor-materialization'
$env:SPECIFY_FEATURE_DIRECTORY='specs/1500-complete-actor-materialization'

dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ActorMaterialization"
```

Start with one failing test per invariant. Implement the smallest reusable rule, then expand to cross-file and repair behavior.

## Documentation-sensitive verification

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests|PromptDocumentationCoverageTests|ValidationSourceGuardTests|GameEngineSourceGuardTests"
```

## Completion checklist

- New Mortal actor rejection/acceptance covered.
- New Guardian, resident, radiant actor, Saref actor, and Shining head binding covered.
- Untouched legacy actor load covered.
- Promotion-to-significance covered.
- System Guardian deterministic seed covered.
- Normalizer preservation and no-invention covered.
- Bounded repair packet covered.
- Generated Mortal template uses current-world characteristic authority and no universal list.
- First-materialization Mortal personality uses 3-5 traits with mandatory integer values 1-10 and the worked fragment enters the production response validator.
- Ordinary existing profile/location/progression/setting-owned characteristic/faction/Fate Card definition mutations use `NPCCoreChanges`; complete carriers remain creation/true-promotion only.
- Authoritative `NPCCoreChanges` templates omit unused groups and pass the production command path as written.
- Inventory repair packet covers genuinely new, ordinary existing, and true legacy-promotion cases: remove the whole ordinary-existing full-object resend and use dedicated deltas, bounded `NPCCoreChanges`, or main-GM fallback for protected unsupported domains, while retaining required promotion inventory.
- Mortal and afterlife prompts/docs/examples/manifests synchronized.
- Third re-review docs explicitly record Mortal-only scope and no Chaos Sea/Shining Abode contract change.
- Fourth re-review guards malformed/duplicate NPCCore authority, every historical actor-owned domain, production Fate Card reduction atomicity, and ID-less canonical skill evidence.
- Shared Block 5 effect `value` enforcement is synchronized with the afterlife matrix/example without changing the separate special-art schema.
- Afterlife worker repair pins exact Soul realm authority without granting write scope; worker changed-file operations are explicit; characteristic overflow and retained output-freshness chains are guarded.
- Player-facing metadata non-leakage covered.
