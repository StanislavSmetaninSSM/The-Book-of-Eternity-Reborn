# Guardian Policy Next Session Handoff

## Статус

`OPEN REMAINING SEAMS`

Текущий baseline после matrix rollout и cleanup:

- full suite: `1019/1019`
- owner-state matrix layer уже стоит в `GuardianSystemRegressionTests`
- `Guardian_Policy_Convergence_Audit.md` в целом синхронизирован с landed remediation, но ещё не учитывает два remaining seam-а ниже

## Что осталось

### 1. `soul_state.json` mixed-key lifecycle parity ещё не закрыт

Проблема:

- strict `AfterlifeResidents` / rival-bonus-clue `soul_state` paths всё ещё используют custom reader в `ValidationService.GuardianAndRivalCrossRefs.cs`
- внутри `TryReadCurrentSoulRelicResidentValidationDocumentAsync(...)` top-level contract до сих пор опирается на `HasAnyAllowedVisibleTopLevelKey(...)`
- это fail-open: current `soul_state.json` вида
  - `{"currentIncarnation":3,"soulRelics":[],"foo":[]}`
  всё ещё считается usable authority state

Почему это проблема:

- lifecycle validation already treats extra visible top-level keys as invalid
- guardian-policy reader должен совпадать с lifecycle strictness для policy-sensitive current state

Нужный outcome:

- любой visible non-underscore top-level key вне `SoulStateResidentValidationAllowedTopLevelKeys` должен давать `ContractInvalidTopLevel`
- при этом нельзя ломать текущую special-case semantics для `soulRelics`:
  - array stays readable
  - object with canonical `equipped` / `stored` arrays stays readable/partial
  - invalid `soulRelics` shape stays `InvalidCollectionShape`

### 2. manifested-companion `npc_core.json` reader всё ещё шире lifecycle contract

Проблема:

- manifested-companion owner-state path использует `ManifestedCompanionNpcValidationAllowedTopLevelKeys`
- сейчас этот set включает:
  - `UpdateNPCs`
  - `NPCsInScene`
  - `NPCs`
  - `npcs`
  - `npcDataChanges`
- но lifecycle `ValidateNpcFile("game_state/npcs/npc_core.json", ...)` разрешает только:
  - `UpdateNPCs`
  - `NPCsRenameData`
  - `NPCsInScene`

Почему это проблема:

- lifecycle-invalid current `npc_core.json` может считаться clean authority state inside `AfterlifeResidents`
- это reopening lifecycle-vs-guardian mismatch

Нужный outcome:

- owner-state validation for manifested-companion path must use the lifecycle-approved top-level contract for `npc_core.json`
- lifecycle-invalid alias sections (`NPCs`, `npcs`, `npcDataChanges`) must not be treated as clean current authority state
- if dependency appears only under those lifecycle-invalid aliases, expected result is local `afterlife_resident_invalid_current_npc_state`
- missing current `npc_core.json` without real manifested-companion dependency should remain permissive

## Matrix Gap

Нужно добавить rows, которых сейчас нет:

- policy-sensitive `soul_state`:
  - `mixed_valid_and_unsupported_top_level`
  - example: `{"currentIncarnation":3,"soulRelics":[],"foo":[]}`
- manifested-companion NPC:
  - lifecycle-invalid alias with dependency
  - lifecycle-invalid alias without dependency

Expected matrix behavior:

- strict resident/relic `soul_state` path => `afterlife_resident_invalid_current_soul_state`
- strict rival bonus-clue `soul_state` path => `rival_arc_bonus_clue_invalid_current_soul_state`
- dormant bonus-clue path => still permissive
- manifested-companion dependency under lifecycle-invalid NPC alias => `afterlife_resident_invalid_current_npc_state`
- same alias without dependency => no local NPC owner issue

## Implementation Plan

### A. Fix custom `soul_state` top-level classification

В `ValidationService.GuardianAndRivalCrossRefs.cs`:

- inside `TryReadCurrentSoulRelicResidentValidationDocumentAsync(...)`
- replace the current `HasAnyAllowedVisibleTopLevelKey(...)` gate
- new rule:
  - collect visible non-underscore top-level keys
  - if any key is outside `SoulStateResidentValidationAllowedTopLevelKeys`, return `ContractInvalidTopLevel`
- keep the existing downstream `soulRelics` shape logic untouched

### B. Align manifested-companion NPC owner-state contract with lifecycle

В `ValidationService.GuardianAndRivalCrossRefs.cs`:

- narrow `ManifestedCompanionNpcValidationAllowedTopLevelKeys` to the lifecycle-approved `npc_core.json` contract
- do not treat `NPCs`, `npcs`, `npcDataChanges` as clean authority state for this owner reader
- preserve current branch-local dependency semantics:
  - malformed / non-object / contract-invalid / invalid-shape + real dependency => local NPC owner issue
  - no real dependency => permissive, no false-positive local NPC issue

Recommended default:

- use lifecycle-approved top-level keys as the owner-state contract
- if later it turns out `NPCsRenameData` cannot carry manifested-companion dependency, it may stay allowed for validity while not participating in dependency scanning

### C. Extend matrix tests

В `GuardianSystemRegressionTests.cs`:

- add mixed-key `soul_state` rows to:
  - `RivalBonusClueCurrentSoulStateMatrixCases`
  - `ResidentRelicCurrentSoulStateMatrixCases`
  - `InvalidRivalBonusClueCurrentSoulStateCases`
  - `InvalidResidentRelicCurrentSoulStateCases`
- add lifecycle-invalid NPC alias rows to:
  - `InvalidManifestedCompanionNpcCurrentStateCases`
  - `InvalidNonManifestedNpcCurrentStateCases`

## Verification

Run at minimum:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:UseSharedCompilation=false --filter "FullyQualifiedName~ValidateGameState_AfterlifeResidents_|FullyQualifiedName~ValidateGameState_RivalBonusClue_"
```

Then full suite:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --no-build
```

## After Landing

- update `OtherGuides/Guardian_Policy_Convergence_Audit.md`
- explicitly mention:
  - mixed valid+unsupported top-level `soul_state` is now owner-failure on strict paths
  - manifested-companion NPC owner-state contract now matches lifecycle `npc_core.json`
