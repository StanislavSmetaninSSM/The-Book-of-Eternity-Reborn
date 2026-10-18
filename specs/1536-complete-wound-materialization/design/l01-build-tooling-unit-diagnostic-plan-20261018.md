# L01 B01 isolated unit build diagnostic plan

> For agentic workers: execute inline with systematic-debugging and executing-plans;
> independent Astra XHigh review precedes the one build-only operation.

Goal: separate a reproducible build/tooling limitation from gameplay investigation.
Task #1536; base `be99971f7fcce34aeca46d85f5b4a3a893ccc147`. The latest user
explicitly permits one meaningful build-only retry after previous-tree completion
checks and independent justification. It does not release gameplay/synthetic.

Architecture: existing sole-writer cloud checkout, SDK-supported private artifacts
outputs, installed GNU timeout and a single purpose observation script. No new
instrumentation framework or production/test assertion change. Existing SDK excludes
both old bin/obj and the new artifacts path from default source globs.

## Evidence and unresolved cause

[Read-only observations](../recovery/l01-build-tooling-observations-20261018.json)
retain original124/137,520.572s integration build and72.273s incomplete unit build.
The600s deadline was shared across the whole catalog;72s was not a unit timeout.
No original Csc/project timings, process CPU or RSS were recorded.

Corrected same-UID stat/executable census with identity-rechecked exact argv0
fallback found no active tool match; prior controller/PW PIDs absent,35dotnet
zombies exited. Executable visibility is limited; this is a dated match-based
census, not original ancestry/reaping proof. No signal/reap/server shutdown occurs.
Affinity exposes5logical CPUs. Observed cpu.max represents4CPU bandwidth and
memory.max is16GiB, with current node headroom about5.9GiB and tmp free7.28GiB.
The cgroup2 mount root is/.. while membership is0::/: these are shared mount-node
observations, not proven effective per-build/ancestor limits. Lifetime max/throttle
events lack an original baseline; OOM0 and file cache do not establish old cause.

Unit build graph is5projects: unit, client, GMBridge, TestSupport and storage host.
Restore graph omits GMBridge metadata, while original build log/evaluated references
include it. Integration has3projects.61cached unit package versions have nupkg files.
SDK10.0.402/csc net10.0.13 targets net8.0; csc config uses server GC. SDK target
defaults shared compilation true when unset; evaluation-only property is empty.
Original actual server use/fallback is unknown. No active server is observed now.

## One finite operation after independent review

- [x] Save read-only evidence, graph, current constraints and one-shot source.
- [x] Independently review diagnosis, ownership/isolation and exact command; save
  report with `buildOnlyAdmission=ACCEPT_ONE_ISOLATED_UNIT_BUILD_ONLY` and the exact
  `buildScriptSha256`. Publish/recover report before invocation.
- [x] Invoke `recovery/l01-build-tooling-unit-once-20261018.py` once with exact clean
  current HEAD, saved review path/hash. Its unused private root is
  `/tmp/l01bd-772d38df9b5b4cf8`; mkdir prevents reuse. Before launch recheck149D01
  pins,172installed inputs and exact S01 helper SHA before import, no active exact
  tool match, old rootPID absence, required numeric resource reads and tmp free.
  An unresolved still-present/reused PID read holds launch/acceptance; a vanished
  PID is distinguished by reprobe. Record raw cgroup mount-node identity/scope.
- [x] The only build is installed `dotnet build BookOfEternityClient.Tests/...csproj
  --verbosity minimal --artifacts-path <root>/artifacts --disable-build-servers
  -p:GenerateDocumentationFile=true -p:NoWarn=1591 -p:NuGetAudit=false
  -p:RestoreSources=/tmp/1536-cloud-tools/nuget -clp:PerformanceSummary;Summary`.
  Full project references remain; restore reads the existing local package cache.
  No test/discovery/fixture/game/diagnostic command is run. Persistent servers are
  ignored per invocation; no global server configuration/shutdown changes.
- [x] Installed GNU timeout owns a new session/process group and ordinary600s
  execution limit, then TERM and, while the monitored command remains alive,
  at most10s before KILL. Normal wrapper exit does not prove descendant cleanup.
  This bound is independent of Python. Samples every2s cover only that session's PID/starttick/
  PPID/state/CPU ticks/RSS and cgroup counters. Ordinary logs plus MSBuild performance
  summary provide project/task timing. No stacks, dumps, registers or game payloads.
- [x] Save first raw command/logs/record/samples and original native status. Wrapper
  normal0 passes through build success; raw negative Popen return means a signal
  (for example,-9), not positive137. Preserve it and map only the external controller
  exit to128+signal.124/negative status do not invent a known separate child exit.
  Observer errors stay separate; own-session/tool liveness after exit
  blocks acceptance and permits no global/PID signal. Retain disposable artifacts.
- [x] Independent EVIDENCE, remote SHA/readback/clean recovery and handoff. No further
  build retry or downstream D01 execution follows automatically.

## Completed first outcome and current handoff

The single admitted source `d3a1ddcf7a6e2f06ae815877c46314ec81d9f0d7`
completed in282.270769695s, raw wrapper/controller0, joined, observer errors0,
94warnings/0errors. [Original evidence](../recovery/l01-build-tooling-b01-first-outcome-20261018.json)
and its four-member archive were saved/recovered at `c20a9cf0` before independent
EVIDENCE. The historical index retains its pending-review status; the subsequent
[independent review](../recovery/l01-build-tooling-b01-evidence-independent-review-20261018.md)
accepts BUILD_ONLY evidence,68checks/findings[]. Root verified all four review/check
artifact hashes before byte-exact copying. Final report/status carrier is published
and recovered with its actual SHA before handoff; no self-receipt commit loop.

Five primary private DLL hashes and all899unchanged normal-bin files were verified.
131retained samples cover ten PID/birth identities, maximum gap5.551541714s;
terminal own-session/tool observations are empty with explicit visibility limits.
No original outcome, source, assertions,149D01pins or normal outputs changed.
`CoreCompile`270576ms/5calls is a target summary entry; `Csc`270535ms/5calls is
a task summary entry. The index's descriptive taskSummary grouping preserves both
original values; neither is exclusive elapsed time. Resource/CPU attribution and
sampling limits above remain. Private DLL byte restoration is not claimed.

Permit CONSUMED, no repeat. Current isolated unit-build capability is established;
the cause of original catalog124/unit137 remains UNKNOWN. This evidence supplies
no reason requiring immediate cloud migration and no general-health/repeatability
claim. Original catalog/D01PREP, PlanOnly, direct Live compilation, synthetic,
gameplay and crash5 remain held/unreleased. L0152/57 and old66529UNKNOWN remain.

The next finite recommendation is PLAN ONLY: inspect a coherent fresh BOTH/catalog
ZERO design with one new private artifacts namespace, demonstrated disabled-server/
local-cache conditions and the existing aggregate600s deadline. Bind exact build
outputs for both project graphs to discovery and subsequent NoBuild PlanOnly;
inspect existing path resolution before proposing the minimum necessary tooling
adjustment. No old899files or B01outputs become accepted PREP. Any change needs
affected SOURCE before command admission and fresh independent PREP afterward.
No fit guarantee, budget reset/increase or automatic catalog/synthetic/game release.

## Interpretation and stop rules

This standalone unit build has its own ordinary600s bound for diagnostic purposes.
It does not split/relabel the failed catalog into a PASS. Private outputs, cached
feed and ignored servers change conditions; one success cannot identify which old
condition caused slowdown. It can prove only the current bounded build capability.
Rusage includes waited children/preflight Git and is not simultaneous tree peak.
Cgroup deltas are group observations, not exclusive build attribution.

No active old tool tree or adequate readable identity/resource state: do not launch;
state the exact missing fact. New timeout/error/unknown/leftover freezes this new
outcome; diagnose it independently and select fresh cloud only on supported signs
of degradation or inability to establish safety. Do not increase600/220/900 limits,
weaken assertions, stop unrelated/zombie processes, install tools or retry blindly.

Review focus: full5-node scope/cache and private paths; standard timeout's owned
group/cleanup and native priority; observer races/uncertainty; honest memory/CPU/
server causality; no transition from build success to catalog/PREP/game qualification.
L0152/57, old66529UNKNOWN, crash5, M01/S01 narrow evidence, all platform/broader holds
and A3NO-PROCEED remain. Windows/CI are unused; no GM contract changes.
