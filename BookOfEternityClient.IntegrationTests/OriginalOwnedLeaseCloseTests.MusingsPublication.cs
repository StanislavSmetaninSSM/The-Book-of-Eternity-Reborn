using System.Diagnostics;
using System.Reflection;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class OriginalOwnedLeaseCloseTests
{
    [Theory]
    [InlineData("acquire", false)]
    [InlineData("acquire", true)]
    [InlineData("read", false)]
    [InlineData("read", true)]
    [InlineData("close", false)]
    [InlineData("close", true)]
    [InlineData("read_close", false)]
    [InlineData("read_close", true)]
    public async Task GuardianMusingsPublication_OriginalComparisonStorageFailureCannotEnterDomainRepair(string mode, bool unauthorized)
    {
        using var fixture = new GuardianSystemRegressionTests();
        await fixture.PrepareMusingsPublicationAsync(1);
        const string path = "game_state/meta/guardians.json";
        var armed = false;
        var cuts = 0;
        var mutations = 0;
        var originalReadCut = false;
        var primary = new IOException("Original guardian comparison " + mode + " failure.");
        var closeFailure = new IOException("Original guardian comparison close failure.");
        var closer = new ThrowingClose(closeFailure);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var allow = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        bool IsComparison() => new StackTrace().ToString().Contains("ReadCompletedGuardianMusingsForComparison", StringComparison.Ordinal) ||
            new StackTrace().ToString().Contains("ReadForComparisonAsync", StringComparison.Ordinal);
        var files = new FileSystemManager(fixture.FixtureRootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = () =>
                {
                    if (armed && mode == "acquire" && IsComparison()) { cuts++; throw primary; }
                    return Task.CompletedTask;
                },
                BeforeCanonicalReadOpenAsync = readPath =>
                {
                    if (!armed || readPath != path || !IsComparison()) return Task.CompletedTask;
                    cuts++;
                    if (mode == "read") throw primary;
                    if (mode is "close" or "read_close") { originalReadCut = true; entered.TrySetResult(); return allow.Task; }
                    return Task.CompletedTask;
                },
                AfterCanonicalReadInitialValidationAsync = readPath =>
                {
                    if (armed && originalReadCut && mode == "read_close" && readPath == path) throw primary;
                    return Task.CompletedTask;
                },
                BeforeCanonicalMutationAsync = _ => { if (armed) mutations++; return Task.CompletedTask; }
            });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var refresh = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
            new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance), validator, new Dictionary<string, string>());
        Assert.True(!refresh.Issues.Any(issue => issue.Severity == IssueSeverity.Error),
            string.Join("; ", refresh.Issues.Select(issue => issue.Code + ": " + issue.Actual)));
        Assert.NotNull(refresh.GuardianMusingsValidation);
        if (unauthorized)
        {
            var root = JsonNode.Parse((await files.ReadFileAsync(path))!)!;
            root["guardians"]![0]!["musings"]![0]!["thought"] = "Подмена прежней мысли.";
            root["activeGuardian"]!["musings"] = root["guardians"]![0]!["musings"]!.DeepClone();
            await files.WriteFileAtomicAsync(path, root.ToJsonString());
        }
        var before = Directory.GetFiles(fixture.FixtureRootPath, "*", SearchOption.AllDirectories).ToDictionary(p => p, File.ReadAllBytes);
        using var scope = validator.UseCompletedGuardianMusingsValidationScope(refresh.GuardianMusingsValidation);
        armed = true;
        var operation = Task.Run(() => validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles, [path])));
        FileSystemManager.CanonicalWriteLease? original = null;
        Exception? failure;
        try
        {
            if (mode is "close" or "read_close")
            {
                Assert.Same(entered.Task, await Task.WhenAny(entered.Task, operation).WaitAsync(TimeSpan.FromSeconds(12)));
                var timer = Stopwatch.StartNew();
                Task? comparison;
                do
                {
                    comparison = FindMusingsComparisonContinuation(allow.Task);
                    if (comparison is null) await Task.Delay(10);
                } while (comparison is null && timer.Elapsed < TimeSpan.FromSeconds(5));
                Assert.NotNull(comparison);
                (original, _) = InspectOriginalNestedOwningLease(comparison!, "GuardianMusingsCompletedValidation+<ReadForComparisonAsync>");
                Assert.Same(files, original.Owner);
                Assert.True(original.IsActive);
                Assert.Null(original.ExternalPublicationContext);
                original.ExternalPublicationContext = closer;
                allow.TrySetResult();
            }
            failure = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(12)));
        }
        finally
        {
            allow.TrySetResult();
            _ = await Record.ExceptionAsync(() => operation.WaitAsync(TimeSpan.FromSeconds(12)));
            armed = false;
        }
        Assert.True(cuts > 0);
        Assert.Same(mode == "close" ? closeFailure : primary, failure);
        if (mode == "read_close") Assert.Same(closeFailure, failure!.Data["CoordinatedLeaseReleaseFailure"]);
        Assert.Equal(mode is "close" or "read_close" ? 1 : 0, closer.Calls);
        if (original is not null) Assert.False(original.IsActive);
        Assert.Equal(0, mutations);
        Assert.Equal(before.Keys.Order(), Directory.GetFiles(fixture.FixtureRootPath, "*", SearchOption.AllDirectories).Order());
        foreach (var pair in before) Assert.Equal(pair.Value, File.ReadAllBytes(pair.Key));
        await using var released = await files.AcquireCanonicalWriteLeaseAsync().WaitAsync(TimeSpan.FromSeconds(3));
    }

    private static Task? FindMusingsComparisonContinuation(Task gate)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var queue = new Queue<object>(); queue.Enqueue(gate);
        var seen = new HashSet<object>(ReferenceEqualityComparer.Instance);
        while (queue.TryDequeue(out var value) && seen.Count < 200)
        {
            if (!seen.Add(value)) continue;
            if (value is Task task)
            {
                var state = task.GetType().GetField("StateMachine", flags)?.GetValue(task);
                if (state?.GetType().FullName?.Contains("GuardianMusingsCompletedValidation+<ReadForComparisonAsync>", StringComparison.Ordinal) == true)
                    return task;
                var continuation = typeof(Task).GetField("m_continuationObject", flags)?.GetValue(task);
                if (continuation is not null) queue.Enqueue(continuation);
            }
            else if (value is Delegate action)
            {
                foreach (var item in action.GetInvocationList()) if (item.Target is not null) queue.Enqueue(item.Target);
            }
            else if (value is System.Collections.IEnumerable list)
            {
                foreach (var item in list) if (item is not null) queue.Enqueue(item);
            }
            else if (value.GetType().Namespace?.StartsWith("System.Threading.Tasks", StringComparison.Ordinal) == true)
            {
                for (var type = value.GetType(); type is not null; type = type.BaseType)
                    foreach (var field in type.GetFields(flags | BindingFlags.DeclaredOnly))
                        if (field.GetValue(value) is { } next && (next is Task or Delegate || field.Name.Contains("continuation", StringComparison.OrdinalIgnoreCase)))
                            queue.Enqueue(next);
            }
        }
        return null;
    }
}
