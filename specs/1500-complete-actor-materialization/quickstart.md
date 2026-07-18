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

## Afterlife valid first materialization

1. Create the actor's type-specific canonical record when required.
2. Create exactly one matching common profile in `game_state/meta/afterlife_entity_profiles.json`.
3. Add an envelope bound to exact actor type and ID.
4. Declare all afterlife governed sections and initialize actor-owned memory.
5. Cross-check combat, mentor, and trade capabilities against their existing realm-specific authority.

## Legacy rule

Do not modify or fabricate untouched actors merely because they lack an envelope. Require the current contract only when the accepted turn creates or promotes the actor, or when an existing envelope is malformed.

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
- Mortal and afterlife prompts/docs/examples/manifests synchronized.
- Player-facing metadata non-leakage covered.
