# Contract: Daren Heavy-Grate Fail Result Aftermath

Source issue: [#1014](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1014)
Parent: [#955](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/955)
Scene prerequisite: [#977](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/977)
Sibling results: [#1012](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1012), [#1013](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1013)

## Result Surface

- Route data authority: `BookOfEternityClient/Services/QteSceneService.Daren.cs`
- Chapter/beat: `physical_pressure` / `Тяжёлая решётка`
- Action: `physical_pressure_action`
- Check type: `MashInput`
- Result grade: `fail`
- Presentation authority: shared C# route data consumed by both console and browser surfaces.

## Must Preserve

- Route id, beat id, beat order, title, action id, action label, check type, characteristic, difficulty, config, routing targets, grade identities, score deltas, reward tier thresholds, persistent profile semantics, New Game grant behavior, endpoints, runtime state files, and browser/console frontend boundaries.
- Existing success and partial result prose from #1012/#1013.
- Parent #955 remains open until all child Daren literary-page/result tasks are closed and verified.

## Must Change

- Replace only the `fail` result prose with a substantial Russian dark-fantasy aftermath insert that reads as dangerous failure: iron/stone crash, alarm/noise/evidence/witness/pursuit pressure, Daren's compromised salvage of the staff/case, bodily pressure, and continuity into the next alarm-pulse corridor beat.

## Must Not Expose In Default Prose

`GM`, `DTO`, `API`, `endpoint`, `debug`, `Spec Kit`, `manual-grade`, `client-owned`, `QTE`, `score`, raw file paths, or implementation/agent terminology.

## Acceptance Evidence

A focused `DarenQteShowcaseTests` guard must fail on the current terse fail result, pass after the rewrite, and assert both literary motifs and unchanged mechanics.
