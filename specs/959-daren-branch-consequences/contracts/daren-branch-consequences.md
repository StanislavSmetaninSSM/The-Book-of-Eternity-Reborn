# Daren Branch Consequences Contract

## Contract Purpose

This contract defines the #959 branch-consequence slice for Daren's standalone QTE heist. It consumes the #956 scene map, #957 shared prose, and #958 NPC/dialogue/cast work, then deepens choices and QTE grades through existing shared QTE route/action/result/score data.

## Source and Authority

- Source issue: #959 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/959>
- Parent: #955 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955>
- Narrative spine: #956 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956>
- Shared route prose: #957 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957>
- Dialogue/cast prerequisite: #958 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/958>
- Base Daren showcase: #919 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919>
- Shared route authority: `QteSceneService.GetDarenShowcaseRoute()` in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Scene map authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.

## Branch-Consequence Contract

Branch-specific consequences must remain inside the existing QTE route contract:

- Chapter narrative text may set up the branch pressure.
- Action success/partial/fail text must describe concrete consequences.
- Existing `ScoreDeltas` / score metrics may represent clean progress, noise, evidence, pursuit control, loot, or hideout safety.
- Existing routing fields may continue or fail the route according to current scenario design.
- `DarenQteNarrativeSpine.json` may record consequence hooks and carry-forward notes as durable authoring context.

No new public consequence engine, campaign-state branch memory, state file, endpoint, React-only consequence copy, or QTE check type is allowed for this slice.

## Required Consequence Types

The route must contain objective evidence for several categories of consequence:

| Category | Required player-visible evidence |
| --- | --- |
| Strong performance | Success text shows clean movement, reduced pressure, stronger position, better clue, or improved control. |
| Partial performance | Partial text shows a cost, delay, suspicion, noisy improvisation, weaker clue, or compromised route while still distinguishing it from success and fail. |
| Poor performance | Fail text names specific danger, noise, evidence, detour, pursuit escalation, or reduced control; non-terminal branches remain playable where existing route design allows. |
| Choice consequence | At least one #958 dialogue/planning choice or social result is referenced by later route prose/result text. |
| Carry-forward echo | Later route text references at least several earlier decisions/results by NPC, route, clue, ward, witness, evidence, or pursuit pressure. |

## Required Invariants

- `routeId`/`QteId` remains `daren_qte_showcase`.
- The original #957 heist beats remain present in their original relative order.
- #958 dialogue/cast/social-choice moments remain shared route content, not a replaced dialogue system.
- Reward profile writes, ending-tier thresholds, New Game grants, ordinary campaign mutation boundaries, and standalone-showcase semantics remain unchanged.
- Console and browser consume the same authored consequence content through shared C# route data.
- #960 ending/epilogue/reward presentation and #961 broad quality gates remain follow-up work.

## Test Contract

Good #959 regression tests should fail when:

- key Daren actions have identical or generic success/partial/fail result text;
- result text describes only pass/fail mechanics and not a story consequence;
- fewer than several earlier choices/results are echoed later in the run;
- no #958 dialogue/planning decision affects later consequence prose;
- non-terminal poor outcomes collapse into generic failure instead of specific increased pressure where the route continues;
- consequences are implemented in a browser-only, console-only, or new state/runtime fork;
- the route id, reward semantics, original beat order, or existing QTE check-type boundaries drift unexpectedly;
- the spine no longer records #959 source/consequence handoff truth.

Good tests should not attempt to judge literary quality beyond objective branch distinction, carry-forward, contract, and player-facing copy requirements.

## GM-Contract Boundary

#959 is client-owned Daren showcase content. It should not change GM-authored QTE contract fields, validation rules, examples, prompts, or campaign pending/control state. If implementation changes a GM-authored QTE contract or validation rule, that is a scope expansion and must update `CLI_API_Specification.md`, `Rules/Block_CLI_QTE.txt`, `Examples/E_CLI_QTE_Offer.txt`, and relevant documentation/source-guard tests in the same PR.

## Follow-up Boundaries

- #960 owns endings, epilogues, and reward presentation.
- #961 owns broad content-quality gates across the interactive-book presentation.
- Future browser visual/presentation polish may add screenshots or visual-smoke evidence, but #959 should not require React changes when shared route data already carries the consequence text.
