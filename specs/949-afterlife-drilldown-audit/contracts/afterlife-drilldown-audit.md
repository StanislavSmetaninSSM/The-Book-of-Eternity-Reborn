# Afterlife Drill-Down Audit Contract

Source issue: #949 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/949

## Purpose

This contract defines the durable output expected from the #949 afterlife detail drill-down audit and the minimum behavior for any small in-PR read-only detail fixes.

## Audit Artifact Contract

The #949 PR must create or update `docs/audits/afterlife-drilldown-audit.md` with a table or structured sections that cover these candidate groups from the issue body:

1. Guardians: `/guardians` / `/хранители` profile sections, thought/social journals, quests, project links, trade/social state.
2. Abodes: `/abodes` / `/обители` and `/abode_power` / `/сила_обители` details, power history, resident lists, treasury, influence.
3. Soul relics: `/soul_relics` / `/реликвии` detail access from afterlife surfaces.
4. Soul archive: `/archive_candidates` / `/архив_души` entry/candidate/source-life/lore details.
5. Guardian local systems: projects, trade, social, residents, interaction history, pending requests/receipts.
6. Shining Abode systems: gates, politics, factions, projects, trade/forge, treasury, Source of Light, native faction discovery/investment/support/retirement.
7. Spiritual conflict: active conflict overview, special art list/detail, combat log exchange detail, help/tactics detail.
8. Afterlife profile/support surfaces: profiles, threats, chronicles, inbox notifications and linked entities.

Each row/section must include:

- canonical command/surface name and aliases when known;
- current console detail affordance summary;
- current browser detail affordance summary;
- classification: `adequate`, `fixed in #949`, `follow-up required`, or `not applicable`;
- severity: `P0 blocker`, `P1 high`, `P2 medium`, or `P3 low`;
- follow-up issue number/URL for every `follow-up required` gap;
- docs/contract impact statement.

## Small Fix Contract

A small in-PR fix is allowed only when all of these are true:

- The fix is read-only and does not create/modify pending/control files.
- The fix preserves existing overview output.
- The fix uses shared C# command-result/Explorer command patterns or existing browser rendering paths.
- The fix has a RED test or source guard before production code.
- The fix does not change GM-authored state schema, validation, normalizer behavior, or afterlife runtime contracts.

If any condition is false, create a focused follow-up issue instead.

## Follow-Up Issue Contract

For each larger confirmed gap, create a GitHub issue that includes:

- exact command/surface and realm;
- console/browser current behavior;
- desired player-facing detail flow;
- whether afterlife contract docs/tests are expected;
- acceptance criteria that preserve overview output and parity;
- a link back to #949.

Do not close #949 while a confirmed gap is only described in prose without a link.

## Verification Contract

The final implementation must provide evidence for:

- audit artifact coverage guard with non-zero test count;
- any RED/GREEN small-fix tests;
- broader afterlife/browser/console slice;
- docs coverage tests if contract docs changed;
- Spec Kit prerequisite resolution;
- diff whitespace check;
- added-line static/security scan excluding plan/spec prose false positives.
