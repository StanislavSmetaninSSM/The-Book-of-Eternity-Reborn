using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    [Theory]
    [InlineData("direct", "case_alias")]
    [InlineData("direct", "literal_backslash")]
    [InlineData("direct", "outer_trim")]
    [InlineData("direct", "root_alias")]
    [InlineData("direct", "fixed_alias")]
    [InlineData("direct", "physical_case_alias")]
    [InlineData("direct", "physical_backslash")]
    [InlineData("direct", "physical_trim")]
    [InlineData("direct", "unicode")]
    [InlineData("intake", "case_alias")]
    [InlineData("intake", "unicode")]
    [InlineData("direct", "native_noncandidate")]
    public async Task OriginalSpiritualRawInventoryIsAdmittedBeforeRevokingActualCapture(string route, string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var mutations = new List<string>();
        var hooks = new FileSystemManagerHooks { BeforeCanonicalMutationAsync = path =>
            { mutations.Add(path); return Task.CompletedTask; } };
        Func<ResourceMaterializationTestContext, Task>? seed = route == "intake" ? SeedOriginalIntakeBaselinesAsync : null;
        var context = await CreateCompleteConflictFrameContextAsync(hooks, seedOriginalInputs: seed);
        using var owned = new OriginalFixtureCompletion(context.RootPath,
            () => context.DisposeAsync().GetAwaiter().GetResult(), _storageOutput.WriteLine);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var first = route == "intake"
            ? await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease)
            : await context.Validator.CaptureSpiritualOriginalTurnAsync(lease);
        AssertNoConflictFrameErrors(first.Issues);
        var old = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(first.Capture);
        Assert.True(old.IsCurrentOwner);
        var names = mode switch
        {
            "case_alias" => new[] { "lore/Entry.bin", "lore/entry.bin" },
            "literal_backslash" => new[] { "lore/odd\\leaf.bin" },
            "outer_trim" => new[] { "lore/trailing.bin " },
            "root_alias" => new[] { "Lore/unread.bin" },
            "fixed_alias" => new[] { LiveTurnPreparationService.TurnRequestPath.ToUpperInvariant() },
            "physical_case_alias" => new[] { "stories/Entry.jsonl", "stories/entry.jsonl" },
            "physical_backslash" => new[] { "input/odd\\leaf.bin" },
            "physical_trim" => new[] { "input/trailing.bin " },
            "native_noncandidate" => new[] {
                LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/native\\payload.bin",
                LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/A.bin",
                LiveTurnPreparationService.PendingTurnSnapshotDirectory + "/a.bin" },
            _ => new[] { "lore/ История.bin" }
        };
        foreach (var path in names)
        {
            var absolute = Path.Combine(context.FileSystem.GameSessionPath, path);
            Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
            await File.WriteAllBytesAsync(absolute, [0, 255, 42]);
        }
        Assert.True(old.IsCurrentOwner);
        var before = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var generation = File.ReadAllBytes(context.FileSystem.SessionGenerationPath);
        mutations.Clear();
        ValidationService.SpiritualOriginalTurnCaptureResult? result = null;
        var failure = await Record.ExceptionAsync(async () =>
            result = route == "intake"
                ? await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease)
                : await context.Validator.CaptureSpiritualOriginalTurnAsync(lease));
        var after = CaptureExactSpiritualFiles(context.FileSystem.GameSessionPath);
        var current = OriginalCaptureField(context.Validator, "_spiritualOriginalTurnCapture");
        _storageOutput.WriteLine(JsonSerializer.Serialize(new { root = context.RootPath, route, mode, names,
            failure = failure?.ToString(), issues = result?.Issues, before, after, mutations,
            sameOwner = ReferenceEquals(old, current), oldIsCurrent = old.IsCurrentOwner,
            generationBefore = generation, generationAfter = File.ReadAllBytes(context.FileSystem.SessionGenerationPath) }));
        Assert.Empty(mutations); Assert.Equal(generation, File.ReadAllBytes(context.FileSystem.SessionGenerationPath));
        Assert.Equal(before.Keys.OrderBy(x => x, StringComparer.Ordinal), after.Keys.OrderBy(x => x, StringComparer.Ordinal));
        foreach (var (path, bytes) in before) Assert.Equal(bytes, after[path]);
        Assert.Null(failure); Assert.NotNull(result);
        if (mode is "unicode" or "native_noncandidate")
        {
            AssertNoConflictFrameErrors(result.Issues);
            var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(result.Capture);
            Assert.True(capture.IsCurrentOwner); Assert.NotSame(old, capture); Assert.False(old.IsCurrentOwner);
            var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
            if (mode == "unicode") Assert.Equal(before[names[0]], draft.ReadImage(names[0]).Bytes);
            else foreach (var path in names) Assert.DoesNotContain(path, draft.PathInventory);
        }
        else
        {
            Assert.Null(result.Capture);
            Assert.Contains(result.Issues, issue => issue.Code == "spiritual_original_input_path_alias");
            Assert.Same(old, current); Assert.True(old.IsCurrentOwner);
        }
    }

    private static Dictionary<string, byte[]> CaptureExactSpiritualFiles(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories).ToDictionary(
            path => Path.GetRelativePath(root, path), File.ReadAllBytes, StringComparer.Ordinal);
}
