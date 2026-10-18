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

Current same-UID tool census found no executing dotnet/pwsh, prior controller/PW
PIDs absent;35dotnet zombies are exited, not active servers. This is a dated census,
not old ancestry/reaping proof. No signal/reap/server shutdown is performed.
5logical CPUs share4CPU quota; cgroup memory limit16GiB, current headroom about5.9GiB,
tmp free7.28GiB. Lifetime max/throttling events lack an original baseline; current
OOM counters0 and file-cache-heavy memory do not establish the previous cause.

Unit build graph is5projects: unit, client, GMBridge, TestSupport and storage host.
Restore graph omits GMBridge metadata, while original build log/evaluated references
include it. Integration has3projects.61cached unit package versions have nupkg files.
SDK10.0.402/csc net10.0.13 targets net8.0; csc config uses server GC. SDK target
defaults shared compilation true when unset; evaluation-only property is empty.
Original actual server use/fallback is unknown. No active server is observed now.

## One finite operation after independent review

- [x] Save read-only evidence, graph, current constraints and one-shot source.
- [ ] Independently review diagnosis, ownership/isolation and exact command; save
  report with `buildOnlyAdmission=ACCEPT_ONE_ISOLATED_UNIT_BUILD_ONLY` and the exact
  `buildScriptSha256`. Publish/recover report before invocation.
- [ ] Invoke `recovery/l01-build-tooling-unit-once-20261018.py` once with exact clean
  current HEAD, saved review path/hash. Its unused private root is
  `/tmp/l01bd-772d38df9b5b4cf8`; mkdir prevents reuse. Before launch recheck149D01
  pins,172installed inputs, no active tool process, old rootPID absence and tmp free.
- [ ] The only build is installed `dotnet build BookOfEternityClient.Tests/...csproj
  --verbosity minimal --artifacts-path <root>/artifacts --disable-build-servers
  -p:GenerateDocumentationFile=true -p:NoWarn=1591 -p:NuGetAudit=false
  -p:RestoreSources=/tmp/1536-cloud-tools/nuget -clp:PerformanceSummary;Summary`.
  Full project references remain; restore reads the existing local package cache.
  No test/discovery/fixture/game/diagnostic command is run. Persistent servers are
  ignored per invocation; no global server configuration/shutdown changes.
- [ ] Installed GNU timeout owns a new session/process group and ordinary600s
  execution limit, then TERM/at most10s cleanup before KILL. It is independent of
  the Python observer. Samples every2s cover only that session's PID/starttick/
  PPID/state/CPU ticks/RSS and cgroup counters. Ordinary logs plus MSBuild performance
  summary provide project/task timing. No stacks, dumps, registers or game payloads.
- [ ] Save first raw command/logs/record/samples and original native status. Wrapper
  normal0 passes through build success;124/137 do not manufacture a known separate
  child exit. Observer errors stay separate; own-session/tool liveness after exit
  blocks acceptance and permits no global/PID signal. Retain disposable artifacts.
- [ ] Independent EVIDENCE, remote SHA/readback/clean recovery and handoff. No further
  build retry or downstream D01 execution follows automatically.

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
