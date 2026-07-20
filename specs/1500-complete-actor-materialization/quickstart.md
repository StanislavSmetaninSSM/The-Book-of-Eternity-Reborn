# Quickstart: Complete Actor Materialization

## Purpose

Use this checklist when implementing or reviewing issue #1500. It is not a player guide.

## Mortal valid first materialization

1. Build a complete first NPC object using existing `NPCsInScene`/new-NPC authority.
2. Keep all existing core fields and arrays.
3. Add `materialization` bound to `mortal_npc` and the exact permanent `NPCId` or same-turn `initialId`.
4. Declare every Mortal governed section.
5. Make each capability agree with canonical skill, teacher, trade, and inventory data.
6. Do not infer anything from setting vocabulary.
7. Resolve one effective identity; a same-turn `initialId` must not collide with any validated pre-turn permanent `NPCId`, and true legacy inventory promotion must repeat the unchanged snapshot while mutations use atomic inventory commands.
8. Reject duplicate members in current actor/inventory/materialization data and validated pre-turn actor/inventory authority before semantic comparison; property order alone remains irrelevant.

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

- Route afterlife memory repair by actor type and preserve every field outside the exact actor-owned memory target.
- Keep `npc_initial_id_collides_with_existing_permanent_id` on the main-GM rollback/repair path.
- Dispatch `npc_existing_inventory_resend_forbidden` only when `expected` is the exact validated pre-turn JSON-array snapshot; restore only that actor/carrier field. Ordinary existing resends stay main-GM-only.
- For `npc_characteristics_empty`, allow only a non-empty numeric object on the exact actor/carrier field. Reject sibling, other-actor, root, add, and delete changes.
- Require explicit proposal `status`; omission/`Unspecified` is invalid, only explicit `completed` may apply, and terminal statuses use empty `changedFiles`.
- Generate every worker audit event ID through the shared UTC-millisecond plus GUID utility; source guards reject hand-built prefixes.

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
- Inventory repair packet covers genuinely new, ordinary existing, and true legacy-promotion cases: remove the whole ordinary-existing full-object resend and use dedicated deltas/main-GM fallback, while retaining required promotion inventory.
- Mortal and afterlife prompts/docs/examples/manifests synchronized.
- Player-facing metadata non-leakage covered.
