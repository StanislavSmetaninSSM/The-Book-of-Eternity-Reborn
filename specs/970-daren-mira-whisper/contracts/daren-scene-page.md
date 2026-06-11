# Contract: Daren `informant_parley` Scene Page

## Source

- GitHub issue: #970 — [QTE/Daren] Rewrite scene 02 “Шёпот Миры” as a full literary page.
- Parent: #955 — Daren interactive-book umbrella.

## Runtime Authority

- Shared C# route data remains the only player-facing source for this scene:
  - `BookOfEternityClient/Services/QteSceneService.Daren.cs`
  - Beat id: `informant_parley`
  - Title: `Шёпот Миры`
- Console and browser must consume this shared route/DTO data; no browser-only or console-only prose fork is permitted.

## Required Scene Qualities

The `informant_parley` narrative must:

1. Be substantial Russian dark-fantasy prose, not a synopsis or briefing.
2. Keep Daren as the protagonist and active point of view.
3. Include Mira as a present named NPC/contact, with visible relationship/subtext and tension.
4. Include dialogue or voiced exchange between Daren and Mira that leads naturally toward the existing precision-choice action.
5. Include the rear-road awning or equivalent meeting place, wet/night atmosphere, Mira's ribbon or equivalent identifying detail, Daren's body language/observation/intent, and stakes around guard/source exposure/pursuit.
6. Avoid verbatim copying of Stanislav's example while matching its level of scene construction.
7. Avoid default player-facing technical language such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, score/debug framing, or implementation terms.
8. Preserve all existing mechanics: beat id, title, action id, check type, choice ids/outcomes, routing, score deltas, reward behavior, profile writes, endpoints, and runtime state.

## Out of Scope

- Other Daren scene rewrites (#971-#983).
- Parent #955 closure.
- QTE engine/runtime changes.
- New dialogue/state systems.
- Reward/profile/New Game grant changes.
- React/frontend-only text or UI changes unless a shared-rendering bug is separately proven.
