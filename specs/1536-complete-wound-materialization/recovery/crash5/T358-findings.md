# T358 — choose role-preserving per-call Windows metadata observations

Source investigation only, spec15r26/plan13r26/META-1..4. Actual Spec Kit setup-plan/setup-tasks/prerequisites and T358-consistency completed. Parent inspected shared callers/ordinary readers/runtime sources/categories; separate bounded consultation used actual gpt-6-astra/high `/root/consult_t358_path_metadata`. This consultation is not independent acceptance. No C# edits, build, test, PlanOnly, native probe or cleanup.

## Measured reason and preserved boundary

Accepted T357 owner observation remains TIMEOUT/0completed/noTRX:666040events/333014closed/12open, response/repeat/closing unknown, root retained. Construction36.3548ms processwall; completed prefix11390async/31111sync reads. T357-findings retains every group and scope; these timings include observers and cannot identify native syscall cost. T328 previously deferred ResolvePath metadata folding; its root/error/dangling risks are now the explicit subject. No unchanged owner replay is selected here.

Local installed .NET8 runtime is8.0.31 (read-only dotnet --list-runtimes). Current compiled44320b27e/source unchanged. Candidate investigation targets only repeated metadata within one synchronous EnsureNoExistingReparsePoint call. All calls before/after open, recovery, current generation, lease/main/worker ownership and proof/continuation fences stay fresh at their original positions. No stored metadata or authority between components, calls, operations or owners.

## Existing shared contract

| Source | Preserve |
| --- | --- |
| FileSystemManager.cs ResolvePath:690 | Null check, trim/separators, rooted/colon/dot/empty-segment rejection, containment, returned path and errors. |
| EnsureNoExistingReparsePoint:761 root | Only directory presence is queried. Root-as-file is not newly rejected; a missing/error-suppressed root does not itself stop descendant walking. Root-equals-path returns after root handling. |
| Same helper segments | Directory-first, file-second only if directory false; both false terminate. Ordinary intermediate file is not newly rejected by this helper. Positive reparse handling retains same message/path. |
| EnsureRuntimePathIsSafe:5852 | Full-path normalization/containment and Client runtime authority label; generation/staging/lock/publication/transaction callers keep their existing invocations. |
| OrdinaryReads async | Resolve/scope, each admission resolve+scope, before-open hook, cancellation/lease checks, fresh pre-open resolve+scope, after-open hook, byte copy, completion scope+resolve, final lease. |
| OrdinaryReads sync/nullable | Initial resolve/scope, per-attempt admission, fresh scope before open, post-open hook, existing retry/data-error order, completion scope+resolve+lease. No new synchronous before-open hook. |
| Presence/diagnostic/original/stream readers | Their distinct lease/recovery/absence/data contracts and completion/disposal remain. |
| Unrelated classifiers | IsReparsePoint, TrustedLocalFileScope.Probe/Linux statx, physical native opens/storage-evidence fold and NativeLineage/native supervisor unchanged. |

## Reject GetAttributes-first shortcut

Its first exception is not equivalent to suppressed static existence. A concrete native ERROR_OPERATION_ABORTED maps to OperationCanceledException; static existence can return false for that failure. Catching only IO/access/argument exceptions introduces a new observable error. Missing-heavy paths would also construct exceptions. [Exception mapping](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/Common/src/System/IO/Win32Marshal.cs).

## Selected bounded design for later implementation

Retain the walker and root/segment short-circuit roles. On Windows, create a new DirectoryInfo for the current component, Refresh once, inspect Exists, and only when true immediately inspect cached Attributes. No hook/callback/await/second Refresh between those steps. A successful ordinary non-reparse observation may replace only the subsequent redundant IsReparsePoint query. Reparse or uncertain positive attributes use the ORIGINAL IsReparsePoint at that logical position, with its failure propagating. For a segment whose directory observation is false, repeat with a new FileInfo; never add a file observation at the root. Both false still stop traversal. Objects are discarded after the component decision. Non-Windows executes the exact old algorithm.

Windows metadata APIs share FillAttributeInfo and its existing GetFileAttributesEx/FindFirstFile fallback; use the managed APIs rather than introducing native classification. [Windows attribute implementation](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/Common/src/System/IO/FileSystem.Attributes.Windows.cs).

FSI Refresh(false) stores its metadata/error. Exists rejects errors and missing attributes; successful Attributes reuses that observation. Never read Attributes after false, which could expose a previously suppressed failure. False is not authority to skip later validations. [FSI implementation](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/System.Private.CoreLib/src/System/IO/FileSystemInfo.Windows.cs).

New observation constructors normalize paths. Limit expected ArgumentException/IOException/UnauthorizedAccessException fallback to the NEW observation, then execute the exact old per-component decision OUTSIDE that catch; never catch its attribute failure or translate arbitrary errors to missing. A suppressed native abort is not a thrown OCE. Static File.Exists also rejects a normalized trailing separator; FileInfo.Exists does not. The actual walker reconstructs separator-free components, so preserve that invariant and use old decision on unexpected spelling rather than building a generic replacement. [DirectoryInfo](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/System.Private.CoreLib/src/System/IO/DirectoryInfo.cs), [FileInfo](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/System.Private.CoreLib/src/System/IO/FileInfo.cs), [static File](https://raw.githubusercontent.com/dotnet/runtime/v8.0.31/src/libraries/System.Private.CoreLib/src/System/IO/File.cs).

Supported stable behavior is preserved, including root typing asymmetry, missing/error suppression and positive reparse/fallback errors. Microscopic replacement or newly failing attributes between the old Exists and GetAttributes could previously be detected in that redundant second query. Folding does NOT reproduce every such schedule. Trusted-local checks already exclude hostile concurrent owner protection; persistent replacement at the actual before-open/after-open/mutation/completion boundaries still MUST refuse. If exact transient intra-walk error equivalence were required, both folding proposals would have to be rejected. No filesystem snapshot or lease-based immunity to direct mutation is asserted.

Expected managed metadata-query count per ordinary component: directory2→1, file segment3→2, missing segment2→2; root regular-file behavior unchanged. A Refresh can itself perform multiple native calls, so these are not syscall counts. Allocation/runtime overhead and wall-time benefit remain unmeasured.

## Minimal meaningful verification design

Later implementation must have one production-used internal walker/observation boundary with invocation-scoped typed observation diagnostics; no private reflection or whole game objects. Passive observers report actual adapter attempts and fallback calls, cannot modify the Refresh/Exists/Attributes interior and cannot change primary outcome. Detached explicit DATA/error tests exercise control/ordering without claiming native authority; real fresh tiny filesystem fixtures exercise the production adapter and counts.

- Root file versus intermediate file, root-equals-path, missing root/ancestor/leaf, first false segment stop and directory-first short circuit.
- No attribute request for false; ordinary cached attrs vs reparse/uncertain old fallback; original fallback exception identity/type/message. IO/access/argument/native-abort suppression model is distinct from final attribute failure.
- Depth/leaf real count cases must assert actual presence/refusal and bytes first, then causal count RED against old implementation and GREEN after fold. No helper-only mirror test. Real/dangling file/directory links and junctions compare exact old stable result, preserving outside sentinels; do not assume all dangling/root links reject.
- Fresh independent calls after same-name normal→link/type replacement. Existing actual async before-open/after-open junction controls, sync completion refusal and runtime root/lock/staging mutation confinement keep their real admission hooks; no manufactured proof.
- Retain exact old Linux branch and unchanged classifiers/native code by source equivalence; Linux remains UNRUN. Fresh XML-enabled selected-project builds and changed API XML inspection, full catalog and exact category inventories/PlanOnly, current source/8DLL/input bindings and whole raw evidence.

Narrow existing category portable-storage-read-construction-boundaries owns six relevant read methods. Select additional exact sync completion/runtime confinement/ordinary bytes+timestamp+lease/retry controls by changed contracts; do not run broad portable-* families just by name, nor repeat unchanged unrelated constructor/physical-probe cases. New fixture category stays independent and cheap. Separate coherent complex SOURCE/PREP review and saved remote/clean recovery precede at most one explicitly scoped changed component observation. Next production/task scope must spell out exact fallback/typed APIs/categories and RED/GREEN; this source spike authorizes NO implementation or runtime.

Independent separate actual Astra/high source findings review accepted without blocking findings at6c04e0f43; T358-final-review.json,489exactcleanrecovery and11independentsamples. This is not implementation/performance acceptance. No source42generation/rebind, original/five/Linux qualification, timeout increase, diagnostic quotas, cached checks or protected-root deletion.
