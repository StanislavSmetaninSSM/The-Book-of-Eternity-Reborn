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
public sealed class MortalLiveOutcomeApplicabilityTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ReadActualShortTurnWithoutDueCycleRequiresNoNewReceipt(bool priorReceipt)
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
            var control = await scheduler.BuildControlForNextTurnAsync("Mortal World");
            Assert.Equal(480, control.CurrentWorldTimeInMinutes);
            Assert.Equal(480, control.LastWorldSimulationTimeInMinutes);
            Assert.Equal(480, control.LastFactionSimulationTimeInMinutes);
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
            await scheduler.ApplyAcceptedTurnOutcomeAsync(control);
            if (priorHistory is not null) await WriteAsync(fs.GameSessionPath, MortalLivingWorldSource.HistoryPath, priorHistory);
            var clock = await fs.ReadFileBytesAsync(MortalLivingWorldSource.WorldTimePath);
            var schedule = await fs.ReadFileBytesAsync(ProgressionScheduleService.SchedulePath);
            Assert.Empty(MortalLivingWorldAssessment.Validate(new(manifest.SessionId, manifest.RequestId, 42,
                480, 500, control.LastWorldSimulationTimeInMinutes, control.LastFactionSimulationTimeInMinutes,
                control.WorldCycleMinutes, control.FactionCycleMinutes), null));
            ProgressionScheduleService.RequirePublishedMortalTreatmentOutcome(control, manifest.SessionId, manifest.RequestId, 42,
                original[ProgressionScheduleService.SchedulePath]!, clock, schedule, await fs.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
            var before = Inventory(root);
            var result = await MortalLiveEvidenceValidator.ReadAsync(baseline, fs.GameSessionPath, request);
            Assert.Equal(manifest.ManifestPayloadHash, result.SnapshotToken);
            Assert.Equal(priorReceipt ? 1 : 0, result.PriorCount);
            Assert.Equal(result.PriorCount, result.CurrentCount);
            Assert.False(result.PublicationAuthority);
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
