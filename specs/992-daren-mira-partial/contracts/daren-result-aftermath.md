# Contract: Daren `informant_parley_action` Partial Aftermath

Source issue: [#992](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/992). Parent issue: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955). Source scene: [#970](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/970).

## Authored Surface

- Route chapter: `informant_parley` / `Шёпот Миры`.
- Action id: `informant_parley_action`.
- Result grade: `partial` / `частичный успех`.
- Shared authority: `BookOfEternityClient/Services/QteSceneService.Daren.cs`.
- Test authority: `BookOfEternityClient.Tests/DarenQteShowcaseTests.cs`.

## Allowed Change

Replace only the current partial result prose:

> Мира берёт монету Дарена, но отвечает коротко: ключник устал, а имя капитана она оставляет за следующим долгом.

with a substantial Russian dark-fantasy aftermath insert where the bargain works enough to move Daren forward, while a visible cost, doubt, debt, withheld truth, source risk, delay, or future consequence remains.

## Required Invariants

The implementation must preserve:

- route id and chapter order;
- action id, label, check type `PrecisionChoice`, characteristic `Wisdom`, base difficulty, choice ids/labels/grades, and choice config;
- routing targets and success/partial/fail grade identities;
- score deltas, reward/profile/New Game behavior, endpoints, runtime state, browser/console shared authority, and frontend files;
- #991 success and #993 fail result prose;
- previous `approach_manor_action` result prose for #988/#989/#990;
- downstream result prose for #994-#1008.

## Player-Facing Copy Rules

The partial aftermath must:

- keep Daren as active point of view;
- show Mira as a personified informant under social pressure, not a result popup;
- include mixed-outcome consequences: coin/debt/bargain, incomplete information, retained leverage, suspicion, source exposure pressure, delay, or future cost;
- still bridge toward `gadget_infiltration_action` / `Крюк и леска`;
- avoid default player-facing technical/meta words such as `GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, JSON/file/debug framing, or implementation terminology.

## Verification Contract

Before implementation completion, evidence must include:

- RED focused Daren guard against the old partial text;
- GREEN focused Daren guard after the rewrite;
- affected Daren/QTE/docs/browser C# slice;
- client and test-project builds;
- `git diff --check origin/main...HEAD`;
- added-line static scan with no hardcoded secrets, shell injection, eval/exec, unsafe deserialization, or SQL formatting matches;
- Spec Kit `tasks.md` / checklist reconciliation with evidence counts.
