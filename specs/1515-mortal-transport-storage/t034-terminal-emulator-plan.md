# T034 owned headless-terminal fixture plan

Status: DESIGN ONLY. No acquisition, adapter code, emulator execution or game run authorized by this document. Existing failed fixture data remain diagnostics, never predecessors. Default PIPE and earlier strict Screen modes remain unchanged. Scope is fullroute-only inert terminal capability, not game production UI.

## Dependency and provenance gate

Candidate exact releases: @xterm/headless 6.0.0 and @xterm/addon-unicode11 0.9.0. Official npm stable tags identify these versions; no beta/ranges. Local Node24.22.3/npm11.9.0 exist. No matching installed package was found in repo/shared/scratch inventory.

Before install, ordinary supported tools obtain official registry metadata for these exact versions: package identity, version, full dependency/peer graph, dist.tarball, dist.integrity and source provenance. Integrity is currently UNKNOWN and must not be invented. GitHub6.0.0 headless/package.json still says5.5.0, so tag alone does not establish npm artifact identity. Reject unexpected dependency graph/host/version; review actual metadata before execution. Pin metadata and SHA256, SRI in durable lock, all installed files and Node binary/version. No reliance on mutable latest at execution.

Use a fresh external prefix mortal-t034/terminal-deps with minimal private package.json, two exact dependencies, durable package-lock and isolated cache/HOME/userconfig/globalconfig. No existing npm credentials, auth tokens, project config ; mandatory NODE_OPTIONS/audit instrumentation must be preserved, or a conflict reported before proceeding. Do not print secret environment values. Preserve mandatory system/tool networking and proxies: the inherited npm proxy warning is not authorization to remove/bypass policy. Keep TLS checks. Official registry only, --ignore-scripts --no-audit --no-fund; no lifecycle scripts, global install, peer auto-surprises or fallback host after denial. If clean credential isolation conflicts with mandatory proxy configuration, stop and report rather than silently changing network policy. Normal use_default/auto-review; any denied operation stops that path.

Primary sources:
- https://www.npmjs.com/package/%40xterm/headless?activeTab=versions
- https://www.npmjs.com/package/%40xterm/addon-unicode11?activeTab=versions
- https://github.com/xtermjs/xterm.js/blob/6.0.0/typings/xterm-headless.d.ts
- https://github.com/xtermjs/xterm.js/blob/6.0.0/headless/package.json
- https://github.com/xtermjs/xterm.js/blob/6.0.0/addons/addon-unicode11/package.json
- https://github.com/xtermjs/xterm.js/blob/master/src/common/InputHandler.ts (DSR implementation; exact acquired release must be checked)

## Exact authority split and protocol

Python remains sole owner of the existing PTY and sole writer to its master. One Node child is an emulator sidecar only, within the same original guardian tree. It cannot open another terminal, launch game code, evaluate injected JS, select menus, fabricate acceptance or acquire ownership. All stdout is framed protocol, stderr diagnostics. No TCP listener/network is needed during execution.

Use sequence-numbered JSON lines over private pipes, base64 for raw byte payloads. Commands are INIT(fixed100x25), WRITE(seq,raw bytes), SNAPSHOT(after acknowledged seq), CLOSE. Responses are READY(pinned versions/Unicode profile), WRITTEN(seq, reply byte list, cursor/modes), SNAPSHOT(seq,cells/cursor), CLOSED. Reject malformed, repeated, missing or out-of-order sequence IDs and unsolicited output. No generic eval/command operation.

Instantiate Terminal with100 columns/25 rows, convertEol=false, window reporting disabled and no clipboard/link integrations. Load Unicode11Addon and set unicode.activeVersion='11'; allowProposedApi is explicitly needed for that documented interface. Check readback, do not infer active profile from loadAddon alone. No custom width provider or CPR implementation.

### Strict admission BEFORE automatic replies

Before any byte is fed to upstream, an incremental policy scanner admits the complete UTF8/control prefix against a fixed observed-sequence allowlist. This scanner is a policy boundary, not a screen/cursor emulator: it never calculates coordinates or rewrites output. Retain incomplete UTF8/ESC/CSI across chunk boundaries. Unknown/oversized/invalid/truncated-at-EOF sequences refuse; never feed an unadmitted query to upstream. The exact known controls include the existing source-bound SGR/cursor/erase/visibility and keyboard modes plus observed CSI6n. Any newly encountered sequence is a source-review blocker, not automatic widening. Alt-screen/conceal/OSC/DCS/window/clipboard controls remain disallowed for this fixture unless separately justified/reviewed.

Feed each admitted prefix once as Uint8Array, in order. One WRITE may be in flight. onData output is buffered, NEVER directly written to PTY. Only after Terminal.write callback confirms that entire admitted prefix was parsed may WRITTEN return replies. For split sequences the unadmitted suffix remains pending, no snapshot/key admission based on that incomplete suffix. New PTY data queued while a callback is pending must be processed in order before a fresh menu-based key is authorized.

A genuine DSR response comes exclusively from upstream onData for a previously admitted CSI6n. Verify one-to-one ordered pending-query accounting and response shape/range; capture upstream cursor state AT EACH onData response/query instant for audit, not the final WRITTEN snapshot if later text shares that batch, and never construct a replacement reply. Unexpected onData/onBinary bytes, missing/extra replies or unsupported queries fail closed. Preserve query offset, prefix sequence, raw response and upstream state. No guessed/hardcoded reply, no rewriting raw captures.

### Single same-PTY writer

Replies and route keys share one serialized Python writer. Before each write check unchanged original monotonic deadline, actual owned descriptor/geometry100x25 and ECHO/ECHONL disabled. Reply bytes are distinguished from user keys in the audit. Queries pending parsing/reply block route keys. Snapshot/menu selection must be tied to acknowledged byte-prefix and actual input cut, with no stale frame fallback. A reply cannot be redirected to another owner/PTY. The noecho snapshot is not a claim of atomic exclusion of child termios changes.

## Qualification and observer limits

Do not replace strict menu admission with substring matching. Earlier raw modes remain unchanged. Fullroute emulator-aware observer must bind rendered cells to an acknowledged fresh byte epoch, exact labels/order/selection and the existing finite route; the semantic archive/transfer receipts remain independent. Emulator state is terminal DATA, not session authority. Any extra control required by actual Options repaint needs explicit source/negative coverage before widening.

Cheap corpus first: actual Russian/emoji scenario strings (⚙️, ⚖, ⚔️, 📦, arrows), combining marks, VS16, wide characters, column99/100 wrap, CRLF versus LF, clear/home/cursor moves, scrolling and cursor reporting. Verify active Unicode11 and exact cursor-cell results, with chunk splits at every UTF8/control boundary. Include missing/duplicate/out-of-order protocol frames, unadmitted query/unknown controls, partial EOF, reply before callback, key before pending query settles, wrong PTY size, reenabled echo and expired deadline. Unicode11 is not a blanket grapheme/emoji correctness guarantee: if exact scenario disagrees, stop; do not silently switch addon/profile.

Then one tiny real .NET Console.CursorTop fixture under owned100x25/noecho PTY: print source-shaped text, query actual cursor, record returned result and prove normal exit/EOF/configured-baseline restoration. This is not a mocked CursorTop or patched game. Separate source review and official bounded selection required before this execution. Only after its outcome gate may the actual fullroute fixture run again. No live launch follows automatically.

## Deadlines, cleanup, cost

Retain original45s route work,55s guardian,60s observer. Sidecar startup/readiness and protocol drain count inside work budget; do not restart clock. CLOSE fences all pending writes/replies, dispose listener/addon/terminal, close pipes, await sidecar exit and drain stderr/stdout. Python closes/joins only its owned child/sidecar; original guardian remains final process-tree authority. PTY child configured-baseline check precedes separate owner-original restoration. Any sidecar failure, unprocessed suffix, undelivered reply, nonzero child, missing EOF or cleanup failure prevents normal success. Timeout/emergency remains separately reported, not repaired into success.

Expected implementation cost: two pinned JS packages and one small framed sidecar plus Python adapter/tests; no native-PTY package, X server, custom terminal engine or game UI changes. Exact download/disk size and dependency graph remain metadata-gated. No managed rebuild unless the tiny real.NET fixture introduces a new compiled test role; reuse current build only when its compiler/output joins remain exact. Preserve every prior failed outcome.

## Focused review clarifications
Discovery links above do not prove acquired implementation. API/DSR/source joins must be checked against the exact acquired npm artifact and its release provenance, not master or a mismatched tag. Preserve all mandatory networking, proxy, audit and NODE_OPTIONS instrumentation while isolating user credentials; stop on conflict. Metadata/artifact acquisition only is approved, not execution or installation.

## Acquired artifact facts (no package execution)
Official exact registry metadata and tarballs downloaded with ordinary curl under inherited system environment; no proxy/NODE_OPTIONS/config changes. Both packages declare no dependency or peer graph. Tarball SHA512 SRI and SHA1 match official metadata; package.json names/versions match. Registry signature records retained but signatures not independently cryptographically verified.
- @xterm/headless@6.0.0: 477641 compressed bytes; SHA256 `07e4970b1674e7ef6cbd57c8c17746eaadcd41aa7df5b33695fd649e6ec4d78a`; SRI `sha512-5Yj1QINYCyzrZtf8OFIHi47iQtI+0qYFPHmouEfG8dHNxbZ9Tb9YGSuLcsEwj9Z+OL75GJqPyJbyoFer80a2Hw==`; 7 declared regular files.
- @xterm/addon-unicode11@0.9.0: 145829 compressed bytes; SHA256 `b665667792f916873fe946ba98cc98a1a742af08e163fb66e1c0809c120ca42e`; SRI `sha512-FxDnYcyuXhNl+XSqGZL/t0U9eiNb/q3EWT5rYkQT/zuig8Gz/VagnQANKHdDWFM2lTMk9ly0EFQxxxtZUoRetw==`; 10 declared regular files.
Both metadata gitHead values: f447274f430fd22513f6adbf9862d19524471c04. Exact SRI-covered source map contains InputHandler.deviceStatus6 using activeBuffer.x/y+1 and triggerDataEvent; headless public API forwards onData and supports write callback/dispose. Unicode addon acquired source registers UnicodeV11 through terminal.unicode.register; activation/readback compatibility still requires future inert execution. Extracted files are inspection-only outside dependency installation prefix; no npm install, import, require or JS execution occurred. verified-artifacts.json SHA2560c88d595c6d6995573d868a774caf270c69eb050010874ee06a2435800f69ecb records every acquired file hash.
