# Contract: Daren Result Aftermath Rewrite

**Feature**: `specs/990-daren-approach-fail/`
**Source issue**: [#990](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/990)

## Authority Boundary

This feature changes only client-owned authored showcase prose for Daren's standalone QTE route. The shared C# route data remains the single authority consumed by both console and browser.

No runtime contract is added or changed:

- no game-state field;
- no pending/control file;
- no validation or normalizer rule;
- no endpoint/DTO/browser API shape;
- no reward/profile/New Game grant behavior;
- no GM-authored prompt/example contract.

## Target Surface

- Route chapter: `approach_manor` / "Подступ к поместью"
- Action: `approach_manor_action` / "Выбрать тень у старой липы"
- Result grade: `fail`
- Current text: `Дарен теряет драгоценный миг у освещённой калитки, и патруль начинает смотреть в его сторону.`

## Required Preservation

Implementation and tests must preserve:

- route id, chapter id, beat title, action id, and action label;
- `BranchChoice` check type/config and choice ids/labels/grades;
- success/partial/fail routing targets and grade identity;
- score deltas and reward/profile/New Game behavior;
- #988 success and #989 partial text;
- downstream #991-#1008 result texts;
- console/browser shared consumption of the same C# route data;
- frontend files, runtime state, endpoints, and GM-facing docs.

## Literary Acceptance

The new fail text must:

- be substantial Russian dark-fantasy aftermath prose, not a mechanical notification;
- keep Daren as active POV;
- make the fail grade clear through concrete danger: exposed light, broken stealth, guard/patrol/dog/lantern/witness attention, evidence, pursuit, or named suspicion;
- keep the route moving toward `informant_parley` / Mira, but under pressure from the failed approach;
- avoid default player-facing technical/meta terms: `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`.

## Verification Contract

- A focused `DarenQteShowcaseTests` guard must fail against the old one-sentence fail text before the production rewrite and pass after it.
- The guard must pin #988 success, #989 partial, and downstream #991-#1008 surfaces out of scope.
- The guard must verify mechanics invariants, not only prose length.
