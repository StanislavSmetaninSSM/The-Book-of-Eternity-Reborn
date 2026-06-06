# Feature Specification: Browser Trade Parity (#805)

**Source issue:** [#805 — feat(web): Торговля с NPC и торговля в посмертии через браузерный клиент](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/805)
**Parent epic:** [#817 — Полный паритет интерактивных действий — консоль vs браузер](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/817)
**Related Browser Client epic:** [#680](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/680)
**Dependencies already closed:** [#801](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/801), [#804](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/804)

## Scope

The Browser Client must expose the same player-meaningful trade actions that the console client already supports, without moving gameplay authority into React. Trade flows remain backed by the shared C# client/application layer, existing JSON state, local-write coordination, and existing pending trade contracts.

This feature covers three browser trade surfaces tracked by #805:

1. Mortal-world NPC trade.
2. Shining Abode faction trade.
3. Chaos Sea Guardian trade.

The default browser UI remains the current minimalist game shell with tabs and a single command/composer input. Browser actions may appear as player-facing contextual buttons or `/help`/command entries, but default player UI must not expose API names, DTO names, endpoint names, raw slash-command audit framing, raw JSON, file paths, or debug/protocol language.

## User Stories

### Story 1 — Mortal NPC trade opens a browser form

As a player in the Mortal World, I can start trade with an NPC from the browser client, inspect the merchant status/stock readiness, and submit buy, sell, or buyback choices through a guided prompt instead of switching to the console.

**Acceptance criteria**

- The browser exposes a player-facing NPC trade command/action for a specific NPC, with aliases such as `/npc_trade` and a Russian alias, or an equivalent player-facing action from the NPC surface.
- The command result renders safe player-facing trade status: trader name, stock readiness, availability/blockers, and available buy/sell/buyback choices when present.
- Buying validates the selected stock slot, money, sold-out state, inventory state, and explicit confirmation before mutating files.
- Selling validates the selected player item, quest-bound/soul-relic/equipped exclusions, price, and explicit confirmation before mutating files.
- Buyback validates the selected buyback entry, money, status, and explicit confirmation before mutating files.
- Mutations use `BrowserMortalWorldWriteService`, `BrowserLocalWriteCoordinator`, and existing `NpcTradeService`/`NpcTradeRequestState` authority; React must not implement pricing or inventory mutation rules.
- If the NPC trade inventory is not ready and an existing pending-contract workflow is required, the browser returns a player-facing pending/blocked result and does not fabricate canonical merchant stock.

### Story 2 — Shining Abode faction trade opens a browser form

As a player in the Shining Abode, I can inspect faction trade availability and submit supported trade actions through the browser while preserving existing Shining trade pending-contract rules.

**Acceptance criteria**

- The browser exposes a player-facing Shining faction trade command/action for a specific faction, with aliases such as `/shining_trade` and a Russian alias, or an equivalent player-facing action from the Shining/faction surface.
- The command result shows faction name, trade tier, stock readiness, rarity/slot/service metadata, and player-facing blockers.
- Requesting/preparing a faction trade inventory uses the existing `ShiningTradeRequestState`/`ShiningTradeService.RequestInventoryAsync` path when canonical stock is not ready.
- Buying from ready stock uses existing `ShiningTradeService.BuyAsync`, local-write coordination, cost blockers, and explicit confirmation.
- If selling to Shining factions is supported by the console/service authority, browser parity includes it; if investigation proves no supported console/service sell operation exists, the spec/plan/tasks and issue evidence must record that scope boundary before closure.
- Browser work must not add, rename, or weaken Shining trade pending/control contracts without updating GM-facing contract docs/tests.

### Story 3 — Guardian trade opens a browser form

As a player in the Chaos Sea, I can trade with Guardians through the browser, including request/stock status, buying, selling, and buyback when the same operations are supported by the C# authority.

**Acceptance criteria**

- The browser exposes a player-facing Guardian trade command/action for a specific Guardian, with aliases such as `/guardian_trade` and a Russian alias, or an equivalent player-facing action from the Guardian surface.
- The command result shows guardian name/domain, reputation tier, trade-slot readiness, pending request state, and safe player-facing blockers.
- Requesting/preparing Guardian stock uses existing `GuardianTradeRequestState`/`GuardianTradeService.EnsureTradeInventoryAsync` contract logic.
- Buying, selling, and buyback use existing `GuardianTradeService` operations, local-write coordination, canonical soul/guardian state checks, and explicit confirmation.
- Browser output keeps Guardian/Chaos Sea conceptual surfaces separate from Mortal NPC and Shining faction trade.
- Browser work must not add, rename, or weaken Guardian trade pending/control contracts without updating GM-facing contract docs/tests.

### Story 4 — Browser command coverage reflects closed trade parity

As a maintainer, I can trust the Browser Client command/parity audit after #805 closes: trade gaps no longer remain hidden inside generic read-only command rows.

**Acceptance criteria**

- `ExplorerCommandCatalog`, browser command coverage metadata, player command menu/action metadata, and fixtures/tests reflect the new trade command/action coverage.
- Remaining unrelated parity gaps (#806–#816) stay tracked; this feature must not claim full #817 closure.
- Default player UI surfaces use Russian, in-world labels/descriptions and keep advanced/raw diagnostics opt-in.
- Source guards or tests fail if the trade commands are absent from the browser command catalog/coverage or if default trade result surfaces expose raw API/DTO/debug/file-path wording.

## Requirements

- Use existing C# trade services as authority: `NpcTradeService`, `ShiningTradeService`, `GuardianTradeService`, and their pending request state classes.
- Use existing prompt-session APIs and write services. Add small C# prompt/result builder helpers if needed; do not add React-specific gameplay logic.
- Use test-first discipline for each new behavior: RED test/source guard, GREEN implementation, refactor only after passing tests.
- Preserve local write safety and pending GM-turn blocking. Browser submits must go through `BrowserLocalWriteCoordinator` or an existing service path that provides equivalent safety.
- Keep Spec Kit artifacts synchronized with implementation evidence and any scope boundary discovered during investigation.
- If implementation changes any GM-authored runtime contract, pending/control file shape, response field, receipt/report, validation rule, normalizer side effect, or authority path, update the relevant GM-facing docs/examples/manifests/tests in the same change.

## Out of Scope

- Closing #817 or other child issues (#806–#816) in this change.
- Adding new economy, pricing, merchant stock generation, Guardian reputation, Shining faction mechanics, banner mechanics, or React-only trade rules.
- Reintroducing the deleted Feature-branch/card-heavy browser design direction.
- Waiting for GitHub Actions; local verification is the normal gate for this project.

## Verification

Minimum verification for closure:

- Focused C# tests for browser prompt generation/submission and trade service integration, with non-zero counts.
- Browser command coverage/source guard tests proving #805 trade commands/actions are no longer tracked follow-up gaps while unrelated follow-ups remain.
- `dotnet build BookOfEternityClient/BookOfEternityClient.csproj --no-restore` after restore/build state exists, otherwise without `--no-restore`.
- `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserMortalWorldWriteService|BrowserAfterlifeWriteService|ExplorerWebPromptSession|BrowserWebUiParity|BrowserWebUiSmoke|CommandResult|Trade"` or a narrower equivalent with explicit rationale and counts.
- `npm run verify --prefix BookOfEternityClient.WebFrontend` if frontend/fixtures/components changed.
- Documentation-sensitive contract verification if any pending/control or GM-facing contract changes are made: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj --no-restore --filter "ExampleDocumentationValidationTests|AfterlifeDocumentationCoverageTests"`.
- `.specify/scripts/powershell/check-prerequisites.ps1 -Json -RequireTasks -IncludeTasks` when Spec Kit artifacts are updated.
- `git diff --check origin/main...HEAD`.
- Added-line static security scan excluding docs/spec false positives.

## Initial Analysis Record

2026-06-06 autonomous worker selection: #805 is the next logical open child of #817 after #801, #802, #803, #804, and #757 are closed. The issue is medium/large browser parity work touching player-facing UX, C# web write services, command catalog/coverage, and afterlife/Chaos Sea/Shining Abode trade contracts, so Spec Kit is required before implementation.

Current authority discovered before delegation:

- `ExplorerWebPromptSessionService` already routes submitted prompt sessions through `BrowserMortalWorldWriteService`, `BrowserAfterlifeWriteService`, and `BrowserSarefStoryWriteService`.
- `BrowserMortalWorldWriteService` currently handles world setup, stat distribution, directives, equip/unequip, and craft; it does not handle NPC trade yet.
- `BrowserAfterlifeWriteService` currently handles Shining treasury, Source of Light, afterlife inbox, spiritual arts/actions, gacha, abode offering, Guardian foundation, and soul relic equip/unequip; it does not handle Shining/Guardian trade yet.
- `NpcTradeService`, `GuardianTradeService`, and `ShiningTradeService` already contain authoritative trade views/operations and pending request state classes.
- `ExplorerCommandCatalog` currently has no dedicated trade commands; `BrowserCommandCoverageService` tracks NPC trade under #805 and broader inventory/interaction follow-ups separately.

## Implementation Record

2026-06-06 implementation evidence:

- Browser command catalog now exposes `/npc_trade` (`/торговля_нпс`), `/shining_trade` (`/сияющая_торговля`), and `/guardian_trade` (`/торговля_хранителя`) as local-turn browser-executable trade forms.
- Mortal NPC browser trade uses `ExplorerLifecycleLocalTurnCommandResultBuilder` for safe prompt/status output and `BrowserMortalWorldWriteService` for confirmed `request`, `buy`, `sell`, and `buyback` submissions through `NpcTradeService`/`NpcTradeRequestState`.
- Shining browser trade uses `ShiningTradeService.RequestInventoryAsync` for request/prep and `ShiningTradeService.BuyAsync` for ready-stock buy. Investigation found no Shining faction sell operation in `ShiningTradeService` or the console Shining trade flow, so browser output documents the current authority boundary and rejects Shining sell without mutating state.
- Guardian browser trade uses `GuardianTradeService.EnsureTradeInventoryAsync`, `BuyAsync`, `SellAsync`, and `BuyBackAsync` through `BrowserAfterlifeWriteService`.
- React/frontend gameplay logic was not changed; existing generic command result and prompt form surfaces render the new C# blocks/prompts.
- Runtime/GM contract shapes were reused unchanged. No pending/control file, action type, receipt/report field, validation rule, normalizer side effect, lifecycle mode, or GM-authoring contract was added, renamed, removed, or weakened.

RED/GREEN evidence:

- RED: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "BrowserTradeParity" --logger "console;verbosity=minimal"` failed before implementation with 14 failed / 0 passed / 0 skipped / 14 total because trade commands and write handlers were absent.
- GREEN: the same `BrowserTradeParity` filter passed after implementation with 14 passed / 0 failed / 0 skipped / 14 total.
- Broader GREEN: `dotnet test BookOfEternityClient.Tests/BookOfEternityClient.Tests.csproj -p:IsTestProject=true --filter "FullyQualifiedName~BrowserMortalWorldWriteService|FullyQualifiedName~BrowserAfterlifeWriteService|FullyQualifiedName~ExplorerWebPromptSession|FullyQualifiedName~BrowserWebUiParity|FullyQualifiedName~BrowserWebUiSmoke|FullyQualifiedName~CommandResult|FullyQualifiedName~Trade" --logger "console;verbosity=minimal"` passed with 268 passed / 0 failed / 0 skipped / 268 total.
