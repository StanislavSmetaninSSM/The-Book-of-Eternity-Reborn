# Feature Specification: Browser Inventory Management (#806)

**Source issue:** [#806 — feat(web): Управление инвентарём — выброс, разделение и объединение стаков](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/806)
**Parent epic:** [#817 — Полный паритет интерактивных действий — консоль vs браузер](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Related Browser Client epic:** [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)
**Recently closed dependency:** [#805](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/805)

## Scope

The Browser Client must expose the same ordinary Mortal World inventory-management actions that the console already performs from `ExplorerMode.Inventory.cs`: dropping an item, splitting an item stack by quantity, and merging compatible stacks. The browser implementation must preserve the current project direction: a minimalist game shell, player-facing Russian labels, existing C# client/application authority, shared prompt-session/write-service flows, and no React-side gameplay rules.

This feature covers only the #806 inventory actions:

1. Discard/drop a selected inventory item.
2. Split one stack into two stacks after the player enters a valid quantity.
3. Merge compatible stacks of the same item identity/signature.

The feature must not close #817 or implement unrelated child parity issues (#807–#816).

## User Stories

### Story 1 — Drop an inventory item from the browser

As a player, I can choose an inventory item in the browser, see a safe confirmation prompt, and discard it without switching to the console.

**Acceptance criteria**

- The browser exposes a player-facing inventory drop command/action, with aliases such as `/inventory_drop` and `/выбросить_предмет` or an equivalent player-facing action from the inventory surface.
- The prompt/result identifies the item by player-facing name and selected stable identifier, shows whether the item is equipped or stacked, and requires explicit confirmation before mutation.
- Confirmed drop removes the item from `game_state/inventory/items.json`; if the item is equipped, the corresponding equipment slot is cleared like the console `DropItemLocal` path.
- Invalid item ids, missing inventory files, missing confirmation, or blocked local writes return Russian player-facing errors and do not mutate state.
- Browser code must not expose raw file paths, API/DTO/protocol/debug wording, raw JSON, or internal stack identifiers in the default player UI unless advanced/debug mode is active.

### Story 2 — Split an inventory stack from the browser

As a player, I can split a stack by entering an amount in the browser, and the resulting inventory matches the console split semantics.

**Acceptance criteria**

- The browser exposes a player-facing stack split command/action, with aliases such as `/inventory_split` and `/разделить_стопку` or an equivalent player-facing action from the inventory surface.
- The prompt/result shows the current stack count, accepts a positive integer quantity, and rejects values less than 1 or greater than or equal to the current count.
- Confirmed split reduces the original stack and creates a new stack entry with a fresh inventory identity while preserving relevant item data, matching `SplitItemStack` semantics.
- The action supports both `count` and `quantity` stack fields consistently with the console path.
- Non-stack or count-1 items return a player-facing unavailable state rather than fabricating a split.

### Story 3 — Merge compatible stacks from the browser

As a player, I can merge compatible stacks from the browser so that duplicate stack entries collapse into one stack using the same compatibility rules as the console.

**Acceptance criteria**

- The browser exposes a player-facing stack merge command/action, with aliases such as `/inventory_merge` and `/объединить_стопки` or an equivalent player-facing action from the inventory surface.
- The prompt/result identifies matching compatible stacks before mutation and requires explicit confirmation.
- Confirmed merge sums compatible stack counts into the selected stack and removes merged duplicate entries, matching console `MergeItemStacks` semantics.
- If there is no compatible second stack, the browser returns a player-facing unavailable state and does not mutate state.
- Merge compatibility must be sourced from existing C# inventory signature logic or an extracted shared C# helper; React must not implement matching rules.

### Story 4 — Browser command coverage reflects closed #806 inventory parity

As a maintainer, I can trust the Browser Client command/parity audit after #806 closes: stack-management gaps no longer remain tracked only under the generic `inventory` row.

**Acceptance criteria**

- `ExplorerCommandCatalog`, browser command coverage metadata, player command menu/action metadata, and tests/fixtures reflect the new inventory drop/split/merge browser coverage.
- The default browser UI uses Russian, in-world labels/descriptions and keeps advanced/raw diagnostics opt-in.
- Remaining unrelated parity gaps (#807–#816) stay tracked; this feature must not claim full #817 closure.
- Focused tests/source guards fail if the #806 commands/actions are absent or if default inventory-management surfaces expose raw API/DTO/debug/file-path/internal-contract wording.

## Requirements

- Use existing C# inventory state and console semantics as authority. If the console logic is private inside `ExplorerMode.Inventory.cs`, extract focused reusable C# helpers or mirror the semantics in a C# service with tests; do not implement gameplay rules in React/TypeScript.
- Use existing browser command and prompt-session APIs. Mutating submissions must go through `BrowserMortalWorldWriteService` and `BrowserLocalWriteCoordinator` (or an existing service path with equivalent local-write safety).
- Preserve pending GM-turn/local UI lock blocking for browser writes.
- Use strict TDD: RED focused test/source guard before production changes; GREEN implementation; refactor only after passing tests.
- If implementation changes any inventory JSON shape, GM-authored contract, validation rule, normalizer side effect, response field, receipt/report, or lifecycle authority path, update relevant GM-facing docs/examples/manifests/tests in the same change. Reusing existing client-owned inventory item state shape does not require GM docs; record that rationale.
- Keep Spec Kit artifacts synchronized with implementation findings, RED/GREEN evidence, verification counts, review findings, and remaining risks.

## Out of Scope

- Closing #817 or implementing #807–#816.
- Adding storage transport, NPC/social interactions, Shining politics, relic forge, incarnation gates, Ink Feather fate edits, afterlife archive pulls, or new inventory/economy mechanics.
- Adding React-only mutation handlers, pricing/economy rules, new inventory schemas, or cloud/remote services.
- Reintroducing the deleted Feature-branch/card-heavy browser design direction.
- Waiting for GitHub Actions; local verification is the normal gate for this project.

## Verification

Minimum verification for closure:

- Focused C# RED/GREEN tests for browser prompt generation/submission and inventory drop/split/merge write behavior, with non-zero counts.
- Browser command coverage/source guard tests proving #806 coverage is explicit while unrelated follow-ups remain tracked.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal` after restore/build state exists, otherwise without `--no-restore`.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement|BrowserMortalWorldWriteService|ExplorerWebPromptSession|BrowserWebUiParity|BrowserWebUiSmoke|CommandResult|Inventory" --logger "console;verbosity=minimal"` or a narrower equivalent with explicit rationale and counts.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend/fixtures/components changed.
- Documentation-sensitive verification if any GM-facing contract/docs changed: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests" --logger "console;verbosity:minimal"`.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` when Spec Kit artifacts are updated.
- `git diff --check origin/main...HEAD`.
- Added-line static security scan excluding docs/spec false positives.

## Initial Analysis Record

2026-06-06 autonomous worker selection: #806 is the next logical open child of #817 after #805 landed on `main`. The issue is medium Browser Client parity work touching player-facing UX, C# web prompt/write flows, command catalog/coverage, and local inventory mutation safety, so Spec Kit artifacts are required before implementation.

Current authority discovered before delegation:

- Console drop/split/merge actions are private methods in `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs`: `DropItemLocal`, `SplitItemStack`, and `MergeItemStacks`.
- Browser command catalog currently covers inventory read-only plus `inventory_equip` and `inventory_unequip`; no dedicated drop/split/merge browser command exists yet.
- `BrowserCommandCoverageService` currently keeps stack-management work tracked under `#806, #817` on the generic `inventory` audit row.
- `ExplorerWebPromptSessionService` already routes local-turn browser prompt submissions through `BrowserMortalWorldWriteService` for mortal-world commands.
- `BrowserMortalWorldWriteService` currently handles stat distribution, directives, equip/unequip, NPC trade, and craft; it does not handle #806 inventory drop/split/merge yet.
- Runtime/GM contract expectation: this feature should reuse the existing `game_state/inventory/items.json` item/stack shape. If no new GM-authored contract or schema is introduced, GM-facing docs are not expected to change.

## Implementation Evidence

2026-06-06 Codex implementation for #806:

- Browser now exposes local-turn inventory-management commands `/выбросить_предмет` + `/inventory_drop`, `/разделить_стопку` + `/inventory_split`, and `/объединить_стопки` + `/inventory_merge`.
- The commands build guided Russian prompt forms in C# and submit through the existing prompt-session/local-write path. Missing confirmation, invalid item selection, invalid split quantities, no compatible merge target, and local-write blockers do not mutate inventory.
- Confirmed drop removes the item from `game_state/inventory/items.json` and clears matching `equipment`/`equippedItems` references. Confirmed split preserves the source `count` or `quantity` field, reduces the original stack, and creates a split stack with a fresh inventory identity. Confirmed merge uses C# merge-signature compatibility that ignores count/quantity and identity fields, sums compatible stack counts, and removes duplicate compatible entries.
- `ExplorerCommandCatalog`, `BrowserPlayerCommandMenuBuilder`, and `BrowserCommandCoverageService` now represent #806 explicitly; unrelated #807, #814, #816, and #817 gaps remain tracked.
- React/TypeScript/frontend files were not changed because generic browser prompt/result rendering already handles the C# DTOs.
- GM-facing docs/prompts/examples were not changed because no GM-authored contract, afterlife pending/control surface, validation/normalizer rule, response field, receipt/report, lifecycle authority, or inventory JSON schema changed.

Verification evidence:

- RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"` failed before implementation with 12 failed / 0 passed / 0 skipped / 12 total for expected missing #806 support.
- GREEN focused: same command passed after implementation with 12 passed / 0 failed / 0 skipped / 12 total.
- Affected C# suite: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"` passed with 88 passed / 0 failed / 0 skipped / 88 total.
- Builds: client and test project builds with `--no-restore --verbosity:minimal` both succeeded with 0 warnings / 0 errors.
- Spec Kit prerequisite check returned the active feature directory and `tasks.md`; `git diff --check` exited 0 with CRLF conversion warnings only; the added-line static scan over production `BookOfEternityClient` additions found no forbidden default player-facing diagnostic terms.
