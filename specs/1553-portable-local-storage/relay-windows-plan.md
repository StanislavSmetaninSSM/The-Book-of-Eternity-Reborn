# Native Windows relay implementation plan

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

- [ ] Add native tests before implementation: help starts; independent processes
  contend on the gate and close refuses correctly; actual owned ConPTY preserves
  Unicode/astral/multiline bracketed paste without submitting; actual CR publishes
  exact bytes; real fixed helper success/error; close waits for child/output;
  original Job cleanup and console mode restoration.
- [ ] Run against the base and retain causal RED evidence.
- [ ] Implement `Terminal.read(timeout)` (`None` means no data),
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
