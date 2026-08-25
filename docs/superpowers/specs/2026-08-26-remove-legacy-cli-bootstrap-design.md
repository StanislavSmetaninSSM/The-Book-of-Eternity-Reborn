# Remove Legacy CLI Bootstrap Contract Design

Status: approved for autonomous implementation on 2026-08-26.

Tracking: GitHub issue #1549. Implementation: PR #1545.

## Context

`Ensure-CliBootstrapSent` and `$script:BootstrapSent` have no production call
sites. Automatic bootstrap dispatch was deliberately removed in `6cc6204` to
fix the lost-turn failure tracked by #1284, but the disconnected function,
state, prompt, log message, and tests that require them remained.

## Decision

Remove the dormant bootstrap path instead of restoring or documenting it as a
manual hook. Normal turns, validation repairs, and terminal-protocol failures
continue to use their existing correlated `Dispatch-WithRetry` paths. This is a
dead-code and contract-alignment change, not a new runtime bootstrap flow.

## Contract tests

Replace the two legacy-positive assertions with explicit negative guards:

- the daemon must not contain `$script:BootstrapSent`,
  `Ensure-CliBootstrapSent`, `BOOTSTRAP GM SESSION`,
  `BOE_GM_BOOTSTRAP_READY`, or the legacy bootstrap dispatch log;
- bounded context-pack guidance must remain available through the active
  per-turn directives;
- turn, repair, and terminal-failure functions must retain their correlated
  retry dispatch paths and must not acquire a separate bootstrap phase.

The RED proof restores the legacy daemon block temporarily after the new tests
are authored. The new absence guards must then fail. Restoring Ivan's deletion
is the minimal GREEN implementation.

## Error handling and compatibility

There is no save, state, command, or player-facing compatibility surface. The
removed state existed only in daemon process memory and was never reached.
PowerShell parsing remains a required verification gate.

## Documentation scope

No GM-facing prompt, rule, example, manifest, or afterlife contract changes are
required. The removed prompt was unreachable and unsupported; active turn and
repair guidance remains unchanged. This no-update rationale must be recorded in
the PR and issue completion comments.

## Verification

Run the exact focused contract class for RED and GREEN, parse the daemon with
PowerShell, run one bounded Fast checkpoint, then run one final PreMerge gate
without an immediately preceding duplicate Fast run.
