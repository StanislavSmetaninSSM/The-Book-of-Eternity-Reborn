# Daren NPC Dialogue Cast Contract

## Contract Purpose

This contract defines the #958 people-driven interaction slice for Daren's standalone QTE heist. It consumes the #956 scene map and #957 shared prose, then adds NPC cast, dialogue/social-choice moments, and response variants through the existing shared QTE route contract.

## Source and Authority

- Source issue: #958 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958>
- Parent: #955 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955>
- Narrative spine: #956 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956>
- Shared route prose: #957 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957>
- Base Daren showcase: #919 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919>
- Shared route authority: `QteSceneService.GetDarenShowcaseRoute()` in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Scene map authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.

## Cast Contract

The route must expose at least four named/personified figures aligned to the #956 cast slots:

| Cast slot | Required role | Player-visible expectation |
| --- | --- | --- |
| `contact_informant` | contact/informant who gives timing, rumor, or first pressure | Visible in early route/choice copy as a person, not just "hint" or "information". |
| `estate_staff_guard` | servant, tired watchman, guard, or staff witness | Visible in interior dialogue/social pressure or reactions. |
| `magical_security_authority` | house mage, steward, ward-bound presence, or house representative | Visible around rune/alarm/staff security and able to respond to answers or mistakes. |
| `pursuit_figure` | named pursuer who personalizes the chase | Visible in pursuit/chase/hideout pressure. |

Implementation may represent cast names/personas in private helper data, existing route copy, or additive `DarenQteNarrativeSpine.json` fields. It must not require a new public runtime model unless tests prove a tiny shared DTO extension is necessary.

## Dialogue / Choice Contract

- Dialogue/social-choice moments must remain normal QTE route chapters/actions.
- Use existing check types only. `PrecisionChoice` is preferred for player-selectable answer options because console and browser already support choices with labels/descriptions/hints. `BranchChoice` may be used only when a predetermined branch is intentionally authored and still player-facing through surrounding route copy.
- Each player-visible dialogue/social-choice moment needs:
  - a chapter/action label or narrative that names the NPC/social pressure;
  - answer option labels/descriptions when the check is interactive;
  - success/partial/fail response text that differs and reads as an NPC reaction or social consequence;
  - routing/score data through existing `QteRouting` and `ScoreDeltas`.
- At least one dialogue/social-choice outcome must change an existing metric such as stealth, pursuit control, evidence, loot, or hideout safety.
- Later route copy or result text must reference at least one earlier NPC/social consequence so the player sees the interaction matter.

## Required Invariants

- `routeId`/`QteId` remains `daren_qte_showcase`.
- The original #957 heist beats remain present in their original relative order, even if #958 inserts dialogue/social-choice beats between them.
- Reward profile writes, ending-tier thresholds, New Game grants, ordinary campaign mutation boundaries, and Daren standalone-showcase semantics remain unchanged.
- Console and browser consume the same authored content through shared C# route data.
- No new dialogue service, dialogue state file, endpoint, React-only story fork, GM-authored campaign contract, or QTE check type is introduced.
- #959 branch-specific expanded consequences, #960 ending/reward presentation, and #961 broad quality gates remain follow-up work.

## Test Contract

Good #958 regression tests should fail when:

- any required cast slot lacks a concrete named/personified figure;
- fewer than three dialogue/social-choice moments exist;
- dialogue/social-choice moments are not implemented through existing QTE route actions/checks;
- player-selectable dialogue choices lack labels/descriptions/hints;
- success/partial/fail response variants are empty, identical, or do not mention/respond to NPC/social context;
- no dialogue/social-choice outcome affects existing score/risk metrics;
- later route copy never references an earlier NPC/social consequence;
- original Daren heist beats disappear or reorder unexpectedly;
- implementation introduces a new dialogue runtime, state file, endpoint, browser-only copy fork, or new QTE check type.

Good tests should not attempt to judge literary taste beyond objective structure, boundary, and player-facing copy requirements.

## Follow-up Boundaries

- #959 owns deeper branch-specific consequence variants for choices and QTE performance.
- #960 owns endings, epilogues, and reward presentation.
- #961 owns broad content-quality gates for the whole interactive-book presentation.
