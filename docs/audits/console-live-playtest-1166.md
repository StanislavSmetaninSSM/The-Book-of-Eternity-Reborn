# Console Live Playtest 1166

Source issue: #1166 - second live console playtest.

Date: 2026-06-20.

## Scope

This was a non-browser live console playtest using:

- Console client with Agent Console enabled.
- Disposable copied session: `C:\Temp\boe-live-e2e-1163-20260620-183207\game_session`.
- GM daemon and bridge launched through `bookofeternity.ps1`.
- GM command: `codex --dangerously-bypass-approvals-and-sandbox`.

Temporary run artifacts were kept outside the repository.

## Route Attempted

1. Started the console client in Agent Console mode.
2. Continued from the Mortal World test state as Asuran de Valmont.
3. Sent a normal player action:
   `Осторожно осматриваю письмо, не вскрывая печать, и сравниваю символ на печати с узорами рунической перчатки.`
4. Waited for GM bridge / daemon completion.
5. Inspected Agent Console snapshot, daemon logs, ready/output files, and error log.

The run did not continue into a full guardian/NPC/QTE/afterlife loop because the first accepted turn exposed two live-test blockers that needed fixes or follow-up.

## Findings

### Fixed In This Branch

- The test Mortal session had a legacy Soul Relic payload in `game_state/meta/soul_state.json`: `id` and `tier` instead of canonical `relicId` and `rarity`.
- The live turn completed from the GM side, but the client then failed strict canonical normalization after 430.3 seconds.
- `ValidationService` now reports `soul_relic_invalid_canonical_shape` before the game reaches the accepted-turn normalizer.
- The reusable Mortal command display save had the same legacy relic payload and was repaired.
- The local working `BookOfEternityClient/game_session` copy was repaired on disk.
- Agent Console now publishes a `game-loop-error` snapshot when the game-loop catch block shows an error screen, so automation does not keep seeing a stale prompt.

### Follow-Up Issues

- #1170 - GM bridge live Codex turn is too slow and sees coding-agent repo context.
- #1171 - GM daemon stdout logs garble Cyrillic player actions.

## Evidence

- Daemon completed the first GM turn: `[18:40:51][TURN] Done (430.3s)`.
- Ready file appeared: `ready/turn_complete.json`.
- GM output appeared: `output/narrative_response.json`.
- Client error before fix:
  `game_state/meta/soul_state.json current soulRelics must already be a canonical object with equipped/stored JsonArray collections when present.`
- Agent Console snapshot before the error-snapshot fix still showed the old `game-loop` prompt; stdout contained the actual error screen.

## Playability Assessment

Console command output, using prepared reusable saves, is now in the 8/10 range for normal inspection workflows: commands render, broad Mortal/Chaos/Shining sweeps pass, and the most obvious raw JSON/default audit leakage has been moved out of normal routes or filed as follow-up.

The live GM bridge route is not yet 8/10. With Codex as GM it is currently closer to 5-6/10 for real playtesting because one ordinary turn took over seven minutes and the child Codex process loaded coding-agent repository context. That is infrastructure friction, not a console rendering regression, and it is tracked separately in #1170.

## Next Live Test Gate

Before claiming a full short-adventure live pass, rerun the route after #1170 or an equivalent GM isolation fix:

- Mortal exploration and command checks.
- NPC/guardian interaction.
- Inventory/status/books/effects/factions/world-news drilldowns.
- One QTE or simple challenge.
- End life, receive rewards, inspect afterlife state.
- Start or prepare the next life.
