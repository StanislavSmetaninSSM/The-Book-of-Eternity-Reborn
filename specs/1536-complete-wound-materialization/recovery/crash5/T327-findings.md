# T327 — direct completed-proof costs and diagnostic transport failure

Actual one-case run `20261021-013538-929-40540-8ce01254fcf74587be42bcbda6af5028-categories`: **FAIL**, test10.479839s. Compiled3afc3a6b20e98829a2c8ff569708aaaeb35c0f4c; saved metadatafae223f8465c8458936759aa2f7f5dfc9269e9ba. No source42/Stage43/GameEngine/original recovery ran.

One genuine applied-phase read completed3.8795222s (processCPU4.546875s); ordinary continuation4.9673492s (processCPU6.375s). CPU is entire test-process time across threads, including runtime work; it may exceed wall time and is not isolated function CPU. Three complete-proof observations executed. The recorded oracle confirms actual applied phase, successful one-notification continuation,220gamefiles/59directories and generation bytes unchanged. Source/archive/threeDLLs unchanged; own root removed and independently absent. Setup1.0802894s includes actual221file import/owner preparation.

All18,274timingrecords/9,137completedintervals/0unfinished survived in the3,355,166byte raw JSONL. RawZIP9E1C71A9CA798C38B7EE6884C64A41E5BE0807AE6C261907D96F80DF471A9E5B preserves13indexedfiles/CRC/hashes and actual TRX failure. The test then failed copying diagnostics to TRX: `File.ReadAllText` requested a sharing mode incompatible with the still-open writer, despite an earlier Flush. Therefore TRX contains no duplicate timing stream; the canonical parser explicitly accepts only this recorded failure when validating the independently indexed complete raw file. It never invents TRX timing records or changes FAIL to PASS.

| Measured work | Calls | Inclusive seconds |
| --- | ---: | ---: |
| Ordinary synchronous read |1078|3.390314|
| Ordinary asynchronous read |324|3.051333|
| Synchronous hook+byte copy |1078|0.045619|
| Asynchronous byte copy |231|0.037763|
| Synchronous scope construction |1078|0.877644|
| Synchronous admission |1078|0.898384|
| Synchronous completion checks |1078|0.848929|
| All three completed-proof footprint checks |3|1.490894|
| All three completed-proof progression checks |3|1.230510|
| All three completed-proof resource checks |3|1.160206|

Parents include children; rows must not be summed as independent work. The two ordinary read totals are disjoint invocations here,6.4416464s; measured copy/synchronous hook portions total0.0833819s (about1.29% of those read totals). These are instrumented single-run observations, not a controlled speedup estimate. Outside-measured-child values include observer output, scheduling and uninstrumented work. One treatment-history first invocation0.4135904s dominates its three-call0.4773177s total, so startup/runtime effects cannot be ignored.

The next source-supported candidate is repeated namespace metadata work inside these still-required reads. ResolvePath/EnsureNoExistingReparsePoint queries Directory.Exists/File.Exists/GetAttributes for the same component; TrustedLocalFileScope separately validates scope ancestors and requested files. Investigate a narrower coherent type observation/fallback preserving fresh entry/open/completion boundaries, missing/link/IO/error semantics and no cross-call authority cache. Do not remove the phase/continuation fences rejected by T325.

The harness source is corrected to Dispose the writer after owned cleanup and before ReadAllText, following the existing accepted component pattern. A separate actual Windows control used the entire exact3,355,166byte raw content: old open-writer read refused; after Dispose all bytes/hash matched; its own file removed. No game runtime was repeated. This verifies the sharing mechanism; corrected C# still awaits a fresh build and relevant later verification. Preserve this case as FAIL and reuse the measured data for the next design.

Original recovery remains FAIL/open; cold Windows five0/5qualified, LinuxUNRUN. No diagnostic quota, timeout increase or OS/scanner conclusion.
