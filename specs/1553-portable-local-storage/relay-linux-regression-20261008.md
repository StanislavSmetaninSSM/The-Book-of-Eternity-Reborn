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

## Executed Linux result

Fresh selected unit build and PlanOnly succeeded in108.248s, discovering four
descriptors/32cases and executing zero. The same freshly built unit binaries then
executed the exact selection with NoBuild: **32/32 PASS**, zero skips/failures/
duplicate IDs, four completed descriptors, complete selection, no timeout,
67.879s total wall time. Runner owned-tree and runtime cleanup both succeeded.

| Category | Passed cases |
| --- | ---: |
| gm-relay-reusable-contract |17|
| gm-relay-reusable-main |3|
| gm-relay-transport |9|
| gm-relay-repair |3|

Commands, run from the isolated checkout after existing activation:

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/windows-relay-20261008/selection-linux.json -PlanOnly -Parallelism 1
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/windows-relay-20261008/selection-linux.json -NoBuild -Parallelism 1
```

Actual tested checkout/build revision is report-only WIP
`4070ad6c400b086e857b200eb5587ba8dad2db5f`. Runtime/test bytes remain the frozen
0067874e source; selection/catalog/documentation bytes are from checkpoint3ce1.
[Exact source pins](recovery/relay-linux-regression-20261008/source-pins.json)
distinguish these references. No runtime or test fix was made.

All14independent scenario guardians observed ECHILD, driverexit0, zero emergency
signals/failures/deadlines. Three production Bridge cases preserve original
identity/inventory, actual T042 submit and shared worker/helper consumption,
queue-close before stop, child exit and drained I/O, original scoped Stopped/ACK
and authority retirement. Two ordinary synthetic packets succeed; the deliberately
invalid completion packet returns consumerexit1 and still closes cleanly.
Their original helper/shell PIDs are absent after retirement. Guardian cleanup
is separately recorded and does not substitute for these logical stop assertions.

[Evidence manifest](recovery/relay-linux-regression-20261008/manifest.json)
retains452exact artifacts in a572,087-byte ZIP: runner plans/logs/TRX,17contract
fixtures,14owned scenario roots, synthetic queue packets and native build provenance.
[Cleanup](recovery/relay-linux-regression-20261008/cleanup.json),
[32unique case identities](recovery/relay-linux-regression-20261008/test-cases.json)
and [environment](recovery/relay-linux-regression-20261008/environment.json)
are separately readable. Packaged binaries are hash-recorded, not embedded.

WIP: independent Sol6.1/xhigh result/coverage review is next. The separate reported
Windows24/24 and ordinary Bridge transport evidence remain external evidence;
this Linux verification makes no Windows, provider, gameplay, full systemd,
HTTP500 diagnosis, cold recovery or whole-game acceptance claim. Catalog and
implementation branch remain unchanged; no new catalog audit or full suite was run.
