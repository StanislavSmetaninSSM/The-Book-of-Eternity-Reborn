# T041-SYSTEMD-MAIN-S1 — bounded source/controlled handoff

Source issue: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Authorized design: `0868b805dc06f005d814d656db7da6d9ff10012c`.
Frozen runtime/tests: `b764117fbb15cc999100d42cee37400e0c65f245`.
Only S1 is implemented and qualified under controlled bus/cgroup observations.
The primary backend is **not publicly available or positively qualified on a
real user manager**. S2/S3 and overall T041 remain open.

## Implemented boundary

The existing factory, coordinator and Bridge retain one original native PTY
session and its checked, duplicated held pidfd. The scope wrapper is created
before attachment, so failed identity capture or uncertain attachment retains
that exact owner. There is no owner constructor from JSON, status, PID or a unit
name, no second launcher and no second journal. Schema1 remains unchanged.

`SystemdUserBus` binds the original bus instance/unique manager owner, UID/boot,
unit path/invocation and cgroup identity. The sd-bus codec transfers actual Unix
FDs in `PIDFDs` (`ah`), subscribes before creation and matches the original job
receipt, including a signal received before the method reply. Start is single
use; an unbound or replacement unit cannot receive StopUnit. The optional
`libsystemd.so.0` transport is compiled source only: its capability remains
`SupportsPidfdScopes=false`, and no real connection was opened in S1.

Prepared precedes held creation; successful original attachment precedes Running;
Running precedes one A1 release within the original held budget. The controlled
capability is internal, fixed-neutral and single use. Missing/unavailable or
consumed capability refuses before native preparation or a new Prepared record.
Ordinary `ProductionMainConfiguration.Resolve` still admits explicit
NativeLineage only; public SystemdUser and Auto remain closed. NativeLineage and
Windows ConPTY/Job routes retain their existing implementation.

The scope stop proof requires original manager/unit/cgroup authority, fresh
same-identity empty evidence, actual native exclusive reap, another fresh check
after native retirement, and original I/O/disposal before the existing Stopped
ACK. Stale/read-error/populated/pruned/replaced evidence, transport loss, capture
failure, ambiguous release/receipt, timeout or disposal failure latches typed
Uncertain. Narrow physical cleanup does not promote it. Expected bus closure
after successful disposal preserves the cached original proof through metadata
ACK debt. Main settlement does not clear independent worker/storage debt or
change main/worker/generation admission.

## Verification and retained failures

| Evidence boundary | Source | Result |
| --- | --- | --- |
| Contract codec/receipt/identity/capability | `ae92abc03e5085e6b774ecb73025bf3a52dddc3a` | 14/14 PASS; five relevant source/test files byte-identical at b764, so no unchanged rerun |
| Actual controlled held terminal and Bridge | `b764117fbb15cc999100d42cee37400e0c65f245` | 24/24 PASS |
| Exact affected native/fence/Windows source guards | same b764 | 4/4 PASS |
| Final PlanOnly | clean `f0bb821d5ea60999f0533e259441e0633847a344` | 42 cases/3 descriptors selected, zero executed, exit0 |
| Final ValidateCatalog | same f0bb; runtime b764 unchanged | 427 categories/11,176 methods/files, zero executed, exit0 |

The **42 distinct** successful cases are not a single 42-case final rerun.
The last runtime command executed 28/28 (24 controlled +4 affected) with complete
selection and cleanup. Its 26 process guardians reached actual ECHILD with zero
emergency signals, failures or deadlines. Real Bridge/T042 covers two inputs,
including Unicode, in one original process, manual draft/cancel/takeover/resize
and original stop/output/disposal/Stopped ACK. Controlled fault cases verify retained original
Uncertain, no release on failed admission and no stop against replacement.
The Windows check is a source guard, **not native Windows execution**.

Causal failures are retained rather than overwritten: missing PIDFDs/attachment,
held-empty acceptance, stale post-native manager evidence, repeated fixture
admission, failed native disposal and failed post-held manager capture. The last
capture RED used one guardian emergency signal and remains a logical failure.
AppContext/bin relocation, malformed shell category arguments, catalog shape and
an own scratch-cleanup error are separately recorded preparation/fixture errors.
The scratch error's planned28/executed4 run was incomplete, not a 28-case pass.

Recorded successful verification selections (the ae92 contract run also included
the then-current 19 controlled cases):

```powershell
pwsh -NoLogo -NoProfile -Command '& ./scripts/test-csharp.ps1 -Category @("gm-main-systemd-contract","gm-main-systemd-controlled-terminal")'
pwsh -NoLogo -NoProfile -Command '& ./scripts/test-csharp.ps1 -Category @("gm-main-systemd-controlled-terminal","gm-main-systemd-affected")'
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile tests/selection.json -PlanOnly -NoBuild
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -ValidateCatalog
```

Final manifest summaries/TRX give the executed sources, exact descriptors and
per-case counts; these are evidence commands, not instructions to rerun them.
Debian13/X64, SDK10.0.401, .NET8 runtime8.0.31,
PowerShell7.5.4 and `/usr/bin/cc` Debian14.2.0-19 were verified. Each native fixture
retains source/binary hashes, manifest and compiler flags/provenance.

- [Qualification and 20 current source hashes](recovery/systemd-main-s1-qualification.json)
- [Initial RED](recovery/evidence/systemd-main-s1/red/manifest.json)
- [Intermediate causal evidence](recovery/evidence/systemd-main-s1/progress/manifest.json)
- [Late RED and final GREEN](recovery/evidence/systemd-main-s1/final/manifest.json)
- [Discovery and preparation supplement](recovery/evidence/systemd-main-s1/discovery/manifest.json)

Independent actual **gpt-6.1-sol/xhigh** source/selection review at b764: PASS,
no remaining actionable S1 finding. Separate evidence review verified all853
pre-discovery gzip artifacts, 114 provenance records, 42 distinct TRX identities,
current pins, connected consumers and historical failure taxonomy: PASS for
source/controlled scope. The same reviewer passed the discovery supplement at
`feef8f7a5481ad46f47524958e534ec8f429967c`. Final metadata verdict is recorded
in the qualification carrier. No full suite, unchanged old cohorts,
real manager calls or new GM requests were performed. This client-owned adapter
does not change gameplay/schema/GM-authored commands; no GM prompt/example update
is required, as accepted in the design.

## Remaining implementation and environment gates

1. **S2:** implement the concrete read-only, descriptor-pinned cgroup source and
   qualify this same adapter with an already-running accessible user manager,
   optional sd-bus/FD API and observable cgroup-v2. Prove native ABI/FD delivery,
   original attachment and descendant retirement/PTY/I/O/ACK. The pinned removed
   directory witness remains closed until proved; `Pruned` currently refuses.
   Qualify the two-second manager stop versus observation deadline margin before
   enabling capability. No manager installation/setup or permission change is
   implied. This environment has no usable user manager per retained evidence;
   S1 made no new manager/cgroup probe.
2. **S3:** only after S2, consume qualified selection in the existing ordinary
   launcher/Bridge/console/daemon/QTE/repair/Load/status route. Explicit
   SystemdUser never downgrades. Approved Auto prefers qualified systemd or
   declares qualified NativeLineage before any launch; no post-start switch.
   Enabled production workers remain independently closed and require their own
   qualification. Native Windows and positive desktop/device environments also
   remain separate obligations.
3. **T043-RELAY-REUSABLE-DESIGN:** the owner requires retaining the existing relay
   and test profile in shared code during cleanup/merge. After S1, independently
   design the smallest reusable interfaces/entrypoint/docs slice preserving real
   request/response bytes, identity, normal game consumer and original cleanup.
   S1 does not add provider integrations, credentials, proxying forbidden hosts,
   new network grants or universal API support.

The existing plan/tasks preserve those open obligations. Cold exactly-once,
reboot/power-loss salvage and new gameplay are outside this slice. Overall
cross-platform completion and merge readiness are not claimed. The parent
coordinates the owner's conditional future merge after overall completion/final
verification and actual default-branch lookup; **no merge occurs here**.

Final metadata carrier requires normal non-force push, exact remote SHA/changed
file byte readback and fresh direct GitHub-only clean/tree/fsck restoration before
writer delivery. Exact final SHA and restoration results accompany that delivery.
Stop after this S1 handoff, before S2/S3 or relay runtime expansion.
