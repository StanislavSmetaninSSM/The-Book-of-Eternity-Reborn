# Linux HTTP coverage audit and real loopback comparison — WIP

Source: #1553 / T054-WINDOWS-HTTP. Verification/report-only branch
`codex/1553-http-linux-audit-20261008`; product source, tests and catalog are
unchanged. HOME-PC remains the product implementation writer. No CI/merge change.

The authoritative reproduction instructions were read at
`c5e3680e13122ddc88e76af7db075ceccf6f1680` in
[windows-http-linux-reproduction.md](windows-http-linux-reproduction.md).

## What the previous Linux coverage missed

The actual host smokes in `LocalWebUiSmokeTests.cs:60` and
`LocalWebUiBuiltFrontendSmokeTests.cs:63` await session/game requests sequentially.
Fetching built assets does not execute frontend JavaScript.
`LocalWebUiHostTests.cs:454` (`BrowserAudioService_SerializesSharedSettingsUpdates`)
checks source strings rather than sending concurrent HTTP.
Actual settings endpoint tests exercise a request on independent hosts/roots.
Historical Linux receipt `recovery/evidence/b2b-services-green/portable-settings-endpoints-integration-003.trx`
contains eight PASS, including that source guard; no OS skip/no-op is inferred.

There was real concurrent HTTP: `OwnedTerminalScenarioDriver.LoadHttp.cs:78`
holds Load while early completion runs, releasing after100ms; `LoadFaults.cs:107`
overlaps completion/cancel for50ms. These protect original Load/ACK/epoch authority,
not sibling state GET requests held beyond the physical guard retry budget.
Direct service/storage concurrency such as `BrowserLocalWriteCoordinatorTests.cs:2054`
protects replacement and generation but is a different boundary.
Production `WebFrontend/src/hooks/loadShellState.ts:21` launches state/settings
reads together using Promise.allSettled. Isolated tests running in parallel do
not recreate sibling requests to one root/host. No Windows-only conclusion or
measured Windows/Linux speed difference follows from this coverage gap.

## Exact causal verification already completed

Commands, original TRX/logs, summaries and source pins are in
[recovery/http-linux-20261008](recovery/http-linux-20261008).

| Source | Command selection | Executed | Result | Completion/cleanup |
|---|---|---:|---|---|
|9dfa154d90f3da003d186fc1e3781d2cbd405b91|browser-http-admission|9/9|8 PASS,1 FAIL|complete; no timeout; cleanup complete|
|3d205d317244b977460fc5537f66fa35e8bc567b|browser-http-admission|9/9|9 PASS|complete; no timeout; cleanup complete|
|3d205d317244b977460fc5537f66fa35e8bc567b|selection-http.json|33/33|33 PASS|5/5 descriptors; no timeout; cleanup complete|

Exact commands used ordinary fresh build/discovery, Parallelism1, no budget
increase, NoBuild or full suite:

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category browser-http-admission -Parallelism 1
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/windows-http-20261008/selection-http.json -Parallelism 1
```

Linux RED `ParallelStateRequestsWaitOutsidePhysicalGuardBudget` reaches actual
Kestrel HTTP and fails expected200/actual500. It does not reach the zero-contention
assertion. This differs from the Windows RED200/198contentions observation.
GREEN verifies meaningful queued200 and zero physical contention; negative
physical-owner refusal remains covered. The separate lifetime REDb238 was not
run here; its two GREEN cases were included in the complete33 selection.

## Actual installed baseline: ordinary NewGame plus persistent relay

Root `/tmp/h3-82779715`, prebuilt runtime code0067874e15f479327dc37268fc891f51e3a2713d,
packaged build4070ad6c400b086e857b200eb5587ba8dad2db5f (report-only difference),
web/core/frontend source equal accepted main6381a9507baae6bb2531e22e9a0ace839f03985f.
The console used ordinary NewGame with isolated synthetic name/descriptions.
It cancelled the initial GM wait before CLI existed, returned to menu and exited0.
No model request or relay submission occurred. The real `--web` entrypoint then
served installed frontend assets on127.0.0.1:31460, and the ordinary launcher
started the maintained persistent relay through original M1/fence/terminal.
Running identity, retained authority and actual Ready were observed.

133 actual requests:107HTTP200 and26HTTP500. All94before relay were200,
including90parallel requests in same-root triplet/three-tab rounds.
With relay Running/Ready, `/api/session` and `/api/game-screen` fail even
sequentially; all13active `/api/audio/settings` requests return200.
The26 exception bodies report `Main run metadata or original owner admission is
unavailable` at `FileSystemManager.MainRunFence.cs:65`, before physical guard
acquisition. This is a distinct participating original-owner admission defect,
not proof of a contention timeout. Exact response bodies and original state are
in baseline-http-responses.json/baseline-result.json plus the archived raw traces.

Baseline original shutdown occurred once with exact identity; scoped stop,
Stopped record, three exit0/EOF/termios and guardianECHILD/0emergency are preserved.
An earlier driver preparation failure expected the wrong Russian NewGame marker;
it reached no HTTP/CLI/model stage and is separate from product failure. Its
original abort/EOF/guardian evidence is retained locally and will be archived.

## Fixed live comparison and environment boundaries

The exact3d205 package is being prepared for the same133-request flow on another
fresh root. GREEN33 is passing but does not qualify this active-owner boundary.
Do not call the entire web scenario PASS before the comparable receipt exists.

Port31460 bind and own TCP roundtrip succeeded; sockets closed. Installed Chromium
with sandbox refused because its SUID sandbox helper is misconfigured. No security
bypass, --no-sandbox or settings change was used. Browser UI execution is NOT RUN;
real loopback HTTP is separate evidence. No model/GM/gameplay acceptance, Windows
execution, systemd qualification, real saves or unrelated tests are claimed.

Independent Sol6.1/xhigh source/evidence review is in progress; final verdict and
fresh GitHub-only restoration remain pending. System/runtime/private credentials
and protected session storage are outside this task.
