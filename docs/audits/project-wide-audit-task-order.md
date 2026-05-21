# Project-Wide Audit Task Closure Order

Tracking task: #626

## Rules

- Record audit findings in `docs/audits/project-wide-audit-findings.md` before fixing them.
- If a finding requires implementation, create or link a dedicated GitHub issue before changing code, prompts, docs, tests, or contracts.
- Do not mix broad audit work and fixes in one untracked change set.
- Prefer closing one audit slice at a time, then merging fixes, then continuing the next slice from a clean `main`.

## Primary Audit Order

1. #626 - Create unified project-wide audit findings ledger and taxonomy.
   This must go first because every later audit writes into the shared ledger.

2. #631 - Test coverage, fixtures, encoding and CI reliability audit.
   Run early because weak tests and encoding defects can invalidate later audit confidence.

3. #627 - Runtime lifecycle, persistence, rollback and save-state integrity audit.
   This protects save data and turn lifecycle before deeper contract work.

4. #633 - Data schema, backward compatibility and migration audit.
   Run after lifecycle because schema defaults, malformed states, and legacy compatibility directly affect save integrity.

5. #628 - Validation, normalization and authority-path consistency audit.
   Run after schema/lifecycle so validator findings can be checked against known ownership and migration rules.

6. #629 - GM prompts, examples and documentation versus runtime logic audit.
   Run after validation because prompt/docs drift must be compared against confirmed runtime contracts.

7. #630 - Player UI, command help and Russian localization audit.
   Run after prompts/docs so player-facing terminology aligns with the finalized gameplay language.

8. #632 - Gameplay completeness and balance audit across Mortal World and Afterlife.
   Run after contract/UI audits so gameplay gaps are judged against the actual implemented and documented surfaces.

9. #636 - Security, safety and destructive-action guardrail audit.
   Run after gameplay audit because destructive gameplay outcomes and unsafe technical side effects need to be separated.

10. #635 - Local tools and browser integration audit: WebUI, map, images, math, CLI bridge.
    Run after core runtime/UI audits so local tools reuse stable command, DTO, locking, and localization rules.

11. #634 - Architecture, service boundaries and refactoring opportunity audit.
    Run last because refactoring should be based on concrete defects and recurring patterns discovered by the previous audits.

## Fix Handling Order

1. Fix P1/data-loss/build-breaking findings immediately in dedicated issues.
2. Fix validation/runtime authority defects before prompt-only or UI-only defects.
3. Fix prompt/docs drift in the same branch as runtime contract changes when the GM must know the rule.
4. Fix UI/localization defects after behavior is stable, unless the UI defect blocks repair or play.
5. Batch low-risk documentation-only fixes only when they do not obscure runtime changes.

## Feature Work After Audit Stabilization

1. Complete foundational WebUI/shared-command infrastructure before browser-only feature screens.
2. Build the shared map viewer foundation before Mortal, political, Chaos Sea, or Shining Abode projections.
3. Implement math assistant contract before integrating it into combat/economy prompts.
4. Expand image viewing/export after local browser/static asset boundaries are stable.
5. Continue Saref/main-story work after memory-scene and Guardian dossier contracts are stable.

## Checkpoint Template

Use this format in `docs/audits/project-wide-audit-findings.md` when an audit slice finds no new issues:

| Date | Issue | Scope | Result | Verification |
| --- | --- | --- | --- | --- |
| YYYY-MM-DD | #NNN | Reviewed files/systems. | No additional discrete defect found. Residual risks listed here. | Commands/tests/manual checks. |
