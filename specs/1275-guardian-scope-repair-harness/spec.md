# Feature Spec: Guardian Scope Repair Harness

Source issue: https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1275

## Problem

During live Chaos Sea GM testing, ordinary afterlife turns with an active Guardian can enter repeated repair loops when validation reports guardian scope and materialized mirror issues separately. The GM sees individual errors such as `structured_guardian_update_out_of_scope`, `active_guardian_missing_from_scope`, `missing_actor_block`, and `guardian_materialized_state_outside_authority`, but the repair request does not currently provide one concrete file-by-file repair route.

## Goals

- When guardian scope or Guardian materialized mirror errors appear, `validation_repair_request.json` must include an explicit harness repair packet.
- The packet must tell the GM which files to repair, which Guardian actor names to use, and what minimum `gm_thoughts_markdown` shape is required.
- The packet must explain that `game_state/meta/guardians.json.activeGuardian` and `guardians[]` are materialized surfaces, not raw authority sources.
- GM-facing docs must mention this repair packet so the GM understands why it exists and how to use it.
- The bridge harness must not dispatch a player turn into Codex CLI while the CLI is still working on an older request or waiting for a workspace confirmation.
- A live Chaos Sea test should verify that the GM no longer needs multiple attempts for the same guardian-scope/mirror mistake, or that any remaining repair attempt is concrete and file-specific.

## Non-Goals

- Do not weaken guardian authority validation.
- Do not let the GM bypass canonical Guardian state reconstruction by editing mirror fields arbitrarily.
- Do not introduce a broad repair automation for all validator errors in this issue.

## Acceptance

- A regression test proves guardian scope/mirror validation errors produce a `harnessRepairPackets[]` entry with kind `guardian_scope_repair`.
- The packet includes `output/debug_logs.json`, `game_state/meta/guardians.json`, canonical actor names, concrete repair steps, and a reusable debug-log template.
- The bridge refuses prompt dispatch when the visible Codex CLI screen shows an active `Working ... esc to interrupt` state, preventing hidden prompt concatenation or buffering.
- Afterlife GM docs describe the packet and the correct handling of Guardian materialized mirrors.
- Live Chaos Sea testing is repeated after implementation and records whether the GM still hits repair-loop friction.
