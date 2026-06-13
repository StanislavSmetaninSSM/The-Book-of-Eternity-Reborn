# Contract: Daren Renara Voice Success Result Aftermath

## Scope

This contract applies only to the shared C# Daren QTE route data for issue #1009:

- Chapter/beat: `ward_steward_parley` / "Голос Ренары"
- Action: `ward_steward_parley_action` / "Ответить Ренаре Вардовой"
- Result surface: `success`

It does not change route ids, command surfaces, browser endpoints, persistence, reward/profile writes, GM-facing contracts, or frontend runtime behavior.

## Authored Prose Requirements

The success result text must:

1. Be Russian, in-world, player-facing dark-fantasy prose.
2. Read as a substantial post-QTE aftermath insert, not as a one-sentence result notification.
3. Keep Daren as the active POV protagonist.
4. Show the clean/best outcome: Daren's false-seal explanation is accepted, Renara Wardova does not escalate, the house quiets the extra seal, and risk is reduced.
5. Use concrete social and sensory details: Renara's ward voice, runes/seals/glass, dust/stone/cold light, Daren's breath/throat/hands, and the listening house.
6. Bridge toward the next heavy-grate beat without rewriting `physical_pressure` or altering route order.
7. Avoid default player-facing implementation terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, raw ids, or test/agent language.

## Invariants

The implementation must preserve:

- route id and Daren showcase availability;
- beat id `ward_steward_parley` and title "Голос Ренары";
- action id `ward_steward_parley_action` and label "Ответить Ренаре Вардовой";
- check type `PrecisionChoice`, Wisdom characteristic, base difficulty, and config choice/outcome mapping;
- routing targets for success/partial/fail;
- score deltas and reward/profile/New Game behavior;
- partial and fail result texts except for a documented minimal connective fix required by tests;
- shared C# route authority consumed by both console and browser;
- absence of new dialogue runtime, state files, endpoints, frontend-only story forks, or GM-authored contract surfaces.

## Verification Contract

A compliant change includes:

- a RED/GREEN focused guard in `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs` for `ward_steward_parley_action` success aftermath;
- focused Daren tests passing;
- affected Daren/QTE/docs/browser C# slice passing;
- client and test-project builds passing;
- `git diff --check origin/main...HEAD` passing;
- added-line static scan over non-Spec changed files showing no secrets/injection/eval/deserialization/SQL-formatting findings;
- diff review showing only expected code/test/Spec Kit files changed.
