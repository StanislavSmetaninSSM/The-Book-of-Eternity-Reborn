using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

[Collection("desktop-helper-console")]
public sealed class MortalLiveEvidenceValidatorTests
{
    private static (string History, string Manifest, string Request, string Token) Evidence(LiveTurnPendingSnapshotManifest? supplied = null)
    {
        var manifest = supplied ?? new LiveTurnPendingSnapshotManifest
        { SessionId = "session", RequestId = "request", TurnNumber = 7, PlayerAction = "Ждать четыре часа", RequestTimestamp = "2026-11-18T00:00:00Z" };
        manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest,
            LiveTurnPreparationService.ManifestHashJsonOptions, value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash);
        var report = new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = "session", ["requestId"] = "request", ["turn"] = 7,
            ["fromMinute"] = 0, ["toMinute"] = 240, ["resolution"] = "noSignificantChange",
            ["audit"] = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
                (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "No significant bounded autonomous change." }).ToArray()),
            ["events"] = new JsonArray()
        };
        var built = MortalLivingWorldReceipt.Build(manifest.ManifestPayloadHash,
            new("session", "request", 7, 0, 240, 0, 0, 240, 1440), report, null);
        var request = JsonSerializer.Serialize(new { sessionId = manifest.SessionId, requestId = manifest.RequestId,
            turnNumber = manifest.TurnNumber, playerAction = manifest.PlayerAction, timestamp = manifest.RequestTimestamp, progressionControl = manifest.ProgressionControl });
        return (built.HistoryJson, JsonSerializer.Serialize(manifest, LiveTurnPreparationService.ManifestJsonOptions), request, manifest.ManifestPayloadHash);
    }

    [Fact]
    public void ProductionTypedReceiptAndManifestYieldDetachedCorrelatedObservation()
    {
        var evidence = Evidence();
        var result = MortalLiveEvidenceValidator.Validate(null, evidence.History, evidence.Manifest, evidence.Request);
        Assert.Equal("session", result.SessionId); Assert.Equal("request", result.RequestId);
        Assert.Equal(7, result.Turn); Assert.Equal(evidence.Token, result.SnapshotToken);
        Assert.Equal(0, result.PriorCount); Assert.Equal(1, result.CurrentCount);
        Assert.StartsWith("sha256:", result.ReceiptFingerprint);
        Assert.False(result.PublicationAuthority);
    }

    [Theory]
    [InlineData("fingerprint")]
    [InlineData("manifest-payload")]
    [InlineData("request")]
    [InlineData("action")]
    [InlineData("duplicate-request-property")]
    [InlineData("extra-receipt")]
    [InlineData("no-append")]
    public void TamperedOrUncorrelatedEvidenceRefuses(string fault)
    {
        var evidence = Evidence(); var history = evidence.History; var manifest = evidence.Manifest;
        var request = evidence.Request; string? prior = null;
        if (fault == "fingerprint") history = history.Replace("sha256:", "bad:");
        if (fault == "manifest-payload") manifest = manifest.Replace("Ждать четыре часа", "Изменённое действие");
        if (fault == "request") request = request.Replace("\"request\"", "\"foreign\"");
        if (fault == "action") { var node = JsonNode.Parse(request)!; node["playerAction"] = "Другое действие"; request = node.ToJsonString(); }
        if (fault == "duplicate-request-property") request = request.Replace("{", "{\"requestId\":\"duplicate\",", StringComparison.Ordinal);
        if (fault == "extra-receipt")
        {
            var root = JsonNode.Parse(history)!; root["entries"]!.AsArray().Add(root["entries"]![0]!.DeepClone()); history = root.ToJsonString();
        }
        if (fault == "no-append") prior = history;
        Assert.Throws<InvalidDataException>(() => MortalLiveEvidenceValidator.Validate(prior, history, manifest, request));
    }

    [Fact]
    public async Task ActualCliBoundaryReturnsOneRefusalForTamperedTypedEvidence()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "live-evidence-cli", Guid.NewGuid().ToString("N"));
        var previous = Console.Out;
        using var output = new StringWriter();
        try
        {
            var evidence = await PrepareReadEvidenceAsync(root);
            var baseline = Path.Combine(root, "before"); var current = Path.Combine(root, "current");
            Directory.CreateDirectory(Path.Combine(baseline, "game_state/control"));
            Directory.CreateDirectory(Path.Combine(current, "game_state/world"));
            await File.WriteAllTextAsync(Path.Combine(baseline, "game_state/control/pending_turn_snapshot.json"), evidence.Manifest);
            await File.WriteAllTextAsync(Path.Combine(current, MortalLivingWorldSource.HistoryPath), evidence.History.Replace("sha256:", "bad:"));
            var request = Path.Combine(root, "actual-request.json");
            await File.WriteAllTextAsync(request, evidence.Request);
            Console.SetOut(output);
            var exit = await MortalLiveEvidenceValidator.RunAsync(baseline, current, request);
            Assert.Equal(2, exit);
            Assert.Equal("{\"valid\":false,\"publicationAuthority\":false,\"errorCode\":\"typed_evidence_invalid\"}" + Environment.NewLine,
                output.ToString());
        }
        finally
        {
            Console.SetOut(previous);
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public async Task ExplicitCapturedInputsAreReadWithoutMutation()
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "live-evidence-readonly", Guid.NewGuid().ToString("N"));
        try
        {
            var evidence = await PrepareReadEvidenceAsync(root);
            var baseline = Path.Combine(root, "before"); var current = Path.Combine(root, "current");
            Directory.CreateDirectory(Path.Combine(baseline, "game_state/control"));
            Directory.CreateDirectory(Path.Combine(current, "game_state/world"));
            await File.WriteAllTextAsync(Path.Combine(baseline, "game_state/control/pending_turn_snapshot.json"), evidence.Manifest);
            await File.WriteAllTextAsync(Path.Combine(current, MortalLivingWorldSource.HistoryPath), evidence.History);
            var request = Path.Combine(root, "actual-request.json"); await File.WriteAllTextAsync(request, evidence.Request);
            var before = Inventory();
            var result = await MortalLiveEvidenceValidator.ReadAsync(baseline, current, request);
            Assert.Equal(evidence.Token, result.SnapshotToken); Assert.False(result.PublicationAuthority);
            Assert.Equal(before, Inventory());
            string[] Inventory() => Directory.GetFiles(root, "*", SearchOption.AllDirectories).OrderBy(p => p, StringComparer.Ordinal)
                .Select(p => p + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))).ToArray();
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }
    /// <summary>Provides current selected-original coverage for the real IO boundary; pure Validate fixtures remain independent.</summary>
    private static async Task<(string History, string Manifest, string Request, string Token)> PrepareReadEvidenceAsync(string root)
    {
        var fs = new FileSystemManager(Path.Combine(root, "source"), NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();
        await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\"}");
        await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":0}");
        var service = new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance);
        var control = await service.BuildControlForNextTurnAsync("Mortal World");
        var manifest = new LiveTurnPendingSnapshotManifest
        { SessionId = "session", RequestId = "request", TurnNumber = 7, PlayerAction = "Ждать четыре часа", RequestTimestamp = "2026-11-18T00:00:00Z",
          ProgressionControl = control, OriginalPathPresenceV1 = new(StringComparer.OrdinalIgnoreCase) };
        foreach (var logical in new[] { "game_state/meta/soul_state.json", MortalLivingWorldSource.WorldTimePath,
            ProgressionScheduleService.SchedulePath, MortalLivingWorldSource.HistoryPath, MortalLivingWorldSource.ReportPath })
        {
            var bytes = await fs.ReadFileBytesAsync(logical);
            manifest.OriginalPathPresenceV1[logical] = bytes is not null;
            if (bytes is null) continue;
            var selected = "game_state/control/pending_turn_snapshot/" + logical;
            manifest.Files[logical] = selected;manifest.SnapshotFileHashes[logical] = Convert.ToHexString(SHA256.HashData(bytes));
            var target = Path.Combine(root, "before", selected);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, bytes);
        }
        await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":240}");
        await service.ApplyAcceptedTurnOutcomeAsync(control);
        foreach (var logical in new[] { MortalLivingWorldSource.WorldTimePath, ProgressionScheduleService.SchedulePath })
        {
            var target = Path.Combine(root, "current", logical);Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            await File.WriteAllBytesAsync(target, (await fs.ReadFileBytesAsync(logical))!);
        }
        return Evidence(manifest);
    }

}
