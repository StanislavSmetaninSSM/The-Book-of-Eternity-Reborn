# GM Worker Audit Append Starvation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Prevent same-process GM worker audit writers for one canonical root from starving at the cross-process file lock and silently losing accepted audit events.

**Architecture:** Store one asynchronous audit-admission gate on the already interned `CanonicalRootIdentity`, so every `FileSystemManager` for the same physical root shares admission without a permanent static path map. Self-acquiring audit entry points take admission before the canonical write lease; overloads that already receive a `CanonicalWriteLease` skip admission to preserve lock ordering.

**Tech Stack:** C#/.NET, `SemaphoreSlim`, xUnit, PowerShell 7, `scripts/test-csharp.ps1`

## Global Constraints

- Tracked task: [GitHub issue #1546](https://github.com/StanislavSmetaninSSM/The-Book-of-Eternity-Reborn/issues/1546).
- Approved design: `docs/superpowers/specs/2026-08-25-gm-worker-audit-append-starvation-design.md`.
- Preserve `specs/1500-complete-actor-materialization/spec.md` FR-020e and SC-005: concurrent appends lose no events, while diagnostic publication failure never revokes an accepted canonical operation.
- Keep the existing cross-process canonical lock authoritative; do not increase retry counts or introduce a global canonical-write scheduler.
- Do not acquire audit admission from an overload whose caller already owns a `CanonicalWriteLease`.
- Use PowerShell 7 and `./scripts/test-csharp.ps1`; Focused may use explicit headroom up to 15 minutes and PreMerge has the approved 30-minute ceiling tracked by #1547.
- Run tests serially. Do not launch a second lane while another lane or an editing/review agent is active.
- Do not commit, push, merge, close issues, or stage `.serena/` without a new explicit user request.
- No migration, compatibility path, gameplay contract, GM prompt, afterlife matrix, example manifest, or player-facing copy change is required.

---

### Task 1: Add a deterministic three-entry-point regression

**Files:**
- Modify: `BookOfEternityClient.Tests/GmWorkerAuditLogTests.cs`

**Interfaces:**
- Consumes: `FileSystemManagerHooks.BeforeCanonicalMutationBoundaryAsync`, `FileSystemManagerHooks.CanonicalWriteLockContendedAsync`, `FileSystemManager.CanonicalRootAuthorityIdentity`, and the three self-acquiring `GmWorkerAuditLog` append entry points.
- Produces: `SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock(AuditAppendEntryPoint)` and small event/dispatch helpers used only by this test class.

- [x] **Step 1: Add a theory that holds the first audit mutation and observes the second same-root writer**

Add this test beside `AppendEventAsync_ConcurrentWritersPreserveEveryEvent`:

```csharp
    [Theory]
    [InlineData(AuditAppendEntryPoint.BestEffort)]
    [InlineData(AuditAppendEntryPoint.CurrentSession)]
    [InlineData(AuditAppendEntryPoint.RequiredOnce)]
    public async Task SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock(
        AuditAppendEntryPoint entryPoint)
    {
        var root = CreateTempRoot();
        var firstWriterAtBoundary = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var holdFirstAuditWrite = 1;
        var canonicalContentionCount = 0;
        try
        {
            var firstFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (path.Equals(
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase) &&
                            Interlocked.CompareExchange(
                                ref holdFirstAuditWrite,
                                0,
                                1) == 1)
                        {
                            firstWriterAtBoundary.TrySetResult();
                            return releaseFirstWriter.Task;
                        }

                        return Task.CompletedTask;
                    }
                });
            var secondFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    CanonicalWriteLockContendedAsync = () =>
                    {
                        Interlocked.Increment(ref canonicalContentionCount);
                        return Task.CompletedTask;
                    }
                });
            string generation;
            await using (var writeLease =
                         await firstFs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = firstFs.GetOrCreateSessionGeneration(writeLease);
            }

            var firstAppend = AppendThroughEntryPointAsync(
                new GmWorkerAuditLog(firstFs),
                entryPoint,
                generation,
                CreateConcurrentAuditEvent(0));
            await firstWriterAtBoundary.Task.WaitAsync(TimeSpan.FromSeconds(5));

            var secondAppend = AppendThroughEntryPointAsync(
                new GmWorkerAuditLog(secondFs),
                entryPoint,
                generation,
                CreateConcurrentAuditEvent(1));
            var contentionBeforeRelease =
                Volatile.Read(ref canonicalContentionCount);

            releaseFirstWriter.TrySetResult();
            await Task.WhenAll(firstAppend, secondAppend);

            Assert.Equal(0, contentionBeforeRelease);
            var events = await new GmWorkerAuditLog(firstFs).ReadEventsAsync();
            Assert.Equal(2, events.Count);
            Assert.Equal(
                2,
                events.Select(item => item.EventId)
                    .Distinct(StringComparer.Ordinal)
                    .Count());
        }
        finally
        {
            releaseFirstWriter.TrySetResult();
            CleanupTempRoot(root);
        }
    }
```

- [x] **Step 2: Add the exact test-only helpers**

Add a second theory after the admission-order theory. It holds a best-effort
writer at the audit mutation boundary, starts one cancellation-aware entry
point through another `FileSystemManager` for the same root, cancels it while
it waits before the canonical lock, and verifies both cancellation and zero
same-process file-lock contention:

```csharp
    [Theory]
    [InlineData(AuditAppendEntryPoint.CurrentSession)]
    [InlineData(AuditAppendEntryPoint.RequiredOnce)]
    public async Task CancellationAwareAppendEntryPoints_CancelWhileWaitingForAdmission(
        AuditAppendEntryPoint entryPoint)
    {
        var root = CreateTempRoot();
        var firstWriterAtBoundary = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseFirstWriter = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var canonicalContentionCount = 0;
        try
        {
            var firstFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    BeforeCanonicalMutationBoundaryAsync = path =>
                    {
                        if (!path.Equals(
                                GmWorkerAuditLog.AuditLogPath,
                                StringComparison.OrdinalIgnoreCase))
                        {
                            return Task.CompletedTask;
                        }

                        firstWriterAtBoundary.TrySetResult();
                        return releaseFirstWriter.Task;
                    }
                });
            var secondFs = CreateFileSystem(
                root,
                new FileSystemManagerHooks
                {
                    CanonicalWriteLockContendedAsync = () =>
                    {
                        Interlocked.Increment(ref canonicalContentionCount);
                        return Task.CompletedTask;
                    }
                });
            string generation;
            await using (var writeLease =
                         await firstFs.AcquireCanonicalWriteLeaseAsync())
            {
                generation = firstFs.GetOrCreateSessionGeneration(writeLease);
            }

            var firstAppend = new GmWorkerAuditLog(firstFs).AppendEventAsync(
                CreateConcurrentAuditEvent(0));
            await firstWriterAtBoundary.Task.WaitAsync(TimeSpan.FromSeconds(5));

            using var cancellation = new CancellationTokenSource();
            var canceledAppend = AppendThroughEntryPointAsync(
                new GmWorkerAuditLog(secondFs),
                entryPoint,
                generation,
                CreateConcurrentAuditEvent(1),
                cancellation.Token);
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => canceledAppend);
            Assert.Equal(0, Volatile.Read(ref canonicalContentionCount));

            releaseFirstWriter.TrySetResult();
            await firstAppend;
            Assert.Single(await new GmWorkerAuditLog(firstFs).ReadEventsAsync());
        }
        finally
        {
            releaseFirstWriter.TrySetResult();
            CleanupTempRoot(root);
        }
    }
```

Add these members near the existing class helpers:

```csharp
    public enum AuditAppendEntryPoint
    {
        BestEffort,
        CurrentSession,
        RequiredOnce
    }

    private static async Task AppendThroughEntryPointAsync(
        GmWorkerAuditLog audit,
        AuditAppendEntryPoint entryPoint,
        string generation,
        WorkerAuditEvent auditEvent,
        CancellationToken cancellationToken = default)
    {
        switch (entryPoint)
        {
            case AuditAppendEntryPoint.BestEffort:
                await audit.AppendEventAsync(auditEvent);
                break;
            case AuditAppendEntryPoint.CurrentSession:
                Assert.True(await audit.AppendEventIfCurrentSessionAsync(
                    generation,
                    auditEvent,
                    cancellationToken));
                break;
            case AuditAppendEntryPoint.RequiredOnce:
                Assert.Equal(
                    GmWorkerAuditAppendDisposition.Appended,
                    await audit.AppendRequiredEventOnceIfCurrentSessionAsync(
                        generation,
                        auditEvent,
                        cancellationToken));
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(entryPoint));
        }
    }

    private static WorkerAuditEvent CreateConcurrentAuditEvent(int index) =>
        new()
        {
            EventId = $"worker_audit_admission_{index:D2}",
            EventType = "task-dispatched",
            WorkerId = "validation_repair_codex",
            TaskId = $"worker_task_admission_{index:D2}",
            TimestampUtc = "2026-08-25T00:00:00Z",
            Summary = $"Admission event {index}."
        };
```

- [x] **Step 3: Run the exact theory and confirm RED for the intended reason**

Run:

```powershell
pwsh ./scripts/test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GmWorkerAuditLogTests.SelfAcquiringAppendEntryPoints_SerializeBeforeCanonicalFileLock|FullyQualifiedName~GmWorkerAuditLogTests.CancellationAwareAppendEntryPoints_CancelWhileWaitingForAdmission" `
  -TimeoutMinutes 5
```

Expected: all five rows are discovered and at least one ordering row fails at
`Assert.Equal(0, contentionBeforeRelease)` because the second writer reaches
`CanonicalWriteLockContendedAsync`. The cancellation rows may also observe
forbidden contention. No timeout, build error, or unrelated failure is
accepted as RED evidence.

---

### Task 2: Add root-shared admission and wire every self-acquiring audit path

**Files:**
- Modify: `BookOfEternityClient/Core/CanonicalRootIdentity.cs`
- Modify: `BookOfEternityClient/Services/GmWorkers/GmWorkerAuditLog.cs`
- Test: `BookOfEternityClient.Tests/GmWorkerAuditLogTests.cs`

**Interfaces:**
- Consumes: the existing interned `FileSystemManager.CanonicalRootAuthorityIdentity` and existing canonical write-lease APIs.
- Produces: `CanonicalRootIdentity.EnterGmWorkerAuditAppendAdmissionAsync(CancellationToken)` returning an idempotently disposable `GmWorkerAuditAppendAdmissionLease`.

- [x] **Step 1: Add the per-root asynchronous admission lease**

Add the semaphore field to `CanonicalRootIdentity`:

```csharp
    private readonly SemaphoreSlim _gmWorkerAuditAppendAdmission = new(1, 1);
```

Add this method and nested lease type before `AttachRegistration`:

```csharp
    internal async ValueTask<GmWorkerAuditAppendAdmissionLease>
        EnterGmWorkerAuditAppendAdmissionAsync(
            CancellationToken cancellationToken = default)
    {
        await _gmWorkerAuditAppendAdmission.WaitAsync(cancellationToken);
        return new GmWorkerAuditAppendAdmissionLease(
            _gmWorkerAuditAppendAdmission);
    }

    internal sealed class GmWorkerAuditAppendAdmissionLease : IAsyncDisposable
    {
        private SemaphoreSlim? _admission;

        internal GmWorkerAuditAppendAdmissionLease(SemaphoreSlim admission)
        {
            _admission = admission;
        }

        public ValueTask DisposeAsync()
        {
            Interlocked.Exchange(ref _admission, null)?.Release();
            return ValueTask.CompletedTask;
        }
    }
```

The lease must release at most once. Do not dispose the semaphore while the weakly interned identity is reachable.

- [x] **Step 2: Admit the public best-effort path around its self-acquired canonical lease**

Replace the `writeLease == null` branch inside `AppendEventCoreAsync` with:

```csharp
            if (writeLease == null)
            {
                await using var admission = await _fs
                    .CanonicalRootAuthorityIdentity
                    .EnterGmWorkerAuditAppendAdmissionAsync(cancellationToken);
                await _fs.AppendFileAtomicAsync(
                    AuditLogPath,
                    line + Environment.NewLine);
            }
            else
            {
                await _fs.AppendFileAtomicAsync(
                    writeLease,
                    AuditLogPath,
                    line + Environment.NewLine,
                    cancellationToken);
            }
```

Keep validation and serialization before the `try`, and keep the existing best-effort exception filter unchanged.

- [x] **Step 3: Admit the session-bound path before it acquires the canonical lease**

At the start of the cancellation-aware `AppendEventIfCurrentSessionAsync`, add admission before `AcquireCanonicalWriteLeaseAsync`:

```csharp
        await using var admission = await _fs
            .CanonicalRootAuthorityIdentity
            .EnterGmWorkerAuditAppendAdmissionAsync(cancellationToken);
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync(
            cancellationToken: cancellationToken);
```

Keep the generation check and append under the same canonical lease.

- [x] **Step 4: Admit the required idempotent path before it acquires the canonical lease**

After `ValidateAuditEvent(auditEvent)` in `AppendRequiredEventOnceIfCurrentSessionAsync`, add:

```csharp
        await using var admission = await _fs
            .CanonicalRootAuthorityIdentity
            .EnterGmWorkerAuditAppendAdmissionAsync(cancellationToken);
```

Keep admission held across generation validation, equivalent-event lookup, and append. Keep required failures observable. Do not add admission to `AppendEventAsync(CanonicalWriteLease, WorkerAuditEvent)` or its `AppendEventCoreAsync` non-null branch.

- [x] **Step 5: Run the exact theory and confirm GREEN**

Run the same Focused command from Task 1.

Expected: `5/5` pass, timeout false, duplicate test IDs zero, and owned-process cleanup complete.

- [x] **Step 6: Run the retained 32-writer regression**

Run:

```powershell
pwsh ./scripts/test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GmWorkerAuditLogTests.AppendEventAsync_ConcurrentWritersPreserveEveryEvent" `
  -TimeoutMinutes 5
```

Expected: `1/1` pass with exactly 32 events and 32 distinct event IDs.

---

### Task 3: Verify cancellation, lock ordering, and the full affected surface

**Files:**
- Test: `BookOfEternityClient.Tests/GmWorkerAuditLogTests.cs`
- Test: `BookOfEternityClient.Tests/GmWorkerApplyGateTests.cs`
- Test: `BookOfEternityClient.Tests/GmWorkerProposalInboxTests.cs`
- Test: `BookOfEternityClient.Tests/SessionOperationContextTests.cs`
- Test: `BookOfEternityClient.Tests/StateDistributorCanonicalLeaseTests.cs`

**Interfaces:**
- Consumes: the implementation from Task 2.
- Produces: bounded verification evidence that all audit tests, adjacent lock-ordering tests, and the normal fast project remain green.

- [x] **Step 1: Repeat the exact deterministic theory three times serially**

Run the Task 1 Focused command three separate times, using `-NoBuild` only after the first clean build.

Expected for every run: `5/5` pass, timeout false, duplicates zero, and cleanup complete.

- [x] **Step 2: Run the complete audit-log class**

Run:

```powershell
pwsh ./scripts/test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GmWorkerAuditLogTests" `
  -TimeoutMinutes 5
```

Expected: all discovered `GmWorkerAuditLogTests` pass, including the retained 32-writer regression, stale-generation behavior, required idempotency, and required failure propagation.

- [x] **Step 3: Run adjacent canonical-lock and GM-worker controls**

Run:

```powershell
pwsh ./scripts/test-csharp.ps1 `
  -Lane Focused `
  -Filter "FullyQualifiedName~GmWorkerApplyGateTests|FullyQualifiedName~GmWorkerProposalInboxTests|FullyQualifiedName~SessionOperationContextTests|FullyQualifiedName~StateDistributorCanonicalLeaseTests" `
  -TimeoutMinutes 10
```

Expected: all selected tests pass without canonical/UI/session lock-order regressions.

- [x] **Step 4: Run one Fast checkpoint**

Run:

```powershell
pwsh ./scripts/test-csharp.ps1 -Lane Fast
```

Expected: exit `0`, timeout false, all discovered fast tests pass, duplicate IDs zero, cleanup complete.

Observed capacity disposition: the run reached the exact five-minute deadline
with all `2,166/2,166` completed results passing, zero failures, duplicate IDs
zero, and cleanup complete. The runner hard-rejects a larger Fast override.
Do not change the runner or micro-optimize the suite in #1546. The separately
tracked #1547 correction gives Task 4 PreMerge a 30-minute deadline; it includes
the entire fast project and must
be green before this step is complete. The checkpoint was executed, but it was
not accepted: this step becomes complete only if Task 4 PreMerge is green and
confirms the full fast project.

Accepted disposition (2026-08-26): the one final PreMerge completed the full
Fast project `4,339/4,339` across four TRX shards with zero failures and then
completed every remaining phase. This supplies the required accepted Fast
checkpoint without removing coverage or rerunning standalone Fast.

- [x] **Step 5: Inspect the implementation diff**

Run:

```powershell
git diff --check -- `
  BookOfEternityClient/Core/CanonicalRootIdentity.cs `
  BookOfEternityClient/Services/GmWorkers/GmWorkerAuditLog.cs `
  BookOfEternityClient.Tests/GmWorkerAuditLogTests.cs
git diff -- `
  BookOfEternityClient/Core/CanonicalRootIdentity.cs `
  BookOfEternityClient/Services/GmWorkers/GmWorkerAuditLog.cs `
  BookOfEternityClient.Tests/GmWorkerAuditLogTests.cs
```

Expected: no whitespace errors; the non-null write-lease overload has no admission acquisition; lock order is always admission then canonical lease.

---

### Task 4: Run final control and record handoff evidence

**Files:**
- Modify only after green verification: `docs/superpowers/plans/2026-08-14-complete-effect-materialization.md`
- Modify only after green verification: `docs/superpowers/plans/2026-08-15-unified-resource-authority.md`
- Modify only after green verification: `specs/1535-complete-effect-materialization/quickstart.md`
- Modify only after green verification: `specs/1535-complete-effect-materialization/tasks.md`
- Modify only after green verification: `specs/1543-unified-resource-authority/quickstart.md`
- Modify only after green verification: `specs/1543-unified-resource-authority/tasks.md`

**Interfaces:**
- Consumes: green Focused/Fast evidence and one fresh PreMerge result directory.
- Produces: exact durable evidence for #1535/#1543 and an uncommitted handoff for explicit user approval.

- [x] **Step 1: Run one final PreMerge control**

Run no additional Fast immediately before it:

```powershell
pwsh ./scripts/test-csharp.ps1 -Lane PreMerge
```

Expected: exit `0` within the approved 30-minute ceiling from #1547, all official results pass, timeout false, duplicate IDs zero, and owned-process cleanup complete.

- [x] **Step 2: Record exact result IDs and counts**

Update the two implementation plans and two quickstarts with the exact Focused/Fast/PreMerge result-directory names, totals, elapsed time, exit status, timeout status, duplicate count, and cleanup status. Record #1546 as the narrow pre-existing audit starvation repair that unblocked final #1535/#1543 verification.

- [x] **Step 3: Mark only evidence-backed Spec Kit tasks complete**

After PreMerge is green, mark `specs/1535-complete-effect-materialization/tasks.md` T118 complete while leaving the integration/user-approval task open. Mark `specs/1543-unified-resource-authority/tasks.md` T122 and T123 complete while leaving the commit/handoff task open.

- [x] **Step 4: Recheck documentation scope**

Record that #1546 changes only in-process audit admission and adds no GM-authored capability, Mortal World/afterlife state contract, pending/control schema, response, receipt, or player-visible command. Therefore no GM prompt, worked example, afterlife matrix, manifest, source guard, or migration update is required.

Accepted final evidence (2026-08-26):

- exact result:
  `TestResults/test-lanes/20260826-004148-035-3528-42f32409a6b34cf4bbb7950b7e8a10d7-premerge/summary.json`;
- exit `0`, timeout `false`, wall `00:21:59.6684306`, duplicate IDs `0`,
  owned-tree cleanup complete;
- all `26` TRX files completed: Fast `4,339/4,339`, core integration
  `2,269/2,269`, ProcessIntegration `508/508`, E2E `15/15`, total
  `7,131/7,131`;
- frontend verification passed `141/141`, and both C# builds had zero
  warnings/errors;
- no duplicate Fast preceded the control; the standalone capacity-invalid Fast
  was not rerun;
- #1535 T118 and #1543 T122-T123 are now evidence-backed complete; their
  owner-controlled integration tasks remain open.

#1546 remains internal harness engineering only. It changes neither a
GM-authored mechanic nor Mortal/afterlife state, pending/control, response,
receipt, command, or player-facing copy. No prompt, worked example, afterlife
matrix, manifest, source guard, migration, compatibility path, or GitHub
Actions change is required.

- [x] **Step 5: Perform the final workspace audit**

Run:

```powershell
git diff --check
git diff --cached --check
git status --short --branch
git rev-list --left-right --count origin/main...HEAD
```

Expected: both diff checks are clean; no unintended unstaged tracked changes remain; `.serena/` remains untracked and unstaged; branch divergence remains understood; no new commit, push, merge, or issue closure has occurred.

Observed final audit (2026-08-26): both unstaged and cached `git diff --check`
returned `0`; line-ending notices were informational only. The branch is
`1535-effect-materialization`, `origin/main...HEAD` is `0 29`, and no new commit
was created. There are `268` staged paths plus `31` expected later tracked
paths. The three non-Serena untracked task-owned files are
`ExplorerEffectPlayerCardBuilder.cs`, this plan, and its approved design;
`.serena/` remains separately untracked with eight files. Tracked `.serena/`,
`.github/`, and workflow matches are all zero. Issues #1535, #1543, #1546, and
#1547 remain open. No stage, commit, push, PR, merge, issue closure, repository
setting, or GitHub Actions mutation occurred.
