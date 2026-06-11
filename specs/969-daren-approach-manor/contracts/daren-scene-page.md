# Contract: Daren `approach_manor` Scene Page

## Source

- GitHub issue: #969 — [QTE/Daren] Rewrite scene 01 “Подступ к поместью” as a full literary page.
- Parent: #955 — Daren interactive-book umbrella.

## Runtime Authority

- Shared C# route data remains the only player-facing source for this scene:
  - `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Beat id: `approach_manor`
  - Title: `Подступ к поместью`
  - Start chapter id: `approach_manor`
- Console and browser must consume this shared route/DTO data; no browser-only or console-only prose fork is permitted.

## Required Scene Qualities

The `approach_manor` narrative must:

1. Be substantial Russian dark-fantasy prose, not a synopsis or briefing.
2. Keep Daren as the protagonist and active point of view.
3. Include the target place and approach pressure: manor wall/grounds, wet grass/night, lantern/patrol/guard light, and a shadowed route such as old linden/gate/wall approach.
4. Include movement, bodily tension, observation, or decision-making by Daren.
5. Lead naturally into the existing QTE choice without saying `QTE`, `debug`, `API`, `DTO`, `GM`, `Spec Kit`, score/debug framing, or implementation language.
6. Preserve all existing mechanics: beat id, title, action id, check type, routing, score deltas, reward behavior, profile writes, endpoints, and runtime state.

## Out of Scope

- Other Daren scene rewrites (#970-#983).
- Parent #955 closure.
- QTE engine/runtime changes.
- New dialogue/state systems.
- Reward/profile/New Game grant changes.
- React/frontend-only text or UI changes unless a shared-rendering bug is separately proven.
