# S2A — original read-only cgroup source implementation plan

> For the sole writer: use Superpowers executing-plans/TDD; independent Sol6.1/xhigh review before code. Source [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553); existing Spec Kit feature1553, no second journal.

**Goal:** supply the missing original read-only descriptor observation to the accepted S1 owner. Public systemd/Auto activation stays closed; controlled tests do not close actual-manager S2.
**Architecture:** one `SystemdCgroupSource : ISystemdCgroupSource` consumes own namespace/mount metadata, original held child's unified membership and a retained read-only directory/events pair. A small `ISystemdCgroupFiles` seam substitutes kernel observations in isolated fixtures; the native implementation uses only read-only descriptors and proc metadata. Existing `SystemdCgroupObservation`, owner/coordinator/Bridge, native reaper and schema1 remain the authority consumers.
**Tech:** .NET8 Linux x64; installed libc statx/openat/pread/fstatfs; no package/service/compiler/player dependency. Spec: [accepted design](systemd-main-design-wip.md).

## Environment and remaining obligations

Exact read-only probe: [script](recovery/systemd-s2-read-only-probe.py), [result](recovery/systemd-s2-read-only-capabilities.json). UID1000; no `/run/user/1000/bus`, no XDG_RUNTIME_DIR/DBUS_SESSION_BUS_ADDRESS; `sd_bus_open_user=-123` ENOMEDIUM. Installed systemd257/libsystemd exports are available; no accessible manager. Visible cgroup2 mount209 has root `/..`, readonly; root events are not an owned scope. No manager/unit/cgroup mutation was attempted.

| Remaining boundary | Current proof / required next step |
| --- | --- |
| Concrete cgroup source | This implementable S2A source + controlled slice; full-root mapping only, non-root/namespace mappings refuse until separately qualified. |
| S2 actual native bus/FD ABI, attached original scope and stop margin | Requires already-running accessible caller user manager, existing user bus, visible matching cgroup-v2; no setup workaround here. Actual fixed neutral CLI qualification must exercise original pidfd attachment, unit/invocation, descendants/stop/I-O/ACK. |
| Manager pruning before zero read | Pinned removal witness remains unqualified and refuses; no pathname ENOENT, nlink, EOF or deleted proc path becomes Empty. Actual manager/kernel evidence needed. |
| S3 ordinary SystemdUser/Auto + real consumers | Remains closed until qualified capability. Auto policy already accepted: prefer qualified systemd or declare qualified NativeLineage before launch; explicit SystemdUser never downgrades; no post-start switch. |
| Production workers / full ordinary gameplay-load-restart | Independent worker admission and unqualified enabled workers remain closed; accepted bounded M1/F1-F3/load/relay proof is reused, not repeated. |
| Native Windows | Planned after completion/merge in existing HOME-PC «Лориан-Codex bridge», coordinated by parent; not a pre-merge PASS requirement. No access here. |
| Other environments | Physical desktop clipboard/audio/associations, Codex state-home and OpenCode provider compatibility remain separately unqualified. One accepted relay real turn remains preserved; no new GM calls. |

## Contract / review focus

1. Unit ControlGroup must equal original held child's `0::` membership, with original self/child cgroup namespace equality before/after bind. Only the retained caller-supplied original held PID is read; no process scan or secrets.
2. Parse bounded mountinfo (1MiB), membership (64KiB) and events (4KiB) with strict UTF8; unique eligible cgroup2 mount with root `/`, unambiguous mount ID/device and canonical absolute paths. Decode standard mountinfo escapes; reject traversal/deleted/malformed/missing/duplicate data. Non-root mounts conservatively refuse.
3. Pin readonly directory and events via component-wise openat NOFOLLOW/CLOEXEC; require cgroup2 fstatfs magic and statx type/inode/device/mount identity. No broad signals, cgroup writes or delegated setup. No public test switch.
4. Every sample rechecks self namespace, chosen mount mapping and pinned directory/events identity versus original path under retained mount descriptor. Fresh bounded positional read from original events fd; parse exactly one populated0/1. Changed/disappeared/replaced descriptor/path/mount, malformed events, read failure or stale sequence latches uncertainty; later Empty cannot repair it.
5. Fresh Empty only on still-bound original boundary, never Pruned; source is single-bind and disposed/faulted instances refuse. Existing scope-empty + native reap + actual I/O/disposal/ACK conjunction stays unchanged.

## Smallest implementation sequence

- [ ] Publish this source plan and exact safe capability evidence; independent Sol design/selection review.
- [ ] Add `BookOfEternityClient.Tests/GmMainSystemdCgroupSourceTests.cs`, new narrow `gm-main-systemd-cgroup-source` category and explicit selection. First causal test reaches missing concrete source (runtime assertion, not preparation/compile failure); run RED through scripts/test-csharp.ps1 and publish.
- [ ] Create `Services/GmRuntime/SystemdCgroupSource.cs` (parsers/original single-bind sampling + small kernel seam) and `LinuxSystemdCgroupFiles.cs` (read-only proc/descriptor implementation). Pure controlled frames and actual FD tests on owned regular fixture files prove bound/fresh/identity/error/cleanup behavior; actual native default rejects non-cgroup storage. These FD/parser tests are not cgroup/kernel scope qualification.
- [ ] Add two exact original neutral Bridge/coordinator scenarios to existing `SystemdControlledScenarioDriver`: concrete source with injected files on successful stop and replacement/read-fault retention. New `gm-main-systemd-cgroup-connected` category selects only these methods. Guardian proves own reaping separately from logical Uncertain. Exact existing public-selection refusal and cgroup-observation tests form a small `gm-main-systemd-cgroup-affected` selector; don't repeat the full S1 cohorts.
- [ ] Run PlanOnly/causal GREEN and ValidateCatalog discovery0 execution; independent source/selection review. Fix only causal/review defects with focused RED/GREEN; archive exact source pins, commands/counts/artifacts/guardian evidence.
- [ ] Independent evidence/metadata review; normal checkpoint/push exact remote/byte readback and new direct GitHub-only clean restoration; handoff. Do not enable SupportsPidfdScopes or public selector, implement S3, install manager or make GM requests.

No game/GM-authored contract changes: client-owned observation adapter, no prompt/mechanic/model/settings changes. Existing spec FR-009/012/013/014/015, main/worker/generation conjunction and trusted-local player governance preserved. This does not add save protection from the player. User authorized independent controlled/source progress under approved design; no new product decision is needed for conservative refusal while unqualified.

Design review Sol6.1/xhigh PASS at73d58e5e: pinned namespace FD to prevent reuse; before/after directory/events/path identity; behavioral causal baseline instead of type-presence assertion. Pure compileable source stub has no observation implementation; positive tests must fail. Connected fault must include second post-reap sample after initial Empty. Original public gates and full S2/S3 remain closed.

Causal baseline: preparation CS0539/0tests retained separately. After fixture Dispose correction, actual connected3/3 executed FAIL (source Bind explicitly not implemented), cleanup complete/3guardian ECHILD0emergency; runner failed before source29cases, those are unexecuted. No implementation behavior yet. Exact logs retained under recovery/systemd-s2a.
