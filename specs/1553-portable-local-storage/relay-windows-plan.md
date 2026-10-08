# Native Windows relay implementation plan

## Current checkpoint: native Windows GREEN, review and Linux pending

Frozen runtime/tests: `0067874e15f479327dc37268fc891f51e3a2713d`.
Native Windows selected run: **24/24 PASS**, 7 native cases plus 17 shared worker
cases, complete selection, no timeout, original owned cleanup confirmed. The
four component ConPTY cases preserve exact Unicode/LF bytes and mode restoration,
real helper success/error, held-child close ordering and original Job retirement.
Component streams are explicitly prepared; the separate ordinary Bridge run
below has no such rebinding. [Safe evidence](recovery/windows-relay-20261008/manifest.json).

Actual ordinary Windows Bridge in a real outer PTY: native Python relay startup,
manual bracketed paste without submission, actual CR with exact Russian/emoji/LF
prompt, credential-free worker inspect/answer, unchanged real PowerShell helper
completion, queue closure, then original Bridge shutdown. Same retained run
`8a4743388aae441286cdb788f5fd4ec0`, original Job empty, cleanup true, authority
false, host exit0. Host/shell/relay PIDs 31908/34624/3412 are gone. No NeutralPackage,
console rebinding, model request or user save. State stays OperatorNotReady:
automatic T042 dispatch/readiness and accepted gameplay remain unqualified.

Independent implementation/evidence review is pending. Linux regression is
**not run** on HOME-PC. Existing source paths are retained but that is not a Linux
PASS. HTTP diagnosis has not started. The original report reproduction remains
limited to synthetic Save/Load and restart; compare clean valid data first.

Reproduce from a prepared native Windows checkout (Python3.14.7 on PATH,
PowerShell7.6.6, .NET SDK10.0.401/runtime8.0.31, Windows11Pro10.0.26200 x64):

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/windows-relay-20261008/selection-windows.json -Parallelism 1
```

The observed final run used `-NoBuild` after a fresh successful build of the
selected unit project and references (0 errors,41 warnings). Prior failed runs
remain documented below, including the invalid pre-product fixture REDs. Run
without `-NoBuild` after restoration. In a separately available native Linux
environment the requested regression command is:

```powershell
pwsh -NoLogo -NoProfile -File scripts/test-csharp.ps1 -SelectionFile specs/1553-portable-local-storage/recovery/windows-relay-20261008/selection-linux.json -Parallelism 1
```

`tests/selection.json` covers both platforms; category CI routing is metadata,
not permission to enable CI or evidence that either hosted platform executed.
Local final summary pins clean source0067874e, 24 cases, 46.04s wall time. Source
and original TRX hashes are retained in the evidence provenance. The report branch
is separately reviewed at68696416; this relay branch does not rewrite that report.

Primary API references: [ReadConsole](https://learn.microsoft.com/en-us/windows/console/readconsole),
[CancelSynchronousIo](https://learn.microsoft.com/en-us/windows/win32/fileio/cancelsynchronousio-func),
[PeekNamedPipe](https://learn.microsoft.com/en-us/windows/win32/api/namedpipeapi/nf-namedpipeapi-peeknamedpipe),
[Windows locking](https://docs.python.org/3/library/msvcrt.html#msvcrt.locking).

Source: [#1553](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1553).
Owner approved the bounded design and separate later HTTP diagnosis on 2026-10-08.
Branch: `codex/1553-windows-relay-20261008`; base `6381a9507baae6bb2531e22e9a0ace839f03985f`.

Goal: execute the maintained developer relay on native Windows without WSL,
installation, new credentials or model requests. Preserve shared request,
response, witness, once-only publication and original terminal ownership.

Architecture: keep protocol/consumer code shared; isolate console modes, Unicode
input/output and child pipe polling in `tools/gm-relay/relay_platform.py`. Preserve
POSIX termios/select behavior. Use Windows console APIs/stdlib and a bounded
one-byte Windows file lock on the same stable queue-local lock file. Never infer
child EOF from temporary lack of bytes or process exit alone.

## One bounded implementation block

- [x] Add native tests before implementation: help starts; independent processes
  contend on the gate and close refuses correctly; actual owned ConPTY preserves
  Unicode/astral/multiline bracketed paste without submitting; actual CR publishes
  exact bytes; real fixed helper success/error; close waits for child/output;
  original Job cleanup and console mode restoration.
- [x] Run against the base and retain causal RED evidence (fixture-only failures corrected separately below).
- [x] Implement `Terminal.read(timeout)` (`None` means no data),
  `Terminal.write(bytes)`, `Terminal.close()` with failure restoration, and
  `poll_child_output(stream)` distinguishing `None`, bytes and actual `b''` EOF.
  Integrate without packet/profile/authority changes. Add native execution_gate
  locking with typed timeout/release failure and never-unlinked lock identity.
- [ ] Run native category and shared worker-contract cases; inspect PlanOnly and
  catalog discovery. Linux selection: reusable-contract, reusable-main, transport,
  repair. Native Windows cannot claim Linux execution passed.
- [ ] Update README/spec/tasks/selection and evidence; publish WIP before GPT-6
  Astra XHigh review, resolve findings, verify remote SHA and GitHub-only recovery.

Review focus: split surrogate pairs; quiet-but-live pipes; exit with buffered
output; cross-process gate timeout without ACK; restoration after parser failure
and original Job retirement. Test barriers may control synthetic timing but
cannot fake real consumer success or original Job evidence.

## Boundaries and execution ledger

Client-owned transport only: no game schema, GM prompt, Mortal World or afterlife
example changes. Synthetic packets do not prove gameplay or automatic readiness.
HTTP is a later separate block; compare synthetic Save/Load/restart with a clean
valid root before attributing failures to general concurrency.

Ruling: execute inline as parent instructed; existing Spec Kit artifacts serve
as the durable plan/ledger instead of duplicate skill plans. Only selected
categories run; repository rules prohibit full suites. TDD skill was read; its
linked `writing-good-tests.md` resource was unavailable through both supplied
skill-relative resource forms. Behavioral assertions and causal RED still apply.

2026-10-08: branch published before implementation; remote base SHA verified.
Report predecessor separately reviewed at
`686964164641b3eb3a5a13ff8854b74aa1490189`, all 20 files recovered from GitHub,
19 payload hashes checked. Native relay implementation/tests/review pending.

Native RED at 68144f912b825bfa2d19f62ff53c57047e5c4b84: fresh unit build
0 errors/41 warnings; category gm-relay-native-windows completed 7/7 cases,
0 PASS/7 FAIL, no timeout, runner owned cleanup complete; all four ConPTY
original Jobs reported empty/cleanup true/authority false. Help fails on termios;
four terminal cases exit before readiness; gate has no native fcntl backend;
pipe adapter is absent. Original logs/TRX retained locally pending safe evidence
extraction. Candidate platform/gate code now implemented; GREEN not yet run.

Candidate 1ce0cea7414ae27eed549e62e5e3a4d9d1462b98: help, independent gate
and pipe tests PASS; four ConPTY cases still fail before relay startup. Diagnostic
wrapper proves GetConsoleMode on a standard handle fails before product import:
these four original RED cases were fixture startup failures, not causal relay
RED. Adjust fixture to the existing PowerShell shell route; do not weaken byte,
consumer, EOF or original Job assertions. The shared17 cases were not executed
because the native descriptor failed. All observed original Jobs cleaned up.
Canonical tools/relay_platform.py required per-command core.ignorecase=false
staging because repository also has Tools/. Both paths remain unchanged.

Native diagnosis: both fixture GetStdHandle values (8/12) fail GetConsoleMode
with Win32 error6 before relay import. Independent Astra XHigh source review
finds no basis for changing production ConPtySession. Component fixture now
explicitly opens attached CONIN$/CONOUT$, validates Win32 and actual CRT handles,
checks mode restoration after setup and restores fixture handles/streams. This
is not evidence for untouched ordinary Bridge startup; separate actual outer
PTY verification is required. No AllocConsole or ownership substitution.

Actual native outer-PTY probes demonstrate CRT getwch loses emoji and > and
converts LF to CR; native ReadConsoleW preserves Unicode/> but ConPTY still
converts LF to CR even with processed-input disabled. Candidate now uses a
bounded console reader thread, retained thread handle, cancellation/join before
mode restoration; CR maps to LF only inside Windows bracketed paste, never at
submit. POSIX transport stays unchanged. GREEN/review remain pending.

At 63e26d1ae0e0c75e6bf2c6d7754baf96a3058e35 native exact Unicode/multiline
paste/no-submit/CR-submit assertions pass in all three consumer cases. Invalid
input case passes normal reader shutdown and exact mode restoration. Overall
4/7 PASS, 3/7 FAIL: actual worker inspect stdout raises UnicodeEncodeError on
emoji under Windows redirected codepage. Fix worker JSON stdout as explicit UTF8
bytes; shared packet/identity stays unchanged. Shared17 still not run because
runner stops after native descriptor failure. Original Job cleanup complete.

Worker stdout follow-up at b8f700b6: bytes are now UTF8, but the existing .NET
Windows reader defaults to an OEM pipe encoding. JSON now uses standard ASCII
Unicode escapes (still UTF8), so ordinary JSON parsers preserve exact Unicode
without a global codepage change. Original prompt/packet file bytes untouched.
