# Implementation Plan: Browser Inventory Management (#806)

**Source issue:** [#806 — feat(web): Управление инвентарём — выброс, разделение и объединение стаков](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/806)
**Parent epic:** [#817](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Spec:** `specs/806-browser-inventory-management/spec.md`
**Branch/worktree:** `fix/806-browser-inventory-management` at `E:/Games/worktrees/boe-806-browser-inventory-management`

## Technical Context

The current repository is a .NET 8 C# game client with a local browser UI served by the C# host and a React/Vite frontend. C# remains the gameplay/application authority. Browser inventory management should use the same command catalog, prompt-session, and local-write coordinator patterns that were used for #805 browser trade parity.

Relevant source areas:

- `BookOfEternityClient/UI/ExplorerMode/ExplorerMode.Inventory.cs` — console authority for `DropItemLocal`, `SplitItemStack`, `MergeItemStacks`, inventory identity helpers, stack count fields, and merge signature semantics.
- `BookOfEternityClient/UI/ExplorerLifecycleLocalTurnCommandResultBuilder.cs` — C# browser local-turn prompt/result builder for lifecycle commands.
- `BookOfEternityClient/WebUi/BrowserMortalWorldWriteService.cs` — C# browser write authority for mortal-world prompt submissions.
- `BookOfEternityClient/WebUi/ExplorerWebPromptSessionService.cs` — prompt-session routing and local UI lock handling.
- `BookOfEternityClient/CommandProtocol/ExplorerCommandCatalog.cs` — command IDs, aliases, mutation status, browser handler kind.
- `BookOfEternityClient/WebUi/BrowserPlayerCommandMenuBuilder.cs` and `BrowserCommandCoverageService.cs` — player-facing action metadata and advanced coverage audit.
- `BookOfEternityClient.Tests/WebUi/` and `BookOfEternityClient.Tests/BrowserApiContractTests.cs` — focused browser parity and command coverage tests.

## Architecture

Add a focused C# inventory-management browser flow. The browser will expose three local-turn commands/actions for drop, split, and merge. `ExplorerLifecycleLocalTurnCommandResultBuilder` should build safe player-facing prompt/result blocks from current inventory state. `BrowserMortalWorldWriteService` should validate prompt submissions and mutate `game_state/inventory/items.json` through shared or extracted C# inventory helper logic under `BrowserLocalWriteCoordinator`. React/TypeScript should remain generic prompt/result presentation unless existing components need fixture/type updates.

If the implementation extracts inventory mutation helpers from `ExplorerMode.Inventory.cs`, keep them small and testable, preserve console behavior, and update the console path to use the same helper only if that can be done without broad unrelated refactoring.

## Spec Kit and governance

Spec Kit is required because #806 is player-facing Browser Client parity work with multi-file C#/tests/coverage changes and a durable handoff path. The active feature directory is `specs/806-browser-inventory-management/`.

Use `.specify/memory/constitution.md` as governance. Do not mark tasks complete until implementation and verification evidence exist. If accepted requirements change during implementation, update `spec.md`, this `plan.md`, and `tasks.md` together.

## Data and contract scope

Expected contract scope is client-owned inventory state only:

- Existing file shape: `game_state/inventory/items.json`.
- Existing item identity/signature/count semantics from `ExplorerMode.Inventory.cs`.
- No new pending/control file, GM-authored response field, receipt/report, validation rule, normalizer side effect, or lifecycle authority path is expected.

If implementation changes any inventory JSON schema or GM-facing contract, update GM-facing documentation/examples/tests in the same PR. Otherwise record “docs/prompts not changed: reused existing client-owned inventory shape and console behavior” in completion evidence.

## Testing strategy

Follow strict TDD:

1. Add focused failing C# tests/source guards for missing #806 browser commands and prompt/write behavior.
2. Run the focused filter and confirm RED for the expected reason.
3. Implement minimal C# prompt/write/coverage changes.
4. Run focused GREEN and affected suites with exact counts.
5. Run frontend verification only if React/fixtures/components change.
6. Run `git diff --check`, added-line static scan, Spec Kit prerequisite check, and independent review before PR/merge.

Suggested focused filters:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"
```

## Implementation outline

1. Investigate and, if needed, extract inventory helper logic for:
   - resolving inventory item by stable identity/name;
   - clearing equipment references on drop;
   - reading/writing `count` or `quantity` consistently;
   - assigning a fresh inventory identity to a split copy;
   - computing merge compatibility and summing/removing compatible stacks.
2. Add browser command metadata for three inventory-management commands/actions.
3. Add prompt/result builder support:
   - missing argument -> form asks for item id and action-specific fields;
   - drop -> confirmation prompt;
   - split -> quantity input with current bounds and confirmation;
   - merge -> matching-stack summary and confirmation;
   - all default text is Russian/player-facing and sanitized.
4. Add write-service handlers in `BrowserMortalWorldWriteService`:
   - parse command token/args and prompt answers;
   - validate confirmation;
   - enforce local-write coordination;
   - mutate inventory with shared C# authority;
   - return useful player-facing success/error result states.
5. Update coverage/menu/help metadata and tests so #806 is explicit and the generic `inventory` audit row no longer carries stack-management as an open gap after the commands exist.
6. Reconcile Spec Kit artifacts with RED/GREEN evidence, docs impact, review findings, and verification counts.

## Risks and mitigations

- **Diverging console/browser stack semantics:** mitigate by extracting or precisely reusing C# helper logic and testing drop/split/merge outcomes against console semantics.
- **Raw technical text leakage:** add default player-facing source/result tests that reject file paths, raw JSON, API/DTO/protocol/debug wording, `slotId`, `contract`, `canonical`, `repair`, and similar diagnostics in default prompt/result blocks.
- **Unsafe writes during pending GM turn/local UI lock:** keep all submissions behind `BrowserLocalWriteCoordinator` and prompt sessions requiring local UI lock.
- **Over-broad #817 closure:** keep remaining child issues tracked and explicitly out of scope.

## Verification plan

Minimum commands before closure:

```bash
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"

dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"

dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal

dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal

powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks

git diff --check origin/main...HEAD
```

Run `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend files, generated browser fixtures, or TypeScript contracts change. Run documentation coverage tests only if contract/docs changed.

## Initial plan record

2026-06-06: plan created by the autonomous Hermes worker before Codex delegation. Baseline local verification is still to be run in the isolated worktree and recorded in the Codex prompt. Implementation will be delegated to Codex with Spec Kit + Superpowers method requirements; Hermes owns final acceptance, independent review, PR, merge, and issue closure.

## Implementation record

2026-06-06 Codex implementation:

- Added `BookOfEternityClient/Services/InventoryManagementService.cs` as focused C# authority for ordinary Mortal World inventory drop/split/merge. It mirrors the console semantics from `ExplorerMode.Inventory.cs`: identity matching by `existedId` / `itemId` / `id` or name, equipment reference clearing on drop, `count`/`quantity` stack handling, fresh split identity assignment, and merge signatures that ignore count/quantity and identity fields.
- Console `ExplorerMode.Inventory.cs` was left unchanged to avoid broad interactive console refactoring. The browser service is behavior-equivalent to the console private helpers and is pinned by focused browser tests for drop, split, merge, invalid submissions, local-write blockers, and default player-facing text.
- Added browser local-turn command metadata for `/выбросить_предмет` + `/inventory_drop`, `/разделить_стопку` + `/inventory_split`, and `/объединить_стопки` + `/inventory_merge`.
- Added C# prompt/result construction in `ExplorerLifecycleLocalTurnCommandResultBuilder.cs`; prompts are Russian/player-facing and use generic `ExplorerCommandResult` prompt DTOs.
- Added `BrowserMortalWorldWriteService` handlers behind `BrowserLocalWriteCoordinator` and `ExplorerWebPromptSessionService` local UI locks.
- Updated `BrowserPlayerCommandMenuBuilder` and `BrowserCommandCoverageService` so #806 coverage is explicit and the generic `inventory` coverage row no longer carries the stack-management follow-up.
- React/TypeScript/frontend files were not changed: the existing generic command result and prompt form render the new C# prompt DTOs.
- Docs/prompts/contracts were not changed: this reused the existing client-owned `game_state/inventory/items.json` shape and console behavior, with no GM-facing prompt, afterlife contract, validation/normalizer, pending/control, response field, receipt, report, or lifecycle guidance change.

## Verification record

RED:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"
```

Result before implementation: failed with 12 failed / 0 passed / 0 skipped / 12 total for the expected missing browser inventory-management command, prompt, write-service, and coverage support.

GREEN evidence collected after implementation:

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserInventoryManagement" --logger "console;verbosity=minimal"
```

Result: passed with 12 passed / 0 failed / 0 skipped / 12 total.

```powershell
dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Inventory" --logger "console;verbosity=minimal"
```

Result: passed with 88 passed / 0 failed / 0 skipped / 88 total.

```powershell
dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore --verbosity:minimal
dotnet build BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --verbosity:minimal
```

Result: both builds succeeded with 0 warnings / 0 errors.

Additional final checks:

- `powershell.exe -NoProfile -ExecutionPolicy Bypass -File .specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` returned the active feature directory and `tasks.md`.
- `git diff --check` exited 0 with CRLF conversion warnings only.
- Added-line static scan over production `BookOfEternityClient` additions, excluding tests/spec docs, found no forbidden default player-facing diagnostic terms.
