# Console Live Playtest #1179

## Scope

Tracked issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1179

Goal: run a short Mortal World console adventure with the real console client, Agent Console, GM daemon, and a Codex bridge launched as `codex --dangerously-bypass-approvals-and-sandbox`. QTE and browser surfaces were intentionally excluded.

Run root: `C:\Temp\boe-live-e2e-1179-20260620-223955`

Seed session: `E:\Games\worktrees\boe-1174-console-polish-9\FileSystemExample\game_session`

Disposable session: `C:\Temp\boe-live-e2e-1179-20260620-223955\game_session`

Commit under test: `598bb369db8c1b12c29c5f1efc598b57293f55b0`

Agent Console: `http://127.0.0.1:60547`

## Setup Notes

- Main menu was continued through Agent Console.
- The GM bridge initially stopped on the Codex trust prompt. Sending Enter through the launcher resolved it, then `ready` confirmed the bridge was usable.
- QTE events were disabled for this run.
- The run was stopped after artifacts were preserved; client, daemon, helper, and shell processes were terminated.

## Command Sweep

The following read-only Mortal World commands were exercised before live play:

`/статус`, `/инв`, `/квесты`, `/карта`, `/эффекты`, `/навыки`, `/статы`, `/нпс`, `/фракции`, `/новости_мира`, `/книги`, `/кодекс`, `/хроника`, `/рассказ`, `/достижения`, `/жизни`, `/локации`, `/где_я`, `/погода`, `/транспорт`, `/правила_мира`, `/доступ_к_хранилищам`, `/взаимодействия`, `/реликвии`, `/квесты_души`, `/душа`, `/перья`, `/моды`, `/извечные_хранители`.

Result: all commands returned to the harness without a hang. The sweep is saved as `mortal-command-pass-summary.json`.

Notable friction from the sweep:

- `/моды` exposed file names such as `.md`, `.json`, `.txt` in player-facing output.
- `/карта` exposed raw map labels such as `Mortal World`, current node ids, and `output/map_viewer.html`.
- `/локации` exposed raw link state `[Unknown]`.
- SelectionPrompt drilldowns are still weakly represented in Agent Console snapshots: human terminal use is possible, but autonomous selection through snapshots is incomplete.

Fixed in this branch after the live run:

- Map renderer localizes realm labels and shows the current location display name instead of raw current node id.
- Legacy `/карта` no longer prints the generated HTML path to the player.
- Location link states are localized or hidden when they are non-informative (`Unknown`).
- `/моды` hides technical file names in overview/action labels and suppresses placeholder `Description`.

## Live Adventure

Turn 1 player action:

`Осторожно осматриваю письмо: печать, бумагу, запах, следы подбросившего. Не трогаю голыми руками, сравниваю знак с тем, что помню о фамильной библиотеке.`

Observed result:

- The GM produced readable narrative around the letter and the protagonist's room.
- The daemon issued one repair attempt before the turn settled.
- Post-turn command checks did not hang.

Turn 2 player action:

`Беру письмо с собой, выхожу в коридор и ищу ближайшего слугу или охранника, чтобы тихо узнать, кто был у моих покоев ночью.`

Observed result:

- The GM produced readable narrative, moved the player to `Коридор поместья Вальмонт`, and introduced Ivetta/Rolan-style social clues.
- `/карта` and `/где_я` reflected the new location.
- `/нпс`, `/хроника`, and `/квесты` did not reflect the newly introduced people/events/quest thread.

## Follow-Up Issues

- #1181: GM turn facts do not persist into NPC, chronicle, and quest surfaces.
- #1182: Agent Console command drilldowns and options menu are not fully observable for autonomous tests.

## Current Assessment

The console client survived the short live run and the ordinary command sweep without hangs. The strongest remaining risk is not terminal rendering anymore, but consistency between accepted GM narrative and canonical state surfaces. A player can read that the story advanced, then inspect commands that still show the old world.

For the 9/10 console target, the next pass should focus on:

- enforcing persistence of narrative facts into player-visible state;
- making Agent Console snapshots rich enough for unattended drilldown testing;
- running another live test after #1181 and #1182 are addressed.

## Second Live Follow-Up Run

Date: 2026-06-21

Run root: `C:\Temp\boe-live-e2e-1174-final-20260621-060857`

Seed session: `E:\Games\worktrees\boe-1174-console-final-sweep\FileSystemExample\game_session`

Disposable session: `C:\Temp\boe-live-e2e-1174-final-20260621-060857\game_session`

Commit under test at launch: `bbb2c2d6373c3c7a01df87209c27a98da8553c9c`

Agent Console: `http://127.0.0.1:52884`

Configuration notes:

- `codex --dangerously-bypass-approvals-and-sandbox` was used for the hidden GM bridge.
- QTE events, image generation, music, and sound were disabled for the run.
- The bridge was launched through the project launcher so Codex CLI had a real terminal.
- The run was stopped after artifact capture; client and daemon PIDs from `run-metadata.json` were terminated.

### Second Command Sweep

The following read-only Mortal World commands were exercised:

`/статус`, `/инв`, `/квесты`, `/карта`, `/эффекты`, `/навыки`, `/статы`, `/нпс`, `/фракции`, `/новости_мира`, `/книги`, `/кодекс`, `/хроника`, `/рассказ`, `/достижения`, `/жизни`, `/локации`, `/где_я`, `/погода`, `/транспорт`, `/правила_мира`, `/доступ_к_хранилищам`, `/взаимодействия`, `/реликвии`, `/квесты_души`, `/душа`, `/перья`, `/моды`, `/извечные_хранители`, `/опции`.

Result: all 30 commands returned without hangs or quality issue markers in the sweep summary `mortal-command-final-sweep-summary-real.json`.

Notable second-run findings:

- `/извечные_хранители` exposed an empty technical-looking preset/manifest table when the sandbox had no system-guardian presets.
- Turn 2 introduced Мартен Рош as journal-only NPC state. `/нпс` showed the fallback table, but there was no journal drilldown action/details path, so the table was a player-facing dead end.

Both findings were fixed after the run with RED/GREEN tests:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore -p:IsTestProject=true --filter "ExecuteAsync_NpcWithRepositoryJournalFixtureAndNoCore_ShowsKnownJournalNotes|ExecuteAsync_NpcJournalFallbackDetail_ShowsFullJournalEntriesAndBackAction|ExecuteAsync_SystemGuardiansWithEmptyLibrary_HidesLocalDirectoryPath"
```

Result: 3 passed, 0 failed.

### Second Live Adventure

Turn 1 player action:

`Осторожно осматриваю руническую перчатку и комнату: ищу следы недавнего вмешательства, проверяю узор на перчатке, вспоминаю, где именно нашёл её в фамильной библиотеке, и не зову слуг, пока не пойму, опасна ли находка.`

Observed result:

- The GM produced readable narrative around the glove, the room, and damaged traces.
- No player repair action was required.
- `/инв` and `/кодекс` detail actions exposed the glove and the newly relevant clue context.

Turn 2 player action:

`Сначала прячу руническую перчатку в футляр, чтобы больше не портить следы, затем зову самого доверенного слугу и спокойно расспрашиваю: кто входил в мои покои ночью, кто нёс письмо, и не видел ли кто-нибудь людей у фамильной библиотеки.`

Observed result:

- The GM produced readable narrative and introduced Мартен Рош as a cautious witness source.
- The validator requested repair because the initial response over-declared NPC scope; the GM repaired by keeping Мартен as player-facing journal state instead of claiming a canonical `npc_core` actor.
- `/нпс` and `/где_я` reflected Мартен and the library clue after repair.
- `/квесты` and `/хроника` did not change because the repaired GM output did not create a quest or chronicle update for this turn.

### Second-Run Assessment

The second run satisfies the qualitative 9/10 check for the non-QTE console scope: ordinary commands are understandable, the bridge can carry a short live scene, and the remaining issues found during play were minor player-facing dead ends rather than blocker bugs. The main residual cost is bridge latency: bootstrap and GM turns are slow enough that future live tests should continue to record timing and repair counts.
