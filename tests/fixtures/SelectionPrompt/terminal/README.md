## Current status (supersedes historical UNRUN notes below)

Bounded qualification independently accepted: outcome `78239928…93f8bb9`.
Policy corpus + transport7 passed; fresh owning build and official1/1 passed.
Exact pinned xterm/Unicode11 runtime corpus and real .NET CursorTop4 on PTY100x25
passed with upstream CPR, normal child/sidecar exit/EOF and separate child-configured
and owner-original termios restoration. This does NOT qualify fullroute or live.
Fullroute-only integration is now source WIP/UNRUN pending a separate source gate.

# Owned terminal fixture (SOURCE ONLY)

Exact packages: headless6.0.0 / unicode11 0.9.0, metadata gitHead f447274f430fd22513f6adbf9862d19524471c04. package-lock/provenance bind official registry SHA512 and all17 regular files. No dependency/peer graph. Registry signatures were retained externally but not cryptographically verified. `node_modules`, tarballs and raw evidence are external, never tracked. Already verified tarballs were extracted without running scripts; no JS import has yet occurred.

This block does NOT change `smoke.py`, live driver, raw grammar or production UI. It introduces independently reviewable admission, headless sidecar and transport source. Fullroute attachment remains gated behind package runtime DATA and actual.NET CursorTop outcomes. No claims of Unicode compatibility merely from package registration.

## Source responsibilities
- policy.mjs admits completeUTF8/control prefixes before upstream. No cursor model or reply construction. Partial UTF8/CSI blocks key/snapshot; EOF rejects partial state.
- sidecar.mjs provides READY, sequence-numbered WRITE/WRITTEN, SNAPSHOT and CLOSE/CLOSED. Fixed initialization occurs before READY (no separateINIT). One write callback fences the full admitted prefix. onData stores response and cursor AT that response instant, not final buffer state. Python alone writes PTY. No JS eval, arbitrary commands or network.
- owned_terminal.py verifies exact installed dependency inventory/hashes/links and owns oneNode process. It calls one injected owned-writer for replies and routekeys; this is a mechanical PTY writer, not an authority/CPR factory. It MUST be supplied by the caller with freshsize/noecho/originaldeadline checks, and caller drains PTY output before authorizing a fresh-frame key. Failure ownership stays with enclosing guardian; no failed constructor/close implies normal cleanup.
- test_policy.mjs source tests splitUTF8/controls, badmodes/query and incompleteEOF without importing npm packages.
- test_emulator.py source tests actual package callback/replyinstantstate, Unicode11 exactcells, wrap and scroll. No terminal/game process. It is UNRUN and expectedwidths are testoracles, not measured facts. Failure-only ownNode termination is not normal qualification.
- TerminalCursorProbe is an inert TestSupport role, actual Console.CursorTop, no game/services/filesystem fixture. New managed source requires fresh owningbuild AFTER review. Future runner must provide fresh absent result path, owned100x25/noechoPTY, fullraw capture, single serialized writer, original45/55/60bound and normalchildconfiguredrestore vsowneroriginalrestore independently. This source block does not launch it.

## Bounded next commands, not yet authorized execution
1. node test_policy.mjs.
2. python test_emulator.py <pinned-node> <external-verified-prefix>, underexisting nativeguardian with exactEOF/process evidence.
3. After cheapruntime/source review, fresh Unit/helper build for terminal-cursor-probe, then one ownedtiny realConsoleCursorTop qualification; no fullroute until independently accepted.

Before fullroute wiring, tests must additionally cover transport malformed/out-of-order/extraresponse, expiredwriter, wronggeometry/re-enabledECHO, callbackpending/partialprefix key refusal and cleanup failures. Do not substitute these source intentions for executed coverage.

## Complete-source qualification gate (UNRUN)

Early WIP transport gaps are now addressed in source: BOM-preserving admission,
seven independent transport DATA methods, nonblocking bounded pipe writes,
permanent protocol failure state, incremental raw/protocol evidence, and owned
abort/drain on constructor or runtime failure. `closed` means joined/drained
normal CLOSE, not merely an acknowledged command. Aborts retain separate evidence.
Node v24.22.3 is bound to SHA256 acbe4b8d9153b841ed0697cbfc4f93e767386620edb745ab9358306c0a6f31aa;
all inherited mandatory instrumentation is preserved. Extra linked/special package
entries are rejected before import, not silently excluded from inventory.

Official exact selection: `mortal-terminal-emulator-qualification`, ONE Fact.
It runs policy corpus, seven transport methods, then two separate ordinary owned
20-second guardians: emulator corpus (10-second work) and actual .NET CursorTop
(12-second work). The existing wrapper observes each guardian for 25 seconds.
This does not increase fullroute's separate 45/55/60 budgets. Set explicitly:
`BOE_TERMINAL_NODE` to the pinned absolute Node and `BOE_TERMINAL_PREFIX` to the
reviewed external extraction prefix. No runtime package is copied into Git.

`pty_probe.py` owns one PTY100x25 and noecho baseline, one .NET child and one Node
sidecar. Only upstream replies traverse its single writer; there are no user keys
or guessed CPR. The writer freshly checks size/noecho/original deadline before
write; this is a snapshot, not a claim of atomic exclusion of runtime changes.
Normal qualification requires actual .NET CursorTop=4, actual all-stdio TTY100x25,
one upstream CPR, child exit0, PTY EOF, child-restored configured baseline before
explicit owner-original restoration, and sidecar normal disposal/drain. Any failure
retains original evidence and only bounded exact-child cleanup; guardian remains
the final ownership fence. No existing smoke/live transport is wired to this API.

All new source remains UNRUN pending complete source review and save. The authored
Unicode width corpus is an oracle to test, not an assertion of measured support.
