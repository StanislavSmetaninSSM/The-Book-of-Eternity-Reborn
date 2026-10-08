# Linux regression of the bounded Windows relay delta

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Verification-only continuation of [the Windows relay plan](relay-windows-plan.md).

## Exact scope and isolation

Checkpoint `3ce1ab52b0a4e41dbe9717b52681236bd368c9b9`; frozen runtime/tests
`0067874e15f479327dc37268fc891f51e3a2713d`; original main
`6381a9507baae6bb2531e22e9a0ace839f03985f`.
Separate report branch `codex/1553-relay-linux-regression-20261008` starts at
the checkpoint. Product source, tests, catalog and implementation branch are
unchanged. No CI enablement, merge, provider/GM request, Windows execution,
systemd call, real save or environment/access/settings change is part of this run.

The retained selection is
`recovery/windows-relay-20261008/selection-linux.json`: shared exact-byte
protocol/identity/gate/publication/close; original production Bridge, worker,
ordinary consumer and retirement; POSIX raw terminal/output and held-child close;
actual repair/helper consumers. Source inspection identifies 17 + 3 + 9 + 3
cases. Fresh runner discovery will determine the executed denominator.

## Execution plan and current status

Actual Linux execution works in the existing cloud workspace. Existing activation
reports SDK10.0.401, .NET8.0.31 (also runtime10.0.12), PowerShell7.5.4,
Python3.12.14 and cc Debian14.2.0-19. No installation or system configuration
is needed. Two old clean duplicate restore checkouts were removed to free
fixture space; their top-level proofs and inventories remain locally preserved.

1. Fresh selected-project build/discovery through `scripts/test-csharp.ps1`
   with the retained selection, `-PlanOnly -Parallelism 1`.
2. Execute that exact selection with `-NoBuild -Parallelism 1` only after the
   fresh build succeeds. Preserve exact pins, commands, plans, TRX, completeness,
   preparation failures and original owner/guardian/I/O cleanup separately.
3. Independent Sol6.1/xhigh evidence/coverage review; publish report-only
   checkpoint with exact remote SHA and byte readback. Reproducible defects go
   to the parent/HOME-PC implementation writer; this executor makes no fixes.

WIP: tests and independent result review have not run. The separate reported
Windows24/24 and ordinary Bridge transport evidence are historical external
evidence, not Linux results or gameplay qualification.
