# T353 — missing asynchronous text admission: source findings

Issue1536, spec15r23/plan13r23. Inspected C# source is unchanged from compiled `5c25cc3b24e72f1dd32a266df079d42f4c8cead3`; source investigation started at metadata `aac4cc67d80d850b50445bbb03ec48d9e3583a25`. T352 acceptance is saved in [T352-final-review.json](T352-final-review.json). No build, test, native diagnostic probe or game run was performed for T353. Counterexamples below are source deductions, not claimed executed regressions.

The proposed direct reuse of the `FileExists` absence protocol is rejected. It would remove supported recovery, ownership and generation fences. Keep the existing missing-text quiescence fallback.

## Measured reason for this investigation

T352 positive actual history consumer: 6,264 storage-evidence spans /7.6596399s within25.7718096s;3,682under the pending query and2,582under other timer parents. Ordinary existence223calls /95.8486ms. All intervals/ancestry are preserved in T352 artifacts. Timer ancestry is not a native/C#call stack. These measurements select a substantial component for investigation, not its historical cause or a proven optimization.

## Different admission contracts

Paths below are relative to the repository root; line numbers refer to the unchanged C# revision.

| Responsibility | Actual source |
| --- | --- |
| Public async text: unleased `null` always acquires publication quiescence and physically rereads | `BookOfEternityClient/Core/FileSystemManager.cs:1517–1545` |
| Existing existence protocol: fresh second presence probe, target/ancestor mutation snapshots, pending query, conditional quiescence | `BookOfEternityClient/Core/FileSystemManager.cs:1933–1999` |
| Pending query checks only local and physical publication roots | `BookOfEternityClient/Core/FileSystemManager.cs:4563–4566` |
| Main/browser preflight, actual main owner guard, worker inventory and main validation precede the canonical lock | `BookOfEternityClient/Core/FileSystemManager.MainRunFence.cs:96–145`; `BookOfEternityClient/Core/FileSystemManager.cs:4115–4147` |
| Physical canonical lock and root authority; worker context/root/pin checks | `BookOfEternityClient/Core/FileSystemManager.cs:4153–4227` |
| Current bound generation is checked before recovery/evidence classification | `BookOfEternityClient/Core/FileSystemManager.cs:4233–4236,4323–4336` |
| Entire browser namespace classification, current/original routing and mixed-protocol refusal | `BookOfEternityClient/Core/FileSystemManager.cs:4239–4284`; `BookOfEternityClient/Services/ExplorerLocalTurnRollbackArtifacts.BrowserProtocol.cs:23–44` |
| Supported load/worker/browser/direct-gacha evidence remains classified/refused in original order; trusted-local recovery remains real | `BookOfEternityClient/Core/FileSystemManager.TrustedLocalStorage.cs:38–114,141–155`; `BookOfEternityClient/Core/FileSystemManager.cs:4244–4293` |
| Post-recovery main condition plus current console rollback cohort ownership | `BookOfEternityClient/Core/FileSystemManager.cs:4294–4297`; `BookOfEternityClient/Core/FileSystemManager.ConsoleRollbackEvidence.cs:36–75` |
| Ordinary read checks physical path/file authority, but its absent return is not a substitute for these generation/ownership gates | `BookOfEternityClient/Core/FileSystemManager.OrdinaryReads.cs:88–154`; `BookOfEternityClient/Core/FileSystemManager.cs:690–725` |

The names “legacy” in storage code do not make current supported original recovery evidence disposable. This is not backward compatibility for hypothetical old saves.

## Source counterexamples to the shortcut

1. Keep the requested text absent and both pending roots empty. Add unresolved load or worker journal evidence in its supported root. Existing missing-text acquisition reaches `EnsureNoLegacyStorageEvidence` and refuses with evidence retained; the existence-style shortcut would return `null`. Unknown browser/direct-gacha members and unsigned console cohorts are independent examples outside the two-root pending query.
2. Bind an operation to a different/missing/malformed current generation. Ordinary namespace checks do not compare the expected generation. An unchanged mutation snapshot does not replace `EnsureBoundSessionOperationCanWrite`; the original fallback supplies it before recovery classification.
3. An external supported writer can hold its actual lease before publishing a journal. `PublishLocalCoreAsync` registers mutations and invokes pre-publication boundary hooks before journal-owner publication (`FileSystemManager.TrustedLocalStorage.cs:302–338`). In-process observation (`FileSystemManager.cs:397–406,427–485`) does not see another process's registration. Two absent probes and empty pending roots do not establish equivalent physical exclusion. Arbitrary direct filesystem writes are also outside that registry.

A broader unlocked “check all roots” branch still omits physical synchronization and main/worker/generation/console admission. Building a replacement admission protocol is outside this investigation and is not justified by the measured repetitions.

## Minimal tests for any future admission change

Use per-case ordinary physical roots and direct typed `FileSystemManager` calls. A real writer or real generation is part of the subject only where required. No GameEngine, world/turn generation, private/internal reflection, frozen proof construction or fabricated successful journal authority.

| Contract | Required independent fixture/control |
| --- | --- |
| Stable missing, present, empty text | Actual return; current acquisition count; file/directory/G invariance |
| Non-publication recovery evidence | Separate unresolved load, worker, unknown browser/direct-gacha and unsigned console cases; exact retained bytes and original refusal |
| Operation binding | Replaced/missing/malformed generation, including invalid adjacent evidence to preserve error precedence |
| Actual writer exclusion | Writer already active; registration in a typed follow-up gap; writer in another process paused before journal publication |
| Publication visibility/recovery | Quarantine gap, completion after initial absence, unknown/uncertain evidence retained |
| Namespace authority | Target/ancestor directory, link or inaccessible entry; no weakening of refusal |
| Existing owner | Actual ambient owner does not reacquire; no lease held across a consumer that legitimately reacquires |

Existing fixture patterns are in `FileSystemManagerTests.cs`: quarantine4134, publication after absence4233, pre-journal file4263/directory4376, owned lease4547 and registration gap4629. They are not claimed to cover a changed async text path. T325/T348 transient pending-wave rejection and all real original/cold paths remain required where affected.

## Concrete next direction

Optimize a fresh storage-evidence probe itself while leaving every call site, invocation order, quiescence/recovery admission and freshness fence intact. `PhysicalFileAuthority.ProbeNamespaceEntryFromRoot` (`Core/PhysicalFileAuthority.cs:160–259`) currently probes the root/each intermediate namespace entry and then opens that same directory again. On Windows this combines separate native probe and stable-directory opens; `OpenStableDirectory:476–531` also calls `Directory.Exists` before its open. This is a source-level duplication candidate, not a demonstrated native-cost attribution.

Before implementation, determine whether one newly opened non-following directory handle can supply both classification and the existing stable authority without relaxing path/type/reparse/hard-link/access/missing/error behavior. Keep root-parent anchoring, final leaf classification, fresh handles per call and all disposal paths. Prefer a storage-evidence-only entry point to changing unrelated physical publishers. Keep the Linux traversal unchanged unless separately justified; Linux remains unexecuted here. No retained root/result cache, skipped evidence enumeration, native privilege change or new process mechanism.

The read-only consultation supports a narrow Windows successful-directory design: one `READ_ATTRIBUTES|LIST_DIRECTORY|SYNCHRONIZE`, share-read/write without share-delete, existing-only/no-follow/backup-semantics open. Preserve BOTH the opened-name/standard-info and normalized-name checks on that same handle, plus tag/type classification. Keep parent authority until the child has passed validation. Native errors2/3 remain missing; other combined-open errors execute the original probe/open fallback, including root disappearance handling. Never fall back after any successful-open validation failure.

The fallback matters because `FILE_LIST_DIRECTORY` and `FILE_READ_DATA` share bit1: a bad regular-file parent can permit attribute access but deny data access. Without fallback, combined access can change its original type/refusal classification. Both handle-path checks also remain meaningful. Primary references: [CreateFile access/sharing](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-createfilew), [access constants](https://learn.microsoft.com/en-us/windows/win32/fileio/file-access-rights-constants), [opened/normalized final path](https://learn.microsoft.com/en-us/windows/win32/api/fileapi/nf-fileapi-getfinalpathnamebyhandlew).

For d relative path segments with all root/intermediate directories present, source-derived successful traversal counts are2d+2native CreateFile attempts currently versus d+2proposed; root-parent and final leaf remain included. Failed-open fallback may add an attempt. These are structural expectations for small typed-observer tests, not measured speedup. Use per-invocation hooks that report actual attempts/access/share/flags and controlled validation boundaries; no global mutable observer, fake handles, kinds or native success. Fixture requirements include missing/ordinary/bad/reparse paths, access/sharing fallback, ancestor retention, post-open failure disposal, Unicode/path spelling and actual storage-evidence consumer refusal. Keep independent current-generation/pending controls where affected.

An implementation scope must explicitly settle these details in canonical spec/plan/tasks before code. Restrict the new traversal entry to storage-evidence probes; other generation/deletion/publication callers keep their existing traversal. Eliminating a second open necessarily changes internal observation timing during artificially timed ACL changes; do not claim equivalence for every such transition or a filesystem snapshot. The supported gates and original error classes must remain. No implemented primitive or measured speedup is claimed by this document.

Independent complex review pending. T345 uncertain root and previous raw evidence remain retained. Original/full-owner/five/Linux qualification remains open.
