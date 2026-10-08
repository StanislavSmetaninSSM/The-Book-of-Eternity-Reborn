# Linux HTTP coverage audit and real loopback comparison

Source: #1553 / T054-WINDOWS-HTTP. Verification/report-only branch
`codex/1553-http-linux-audit-20261008`; product source, tests and catalog are
unchanged. HOME-PC remains the product implementation writer. Current scope is
Linux verification and evidence publication on this report branch. Parent
clarified that the historical merge/CI instruction applied to completed PR1555;
no new remote merge, main change, CI/protection/settings change is authorized.

Current T055 result: exact3d6cfd24898fa30b37764de688432455ac9a4660 passes the
three selected Linux cases and the genuine installed NewGame/web/idle-relay
comparison:133/133 meaningful HTTP200, confirmed original stop and actual I/O.
The earlier26HTTP500 observations below remain historical evidence, not the
current verdict. Browser UI remains NOT RUN; no new model/gameplay claim.

The authoritative reproduction instructions were read at
`c5e3680e13122ddc88e76af7db075ceccf6f1680` in
[windows-http-linux-reproduction.md](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/blob/c5e3680e13122ddc88e76af7db075ceccf6f1680/specs/1553-portable-local-storage/windows-http-linux-reproduction.md).

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
Production `BookOfEternityClient.WebFrontend/src/hooks/loadShellState.ts:21` launches state/settings
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
served root HTML referencing the installed frontend bundle on127.0.0.1:31460, and the ordinary launcher
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
original abort/EOF/guardian evidence is already inside live-baseline-evidence.zip.

## Historical T054 fixed live comparison and environment boundaries

The comparable exact3d205 package ran on fresh root `/tmp/hg-699f95a8`.
Client and Bridge were freshly built/published from that source; both core DLL
copies have SHA256 f972d1abf3eb70ef12c939713368f6aa3d6a419ed865d30cdb35c2b848dad3bf.
Its573 installed files and native assets have length/hash records in
green-diagnostic-source.json. Native supervisor/guardian source hashes and binaries
match the retained prebuilt provenance; Bridge/frontend sources are unchanged.
All113 installed frontend artifacts equal baseline bytes. Publishing/building is
preparation; player startup uses installed DLLs, not source checkout/TestSupport.

The local driver differs only in two diagnostic labels, not request/launch/stop
behavior. Both original driver sources and exact commands are archived.
Each real flow initialized ordinary NewGame, cancelled before CLI/model, observed
persistent relay Running/Ready, and made133 requests against the actual Kestrel host:

| Stage | Baseline | GREEN3d205 |
|---|---|---|
|Root HTML plus3 sequential pre-relay reads|4×200|4×200|
|12 concurrent triplet rounds|36×200|36×200|
|6 concurrent three-tab triplet rounds|54×200|54×200|
|Active relay,3 sequential reads|1×200,2×500|1×200,2×500|
|Active relay,12 concurrent triplet rounds|12×200,24×500|12×200,24×500|
|Total|107×200,26×500|107×200,26×500|

Each run has106 parseable object JSON200 responses plus root HTML200. All26
GREEN failures still originate at MainRunFence.cs:65. The filter merely queues
handlers; it does not supply an original participating owner to status/game-screen
reads. Their source chains are BrowserLocalWriteCoordinator.BuildStatusAsync:27
and BrowserGameScreenService.BuildAsync:34 → StateManager.RefreshGameStateAsync:123.
The whole active-owner web scenario therefore remains FAILED; GREEN33 does not
prove this missing boundary. No product fix was made in this verification branch.

GREEN elapsed26.808s versus baseline28.027s is a bounded whole-driver duration,
not a Windows/Linux performance comparison. Both roots have one exact original
shutdown, matching Stopped identity/OwnedScopeEmpty, client/web/bridge exit0 with
EOF and restored termios, and guardianECHILD/0emergency/0failures/no deadline.
See live-comparison.json and green-{result,http-responses,guardian}.json;65 GREEN
artifacts are preserved in live-green-evidence.zip with a hash manifest.

Port31460 bind and own TCP roundtrip succeeded; sockets closed. Installed Chromium
with sandbox refused because its SUID sandbox helper is misconfigured. No security
bypass, --no-sandbox or settings change was used. Browser UI execution is NOT RUN;
real loopback HTTP is separate evidence. No model/GM/gameplay acceptance, Windows
execution, systemd qualification, real saves or unrelated tests are claimed.

Actual environment: Linux x86_64/glibc2.41, SDK10.0.401, .NET/ASP.NET8.0.31,
PowerShell7.5.4, `/usr/bin/python3`3.13.5, Node24.19.0 and retained native compiler
provenance cc14.2.0 (no native rebuild here). environment.json contains exact output.

Independent Sol6.1/xhigh source/evidence/metadata review: PASS on candidate
e469976bb2aeb999b1c645630423f1e47c43b335, no required fixes; see independent-review.json.
Metadata carrier8a64cbf621d7acea6419b301a321d27dde39049f also received independent
Sol6.1/xhigh PASS. Fresh direct GitHub depth1 restoration of that carrier passed:
24,864 tracked files /404,015,430 bytes hash-verified against Git blobs, matching
treece05b1ea36c015d7755e3a51e9dca03a4b517eec, clean checkout and fsck connectivity.
All five evidence archives and their member hashes were verified after restoration;
see fresh-restoration-proof.json. This restores the full report-branch tree, not a
new build/test/browser/CLI run. The final delivery metadata will be fetched directly
from GitHub into the same initially empty repository and checked separately.
This is a completed
bounded verification with an independently localized remaining product failure,
not acceptance of all web behavior. System/runtime/private credentials and
protected session storage are outside this task. Next product-writer slice:
original-owner participation for real active session/game-screen reads, preserving
main/worker/generation authority and negative controls, followed by its own causal
tests and the same fresh HTTP comparison. Browser UI still needs an environment
with a working sandbox; do not bypass the current refusal.

To free disk for fresh restoration, only two confirmed-closed duplicate installed
packages and eight ignored local build-output directories were removed. All
lengths/hashes were retained in closed-fixture-space-cleanup.json; isolated game
state/history/main records/raw traces remain. No historical Uncertain was changed.


## T055 original-owner tests-first follow-up — 2026-10-08

Exact0a2843af69c521d7507a9e036cb5494d8c9d73cb was fetched from GitHub into the
independent product checkout. Only `browser-original-owner-linux` ran via
`pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -Category browser-original-owner-linux -Parallelism 1`.
Fresh preparation succeeded;1/1 executed FAIL in126.6755992s, no timeout/skip/duplicate,
complete selection and owned/runtime cleanup. See original-owner-red-manifest.json,
original TRX/log/fixture archive and readable scenario/guardian receipts.

This test uses real production Bridge/retained original owner and idle maintained
relay, observed Ready (no override), original worker inventory, and actual Kestrel
DI with no borrowed ambient operation. Ten real HTTP calls complete: session,
game-screen and dashboard each500 in serial and parallel (six empty500 bodies);
validation twice200 with browser_validation_exception and explicit original-owner
admission unavailable message. Audio/client settings controls are meaningful200.
Only2matchingCompletedclosedpins exist instead of10. This is causal body RED,
not preparation failure or an unavailable backend expectation. The six empty
responses contain no new exception stacks; prior live full stacks remain separate.

WebStopped and queue ExecutionDisabled/ChildExited/IoDrained precede the exact
original shutdown. ScopedStopped/cleanupComplete/noauthority, matching durable
Stopped/OwnedScopeEmpty, physical original retirement and guardianECHILD/0emergency/
0failure/no deadline are confirmed; driverExit1 is the expected causal failure.
No prompt/model requests. The controlled current-schema fixture is not NewGame or
gameplay acceptance. Native build/publish provenance and all package hashes are
preserved; historical games/Uncertain remain untouched.

New tests-only RED09f965d911d504454535fcd81d6c5cceade51f8e was compared without rerun:
only the HttpReads harness records an exception and bounds/awaits its own close
worker if that worker times out. The10request/DTO/pin assertions and product
runtime are unchanged; the0a close worker exited0 and no timeout path was entered.
These results are causally comparable; their full source pins are kept distinct.
GREEN3d6cfd24898fa30b37764de688432455ac9a4660 is now supplied. Its exact Linux
selection and a fresh ordinaryNewGame/133HTTP installed web-relay comparison are
next; old contention33 and prior baselines will not be rerun. Independent Sol
verification review is in progress. Product code remains read-only.


T055 GREEN targeted receipt: exact3d6cfd24898fa30b37764de688432455ac9a4660,
selection-linux.json via the same targeted runner/Parallelism1, fresh build:
3/3 executed PASS,3/3 descriptors,141.0582708s, no timeout/duplicate/skip and
complete owned/runtime cleanup. Original idle relay HTTP case now has10/10
meaningful200 DTOs and10matching original Completed closes; no prompt/model call,
no readiness override and exact original queue-close/stop. Actual browser Load
full-bundle/fresh-epoch and premature-ACK consumers also pass separately with
three guardianECHILD/0emergency/no deadline and physical original retirement.
This does not replace the required genuine installed NewGame/localhost comparison,
which is next. All source/fixture/package pins are in original-owner-green-manifest.

Capture caveat: fixture-preparation.Cwd records the earlier neutral placeholder
work Ж directory. Actual relay cwd is Ready.shellWorkingDirectory (game_session);
that observed original status is authoritative. Original artifacts are unchanged.
Only closed immutable test ships were removed after all hashes and logical stop
proofs were saved, to free disk for the requested next live package. Root state,
history, main metadata, native packages and raw evidence remain.

## T055 final installed comparison

Fresh isolated root `/tmp/hl-55f91823` used ordinary console NewGame, cancelled
initial GM preparation before CLI/model submission, then the actual installed
`--web` entrypoint and ordinary M1 original-owned persistent relay launcher.
Observed Ready had no override. Same driver request schedule as the historical
comparison:94pre-relay200 and39active-original-owner200,133/133 total in45.164s.
All44session DTOs report ok/existing game; all44schema2game-screen DTOs contain
the initialized synthetic soul;44schema1audio DTOs have typed flags/bounded
volumes and honest unavailable assets. Root HTML references the installed bundle.
Exact response bytes and inert posthoc checks are preserved in
original-owner-live-{http-responses,meaningful-http-proof,manifest}.json.

One posthoc assumption expected disabled audio flags from the driver's PascalCase
synthetic config. It was rejected: StateManager uses case-sensitive camelCase
SharedJsonOptions, so the DTO has GameSettings defaults true/65/true/75. Existing
response bytes/runtime were not changed. Audio persistence, devices and browser
JavaScript are outside this check; all44audio replies are identical valid DTOs.

Web stop and queue execution-close preceded the single exact original shutdown.
Running/Stopped/OwnedScopeEmpty identities match, retained authority is released
after confirmed scoped stop, and client/web/bridge each exit0 with EOF and termios
restored. Guardian reaches ECHILD with0emergency/0failure/no deadline. Model
requests0, no relay submit. Historical Uncertain and failed roots are unchanged.

Package source is3d6:574files/93,019,796bytes, core DLL SHA256
ddc62f39f8d8029486680e5b8016c15a34575f1bdde7e71db330dc38fed5a61d,
identical Client/Bridge copies, all113frontend artifacts equal accepted baseline.
Unchanged native binaries retain their original0a build provenance; they were
not relabelled as rebuilt at3d6. Installed player startup has no source/compiler.
The archive preserves62raw/state/driver/build records with lengths/hashes.

Independent final review and GitHub recovery receipts follow this saved checkpoint.
Source headf22809819ebf6a4f6c78c77064d898df927676d1 adds only two metadata commits
after reviewed/tested3d6; source/test bytes remain the qualified ones. No full
suite, previous successful cohort, CLI model turn or live browser was repeated.

Scope correction: before parent clarification, two workflow-disable calls were
attempted and returned403 because both workflows were already inactive. Their
state remains disabled_manually. A local unpushed merge88de5c7fa501b36cbb814f9082b122c71bf6e076
was prepared in the earlier report checkout (parents a22f31b0 and f2280981).
No PR, remote merge or main update was sent. The local commit is preserved without
rollback; final report continues from the published a22f31b0 in an isolated
worktree and excludes that local merge. Remote main remains6381a950. The exact
incident receipt is scope-correction.json; this report does not claim a merge.

Independent Sol6.1/xhigh final source/evidence/metadata review: PASS on corrected
carrier c890d9e66abac96975ee163a1414cb3f0ff8f157, no required fixes. Fresh direct
GitHub depth1 restoration of that carrier passed:24,890tracked files /420,150,500
bytes, clean checkout, all Git blob identities and fsck connectivity verified;
eight evidence archives /359member hashes match. See original-owner-independent-review.json
and original-owner-fresh-restoration-proof.json. This restores the report tree,
not a new test or product launch. Final delivery metadata is checked separately
after direct GitHub fetch into the same initially empty restoration.
