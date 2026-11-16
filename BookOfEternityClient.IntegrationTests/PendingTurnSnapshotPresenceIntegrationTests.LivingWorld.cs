using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class PendingTurnSnapshotPresenceIntegrationTests
{
    private const string LivingFlagsPath = "game_state/world/world_flags.json";

    [Fact]
    public async Task LivingWorldSource_FirstCrossedCycleRequiresReportFromRealOriginal()
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await context.WriteExactJsonAsync(MortalLivingWorldSource.WorldTimePath, """{"currentTimeInMinutes":240}""");
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, lease);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_report_required");
        Assert.Null(result.Source);
    }

    [Fact]
    public async Task LivingWorldSource_NoChangeUsesSignedOriginalNotWorkingRollbackImage()
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await WriteLivingWorldReportAsync(context);
        await context.WriteExactJsonAsync(LivingFlagsPath, """{"worldStateFlags":[{"flagId":"player_choice","value":2}]}""");
        var before = await context.FileSystem.ReadFileBytesAsync(LivingFlagsPath);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, lease);
        Assert.Empty(result.Issues);
        var source = Assert.IsType<MortalLivingWorldSource>(result.Source);
        Assert.Equal(0, source.Context.OriginalMinute);
        Assert.Equal(240, source.Context.ResultMinute);
        Assert.Equal(1, source.ReadOriginalDocument(LivingFlagsPath)!["worldStateFlags"]![0]!["value"]!.GetValue<int>());
        Assert.Null(source.ReadOriginalDocument(MortalLivingWorldSource.HistoryPath));
        await source.RequireUnchangedInputsAsync(context.FileSystem, lease);
        Assert.Equal(before, await context.FileSystem.ReadFileBytesAsync(lease, LivingFlagsPath));
        Assert.Null(await context.FileSystem.ReadFileBytesAsync(lease, MortalLivingWorldSource.HistoryPath));
    }

    [Theory]
    [InlineData("requestId", "other-request")]
    [InlineData("sessionId", "presence-live ")]
    public async Task LivingWorldSource_ReportCannotBorrowAnotherSource(string field, string value)
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await WriteLivingWorldReportAsync(context);
        var report = (await context.ReadJsonAsync(MortalLivingWorldSource.ReportPath))!.AsObject();
        report[field] = value;
        await context.WriteExactJsonAsync(MortalLivingWorldSource.ReportPath, report.ToJsonString());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, lease);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_source_invalid");
        Assert.Null(result.Source);
    }

    [Fact]
    public async Task LivingWorldSource_ClientOwnedHistoryCannotBeAuthoredByReportWriter()
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await WriteLivingWorldReportAsync(context);
        await context.WriteExactJsonAsync(MortalLivingWorldSource.HistoryPath, """{"schemaVersion":1,"entries":[]}""");
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, lease);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_history_mutation");
        Assert.Null(result.Source);
    }

    [Fact]
    public async Task LivingWorldSource_CalendarOverrideCannotMasqueradeAsZeroElapsedTime()
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await context.WriteExactJsonAsync(MortalLivingWorldSource.WorldTimePath,
            """{"setWorldTime":{"year":3,"monthName":"Winter","dayOfMonth":4,"timeOfDay":"Morning"}}""");
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, lease);
        Assert.Contains(result.Issues, issue => issue.Code == "mortal_living_world_clock_unresolved");
        Assert.Null(result.Source);
    }

    [Fact]
    public async Task LivingWorldSource_OpeningEvidenceCannotSurviveSourceRewrite()
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await WriteLivingWorldReportAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, lease);
        Assert.Empty(result.Issues);
        var source = Assert.IsType<MortalLivingWorldSource>(result.Source);
        await context.FileSystem.WriteFileAtomicAsync(lease, MortalLivingWorldSource.ReportPath, "{}");
        await Assert.ThrowsAsync<InvalidDataException>(() => source.RequireUnchangedInputsAsync(context.FileSystem, lease));
    }

    [Fact]
    public async Task LivingWorldSource_CaptureCannotCrossPhysicalLease()
    {
        await using var context = await PrepareLivingWorldOriginalAsync();
        await WriteLivingWorldReportAsync(context);
        MortalLivingWorldSource source;
        await using (var first = await context.FileSystem.AcquireCanonicalWriteLeaseAsync())
        {
            var result = await MortalLivingWorldSource.PrepareAsync(context.FileSystem, first);
            Assert.Empty(result.Issues);
            source = Assert.IsType<MortalLivingWorldSource>(result.Source);
        }
        await using var second = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => source.RequireUnchangedInputsAsync(context.FileSystem, second));
    }

    private static async Task<ResourceMaterializationTestContext> PrepareLivingWorldOriginalAsync()
    {
        var context = await CreateSeededContextAsync();
        await context.WriteExactJsonAsync(SoulPath, """{"currentRealm":"Mortal World"}""");
        await context.WriteExactJsonAsync(MortalLivingWorldSource.WorldTimePath, """{"currentTimeInMinutes":0}""");
        await context.WriteExactJsonAsync(LivingFlagsPath, """{"worldStateFlags":[{"flagId":"player_choice","value":1}]}""");
        await PrepareLiveAsync(context);
        return context;
    }

    private static async Task WriteLivingWorldReportAsync(ResourceMaterializationTestContext context)
    {
        await context.WriteExactJsonAsync(MortalLivingWorldSource.WorldTimePath, """{"currentTimeInMinutes":240}""");
        var audit = new JsonArray();
        foreach (var scope in new[] { "npcs", "factions", "locations", "projects", "world" })
            audit.Add(new JsonObject { ["scope"] = scope, ["reason"] = "No significant autonomous result in this interval." });
        var report = new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = "presence-live", ["requestId"] = "presence-live-request",
            ["turn"] = 42, ["fromMinute"] = 0, ["toMinute"] = 240,
            ["resolution"] = "noSignificantChange", ["audit"] = audit, ["events"] = new JsonArray()
        };
        await context.WriteExactJsonAsync(MortalLivingWorldSource.ReportPath, report.ToJsonString());
    }
}
