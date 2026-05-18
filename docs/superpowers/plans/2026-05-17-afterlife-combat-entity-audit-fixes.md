# Afterlife Combat And Entity Audit Fixes Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Fix the remaining audit defects in afterlife entity progression and make the afterlife action-point economy internally consistent.

**Architecture:** Keep deterministic afterlife entity progression in `AfterlifeEntityProfileState`, but make accounting canonical: signed GM override deltas are applied to state and split into non-negative ledger `income`/`spending`. Process scheduler reports per afterlife contour instead of collapsing them into a single global Shining/Chaos cycle. Treat strategy restrictions as executable rules, not display-only metadata. Extend spiritual-combat action-cost validation so opposition OD is either enforced symmetrically or the contract is explicitly narrowed.

**Tech Stack:** .NET 8, C#, `System.Text.Json.Nodes`, xUnit, PowerShell, existing validation/documentation coverage tests.

---

## File Map

- Modify `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`: progression override ledger accounting, per-profile/per-contour auto-progression cycle selection, strategy reserve/allow/forbid enforcement.
- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`: strengthen strategy metadata validation if needed and add regression checks for canonical ledger shape.
- Modify `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`: add opposition action-cost audit validation or explicitly reject unsupported opposition OD mutations.
- Modify `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`: preserve/project `actionCostAudit.opposition` if symmetrical opposition OD is implemented.
- Modify `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`: display opposition OD audit in `/spiritual_combat_log` and `/spiritual_combat_help` if the contract is expanded.
- Modify docs when runtime contracts change:
  - `OtherGuides/Afterlife_Contract_Matrix.md`
  - `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`
  - `Examples/E_CLI_Afterlife_Turns.txt`
  - `CLI_API_Specification.md`
  - `CLI_Agent_Daemon_Specification.md`
  - `TaskGuides/CLI_Step_Main.txt`
- Modify tests:
  - `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
  - `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`
  - `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
  - `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`
  - `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs`
  - `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs`

---

### Task 1: Fix GM Override Ledger Accounting

**Files:**
- Modify: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Test: `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`

- [ ] **Step 1: Write failing normalizer test for signed `currencyDeltas` ledger split**

Add a test that applies:

```json
"currencyDeltas": { "inkFeathers": -5, "lightSparks": 2 }
```

Expected canonical ledger entry:

```json
"income": { "inkFeathers": 0, "lightSparks": 2 },
"spending": { "inkFeathers": 5, "lightSparks": 0 }
```

Also assert the final profile currencies changed by `-5/+2`.

Run:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "CanonicalStateNormalizerTests.AfterlifeEntityProfiles"
```

Expected: FAIL because `spending.inkFeathers` is currently `-5`.

- [ ] **Step 2: Write failing validation regression for normalized override output**

In `AfterlifeEntityProfileValidationTests`, validate a canonical profile containing the new split ledger shape and assert no `afterlife_entity_profile_progression_ledger_negative_amount`.

Also validate the current broken shape:

```json
"spending": { "inkFeathers": -5 }
```

Expected: still rejected with `afterlife_entity_profile_progression_ledger_negative_amount`.

- [ ] **Step 3: Implement `SplitSignedCurrencyDeltasForLedger`**

In `AfterlifeEntityProfileState.cs`, add a helper near existing currency helpers:

```csharp
private static (CurrencyDelta Income, CurrencyDelta Spending) SplitSignedCurrencyDeltasForLedger(JsonObject? deltas)
{
    var ink = GetNodeInt(deltas?["inkFeathers"]);
    var sparks = GetNodeInt(deltas?["lightSparks"]);
    return (
        new CurrencyDelta(Math.Max(0, ink), Math.Max(0, sparks)),
        new CurrencyDelta(Math.Max(0, -ink), Math.Max(0, -sparks)));
}
```

Update the `gm_override` ledger append to write non-negative `income` and `spending` from that split instead of cloning signed `currencyDeltas` into `spending`.

- [ ] **Step 4: Run targeted tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "CanonicalStateNormalizerTests.AfterlifeEntityProfiles|AfterlifeEntityProfileValidationTests"
```

Expected: PASS.

- [ ] **Step 5: Commit**

```powershell
git add BookOfEternityClient\Services\AfterlifeEntityProfileState.cs BookOfEternityClient.Tests\CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs BookOfEternityClient.Tests\AfterlifeEntityProfileValidationTests.cs
git commit -m "fix: split afterlife entity override ledger deltas"
```

---

### Task 2: Process Entity Auto-Progression Per Afterlife Contour

**Files:**
- Modify: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Test: `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
- Docs: `CLI_API_Specification.md`, `OtherGuides/Afterlife_Contract_Matrix.md`, `Examples/E_CLI_Afterlife_Turns.txt` if wording needs precision.

- [ ] **Step 1: Write failing mixed-contour test**

Create a normalizer test with two profiles:

```json
{
  "actorType": "guardian",
  "actorId": "guardian_chaos",
  "realm": "Chaos Sea",
  "currencies": { "inkFeathers": 0, "lightSparks": 0 },
  "progressionStrategy": { "strategyId": "s1", "summary": "Качать защиту.", "priorityOrder": ["guard"] }
}
```

```json
{
  "actorType": "radiant_actor",
  "actorId": "resident_shining",
  "realm": "Shining Abode",
  "currencies": { "inkFeathers": 0, "lightSparks": 0 },
  "progressionStrategy": { "strategyId": "s2", "summary": "Качать сияние.", "priorityOrder": ["radiance"] }
}
```

Use a report containing:

```json
"guardianProjectCyclesProcessed": 1,
"newLastGuardianProjectCycleOrdinal": 20,
"shiningAbodeCyclesProcessed": 1,
"newLastShiningAbodeCycleOrdinal": 30
```

Expected:

- Chaos profile uses `chaos:20`, receives Chaos income only: `inkFeathers +12`, `lightSparks +0`.
- Shining profile uses `shining:30`, receives Shining income: `inkFeathers +6`, `lightSparks +1`.
- Chaos profile does not get `lightSparks`.

Run the normalizer test and expect FAIL because current code picks one global Shining cycle.

- [ ] **Step 2: Replace single `ResolveProgressionCycle` with per-profile cycle resolution**

In `AfterlifeEntityProfileState.cs`, replace:

```csharp
var cycle = ResolveProgressionCycle(progressionReportRoot);
foreach (var profile in profiles.OfType<JsonObject>())
    ApplyAutomaticProgression(profile, cycle.Value);
```

with logic that:

- Reads all contour counts from `progressionProcessingReport`.
- For Shining-realm profiles, prefers Shining contours and cycle key `shining:<max shining ordinal>` when Shining counts are positive.
- For Chaos Sea profiles, uses Chaos/Guardian/Resident contours and cycle key `chaos:<max chaos/guardian/resident ordinal>` when those counts are positive.
- Does not apply Shining income to non-Shining profiles merely because the same report has Shining cycles.
- Preserves idempotence through `lastAutoProgressionCycleKey`.

- [ ] **Step 3: Keep existing single-contour tests passing**

Ensure existing tests for Chaos-only auto progression still expect:

```json
"income": { "inkFeathers": 12, "lightSparks": 0 }
```

Ensure Shining-only tests, if present or added, expect:

```json
"income": { "inkFeathers": 6, "lightSparks": 1 }
```

- [ ] **Step 4: Update docs if current wording implies one global cycle**

Docs should say:

```text
Entity auto-progression consumes the processed contour matching each profile realm. Chaos Sea profiles use Chaos/Guardian/Resident afterlife contours and receive Ink Feather income only. Shining Abode profiles use Shining contours and receive Ink Feather + Light Spark income.
```

- [ ] **Step 5: Run targeted tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "CanonicalStateNormalizerTests.AfterlifeEntityProfiles|ProgressionScheduleServiceTests|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

Expected: PASS.

- [ ] **Step 6: Commit**

```powershell
git add BookOfEternityClient\Services\AfterlifeEntityProfileState.cs BookOfEternityClient.Tests\CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs CLI_API_Specification.md OtherGuides\Afterlife_Contract_Matrix.md Examples\E_CLI_Afterlife_Turns.txt
git commit -m "fix: apply afterlife entity progression per contour"
```

---

### Task 3: Make Strategy Reserve And Allow/Forbid Rules Enforced

**Files:**
- Modify: `BookOfEternityClient/Services/AfterlifeEntityProfileState.cs`
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeEntityProfiles.cs`
- Test: `BookOfEternityClient.Tests/CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeEntityProfileValidationTests.cs`
- Docs: `Examples/E_CLI_Afterlife_Turns.txt`, `OtherGuides/Afterlife_Contract_Matrix.md`, `CLI_API_Specification.md`

- [ ] **Step 1: Write failing reserve test**

Profile:

```json
"currencies": { "inkFeathers": 20, "lightSparks": 0 },
"standardArts": { "guard": 0 },
"progressionStrategy": {
  "strategyId": "reserve_guard",
  "summary": "Не тратить резерв.",
  "priorityOrder": ["guard"],
  "resourceReserve": { "inkFeathers": 15, "lightSparks": 0 }
}
```

After Chaos income `+12`, available spend above reserve is `17`, so `guard` tier 0 cost `10` is allowed.

Add a second profile with only `currencies.inkFeathers = 12` and reserve `15`; after income it has `24`, available above reserve `9`, so `guard` must not upgrade.

- [ ] **Step 2: Write failing forbidden-spend test**

Profile:

```json
"progressionStrategy": {
  "priorityOrder": ["soul_dissipation", "guard"],
  "forbiddenSpends": ["soulDissipationTier"]
}
```

Expected: auto-progression skips `soul_dissipation` and buys `guard` if affordable.

- [ ] **Step 3: Write failing allowed-spend test**

Profile:

```json
"progressionStrategy": {
  "priorityOrder": ["guard", "enlightenment"],
  "allowedSpends": ["enlightenment"]
}
```

Expected: skips `guard`, applies `enlightenment` if affordable.

- [ ] **Step 4: Implement strategy spend classifier**

In `AfterlifeEntityProfileState.cs`, add a small classifier:

```csharp
private static string ClassifyProgressionSpend(string priority, JsonObject profile)
```

Return canonical categories:

- `standardArts` for standard art ids.
- `specialArts` for `specialArts[].artId`.
- `enlightenment`
- `radiance`
- `soulDissipationTier` for `soul_dissipation` / `soulDissipation`.

Before attempting an upgrade:

- If `allowedSpends` exists and does not contain the category, skip.
- If `forbiddenSpends` contains the category, skip.
- For affordability, compare against `currencies - resourceReserve`, not raw currency.

- [ ] **Step 5: Validate strategy categories**

In `ValidationService.AfterlifeEntityProfiles.cs`, validate `allowedSpends[]` and `forbiddenSpends[]` entries against:

```text
standardArts, specialArts, enlightenment, radiance, soulDissipationTier
```

Reject unknown values with a specific code, e.g. `afterlife_entity_profile_strategy_unknown_spend_category`.

- [ ] **Step 6: Update docs**

Document that:

- `resourceReserve` is a hard minimum remaining balance.
- `allowedSpends` is an allow-list by spend category.
- `forbiddenSpends` is a deny-list by spend category.
- If both are present, allow-list is applied first, then deny-list.

- [ ] **Step 7: Run targeted tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "CanonicalStateNormalizerTests.AfterlifeEntityProfiles|AfterlifeEntityProfileValidationTests|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

Expected: PASS.

- [ ] **Step 8: Commit**

```powershell
git add BookOfEternityClient\Services\AfterlifeEntityProfileState.cs BookOfEternityClient\Services\Validation\ValidationService.AfterlifeEntityProfiles.cs BookOfEternityClient.Tests\CanonicalStateNormalizerTests.AfterlifeEntityProfiles.cs BookOfEternityClient.Tests\AfterlifeEntityProfileValidationTests.cs CLI_API_Specification.md OtherGuides\Afterlife_Contract_Matrix.md Examples\E_CLI_Afterlife_Turns.txt
git commit -m "fix: enforce afterlife entity progression strategy constraints"
```

---

### Task 4: Make Opposition OD Economy Contract Explicit And Enforced

**Files:**
- Modify: `BookOfEternityClient/Services/Validation/ValidationService.AfterlifeSpiritualConflict.cs`
- Modify: `BookOfEternityClient/Services/AfterlifeSpiritualConflictState.cs`
- Modify: `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Afterlife.SpiritualConflict.cs`
- Test: `BookOfEternityClient.Tests/AfterlifeSpiritualConflictValidationTests.cs`
- Test: `BookOfEternityClient.Tests/ExplorerModeCommandTests.Afterlife.cs`
- Docs: `OtherGuides/Afterlife_Contract_Matrix.md`, `OtherGuides/Afterlife_Combat_Terminology_Glossary.md`, `Examples/E_CLI_Afterlife_Turns.txt`, `CLI_API_Specification.md`, `CLI_Agent_Daemon_Specification.md`, `TaskGuides/CLI_Step_Main.txt`

- [ ] **Step 1: Decide the contract direction before code**

Use this rule unless the task owner explicitly chooses a narrower scope:

```text
Every current exchange that resolves an active opposition tactical operation must carry actionCostAudit.opposition in the same shape as actionCostAudit.player. The opposition art tier is taken from the validated pre-turn afterlife entity profile for the opposition lead actor. If the opposition actor has no profile, the exchange must not spend opposition OD and must document the missing profile as a repair-blocking profile issue.
```

- [ ] **Step 2: Write failing validation test for missing opposition audit**

Create a current exchange with:

```json
"incomingAction": { "operationType": "pressure", "actorType": "guardian", "actorId": "guardian_mirror" },
"actionEconomy": {
  "player": { "current": 6, "max": 6, "source": "Средоточие Души tier 0" },
  "opposition": { "current": 6, "max": 6, "source": "guardian profile" }
},
"actionCostAudit": {
  "player": { "...": "valid player cost audit" }
}
```

Expected: validation error `afterlife_conflict_opposition_action_cost_audit_missing`.

- [ ] **Step 3: Write passing validation test for opposition audit**

Add an afterlife entity profile for `guardian_mirror` with:

```json
"standardArts": { "pressure": 2 }
```

For `pressure`, base/min `3/1`, tier `2`, expected effective cost is `1`.

Expected:

```json
"actionCostAudit": {
  "opposition": {
    "operationType": "pressure",
    "baseCost": 3,
    "minCost": 1,
    "artTier": 2,
    "effectiveCost": 1,
    "before": 6,
    "after": 5
  }
}
```

Validation should pass and final `actionEconomy.opposition.current` should equal `5`.

- [ ] **Step 4: Add opposition authority resolution**

In `ValidationService.AfterlifeSpiritualConflict.cs`, extend the action-cost authority context to resolve opposition art tiers from pre-turn/current validated `afterlife_entity_profiles.json` by `incomingAction.actorType/actorId` or `oppositionSide.leadContestant.actorType/actorId`.

Use existing player cost formula:

```csharp
effectiveCost = Math.Max(minCost, baseCost - artTier);
```

- [ ] **Step 5: Validate opposition cost and sequence**

Add validation analogous to `ValidateActionCostAudit` and `ValidateCurrentActionCostSequence`, but for `actionCostAudit.opposition`.

Rules:

- Required when `incomingAction.operationType` or `matchupAudit.oppositionOperation` is a tactical operation with a cost.
- Not required for `none`, `passive`, `withdraw`, `surrender`, `negotiate`.
- `before >= effectiveCost`.
- `after == before - effectiveCost` for non-recovery operations.
- If opposition uses `recover_spiritual_power`, apply the same recovery bounds as player, capped by `actionEconomy.opposition.max`.
- Final `activeConflict.actionEconomy.opposition.current` must match the last current opposition audit.

- [ ] **Step 6: Update projection/UI**

Ensure `AfterlifeSpiritualConflictState.ApplyExchange` preserves full `actionCostAudit` and `actionEconomy.opposition`.

Update combat log display to show:

```text
ОД игрока: before -> after
ОД противника: before -> after
```

- [ ] **Step 7: Update GM-facing docs**

Update all afterlife contract docs to say:

```text
actionCostAudit.player validates the player's chosen operation. actionCostAudit.opposition validates the opposition operation whenever the exchange resolves an active incoming/opposed tactical action. Both sides use the same base/min cost table and tier discount formula.
```

- [ ] **Step 8: Run targeted tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflictValidationTests|ExplorerModeCommandTests.Afterlife|AfterlifeDocumentationCoverageTests|ExampleDocumentationValidationTests"
```

Expected: PASS.

- [ ] **Step 9: Commit**

```powershell
git add BookOfEternityClient\Services\Validation\ValidationService.AfterlifeSpiritualConflict.cs BookOfEternityClient\Services\AfterlifeSpiritualConflictState.cs BookOfEternityClient\UI\ExplorerMode\ExplorerMode.Afterlife.SpiritualConflict.cs BookOfEternityClient.Tests\AfterlifeSpiritualConflictValidationTests.cs BookOfEternityClient.Tests\ExplorerModeCommandTests.Afterlife.cs OtherGuides\Afterlife_Contract_Matrix.md OtherGuides\Afterlife_Combat_Terminology_Glossary.md Examples\E_CLI_Afterlife_Turns.txt CLI_API_Specification.md CLI_Agent_Daemon_Specification.md TaskGuides\CLI_Step_Main.txt
git commit -m "feat: enforce opposition afterlife action costs"
```

---

### Task 5: Final Verification And Review

**Files:**
- No planned code files.
- Verify all changed files.

- [ ] **Step 1: Run documentation-sensitive afterlife tests**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"
```

Expected: PASS.

- [ ] **Step 2: Run focused afterlife suites**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "AfterlifeSpiritualConflictValidationTests|AfterlifeEntityProfileValidationTests|CanonicalStateNormalizerTests.AfterlifeEntityProfiles|ExplorerModeCommandTests.Afterlife"
```

Expected: PASS.

- [ ] **Step 3: Run full suite**

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore
```

Expected: PASS.

- [ ] **Step 4: Manual audit checklist**

Check:

- GM override negative `currencyDeltas` cannot create negative `progressionLedger.spending`.
- Mixed Shining + Chaos reports do not grant Light Sparks to Chaos profiles.
- Strategy `resourceReserve`, `allowedSpends`, and `forbiddenSpends` affect auto-progression.
- Current spiritual exchanges cannot spend or mutate opposition OD without `actionCostAudit.opposition`.
- `/spiritual_combat_help`, `/spiritual_combat_log`, `/afterlife_profiles`, matrix docs, daemon prompt, API spec, task guide, and examples use the same Russian terms and JSON contract.

- [ ] **Step 5: Commit verification-only doc/test fixes if needed**

Only if Step 4 finds doc/test wording drift:

```powershell
git add <exact files changed>
git commit -m "docs: align afterlife audit fix guidance"
```

---

## Self-Review

- Spec coverage: all four audit findings are covered: signed override ledger, mixed cycle progression, strategy constraints, opposition OD gap.
- Placeholder scan: no TODO/TBD placeholders remain.
- Type consistency: uses existing JSON property names: `afterlifeEntityProgressionOverrides`, `currencyDeltas`, `progressionLedger`, `actionEconomy`, `actionCostAudit`, `incomingAction`, `matchupAudit`.
- Scope note: Task 4 is a contract expansion, so it must update GM-facing docs and documentation coverage in the same change.
