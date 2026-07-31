# StateDistributor transaction-lease remediation

## Scope

Архитектурный разбор finding #2 из независимого review issue #1500. Изменения
исходников и тестов не выполнялись. Цель remediation: исключить ситуацию, в
которой rollback `StateDistributor` перезаписывает уже принятый concurrent
canonical writer, сохранив обязательные load/worker-apply recovery semantics и
не создав reentrant deadlock на canonical lock.

## Подтверждённая причина

`StateDistributor.DistributeAsync` реализует одну логическую операцию как набор
независимых lock-интервалов:

- backup каждого target: `BookOfEternityClient/IO/StateDistributor.cs:42-48`;
- read/modify/write каждого target: `StateDistributor.cs:50-55` и `:111-237`;
- output writes: `StateDistributor.cs:272-309`;
- rollback каждого backup: `StateDistributor.cs:71-73`;
- cleanup каждого backup: `StateDistributor.cs:60-62`.

Каждый public filesystem helper отдельно захватывает lease:
`FileSystemManager.cs:177-195`, `:1121-1125`, `:1143-1147`,
`:1164-1168`. Между этими интервалами другой cooperating writer может
закоммитить новое значение. `RestoreBackupCore` затем без CAS выполняет
`File.Copy(..., overwrite: true)` (`FileSystemManager.cs:1149-1155`) и
аннулирует этот commit.

Canonical lease невложенная: это `FileStream` с `FileShare.None`
(`FileSystemManager.cs:448-478`). Простая внешняя обёртка текущего кода
зависнет на первом public `CreateBackupAsync`, `WriteFileAtomicAsync`,
`RestoreBackupAsync` или `CleanupBackupAsync`, который попытается повторно
открыть тот же lock-файл.

## Минимальный корректный дизайн

### 1. Одна transaction lease

Public entrypoint должен иметь единственного владельца lease:

```csharp
public async Task<List<string>> DistributeAsync(GameResponse response)
{
    var plan = BuildDistributionPlan(response); // pure, no filesystem access
    await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
    return await DistributeAsync(lease, plan);
}
```

Нужен internal overload в стиле существующего
`StateManager.RefreshGameStateAsync(CanonicalWriteLease)`:

```csharp
internal Task<List<string>> DistributeAsync(
    FileSystemManager.CanonicalWriteLease lease,
    GameResponse response);
```

Он нужен не для текущего caller, а для безопасной композиции с будущей более
широкой QTE transaction. Caller, уже владеющий lease, обязан передать её, а не
вызывать public overload.

Lease должна удерживаться непрерывно:

1. от первого canonical baseline read/backup;
2. через все merge reads и authority reads;
3. через state и `output/*` writes;
4. через rollback либо commit decision;
5. через best-effort backup cleanup.

После освобождения lease метод не должен выполнять canonical mutation.

### 2. Recovery только на внешнем acquire

`AcquireCanonicalWriteLeaseAsync` уже восстанавливает active load journal и
active worker-apply journal до возврата lease
(`FileSystemManager.cs:473-478`). Поэтому:

- baseline разрешено читать только после успешного acquire;
- `StateDistributor` не должен повторно вызывать recovery;
- recovery failure должен выйти из public entrypoint до создания backup или
  первой canonical mutation.

Это сохраняет единый fail-closed recovery boundary и исключает чтение
полувосстановленного состояния.

### 3. Lease-aware helper graph без повторного acquire

Под outer lease нельзя вызывать ни один public writer wrapper. Минимально нужны
internal lease-aware overloads поверх уже существующих core-операций:

```csharp
internal string? CreateBackup(
    CanonicalWriteLease lease,
    string relativePath);

internal void RestoreBackup(
    CanonicalWriteLease lease,
    string backupFullPath,
    string originalRelativePath);

internal void CleanupBackup(
    CanonicalWriteLease lease,
    string backupFullPath);
```

Каждый overload сначала валидирует owner/active lease и затем вызывает
соответствующий core helper без захвата lock.

В `StateDistributor` lease должна передаваться далее в:

- `MergeFieldsIntoFile`;
- `ResolveCurrentSpiritFocusTierAsync`;
- `WriteOutputFiles`;
- backup capture, rollback и cleanup.

`MergeFieldsIntoFile` использует только
`ReadFileAsync(lease, ...)`/`WriteFileAtomicAsync(lease, ...)`.
`WriteOutputFiles` также использует только lease-aware writes.

### 4. Rollback только собственных завершённых mutations

Transaction должна хранить запись для каждого потенциального target:

```text
path
existedBefore
backupPath (nullable)
mutationApplied
```

Правила:

- backup/baseline создаётся под outer lease;
- `mutationApplied=true` выставляется только после успешного atomic write;
- rollback идёт в обратном порядке только по `mutationApplied=true`;
- существующий target восстанавливается из backup;
- target, отсутствовавший до transaction, удаляется;
- rollback продолжает независимые restores после отдельной ошибки;
- при rollback failure сохраняются backup/evidence и выбрасывается aggregate,
  содержащий original failure и все rollback failures.

Это устраняет текущий blind restore target, который distributor ещё не менял,
и корректно откатывает newly-created files.

### 5. Commit и backup cleanup не смешиваются

Успешное завершение всех writes является logical commit. Ошибка удаления
backup после commit:

- логируется как cleanup warning;
- оставляет backup для последующей ручной/служебной очистки;
- не запускает rollback уже committed state;
- не меняет успешный distribution result.

Сейчас cleanup находится внутри общего `try`, поэтому ошибка после удаления
части backups может запустить неполный rollback с уже утраченными before-images.

### 6. Почему не достаточно CAS без общей lease

CAS-only rollback мог бы не перезаписывать конкретное новое значение, но не
устраняет смешанный snapshot: writer всё ещё может войти между backup, authority
read, merge и соседним target write. Load также может заменить всю session между
этими фазами. Общая canonical lease является минимальной совместимой с
существующим writer protocol границей. Durable general-purpose journal для
`StateDistributor` усилил бы crash recovery, но не требуется для закрытия этого
finding и существенно расширяет scope.

## RED tests

Рекомендуемый файл:
`BookOfEternityClient.Tests/StateDistributorCanonicalLeaseTests.cs`.

Для детерминированного TDD seam достаточно:

```csharp
internal sealed class StateDistributorHooks
{
    internal Func<Task>? AfterBackupsCapturedAsync { get; init; }
    internal Func<string, Task>? AfterFileMutationAppliedAsync { get; init; }
    internal Func<Task>? BeforeBackupCleanupAsync { get; init; }
}
```

В GREEN эти callbacks выполняются внутри outer lease. Callback не должен сам
вызывать canonical writer: он только сигнализирует barrier. Concurrent writer
запускается отдельной task через второй `FileSystemManager` для того же root.
Все `TaskCompletionSource` создаются с
`RunContinuationsAsynchronously`; timeout используется только как watchdog, не
как доказательство ordering.

### RED-1: rollback не восстанавливает untouched backup поверх accepted writer

Имя:

```text
DistributeAsync_FailureAfterBackupCapture_DoesNotOverwriteConcurrentAcceptedWriter
```

Arrange:

1. Записать baseline в `game_state/world/weather.json`.
2. Создать distributor response с `WeatherChange`.
3. `AfterBackupsCapturedAsync` сигнализирует `backupsCaptured`, ждёт
   `releaseFailure`, затем выбрасывает injected `IOException`.
4. После `backupsCaptured` запустить через второй `FileSystemManager`
   canonical writer, записывающий marker `accepted-concurrent-writer`.
5. На writer instance установить
   `CanonicalWriteLockContendedAsync`, сигнализирующий `writerContended`.
6. Дождаться строго одного из событий: writer завершился либо получил
   contention. Затем отпустить `releaseFailure`.

Ожидание RED на текущем коде:

- writer завершится без contention между backup и rollback;
- distributor восстановит старый backup, хотя сам target не менял;
- final marker будет baseline, поэтому тест падает.

Ожидание GREEN:

- `writerContended` наступает, пока outer lease удерживается;
- distributor обрабатывает failure и завершает rollback;
- после освобождения lease writer коммитит marker;
- final marker равен `accepted-concurrent-writer`.

Обязательные assertions:

```csharp
Assert.Same(writerContended.Task, firstObserved);
await Assert.ThrowsAsync<IOException>(() => distributionTask);
await writerTask;
Assert.Equal("accepted-concurrent-writer", ReadMarker(finalJson));
```

`releaseFailure.TrySetResult()` должен находиться в `finally`.

### RED-2: rollback собственной mutation не аннулирует concurrent commit

Имя:

```text
DistributeAsync_FailureAfterFirstWrite_DoesNotRollbackConcurrentAcceptedWriter
```

Arrange аналогичен RED-1, но barrier находится в
`AfterFileMutationAppliedAsync(weatherPath)`. Callback ждёт release и затем
выбрасывает injected failure. Concurrent writer стартует, когда distributor уже
записал своё weather value.

Ожидание RED на текущем коде:

- merge write освободил свою отдельную lease;
- concurrent writer успешно записал accepted marker;
- catch/restore снова захватил lease и затёр marker baseline-значением.

Ожидание GREEN:

- concurrent writer получает contention;
- distributor под той же lease восстанавливает baseline;
- writer входит только после rollback и становится последним commit;
- final marker остаётся `accepted-concurrent-writer`.

Этот тест должен также проверять отсутствие `.backup.*` после успешного
rollback cleanup.

### RED-3: cleanup failure не превращает commit в rollback

Имя:

```text
DistributeAsync_BackupCleanupFailure_DoesNotRollbackCommittedDistribution
```

`BeforeBackupCleanupAsync` выбрасывает injected `IOException` после всех state
и output writes. Ожидание:

- `DistributeAsync` возвращает modified files;
- новое canonical значение сохранено;
- cleanup failure зарегистрирован как warning;
- backup evidence не удалено;
- rollback не выполнялся.

Текущий общий `try/catch` откатывает значение и повторно выбрасывает exception,
поэтому тест является RED.

## Обязательные GREEN guards

Эти тесты могут не быть RED на исходной реализации, но обязательны, чтобы
remediation не внесла deadlock/recovery regression.

### No reentrant acquisition

```text
DistributeAsync_OuterLease_UsesOnlyLeaseAwareFilesystemOperations
```

Запустить простую distribution без конкурента с
`CanonicalWriteLockContendedAsync`, который немедленно фиксирует unexpected
contention. Метод должен завершиться, callback не должен вызываться. Это ловит
наивную внешнюю обёртку, внутри которой остался хотя бы один public writer
wrapper.

### Recovery precedes baseline capture

```text
DistributeAsync_RecoversInterruptedWorkerApplyBeforeCapturingBaselines
DistributeAsync_RecoversInterruptedLoadBeforeCapturingBaselines
```

Создать active uncommitted journal существующими test operations и оставить
canonical bytes в частично применённом состоянии. На
`AfterBackupsCapturedAsync` проверить:

- journal уже разрешён/удалён;
- backup содержит восстановленный baseline, а не partial-applied bytes.

После injected distribution failure final state должен равняться recovered
baseline. При unresolved recovery callback вообще не должен быть достигнут и
ни один backup/target не должен измениться.

### Full phase exclusion

```text
DistributeAsync_ConcurrentWriterRemainsBlockedThroughOutputWriteAndRollbackDecision
```

Barrier после state write и перед output write подтверждает, что один и тот же
writer остаётся blocked не только во время merge, но до окончательного
commit/rollback decision.

## Acceptance criteria

Remediation закрывает finding только если одновременно доказано:

1. public `DistributeAsync` захватывает canonical lease ровно один раз;
2. recovery завершается до первого baseline read;
3. ни один helper под outer lease не захватывает её повторно;
4. cooperating writer не может завершиться между backup/read/write/rollback;
5. оба rollback RED-теста сохраняют accepted writer как final value;
6. rollback затрагивает только успешно изменённые distributor targets и
   удаляет newly-created targets;
7. cleanup failure не отменяет logical commit;
8. focused suite проходит без delay-based correctness assertions.

Минимальная focused-команда после реализации:

```powershell
dotnet test BookOfEternityClient.Tests\BookOfEternityClient.Tests.csproj --no-restore --filter "StateDistributorCanonicalLeaseTests|FileSystemManagerTests|StateManagerTests"
```
