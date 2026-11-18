using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Detached captured-input evidence only. These fixtures issue no live turn or publication authority.</summary>
[Collection("desktop-helper-console")]
public sealed class MortalLiveOutcomeApplicabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ReadActualShortTurnWithoutDueCycleRequiresNoNewReceipt(bool priorReceipt) => CheckAsync(priorReceipt, null);

    [Theory]
    [InlineData("legal-serialization")]
    [InlineData("legal-timestamp")]
    [InlineData("legal-appended-below-cycle")]
    public Task LegalScheduleAndVoluntaryReceiptRemainDistinct(string variant) => CheckAsync(true, variant);

    [Theory]
    [InlineData("previously-pending")]
    [InlineData("due")]
    [InlineData("backwards")]
    [InlineData("clock-command")]
    [InlineData("schedule-marker")]
    [InlineData("schedule-cycle")]
    [InlineData("schedule-context")]
    [InlineData("request-control")]
    [InlineData("raw-control-missing-default")]
    [InlineData("raw-control-unknown")]
    [InlineData("raw-both-controls-unknown")]
    [InlineData("absent-history-member")]
    [InlineData("absent-history-link")]
    [InlineData("absent-report-member")]
    [InlineData("absent-report-link")]
    [InlineData("living-report")]
    [InlineData("progression-report")]
    [InlineData("original-report")]
    [InlineData("history-malformed")]
    [InlineData("extra-receipt")]
    [InlineData("history-null")]
    [InlineData("history-bom-change")]
    [InlineData("history-deleted")]
    [InlineData("history-whitespace")]
    [InlineData("request")]
    [InlineData("hash")]
    [InlineData("uncovered-absence")]
    [InlineData("selected-path")]
    [InlineData("case-alias")]
    [InlineData("normalization-alias")]
    [InlineData("case-alias-hashes")]
    [InlineData("case-alias-presence")]
    [InlineData("normalization-alias-hashes")]
    [InlineData("normalization-alias-presence")]
    public Task InvalidCapturedOrCurrentOutcomeRefusesWithoutMutation(string fault) => CheckAsync(!fault.StartsWith("absent-history", StringComparison.Ordinal), fault);

    /// <summary>Runs one owned detached fixture; faults are applied only after lawful production prerequisites.</summary>
    private static async Task CheckAsync(bool priorReceipt, string? fault)
    {
        var root = Path.Combine(TestDiagnosticPaths.Root, "live-outcome-applicability", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var fs = new FileSystemManager(Path.Combine(root, "working"), NullLogger<FileSystemManager>.Instance);
            fs.EnsureDirectoryStructure();
            await fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\"}");
            await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":480}");
            var scheduler = new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance);
            if (fault == "previously-pending")
            {
                await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":0}");
                await scheduler.BuildControlForNextTurnAsync("Mortal World");
                await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":480}");
            }
            var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
            Assert.Equal(480, control.CurrentWorldTimeInMinutes);
            Assert.Equal(fault == "previously-pending" ? 0 : 480, control.LastWorldSimulationTimeInMinutes);
            Assert.Equal(fault == "previously-pending" ? 0 : 480, control.LastFactionSimulationTimeInMinutes);
            byte[]? priorHistory = priorReceipt ? PriorReceipt() : null;
            var original = new Dictionary<string, byte[]?>
            {
                ["game_state/meta/soul_state.json"] = await fs.ReadFileBytesAsync("game_state/meta/soul_state.json"),
                [MortalLivingWorldSource.WorldTimePath] = await fs.ReadFileBytesAsync(MortalLivingWorldSource.WorldTimePath),
                [ProgressionScheduleService.SchedulePath] = await fs.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath),
                [MortalLivingWorldSource.HistoryPath] = priorHistory,
                [MortalLivingWorldSource.ReportPath] = null
            };
            var baseline = Path.Combine(root, "captured");
            var manifest = new LiveTurnPendingSnapshotManifest
            {
                SessionId = "detached-session", RequestId = "short-request", TurnNumber = 42,
                PlayerAction = "Осмотреть двор", RequestTimestamp = "2026-11-18T00:00:00Z", ProgressionControl = control,
                OriginalPathPresenceV1 = new(StringComparer.OrdinalIgnoreCase)
            };
            foreach (var pair in original)
            {
                manifest.OriginalPathPresenceV1[pair.Key] = pair.Value is not null;
                if (pair.Value is null) continue;
                var selected = "game_state/control/pending_turn_snapshot/" + pair.Key;
                manifest.Files[pair.Key] = selected;
                manifest.SnapshotFileHashes[pair.Key] = Convert.ToHexString(SHA256.HashData(pair.Value));
                await WriteAsync(baseline, selected, pair.Value);
            }
            manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest,
                LiveTurnPreparationService.ManifestHashJsonOptions, value => value.ManifestPayloadHash,
                (value, hash) => value.ManifestPayloadHash = hash);
            await WriteAsync(baseline, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                JsonSerializer.SerializeToUtf8Bytes(manifest, LiveTurnPreparationService.ManifestJsonOptions));
            // Old receipt-only helper reads this flat history; the new full outcome
            // reader must prefer the explicit manifest-selected original above.
            if (priorHistory is not null) await WriteAsync(baseline, MortalLivingWorldSource.HistoryPath, priorHistory);
            var request = Path.Combine(root, "actual-request.json");
            await File.WriteAllTextAsync(request, JsonSerializer.Serialize(new
            {
                sessionId = manifest.SessionId, requestId = manifest.RequestId, turnNumber = manifest.TurnNumber,
                playerAction = manifest.PlayerAction, timestamp = manifest.RequestTimestamp, progressionControl = control
            }));
            await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, "{\"currentTimeInMinutes\":500}");
            if (fault != "previously-pending") await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
            if (priorHistory is not null) await WriteAsync(fs.GameSessionPath, MortalLivingWorldSource.HistoryPath, priorHistory);
            var clock = await fs.ReadFileBytesAsync(MortalLivingWorldSource.WorldTimePath);
            var schedule = await fs.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
            var assessment = MortalLivingWorldAssessment.Validate(new(manifest.SessionId, manifest.RequestId, 42,
                480, 500, control.LastWorldSimulationTimeInMinutes, control.LastFactionSimulationTimeInMinutes,
                control.WorldCycleMinutes, control.FactionCycleMinutes), null);
            if (fault == "previously-pending") Assert.NotEmpty(assessment);
            else Assert.Empty(assessment);
            if (fault != "previously-pending") ProgressionScheduleService.RequirePublishedMortalTreatmentOutcome(control, manifest.SessionId, manifest.RequestId, 42,
                original[ProgressionScheduleService.SchedulePath]!, clock, schedule, await fs.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
            if (fault is "due" or "backwards" or "clock-command")
                await fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, fault == "due" ? "{\"currentTimeInMinutes\":720}" :
                    fault == "backwards" ? "{\"currentTimeInMinutes\":479}" : "{\"currentTimeInMinutes\":500,\"timeChange\":0}");
            if (fault is "legal-serialization" or "legal-timestamp")
            {
                var node = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(schedule!))!.AsObject();
                if (fault == "legal-timestamp") node["lastUpdatedUtc"] = "2026-11-19T00:00:00.0000000Z";
                await WriteAsync(fs.GameSessionPath, ProgressionScheduleService.SchedulePath, Encoding.UTF8.GetBytes(node.ToJsonString()));
            }
            if (fault is "schedule-marker" or "schedule-cycle" or "schedule-context")
            {
                var node = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(schedule!))!.AsObject();
                if (fault == "schedule-marker") node["lastWorldSimulationTimeInMinutes"] = 500;
                if (fault == "schedule-cycle") node["worldCycleMinutes"] = 241;
                if (fault == "schedule-context") node["currentRealm"] = "Other realm";
                await fs.WriteFileAtomicAsync(ProgressionScheduleService.SchedulePath, node.ToJsonString());
            }
            if (fault is "legal-appended-below-cycle" or "extra-receipt")
            {
                var report = new JsonObject
                {
                    ["schemaVersion"] = 1, ["sessionId"] = manifest.SessionId, ["requestId"] = manifest.RequestId, ["turn"] = 42,
                    ["fromMinute"] = 480, ["toMinute"] = 500, ["resolution"] = "noSignificantChange",
                    ["audit"] = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
                        (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "No significant bounded autonomous change." }).ToArray()),
                    ["events"] = new JsonArray()
                };
                var built = MortalLivingWorldReceipt.Build(manifest.ManifestPayloadHash,
                    new(manifest.SessionId, manifest.RequestId, 42, 480, 500, 480, 480, 240, 1440), report,
                    priorHistory is null ? null : JsonNode.Parse(priorHistory)!.AsObject());
                var history = JsonNode.Parse(built.HistoryJson)!.AsObject();
                if (fault == "extra-receipt") history["entries"]!.AsArray().Add(history["entries"]!.AsArray()[^1]!.DeepClone());
                await fs.WriteFileAtomicAsync(MortalLivingWorldSource.HistoryPath, history.ToJsonString());
            }
            if (fault == "history-null") await fs.WriteFileAtomicAsync(MortalLivingWorldSource.HistoryPath, "null");
            if (fault == "history-bom-change") await WriteAsync(fs.GameSessionPath, MortalLivingWorldSource.HistoryPath,
                new byte[] { 0xef, 0xbb, 0xbf }.Concat(priorHistory!).ToArray());
            if (fault == "living-report") await fs.WriteFileAtomicAsync(MortalLivingWorldSource.ReportPath, "{}");
            if (fault == "progression-report") await fs.WriteFileAtomicAsync(ProgressionScheduleService.ReportPath, "{}");
            if (fault == "history-malformed") await fs.WriteFileAtomicAsync(MortalLivingWorldSource.HistoryPath, "{}");
            if (fault == "history-deleted") File.Delete(Path.Combine(fs.GameSessionPath, MortalLivingWorldSource.HistoryPath));
            if (fault == "history-whitespace") await WriteAsync(fs.GameSessionPath, MortalLivingWorldSource.HistoryPath, priorHistory!.Concat(new byte[] { 32 }).ToArray());
            if (fault == "request-control")
            {
                var node = JsonNode.Parse(await File.ReadAllTextAsync(request))!.AsObject();
                node["progressionControl"]!.AsObject()["worldCycleMinutes"] = 241;
                await File.WriteAllTextAsync(request, node.ToJsonString());
            }
            if (fault == "request") await File.WriteAllTextAsync(request, (await File.ReadAllTextAsync(request)).Replace("short-request", "foreign-request"));
            if (fault is "hash" or "uncovered-absence" or "selected-path" or "original-report")
            {
                if (fault == "hash") manifest.SnapshotFileHashes[MortalLivingWorldSource.WorldTimePath] = new string('F', 64);
                if (fault == "uncovered-absence") manifest.OriginalPathPresenceV1!.Remove(MortalLivingWorldSource.ReportPath);
                if (fault == "selected-path") manifest.Files[MortalLivingWorldSource.WorldTimePath] = MortalLivingWorldSource.WorldTimePath;
                if (fault == "original-report")
                {
                    var bytes = Encoding.UTF8.GetBytes("{}");var selected = "game_state/control/pending_turn_snapshot/" + MortalLivingWorldSource.ReportPath;
                    manifest.OriginalPathPresenceV1![MortalLivingWorldSource.ReportPath] = true;
                    manifest.Files[MortalLivingWorldSource.ReportPath] = selected;
                    manifest.SnapshotFileHashes[MortalLivingWorldSource.ReportPath] = Convert.ToHexString(SHA256.HashData(bytes));
                    await WriteAsync(baseline, selected, bytes);
                }
                manifest.ManifestPayloadHash = PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(manifest,
                    LiveTurnPreparationService.ManifestHashJsonOptions, value => value.ManifestPayloadHash, (value, hash) => value.ManifestPayloadHash = hash);
                await WriteAsync(baseline, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    JsonSerializer.SerializeToUtf8Bytes(manifest, LiveTurnPreparationService.ManifestJsonOptions));
            }
            if (fault?.Contains("alias", StringComparison.Ordinal) == true)
            {
                var raw = JsonNode.Parse(JsonSerializer.Serialize(manifest, LiveTurnPreparationService.ManifestJsonOptions))!.AsObject();
                var name = fault.StartsWith("case-", StringComparison.Ordinal) ? MortalLivingWorldSource.WorldTimePath.ToUpperInvariant() : "./" + MortalLivingWorldSource.WorldTimePath;
                var dictionary = fault.EndsWith("hashes", StringComparison.Ordinal) ? "snapshotFileHashes" :
                    fault.EndsWith("presence", StringComparison.Ordinal) ? "originalPathPresenceV1" : "files";
                raw[dictionary]!.AsObject()[name] = raw[dictionary]![MortalLivingWorldSource.WorldTimePath]!.DeepClone();
                await WriteAsync(baseline, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, Encoding.UTF8.GetBytes(raw.ToJsonString()));
            }
            if (fault is "raw-control-missing-default" or "raw-control-unknown" or "raw-both-controls-unknown")
            {
                var raw = JsonNode.Parse(JsonSerializer.Serialize(manifest, LiveTurnPreparationService.ManifestJsonOptions))!.AsObject();
                var rawControl = raw["progressionControl"]!.AsObject();
                if (fault == "raw-control-missing-default") Assert.True(rawControl.Remove("worldCycleMinutes"));
                else rawControl["unrecognizedControlMember"] = 1;
                // Keep the genuine original token: deserialization would previously erase this raw mutation.
                Assert.Equal(manifest.ManifestPayloadHash, raw["manifestPayloadHash"]!.GetValue<string>());
                await WriteAsync(baseline, LiveTurnPreparationService.PendingTurnSnapshotManifestPath, Encoding.UTF8.GetBytes(raw.ToJsonString()));
                if (fault == "raw-both-controls-unknown")
                {
                    var rawRequest = JsonNode.Parse(await File.ReadAllTextAsync(request))!.AsObject();
                    rawRequest["progressionControl"]!.AsObject()["unrecognizedControlMember"] = 1;
                    await File.WriteAllTextAsync(request, rawRequest.ToJsonString());
                }
            }
            if (fault?.StartsWith("absent-", StringComparison.Ordinal) == true)
            {
                var logical = fault.StartsWith("absent-history", StringComparison.Ordinal) ? MortalLivingWorldSource.HistoryPath : MortalLivingWorldSource.ReportPath;
                Assert.False(manifest.OriginalPathPresenceV1![logical]);
                Assert.False(manifest.Files.ContainsKey(logical));
                Assert.False(manifest.SnapshotFileHashes.ContainsKey(logical));
                var selected = "game_state/control/pending_turn_snapshot/" + logical;
                if (fault.EndsWith("-member", StringComparison.Ordinal)) await WriteAsync(baseline, selected, Encoding.UTF8.GetBytes("{}"));
                else
                {
                    var path = Path.Combine(baseline, selected);Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                    File.CreateSymbolicLink(path, Path.Combine(baseline, manifest.Files[MortalLivingWorldSource.WorldTimePath]));
                }
            }
            var before = Inventory(root);
            if (fault is not null && !fault.StartsWith("legal-", StringComparison.Ordinal))
            {
                var refused = await Assert.ThrowsAsync<InvalidDataException>(() => MortalLiveEvidenceValidator.ReadAsync(baseline, fs.GameSessionPath, request));
                if (fault?.Contains("alias", StringComparison.Ordinal) == true) Assert.Equal("Aliased original source key.", refused.Message);
                if (fault.StartsWith("raw-", StringComparison.Ordinal)) Assert.Equal("Raw captured/request progression control is not the exact closed control DTO.", refused.Message);
                if (fault.StartsWith("absent-", StringComparison.Ordinal)) Assert.Equal(fault.EndsWith("-link", StringComparison.Ordinal)
                    ? "Linked evidence path." : "Explicitly absent original has a physical captured member.", refused.Message);
                Assert.Equal(before, Inventory(root));
                return;
            }
            var result = await MortalLiveEvidenceValidator.ReadAsync(baseline, fs.GameSessionPath, request);
            var expectedKind = fault == "legal-appended-below-cycle" ? "ReceiptAppended" : "NotRequired";
            Assert.Equal(expectedKind, result.Kind);
            Assert.Equal(manifest.ManifestPayloadHash, result.SnapshotToken);
            Assert.Equal(priorReceipt ? 1 : 0, result.PriorCount);
            Assert.Equal(result.PriorCount + (expectedKind == "ReceiptAppended" ? 1 : 0), result.CurrentCount);
            Assert.False(result.PublicationAuthority);
            Assert.Equal(before, Inventory(root));
            await MortalLiveEvidenceValidatorTests.CheckWireAsync(baseline, fs.GameSessionPath, request, root, expectedKind);
            Assert.Equal(before, Inventory(root));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    /// <summary>Uses the existing typed receipt builder for a prior immutable receipt, not authority.</summary>
    private static byte[] PriorReceipt()
    {
        var report = new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = "detached-session", ["requestId"] = "prior-request", ["turn"] = 41,
            ["fromMinute"] = 0, ["toMinute"] = 480, ["resolution"] = "noSignificantChange",
            ["audit"] = new JsonArray(new[] { "npcs", "factions", "locations", "projects", "world" }.Select(scope =>
                (JsonNode)new JsonObject { ["scope"] = scope, ["reason"] = "No significant bounded autonomous change." }).ToArray()),
            ["events"] = new JsonArray()
        };
        return Encoding.UTF8.GetBytes(MortalLivingWorldReceipt.Build(new string('A', 64),
            new("detached-session", "prior-request", 41, 0, 480, 0, 0, 240, 1440), report, null).HistoryJson);
    }

    /// <summary>Writes fixture DATA only; no runtime source authentication is inferred.</summary>
    private static async Task WriteAsync(string root, string relative, byte[] bytes)
    {
        var path = Path.Combine(root, relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await File.WriteAllBytesAsync(path, bytes);
    }

    /// <summary>Retains exact input bytes to prove the detached reader does not mutate them.</summary>
    private static string[] Inventory(string root) => Directory.GetFiles(root, "*", SearchOption.AllDirectories)
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => path + ":" + Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();
}
