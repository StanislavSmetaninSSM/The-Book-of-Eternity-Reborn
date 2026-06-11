# Daren Literary Scene Prose Contract

## Contract Purpose

This contract defines the player-facing prose surface for #957. It consumes the #956 Daren narrative spine and updates the existing shared Daren QTE route so console and browser present every current QTE node as a short interactive-book scene rather than a bare mechanic.

## Source and Authority

- Source issue: #957 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/957>
- Parent: #955 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955>
- Prerequisite scene map: #956 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/956>
- Base Daren showcase: #919 — <https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/919>
- Shared route authority: `QteSceneService.GetDarenShowcaseRoute()` in `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Scene map authority: `BookOfEternityClient/Content/DarenQteNarrativeSpine.json`.

## Prose Surfaces

Implementation may update these existing fields only for #957 content:

- `QteOffer.OfferText`
- `QteOffer.IntroNarrative`
- `QteChapter.Narrative` for every Daren chapter/beat
- `QteAction.SuccessText`
- `QteAction.PartialText`
- `QteAction.FailText`

Mechanical labels such as `QteAction.Label` may stay concise mini-game prompts as long as the surrounding chapter narrative and result text carry the story context.

## Required Invariants

- `routeId`/`QteId` remains `daren_qte_showcase`.
- Beat ids, beat order, action ids, QTE check types/configs, routing, score deltas, score model, ending tiers, reward profile writes, and New Game grants remain unchanged.
- Every current beat has chapter prose that communicates:
  - where Daren is or what immediate scene he is in;
  - what is at stake or what danger presses on him;
  - why the upcoming QTE matters.
- Every success/partial/fail text is a short transition or immediate consequence, not just a bare result label.
- Prose remains concise for console. Objective tests should use project-appropriate bounds rather than subjective style scoring.
- Default player-facing prose must not expose raw technical terms such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, or `client-owned`.
- Console and browser receive the same authored prose through shared route data. Do not add duplicate React/browser-only copy for this issue.
- This contract does not add dialogue choices, NPC response variants, expanded branch-specific consequence text, ending/reward presentation changes, broad content quality gates, new QTE mechanics, or a new scenario/dialogue runtime.

## Test Contract

A good #957 regression test should fail when:

- any Daren chapter narrative is empty, too terse, one-sentence mechanical copy, or too long for console;
- any Daren action success/partial/fail text is empty, very terse, or technical/debug-like;
- Daren offer/intro/chapter/action copy leaks forbidden default-UI technical wording;
- route beats/QTE types drift away from `DarenQteNarrativeSpine.json`;
- implementation changes mechanics while claiming to edit prose only.

A good #957 regression test should not fail because of subjective literary preference when all objective player-facing boundaries are satisfied.

## Follow-up Boundaries

- #958 owns NPC cast, dialogue choices, and response variants.
- #959 owns branch-specific consequence variants for choices and QTE performance beyond the existing success/partial/fail surfaces.
- #960 owns endings, epilogues, and reward presentation.
- #961 owns broader content-quality gates for interactive-book presentation.
