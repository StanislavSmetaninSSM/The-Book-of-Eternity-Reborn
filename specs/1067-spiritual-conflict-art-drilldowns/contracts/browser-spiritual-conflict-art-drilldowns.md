# Contract: Browser Spiritual Conflict and Art Drill-Downs

Source issue: #1067 — https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1067

Origin audit: #949 AFD-006 — `docs/audits/afterlife-drilldown-audit.md`

## Purpose

This contract defines the player-facing Browser Client command-result behavior for read-only selected-detail actions on existing spiritual-conflict surfaces. It is a presentation/detail-action contract only. It does not change canonical afterlife state schemas, spiritual-combat dice/reward mechanics, local write contracts, pending/control files, validation, normalizers, or GM-facing authoring contracts.

## Surfaces

- `/spiritual_conflict` / `/духовный_конфликт`
- `/spiritual_combat_log` / `/журнал_духовного_боя`
- `/spiritual_arts` / `/духовные_искусства`
- `/spiritual_combat_help` / `/духовный_бой` only as explanatory context/help where useful

## Overview Action Contract

Default browser command results for the scoped overview surfaces should expose read-only action metadata for concrete visible rows when canonical state contains resolvable targets:

1. Active conflict exchange rows from `/spiritual_conflict`.
2. Combat-log exchange rows and recent-conflict rows from `/spiritual_combat_log`.
3. Spiritual-art rows from `/spiritual_arts`.

The action label/copy must be Russian/in-world and should communicate inspection rather than mutation, for example “Осмотреть обмен”, “Разобрать запись боя”, or “Осмотреть искусство”. The exact action metadata shape should follow existing #1063-#1066 browser command-result detail patterns.

## Selected Detail Contract

A selected detail result must:

- Preserve or clearly link back to the overview context.
- Resolve the selected row from canonical state using a stable id if one exists; use a safe index only when the state has no durable id and stale-index tests cover the behavior.
- Render player-facing Russian/in-world copy.
- Include relevant available context without inventing missing data:
  - exchange/log actor and opposition/target;
  - action/intention;
  - dice/roll or contest context when present;
  - position and tension changes when present;
  - cost/action-point/spiritual-power context when present;
  - outcome/resolution and reward/reason context when present;
  - spiritual-art rank/level/effect/cost/availability when present.
- Suppress hidden, gm-only, secret, internal, or unsupported fields in ordinary default mode.

## Missing, Stale, Sparse, and Malformed State

When the selected target cannot be resolved safely, the result must show an explicit player-facing unavailable state. Ordinary default output must not include:

- raw JSON;
- raw file names or local paths such as `game_state/`;
- `JsonException` or parser exception text;
- `Path:`, `LineNumber`, or `BytePositionInLine`;
- API, DTO, endpoint, protocol, debug, Spec Kit, or agent meta-language;
- hidden/gm-only/secret fields.

Advanced/debug mode may continue to expose raw diagnostics only through existing explicit advanced pathways.

## Read-Only Boundary

Read-only detail actions must not:

- mutate `afterlife_spiritual_conflict_state.json` or related state;
- create, rename, or delete pending/control files;
- submit spiritual-art upgrades or other local write operations;
- route through prompt/write services;
- change dice, rewards, validation, normalizers, or scheduler behavior.

Existing `/spiritual_arts` local-turn upgrade/write flows must remain available through their existing C# prompt/write authority. A read-only inspect action may coexist with those flows but must not replace or bypass them.

## Documentation Impact Rule

If the implementation changes only command-result presentation/action metadata/tests/spec artifacts, GM-facing afterlife docs/examples/manifests are not required. If any runtime afterlife contract, pending/control file, validation/normalizer rule, command mechanic, response field, GM prompt/example/manifest, or write authority changes, the implementation must update the relevant docs/tests in the same PR.

## Verification Expectations

- RED/GREEN tests for overview action exposure and selected exchange/log/art detail rendering.
- Missing/stale/sparse/malformed target tests with no raw/default diagnostic leakage.
- No-mutation tests for read-only detail actions.
- Focused afterlife/browser command-result tests and broad afterlife/browser/console slice.
- C# builds, Spec Kit prerequisite, `git diff --check`, and added-line static/security scan before PR/merge.
