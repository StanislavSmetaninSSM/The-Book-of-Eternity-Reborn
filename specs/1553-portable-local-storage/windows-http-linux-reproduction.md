# HTTP contention: previous coverage and native Linux reproduction

Source: #1553 / T054-WINDOWS-HTTP, owner question 2026-10-08.
This source audit does not claim a Linux execution result. HOME-PC measurements
are native Windows; Linux execution and its original receipts remain separate.

## What previous tests actually exercised

The pre-fix product at `9dfa154d90f3da003d186fc1e3781d2cbd405b91` matches the
HTTP behavior inherited from merged main `6381a9507baae6bb2531e22e9a0ace839f03985f`.
The new diagnostic harness and nine causal tests are present at 9dfa; the host
admission filter is not. The following earlier source is unchanged by this fix:

- `BookOfEternityClient.IntegrationTests/LocalWebUiBuiltFrontendSmokeTests.cs:60`
  fetches the built HTML/assets and API with sequential awaited HttpClient calls.
  It does not execute the frontend JavaScript in a browser. Likewise
  `LocalWebUiSmokeTests.cs:58` awaits menu, session, game and dashboard in sequence.
- `LocalWebUiHostTests.cs:486,554,596` exercises real Save/Load and complete
  exact-generation refresh bundles, but its HTTP calls are sequential. The
  generation/read/close fault hooks verify authority and bundle integrity.
- `LocalWebUiHostTests.cs:454` has a misleadingly broad name,
  `BrowserAudioService_SerializesSharedSettingsUpdates`: it asserts semaphore
  strings in source. It does not send simultaneous HTTP requests.
- There **is** genuine concurrent HTTP coverage. In
  `BookOfEternityClient.TestSupport/OwnedTerminalScenarioDriver.LoadHttp.cs:78`,
  an actual Load waits at BeforeCommittedMenuRefresh while an early Load-complete
  request is sent; the barrier releases after 100ms. `LoadFaults.cs:107` overlaps
  actual completion/cancel and releases after 50ms. These verify original Load
  decision/ACK/launch authority, not five sibling state reads exhausting the
  main-owner guard's retry window.
- There is also genuine direct service/storage concurrency, for example
  `BrowserLocalWriteCoordinatorTests.cs:2054`: atomic write overlaps replacement,
  observes contention and releases through barriers. Other generation-fencing
  tests protect QTE/player/world writes. These are valuable but exercise a
  different boundary and do not drive simultaneous startup HTTP reads.
- Each ordinary HTTP test uses its own host/root. Parallel test execution on
  isolated roots does not recreate sibling requests against the same host/root.

Production `BookOfEternityClient.WebFrontend/src/hooks/loadShellState.ts:21`
uses Promise.allSettled to launch main-menu, session, game-screen, audio settings,
client settings and pure command metadata together. The audited prior scenarios
do not reproduce that traffic shape while a participating state request holds
the original physical main-owner guard for its whole long operation. This is a
coverage gap, not evidence that all old Linux tests lacked concurrency. Source
membership alone does not prove that a particular historical category executed;
use its original Linux receipt for that claim.

## What the causal test proves

`LocalWebUiRequestAdmissionTests.ParallelStateRequestsWaitOutsidePhysicalGuardBudget`
uses the real LocalWebUiHost/Kestrel and original DI. A filesystem hook pauses an
actual `/api/session` request after its main admission is acquired. It then sends
`/api/audio/settings` and holds the first request for 11.5 seconds. The existing
physical acquisition remains 200 attempts with 50ms delays, nominally 10 seconds;
scheduling can extend the actual interval. No storage guard or response is faked.

Native Windows RED at 9dfa completed the audio response with 200 but observed
198 physical contentions, failing the expected-zero assertion. The actual 500
observations are the independent empty/ordinary/synthetic Kestrel probes with
IOException stacks, not that particular assertion. With host admission at
`3d205d317244b977460fc5537f66fa35e8bc567b`, the queued sibling enters the handler
only after the first leaves, returns meaningful 200 and records zero physical
contentions. Its negative control still refuses a separately held physical guard.

There is no OS branch in this HTTP scheduling correction. Windows request times
of roughly 10–14 seconds before the fix were measured; validation's own share,
filesystem cost and a Windows-versus-Linux timing difference were not profiled.
Faster Linux timing hiding the natural race is a hypothesis until measured. A
Linux natural probe with no 500 would not refute the controlled contention test.

## Exact independent Linux commands

Use two isolated, already prepared native Linux checkouts/worktrees; do not install
anything or use HOME-PC/WSL. Keep each checkout's test output before switching.

RED source: `9dfa154d90f3da003d186fc1e3781d2cbd405b91`.
GREEN source: `3d205d317244b977460fc5537f66fa35e8bc567b`.
Both are published on `codex/1553-windows-http-20261008` history.
From each exact checkout run the same nine-case category:

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category browser-http-admission -Parallelism 1
```

On GREEN, the reviewed affected selection additionally includes lifetime2,
Load10+4 and settings8 (33 cases total):

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/windows-http-20261008/selection-http.json -Parallelism 1
```

No full suite, all-category run, budget increase or stale NoBuild. Preserve
original TRXs, summary, actual completed counts and owned cleanup. Separate
lifetime causal RED is `b238e4ea946db38a5782d3c793b87a1e345a7c17`, category
`browser-http-admission-lifetime` (one cancellation PASS and one shutdown FAIL
on Windows); GREEN is the same 3d205 source.

For the minimal natural HTTP comparison, the diagnostic project is portable
despite its WindowsHttpProbe name. At each exact checkout, in bash:

```bash
probe_root=$(mktemp -d -t boe-http-probe-XXXXXX)
mkdir "$probe_root/data"
dotnet build tests/fixtures/WindowsHttpProbe/WindowsHttpProbe.csproj
dotnet tests/fixtures/WindowsHttpProbe/bin/Debug/net8.0/WindowsHttpProbe.dll \
  "$probe_root/data" "$PWD/BookOfEternityClient.WebFrontend/public" "$probe_root/report.json"
```

This uses actual loopback Kestrel, original DI and existing ILoggerFactory plus
a diagnostic provider. It sends two parallel batches of the five state GETs,
then one serialized request per route, collects actual server stacks and awaits
StopAsync/DisposeAsync. Static tracked fallback assets avoid a Node build and
do not alter API behavior. It does not start a GM/model or perform Save/Load.
Retain each report separately. Inspect **all** observation statuses/exceptions
and server logs: exit0/Fatalnull alone is not success. On a fresh empty root,
game-screen404 is legitimate; the other four routes must return real 200. A
natural race depends on timing; the controlled category is the causal comparison.
