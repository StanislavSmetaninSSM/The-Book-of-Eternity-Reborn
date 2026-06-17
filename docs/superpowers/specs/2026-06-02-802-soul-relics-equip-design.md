# 2026-06-02 — #802 Soul Relics Browser Equip/Unequip Design

## Goal
Give the browser client the same soul-relic equip/unequip/inspect workflow the console already has
(`ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs`), backed by `game_state/meta/soul_state.json`.

## Approach
Mirror the shape of the just-merged #801 work, but for soul relics (different file, different
slot semantics). Keep the boundary explicit — a new `SoulRelicEquipmentService` lives next to
`InventoryEquipmentService`, NOT a refactor of it.

## Why a separate service
- `InventoryEquipmentService` reads `items.json` and uses `equipment` (a slot→reference object).
  It is intentionally hostile to soul relics (its own `IsSoulRelic` check rejects them).
- `soul_state.json` has `soulRelics = { equipped: JsonArray, stored: JsonArray }` — a list-of-objects
  model, not a slot map. The two data shapes don't share enough code to make a joint service
  cleaner than two parallel ones.
- Both produce a `*EquipmentContext` (lists, equipped, validate, equip, unequip). Same shape, two
  files. Browser command routing and prompt sessions can mirror the #801 plumbing.

## Components

### 1. `Services/SoulRelicEquipmentService.cs` (new, ~400-500 lines)
Mirrors `InventoryEquipmentService`:
- `SoulStatePath = "game_state/meta/soul_state.json"`
- `ReadContextAsync(fs)` → `SoulRelicEquipmentContext`:
  - `Stored: IReadOnlyList<SoulRelicItem>`
  - `Equipped: IReadOnlyList<SoulRelicItem>` with slot info
  - Exposes `IsSoulRelic` semantics automatically (every item is a relic).
- `EquipAsync(fs, relicId, slotKey)` → moves a stored relic into `equipped[]` with the chosen slot.
- `UnequipAsync(fs, relicId)` → moves an equipped relic back to `stored[]`.
- `ValidateEquipAsync(...)` / `ValidateUnequipAsync(...)` → return a `SoulRelicWriteOutcome`.
- `MaxEquippedCount` constant (read from existing rules if any; default 6 to match common
  console behavior — verify against `AfterlifeProgressionTuning` first; if not defined, hardcode
  with a clear constant and TODO marker).
- Compatibility check: if a relic's `allowedSlots` doesn't include the chosen slot, reject.
- Relic-identity matching: match by `relicId` first, fall back to name.

### 2. `WebUi/BrowserAfterlifeWriteService.cs` (extend)
Add three handlers routed by command token:
- `"/soul_relic_equip"` / `"/экипировать_реликвию"` → `ApplySoulRelicEquipAsync`
- `"/soul_relic_unequip"` / `"/снять_реликвию"` → `ApplySoulRelicUnequipAsync`
- `"/soul_relic_inspect"` is left to read-only `BuildSoulSection` (no write needed)
All three go through the existing `ExecuteAsync` lock/rollback path with `SoulStatePath` as the
rollback path.

### 3. `UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` (extend)
Add `BuildSoulRelicEquipAsync` and `BuildSoulRelicUnequipAsync`:
- Equip prompt: relic selection (`UiSelectionPrompt` from stored list) → slot selection
  (`UiSelectionPrompt` filtered by the relic's allowed slots) → confirmation
  (`UiConfirmationPrompt` reading `confirm_soul_relic_write`).
- Unequip prompt: relic selection from equipped list → confirmation.

### 4. `CommandProtocol/ExplorerCommandCatalog.cs`
Change `soul_relics` from `ReadOnly` to `LocalTurn` with `acceptsArguments: true`, add the two new
subcommand ids `soul_relic_equip` and `soul_relic_unequip`.

### 5. `WebUi/ExplorerWebPromptSessionService.cs`
Add `/soul_relic_equip` and `/soul_relic_unequip` (and Russian variants) to `RequiresLocalUiLock`.

### 6. `WebUi/BrowserCommandCoverageService.cs`
Update the `soul_relics` audit override: relic equip/unequip actions are covered, inspect remains
read-only; remove #802 from the `Tracked(...)` list.

### 7. Tests (in `BookOfEternityClient.Tests/ExplorerWebCommandServiceTests.cs`)
- `ExecuteAsync_SoulRelics_AddsEquipAndUnequipActions`
- `ExecuteAsync_SoulRelicEquipAction_OpensPromptSessionWithSlotAndConfirmation`
- `ExecuteAsync_SoulRelicEquip_WithActiveGmTurn_BlocksPromptSession`
- `ExecuteAsync_SoulRelicEquip_WithOtherLocalLock_BlocksPromptSession`
- `SubmitPromptSessionAsync_SoulRelicEquip_MovesRelicToEquippedAndReleasesLock`
- `SubmitPromptSessionAsync_SoulRelicUnequip_MovesRelicToStoredAndReleasesLock`
- `SubmitPromptSessionAsync_SoulRelicEquip_WhenMaxEquipped_RejectsWithFriendlyError`
- `SubmitPromptSessionAsync_SoulRelicEquip_WhenSlotIncompatible_RejectsWithFriendlyError`

## Data assumptions (verified during impl)
- `soulRelics.equipped` is a `JsonArray` of relic objects, each having `relicId`, `name`, `slot`
  (the slot the relic occupies when equipped), and other metadata.
- `soulRelics.stored` is the same shape.
- If the file/section is missing, treat as empty (no relics, no equip possible).
- The console path uses `ExplorerMode.Afterlife.SoulRelicsArchiveInbox.cs`. We do NOT call into
  it; we re-implement the same logic in the new service so the browser path is independent.

## Afterlife-contract guardrail (AGENTS.md)
This change touches the afterlife runtime contract:
- New `BrowserAfterlifeWriteService` handlers: `soul_relic_equip`, `soul_relic_unequip`.
- Lifecycle prompt-session surface gains `soul_relic_equip`, `soul_relic_unequip`.
- Browser coverage audit override updated.

After implementation, update:
- `OtherGuides/Afterlife_Contract_Matrix.md` (add the two new actions to the browser rows).
- `Examples/E_CLI_Afterlife_Turns.txt` (add example browser-write turns for equip/unequip).
- `Examples/example_validation_manifest.json` (add equip/unequip validation entries if needed).
- `BookOfEternityClient.Tests/AfterlifeDocumentationCoverageTests.cs` and
  `BookOfEternityClient.Tests/ExampleDocumentationValidationTests.cs` — run the validation gate
  below before merge.

## Out of scope (separate issues)
- Detailed relic stats / inspect view (the existing read-only `BuildSoulSection` already shows
  relic data; this PR only adds equip/unequip actions next to it).
- Relic forging / reshaping (#813).
- Cross-faction relic trades (covered by `/abode_offering` already).

## Risk
- The shape of `soulRelics.equipped[]` items in real save data may differ from the schema
  assumed here. Mitigation: tests use realistic seed JSON parsed against the actual
  `GuardianPolicyContracts.TryReadStrictCurrentSoulRelicCollections` rules; if those rules
  reject the seed, the tests fail RED and we adjust.
- `MaxEquippedCount`: if the project has a canonical source for this number, we must use it.
  Otherwise we hardcode with a clearly-named constant.
