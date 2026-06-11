# Contract: Daren `gadget_infiltration` Scene Page

## Source

- GitHub issue: #971 — [QTE/Daren] Rewrite scene 03 “Крюк и леска” as a full literary page.
- Parent: #955 — Daren interactive-book umbrella.

## Runtime Authority

- Shared C# route data remains the only player-facing source for this scene:
  - `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Beat id: `gadget_infiltration`
  - Title: `Крюк и леска`
- Console and browser must consume this shared route/DTO data; no browser-only or console-only prose fork is permitted.

## Required Scene Qualities

The `gadget_infiltration` narrative must:

1. Be substantial Russian dark-fantasy prose, not a synopsis or briefing.
2. Keep Daren as the protagonist and active point of view.
3. Include the tower wall/cold stone, balcony/courtyard space, folding hook, line/cord/metal, Daren's hands/body/ascent preparation, and guard/sound/light stakes.
4. Lead naturally into the existing hook launch action without saying `QTE`, `debug`, `API`, `DTO`, `GM`, `Spec Kit`, score/debug framing, or implementation language.
5. Preserve all existing mechanics: beat id, title, action id, check type/config, routing, score deltas, reward behavior, profile writes, endpoints, and runtime state.

## Out of Scope

- Other Daren scene rewrites (#972-#983).
- Parent #955 closure.
- QTE engine/runtime changes.
- New gadget mechanics or inventory systems.
- Reward/profile/New Game grant changes.
- React/frontend-only text or UI changes unless a shared-rendering bug is separately proven.
